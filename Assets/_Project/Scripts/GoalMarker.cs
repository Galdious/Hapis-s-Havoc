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

    // We store a direct reference to the object we need to follow.
    private TileInstance targetTile;
    private Transform targetBankParent;

    private bool isFollowingTile = false;
    private bool isEndGoal = false;

    // This method is now responsible for creating and populating the new goalInfo structure.
    public void Setup(GridManager gridManager, TileInstance tile, RiverBankManager.BankSide? bank, int? snapIndex, bool isEndMarker)
    {
        this.isEndGoal = isEndMarker;

        goalInfo = new GoalData();

        if (bank.HasValue)
        {
            // This is a bank goal.
            goalInfo.isBankGoal = true;
            goalInfo.bankSide = bank.Value;

            isFollowingTile = false; // We are following a bank, not a tile.

            // --- NEW: Store the bank's transform for following ---
            RiverBankManager bankManager = FindFirstObjectByType<RiverBankManager>();
            if (bankManager != null)
            {
                targetBankParent = bankManager.GetBankGameObject(bank.Value)?.transform;
            }

        }
        else if (tile != null)
        {
            // This is a tile goal.
            goalInfo.isBankGoal = false;
            var coords = gridManager.GetTileCoordinates(tile);
            goalInfo.tileX = coords.x;
            goalInfo.tileY = coords.y;

            targetTile = tile;
            isFollowingTile = true; // We are now following a tile.

            // Only set the snap point if one was provided.
            if (snapIndex.HasValue)
            {
                goalInfo.snapPointIndex = snapIndex.Value;
            }
        }
    }
    
    // LateUpdate runs after all other updates, which is perfect for adjusting visuals.
    private void LateUpdate()
    {
        // --- MODIFIED LOGIC ---

        // If we are supposed to be following a tile...
        if (isFollowingTile)
        {
            // ...and that tile still exists...
            if (targetTile != null)
            {
                // ...update our position to match it.
                if (goalInfo.snapPointIndex != -1)
                {
                    transform.position = targetTile.snapPoints[goalInfo.snapPointIndex].position;
                }
                else
                {
                    transform.position = targetTile.transform.position;
                }
            }
            // ...but the tile has been destroyed (is now null)...
            else
            {
                // ...then our job is done. Destroy this marker.
                Destroy(gameObject);
            }
        }
        // Otherwise, if we are following a bank...
        else if (targetBankParent != null)
        {
            // ...just keep following the bank's transform.
            transform.position = targetBankParent.position;
        }
    }

    // This is called by Unity the moment the GameObject is destroyed.
    private void OnDestroy()
    {
        // If this was the end goal AND the game is currently being played...
        if (isEndGoal && GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
        {
            // ...tell the GameManager that the end goal was just destroyed.
            GameManager.Instance.OnEndGoalDestroyed();
        }
    }







}