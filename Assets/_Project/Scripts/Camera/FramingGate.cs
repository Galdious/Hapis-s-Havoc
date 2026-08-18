using System.Collections;
using UnityEngine;

/// <summary>
/// Holds the screen black until the board has actually been framed, then fades in.
///
/// THE PROBLEM THIS SOLVES. <see cref="BoardFramingDriver"/> cannot frame instantly and should not
/// try: it waits for the board bounds to be stable for 0.25s of GAME time, because the banks, the
/// boat and the goal marker arrive on coroutines after the tiles do, and framing against a board
/// that is still arriving is how study_3x3 got framed against a half-built scene. Everything during
/// that wait is on screen - the authored camera pose, tiles mid-ScaleIn, rows popping in - and then
/// the camera jumps to the framed pose. Reported from playtesting as "camera starts zoomed in at a
/// wrong position, then snaps".
///
/// So this does not make framing faster. It stops the un-framed camera being SHOWN.
///
/// Covers mode entry, a fresh Endless start, and - as a side effect rather than a fix - the initial
/// row-spawn pop, because those rows arrive while the screen is still black.
///
/// FAILURE MODE DELIBERATELY CHOSEN. If framing never happens, a gate that waits forever leaves the
/// player looking at a black screen, which is worse than an ugly first frame. The gate therefore
/// fades in anyway after <see cref="safetyTimeoutSeconds"/> of WALL CLOCK and says loudly that it
/// did. Wall clock because this is a stuck-detector: it must fire even if game time has stopped
/// advancing, which is exactly one of the ways framing could fail to arrive.
/// </summary>
[DefaultExecutionOrder(-100)]
public class FramingGate : MonoBehaviour
{
    [Tooltip("Give up waiting and fade in regardless after this many WALL-CLOCK seconds, so a " +
             "framing failure never leaves the player on a black screen.")]
    [SerializeField] private float safetyTimeoutSeconds = 12f;

    [Tooltip("Set false to disable the gate entirely without removing the component.")]
    [SerializeField] private bool holdUntilFramed = true;

    bool _released;

    void OnEnable()  => BoardFramingDriver.FramingApplied += OnFramingApplied;
    void OnDisable() => BoardFramingDriver.FramingApplied -= OnFramingApplied;

    void Start()
    {
        if (!holdUntilFramed) return;

        var fader = ScreenFader.Instance;
        if (fader == null)
        {
            Debug.LogWarning("[FramingGate] No ScreenFader in the scene, so the un-framed camera " +
                             "will be visible on entry. Not fatal, but the snap it causes is what " +
                             "this component exists to hide.");
            return;
        }

        // Opaque IMMEDIATELY, not via a fade: the whole point is that frame zero is not shown, and
        // fading TO black would show exactly the thing being hidden.
        fader.SetOpaqueNow();
        StartCoroutine(ReleaseWhenFramedOrTimedOut());
    }

    void OnFramingApplied() => _released = true;

    IEnumerator ReleaseWhenFramedOrTimedOut()
    {
        float deadline = Time.realtimeSinceStartup + safetyTimeoutSeconds;

        while (!_released)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Debug.LogError(
                    $"[FramingGate] Framing did not arrive within {safetyTimeoutSeconds}s of wall " +
                    "clock. Fading in anyway so the player is not left on a black screen - but the " +
                    "camera is showing its AUTHORED pose, not a framed one. Investigate " +
                    "BoardFramingDriver rather than raising this timeout.");
                break;
            }
            yield return null;
        }

        var fader = ScreenFader.Instance;
        if (fader != null) yield return fader.FadeIn();
    }
}
