using System;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using System.Linq;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        private sealed class World
        {
            public GameObject Host, PlayerGo;
            public ManagedBuildMode Mode;
            public ManagedCharacter Player;
            public ManagedCameraRig Rig;
            public Keyboard Kb;
            public Mouse Ms;
            public UnityEngine.Camera Cam;
            public int ModeChanges;

            public ManagedBuildPanel Panel { get { return (ManagedBuildPanel)GetField(Mode, "_panel"); } }
            public VisualElement DocRoot { get { return ((GameObject)GetField(Mode, "_uiRoot")).GetComponent<UIDocument>().rootVisualElement; } }
            public VisualElement Tray { get { return DocRoot.Children.First().Children.First(); } }
            public List<VisualElement> Slots { get { DocRoot.Scheduler.RunAll(); return Tray.Children.ElementAt(1).Children.ToList(); } }
            public ManagedPlacementController Ctl { get { return Mode.Placement; } }

            // One frame: the frame's input is already set on the devices. Press flags clear afterwards.
            public void Frame()
            {
                Time.frameCount++;
                DocRoot.Scheduler.RunAll();
                Call(Mode, "LateUpdate");
                Kb[Key.B].wasPressedThisFrame = false; Kb.rKey.wasPressedThisFrame = false; Kb.escapeKey.wasPressedThisFrame = false;
                Ms.leftButton.wasPressedThisFrame = false; Ms.rightButton.wasPressedThisFrame = false; Ms.scroll.Value = Vector2.zero;
            }

            public void Point(float worldX, float worldZ) { Ms.position.Value = new Vector2(960f + worldX * 100f, 540f + worldZ * 100f); }
            public void PointTray() { Ms.position.Value = new Vector2(960f, 50f); }
            public void Click() { Ms.leftButton.wasPressedThisFrame = true; Frame(); }
            public void Key_B() { Kb[Key.B].wasPressedThisFrame = true; Frame(); }
            public void Hover(float x, float z) { Point(x, z); Frame(); }

            // Panel coordinates of the pointer, for UI events. The panel counts from the top.
            public Vector3 PanelPos() { return new Vector3(Ms.position.Value.x, 1080f - Ms.position.Value.y, 0f); }

            public bool Drew(Func<DrawCall, bool> pred) { return Graphics.Calls.Any(pred); }
        }

        private static World NewWorld(bool withRig, Action<ManagedBuildMode> beforeStart = null, bool lockCursor = true, bool castOn = true)
        {
            Reset();
            var w = new World();
            Ground();
            w.Kb = new Keyboard(); w.Ms = new Mouse();
            Keyboard.current = w.Kb; Mouse.current = w.Ms;
            var camGo = new GameObject("cam");
            w.Cam = camGo.AddComponent<UnityEngine.Camera>();
            w.Cam.RayFactory = p => new Ray(new Vector3((p.x - 960f) / 100f, 10f, (p.y - 540f) / 100f), Vector3.down);
            UnityEngine.Camera.main = w.Cam;

            w.PlayerGo = new GameObject("player");
            w.PlayerGo.layer = 8;
            w.Player = w.PlayerGo.AddComponent<ManagedCharacter>();
            if (!castOn) w.Player.Features &= ~ManagedCharacterFeature.Cast;
            Box("body", new Vector3(0, 1, 0), new Vector3(1, 2, 1), 8, w.PlayerGo.transform);

            if (withRig) { w.Rig = new GameObject("rig").AddComponent<ManagedCameraRig>(); }

            w.Host = new GameObject("host");
            w.Mode = w.Host.AddComponent<ManagedBuildMode>();
            Call(w.Mode, "Reset");
            SetField(w.Mode, "_groundMask", new LayerMask { value = ~(1 << 8) });
            if (beforeStart != null) beforeStart(w.Mode);

            if (lockCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            Call(w.Mode, "Start");
            w.Mode.BuildModeChanged += on => w.ModeChanges++;
            w.Tray.worldBound = new Rect(700, 980, 520, 100);
            return w;
        }

        private static void ModeTests()
        {
            Section("mode start");
            World w = NewWorld(true);
            Check(!w.Mode.BuildModeActive, "build mode starts closed");
            Eq(w.Mode.Stock.GetCount("crafting_table"), 1, "Reset seeds a crafting table"); Eq(w.Mode.Stock.GetCount("butchery_station"), 1, "and a butchery station");
            Check(w.Mode.Blueprints.IsUnlocked("crafting_table") && w.Mode.Blueprints.IsUnlocked("butchery_station"), "and unlocks both blueprints");
            Check(w.Mode.Store != null && w.Mode.Placement != null, "parts exist after Start");
            Check(!w.Panel.Visible, "the tray is hidden");
            Eq(Cursor.lockState, CursorLockMode.Locked, "the cursor is untouched while closed");
            w.Frame(); w.Frame();
            Eq(Graphics.Calls.Count, 0, "nothing is drawn with no pieces and no build mode");
            Check(Object.Registry.Any(g => g.name == "GTG Placed Pieces"), "the pieces root exists at the scene root");
            Check(Object.Registry.First(g => g.name == "GTG Placed Pieces").transform.parent == null, "and has no parent");
            Check(Object.Registry.Any(g => g.GetComponent<UnityEngine.EventSystems.EventSystem>() != null), "an EventSystem was ensured");

            Section("mode toggle");
            w.Player.Features = ManagedCharacterFeature.All;
            w.Key_B();
            Check(w.Mode.BuildModeActive, "B opens build mode"); Eq(w.ModeChanges, 1, "event raised");
            Eq(Cursor.lockState, CursorLockMode.None, "the cursor is freed"); Check(Cursor.visible, "and visible");
            Check(w.Rig.CursorHeldByUi, "the rig is told the UI holds the cursor");
            Check(!w.Player.Has(ManagedCharacterFeature.Cast), "casting is off while the tray is open");
            Check(w.Player.Has(ManagedCharacterFeature.Move) && w.Player.Has(ManagedCharacterFeature.Look), "movement and look stay on");
            Check(w.Panel.Visible, "the tray is shown");
            w.Key_B();
            Check(!w.Mode.BuildModeActive, "B closes it"); Eq(w.ModeChanges, 2, "second event");
            Eq(Cursor.lockState, CursorLockMode.Locked, "the lock is restored"); Check(!Cursor.visible, "and the cursor hidden again");
            Check(!w.Rig.CursorHeldByUi, "the rig is released"); Check(w.Player.Has(ManagedCharacterFeature.Cast), "casting is back");
            Check(!w.Panel.Visible, "the tray is hidden again");
            w.Mode.SetBuildMode(true); w.Mode.SetBuildMode(true);
            Eq(w.ModeChanges, 3, "setting the same state twice raises once");
            w.Mode.SetBuildMode(false);

            // The cursor was unlocked before: it stays unlocked afterwards.
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            w.Key_B(); w.Key_B();
            Eq(Cursor.lockState, CursorLockMode.None, "an unlocked cursor is not captured on exit");
            Check(!w.Rig.CursorHeldByUi, "hold is cleared");

            // Cast was off before: it stays off.
            w = NewWorld(true, null, true, false);
            w.Key_B(); w.Key_B();
            Check(!w.Player.Has(ManagedCharacterFeature.Cast), "a Cast flag that was off is not switched on by closing the tray");

            // Without a rig the cursor is handled directly.
            w = NewWorld(false);
            w.Key_B();
            Eq(Cursor.lockState, CursorLockMode.None, "no rig: cursor freed"); w.Key_B();
            Eq(Cursor.lockState, CursorLockMode.Locked, "no rig: cursor locked again"); Check(!Cursor.visible, "no rig: hidden");

            // Disabling while open restores everything.
            w = NewWorld(true);
            w.Key_B();
            Call(w.Mode, "OnDisable");
            Check(!w.Mode.BuildModeActive && !w.Rig.CursorHeldByUi && w.Player.Has(ManagedCharacterFeature.Cast) && Cursor.lockState == CursorLockMode.Locked, "OnDisable leaves build mode cleanly");

            // Before Start.
            w = NewWorld(true, m => m.SetBuildMode(true));
            Check(w.Mode.BuildModeActive, "SetBuildMode before Start opens it on Start"); Eq(Cursor.lockState, CursorLockMode.None, "and frees the cursor");
            Check(w.Panel.Visible, "and shows the tray");

            Section("mode stock");
            var custom = new ManagedBuildInventory(); custom.Give("crafting_table", 5);
            w = NewWorld(true, m => m.SetStock(custom));
            Eq(w.Mode.Stock.GetCount("crafting_table"), 5, "a custom stock replaces the default");
            Eq(w.Mode.Stock.GetCount("butchery_station"), 0, "and the seeds are not added to it");
            bool threw = false;
            try { w.Mode.SetStock(custom); } catch (InvalidOperationException) { threw = true; }
            Check(threw, "SetStock after Start is refused loudly");

            w = NewWorld(true, m => SetField(m, "_startingStock", new[] { new ManagedBuildMode.StartingStation { BuildableId = "ghost_item", Count = 1 }, null, new ManagedBuildMode.StartingStation { BuildableId = "crafting_table", Count = 0 } }));
            Eq(Debug.Warnings.Count(x => x.Contains("ghost_item")), 1, "an unknown starting id warns once");
            Eq(w.Mode.Stock.GetCount("crafting_table"), 0, "a zero count seeds nothing, a null entry is skipped");
            w = NewWorld(true, m => SetField(m, "_startingBlueprints", new[] { "nope" }));
            Check(Debug.Warnings.Any(x => x.Contains("nope")), "an unknown starting blueprint warns");

            Section("mode click place");
            w = NewWorld(true);
            w.Key_B();
            w.PointTray();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            Eq(w.Ctl.Mode, ManagedPlacementMode.Placing, "clicking a slot selects it");
            Check(!w.Ctl.CommitOnRelease, "and arms it for a click");
            Eq(w.Ctl.Source, ManagedPlacementSource.Stock, "from the stock");
            Graphics.Calls.Clear();
            w.Hover(3, 3);
            Check(w.Ctl.Result.IsValid, "a free spot is valid");
            Check(w.Drew(c => c.Kind == "mesh" && c.Material.Colors["_Color"].b > 0.9f), "the ghost is blue");
            Graphics.Calls.Clear();
            w.Hover(9, 0);
            Eq(w.Ctl.Result.Status, ManagedPlacementStatus.OutOfReach, "far away is out of reach");
            Check(w.Drew(c => c.Kind == "mesh" && c.Material.Colors["_Color"].r > 0.9f), "and the ghost is red");
            w.Click();
            Eq(w.Mode.Store.Count, 0, "clicking an out of reach spot places nothing");
            w.Hover(3, 3);
            w.Click();
            Eq(w.Mode.Store.Count, 1, "clicking a valid spot places the station"); Eq(w.Mode.Stock.GetCount("crafting_table"), 0, "and spends it");
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "the last station ends placing");
            Check(CountProxies() == 1, "a collider proxy was created");
            Graphics.Calls.Clear();
            w.Hover(-6, -3);
            Check(w.Drew(c => c.Kind == "instanced" && c.Count == 1), "the placed piece is drawn instanced");
            Check(!w.Drew(c => c.Kind == "mesh"), "no ghost while idle and not over a piece");

            Section("mode drag place");
            w = NewWorld(true);
            w.Key_B();
            w.PointTray();
            VisualElement slot = w.Slots[0];
            Stub.Send(slot, Down(960, 1030));
            Eq(w.Ctl.Mode, ManagedPlacementMode.Placing, "pressing a slot starts placing"); Check(w.Ctl.CommitOnRelease, "as a drag");
            Graphics.Calls.Clear();
            w.Frame();
            Check(!w.Drew(c => c.Kind == "mesh"), "no ghost while the pointer is still over the tray");
            w.Point(3, 3); w.Frame();
            Check(w.Drew(c => c.Kind == "mesh" && c.Material.Colors["_Color"].b > 0.9f), "the ghost follows the pointer over the world");
            Stub.Send(slot, Up(w.PanelPos().x, w.PanelPos().y));
            Eq(w.Mode.Store.Count, 1, "releasing over a valid spot places the piece"); Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "and ends the drag");
            Eq(w.Mode.Stock.GetCount("crafting_table"), 0, "one station spent");

            // Dropping on a taken spot keeps the piece in hand, then a click elsewhere places it.
            w.Slots[0].Children.Count();
            Stub.Send(w.Slots[0], Down(960, 1030));
            Eq(w.Ctl.Definition.Id, "butchery_station", "the remaining station is the one in the tray");
            w.Point(3, 3); w.Frame();
            Stub.Send(w.Slots[0], Up(w.PanelPos().x, w.PanelPos().y));
            Eq(w.Mode.Store.Count, 1, "an occupied drop places nothing"); Eq(w.Ctl.Mode, ManagedPlacementMode.Placing, "the piece stays in hand");
            Check(!w.Ctl.CommitOnRelease, "armed for a click");
            w.Point(-4, -4); w.Frame();
            Check(w.Ctl.Result.IsValid, "a free spot is valid again");
            w.Click();
            Eq(w.Mode.Store.Count, 2, "a click places it"); Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "done");

            // Dragging back onto the tray cancels.
            w.Mode.Stock.Give("crafting_table", 1);
            w.Tray.worldBound = new Rect(700, 980, 520, 100);
            Stub.Send(w.Slots[0], Down(960, 1030));
            w.Point(5, 0); w.Frame();
            w.PointTray(); w.Frame();
            Check(w.Ctl.Result.Status == ManagedPlacementStatus.NoAim, "the ghost disappears over the tray");
            Stub.Send(w.Slots[0], Up(900, 1020));
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "a drag released over the tray cancels"); Eq(w.Mode.Stock.GetCount("crafting_table"), 1, "nothing spent");

            // Tray press with the pointer over it never commits a click placement.
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            Check(w.Ctl.Mode == ManagedPlacementMode.Placing && !w.Ctl.CommitOnRelease, "armed again");
            w.PointTray(); w.Click();
            Eq(w.Mode.Store.Count, 2, "a click on the tray does not place"); Eq(w.Ctl.Mode, ManagedPlacementMode.Placing, "still placing");
            // A press on the tray background cancels.
            Stub.Send(w.Tray, Down(710, 990));
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "pressing the tray background cancels");

            Section("mode cancel and rotate");
            w = NewWorld(true);
            w.Key_B();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            w.Hover(3, 3);
            Eq(w.Ctl.Quarter, 0, "no rotation yet");
            w.Kb.rKey.wasPressedThisFrame = true; w.Frame();
            Eq(w.Ctl.Quarter, 1, "R rotates");
            w.Kb.rKey.wasPressedThisFrame = true; w.Kb.leftShiftKey.isPressed = true; w.Frame(); w.Kb.leftShiftKey.isPressed = false;
            Eq(w.Ctl.Quarter, 0, "shift R turns back");
            w.Ms.scroll.Value = new Vector2(0, 120); w.Frame();
            Eq(w.Ctl.Quarter, 1, "the wheel rotates while placing");
            w.Ms.rightButton.wasPressedThisFrame = true; w.Frame();
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "right click cancels");
            w.Ms.scroll.Value = new Vector2(0, 120); w.Frame();
            Eq(w.Ctl.Quarter, 1, "the wheel does nothing while idle");
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            Eq(w.Ctl.Quarter, 1, "the rotation is remembered");
            w.Kb.escapeKey.wasPressedThisFrame = true; w.Frame();
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "Esc cancels");

            Section("mode pick up");
            w = NewWorld(true);
            w.Key_B();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            w.Hover(3, 3); w.Click();
            Eq(w.Mode.Store.Count, 1, "placed");
            ManagedPlacedPiece placed; w.Mode.Store.TryGet(0, out placed);
            Graphics.Calls.Clear();
            w.Hover(3, 3);
            Check(w.Drew(c => c.Kind == "mesh" && c.Material.Colors["_Color"].g > 0.8f && c.Material.Colors["_Color"].b < 0.4f), "hovering a placed piece draws the yellow marker");
            Graphics.Calls.Clear();
            w.Hover(-6, 2);
            Check(!w.Drew(c => c.Kind == "mesh"), "no marker away from it");
            w.Hover(3, 3);
            w.Click();
            Eq(w.Ctl.Source, ManagedPlacementSource.Carried, "clicking it picks it up");
            Eq(w.Mode.Store.Count, 0, "and it left the world, not placed again by the same click");
            Check(CountProxiesActive() == 0, "its collider is gone");
            Check(w.Ctl.Result.IsValid, "its own spot is valid for it, and the ghost is already there on the pick up frame");
            w.Hover(-4, 3);
            Graphics.Calls.Clear();
            w.Hover(-4, 3);
            Check(w.Drew(c => c.Kind == "mesh" && c.Material.Colors["_Color"].b > 0.9f), "the carried piece shows a blue ghost");
            w.Click();
            Eq(w.Mode.Store.Count, 1, "clicking puts it down"); Eq(w.Mode.Stock.GetCount("crafting_table"), 0, "moving costs nothing"); Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "and the hand is empty");
            ManagedPlacedPiece moved; w.Mode.Store.TryGet(0, out moved);
            Check(moved.CellX < placed.CellX, "it moved to the left");

            // Esc puts it back where it was.
            w.Hover(-4, 3); w.Click();
            Eq(w.Ctl.Source, ManagedPlacementSource.Carried, "picked up again");
            w.Kb.escapeKey.wasPressedThisFrame = true; w.Frame();
            ManagedPlacedPiece again; w.Mode.Store.TryGet(0, out again);
            Check(w.Mode.Store.Count == 1 && again.CellX == moved.CellX && again.CellZ == moved.CellZ, "Esc restores the piece to where it was lifted");

            // Pressing a slot with a piece in hand stashes it.
            w.Hover(-4, 3); w.Click();
            Stub.Send(w.Slots[0], Down(960, 1030));
            Eq(w.Mode.Stock.GetCount("crafting_table"), 1, "pressing the tray with a piece in hand stores it"); Eq(w.Mode.Store.Count, 0, "and the world is empty");
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "idle");

            // Closing the tray with a piece in hand puts it back.
            w.Mode.Store.Add(Craft, 8, 8, 0, 0f);
            w.Hover(4.75f, 4.5f);
            Check(w.Mode.Store.Count == 1, "a piece added through the API appears");
            Graphics.Calls.Clear();
            w.Hover(4.75f, 4.5f);
            w.Click();
            Eq(w.Ctl.Source, ManagedPlacementSource.Carried, "picked up by pointer");
            w.Key_B();
            Eq(w.Mode.Store.Count, 1, "closing build mode puts the carried piece back");
            Eq(w.Ctl.Mode, ManagedPlacementMode.Idle, "idle");

            Section("mode blocked by player");
            w = NewWorld(true);
            w.Key_B();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            w.Hover(0, 0);
            Eq(w.Ctl.Result.Status, ManagedPlacementStatus.Blocked, "a station cannot be dropped onto the player");
            w.Click();
            Eq(w.Mode.Store.Count, 0, "and the click places nothing");

            Section("mode pointer limits");
            w = NewWorld(true);
            w.Key_B();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            w.Hover(3, 3);
            Check(w.Ctl.Result.IsValid, "valid first");
            w.Ms.position.Value = new Vector2(-50, 300); w.Frame();
            Eq(w.Ctl.Result.Status, ManagedPlacementStatus.NoAim, "a pointer outside the game view gives no aim");
            Mouse.current = null;
            w.Frame();
            Eq(w.Ctl.Result.Status, ManagedPlacementStatus.NoAim, "no mouse gives no aim");
            Mouse.current = w.Ms;
            UnityEngine.Camera.main = null;
            w.Hover(3, 3);
            Eq(w.Ctl.Result.Status, ManagedPlacementStatus.NoAim, "no camera gives no aim");

            Section("mode destroy");
            w = NewWorld(true);
            w.Key_B();
            Stub.Send(w.Slots[0], Down(960, 1030)); Stub.Send(w.Slots[0], Up(960, 1030));
            w.Hover(3, 3); w.Click();
            Eq(CountProxies(), 1, "a proxy exists");
            VisualElement docRoot = w.DocRoot;
            Call(w.Mode, "OnDisable");
            Call(w.Mode, "OnDestroy");
            Eq(CountProxies(), 0, "destroy removes the proxies");
            Check(!Object.Registry.Any(g => g.name == "GTG Placed Pieces"), "and the pieces root");
            Eq(docRoot.childCount, 0, "and the tray");
            Check(((PanelSettings)GetField(w.Mode, "_createdPanelSettings")) == null, "and the runtime PanelSettings");

            Section("camera rig hook");
            w = NewWorld(true);
            Call(w.Rig, "Awake");
            SetField(w.Rig, "_player", w.Player);
            Time.frameCount = 1;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            w.Ms.leftButton.wasPressedThisFrame = true;
            w.Rig.CursorHeldByUi = true;
            Call(w.Rig, "LateUpdate");
            Eq(Cursor.lockState, CursorLockMode.None, "a click does not recapture the cursor while the UI holds it");
            w.Rig.CursorHeldByUi = false;
            Call(w.Rig, "LateUpdate");
            Eq(Cursor.lockState, CursorLockMode.Locked, "without the hold a click recaptures it as before"); Check(!Cursor.visible, "and hides it");
            w.Rig.ReleaseCursor();
            Eq(Cursor.lockState, CursorLockMode.None, "ReleaseCursor frees"); Check(Cursor.visible, "and shows the cursor");
            w.Rig.CaptureCursor();
            Eq(Cursor.lockState, CursorLockMode.Locked, "CaptureCursor locks");
            GUI.TextureDraws = 0;
            w.Rig.CursorHeldByUi = false;
            Call(w.Rig, "OnGUI");
            Check(GUI.TextureDraws == 4, "the crosshair is drawn as four bars normally");
            GUI.TextureDraws = 0;
            w.Rig.CursorHeldByUi = true;
            Call(w.Rig, "OnGUI");
            Eq(GUI.TextureDraws, 0, "the crosshair is hidden while the UI holds the cursor");

            Section("mode validate");
            var host = new GameObject("v").AddComponent<ManagedBuildMode>();
            ((ManagedBuildRules)GetField(host, "_rules")).CellSize = 0f;
            SetField(host, "_aimRayLength", 0f);
            Call(host, "OnValidate");
            Check(((ManagedBuildRules)GetField(host, "_rules")).CellSize >= 0.05f, "OnValidate clamps the cell size");
            Check((float)GetField(host, "_aimRayLength") >= 1f, "and the ray length");
        }

        private static int CountProxiesActive()
        {
            return Object.Registry.Count(g => g.GetComponent<ManagedPlacedPieceProxy>() != null && g.activeInHierarchy);
        }
    }
}
