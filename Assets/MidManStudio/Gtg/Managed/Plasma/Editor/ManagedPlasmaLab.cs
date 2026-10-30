// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/plasma.md, section "ManagedPlasmaLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using MidManStudio.Gtg.Managed.Magic;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Plasma
{
    /// <summary>
    /// Editor test tools for plasma rings. Places a preview ring in the open scene, changes its seed and palette, switches
    /// between additive and alpha blending, and prints what it is drawing. Also places the spell burst view and fires test
    /// bursts without casting. Menu: GTG, Plasma.
    /// </summary>
    public static class ManagedPlasmaLab
    {
        private const string PreviewName = "GTG Plasma Preview";
        private const string BurstViewName = "GTG Plasma Bursts";

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

        [MenuItem("GTG/Plasma/Ensure Burst View (Rings At Spell Impacts)", false, 1520)]
        private static void EnsureBurstViewMenu()
        {
            ManagedPlasmaBurstView view = EnsureBurstView();
            GtgLabKit.Finish(view.gameObject, "Plasma burst view ready. In Play mode a ring bursts at every spell impact, yours and NPC mages', "
                + "in the spell's palette. Use the Test Burst menus to see one without casting.");
        }

        [MenuItem("GTG/Plasma/Test Fireball Burst At Scene View (Play)", false, 1521)]
        private static void TestFireballBurstMenu()
        {
            TestBurst(ManagedSpellKind.Fireball);
        }

        [MenuItem("GTG/Plasma/Test Ice Burst At Scene View (Play)", false, 1522)]
        private static void TestIceBurstMenu()
        {
            TestBurst(ManagedSpellKind.Ice);
        }

        [MenuItem("GTG/Plasma/Test Seeker Burst At Scene View (Play)", false, 1523)]
        private static void TestSeekerBurstMenu()
        {
            TestBurst(ManagedSpellKind.Seeker);
        }

        [MenuItem("GTG/Plasma/Print Burst View State (Play)", false, 1524)]
        private static void PrintBurstStateMenu()
        {
            if (!GtgLabKit.RequirePlaying("Print Burst View State"))
            {
                return;
            }

            ManagedPlasmaBurstView view = FindBurstView();
            if (view == null)
            {
                GtgLabKit.Warn("No plasma burst view in the scene. Use GTG, Plasma, Ensure Burst View first, then press Play.");
                return;
            }

            GtgLabKit.Log(view.Describe());
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

        internal static ManagedPlasmaBurstView EnsureBurstView()
        {
            GameObject go = GtgLabKit.FindRoot(BurstViewName);
            if (go == null)
            {
                go = GtgLabKit.NewObject(BurstViewName, null);
            }

            return GtgLabKit.Ensure<ManagedPlasmaBurstView>(go);
        }

        private static ManagedPlasmaBurstView FindBurstView()
        {
            GameObject go = GtgLabKit.FindRoot(BurstViewName);
            return go != null ? go.GetComponent<ManagedPlasmaBurstView>() : null;
        }

        // Fires one burst at the point the Scene view looks at, lying flat on the ground, with no spell cast.
        private static void TestBurst(ManagedSpellKind kind)
        {
            if (!GtgLabKit.RequirePlaying("Test " + kind + " Burst"))
            {
                return;
            }

            ManagedPlasmaBurstView view = FindBurstView();
            if (view == null)
            {
                GtgLabKit.Warn("No plasma burst view in the scene. Use GTG, Plasma, Ensure Burst View first, then press Play.");
                return;
            }

            bool started = view.Spawn(kind, GtgLabKit.ViewPivot(), Vector3.up);
            GtgLabKit.Log(started ? kind + " burst started at the Scene view pivot. " + view.Describe() : kind + " burst was refused.");
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
