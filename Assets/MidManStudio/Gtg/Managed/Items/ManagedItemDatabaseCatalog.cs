// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemDatabaseCatalog.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Data;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// Shows the baked item table (<see cref="ItemDatabase"/>) as an <see cref="IManagedItemCatalog"/>.
    /// This is the only file of the Items folder that knows the Data folder. If the table changes
    /// shape, this adapter changes and nothing else does.
    /// </summary>
    public sealed class ManagedItemDatabaseCatalog : IManagedItemCatalog
    {
        private readonly Dictionary<int, ManagedItemInfo> _items = new Dictionary<int, ManagedItemInfo>();

        /// <summary>
        /// Reads the table once. A null table gives an empty catalog. A row that is null is
        /// skipped, and when two rows share an id the first one is used.
        /// </summary>
        public ManagedItemDatabaseCatalog(ItemDatabase database)
        {
            if (database == null || database.Items == null)
            {
                return;
            }

            int maxStack = database.MaxStackSize;
            List<ItemDefinition> rows = database.Items;
            for (int i = 0; i < rows.Count; i++)
            {
                ItemDefinition row = rows[i];
                if (row == null || _items.ContainsKey(row.Id))
                {
                    continue;
                }

                // The table says whether an item stacks and gives one table-wide stack size, and
                // ManagedItemInfo turns a size below 1 into 1. Its Value is read as silver, which the
                // table does not state.
                _items.Add(row.Id, new ManagedItemInfo(row.Id, row.Name, row.Value, row.Stackable ? maxStack : 1));
            }
        }

        public int Count { get { return _items.Count; } }

        public bool TryGet(int itemId, out ManagedItemInfo info)
        {
            return _items.TryGetValue(itemId, out info);
        }
    }
}
