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

    public event System.Action OnTileConsumed;

    // ------------------------------------------------------------
    // 1.  Inspector references & settings
    // ------------------------------------------------------------

    [Header("Scene References")]
    public TileBagManager bagManager;    // drag BagManager GO here
    public GameObject tilePrefab;    // DominoTile prefab
    public Transform gridParent;    // optional parent object
    public BoatManager boatManager;   // link our BoatManager for spawning boats
    private LevelEditorManager levelEditorManager;


    [Header("Seeding")]
    [Tooltip("Check this to use the specific integer seed below. Uncheck for a new random river each time.")]
    public bool useSpecificSeed = false;
    [Tooltip("The specific seed to use for river generation when the box above is checked.")]
    public int gridSeed = 12345;

    [HideInInspector] // Hide from Inspector, let LevelEditorManager control it.
    public bool isPuzzleMode = false;


    //public enum ReversedTileRule { Blocker, PushYourLuck }
    [Header("Gameplay Rules")]
    //public ReversedTileRule reversedTileRule = ReversedTileRule.PushYourLuck;

    [Header("Grid Size")]
    public int rows = 6;
    public int cols = 6;

    [Header("Tile Spacing")]
    public float tileWidth = 2f;        // X spacing
    public float tileHeight = 1f;        // Z spacing
    public float gapX = 0f;   // space left-right
    public float gapZ = 0f;   // space front-back

    [Header("Spawn Animation")]
    public Vector2 delayRange = new Vector2(0f, 0.35f); // random delay per tile
    public float scaleTime = 0.25f;                  // pop-in duration

    [Header("Push Animation")]
    public float pushDuration = 0.8f;    // how long tiles take to slide
    public float spawnOffset = 3f;       // how far outside grid new tiles spawn
    public AnimationCurve pushCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    [Header("Physics Fall Settings")]
    [Tooltip("How long the tile's fade-out animation takes at the end of its life.")]
    public float fallFadeDuration = 1.5f;
    public float fallCleanupTime = 3f;    // how long before we clean up fallen tiles
    public float fallTorque = 5f;         // spinning force when ejected
    public bool createGameFloor = true;   // create physical floor under grid
    public float floorHeight = -0.5f;     // Y position of floor (negative = below tiles)
    public float separationTime = 0.2f;   // how long ejected tile takes to separate from neighbors

    [Header("Gameplay Visuals")]
    public GameObject blockerMarkerPrefab;
    public GameObject reversedTileMarkerPrefab;


    // ------------------------------------------------------------
    // 2.  Runtime storage
    // ------------------------------------------------------------

    private TileInstance[,] grid;        // 2-D array for grid management
    private Vector3 boardOrigin;         // calculated board center offset
    private bool isPushingInProgress = false;  // prevent multiple pushes


    private int currentSeed;             // stores the seed actually used for generation

    // ------------------------------------------------------------
    // 3.  Unity lifecycle
    // ------------------------------------------------------------

    private void Start()
    {

        levelEditorManager = FindFirstObjectByType<LevelEditorManager>();

        if (bagManager == null || tilePrefab == null)
        {
            Debug.LogError("[GridManager] Missing references!");
            return;
        }




        // --- START OF BLOCK TO ADD ---
        // This block initializes the random number generator with a seed.
        if (useSpecificSeed)
        {
            // Use the seed provided in the Inspector.
            currentSeed = gridSeed;
        }
        else
        {
            // Generate a new random seed based on the system time.
            currentSeed = (int)System.DateTime.Now.Ticks;
        }

        // Apply the chosen seed to Unity's random number generator.
        Random.InitState(currentSeed);
        Debug.Log($"[GridManager] Generating river with seed: {currentSeed}");
        // --- END OF BLOCK TO ADD ---



        // // Create physical floor under the grid if requested
        // if (createGameFloor)
        // {
        //     CreateGameFloor();
        // }

        bagManager.BuildBag();   // guarantees a fresh full bag
        //BuildGrid();  // Build the grid of tiles - commenet out for lever editor mode
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

        float totalWidth = (cols - 1) * (tileWidth + gapX);
        float totalHeight = (rows - 1) * (tileHeight + gapZ);

        // Shift so the middle of the board sits at (0,0,0)
        boardOrigin = new Vector3(
            -totalWidth / 2f,
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


    public List<Coroutine> CreateGridFromEditor(int newCols, int newRows, List<TileSaveData> tileBlueprint = null, bool animate = true)
    {

        var cameraController = FindFirstObjectByType<UniversalCameraController>();
        if (cameraController != null)
        {
            cameraController.OnGridChanged();
        }

        Dictionary<(int, int), TileSaveData> tileDataMap = null;
        if (tileBlueprint != null)
        {
            tileDataMap = new Dictionary<(int, int), TileSaveData>();
            foreach (var data in tileBlueprint)
            {
                tileDataMap[(data.gridX, data.gridY)] = data;
            }
        }

        // Create a list to hold all the animation coroutines we are about to start.
        List<Coroutine> runningAnimations = new List<Coroutine>();

        if (gridParent != null)
        {
            Transform oldFloor = gridParent.Find("GameFloor");
            if (oldFloor != null)
            {
                Destroy(oldFloor.gameObject);
            }
        }

        if (grid != null)
        {
            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (grid[x, y] != null)
                    {
                        Destroy(grid[x, y].gameObject);
                    }
                }
            }
        }

        cols = newCols;
        rows = newRows;

        // --- MODIFIED SECTION ---
        grid = new TileInstance[cols, rows];
        float totalWidth = (cols - 1) * (tileWidth + gapX);
        float totalHeight = (rows - 1) * (tileHeight + gapZ);
        boardOrigin = new Vector3(-totalWidth / 2f, 0f, -totalHeight / 2f);

        if (tileDataMap != null)
        {
            // --- BLUEPRINT MODE ---
            // If a blueprint exists, ONLY create tiles specified in the blueprint.
            // This is crucial for resuming an endless run correctly.
            Debug.Log("[GridManager] Blueprint detected. Creating specified tiles only.");
            foreach (var tileData in tileBlueprint)
            {
                // We directly use the data from the list, ignoring the loops.
                // This correctly handles sparse/offset grids.
                runningAnimations.Add(CreateTileAtGridPosition(tileData.gridX, tileData.gridY, tileData, animate));
            }
        }
        else
        {
            // --- RANDOM/FULL GRID MODE ---
            // If no blueprint is provided, create a full grid with random tiles.
            // This is the original behavior for starting a new puzzle in the editor.
            Debug.Log("[GridManager] No blueprint. Creating a full random grid.");
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    // Passing 'null' to CreateTileAtGridPosition triggers random generation.
                    runningAnimations.Add(CreateTileAtGridPosition(x, y, null, animate));
                }
            }
        }
        Debug.Log("[GridManager] River grid build process started.");
        // --- END OF MODIFIED SECTION ---

        if (createGameFloor)
        {
            CreateGameFloor();
        }

        Debug.Log($"[GridManager] Created a new {cols}x{rows} grid from the editor.");

        // Return the complete list of all running animations.
        return runningAnimations;
    }






    // ------------------------------------------------------------
    // 5.  Tile creation helper
    // ------------------------------------------------------------

    private Coroutine CreateTileAtGridPosition(int x, int y, TileSaveData data = null, bool animate = true)
    {
        TileType template;
        Quaternion rotation;
        bool isFlipped;
        bool isHardBlocker;

        // IF we have data (loading a level), use it.
        if (data != null)
        {
            template = FindTileTypeByName(data.tileTypeName);
            rotation = Quaternion.Euler(data.isFlipped ? 180f : 0f, data.rotationY, 0); // Use saved flip (X) and rotation (Y)
            isFlipped = data.isFlipped;
            isHardBlocker = data.isHardBlocker;
        }
        // ELSE (creating a new random grid), use random values.
        else
        {
            template = bagManager.DrawRandomTile();
            rotation = (template != null && template.canRotate180 && Random.value > 0.5f) ? Quaternion.Euler(0, 180f, 0) : Quaternion.identity;
            isFlipped = false; // Default to not flipped for new grids
            isHardBlocker = false;
        }

        if (template == null) // Failsafe for both cases
        {
            Debug.LogError($"Could not determine a tile template for ({x},{y}). Aborting creation for this tile.");
            return null;
        }

        // Calculate world-space position (on the floor)
        Vector3 pos = GetWorldPosition(x, y);

        // Spawn the prefab
        GameObject go = Instantiate(tilePrefab, pos, rotation, gridParent);
        go.name = $"Tile ({x},{y})";

        // Ensure tile has Rigidbody for physics
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = go.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // Controlled during sliding
        rb.mass = 1f;



        // Initialize tile with proper side
        TileInstance ti = go.GetComponent<TileInstance>();
        InitializeTile(ti, template, isFlipped);
        ti.IsHardBlocker = isHardBlocker; // Set blocker status from the correct source

        // After setting all the data, tell the grid manager to update the visuals accordingly.
        UpdateTileGameplayVisuals(ti);

        grid[x, y] = ti;
        BoardTile.MarkAsBoard(ti);   // board, not inventory - see BoardTile

        // Play staggered pop-in animation
        if (animate)
        {
            float delay = Random.Range(delayRange.x, delayRange.y);
            return StartCoroutine(ScaleIn(go.transform, delay, scaleTime));
        }
        else
        {
            // If not animating, return null because no coroutine was started.
            return null;
        }
    
    }



    // ------------------------------------------------------------
    // 6.  Position calculation
    // ------------------------------------------------------------

    public Vector3 GetWorldPosition(int x, int y)
    {
        return boardOrigin + new Vector3(
            x * (tileWidth + gapX),
            0f,
            y * (tileHeight + gapZ));
    }

    public Vector3 GetSpawnPosition(int rowIndex, bool fromLeft)
    {
        Vector3 rowCenter = GetWorldPosition(fromLeft ? 0 : cols - 1, rowIndex);
        float xOffset = fromLeft ? -spawnOffset : spawnOffset;
        return rowCenter + new Vector3(xOffset, 0f, 0f);
    }

    public (int x, int y) GetTileCoordinates(TileInstance tile)
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (grid[x, y] == tile)
                {
                    return (x, y);
                }
            }
        }
        return (-1, -1); // Return an invalid coordinate if not found
    }



    // ------------------------------------------------------------
    // 7.  Hapi's Havoc Push Mechanics - Public API
    // ------------------------------------------------------------

    /// Push a row horizontally, inserting new tile from specified side with chosen face
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
    //
    // This was three near-identical coroutines of ~880 lines total that had drifted apart in
    // nine separate ways - the copy-paste hazard of CLAUDE.md gotcha 7, where a fix applied to
    // one overload historically never reached the other two.
    //
    // They now differ ONLY in how the incoming tile is obtained. Everything from the boat
    // parenting onwards is PushRowInternal, so a fix lands in one place. The three public
    // signatures are unchanged - RiverControls, LevelEditorManager and EndlessModeManager all
    // still call them by their existing shapes, and no scene wiring moves.
    // ------------------------------------------------------------

    /// <summary>
    /// Sandbox / Editor bag push. The only variant that draws from the bag, and the only one
    /// that may randomise its own yaw.
    /// </summary>
    public IEnumerator PushRowCoroutine(int rowIndex, bool fromLeft, bool showObstacleSide)
    {
        TileType template = bagManager.DrawRandomTile();
        OnTileConsumed?.Invoke();

        if (template == null)
        {
            Debug.LogError("[GridManager] No tiles left in bag!");
            yield break;
        }

        float randomYaw = (template.canRotate180 && Random.value > 0.5f) ? 180f : 0f;

        yield return PushRowInternal(rowIndex, fromLeft,
            CreateIncomingTile(rowIndex, fromLeft, template, randomYaw, showObstacleSide));
    }

    /// <summary>
    /// Editor hand-arrow pushes and every Endless storm push: an explicit hand tile whose
    /// GameObject this method creates.
    /// </summary>
    public IEnumerator PushRowCoroutine(int rowIndex, bool fromLeft, PuzzleHandTile handTile)
    {
        OnTileConsumed?.Invoke();

        if (handTile == null || handTile.tileType == null)
        {
            Debug.LogError("[GridManager] Push failed: Invalid hand tile provided!");
            yield break;
        }

        yield return PushRowInternal(rowIndex, fromLeft,
            CreateIncomingTile(rowIndex, fromLeft, handTile.tileType,
                               handTile.rotationY, handTile.isFlipped));
    }

    /// <summary>
    /// Playing drag-to-drop-zone and the Endless player drag. The caller has already built the
    /// tile GameObject and animated it to the spawn position, so adopt it rather than creating
    /// a second one - and leave its transform alone, because it carries the orientation the
    /// player chose while dragging.
    /// </summary>
    public IEnumerator PushRowCoroutine(int rowIndex, bool fromLeft, PuzzleHandTile handTile, GameObject newTileGO)
    {
        OnTileConsumed?.Invoke();

        if (handTile == null || handTile.tileType == null || newTileGO == null)
        {
            Debug.LogError("[GridManager] Push failed: Invalid dragged hand tile provided!");
            yield break;
        }

        newTileGO.name = $"NewTile ({(fromLeft ? 0 : cols - 1)},{rowIndex})";

        yield return PushRowInternal(rowIndex, fromLeft,
            PrepareIncomingTile(newTileGO, handTile.tileType, handTile.isFlipped));
    }

    /// <summary>
    /// Builds the GameObject for a tile about to be pushed in, at the off-grid spawn position
    /// for that row and side.
    ///
    /// CANONICAL ORIENTATION: the flip is on X, matching the level loader, CreateNewEndlessRow
    /// and LevelEditorManager.FlipTile. The hand-tile overload used to build
    /// Quaternion.Euler(0, rotationY, isFlipped ? 180 : 0) instead. Unity's Euler order is ZXY,
    /// so that equals Euler(180, rotationY, 0) post-multiplied by Ry(180): the correct face,
    /// plus an extra 180 degree yaw that silently permuted the snap-point labels 0-3, 1-2, 4-5.
    /// That was risk R1. Never reconstruct this by reading eulerAngles back - see CLAUDE.md
    /// gotcha 3 - use TileOrientation.
    /// </summary>
    private TileInstance CreateIncomingTile(int rowIndex, bool fromLeft, TileType template,
                                            float rotationY, bool isFlipped)
    {
        GameObject go = Instantiate(tilePrefab,
                                    GetSpawnPosition(rowIndex, fromLeft),
                                    Quaternion.Euler(isFlipped ? 180f : 0f, rotationY, 0f),
                                    gridParent);
        go.name = $"NewTile ({(fromLeft ? 0 : cols - 1)},{rowIndex})";

        return PrepareIncomingTile(go, template, isFlipped);
    }

    /// <summary>
    /// Shared final setup for the incoming tile, whichever wrapper produced its GameObject.
    /// </summary>
    private TileInstance PrepareIncomingTile(GameObject go, TileType template, bool isFlipped)
    {
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;      // driven by SlideTileToPosition; EjectTileToAbyss releases it
        rb.mass = 1f;

        TileInstance tile = go.GetComponent<TileInstance>();
        BoardTile.MarkAsBoard(tile);   // board, not inventory - see BoardTile
        InitializeTile(tile, template, isFlipped);
        UpdateTileGameplayVisuals(tile);

        // The dragged-tile path may already carry one; do not add a second.
        if (levelEditorManager != null && go.GetComponent<EditorGridTile>() == null)
        {
            var editorTile = go.AddComponent<EditorGridTile>();
            editorTile.editorManager = levelEditorManager;
            editorTile.tileInstance = tile;
        }

        return tile;
    }

    /// <summary>
    /// The one push. <paramref name="newTile"/> is already built, oriented, and sitting at the
    /// off-grid spawn position for this row and side.
    /// </summary>
    private IEnumerator PushRowInternal(int rowIndex, bool fromLeft, TileInstance newTile)
    {
        isPushingInProgress = true;

        int insertCol = fromLeft ? 0 : cols - 1;
        int exitCol   = fromLeft ? cols - 1 : 0;

        // Deselect first so the boat's lift and highlights do not survive into the push.
        BoatController previouslySelectedBoat =
            (boatManager != null) ? boatManager.GetSelectedBoat() : null;
        if (previouslySelectedBoat != null)
        {
            previouslySelectedBoat.DeselectBoat();
            yield return new WaitForSeconds(0.3f);   // let the lowering animation play
        }

        RiverControls riverControls = FindFirstObjectByType<RiverControls>();
        if (riverControls != null) riverControls.SetArrowCollidersEnabled(false);

        TileInstance ejectingTile = grid[exitCol, rowIndex];

        // Captured before the tile is ejected, because the boat's landing snap point depends on
        // whether the destination tile's yaw differs from this one's.
        //
        // Read through TileOrientation, never eulerAngles. This compared raw eulerAngles.y and
        // was wrong in a way that decided WHERE BOATS LAND: two tiles authored at the same yaw,
        // one flipped and one not, read back 180 and 0 (CLAUDE.md gotcha 3), so the comparison
        // saw a 180 degree difference that did not exist and mirrored the snap point through
        // GetOppositeSnapPoint - putting the boat on the opposite edge. See tests L9 and X9.
        bool ejectedTileYawFlipped = (ejectingTile != null)
            && TileOrientation.IsYawFlipped(ejectingTile.transform);

        BoatController ejectedBoat = null;
        int originalSnapPoint = -1;
        List<BoatController> boatsToParent = new List<BoatController>();

        if (boatManager != null)
        {
            foreach (var boat in boatManager.GetPlayerBoats())
            {
                if (boat == null) continue;

                TileInstance boatTile = boat.GetCurrentTile();
                if (boatTile == ejectingTile)
                {
                    // Riding the tile that is about to fall off - un-parent it now.
                    ejectedBoat = boat;
                    originalSnapPoint = boat.GetCurrentSnapPoint();
                    boat.transform.SetParent(null, true);
                }
                else
                {
                    // Riding a tile that merely slides - parent it so it slides along.
                    for (int x = 0; x < cols; x++)
                    {
                        if (grid[x, rowIndex] == boatTile)
                        {
                            boatsToParent.Add(boat);
                            boat.transform.SetParent(boatTile.transform, true);
                            break;
                        }
                    }
                }
            }
        }

        // Animate the critical movements concurrently.
        //
        // The ejected boat's fade used to run TWICE in all three copies: once blocking, before
        // the tile was created, and then again inside this list. It now runs once, concurrent
        // with the slides, so ejection is visibly quicker and no longer re-fades an already
        // faded boat.
        List<Coroutine> essentialAnimations = new List<Coroutine>();

        if (ejectedBoat != null)
            essentialAnimations.Add(StartCoroutine(ejectedBoat.FadeOutForEjection()));

        essentialAnimations.Add(StartCoroutine(SlideTileToPosition(
            newTile.transform, GetWorldPosition(insertCol, rowIndex))));

        if (fromLeft)
        {
            for (int x = cols - 1; x >= 1; x--)
            {
                if (grid[x - 1, rowIndex] != null)
                {
                    grid[x, rowIndex] = grid[x - 1, rowIndex];
                    essentialAnimations.Add(StartCoroutine(SlideTileToPosition(
                        grid[x, rowIndex].transform, GetWorldPosition(x, rowIndex))));
                }
            }
            grid[0, rowIndex] = newTile;
        }
        else
        {
            for (int x = 0; x < cols - 1; x++)
            {
                if (grid[x + 1, rowIndex] != null)
                {
                    grid[x, rowIndex] = grid[x + 1, rowIndex];
                    essentialAnimations.Add(StartCoroutine(SlideTileToPosition(
                        grid[x, rowIndex].transform, GetWorldPosition(x, rowIndex))));
                }
            }
            grid[cols - 1, rowIndex] = newTile;
        }

        // Fire-and-forget: we do not wait for the ejected tile to finish falling.
        if (ejectingTile != null)
            StartCoroutine(EjectTileToAbyss(ejectingTile, !fromLeft));

        foreach (var anim in essentialAnimations)
            yield return anim;

        foreach (var boat in boatsToParent)
        {
            if (boat != null)
            {
                boat.transform.SetParent(null, true);
                Debug.Log($"[GridManager] Un-parenting {boat.name}.");
            }
        }

        // Put the ejected boat back on the board, or on a bank.
        if (ejectedBoat != null)
        {
            ejectedBoat.ResetStateAfterEjection();
            int targetRow = rowIndex + (ejectedBoat.starsCollected > 0 ? 1 : -1);

            if (targetRow < 0)
            {
                yield return StartCoroutine(ejectedBoat.AnimateToNewPositionAfterEjection(
                    RiverBankManager.BankSide.Bottom));
                ejectedBoat.enabled = true;
            }
            else if (targetRow >= rows)
            {
                yield return StartCoroutine(ejectedBoat.AnimateToNewPositionAfterEjection(
                    RiverBankManager.BankSide.Top));
                ejectedBoat.enabled = true;
            }
            else
            {
                int landingCol = fromLeft ? cols - 1 : 0;
                int searchDirection = (ejectedBoat.starsCollected > 0) ? 1 : -1;
                int currentRow = targetRow;
                RiverBankManager.BankSide destinationBank = (searchDirection == 1)
                    ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;

                // See through a run of reversed tiles to the first normal one.
                List<TileInstance> crossedReversedTiles = new List<TileInstance>();
                TileInstance finalLandingTile = null;

                while (currentRow >= 0 && currentRow < rows)
                {
                    TileInstance tileToCheck = GetTileAt(landingCol, currentRow);
                    if (tileToCheck != null && tileToCheck.IsReversed)
                    {
                        crossedReversedTiles.Add(tileToCheck);
                        currentRow += searchDirection;
                    }
                    else
                    {
                        finalLandingTile = tileToCheck;
                        break;
                    }
                }

                if (crossedReversedTiles.Count > 0)
                    ejectedBoat.ApplyPenaltiesForForcedMove(crossedReversedTiles);

                if (finalLandingTile != null)
                {
                    // Only mirror when the landing tile's yaw genuinely differs from the tile
                    // the boat left. GetOppositeSnapPoint here is GridManager's mirror mapping
                    // (0-3, 1-2, 4-5), which IS the permutation a 180 degree yaw induces - the
                    // right one of the two same-named methods. See ARCHITECTURE.md section 6.2 F.
                    int targetSnapPoint = originalSnapPoint;

                    if (TileOrientation.IsYawFlipped(finalLandingTile.transform) != ejectedTileYawFlipped)
                        targetSnapPoint = GetOppositeSnapPoint(originalSnapPoint);

                    yield return StartCoroutine(ejectedBoat.AnimateToNewPositionAfterEjection(
                        finalLandingTile, targetSnapPoint));
                    ejectedBoat.enabled = true;
                    ejectedBoat.CheckForCollectibleOnCurrentTile();
                }
                else
                {
                    yield return StartCoroutine(ejectedBoat.AnimateToNewPositionAfterEjection(
                        destinationBank));
                    ejectedBoat.enabled = true;
                }

                // Endless follows the boat with its camera proxy. The sandbox overload never
                // did this - one of the nine divergences.
                EndlessModeManager endlessManager = FindFirstObjectByType<EndlessModeManager>();
                if (endlessManager != null)
                    endlessManager.UpdateCameraTargetToBoatPosition();
            }
        }

        // Return the ejected tile to the bag. Puzzle mode has a finite hand, so it must not.
        if (ejectingTile != null && ejectingTile.originalTemplate != null)
        {
            if (!isPuzzleMode)
            {
                bagManager.ReturnTile(ejectingTile.originalTemplate);
                Debug.Log($"[GridManager] Returned {ejectingTile.originalTemplate.displayName} to bag");
            }
            else
            {
                Debug.Log($"[GridManager] Puzzle Mode: Did NOT return {ejectingTile.originalTemplate.displayName} to bag.");
            }
        }
        else if (ejectingTile != null)
        {
            Debug.LogWarning("[GridManager] Ejected tile had no originalTemplate - cannot return to bag!");
        }

        if (riverControls != null) riverControls.SetArrowCollidersEnabled(true);

        // Re-select, which lifts the boat and recomputes its valid moves.
        if (previouslySelectedBoat != null) previouslySelectedBoat.SelectBoat();

        HistoryManager.Instance.SaveState();

        isPushingInProgress = false;

        string tileName = (newTile.originalTemplate != null)
            ? newTile.originalTemplate.displayName : "<unknown>";
        Debug.Log($"[GridManager] Row {rowIndex} pushed from {(fromLeft ? "Left" : "Right")} " +
                  $"with '{tileName}' ({(newTile.IsReversed ? "Red (Obstacle)" : "Blue (River)")})");
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

        var goalMarker = tile.GetComponentInChildren<GoalMarker>();
        if (goalMarker != null)
        {
            Destroy(goalMarker.gameObject);
        }


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

        // --- NEW FADE LOGIC ---
        // Calculate how long the tile should fall before starting to fade.
        float actualFadeDuration = Mathf.Max(0, Mathf.Min(fallFadeDuration, fallCleanupTime));
        float solidTime = fallCleanupTime - actualFadeDuration;

        // Wait for the "solid" fall time to pass.
        if (solidTime > 0)
        {
            yield return new WaitForSeconds(solidTime);
        }

        // Now, perform the fade over the remaining duration.
        if (actualFadeDuration > 0 && tile != null)
        {
            var tileRenderer = tile.GetComponentInChildren<MeshRenderer>();
            if (tileRenderer != null)
            {

                Color startColor = tileRenderer.material.color;
                float fadeElapsed = 0f;
                while (fadeElapsed < actualFadeDuration)
                {
                    if (tile == null) yield break; // Safety check

                    fadeElapsed += Time.deltaTime;
                    float progress = fadeElapsed / actualFadeDuration;

                    float newAlpha = Mathf.Lerp(startColor.a, 0f, progress);
                    tileRenderer.material.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);

                    yield return null;
                }
            }
        }






        // Clean up the tile GameObject itself
        if (tile != null && tile.gameObject != null)
        {
            // First, explicitly tell the PathVisualizer to destroy its children.
            tile.GetComponent<PathVisualizer>()?.CleanUpPaths();

            // Then, destroy the main tile object.
            Destroy(tile.gameObject);
        }









    }

    //NEW OVERLOAD - Pushes a specific, pre-configured tile from the player's hand.












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

    public void InitializeTile(TileInstance tileInstance, TileType template, bool showObstacleSide)
    {
        if (showObstacleSide)
        {
            // Flip tile to show obstacle (red) side
            // tileInstance.transform.Rotate(180f, 0f, 0f);

            // Connections are BIDIRECTIONAL (CLAUDE.md domain conventions), so each straight
            // path is listed ONCE. This previously listed all three twice (0-2 and 2-0, etc).
            // PathVisualizer creates a LineRenderer GameObject per connection but registers them
            // under a canonical (min,max) key only when absent, so the three duplicates were
            // orphaned beyond CleanUpPaths' reach - +3 leaked GameObjects per reversed init.
            // LevelEditorManager.FlipTile already used this three-entry form; the two now agree.
            var straightPaths = new List<TileInstance.Connection>
        {
            new TileInstance.Connection { from = 0, to = 2 },
            new TileInstance.Connection { from = 1, to = 3 },
            new TileInstance.Connection { from = 4, to = 5 }
        };

            // PASS THE TEMPLATE to the tile
            tileInstance.Initialise(straightPaths, true, template);
        }
        else
        {
            // PASS THE TEMPLATE to the tile
            tileInstance.Initialise(ConvertPaths(template.frontPaths), false, template);
        }
    }

    // ------------------------------------------------------------
    // 12. Helper methods
    // ------------------------------------------------------------

    public List<TileInstance.Connection> ConvertPaths(List<Vector2Int> src)
    {
        var list = new List<TileInstance.Connection>();
        foreach (Vector2Int v in src)
            list.Add(new TileInstance.Connection { from = v.x, to = v.y });
        return list;
    }

    private IEnumerator ScaleIn(Transform t, float delay, float duration)
    {
        Vector3 target = t.localScale;
        t.localScale = Vector3.zero;

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
    private int GetOppositeSnapPoint(int snap)
    {
        switch (snap)
        {
            case 0: return 3;
            case 1: return 2;
            case 2: return 1;
            case 3: return 0;
            // Snap points 4 and 5 (sides) are their own opposites in a 180-degree flip.
            case 4: return 5;
            case 5: return 4;
            default: return snap; // Failsafe
        }
    }
    public bool IsPushInProgress()
    {
        return isPushingInProgress;
    }

    public TileInstance GetTileAt(int x, int y)
    {
        // cols/rows come from serialised defaults (6/6), so the bounds check below passes
        // before the array exists - notably in Endless mode, which builds the grid later.
        if (grid == null) return null;

        if (x >= 0 && x < cols && y >= 0 && y < rows)
            return grid[x, y];
        return null;
    }

    private TileType FindTileTypeByName(string name)
    {
        if (bagManager.tileLibrary == null) return null;
        foreach (var type in bagManager.tileLibrary.tileTypes)
        {
            if (type.displayName == name) return type;
        }
        return null;
    }

    /// Sets the layer for all tiles currently on the grid.
    /// This is used to make them temporarily invisible to UI raycasts during a drag.
    public void SetGridTilesLayer(string layerName)
    {
        int newLayer = LayerMask.NameToLayer(layerName);
        if (newLayer == -1)
        {
            Debug.LogError($"[GridManager] The layer '{layerName}' does not exist. Please create it in the Layer settings.");
            return;
        }

        if (grid == null) return;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                TileInstance tile = GetTileAt(x, y);
                if (tile != null)
                {
                    // We must set the layer on the parent and all children to ensure the collider is affected.
                    SetLayerRecursively(tile.gameObject, newLayer);
                }
            }
        }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    public void UpdateTileGameplayVisuals(TileInstance tile)
    {
        if (tile == null) return;

        // Define the names we'll use for the markers to easily find them.
        string blockerMarkerName = "BlockerMarker";
        string reversedMarkerName = "ReversedMarker";

        // Find any existing markers on the tile.
        Transform existingBlocker = tile.transform.Find(blockerMarkerName);
        Transform existingReversed = tile.transform.Find(reversedMarkerName);

        // --- Logic for what SHOULD be on the tile ---

        if (tile.IsReversed)
        {
            if (tile.IsHardBlocker)
            {
                // CONDITION: Should be a hard blocker.
                // Action: Ensure blocker marker exists, and reversed marker does NOT.
                if (existingReversed != null) Destroy(existingReversed.gameObject);
                if (existingBlocker == null && blockerMarkerPrefab != null)
                {
                    Instantiate(blockerMarkerPrefab, tile.transform.position, Quaternion.identity, tile.transform).name = blockerMarkerName;
                }
            }
            else
            {
                // CONDITION: Should be a standard reversed tile (our vortex).
                // Action: Ensure reversed marker exists, and blocker marker does NOT.
                if (existingBlocker != null) Destroy(existingBlocker.gameObject);
                if (existingReversed == null && reversedTileMarkerPrefab != null)
                {
                    // For the vortex, we need to apply the tile's rotation to the marker itself.
                    Quaternion markerRotation = tile.transform.rotation * Quaternion.Euler(90, 0, 0);
                    Vector3 markerPosition = tile.transform.position + (tile.transform.up * 0.01f);
                    Instantiate(reversedTileMarkerPrefab, markerPosition, markerRotation, tile.transform).name = reversedMarkerName;
                }
            }
        }
        else
        {
            // CONDITION: Tile is blue (not reversed).
            // Action: Ensure NO markers exist.
            if (existingBlocker != null) Destroy(existingBlocker.gameObject);
            if (existingReversed != null) Destroy(existingReversed.gameObject);
        }
    }




    public void CreateNewEndlessRow(int y, int width, bool animate, float obstacleChance, float blockerChance, float collectibleChance, GameObject collectiblePrefab)

    {
        // This method assumes the grid array is large enough. We will resize it later if needed.
        // For now, let's ensure it doesn't crash if the array is too small.
        if (y >= this.rows)
        {
            // In a future step, we would resize the 'grid' array here.
            // For now, we'll log a warning and continue, as the tiles will still be created visually.
            Debug.LogWarning($"[GridManager] Attempting to create row {y}, which is outside the initial grid bounds ({this.rows}). Pathfinding might be affected.");
        }

        List<TileInstance> newTiles = new List<TileInstance>();
        for (int x = 0; x < width; x++)
        {
            // We can reuse the CreateTileAtGridPosition logic, but we need to modify it slightly
            // to return the created TileInstance. For simplicity, let's duplicate the core logic here.

            TileType template = bagManager.DrawRandomTile();
            if (template == null) continue;

            float yRotation = (template.canRotate180 && Random.value > 0.5f) ? 180f : 0f;
            bool isFlipped = Random.value < obstacleChance; // Use the manager's variable
            bool isHardBlocker = isFlipped && (Random.value < blockerChance); // Use the manager's variable

            // Create the final rotation including the potential X-axis flip
            Quaternion finalRotation = Quaternion.Euler(isFlipped ? 180f : 0f, yRotation, 0f);

            Vector3 pos = GetWorldPosition(x, y);
            GameObject go = Instantiate(tilePrefab, pos, finalRotation, gridParent);


            go.name = $"Tile ({x},{y})";

            if (animate)
            {
                float delay = Random.Range(delayRange.x, delayRange.y);
                StartCoroutine(ScaleIn(go.transform, delay, scaleTime));
            }


            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            TileInstance ti = go.GetComponent<TileInstance>();
            // 1. First, set the data state.
            ti.IsHardBlocker = isHardBlocker;
            // 2. NOW, initialize. This method will read the IsReversed state and correctly set up paths.
            InitializeTile(ti, template, isFlipped);
            // 3. Finally, update visuals based on the final state.
            UpdateTileGameplayVisuals(ti);

            

            if (!isFlipped && collectiblePrefab != null && Random.value < collectibleChance)
            {
                // It's a blue tile and our dice roll succeeded. Spawn the collectible.
                Vector3 spawnPos = ti.transform.position + Vector3.up * 0.25f; // Place it slightly above the tile
                Instantiate(collectiblePrefab, spawnPos, Quaternion.identity, ti.transform);
            }

            // Set the reference in the grid array if possible
            // if (x < this.cols && y < this.rows)
            // {
            grid[x, y] = ti;
            BoardTile.MarkAsBoard(ti);   // board, not inventory - see BoardTile
            // }
        }
        
    }

    /// Destroys all tile GameObjects in a given row for Endless Mode.
    public void DestroyEndlessRow(int y, List<TileInstance> tilesToDestroy)
    {
        // If the list is null or empty, there's nothing to do.
        if (tilesToDestroy == null) return;

        foreach (var tile in tilesToDestroy)
        {
            if (tile != null)
            {
                // Clear its reference in the main grid array if it exists
                for (int x = 0; x < this.cols; x++)
                {
                    if (y < this.rows && grid[x, y] == tile)
                    {
                        grid[x, y] = null;
                        break; // Found it, move to the next tile
                    }
                }
                // Now, safely destroy the GameObject.
                Destroy(tile.gameObject);
            }
        }
    }


    public void ExpandGridForEndless(int newRowCount)
    {
        // If the grid is already big enough, do nothing.
        if (newRowCount <= this.rows) return;

        Debug.Log($"<color=cyan>[GridManager]</color> Expanding grid from {this.rows} to {newRowCount} rows.");

        // Create a new, larger 2D array.
        TileInstance[,] newGrid = new TileInstance[this.cols, newRowCount];

        // Copy the contents of the old grid into the new one.
        for (int y = 0; y < this.rows; y++)
        {
            for (int x = 0; x < this.cols; x++)
            {
                newGrid[x, y] = this.grid[x, y];
            }
        }

        // Replace the old grid with the new, larger one.
        this.grid = newGrid;
        this.rows = newRowCount; // IMPORTANT: Update the row count!
    }


    public List<TileInstance> GetTilesInRow(int y)
    {
        List<TileInstance> tiles = new List<TileInstance>();
        // Ensure the requested row is within the bounds of our grid array.
        if (y < 0 || y >= this.rows)
        {
            return tiles; // Return an empty list if the row is invalid.
        }

        for (int x = 0; x < this.cols; x++)
        {
            if (grid[x, y] != null)
            {
                tiles.Add(grid[x, y]);
            }
        }
        return tiles;
    }


    /// <summary>
    /// Reverse lookup: which tile and snap point is this world position sitting on?
    ///
    /// COMPARED IN THE HORIZONTAL PLANE ONLY. Every snap point on the board lies on the same
    /// flat Y, while the boat's Y swings with its resting height, the lift on selection and the
    /// idle bob - none of which says anything about WHICH snap point it is on. Including Y added
    /// a near-constant ~0.5 error that consumed the 0.5 threshold on its own, so a correctly
    /// placed resting boat reverse-looked-up onto a NEIGHBOURING tile across a shared edge and
    /// ResynchronizeStateWithTransform reported "Boat Desync Detected!" for a desync that had
    /// not happened. Measured on a row push: the boat's distance to its own snap point was
    /// 0.1500 before the push and 0.1500 after it - the push tracks the boat correctly - and
    /// only rose to 0.5220 once the boat settled to resting height. See test L2.
    ///
    /// The small inward boatOffset is what distinguishes the two coincident snap points at a
    /// shared tile edge, so dropping Y is precisely what lets it do its job.
    /// </summary>
    public (TileInstance, int) FindTileAndSnapPointAtWorldPos(Vector3 worldPosition)
    {
        TileInstance closestTile = null;
        int closestSnapPoint = -1;
        float minDistance = float.MaxValue;

        // How far inside the tile a boat sits from the snap point itself. This used to read
        // tile.GetComponent<BoatController>()?.snapOffset - a lookup on the TILE, which never
        // carries a BoatController, so it always fell through to this literal anyway.
        const float boatOffset = 0.15f;

        Vector3 flatTarget = new Vector3(worldPosition.x, 0f, worldPosition.z);

        // Search every tile in the grid
        for (int y = 0; y < this.rows; y++)
        {
            for (int x = 0; x < this.cols; x++)
            {
                TileInstance tile = grid[x, y];
                if (tile != null)
                {
                    // Check the distance to every snap point on this tile
                    for (int i = 0; i < tile.snapPoints.Length; i++)
                    {
                        if (tile.snapPoints[i] != null)
                        {
                            Vector3 tileCenter = tile.transform.position;
                            Vector3 direction = (tile.snapPoints[i].position - tileCenter).normalized;
                            Vector3 boatPositionOnSnap = tile.snapPoints[i].position - direction * boatOffset;
                            boatPositionOnSnap.y = 0f;

                            float distance = Vector3.Distance(flatTarget, boatPositionOnSnap);

                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                closestTile = tile;
                                closestSnapPoint = i;
                            }
                        }
                    }
                }
            }
        }

        // Only return a valid result if we found something very close
        if (minDistance < 0.5f) // Use a small threshold to ensure we found the right spot
        {
            return (closestTile, closestSnapPoint);
        }

        return (null, -1); // Return null if no valid spot was found
    }






}