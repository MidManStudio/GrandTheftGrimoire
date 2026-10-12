// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShopDevSession.cs"
// ============================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Text;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Flags;
using MidManStudio.Gtg.Managed.Items;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// Everything the shop test panel does that is not drawing: it holds the shops, a bag and a flag
    /// set, runs a buy or a sell and words the result. It has no Unity types, so it is checked
    /// with plain tests, and the panel only calls it. The wallet is passed in, because the
    /// player's wallet belongs to the scene.
    /// </summary>
    public sealed class ManagedShopDevSession
    {
        private readonly List<ManagedShop> _shops = new List<ManagedShop>();
        private readonly StringBuilder _text = new StringBuilder(96);

        public ManagedShopDevSession(IManagedItemCatalog catalog, IEnumerable<ManagedShopDefinition> definitions)
        {
            Catalog = catalog;
            Bag = new ManagedItemBag(catalog);
            Flags = new ManagedWorldFlags();
            LastMessage = string.Empty;

            if (definitions != null)
            {
                foreach (ManagedShopDefinition definition in definitions)
                {
                    if (definition != null)
                    {
                        _shops.Add(new ManagedShop(definition, catalog, Flags));
                    }
                }
            }
        }

        /// <summary>A session with the sample items and the two sample shops.</summary>
        public static ManagedShopDevSession CreateSample()
        {
            return new ManagedShopDevSession(ManagedShopSamples.Catalog(), ManagedShopSamples.Shops());
        }

        public IManagedItemCatalog Catalog { get; private set; }

        public ManagedItemBag Bag { get; private set; }

        public ManagedWorldFlags Flags { get; private set; }

        /// <summary>The words for the last action. Empty before the first one.</summary>
        public string LastMessage { get; private set; }

        public int ShopCount { get { return _shops.Count; } }

        /// <summary>The shop at an index, or null outside the list.</summary>
        public ManagedShop ShopAt(int index)
        {
            return index >= 0 && index < _shops.Count ? _shops[index] : null;
        }

        public void Buy(int shopIndex, IManagedCoinPurse wallet, int itemId, int count)
        {
            ManagedShop shop = ShopAt(shopIndex);
            if (shop == null)
            {
                LastMessage = "There is no such shop.";
                return;
            }

            // The price is read first, because a trade can empty the stock the quote depends on.
            long total;
            shop.QuoteBuy(itemId, count, out total);
            ManagedShopResult result = shop.TryBuy(wallet, Bag, itemId, count);
            if (result == ManagedShopResult.Ok)
            {
                LastMessage = "Bought " + count + " " + NameOf(itemId) + " for " + Money(total) + ".";
                return;
            }

            LastMessage = Explain(shop, result, itemId, count, true);
        }

        public void Sell(int shopIndex, IManagedCoinPurse wallet, int itemId, int count)
        {
            ManagedShop shop = ShopAt(shopIndex);
            if (shop == null)
            {
                LastMessage = "There is no such shop.";
                return;
            }

            long total;
            shop.QuoteSell(itemId, count, out total);
            ManagedShopResult result = shop.TrySell(wallet, Bag, itemId, count);
            if (result == ManagedShopResult.Ok)
            {
                LastMessage = "Sold " + count + " " + NameOf(itemId) + " for " + Money(total) + ".";
                return;
            }

            LastMessage = Explain(shop, result, itemId, count, false);
        }

        /// <summary>Sells everything of one item that the bag carries.</summary>
        public void SellAll(int shopIndex, IManagedCoinPurse wallet, int itemId)
        {
            int carried = Bag.CountOf(itemId);
            if (carried < 1)
            {
                LastMessage = "You carry no " + NameOf(itemId) + ".";
                return;
            }

            Sell(shopIndex, wallet, itemId, carried);
        }

        /// <summary>Sets the flag when it is clear and clears it when it is set.</summary>
        public void ToggleFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }

            bool nowSet = !Flags.IsSet(flag);
            if (nowSet)
            {
                Flags.Set(flag);
            }
            else
            {
                Flags.Clear(flag);
            }

            LastMessage = "Flag " + flag + (nowSet ? " is set." : " is clear.");
        }

        /// <summary>The bag as one line, such as "Iron Sword x1, Health Potion x4", or "empty".</summary>
        public string DescribeBag()
        {
            _text.Length = 0;
            for (int i = 0; i < Bag.SlotCount; i++)
            {
                int itemId;
                int count;
                if (!Bag.TryGetSlot(i, out itemId, out count))
                {
                    continue;
                }

                if (_text.Length > 0)
                {
                    _text.Append(", ");
                }

                _text.Append(NameOf(itemId)).Append(" x").Append(count);
            }

            return _text.Length == 0 ? "empty" : _text.ToString();
        }

        /// <summary>Silver written as gold and silver, such as "2 gold 5 silver". Zero parts are left out.</summary>
        public static string Money(long silver)
        {
            if (silver < 0)
            {
                silver = 0;
            }

            long gold = silver / ManagedWallet.SilverPerGold;
            long rest = silver % ManagedWallet.SilverPerGold;
            if (gold == 0)
            {
                return rest + " silver";
            }

            return rest == 0 ? gold + " gold" : gold + " gold " + rest + " silver";
        }

        private string NameOf(int itemId)
        {
            ManagedItemInfo info;
            return Catalog != null && Catalog.TryGet(itemId, out info) ? info.Name : "item " + itemId;
        }

        private string Explain(ManagedShop shop, ManagedShopResult result, int itemId, int count, bool buying)
        {
            switch (result)
            {
                case ManagedShopResult.Closed:
                    return shop.DisplayName + " is closed. It needs the flag " + shop.RequiredFlag + ".";
                case ManagedShopResult.BadCount:
                    return "That count is not valid.";
                case ManagedShopResult.NotSold:
                    return shop.DisplayName + " does not sell " + NameOf(itemId) + ".";
                case ManagedShopResult.NotBought:
                    return shop.DisplayName + " does not buy " + NameOf(itemId) + ".";
                case ManagedShopResult.UnknownItem:
                    return "The item list does not know item " + itemId + ".";
                case ManagedShopResult.NoPrice:
                    return NameOf(itemId) + " has no price here.";
                case ManagedShopResult.OutOfStock:
                    return shop.DisplayName + " does not have " + count + " " + NameOf(itemId) + ".";
                case ManagedShopResult.CannotAfford:
                    long price;
                    shop.QuoteBuy(itemId, count, out price);
                    return "You cannot afford that. " + count + " " + NameOf(itemId) + " cost " + Money(price) + ".";
                case ManagedShopResult.BagFull:
                    return "The bag has no room for " + count + " " + NameOf(itemId) + ".";
                case ManagedShopResult.NotEnoughItems:
                    return "You do not carry " + count + " " + NameOf(itemId) + ".";
                case ManagedShopResult.MissingWallet:
                    return "There is no player wallet in the scene yet. The spell caster adds one when play starts.";
                case ManagedShopResult.MissingBag:
                    return "There is no bag.";
                default:
                    return (buying ? "The purchase" : "The sale") + " was refused.";
            }
        }
    }
}

#endif
