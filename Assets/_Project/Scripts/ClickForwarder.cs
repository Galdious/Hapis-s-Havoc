/* ClickForwarder.cs (Safe Version) */
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickForwarder : MonoBehaviour, IPointerClickHandler
{
    // A reference to the specific LevelMarker script on the parent.
    private LevelMarker parentMarker;

    void Awake()
    {
        // Find the LevelMarker component in our parent.
        // This is safe because this script is not a LevelMarker itself.
        parentMarker = GetComponentInParent<LevelMarker>();
        
        if (parentMarker == null)
        {
            Debug.LogError("ClickForwarder could not find a LevelMarker component in any parent!", this);
        }
    }

    // This is called by the Event System when the mesh is clicked.
    public void OnPointerClick(PointerEventData eventData)
    {
        // If we found the parent marker, call its specific "HandleClick" method.
        if (parentMarker != null)
        {
            parentMarker.HandleClick();
        }
    }
}