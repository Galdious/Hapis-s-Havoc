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

        // 1. Load all "TextAsset" files from the "Resources/Levels" folder.
        //    Unity automatically knows how to handle .json, .txt, .xml as TextAssets.
        TextAsset[] levelAssets = Resources.LoadAll<TextAsset>(levelsSubfolderName);

        if (levelAssets.Length == 0)
        {
            Debug.LogError($"[LevelFinder] Found no level files in the 'Resources/{levelsSubfolderName}' folder. Make sure your levels are there.");
            return foundLevels; // Return an empty list
        }

        foreach (TextAsset levelAsset in levelAssets)
        {
            // 2. The LevelInfo constructor needs a "path-like" string to parse the name.
            //    We can give it the resource path, which is "Levels/LEVEL_NAME".
            //    The constructor will correctly parse the name from this string.
            //    We also store this resource path so we can load it later.
            string resourcePath = $"{levelsSubfolderName}/{levelAsset.name}";
            var levelInfo = new LevelInfo(resourcePath);
            foundLevels.Add(levelInfo);
        }

        // 3. Sort the list (this logic is unchanged and still works perfectly).
        List<LevelInfo> sortedLevels = foundLevels
            .OrderBy(level => level.WorldNumber)
            .ThenBy(level => level.LevelNumber)
            .ToList();

        return sortedLevels;
    }

}