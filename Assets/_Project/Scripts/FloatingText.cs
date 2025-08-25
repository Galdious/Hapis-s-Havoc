/* FloatingText.cs */
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class FloatingText : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text textComponent;
    [SerializeField] private Image iconComponent;

    [Header("Animation Settings")]
    [SerializeField] private float floatSpeed = 50f;
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float lifetime = 1.2f;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    // Called by the manager to set up the text and icon
    public void Initialize(string message, Color textColor, Sprite icon = null)
    {
        textComponent.text = message;
        textComponent.color = textColor;

        if (icon != null)
        {
            iconComponent.sprite = icon;
            iconComponent.gameObject.SetActive(true);
        }
        else
        {
            iconComponent.gameObject.SetActive(false);
        }
        
        StartCoroutine(AnimateAndDestroy());
    }

    private IEnumerator AnimateAndDestroy()
    {
        float timer = 0f;
        Color startTextColor = textComponent.color;
        Color startIconColor = iconComponent.color;

        while (timer < lifetime)
        {
            // Move upwards
            rectTransform.anchoredPosition += Vector2.up * floatSpeed * Time.deltaTime;

            // Fade out in the last part of the lifetime
            if (timer > lifetime - fadeDuration)
            {
                float fadeProgress = (timer - (lifetime - fadeDuration)) / fadeDuration;
                
                Color newTextColor = startTextColor;
                newTextColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
                textComponent.color = newTextColor;

                if (iconComponent.gameObject.activeSelf)
                {
                    Color newIconColor = startIconColor;
                    newIconColor.a = Mathf.Lerp(1f, 0f, fadeProgress);
                    iconComponent.color = newIconColor;
                }
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Animation finished, destroy this object
        Destroy(gameObject);
    }
}