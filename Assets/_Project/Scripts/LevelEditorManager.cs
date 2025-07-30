using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; 

public class LevelEditorManager : MonoBehaviour
{
    private enum EditorTool { Paint, Rotate, Flip, AddToHand, RemoveFromHand, SetStart, SetEnd }
    private EditorTool currentTool = EditorTool.Paint; // Default to painting
    private enum EditorBagMode { Sandbox, Hand }
    private EditorBagMode currentBagMode = EditorBagMode.Sandbox;


    [Header("Goal Settings")]
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

    [Header("Editor Tools")]
    public Button paintToolButton;
    public Button rotateToolButton;
    public Button flipToolButton;
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
            // 1. Always clear any previous selection first.
            ClearHandHighlight();

            // 2. Find the specific instance in our hand data.
            selectedHandTileForPush = playerHand.FirstOrDefault(t => t.tileType == clickedTile.myTileType);

            if (selectedHandTileForPush != null)
            {
                Debug.Log($"SELECTED '{clickedTile.myTileType.displayName}' for the next push.");
                // 3. Apply the highlight to the visual tile we just clicked.
                HighlightPaletteTile(clickedTile.gameObject); 
                // And store a reference to it for clearing later.
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
        // Get a list of all UNIQUE tile types currently in the hand.
        var uniqueTypesInHand = playerHand.Select(t => t.tileType).Distinct().ToList();


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




        // czy usuwac var?
        // Use the same positioning logic as the main palette
        var groupedHand = playerHand.GroupBy(t => t.tileType);
        float startZ = (groupedHand.Count() - 1) * paletteSpacing / 2f;
        int index = 0;

            foreach (TileType type in uniqueTypesInHand.OrderBy(t => t.displayName))
    {
        // Find the first data object for this type to use as our visual model.
        // This ensures the visual shows the rotation/flip of at least one of the tiles of this type.
        PuzzleHandTile representativeTile = playerHand.First(t => t.tileType == type);

        // Calculate the spawn position for this tile in the column.
        Vector3 spawnPos = new Vector3(0, 0, startZ - (index * paletteSpacing));
        
        // Instantiate the tile prefab and set its state from our data model.
        GameObject tileGO = Instantiate(gridManager.tilePrefab, spawnPos, Quaternion.Euler(0, representativeTile.rotationY, representativeTile.isFlipped ? 180f : 0f));
        tileGO.transform.SetParent(handPaletteContainer, false);
        tileGO.name = "HandPalette_" + type.displayName;

        // Initialize its paths based on its current flip state.
        var tileInstance = tileGO.GetComponent<TileInstance>();
        gridManager.InitializeTile(tileInstance, type, representativeTile.isFlipped);

        // Add the component that makes it clickable.
        var handTile = tileGO.AddComponent<HandPaletteTile>();
        handTile.editorManager = this;
        handTile.myTileType = type;
        
        // Add the text counter.
        if (countIndicatorPrefab != null)
        {
            GameObject indicatorGO = Instantiate(countIndicatorPrefab, tileGO.transform);
            indicatorGO.transform.localPosition = new Vector3(0, 0.7f, -0.7f);
            var text = indicatorGO.GetComponentInChildren<TMP_Text>();
            if (text)
            {
                // Count how many of this type are in our data list.
                int countInHand = playerHand.Count(t => t.tileType == type);
                text.text = $"x{countInHand}";
                
                // Store a reference to this text component so we can update it later
                // without having to redraw everything.
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
            SetStartPosition(null, side);
        }
    
}

// And finally, the logic to place the end marker.
private void SetEndPosition(TileInstance tile = null, RiverBankManager.BankSide? side = null)
{
    if (activeEndMarker != null) Destroy(activeEndMarker);

    if (tile != null)
    {
        endTile = tile;
        endBank = null;
        activeEndMarker = Instantiate(endMarkerPrefab, tile.transform.position, Quaternion.identity);
        Debug.Log($"End point set on tile {tile.name}.");
    }
    else if (side.HasValue)
    {
        endTile = null;
        endBank = side.Value;
        Transform bankTransform = riverBankManager.GetBankGameObject(side.Value).transform;
        activeEndMarker = Instantiate(endMarkerPrefab, bankTransform.position, Quaternion.identity);
        Debug.Log($"End point set on {side.Value} bank.");
    }
}



private void SetStartPosition(TileInstance tile = null, RiverBankManager.BankSide? side = null)
{
    // Clear the old marker first
    if (activeStartMarker != null) Destroy(activeStartMarker);

    // Scenario 1: A TILE was clicked
    if (tile != null)
    {
        // Find the closest snap point on the clicked tile (this logic is still needed for tiles)
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 clickWorldPos = hit.point;
            float minDistance = float.MaxValue;
            int closestSnapIndex = -1;

            for (int i = 0; i < tile.snapPoints.Length; i++)
            {
                if (tile.snapPoints[i] != null)
                {
                    float distance = Vector3.Distance(clickWorldPos, tile.snapPoints[i].position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestSnapIndex = i;
                    }
                }
            }

            if (closestSnapIndex != -1)
            {
                // Update state for a TILE start
                startTile = tile;
                startSnapPointIndex = closestSnapIndex;
                startBank = null;

                // Place marker on the specific snap point
                activeStartMarker = Instantiate(startMarkerPrefab, tile.snapPoints[closestSnapIndex]);
                Debug.Log($"Start point set on TILE {tile.name}, snap point {closestSnapIndex}.");
            }
        }
    }
    // Scenario 2: A BANK was clicked
    else if (side.HasValue)
    {
        // Update state for a BANK start
        startTile = null;
        startSnapPointIndex = -1;
        startBank = side.Value;
        // We can just store the BankSide. The PuzzleGameManager will figure out the spawn point later.
            // For the visual marker, we'll place it in the middle of the bank.
            Transform bankTransform = riverBankManager.GetBankGameObject(side.Value).transform;
        
        activeStartMarker = Instantiate(startMarkerPrefab, bankTransform.position, Quaternion.identity);
        Debug.Log($"Start point set on BANK {side.Value}.");
        
        // Storing the actual spawn transform isn't necessary for the editor,
        // but we can store the side for when we save the level.
        // Let's modify the class variable to store the side instead of the transform.
        // startBankSpawn = bankTransform; <--- We can improve this.
    }
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
        // --- THE NEW, SIMPLIFIED LOGIC ---
        
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



}






