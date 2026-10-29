// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using MidManStudio.Gtg.Managed.Magic;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// Editor test tools for glyphs. Places a preview glyph in the open scene, changes its seed, style and atom look,
    /// prints the vertex and bake cost of every LOD, and puts the cast view on the player so a spell draws its glyph
    /// while it winds up. Menu: GTG, Glyphs.
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

        [MenuItem("GTG/Glyphs/Toggle Style On Selected Preview (Circle or Molecule)", false, 1403)]
        private static void ToggleStyleMenu()
        {
            ManagedGlyphPreview preview = SelectedPreview("Toggle Style");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "Toggle glyph style");
            bool toMolecule = preview.Style == ManagedGlyphStyle.Circle;
            preview.Style = toMolecule ? ManagedGlyphStyle.Molecule : ManagedGlyphStyle.Circle;

            // A molecule grows about twice as wide as a circle for the same radius, so the radius follows to keep the size.
            preview.Radius = preview.Radius * (toMolecule ? 0.5f : 2f);
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Glyph style is now " + preview.Style + ". Radius is " + preview.Radius.ToString("F2") + ".");
        }

        [MenuItem("GTG/Glyphs/Toggle Atom Look On Selected Preview (Letters or Balls)", false, 1404)]
        private static void ToggleAtomLookMenu()
        {
            ManagedGlyphPreview preview = SelectedPreview("Toggle Atom Look");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "Toggle glyph atom look");
            preview.AtomLook = preview.AtomLook == ManagedGlyphAtomLook.Letters ? ManagedGlyphAtomLook.Balls : ManagedGlyphAtomLook.Letters;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Atom look is now " + preview.AtomLook + ". It only changes a Molecule glyph.");
        }

        [MenuItem("GTG/Glyphs/Ensure Cast View On Player", false, 1405)]
        private static void EnsureCastViewMenu()
        {
            ManagedSpellCaster caster = ManagedMagicLab.EnsureCaster();
            ManagedGlyphCastView view = GtgLabKit.Ensure<ManagedGlyphCastView>(caster.gameObject);
            GtgLabKit.Finish(view.gameObject, "Cast view ready on the player, next to the spell caster. In Play: press F or left click and the spell "
                + "winds up for about half a second while its glyph draws on in front of the hand, then the shot leaves. Keys 1, 2 and 3 pick fireball "
                + "(circle), ice (molecule) and seeker (two wheel circle). Tune size and distance on the Glyph Cast View in the Inspector.");
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

        [MenuItem("GTG/Glyphs/Print Cast View State (Play)", false, 1411)]
        private static void PrintCastViewMenu()
        {
            if (!GtgLabKit.RequirePlaying("Print Cast View State"))
            {
                return;
            }

            ManagedGlyphCastView view = Object.FindFirstObjectByType<ManagedGlyphCastView>();
            if (view == null)
            {
                GtgLabKit.Warn("No Glyph Cast View in the scene. Use GTG, Glyphs, Ensure Cast View On Player.");
                return;
            }

            GtgLabKit.Log(view.Describe());
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
