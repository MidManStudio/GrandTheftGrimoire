// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedCrownLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Crown
{
    /// <summary>
    /// Editor test tools for Crown Heat. Adds a region and the HUD feeder to the open scene, and in Play
    /// mode raises, lowers and prints the heat of every region. Menu: GTG, Crown.
    /// </summary>
    public static class ManagedCrownLab
    {
        public const string RegionName = "GTG Crown Region";
        private const float HeatStep = 10f;

        [MenuItem("GTG/Crown/Ensure Region And HUD Feeder", false, 1200)]
        private static void EnsureRegionMenu()
        {
            ManagedCrownRegion region = EnsureRegion();
            GtgLabKit.Finish(region.gameObject, "Crown region ready: a 120 x 40 x 120 m box at the origin, with the feeder that moves the HUD crowns. "
                + "Wanted at 10 heat, hunted at 40, outlaw at 75.");
        }

        [MenuItem("GTG/Crown/Add 10 Heat To Every Region (Play)", false, 1210)]
        private static void AddHeatMenu()
        {
            Change("Add Heat", region => region.Add(HeatStep));
        }

        [MenuItem("GTG/Crown/Remove 10 Heat From Every Region (Play)", false, 1211)]
        private static void ReduceHeatMenu()
        {
            Change("Remove Heat", region => region.Reduce(HeatStep));
        }

        [MenuItem("GTG/Crown/Clear Heat (Play)", false, 1212)]
        private static void ClearMenu()
        {
            Change("Clear Heat", region => region.Clear());
        }

        /// <summary>A region box at the origin and a HUD feeder, added when missing.</summary>
        public static ManagedCrownRegion EnsureRegion()
        {
            ManagedCrownRegion region = Object.FindFirstObjectByType<ManagedCrownRegion>();
            if (region == null)
            {
                GameObject go = GtgLabKit.NewObject(RegionName, null);
                region = Undo.AddComponent<ManagedCrownRegion>(go);
            }

            if (Object.FindFirstObjectByType<ManagedCrownHudHeat>() == null)
            {
                Undo.AddComponent<ManagedCrownHudHeat>(region.gameObject);
            }

            return region;
        }

        private static void Change(string action, System.Action<ManagedCrownRegion> apply)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return;
            }

            int count = ManagedCrownRegion.Count;
            if (count == 0)
            {
                GtgLabKit.Warn("No Crown region is alive. Use GTG, Crown, Ensure Region And HUD Feeder before Play.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ManagedCrownRegion region = ManagedCrownRegion.Get(i);
                apply(region);
                GtgLabKit.Log(region.Describe());
            }
        }
    }
}
