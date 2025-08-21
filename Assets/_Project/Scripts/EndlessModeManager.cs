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
        // This is the heart of the mode.
        while (currentStamina > 0)
        {
            // 1. River Forecast Phase
            Debug.Log("Endless Cycle: Forecast Phase");
            // TODO: Announce the 2 blue and 1 red pushes. For now, we'll just wait.
            yield return new WaitForSeconds(1f); // Placeholder for forecast

            // 2. Player Action Phase
            isPlayerTurn = true;
            currentAP = Mathf.Min(apPerCycle, currentStamina);
            UpdateAPUI();
            Debug.Log($"Endless Cycle: Player Turn. Stamina: {currentStamina}, AP: {currentAP}");
            // The game now waits for the player to spend their AP.
            // We'll implement the logic for this waiting part next.
            yield return new WaitUntil(() => currentAP <= 0 || !isPlayerTurn);


            // 3. River Push Phase
            isPlayerTurn = false;
            Debug.Log("Endless Cycle: River Push Phase");
            // TODO: Execute the announced pushes sequentially.
            yield return new WaitForSeconds(1f); // Placeholder for pushes

            // 4. Generation Phase
            // TODO: Generate new rows and despawn old ones.

            yield return null; // Wait a frame before the next cycle
        }

        Debug.Log("<color=red>GAME OVER. Final Score: " + score + "</color>");
        // TODO: Show Game Over screen
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









}