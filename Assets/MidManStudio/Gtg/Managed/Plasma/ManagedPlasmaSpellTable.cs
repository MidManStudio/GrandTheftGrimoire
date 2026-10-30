// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaSpellTable.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Magic;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Which plasma ring each spell bursts with when it hits: its palette and look, how long the burst lasts, and the blast
    /// radius to draw when the spell has none of its own. The table lives in code for now and moves to an mdix table later, like
    /// the spell table and the glyph spell table. This is where the plasma system meets the spell system: Magic knows nothing
    /// about plasma, and the generic ring, ramp and renderer know nothing about spells.
    /// </summary>
    public static class ManagedPlasmaSpellTable
    {
        /// <summary>How a spell's burst is drawn.</summary>
        public sealed class Style
        {
            public ManagedPlasmaRingSpec Spec;

            /// <summary>Seconds the burst lasts.</summary>
            public float Seconds;

            /// <summary>Blast radius in metres used when the spell definition has no impact radius, as ice has none.</summary>
            public float FallbackRadius;
        }

        // Built once and shared. Callers read them and never change them: the tracker copies the spec.
        private static readonly Style Fireball = new Style
        {
            Spec = new ManagedPlasmaRingSpec { Seed = 11, Palette = ManagedPlasmaPalette.Fire, Turbulence = 0.8f, Streaks = 0.9f },
            Seconds = 0.6f,
            FallbackRadius = 3f,
        };

        private static readonly Style Ice = new Style
        {
            Spec = new ManagedPlasmaRingSpec { Seed = 21, Palette = ManagedPlasmaPalette.Water, Turbulence = 0.5f, Streaks = 0.6f },
            Seconds = 0.7f,
            FallbackRadius = 1.8f,
        };

        private static readonly Style Seeker = new Style
        {
            Spec = new ManagedPlasmaRingSpec { Seed = 31, Palette = ManagedPlasmaPalette.Arcane, Turbulence = 0.7f, Streaks = 0.8f },
            Seconds = 0.55f,
            FallbackRadius = 2.5f,
        };

        /// <summary>The burst style of a spell. A spell with no entry gets the Fireball style. Never null.</summary>
        public static Style For(ManagedSpellKind kind)
        {
            switch (kind)
            {
                case ManagedSpellKind.Ice:
                    return Ice;
                case ManagedSpellKind.Seeker:
                    return Seeker;
                default:
                    return Fireball;
            }
        }

        /// <summary>
        /// The blast radius to draw for a spell: its own impact radius from the spell table when it has one, so the burst follows the
        /// tuning, and the style's fallback when it has none.
        /// </summary>
        public static float BlastRadius(ManagedSpellKind kind)
        {
            ManagedSpellDefinition definition = ManagedSpellDefinition.For(kind);
            if (definition != null && definition.ImpactRadius > 0f)
            {
                return definition.ImpactRadius;
            }

            return For(kind).FallbackRadius;
        }
    }
}
