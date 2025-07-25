using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        if(boatManager.GetPlayerBoats().Count > 0)
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

        // 5. Finally, hide the setup panel
        gridSetupPanel.SetActive(false);
    }
}