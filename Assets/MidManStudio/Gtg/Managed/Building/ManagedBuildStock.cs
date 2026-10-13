// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedBuildStock.cs"
// ============================================================================

using System;
using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>One line of the stock: an item id and how many the player holds.</summary>
    public struct ManagedStockEntry
    {
        public string ItemId;
        public int Count;
    }

    /// <summary>
    /// What building reads and spends. A stock entry whose id matches a buildable definition is
    /// a placeable station. Other ids are materials that blueprint costs refer to. A game that
    /// keeps its items elsewhere, for instance in an Inventorizz container, implements this
    /// interface over that store and hands it to <see cref="ManagedBuildMode"/>.
    /// </summary>
    public interface IManagedBuildStock
    {
        int GetCount(string itemId);

        /// <summary>Takes <paramref name="count"/> items. Takes nothing and returns false when fewer are held.</summary>
        bool TryTake(string itemId, int count);

        void Give(string itemId, int count);

        /// <summary>Appends every entry with a count above zero, in a stable order.</summary>
        void CopyEntries(List<ManagedStockEntry> into);

        /// <summary>Raised after any count changed.</summary>
        event Action Changed;
    }

    /// <summary>In-memory stock. Entries keep the order in which their id first appeared.</summary>
    public sealed class ManagedBuildInventory : IManagedBuildStock
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private readonly List<string> _order = new List<string>();

        public event Action Changed;

        public int GetCount(string itemId)
        {
            int count;
            return itemId != null && _counts.TryGetValue(itemId, out count) ? count : 0;
        }

        public bool TryTake(string itemId, int count)
        {
            if (count <= 0 || GetCount(itemId) < count)
            {
                return false;
            }

            _counts[itemId] -= count;
            RaiseChanged();
            return true;
        }

        public void Give(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
            {
                return;
            }

            int current;
            if (!_counts.TryGetValue(itemId, out current))
            {
                _order.Add(itemId);
            }

            _counts[itemId] = current + count;
            RaiseChanged();
        }

        public void CopyEntries(List<ManagedStockEntry> into)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int count = _counts[_order[i]];
                if (count > 0)
                {
                    into.Add(new ManagedStockEntry { ItemId = _order[i], Count = count });
                }
            }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }

    /// <summary>The schematics the player has unlocked, by buildable id.</summary>
    public sealed class ManagedBlueprintBook
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();

        public event Action Changed;

        /// <summary>True when nothing gates the buildable, or when its schematic was unlocked.</summary>
        public bool IsAvailable(ManagedBuildableDefinition def)
        {
            return !def.RequiresBlueprint || _unlocked.Contains(def.Id);
        }

        public bool IsUnlocked(string buildableId)
        {
            return buildableId != null && _unlocked.Contains(buildableId);
        }

        public void Unlock(string buildableId)
        {
            if (!string.IsNullOrEmpty(buildableId) && _unlocked.Add(buildableId))
            {
                RaiseChanged();
            }
        }

        public void Lock(string buildableId)
        {
            if (buildableId != null && _unlocked.Remove(buildableId))
            {
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
