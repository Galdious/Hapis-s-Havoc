/*
 *  BoatManager.cs
 *  ---------------------------------------------------------------
 *  Manages boat spawning from river banks and boat lifecycle.
 *  Spawns boats at bank spawn points when scene starts.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class BoatManager : MonoBehaviour
{
    [Header("Boat Settings")]
    public GameObject boatPrefab;
    public int boatsPerPlayer = 2;        // from rulebook: 2 River-runners per player
    
    [Header("Testing")]
    public bool spawnTestBoats = true;
    public RiverBankManager.BankSide testBankSide = RiverBankManager.BankSide.Bottom;
    
    private RiverBankManager bankManager;
    private List<BoatController> playerBoats = new List<BoatController>();
    
    void Start()
    {
        bankManager = FindFirstObjectByType<RiverBankManager>();
        
        if (spawnTestBoats && boatPrefab != null)
        {
            // Wait for banks to be created
            Invoke(nameof(SpawnTestBoats), 0.2f);
        }
    }
    
    void SpawnTestBoats()
    {
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
}