// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellTypes.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    public enum ManagedSpellKind : byte
    {
        Fireball = 0,
        Ice = 1,
    }

    /// <summary>One spell hit, or a collapse of the bubble. The caster raises it. The chemistry field and the spell damage consume it.</summary>
    public struct ManagedSpellImpact
    {
        public Vector3 Position;
        public Vector3 Normal;
        public ManagedSpellKind Kind;

        /// <summary>Id of the payload the spell carried, see <see cref="ManagedSpellPayloads"/>.</summary>
        public int PayloadId;

        /// <summary>The object that cast the spell, so damage can skip the caster. Null for an unknown source.</summary>
        public GameObject Source;
    }
}
