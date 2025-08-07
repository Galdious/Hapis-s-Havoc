/* GameManager.cs */
using UnityEngine;

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

    [Header("Game State")]
    public GameState currentState;
    public OperatingMode currentMode { get; private set; } = OperatingMode.Editor;
    public LevelData currentLevelData { get; private set; }
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
        currentState = GameState.Loading;
    }

    /// <summary>
    /// Called by the LevelEditorManager after it finishes loading a level.
    /// This gives the GameManager all the info it needs about the current puzzle.
    /// </summary>
    // public void InitializeLevel(LevelData data, GameObject endMarker)
    // {
    //     currentLevelData = data;
    //     // --- ADD THIS LINE ---
    //     activeEndMarker = endMarker; 

    //     currentState = GameState.Playing;

    //     //START THE TIMER ---
    //     levelStartTime = Time.time;
    //     isTimerRunning = true;
    //     Debug.Log("<color=orange>[Playtest Timer]</color> Timer started.");

    //     Debug.Log($"<color=green>[GameManager]</color> Initialized with level. End marker tracking enabled.");
    // }

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


    /// <summary>
    /// This is the master method that evaluates the game state after every boat move.
    /// It checks for win and loss conditions in the correct order of priority.
    /// </summary>
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

        // // Now, if any of the above conditions were met...
        // if (isGameOver)
        // {
        //     StopTimerAndLogResult();
        //     Debug.Log(reason); // This will now always print the correct reason.
        // }

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



    /// <summary>
    /// Called by the End GoalMarker's OnDestroy() method.
    /// This is an event-driven way to handle the loss condition.
    /// </summary>
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


    // public void EnterPlayMode()
    // {
    //     Debug.Log("<color=cyan>Switching to PLAY Mode.</color>");
    //     currentMode = OperatingMode.Playing;

    //     // Hide the editor UI and show the player UI
    //     if (uiManager != null)
    //     {
    //         uiManager.SwitchToMode(OperatingMode.Playing);
    //     }

    //     // Restart the level from its base state to begin the playtest
    //     if (FindFirstObjectByType<LevelEditorManager>() is LevelEditorManager editor)
    //     {
    //         editor.RestartCurrentLevel();
    //     }
    // }


    public void SetReconstructing(bool status)
    {
        isReconstructing = status;
    }

    public void SetOperatingMode(OperatingMode mode)
    {
        currentMode = mode;
    }



}