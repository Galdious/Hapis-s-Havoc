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
    public float bobAmount = 0.1f;
    public float bobSpeed = 2f;
    
    [Header("Tile Animation")]
    public float tileLiftDuration = 0.4f;
    public float tileLiftDelay = 0.2f;
    public AnimationCurve tileLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Movement System")]
    public int maxMovementPoints = 3;
    private int currentMovementPoints = 3;

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
    private bool isSelected = false;
    private bool isMoving = false;
    
    private Vector3 originalBoatPosition;
    private bool isBobbing = false;
    
    private GridManager gridManager;
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
        gridManager = FindFirstObjectByType<GridManager>();
        riverBankManager = FindFirstObjectByType<RiverBankManager>();
        if (gridManager == null) Debug.LogError("[BoatController] GridManager not found!");
        if (riverBankManager == null) Debug.LogError("[BoatController] RiverBankManager not found!");
    }
    
    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame) EndMovementTurn();
        if (Keyboard.current.rKey.wasPressedThisFrame) ResetMovementPoints();
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
        if (!isSelected) SelectBoat();
        else DeselectBoat();
    }
    
    void SelectBoat()
    {
        isSelected = true;
        if (currentMovementPoints <= 0) currentMovementPoints = maxMovementPoints;

        if (!isAtBank && currentTile != null)
        {
           HighlightTile(currentTile);
        }
        
        StartCoroutine(LiftAndBobBoat(true));
        FindValidMoves();
        StartCoroutine(HighlightValidMovesWithDelay());
    }
    
    void DeselectBoat()
    {
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
        targetPos.y = lift ? baseY + hoverHeight : baseY;
        
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


void FindMoveAtEndOfChain(TileInstance startTile, int exitSnap, bool isReverseMove)
{
    TileInstance currentSearchTile = startTile;
    int currentExitSnap = exitSnap;
    List<TileInstance> crossedReversedTiles = new List<TileInstance>();

    for (int i = 0; i < gridManager.cols + 2; i++) // Safety break
    {
        // --- CHANGE #1: Use the horizontal-only search ---
        TileInstance neighbor = FindConnectedTile_HorizontalOnly(currentSearchTile, currentExitSnap);

        if (neighbor == null)
        {
            var (x, y) = GetTileCoordinates(currentSearchTile);
            if (y == 0) HighlightBankForDocking(RiverBankManager.BankSide.Bottom);
            else if (y == gridManager.rows - 1) HighlightBankForDocking(RiverBankManager.BankSide.Top);
            return;
        }

        if (neighbor.IsReversed)
        {
            if (gridManager.reversedTileRule == GridManager.ReversedTileRule.Blocker) return;

            crossedReversedTiles.Add(neighbor);
            // --- CHANGE #2: Use the horizontal-only search here as well ---
            int entrySnap = FindConnectedSnapPoint_HorizontalOnly(currentSearchTile, currentExitSnap, neighbor);
            currentExitSnap = GetOppositeSnapPoint(entrySnap);
            currentSearchTile = neighbor;
        }
        else
        {
            // --- CHANGE #3: And here, to find the final landing snap ---
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
        isSelected = false;

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
        isSelected = false;
        StopAllCoroutines();
        
        Transform targetSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        StartCoroutine(MoveToBankCoroutine(targetSpawn));
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
        for (int col = 0; col < gridManager.cols; col++)
        {
            var tile = gridManager.GetTileAt(col, entryRow);
            if (tile != null) validMoves.Add(tile);
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
    
    CompleteMovementTurn();
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
        SetAtBank(targetSpawn);
        currentMovementPoints--;
        CompleteMovementTurn();
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