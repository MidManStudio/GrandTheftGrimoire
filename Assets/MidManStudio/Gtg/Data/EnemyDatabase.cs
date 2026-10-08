// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/data.md, section "EnemyDatabase.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Mdix.Unity;
using UnityEngine;

namespace MidManStudio.Gtg.Data
{
    /// <summary>
    /// How an enemy behaves. The numbers are the ones <c>AIType</c> has in
    /// <c>Assets/game_enemies.mdix</c>. The bake reads the number, so a value cannot be
    /// renumbered on one side only.
    /// </summary>
    public enum EnemyAiType
    {
        Passive = 0,
        Neutral = 1,
        Aggressive = 2,
        Boss = 3,
    }

    /// <summary>
    /// Enemy rarity. <c>game_enemies.mdix</c> declares its own <c>Rarity</c> with four values
    /// and no Epic, which is a different list from the item one. Sharing a single C# enum
    /// would turn a Legendary enemy (3) into an Epic one, so enemies get their own.
    /// </summary>
    public enum EnemyRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Legendary = 3,
    }

    /// <summary>One row of the <c>enemies</c> table in <c>game_enemies.mdix</c>.</summary>
    [Serializable]
    public sealed class EnemyDefinition
    {
        public string Name;
        public int Health;
        public int Damage;

        // The file derives these two from the health with a division, so the engine stores
        // them as decimal numbers and they are floats here.
        public float Armor;
        public float Xp;

        public EnemyAiType AiType;
        public EnemyRarity Rarity;
        public float SpawnRate;
    }

    /// <summary>
    /// The enemy table from <c>Assets/game_enemies.mdix</c> as a ScriptableObject. Create it
    /// with right click on the file, MDIX, Generate ScriptableObject.
    /// </summary>
    [MdixBakeable("", "Enemy table (game_enemies.mdix)")]
    public sealed class EnemyDatabase : ScriptableObject
    {
        public int SpawnCap;

        // Stored as authored in the file, which does not state a unit.
        public int RespawnDelay;

        public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
    }
}
