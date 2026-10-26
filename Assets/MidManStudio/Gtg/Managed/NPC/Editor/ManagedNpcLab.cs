// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedNpcLab.cs"
// ============================================================================

using System;
using System.Reflection;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Lab;
using MidManStudio.Gtg.NPC.Components;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Editor test tools for NPCs. Spawns a merchant, a melee fighter, an archer or a fighting pair at the
    /// point the Scene view looks at, and adds the NavMesh surface they walk on. Every NPC has its pivot at
    /// its feet, so the component defaults for heights are right and nothing is overridden. Open GTG, NPC
    /// Debugger in Play mode to read what each one does. Menu: GTG, NPC.
    /// </summary>
    public static class ManagedNpcLab
    {
        private const string SurfaceTypeName = "Unity.AI.Navigation.NavMeshSurface";

        [MenuItem("GTG/NPC/Spawn Merchant", false, 1100)]
        private static void SpawnMerchantMenu()
        {
            GameObject npc = SpawnNpc("Npc_Merchant", GtgLabKit.ViewPivot(), 0f, NpcRole.Merchant, DecisionBackend.StateMachine,
                NpcDisposition.Peaceful, AttackStyle.Melee, false, false, new Color(0.9f, 0.75f, 0.3f, 1f));
            GtgLabKit.Finish(npc, "Merchant spawned: role Merchant, state machine, stands still, peaceful.");
        }

        [MenuItem("GTG/NPC/Spawn Melee Enemy", false, 1101)]
        private static void SpawnMeleeMenu()
        {
            GameObject npc = SpawnNpc("Npc_Melee", GtgLabKit.ViewPivot(), 0f, NpcRole.Enemy, DecisionBackend.Utility,
                NpcDisposition.Hostile, AttackStyle.Melee, true, false, new Color(0.8f, 0.25f, 0.25f, 1f));
            GtgLabKit.Finish(npc, "Melee enemy spawned. It needs a baked NavMesh to move, see GTG, NPC, Ensure NavMesh Surface.");
        }

        [MenuItem("GTG/NPC/Spawn Archer", false, 1102)]
        private static void SpawnArcherMenu()
        {
            GameObject npc = SpawnNpc("Npc_Archer", GtgLabKit.ViewPivot(), 0f, NpcRole.Enemy, DecisionBackend.Utility,
                NpcDisposition.Hostile, AttackStyle.Ranged, true, false, new Color(0.3f, 0.65f, 0.35f, 1f));
            Undo.AddComponent<ManagedNpcShooter>(npc);
            GtgLabKit.Finish(npc, "Archer spawned with a shooter: arrows for 12 damage from up to 20 m.");
        }

        [MenuItem("GTG/NPC/Spawn Fighting Pair (melee vs archer)", false, 1103)]
        private static void SpawnPairMenu()
        {
            Vector3 center = GtgLabKit.ViewPivot();
            GameObject melee = SpawnNpc("Npc_Melee", center + new Vector3(-4f, 0f, 0f), 90f, NpcRole.Enemy, DecisionBackend.Utility,
                NpcDisposition.Hostile, AttackStyle.Melee, true, true, new Color(0.8f, 0.25f, 0.25f, 1f));
            GameObject archer = SpawnNpc("Npc_Archer", center + new Vector3(4f, 0f, 0f), -90f, NpcRole.Enemy, DecisionBackend.Utility,
                NpcDisposition.Hostile, AttackStyle.Ranged, true, true, new Color(0.3f, 0.65f, 0.35f, 1f));
            Undo.AddComponent<ManagedNpcShooter>(archer);
            GtgLabKit.Finish(melee, "Pair spawned 8 m apart, facing each other, each a threat to the other. "
                + "Bake the NavMesh, press Play, and open GTG, NPC Debugger to watch them fight.");
            Selection.objects = new UnityEngine.Object[] { melee, archer };
        }

        [MenuItem("GTG/NPC/Make Selected A Target (threat source + health)", false, 1110)]
        private static void MakeTargetMenu()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                GtgLabKit.Warn("Select the player or an NPC first.");
                return;
            }

            for (int i = 0; i < selected.Length; i++)
            {
                GtgLabKit.Ensure<ManagedHealth>(selected[i]);
                GtgLabKit.Ensure<ManagedNpcThreatSource>(selected[i]);
            }

            GtgLabKit.Finish(selected[0], "Threat source and health added. The default sight points assume the pivot is at the feet and the body is 2 m tall.");
        }

        [MenuItem("GTG/NPC/Ensure NavMesh Surface On Test Ground", false, 1111)]
        private static void EnsureSurfaceMenu()
        {
            GameObject ground = GtgLabKit.FindRoot("GTG Test Ground");
            if (ground == null)
            {
                GtgLabKit.Warn("No GTG Test Ground in the scene. Use GTG, Player, Ensure Test Ground first.");
                return;
            }

            Type surfaceType = FindType(SurfaceTypeName);
            if (surfaceType == null)
            {
                GtgLabKit.Warn("The AI Navigation package is not installed, so there is no NavMesh Surface. Install it in the Package Manager.");
                return;
            }

            Component surface = ground.GetComponent(surfaceType);
            if (surface == null)
            {
                surface = Undo.AddComponent(ground, surfaceType);
            }

            GtgLabKit.SetInt(surface, "m_LayerMask", GtgLabKit.GroundMask | (1 << GtgLabKit.LayerWall));
            GtgLabKit.Finish(ground, "NavMesh Surface is on the test ground with its layer mask set to Ground and Wall. "
                + "Click Bake on that component now. Bake again after moving the ground or the walls.");
        }

        /// <summary>
        /// An NPC with its pivot at its feet: a root with a capsule collider and a capsule view, on layer
        /// Npc, with health and a brain, and an actor when it can move. With <paramref name="targetable"/>
        /// it is also a target for other NPCs.
        /// </summary>
        public static GameObject SpawnNpc(string npcName, Vector3 groundPoint, float yaw, NpcRole role, DecisionBackend backend,
            NpcDisposition disposition, AttackStyle style, bool canMove, bool targetable, Color color)
        {
            GtgLabKit.EnsureLayerNames();

            GameObject npc = GtgLabKit.NewObject(npcName, null);
            npc.layer = GtgLabKit.LayerNpc;
            npc.transform.position = groundPoint + new Vector3(0f, 0.05f, 0f);
            npc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(npc);
            collider.height = 2f;
            collider.radius = 0.5f;
            collider.center = new Vector3(0f, 1f, 0f);

            GameObject view = GtgLabKit.Primitive(PrimitiveType.Capsule, "View", npc.transform,
                new Vector3(0f, 1f, 0f), Vector3.one, "Npc" + npcName, color, false);
            view.layer = GtgLabKit.LayerNpc;
            GameObject nose = GtgLabKit.Primitive(PrimitiveType.Cube, "Nose", view.transform,
                new Vector3(0f, 0.35f, 0.5f), new Vector3(0.25f, 0.25f, 0.4f), "NpcNose", Color.white, false);
            nose.layer = GtgLabKit.LayerNpc;

            Undo.AddComponent<ManagedHealth>(npc);
            ManagedNpcBrain brain = Undo.AddComponent<ManagedNpcBrain>(npc);
            GtgLabKit.SetInt(brain, "_role", (int)role);
            GtgLabKit.SetInt(brain, "_backend", (int)backend);
            GtgLabKit.SetInt(brain, "_disposition", (int)disposition);
            GtgLabKit.SetInt(brain, "_attackStyle", (int)style);
            GtgLabKit.SetBool(brain, "_canMove", canMove);
            GtgLabKit.SetInt(brain, "_sightMask", GtgLabKit.SightMask);

            if (canMove)
            {
                Undo.AddComponent<ManagedNpcActor>(npc);
            }

            if (targetable)
            {
                Undo.AddComponent<ManagedNpcThreatSource>(npc);
            }

            return npc;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(fullName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
