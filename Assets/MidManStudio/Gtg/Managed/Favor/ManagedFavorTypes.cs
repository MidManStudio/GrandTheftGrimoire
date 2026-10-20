// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedFavorTypes.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Crown;

namespace MidManStudio.Gtg.Managed.Favor
{
    /// <summary>
    /// The favor tiers. Favor runs from -100 to 100, and the tiers are the named bands of it. The Liked tier starts
    /// where the native decision starts to treat a shopkeeper or a civilian as friendly, see FRIENDLY_FAVOR in the
    /// npc-behavior crate. It is plain data with no Unity types.
    /// </summary>
    public static class ManagedFavorTier
    {
        /// <summary>At or below the Hated line.</summary>
        public const int Hated = -2;

        public const int Disliked = -1;

        /// <summary>Between the Disliked and Liked lines. Zero favor is here, and so is an NPC nobody asked.</summary>
        public const int Neutral = 0;

        /// <summary>Friendly. A shopkeeper or a civilian at this tier does not take the player for a threat.</summary>
        public const int Liked = 1;

        /// <summary>At or above the Adored line.</summary>
        public const int Adored = 2;

        /// <summary>Favor at or below which the tier is Hated. Placeholder value.</summary>
        public const float HatedAt = -60f;

        /// <summary>Favor at or below which the tier is Disliked. Placeholder value.</summary>
        public const float DislikedAt = -20f;

        /// <summary>Favor at or above which the tier is Liked. Matches FRIENDLY_FAVOR times 100.</summary>
        public const float LikedAt = 20f;

        /// <summary>Favor at or above which the tier is Adored. Placeholder value.</summary>
        public const float AdoredAt = 60f;

        /// <summary>The tier of a favor value. A value that is not a number is Neutral.</summary>
        public static int For(float favor)
        {
            if (float.IsNaN(favor))
            {
                return Neutral;
            }

            if (favor <= HatedAt)
            {
                return Hated;
            }

            if (favor <= DislikedAt)
            {
                return Disliked;
            }

            if (favor >= AdoredAt)
            {
                return Adored;
            }

            return favor >= LikedAt ? Liked : Neutral;
        }

        public static string Name(int tier)
        {
            switch (tier)
            {
                case Hated: return "hated";
                case Disliked: return "disliked";
                case Liked: return "liked";
                case Adored: return "adored";
                default: return "neutral";
            }
        }
    }

    /// <summary>How hard each kind of violence weighs on favor. It scales the penalties set on the NPC and the region.</summary>
    public static class ManagedFavorSeverity
    {
        /// <summary>Hurting somebody, a civilian, a merchant or a guard. Placeholder value.</summary>
        public const float Assault = 1f;

        /// <summary>Killing somebody. Placeholder value.</summary>
        public const float Murder = 3f;

        public static float Of(ManagedCrimeKind kind)
        {
            return kind == ManagedCrimeKind.Murder || kind == ManagedCrimeKind.MurderOfGuard ? Murder : Assault;
        }
    }
}
