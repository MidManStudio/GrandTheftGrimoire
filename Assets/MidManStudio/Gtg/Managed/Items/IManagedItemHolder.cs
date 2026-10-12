// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedItemHolder.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// Anything that can carry items for the player. The shop puts bought items in one and takes
    /// sold items out of one, and never learns what kind it is. <see cref="ManagedItemBag"/> is
    /// the plain one. An inventory package can be wrapped to fit the same four methods.
    /// </summary>
    public interface IManagedItemHolder
    {
        /// <summary>How many of an item the holder carries, in all its stacks.</summary>
        int CountOf(int itemId);

        /// <summary>True when <see cref="TryAdd"/> with the same arguments would succeed.</summary>
        bool CanAdd(int itemId, int count);

        /// <summary>Adds items, all or none. Returns false and changes nothing when they do not fit.</summary>
        bool TryAdd(int itemId, int count);

        /// <summary>Takes items out, all or none. Returns false and changes nothing when there are too few.</summary>
        bool TryRemove(int itemId, int count);
    }
}
