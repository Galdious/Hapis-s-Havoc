using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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


    // This class holds our "brush" information
    private class EditorBrush
    {
        public TileType tileType;
        public float currentRotationY = 0f;
    }
    private EditorBrush currentBrush;

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
    
    // This is the public method our PaletteTile script calls
    public void OnPaletteTileClicked(PaletteTile clickedTile)
    {
        // Logic for selection/deselection
        if (currentBrush != null && currentBrush.tileType == clickedTile.myTileType)
        {
            currentBrush = null;
            Debug.Log("Brush deselected.");
            // We can add code here to remove a visual highlight
        }
        else
        {
            currentBrush = new EditorBrush { tileType = clickedTile.myTileType, currentRotationY = 0f };
            Debug.Log($"Selected Brush: {currentBrush.tileType.displayName}");
            // We can add code here to add a visual highlight to the clicked tile
        }
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
}






