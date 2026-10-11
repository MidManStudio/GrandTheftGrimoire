// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedVesselCharging.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Economy;

namespace MidManStudio.Gtg.Managed.Magic
{
    public enum ManagedChargeResult : byte
    {
        /// <summary>A crystal was used and the orb took SP from it.</summary>
        Charged = 0,

        /// <summary>The slot is a bottle, or holds nothing. Only an orb can be recharged.</summary>
        NotAnOrb = 1,

        /// <summary>The orb has no room for more SP.</summary>
        Full = 2,

        /// <summary>The wallet has no crystal to use.</summary>
        NoCrystal = 3,
    }

    /// <summary>
    /// The soul gem rule: a crystal fills an orb at once and is used up. It picks the crystal that
    /// suits what the orb is missing, see <see cref="ManagedWallet.TryTakeCrystalFor"/>. The part
    /// of the crystal that does not fit in the orb is lost. Nothing changes unless a crystal is used.
    /// </summary>
    public static class ManagedVesselCharging
    {
        public static ManagedChargeResult Recharge(
            ManagedVesselBelt belt, int slot, ManagedWallet wallet, out float crystalSp, out float taken)
        {
            crystalSp = 0f;
            taken = 0f;

            if (belt.KindAt(slot) != ManagedVesselKind.Orb || !belt.IsInUse(slot))
            {
                return ManagedChargeResult.NotAnOrb;
            }

            float missing = belt.MissingSpAt(slot);
            if (!(missing > 0f))
            {
                return ManagedChargeResult.Full;
            }

            if (wallet == null || !wallet.TryTakeCrystalFor(missing, out crystalSp))
            {
                return ManagedChargeResult.NoCrystal;
            }

            taken = belt.Charge(slot, crystalSp);
            return ManagedChargeResult.Charged;
        }
    }
}
