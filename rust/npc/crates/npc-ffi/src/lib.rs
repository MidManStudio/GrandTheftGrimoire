//! Versioned batched C ABI. Callers own all memory; no pointers retained.
use gtg_npc_core::{DecisionBackend, NpcAction, NpcRole, Observation};

pub const ABI_VERSION: u32 = 2;
pub const STATUS_OK: i32 = 0;
pub const STATUS_NULL: i32 = -1;
pub const STATUS_CAPACITY: i32 = -2;
pub const STATUS_INVALID: i32 = -3;
pub const MAX_BATCH: usize = 4096;

/// 32 bytes, 8-byte alignment on supported 64-bit targets. C# uses explicit offsets.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct NpcObservationAbi {
    pub npc_id: u64,          // 0
    pub role: u32,            // 8
    pub backend: u32,         // 12
    pub threat_visible: u32,  // 16
    pub health_fraction: f32, // 20
    pub can_move: u32,        // 24
    pub reserved: u32,        // 28; must be zero
}
/// 16 bytes.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct NpcDecisionAbi {
    pub npc_id: u64,
    pub action: i32,
    pub reserved: u32,
}

#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_abi_version() -> u32 {
    ABI_VERSION
}
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_observation_size() -> u32 {
    std::mem::size_of::<NpcObservationAbi>() as u32
}
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_decision_size() -> u32 {
    std::mem::size_of::<NpcDecisionAbi>() as u32
}

fn parse(raw: &NpcObservationAbi) -> Option<Observation> {
    let role = match raw.role {
        0 => NpcRole::Merchant,
        1 => NpcRole::Civilian,
        2 => NpcRole::Guard,
        3 => NpcRole::Enemy,
        4 => NpcRole::Boss,
        _ => return None,
    };
    let backend = match raw.backend {
        0 => DecisionBackend::StateMachine,
        1 => DecisionBackend::Utility,
        2 => DecisionBackend::HybridMl,
        _ => return None,
    };
    if raw.reserved != 0
        || raw.threat_visible > 1
        || raw.can_move > 1
        || !raw.health_fraction.is_finite()
    {
        return None;
    }
    Some(Observation {
        npc_id: raw.npc_id,
        role,
        backend,
        threat_visible: raw.threat_visible == 1,
        health_fraction: raw.health_fraction.clamp(0.0, 1.0),
        can_move: raw.can_move == 1,
    })
}
fn action_code(action: NpcAction) -> i32 {
    match action {
        NpcAction::Idle => 0,
        NpcAction::Trade => 1,
        NpcAction::Patrol => 2,
        NpcAction::Attack => 3,
        NpcAction::Retreat => 4,
    }
}

/// # Safety
/// `inputs` must point to `count` initialized observations and `outputs` to
/// `output_capacity` writable decisions, without overlap. Both remain valid
/// for the call. Zero-count calls may use null pointers. No memory is retained.
/// All observations are validated before any output is written.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn gtg_npc_decide_batch(
    inputs: *const NpcObservationAbi,
    count: u32,
    outputs: *mut NpcDecisionAbi,
    output_capacity: u32,
) -> i32 {
    let n = count as usize;
    if n > MAX_BATCH {
        return STATUS_CAPACITY;
    }
    if n == 0 {
        return STATUS_OK;
    }
    if output_capacity < count {
        return STATUS_CAPACITY;
    }
    if inputs.is_null() || outputs.is_null() {
        return STATUS_NULL;
    }
    // Check pointer arithmetic limits before making slices; contract requires non-overlap.
    if n > isize::MAX as usize / std::mem::size_of::<NpcObservationAbi>()
        || n > isize::MAX as usize / std::mem::size_of::<NpcDecisionAbi>()
    {
        return STATUS_CAPACITY;
    }
    let observations = unsafe { std::slice::from_raw_parts(inputs, n) };
    if observations.iter().any(|o| parse(o).is_none()) {
        return STATUS_INVALID;
    }
    let decisions = unsafe { std::slice::from_raw_parts_mut(outputs, n) };
    for (raw, dst) in observations.iter().zip(decisions.iter_mut()) {
        let d = gtg_npc_behavior::decide(parse(raw).expect("prevalidated"));
        *dst = NpcDecisionAbi {
            npc_id: d.npc_id,
            action: action_code(d.action),
            reserved: 0,
        };
    }
    STATUS_OK
}

/// Compatibility entry point for the initial scalar prototype.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_decide_stub(
    role: u32,
    threat_visible: u8,
    health_fraction: f32,
    can_move: u8,
) -> i32 {
    let raw = NpcObservationAbi {
        role,
        backend: 0,
        threat_visible: u32::from(threat_visible),
        health_fraction,
        can_move: u32::from(can_move),
        ..Default::default()
    };
    match parse(&raw) {
        Some(o) => action_code(gtg_npc_behavior::decide(o).action),
        None => STATUS_INVALID,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn raw(role: u32) -> NpcObservationAbi {
        NpcObservationAbi {
            npc_id: 77,
            role,
            backend: 1,
            health_fraction: 1.0,
            can_move: 1,
            ..Default::default()
        }
    }
    fn call(input: &[NpcObservationAbi], output: &mut [NpcDecisionAbi]) -> i32 {
        unsafe {
            gtg_npc_decide_batch(
                input.as_ptr(),
                input.len() as u32,
                output.as_mut_ptr(),
                output.len() as u32,
            )
        }
    }
    #[test]
    fn abi_layout() {
        assert_eq!(std::mem::size_of::<NpcObservationAbi>(), 32);
        assert_eq!(std::mem::size_of::<NpcDecisionAbi>(), 16);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, health_fraction), 20);
        assert_eq!(std::mem::offset_of!(NpcDecisionAbi, action), 8);
    }
    #[test]
    fn batch_preserves_order_and_identity() {
        let mut enemy = raw(3);
        enemy.npc_id = 99;
        enemy.threat_visible = 1;
        let mut out = [NpcDecisionAbi::default(); 2];
        assert_eq!(call(&[raw(0), enemy], &mut out), 0);
        assert_eq!((out[0].npc_id, out[0].action), (77, 1));
        assert_eq!((out[1].npc_id, out[1].action), (99, 3));
    }
    #[test]
    fn rejects_short_output_without_writing() {
        let sentinel = NpcDecisionAbi {
            npc_id: 999,
            action: 9,
            reserved: 9,
        };
        let mut out = [sentinel];
        assert_eq!(call(&[raw(0), raw(3)], &mut out), STATUS_CAPACITY);
        assert_eq!(out[0], sentinel);
    }
    #[test]
    fn rejects_bad_input_atomically() {
        let sentinel = NpcDecisionAbi {
            npc_id: 999,
            action: 9,
            reserved: 9,
        };
        let mut out = [sentinel; 2];
        assert_eq!(call(&[raw(0), raw(99)], &mut out), STATUS_INVALID);
        assert_eq!(out, [sentinel; 2]);
    }
    #[test]
    fn rejects_nonfinite_and_reserved() {
        let mut o = raw(3);
        o.health_fraction = f32::NAN;
        let mut out = [NpcDecisionAbi::default()];
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.health_fraction = 1.0;
        o.reserved = 1;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
    }
    #[test]
    fn null_and_empty() {
        assert_eq!(
            unsafe { gtg_npc_decide_batch(std::ptr::null(), 0, std::ptr::null_mut(), 0) },
            0
        );
        assert_eq!(
            unsafe { gtg_npc_decide_batch(std::ptr::null(), 1, std::ptr::null_mut(), 1) },
            STATUS_NULL
        );
    }
    #[test]
    fn max_batch_is_enforced() {
        assert_eq!(
            unsafe { gtg_npc_decide_batch(std::ptr::null(), 4097, std::ptr::null_mut(), 4097) },
            STATUS_CAPACITY
        );
    }
}
