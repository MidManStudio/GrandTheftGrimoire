// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedBuildingLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Editor test tools for station placement. Adds build mode to the open scene with the Ground layer
    /// as the only floor and five of each station in the tray, and in Play mode opens and closes build
    /// mode. Menu: GTG, Building.
    /// </summary>
    public static class ManagedBuildingLab
    {
        public const string HostName = "GTG Build Mode";
        private const int StockPerStation = 5;

        [MenuItem("GTG/Building/Ensure Build Mode", false, 900)]
        private static void EnsureBuildModeMenu()
        {
            ManagedBuildMode mode = EnsureBuildMode();
            GtgLabKit.Finish(mode.gameObject, "Build mode ready: " + StockPerStation + " crafting tables and " + StockPerStation
                + " butchery stations in the tray, both blueprints unlocked, the Ground layer is the only floor. "
                + "In Play press B, then drag a station from the tray onto the ground.");
        }

        [MenuItem("GTG/Building/Toggle Build Mode (Play)", false, 910)]
        private static void ToggleMenu()
        {
            if (!GtgLabKit.RequirePlaying("Toggle Build Mode"))
            {
                return;
            }

            ManagedBuildMode mode = Object.FindFirstObjectByType<ManagedBuildMode>();
            if (mode == null)
            {
                GtgLabKit.Warn("No ManagedBuildMode in the scene. Use GTG, Building, Ensure Build Mode.");
                return;
            }

            mode.SetBuildMode(!mode.BuildModeActive);
            GtgLabKit.Log("Build mode is " + (mode.BuildModeActive ? "open." : "closed."));
        }

        /// <summary>
        /// Build mode on its own object, with the tray stocked and the ground mask set to the Ground
        /// layer, which leaves the player layer out as the setup notes ask.
        /// </summary>
        public static ManagedBuildMode EnsureBuildMode()
        {
            ManagedBuildMode existing = Object.FindFirstObjectByType<ManagedBuildMode>();
            if (existing != null)
            {
                return existing;
            }

            GtgLabKit.EnsureLayerNames();
            GameObject host = GtgLabKit.NewObject(HostName, null);
            ManagedBuildMode mode = Undo.AddComponent<ManagedBuildMode>(host);

            GtgLabKit.SetInt(mode, "_groundMask", GtgLabKit.GroundMask);
            SetStock(mode);
            return mode;
        }

        // A component added from code starts with an empty tray, so the tray is written out here.
        private static void SetStock(ManagedBuildMode mode)
        {
            string[] ids =
            {
                ManagedBuildableDefinition.CraftingTable.Id,
                ManagedBuildableDefinition.ButcheryStation.Id,
            };

            SerializedObject so;
            SerializedProperty stock = GtgLabKit.Find(mode, "_startingStock", out so);
            if (stock != null)
            {
                stock.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                {
                    SerializedProperty entry = stock.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("BuildableId").stringValue = ids[i];
                    entry.FindPropertyRelative("Count").intValue = StockPerStation;
                }

                so.ApplyModifiedProperties();
            }

            SerializedProperty blueprints = GtgLabKit.Find(mode, "_startingBlueprints", out so);
            if (blueprints != null)
            {
                blueprints.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                {
                    blueprints.GetArrayElementAtIndex(i).stringValue = ids[i];
                }

                so.ApplyModifiedProperties();
            }
        }
    }
}
