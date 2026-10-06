using System.Runtime.InteropServices;

namespace MidManStudio.Gtg.NPC.Native
{
    // Must match npc-ffi/src/lib.rs exactly (ABI version 3, 64 bytes). The first 32 bytes are the version 2
    // layout. Booleans are 32-bit integers up to offset 28 and bytes after that, and every Reserved field
    // must stay zero. A zero in every field from Disposition on means: hostile, no order, not provoked, level 0.
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct NPCNativeObservation
    {
        [FieldOffset(0)] public ulong NpcId;
        [FieldOffset(8)] public uint Role;
        [FieldOffset(12)] public uint Backend;
        [FieldOffset(16)] public uint ThreatVisible;
        [FieldOffset(20)] public float HealthFraction;
        [FieldOffset(24)] public uint CanMove;
        [FieldOffset(28)] public uint Reserved;
        [FieldOffset(32)] public byte Disposition;
        [FieldOffset(33)] public byte Order;
        [FieldOffset(34)] public byte Provoked;
        [FieldOffset(35)] public byte ReservedByte;
        [FieldOffset(36)] public ushort Level;
        [FieldOffset(38)] public ushort OrderLevel;
        [FieldOffset(40)] public ulong ReservedTail0;
        [FieldOffset(48)] public ulong ReservedTail1;
        [FieldOffset(56)] public ulong ReservedTail2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct NPCNativeDecision
    {
        [FieldOffset(0)] public ulong NpcId;
        [FieldOffset(8)] public int Action;
        [FieldOffset(12)] public uint Reserved;
    }
}
