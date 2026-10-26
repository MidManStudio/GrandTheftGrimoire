// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedPlayerLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.CharacterController
{
    /// <summary>
    /// Editor test tools for the player. Builds a ground with four landmark pillars and a player whose
    /// pivot is at its feet, with a capsule body and a yellow nose that shows which way it faces.
    /// Menu: GTG, Player. Nothing here runs in a build.
    /// </summary>
    public static class ManagedPlayerLab
    {
        public const string GroundName = "GTG Test Ground";
        public const string LandmarksName = "GTG Test Landmarks";
        public const string PlayerName = "Player";
        public const string BodyViewName = "BodyView";

        [MenuItem("GTG/Player/Ensure Test Ground", false, 100)]
        private static void EnsureGroundMenu()
        {
            GameObject ground = EnsureGround();
            GtgLabKit.Finish(ground, "Test ground ready: a 100 x 100 m slab on layer Ground with four colored pillars on layer Wall "
                + "(red +Z, blue -Z, green +X, yellow -X).");
        }

        [MenuItem("GTG/Player/Ensure Player", false, 101)]
        private static void EnsurePlayerMenu()
        {
            ManagedCharacter player = EnsurePlayer();
            GtgLabKit.Finish(player.gameObject, "Player ready at the origin: pivot at the feet, 2 m capsule, layer Player. "
                + "Press Play: WASD moves, mouse looks, Space jumps.");
        }

        [MenuItem("GTG/Player/Select Player", false, 102)]
        private static void SelectPlayerMenu()
        {
            ManagedCharacter player = FindPlayer();
            if (player == null)
            {
                GtgLabKit.Warn("No ManagedCharacter in the open scene. Use GTG, Player, Ensure Player.");
                return;
            }

            Selection.activeGameObject = player.gameObject;
        }

        [MenuItem("GTG/Player/Print State (Play)", false, 103)]
        private static void PrintStateMenu()
        {
            if (!GtgLabKit.RequirePlaying("Print State"))
            {
                return;
            }

            ManagedCharacter player = FindPlayer();
            if (player == null)
            {
                GtgLabKit.Warn("No ManagedCharacter in the scene.");
                return;
            }

            GtgLabKit.Log("Player feet " + player.FeetPosition + ", yaw " + player.Yaw.ToString("F1") + ", pitch "
                + player.Pitch.ToString("F1") + ", grounded " + player.IsGrounded + ", features " + player.Features);
        }

        public static ManagedCharacter FindPlayer()
        {
            return Object.FindFirstObjectByType<ManagedCharacter>();
        }

        /// <summary>The 100 x 100 m test ground and the landmark pillars. Existing ones are kept.</summary>
        public static GameObject EnsureGround()
        {
            GtgLabKit.EnsureLayerNames();

            GameObject ground = GtgLabKit.FindRoot(GroundName);
            if (ground == null)
            {
                ground = GtgLabKit.Primitive(PrimitiveType.Cube, GroundName, null,
                    new Vector3(0f, -0.5f, 0f), new Vector3(100f, 1f, 100f), "Ground", new Color(0.36f, 0.43f, 0.31f, 1f), true);
                ground.layer = GtgLabKit.LayerGround;
            }

            if (GtgLabKit.FindRoot(LandmarksName) == null)
            {
                GameObject marks = GtgLabKit.NewObject(LandmarksName, null);
                Pillar(marks.transform, "Pillar +Z", new Vector3(0f, 1.5f, 12f), "PillarRed", new Color(0.85f, 0.2f, 0.2f, 1f));
                Pillar(marks.transform, "Pillar -Z", new Vector3(0f, 1.5f, -12f), "PillarBlue", new Color(0.2f, 0.35f, 0.85f, 1f));
                Pillar(marks.transform, "Pillar +X", new Vector3(12f, 1.5f, 0f), "PillarGreen", new Color(0.2f, 0.7f, 0.3f, 1f));
                Pillar(marks.transform, "Pillar -X", new Vector3(-12f, 1.5f, 0f), "PillarYellow", new Color(0.9f, 0.8f, 0.2f, 1f));
            }

            return ground;
        }

        private static void Pillar(Transform parent, string objectName, Vector3 position, string materialName, Color color)
        {
            GameObject pillar = GtgLabKit.Primitive(PrimitiveType.Cube, objectName, parent, position,
                new Vector3(1.5f, 3f, 1.5f), materialName, color, true);
            pillar.layer = GtgLabKit.LayerWall;
        }

        /// <summary>
        /// The player of the open scene, or a new one at the origin. A new player is an empty root at its
        /// feet with a 2 m capsule controller, layer Player, and a BodyView child with a nose on +Z.
        /// </summary>
        public static ManagedCharacter EnsurePlayer()
        {
            ManagedCharacter existing = FindPlayer();
            if (existing != null)
            {
                return existing;
            }

            GtgLabKit.EnsureLayerNames();
            EnsureGround();

            GameObject root = GtgLabKit.NewObject(PlayerName, null);
            root.layer = GtgLabKit.LayerPlayer;
            root.transform.position = new Vector3(0f, 0.05f, 0f);

            UnityEngine.CharacterController capsule = Undo.AddComponent<UnityEngine.CharacterController>(root);
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.stepOffset = 0.3f;

            ManagedCharacter character = Undo.AddComponent<ManagedCharacter>(root);

            GameObject view = GtgLabKit.Primitive(PrimitiveType.Capsule, BodyViewName, root.transform,
                new Vector3(0f, 1f, 0f), Vector3.one, "PlayerBody", new Color(0.3f, 0.5f, 0.9f, 1f), false);
            view.layer = GtgLabKit.LayerPlayer;
            GameObject nose = GtgLabKit.Primitive(PrimitiveType.Cube, "Nose", view.transform,
                new Vector3(0f, 0.35f, 0.5f), new Vector3(0.25f, 0.25f, 0.4f), "PlayerNose", new Color(0.95f, 0.85f, 0.2f, 1f), false);
            nose.layer = GtgLabKit.LayerPlayer;

            return character;
        }
    }
}
