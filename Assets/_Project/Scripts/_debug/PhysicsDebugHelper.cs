/*
 *  PhysicsDebugHelper.cs
 *  ---------------------------------------------------------------
 *  Helps visualize what's blocking tile physics.
 *  Add this to an empty GameObject to debug collision issues.
 */

using UnityEngine;

public class PhysicsDebugHelper : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showAllColliders = true;
    public bool showFloorBounds = true;
    public bool logCollisions = true;
    
    void Start()
    {
        if (showAllColliders)
        {
            ShowAllColliders();
        }
        
        if (showFloorBounds)
        {
            HighlightFloor();
        }
    }
    
    void ShowAllColliders()
    {
        Debug.Log("=== ALL COLLIDERS IN SCENE ===");
        
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        
        foreach (Collider col in allColliders)
        {
            string info = $"Collider: {col.gameObject.name}";
            info += $" | Position: {col.transform.position}";
            info += $" | Size: {col.bounds.size}";
            info += $" | Center: {col.bounds.center}";
            info += $" | IsTrigger: {col.isTrigger}";
            info += $" | Layer: {col.gameObject.layer} ({LayerMask.LayerToName(col.gameObject.layer)})";
            
            Debug.Log(info);
            
            // Draw bounds in scene view
            Debug.DrawLine(col.bounds.min, col.bounds.max, Color.red, 10f);
        }
        
        Debug.Log($"Total colliders found: {allColliders.Length}");
    }
    
    void HighlightFloor()
    {
        GameObject floor = GameObject.Find("GameFloor");
        if (floor != null)
        {
            Debug.Log($"Found GameFloor at: {floor.transform.position}");
            Debug.Log($"GameFloor scale: {floor.transform.localScale}");
            Debug.Log($"GameFloor bounds: {floor.GetComponent<Collider>().bounds}");
            
            // Draw floor outline
            Collider floorCol = floor.GetComponent<Collider>();
            if (floorCol != null)
            {
                Vector3 center = floorCol.bounds.center;
                Vector3 size = floorCol.bounds.size;
                
                // Draw floor edges in green
                Vector3[] corners = new Vector3[8];
                corners[0] = center + new Vector3(-size.x/2, -size.y/2, -size.z/2);
                corners[1] = center + new Vector3(size.x/2, -size.y/2, -size.z/2);
                corners[2] = center + new Vector3(size.x/2, -size.y/2, size.z/2);
                corners[3] = center + new Vector3(-size.x/2, -size.y/2, size.z/2);
                corners[4] = center + new Vector3(-size.x/2, size.y/2, -size.z/2);
                corners[5] = center + new Vector3(size.x/2, size.y/2, -size.z/2);
                corners[6] = center + new Vector3(size.x/2, size.y/2, size.z/2);
                corners[7] = center + new Vector3(-size.x/2, size.y/2, size.z/2);
                
                // Bottom face
                Debug.DrawLine(corners[0], corners[1], Color.green, 10f);
                Debug.DrawLine(corners[1], corners[2], Color.green, 10f);
                Debug.DrawLine(corners[2], corners[3], Color.green, 10f);
                Debug.DrawLine(corners[3], corners[0], Color.green, 10f);
                
                // Top face
                Debug.DrawLine(corners[4], corners[5], Color.green, 10f);
                Debug.DrawLine(corners[5], corners[6], Color.green, 10f);
                Debug.DrawLine(corners[6], corners[7], Color.green, 10f);
                Debug.DrawLine(corners[7], corners[4], Color.green, 10f);
            }
        }
        else
        {
            Debug.LogWarning("GameFloor not found!");
        }
    }
    
    [ContextMenu("List All GameObjects")]
    public void ListAllGameObjects()
    {
        Debug.Log("=== ALL GAMEOBJECTS IN SCENE ===");
        
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.GetComponent<Collider>() != null)
            {
                Debug.Log($"GameObject with Collider: {obj.name} at {obj.transform.position}");
            }
        }
    }
    
    [ContextMenu("Test Tile Physics")]
    public void TestTilePhysics()
    {
        Debug.Log("Creating test physics cube...");
        
        GameObject testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        testCube.name = "PhysicsTestCube";
        testCube.transform.position = new Vector3(5f, 2f, 0f); // Outside grid, above
        testCube.GetComponent<Renderer>().material.color = Color.magenta;
        
        Rigidbody rb = testCube.AddComponent<Rigidbody>();
        rb.mass = 1f;
        
        // Give it a push toward the grid
        rb.AddForce(Vector3.left * 3f, ForceMode.Impulse);
        
        Debug.Log("Test cube created and pushed. Watch how it falls!");
        
        // Destroy after 5 seconds
        Destroy(testCube, 5f);
    }
}