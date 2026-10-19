// Checks for the Managed Quests bridge. Verification harness only.
using System;
using System.Collections.Generic;
using MidManStudio.Questly.Core;
using MidManStudio.Questly.Generated;

namespace MidManStudio.Gtg.Managed.Quests.Tests
{
    /// <summary>A quest table held in memory, with a way to swap its content and raise the change event.</summary>
    public sealed class TestTable : IQuestTable
    {
        private List<QuestDefinition> _all = new List<QuestDefinition>();
        private Dictionary<string, QuestDefinition> _byId = new Dictionary<string, QuestDefinition>();
        private Action _changed;

        public int Subscribers;

        public TestTable(IEnumerable<QuestDefinition> quests)
        {
            Swap(quests);
        }

        public IReadOnlyList<QuestDefinition> AllQuests { get { return _all; } }

        public QuestDefinition Find(string questId)
        {
            QuestDefinition found;
            return _byId.TryGetValue(questId, out found) ? found : null;
        }

        public event Action DefinitionsChanged
        {
            add { _changed += value; Subscribers++; }
            remove { _changed -= value; Subscribers--; }
        }

        public void Replace(IEnumerable<QuestDefinition> quests, bool raise)
        {
            Swap(quests);
            if (raise && _changed != null)
            {
                _changed();
            }
        }

        private void Swap(IEnumerable<QuestDefinition> quests)
        {
            _all = new List<QuestDefinition>(quests);
            _byId = new Dictionary<string, QuestDefinition>();
            for (int i = 0; i < _all.Count; i++)
            {
                _byId[_all[i].Identity.Id] = _all[i];
            }
        }
    }

    public static partial class Tests
    {
        private static int _checks, _failures;
        private static string _section = "";

        public static void Section(string name) { _section = name; }

        public static void Check(bool ok, string what)
        {
            _checks++;
            if (!ok)
            {
                _failures++;
                Console.WriteLine("FAIL [" + _section + "] " + what);
            }
        }

        public static void Eq<T>(T actual, T expected, string what)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            Check(ok, what + " (expected " + expected + ", got " + actual + ")");
        }

        public static void Throws<TException>(Action action, string what) where TException : Exception
        {
            bool threw = false;
            try { action(); }
            catch (TException) { threw = true; }
            Check(threw, what + " (expected " + typeof(TException).Name + ")");
        }

        private static void Group(string name, Action body)
        {
            Section(name);
            try
            {
                body();
            }
            catch (Exception exception)
            {
                _checks++;
                _failures++;
                Console.WriteLine("FAIL [" + name + "] threw " + exception);
            }
        }

        public static int RunAll()
        {
            Group("text", TextChecks);
            Group("flag source", FlagSourceChecks);
            Group("mission source", SourceChecks);
            Group("mission source: tracking", TrackingChecks);
            Group("mission source: change and cost", ChangeChecks);
            Group("host", HostChecks);
            Console.WriteLine(_checks + " checks, " + _failures + " failed");
            return _failures == 0 ? 0 : 1;
        }

        // ---- Fixtures ----

        public static ObjectiveDefinition Obj(string id, ObjectiveKind kind, string labelKey, float target, float duration, params string[] children)
        {
            ObjectiveDefinition objective = new ObjectiveDefinition();
            objective.Id = id;
            objective.Kind = kind;
            objective.LabelKey = labelKey;
            objective.Target = target;
            objective.DurationSeconds = duration;
            objective.ChildIds = new List<string>(children);
            return objective;
        }

        public static QuestDefinition Quest(string id, QuestType type, string titleKey, ObjectiveDefinition[] objectives, params PrereqDefinition[] prereqs)
        {
            QuestIdentity identity = new QuestIdentity();
            identity.Id = id;
            identity.Type = type;
            identity.TitleKey = titleKey;
            return new QuestDefinition(identity, objectives, prereqs, new RewardDefinition[0]);
        }

        public static PrereqDefinition AfterQuest(string questId)
        {
            PrereqDefinition prereq = new PrereqDefinition();
            prereq.Kind = PrereqKind.QUEST_COMPLETED;
            prereq.QuestId = questId;
            return prereq;
        }

        public static List<QuestDefinition> Fixture(string woodLabelKey = "quest.collect_wood.obj_counter")
        {
            List<QuestDefinition> quests = new List<QuestDefinition>();
            quests.Add(Quest("collect_wood", QuestType.SIDE, "quest.collect_wood.title", new[]
            {
                Obj("obj_counter", ObjectiveKind.COUNTER, woodLabelKey, 5f, 0f),
            }));
            quests.Add(Quest("deliver_package", QuestType.SIDE, "quest.deliver_package.title", new[]
            {
                Obj("obj_flag", ObjectiveKind.FLAG, "quest.deliver_package.obj_flag", 0f, 0f),
            }, AfterQuest("collect_wood")));
            quests.Add(Quest("clear_camp", QuestType.MAIN, "quest.clear_camp.title", new[]
            {
                Obj("obj_defeat", ObjectiveKind.COUNTER, "quest.clear_camp.obj_defeat", 10f, 0f),
                Obj("obj_flag_leader", ObjectiveKind.FLAG, "quest.clear_camp.obj_flag_leader", 0f, 0f),
                Obj("obj_all", ObjectiveKind.COMPOSITE_ALL, "quest.clear_camp.obj_all", 0f, 0f, "obj_defeat", "obj_flag_leader"),
            }));
            quests.Add(Quest("two_step", QuestType.SIDE, "", new[]
            {
                Obj("a", ObjectiveKind.FLAG, "quest.two_step.a", 0f, 0f),
                Obj("b", ObjectiveKind.FLAG, "", 0f, 0f),
            }));
            quests.Add(Quest("timed", QuestType.SIDE, "quest.timed.title", new[]
            {
                Obj("t", ObjectiveKind.TIMER, "quest.timed.t", 0f, 30f),
                Obj("f", ObjectiveKind.FLAG, "quest.timed.f", 0f, 0f),
            }));
            quests.Add(Quest("secret_job", QuestType.HIDDEN, "quest.secret_job.title", new[]
            {
                Obj("s", ObjectiveKind.FLAG, "quest.secret_job.s", 0f, 0f),
            }));
            quests.Add(Quest("combo", QuestType.SIDE, "", new[]
            {
                Obj("c1", ObjectiveKind.FLAG, "quest.combo.c1", 0f, 0f),
                Obj("c2", ObjectiveKind.FLAG, "quest.combo.c2", 0f, 0f),
                Obj("all", ObjectiveKind.COMPOSITE_ALL, "quest.combo.all", 0f, 0f, "c1", "c2"),
                Obj("other", ObjectiveKind.FLAG, "quest.combo.other", 0f, 0f),
            }));
            return quests;
        }

        public const string TextSource =
            "# words for the fixture\n" +
            "quest.collect_wood.title=Collect wood\n" +
            "quest.collect_wood.obj_counter=Gather logs\n" +
            "quest.deliver_package.title=Deliver the package\n" +
            "quest.deliver_package.obj_flag=Hand over the parcel\n" +
            "quest.clear_camp.title=Clear the camp\n" +
            "quest.clear_camp.obj_all=Clear the camp fully\n" +
            "quest.two_step.a=Find the key\n" +
            "quest.timed.title=Timed job\n" +
            "quest.timed.f=Press the button\n" +
            "quest.combo.all=Both parts\n" +
            "quest.combo.other=Other thing\n";

        public sealed class Rig
        {
            public TestTable Table;
            public QuestRuntime Runtime;
            public ManagedQuestText Text;
            public ManagedQuestMissionSource Source;
        }

        public static Rig NewRig(bool withText)
        {
            Rig rig = new Rig();
            rig.Table = new TestTable(Fixture());
            rig.Runtime = new QuestRuntime(rig.Table);
            rig.Text = new ManagedQuestText();
            if (withText)
            {
                rig.Text.Load(TextSource);
            }

            rig.Source = new ManagedQuestMissionSource(rig.Runtime, rig.Table, rig.Text);
            return rig;
        }

        public static string Panel(ManagedQuestMissionSource source)
        {
            string panel = source.Title;
            for (int i = 0; i < source.LineCount; i++)
            {
                panel += " | " + source.LineAt(i);
            }

            return panel;
        }
    }
}
