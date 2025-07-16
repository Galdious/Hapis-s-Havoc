/*  TileLibrary.cs
 *  A ScriptableObject that stores every tile template and its quantity.
 *  Each template knows:
 *    • displayName      – friendly label
 *    • quantity         – copies in the bag
 *    • frontPaths       – list of snap-index pairs on the FRONT
 *    • backObstacle     – what’s on the reverse side
 *    • canRotate180     – may rotate 180°
 *    • canFlip          – may flip to obstacle side
 */

using System.Collections.Generic;               // for List<>
using UnityEngine;

#region  ENUMS -------------------------------------------------------------

// Put the enum here (simplest).  One file = all tile-library definitions.
public enum ObstacleType
{
    None,
    Boulder,
    Whirlpool,
    Sandbank
}

#endregion

#region  CORE DATA CLASSES -------------------------------------------------

[System.Serializable]              // Makes Unity show this in Inspector
public class TileType
{
    public string  displayName = "New Tile";

    [Min(0)]
    public int     quantity    = 1;

    [Tooltip("Pairs of snap-point indexes on the FRONT side " +
             "(0:TopL, 1:TopR, 2:DownL, 3:DownR, 4:L, 5:R).")]
    public List<Vector2Int> frontPaths = new List<Vector2Int>();

    [Header("Back (reverse side)")]
    public ObstacleType backObstacle = ObstacleType.None;

    [Header("Rules")]
    public bool canRotate180 = true;    // 180° flips allowed
    public bool canFlip      = true;    // front <--> back
}

#endregion

#region  SCRIPTABLEOBJECT CONTAINER ---------------------------------------

[CreateAssetMenu(
    fileName = "TileLibrary",
    menuName = "Hapi/Tile Library",
    order    = 0)]
public class TileLibrary : ScriptableObject
{
    [Tooltip("All distinct tile templates used in the game.")]
    public List<TileType> tileTypes = new List<TileType>();
}

#endregion
