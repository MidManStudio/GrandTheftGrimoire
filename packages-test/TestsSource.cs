// Checks for ManagedQuestMissionSource. Verification harness only.
using System;
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Missions;
using MidManStudio.Questly.Core;
using MidManStudio.Questly.Generated;

namespace MidManStudio.Gtg.Managed.Quests.Tests
{
    public static partial class Tests
    {
        private static void SourceChecks()
        {
            Throws<ArgumentNullException>(delegate { Rig r = NewRig(true); new ManagedQuestMissionSource(null, r.Table, r.Text); }, "a null runtime is refused");
            Throws<ArgumentNullException>(delegate { Rig r = NewRig(true); new ManagedQuestMissionSource(r.Runtime, null, r.Text); }, "a null table is refused");
            Throws<ArgumentNullException>(delegate { Rig r = NewRig(true); new ManagedQuestMissionSource(r.Runtime, r.Table, null); }, "a null text table is refused");

            // The empty state.
            Rig rig = NewRig(true);
            ManagedQuestMissionSource source = rig.Source;
            Eq(Panel(source), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "with no ACTIVE quest the panel is empty");
            Check(source.TrackedId == null, "nothing is tracked");
            Eq(source.Version, 1, "the first content is version 1");
            Eq(source.LineAt(-1), "", "a line below range is empty");
            Eq(source.LineAt(1), "", "a line past range is empty");
            Check(!source.Refresh(), "a refresh with nothing new says so");
            Eq(source.Version, 1, "and keeps the version");

            // A counter quest.
            rig.Runtime.StartQuest("collect_wood");
            Check(source.Refresh(), "starting a quest changes the panel");
            Eq(Panel(source), "Collect wood | Gather logs 0/5", "title and counter line");
            Eq(source.TrackedId, "collect_wood", "the started quest is tracked");
            Eq(source.Version, 2, "version 2");

            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 2f);
            Check(source.Refresh(), "a counter step changes the panel");
            Eq(Panel(source), "Collect wood | Gather logs 2/5", "counter at 2");
            Eq(source.Version, 3, "version 3");

            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 0.5f);
            Check(!source.Refresh(), "a step that keeps the whole part changes nothing");
            Eq(source.Version, 3, "and keeps the version");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 0.5f);
            Check(source.Refresh(), "the whole part moves from 2 to 3");
            Eq(Panel(source), "Collect wood | Gather logs 3/5", "counter at 3");

            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 2f);
            Eq(rig.Runtime.GetQuestState("collect_wood"), QuestState.COMPLETED, "the quest completes at 5");
            Check(source.Refresh(), "a completed quest leaves the panel");
            Eq(Panel(source), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "the panel is empty again");
            Check(source.TrackedId == null, "nothing is tracked again");
            Check(rig.Runtime.GetQuestState("deliver_package") == QuestState.AVAILABLE, "the quest it unlocks is AVAILABLE, so it is not shown");
            Check(!source.Refresh(), "and a refresh finds nothing new");

            // Done lines, an empty title key and an empty label key.
            rig = NewRig(true);
            rig.Runtime.StartQuest("two_step");
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "two_step | Find the key | b", "an empty title key shows the id and an empty label key shows the objective id");
            rig.Runtime.SetFlag("two_step", "a", true);
            Check(rig.Source.Refresh(), "a flag objective that completes changes the panel");
            Eq(Panel(rig.Source), "two_step | Find the key" + ManagedMissionTracker.DoneSuffix + " | b", "a done objective takes the suffix of the tracker");
            rig.Runtime.SetFlag("two_step", "b", true);
            rig.Source.Refresh();
            Eq(Panel(rig.Source), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "the quest leaves with its last objective");

            // Missing words show the key, then the words arrive.
            rig = NewRig(false);
            rig.Runtime.StartQuest("collect_wood");
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "quest.collect_wood.title | quest.collect_wood.obj_counter 0/5", "missing text shows the keys");
            rig.Text.Load(TextSource);
            Check(rig.Source.Refresh(), "new words change the panel");
            Eq(Panel(rig.Source), "Collect wood | Gather logs 0/5", "the words are used");
            rig.Text.Clear();
            Check(rig.Source.Refresh(), "dropping the words changes the panel");
            Eq(Panel(rig.Source), "quest.collect_wood.title | quest.collect_wood.obj_counter 0/5", "the keys show again");

            // A composite is one line, and its children are not lines.
            rig = NewRig(true);
            rig.Runtime.StartQuest("clear_camp");
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "Clear the camp | Clear the camp fully", "only the root objective is a line");
            rig.Runtime.ReportProgress("clear_camp", "obj_defeat", 10f);
            Eq(rig.Runtime.GetObjectiveState("clear_camp", "obj_defeat"), ObjectiveState.COMPLETE, "the child completes");
            Check(!rig.Source.Refresh(), "a child that completes does not change the composite line");
            rig.Runtime.SetFlag("clear_camp", "obj_flag_leader", true);
            Eq(rig.Runtime.GetQuestState("clear_camp"), QuestState.COMPLETED, "the composite completes the quest");
            rig.Source.Refresh();
            Check(rig.Source.TrackedId == null, "the quest leaves");

            // A composite that completes while the quest stays open changes its line by state alone.
            rig = NewRig(true);
            rig.Runtime.StartQuest("combo");
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "combo | Both parts | Other thing", "a composite and a flag next to it");
            rig.Runtime.SetFlag("combo", "c1", true);
            Check(!rig.Source.Refresh(), "the first child changes no line");
            rig.Runtime.SetFlag("combo", "c2", true);
            Eq(rig.Runtime.GetQuestState("combo"), QuestState.ACTIVE, "the quest is still open");
            Check(rig.Source.Refresh(), "the composite completing changes its line, though no value moved");
            Eq(Panel(rig.Source), "combo | Both parts" + ManagedMissionTracker.DoneSuffix + " | Other thing", "the composite line is done");

            // A timer objective has no line.
            rig = NewRig(true);
            rig.Runtime.StartQuest("timed");
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "Timed job | Press the button", "a TIMER objective is not shown");
            Eq(rig.Source.LineCount, 1, "one line");

            // Odd values.
            rig = NewRig(true);
            rig.Runtime.StartQuest("collect_wood");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", -3f);
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "Collect wood | Gather logs 0/5", "a negative value shows as 0");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", float.NaN);
            rig.Source.Refresh();
            Eq(Panel(rig.Source), "Collect wood | Gather logs 0/5", "NaN shows as 0");
        }

        private static void TrackingChecks()
        {
            // The first ACTIVE quest in table order is picked, and it stays.
            Rig rig = NewRig(true);
            rig.Runtime.StartQuest("clear_camp");
            rig.Runtime.StartQuest("collect_wood");
            rig.Source.Refresh();
            Eq(rig.Source.TrackedId, "collect_wood", "the first ACTIVE quest of the table is picked");

            rig = NewRig(true);
            rig.Runtime.StartQuest("two_step");
            rig.Source.Refresh();
            Eq(rig.Source.TrackedId, "two_step", "two_step is tracked");
            rig.Runtime.StartQuest("collect_wood");
            Check(!rig.Source.Refresh(), "a quest that starts later does not take the panel");
            Eq(rig.Source.TrackedId, "two_step", "the tracked quest stays");

            // Track.
            Check(rig.Source.Track("collect_wood"), "an ACTIVE quest can be tracked");
            Eq(Panel(rig.Source), "Collect wood | Gather logs 0/5", "the panel follows at once");
            Check(rig.Source.Track("collect_wood"), "tracking the tracked quest is true");
            Check(!rig.Source.Track("clear_camp"), "an AVAILABLE quest cannot be tracked");
            Check(!rig.Source.Track("deliver_package"), "a LOCKED quest cannot be tracked");
            Check(!rig.Source.Track("no_such_quest"), "an unknown quest cannot be tracked");
            Check(!rig.Source.Track(null), "a null id cannot be tracked");
            Check(!rig.Source.Track(""), "an empty id cannot be tracked");
            Eq(rig.Source.TrackedId, "collect_wood", "failed tracking changes nothing");

            // A HIDDEN quest is never shown.
            rig.Runtime.StartQuest("secret_job");
            Eq(rig.Runtime.GetQuestState("secret_job"), QuestState.ACTIVE, "the hidden quest is ACTIVE");
            Check(!rig.Source.Track("secret_job"), "a HIDDEN quest cannot be tracked");

            // When the tracked quest leaves, the next ACTIVE one that is not hidden takes over.
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 5f);
            Check(rig.Source.Refresh(), "the panel moves on");
            Eq(rig.Source.TrackedId, "two_step", "to the next ACTIVE quest");
            rig.Runtime.ForceState("two_step", QuestState.ABANDONED);
            rig.Source.Refresh();
            Check(rig.Source.TrackedId == null, "a quest that is no longer ACTIVE leaves, and a HIDDEN one does not replace it");
            Eq(Panel(rig.Source), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "the panel is empty");

            // A tracked quest that is forced out and starts again is not pinned.
            rig = NewRig(true);
            rig.Runtime.StartQuest("collect_wood");
            rig.Runtime.StartQuest("two_step");
            rig.Source.Refresh();
            rig.Source.Track("two_step");
            rig.Runtime.ForceState("two_step", QuestState.AVAILABLE);
            rig.Source.Refresh();
            Eq(rig.Source.TrackedId, "collect_wood", "the other ACTIVE quest takes the panel");
            rig.Runtime.StartQuest("two_step");
            rig.Source.Refresh();
            Eq(rig.Source.TrackedId, "collect_wood", "and the quest that started again does not take it back");

            // A source built after progress already exists reads it.
            Rig first = NewRig(true);
            first.Runtime.StartQuest("collect_wood");
            first.Runtime.ReportProgress("collect_wood", "obj_counter", 3f);
            ManagedQuestMissionSource late = new ManagedQuestMissionSource(first.Runtime, first.Table, first.Text);
            Eq(Panel(late), "Collect wood | Gather logs 3/5", "a new source shows the current progress");

            // A restored save raises no Questly event, and the next refresh picks it up.
            QuestProgressSnapshot snapshot = first.Runtime.CaptureState();
            Rig second = NewRig(true);
            second.Runtime.RestoreState(snapshot);
            Eq(Panel(second.Source), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "a restore alone does not move the panel");
            Check(second.Source.Refresh(), "a refresh after the restore does");
            Eq(Panel(second.Source), "Collect wood | Gather logs 3/5", "the restored progress is shown");
        }

        private static void ChangeChecks()
        {
            // A definition change with new words for a label.
            Rig rig = NewRig(true);
            rig.Text.Load("quest.collect_wood.obj_counter2=Chop timber");
            rig.Runtime.StartQuest("collect_wood");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 1f);
            rig.Source.Refresh();
            Eq(rig.Table.Subscribers, 2, "the runtime and the source listen to the table");
            Eq(Panel(rig.Source), "Collect wood | Gather logs 1/5", "before the change");
            rig.Table.Replace(Fixture("quest.collect_wood.obj_counter2"), true);
            Eq(rig.Runtime.GetObjectiveValue("collect_wood", "obj_counter"), 1f, "Questly keeps the progress");
            Check(rig.Source.Refresh(), "a changed definition changes the panel");
            Eq(Panel(rig.Source), "Collect wood | Chop timber 1/5", "the new label is used");
            Check(!rig.Source.Refresh(), "and a second refresh finds nothing");

            // A change event that leaves the text as it was is not a change of the panel.
            int version = rig.Source.Version;
            rig.Table.Replace(Fixture("quest.collect_wood.obj_counter2"), true);
            Check(!rig.Source.Refresh(), "a rebuilt definition with the same text changes nothing");
            Eq(rig.Source.Version, version, "and keeps the version");

            // A definition changed in place, with the table raising its event, is read again.
            rig.Text.Load("quest.collect_wood.title2=Timber run");
            rig.Source.Refresh();
            rig.Table.Find("collect_wood").Identity.TitleKey = "quest.collect_wood.title2";
            rig.Table.Replace(new List<QuestDefinition>(rig.Table.AllQuests), true);
            Check(rig.Source.Refresh(), "a definition changed in place changes the panel");
            Eq(Panel(rig.Source), "Timber run | Chop timber 1/5", "the new title of the same definition is used");

            // A definition that disappears from the table leaves the panel.
            List<QuestDefinition> without = Fixture();
            without.RemoveAt(0);
            rig.Table.Replace(without, true);
            Check(rig.Source.Refresh(), "a quest that is gone from the table leaves the panel");
            Check(rig.Source.TrackedId == null, "nothing is tracked");

            // Dispose.
            rig = NewRig(true);
            rig.Runtime.StartQuest("collect_wood");
            rig.Source.Refresh();
            rig.Source.Dispose();
            Eq(rig.Table.Subscribers, 1, "dispose stops listening to the table");
            rig.Source.Dispose();
            Eq(rig.Table.Subscribers, 1, "a second dispose does nothing");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 2f);
            Check(!rig.Source.Refresh(), "a disposed source does not refresh");
            Check(!rig.Source.Track("collect_wood"), "a disposed source does not track");
            Eq(Panel(rig.Source), "Collect wood | Gather logs 0/5", "a disposed source keeps what it showed");

            // The cost of a refresh that finds nothing new.
            rig = NewRig(true);
            rig.Runtime.StartQuest("clear_camp");
            rig.Runtime.ReportProgress("clear_camp", "obj_defeat", 3f);
            rig.Source.Refresh();
            for (int i = 0; i < 50; i++)
            {
                rig.Source.Refresh();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                rig.Source.Refresh();
            }

            Eq(GC.GetAllocatedBytesForCurrentThread() - allocated, 0L, "2000 refreshes with nothing new allocate nothing");

            rig = NewRig(true);
            long idle = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                rig.Source.Refresh();
            }

            Eq(GC.GetAllocatedBytesForCurrentThread() - idle, 0L, "2000 refreshes of the empty state allocate nothing");
        }
    }
}
