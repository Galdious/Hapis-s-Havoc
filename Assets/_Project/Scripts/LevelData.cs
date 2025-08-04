/*
 *  LevelData.cs
 *  ---------------------------------------------------------------
 *  This file defines the data structures for saving and loading puzzle levels.
 *  These are simple C# classes, not MonoBehaviours, designed to be
 *  serialized to and from JSON.
 */

using System.Collections.Generic;
using UnityEngine;

// A small helper class to store the state of a single tile on the grid.
[System.Serializable]
public class TileSaveData
{
    public string tileTypeName; // We save the type by name to look it up in the library later
    public int gridX;
    public int gridY;
    public float rotationY;
    public bool isFlipped;
    public bool isHardBlocker;
}

// A helper class for collectibles.
[System.Serializable]
public class CollectibleSaveData
{
    public int gridX;
    public int gridY;
    public CollectibleType type;
    public int value; // For 'ExtraMove' collectibles
}

// A helper class for the player's starting hand.
[System.Serializable]
public class HandTileSaveData
{
    public string tileTypeName;
    public float rotationY;
    public bool isFlipped;
}

// A helper class to define a start or end goal.
// Using nullable types lets us know if the goal is a bank or a tile.
[System.Serializable]
public class GoalData
{
    // Use a boolean to determine if this goal is on a bank or a tile.
    public bool isBankGoal;

    // --- Fields for a TILE goal ---
    // We use -1 as a sentinel value to indicate "not set".
    public int tileX = -1;
    public int tileY = -1;
    public int snapPointIndex = -1; // Primarily for the start position.

    // --- Field for a BANK goal ---
    // This will only be relevant if isBankGoal is true.
    public RiverBankManager.BankSide bankSide;
}

// This is the main class that holds all the data for a single level.
[System.Serializable]
public class LevelData
{
    [Header("Grid & Rules")]
    public int gridWidth;
    public int gridHeight;
    public int maxMoves;
    //public bool[] lockedRows;
    public int[] lockedRows;

    [Header("Level Content")]
    public List<TileSaveData> tiles = new List<TileSaveData>();
    public List<CollectibleSaveData> collectibles = new List<CollectibleSaveData>();
    public List<HandTileSaveData> playerHand = new List<HandTileSaveData>();

    [Header("Goals")]
    public GoalData startPosition;
    public GoalData endPosition;
}