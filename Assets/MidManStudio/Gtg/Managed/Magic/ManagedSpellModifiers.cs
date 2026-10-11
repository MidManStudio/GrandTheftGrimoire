// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedSpellModifiers.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Magic
{
    /// <summary>
    /// Scales a caster applies on top of the spell tables. All of them are neutral by default.
    /// The caster owns one and hands it to <see cref="ManagedSpellDefinition.CreateSpawn"/>, so
    /// anything that wants to change how spells behave, a cheat today and an item or an effect
    /// later, sets a field here and no spell, flight or vessel code needs to know who did it.
    /// </summary>
    public sealed class ManagedSpellModifiers
    {
        /// <summary>Multiplies the cooldown after a cast. Zero casts as fast as the button allows.</summary>
        public float CooldownScale = 1f;

        /// <summary>Multiplies the SP a shot starts with, and so its range. The burn rate does not change.</summary>
        public float RangeScale = 1f;

        /// <summary>Multiplies the SP a turn costs. Zero turns for free.</summary>
        public float SteerCostScale = 1f;

        /// <summary>Every cast finds its vessel full and does not use it up.</summary>
        public bool InfiniteVessels;

        public bool IsNeutral
        {
            get { return CooldownScale == 1f && RangeScale == 1f && SteerCostScale == 1f && !InfiniteVessels; }
        }

        public void Reset()
        {
            CooldownScale = 1f;
            RangeScale = 1f;
            SteerCostScale = 1f;
            InfiniteVessels = false;
        }
    }
}
