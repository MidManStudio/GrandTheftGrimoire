// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMissionBoard.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Missions
{
    /// <summary>One mission set up in the Inspector.</summary>
    [Serializable]
    public sealed class ManagedMissionEntry
    {
        [Tooltip("A short name the code refers to the mission by. Must be unique on the board.")]
        public string Id;

        [Tooltip("The heading of the panel while this mission is active. The id is used when empty.")]
        public string Title;

        [Tooltip("One line each. The panel shows the first four.")]
        public string[] Objectives;
    }

    /// <summary>
    /// The scene component that holds the player's missions. It owns a <see cref="ManagedMissionTracker"/> and
    /// registers it as the mission source of the HUD while it is enabled. Add it to any GameObject. Missions
    /// can be set in the Inspector, and code adds and ticks them off through <see cref="Tracker"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManagedMissionBoard : MonoBehaviour
    {
        [Tooltip("Missions held when the scene starts. The first is the active one.")]
        [SerializeField] private List<ManagedMissionEntry> _startingMissions = new List<ManagedMissionEntry>();

        private ManagedMissionTracker _tracker;
        private bool _seeded;

        /// <summary>The missions. Created on first use, so code may use it before this component has started.</summary>
        public ManagedMissionTracker Tracker
        {
            get
            {
                if (_tracker == null)
                {
                    _tracker = new ManagedMissionTracker();
                }

                return _tracker;
            }
        }

        private void Awake()
        {
            Seed();
        }

        private void OnEnable()
        {
            Seed();
            ManagedMissionSources.Register(Tracker);
        }

        private void OnDisable()
        {
            ManagedMissionSources.Unregister(_tracker);
        }

        // Loads the Inspector list once. An entry with no id, or an id used before, is skipped.
        private void Seed()
        {
            if (_seeded)
            {
                return;
            }

            _seeded = true;
            if (_startingMissions == null)
            {
                return;
            }

            for (int i = 0; i < _startingMissions.Count; i++)
            {
                ManagedMissionEntry entry = _startingMissions[i];
                if (entry != null)
                {
                    Tracker.Add(entry.Id, entry.Title, entry.Objectives);
                }
            }
        }
    }
}
