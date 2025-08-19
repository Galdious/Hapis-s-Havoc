/* LevelLoaderUI.cs (Updated) */
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq; // We need this for the .Select() method

public class LevelLoaderUI : MonoBehaviour
{
    [Header("Scene References")]
    [Tooltip("Drag the main LevelEditorManager GameObject here.")]
    [SerializeField] private LevelEditorManager editorManager;

    // --- Private Fields ---
    private TMP_Dropdown levelDropdown;
    // This list will now store the full LevelInfo object for each entry.
    private List<LevelInfo> levelDataList = new List<LevelInfo>();

    void Start()
    {
        levelDropdown = GetComponent<TMP_Dropdown>();
        if (levelDropdown == null || editorManager == null)
        {
            Debug.LogError("[LevelLoaderUI] A required reference is missing!", this);
            return;
        }

        PopulateDropdown();
        levelDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    void PopulateDropdown()
    {
        levelDropdown.ClearOptions();
        levelDataList.Clear();

        // Add a default, non-selectable option at the top.
        levelDropdown.options.Add(new TMP_Dropdown.OptionData("Select a Level..."));
        levelDataList.Add(null); // Add a null entry for the default option.

        // --- CORE CHANGE ---
        // Get the complete, sorted list of levels from our new utility.
        List<LevelInfo> allLevels = LevelFinder.GetAllLevels();

        if (allLevels.Count == 0)
        {
            Debug.LogWarning("[LevelLoaderUI] LevelFinder returned no levels. Is your 'Levels' folder empty?");
            return;
        }

        // Store the full LevelInfo objects.
        levelDataList.AddRange(allLevels);

        // Create the user-friendly display names for the dropdown.
        // e.g., "World 1-1: The First Step"
        List<string> displayNames = allLevels.Select(level =>
            $"World {level.WorldNumber}-{level.LevelNumber}: {level.Description}"
        ).ToList();

        levelDropdown.AddOptions(displayNames);
    }

    private void OnDropdownValueChanged(int index)
    {
        // Ignore the first "Select..." option.
        if (index == 0)
        {
            UIManager.Instance.UpdateCurrentLevelName("No Level Loaded");
            return;
        }

        // Get the full LevelInfo object corresponding to the selection.
        LevelInfo selectedLevel = levelDataList[index];

        if (selectedLevel != null)
        {
            // Tell the editor manager to load this specific file.
            editorManager.LoadLevelFromFile(selectedLevel.FilePath);

            // Update the display text to show what's loaded.
            UIManager.Instance.UpdateCurrentLevelName(levelDropdown.options[index].text);
            UIManager.Instance.SetEditorRestartButtonInteractable(true);
        }
    }
}