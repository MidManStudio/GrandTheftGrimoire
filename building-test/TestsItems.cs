// Checks for the item seams: the slot reader on the plain bag, the item lists of the two catalogs, and the player's
// bag component taking another bag. Verification harness only. The Inventorizz bridge that uses these seams is
// checked in packages-test/.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Data;
using MidManStudio.Gtg.Managed.Hud;
using MidManStudio.Gtg.Managed.Items;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    /// <summary>A bag that is not the plain one. Its content is set by hand.</summary>
    public sealed class FakeSlots : IManagedItemSlots
    {
        public int Potions;

        public int SlotCount { get { return 2; } }

        public int UsedSlots { get { return Potions > 0 ? 1 : 0; } }

        public bool TryGetSlot(int slot, out int itemId, out int count)
        {
            itemId = 0;
            count = 0;
            if (slot != 0 || Potions <= 0)
            {
                return false;
            }

            itemId = 7;
            count = Potions;
            return true;
        }

        public int CountOf(int itemId) { return itemId == 7 ? Potions : 0; }

        public bool CanAdd(int itemId, int count) { return false; }

        public bool TryAdd(int itemId, int count) { return false; }

        public bool TryRemove(int itemId, int count) { return false; }
    }

    public static partial class Tests
    {
        private static ManagedItemCatalog ItemFixture()
        {
            ManagedItemCatalog catalog = new ManagedItemCatalog();
            catalog.Add(new ManagedItemInfo(7, "potion", 5L, 10));
            catalog.Add(new ManagedItemInfo(1, "sword", 50L, 1));
            return catalog;
        }

        private static void ItemTests()
        {
            Section("items: slots of the plain bag");
            ManagedItemBag plain = new ManagedItemBag(ItemFixture(), 3);
            IManagedItemSlots slots = plain;
            int itemId;
            int count;
            Eq(slots.SlotCount, 3, "the slot count is the one given");
            Eq(slots.UsedSlots, 0, "an empty bag uses no slot");
            Check(!slots.TryGetSlot(0, out itemId, out count), "an empty slot reads as false");
            Check(slots.TryAdd(7, 12), "twelve potions fit");
            Check(slots.TryGetSlot(0, out itemId, out count) && itemId == 7 && count == 10, "the first slot holds a full stack");
            Check(slots.TryGetSlot(1, out itemId, out count) && itemId == 7 && count == 2, "the second holds the rest");
            Check(!slots.TryGetSlot(2, out itemId, out count), "the third is empty");
            Check(!slots.TryGetSlot(-1, out itemId, out count), "a slot below the first reads as false");
            Check(!slots.TryGetSlot(3, out itemId, out count), "a slot past the last reads as false");
            Eq(slots.UsedSlots, 2, "two slots are used");
            Check(slots.TryRemove(7, 3), "three potions can be taken");
            Check(!slots.TryGetSlot(1, out itemId, out count), "the last stack emptied first");
            Check(slots.TryGetSlot(0, out itemId, out count) && count == 9, "and the first gave one");
            Eq(slots.UsedSlots, 1, "so one slot is used");

            Section("items: catalogs list their items");
            IManagedItemList list = ItemFixture();
            Eq(list.Items.Count, 2, "the code catalog lists both items");
            Eq(list.Items[0].Id, 1, "in id order, whatever the order they were added in");
            Eq(list.Items[1].Id, 7, "to the last one");
            Eq(list.Items[1].MaxStack, 10, "with their data");
            Check(!ReferenceEquals(list.Items, list.Items), "each read is a new list, so a caller cannot change the catalog");

            ItemDatabase database = ScriptableObject.CreateInstance<ItemDatabase>();
            database.MaxStackSize = 10;
            database.Items = new List<ItemDefinition>
            {
                new ItemDefinition { Id = 3, Name = "ale", Value = 4, Stackable = true },
                null,
                new ItemDefinition { Id = 1, Name = "sword", Value = 50, Stackable = false },
                new ItemDefinition { Id = 3, Name = "second ale", Value = 9, Stackable = true },
            };
            ManagedItemDatabaseCatalog fromTable = new ManagedItemDatabaseCatalog(database);
            IManagedItemList tableList = fromTable;
            Eq(tableList.Items.Count, 2, "the table catalog skips the null row and the repeated id");
            Eq(tableList.Items[0].Id, 1, "in id order");
            Eq(tableList.Items[0].MaxStack, 1, "an item that does not stack has a stack of one");
            Eq(tableList.Items[1].Name, "ale", "the first row of an id wins");
            Eq(tableList.Items[1].MaxStack, 10, "a stackable item has the table's stack size");
            Eq(((IManagedItemList)new ManagedItemDatabaseCatalog(null)).Items.Count, 0, "a null table lists nothing");

            Section("items: the player bag takes another bag");
            Reset();
            ManagedPlayerItemBag player = new GameObject("player bag").AddComponent<ManagedPlayerItemBag>();
            player.UseCatalog(ItemFixture());
            Check(player.Bag is ManagedItemBag, "the bag is the plain one at first");

            int replaced = 0;
            player.CatalogReplaced += () => { replaced++; };
            player.UseCatalog(ItemFixture());
            Eq(replaced, 1, "a new catalog raises CatalogReplaced once");
            Check(player.Bag is ManagedItemBag, "and puts a plain bag back");
            ManagedQuickSlots quick = player.QuickSlots;

            FakeSlots fake = new FakeSlots();
            player.UseBag(fake);
            Check(ReferenceEquals(player.Bag, fake), "UseBag installs the bag");
            Check(ReferenceEquals(player.QuickSlots, quick), "and leaves the quick row alone");
            Eq(replaced, 1, "without raising CatalogReplaced");

            bool refused = false;
            try { player.UseBag(null); }
            catch (ArgumentNullException) { refused = true; }
            Check(refused, "a null bag is refused");
            Check(ReferenceEquals(player.Bag, fake), "and the installed bag stays");

            player.UseCatalog(ItemFixture());
            Check(player.Bag is ManagedItemBag, "a later catalog puts the plain bag back");
            Eq(replaced, 2, "and raises CatalogReplaced again");

            Section("items: the HUD reads the installed bag");
            World w = NewWorld(true);
            ManagedPlayerItemBag hudBag;
            ManagedHud hud = NewHud(w, out hudBag);
            Check(hud.UseQuickSlot(0), "the quick slot is used against the plain bag, which carries three potions");

            FakeSlots other = new FakeSlots();
            hudBag.UseBag(other);
            Check(ReferenceEquals(hud.Bag, other), "the HUD shows the bag that was installed");
            Check(ReferenceEquals(hud.QuickSlots, hudBag.QuickSlots), "and the same quick row");
            Check(!hud.UseQuickSlot(0), "a bag with no potions refuses the quick slot");
            other.Potions = 4;
            Check(hud.UseQuickSlot(0), "and a bag with potions accepts it");
            HudFrame(hud, 2f);
            Check(ReferenceEquals(hud.Bag, other), "a frame drawn over the installed bag keeps it");
        }
    }
}
