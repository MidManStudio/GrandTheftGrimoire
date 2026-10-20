// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedFavorMath.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Favor
{
    /// <summary>
    /// The arithmetic of favor, with no Unity or NPC types. Favor runs from minus <see cref="Max"/> to <see cref="Max"/>,
    /// and the native decision is sent the same value divided by Max, from -1 to 1. A group of people, such as the
    /// relevant NPCs of a town, has one value that is built from the favor of its members, see <see cref="Blend"/>.
    /// </summary>
    public static class ManagedFavorMath
    {
        /// <summary>The most favor an NPC or a group can hold, in either direction.</summary>
        public const float Max = 100f;

        /// <summary>The value held between -Max and Max. A value that is not a number counts as zero.</summary>
        public static float Clamp(float favor)
        {
            if (float.IsNaN(favor))
            {
                return 0f;
            }

            return favor < -Max ? -Max : (favor > Max ? Max : favor);
        }

        /// <summary>
        /// The value for the native decision: favor over <see cref="Max"/>, from -1 to 1. A value that is not a number
        /// is zero, which the decision reads as neutral.
        /// </summary>
        public static float Normalize(float favor)
        {
            return Clamp(favor) / Max;
        }

        /// <summary>
        /// The favor of a group made of named members and a crowd. The members count one each, and the crowd counts as
        /// <paramref name="crowdWeight"/> more members who all feel the crowd value. So a group with no members is the
        /// crowd value, a group with few members leans on the crowd, and a group with many members is their average.
        /// </summary>
        /// <param name="sum">The favor of the members added together.</param>
        /// <param name="count">How many members there are. Below zero counts as zero.</param>
        /// <param name="crowd">The favor of everybody else, the unnamed people of the group.</param>
        /// <param name="crowdWeight">How many members the crowd is worth. Not above zero, or not a number, means the crowd counts for nothing.</param>
        public static float Blend(float sum, int count, float crowd, float crowdWeight)
        {
            float members = count > 0 ? count : 0f;
            float weight = crowdWeight > 0f ? crowdWeight : 0f;
            float total = members + weight;
            if (!(total > 0f))
            {
                return Clamp(crowd);
            }

            float memberSum = float.IsNaN(sum) ? 0f : sum;
            return Clamp((memberSum + weight * Clamp(crowd)) / total);
        }
    }
}
