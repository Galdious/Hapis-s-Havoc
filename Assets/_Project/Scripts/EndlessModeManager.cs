/* EndlessModeManager.cs */
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Linq;

public class EndlessModeManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private BoatManager boatManager;
    [SerializeField] private RiverControls riverControls; // To show the forecast
    [SerializeField] private UIManager uiManager;
    [SerializeField] private RiverBankManager riverBankManager;


    [Header("Gameplay Settings")]
    [SerializeField] private int startStamina = 25;
    [SerializeField] private int apPerCycle = 4;
    [SerializeField] private int gridWidth = 3;
    [SerializeField] private int initialGridHeight = 8;

    [Header("UI References")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text apText;
    [SerializeField] private TMP_Text highScoreText;


    [Header("River Settings")]
    [Tooltip("The chance (0-1) that a newly pushed tile will be a red obstacle.")]
    [SerializeField] [Range(0f, 1f)] private float obstacleChance = 0.25f;
    [Tooltip("The chance (0-1) that a red obstacle tile will also be a hard blocker.")]
    [SerializeField][Range(0f, 1f)] private float blockerChance = 0.1f;

    [SerializeField] private TileLibrary tileLibrary;


    private class PlannedPush
    {
        public int row;
        public bool fromLeft;
        public TileType tileType;
        public bool isObstacle;
        public bool isBlocker;
        public RowDropZone forecastZone;
    }
    private List<PlannedPush> plannedPushes = new List<PlannedPush>();







    // --- Runtime State ---
    private int currentStamina;
    private int currentAP;
    private int score = 0;
    private int highScore = 0;
    private bool isPlayerTurn = false;
    private BoatController playerBoat;


    private const string highScoreKey = "EndlessHighScore";

    void Start()
    {
        // This manager should only be active if the game is in Endless mode.
        // We'll call a public "StartEndlessMode" method from the MainMenu to begin.
        // this.gameObject.SetActive(false);
    }

    public IEnumerator StartEndlessModeCoroutine()
    {
        yield return null;

        // this.gameObject.SetActive(true);
        Debug.Log("<color=cyan>--- STARTING ENDLESS MODE ---</color>");

        // Load high score
        highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        UpdateHighScoreUI();

        // Initialize state
        currentStamina = startStamina;
        score = 0;
        UpdateScoreUI();
        UpdateStaminaUI();

        // Setup the initial game board
        SetupBoardCoroutine();

        yield return StartCoroutine(SetupBoardCoroutine());

        // Start the main game loop
        StartCoroutine(EndlessGameLoop());
    }

    private IEnumerator SetupBoardCoroutine()
    {
        // For now, we create a fixed-size grid. We'll make it infinite later.
        gridManager.CreateGridFromEditor(gridWidth, initialGridHeight);

        if (riverBankManager != null)
        {
            riverBankManager.CreateBottomBank();
        }


        riverControls.InitializeLockStates(initialGridHeight);
        riverControls.GenerateControlsForGrid(); // This will create the drop zones

        // Spawn the boat at the bottom bank
        playerBoat = boatManager.SpawnPlayerBoat(RiverBankManager.BankSide.Bottom, 0);

        yield return null;

        if (playerBoat != null)
        {
            playerBoat.SelectBoat(); // <<< ADD THIS LINE
        }

    }

    private IEnumerator EndlessGameLoop()
    {
        while (currentStamina > 0)
        {
            // 1. River Forecast Phase
            Debug.Log("Endless Cycle: Forecast Phase");
            plannedPushes.Clear();

            // Plan 3 pushes on unique random rows: 1 red, 2 blue
            List<int> availableRows = new List<int>();
            for (int i = 0; i < initialGridHeight; i++) { availableRows.Add(i); }

            // Plan the red push
            int redRowIndex = Random.Range(0, availableRows.Count);
            PlanSinglePush(availableRows[redRowIndex], true);
            availableRows.RemoveAt(redRowIndex);

            // Plan the first blue push
            int blueRowIndex1 = Random.Range(0, availableRows.Count);
            PlanSinglePush(availableRows[blueRowIndex1], false);
            availableRows.RemoveAt(blueRowIndex1);

            // Plan the second blue push
            int blueRowIndex2 = Random.Range(0, availableRows.Count);
            PlanSinglePush(availableRows[blueRowIndex2], false);

            // Show the forecast visuals and wait for the player to see them
            foreach (var push in plannedPushes) { push.forecastZone?.ShowForecast(push.isObstacle); }
            yield return new WaitForSeconds(1.5f); // Give player time to plan

            // 2. Player Action Phase
            isPlayerTurn = true;
            currentAP = Mathf.Min(apPerCycle, currentStamina);
            UpdateAPUI();
            Debug.Log($"Endless Cycle: Player Turn. Stamina: {currentStamina}, AP: {currentAP}");
            yield return new WaitUntil(() => currentAP <= 0 || !isPlayerTurn || currentStamina <= 0);

            isPlayerTurn = false;
            foreach (var push in plannedPushes) { push.forecastZone?.HideForecast(); }
            yield return StartCoroutine(EndTurnCleanupCoroutine());


            if (currentStamina <= 0) break;

            // 3. River Push Phase
            Debug.Log("Endless Cycle: River Push Phase");
            foreach (var push in plannedPushes)
            {
                PuzzleHandTile tempHandTile = new PuzzleHandTile(push.tileType) { isFlipped = push.isObstacle };

                // We yield here to wait for each push to complete sequentially
                yield return StartCoroutine(gridManager.PushRowCoroutine(push.row, push.fromLeft, tempHandTile));
            }

            // 4. Generation Phase (Still a TODO for the next chunk)

            yield return new WaitForSeconds(0.5f); // A brief pause before the next cycle
        }

        Debug.Log("<color=red>GAME OVER. Final Score: " + score + "</color>");
    }


    // --- UI Update Methods ---
    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
    }

    private void UpdateStaminaUI()
    {
        if (staminaText != null) staminaText.text = $"Stamina: {currentStamina}";
    }

    private void UpdateHighScoreUI()
    {
        if (highScoreText != null) highScoreText.text = $"High Score: {highScore}";
    }
    private void UpdateAPUI()
    {
        if (apText != null) apText.text = $"AP: {currentAP}";
    }

    public void SpendActionPoint()
    {
        if (!isPlayerTurn || currentAP <= 0) return;

        currentAP--;
        currentStamina--; // Every move costs 1 AP and 1 Stamina

        UpdateAPUI();
        UpdateStaminaUI();
        UpdateScore();


        StartCoroutine(FinalizeBoatStateAfterMove());

        // if (currentAP > 0)
        // {
        //     // Re-selecting the boat will handle all the state and visuals for the next move.
        //     StartCoroutine(ReselectBoatAfterMove());
        // }


        Debug.Log($"Move made. AP left: {currentAP}, Stamina left: {currentStamina}");
    }


    private void UpdateScore()
    {
        BoatController boat = boatManager.GetPlayerBoats().FirstOrDefault();
        if (boat == null || boat.GetCurrentTile() == null) return;

        // Get the boat's current grid coordinates
        var coords = gridManager.GetTileCoordinates(boat.GetCurrentTile());

        // The score is the highest Y coordinate (row) the boat has reached
        if (coords.y > score)
        {
            score = coords.y;
            UpdateScoreUI();

            // Check for new high score
            if (score > highScore)
            {
                highScore = score;
                PlayerPrefs.SetInt(highScoreKey, highScore);
                UpdateHighScoreUI();
            }
        }
    }


    public void EndPlayerTurn()
    {
        if (!isPlayerTurn) return;

        Debug.Log("Player ended turn manually.");
        isPlayerTurn = false;
        // Setting isPlayerTurn to false will satisfy the 'WaitUntil' in our game loop,
        // allowing the cycle to proceed to the River Push phase.
    }




    public void StartEndlessMode()
    {
        StartCoroutine(StartEndlessModeCoroutine());
    }


    private IEnumerator FinalizeBoatStateAfterMove()
    {
        // Wait for the end of the frame. This is a robust way to ensure
        // that the boat's MoveToTileCoroutine has finished setting its
        // new 'currentTile' and 'currentSnapPoint' state.
        yield return new WaitForEndOfFrame();

        if (playerBoat != null)
        {
            // 1. Tell the boat that its movement action is officially over.
            playerBoat.CompleteMovement();

            // 2. NOW, decide what to do based on remaining AP.
            if (currentAP > 0)
            {
                // If the player has moves left, re-select the boat for the next action.
                playerBoat.SelectBoat();
            }
            else
            {
                // If the player is out of moves, deselect the boat to provide clear visual feedback.
                playerBoat.DeselectBoat();
            }

        }
    }



    public void OnBoatClicked()
    {
        // The player is only allowed to select the boat if it's their turn AND they have AP to spend.
        if (isPlayerTurn && currentAP > 0)
        {
            if (playerBoat != null && !playerBoat.isSelected)
            {
                playerBoat.SelectBoat();
            }
            else if (playerBoat != null && playerBoat.isSelected)
            {
                // Optional: allow deselecting mid-turn
                // playerBoat.DeselectBoat(); 
            }
        }
        else
        {
            Debug.Log("Cannot select boat: Not player's turn or out of AP.");
            // Here you could play a "no moves left" sound effect.
        }
    }

    // This is our new method for getting an infinite supply of random tiles.
    private TileType GetRandomTileFromLibrary()
    {
        if (tileLibrary == null || tileLibrary.tileTypes.Count == 0)
        {
            Debug.LogError("TileLibrary is not assigned or is empty in EndlessModeManager!");
            return null;
        }
        int randomIndex = Random.Range(0, tileLibrary.tileTypes.Count);
        return tileLibrary.tileTypes[randomIndex];
    }

    // A helper method to create a single planned push and add it to our list.
    private void PlanSinglePush(int row, bool isObstacle)
    {
        bool fromLeft = Random.value > 0.5f;
        
        var push = new PlannedPush
        {
            row = row,
            fromLeft = fromLeft,
            tileType = GetRandomTileFromLibrary(),
            isObstacle = isObstacle,
            // We will get the forecastZone using a new helper method in RiverControls
            forecastZone = riverControls.GetDropZone(row, fromLeft) 
        };
        
        if (isObstacle)
        {
            push.isBlocker = Random.value < blockerChance;
        }
        
        plannedPushes.Add(push);
    }


    private IEnumerator EndTurnCleanupCoroutine()
    {
        // If the player's boat exists and is currently selected...
        if (playerBoat != null && playerBoat.isSelected)
        {
            // ...tell it to deselect. This will play the lowering animation.
            playerBoat.DeselectBoat();
            
            // Wait for the boat's deselection animation to finish.
            // The LiftAndBobBoat coroutine has a duration of 0.3f.
            // We'll wait a little longer to be safe.
            yield return new WaitForSeconds(0.4f);
        }
        else
        {
            // If the boat isn't selected, just add a small pause for pacing.
            yield return new WaitForSeconds(0.2f);
        }
    }


}