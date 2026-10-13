using System;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private sealed class Ctl
        {
            public ManagedBuildRules Rules = new ManagedBuildRules();
            public ManagedPlacedStore Store = NewStore();
            public FakeProbe Probe = new FakeProbe();
            public ManagedBuildInventory Stock = new ManagedBuildInventory();
            public ManagedBlueprintBook Book = new ManagedBlueprintBook();
            public ManagedPlacementController C;
            public int StateChanges, ResultChanges, PlacedEvents, Returned;
            public List<int> PlacedIds = new List<int>();

            public Ctl(IManagedBuildStock stock = null)
            {
                Rules.MaxReach = 0f;
                C = new ManagedPlacementController(Rules, Store, Probe, stock ?? Stock, Book);
                C.StateChanged += () => StateChanges++;
                C.ResultChanged += r => ResultChanges++;
                C.Placed += id => { PlacedEvents++; PlacedIds.Add(id); };
                C.ReturnedToStock += d => Returned++;
            }

            public void Aim(float x, float z) { C.SetAim(true, new Vector3(x, 0f, z), Vector3.zero); }
        }

        private static void ControllerTests()
        {
            Section("controller");
            ManagedPlacementStatus refusal;

            // Refusals.
            var c = new Ctl();
            Check(!c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal), "no stock refuses");
            Eq(refusal, ManagedPlacementStatus.MissingItems, "reason is missing items");
            Check(!c.C.BeginPlacing(ManagedPlacementSource.Blueprint, "crafting_table", false, out refusal), "locked blueprint refuses");
            Eq(refusal, ManagedPlacementStatus.NotUnlocked, "reason is not unlocked");
            Check(!c.C.BeginPlacing(ManagedPlacementSource.Stock, "nope", false, out refusal), "unknown id refuses");
            Check(!c.C.BeginPlacing(ManagedPlacementSource.Carried, "crafting_table", false, out refusal), "carried is not a start source");
            Check(!c.C.BeginPlacing(ManagedPlacementSource.None, "crafting_table", false, out refusal), "none is not a start source");
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "still idle after refusals");
            Eq(c.StateChanges, 0, "no state change after refusals");
            Check(!c.C.Result.IsValid, "an idle controller never reports a valid spot");
            Eq(c.C.Result.Status, ManagedPlacementStatus.NoAim, "idle result is NoAim");
            c.C.Rotate(1); c.C.Cancel(); c.C.Stash(); c.C.ArmForClick(); c.C.SetAim(true, Vector3.zero, Vector3.zero);
            Eq(c.StateChanges, 0, "calls while idle do nothing");
            int pid;
            Check(!c.C.TryCommit(out pid) && pid == -1, "commit while idle fails");
            Check(!c.C.BeginCarry(5), "carry of an unknown piece fails");

            // Click placing from stock, stock of two.
            c = new Ctl();
            c.Stock.Give("crafting_table", 2);
            Check(c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal), "begin from stock");
            Eq(c.C.Mode, ManagedPlacementMode.Placing, "placing"); Eq(c.C.Source, ManagedPlacementSource.Stock, "source stock");
            Eq(c.C.Definition.Id, "crafting_table", "definition"); Check(!c.C.CommitOnRelease, "click mode");
            Eq(c.C.Result.Status, ManagedPlacementStatus.NoAim, "no aim yet");
            Check(!c.C.TryCommit(out pid), "cannot commit without aim");
            c.Aim(2f, 2f);
            Check(c.C.Result.IsValid, "valid after aim");
            Check(c.C.TryCommit(out pid), "commit"); Eq(pid, 0, "first piece id");
            Eq(c.Stock.GetCount("crafting_table"), 1, "one spent");
            Eq(c.C.Mode, ManagedPlacementMode.Placing, "keeps placing while stock remains");
            Eq(c.PlacedEvents, 1, "placed event");
            Check(!c.C.TryCommit(out pid), "same spot is now occupied");
            Eq(c.C.Result.Status, ManagedPlacementStatus.Occupied, "result went red for the occupied spot");
            c.Aim(6f, 2f);
            Check(c.C.TryCommit(out pid), "second commit"); Eq(c.Stock.GetCount("crafting_table"), 0, "stock empty");
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "leaves when the stock is empty");
            Eq(c.Store.Count, 2, "two pieces placed");

            // Drag placing leaves after one even with stock left.
            c = new Ctl();
            c.Stock.Give("crafting_table", 3);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", true, out refusal);
            Check(c.C.CommitOnRelease, "drag mode"); c.Aim(2f, 2f);
            Check(c.C.TryCommit(out pid), "drag commit");
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "a drag places one and leaves");
            Eq(c.Stock.GetCount("crafting_table"), 2, "two left");

            // Armed for click.
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", true, out refusal);
            int sc = c.StateChanges;
            c.C.ArmForClick(); Check(!c.C.CommitOnRelease, "armed"); Eq(c.StateChanges, sc + 1, "arming raises state change");
            c.C.ArmForClick(); Eq(c.StateChanges, sc + 1, "arming twice raises once");
            c.C.Cancel();

            // Invalid spot refuses and spends nothing.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Probe.Blocked = (a, b) => true;
            c.Aim(2f, 2f);
            Eq(c.C.Result.Status, ManagedPlacementStatus.Blocked, "blocked spot");
            Check(!c.C.TryCommit(out pid), "commit refused"); Eq(c.Stock.GetCount("crafting_table"), 1, "nothing spent"); Eq(c.Store.Count, 0, "nothing stored");

            // Blueprint without cost.
            c = new Ctl();
            c.Book.Unlock("crafting_table");
            Check(c.C.BeginPlacing(ManagedPlacementSource.Blueprint, "crafting_table", false, out refusal), "blueprint begins");
            c.Aim(2f, 2f); c.C.TryCommit(out pid);
            Eq(c.C.Mode, ManagedPlacementMode.Placing, "free blueprint keeps placing");
            c.Aim(6f, 2f); c.C.TryCommit(out pid);
            c.Aim(10f, 2f); Check(c.C.TryCommit(out pid), "unlimited placements");
            Eq(c.Store.Count, 3, "three placed for free");

            // Blueprint with cost.
            var costly = ManagedBuildableCatalog.Get(Craft);
            var saved = costly.Cost;
            costly.Cost = new[] { new ManagedBuildCostLine("wood", 2), new ManagedBuildCostLine("nail", 1) };
            try
            {
                c = new Ctl();
                c.Book.Unlock("crafting_table");
                Check(!c.C.BeginPlacing(ManagedPlacementSource.Blueprint, "crafting_table", false, out refusal), "cannot afford");
                Eq(refusal, ManagedPlacementStatus.MissingItems, "reason missing items");
                c.Stock.Give("wood", 3); c.Stock.Give("nail", 1);
                Check(c.C.BeginPlacing(ManagedPlacementSource.Blueprint, "crafting_table", false, out refusal), "can afford");
                c.Aim(2f, 2f);
                Check(c.C.TryCommit(out pid), "paid commit");
                Eq(c.Stock.GetCount("wood"), 1, "wood spent"); Eq(c.Stock.GetCount("nail"), 0, "nail spent");
                Eq(c.C.Mode, ManagedPlacementMode.Idle, "leaves when it cannot afford another");

                // One line short at commit time: nothing is taken and the piece is not kept.
                c = new Ctl();
                c.Book.Unlock("crafting_table");
                c.Stock.Give("wood", 2); c.Stock.Give("nail", 1);
                c.C.BeginPlacing(ManagedPlacementSource.Blueprint, "crafting_table", false, out refusal);
                c.Aim(2f, 2f);
                c.Stock.TryTake("nail", 1);
                c.Aim(2f, 2f);
                Eq(c.C.Result.Status, ManagedPlacementStatus.MissingItems, "the ghost turns red when the cost can no longer be paid");
                Check(!c.C.TryCommit(out pid), "commit refused"); Eq(c.Stock.GetCount("wood"), 2, "wood untouched"); Eq(c.Store.Count, 0, "no piece");
            }
            finally { costly.Cost = saved; }

            // Pay failure rolls the piece back.
            var refusing = new RefusingStock();
            refusing.Give("crafting_table", 1);
            c = new Ctl(refusing);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Aim(2f, 2f);
            refusing.RefuseTake = true;
            Check(!c.C.TryCommit(out pid), "a refused payment fails the commit");
            Eq(c.Store.Count, 0, "the added piece is rolled back"); Check(c.Store.Grid.AreFree(0, 0, 40, 40), "and its cells are free");
            Eq(c.PlacedEvents, 0, "no placed event");

            // Stock disappears while placing.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Aim(2f, 2f); Check(c.C.Result.IsValid, "valid first");
            c.Stock.TryTake("crafting_table", 1);
            c.Aim(2f, 2f);
            Eq(c.C.Result.Status, ManagedPlacementStatus.MissingItems, "red when the stock was spent elsewhere");

            // Rotation and remembered quarter.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Aim(2f, 2f);
            ManagedPlacementResult before = c.C.Result;
            c.C.Rotate(1); Eq(c.C.Quarter, 1, "rotated");
            c.C.Rotate(-1); c.C.Rotate(-1); Eq(c.C.Quarter, 3, "wraps below zero");
            c.C.Rotate(2); Eq(c.C.Quarter, 1, "wraps above three");
            c.C.Rotate(0); Eq(c.C.Quarter, 1, "zero does nothing");
            c.C.Cancel();
            c.Stock.Give("butchery_station", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "butchery_station", false, out refusal);
            Eq(c.C.Quarter, 1, "the next piece starts with the last rotation");
            c.C.Cancel();

            // Result change events fire on changes only.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            int rc = c.ResultChanges;
            c.Aim(2f, 2f); Eq(c.ResultChanges, rc + 1, "first aim changes the result");
            c.Aim(2f, 2f); Eq(c.ResultChanges, rc + 1, "same aim does not raise again");
            c.Aim(2.1f, 2.1f); Eq(c.ResultChanges, rc + 1, "an aim inside the same cell does not raise");
            c.Aim(2.6f, 2.1f); Eq(c.ResultChanges, rc + 2, "a new cell raises");

            // Carrying.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.C.Rotate(1); c.Aim(2f, 2f); c.C.TryCommit(out pid);
            ManagedPlacedPiece original; c.Store.TryGet(pid, out original);
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "idle after the last one");

            Check(c.C.BeginCarry(pid), "carry"); Eq(c.C.Source, ManagedPlacementSource.Carried, "carried source");
            Eq(c.Store.Count, 0, "the piece left the store"); Check(c.Store.Grid.AreFree(original.CellX, original.CellZ, 2, 3), "its cells are free");
            Eq(c.C.Quarter, 1, "carried piece keeps its rotation");
            Check(!c.C.BeginCarry(pid), "cannot carry while placing");
            c.Aim(2f, 2f);
            Check(c.C.Result.IsValid, "its own old spot is valid for it");
            c.C.Cancel();
            Eq(c.Store.Count, 1, "cancel puts it back");
            ManagedPlacedPiece back; Check(c.Store.TryGet(0, out back), "restored with an id");
            Eq(back.CellX, original.CellX, "same cell x"); Eq(back.CellZ, original.CellZ, "same cell z"); Eq(back.Quarter, 1, "same quarter"); Near(back.GroundY, original.GroundY, "same ground");
            Eq(c.Stock.GetCount("crafting_table"), 0, "carrying and cancelling does not touch the stock");

            // Carry, move, place.
            c.C.BeginCarry(0);
            c.Aim(10f, 2f);
            Check(c.C.TryCommit(out pid), "put down elsewhere");
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "a carried piece leaves after one placement");
            Eq(c.Stock.GetCount("crafting_table"), 0, "moving a piece is free"); Eq(c.Store.Count, 1, "still one piece");
            ManagedPlacedPiece moved; c.Store.TryGet(pid, out moved);
            Near(c.Store.Grid.CenterX(moved.CellX, 2), 10f, "it stands where it was dropped", 0.3f);

            // Stash.
            c.C.BeginCarry(pid);
            c.C.Stash();
            Eq(c.Stock.GetCount("crafting_table"), 1, "stash gives the station back"); Eq(c.Store.Count, 0, "and the world is empty");
            Eq(c.C.Mode, ManagedPlacementMode.Idle, "idle after stash");
            // Stash of a stock-placing piece only cancels.
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.C.Stash();
            Eq(c.Stock.GetCount("crafting_table"), 1, "stash of an aimed station does not duplicate"); Eq(c.C.Mode, ManagedPlacementMode.Idle, "idle");

            // Starting another placement while carrying puts the carried piece back first.
            c = new Ctl();
            c.Stock.Give("crafting_table", 1); c.Stock.Give("butchery_station", 1);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Aim(2f, 2f); c.C.TryCommit(out pid);
            c.C.BeginCarry(pid);
            Check(c.C.BeginPlacing(ManagedPlacementSource.Stock, "butchery_station", false, out refusal), "begin while carrying");
            Eq(c.Store.Count, 1, "the carried piece went back to the world"); Eq(c.C.Definition.Id, "butchery_station", "now placing the new station");

            // Occupied by another piece while carrying leaves the origin restorable.
            c = new Ctl();
            c.Stock.Give("crafting_table", 2);
            c.C.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out refusal);
            c.Aim(2f, 2f); c.C.TryCommit(out pid);
            c.Aim(6f, 2f); c.C.TryCommit(out pid);
            ManagedPlacedPiece p0; c.Store.TryGet(0, out p0);
            c.C.BeginCarry(0);
            c.Aim(6f, 2f);
            Eq(c.C.Result.Status, ManagedPlacementStatus.Occupied, "cannot drop onto the other table");
            c.C.Cancel();
            Eq(c.Store.Count, 2, "both tables stand again"); Check(BucketsConsistent(c.Store), "store consistent after the round trip");

            // Idle results stay NoAim after leaving, so the ghost is not drawn.
            Eq(c.C.Result.Status, ManagedPlacementStatus.NoAim, "idle again means NoAim");
        }
    }
}
