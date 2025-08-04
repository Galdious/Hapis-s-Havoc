/*
 *  LevelLoaderUI.cs
 *  ---------------------------------------------------------------
 *  Manages the level loading dropdown in the editor.
 *  - Automatically finds all .json files in the specified levels folder.
 *  - Populates a TMPro Dropdown with formatted level names.
 *  - Handles future folder structures (e.g., "Worlds/World1/level.json").
 *  - Tells the LevelEditorManager which file to load when a selection is made.
 */

using UnityEngine;
using TMPro;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class LevelLoaderUI : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Drag the main LevelEditorManager GameObject here.")]
    [SerializeField] private LevelEditorManager editorManager;

    [Tooltip("Drag the 'CurrentLevel_Text' UI element here.")]
    [SerializeField] private TMP_Text currentLevelText;

    [Header("Settings")]
    [Tooltip("The name of the subfolder inside 'Assets' where levels are stored.")]
    [SerializeField] private string levelsSubfolderName = "Levels";

    // --- Private Fields ---
    private TMP_Dropdown levelDropdown;
    // This list will store the full file path for each entry in the dropdown.
    private List<string> levelFilePaths = new List<string>();

    void Start()
    {
        levelDropdown = GetComponent<TMP_Dropdown>();
        if (levelDropdown == null)
        {
            Debug.LogError("[LevelLoaderUI] Could not find the Dropdown component on this GameObject!", this);
            return;
        }

        if (editorManager == null)
        {
            Debug.LogError("[LevelLoaderUI] The LevelEditorManager reference is not set in the Inspector!", this);
            return;
        }

        PopulateDropdown();

        // Add a listener that calls our method whenever the user picks a new option
        levelDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// Finds all level files and fills the dropdown with their names.
    /// </summary>
    void PopulateDropdown()
    {
        levelDropdown.ClearOptions();
        levelFilePaths.Clear();

        // Add a default, non-selectable option at the top.
        levelDropdown.options.Add(new TMP_Dropdown.OptionData("Select a Level..."));
        levelFilePaths.Add(null); // Add a null path for the default option.

        string fullPath = Path.Combine(Application.dataPath, levelsSubfolderName);

        if (!Directory.Exists(fullPath))
        {
            Debug.LogWarning($"[LevelLoaderUI] The levels directory does not exist at: {fullPath}. Creating it now.");
            Directory.CreateDirectory(fullPath);
            return; // No files to load yet.
        }

        // Get all .json files, searching in all subdirectories (for our future "Worlds" feature)
        string[] files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);

        List<string> displayNames = new List<string>();

        foreach (string filePath in files)
        {
            // Store the full path so we know exactly which file to load.
            levelFilePaths.Add(filePath);

            // --- Create a user-friendly display name ---
            string levelName = Path.GetFileNameWithoutExtension(filePath);
            DirectoryInfo parentDir = Directory.GetParent(filePath);

            // If the level is inside a subfolder (like "World1"), prepend the folder name.
            if (parentDir.Name != levelsSubfolderName)
            {
                displayNames.Add($"{parentDir.Name} / {levelName}");
            }
            else
            {
                displayNames.Add(levelName);
            }
        }
        
        levelDropdown.AddOptions(displayNames);
    }

    /// <summary>
    /// Called when the user selects an item from the dropdown.
    /// </summary>
    /// <param name="index">The index of the selected option.</param>
    private void OnDropdownValueChanged(int index)
    {
        // Ignore the first "Select..." option.
        if (index == 0)
        {
            if (currentLevelText != null) currentLevelText.text = "No Level Loaded";
            return;
        }

        // Get the full file path corresponding to the selection.
        string selectedPath = levelFilePaths[index];

        if (!string.IsNullOrEmpty(selectedPath))
        {
            // Tell the editor manager to load this specific file.
            editorManager.LoadLevelFromFile(selectedPath);

            // Update the display text to show what's loaded.
            if (currentLevelText != null)
            {
                currentLevelText.text = $"{levelDropdown.options[index].text}";
            }
        }
    }
}