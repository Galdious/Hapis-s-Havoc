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
    private Dictionary<Renderer, Material> originalArrowMaterials = new Dictionary<Renderer, Material>(); // <<< ADD
    private GameManager gameManager;
    private Canvas dropZoneCanvas;
    private Dictionary<int, Coroutine> runningRowAnimations = new Dictionary<int, Coroutine>();

    // ArrowButton class removed - now using PointerArrowButton

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

        gameManager = FindFirstObjectByType<GameManager>();

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





    // REPLACES the old GenerateArrowsForGrid method entirely.
    public void GenerateControlsForGrid()
    {
        // First, clear any old controls
        ClearArrowsAndDropZones();

        // Initialize the internal arrays based on the new grid size
        int rows = gridManager.rows;
        leftArrows = new PointerArrowButton[rows, 2];
        rightArrows = new PointerArrowButton[rows, 2];




        // --- NEW: Create the World Space Canvas dynamically ---
        if (gameManager != null && gameManager.currentMode == OperatingMode.Playing)
        {
            GameObject canvasGO = new GameObject("DropZoneCanvas");
            canvasGO.transform.SetParent(this.transform); // Attach to RiverControls
            dropZoneCanvas = canvasGO.AddComponent<Canvas>();
            dropZoneCanvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<GraphicRaycaster>(); // Required for UI interaction


            // Find the main camera to assign to the event camera
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

    
    private void CreateDropZonesForRow(int row)
    {
        if (dropZoneCanvas == null) return;

        Vector3 rowCenter = GetRowCenterPosition(row);

        float gridHalfWidth = (gridManager.cols * gridManager.tileWidth + (gridManager.cols - 1) * gridManager.gapX) / 2f;
        float zoneWidth = gridManager.tileWidth * 1.5f;
        float zoneHeight = gridManager.tileHeight + (gridManager.gapZ * 0.5f);
        float offsetFromEdge = zoneWidth / 2f; // Position center of zone at the grid edge

        // Left Drop Zone
        GameObject leftZoneGO = Instantiate(dropZonePrefab, dropZoneCanvas.transform);
        leftZoneGO.name = $"DropZone_Row{row}_L";
        RectTransform leftRect = leftZoneGO.GetComponent<RectTransform>();
        leftRect.position = new Vector3(rowCenter.x - gridHalfWidth - offsetFromEdge, rowCenter.y, rowCenter.z);
        leftRect.sizeDelta = new Vector2(zoneWidth, zoneHeight);
        RowDropZone leftZone = leftZoneGO.GetComponent<RowDropZone>();
        leftZone.row = row;
        leftZone.fromLeft = true;
        leftZone.riverControls = this;
        if (rowLockStates[row] == RowLockState.LeftLocked || rowLockStates[row] == RowLockState.BothLocked) leftZoneGO.SetActive(false);


        // Right Drop Zone
        GameObject rightZoneGO = Instantiate(dropZonePrefab, dropZoneCanvas.transform);
        rightZoneGO.name = $"DropZone_Row{row}_R";
        RectTransform rightRect = rightZoneGO.GetComponent<RectTransform>();
        rightRect.position = new Vector3(rowCenter.x + gridHalfWidth + offsetFromEdge, rowCenter.y, rowCenter.z);
        rightRect.sizeDelta = new Vector2(zoneWidth, zoneHeight);
        RowDropZone rightZone = rightZoneGO.GetComponent<RowDropZone>();
        rightZone.row = row;
        rightZone.fromLeft = false;
        rightZone.riverControls = this;
        if (rowLockStates[row] == RowLockState.RightLocked || rowLockStates[row] == RowLockState.BothLocked) rightZoneGO.SetActive(false);
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
        // Get row center position from grid
        Vector3 rowCenter = GetRowCenterPosition(row);

        // FIXED: Use dynamic arrow distance that scales with grid size
        float dynamicArrowDistance = GetDynamicArrowDistance();

        // Left side arrows: Red (far) - Blue (near grid) - elevated above tiles
        Vector3 leftBasePos = rowCenter + Vector3.left * dynamicArrowDistance + Vector3.up * arrowHeight;
        leftArrows[row, 1] = CreateSingleArrow(leftBasePos + Vector3.left * arrowSpacing * 0.5f, row, true, true);   // Red (farther)
        leftArrows[row, 0] = CreateSingleArrow(leftBasePos + Vector3.right * arrowSpacing * 0.5f, row, true, false); // Blue (closer to grid)

        // Right side arrows: Blue (near grid) - Red (far) - elevated above tiles
        Vector3 rightBasePos = rowCenter + Vector3.right * dynamicArrowDistance + Vector3.up * arrowHeight;
        rightArrows[row, 0] = CreateSingleArrow(rightBasePos + Vector3.left * arrowSpacing * 0.5f, row, false, false); // Blue (closer to grid)
        rightArrows[row, 1] = CreateSingleArrow(rightBasePos + Vector3.right * arrowSpacing * 0.5f, row, false, true);  // Red (farther)

        // Position the lock to the right of the red arrow
        Vector3 lockPos = rightBasePos + Vector3.right * (arrowSpacing * 1.5f);
        if (lockPrefab != null)
        {
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
        arrow.tag = "Arrow"; // Add tag for identification
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
        // FIXED: Calculate the center position of a row using ACTUAL GridManager values
        // This mirrors the GridManager's position calculation exactly
        float totalWidth = (gridManager.cols - 1) * (gridManager.tileWidth + gridManager.gapX);
        float totalHeight = (gridManager.rows - 1) * (gridManager.tileHeight + gridManager.gapZ);

        Vector3 boardOrigin = new Vector3(-totalWidth / 2f, 0f, -totalHeight / 2f);

        return boardOrigin + new Vector3(
            totalWidth / 2f,  // center X
            0f,
            row * (gridManager.tileHeight + gridManager.gapZ));
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



        // OLD VERSION: This was the original code that enabled/disabled colliders for all rows.

        // for (int row = startRow; row < endRow; row++)
        // {
        //     // vvv ADD THIS CHECK vvv
        //     // Even if we're asked to enable them, if the row is locked, KEEP them disabled.
        //     bool finalEnabledState = enabled && !rowLockStates[row];
        //     // ^^^ END OF ADDED CHECK ^^^

        //     for (int side = 0; side < 2; side++)
        //     {
        //         if (leftArrows[row, side]?.gameObject != null)
        //         {
        //             var collider = leftArrows[row, side].GetComponent<Collider>();
        //             if (collider != null) collider.enabled = finalEnabledState; // Use the new final state
        //         }
        //         if (rightArrows[row, side]?.gameObject != null)
        //         {
        //             var collider = rightArrows[row, side].GetComponent<Collider>();
        //             if (collider != null) collider.enabled = finalEnabledState; // Use the new final state
        //         }
        //     }
        // }
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





















}