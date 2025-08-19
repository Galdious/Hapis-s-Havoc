/* PlayerPrefsEditor.cs */
using UnityEditor; // This is needed for editor scripting
using UnityEngine;

public class PlayerPrefsEditor
{
    // This creates a new menu item at the top of the Unity Editor.
    [MenuItem("Hapi/Clear Player Progress (PlayerPrefs)")]
    private static void ClearPlayerPrefs()
    {
        // This command deletes every single key and value from PlayerPrefs.
        PlayerPrefs.DeleteAll();
        
        // This provides confirmation in the console.
        Debug.Log("<color=orange>[Editor Tool]</color> All PlayerPrefs data has been cleared.");
    }
}