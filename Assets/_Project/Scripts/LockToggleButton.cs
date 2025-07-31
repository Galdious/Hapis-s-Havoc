/*
 *  LockToggleButton.cs
 *  ---------------------------------------------------------------
 *  A simple component for the lock icon in the level editor.
 *  It listens for clicks and tells the RiverControls manager
 *  to toggle the lock state for its assigned row.
 */

using UnityEngine;
using UnityEngine.EventSystems;

public class LockToggleButton : MonoBehaviour, IPointerClickHandler
{
    // These will be set by the RiverControls script when spawned
    [HideInInspector] public int row;
    [HideInInspector] public RiverControls controller;

    public void OnPointerClick(PointerEventData eventData)
    {
        // When clicked, just notify the main controller.
        if (controller != null)
        {
            controller.OnLockButtonClicked(row);
        }
    }
}