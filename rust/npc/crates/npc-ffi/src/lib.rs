// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "lib.rs"
// ============================================================================

//! Versioned batched C ABI. Callers own all memory; no pointers retained.

// The workspace denies `unsafe_code`. This crate is the C boundary, so it takes raw pointers and exports
// `no_mangle` functions, and opts back out here, in one visible line. Every `unsafe` block below carries a
// `// SAFETY:` comment and every `unsafe fn` a `# Safety` section.
#![allow(unsafe_code)]

use gtg_npc_core::{DecisionBackend, NpcAction, NpcDisposition, NpcOrder, NpcRole, Observation};

/// Version of the C layout and meaning of every field. The host refuses a library with another version.
pub const ABI_VERSION: u32 = 3;
/// Success.
pub const STATUS_OK: i32 = 0;
/// A pointer was null or not aligned for its type.
pub const STATUS_NULL: i32 = -1;
/// A count was above [`MAX_BATCH`], or the output buffer was smaller than the input.
pub const STATUS_CAPACITY: i32 = -2;
/// An observation had an unknown value, a non-finite number or a non-zero reserved field, or the
/// input and output buffers overlapped. Nothing was written.
pub const STATUS_INVALID: i32 = -3;
/// Most observations accepted by one call.
pub const MAX_BATCH: usize = 4096;

/// One NPC, as C sees it. 64 bytes, 8-byte alignment on supported 64-bit targets. C# uses explicit
/// offsets. Booleans are integers that must be 0 or 1. A zero in every field added after version 2
/// means "no information": hostile, no order, not provoked, level 0.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub struct NpcObservationAbi {
    /// Identifies the NPC, echoed in the decision.
    pub npc_id: u64, // 0
    /// `NpcRole`: 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss, 5 companion.
    pub role: u32, // 8
    /// `DecisionBackend`: 0 state machine, 1 utility, 2 hybrid ML.
    pub backend: u32, // 12
    /// 1 when a threat is in view, otherwise 0.
    pub threat_visible: u32, // 16
    /// Health from 0 to 1. Must be finite.
    pub health_fraction: f32, // 20
    /// 1 when the NPC may move, otherwise 0.
    pub can_move: u32, // 24
    /// Must be zero.
    pub reserved: u32, // 28
    /// `NpcDisposition`: 0 hostile, 1 retaliatory, 2 peaceful.
    pub disposition: u8, // 32
    /// `NpcOrder`: 0 none, 1 follow, 2 hold, 3 attack. Used by companions only.
    pub order: u8, // 33
    /// 1 when the NPC was attacked recently, otherwise 0.
    pub provoked: u8, // 34
    /// Must be zero.
    pub reserved_byte: u8, // 35
    /// The NPC's level.
    pub level: u16, // 36
    /// The difficulty of the current order as a level, 0 for an order with none.
    pub order_level: u16, // 38
    /// Must be zero. Room for fields that a later version defines.
    pub reserved_tail: [u64; 3], // 40
}

/// One decision, as C sees it. 16 bytes.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct NpcDecisionAbi {
    /// The NPC the decision is for.
    pub npc_id: u64,
    /// 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat, 5 follow, 6 hold, 7 refuse order.
    pub action: i32,
    /// Always zero.
    pub reserved: u32,
}

const _: () = assert!(std::mem::size_of::<NpcObservationAbi>() == 64);
const _: () = assert!(std::mem::size_of::<NpcDecisionAbi>() == 16);

/// The ABI version of this library.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_abi_version() -> u32 {
    ABI_VERSION
}

/// The size of one [`NpcObservationAbi`] in bytes, for the host to check against its own struct.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_observation_size() -> u32 {
    std::mem::size_of::<NpcObservationAbi>() as u32
}

/// The size of one [`NpcDecisionAbi`] in bytes, for the host to check against its own struct.
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
        5 => NpcRole::Companion,
        _ => return None,
    };
    let backend = match raw.backend {
        0 => DecisionBackend::StateMachine,
        1 => DecisionBackend::Utility,
        2 => DecisionBackend::HybridMl,
        _ => return None,
    };
    let disposition = match raw.disposition {
        0 => NpcDisposition::Hostile,
        1 => NpcDisposition::Retaliatory,
        2 => NpcDisposition::Peaceful,
        _ => return None,
    };
    let order = match raw.order {
        0 => NpcOrder::None,
        1 => NpcOrder::Follow,
        2 => NpcOrder::Hold,
        3 => NpcOrder::Attack,
        _ => return None,
    };
    if raw.reserved != 0
        || raw.reserved_byte != 0
        || raw.reserved_tail != [0; 3]
        || raw.threat_visible > 1
        || raw.can_move > 1
        || raw.provoked > 1
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
        disposition,
        order,
        provoked: raw.provoked == 1,
        level: raw.level,
        order_level: raw.order_level,
    })
}

fn action_code(action: NpcAction) -> i32 {
    match action {
        NpcAction::Idle => 0,
        NpcAction::Trade => 1,
        NpcAction::Patrol => 2,
        NpcAction::Attack => 3,
        NpcAction::Retreat => 4,
        NpcAction::Follow => 5,
        NpcAction::Hold => 6,
        NpcAction::RefuseOrder => 7,
    }
}

/// Decides every NPC in a batch with one call. All observations are checked before any output is
/// written, so a rejected batch leaves `outputs` untouched. Returns one of the `STATUS_` codes.
///
/// # Safety
///
/// `inputs` must point to `count` initialized observations and `outputs` to `output_capacity`
/// writable decisions, both aligned for their type and not overlapping. Both remain valid for the
/// call. Zero-count calls may use null pointers. No memory is retained.
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
    if inputs.is_null() || outputs.is_null() || !inputs.is_aligned() || !outputs.is_aligned() {
        return STATUS_NULL;
    }
    // The two ranges must not overlap, because one is read as a shared slice and the other written as
    // an exclusive one. `n` is at most MAX_BATCH, so the sums below stay far from isize::MAX.
    let in_start = inputs as usize;
    let out_start = outputs as usize;
    let (Some(in_end), Some(out_end)) = (
        in_start.checked_add(n * std::mem::size_of::<NpcObservationAbi>()),
        out_start.checked_add(n * std::mem::size_of::<NpcDecisionAbi>()),
    ) else {
        return STATUS_INVALID;
    };
    if in_start < out_end && out_start < in_end {
        return STATUS_INVALID;
    }
    // SAFETY: `inputs` is non-null and aligned (checked above). The caller guarantees `count`
    // initialized observations that stay valid for the call (see # Safety), and n * 64 bytes is far below
    // isize::MAX because n <= MAX_BATCH.
    let observations = unsafe { std::slice::from_raw_parts(inputs, n) };
    if observations.iter().any(|o| parse(o).is_none()) {
        return STATUS_INVALID;
    }
    // SAFETY: `outputs` is non-null and aligned (checked above) and the caller guarantees
    // `output_capacity >= count` writable decisions, valid for the call. It does not overlap `inputs`
    // (checked above), so no other reference to this memory exists while the slice is alive.
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

/// Compatibility entry point for the initial scalar prototype. It decides one hostile NPC with the
/// state machine backend and no order. Returns the action code, or [`STATUS_INVALID`].
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
        // SAFETY: both slices are valid for their lengths, cannot overlap (one is shared, one is
        // exclusive), are aligned by construction and outlive the call.
        unsafe {
            gtg_npc_decide_batch(
                input.as_ptr(),
                input.len() as u32,
                output.as_mut_ptr(),
                output.len() as u32,
            )
        }
    }

    fn one(o: NpcObservationAbi) -> i32 {
        let mut out = [NpcDecisionAbi::default()];
        assert_eq!(call(&[o], &mut out), STATUS_OK);
        out[0].action
    }

    fn companion(order: u8, level: u16, order_level: u16) -> NpcObservationAbi {
        NpcObservationAbi {
            order,
            level,
            order_level,
            ..raw(5)
        }
    }

    #[test]
    fn abi_layout() {
        assert_eq!(std::mem::size_of::<NpcObservationAbi>(), 64);
        assert_eq!(std::mem::size_of::<NpcDecisionAbi>(), 16);
        assert_eq!(std::mem::align_of::<NpcObservationAbi>(), 8);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, npc_id), 0);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, role), 8);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, backend), 12);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, threat_visible), 16);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, health_fraction), 20);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, can_move), 24);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, reserved), 28);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, disposition), 32);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, order), 33);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, provoked), 34);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, reserved_byte), 35);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, level), 36);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, order_level), 38);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, reserved_tail), 40);
        assert_eq!(std::mem::offset_of!(NpcDecisionAbi, action), 8);
        assert_eq!(gtg_npc_abi_version(), 3);
        assert_eq!(gtg_npc_observation_size(), 64);
        assert_eq!(gtg_npc_decision_size(), 16);
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
        o.health_fraction = f32::INFINITY;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.health_fraction = 1.0;
        o.reserved = 1;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.reserved = 0;
        o.reserved_byte = 1;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.reserved_byte = 0;
        for slot in 0..3 {
            let mut t = o;
            t.reserved_tail[slot] = 1;
            assert_eq!(call(&[t], &mut out), STATUS_INVALID, "tail slot {slot}");
        }
        assert_eq!(call(&[o], &mut out), STATUS_OK);
    }

    #[test]
    fn rejects_unknown_enum_values_and_non_boolean_flags() {
        let mut out = [NpcDecisionAbi::default()];
        for (name, bad) in [
            ("role", NpcObservationAbi { role: 6, ..raw(3) }),
            (
                "backend",
                NpcObservationAbi {
                    backend: 3,
                    ..raw(3)
                },
            ),
            (
                "disposition",
                NpcObservationAbi {
                    disposition: 3,
                    ..raw(3)
                },
            ),
            ("order", NpcObservationAbi { order: 4, ..raw(5) }),
            (
                "provoked",
                NpcObservationAbi {
                    provoked: 2,
                    ..raw(3)
                },
            ),
            (
                "threat_visible",
                NpcObservationAbi {
                    threat_visible: 2,
                    ..raw(3)
                },
            ),
            (
                "can_move",
                NpcObservationAbi {
                    can_move: 2,
                    ..raw(3)
                },
            ),
        ] {
            assert_eq!(call(&[bad], &mut out), STATUS_INVALID, "{name}");
        }
    }

    #[test]
    fn every_valid_enum_value_is_accepted() {
        let mut out = [NpcDecisionAbi::default()];
        for role in 0..=5 {
            assert_eq!(call(&[raw(role)], &mut out), STATUS_OK, "role {role}");
        }
        for backend in 0..=2 {
            let o = NpcObservationAbi { backend, ..raw(3) };
            assert_eq!(call(&[o], &mut out), STATUS_OK, "backend {backend}");
        }
        for disposition in 0..=2 {
            let o = NpcObservationAbi {
                disposition,
                ..raw(3)
            };
            assert_eq!(call(&[o], &mut out), STATUS_OK, "disposition {disposition}");
        }
        for order in 0..=3 {
            assert_eq!(
                call(&[companion(order, 1, 0)], &mut out),
                STATUS_OK,
                "order {order}"
            );
        }
    }

    #[test]
    fn version_two_callers_keep_their_behavior() {
        // Only the fields that existed in version 2 are set. Every later field is zero.
        let mut enemy = raw(3);
        enemy.threat_visible = 1;
        assert_eq!(one(enemy), 3, "an enemy that sees a threat attacks");
        assert_eq!(one(raw(3)), 2, "an enemy that sees nothing patrols");
        assert_eq!(one(raw(0)), 1, "a merchant trades");
        let mut civilian = raw(1);
        civilian.threat_visible = 1;
        assert_eq!(one(civilian), 4, "a civilian that sees a threat runs");
    }

    #[test]
    fn disposition_and_provoked_reach_the_decision() {
        let mut retaliatory = NpcObservationAbi {
            disposition: 1,
            threat_visible: 1,
            ..raw(3)
        };
        assert_eq!(one(retaliatory), 2, "ignores the threat, patrols");
        retaliatory.provoked = 1;
        assert_eq!(one(retaliatory), 3, "fights back once attacked");
        let peaceful = NpcObservationAbi {
            disposition: 2,
            threat_visible: 1,
            provoked: 1,
            ..raw(3)
        };
        assert_eq!(one(peaceful), 4, "never fights, runs");
    }

    #[test]
    fn companion_orders_reach_the_decision() {
        assert_eq!(one(companion(0, 10, 0)), 5, "no order follows");
        assert_eq!(one(companion(1, 10, 0)), 5, "follow");
        assert_eq!(one(companion(2, 10, 0)), 6, "hold");
        let mut attack = companion(3, 10, 0);
        attack.threat_visible = 1;
        assert_eq!(one(attack), 3, "attack order engages");
        assert_eq!(
            one(companion(3, 10, 15)),
            5,
            "an order exactly at the gap is accepted"
        );
        assert_eq!(
            one(companion(3, 10, 16)),
            7,
            "an order above the gap is refused"
        );
    }

    #[test]
    fn every_action_code_can_be_produced() {
        let mut seen = [false; 8];
        let mut hurt_attacker = raw(3);
        hurt_attacker.threat_visible = 1;
        hurt_attacker.health_fraction = 0.1;
        let mut attacker = raw(3);
        attacker.threat_visible = 1;
        for o in [
            raw(3),
            raw(0),
            NpcObservationAbi {
                threat_visible: 1,
                ..raw(0)
            },
            attacker,
            hurt_attacker,
            companion(1, 5, 0),
            companion(2, 5, 0),
            companion(3, 5, 99),
        ] {
            seen[one(o) as usize] = true;
        }
        assert_eq!(seen, [true; 8]);
    }

    #[test]
    fn null_and_empty() {
        // SAFETY: a zero count never reads or writes through the pointers, which the contract allows to be null.
        let empty = unsafe { gtg_npc_decide_batch(std::ptr::null(), 0, std::ptr::null_mut(), 0) };
        assert_eq!(empty, 0);
        // SAFETY: the call checks for null before it touches either pointer and returns an error code.
        let null = unsafe { gtg_npc_decide_batch(std::ptr::null(), 1, std::ptr::null_mut(), 1) };
        assert_eq!(null, STATUS_NULL);
    }

    #[test]
    fn max_batch_is_enforced() {
        // SAFETY: a count above MAX_BATCH is rejected before either pointer is used.
        let status =
            unsafe { gtg_npc_decide_batch(std::ptr::null(), 4097, std::ptr::null_mut(), 4097) };
        assert_eq!(status, STATUS_CAPACITY);
        let input = vec![raw(0); MAX_BATCH];
        let mut out = vec![NpcDecisionAbi::default(); MAX_BATCH];
        assert_eq!(
            call(&input, &mut out),
            STATUS_OK,
            "exactly the limit is accepted"
        );
    }

    #[test]
    fn misaligned_pointers_are_rejected_before_use() {
        let bytes = vec![0u8; 64 * 2 + 8];
        let misaligned = bytes.as_ptr().wrapping_add(1).cast::<NpcObservationAbi>();
        let mut out = [NpcDecisionAbi::default()];
        // SAFETY: the call rejects a misaligned input pointer without reading through it, and the
        // output pointer is valid.
        let status = unsafe { gtg_npc_decide_batch(misaligned, 1, out.as_mut_ptr(), 1) };
        assert_eq!(status, STATUS_NULL);
        let input = [raw(0)];
        let mut buf = vec![0u8; 16 * 2 + 8];
        let bad_out = buf.as_mut_ptr().wrapping_add(1).cast::<NpcDecisionAbi>();
        // SAFETY: the call rejects a misaligned output pointer without writing through it, and the
        // input pointer is valid.
        let status = unsafe { gtg_npc_decide_batch(input.as_ptr(), 1, bad_out, 1) };
        assert_eq!(status, STATUS_NULL);
    }

    #[test]
    fn overlapping_buffers_are_rejected_and_nothing_is_written() {
        let mut shared = vec![raw(3); 4];
        let before = shared[0].npc_id;
        let inputs = shared.as_ptr();
        let outputs = shared.as_mut_ptr().cast::<NpcDecisionAbi>();
        // SAFETY: the call detects the overlap and returns before it creates a slice from either
        // pointer, so the aliasing is never observed. Both pointers are valid and aligned.
        let status = unsafe { gtg_npc_decide_batch(inputs, 2, outputs, 2) };
        assert_eq!(status, STATUS_INVALID);
        assert_eq!(shared[0].npc_id, before);

        // Adjacent ranges that do not overlap are fine.
        let input = vec![raw(0); 2];
        let mut out = vec![NpcDecisionAbi::default(); 2];
        assert_eq!(call(&input, &mut out), STATUS_OK);
    }

    #[test]
    fn the_scalar_stub_still_works() {
        assert_eq!(gtg_npc_decide_stub(3, 1, 1.0, 1), 3);
        assert_eq!(gtg_npc_decide_stub(0, 0, 1.0, 0), 1);
        assert_eq!(gtg_npc_decide_stub(99, 0, 1.0, 1), STATUS_INVALID);
        assert_eq!(gtg_npc_decide_stub(3, 0, f32::NAN, 1), STATUS_INVALID);
    }
}
