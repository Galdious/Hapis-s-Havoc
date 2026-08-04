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
        public const int Width = 1080;
        public const int Height = 1920;
        public const int DefaultSeed = 12345;

        // Quality level is pinned by NAME, not index: index order is not stable across
        // platform switches, and Mobile uses renderScale 0.8 while PC uses 1.0 - which
        // silently changes every pixel.
        public const string PinnedQualityLevel = "PC";

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

        // Framing is DERIVED from the grid bounds, not hardcoded. A fixed pose only frames one
        // board size; the project has 3x3 puzzle levels, a 6x6 default and a 3-wide endless
        // strip. A hardcoded pose also let the player hand palette bleed into frame, and the
        // hand differs per level and per mode - which would inject false diffs into every
        // golden comparison.
        public const float BoardCameraPitch = 55f;
        public const float BoardCameraFov = 40f;
        // 0.78 keeps the board, BOTH banks and the side push affordances in frame. Zooming
        // further to crop the hand palette also cropped the arrows, so the hand is suppressed
        // explicitly in Quiesce() instead - see HideHandPalettes(). Fighting a wide, shallow
        // board into a tall portrait frame by zoom alone does not work.
        public const float BoardFillFraction = 0.78f;

        public DeterministicContext(int seed = DefaultSeed)
        {
            _prevRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            PinQualityLevel();

            _prevCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;

            Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
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

        void PinQualityLevel()
        {
            _prevQuality = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == PinnedQualityLevel)
                {
                    QualitySettings.SetQualityLevel(i, true);
                    return;
                }
            }
            Debug.LogWarning($"[DeterministicContext] Quality level '{PinnedQualityLevel}' not found. " +
                             $"Captures may not be comparable across machines. Available: {string.Join(", ", names)}");
        }

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

            _prevCamPos = Cam.transform.position;
            _prevCamRot = Cam.transform.rotation;
            _prevFov = Cam.fieldOfView;
            _prevCamOrtho = Cam.orthographic;
            _prevOrthoSize = Cam.orthographicSize;
            _prevCamTarget = Cam.targetTexture;

            Cam.orthographic = false;
            Cam.fieldOfView = BoardCameraFov;
            Cam.targetTexture = Target;
            FrameBoard();
        }

        void SuppressDebugOverlays()
        {
            DisableAll<FPSCounter>();
        }

        /// <summary>
        /// Points the camera at the centre of the actual grid and pulls back just far enough to
        /// fit it. Recompute this whenever the board changes size (endless streams new rows).
        /// </summary>
        public void FrameBoard()
        {
            var grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
            if (grid == null || grid.cols <= 0 || grid.rows <= 0)
            {
                Cam.transform.SetPositionAndRotation(new Vector3(0f, 14f, -9f),
                                                     Quaternion.Euler(BoardCameraPitch, 0f, 0f));
                return;
            }

            Vector3 min = grid.GetWorldPosition(0, 0);
            Vector3 max = grid.GetWorldPosition(grid.cols - 1, grid.rows - 1);
            Vector3 centre = (min + max) * 0.5f;

            float boardWidth = Mathf.Abs(max.x - min.x) + grid.tileWidth;
            float boardDepth = Mathf.Abs(max.z - min.z) + grid.tileHeight;

            // Extend depth so both banks stay in frame; they are part of the board's visual
            // state and V1 asserts on bank highlight clearing.
            var banks = UnityEngine.Object.FindFirstObjectByType<RiverBankManager>();
            if (banks != null) boardDepth += 2f * (banks.bankDistance + banks.bankWidth * 0.5f);

            float vFovRad = BoardCameraFov * Mathf.Deg2Rad;
            float aspect = (float)Width / Height;                       // 0.5625 portrait
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);

            // Distance needed for each axis; take the larger so nothing is cropped.
            float distForWidth = (boardWidth * 0.5f) / Mathf.Tan(hFovRad * 0.5f);
            // Depth is foreshortened by the pitch.
            float pitchRad = BoardCameraPitch * Mathf.Deg2Rad;
            float apparentDepth = boardDepth * Mathf.Cos(pitchRad);
            float distForDepth = (apparentDepth * 0.5f) / Mathf.Tan(vFovRad * 0.5f);

            float dist = Mathf.Max(distForWidth, distForDepth) / BoardFillFraction;

            var rot = Quaternion.Euler(BoardCameraPitch, 0f, 0f);
            Vector3 back = rot * Vector3.back;                          // camera-to-target inverse
            Cam.transform.SetPositionAndRotation(centre + back * dist, rot);
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

            foreach (var t in FindAll<TileInstance>())
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
