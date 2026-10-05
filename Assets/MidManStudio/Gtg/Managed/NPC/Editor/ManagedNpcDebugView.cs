// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcDebugView.cs"
// ============================================================================

using System.Collections.Generic;
using MidManStudio.Gtg.NPC.Components;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>What the NPC debugger draws in the Scene view.</summary>
    public enum ManagedNpcDrawMode
    {
        Off = 0,
        SelectedOnly = 1,
        All = 2,
    }

    /// <summary>Which NPCs the list and the Scene view show, by what they are doing.</summary>
    public enum ManagedNpcActionFilter
    {
        Any = 0,
        Idle = 1,
        Trade = 2,
        Patrol = 3,
        Attack = 4,
        Retreat = 5,
    }

    /// <summary>The switches of the debugger.</summary>
    public sealed class ManagedNpcDrawOptions
    {
        public ManagedNpcDrawMode Mode = ManagedNpcDrawMode.SelectedOnly;
        public ManagedNpcActionFilter Filter = ManagedNpcActionFilter.Any;
        public bool OnlyWithThreat;
        public bool Labels = true;
        public bool Paths = true;
        public bool Ranges = true;
        public bool SightCones;
    }

    /// <summary>
    /// Editor-only drawing and text for the NPC debugger. Everything here reads the public state of the
    /// brain and the actor and changes nothing in the game. The window calls it from the Scene view.
    /// </summary>
    public static class ManagedNpcDebugView
    {
        /// <summary>Most NPCs drawn in one Scene view repaint, so a crowd stays usable.</summary>
        public const int MaxDrawn = 300;

        private const int MaxPathCorners = 64;
        private static readonly Vector3[] Corners = new Vector3[MaxPathCorners];

        public static Color ColorFor(NpcAction action)
        {
            switch (action)
            {
                case NpcAction.Trade: return new Color(0.3f, 0.85f, 0.95f, 1f);
                case NpcAction.Patrol: return new Color(0.35f, 0.9f, 0.4f, 1f);
                case NpcAction.Attack: return new Color(1f, 0.25f, 0.2f, 1f);
                case NpcAction.Retreat: return new Color(1f, 0.75f, 0.15f, 1f);
                default: return new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        public static bool Passes(ManagedNpcDrawOptions options, ManagedNpcBrain brain)
        {
            if (options.OnlyWithThreat && !brain.ThreatVisible)
            {
                return false;
            }

            return options.Filter == ManagedNpcActionFilter.Any || (int)brain.Action == (int)options.Filter - 1;
        }

        /// <summary>One line for the list and the Scene label.</summary>
        public static string Describe(ManagedNpcBrain brain)
        {
            string text = brain.name + "  " + brain.Role + "/" + brain.Backend + "  " + brain.Action;
            if (!brain.IsAlive)
            {
                return text + "  dead";
            }

            text += "  hp " + Mathf.RoundToInt(brain.HealthFraction * 100f) + "%";
            if (brain.ThreatVisible)
            {
                text += "  sees threat";
            }

            return text;
        }

        public static string DescribeDirector(ManagedNpcDirector director, int registered)
        {
            if (director == null)
            {
                return registered + " NPCs registered, no director running";
            }

            return registered + " NPCs, batch " + director.LastBatchCount + ", rays " + director.LastSightRays +
                (director.LastUsedNative ? ", native library" : ", managed fallback");
        }

        /// <summary>The waypoints of an actor in visiting order, starting at the next one. Null stops are skipped.</summary>
        public static int CollectRoute(ManagedNpcActor actor, List<Vector3> into)
        {
            into.Clear();
            int count = actor.WaypointCount;
            int first = actor.NextWaypointIndex;
            for (int i = 0; i < count; i++)
            {
                Transform waypoint = actor.GetWaypoint((first + i) % count);
                if (waypoint != null)
                {
                    into.Add(waypoint.position);
                }
            }

            return into.Count;
        }

        /// <summary>Draws one NPC. Call it from SceneView.duringSceneGui.</summary>
        public static void Draw(ManagedNpcBrain brain, ManagedNpcDrawOptions options, bool selected, List<Vector3> routeScratch)
        {
            Color color = ColorFor(brain.Action);
            Vector3 position = brain.transform.position;
            ManagedNpcActor actor = brain.GetComponent<ManagedNpcActor>();

            if (options.Labels)
            {
                GUIStyle style = new GUIStyle();
                style.normal.textColor = color;
                style.fontSize = selected ? 12 : 10;
                Handles.Label(position + Vector3.up * 2.3f, Describe(brain), style);
            }

            if (options.SightCones)
            {
                DrawCone(brain, color);
            }

            if (actor != null)
            {
                if (options.Ranges)
                {
                    DrawRanges(brain, actor, color);
                }

                if (options.Paths)
                {
                    DrawPath(brain, actor, color, routeScratch);
                }
            }

            if (options.Paths && brain.Threat != null)
            {
                Handles.color = ColorFor(NpcAction.Attack);
                Vector3 mark = brain.LastKnownThreatPosition;
                Handles.DrawLine(mark + new Vector3(-0.4f, 0.05f, -0.4f), mark + new Vector3(0.4f, 0.05f, 0.4f));
                Handles.DrawLine(mark + new Vector3(-0.4f, 0.05f, 0.4f), mark + new Vector3(0.4f, 0.05f, -0.4f));
            }
        }

        private static void DrawCone(ManagedNpcBrain brain, Color color)
        {
            Vector3 eye = brain.EyePosition;
            float half = Mathf.Clamp(brain.FieldOfViewDegrees, 0f, 360f) * 0.5f;
            Vector3 forward = brain.transform.forward;
            Vector3 left = Quaternion.AngleAxis(-half, Vector3.up) * forward;
            Handles.color = new Color(color.r, color.g, color.b, brain.ThreatVisible ? 0.9f : 0.4f);
            Handles.DrawWireArc(eye, Vector3.up, left, half * 2f, brain.ViewRange);
            Handles.DrawLine(eye, eye + left * brain.ViewRange);
            Handles.DrawLine(eye, eye + (Quaternion.AngleAxis(half * 2f, Vector3.up) * left) * brain.ViewRange);
        }

        private static void DrawRanges(ManagedNpcBrain brain, ManagedNpcActor actor, Color color)
        {
            Vector3 position = brain.transform.position;
            Handles.color = new Color(color.r, color.g, color.b, 0.6f);
            if (actor.Mode == NpcAction.Attack)
            {
                Handles.DrawWireDisc(position, Vector3.up, actor.AttackRange);
            }
            else if (actor.Mode == NpcAction.Patrol && actor.WaypointCount == 0)
            {
                Handles.DrawWireDisc(actor.Home, Vector3.up, actor.WanderRadius);
            }
            else if (actor.Mode == NpcAction.Retreat)
            {
                Handles.DrawWireDisc(position, Vector3.up, actor.RetreatDistance);
            }
        }

        private static void DrawPath(ManagedNpcBrain brain, ManagedNpcActor actor, Color color, List<Vector3> routeScratch)
        {
            Vector3 position = brain.transform.position;
            Handles.color = color;

            int corners = actor.GetPathCorners(Corners);
            for (int i = 1; i < corners; i++)
            {
                Handles.DrawLine(Corners[i - 1], Corners[i]);
            }

            if (corners > 0)
            {
                Handles.DrawSolidDisc(actor.Destination, Vector3.up, 0.25f);
            }

            if (actor.Mode == NpcAction.Patrol && CollectRoute(actor, routeScratch) > 0)
            {
                Handles.color = new Color(color.r, color.g, color.b, 0.5f);
                Vector3 previous = routeScratch[routeScratch.Count - 1];
                for (int i = 0; i < routeScratch.Count; i++)
                {
                    Handles.DrawDottedLine(previous, routeScratch[i], 4f);
                    Handles.DrawWireDisc(routeScratch[i], Vector3.up, 0.3f);
                    Handles.Label(routeScratch[i] + Vector3.up * 0.4f, (i + 1).ToString());
                    previous = routeScratch[i];
                }
            }
        }
    }
}
