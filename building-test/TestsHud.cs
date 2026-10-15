// Checks for the part of the HUD that reads build mode: the state flag, the feed that copies it, and the
// bottom group that hides while the tray is open. Verification harness only.
using System;
using System.Linq;
using MidManStudio.Gtg.Managed.Hud;
using MidManStudio.Gtg.Managed.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private static ManagedHud NewHud(World w, out ManagedPlayerItemBag bag)
        {
            CallStatic(typeof(ManagedHud), "ResetStatics");
            ManagedItemCatalog catalog = new ManagedItemCatalog();
            catalog.Add(new ManagedItemInfo(7, "potion", 5L, 10));

            // The bag is the player's own component, so the feed finds it and creates none.
            bag = w.PlayerGo.AddComponent<ManagedPlayerItemBag>();
            bag.UseCatalog(catalog);
            bag.Bag.TryAdd(7, 3);
            bag.QuickSlots.Assign(0, 7);

            var go = new GameObject("hud");
            ManagedHud hud = go.AddComponent<ManagedHud>();
            Call(hud, "OnEnable");
            Call(hud, "Start");
            return hud;
        }

        private static void HudFrame(ManagedHud hud, float time)
        {
            Time.unscaledTime = time;
            Call(hud, "Update");
            Call(hud, "LateUpdate");
        }

        private static GameObject Named(string name)
        {
            return Object.Registry.FirstOrDefault(g => g.name == name);
        }

        private static void HudTests()
        {
            ManagedItemDrag.Reset();
            Section("hud state");
            var state = new ManagedHudState();
            Check(!state.BottomHidden, "the bottom group is shown by default");
            state.SetBottomHidden(true);
            Check(state.BottomHidden, "SetBottomHidden hides it");
            state.SetBottomHidden(false);
            Check(!state.BottomHidden, "and shows it again");

            Section("hud feed reads build mode");
            World w = NewWorld(true);
            var feed = new ManagedHudFeed(false);
            state = new ManagedHudState();
            feed.Refresh(state, 10f);
            Check(!state.BottomHidden, "a closed build mode leaves the bottom group shown");
            w.Mode.SetBuildMode(true);
            feed.Refresh(state, 10.1f);
            Check(state.BottomHidden, "an open build mode hides it");
            w.Mode.SetBuildMode(false);
            feed.Refresh(state, 10.2f);
            Check(!state.BottomHidden, "closing it shows the group again");

            // A destroyed build mode no longer hides anything, even one that was open.
            w.Mode.SetBuildMode(true);
            feed.Refresh(state, 10.3f);
            Check(state.BottomHidden, "open again");
            Object.Destroy(w.Host);
            feed.Refresh(state, 10.4f);
            Check(!state.BottomHidden, "a destroyed build mode does not keep the group hidden");

            // No build mode in the scene: nothing hides, and the search is paced.
            w = NewWorld(true);
            Object.Destroy(w.Host);
            feed = new ManagedHudFeed(false);
            state = new ManagedHudState();
            feed.Refresh(state, 20f);
            Check(!state.BottomHidden, "no build mode in the scene shows the group");

            GameObject lateHost = new GameObject("late");
            ManagedBuildMode late = lateHost.AddComponent<ManagedBuildMode>();
            Call(late, "Reset");
            Call(late, "Start");
            late.SetBuildMode(true);
            feed.Refresh(state, 20f + ManagedHudFeed.BuildSearchSeconds - 0.1f);
            Check(!state.BottomHidden, "a build mode that appears is not searched for before the pause is over");
            feed.Refresh(state, 20f + ManagedHudFeed.BuildSearchSeconds + 0.1f);
            Check(state.BottomHidden, "and is found once it is");

            Section("hud bottom group");
            w = NewWorld(true);
            ManagedPlayerItemBag bag;
            ManagedHud hud = NewHud(w, out bag);
            int used = 0, usedSlot = -1;
            hud.QuickSlotUsed += (slot, item) => { used++; usedSlot = slot; };

            GameObject group = Named("Bottom Group");
            Check(group != null, "the HUD builds a bottom group");
            Check(group.transform.parent == Named("GTG HUD Canvas").transform, "directly under the canvas, so its anchors read as the canvas's");
            string[] expectedChildren = { "Hp Bar", "Sp Bar", "Quick Slot 0", "Quick Slot 1", "Quick Slot 2", "Quick Slot 3", "Quick Slot 4" };
            foreach (string childName in expectedChildren)
            {
                GameObject child = Named(childName);
                Check(child != null && child.transform.parent == group.transform, childName + " sits in the bottom group");
            }

            foreach (string outsideName in new[] { "Missions", "Compass", "Status", "Wheel Button" })
            {
                GameObject outside = Named(outsideName);
                Check(outside != null && outside.transform.parent != group.transform, outsideName + " stays outside the bottom group");
            }

            HudFrame(hud, 1f);
            Check(group.activeSelf && !hud.State.BottomHidden, "shown while build mode is closed");

            // A quick key works while shown.
            w.Kb.digit4Key.wasPressedThisFrame = true;
            HudFrame(hud, 1.1f);
            w.Kb.digit4Key.wasPressedThisFrame = false;
            Eq(used, 1, "key 4 uses the first quick slot while the group is shown"); Eq(usedSlot, 0, "slot 0");

            // Build mode opens: the group hides and the keys stop.
            w.Key_B();
            HudFrame(hud, 1.2f);
            Check(hud.State.BottomHidden, "the state hides the group while the tray is open");
            Check(!group.activeSelf, "the bottom group is switched off");
            Check(!Named("Quick Slot 0").activeInHierarchy && !Named("Hp Bar").activeInHierarchy && !Named("Sp Bar").activeInHierarchy,
                "so the slots and the bars are inactive");
            Check(Named("Wheel Button").activeInHierarchy && Named("Missions").activeInHierarchy, "while the rest of the HUD stays on");

            w.Kb.digit4Key.wasPressedThisFrame = true;
            HudFrame(hud, 1.3f);
            w.Kb.digit4Key.wasPressedThisFrame = false;
            Eq(used, 1, "a quick key does nothing while the group is hidden");
            Check(hud.UseQuickSlot(0), "the UseQuickSlot call from code still works, because only the keys are gated");
            Eq(used, 2, "and raises the event");

            // Closing shows it again.
            w.Key_B();
            HudFrame(hud, 1.4f);
            Check(group.activeSelf && !hud.State.BottomHidden, "closing the tray shows the group again");
            w.Kb.digit4Key.wasPressedThisFrame = true;
            HudFrame(hud, 1.5f);
            w.Kb.digit4Key.wasPressedThisFrame = false;
            Eq(used, 3, "and the quick keys work again");

            // A drag from a slot cannot finish once the slot is inactive, so opening the tray drops it.
            ManagedItemDrag.Begin(7, 1, ManagedItemDragOrigin.QuickSlot, 0);
            hud.ShowGhost(7, new Vector2(100f, 100f));
            w.Key_B();
            HudFrame(hud, 1.6f);
            Check(!ManagedItemDrag.Active, "a drag that started on a quick slot is dropped when the group hides");
            Check(!Named("Drag Ghost").activeSelf, "and the ghost is hidden");
            w.Key_B();
            HudFrame(hud, 1.7f);

            // A drag from somewhere else, such as a bag window, is left alone.
            ManagedItemDrag.Begin(7, 1, ManagedItemDragOrigin.Bag, 2);
            w.Key_B();
            HudFrame(hud, 1.8f);
            Check(ManagedItemDrag.Active, "a drag that did not start on a quick slot survives the hide");
            ManagedItemDrag.Reset();
            w.Key_B();
            HudFrame(hud, 1.9f);
        }
    }
}
