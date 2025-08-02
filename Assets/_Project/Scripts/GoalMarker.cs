/*
 *  GoalMarker.cs
 *  A simple data component attached to Start/End markers.
 *  It holds a copy of the GoalData, making saving and loading robust.
 *  It now has a Setup method to initialize itself.
 */
using UnityEngine;

public class GoalMarker : MonoBehaviour
{

    public GoalData goalInfo;


    // This method is now responsible for creating and populating the new goalInfo structure.
    public void Setup(GridManager gridManager, TileInstance tile, RiverBankManager.BankSide? bank, int? snapIndex)
    {
        goalInfo = new GoalData();

        if (bank.HasValue)
        {
            // This is a bank goal.
            goalInfo.isBankGoal = true;
            goalInfo.bankSide = bank.Value;
        }
        else if (tile != null)
        {
            // This is a tile goal.
            goalInfo.isBankGoal = false;
            var coords = gridManager.GetTileCoordinates(tile);
            goalInfo.tileX = coords.x;
            goalInfo.tileY = coords.y;

            // Only set the snap point if one was provided.
            if (snapIndex.HasValue)
            {
                goalInfo.snapPointIndex = snapIndex.Value;
            }
        }
    }
}