// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Editor test tools for glyphs. Places a preview glyph in the open scene, changes its seed, and prints the
    /// vertex and bake cost of every LOD. Menu: GTG, Glyphs.
    /// </summary>
    public static class ManagedGlyphLab
    {
        private const string PreviewName = "GTG Glyph Preview";

        [MenuItem("GTG/Glyphs/Ensure Preview At Scene View", false, 1400)]
        private static void EnsurePreviewMenu()
        {
            ManagedGlyphPreview preview = EnsurePreview();
            GtgLabKit.Finish(preview.gameObject, "Glyph preview ready. Move Charge in the Inspector to grow it. In Play mode, tick Auto Charge to "
                + "watch it draw on. The glyph faces the object's blue Z axis.");
        }

        [MenuItem("GTG/Glyphs/New Seed On Selected Preview", false, 1401)]
        private static void NewSeedMenu()
        {
            ManagedGlyphPreview preview = SelectedPreview("New Seed");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "New glyph seed");
            preview.Seed = preview.Seed + 1;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Glyph seed is now " + preview.Seed + ".");
        }

        [MenuItem("GTG/Glyphs/Toggle Auto Charge On Selected Preview", false, 1402)]
        private static void ToggleAutoMenu()
        {
            ManagedGlyphPreview preview = SelectedPreview("Toggle Auto Charge");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "Toggle glyph auto charge");
            preview.AutoCharge = !preview.AutoCharge;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Auto Charge is " + (preview.AutoCharge ? "on" : "off") + ". It only runs in Play mode.");
        }

        [MenuItem("GTG/Glyphs/Print Glyph Stats", false, 1410)]
        private static void PrintStatsMenu()
        {
            ManagedGlyphPreview preview = SelectedPreview("Print Glyph Stats");
            if (preview == null)
            {
                return;
            }

            GtgLabKit.Log(preview.DescribeStats());
        }

        internal static ManagedGlyphPreview EnsurePreview()
        {
            GameObject go = GtgLabKit.FindRoot(PreviewName);
            if (go == null)
            {
                go = GtgLabKit.NewObject(PreviewName, null);
                Vector3 pivot = GtgLabKit.ViewPivot();
                go.transform.position = new Vector3(pivot.x, 1.5f, pivot.z);
            }

            return GtgLabKit.Ensure<ManagedGlyphPreview>(go);
        }

        private static ManagedGlyphPreview SelectedPreview(string action)
        {
            GameObject selected = Selection.activeGameObject;
            ManagedGlyphPreview preview = selected != null ? selected.GetComponent<ManagedGlyphPreview>() : null;
            if (preview == null)
            {
                GtgLabKit.Warn(action + " needs a glyph preview selected. Use GTG, Glyphs, Ensure Preview At Scene View first.");
            }

            return preview;
        }
    }
}
