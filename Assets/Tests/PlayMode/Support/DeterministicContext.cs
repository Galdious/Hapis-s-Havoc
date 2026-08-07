using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HapisHavoc.Tests
{
    /// <summary>
    /// Pins every source of frame-to-frame and run-to-run variation for the duration of a
    /// capture, then restores it on Dispose.
    ///
    /// The capture itself is driven by an explicit Camera.Render() into a fixed RenderTexture,
    /// NOT by the frame loop, so a capture never depends on how many frames happened to elapse.
    /// That is the single most important design decision here: batchmode runs uncapped, so
    /// anything phase-locked to the frame loop is not reproducible.
    /// </summary>
    public sealed class DeterministicContext : IDisposable
    {
        // Portrait is the shipping target and the default for every existing golden. Landscape
        // is opt-in per context, for the board-shape study - a fixed const could not express it.
        public const int Width = 1080;
        public const int Height = 1920;

        /// <summary>Render target size actually in use. Defaults to the portrait constants.</summary>
        public int RtWidth { get; }
        public int RtHeight { get; }
        public const int DefaultSeed = 12345;

        // Quality level is pinned by NAME, not index: index order is not stable across
        // platform switches, and Mobile uses renderScale 0.8 while PC uses 1.0 - which
        // silently changes every pixel.
        // SHIPPING QUALITY, not the convenient one. This was "PC" (renderScale 1.0) while the
        // game ships Mobile (renderScale 0.8), so every golden and every study capture rendered
        // at a resolution the player never sees - and the shape study judged 5-8px path widths
        // against it, with its own legibility floor at ~6px.
        public const string PinnedQualityLevel = "Mobile";

        /// <summary>
        /// EVERY suppression this context performs, declared rather than scattered.
        ///
        /// Three real bugs have hidden behind suppressions that were individually reasonable -
        /// the hand palette, BoardFraming being harness-only, and CinemachineBrain. Declaring the
        /// list makes adding a fourth a deliberate act: X18 fails until the new entry is also
        /// added to the allowlist in docs/audit/HARNESS_DIVERGENCE.md, which forces someone to
        /// write down why it is safe.
        ///
        /// This is a STRUCTURAL guard, not a pixel one. A harness-versus-live image comparison is
        /// not buildable: the live game is non-deterministic, which is the entire reason this
        /// class exists.
        /// </summary>
        public static readonly string[] Suppressions =
        {
            "CinemachineBrain",          // CRITICAL - the live camera is a vCam, not this pose
            "UniversalCameraController", // pan
            "EndlessModeManager",        // KNOWN DIVERGENCE #3 - see HARNESS_DIVERGENCE.md
            "BoardFramingDriver",        // would ease off the pinned pose
            "FPSCounter",                // debug overlay, genuinely unwanted
            "QualityLevel:Mobile",       // shipping quality, renderScale 0.8
            "Time.captureDeltaTime",
            "Random.InitState",
            "Camera.targetTexture",
            "HandPalettes",              // PROVEN to have concealed the framing bounds bug
            "BoatCoroutines",
            "PathVisualizerCoroutines",
            "GridManagerCoroutines",
            "TileLocalScale",
            "ShaderGlobalTime",
        };

        public RenderTexture Target { get; private set; }
        public Camera Cam { get; private set; }

        readonly List<Behaviour> _disabled = new List<Behaviour>();
        int _prevQuality;
        float _prevCaptureDelta;
        RenderTexture _prevCamTarget;
        Vector3 _prevCamPos;
        Quaternion _prevCamRot;
        float _prevFov;
        bool _prevCamOrtho;
        float _prevOrthoSize;
        UnityEngine.Random.State _prevRandom;

        // WHAT THIS PINS, AND WHY IT CHANGED
        // ----------------------------------
        // This used to pin the camera POSE - position, rotation and FOV - computed by its own
        // private copy of the framing maths. That was fine while framing was fixed, but it made
        // framing itself unmeasurable: every capture showed the harness's idea of the camera,
        // never the game's, so a framing bug could not appear in a golden.
        //
        // It now pins the INPUTS to framing - layout config, orientation, projection, render
        // target size, board shape - and lets the production BoardFraming compute the pose. The
        // captures therefore show what the game will show, and a framing regression moves pixels.
        public BoardLayout Layout { get; }
        public BoardLayout.Orientation Orientation { get; }
        public BoardFraming.Projection Projection { get; }

        /// <summary>Pitch the board is FITTED FOR. Framing fits the camera that exists, so this
        /// must match whatever angle actually renders.</summary>
        public float PitchDegrees { get; private set; }

        /// <summary>The pose BoardFraming computed, and the bounds it fitted. Recorded so tests
        /// can assert against exactly what was rendered rather than recomputing it.</summary>
        public BoardFraming.Pose FramedPose { get; private set; }
        public Bounds FramedBounds { get; private set; }

        public DeterministicContext(int seed = DefaultSeed,
                                    // Orthographic tilted is the adopted default - it won the projection comparison on
                                    // BOTH foreshortening (1.000 vs 1.114) and fill (47.75% vs 40.89%).
                                    BoardFraming.Projection projection = BoardFraming.Projection.OrthographicTilted,
                                    BoardLayout layout = null,
                                    BoardLayout.Orientation? orientation = null,
                                    int width = Width, int height = Height,
                                    float? pitchDegrees = null)
        {
            PitchDegrees = pitchDegrees ?? BoardFraming.PitchFor(projection);
            RtWidth = width;
            RtHeight = height;
            Layout = layout != null ? layout : LoadDefaultLayout();
            Orientation = orientation ?? BoardLayout.OrientationFor(RtWidth, RtHeight);
            Projection = projection;

            _prevRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            PinQualityLevel();

            _prevCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;

            Target = new RenderTexture(RtWidth, RtHeight, 24, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB)
            {
                name = "HapiDeterministicRT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            Target.Create();

            AcquireCameraAndSuppressDrivers();
            SuppressDebugOverlays();
        }

        /// <summary>The quality level actually in force for this capture. Recorded in the
        /// sidecar next to every PNG so a golden always carries the conditions it was made under.</summary>
        public string ResolvedQualityLevel { get; private set; }

        /// <summary>Active build target, or "unknown" outside the Editor. Quality levels can be
        /// excluded per platform, so the build target changes which levels even exist.</summary>
        public static string ActiveBuildTarget =>
#if UNITY_EDITOR
            UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            "unknown(player)";
#endif

        void PinQualityLevel()
        {
            _prevQuality = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == PinnedQualityLevel)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    ResolvedQualityLevel = names[i];
                    return;
                }
            }

            // FAIL LOUDLY. QualitySettings.names only lists levels not excluded for the active
            // build target, so switching to Android drops "PC" from the list entirely. Warning
            // and continuing would silently capture at a different renderScale (Mobile is 0.8,
            // PC is 1.0) and bake that into the goldens without announcing itself.
            throw new InvalidOperationException(
                $"[DeterministicContext] Quality level '{PinnedQualityLevel}' is not available for " +
                $"build target '{ActiveBuildTarget}'. Available: [{string.Join(", ", names)}]. " +
                "Captures would not be comparable, so refusing to continue. Switch the build target " +
                "back, or change PinnedQualityLevel deliberately and re-capture every golden.");
        }

        /// <summary>One line describing everything that could change what a capture looks like.</summary>
        public string Conditions =>
            $"editor={Application.unityVersion} " +
            $"quality={ResolvedQualityLevel} " +
            $"buildTarget={ActiveBuildTarget} " +
            $"rt={RtWidth}x{RtHeight} " +
            $"orientation={Orientation} " +
            $"projection={Projection} " +
            $"pitch={PitchDegrees:F1} " +
            $"colorSpace={QualitySettings.activeColorSpace} " +
            $"renderPipeline={(QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "default")}";

        void AcquireCameraAndSuppressDrivers()
        {
            Cam = Camera.main;
            if (Cam == null)
            {
#if UNITY_2023_1_OR_NEWER
                Cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
                Cam = UnityEngine.Object.FindObjectOfType<Camera>();
#endif
            }
            if (Cam == null)
                throw new InvalidOperationException("[DeterministicContext] No Camera in the scene.");

            // Anything that writes to the camera transform in Update/LateUpdate must be off,
            // or it overwrites the pinned pose before the next render.
            DisableByTypeName("Unity.Cinemachine.CinemachineBrain");
            DisableByTypeName("CinemachineBrain");
            DisableAll<UniversalCameraController>();
            DisableAll<EndlessModeManager>();
            DisableAll<BoardFramingDriver>();   // would ease the camera off the pinned pose

            _prevCamPos = Cam.transform.position;
            _prevCamRot = Cam.transform.rotation;
            _prevFov = Cam.fieldOfView;
            _prevCamOrtho = Cam.orthographic;
            _prevOrthoSize = Cam.orthographicSize;
            _prevCamTarget = Cam.targetTexture;

            Cam.targetTexture = Target;
            FrameBoard();          // sets projection, FOV/ortho size and pose together
        }

        void SuppressDebugOverlays()
        {
            DisableAll<FPSCounter>();
        }

        /// <summary>
        /// Ask the PRODUCTION framing code for the pose and apply it. The harness supplies the
        /// inputs and nothing else - if BoardFraming is wrong, the capture is wrong, which is
        /// the entire point of the change.
        /// </summary>
        public void FrameBoard()
        {
            if (!BoardFraming.TryFit(Layout, Orientation, RtWidth, RtHeight, Projection,
                                     PitchDegrees, out var pose, out var bounds))
            {
                // Nothing renderable yet. Leave the camera alone rather than inventing a pose -
                // a made-up fallback would silently produce a "successful" capture of nothing.
                Debug.LogWarning("[DeterministicContext] No board bounds to frame.");
                return;
            }

            FramedPose = pose;
            FramedBounds = bounds;
            pose.ApplyTo(Cam);
        }

        /// <summary>
        /// The shipped layout, or the class defaults if the asset is missing. Falling back keeps
        /// the harness runnable on a fresh clone, and the defaults ARE the authored values.
        /// </summary>
        static BoardLayout LoadDefaultLayout()
        {
            var asset = Resources.Load<BoardLayout>("BoardLayout_Default");
            var layout = asset != null ? asset : ScriptableObject.CreateInstance<BoardLayout>();

            // HAPI_REFRAME_PADDING deliberately reframes the board, to prove that assertions
            // claiming to be subject-relative actually are. "Invariant by construction" is
            // reasoning; moving the camera a long way and watching them stay green is the
            // measurement. Never set in a normal run.
            var over = System.Environment.GetEnvironmentVariable("HAPI_REFRAME_PADDING");
            if (!string.IsNullOrEmpty(over) && float.TryParse(over, out float pad))
            {
                layout = UnityEngine.Object.Instantiate(layout);   // never mutate the shipped asset
                layout.portrait.padding = pad;
                layout.landscape.padding = pad;
                Debug.Log($"[DeterministicContext] REFRAME CHECK: padding overridden to {pad}");
            }
            return layout;
        }

        void DisableAll<T>() where T : Behaviour
        {
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
            foreach (var b in found)
            {
                if (b != null && b.enabled) { b.enabled = false; _disabled.Add(b); }
            }
        }

        void DisableByTypeName(string fullName)
        {
            var t = Type.GetType(fullName) ?? FindTypeInLoadedAssemblies(fullName);
            if (t == null) return;
#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var found = UnityEngine.Object.FindObjectsOfType(t, true);
#endif
            foreach (var o in found)
            {
                if (o is Behaviour b && b.enabled) { b.enabled = false; _disabled.Add(b); }
            }
        }

        static Type FindTypeInLoadedAssemblies(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Bring every animated thing to a settled, known pose. Call immediately before a capture.
        /// Coroutines are the main offender: Unity does not stop them when a Behaviour is merely
        /// disabled, only when its GameObject is deactivated.
        /// </summary>
        public void Quiesce()
        {
            foreach (var boat in FindAll<BoatController>())
            {
                boat.StopAllCoroutines();
                boat.isSelected = false;       // terminates the BobBoat while-loop
                var tile = boat.GetCurrentTile();
                if (tile != null) boat.PlaceOnTile(tile, boat.GetCurrentSnapPoint());
            }

            // PathVisualizer runs colour-fade coroutines that would otherwise be mid-lerp.
            foreach (var pv in FindAll<PathVisualizer>())
                pv.StopAllCoroutines();

            // Tile pop-in (ScaleIn) leaves transforms mid-scale if it is still running.
            foreach (var grid in FindAll<GridManager>())
                grid.StopAllCoroutines();

            foreach (var t in FindAll<BoardTile>())
                t.transform.localScale = Vector3.one;

            HideHandPalettes();

            // Pin shader time so any _Time-driven effect is identical every run.
            Shader.SetGlobalVector("_Time", new Vector4(0f, 0f, 0f, 0f));
            Shader.SetGlobalVector("_SinTime", Vector4.zero);
            Shader.SetGlobalVector("_CosTime", Vector4.one);
        }

        readonly System.Collections.Generic.List<GameObject> _hiddenHands =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>
        /// Board captures must show the board. The hand palette differs per level and per mode,
        /// animates as tiles are consumed, and sits below the board where a portrait frame
        /// always catches it - so it is hidden for the duration of the capture and restored
        /// on Dispose. Hand-specific assertions should capture the hand deliberately instead.
        /// </summary>
        void HideHandPalettes()
        {
            var lem = UnityEngine.Object.FindFirstObjectByType<LevelEditorManager>();
            if (lem == null) return;
            foreach (var t in new[] { lem.playerHandContainer, lem.editorHandContainer })
            {
                if (t != null && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    _hiddenHands.Add(t.gameObject);
                }
            }
        }

        static T[] FindAll<T>() where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>(true);
#endif
        }

        /// <summary>Wall-clock wait for a predicate. Never frame counts - batchmode is uncapped.</summary>
        public static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline)
                    throw new TimeoutException($"[DeterministicContext] Timed out after {timeoutSeconds}s waiting for: {what}");
                yield return null;
            }
        }

        public void Dispose()
        {
            foreach (var b in _disabled) if (b != null) b.enabled = true;
            _disabled.Clear();

            foreach (var go in _hiddenHands) if (go != null) go.SetActive(true);
            _hiddenHands.Clear();

            if (Cam != null)
            {
                Cam.targetTexture = _prevCamTarget;
                Cam.transform.SetPositionAndRotation(_prevCamPos, _prevCamRot);
                Cam.fieldOfView = _prevFov;
                Cam.orthographic = _prevCamOrtho;
                Cam.orthographicSize = _prevOrthoSize;
            }

            if (RenderTexture.active == Target) RenderTexture.active = null;
            if (Target != null) { Target.Release(); UnityEngine.Object.DestroyImmediate(Target); Target = null; }

            Time.captureDeltaTime = _prevCaptureDelta;
            QualitySettings.SetQualityLevel(_prevQuality, true);
            UnityEngine.Random.state = _prevRandom;
        }
    }
}
