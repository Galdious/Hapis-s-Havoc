/*
 *  BoatManager.cs
 *  ---------------------------------------------------------------
 *  Manages boat spawning from river banks and boat lifecycle.
 *  Spawns boats at bank spawn points when scene starts.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
public class BoatManager : MonoBehaviour
{
    [Header("Boat Settings")]
    public GameObject boatPrefab;
    public int boatsPerPlayer = 2;

    [Header("UI References")] // <<< ADD THIS HEADER AND FIELD
    public TMP_Text starCounterText;
    public TMP_Text moveCounterText;

    [Header("Testing")]
    public bool spawnTestBoats = true;
    public RiverBankManager.BankSide testBankSide = RiverBankManager.BankSide.Bottom;

    public void SetSelectedBoat(BoatController boat) { selectedBoat = boat; }
    public void ClearSelectedBoat() { selectedBoat = null; }
    public BoatController GetSelectedBoat() { return selectedBoat; }

    private RiverBankManager bankManager;
    private List<BoatController> playerBoats = new List<BoatController>();
    private BoatController selectedBoat;
    private LevelEditorManager editorManager;

    void Start()
    {
        bankManager = FindFirstObjectByType<RiverBankManager>();

        if (spawnTestBoats && boatPrefab != null)
        {
            // Wait for banks to be created
            // Invoke(nameof(SpawnTestBoats), 0.2f); // commented out for level editor mode

        }
    }

    public void SpawnTestBoats()
    {
        bankManager = FindFirstObjectByType<RiverBankManager>();
        editorManager = FindFirstObjectByType<LevelEditorManager>();


        if (bankManager == null)
        {
            Debug.LogError("[BoatManager] RiverBankManager not found!");
            return;
        }

        // For testing, just spawn one boat at the first spawn point of bottom bank
        Transform spawnPoint = bankManager.GetSpawnPoint(testBankSide, 0);
        if (spawnPoint != null)
        {
            SpawnBoatAtBank(spawnPoint, 0);
        }

        Debug.Log($"[BoatManager] Spawned test boat at {testBankSide} bank");
    }

    BoatController SpawnBoatAtBank(Transform spawnPoint, int boatIndex)
    {
        // Create boat at spawn point
        GameObject boatGO = Instantiate(boatPrefab, spawnPoint.position, spawnPoint.rotation);
        boatGO.name = $"Boat_{boatIndex}";

        BoatController boat = boatGO.GetComponent<BoatController>();

        if (boat == null)
        {
            boat = boatGO.AddComponent<BoatController>();
        }

        if (editorManager != null)
        {
            // Ask the editor for the correct number of moves.
            int maxMoves = editorManager.GetCurrentMaxMoves();

            // Apply these settings to the newly created boat.
            boat.maxMovementPoints = maxMoves;
            boat.currentMovementPoints = maxMoves;
        }

        if (starCounterText != null)
        {
            boat.starCounterText = this.starCounterText;
        }
        if (moveCounterText != null)
        {
            boat.moveCounterText = this.moveCounterText;
        }

        // Initialize boat at bank (not on a tile yet)
        boat.SetAtBank(spawnPoint);

        playerBoats.Add(boat);
        return boat;
    }

    [ContextMenu("Respawn All Boats")]
    public void RespawnAllBoats()
    {
        // Clear existing boats
        foreach (BoatController boat in playerBoats)
        {
            if (boat != null)
            {
                DestroyImmediate(boat.gameObject);
            }
        }
        playerBoats.Clear();

        // Respawn
        SpawnTestBoats();
    }


    public void SpawnBoatAtLevelStart(TileInstance startTile, int startSnapPoint, RiverBankManager.BankSide? startBank)
    {
        // First, clear any existing boats to ensure a clean slate.
        ClearAllBoats();

        // --- CASE 1: The start point is a BANK ---
        if (startBank.HasValue)
        {
            // Find the spawn point and use the dedicated method for banks.
            Transform spawnPoint = bankManager.GetNearestSpawnPoint(startBank.Value, Vector3.zero);
            if (spawnPoint != null)
            {
                SpawnBoatAtBank(spawnPoint, 0);
            }
        }
        // --- CASE 2: The start point is a TILE (NEW, DIRECT LOGIC) ---
        else if (startTile != null) // This handles the tile start case
        {
            // Step 1: Calculate the final position and rotation PRECISELY.
            Vector3 finalPos;
            Quaternion finalRot;

            Vector3 snapPosition = startTile.snapPoints[startSnapPoint].position;
            Vector3 tileCenter = startTile.transform.position;
            Vector3 direction = (snapPosition - tileCenter).normalized;

            // We need to access the public snapOffset value from the boat prefab.
            float offset = boatPrefab.GetComponent<BoatController>().snapOffset;
            finalPos = snapPosition - direction * offset;

            // Step 2: Call the NEWLY STATIC method. This is legal and correct.
            finalRot = BoatController.GetSnapPointRotation(startTile, startSnapPoint);

            // Step 3: Instantiate the boat at the PERFECT final transform.
            GameObject boatGO = Instantiate(boatPrefab, finalPos, finalRot);
            boatGO.name = "Boat_0";

            BoatController boat = boatGO.GetComponent<BoatController>();
            if (boat == null) boat = boatGO.AddComponent<BoatController>();

            // Step 4: Call the SAFE method to set the boat's state WITHOUT moving it.
            boat.InitializeStateOnTile(startTile, startSnapPoint);

            // Step 5: Apply editor settings and add to list.
            if (FindFirstObjectByType<LevelEditorManager>() is LevelEditorManager editorManager)
            {
                int maxMoves = editorManager.GetCurrentMaxMoves();
                boat.maxMovementPoints = maxMoves;
                boat.currentMovementPoints = maxMoves;
            }
            if (starCounterText != null)
            {
                boat.starCounterText = this.starCounterText;
            }
            if (moveCounterText != null) // <<< ADD THIS IF-STATEMENT
            {
                boat.moveCounterText = this.moveCounterText;
            }

            playerBoats.Add(boat);
        }
        // --- Failsafe Case ---
        else
        {
            Debug.LogWarning("No valid start point found for boat. Spawning at default test location.");
            SpawnTestBoats();
        }

        Debug.Log("[BoatManager] Spawned boat at level's defined start position.");
    }

    public void ClearAllBoats()
    {
        foreach (BoatController boat in playerBoats)
        {
            if (boat != null)
            {
                Destroy(boat.gameObject);
            }
        }
        playerBoats.Clear();
    }




    void Update()
    {
        // Test controls using new Input System
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            RespawnAllBoats();
        }
    }

    // Public methods for game management
    public List<BoatController> GetPlayerBoats()
    {
        return new List<BoatController>(playerBoats);
    }

    public BoatController SpawnPlayerBoat(RiverBankManager.BankSide side, int spawnIndex)
    {
        Transform spawnPoint = bankManager.GetSpawnPoint(side, spawnIndex);
        if (spawnPoint != null)
        {
            return SpawnBoatAtBank(spawnPoint, playerBoats.Count);
        }
        return null;
    }

    public BoatController SpawnBoatWithoutPositioning()
    {
        // A simplified version of SpawnBoatAtBank that just creates the boat.
        GameObject boatGO = Instantiate(boatPrefab, Vector3.zero, Quaternion.identity);
        boatGO.name = "PlayerBoat";

        BoatController boat = boatGO.GetComponent<BoatController>();
        if (boat == null) boat = boatGO.AddComponent<BoatController>();

        if (editorManager != null)
        {
            int maxMoves = editorManager.GetCurrentMaxMoves();
            boat.maxMovementPoints = maxMoves;
        }

        if (starCounterText != null) boat.starCounterText = this.starCounterText;
        if (moveCounterText != null) boat.moveCounterText = this.moveCounterText;

        playerBoats.Add(boat);
        return boat;
    }









}