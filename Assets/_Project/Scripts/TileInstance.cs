// In TileInstance.cs

using System.Collections.Generic;
using UnityEngine;

public class TileInstance : MonoBehaviour
{
    [Header("CONFIGURATION - DRAG SNAPS HERE")]
    [Tooltip("Drag the 6 Snap Point GameObjects here in the correct order:\n0: TopL, 1: TopR, 2: DownL, 3: DownR, 4: R, 5: L")]
    public Transform[] snapPoints = new Transform[6];

    [Header("Runtime State")]
    public bool IsReversed { get; private set; }
    public bool IsHardBlocker { get; set; } = false;

    [System.NonSerialized]
    public TileType originalTemplate;

    [System.Serializable]
    public struct Connection
    {
        public int from;
        public int to;
    }
    public List<Connection> connections = new();


    public void Initialise(List<Connection> templateConnections, bool isReversed, TileType template = null)
    {
        originalTemplate = template;
        connections = new List<Connection>(templateConnections);
        IsReversed = isReversed;
        GetComponent<PathVisualizer>()?.DrawPaths();
    }
}