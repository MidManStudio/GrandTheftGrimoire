// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedInventoryLink.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Inventorizz.Core;

namespace MidManStudio.Gtg.Managed.Inventory
{
    /// <summary>
    /// The whole bridge as plain values: the item content, an Inventorizz runtime, the player's one container
    /// and the <see cref="ManagedInventoryBag"/> that shows it to GTG. It has no Unity types, so the checks
    /// build it directly, and <see cref="ManagedInventoryHost"/> only decides when to build it. Saving is
    /// <see cref="Capture"/> and <see cref="Restore"/>, and the link stores nothing itself.
    /// </summary>
    public sealed class ManagedInventoryLink
    {
        /// <summary>The Inventorizz owner of the container.</summary>
        public const string OwnerId = "player";

        /// <summary>The instance id of the container. It is fixed, so a saved game finds the same container.</summary>
        public const string ContainerKey = "player_bag";

        private readonly ManagedInventoryCatalog _catalog;
        private readonly InventorizzRuntime _runtime;
        private readonly ManagedInventoryBag _bag;

        /// <summary>
        /// Builds the content from the items, a runtime over it and an empty container of
        /// <paramref name="bagSlots"/> slots. A slot count below 1 gives 1 slot.
        /// </summary>
        public ManagedInventoryLink(IEnumerable<ManagedItemInfo> items, int bagSlots)
        {
            _catalog = new ManagedInventoryCatalog(items, bagSlots);
            _runtime = new InventorizzRuntime(_catalog);
            _runtime.CreateContainer(OwnerId, ManagedInventoryCatalog.BagTypeId, ContainerKey);
            _bag = new ManagedInventoryBag(_runtime, _catalog, ContainerKey);
        }

        public ManagedInventoryCatalog Catalog { get { return _catalog; } }

        /// <summary>The Inventorizz runtime, for anything the GTG interfaces do not cover, such as locking the container.</summary>
        public InventorizzRuntime Runtime { get { return _runtime; } }

        /// <summary>The container as a GTG bag.</summary>
        public ManagedInventoryBag Bag { get { return _bag; } }

        /// <summary>What a save keeps: the instances and their filled slots.</summary>
        public InventoryProgressSnapshot Capture()
        {
            return _runtime.CaptureState();
        }

        /// <summary>
        /// Replaces the state with a saved one. A snapshot that has no player container, such as an empty one,
        /// leaves a new empty container, so the bag keeps working. A saved slot that holds an item the catalog
        /// no longer knows stays in the container and is invisible to the bag, see <see cref="ManagedInventoryBag"/>.
        /// </summary>
        public void Restore(InventoryProgressSnapshot snapshot)
        {
            _runtime.RestoreState(snapshot ?? new InventoryProgressSnapshot());
            if (!_runtime.ContainerExists(ContainerKey))
            {
                _runtime.CreateContainer(OwnerId, ManagedInventoryCatalog.BagTypeId, ContainerKey);
            }
        }
    }
}
