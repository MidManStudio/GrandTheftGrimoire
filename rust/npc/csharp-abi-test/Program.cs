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
                ulong pct = r % 100;
                uint role = pct < 15 ? 0u : pct < 60 ? 1u : pct < 75 ? 2u : pct < 95 ? 3u : 4u;
                uint sel = (uint)((r >> 8) % 2);
                pool[i] = new NPCNativeObservation
                {
                    NpcId = (ulong)i + 1,
                    Role = role,
                    Backend = role <= 1 ? 0u : role == 2 ? 1u : role == 3 ? sel : 1u + sel,
                    ThreatVisible = ((r >> 16) % 100) < 25 ? 1u : 0u,
                    HealthFraction = (float)((r >> 24) % 1001) / 1000f,
                    CanMove = (role == 0 || ((r >> 40) % 100) < 10) ? 0u : 1u,
                    Reserved = 0
                };
            }
            return pool;
        }

        private static int Main()
        {
            // 1. Layout of the C# mirror structs (explicit layout, so this checks the declarations).
            Check(Marshal.SizeOf<NPCNativeObservation>() == 32, "observation size is 32");
            Check(Marshal.SizeOf<NPCNativeDecision>() == 16, "decision size is 16");
            string[] obsFields = { "NpcId", "Role", "Backend", "ThreatVisible", "HealthFraction", "CanMove", "Reserved" };
            int[] obsOffsets = { 0, 8, 12, 16, 20, 24, 28 };
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
            var hist = new ulong[5];
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
            Console.WriteLine("# mixed_actions idle={0} trade={1} patrol={2} attack={3} retreat={4} fnv1a=0x{5:x16}",
                hist[0], hist[1], hist[2], hist[3], hist[4], hash);

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

            Console.WriteLine(failures == 0 ? "C# ABI test: PASS" : "C# ABI test: FAIL (" + failures + ")");
            return failures == 0 ? 0 : 1;
        }
    }
}
