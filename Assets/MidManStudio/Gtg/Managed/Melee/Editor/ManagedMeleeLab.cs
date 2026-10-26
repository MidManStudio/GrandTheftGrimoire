// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedMeleeLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// Editor test tools for the sword. Adds melee to the player, spawns dummies in front of it, draws
    /// the strike cone in the Scene view and, in Play mode, equips the sword and strikes without a
    /// click. Menu: GTG, Melee.
    /// </summary>
    public static class ManagedMeleeLab
    {
        [MenuItem("GTG/Melee/Ensure Melee On Player", false, 500)]
        private static void EnsureMeleeMenu()
        {
            ManagedMelee melee = EnsureMelee();
            GtgLabKit.Finish(melee.gameObject, "Melee ready on the player. In Play: press \\ to stock the bag, then 5 to equip the Iron Sword, "
                + "then click or press F to swing. The orange fan in the Scene view is the strike cone.");
        }

        [MenuItem("GTG/Melee/Spawn 3 Dummies In Front Of Player", false, 501)]
        private static void SpawnDummiesMenu()
        {
            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            Vector3 forward = Flat(player.transform.forward);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 feet = player.FeetPosition;
            GameObject first = null;
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = feet + forward * 1.8f + right * ((i - 1) * 1.6f);
                GameObject dummy = ManagedHealthLab.SpawnDummy(spot, "Melee Dummy " + (i + 1));
                if (first == null)
                {
                    first = dummy;
                }
            }

            GtgLabKit.Finish(first, "Three dummies placed 1.8 m in front of the player, inside the 2.2 m reach.");
        }

        [MenuItem("GTG/Melee/Equip Sword (Play)", false, 510)]
        private static void EquipMenu()
        {
            ManagedMelee melee = PlayMelee("Equip Sword");
            if (melee != null)
            {
                melee.Equip();
                GtgLabKit.Log("Sword in hand: " + melee.IsEquipped + ". The fire button now swings.");
            }
        }

        [MenuItem("GTG/Melee/Unequip Sword (Play)", false, 511)]
        private static void UnequipMenu()
        {
            ManagedMelee melee = PlayMelee("Unequip Sword");
            if (melee != null)
            {
                melee.Unequip();
                GtgLabKit.Log("Sword away. The fire button casts again.");
            }
        }

        [MenuItem("GTG/Melee/Strike Now Without Input (Play)", false, 512)]
        private static void StrikeNowMenu()
        {
            ManagedMelee melee = PlayMelee("Strike Now");
            if (melee == null)
            {
                return;
            }

            ManagedCharacter character = melee.GetComponent<ManagedCharacter>();
            ManagedMeleeWeapon weapon = melee.Weapon;
            Vector3 origin = character.FeetPosition + Vector3.up * weapon.OriginHeight;
            Vector3 forward = Quaternion.Euler(0f, character.Yaw, 0f) * Vector3.forward;
            int hits = ManagedMeleeStrike.Strike(origin, forward, weapon, melee.gameObject, GtgLabKit.HitMask);
            GtgLabKit.Log("Strike hit " + hits + " target(s) in the " + weapon.ArcDegrees + " degree cone, " + weapon.Reach + " m. It uses the same check as a swing, without the timing.");
        }

        /// <summary>The melee component of the player, added when missing. Hit mask Ground, Wall and Npc.</summary>
        public static ManagedMelee EnsureMelee()
        {
            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            ManagedMelee melee = player.GetComponent<ManagedMelee>();
            if (melee == null)
            {
                melee = Undo.AddComponent<ManagedMelee>(player.gameObject);
                GtgLabKit.SetInt(melee, "_hitMask", GtgLabKit.HitMask);
            }

            GtgLabKit.Ensure<ManagedMeleeEquipBridge>(player.gameObject);
            return melee;
        }

        private static ManagedMelee PlayMelee(string action)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return null;
            }

            ManagedMelee melee = Object.FindFirstObjectByType<ManagedMelee>();
            if (melee == null)
            {
                GtgLabKit.Warn("No ManagedMelee in the scene. Use GTG, Melee, Ensure Melee On Player.");
            }

            return melee;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
        }

        // The strike cone, flat at the height the check measures from. Orange at rest, red while swinging.
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawCone(ManagedMelee melee, GizmoType type)
        {
            ManagedCharacter character = melee.GetComponent<ManagedCharacter>();
            if (character == null)
            {
                return;
            }

            ManagedMeleeWeapon weapon = melee.Weapon;
            Vector3 origin = character.FeetPosition + Vector3.up * weapon.OriginHeight;
            Vector3 forward = Quaternion.Euler(0f, character.Yaw, 0f) * Vector3.forward;
            float half = weapon.ArcDegrees * 0.5f;

            Gizmos.color = Application.isPlaying && melee.IsSwinging ? Color.red : new Color(1f, 0.55f, 0f, 1f);
            Vector3 previous = origin + Quaternion.AngleAxis(-half, Vector3.up) * forward * weapon.Reach;
            Gizmos.DrawLine(origin, previous);
            const int Segments = 24;
            for (int i = 1; i <= Segments; i++)
            {
                float angle = -half + weapon.ArcDegrees * i / Segments;
                Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * weapon.Reach;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.DrawLine(origin, previous);
        }
    }
}
