using System.Collections.Generic;
using UnityEngine;
public class TileInstance : MonoBehaviour
{
    // 1) Snap-point array  ----------------------------------------------

    // We PRE-allocate an array of size 6.  Index meaning is fixed:
    // 0=TopL  1=TopR  2=DownL  3=DownR  4=L  5=R
    public Transform[] snapPoints = new Transform[6];
    public bool IsReversed { get; private set; }

    [System.NonSerialized] // Don't serialize this, it's runtime-only
    public TileType originalTemplate;

    // 2) Connection struct  ---------------------------------------------

    // Simple value-type pair (integers), serialisable so Unity shows it.
    [System.Serializable]
    public struct Connection
    {
        public int from;   // which snap-point index path starts at
        public int to;     // which snap-point index it ends at
    }

    // List that will store THIS tile's actual paths after Initialise()
    public List<Connection> connections = new();

    // 3) Lifecycle -------------------------------------------------------

    private void Awake()      // runs the moment the prefab spawns
    {
        CacheSnapPoints();    // fill snapPoints[0..5] once
    }

    // 4) Public API ------------------------------------------------------

    /// <summary>
    /// GridManager will call this once, passing in the template
    /// connections from the TileLibrary.
    /// </summary>
    public void Initialise(List<Connection> templateConnections, bool isReversed, TileType template = null)
    {
        // Store the original template so we can return it to the bag later
        originalTemplate = template;
        
        // ... rest of existing code stays the same ...
        connections = new List<Connection>(templateConnections);
        IsReversed = isReversed;
        GetComponent<PathVisualizer>()?.DrawPaths();
        Debug.Log($"{name} initialised with {connections.Count} connections.");
    }

    // 5) Helper: find children ------------------------------------------

    private void CacheSnapPoints()
    {
        // Matches your renamed snap-point children:
        for (int i = 0; i < snapPoints.Length; i++)
        {
            string snapName = i switch
            {
                0 => "Snap_TopL",
                1 => "Snap_TopR",
                2 => "Snap_DownL",
                3 => "Snap_DownR",
                4 => "Snap_R",
                5 => "Snap_L",
                _ => null
            };

            var child = transform.Find(snapName);
            if (child != null)
                snapPoints[i] = child;
            else
                Debug.LogWarning($"[TileInstance] {snapName} missing on {name}");
        }
    }
}

