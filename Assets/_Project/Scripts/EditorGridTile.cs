/*
 *  EditorGridTile.cs
 *  ---------------------------------------------------------------
 *  A simple component added to grid tiles ONLY in the level editor.
 *  It listens for clicks and tells the LevelEditorManager to "paint"
 *  the current brush onto it.
 */

using UnityEngine;
using UnityEngine.EventSystems;

public class EditorGridTile : MonoBehaviour, IPointerClickHandler
{
    // These will be set by the LevelEditorManager
    public LevelEditorManager editorManager;
    public TileInstance tileInstance;

    public void OnPointerClick(PointerEventData eventData)
    {
        // Safety check
        if (editorManager == null || tileInstance == null) return;
        
        // Tell the manager that THIS tile was clicked for painting
        editorManager.OnGridTileClicked(tileInstance);
    }
}