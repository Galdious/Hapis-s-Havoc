/*
 *  RiverBankManager.cs
 *  ---------------------------------------------------------------
 *  Creates river banks on both sides of the grid with boat spawn points.
 *  Banks align with tile rows and provide starting positions for boats.
 */

using System.Collections.Generic;
using UnityEngine;

public class RiverBankManager : MonoBehaviour
{
    [Header("Bank Settings")]
    public GameObject bankPrefab;         // visual bank object (optional)
    public float bankDistance = 1f;       // how far banks are from grid edge
    public float bankWidth = 0.5f;        // width of bank sections
    public float bankHeight = 1f;         // height of bank (same as tiles by default)
    public float bankYPosition = 0f;      // Y position of bank (same as tiles)
    public bool createVisualBanks = true; // whether to create visible bank objects

    [Header("Spawn Points")]
    public int spawnPointsPerSide = 4;    // how many spawn points per bank
    public float spawnPointSpacing = 1f;  // spacing between spawn points
    public bool showSpawnPointGizmos = true;

    [Header("Materials")]
    public Material bankMaterial;         // material for bank visuals
    public Material spawnPointMaterial;   // material for spawn point indicators

    private GridManager gridManager;
    private List<Transform> topBankSpawns = new List<Transform>();
    private List<Transform> bottomBankSpawns = new List<Transform>();
    private Transform topBankParent;
    private Transform bottomBankParent;

    public enum BankSide { Top, Bottom }

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null)
        {
            Debug.LogError("[RiverBankManager] GridManager not found!");
            return;
        }

        // Wait a frame for grid to be fully built
        //Invoke(nameof(CreateBanks), 0.1f); // commented out for level editor
    }


/// <summary>
/// Destroys existing bank GameObjects to prepare for regeneration.
/// </summary>
private void ClearBanks()
{
    if (topBankParent != null)
    {
        Destroy(topBankParent.gameObject);
    }
    if (bottomBankParent != null)
    {
        Destroy(bottomBankParent.gameObject);
    }
    topBankSpawns.Clear();
    bottomBankSpawns.Clear();
}

/// <summary>
/// Public method to be called by an external manager to generate the banks.
/// </summary>
public void GenerateBanksForGrid()
{
    ClearBanks();
    CreateBanks();
}



    void CreateBanks()
    {
        CreateTopBank();
        CreateBottomBank();

        Debug.Log($"[RiverBankManager] Created banks with {topBankSpawns.Count + bottomBankSpawns.Count} spawn points total");
    }

    void CreateTopBank()
    {
        // Create parent object for top bank
        topBankParent = new GameObject("TopBank").transform;
        topBankParent.SetParent(transform);

        // Calculate top bank position
        Vector3 bankPosition = GetBankPosition(BankSide.Top);
        topBankParent.position = bankPosition;

        // Create visual bank if requested
        if (createVisualBanks)
        {
            CreateBankVisual(topBankParent, BankSide.Top);
        }

        // Create spawn points
        CreateSpawnPoints(topBankParent, BankSide.Top, topBankSpawns);
    }

    void CreateBottomBank()
    {
        // Create parent object for bottom bank
        bottomBankParent = new GameObject("BottomBank").transform;
        bottomBankParent.SetParent(transform);

        // Calculate bottom bank position
        Vector3 bankPosition = GetBankPosition(BankSide.Bottom);
        bottomBankParent.position = bankPosition;

        // Create visual bank if requested
        if (createVisualBanks)
        {
            CreateBankVisual(bottomBankParent, BankSide.Bottom);
        }

        // Create spawn points
        CreateSpawnPoints(bottomBankParent, BankSide.Bottom, bottomBankSpawns);
    }

    Vector3 GetBankPosition(BankSide side)
    {
        // Get grid bounds from GridManager
        float gridHeight = (gridManager.rows - 1) * (gridManager.tileHeight + gridManager.gapZ);
        float zOffset = (gridHeight / 2f) + bankDistance;

        if (side == BankSide.Bottom)
        {
            zOffset = -zOffset; // Negative Z (bottom)
        }
        // Top bank stays positive (no change needed)

        return new Vector3(0f, bankYPosition, zOffset); // Use configurable Y position
    }

    void CreateBankVisual(Transform bankParent, BankSide side)
    {
        GameObject bankVisual;

        if (bankPrefab != null)
        {
            bankVisual = Instantiate(bankPrefab, bankParent);
        }
        else
        {
            // Create simple bank visual
            bankVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bankVisual.transform.SetParent(bankParent);
            bankVisual.name = $"{side}BankVisual";
        }

        // Size and position the bank visual - span full river width including gaps
        float riverWidth = (gridManager.cols * gridManager.tileWidth) + ((gridManager.cols - 1) * gridManager.gapX);
        bankVisual.transform.localPosition = Vector3.zero;
        bankVisual.transform.localScale = new Vector3(riverWidth, bankHeight, bankWidth); // Width includes tile gaps

        // Apply material
        if (bankMaterial != null)
        {
            bankVisual.GetComponent<Renderer>().sharedMaterial = bankMaterial;
        }
        else
        {
            bankVisual.GetComponent<Renderer>().material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
        }
    }

    void CreateSpawnPoints(Transform bankParent, BankSide side, List<Transform> spawnList)
    {
        // FIXED: Calculate spawn point positions based on ACTUAL grid dimensions
        float riverWidth = (gridManager.cols * gridManager.tileWidth) + ((gridManager.cols - 1) * gridManager.gapX);
        
        // Adjust number of spawn points based on grid size - ensure we have enough but not too many
        int actualSpawnPoints = Mathf.Max(2, Mathf.Min(spawnPointsPerSide, gridManager.cols + 1));
        
        float totalSpacing = (actualSpawnPoints - 1) * spawnPointSpacing;
        
        // Make sure spawn points fit within the river width
        if (totalSpacing > riverWidth * 0.8f) // Leave 20% margin
        {
            spawnPointSpacing = (riverWidth * 0.8f) / (actualSpawnPoints - 1);
            totalSpacing = (actualSpawnPoints - 1) * spawnPointSpacing;
        }
        
        float startX = -totalSpacing / 2f;

        for (int i = 0; i < actualSpawnPoints; i++)
        {
            // Create spawn point object
            GameObject spawnPoint = new GameObject($"{side}Spawn_{i}");
            spawnPoint.transform.SetParent(bankParent);

            // Position spawn point along X axis (across the river) at snap point height
            Vector3 localPos = new Vector3(startX + (i * spawnPointSpacing), bankHeight * 0.5f, 0f);
            spawnPoint.transform.localPosition = localPos;

            // Orient spawn point toward river center
            if (side == BankSide.Top)
            {
                spawnPoint.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Face south (into river)
            }
            else
            {
                spawnPoint.transform.localRotation = Quaternion.identity; // Face north (into river)
            }

            // Add visual indicator (small cube)
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.transform.SetParent(spawnPoint.transform);
            indicator.transform.localPosition = Vector3.zero;
            indicator.transform.localScale = Vector3.one * 0.3f;
            indicator.name = "SpawnIndicator";

            // Style the indicator
            if (spawnPointMaterial != null)
            {
                indicator.GetComponent<Renderer>().material = spawnPointMaterial;
            }
            else
            {
                indicator.GetComponent<Renderer>().material.color = Color.cyan;
            }

            // Add to spawn list
            spawnList.Add(spawnPoint.transform);
        }
        
        Debug.Log($"[RiverBankManager] Created {actualSpawnPoints} spawn points for {side} bank (riverWidth: {riverWidth:F1})");
    }

    // Public methods for boat spawning
    public Transform GetSpawnPoint(BankSide side, int index)
    {
        List<Transform> spawns = side == BankSide.Top ? topBankSpawns : bottomBankSpawns;

        if (index >= 0 && index < spawns.Count)
        {
            return spawns[index];
        }

        Debug.LogWarning($"[RiverBankManager] Invalid spawn point index {index} for {side} bank");
        return null;
    }

    public Transform GetRandomSpawnPoint(BankSide side)
    {
        List<Transform> spawns = side == BankSide.Top ? topBankSpawns : bottomBankSpawns;

        if (spawns.Count > 0)
        {
            int randomIndex = Random.Range(0, spawns.Count);
            return spawns[randomIndex];
        }

        return null;
    }

    public List<Transform> GetAllSpawnPoints(BankSide side)
    {
        return side == BankSide.Top ? new List<Transform>(topBankSpawns) : new List<Transform>(bottomBankSpawns);
    }

    void OnDrawGizmos()
    {
        if (!showSpawnPointGizmos) return;

        // Draw spawn points
        Gizmos.color = Color.cyan;
        foreach (Transform spawn in topBankSpawns)
        {
            if (spawn != null)
                Gizmos.DrawWireSphere(spawn.position, 0.2f);
        }

        foreach (Transform spawn in bottomBankSpawns)
        {
            if (spawn != null)
                Gizmos.DrawWireSphere(spawn.position, 0.2f);
        }
    }



    // Add these two new public methods inside your RiverBankManager class

    public GameObject GetBankGameObject(BankSide side)
    {
        if (side == BankSide.Top && topBankParent != null) return topBankParent.gameObject;
        if (side == BankSide.Bottom && bottomBankParent != null) return bottomBankParent.gameObject;
        return null;
    }

    public Transform GetNearestSpawnPoint(BankSide side, Vector3 worldPosition)
    {
        List<Transform> spawns = (side == BankSide.Top) ? topBankSpawns : bottomBankSpawns;
        Transform closestSpawn = null;
        float minDistance = float.MaxValue;

        foreach (Transform spawn in spawns)
        {
            float distance = Vector3.Distance(spawn.position, worldPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestSpawn = spawn;
            }
        }
        return closestSpawn;
    }
    

    
}