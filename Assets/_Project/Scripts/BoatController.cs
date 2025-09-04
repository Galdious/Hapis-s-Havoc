/*
 *  BoatController.cs - v03
 *  ---------------------------------------------------------------
 *  - VERSION 03: Fixes the bank material bug.
 *
 *  - This script is based on the working version 02.
 *  - CHANGE: The logic for highlighting and clearing highlights on BANKS
 *    has been updated to correctly use sharedMaterial, mirroring the
 *    successful fix for the tiles. This prevents banks from staying cyan.
 *  - No other code has been modified.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Linq;
using TMPro;

public class BoatController : MonoBehaviour, IPointerClickHandler
{
    [Header("Boat Settings")]
    public float snapOffset = 0.15f;
    public float moveSpeed = 1f;
    public float hoverHeight = 0.5f;

    public float aboveTileHoverDistance = 0.1f; // How high the boat hovers above the tile when selected.
    public float bobAmount = 0.1f;
    public float bobSpeed = 2f;

    [Header("Tile Animation")]
    public float tileLiftDuration = 0.4f;
    public float tileLiftDelay = 0.2f;
    public AnimationCurve tileLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);




    [Header("Ejection Animation")]
    [Tooltip("A short delay before the boat begins its settle animation after being ejected.")]
    public float ejectionSettleDelay = 0.2f; // You can adjust this value
    [Tooltip("How high above its final position the boat appears before settling.")]
    public float settleStartHeight = 1.5f;
    [Tooltip("How long the settle and fade-in animation takes.")]
    public float settleDuration = 0.6f;
    [Tooltip("How long the boat's fade-out takes when it is ejected from a falling tile.")]
    public float ejectionFadeOutDuration = 0.2f; // A quick fade




    [Header("Movement System")]
    public int maxMovementPoints = 3;
    public int currentMovementPoints = 3;
    [Tooltip("The number of extra moves awarded per red tile skipped during a forward move.")]
    public int moveBonusMultiplier = 2;


    [Header("Gameplay State")]
    public int starsCollected { get; private set; } = 0; // inventory logic
    public int extraMovesCollected { get; private set; } = 0;

    [Header("Visual Feedback")]
    public Color selectedColor = Color.magenta;
    public TMP_Text starCounterText;
    public TMP_Text moveCounterText;

    [Header("Embarking Visuals")]
    [SerializeField] private GameObject embarkArrowPrefab;
    [SerializeField] private float arrowHoverHeight = 0.3f;


    [Header("Debug")]
    public bool showDebugInfo = true;

    public TileInstance GetCurrentTile() => currentTile;

    public int GetCurrentSnapPoint() => currentSnapPoint;

    public RiverBankManager.BankSide? CurrentBank { get; private set; } = null;

    // --- State & References ---
    private TileInstance currentTile;
    private int currentSnapPoint = -1;
    private Transform bankPosition;
    private bool isAtBank = true;
    public bool isSelected = false;
    private bool isMoving = false;

    // --- ADD THESE NEW FIELDS ---
    private MeshRenderer boatRenderer;
    private Color opaqueColor;

    private Vector3 originalBoatPosition;
    private bool isBobbing = false;
    private BoatManager boatManager;
    private GridManager gridManager;
    private RiverBankManager riverBankManager;
    private GameManager gameManager;
    private EndlessModeManager endlessManager;

    private List<TileInstance> validMoves = new List<TileInstance>();
    private List<GameObject> highlightedTiles = new List<GameObject>();
    private List<GameObject> highlightedBanks = new List<GameObject>();
    private Dictionary<TileInstance, int> tileToSnapPoint = new Dictionary<TileInstance, int>();
    private Dictionary<TileInstance, int> tileToReverseSnapPoint = new Dictionary<TileInstance, int>();


    private Dictionary<Renderer, Material> originalMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Material> originalBankMaterials = new Dictionary<Renderer, Material>();
    private Dictionary<TileInstance, List<TileInstance>> reversedPathways = new Dictionary<TileInstance, List<TileInstance>>();
    private Dictionary<TileInstance, List<int>> bankEntrySnapPoints = new Dictionary<TileInstance, List<int>>();


    void Awake()
    {
        boatManager = FindFirstObjectByType<BoatManager>();
        gridManager = FindFirstObjectByType<GridManager>();
        riverBankManager = FindFirstObjectByType<RiverBankManager>();

        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            endlessManager = FindFirstObjectByType<EndlessModeManager>();
        }

        boatRenderer = GetComponentInChildren<MeshRenderer>();
        if (boatRenderer != null)
        {
            opaqueColor = boatRenderer.material.color;
        }
    }


    void Start()
    {


        UpdateStarCounterUI();
        UpdateMoveCounterUI();

        if (gridManager == null) Debug.LogError("[BoatController] GridManager not found!");
        if (riverBankManager == null) Debug.LogError("[BoatController] RiverBankManager not found!");
    }

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame) EndMovementTurn();
        if (Keyboard.current.rKey.wasPressedThisFrame) ResetMovementPoints();
    }
    private void UpdateStarCounterUI()
    {
        if (starCounterText != null)
        {
            starCounterText.text = $"Stars: {starsCollected}";
        }
    }

    public void UpdateMoveCounterUI()
    {
        if (moveCounterText != null)
        {
            moveCounterText.text = $"Moves: {currentMovementPoints}";
        }
    }

    /// Sets the boat's internal state to be on a specific tile without moving the transform.
    /// This is used for initialization.
    public void InitializeStateOnTile(TileInstance tile, int snapPointIndex)
    {
        if (tile == null) return;

        currentTile = tile;
        currentSnapPoint = snapPointIndex;
        isAtBank = false;
        bankPosition = null;
        CurrentBank = null;
    }

    public IEnumerator FadeOutForEjection()
    {
        if (boatRenderer == null) yield break;

        boatRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        isBobbing = false;
        float elapsed = 0f;
        Color startColor = boatRenderer.material.color;

        while (elapsed < ejectionFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / ejectionFadeOutDuration);

            // Lerp towards a version of the OPAQUE color with zero alpha
            Color targetColor = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
            boatRenderer.material.color = Color.Lerp(startColor, targetColor, progress);

            yield return null;
        }

        // Ensure it is fully transparent at the end
        boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
    }

    private IEnumerator FadeOutCoroutine()
    {
        isBobbing = false;
        float elapsed = 0f;
        Color startColor = boatRenderer.material.color; // Read current color for smooth start

        while (elapsed < ejectionFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / ejectionFadeOutDuration);

            // Lerp towards a version of the OPAQUE color with zero alpha
            Color targetColor = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
            boatRenderer.material.color = Color.Lerp(startColor, targetColor, progress);

            yield return null;
        }
        // Ensure it is fully transparent at the end
        boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);
    }


    public void SetAtBank(Transform bankSpawnPoint)
    {
        bankPosition = bankSpawnPoint;
        isAtBank = true;
        currentTile = null;
        currentSnapPoint = -1;
        CurrentBank = bankSpawnPoint.name.Contains("Top") ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;
        transform.position = bankSpawnPoint.position;
        transform.rotation = bankSpawnPoint.rotation;
    }

    public void MoveToBank(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null) return;

        // This is a simplified, immediate version of the movement coroutine
        Transform targetSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        if (targetSpawn != null)
        {
            SetAtBank(targetSpawn);
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isMoving) return;

        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            // In Endless Mode, don't select directly. Ask the manager.
            if (endlessManager != null)
            {
                endlessManager.OnBoatClicked();
            }
            return; // Stop further execution in this method for endless mode
        }




        if (gridManager != null && gridManager.isPuzzleMode && currentMovementPoints <= 0)
        {
            Debug.Log("Out of moves. Cannot select boat.");
            return; // Exit the method immediately.
        }

        if (!isSelected) SelectBoat();
        else DeselectBoat();
    }




    private void SetStateForBank(Transform bankSpawnPoint)
    {
        // This method ONLY sets the internal state variables for being at a bank.
        // It does NOT touch the transform's position or rotation, leaving that to the animation.
        bankPosition = bankSpawnPoint;
        isAtBank = true;
        currentTile = null;
        currentSnapPoint = -1;
        CurrentBank = bankSpawnPoint.name.Contains("Top") ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;
    }






    // This new public method will be called by GridManager.
    public IEnumerator AnimateToNewPositionAfterEjection(TileInstance tile, int snapPoint)
    {
        // CHANGE 2: We now "yield return" the coroutine so the calling script can wait for it.
        yield return StartCoroutine(SettleAndFadeInCoroutine(tile, snapPoint, null));
    }

    // Overload for moving to a bank.
    // CHANGE 1: The return type is now IEnumerator.
    public IEnumerator AnimateToNewPositionAfterEjection(Transform bankSpawn)
    {
        // CHANGE 2: We now "yield return" the coroutine.
        yield return StartCoroutine(SettleAndFadeInCoroutine(null, -1, bankSpawn));
    }


    // CHANGE 1: The return type is now IEnumerator.
    public IEnumerator AnimateToNewPositionAfterEjection(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null)
        {
            Debug.LogError("[BoatController] Cannot animate to bank, RiverBankManager is missing!");
            // If the RBM is gone, we can't find a bank. This is a game-over state in Endless.
            if (endlessManager != null)
            {
                endlessManager.TriggerGameOverByEjection();
            }
            yield break;
        }

        Transform bankSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);

        // --- THIS IS THE CRITICAL FIX ---
        if (bankSpawn != null)
        {
            // A valid landing spot exists. Animate the boat to it.
            yield return StartCoroutine(AnimateToNewPositionAfterEjection(bankSpawn));
        }
        else
        {
            // The bank for this side has been destroyed. There is nowhere to land.
            // Tell the EndlessModeManager to end the game.
            if (endlessManager != null)
            {
                // We found the manager, now press the big red button.
                endlessManager.TriggerGameOverByEjection();
            }
            else
            {
                // Failsafe in case we can't find the manager.
                Debug.LogError("[BoatController] Ejected into a void, but could not find EndlessModeManager to trigger game over!");
            }
            // Use yield break because there's no animation to perform.
            yield break;
        }
        // --- END OF FIX ---
    }


    private IEnumerator SettleAndFadeInCoroutine(TileInstance destinationTile, int snapPoint, Transform bankSpawn)
    {
        // 1. Wait for the settle delay if there is one.
        if (ejectionSettleDelay > 0)
        {
            yield return new WaitForSeconds(ejectionSettleDelay);
        }

        // Safety check
        if (boatRenderer == null) yield break;

        // 2. Make sure the boat is fully transparent before we start.
        // Use the cached 'opaqueColor' to maintain the correct RGB values.
        boatRenderer.material.color = new Color(opaqueColor.r, opaqueColor.g, opaqueColor.b, 0f);

        // 3. Determine the final destination position and rotation.
        Vector3 finalPos;
        Quaternion finalRot;

        if (destinationTile != null)
        {
                        Vector3 snapPosition = destinationTile.snapPoints[snapPoint].position;

            // --- NEW AXIS-ALIGNED OFFSET LOGIC ---
            switch (snapPoint)
            {
                case 0: case 1:
                    finalPos = snapPosition - (destinationTile.transform.forward * snapOffset);
                    break;
                case 2: case 3:
                    finalPos = snapPosition + (destinationTile.transform.forward * snapOffset);
                    break;
                case 4:
                    finalPos = snapPosition - (destinationTile.transform.right * snapOffset);
                    break;
                case 5:
                    finalPos = snapPosition + (destinationTile.transform.right * snapOffset);
                    break;
                default:
                    Vector3 tileCenter = destinationTile.transform.position;
                    Vector3 direction = (snapPosition - tileCenter).normalized;
                    finalPos = snapPosition - direction * snapOffset;
                    break;
            }
            // --- END OF NEW LOGIC ---

            finalRot = GetSnapPointRotation(destinationTile, snapPoint);
        }
        else
        {
            finalPos = bankSpawn.position;
            finalRot = bankSpawn.rotation;
        }

        // 4. Set the starting position for the animation (above the final spot).
        Vector3 startPos = finalPos + Vector3.up * settleStartHeight;
        transform.position = startPos;
        transform.rotation = finalRot;

        // 5. Animate the movement and fade-in over time.
        float elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / settleDuration);

            // Animate position from the start point to the final point.
            transform.position = Vector3.Lerp(startPos, finalPos, progress);

            // Animate the material color from its current state towards the fully opaque color.
            boatRenderer.material.color = Color.Lerp(boatRenderer.material.color, opaqueColor, progress);

            yield return null;
        }

        // 6. Finalize the state to ensure perfect placement and appearance.
        transform.position = finalPos;
        boatRenderer.material.color = opaqueColor;

        boatRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        if (destinationTile != null)
        {
            PlaceOnTile(destinationTile, snapPoint);
        }
        else
        {
            SetStateForBank(bankSpawn);
        }



    }
    public void SelectBoat()
    {
        ResynchronizeStateWithTransform(); // Sanity check to fix state after a river push.

        // First, clear any highlights that might exist from a previous state.
        // This wipes the slate clean before we do anything else.
        ClearHighlights();

        // --- The rest of the method is the same as before ---
        if (boatManager != null) boatManager.SetSelectedBoat(this);

        isSelected = true;


        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            // Do nothing related to movement points. Just proceed.
        }
        else
        {
            // This is the original logic for Puzzle/Editor mode. It can stay.
            if (gridManager == null || !gridManager.isPuzzleMode)
            {
                if (currentMovementPoints <= 0)
                {
                    currentMovementPoints = maxMovementPoints;
                }
            }
        }


        StartCoroutine(LiftAndBobBoat(true));
        FindValidMoves();
        StartCoroutine(HighlightValidMovesWithDelay());
        UpdateMoveCounterUI();
    }

    public void DeselectBoat()
    {
        if (boatManager != null) boatManager.ClearSelectedBoat();
        isSelected = false;

        // We will no longer call StopAllCoroutines() here, as it's too aggressive
        // and interrupts the tile-lowering animations that are part of ClearHighlights.
        // StopAllCoroutines(); // <<< REMOVE OR COMMENT OUT THIS LINE

        StartCoroutine(LiftAndBobBoat(false)); // This lowers the boat itself.
        ClearHighlights(); // This handles lowering the tiles and removing click handlers.
    }

    public void CheckForCollectibleOnCurrentTile()
    {
        // Only run if the boat is actually on a tile.
        if (isAtBank || currentTile == null) return;

        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless && endlessManager == null)
        {
            endlessManager = FindFirstObjectByType<EndlessModeManager>();
        }

        var collectible = currentTile.GetComponentInChildren<CollectibleInstance>();
        if (collectible != null)
        {
            Debug.Log($"Landed on a {collectible.type}!");
            switch (collectible.type)
            {
                case CollectibleType.Star:
                    starsCollected++;
                    UpdateStarCounterUI();
                    FloatingTextManager.Instance.ShowText("+1", FloatingTextManager.FloatingTextType.StarGain, transform.position);
                    Debug.Log($"Collected a Star! Total stars: {starsCollected}");
                    break;

                case CollectibleType.ExtraMove:
                    extraMovesCollected++;

                    // Check which mode we are in.
                    if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
                    {
                        EndlessModeManager manager = FindFirstObjectByType<EndlessModeManager>();
                        if (manager != null)
                        {
                            manager.AddStamina(collectible.value);
                            FloatingTextManager.Instance.ShowText($"+{collectible.value}", FloatingTextManager.FloatingTextType.MoveGain, transform.position);
                            Debug.Log($"<color=green>ENDLESS:</color> Collected an Extra Move! Gained {collectible.value} Stamina.");
                        }
                        else
                        {
                            Debug.LogError("Could not find EndlessModeManager to add stamina!");
                        }

                    }
                    else
                    {
                        // PUZZLE MODE: Add to Movement Points (the original logic)
                        currentMovementPoints += collectible.value;
                        UpdateMoveCounterUI();
                        FloatingTextManager.Instance.ShowText($"+{collectible.value}", FloatingTextManager.FloatingTextType.MoveGain, transform.position);
                        Debug.Log($"<color=yellow>PUZZLE:</color> Collected an Extra Move! Gained {collectible.value} moves. Current moves: {currentMovementPoints}");
                    }
                    break;
            }

            // Destroy the collectible from the scene after pickup
            Destroy(collectible.gameObject);
        }
    }


    IEnumerator LiftAndBobBoat(bool lift)
    {
        float baseY = isAtBank && bankPosition != null ? bankPosition.position.y : 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos;
        targetPos.y = lift ? baseY + hoverHeight + aboveTileHoverDistance : baseY;

        float liftDuration = 0.3f;
        float elapsed = 0f;

        while (elapsed < liftDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, tileLiftCurve.Evaluate(elapsed / liftDuration));
            yield return null;
        }

        transform.position = targetPos;
        originalBoatPosition = targetPos;

        isBobbing = lift;
        if (lift) StartCoroutine(BobBoat());
    }

    IEnumerator BobBoat()
    {
        while (isBobbing && isSelected)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            Vector3 bobPos = originalBoatPosition;
            bobPos.y += bobOffset;
            transform.position = bobPos;
            yield return null;
        }
    }

    IEnumerator HighlightValidMovesWithDelay()
    {
        if (GameManager.Instance.currentState == GameState.LevelComplete) yield break;
        
        yield return new WaitForSeconds(tileLiftDelay);
        if (!isSelected) yield break;

        // --- 1. Highlight "You Are Here" Paths (On the CURRENT Tile) ---
        if (!isAtBank && currentTile != null)
        {
            var currentTileVisualizer = currentTile.GetComponent<PathVisualizer>();
            if (currentTileVisualizer != null)
            {
                // Find all connections on the current tile that involve our boat's snap point.
                foreach (var connection in currentTile.connections)
                {
                    if (connection.from == currentSnapPoint || connection.to == currentSnapPoint)
                    {
                        // For solid highlights, the order doesn't matter.
                        currentTileVisualizer.HighlightPath(connection.from, connection.to, false);
                    }
                }
            }
        }

        // --- 2. Highlight "You Can Go Here" Tiles AND Their Paths ---
        foreach (TileInstance destinationTile in validMoves)
        {
            if (destinationTile != null)
            {
                // This part is the same: lift the tile and add the click handler.
                HighlightTile(destinationTile);

                // Get the visualizer for the destination tile.
                var destinationVisualizer = destinationTile.GetComponent<PathVisualizer>();
                if (destinationVisualizer == null) continue; // Skip if no visualizer

                // --- NEW IF/ELSE BLOCK ---
                if (isAtBank)
                {
                    // We are at a bank. Use our new dictionary of entry points.
                    if (bankEntrySnapPoints.ContainsKey(destinationTile))
                    {
                        List<int> entryPoints = bankEntrySnapPoints[destinationTile];
                        foreach (int entrySnap in entryPoints)
                        {
                            // For each entry point, find its connections on the tile and highlight them.
                            foreach (var connection in destinationTile.connections)
                            {
                                if (connection.from == entrySnap || connection.to == entrySnap)
                                {
                                    int otherSnap = (connection.from == entrySnap) ? connection.to : connection.from;
                                    destinationVisualizer.HighlightPath(entrySnap, otherSnap, true); // Gradient highlight
                                }
                            }
                        }
                    }
                }
                else
                {
                    // We are on a tile. Use the existing river-to-river logic.
                    int landingSnapPoint = -1;
                    if (tileToSnapPoint.ContainsKey(destinationTile))
                    {
                        landingSnapPoint = tileToSnapPoint[destinationTile];
                    }
                    else if (tileToReverseSnapPoint.ContainsKey(destinationTile))
                    {
                        landingSnapPoint = tileToReverseSnapPoint[destinationTile];
                    }

                    if (landingSnapPoint != -1)
                    {
                        foreach (var connection in destinationTile.connections)
                        {
                            if (connection.from == landingSnapPoint || connection.to == landingSnapPoint)
                            {
                                int otherSnap = (connection.from == landingSnapPoint) ? connection.to : connection.from;
                                destinationVisualizer.HighlightPath(landingSnapPoint, otherSnap, true);
                            }
                        }
                    }
                }
                // --- END OF NEW IF/ELSE BLOCK ---
            }
        }
    
    }

    void FindValidMoves()
    {
        validMoves.Clear();
        tileToSnapPoint.Clear();
        tileToReverseSnapPoint.Clear();
        bankEntrySnapPoints.Clear(); 
        reversedPathways.Clear(); 

        if (isAtBank) FindBankEntryMoves();
        else if (currentTile != null) FindRiverPathMoves();

        if (currentTile != null) validMoves.Remove(currentTile);
    }

    void FindRiverPathMoves()
    {
        if (currentTile == null || currentSnapPoint < 0) return;

        // Check all forward paths defined by the tile's connections
        foreach (var connection in currentTile.connections)
        {
            int exitSnap = (connection.from == currentSnapPoint) ? connection.to : (connection.to == currentSnapPoint ? connection.from : -1);
            if (exitSnap != -1)
            {
                FindMoveAtEndOfChain(currentTile, exitSnap, isReverseMove: false);
            }
        }
        // Also check the path for reversing your last move
        FindMoveAtEndOfChain(currentTile, currentSnapPoint, isReverseMove: true);
    }

    public void ApplyPenaltiesForForcedMove(List<TileInstance> crossedTiles)
    {
        if (crossedTiles == null || crossedTiles.Count == 0) return;
        Debug.Log($"[BoatController] Ejected! Crossed {crossedTiles.Count} reversed tiles during forced move.");
        foreach (var tile in crossedTiles)
        {
            Debug.Log($"-- Applying placeholder penalty for crossing {tile.name}!");
        }
    }

    void FindMoveAtEndOfChain(TileInstance startTile, int exitSnap, bool isReverseMove)
    {
        TileInstance currentSearchTile = startTile;
        int currentExitSnap = exitSnap;
        List<TileInstance> crossedReversedTiles = new List<TileInstance>();

        for (int i = 0; i < gridManager.cols + 2; i++) // Safety break
        {
            TileInstance neighbor = FindConnectedTile_HorizontalOnly(currentSearchTile, currentExitSnap);

            if (neighbor == null)
            {
                // --- NEW DIRECTIONAL LOGIC ---
                var (x, y) = GetTileCoordinates(currentSearchTile);

                // Get the world-space direction vector from the tile's center to the exit snap point.
                Vector3 exitDirection = (currentSearchTile.snapPoints[currentExitSnap].position - currentSearchTile.transform.position).normalized;

                // To dock at the TOP bank, the boat must be on the top row AND the path must be exiting upwards (positive Z).
                if (y == gridManager.rows - 1 && exitDirection.z > 0.1f) // Use a small threshold
                {
                    HighlightBankForDocking(RiverBankManager.BankSide.Top);
                }
                // To dock at the BOTTOM bank, the boat must be on the bottom row AND the path must be exiting downwards (negative Z).
                else if (y == 0 && exitDirection.z < -0.1f) // Use a small threshold
                {
                    HighlightBankForDocking(RiverBankManager.BankSide.Bottom);
                }
                // --- END OF NEW LOGIC ---
                return;
            }

            if (neighbor.IsReversed)
            {
                if (neighbor.IsHardBlocker) return;

                crossedReversedTiles.Add(neighbor);
                int entrySnap = FindConnectedSnapPoint_HorizontalOnly(currentSearchTile, currentExitSnap, neighbor);
                currentExitSnap = GetOppositeSnapPoint(entrySnap);
                currentSearchTile = neighbor;
            }
            else
            {
                // This method already accounts for the neighbor's rotation by finding the
                // physically closest connecting snap point. Its result is the source of truth.
                int landingSnap = FindConnectedSnapPoint_HorizontalOnly(currentSearchTile, currentExitSnap, neighbor);

                if (landingSnap != -1)
                {
                    // We do not need to transform or mirror 'landingSnap'. It is already the correct
                    // logical index for the neighbor tile, regardless of its rotation.

                    if (!validMoves.Contains(neighbor)) validMoves.Add(neighbor);

                    // Store the DIRECT result from the connection finder.
                    if (isReverseMove)
                    {
                        tileToReverseSnapPoint[neighbor] = landingSnap;
                    }
                    else
                    {
                        tileToSnapPoint[neighbor] = landingSnap;
                    }

                    if (crossedReversedTiles.Count > 0)
                    {
                        reversedPathways[neighbor] = new List<TileInstance>(crossedReversedTiles);
                    }
                }
                return; // We found a valid blue tile, so we stop searching down this chain.

            }
        }
    }




    /// Instantly stops animations and updates internal state for a forced move (like ejection).
    /// This does NOT animate the boat, leaving it frozen for another script to control.
    public void PrepareForForcedMove()
    {
        if (boatManager != null) boatManager.ClearSelectedBoat();

        isSelected = false;
        isBobbing = false;
        StopAllCoroutines();

        ClearHighlights();

        // We intentionally do NOT call LiftAndBobBoat or ClearHighlights here.
        // We want the boat to remain visually where it is, and highlights will be
        // cleared by the GridManager/ejection logic later.
        Debug.Log($"[BoatController] {name} prepared for forced move.");
    }





    // Helper to find the opposite snap point for straight-line travel.
    int GetOppositeSnapPoint(int snap)
    {
        switch (snap)
        {
            case 0: return 2;
            case 1: return 3;
            case 2: return 0;
            case 3: return 1;
            case 4: return 5;
            case 5: return 4;
            default: return -1;
        }
    }

    (int, int) GetTileCoordinates(TileInstance tile)
    {
        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                if (gridManager.GetTileAt(x, y) == tile)
                    return (x, y);
            }
        }
        return (-1, -1);
    }


    void HighlightBankForDocking(RiverBankManager.BankSide side)
    {
        if (riverBankManager == null) return;

        GameObject bankGO = riverBankManager.GetBankGameObject(side);
        if (bankGO != null && !highlightedBanks.Contains(bankGO))
        {
            var renderer = bankGO.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                // Correctly save the original shared material before changing the color.
                if (!originalBankMaterials.ContainsKey(renderer))
                {
                    originalBankMaterials[renderer] = renderer.sharedMaterial;
                }
                renderer.material.color = Color.cyan;

                highlightedBanks.Add(bankGO);
            }

            var clicker = renderer.gameObject.AddComponent<BankClickHandler>();
            clicker.targetBoat = this;
            clicker.bankSide = side;
        }
    }


    void ClearHighlights()
    {
        // 1. Clear highlights on the boat's current tile (the "You Are Here" paths).
        if (currentTile != null)
        {
            // The ?. operator is a safe way to call a method on a component that might be null.
            currentTile.GetComponent<PathVisualizer>()?.ClearAllHighlights();
        }

        // 2. Clear highlights on all the valid destination tiles.
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                tileGO.GetComponent<PathVisualizer>()?.ClearAllHighlights();
            }
        }


        // Restore tile materials
        foreach (var pair in originalMaterials)
        {
            if (pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
            }
        }

        // Restore bank materials
        foreach (var pair in originalBankMaterials)
        {
            if (pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
            }
        }

        // --- Clean Up GameObjects in ONE PASS ---
        foreach (GameObject tileGO in highlightedTiles)
        {
            if (tileGO != null)
            {
                // 1. Find the arrows and tell them to start fading out.
                // The arrow's own script will handle its destruction after the animation.
                EmbarkArrow[] arrows = tileGO.GetComponentsInChildren<EmbarkArrow>();
                foreach (EmbarkArrow arrow in arrows)
                {
                    arrow.TriggerFadeOutAndDestroy();
                }

                // 2. Animate the tile lowering.
                StartCoroutine(LiftTileSmooth(tileGO.GetComponent<TileInstance>(), false));

                // 3. Schedule the click handler for destruction at the end of the frame.
                // We use the gentle Destroy(), NOT DestroyImmediate().
                var clicker = tileGO.GetComponent<SimpleTileClickHandler>();
                if (clicker != null)
                {
                    Destroy(clicker);
                }
            }
        }
        
        foreach (GameObject bankGO in highlightedBanks)
        {
            if (bankGO != null)
            {
                var clicker = bankGO.GetComponent<BankClickHandler>();
                if (clicker != null) Destroy(clicker);
            }
        }

        highlightedTiles.Clear();
        highlightedBanks.Clear();
        originalMaterials.Clear();
        originalBankMaterials.Clear();
    }

        
public void OnTileClicked(TileInstance clickedTile, PointerEventData eventData)
    {

        if (reversedPathways.ContainsKey(clickedTile))
        {
            // A "skip" move across red tiles has been detected.

            // First, we need to ensure we can get the grid coordinates to check our "forward progress" rule.
            if (gridManager != null && currentTile != null)
            {
                var startCoords = gridManager.GetTileCoordinates(currentTile);
                var endCoords = gridManager.GetTileCoordinates(clickedTile);

                // This is the "Forward Progress" rule you designed:
                // We only grant a bonus if the destination row is the same as or higher than the start row.
                if (endCoords.y >= startCoords.y)
                {
                    // Calculate the bonus.
                    int tilesSkipped = reversedPathways[clickedTile].Count;
                    int bonusAmount = tilesSkipped * moveBonusMultiplier;

                    // Only proceed if a bonus was actually earned.
                    if (bonusAmount > 0)
                    {
                        // Now, check which game mode we are in to award the correct resource.
                        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
                        {
                            // In ENDLESS MODE, we award STAMINA.
                            if (endlessManager != null)
                            {
                                endlessManager.AddStamina(bonusAmount);
                                FloatingTextManager.Instance.ShowSkipBonus(bonusAmount, transform.position);
                                Debug.Log($"<color=green>ENDLESS SKIP BONUS!</color> Gained {bonusAmount} Stamina for skipping {tilesSkipped} tiles.");
                            }
                        }
                        else
                        {
                            // In PUZZLE MODE, we award standard MOVEMENT POINTS.
                            // currentMovementPoints += bonusAmount;
                            // UpdateMoveCounterUI();
                            // FloatingTextManager.Instance.ShowText("Skip Bonus!", FloatingTextManager.FloatingTextType.Neutral, transform.position);
                            // FloatingTextManager.Instance.ShowText($"+{bonusAmount}", FloatingTextManager.FloatingTextType.MoveGain, transform.position, 0.3f); // 0.3s delay
                            // Debug.Log($"<color=yellow>PUZZLE SKIP BONUS!</color> Gained {bonusAmount} moves for skipping {tilesSkipped} tiles.");

                            // In PUZZLE MODE, the move across red tiles is allowed, but no bonus is awarded.
                            // It's a strategic positional play, not a resource gain.
                            
                            Debug.Log($"<color=yellow>PUZZLE:</color> Successfully crossed {tilesSkipped} red tiles. No bonus awarded.");
    
                        }
                    }
                }
                else
                {
                    // The move was backwards, so we log it but award no bonus.
                    Debug.Log("Crossed red tiles moving backward. No skip bonus awarded.");
                }
            }
        }



        if (isMoving || !isSelected || !validMoves.Contains(clickedTile) || (gameManager != null && gameManager.currentMode != OperatingMode.Endless && currentMovementPoints <= 0))
        {
            // Add a detailed log to see which condition is the problem.
            Debug.LogWarning($"OnTileClicked IGNORED. Reason: " +
                            $"isMoving={isMoving}, " +
                            $"!isSelected={!isSelected}, " +
                            $"!validMoves.Contains={!validMoves.Contains(clickedTile)}, " +
                            $"outOfMoves={(gameManager != null && gameManager.currentMode != OperatingMode.Endless && currentMovementPoints <= 0)}");
            return;
        }


        // HistoryManager.Instance.SaveState();

        isMoving = true;


        if (isAtBank)
        {
            MoveFromBankToTile(clickedTile, DetermineSnapPointFromClick(clickedTile));
        }
        else
        {
            int targetSnapPoint = DetermineRiverSnapPoint(clickedTile, eventData);
            if (targetSnapPoint != -1) MoveFromTileToTile(clickedTile, targetSnapPoint);
        }
    }

    public void OnBankClicked(RiverBankManager.BankSide side)
    {
        if (isMoving || !isSelected || currentMovementPoints <= 0) return;

        // HistoryManager.Instance.SaveState();

        isMoving = true;

        StopAllCoroutines();

        Transform targetSpawn = riverBankManager.GetNearestSpawnPoint(side, transform.position);
        StartCoroutine(MoveToBankCoroutine(targetSpawn));
    }

    public void SetCollectedStars(int amount)
    {
        starsCollected = amount;
        UpdateStarCounterUI();
    }

    IEnumerator RefreshMovementOptionsCoroutine()
    {

        yield return null;

        HistoryManager.Instance.SaveState();
        isMoving = false;

        // After every move, just ask the GameManager to evaluate the situation.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EvaluateGameStateAfterMove(this);
        }
        // --- END OF NEW CALL ---

        // Check if we are in puzzle mode before doing anything else.
        if (gridManager != null && gridManager.isPuzzleMode)
        {
            if (currentMovementPoints <= 0)
            {
                Debug.Log("PUZZLE MODE: Out of moves! Turn ended.");
                DeselectBoat();
                yield break;
            }
        }



        // After a move, check if we are out of points.
        if (currentMovementPoints <= 0)
        {
            // If we have no points left, automatically end the turn.
            // DeselectBoat() will handle lowering the boat and clearing highlights.
            DeselectBoat();
        }
        else
        {
            // If we still have points, find and show the next set of moves.
            ClearHighlights();
            FindValidMoves();
            StartCoroutine(HighlightValidMovesWithDelay());
        }
    }



    int DetermineRiverSnapPoint(TileInstance tile, PointerEventData eventData)
    {
        bool isPath = tileToSnapPoint.ContainsKey(tile);
        bool isReverse = tileToReverseSnapPoint.ContainsKey(tile);

        if (isPath && isReverse)
        {
            Ray ray = eventData.pressEventCamera.ScreenPointToRay(eventData.position);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                int continueSnapIndex = tileToSnapPoint[tile];
                Vector3 continueSnapPos = tile.snapPoints[continueSnapIndex].position;
                int reverseSnapIndex = tileToReverseSnapPoint[tile];
                Vector3 reverseSnapPos = tile.snapPoints[reverseSnapIndex].position;

                float distanceToContinue = Vector3.Distance(hit.point, continueSnapPos);
                float distanceToReverse = Vector3.Distance(hit.point, reverseSnapPos);

                return (distanceToContinue < distanceToReverse) ? continueSnapIndex : reverseSnapIndex;
            }
        }
        else if (isPath) return tileToSnapPoint[tile];
        else if (isReverse) return tileToReverseSnapPoint[tile];

        return -1;
    }

    TileInstance FindConnectedTile(TileInstance fromTile, int snapPointIndex)
    {
        Vector3 snapPosition = fromTile.snapPoints[snapPointIndex].position;
        float minDistance = float.MaxValue;
        TileInstance closestTile = null;
        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile != fromTile)
                {
                    for (int i = 0; i < tile.snapPoints.Length; i++)
                    {
                        if (tile.snapPoints[i] != null)
                        {
                            float distance = Vector3.Distance(snapPosition, tile.snapPoints[i].position);
                            if (distance < 0.5f && distance < minDistance)
                            {
                                minDistance = distance;
                                closestTile = tile;
                            }
                        }
                    }
                }
            }
        }
        return closestTile;
    }










    TileInstance FindConnectedTile_HorizontalOnly(TileInstance fromTile, int snapPointIndex)
    {
        Vector2 snapPosXZ = new Vector2(fromTile.snapPoints[snapPointIndex].position.x, fromTile.snapPoints[snapPointIndex].position.z);
        float minDistance = float.MaxValue;
        TileInstance closestTile = null;

        for (int x = 0; x < gridManager.cols; x++)
        {
            for (int y = 0; y < gridManager.rows; y++)
            {
                TileInstance tile = gridManager.GetTileAt(x, y);
                if (tile != null && tile != fromTile)
                {
                    for (int i = 0; i < tile.snapPoints.Length; i++)
                    {
                        if (tile.snapPoints[i] != null)
                        {
                            Vector2 otherSnapPosXZ = new Vector2(tile.snapPoints[i].position.x, tile.snapPoints[i].position.z);
                            float distance = Vector2.Distance(snapPosXZ, otherSnapPosXZ);
                            if (distance < 0.5f && distance < minDistance)
                            {
                                minDistance = distance;
                                closestTile = tile;
                            }
                        }
                    }
                }
            }
        }
        return closestTile;
    }

    // A special version of FindConnectedSnapPoint that only compares horizontal (XZ) distance.
    int FindConnectedSnapPoint_HorizontalOnly(TileInstance fromTile, int fromSnapIndex, TileInstance toTile)
    {
        Vector2 fromSnapPosXZ = new Vector2(fromTile.snapPoints[fromSnapIndex].position.x, fromTile.snapPoints[fromSnapIndex].position.z);
        float minDistance = float.MaxValue;
        int closestSnapPoint = -1;

        for (int i = 0; i < toTile.snapPoints.Length; i++)
        {
            if (toTile.snapPoints[i] != null)
            {
                Vector2 toSnapPosXZ = new Vector2(toTile.snapPoints[i].position.x, toTile.snapPoints[i].position.z);
                float distance = Vector2.Distance(fromSnapPosXZ, toSnapPosXZ);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestSnapPoint = i;
                }
            }
        }
        return minDistance < 0.5f ? closestSnapPoint : -1;
    }












    int FindConnectedSnapPoint(TileInstance fromTile, int fromSnapIndex, TileInstance toTile)
    {
        Vector3 fromSnapPos = fromTile.snapPoints[fromSnapIndex].position;
        float minDistance = float.MaxValue;
        int closestSnapPoint = -1;
        for (int i = 0; i < toTile.snapPoints.Length; i++)
        {
            if (toTile.snapPoints[i] != null)
            {
                float distance = Vector3.Distance(fromSnapPos, toTile.snapPoints[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestSnapPoint = i;
                }
            }
        }
        return minDistance < 0.5f ? closestSnapPoint : -1;
    }

    void FindBankEntryMoves()
    {
        int entryRow = DetermineEntryRow();
        // Determine the direction of search: +1 for up (from bottom), -1 for down (from top).
        int searchDirection = (entryRow == 0) ? 1 : -1;
        RiverBankManager.BankSide oppositeBank = (entryRow == 0) ? RiverBankManager.BankSide.Top : RiverBankManager.BankSide.Bottom;

        for (int col = 0; col < gridManager.cols; col++)
        {
            bool foundLandingTile = false;
            int currentRow = entryRow;
            List<TileInstance> crossedReversedTiles = new List<TileInstance>();

            // Search through the column until we find a non-reversed tile or go off the board.
            while (currentRow >= 0 && currentRow < gridManager.rows)
            {
                var tile = gridManager.GetTileAt(col, currentRow);

                if (tile == null)
                {
                    // This column has a gap, stop searching here.
                    break;
                }

                if (tile.IsHardBlocker)
                {
                    // This is a wall. Stop searching this column immediately.
                    break;
                }

                else if (tile.IsReversed)
                {
                    // This is a reversed tile. Log it and continue searching.
                    crossedReversedTiles.Add(tile);
                    currentRow += searchDirection;
                }
                else
                {
                    // This is a valid, non-obstacle tile.
                    if (!validMoves.Contains(tile))
                    {
                        validMoves.Add(tile);

                        // --- NEW LOGIC TO FIND ENTRY POINTS ---
                        List<int> entryPoints = new List<int>();
                        bool isRotated = Mathf.RoundToInt(tile.transform.eulerAngles.y) == 180;

                        if (entryRow == 0) // Coming from the Bottom Bank
                        {
                            // If not rotated, the entry points are the bottom snaps (2, 3).
                            // If rotated, the tile is upside down, so the entry points are now snaps (0, 1).
                            entryPoints.AddRange(isRotated ? new int[] { 0, 1 } : new int[] { 2, 3 });
                        }
                        else // Coming from the Top Bank (entryRow == gridManager.rows - 1)
                        {
                            // If not rotated, the entry points are the top snaps (0, 1).
                            // If rotated, the tile is upside down, so the entry points are now snaps (2, 3).
                            entryPoints.AddRange(isRotated ? new int[] { 2, 3 } : new int[] { 0, 1 });
                        }
                        
                        // Store the identified entry points for this tile.
                        bankEntrySnapPoints[tile] = entryPoints;
                        // --- END OF NEW LOGIC ---
                    }


                    // If we crossed any reversed tiles to get here, store that path.
                    if (crossedReversedTiles.Count > 0)
                    {
                        reversedPathways[tile] = crossedReversedTiles;
                    }

                    foundLandingTile = true;
                    break; // Found our landing spot for this column, so we can stop searching it.
                }
            }

            // If we searched the entire column and only found reversed tiles,
            // then the opposite bank becomes a valid move.
            if (!foundLandingTile)
            {
                HighlightBankForDocking(oppositeBank);
            }
        }
    }

    int DetermineEntryRow()
    {
        if (bankPosition != null)
        {
            if (bankPosition.name.Contains("Top")) return gridManager.rows - 1;
            if (bankPosition.name.Contains("Bottom")) return 0;
        }
        return 0;
    }

    void MoveFromBankToTile(TileInstance targetTile, int snapPoint)
    {
        StopAllCoroutines();
        ClearHighlights();
        // ClearNonTargetHighlights(targetTile);
        StartCoroutine(MoveToTileCoroutine(targetTile, snapPoint));
    }

    void MoveFromTileToTile(TileInstance targetTile, int targetSnapPoint)
    {
        StopAllCoroutines();
        ClearNonTargetHighlights(targetTile);
        StartCoroutine(MoveToTileCoroutine(targetTile, targetSnapPoint));
    }

    void ClearNonTargetHighlights(TileInstance targetTile)
    {

        if (currentTile != null)
        {
            // At this point, "currentTile" is still the tile we are moving FROM.
            // Clear its path highlights.
            currentTile.GetComponent<PathVisualizer>()?.ClearAllHighlights();
        }


        var renderersToKeep = new List<Renderer>();
        var renderer = targetTile?.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderersToKeep.Add(renderer);

        var materialsToRestore = originalMaterials.Where(pair => !renderersToKeep.Contains(pair.Key)).ToList();

        foreach (var pair in materialsToRestore)
        {
            if (pair.Key != null)
            {
                pair.Key.sharedMaterial = pair.Value;
                originalMaterials.Remove(pair.Key);

                var tile = pair.Key.GetComponentInParent<TileInstance>();
                if (tile != null)
                {
                    tile.GetComponent<PathVisualizer>()?.ClearAllHighlights();

                    StartCoroutine(LiftTileSmooth(tile, false));
                    var clicker = tile.GetComponent<SimpleTileClickHandler>();
                    if (clicker != null) DestroyImmediate(clicker);
                    highlightedTiles.Remove(tile.gameObject);
                }
            }
        }
    }

    IEnumerator MoveToTileCoroutine(TileInstance targetTile, int snapPoint)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        // The temporary state change is no longer needed and has been removed.
        // We now pass the targetTile directly to the rotation method.
        Quaternion targetRot = GetSnapPointRotation(targetTile, snapPoint);

        Vector3 snapPosition = targetTile.snapPoints[snapPoint].position;
        Vector3 targetPos;

        // --- NEW AXIS-ALIGNED OFFSET LOGIC ---
        switch (snapPoint)
        {
            case 0: case 1:
                targetPos = snapPosition - (targetTile.transform.forward * snapOffset);
                break;
            case 2: case 3:
                targetPos = snapPosition + (targetTile.transform.forward * snapOffset);
                break;
            case 4:
                targetPos = snapPosition - (targetTile.transform.right * snapOffset);
                break;
            case 5:
                targetPos = snapPosition + (targetTile.transform.right * snapOffset);
                break;
            default:
                Vector3 tileCenter = targetTile.transform.position;
                Vector3 direction = (snapPosition - tileCenter).normalized;
                targetPos = snapPosition - direction * snapOffset;
                break;
        }

        float elapsed = 0f;
        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            float easeProgress = tileLiftCurve.Evaluate(elapsed / moveSpeed);
            transform.position = Vector3.Lerp(startPos, targetPos, easeProgress);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, easeProgress);
            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        PlaceOnTile(targetTile, snapPoint);

        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            // In Endless Mode, tell the EndlessModeManager about the move.
            if (endlessManager != null)
            {
                endlessManager.SpendActionPoint();
            }
        }
        else
        {
            // In Puzzle/Editor Mode, use the existing logic.
            currentMovementPoints--;
            UpdateMoveCounterUI();
        }



        CheckForCollectibleOnCurrentTile();



        if (gameManager != null && gameManager.currentMode != OperatingMode.Endless)
        {
            StartCoroutine(RefreshMovementOptionsCoroutine());
        }

    }

    IEnumerator MoveToBankCoroutine(Transform targetSpawn)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = targetSpawn.position;
        Quaternion targetRot = targetSpawn.rotation;
        float elapsed = 0f;
        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            float easeProgress = tileLiftCurve.Evaluate(elapsed / moveSpeed);
            transform.position = Vector3.Lerp(startPos, targetPos, easeProgress);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, easeProgress);
            yield return null;
        }
        SetStateForBank(targetSpawn);

        // --- START OF MODIFIED LOGIC ---
        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            if (endlessManager != null)
            {
                endlessManager.SpendActionPoint();
            }
        }
        else
        {
            currentMovementPoints--;
            UpdateMoveCounterUI();
        }
        // --- END OF MODIFIED LOGIC ---

        if (gameManager != null && gameManager.currentMode != OperatingMode.Endless)
        {
            StartCoroutine(RefreshMovementOptionsCoroutine());
        }

    }

    void CompleteMovementTurn()
    {
        isMoving = false;
        isBobbing = false;
        StartCoroutine(LiftAndBobBoat(false));
        ClearHighlights();
    }

    public void EndMovementTurn()
    {
        if (isSelected) DeselectBoat();
        currentMovementPoints = 0;
        UpdateMoveCounterUI();
    }

    public void ResetStateAfterEjection()
    {
        // Force the boat out of any selection or animation state.
        isSelected = false;
        isBobbing = false;
        StopAllCoroutines();

        // Reset collected stars when ejected
        if (starsCollected > 0)
        {
            Debug.Log($"Ejected! Lost {starsCollected} stars.");
            FloatingTextManager.Instance.ShowText($"-{starsCollected}", FloatingTextManager.FloatingTextType.StarLoss, transform.position);
            starsCollected = 0; // WE CAN CHANGE THIS LATER TO -1 IF WE WANT TO KEEP SOME OF THE STARS
            extraMovesCollected = 0;
            UpdateStarCounterUI();
        }


        // Ensure the boat is visually lowered to its base position,
        // as if it were never selected.
        Vector3 currentPos = transform.position;
        currentPos.y = isAtBank && bankPosition != null ? bankPosition.position.y : 0f;
        transform.position = currentPos;

        // Clear any leftover visual artifacts immediately.
        ClearHighlights();
    }

    public void ResetMovementPoints()
    {
        // In Puzzle Mode, turns do not reset. Ignore this call.
        if (gridManager != null && gridManager.isPuzzleMode)
        {
            Debug.Log("Cannot reset movement points in Puzzle Mode.");
            return;

        }


        currentMovementPoints = maxMovementPoints;
        UpdateMoveCounterUI();

        if (isSelected)
        {
            DeselectBoat();
            SelectBoat();
        }
    }

    public static Quaternion GetSnapPointRotation(TileInstance tile, int snapPointIndex)
    {
        if (tile == null) return Quaternion.identity;

        // --- STEP 1: Determine the BASE rotation using the reliable switch statement ---
        float targetYRotation = 0f;
        switch (snapPointIndex)
        {
            case 0: // Top-Left faces down
            case 1: // Top-Right faces down
                targetYRotation = 180f;
                break;

            case 2: // Down-Left faces up
            case 3: // Down-Right faces up
                targetYRotation = 0f;
                break;

            case 4: // Right side faces left
                targetYRotation = 270f;
                break;

            case 5: // Left side faces right
                targetYRotation = 90f;
                break;

            default:
                return Quaternion.identity;
        }

        // --- STEP 2: Check if the TILE itself is rotated ---
        // We use Mathf.RoundToInt to avoid floating point comparison issues.
        // This will correctly identify a rotation of 179.999 as 180.
        bool tileIsRotated = Mathf.RoundToInt(tile.transform.eulerAngles.y) == 180;

        // --- STEP 3: If the tile is rotated, flip the boat's rotation ---
        if (tileIsRotated)
        {
            targetYRotation = (targetYRotation + 180f) % 360f;
        }

        // --- STEP 4: Return the final, correct rotation ---
        return Quaternion.Euler(0f, targetYRotation, 0f);
    }



public void PlaceOnTile(TileInstance tile, int snapPointIndex)
    {
        if (tile == null || snapPointIndex < 0 || snapPointIndex >= tile.snapPoints.Length) return;

        // First, calculate and set the physical transform
        Vector3 snapPosition = tile.snapPoints[snapPointIndex].position;
        Vector3 finalPosition;

        // --- NEW AXIS-ALIGNED OFFSET LOGIC ---
        // This implements the fix you correctly diagnosed.
        switch (snapPointIndex)
        {
            case 0: // Top-Left
            case 1: // Top-Right
                // The top edge is on the tile's local positive Z. Offset is in the negative local Z.
                finalPosition = snapPosition - (tile.transform.forward * snapOffset);
                break;
            case 2: // Down-Left
            case 3: // Down-Right
                // The bottom edge is on the tile's local negative Z. Offset is in the positive local Z.
                finalPosition = snapPosition + (tile.transform.forward * snapOffset);
                break;
            case 4: // Right
                // The right edge is on the tile's local positive X. Offset is in the negative local X.
                finalPosition = snapPosition - (tile.transform.right * snapOffset);
                break;
            case 5: // Left
                // The left edge is on the tile's local negative X. Offset is in the positive local X.
                finalPosition = snapPosition + (tile.transform.right * snapOffset);
                break;
            default:
                // Fallback to a simple diagonal offset if something goes wrong.
                Vector3 tileCenter = tile.transform.position;
                Vector3 direction = (snapPosition - tileCenter).normalized;
                finalPosition = snapPosition - direction * snapOffset;
                break;
        }

        transform.position = finalPosition;
        transform.rotation = GetSnapPointRotation(tile, snapPointIndex);
        // --- END OF NEW LOGIC ---

        // Now, call the new method to update the internal state
        InitializeStateOnTile(tile, snapPointIndex);
    }

    int DetermineSnapPointFromClick(TileInstance tile)
    {
        List<int> validSnapPoints = FindClosestSnapPointsToBank(tile);
        if (validSnapPoints.Count == 0) return 2;
        if (validSnapPoints.Count == 1) return validSnapPoints[0];
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 clickWorldPos = hit.point;
            float dist0 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[0]].position);
            float dist1 = Vector3.Distance(clickWorldPos, tile.snapPoints[validSnapPoints[1]].position);
            return dist0 < dist1 ? validSnapPoints[0] : validSnapPoints[1];
        }
        return validSnapPoints[0];
    }

    List<int> FindClosestSnapPointsToBank(TileInstance tile)
    {
        List<int> closestPoints = new List<int>();
        if (bankPosition == null) return closestPoints;
        Vector3 tileCenter = tile.transform.position;
        Vector3 bankDirection = bankPosition.name.Contains("Bottom") ? Vector3.back : Vector3.forward;
        var snapDots = new List<(int, float)>();
        for (int i = 0; i < tile.snapPoints.Length; i++)
        {
            if (tile.snapPoints[i] != null)
            {
                Vector3 snapDirection = (tile.snapPoints[i].position - tileCenter).normalized;
                snapDots.Add((i, Vector3.Dot(snapDirection, bankDirection)));
            }
        }
        snapDots.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        if (snapDots.Count > 0) closestPoints.Add(snapDots[0].Item1);
        if (snapDots.Count > 1) closestPoints.Add(snapDots[1].Item1);
        return closestPoints;
    }

    void HighlightTile(TileInstance tile)
    {
        var renderer = tile.GetComponentInChildren<MeshRenderer>();
        if (renderer == null || originalMaterials.ContainsKey(renderer)) return;

        originalMaterials[renderer] = renderer.sharedMaterial;
        renderer.material.color = selectedColor;

        if (!highlightedTiles.Contains(tile.gameObject))
        {
            highlightedTiles.Add(tile.gameObject);
        }

        StartCoroutine(LiftTileSmooth(tile, true));
        var clickHandler = tile.gameObject.AddComponent<SimpleTileClickHandler>();
        clickHandler.targetBoat = this;
        clickHandler.targetTile = tile;

        if (isAtBank && embarkArrowPrefab != null)
        {
            // --- START OF CORRECTED LOGIC ---

            // 1. Determine which edge of the tile is physically facing the bank.
            bool tileIsRotated = Mathf.RoundToInt(tile.transform.eulerAngles.y) == 180;
            int[] snapIndices;

            if (CurrentBank == RiverBankManager.BankSide.Top)
            {
                // If at the TOP bank, we need the arrows on the tile's top-facing edge.
                // This is snaps {0, 1} if not rotated, or {2, 3} if it is rotated.
                snapIndices = tileIsRotated ? new int[] { 2, 3 } : new int[] { 0, 1 };
            }
            else // From Bottom Bank
            {
                // If at the BOTTOM bank, we need arrows on the tile's bottom-facing edge.
                // This is snaps {2, 3} if not rotated, or {0, 1} if it is rotated.
                snapIndices = tileIsRotated ? new int[] { 0, 1 } : new int[] { 2, 3 };
            }

            // 2. Create the arrows, applying the simple and correct rotation rule for each snap point.
            foreach (int index in snapIndices)
            {
                Transform snapPointTransform = tile.snapPoints[index];
                GameObject arrowGO = Instantiate(embarkArrowPrefab, snapPointTransform);
                arrowGO.transform.localPosition = Vector3.up * arrowHoverHeight;

                // THIS is the original, correct rotation logic that we are restoring.
                // It correctly sets the rotation based on the snap point's index, regardless of the bank.
                // Snaps 0/1 are on the "top" edge and must point down (180 degrees).
                // Snaps 2/3 are on the "bottom" edge and must point up (0 degrees).
                arrowGO.transform.localRotation = (index <= 1) ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
            }
            // --- END OF CORRECTED LOGIC ---
        }


        
    }

    IEnumerator LiftTileSmooth(TileInstance tile, bool lift)
    {
        if (tile == null) yield break;
        Vector3 startPos = tile.transform.position;
        Vector3 targetPos = new Vector3(startPos.x, lift ? hoverHeight : 0f, startPos.z);

        float elapsed = 0f;
        while (elapsed < tileLiftDuration)
        {
            if (tile == null) yield break;
            elapsed += Time.deltaTime;
            tile.transform.position = Vector3.Lerp(startPos, targetPos, tileLiftCurve.Evaluate(elapsed / tileLiftDuration));
            yield return null;
        }
        if (tile != null) tile.transform.position = targetPos;
    }

    void OnDrawGizmosSelected()
    {
        if (currentTile != null && currentSnapPoint >= 0 && currentSnapPoint < currentTile.snapPoints.Length && currentTile.snapPoints[currentSnapPoint] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTile.snapPoints[currentSnapPoint].position);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 1f);
        }
    }





    public void CompleteMovement()
    {
        isMoving = false;
        // We can add any other state resets here in the future if needed.
    }



    private int GetMirroredSnapPointForRotatedTile(int snapPoint)
    {
        switch (snapPoint)
        {
            case 0: return 3; // Top-Left <-> Down-Right
            case 1: return 2; // Top-Right <-> Down-Left
            case 2: return 1;
            case 3: return 0;
            case 4: return 5; // Right <-> Left
            case 5: return 4;
            default: return -1; // Should not happen
        }
    }


    private void ResynchronizeStateWithTransform()
    {
        // This is our sanity check. It ensures the boat's internal state
        // matches its physical location in the world.
        if (gridManager == null || isAtBank) return;

        // Ask the GridManager to find the tile and snap point where we are right now.
        var (foundTile, foundSnapPoint) = gridManager.FindTileAndSnapPointAtWorldPos(transform.position);

        if (foundTile != null && (currentTile != foundTile || currentSnapPoint != foundSnapPoint))
        {
            Debug.LogWarning($"<color=orange>Boat Desync Detected!</color> Resynchronizing state. Was on {currentTile?.name}:{currentSnapPoint}, now on {foundTile.name}:{foundSnapPoint}.");
            // Forcibly update our internal state to the correct values.
            InitializeStateOnTile(foundTile, foundSnapPoint);
        }
    }






}

public class SimpleTileClickHandler : MonoBehaviour, IPointerClickHandler
{
    public BoatController targetBoat;
    public TileInstance targetTile;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetBoat != null && targetTile != null)
        {
            targetBoat.OnTileClicked(targetTile, eventData);
        }
    }




}