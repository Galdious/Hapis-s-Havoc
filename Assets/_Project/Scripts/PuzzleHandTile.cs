/*
 *  PuzzleHandTile.cs
 *  A simple data class to hold the state of a single
 *  tile available in the puzzle player's hand.
 */
using UnityEngine;

public class PuzzleHandTile
{
    public TileType tileType;
    public float rotationY = 0f;
    public bool isFlipped = false;
    
    // We'll add a unique ID to make finding and removing it easier
    public System.Guid id;

    public PuzzleHandTile(TileType type)
    {
        this.tileType = type;
        this.id = System.Guid.NewGuid();
    }
}