// Checks for the Managed Inventory bridge. Verification harness only. They run the real Inventorizz runtime, fetched
// at the commit pinned in Packages/manifest.json, behind the GTG bag interfaces, and compare it with the plain bag.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Inventory;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Inventorizz.Core;
using MidManStudio.Inventorizz.Generated;
using InvItem = MidManStudio.Inventorizz.Core.ItemDefinition;

namespace MidManStudio.Gtg.Managed.Quests.Tests
{
    /// <summary>A catalog that answers one id at a time and cannot list its items.</summary>
    public sealed class OpaqueCatalog : IManagedItemCatalog
    {
        public bool TryGet(int itemId, out ManagedItemInfo info)
        {
            info = default(ManagedItemInfo);
            return false;
        }
    }

    public static partial class Tests
    {
        private const int InvSword = 1;
        private const int InvPotion = 2;
        private const int InvScale = 3;

        // A sword and a scale do not stack, a potion stacks to 10.
        private static ManagedItemCatalog InvItems()
        {
            ManagedItemCatalog catalog = new ManagedItemCatalog();
            catalog.Add(new ManagedItemInfo(InvSword, "Iron Sword", 50, 1));
            catalog.Add(new ManagedItemInfo(InvPotion, "Health Potion", 25, 10));
            catalog.Add(new ManagedItemInfo(InvScale, "Dragon Scale", 5000, 1));
            return catalog;
        }

        private static ManagedInventoryLink InvLink(int slots)
        {
            return new ManagedInventoryLink(InvItems().Items, slots);
        }

        private static bool HasInventoryWarning()
        {
            return UnityEngine.Debug.Warnings.Exists(w => w.StartsWith("[GTG Inventory]"));
        }

        // ---- the content ----

        private static void InventoryCatalogChecks()
        {
            ManagedInventoryCatalog catalog = new ManagedInventoryCatalog(InvItems().Items, 5);
            IInventoryCatalog asInterface = catalog;
            Eq(asInterface.AllItems.Count, 3, "one Inventorizz item for each GTG item");
            Eq(asInterface.AllItems[0].Id, "item_1", "ordered by id, as the list was");
            Eq(asInterface.AllItems[2].Id, "item_3", "to the last item");
            Eq(catalog.AllContainerTypes.Count, 1, "one container type");
            Eq(catalog.BagSlots, 5, "the bag has the slot count that was asked for");

            Eq(catalog.KeyOf(InvPotion), "item_2", "the key of an item is item_ and its id");
            Eq<string>(catalog.KeyOf(99), null, "an item the catalog does not know has no key");

            int id;
            Check(catalog.TryGetItemId("item_2", out id) && id == InvPotion, "a key gives the GTG id back");
            Check(!catalog.TryGetItemId("item_77", out id), "a key of an unknown item gives nothing");
            Check(!catalog.TryGetItemId("nonsense", out id), "so does a text that is not a key");
            Check(!catalog.TryGetItemId(null, out id), "and null");

            InvItem potion = catalog.FindItem("item_2");
            Check(potion != null, "FindItem finds the potion");
            Eq(potion.MaxStack, 10, "the stack size is the GTG one");
            Eq(potion.StackPolicy, StackPolicy.STACKABLE, "a potion stacks");
            Eq(potion.DisplayKey, "Health Potion", "the display key is the GTG name");
            Eq(potion.Weight, 0f, "nothing has weight, the bag has no weight limit");

            InvItem sword = catalog.FindItem("item_1");
            Eq(sword.StackPolicy, StackPolicy.UNIQUE, "an item with a stack of one is unique");
            Eq(sword.MaxStack, 1, "and has a stack of one");

            Check(catalog.FindItem(null) == null, "FindItem of null finds nothing");
            Check(catalog.FindItem("item_77") == null, "and so does an unknown key");

            ContainerTypeDefinition bag = catalog.FindContainerType(ManagedInventoryCatalog.BagTypeId);
            Check(bag != null, "the bag type exists");
            Eq(bag.CapacityKind, CapacityKind.SLOT_COUNT, "it counts slots");
            Eq(bag.MaxSlots, 5, "with the slot count");
            Eq(bag.MaxWeight, 0f, "and no weight");
            Check(catalog.FindContainerType("other") == null, "no other type exists");
            Check(catalog.FindContainerType(null) == null, "and null finds none");

            // Two entries with one id: the first is used.
            List<ManagedItemInfo> twice = new List<ManagedItemInfo>
            {
                new ManagedItemInfo(4, "First", 1, 5),
                new ManagedItemInfo(4, "Second", 2, 6),
            };
            ManagedInventoryCatalog dup = new ManagedInventoryCatalog(twice, 2);
            Eq(dup.AllItems.Count, 1, "an id given twice is one item");
            Eq(dup.FindItem("item_4").DisplayKey, "First", "the first entry wins");
            Eq(dup.FindItem("item_4").MaxStack, 5, "with its stack size");

            // Nothing in, nothing listed, and the bag still exists with at least one slot.
            ManagedInventoryCatalog empty = new ManagedInventoryCatalog(null, 0);
            Eq(empty.AllItems.Count, 0, "a null list gives no items");
            Eq(empty.BagSlots, 1, "a slot count below 1 gives 1 slot");
            Check(empty.FindContainerType(ManagedInventoryCatalog.BagTypeId) != null, "and the bag type exists");

            // The content is fixed, so subscribing is allowed and does nothing.
            Action listener = () => { };
            catalog.DefinitionsChanged += listener;
            catalog.DefinitionsChanged -= listener;
            Check(true, "subscribing to DefinitionsChanged and leaving is accepted");
        }

        // ---- the adapter on its own ----

        private static void InventoryBagChecks()
        {
            ManagedInventoryLink link = InvLink(3);
            ManagedInventoryBag bag = link.Bag;
            ManagedInventoryCatalog catalog = link.Catalog;

            Throws<ArgumentNullException>(() => new ManagedInventoryBag(null, catalog, "x"), "a null runtime is refused");
            Throws<ArgumentNullException>(() => new ManagedInventoryBag(link.Runtime, null, "x"), "a null catalog is refused");
            Throws<ArgumentException>(() => new ManagedInventoryBag(link.Runtime, catalog, ""), "an empty container id is refused");
            Throws<ArgumentException>(() => new ManagedInventoryBag(link.Runtime, catalog, null), "and so is a null one");

            Eq(bag.ContainerId, ManagedInventoryLink.ContainerKey, "the bag names its container");
            Eq(bag.SlotCount, 3, "a bag of three slots");
            Eq(bag.UsedSlots, 0, "starts empty");

            int itemId;
            int count;
            Check(!bag.TryGetSlot(0, out itemId, out count), "an empty slot reads as false");
            Check(!bag.TryGetSlot(-1, out itemId, out count), "a slot below the first reads as false");
            Check(!bag.TryGetSlot(3, out itemId, out count), "a slot past the last reads as false");

            Check(bag.TryAdd(InvPotion, 12), "twelve potions fit");
            Eq(bag.CountOf(InvPotion), 12, "and are counted");
            Check(bag.TryGetSlot(0, out itemId, out count) && itemId == InvPotion && count == 10, "the first slot holds a full stack");
            Check(bag.TryGetSlot(1, out itemId, out count) && itemId == InvPotion && count == 2, "the second holds the rest");
            Eq(bag.UsedSlots, 2, "two slots are used");

            // What the runtime holds is what the bag reads: nothing is copied.
            string key = catalog.KeyOf(InvPotion);
            int taken;
            Check(link.Runtime.TryAddItem(ManagedInventoryLink.ContainerKey, key, 3, out taken) && taken == 3, "the runtime adds three more");
            Eq(bag.CountOf(InvPotion), 15, "the bag counts them at once");

            // Nothing is added by halves. Seven more potions need a third slot that does not exist after the sword.
            Check(bag.TryAdd(InvSword, 1), "the sword takes the last free slot");
            Check(!bag.CanAdd(InvPotion, 6), "the room left in the potion stacks is 5");
            Check(bag.CanAdd(InvPotion, 5), "and five fit");
            Check(!bag.TryAdd(InvPotion, 6), "six are refused");
            Eq(bag.CountOf(InvPotion), 15, "and nothing of them was added");

            Check(!bag.TryAdd(99, 1), "an item the catalog does not know is refused");
            Check(!bag.CanAdd(99, 1), "also by CanAdd");
            Check(!bag.TryRemove(99, 1), "and cannot be taken");
            Eq(bag.CountOf(99), 0, "and counts as none");

            // A locked container refuses every change and still reads.
            link.Runtime.SetLocked(ManagedInventoryLink.ContainerKey, true);
            Check(!bag.CanAdd(InvPotion, 1), "a locked container has no room");
            Check(!bag.TryAdd(InvPotion, 1), "refuses an add");
            Check(!bag.TryRemove(InvPotion, 1), "refuses a removal");
            Eq(bag.CountOf(InvPotion), 15, "and still reads its content");
            Check(bag.TryGetSlot(0, out itemId, out count) && count == 10, "and its slots");
            link.Runtime.SetLocked(ManagedInventoryLink.ContainerKey, false);
            Check(bag.TryRemove(InvPotion, 1), "unlocked, a removal works again");
            Eq(bag.CountOf(InvPotion), 14, "and takes one");

            // A destroyed container is empty and refuses everything, without throwing.
            link.Runtime.DestroyContainer(ManagedInventoryLink.ContainerKey);
            Eq(bag.SlotCount, 0, "a destroyed container has no slots");
            Eq(bag.UsedSlots, 0, "uses none");
            Eq(bag.CountOf(InvPotion), 0, "carries nothing");
            Check(!bag.CanAdd(InvPotion, 1), "has no room");
            Check(!bag.TryAdd(InvPotion, 1), "refuses an add");
            Check(!bag.TryRemove(InvPotion, 1), "refuses a removal");
            Check(!bag.TryGetSlot(0, out itemId, out count), "and reads no slot");

            // A saved slot holding an item the catalog no longer knows stays, and the bag cannot see it.
            ManagedInventoryLink saved = InvLink(3);
            InventoryProgressSnapshot snapshot = new InventoryProgressSnapshot();
            ContainerInstanceRow instance = new ContainerInstanceRow();
            instance.InstanceId = ManagedInventoryLink.ContainerKey;
            instance.ContainerTypeId = ManagedInventoryCatalog.BagTypeId;
            instance.OwnerId = ManagedInventoryLink.OwnerId;
            snapshot.Instances.Add(instance);
            snapshot.Slots.Add(SaveRow(0, "item_77", 4));
            snapshot.Slots.Add(SaveRow(1, "item_2", 2));
            saved.Restore(snapshot);
            Check(!saved.Bag.TryGetSlot(0, out itemId, out count), "a slot of an unknown item reads as false");
            Check(saved.Bag.TryGetSlot(1, out itemId, out count) && itemId == InvPotion && count == 2, "a known one reads");
            Eq(saved.Bag.UsedSlots, 2, "the unknown slot still counts as used");
            Check(saved.Bag.CanAdd(InvPotion, 18), "the room is the stack's 8 and the free slot's 10, the unknown slot has none");
            Check(!saved.Bag.CanAdd(InvPotion, 19), "and one more is refused");
            Check(saved.Bag.TryAdd(InvPotion, 18), "all 18 are added");
            Eq(saved.Bag.CountOf(InvPotion), 20, "to the two that were there");
            Check(saved.Bag.TryGetSlot(2, out itemId, out count) && count == 10, "and the unknown slot was left alone");

            // The room is counted from this bag's catalog. A catalog that disagrees with the runtime's is a setup
            // error, and a partial add is taken out again so that the call is still all or none.
            ManagedItemCatalog small = new ManagedItemCatalog();
            small.Add(new ManagedItemInfo(InvPotion, "Health Potion", 25, 10));
            ManagedItemCatalog large = new ManagedItemCatalog();
            large.Add(new ManagedItemInfo(InvPotion, "Health Potion", 25, 99));
            ManagedInventoryCatalog runtimeCatalog = new ManagedInventoryCatalog(small.Items, 2);
            ManagedInventoryCatalog bagCatalog = new ManagedInventoryCatalog(large.Items, 2);
            InventorizzRuntime runtime = new InventorizzRuntime(runtimeCatalog);
            string containerId = runtime.CreateContainer("p", ManagedInventoryCatalog.BagTypeId);
            ManagedInventoryBag mismatched = new ManagedInventoryBag(runtime, bagCatalog, containerId);
            Check(mismatched.CanAdd(InvPotion, 30), "the bag believes 30 fit, from its own catalog");
            Check(!mismatched.TryAdd(InvPotion, 30), "the runtime takes 20, so the add is undone and refused");
            Eq(mismatched.CountOf(InvPotion), 0, "nothing stays in the container");
            Eq(mismatched.UsedSlots, 0, "and no slot");
        }

        private static ContainerSlotSaveRow SaveRow(int slot, string itemKey, int count)
        {
            ContainerSlotSaveRow row = new ContainerSlotSaveRow();
            row.InstanceId = ManagedInventoryLink.ContainerKey;
            row.SlotIndex = slot;
            row.ItemId = itemKey;
            row.Count = count;
            return row;
        }

        // ---- the same answers as the plain bag ----

        // Runs one call on both bags, and compares the answer with the one worked out by hand and then the whole
        // state: counts, used slots and every slot, so a difference in where a stack sits shows too.
        private static void InvStep(string what, ManagedItemBag plain, ManagedInventoryBag inv, Func<IManagedItemHolder, bool> call, bool expected)
        {
            bool fromPlain = call(plain);
            bool fromInv = call(inv);
            Eq(fromPlain, expected, what + ": the plain bag answers as worked out");
            Eq(fromInv, fromPlain, what + ": the Inventorizz bag answers as the plain bag");
            InvSame(what, plain, inv);
        }

        private static void InvSame(string what, ManagedItemBag plain, ManagedInventoryBag inv)
        {
            Eq(inv.SlotCount, plain.SlotCount, what + ": slot count");
            Eq(inv.UsedSlots, plain.UsedSlots, what + ": used slots");
            for (int id = InvSword; id <= InvScale; id++)
            {
                Eq(inv.CountOf(id), plain.CountOf(id), what + ": count of item " + id);
            }

            for (int slot = -1; slot <= plain.SlotCount; slot++)
            {
                int plainItem;
                int plainCount;
                int invItem;
                int invCount;
                bool plainFilled = plain.TryGetSlot(slot, out plainItem, out plainCount);
                bool invFilled = inv.TryGetSlot(slot, out invItem, out invCount);
                Eq(invFilled, plainFilled, what + ": slot " + slot + " filled");
                if (plainFilled)
                {
                    Eq(invItem, plainItem, what + ": item in slot " + slot);
                    Eq(invCount, plainCount, what + ": count in slot " + slot);
                }
            }
        }

        private static void InventoryParityChecks()
        {
            ManagedItemBag plain = new ManagedItemBag(InvItems(), 3);
            ManagedInventoryLink link = InvLink(3);
            ManagedInventoryBag inv = link.Bag;
            InvSame("start", plain, inv);

            // The layout after each step is worked out by hand: P10 is a potion stack of 10, S a sword, C a scale.
            InvStep("add 25 potions", plain, inv, h => h.TryAdd(InvPotion, 25), true);                // P10 P10 P5
            InvStep("add 6 potions", plain, inv, h => h.TryAdd(InvPotion, 6), false);                 // room is 5
            InvStep("add 5 potions", plain, inv, h => h.TryAdd(InvPotion, 5), true);                  // P10 P10 P10
            InvStep("add a sword to a full bag", plain, inv, h => h.TryAdd(InvSword, 1), false);
            InvStep("remove 12 potions", plain, inv, h => h.TryRemove(InvPotion, 12), true);          // P10 P8 -
            InvStep("add a sword", plain, inv, h => h.TryAdd(InvSword, 1), true);                     // P10 P8 S
            InvStep("remove 3 potions", plain, inv, h => h.TryRemove(InvPotion, 3), true);            // P10 P5 S
            InvStep("can add 5 potions", plain, inv, h => h.CanAdd(InvPotion, 5), true);
            InvStep("can add 6 potions", plain, inv, h => h.CanAdd(InvPotion, 6), false);
            InvStep("add 5 potions to the partial stack", plain, inv, h => h.TryAdd(InvPotion, 5), true); // P10 P10 S
            InvStep("remove 2 swords", plain, inv, h => h.TryRemove(InvSword, 2), false);
            InvStep("remove the sword", plain, inv, h => h.TryRemove(InvSword, 1), true);             // P10 P10 -
            InvStep("add 2 scales", plain, inv, h => h.TryAdd(InvScale, 2), false);                   // one slot, a stack of one
            InvStep("add a scale", plain, inv, h => h.TryAdd(InvScale, 1), true);                     // P10 P10 C
            InvStep("add an unknown item", plain, inv, h => h.TryAdd(99, 1), false);
            InvStep("add none", plain, inv, h => h.TryAdd(InvPotion, 0), false);
            InvStep("add a negative count", plain, inv, h => h.TryAdd(InvPotion, -1), false);
            InvStep("remove none", plain, inv, h => h.TryRemove(InvPotion, 0), false);
            InvStep("remove a negative count", plain, inv, h => h.TryRemove(InvPotion, -1), false);
            InvStep("remove an unknown item", plain, inv, h => h.TryRemove(99, 1), false);
            InvStep("remove 20 potions", plain, inv, h => h.TryRemove(InvPotion, 20), true);          // - - C
            InvStep("remove 1 more potion", plain, inv, h => h.TryRemove(InvPotion, 1), false);
            InvStep("can add 20 potions", plain, inv, h => h.CanAdd(InvPotion, 20), true);
            InvStep("can add 21 potions", plain, inv, h => h.CanAdd(InvPotion, 21), false);
        }

        // ---- the link ----

        private static void InventoryLinkChecks()
        {
            ManagedInventoryLink link = InvLink(4);
            Eq(link.Bag.ContainerId, ManagedInventoryLink.ContainerKey, "the bag shows the container with the fixed id");
            Check(link.Runtime.ContainerExists(ManagedInventoryLink.ContainerKey), "it exists in the runtime");
            Eq(link.Runtime.GetInstance(ManagedInventoryLink.ContainerKey).OwnerId, ManagedInventoryLink.OwnerId, "the player owns it");
            Eq(link.Bag.SlotCount, 4, "it has the slots that were asked for");

            Check(link.Bag.TryAdd(InvPotion, 12), "twelve potions go in");
            Check(link.Bag.TryAdd(InvSword, 1), "and a sword");
            InventoryProgressSnapshot snapshot = link.Capture();
            Eq(snapshot.Instances.Count, 1, "the snapshot has the one container");
            Eq(snapshot.Slots.Count, 3, "and its three filled slots");

            ManagedInventoryLink other = InvLink(4);
            other.Restore(snapshot);
            Eq(other.Bag.CountOf(InvPotion), 12, "a restored link carries the potions");
            Eq(other.Bag.CountOf(InvSword), 1, "and the sword");
            Eq(other.Bag.UsedSlots, 3, "in the same slots");
            Eq(link.Bag.CountOf(InvPotion), 12, "restoring another link left the first one alone");

            // A saved slot beyond a smaller container is dropped, the rest is kept.
            ManagedInventoryLink smaller = InvLink(2);
            smaller.Restore(snapshot);
            Eq(smaller.Bag.CountOf(InvPotion), 12, "a smaller container keeps the potions that still have a slot");
            Eq(smaller.Bag.CountOf(InvSword), 0, "and drops the sword whose slot is gone");

            // An empty or missing snapshot leaves a working empty container.
            other.Restore(new InventoryProgressSnapshot());
            Check(other.Runtime.ContainerExists(ManagedInventoryLink.ContainerKey), "an empty snapshot leaves the container in place");
            Eq(other.Bag.UsedSlots, 0, "empty");
            Check(other.Bag.TryAdd(InvPotion, 1), "and usable");
            other.Restore(null);
            Check(other.Runtime.ContainerExists(ManagedInventoryLink.ContainerKey), "a null snapshot is the same");
            Eq(other.Bag.CountOf(InvPotion), 0, "and clears the bag");
        }

        // ---- the scene host ----

        private static ManagedPlayerItemBag InvPlayer(IManagedItemCatalog catalog)
        {
            ManagedPlayerItemBag player = new ManagedPlayerItemBag();
            player.UseCatalog(catalog);
            return player;
        }

        private static void InventoryHostChecks()
        {
            UnityEngine.Debug.Reset();
            ManagedPlayerItemBag player = InvPlayer(InvItems());
            ManagedQuickSlots quick = player.QuickSlots;
            Check(player.Bag is ManagedItemBag, "before the host the bag is the plain one");

            ManagedInventoryHost host = new ManagedInventoryHost();
            SetField(host, "_player", player);
            Check(!host.IsLinked, "a host that has not started is not linked");
            Call(host, "Start");
            Check(host.IsLinked, "after Start the host is linked");
            Check(player.Bag is ManagedInventoryBag, "the player's bag is the Inventorizz one");
            Check(ReferenceEquals(player.Bag, host.Link.Bag), "and it is the bag of the link");
            Eq(player.Bag.SlotCount, ManagedItemBag.DefaultSlotCount, "with the slot count of the component");
            Check(!HasInventoryWarning(), "a linked host warns of nothing");

            Check(player.Bag.TryAdd(InvPotion, 5), "the player carries five potions through the new bag");
            Eq(host.Link.Bag.CountOf(InvPotion), 5, "the link reads them");
            Check(ReferenceEquals(player.QuickSlots, quick), "the quick row is the same object");
            int used = 0;
            quick.Used += (slot, item) => { used++; };
            quick.Assign(0, InvPotion);
            Check(quick.TryUse(0, player.Bag), "a quick slot is used against the Inventorizz bag");
            Eq(used, 1, "and raises the event");

            // Another catalog replaces the bag, and the host builds a new link over it.
            ManagedInventoryLink before = host.Link;
            ManagedItemCatalog second = new ManagedItemCatalog();
            second.Add(new ManagedItemInfo(7, "Rope", 3, 4));
            player.UseCatalog(second);
            Check(host.IsLinked && !ReferenceEquals(host.Link, before), "a new catalog gives a new link");
            Check(player.Bag is ManagedInventoryBag && ReferenceEquals(player.Bag, host.Link.Bag), "installed again");
            Eq(player.Bag.CountOf(InvPotion), 0, "the old content is gone");
            Check(player.Bag.TryAdd(7, 4), "the new item can be carried");
            Check(!player.Bag.TryAdd(InvPotion, 1), "an item of the old catalog is unknown now");
            Eq(host.Link.Catalog.KeyOf(7), "item_7", "the new content is the new items");

            // A catalog that cannot list its items leaves the plain bag, with a warning.
            player.UseCatalog(new OpaqueCatalog());
            Check(!host.IsLinked, "an opaque catalog cannot be linked");
            Check(player.Bag is ManagedItemBag, "so the plain bag stays");
            Check(HasInventoryWarning(), "and the host says why");

            // A catalog that can list its items links again.
            player.UseCatalog(InvItems());
            Check(host.IsLinked && player.Bag is ManagedInventoryBag, "a listing catalog links again");

            // A destroyed host stops listening.
            Call(host, "OnDestroy");
            Check(!host.IsLinked, "a destroyed host is not linked");
            player.UseCatalog(InvItems());
            Check(player.Bag is ManagedItemBag, "and no longer replaces the bag");

            // No bag component: a warning and nothing else.
            UnityEngine.Debug.Reset();
            ManagedInventoryHost lonely = new ManagedInventoryHost();
            Call(lonely, "Start");
            Check(!lonely.IsLinked, "a host with no bag component is not linked");
            Check(HasInventoryWarning(), "and says so");

            // The component on the same object is found without being assigned.
            UnityEngine.Debug.Reset();
            ManagedPlayerItemBag sibling = InvPlayer(InvItems());
            ManagedInventoryHost neighbour = new ManagedInventoryHost();
            neighbour.Siblings.Add(sibling);
            Call(neighbour, "Start");
            Check(neighbour.IsLinked && sibling.Bag is ManagedInventoryBag, "the component on the same object is used");
            Check(!HasInventoryWarning(), "without a warning");
        }
    }
}
