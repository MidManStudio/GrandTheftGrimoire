// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCrownTypes.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Crown
{
    /// <summary>What the player did to somebody the law protects. Each kind adds its own amount of Crown Heat.</summary>
    public enum ManagedCrimeKind
    {
        /// <summary>Hurt a merchant or a civilian.</summary>
        Assault = 0,

        /// <summary>Killed a merchant or a civilian.</summary>
        Murder = 1,

        /// <summary>Hurt a guard.</summary>
        AssaultOnGuard = 2,

        /// <summary>Killed a guard.</summary>
        MurderOfGuard = 3,
    }

    /// <summary>The Crown Heat tiers. The numbers are the ones the native decision receives.</summary>
    public static class ManagedCrownTier
    {
        /// <summary>No heat. Guards treat the player as everyone else does.</summary>
        public const int Clear = 0;

        /// <summary>Guards fight the player on sight.</summary>
        public const int Wanted = 1;

        /// <summary>Same as wanted for the decision today. Reserved for bounty hunters.</summary>
        public const int Hunted = 2;

        /// <summary>Same as wanted for the decision today. Reserved for the Crown's own knights.</summary>
        public const int Outlaw = 3;

        /// <summary>The highest tier the native decision accepts.</summary>
        public const int Max = 3;

        public static string Name(int tier)
        {
            switch (tier)
            {
                case Wanted: return "wanted";
                case Hunted: return "hunted";
                case Outlaw: return "outlaw";
                default: return "clear";
            }
        }
    }
}
