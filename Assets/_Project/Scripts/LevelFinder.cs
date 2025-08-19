/* LevelFinder.cs */
using System.Collections.Generic;
using System.IO;
using System.Linq; // We need this for the OrderBy().ThenBy() sorting.
using UnityEngine;

// This is a static utility class. It doesn't inherit from MonoBehaviour
// and is not attached to any GameObject.
public static class LevelFinder
{
    private static readonly string levelsSubfolderName = "Levels";

    // This is the one public method everyone will call.
    // It finds, parses, sorts, and returns all valid level files.
    public static List<LevelInfo> GetAllLevels()
    {
        List<LevelInfo> foundLevels = new List<LevelInfo>();

        string fullPath = Path.Combine(Application.dataPath, levelsSubfolderName);

        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"[LevelFinder] The levels directory does not exist at: {fullPath}. Cannot find any levels.");
            return foundLevels; // Return an empty list
        }

        // Get all .json files from the directory.
        string[] files = Directory.GetFiles(fullPath, "*.json", SearchOption.AllDirectories);

        foreach (string filePath in files)
        {
            // Create a new LevelInfo object, which parses the filename in its constructor.
            var levelInfo = new LevelInfo(filePath);
            foundLevels.Add(levelInfo);

        }

        // --- Sort the list ---
        // First, sort by World number.
        // For ties in world number, sort by Level number.
        List<LevelInfo> sortedLevels = foundLevels
            .OrderBy(level => level.WorldNumber)
            .ThenBy(level => level.LevelNumber)
            .ToList();

        return sortedLevels;
    }
}