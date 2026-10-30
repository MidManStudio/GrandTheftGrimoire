// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/specter.md, section "ManagedSpecterLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using MidManStudio.Gtg.Managed.Plasma;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Specter
{
    /// <summary>
    /// Editor test tools for specter butterflies. Places a single butterfly preview, the code-moved swarm (variant A) and the
    /// Particle System one (variant B) in the open scene, side by side so the two can be compared, changes their palette and
    /// blend mode, releases bursts in Play mode and prints what each is drawing. Menu: GTG, Specter.
    /// </summary>
    public static class ManagedSpecterLab
    {
        private const string PreviewName = "GTG Specter Preview";
        private const string SwarmName = "GTG Specter Swarm (code-moved)";
        private const string ParticlesName = "GTG Specter Particles (Particle System)";
        private const int BurstSize = 12;

        [MenuItem("GTG/Specter/Ensure Single Butterfly Preview At Scene View", false, 1600)]
        private static void EnsurePreviewMenu()
        {
            ManagedSpecterPreview preview = EnsurePreview();
            GtgLabKit.Finish(preview.gameObject, "Specter preview ready. Its pattern runs through a whole life in Play mode. Untick Animate and move the Life "
                + "slider to hold one moment. Wobble, Glow, Churn and Reach shape it. It looks flat without Bloom: turn on HDR in the URP asset and add a Bloom override to a Volume.");
        }

        [MenuItem("GTG/Specter/New Seed On Selected Preview", false, 1601)]
        private static void NewSeedMenu()
        {
            ManagedSpecterPreview preview = SelectedPreview("New Seed");
            if (preview == null)
            {
                return;
            }

            Undo.RecordObject(preview, "New specter seed");
            preview.Seed = preview.Seed + 1;
            EditorUtility.SetDirty(preview);
            GtgLabKit.Log("Specter seed is now " + preview.Seed + ".");
        }

        [MenuItem("GTG/Specter/Cycle Palette On Selected (Fire, Water, Arcane)", false, 1602)]
        private static void CyclePaletteMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (!HasAnySpecter(selected))
            {
                GtgLabKit.Warn("Cycle Palette needs a specter preview, swarm or particles object selected. Use one of the Ensure menus first.");
                return;
            }

            ManagedSpecterPreview preview = selected.GetComponent<ManagedSpecterPreview>();
            ManagedSpecterSwarm swarm = selected.GetComponent<ManagedSpecterSwarm>();
            ManagedSpecterParticles particles = selected.GetComponent<ManagedSpecterParticles>();
            ManagedPlasmaPalette now = ManagedPlasmaPalette.Arcane;
            if (preview != null)
            {
                Undo.RecordObject(preview, "Cycle specter palette");
                preview.CyclePalette();
                EditorUtility.SetDirty(preview);
                now = preview.Palette;
            }

            if (swarm != null)
            {
                Undo.RecordObject(swarm, "Cycle specter palette");
                swarm.CyclePalette();
                EditorUtility.SetDirty(swarm);
                now = swarm.Palette;
            }

            if (particles != null)
            {
                Undo.RecordObject(particles, "Cycle specter palette");
                particles.CyclePalette();
                EditorUtility.SetDirty(particles);
                now = particles.Palette;
            }

            GtgLabKit.Log("Specter palette is now " + now + ". A swarm applies it to butterflies released after this, the others at once.");
        }

        [MenuItem("GTG/Specter/Toggle Additive Blend On Selected", false, 1603)]
        private static void ToggleAdditiveMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (!HasAnySpecter(selected))
            {
                GtgLabKit.Warn("Toggle Additive Blend needs a specter preview, swarm or particles object selected. Use one of the Ensure menus first.");
                return;
            }

            bool additive = true;
            ManagedSpecterPreview preview = selected.GetComponent<ManagedSpecterPreview>();
            ManagedSpecterSwarm swarm = selected.GetComponent<ManagedSpecterSwarm>();
            ManagedSpecterParticles particles = selected.GetComponent<ManagedSpecterParticles>();
            if (preview != null)
            {
                Undo.RecordObject(preview, "Toggle specter blend");
                preview.Additive = !preview.Additive;
                EditorUtility.SetDirty(preview);
                additive = preview.Additive;
            }

            if (swarm != null)
            {
                Undo.RecordObject(swarm, "Toggle specter blend");
                swarm.Additive = !swarm.Additive;
                EditorUtility.SetDirty(swarm);
                additive = swarm.Additive;
            }

            if (particles != null)
            {
                Undo.RecordObject(particles, "Toggle specter blend");
                particles.Additive = !particles.Additive;
                EditorUtility.SetDirty(particles);
                additive = particles.Additive;
            }

            GtgLabKit.Log("Specter blend is now " + (additive ? "additive" : "alpha") + ".");
        }

        [MenuItem("GTG/Specter/Ensure Swarm At Scene View (A: Code-Moved Quads)", false, 1610)]
        private static void EnsureSwarmMenu()
        {
            ManagedSpecterSwarm swarm = EnsureSwarm(0f);
            GtgLabKit.Finish(swarm.gameObject, "Specter swarm (variant A, code-moved quads) ready. In Play mode it releases 3 butterflies a second, "
                + "and GTG, Specter, Burst Selected (Play) releases " + BurstSize + " more. Tick Force Draw Mesh in the Inspector to test the fallback path.");
        }

        [MenuItem("GTG/Specter/Ensure Particles At Scene View (B: Particle System)", false, 1611)]
        private static void EnsureParticlesMenu()
        {
            ManagedSpecterParticles particles = EnsureParticles(0f);
            GtgLabKit.Finish(particles.gameObject, "Specter particles (variant B, Particle System) ready. In Play mode it releases 3 butterflies a second, "
                + "and GTG, Specter, Burst Selected (Play) releases " + BurstSize + " more. The Particle System itself is a child that exists only in Play mode.");
        }

        [MenuItem("GTG/Specter/Ensure Both Side By Side (A Left, B Right)", false, 1612)]
        private static void EnsureBothMenu()
        {
            ManagedSpecterSwarm swarm = EnsureSwarm(-1.6f);
            ManagedSpecterParticles particles = EnsureParticles(1.6f);
            GtgLabKit.Finish(swarm.gameObject, "Both variants placed 3.2 metres apart: the code-moved swarm (A) on the left, the Particle System one (B) on the right, "
                + "same palette and settings. Press Play, then use GTG, Specter, Burst Selected (Play) with both selected or nothing selected. "
                + "Compare the look, the motion and the batch count in the Game view's Stats overlay.");
            Selection.objects = new Object[] { swarm.gameObject, particles.gameObject };
        }

        [MenuItem("GTG/Specter/Burst Selected, Or Both If Nothing Selected (Play)", false, 1620)]
        private static void BurstMenu()
        {
            if (!GtgLabKit.RequirePlaying("Burst"))
            {
                return;
            }

            // Every selected object that carries a swarm or particles bursts. With none selected, the first of each in the scene does.
            string report = "Burst of " + BurstSize + ".";
            int bursts = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                ManagedSpecterSwarm selectedSwarm = selected.GetComponent<ManagedSpecterSwarm>();
                if (selectedSwarm != null)
                {
                    report += " Swarm took " + selectedSwarm.Burst(BurstSize) + ".";
                    bursts++;
                }

                ManagedSpecterParticles selectedParticles = selected.GetComponent<ManagedSpecterParticles>();
                if (selectedParticles != null)
                {
                    report += " Particles took " + selectedParticles.Burst(BurstSize) + ".";
                    bursts++;
                }
            }

            if (bursts == 0)
            {
                ManagedSpecterSwarm swarm = Object.FindFirstObjectByType<ManagedSpecterSwarm>();
                ManagedSpecterParticles particles = Object.FindFirstObjectByType<ManagedSpecterParticles>();
                if (swarm == null && particles == null)
                {
                    GtgLabKit.Warn("No specter swarm or particles in the scene. Use one of the Ensure menus first, then press Play.");
                    return;
                }

                if (swarm != null)
                {
                    report += " Swarm took " + swarm.Burst(BurstSize) + ".";
                }

                if (particles != null)
                {
                    report += " Particles took " + particles.Burst(BurstSize) + ".";
                }
            }

            GtgLabKit.Log(report);
        }

        [MenuItem("GTG/Specter/Print Specter State", false, 1630)]
        private static void PrintStateMenu()
        {
            bool any = false;
            ManagedSpecterPreview preview = Object.FindFirstObjectByType<ManagedSpecterPreview>();
            if (preview != null)
            {
                GtgLabKit.Log(preview.DescribeStats());
                any = true;
            }

            ManagedSpecterSwarm swarm = Object.FindFirstObjectByType<ManagedSpecterSwarm>();
            if (swarm != null)
            {
                GtgLabKit.Log(swarm.Describe());
                any = true;
            }

            ManagedSpecterParticles particles = Object.FindFirstObjectByType<ManagedSpecterParticles>();
            if (particles != null)
            {
                GtgLabKit.Log(particles.Describe());
                any = true;
            }

            if (!any)
            {
                GtgLabKit.Warn("No specter objects in the scene. Use one of the GTG, Specter, Ensure menus first.");
            }
        }

        internal static ManagedSpecterPreview EnsurePreview()
        {
            GameObject go = GtgLabKit.FindRoot(PreviewName);
            if (go == null)
            {
                go = GtgLabKit.NewObject(PreviewName, null);
                Vector3 pivot = GtgLabKit.ViewPivot();
                go.transform.position = new Vector3(pivot.x, 1.5f, pivot.z);
            }

            return GtgLabKit.Ensure<ManagedSpecterPreview>(go);
        }

        // sideways moves it along the world X axis from the Scene view's pivot, so the two variants can stand side by side.
        internal static ManagedSpecterSwarm EnsureSwarm(float sideways)
        {
            GameObject go = GtgLabKit.FindRoot(SwarmName);
            bool created = go == null;
            if (created)
            {
                go = GtgLabKit.NewObject(SwarmName, null);
                Vector3 pivot = GtgLabKit.ViewPivot();
                go.transform.position = new Vector3(pivot.x + sideways, 1.2f, pivot.z);
            }

            ManagedSpecterSwarm swarm = GtgLabKit.Ensure<ManagedSpecterSwarm>(go);
            if (created)
            {
                GtgLabKit.SetFloat(swarm, "_autoPerSecond", 3f);
            }

            return swarm;
        }

        internal static ManagedSpecterParticles EnsureParticles(float sideways)
        {
            GameObject go = GtgLabKit.FindRoot(ParticlesName);
            bool created = go == null;
            if (created)
            {
                go = GtgLabKit.NewObject(ParticlesName, null);
                Vector3 pivot = GtgLabKit.ViewPivot();
                go.transform.position = new Vector3(pivot.x + sideways, 1.2f, pivot.z);
            }

            ManagedSpecterParticles particles = GtgLabKit.Ensure<ManagedSpecterParticles>(go);
            if (created)
            {
                GtgLabKit.SetFloat(particles, "_autoPerSecond", 3f);
            }

            return particles;
        }

        private static bool HasAnySpecter(GameObject go)
        {
            return go != null
                && (go.GetComponent<ManagedSpecterPreview>() != null
                    || go.GetComponent<ManagedSpecterSwarm>() != null
                    || go.GetComponent<ManagedSpecterParticles>() != null);
        }

        private static ManagedSpecterPreview SelectedPreview(string action)
        {
            GameObject selected = Selection.activeGameObject;
            ManagedSpecterPreview preview = selected != null ? selected.GetComponent<ManagedSpecterPreview>() : null;
            if (preview == null)
            {
                GtgLabKit.Warn(action + " needs a specter preview selected. Use GTG, Specter, Ensure Single Butterfly Preview first.");
            }

            return preview;
        }
    }
}
