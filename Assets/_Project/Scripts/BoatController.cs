/*
 *  BoatController.cs - v03
 *  ---------------------------------------------------------------
 *  - VERSION 03: Fixes the bank material bug.
 *
 *  - This script is based on the working version 02.
 *  - CHANGE: The logic for highlighting and clearing highlights on BANKS
 *    has been updated to correctly use sharedMaterial, mirroring the
 *    successful fix for the tiles. This prevents banks from staying cyan.
 *  - No other code has been modified.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;

public class BoatController : MonoBehaviour, IPointerClickHandler
{
    [Header("Boat Settings")]
    public float snapOffset = 0.15f;
    public float moveSpeed = 1f;
    public float hoverHeight = 0.5f;

    public float aboveTileHoverDistance = 0.1f; // How high the boat hovers above the tile when selected.
    public float bobAmount = 0.1f;
    public float bobSpeed = 2f;
    
    [Header("Tile Animation")]
    public float tileLiftDuration = 0.4f;
    public float tileLiftDelay = 0.2f;
    public AnimationCurve tileLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);




    [Header("Ejection Animation")]
[Tooltip("A short delay before the boat begins its settle animation after being ejected.")]
public float ejectionSettleDelay = 0.2f; // You can adjust this value
[Tooltip("How high above its final position the boat appears before settling.")]
public float settleStartHeight = 1.5f;
    [Tooltip("How long the settle and fade-in animation takes.")]
    public float settleDuration = 0.6f;
[Tooltip("How long the boat's fade-out takes when it is ejected from a falling tile.")]
public float ejectionFadeOutDuration = 0.2f; // A quick fade



    
    [Header("Movement System")]
    public int maxMovementPoints = 3;
    public int currentMovementPoints = 3;

    [Header("Gameplay State")]
    public bool hasCargo = false; // Placeholder for inventory logic
    
    [Header("Visual Feedback")]
    public Color selectedColor = Color.magenta;
    
    [Header("Debug")]
    public bool showDebugInfo = true;

    public TileInstance GetCurrentTile() => currentTile;

    public int GetCurrentSnapPoint() => currentSnapPoint;
    
    // --- State & References ---
    private TileInstance currentTile;
    private int currentSnapPoint = -1;
    private Transform bankPosition;
    private bool isAtBank = true;
    public bool isSelected = false;
    private bool isMoving = false;
    
    // --- ADD THESE NEW FIELDS ---
    private MeshRenderer boatRenderer;
private Color opaqueColor;
    
    private Vector3 originalBoatPosition;
    private bool isBobbing = false;
    private BoatManager boatManager;    private GridManager gridManager;
    private RiverBankManager riverBankManager;
    private List<TileInstance> validMoves = new List<TileInstance>();
    private List<GameObject> highlightedTiles = new List<GameObject>();
    private List<GameObject> highlightedBanks = new List<GameObject>();
    private Dictionary<TileInstance, int> tileToSnapPoint = new Dictionary<TileInstance, int>();
    private Dictionary<TileInstance, int> tileToReverseSnapPoint = new Dictionary<TileInstance, int>();
    

    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Material> originalBankMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<TileInstance, List<TileInstance>> reversedPathways = new Dictionary<TileInstance, List<TileInstance>>();
    
    void Start()
    {
        boatManager = FindFirstObjectByType<BoatManager>();
        gridManager = FindFirstObjectByType<GridManager>();
        riverBankManager = FindFirstObjectByType<RiverBankManager>();



        boatRenderer = GetComponentInChildren<MeshRenderer>();
        if (boatRenderer != null)
        {
            opaqueColor = boatRenderer.material.color;
        }


        if (gridManager == null) Debug.LogError("[BoatController] GridManager not found!");
        if (riverBankManager == null) Debug.LogError("[BoatController] RiverBankManager not found!");
    }
    
    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame) EndMovementTurn();
        if (Keyboard.current.rKey.wasPressedThisFrame) ResetMovementPoints();
    }



public IEnumerator FadeOutForEjection()
{
    if (boatRenderer == null) yield break;

    boatRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

    isBobbing = false;
    float elapsed = 0f;
    Color startColor = boatRenderer.material.color;

    while (elapsed < ejectionFadeOutDuration)
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / ejectionFadeOutDuration);
        
        // Lerp towards a version of the OPAQUE color with zero alpha
        Color targetColor = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
        boatRenderer.material.color = Color.Lerp(startColor, targetColor, progress);

        yield return null;
    }
    
    // Ensure it is fully transparent at the end
    boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
}

private IEnumerator FadeOutCoroutine()
{
    isBobbing = false;
    float elapsed = 0f;
    Color startColor = boatRenderer.material.color; // Read current color for smooth start

    while (elapsed < ejectionFadeOutDuration)
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / ejectionFadeOutDuration);
        
        // Lerp towards a version of the OPAQUE color with zero alpha
        Color targetColor = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
        boatRenderer.material.color = Color.Lerp(startColor, targetColor, progress);

        yield return null;
    }
    // Ensure it is fully transparent at the end
    boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
}


    public void SetAtBank(Transform bankSpawnPoint)
    {
        bankPosition = bankSpawnPoint;
        isAtBank = true;
        currentTile = null;
        currentSnapPoint = -1;
        transform.position = bankSpawnPoint.position;
        transform.rotation = bankSpawnPoint.rotation;
    }

    public void MoveToBank(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null) return;

        // This is a simplified, immediate version of the movement coroutine
        Transform targetSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        if (targetSpawn != null)
        {
            SetAtBank(targetSpawn);
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isMoving) return;

        if (gridManager != null && gridManager.isPuzzleMode && currentMovementPoints <= 0)
        {
            Debug.Log("Out of moves. Cannot select boat.");
            return; // Exit the method immediately.
        }

        if (!isSelected) SelectBoat();
        else DeselectBoat();
    }
    



private void SetStateForBank(Transform bankSpawnPoint)
{
    // This method ONLY sets the internal state variables for being at a bank.
    // It does NOT touch the transform's position or rotation, leaving that to the animation.
    bankPosition = bankSpawnPoint;
    isAtBank = true;
    currentTile = null;
    currentSnapPoint = -1;
}






    // This new public method will be called by GridManager.
    public IEnumerator AnimateToNewPositionAfterEjection(TileInstance tile, int snapPoint)
    {
        // CHANGE 2: We now "yield return" the coroutine so the calling script can wait for it.
        yield return StartCoroutine(SettleAndFadeInCoroutine(tile, snapPoint, null));
    }

    // Overload for moving to a bank.
    // CHANGE 1: The return type is now IEnumerator.
    public IEnumerator AnimateToNewPositionAfterEjection(Transform bankSpawn)
    {
        // CHANGE 2: We now "yield return" the coroutine.
        yield return StartCoroutine(SettleAndFadeInCoroutine(null, -1, bankSpawn));
    }


    // CHANGE 1: The return type is now IEnumerator.
    public IEnumerator AnimateToNewPositionAfterEjection(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null)
        {
            Debug.LogError("[BoatController] Cannot animate to bank, RiverBankManager is missing!");
            yield break; // Use yield break in an IEnumerator instead of return.
        }
        
        Transform bankSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        
        if (bankSpawn != null)
        {
            // CHANGE 2: We now "yield return" the call to the other overload.
            yield return StartCoroutine(AnimateToNewPositionAfterEjection(bankSpawn));
        }
    }

   private IEnumerator SettleAndFadeInCoroutine(TileInstance destinationTile, int snapPoint, Transform bankSpawn)
{
    // 1. Wait for the settle delay if there is one.
    if (ejectionSettleDelay > 0)
    {
        yield return new WaitForSeconds(ejectionSettleDelay);
    }

    // Safety check
    if (boatRenderer == null) yield break;
    
    // 2. Make sure the boat is fully transparent before we start.
    // Use the cached 'opaqueColor' to maintain the correct RGB values.
    boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);

    // 3. Determine the final destination position and rotation.
    Vector3 finalPos;
    Quaternion finalRot;

    if (destinationTile != null)
    {
        Vector3 snapPosition = destinationTile.snapPoints[snapPoint].position;
        Vector3 tileCenter = destinationTile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        finalPos = snapPosition - direction * snapOffset;
        finalRot = GetSnapPointRotation(destinationTile, snapPoint);
    }
    else
    {
        finalPos = bankSpawn.position;
        finalRot = bankSpawn.rotation;
    }
    
    // 4. Set the starting position for the animation (above the final spot).
    Vector3 startPos = finalPos + Vector3.up * settleStartHeight;
    transform.position = startPos;
    transform.rotation = finalRot;

    // 5. Animate the movement and fade-in over time.
    float elapsed = 0f;
    while (elapsed < settleDuration)
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / settleDuration);
        
        // Animate position from the start point to the final point.
        transform.position = Vector3.Lerp(startPos, finalPos, progress);
        
        // Animate the material color from its current state towards the fully opaque color.
        boatRenderer.material.color = Color.Lerp(boatRenderer.material.color, opaqueColor, progress);

        yield return null;
    }

    // 6. Finalize the state to ensure perfect placement and appearance.
    transform.position = finalPos;
    boatRenderer.material.color = opaqueColor;
    
    boatRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

    if (destinationTile != null)
        {
            PlaceOnTile(destinationTile, snapPoint);
        }
        else
        {
            SetStateForBank(bankSpawn);
        }
}
public void SelectBoat()
{
    // --- ADD THIS LINE ---
    // First, clear any highlights that might exist from a previous state.
    // This wipes the slate clean before we do anything else.
    ClearHighlights();

    // --- The rest of the method is the same as before ---
    if (boatManager != null) boatManager.SetSelectedBoat(this);

    isSelected = true;


    if (gridManager == null || !gridManager.isPuzzleMode)
    {
        if (currentMovementPoints <= 0)
        {
            currentMovementPoints = maxMovementPoints;
        }
    }
    
    StartCoroutine(LiftAndBobBoat(true));
    FindValidMoves();
    StartCoroutine(HighlightValidMovesWithDelay());
}
    
    public void DeselectBoat()
    {
        if (boatManager != null) boatManager.ClearSelectedBoat();
        isSelected = false;
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
    }
    
    IEnumerator LiftAndBobBoat(bool lift)
    {
        float baseY = isAtBank && bankPosition != null ? bankPosition.position.y : 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos;
        targetPos.y = lift ? baseY + hoverHeight + aboveTileHoverDistance : baseY;
        
        float liftDuration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, tileLiftCurve.Evaluate(elapsed / liftDuration));
            yield return null;
        }
        
        transform.position = targetPos;
        originalBoatPosition = targetPos;
        
        isBobbing = lift;
        if (lift) StartCoroutine(BobBoat());
    }
    
    IEnumerator BobBoat()
    {
        while (isBobbing && isSelected)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            Vector3 bobPos = originalBoatPosition;
            bobPos.y += bobOffset;
            transform.position = bobPos;
            yield return null;
        }
    }
    
    IEnumerator HighlightValidMovesWithDelay()
    {
        yield return new WaitForSeconds(tileLiftDelay);
        if(!isSelected) yield break;
        foreach (TileInstance tile in validMoves)
        {
            if (tile != null) HighlightTile(tile);
        }
    }

    void FindValidMoves()
    {
        validMoves.Clear();
        tileToSnapPoint.Clear();
        tileToReverseSnapPoint.Clear();
        reversedPathways.Clear(); // <<< ADD THIS LINE

        if (isAtBank) FindBankEntryMoves();
        else if (currentTile != null) FindRiverPathMoves();
        
        if (currentTile != null) validMoves.Remove(currentTile);
    }

void FindRiverPathMoves()
{
    if (currentTile == null || currentSnapPoint < 0) return;

    // Check all forward paths defined by the tile's connections
    foreach (var connection in currentTile.connections)
    {
        int exitSnap = (connection.from == currentSnapPoint) ? connection.to : (connection.to == currentSnapPoint ? connection.from : -1);
        if (exitSnap != -1)
        {
            FindMoveAtEndOfChain(currentTile, exitSnap, isReverseMove: false);
        }
    }
    // Also check the path for reversing your last move
    FindMoveAtEndOfChain(currentTile, currentSnapPoint, isReverseMove: true);
}

public void ApplyPenaltiesForForcedMove(List<TileInstance> crossedTiles)
{
    if (crossedTiles == null || crossedTiles.Count == 0) return;
    Debug.Log($"[BoatController] Ejected! Crossed {crossedTiles.Count} reversed tiles during forced move.");
    foreach (var tile in crossedTiles)
    {
        Debug.Log($"-- Applying placeholder penalty for crossing {tile.name}!");
    }
}

    void FindMoveAtEndOfChain(TileInstance startTile, int exitSnap, bool isReverseMove)
    {
        TileInstance currentSearchTile = startTile;
        int currentExitSnap = exitSnap;
        List<TileInstance> crossedReversedTiles = new List<TileInstance>();

        for (int i = 0; i < gridManager.cols + 2; i++) // Safety break
        {
            TileInstance neighbor = FindConnectedTile_HorizontalOnly(currentSearchTile, currentExitSnap);

            if (neighbor == null)
            {
                // --- NEW DIRECTIONAL LOGIC ---
                var (x, y) = GetTileCoordinates(currentSearchTile);

                // Get the world-space direction vector from the tile's center to the exit snap point.
                Vector3 exitDirection = (currentSearchTile.snapPoints[currentExitSnap].position - currentSearchTile.transform.position).normalized;

                // To dock at the TOP bank, the boat must be on the top row AND the path must be exiting upwards (positive Z).
                if (y == gridManager.rows - 1 && exitDirection.z > 0.1f) // Use a small threshold
                {
                    HighlightBankForDocking(RiverBankManager.BankSide.Top);
                }
                // To dock at the BOTTOM bank, the boat must be on the bottom row AND the path must be exiting downwards (negative Z).
                else if (y == 0 && exitDirection.z < -0.1f) // Use a small threshold
                {
                    HighlightBankForDocking(RiverBankManager.BankSide.Bottom);
                }
                // --- END OF NEW LOGIC ---
                return;
            }

            if (neighbor.IsReversed)
            {
                if (neighbor.IsHardBlocker) return; 

                crossedReversedTiles.Add(neighbor);
                int entrySnap = FindConnectedSnapPoint_HorizontalOnly(currentSearchTile, currentExitSnap, neighbor);
                currentExitSnap = GetOppositeSnapPoint(entrySnap);
                currentSearchTile = neighbor;
            }
            else
            {
                int landingSnap = FindConnectedSnapPoint_HorizontalOnly(currentSearchTile, currentExitSnap, neighbor);
                if (landingSnap != -1)
                {
                    if (!validMoves.Contains(neighbor)) validMoves.Add(neighbor);

                    if (isReverseMove) tileToReverseSnapPoint[neighbor] = landingSnap;
                    else tileToSnapPoint[neighbor] = landingSnap;

                    if (crossedReversedTiles.Count > 0)
                    {
                        reversedPathways[neighbor] = crossedReversedTiles;
                    }
                }
                return;
            }
        }
    }




/// Instantly stops animations and updates internal state for a forced move (like ejection).
/// This does NOT animate the boat, leaving it frozen for another script to control.
/// </summary>
public void PrepareForForcedMove()
{
    if (boatManager != null) boatManager.ClearSelectedBoat();

    isSelected = false;
    isBobbing = false;
    StopAllCoroutines();
    
    ClearHighlights();
    
    // We intentionally do NOT call LiftAndBobBoat or ClearHighlights here.
        // We want the boat to remain visually where it is, and highlights will be
        // cleared by the GridManager/ejection logic later.
        Debug.Log($"[BoatController] {name} prepared for forced move.");
}





    // Helper to find the opposite snap point for straight-line travel.
    int GetOppositeSnapPoint(int snap)
{
    switch (snap)
    {
        case 0: return 2; case 1: return 3;
        case 2: return 0; case 3: return 1;
        case 4: return 5; case 5: return 4;
        default: return -1;
    }
}
    
    (int, int) GetTileCoordinates(TileInstance tile)
    {
        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                if (gridManager.GetTileAt(x, y) == tile)
                    return (x, y);
            }
        }
        return (-1, -1);
    }
    
    // ┌──────────────────────────────────────────────────┐
    // │ │
    // │ --- !!! THIS IS THE FIRST CHANGED METHOD (v03) !!! --- │
    // │ │
    // └──────────────────────────────────────────────────┘
    void HighlightBankForDocking(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null) return;
        
        GameObject bankGO = riverBankManager.GetBankGameObject(side);
        if (bankGO != null && !highlightedBanks.Contains(bankGO))
        {
            var renderer = bankGO.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                // Correctly save the original shared material before changing the color.
                if (!originalBankMaterials.ContainsKey(renderer))
                {
                    originalBankMaterials[renderer] = renderer.sharedMaterial;
                }
                renderer.material.color = Color.cyan;
                
                highlightedBanks.Add(bankGO);
            }

            var clicker = bankGO.AddComponent<BankClickHandler>();
            clicker.targetBoat = this;
            clicker.bankSide = side;
        }
    }

    // ┌──────────────────────────────────────────────────┐
    // │ │
    // │ --- !!! THIS IS THE SECOND CHANGED METHOD (v03) !!! --- │
    // │ │
    // └──────────────────────────────────────────────────┘
    void ClearHighlights()
    {
        // Restore tile materials
        foreach (var pair in originalMaterials)
        {
            if (pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
            }
        }

        // Restore bank materials
        foreach (var pair in originalBankMaterials)
        {
            if(pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
            }
        }

        // Clean up GameObjects and components
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                StartCoroutine(LiftTileSmooth(tileGO.GetComponent<TileInstance>(), false));
                var clicker = tileGO.GetComponent<SimpleTileClickHandler>();
                if (clicker != null) DestroyImmediate(clicker);
            }
        }
        foreach(GameObject bankGO in highlightedBanks)
        {
            if(bankGO != null)
            {
                var clicker = bankGO.GetComponent<BankClickHandler>();
                if (clicker != null) Destroy(clicker);
            }
        }
        
        highlightedTiles.Clear();
        highlightedBanks.Clear();
        originalMaterials.Clear();
        originalBankMaterials.Clear();
    }
    
    public void OnTileClicked(TileInstance clickedTile)
    {
            // --- ADD THIS BLOCK to the start of the method ---
    if (reversedPathways.ContainsKey(clickedTile))
    {
        List<TileInstance> crossedTiles = reversedPathways[clickedTile];
        Debug.Log($"[BoatController] Crossed {crossedTiles.Count} reversed tiles.");
        foreach (var tile in crossedTiles)
        {
            Debug.Log($"-- Applying placeholder penalty for crossing {tile.name}!");
        }
    }
        // --- END OF BLOCK TO ADD ---
    
        if (isMoving || !isSelected || !validMoves.Contains(clickedTile) || currentMovementPoints <= 0) return;
        
        isMoving = true;
        

        if (isAtBank)
        {
            MoveFromBankToTile(clickedTile, DetermineSnapPointFromClick(clickedTile));
        }
        else
        {
            int targetSnapPoint = DetermineRiverSnapPoint(clickedTile);
            if(targetSnapPoint != -1) MoveFromTileToTile(clickedTile, targetSnapPoint);
        }
    }
    
    public void OnBankClicked(RiverBankManager.BankSide side)
    {
        if(isMoving || !isSelected || currentMovementPoints <= 0) return;

        isMoving = true;
        
        StopAllCoroutines();
        
        Transform targetSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        StartCoroutine(MoveToBankCoroutine(targetSpawn));
    }
    

void RefreshMovementOptions()
{
    isMoving = false;
    
    // Check if we are in puzzle mode before doing anything else.
    if (gridManager != null && gridManager.isPuzzleMode)
    {
        // In Puzzle Mode, running out of points is final.
        if (currentMovementPoints <= 0)
        {
            Debug.Log("PUZZLE MODE: Out of moves!");
            DeselectBoat(); // Deselect to provide feedback
            // Here, a future PuzzleGameManager would trigger the "You Lose" screen.
            // For now, the boat is just stuck, which is correct.
            return; // Stop the method here.
        }
    }



    // After a move, check if we are out of points.
        if (currentMovementPoints <= 0)
        {
            // If we have no points left, automatically end the turn.
            // DeselectBoat() will handle lowering the boat and clearing highlights.
            DeselectBoat();
        }
        else
        {
            // If we still have points, find and show the next set of moves.
            ClearHighlights();
            FindValidMoves();
            StartCoroutine(HighlightValidMovesWithDelay());
        }
}



    int DetermineRiverSnapPoint(TileInstance tile)
    {
        bool isPath = tileToSnapPoint.ContainsKey(tile);
        bool isReverse = tileToReverseSnapPoint.ContainsKey(tile);

        if (isPath && isReverse)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                int continueSnapIndex = tileToSnapPoint[tile];
                Vector3 continueSnapPos = tile.snapPoints[continueSnapIndex].position;
                int reverseSnapIndex = tileToReverseSnapPoint[tile];
                Vector3 reverseSnapPos = tile.snapPoints[reverseSnapIndex].position;

                float distanceToContinue = Vector3.Distance(hit.point, continueSnapPos);
                float distanceToReverse = Vector3.Distance(hit.point, reverseSnapPos);

                return (distanceToContinue < distanceToReverse) ? continueSnapIndex : reverseSnapIndex;
            }
        }
        else if (isPath) return tileToSnapPoint[tile];
        else if (isReverse) return tileToReverseSnapPoint[tile];

        return -1;
    }

    TileInstance FindConnectedTile(TileInstance fromTile, int snapPointIndex)
    {
        Vector3 snapPosition = fromTile.snapPoints[snapPointIndex].position;
        float minDistance = float.MaxValue;
        TileInstance closestTile = null;
        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile != fromTile)
                {
                    for (int i = 0; i < tile.snapPoints.Length; i++)
                    {
                        if (tile.snapPoints[i] != null)
                        {
                            float distance = Vector3.Distance(snapPosition, tile.snapPoints[i].position);
                            if (distance < 0.5f && distance < minDistance)
                            {
                                minDistance = distance;
                                closestTile = tile;
                            }
                        }
                    }
                }
            }
        }
        return closestTile;
    }










TileInstance FindConnectedTile_HorizontalOnly(TileInstance fromTile, int snapPointIndex)
{
    Vector2 snapPosXZ = new Vector2(fromTile.snapPoints[snapPointIndex].position.x, fromTile.snapPoints[snapPointIndex].position.z);
    float minDistance = float.MaxValue;
    TileInstance closestTile = null;

    for (int x = 0; x < gridManager.cols; x++)
    {
        for (int y = 0; y < gridManager.rows; y++)
        {
            TileInstance tile = gridManager.GetTileAt(x, y);
            if (tile != null && tile != fromTile)
            {
                for (int i = 0; i < tile.snapPoints.Length; i++)
                {
                    if (tile.snapPoints[i] != null)
                    {
                        Vector2 otherSnapPosXZ = new Vector2(tile.snapPoints[i].position.x, tile.snapPoints[i].position.z);
                        float distance = Vector2.Distance(snapPosXZ, otherSnapPosXZ);
                        if (distance < 0.5f && distance < minDistance)
                        {
                            minDistance = distance;
                            closestTile = tile;
                        }
                    }
                }
            }
        }
    }
    return closestTile;
}

// A special version of FindConnectedSnapPoint that only compares horizontal (XZ) distance.
int FindConnectedSnapPoint_HorizontalOnly(TileInstance fromTile, int fromSnapIndex, TileInstance toTile)
{
    Vector2 fromSnapPosXZ = new Vector2(fromTile.snapPoints[fromSnapIndex].position.x, fromTile.snapPoints[fromSnapIndex].position.z);
    float minDistance = float.MaxValue;
    int closestSnapPoint = -1;

    for (int i = 0; i < toTile.snapPoints.Length; i++)
    {
        if (toTile.snapPoints[i] != null)
        {
            Vector2 toSnapPosXZ = new Vector2(toTile.snapPoints[i].position.x, toTile.snapPoints[i].position.z);
            float distance = Vector2.Distance(fromSnapPosXZ, toSnapPosXZ);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestSnapPoint = i;
            }
        }
    }
    return minDistance < 0.5f ? closestSnapPoint : -1;
}












    int FindConnectedSnapPoint(TileInstance fromTile, int fromSnapIndex, TileInstance toTile)
    {
        Vector3 fromSnapPos = fromTile.snapPoints[fromSnapIndex].position;
        float minDistance = float.MaxValue;
        int closestSnapPoint = -1;
        for (int i = 0; i < toTile.snapPoints.Length; i++)
        {
            if (toTile.snapPoints[i] != null)
            {
                float distance = Vector3.Distance(fromSnapPos, toTile.snapPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestSnapPoint = i;
                }
            }
        }
        return minDistance < 0.5f ? closestSnapPoint : -1;
    }
    
void FindBankEntryMoves()
{
    int entryRow = DetermineEntryRow();
    // Determine the direction of search: +1 for up (from bottom), -1 for down (from top).
    int searchDirection = (entryRow == 0) ? 1 : -1; 
    RiverBankManager.BankSide oppositeBank = (entryRow == 0) ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;

    for (int col = 0; col < gridManager.cols; col++)
    {
        bool foundLandingTile = false;
        int currentRow = entryRow;
        List<TileInstance> crossedReversedTiles = new List<TileInstance>();

        // Search through the column until we find a non-reversed tile or go off the board.
        while (currentRow >= 0 && currentRow < gridManager.rows)
        {
            var tile = gridManager.GetTileAt(col, currentRow);

            if (tile == null)
            {
                // This column has a gap, stop searching here.
                break;
            }
            
            if (tile.IsHardBlocker)
            {
                // This is a wall. Stop searching this column immediately.
                break; 
            }

            else if (tile.IsReversed)
                {
                    // This is a reversed tile. Log it and continue searching.
                    crossedReversedTiles.Add(tile);
                    currentRow += searchDirection;
                }
                else
                {
                    // This is a valid, non-obstacle tile. Add it as a potential move.
                    if (!validMoves.Contains(tile))
                    {
                        validMoves.Add(tile);
                    }

                    // If we crossed any reversed tiles to get here, store that path.
                    if (crossedReversedTiles.Count > 0)
                    {
                        reversedPathways[tile] = crossedReversedTiles;
                    }

                    foundLandingTile = true;
                    break; // Found our landing spot for this column, so we can stop searching it.
                }
        }

        // If we searched the entire column and only found reversed tiles,
        // then the opposite bank becomes a valid move.
        if (!foundLandingTile)
        {
            HighlightBankForDocking(oppositeBank);
        }
    }
}

    int DetermineEntryRow()
    {
        if (bankPosition != null)
        {
            if (bankPosition.name.Contains("Top")) return gridManager.rows - 1;
            if (bankPosition.name.Contains("Bottom")) return 0;
        }
        return 0;
    }

    void MoveFromBankToTile(TileInstance targetTile, int snapPoint)
    {
        StopAllCoroutines();
        ClearNonTargetHighlights(targetTile);
        StartCoroutine(MoveToTileCoroutine(targetTile, snapPoint));
    }

    void MoveFromTileToTile(TileInstance targetTile, int targetSnapPoint)
    {
        StopAllCoroutines();
        ClearNonTargetHighlights(targetTile);
        StartCoroutine(MoveToTileCoroutine(targetTile, targetSnapPoint));
    }

    void ClearNonTargetHighlights(TileInstance targetTile)
    {
        var renderersToKeep = new List<Renderer>();
        var renderer = targetTile?.GetComponentInChildren<MeshRenderer>();
        if(renderer != null) renderersToKeep.Add(renderer);

        var materialsToRestore = originalMaterials.Where(pair => !renderersToKeep.Contains(pair.Key)).ToList();
        
        foreach (var pair in materialsToRestore)
        {
            if(pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
                originalMaterials.Remove(pair.Key);

                var tile = pair.Key.GetComponentInParent<TileInstance>();
                if(tile != null)
                {
                    StartCoroutine(LiftTileSmooth(tile, false));
                    var clicker = tile.GetComponent<SimpleTileClickHandler>();
                    if(clicker != null) DestroyImmediate(clicker);
                    highlightedTiles.Remove(tile.gameObject);
                }
            }
        }
    }

IEnumerator MoveToTileCoroutine(TileInstance targetTile, int snapPoint)
{
    Vector3 startPos = transform.position;
    Quaternion startRot = transform.rotation;
    
    // The temporary state change is no longer needed and has been removed.
    // We now pass the targetTile directly to the rotation method.
    Quaternion targetRot = GetSnapPointRotation(targetTile, snapPoint);

    Vector3 snapPosition = targetTile.snapPoints[snapPoint].position;
    Vector3 tileCenter = targetTile.transform.position;
    Vector3 direction = (snapPosition - tileCenter).normalized;
    Vector3 targetPos = snapPosition - direction * snapOffset;
    
    float elapsed = 0f;
    while (elapsed < moveSpeed)
    {
        elapsed += Time.deltaTime;
        float easeProgress = tileLiftCurve.Evaluate(elapsed / moveSpeed);
        transform.position = Vector3.Lerp(startPos, targetPos, easeProgress);
        transform.rotation = Quaternion.Slerp(startRot, targetRot, easeProgress);
        yield return null;
    }
    
    transform.position = targetPos;
    transform.rotation = targetRot;
    
    PlaceOnTile(targetTile, snapPoint);
    currentMovementPoints--;
    
    RefreshMovementOptions();
}
    
    IEnumerator MoveToBankCoroutine(Transform targetSpawn)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = targetSpawn.position;
        Quaternion targetRot = targetSpawn.rotation;
        float elapsed = 0f;
        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            float easeProgress = tileLiftCurve.Evaluate(elapsed / moveSpeed);
            transform.position = Vector3.Lerp(startPos, targetPos, easeProgress);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, easeProgress);
            yield return null;
        }
        SetStateForBank(targetSpawn);
        currentMovementPoints--;
        RefreshMovementOptions();
    }
    
    void CompleteMovementTurn()
    {
        isMoving = false;
        isBobbing = false;
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
    }

    public void EndMovementTurn()
    {
        if(isSelected) DeselectBoat();
        currentMovementPoints = 0;
    }

    public void ResetStateAfterEjection()
{
    // Force the boat out of any selection or animation state.
    isSelected = false;
    isBobbing = false;
    StopAllCoroutines();
    
    // Ensure the boat is visually lowered to its base position,
    // as if it were never selected.
    Vector3 currentPos = transform.position;
    currentPos.y = isAtBank && bankPosition != null ? bankPosition.position.y : 0f;
    transform.position = currentPos;
    
    // Clear any leftover visual artifacts immediately.
    ClearHighlights();
}

    public void ResetMovementPoints()
    {
        // In Puzzle Mode, turns do not reset. Ignore this call.
        if (gridManager != null && gridManager.isPuzzleMode)
        {
        Debug.Log("Cannot reset movement points in Puzzle Mode.");
        return;

        }


        currentMovementPoints = maxMovementPoints;
        if (isSelected)
        {
            DeselectBoat();
            SelectBoat();
        }
    }
    
Quaternion GetSnapPointRotation(TileInstance tile, int snapPointIndex)
{
    // The only change is using the 'tile' parameter instead of 'currentTile'
    if (tile != null && snapPointIndex >= 0 && snapPointIndex < tile.snapPoints.Length)
    {
        Vector3 snapPos = tile.snapPoints[snapPointIndex].position;
        Vector3 tileCenter = tile.transform.position;
        Vector3 directionToCenter = (tileCenter - snapPos).normalized;
        if (directionToCenter != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(directionToCenter);
            Vector3 euler = lookRotation.eulerAngles;
            float roundedY = Mathf.Round(euler.y / 90f) * 90f;
            return Quaternion.Euler(0f, roundedY, 0f);
        }
    }
    return Quaternion.identity;

    }


    
public void PlaceOnTile(TileInstance tile, int snapPointIndex)
{
    if (tile == null || snapPointIndex < 0 || snapPointIndex >= tile.snapPoints.Length) return;
    currentTile = tile;
    currentSnapPoint = snapPointIndex;
    isAtBank = false;
    bankPosition = null;
    
    Vector3 snapPosition = tile.snapPoints[snapPointIndex].position;
    Vector3 tileCenter = tile.transform.position;
    Vector3 direction = (snapPosition - tileCenter).normalized;
    transform.position = snapPosition - direction * snapOffset;
    
    // Update the call to pass the correct tile.
    transform.rotation = GetSnapPointRotation(tile, snapPointIndex);
}

    int DetermineSnapPointFromClick(TileInstance tile)
    {
        List<int> validSnapPoints = FindClosestSnapPointsToBank(tile);
        if (validSnapPoints.Count == 0) return 2;
        if (validSnapPoints.Count == 1) return validSnapPoints[0];
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 clickWorldPos = hit.point;
            float dist0 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[0]].position);
            float dist1 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[1]].position);
            return dist0 < dist1 ? validSnapPoints[0] : validSnapPoints[1];
        }
        return validSnapPoints[0];
    }
    
    List<int> FindClosestSnapPointsToBank(TileInstance tile)
    {
        List<int> closestPoints = new List<int>();
        if(bankPosition == null) return closestPoints;
        Vector3 tileCenter = tile.transform.position;
        Vector3 bankDirection = bankPosition.name.Contains("Bottom") ? Vector3.back : Vector3.forward;
        var snapDots = new List<(int, float)>();
        for (int i = 0; i < tile.snapPoints.Length; i++)
        {
            if (tile.snapPoints[i] != null)
            {
                Vector3 snapDirection = (tile.snapPoints[i].position - tileCenter).normalized;
                snapDots.Add((i, Vector3.Dot(snapDirection, bankDirection)));
            }
        }
        snapDots.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (snapDots.Count > 0) closestPoints.Add(snapDots[0].Item1);
        if (snapDots.Count > 1) closestPoints.Add(snapDots[1].Item1);
        return closestPoints;
    }
    
    void HighlightTile(TileInstance tile)
    {
        var renderer = tile.GetComponentInChildren<MeshRenderer>();
        if (renderer == null || originalMaterials.ContainsKey(renderer)) return;

        originalMaterials[renderer] = renderer.sharedMaterial;
        renderer.material.color = selectedColor;
        
        if (!highlightedTiles.Contains(tile.gameObject))
        {
             highlightedTiles.Add(tile.gameObject);
        }

        StartCoroutine(LiftTileSmooth(tile, true));
        var clickHandler = tile.gameObject.AddComponent<SimpleTileClickHandler>();
        clickHandler.targetBoat = this;
        clickHandler.targetTile = tile;
    }
    
    IEnumerator LiftTileSmooth(TileInstance tile, bool lift)
    {
        if (tile == null) yield break;
        Vector3 startPos = tile.transform.position;
        Vector3 targetPos = new Vector3(startPos.x, lift ? hoverHeight : 0f, startPos.z);
        
        float elapsed = 0f;
        while (elapsed < tileLiftDuration)
        {
            if (tile == null) yield break;
            elapsed += Time.deltaTime;
            tile.transform.position = Vector3.Lerp(startPos, targetPos, tileLiftCurve.Evaluate(elapsed / tileLiftDuration));
            yield return null;
        }
        if (tile != null) tile.transform.position = targetPos;
    }
    
    void OnDrawGizmosSelected()
    {
        if (currentTile != null && currentSnapPoint >= 0 && currentSnapPoint < currentTile.snapPoints.Length && currentTile.snapPoints[currentSnapPoint] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTile.snapPoints[currentSnapPoint].position);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }
}

public class SimpleTileClickHandler : MonoBehaviour, IPointerClickHandler
{
    public BoatController targetBoat;
    public TileInstance targetTile;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetBoat != null && targetTile != null)
        {
            targetBoat.OnTileClicked(targetTile);
        }
    }




}