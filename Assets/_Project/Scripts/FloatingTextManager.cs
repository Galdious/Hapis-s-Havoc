/* FloatingTextManager.cs */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloatingTextManager : MonoBehaviour
{
    // Singleton pattern for easy global access
    public static FloatingTextManager Instance { get; private set; }

    [Header("Prefab & Parent")]
    [Tooltip("The FloatingText prefab we created.")]
    [SerializeField] private GameObject floatingTextPrefab;
    [Tooltip("The canvas transform to spawn the text under. This should be your main UI canvas.")]
    [SerializeField] private RectTransform textParentCanvas;
    [Tooltip("How many PIXELS above the target's screen position the text should initially appear.")]
    [SerializeField] private float screenYOffset = 50f; // Note the new name and a more suitable default value



    [Header("Icon Assets")]
    [Tooltip("Sprite for gaining a star.")]
    [SerializeField] private Sprite starGainedIcon;
    [Tooltip("Sprite for losing a star.")]
    [SerializeField] private Sprite starLostIcon;
    [Tooltip("Sprite for gaining movement points/stamina.")]
    [SerializeField] private Sprite moveGainedIcon;
    [Tooltip("Sprite for losing movement points/stamina.")]
    [SerializeField] private Sprite moveLostIcon;

    [Header("Color Settings")]
    [SerializeField] private Color gainColor = Color.green;
    [SerializeField] private Color lossColor = Color.red;
    [SerializeField] private Color neutralColor = Color.white;

    [Header("Timing Settings")] // You can create a new header for organization
    [Tooltip("The delay between the 'Skip Bonus!' message and the amount.")]
    [SerializeField] private float skipBonusMessageDelay = 0.8f;


    private Camera mainCamera;

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
            mainCamera = Camera.main;
        }
    }

    // This is the main public method everyone will call.
    // We use an enum to make the calls clean and readable.
    public enum FloatingTextType { StarGain, StarLoss, MoveGain, MoveLoss, Neutral }

    public void ShowText(string message, FloatingTextType type, Vector3 worldPosition, float delay = 0f)
    {
        if (floatingTextPrefab == null) return;

        // Start the coroutine to handle the potential delay.
        StartCoroutine(ShowTextCoroutine(message, type, worldPosition, delay));
    }

    private IEnumerator ShowTextCoroutine(string message, FloatingTextType type, Vector3 worldPosition, float delay)
    {
        // 1. Handle the delay
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        // 2. The rest of the logic is the same as before
        GameObject textGO = Instantiate(floatingTextPrefab, textParentCanvas);
        FloatingText floatingText = textGO.GetComponent<FloatingText>();

        Color color = GetColorForType(type);
        Sprite icon = GetIconForType(type);

        Vector2 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        screenPosition.y += screenYOffset;
        textGO.GetComponent<RectTransform>().position = screenPosition;

        floatingText.Initialize(message, color, icon);
    }


    // Helper methods to keep the main method clean
    private Color GetColorForType(FloatingTextType type)
    {
        switch (type)
        {
            case FloatingTextType.StarGain:
            case FloatingTextType.MoveGain:
                return gainColor;
            case FloatingTextType.StarLoss:
            case FloatingTextType.MoveLoss:
                return lossColor;
            default:
                return neutralColor;
        }
    }

    private Sprite GetIconForType(FloatingTextType type)
    {
        switch (type)
        {
            case FloatingTextType.StarGain: return starGainedIcon;
            case FloatingTextType.StarLoss: return starLostIcon;
            case FloatingTextType.MoveGain: return moveGainedIcon;
            case FloatingTextType.MoveLoss: return moveLostIcon;
            default: return null;
        }
    }



    public void ShowSkipBonus(int bonusAmount, Vector3 worldPosition)
    {
        // First, show the "Skip Bonus!" text immediately.
        ShowText("Skip Bonus!", FloatingTextType.Neutral, worldPosition);

        // Then, show the actual bonus amount after our configurable delay.
        string message = $"+{bonusAmount}";
        ShowText(message, FloatingTextType.MoveGain, worldPosition, skipBonusMessageDelay);
    }



}