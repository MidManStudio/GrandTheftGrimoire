using System.Runtime.InteropServices;

namespace MidManStudio.Gtg.NPC.Native
{
    // Prototype API only. Do not call until native binaries are imported for the target platform.
    internal static class NPCNativeLib
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#else
        private const string Library = "gtg_npc_ffi";
#endif
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint gtg_npc_abi_version();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int gtg_npc_decide_stub(uint role, byte threatVisible, float healthFraction, byte canMove);
    }
}
