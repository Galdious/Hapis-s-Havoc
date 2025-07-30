using UnityEngine;
using UnityEngine.EventSystems;

public class EditorBankClickHandler : MonoBehaviour, IPointerClickHandler
{
    public LevelEditorManager editorManager;
    public RiverBankManager.BankSide bankSide;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (editorManager != null)
        {
            editorManager.OnBankClicked(bankSide);
        }
    }
}