/*
 *  PlayableHandTile.cs
 *  ---------------------------------------------------------------
 *  This component is added to hand tiles ONLY when in Play Mode.
 *  It makes the tiles interactive, handling tap-to-rotate and
 *  drag-and-drop functionality.
 */

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections; 

// [RequireComponent(typeof(CanvasGroup))]
public class PlayableHandTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- Public References (Set by LevelEditorManager) ---
    [HideInInspector] public TileType myTileType;
    [HideInInspector] public UIManager uiManager;
    [HideInInspector] public LevelEditorManager editorManager; // To call the push coroutine

    
    

    [HideInInspector] public int handCount = 0; 

    [HideInInspector] public HandTileAnimationSettings animationSettings; // It will receive the settings from the manager.
    [HideInInspector] public EndlessModeManager endlessManager;
    [HideInInspector] public OperatingMode currentMode;

    
    private bool isRotating = false; // Prevents spam-clicking




    // --- Settings ---
    private float liftHeight = 0.5f;
    private float returnAnimationTime = 0.2f;


    // --- Private State ---
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private Vector3 dragOffset; // The offset from the object's center to the initial click point
    private Plane dragPlane;    // A mathematical plane to drag the object along

    private RowDropZone currentHoveredZone = null;
    private CanvasGroup canvasGroup;
    private bool isDragging = false;

    private int originalLayer; // <<< --- ADD THIS LINE
    private int draggableLayer;








    void Awake()
    {
        if (endlessManager == null)
        {
            endlessManager = FindFirstObjectByType<EndlessModeManager>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        draggableLayer = LayerMask.NameToLayer("DraggableTile"); 
    }



    void Start()
    {
        // Store initial state for returning the tile if a drag is cancelled.
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalParent = transform.parent;
    }


    /// Called by the Event System on a short, complete click (down and up without dragging).
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging || isRotating) return;
        // A click (not a drag) will rotate the tile.
        RotateTile();
    }


    /// Called by the Event System at the moment a drag is detected.
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        StopAllCoroutines(); // Stop any "return to hand" animation



        // Only create a stand-in if this is NOT the last tile of its type.
        if (handCount > 1)
        {
            // 1. Instantiate a copy of ourself at our current position and rotation.
            GameObject standIn = Instantiate(this.gameObject, transform.position, transform.rotation, originalParent);
            standIn.name = $"{this.gameObject.name} (Stand-In)";
            // 2. Destroy the PlayableHandTile script on the stand-in so it's not interactive.
            Destroy(standIn.GetComponent<PlayableHandTile>());
            // 3. Add our marker script so we can find it later.
            standIn.AddComponent<HandTileStandIn>();
        }
    
        // --- NEW: Hide the counter on drag start ---
        CounterIndicatorTag indicator = GetComponentInChildren<CounterIndicatorTag>();
        if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
        // -----------------------------------------



        originalLayer = gameObject.layer;
        SetLayerRecursively(this.gameObject, draggableLayer);

        editorManager.gridManager.SetGridTilesLayer("DraggableTile"); // Tell GridManager to hide grid tiles

        // --- Prepare the tile for dragging ---
        // 1. Visually lift it by bringing it to a top-level container so it renders over everything.
        if (uiManager != null) transform.SetParent(uiManager.transform, true);

        Vector3 liftedPosition = originalPosition + Vector3.up * liftHeight;
        dragPlane = new Plane(Vector3.up, liftedPosition);

        // 3. Calculate the initial offset. This makes the drag feel natural,
        // as the tile won't "jump" to its center when you start dragging.
        Ray ray = eventData.pressEventCamera.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enter))
        {
            transform.position = ray.GetPoint(enter);
            dragOffset = transform.position - ray.GetPoint(enter);
        }
    }


    /// Called by the Event System every frame a drag is in progress.

    public void OnDrag(PointerEventData eventData)
    {
        // --- Move the tile with the pointer ---
        Ray ray = eventData.pressEventCamera.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enter))
        {
            // The new position is the point on the plane plus the original offset.
            transform.position = ray.GetPoint(enter) + dragOffset;
        }

        // --- Check for Drop Zones underneath ---
        RowDropZone newZone = null;
        // Check what the pointer is over right now.
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            newZone = eventData.pointerCurrentRaycast.gameObject.GetComponent<RowDropZone>();
        }

        // If the zone we are hovering over has changed...
        if (newZone != currentHoveredZone)
        {
            // ...leave the old zone (if there was one)...
            currentHoveredZone?.OnHoverExit();
            // ...and enter the new one (if it exists).
            newZone?.OnHoverEnter();
            // Update our state.
            currentHoveredZone = newZone;
        }
    }


    /// Called by the Event System when the drag is released.

    public void OnEndDrag(PointerEventData eventData)
    {
        SetLayerRecursively(this.gameObject, originalLayer);

        // CHANGE: Instead of always assuming editorManager exists, we check the mode.
        // We get the GridManager reference from the currently active manager.
        GridManager gridManager = null;
        if (currentMode == OperatingMode.Endless && endlessManager != null)
        {
            gridManager = endlessManager.GetComponentInChildren<GridManager>();
        }
        else if (editorManager != null) // This covers OperatingMode.Playing
        {
            gridManager = editorManager.gridManager;
        }

        if (gridManager != null)
        {
            gridManager.SetGridTilesLayer("Default");
        }


        // If we are currently hovering over a valid drop zone...
        if (currentHoveredZone != null)
        {
            HandTileStandIn standIn = FindFirstObjectByType<HandTileStandIn>();
            if (standIn != null)
            {
                Destroy(standIn.gameObject);
            }

            // Tell the zone it's no longer being hovered over.
            currentHoveredZone.OnHoverExit();

            // --- THE CORE LOGIC CHANGE IS HERE ---
            // This is the new 'if/else' block that directs the call to the correct manager.
            if (currentMode == OperatingMode.Endless)
            {
                // If we are in Endless Mode, we MUST call the EndlessModeManager.
                if (endlessManager != null)
                {
                    endlessManager.StartCoroutine(endlessManager.HandleEndlessPush(
                        currentHoveredZone,
                        myTileType,
                        this.gameObject
                    ));
                }
            }
            else // This will be true for OperatingMode.Playing (your Puzzle Mode)
            {
                // If we are in Puzzle Mode, call the LevelEditorManager, exactly like your original code.
                // This part is IDENTICAL to your old logic, ensuring nothing breaks.
                editorManager.StartCoroutine(editorManager.HandleDropZonePush(
                    currentHoveredZone.row,
                    currentHoveredZone.fromLeft,
                    myTileType,
                    this.gameObject
                ));
            }
            // --- END OF THE CORE LOGIC CHANGE ---
        }
        else // Otherwise, the drop was invalid.
        {
            // Return the tile to its original spot in the hand.
            StartCoroutine(AnimateBackToHand());
        }

        // Reset the hover state.
        currentHoveredZone = null;
        Invoke(nameof(ResetDragFlag), 0.1f);
    }


   
       private void ResetDragFlag()
    {
        isDragging = false;
    }

    private IEnumerator AnimateBackToHand()
    {
        transform.SetParent(originalParent, true);
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        while (elapsed < returnAnimationTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0, 1, elapsed / returnAnimationTime);
            transform.position = Vector3.Lerp(startPos, originalPosition, progress);
            transform.rotation = Quaternion.Slerp(startRot, originalRotation, progress);
            yield return null;
        }
        transform.position = originalPosition;
        transform.rotation = originalRotation;

        // Now that the animation is finished, destroy the stand-in.
        HandTileStandIn standIn = FindFirstObjectByType<HandTileStandIn>();
        if (standIn != null)
        {
            Destroy(standIn.gameObject);
        }

        // Find our own counter (which is currently inactive) and turn it back on.
        // The 'true' argument tells GetComponentInChildren to include inactive children in the search.
        CounterIndicatorTag indicator = GetComponentInChildren<CounterIndicatorTag>(true);
        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);
        }
    
        
    }



    private void RotateTile()
    {
        if (myTileType == null || isRotating) return;

        StartCoroutine(AnimateRotationCoroutine());

        Debug.Log($"Hand tile '{myTileType.displayName}' rotation started.");
    
    }
    


    /// Sets the layer for this GameObject and all of its children.
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;

        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    private IEnumerator AnimateRotationCoroutine()
    {

                if (animationSettings == null)
        {
            Debug.LogError("Animation Settings have not been assigned to this PlayableHandTile!", this);
            // Just snap to the end rotation as a fallback.
            transform.Rotate(0, 180f, 0);
            originalRotation = transform.rotation;
            yield break;
        }

        isRotating = true;

        Quaternion startRotation = transform.rotation;
        // We rotate 180 degrees on the Y-axis from our starting point.
        Quaternion endRotation = startRotation * Quaternion.Euler(0, 180f, 0);

        float elapsed = 0f;
        while (elapsed < animationSettings.rotationDuration)
        {
            elapsed += Time.deltaTime;
            // Evaluate the curve to get a smooth, eased progress value
            float progress = animationSettings.rotationCurve.Evaluate(elapsed / animationSettings.rotationDuration);

            // Slerp (Spherical Linear Interpolation) is the correct way to animate Quaternions
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, progress);

            yield return null;
        }

        // Snap to the final rotation to ensure it's perfect
        transform.rotation = endRotation;

        // Crucially, update our "home" rotation so if a drag is cancelled, it returns here.
        originalRotation = transform.rotation;

        isRotating = false;
    }




}