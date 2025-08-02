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

public class GameManager : MonoBehaviour
{
    // Singleton pattern to make the GameManager easily accessible.
    public static GameManager Instance { get; private set; }

    [Header("Scene References")]
    [SerializeField] private GridManager gridManager; // Drag your GridManager here in the Inspector

    [Header("Game State")]
    public GameState currentState;
    public LevelData currentLevelData { get; private set; }
    private GameObject activeEndMarker; 

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
        currentState = GameState.Loading;
    }

    /// <summary>
    /// Called by the LevelEditorManager after it finishes loading a level.
    /// This gives the GameManager all the info it needs about the current puzzle.
    /// </summary>
    public void InitializeLevel(LevelData data, GameObject endMarker)
    {
        currentLevelData = data;
        // --- ADD THIS LINE ---
        activeEndMarker = endMarker; 

        currentState = GameState.Playing;
        Debug.Log($"<color=green>[GameManager]</color> Initialized with level. End marker tracking enabled.");
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
  
        // Check if goal is a tile
        TileInstance boatTile = boat.GetCurrentTile();
        if (!endGoal.isBankGoal && boatTile != null)
        {
            var boatCoords = gridManager.GetTileCoordinates(boatTile);
            if (boatCoords.x == endGoal.tileX && boatCoords.y == endGoal.tileY)
            {
                currentState = GameState.LevelComplete;
                Debug.Log($"<color=yellow>LEVEL COMPLETE!</color> Reached the goal tile ({endGoal.tileX}, {endGoal.tileY}).");
                return; // Stop checking
            }
        }

        // --- 2. CHECK FOR EJECTED GOAL LOSS (Your brilliant idea) ---
        // If the goal is a tile, but its marker GameObject has been destroyed, it's a loss.
        if (!endGoal.isBankGoal && activeEndMarker == null)
        {
            currentState = GameState.LevelFailed;
            Debug.LogWarning($"<color=red>LEVEL FAILED!</color> The end goal marker was destroyed (ejected from the grid).");
            return; // Stop checking
        }

        // --- 3. CHECK FOR OUT OF MOVES LOSS ---
        // If we are out of moves and haven't won, it's a loss.
        if (boat.currentMovementPoints <= 0)
        {
            currentState = GameState.LevelFailed;
            Debug.LogWarning($"<color=red>LEVEL FAILED!</color> Out of movement points.");
            return; // Stop checking
        }
    }



    /// <summary>
    /// Called by the End GoalMarker's OnDestroy() method.
    /// This is an event-driven way to handle the loss condition.
    /// </summary>
    public void OnEndGoalDestroyed()
    {
        // If we are playing, this means the goal was destroyed during gameplay.
        if (currentState == GameState.Playing)
        {
            currentState = GameState.LevelFailed;
            Debug.LogWarning($"<color=red>LEVEL FAILED!</color> The end goal was ejected from the grid.");
        }
    }











}