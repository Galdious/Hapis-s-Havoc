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
    IEnumerator FrameWhenBoardIsReady()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!BoardFraming.TryCollectBoardBounds(out _, out int contributors) || contributors < 2)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogWarning("[BoardFramingDriver] Timed out waiting for a populated board.");
                yield break;
            }
            yield return null;
        }
        Apply(snap: true);
    }

    /// <summary>
    /// Endless spawns rows continuously. Re-framing on every single spawn would lurch the camera,
    /// so growth is filtered TWICE: hysteresis rejects changes smaller than a threshold outright,
    /// and anything that survives is EASED rather than snapped. A row appearing therefore reads
    /// as the board settling, not as a cut.
    /// </summary>
    void OnTileSpawnedHandler(TileInstance tile) => Apply(snap: false);

    IEnumerator LogNextFrame(CinemachineCamera vcam)
    {
        yield return null;
        var cam = Camera.main;
        Debug.Log($"[FRAMEDBG] next frame: vcam.Lens.OrthographicSize={vcam.Lens.OrthographicSize:F2} " +
                  $"Camera.main.orthographicSize={(cam != null ? cam.orthographicSize : -1f):F2} " +
                  $"Camera.main.pos={(cam != null ? cam.transform.position : Vector3.zero):F2}");
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
    }

}
