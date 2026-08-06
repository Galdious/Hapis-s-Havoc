using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world: an ordered run of levels, the theme they are painted with, and the mechanics they
/// are allowed to use. Progression data, deliberately separate from visual data.
///
/// WHY SEPARATE FROM ThemeDefinition
/// ---------------------------------
/// So the two can vary independently. A seasonal re-skin should be able to repaint world 1
/// without touching its level order or unlocks, and a re-ordering of levels should not be able
/// to change how anything looks. They are joined here by reference and nowhere else.
///
/// SCAFFOLDING ONLY - NOTHING READS THIS YET
/// -----------------------------------------
/// Progression currently runs through LevelSelectManager and PlayerPrefs keys, and this asset
/// does NOT participate in it. That is intentional: adopting it means migrating the existing
/// star and unlock persistence, which is a separate change with its own save-compatibility
/// risk. Creating these assets changes no runtime behaviour.
/// </summary>
[CreateAssetMenu(fileName = "World_", menuName = "Hapi's Havoc/World Definition", order = 1)]
public class WorldDefinition : ScriptableObject
{
    /// <summary>
    /// Mechanics a world may switch on. All of these are QUEUED, NOT BUILT - the flags exist so
    /// world assets do not need a schema change when the mechanics land. Nothing reads them.
    /// </summary>
    [Flags]
    public enum MechanicFlags
    {
        None        = 0,
        KeyAndLock  = 1 << 0,
        TollTiles   = 1 << 1,
        OneWayTiles = 1 << 2,
    }

    [Header("Identity")]
    [Tooltip("1-based, and the order worlds are presented in.")]
    public int worldIndex = 1;
    public string displayName = "Untitled World";

    [Header("Look")]
    public ThemeDefinition theme;

    [Header("Levels")]
    [Tooltip("Resource paths under Assets/Resources, in play order - e.g. 'Levels/01_01_BasicMoves'. " +
             "These are the same strings LevelSelectManager.LevelToLoad accepts, so adopting " +
             "this list later does not require a new format.")]
    public List<string> levelResourcePaths = new List<string>();

    [Header("Mechanics")]
    [Tooltip("Queued, not built. Nothing reads this yet.")]
    public MechanicFlags unlockedMechanics = MechanicFlags.None;

    public override string ToString() =>
        $"WorldDefinition({worldIndex}: '{displayName}', {levelResourcePaths.Count} levels, " +
        $"theme={(theme != null ? theme.id : "<none>")})";
}
