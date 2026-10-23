// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedItemList.cs"
// ============================================================================

using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A catalog that can list every item it knows. <see cref="IManagedItemCatalog"/> answers one id at a
    /// time, so a system that must know the whole list in advance, such as the inventory bridge, asks for
    /// this as well.
    /// </summary>
    public interface IManagedItemList
    {
        /// <summary>Every item, ordered by id. Each read gives a new list, so the caller may keep it.</summary>
        IReadOnlyList<ManagedItemInfo> Items { get; }
    }
}
