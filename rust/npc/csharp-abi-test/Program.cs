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
        private const ulong Seed3 = 0x47544702UL;
        private const int Actions = 10;
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
                ulong r3 = Mix(Seed3 + (ulong)i);
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
                    Order = (byte)(role == 5 ? (r2 >> 24) % 6 : 0),
                    OrderLevel = (ushort)(role == 5 ? (r2 >> 32) % 40 : 0),
                    Trustworthiness = role == 5 ? (float)(r3 % 1001) / 1000f : 0f,
                    Affinity = role == 5 ? (float)((r3 >> 12) % 1001) / 1000f : 0f,
                    PaySatisfaction = role == 5 ? (float)((r3 >> 24) % 1001) / 1000f : 0f,
                    Noise = role == 5 ? (uint)(r3 >> 32) : 0u,
                    BetrayalOpportunity = (byte)(role == 5 && (r2 >> 40) % 100 < 30 ? 1 : 0)
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
                "Disposition", "Order", "Provoked", "ReservedByte", "Level", "OrderLevel", "Trustworthiness", "Affinity", "PaySatisfaction",
                "Noise", "BetrayalOpportunity", "ReservedA", "ReservedB", "ReservedC" };
            int[] obsOffsets = { 0, 8, 12, 16, 20, 24, 28, 32, 33, 34, 35, 36, 38, 40, 44, 48, 52, 56, 57, 58, 60 };
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
            Console.WriteLine("# mixed_actions idle={0} trade={1} patrol={2} attack={3} retreat={4} follow={5} hold={6} refuse={7} mission={8} betray={9} fnv1a=0x{10:x16}",
                hist[0], hist[1], hist[2], hist[3], hist[4], hist[5], hist[6], hist[7], hist[8], hist[9], hash);

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
            string[] badNames = { "disposition 3", "order 6", "provoked 2", "reserved byte", "reserved a", "reserved c", "opportunity 2", "trust nan", "affinity infinity" };
            for (int k = 0; k < badNames.Length; k++)
            {
                Array.Copy(pool, 0, input, 0, 2);
                switch (k)
                {
                    case 0: input[1].Disposition = 3; break;
                    case 1: input[1].Order = 6; break;
                    case 2: input[1].Provoked = 2; break;
                    case 3: input[1].ReservedByte = 1; break;
                    case 4: input[1].ReservedA = 1; break;
                    case 5: input[1].ReservedC = 1; break;
                    case 6: input[1].BetrayalOpportunity = 2; break;
                    case 7: input[1].Trustworthiness = float.NaN; break;
                    default: input[1].Affinity = float.PositiveInfinity; break;
                }

                native[0] = sentinel; native[1] = sentinel;
                Check(!NPCNativeLib.TryDecide(input, native, 2) && native[0].Action == 9 && native[1].Action == 9, "native rejects " + badNames[k] + " and writes nothing");
            }

            // The refusal gap depends on the order type, and C# and Rust agree on it.
            // Level 10 accepts a delivery up to 16, an attack up to 13 and a raid up to 11.
            int[][] gapCases = { new[] { 4, 16, 8 }, new[] { 4, 17, 7 }, new[] { 3, 13, 5 }, new[] { 3, 14, 7 }, new[] { 5, 11, 8 }, new[] { 5, 12, 7 } };
            foreach (int[] c in gapCases)
            {
                input[0] = new NPCNativeObservation { NpcId = 5, Role = 5, Backend = 1, HealthFraction = 1f, CanMove = 1, Level = 10, Order = (byte)c[0], OrderLevel = (ushort)c[1], Trustworthiness = 1f, Affinity = 1f, PaySatisfaction = 1f };
                Check(NPCNativeLib.TryDecide(input, native, 1) && native[0].Action == c[2], "native: order " + c[0] + " at level " + c[1] + " gives action " + c[2] + ", got " + native[0].Action);
                Check(NPCManagedFallback.Decide(input[0]).Action == c[2], "fallback: order " + c[0] + " at level " + c[1] + " gives action " + c[2]);
            }

            // The betrayal line, exactly. A companion with no loyalty has a chance of 0.5, so the roll
            // (noise >> 8 over 2^24) must be below 0.5. A roll exactly on the line does not betray.
            uint[] lineNoise = { 0x7FFFFFFFu, 0x80000000u, 0x7FFFFF00u, 0x80000100u };
            int[] lineExpected = { 9, -1, 9, -1 };
            for (int k = 0; k < lineNoise.Length; k++)
            {
                input[0] = new NPCNativeObservation { NpcId = 6, Role = 5, Backend = 1, HealthFraction = 1f, CanMove = 1, Level = 10, BetrayalOpportunity = 1, Noise = lineNoise[k] };
                bool betrays = lineExpected[k] == 9;
                Check(NPCNativeLib.TryDecide(input, native, 1) && (native[0].Action == 9) == betrays, "native: noise 0x" + lineNoise[k].ToString("X8") + (betrays ? " betrays" : " does not betray"));
                Check((NPCManagedFallback.Decide(input[0]).Action == 9) == betrays, "fallback: noise 0x" + lineNoise[k].ToString("X8") + (betrays ? " betrays" : " does not betray"));
            }

            // Off-screen mission resolution: the C# copy and the Rust library agree on every input tried, and both
            // reject the same bad input.
            ulong state = 0x1234567UL;
            long missionMismatches = 0, missionChecked = 0;
            var outcomes = new long[4];
            for (int i = 0; i < 200000; i++)
            {
                state = Mix(state);
                uint order = (state & 1) == 0 ? 4u : 5u;
                uint companionLevel = (uint)((state >> 8) % 60);
                uint missionLevel = (uint)((state >> 20) % 60);
                uint noise = (uint)(state >> 32);
                bool nativeOk = NPCNativeLib.TryResolveMission(order, companionLevel, missionLevel, noise, out int nativeOutcome);
                bool managedOk = NPCManagedMission.TryResolve(order, companionLevel, missionLevel, noise, out NpcMissionOutcome managedOutcome);
                if (!nativeOk || !managedOk || nativeOutcome != (int)managedOutcome) missionMismatches++;
                else outcomes[nativeOutcome]++;
                missionChecked++;
            }
            Check(missionMismatches == 0, "mission resolution matches between Rust and C# on " + missionChecked + " inputs (" + missionMismatches + " differ)");
            foreach (long n in outcomes) Check(n > 0, "every mission outcome occurs");
            Console.WriteLine("# missions success={0} failed={1} caught={2} killed={3}", outcomes[0], outcomes[1], outcomes[2], outcomes[3]);
            foreach (uint badOrder in new uint[] { 0, 1, 2, 3, 6, 99 })
            {
                Check(!NPCNativeLib.TryResolveMission(badOrder, 10, 10, 0, out _), "native rejects mission order " + badOrder);
                Check(!NPCManagedMission.TryResolve(badOrder, 10, 10, 0, out _), "fallback rejects mission order " + badOrder);
            }
            Check(!NPCNativeLib.TryResolveMission(5, 65536, 10, 0, out _) && !NPCManagedMission.TryResolve(5, 65536, 10, 0, out _), "both reject a companion level above 65535");
            Check(!NPCNativeLib.TryResolveMission(5, 10, 65536, 0, out _) && !NPCManagedMission.TryResolve(5, 10, 65536, 0, out _), "both reject a mission level above 65535");
            Check(NPCNativeLib.TryResolveMission(5, 65535, 65535, 0, out int largest) && largest == 0, "the largest levels are accepted");

            Console.WriteLine(failures == 0 ? "C# ABI test: PASS" : "C# ABI test: FAIL (" + failures + ")");
            return failures == 0 ? 0 : 1;
        }
    }
}
