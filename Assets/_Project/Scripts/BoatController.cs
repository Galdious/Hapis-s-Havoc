/*
 *  BoatController.cs
 *  ---------------------------------------------------------------
 *  Controls river boat movement along tile paths.
 *  Boats snap to tile edges and follow river connections.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BoatController : MonoBehaviour, IPointerClickHandler
{
    [Header("Boat Settings")]
    public float snapOffset = 0.15f;     // how far into tile from snap point
    public float moveSpeed = 1f;         // movement animation duration (in seconds)
    public float hoverHeight = 0.5f;     // how high boat lifts when selected
    public float bobAmount = 0.1f;       // how much boat bobs while selected
    public float bobSpeed = 2f;          // how fast boat bobs
    
    [Header("Tile Animation")]
    public float tileLiftDuration = 0.4f;     // how long tiles take to lift/lower
    public float tileLiftDelay = 0.2f;        // delay before tiles start lifting after boat selection
    public AnimationCurve tileLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Movement System")]
    public int maxMovementPoints = 3;         // how many moves per turn
    private int currentMovementPoints = 3;    // remaining moves
    
    [Header("Visual Feedback")]
    public Material validMoveMaterial;    // material for valid move tiles
    public Color selectedColor = Color.yellow;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Current state
    private TileInstance currentTile;     // tile boat is currently on
    private int currentSnapPoint = -1;    // which snap point (0-5)
    private Transform bankPosition;       // bank spawn point (when not on river)
    private bool isAtBank = true;         // whether boat is at bank or on river
    private bool isSelected = false;      // boat selection state
    private bool isMoving = false;        // prevent clicks during movement
    
    // Animation
    private Vector3 originalBoatPosition;
    private bool isBobbing = false;
    
    // Movement system
    private GridManager gridManager;
    private List<TileInstance> validMoves = new List<TileInstance>();
    private List<GameObject> highlightedTiles = new List<GameObject>();
    private Dictionary<TileInstance, int> tileToSnapPoint = new Dictionary<TileInstance, int>(); // Store which snap point to use for each valid move
    private Dictionary<TileInstance, int> tileToReverseSnapPoint = new Dictionary<TileInstance, int>(); // Store reverse snap point for each valid move
    private int lastSnapPointUsed = -1; // Remember which snap point we came from on previous tile
    
    // Original materials for cleanup
    private Dictionary<TileInstance, Material> originalMaterials = new Dictionary<TileInstance, Material>();
    
    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[BoatController] GridManager not found!");
        }
    }
    
    void Update()
    {
        // Testing controls
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            EndMovementTurn();
        }
        
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetMovementPoints();
        }
        
        if (showDebugInfo && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Debug.Log($"[BoatController] Current state - Selected: {isSelected}, At Bank: {isAtBank}, Movement Points: {currentMovementPoints}, Current Tile: {currentTile?.name}, Snap Point: {currentSnapPoint}");
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isMoving) return; // Prevent clicks during movement
        
        if (!isSelected)
        {
            SelectBoat();
        }
        else
        {
            DeselectBoat();
        }
    }
    
    void SelectBoat()
    {
        isSelected = true;
        
        // Only reset movement points if this is a fresh selection (not continuing from previous move)
        if (currentMovementPoints <= 0)
        {
            currentMovementPoints = maxMovementPoints;
        }
        
        // Start smooth lift and bob animation
        StartCoroutine(LiftAndBobBoat(true));
        
        // Find and highlight valid moves with delay
        FindValidMoves();
        StartCoroutine(HighlightValidMovesWithDelay());
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Boat selected. Found {validMoves.Count} valid moves. Movement points: {currentMovementPoints}");
        }
    }
    
    void DeselectBoat()
    {
        isSelected = false;
        
        // Stop bobbing and lower boat
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        
        // Clear highlights
        ClearHighlights();
        
        if (showDebugInfo)
        {
            Debug.Log("[BoatController] Boat deselected.");
        }
    }
    
    IEnumerator LiftAndBobBoat(bool lift)
    {
        float baseY = isAtBank ? bankPosition.position.y : 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos;
        targetPos.y = lift ? baseY + hoverHeight : baseY;
        
        // Smooth lift animation
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
        
        // Start bobbing if lifting
        if (lift)
        {
            isBobbing = true;
            StartCoroutine(BobBoat());
        }
        else
        {
            isBobbing = false;
        }
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
        // Wait for boat lift to start, then add delay
        yield return new WaitForSeconds(tileLiftDelay);
        
        // Highlight tiles with smooth animation
        foreach (TileInstance tile in validMoves)
        {
            if (tile != null)
            {
                HighlightTile(tile);
            }
        }
    }
    
    void FindValidMoves()
    {
        validMoves.Clear();
        tileToSnapPoint.Clear();
        tileToReverseSnapPoint.Clear();
        
        if (isAtBank)
        {
            // If boat is at bank, show entire first row as valid entry points
            FindBankEntryMoves();
        }
        else if (currentTile != null)
        {
            // If boat is on river, find connected tiles via river paths
            FindRiverPathMoves();
        }
    }
    
    void FindBankEntryMoves()
    {
        // Determine which row is closest to our bank
        int entryRow = DetermineEntryRow();
        
        // Add all tiles in the entry row as valid moves
        for (int col = 0; col < gridManager.cols; col++)
        {
            TileInstance tile = gridManager.GetTileAt(col, entryRow);
            if (tile != null)
            {
                validMoves.Add(tile);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Found {validMoves.Count} entry tiles in row {entryRow}");
        }
    }
    
    int DetermineEntryRow()
    {
        // Determine which row is closest to the bank we're spawned from
        if (bankPosition != null)
        {
            if (bankPosition.name.Contains("Top"))
            {
                return gridManager.rows - 1; // Last row (closest to top bank)
            }
            else if (bankPosition.name.Contains("Bottom"))
            {
                return 0; // First row (closest to bottom bank)
            }
        }
        
        // Default to first row
        return 0;
    }
    
    void FindRiverPathMoves()
    {
        if (currentTile == null || currentSnapPoint < 0) return;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Finding moves from {currentTile.name} at snap point {currentSnapPoint}");
        }
        
        // Find ALL connections that include our current snap point
        foreach (var connection in currentTile.connections)
        {
            if (connection.from == currentSnapPoint || connection.to == currentSnapPoint)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"[BoatController] Checking connection {connection.from}-{connection.to} (includes our snap {currentSnapPoint})");
                }
                
                // This connection includes our current snap point
                // Check BOTH ends of this connection for connected tiles
                
                // Check the 'from' end
                TileInstance tile1 = FindConnectedTile(currentTile, connection.from);
                if (tile1 != null && !validMoves.Contains(tile1))
                {
                    int entrySnapPoint1 = FindConnectedSnapPoint(currentTile, connection.from, tile1);
                    if (entrySnapPoint1 >= 0)
                    {
                        validMoves.Add(tile1);
                        tileToSnapPoint[tile1] = entrySnapPoint1; // Continue path snap point
                        
                        // Calculate reverse snap point (if we came from this tile)
                        if (lastSnapPointUsed >= 0)
                        {
                            tileToReverseSnapPoint[tile1] = lastSnapPointUsed;
                        }
                        else
                        {
                            tileToReverseSnapPoint[tile1] = entrySnapPoint1; // Fallback
                        }
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[BoatController] Found connection via 'from' end: {currentTile.name}[{connection.from}] -> {tile1.name}[{entrySnapPoint1}] (reverse: {tileToReverseSnapPoint[tile1]})");
                        }
                    }
                    else if (showDebugInfo)
                    {
                        Debug.Log($"[BoatController] No valid entry snap point found for {tile1.name} via 'from' end");
                    }
                }
                else if (showDebugInfo && tile1 != null)
                {
                    Debug.Log($"[BoatController] Tile {tile1.name} already in valid moves (via 'from' end)");
                }
                
                // Check the 'to' end
                TileInstance tile2 = FindConnectedTile(currentTile, connection.to);
                if (tile2 != null && !validMoves.Contains(tile2))
                {
                    int entrySnapPoint2 = FindConnectedSnapPoint(currentTile, connection.to, tile2);
                    if (entrySnapPoint2 >= 0)
                    {
                        validMoves.Add(tile2);
                        tileToSnapPoint[tile2] = entrySnapPoint2; // Continue path snap point
                        
                        // Calculate reverse snap point (if we came from this tile)
                        if (lastSnapPointUsed >= 0)
                        {
                            tileToReverseSnapPoint[tile2] = lastSnapPointUsed;
                        }
                        else
                        {
                            tileToReverseSnapPoint[tile2] = entrySnapPoint2; // Fallback
                        }
                        
                        if (showDebugInfo)
                        {
                            Debug.Log($"[BoatController] Found connection via 'to' end: {currentTile.name}[{connection.to}] -> {tile2.name}[{entrySnapPoint2}] (reverse: {tileToReverseSnapPoint[tile2]})");
                        }
                    }
                    else if (showDebugInfo)
                    {
                        Debug.Log($"[BoatController] No valid entry snap point found for {tile2.name} via 'to' end");
                    }
                }
                else if (showDebugInfo && tile2 != null)
                {
                    Debug.Log($"[BoatController] Tile {tile2.name} already in valid moves (via 'to' end)");
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Found {validMoves.Count} connected tiles from current snap point {currentSnapPoint}");
            foreach (var move in validMoves)
            {
                Debug.Log($"  -> {move.name} at snap {tileToSnapPoint[move]} (reverse: {tileToReverseSnapPoint[move]})");
            }
        }
    }
    
    TileInstance FindConnectedTile(TileInstance fromTile, int snapPointIndex)
    {
        // Get the world position of the snap point
        Vector3 snapPosition = fromTile.snapPoints[snapPointIndex].position;
        
        // Find the closest tile that has a snap point near this position
        float minDistance = float.MaxValue;
        TileInstance closestTile = null;
        
        // Check all tiles in the grid
        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile != fromTile)
                {
                    // Check all snap points of this tile
                    for (int i = 0; i < tile.snapPoints.Length; i++)
                    {
                        if (tile.snapPoints[i] != null)
                        {
                            float distance = Vector3.Distance(snapPosition, tile.snapPoints[i].position);
                            if (distance < 0.5f && distance < minDistance) // Close enough to be connected
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
    
    int FindConnectedSnapPoint(TileInstance fromTile, int fromSnapIndex, TileInstance toTile)
    {
        Vector3 fromSnapPos = fromTile.snapPoints[fromSnapIndex].position;
        
        // Find the snap point on toTile that's closest to fromSnapIndex
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
        
        return minDistance < 0.5f ? closestSnapPoint : -1; // Only if close enough
    }
    
    void HighlightValidMoves()
    {
        ClearHighlights(); // Clear any existing highlights
        
        foreach (TileInstance tile in validMoves)
        {
            if (tile != null)
            {
                HighlightTile(tile);
            }
        }
    }
    
    void HighlightTile(TileInstance tile)
    {
        // Look for Renderer on the tile or its children
        Renderer renderer = tile.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = tile.GetComponentInChildren<Renderer>();
        }
        
        if (renderer == null)
        {
            Debug.LogError($"[BoatController] No Renderer found on {tile.name} or its children!");
            return;
        }
        
        // Store original material
        if (!originalMaterials.ContainsKey(tile))
        {
            originalMaterials[tile] = renderer.material;
        }
        
        // Apply highlight material
        renderer.material.color = Color.magenta; // Bright for testing
        
        // Start smooth tile lift animation
        StartCoroutine(LiftTileSmooth(tile, true));
        
        highlightedTiles.Add(tile.gameObject);
        
        // Add clickable component for tile interaction
        SimpleTileClickHandler clickHandler = tile.gameObject.AddComponent<SimpleTileClickHandler>();
        clickHandler.targetBoat = this;
        clickHandler.targetTile = tile;
    }
    
    IEnumerator LiftTileSmooth(TileInstance tile, bool lift)
    {
        if (tile == null) yield break;
        
        Vector3 startPos = tile.transform.position;
        Vector3 targetPos = startPos;
        
        if (lift)
        {
            targetPos.y = startPos.y + hoverHeight;
        }
        else
        {
            targetPos.y = startPos.y - hoverHeight;
        }
        
        float elapsed = 0f;
        
        while (elapsed < tileLiftDuration)
        {
            if (tile == null) yield break; // Safety check in case tile gets destroyed
            
            elapsed += Time.deltaTime;
            float progress = elapsed / tileLiftDuration;
            float curveValue = tileLiftCurve.Evaluate(progress);
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, curveValue);
            tile.transform.position = currentPos;
            
            yield return null;
        }
        
        // Ensure final position is exact
        if (tile != null)
        {
            tile.transform.position = targetPos;
        }
    }
    
    void ClearHighlights()
    {
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                TileInstance tile = tileGO.GetComponent<TileInstance>();
                if (tile != null && originalMaterials.ContainsKey(tile))
                {
                    // Find the renderer (on tile or children)
                    Renderer renderer = tile.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        renderer = tile.GetComponentInChildren<Renderer>();
                    }
                    
                    if (renderer != null)
                    {
                        renderer.material = originalMaterials[tile];
                    }
                    
                    // Start smooth tile lower animation
                    StartCoroutine(LiftTileSmooth(tile, false));
                }
                
                // Remove clickable component
                SimpleTileClickHandler clickHandler = tileGO.GetComponent<SimpleTileClickHandler>();
                if (clickHandler != null)
                {
                    DestroyImmediate(clickHandler);
                }
            }
        }
        
        // Also lower the current tile if boat is on river
        if (!isAtBank && currentTile != null)
        {
            StartCoroutine(LiftTileSmooth(currentTile, false));
            
            if (showDebugInfo)
            {
                Debug.Log($"[BoatController] Lowering current tile: {currentTile.name}");
            }
        }
        
        highlightedTiles.Clear();
        originalMaterials.Clear();
    }
    
    // Called when a highlighted tile is clicked
    public void OnTileClicked(TileInstance clickedTile)
    {
        if (isMoving || !isSelected) return;
        
        // Check if this tile is in our valid moves
        if (!validMoves.Contains(clickedTile))
        {
            Debug.LogWarning("[BoatController] Clicked tile is not a valid move!");
            return;
        }
        
        // Check if we have movement points left
        if (currentMovementPoints <= 0)
        {
            Debug.LogWarning("[BoatController] No movement points left!");
            return;
        }
        
        if (isAtBank)
        {
            // Moving from bank to river - choose snap point based on click position
            int snapPoint = DetermineSnapPointFromClick(clickedTile);
            MoveFromBankToTile(clickedTile, snapPoint);
        }
        else
        {
            // Moving between tiles on river - use click detection for reverse vs continue
            if (tileToSnapPoint.ContainsKey(clickedTile))
            {
                int targetSnapPoint = DetermineRiverSnapPointFromClick(clickedTile);
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
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Moving from tile {currentTile.name}[{currentSnapPoint}] to tile {targetTile.name}[{targetSnapPoint}]");
        }
        
        // Stop all animations (including bobbing) before starting movement
        StopAllCoroutines();
        isBobbing = false;
        
        // Clear highlights but keep current and target tiles elevated
        ClearNonTargetHighlights(targetTile);
        
        // Move boat to tile
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, targetSnapPoint));
    }
    
    void ClearNonTargetHighlights(TileInstance targetTile)
    {
        // Clear highlights from non-target tiles but keep them elevated
        for (int i = highlightedTiles.Count - 1; i >= 0; i--)
        {
            GameObject tileGO = highlightedTiles[i];
            if (tileGO != null)
            {
                TileInstance tile = tileGO.GetComponent<TileInstance>();
                if (tile != null && tile != targetTile && tile != currentTile)
                {
                    // Restore material
                    if (originalMaterials.ContainsKey(tile))
                    {
                        Renderer renderer = tile.GetComponent<Renderer>();
                        if (renderer == null)
                        {
                            renderer = tile.GetComponentInChildren<Renderer>();
                        }
                        
                        if (renderer != null)
                        {
                            renderer.material = originalMaterials[tile];
                        }
                        
                        originalMaterials.Remove(tile);
                    }
                    
                    // IMPORTANT: Lower the tile that we're not moving to
                    StartCoroutine(LiftTileSmooth(tile, false));
                    
                    // Remove clickable component
                    SimpleTileClickHandler clickHandler = tileGO.GetComponent<SimpleTileClickHandler>();
                    if (clickHandler != null)
                    {
                        DestroyImmediate(clickHandler);
                    }
                    
                    // Remove from list
                    highlightedTiles.RemoveAt(i);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"[BoatController] Lowering non-target tile: {tile.name}");
                    }
                }
            }
        }
    }
    
    int DetermineRiverSnapPointFromClick(TileInstance tile)
    {
        // Check if this is a U-shaped tile (TileFace with connection 2-3)
        bool isUShapedTile = false;
        foreach (var connection in tile.connections)
        {
            if ((connection.from == 2 && connection.to == 3) || (connection.from == 3 && connection.to == 2))
            {
                isUShapedTile = true;
                break;
            }
        }
        
        // If NOT a U-shaped tile, use regular logic (continue path only)
        if (!isUShapedTile)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[BoatController] Not a U-shaped tile - using continue path snap point {tileToSnapPoint[tile]}");
            }
            return tileToSnapPoint[tile];
        }
        
        // U-shaped tile logic: determine click side and start side
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        
        bool clickedOnRight = false;
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 clickWorldPos = hit.point;
            
            // Determine which side of the tile was clicked (in tile's local space)
            Vector3 localClickPos = tile.transform.InverseTransformPoint(clickWorldPos);
            clickedOnRight = localClickPos.x > 0;
        }
        
        // Determine which side we originally came from based on lastSnapPointUsed
        bool cameFromRight = false;
        if (lastSnapPointUsed >= 0)
        {
            // Left side: 0, 2, 5 | Right side: 1, 3, 4
            cameFromRight = (lastSnapPointUsed == 1 || lastSnapPointUsed == 3 || lastSnapPointUsed == 4);
        }
        
        // Apply the logic: same side = reverse, opposite side = continue
        bool shouldReverse = (clickedOnRight == cameFromRight);
        
        int targetSnapPoint;
        if (shouldReverse && tileToReverseSnapPoint.ContainsKey(tile))
        {
            targetSnapPoint = tileToReverseSnapPoint[tile];
        }
        else
        {
            targetSnapPoint = tileToSnapPoint[tile];
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] U-shaped tile (TileFace) click:");
            Debug.Log($"  - Came from snap {lastSnapPointUsed} ({(cameFromRight ? "RIGHT" : "LEFT")} side)");
            Debug.Log($"  - Clicked {(clickedOnRight ? "RIGHT" : "LEFT")} side of tile");
            Debug.Log($"  - Logic: {(shouldReverse ? "REVERSE" : "CONTINUE")} -> snap {targetSnapPoint}");
        }
        
        return targetSnapPoint;
    }
    
    int DetermineSnapPointFromClick(TileInstance tile)
    {
        // This is for bank-to-river movement only
        // Find the two closest snap points to the bank
        List<int> validSnapPoints = FindClosestSnapPointsToBank(tile);
        
        if (validSnapPoints.Count == 0)
        {
            return 2; // Default fallback
        }
        
        if (validSnapPoints.Count == 1)
        {
            return validSnapPoints[0];
        }
        
        // Get mouse position in world space using raycast
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 clickWorldPos = hit.point;
            
            // Determine which snap point is closer to the click position
            float dist0 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[0]].position);
            float dist1 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[1]].position);
            
            int chosenSnapPoint = dist0 < dist1 ? validSnapPoints[0] : validSnapPoints[1];
            
            if (showDebugInfo)
            {
                Debug.Log($"[BoatController] Bank click at {clickWorldPos}, distances: {dist0:F2} vs {dist1:F2}, chose snap {chosenSnapPoint}");
            }
            
            return chosenSnapPoint;
        }
        
        // Fallback to first valid snap point
        return validSnapPoints[0];
    }
    
    List<int> FindClosestSnapPointsToBank(TileInstance tile)
    {
        List<int> closestPoints = new List<int>();
        Vector3 tileCenter = tile.transform.position;
        Vector3 bankDirection = bankPosition.name.Contains("Bottom") ? Vector3.back : Vector3.forward;
        
        // Find snap points that face toward the bank
        float bestDot = -1f;
        List<System.Tuple<int, float>> snapDots = new List<System.Tuple<int, float>>();
        
        for (int i = 0; i < tile.snapPoints.Length; i++)
        {
            if (tile.snapPoints[i] != null)
            {
                Vector3 snapDirection = (tile.snapPoints[i].position - tileCenter).normalized;
                float dot = Vector3.Dot(snapDirection, bankDirection);
                snapDots.Add(new System.Tuple<int, float>(i, dot));
                
                if (dot > bestDot)
                {
                    bestDot = dot;
                }
            }
        }
        
        // Get the two snap points with the highest dot product (closest to bank)
        snapDots.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        
        for (int i = 0; i < Mathf.Min(2, snapDots.Count); i++)
        {
            if (snapDots[i].Item2 >= bestDot - 0.1f) // Allow small tolerance
            {
                closestPoints.Add(snapDots[i].Item1);
            }
        }
        
        return closestPoints;
    }
    
    void MoveFromBankToTile(TileInstance targetTile, int snapPoint)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Moving from bank to tile {targetTile.name} at snap point {snapPoint}");
        }
        
        // Stop all animations (including bobbing) before starting movement
        StopAllCoroutines();
        isBobbing = false;
        
        // Lower all tiles except the target tile
        StartCoroutine(LowerNonTargetTiles(targetTile));
        
        // Move boat to tile
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, snapPoint));
    }
    
    IEnumerator LowerNonTargetTiles(TileInstance targetTile)
    {
        // Lower all highlighted tiles except the target tile
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                TileInstance tile = tileGO.GetComponent<TileInstance>();
                if (tile != null && tile != targetTile)
                {
                    // Restore material
                    if (originalMaterials.ContainsKey(tile))
                    {
                        Renderer renderer = tile.GetComponent<Renderer>();
                        if (renderer == null)
                        {
                            renderer = tile.GetComponentInChildren<Renderer>();
                        }
                        
                        if (renderer != null)
                        {
                            renderer.material = originalMaterials[tile];
                        }
                    }
                    
                    // Start smooth tile lower animation
                    StartCoroutine(LiftTileSmooth(tile, false));
                    
                    // Remove clickable component
                    SimpleTileClickHandler clickHandler = tileGO.GetComponent<SimpleTileClickHandler>();
                    if (clickHandler != null)
                    {
                        DestroyImmediate(clickHandler);
                    }
                }
            }
        }
        
        // Update highlighted tiles list to only include target tile
        highlightedTiles.RemoveAll(go => go != targetTile.gameObject);
        
        // Update original materials to only include target tile
        var tempMaterials = new Dictionary<TileInstance, Material>();
        if (originalMaterials.ContainsKey(targetTile))
        {
            tempMaterials[targetTile] = originalMaterials[targetTile];
        }
        originalMaterials = tempMaterials;
        
        yield return null;
    }
    
    IEnumerator MoveToTileCoroutine(TileInstance targetTile, int snapPoint)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Starting movement coroutine to {targetTile.name}, snap point {snapPoint}");
        }
        
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Start position: {startPos}");
        }
        
        // Store the target tile temporarily for rotation calculation
        TileInstance previousTile = currentTile;
        currentTile = targetTile;
        
        // Calculate target position
        Vector3 snapPosition = targetTile.snapPoints[snapPoint].position;
        Vector3 tileCenter = targetTile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        Vector3 targetPos = snapPosition - direction * snapOffset;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Snap position: {snapPosition}");
            Debug.Log($"[BoatController] Target position: {targetPos}");
            Debug.Log($"[BoatController] Direction: {direction}");
        }
        
        // Calculate target rotation
        Quaternion targetRot = GetSnapPointRotation(snapPoint);
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Target rotation: {targetRot.eulerAngles}");
        }
        
        // Restore previous tile
        currentTile = previousTile;
        
        // Smooth animation with ease in/out
        float elapsed = 0f;
        AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Starting animation loop. Duration: {moveSpeed}s");
        }
        
        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / moveSpeed;
            float easeProgress = easeCurve.Evaluate(progress);
            
            // Smooth position and rotation
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, easeProgress);
            Quaternion currentRot = Quaternion.Slerp(startRot, targetRot, easeProgress);
            
            transform.position = currentPos;
            transform.rotation = currentRot;
            
            if (showDebugInfo && (elapsed < 0.1f || progress > 0.9f))
            {
                Debug.Log($"[BoatController] Animation progress: {progress:F2}, position: {currentPos}");
            }
            
            yield return null;
        }
        
        // Ensure final position is exact
        transform.position = targetPos;
        transform.rotation = targetRot;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Movement animation complete. Final position: {transform.position}");
        }
        
        // Finalize placement
        PlaceOnTile(targetTile, snapPoint);
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] PlaceOnTile called. IsAtBank: {isAtBank}, CurrentTile: {currentTile?.name}");
        }
        
        // Use one movement point
        currentMovementPoints--;
        
        // Check if we have more moves available
        if (currentMovementPoints > 0)
        {
            // Find new valid moves and keep boat selected
            FindValidMoves();
            
            if (validMoves.Count > 0)
            {
                // Found valid moves - highlight them and keep boat lifted
                StartCoroutine(HighlightValidMovesWithDelay());
                
                // Keep boat selected and lifted
                isSelected = true;
                if (!isBobbing)
                {
                    isBobbing = true;
                    StartCoroutine(BobBoat());
                }
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BoatController] Movement complete. Remaining movement points: {currentMovementPoints}. Boat stays lifted.");
                }
            }
            else
            {
                // No valid moves found - end turn
                if (showDebugInfo)
                {
                    Debug.Log("[BoatController] No valid moves found. Turn complete.");
                }
                
                yield return new WaitForSeconds(0.5f);
                CompleteMovementTurn();
            }
        }
        else
        {
            // No more moves - end turn
            if (showDebugInfo)
            {
                Debug.Log("[BoatController] No movement points left. Turn complete.");
            }
            
            yield return new WaitForSeconds(0.5f);
            CompleteMovementTurn();
        }
        
        isMoving = false;
        
        if (showDebugInfo)
        {
            Debug.Log("[BoatController] Movement coroutine complete.");
        }
    }
    
    void CompleteMovementTurn()
    {
        // Lower boat and all remaining tiles AND reset movement points
        isSelected = false;
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights(); // This should lower all tiles including current tile
        
        // Reset movement points when turn is complete
        currentMovementPoints = 0;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Movement turn completed. Boat deselected. Movement points reset to 0.");
        }
    }
    
    // Public method to manually end turn (for UI button)
    public void EndMovementTurn()
    {
        currentMovementPoints = 0;
        CompleteMovementTurn();
        
        if (showDebugInfo)
        {
            Debug.Log("[BoatController] Turn fully ended. Movement points reset.");
        }
    }
    
    // Public method to reset movement points for testing (gives full movement back)
    public void ResetMovementPoints()
    {
        currentMovementPoints = maxMovementPoints;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Movement points reset to {maxMovementPoints} for testing.");
        }
        
        // If boat is selected, refresh the valid moves
        if (isSelected)
        {
            ClearHighlights();
            FindValidMoves();
            StartCoroutine(HighlightValidMovesWithDelay());
        }
    }
    
    Quaternion GetSnapPointRotation(int snapPointIndex)
    {
        // Calculate rotation based on the actual snap point direction relative to tile center
        if (currentTile != null && snapPointIndex >= 0 && snapPointIndex < currentTile.snapPoints.Length)
        {
            Vector3 snapPos = currentTile.snapPoints[snapPointIndex].position;
            Vector3 tileCenter = currentTile.transform.position;
            Vector3 directionToCenter = (tileCenter - snapPos).normalized;
            
            // Boat should face toward the tile center (inward)
            if (directionToCenter != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(directionToCenter);
                
                // Round to nearest 90-degree increment
                Vector3 euler = lookRotation.eulerAngles;
                float roundedY = Mathf.Round(euler.y / 90f) * 90f;
                
                if (showDebugInfo)
                {
                    Debug.Log($"[BoatController] Snap {snapPointIndex}: direction {directionToCenter}, rounded Y: {roundedY}");
                }
                
                return Quaternion.Euler(0f, roundedY, 0f);
            }
        }
        
        // Fallback to default rotation
        return Quaternion.identity;
    }
    
    // Public method to place boat at bank
    public void SetAtBank(Transform bankSpawnPoint)
    {
        bankPosition = bankSpawnPoint;
        isAtBank = true;
        currentTile = null;
        currentSnapPoint = -1;
        
        transform.position = bankSpawnPoint.position;
        transform.rotation = bankSpawnPoint.rotation;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Boat placed at bank: {bankSpawnPoint.name}");
        }
    }
    
    // Public method to place boat on specific tile and snap point
    public void PlaceOnTile(TileInstance tile, int snapPointIndex)
    {
        if (tile == null || snapPointIndex < 0 || snapPointIndex >= tile.snapPoints.Length)
        {
            Debug.LogError("[BoatController] Invalid tile or snap point!");
            return;
        }
        
        // Remember which snap point we used for future reverse moves
        lastSnapPointUsed = currentSnapPoint;
        
        currentTile = tile;
        currentSnapPoint = snapPointIndex;
        isAtBank = false;
        bankPosition = null;
        
        // Calculate position with offset
        Vector3 snapPosition = tile.snapPoints[snapPointIndex].position;
        Vector3 tileCenter = tile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        Vector3 boatPosition = snapPosition - direction * snapOffset;
        
        transform.position = boatPosition;
        
        // Orient boat based on actual snap point direction (dynamic calculation)
        Quaternion targetRotation = GetSnapPointRotation(snapPointIndex);
        transform.rotation = targetRotation;
        
        if (showDebugInfo)
        {
            Debug.Log($"[BoatController] Placed on tile {tile.name} at snap point {snapPointIndex} with rotation {targetRotation.eulerAngles}");
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (currentTile != null && currentSnapPoint >= 0)
        {
            // Draw connection to current snap point
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTile.snapPoints[currentSnapPoint].position);
            
            // Draw boat direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }
}

// Simple click handler for tiles
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