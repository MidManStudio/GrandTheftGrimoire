// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedNpcDirector.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.NPC
{
    /// <summary>
    /// Drives every ManagedNpcBrain. A tick takes a bounded batch of NPCs, looks for threats,
    /// asks the Rust library for one decision per NPC in a single call, and hands the actions
    /// back. With no native library the managed copy of the decision runs instead. One
    /// director per scene. Brains create one on their own when the scene has none.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class ManagedNpcDirector : MonoBehaviour
    {
        private static readonly List<ManagedNpcBrain> Brains = new List<ManagedNpcBrain>();
        private static ManagedNpcDirector _instance;
        private static ulong _nextId;

        [Tooltip("Seconds between decision ticks.")]
        [SerializeField] private float _decisionIntervalSeconds = 0.1f;
        [Tooltip("Most NPCs decided in one tick. A larger population is served in turns.")]
        [SerializeField] private int _maxBatch = 256;
        [Tooltip("Most sight rays in one tick. An NPC that is out of budget keeps its last result until its next turn.")]
        [SerializeField] private int _maxSightRaysPerTick = 128;
        [SerializeField] private bool _showOverlay;

        private NPCNativeObservation[] _observations = new NPCNativeObservation[0];
        private NPCNativeDecision[] _decisions = new NPCNativeDecision[0];
        private ManagedNpcBrain[] _selected = new ManagedNpcBrain[0];
        private ManagedNpcBrain[] _owners = new ManagedNpcBrain[0];
        private float _nextTickTime;
        private int _cursor;
        private int _sightStart;
        private bool _warnedNoSources;

        public static int RegisteredCount { get { return Brains.Count; } }

        /// <summary>The active director, or null when none is running. For debug tools.</summary>
        public static ManagedNpcDirector Current { get { return _instance; } }

        /// <summary>The registered brain at an index below RegisteredCount. For debug tools.</summary>
        public static ManagedNpcBrain GetBrain(int index)
        {
            return Brains[index];
        }

        /// <summary>NPCs sent to the decision in the last tick.</summary>
        public int LastBatchCount { get; private set; }

        /// <summary>Sight rays cast in the last tick.</summary>
        public int LastSightRays { get; private set; }

        /// <summary>True when the last batch was decided by the native library, false for the managed copy.</summary>
        public bool LastUsedNative { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            Brains.Clear();
            _nextId = 0;
        }

        internal static void Register(ManagedNpcBrain brain)
        {
            if (Brains.Contains(brain))
            {
                return;
            }

            if (brain.Id == 0)
            {
                brain.Id = ++_nextId;
            }

            Brains.Add(brain);
            EnsureExists();
        }

        internal static void Unregister(ManagedNpcBrain brain)
        {
            Brains.Remove(brain);
        }

        private static void EnsureExists()
        {
            if (_instance != null)
            {
                return;
            }

            // A director that exists but is disabled or inactive is the scene's own choice, so no second one is made.
            if (FindFirstObjectByType<ManagedNpcDirector>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject host = new GameObject("GTG NPC Director");
            host.AddComponent<ManagedNpcDirector>();
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("More than one ManagedNpcDirector. Disabling the extra one on " + name + ".");
                enabled = false;
                return;
            }

            _instance = this;
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            float now = Time.time;
            if (now < _nextTickTime)
            {
                return;
            }

            _nextTickTime = now + Mathf.Max(0.01f, _decisionIntervalSeconds);
            Tick(now);
        }

        private void Tick(float now)
        {
            int total = Brains.Count;
            LastBatchCount = 0;
            LastSightRays = 0;
            if (total == 0)
            {
                _cursor = 0;
                return;
            }

            if (ManagedNpcThreatSource.Count == 0 && !_warnedNoSources)
            {
                _warnedNoSources = true;
                Debug.LogWarning("NPCs are registered but no ManagedNpcThreatSource is enabled, so no NPC can see a threat. Add the component to the player.");
            }

            int limit = Mathf.Clamp(_maxBatch, 1, NPCNativeLib.MaxBatch);
            EnsureBuffers(limit);
            if (_cursor >= total)
            {
                _cursor = 0;
            }

            // Phase 1: pick the batch. Nothing here raises an event, so the list cannot change under us.
            int want = Mathf.Min(limit, total);
            for (int i = 0; i < want; i++)
            {
                _selected[i] = Brains[(_cursor + i) % total];
            }

            _cursor = (_cursor + want) % total;

            // Phase 2: sight and observations for the living. The dead are handled in phase 3.
            // The first NPC that runs out of ray budget leads the next tick, so a batch that fits
            // in one tick still serves every NPC in turn when the budget is smaller than the demand.
            int rayBudget = Mathf.Max(0, _maxSightRaysPerTick);
            int start = _sightStart % want;
            int firstStarved = -1;
            int count = 0;
            for (int k = 0; k < want; k++)
            {
                ManagedNpcBrain brain = _selected[(start + k) % want];
                if (!brain.IsAlive)
                {
                    continue;
                }

                if (!brain.UpdateSight(now, ref rayBudget) && firstStarved < 0)
                {
                    firstStarved = k;
                }

                _observations[count] = brain.BuildObservation();
                _owners[count] = brain;
                count++;
            }

            if (firstStarved >= 0)
            {
                _sightStart = (start + firstStarved) % want;
            }

            LastSightRays = Mathf.Max(0, _maxSightRaysPerTick) - rayBudget;

            bool native = NPCNativeLib.TryDecide(_observations, _decisions, count);
            if (!native)
            {
                for (int i = 0; i < count; i++)
                {
                    _decisions[i] = NPCManagedFallback.Decide(_observations[i]);
                }
            }

            LastBatchCount = count;
            LastUsedNative = native;

            // Phase 3: hand the actions back. Listeners may disable or destroy NPCs here.
            for (int i = 0; i < want; i++)
            {
                if (!_selected[i].IsAlive)
                {
                    _selected[i].ApplyAction(NpcAction.Idle);
                }

                _selected[i] = null;
            }

            for (int i = 0; i < count; i++)
            {
                NPCNativeDecision decision = _decisions[i];
                ManagedNpcBrain owner = _owners[i];
                _owners[i] = null;

                // A decision that does not belong to this row, or is out of range, is dropped.
                if (decision.NpcId != _observations[i].NpcId || decision.Action < 0 || decision.Action > (int)NpcAction.Betray)
                {
                    continue;
                }

                if (owner != null)
                {
                    owner.ApplyAction((NpcAction)decision.Action);
                }
            }
        }

        private void EnsureBuffers(int size)
        {
            if (_observations.Length == size)
            {
                return;
            }

            _observations = new NPCNativeObservation[size];
            _decisions = new NPCNativeDecision[size];
            _selected = new ManagedNpcBrain[size];
            _owners = new ManagedNpcBrain[size];
        }

        private void OnGUI()
        {
            if (!_showOverlay)
            {
                return;
            }

            GUI.Label(new Rect(10f, 10f, 520f, 24f),
                "NPC: " + Brains.Count + " registered, batch " + LastBatchCount + ", rays " + LastSightRays +
                (LastUsedNative ? ", native" : ", managed fallback"));
        }
    }
}
