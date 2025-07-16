/*
 *  ClickableTile.cs
 *  ---------------------------------------------------------------
 *  Temporary component added to tiles when they become clickable
 *  for boat movement. Handles the click and notifies the boat.
 */

using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableTile : MonoBehaviour, IPointerClickHandler
{
    private BoatController targetBoat;
    private TileInstance tileInstance;
    
    public void Initialize(BoatController boat, TileInstance tile)
    {
        targetBoat = boat;
        tileInstance = tile;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetBoat != null && tileInstance != null)
        {
            targetBoat.OnTileClicked(tileInstance);
        }
    }
}