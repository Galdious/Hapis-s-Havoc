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

public class RiverControls : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public Transform gridParent;  // same as GridManager's gridParent
    public BoatManager boatManager;

    [Header("Arrow Settings")]
    public GameObject arrowPrefab;  // simple cube or arrow mesh
    public Material blueMaterial;   // for blue/river arrows
    public Material redMaterial;    // for red/obstacle arrows
    public float arrowDistance = 4f; // how far from grid edge (back to original)
    public float arrowHeight = 4f;   // how high above tiles
    public float arrowScale = 0.3f;  // size of arrow cubes (made smaller)
    public float arrowSpacing = 0.8f; // space between blue and red arrows

    [Header("Visual Feedback")]
    public bool showHoverEffect = true;
    public Color hoverColor = Color.yellow;

    private PointerArrowButton[,] leftArrows;   // [row, side] 0=blue, 1=red
    private PointerArrowButton[,] rightArrows;  // [row, side] 0=blue, 1=red

    // ArrowButton class removed - now using PointerArrowButton

    private void Start()
    {
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
        
        CreateArrows();
    }

    private void CreateArrows()
    {
        int rows = gridManager.rows;
        leftArrows = new PointerArrowButton[rows, 2];  // 2 = blue and red for each row
        rightArrows = new PointerArrowButton[rows, 2];
        
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




public void OnArrowClicked(int row, bool fromLeft, bool isRed)
{
    if (gridManager.IsPushInProgress()) return;

    BoatController selectedBoat = boatManager.GetSelectedBoat();
    bool hadSelectedBoat = selectedBoat != null;

    // Always push the row normally - GridManager now handles clearing selections internally
    StartCoroutine(HandlePushWithReselect(hadSelectedBoat, row, fromLeft, isRed));
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
    public void SetArrowCollidersEnabled(bool enabled)
    {
        if (leftArrows != null)
        {
            for (int row = 0; row < leftArrows.GetLength(0); row++)
            {
                for (int side = 0; side < 2; side++)
                {
                    if (leftArrows[row, side]?.gameObject != null)
                    {
                        var collider = leftArrows[row, side].GetComponent<Collider>();
                        if (collider != null) collider.enabled = enabled;
                    }
                    if (rightArrows[row, side]?.gameObject != null)
                    {
                        var collider = rightArrows[row, side].GetComponent<Collider>();
                        if (collider != null) collider.enabled = enabled;
                    }
                }
            }
        }
    }
}