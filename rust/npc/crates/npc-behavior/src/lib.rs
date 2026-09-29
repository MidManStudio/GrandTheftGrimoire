//! Deterministic baseline: no allocation and no ML required per decision.
use gtg_npc_core::{Decision, DecisionBackend, NpcAction, NpcRole, Observation};

pub fn decide(o: Observation) -> Decision {
    let health = if o.health_fraction.is_finite() { o.health_fraction.clamp(0.0, 1.0) } else { 1.0 };
    let action = match o.role {
        NpcRole::Merchant => if o.threat_visible { NpcAction::Idle } else { NpcAction::Trade },
        NpcRole::Civilian => if o.threat_visible && o.can_move { NpcAction::Retreat }
            else if o.can_move { NpcAction::Patrol } else { NpcAction::Idle },
        NpcRole::Guard | NpcRole::Enemy | NpcRole::Boss => {
            if !o.threat_visible { if o.can_move { NpcAction::Patrol } else { NpcAction::Idle } }
            else if health < 0.2 && o.can_move { NpcAction::Retreat }
            else if matches!(o.backend, DecisionBackend::StateMachine) { NpcAction::Attack }
            else { utility_combat(health, o.can_move) }
        }
    };
    Decision { npc_id: o.npc_id, action }
}

// Explicit utility scores provide a stable baseline for a future learned policy.
// HybridMl deliberately falls back to this deterministic path until a model is loaded.
fn utility_combat(health: f32, can_move: bool) -> NpcAction {
    let attack = 0.6 + 0.4 * health;
    let retreat = if can_move { 1.0 - health } else { -1.0 };
    if retreat > attack { NpcAction::Retreat } else { NpcAction::Attack }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn obs(role: NpcRole) -> Observation { Observation { npc_id: 42, role, backend: DecisionBackend::Utility, threat_visible: false, health_fraction: 1.0, can_move: true } }
    #[test] fn stationary_merchant_never_patrols() { let mut o=obs(NpcRole::Merchant);o.can_move=false;assert_eq!(decide(o).action,NpcAction::Trade);o.threat_visible=true;assert_eq!(decide(o).action,NpcAction::Idle); }
    #[test] fn goblin_patrols_attacks_and_retreats() {let mut o=obs(NpcRole::Enemy);assert_eq!(decide(o).action,NpcAction::Patrol);o.threat_visible=true;assert_eq!(decide(o).action,NpcAction::Attack);o.health_fraction=0.1;assert_eq!(decide(o).action,NpcAction::Retreat);}
    #[test] fn immobile_enemy_cannot_retreat() {let mut o=obs(NpcRole::Enemy);o.can_move=false;o.threat_visible=true;o.health_fraction=0.01;assert_eq!(decide(o).action,NpcAction::Attack);}
    #[test] fn hybrid_has_deterministic_fallback() {let mut o=obs(NpcRole::Boss);o.backend=DecisionBackend::HybridMl;o.threat_visible=true;assert_eq!(decide(o).action,NpcAction::Attack);}
    #[test] fn nonfinite_health_is_safe() {let mut o=obs(NpcRole::Enemy);o.threat_visible=true;o.health_fraction=f32::NAN;assert_eq!(decide(o).action,NpcAction::Attack);}
}
