// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "ManagedHealthLab.cs"
// ============================================================================

using MidManStudio.Gtg.Managed.Lab;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Health
{
    /// <summary>
    /// Editor test tools for health. Spawns target dummies, adds health to what is selected, and in
    /// Play mode hurts, heals or kills the selection. In Play mode every health shows as hp over its
    /// head in the Scene view. Menu: GTG, Health.
    /// </summary>
    public static class ManagedHealthLab
    {
        private const float DamageStep = 25f;

        [MenuItem("GTG/Health/Spawn Target Dummy At Scene View", false, 400)]
        private static void SpawnDummyMenu()
        {
            GameObject dummy = SpawnDummy(GtgLabKit.ViewPivot(), "Target Dummy");
            GtgLabKit.Finish(dummy, "Dummy spawned where the Scene view looks: layer Npc, 100 health, a capsule collider. "
                + "A fireball or a sword swing hurts it, and its hp shows over its head in Play mode.");
        }

        [MenuItem("GTG/Health/Add Health To Selected", false, 401)]
        private static void AddHealthMenu()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                GtgLabKit.Warn("Select one or more objects first.");
                return;
            }

            for (int i = 0; i < selected.Length; i++)
            {
                GtgLabKit.Ensure<ManagedHealth>(selected[i]);
            }

            GtgLabKit.Finish(selected[0], "Health added to " + selected.Length + " object(s), 100 each.");
        }

        [MenuItem("GTG/Health/Damage Selected 25 (Play)", false, 410)]
        private static void DamageMenu()
        {
            ForSelected("Damage", health => health.TakeDamage(DamageStep, null));
        }

        [MenuItem("GTG/Health/Heal Selected 25 (Play)", false, 411)]
        private static void HealMenu()
        {
            ForSelected("Heal", health => health.Heal(DamageStep));
        }

        [MenuItem("GTG/Health/Kill Selected (Play)", false, 412)]
        private static void KillMenu()
        {
            ForSelected("Kill", health => health.TakeDamage(health.Max + health.Current + 1f, null));
        }

        [MenuItem("GTG/Health/Reset Selected To Full (Play)", false, 413)]
        private static void ResetMenu()
        {
            ForSelected("Reset", health => health.ResetToFull());
        }

        [MenuItem("GTG/Health/Toggle Invulnerable On Selected (Play)", false, 414)]
        private static void InvulnerableMenu()
        {
            ForSelected("Toggle Invulnerable", health => health.Invulnerable = !health.Invulnerable);
        }

        /// <summary>
        /// A capsule target on layer Npc with a collider and 100 health. Its pivot is at its middle, so
        /// <paramref name="groundPoint"/> is the point on the ground it stands on.
        /// </summary>
        public static GameObject SpawnDummy(Vector3 groundPoint, string dummyName)
        {
            GtgLabKit.EnsureLayerNames();
            GameObject dummy = GtgLabKit.Primitive(PrimitiveType.Capsule, dummyName, null,
                groundPoint + new Vector3(0f, 1.05f, 0f), new Vector3(1f, 1f, 1f), "Dummy", new Color(0.95f, 0.55f, 0.15f, 1f), true);
            dummy.layer = GtgLabKit.LayerNpc;
            Undo.AddComponent<ManagedHealth>(dummy);
            return dummy;
        }

        private static void ForSelected(string action, System.Action<ManagedHealth> apply)
        {
            if (!GtgLabKit.RequirePlaying(action))
            {
                return;
            }

            GameObject[] selected = Selection.gameObjects;
            int done = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                ManagedHealth health = selected[i].GetComponentInParent<ManagedHealth>();
                if (health == null)
                {
                    continue;
                }

                apply(health);
                done++;
            }

            if (done == 0)
            {
                GtgLabKit.Warn("Nothing selected has a ManagedHealth. Select an object that has one, or use Add Health To Selected before Play.");
                return;
            }

            GtgLabKit.Log(action + " applied to " + done + " object(s).");
        }

        // hp over every health in Play mode, red when dead.
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawHealth(ManagedHealth health, GizmoType type)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Handles.Label(health.transform.position + Vector3.up * 2.3f,
                (health.IsAlive ? "hp " : "dead ") + Mathf.CeilToInt(health.Current) + "/" + Mathf.CeilToInt(health.Max)
                + (health.Invulnerable ? " (invulnerable)" : string.Empty));
        }
    }
}
