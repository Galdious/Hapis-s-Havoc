/*
 *  RowDropZone.cs
 *  ---------------------------------------------------------------
 *  An invisible UI component that acts as a target for dropping
 *  hand tiles. It highlights when hovered over and provides its
 *  grid location data to the tile being dropped.
 */

using UnityEngine;
using UnityEngine.UI; // Required for the Image component

[RequireComponent(typeof(Image))]
public class RowDropZone : MonoBehaviour
{
    // --- Public References & Data (Set by RiverControls) ---
    [HideInInspector] public int row;
    [HideInInspector] public bool fromLeft;
    [HideInInspector] public RiverControls riverControls;

    [Header("Visual Feedback")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0f, 0.3f); // Yellow, semi-transparent
    [SerializeField] private Color forecastBlueColor = new Color(0f, 0.5f, 1f, 0.4f);
    [SerializeField] private Color forecastRedColor = new Color(1f, 0.2f, 0f, 0.4f);

    // --- Private State ---
    private Image dropZoneImage;
    private Color originalColor;
    private bool isHovered = false;

    void Awake()
    {
        dropZoneImage = GetComponent<Image>();
        if (dropZoneImage != null)
        {
            // Start completely transparent

            originalColor = dropZoneImage.color;
        }
    }


    /// This is called BY the PlayableHandTile script when a drag enters our bounds.
    public void OnHoverEnter()
    {
        if (isHovered) return;
        isHovered = true;

        if (dropZoneImage != null)
        {
            dropZoneImage.color = highlightColor;
        }

        // --- NEW: Trigger the "make room" animation ---
        // We'll add these methods to RiverControls later. For now, the calls can stay.
        riverControls?.AnimateRowForDrop(row, fromLeft);

        Debug.Log($"Hovering over drop zone: Row {row}, From Left: {fromLeft}");
    }


    /// This is called BY the PlayableHandTile script when a drag leaves our bounds.
    public void OnHoverExit()
    {
        if (!isHovered) return;
        isHovered = false;

        if (dropZoneImage != null)
        {
            dropZoneImage.color = originalColor;
        }

        // --- NEW: Reset the "make room" animation ---
        riverControls?.ResetRowAnimation(row);
    }
    


    public void ShowForecast(bool isObstacle)
    {
        if (dropZoneImage != null)
        {
            dropZoneImage.color = isObstacle ? forecastRedColor : forecastBlueColor;
        }
    }

    public void HideForecast()
    {
        if (dropZoneImage != null)
        {
            dropZoneImage.color = originalColor; // Resets it to be fully transparent
        }
    }






}