// Checks for the two feeders of the HUD: the heat sources (the Crown heat map and the registry) and the mission
// sources (the tracker, the board and the registry), and how the feed and the HUD read them. The Crown component
// that implements the heat source needs the NPC types and is compiled by the NPC harness instead. Verification
// harness only.
using System;
using System.Collections.Generic;
using System.Linq;
using MidManStudio.Gtg.Managed.Crown;
using MidManStudio.Gtg.Managed.Hud;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Gtg.Managed.Missions;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private sealed class FakeHeat : IManagedHudHeatSource
        {
            public int Value;
            public int Calls;
            public Vector3 LastPoint;

            public int SectionsAt(Vector3 point)
            {
                Calls++;
                LastPoint = point;
                return Value;
            }
        }

        private sealed class FakeMissions : IManagedMissionSource
        {
            public string TitleText;
            public readonly List<string> Lines = new List<string>();

            public string Title { get { return TitleText; } }
            public int LineCount { get { return Lines.Count; } }
            public string LineAt(int index) { return index >= 0 && index < Lines.Count ? Lines[index] : null; }
        }

        // The tier rule of ManagedCrownRegion, with the default thresholds.
        private static int DefaultTier(float heat)
        {
            if (!(heat > 0f)) { return 0; }
            if (heat >= 75f) { return 3; }
            if (heat >= 40f) { return 2; }
            return heat >= 10f ? 1 : 0;
        }

        private static bool SamePoint(Vector3 point, float x, float y, float z)
        {
            return point.x == x && point.y == y && point.z == z;
        }

        private static int DefaultSections(float heat)
        {
            return ManagedCrownHeatMap.Sections(DefaultTier(heat), heat, 10f, 40f, 75f, 100f);
        }

        private static int Sections(int tier, float heat)
        {
            return ManagedCrownHeatMap.Sections(tier, heat, 10f, 40f, 75f, 100f);
        }

        private static void FeederTests()
        {
            CrownHeatMapTests();
            SourceRegistryTests();
            TrackerTests();
            BoardTests();
            FeedHeatTests();
            FeedMissionTests();
            HudFeederTests();
        }

        private static void CrownHeatMapTests()
        {
            Section("crown heat map");
            Eq(ManagedCrownHeatMap.MaxSections, ManagedHudMath.MaxHeat, "the map's top is the HUD's nine sections");
            Eq(ManagedCrownHeatMap.SectionsPerTier, ManagedHudMath.SectionsPerCrown, "and a tier owns one crown of the HUD");

            Eq(Sections(0, 0f), 0, "clear with no heat shows nothing");
            Eq(Sections(0, 9.9f), 0, "heat below the wanted threshold shows nothing");
            Eq(Sections(-2, 50f), 0, "a tier below clear shows nothing whatever the heat");
            Eq(Sections(1, float.NaN), 0, "a heat that is not a number shows nothing");

            Eq(Sections(1, 10f), 1, "wanted at its threshold lights one section, so the crowns show");
            Eq(Sections(1, 19.99f), 1, "the first third of the wanted band is one section");
            Eq(Sections(1, 20f), 2, "a heat exactly on the first third falls in the second third");
            Eq(Sections(1, 29.99f), 2, "the second third is two sections");
            Eq(Sections(1, 30f), 3, "a heat exactly on the second third falls in the last third");
            Eq(Sections(1, 39.99f), 3, "the last third of wanted fills the first crown");

            Eq(Sections(2, 40f), 4, "hunted at its threshold lights the first section of the second crown");
            Eq(Sections(2, 50f), 4, "still the first third of a 35 wide band");
            Eq(Sections(2, 52f), 5, "then the second");
            Eq(Sections(2, 64f), 6, "then the third, which fills the second crown");
            Eq(Sections(2, 74.9f), 6, "up to the outlaw threshold");

            Eq(Sections(3, 75f), 7, "outlaw at its threshold lights the first section of the third crown");
            Eq(Sections(3, 84f), 8, "the second");
            Eq(Sections(3, 92f), 9, "the third");
            Eq(Sections(3, 100f), 9, "the most heat the region holds fills every section");
            Eq(Sections(3, 250f), 9, "heat above the most is held at nine");
            Eq(Sections(7, 100f), 9, "a tier above the highest counts as the highest");
            Eq(Sections(7, 76f), 7, "and measures from the outlaw threshold");

            // The whole range, walked in half steps with the region's own tier rule.
            bool monotonic = true, inRange = true, zeroOnlyBelowWanted = true;
            int previous = 0;
            for (float heat = 0f; heat <= 100f; heat += 0.5f)
            {
                int sections = DefaultSections(heat);
                if (sections < previous) { monotonic = false; }
                if (sections < 0 || sections > 9) { inRange = false; }
                if ((sections == 0) != (heat < 10f)) { zeroOnlyBelowWanted = false; }
                previous = sections;
            }

            Check(monotonic, "walking the heat up never lowers the sections");
            Check(inRange, "and every value is between 0 and 9");
            Check(zeroOnlyBelowWanted, "and the HUD is hidden exactly while the player is clear");

            // A region tuned by hand measures its own bands.
            Eq(ManagedCrownHeatMap.Sections(1, 5f, 5f, 20f, 50f, 60f), 1, "a region with wanted at 5 lights a section at 5");
            Eq(ManagedCrownHeatMap.Sections(1, 10f, 5f, 20f, 50f, 60f), 2, "and its second third is 10, a third of 15 past 5");
            Eq(ManagedCrownHeatMap.Sections(1, 15f, 5f, 20f, 50f, 60f), 3, "and its last third 15");
            Eq(ManagedCrownHeatMap.Sections(3, 55f, 5f, 20f, 50f, 60f), 8, "its outlaw band runs from 50 to 60");
            Eq(ManagedCrownHeatMap.Sections(3, 60f, 5f, 20f, 50f, 60f), 9, "and ends full");

            // Thresholds that leave a band no width, which the region allows.
            Eq(ManagedCrownHeatMap.Sections(3, 75f, 10f, 40f, 75f, 75f), 9, "an outlaw band as wide as 0 is full");
            Eq(ManagedCrownHeatMap.Sections(1, 10f, 10f, 10f, 75f, 100f), 3, "so is a band that cannot be reached, without a divide by zero");
            Eq(ManagedCrownHeatMap.Sections(1, 5f, 10f, 10f, 75f, 100f), 3, "and a band with no width is full even for a heat below it");
            int nan = ManagedCrownHeatMap.Sections(2, 50f, float.NaN, float.NaN, float.NaN, float.NaN);
            Check(nan >= 4 && nan <= 6, "thresholds that are not numbers still give a section of the right crown");
        }

        private static void SourceRegistryTests()
        {
            Reset();
            Section("heat source registry");
            Check(ManagedHudHeatSources.Current == null, "there is no heat source at the start");
            ManagedHudHeatSources.Register(null);
            Check(ManagedHudHeatSources.Current == null, "registering null changes nothing");
            var a = new FakeHeat(); var b = new FakeHeat();
            ManagedHudHeatSources.Register(a);
            Check(ReferenceEquals(ManagedHudHeatSources.Current, a), "a registered source is the current one");
            ManagedHudHeatSources.Register(null);
            Check(ReferenceEquals(ManagedHudHeatSources.Current, a), "registering null leaves the current source");
            ManagedHudHeatSources.Register(b);
            Check(ReferenceEquals(ManagedHudHeatSources.Current, b), "the last to register wins");
            ManagedHudHeatSources.Unregister(a);
            Check(ReferenceEquals(ManagedHudHeatSources.Current, b), "unregistering a source that is not current leaves the current one");
            ManagedHudHeatSources.Unregister(null);
            Check(ReferenceEquals(ManagedHudHeatSources.Current, b), "unregistering null leaves it too");
            ManagedHudHeatSources.Unregister(b);
            Check(ManagedHudHeatSources.Current == null, "unregistering the current one clears it");
            ManagedHudHeatSources.Register(a);
            ManagedHudHeatSources.Register(b);
            ManagedHudHeatSources.Unregister(b);
            Check(ManagedHudHeatSources.Current == null, "a displaced source does not come back");
            ManagedHudHeatSources.Register(a);
            CallStatic(typeof(ManagedHudHeatSources), "ResetStatics");
            Check(ManagedHudHeatSources.Current == null, "starting play clears a source left over from an earlier session");

            Section("mission source registry");
            Check(ManagedMissionSources.Current == null, "there is no mission source at the start");
            ManagedMissionSources.Register(null);
            Check(ManagedMissionSources.Current == null, "registering null changes nothing");
            var m1 = new FakeMissions(); var m2 = new FakeMissions();
            ManagedMissionSources.Register(m1);
            Check(ReferenceEquals(ManagedMissionSources.Current, m1), "a registered source is the current one");
            ManagedMissionSources.Register(null);
            Check(ReferenceEquals(ManagedMissionSources.Current, m1), "registering null leaves the current source");
            ManagedMissionSources.Register(m2);
            Check(ReferenceEquals(ManagedMissionSources.Current, m2), "the last to register wins");
            ManagedMissionSources.Unregister(m1);
            Check(ReferenceEquals(ManagedMissionSources.Current, m2), "unregistering a source that is not current leaves the current one");
            ManagedMissionSources.Unregister(null);
            Check(ReferenceEquals(ManagedMissionSources.Current, m2), "unregistering null leaves it too");
            ManagedMissionSources.Unregister(m2);
            Check(ManagedMissionSources.Current == null, "unregistering the current one clears it");
            ManagedMissionSources.Register(m1);
            ManagedMissionSources.Register(m2);
            ManagedMissionSources.Unregister(m2);
            Check(ManagedMissionSources.Current == null, "a displaced source does not come back");
            ManagedMissionSources.Register(m1);
            CallStatic(typeof(ManagedMissionSources), "ResetStatics");
            Check(ManagedMissionSources.Current == null, "starting play clears a source left over from an earlier session");
        }

        private static void TrackerTests()
        {
            Section("mission tracker");
            var t = new ManagedMissionTracker();
            Eq(t.Count, 0, "a new tracker holds nothing");
            Check(t.ActiveId == null, "and has no active mission");
            Eq(t.Title, "Missions", "the title is the default");
            Eq(t.LineCount, 1, "with one line"); Eq(t.LineAt(0), "No active mission", "that says so");
            Eq(t.LineAt(1), "", "a line out of range is empty"); Eq(t.LineAt(-1), "", "and so is a negative index");

            Check(!t.Add(null, "x", null), "a null id adds nothing");
            Check(!t.Add("", "x", null), "and an empty one");
            Eq(t.Count, 0, "so nothing is held");
            Check(t.Add("a", "Deliver the letter", new[] { "Take the letter", "Find the miller", null }), "a mission with an id is added");
            Eq(t.Count, 1, "and held");
            Check(!t.Add("a", "Again", null), "the same id twice is refused");
            Eq(t.Count, 1, "and adds none");
            Eq(t.ActiveId, "a", "the first mission becomes the active one");
            Eq(t.Title, "Deliver the letter", "its title is shown");
            Eq(t.LineCount, 3, "with a line per objective");
            Eq(t.LineAt(0), "Take the letter", "the first");
            Eq(t.LineAt(2), "", "a null objective is an empty line");
            Eq(t.LineAt(3), "", "one past the end is empty");
            Check(t.Has("a") && !t.Has("b") && !t.Has(null) && !t.Has(""), "Has finds only what is held");
            Eq(t.ObjectiveCount("a"), 3, "the objective count"); Eq(t.ObjectiveCount("zz"), 0, "and 0 for an unknown id");

            Check(t.Add("b", null, null), "a mission with no title and no objectives is added");
            Eq(t.ActiveId, "a", "the second does not take over the active mission");
            Check(t.SetActive("b"), "it can be made active");
            Eq(t.Title, "b", "its title is its id"); Eq(t.LineCount, 0, "and it has no lines");
            Check(!t.SetActive("zz"), "an unknown mission cannot be made active"); Eq(t.ActiveId, "b", "and the active one stays");
            Check(t.SetActive("a"), "back to the first");

            Check(!t.CompleteObjective("a", -1) && !t.CompleteObjective("a", 3) && !t.CompleteObjective("zz", 0), "an objective out of range or of an unknown mission is refused");
            Check(t.CompleteObjective("a", 0), "an objective is ticked off");
            Check(t.IsObjectiveDone("a", 0) && !t.IsObjectiveDone("a", 1), "that one only");
            Check(!t.IsObjectiveDone("a", 7) && !t.IsObjectiveDone("zz", 0), "an objective out of range is not done");
            Eq(t.LineAt(0), "Take the letter (done)", "a done objective shows it");
            Check(!t.CompleteObjective("a", 0), "ticking it twice is refused");
            Check(!t.IsComplete("a"), "the mission is open");
            Check(t.CompleteObjective("a", 2), "the last objective but one");
            Check(!t.IsComplete("a"), "still open with one objective left");
            Check(t.CompleteObjective("a", 1), "the last objective");
            Check(t.IsComplete("a"), "completes the mission");
            Eq(t.ActiveId, "b", "and the next open mission takes over");
            Check(!t.CompleteObjective("a", 0), "a complete mission takes no more ticks");
            Check(!t.SetActive("a"), "and cannot be made active again");
            Check(!t.Complete("a"), "or completed again");

            Check(t.Complete("b"), "a mission with no objectives completes by hand");
            Check(t.ActiveId == null, "with none left open there is no active mission");
            Eq(t.Title, "Missions", "the title is the default again"); Eq(t.LineAt(0), "No active mission", "and the line says so");
            Eq(t.Count, 2, "complete missions are still held");

            // Completing by hand ticks every objective.
            t.Add("c", "Third", new[] { "one", "two" });
            Eq(t.ActiveId, "c", "a mission added while none is active becomes active");
            Check(t.Complete("c"), "completed by hand"); Check(t.IsObjectiveDone("c", 0) && t.IsObjectiveDone("c", 1), "ticks its objectives");

            // Remove.
            t.Add("d", "Fourth", new[] { "x" }); t.Add("e", "Fifth", new[] { "y" });
            Eq(t.ActiveId, "d", "d is active");
            Check(!t.Remove("zz"), "removing an unknown mission is refused");
            Check(t.Remove("d"), "removing the active mission");
            Check(!t.Has("d"), "drops it");
            Eq(t.ActiveId, "e", "and the first open one takes over");
            Check(t.Remove("c"), "removing a complete mission"); Eq(t.ActiveId, "e", "does not move the active one");
            Check(t.Remove("e") && t.ActiveId == null, "removing the last open one leaves none active");
            t.Clear();
            Eq(t.Count, 0, "Clear drops them all"); Check(t.ActiveId == null, "and the active one");
            Check(t.Add("a", "Again", null), "an id that was dropped can be used again");

            // A list the caller changes later does not change the mission.
            var list = new List<string> { "keep" };
            t.Clear(); t.Add("k", "K", list); list[0] = "changed"; list.Add("more");
            Eq(t.LineAt(0), "keep", "the objectives are copied, not shared"); Eq(t.LineCount, 1, "with their count");
        }

        private static void BoardTests()
        {
            Reset();
            Section("mission board");
            var go = new GameObject("board");
            ManagedMissionBoard board = go.AddComponent<ManagedMissionBoard>();
            Check(board.Tracker != null, "the tracker exists before the component has started");
            Check(ReferenceEquals(board.Tracker, board.Tracker), "and is the same one each time");
            board.Tracker.Add("early", "Early", new[] { "added from code" });

            SetField(board, "_startingMissions", new List<ManagedMissionEntry>
            {
                new ManagedMissionEntry { Id = "first", Title = "First", Objectives = new[] { "one", "two" } },
                null,
                new ManagedMissionEntry { Id = "", Title = "No id", Objectives = new[] { "x" } },
                new ManagedMissionEntry { Id = "first", Title = "Same id", Objectives = null },
                new ManagedMissionEntry { Id = "second", Title = null, Objectives = null },
            });
            Call(board, "Awake");
            Call(board, "OnEnable");
            Eq(board.Tracker.Count, 3, "the early mission and the two valid entries are held, the null, the empty id and the repeat are skipped");
            Eq(board.Tracker.ActiveId, "early", "the mission added first stays active");
            Check(board.Tracker.Has("first") && board.Tracker.Has("second"), "the Inspector missions are in");
            Eq(board.Tracker.Title, "Early", "the panel shows the active one");
            Check(ReferenceEquals(ManagedMissionSources.Current, board.Tracker), "enabling registers the tracker as the HUD's mission source");

            Call(board, "OnEnable");
            Eq(board.Tracker.Count, 3, "enabling again does not load the Inspector list twice");
            Call(board, "OnDisable");
            Check(ManagedMissionSources.Current == null, "disabling unregisters it");
            Call(board, "OnEnable");
            Check(ReferenceEquals(ManagedMissionSources.Current, board.Tracker), "and enabling registers it again");
            board.Tracker.Remove("first");
            Call(board, "OnDisable"); Call(board, "OnEnable");
            Check(!board.Tracker.Has("first"), "a mission removed in play does not come back when the board is enabled again");

            // A board that was displaced by another source does not clear it when it goes.
            var other = new FakeMissions();
            ManagedMissionSources.Register(other);
            Call(board, "OnDisable");
            Check(ReferenceEquals(ManagedMissionSources.Current, other), "a board that is not the current source leaves the current one alone");

            // Never enabled: OnDisable has nothing to unregister and does not create a tracker out of nothing.
            Reset();
            var idle = new GameObject("idle").AddComponent<ManagedMissionBoard>();
            Call(idle, "OnDisable");
            Check(ManagedMissionSources.Current == null, "a board that was never enabled unregisters nothing");

            // A board with a null Inspector list starts empty.
            var nullList = new GameObject("null list").AddComponent<ManagedMissionBoard>();
            SetField(nullList, "_startingMissions", null);
            Call(nullList, "Awake");
            Eq(nullList.Tracker.Count, 0, "a null Inspector list loads nothing and does not throw");
        }

        private static void FeedHeatTests()
        {
            Section("feed heat");
            World w = NewWorld(true);
            var feed = new ManagedHudFeed(false);
            var state = new ManagedHudState();
            float t = 10f;

            state.SetHeat(4);
            feed.Refresh(state, t);
            Eq(state.HeatSections, 4, "with no heat source the feed leaves the heat alone, so SetHeat from code and the dev keys work");

            var source = new FakeHeat { Value = 5 };
            w.PlayerGo.transform.position = new Vector3(3f, 0f, 4f);
            ManagedHudHeatSources.Register(source);
            feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 5, "a registered source sets the heat");
            Check(SamePoint(source.LastPoint, 3f, 0f, 4f), "and is asked about the point the player stands at");
            w.PlayerGo.transform.position = new Vector3(-8f, 2f, 9f);
            source.Value = 8;
            feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 8, "it follows the source every frame");
            Check(SamePoint(source.LastPoint, -8f, 2f, 9f), "and the player's new position");
            int calls = source.Calls;
            feed.Refresh(state, t += 0.1f);
            Eq(source.Calls, calls + 1, "the source is asked once per refresh");

            source.Value = 40; feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 9, "a value above nine is held at nine");
            source.Value = -3; feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 0, "and a negative one at 0");

            // Without a player there is nowhere to ask about.
            source.Value = 6; feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 6, "back to six");
            calls = source.Calls;
            Object.Destroy(w.PlayerGo);
            feed.Refresh(state, t += 0.1f);
            Eq(state.HeatSections, 0, "a player that is gone clears the heat");
            Eq(source.Calls, calls, "and the source is not asked");

            // The source goes away: the crowns clear once, and the heat is the caller's again.
            w = NewWorld(true);
            feed = new ManagedHudFeed(false);
            state = new ManagedHudState();
            source = new FakeHeat { Value = 7 };
            ManagedHudHeatSources.Register(source);
            feed.Refresh(state, 20f);
            Eq(state.HeatSections, 7, "a source is read in a fresh scene");
            ManagedHudHeatSources.Unregister(source);
            feed.Refresh(state, 20.1f);
            Eq(state.HeatSections, 0, "when the source goes away the heat clears, so crowns that nothing owns do not stay lit");
            state.SetHeat(3);
            feed.Refresh(state, 20.2f);
            Eq(state.HeatSections, 3, "and after that the heat is left alone again");

            // A feed that never saw a source does not clear what a caller set.
            feed = new ManagedHudFeed(false);
            state = new ManagedHudState();
            state.SetHeat(2);
            feed.Refresh(state, 30f);
            Eq(state.HeatSections, 2, "a feed that never read a source leaves a set heat alone");
        }

        private static void FeedMissionTests()
        {
            Section("feed missions");
            World w = NewWorld(true);
            var feed = new ManagedHudFeed(false);
            var state = new ManagedHudState();
            float t = 10f;

            state.SetMissions("Mine", new[] { "set by hand" });
            feed.Refresh(state, t);
            Eq(state.MissionTitle, "Mine", "with no mission source the feed leaves the panel alone");
            Eq(state.MissionLineAt(0), "set by hand", "and its lines");

            var tracker = new ManagedMissionTracker();
            tracker.Add("m", "Find the key", new[] { "Search the cellar", "Ask the cook" });
            ManagedMissionSources.Register(tracker);
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionTitle, "Find the key", "a registered source sets the title");
            Eq(state.MissionLineCount, 2, "and the lines"); Eq(state.MissionLineAt(1), "Ask the cook", "in order");

            int version = state.MissionsVersion;
            feed.Refresh(state, t += 0.1f); feed.Refresh(state, t += 0.1f);
            Eq(state.MissionsVersion, version, "refreshing with nothing changed does not count as a change");

            tracker.CompleteObjective("m", 0);
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionLineAt(0), "Search the cellar (done)", "a change in the source shows on the next refresh");
            Eq(state.MissionsVersion, version + 1, "and counts once");

            tracker.Add("long", "Long", new[] { "1", "2", "3", "4", "5", "6" });
            tracker.SetActive("long");
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionLineCount, ManagedHudState.MaxMissionLines, "more lines than the panel holds are cut to what it holds");
            Eq(state.MissionLineAt(3), "4", "the first four stay");

            // A source that returns nulls.
            var rough = new FakeMissions { TitleText = null };
            rough.Lines.Add(null); rough.Lines.Add("kept");
            ManagedMissionSources.Register(rough);
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionTitle, "", "a null title is empty"); Eq(state.MissionLineCount, 2, "both lines are kept"); Eq(state.MissionLineAt(0), "", "a null line is empty");

            // The source goes away: the panel shows the empty state once, then belongs to the caller again.
            ManagedMissionSources.Unregister(rough);
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionTitle, "Missions", "when the source goes away the title is the default");
            Eq(state.MissionLineCount, 1, "with one line"); Eq(state.MissionLineAt(0), "No active mission", "that says so");
            state.SetMissions("Again", new[] { "by hand" });
            feed.Refresh(state, t += 0.1f);
            Eq(state.MissionTitle, "Again", "and after that the panel is left alone again");

            feed = new ManagedHudFeed(false);
            state = new ManagedHudState();
            state.SetMissions("Kept", null);
            feed.Refresh(state, 50f);
            Eq(state.MissionTitle, "Kept", "a feed that never read a source leaves a set panel alone");

            Section("hud state missions version");
            state = new ManagedHudState();
            Eq(state.MissionsVersion, 0, "the version starts at 0");
            state.SetMissions("", null);
            Eq(state.MissionsVersion, 0, "setting what is shown already does not count");
            state.SetMissions("A", new[] { "x", "y" });
            Eq(state.MissionsVersion, 1, "a new title and lines count once");
            state.SetMissions("A", new[] { "x", "y" });
            Eq(state.MissionsVersion, 1, "the same again does not");
            state.SetMissions("A", new[] { "x", "z" });
            Eq(state.MissionsVersion, 2, "one line different does");
            state.SetMissions("A", new[] { "x" });
            Eq(state.MissionsVersion, 3, "one line fewer does");
            state.SetMissions("B", new[] { "x" });
            Eq(state.MissionsVersion, 4, "a different title does");
            state.SetMissions("B", new[] { "x", "q", "q", "q", "q", "q" });
            Eq(state.MissionLineCount, ManagedHudState.MaxMissionLines, "lines beyond the limit are cut");
            int cut = state.MissionsVersion;
            state.SetMissions("B", new[] { "x", "q", "q", "q", "extra that is cut" });
            Eq(state.MissionsVersion, cut, "and a change only in the lines that are cut does not count");
            state.SetMissions("B", new string[] { null });
            Eq(state.MissionLineAt(0), "", "a null line is empty");
            int empty = state.MissionsVersion;
            state.SetMissions("B", new[] { "" });
            Eq(state.MissionsVersion, empty, "and equals an empty one");
            state.SetMissions(null, null);
            Eq(state.MissionTitle, "", "a null title is empty"); Eq(state.MissionLineCount, 0, "a null list clears the lines");
        }

        private static void HudFeederTests()
        {
            Section("hud follows the feeders");
            World w = NewWorld(true);
            ManagedPlayerItemBag bag;
            ManagedHud hud = NewHud(w, out bag);
            float t = 1f;
            Func<string> body = () => ((ManagedHudText)GetField(hud, "_missionBody")).Text;
            Func<string> title = () => ((ManagedHudText)GetField(hud, "_missionTitle")).Text;

            HudFrame(hud, t += 0.1f);
            Eq(title(), "Missions", "the panel starts with the default title"); Eq(body(), "No active mission", "and line");

            // The panel must redraw when only the feed changed the state, which no call on the HUD did.
            var tracker = new ManagedMissionTracker();
            tracker.Add("m", "Find the key", new[] { "Search the cellar", "Ask the cook" });
            ManagedMissionSources.Register(tracker);
            HudFrame(hud, t += 0.1f);
            Eq(title(), "Find the key", "the panel shows the title a feed copied");
            Eq(body(), "Search the cellar\nAsk the cook", "and the lines");
            tracker.CompleteObjective("m", 1);
            HudFrame(hud, t += 0.1f);
            Eq(body(), "Search the cellar\nAsk the cook (done)", "a change in the tracker reaches the panel");

            // A call on the HUD still redraws, while no source is registered.
            ManagedMissionSources.Unregister(tracker);
            HudFrame(hud, t += 0.1f);
            Eq(title(), "Missions", "when the source goes the panel is empty again");
            hud.SetMissions("By hand", new[] { "a", "b" });
            HudFrame(hud, t += 0.1f);
            Eq(title(), "By hand", "SetMissions on the HUD still redraws"); Eq(body(), "a\nb", "with its lines");
            hud.SetMissions("By hand", new[] { "a", "b" });
            HudFrame(hud, t += 0.1f);
            Eq(body(), "a\nb", "setting the same again leaves it drawn");

            // The crowns.
            GameObject crowns = Named("Crowns");
            Check(crowns != null && !crowns.activeSelf, "the crowns are hidden while the player is not wanted");
            Image[] lit = (Image[])GetField(hud, "_crownLit");
            var heat = new FakeHeat { Value = 4 };
            ManagedHudHeatSources.Register(heat);
            HudFrame(hud, t += 0.1f);
            Check(crowns.activeSelf, "a heat source above 0 shows the crowns");
            Near(lit[0].fillAmount, 1f, "the first crown is full at four sections");
            Near(lit[1].fillAmount, 1f / 3f, "the second shows its first third");
            Near(lit[2].fillAmount, 0f, "the third is empty");
            heat.Value = 9;
            HudFrame(hud, t += 0.1f);
            Near(lit[2].fillAmount, 1f, "nine sections fill every crown");
            heat.Value = 0;
            HudFrame(hud, t += 0.1f);
            Check(!crowns.activeSelf, "heat back to 0 hides them again");
            heat.Value = 2;
            HudFrame(hud, t += 0.1f);
            hud.SetHeat(8);
            HudFrame(hud, t += 0.1f);
            Eq(hud.State.HeatSections, 2, "while a source is registered the feed overwrites a SetHeat from code");
            ManagedHudHeatSources.Unregister(heat);
            HudFrame(hud, t += 0.1f);
            Check(!crowns.activeSelf, "and the crowns clear when the source goes");
            hud.SetHeat(5);
            HudFrame(hud, t += 0.1f);
            Check(crowns.activeSelf && hud.State.HeatSections == 5, "after that SetHeat works again");
        }
    }
}
