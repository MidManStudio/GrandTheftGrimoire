// Checks for ManagedQuestText and ManagedQuestFlagSource. Verification harness only.
using System;
using MidManStudio.Questly.Generated;

namespace MidManStudio.Gtg.Managed.Quests.Tests
{
    public static partial class Tests
    {
        private static void TextChecks()
        {
            ManagedQuestText text = new ManagedQuestText();
            Eq(text.Count, 0, "a new table is empty");
            Eq(text.Version, 0, "a new table has version 0");
            Eq(text.Load(null), 0, "null source takes nothing");
            Eq(text.Load(""), 0, "empty source takes nothing");
            Eq(text.Version, 0, "a load that took nothing keeps the version");

            int taken = text.Load(
                "# a comment\n" +
                "// another comment\n" +
                "\n" +
                "   \n" +
                "a.title=First\n" +
                "  b.title  =  Second one  \n" +
                "c.title=has=equals=signs\n" +
                "no equals sign here\n" +
                "=no key\n" +
                "d.title=\n" +
                "e.title=   \n" +
                "f.title=Windows line\r\n" +
                "a.title=First again\n");
            Eq(taken, 5, "five lines are taken, a duplicate counts as taken");
            Eq(text.Count, 4, "four distinct keys");
            Eq(text.Version, 1, "one load, one version step");

            string value;
            Check(text.TryGet("a.title", out value) && value == "First again", "the last duplicate wins");
            Check(text.TryGet("b.title", out value) && value == "Second one", "spaces around key and text are dropped");
            Check(text.TryGet("c.title", out value) && value == "has=equals=signs", "the line splits on the first equals sign only");
            Check(text.TryGet("f.title", out value) && value == "Windows line", "a carriage return is dropped");
            Check(!text.TryGet("d.title", out value), "a key with empty text is not held");
            Check(!text.TryGet("e.title", out value), "a key with blank text is not held");
            Check(!text.TryGet("", out value) && value == null, "an empty key is never found");
            Check(!text.TryGet(null, out value) && value == null, "a null key is never found");
            Check(!text.TryGet("missing", out value), "a missing key is not found");

            Eq(text.Resolve("b.title"), "Second one", "resolve returns the text");
            Eq(text.Resolve("missing.key"), "missing.key", "resolve returns the key when it has no text");
            Eq(text.Resolve(null), "", "resolve of null is empty");
            Eq(text.Resolve(""), "", "resolve of empty is empty");

            ManagedQuestText bom = new ManagedQuestText();
            Eq(bom.Load("﻿first.key=after a bom\nsecond.key=ok"), 2, "a byte order mark before the first line is ignored");
            Check(bom.TryGet("first.key", out value) && value == "after a bom", "the key after a bom is clean");
            ManagedQuestText bomSpace = new ManagedQuestText();
            Eq(bomSpace.Load("﻿  spaced.key = x"), 1, "a bom before spaces is ignored");
            Check(bomSpace.TryGet("spaced.key", out value) && value == "x", "the key after a bom and spaces is clean");

            int before = text.Version;
            text.Load("z=1");
            Eq(text.Version, before + 1, "another load is another step");
            text.Clear();
            Eq(text.Count, 0, "clear drops every key");
            Eq(text.Version, before + 2, "clear that dropped something is a step");
            text.Clear();
            Eq(text.Version, before + 2, "clear of an empty table is not a step");
        }

        private static void FlagSourceChecks()
        {
            Throws<ArgumentNullException>(delegate { new ManagedQuestFlagSource(null); }, "a null runtime is refused");

            Rig rig = NewRig(true);
            ManagedQuestFlagSource flags = new ManagedQuestFlagSource(rig.Runtime);
            Check(!flags.IsSet(null), "a null flag is never set");
            Check(!flags.IsSet(""), "an empty flag is never set");
            Check(!flags.IsSet("no_such_quest"), "an unknown quest is not set");
            Check(!flags.IsSet("collect_wood"), "an AVAILABLE quest is not set");
            rig.Runtime.StartQuest("collect_wood");
            Check(!flags.IsSet("collect_wood"), "an ACTIVE quest is not set");
            rig.Runtime.ReportProgress("collect_wood", "obj_counter", 5f);
            Eq(rig.Runtime.GetQuestState("collect_wood"), QuestState.COMPLETED, "the fixture quest completes");
            Check(flags.IsSet("collect_wood"), "a COMPLETED quest is set");
            Check(!flags.IsSet("deliver_package"), "the quest it unlocks is not set");
            rig.Runtime.ForceState("collect_wood", QuestState.FAILED);
            Check(!flags.IsSet("collect_wood"), "a quest that leaves COMPLETED is not set any more");
        }
    }
}
