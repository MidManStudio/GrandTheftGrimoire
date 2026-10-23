// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedInventoryBag.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Inventorizz.Core;
using InventoryItem = MidManStudio.Inventorizz.Core.ItemDefinition;

namespace MidManStudio.Gtg.Managed.Inventory
{
    /// <summary>
    /// One Inventorizz container shown as a GTG bag. It answers the four holder methods and the three slot
    /// readers with the ids and the all or none rules that <see cref="ManagedItemBag"/> follows, so the shop,
    /// the HUD and the quick slots treat the two the same. Inventorizz itself fills as much as fits and
    /// reports how much, so the adapter works out whether everything fits before it adds anything. It keeps no
    /// copy of the slots. Every read goes to the container, so a restored save or a lock shows at once.
    /// </summary>
    public sealed class ManagedInventoryBag : IManagedItemSlots
    {
        private readonly InventorizzRuntime _runtime;
        private readonly ManagedInventoryCatalog _catalog;
        private readonly string _containerId;

        /// <summary>
        /// The container has to exist already, and the catalog has to be the one the runtime was built with.
        /// </summary>
        public ManagedInventoryBag(InventorizzRuntime runtime, ManagedInventoryCatalog catalog, string containerId)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            if (string.IsNullOrEmpty(containerId))
            {
                throw new ArgumentException("A container id is needed.", "containerId");
            }

            _runtime = runtime;
            _catalog = catalog;
            _containerId = containerId;
        }

        /// <summary>The Inventorizz container this bag shows.</summary>
        public string ContainerId { get { return _containerId; } }

        /// <summary>The slots of the container. 0 once the container is destroyed.</summary>
        public int SlotCount { get { return _runtime.GetSlots(_containerId).Count; } }

        /// <summary>The number of slots that hold something.</summary>
        public int UsedSlots { get { return _runtime.GetUsedSlotCount(_containerId); } }

        /// <summary>
        /// Reads a slot. Returns false for an empty slot, an index outside the container, or an item the
        /// catalog does not know.
        /// </summary>
        public bool TryGetSlot(int slot, out int itemId, out int count)
        {
            itemId = 0;
            count = 0;

            IReadOnlyList<ContainerSlotRow> slots = _runtime.GetSlots(_containerId);
            if (slot < 0 || slot >= slots.Count)
            {
                return false;
            }

            ContainerSlotRow row = slots[slot];
            int id;
            if (row.Count <= 0 || !_catalog.TryGetItemId(row.ItemId, out id))
            {
                return false;
            }

            itemId = id;
            count = row.Count;
            return true;
        }

        public int CountOf(int itemId)
        {
            string key = _catalog.KeyOf(itemId);
            if (key == null)
            {
                return 0;
            }

            IReadOnlyList<ContainerSlotRow> slots = _runtime.GetSlots(_containerId);
            long total = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                ContainerSlotRow row = slots[i];
                if (row.Count > 0 && row.ItemId == key)
                {
                    total += row.Count;
                }
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        public bool CanAdd(int itemId, int count)
        {
            return count >= 1 && RoomFor(itemId) >= count;
        }

        /// <summary>
        /// Adds all of it or none of it. A container that is locked, a destroyed one and an item the catalog
        /// does not know refuse. If Inventorizz still takes only part of it, the part is taken out again and
        /// the call is false.
        /// </summary>
        public bool TryAdd(int itemId, int count)
        {
            if (!CanAdd(itemId, count))
            {
                return false;
            }

            string key = _catalog.KeyOf(itemId);
            int added;
            if (!_runtime.TryAddItem(_containerId, key, count, out added))
            {
                return false;
            }

            if (added == count)
            {
                return true;
            }

            _runtime.TryRemoveItem(_containerId, key, added);
            return false;
        }

        /// <summary>
        /// Takes all of it or none of it, from the last slot first, so the earlier stacks stay full. That is
        /// the order of <see cref="ManagedItemBag"/>. Inventorizz takes from the first slot.
        /// </summary>
        public bool TryRemove(int itemId, int count)
        {
            string key = _catalog.KeyOf(itemId);
            if (count < 1 || key == null || _runtime.IsLocked(_containerId) || CountOf(itemId) < count)
            {
                return false;
            }

            IReadOnlyList<ContainerSlotRow> slots = _runtime.GetSlots(_containerId);
            int left = count;
            for (int i = slots.Count - 1; i >= 0 && left > 0; i--)
            {
                ContainerSlotRow row = slots[i];
                if (row.Count > 0 && row.ItemId == key)
                {
                    int take = Math.Min(left, row.Count);
                    _runtime.TryRemoveFromSlot(_containerId, i, take);
                    left -= take;
                }
            }

            return true;
        }

        // How many of an item fit, counted the way TryAddItem places them: the room left in the stacks the item
        // already has, then a full stack for each empty slot. The container type of the bridge has a fixed slot
        // count and no weight limit, so nothing else limits it. A container of any other type has no room here.
        private long RoomFor(int itemId)
        {
            string key = _catalog.KeyOf(itemId);
            ContainerInstanceRow instance = _runtime.GetInstance(_containerId);
            if (key == null || instance == null || _runtime.IsLocked(_containerId))
            {
                return 0;
            }

            InventoryItem item = _catalog.FindItem(key);
            if (item == null || _catalog.FindContainerType(instance.ContainerTypeId) == null)
            {
                return 0;
            }

            long maxStack = Math.Max(1, item.MaxStack);
            long room = 0;
            IReadOnlyList<ContainerSlotRow> slots = _runtime.GetSlots(_containerId);
            for (int i = 0; i < slots.Count; i++)
            {
                ContainerSlotRow row = slots[i];
                if (row.Count <= 0 || string.IsNullOrEmpty(row.ItemId))
                {
                    room += maxStack;
                }
                else if (row.ItemId == key && row.Count < maxStack)
                {
                    room += maxStack - row.Count;
                }
            }

            return room;
        }
    }
}
