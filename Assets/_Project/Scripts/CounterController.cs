/* CounterController.cs */
using UnityEngine;
using TMPro;

public class CounterController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The offset from the tile's center. We'll flip this when the tile rotates.")]
    [SerializeField] private Vector3 displayOffset = new Vector3(1f, 0.26f, -0.5f);

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

        // 1. Start with our base offset, which corresponds to the non-rotated tile.
        //    For example, (0.6, 0.5, -0.6) puts it in the bottom-right.
        Vector3 finalOffset = displayOffset;

        // 2. Check if the tile is rotated approximately 180 degrees.
        //    We check against 179 and 181 to avoid floating-point errors.
        bool tileIsRotated = (Mathf.Abs(targetTile.eulerAngles.y - 180f) < 1.0f);

        // 3. If the tile IS rotated, we simply invert the X and Z components of our base offset.
        //    This calculates the new position that is diagonally opposite the original.
        // if (tileIsRotated)
        // {
        //     finalOffset.x = -displayOffset.x;
        //     finalOffset.z = -displayOffset.z;
        // }

        // 4. Apply the final position. We add the calculated offset to the tile's world-space center.
        transform.position = targetTile.position + finalOffset;

        // 5. Keep the counter's own rotation fixed and facing the camera.
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