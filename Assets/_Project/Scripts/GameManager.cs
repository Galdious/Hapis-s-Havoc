/* GameManager.cs */
using UnityEngine;
using System.Collections;



// This enum will track the overall state of the game.
public enum GameState
{
    Loading,
    Playing,
    Paused,
    LevelComplete,
    LevelFailed
}

public enum OperatingMode { Editor, Playing }

public class GameManager : MonoBehaviour
{
    // Singleton pattern to make the GameManager easily accessible.
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private LevelEditorManager levelEditorManager;


    [Header("Game State")]
    public GameState currentState;
    public OperatingMode currentMode { get; private set; } = OperatingMode.Editor;
    public LevelData currentLevelData { get; private set; }
    public LevelInfo currentLevelInfo { get; private set; }
    private GameObject activeEndMarker;

    [Header("Scoring")]
    [Tooltip("If the player has this many moves or fewer left, they lose a star.")]
    [SerializeField] private int movePenaltyThreshold = 0; // Set to 0 for the strict "must have leftover moves" rule. You can increase this.

    private int totalStarsInLevel = 0;
    private int totalPowerupsInLevel = 0; // We can track this too
    private bool isReconstructing = false;


    // TIME TRACKING
    private float levelStartTime;
    private float levelEndTime;
    private bool isTimerRunning = false;

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

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>();
        }



    }

    private void Start()
    {
        // Check if there is an instruction to load a level.
        if (!string.IsNullOrEmpty(LevelSelectManager.LevelToLoad))
        {
            // If so, start our new "waiter" coroutine.
            // DO NOT load the level directly from here.
            StartCoroutine(LoadLevelAfterSceneIsReady());
        }
        else
        {
            // If we are just opening the editor scene normally, set the default state.
            currentState = GameState.Loading;
        }
    }

    private IEnumerator LoadLevelAfterSceneIsReady()
    {
        // --- THIS IS THE MAGIC LINE ---
        // Wait for the end of the current frame. By the time the next frame starts,
        // every other script in the scene will have executed its Awake() and Start() methods.
        yield return null;

        // Now that the scene is fully initialized, we can safely issue our commands.
        Debug.Log("<color=lime>[GameManager]</color> Scene is ready. Proceeding with level load.");

        if (levelEditorManager != null)
        {
            // Use our powerful method that handles everything.
            levelEditorManager.LoadAndPlayLevel(LevelSelectManager.LevelToLoad);
        }
        else
        {
            Debug.LogError("[GameManager] Cannot load level! The reference to LevelEditorManager is missing.");
        }

        // IMPORTANT: Clear the instruction so it doesn't try to load again if this scene is reloaded.
        LevelSelectManager.LevelToLoad = null;
    }




    public void StartLevelTimer()
    {
        // currentState = GameState.Playing;
        levelStartTime = Time.time;
        isTimerRunning = true;
        Debug.Log("<color=orange>[Playtest Timer]</color> Timer started.");
    }


    /// Updates the GameManager's references after a state change (like an Undo)
    /// without resetting the level timer.
    public void UpdateLevelState(LevelData data, GameObject endMarker)
    {
        currentLevelData = data;
        activeEndMarker = endMarker;
        currentState = GameState.Playing;
        isTimerRunning = true;

        Debug.Log("<color=green>[GameManager]</color> Level state references updated without resetting timer.");
    }


    private void StopTimerAndLogResult()
    {
        // Safety check to ensure we don't stop it more than once.
        if (!isTimerRunning) return;

        levelEndTime = Time.time;
        isTimerRunning = false;

        float elapsedTime = levelEndTime - levelStartTime;

        // Let's format it nicely for the log.
        System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(elapsedTime);
        string timeText = string.Format("{0:D2}:{1:D2}.{2:D3}", timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);

        Debug.Log($"<color=orange>[Playtest Timer]</color> Level Time: <b>{timeText}</b>");
    }

    /// This is the master method that evaluates the game state after every boat move.
    /// It checks for win and loss conditions in the correct order of priority.
    public void EvaluateGameStateAfterMove(BoatController boat)
    {
        // Don't do anything if the game is not in the 'Playing' state.
        if (currentState != GameState.Playing) return;

        GoalData endGoal = currentLevelData.endPosition;
        bool isGameOver = false;
        string reason = "";

        // Check for Win Condition 1: Reached Tile Goal
        TileInstance boatTile = boat.GetCurrentTile();

        (int x, int y) boatCoords = (-1, -1); // Initialize to invalid coordinates
        if (boatTile != null) boatCoords = gridManager.GetTileCoordinates(boatTile);

        if (!endGoal.isBankGoal && boatTile != null && boatCoords.x == endGoal.tileX && boatCoords.y == endGoal.tileY)
        {
            currentState = GameState.LevelComplete;
            isGameOver = true;
            reason = $"<color=yellow>LEVEL COMPLETE!</color> Reached the goal tile ({endGoal.tileX}, {endGoal.tileY}).";
        }
        // Check for Win Condition 2: Reached Bank Goal
        else if (endGoal.isBankGoal && boat.CurrentBank.HasValue && boat.CurrentBank.Value == endGoal.bankSide)
        {
            currentState = GameState.LevelComplete;
            isGameOver = true;
            reason = $"<color=yellow>LEVEL COMPLETE!</color> Reached the goal bank ({endGoal.bankSide}).";
        }
        // Check for Loss Condition 1: Goal Ejected
        else if (!endGoal.isBankGoal && activeEndMarker == null)
        {
            currentState = GameState.LevelFailed;
            isGameOver = true;
            reason = $"<color=red>LEVEL FAILED!</color> The end goal marker was destroyed.";
        }
        // Check for Loss Condition 2: Out of Moves
        else if (boat.currentMovementPoints <= 0)
        {
            currentState = GameState.LevelFailed;
            isGameOver = true;
            reason = $"<color=red>LEVEL FAILED!</color> Out of movement points.";
        }



        if (isGameOver)
        {
            StopTimerAndLogResult();
            Debug.Log(reason);

            switch (currentState)
            {
                case GameState.LevelComplete:
                    // Calculate the score and gather all stats
                    int finalScore = CalculateFinalScore(boat);
                    float elapsedTime = levelEndTime - levelStartTime;
                    int maxMoves = boat.maxMovementPoints;
                    int movesUsed = maxMoves - boat.currentMovementPoints;

                        if (currentLevelInfo != null)
                        {
                            // 1. Create the unique key for this level. e.g., "Level_01_01_Stars"
                            string levelKey = $"Level_{currentLevelInfo.WorldNumber:00}_{currentLevelInfo.LevelNumber:00}_Stars";

                            // 2. Get the previously saved score for this level (defaults to 0 if it doesn't exist).
                            int oldBestScore = PlayerPrefs.GetInt(levelKey, 0);

                            // 3. Only save if the new score is better than the old one.
                            if (finalScore > oldBestScore)
                            {
                                PlayerPrefs.SetInt(levelKey, finalScore);
                                // Save the data to disk immediately.
                                PlayerPrefs.Save();
                                Debug.Log($"<color=yellow>[GameManager]</color> New high score for {levelKey}: {finalScore} stars! (Old score: {oldBestScore}). Progress saved.");
                            }
                            else
                            {
                                Debug.Log($"<color=white>[GameManager]</color> Score for {levelKey} was {finalScore}, but best is {oldBestScore}. Progress not saved.");
                            }
                        }


                    // Tell the UI Manager to show the results
                    if (uiManager != null)
                    {
                        uiManager.ShowLevelCompleteScreen(finalScore, elapsedTime, movesUsed, maxMoves, boat.starsCollected, totalStarsInLevel);
                    }
                    break;

                case GameState.LevelFailed:
                    if (uiManager != null)
                    {
                        // For a failure, we can show a simpler screen or pass stats too
                        uiManager.ShowLevelFailedScreen(reason);
                    }
                    break;
            }
        }




    }


    public void OnEndGoalDestroyed()
    {
        if (isReconstructing) return;

        // If we are playing, this means the goal was destroyed during gameplay.
        if (currentState == GameState.Playing)
        {
            currentState = GameState.LevelFailed;
            StopTimerAndLogResult(); // Stop the timer when the level fails.
            string reason = "The end goal was ejected from the grid.";
            Debug.LogWarning($"<color=red>LEVEL FAILED!</color> The end goal was ejected from the grid.");

            if (uiManager != null)
            {
                uiManager.ShowLevelFailedScreen(reason); // Pass the failure reason
            }

        }
    }



    public void SetLevelInfo(int starCount)
    {
        totalStarsInLevel = starCount;
    }


    private int CalculateFinalScore(BoatController boat)
    {
        // Start with a perfect score
        int finalScore = 3;
        Debug.Log($"[Scoring] Starting with {finalScore} stars.");

        // Penalty 1: Moves Used
        if (boat.currentMovementPoints <= movePenaltyThreshold)
        {
            finalScore--;
            Debug.Log($"[Scoring] Penalty applied: Moves left ({boat.currentMovementPoints}) is at or below threshold ({movePenaltyThreshold}). New score: {finalScore}");
        }

        // Penalty 2: Collectibles
        if (boat.starsCollected < totalStarsInLevel)
        {
            finalScore--;
            Debug.Log($"[Scoring] Penalty applied: Not all stars collected ({boat.starsCollected} / {totalStarsInLevel}). New score: {finalScore}");
        }

        // Penalty 3: Undo Usage
        if (HistoryManager.Instance != null && HistoryManager.Instance.hasUsedUndo)
        {
            finalScore--;
            Debug.Log($"[Scoring] Penalty applied: Undo was used. New score: {finalScore}");
        }

        // Ensure the score is never less than 1 for a win
        return Mathf.Max(1, finalScore);
    }





    public void SetReconstructing(bool status)
    {
        isReconstructing = status;
    }

    public void SetOperatingMode(OperatingMode mode)
    {
        currentMode = mode;
    }




    public void EnterEditorMode()
    {
        // Set our camera to the editor view
        if (CameraManager.Instance != null) CameraManager.Instance.SwitchToEditorView();

        Debug.Log("<color=orange>Returning to EDITOR Mode.</color>");

        // Stop the game logic and timer
        currentState = GameState.Loading; // A neutral state
        isTimerRunning = false;

        // Set the operating mode
        SetOperatingMode(OperatingMode.Editor);

        // Tell the UIManager to switch the UI back
        if (uiManager != null)
        {
            uiManager.SwitchToMode(OperatingMode.Editor);
        }



        // Tell RiverControls to completely regenerate its visuals for the new mode.
        // This will destroy the drop zones and create the arrows and locks.
        if (FindFirstObjectByType<RiverControls>() is RiverControls controls)
        {
            controls.GenerateControlsForGrid(); // <-- THIS IS THE CORRECT, POWERFUL CALL
        }

        // Finally, tell the Level Editor to redraw its hand palette.
        if (FindFirstObjectByType<LevelEditorManager>() is LevelEditorManager editor)
        {
            editor.RedrawHandPalette();
        }


        // Optional: you could choose to reload the level to its last saved state here,
        // or leave it as it was at the end of the playtest. For now, we'll leave it.
    }





    public void SetCurrentLevel(LevelData data, LevelInfo info)
    {
        currentLevelData = data;
        currentLevelInfo = info;
    }
    






}