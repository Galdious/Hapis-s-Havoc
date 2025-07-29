/*
 *  TileBagManager.cs
 *  -------------------------------------------------------------
 *  Builds a runtime “bag” of tile templates (TileType objects)
 *  from the HapiTileLibrary ScriptableObject.  Lets you:
 *      • Draw random tiles without replacement   (DrawRandomTile)
 *      • Return tiles back into the bag          (ReturnTile)
 *  -------------------------------------------------------------
 *  HOW TO USE:
 *      1. Put this script in Assets/Scripts.
 *      2. Add it to an empty GameObject (e.g., BagManager) in your scene.
 *      3. Drag the HapiTileLibrary asset into the Inspector field.
 *      4. Press Play — watch the Console for debug output.
 */

using System.Collections.Generic;        // gives us List<>
using UnityEngine;                        // core Unity types

public class TileBagManager : MonoBehaviour
{
    // ===========================================================
    // 1. PUBLIC FIELDS  (visible in Inspector)
    // ===========================================================

    [Header("Data Asset")]
    [Tooltip("Drag your HapiTileLibrary ScriptableObject here.")]
    public TileLibrary tileLibrary;       // reference to the data asset

    [Header("Debug Options")]
    [Tooltip("How many tiles to auto-draw in Start() for a quick test.")]
    [Range(0, 10)]
    public int demoDrawCount = 0;

    // ===========================================================
    // 2. PRIVATE RUNTIME DATA
    // ===========================================================

    /// <summary>
    /// The actual “bag” we’ll draw from while the game runs.
    /// Stores references to TileType templates.
    /// </summary>
    private List<TileType> bag = new List<TileType>();

    // ===========================================================
    // 3. UNITY LIFECYCLE
    // ===========================================================

    private void Start()
    {
        // Safety: make sure designer linked the ScriptableObject.
        if (tileLibrary == null)
        {
            Debug.LogError("[TileBagManager] TileLibrary reference missing!");
            return;
        }

        BuildBag();    // fill + shuffle once when the scene starts

        Debug.Log($"[TileBagManager] Bag built with {bag.Count} tiles.");

        // OPTIONAL: draw a few tiles so you see it work in Console.
        for (int i = 0; i < demoDrawCount && bag.Count > 0; i++)
        {
            TileType drawn = DrawRandomTile();
            Debug.Log($"   • Drew tile: {drawn.displayName}.  {bag.Count} left.");
        }
    }

    // ===========================================================
    // 4. BAG CONSTRUCTION
    // ===========================================================

    /// <summary>
    /// Clears bag and refills it based on quantities in TileLibrary.
    /// Tiles with Quantity ≤ 0 are skipped (not in play this session).
    /// </summary>
    public void BuildBag()
    {
        bag.Clear();

        foreach (TileType type in tileLibrary.tileTypes)
        {
            if (type.quantity <= 0)
                continue;          // skip de-activated tiles

            // Add a reference 'quantity' times
            for (int i = 0; i < type.quantity; i++)
                bag.Add(type);
        }

        Shuffle(bag);              // randomise order
    }

    // ===========================================================
    // 5. PUBLIC BAG API
    // ===========================================================

    /// <summary> Draws one random tile template and removes it from the bag.
    /// Returns null if bag is empty. </summary>
    public TileType DrawRandomTile()
    {
        if (bag.Count == 0)
            return null;

        int idx = Random.Range(0, bag.Count);
        TileType chosen = bag[idx];
        bag.RemoveAt(idx);
        return chosen;
    }

    /// <summary> Puts a tile template back into the bag (e.g., tile left board). </summary>
    public void ReturnTile(TileType type)
    {
        if (type == null) return;  // guard against bad calls
        bag.Add(type);
    }

    /// <summary> How many tiles remain available to draw. </summary>
    public int TilesRemaining => bag.Count;

    // ===========================================================
    // 6. UTILITY — Fisher-Yates shuffle
    // ===========================================================

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);  // tuple swap
        }
    }


public void BuildBagFromHand(Dictionary<TileType, int> hand)
{
    bag.Clear();

    if (hand == null) return;

    foreach (var pair in hand)
    {
        TileType type = pair.Key;
        int quantity = pair.Value;
        
        for (int i = 0; i < quantity; i++)
        {
            bag.Add(type);
        }
    }

    Shuffle(bag);
    Debug.Log($"[TileBagManager] Bag built from player hand with {bag.Count} tiles.");
}











}
