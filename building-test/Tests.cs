// Checks for the Managed Building code. Verification harness only.
using System;
using Object = UnityEngine.Object;
using Cursor = UnityEngine.Cursor;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public sealed class FakeProbe : IManagedBuildWorldProbe
    {
        public Func<float, float, float?> Ground = (x, z) => 0f;
        public Func<Vector3, Vector3, bool> Blocked = (c, h) => false;
        public int GroundCalls, BlockCalls;
        public Vector3 LastBoxCenter, LastBoxHalf;
        public float LastReferenceY;

        public bool TryGetGroundHeight(float x, float z, float referenceY, out float y)
        {
            GroundCalls++;
            LastReferenceY = referenceY;
            float? g = Ground(x, z);
            y = g.HasValue ? g.Value : 0f;
            return g.HasValue;
        }

        public bool IsBlocked(Vector3 center, Vector3 halfExtents)
        {
            BlockCalls++;
            LastBoxCenter = center;
            LastBoxHalf = halfExtents;
            return Blocked(center, halfExtents);
        }
    }

    public sealed class RefusingStock : IManagedBuildStock
    {
        private readonly ManagedBuildInventory _inner = new ManagedBuildInventory();
        public bool RefuseTake;
        public event Action Changed { add { _inner.Changed += value; } remove { _inner.Changed -= value; } }
        public int GetCount(string id) { return _inner.GetCount(id); }
        public bool TryTake(string id, int count) { return !RefuseTake && _inner.TryTake(id, count); }
        public void Give(string id, int count) { _inner.Give(id, count); }
        public void CopyEntries(List<ManagedStockEntry> into) { _inner.CopyEntries(into); }
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

        public static void Near(float actual, float expected, string what, float eps = 1e-4f)
        {
            Check(Math.Abs(actual - expected) <= eps, what + " (expected " + expected + ", got " + actual + ")");
        }

        public static void Reset()
        {
            Object.Registry.Clear();
            Graphics.Calls.Clear();
            Debug.Warnings.Clear();
            Debug.Errors.Clear();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Keyboard.current = null;
            Mouse.current = null;
            Gamepad.current = null;
            UnityEngine.Camera.main = null;
            Stub.Reset();
            MidManStudio.Gtg.Managed.Camera.ManagedCursorBlock.Reset();
            CallStatic(typeof(MidManStudio.Gtg.Managed.Hud.ManagedHudHeatSources), "ResetStatics");
            CallStatic(typeof(MidManStudio.Gtg.Managed.Missions.ManagedMissionSources), "ResetStatics");
            Material.LitProps = new HashSet<string> { "_BaseColor" };
            Shader.Known = new HashSet<string> { "Universal Render Pipeline/Lit", "Standard", "Sprites/Default" };
            Resources.HasLegacyRuntime = true;
            Time.frameCount = 0;
        }

        public static int RunAll()
        {
            Reset();
            GridTests();
            StoreTests();
            BoxMeshTests();
            ValidatorTests();
            StockTests();
            ControllerTests();
            AimAndProbeTests();
            ColliderTests();
            RendererTests();
            InputTests();
            PanelTests();
            UiHostTests();
            ModeTests();
            RigTests();
            ItemTests();
            HudTests();
            FeederTests();
            MeleeTests();
            MeleeBridgeTests();
            FacingTests();
            Console.WriteLine(_checks + " checks, " + _failures + " failed");
            return _failures == 0 ? 0 : 1;
        }

        // ---- helpers ------------------------------------------------------------------------------------------
        public static ManagedBuildRules Rules()
        {
            return new ManagedBuildRules();
        }

        public static ManagedPlacedStore NewStore(float cell = 0.5f)
        {
            return new ManagedPlacedStore(new ManagedBuildGrid(cell), 4);
        }

        public static int Craft { get { return 0; } }
        public static int Butcher { get { return 1; } }

        public static object GetField(object o, string name)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) throw new Exception("no field " + name);
            return f.GetValue(o);
        }

        public static void SetField(object o, string name, object value)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f == null) throw new Exception("no field " + name);
            f.SetValue(o, value);
        }

        public static object Call(object o, string name, params object[] args)
        {
            MethodInfo m = o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (m == null) throw new Exception("no method " + name);
            try { return m.Invoke(o, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        public static object CallStatic(Type type, string name, params object[] args)
        {
            MethodInfo m = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new Exception("no static method " + name);
            try { return m.Invoke(null, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }

        // ---- grid ---------------------------------------------------------------------------------------------
        private static void GridTests()
        {
            Section("grid");
            int w, d;
            ManagedBuildGrid.RotatedSize(3, 2, 0, out w, out d); Eq(w * 10 + d, 32, "quarter 0 keeps size");
            ManagedBuildGrid.RotatedSize(3, 2, 1, out w, out d); Eq(w * 10 + d, 23, "quarter 1 swaps");
            ManagedBuildGrid.RotatedSize(3, 2, 2, out w, out d); Eq(w * 10 + d, 32, "quarter 2 keeps size");
            ManagedBuildGrid.RotatedSize(3, 2, 3, out w, out d); Eq(w * 10 + d, 23, "quarter 3 swaps");
            ManagedBuildGrid.RotatedSize(3, 2, 5, out w, out d); Eq(w * 10 + d, 23, "quarter 5 behaves as 1");

            bool threw = false;
            try { new ManagedBuildGrid(0f); } catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "zero cell size is refused");

            var grid = new ManagedBuildGrid(0.5f);
            var rnd = new Random(7);
            bool closest = true;
            for (int i = 0; i < 4000; i++)
            {
                float x = (float)(rnd.NextDouble() * 40 - 20);
                int width = rnd.Next(1, 7);
                int a = grid.AnchorX(x, width);
                float center = grid.CenterX(a, width);
                if (Math.Abs(center - x) > 0.25f + 1e-4f) closest = false;
                int az = grid.AnchorZ(x, width);
                if (Math.Abs(grid.CenterZ(az, width) - x) > 0.25f + 1e-4f) closest = false;
            }
            Check(closest, "the anchored footprint center is within half a cell of the aim, negatives included");

            Check(grid.AreFree(0, 0, 3, 2), "empty grid is free");
            grid.Occupy(0, 0, 3, 2, 5);
            Check(!grid.AreFree(2, 1, 2, 2), "overlap at the corner is refused");
            Check(grid.AreFree(3, 0, 2, 2), "touching edge is free");
            Eq(grid.OwnerAt(2, 1), 5, "owner at a covered cell");
            Eq(grid.OwnerAt(3, 1), -1, "owner outside is -1");
            Eq(grid.OccupiedCellCount, 6, "six cells covered");
            grid.Occupy(3, 0, 1, 1, 9);
            grid.Release(0, 0, 4, 2, 5);
            Eq(grid.OwnerAt(3, 0), 9, "release leaves cells of another piece");
            Eq(grid.OwnerAt(0, 0), -1, "release frees own cells");
            Eq(grid.OccupiedCellCount, 1, "one cell left");

            grid.Clear();
            grid.Occupy(-1, 0, 1, 1, 1);
            grid.Occupy(0, -1, 1, 1, 2);
            grid.Occupy(-1, -1, 1, 1, 3);
            grid.Occupy(0, 0, 1, 1, 4);
            Eq(grid.OccupiedCellCount, 4, "negative cells do not collide in the key");
            Eq(grid.OwnerAt(-1, 0) * 1000 + grid.OwnerAt(0, -1) * 100 + grid.OwnerAt(-1, -1) * 10 + grid.OwnerAt(0, 0), 1000 + 200 + 30 + 4, "owners of the four quadrants");
            grid.Occupy(int.MinValue, int.MaxValue, 1, 1, 8);
            Eq(grid.OwnerAt(int.MinValue, int.MaxValue), 8, "extreme cell");
        }

        // ---- store --------------------------------------------------------------------------------------------
        private static void StoreTests()
        {
            Section("store");
            ManagedPlacedStore store = NewStore();
            var added = new List<ManagedPlacedPiece>();
            var removed = new List<ManagedPlacedPiece>();
            store.PieceAdded += p => added.Add(p);
            store.PieceRemoved += p => removed.Add(p);

            int a = store.Add(Craft, 0, 0, 0, 0f);
            int b = store.Add(Butcher, 10, 10, 1, 0.25f);
            Eq(a, 0, "first id"); Eq(b, 1, "second id");
            Eq(store.Count, 2, "count");
            Eq(store.Add(Craft, 2, 1, 0, 0f), -1, "overlap refused");
            Eq(store.Count, 2, "refusal changes nothing");
            Eq(added.Count, 2, "added events only for accepted");
            Eq(store.GetBucket(Craft).Count, 1, "craft bucket");
            Eq(store.Add(99, 0, 0, 0, 0f), -1, "unknown type refused");
            Eq(store.Add(-1, 0, 0, 0, 0f), -1, "negative type refused");

            // Rotation turns the footprint, so a rotated table claims a different set of cells.
            Eq(store.Add(Craft, 3, 0, 1, 0f), 2, "rotated table fits next to the first (2 wide, 3 deep)");
            Check(!store.Grid.AreFree(3, 2, 1, 1), "rotated footprint covers z up to 2");
            Check(store.Grid.AreFree(5, 0, 1, 1), "and x only up to 4");

            ManagedPlacedPiece got;
            Check(store.TryGet(b, out got), "TryGet live");
            Eq(got.TypeIndex, Butcher, "type"); Eq(got.CellX, 10, "cellx"); Eq(got.Quarter, 1, "quarter"); Near(got.GroundY, 0.25f, "ground");
            Check(!store.TryGet(77, out got), "TryGet unknown id");

            // Matrix of the bucket entry equals the static builder.
            Matrix4x4 expect = ManagedPlacedStore.BuildMatrix(ManagedBuildableCatalog.Get(Butcher), 0.5f, 10, 10, 1, 0.25f);
            int idx = -1;
            ManagedPieceBucket bucket = store.GetBucket(Butcher);
            for (int i = 0; i < bucket.Count; i++) if (bucket.PieceIdAt(i) == b) idx = i;
            Check(idx >= 0, "piece is in its bucket");
            Check(Same(bucket.MatrixAt(idx), expect), "bucket matrix equals BuildMatrix");

            Check(store.Remove(a), "remove");
            Check(!store.Remove(a), "second remove refused");
            Check(!store.IsAlive(a), "dead after remove");
            Eq(removed.Count, 1, "removed event");
            Eq(removed[0].Id, a, "removed snapshot id");
            Check(store.Grid.AreFree(0, 0, 3, 2), "cells freed");
            Eq(store.Add(Craft, 0, 0, 0, 0f), a, "freed id is reused");

            // Swap remove keeps the index map right.
            ManagedPlacedStore s2 = NewStore();
            var ids = new List<int>();
            for (int i = 0; i < 6; i++) ids.Add(s2.Add(Craft, i * 10, 0, 0, 0f));
            s2.Remove(ids[1]);
            s2.Remove(ids[4]);
            Check(BucketsConsistent(s2), "buckets consistent after swap removes");
            s2.Remove(ids[0]); s2.Remove(ids[5]); s2.Remove(ids[2]); s2.Remove(ids[3]);
            Eq(s2.Count, 0, "all removed");
            Eq(s2.GetBucket(Craft).Count, 0, "bucket empty");
            Eq(s2.Grid.OccupiedCellCount, 0, "grid empty");

            // Chunks of 1023.
            ManagedPlacedStore big = NewStore();
            for (int i = 0; i < 2050; i++) big.Add(Craft, (i % 50) * 4, (i / 50) * 3, i & 3, 0f);
            ManagedPieceBucket bb = big.GetBucket(Craft);
            Eq(big.Count, 2050, "2050 pieces");
            Eq(bb.ChunkCount, 3, "three chunks");
            Eq(bb.GetChunkLength(0) * 100000 + bb.GetChunkLength(1) * 100 + bb.GetChunkLength(2), 1023 * 100000 + 1023 * 100 + 4, "chunk lengths");
            Eq(bb.GetChunk(0).Length, 1023, "chunk arrays hold 1023");
            Check(BucketsConsistent(big), "big buckets consistent");
            for (int i = 0; i < 2050; i += 3) big.Remove(i);
            Check(BucketsConsistent(big), "big buckets consistent after removing every third");
            Eq(bb.GetChunkLength(bb.ChunkCount - 1) > 0, true, "last chunk is not empty");

            // Fuzz.
            ManagedPlacedStore fz = NewStore();
            var live = new List<int>();
            var rnd = new Random(1234);
            bool ok = true;
            for (int step = 0; step < 6000 && ok; step++)
            {
                if (live.Count == 0 || rnd.Next(100) < 55)
                {
                    int id = fz.Add(rnd.Next(2), rnd.Next(-30, 30) * 5, rnd.Next(-30, 30) * 5, rnd.Next(4), (float)rnd.NextDouble());
                    if (id >= 0) live.Add(id);
                }
                else
                {
                    int k = rnd.Next(live.Count);
                    fz.Remove(live[k]);
                    live.RemoveAt(k);
                }

                if (step % 97 == 0) ok = BucketsConsistent(fz) && fz.Count == live.Count;
            }

            Check(ok, "fuzz keeps buckets, grid and counts consistent");

            // Clear raises one removed event per piece and resets ids.
            ManagedPlacedStore s3 = NewStore();
            int removedEvents = 0;
            s3.PieceRemoved += p => removedEvents++;
            s3.Add(Craft, 0, 0, 0, 0f); s3.Add(Butcher, 10, 0, 0, 0f); s3.Add(Craft, 20, 0, 0, 0f);
            s3.Clear();
            Eq(removedEvents, 3, "Clear raises PieceRemoved per piece");
            Eq(s3.Count, 0, "Clear empties"); Eq(s3.Grid.OccupiedCellCount, 0, "Clear frees the grid");
            Eq(s3.Add(Craft, 0, 0, 0, 0f), 0, "ids restart after Clear");
            ManagedPlacedStore s4 = NewStore();
            s4.Add(Craft, 0, 0, 0, 0f);
            s4.Clear();
            Eq(s4.Count, 0, "Clear without listeners");
            var copy = new List<ManagedPlacedPiece>();
            s3.CopyPieces(copy);
            Eq(copy.Count, 1, "CopyPieces after Clear and one Add");

            // Matrix: corners of the unit box land inside the footprint rectangle for every quarter.
            ManagedBuildableDefinition def = ManagedBuildableCatalog.Get(Craft);
            bool corners = true;
            for (int q = 0; q < 4; q++)
            {
                Matrix4x4 m = ManagedPlacedStore.BuildMatrix(def, 0.5f, 4, -3, q, 1.5f);
                int ww, dd;
                ManagedBuildGrid.RotatedSize(def.FootprintCellsX, def.FootprintCellsZ, q, out ww, out dd);
                float x0 = 4 * 0.5f, x1 = (4 + ww) * 0.5f, z0 = -3 * 0.5f, z1 = (-3 + dd) * 0.5f;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? -0.5f : 0.5f, (c & 2) == 0 ? -0.5f : 0.5f, (c & 4) == 0 ? -0.5f : 0.5f));
                    bool inside = p.x > x0 - 1e-3f && p.x < x1 + 1e-3f && p.z > z0 - 1e-3f && p.z < z1 + 1e-3f && p.y > 1.5f - 1e-3f && p.y < 1.5f + def.HeightMeters + 1e-3f;
                    // Every corner sits on the footprint border.
                    bool onX = Math.Abs(p.x - x0) < 1e-3f || Math.Abs(p.x - x1) < 1e-3f;
                    bool onZ = Math.Abs(p.z - z0) < 1e-3f || Math.Abs(p.z - z1) < 1e-3f;
                    if (!inside || !onX || !onZ) corners = false;
                }
            }

            Check(corners, "BuildMatrix corners match the rotated footprint for all four quarters");
        }

        public static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            float[] x = a.ToArray(), y = b.ToArray();
            for (int i = 0; i < 16; i++) if (Math.Abs(x[i] - y[i]) > 1e-4f) return false;
            return true;
        }

        // Every live piece is in its bucket once, with the matrix BuildMatrix gives, and owns its cells.
        public static bool BucketsConsistent(ManagedPlacedStore store)
        {
            var pieces = new List<ManagedPlacedPiece>();
            store.CopyPieces(pieces);
            int inBuckets = 0;
            for (int t = 0; t < ManagedBuildableCatalog.Count; t++)
            {
                ManagedPieceBucket b = store.GetBucket(t);
                for (int i = 0; i < b.Count; i++)
                {
                    ManagedPlacedPiece p;
                    if (!store.TryGet(b.PieceIdAt(i), out p) || p.TypeIndex != t) return false;
                    if (!Same(b.MatrixAt(i), ManagedPlacedStore.BuildMatrix(ManagedBuildableCatalog.Get(t), store.Grid.CellSize, p.CellX, p.CellZ, p.Quarter, p.GroundY))) return false;
                    inBuckets++;
                }
            }

            if (inBuckets != pieces.Count) return false;
            int cells = 0;
            foreach (ManagedPlacedPiece p in pieces)
            {
                ManagedBuildableDefinition d = ManagedBuildableCatalog.Get(p.TypeIndex);
                int w, dd;
                ManagedBuildGrid.RotatedSize(d.FootprintCellsX, d.FootprintCellsZ, p.Quarter, out w, out dd);
                for (int x = 0; x < w; x++) for (int z = 0; z < dd; z++) if (store.Grid.OwnerAt(p.CellX + x, p.CellZ + z) != p.Id) return false;
                cells += w * dd;
            }

            return cells == store.Grid.OccupiedCellCount;
        }

        // ---- box mesh -----------------------------------------------------------------------------------------
        private static void BoxMeshTests()
        {
            Section("boxmesh");
            Vector3[] v, n; int[] t;
            ManagedBoxMesh.Build(out v, out n, out t);
            Eq(v.Length, ManagedBoxMesh.VertexCount, "vertex count"); Eq(n.Length, 24, "normal count"); Eq(t.Length, ManagedBoxMesh.IndexCount, "index count");
            bool range = true; foreach (int i in t) if (i < 0 || i >= 24) range = false;
            Check(range, "indices in range");
            bool extent = true;
            foreach (Vector3 p in v) if (Math.Abs(Math.Abs(p.x) - 0.5f) > 1e-5f && Math.Abs(Math.Abs(p.y) - 0.5f) > 1e-5f && Math.Abs(Math.Abs(p.z) - 0.5f) > 1e-5f) extent = false;
            foreach (Vector3 p in v) if (Math.Abs(p.x) > 0.5001f || Math.Abs(p.y) > 0.5001f || Math.Abs(p.z) > 0.5001f) extent = false;
            Check(extent, "vertices lie on a unit box");

            bool outward = true, normalsMatch = true;
            var faces = new HashSet<string>();
            for (int tri = 0; tri < 12; tri++)
            {
                Vector3 a = v[t[tri * 3]], b = v[t[tri * 3 + 1]], c = v[t[tri * 3 + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                var cross = new Vector3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x);
                Vector3 centroid = (a + b + c) * (1f / 3f);
                float dot = cross.x * centroid.x + cross.y * centroid.y + cross.z * centroid.z;
                if (dot <= 0) outward = false;
                Vector3 vn = n[t[tri * 3]];
                float align = cross.x * vn.x + cross.y * vn.y + cross.z * vn.z;
                if (align <= 0) normalsMatch = false;
                faces.Add(vn.ToString());
            }

            Check(outward, "every triangle winds so its geometric normal points away from the center");
            Check(normalsMatch, "vertex normals agree with the winding");
            Eq(faces.Count, 6, "six distinct face normals");
            float area = 0;
            for (int tri = 0; tri < 12; tri++)
            {
                Vector3 a = v[t[tri * 3]], b = v[t[tri * 3 + 1]], c = v[t[tri * 3 + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                var cr = new Vector3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x);
                area += cr.magnitude * 0.5f;
            }

            Near(area, 6f, "surface area of a unit box");
        }

        // ---- validator ----------------------------------------------------------------------------------------
        private static void ValidatorTests()
        {
            Section("validator");
            ManagedBuildRules rules = Rules();
            ManagedPlacedStore store = NewStore();
            var probe = new FakeProbe();
            ManagedBuildableDefinition craft = ManagedBuildableCatalog.Get(Craft);
            Vector3 origin = Vector3.zero;

            ManagedPlacementResult r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, false, new Vector3(2, 0, 2), origin);
            Eq(r.Status, ManagedPlacementStatus.NoAim, "no aim"); Eq(probe.GroundCalls, 0, "no probe call without aim");

            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(2, 0, 2), origin);
            Eq(r.Status, ManagedPlacementStatus.Valid, "flat free spot is valid");
            Eq(probe.GroundCalls, 5, "five ground samples"); Eq(probe.BlockCalls, 1, "one blocker query");
            Near(r.Center.x, r.CellX * 0.5f + 0.75f, "center x is the footprint middle"); Near(r.Center.z, r.CellZ * 0.5f + 0.5f, "center z");
            Near(probe.LastBoxHalf.x, (1.5f - 0.1f) * 0.5f, "blocker half x is inset by the edge inset on both sides");
            Near(probe.LastBoxHalf.z, (1.0f - 0.1f) * 0.5f, "blocker half z");
            Near(probe.LastBoxHalf.y, (0.9f - 0.1f) * 0.5f, "blocker half height is the height minus the skin");
            Near(probe.LastBoxCenter.y, 0f + 0.1f + (0.9f - 0.1f) * 0.5f, "blocker box starts above the ground by the skin");

            // Rotated footprint changes the blocker box.
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 1, true, new Vector3(2, 0, 2), origin);
            Near(probe.LastBoxHalf.x, (1.0f - 0.1f) * 0.5f, "rotated blocker half x");
            Near(probe.LastBoxHalf.z, (1.5f - 0.1f) * 0.5f, "rotated blocker half z");

            // Reach.
            int callsBefore = probe.GroundCalls;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(20, 0, 0), origin);
            Eq(r.Status, ManagedPlacementStatus.OutOfReach, "too far");
            Eq(probe.GroundCalls, callsBefore, "out of reach costs no query");
            rules.MaxReach = 0f;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(20, 0, 0), origin);
            Eq(r.Status, ManagedPlacementStatus.Valid, "reach limit off");
            rules.MaxReach = 8f;

            // Occupied is decided before any query.
            store.Add(Craft, 3, 3, 0, 0f);
            probe.GroundCalls = 0; probe.BlockCalls = 0;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(3, 0, 2.2f), origin);
            Eq(r.Status, ManagedPlacementStatus.Occupied, "occupied"); Eq(probe.GroundCalls, 0, "occupied costs no ground query");

            // Ground.
            probe.Ground = (x, z) => x > 2.0f && x < 2.3f ? (float?)null : 0f;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(2.25f, 0, -2f), origin);
            Eq(r.Status, ManagedPlacementStatus.NoGround, "a missing sample is NoGround");
            probe.Blocked = (c, h) => false;

            probe.Ground = (x, z) => x > 2.3f ? 0.3f : 0f;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(2.25f, 0, -2f), origin);
            Eq(r.Status, ManagedPlacementStatus.Uneven, "0.3 m step is uneven");
            probe.Ground = (x, z) => x > 2.3f ? 0.1f : 0f;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(2.25f, 0, -2f), origin);
            Eq(r.Status, ManagedPlacementStatus.Valid, "0.1 m step is allowed");
            Near(r.GroundY, 0.1f, "ground is the highest sample");
            probe.Ground = (x, z) => 0f;

            // Blocker.
            probe.Blocked = (c, h) => true;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, craft, 0, true, new Vector3(2.25f, 0, -2f), origin);
            Eq(r.Status, ManagedPlacementStatus.Blocked, "blocked");
            probe.Blocked = (c, h) => false;

            // A footprint shorter than the skin has no blocker box.
            var flat = new ManagedBuildableDefinition { Id = "flat", DisplayName = "x", FootprintCellsX = 1, FootprintCellsZ = 1, HeightMeters = 0.05f };
            probe.BlockCalls = 0;
            r = ManagedPlacementValidator.Evaluate(rules, store, probe, flat, 0, true, new Vector3(-5, 0, -5), origin);
            Eq(probe.BlockCalls, 0, "no blocker box for a piece lower than the skin");

            Eq(ManagedPlacementValidator.Describe(ManagedPlacementStatus.Valid), "", "Describe(Valid) is empty");
            bool all = true;
            foreach (ManagedPlacementStatus s in Enum.GetValues(typeof(ManagedPlacementStatus)))
                if (s != ManagedPlacementStatus.Valid && ManagedPlacementValidator.Describe(s).Length == 0) all = false;
            Check(all, "every refusal has text");
            Check(new ManagedPlacementResult().IsValid, "the zero result reads valid, which is why the controller resets to NoAim");
        }

        // ---- stock and blueprints -----------------------------------------------------------------------------
        private static void StockTests()
        {
            Section("stock");
            var inv = new ManagedBuildInventory();
            int changes = 0;
            inv.Changed += () => changes++;
            Eq(inv.GetCount("x"), 0, "unknown id is zero");
            Eq(inv.GetCount(null), 0, "null id is zero");
            inv.Give("crafting_table", 2); inv.Give("wood", 5); inv.Give("crafting_table", 1);
            Eq(inv.GetCount("crafting_table"), 3, "give adds");
            Eq(changes, 3, "changed per give");
            inv.Give("", 1); inv.Give("x", 0); inv.Give("x", -2);
            Eq(changes, 3, "bad gives are ignored");
            Check(!inv.TryTake("wood", 6), "cannot take more than held"); Eq(inv.GetCount("wood"), 5, "unchanged after refusal");
            Check(!inv.TryTake("wood", 0), "zero take refused"); Check(!inv.TryTake("wood", -1), "negative take refused");
            Check(inv.TryTake("wood", 5), "take all"); Eq(inv.GetCount("wood"), 0, "now zero");
            var entries = new List<ManagedStockEntry>();
            inv.CopyEntries(entries);
            Eq(entries.Count, 1, "zero-count entries are not listed"); Eq(entries[0].ItemId, "crafting_table", "entry id");
            inv.Give("wood", 1);
            entries.Clear(); inv.CopyEntries(entries);
            Eq(entries.Count, 2, "wood returns"); Eq(entries[1].ItemId, "wood", "order keeps first appearance");

            var book = new ManagedBlueprintBook();
            int bc = 0; book.Changed += () => bc++;
            ManagedBuildableDefinition craft = ManagedBuildableCatalog.Get(Craft);
            Check(!book.IsAvailable(craft), "gated until unlocked");
            book.Unlock("crafting_table"); book.Unlock("crafting_table");
            Eq(bc, 1, "unlock raises once"); Check(book.IsAvailable(craft), "available after unlock"); Check(book.IsUnlocked("crafting_table"), "IsUnlocked");
            book.Lock("crafting_table"); book.Lock("crafting_table");
            Eq(bc, 2, "lock raises once"); Check(!book.IsAvailable(craft), "locked again");
            var free = new ManagedBuildableDefinition { Id = "free", RequiresBlueprint = false };
            Check(book.IsAvailable(free), "ungated definitions are always available");
            book.Unlock(null); book.Unlock("");
            Eq(bc, 2, "empty unlock ignored");

            Section("catalog");
            var seen = new HashSet<string>();
            bool unique = true;
            for (int i = 0; i < ManagedBuildableCatalog.Count; i++)
            {
                ManagedBuildableDefinition d = ManagedBuildableCatalog.Get(i);
                if (!seen.Add(d.Id)) unique = false;
                int idx; Check(ManagedBuildableCatalog.TryGetIndex(d.Id, out idx) && idx == i, "TryGetIndex round trip " + d.Id);
                Check(d.FootprintCellsX > 0 && d.FootprintCellsZ > 0 && d.HeightMeters > 0 && d.Cost != null, "definition " + d.Id + " is sane");
            }

            Check(unique, "ids are unique");
            ManagedBuildableDefinition none;
            Check(!ManagedBuildableCatalog.TryGet("nope", out none) && none == null, "unknown id");
            Check(!ManagedBuildableCatalog.TryGet(null, out none), "null id");
        }
    }
}
