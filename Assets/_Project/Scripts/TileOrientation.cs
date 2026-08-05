using UnityEngine;

/// <summary>
/// The single shared definition of "how is this tile oriented".
///
/// WHY THIS EXISTS
/// ---------------
/// Reading transform.eulerAngles.y to decide whether a tile is rotated 180 degrees is WRONG
/// for flipped tiles, and the project has done it in several places.
///
/// A tile is built as Quaternion.Euler(isFlipped ? 180 : 0, rotationY, 0). Unity normalises
/// the euler representation of that product, so eulerAngles.y does NOT read back as the
/// authored rotationY once the X flip is involved:
///
///     authored (rotationY=0,   flipped=false)  ->  eulerAngles.y ==   0
///     authored (rotationY=180, flipped=false)  ->  eulerAngles.y == 180
///     authored (rotationY=0,   flipped=true)   ->  eulerAngles.y == 180   <-- inverted
///     authored (rotationY=180, flipped=true)   ->  eulerAngles.y ==   0   <-- inverted
///
/// So for a flipped tile the reading is exactly inverted relative to what was authored.
/// This was measured on level 01_06_TestLevel during the Unity 6.3 migration: tiles (1,0)
/// and (1,1) are authored rotationY~0 + flipped and read back 180; tile (1,2) is authored
/// rotationY=180 + flipped and reads back 0.
///
/// The functions here read BASIS VECTORS instead, which are representation-independent:
///   - a 180 degree yaw sends local +X to world -X, and an X flip leaves local +X alone
///   - any flip (about X or Z) sends local +Y to world -Y
///
/// One consequence worth knowing: an X flip and a Z flip produce DIFFERENT orientations
/// (Rz(180) inverts local +X, Rx(180) does not). These helpers report that difference
/// faithfully rather than hiding it - which is the point, because GridManager's three
/// PushRowCoroutine overloads do not agree on which axis they flip (ARCHITECTURE.md risk R1).
/// </summary>
public static class TileOrientation
{
    /// <summary>Tolerance on a basis-vector component. Well clear of float noise; the shipped
    /// levels store rotationY values like 0.000005008956122765085.</summary>
    const float Epsilon = 0.01f;

    /// <summary>True when the tile's local +X points along world -X, i.e. it is yawed 180.
    /// Correct for flipped tiles, unlike reading eulerAngles.y.</summary>
    public static bool IsYawFlipped(Transform t) => t != null && t.right.x < -Epsilon;

    /// <summary>True when the tile shows its reverse face (local +Y points down).
    /// True for both an X flip and a Z flip.</summary>
    public static bool IsFaceFlipped(Transform t) => t != null && t.up.y < -Epsilon;

    public static bool IsYawFlipped(TileInstance tile) => tile != null && IsYawFlipped(tile.transform);
    public static bool IsFaceFlipped(TileInstance tile) => tile != null && IsFaceFlipped(tile.transform);

    /// <summary>Do these two tiles sit in the same orientation? Use this for parity checks
    /// instead of comparing eulerAngles or raw quaternions.</summary>
    public static bool SameOrientation(Transform a, Transform b)
        => a != null && b != null
        && IsYawFlipped(a) == IsYawFlipped(b)
        && IsFaceFlipped(a) == IsFaceFlipped(b);

    public static bool SameOrientation(TileInstance a, TileInstance b)
        => a != null && b != null && SameOrientation(a.transform, b.transform);

    /// <summary>Does this tile's live orientation match what a level JSON authored?
    /// Pass TileSaveData.rotationY and TileSaveData.isFlipped straight in.</summary>
    public static bool MatchesAuthored(Transform t, float authoredRotationY, bool authoredFlipped)
    {
        if (t == null) return false;
        bool wantYaw = Mathf.RoundToInt(Mathf.Repeat(authoredRotationY, 360f)) == 180;
        return IsYawFlipped(t) == wantYaw && IsFaceFlipped(t) == authoredFlipped;
    }

    public static bool MatchesAuthored(TileInstance tile, float authoredRotationY, bool authoredFlipped)
        => tile != null && MatchesAuthored(tile.transform, authoredRotationY, authoredFlipped);

    /// <summary>Readable orientation for assertion failure messages. Includes the raw
    /// eulerAngles.y precisely so a reader can see why trusting it would have been wrong.</summary>
    public static string Describe(Transform t)
    {
        if (t == null) return "<null>";
        return $"yaw180={IsYawFlipped(t)} faceFlipped={IsFaceFlipped(t)} " +
               $"(raw eulerAngles.y={t.eulerAngles.y:F1} - do not compare this directly)";
    }

    public static string Describe(TileInstance tile)
        => tile == null ? "<null>" : Describe(tile.transform);
}
