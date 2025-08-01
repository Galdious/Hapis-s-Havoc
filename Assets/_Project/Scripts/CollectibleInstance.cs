/*
 *  CollectibleInstance.cs
 *  ---------------------------------------------------------------
 *  A simple component attached to a collectible object on the grid.
 *  It holds a reference to its type.
 */

using UnityEngine;

public class CollectibleInstance : MonoBehaviour
{
    public CollectibleType type;

    [Tooltip("Only used if type is ExtraMove. How many moves to grant.")]
    public int value = 1; // <<< ADD THIS LINE






}