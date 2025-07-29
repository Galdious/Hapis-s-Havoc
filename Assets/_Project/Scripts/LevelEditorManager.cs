using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class LevelEditorManager : MonoBehaviour
{
    private enum EditorTool { Paint, Rotate, Flip, AddToHand, RemoveFromHand }
    private EditorTool currentTool = EditorTool.Paint; // Default to painting


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

    private Dictionary<TileType, int> playerHand = new Dictionary<TileType, int>();



    // This class holds our "brush" information
    private class EditorBrush
    {
        public TileType tileType;
        public float currentRotationY = 0f;
        public PaletteTile sourcePaletteTile;
    }
    private EditorBrush currentBrush;


    // This will store the original material of the highlighted palette tile
    private Dictionary<Renderer, Material> originalPaletteMaterial = new Dictionary<Renderer, Material>();
    // This will keep a reference to the GameObject we highlighted
    private GameObject currentlyHighlightedPaletteTile;





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
            gridManager.bagManager.BuildBagFromHand(playerHand);
            gridManager.isPuzzleMode = true;
            Debug.LogWarning($"PUZZLE MODE ACTIVATED. Bag now contains {gridManager.bagManager.TilesRemaining} tiles from the hand. Ejected tiles will NOT be returned.");
        }
        else
        {
            Debug.LogError("Cannot apply hand to bag: The hand is empty! Define a hand first.");
        }
    }

        public void ApplySandboxBag()
    {
        gridManager.bagManager.BuildBag(); // The original method
        gridManager.isPuzzleMode = false;
        Debug.LogWarning($"SANDBOX MODE ACTIVATED. Bag reset to full library ({gridManager.bagManager.TilesRemaining} tiles). Ejected tiles WILL be returned.");
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
            tileInstance.Initialise(ConvertPaths(type.frontPaths), false, type);

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
                // If we are in "Add to Hand" mode, we add the tile to the hand.
        if (currentTool == EditorTool.AddToHand)
        {
            AddToHand(clickedTile.myTileType);
            return; // Exit here, we don't want to select a brush.
        }



        // Palette clicks should ALWAYS select a brush, so we force it to Paint mode.
        // This is the most intuitive workflow.
        SelectPaintTool();

        // 1. First, always clear any existing highlight. This handles deselection.
        ClearPaletteHighlight();

        // 2. Check if we are clicking the tile that was already selected.
        // If so, the user wants to deselect the brush. We've already cleared the
        // highlight, so we can just set the brush to null and exit.
        if (currentBrush != null && currentBrush.sourcePaletteTile == clickedTile)
        {
            currentBrush = null;
            Debug.Log("Brush deselected.");
            return;
        }

        // 3. If we're here, it's a new selection.
        currentBrush = new EditorBrush
        {
            tileType = clickedTile.myTileType,
            sourcePaletteTile = clickedTile
        };
        Debug.Log($"Selected Brush: {currentBrush.tileType.displayName}");

        // 4. Apply the highlight to the newly clicked tile.
        HighlightPaletteTile(clickedTile.gameObject);
    }


    public void OnHandPaletteTileClicked(HandPaletteTile clickedTile)
    {
        if (currentTool == EditorTool.RemoveFromHand)
        {
            RemoveFromHand(clickedTile.myTileType);
        }
    }

    // --- ADD the core hand logic methods ---
    private void AddToHand(TileType type)
    {
        if (playerHand.ContainsKey(type))
        {
            playerHand[type]++; // Increment count
        }
        else
        {
            playerHand[type] = 1; // Add new entry
        }
        Debug.Log($"Added {type.displayName} to hand. New count: {playerHand[type]}");
        RedrawHandPalette();
    }

    private void RemoveFromHand(TileType type)
    {
        if (playerHand.ContainsKey(type))
        {
            playerHand[type]--; // Decrement count
            Debug.Log($"Removed {type.displayName} from hand. New count: {playerHand[type]}");
            if (playerHand[type] <= 0)
            {
                playerHand.Remove(type); // Remove if count is zero or less
            }
            RedrawHandPalette();
        }
    }

    private void RedrawHandPalette()
    {
        foreach (Transform child in handPaletteContainer)
        {
            Destroy(child.gameObject);
        }



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





        // Use the same positioning logic as the main palette
        float startZ = (playerHand.Count - 1) * paletteSpacing / 2f;
        int index = 0;

        foreach (var pair in playerHand.OrderBy(p => p.Key.displayName)) // Order alphabetically for consistency
        {
            TileType type = pair.Key;
            int count = pair.Value;

            Vector3 spawnPos = new Vector3(0, 0, startZ - (index * paletteSpacing));
            GameObject tileGO = Instantiate(gridManager.tilePrefab, spawnPos, Quaternion.identity);
            tileGO.transform.SetParent(handPaletteContainer, false);
            tileGO.name = "HandPalette_" + type.displayName;

            var tileInstance = tileGO.GetComponent<TileInstance>();
            tileInstance.Initialise(ConvertPaths(type.frontPaths), false, type);

            // Add the clickable component for removal
            var handTile = tileGO.AddComponent<HandPaletteTile>();
            handTile.editorManager = this;
            handTile.myTileType = type;

            // Add the count indicator if needed
            if (count > 1 && countIndicatorPrefab != null)
            {
                GameObject indicatorGO = Instantiate(countIndicatorPrefab, tileGO.transform);
                indicatorGO.transform.localPosition = new Vector3(0, 0.7f, -0.7f); // Position it top-right-ish
                var text = indicatorGO.GetComponentInChildren<TMP_Text>();
                if (text) text.text = $"x{count}";
            }
            index++;
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
        tileToPaint.Initialise(ConvertPaths(currentBrush.tileType.frontPaths), false, currentBrush.tileType);

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

    // Helper method to convert paths
    private List<TileInstance.Connection> ConvertPaths(List<Vector2Int> src)
    {
        var list = new List<TileInstance.Connection>();
        foreach (Vector2Int v in src)
            list.Add(new TileInstance.Connection { from = v.x, to = v.y });
        return list;
    }

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
            tileToFlip.Initialise(ConvertPaths(tileToFlip.originalTemplate.frontPaths), false, tileToFlip.originalTemplate);
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
    }

    private void SetButtonColor(Button btn, EditorTool tool)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = (currentTool == tool) ? toolSelectedColor : toolDefaultColor;
        btn.colors = colors;
    }



}






