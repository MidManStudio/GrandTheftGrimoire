using System;
using System.Runtime.InteropServices;

namespace MidManStudio.Gtg.NPC.Native
{
    internal static class NPCNativeLib
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#else
        private const string Library = "gtg_npc_ffi";
#endif
        internal const uint AbiVersion = 4;
        internal const int MaxBatch = 4096;
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint gtg_npc_abi_version();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint gtg_npc_observation_size();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint gtg_npc_decision_size();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int gtg_npc_decide_batch(IntPtr inputs, uint count, IntPtr outputs, uint capacity);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int gtg_npc_resolve_mission(uint order, uint companionLevel, uint missionLevel, uint noise);

        private static bool checkedAbi;
        private static bool available;
        private static bool warned;

        internal static bool TryDecide(NPCNativeObservation[] inputs, NPCNativeDecision[] outputs, int count)
        {
            if (count == 0) return true;
            if (count < 0 || count > MaxBatch || inputs == null || outputs == null || inputs.Length < count || outputs.Length < count)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!EnsureAbi()) return false;
            var inputHandle = GCHandle.Alloc(inputs, GCHandleType.Pinned);
            var outputHandle = GCHandle.Alloc(outputs, GCHandleType.Pinned);
            try
            {
                int result = gtg_npc_decide_batch(inputHandle.AddrOfPinnedObject(), (uint)count,
                    outputHandle.AddrOfPinnedObject(), (uint)outputs.Length);
                if (result == 0) return true;
                WarnOnce("NPC native batch returned status " + result + "; using managed fallback.");
                return false;
            }
            catch (DllNotFoundException) { available = false; WarnOnce("NPC native library unavailable; using managed fallback."); return false; }
            catch (EntryPointNotFoundException) { available = false; WarnOnce("NPC batch entry point unavailable; using managed fallback."); return false; }
            finally { outputHandle.Free(); inputHandle.Free(); }
        }
        // Loads the library once and checks its version and struct sizes. False means the managed fallback runs.
        private static bool EnsureAbi()
        {
            if (!checkedAbi)
            {
                checkedAbi = true;
                try
                {
                    available = gtg_npc_abi_version() == AbiVersion &&
                        gtg_npc_observation_size() == Marshal.SizeOf<NPCNativeObservation>() &&
                        gtg_npc_decision_size() == Marshal.SizeOf<NPCNativeDecision>();
                    if (!available) WarnOnce("NPC native ABI mismatch; using managed fallback.");
                }
                catch (DllNotFoundException) { WarnOnce("NPC native library missing; using managed fallback."); }
                catch (EntryPointNotFoundException) { WarnOnce("NPC native entry point missing; using managed fallback."); }
            }
            return available;
        }

        // How an off-screen mission ended, as decided by the Rust library. False when the library is not usable
        // or rejects the input (an order that is not a mission, or a level above 65535). The caller then falls
        // back to NPCManagedMission, which applies the same checks.
        internal static bool TryResolveMission(uint order, uint companionLevel, uint missionLevel, uint noise, out int outcome)
        {
            outcome = 0;
            if (!EnsureAbi()) return false;
            try
            {
                int result = gtg_npc_resolve_mission(order, companionLevel, missionLevel, noise);
                if (result < 0) return false;
                outcome = result;
                return true;
            }
            catch (DllNotFoundException) { available = false; return false; }
            catch (EntryPointNotFoundException) { available = false; WarnOnce("NPC mission entry point unavailable; using managed fallback."); return false; }
        }

        private static void WarnOnce(string message)
        {
            if (warned) return;
            warned = true;
            UnityEngine.Debug.LogWarning(message);
        }
    }
}
