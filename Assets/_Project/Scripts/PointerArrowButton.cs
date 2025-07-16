/*
 *  PointerArrowButton.cs
 *  ---------------------------------------------------------------
 *  Modern event system for arrows using IPointerClickHandler.
 *  Works reliably on desktop, mobile, and web.
 *  Replace the OnMouse system with this.
 */

using UnityEngine;
using UnityEngine.EventSystems;

public class PointerArrowButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Arrow Data")]
    [HideInInspector] public int row;
    [HideInInspector] public bool fromLeft;
    [HideInInspector] public bool isRed;
    
    [Header("Visual Feedback")]
    public Color hoverColor = Color.yellow;
    
    private Renderer arrowRenderer;
    private Material originalMaterial;
    private Color originalColor;
    private RiverControls controller;
    
    public void Initialize(int row, bool fromLeft, bool isRed, RiverControls controller)
    {
        this.row = row;
        this.fromLeft = fromLeft;
        this.isRed = isRed;
        this.controller = controller;
        
        arrowRenderer = GetComponent<Renderer>();
        if (arrowRenderer != null)
        {
            originalMaterial = arrowRenderer.material;
            originalColor = originalMaterial.color;
        }
        
        Debug.Log($"PointerArrowButton initialized: Row {row}, From {(fromLeft ? "Left" : "Right")}, {(isRed ? "Red" : "Blue")}");
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"OnPointerClick triggered on {gameObject.name}");
        
        if (controller != null && controller.gridManager != null && !controller.gridManager.IsPushInProgress())
        {
            controller.gridManager.PushRowFromSide(row, fromLeft, isRed);
            Debug.Log($"Arrow clicked: Row {row}, From {(fromLeft ? "Left" : "Right")}, {(isRed ? "Red" : "Blue")}");
        }
        else
        {
            Debug.Log($"Cannot push: controller={controller != null}, gridManager={controller?.gridManager != null}, inProgress={controller?.gridManager?.IsPushInProgress()}");
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"Mouse entered {gameObject.name}");
        if (controller != null && controller.showHoverEffect && arrowRenderer != null)
        {
            arrowRenderer.material.color = hoverColor;
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"Mouse exited {gameObject.name}");
        if (controller != null && controller.showHoverEffect && arrowRenderer != null)
        {
            arrowRenderer.material.color = originalColor;
        }
    }
}