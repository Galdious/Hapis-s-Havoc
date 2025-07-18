/*
 *  GridManager.cs   –  Hapi's Havoc River Mechanics with Physics
 *  ---------------------------------------------------------------
 *  •   Spawns a 6 × 6 grid of domino tiles (river tiles).
 *  •   HORIZONTAL ONLY tile pushing - no column movement.
 *  •   New tiles spawn BESIDE the grid and slide in.
 *  •   Ejected tiles fall with physics off edges.
 *  •   Manual tile placement with blue/red side selection.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridManager : MonoBehaviour
{
    // ------------------------------------------------------------
    // 1.  Inspector references & settings
    // ------------------------------------------------------------

    [Header("Scene References")]
    public TileBagManager bagManager;    // drag BagManager GO here
    public GameObject     tilePrefab;    // DominoTile prefab
    public Transform      gridParent;    // optional parent object
    public BoatManager    boatManager;   // link our BoatManager for spawning boats

    public enum ReversedTileRule { Blocker, PushYourLuck }
    [Header("Gameplay Rules")]
    public ReversedTileRule reversedTileRule = ReversedTileRule.PushYourLuck;

    [Header("Grid Size")]
    public int   rows = 6;
    public int   cols = 6;

    [Header("Tile Spacing")]
    public float tileWidth  = 2f;        // X spacing
    public float tileHeight = 1f;        // Z spacing
    public float gapX = 0f;   // space left-right
    public float gapZ = 0f;   // space front-back

    [Header("Spawn Animation")]
    public Vector2 delayRange   = new Vector2(0f, 0.35f); // random delay per tile
    public float   scaleTime    = 0.25f;                  // pop-in duration

    [Header("Push Animation")]
    public float pushDuration = 0.8f;    // how long tiles take to slide
    public float spawnOffset = 3f;       // how far outside grid new tiles spawn
    public AnimationCurve pushCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Physics Fall Settings")]
    public float fallCleanupTime = 3f;    // how long before we clean up fallen tiles
    public float fallTorque = 5f;         // spinning force when ejected
    public bool createGameFloor = true;   // create physical floor under grid
    public float floorHeight = -0.5f;     // Y position of floor (negative = below tiles)
    public float separationTime = 0.2f;   // how long ejected tile takes to separate from neighbors

    // ------------------------------------------------------------
    // 2.  Runtime storage
    // ------------------------------------------------------------

    private TileInstance[,] grid;        // 2-D array for grid management
    private Vector3 boardOrigin;         // calculated board center offset
    private bool isPushingInProgress = false;  // prevent multiple pushes

    // ------------------------------------------------------------
    // 3.  Unity lifecycle
    // ------------------------------------------------------------

    private void Start()
    {
        if (bagManager == null || tilePrefab == null)
        {
            Debug.LogError("[GridManager] Missing references!");
            return;
        }

        // Create physical floor under the grid if requested
        if (createGameFloor)
        {
            CreateGameFloor();
        }

        bagManager.BuildBag();   // guarantees a fresh full bag
        BuildGrid();
    }
    

    private void Update()
    {
        // Input handling moved to RiverControls visual arrows
        // Keep this empty or add other update logic as needed
    }

    // ------------------------------------------------------------
    // 4.  Build the initial board
    // ------------------------------------------------------------

    private void BuildGrid()
    {
        grid = new TileInstance[cols, rows];

        float totalWidth  = (cols - 1) * (tileWidth  + gapX);
        float totalHeight = (rows - 1) * (tileHeight + gapZ);

        // Shift so the middle of the board sits at (0,0,0)
        boardOrigin = new Vector3(
            -totalWidth  / 2f,
            0f,
            -totalHeight / 2f);         

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                CreateTileAtGridPosition(x, y);
            }
        }

        Debug.Log("[GridManager] River grid built successfully.");
    }

    // ------------------------------------------------------------
    // 5.  Tile creation helper
    // ------------------------------------------------------------

    private void CreateTileAtGridPosition(int x, int y)
    {
        // Draw a tile template from the bag
        TileType template = bagManager.DrawRandomTile();
        if (template == null)
        {
            Debug.LogError("[GridManager] Bag ran out of tiles!");
            return;
        }

        // Calculate world-space position (on the floor)
        Vector3 pos = GetWorldPosition(x, y);

        // Spawn the prefab
        GameObject go = Instantiate(tilePrefab, pos, Quaternion.identity, gridParent);
        go.name = $"Tile ({x},{y})";

        // Ensure tile has Rigidbody for physics
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // Controlled during sliding
        rb.mass = 1f;

        // Random 0° / 180° spin (physical only – no path remap)
        if (template.canRotate180 && Random.value > 0.5f)
            go.transform.Rotate(0f, 180f, 0f);

        // Initialize tile with proper side
        TileInstance ti = go.GetComponent<TileInstance>();
        InitializeTile(ti, template, false); // Start with blue side

        grid[x, y] = ti;

        // Play staggered pop-in animation
        float delay = Random.Range(delayRange.x, delayRange.y);
        StartCoroutine(ScaleIn(go.transform, delay, scaleTime));
    }

    // ------------------------------------------------------------
    // 6.  Position calculation
    // ------------------------------------------------------------

    private Vector3 GetWorldPosition(int x, int y)
    {
        return boardOrigin + new Vector3(
            x * (tileWidth + gapX),
            0f,
            y * (tileHeight + gapZ));
    }

    private Vector3 GetSpawnPosition(int rowIndex, bool fromLeft)
    {
        Vector3 rowCenter = GetWorldPosition(fromLeft ? 0 : cols - 1, rowIndex);
        float xOffset = fromLeft ? -spawnOffset : spawnOffset;
        return rowCenter + new Vector3(xOffset, 0f, 0f);
    }

    // ------------------------------------------------------------
    // 7.  Hapi's Havoc Push Mechanics - Public API
    // ------------------------------------------------------------

    /// <summary>
    /// Push a row horizontally, inserting new tile from specified side with chosen face
    /// </summary>
    /// <param name="rowIndex">Which row (0-5)</param>
    /// <param name="fromLeft">True = insert from left side, False = from right side</param>
    /// <param name="showObstacleSide">True = red obstacle side, False = blue river side</param>
    public void PushRowFromSide(int rowIndex, bool fromLeft, bool showObstacleSide)
    {
        if (isPushingInProgress)
        {
            Debug.LogWarning("[GridManager] Push already in progress!");
            return;
        }

        if (rowIndex < 0 || rowIndex >= rows)
        {
            Debug.LogError($"[GridManager] Invalid row index: {rowIndex}");
            return;
        }

        StartCoroutine(PushRowCoroutine(rowIndex, fromLeft, showObstacleSide));
    }

    // ------------------------------------------------------------
    // 8.  Push implementation
    // ------------------------------------------------------------

    private IEnumerator PushRowCoroutine(int rowIndex, bool fromLeft, bool showObstacleSide)
    {
        isPushingInProgress = true;

        // Disable arrow colliders during push to prevent interference
        RiverControls riverControls = FindFirstObjectByType<RiverControls>();
        if (riverControls != null)
        {
            riverControls.SetArrowCollidersEnabled(false);
        }

        // Get new tile from bag
        TileType newTileTemplate = bagManager.DrawRandomTile();
        if (newTileTemplate == null)
        {
            Debug.LogError("[GridManager] No tiles left in bag!");
            isPushingInProgress = false;
            if (riverControls != null) riverControls.SetArrowCollidersEnabled(true);
            yield break;
        }

        // Determine positions
        int insertCol = fromLeft ? 0 : cols - 1;
        int exitCol = fromLeft ? cols - 1 : 0;
        Vector3 spawnPos = GetSpawnPosition(rowIndex, fromLeft);

        // Store the tile that will be ejected
        TileInstance ejectingTile = grid[exitCol, rowIndex];

        //This part finds any boats that need to be saved or parented befroe the tiles move
                // --- START OF BLOCK TO ADD (Part 1) ---
        float ejectedTileRotation = 0f;
        if(ejectingTile != null)
        {
            ejectedTileRotation = ejectingTile.transform.eulerAngles.y;
        }

        BoatController ejectedBoat = null;
        int originalSnapPoint = -1;
        List<BoatController> boatsToParent = new List<BoatController>();

        if (boatManager != null)
        {
            foreach (var boat in boatManager.GetPlayerBoats())
            {
                if (boat != null)
                {
                    TileInstance boatTile = boat.GetCurrentTile();
                    if (boatTile == ejectingTile)
                    {
                        // This boat is on the tile that will fall off. Save it.
                        ejectedBoat = boat;
                        originalSnapPoint = boat.GetCurrentSnapPoint();
                        boat.transform.SetParent(null, true); // Un-parent from the falling tile now!
                    }
                    else
                    {
                        // Check if this boat is on a tile that is just sliding.
                        for(int x = 0; x < cols; x++)
                        {
                            if (grid[x, rowIndex] == boatTile)
                            {
                                boatsToParent.Add(boat);
                                boat.transform.SetParent(boat.GetCurrentTile().transform, true);
                                break;
                            }
                        }
                    }
                }
            }
        }
        // --- END OF BLOCK TO ADD (Part 1) ---
        
        // Create new tile at spawn position (outside grid)
        GameObject newTileGO = Instantiate(tilePrefab, spawnPos, Quaternion.identity, gridParent);
        newTileGO.name = $"NewTile ({insertCol},{rowIndex})";

        // Setup physics for new tile
        Rigidbody newRb = newTileGO.GetComponent<Rigidbody>();
        if (newRb == null)
        {
            newRb = newTileGO.AddComponent<Rigidbody>();
        }
        newRb.isKinematic = true; // Controlled during sliding
        newRb.mass = 1f;

        // Random rotation
        if (newTileTemplate.canRotate180 && Random.value > 0.5f)
            newTileGO.transform.Rotate(0f, 180f, 0f);

        TileInstance newTile = newTileGO.GetComponent<TileInstance>();
        InitializeTile(newTile, newTileTemplate, showObstacleSide);


      
    // --- START OF CORRECTED BLOCK ---
    // BOAT STICKING LOGIC (Part 1: Find and Parent)
    List<BoatController> boatsOnRow = new List<BoatController>();
    if (boatManager != null)
    {
        // Get all tiles that are currently in the row that is about to move.
        List<TileInstance> tilesInRow = new List<TileInstance>();
        for(int x = 0; x < cols; x++) 
        {
            if(grid[x, rowIndex] != null)
            {
                tilesInRow.Add(grid[x, rowIndex]);
            }
        }

        // Find all boats that are on one of those tiles.
        foreach (var boat in boatManager.GetPlayerBoats())
        {
            if (boat != null && tilesInRow.Contains(boat.GetCurrentTile()))
            {
                boatsOnRow.Add(boat);
            }
        }

        // Temporarily parent the found boats to their respective tiles.
        foreach (var boat in boatsOnRow)
        {
            boat.transform.SetParent(boat.GetCurrentTile().transform, true);
            // The 'if (showDebugInfo)' has been removed from the next line
            Debug.Log($"[GridManager] Parenting {boat.name} to {boat.GetCurrentTile().name} for push.");
        }
    }
    // --- END OF CORRECTED BLOCK ---

    



        // Animate all tiles sliding
        List<Coroutine> slideCoroutines = new List<Coroutine>();

        // Slide new tile into position
        slideCoroutines.Add(StartCoroutine(SlideTileToPosition(
            newTile.transform, GetWorldPosition(insertCol, rowIndex))));

        if (fromLeft)
        {
            // Slide existing tiles right
            for (int x = cols - 1; x >= 1; x--)
            {
                if (grid[x - 1, rowIndex] != null)
                {
                    grid[x, rowIndex] = grid[x - 1, rowIndex];
                    slideCoroutines.Add(StartCoroutine(SlideTileToPosition(
                        grid[x, rowIndex].transform, GetWorldPosition(x, rowIndex))));
                }
            }
            grid[0, rowIndex] = newTile;
        }
        else
        {
            // Slide existing tiles left
            for (int x = 0; x < cols - 1; x++)
            {
                if (grid[x + 1, rowIndex] != null)
                {
                    grid[x, rowIndex] = grid[x + 1, rowIndex];
                    slideCoroutines.Add(StartCoroutine(SlideTileToPosition(
                        grid[x, rowIndex].transform, GetWorldPosition(x, rowIndex))));
                }
            }
            grid[cols - 1, rowIndex] = newTile;
        }

        // Animate ejected tile falling
        if (ejectingTile != null)
        {
            slideCoroutines.Add(StartCoroutine(EjectTileToAbyss(ejectingTile, !fromLeft))); // Fixed: opposite direction
        }

        // Wait for all animations to complete
        foreach (var coroutine in slideCoroutines)
        {
            yield return coroutine;
        }


      
      
// --- START OF CORRECTED BLOCK ---
    // BOAT STICKING LOGIC (Part 2: Un-parent)
    foreach (var boat in boatsOnRow)
    {
        if (boat != null) // Safety check in case the boat was on the ejected tile
        {
            boat.transform.SetParent(null, true); // Un-parent from tile
            // The 'if (showDebugInfo)' has been removed from the next line
            Debug.Log($"[GridManager] Un-parenting {boat.name}.");
        }
    }

        //This logic places the saved boat back onto the grid or bank
        // --- START OF BLOCK TO ADD (Part 2) ---
        if (ejectedBoat != null)
        {

            // Tell the boat to reset its internal state before moving it.
            ejectedBoat.ResetStateAfterEjection();

            // Determine target row based on cargo (one row "back")
            int targetRow = rowIndex + (ejectedBoat.hasCargo ? 1 : -1);

            // Bank Snap Rule
            if (targetRow < 0)
            {
                ejectedBoat.MoveToBank(RiverBankManager.BankSide.Bottom);
                ejectedBoat.enabled = true; // Re-enable the script
            }
            else if (targetRow >= rows)
            {
                ejectedBoat.MoveToBank(RiverBankManager.BankSide.Top);
                ejectedBoat.enabled = true; // Re-enable the script
            }
            else // Tile Snap Rule
            {
                int landingCol = fromLeft ? cols - 1 : 0;
                TileInstance landingTile = GetTileAt(landingCol, targetRow);

                if (landingTile != null)
                {
                    int targetSnapPoint = originalSnapPoint;
                    float newTileRotation = landingTile.transform.eulerAngles.y;

                    if (Mathf.Abs(ejectedTileRotation - newTileRotation) > 1f)
                    {
                        switch (originalSnapPoint)
                        {
                            case 0: targetSnapPoint = 3; break;
                            case 1: targetSnapPoint = 2; break;
                            case 2: targetSnapPoint = 1; break;
                            case 3: targetSnapPoint = 0; break;
                        }
                    }
                    ejectedBoat.PlaceOnTile(landingTile, targetSnapPoint);
                    ejectedBoat.enabled = true; // Re-enable the script to make it clickable
                }
                else
                {
                    // Fallback if the landing tile doesn't exist for some reason
                    ejectedBoat.MoveToBank(ejectedBoat.hasCargo ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom);
                    ejectedBoat.enabled = true;
                }
            }
        }
        // --- END OF BLOCK TO ADD (Part 2) ---


        // --- END OF CORRECTED BLOCK ---

        // Return ejected tile to bag (extract its template data)
        if (ejectingTile != null)
        {
            // TODO: Extract tile template from ejectingTile to return to bag properly
            // For now, we'll add it back as the same template
            bagManager.ReturnTile(newTileTemplate); // Placeholder - should be ejectingTile's template
        }

        // Re-enable arrow colliders after push is complete
        if (riverControls != null)
        {
            riverControls.SetArrowCollidersEnabled(true);
        }

        isPushingInProgress = false;
        
        string sideText = showObstacleSide ? "Red (Obstacle)" : "Blue (River)";
        string directionText = fromLeft ? "Left" : "Right";
        Debug.Log($"[GridManager] Row {rowIndex} pushed from {directionText} with {sideText} tile");
    }

    // ------------------------------------------------------------
    // 9.  Animation helpers
    // ------------------------------------------------------------

    private IEnumerator SlideTileToPosition(Transform tileTransform, Vector3 targetPosition)
    {
        Vector3 startPosition = tileTransform.position;
        float elapsed = 0f;

        while (elapsed < pushDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / pushDuration;
            float curveValue = pushCurve.Evaluate(progress);
            
            tileTransform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);
            yield return null;
        }

        tileTransform.position = targetPosition;
    }

    private IEnumerator EjectTileToAbyss(TileInstance tile, bool exitingLeft)
    {
        // First, smoothly slide the tile a bit further off the edge to separate it from neighbors
        Vector3 startPos = tile.transform.position;
        Vector3 separationTarget = startPos + (exitingLeft ? Vector3.left : Vector3.right) * 1f; // 1 unit separation
        
        float elapsed = 0f;
        
        // Slide to separation position using adjustable separation time
        while (elapsed < separationTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / separationTime;
            tile.transform.position = Vector3.Lerp(startPos, separationTarget, progress);
            yield return null;
        }
        
        tile.transform.position = separationTarget;
        
        // Now enable physics when tile is safely separated from neighbors
        Rigidbody rb = tile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = tile.gameObject.AddComponent<Rigidbody>();
        }
        
        rb.mass = 5f;                // Much heavier - more stable, less bouncy
        rb.isKinematic = false;      // Enable physics
        rb.linearDamping = 0.4f;     // More damping to reduce bouncing
        rb.angularDamping = 0.5f;    // More angular damping for stability
        
        // No artificial push - just let gravity do its work naturally
        // The tile is already positioned at the edge and will fall on its own
        
        // Add some random spinning for visual interest
        Vector3 randomTorque = new Vector3(
            Random.Range(-fallTorque, fallTorque),
            Random.Range(-fallTorque, fallTorque),
            Random.Range(-fallTorque, fallTorque)
        );
        rb.AddTorque(randomTorque, ForceMode.Impulse);
        
        // Wait for physics to play out, then clean up
        yield return new WaitForSeconds(fallCleanupTime);
        
        // Clean up the tile
        if (tile != null && tile.gameObject != null)
        {
            Destroy(tile.gameObject);
        }
    }

    // ------------------------------------------------------------
    // 10. Floor creation
    // ------------------------------------------------------------
    
    private void CreateGameFloor()
    {
        // Calculate grid bounds (floor should only be under the grid, not extending beyond)
        float gridWidth = (cols - 1) * (tileWidth + gapX) + tileWidth;
        float gridHeight = (rows - 1) * (tileHeight + gapZ) + tileHeight;
        
        // Main floor under the grid - use public floorHeight setting
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "GameFloor";
        floor.transform.position = new Vector3(0, floorHeight, 0); // Use adjustable height
        floor.transform.localScale = new Vector3(gridWidth, 1f, gridHeight); // Only grid size, no extra margin
        floor.transform.SetParent(gridParent);
        
        // Make it invisible but keep the collider
        Renderer floorRenderer = floor.GetComponent<Renderer>();
        floorRenderer.enabled = false; // Invisible
        
        // Add physics material for realistic interaction
        PhysicsMaterial floorMaterial = new PhysicsMaterial("FloorMaterial");
        floorMaterial.bounciness = 0.1f;        // Very little bounce
        floorMaterial.staticFriction = 0.8f;    // Good static friction
        floorMaterial.dynamicFriction = 0.6f;   // Good dynamic friction
        floor.GetComponent<Collider>().material = floorMaterial;
        
        Debug.Log($"[GridManager] Created game floor: {gridWidth} x {gridHeight} at Y = {floorHeight}");
    }

    // ------------------------------------------------------------
    // 11. Tile initialization helper
    // ------------------------------------------------------------

    private void InitializeTile(TileInstance tileInstance, TileType template, bool showObstacleSide)
    {
        if (showObstacleSide)
        {
            // Flip tile to show obstacle (red) side
            tileInstance.transform.Rotate(180f, 0f, 0f); // Flip on X-axis to show bottom
            
        var straightPaths = new List<TileInstance.Connection>
        {
            new TileInstance.Connection { from = 0, to = 2 },
            new TileInstance.Connection { from = 2, to = 0 },
            new TileInstance.Connection { from = 1, to = 3 },
            new TileInstance.Connection { from = 3, to = 1 },
            new TileInstance.Connection { from = 4, to = 5 },
            new TileInstance.Connection { from = 5, to = 4 }
        };
        
        tileInstance.Initialise(straightPaths, true);
        }
        else
        {
            // Show normal river paths (blue side) - no flip needed
            tileInstance.Initialise(ConvertPaths(template.frontPaths), false); // Pass false for isReversed
        }
    }

    // ------------------------------------------------------------
    // 12. Helper methods
    // ------------------------------------------------------------

    private List<TileInstance.Connection> ConvertPaths(List<Vector2Int> src)
    {
        var list = new List<TileInstance.Connection>();
        foreach (Vector2Int v in src)
            list.Add(new TileInstance.Connection { from = v.x, to = v.y });
        return list;
    }

    private IEnumerator ScaleIn(Transform t, float delay, float duration)
    {
        Vector3 target = t.localScale;
        t.localScale   = Vector3.zero;

        yield return new WaitForSeconds(delay);

        float time = 0f;
        while (time < duration)
        {
            float k = time / duration;
            t.localScale = Vector3.Lerp(Vector3.zero, target, k);
            time += Time.deltaTime;
            yield return null;
        }
        t.localScale = target;
    }

    // ------------------------------------------------------------
    // 13. Public utility methods
    // ------------------------------------------------------------

    public bool IsPushInProgress()
    {
        return isPushingInProgress;
    }

    public TileInstance GetTileAt(int x, int y)
    {
        if (x >= 0 && x < cols && y >= 0 && y < rows)
            return grid[x, y];
        return null;
    }
}