// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedInventoryHost.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Items;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Inventory
{
    /// <summary>
    /// Puts the player's bag on Inventorizz. Add it to the player next to the <see cref="ManagedPlayerItemBag"/>.
    /// In <c>Start</c> it builds a <see cref="ManagedInventoryLink"/> from the items of that component's catalog
    /// and installs the link's bag with <see cref="ManagedPlayerItemBag.UseBag"/>, so the HUD, the quick slots and
    /// the shop read the container without knowing it. The bag keeps the slot count of the component. When the
    /// component gets another catalog, the host builds a new link over the new items.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedInventoryHost : MonoBehaviour
    {
        [Tooltip("The player's bag component. Left empty, the one on this GameObject is used.")]
        [SerializeField] private ManagedPlayerItemBag _player;

        private ManagedPlayerItemBag _attached;
        private ManagedInventoryLink _link;

        /// <summary>True while the player's bag is the Inventorizz container.</summary>
        public bool IsLinked { get { return _link != null; } }

        /// <summary>The link, or null while it is not linked. It is a new object each time the catalog changes.</summary>
        public ManagedInventoryLink Link { get { return _link; } }

        private void Start()
        {
            ManagedPlayerItemBag player = _player != null ? _player : GetComponent<ManagedPlayerItemBag>();
            if (player == null)
            {
                Debug.LogWarning("[GTG Inventory] " + name + " has no ManagedPlayerItemBag. Add one to the same object, "
                    + "or assign it, so there is a bag to put on Inventorizz.");
                return;
            }

            Detach();
            _attached = player;
            _attached.CatalogReplaced += OnCatalogReplaced;
            Connect();
        }

        // The component put a plain bag back with its new catalog, so the container is built again. Anything the
        // old container held is gone, the same as the plain bag that the component replaced.
        private void OnCatalogReplaced()
        {
            Connect();
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Detach()
        {
            if (_attached != null)
            {
                _attached.CatalogReplaced -= OnCatalogReplaced;
            }

            _attached = null;
            _link = null;
        }

        private void Connect()
        {
            IManagedItemList list = _attached.Catalog as IManagedItemList;
            if (list == null)
            {
                _link = null;
                Debug.LogWarning("[GTG Inventory] The catalog of the player's bag cannot list its items, so the bag stays "
                    + "the plain one. Use a ManagedItemCatalog or a ManagedItemDatabaseCatalog.");
                return;
            }

            _link = new ManagedInventoryLink(list.Items, _attached.Bag.SlotCount);
            _attached.UseBag(_link.Bag);
        }
    }
}
