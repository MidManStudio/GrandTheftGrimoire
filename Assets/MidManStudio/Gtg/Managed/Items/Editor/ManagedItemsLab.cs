// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedItemsLab.cs"
// ============================================================================

using System.Text;
using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// Editor test tools for the item bag and the quick slots. Adds a bag to the selected object, and in
    /// Play mode stocks the bag with the sample items and prints what the bag and the quick row hold.
    /// Menu: GTG, Items.
    /// </summary>
    public static class ManagedItemsLab
    {
        [MenuItem("GTG/Items/Add Player Bag To Selected", false, 700)]
        private static void AddBagMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                GtgLabKit.Warn("Select the player object first.");
                return;
            }

            GtgLabKit.Ensure<ManagedPlayerItemBag>(selected);
            GtgLabKit.Finish(selected, "Item bag added. The HUD would add one by itself at Play, so this matters only when no HUD is in the scene.");
        }

        [MenuItem("GTG/Items/Stock Sample Items (Play)", false, 710)]
        private static void StockMenu()
        {
            ManagedPlayerItemBag bag = PlayBag("Stock Sample Items");
            if (bag == null)
            {
                return;
            }

            ManagedItemSamples.StockPlayerBag(bag);
            GtgLabKit.Log("Bag stocked: ten potions, a sword and a dragon scale, with the potion and the sword on the first two quick slots.\n" + Describe(bag));
        }

        [MenuItem("GTG/Items/Print Bag And Quick Slots (Play)", false, 711)]
        private static void PrintMenu()
        {
            ManagedPlayerItemBag bag = PlayBag("Print Bag");
            if (bag != null)
            {
                GtgLabKit.Log(Describe(bag));
            }
        }

        private static ManagedPlayerItemBag PlayBag(string action)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return null;
            }

            ManagedPlayerItemBag bag = Object.FindFirstObjectByType<ManagedPlayerItemBag>();
            if (bag == null)
            {
                GtgLabKit.Warn("No ManagedPlayerItemBag in the scene. Add a ManagedHud, or use Add Player Bag To Selected before Play.");
            }

            return bag;
        }

        private static string Describe(ManagedPlayerItemBag bag)
        {
            StringBuilder text = new StringBuilder();
            IManagedItemSlots slots = bag.Bag;
            text.Append("Bag (").Append(slots.UsedSlots).Append(" of ").Append(slots.SlotCount).Append(" slots used):");
            for (int i = 0; i < slots.SlotCount; i++)
            {
                int itemId;
                int count;
                if (slots.TryGetSlot(i, out itemId, out count))
                {
                    text.Append(" [").Append(i).Append("] item ").Append(itemId).Append(" x").Append(count);
                }
            }

            ManagedQuickSlots quick = bag.QuickSlots;
            text.Append("\nQuick slots:");
            for (int i = 0; i < quick.SlotCount; i++)
            {
                int itemId;
                text.Append(" [").Append(i).Append("] ").Append(quick.TryGetItem(i, out itemId) ? "item " + itemId : "empty");
            }

            return text.ToString();
        }
    }
}
