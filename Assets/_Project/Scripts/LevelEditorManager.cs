using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class LevelEditorManager : MonoBehaviour
{
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

        widthInput.text = "6";
        heightInput.text = "6";
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

        // 2. Generate the core grid first
        gridManager.CreateGridFromEditor(width, height);


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


    // This is called by EditorGridTile.cs when a grid tile is clicked
    public void OnGridTileClicked(TileInstance tileToPaint)
    {
        if (currentBrush == null)
        {
            Debug.Log("No brush selected. Click a tile from the palette on the left.");
            return;
        }

        if (tileToPaint == null) return;

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















}






