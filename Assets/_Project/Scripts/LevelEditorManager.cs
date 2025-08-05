using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; 
using System.IO;

public class LevelEditorManager : MonoBehaviour
{
    private enum EditorTool { Paint, Rotate, Flip, AddToHand, RemoveFromHand, SetStart, SetEnd, ToggleBlocker, PlaceCollectible }
    private EditorTool currentTool = EditorTool.Paint; // Default to painting
    private enum EditorBagMode { Sandbox, Hand }
    private EditorBagMode currentBagMode = EditorBagMode.Sandbox;

    [Header("Save & Load")]
    public TMP_InputField filenameInput;
    public Button saveLevelButton;
    public Button loadLevelButton;

    [Header("Goal Settings")]
    public TMP_InputField maxMovesInput;
    public GameObject startMarkerPrefab; // A green flag/cone you create
    private GameObject activeStartMarker;
    private TileInstance startTile;
    private int startSnapPointIndex = -1;
    private RiverBankManager.BankSide? startBank = null;
    public GameObject endMarkerPrefab; // A red flag/cone you create
    private GameObject activeEndMarker;
    private TileInstance endTile;
    private RiverBankManager.BankSide? endBank = null; // A nullable enum to store bank side


    [Header("Scene References")]
    public GridManager gridManager;
    public RiverBankManager riverBankManager; // <-- ADD THIS
    public RiverControls riverControls;       // <-- ADD THIS
    public BoatManager boatManager;

    [Header("UI References")]
    public GameObject gridSetupPanel; // <-- CHANGE THIS (was a bunch of separate fields)
    public TMP_InputField widthInput;
    public TMP_InputField heightInput;
    public Button createGridButton;
    public TMP_Dropdown collectibleDropdown;

    [Header("Editor Tools")]
    public Button paintToolButton;
    public Button rotateToolButton;
    public Button flipToolButton;
    public Button toggleBlockerToolButton;
    public Button placeCollectibleToolButton;
    public Button addToHandButton;      // <-- ADD
    public Button removeFromHandButton;
    public Button applyHandBagButton;     // <-- ADD
    public Button applySandboxBagButton;
    public Button setStartToolButton;
    public Button setEndToolButton;
    public Color toolSelectedColor = Color.yellow; // <-- ADD THIS
    private Color toolDefaultColor;                // <-- ADD THIS

    [Header("3D Palette Settings")]
    public Transform paletteContainer; // The empty GameObject we created
    public float paletteSpacing = 1.5f; // How far apart to space the palette tiles

    [Header("Editor Visuals")]
    public GameObject blockerMarkerPrefab;
    public GameObject starCollectiblePrefab;
    public GameObject extraMoveCollectiblePrefab;
    [Tooltip("The color to apply to the selected palette tile.")]
    public Color paletteSelectionColor = Color.cyan; // You can change this in the Inspector
    [Tooltip("How high to lift the selected palette tile.")]
    public float tileLiftHeight = 0.5f;
    [Tooltip("How long the lift animation takes.")]
    public float tileLiftDuration = 0.3f;
    public AnimationCurve tileLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Re-using the curve logic


    [Header("Puzzle Hand Setup")]
    public Transform handPaletteContainer; // The 3D container on the right
    public GameObject countIndicatorPrefab; // The TextMeshPro prefab for the "x2" counter

    private List<PuzzleHandTile> playerHand = new List<PuzzleHandTile>();
    private Dictionary<TileType, TMP_Text> handCounters = new Dictionary<TileType, TMP_Text>();

    private GameObject currentlyHighlightedHandTile;

    // This class holds our "brush" information
    private class EditorBrush
    {
        public TileType tileType;
        // public float currentRotationY = 0f;
        public PaletteTile sourcePaletteTile;
    }
    private EditorBrush currentBrush;
    private PuzzleHandTile selectedHandTileForPush = null;
    private LevelData currentLoadedLevelData;


    // This will store the original material of the highlighted palette tile
    private Dictionary<Renderer, Material> originalPaletteMaterial = new Dictionary<Renderer, Material>();
    // This will keep a reference to the GameObject we highlighted
    private GameObject currentlyHighlightedPaletteTile;
    private Dictionary<TileType, int> initialHandBlueprint = new Dictionary<TileType, int>();




    void Start()
    {
        if (gridManager == null || riverBankManager == null || riverControls == null || gridSetupPanel == null)
        {
            Debug.LogError("[LevelEditorManager] A reference is missing! Please assign all fields in the Inspector.");
            return;
        }

        createGridButton.onClick.AddListener(OnCreateGridClicked);

        // Make sure the setup panel is visible at the start
        gridSetupPanel.SetActive(true);


        maxMovesInput.text = "3";
        widthInput.text = "3";
        heightInput.text = "3";

        if (addToHandButton != null) addToHandButton.onClick.AddListener(SelectAddToHandTool);
        if (removeFromHandButton != null) removeFromHandButton.onClick.AddListener(SelectRemoveFromHandTool);
        if (paintToolButton != null) paintToolButton.onClick.AddListener(SelectPaintTool);
        if (rotateToolButton != null) rotateToolButton.onClick.AddListener(SelectRotateTool);
        if (flipToolButton != null) flipToolButton.onClick.AddListener(SelectFlipTool);
        if (applyHandBagButton != null) applyHandBagButton.onClick.AddListener(ApplyHandToBag);
        if (applySandboxBagButton != null) applySandboxBagButton.onClick.AddListener(ApplySandboxBag);
        if (setStartToolButton != null) setStartToolButton.onClick.AddListener(SelectSetStartTool);
        if (setEndToolButton != null) setEndToolButton.onClick.AddListener(SelectSetEndTool);
        if (toggleBlockerToolButton != null) toggleBlockerToolButton.onClick.AddListener(SelectToggleBlockerTool);
        if (placeCollectibleToolButton != null) placeCollectibleToolButton.onClick.AddListener(SelectPlaceCollectibleTool);
        if (saveLevelButton != null) saveLevelButton.onClick.AddListener(SaveLevel);
        //if (loadLevelButton != null) loadLevelButton.onClick.AddListener(LoadLevel);

        if (collectibleDropdown != null)
        {
            collectibleDropdown.ClearOptions();
            // Get the names of all items in our enum and add them to the dropdown
            string[] collectibleNames = System.Enum.GetNames(typeof(CollectibleType));
            collectibleDropdown.AddOptions(new List<string>(collectibleNames));
        }


        // Store the default color from one of the buttons at the start.
        if (paintToolButton != null)
        {
            toolDefaultColor = paintToolButton.colors.normalColor;
        }
        else
        {
            toolDefaultColor = Color.white; // Failsafe
        }

        // Set the initial visual state
        UpdateToolButtonVisuals();
        UpdateBagButtonVisuals();

        if (gridManager != null)
        {
            gridManager.OnTileConsumed += UpdateHandCounters;
        }

    }





    /// Gathers the complete current state of the game and returns it as a snapshot object.
    /// <returns>A populated GameStateSnapshot object.</returns>
    public GameStateSnapshot CreateCurrentStateSnapshot()
    {
        GameStateSnapshot snapshot = new GameStateSnapshot();

        // 1. Capture Grid and Collectible State
        snapshot.tileStates = new List<TileSaveData>();
        snapshot.collectibleStates = new List<CollectibleSaveData>();
        for (int y = 0; y < gridManager.rows; y++)
        {
            for (int x = 0; x < gridManager.cols; x++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile.originalTemplate != null)
                {
                    snapshot.tileStates.Add(new TileSaveData
                    {
                        tileTypeName = tile.originalTemplate.displayName,
                        gridX = x,
                        gridY = y,
                        rotationY = tile.transform.eulerAngles.y,
                        isFlipped = tile.IsReversed,
                        isHardBlocker = tile.IsHardBlocker
                    });

                    var collectible = tile.GetComponentInChildren<CollectibleInstance>();
                    if (collectible != null)
                    {
                        snapshot.collectibleStates.Add(new CollectibleSaveData
                        {
                            gridX = x,
                            gridY = y,
                            type = collectible.type,
                            value = collectible.value
                        });
                    }
                }
            }
        }

        // 2. Capture Row Lock State
        snapshot.lockedRowsState = riverControls.GetLockStatesAsInts();

        // 3. Capture Boat State (assuming single boat for puzzle mode)
        var boat = boatManager.GetPlayerBoats().FirstOrDefault();
        if (boat != null)
        {
            snapshot.boatMovementPoints = boat.currentMovementPoints;
            snapshot.boatStarsCollected = boat.starsCollected;

            // Use a GoalData object to store the boat's position cleanly
            snapshot.boatPosition = new GoalData();
            if (boat.GetCurrentTile() != null)
            {
                var coords = gridManager.GetTileCoordinates(boat.GetCurrentTile());
                snapshot.boatPosition.isBankGoal = false;
                snapshot.boatPosition.tileX = coords.x;
                snapshot.boatPosition.tileY = coords.y;
                snapshot.boatPosition.snapPointIndex = boat.GetCurrentSnapPoint();
            }
            else if (boat.CurrentBank.HasValue)
            {
                snapshot.boatPosition.isBankGoal = true;
                snapshot.boatPosition.bankSide = boat.CurrentBank.Value;
            }
        }

        // 4. Capture Player Hand State
        snapshot.playerHandState = new List<HandTileSaveData>();
        foreach (var handTile in playerHand)
        {
            snapshot.playerHandState.Add(new HandTileSaveData
            {
                tileTypeName = handTile.tileType.displayName,
                rotationY = handTile.rotationY,
                isFlipped = handTile.isFlipped
            });
        }

        // We will handle undoCount later when we implement the undo action itself.

        return snapshot;
    }











    private void SelectToggleBlockerTool()
    {
        currentTool = (currentTool == EditorTool.ToggleBlocker) ? EditorTool.Paint : EditorTool.ToggleBlocker;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Click a RED tile to toggle its blocker status.");
    }

    private void SelectPlaceCollectibleTool()
    {
        currentTool = (currentTool == EditorTool.PlaceCollectible) ? EditorTool.Paint : EditorTool.PlaceCollectible;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Select a type from the dropdown and click a tile.");
    }



    public int GetCurrentMaxMoves()
    {
        // If the input field exists and we can parse its text into a number, return that number.
        if (maxMovesInput != null && int.TryParse(maxMovesInput.text, out int moves))
        {
            return moves;
        }

        if (boatManager != null && boatManager.boatPrefab != null)
        {
            // Get the BoatController component directly from the prefab asset.
            BoatController prefabController = boatManager.boatPrefab.GetComponent<BoatController>();
            if (prefabController != null)
            {
                // Return the default value set on the prefab.
                return prefabController.maxMovementPoints;
            }
        }


        // Otherwise, return a safe default value.
        return 3;
    }







    private void SelectSetStartTool()
    {
        currentTool = (currentTool == EditorTool.SetStart) ? EditorTool.Paint : EditorTool.SetStart;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Click near a snap point to set the start position.");
    }

    private void SelectSetEndTool()
    {
        currentTool = (currentTool == EditorTool.SetEnd) ? EditorTool.Paint : EditorTool.SetEnd;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Click a tile or bank to set the end position.");
    }

    private void OnDestroy()
    {
        if (gridManager != null)
        {
            gridManager.OnTileConsumed -= UpdateHandCounters;
        }
    }

    private void SelectAddToHandTool()
    {
        currentTool = (currentTool == EditorTool.AddToHand) ? EditorTool.Paint : EditorTool.AddToHand;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Click a tile from the LEFT palette.");
    }

    private void SelectRemoveFromHandTool()
    {
        currentTool = (currentTool == EditorTool.RemoveFromHand) ? EditorTool.Paint : EditorTool.RemoveFromHand;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}. Click a tile from the RIGHT hand palette to remove.");
    }






    private void SelectPaintTool()
    {
        currentTool = EditorTool.Paint;
        UpdateToolButtonVisuals();
        Debug.Log("Switched to Paint tool.");
    }

    private void SelectRotateTool()
    {
        // If the rotate tool is already active, switch back to paint. Otherwise, activate it.
        currentTool = (currentTool == EditorTool.Rotate) ? EditorTool.Paint : EditorTool.Rotate;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}");
    }

    private void SelectFlipTool()
    {
        // If the flip tool is already active, switch back to paint. Otherwise, activate it.
        currentTool = (currentTool == EditorTool.Flip) ? EditorTool.Paint : EditorTool.Flip;
        UpdateToolButtonVisuals();
        Debug.Log($"Tool is now: {currentTool}");
    }






    private void OnCreateGridClicked()
    {

        if (boatManager.GetPlayerBoats().Count > 0)
        {
            boatManager.ClearAllBoats(); // Clear old boats first

        }

        // 1. Read the input
        int width = int.Parse(widthInput.text);
        int height = int.Parse(heightInput.text);




        ApplySandboxBag();


        // 2. Generate the core grid first
        gridManager.CreateGridFromEditor(width, height);
        // TUTAJ


        // After creating the grid, add the clickable component to each tile.
        for (int y = 0; y < gridManager.rows; y++)
        {
            for (int x = 0; x < gridManager.cols; x++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null)
                {
                    var editorTile = tile.gameObject.AddComponent<EditorGridTile>();
                    editorTile.editorManager = this;
                    editorTile.tileInstance = tile;
                }
            }
        }


        // 3. NOW, tell the other managers to build based on the new grid
        riverBankManager.GenerateBanksForGrid();
        riverControls.GenerateArrowsForGrid();


        // 4. THIS IS THE NEW STEP: Command the boat to spawn
        boatManager.SpawnTestBoats();


        // After creating the main grid, generate our 3D palette
        Generate3DPalette();


        // 5. Finally, hide the setup panel
        gridSetupPanel.SetActive(false);
    }

    public void ApplyHandToBag()
    {
        if (playerHand.Count > 0)
        {
            initialHandBlueprint.Clear();
            var handAsDictionary = playerHand.GroupBy(t => t.tileType).ToDictionary(g => g.Key, g => g.Count());
            initialHandBlueprint = new Dictionary<TileType, int>(handAsDictionary);

            gridManager.bagManager.BuildBagFromHand(handAsDictionary);
            gridManager.isPuzzleMode = true;
            Debug.LogWarning($"PUZZLE MODE ACTIVATED. Blueprint set.");
            UpdateHandCounters(); // Update counters to the initial state

            currentBagMode = EditorBagMode.Hand;
            UpdateBagButtonVisuals();
        }
        else
        {
            Debug.LogError("Cannot apply hand to bag: The hand is empty!");
        }
    }

    public void ApplySandboxBag()
    {
        initialHandBlueprint.Clear(); // No blueprint in sandbox mode
        gridManager.bagManager.BuildBag();
        gridManager.isPuzzleMode = false;
        Debug.LogWarning($"SANDBOX MODE ACTIVATED. Bag reset to full library.");
        UpdateHandCounters();

        currentBagMode = EditorBagMode.Sandbox;
        UpdateBagButtonVisuals();
    }






    // --- THIS IS OUR NEW PALETTE GENERATION METHOD ---
    void Generate3DPalette()
    {

        foreach (Transform child in paletteContainer)
        {
            Destroy(child.gameObject);
        }

        TileLibrary library = FindFirstObjectByType<TileBagManager>().tileLibrary;
        if (library == null)
        {
            Debug.LogError("Could not find TileLibrary!");
            return;
        }

        // --- START OF NEW POSITIONING LOGIC ---

        // 1. Calculate the X position for the palette
        // Get the total width of the grid itself
        float gridWidth = (gridManager.cols - 1) * (gridManager.tileWidth + gridManager.gapX) + gridManager.tileWidth;
        // The grid is centered, so its leftmost edge is at -(gridWidth / 2)
        float gridLeftEdgeX = -gridWidth / 2f;
        // Ask RiverControls how much space its arrows use (we'll add a buffer for this)
        // Let's assume the arrows and their spacing take up about 3-4 units.
        // A better way would be to get this from RiverControls directly if it's dynamic.
        float arrowSpaceBuffer = 4f;
        // Set the final X position for our container
        float paletteX = gridLeftEdgeX - arrowSpaceBuffer;


        // 2. Calculate the starting Z position to center the column
        int tileCount = library.tileTypes.Count;
        // Get the total height of the entire column
        float totalPaletteHeight = (tileCount - 1) * paletteSpacing;
        // The starting Z is half the total height shifted downwards
        float startZ = totalPaletteHeight / 2f;


        // 3. Set the final position of our container object
        paletteContainer.position = new Vector3(paletteX, 0, 0);

        // --- END OF NEW POSITIONING LOGIC ---


        float currentZ = 0;

        foreach (TileType type in library.tileTypes)
        {
            // Calculate the position for this tile relative to the container, using our centered startZ
            Vector3 spawnPos = paletteContainer.position + new Vector3(0, 0, startZ - currentZ);

            // ... (The rest of the method for instantiating and setting up the tile stays EXACTLY the same) ...
            GameObject tileGO = Instantiate(gridManager.tilePrefab, spawnPos, Quaternion.identity);
            tileGO.transform.SetParent(paletteContainer);
            tileGO.name = "Palette_" + type.displayName;

            var tileInstance = tileGO.GetComponent<TileInstance>();
            tileInstance.Initialise(gridManager.ConvertPaths(type.frontPaths), false, type);

            PaletteTile paletteTile = tileGO.AddComponent<PaletteTile>();
            paletteTile.editorManager = this;
            paletteTile.myTileType = type;

            currentZ += paletteSpacing;
        }


        // Adjust the main camera to see everything
        //AdjustCameraView();
    }

    public void OnPaletteTileClicked(PaletteTile clickedTile)
    {
        // The behavior depends on the selected tool.
        switch (currentTool)
        {
            case EditorTool.Rotate:
                RotateTile(clickedTile.GetComponent<TileInstance>());
                break;

            case EditorTool.Flip:
                FlipTile(clickedTile.GetComponent<TileInstance>());
                break;

            case EditorTool.AddToHand:
                AddToHand(clickedTile.myTileType);
                break;

            // The default behavior (and for Paint mode) is to select a brush.
            case EditorTool.Paint:
            default:
                SelectPaintTool(); // Ensure we're in paint mode visually
                ClearPaletteHighlight();

                if (currentBrush != null && currentBrush.sourcePaletteTile == clickedTile)
                {
                    currentBrush = null;
                    Debug.Log("Brush deselected.");
                    return;
                }

                currentBrush = new EditorBrush
                {
                    tileType = clickedTile.myTileType,
                    sourcePaletteTile = clickedTile
                };
                Debug.Log($"Selected Brush: {currentBrush.tileType.displayName}");
                HighlightPaletteTile(clickedTile.gameObject);
                break;
        }
    }


    public void OnHandPaletteTileClicked(HandPaletteTile clickedTile)
    {
        // The behavior depends on the selected tool.
        switch (currentTool)
        {
            case EditorTool.Rotate:
                RotateTile(clickedTile.GetComponent<TileInstance>());
                break;

            case EditorTool.Flip:
                FlipTile(clickedTile.GetComponent<TileInstance>());
                break;

            case EditorTool.RemoveFromHand:
                ClearHandHighlight();
                RemoveFromHand(clickedTile.myTileType);
                break;

            case EditorTool.Paint:
            default:
                // First, find an available data object for the type of tile that was clicked.
                PuzzleHandTile tileToSelect = playerHand.FirstOrDefault(t => t.tileType == clickedTile.myTileType);

                // If we couldn't find one (i.e., we've used them all), do nothing.
                if (tileToSelect == null)
                {
                    Debug.LogWarning($"No more tiles of type '{clickedTile.myTileType.displayName}' available in hand.");
                    ClearHandHighlight();
                    selectedHandTileForPush = null;
                    break;
                }

                // Check if the currently selected tile is of the same type as the one we just clicked.
                if (selectedHandTileForPush != null && selectedHandTileForPush.tileType == clickedTile.myTileType)
                {
                    // If it is, deselect it. This makes the click a toggle.
                    ClearHandHighlight();
                    selectedHandTileForPush = null;
                    Debug.Log("Hand tile deselected.");
                }
                else // Otherwise, select this new tile type.
                {
                    ClearHandHighlight();
                    selectedHandTileForPush = tileToSelect; // Select the first available one.

                    Debug.Log($"SELECTED '{clickedTile.myTileType.displayName}' for the next push.");
                    HighlightPaletteTile(clickedTile.gameObject); // Highlight the visual tile we clicked.
                    currentlyHighlightedHandTile = clickedTile.gameObject;
                }
                break;

        }
    }

    // --- ADD the core hand logic methods ---
    private void AddToHand(TileType type)
    {
        // Add a new instance of our data class to the list.
        playerHand.Add(new PuzzleHandTile(type));
        Debug.Log($"Added {type.displayName} to hand. Hand now contains {playerHand.Count} total tiles.");
        RedrawHandPalette();
    }

    private void RemoveFromHand(TileType type)
    {
        // Find the last tile of this type in the list and remove it.
        PuzzleHandTile tileToRemove = playerHand.LastOrDefault(t => t.tileType == type);
        if (tileToRemove != null)
        {
            playerHand.Remove(tileToRemove);
            Debug.Log($"Removed {type.displayName} from hand.");
            UpdateSingleCounter(type);
        }
    }


    /// <summary>
    /// Updates the text for a single tile type in the hand palette.
    /// </summary>
    private void UpdateSingleCounter(TileType type)
    {
        if (handCounters.ContainsKey(type))
        {
            TMP_Text counterText = handCounters[type];
            if (counterText != null)
            {
                int initialCount = initialHandBlueprint.ContainsKey(type) ? initialHandBlueprint[type] : 0;
                int currentAmountInBag = gridManager.bagManager.GetCountOfTileType(type);

                counterText.text = $"x{initialCount} ({currentAmountInBag} left)";
                counterText.color = (currentAmountInBag > 0) ? Color.white : Color.grey;
            }
        }
    }






    private void RedrawHandPalette()
    {
        // // Get a list of all UNIQUE tile types currently in the hand.
        // var uniqueTypesInHand = playerHand.Select(t => t.tileType).Distinct().ToList();


        foreach (Transform child in handPaletteContainer)
        {
            Destroy(child.gameObject);
        }

        handCounters.Clear();

        // 1. Calculate the X position, mirroring the main palette's logic.
        float gridWidth = (gridManager.cols - 1) * (gridManager.tileWidth + gridManager.gapX) + gridManager.tileWidth;
        // The grid is centered, so its rightmost edge is at +(gridWidth / 2)
        float gridRightEdgeX = gridWidth / 2f;
        // Use the same buffer space as the other palette.
        float arrowSpaceBuffer = 4f;
        // Set the final X position for our container, but on the positive side.
        float paletteX = gridRightEdgeX + arrowSpaceBuffer;

        // 2. Set the final position of our container object
        handPaletteContainer.position = new Vector3(paletteX, 0, 0);


        var groupedHand = playerHand.GroupBy(t => t.tileType).ToDictionary(g => g.Key, g => g.ToList());

        float startZ = (groupedHand.Count - 1) * paletteSpacing / 2f;
        int index = 0;

        // We iterate through the GROUPS to position them correctly in the column.
        foreach (var group in groupedHand.OrderBy(g => g.Key.displayName))
        {
            TileType type = group.Key;
            List<PuzzleHandTile> tilesOfType = group.Value;

            // Use the first tile in the group as the visual representative.
            PuzzleHandTile representativeTile = tilesOfType.First();

            // Calculate spawn position for this group.
            Vector3 spawnPos = new Vector3(0, 0, startZ - (index * paletteSpacing));

            // --- VISUAL TILE CREATION (This part is mostly the same) ---
            GameObject tileGO = Instantiate(gridManager.tilePrefab, spawnPos, Quaternion.Euler(0, representativeTile.rotationY, representativeTile.isFlipped ? 180f : 0f));
            tileGO.transform.SetParent(handPaletteContainer, false);
            tileGO.name = "HandPalette_" + type.displayName;

            var tileInstance = tileGO.GetComponent<TileInstance>();
            gridManager.InitializeTile(tileInstance, type, representativeTile.isFlipped);

            // --- ASSIGN ALL IDs TO THE CLICK HANDLER ---
            // This is a conceptual simplification. The clicker will now just report the type.
            // We will then find an available tile of that type in the hand.
            var handTileClicker = tileGO.AddComponent<HandPaletteTile>();
            handTileClicker.editorManager = this;
            handTileClicker.myTileType = type;
            // We no longer need the unique ID on the visual component itself.

            // --- COUNTER LOGIC (This is now correct) ---
            if (countIndicatorPrefab != null)
            {
                GameObject indicatorGO = Instantiate(countIndicatorPrefab, tileGO.transform);
                indicatorGO.transform.localPosition = new Vector3(0, 0.7f, -0.7f);
                var text = indicatorGO.GetComponentInChildren<TMP_Text>();
                if (text)
                {
                    // The text now shows how many of this type we have in our data list.
                    text.text = $"x{tilesOfType.Count}";
                    handCounters[type] = text;
                }
            }
            index++;
        }
    }

    public void UpdateHandCounters()
    {
        if (gridManager.isPuzzleMode)
        {
            // In puzzle mode, we update all counters based on the blueprint.
            foreach (var pair in handCounters)
            {
                UpdateSingleCounter(pair.Key);
            }
        }
        else
        {
            // In sandbox mode, all counters should just show their hand definition.
            foreach (var pair in handCounters)
            {
                TMP_Text counterText = pair.Value;
                int countInHand = playerHand.Count(t => t.tileType == pair.Key);
                counterText.text = $"x{countInHand}";
                counterText.color = Color.white;
            }
        }
    }



    public void OnGridTileClicked(TileInstance tileToModify)
    {
        if (tileToModify == null) return;

        // Use a switch to decide what to do based on the current tool
        switch (currentTool)
        {
            case EditorTool.Paint:
                PaintTile(tileToModify);
                break;

            case EditorTool.Rotate:
                RotateTile(tileToModify);
                break;

            case EditorTool.Flip:
                FlipTile(tileToModify);
                break;

            case EditorTool.SetStart:
                SetStartPosition(tileToModify);
                break;

            case EditorTool.SetEnd:
                SetEndPosition(tileToModify); // Pass the tile instance
                break;

            case EditorTool.ToggleBlocker: // <<< ADD THIS CASE
                ToggleTileBlocker(tileToModify);
                break;

            case EditorTool.PlaceCollectible: // <<< ADD THIS CASE
                PlaceOrRemoveCollectible(tileToModify);
                break;
        }
    }

    private void PlaceOrRemoveCollectible(TileInstance tile)
    {
        // First, check if a collectible already exists on this tile
        var existingCollectible = tile.GetComponentInChildren<CollectibleInstance>();

        if (existingCollectible != null)
        {
            // If one exists, destroy it. This makes the tool a toggle.
            Destroy(existingCollectible.gameObject);
            Debug.Log($"Removed collectible from tile {tile.name}");
        }
        else
        {
            // If none exists, create a new one based on the dropdown selection.
            if (collectibleDropdown == null) return;

            CollectibleType selectedType = (CollectibleType)collectibleDropdown.value;
            GameObject prefabToSpawn = null;

            switch (selectedType)
            {
                case CollectibleType.Star:
                    prefabToSpawn = starCollectiblePrefab;
                    break;
                case CollectibleType.ExtraMove:
                    prefabToSpawn = extraMoveCollectiblePrefab;
                    break;
            }

            // --- THIS IS THE FIX ---
            // We now get the value from the prefab itself instead of hardcoding it.
            int value = 0;
            if (prefabToSpawn != null)
            {
                var prefabInstance = prefabToSpawn.GetComponent<CollectibleInstance>();
                if (prefabInstance != null)
                {
                    value = prefabInstance.value;
                }
            }
            PlaceOrRemoveCollectible(tile, selectedType, value);


        }
    }

    // This version is for our loading code. It takes specific data and places the correct item.
    private void PlaceOrRemoveCollectible(TileInstance tile, CollectibleType type, int value)
    {
        // If a collectible already exists here (from a previous load attempt, etc.), clear it first.
        var existingCollectible = tile.GetComponentInChildren<CollectibleInstance>();
        if (existingCollectible != null)
        {
            Destroy(existingCollectible.gameObject);
        }

        GameObject prefabToSpawn = null;

        switch (type)
        {
            case CollectibleType.Star:
                prefabToSpawn = starCollectiblePrefab;
                break;
            case CollectibleType.ExtraMove:
                prefabToSpawn = extraMoveCollectiblePrefab;
                break;
        }

        if (prefabToSpawn != null)
        {
            Vector3 spawnPos = tile.transform.position + Vector3.up * 0.25f;
            GameObject collectibleGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, tile.transform);

            var collectibleInstance = collectibleGO.GetComponent<CollectibleInstance>();
            if (collectibleInstance != null)
            {
                collectibleInstance.type = type;
                collectibleInstance.value = value; // Set the value from the loaded data
            }
            // Don't need to log this during a level load, it clutters the console.
            // Debug.Log($"Placed {type} on tile {tile.name}");
        }
    }
    private void UpdateBlockerVisual(TileInstance tile)
    {
        string markerName = "BlockerMarker";
        Transform existingMarker = tile.transform.Find(markerName);

        // If the tile's data says it's a blocker...
        if (tile.IsHardBlocker)
        {
            // ...and it's a red tile and doesn't have a marker, add one.
            if (tile.IsReversed && existingMarker == null && blockerMarkerPrefab != null)
            {
                Instantiate(blockerMarkerPrefab, tile.transform.position, Quaternion.identity, tile.transform).name = markerName;
            }
        }
        // If the tile's data says it's NOT a blocker...
        else
        {
            // ...and it has a marker, remove it.
            if (existingMarker != null)
            {
                Destroy(existingMarker.gameObject);
            }
        }
    }
    private void ToggleTileBlocker(TileInstance tile)
    {
        // This method is for user clicks.
        if (!tile.IsReversed)
        {
            Debug.LogWarning($"Cannot set blocker status on a non-reversed (blue) tile: {tile.name}");
            return;
        }

        // Flip the data state.
        tile.IsHardBlocker = !tile.IsHardBlocker;
        Debug.Log($"Tile {tile.name} IsHardBlocker set to: {tile.IsHardBlocker}");

        // Tell the new helper to update the visual to match the new data state.
        UpdateBlockerVisual(tile);
    }



    private void ToggleTileBlocker(TileInstance tile, bool shouldBeBlocker)
    {
        // Safety check: We can only toggle blockers on reversed (red) tiles.
        if (!tile.IsReversed)
        {
            Debug.LogWarning($"Cannot set blocker status on a non-reversed (blue) tile: {tile.name}");
            return;
        }

        // Flip the state
        tile.IsHardBlocker = !tile.IsHardBlocker;
        Debug.Log($"Tile {tile.name} IsHardBlocker set to: {tile.IsHardBlocker}");

        // Update the visual marker
        string markerName = "BlockerMarker";
        Transform existingMarker = tile.transform.Find(markerName);

        if (tile.IsHardBlocker)
        {
            // If it's now a blocker and doesn't have a marker, add one.
            if (existingMarker == null && blockerMarkerPrefab != null)
            {
                GameObject marker = Instantiate(blockerMarkerPrefab, tile.transform.position, Quaternion.identity, tile.transform);
                marker.name = markerName;
            }
        }
        else
        {
            // If it's not a blocker and has a marker, remove it.
            if (existingMarker != null)
            {
                Destroy(existingMarker.gameObject);
            }
        }
    }




    public void OnBankClicked(RiverBankManager.BankSide side)
    {
        // This can only be called if a tool is active that cares about banks.
        if (currentTool == EditorTool.SetEnd)
        {
            SetEndPosition(null, side); // Pass the bank side
        }

        // We can add SetStart here later if we want to allow starting on a bank.
        else if (currentTool == EditorTool.SetStart)
        {
            // Tell the SetStartPosition method that a bank was clicked.
            SetStartPosition(null, side, null);
        }

    }


    // This is the version for when the USER CLICKS a tile. It uses a raycast to find the nearest snap point.
    private void SetStartPosition(TileInstance tile)
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            float minDistance = float.MaxValue;
            int closestSnapIndex = -1;
            for (int i = 0; i < tile.snapPoints.Length; i++)
            {
                if (tile.snapPoints[i] != null)
                {
                    float distance = Vector3.Distance(hit.point, tile.snapPoints[i].position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestSnapIndex = i;
                    }
                }
            }
            if (closestSnapIndex != -1)
            {
                SetStartPosition(tile, null, closestSnapIndex);
            }
        }
    }

    // This is the version that does the actual work for both user clicks and loading from data.
    private void SetStartPosition(TileInstance tile, RiverBankManager.BankSide? side, int? snapIndex)
    {
        Debug.Log($"[SetStartPosition] Method entered. Received snapIndex: {snapIndex}");
        if (activeStartMarker != null) Destroy(activeStartMarker);

        // Update the editor's internal state variables for the boat spawner
        this.startTile = tile;
        this.startBank = side;
        this.startSnapPointIndex = snapIndex ?? -1;

        // Determine marker's world position
        Vector3 markerPosition = Vector3.zero;
        if (side.HasValue)
        {
            markerPosition = riverBankManager.GetBankGameObject(side.Value).transform.position;
        }
        else if (tile != null && snapIndex.HasValue)
        {
            markerPosition = tile.snapPoints[snapIndex.Value].position;
        }

        // Instantiate the marker prefab
        activeStartMarker = Instantiate(startMarkerPrefab, markerPosition, Quaternion.identity);

        // Get the GoalMarker component and tell it to set itself up.
        // This is the key change.
        var markerComponent = activeStartMarker.AddComponent<GoalMarker>();

        Debug.Log($"[SetStartPosition] Condition is TRUE. snapIndex.Value is: {snapIndex.Value}");
        Debug.Log($"[SetStartPosition] Position of snap point {snapIndex.Value} is: {tile.snapPoints[snapIndex.Value].position}");
        markerComponent.Setup(gridManager, tile, side, snapIndex, false);
    }

    // This method places the end marker and attaches the data component.
    private void SetEndPosition(TileInstance tile = null, RiverBankManager.BankSide? side = null)
    {
        if (activeEndMarker != null) Destroy(activeEndMarker);

        // Update the editor's internal state variables
        this.endTile = tile;
        this.endBank = side;

        // Determine marker's world position
        Vector3 markerPosition = Vector3.zero;
        if (side.HasValue)
        {
            markerPosition = riverBankManager.GetBankGameObject(side.Value).transform.position;
        }
        else if (tile != null)
        {
            markerPosition = tile.transform.position;
        }

        // Instantiate the marker prefab
        activeEndMarker = Instantiate(endMarkerPrefab, markerPosition, Quaternion.identity);

        // Get the GoalMarker component and tell it to set itself up.
        // The snap point is null for the end position.
        var markerComponent = activeEndMarker.AddComponent<GoalMarker>();
        markerComponent.Setup(gridManager, tile, side, null, true);
    }




    // This is called by EditorGridTile.cs when a grid tile is clicked
    private void PaintTile(TileInstance tileToPaint)

    {



        if (currentBrush == null)
        {
            Debug.Log("No brush selected. Click a tile from the palette on the left.");
            return;
        }

        // if (tileToPaint == null) return;

        Debug.Log($"Painting tile at ({tileToPaint.name}) with brush '{currentBrush.tileType.displayName}'");

        // --- THIS IS THE CRITICAL FIX ---
        // 1. Get the PathVisualizer and tell it to destroy its old line renderers.
        var visualizer = tileToPaint.GetComponent<PathVisualizer>();
        if (visualizer != null)
        {
            visualizer.CleanUpPaths();
        }

        // 2. Re-Initialise the tile with the new data from our brush.
        //    This updates its internal logic and connections.
        tileToPaint.transform.rotation = Quaternion.identity;
        tileToPaint.Initialise(gridManager.ConvertPaths(currentBrush.tileType.frontPaths), false, currentBrush.tileType);

        // 3. Tell the PathVisualizer to draw the NEW paths.
        if (visualizer != null)
        {
            visualizer.DrawPaths();
        }
        // --- END OF CRITICAL FIX ---
    }



    // void AdjustCameraView()
    // {
    //     // This is a simple auto-adjust. You can fine-tune these values.
    //     Camera.main.transform.position = new Vector3(-5, 15, 5);
    //     Camera.main.transform.rotation = Quaternion.Euler(60, 0, 0);
    // }



    /// Applies the color and lift highlight to a given palette tile.
    /// </summary>
    private void HighlightPaletteTile(GameObject tileGO)
    {
        var renderer = tileGO.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) return;

        // Store its original material so we can restore it later.
        // We use sharedMaterial for reading to avoid creating material instances unintentionally.
        if (!originalPaletteMaterial.ContainsKey(renderer))
        {
            originalPaletteMaterial[renderer] = renderer.sharedMaterial;
        }

        // Change the color. Accessing .material creates a new instance if one doesn't exist.
        renderer.material.color = paletteSelectionColor;

        // Lift the tile.
        StartCoroutine(LiftTileSmooth(tileGO.transform, true));

        // Keep track of which object is highlighted.
        currentlyHighlightedPaletteTile = tileGO;
    }

    /// Removes the highlight from the currently selected palette tile.
    /// </summary>
    private void ClearPaletteHighlight()
    {
        if (currentlyHighlightedPaletteTile == null) return;

        var renderer = currentlyHighlightedPaletteTile.GetComponentInChildren<MeshRenderer>();
        if (renderer != null && originalPaletteMaterial.ContainsKey(renderer))
        {
            // Restore the original material.
            renderer.sharedMaterial = originalPaletteMaterial[renderer];

            // Lower the tile.
            StartCoroutine(LiftTileSmooth(currentlyHighlightedPaletteTile.transform, false));

            // Clean up our tracking variables.
            originalPaletteMaterial.Remove(renderer);
            currentlyHighlightedPaletteTile = null;
        }
    }

    /// Smoothly animates a tile's transform up or down.
    /// </summary>
    private IEnumerator LiftTileSmooth(Transform tileTransform, bool lift)
    {
        if (tileTransform == null) yield break;

        // Palette tiles are children of the container, so their "down" position is Y=0 locally.
        Vector3 startPos = tileTransform.localPosition;
        Vector3 targetPos = new Vector3(startPos.x, lift ? tileLiftHeight : 0f, startPos.z);

        float elapsed = 0f;
        while (elapsed < tileLiftDuration)
        {
            if (tileTransform == null) yield break;
            elapsed += Time.deltaTime;
            float progress = tileLiftCurve.Evaluate(elapsed / tileLiftDuration);
            tileTransform.localPosition = Vector3.Lerp(startPos, targetPos, progress);
            yield return null;
        }

        if (tileTransform != null)
        {
            tileTransform.localPosition = targetPos;
        }
    }


    private void RotateTile(TileInstance tileToRotate)
    {
        Debug.Log($"Rotating tile {tileToRotate.name}");


        // We need to check if the tile we clicked is in the hand palette.
        HandPaletteTile handTileComponent = tileToRotate.GetComponent<HandPaletteTile>();
        if (handTileComponent != null)
        {
            // It's a hand tile! Find the first matching data object in our list.
            PuzzleHandTile tileData = playerHand.FirstOrDefault(t => t.tileType == handTileComponent.myTileType);
            if (tileData != null)
            {
                // Update the rotation in our DATA object.
                // The % 360 ensures the value stays within 0-359.
                tileData.rotationY = (tileData.rotationY + 180f) % 360f;
                Debug.Log($"Updated hand data for {tileData.tileType.displayName}, new rotation: {tileData.rotationY} degrees.");
            }
        }




        // Apply the visual rotation
        tileToRotate.transform.Rotate(0, 180f, 0);

        // The paths are defined in local space, so we just need to tell the
        // visualizer to clean up and redraw in the new rotated orientation.
        var visualizer = tileToRotate.GetComponent<PathVisualizer>();
        if (visualizer != null)
        {
            visualizer.CleanUpPaths();
            visualizer.DrawPaths();
        }
    }


    private void FlipTile(TileInstance tileToFlip)
    {
        Debug.Log($"Flipping tile {tileToFlip.name}");


        // Check if the tile we clicked is in the hand palette.
        HandPaletteTile handTileComponent = tileToFlip.GetComponent<HandPaletteTile>();
        if (handTileComponent != null)
        {
            // It's a hand tile! Find its corresponding data object.
            PuzzleHandTile tileData = playerHand.FirstOrDefault(t => t.tileType == handTileComponent.myTileType);
            if (tileData != null)
            {
                // Update the flipped state in our DATA object.
                tileData.isFlipped = !tileData.isFlipped;
                Debug.Log($"Updated hand data for {tileData.tileType.displayName}, IsFlipped is now: {tileData.isFlipped}.");
            }
        }





        // Get the visualizer
        var visualizer = tileToFlip.GetComponent<PathVisualizer>();
        if (visualizer != null)
        {
            visualizer.CleanUpPaths();
        }

        // We need to know which way to flip it
        bool willBeReversed = !tileToFlip.IsReversed;

        // Apply visual rotation on X-axis
        tileToFlip.transform.Rotate(180f, 0, 0);

        if (willBeReversed)
        {
            // FLIPPING TO RED (OBSTACLE) SIDE
            // Obstacle sides have standard straight paths
            var straightPaths = new List<TileInstance.Connection>
            {
                new TileInstance.Connection { from = 0, to = 2 },
                new TileInstance.Connection { from = 1, to = 3 },
                new TileInstance.Connection { from = 4, to = 5 },
            };
            tileToFlip.Initialise(straightPaths, true, tileToFlip.originalTemplate);
        }
        else
        {
            // FLIPPING BACK TO BLUE (PATH) SIDE
            // Restore the original paths from its template
            tileToFlip.Initialise(gridManager.ConvertPaths(tileToFlip.originalTemplate.frontPaths), false, tileToFlip.originalTemplate);
        }

        // Redraw the new paths
        if (visualizer != null)
        {
            visualizer.DrawPaths();
        }
    }



    private void UpdateToolButtonVisuals()
    {
        // This is how you change a button's color via script.
        // You must get its ColorBlock, modify it, and then assign it back.

        SetButtonColor(paintToolButton, EditorTool.Paint);
        SetButtonColor(rotateToolButton, EditorTool.Rotate);
        SetButtonColor(flipToolButton, EditorTool.Flip);
        SetButtonColor(addToHandButton, EditorTool.AddToHand);
        SetButtonColor(removeFromHandButton, EditorTool.RemoveFromHand);
        SetButtonColor(setStartToolButton, EditorTool.SetStart);
        SetButtonColor(setEndToolButton, EditorTool.SetEnd);
        SetButtonColor(toggleBlockerToolButton, EditorTool.ToggleBlocker);
        SetButtonColor(placeCollectibleToolButton, EditorTool.PlaceCollectible);

        EventSystem.current.SetSelectedGameObject(null);
    }

    private void SetButtonColor(Button btn, EditorTool tool)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = (currentTool == tool) ? toolSelectedColor : toolDefaultColor;
        btn.colors = colors;

        if (currentTool == tool) EventSystem.current.SetSelectedGameObject(null);
    }


    public IEnumerator HandleArrowPush(int row, bool fromLeft, bool isForObstacleSide)
    {
        HistoryManager.Instance.SaveState();

        // Step 1: Are we in Puzzle Mode?
        if (gridManager.isPuzzleMode)
        {
            // We are in puzzle mode. A hand tile MUST be selected.
            if (selectedHandTileForPush == null)
            {
                Debug.LogError("PUZZLE MODE: Cannot push. No tile selected from the hand. Please select a tile first.");
                yield break; // STOP. Do absolutely nothing.
            }

            // A hand tile IS selected. Proceed with the puzzle push.
            Debug.Log($"PUZZLE MODE: Pushing selected hand tile: {selectedHandTileForPush.tileType.displayName}");

            // Immediately consume the tile from our local data. This prevents re-use.
            PuzzleHandTile tileToPush = new PuzzleHandTile(selectedHandTileForPush.tileType)
            {
                rotationY = selectedHandTileForPush.rotationY,
                isFlipped = isForObstacleSide,
                id = selectedHandTileForPush.id
            };
            playerHand.RemoveAll(t => t.id == selectedHandTileForPush.id);
            ClearHandHighlight();
            selectedHandTileForPush = null; // Deselect immediately

            ApplyHandToBag();

            // Now, tell GridManager to push this specific tile and wait for it to finish.
            // GridManager will announce OnTileConsumed, which triggers the UI update.
            yield return StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, tileToPush));
        }
        else // Step 2: We are NOT in puzzle mode.
        {
            // We must be in Sandbox mode. Perform a normal push using the infinite bag.
            Debug.Log("SANDBOX MODE: Pushing random tile from bag.");
            yield return StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, isForObstacleSide));
        }
    }


    // should we leave it?
    // public bool UseSelectedHandTile(int row, bool fromLeft, bool isForObstacleSide)
    // {
    //     // Check if a tile has been selected from the hand.
    //     if (selectedHandTileForPush == null)
    //     {
    //         Debug.LogWarning("Arrow clicked, but no hand tile was selected. Performing default action.");
    //         return false; // Tells RiverControls to do its normal push.
    //     }

    //     Debug.Log($"Pushing selected hand tile: {selectedHandTileForPush.tileType.displayName}");

    //     // Here, we decide the flip state. If the RED arrow is clicked, we override the tile's saved flip state.
    //     bool pushAsObstacle = isForObstacleSide;

    //     // We create a temporary data object to send to the GridManager.
    //     // This allows the red arrow to override the flip state for one push.
    //     PuzzleHandTile tileToPush = new PuzzleHandTile(selectedHandTileForPush.tileType)
    //     {
    //         rotationY = selectedHandTileForPush.rotationY,
    //         isFlipped = pushAsObstacle, // Use the state from the arrow click
    //         id = selectedHandTileForPush.id
    //     };

    //     // ???
    //     ClearHandHighlight();


    //     // Tell the GridManager to perform the special push.
    //     StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, tileToPush));

    //     // Consume the tile from our data list by finding its unique ID.
    //     var tileDataToRemove = playerHand.FirstOrDefault(t => t.id == selectedHandTileForPush.id);
    //     if (tileDataToRemove != null)
    //     {
    //         playerHand.Remove(tileDataToRemove);
    //     }

    //     // Update the counter text for the tile type we just used.
    //     UpdateSingleCounter(selectedHandTileForPush.tileType);

    //     // Clear the selection state so you can't push the same tile twice.
    //     selectedHandTileForPush = null;

    //     // Tell RiverControls that we handled the push successfully.
    //     return true;
    // }


    private void ClearHandHighlight()
    {
        if (currentlyHighlightedHandTile == null) return;

        var renderer = currentlyHighlightedHandTile.GetComponentInChildren<MeshRenderer>();
        if (renderer != null && originalPaletteMaterial.ContainsKey(renderer))
        {
            // Restore the original material.
            renderer.sharedMaterial = originalPaletteMaterial[renderer];

            // Lower the tile.
            StartCoroutine(LiftTileSmooth(currentlyHighlightedHandTile.transform, false));

            // Clean up our tracking variables.
            originalPaletteMaterial.Remove(renderer);
            currentlyHighlightedHandTile = null;
        }
    }

    private void UpdateBagButtonVisuals()
    {
        // This function will set the colors based on the currentBagMode state.
        SetBagButtonColor(applySandboxBagButton, EditorBagMode.Sandbox);
        SetBagButtonColor(applyHandBagButton, EditorBagMode.Hand);
    }

    private void SetBagButtonColor(Button btn, EditorBagMode mode)
    {
        if (btn == null) return;
        var colors = btn.colors;
        // Compare against the currentBagMode to decide the color
        colors.normalColor = (currentBagMode == mode) ? toolSelectedColor : toolDefaultColor;
        btn.colors = colors;

        if (currentBagMode == mode) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Gathers all the current level data from the editor, converts it to JSON,
    /// and saves it to a file in the persistent data path.
    /// </summary>
    public void SaveLevel()
    {
        // 1. Get the filename from the input field. If it's empty, do nothing.
        string filename = filenameInput.text;
        if (string.IsNullOrWhiteSpace(filename))
        {
            Debug.LogError("Cannot save level: Please enter a filename.");
            return;
        }

        // 2. Create the master LevelData object to hold everything.
        LevelData levelData = new LevelData();

        // 3. Populate Grid & Rules Data
        levelData.gridWidth = gridManager.cols;
        levelData.gridHeight = gridManager.rows;
        levelData.maxMoves = GetCurrentMaxMoves();
        //levelData.lockedRows = riverControls.GetLockStates(); // We will need to add this helper function
        levelData.lockedRows = riverControls.GetLockStatesAsInts();

        // 4. Populate Tile Data
        for (int y = 0; y < gridManager.rows; y++)
        {
            for (int x = 0; x < gridManager.cols; x++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile.originalTemplate != null)
                {
                    TileSaveData tileSave = new TileSaveData
                    {
                        tileTypeName = tile.originalTemplate.displayName,
                        gridX = x,
                        gridY = y,
                        rotationY = tile.transform.eulerAngles.y,
                        isFlipped = tile.IsReversed,
                        isHardBlocker = tile.IsHardBlocker
                    };
                    levelData.tiles.Add(tileSave);

                    // While we're here, check for a collectible on this tile
                    var collectible = tile.GetComponentInChildren<CollectibleInstance>();
                    if (collectible != null)
                    {
                        CollectibleSaveData collectibleSave = new CollectibleSaveData
                        {
                            gridX = x,
                            gridY = y,
                            type = collectible.type,
                            value = collectible.value
                        };
                        levelData.collectibles.Add(collectibleSave);
                    }
                }
            }
        }

        // 5. Populate Player Hand Data
        foreach (PuzzleHandTile handTile in playerHand)
        {
            HandTileSaveData handTileSave = new HandTileSaveData
            {
                tileTypeName = handTile.tileType.displayName,
                rotationY = handTile.rotationY,
                isFlipped = handTile.isFlipped
            };
            levelData.playerHand.Add(handTileSave);
        }

        // 6. Populate Start & End Goal Data
        if (activeStartMarker != null)
        {
            levelData.startPosition = activeStartMarker.GetComponent<GoalMarker>().goalInfo;
        }
        else
        {
            // If there's no marker, ensure we save a null or empty object so the file is valid.
            levelData.startPosition = new GoalData();
        }

        if (activeEndMarker != null)
        {
            levelData.endPosition = activeEndMarker.GetComponent<GoalMarker>().goalInfo;
        }
        else
        {
            // If there's no marker, ensure we save a null or empty object so the file is valid.
            levelData.endPosition = new GoalData();
        }

        // 7. Convert to JSON and Save to File
        string json = JsonUtility.ToJson(levelData, true); // 'true' for pretty print

        // Define the path to our new "Levels" subfolder within Assets
        string directoryPath = Path.Combine(Application.dataPath, "Levels");

        // Ensure the directory exists. If not, create it.
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Combine the directory path with the desired filename
        string path = Path.Combine(directoryPath, filename + ".json");

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"<color=lime>Level saved successfully to: {path}</color>");

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save level to {path}. Error: {e.Message}");
        }
    }

    /// <summary>
    /// Loads level data from a JSON file. For now, it just loads it into memory
    /// and prints it to the console to verify it works.
    /// </summary>


    public void LoadLevelFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Debug.LogError($"[LevelEditorManager] Load failed: File not found or path is invalid: {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            LevelData loadedData = JsonUtility.FromJson<LevelData>(json);

            if (loadedData != null)
            {
                currentLoadedLevelData = loadedData;

                // Clear history for a new level.
                if (HistoryManager.Instance != null)
                {
                    HistoryManager.Instance.ClearHistory();
                }

                // Convert the loaded file data into a snapshot to reconstruct the initial state.
                GameStateSnapshot initialSnapshot = CreateSnapshotFromLevelData(loadedData);

                // Save the initial state immediately after creating it (before reconstruction starts).
                if (HistoryManager.Instance != null)
                {
                    HistoryManager.Instance.SaveState(initialSnapshot); // Pass the initial snapshot
                }


                StartCoroutine(ReconstructLevelFromDataCoroutine(initialSnapshot)); // Start reconstruction

                Debug.Log($"<color=cyan>Successfully loaded level data from: {path}</color>");

            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load or parse level from {path}. Error: {e.Message}");
        }








    }



    private GameStateSnapshot CreateSnapshotFromLevelData(LevelData data)
    {
        GameStateSnapshot snapshot = new GameStateSnapshot();
        snapshot.tileStates = data.tiles;
        snapshot.collectibleStates = data.collectibles;
        snapshot.lockedRowsState = data.lockedRows;
        snapshot.boatMovementPoints = data.maxMoves;
        snapshot.boatStarsCollected = 0;
        snapshot.boatPosition = data.startPosition;
        snapshot.playerHandState = data.playerHand;
        snapshot.undoCount = 0;
        return snapshot;
    }


    /// Reconstructs the level using the last successfully loaded level data.
    public void RestartCurrentLevel()
    {
        // First, check if we actually have a level loaded to restart.
        if (currentLoadedLevelData != null)
        {
            Debug.Log($"<color=yellow>Restarting level...</color>");

            // Clear history and save the initial state (just like a new load)
            if (HistoryManager.Instance != null)
            {
                HistoryManager.Instance.ClearHistory();
            }
            GameStateSnapshot initialSnapshot = CreateSnapshotFromLevelData(currentLoadedLevelData);
            if (HistoryManager.Instance != null)
            {
                HistoryManager.Instance.SaveState(initialSnapshot);
            }

            StartCoroutine(ReconstructLevelFromDataCoroutine(initialSnapshot));
        }
        else
        {
            Debug.LogWarning("Cannot restart: No level has been loaded yet.");
        }
    }






    // It helps us find a TileType from our library using its name.
    private TileType FindTileTypeByName(string name)
    {
        if (gridManager.bagManager.tileLibrary == null) return null;
        return gridManager.bagManager.tileLibrary.tileTypes.FirstOrDefault(t => t.displayName == name);
    }

    public IEnumerator ReconstructLevelFromDataCoroutine(GameStateSnapshot snapshot, bool isUndoAction = false)
    {
        Debug.Log("Beginning level reconstruction from snapshot...");

        // 1. Clear the current scene state
        boatManager.ClearAllBoats();
        if (activeStartMarker != null) Destroy(activeStartMarker);
        if (activeEndMarker != null) Destroy(activeEndMarker);
        playerHand.Clear();
        ClearPaletteHighlight();
        ClearHandHighlight();
        startTile = null;
        startBank = null;
        startSnapPointIndex = -1;
        endTile = null;
        endBank = null;
        // Clear goal markers, we will restore them from the CURRENT level data later
        foreach (var marker in FindObjectsByType<GoalMarker>(FindObjectsSortMode.None)) { Destroy(marker.gameObject); }

        // We use the grid dimensions FROM THE SNAPSHOT.
        List<Coroutine> gridAnimations = gridManager.CreateGridFromEditor(currentLoadedLevelData.gridWidth, currentLoadedLevelData.gridHeight, snapshot.tileStates);


        // --- THIS IS OUR FADE-OUT/FADE-IN TRANSITION (Optional but cool) ---


        // Now, wait for every single one of those animations to complete.
        // Debug.Log($"Waiting for {gridAnimations.Count} tile animations to finish...");
        if (gridAnimations != null)
        {
            foreach (var anim in gridAnimations)
            {
                if (anim != null) yield return anim;
            }
        }
        // Debug.Log("All tile animations complete. Proceeding...");

        riverBankManager.GenerateBanksForGrid();
        riverControls.GenerateArrowsForGrid();
        //riverControls.SetLockStates(data.lockedRows); 
        riverControls.SetLockStatesFromInts(snapshot.lockedRowsState);

        // 3. Place all the tiles and add their editor components
        for (int y = 0; y < currentLoadedLevelData.gridHeight; y++)
        {
            for (int x = 0; x < currentLoadedLevelData.gridWidth; x++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null)
                {
                    var editorTile = tile.gameObject.AddComponent<EditorGridTile>();
                    editorTile.editorManager = this;
                    editorTile.tileInstance = tile;
                    UpdateBlockerVisual(tile); // Update blocker visuals after creation
                }
            }
        }

        // 4. Initialize each tile's state from the loaded data
        // THIS LOOP IS NOW ONLY HERE ONCE.
        // foreach (var tileData in data.tiles)
        // {
        //     TileInstance tileInstance = gridManager.GetTileAt(tileData.gridX, tileData.gridY);
        //     TileType type = FindTileTypeByName(tileData.tileTypeName);

        //     if (tileInstance != null && type != null)
        //     {
        //         tileInstance.GetComponent<PathVisualizer>()?.CleanUpPaths();
        //         tileInstance.transform.rotation = Quaternion.Euler(0, tileData.rotationY, tileData.isFlipped ? 180f : 0);
        //         gridManager.InitializeTile(tileInstance, type, tileData.isFlipped);

        //         // Directly set the blocker data and update the visual
        //         tileInstance.IsHardBlocker = tileData.isHardBlocker;
        //         UpdateBlockerVisual(tileInstance);
        //     }
        // }

        // 5. Place Collectibles
        foreach (var collectibleData in snapshot.collectibleStates)
        {
            TileInstance tile = gridManager.GetTileAt(collectibleData.gridX, collectibleData.gridY);
            if (tile != null)
            {
                PlaceOrRemoveCollectible(tile, collectibleData.type, collectibleData.value);
            }
        }

        // 6. Rebuild the Player's Hand data and visuals
        foreach (var handTileData in snapshot.playerHandState)
        {
            TileType type = FindTileTypeByName(handTileData.tileTypeName);
            if (type != null)
            {
                playerHand.Add(new PuzzleHandTile(type)
                {
                    rotationY = handTileData.rotationY,
                    isFlipped = handTileData.isFlipped
                });
            }
        }
        RedrawHandPalette();
        // Force the game into Puzzle Mode with the hand we just loaded.
        ApplyHandToBag();

        // // 7. Update UI Fields
        // widthInput.text = data.gridWidth.ToString();
        // heightInput.text = data.gridHeight.ToString();
        // maxMovesInput.text = data.maxMoves.ToString();

        // // 8. Place Start and End Markers by reading the new, robust GoalData structure
        // if (data.startPosition != null)
        // {
        //     if (data.startPosition.isBankGoal)
        //     {
        //         // Load a bank start position
        //         SetStartPosition(null, data.startPosition.bankSide, null);
        //     }
        //     else if (data.startPosition.tileX != -1)
        //     {
        //         // Load a tile start position
        //         TileInstance tile = gridManager.GetTileAt(data.startPosition.tileX, data.startPosition.tileY);
        //         Debug.Log($"[Reconstruct] Reading snapPointIndex from JSON: {data.startPosition.snapPointIndex}. Calling SetStartPosition...");
        //         SetStartPosition(tile, null, data.startPosition.snapPointIndex);
        //     }
        // }

        // if (data.endPosition != null)
        // {
        //     if (data.endPosition.isBankGoal)
        //     {
        //         // Load a bank end position
        //         SetEndPosition(null, data.endPosition.bankSide);
        //     }
        //     else if (data.endPosition.tileX != -1)
        //     {
        //         // Load a tile end position
        //         TileInstance tile = gridManager.GetTileAt(data.endPosition.tileX, data.endPosition.tileY);
        //         SetEndPosition(tile, null);
        //     }
        // }

        // 5. Re-place Start and End Markers FROM THE ORIGINAL LEVEL DATA
        // The goals don't move, so we restore them from 'currentLoadedLevelData'.
        var startGoalData = currentLoadedLevelData.startPosition;
        if (startGoalData != null)
        {
            if (startGoalData.isBankGoal) SetStartPosition(null, startGoalData.bankSide, null);
            else if (startGoalData.tileX != -1) SetStartPosition(gridManager.GetTileAt(startGoalData.tileX, startGoalData.tileY), null, startGoalData.snapPointIndex);
        }
        var endGoalData = currentLoadedLevelData.endPosition;
        if (endGoalData != null)
        {
            if (endGoalData.isBankGoal) SetEndPosition(null, endGoalData.bankSide);
            else if (endGoalData.tileX != -1) SetEndPosition(gridManager.GetTileAt(endGoalData.tileX, endGoalData.tileY), null);
        }

        // 6. Spawn the boat and RESTORE ITS STATE
        BoatController boat = boatManager.SpawnBoatWithoutPositioning(); // Spawn it without positioning
        if (boat != null && snapshot.boatPosition != null)
        {
            // Restore dynamic values
            boat.currentMovementPoints = snapshot.boatMovementPoints;
            boat.SetCollectedStars(snapshot.boatStarsCollected);

            // Restore position
            if (snapshot.boatPosition.isBankGoal)
            {
                boat.MoveToBank(snapshot.boatPosition.bankSide);
            }
            else
            {
                TileInstance boatTile = gridManager.GetTileAt(snapshot.boatPosition.tileX, snapshot.boatPosition.tileY);
                boat.PlaceOnTile(boatTile, snapshot.boatPosition.snapPointIndex);
            }
        }


        // 9. Spawn the test boat in its starting position
        // This now works because Step 2 fixed SetStartPosition to update these variables!
        // Debug.Log($"[Reconstruct] Spawning boat with startSnapPointIndex: {startSnapPointIndex}");

        //Debug.Break();

        // boatManager.SpawnBoatAtLevelStart(startTile, startSnapPointIndex, startBank);
        GameManager.Instance.InitializeLevel(currentLoadedLevelData, activeEndMarker);


        // // Clear any history from a previous level and save the initial state.
        // if (HistoryManager.Instance != null)
        // {
        //     HistoryManager.Instance.ClearHistory();
        //     HistoryManager.Instance.SaveState();
        // }


        // After everything is visually in place, run the logic finalization routine.
        yield return StartCoroutine(FinalizeStateReconstruction(snapshot, isUndoAction));

        Debug.Log("<color=cyan>Level reconstruction from snapshot complete.</color>");
    }








    /// This coroutine runs AFTER the level has been visually reconstructed.
    /// It re-initializes game logic, re-selects the boat, and checks for win/loss/collectible conditions.
    private IEnumerator FinalizeStateReconstruction(GameStateSnapshot snapshot, bool isUndoAction)
    {
        // Wait a single frame to ensure all GameObjects from the reconstruction have been fully initialized.
        yield return null;

        // Find the boat in the scene.
        var boat = boatManager.GetPlayerBoats().FirstOrDefault();
        if (boat == null)
        {
            Debug.LogWarning("[FinalizeState] Could not find boat after reconstruction. Aborting finalization.");
            yield break;
        }

        // --- LOGIC RE-EVALUATION ---

        // 1. Tell the boat to check if it landed on a collectible.
        boat.CheckForCollectibleOnCurrentTile();

        // 2. Re-select the boat. This will lift it, start the bobbing animation, and find its valid moves.
        // This is crucial for player experience, so it feels like a proper turn state.
        if (isUndoAction)
        {
            // Only re-select the boat if this was triggered by an Undo action.
            boat.SelectBoat();
        }
        
        // We need to wait for the boat's "lift" animation to finish before checking for game over,
        // as some checks might depend on the boat being in the correct state.
        // The LiftAndBobBoat coroutine has a hardcoded duration of 0.3f. Let's wait for that.
        yield return new WaitForSeconds(0.3f);

        // 3. Manually trigger the GameManager to evaluate the new state.
        // This will now correctly detect a win (on goal tile) or loss (out of moves).
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EvaluateGameStateAfterMove(boat);
        }
        
        Debug.Log("<color=lime>[FinalizeState]</color> Post-undo logic executed. Boat selected, game state evaluated.");
    }










}






