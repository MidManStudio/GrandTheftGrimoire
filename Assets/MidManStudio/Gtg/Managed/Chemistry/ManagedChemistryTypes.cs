// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedChemistryTypes.cs"
// ============================================================================

using UnityEngine;

namespace MidManStudio.Gtg.Managed.Chemistry
{
    public enum ManagedHazardType : byte
    {
        Explosion = 0,
        Gas = 1,
        Fire = 2,
        Freeze = 3,
    }

    /// <summary>
    /// The gameplay footprint of a reaction. This is the authoritative state: damage
    /// checks read <see cref="CurrentRadius"/>, and the visuals only mirror it.
    /// </summary>
    public sealed class ManagedChemicalHazard
    {
        public Vector3 Origin;
        public float CurrentRadius;
        public float MaxRadius;
        public ManagedHazardType Type;

        /// <summary>Scaled game time when the hazard spawned, in seconds.</summary>
        public double SpawnTime;

        public float Duration;
        public float GrowDuration;

        /// <summary>True when Alembic confirmed the reaction for the recipe.</summary>
        public bool AlembicConfirmed;
    }

    /// <summary>Outcome of one Alembic run.</summary>
    public struct ManagedReactionResult
    {
        public bool NativeUsed;
        public bool Confirmed;
        public int FormedBonds;
        public int BrokenBonds;
        public int Steps;
        public float TemperatureK;

        public ManagedReactionResult(
            bool nativeUsed, bool confirmed, int formedBonds, int brokenBonds, int steps, float temperatureK)
        {
            NativeUsed = nativeUsed;
            Confirmed = confirmed;
            FormedBonds = formedBonds;
            BrokenBonds = brokenBonds;
            Steps = steps;
            TemperatureK = temperatureK;
        }
    }
}
