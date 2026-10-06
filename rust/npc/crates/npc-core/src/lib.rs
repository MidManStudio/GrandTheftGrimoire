// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-core.md, section "lib.rs"
// ============================================================================

//! Shared decision contract. Keep the C ABI representation in npc-ffi.

/// What an NPC is. The role picks the rule set in `gtg-npc-behavior`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcRole {
    /// Stands at a stall, trades, never fights.
    Merchant,
    /// Walks around and runs from threats.
    Civilian,
    /// Fights what it sees as a threat.
    Guard,
    /// Hostile mob or bandit.
    Enemy,
    /// Enemy with its own place in a story or an encounter.
    Boss,
    /// Follows orders from the player, has a level, and can refuse an order.
    Companion,
}

/// Which decision rules an NPC uses.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DecisionBackend {
    /// Fixed rules only.
    StateMachine,
    /// Scored rules.
    Utility,
    /// Reserved for a learned policy. Uses the utility rules until a model exists.
    HybridMl,
}

/// How a combatant reacts to a threat it sees.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcDisposition {
    /// Attacks a threat on sight. The default, and what mobs always do.
    Hostile,
    /// Ignores a threat until it has been attacked, then fights back.
    Retaliatory,
    /// Never fights. Runs from a threat when it can.
    Peaceful,
}

/// A standing order given to a companion. Other roles ignore it.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcOrder {
    /// No order. Behaves like [`NpcOrder::Follow`].
    None,
    /// Stay with the leader and defend: fight only after being attacked.
    Follow,
    /// Stay where it is.
    Hold,
    /// Stay with the leader and engage any threat it sees.
    Attack,
}

/// What the NPC should do next. The game carries the action out.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcAction {
    /// Stand still.
    Idle,
    /// Offer trade, standing still.
    Trade,
    /// Walk around.
    Patrol,
    /// Fight the threat.
    Attack,
    /// Run away from the threat.
    Retreat,
    /// Walk to the leader and stay near.
    Follow,
    /// Stay exactly where it is.
    Hold,
    /// The companion declines its current order. The game clears the order.
    RefuseOrder,
}

/// Everything the decision knows about one NPC. The game collects it, memory included.
#[derive(Clone, Copy, Debug)]
pub struct Observation {
    /// Identifies the NPC. Echoed back in the decision.
    pub npc_id: u64,
    /// What the NPC is.
    pub role: NpcRole,
    /// Which rule set decides.
    pub backend: DecisionBackend,
    /// A threat is in view.
    pub threat_visible: bool,
    /// Health from 0 to 1.
    pub health_fraction: f32,
    /// False for an NPC that must not move, such as a merchant at a stall.
    pub can_move: bool,
    /// How a combatant reacts to a threat.
    pub disposition: NpcDisposition,
    /// The standing order, for companions.
    pub order: NpcOrder,
    /// The NPC was attacked recently. The game keeps this memory, not the decision.
    pub provoked: bool,
    /// The NPC's own level.
    pub level: u16,
    /// How hard the current order is, as a level. Zero for an order with no difficulty.
    pub order_level: u16,
}

/// The result of one decision.
#[derive(Clone, Copy, Debug)]
pub struct Decision {
    /// The NPC this decision is for.
    pub npc_id: u64,
    /// What it should do.
    pub action: NpcAction,
}
