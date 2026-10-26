// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedLabArena.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Building;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Chemistry;
using MidManStudio.Gtg.Managed.Economy;
using MidManStudio.Gtg.Managed.Facing;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.Melee;
using MidManStudio.Gtg.Managed.NPC;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Lab
{
    /// <summary>
    /// One click that fills the open scene with everything the labs of the feature folders build: ground,
    /// player, camera, melee, spells, facing, build mode, a hazard field and a few dummies. It only calls
    /// the Ensure methods of those labs, so a second click changes nothing that already exists.
    /// This is the one place that reads every feature folder. Menu: GTG, Lab.
    /// </summary>
    public static class ManagedLabArena
    {
        [MenuItem("GTG/Lab/Build Test Arena", false, 1)]
        private static void BuildMenu()
        {
            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            ManagedCameraLab.EnsureRig();
            ManagedMeleeLab.EnsureMelee();
            ManagedMagicLab.EnsureCaster();
            GtgLabKit.Ensure<ManagedBodyFacing>(player.gameObject);
            GtgLabKit.Ensure<ManagedHealth>(player.gameObject);
            GtgLabKit.Ensure<ManagedNpcThreatSource>(player.gameObject);
            GtgLabKit.Ensure<ManagedPlayerItemBag>(player.gameObject);
            GtgLabKit.Ensure<ManagedPlayerWallet>(player.gameObject);
            ManagedBuildingLab.EnsureBuildMode();
            ManagedChemistryLab.EnsureField();

            // A first run gets the dummies. A scene that already has dummies keeps what it has.
            if (GameObject.Find("Melee Dummy 1") == null)
            {
                Vector3 feet = player.FeetPosition;
                Vector3 forward = Vector3.forward;
                ManagedHealthLab.SpawnDummy(feet + forward * 1.8f, "Melee Dummy 1");
                ManagedHealthLab.SpawnDummy(feet + forward * 8f + Vector3.left * 1.5f, "Spell Dummy 8 m");
                ManagedHealthLab.SpawnDummy(feet + forward * 14f + Vector3.right * 1.5f, "Spell Dummy 14 m");
            }

            GtgLabKit.Finish(player.gameObject, "Test arena ready. Press Play. WASD moves, mouse looks, V switches view, 1 2 3 pick a spell, click casts, "
                + "\\ stocks the bag, 5 equips the sword, B opens build mode, ` opens the cheats. Add a ManagedHud for the bars if you want them.");
        }

        [MenuItem("GTG/Lab/Print What The Scene Has", false, 2)]
        private static void PrintMenu()
        {
            GtgLabKit.Log("Player " + Has<ManagedCharacter>() + ", camera rig " + Has<ManagedCameraRig>() + ", melee " + Has<ManagedMelee>()
                + ", caster " + Has<ManagedSpellCaster>() + ", body facing " + Has<ManagedBodyFacing>() + ", build mode " + Has<ManagedBuildMode>()
                + ", hazard field " + Has<ManagedChemicalHazardField>() + ", healths " + Object.FindObjectsByType<ManagedHealth>(FindObjectsSortMode.None).Length
                + ", NPC brains " + Object.FindObjectsByType<ManagedNpcBrain>(FindObjectsSortMode.None).Length + ".");
        }

        private static string Has<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>() != null ? "yes" : "no";
        }
    }
}
