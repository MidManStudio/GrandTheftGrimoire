// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemBag.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A small bag of slots. A slot holds one kind of item and up to its stack size, and an item
    /// that does not stack takes a slot of its own. It stands in until an inventory package is
    /// used, and the shop only sees <see cref="IManagedItemHolder"/>. It holds plain values and
    /// knows nothing about Unity.
    /// </summary>
    public sealed class ManagedItemBag : IManagedItemHolder
    {
        /// <summary>The slot count used when none is given. A placeholder.</summary>
        public const int DefaultSlotCount = 20;

        private readonly IManagedItemCatalog _catalog;
        private readonly int[] _ids;
        private readonly int[] _counts;

        /// <summary>
        /// The catalog tells the bag how far an item stacks. A bag with no catalog refuses every
        /// item. A slot count below 1 gives 1 slot.
        /// </summary>
        public ManagedItemBag(IManagedItemCatalog catalog, int slotCount = DefaultSlotCount)
        {
            _catalog = catalog;
            int slots = Math.Max(1, slotCount);
            _ids = new int[slots];
            _counts = new int[slots];
        }

        public int SlotCount { get { return _ids.Length; } }

        /// <summary>The number of slots that hold something.</summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < _counts.Length; i++)
                {
                    if (_counts[i] > 0)
                    {
                        used++;
                    }
                }

                return used;
            }
        }

        /// <summary>Reads a slot. Returns false for an empty slot or an index outside the bag.</summary>
        public bool TryGetSlot(int slot, out int itemId, out int count)
        {
            if (slot < 0 || slot >= _ids.Length || _counts[slot] <= 0)
            {
                itemId = 0;
                count = 0;
                return false;
            }

            itemId = _ids[slot];
            count = _counts[slot];
            return true;
        }

        public int CountOf(int itemId)
        {
            long total = 0;
            for (int i = 0; i < _ids.Length; i++)
            {
                if (_counts[i] > 0 && _ids[i] == itemId)
                {
                    total += _counts[i];
                }
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        public bool CanAdd(int itemId, int count)
        {
            if (count < 1 || _catalog == null)
            {
                return false;
            }

            ManagedItemInfo info;
            if (!_catalog.TryGet(itemId, out info))
            {
                return false;
            }

            return count <= RoomFor(itemId, info.MaxStack);
        }

        public bool TryAdd(int itemId, int count)
        {
            if (!CanAdd(itemId, count))
            {
                return false;
            }

            ManagedItemInfo info;
            _catalog.TryGet(itemId, out info);
            int max = info.MaxStack;
            int left = count;

            // Fill the stacks the item already has, then open empty slots from the front.
            for (int i = 0; i < _ids.Length && left > 0; i++)
            {
                if (_counts[i] > 0 && _ids[i] == itemId && _counts[i] < max)
                {
                    int put = Math.Min(left, max - _counts[i]);
                    _counts[i] += put;
                    left -= put;
                }
            }

            for (int i = 0; i < _ids.Length && left > 0; i++)
            {
                if (_counts[i] <= 0)
                {
                    int put = Math.Min(left, max);
                    _ids[i] = itemId;
                    _counts[i] = put;
                    left -= put;
                }
            }

            return true;
        }

        public bool TryRemove(int itemId, int count)
        {
            if (count < 1 || CountOf(itemId) < count)
            {
                return false;
            }

            // Take from the last slot first, so the earlier stacks stay full.
            int left = count;
            for (int i = _ids.Length - 1; i >= 0 && left > 0; i--)
            {
                if (_counts[i] > 0 && _ids[i] == itemId)
                {
                    int take = Math.Min(left, _counts[i]);
                    _counts[i] -= take;
                    left -= take;
                }
            }

            return true;
        }

        // The room left for an item: space in its own partial stacks plus a full stack for every
        // empty slot. A stack already over the size, after the catalog shrank, has no room.
        private long RoomFor(int itemId, int max)
        {
            long room = 0;
            for (int i = 0; i < _ids.Length; i++)
            {
                if (_counts[i] <= 0)
                {
                    room += max;
                }
                else if (_ids[i] == itemId)
                {
                    room += Math.Max(0, max - _counts[i]);
                }
            }

            return room;
        }
    }
}
