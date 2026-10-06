namespace MidManStudio.Gtg.NPC.Components
{
    // Values are the Rust ABI v3 values (rust/npc/crates/npc-ffi/src/lib.rs) and the numbers in
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
        Boss = 4,
        Companion = 5
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

    // How a combatant reacts to a threat it sees. Zero is Hostile, so a caller that never sets it
    // gets the behavior that existed before version 3.
    public enum NpcDisposition : byte
    {
        Hostile = 0,
        Retaliatory = 1,
        Peaceful = 2
    }

    // A standing order for a companion. Other roles ignore it.
    public enum NpcOrder : byte
    {
        None = 0,
        Follow = 1,
        Hold = 2,
        Attack = 3
    }

    // Matches npc-ffi action_code(). The native decision struct keeps a raw int on purpose.
    public enum NpcAction
    {
        Idle = 0,
        Trade = 1,
        Patrol = 2,
        Attack = 3,
        Retreat = 4,
        Follow = 5,
        Hold = 6,
        RefuseOrder = 7
    }
}
