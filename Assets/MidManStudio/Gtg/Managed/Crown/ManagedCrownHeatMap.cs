// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCrownHeatMap.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Crown
{
    /// <summary>
    /// Turns the Crown Heat of a region into the nine sections the HUD shows, three crowns of three. Each
    /// tier owns one crown: wanted lights the first, hunted the second, outlaw the third. Inside a tier the
    /// sections fill in thirds as the heat climbs from the tier's threshold to the next one. It is plain
    /// arithmetic with no Unity or NPC types.
    /// </summary>
    public static class ManagedCrownHeatMap
    {
        /// <summary>Sections in one crown, and so the sections one tier can light.</summary>
        public const int SectionsPerTier = 3;

        /// <summary>Every section of every crown, one crown for each tier above clear.</summary>
        public const int MaxSections = SectionsPerTier * ManagedCrownTier.Max;

        /// <summary>
        /// The sections to light, 0 through <see cref="MaxSections"/>. A tier of clear or less is 0, because
        /// the HUD hides the crowns then. From the wanted tier on, at least one section is lit, so the crowns
        /// show as soon as the player is wanted. The thirds of a tier are measured from its own threshold to
        /// the threshold of the next tier, and the outlaw tier up to the most heat the region holds. A tier
        /// above the highest counts as the highest.
        /// </summary>
        public static int Sections(int tier, float heat, float wantedAt, float huntedAt, float outlawAt, float maxHeat)
        {
            if (tier < ManagedCrownTier.Wanted || float.IsNaN(heat))
            {
                return 0;
            }

            if (tier > ManagedCrownTier.Max)
            {
                tier = ManagedCrownTier.Max;
            }

            float low;
            float high;
            if (tier == ManagedCrownTier.Wanted)
            {
                low = wantedAt;
                high = huntedAt;
            }
            else if (tier == ManagedCrownTier.Hunted)
            {
                low = huntedAt;
                high = outlawAt;
            }
            else
            {
                low = outlawAt;
                high = maxHeat;
            }

            // Thirds are compared by multiplying, not dividing, so a heat exactly on a third falls in the upper one.
            float width = high - low;
            float offset = heat - low;
            int inside;
            if (!(width > 0f) || offset * 3f >= width * 2f)
            {
                inside = 3;
            }
            else if (offset * 3f >= width)
            {
                inside = 2;
            }
            else
            {
                inside = 1;
            }

            return (tier - 1) * SectionsPerTier + inside;
        }
    }
}
