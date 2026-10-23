// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedInventoryCatalog.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Inventorizz.Core;
using MidManStudio.Inventorizz.Generated;
using InventoryItem = MidManStudio.Inventorizz.Core.ItemDefinition;

namespace MidManStudio.Gtg.Managed.Inventory
{
    /// <summary>
    /// The content of Inventorizz for GTG, built in code from a list of <see cref="ManagedItemInfo"/> instead
    /// of from a .mdix file. Every item becomes an Inventorizz item with the id <c>item_&lt;id&gt;</c>, and
    /// one container type, <see cref="BagTypeId"/>, holds a fixed number of slots. It also maps the two id
    /// spaces, because GTG items have integer ids and Inventorizz items have text ids. The content never
    /// changes after construction, so <see cref="DefinitionsChanged"/> is never raised.
    /// </summary>
    public sealed class ManagedInventoryCatalog : IInventoryCatalog
    {
        /// <summary>The id of the one container type.</summary>
        public const string BagTypeId = "gtg_bag";

        /// <summary>What every Inventorizz item id starts with, followed by the GTG item id.</summary>
        public const string ItemKeyPrefix = "item_";

        private readonly List<InventoryItem> _items = new List<InventoryItem>();
        private readonly List<ContainerTypeDefinition> _types = new List<ContainerTypeDefinition>();
        private readonly Dictionary<string, InventoryItem> _itemByKey = new Dictionary<string, InventoryItem>();
        private readonly Dictionary<string, int> _idByKey = new Dictionary<string, int>();
        private readonly Dictionary<int, string> _keyById = new Dictionary<int, string>();

        /// <summary>
        /// Reads the list once. A null list gives a catalog with no items. When two entries share an id the
        /// first one is used. A slot count below 1 gives 1 slot.
        /// </summary>
        public ManagedInventoryCatalog(IEnumerable<ManagedItemInfo> items, int bagSlots)
        {
            if (items != null)
            {
                foreach (ManagedItemInfo info in items)
                {
                    if (_keyById.ContainsKey(info.Id))
                    {
                        continue;
                    }

                    string key = ItemKeyPrefix + info.Id.ToString(CultureInfo.InvariantCulture);
                    InventoryItem item = new InventoryItem();
                    item.Id = key;
                    item.DisplayKey = info.Name;
                    item.Weight = 0f;
                    item.MaxStack = info.MaxStack;
                    item.StackPolicy = info.Stackable ? StackPolicy.STACKABLE : StackPolicy.UNIQUE;

                    _items.Add(item);
                    _itemByKey.Add(key, item);
                    _idByKey.Add(key, info.Id);
                    _keyById.Add(info.Id, key);
                }
            }

            ContainerTypeDefinition bag = new ContainerTypeDefinition();
            bag.Id = BagTypeId;
            bag.DisplayKey = "Bag";
            bag.CapacityKind = CapacityKind.SLOT_COUNT;
            bag.MaxWeight = 0f;
            bag.MaxSlots = Math.Max(1, bagSlots);
            _types.Add(bag);
        }

        /// <summary>The slots of the bag container type.</summary>
        public int BagSlots { get { return _types[0].MaxSlots; } }

        public IReadOnlyList<InventoryItem> AllItems { get { return _items; } }

        public IReadOnlyList<ContainerTypeDefinition> AllContainerTypes { get { return _types; } }

        public InventoryItem FindItem(string itemId)
        {
            InventoryItem found;
            return itemId != null && _itemByKey.TryGetValue(itemId, out found) ? found : null;
        }

        public ContainerTypeDefinition FindContainerType(string containerTypeId)
        {
            return containerTypeId == BagTypeId ? _types[0] : null;
        }

        /// <summary>Never raised, and subscribing does nothing. The content is fixed at construction.</summary>
        public event Action DefinitionsChanged
        {
            add { }
            remove { }
        }

        /// <summary>The Inventorizz id of a GTG item, or null for an item the catalog does not know.</summary>
        public string KeyOf(int itemId)
        {
            string key;
            return _keyById.TryGetValue(itemId, out key) ? key : null;
        }

        /// <summary>The GTG id behind an Inventorizz id. Returns false for an id the catalog does not know.</summary>
        public bool TryGetItemId(string key, out int itemId)
        {
            if (key == null)
            {
                itemId = 0;
                return false;
            }

            return _idByKey.TryGetValue(key, out itemId);
        }
    }
}
