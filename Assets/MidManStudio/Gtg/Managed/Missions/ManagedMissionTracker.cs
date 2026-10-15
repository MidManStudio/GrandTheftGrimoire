// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedMissionTracker.cs"
// ============================================================================

using System.Collections.Generic;

namespace MidManStudio.Gtg.Managed.Missions
{
    /// <summary>
    /// A plain list of missions, each with a title and a list of objectives, and one of them active. The
    /// active mission is what the missions panel shows: its title, then its objectives as lines. It has no
    /// Unity types, so it can be built and checked anywhere. Nothing here decides what a mission is for, a
    /// caller adds and ticks them off.
    /// </summary>
    public sealed class ManagedMissionTracker : IManagedMissionSource
    {
        /// <summary>The title shown while no mission is active.</summary>
        public const string DefaultTitle = "Missions";

        /// <summary>The line shown while no mission is active.</summary>
        public const string NoMissionLine = "No active mission";

        /// <summary>Added to the text of an objective that is done.</summary>
        public const string DoneSuffix = " (done)";

        private sealed class Mission
        {
            public string Id;
            public string Title;
            public bool Complete;
            public readonly List<string> Objectives = new List<string>();
            public readonly List<bool> Done = new List<bool>();
        }

        private readonly List<Mission> _missions = new List<Mission>();
        private Mission _active;

        /// <summary>Missions held, done or not.</summary>
        public int Count { get { return _missions.Count; } }

        /// <summary>The id of the active mission, or null when there is none.</summary>
        public string ActiveId { get { return _active != null ? _active.Id : null; } }

        /// <summary>The title of the active mission, or <see cref="DefaultTitle"/> when there is none.</summary>
        public string Title { get { return _active != null ? _active.Title : DefaultTitle; } }

        /// <summary>The objectives of the active mission, or the one line of <see cref="NoMissionLine"/>.</summary>
        public int LineCount { get { return _active != null ? _active.Objectives.Count : 1; } }

        public string LineAt(int index)
        {
            if (_active == null)
            {
                return index == 0 ? NoMissionLine : string.Empty;
            }

            if (index < 0 || index >= _active.Objectives.Count)
            {
                return string.Empty;
            }

            return _active.Done[index] ? _active.Objectives[index] + DoneSuffix : _active.Objectives[index];
        }

        public bool Has(string id)
        {
            return Find(id) != null;
        }

        public bool IsComplete(string id)
        {
            Mission mission = Find(id);
            return mission != null && mission.Complete;
        }

        /// <summary>The number of objectives of a mission, 0 for one that is not held.</summary>
        public int ObjectiveCount(string id)
        {
            Mission mission = Find(id);
            return mission != null ? mission.Objectives.Count : 0;
        }

        /// <summary>True when the objective exists and is done.</summary>
        public bool IsObjectiveDone(string id, int index)
        {
            Mission mission = Find(id);
            return mission != null && index >= 0 && index < mission.Done.Count && mission.Done[index];
        }

        /// <summary>
        /// Adds a mission. It becomes the active one when none is. A null or empty id, or an id that is
        /// held already, adds nothing and returns false. A null or empty title is the id, a null list has
        /// no objectives and a null objective is an empty line.
        /// </summary>
        public bool Add(string id, string title, IList<string> objectives)
        {
            if (string.IsNullOrEmpty(id) || Find(id) != null)
            {
                return false;
            }

            Mission mission = new Mission();
            mission.Id = id;
            mission.Title = string.IsNullOrEmpty(title) ? id : title;
            if (objectives != null)
            {
                for (int i = 0; i < objectives.Count; i++)
                {
                    mission.Objectives.Add(objectives[i] ?? string.Empty);
                    mission.Done.Add(false);
                }
            }

            _missions.Add(mission);
            if (_active == null)
            {
                _active = mission;
            }

            return true;
        }

        /// <summary>Drops a mission. When it was the active one, the first mission that is not complete takes over.</summary>
        public bool Remove(string id)
        {
            Mission mission = Find(id);
            if (mission == null)
            {
                return false;
            }

            _missions.Remove(mission);
            if (_active == mission)
            {
                _active = FirstOpen();
            }

            return true;
        }

        /// <summary>Makes a mission the active one. A mission that is not held, or is complete, cannot be.</summary>
        public bool SetActive(string id)
        {
            Mission mission = Find(id);
            if (mission == null || mission.Complete)
            {
                return false;
            }

            _active = mission;
            return true;
        }

        /// <summary>
        /// Ticks off an objective. The mission completes with its last open objective. A mission that is not
        /// held or is complete, an index out of range and an objective that is done already change nothing.
        /// </summary>
        public bool CompleteObjective(string id, int index)
        {
            Mission mission = Find(id);
            if (mission == null || mission.Complete || index < 0 || index >= mission.Done.Count || mission.Done[index])
            {
                return false;
            }

            mission.Done[index] = true;
            if (AllDone(mission))
            {
                Finish(mission);
            }

            return true;
        }

        /// <summary>Completes a mission and every objective in it. One that is not held or is complete already returns false.</summary>
        public bool Complete(string id)
        {
            Mission mission = Find(id);
            if (mission == null || mission.Complete)
            {
                return false;
            }

            for (int i = 0; i < mission.Done.Count; i++)
            {
                mission.Done[i] = true;
            }

            Finish(mission);
            return true;
        }

        /// <summary>Drops every mission.</summary>
        public void Clear()
        {
            _missions.Clear();
            _active = null;
        }

        private void Finish(Mission mission)
        {
            mission.Complete = true;
            if (_active == mission)
            {
                _active = FirstOpen();
            }
        }

        private Mission FirstOpen()
        {
            for (int i = 0; i < _missions.Count; i++)
            {
                if (!_missions[i].Complete)
                {
                    return _missions[i];
                }
            }

            return null;
        }

        private static bool AllDone(Mission mission)
        {
            if (mission.Done.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < mission.Done.Count; i++)
            {
                if (!mission.Done[i])
                {
                    return false;
                }
            }

            return true;
        }

        private Mission Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < _missions.Count; i++)
            {
                if (_missions[i].Id == id)
                {
                    return _missions[i];
                }
            }

            return null;
        }
    }
}
