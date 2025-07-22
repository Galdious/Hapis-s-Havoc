using UnityEngine;

[RequireComponent(typeof(Transform))]
public class WaterResizer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The height (Y-position) of the water surface.")]
    public float waterLevel = 0.1f; // <<< NEW: Public variable for height

    [Tooltip("A little extra padding around the edges of the water plane.")]
    public float padding = 0.5f;

    void Start()
    {
        // Find the GridManager in our scene.
        GridManager gridManager = FindFirstObjectByType<GridManager>();

        if (gridManager == null)
        {
            Debug.LogError("[WaterResizer] Could not find the GridManager in the scene! Cannot resize water.", this);
            return;
        }

        // --- APPLY THE WATER LEVEL ---
        // Get the current position and update its Y value from our public variable.
        Vector3 currentPosition = transform.position;
        transform.position = new Vector3(currentPosition.x, waterLevel, currentPosition.z); // <<< NEW: Apply the height

        // --- CALCULATE AND APPLY SCALE (Same as before) ---
        float totalWidth = gridManager.cols * gridManager.tileWidth + (gridManager.cols - 1) * gridManager.gapX;
        float totalDepth = gridManager.rows * gridManager.tileHeight + (gridManager.rows - 1) * gridManager.gapZ;

        // A default Unity Plane is 10x10 world units in size.
        float scaleX = (totalWidth + padding) / 10f;
        float scaleZ = (totalDepth + padding) / 10f;

        transform.localScale = new Vector3(scaleX, 1f, scaleZ);

        Debug.Log($"[WaterResizer] Water surface resized. Level: {waterLevel}, Scale: ({scaleX}, 1, {scaleZ})");
    }
}