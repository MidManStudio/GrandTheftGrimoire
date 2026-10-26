// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedMagicLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Editor test tools for spells. Adds the caster to the player, places a row of target dummies at
    /// growing range, and in Play mode refills the vessels and prints the caster state.
    /// Menu: GTG, Magic.
    /// </summary>
    public static class ManagedMagicLab
    {
        [MenuItem("GTG/Magic/Ensure Caster On Player", false, 600)]
        private static void EnsureCasterMenu()
        {
            ManagedSpellCaster caster = EnsureCaster();
            GtgLabKit.Finish(caster.gameObject, "Spell caster ready on the player. In Play: left click or F casts, 1, 2, 3 or Tab picks the spell, "
                + "R uses a crystal. Fireballs hurt anything with a ManagedHealth.");
        }

        [MenuItem("GTG/Magic/Spawn Dummy Row At 8 To 24 m", false, 601)]
        private static void SpawnRowMenu()
        {
            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 feet = player.FeetPosition;

            GameObject first = null;
            for (int i = 0; i < 5; i++)
            {
                float range = 8f + i * 4f;
                float side = (i % 2 == 0 ? -1f : 1f) * 1.5f;
                GameObject dummy = ManagedHealthLab.SpawnDummy(feet + forward * range + right * side, "Spell Dummy " + range + " m");
                if (first == null)
                {
                    first = dummy;
                }
            }

            GtgLabKit.Finish(first, "Five dummies placed at 8, 12, 16, 20 and 24 m. A shot that burns out its SP short of a dummy shows the range limit.");
        }

        [MenuItem("GTG/Magic/Refill Vessels (Play)", false, 610)]
        private static void RefillMenu()
        {
            ManagedSpellCaster caster = PlayCaster("Refill Vessels");
            if (caster != null)
            {
                caster.RefillVessels();
                GtgLabKit.Log("Every orb and bottle is full again.");
            }
        }

        [MenuItem("GTG/Magic/Print Caster State (Play)", false, 611)]
        private static void PrintMenu()
        {
            ManagedSpellCaster caster = PlayCaster("Print Caster State");
            if (caster != null)
            {
                GtgLabKit.Log("Selected vessel: " + caster.SelectedVesselText + ". Shots in flight: " + caster.ShotCount
                    + ". Purse: " + (caster.Wallet != null ? caster.Wallet.Describe() : "not built yet") + ".");
            }
        }

        /// <summary>The caster of the player, added when missing. Hit mask Ground, Wall and Npc.</summary>
        public static ManagedSpellCaster EnsureCaster()
        {
            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            ManagedSpellCaster caster = player.GetComponent<ManagedSpellCaster>();
            if (caster == null)
            {
                caster = Undo.AddComponent<ManagedSpellCaster>(player.gameObject);
                GtgLabKit.SetInt(caster, "_hitMask", GtgLabKit.HitMask);
            }

            return caster;
        }

        private static ManagedSpellCaster PlayCaster(string action)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return null;
            }

            ManagedSpellCaster caster = Object.FindFirstObjectByType<ManagedSpellCaster>();
            if (caster == null)
            {
                GtgLabKit.Warn("No ManagedSpellCaster in the scene. Use GTG, Magic, Ensure Caster On Player.");
            }

            return caster;
        }
    }
}
