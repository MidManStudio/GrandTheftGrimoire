// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedItemCatalog.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A read-only list of the items that exist. The shop and the bag ask it and never learn
    /// where the answer is stored, so the item table, a test list or an inventory package can
    /// stand behind it.
    /// </summary>
    public interface IManagedItemCatalog
    {
        /// <summary>Looks an item up by id. Returns false for an id the catalog does not know.</summary>
        bool TryGet(int itemId, out ManagedItemInfo info);
    }
}
