// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedFacingLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Facing
{
    /// <summary>
    /// Editor test tools for body facing and the character model. Puts a model, a prefab or an imported
    /// FBX, under the player as the body that turns, measures it, and draws the aim and body directions
    /// in the Scene view. Menu: GTG, Facing.
    /// </summary>
    public static class ManagedFacingLab
    {
        public const string ModelName = "Model";

        [MenuItem("GTG/Facing/Use Selected As Player Model", false, 300)]
        private static void UseSelectedMenu()
        {
            UseAsModel(Selection.activeGameObject);
        }

        [MenuItem("GTG/Facing/Use Selected As Player Model", true)]
        private static bool UseSelectedValidate()
        {
            return Selection.activeGameObject != null;
        }

        [MenuItem("GTG/Facing/Report Selected Model", false, 301)]
        private static void ReportSelectedMenu()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                GtgLabKit.Warn("Select a model in the Hierarchy or the Project window first.");
                return;
            }

            ManagedCharacter player = ManagedPlayerLab.FindPlayer();
            Vector3 feet = player != null ? player.FeetPosition : Vector3.zero;

            // A project asset has no bounds of its own, so an instance is measured and thrown away.
            if (EditorUtility.IsPersistent(selected))
            {
                GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(selected);
                probe.transform.position = feet;
                int found;
                string text = ManagedModelReport.Describe(probe, feet, out found);
                Object.DestroyImmediate(probe);
                GtgLabKit.Log(text + ManagedModelReport.DescribeImporter(selected));
                return;
            }

            int problems;
            GtgLabKit.Log(ManagedModelReport.Describe(selected, feet, out problems));
        }

        [MenuItem("GTG/Facing/Put Capsule Body Back", false, 302)]
        private static void CapsuleBackMenu()
        {
            ManagedCharacter player = ManagedPlayerLab.FindPlayer();
            if (player == null)
            {
                GtgLabKit.Warn("No player in the open scene.");
                return;
            }

            Transform model = player.transform.Find(ModelName);
            if (model != null)
            {
                Undo.DestroyObjectImmediate(model.gameObject);
            }

            Transform view = player.transform.Find(ManagedPlayerLab.BodyViewName);
            if (view != null)
            {
                Undo.RecordObject(view.gameObject, "Show body view");
                view.gameObject.SetActive(true);
                ManagedBodyFacing facing = player.GetComponent<ManagedBodyFacing>();
                if (facing != null)
                {
                    GtgLabKit.SetObject(facing, "_body", view);
                }
            }

            GtgLabKit.Finish(player.gameObject, "Capsule body is back.");
        }

        /// <summary>
        /// Puts a copy of <paramref name="source"/> under the player as the child that turns, replacing an
        /// earlier model, hiding the capsule body and logging the model report. The model origin must be
        /// at the soles and the model must face +Z. Returns the model, or null.
        /// </summary>
        public static Transform UseAsModel(GameObject source)
        {
            if (source == null)
            {
                GtgLabKit.Warn("Select a model first.");
                return null;
            }

            ManagedCharacter player = ManagedPlayerLab.EnsurePlayer();
            if (source == player.gameObject)
            {
                GtgLabKit.Warn("The player itself is selected. Select the model, a prefab or an imported FBX.");
                return null;
            }

            GameObject instance;
            if (!EditorUtility.IsPersistent(source) && source.transform.IsChildOf(player.transform))
            {
                instance = source;
            }
            else
            {
                Transform old = player.transform.Find(ModelName);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }

                instance = EditorUtility.IsPersistent(source)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(source, player.transform)
                    : Object.Instantiate(source, player.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Use model");
                instance.name = ModelName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
            }

            Transform[] all = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                all[i].gameObject.layer = GtgLabKit.LayerPlayer;
            }

            Transform view = player.transform.Find(ManagedPlayerLab.BodyViewName);
            if (view != null)
            {
                Undo.RecordObject(view.gameObject, "Hide body view");
                view.gameObject.SetActive(false);
            }

            ManagedBodyFacing facing = GtgLabKit.Ensure<ManagedBodyFacing>(player.gameObject);
            GtgLabKit.SetObject(facing, "_body", instance.transform);

            // The rig lists the body renderers it hides in first person. A list left from the capsule
            // is cleared so the rig collects the model at Play.
            ManagedCameraRig rig = Object.FindFirstObjectByType<ManagedCameraRig>();
            if (rig != null)
            {
                GtgLabKit.ClearObjectArray(rig, "_bodyRenderers");
            }

            int problems;
            string report = ManagedModelReport.Describe(instance, player.FeetPosition, out problems);
            string importer = ManagedModelReport.DescribeImporter(source);
            GtgLabKit.Finish(player.gameObject, "Model mounted under the player as Body of Body Facing. "
                + (problems == 0 ? "No problems found.\n" : problems + " problem(s) found.\n") + report + importer);
            return instance.transform;
        }

        // The aim is cyan and the body is green, both drawn from the soles. They agree in first person and
        // for a moment after a cast or a swing, and part when the body turns toward the way it walks.
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawDirections(ManagedBodyFacing facing, GizmoType type)
        {
            ManagedCharacter character = facing.GetComponent<ManagedCharacter>();
            if (character == null)
            {
                return;
            }

            Vector3 origin = character.FeetPosition + Vector3.up * 0.05f;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, Quaternion.Euler(0f, character.Yaw, 0f) * Vector3.forward * 1.6f);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(origin, Quaternion.Euler(0f, facing.BodyYaw, 0f) * Vector3.forward * 1.2f);
            }
        }
    }
}
