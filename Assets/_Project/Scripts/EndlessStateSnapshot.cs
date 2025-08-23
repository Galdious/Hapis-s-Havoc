/* EndlessStateSnapshot.cs */
using System.Collections.Generic;

[System.Serializable]
public class EndlessStateSnapshot
{
    // --- Player Stats ---
    public int currentStamina;
    public int score;

    // --- Boat State ---
    // We reuse GoalData because it's perfect for storing a tile or bank position.
    public GoalData boatPosition;
    public int boatStarsCollected;

    // --- World State ---
    public List<TileSaveData> tileStates;
    public List<CollectibleSaveData> collectibleStates;

    // --- World Boundaries (Crucial for Endless) ---
    public int lowestGeneratedRow;
    public int highestGeneratedRow;
}