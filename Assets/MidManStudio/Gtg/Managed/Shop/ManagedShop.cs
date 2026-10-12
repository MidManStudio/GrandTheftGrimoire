// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShop.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Flags;
using MidManStudio.Gtg.Managed.Items;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// One shop at run time: a definition, the stock left, and the rules for buying and selling.
    /// The shop gets its catalog and its flags as interfaces and its purse and bag, also
    /// interfaces, with each trade, so it can be tested with plain objects and none of those has to
    /// know about the shop. Prices and counts are checked before anything changes, and a refused trade leaves
    /// the wallet, the bag and the stock as they were. The shop holds plain values and knows
    /// nothing about Unity.
    /// </summary>
    public sealed class ManagedShop
    {
        private readonly string _id;
        private readonly string _displayName;
        private readonly string _shopkeeperName;
        private readonly string _requiredFlag;
        private readonly int _buyPercent;
        private readonly IManagedItemCatalog _catalog;
        private readonly IManagedFlagSource _flags;
        private readonly List<ManagedShopLine> _sells = new List<ManagedShopLine>();
        private readonly List<int> _stock = new List<int>();
        private readonly List<ManagedShopBuyLine> _buys = new List<ManagedShopBuyLine>();

        /// <summary>
        /// Copies the definition, so later edits to it do not reach a running shop. When two lines
        /// name the same item the first one is used, and a null line is
        /// skipped. The catalog and the flags may be null: every trade then reports an unknown
        /// item, and a shop that needs a flag stays closed.
        /// </summary>
        public ManagedShop(ManagedShopDefinition definition, IManagedItemCatalog catalog, IManagedFlagSource flags)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            _id = definition.Id ?? string.Empty;
            _displayName = definition.DisplayName ?? string.Empty;
            _shopkeeperName = definition.ShopkeeperName ?? string.Empty;
            _requiredFlag = definition.RequiredFlag ?? string.Empty;
            _buyPercent = definition.BuyPercent;
            _catalog = catalog;
            _flags = flags;

            var seenSold = new HashSet<int>();
            if (definition.Sells != null)
            {
                for (int i = 0; i < definition.Sells.Count; i++)
                {
                    ManagedShopLine line = definition.Sells[i];
                    if (line != null && seenSold.Add(line.ItemId))
                    {
                        _sells.Add(new ManagedShopLine(line.ItemId, line.PriceSilver, line.Stock));
                        _stock.Add(line.Stock < 0 ? ManagedShopLine.Unlimited : line.Stock);
                    }
                }
            }

            var seenBought = new HashSet<int>();
            if (definition.Buys != null)
            {
                for (int i = 0; i < definition.Buys.Count; i++)
                {
                    ManagedShopBuyLine line = definition.Buys[i];
                    if (line != null && seenBought.Add(line.ItemId))
                    {
                        _buys.Add(new ManagedShopBuyLine(line.ItemId, line.PriceSilver));
                    }
                }
            }
        }

        public string Id { get { return _id; } }

        public string DisplayName { get { return _displayName.Length == 0 ? _id : _displayName; } }

        public string ShopkeeperName { get { return _shopkeeperName; } }

        public string RequiredFlag { get { return _requiredFlag; } }

        /// <summary>
        /// True when the shop needs no flag, or its flag is set. The flag is read on every call, so
        /// the shop opens the moment the flag is set.
        /// </summary>
        public bool IsOpen
        {
            get
            {
                string flag = RequiredFlag;
                return flag.Length == 0 || (_flags != null && _flags.IsSet(flag));
            }
        }

        /// <summary>Raised after a trade went through, with what was traded and for how much.</summary>
        public event Action<ManagedShopTrade> Traded;

        public int SellOfferCount { get { return _sells.Count; } }

        public int BuyOfferCount { get { return _buys.Count; } }

        /// <summary>Reads row <paramref name="index"/> of what the shop sells. False outside the list.</summary>
        public bool TryGetSellOffer(int index, out ManagedShopOffer offer)
        {
            offer = default(ManagedShopOffer);
            if (index < 0 || index >= _sells.Count)
            {
                return false;
            }

            ManagedShopLine line = _sells[index];
            ManagedItemInfo info;
            long price = _catalog != null && _catalog.TryGet(line.ItemId, out info) ? SellUnitPrice(line) : 0;
            offer = new ManagedShopOffer(line.ItemId, NameOf(line.ItemId), price, _stock[index]);
            return true;
        }

        /// <summary>Reads row <paramref name="index"/> of what the shop buys. False outside the list.</summary>
        public bool TryGetBuyOffer(int index, out ManagedShopOffer offer)
        {
            offer = default(ManagedShopOffer);
            if (index < 0 || index >= _buys.Count)
            {
                return false;
            }

            ManagedShopBuyLine line = _buys[index];
            ManagedItemInfo info;
            long price = _catalog != null && _catalog.TryGet(line.ItemId, out info) ? BuyUnitPrice(line, info) : 0;
            offer = new ManagedShopOffer(line.ItemId, NameOf(line.ItemId), price, ManagedShopLine.Unlimited);
            return true;
        }

        /// <summary>What the player would pay for <paramref name="count"/> of an item, with the checks a purchase makes except the wallet and the bag.</summary>
        public ManagedShopResult QuoteBuy(int itemId, int count, out long totalSilver)
        {
            int index;
            return CheckBuy(itemId, count, out totalSilver, out index);
        }

        /// <summary>What the shop would pay for <paramref name="count"/> of an item, with the checks a sale makes except the bag.</summary>
        public ManagedShopResult QuoteSell(int itemId, int count, out long totalSilver)
        {
            int index;
            return CheckSell(itemId, count, out totalSilver, out index);
        }

        /// <summary>
        /// Buys <paramref name="count"/> of an item. The checks run in this order: the shop is open,
        /// the count is valid, the shop sells the item, the catalog knows it, it has a price, there
        /// is enough stock, the wallet and the bag exist, the wallet can pay and the bag has room.
        /// Only when all pass does the coin leave the wallet, the items enter the bag and the stock
        /// drop. Any refusal changes nothing.
        /// </summary>
        public ManagedShopResult TryBuy(IManagedCoinPurse wallet, IManagedItemHolder bag, int itemId, int count)
        {
            long total;
            int index;
            ManagedShopResult quote = CheckBuy(itemId, count, out total, out index);
            if (quote != ManagedShopResult.Ok)
            {
                return quote;
            }

            if (wallet == null)
            {
                return ManagedShopResult.MissingWallet;
            }

            if (bag == null)
            {
                return ManagedShopResult.MissingBag;
            }

            if (!wallet.CanAfford(total))
            {
                return ManagedShopResult.CannotAfford;
            }

            if (!bag.TryAdd(itemId, count))
            {
                return ManagedShopResult.BagFull;
            }

            if (!wallet.TrySpend(0, total))
            {
                // The wallet said yes a moment ago, so this is a wallet that changed its mind.
                // Give the items back and keep the books straight.
                bag.TryRemove(itemId, count);
                return ManagedShopResult.CannotAfford;
            }

            if (_stock[index] != ManagedShopLine.Unlimited)
            {
                _stock[index] -= count;
            }

            Raise(new ManagedShopTrade(Id, ManagedTradeKind.Bought, itemId, count, total));
            return ManagedShopResult.Ok;
        }

        /// <summary>
        /// Sells <paramref name="count"/> of an item to the shop. The checks run in this order: the
        /// shop is open, the count is valid, the shop buys the item, the catalog knows it, it has a
        /// price, the wallet and the bag exist, the bag holds enough and the wallet can take the
        /// coin. Only then do the items leave the bag and the coin enter the wallet. The shop does
        /// not add what it buys to its stock. Any refusal changes nothing.
        /// </summary>
        public ManagedShopResult TrySell(IManagedCoinPurse wallet, IManagedItemHolder bag, int itemId, int count)
        {
            long total;
            int index;
            ManagedShopResult quote = CheckSell(itemId, count, out total, out index);
            if (quote != ManagedShopResult.Ok)
            {
                return quote;
            }

            if (wallet == null)
            {
                return ManagedShopResult.MissingWallet;
            }

            if (bag == null)
            {
                return ManagedShopResult.MissingBag;
            }

            if (bag.CountOf(itemId) < count)
            {
                return ManagedShopResult.NotEnoughItems;
            }

            if (wallet.TotalSilver > long.MaxValue - total)
            {
                return ManagedShopResult.BadCount;
            }

            if (!bag.TryRemove(itemId, count))
            {
                return ManagedShopResult.NotEnoughItems;
            }

            wallet.AddSilver(total);
            Raise(new ManagedShopTrade(Id, ManagedTradeKind.Sold, itemId, count, total));
            return ManagedShopResult.Ok;
        }

        private ManagedShopResult CheckBuy(int itemId, int count, out long total, out int index)
        {
            total = 0;
            index = -1;
            if (!IsOpen)
            {
                return ManagedShopResult.Closed;
            }

            if (count < 1)
            {
                return ManagedShopResult.BadCount;
            }

            for (int i = 0; i < _sells.Count; i++)
            {
                if (_sells[i].ItemId == itemId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return ManagedShopResult.NotSold;
            }

            ManagedItemInfo info;
            if (_catalog == null || !_catalog.TryGet(itemId, out info))
            {
                return ManagedShopResult.UnknownItem;
            }

            long unit = SellUnitPrice(_sells[index]);
            if (unit <= 0)
            {
                return ManagedShopResult.NoPrice;
            }

            int stock = _stock[index];
            if (stock != ManagedShopLine.Unlimited && stock < count)
            {
                return ManagedShopResult.OutOfStock;
            }

            if (unit > long.MaxValue / count)
            {
                return ManagedShopResult.BadCount;
            }

            total = unit * count;
            return ManagedShopResult.Ok;
        }

        private ManagedShopResult CheckSell(int itemId, int count, out long total, out int index)
        {
            total = 0;
            index = -1;
            if (!IsOpen)
            {
                return ManagedShopResult.Closed;
            }

            if (count < 1)
            {
                return ManagedShopResult.BadCount;
            }

            for (int i = 0; i < _buys.Count; i++)
            {
                if (_buys[i].ItemId == itemId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return ManagedShopResult.NotBought;
            }

            ManagedItemInfo info;
            if (_catalog == null || !_catalog.TryGet(itemId, out info))
            {
                return ManagedShopResult.UnknownItem;
            }

            long unit = BuyUnitPrice(_buys[index], info);
            if (unit <= 0)
            {
                return ManagedShopResult.NoPrice;
            }

            if (unit > long.MaxValue / count)
            {
                return ManagedShopResult.BadCount;
            }

            total = unit * count;
            return ManagedShopResult.Ok;
        }

        // The price of one item the player buys. The line's own price wins, otherwise the item's
        // value. 0 means there is no price. Call only for an item the catalog knows.
        private long SellUnitPrice(ManagedShopLine line)
        {
            if (line.PriceSilver > 0)
            {
                return line.PriceSilver;
            }

            ManagedItemInfo info;
            return _catalog != null && _catalog.TryGet(line.ItemId, out info) ? info.ValueSilver : 0;
        }

        // The price of one item the shop buys. The line's own price wins, otherwise the buy percent
        // of the item's value, rounded down but never below 1 silver for an item with a value.
        private long BuyUnitPrice(ManagedShopBuyLine line, ManagedItemInfo info)
        {
            if (line.PriceSilver > 0)
            {
                return line.PriceSilver;
            }

            int percent = Math.Max(0, _buyPercent);
            if (percent == 0 || info.ValueSilver <= 0)
            {
                return 0;
            }

            return Math.Max(1L, PercentOf(info.ValueSilver, percent));
        }

        // The percent of a value, rounded down. It is worked as whole hundreds plus the rest, so a
        // huge value times a huge percent stops at the largest number instead of wrapping round to
        // a negative one, and a value that fits is never cut short.
        private static long PercentOf(long value, int percent)
        {
            long whole = value / 100;
            long rest = value % 100;
            long scaled = whole != 0 && percent > long.MaxValue / whole ? long.MaxValue : whole * percent;
            long extra = rest * percent / 100;
            return scaled > long.MaxValue - extra ? long.MaxValue : scaled + extra;
        }

        private string NameOf(int itemId)
        {
            ManagedItemInfo info;
            return _catalog != null && _catalog.TryGet(itemId, out info) ? info.Name : "item " + itemId;
        }

        private void Raise(ManagedShopTrade trade)
        {
            Action<ManagedShopTrade> handler = Traded;
            if (handler != null)
            {
                handler(trade);
            }
        }
    }
}
