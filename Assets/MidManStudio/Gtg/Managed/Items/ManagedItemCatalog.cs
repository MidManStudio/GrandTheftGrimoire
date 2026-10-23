// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemCatalog.cs"
// ============================================================================

using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A catalog filled from code. Tests, the shop test panel and any later source that has no
    /// catalog of its own use it. It holds plain values and knows nothing about Unity.
    /// </summary>
    public sealed class ManagedItemCatalog : IManagedItemCatalog, IManagedItemList
    {
        private readonly Dictionary<int, ManagedItemInfo> _items = new Dictionary<int, ManagedItemInfo>();

        public int Count { get { return _items.Count; } }

        public IReadOnlyList<ManagedItemInfo> Items
        {
            get
            {
                List<ManagedItemInfo> list = new List<ManagedItemInfo>(_items.Values);
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
                return list;
            }
        }

        /// <summary>Adds an item. An item with the same id is replaced.</summary>
        public void Add(ManagedItemInfo info)
        {
            _items[info.Id] = info;
        }

        public bool TryGet(int itemId, out ManagedItemInfo info)
        {
            return _items.TryGetValue(itemId, out info);
        }
    }
}
