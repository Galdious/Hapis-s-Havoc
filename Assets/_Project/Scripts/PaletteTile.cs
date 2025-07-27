using UnityEngine;
using UnityEngine.EventSystems; // <-- Add this namespace for the Event System

// Add IPointerClickHandler to the class definition
public class PaletteTile : MonoBehaviour, IPointerClickHandler 
{
    // These will be set by the LevelEditorManager when the tile is created
    public LevelEditorManager editorManager;
    public TileType myTileType;

    // This is the required method for the IPointerClickHandler interface.
    // It will be called automatically by the Event System when this tile is clicked.
    public void OnPointerClick(PointerEventData eventData)
    {
        // Tell the editor manager that I was the one who was clicked.
        editorManager.OnPaletteTileClicked(this);
    }
}