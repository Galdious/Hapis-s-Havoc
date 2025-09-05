/* LevelSelectCameraController.cs (EventSystem Version) */
using UnityEngine;
using UnityEngine.EventSystems; // We need this for the drag/click interfaces
using System.Collections.Generic;
using System.Linq;

// This script goes on a full-screen, invisible UI Image with "Raycast Target" enabled.
public class LevelSelectCameraController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IPointerDownHandler
{
    [Header("Scene References")]
    [Tooltip("The parent object of the Cinemachine camera that we will move.")]
    [SerializeField] private Transform cameraProxy;

    [Header("Panning Settings")]
    [Tooltip("How sensitive the dragging is. A good starting value is around 1.0.")]
    [SerializeField] private float panSpeed = 1.0f;
    [Tooltip("How much friction is applied after release. 0 = glides forever, 10 = stops instantly. A good start is 4.")]
    [Range(0f, 10f)]
    [SerializeField] private float friction = 4f;
    [Tooltip("How much extra space to allow panning beyond the first and last level.")]
    [SerializeField] private float clampPadding = 5f;

    [Header("Click vs. Drag")]
    [Tooltip("The max distance (in pixels) the pointer can move for an action to be considered a click.")]
    [SerializeField] private float clickThreshold = 10f;

    [Header("Intro Animation")]
    [SerializeField] private bool playIntroAnimation = true;
    [SerializeField] private float introAnimationDuration = 1.2f;
    [SerializeField] private float introStartOffset = 20f;

    // --- Private State ---
    private Vector3 velocity;
    private bool isDragging = false;
    private bool isAnimatingIntro = false;
    private float minZClamp;
    private float maxZClamp;
    
    private Vector2 pointerDownPosition;
    
    void Start()
    {
        if (cameraProxy == null)
        {
            Debug.LogError("[CameraController] The Camera Proxy transform has not been assigned!", this);
            enabled = false;
            return;
        }

        SetupClamping();
        FocusOnStartLevel();
    }

    void Update()
    {
        // The inertia logic runs here, independent of input events.
        if (!isDragging && !isAnimatingIntro && velocity.magnitude > 0.1f)
        {
            // Move the camera based on the decaying velocity.
            Vector3 displacement = velocity * Time.deltaTime;
            Vector3 newPos = cameraProxy.position + displacement;
            newPos.z = Mathf.Clamp(newPos.z, minZClamp, maxZClamp);
            cameraProxy.position = newPos;

            // Apply friction.
            velocity = Vector3.Lerp(velocity, Vector3.zero, friction * Time.deltaTime);
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isAnimatingIntro) return;
        
        // Record the position where the click/drag started.
        pointerDownPosition = eventData.position;
        // Stop any existing inertia immediately.
        velocity = Vector3.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isAnimatingIntro) return;
        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || isAnimatingIntro) return;

        // Calculate world-space displacement from screen-space delta.
        float moveZ = -eventData.delta.y * panSpeed * 0.1f; // Use eventData.delta
        Vector3 displacement = new Vector3(0, 0, moveZ);

        // Apply movement.
        Vector3 newPosition = cameraProxy.position + displacement;
        newPosition.z = Mathf.Clamp(newPosition.z, minZClamp, maxZClamp);
        cameraProxy.position = newPosition;

        // Calculate the velocity for inertia.
        velocity = displacement / Time.deltaTime;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        // The velocity has already been calculated in OnDrag.
        // It will now be used by the Update loop for inertia.
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Check if the pointer moved less than our threshold. If so, it's a click.
        if (Vector2.Distance(pointerDownPosition, eventData.position) < clickThreshold)
        {
            HandleClick(eventData);
        }
    }

    private void HandleClick(PointerEventData eventData)
    {
        // This is where we act as a "forwarder".
        // We cast a ray from the camera to see if we hit a LevelMarker.
        Ray ray = Camera.main.ScreenPointToRay(eventData.position);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // We check if the object we hit (or its parent) has a LevelMarker component.
            LevelMarker marker = hit.collider.GetComponentInParent<LevelMarker>();
            if (marker != null)
            {
                Debug.Log($"Forwarding click to LevelMarker: {marker.name}");
                // Manually call the marker's public click handler.
                marker.HandleClick();
            }
        }
    }

    // --- The rest of the script is unchanged ---

    private void SetupClamping()
    {
        LevelMarker[] markers = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
        if (markers.Length == 0) { minZClamp = -10f; maxZClamp = 10f; return; }
        minZClamp = markers.Min(marker => marker.transform.position.z) - clampPadding;
        maxZClamp = markers.Max(marker => marker.transform.position.z) + clampPadding;
    }

    private void FocusOnStartLevel()
    {
        List<LevelInfo> allLevels = LevelFinder.GetAllLevels();
        LevelMarker[] markersInScene = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
        LevelMarker targetMarker = null;

        foreach (var level in allLevels)
        {
            string levelKey = $"Level_{level.WorldNumber:00}_{level.LevelNumber:00}_Stars";
            bool isUnlocked = IsLevelUnlocked(level, allLevels);
            if (isUnlocked && PlayerPrefs.GetInt(levelKey, 0) == 0)
            {
                targetMarker = markersInScene.FirstOrDefault(m => m.worldNumber == level.WorldNumber && m.levelNumber == level.LevelNumber);
                if (targetMarker != null) break;
            }
        }
        if (targetMarker == null)
        {
            for (int i = allLevels.Count - 1; i >= 0; i--)
            {
                if (IsLevelUnlocked(allLevels[i], allLevels))
                {
                    targetMarker = markersInScene.FirstOrDefault(m => m.worldNumber == allLevels[i].WorldNumber && m.levelNumber == allLevels[i].LevelNumber);
                    if (targetMarker != null) break;
                }
            }
        }
        
        if (targetMarker != null)
        {
            Vector3 finalPosition = new Vector3(cameraProxy.position.x, cameraProxy.position.y, targetMarker.transform.position.z);
            if (playIntroAnimation)
            {
                StartCoroutine(AnimateCameraToStartPosition(finalPosition));
            }
            else
            {
                cameraProxy.position = finalPosition;
            }
        }
    }

    private System.Collections.IEnumerator AnimateCameraToStartPosition(Vector3 targetPosition)
    {
        isAnimatingIntro = true;
        Vector3 startPosition = targetPosition + new Vector3(0, 0, -introStartOffset);
        cameraProxy.position = startPosition;

        float elapsedTime = 0f;
        while (elapsedTime < introAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = 1 - Mathf.Pow(1 - (elapsedTime / introAnimationDuration), 3); // Ease out curve
            cameraProxy.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        cameraProxy.position = targetPosition;
        isAnimatingIntro = false;
    }

    private bool IsLevelUnlocked(LevelInfo level, List<LevelInfo> allLevels)
    {
        if (level.WorldNumber == 1 && level.LevelNumber == 1) return true;
        LevelInfo prevLevel = GetPreviousLevel(level, allLevels);
        if (prevLevel != null)
        {
            string prevLevelKey = $"Level_{prevLevel.WorldNumber:00}_{prevLevel.LevelNumber:00}_Stars";
            return PlayerPrefs.GetInt(prevLevelKey, 0) > 0;
        }
        return false;
    }

    private LevelInfo GetPreviousLevel(LevelInfo currentLevel, List<LevelInfo> allLevels)
    {
        int currentIndex = allLevels.FindIndex(l => l.FilePath == currentLevel.FilePath);
        return (currentIndex > 0) ? allLevels[currentIndex - 1] : null;
    }
}