/*
 *  RiverControls.cs
 *  ---------------------------------------------------------------
 *  Creates visual arrow controls around the river grid for testing.
 *  Blue arrows = insert blue (river) tile
 *  Red arrows = insert red (obstacle) tile
 *  Click arrows to push tiles into rows from left/right sides.
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;


public enum RowLockState { Unlocked, LeftLocked, RightLocked, BothLocked }


public class RiverControls : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public Transform gridParent;  // same as GridManager's gridParent
    public BoatManager boatManager;
    public LevelEditorManager levelEditorManager;

    [Header("Arrow Settings")]
    public GameObject arrowPrefab;  // simple cube or arrow mesh
    public GameObject lockPrefab; // prefab for lock toggle buttons
    public GameObject dropZonePrefab; // <<< ADD (for level editor drop zones)
    public Material blueMaterial;   // for blue/river arrows
    public Material redMaterial;    // for red/obstacle arrows
    public Material lockMaterial;   // <<< ADD (Default unlocked material)
    public Material lockedMaterial; // <<< ADD (Grayed-out locked material)
    public float arrowDistance = 4f; // how far from grid edge (back to original)
    public float arrowHeight = 4f;   // how high above tiles
    public float arrowScale = 0.3f;  // size of arrow cubes (made smaller)
    public float arrowSpacing = 0.8f; // space between blue and red arrows

    [Header("Visual Feedback")]
    public bool showHoverEffect = true;
    public Color hoverColor = Color.yellow;
    [Tooltip("How far a row slides sideways when a tile is hovered over its drop zone.")]
    public float rowSlideAmount = 0.2f;
    [Tooltip("How long the row slide animation takes.")]
    public float rowSlideDuration = 0.15f;


    private PointerArrowButton[,] leftArrows;   // [row, side] 0=blue, 1=red
    private PointerArrowButton[,] rightArrows;  // [row, side] 0=blue, 1=red
    private RowLockState[] rowLockStates; // <<< ADD

    /// <summary>
    /// Read-only lock state for a row. Framing needs it to reserve affordance width per SIDE and
    /// per level: a fully locked row has no drop zones at all, so reserving space for them makes
    /// the board needlessly small. Returns BothLocked when unknown, which reserves nothing.
    /// </summary>
    public RowLockState GetRowLockState(int row)
    {
        if (rowLockStates == null || row < 0 || row >= rowLockStates.Length)
            return RowLockState.BothLocked;
        return rowLockStates[row];
    }
    private Dictionary<Renderer, Material> originalArrowMaterials = new Dictionary<Renderer, Material>(); // <<< ADD
    private GameManager gameManager;
    private Canvas dropZoneCanvas;
    private Dictionary<int, Coroutine> runningRowAnimations = new Dictionary<int, Coroutine>();
    private Dictionary<(int, bool), RowDropZone> dropZones = new Dictionary<(int, bool), RowDropZone>();


    // ArrowButton class removed - now using PointerArrowButton


    private void Awake()
    {
        // Find the most critical scene manager as early as possible.
        gameManager = FindFirstObjectByType<GameManager>();
    }




    private void Start()
    {
        if (levelEditorManager == null) levelEditorManager = FindFirstObjectByType<LevelEditorManager>();
        if (boatManager == null) boatManager = FindFirstObjectByType<BoatManager>();
        if (gridManager == null)
        {
            gridManager = FindFirstObjectByType<GridManager>();
        }

        if (gridManager == null)
        {
            Debug.LogError("[RiverControls] GridManager not found!");
            return;
        }

        // gameManager = FindFirstObjectByType<GameManager>();

        //CreateArrows();  // commented out for level editor mode
    }

    public void AnimateRowForDrop(int row, bool fromLeft)
    {
        if (runningRowAnimations.ContainsKey(row))
        {
            StopCoroutine(runningRowAnimations[row]);
        }
        runningRowAnimations[row] = StartCoroutine(AnimateRowPosition(row, fromLeft));
    }
        public void ResetRowAnimation(int row)
    {
        if (runningRowAnimations.ContainsKey(row))
        {
            StopCoroutine(runningRowAnimations[row]);
        }
        runningRowAnimations[row] = StartCoroutine(AnimateRowPosition(row, fromLeft: false, reverse: true));
    }

private IEnumerator AnimateRowPosition(int row, bool fromLeft, bool reverse = false)
{
    // The direction the ROW should move.
    // If a tile comes FROM THE LEFT, the row must shift RIGHT (+1).
    // If a tile comes FROM THE RIGHT, the row must shift LEFT (-1).
    float direction = fromLeft ? 1f : -1f; // <<< --- THIS LOGIC IS NOW CORRECT
    float targetOffset = reverse ? 0f : rowSlideAmount * direction;

    List<Transform> rowTiles = new List<Transform>();
    List<Vector3> basePositions = new List<Vector3>(); // Use a different name to avoid confusion
    for (int x = 0; x < gridManager.cols; x++)
    {
        TileInstance tile = gridManager.GetTileAt(x, row);
        if (tile != null)
        {
            rowTiles.Add(tile.transform);
            // We need the tile's original, centered grid position as the base
            basePositions.Add(gridManager.GetWorldPosition(x, row));
        }
    }
    if (rowTiles.Count == 0) yield break;

    // Calculate the offset at the start of the animation
    float startOffset = rowTiles[0].transform.position.x - basePositions[0].x;

    float elapsed = 0f;
    while (elapsed < rowSlideDuration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / rowSlideDuration;
        float newOffset = Mathf.Lerp(startOffset, targetOffset, progress);

        for (int i = 0; i < rowTiles.Count; i++)
        {
            if (rowTiles[i] != null)
            {
                // Always calculate from the original base position
                rowTiles[i].position = new Vector3(basePositions[i].x + newOffset, basePositions[i].y, basePositions[i].z);
            }
        }
        yield return null;
    }

    // Final snap to position
    for (int i = 0; i < rowTiles.Count; i++)
    {
        if (rowTiles[i] != null)
        {
            rowTiles[i].position = new Vector3(basePositions[i].x + targetOffset, basePositions[i].y, basePositions[i].z);
        }
    }
}




    private void ClearArrowsAndDropZones()
    {
        // Destroy the canvas if it exists
        if (dropZoneCanvas != null)
        {
            Destroy(dropZoneCanvas.gameObject);
            dropZoneCanvas = null;
        }

        // Destroy all arrow and lock GameObjects
        if (gridParent != null)
        {
            List<GameObject> objectsToDestroy = new List<GameObject>();
            foreach (Transform child in gridParent)
            {
                if (child.name.StartsWith("Arrow_") || child.name.StartsWith("Lock_"))
                {
                    objectsToDestroy.Add(child.gameObject);
                }
            }
            foreach (GameObject obj in objectsToDestroy)
            {
                // Using Destroy instead of DestroyImmediate is safer in Play Mode
                Destroy(obj);
            }

            dropZones.Clear();
        }

        // Clear internal data structures
        leftArrows = null;
        rightArrows = null;
        // rowLockStates = null;
        originalArrowMaterials.Clear();
    }

    public void InitializeLockStates(int rows)
    {
        // This method creates a fresh, default set of lock states.
        rowLockStates = new RowLockState[rows];
        Debug.Log($"[RiverControls] Initialized a new lock state array for {rows} rows.");
    }



    public int[] GetLockStatesAsInts()
    {
        int[] lockStatesAsInts = new int[rowLockStates.Length];
        for (int i = 0; i < rowLockStates.Length; i++)
        {
            lockStatesAsInts[i] = (int)rowLockStates[i];
        }
        return lockStatesAsInts;
    }


    public void SetLockStatesFromInts(int[] newLockStates)
    {
        if (newLockStates == null || rowLockStates == null || newLockStates.Length != rowLockStates.Length)
        {
            Debug.LogWarning("Could not apply lock states from loaded data: Data was invalid or for a different grid size.");
            return;
        }

        for (int i = 0; i < newLockStates.Length; i++)
        {
            rowLockStates[i] = (RowLockState)newLockStates[i];
            // UpdateRowLockVisuals(i); // Update visuals for each row as it's loaded
        }
    }






    public void GenerateControlsForGrid()
    {
        // First, clear any old controls
        ClearArrowsAndDropZones();

        // Initialize the internal arrays based on the new grid size
        int rows = gridManager.rows;
        leftArrows = new PointerArrowButton[rows, 2];
        rightArrows = new PointerArrowButton[rows, 2];




        EnsureCanvasExists();


        for (int row = 0; row < rows; row++)
        {
            // --- NEW: Mode-switching logic ---
            if (gameManager != null && gameManager.currentMode == OperatingMode.Editor)
            {
                CreateArrowsForRow(row);
            }
            else // Playing Mode
            {
                if (dropZonePrefab != null)
                {
                    CreateDropZonesForRow(row);
                }
            }
        }

        // --- NEW: Final step to apply the correct visual state after creation ---
        // In Editor mode, we need to manually update the arrow visuals to match the loaded data.
        // In Play mode, the drop zones' active state is already handled during their creation.
        if (gameManager != null && gameManager.currentMode == OperatingMode.Editor)
        {
            for (int row = 0; row < rows; row++)
            {
                UpdateRowLockVisuals(row);
            }
        }


        Debug.Log($"[RiverControls] Generated controls for {rows} rows in {gameManager.currentMode} mode.");
    }


    public void CreateDropZonesForRow(int row)
    {
        EnsureCanvasExists();
        // Add a null check for rowLockStates for safety during initialization
        if (dropZonePrefab == null || row < 0 || rowLockStates == null || row >= rowLockStates.Length) return;

        // POSITIONS COME FROM TryGetDropZonePlacement, which is also what the camera framing
        // reserves against. Do not inline the maths here again - see the affordance geometry
        // region for the five drifts that cost.
        //
        // The Endless and Puzzle branches this replaced were character-for-character identical
        // expressions behind an `if (mode == Endless)`, with a comment on each explaining why
        // they differed. They did not.

        // --- Left Zone Creation (if unlocked) ---
        if (TryGetDropZonePlacement(row, true, out var leftCenter, out var leftSize))
        {
            GameObject leftZoneGO = Instantiate(dropZonePrefab, dropZoneCanvas.transform);
            leftZoneGO.name = $"DropZone_Row{row}_L";
            RectTransform leftRect = leftZoneGO.GetComponent<RectTransform>();
            leftRect.position = leftCenter;
            leftRect.sizeDelta = leftSize;
            leftRect.localScale = new Vector3(-1f, 1f, 1f); // FLIP The Left Zone
            RowDropZone leftZone = leftZoneGO.GetComponent<RowDropZone>();
            leftZone.row = row;
            leftZone.fromLeft = true;
            leftZone.riverControls = this;
            dropZones[(row, true)] = leftZone;
        }

        // --- Right Zone Creation (if unlocked) ---
        if (TryGetDropZonePlacement(row, false, out var rightCenter, out var rightSize))
        {
            GameObject rightZoneGO = Instantiate(dropZonePrefab, dropZoneCanvas.transform);
            rightZoneGO.name = $"DropZone_Row{row}_R";
            RectTransform rightRect = rightZoneGO.GetComponent<RectTransform>();
            rightRect.position = rightCenter;
            rightRect.sizeDelta = rightSize;
            rightRect.localScale = Vector3.one;
            RowDropZone rightZone = rightZoneGO.GetComponent<RowDropZone>();
            rightZone.row = row;
            rightZone.fromLeft = false;
            rightZone.riverControls = this;
            dropZones[(row, false)] = rightZone;
        }
    }







    private void CreateArrows()
    {
        int rows = gridManager.rows;
        leftArrows = new PointerArrowButton[rows, 2];  // 2 = blue and red for each row
        rightArrows = new PointerArrowButton[rows, 2];
        rowLockStates = new RowLockState[rows];

        for (int row = 0; row < rows; row++)
        {
            CreateArrowsForRow(row);
        }

        Debug.Log($"[RiverControls] Created {rows * 4} arrows for {rows} rows");
    }

    private void CreateArrowsForRow(int row)
    {
        // POSITIONS COME FROM GetArrowPlacements, which is also what the camera framing reserves
        // against. The order is fixed by that method: L_Red, L_Blue, R_Blue, R_Red, then Lock.
        _placementScratch.Clear();
        GetArrowPlacements(row, _placementScratch);
        if (_placementScratch.Count < 4) return;

        leftArrows[row, 1] = CreateSingleArrow(_placementScratch[0].Center, row, true, true);   // Red (farther)
        leftArrows[row, 0] = CreateSingleArrow(_placementScratch[1].Center, row, true, false);  // Blue (closer to grid)
        rightArrows[row, 0] = CreateSingleArrow(_placementScratch[2].Center, row, false, false);// Blue (closer to grid)
        rightArrows[row, 1] = CreateSingleArrow(_placementScratch[3].Center, row, false, true); // Red (farther)

        if (lockPrefab != null && _placementScratch.Count >= 5)
        {
            Vector3 lockPos = _placementScratch[4].Center;
            GameObject lockGO = Instantiate(lockPrefab, lockPos, Quaternion.identity, gridParent);
            lockGO.transform.localScale = Vector3.one * arrowScale;
            lockGO.name = $"Lock_Row{row}";

            var lockButton = lockGO.AddComponent<LockToggleButton>();
            lockButton.controller = this;
            lockButton.row = row;

            // Set its initial material
            Renderer lockRenderer = lockGO.GetComponent<Renderer>();
            if (lockRenderer != null && lockMaterial != null)
            {
                lockRenderer.material = lockMaterial;
            }

            if (gameManager != null && gameManager.currentMode == OperatingMode.Playing)
            {
                lockGO.SetActive(false);
            }




        }



    }

    // ================================================================= affordance geometry
    //
    // ONE SOURCE OF TRUTH FOR WHERE AFFORDANCES GO.
    //
    // Everything in this region is used TWICE: once to PLACE the arrows, locks and drop zones,
    // and once to RESERVE room for them in the camera framing. That is the whole point. The
    // camera must leave space for a drop zone before the drop zone exists - zones are created
    // during a drag, and framing that reacted to them would lurch the board mid-interaction -
    // so the reservation cannot simply measure the objects.
    //
    // The previous design had BoardFraming re-derive these formulas from scratch. It drifted
    // FIVE times: arrows reserved in modes that have none, both sides reserved on one-sided
    // levels, X reserved but not Z, Z derived at the wrong pitch, and - found while writing
    // this - an arrow reach computed from `cols * tileWidth`, silently dropping every gap,
    // where GetDynamicArrowDistance uses `(cols-1) * (tileWidth + gapX) + tileWidth`.
    //
    // A sixth drift is now impossible by construction rather than by vigilance: if a placement
    // formula changes, the reservation changes with it, because they are the same code. D5
    // asserts the consequence - every affordance that actually exists is inside the reservation.

    /// <summary>A world-axis-aligned box an affordance occupies. Size is FULL extents.</summary>
    public readonly struct AffordanceBox
    {
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly Vector3 Size;
        public AffordanceBox(string name, Vector3 center, Vector3 size)
        { Name = name; Center = center; Size = size; }
        public Bounds ToBounds() => new Bounds(Center, Size);
    }

    // Prefab mesh extents, measured once. The old reservation used a hardcoded `arrowScale * 2f`
    // for this, which is a guess about art that nobody re-checks when the art changes.
    bool _prefabExtentsMeasured;
    Vector3 _arrowLocalSize, _lockLocalSize;

    // Reused so per-row placement does not allocate. Mobile target - house rule.
    readonly List<AffordanceBox> _placementScratch = new List<AffordanceBox>();

    void MeasurePrefabExtents()
    {
        if (_prefabExtentsMeasured) return;
        _prefabExtentsMeasured = true;
        _arrowLocalSize = LocalMeshSize(arrowPrefab);
        _lockLocalSize = LocalMeshSize(lockPrefab);
    }

    /// <summary>
    /// Combined mesh size of a prefab IN THE PREFAB ROOT'S OWN LOCAL SPACE. Reads sharedMesh
    /// rather than renderer.bounds because a prefab asset is not in the scene and has no world
    /// bounds. Falls back to a unit cube, which is what CreateSingleArrow itself falls back to
    /// when arrowPrefab is null.
    ///
    /// THE ROOT'S OWN SCALE IS DIVIDED OUT, and that is not a detail. CreateSingleArrow does
    /// `arrow.transform.localScale = Vector3.one * arrowScale`, OVERWRITING whatever the prefab
    /// was authored at - so the authored root scale never reaches the scene and must not reach
    /// this measurement either. Including it made the lock reservation exactly half the real
    /// size, because the lock prefab is authored at root scale 0.5. D5 caught that on its first
    /// run, which is the entire reason D5 compares against real objects rather than the formula.
    /// </summary>
    static Vector3 LocalMeshSize(GameObject prefab)
    {
        if (prefab == null) return Vector3.one;

        Vector3 rootScale = prefab.transform.lossyScale;
        Vector3 Safe(Vector3 v) => new Vector3(
            Mathf.Approximately(v.x, 0f) ? 1f : v.x,
            Mathf.Approximately(v.y, 0f) ? 1f : v.y,
            Mathf.Approximately(v.z, 0f) ? 1f : v.z);
        rootScale = Safe(rootScale);

        bool any = false;
        Bounds acc = default;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;

            // InverseTransformPoint already expresses the centre in root-local units.
            var centre = prefab.transform.InverseTransformPoint(mf.transform.TransformPoint(b.center));

            // Size relative to the root: the child's world scale with the root's divided out.
            var child = mf.transform.lossyScale;
            var rel = new Vector3(child.x / rootScale.x, child.y / rootScale.y, child.z / rootScale.z);
            var size = Vector3.Scale(b.size, rel);

            var childBox = new Bounds(centre, size);
            if (!any) { acc = childBox; any = true; } else acc.Encapsulate(childBox);
        }
        return any ? acc.size : Vector3.one;
    }

    /// <summary>
    /// Where a drop zone for this row/side goes, and how big it is in world space.
    /// Returns false when that side carries no zone (locked, or the row is out of range).
    ///
    /// The canvas is world-space and rotated Euler(90,0,0), so it lies FLAT in the XZ plane:
    /// the rect's width maps to world X and its height to world Z, and its Y extent is zero.
    /// That is why this needs no pitch - the zones lie on the ground, they do not face the
    /// camera, so their world extent does not move when the camera angle does.
    /// </summary>
    public bool TryGetDropZonePlacement(int row, bool left, out Vector3 center, out Vector2 sizeXZ)
    {
        center = default; sizeXZ = default;
        if (gridManager == null || row < 0 || row >= gridManager.rows) return false;

        RowLockState state = GetRowLockState(row);
        bool open = left ? (state != RowLockState.LeftLocked && state != RowLockState.BothLocked)
                         : (state != RowLockState.RightLocked && state != RowLockState.BothLocked);
        if (!open) return false;

        Vector3 rowCenter = GetRowCenterPosition(row);
        float gridHalfWidth = (gridManager.cols * gridManager.tileWidth
                               + (gridManager.cols - 1) * gridManager.gapX) / 2f;
        float zoneWidth = gridManager.tileWidth * 1.5f;
        float zoneHeight = gridManager.tileHeight + (gridManager.gapZ * 0.5f);
        float offsetFromEdge = zoneWidth / 2f;

        // Endless and Puzzle used to be separate branches computing the identical expression.
        float x = left ? rowCenter.x - gridHalfWidth - offsetFromEdge + 2f
                       : rowCenter.x + gridHalfWidth + offsetFromEdge - 2f;

        center = new Vector3(x, rowCenter.y, rowCenter.z - 0.25f);
        sizeXZ = new Vector2(zoneWidth, zoneHeight);
        return true;
    }

    /// <summary>
    /// The four arrows and the row lock for a row, in world space. Editor-only affordances:
    /// GenerateControlsForGrid creates arrows ONLY when currentMode == Editor.
    ///
    /// Arrows exist on both sides regardless of lock state, because the lock toggle is how you
    /// change that state - it has to stay reachable.
    /// </summary>
    public void GetArrowPlacements(int row, List<AffordanceBox> into)
    {
        if (into == null || gridManager == null || row < 0 || row >= gridManager.rows) return;
        MeasurePrefabExtents();

        Vector3 rowCenter = GetRowCenterPosition(row);
        float dyn = GetDynamicArrowDistance();
        Vector3 arrowSize = _arrowLocalSize * arrowScale;
        Vector3 lockSize = _lockLocalSize * arrowScale;

        Vector3 leftBase = rowCenter + Vector3.left * dyn + Vector3.up * arrowHeight;
        Vector3 rightBase = rowCenter + Vector3.right * dyn + Vector3.up * arrowHeight;

        into.Add(new AffordanceBox($"Arrow_Row{row}_L_Red",
                 leftBase + Vector3.left * arrowSpacing * 0.5f, arrowSize));
        into.Add(new AffordanceBox($"Arrow_Row{row}_L_Blue",
                 leftBase + Vector3.right * arrowSpacing * 0.5f, arrowSize));
        into.Add(new AffordanceBox($"Arrow_Row{row}_R_Blue",
                 rightBase + Vector3.left * arrowSpacing * 0.5f, arrowSize));
        into.Add(new AffordanceBox($"Arrow_Row{row}_R_Red",
                 rightBase + Vector3.right * arrowSpacing * 0.5f, arrowSize));

        if (lockPrefab != null)
            into.Add(new AffordanceBox($"Lock_Row{row}",
                     rightBase + Vector3.right * (arrowSpacing * 1.5f), lockSize));
    }

    /// <summary>
    /// THE ROOM THE CAMERA MUST LEAVE for everything the player has to touch, for the current
    /// mode and the current lock states. This is what BoardFraming asks for instead of
    /// re-deriving; see the region header for why that distinction has cost five corrections.
    ///
    /// Static for a given level: lock states are fixed at load, so the answer cannot thrash
    /// while the player interacts.
    ///
    /// NOT PITCH-DEPENDENT, deliberately. Every affordance is either a ground-plane world-space
    /// UI rect or a mesh at a fixed world position; none of them billboard. The pitch-sensitivity
    /// that showed up in C2 is in the FIT, not in the reservation - BoardFraming already takes
    /// pitch as an input and it is the projection of these boxes that moves, not the boxes.
    /// </summary>
    public bool TryGetReservedBounds(out Bounds reserved)
    {
        reserved = default;
        if (gridManager == null || gridManager.rows <= 0 || gridManager.cols <= 0) return false;

        bool editorMode = gameManager == null || gameManager.currentMode == OperatingMode.Editor;
        bool any = false;

        // Local, because C# forbids touching an `out` parameter from a local function.
        Bounds acc = default;

        void Take(Bounds b)
        {
            if (!any) { acc = b; any = true; } else acc.Encapsulate(b);
        }

        var arrows = new List<AffordanceBox>();
        for (int row = 0; row < gridManager.rows; row++)
        {
            if (editorMode)
            {
                arrows.Clear();
                GetArrowPlacements(row, arrows);
                foreach (var a in arrows) Take(a.ToBounds());
            }
            else
            {
                if (TryGetDropZonePlacement(row, true, out var cL, out var sL))
                    Take(new Bounds(cL, new Vector3(sL.x, 0f, sL.y)));
                if (TryGetDropZonePlacement(row, false, out var cR, out var sR))
                    Take(new Bounds(cR, new Vector3(sR.x, 0f, sR.y)));
            }
        }

        reserved = acc;
        return any;
    }

    private PointerArrowButton CreateSingleArrow(Vector3 position, int row, bool fromLeft, bool isRed)
    {
        GameObject arrow;

        if (arrowPrefab != null)
        {
            arrow = Instantiate(arrowPrefab, position, Quaternion.identity, gridParent);
        }
        else
        {
            // Create simple cube as fallback
            arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.transform.position = position;
            arrow.transform.parent = gridParent;
        }

        arrow.transform.localScale = Vector3.one * arrowScale;
        arrow.name = $"Arrow_Row{row}_{(fromLeft ? "L" : "R")}_{(isRed ? "Red" : "Blue")}";
        arrow.layer = LayerMask.NameToLayer("UI") != -1 ? LayerMask.NameToLayer("UI") : 5; // Put on UI layer or layer 5

        // Ensure it has a collider for mouse detection - make it much bigger for easier clicking
        Collider collider = arrow.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = arrow.AddComponent<BoxCollider>();
            boxCollider.size = Vector3.one * 3f; // Make clickable area much bigger than visual
            boxCollider.isTrigger = true; // Make it a trigger so it doesn't block physics
        }
        else if (collider is BoxCollider boxCol)
        {
            boxCol.size = Vector3.one * 3f; // Make existing collider bigger
            boxCol.isTrigger = true; // Make it a trigger so it doesn't block physics
        }

        // Set material color
        Renderer renderer = arrow.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (isRed && redMaterial != null)
            {
                renderer.material = redMaterial;
            }
            else if (!isRed && blueMaterial != null)
            {
                renderer.material = blueMaterial;
            }
            else
            {
                // Create new material instance with color
                Material newMat = new Material(renderer.material);
                newMat.color = isRed ? Color.red : Color.blue;
                renderer.material = newMat;
            }
        }

        // Add the ArrowButton component and initialize it
        PointerArrowButton arrowButton = arrow.AddComponent<PointerArrowButton>();
        arrowButton.Initialize(row, fromLeft, isRed, this);

        // Debug the arrow creation
        Debug.Log($"Created arrow: {arrow.name} at position {position} with collider: {arrow.GetComponent<Collider>() != null}");

        // Store the original material so we can restore it later when unlocking
        if (renderer != null && !originalArrowMaterials.ContainsKey(renderer))
        {
            originalArrowMaterials[renderer] = renderer.material;
        }



        return arrowButton;
    }

    private Vector3 GetRowCenterPosition(int row)
    {
        // This new version is simpler and guarantees correctness by using the GridManager's
        // own stable coordinate system, which has a non-changing origin point.

        // Get the world position of the first and last tile in the given row.
        Vector3 leftEdgePos = gridManager.GetWorldPosition(0, row);
        Vector3 rightEdgePos = gridManager.GetWorldPosition(gridManager.cols - 1, row);

        // The true center is the average of these two points.
        return (leftEdgePos + rightEdgePos) / 2f;
    }

    // Also add this method to dynamically calculate arrow distance based on grid size:
    private float GetDynamicArrowDistance()
    {
        // FIXED: Scale arrow distance based on grid width, but ensure minimum clearance
        float gridWidth = (gridManager.cols - 1) * (gridManager.tileWidth + gridManager.gapX) + gridManager.tileWidth;

        // Use the original arrowDistance as base, then add proportional scaling
        // This ensures arrows are always outside the grid regardless of size
        float baseDistance = arrowDistance; // Use the Inspector value as minimum
        float scaledDistance = gridWidth * 0.5f + 1f; // Half grid width + 1 unit clearance

        return Mathf.Max(baseDistance, scaledDistance);
    }

    // Public methods for manual control
    public void PushFromLeft(int row, bool redSide)
    {
        if (gridManager != null && !gridManager.IsPushInProgress())
            gridManager.PushRowFromSide(row, true, redSide);
    }

    public void PushFromRight(int row, bool redSide)
    {
        if (gridManager != null && !gridManager.IsPushInProgress())
            gridManager.PushRowFromSide(row, false, redSide);
    }

    // This is called by LockToggleButton when a lock is clicked.
    public void OnLockButtonClicked(int row)
    {
        if (row < 0 || row >= rowLockStates.Length) return;

        // Get the current state as an integer, add 1, and wrap around if it goes past the last state (3).
        int currentStateInt = (int)rowLockStates[row];
        int nextStateInt = (currentStateInt + 1) % 4; // 4 is the number of states in our enum
        rowLockStates[row] = (RowLockState)nextStateInt;

        Debug.Log($"Row {row} lock state is now: {rowLockStates[row]}");

        // Update the visuals to reflect the new state.
        UpdateRowLockVisuals(row);
    }

    // This updates the materials for all controls on a specific row.
    private void UpdateRowLockVisuals(int row)
    {
        RowLockState state = rowLockStates[row];

        // Determine which sides should be visually locked
        bool lockLeft = (state == RowLockState.LeftLocked || state == RowLockState.BothLocked);
        bool lockRight = (state == RowLockState.RightLocked || state == RowLockState.BothLocked);

        // Get all the arrow renderers for this row
        Renderer[] leftRowArrows = { leftArrows[row, 0]?.GetComponent<Renderer>(), leftArrows[row, 1]?.GetComponent<Renderer>() };
        Renderer[] rightRowArrows = { rightArrows[row, 0]?.GetComponent<Renderer>(), rightArrows[row, 1]?.GetComponent<Renderer>() };

        // Update the left arrows
        foreach (var arrowRenderer in leftRowArrows)
        {
            if (arrowRenderer != null)
            {
                // If this side is locked, apply the gray material. Otherwise, restore its original material.
                arrowRenderer.material = lockLeft ? lockedMaterial : originalArrowMaterials[arrowRenderer];
            }
        }

        // Update the right arrows
        foreach (var arrowRenderer in rightRowArrows)
        {
            if (arrowRenderer != null)
            {
                arrowRenderer.material = lockRight ? lockedMaterial : originalArrowMaterials[arrowRenderer];
            }
        }

        // Update the lock button's own visual
        var lockRenderer = gridParent.Find($"Lock_Row{row}")?.GetComponent<Renderer>();
        if (lockRenderer != null)
        {
            // The lock icon itself can turn grey when both sides are locked, as a clear indicator.
            lockRenderer.material = (state == RowLockState.BothLocked) ? lockedMaterial : lockMaterial;
        }

        // We also need to enable/disable the colliders to match the visuals
        SetArrowCollidersEnabledForSide(row, true, !lockLeft);  // Enable left arrows if NOT locked
        SetArrowCollidersEnabledForSide(row, false, !lockRight); // Enable right arrows if NOT locked
    }


    private void SetArrowCollidersEnabledForSide(int row, bool isLeftSide, bool enabled)
    {
        PointerArrowButton[,] arrows = isLeftSide ? leftArrows : rightArrows;
        if (arrows == null) return;

        for (int side = 0; side < 2; side++) // 0=blue, 1=red
        {
            if (arrows[row, side]?.gameObject != null)
            {
                var collider = arrows[row, side].GetComponent<Collider>();
                if (collider != null) collider.enabled = enabled;
            }
        }
    }

    public void OnArrowClicked(int row, bool fromLeft, bool isRed)
    {
        RowLockState state = rowLockStates[row];
        if ((fromLeft && (state == RowLockState.LeftLocked || state == RowLockState.BothLocked)) ||
            (!fromLeft && (state == RowLockState.RightLocked || state == RowLockState.BothLocked)))
        {
            Debug.Log($"Row {row} is locked on the {(fromLeft ? "left" : "right")} side. Push ignored.");
            return;
        }

        if (gridManager.IsPushInProgress()) return;

        // If the editor is present, it is the BOSS. It handles EVERYTHING.
        if (levelEditorManager != null)
        {
            StartCoroutine(levelEditorManager.HandleArrowPush(row, fromLeft, isRed));
        }
        // If there's no editor (e.g., in a final game scene), fall back to the basic sandbox behavior.
        else
        {
            StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, isRed));
        }
    }

    private IEnumerator HandlePushWithReselect(bool hadSelectedBoat, int row, bool fromLeft, bool isRed)
    {
        // 1. Start the push and WAIT for it to complete (GridManager handles deselection internally)
        yield return StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, isRed));

        // 2. If we had a selected boat before, try to find and reselect it
        if (hadSelectedBoat && boatManager != null)
        {
            // Find the first boat that can still move and select it
            foreach (var boat in boatManager.GetPlayerBoats())
            {
                if (boat != null && boat.maxMovementPoints > 0) // Or whatever your condition is
                {
                    boat.SelectBoat();
                    break; // Only select the first available boat
                }
            }
        }
    }

    // Temporarily disable arrow colliders during pushes
    public void SetArrowCollidersEnabled(bool enabled, int specificRow = -1)
    {
        if (leftArrows == null) return;

        // If a specific row is given, only iterate for that one row.
        int startRow = (specificRow == -1) ? 0 : specificRow;
        int endRow = (specificRow == -1) ? leftArrows.GetLength(0) : specificRow + 1;

        // NEW VERSION: This is the new code that enables/disables colliders for a specific range of rows.
        for (int row = startRow; row < endRow; row++)
        {
            if (enabled)
            {
                // If we are ENABLING arrows, we must respect their lock state.
                // We can just call our existing visual update method, which also handles colliders.
                UpdateRowLockVisuals(row);
            }
            else
            {
                // If we are DISABLING arrows (because a push is in progress),
                // we disable ALL of them temporarily, regardless of their lock state.
                SetArrowCollidersEnabledForSide(row, true, false);  // Disable left side
                SetArrowCollidersEnabledForSide(row, false, false); // Disable right side
            }
        }


    }




    public void UpdateControlsForMode()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null || rowLockStates == null) return;

        bool shouldLocksBeVisible = (gameManager.currentMode == OperatingMode.Editor);

        for (int row = 0; row < rowLockStates.Length; row++)
        {
            Transform lockTransform = gridParent.Find($"Lock_Row{row}");
            if (lockTransform != null)
            {
                lockTransform.gameObject.SetActive(shouldLocksBeVisible);
            }
        }
    }


    public RowDropZone GetDropZone(int row, bool fromLeft)
    {
        dropZones.TryGetValue((row, fromLeft), out RowDropZone zone);
        return zone;
    }




    public void DestroyControlsForRow(int row)
    {
        // Destroy Drop Zones
        if (dropZones.TryGetValue((row, true), out RowDropZone leftZone) && leftZone != null)
        {
            Destroy(leftZone.gameObject);
            dropZones.Remove((row, true));
        }
        if (dropZones.TryGetValue((row, false), out RowDropZone rightZone) && rightZone != null)
        {
            Destroy(rightZone.gameObject);
            dropZones.Remove((row, false));
        }
    }


    public void SetDropZoneCanvas(Canvas canvas)
    {
        this.dropZoneCanvas = canvas;
    }


    public void ExpandLockStates(int newRowCount)
    {
        // If the array is already big enough, do nothing.
        if (rowLockStates != null && newRowCount <= rowLockStates.Length)
        {
            return;
        }

        Debug.Log($"[RiverControls] Expanding lock states from {rowLockStates?.Length ?? 0} to {newRowCount}.");

        // Create a new array of the required size.
        // All new elements will default to RowLockState.Unlocked, which is what we want for Endless Mode.
        RowLockState[] newStates = new RowLockState[newRowCount];

        // If an old array exists, copy the old data into the new one.
        if (rowLockStates != null)
        {
            for (int i = 0; i < rowLockStates.Length; i++)
            {
                newStates[i] = rowLockStates[i];
            }
        }

        // Replace the old array with the new, larger one.
        rowLockStates = newStates;
    }


    private void EnsureCanvasExists()
    {
        // If the canvas already exists and is active, do nothing.
        if (dropZoneCanvas != null) return;

        Debug.LogWarning("[RiverControls] DropZone Canvas was missing. Creating one dynamically.");

        GameObject canvasGO = new GameObject("DropZoneCanvas");
        canvasGO.transform.SetParent(this.transform);
        dropZoneCanvas = canvasGO.AddComponent<Canvas>();
        dropZoneCanvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<GraphicRaycaster>();

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            dropZoneCanvas.worldCamera = mainCamera;
        }
        else
        {
            Debug.LogError("[RiverControls] Main Camera not found! Drop zones will not work.");
        }
        dropZoneCanvas.transform.rotation = Quaternion.Euler(90, 0, 0);
    }





}