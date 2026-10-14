// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedItemSamples.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace MidManStudio.Gtg.Managed.Items
{
    /// <summary>
    /// Three made-up items for trying the shop and the HUD before the real item table is wired in.
    /// Editor and development builds only. Nothing in a shipped build refers to it.
    /// </summary>
    public static class ManagedItemSamples
    {
        public const int IronSword = 1;
        public const int HealthPotion = 2;
        public const int DragonScale = 3;

        /// <summary>The sample catalog: a sword, a stacking potion and a costly scale.</summary>
        public static ManagedItemCatalog Catalog()
        {
            var catalog = new ManagedItemCatalog();
            catalog.Add(new ManagedItemInfo(IronSword, "Iron Sword", 50, 1));
            catalog.Add(new ManagedItemInfo(HealthPotion, "Health Potion", 25, 99));
            catalog.Add(new ManagedItemInfo(DragonScale, "Dragon Scale", 5000, 1));
            return catalog;
        }

        /// <summary>
        /// Gives the player the sample catalog and some stock: ten potions, a sword and a scale in
        /// the bag, with the potion and the sword in the first two quick slots. Whatever the bag
        /// held before is gone.
        /// </summary>
        public static void StockPlayerBag(ManagedPlayerItemBag player)
        {
            if (player == null)
            {
                return;
            }

            player.UseCatalog(Catalog());
            player.Bag.TryAdd(HealthPotion, 10);
            player.Bag.TryAdd(IronSword, 1);
            player.Bag.TryAdd(DragonScale, 1);
            player.QuickSlots.Assign(0, HealthPotion);
            player.QuickSlots.Assign(1, IronSword);
        }
    }
}

#endif
