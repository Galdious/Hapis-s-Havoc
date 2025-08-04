/*
 *  GameStateSnapshot.cs
 *  ---------------------------------------------------------------
 *  A data container that holds a complete snapshot of the game's
 *  state at a single moment in time. Used by the HistoryManager.
 */

using System.Collections.Generic;

[System.Serializable]
public class GameStateSnapshot
{
    // --- Grid State ---
    public List<TileSaveData> tileStates;
    public List<CollectibleSaveData> collectibleStates;
    public int[] lockedRowsState;

    // --- Boat State ---
    public int boatMovementPoints;
    public int boatStarsCollected;

    // We use a single GoalData object to store the boat's position,
    // as it can elegantly handle being on a tile OR a bank.
    public GoalData boatPosition;

    // --- Player/Puzzle State ---
    public List<HandTileSaveData> playerHandState;
    public int undoCount;
}