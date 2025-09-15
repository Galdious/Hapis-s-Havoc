/* RowDropZone.cs (Upgraded for Sprite Swapping) */
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct VisualState
{
    public Sprite sprite;
    public Color tint;
}

[RequireComponent(typeof(Image))]
public class RowDropZone : MonoBehaviour
{
    // --- Public References & Data ---
    [HideInInspector] public int row;
    [HideInInspector] public bool fromLeft;
    [HideInInspector] public RiverControls riverControls;
    
    // --- NEW: Visual State Configuration ---
    [Header("Visual States")]
    [Tooltip("The default appearance for Puzzle/Play mode.")]
    [SerializeField] private VisualState playModeOriginalState;
    [Tooltip("The default appearance for Endless mode.")]
    [SerializeField] private VisualState endlessModeOriginalState;
    [Space(10)]
    [Tooltip("Appearance when a tile is hovered over.")]
    [SerializeField] private VisualState highlightState;
    [Tooltip("Appearance for a blue tile forecast.")]
    [SerializeField] private VisualState forecastBlueState;
    [Tooltip("Appearance for a red tile forecast.")]
    [SerializeField] private VisualState forecastRedState;

    // --- Private State ---
    private Image dropZoneImage;
    private VisualState originalState; // Will be set to one of the above based on game mode
    private bool isHovered = false;

    // Note: We don't have an Initialize method anymore, Awake handles it all.
    void Awake()
    {
        dropZoneImage = GetComponent<Image>();

        // Determine which original state to use based on the current game mode
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null && gameManager.currentMode == OperatingMode.Endless)
        {
            originalState = endlessModeOriginalState;
        }
        else
        {
            originalState = playModeOriginalState;
        }

        // Apply the chosen original state
        ApplyVisualState(originalState);
    }

    // --- NEW: A helper method to apply a state ---
    private void ApplyVisualState(VisualState state)
    {
        if (dropZoneImage == null) return;
        
        // If a sprite is provided in the state, use it. Otherwise, keep the current sprite.
        if (state.sprite != null)
        {
            dropZoneImage.sprite = state.sprite;
        }
        
        // Always apply the tint color.
        dropZoneImage.color = state.tint;
    }

    public void OnHoverEnter()
    {
        if (isHovered) return;
        isHovered = true;
        ApplyVisualState(highlightState);
        riverControls?.AnimateRowForDrop(row, fromLeft);
    }

    public void OnHoverExit()
    {
        if (!isHovered) return;
        isHovered = false;
        ApplyVisualState(originalState);
        riverControls?.ResetRowAnimation(row);
    }
    
    public void ShowForecast(bool isObstacle)
    {
        ApplyVisualState(isObstacle ? forecastRedState : forecastBlueState);
    }

    public void HideForecast()
    {
        ApplyVisualState(originalState);
    }
}