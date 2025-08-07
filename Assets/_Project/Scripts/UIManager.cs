/* UIManager.cs */
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // Needed for the Image component
using TMPro; // Needed for TextMeshPro components
using System.Collections.Generic; // Needed for List

public class UIManager : MonoBehaviour
{
    // Singleton pattern
    public static UIManager Instance { get; private set; }

    [Header("Scene References")]
    [Tooltip("The LevelEditorManager is needed to handle restarts.")]
    [SerializeField] private LevelEditorManager levelEditorManager;

    [Header("UI Panels")]
    [Tooltip("Drag the 'LevelCompletePanel' GameObject here.")]
    [SerializeField] private GameObject levelCompletePanel;

    [Tooltip("Drag the 'LevelFailedPanel' GameObject here.")]
    [SerializeField] private GameObject levelFailedPanel;

    [Tooltip("Drag the parent GameObject containing all editor UI (tool buttons, palettes, etc.).")]
    [SerializeField] private GameObject editorUI_Container;
    [Tooltip("Drag the parent GameObject containing all player-facing gameplay UI.")]
    [SerializeField] private GameObject playerUI_Container; 


    [Header("Level Complete Stats")]
    [Tooltip("The list of the 3 star images on the win panel.")]
    [SerializeField] private List<Image> starRatingImages;
    [Tooltip("The sprite for a bright, earned star.")]
    [SerializeField] private Sprite starWonSprite;
    [Tooltip("The sprite for a grey, un-earned star.")]
    [SerializeField] private Sprite starLostSprite;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private TMP_Text collectiblesText;

    [Header("Level Failed Stats")]
    [SerializeField] private TMP_Text failureReasonText;


    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (levelEditorManager == null)
        {
            levelEditorManager = FindFirstObjectByType<LevelEditorManager>();
        }

        // Ensure all panels are hidden at the start of the game
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null) levelFailedPanel.SetActive(false);
        SwitchToMode(OperatingMode.Editor);
    }

    // --- This is the new, upgraded method ---
    public void ShowLevelCompleteScreen(int finalScore, float elapsedTime, int movesUsed, int totalMoves, int starsCollected, int totalStarsInLevel)
    {
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
            if(playerUI_Container != null) playerUI_Container.SetActive(false); // Hide the editor UI

            // 1. Update Star Rating
            for (int i = 0; i < starRatingImages.Count; i++)
            {
                if (i < finalScore)
                {
                    starRatingImages[i].sprite = starWonSprite; // Show a bright star
                }
                else
                {
                    starRatingImages[i].sprite = starLostSprite; // Show a greyed-out star
                }
            }

            // 2. Update Time Text
            // This formats the raw float time into MM:SS format
            System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(elapsedTime);
            timeText.text = string.Format("Time: {0:D2}:{1:D2}", timeSpan.Minutes, timeSpan.Seconds);

            // 3. Update Moves Text
            movesText.text = $"Moves Used: {movesUsed} / {totalMoves}";

            // 4. Update Collectibles Text
            collectiblesText.text = $"Stars Gathered: {starsCollected} / {totalStarsInLevel}";
        }
    }

    // --- This is the new, upgraded method ---
    public void ShowLevelFailedScreen(string reason)
    {
        if (levelFailedPanel != null)
        {
            levelFailedPanel.SetActive(true);
            if(playerUI_Container != null) playerUI_Container.SetActive(false); // Hide the editor UI

            // Update failure reason text
            if (failureReasonText != null)
            {
                failureReasonText.text = reason;
            }
        }
    }

    // --- Public Methods for Button OnClick() Events (these are unchanged) ---
    public void HandleRestart()
    {

        ResetToGameplayUI();
        if (levelEditorManager != null)
        {
            levelEditorManager.RestartCurrentLevel();
        }
        else
        {
            Debug.LogError("[UIManager] Cannot restart: LevelEditorManager not found!");
        }
    }

    public void HandleNextLevel()
    {
        Debug.Log("TODO: Implement 'Next Level' logic. For now, returning to menu.");
        HandleReturnToMenu();
    }

    public void HandleReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    public void ResetToGameplayUI()
    {
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null) levelFailedPanel.SetActive(false);
        // We now have two potential UIs to show, so we need to know which mode to return to.
        // For now, restarting will always take us back to the player UI.
        SwitchToMode(OperatingMode.Playing);
    
    }




    public void SwitchToMode(OperatingMode mode)
    {
        if (mode == OperatingMode.Editor)
        {
            if (editorUI_Container != null) editorUI_Container.SetActive(true);
            if (playerUI_Container != null) playerUI_Container.SetActive(false);
        }
        else // Switching to Playing mode
        {
            if (editorUI_Container != null) editorUI_Container.SetActive(false);
            if (playerUI_Container != null) playerUI_Container.SetActive(true);
        }
    }








}