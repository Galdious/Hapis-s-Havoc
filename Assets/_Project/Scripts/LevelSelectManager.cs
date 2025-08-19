/* LevelSelectManager.cs (Final Version) */
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class LevelSelectManager : MonoBehaviour
{
    public static string LevelToLoad { get; set; }
    private List<LevelInfo> allLevelData;

    void Start()
    {
        allLevelData = LevelFinder.GetAllLevels();
        LevelMarker[] markersInScene = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);

        foreach (LevelMarker marker in markersInScene)
        {
            marker.Initialize(this);

            // --- NEW, MORE ROBUST UNLOCK LOGIC ---
            bool isUnlocked = false;

            // The very first level (World 1, Level 1) is always unlocked by default.
            if (marker.worldNumber == 1 && marker.levelNumber == 1)
            {
                isUnlocked = true;
            }
            else
            {
                // For any other level, we need to find its predecessor.
                LevelInfo previousLevel = null;
                if (marker.levelNumber > 1) // Case 1: The previous level is in the same world.
                {
                    previousLevel = GetLevelInfo(marker.worldNumber, marker.levelNumber - 1);
                }
                else if (marker.worldNumber > 1) // Case 2: This is the first level of a new world.
                {
                    int prevWorldNum = marker.worldNumber - 1;
                    // Find the level with the highest number in the previous world.
                    int lastLevelInPrevWorld = allLevelData
                        .Where(l => l.WorldNumber == prevWorldNum)
                        .Max(l => l.LevelNumber);
                    previousLevel = GetLevelInfo(prevWorldNum, lastLevelInPrevWorld);
                }

                // Now, check if the predecessor level has been completed (has at least 1 star).
                if (previousLevel != null)
                {
                    string prevLevelKey = $"Level_{previousLevel.WorldNumber:00}_{previousLevel.LevelNumber:00}_Stars";
                    if (PlayerPrefs.GetInt(prevLevelKey, 0) > 0)
                    {
                        isUnlocked = true;
                    }
                }
            }

            // The rest of the logic is the same: get star count and update visuals.
            string currentLevelKey = $"Level_{marker.worldNumber:00}_{marker.levelNumber:00}_Stars";
            int starCount = PlayerPrefs.GetInt(currentLevelKey, 0);

            marker.UpdateVisuals(starCount, isUnlocked);
        }

    }

    // --- THIS METHOD IS CALLED BY LevelMarker.cs ---
    public void OnMarkerClicked(LevelMarker clickedMarker)
    {
        LevelInfo levelToLoad = GetLevelInfo(clickedMarker.worldNumber, clickedMarker.levelNumber);
        if (levelToLoad != null)
        {
            Debug.Log($"Loading level: {levelToLoad.FilePath}");
            LevelToLoad = levelToLoad.FilePath;
            SceneManager.LoadScene("LevelEditor"); // Or your main game scene name
        }
    }
    
    private LevelInfo GetLevelInfo(int world, int level)
    {
        return allLevelData.FirstOrDefault(l => l.WorldNumber == world && l.LevelNumber == level);
    }
}