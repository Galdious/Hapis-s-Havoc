/* EndlessModeManager.cs */
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine.UI; 

public class EndlessModeManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private BoatManager boatManager;
    [SerializeField] private RiverControls riverControls; // To show the forecast
    [SerializeField] private UIManager uiManager;
    [SerializeField] private RiverBankManager riverBankManager;
    [SerializeField] private Unity.Cinemachine.CinemachineCamera endlessVCam;


    [Header("Gameplay Settings")]
    [SerializeField] private int startStamina = 25;
    [SerializeField] private int apPerCycle = 4;
    [SerializeField] private int gridWidth = 3;
    [SerializeField] private int initialGridHeight = 8;

    [Header("World Generation")]
    [Tooltip("How many rows to generate ahead of the boat's current position.")]
    [SerializeField] private int leadingBuffer = 10;
    [Tooltip("How many rows to keep behind the boat before destroying them.")]
    [SerializeField] private int trailingBuffer = 5;


    [Header("UI References")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text apText;
    [SerializeField] private TMP_Text highScoreText;


    [Header("River Settings")]
    [Tooltip("The chance (0-1) that a newly pushed tile will be a red obstacle.")]
    [SerializeField][Range(0f, 1f)] private float obstacleChance = 0.25f;
    [Tooltip("The chance (0-1) that a red obstacle tile will also be a hard blocker.")]
    [SerializeField][Range(0f, 1f)] private float blockerChance = 0.1f;

    [Header("Collectible Settings")]
    [Tooltip("The chance (0-1) that an Extra Move collectible will spawn on a new blue tile.")]
    [SerializeField][Range(0f, 1f)] private float extraMoveSpawnChance = 0.1f; // 10% chance
    [Tooltip("The prefab for the Extra Move collectible object.")]
    [SerializeField] private GameObject extraMoveCollectiblePrefab;




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


    [Header("Camera Control")]
    [SerializeField] private Transform cameraProxy; // Drag the CameraProxy GameObject here
    [SerializeField] private float cameraZOffset = -10f; // How far back the camera is from the boat
    [SerializeField] private float cameraHeight = 15f;   // How high the camera is
    [SerializeField] private float cameraMoveSpeed = 5f;





    // --- Runtime State ---
    private int currentStamina;
    private int currentAP;
    private int score = 0;
    private int highScore = 0;
    private bool isPlayerTurn = false;
    private BoatController playerBoat;
    private bool skipFirstForecast = false; 
    private const string endlessSaveKey = "EndlessSaveFile";


    private CinemachineBlendDefinition originalCameraBlend;
    private Canvas dropZoneCanvas; 


    private Vector3 cameraTargetPosition;


    // private Dictionary<int, List<TileInstance>> activeRows = new Dictionary<int, List<TileInstance>>();
    private int highestGeneratedRow = -1;
    private int lowestGeneratedRow = 0;

    private const string highScoreKey = "EndlessHighScore";

    void Start()
    {
        // This manager should only be active if the game is in Endless mode.
        // We'll call a public "StartEndlessMode" method from the MainMenu to begin.
        // this.gameObject.SetActive(false);
    }


    void LateUpdate()
    {


        if (cameraProxy == null || playerBoat == null || endlessVCam == null)
        {
            // --- EMERGENCY FOLLOW LOGIC ---
            if (isPlayerTurn) // Only check this during the player's actual turn
            {
                // Convert the boat's world position to a screen position (0-1 range)
                Vector3 boatViewportPos = Camera.main.WorldToViewportPoint(playerBoat.transform.position);

                // Define our "emergency" threshold near the top of the screen
                float topThreshold = 0.9f;

                // If the boat has moved above the threshold...
                if (boatViewportPos.y > topThreshold)
                {
                    // ...immediately update the camera's target to the boat's current Z position.
                    cameraTargetPosition = new Vector3(0, 0, playerBoat.transform.position.z);
                }
            }
        }
        // --- REGULAR SMOOTH MOVEMENT LOGIC (Unchanged) ---
        // This part always runs, smoothly moving the proxy towards its current target,
        // whether that target was set at the end of the turn or during an emergency.
        cameraProxy.position = Vector3.Lerp(cameraProxy.position, cameraTargetPosition, Time.deltaTime * cameraMoveSpeed);
        endlessVCam.transform.position = new Vector3(0, cameraHeight, cameraProxy.position.z + cameraZOffset);
    }









    public IEnumerator StartEndlessModeCoroutine()
    {
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            // 1. Store the brain's current default blend setting.
            originalCameraBlend = brain.DefaultBlend;

            // 2. Create a new "Cut" blend using the correct syntax.
            //    - We refer to the enum via the type: `CinemachineBlendDefinition.Styles`
            //    - We use the correct enum name: `Styles.Cut` (plural)
            var cutBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // 3. Force the brain to use our "Cut" blend for the next transition.
            brain.DefaultBlend = cutBlend;
        }


        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToEndlessView();
        }

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
        // --- 1. Use a temporary blueprint to build the initial grid in one go ---
        List<TileSaveData> initialGridBlueprint = new List<TileSaveData>();
        for (int y = 0; y < initialGridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                TileType randomType = GetRandomTileFromLibrary();
                if (randomType == null) continue;

                bool isObstacle = Random.value < obstacleChance;
                bool isBlocker = isObstacle && (Random.value < blockerChance);

                initialGridBlueprint.Add(new TileSaveData
                {
                    gridX = x,
                    gridY = y,
                    tileTypeName = randomType.displayName,
                    isFlipped = isObstacle,
                    isHardBlocker = isBlocker,
                    rotationY = (randomType.canRotate180 && Random.value > 0.5f) ? 180f : 0f
                });
            }
        }

        // GridManager creates the visual world and sets up its internal grid array
        gridManager.CreateGridFromEditor(gridWidth, initialGridHeight, initialGridBlueprint);



        // --- 3. Set the initial world boundaries ---
        lowestGeneratedRow = 0;
        highestGeneratedRow = initialGridHeight - 1;

        if (riverBankManager != null)
        {
            riverBankManager.CreateBottomBank();
        }

        riverControls.InitializeLockStates(initialGridHeight);
        riverControls.GenerateControlsForGrid();

        playerBoat = boatManager.SpawnPlayerBoat(RiverBankManager.BankSide.Bottom, 0);



        if (cameraProxy != null && playerBoat != null && endlessVCam != null)
        {
            Vector3 boatStartPos = playerBoat.transform.position;

            // 1. Calculate the final, correct position for the camera proxy.
            // We only care about the boat's vertical (Z) position.
            Vector3 finalProxyPos = new Vector3(0, 0, boatStartPos.z);

            // 2. Set BOTH the current position AND the target position to this final spot.
            // This prevents any "lerping" in LateUpdate from a different starting point.
            cameraProxy.position = finalProxyPos;
            cameraTargetPosition = finalProxyPos;

            // 3. INSTANTLY teleport the Cinemachine camera itself to its final calculated position.
            // This happens in a single frame before the player sees anything.
            endlessVCam.transform.position = new Vector3(0, cameraHeight, cameraProxy.position.z + cameraZOffset);



        }




        yield return null;

        if (playerBoat != null)
        {
            playerBoat.SelectBoat();
        }

        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            brain.DefaultBlend = originalCameraBlend;
        }

    }

    private IEnumerator EndlessGameLoop()
    {
        while (true)
        {
            if (currentStamina <= 0)
            {
                EndGame();
                yield break; // Exit the coroutine completely.
            }

            if (!skipFirstForecast)
            {

                // 1. River Forecast Phase
                Debug.Log("Endless Cycle: Forecast Phase");
                plannedPushes.Clear();

                // Plan 3 pushes on unique random rows: 1 red, 2 blue
                // Get a list of all rows that CURRENTLY exist in the game world.
                List<int> availableRows = new List<int>();
                for (int i = lowestGeneratedRow; i <= highestGeneratedRow; i++)
                {
                    availableRows.Add(i);
                }

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
            }

            skipFirstForecast = false;

            // 2. Player Action Phase
            isPlayerTurn = true;
            currentAP = Mathf.Min(apPerCycle, currentStamina);
            UpdateAPUI();
            Debug.Log($"Endless Cycle: Player Turn. Stamina: {currentStamina}, AP: {currentAP}");
            yield return new WaitUntil(() => currentAP <= 0 || !isPlayerTurn || currentStamina <= 0);

            isPlayerTurn = false;
            foreach (var push in plannedPushes) { push.forecastZone?.HideForecast(); }

            if (playerBoat != null)
            {
                Vector3 boatCurrentPos = playerBoat.transform.position;
                // We only care about the boat's Z position (our world's Y)
                cameraTargetPosition = new Vector3(0, boatCurrentPos.y, boatCurrentPos.z);
            }

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

            Debug.Log("Endless Cycle: Cleaning up old rows.");
            CleanupOldRows();

            SaveEndlessRun(); 

            // 4. Generation Phase (Still a TODO for the next chunk)

            yield return new WaitForSeconds(0.5f); // A brief pause before the next cycle

            // At the very end of the cycle, before it loops back to the Forecast Phase,
            // re-select the boat to prepare it for the player's next turn.
            if (playerBoat != null)
            {
                playerBoat.SelectBoat();
            }

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

        if (currentStamina <= 0)
        {
            EndGame();
            return; // Stop further turn logic.
        }


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

        // yield return StartCoroutine(UpdateWorldBounds());
        yield return StartCoroutine(GenerateNewRowsIfNeeded());

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

        // --- NEW FILTERING LOGIC ---
        // 1. Create a new, temporary list of all tiles that are allowed in the endless bag.
        //    We use LINQ's 'Where' clause to filter the main list.
        List<TileType> availableTiles = tileLibrary.tileTypes.Where(tile => tile.quantity > 0).ToList();

        // 2. Check if any tiles passed the filter.
        if (availableTiles.Count == 0)
        {
            Debug.LogWarning("No available tiles found in TileLibrary with count > 0. The bag is empty!");
            // As a fallback, we could return a default tile, but for now, returning null is safer.
            return null;
        }

        // 3. Pick a random tile from our new, filtered list.
        int randomIndex = Random.Range(0, availableTiles.Count);
        return availableTiles[randomIndex];
        // --- END OF NEW LOGIC ---
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






    private IEnumerator UpdateWorldBounds()
    {
        if (playerBoat == null || playerBoat.GetCurrentTile() == null)
        {
            // If we don't know where the boat is, we can't update the world.
            yield break;
        }

        // 1. Get the boat's current position
        var boatCoords = gridManager.GetTileCoordinates(playerBoat.GetCurrentTile());
        int boatY = boatCoords.y;

        // 2. Calculate the desired top and bottom of our world "window"
        int highestRequiredRow = boatY + leadingBuffer;
        int lowestAllowedRow = boatY - trailingBuffer;

        // 3. Call the helper methods to do the work
        GenerateMissingRows(highestRequiredRow);
        DestroyOldRows(lowestAllowedRow);

        // We yield for one frame to allow Unity to process any object creation/destruction
        yield return null;
    }

    private void GenerateMissingRows(int targetTopRow)
    {
        gridManager.ExpandGridForEndless(targetTopRow + 1); // +1 because row count is 1-based

        // Loop from the row just above our current highest, up to the target.
        for (int y = highestGeneratedRow + 1; y <= targetTopRow; y++)
        {

            gridManager.CreateNewEndlessRow(y, gridWidth, this.obstacleChance, this.blockerChance, this.extraMoveSpawnChance, this.extraMoveCollectiblePrefab);

            // Update our boundary tracker.
            highestGeneratedRow = y;

            riverControls.CreateDropZonesForRow(y); // We will make CreateDropZonesForRow public.


            // We also need to tell the RiverControls to expand its lock states
            riverControls.InitializeLockStates(highestGeneratedRow + 1);
        }
    }

    private void DestroyOldRows(int targetBottomRow)
    {
        // Loop through all row indexes from the current bottom of the world up to the new target.
        for (int y = lowestGeneratedRow; y < targetBottomRow; y++)
        {
            // Ask the GridManager for the CURRENT, most up-to-date list of tiles in this row.
            List<TileInstance> tilesToDestroy = gridManager.GetTilesInRow(y);

            // If there are tiles to destroy, proceed.
            if (tilesToDestroy.Count > 0)
            {
                Debug.Log($"<color=red>Destroying {tilesToDestroy.Count} tiles in row {y}</color>");
                gridManager.DestroyEndlessRow(y, tilesToDestroy);
                riverControls.DestroyControlsForRow(y);
            }
        }

        // After the loop, update our boundary.
        lowestGeneratedRow = targetBottomRow;

        if (riverBankManager.GetBankGameObject(RiverBankManager.BankSide.Bottom) != null && lowestGeneratedRow > 0)
        {
            riverBankManager.DestroyBottomBank();
        }
    }




    public IEnumerator HandleEndlessPush(RowDropZone usedZone, TileType tileType, GameObject droppedTileGO)
    {
        // --- 1. PREPARE FOR THE PUSH & CLEANUP ---

        // Get the row and direction from the used zone.
        bool fromLeft = usedZone.fromLeft;
        int row = usedZone.row;

        // Immediately find and destroy the OTHER drop zone in the same row to prevent double-clicks.
        RowDropZone otherZone = riverControls.GetDropZone(row, !fromLeft);
        if (otherZone != null)
        {
            Destroy(otherZone.gameObject);
        }

        // The tile has been dropped, so it's no longer a "playable" hand tile. Remove the script.
        if (droppedTileGO.GetComponent<PlayableHandTile>() != null)
        {
            Destroy(droppedTileGO.GetComponent<PlayableHandTile>());
        }

        // --- 2. ANIMATE THE TILE FROM DROP POINT TO PUSH START POINT ---

        // This creates a smooth handoff from the player's drag to the system-controlled push.
        Vector3 dropPosition = droppedTileGO.transform.position;
        Vector3 pushStartPosition = gridManager.GetSpawnPosition(row, fromLeft);
        float handoffDuration = 0.2f;
        float elapsed = 0f;
        while (elapsed < handoffDuration)
        {
            elapsed += Time.deltaTime;
            droppedTileGO.transform.position = Vector3.Lerp(dropPosition, pushStartPosition, elapsed / handoffDuration);
            yield return null;
        }
        droppedTileGO.transform.position = pushStartPosition;
        droppedTileGO.transform.SetParent(gridManager.gridParent, true); // Parent it to the grid

        // --- 3. EXECUTE THE PUSH USING GRIDMANAGER ---

        // The GridManager needs a PuzzleHandTile object to know the tile's properties.
        // Since this is endless mode, the tile is "consumed" and doesn't come from a limited hand.
        PuzzleHandTile tileToPush = new PuzzleHandTile(tileType)
        {
            rotationY = droppedTileGO.transform.eulerAngles.y,
            isFlipped = (Mathf.RoundToInt(droppedTileGO.transform.eulerAngles.x) == 180)
        };

        // We call the third overload of PushRowCoroutine, which accepts a pre-made GameObject.
        yield return StartCoroutine(gridManager.PushRowCoroutine(row, fromLeft, tileToPush, droppedTileGO));

        // --- 4. FINAL CLEANUP ---

        // The push animation is complete. Now destroy the drop zone that was used to trigger it.
        if (usedZone != null)
        {
            Destroy(usedZone.gameObject);
        }

        // Now that the push is fully complete, spend the action point and update the world state.
        SpendActionPoint();
    }


    private IEnumerator GenerateNewRowsIfNeeded()
    {
        if (playerBoat == null || playerBoat.GetCurrentTile() == null) yield break;

        var boatCoords = gridManager.GetTileCoordinates(playerBoat.GetCurrentTile());
        int boatY = boatCoords.y;
        int highestRequiredRow = boatY + leadingBuffer;

        // This only calls the generation part, not the destruction part.
        if (highestRequiredRow > highestGeneratedRow)
        {
            GenerateMissingRows(highestRequiredRow);
        }
        yield return null;
    }


    private void CleanupOldRows()
    {
        if (playerBoat == null || playerBoat.GetCurrentTile() == null) return;

        var boatCoords = gridManager.GetTileCoordinates(playerBoat.GetCurrentTile());
        int boatY = boatCoords.y;
        int lowestAllowedRow = boatY - trailingBuffer;

        // This calls our existing, robust destruction logic.
        DestroyOldRows(lowestAllowedRow);
    }

    public void UpdateCameraTargetToBoatPosition()
    {
        if (playerBoat != null)
        {
            Vector3 boatCurrentPos = playerBoat.transform.position;
            // Update the camera's target to the boat's current Z position.
            cameraTargetPosition = new Vector3(0, 0, boatCurrentPos.z);
            Debug.Log($"[EndlessModeManager] Camera target updated after ejection to Z: {boatCurrentPos.z}");
        }
    }

    public void AddStamina(int amount)
    {
        currentStamina += amount;
        // We can add logic here for max stamina, visual effects, etc., later.
        UpdateStaminaUI();
    }


    private void EndGame()
    {
        // Make sure we only trigger the end game sequence once.
        if (!isPlayerTurn && !enabled) return; // A simple check to see if EndGame has already run.

        Debug.Log($"<color=red>--- GAME OVER ---</color> Final Score: {score}. High Score: {highScore}.");
        if (PlayerPrefs.HasKey(endlessSaveKey))
        {
            PlayerPrefs.DeleteKey(endlessSaveKey);
            Debug.Log("[Endless] Game over. Save file for completed run has been deleted.");
        }

        // Stop the main game loop and disable this manager.
        StopAllCoroutines();
        isPlayerTurn = false;
        enabled = false; // Disables this script, preventing further updates.

        // Make sure the boat is deselected and not interactive.
        if (playerBoat != null && playerBoat.isSelected)
        {
            playerBoat.DeselectBoat();
        }

        // Tell the UIManager to show the final score screen.
        if (uiManager != null)
        {
            uiManager.ShowEndlessScoreScreen(score, highScore);
        }
    }

    private void SaveEndlessRun()
    {
        // Safety check: Only save if a run is actually in progress and we have a boat.
        if (playerBoat == null)
        {
            Debug.LogWarning("[EndlessSave] Could not save run: Player boat not found.");
            return;
        }

        Debug.Log("<color=lightblue>[EndlessSave] Creating snapshot of current run...</color>");

        // 1. Create the data container
        EndlessStateSnapshot snapshot = new EndlessStateSnapshot();

        // 2. Populate Player & Boat Stats
        snapshot.currentStamina = this.currentStamina;
        snapshot.score = this.score;
        snapshot.boatStarsCollected = playerBoat.starsCollected;

        snapshot.boatPosition = new GoalData();
        if (playerBoat.GetCurrentTile() != null)
        {
            var coords = gridManager.GetTileCoordinates(playerBoat.GetCurrentTile());
            snapshot.boatPosition.isBankGoal = false;
            snapshot.boatPosition.tileX = coords.x;
            snapshot.boatPosition.tileY = coords.y;
            snapshot.boatPosition.snapPointIndex = playerBoat.GetCurrentSnapPoint();
        }
        else if (playerBoat.CurrentBank.HasValue)
        {
            snapshot.boatPosition.isBankGoal = true;
            snapshot.boatPosition.bankSide = playerBoat.CurrentBank.Value;
        }

        // 3. Populate World Boundaries
        snapshot.lowestGeneratedRow = this.lowestGeneratedRow;
        snapshot.highestGeneratedRow = this.highestGeneratedRow;

        // 4. Populate World State (Tiles & Collectibles)
        snapshot.tileStates = new List<TileSaveData>();
        snapshot.collectibleStates = new List<CollectibleSaveData>();

        // We only need to save the tiles that actually exist.
        for (int y = lowestGeneratedRow; y <= highestGeneratedRow; y++)
        {
            for (int x = 0; x < gridManager.cols; x++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile.originalTemplate != null)
                {
                    snapshot.tileStates.Add(new TileSaveData
                    {
                        tileTypeName = tile.originalTemplate.displayName,
                        gridX = x,
                        gridY = y,
                        rotationY = tile.transform.eulerAngles.y,
                        isFlipped = tile.IsReversed,
                        isHardBlocker = tile.IsHardBlocker
                    });

                    var collectible = tile.GetComponentInChildren<CollectibleInstance>();
                    if (collectible != null)
                    {
                        snapshot.collectibleStates.Add(new CollectibleSaveData
                        {
                            gridX = x,
                            gridY = y,
                            type = collectible.type,
                            value = collectible.value
                        });
                    }
                }
            }
        }
        Debug.Log($"[EndlessSave] Saving snapshot with {snapshot.tileStates.Count} tiles.");
        // 5. Convert to JSON and save
        string json = JsonUtility.ToJson(snapshot, true);
        PlayerPrefs.SetString(endlessSaveKey, json);
        PlayerPrefs.Save(); // Force a write to disk

        Debug.Log($"<color=lime>[EndlessSave] Run saved successfully. Stamina: {currentStamina}, Score: {score}.</color>");
    }

    public void ResumeEndlessMode()
    {
        StartCoroutine(ResumeEndlessModeCoroutine());
    }

    private IEnumerator ResumeEndlessModeCoroutine()
    {
        Debug.Log("<color=cyan>--- RESUMING ENDLESS MODE ---</color>");

        // 1. Load and Parse the Save File
        if (!PlayerPrefs.HasKey(endlessSaveKey))
        {
            Debug.LogError("[EndlessResume] Attempted to resume, but no save file was found! Starting new game as fallback.");
            yield return StartCoroutine(StartEndlessModeCoroutine());
            yield break; // Stop this coroutine
        }

        string json = PlayerPrefs.GetString(endlessSaveKey);
        EndlessStateSnapshot snapshot = JsonUtility.FromJson<EndlessStateSnapshot>(json);
        Debug.Log($"[EndlessResume] Loaded snapshot with {snapshot.tileStates.Count} tiles.");

        if (snapshot == null)
        {
            Debug.LogError("[EndlessResume] Failed to parse save file! Starting new game as fallback.");
            PlayerPrefs.DeleteKey(endlessSaveKey); // The file is corrupt, delete it.
            yield return StartCoroutine(StartEndlessModeCoroutine());
            yield break;
        }

        // --- Camera Cut (same as new game) ---
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            originalCameraBlend = brain.DefaultBlend;
            var cutBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.DefaultBlend = cutBlend;
        }
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.SwitchToEndlessView();
        }
        yield return null;


        // 2. Restore Player Stats & UI
        highScore = PlayerPrefs.GetInt(highScoreKey, 0);
        UpdateHighScoreUI();

        if (dropZoneCanvas == null)
        {
            GameObject canvasGO = new GameObject("DropZoneCanvas");
            canvasGO.transform.SetParent(this.transform);
            dropZoneCanvas = canvasGO.AddComponent<Canvas>();
            dropZoneCanvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<GraphicRaycaster>();
            dropZoneCanvas.worldCamera = Camera.main;
            dropZoneCanvas.transform.rotation = Quaternion.Euler(90, 0, 0);

            riverControls.SetDropZoneCanvas(dropZoneCanvas);
        }

        this.currentStamina = snapshot.currentStamina;
        this.score = snapshot.score;
        UpdateScoreUI();
        UpdateStaminaUI();

        // 3. Reconstruct the World
        this.lowestGeneratedRow = snapshot.lowestGeneratedRow;
        this.highestGeneratedRow = snapshot.highestGeneratedRow;

        int requiredGridHeight = highestGeneratedRow + 1;

        // Use a temporary list to build the grid in one go.
        List<TileSaveData> gridBlueprint = snapshot.tileStates;

        // GridManager creates the visual world. ExpandGrid first, then create.
        // gridManager.ExpandGridForEndless(requiredGridHeight); 

        // 1. Call the method and CAPTURE the list of running animations.
        List<Coroutine> gridAnimations = gridManager.CreateGridFromEditor(this.gridWidth, requiredGridHeight, gridBlueprint);

        // 2. WAIT for all of those animations to complete before proceeding.
        if (gridAnimations != null)
        {
            foreach (var anim in gridAnimations)
            {
                if (anim != null)
                {
                    yield return anim;
                }
            }
        }
        Debug.Log("[EndlessResume] Grid visual creation is complete. Now placing collectibles.");


        // Restore collectibles
        foreach (var collectibleData in snapshot.collectibleStates)
        {
            TileInstance tile = gridManager.GetTileAt(collectibleData.gridX, collectibleData.gridY);
            if (tile != null)
            {
                // We'll create this helper method in GridManager next, for now, let's assume it exists.
                // For now, this line will cause an error, which we will fix.
                PlaceCollectibleOnTile(tile, collectibleData.type, collectibleData.value);
            }
        }

        // Recreate banks and controls for the loaded state
        if (lowestGeneratedRow <= 0)
        {
            riverBankManager.CreateBottomBank();
        }
        riverControls.InitializeLockStates(requiredGridHeight);

        // Re-create all the drop zones for the active rows
        for (int y = lowestGeneratedRow; y <= highestGeneratedRow; y++)
        {
            riverControls.CreateDropZonesForRow(y);
        }

        // 4. Restore the Boat
        playerBoat = boatManager.SpawnBoatWithoutPositioning();
        if (playerBoat != null && snapshot.boatPosition != null)
        {
            playerBoat.SetCollectedStars(snapshot.boatStarsCollected);

            if (snapshot.boatPosition.isBankGoal)
            {
                playerBoat.MoveToBank(snapshot.boatPosition.bankSide);
            }
            else
            {
                TileInstance boatTile = gridManager.GetTileAt(snapshot.boatPosition.tileX, snapshot.boatPosition.tileY);
                if (boatTile != null)
                {
                    playerBoat.PlaceOnTile(boatTile, snapshot.boatPosition.snapPointIndex);
                }
                else
                {
                    Debug.LogError($"[EndlessResume] Could not find tile for boat at ({snapshot.boatPosition.tileX}, {snapshot.boatPosition.tileY}). Placing at bank.");
                    playerBoat.MoveToBank(RiverBankManager.BankSide.Bottom);
                }
            }
        }

        // 5. Finalize Camera and Game Loop
        if (cameraProxy != null && playerBoat != null && endlessVCam != null)
        {
            Vector3 boatStartPos = playerBoat.transform.position;
            cameraProxy.position = new Vector3(0, 0, boatStartPos.z);
            cameraTargetPosition = cameraProxy.position;
            endlessVCam.transform.position = new Vector3(0, cameraHeight, cameraProxy.position.z + cameraZOffset);
        }

        if (brain != null)
        {
            brain.DefaultBlend = originalCameraBlend;
        }

        // 6. Start the game loop
        Debug.Log("<color=lime>Resume successful. Initializing player turn.</color>");

        // --- FIX 2: MANUALLY START THE PLAYER'S TURN ---
        isPlayerTurn = true;
        currentAP = Mathf.Min(apPerCycle, currentStamina);
        UpdateAPUI();

        if (playerBoat != null)
        {
            playerBoat.SelectBoat();
        }

        // Set the flag and start the main loop.
        skipFirstForecast = true;
        StartCoroutine(EndlessGameLoop());
    
}

    private void PlaceCollectibleOnTile(TileInstance tile, CollectibleType type, int value)
    {
        GameObject prefabToSpawn = null;
        switch (type)
        {
            case CollectibleType.ExtraMove:
                prefabToSpawn = extraMoveCollectiblePrefab;
                break;
                // Add other collectible types here if you have them, e.g., Star
        }

        if (prefabToSpawn != null)
        {
            Vector3 spawnPos = tile.transform.position + Vector3.up * 0.25f; // Place it slightly above the tile
            GameObject collectibleGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, tile.transform);
            var instance = collectibleGO.GetComponent<CollectibleInstance>();
            if (instance != null)
            {
                instance.type = type;
                instance.value = value;
            }
        }
    }




}