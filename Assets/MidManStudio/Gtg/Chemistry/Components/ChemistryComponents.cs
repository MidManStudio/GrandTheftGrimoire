// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/chemistry.md, section "ChemistryComponents.cs"
// ============================================================================

using Unity.Entities;
using Unity.Mathematics;

namespace MidManStudio.Gtg.Chemistry
{
    public enum HazardType : byte
    {
        Explosion = 0,
        Gas = 1,
        Fire = 2,
    }

    /// <summary>
    /// The gameplay footprint of a reaction. This is the authoritative state:
    /// damage checks read <see cref="CurrentRadius"/>, and the visuals only
    /// mirror it.
    /// </summary>
    public struct ChemicalHazard : IComponentData
    {
        public float3 Origin;
        public float CurrentRadius;
        public float MaxRadius;
        public HazardType Type;

        /// <summary>World elapsed time when the hazard spawned, in seconds.</summary>
        public double SpawnTime;

        public float Duration;
        public float GrowDuration;

        /// <summary>True when Alembic reported bond formation for the recipe.</summary>
        public bool AlembicConfirmed;
    }
}
