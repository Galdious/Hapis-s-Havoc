/*
 *  BoatController.cs
 *  ---------------------------------------------------------------
 *  FINAL CORRECTED VERSION
 *
 *  - RESTORED the user's original, working connection-finding logic (FindConnectedTile/FindConnectedSnapPoint)
 *    as it correctly handles rotation by using world-space distance checks.
 *  - RE-IMPLEMENTED FindRiverPathMoves to use this correct logic within a new stateless framework.
 *  - PRESERVED all of the user's animation, rotation (GetSnapPointRotation), and click-handling logic.
 *  - RETAINED the MeshRenderer fix to prevent highlighting errors.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    
    [Header("Visual Feedback")]
    public Material validMoveMaterial;
    public Color selectedColor = Color.yellow;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Current state
    private TileInstance currentTile;
    private int currentSnapPoint = -1;
    private Transform bankPosition;
    private bool isAtBank = true;
    private bool isSelected = false;
    private bool isMoving = false;
    
    // Animation
    private Vector3 originalBoatPosition;
    private bool isBobbing = false;
    
    // Movement system
    private GridManager gridManager;
    private List<TileInstance> validMoves = new List<TileInstance>();
    private List<GameObject> highlightedTiles = new List<GameObject>();
    private Dictionary<TileInstance, int> tileToSnapPoint = new Dictionary<TileInstance, int>();
    private Dictionary<TileInstance, int> tileToReverseSnapPoint = new Dictionary<TileInstance, int>();
    private Dictionary<TileInstance, Material> originalMaterials = new Dictionary<TileInstance, Material>();
    
    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) Debug.LogError("[BoatController] GridManager not found!");
    }
    
    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame) EndMovementTurn();
        if (Keyboard.current.rKey.wasPressedThisFrame) ResetMovementPoints();
        if (showDebugInfo && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log($"[BoatController] Current state - Selected: {isSelected}, At Bank: {isAtBank}, Movement Points: {currentMovementPoints}, Current Tile: {currentTile?.name}, Snap Point: {currentSnapPoint}");
        }
    }
    
    // All methods from here down to FindValidMoves are from your original, working script.
    #region Original Selection & Animation Logic

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
        StartCoroutine(LiftAndBobBoat(true));
        FindValidMoves();
        StartCoroutine(HighlightValidMovesWithDelay());
        if (showDebugInfo) Debug.Log($"[BoatController] Boat selected. Found {validMoves.Count} valid moves. Movement points: {currentMovementPoints}");
    }
    
    void DeselectBoat()
    {
        isSelected = false;
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
        if (showDebugInfo) Debug.Log("[BoatController] Boat deselected.");
    }
    
    IEnumerator LiftAndBobBoat(bool lift)
    {
        float baseY = isAtBank && bankPosition != null ? bankPosition.position.y : 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos;
        targetPos.y = lift ? baseY + hoverHeight : baseY;
        
        float liftDuration = 0.3f;
        float elapsed = 0f;
        AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            float progress = liftCurve.Evaluate(elapsed / liftDuration);
            transform.position = Vector3.Lerp(startPos, targetPos, progress);
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
        foreach (TileInstance tile in validMoves)
        {
            if (tile != null) HighlightTile(tile);
        }
    }

    #endregion

    // ┌───────────────────────────────────────────────────────────────────┐
    // │ │
    // │ !!! --- CORE LOGIC REIMPLEMENTATION --- !!! │
    // │ │
    // └───────────────────────────────────────────────────────────────────┘

    void FindValidMoves()
    {
        validMoves.Clear();
        tileToSnapPoint.Clear();
        tileToReverseSnapPoint.Clear();
        
        if (isAtBank) FindBankEntryMoves();
        else if (currentTile != null) FindRiverPathMoves();
    }
    
    /// <summary>
    /// REIMPLEMENTED: Uses your original, correct connection-finding logic within a stateless framework.
    /// </summary>
    void FindRiverPathMoves()
    {
        if (currentTile == null || currentSnapPoint < 0) return;
        if (showDebugInfo) Debug.Log($"--- Finding moves from {currentTile.name} at snap {currentSnapPoint} ---");

        // 1. Find PATH moves by checking every connection on the current tile.
        foreach (var connection in currentTile.connections)
        {
            int exitSnapPoint = -1;
            if (connection.from == currentSnapPoint) exitSnapPoint = connection.to;
            else if (connection.to == currentSnapPoint) exitSnapPoint = connection.from;
            
            if(exitSnapPoint != -1)
            {
                TileInstance neighbor = FindConnectedTile(currentTile, exitSnapPoint);
                if(neighbor != null)
                {
                    int entrySnap = FindConnectedSnapPoint(currentTile, exitSnapPoint, neighbor);
                    if(entrySnap != -1)
                    {
                        if (!validMoves.Contains(neighbor)) validMoves.Add(neighbor);
                        tileToSnapPoint[neighbor] = entrySnap; // Assign as a "Path" move
                        if(showDebugInfo) Debug.Log($"Found PATH move: {currentTile.name}[{exitSnapPoint}] -> {neighbor.name}[{entrySnap}]");
                    }
                }
            }
        }
        
        // 2. Find the REVERSE move by finding the tile connected to the current snap point.
        TileInstance reverseNeighbor = FindConnectedTile(currentTile, currentSnapPoint);
        if(reverseNeighbor != null)
        {
            int reverseEntrySnap = FindConnectedSnapPoint(currentTile, currentSnapPoint, reverseNeighbor);
            if(reverseEntrySnap != -1)
            {
                if (!validMoves.Contains(reverseNeighbor)) validMoves.Add(reverseNeighbor);
                tileToReverseSnapPoint[reverseNeighbor] = reverseEntrySnap; // Assign as a "Reverse" move
                if(showDebugInfo) Debug.Log($"Found REVERSE move: {currentTile.name}[{currentSnapPoint}] -> {reverseNeighbor.name}[{reverseEntrySnap}]");
            }
        }
    }

    /// <summary>
    /// RESTORED: Your original, working method for finding a connected tile via distance check.
    /// This correctly handles tile rotations implicitly.
    /// </summary>
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

    /// <summary>
    /// RESTORED: Your original, working method for finding the corresponding snap point on a connected tile.
    /// </summary>
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

    // From here, the rest of the script is your original code with only the MeshRenderer fix.
    #region Original Bank, Highlighting, and Movement Logic

    void FindBankEntryMoves()
    {
        int entryRow = DetermineEntryRow();
        for (int col = 0; col < gridManager.cols; col++)
        {
            TileInstance tile = gridManager.GetTileAt(col, entryRow);
            if (tile != null) validMoves.Add(tile);
        }
        if (showDebugInfo) Debug.Log($"[BoatController] Found {validMoves.Count} entry tiles in row {entryRow}");
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
    
    void HighlightTile(TileInstance tile)
    {
        MeshRenderer renderer = tile.GetComponentInChildren<MeshRenderer>();
        if (renderer == null)
        {
            Debug.LogError($"[BoatController] No MeshRenderer found on {tile.name} or its children!");
            return;
        }
        
        if (!originalMaterials.ContainsKey(tile)) originalMaterials[tile] = renderer.material;
        
        renderer.material.color = Color.magenta;
        StartCoroutine(LiftTileSmooth(tile, true));
        highlightedTiles.Add(tile.gameObject);
        
        SimpleTileClickHandler clickHandler = tile.gameObject.AddComponent<SimpleTileClickHandler>();
        clickHandler.targetBoat = this;
        clickHandler.targetTile = tile;
    }
    
    IEnumerator LiftTileSmooth(TileInstance tile, bool lift)
    {
        if (tile == null) yield break;
        
        Vector3 startPos = tile.transform.position;
        Vector3 targetPos = startPos;
        targetPos.y = lift ? hoverHeight : 0f;
        
        float elapsed = 0f;
        while (elapsed < tileLiftDuration)
        {
            if (tile == null) yield break;
            elapsed += Time.deltaTime;
            float curveValue = tileLiftCurve.Evaluate(elapsed / tileLiftDuration);
            tile.transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            yield return null;
        }
        if (tile != null) tile.transform.position = targetPos;
    }
    
    void ClearHighlights()
    {
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                TileInstance tile = tileGO.GetComponent<TileInstance>();
                if (tile != null)
                {
                    if (originalMaterials.ContainsKey(tile))
                    {
                        MeshRenderer renderer = tile.GetComponentInChildren<MeshRenderer>();
                        if (renderer != null) renderer.material = originalMaterials[tile];
                    }
                    StartCoroutine(LiftTileSmooth(tile, false));
                }
                SimpleTileClickHandler clickHandler = tileGO.GetComponent<SimpleTileClickHandler>();
                if (clickHandler != null) DestroyImmediate(clickHandler);
            }
        }
        
        if (!isAtBank && currentTile != null)
        {
            StartCoroutine(LiftTileSmooth(currentTile, false));
        }
        
        highlightedTiles.Clear();
        originalMaterials.Clear();
    }
    
    public void OnTileClicked(TileInstance clickedTile)
    {
        if (isMoving || !isSelected || !validMoves.Contains(clickedTile) || currentMovementPoints <= 0) return;
        
        if (isAtBank)
        {
            int snapPoint = DetermineSnapPointFromClick(clickedTile);
            MoveFromBankToTile(clickedTile, snapPoint);
        }
        else
        {
            int targetSnapPoint = DetermineRiverSnapPoint(clickedTile);
            if(targetSnapPoint != -1)
            {
                MoveFromTileToTile(clickedTile, targetSnapPoint);
            }
            else
            {
                Debug.LogWarning("[BoatController] No pre-calculated snap point found for river movement!");
            }
        }
    }
    
    void MoveFromTileToTile(TileInstance targetTile, int targetSnapPoint)
    {
        if (showDebugInfo) Debug.Log($"[BoatController] Moving from tile {currentTile.name}[{currentSnapPoint}] to tile {targetTile.name}[{targetSnapPoint}]");
        
        StopAllCoroutines();
        isBobbing = false;
        
        ClearNonTargetHighlights(targetTile);
        
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, targetSnapPoint));
    }
    
    void ClearNonTargetHighlights(TileInstance targetTile)
    {
        var tilesToRemove = new List<GameObject>();
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                TileInstance tile = tileGO.GetComponent<TileInstance>();
                if (tile != null && tile != targetTile && tile != currentTile)
                {
                    tilesToRemove.Add(tileGO);
                }
            }
        }

        foreach(var tileGO in tilesToRemove)
        {
            highlightedTiles.Remove(tileGO);
            TileInstance tile = tileGO.GetComponent<TileInstance>();
            if (tile != null)
            {
                if (originalMaterials.ContainsKey(tile))
                {
                    MeshRenderer renderer = tile.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null) renderer.material = originalMaterials[tile];
                    originalMaterials.Remove(tile);
                }
                StartCoroutine(LiftTileSmooth(tile, false));
                SimpleTileClickHandler clickHandler = tileGO.GetComponent<SimpleTileClickHandler>();
                if (clickHandler != null) DestroyImmediate(clickHandler);
            }
        }
    }
    
    int DetermineRiverSnapPoint(TileInstance tile)
    {
        bool isPathMoveAvailable = tileToSnapPoint.ContainsKey(tile);
        bool isReverseMoveAvailable = tileToReverseSnapPoint.ContainsKey(tile);

        if (isPathMoveAvailable && isReverseMoveAvailable)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 localClickPos = tile.transform.InverseTransformPoint(hit.point);
                bool isBoatOnLeftSide = currentSnapPoint == 0 || currentSnapPoint == 2 || currentSnapPoint == 5;
                bool isClickOnLeftSide = localClickPos.x < 0;

                if ((isBoatOnLeftSide && isClickOnLeftSide) || (!isBoatOnLeftSide && !isClickOnLeftSide))
                {
                    if (showDebugInfo) Debug.Log("Ambiguous move resolved as: REVERSE");
                    return tileToReverseSnapPoint[tile];
                }
                else
                {
                    if (showDebugInfo) Debug.Log("Ambiguous move resolved as: CONTINUE PATH");
                    return tileToSnapPoint[tile];
                }
            }
        }
        else if (isPathMoveAvailable) return tileToSnapPoint[tile];
        else if (isReverseMoveAvailable) return tileToReverseSnapPoint[tile];

        return -1;
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
        
        var snapDots = new List<System.Tuple<int, float>>();
        for (int i = 0; i < tile.snapPoints.Length; i++)
        {
            if (tile.snapPoints[i] != null)
            {
                Vector3 snapDirection = (tile.snapPoints[i].position - tileCenter).normalized;
                float dot = Vector3.Dot(snapDirection, bankDirection);
                snapDots.Add(new System.Tuple<int, float>(i, dot));
            }
        }
        
        snapDots.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (snapDots.Count > 0) closestPoints.Add(snapDots[0].Item1);
        if (snapDots.Count > 1) closestPoints.Add(snapDots[1].Item1);
        
        return closestPoints;
    }
    
    void MoveFromBankToTile(TileInstance targetTile, int snapPoint)
    {
        if (showDebugInfo) Debug.Log($"[BoatController] Moving from bank to tile {targetTile.name} at snap point {snapPoint}");
        StopAllCoroutines();
        isBobbing = false;
        StartCoroutine(LowerNonTargetTiles(targetTile));
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, snapPoint));
    }
    
    IEnumerator LowerNonTargetTiles(TileInstance targetTile)
    {
        var tilesToRemove = new List<GameObject>();
        foreach (var tileGO in highlightedTiles)
        {
            if (tileGO != null && (targetTile == null || tileGO != targetTile.gameObject))
            {
                tilesToRemove.Add(tileGO);
            }
        }

        foreach (var tileGO in tilesToRemove)
        {
            highlightedTiles.Remove(tileGO);
            TileInstance tile = tileGO.GetComponent<TileInstance>();
            if (tile != null)
            {
                if (originalMaterials.ContainsKey(tile))
                {
                    var renderer = tile.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null) renderer.material = originalMaterials[tile];
                    originalMaterials.Remove(tile);
                }
                StartCoroutine(LiftTileSmooth(tile, false));
                var clickHandler = tile.GetComponent<SimpleTileClickHandler>();
                if (clickHandler != null) DestroyImmediate(clickHandler);
            }
        }
        yield return null;
    }
    
    IEnumerator MoveToTileCoroutine(TileInstance targetTile, int snapPoint)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        TileInstance previousTile = currentTile;
        currentTile = targetTile;
        Quaternion targetRot = GetSnapPointRotation(snapPoint);
        currentTile = previousTile;

        Vector3 snapPosition = targetTile.snapPoints[snapPoint].position;
        Vector3 tileCenter = targetTile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        Vector3 targetPos = snapPosition - direction * snapOffset;
        
        float elapsed = 0f;
        AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            float easeProgress = easeCurve.Evaluate(elapsed / moveSpeed);
            transform.position = Vector3.Lerp(startPos, targetPos, easeProgress);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, easeProgress);
            yield return null;
        }
        
        transform.position = targetPos;
        transform.rotation = targetRot;
        
        PlaceOnTile(targetTile, snapPoint);
        currentMovementPoints--;
        
        if (currentMovementPoints > 0)
        {
            FindValidMoves();
            if (validMoves.Count > 0)
            {
                ClearHighlights();
                StartCoroutine(HighlightValidMovesWithDelay());
                isSelected = true;
                StartCoroutine(LiftAndBobBoat(true));
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                CompleteMovementTurn();
            }
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            CompleteMovementTurn();
        }
        isMoving = false;
    }
    
    void CompleteMovementTurn()
    {
        isSelected = false;
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
        currentMovementPoints = 0;
        if (showDebugInfo) Debug.Log($"[BoatController] Movement turn completed.");
    }
    
    public void EndMovementTurn()
    {
        currentMovementPoints = 0;
        CompleteMovementTurn();
    }
    
    public void ResetMovementPoints()
    {
        currentMovementPoints = maxMovementPoints;
        if (showDebugInfo) Debug.Log($"[BoatController] Movement points reset to {maxMovementPoints}.");
        if (isSelected)
        {
            ClearHighlights();
            FindValidMoves();
            StartCoroutine(HighlightValidMovesWithDelay());
        }
    }
    
    Quaternion GetSnapPointRotation(int snapPointIndex)
    {
        if (currentTile != null && snapPointIndex >= 0 && snapPointIndex < currentTile.snapPoints.Length)
        {
            Vector3 snapPos = currentTile.snapPoints[snapPointIndex].position;
            Vector3 tileCenter = currentTile.transform.position;
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
    
    public void SetAtBank(Transform bankSpawnPoint)
    {
        bankPosition = bankSpawnPoint;
        isAtBank = true;
        currentTile = null;
        currentSnapPoint = -1;
        transform.position = bankSpawnPoint.position;
        transform.rotation = bankSpawnPoint.rotation;
    }
    
    public void PlaceOnTile(TileInstance tile, int snapPointIndex)
    {
        if (tile == null || snapPointIndex < 0 || snapPointIndex >= tile.snapPoints.Length)
        {
            Debug.LogError("[BoatController] Invalid tile or snap point!");
            return;
        }
        
        currentTile = tile;
        currentSnapPoint = snapPointIndex;
        isAtBank = false;
        bankPosition = null;
        
        Vector3 snapPosition = tile.snapPoints[snapPointIndex].position;
        Vector3 tileCenter = tile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        Vector3 boatPosition = snapPosition - direction * snapOffset;
        transform.position = boatPosition;
        
        Quaternion targetRotation = GetSnapPointRotation(snapPointIndex);
        transform.rotation = targetRotation;
        
        if (showDebugInfo) Debug.Log($"[BoatController] Placed on tile {tile.name} at snap point {snapPointIndex} with rotation {targetRotation.eulerAngles}");
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

    #endregion
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