// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "IManagedDamageable.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Health
{
    /// <summary>
    /// Implemented by anything that can take damage. Spells, melee and hazards call this
    /// interface, so none of them needs to know whether the target is an NPC or the player.
    /// </summary>
    public interface IManagedDamageable
    {
        /// <summary>False once dead. A hit on something already down is ignored.</summary>
        bool IsAlive { get; }

        /// <summary>
        /// Applies damage. The source is the object that caused it, or null for an
        /// environmental cause such as a hazard.
        /// </summary>
        void TakeDamage(float amount, GameObject source = null);
    }
}
