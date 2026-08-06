using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies a <see cref="ThemeDefinition"/> to the live board. The one place theme appearance
/// is set, in the same spirit as HighlightService being the one place highlights are set.
///
///
/// THE LAYERING, AND WHY IT IS SHAPED THIS WAY
/// ===========================================
/// This is the seam where the project's "highlight stuck on" bug family would come back, so the
/// contract is stated here and enforced by T1.
///
///     Layer 0  THEME       renderer.sharedMaterial, LineRenderer colours, camera background.
///                          Persistent. Owned by ThemeService.
///     Layer 1  HIGHLIGHT   MaterialPropertyBlock. Transient. Owned by HighlightService.
///
/// The two layers use DIFFERENT MECHANISMS, and that is the whole design:
///
///   - HighlightService.Clear() is `renderer.SetPropertyBlock(null)`. It wipes the ENTIRE
///     property block, not just the colour it set. So if the theme also lived in a property
///     block, clearing a highlight would erase the theme with it and the tile would snap back
///     to the raw material. Putting the theme in the shared material makes that impossible.
///
///   - Because the theme is in the shared material, "clear the highlight" and "return to the
///     theme's value" are THE SAME OPERATION. Removing the override reveals what was underneath
///     all along. There is no snapshot to capture, no dictionary to keep, and therefore no way
///     for a snapshot to go stale or a restore to be missed - which is precisely how the
///     original bug worked (renderer.material.color = X, restored via sharedMaterial).
///
///   - It also means re-theming a tile WHILE it is highlighted is safe and order-independent:
///     the two layers do not interact, and clearing later still lands on the new theme's value
///     rather than the one that was current when the highlight began.
///
/// The two invariants that keep this true:
///     ThemeService     must never touch MaterialPropertyBlock.
///     HighlightService must never touch materials.
/// Break either and the layers collapse into one. T1 asserts the observable consequence.
///
///
/// HOW MATERIALS ARE SWAPPED
/// =========================
/// By IDENTITY REMAP, not by walking the hierarchy and guessing which renderer is which.
/// A tile has several child renderers (base, vortex, blocker) with different materials, so
/// "assign tileMat to every renderer under the tile" would flatten them and change the picture.
///
/// Instead the service builds a map from the REFERENCE theme's materials to the target theme's,
/// and replaces shared materials by reference equality. Two useful consequences:
///   - applying the reference theme is an identity map, so it is a provable no-op. That is what
///     lets Egypt reproduce today's appearance at 0.0000%.
///   - a renderer using a material the theme says nothing about is left completely alone,
///     rather than being given something arbitrary.
///
///
/// PATHS ARE A THIRD MECHANISM, PRE-EXISTING
/// =========================================
/// PathVisualizer colours its LineRenderers through `startColor`/`endColor` fields, not through
/// a material or a property block, and it does `lr.material = lineMat` (which instantiates -
/// a house rule 2 violation that predates this class). The theme therefore drives paths by
/// setting PathVisualizer's `defaultPathColor` / `highlightColor` fields. Left as found:
/// rewriting PathVisualizer's colour handling would move pixels, and this job must not.
/// </summary>
public class ThemeService : MonoBehaviour
{
    [Header("Themes")]
    [Tooltip("The theme the raw prefabs and scene actually ship with. Material remapping is " +
             "computed relative to this, so applying it is an identity map and a no-op.")]
    [SerializeField] private ThemeDefinition referenceTheme;

    [Tooltip("Applied on Start. Leave null to apply the reference theme.")]
    [SerializeField] private ThemeDefinition startingTheme;

    [Header("Endless streaming")]
    [Tooltip("Endless spawns rows continuously. GridManager raises no spawn event and house " +
             "rule 1 forbids adding one there, so new tiles are reconciled by a low-frequency " +
             "poll instead of per-frame work. See the note on ReconcileLoop.")]
    [SerializeField] private float reconcileIntervalSeconds = 0.25f;

    /// <summary>The theme currently applied. Null before the first apply.</summary>
    public static ThemeDefinition Active { get; private set; }

    /// <summary>
    /// The theme the raw prefabs ship with. Exposed so tests can reach the shipping theme
    /// without an AssetDatabase, which PlayMode does not have.
    /// </summary>
    public ThemeDefinition ReferenceTheme => referenceTheme;

    /// <summary>Raised after a theme is applied, so UI can restyle without polling.</summary>
    public static event System.Action<ThemeDefinition> OnThemeApplied;

    GridManager _grid;
    Camera _camera;

    // Renderers already remapped for the Active theme, so streaming reconciliation does not
    // redo work every tick.
    readonly HashSet<Renderer> _applied = new HashSet<Renderer>();
    Dictionary<Material, Material> _remap = new Dictionary<Material, Material>();

    void Awake()
    {
        // Cached once, never in Update - house rule 3.
        _grid = FindFirstObjectByType<GridManager>();
        _camera = Camera.main;
    }

    void Start()
    {
        ApplyTheme(startingTheme != null ? startingTheme : referenceTheme);
        if (reconcileIntervalSeconds > 0f) StartCoroutine(ReconcileLoop());
    }

    void OnDestroy()
    {
        // Static state must not survive a scene reload; the project relies on domain reload
        // between tests and a stale Active would leak across them.
        Active = null;
    }

    /// <summary>
    /// Apply a theme to everything currently in the scene. Safe to call repeatedly and safe to
    /// call while tiles are highlighted - the highlight layer is untouched.
    /// </summary>
    public void ApplyTheme(ThemeDefinition theme)
    {
        if (theme == null)
        {
            Debug.LogWarning("[ThemeService] ApplyTheme(null) - nothing applied.");
            return;
        }

        Active = theme;
        _remap = BuildRemap(referenceTheme, theme);
        _applied.Clear();

        ApplyMaterialsUnder(_grid != null ? _grid.gridParent : null);
        ApplyMaterialsUnder(transform.parent);           // banks, boat and props, if grouped
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            ApplyMaterialsUnder(root.transform);

        ApplyPalette(theme);

        Debug.Log($"[ThemeService] Applied {theme} - {_remap.Count} material mapping(s), " +
                  $"{_applied.Count} renderer(s) touched.");
        OnThemeApplied?.Invoke(theme);
    }

    /// <summary>
    /// Apply the active theme to one tile. Public so Endless streaming - or anything else that
    /// spawns board content - can theme it immediately rather than waiting for reconciliation.
    /// </summary>
    public void ApplyToTile(TileInstance tile)
    {
        if (tile == null || Active == null) return;
        ApplyMaterialsUnder(tile.transform);
        ApplyPathColoursUnder(tile.transform, Active);
    }

    /// <summary>
    /// Maps every material the reference theme declares to the corresponding one in the target.
    /// Entries where either side is null, or where both are the same asset, are skipped - the
    /// identity case must produce an EMPTY map so applying the reference theme provably does
    /// nothing at all.
    /// </summary>
    static Dictionary<Material, Material> BuildRemap(ThemeDefinition from, ThemeDefinition to)
    {
        var map = new Dictionary<Material, Material>();
        if (from == null || to == null) return map;

        void Add(Material a, Material b)
        {
            if (a == null || b == null || a == b) return;
            map[a] = b;
        }

        Add(from.tileMat, to.tileMat);
        Add(from.pathMat, to.pathMat);
        Add(from.blockerMat, to.blockerMat);
        Add(from.vortexMat, to.vortexMat);
        Add(from.bankMat, to.bankMat);
        Add(from.boatMat, to.boatMat);
        return map;
    }

    /// <summary>
    /// Walks renderers and replaces shared materials by reference identity. Renderers whose
    /// materials the theme says nothing about are left untouched.
    ///
    /// sharedMaterials, never `materials` - reading `renderer.materials` instantiates a copy
    /// per renderer, which is the leak house rule 2 exists to prevent.
    /// </summary>
    void ApplyPathColoursUnder(Transform root, ThemeDefinition theme)
    {
        if (root == null || theme == null) return;
        foreach (var pv in root.GetComponentsInChildren<PathVisualizer>(true))
        {
            if (pv == null) continue;
            pv.defaultPathColor = theme.pathShallow;
            pv.highlightColor = theme.pathHighlight;
        }
    }

    void ApplyMaterialsUnder(Transform root)
    {
        if (root == null || _remap.Count == 0) return;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || _applied.Contains(renderer)) continue;

            var mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && _remap.TryGetValue(mats[i], out var replacement))
                {
                    mats[i] = replacement;
                    changed = true;
                }
            }

            if (changed) renderer.sharedMaterials = mats;
            _applied.Add(renderer);
        }
    }

    /// <summary>
    /// Palette values that are not renderer materials: path vertex colours and the background.
    /// </summary>
    void ApplyPalette(ThemeDefinition theme)
    {
        // PathVisualizer is PER TILE, not a scene singleton, so every tile carries its own.
        //
        // KNOWN LIMIT: PathVisualizer copies these fields into LineRenderer vertex colours at
        // DRAW time (lr.startColor = defaultPathColor). Changing them afterwards does not
        // recolour lines that are already drawn - those only pick it up when the tile next
        // redraws. Applying a theme at load is therefore correct; hot-swapping a theme mid-game
        // repaints materials immediately but leaves existing path lines until they redraw.
        // Not fixed here because forcing a redraw would move pixels, and this job must not.
        ApplyPathColoursUnder(_grid != null ? _grid.gridParent : null, theme);

        // Only touch the clear colour if the camera actually clears to one. If it renders a
        // skybox, or the background is drawn by geometry, backgroundColor is not what is on
        // screen and writing it would be a silent no-op that looks like it worked.
        if (_camera != null && theme.skyboxMaterial == null &&
            _camera.clearFlags == CameraClearFlags.SolidColor)
            _camera.backgroundColor = theme.backgroundBottom;

        if (theme.skyboxMaterial != null)
            RenderSettings.skybox = theme.skyboxMaterial;
    }

    /// <summary>
    /// Endless streams new rows in continuously. GridManager raises no "tile spawned" event, and
    /// house rule 1 forbids adding one to it, so this reconciles instead.
    ///
    /// It is a POLL, not per-frame work: it wakes a few times a second and, in the common case,
    /// does a single int comparison and goes back to sleep. That is cheap enough for a mobile
    /// target, but it is a stopgap, not the right long-term design - a one-line OnTileSpawned
    /// event on GridManager would remove it entirely. Flagged rather than added unilaterally.
    /// </summary>
    IEnumerator ReconcileLoop()
    {
        var wait = new WaitForSeconds(reconcileIntervalSeconds);
        int lastCount = -1;

        while (true)
        {
            yield return wait;

            if (Active == null || _remap.Count == 0) continue;
            var parent = _grid != null ? _grid.gridParent : null;
            if (parent == null) continue;

            int count = parent.childCount;
            if (count == lastCount) continue;    // nothing spawned or despawned
            lastCount = count;

            ApplyMaterialsUnder(parent);
            ApplyPathColoursUnder(parent, Active);
        }
    }
}
