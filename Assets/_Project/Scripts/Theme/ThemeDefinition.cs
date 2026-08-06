using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The visual and audio identity of a world. Pure data - it holds no behaviour and mutates
/// nothing; <see cref="ThemeService"/> is what applies it.
///
/// THE ONE RULE
/// ------------
/// A theme may change how something LOOKS. It may never change what something MEANS or WHERE
/// it is. Tile topology, snap-point positions and grid geometry are outside a theme's reach.
/// A player who has learned that a TileCross connects 0-2, 1-3 and 5-4 must be able to carry
/// that knowledge into every world without re-learning it. Repainting the board is a theme;
/// re-plumbing it is a mechanic, and mechanics live in <see cref="WorldDefinition"/>.
///
/// WHY THERE IS NO TILE MESH FIELD
/// -------------------------------
/// Deliberate, and load-bearing. Tile meshes are shared and fixed. The 13 tile types are the
/// game's vocabulary, and their silhouettes are how the player reads the board at a glance.
/// A per-theme mesh would let a world silently change what a tile is, which is exactly the
/// line above. If a future theme genuinely needs a different tile shape, that is a DESIGN
/// change to the tile catalogue, not a theme field - raise it, do not add the field here.
///
/// MATERIALS VS PALETTE
/// --------------------
/// Both exist because they do different jobs:
///   - materials are assigned to renderers, and are the base layer (see ThemeService's
///     layering note). They are what actually gets drawn.
///   - palette colours drive things that are NOT renderer materials: LineRenderer vertex
///     colours on paths, the background gradient, UI accent. They are also what V5 and V6
///     assert against, because a declared palette is checkable and a material is not.
/// A theme is expected to keep the two consistent; V6 is the assertion that it does.
/// </summary>
[CreateAssetMenu(fileName = "Theme_", menuName = "Hapi's Havoc/Theme Definition", order = 0)]
public class ThemeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable machine key. Never localise this; it is used for lookup and save data.")]
    public string id = "untitled";
    [Tooltip("Player-facing name.")]
    public string displayName = "Untitled";

    [Header("Palette")]
    public Color tileBase = Color.white;
    public Color tileEdge = Color.white;
    public Color pathShallow = Color.white;
    public Color pathDeep = Color.white;
    public Color pathHighlight = Color.green;
    public Color blocker = Color.black;
    public Color vortex = Color.cyan;
    public Color goal = Color.red;
    public Color bankTop = Color.grey;
    public Color bankBottom = Color.grey;
    public Color uiAccent = Color.yellow;
    public Color backgroundTop = Color.white;
    public Color backgroundBottom = Color.white;

    [Header("Materials")]
    [Tooltip("Assigned to renderers as SHARED materials. Never via MaterialPropertyBlock - " +
             "that layer belongs to HighlightService. See ThemeService.")]
    public Material tileMat;
    public Material pathMat;
    public Material blockerMat;
    public Material vortexMat;
    public Material bankMat;
    public Material boatMat;

    [Header("Flavour prefabs")]
    [Tooltip("Swapped in for the generic equivalents. Leave null to keep the project default.")]
    public GameObject bankPrefab;
    public GameObject boatPrefab;
    public GameObject blockerPrefab;
    public GameObject vortexPrefab;
    public GameObject goalMarkerPrefab;
    [Tooltip("Purely decorative. Nothing may depend on these existing, and nothing they do " +
             "may affect gameplay - they must not carry colliders that the board raycasts hit.")]
    public List<GameObject> decorativeProps = new List<GameObject>();

    [Header("Background")]
    public Gradient backgroundGradient;
    public Material skyboxMaterial;

    [Header("Audio")]
    [Tooltip("Intentionally unpopulated. The field exists so themes do not need a schema " +
             "change when sound lands; nothing reads it yet.")]
    public AudioClip ambientLoop;
    public List<AudioClip> stingers = new List<AudioClip>();

    /// <summary>
    /// Every palette colour, in declaration order, for tests that need to check conformance
    /// without hard-coding the field list. Kept in sync by hand; V6 is what catches drift.
    /// </summary>
    public IEnumerable<Color> AllPaletteColours()
    {
        yield return tileBase;
        yield return tileEdge;
        yield return pathShallow;
        yield return pathDeep;
        yield return pathHighlight;
        yield return blocker;
        yield return vortex;
        yield return goal;
        yield return bankTop;
        yield return bankBottom;
        yield return uiAccent;
        yield return backgroundTop;
        yield return backgroundBottom;
    }

    public override string ToString() => $"ThemeDefinition('{id}' / {displayName})";
}
