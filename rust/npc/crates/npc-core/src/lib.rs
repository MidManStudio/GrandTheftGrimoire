// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-core.md, section "lib.rs"
// ============================================================================

//! Shared decision contract. Keep the C ABI representation in npc-ffi.

/// The highest Crown Heat tier. The tiers are 0 none, 1 wanted, 2 hunted and 3 outlaw. Only the step from
/// 0 to 1 changes a decision today, see `WANTED_CROWN_HEAT` in `gtg-npc-behavior`. A larger value is
/// rejected at the C boundary, so a host built for more tiers fails loudly.
pub const MAX_CROWN_HEAT: u8 = 3;

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

/// How a combatant fights once it has chosen to. The style says which kinds of attack it has, and the
/// rules in `gtg-npc-behavior` turn it into melee, shooting or keeping its distance.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum AttackStyle {
    /// Closes in and strikes by hand. The default, and the only style before version 6.
    Melee,
    /// Stays away from the threat and shoots, with a bow, a crossbow or a spell.
    Ranged,
    /// Has both and switches between them by distance and by what it has left to shoot.
    Hybrid,
}

/// A standing order given to a companion. Other roles ignore it. Every order has a type, and the type
/// decides how far above its own level a companion still accepts it.
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
    /// Take something to someone. A mission, resolved by the game on screen or off screen.
    Deliver,
    /// Raid a competitor. A mission, resolved by the game on screen or off screen.
    Raid,
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
    /// Carry out the accepted mission. The game knows what the mission is and where.
    Mission,
    /// The companion turns on the player. The game decides what that means.
    Betray,
    /// Fight from a distance: hold a place away from the threat and shoot at it. Only ranged and
    /// hybrid fighters choose it.
    Shoot,
    /// Back away from a threat that has come too close, shooting when it can. Only ranged fighters
    /// choose it.
    Kite,
}

/// How a mission ended, as decided by `resolve_mission` in `gtg-npc-behavior`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum MissionOutcome {
    /// The mission went well. The player gets the reward.
    Success,
    /// The mission failed and the companion comes back.
    Failed,
    /// The companion was caught and can be rescued.
    Caught,
    /// The companion was killed.
    Killed,
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
    /// How trustworthy the companion is, from 0 to 1. Used for betrayal only.
    pub trustworthiness: f32,
    /// How much the companion likes the player, from 0 to 1. Used for betrayal only.
    pub affinity: f32,
    /// How satisfied the companion is with its pay, from 0 to 1. Used for betrayal only.
    pub pay_satisfaction: f32,
    /// A random number the game supplies for each decision, so a chance in a rule stays reproducible.
    pub noise: u32,
    /// The companion could betray the player now. The game decides that, for instance when it is alone
    /// with something valuable, and never sets it for a companion the story protects.
    pub betrayal_opportunity: bool,
    /// The Crown Heat tier of the region the NPC stands in, from 0 to [`MAX_CROWN_HEAT`]. The game keeps
    /// the meter and sends the tier. Zero means no heat, or no information, so a caller that never sets
    /// it gets the behavior from before the field existed. Only guards read it.
    pub crown_heat: u8,
    /// How the NPC fights. Melee, the default, behaves as before version 6.
    pub attack_style: AttackStyle,
    /// The NPC has something to shoot: arrows in its quiver, or mana for its spell. Read by ranged and
    /// hybrid fighters only.
    pub ranged_ready: bool,
    /// The NPC was fighting at range when it was last in a fight, so its last combat action was
    /// [`NpcAction::Shoot`] or [`NpcAction::Kite`]. A hybrid keeps its stance while the threat is in
    /// the buffer between the two distances. The game keeps this memory, not the decision.
    pub ranged_stance: bool,
    /// Distance in meters from the NPC to the threat it is fighting. Zero or less means unknown, and an
    /// unknown distance counts as the buffer between the two ranges, so nothing switches.
    pub threat_distance: f32,
    /// How much the NPC likes the player, from -1 (hates) to 1 (adores). The game keeps the meter and
    /// sends the value. Zero means neutral, or no information, so a caller that never sets it gets the
    /// behavior from before the field existed. A shopkeeper or a civilian that likes the player does not
    /// treat a visible threat as one, see `FRIENDLY_FAVOR` in `gtg-npc-behavior`.
    pub favor: f32,
}

/// The result of one decision.
#[derive(Clone, Copy, Debug)]
pub struct Decision {
    /// The NPC this decision is for.
    pub npc_id: u64,
    /// What it should do.
    pub action: NpcAction,
}
