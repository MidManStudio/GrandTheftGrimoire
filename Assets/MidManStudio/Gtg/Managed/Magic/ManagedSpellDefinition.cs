// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellDefinition.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Tuning of one spell. The table lives in code for now and moves to an mdix table
    /// later, like the chemistry recipes. The order of the table is the slot order, so
    /// key 1 picks the first entry.
    /// </summary>
    public sealed class ManagedSpellDefinition
    {
        public ManagedSpellKind Kind;
        public string DisplayName;
        public float CooldownSeconds;
        public float Speed;
        public float LifetimeSeconds;

        /// <summary>Radius of the sphere swept along the path, so thin colliders are still hit.</summary>
        public float SweepRadius;

        /// <summary>Diameter of the drawn shot.</summary>
        public float VisualDiameter;

        public Color Color;

        /// <summary>Damage at the center of the explosion. Zero means the spell hurts nothing by itself.</summary>
        public float ImpactDamage;

        /// <summary>Radius of the damaging explosion in meters.</summary>
        public float ImpactRadius;

        /// <summary>Share of the damage that remains at the edge of the radius, from 0 to 1.</summary>
        public float ImpactEdgeFraction;

        public static readonly ManagedSpellDefinition Fireball = new ManagedSpellDefinition
        {
            Kind = ManagedSpellKind.Fireball,
            DisplayName = "Fireball",
            CooldownSeconds = 0.4f,
            Speed = 25f,
            LifetimeSeconds = 3f,
            SweepRadius = 0.15f,
            VisualDiameter = 0.4f,
            Color = new Color(1f, 0.45f, 0.1f, 1f),
            ImpactDamage = 40f,
            ImpactRadius = 3f,
            ImpactEdgeFraction = 0.25f,
        };

        public static readonly ManagedSpellDefinition Ice = new ManagedSpellDefinition
        {
            Kind = ManagedSpellKind.Ice,
            DisplayName = "Ice",
            CooldownSeconds = 0.6f,
            Speed = 18f,
            LifetimeSeconds = 3.5f,
            SweepRadius = 0.15f,
            VisualDiameter = 0.35f,
            Color = new Color(0.55f, 0.85f, 1f, 1f),
        };

        private static readonly ManagedSpellDefinition[] Slots = { Fireball, Ice };

        public static int Count { get { return Slots.Length; } }

        public static ManagedSpellDefinition At(int index)
        {
            return Slots[index];
        }

        /// <summary>The definition of a kind, or null when the table has none.</summary>
        public static ManagedSpellDefinition For(ManagedSpellKind kind)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Kind == kind)
                {
                    return Slots[i];
                }
            }

            return null;
        }
    }
}
