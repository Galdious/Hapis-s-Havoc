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


public class PlayableHandTile : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- Public References (Set by LevelEditorManager) ---
    [HideInInspector] public TileType myTileType; 
    [HideInInspector] public UIManager uiManager;
    [HideInInspector] public LevelEditorManager editorManager; // To call the push coroutine

    // --- Private State ---
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private Vector3 dragOffset; // The offset from the object's center to the initial click point
    private Plane dragPlane;    // A mathematical plane to drag the object along

    private RowDropZone currentHoveredZone = null;

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
        // A click (not a drag) will rotate the tile.
        RotateTile();
    }

    /// <summary>
    /// Called by the Event System at the moment a drag is detected.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        // --- Prepare the tile for dragging ---
        // 1. Visually lift it by bringing it to a top-level container so it renders over everything.
        if (uiManager != null) transform.SetParent(uiManager.transform, true);

        // 2. Create a plane at the tile's height to drag along.
        dragPlane = new Plane(Vector3.up, originalPosition);

        // 3. Calculate the initial offset. This makes the drag feel natural,
        // as the tile won't "jump" to its center when you start dragging.
        Ray ray = eventData.pressEventCamera.ScreenPointToRay(eventData.position);
        if (dragPlane.Raycast(ray, out float enter))
        {
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
                myTileType
            ));

            // The tile has been successfully used, so we destroy its GameObject.
            Destroy(gameObject);
        }
        else // Otherwise, the drop was invalid.
        {
            // Return the tile to its original spot in the hand.
            transform.SetParent(originalParent, true);
            transform.position = originalPosition;
            transform.rotation = originalRotation;
        }

        // Reset the hover state.
        currentHoveredZone = null;
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
}