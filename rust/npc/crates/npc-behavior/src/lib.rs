// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-behavior.md, section "lib.rs"
// ============================================================================

//! Deterministic baseline: no allocation and no ML required per decision.
use gtg_npc_core::{
    Decision, DecisionBackend, NpcAction, NpcDisposition, NpcOrder, NpcRole, Observation,
};

/// How many levels above its own a companion accepts an order. A higher order level is refused.
/// Placeholder value, see docs/gtg-npc-behavior.md.
pub const REFUSE_LEVEL_GAP: u16 = 5;

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

// Companions follow orders unless the order is too hard for their level.
fn companion(o: &Observation, health: f32) -> NpcAction {
    if o.order != NpcOrder::None && o.order_level > o.level.saturating_add(REFUSE_LEVEL_GAP) {
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
    fn an_order_far_above_the_level_is_refused() {
        let mut o = obs(NpcRole::Companion);
        o.level = 10;
        o.order = NpcOrder::Attack;
        o.order_level = 10 + REFUSE_LEVEL_GAP;
        assert_ne!(
            act(o),
            NpcAction::RefuseOrder,
            "exactly at the gap is accepted"
        );
        o.order_level = 10 + REFUSE_LEVEL_GAP + 1;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.order = NpcOrder::Hold;
        assert_eq!(
            act(o),
            NpcAction::RefuseOrder,
            "every order type can be refused"
        );
        o.order = NpcOrder::Follow;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.order = NpcOrder::None;
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
        o.order_level = 7;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.level = u16::MAX;
        o.order_level = u16::MAX;
        assert_ne!(
            act(o),
            NpcAction::RefuseOrder,
            "saturating add, no overflow at the top level"
        );
    }

    #[test]
    fn a_higher_level_accepts_harder_orders() {
        let mut o = obs(NpcRole::Companion);
        o.order = NpcOrder::Attack;
        o.order_level = 20;
        o.level = 10;
        assert_eq!(act(o), NpcAction::RefuseOrder);
        o.level = 15;
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
