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
    }

    /// <summary>One spell hit. The caster raises it and the chemistry field consumes it.</summary>
    public struct ManagedSpellImpact
    {
        public Vector3 Position;
        public Vector3 Normal;
        public ManagedSpellKind Kind;
    }
}
