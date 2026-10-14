// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedPlayerItemBag.cs"
// ============================================================================

using MidManStudio.Gtg.Data;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// Puts the player's bag and quick slots on a GameObject so other systems can find them, the
    /// way <c>ManagedPlayerWallet</c> does for the purse. The item table comes from the assigned
    /// <see cref="ItemDatabase"/>, or from a database asset named <c>ItemDatabase</c> in a
    /// Resources folder. With neither, the catalog is empty and the bag refuses every item.
    /// Nothing is saved.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedPlayerItemBag : MonoBehaviour
    {
        /// <summary>The Resources name tried when no database is assigned.</summary>
        public const string DatabaseResourceName = "ItemDatabase";

        [Tooltip("The baked item table. Left empty, an asset named ItemDatabase in a Resources folder is used.")]
        [SerializeField] private ItemDatabase _database;

        [Tooltip("Slots in the bag. A placeholder size.")]
        [SerializeField] private int _bagSlots = ManagedItemBag.DefaultSlotCount;

        [Tooltip("Slots in the quick row.")]
        [SerializeField] private int _quickSlots = ManagedQuickSlots.DefaultSlotCount;

        private IManagedItemCatalog _catalog;
        private ManagedItemBag _bag;
        private ManagedQuickSlots _quick;

        /// <summary>The player's bag.</summary>
        public ManagedItemBag Bag
        {
            get
            {
                Ensure();
                return _bag;
            }
        }

        /// <summary>The player's quick row. Its slots hold item ids, and the bag holds the items.</summary>
        public ManagedQuickSlots QuickSlots
        {
            get
            {
                Ensure();
                return _quick;
            }
        }

        /// <summary>The catalog the bag and the quick row read.</summary>
        public IManagedItemCatalog Catalog
        {
            get
            {
                Ensure();
                return _catalog;
            }
        }

        private void Awake()
        {
            Ensure();
        }

        /// <summary>
        /// Replaces the catalog and starts over with an empty bag and an empty quick row. A null
        /// catalog is an empty one. Whatever listened to the old quick row has to listen again.
        /// </summary>
        public void UseCatalog(IManagedItemCatalog catalog)
        {
            Build(catalog ?? new ManagedItemCatalog());
        }

        private void Ensure()
        {
            if (_bag != null)
            {
                return;
            }

            ItemDatabase database = _database != null ? _database : Resources.Load<ItemDatabase>(DatabaseResourceName);
            if (database == null)
            {
                Debug.LogWarning("ManagedPlayerItemBag on " + name + " has no ItemDatabase. Assign one, or name the asset "
                    + DatabaseResourceName + " in a Resources folder. The bag knows no items until it has a catalog.");
                Build(new ManagedItemCatalog());
                return;
            }

            Build(new ManagedItemDatabaseCatalog(database));
        }

        private void Build(IManagedItemCatalog catalog)
        {
            _catalog = catalog;
            _bag = new ManagedItemBag(catalog, _bagSlots);
            _quick = new ManagedQuickSlots(catalog, _quickSlots);
        }
    }
}
