// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedQuickSlots.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// A short row of slots the player fills with items to reach them fast. A slot holds an item
    /// id and nothing else, so the amount carried is always read from the holder and a slot does
    /// not lose its item when the last one is used. The model holds plain values and knows nothing
    /// about Unity, and the HUD only draws it.
    /// </summary>
    public sealed class ManagedQuickSlots
    {
        /// <summary>The slot count used when none is given. A placeholder.</summary>
        public const int DefaultSlotCount = 5;

        private readonly IManagedItemCatalog _catalog;
        private readonly int[] _ids;
        private readonly bool[] _filled;

        /// <summary>Raised after any change to what the slots hold.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised by <see cref="TryUse"/> with the slot and the item. Using an item is not decided
        /// here. The listener that makes the item do something also takes it from the holder.
        /// </summary>
        public event Action<int, int> Used;

        /// <summary>
        /// The catalog is used to refuse items it does not know. A null catalog accepts any id. A
        /// slot count below 1 gives 1 slot.
        /// </summary>
        public ManagedQuickSlots(IManagedItemCatalog catalog = null, int slotCount = DefaultSlotCount)
        {
            _catalog = catalog;
            int slots = Math.Max(1, slotCount);
            _ids = new int[slots];
            _filled = new bool[slots];
        }

        public int SlotCount { get { return _ids.Length; } }

        /// <summary>Reads a slot. Returns false for an empty slot or an index outside the row.</summary>
        public bool TryGetItem(int slot, out int itemId)
        {
            if (slot < 0 || slot >= _ids.Length || !_filled[slot])
            {
                itemId = 0;
                return false;
            }

            itemId = _ids[slot];
            return true;
        }

        /// <summary>The first slot that holds the item, or -1.</summary>
        public int SlotOf(int itemId)
        {
            for (int i = 0; i < _ids.Length; i++)
            {
                if (_filled[i] && _ids[i] == itemId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Puts an item in a slot. An item sits in one slot at most, so an item already in another
        /// slot moves here and that slot empties. Returns false, and changes nothing, for a slot
        /// outside the row or an item the catalog does not know. Putting the item a slot already
        /// holds is accepted and raises no event.
        /// </summary>
        public bool Assign(int slot, int itemId)
        {
            if (slot < 0 || slot >= _ids.Length)
            {
                return false;
            }

            if (_catalog != null)
            {
                ManagedItemInfo info;
                if (!_catalog.TryGet(itemId, out info))
                {
                    return false;
                }
            }

            if (_filled[slot] && _ids[slot] == itemId)
            {
                return true;
            }

            int other = SlotOf(itemId);
            if (other >= 0)
            {
                _filled[other] = false;
                _ids[other] = 0;
            }

            _ids[slot] = itemId;
            _filled[slot] = true;
            RaiseChanged();
            return true;
        }

        /// <summary>Empties a slot. Returns false when it was already empty or outside the row.</summary>
        public bool Clear(int slot)
        {
            if (slot < 0 || slot >= _ids.Length || !_filled[slot])
            {
                return false;
            }

            _filled[slot] = false;
            _ids[slot] = 0;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Swaps two slots, one of which may be empty. Returns false for the same slot twice, a
        /// slot outside the row, or two empty slots.
        /// </summary>
        public bool Swap(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= _ids.Length || b >= _ids.Length)
            {
                return false;
            }

            if (!_filled[a] && !_filled[b])
            {
                return false;
            }

            int id = _ids[a];
            bool filled = _filled[a];
            _ids[a] = _ids[b];
            _filled[a] = _filled[b];
            _ids[b] = id;
            _filled[b] = filled;
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// Uses the item in a slot. Returns false, and raises nothing, for an empty slot, a slot
        /// outside the row, or a holder that carries none of the item. The holder is only read.
        /// </summary>
        public bool TryUse(int slot, IManagedItemHolder holder)
        {
            int itemId;
            if (holder == null || !TryGetItem(slot, out itemId) || holder.CountOf(itemId) < 1)
            {
                return false;
            }

            Action<int, int> used = Used;
            if (used != null)
            {
                used(slot, itemId);
            }

            return true;
        }

        private void RaiseChanged()
        {
            Action changed = Changed;
            if (changed != null)
            {
                changed();
            }
        }
    }
}
