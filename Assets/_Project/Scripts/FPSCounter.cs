/* FPSCounter.cs */
using UnityEngine;
using TMPro; // We need this namespace to work with TextMeshPro

[RequireComponent(typeof(TMP_Text))]
public class FPSCounter : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("How often the FPS display updates (in seconds).")]
    [SerializeField] private float updateInterval = 0.5f;

    // --- Private State ---
    private TMP_Text fpsText;
    private float accumulatedTime;
    private int frameCount;

    private void Awake()
    {
        // Get the TextMeshPro component attached to this same GameObject.
        fpsText = GetComponent<TMP_Text>();
        if (fpsText == null)
        {
            Debug.LogError("[FPSCounter] Could not find the TMP_Text component!", this);
            enabled = false; // Disable the script if no text component is found.
        }
    }

    private void Update()
    {
        // Accumulate time and frame count
        accumulatedTime += Time.unscaledDeltaTime; // Use unscaled time to work even if the game is paused or in slow-motion
        frameCount++;

        // Check if it's time to update the display
        if (accumulatedTime >= updateInterval)
        {
            // Calculate the average FPS over the interval
            float fps = frameCount / accumulatedTime;

            // Update the text display
            fpsText.text = $"{Mathf.RoundToInt(fps)} FPS";

            // Reset the counters for the next interval
            accumulatedTime = 0f;
            frameCount = 0;
        }
    }
}