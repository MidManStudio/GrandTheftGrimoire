// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedQuestHost.cs"
// ============================================================================

using System;
using MidManStudio.Gtg.Managed.Missions;
using MidManStudio.Mdix.Core;
using MidManStudio.Mdix.Unity;
using MidManStudio.Questly.Core;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Quests
{
    /// <summary>
    /// The scene component that connects Questly to the rest of the game. It builds a
    /// <see cref="QuestRuntime"/> from the quest data it is given, registers a
    /// <see cref="ManagedQuestMissionSource"/> as the mission source of the HUD while it is enabled, and
    /// offers a <see cref="ManagedQuestFlagSource"/> for anything that asks flag questions. Add it to any
    /// GameObject and assign either a baked quest asset or a .mdix asset. Code starts quests and reports
    /// progress through <see cref="Runtime"/>. The host does not call <c>AdvanceTime</c>, so a quest
    /// with a schedule needs a caller that does.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ManagedQuestHost : MonoBehaviour
    {
        [Tooltip("Baked quest data. Needs no native library at run time. Used first when both this and the .mdix asset are set.")]
        [SerializeField] private QuestDataAsset _baked;

        [Tooltip("A .mdix file with the quests. Loaded through the native mdix library, so it suits the Editor and development.")]
        [SerializeField] private MdixAsset _definitions;

        [Tooltip("Text file with one key=text line for every quest title and objective label.")]
        [SerializeField] private TextAsset _text;

        [Tooltip("Quest ids started when the scene loads, for testing. A quest that is not AVAILABLE is left alone.")]
        [SerializeField] private string[] _startAtLoad = new string[0];

        private readonly ManagedQuestText _words = new ManagedQuestText();
        private QuestRuntime _runtime;
        private ManagedQuestMissionSource _missions;
        private ManagedQuestFlagSource _flags;
        private MdixDatabase _database;

        /// <summary>True once a runtime is attached.</summary>
        public bool IsLoaded { get { return _runtime != null; } }

        /// <summary>The quests, or null while nothing is loaded.</summary>
        public QuestRuntime Runtime { get { return _runtime; } }

        /// <summary>The panel source, or null while nothing is loaded.</summary>
        public ManagedQuestMissionSource Missions { get { return _missions; } }

        /// <summary>Answers flag questions with quest ids, or null while nothing is loaded.</summary>
        public ManagedQuestFlagSource Flags { get { return _flags; } }

        /// <summary>The words of the panel. Loading more text here shows on the next frame.</summary>
        public ManagedQuestText Words { get { return _words; } }

        /// <summary>
        /// Uses this runtime and its table in place of the assigned data, and replaces the earlier runtime. A
        /// .mdix file the host loaded itself stays open until the host is destroyed.
        /// </summary>
        public void Attach(QuestRuntime runtime, IQuestTable table)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException("runtime");
            }

            if (table == null)
            {
                throw new ArgumentNullException("table");
            }

            ReleaseSource();
            _runtime = runtime;
            _missions = new ManagedQuestMissionSource(runtime, table, _words);
            _flags = new ManagedQuestFlagSource(runtime);
            if (isActiveAndEnabled)
            {
                ManagedMissionSources.Register(_missions);
            }
        }

        /// <summary>Puts a quest on the panel. False when nothing is loaded or the quest cannot be shown, see <see cref="ManagedQuestMissionSource.Track"/>.</summary>
        public bool Track(string questId)
        {
            return _missions != null && _missions.Track(questId);
        }

        /// <summary>Restores saved progress and refreshes the panel. False when nothing is loaded or the snapshot is null.</summary>
        public bool Restore(QuestProgressSnapshot snapshot)
        {
            if (_runtime == null || snapshot == null)
            {
                return false;
            }

            _runtime.RestoreState(snapshot);
            _missions.Refresh();
            return true;
        }

        private void Awake()
        {
            _words.Load(_text != null ? _text.text : null);

            IQuestTable table = BuildTable();
            if (table == null)
            {
                return;
            }

            Attach(new QuestRuntime(table), table);
            for (int i = 0; _startAtLoad != null && i < _startAtLoad.Length; i++)
            {
                if (!string.IsNullOrEmpty(_startAtLoad[i]))
                {
                    _runtime.StartQuest(_startAtLoad[i]);
                }
            }

            _missions.Refresh();
        }

        private void OnEnable()
        {
            if (_missions != null)
            {
                ManagedMissionSources.Register(_missions);
            }
        }

        private void OnDisable()
        {
            ManagedMissionSources.Unregister(_missions);
        }

        // Questly raises no event for a counter that moves, so the panel is read once a frame. The order of
        // this component runs it after the scripts that report progress in Update.
        private void Update()
        {
            if (_missions != null)
            {
                _missions.Refresh();
            }
        }

        private void OnDestroy()
        {
            ReleaseSource();
            if (_database != null)
            {
                _database.Dispose();
                _database = null;
            }
        }

        private void ReleaseSource()
        {
            if (_missions != null)
            {
                ManagedMissionSources.Unregister(_missions);
                _missions.Dispose();
            }

            _missions = null;
            _flags = null;
            _runtime = null;
        }

        private IQuestTable BuildTable()
        {
            if (_baked != null)
            {
                return new BakedQuestTable(_baked);
            }

            if (_definitions == null)
            {
                Debug.LogWarning("[GTG Quests] No quest data is assigned, so no quests are loaded.");
                return null;
            }

            try
            {
                MdixResult<MdixDatabase> loaded = _definitions.Load();
                if (loaded.IsFailure)
                {
                    Debug.LogError("[GTG Quests] " + _definitions.name + " did not load: " + loaded.Error);
                    return null;
                }

                MdixDatabase database = loaded.SuccessResult;
                MdixResult<MdixQuestTable> table = MdixQuestTable.Load(database);
                if (table.IsFailure)
                {
                    database.Dispose();
                    Debug.LogError("[GTG Quests] " + _definitions.name + " is not valid quest data: " + table.Error);
                    return null;
                }

                _database = database;
                return table.SuccessResult;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GTG Quests] Loading " + _definitions.name + " failed: " + exception.Message);
                return null;
            }
        }
    }
}
