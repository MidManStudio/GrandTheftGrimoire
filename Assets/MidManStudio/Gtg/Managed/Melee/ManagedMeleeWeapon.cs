// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMeleeWeapon.cs"
// ============================================================================

using System;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Melee
{
    /// <summary>
    /// The numbers of one melee weapon. Serialized, so a scene edits them in the Inspector. The
    /// defaults describe the Iron Sword, item id 1 of the item table. They are placeholders until
    /// the weapon numbers come from the item table.
    /// </summary>
    [Serializable]
    public sealed class ManagedMeleeWeapon
    {
        [Tooltip("Id of the item that equips this weapon.")]
        public int ItemId = 1;

        public string DisplayName = "Iron Sword";

        [Tooltip("Damage of one hit, applied to every target inside the cone.")]
        public float Damage = 25f;

        [Tooltip("How far from the chest a target may be, to the nearest point of its collider.")]
        public float Reach = 2.2f;

        [Tooltip("Width of the cone in degrees, flat on the ground, centered on the aim direction.")]
        [Range(10f, 360f)]
        public float ArcDegrees = 100f;

        [Tooltip("Seconds from the press to the moment the cone is checked.")]
        public float HitDelaySeconds = 0.25f;

        [Tooltip("Seconds from the press until the next swing can start. At least the hit delay.")]
        public float SwingSeconds = 0.6f;

        [Tooltip("Height of the chest above the feet, where the cone starts.")]
        public float OriginHeight = 1.1f;

        /// <summary>The hit delay, never negative.</summary>
        public float SafeHitDelay { get { return Mathf.Max(0f, HitDelaySeconds); } }

        /// <summary>The swing length, never shorter than the hit delay.</summary>
        public float SafeSwingSeconds { get { return Mathf.Max(SafeHitDelay, SwingSeconds); } }
    }
}
