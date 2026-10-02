#if GTG_ECS
// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/magic.md, section "SpellComponents.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.Magic
{
    public enum SpellKind : byte
    {
        Fireball = 0,
    }

    /// <summary>Casting state and tuning for one character. Baked from <c>SpellCasterAuthoring</c>.</summary>
    public struct SpellCaster : IComponentData
    {
        public float Cooldown;
        public float CooldownRemaining;
        public float ProjectileSpeed;
        public float ProjectileLifetime;

        /// <summary>Muzzle position in the character's yaw space, from the feet pivot.</summary>
        public float3 MuzzleOffset;
    }

    /// <summary>A fireball in flight. Moves in a straight line until it hits something or expires.</summary>
    public struct FireballProjectile : IComponentData
    {
        public float3 Velocity;
        public float RemainingLife;

        /// <summary>The caster, ignored by the hit ray.</summary>
        public Entity Owner;
    }

    /// <summary>
    /// One spell hit, created by the projectile system as its own short-lived entity.
    /// The chemistry system reads it and destroys it.
    /// </summary>
    public struct SpellImpact : IComponentData
    {
        public float3 Position;
        public float3 Normal;
        public SpellKind Kind;
    }
}
#endif
