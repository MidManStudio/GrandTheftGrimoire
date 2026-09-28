//! Versioned, scalar-only prototype ABI. Expand to batched SoA after profiling.
use gtg_npc_core::{DecisionBackend, NpcRole, Observation};

/// 1 = current ABI. Reserved fields must remain zero.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_abi_version() -> u32 { 1 }

/// Role: 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss.
/// Returns action: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat; -1 invalid role.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_decide_stub(role: u32, threat_visible: u8, health_fraction: f32, can_move: u8) -> i32 {
    let role = match role { 0 => NpcRole::Merchant, 1 => NpcRole::Civilian, 2 => NpcRole::Guard, 3 => NpcRole::Enemy, 4 => NpcRole::Boss, _ => return -1 };
    let obs = Observation { npc_id: 0, role, backend: DecisionBackend::StateMachine, threat_visible: threat_visible != 0, health_fraction: health_fraction.clamp(0.0, 1.0), can_move: can_move != 0 };
    use gtg_npc_core::NpcAction;
    match gtg_npc_behavior::decide(obs).action { NpcAction::Idle => 0, NpcAction::Trade => 1, NpcAction::Patrol => 2, NpcAction::Attack => 3, NpcAction::Retreat => 4 }
}
