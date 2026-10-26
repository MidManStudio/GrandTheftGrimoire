// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedEconomyLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Economy
{
    /// <summary>
    /// Editor test tools for the purse. In Play mode gives the player gold, silver or crystals and prints
    /// the purse. The spell caster adds the wallet component when play starts. Menu: GTG, Economy.
    /// </summary>
    public static class ManagedEconomyLab
    {
        [MenuItem("GTG/Economy/Give 1 Gold (Play)", false, 800)]
        private static void GiveGoldMenu()
        {
            ManagedWallet wallet = PlayWallet("Give Gold");
            if (wallet != null)
            {
                wallet.AddGold(1);
                GtgLabKit.Log("Purse: " + wallet.Describe());
            }
        }

        [MenuItem("GTG/Economy/Give 100 Silver (Play)", false, 801)]
        private static void GiveSilverMenu()
        {
            ManagedWallet wallet = PlayWallet("Give Silver");
            if (wallet != null)
            {
                wallet.AddSilver(100);
                GtgLabKit.Log("Purse: " + wallet.Describe());
            }
        }

        [MenuItem("GTG/Economy/Give 3 Medium Crystals (Play)", false, 802)]
        private static void GiveCrystalsMenu()
        {
            ManagedWallet wallet = PlayWallet("Give Crystals");
            if (wallet != null)
            {
                wallet.AddCrystals(ManagedCrystalSize.Medium, 3);
                GtgLabKit.Log("Purse: " + wallet.Describe());
            }
        }

        [MenuItem("GTG/Economy/Print Purse (Play)", false, 803)]
        private static void PrintMenu()
        {
            ManagedWallet wallet = PlayWallet("Print Purse");
            if (wallet != null)
            {
                GtgLabKit.Log("Purse: " + wallet.Describe());
            }
        }

        private static ManagedWallet PlayWallet(string action)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return null;
            }

            ManagedPlayerWallet holder = Object.FindFirstObjectByType<ManagedPlayerWallet>();
            if (holder == null)
            {
                GtgLabKit.Warn("No ManagedPlayerWallet in the scene. The spell caster adds one when play starts, so use GTG, Magic, Ensure Caster On Player first.");
                return null;
            }

            return holder.Wallet;
        }
    }
}
