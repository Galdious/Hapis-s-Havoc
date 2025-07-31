/*
 *  RiverControls.cs
 *  ---------------------------------------------------------------
 *  Creates visual arrow controls around the river grid for testing.
 *  Blue arrows = insert blue (river) tile
 *  Red arrows = insert red (obstacle) tile
 *  Click arrows to push tiles into rows from left/right sides.
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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

    

    private PointerArrowButton[,] leftArrows;   // [row, side] 0=blue, 1=red
    private PointerArrowButton[,] rightArrows;  // [row, side] 0=blue, 1=red
    private bool[] rowLockStates; // <<< ADD
    private Dictionary<Renderer, Material> originalArrowMaterials = new Dictionary<Renderer, Material>(); // <<< ADD

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

        //CreateArrows();  // commented out for level editor mode
    }
    
    private void ClearArrows()
{
    if (leftArrows == null) return; // Nothing to clear

    for (int row = 0; row < leftArrows.GetLength(0); row++)
    {
        // Check both blue (0) and red (1) arrows
        if (leftArrows[row, 0] != null) Destroy(leftArrows[row, 0].gameObject);
        if (leftArrows[row, 1] != null) Destroy(leftArrows[row, 1].gameObject);

        if (rightArrows[row, 0] != null) Destroy(rightArrows[row, 0].gameObject);
        if (rightArrows[row, 1] != null) Destroy(rightArrows[row, 1].gameObject);
    }

    leftArrows = null;
    rightArrows = null;
}

/// <summary>
/// Public method to be called by an external manager to generate arrows.
/// </summary>
public void GenerateArrowsForGrid()
{
    ClearArrows();
    CreateArrows();
}

    private void CreateArrows()
    {
        int rows = gridManager.rows;
        leftArrows = new PointerArrowButton[rows, 2];  // 2 = blue and red for each row
        rightArrows = new PointerArrowButton[rows, 2];
        rowLockStates = new bool[rows];

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

        // Flip the boolean state for the given row.
        rowLockStates[row] = !rowLockStates[row];
        Debug.Log($"Row {row} lock state is now: {(rowLockStates[row] ? "LOCKED" : "UNLOCKED")}");

        // Update the visuals to reflect the new state.
        UpdateRowLockVisuals(row);
    }
    
    // This updates the materials for all controls on a specific row.
private void UpdateRowLockVisuals(int row)
{
    bool isLocked = rowLockStates[row];
    Material materialToApply = isLocked ? lockedMaterial : null; // Use null to signal restoration

    // Find all controls for this row
    var leftBlueArrow = leftArrows[row, 0]?.GetComponent<Renderer>();
    var leftRedArrow = leftArrows[row, 1]?.GetComponent<Renderer>();
    var rightBlueArrow = rightArrows[row, 0]?.GetComponent<Renderer>();
    var rightRedArrow = rightArrows[row, 1]?.GetComponent<Renderer>();
    
    // Find the lock button's renderer
    // Note: This relies on the lock's name being consistent from when we spawned it.
    var lockRenderer = gridParent.Find($"Lock_Row{row}")?.GetComponent<Renderer>();

    // An array to easily loop through all arrow renderers for this row
    Renderer[] rowArrows = { leftBlueArrow, leftRedArrow, rightBlueArrow, rightRedArrow };

    foreach (var arrowRenderer in rowArrows)
    {
        if (arrowRenderer != null)
        {
            // If we are locking, apply the gray locked material.
            // If we are unlocking, restore the original material we saved in the dictionary.
            arrowRenderer.material = isLocked ? materialToApply : originalArrowMaterials[arrowRenderer];
        }
    }

    if (lockRenderer != null)
    {
        // The lock button itself also toggles between locked and unlocked materials.
        lockRenderer.material = isLocked ? lockedMaterial : lockMaterial;
    }

    // Toggle the colliders as well
    SetArrowCollidersEnabled(!isLocked, row);
}

    public void OnArrowClicked(int row, bool fromLeft, bool isRed)
    {
        if (rowLockStates[row])
        {
        Debug.Log($"Row {row} is locked. Push ignored.");
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

    for (int row = startRow; row < endRow; row++)
    {
        // vvv ADD THIS CHECK vvv
        // Even if we're asked to enable them, if the row is locked, KEEP them disabled.
        bool finalEnabledState = enabled && !rowLockStates[row];
        // ^^^ END OF ADDED CHECK ^^^

        for (int side = 0; side < 2; side++)
        {
            if (leftArrows[row, side]?.gameObject != null)
            {
                var collider = leftArrows[row, side].GetComponent<Collider>();
                if (collider != null) collider.enabled = finalEnabledState; // Use the new final state
            }
            if (rightArrows[row, side]?.gameObject != null)
            {
                var collider = rightArrows[row, side].GetComponent<Collider>();
                if (collider != null) collider.enabled = finalEnabledState; // Use the new final state
            }
        }
    }
}
}