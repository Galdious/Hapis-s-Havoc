/* CounterController.cs */
using UnityEngine;
using TMPro;

public class CounterController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The offset from the tile's center. We'll flip this when the tile rotates.")]
    [SerializeField] private Vector3 displayOffset = new Vector3(0.6f, 0.5f, -0.6f);

    // --- References ---
    private Transform targetTile;
    private TMP_Text counterText;
    private Quaternion fixedRotation;

    void Awake()
    {
        // Find the text component on this object or its children.
        counterText = GetComponentInChildren<TMP_Text>();
        
        // This rotation makes the counter lie flat, facing up, which is perfect for a top-down camera.
        // It will never change.
        fixedRotation = Quaternion.Euler(90f, 0, 0);
    }

    // A public method to be called by the LevelEditorManager after this is spawned.
    public void Initialize(Transform tileToFollow, int count)
    {
        this.targetTile = tileToFollow;
        UpdateCount(count);
    }

    // LateUpdate runs after all other Update calls, which is perfect for a follower object.
    // This ensures the tile has already finished its movement for the frame.
    void LateUpdate()
    {
        if (targetTile == null)
        {
            // If our target tile gets destroyed, we should destroy ourselves too.
            Destroy(gameObject);
            return;
        }

        // --- CORE LOGIC ---

        Vector3 currentOffset = displayOffset;

        // Check if the tile is rotated 180 degrees on the Y-axis.
        // We use Mathf.RoundToInt to avoid floating point precision issues.
        bool tileIsRotated = Mathf.RoundToInt(targetTile.eulerAngles.y) == 180;

        // If the tile is rotated, we invert the X and Z components of our offset.
        // This makes the counter "jump" to the opposite corner to maintain its relative screen position.
        if (tileIsRotated)
        {
            currentOffset.x *= -1;
            currentOffset.z *= -1;
        }
        
        // Apply the final position and our fixed rotation.
        transform.position = targetTile.position + currentOffset;
        transform.rotation = fixedRotation;
    }

    // Public method to allow the manager to update the text.
    public void UpdateCount(int newCount)
    {
        if (counterText != null)
        {
            counterText.text = $"x{newCount}";
            // Show or hide the counter entirely if the count is 1 or less.
            gameObject.SetActive(newCount > 1);
        }
    }
}