// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedFavorLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Favor
{
    /// <summary>
    /// Editor test tools for favor. Adds a region to the open scene, gives the selected NPCs a favor
    /// meter, and in Play mode moves the crowd favor of every region. Menu: GTG, Favor.
    /// </summary>
    public static class ManagedFavorLab
    {
        public const string RegionName = "GTG Favor Region";
        private const float CrowdStep = 10f;

        [MenuItem("GTG/Favor/Ensure Region", false, 1300)]
        private static void EnsureRegionMenu()
        {
            ManagedFavorRegion region = EnsureRegion();
            GtgLabKit.Finish(region.gameObject, "Favor region ready: a 120 x 40 x 120 m box at the origin. Add favor to the NPCs with GTG, Favor, Add Favor To Selected NPCs.");
        }

        [MenuItem("GTG/Favor/Add Favor To Selected NPCs", false, 1301)]
        private static void AddFavorMenu()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                GtgLabKit.Warn("Select one or more NPCs first.");
                return;
            }

            for (int i = 0; i < selected.Length; i++)
            {
                GtgLabKit.Ensure<ManagedNpcFavor>(selected[i]);
            }

            GtgLabKit.Finish(selected[0], "Favor added to " + selected.Length + " object(s). An NPC that likes the player (20 or more) stops treating it as a threat.");
        }

        [MenuItem("GTG/Favor/Add 10 Crowd Favor (Play)", false, 1310)]
        private static void AddCrowdMenu()
        {
            Change("Add Crowd Favor", region => region.AddCrowd(CrowdStep));
        }

        [MenuItem("GTG/Favor/Remove 10 Crowd Favor (Play)", false, 1311)]
        private static void RemoveCrowdMenu()
        {
            Change("Remove Crowd Favor", region => region.AddCrowd(-CrowdStep));
        }

        /// <summary>A favor region box at the origin, added when missing.</summary>
        public static ManagedFavorRegion EnsureRegion()
        {
            ManagedFavorRegion region = Object.FindFirstObjectByType<ManagedFavorRegion>();
            if (region == null)
            {
                GameObject go = GtgLabKit.NewObject(RegionName, null);
                region = Undo.AddComponent<ManagedFavorRegion>(go);
            }

            return region;
        }

        private static void Change(string action, System.Action<ManagedFavorRegion> apply)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return;
            }

            int count = ManagedFavorRegion.Count;
            if (count == 0)
            {
                GtgLabKit.Warn("No favor region is alive. Use GTG, Favor, Ensure Region before Play.");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ManagedFavorRegion region = ManagedFavorRegion.Get(i);
                apply(region);
                GtgLabKit.Log(region.Describe());
            }
        }
    }
}
