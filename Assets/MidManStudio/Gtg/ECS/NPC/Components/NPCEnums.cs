namespace MidManStudio.Gtg.NPC.Components
{
    // Values are the Rust ABI v2 values (rust/npc/crates/npc-ffi/src/lib.rs) and the numbers in
    // Assets/NPC/Archetypes/archetypes.mdix. Do not renumber without bumping the ABI version.
    // scripts/check_npc_enum_sync.py fails CI if the three sources drift apart.
    // Serialized as integers by Unity, so switching a field from int to one of these enums keeps
    // existing scene and prefab values.

    public enum NpcRole : byte
    {
        Merchant = 0,
        Civilian = 1,
        Guard = 2,
        Enemy = 3,
        Boss = 4
    }

    public enum DecisionBackend : byte
    {
        StateMachine = 0,
        Utility = 1,
        HybridMl = 2
    }

    // Authoring metadata only. Not an ABI field and not an implemented ML feature yet.
    public enum LearningMode : byte
    {
        Disabled = 0,
        FixedWeights = 1,
        OnlineUpdates = 2
    }

    // Matches npc-ffi action_code(). The native decision struct keeps a raw int on purpose.
    public enum NpcAction
    {
        Idle = 0,
        Trade = 1,
        Patrol = 2,
        Attack = 3,
        Retreat = 4
    }
}
