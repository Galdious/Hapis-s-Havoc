/*─────────────────────────────────────────────────────────────
 *  ConnectionDump.cs
 *  -----------------------------------------------------------
 *  Logs every connection a DominoTile spawns with, plus the
 *  scene-object name and (optionally) the design-time tile name.
 *────────────────────────────────────────────────────────────*/
using UnityEngine;
using System.Text;

[RequireComponent(typeof(TileInstance))]
public class ConnectionDump : MonoBehaviour
{
    void Start()   // runs once when the tile spawns
    {
        TileInstance tile = GetComponent<TileInstance>();

        /*───────────────────────────────────────────────────────────
         * (1)  Get the design-time tile name (optional)
         *      ────────────────────────────────────────────────────
         *  Many projects store a reference to a ScriptableObject
         *  or a string in TileInstance that tells which tile this
         *  is (e.g., “Corner_L”, “Straight_3Way”, etc.).
         *
         *  Uncomment ONE line below that matches your project.
         */
        string designName = "design-unknown";

        // if (tile.definition)             designName = tile.definition.name;
        // if (tile.tileData)               designName = tile.tileData.name;
        // if (!string.IsNullOrEmpty(tile.tileName)) designName = tile.tileName;
        // if (!string.IsNullOrEmpty(tile.id))       designName = tile.id;

        /*───────────────────────────────────────────────────────────
         * (2)  Build the log message
         *──────────────────────────────────────────────────────────*/
        var sb = new StringBuilder();
        sb.Append($"{gameObject.name}  <{designName}>  connections: ");

        foreach (var c in tile.connections)
            sb.Append($"({c.from}-{c.to}) ");

        /*───────────────────────────────────────────────────────────
         * (3)  Print to the Console (object reference helps locate
         *      the tile in Hierarchy when you click the log line)
         *──────────────────────────────────────────────────────────*/
        Debug.Log(sb.ToString(), this);
    }
}
