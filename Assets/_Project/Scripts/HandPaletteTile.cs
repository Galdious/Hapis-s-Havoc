/*
 *  HandPaletteTile.cs
 *  ---------------------------------------------------------------
 *  Added to tiles in the 3D "Hand" palette on the right.
 *  Tells the editor when it has been clicked for removal.
 */
using UnityEngine;
using UnityEngine.EventSystems;

public class HandPaletteTile : MonoBehaviour, IPointerClickHandler
{
    public LevelEditorManager editorManager;
    public TileType myTileType;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (editorManager != null)
        {
            editorManager.OnHandPaletteTileClicked(this);
        }
    }
}