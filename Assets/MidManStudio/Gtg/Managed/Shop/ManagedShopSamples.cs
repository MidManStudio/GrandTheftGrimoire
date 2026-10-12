// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShopSamples.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Items;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// Two made-up shops and three items for the shop test panel, so buying can be tried before the
    /// real shops exist. None of it is game content. It is left out of release builds, and the real
    /// shops and items never read it.
    /// </summary>
    public static class ManagedShopSamples
    {
        /// <summary>The flag that opens the sample hidden shop.</summary>
        public const string HiddenShopFlag = "sample_hidden_shop_open";

        /// <summary>
        /// The three rows of <c>Assets/inventory_items.mdix</c> as they are today: Iron Sword 50,
        /// Health Potion 25 stacking to 99, Dragon Scale 5000.
        /// </summary>
        public static ManagedItemCatalog Catalog()
        {
            var catalog = new ManagedItemCatalog();
            catalog.Add(new ManagedItemInfo(1, "Iron Sword", 50, 1));
            catalog.Add(new ManagedItemInfo(2, "Health Potion", 25, 99));
            catalog.Add(new ManagedItemInfo(3, "Dragon Scale", 5000, 1));
            return catalog;
        }

        public static List<ManagedShopDefinition> Shops()
        {
            var general = new ManagedShopDefinition
            {
                Id = "sample_general",
                DisplayName = "Sample general store",
                ShopkeeperName = string.Empty,
                RequiredFlag = string.Empty,
            };
            general.Sells.Add(new ManagedShopLine(2, 0, ManagedShopLine.Unlimited));
            general.Sells.Add(new ManagedShopLine(1, 60, 2));
            general.Buys.Add(new ManagedShopBuyLine(2, 0));
            general.Buys.Add(new ManagedShopBuyLine(3, 0));

            var hidden = new ManagedShopDefinition
            {
                Id = "sample_hidden",
                DisplayName = "Sample hidden shop",
                ShopkeeperName = string.Empty,
                RequiredFlag = HiddenShopFlag,
            };
            hidden.Sells.Add(new ManagedShopLine(3, 0, 1));
            hidden.Buys.Add(new ManagedShopBuyLine(1, 40));

            return new List<ManagedShopDefinition> { general, hidden };
        }
    }
}

#endif
