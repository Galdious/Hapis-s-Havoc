/* BankClickHandler.cs */
using UnityEngine;
using UnityEngine.EventSystems;

public class BankClickHandler : MonoBehaviour, IPointerClickHandler
{
    public BoatController targetBoat;
    public RiverBankManager.BankSide bankSide;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetBoat != null) targetBoat.OnBankClicked(bankSide);
    }
}