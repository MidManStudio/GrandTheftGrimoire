using System.Runtime.InteropServices;

namespace MidManStudio.Gtg.NPC.Native
{
    // Must match npc-ffi/src/lib.rs exactly. All booleans are 32-bit integers.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct NPCNativeObservation
    {
        [FieldOffset(0)] public ulong NpcId;
        [FieldOffset(8)] public uint Role;
        [FieldOffset(12)] public uint Backend;
        [FieldOffset(16)] public uint ThreatVisible;
        [FieldOffset(20)] public float HealthFraction;
        [FieldOffset(24)] public uint CanMove;
        [FieldOffset(28)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct NPCNativeDecision
    {
        [FieldOffset(0)] public ulong NpcId;
        [FieldOffset(8)] public int Action;
        [FieldOffset(12)] public uint Reserved;
    }
}
