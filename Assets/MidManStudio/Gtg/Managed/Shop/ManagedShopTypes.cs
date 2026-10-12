// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShopTypes.cs"
// ============================================================================

using System;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// One thing a shop sells. The numbers are plain whole values, so a data table can fill
    /// them. A missing value reads as 0, which is the safe side for both: no price override
    /// means the item's own value, and no stock means sold out.
    /// </summary>
    [Serializable]
    public sealed class ManagedShopLine
    {
        /// <summary>The value of <see cref="Stock"/> for a shop that never runs out.</summary>
        public const int Unlimited = -1;

        public int ItemId;

        /// <summary>The price of one item in silver. 0 or less means the item's own value.</summary>
        public int PriceSilver;

        /// <summary>How many the shop starts with. <see cref="Unlimited"/> never runs out.</summary>
        public int Stock;

        public ManagedShopLine()
        {
        }

        public ManagedShopLine(int itemId, int priceSilver, int stock)
        {
            ItemId = itemId;
            PriceSilver = priceSilver;
            Stock = stock;
        }
    }

    /// <summary>One thing a shop buys from the player.</summary>
    [Serializable]
    public sealed class ManagedShopBuyLine
    {
        public int ItemId;

        /// <summary>
        /// What the shop pays for one item in silver. 0 or less means the item's value times the
        /// shop's buy percent.
        /// </summary>
        public int PriceSilver;

        public ManagedShopBuyLine()
        {
        }

        public ManagedShopBuyLine(int itemId, int priceSilver)
        {
            ItemId = itemId;
            PriceSilver = priceSilver;
        }
    }

    /// <summary>The outcome of a quote or a trade. <see cref="Ok"/> is the only success.</summary>
    public enum ManagedShopResult
    {
        /// <summary>The trade happened, or the quote is valid.</summary>
        Ok = 0,

        /// <summary>The shop needs a flag that is not set.</summary>
        Closed,

        /// <summary>The count is below 1, or the total price is too large to count.</summary>
        BadCount,

        /// <summary>The shop does not sell this item.</summary>
        NotSold,

        /// <summary>The shop does not buy this item.</summary>
        NotBought,

        /// <summary>The item catalog does not know this item.</summary>
        UnknownItem,

        /// <summary>There is no price: no override and the item has no value to work from.</summary>
        NoPrice,

        /// <summary>The shop has fewer than the count.</summary>
        OutOfStock,

        /// <summary>The wallet holds less than the total price.</summary>
        CannotAfford,

        /// <summary>The items do not fit in the bag.</summary>
        BagFull,

        /// <summary>The bag holds fewer than the count to sell.</summary>
        NotEnoughItems,

        /// <summary>The trade was given no wallet.</summary>
        MissingWallet,

        /// <summary>The trade was given no bag.</summary>
        MissingBag,
    }

    public enum ManagedTradeKind
    {
        /// <summary>The player bought from the shop.</summary>
        Bought = 0,

        /// <summary>The player sold to the shop.</summary>
        Sold = 1,
    }

    /// <summary>What <see cref="ManagedShop.Traded"/> reports after a trade went through.</summary>
    public readonly struct ManagedShopTrade
    {
        public readonly string ShopId;
        public readonly ManagedTradeKind Kind;
        public readonly int ItemId;
        public readonly int Count;

        /// <summary>The whole price in silver, paid by the player or paid to the player.</summary>
        public readonly long TotalSilver;

        public ManagedShopTrade(string shopId, ManagedTradeKind kind, int itemId, int count, long totalSilver)
        {
            ShopId = shopId;
            Kind = kind;
            ItemId = itemId;
            Count = count;
            TotalSilver = totalSilver;
        }
    }

    /// <summary>One row of a shop's window: an item, its name, the price of one and what is left.</summary>
    public readonly struct ManagedShopOffer
    {
        public readonly int ItemId;
        public readonly string Name;

        /// <summary>The price of one item in silver. 0 when the shop has no price for it.</summary>
        public readonly long UnitPriceSilver;

        /// <summary>What the shop has left. <see cref="ManagedShopLine.Unlimited"/> for no limit, and for a buy row.</summary>
        public readonly int Stock;

        public ManagedShopOffer(int itemId, string name, long unitPriceSilver, int stock)
        {
            ItemId = itemId;
            Name = name;
            UnitPriceSilver = unitPriceSilver;
            Stock = stock;
        }
    }
}
