// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcDebuggerWindow.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Window with a live list of every NPC and the switches that draw movement, sight and targets in the
    /// Scene view. Open it from GTG, NPC Debugger, then press Play. It only reads the game.
    /// </summary>
    public sealed class ManagedNpcDebuggerWindow : EditorWindow
    {
        private const int MaxRows = 300;

        private static readonly string[] ModeNames = { "Off", "Selected only", "All NPCs" };
        private static readonly string[] FilterNames = { "Any action", "Idle", "Trade", "Patrol", "Attack", "Retreat", "Follow", "Hold", "Refused an order" };

        private readonly ManagedNpcDrawOptions _options = new ManagedNpcDrawOptions();
        private readonly List<Vector3> _routeScratch = new List<Vector3>();
        private Vector2 _scroll;

        [MenuItem("GTG/NPC Debugger")]
        public static void Open()
        {
            ManagedNpcDebuggerWindow window = GetWindow<ManagedNpcDebuggerWindow>("GTG NPC Debugger");
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.RepaintAll();
        }

        // Ten times a second, which is plenty for a debug list and keeps the cost low.
        private void OnInspectorUpdate()
        {
            Repaint();
            if (_options.Mode != ManagedNpcDrawMode.Off && Application.isPlaying)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play. The list and the Scene drawing show live NPCs and are empty in Edit mode.", MessageType.Info);
            }

            GUILayout.Label(ManagedNpcDebugView.DescribeDirector(ManagedNpcDirector.Current, ManagedNpcDirector.RegisteredCount));

            _options.Mode = (ManagedNpcDrawMode)EditorGUILayout.Popup("Scene drawing", (int)_options.Mode, ModeNames);
            _options.Filter = (ManagedNpcActionFilter)EditorGUILayout.Popup("Show", (int)_options.Filter, FilterNames);
            _options.OnlyWithThreat = EditorGUILayout.ToggleLeft("Only NPCs that see a threat", _options.OnlyWithThreat);
            _options.Labels = EditorGUILayout.ToggleLeft("Labels", _options.Labels);
            _options.Paths = EditorGUILayout.ToggleLeft("Paths, destinations, patrol routes, last known target", _options.Paths);
            _options.Ranges = EditorGUILayout.ToggleLeft("Attack, wander and retreat ranges", _options.Ranges);
            _options.SightCones = EditorGUILayout.ToggleLeft("Sight cones", _options.SightCones);

            GUILayout.Space(6f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int total = ManagedNpcDirector.RegisteredCount;
            int shown = 0;
            for (int i = 0; i < total; i++)
            {
                ManagedNpcBrain brain = ManagedNpcDirector.GetBrain(i);
                if (brain == null || !ManagedNpcDebugView.Passes(_options, brain))
                {
                    continue;
                }

                if (shown >= MaxRows)
                {
                    GUILayout.Label("More NPCs match, narrow the filter to see them.");
                    break;
                }

                shown++;
                if (GUILayout.Button(ManagedNpcDebugView.Describe(brain)))
                {
                    Selection.activeGameObject = brain.gameObject;
                    SceneView view = SceneView.lastActiveSceneView;
                    if (view != null)
                    {
                        view.FrameSelected();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGui(SceneView view)
        {
            if (!Application.isPlaying || _options.Mode == ManagedNpcDrawMode.Off)
            {
                return;
            }

            GameObject selected = Selection.activeGameObject;
            int total = ManagedNpcDirector.RegisteredCount;
            int drawn = 0;
            for (int i = 0; i < total && drawn < ManagedNpcDebugView.MaxDrawn; i++)
            {
                ManagedNpcBrain brain = ManagedNpcDirector.GetBrain(i);
                if (brain == null)
                {
                    continue;
                }

                bool isSelected = selected != null && brain.gameObject == selected;
                if (_options.Mode == ManagedNpcDrawMode.SelectedOnly && !isSelected)
                {
                    continue;
                }

                if (!ManagedNpcDebugView.Passes(_options, brain))
                {
                    continue;
                }

                ManagedNpcDebugView.Draw(brain, _options, isSelected, _routeScratch);
                drawn++;
            }
        }
    }
}
