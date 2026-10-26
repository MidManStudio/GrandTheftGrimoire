// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedCameraLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Camera
{
    /// <summary>
    /// Editor test tools for the camera rig. Adds the rig and a main camera to the open scene and, in
    /// Play mode, switches the view and prints what the rig is driving. Menu: GTG, Camera.
    /// </summary>
    public static class ManagedCameraLab
    {
        public const string RigName = "GTG Camera Rig";

        [MenuItem("GTG/Camera/Ensure Rig And Main Camera", false, 200)]
        private static void EnsureRigMenu()
        {
            ManagedCameraRig rig = EnsureRig();
            GtgLabKit.Finish(rig.gameObject, "Camera rig ready. With no cameras assigned it drives the main camera: "
                + "third person at 3.5 m, V switches to first person.");
        }

        [MenuItem("GTG/Camera/Toggle First Person (Play)", false, 201)]
        private static void ToggleMenu()
        {
            if (!GtgLabKit.RequirePlaying("Toggle First Person"))
            {
                return;
            }

            ManagedCameraRig rig = Object.FindFirstObjectByType<ManagedCameraRig>();
            if (rig == null)
            {
                GtgLabKit.Warn("No ManagedCameraRig in the scene.");
                return;
            }

            rig.ToggleFirstPerson();
            GtgLabKit.Log("View is now " + (rig.IsFirstPerson ? "first person." : "third person."));
        }

        [MenuItem("GTG/Camera/Print Wiring", false, 202)]
        private static void PrintWiringMenu()
        {
            ManagedCameraRig rig = Object.FindFirstObjectByType<ManagedCameraRig>();
            if (rig == null)
            {
                GtgLabKit.Warn("No ManagedCameraRig in the open scene. Use GTG, Camera, Ensure Rig And Main Camera.");
                return;
            }

            UnityEngine.Camera main = UnityEngine.Camera.main;
            GtgLabKit.Log("Rig on " + rig.name + ". Main camera: " + (main != null ? main.name : "none (tag a camera MainCamera)")
                + ". View: " + (Application.isPlaying ? (rig.IsFirstPerson ? "first person" : "third person") : "set at Play")
                + ". The Cinemachine cameras, if any, are assigned on the rig and get their Follow pointed at its target at Play.");
        }

        /// <summary>The rig of the open scene, or a new one, and a main camera when the scene has none.</summary>
        public static ManagedCameraRig EnsureRig()
        {
            ManagedCameraRig rig = Object.FindFirstObjectByType<ManagedCameraRig>();
            if (rig == null)
            {
                GameObject go = GtgLabKit.NewObject(RigName, null);
                rig = Undo.AddComponent<ManagedCameraRig>(go);
            }

            if (UnityEngine.Camera.main == null)
            {
                GameObject cam = GtgLabKit.NewObject("Main Camera", null);
                cam.tag = "MainCamera";
                cam.transform.position = new Vector3(0f, 2f, -5f);
                Undo.AddComponent<UnityEngine.Camera>(cam);
                Undo.AddComponent<AudioListener>(cam);
            }

            return rig;
        }
    }
}
