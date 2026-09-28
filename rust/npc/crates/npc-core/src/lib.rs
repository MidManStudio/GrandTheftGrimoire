//! Data-only NPC contract; intentionally independent of Unity and ML.

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcRole { Merchant, Civilian, Guard, Enemy, Boss }

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum DecisionBackend { StateMachine, Utility, HybridMl }

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum NpcAction { Idle, Trade, Patrol, Attack, Retreat }

#[derive(Clone, Copy, Debug)]
pub struct Observation {
    pub npc_id: u64,
    pub role: NpcRole,
    pub backend: DecisionBackend,
    pub threat_visible: bool,
    pub health_fraction: f32,
    pub can_move: bool,
}

#[derive(Clone, Copy, Debug)]
pub struct Decision { pub npc_id: u64, pub action: NpcAction }
