using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one place highlight colour is applied and cleared.
///
/// WHY THIS EXISTS (risk R2)
/// -------------------------
/// Every highlight site in this project used to do:
///     renderer.material.color = X;                    // silently INSTANTIATES a material
///     ...
///     renderer.sharedMaterial = original;             // orphans that instance
/// Reading renderer.material creates a per-renderer copy. Restoring through sharedMaterial
/// throws the copy away without destroying it, and any path that misses the restore leaves the
/// highlight stuck on. That is the documented root of the "banks staying cyan" fix recorded in
/// BoatController's v03 header, and the reason house rule 2 exists.
///
/// MaterialPropertyBlock avoids both problems: it overrides shader properties per renderer
/// WITHOUT instantiating a material, so there is nothing to leak and nothing to restore -
/// clearing is just removing the override. It also preserves SRP batching, which per-renderer
/// material instances break.
/// </summary>
public static class HighlightService
{
    /// <summary>
    /// Colour property names to override. The project mixes URP Lit (Bank_Base_Mat) with
    /// Lit_ZWrite.shadergraph (Tile_Base_Mat, Boat_Base_Material), and ARCHITECTURE.md risk R16
    /// records that Tile_Base_Mat carries BOTH _BaseColor and _Color with different values - so
    /// which one a given shader actually reads is not uniform. Setting a name the shader does
    /// not declare is harmless for a MaterialPropertyBlock, so all candidates are set and the
    /// right one wins. V2 asserts the highlight is actually visible, so a total miss here fails
    /// loudly rather than silently doing nothing.
    /// </summary>
    static readonly int[] ColourProperties =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_Base_Color"),
    };

    static MaterialPropertyBlock _block;
    static readonly HashSet<Renderer> _highlighted = new HashSet<Renderer>();

    static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();

    /// <summary>Tint a renderer. Idempotent; safe to call repeatedly.</summary>
    public static void Apply(Renderer renderer, Color colour)
    {
        if (renderer == null) return;

        var block = Block;
        renderer.GetPropertyBlock(block);
        foreach (int id in ColourProperties) block.SetColor(id, colour);
        renderer.SetPropertyBlock(block);
        block.Clear();

        _highlighted.Add(renderer);
    }

    /// <summary>
    /// Remove the tint. No "original material" bookkeeping is needed - clearing the property
    /// block restores whatever the shared material already said.
    /// </summary>
    public static void Clear(Renderer renderer)
    {
        if (renderer == null) { return; }
        renderer.SetPropertyBlock(null);
        _highlighted.Remove(renderer);
    }

    /// <summary>Clear every renderer this service has tinted. Cheap safety net for mode
    /// switches and level reloads, where a renderer can be dropped without being cleared.</summary>
    public static void ClearAll()
    {
        foreach (var r in _highlighted)
            if (r != null) r.SetPropertyBlock(null);
        _highlighted.Clear();
    }

    /// <summary>How many renderers are currently tinted. Diagnostics and tests.</summary>
    public static int HighlightedCount
    {
        get
        {
            _highlighted.RemoveWhere(r => r == null);
            return _highlighted.Count;
        }
    }
}
