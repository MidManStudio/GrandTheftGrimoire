//! Deliberately simple baseline: compare future learned policies against this.
use gtg_npc_core::{Decision, NpcAction, NpcRole, Observation};

pub fn decide(observation: Observation) -> Decision {
    let action = match observation.role {
        NpcRole::Merchant => NpcAction::Trade,
        _ if observation.threat_visible && observation.health_fraction < 0.2 && observation.can_move => NpcAction::Retreat,
        NpcRole::Guard | NpcRole::Enemy | NpcRole::Boss if observation.threat_visible => NpcAction::Attack,
        NpcRole::Civilian if observation.threat_visible => NpcAction::Idle,
        _ if observation.can_move => NpcAction::Patrol,
        _ => NpcAction::Idle,
    };
    Decision { npc_id: observation.npc_id, action }
}

#[cfg(test)]
mod tests {
    use super::*;
    use gtg_npc_core::DecisionBackend;
    #[test]
    fn stationary_merchant_never_patrols() {
        let d = decide(Observation { npc_id: 1, role: NpcRole::Merchant, backend: DecisionBackend::StateMachine, threat_visible: false, health_fraction: 1.0, can_move: false });
        assert_eq!(d.action, NpcAction::Trade);
    }
}
