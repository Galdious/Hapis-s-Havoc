using UnityEngine;

/// <summary>
/// Where things live on screen, in normalised viewport coordinates (0..1, origin bottom-left),
/// per orientation. Pure data; <see cref="BoardFraming"/> does the maths.
///
/// WHY NORMALISED VIEWPORT AND NOT PIXELS
/// --------------------------------------
/// The same config has to hold on a 1080x1920 capture, a 1179x2556 phone and a tablet. Anything
/// expressed in pixels would need a per-device table. Rects are resolution-independent; the
/// aspect ratio is supplied separately at fit time.
///
/// THE HAND OVERLAP
/// ----------------
/// handOverlapsBoard is the interesting one. The bottom bank is NOT puzzle-solving space - it is
/// where the boat embarks and returns - so fanned hand cards may sit OVER it without costing the
/// player anything they need to read. That lets boardRect extend underneath handRect and buys
/// back screen the board would otherwise surrender. The constraint that keeps it honest is that
/// no PLAYABLE tile may be occluded, which is C3's job, not this config's.
/// </summary>
[CreateAssetMenu(fileName = "BoardLayout_", menuName = "Hapi's Havoc/Board Layout", order = 2)]
public class BoardLayout : ScriptableObject
{
    public enum Orientation { Portrait, Landscape }

    [System.Serializable]
    public class Config
    {
        [Tooltip("The region the board is fitted into, in normalised viewport coords.")]
        public Rect boardRect = new Rect(0.02f, 0.10f, 0.96f, 0.72f);

        [Tooltip("Top strip: moves, stars, level name.")]
        public Rect hudRect = new Rect(0f, 0.88f, 1f, 0.12f);

        [Tooltip("Where the hand sits.")]
        public Rect handRect = new Rect(0f, 0f, 1f, 0.16f);

        [Tooltip("If true, boardRect may extend under handRect - the hand only ever covers " +
                 "bank space, never a playable tile. C3 is what enforces that.")]
        public bool handOverlapsBoard = true;

        [Tooltip("Fraction of boardRect's smaller dimension kept clear around the board. " +
                 "Stops the outermost push arrow sitting flush against the frame edge.")]
        [Range(0f, 0.25f)] public float padding = 0.04f;
    }

    public Config portrait = new Config
    {
        // Extends down to 0.06 - under the hand - because the lower bank may be covered.
        boardRect = new Rect(0.02f, 0.06f, 0.96f, 0.78f),
        hudRect = new Rect(0f, 0.88f, 1f, 0.12f),
        handRect = new Rect(0f, 0f, 1f, 0.16f),
        handOverlapsBoard = true,
        padding = 0.04f,
    };

    public Config landscape = new Config
    {
        // Hand becomes a column on the right, so the board loses width rather than height.
        boardRect = new Rect(0.02f, 0.04f, 0.76f, 0.84f),
        hudRect = new Rect(0f, 0.88f, 1f, 0.12f),
        handRect = new Rect(0.80f, 0f, 0.20f, 0.88f),
        handOverlapsBoard = false,
        padding = 0.04f,
    };

    public Config For(Orientation o) => o == Orientation.Landscape ? landscape : portrait;

    /// <summary>Orientation implied by a render target's aspect. Square counts as portrait.</summary>
    public static Orientation OrientationFor(int width, int height) =>
        width > height ? Orientation.Landscape : Orientation.Portrait;
}
