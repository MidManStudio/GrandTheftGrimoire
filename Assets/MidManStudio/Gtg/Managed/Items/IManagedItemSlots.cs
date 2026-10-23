// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedItemSlots.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A holder whose slots can be read, so an overlay can draw what it carries. The HUD and the shop
    /// test panel read the player's bag through this and never learn whether the plain
    /// <see cref="ManagedItemBag"/> or an inventory package stands behind it. The shop itself needs
    /// only <see cref="IManagedItemHolder"/>.
    /// </summary>
    public interface IManagedItemSlots : IManagedItemHolder
    {
        /// <summary>How many slots there are. A holder with no fixed grid reports the slots it uses now.</summary>
        int SlotCount { get; }

        /// <summary>The number of slots that hold something.</summary>
        int UsedSlots { get; }

        /// <summary>Reads a slot. Returns false for an empty slot or an index outside the holder.</summary>
        bool TryGetSlot(int slot, out int itemId, out int count);
    }
}
