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

    /// <summary>
    /// Called by the Event System on a short, complete click (down and up without dragging).
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        // A click (not a drag) will rotate the tile.
        RotateTile();
    }

    /// <summary>
    /// Called by the Event System at the moment a drag is detected.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        StopAllCoroutines(); // Stop any "return to hand" animation

        // --- NEW: Hide the counter on drag start ---
        CounterIndicatorTag indicator = GetComponentInChildren<CounterIndicatorTag>();
        if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
        // -----------------------------------------
    
        // --- NEW: Create a visual stand-in ---
        // 1. Instantiate a copy of ourself at our current position and rotation.
        GameObject standIn = Instantiate(this.gameObject, transform.position, transform.rotation, originalParent);
        standIn.name = $"{this.gameObject.name} (Stand-In)";
        // 2. Destroy the PlayableHandTile script on the stand-in so it's not interactive.
        Destroy(standIn.GetComponent<PlayableHandTile>());
        // 3. Add our marker script so we can find it later.
        standIn.AddComponent<HandTileStandIn>();
        // ------------------------------------
    




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

    /// <summary>
    /// Called by the Event System every frame a drag is in progress.
    /// </summary>

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

    /// <summary>
    /// Called by the Event System when the drag is released.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        SetLayerRecursively(this.gameObject, originalLayer);
        editorManager.gridManager.SetGridTilesLayer("Default"); // Tell GridManager to make grid tiles interactable again

        // Find and destroy the stand-in tile from the hand palette.
        HandTileStandIn standIn = FindFirstObjectByType<HandTileStandIn>();
        if (standIn != null)
        {
            Destroy(standIn.gameObject);
        }





        // If we are currently hovering over a valid drop zone...
        if (currentHoveredZone != null)
        {
            // Tell the zone it's no longer being hovered over.
            currentHoveredZone.OnHoverExit();

            // Tell the Level Editor to start the push action with our data.
            // We start a coroutine on the manager, which will handle the animation and logic.
            editorManager.StartCoroutine(editorManager.HandleDropZonePush(
                currentHoveredZone.row,
                currentHoveredZone.fromLeft,
                myTileType,
                this.gameObject
            ));

            // The tile has been successfully used, so we destroy its GameObject.
            // Destroy(gameObject);
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

    private IEnumerator AnimateBackToHand() // FIX #7
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
    }

   
   
   
   
   
   
   
   
   
   
   
   
   
   
   
    private void RotateTile()
    {
        if (myTileType == null) return;

        // Update the visual rotation.
        transform.Rotate(0, 180f, 0);

        // Update the underlying data to keep it in sync.


        // Store the new "home" rotation for if we cancel a drag.
        originalRotation = transform.rotation;

        Debug.Log($"Hand tile '{myTileType.displayName}' rotated."); // Modified log
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












}