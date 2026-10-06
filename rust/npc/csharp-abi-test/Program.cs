// Runs the Unity-side native layer (NPCNativeLib / NPCNativeTypes) against the real gtg_npc_ffi
// library and compares it with the managed fallback. Exit code 0 = all checks passed.
// Needs the native library on the loader path (LD_LIBRARY_PATH / DYLD_LIBRARY_PATH / PATH).
using System;
using System.Runtime.InteropServices;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;

namespace MidManStudio.Gtg.NPC.AbiTest
{
    internal static class Program
    {
        private const int Pool = 16384;
        private const ulong Seed = 0x47544700UL;
        private const ulong Seed2 = 0x47544701UL;
        private const int Actions = 8;
        private static int failures;

        private static void Check(bool ok, string what)
        {
            if (ok) return;
            failures++;
            Console.WriteLine("FAIL: " + what);
        }

        private static ulong Mix(ulong x)
        {
            unchecked
            {
                x += 0x9E3779B97F4A7C15UL;
                ulong z = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // Same generator as crates/npc-ffi/examples/decision_throughput.rs and ffi-smoke-test/bench.c.
        private static NPCNativeObservation[] MixedPool()
        {
            var pool = new NPCNativeObservation[Pool];
            for (int i = 0; i < Pool; i++)
            {
                ulong r = Mix(Seed + (ulong)i);
                ulong r2 = Mix(Seed2 + (ulong)i);
                ulong pct = r % 100;
                uint role = pct < 12 ? 0u : pct < 50 ? 1u : pct < 62 ? 2u : pct < 82 ? 3u : pct < 90 ? 4u : 5u;
                uint sel = (uint)((r >> 8) % 2);
                ulong d = r2 % 100;
                pool[i] = new NPCNativeObservation
                {
                    NpcId = (ulong)i + 1,
                    Role = role,
                    Backend = role <= 1 ? 0u : role == 2 ? 1u : (role == 3 || role == 5) ? sel : 1u + sel,
                    ThreatVisible = ((r >> 16) % 100) < 25 ? 1u : 0u,
                    HealthFraction = (float)((r >> 24) % 1001) / 1000f,
                    CanMove = (role == 0 || ((r >> 40) % 100) < 10) ? 0u : 1u,
                    Reserved = 0,
                    Disposition = (byte)((role >= 2 && role <= 4) ? (d < 60 ? 0 : d < 85 ? 1 : 2) : 0),
                    Provoked = (byte)(((r2 >> 8) % 100) < 20 ? 1 : 0),
                    Level = (ushort)(1 + (r2 >> 16) % 30),
                    Order = (byte)(role == 5 ? (r2 >> 24) % 4 : 0),
                    OrderLevel = (ushort)(role == 5 ? (r2 >> 32) % 40 : 0)
                };
            }
            return pool;
        }

        private static int Main()
        {
            // 1. Layout of the C# mirror structs (explicit layout, so this checks the declarations).
            Check(Marshal.SizeOf<NPCNativeObservation>() == 64, "observation size is 64");
            Check(Marshal.SizeOf<NPCNativeDecision>() == 16, "decision size is 16");
            string[] obsFields = { "NpcId", "Role", "Backend", "ThreatVisible", "HealthFraction", "CanMove", "Reserved",
                "Disposition", "Order", "Provoked", "ReservedByte", "Level", "OrderLevel", "ReservedTail0", "ReservedTail1", "ReservedTail2" };
            int[] obsOffsets = { 0, 8, 12, 16, 20, 24, 28, 32, 33, 34, 35, 36, 38, 40, 48, 56 };
            for (int i = 0; i < obsFields.Length; i++)
                Check((int)Marshal.OffsetOf<NPCNativeObservation>(obsFields[i]) == obsOffsets[i], "observation offset of " + obsFields[i]);
            string[] decFields = { "NpcId", "Action", "Reserved" };
            int[] decOffsets = { 0, 8, 12 };
            for (int i = 0; i < decFields.Length; i++)
                Check((int)Marshal.OffsetOf<NPCNativeDecision>(decFields[i]) == decOffsets[i], "decision offset of " + decFields[i]);

            // 2. Real native calls. TryDecide returns false if the ABI version or struct sizes disagree,
            //    the library is missing, or the batch is rejected, so `true` proves the boundary works.
            var pool = MixedPool();
            const int Batch = 256; // same cap as NPCDecisionSystem
            var input = new NPCNativeObservation[Batch];
            var native = new NPCNativeDecision[Batch];
            var hist = new ulong[Actions];
            ulong hash = 0xcbf29ce484222325UL;
            long mismatches = 0;
            for (int off = 0; off < Pool; off += Batch)
            {
                Array.Copy(pool, off, input, 0, Batch);
                bool ok = NPCNativeLib.TryDecide(input, native, Batch);
                Check(ok, "native batch at offset " + off);
                if (!ok) { Console.WriteLine("Aborting: native library unusable."); return 1; }
                for (int i = 0; i < Batch; i++)
                {
                    var managed = NPCManagedFallback.Decide(input[i]);
                    if (native[i].NpcId != input[i].NpcId) { Check(false, "NpcId echoed for NPC " + input[i].NpcId); return 1; }
                    if (native[i].Reserved != 0) Check(false, "decision reserved is zero");
                    if (native[i].Action != managed.Action) mismatches++;
                    hist[native[i].Action]++;
                    unchecked { hash = (hash ^ (ulong)(native[i].Action + 1)) * 0x100000001b3UL; }
                }
            }
            Check(mismatches == 0, "managed fallback matches native for all " + Pool + " NPCs (" + mismatches + " differ)");
            foreach (ulong n in hist) Check(n > 0, "every action occurs in the mixed pool");
            Console.WriteLine("# mixed_actions idle={0} trade={1} patrol={2} attack={3} retreat={4} follow={5} hold={6} refuse={7} fnv1a=0x{8:x16}",
                hist[0], hist[1], hist[2], hist[3], hist[4], hist[5], hist[6], hist[7], hash);

            // 3. Error paths through the real library.
            Check(NPCNativeLib.TryDecide(input, native, 0), "empty batch succeeds");
            bool threw = false;
            try { NPCNativeLib.TryDecide(input, native, NPCNativeLib.MaxBatch + 1); }
            catch (ArgumentOutOfRangeException) { threw = true; }
            Check(threw, "count above MaxBatch throws");
            Array.Copy(pool, 0, input, 0, 2);
            input[1].Role = 99;
            var sentinel = new NPCNativeDecision { NpcId = 999, Action = 9, Reserved = 9 };
            native[0] = sentinel; native[1] = sentinel;
            Check(!NPCNativeLib.TryDecide(input, native, 2), "invalid role is rejected");
            Check(native[0].Action == 9 && native[1].Action == 9, "rejected batch leaves outputs untouched");

            // Each field added in version 3 is validated by the native side. A bad value is rejected as a whole.
            string[] badNames = { "disposition 3", "order 4", "provoked 2", "reserved byte", "reserved tail 0", "reserved tail 2" };
            for (int k = 0; k < badNames.Length; k++)
            {
                Array.Copy(pool, 0, input, 0, 2);
                switch (k)
                {
                    case 0: input[1].Disposition = 3; break;
                    case 1: input[1].Order = 4; break;
                    case 2: input[1].Provoked = 2; break;
                    case 3: input[1].ReservedByte = 1; break;
                    case 4: input[1].ReservedTail0 = 1; break;
                    default: input[1].ReservedTail2 = 1; break;
                }

                native[0] = sentinel; native[1] = sentinel;
                Check(!NPCNativeLib.TryDecide(input, native, 2) && native[0].Action == 9 && native[1].Action == 9, "native rejects " + badNames[k] + " and writes nothing");
            }

            // Companion refusal through the real library: level 10 accepts an order of level 15 and refuses 16.
            var companion = new NPCNativeObservation { NpcId = 5, Role = 5, Backend = 1, HealthFraction = 1f, CanMove = 1, Level = 10, Order = 3, OrderLevel = 15 };
            input[0] = companion; companion.OrderLevel = 16; input[1] = companion;
            Check(NPCNativeLib.TryDecide(input, native, 2) && native[0].Action != (int)MidManStudio.Gtg.NPC.Components.NpcAction.RefuseOrder && native[1].Action == (int)MidManStudio.Gtg.NPC.Components.NpcAction.RefuseOrder, "the refusal gap matches between C# and Rust");

            Console.WriteLine(failures == 0 ? "C# ABI test: PASS" : "C# ABI test: FAIL (" + failures + ")");
            return failures == 0 ? 0 : 1;
        }
    }
}
