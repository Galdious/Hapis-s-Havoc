/*
 *  BoatController.cs
 *  ---------------------------------------------------------------
 *  FINAL VERSION - BANK DOCKING CORRECTED
 *
 *  - This is the working baseline script with the correct U-turn logic.
 *  - FIXED: Bank docking is now correctly determined by the TILE'S ROW, not the snap point index.
 *  - All other working logic is preserved.
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
    
    [Header("Visual Feedback")]
    public Material validMoveMaterial;
    public Color selectedColor = Color.yellow;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
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
    private Dictionary<TileInstance, Material> originalMaterials = new Dictionary<TileInstance, Material>();
    private Dictionary<GameObject, Material> originalBankMaterials = new Dictionary<GameObject, Material>();
    
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
        
        if (isAtBank) FindBankEntryMoves();
        else if (currentTile != null) FindRiverPathMoves();
    }

    // ┌──────────────────────────────────────────────────┐
    // │ │
    // │ --- !!! ONLY THESE 2 METHODS ARE CHANGED !!! --- │
    // │ │
    // └──────────────────────────────────────────────────┘

    /// <summary>
    /// MODIFIED: Now uses the tile's ROW to determine which bank to highlight.
    /// </summary>
    void FindRiverPathMoves()
    {
        if (currentTile == null || currentSnapPoint < 0) return;

        var exitPoints = currentTile.connections
            .Select(c => c.from == currentSnapPoint ? c.to : (c.to == currentSnapPoint ? c.from : -1))
            .Where(p => p != -1)
            .ToList();
        exitPoints.Add(currentSnapPoint);

        foreach (int exitPoint in exitPoints.Distinct())
        {
            TileInstance neighbor = FindConnectedTile(currentTile, exitPoint);
            if (neighbor != null)
            {
                int entrySnap = FindConnectedSnapPoint(currentTile, exitPoint, neighbor);
                if (entrySnap != -1)
                {
                    if (!validMoves.Contains(neighbor)) validMoves.Add(neighbor);
                    if (exitPoint == currentSnapPoint)
                        tileToReverseSnapPoint[neighbor] = entrySnap;
                    else
                        tileToSnapPoint[neighbor] = entrySnap;
                }
            }
            else // Path leads off the grid
            {
                var (x, y) = GetTileCoordinates(currentTile);
                if (y == 0) HighlightBankForDocking(RiverBankManager.BankSide.Bottom);
                else if (y == gridManager.rows - 1) HighlightBankForDocking(RiverBankManager.BankSide.Top);
            }
        }
    }
    
    /// <summary>
    /// NEW HELPER: Gets the grid coordinates of a tile, necessary for the fix above.
    /// </summary>
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
        return (-1, -1); // Not found
    }
    
    // --- ALL OTHER CODE BELOW IS THE WORKING BASELINE ---
    
    void HighlightBankForDocking(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null) return;
        
        GameObject bankGO = riverBankManager.GetBankGameObject(side);
        if (bankGO != null && !highlightedBanks.Contains(bankGO))
        {
            highlightedBanks.Add(bankGO);
            var renderer = bankGO.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                if (!originalBankMaterials.ContainsKey(bankGO)) originalBankMaterials[bankGO] = renderer.material;
                renderer.material.color = Color.cyan;
            }

            var clicker = bankGO.AddComponent<BankClickHandler>();
            clicker.targetBoat = this;
            clicker.bankSide = side;
        }
    }

    void ClearHighlights()
    {
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                var tile = tileGO.GetComponent<TileInstance>();
                if (tile != null && originalMaterials.ContainsKey(tile))
                {
                    var renderer = tile.GetComponentInChildren<MeshRenderer>();
                    if(renderer != null) renderer.material = originalMaterials[tile];
                }
                StartCoroutine(LiftTileSmooth(tile, false));
                if (tileGO.GetComponent<SimpleTileClickHandler>() != null) DestroyImmediate(tileGO.GetComponent<SimpleTileClickHandler>());
            }
        }
        
        foreach(GameObject bankGO in highlightedBanks)
        {
            if(bankGO != null)
            {
                if(originalBankMaterials.ContainsKey(bankGO))
                {
                    var renderer = bankGO.GetComponentInChildren<MeshRenderer>();
                    if(renderer != null) renderer.material = originalBankMaterials[bankGO];
                }
                if(bankGO.GetComponent<BankClickHandler>() != null) Destroy(bankGO.GetComponent<BankClickHandler>());
            }
        }

        if (!isAtBank && currentTile != null) StartCoroutine(LiftTileSmooth(currentTile, false));
        
        highlightedTiles.Clear();
        highlightedBanks.Clear();
        originalMaterials.Clear();
        originalBankMaterials.Clear();
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

                if (distanceToContinue < distanceToReverse) return continueSnapIndex;
                else return reverseSnapIndex;
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
        isBobbing = false;
        ClearNonTargetHighlights(targetTile);
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, snapPoint));
    }

    void MoveFromTileToTile(TileInstance targetTile, int targetSnapPoint)
    {
        StopAllCoroutines();
        isBobbing = false;
        ClearNonTargetHighlights(targetTile);
        isMoving = true;
        StartCoroutine(MoveToTileCoroutine(targetTile, targetSnapPoint));
    }

    void ClearNonTargetHighlights(TileInstance targetTile)
    {
        var tilesToRemove = new List<GameObject>();
        foreach (var tileGO in highlightedTiles)
        {
            if (tileGO != null && (targetTile == null || tileGO != targetTile.gameObject))
                tilesToRemove.Add(tileGO);
        }
        foreach (var tileGO in tilesToRemove)
        {
            highlightedTiles.Remove(tileGO);
            var tile = tileGO.GetComponent<TileInstance>();
            if (tile != null)
            {
                if (originalMaterials.ContainsKey(tile))
                {
                    var renderer = tile.GetComponentInChildren<MeshRenderer>();
                    if (renderer != null) renderer.material = originalMaterials[tile];
                    originalMaterials.Remove(tile);
                }
                StartCoroutine(LiftTileSmooth(tile, false));
                if (tile.GetComponent<SimpleTileClickHandler>() != null) DestroyImmediate(tile.GetComponent<SimpleTileClickHandler>());
            }
        }
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
        isMoving = false;
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
        isMoving = false;
    }
    
    void CompleteMovementTurn()
    {
        isSelected = false;
        isBobbing = false;
        StopAllCoroutines();
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
    }

    public void EndMovementTurn()
    {
        currentMovementPoints = 0;
        if(isSelected) DeselectBoat();
    }

    public void ResetMovementPoints()
    {
        currentMovementPoints = maxMovementPoints;
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
                return Quaternion.Euler(0f, Mathf.Round(Quaternion.LookRotation(directionToCenter).eulerAngles.y / 90f) * 90f, 0f);
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
        if (tile == null || snapPointIndex < 0 || snapPointIndex >= tile.snapPoints.Length) return;
        currentTile = tile;
        currentSnapPoint = snapPointIndex;
        isAtBank = false;
        bankPosition = null;
        Vector3 snapPosition = tile.snapPoints[snapPointIndex].position;
        Vector3 tileCenter = tile.transform.position;
        Vector3 direction = (snapPosition - tileCenter).normalized;
        transform.position = snapPosition - direction * snapOffset;
        transform.rotation = GetSnapPointRotation(snapPointIndex);
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
        MeshRenderer renderer = tile.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) return;
        if (!originalMaterials.ContainsKey(tile)) originalMaterials[tile] = renderer.material;
        renderer.material.color = Color.magenta;
        StartCoroutine(LiftTileSmooth(tile, true));
        highlightedTiles.Add(tile.gameObject);
        var clickHandler = tile.gameObject.AddComponent<SimpleTileClickHandler>();
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