using UnityEngine;
using UnityEngine.SceneManagement; // This is CRUCIAL for loading scenes

public class MainMenuController : MonoBehaviour
{
    // This function will be called by the "Play Puzzle" button
    public void GoToPuzzleGame()
    {
        Debug.Log("Loading PuzzleGame scene...");
        SceneManager.LoadScene("PuzzleGame");
    }

    // This function will be called by the "Level Editor" button
    public void GoToLevelEditor()
    {
        Debug.Log("Loading LevelEditor scene...");
        SceneManager.LoadScene("LevelEditor");
    }

    // This function will be called by the "Testing Bed" button
    public void GoToTestingBed()
    {
        Debug.Log("Loading TestingBed scene...");
        SceneManager.LoadScene("TestingBed");
    }
}