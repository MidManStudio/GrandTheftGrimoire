// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-behavior.md, section "lib.rs"
// ============================================================================

//! Deterministic baseline: no allocation and no ML required per decision.
use gtg_npc_core::{
    Decision, DecisionBackend, MissionOutcome, NpcAction, NpcDisposition, NpcOrder, NpcRole,
    Observation,
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

/// Chooses what one NPC does next. Pure: the same observation always gives the same decision.
pub fn decide(o: Observation) -> Decision {
    let health = if o.health_fraction.is_finite() {
        o.health_fraction.clamp(0.0, 1.0)
    } else {
        1.0
    };
    let action = match o.role {
        NpcRole::Merchant => {
            if o.threat_visible {
                NpcAction::Idle
            } else {
                NpcAction::Trade
            }
        }
        NpcRole::Civilian => {
            if o.threat_visible && o.can_move {
                NpcAction::Retreat
            } else {
                wander(o.can_move)
            }
        }
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

// Guards, enemies and bosses. Disposition decides whether a visible threat is fought at all.
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
        NpcDisposition::Retaliatory if !o.provoked => wander(o.can_move),
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

// Fighting a threat: run when badly hurt, otherwise attack.
fn fight(o: &Observation, health: f32) -> NpcAction {
    if health < 0.2 && o.can_move {
        NpcAction::Retreat
    } else if matches!(o.backend, DecisionBackend::StateMachine) {
        NpcAction::Attack
    } else {
        utility_combat(health, o.can_move)
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
    fn disposition_does_not_touch_merchants_or_civilians() {
        for d in [
            NpcDisposition::Hostile,
            NpcDisposition::Retaliatory,
            NpcDisposition::Peaceful,
        ] {
            let mut m = obs(NpcRole::Merchant);
            m.disposition = d;
            assert_eq!(act(m), NpcAction::Trade);
            let mut c = obs(NpcRole::Civilian);
            c.disposition = d;
            assert_eq!(act(c), NpcAction::Patrol);
            c.threat_visible = true;
            assert_eq!(act(c), NpcAction::Retreat);
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
