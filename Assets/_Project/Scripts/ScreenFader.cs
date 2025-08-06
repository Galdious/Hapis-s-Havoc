/* ScreenFader.cs */
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    // Singleton pattern
    public static ScreenFader Instance { get; private set; }

    [Tooltip("How long the fade animation takes in seconds.")]
    public float defaultFadeDuration = 0.4f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogError("[ScreenFader] CanvasGroup component not found!", this);
        }

        // Ensure the fader starts transparent AND not blocking clicks.
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        
    }

    /// <summary>
    /// Coroutine to fade the screen from transparent to fully opaque (black).
    /// </summary>
    public IEnumerator FadeOut()
    {
        Debug.Log("<color=orange>FADE OUT: Starting fade to black.</color>");
        // Start blocking clicks immediately.
        canvasGroup.blocksRaycasts = true;

        float counter = 0f;
        while (counter < defaultFadeDuration)
        {
            counter += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, counter / defaultFadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1;
    }

    /// <summary>
    /// Coroutine to fade the screen from fully opaque (black) to transparent.
    /// </summary>
    public IEnumerator FadeIn()
    {
        Debug.Log("<color=lime>FADE IN: Starting fade to clear.</color>");

        float counter = 0f;
        while (counter < defaultFadeDuration)
        {
            counter += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1, 0, counter / defaultFadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0;

        // Stop blocking clicks now that the screen is clear.
        canvasGroup.blocksRaycasts = false;
    }
}