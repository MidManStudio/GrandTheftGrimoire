// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedQuestMissionSource.cs"
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using MidManStudio.Gtg.Managed.Missions;
using MidManStudio.Questly.Core;
using MidManStudio.Questly.Generated;

namespace MidManStudio.Gtg.Managed.Quests
{
    /// <summary>
    /// Shows one active Questly quest on the missions panel of the HUD: its title, then one line for each
    /// of its objectives. It reads a <see cref="QuestRuntime"/> and never changes it. Questly raises no event
    /// when a counter moves and none when a save is restored, so the source does not wait for events. A
    /// caller runs <see cref="Refresh"/> once a frame, and a refresh that finds nothing new allocates nothing.
    /// The words come from a <see cref="ManagedQuestText"/>.
    /// </summary>
    public sealed class ManagedQuestMissionSource : IManagedMissionSource, IDisposable
    {
        private readonly QuestRuntime _runtime;
        private readonly IQuestTable _table;
        private readonly ManagedQuestText _text;

        private readonly List<ObjectiveDefinition> _shown = new List<ObjectiveDefinition>();
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _next = new List<string>();
        private readonly List<int> _signature = new List<int>();
        private readonly List<int> _scratch = new List<int>();

        private string _trackedId;
        private QuestDefinition _shownDefinition;
        private string _title = ManagedMissionTracker.DefaultTitle;
        private bool _definitionsChanged;
        private bool _disposed;

        public ManagedQuestMissionSource(QuestRuntime runtime, IQuestTable table, ManagedQuestText text)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            _runtime = runtime;
            _table = table;
            _text = text;
            _table.DefinitionsChanged += OnDefinitionsChanged;
            Refresh();
        }

        /// <summary>The id of the quest on the panel, or null when none is shown.</summary>
        public string TrackedId { get { return _trackedId; } }

        /// <summary>Goes up by one each time <see cref="Refresh"/> changes the title or a line of the panel.</summary>
        public int Version { get; private set; }

        /// <summary>The name of the tracked quest, or <see cref="ManagedMissionTracker.DefaultTitle"/> when none is shown.</summary>
        public string Title { get { return _title; } }

        /// <summary>The objectives of the tracked quest, or the one line of <see cref="ManagedMissionTracker.NoMissionLine"/>.</summary>
        public int LineCount { get { return _lines.Count; } }

        public string LineAt(int index)
        {
            return index >= 0 && index < _lines.Count ? _lines[index] : string.Empty;
        }

        /// <summary>
        /// Puts a quest on the panel. Only a quest that is ACTIVE, is in the table and is not of type HIDDEN
        /// can be shown, and any other id returns false and changes nothing. The quest stays until it is no
        /// longer ACTIVE.
        /// </summary>
        public bool Track(string questId)
        {
            if (_disposed || Showable(questId) == null)
            {
                return false;
            }

            _trackedId = questId;
            Refresh();
            return true;
        }

        /// <summary>
        /// Brings the panel up to date. A quest that stops being ACTIVE leaves the panel, and the first ACTIVE
        /// quest of the table that is not HIDDEN takes its place, or the panel shows the empty state when there
        /// is none. Returns true when the title or a line changed.
        /// </summary>
        public bool Refresh()
        {
            if (_disposed)
            {
                return false;
            }

            QuestDefinition definition = null;
            if (_trackedId != null)
            {
                definition = Showable(_trackedId);
                if (definition == null)
                {
                    _trackedId = null;
                }
            }

            if (_trackedId == null)
            {
                definition = PickFirst();
            }

            bool definitionMoved = !ReferenceEquals(definition, _shownDefinition) || _definitionsChanged;
            if (definitionMoved)
            {
                BuildShown(definition);
            }

            WriteSignature(definition);
            if (!definitionMoved && SameSignature())
            {
                return false;
            }

            _shownDefinition = definition;
            _definitionsChanged = false;
            _signature.Clear();
            _signature.AddRange(_scratch);
            return WriteContent(definition);
        }

        /// <summary>Stops listening to the table. The panel keeps what it last showed and <see cref="Refresh"/> does nothing after this.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _table.DefinitionsChanged -= OnDefinitionsChanged;
        }

        private void OnDefinitionsChanged()
        {
            _definitionsChanged = true;
        }

        private QuestDefinition Showable(string questId)
        {
            if (string.IsNullOrEmpty(questId))
            {
                return null;
            }

            QuestDefinition definition = _table.Find(questId);
            return definition != null && IsShowable(definition) ? definition : null;
        }

        private bool IsShowable(QuestDefinition definition)
        {
            return definition.Identity.Type != QuestType.HIDDEN
                && _runtime.GetQuestState(definition.Identity.Id) == QuestState.ACTIVE;
        }

        private QuestDefinition PickFirst()
        {
            IReadOnlyList<QuestDefinition> all = _table.AllQuests;
            for (int i = 0; i < all.Count; i++)
            {
                if (IsShowable(all[i]))
                {
                    _trackedId = all[i].Identity.Id;
                    return all[i];
                }
            }

            return null;
        }

        // The lines of a quest are its root objectives, the ones no composite lists as a child, in the order
        // of the definition. A composite line stands for its children. A TIMER objective has no line.
        private void BuildShown(QuestDefinition definition)
        {
            _shown.Clear();
            if (definition == null)
            {
                return;
            }

            HashSet<string> children = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                List<string> ids = definition.Objectives[i].ChildIds;
                for (int c = 0; ids != null && c < ids.Count; c++)
                {
                    children.Add(ids[c]);
                }
            }

            for (int i = 0; i < definition.Objectives.Count; i++)
            {
                ObjectiveDefinition objective = definition.Objectives[i];
                if (!children.Contains(objective.Id) && objective.Kind != ObjectiveKind.TIMER)
                {
                    _shown.Add(objective);
                }
            }
        }

        // What the panel depends on, as plain numbers: the version of the words, then the state and the whole
        // part of the value of each line. Two equal signatures give equal text.
        private void WriteSignature(QuestDefinition definition)
        {
            _scratch.Clear();
            _scratch.Add(_text.Version);
            if (definition == null)
            {
                return;
            }

            string questId = definition.Identity.Id;
            for (int i = 0; i < _shown.Count; i++)
            {
                string objectiveId = _shown[i].Id;
                _scratch.Add((int)_runtime.GetObjectiveState(questId, objectiveId));
                _scratch.Add(Whole(_runtime.GetObjectiveValue(questId, objectiveId)));
            }
        }

        private bool SameSignature()
        {
            if (_scratch.Count != _signature.Count)
            {
                return false;
            }

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_scratch[i] != _signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Builds the title and the lines, and keeps them only when they differ from what the panel shows.
        private bool WriteContent(QuestDefinition definition)
        {
            string title;
            _next.Clear();
            if (definition == null)
            {
                title = ManagedMissionTracker.DefaultTitle;
                _next.Add(ManagedMissionTracker.NoMissionLine);
            }
            else
            {
                string questId = definition.Identity.Id;
                string titleKey = definition.Identity.TitleKey;
                title = string.IsNullOrEmpty(titleKey) ? questId : _text.Resolve(titleKey);
                for (int i = 0; i < _shown.Count; i++)
                {
                    _next.Add(LineFor(questId, _shown[i]));
                }
            }

            if (title == _title && SameLines())
            {
                return false;
            }

            _title = title;
            _lines.Clear();
            _lines.AddRange(_next);
            Version++;
            return true;
        }

        private bool SameLines()
        {
            if (_next.Count != _lines.Count)
            {
                return false;
            }

            for (int i = 0; i < _next.Count; i++)
            {
                if (_next[i] != _lines[i])
                {
                    return false;
                }
            }

            return true;
        }

        private string LineFor(string questId, ObjectiveDefinition objective)
        {
            string label = string.IsNullOrEmpty(objective.LabelKey) ? objective.Id : _text.Resolve(objective.LabelKey);
            if (_runtime.GetObjectiveState(questId, objective.Id) == ObjectiveState.COMPLETE)
            {
                return label + ManagedMissionTracker.DoneSuffix;
            }

            if (objective.Kind == ObjectiveKind.COUNTER)
            {
                int value = Whole(_runtime.GetObjectiveValue(questId, objective.Id));
                return label + " " + value.ToString(CultureInfo.InvariantCulture) + "/" + Whole(objective.Target).ToString(CultureInfo.InvariantCulture);
            }

            return label;
        }

        // Counts show as whole numbers, rounded down. A negative number, NaN and anything past int range are clamped.
        private static int Whole(float value)
        {
            if (!(value > 0f))
            {
                return 0;
            }

            return value >= 2147483000f ? int.MaxValue : (int)value;
        }
    }
}
