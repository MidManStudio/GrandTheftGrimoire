// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-behavior.md, section "lib.rs"
// ============================================================================

//! Deterministic baseline: no allocation and no ML required per decision.
use gtg_npc_core::{
    AttackStyle, Decision, DecisionBackend, MissionOutcome, NpcAction, NpcDisposition, NpcOrder,
    NpcRole, Observation,
};

/// How many levels above its own a companion still accepts an order of this type. An order level above
/// that is refused. Dangerous orders allow little, safe ones allow more. Placeholder values, see
/// docs/gtg-npc-behavior.md.
pub const fn refuse_gap(order: NpcOrder) -> u16 {
    match order {
        NpcOrder::None | NpcOrder::Follow | NpcOrder::Hold => 5,
        NpcOrder::Attack => 3,
        NpcOrder::Deliver => 6,
        NpcOrder::Raid => 1,
    }
}

/// How much trustworthiness counts toward loyalty. The three weights add up to 1. Placeholder value.
pub const LOYALTY_WEIGHT_TRUST: f32 = 0.4;
/// How much liking the player counts toward loyalty. Placeholder value.
pub const LOYALTY_WEIGHT_AFFINITY: f32 = 0.3;
/// How much satisfaction with pay counts toward loyalty. Placeholder value.
pub const LOYALTY_WEIGHT_PAY: f32 = 0.3;
/// A companion with loyalty at or above this never betrays. Placeholder value.
pub const BETRAY_LOYALTY_CEILING: f32 = 0.5;
/// The chance of betrayal for a companion with no loyalty at all, at an opportunity. Placeholder value.
pub const BETRAY_MAX_CHANCE: f32 = 0.5;

/// The lowest Crown Heat tier at which a guard treats a visible threat as a wanted criminal and fights it,
/// whatever its disposition says about unprovoked threats. Tier 0 changes nothing. Placeholder value.
pub const WANTED_CROWN_HEAT: u8 = 1;

/// The lowest favor at which a shopkeeper or a civilian counts as liking the player. Favor runs from -1 to
/// 1, so this is 20 on the 100 point meter of the game and the Liked tier starts there. Placeholder value,
/// see docs/gtg-npc-behavior.md.
pub const FRIENDLY_FAVOR: f32 = 0.2;

/// A ranged fighter that can move backs away from a threat that is closer than this many meters, and
/// shoots when it is not. Placeholder value, see docs/gtg-npc-behavior.md.
pub const KITE_DISTANCE: f32 = 6.0;
/// A hybrid fighter at or inside this many meters fights by hand. Placeholder value.
pub const HYBRID_MELEE_DISTANCE: f32 = 4.0;
/// A hybrid fighter at or beyond this many meters shoots. Between this and [`HYBRID_MELEE_DISTANCE`]
/// it keeps the stance it had, so it does not flip back and forth at one distance. Placeholder value.
pub const HYBRID_RANGED_DISTANCE: f32 = 8.0;

/// Chooses what one NPC does next. Pure: the same observation always gives the same decision.
pub fn decide(o: Observation) -> Decision {
    let health = if o.health_fraction.is_finite() {
        o.health_fraction.clamp(0.0, 1.0)
    } else {
        1.0
    };
    let action = match o.role {
        NpcRole::Merchant => merchant(&o, health),
        NpcRole::Civilian => civilian(&o),
        NpcRole::Guard | NpcRole::Enemy | NpcRole::Boss => combatant(&o, health),
        NpcRole::Companion => companion(&o, health),
    };
    Decision {
        npc_id: o.npc_id,
        action,
    }
}

fn wander(can_move: bool) -> NpcAction {
    if can_move {
        NpcAction::Patrol
    } else {
        NpcAction::Idle
    }
}

fn follow_or_idle(can_move: bool) -> NpcAction {
    if can_move {
        NpcAction::Follow
    } else {
        NpcAction::Idle
    }
}

// Whether the NPC likes the player enough to not take the player for a threat: its favor is at or above
// `FRIENDLY_FAVOR`. A number that is not finite counts as neutral, so bad input never makes an NPC friendly.
fn likes_player(o: &Observation) -> bool {
    o.favor.is_finite() && o.favor >= FRIENDLY_FAVOR
}

// A civilian runs from a visible threat when it can move. A civilian that likes the player does not take the
// player for a threat and goes about its day, unless the player has just attacked it.
fn civilian(o: &Observation) -> NpcAction {
    if o.threat_visible && o.can_move && !(likes_player(o) && !o.provoked) {
        NpcAction::Retreat
    } else {
        wander(o.can_move)
    }
}

// A shopkeeper trades when nothing threatens it and stands still when something does. A shopkeeper with the
// retaliatory disposition that has been attacked fights back instead, the way a retaliatory guard does, and
// fights in its own attack style. Hostile and peaceful shopkeepers never fight, and neither does a retaliatory
// one that has not been attacked. A shopkeeper that likes the player keeps trading under the player's eye,
// unless the player has just attacked it. Once the threat is out of sight the shopkeeper trades again.
fn merchant(o: &Observation, health: f32) -> NpcAction {
    if !o.threat_visible {
        NpcAction::Trade
    } else if o.disposition == NpcDisposition::Retaliatory && o.provoked {
        fight(o, health)
    } else if likes_player(o) && !o.provoked {
        NpcAction::Trade
    } else {
        NpcAction::Idle
    }
}

// A guard in a region where the player is wanted. Only guards enforce the law, so no other role reads the
// tier, and a peaceful guard still never fights.
fn enforces_crown_heat(o: &Observation) -> bool {
    o.role == NpcRole::Guard && o.crown_heat >= WANTED_CROWN_HEAT
}

// Guards, enemies and bosses. Disposition decides whether a visible threat is fought at all. A retaliatory
// guard also fights a visible threat when the region's Crown Heat has reached the wanted tier.
fn combatant(o: &Observation, health: f32) -> NpcAction {
    if !o.threat_visible {
        return wander(o.can_move);
    }
    match o.disposition {
        NpcDisposition::Peaceful => {
            if o.can_move {
                NpcAction::Retreat
            } else {
                NpcAction::Idle
            }
        }
        NpcDisposition::Retaliatory if !o.provoked && !enforces_crown_heat(o) => wander(o.can_move),
        NpcDisposition::Hostile | NpcDisposition::Retaliatory => fight(o, health),
    }
}

fn mission_or_idle(can_move: bool) -> NpcAction {
    if can_move {
        NpcAction::Mission
    } else {
        NpcAction::Idle
    }
}

// A value from 0 to 1. A number that is not finite counts as 1, the most loyal value, so bad input never
// causes a betrayal.
fn unit(value: f32) -> f32 {
    if value.is_finite() {
        value.clamp(0.0, 1.0)
    } else {
        1.0
    }
}

/// How loyal a companion is, from 0 to 1: a weighted mix of trustworthiness, liking the player and
/// satisfaction with pay.
pub fn loyalty(o: &Observation) -> f32 {
    LOYALTY_WEIGHT_TRUST * unit(o.trustworthiness)
        + LOYALTY_WEIGHT_AFFINITY * unit(o.affinity)
        + LOYALTY_WEIGHT_PAY * unit(o.pay_satisfaction)
}

/// The chance that a companion with this loyalty betrays the player at an opportunity. Zero at the
/// ceiling, the maximum at no loyalty, linear in between.
pub fn betrayal_chance(loyalty: f32) -> f32 {
    ((BETRAY_LOYALTY_CEILING - loyalty) / BETRAY_LOYALTY_CEILING).clamp(0.0, 1.0)
        * BETRAY_MAX_CHANCE
}

// A number from 0 up to but not including 1, taken from the top 24 bits so it is exact in an f32.
fn roll(noise: u32) -> f32 {
    (noise >> 8) as f32 / 16_777_216.0
}

fn betrays(o: &Observation) -> bool {
    o.betrayal_opportunity && roll(o.noise) < betrayal_chance(loyalty(o))
}

// Companions follow orders unless the order is too hard for their level, and may turn on the player.
fn companion(o: &Observation, health: f32) -> NpcAction {
    if betrays(o) {
        return NpcAction::Betray;
    }
    if o.order != NpcOrder::None && o.order_level > o.level.saturating_add(refuse_gap(o.order)) {
        return NpcAction::RefuseOrder;
    }
    match o.order {
        NpcOrder::Hold => NpcAction::Hold,
        NpcOrder::Attack => {
            if o.threat_visible {
                fight(o, health)
            } else {
                follow_or_idle(o.can_move)
            }
        }
        NpcOrder::Raid => {
            if o.threat_visible {
                fight(o, health)
            } else {
                mission_or_idle(o.can_move)
            }
        }
        NpcOrder::Deliver => {
            if o.threat_visible && o.provoked {
                fight(o, health)
            } else {
                mission_or_idle(o.can_move)
            }
        }
        NpcOrder::None | NpcOrder::Follow => {
            if o.threat_visible && o.provoked {
                fight(o, health)
            } else {
                follow_or_idle(o.can_move)
            }
        }
    }
}

// Fighting a threat: run when badly hurt, otherwise attack the way the fighter's style says.
fn fight(o: &Observation, health: f32) -> NpcAction {
    if health < 0.2 && o.can_move {
        NpcAction::Retreat
    } else if matches!(o.backend, DecisionBackend::StateMachine) {
        engage(o)
    } else {
        match utility_combat(health, o.can_move) {
            NpcAction::Attack => engage(o),
            other => other,
        }
    }
}

// The distance to the threat in meters, or `None` when the game does not know it. Zero, a negative
// number and a number that is not finite all mean unknown.
fn known_distance(o: &Observation) -> Option<f32> {
    if o.threat_distance.is_finite() && o.threat_distance > 0.0 {
        Some(o.threat_distance)
    } else {
        None
    }
}

// How a fighter that has decided to attack does it. A melee fighter closes in. A ranged fighter shoots
// from where it stands and backs away when the threat is too close, and with nothing to shoot it runs, or
// stands when it cannot. A hybrid fights by hand inside the near distance, shoots beyond the far one,
// keeps its stance in between, and fights by hand when it has nothing to shoot.
fn engage(o: &Observation) -> NpcAction {
    match o.attack_style {
        AttackStyle::Melee => NpcAction::Attack,
        AttackStyle::Ranged => {
            if !o.ranged_ready {
                if o.can_move {
                    NpcAction::Retreat
                } else {
                    NpcAction::Idle
                }
            } else if o.can_move && known_distance(o).is_some_and(|d| d < KITE_DISTANCE) {
                NpcAction::Kite
            } else {
                NpcAction::Shoot
            }
        }
        AttackStyle::Hybrid => {
            if !o.ranged_ready {
                return NpcAction::Attack;
            }
            match known_distance(o) {
                Some(d) if d <= HYBRID_MELEE_DISTANCE => NpcAction::Attack,
                Some(d) if d >= HYBRID_RANGED_DISTANCE => NpcAction::Shoot,
                _ if o.ranged_stance => NpcAction::Shoot,
                _ => NpcAction::Attack,
            }
        }
    }
}

// Explicit utility scores provide a stable baseline for a future learned policy.
// HybridMl deliberately falls back to this deterministic path until a model is loaded.
fn utility_combat(health: f32, can_move: bool) -> NpcAction {
    let attack = 0.6 + 0.4 * health;
    let retreat = if can_move { 1.0 - health } else { -1.0 };
    if retreat > attack {
        NpcAction::Retreat
    } else {
        NpcAction::Attack
    }
}

// Mission profile: base success chance, change per level of difference, lowest and highest chance, and the
// shares of a failure that end as Failed and as Caught. The rest of a failure is Killed.
struct MissionProfile {
    base: f32,
    per_level: f32,
    min: f32,
    max: f32,
    failed: f32,
    caught: f32,
}

const DELIVER: MissionProfile = MissionProfile {
    base: 0.9,
    per_level: 0.03,
    min: 0.1,
    max: 0.99,
    failed: 0.8,
    caught: 0.15,
};

const RAID: MissionProfile = MissionProfile {
    base: 0.5,
    per_level: 0.06,
    min: 0.05,
    max: 0.95,
    failed: 0.35,
    caught: 0.4,
};

/// How a mission ends. The game calls this for a mission that happens off screen. A mission the player
/// joins is played out instead. `noise` is a random number from the game. Returns `None` for an order
/// that is not a mission. The numbers are placeholders, see docs/gtg-npc-behavior.md.
pub fn resolve_mission(
    order: NpcOrder,
    companion_level: u16,
    mission_level: u16,
    noise: u32,
) -> Option<MissionOutcome> {
    let profile = match order {
        NpcOrder::Deliver => DELIVER,
        NpcOrder::Raid => RAID,
        _ => return None,
    };
    let difference = i32::from(companion_level) - i32::from(mission_level);
    let chance =
        (profile.base + profile.per_level * difference as f32).clamp(profile.min, profile.max);
    if roll(noise) < chance {
        return Some(MissionOutcome::Success);
    }
    let split = (noise & 0xFF) as f32 / 256.0;
    Some(if split < profile.failed {
        MissionOutcome::Failed
    } else if split < profile.failed + profile.caught {
        MissionOutcome::Caught
    } else {
        MissionOutcome::Killed
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use gtg_npc_core::MAX_CROWN_HEAT;

    fn obs(role: NpcRole) -> Observation {
        Observation {
            npc_id: 42,
            role,
            backend: DecisionBackend::Utility,
            threat_visible: false,
            health_fraction: 1.0,
            can_move: true,
            disposition: NpcDisposition::Hostile,
            order: NpcOrder::None,
            provoked: false,
            level: 10,
            order_level: 0,
            trustworthiness: 0.5,
            affinity: 0.5,
            pay_satisfaction: 0.5,
            noise: 0,
            betrayal_opportunity: false,
            crown_heat: 0,
            attack_style: AttackStyle::Melee,
            ranged_ready: false,
            ranged_stance: false,
            threat_distance: 0.0,
            favor: 0.0,
        }
    }

    fn act(o: Observation) -> NpcAction {
        decide(o).action
    }

    #[test]
    fn stationary_merchant_never_patrols() {
        let mut o = obs(NpcRole::Merchant);
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Trade);
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Idle);
    }

    #[test]
    fn goblin_patrols_attacks_and_retreats() {
        let mut o = obs(NpcRole::Enemy);
        assert_eq!(act(o), NpcAction::Patrol);
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Attack);
        o.health_fraction = 0.1;
        assert_eq!(act(o), NpcAction::Retreat);
    }

    #[test]
    fn immobile_enemy_cannot_retreat() {
        let mut o = obs(NpcRole::Enemy);
        o.can_move = false;
        o.threat_visible = true;
        o.health_fraction = 0.01;
        assert_eq!(act(o), NpcAction::Attack);
    }

    #[test]
    fn hybrid_has_deterministic_fallback() {
        let mut o = obs(NpcRole::Boss);
        o.backend = DecisionBackend::HybridMl;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Attack);
    }

    #[test]
    fn nonfinite_health_is_safe() {
        let mut o = obs(NpcRole::Enemy);
        o.threat_visible = true;
        o.health_fraction = f32::NAN;
        assert_eq!(act(o), NpcAction::Attack);
    }

    #[test]
    fn id_is_echoed() {
        assert_eq!(decide(obs(NpcRole::Enemy)).npc_id, 42);
    }

    #[test]
    fn hostile_attacks_on_sight_and_is_the_old_behavior() {
        let mut o = obs(NpcRole::Guard);
        o.threat_visible = true;
        assert_eq!(o.disposition, NpcDisposition::Hostile);
        assert_eq!(act(o), NpcAction::Attack);
        o.provoked = true;
        assert_eq!(
            act(o),
            NpcAction::Attack,
            "provoked changes nothing for hostile"
        );
    }

    #[test]
    fn retaliatory_ignores_a_threat_until_attacked() {
        let mut o = obs(NpcRole::Enemy);
        o.disposition = NpcDisposition::Retaliatory;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Patrol);
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Idle);
        o.can_move = true;
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Attack);
        o.health_fraction = 0.1;
        assert_eq!(
            act(o),
            NpcAction::Retreat,
            "a provoked fighter still runs when badly hurt"
        );
        o.threat_visible = false;
        assert_eq!(
            act(o),
            NpcAction::Patrol,
            "no visible threat means no fight"
        );
    }

    #[test]
    fn peaceful_never_fights() {
        let mut o = obs(NpcRole::Enemy);
        o.disposition = NpcDisposition::Peaceful;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Retreat);
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Retreat, "even when attacked");
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Idle, "cannot run, so stands still");
        o.threat_visible = false;
        assert_eq!(act(o), NpcAction::Idle);
    }

    #[test]
    fn a_retaliatory_guard_fights_a_wanted_player_on_sight() {
        let mut o = obs(NpcRole::Guard);
        o.disposition = NpcDisposition::Retaliatory;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Patrol, "no heat: ignored until attacked");
        for tier in 1..=MAX_CROWN_HEAT {
            o.crown_heat = tier;
            assert_eq!(act(o), NpcAction::Attack, "tier {tier}");
        }
        o.crown_heat = 1;
        o.threat_visible = false;
        assert_eq!(act(o), NpcAction::Patrol, "nothing in view means no fight");
        o.threat_visible = true;
        o.health_fraction = 0.1;
        assert_eq!(act(o), NpcAction::Retreat, "a hurt guard still runs");
        o.health_fraction = 1.0;
        o.can_move = false;
        assert_eq!(
            act(o),
            NpcAction::Attack,
            "and one that cannot move holds its ground"
        );
    }

    #[test]
    fn crown_heat_does_not_change_hostile_or_peaceful_guards() {
        for tier in 0..=MAX_CROWN_HEAT {
            let mut hostile = obs(NpcRole::Guard);
            hostile.threat_visible = true;
            hostile.crown_heat = tier;
            assert_eq!(act(hostile), NpcAction::Attack, "hostile, tier {tier}");
            let mut peaceful = hostile;
            peaceful.disposition = NpcDisposition::Peaceful;
            assert_eq!(act(peaceful), NpcAction::Retreat, "peaceful, tier {tier}");
            peaceful.provoked = true;
            assert_eq!(
                act(peaceful),
                NpcAction::Retreat,
                "peaceful and attacked, tier {tier}"
            );
        }
    }

    #[test]
    fn only_guards_read_crown_heat() {
        for role in [NpcRole::Enemy, NpcRole::Boss] {
            let mut o = obs(role);
            o.disposition = NpcDisposition::Retaliatory;
            o.threat_visible = true;
            o.crown_heat = MAX_CROWN_HEAT;
            assert_eq!(act(o), NpcAction::Patrol, "{role:?} ignores Crown Heat");
        }
    }

    // Every combination of the inputs that matter. A tier changes the decision in exactly one case: a
    // guard with a retaliatory disposition that sees a threat and was not attacked. There it gives what
    // the same guard gives once it has been attacked. Everywhere else the decision is the one at tier 0.
    #[test]
    fn crown_heat_changes_one_case_and_nothing_else() {
        let roles = [
            NpcRole::Merchant,
            NpcRole::Civilian,
            NpcRole::Guard,
            NpcRole::Enemy,
            NpcRole::Boss,
            NpcRole::Companion,
        ];
        let dispositions = [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ];
        let orders = [
            NpcOrder::None,
            NpcOrder::Follow,
            NpcOrder::Hold,
            NpcOrder::Attack,
            NpcOrder::Deliver,
            NpcOrder::Raid,
        ];
        let backends = [
            DecisionBackend::StateMachine,
            DecisionBackend::Utility,
            DecisionBackend::HybridMl,
        ];
        let mut changed = 0u32;
        let mut checked = 0u32;
        for role in roles {
            for disposition in dispositions {
                for order in orders {
                    for backend in backends {
                        for bits in 0..16u32 {
                            let mut base = obs(role);
                            base.disposition = disposition;
                            base.order = order;
                            base.backend = backend;
                            base.threat_visible = bits & 1 != 0;
                            base.provoked = bits & 2 != 0;
                            base.can_move = bits & 4 != 0;
                            base.health_fraction = if bits & 8 != 0 { 1.0 } else { 0.3 };
                            let expected = act(base);
                            for tier in 1..=MAX_CROWN_HEAT {
                                let mut hot = base;
                                hot.crown_heat = tier;
                                checked += 1;
                                let the_case = role == NpcRole::Guard
                                    && disposition == NpcDisposition::Retaliatory
                                    && base.threat_visible
                                    && !base.provoked;
                                if the_case {
                                    let mut attacked = base;
                                    attacked.provoked = true;
                                    assert_eq!(act(hot), act(attacked), "{role:?} tier {tier}");
                                    if act(hot) != expected {
                                        changed += 1;
                                    }
                                } else {
                                    assert_eq!(
                                        act(hot),
                                        expected,
                                        "{role:?} {disposition:?} tier {tier}"
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
        assert!(changed > 0, "the one case must actually change something");
        assert!(checked > 5000, "checked {checked}");
    }

    #[test]
    fn disposition_does_not_touch_civilians() {
        for d in [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ] {
            for provoked in [false, true] {
                let mut c = obs(NpcRole::Civilian);
                c.disposition = d;
                c.provoked = provoked;
                assert_eq!(act(c), NpcAction::Patrol);
                c.threat_visible = true;
                assert_eq!(act(c), NpcAction::Retreat);
            }
        }
    }

    #[test]
    fn disposition_only_changes_a_shopkeeper_that_was_attacked() {
        for d in [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ] {
            for backend in [
                DecisionBackend::StateMachine,
                DecisionBackend::Utility,
                DecisionBackend::HybridMl,
            ] {
                for can_move in [true, false] {
                    let mut m = obs(NpcRole::Merchant);
                    m.disposition = d;
                    m.backend = backend;
                    m.can_move = can_move;
                    // Nothing in view: trades, attacked or not.
                    assert_eq!(act(m), NpcAction::Trade);
                    m.provoked = true;
                    assert_eq!(act(m), NpcAction::Trade, "{d:?} attacked, nothing in view");
                    // A threat in view that has not attacked: stands still, whatever the disposition.
                    m.provoked = false;
                    m.threat_visible = true;
                    assert_eq!(act(m), NpcAction::Idle, "{d:?} {backend:?} unprovoked");
                    // Attacked with the threat in view: only the retaliatory one fights.
                    m.provoked = true;
                    let expected = if d == NpcDisposition::Retaliatory {
                        NpcAction::Attack
                    } else {
                        NpcAction::Idle
                    };
                    assert_eq!(act(m), expected, "{d:?} {backend:?} can_move {can_move}");
                }
            }
        }
    }

    #[test]
    fn a_retaliatory_shopkeeper_runs_when_badly_hurt_like_any_fighter() {
        let mut m = obs(NpcRole::Merchant);
        m.disposition = NpcDisposition::Retaliatory;
        m.backend = DecisionBackend::StateMachine;
        m.threat_visible = true;
        m.provoked = true;
        m.health_fraction = 0.19;
        assert_eq!(act(m), NpcAction::Retreat);
        m.health_fraction = 0.2;
        assert_eq!(act(m), NpcAction::Attack);
        // A shopkeeper that cannot leave its stall keeps fighting.
        m.health_fraction = 0.05;
        m.can_move = false;
        assert_eq!(act(m), NpcAction::Attack);
    }

    // A shopkeeper who fights back fights in its own style, so a crossbow behind the counter shoots and a club
    // swings. The retaliatory and provoked gate is the same one the melee case uses.
    #[test]
    fn a_retaliatory_shopkeeper_fights_in_its_own_style() {
        let mut m = obs(NpcRole::Merchant);
        m.disposition = NpcDisposition::Retaliatory;
        m.backend = DecisionBackend::StateMachine;
        m.threat_visible = true;
        m.provoked = true;
        m.ranged_ready = true;
        m.attack_style = AttackStyle::Ranged;
        m.threat_distance = 20.0;
        assert_eq!(act(m), NpcAction::Shoot);
        m.threat_distance = 2.0;
        assert_eq!(act(m), NpcAction::Kite);
        m.can_move = false;
        assert_eq!(act(m), NpcAction::Shoot);
        m.ranged_ready = false;
        assert_eq!(act(m), NpcAction::Idle);
        m.can_move = true;
        assert_eq!(act(m), NpcAction::Retreat);

        let mut h = m;
        h.attack_style = AttackStyle::Hybrid;
        h.ranged_ready = true;
        h.threat_distance = 20.0;
        assert_eq!(act(h), NpcAction::Shoot);
        h.threat_distance = 2.0;
        assert_eq!(act(h), NpcAction::Attack);

        // Without the attack, the style changes nothing.
        for style in [AttackStyle::Ranged, AttackStyle::Hybrid] {
            let mut calm = m;
            calm.attack_style = style;
            calm.provoked = false;
            assert_eq!(act(calm), NpcAction::Idle);
        }
    }

    #[test]
    fn non_companions_ignore_orders() {
        for role in [
            NpcRole::Merchant,
            NpcRole::Civilian,
            NpcRole::Guard,
            NpcRole::Enemy,
        ] {
            let plain = obs(role);
            let mut ordered = plain;
            ordered.order = NpcOrder::Hold;
            ordered.order_level = 99;
            assert_eq!(act(plain), act(ordered), "{role:?}");
        }
    }

    #[test]
    fn companion_without_an_order_follows() {
        let mut o = obs(NpcRole::Companion);
        assert_eq!(act(o), NpcAction::Follow);
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Idle);
    }

    #[test]
    fn follow_defends_but_does_not_start_fights() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Follow;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Follow);
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Attack);
        o.health_fraction = 0.1;
        assert_eq!(act(o), NpcAction::Retreat);
    }

    #[test]
    fn attack_order_engages_on_sight() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Attack;
        assert_eq!(act(o), NpcAction::Follow, "nothing to attack yet");
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Attack);
        o.health_fraction = 0.1;
        assert_eq!(act(o), NpcAction::Retreat);
    }

    #[test]
    fn hold_stays_put_whatever_happens() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Hold;
        assert_eq!(act(o), NpcAction::Hold);
        o.threat_visible = true;
        o.provoked = true;
        o.health_fraction = 0.05;
        assert_eq!(act(o), NpcAction::Hold);
    }

    #[test]
    fn the_gap_depends_on_the_order_type() {
        assert_eq!(refuse_gap(NpcOrder::Raid), 1);
        assert_eq!(refuse_gap(NpcOrder::Attack), 3);
        assert_eq!(refuse_gap(NpcOrder::Deliver), 6);
        assert!(refuse_gap(NpcOrder::Raid) < refuse_gap(NpcOrder::Attack));
        assert!(refuse_gap(NpcOrder::Attack) < refuse_gap(NpcOrder::Deliver));
        for order in [NpcOrder::Raid, NpcOrder::Attack, NpcOrder::Deliver] {
            let gap = refuse_gap(order);
            let mut o = obs(NpcRole::Companion);
            o.level = 10;
            o.order = order;
            o.order_level = 10 + gap;
            assert_ne!(
                act(o),
                NpcAction::RefuseOrder,
                "{order:?} exactly at its gap is accepted"
            );
            o.order_level = 10 + gap + 1;
            assert_eq!(
                act(o),
                NpcAction::RefuseOrder,
                "{order:?} one above its gap is refused"
            );
        }
    }

    #[test]
    fn the_same_level_gap_is_accepted_for_a_delivery_and_refused_for_a_raid() {
        let mut o = obs(NpcRole::Companion);
        o.level = 10;
        o.order_level = 14;
        o.order = NpcOrder::Deliver;
        assert_ne!(act(o), NpcAction::RefuseOrder);
        o.order = NpcOrder::Raid;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.order = NpcOrder::Attack;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.order_level = 13;
        assert_ne!(
            act(o),
            NpcAction::RefuseOrder,
            "an attack order at a gap of 3 is accepted"
        );
    }

    #[test]
    fn every_order_type_can_be_refused_and_none_cannot() {
        for order in [
            NpcOrder::Follow,
            NpcOrder::Hold,
            NpcOrder::Attack,
            NpcOrder::Deliver,
            NpcOrder::Raid,
        ] {
            let mut o = obs(NpcRole::Companion);
            o.level = 1;
            o.order = order;
            o.order_level = 100;
            assert_eq!(act(o), NpcAction::RefuseOrder, "{order:?}");
        }
        let mut o = obs(NpcRole::Companion);
        o.order_level = 100;
        assert_ne!(
            act(o),
            NpcAction::RefuseOrder,
            "no order, nothing to refuse"
        );
    }

    #[test]
    fn refusal_beats_danger_and_does_not_overflow() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Attack;
        o.threat_visible = true;
        o.level = 1;
        o.order_level = 5;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.level = u16::MAX;
        o.order_level = u16::MAX;
        for order in [NpcOrder::Attack, NpcOrder::Deliver, NpcOrder::Raid] {
            o.order = order;
            assert_ne!(
                act(o),
                NpcAction::RefuseOrder,
                "saturating add, no overflow, {order:?}"
            );
        }
    }

    #[test]
    fn a_higher_level_accepts_harder_orders() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Attack;
        o.order_level = 20;
        o.level = 10;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.level = 17;
        assert_ne!(act(o), NpcAction::RefuseOrder);
    }

    #[test]
    fn a_state_machine_fighter_runs_below_a_fifth_of_its_health() {
        for role in [NpcRole::Enemy, NpcRole::Companion] {
            let mut o = obs(role);
            o.backend = DecisionBackend::StateMachine;
            o.threat_visible = true;
            o.provoked = true;
            o.order = if role == NpcRole::Companion {
                NpcOrder::Attack
            } else {
                NpcOrder::None
            };
            o.health_fraction = 0.25;
            assert_eq!(act(o), NpcAction::Attack, "{role:?} at 0.25");
            o.health_fraction = 0.2;
            assert_eq!(act(o), NpcAction::Attack, "{role:?} exactly at 0.2");
            o.health_fraction = 0.19;
            assert_eq!(act(o), NpcAction::Retreat, "{role:?} at 0.19");
            o.can_move = false;
            assert_eq!(act(o), NpcAction::Attack, "{role:?} cannot run");
        }
    }

    #[test]
    fn decisions_are_deterministic() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Attack;
        o.threat_visible = true;
        for _ in 0..100 {
            assert_eq!(act(o), act(o));
        }
    }
}

#[cfg(test)]
mod companion_v4_tests {
    use super::*;

    fn companion() -> Observation {
        Observation {
            npc_id: 9,
            role: NpcRole::Companion,
            backend: DecisionBackend::Utility,
            threat_visible: false,
            health_fraction: 1.0,
            can_move: true,
            disposition: NpcDisposition::Hostile,
            order: NpcOrder::None,
            provoked: false,
            level: 10,
            order_level: 0,
            trustworthiness: 0.0,
            affinity: 0.0,
            pay_satisfaction: 0.0,
            noise: 0,
            betrayal_opportunity: true,
            crown_heat: 0,
            attack_style: AttackStyle::Melee,
            ranged_ready: false,
            ranged_stance: false,
            threat_distance: 0.0,
            favor: 0.0,
        }
    }

    fn act(o: Observation) -> NpcAction {
        decide(o).action
    }

    #[test]
    fn the_weights_add_up_to_one() {
        let sum = LOYALTY_WEIGHT_TRUST + LOYALTY_WEIGHT_AFFINITY + LOYALTY_WEIGHT_PAY;
        assert!((sum - 1.0).abs() < 1e-6);
    }

    #[test]
    fn loyalty_is_a_weighted_mix() {
        let mut o = companion();
        assert_eq!(loyalty(&o), 0.0);
        o.trustworthiness = 1.0;
        assert!(
            (loyalty(&o) - LOYALTY_WEIGHT_TRUST).abs() < 1e-6,
            "trust alone"
        );
        o.trustworthiness = 0.0;
        o.affinity = 1.0;
        assert!(
            (loyalty(&o) - LOYALTY_WEIGHT_AFFINITY).abs() < 1e-6,
            "liking alone"
        );
        o.affinity = 0.0;
        o.pay_satisfaction = 1.0;
        assert!((loyalty(&o) - LOYALTY_WEIGHT_PAY).abs() < 1e-6, "pay alone");
        o.trustworthiness = 1.0;
        o.affinity = 1.0;
        assert!((loyalty(&o) - 1.0).abs() < 1e-6, "everything");
        o.trustworthiness = 5.0;
        o.affinity = -3.0;
        o.pay_satisfaction = f32::NAN;
        assert!(
            loyalty(&o).is_finite() && (0.0..=1.0).contains(&loyalty(&o)),
            "bad input stays in range"
        );
    }

    #[test]
    fn the_chance_falls_linearly_to_zero_at_the_ceiling() {
        assert!((betrayal_chance(0.0) - BETRAY_MAX_CHANCE).abs() < 1e-6);
        assert!(
            (betrayal_chance(BETRAY_LOYALTY_CEILING / 2.0) - BETRAY_MAX_CHANCE / 2.0).abs() < 1e-6
        );
        assert_eq!(betrayal_chance(BETRAY_LOYALTY_CEILING), 0.0);
        assert_eq!(betrayal_chance(0.9), 0.0);
        assert_eq!(betrayal_chance(1.0), 0.0);
        let mut last = f32::MAX;
        for step in 0..=20 {
            let chance = betrayal_chance(step as f32 / 20.0);
            assert!(chance <= last, "never rises with loyalty");
            last = chance;
        }
    }

    #[test]
    fn no_opportunity_means_no_betrayal() {
        let mut o = companion();
        o.betrayal_opportunity = false;
        o.noise = 0;
        assert_ne!(
            act(o),
            NpcAction::Betray,
            "even a disloyal companion with the luckiest roll"
        );
    }

    #[test]
    fn a_loyal_companion_never_betrays() {
        let mut o = companion();
        o.trustworthiness = 1.0;
        o.affinity = 1.0;
        o.pay_satisfaction = 1.0;
        for noise in [0, 1, u32::MAX / 2, u32::MAX] {
            o.noise = noise;
            assert_ne!(act(o), NpcAction::Betray);
        }
        o.trustworthiness = 0.5;
        o.affinity = 0.5;
        o.pay_satisfaction = 0.5;
        o.noise = 0;
        assert_ne!(
            act(o),
            NpcAction::Betray,
            "loyalty exactly at the ceiling never betrays"
        );
    }

    #[test]
    fn a_disloyal_companion_betrays_below_the_chance_and_not_above() {
        let mut o = companion();
        o.noise = 0;
        assert_eq!(act(o), NpcAction::Betray, "the lowest roll");
        // The roll is noise >> 8 over 2^24. A chance of 0.5 puts the line at the middle.
        o.noise = (0x7F_FFFF << 8) | 0xFF;
        assert_eq!(act(o), NpcAction::Betray, "just below the line");
        o.noise = 0x80_0000 << 8;
        assert_ne!(
            act(o),
            NpcAction::Betray,
            "exactly on the line is not below it"
        );
        o.noise = u32::MAX;
        assert_ne!(act(o), NpcAction::Betray, "the highest roll");
    }

    #[test]
    fn the_roll_uses_the_top_bits_only() {
        let mut o = companion();
        o.noise = 0xFF;
        assert_eq!(act(o), NpcAction::Betray, "low bits do not matter");
        o.noise = 0xFFFF_FF00;
        assert_ne!(act(o), NpcAction::Betray);
    }

    #[test]
    fn more_loyalty_means_fewer_betrayals() {
        let count = |trust: f32| {
            let mut o = companion();
            o.trustworthiness = trust;
            (0..4096u32)
                .filter(|i| {
                    o.noise = i << 20;
                    act(o) == NpcAction::Betray
                })
                .count()
        };
        let low = count(0.0);
        let mid = count(0.6);
        let high = count(1.0);
        assert!(low > mid && mid > high, "{low} {mid} {high}");
        assert!(
            high > 0,
            "trust alone gives loyalty 0.4, still below the ceiling"
        );
    }

    #[test]
    fn betrayal_beats_orders_and_refusals() {
        let mut o = companion();
        o.order = NpcOrder::Hold;
        assert_eq!(act(o), NpcAction::Betray);
        o.order = NpcOrder::Raid;
        o.order_level = 99;
        assert_eq!(
            act(o),
            NpcAction::Betray,
            "a betraying companion does not bother to refuse"
        );
        o.threat_visible = true;
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Betray);
    }

    #[test]
    fn only_companions_betray() {
        for role in [
            NpcRole::Merchant,
            NpcRole::Civilian,
            NpcRole::Guard,
            NpcRole::Enemy,
            NpcRole::Boss,
        ] {
            let mut o = companion();
            o.role = role;
            assert_ne!(act(o), NpcAction::Betray, "{role:?}");
        }
    }

    #[test]
    fn missions_are_carried_out_when_accepted() {
        let mut o = companion();
        o.betrayal_opportunity = false;
        o.order = NpcOrder::Deliver;
        o.order_level = 5;
        assert_eq!(act(o), NpcAction::Mission);
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Idle);
        o.can_move = true;
        o.order = NpcOrder::Raid;
        o.order_level = 10;
        assert_eq!(act(o), NpcAction::Mission);
    }

    #[test]
    fn a_raid_fights_on_sight_and_a_delivery_only_when_provoked() {
        let mut o = companion();
        o.betrayal_opportunity = false;
        o.order = NpcOrder::Raid;
        o.order_level = 10;
        o.threat_visible = true;
        assert_eq!(act(o), NpcAction::Attack);
        o.health_fraction = 0.1;
        assert_eq!(act(o), NpcAction::Retreat);
        o.health_fraction = 1.0;
        o.order = NpcOrder::Deliver;
        o.order_level = 5;
        assert_eq!(
            act(o),
            NpcAction::Mission,
            "a courier ignores a threat that has not attacked"
        );
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Attack);
    }

    #[test]
    fn delivery_and_raid_resolution_ignore_other_orders() {
        for order in [
            NpcOrder::None,
            NpcOrder::Follow,
            NpcOrder::Hold,
            NpcOrder::Attack,
        ] {
            assert_eq!(resolve_mission(order, 10, 10, 0), None, "{order:?}");
        }
        assert!(resolve_mission(NpcOrder::Deliver, 10, 10, 0).is_some());
        assert!(resolve_mission(NpcOrder::Raid, 10, 10, 0).is_some());
    }

    #[test]
    fn mission_outcomes_follow_the_rolls() {
        // A raid at equal levels: success chance 0.5. The roll is noise >> 8 over 2^24.
        let raid = |noise| resolve_mission(NpcOrder::Raid, 10, 10, noise).unwrap();
        assert_eq!(raid(0x0000_0000), MissionOutcome::Success);
        assert_eq!(
            raid(0x7F_FFFF << 8),
            MissionOutcome::Success,
            "just below the line"
        );
        // At or above the line the low byte splits the failure: failed below 0.35, caught below 0.75.
        assert_eq!(raid(0x80_0000 << 8), MissionOutcome::Failed, "low byte 0");
        assert_eq!(
            raid((0x80_0000 << 8) | 89),
            MissionOutcome::Failed,
            "89 / 256 is below 0.35"
        );
        assert_eq!(
            raid((0x80_0000 << 8) | 90),
            MissionOutcome::Caught,
            "90 / 256 is not below 0.35"
        );
        assert_eq!(
            raid((0x80_0000 << 8) | 191),
            MissionOutcome::Caught,
            "191 / 256 is below 0.75"
        );
        assert_eq!(
            raid((0x80_0000 << 8) | 192),
            MissionOutcome::Killed,
            "192 / 256 is not below 0.75"
        );
        assert_eq!(raid(u32::MAX), MissionOutcome::Killed);
    }

    #[test]
    fn a_stronger_companion_does_better_and_a_weaker_one_worse() {
        let successes = |order: NpcOrder, companion: u16, mission: u16| {
            (0..8192u32)
                .filter(|i| {
                    resolve_mission(order, companion, mission, i << 19)
                        == Some(MissionOutcome::Success)
                })
                .count()
        };
        for order in [NpcOrder::Raid, NpcOrder::Deliver] {
            let weak = successes(order, 5, 20);
            let even = successes(order, 20, 20);
            let strong = successes(order, 35, 20);
            assert!(
                weak < even && even < strong,
                "{order:?}: {weak} {even} {strong}"
            );
        }
    }

    #[test]
    fn the_success_chance_is_clamped() {
        let rate = |order, companion, mission| {
            (0..8192u32)
                .filter(|i| {
                    resolve_mission(order, companion, mission, i << 19)
                        == Some(MissionOutcome::Success)
                })
                .count() as f32
                / 8192.0
        };
        assert!(
            (rate(NpcOrder::Raid, 0, 1000) - 0.05).abs() < 0.01,
            "lowest raid chance"
        );
        assert!(
            (rate(NpcOrder::Raid, 1000, 0) - 0.95).abs() < 0.01,
            "highest raid chance"
        );
        assert!(
            (rate(NpcOrder::Deliver, 0, 1000) - 0.1).abs() < 0.01,
            "lowest delivery chance"
        );
        assert!(
            (rate(NpcOrder::Deliver, 1000, 0) - 0.99).abs() < 0.01,
            "highest delivery chance"
        );
        assert!(
            (rate(NpcOrder::Raid, 10, 10) - 0.5).abs() < 0.01,
            "even raid"
        );
        assert!(
            (rate(NpcOrder::Deliver, 10, 10) - 0.9).abs() < 0.01,
            "even delivery"
        );
    }

    #[test]
    fn a_raid_is_deadlier_than_a_delivery() {
        let share = |order, wanted| {
            let failures: Vec<_> = (0..65536u32)
                .map(|i| resolve_mission(order, 10, 10, (i << 16) | (i & 0xFF)).unwrap())
                .filter(|o| *o != MissionOutcome::Success)
                .collect();
            failures.iter().filter(|o| **o == wanted).count() as f32 / failures.len() as f32
        };
        assert!(
            share(NpcOrder::Raid, MissionOutcome::Killed)
                > share(NpcOrder::Deliver, MissionOutcome::Killed)
        );
        assert!(
            share(NpcOrder::Raid, MissionOutcome::Caught)
                > share(NpcOrder::Deliver, MissionOutcome::Caught)
        );
        assert!(
            share(NpcOrder::Deliver, MissionOutcome::Failed)
                > share(NpcOrder::Raid, MissionOutcome::Failed)
        );
    }

    #[test]
    fn resolution_is_deterministic() {
        for noise in [0, 1, 12345, u32::MAX] {
            assert_eq!(
                resolve_mission(NpcOrder::Raid, 7, 9, noise),
                resolve_mission(NpcOrder::Raid, 7, 9, noise)
            );
        }
    }
}

#[cfg(test)]
mod attack_style_tests {
    use super::*;

    fn fighter(style: AttackStyle) -> Observation {
        Observation {
            npc_id: 31,
            role: NpcRole::Enemy,
            backend: DecisionBackend::StateMachine,
            threat_visible: true,
            health_fraction: 1.0,
            can_move: true,
            disposition: NpcDisposition::Hostile,
            order: NpcOrder::None,
            provoked: false,
            level: 10,
            order_level: 0,
            trustworthiness: 0.5,
            affinity: 0.5,
            pay_satisfaction: 0.5,
            noise: 0,
            betrayal_opportunity: false,
            crown_heat: 0,
            attack_style: style,
            ranged_ready: true,
            ranged_stance: false,
            threat_distance: 0.0,
            favor: 0.0,
        }
    }

    fn at(style: AttackStyle, distance: f32) -> Observation {
        Observation {
            threat_distance: distance,
            ..fighter(style)
        }
    }

    fn act(o: Observation) -> NpcAction {
        decide(o).action
    }

    #[test]
    fn the_distances_are_in_order() {
        const {
            assert!(KITE_DISTANCE > 0.0);
            assert!(HYBRID_MELEE_DISTANCE > 0.0);
            assert!(HYBRID_MELEE_DISTANCE < HYBRID_RANGED_DISTANCE);
        }
    }

    #[test]
    fn a_melee_fighter_ignores_every_new_field() {
        let roles = [
            NpcRole::Merchant,
            NpcRole::Civilian,
            NpcRole::Guard,
            NpcRole::Enemy,
            NpcRole::Boss,
            NpcRole::Companion,
        ];
        let dispositions = [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ];
        let orders = [
            NpcOrder::None,
            NpcOrder::Follow,
            NpcOrder::Hold,
            NpcOrder::Attack,
            NpcOrder::Deliver,
            NpcOrder::Raid,
        ];
        let backends = [
            DecisionBackend::StateMachine,
            DecisionBackend::Utility,
            DecisionBackend::HybridMl,
        ];
        let distances = [0.0, 1.0, 5.99, 6.0, 20.0, -3.0, f32::NAN, f32::INFINITY];
        let mut checked = 0u32;
        for role in roles {
            for disposition in dispositions {
                for order in orders {
                    for backend in backends {
                        for bits in 0..16u32 {
                            let mut base = fighter(AttackStyle::Melee);
                            base.role = role;
                            base.disposition = disposition;
                            base.order = order;
                            base.backend = backend;
                            base.ranged_ready = false;
                            base.threat_visible = bits & 1 != 0;
                            base.provoked = bits & 2 != 0;
                            base.can_move = bits & 4 != 0;
                            base.health_fraction = if bits & 8 != 0 { 1.0 } else { 0.3 };
                            let expected = act(base);
                            assert!(
                                !matches!(expected, NpcAction::Shoot | NpcAction::Kite),
                                "melee never shoots"
                            );
                            for distance in distances {
                                for (ready, stance) in
                                    [(false, false), (true, false), (true, true), (false, true)]
                                {
                                    let mut o = base;
                                    o.threat_distance = distance;
                                    o.ranged_ready = ready;
                                    o.ranged_stance = stance;
                                    checked += 1;
                                    assert_eq!(
                                        act(o),
                                        expected,
                                        "{role:?} {disposition:?} distance {distance} ready {ready} stance {stance}"
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }
        assert!(checked > 100_000, "checked {checked}");
    }

    #[test]
    fn merchants_and_civilians_do_not_read_the_style() {
        for role in [NpcRole::Merchant, NpcRole::Civilian] {
            for style in [AttackStyle::Ranged, AttackStyle::Hybrid] {
                for can_move in [true, false] {
                    for threat in [true, false] {
                        let mut melee = fighter(AttackStyle::Melee);
                        melee.role = role;
                        melee.can_move = can_move;
                        melee.threat_visible = threat;
                        let mut other = melee;
                        other.attack_style = style;
                        other.threat_distance = 2.0;
                        assert_eq!(act(other), act(melee), "{role:?} {style:?}");
                    }
                }
            }
        }
    }

    #[test]
    fn a_ranged_fighter_shoots_from_far_and_backs_away_when_close() {
        assert_eq!(act(at(AttackStyle::Ranged, 20.0)), NpcAction::Shoot);
        assert_eq!(act(at(AttackStyle::Ranged, 6.01)), NpcAction::Shoot);
        assert_eq!(act(at(AttackStyle::Ranged, 5.99)), NpcAction::Kite);
        assert_eq!(act(at(AttackStyle::Ranged, 0.5)), NpcAction::Kite);
    }

    #[test]
    fn the_kite_line_is_exact() {
        let just_inside = f32::from_bits(KITE_DISTANCE.to_bits() - 1);
        assert_eq!(
            act(at(AttackStyle::Ranged, KITE_DISTANCE)),
            NpcAction::Shoot
        );
        assert_eq!(act(at(AttackStyle::Ranged, just_inside)), NpcAction::Kite);
    }

    #[test]
    fn an_unknown_distance_never_makes_a_ranged_fighter_kite() {
        for distance in [0.0, -1.0, f32::NAN, f32::NEG_INFINITY] {
            assert_eq!(
                act(at(AttackStyle::Ranged, distance)),
                NpcAction::Shoot,
                "{distance}"
            );
        }
    }

    #[test]
    fn a_ranged_fighter_that_cannot_move_shoots_whatever_the_distance() {
        for distance in [0.5, 3.0, 20.0] {
            let mut o = at(AttackStyle::Ranged, distance);
            o.can_move = false;
            assert_eq!(act(o), NpcAction::Shoot, "{distance}");
        }
    }

    #[test]
    fn a_ranged_fighter_with_nothing_to_shoot_runs_or_stands() {
        for distance in [0.0, 2.0, 20.0] {
            let mut o = at(AttackStyle::Ranged, distance);
            o.ranged_ready = false;
            assert_eq!(act(o), NpcAction::Retreat, "{distance}");
            o.can_move = false;
            assert_eq!(act(o), NpcAction::Idle, "{distance}");
        }
    }

    #[test]
    fn a_hurt_ranged_fighter_still_runs() {
        let mut o = at(AttackStyle::Ranged, 20.0);
        o.health_fraction = 0.19;
        assert_eq!(act(o), NpcAction::Retreat);
        o.health_fraction = 0.2;
        assert_eq!(act(o), NpcAction::Shoot);
        o.health_fraction = 0.19;
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Shoot, "cornered, so it shoots");
    }

    #[test]
    fn a_hybrid_fights_by_hand_close_up_and_shoots_far_away() {
        assert_eq!(act(at(AttackStyle::Hybrid, 1.0)), NpcAction::Attack);
        assert_eq!(act(at(AttackStyle::Hybrid, 4.0)), NpcAction::Attack);
        assert_eq!(act(at(AttackStyle::Hybrid, 8.0)), NpcAction::Shoot);
        assert_eq!(act(at(AttackStyle::Hybrid, 30.0)), NpcAction::Shoot);
    }

    #[test]
    fn a_hybrid_in_the_buffer_keeps_its_stance() {
        for distance in [4.01, 6.0, 7.99] {
            let mut o = at(AttackStyle::Hybrid, distance);
            assert_eq!(act(o), NpcAction::Attack, "by hand at {distance}");
            o.ranged_stance = true;
            assert_eq!(act(o), NpcAction::Shoot, "shooting at {distance}");
        }
    }

    #[test]
    fn a_hybrid_switches_when_the_threat_leaves_the_buffer() {
        let mut far = at(AttackStyle::Hybrid, 30.0);
        far.ranged_stance = false;
        assert_eq!(act(far), NpcAction::Shoot, "by hand, threat far away");
        let mut near = at(AttackStyle::Hybrid, 1.0);
        near.ranged_stance = true;
        assert_eq!(act(near), NpcAction::Attack, "shooting, threat in its face");
    }

    #[test]
    fn the_hybrid_lines_are_exact() {
        let mut o = at(AttackStyle::Hybrid, HYBRID_MELEE_DISTANCE);
        o.ranged_stance = true;
        assert_eq!(act(o), NpcAction::Attack, "at the near line");
        o.threat_distance = f32::from_bits(HYBRID_MELEE_DISTANCE.to_bits() + 1);
        assert_eq!(
            act(o),
            NpcAction::Shoot,
            "just past the near line keeps the stance"
        );
        let mut o = at(AttackStyle::Hybrid, HYBRID_RANGED_DISTANCE);
        o.ranged_stance = false;
        assert_eq!(act(o), NpcAction::Shoot, "at the far line");
        o.threat_distance = f32::from_bits(HYBRID_RANGED_DISTANCE.to_bits() - 1);
        assert_eq!(
            act(o),
            NpcAction::Attack,
            "just inside the far line keeps the stance"
        );
    }

    #[test]
    fn a_hybrid_with_an_unknown_distance_keeps_its_stance() {
        for distance in [0.0, -2.0, f32::NAN] {
            let mut o = at(AttackStyle::Hybrid, distance);
            assert_eq!(act(o), NpcAction::Attack, "{distance}");
            o.ranged_stance = true;
            assert_eq!(act(o), NpcAction::Shoot, "{distance}");
        }
    }

    #[test]
    fn a_hybrid_with_nothing_to_shoot_fights_by_hand() {
        for distance in [0.0, 2.0, 30.0] {
            let mut o = at(AttackStyle::Hybrid, distance);
            o.ranged_ready = false;
            o.ranged_stance = true;
            assert_eq!(act(o), NpcAction::Attack, "{distance}");
        }
    }

    #[test]
    fn a_hybrid_never_backs_away_but_runs_when_hurt() {
        for distance in [0.0, 1.0, 5.0, 7.0, 12.0] {
            for stance in [false, true] {
                let mut o = at(AttackStyle::Hybrid, distance);
                o.ranged_stance = stance;
                assert_ne!(act(o), NpcAction::Kite, "{distance} {stance}");
                o.health_fraction = 0.1;
                assert_eq!(act(o), NpcAction::Retreat, "{distance} {stance} hurt");
            }
        }
    }

    #[test]
    fn only_a_ranged_fighter_backs_away_and_only_ranged_styles_shoot() {
        let styles = [AttackStyle::Melee, AttackStyle::Ranged, AttackStyle::Hybrid];
        let backends = [
            DecisionBackend::StateMachine,
            DecisionBackend::Utility,
            DecisionBackend::HybridMl,
        ];
        for style in styles {
            for backend in backends {
                for distance in [0.0, 1.0, 5.0, 7.0, 9.0, 40.0] {
                    for (ready, stance) in [(false, false), (true, false), (true, true)] {
                        for health in [1.0, 0.5, 0.1] {
                            let mut o = at(style, distance);
                            o.backend = backend;
                            o.ranged_ready = ready;
                            o.ranged_stance = stance;
                            o.health_fraction = health;
                            let action = act(o);
                            if action == NpcAction::Kite {
                                assert_eq!(style, AttackStyle::Ranged);
                            }
                            if action == NpcAction::Shoot {
                                assert_ne!(style, AttackStyle::Melee);
                            }
                        }
                    }
                }
            }
        }
    }

    #[test]
    fn the_scored_backends_pick_the_style_after_choosing_to_attack() {
        for backend in [DecisionBackend::Utility, DecisionBackend::HybridMl] {
            let mut o = at(AttackStyle::Ranged, 20.0);
            o.backend = backend;
            assert_eq!(act(o), NpcAction::Shoot, "{backend:?} far");
            o.threat_distance = 3.0;
            assert_eq!(act(o), NpcAction::Kite, "{backend:?} close");
            o.health_fraction = 0.1;
            assert_eq!(act(o), NpcAction::Retreat, "{backend:?} hurt");
        }
    }

    #[test]
    fn a_ranged_companion_shoots_on_an_attack_order_and_follows_otherwise() {
        let mut o = at(AttackStyle::Ranged, 20.0);
        o.role = NpcRole::Companion;
        o.order = NpcOrder::Attack;
        assert_eq!(act(o), NpcAction::Shoot);
        o.threat_distance = 2.0;
        assert_eq!(act(o), NpcAction::Kite);
        o.order = NpcOrder::Follow;
        assert_eq!(
            act(o),
            NpcAction::Follow,
            "follow defends only after an attack"
        );
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Kite);
        o.order = NpcOrder::Hold;
        assert_eq!(act(o), NpcAction::Hold);
    }

    #[test]
    fn a_ranged_guard_follows_the_disposition_and_crown_heat_rules() {
        let mut guard = at(AttackStyle::Ranged, 20.0);
        guard.role = NpcRole::Guard;
        guard.disposition = NpcDisposition::Retaliatory;
        assert_eq!(
            act(guard),
            NpcAction::Patrol,
            "ignores an unprovoked threat"
        );
        guard.crown_heat = 1;
        assert_eq!(
            act(guard),
            NpcAction::Shoot,
            "wanted, so it fights from range"
        );
        guard.crown_heat = 0;
        guard.provoked = true;
        assert_eq!(
            act(guard),
            NpcAction::Shoot,
            "attacked, so it fights from range"
        );
        guard.disposition = NpcDisposition::Peaceful;
        assert_eq!(act(guard), NpcAction::Retreat, "peaceful never fights");
    }

    #[test]
    fn no_threat_means_no_attack_whatever_the_style() {
        for style in [AttackStyle::Ranged, AttackStyle::Hybrid] {
            let mut o = at(style, 20.0);
            o.threat_visible = false;
            assert_eq!(act(o), NpcAction::Patrol);
            o.can_move = false;
            assert_eq!(act(o), NpcAction::Idle);
        }
    }

    #[test]
    fn decisions_stay_deterministic() {
        let mut o = at(AttackStyle::Hybrid, 6.0);
        o.ranged_stance = true;
        for _ in 0..100 {
            assert_eq!(act(o), act(o));
        }
    }
}

// How favor changes a shopkeeper and a civilian. A fighter and a companion never read it.
#[cfg(test)]
mod favor_tests {
    use super::*;

    fn obs(role: NpcRole) -> Observation {
        Observation {
            npc_id: 77,
            role,
            backend: DecisionBackend::Utility,
            threat_visible: false,
            health_fraction: 1.0,
            can_move: true,
            disposition: NpcDisposition::Hostile,
            order: NpcOrder::None,
            provoked: false,
            level: 10,
            order_level: 0,
            trustworthiness: 0.5,
            affinity: 0.5,
            pay_satisfaction: 0.5,
            noise: 0,
            betrayal_opportunity: false,
            crown_heat: 0,
            attack_style: AttackStyle::Melee,
            ranged_ready: false,
            ranged_stance: false,
            threat_distance: 0.0,
            favor: 0.0,
        }
    }

    fn act(o: Observation) -> NpcAction {
        decide(o).action
    }

    fn watched(role: NpcRole) -> Observation {
        let mut o = obs(role);
        o.threat_visible = true;
        o
    }

    #[test]
    fn the_friendly_line_is_inside_the_range_of_favor() {
        const {
            assert!(FRIENDLY_FAVOR > 0.0 && FRIENDLY_FAVOR < 1.0);
        }
    }

    #[test]
    fn a_shopkeeper_that_likes_the_player_keeps_trading_under_a_visible_threat() {
        let mut o = watched(NpcRole::Merchant);
        assert_eq!(act(o), NpcAction::Idle, "neutral: stands still, as before");
        o.favor = FRIENDLY_FAVOR;
        assert_eq!(
            act(o),
            NpcAction::Trade,
            "exactly at the line counts as liking"
        );
        o.favor = 1.0;
        assert_eq!(act(o), NpcAction::Trade, "adoring");
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Trade, "a stall keeper too");
    }

    #[test]
    fn the_friendly_line_is_exact() {
        let mut o = watched(NpcRole::Merchant);
        o.favor = f32::from_bits(FRIENDLY_FAVOR.to_bits() - 1);
        assert_eq!(act(o), NpcAction::Idle, "one step under the line");
        o.favor = FRIENDLY_FAVOR;
        assert_eq!(act(o), NpcAction::Trade, "on the line");
        let mut c = watched(NpcRole::Civilian);
        c.favor = f32::from_bits(FRIENDLY_FAVOR.to_bits() - 1);
        assert_eq!(
            act(c),
            NpcAction::Retreat,
            "a civilian one step under the line"
        );
        c.favor = FRIENDLY_FAVOR;
        assert_eq!(act(c), NpcAction::Patrol, "a civilian on the line");
    }

    #[test]
    fn a_shopkeeper_that_dislikes_the_player_is_no_different_from_a_neutral_one() {
        let mut o = watched(NpcRole::Merchant);
        for favor in [0.0, -0.1, -0.5, -1.0] {
            o.favor = favor;
            assert_eq!(act(o), NpcAction::Idle, "favor {favor}");
        }
    }

    #[test]
    fn a_shopkeeper_the_player_just_attacked_does_not_stay_friendly() {
        let mut o = watched(NpcRole::Merchant);
        o.favor = 1.0;
        o.provoked = true;
        for disposition in [NpcDisposition::Hostile, NpcDisposition::Peaceful] {
            o.disposition = disposition;
            assert_eq!(
                act(o),
                NpcAction::Idle,
                "{disposition:?} does not trade after a blow"
            );
        }
        o.disposition = NpcDisposition::Retaliatory;
        assert_eq!(
            act(o),
            NpcAction::Attack,
            "a retaliatory shopkeeper still fights back however much it liked the player"
        );
    }

    #[test]
    fn a_friendly_shopkeeper_with_nothing_in_view_trades_as_always() {
        let mut o = obs(NpcRole::Merchant);
        for favor in [-1.0, 0.0, 1.0] {
            o.favor = favor;
            assert_eq!(act(o), NpcAction::Trade, "favor {favor}");
        }
    }

    #[test]
    fn a_civilian_that_likes_the_player_does_not_run() {
        let mut o = watched(NpcRole::Civilian);
        assert_eq!(act(o), NpcAction::Retreat, "neutral: runs, as before");
        o.favor = 0.5;
        assert_eq!(act(o), NpcAction::Patrol, "liked: goes about its day");
        o.can_move = false;
        assert_eq!(
            act(o),
            NpcAction::Idle,
            "a civilian that cannot move stays put"
        );
        o.can_move = true;
        o.favor = -1.0;
        assert_eq!(act(o), NpcAction::Retreat, "disliked: runs");
    }

    #[test]
    fn a_civilian_the_player_just_attacked_runs_whatever_it_felt() {
        let mut o = watched(NpcRole::Civilian);
        o.favor = 1.0;
        o.provoked = true;
        assert_eq!(act(o), NpcAction::Retreat);
        o.can_move = false;
        assert_eq!(act(o), NpcAction::Idle, "and stands when it cannot move");
    }

    #[test]
    fn a_number_that_is_not_finite_is_neutral() {
        for favor in [f32::NAN, f32::INFINITY, f32::NEG_INFINITY] {
            let mut m = watched(NpcRole::Merchant);
            m.favor = favor;
            assert_eq!(act(m), NpcAction::Idle, "merchant, favor {favor}");
            let mut c = watched(NpcRole::Civilian);
            c.favor = favor;
            assert_eq!(act(c), NpcAction::Retreat, "civilian, favor {favor}");
        }
    }

    #[test]
    fn fighters_and_companions_do_not_read_favor() {
        let dispositions = [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ];
        let orders = [
            NpcOrder::None,
            NpcOrder::Follow,
            NpcOrder::Hold,
            NpcOrder::Attack,
            NpcOrder::Deliver,
            NpcOrder::Raid,
        ];
        for role in [
            NpcRole::Guard,
            NpcRole::Enemy,
            NpcRole::Boss,
            NpcRole::Companion,
        ] {
            for disposition in dispositions {
                for order in orders {
                    for (visible, provoked, can_move) in [
                        (false, false, true),
                        (true, false, true),
                        (true, true, true),
                        (true, true, false),
                    ] {
                        let mut o = obs(role);
                        o.disposition = disposition;
                        o.order = order;
                        o.threat_visible = visible;
                        o.provoked = provoked;
                        o.can_move = can_move;
                        o.crown_heat = 1;
                        let baseline = act(o);
                        for favor in [-1.0, 0.3, 1.0] {
                            o.favor = favor;
                            assert_eq!(
                                act(o),
                                baseline,
                                "{role:?} {disposition:?} {order:?} visible {visible} provoked {provoked}, favor {favor}"
                            );
                        }
                    }
                }
            }
        }
    }
}
