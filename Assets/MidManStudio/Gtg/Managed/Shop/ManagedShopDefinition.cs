// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedShopDefinition.cs"
// ============================================================================

using System;
using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Shop
{
    /// <summary>
    /// The data of one shop: who runs it, what it sells, what it buys and what must have happened
    /// before it opens. It is a plain serializable class with no logic, so a data file, a
    /// ScriptableObject or code can fill it, and <see cref="ManagedShop"/> does the work.
    /// </summary>
    [Serializable]
    public sealed class ManagedShopDefinition
    {
        /// <summary>The short name other code finds the shop by.</summary>
        public string Id;

        public string DisplayName;

        /// <summary>The shopkeeper's name. Empty until the name is decided.</summary>
        public string ShopkeeperName;

        /// <summary>
        /// The flag that must be set for the shop to open. Empty means the shop is always open.
        /// The hidden shop names the flag that its mission sets.
        /// </summary>
        public string RequiredFlag;

        /// <summary>
        /// What the shop pays for an item that has no price of its own, as a percent of the
        /// item's value. 20 is a placeholder for "dirt cheap".
        /// </summary>
        public int BuyPercent = 20;

        public List<ManagedShopLine> Sells = new List<ManagedShopLine>();

        public List<ManagedShopBuyLine> Buys = new List<ManagedShopBuyLine>();
    }
}
