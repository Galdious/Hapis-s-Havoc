using UnityEngine;

/// <summary>
/// Marks a TileInstance as being ON THE BOARD, as opposed to inventory.
///
/// WHY THIS EXISTS
/// ---------------
/// Hand tiles and the editor's tile palette are built from the same DominoTile prefab as grid
/// tiles, so they all carry TileInstance. Any global sweep for TileInstance therefore collects
/// inventory as board geometry. That is exactly how the camera framing bug happened: hand and
/// palette tiles dragged the world bounds to more than twice the grid's width, and it was
/// invisible in every golden because the capture rig hides hand palettes.
///
/// Filtering by ancestry fixed that instance, but it is OPT-IN: every future sweep has to
/// remember, and forgetting produces a slightly-wrong camera rather than an error. This marker
/// inverts the default - a sweep asks for BoardTile and is correct without knowing why.
///
/// DELIBERATELY NOT ON THE PREFAB. Hand and palette tiles instantiate the same prefab and would
/// inherit it. It is added at the point a tile is placed into the grid, and nowhere else.
///
/// TileInstance keeps its meaning: tile DATA, valid for a tile anywhere.
/// </summary>
[DisallowMultipleComponent]
public class BoardTile : MonoBehaviour
{
    /// <summary>Idempotent - safe on a tile that is re-placed or reused.</summary>
    public static void MarkAsBoard(Component tile)
    {
        if (tile == null) return;
        if (tile.GetComponent<BoardTile>() == null) tile.gameObject.AddComponent<BoardTile>();
    }
}
