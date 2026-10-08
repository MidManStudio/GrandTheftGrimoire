// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/data.md, section "ItemDatabase.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Mdix.Unity;
using UnityEngine;

namespace MidManStudio.Gtg.Data
{
    /// <summary>Item category. Numbers match <c>ItemType</c> in <c>Assets/inventory_items.mdix</c>.</summary>
    public enum ItemType
    {
        Weapon = 0,
        Armor = 1,
        Consumable = 2,
        Quest = 3,
    }

    /// <summary>
    /// Item rarity, five values. The enemy file has its own four value list, see
    /// <see cref="EnemyRarity"/>.
    /// </summary>
    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    /// <summary>One row of the <c>items</c> table in <c>inventory_items.mdix</c>.</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        public int Id;
        public string Name;
        public ItemType Type;
        public ItemRarity Rarity;
        public int Value;

        // The file derives the weight from the value with a division, so it is a decimal number.
        public float Weight;

        public bool Stackable;
    }

    /// <summary>
    /// The item table from <c>Assets/inventory_items.mdix</c> as a ScriptableObject. Create it
    /// with right click on the file, MDIX, Generate ScriptableObject.
    /// </summary>
    [MdixBakeable("", "Item table (inventory_items.mdix)")]
    public sealed class ItemDatabase : ScriptableObject
    {
        public int MaxStackSize;

        /// <summary>
        /// A long text that <c>inventory_items.mdix</c> carries today under the key
        /// <c>keepSake</c>. It reads like a note and nothing uses it. It has a field so the bake
        /// reports no unused key, and both can go when the file is cleaned up.
        /// </summary>
        public string KeepSake;

        public List<ItemDefinition> Items = new List<ItemDefinition>();
    }
}
