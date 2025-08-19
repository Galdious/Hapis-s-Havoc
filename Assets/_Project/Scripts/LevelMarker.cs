/* LevelMarker.cs */
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class LevelMarker : MonoBehaviour
{
    [Header("Level Identity")]
    [Tooltip("The world this marker represents (e.g., 1 for World 1).")]
    public int worldNumber;
    [Tooltip("The level within the world (e.g., 5 for Level 5).")]
    public int levelNumber;

    [Header("Visual Components")]
    [Tooltip("The GameObject for the lock visual (e.g., chains).")]
    [SerializeField] private GameObject lockVisual;

    [Tooltip("The list of Sprite Renderers for the stars, in order (1, 2, 3).")]
    [SerializeField] private List<SpriteRenderer> starRenderers;

    [Header("Star Sprites")]
    [Tooltip("The sprite for a bright, earned star.")]
    [SerializeField] private Sprite starWonSprite;
    [Tooltip("The sprite for a grey, un-earned star.")]
    [SerializeField] private Sprite starLostSprite;

    // --- Public Properties ---
    public bool IsLocked { get; private set; } = true; // Levels start locked by default.

    private LevelSelectManager manager;

    public void Initialize(LevelSelectManager manager)
    {
        this.manager = manager;
    }

    public void HandleClick()
    {
        // This is the same logic that was in OnPointerClick before.
        if (!IsLocked && manager != null)
        {
            manager.OnMarkerClicked(this);
        }
    }



    // A method to initialize the marker's visuals based on saved data.
    public void UpdateVisuals(int starCount, bool isUnlocked)
    {
        this.IsLocked = !isUnlocked;

        if (lockVisual != null)
        {
            lockVisual.SetActive(IsLocked);
        }

        // --- CORRECTED STAR LOGIC ---

        // First, decide if the star container should be visible at all.
        // It should only be visible if the level is UNLOCKED and has been COMPLETED (starCount > 0).
        bool shouldShowStars = isUnlocked && (starCount > 0);

        foreach (var star in starRenderers)
        {
            if (star != null) star.gameObject.SetActive(shouldShowStars);
        }

        // If we are showing the stars, update their sprites to match the score.
        if (shouldShowStars)
        {
            for (int i = 0; i < starRenderers.Count; i++)
            {
                if (starRenderers[i] != null)
                {
                    starRenderers[i].sprite = (i < starCount) ? starWonSprite : starLostSprite;
                }
            }
        }
    }

    



}