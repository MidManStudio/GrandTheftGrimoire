using System;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private sealed class PanelRig
        {
            public VisualElement Parent = new VisualElement { name = "docroot" };
            public ManagedBuildInventory Stock = new ManagedBuildInventory();
            public ManagedBlueprintBook Book = new ManagedBlueprintBook();
            public ManagedPlacedStore Store = NewStore();
            public FakeProbe Probe = new FakeProbe();
            public ManagedPlacementController Ctl;
            public ManagedBuildPanel Panel;
            public List<string> Presses = new List<string>();
            public List<string> Releases = new List<string>();
            public int TrayPresses;
            public bool PressResult = true;

            public PanelRig()
            {
                Parent.PanelRef = new StubPanel();
                var rules = new ManagedBuildRules { MaxReach = 0f };
                Ctl = new ManagedPlacementController(rules, Store, Probe, Stock, Book);
                Panel = new ManagedBuildPanel(
                    Parent, Stock, Book, Ctl,
                    (s, id) => { Presses.Add(s + ":" + id); return PressResult; },
                    (over, dragged) => Releases.Add(over + "/" + dragged),
                    () => TrayPresses++,
                    "B");
                Tray.worldBound = new Rect(700, 980, 520, 100);
            }

            public VisualElement Root { get { return Parent.Children.First(); } }
            public VisualElement Tray { get { return Root.Children.First(); } }
            public VisualElement Header { get { return Tray.Children.ElementAt(0); } }
            public VisualElement SlotRow { get { return Tray.Children.ElementAt(1); } }
            public Label Status { get { return (Label)Tray.Children.ElementAt(2); } }
            public Label Hint { get { return (Label)Tray.Children.ElementAt(3); } }
            public List<VisualElement> Slots { get { return SlotRow.Children.ToList(); } }
            public void Flush() { Parent.Scheduler.RunAll(); }
        }

        private static PointerDownEvent Down(float x, float y, int button = 0, int pointer = 0)
        {
            return new PointerDownEvent { pointerId = pointer, button = button, position = new Vector3(x, y, 0) };
        }

        private static PointerUpEvent Up(float x, float y, int pointer = 0)
        {
            return new PointerUpEvent { pointerId = pointer, button = 0, position = new Vector3(x, y, 0) };
        }

        private static void PanelTests()
        {
            Section("panel");
            Reset();
            PanelRig p = new PanelRig();
            Check(!p.Panel.Visible, "starts hidden");
            Eq(p.Root.StubStyleInstance.display.Value, DisplayStyle.None, "hidden through display none");
            Eq(p.Slots.Count, 1, "an empty inventory shows one hint label");
            Check(p.Slots[0] is Label, "the empty label");

            p.Stock.Give("crafting_table", 2); p.Stock.Give("wood", 3);
            Eq(p.Parent.Scheduler.Queue.Count, 1, "a stock change queues one rebuild");
            p.Stock.Give("crafting_table", 1);
            Eq(p.Parent.Scheduler.Queue.Count, 1, "several changes queue one rebuild");
            p.Flush();
            Eq(p.Slots.Count, 1, "only buildable stock gets a slot, materials do not");
            Eq(((Label)p.Slots[0].Children.ElementAt(2)).text, "x3", "slot shows the count");
            Eq(((Label)p.Slots[0].Children.ElementAt(1)).text, "Crafting Table", "slot shows the name");

            p.Panel.SetVisible(true);
            Check(p.Panel.Visible, "visible"); Eq(p.Root.StubStyleInstance.display.Value, DisplayStyle.Flex, "shown through display flex");
            VisualElement slot = p.Slots[0];

            // Press, hold, release on the tray.
            Stub.Send(slot, Down(800, 1020));
            Eq(p.Presses.Count, 1, "press reaches the callback"); Eq(p.Presses[0], "Stock:crafting_table", "with source and id");
            Eq(p.TrayPresses, 0, "the slot press does not reach the tray handler");
            Check(slot.HasPointerCapture(0), "the slot holds the pointer");
            Stub.Send(slot, Down(800, 1020));
            Eq(p.Presses.Count, 1, "a second press during a drag is ignored");
            Stub.Send(slot, Up(800, 1020));
            Eq(p.Releases.Count, 1, "release reaches the callback"); Eq(p.Releases[0], "True/False", "over the tray and not dragged");
            Check(!slot.HasPointerCapture(0), "capture is released");
            Stub.Send(slot, Up(800, 1020));
            Eq(p.Releases.Count, 1, "a stray release after the drag is ignored");

            // Drag out of the tray.
            Stub.Send(slot, Down(800, 1020));
            Stub.Send(slot, Up(1000, 300));
            Eq(p.Releases[1], "False/True", "released over the world after a long move");
            Stub.Send(slot, Down(800, 1020));
            Stub.Send(slot, Up(803, 1022));
            Eq(p.Releases[2], "True/False", "a move under the threshold is a click");
            Stub.Send(slot, Down(800, 1020));
            Stub.Send(slot, Up(780, 1020));
            Eq(p.Releases[3], "True/True", "a long move that ends inside the tray is a drag over the tray");

            // Other pointer and other button.
            Stub.Send(slot, Down(800, 1020, 1));
            Eq(p.Presses.Count, 4, "right button press is ignored");
            Stub.Send(slot, Down(800, 1020, 0, 3));
            Eq(p.Presses.Count, 5, "another pointer id may start its own press");
            Stub.Send(slot, Up(800, 1020, 0));
            Eq(p.Releases.Count, 4, "release of an unrelated pointer is ignored");
            Stub.Send(slot, Up(900, 1000, 3));
            Eq(p.Releases.Count, 5, "release of the captured pointer ends it");
            Stub.Reset();

            // Interrupted capture.
            Stub.Send(slot, Down(800, 1020));
            slot.ReleasePointer(0);
            Eq(p.Releases.Last(), "True/True", "a lost capture is reported as a cancelled drag");
            Eq(p.Releases.Count, 6, "exactly once");

            // Hiding the tray releases a held pointer without a callback.
            Stub.Send(slot, Down(800, 1020));
            p.Panel.SetVisible(false);
            Check(!slot.HasPointerCapture(0), "hiding releases the capture"); Eq(p.Releases.Count, 6, "and raises no callback");
            p.Panel.SetVisible(true);
            slot = p.Slots[0];
            int presses = p.Presses.Count;
            Stub.Send(slot, Down(800, 1020));
            Eq(p.Presses.Count, presses + 1, "the slot accepts a press again afterwards");
            Stub.Send(slot, Up(800, 1020));

            // A refused press takes nothing.
            p.PressResult = false;
            Stub.Send(slot, Down(800, 1020));
            Check(!slot.HasPointerCapture(0), "a refused press does not capture"); p.PressResult = true;

            // The pressed slot survives a stock change during the press.
            Stub.Send(slot, Down(800, 1020));
            p.Stock.TryTake("crafting_table", 1);
            Check(p.Slots.Contains(slot), "the pressed slot is still there until the rebuild runs");
            p.Flush();
            Eq(((Label)p.Slots[0].Children.ElementAt(2)).text, "x2", "the rebuild shows the new count");
            Stub.Send(slot, Up(800, 1020));

            // Tray background.
            Stub.Send(p.Tray, Down(710, 990));
            Eq(p.TrayPresses, 1, "tray press"); Stub.Send(p.Tray, Down(710, 990, 1)); Eq(p.TrayPresses, 1, "right button on the tray is ignored");

            // Tabs.
            p.Book.Unlock("crafting_table");
            p.Flush();
            Stub.Send(p.Header.Children.ElementAt(1), Down(0, 0));
            Eq(p.Slots.Count, 1, "blueprints tab lists the unlocked blueprint");
            Eq(((Label)p.Slots[0].Children.ElementAt(2)).text, "Free", "a blueprint without cost reads Free");
            Stub.Send(p.Slots[0], Down(800, 1020));
            Eq(p.Presses.Last(), "Blueprint:crafting_table", "blueprint source");
            Stub.Send(p.Slots[0], Up(800, 1020));
            Stub.Send(p.Header.Children.ElementAt(0), Down(0, 0));
            Eq(((Label)p.Slots[0].Children.ElementAt(2)).text, "x2", "back on the inventory tab");

            var def = ManagedBuildableCatalog.Get(Craft);
            var saved = def.Cost;
            def.Cost = new[] { new ManagedBuildCostLine("wood", 2), new ManagedBuildCostLine("nail", 1) };
            Stub.Send(p.Header.Children.ElementAt(1), Down(0, 0));
            Eq(((Label)p.Slots[0].Children.ElementAt(2)).text, "2 x wood, 1 x nail", "cost text");
            def.Cost = saved;
            Stub.Send(p.Header.Children.ElementAt(0), Down(0, 0));

            // Notice and status text.
            p.Panel.ShowNotice("Missing materials");
            Eq(p.Status.text, "Missing materials", "notice text"); Check(p.Status.StubStyleInstance.color.Value.r > 0.9f, "notice is red");
            p.Stock.Give("butchery_station", 1); p.Flush();
            p.Ctl.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out _);
            Check(p.Status.text != "Missing materials", "the next state change replaces the notice");
            p.Ctl.SetAim(true, new Vector3(2, 0, 2), Vector3.zero);
            Eq(p.Status.text, "Ready to place", "valid spot text"); Check(p.Status.StubStyleInstance.color.Value.b > 0.9f, "valid text is blue");
            p.Probe.Blocked = (a, b) => true;
            p.Ctl.SetAim(true, new Vector3(2.6f, 0, 2), Vector3.zero);
            Eq(p.Status.text, "Something is in the way", "invalid spot text"); Check(p.Status.StubStyleInstance.color.Value.r > 0.9f, "invalid text is red");
            Check(p.Hint.text.Contains("Click: place"), "placing hint");
            p.Ctl.Cancel();
            Check(p.Hint.text.Contains("Drag a station"), "idle hint"); Check(p.Hint.text.Contains("B: close"), "hint names the close key");
            Eq(p.Status.text, "", "status clears when idle");

            // Selection border.
            p.Flush();
            p.Ctl.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out _);
            Check(p.Slots[0].StubStyleInstance.borderTopColor.Value.b > 0.9f && p.Slots[0].StubStyleInstance.borderTopColor.Value.a > 0.9f, "the selected slot gets the accent border");
            p.Ctl.Cancel();
            Check(p.Slots[0].StubStyleInstance.borderTopColor.Value.a < 0.5f, "and loses it again");

            // IsOverTray.
            Check(p.Panel.IsOverTray(new Vector2(960, 50)), "bottom center is over the tray, with the Y axis flipped");
            Check(!p.Panel.IsOverTray(new Vector2(960, 500)), "screen center is not");
            Check(!p.Panel.IsOverTray(new Vector2(100, 50)), "bottom left is outside the tray width");
            ((StubPanel)p.Parent.PanelRef).PixelsPerScreenPixel = 0.5f;
            Check(p.Panel.IsOverTray(new Vector2(1960, 100)) == false, "scaling is applied");
            ((StubPanel)p.Parent.PanelRef).PixelsPerScreenPixel = 1f;
            p.Panel.SetVisible(false);
            Check(!p.Panel.IsOverTray(new Vector2(960, 50)), "a hidden tray is never hit");
            p.Panel.SetVisible(true);
            p.Parent.PanelRef = null;
            Check(!p.Panel.IsOverTray(new Vector2(960, 50)), "no panel means no hit");

            // Dispose.
            p.Parent.PanelRef = new StubPanel();
            p.Flush();
            p.Panel.Dispose();
            Eq(p.Parent.childCount, 0, "dispose removes the panel from the document");
            p.Stock.Give("crafting_table", 1);
            Eq(p.Parent.Scheduler.Queue.Count, 0, "a disposed panel no longer reacts to the stock");
            p.Ctl.BeginPlacing(ManagedPlacementSource.Stock, "crafting_table", false, out _);
        }

        private static void UiHostTests()
        {
            Section("uihost");
            Reset();
            var go = new GameObject("ui");
            PanelSettings created;
            UIDocument doc = ManagedBuildUiHost.Create(go, null, 50f, out created);
            Check(created != null, "a runtime PanelSettings is created when none is given");
            Check(ReferenceEquals(doc.panelSettings, created), "and assigned");
            Eq(created.scaleMode, PanelScaleMode.ScaleWithScreenSize, "scale with screen size");
            Eq(created.referenceResolution.x * 10000 + created.referenceResolution.y, 1920 * 10000 + 1080, "reference resolution");
            Near(created.match, 0.5f, "match");
            Near(doc.sortingOrder, 50f, "sorting order");
            Check(doc.rootVisualElement.StubStyleInstance.unityFontDefinition.Font != null, "the built-in font is set on the root");
            Check(doc.rootVisualElement.panel != null, "the root is attached to a panel");

            Reset();
            go = new GameObject("ui");
            var own = new PanelSettings();
            doc = ManagedBuildUiHost.Create(go, own, 7f, out created);
            Check(created == null, "no copy is made when settings are supplied"); Check(ReferenceEquals(doc.panelSettings, own), "the supplied settings are used");

            Reset();
            Resources.HasLegacyRuntime = false;
            go = new GameObject("ui");
            bool threw = false;
            try { doc = ManagedBuildUiHost.Create(go, null, 7f, out created); } catch (Exception) { threw = true; }
            Check(!threw, "a missing built-in font does not throw"); Check(doc.rootVisualElement.StubStyleInstance.unityFontDefinition.Font == null, "and no font is set");

            Reset();
            ManagedBuildUiHost.EnsureEventSystem();
            Eq(Object.Registry.Count(g => g.GetComponent<EventSystem>() != null), 1, "an EventSystem is created");
            Check(Object.Registry.Any(g => g.GetComponent<InputSystemUIInputModule>() != null), "with the Input System module");
            ManagedBuildUiHost.EnsureEventSystem();
            Eq(Object.Registry.Count(g => g.GetComponent<EventSystem>() != null), 1, "a second call adds nothing");
            Eq(Debug.Warnings.Count, 0, "no warning for a good setup");

            Reset();
            var es = new GameObject("es"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            ManagedBuildUiHost.EnsureEventSystem();
            Eq(Debug.Warnings.Count, 1, "the old input module is reported");
            Eq(Object.Registry.Count(g => g.GetComponent<EventSystem>() != null), 1, "and no second EventSystem is made");
        }
    }
}
