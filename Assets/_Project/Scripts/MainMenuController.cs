/* MainMenuController.cs (Updated) */
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class MainMenuController : MonoBehaviour
{
    // This function will be called by the "Level Select" button
    public void GoToLevelSelect()
    {
        Debug.Log("Loading LevelSelect scene...");
        SceneManager.LoadScene("LevelSelect");
    }

    // This function will be called by the "Level Editor" button
    public void GoToLevelEditor()
    {
        Debug.Log("Loading LevelEditor scene...");
        // CRUCIAL: We ensure the instruction is null so it just opens the empty editor.
        LevelSelectManager.LevelToLoad = null;
        SceneManager.LoadScene("LevelEditor");
    }

    // This is the new "Smart Play" button logic.
    public void GoToNextAvailableLevel()
    {
        // 1. Get the sorted list of all levels in the game.
        List<LevelInfo> allLevels = LevelFinder.GetAllLevels();
        if (allLevels.Count == 0)
        {
            Debug.LogError("Play button failed: No levels found!");
            return;
        }

        // 2. Find the first level that hasn't been completed (has 0 stars).
        LevelInfo nextLevel = null;
        foreach (var level in allLevels)
        {
            string levelKey = $"Level_{level.WorldNumber:00}_{level.LevelNumber:00}_Stars";
            if (PlayerPrefs.GetInt(levelKey, 0) == 0)
            {
                // This is the first level we haven't perfected (or played).
                nextLevel = level;
                break; // Stop searching
            }
        }

        // 3. If we searched and found ALL levels are complete, just load the last level.
        if (nextLevel == null)
        {
            nextLevel = allLevels.LastOrDefault();
        }

        // 4. Load the level.
        if (nextLevel != null)
        {
            Debug.Log($"'Play' button clicked. Loading next available level: {nextLevel.Description}");
            LevelSelectManager.LevelToLoad = nextLevel.FilePath;
            SceneManager.LoadScene("LevelEditor"); // Your main game scene
        }
    }


    public void GoToEndlessMode()
    {
        Debug.Log("Loading Endless Mode...");

        // --- NEW LOGIC: Check if a save file exists ---
        if (PlayerPrefs.HasKey("EndlessSaveFile"))
        {
            // If a save exists, send the RESUME instruction.
            Debug.Log("Save file found. Resuming endless run.");
            LevelSelectManager.LevelToLoad = "RESUME_ENDLESS_MODE";
        }
        else
        {
            // Otherwise, start a new run as normal.
            Debug.Log("No save file found. Starting a new endless run.");
            LevelSelectManager.LevelToLoad = "ENDLESS_MODE";
        }

        SceneManager.LoadScene("LevelEditor"); // Or your main game scene name
    }



    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }









}