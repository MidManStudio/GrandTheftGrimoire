// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/chemistry.md, section "ChemistryRecipe.cs"
// ============================================================================

using MidManStudio.Gtg.Magic;
using Unity.Mathematics;

namespace MidManStudio.Gtg.Chemistry
{
    /// <summary>
    /// Game side description of one reaction. Alembic runs the atoms, and the
    /// hazard numbers here decide the footprint. Values live in code for now
    /// and move to an mdix table later.
    /// </summary>
    public sealed class ChemistryRecipe
    {
        public string Id = "fireball";

        /// <summary>Number of O + 2 H groups spawned in the mini simulation.</summary>
        public int Molecules = 2;

        public float MoleculeSeparationAngstrom = 6f;

        /// <summary>O-H spacing as a multiple of Alembic's r_min. Bonds form between 1.0 and 1.15.</summary>
        public float SpawnSpacingFactor = 1.08f;

        public float TemperatureK = 1000f;
        public float DtFemtoseconds = 1f;
        public float CutoffAngstrom = 10f;
        public int MaxSteps = 32;
        public int MinFormedBonds = 1;
        public ulong Seed = 1234UL;

        /// <summary>When false, an unconfirmed reaction still spawns its hazard.</summary>
        public bool RequireConfirmation;

        public HazardType Hazard = HazardType.Explosion;
        /// <summary>Authored footprint. Explosions appear at full radius, so no grow time.</summary>
        public float BaseRadiusMeters = 3f;
        public float HazardDurationSeconds = 1.5f;
        public float GrowSeconds = 0f;

        /// <summary>
        /// Footprint for a reagent quantity. Sub-linear on purpose, doubling the
        /// reagent must not double the radius. A spell cast has quantity 1.
        /// </summary>
        public float RadiusFor(float quantity)
        {
            return BaseRadiusMeters * math.sqrt(math.max(1f, quantity));
        }

        public static readonly ChemistryRecipe Fireball = new ChemistryRecipe();

        public static ChemistryRecipe ForSpell(SpellKind kind)
        {
            return Fireball;
        }
    }
}
