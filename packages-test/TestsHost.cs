// Checks for ManagedQuestHost. Verification harness only. Unity calls the lifecycle methods of a component,
// so the checks call them by reflection in the order Unity does.
using System;
using System.Collections.Generic;
using System.Reflection;
using MidManStudio.Gtg.Managed.Missions;
using MidManStudio.Mdix.Unity;
using MidManStudio.Questly.Core;
using MidManStudio.Questly.Generated;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Quests.Tests
{
    public static partial class Tests
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void Call(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, AnyInstance);
            if (info == null)
            {
                throw new InvalidOperationException("no method " + method);
            }

            info.Invoke(target, null);
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, AnyInstance);
            if (info == null)
            {
                throw new InvalidOperationException("no field " + field);
            }

            info.SetValue(target, value);
        }

        private static void ResetSources()
        {
            typeof(ManagedMissionSources).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Invoke(null, null);
            UnityEngine.Debug.Reset();
        }

        private static void Enable(ManagedQuestHost host)
        {
            host.isActiveAndEnabled = true;
            Call(host, "OnEnable");
        }

        private static void Disable(ManagedQuestHost host)
        {
            Call(host, "OnDisable");
            host.isActiveAndEnabled = false;
        }

        private static QuestDataAsset BakedAsset()
        {
            QuestDataAsset asset = ScriptableObject.CreateInstance<QuestDataAsset>();
            BakedQuest quest = new BakedQuest();
            quest.identity.id = "collect_wood";
            quest.identity.type = QuestType.SIDE;
            quest.identity.titleKey = "quest.collect_wood.title";
            BakedObjective objective = new BakedObjective();
            objective.id = "obj_counter";
            objective.kind = ObjectiveKind.COUNTER;
            objective.labelKey = "quest.collect_wood.obj_counter";
            objective.target = 5f;
            quest.objectives.Add(objective);
            asset.quests.Add(quest);
            return asset;
        }

        private static ManagedQuestHost LoadedHost()
        {
            ManagedQuestHost host = new ManagedQuestHost();
            SetField(host, "_baked", BakedAsset());
            SetField(host, "_text", new TextAsset(TextSource));
            SetField(host, "_startAtLoad", new[] { "collect_wood" });
            Call(host, "Awake");
            return host;
        }

        private static void HostChecks()
        {
            // Attributes that decide when the host runs.
            DefaultExecutionOrderAttribute order = typeof(ManagedQuestHost).GetCustomAttribute<DefaultExecutionOrderAttribute>();
            Check(order != null && order.Order == 100, "the host runs at order 100, after scripts that report progress in Update");
            Check(typeof(ManagedQuestHost).GetCustomAttribute<DisallowMultipleComponent>() != null, "one host per object");

            // Nothing assigned.
            ResetSources();
            ManagedQuestHost host = new ManagedQuestHost();
            Call(host, "Awake");
            Check(UnityEngine.Debug.Warnings.Count == 1 && UnityEngine.Debug.Warnings[0].Contains("No quest data"), "a warning says no data is assigned");
            Check(!host.IsLoaded && host.Runtime == null && host.Missions == null && host.Flags == null, "nothing is loaded");
            Check(!host.Track("collect_wood"), "track with nothing loaded is false");
            Check(!host.Restore(new QuestProgressSnapshot()), "restore with nothing loaded is false");
            Enable(host);
            Call(host, "Update");
            Check(ManagedMissionSources.Current == null, "no source is registered");
            Disable(host);
            Call(host, "OnDestroy");

            // Baked data.
            ResetSources();
            host = LoadedHost();
            Check(host.IsLoaded && host.Runtime != null && host.Missions != null && host.Flags != null, "baked data loads");
            Eq(UnityEngine.Debug.Warnings.Count + UnityEngine.Debug.Errors.Count, 0, "with no log lines");
            Eq(host.Words.Count, 11, "the text asset is read");
            Eq(host.Runtime.GetQuestState("collect_wood"), QuestState.ACTIVE, "the quest of the start list is started");
            Eq(Panel(host.Missions), "Collect wood | Gather logs 0/5", "the panel is ready before the first frame");
            Check(ManagedMissionSources.Current == null, "a disabled host registers nothing");
            Enable(host);
            Check(ReferenceEquals(ManagedMissionSources.Current, host.Missions), "enabling registers the source");

            host.Runtime.ReportProgress("collect_wood", "obj_counter", 2f);
            Eq(Panel(host.Missions), "Collect wood | Gather logs 0/5", "the panel waits for the frame");
            Call(host, "Update");
            Eq(Panel(host.Missions), "Collect wood | Gather logs 2/5", "the frame brings progress in");
            Check(!host.Flags.IsSet("collect_wood"), "the flag is not set yet");
            host.Runtime.ReportProgress("collect_wood", "obj_counter", 3f);
            Call(host, "Update");
            Eq(Panel(host.Missions), ManagedMissionTracker.DefaultTitle + " | " + ManagedMissionTracker.NoMissionLine, "a completed quest leaves the panel");
            Check(host.Flags.IsSet("collect_wood"), "the flag is set");

            host.Words.Load("quest.deliver_package.title=Deliver");
            Check(host.Track("deliver_package") == false, "a quest that is not ACTIVE is not tracked");

            Disable(host);
            Check(ManagedMissionSources.Current == null, "disabling unregisters the source");
            Check(host.IsLoaded, "and keeps the progress");
            Enable(host);
            Check(ReferenceEquals(ManagedMissionSources.Current, host.Missions), "enabling again registers it again");
            Eq(host.Runtime.GetQuestState("collect_wood"), QuestState.COMPLETED, "with the same progress");

            ManagedMissionTracker other = new ManagedMissionTracker();
            ManagedMissionSources.Register(other);
            Disable(host);
            Check(ReferenceEquals(ManagedMissionSources.Current, other), "a source registered later is not removed when the host is disabled");

            Call(host, "OnDestroy");
            Check(!host.IsLoaded && host.Missions == null, "destroying releases the runtime");
            Call(host, "OnDestroy");

            // New words show on the next frame.
            ResetSources();
            host = LoadedHost();
            Enable(host);
            host.Words.Load("quest.collect_wood.title=Hauling");
            Call(host, "Update");
            Eq(host.Missions.Title, "Hauling", "new words show on the next frame");

            // Track and Restore on a loaded host.
            ResetSources();
            host = LoadedHost();
            Check(host.Track("collect_wood"), "track an ACTIVE quest");
            Check(!host.Restore(null), "restore of null is false");
            ManagedQuestHost other2 = LoadedHost();
            other2.Runtime.ReportProgress("collect_wood", "obj_counter", 4f);
            QuestProgressSnapshot snapshot = other2.Runtime.CaptureState();
            Check(host.Restore(snapshot), "restore with a snapshot is true");
            Eq(Panel(host.Missions), "Collect wood | Gather logs 4/5", "the panel shows the restored progress at once");

            // The baked asset is used before the .mdix asset.
            ResetSources();
            host = new ManagedQuestHost();
            MdixAsset mdix = new MdixAsset();
            SetField(host, "_baked", BakedAsset());
            SetField(host, "_definitions", mdix);
            Call(host, "Awake");
            Check(host.IsLoaded, "loaded");
            Eq(mdix.LoadCalls, 0, "the .mdix asset is not touched when baked data is set");

            // A .mdix asset that does not load.
            ResetSources();
            host = new ManagedQuestHost();
            mdix = new MdixAsset();
            mdix.name = "quests_asset";
            SetField(host, "_definitions", mdix);
            Call(host, "Awake");
            Check(!host.IsLoaded, "a failed load leaves the host empty");
            Check(UnityEngine.Debug.Errors.Count == 1 && UnityEngine.Debug.Errors[0].Contains("quests_asset") && UnityEngine.Debug.Errors[0].Contains("did not load"), "the error names the asset");
            Check(UnityEngine.Debug.Errors[0].Contains("stand-in asset has no source"), "and carries the mdix message");

            // A native library that is missing throws instead of returning a result.
            ResetSources();
            host = new ManagedQuestHost();
            mdix = new MdixAsset();
            mdix.name = "quests_asset";
            mdix.ThrowOnLoad = true;
            SetField(host, "_definitions", mdix);
            Call(host, "Awake");
            Check(!host.IsLoaded, "a throwing load leaves the host empty");
            Check(UnityEngine.Debug.Errors.Count == 1 && UnityEngine.Debug.Errors[0].Contains("failed") && UnityEngine.Debug.Errors[0].Contains("mdix_ffi stand-in"), "the exception message is logged");
            Call(host, "Update");

            // Attach.
            ResetSources();
            host = new ManagedQuestHost();
            Call(host, "Awake");
            Enable(host);
            Rig one = NewRig(true);
            Rig two = NewRig(true);
            Throws<ArgumentNullException>(delegate { host.Attach(null, one.Table); }, "attach refuses a null runtime");
            Throws<ArgumentNullException>(delegate { host.Attach(one.Runtime, null); }, "attach refuses a null table");
            int listeners = one.Table.Subscribers;
            host.Attach(one.Runtime, one.Table);
            Check(host.IsLoaded && ReferenceEquals(host.Runtime, one.Runtime), "attach loads the runtime");
            Check(ReferenceEquals(ManagedMissionSources.Current, host.Missions), "an enabled host registers at once");
            ManagedQuestMissionSource first = host.Missions;
            Eq(one.Table.Subscribers, listeners + 1, "the new source listens to the table");
            host.Attach(two.Runtime, two.Table);
            Eq(one.Table.Subscribers, listeners, "the replaced source stopped listening");
            Check(!ReferenceEquals(host.Missions, first) && ReferenceEquals(ManagedMissionSources.Current, host.Missions), "the new source is the one registered");
            Disable(host);
            host.Attach(one.Runtime, one.Table);
            Check(ManagedMissionSources.Current == null, "a disabled host does not register on attach");
            Enable(host);
            Check(ReferenceEquals(ManagedMissionSources.Current, host.Missions), "and registers when enabled");
            Call(host, "OnDestroy");
            Eq(one.Table.Subscribers, listeners, "destroying stops the source listening");
        }
    }
}
