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

        // Show or hide the lock model.
        if (lockVisual != null)
        {
            lockVisual.SetActive(IsLocked);
        }

        // --- Star Logic ---
        bool levelIsCompleted = starCount > 0;

        // Only show stars if the level has been completed at least once.
        foreach (var star in starRenderers)
        {
            if (star != null) star.gameObject.SetActive(levelIsCompleted);
        }

        if (levelIsCompleted)
        {
            // Update the star sprites based on the score.
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