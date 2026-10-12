// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemInfo.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// What a shop or a bag needs to know about one item: who it is, what it is worth in silver
    /// and how many fit in one stack. It is a plain value. Where the numbers come from is the
    /// business of an <see cref="IManagedItemCatalog"/>.
    /// </summary>
    public readonly struct ManagedItemInfo
    {
        public readonly int Id;
        public readonly string Name;

        /// <summary>The base worth of one item, counted in silver. Never negative.</summary>
        public readonly long ValueSilver;

        /// <summary>How many fit in one bag slot. 1 for an item that does not stack. Never below 1.</summary>
        public readonly int MaxStack;

        public ManagedItemInfo(int id, string name, long valueSilver, int maxStack)
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? "item " + id : name;
            ValueSilver = valueSilver < 0 ? 0 : valueSilver;
            MaxStack = maxStack < 1 ? 1 : maxStack;
        }

        public bool Stackable { get { return MaxStack > 1; } }
    }
}
