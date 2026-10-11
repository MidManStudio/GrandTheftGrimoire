// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCrystal.cs"
// ============================================================================

namespace MidManStudio.Gtg.Managed.Economy
{
    /// <summary>The size of an SP crystal. A crystal is an item with a fixed SP value, like a soul gem.</summary>
    public enum ManagedCrystalSize : byte
    {
        Small = 0,
        Medium = 1,
        Large = 2,
    }

    /// <summary>
    /// The SP value of each crystal size. SP is both what powers a vessel and a currency, and a
    /// crystal is how it is carried. The values are placeholders. A medium crystal equals the
    /// SP of a full orb, so one fills an empty one.
    /// </summary>
    public static class ManagedCrystals
    {
        public const int SizeCount = 3;

        public static float SpOf(ManagedCrystalSize size)
        {
            switch (size)
            {
                case ManagedCrystalSize.Small:
                    return 25f;
                case ManagedCrystalSize.Medium:
                    return 100f;
                case ManagedCrystalSize.Large:
                    return 250f;
                default:
                    return 0f;
            }
        }

        public static string NameOf(ManagedCrystalSize size)
        {
            switch (size)
            {
                case ManagedCrystalSize.Small:
                    return "small";
                case ManagedCrystalSize.Medium:
                    return "medium";
                case ManagedCrystalSize.Large:
                    return "large";
                default:
                    return "unknown";
            }
        }
    }
}
