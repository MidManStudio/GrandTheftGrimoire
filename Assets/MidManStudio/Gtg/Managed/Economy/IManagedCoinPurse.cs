// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedCoinPurse.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Economy
{
    /// <summary>
    /// The coin side of a wallet, which is all a shop needs: how much there is, whether a price can
    /// be paid, paying it and being paid. The shop holds this and not the wallet, so it does not
    /// care what carries the coin, and a test can hand it a purse of its own. <see cref="ManagedWallet"/>
    /// is the real one.
    /// </summary>
    public interface IManagedCoinPurse
    {
        /// <summary>All the coin held, counted in silver.</summary>
        long TotalSilver { get; }

        /// <summary>True when the purse holds at least this much silver.</summary>
        bool CanAfford(long silver);

        /// <summary>Pays a price written as gold and silver. Fails and changes nothing when it cannot be paid.</summary>
        bool TrySpend(long gold, long silver);

        /// <summary>Adds silver. A zero or negative amount does nothing.</summary>
        void AddSilver(long silver);
    }
}
