using System;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        public static GameObject Box(string name, Vector3 center, Vector3 size, int layer = 0, Transform parent = null, bool trigger = false)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.position = center;
            if (parent != null) go.transform.SetParent(parent, false);
            BoxCollider b = go.AddComponent<BoxCollider>();
            b.size = size;
            b.isTrigger = trigger;
            return go;
        }

        public static GameObject Proxy(int pieceId, Vector3 center, Vector3 size, int layer = 0)
        {
            GameObject go = Box("proxy" + pieceId, center, size, layer);
            go.AddComponent<ManagedPlacedPieceProxy>().PieceId = pieceId;
            return go;
        }

        public static GameObject Ground(float topY = 0f)
        {
            return Box("ground", new Vector3(0, topY - 0.5f, 0), new Vector3(400, 1, 400));
        }

        private static Ray RayDown(float x, float z, float y = 10f) { return new Ray(new Vector3(x, y, z), Vector3.down); }

        // ---- aim and probe ------------------------------------------------------------------------------------
        private static void AimAndProbeTests()
        {
            Section("aim");
            Reset();
            Ground();
            var aim = new ManagedBuildAim(~0, 100f);
            Vector3 p; int hover;

            Check(aim.Cast(RayDown(0, 0), out p, out hover), "ground hit");
            Near(p.y, 0f, "ground point y"); Eq(hover, -1, "no hover");
            Check(!aim.Cast(new Ray(new Vector3(0, 10, 0), Vector3.up), out p, out hover), "a ray into the sky misses");
            Check(!new ManagedBuildAim(~0, 5f).Cast(RayDown(0, 0), out p, out hover), "the ray length limits the cast");

            GameObject piece = Proxy(7, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 1f));
            Check(aim.Cast(RayDown(0, 0), out p, out hover), "ground behind a piece");
            Near(p.y, 0f, "the aim point is the floor, not the top of the piece"); Eq(hover, 7, "the piece is hovered");
            Check(aim.Cast(RayDown(5, 0), out p, out hover) && hover == -1, "away from the piece nothing is hovered");

            Proxy(8, new Vector3(5, -2f, 0), new Vector3(1, 1, 1));
            Check(aim.Cast(RayDown(5, 0), out p, out hover), "ground over a buried piece");
            Eq(hover, -1, "a piece behind the ground is not hovered");

            // A piece over open air still hovers and there is no ground.
            Reset();
            Proxy(3, new Vector3(0, 5, 0), new Vector3(1, 1, 1));
            Check(!aim.Cast(RayDown(0, 0), out p, out hover), "no ground");
            Eq(hover, 3, "the piece is still reported");

            // Two pieces, nearest wins, whatever order the physics returns.
            Reset(); Ground();
            Proxy(1, new Vector3(0, 2.5f, 0), new Vector3(1, 1, 1));
            Proxy(2, new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1));
            aim.Cast(RayDown(0, 0), out p, out hover);
            Eq(hover, 1, "the nearer of two stacked pieces wins");

            // Ignore root.
            Reset(); Ground();
            GameObject player = new GameObject("player");
            Box("body", new Vector3(0, 1, 0), new Vector3(1, 2, 1), 0, player.transform);
            aim.IgnoreRoot = null;
            aim.Cast(RayDown(0, 0), out p, out hover);
            Near(p.y, 2f, "without an ignore root the player's head counts as ground");
            aim.IgnoreRoot = player.transform;
            aim.Cast(RayDown(0, 0), out p, out hover);
            Near(p.y, 0f, "with it the player is skipped");

            // Mask and triggers.
            Reset(); Ground();
            Box("other layer", new Vector3(0, 1, 0), new Vector3(1, 1, 1), 5);
            Box("trigger", new Vector3(0, 3, 0), new Vector3(1, 1, 1), 0, null, true);
            aim = new ManagedBuildAim(1, 100f);
            aim.Cast(RayDown(0, 0), out p, out hover);
            Near(p.y, 0f, "a layer outside the mask and a trigger do not count");

            Section("probe");
            Reset();
            Ground();
            Box("step", new Vector3(5, 0.15f, 0), new Vector3(2, 0.3f, 4));
            Box("roof", new Vector3(0, 3.1f, 0), new Vector3(8, 0.2f, 8));
            Box("trigger", new Vector3(-5, 0.4f, 0), new Vector3(2, 0.8f, 2), 0, null, true);
            Box("wall layer", new Vector3(0, 0.9f, 7), new Vector3(2, 1.8f, 0.4f), 6);
            var probe = new ManagedUnityBuildProbe(1, ~0);
            float y;
            Check(probe.TryGetGroundHeight(0, 0, 0f, out y) && Math.Abs(y) < 1e-4f, "plain ground");
            Check(probe.TryGetGroundHeight(5, 0, 0f, out y) && Math.Abs(y - 0.3f) < 1e-4f, "the step top");
            Check(probe.TryGetGroundHeight(0, 0, 0f, out y) && Math.Abs(y) < 1e-4f, "the roof above the start height is not ground");
            Check(probe.TryGetGroundHeight(-5, 0, 0f, out y) && Math.Abs(y) < 1e-4f, "a trigger is not ground");
            Box("platform on another layer", new Vector3(300, 0.5f, 0), new Vector3(2, 1, 2), 6);
            Check(!probe.TryGetGroundHeight(300, 0, 0f, out y), "a layer outside the ground mask is not ground");
            Check(!probe.TryGetGroundHeight(500, 0, 0f, out y), "no ground far away");
            Check(!probe.TryGetGroundHeight(0, 0, 20f, out y), "ground deeper than the ray length from the start is missed");

            Check(probe.IsBlocked(new Vector3(5, 0.2f, 0), new Vector3(0.5f, 0.05f, 0.5f)), "a box inside the step is blocked");
            Check(!probe.IsBlocked(new Vector3(0, 1.0f, 0), new Vector3(0.5f, 0.3f, 0.5f)), "free air is not blocked");
            Check(!probe.IsBlocked(new Vector3(-5, 0.4f, 0), new Vector3(0.5f, 0.2f, 0.5f)), "a trigger does not block");
            Check(probe.IsBlocked(new Vector3(0, 0.9f, 7), new Vector3(0.5f, 0.2f, 0.5f)), "blocker mask is separate from ground mask");
            Check(!new ManagedUnityBuildProbe(~0, 1).IsBlocked(new Vector3(0, 0.9f, 7), new Vector3(0.5f, 0.2f, 0.5f)), "blocker mask excludes layer 6");
            Check(probe.IsBlocked(new Vector3(0, -0.2f, 0), new Vector3(0.5f, 0.1f, 0.5f)), "the ground itself is a collider, which is why the blocker box starts above it");
            Check(!probe.IsBlocked(new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.2f, 0.5f)), "a box that starts 0.1 m above the ground is clear of it");
        }

        // ---- colliders ----------------------------------------------------------------------------------------
        private static void ColliderTests()
        {
            Section("colliders");
            Reset();
            ManagedPlacedStore store = NewStore();
            int early = store.Add(Craft, 0, 0, 0, 0f);
            var parent = new GameObject("pieces");
            var colliders = new ManagedPlacedColliders(store, parent.Transform(), 6);

            Eq(CountProxies(), 1, "a piece that existed first gets a proxy");
            int id = store.Add(Butcher, 10, 4, 1, 0.5f);
            Eq(CountProxies(), 2, "added piece gets a proxy");
            ManagedPlacedPieceProxy px = FindProxy(id);
            Check(px != null && px.gameObject.activeInHierarchy, "proxy is active");
            Eq(px.gameObject.layer, 6, "proxy layer"); Check(px.transform.parent == parent.transform, "proxy parent");
            Eq(px.TypeIndex, Butcher, "type index copied");
            BoxCollider box = px.GetComponent<BoxCollider>();
            Near(box.size.x, 4 * 0.5f, "collider size is the unrotated footprint x"); Near(box.size.z, 2 * 0.5f, "collider size z"); Near(box.size.y, 1.0f, "collider height");

            // The collider's world box is exactly the footprint rectangle the grid claims.
            bool agree = true;
            for (int q = 0; q < 4; q++)
            {
                ManagedPlacedStore s = NewStore();
                Reset();
                var root = new GameObject("r");
                var cc = new ManagedPlacedColliders(s, root.transform, 0);
                int pid = s.Add(Butcher, -7, 3, q, 1.25f);
                ManagedPlacedPieceProxy pp = FindProxy(pid);
                Bounds b = pp.GetComponent<BoxCollider>().WorldBoundsForTest();
                int w, d;
                ManagedBuildGrid.RotatedSize(4, 2, q, out w, out d);
                if (Math.Abs(b.min.x - (-7) * 0.5f) > 1e-3f || Math.Abs(b.max.x - (-7 + w) * 0.5f) > 1e-3f
                    || Math.Abs(b.min.z - 3 * 0.5f) > 1e-3f || Math.Abs(b.max.z - (3 + d) * 0.5f) > 1e-3f
                    || Math.Abs(b.min.y - 1.25f) > 1e-3f || Math.Abs(b.max.y - 2.25f) > 1e-3f) agree = false;
                cc.Dispose();
            }

            Check(agree, "collider bounds equal the footprint for every quarter");

            Reset();
            store = NewStore();
            parent = new GameObject("pieces");
            colliders = new ManagedPlacedColliders(store, parent.transform, 0);
            Ground();
            int a = store.Add(Craft, 0, 0, 0, 0f);
            RaycastHit hit;
            Check(Physics.Raycast(new Vector3(0.7f, 5, 0.5f), Vector3.down, out hit, 20, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > 0.5f, "the ray hits the piece");
            int proxiesBefore = CountProxies();
            store.Remove(a);
            Check(Physics.Raycast(new Vector3(0.7f, 5, 0.5f), Vector3.down, out hit, 20, ~0, QueryTriggerInteraction.Ignore) && hit.point.y < 0.01f, "after removal the ray reaches the ground");
            int b2 = store.Add(Craft, 20, 20, 2, 0f);
            Eq(CountProxies(), proxiesBefore, "a removed proxy is reused, not recreated");
            Eq(FindProxy(b2).PieceId, b2, "reused proxy carries the new id");
            Eq(FindProxy(b2).transform.position.x, (20 + 3 * 0.5f) * 0.5f, "reused proxy moved");

            for (int i = 0; i < 5; i++) store.Add(Craft, i * 10, -20, 0, 0f);
            store.Clear();
            int active = 0;
            foreach (GameObject go in Object.Registry) if (go.GetComponent<ManagedPlacedPieceProxy>() != null && go.activeInHierarchy) active++;
            Eq(active, 0, "Store.Clear deactivates every proxy");
            store.Add(Craft, 0, 0, 0, 0f);
            Eq(FindProxy(0).GetComponent<ManagedPlacedPieceProxy>().PieceId, 0, "ids restart cleanly after Clear");

            colliders.Dispose();
            Eq(CountProxies(), 0, "Dispose destroys every proxy");
            store.Add(Craft, 100, 100, 0, 0f);
            Eq(CountProxies(), 0, "a disposed collider set no longer follows the store");
        }

        private static int CountProxies()
        {
            int n = 0;
            foreach (GameObject go in Object.Registry) if (go.GetComponent<ManagedPlacedPieceProxy>() != null) n++;
            return n;
        }

        private static ManagedPlacedPieceProxy FindProxy(int pieceId)
        {
            foreach (GameObject go in Object.Registry)
            {
                var p = go.GetComponent<ManagedPlacedPieceProxy>();
                if (p != null && go.activeInHierarchy && p.PieceId == pieceId) return p;
            }

            return null;
        }

        // ---- renderer -----------------------------------------------------------------------------------------
        private static void RendererTests()
        {
            Section("renderer");
            Reset();
            ManagedPlacedStore store = NewStore();
            var r = new ManagedPlacedRenderer(store, 3, null);
            r.DrawPieces();
            Eq(Graphics.Calls.Count, 0, "an empty store draws nothing");

            for (int i = 0; i < 2050; i++) store.Add(Craft, (i % 50) * 4, (i / 50) * 3, 0, 0f);
            for (int i = 0; i < 3; i++) store.Add(Butcher, 1000 + i * 10, 0, 0, 0f);
            r.DrawPieces();
            var inst = Graphics.Calls.FindAll(c => c.Kind == "instanced");
            Eq(inst.Count, 4, "three chunks of one type and one of the other");
            Eq(inst[0].Count, 1023, "full chunk"); Eq(inst[1].Count, 1023, "second full chunk"); Eq(inst[2].Count, 4, "partial chunk"); Eq(inst[3].Count, 3, "other type");
            Check(ReferenceEquals(inst[0].Source, store.GetBucket(Craft).GetChunk(0)), "the bucket array goes to the GPU without a copy");
            Check(ReferenceEquals(inst[2].Source, store.GetBucket(Craft).GetChunk(2)), "last chunk array");
            Check(inst.TrueForAll(c => c.Layer == 3), "layer is passed");
            Check(!ReferenceEquals(inst[0].Material, inst[3].Material), "each type has its own material");
            Near(inst[0].Material.Colors["_BaseColor"].r, ManagedBuildableCatalog.Get(Craft).Color.r, "URP tint set on _BaseColor");
            Near(inst[3].Material.Colors["_Color"].g, ManagedBuildableCatalog.Get(Butcher).Color.g, "built in tint set on _Color");
            Check(inst[0].Material.enableInstancing, "instancing is enabled");
            Check(inst[0].Mesh.vertices.Length == 24 && inst[0].Mesh.colors32.Length == 24, "box mesh with white vertex colors");

            // Template material is copied, not modified.
            Reset();
            var template = new Material(Shader.Find("Standard")); template.Colors["_Color"] = new Color(9, 9, 9, 9);
            store = NewStore(); store.Add(Craft, 0, 0, 0, 0f);
            r = new ManagedPlacedRenderer(store, 0, template);
            r.DrawPieces();
            Material used = Graphics.Calls[0].Material;
            Check(!ReferenceEquals(used, template), "the template is copied");
            Near(template.Colors["_Color"].r, 9f, "the template keeps its color");
            Near(used.Colors["_Color"].r, ManagedBuildableCatalog.Get(Craft).Color.r, "the copy carries the piece color");

            // Shader fallbacks.
            Reset();
            Shader.Known = new HashSet<string> { "Sprites/Default" };
            store = NewStore(); store.Add(Craft, 0, 0, 0, 0f);
            r = new ManagedPlacedRenderer(store, 0, null);
            r.DrawPieces();
            Eq(Graphics.Calls[0].Material.shader.name, "Sprites/Default", "falls back down the shader list");
            Reset();
            Shader.Known = new HashSet<string>();
            store = NewStore(); store.Add(Craft, 0, 0, 0, 0f);
            r = new ManagedPlacedRenderer(store, 0, null);
            r.DrawPieces();
            r.DrawGhost(ManagedBuildableCatalog.Get(Craft), new ManagedPlacementResult { Status = ManagedPlacementStatus.Valid }, 0);
            r.DrawHover(0);
            Eq(Graphics.Calls.Count, 0, "with no shader at all nothing draws and nothing throws");
            Check(Debug.Errors.Count >= 2, "and the errors say why");

            // Ghost and hover.
            Reset();
            store = NewStore(); int pid = store.Add(Craft, 4, 6, 1, 0.2f);
            r = new ManagedPlacedRenderer(store, 2, null);
            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(Craft);
            r.DrawGhost(def, new ManagedPlacementResult { Status = ManagedPlacementStatus.NoAim }, 0);
            Eq(Graphics.Calls.Count, 0, "no ghost without an aim");
            var good = new ManagedPlacementResult { Status = ManagedPlacementStatus.Valid, CellX = 10, CellZ = -2, GroundY = 1f };
            r.DrawGhost(def, good, 3);
            DrawCall g = Graphics.Calls[0];
            Eq(g.Kind, "mesh", "ghost is a single mesh draw"); Eq(g.Layer, 2, "ghost layer");
            Near(g.Material.Colors["_Color"].b, 1f, "valid ghost is blue"); Check(g.Material.Colors["_Color"].r < 0.5f, "blue has little red");
            Matrix4x4 expect = ManagedPlacedStore.BuildMatrix(def, 0.5f, 10, -2, 3, 1f) * Matrix4x4.Scale(new Vector3(1.02f, 1.02f, 1.02f));
            Check(Same(g.First, expect), "ghost matrix is the piece matrix inflated by two percent");
            Graphics.Calls.Clear();
            foreach (ManagedPlacementStatus s in Enum.GetValues(typeof(ManagedPlacementStatus)))
            {
                if (s == ManagedPlacementStatus.Valid || s == ManagedPlacementStatus.NoAim) continue;
                Graphics.Calls.Clear();
                r.DrawGhost(def, new ManagedPlacementResult { Status = s }, 0);
                Check(Graphics.Calls.Count == 1 && Graphics.Calls[0].Material.Colors["_Color"].r > 0.9f && Graphics.Calls[0].Material.Colors["_Color"].b < 0.4f, "ghost is red for " + s);
            }

            Graphics.Calls.Clear();
            r.DrawHover(pid);
            Eq(Graphics.Calls.Count, 1, "hover draws"); Check(Graphics.Calls[0].Material.Colors["_Color"].g > 0.8f && Graphics.Calls[0].Material.Colors["_Color"].b < 0.4f, "hover is yellow");
            Graphics.Calls.Clear();
            r.DrawHover(99); Eq(Graphics.Calls.Count, 0, "hover of an unknown piece draws nothing");
            store.Remove(pid); r.DrawHover(pid); Eq(Graphics.Calls.Count, 0, "hover of a removed piece draws nothing");

            r.Dispose();
            Graphics.Calls.Clear();
            store.Add(Craft, 0, 0, 0, 0f);
            r.DrawPieces();
            Eq(Graphics.Calls.Count, 0, "a disposed renderer draws nothing");
        }

        // ---- input --------------------------------------------------------------------------------------------
        private static void InputTests()
        {
            Section("input");
            Reset();
            ManagedBuildInputFrame f = ManagedBuildInput.Read(Key.B, true);
            Check(!f.TogglePressed && !f.PointerAvailable && f.Rotate == 0 && !f.CancelPressed && !f.PrimaryPressed, "no devices gives an empty frame");

            var kb = new Keyboard(); var ms = new Mouse();
            Keyboard.current = kb; Mouse.current = ms;
            ms.position.Value = new Vector2(120, 340);
            f = ManagedBuildInput.Read(Key.B, true);
            Check(f.PointerAvailable, "mouse present"); Near(f.Pointer.x, 120, "pointer x"); Near(f.Pointer.y, 340, "pointer y");
            Check(!f.TogglePressed, "idle");

            kb[Key.B].wasPressedThisFrame = true;
            Check(ManagedBuildInput.Read(Key.B, true).TogglePressed, "toggle key");
            Check(!ManagedBuildInput.Read(Key.V, true).TogglePressed, "another key is not the toggle");
            kb[Key.B].wasPressedThisFrame = false;

            bool threw = false;
            try { ManagedBuildInput.Read(Key.None, true); ManagedBuildInput.Read(Key.IMESelected, true); } catch (Exception) { threw = true; }
            Check(!threw, "Key.None and IMESelected as the toggle do not throw");
            Check(!ManagedBuildInput.Read(Key.None, true).TogglePressed, "Key.None never toggles");

            kb.rKey.wasPressedThisFrame = true;
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, 1, "R rotates");
            kb.leftShiftKey.isPressed = true;
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, -1, "shift R reverses");
            kb.leftShiftKey.isPressed = false; kb.rightShiftKey.isPressed = true;
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, -1, "right shift too");
            kb.rightShiftKey.isPressed = false; kb.rKey.wasPressedThisFrame = false;

            ms.scroll.Value = new Vector2(0, 120);
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, 1, "wheel up");
            ms.scroll.Value = new Vector2(0, -120);
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, -1, "wheel down");
            Eq(ManagedBuildInput.Read(Key.B, false).Rotate, 0, "wheel ignored when it is not allowed");
            kb.rKey.wasPressedThisFrame = true;
            Eq(ManagedBuildInput.Read(Key.B, true).Rotate, 1, "R wins over the wheel");
            kb.rKey.wasPressedThisFrame = false; ms.scroll.Value = Vector2.zero;

            kb.escapeKey.wasPressedThisFrame = true;
            Check(ManagedBuildInput.Read(Key.B, true).CancelPressed, "Esc cancels");
            kb.escapeKey.wasPressedThisFrame = false;
            ms.rightButton.wasPressedThisFrame = true;
            Check(ManagedBuildInput.Read(Key.B, true).CancelPressed, "right click cancels");
            ms.rightButton.wasPressedThisFrame = false;
            ms.leftButton.wasPressedThisFrame = true;
            f = ManagedBuildInput.Read(Key.B, true);
            Check(f.PrimaryPressed && !f.CancelPressed, "left click is primary");
        }
    }

    internal static class StubExt
    {
        public static Transform Transform(this GameObject go) { return go.transform; }
        public static Bounds WorldBoundsForTest(this BoxCollider c) { return c.WorldBounds(); }
    }
}
