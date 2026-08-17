using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Applies <see cref="BoardFraming"/> to the live camera. THE DRIVER DECIDES WHEN; BoardFraming
/// decides WHAT — no framing maths lives here, deliberately, because two copies of a rule is the
/// failure this project keeps paying for (risk R1).
///
/// Until this existed, BoardFraming was harness-only: every assertion checked a pose the TEST
/// computed, while the running game carried a static orthographic size, so a 3x3 and a 6x6 were
/// framed identically. C7 is the assertion that this is actually applied.
/// </summary>
public class BoardFramingDriver : MonoBehaviour
{
    [SerializeField] private BoardLayout layout;

    [Tooltip("Ignore board growth smaller than this in world units, so a single row spawning " +
             "does not re-frame the camera on its own.")]
    [SerializeField] private float hysteresisWorldUnits = 0.75f;

    Camera _cam;
    GridManager _grid;
    UniversalCameraController _ucc;
    Transform _proxy;
    Bounds _lastFramed;

    /// <summary>Contributors the driver actually framed against. Diagnostic only.</summary>
    public static System.Collections.Generic.List<string> DriverContributors =
        new System.Collections.Generic.List<string>();
    bool _hasFramed;

    /// <summary>
    /// The layout a mode should use. The Editor is a DESKTOP AUTHORING SURFACE — tile palette,
    /// hand builder, grid-size controls, lock toggles — so it gets the landscape config rather
    /// than being forced into the player's portrait one.
    /// </summary>
    public static BoardLayout LayoutFor(OperatingMode mode)
    {
        var asset = Resources.Load<BoardLayout>("BoardLayout_Default");
        return asset != null ? asset : ScriptableObject.CreateInstance<BoardLayout>();
    }

    /// <summary>Editor authors in landscape; the player's modes follow the actual screen.</summary>
    public static BoardLayout.Orientation OrientationFor(OperatingMode mode, int width, int height) =>
        mode == OperatingMode.Editor
            ? BoardLayout.Orientation.Landscape
            : BoardLayout.OrientationFor(width, height);

    void Awake()
    {
        // Cached once, never in Update - house rule 3.
        _cam = Camera.main;
        _grid = FindFirstObjectByType<GridManager>();
        _ucc = FindFirstObjectByType<UniversalCameraController>();
        if (layout == null) layout = LayoutFor(CurrentMode);
    }

    void Start() => StartCoroutine(FrameWhenBoardIsReady());

    void OnEnable()  { if (_grid != null) _grid.OnTileSpawned += OnTileSpawnedHandler; }
    void OnDisable() { if (_grid != null) _grid.OnTileSpawned -= OnTileSpawnedHandler; }

    static OperatingMode CurrentMode =>
        GameManager.Instance != null ? GameManager.Instance.currentMode : OperatingMode.Editor;

    /// <summary>
    /// Framing an EMPTY grid gives a meaningless result - the bounds would be the banks alone -
    /// so wait until tiles exist. Wall clock, never frame counts: batchmode runs uncapped.
    /// </summary>
    [Tooltip("GAME-time seconds the board bounds must be unchanged before framing. Never a " +
             "frame count - see the note on FrameWhenBoardIsReady.")]
    [SerializeField] private float stableGameSeconds = 0.25f;

    [Tooltip("World units within which bounds count as unchanged.")]
    [SerializeField] private float stabilityEpsilon = 0.01f;

    /// <summary>
    /// TWO GATES, and the second is deliberately CONTRIBUTOR-AGNOSTIC.
    ///
    /// Measured: after the tile-scale gate passed, ELEVEN more renderers still joined - both
    /// banks, their spawn indicators, the boat and six pieces of the goal marker - growing the
    /// bounds by a fixed +1.00 X, +0.63 Y, +2.20 Z on every board.
    ///
    /// The obvious fix is to also wait for banks, boat and goal. That is exactly the affordance
    /// reservation's failure mode: a list that grows by one every time something joins and fails
    /// silently when someone forgets. It has cost five corrections there already.
    ///
    /// So the gate waits for the BOUNDS TO STOP CHANGING instead. It survives the theme system,
    /// the channel geometry and anything else that adds renderers later, without being told.
    ///
    /// The tile-scale check is KEPT, because objects that exist but are ramping would otherwise
    /// read as "stable at zero" and satisfy a stability gate on their own.
    /// </summary>
    IEnumerator FrameWhenBoardIsReady()
    {
        float deadline = Time.realtimeSinceStartup + 15f;

        while (!BoardIsReady(out string why))
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogError($"[BoardFramingDriver] Timed out after 15s waiting for the board: {why}. " +
                               "NOT framing - the camera is left as authored.");
                yield break;
            }
            yield return null;
        }

        // GAME TIME, NOT FRAMES. A 5-consecutive-frame check passed in about four MILLISECONDS
        // of batchmode time - before the banks had even spawned - and study_3x3 framed against a
        // board that was still arriving. Larger boards only passed because tile spawning burned
        // enough frames for the banks to land first, which is luck, not correctness.
        //
        // Game time because the banks arrive on coroutines that run on game time; the timeout
        // stays on the wall clock so a genuine hang fails loudly instead of blocking forever.
        Bounds previous = default;
        bool havePrevious = false;
        float stableSince = -1f;

        while (true)
        {
            yield return null;

            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogError($"[BoardFramingDriver] Timed out after 15s waiting for the board bounds " +
                               $"to settle for {stableGameSeconds:F2}s of game time. " +
                               "NOT framing - the camera is left as authored.");
                yield break;
            }

            if (!BoardFraming.TryCollectBoardBounds(out var now, out _)) { stableSince = -1f; continue; }

            bool unchanged = havePrevious &&
                (now.center - previous.center).sqrMagnitude <= stabilityEpsilon * stabilityEpsilon &&
                (now.size - previous.size).sqrMagnitude <= stabilityEpsilon * stabilityEpsilon;

            if (unchanged)
            {
                if (stableSince < 0f) stableSince = Time.time;
                if (Time.time - stableSince >= stableGameSeconds) break;
            }
            else stableSince = -1f;

            previous = now;
            havePrevious = true;
        }

        Apply(snap: true);
    }

    /// <summary>
    /// COUNTING TILES IS NOT ENOUGH. Measured: the grid reported 9 of 9 tiles while the bounds
    /// were still degenerate, because the tiles were mid-ScaleIn - most renderers reported
    /// exactly zero bounds and were filtered out, and the four that survived were a twelfth of
    /// full size. The gate must wait for the tiles to be the size they will be drawn at.
    /// </summary>
    bool BoardIsReady(out string why)
    {
        why = null;
        if (_grid == null || _grid.cols <= 0 || _grid.rows <= 0) { why = "no grid"; return false; }

        int expected = _grid.cols * _grid.rows;
        var tiles = FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (tiles.Length < expected)
        {
            why = $"{tiles.Length} of {expected} tiles spawned";
            return false;
        }

        foreach (var t in tiles)
        {
            var sc = t.transform.localScale;
            if (Mathf.Abs(sc.x - 1f) > 0.01f || Mathf.Abs(sc.y - 1f) > 0.01f || Mathf.Abs(sc.z - 1f) > 0.01f)
            {
                why = $"tile '{t.name}' still at scale {sc:F2} - spawn animation in progress";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Endless spawns rows continuously. Re-framing on every single spawn would lurch the camera,
    /// so growth is filtered TWICE: hysteresis rejects changes smaller than a threshold outright,
    /// and anything that survives is EASED rather than snapped. A row appearing therefore reads
    /// as the board settling, not as a cut.
    /// </summary>
    void OnTileSpawnedHandler(TileInstance tile) => Apply(snap: false);

    /// <summary>
    /// Four readings together, so "pipeline not reading the field" / "Brain not applying" /
    /// "blend never finishing" / "something else writing the camera" are separable in ONE run
    /// rather than by successive guesses.
    /// </summary>
    IEnumerator LogPosNextFrame(CinemachineCamera vcam, Vector3 wantPos, Vector3 offset, Vector3 resting)
    {
        yield return null; yield return null;
        var cam = Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Debug.Log($"[POSDBG] f+2 camera={camPos:F3}  proxy={_proxy.position:F3}  " +
                  $"proxy+offset={(_proxy.position + offset):F3}  want={wantPos:F3}  " +
                  $"camera-want={(camPos - wantPos):F3}  vcamPos={vcam.transform.position:F3}");
    }

    IEnumerator LogNextFrame(CinemachineCamera vcam)
    {
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        for (int frame = 1; frame <= 3; frame++)
        {
            yield return null;
            var cam = Camera.main;
            float stateLens = brain != null && brain.ActiveVirtualCamera != null
                ? brain.ActiveVirtualCamera.State.Lens.OrthographicSize : -1f;
            bool blending = brain != null && brain.IsBlending;
            string blendInfo = "-";
            if (brain != null && brain.ActiveBlend != null)
                blendInfo = $"{brain.ActiveBlend.TimeInBlend:F2}/{brain.ActiveBlend.Duration:F2}";

            Debug.Log($"[TIMEDBG] f+{frame}  Time.time={Time.time:F3} unscaled={Time.unscaledTime:F3} " +
                      $"deltaTime={Time.deltaTime:F4} captureDeltaTime={Time.captureDeltaTime:F4} " +
                      $"timeScale={Time.timeScale:F2} " +
                      $"brainBlendUpdate={(brain != null ? brain.BlendUpdateMethod.ToString() : "?")} " +
                      $"brainUpdate={(brain != null ? brain.UpdateMethod.ToString() : "?")}");
            Debug.Log($"[LENSDBG] f+{frame}  vcam.Lens={vcam.Lens.OrthographicSize:F3}  " +
                      $"brain.State.Lens={stateLens:F3}  " +
                      $"Camera.main={(cam != null ? cam.orthographicSize : -1f):F3}  " +
                      $"IsBlending={blending} blend={blendInfo}");
        }
    }

    /// <summary>
    /// The vCam this mode renders through, BY MODE rather than by Priority. The driver can run
    /// before CameraManager.SwitchTo*View has assigned priorities, and picking the highest at
    /// that moment sized the wrong vCam - measured as an orthographic size of 0.00 on screen.
    /// </summary>
    static CinemachineCamera ActiveVCam()
    {
        if (CameraManager.Instance != null)
        {
            var byMode = CameraManager.Instance.CameraFor(CurrentMode);
            if (byMode != null) return byMode;
        }
        CinemachineCamera best = null;
        foreach (var v in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude,
                                                              FindObjectsSortMode.None))
            if (best == null || v.Priority.Value > best.Priority.Value) best = v;
        return best;
    }

    /// <summary>
    /// HOW THIS COMPOSES, and why the driver writes neither the camera nor the vCam transform:
    ///
    ///   driver  -> CameraProxy.position (the RESTING pose) and the vCam's lens
    ///   UCC     -> pans the proxy from that resting position, and rubberbands back to it
    ///   Follow  -> camera = proxy + FollowOffset, with ZERO PositionDamping, so it is rigid
    ///   Brain   -> copies the vCam onto Main Camera
    ///
    /// Each owns exactly one thing. The driver owns the TARGET and the LENS; CinemachineFollow
    /// owns the transform; UCC owns the offset from rest. Nothing overwrites anything else, which
    /// is what made the previous attempt fail - it wrote Camera.main and the Brain restored it.
    /// </summary>
    public void Apply(bool snap)
    {
        if (layout == null) return;
        BoardFraming.DebugBounds = snap;
        bool got = BoardFraming.TryCollectBoardBounds(out var bounds, out _);
        BoardFraming.DebugBounds = false;
        if (!got) return;

        var vcam = ActiveVCam();
        if (vcam == null) return;

        if (!snap && _hasFramed)
        {
            float delta = Mathf.Max(
                Mathf.Abs(bounds.size.x - _lastFramed.size.x),
                Mathf.Abs(bounds.size.z - _lastFramed.size.z));
            if (delta < hysteresisWorldUnits) return;
        }

        // PITCH FROM THE CAMERA THAT WILL RENDER. Fitting at one angle and rendering at another
        // is wrong - extU scales with cos(pitch).
        float pitch = BoardFraming.PitchOf(vcam.transform);

        var pose = BoardFraming.Fit(bounds, layout,
            OrientationFor(CurrentMode, Screen.width, Screen.height),
            Screen.width, Screen.height, BoardFraming.Projection.OrthographicTilted, pitch);

        _lastFramed = bounds;
        _hasFramed = true;

        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        string brainCam = brain != null && brain.ActiveVirtualCamera != null
            ? brain.ActiveVirtualCamera.Name : "<none>";

        int tilesSeen = FindObjectsByType<BoardTile>(FindObjectsInactive.Exclude,
                                                     FindObjectsSortMode.None).Length;
        int tilesExpected = _grid != null ? _grid.cols * _grid.rows : -1;

        Debug.Log($"[FRAMEDBG] snap={snap} driverVCam={vcam.name} brainActiveVCam={brainCam} " +
                  $"{(vcam.name == brainCam ? "AGREE" : "*** DISAGREE ***")} pitch={pitch:F1}\n" +
                  $"          tiles seen={tilesSeen} expected={tilesExpected}\n" +
                  $"          bounds min={bounds.min:F2} max={bounds.max:F2} size={bounds.size:F2}\n" +
                  $"          derived size={pose.orthographicSize:F2} pos={pose.position:F2}");

        // The lens is the vCam's, not the Camera's - the Brain overwrites the Camera every frame.
        var lens = vcam.Lens;
        lens.OrthographicSize = pose.orthographicSize;
        vcam.Lens = lens;

        Debug.Log($"[FRAMEDBG] immediately after write: vcam.Lens.OrthographicSize={vcam.Lens.OrthographicSize:F2}");
        StartCoroutine(LogNextFrame(vcam));

        // Endless drives its own vCam transform directly (EndlessModeManager:183), so the driver
        // must not also move the proxy there or the two would fight. Lens only in that mode.
        if (CurrentMode == OperatingMode.Endless) return;

        var follow = vcam.GetComponent<CinemachineFollow>();
        Vector3 offset = follow != null ? follow.FollowOffset : Vector3.zero;

        // camera = proxy + offset (PositionDamping is zero), so solve for the proxy.
        Vector3 resting = pose.position - offset;

        if (_proxy == null) _proxy = vcam.Follow != null ? vcam.Follow : vcam.transform;
        if (_proxy != null)
        {
            // Identity rotation, deliberately: it makes BindingMode's world-vs-local ambiguity
            // irrelevant rather than requiring it to be solved.
            _proxy.rotation = Quaternion.identity;
            _proxy.position = resting;
        }

        if (_ucc != null) _ucc.SetFramedRestingPosition(resting);

        DriverContributors = new System.Collections.Generic.List<string>(BoardFraming.LastContributors);
        Debug.Log($"[POSDBG] fit.pos={pose.position:F3}  followOffset={offset:F3}  " +
                  $"proxy set to={resting:F3}  proxyActual={_proxy.position:F3}  " +
                  $"boundsUsed min={bounds.min:F2} max={bounds.max:F2} centre={bounds.center:F2}");
        StartCoroutine(LogPosNextFrame(vcam, pose.position, offset, resting));
    }

}
