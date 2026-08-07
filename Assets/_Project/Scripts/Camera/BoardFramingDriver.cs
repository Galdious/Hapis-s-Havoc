using System.Collections;
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

    [Tooltip("Seconds to ease into a new pose when the board grows. 0 snaps.")]
    [SerializeField] private float smoothSeconds = 0.35f;

    [Tooltip("Ignore board growth smaller than this in world units, so a single row spawning " +
             "does not re-frame the camera on its own.")]
    [SerializeField] private float hysteresisWorldUnits = 0.75f;

    Camera _cam;
    GridManager _grid;
    Coroutine _ease;
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

    public void Apply(bool snap)
    {
        if (_cam == null || layout == null) return;
        if (!BoardFraming.TryCollectBoardBounds(out var bounds, out _)) return;

        if (!snap && _hasFramed)
        {
            // Hysteresis on the BOUNDS, not the pose: it is the board growing that matters, and
            // a small change should not move the camera at all.
            float delta = Mathf.Max(
                Mathf.Abs(bounds.size.x - _lastFramed.size.x),
                Mathf.Abs(bounds.size.z - _lastFramed.size.z));
            if (delta < hysteresisWorldUnits) return;
        }

        var pose = BoardFraming.Fit(bounds, layout,
            OrientationFor(CurrentMode, Screen.width, Screen.height),
            Screen.width, Screen.height, BoardFraming.Projection.OrthographicTilted);

        _lastFramed = bounds;
        _hasFramed = true;

        if (_ease != null) StopCoroutine(_ease);
        if (snap || smoothSeconds <= 0f) pose.ApplyTo(_cam);
        else _ease = StartCoroutine(EaseTo(pose));
    }

    IEnumerator EaseTo(BoardFraming.Pose target)
    {
        Vector3 p0 = _cam.transform.position;
        float s0 = _cam.orthographicSize;
        _cam.orthographic = target.orthographic;

        float t = 0f;
        while (t < smoothSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / smoothSeconds);
            _cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, target.position, k), target.rotation);
            _cam.orthographicSize = Mathf.Lerp(s0, target.orthographicSize, k);
            yield return null;
        }
        target.ApplyTo(_cam);
        _ease = null;
    }
}
