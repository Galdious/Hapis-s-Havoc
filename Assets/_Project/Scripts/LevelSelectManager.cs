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
            // --- INITIALIZE THE MARKER ---
            // Pass a reference of this manager to each marker.
            marker.Initialize(this);

            // --- The rest of the logic is the same ---
            bool isUnlocked = (marker.worldNumber == 1 && marker.levelNumber == 1);
            if (!isUnlocked)
            {
                LevelInfo previousLevel = GetLevelInfo(marker.worldNumber, marker.levelNumber - 1);
                if (previousLevel != null)
                {
                    string prevLevelKey = $"Level_{previousLevel.WorldNumber:00}_{previousLevel.LevelNumber:00}_Stars";
                    if (PlayerPrefs.GetInt(prevLevelKey, 0) > 0)
                    {
                        isUnlocked = true;
                    }
                }
            }
            
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