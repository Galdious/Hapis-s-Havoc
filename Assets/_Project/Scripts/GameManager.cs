/* GameManager.cs */
using UnityEngine;

// This enum will track the overall state of the game.
public enum GameState
{
    Loading,
    Playing,
    Paused,
    LevelComplete
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
    public void InitializeLevel(LevelData data)
    {
        currentLevelData = data;
        currentState = GameState.Playing;
        Debug.Log($"<color=green>[GameManager]</color> Initialized with level: {data.gridWidth}x{data.gridHeight}. Let the game begin!");
    }

    /// <summary>
    /// Checks if the boat has reached the designated end goal for the level.
    /// </summary>
    public void CheckForWinCondition(BoatController boat)
    {
        // We only check for wins while actively playing.
        if (currentState != GameState.Playing || currentLevelData == null || currentLevelData.endPosition == null)
        {
            return;
        }

        GoalData endGoal = currentLevelData.endPosition;

        // Case 1: The goal is a specific bank.
        if (endGoal.isBankGoal)
        {
            if (boat.CurrentBank.HasValue && boat.CurrentBank.Value == endGoal.bankSide)
            {
                // WE HAVE A WINNER!
                currentState = GameState.LevelComplete;
                Debug.Log($"<color=yellow>LEVEL COMPLETE! Boat reached the goal bank: {endGoal.bankSide}.</color>");
                // Future: Show "You Win!" UI.
            }
        }
        // Case 2: The goal is a specific tile.
        else if (endGoal.tileX != -1)
        {
            TileInstance boatTile = boat.GetCurrentTile();
            if (boatTile != null)
            {
                var boatCoords = gridManager.GetTileCoordinates(boatTile);
                if (boatCoords.x == endGoal.tileX && boatCoords.y == endGoal.tileY)
                {
                    // WE HAVE A WINNER!
                    currentState = GameState.LevelComplete;
                    Debug.Log($"<color=yellow>LEVEL COMPLETE! Boat reached the goal tile ({endGoal.tileX}, {endGoal.tileY}).</color>");
                    // In the future, we would show a "You Win!" UI panel here.
                }
            }
        }
    }
}