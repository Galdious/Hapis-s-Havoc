/* LevelInfo.cs */

// This is a simple class, not a MonoBehaviour.
// It just holds data in a structured way.
public class LevelInfo
{
    public int WorldNumber { get; private set; }
    public int LevelNumber { get; private set; }
    public string Description { get; private set; }
    public string FilePath { get; private set; }

    // The constructor takes the raw file path and does all the parsing work.
    public LevelInfo(string filePath)
    {
        this.FilePath = filePath;

        // Get just the filename, without the folder path or the ".json" extension.
        // e.g., "C:/.../Levels/01_01_TheFirstStep.json" -> "01_01_TheFirstStep"
        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

        // Split the name by the underscore character.
        // e.g., "01_01_TheFirstStep" -> ["01", "01", "TheFirstStep"]
        string[] parts = fileName.Split('_');

        // --- Robust Parsing ---
        // We check if the filename is in the correct format. If not, we set default
        // values and can handle it gracefully later.
        if (parts.Length >= 3 && int.TryParse(parts[0], out int world) && int.TryParse(parts[1], out int level))
        {
            this.WorldNumber = world;
            this.LevelNumber = level;

            // Join the remaining parts back together to form the description.
            // This allows for descriptions with underscores, e.g., "01_02_A_Tricky_Turn"
            this.Description = string.Join(" ", parts, 2, parts.Length - 2);
        }
        else
        {
            // If the filename is malformed (e.g., "MyTestLevel.json"), we handle it.
            this.WorldNumber = 0; // Default to world 0
            this.LevelNumber = 0;
            this.Description = fileName; // Use the whole filename as the description
            UnityEngine.Debug.LogWarning($"[LevelFinder] Could not parse level file name: {fileName}. Please use the 'WW_LL_Description.json' format.");
        }
    }
}