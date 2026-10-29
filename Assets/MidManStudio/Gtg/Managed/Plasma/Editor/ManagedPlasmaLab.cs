// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Editor test tools for plasma rings. Places a preview ring in the open scene, changes its seed and palette, switches
    /// between additive and alpha blending, and prints what it is drawing. Menu: GTG, Plasma.
    /// </summary>
    public static class ManagedPlasmaLab
    {
        private const string PreviewName = "GTG Plasma Preview";

        [MenuItem("GTG/Plasma/Ensure Preview At Scene View", false, 1500)]
        private static void EnsurePreviewMenu()
        {
            ManagedPlasmaPreview preview = EnsurePreview();
            GtgLabKit.Finish(preview.gameObject, "Plasma preview ready. It animates in Play mode. Change Turbulence, Streaks, Ring Radius and Band Width "
                + "in the Inspector. The ring faces the object's blue Z axis, or tick Face Camera.");
        }

        [MenuItem("GTG/Plasma/New Seed On Selected Preview", false, 1501)]
        private static void NewSeedMenu()
        {
            ManagedPlasmaPreview preview = SelectedPreview("New Seed");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "New plasma seed");
            preview.Seed = preview.Seed + 1;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Plasma seed is now " + preview.Seed + ".");
        }

        [MenuItem("GTG/Plasma/Cycle Palette On Selected Preview (Fire, Water, Arcane)", false, 1502)]
        private static void CyclePaletteMenu()
        {
            ManagedPlasmaPreview preview = SelectedPreview("Cycle Palette");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "Cycle plasma palette");
            preview.CyclePalette();
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Plasma palette is now " + preview.Palette + ".");
        }

        [MenuItem("GTG/Plasma/Toggle Additive Blend On Selected Preview", false, 1503)]
        private static void ToggleAdditiveMenu()
        {
            ManagedPlasmaPreview preview = SelectedPreview("Toggle Additive Blend");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "Toggle plasma blend");
            preview.Additive = !preview.Additive;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Plasma blend is now " + (preview.Additive ? "additive" : "alpha") + ".");
        }

        [MenuItem("GTG/Plasma/Print Plasma Stats", false, 1510)]
        private static void PrintStatsMenu()
        {
            ManagedPlasmaPreview preview = SelectedPreview("Print Plasma Stats");
            if (preview == null)
            {
                return;
            }

            GtgLabKit.Log(preview.DescribeStats());
        }

        internal static ManagedPlasmaPreview EnsurePreview()
        {
            GameObject go = GtgLabKit.FindRoot(PreviewName);
            if (go == null)
            {
                go = GtgLabKit.NewObject(PreviewName, null);
                Vector3 pivot = GtgLabKit.ViewPivot();
                go.transform.position = new Vector3(pivot.x, 1.5f, pivot.z);
            }

            return GtgLabKit.Ensure<ManagedPlasmaPreview>(go);
        }

        private static ManagedPlasmaPreview SelectedPreview(string action)
        {
            GameObject selected = Selection.activeGameObject;
            ManagedPlasmaPreview preview = selected != null ? selected.GetComponent<ManagedPlasmaPreview>() : null;
            if (preview == null)
            {
                GtgLabKit.Warn(action + " needs a plasma preview selected. Use GTG, Plasma, Ensure Preview At Scene View first.");
            }

            return preview;
        }
    }
}
