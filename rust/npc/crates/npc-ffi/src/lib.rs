// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "lib.rs"
// ============================================================================

//! Versioned batched C ABI. Callers own all memory; no pointers retained.

// The workspace denies `unsafe_code`. This crate is the C boundary, so it takes raw pointers and exports
// `no_mangle` functions, and opts back out here, in one visible line. Every `unsafe` block below carries a
// `// SAFETY:` comment and every `unsafe fn` a `# Safety` section.
#![allow(unsafe_code)]

use gtg_npc_core::{
    AttackStyle, DecisionBackend, MissionOutcome, NpcAction, NpcDisposition, NpcOrder, NpcRole,
    Observation, MAX_CROWN_HEAT,
};

/// Version of the C layout and meaning of every field. The host refuses a library with another version.
pub const ABI_VERSION: u32 = 7;
/// Success.
pub const STATUS_OK: i32 = 0;
/// A pointer was null or not aligned for its type.
pub const STATUS_NULL: i32 = -1;
/// A count was above [`MAX_BATCH`], or the output buffer was smaller than the input.
pub const STATUS_CAPACITY: i32 = -2;
/// An observation had an unknown value or a non-finite number, or the input and output buffers
/// overlapped. Nothing was written.
pub const STATUS_INVALID: i32 = -3;
/// Most observations accepted by one call.
pub const MAX_BATCH: usize = 4096;

/// One NPC, as C sees it. 64 bytes, 8-byte alignment on supported 64-bit targets. C# uses explicit
/// offsets. Booleans are integers that must be 0 or 1. A zero in every field added after version 2
/// means "no information": hostile, no order, not provoked, level 0, no betrayal opportunity, no
/// Crown Heat, melee, nothing to shoot, not in a ranged stance, an unknown distance to the threat and
/// neutral favor.
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
    /// Distance in meters from the NPC to the threat it is fighting. Must be finite. Zero or less means
    /// unknown. Offset 28 held a reserved word up to version 5, so a caller that still sends zero there
    /// means unknown.
    pub threat_distance: f32, // 28
    /// `NpcDisposition`: 0 hostile, 1 retaliatory, 2 peaceful.
    pub disposition: u8, // 32
    /// `NpcOrder`: 0 none, 1 follow, 2 hold, 3 attack, 4 deliver, 5 raid. Used by companions only.
    pub order: u8, // 33
    /// 1 when the NPC was attacked recently, otherwise 0.
    pub provoked: u8, // 34
    /// The Crown Heat tier of the region the NPC stands in: 0 none, 1 wanted, 2 hunted, 3 outlaw. Above 3
    /// is rejected. Only guards read it. Offset 35 held a reserved byte up to version 4, so a caller that
    /// still sends zero there means no heat.
    pub crown_heat: u8, // 35
    /// The NPC's level.
    pub level: u16, // 36
    /// The difficulty of the current order as a level, 0 for an order with none.
    pub order_level: u16, // 38
    /// How trustworthy the companion is, 0 to 1. Must be finite. Used for betrayal only.
    pub trustworthiness: f32, // 40
    /// How much the companion likes the player, 0 to 1. Must be finite. Used for betrayal only.
    pub affinity: f32, // 44
    /// How satisfied the companion is with its pay, 0 to 1. Must be finite. Used for betrayal only.
    pub pay_satisfaction: f32, // 48
    /// A random number the host supplies for each decision.
    pub noise: u32, // 52
    /// 1 when the companion could betray the player now, otherwise 0. Without it no betrayal happens.
    pub betrayal_opportunity: u8, // 56
    /// `AttackStyle`: 0 melee, 1 ranged, 2 hybrid. Offset 57 held a reserved byte up to version 5, so a
    /// caller that still sends zero there is a melee fighter.
    pub attack_style: u8, // 57
    /// 1 when the NPC has something to shoot, arrows or mana, otherwise 0. Ranged and hybrid fighters read
    /// it. Offset 58 held a reserved byte up to version 5.
    pub ranged_ready: u8, // 58
    /// 1 when the NPC's last combat action was shooting or backing away, otherwise 0. A hybrid fighter
    /// keeps its stance while the threat is between its two distances. Offset 59 held a reserved byte up
    /// to version 5.
    pub ranged_stance: u8, // 59
    /// How much the NPC likes the player, from -1 (hates) to 1 (adores). Must be finite. A value outside
    /// the range is clamped. Zero is neutral. Offset 60 was a reserved word up to version 6, so a caller
    /// that still sends zero there means neutral. Only shopkeepers and civilians read it.
    pub favor: f32, // 60
}

/// One decision, as C sees it. 16 bytes.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct NpcDecisionAbi {
    /// The NPC the decision is for.
    pub npc_id: u64,
    /// 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat, 5 follow, 6 hold, 7 refuse order, 8 mission,
    /// 9 betray, 10 shoot, 11 kite.
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
        4 => NpcOrder::Deliver,
        5 => NpcOrder::Raid,
        _ => return None,
    };
    let attack_style = match raw.attack_style {
        0 => AttackStyle::Melee,
        1 => AttackStyle::Ranged,
        2 => AttackStyle::Hybrid,
        _ => return None,
    };
    if raw.threat_visible > 1
        || raw.can_move > 1
        || raw.provoked > 1
        || raw.betrayal_opportunity > 1
        || raw.ranged_ready > 1
        || raw.ranged_stance > 1
        || raw.crown_heat > MAX_CROWN_HEAT
        || !raw.health_fraction.is_finite()
        || !raw.threat_distance.is_finite()
        || !raw.favor.is_finite()
        || !raw.trustworthiness.is_finite()
        || !raw.affinity.is_finite()
        || !raw.pay_satisfaction.is_finite()
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
        trustworthiness: raw.trustworthiness.clamp(0.0, 1.0),
        affinity: raw.affinity.clamp(0.0, 1.0),
        pay_satisfaction: raw.pay_satisfaction.clamp(0.0, 1.0),
        noise: raw.noise,
        betrayal_opportunity: raw.betrayal_opportunity == 1,
        crown_heat: raw.crown_heat,
        attack_style,
        ranged_ready: raw.ranged_ready == 1,
        ranged_stance: raw.ranged_stance == 1,
        // A negative distance means unknown, the same as zero.
        threat_distance: raw.threat_distance.max(0.0),
        favor: raw.favor.clamp(-1.0, 1.0),
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
        NpcAction::Mission => 8,
        NpcAction::Betray => 9,
        NpcAction::Shoot => 10,
        NpcAction::Kite => 11,
    }
}

fn outcome_code(outcome: MissionOutcome) -> i32 {
    match outcome {
        MissionOutcome::Success => 0,
        MissionOutcome::Failed => 1,
        MissionOutcome::Caught => 2,
        MissionOutcome::Killed => 3,
    }
}

/// How a mission that happens off screen ends. `order` is 4 for a delivery or 5 for a raid, the levels
/// are the companion's and the mission's, and `noise` is a random number from the host. Returns 0 for
/// success, 1 failed, 2 caught, 3 killed, or [`STATUS_INVALID`] for an order that is not a mission or a
/// level above 65535.
#[unsafe(no_mangle)]
pub extern "C" fn gtg_npc_resolve_mission(
    order: u32,
    companion_level: u32,
    mission_level: u32,
    noise: u32,
) -> i32 {
    let order = match order {
        4 => NpcOrder::Deliver,
        5 => NpcOrder::Raid,
        _ => return STATUS_INVALID,
    };
    let (Ok(companion), Ok(mission)) =
        (u16::try_from(companion_level), u16::try_from(mission_level))
    else {
        return STATUS_INVALID;
    };
    match gtg_npc_behavior::resolve_mission(order, companion, mission, noise) {
        Some(outcome) => outcome_code(outcome),
        None => STATUS_INVALID,
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
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, threat_distance), 28);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, disposition), 32);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, order), 33);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, provoked), 34);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, crown_heat), 35);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, level), 36);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, order_level), 38);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, trustworthiness), 40);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, affinity), 44);
        assert_eq!(
            std::mem::offset_of!(NpcObservationAbi, pay_satisfaction),
            48
        );
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, noise), 52);
        assert_eq!(
            std::mem::offset_of!(NpcObservationAbi, betrayal_opportunity),
            56
        );
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, attack_style), 57);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, ranged_ready), 58);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, ranged_stance), 59);
        assert_eq!(std::mem::offset_of!(NpcObservationAbi, favor), 60);
        assert_eq!(std::mem::offset_of!(NpcDecisionAbi, action), 8);
        assert_eq!(gtg_npc_abi_version(), 7);
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
    fn rejects_nonfinite() {
        let mut o = raw(3);
        o.health_fraction = f32::NAN;
        let mut out = [NpcDecisionAbi::default()];
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.health_fraction = f32::INFINITY;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID);
        o.health_fraction = 1.0;
        o.threat_distance = f32::NAN;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID, "distance nan");
        o.threat_distance = f32::INFINITY;
        assert_eq!(call(&[o], &mut out), STATUS_INVALID, "distance infinite");
        o.threat_distance = f32::NEG_INFINITY;
        assert_eq!(
            call(&[o], &mut out),
            STATUS_INVALID,
            "distance negative infinite"
        );
        o.threat_distance = 0.0;
        for (name, favor) in [
            ("favor nan", f32::NAN),
            ("favor infinite", f32::INFINITY),
            ("favor negative infinite", f32::NEG_INFINITY),
        ] {
            let bad = NpcObservationAbi { favor, ..o };
            assert_eq!(call(&[bad], &mut out), STATUS_INVALID, "{name}");
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
            ("order", NpcObservationAbi { order: 6, ..raw(5) }),
            (
                "betrayal_opportunity",
                NpcObservationAbi {
                    betrayal_opportunity: 2,
                    ..raw(5)
                },
            ),
            (
                "trustworthiness nan",
                NpcObservationAbi {
                    trustworthiness: f32::NAN,
                    ..raw(5)
                },
            ),
            (
                "affinity infinite",
                NpcObservationAbi {
                    affinity: f32::INFINITY,
                    ..raw(5)
                },
            ),
            (
                "pay nan",
                NpcObservationAbi {
                    pay_satisfaction: f32::NAN,
                    ..raw(5)
                },
            ),
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
            (
                "attack_style",
                NpcObservationAbi {
                    attack_style: 3,
                    ..raw(3)
                },
            ),
            (
                "ranged_ready",
                NpcObservationAbi {
                    ranged_ready: 2,
                    ..raw(3)
                },
            ),
            (
                "ranged_stance",
                NpcObservationAbi {
                    ranged_stance: 2,
                    ..raw(3)
                },
            ),
            (
                "crown_heat above the highest tier",
                NpcObservationAbi {
                    crown_heat: 4,
                    ..raw(2)
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
        for order in 0..=5 {
            assert_eq!(
                call(&[companion(order, 1, 0)], &mut out),
                STATUS_OK,
                "order {order}"
            );
        }
        for attack_style in 0..=2 {
            let o = NpcObservationAbi {
                attack_style,
                ..raw(3)
            };
            assert_eq!(
                call(&[o], &mut out),
                STATUS_OK,
                "attack style {attack_style}"
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
    fn crown_heat_is_validated_and_reaches_the_decision() {
        let mut out = [NpcDecisionAbi::default()];
        for tier in 0..=3u8 {
            let o = NpcObservationAbi {
                crown_heat: tier,
                ..raw(2)
            };
            assert_eq!(call(&[o], &mut out), STATUS_OK, "tier {tier}");
        }
        let sentinel = NpcDecisionAbi {
            npc_id: 999,
            action: 9,
            reserved: 9,
        };
        for tier in [4u8, 5, 100, 255] {
            let mut out = [sentinel; 2];
            let bad = NpcObservationAbi {
                crown_heat: tier,
                ..raw(2)
            };
            assert_eq!(
                call(&[raw(0), bad], &mut out),
                STATUS_INVALID,
                "tier {tier}"
            );
            assert_eq!(out, [sentinel; 2], "a rejected tier writes nothing");
        }
        let guard = NpcObservationAbi {
            disposition: 1,
            threat_visible: 1,
            ..raw(2)
        };
        assert_eq!(
            one(guard),
            2,
            "a retaliatory guard ignores a threat while the player is not wanted"
        );
        for tier in 1..=3u8 {
            let wanted = NpcObservationAbi {
                crown_heat: tier,
                ..guard
            };
            assert_eq!(one(wanted), 3, "and attacks it at tier {tier}");
        }
        let enemy = NpcObservationAbi {
            disposition: 1,
            threat_visible: 1,
            crown_heat: 3,
            ..raw(3)
        };
        assert_eq!(one(enemy), 2, "an enemy does not read the tier");
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
            one(companion(3, 10, 13)),
            5,
            "an attack order exactly at its gap of 3 is accepted"
        );
        assert_eq!(
            one(companion(3, 10, 14)),
            7,
            "an attack order above its gap is refused"
        );
    }

    #[test]
    fn every_action_code_can_be_produced() {
        let mut seen = [false; 12];
        let mut hurt_attacker = raw(3);
        hurt_attacker.threat_visible = 1;
        hurt_attacker.health_fraction = 0.1;
        let mut attacker = raw(3);
        attacker.threat_visible = 1;
        let betrayer = NpcObservationAbi {
            betrayal_opportunity: 1,
            ..companion(0, 5, 0)
        };
        let archer = NpcObservationAbi {
            attack_style: 1,
            ranged_ready: 1,
            threat_distance: 20.0,
            ..attacker
        };
        let cornered_archer = NpcObservationAbi {
            threat_distance: 2.0,
            ..archer
        };
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
            companion(4, 5, 5),
            betrayer,
            archer,
            cornered_archer,
        ] {
            seen[one(o) as usize] = true;
        }
        assert_eq!(seen, [true; 12]);
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
        // The buffers start on an 8-byte boundary, so a pointer one byte into them is misaligned for both
        // structs by construction, whatever the allocator does.
        #[repr(align(8))]
        struct Aligned<const N: usize>([u8; N]);
        let bytes = Aligned([0u8; 64 * 2 + 8]);
        let misaligned = bytes.0.as_ptr().wrapping_add(1).cast::<NpcObservationAbi>();
        let mut out = [NpcDecisionAbi::default()];
        // SAFETY: the call rejects a misaligned input pointer without reading through it, and the
        // output pointer is valid.
        let status = unsafe { gtg_npc_decide_batch(misaligned, 1, out.as_mut_ptr(), 1) };
        assert_eq!(status, STATUS_NULL);
        let input = [raw(0)];
        let mut buf = Aligned([0u8; 16 * 2 + 8]);
        let bad_out = buf.0.as_mut_ptr().wrapping_add(1).cast::<NpcDecisionAbi>();
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

    fn loyal(order: u8, level: u16, order_level: u16) -> NpcObservationAbi {
        NpcObservationAbi {
            trustworthiness: 1.0,
            affinity: 1.0,
            pay_satisfaction: 1.0,
            ..companion(order, level, order_level)
        }
    }

    #[test]
    fn the_refusal_gap_depends_on_the_order_type() {
        // Level 10: a delivery accepts up to 16, an attack up to 13, a raid up to 11.
        assert_eq!(
            one(loyal(4, 10, 16)),
            8,
            "delivery at its gap is accepted and carried out"
        );
        assert_eq!(
            one(loyal(4, 10, 17)),
            7,
            "delivery above its gap is refused"
        );
        assert_eq!(
            one(loyal(3, 10, 13)),
            5,
            "an attack order at its gap is accepted"
        );
        assert_eq!(one(loyal(3, 10, 14)), 7, "above its gap is refused");
        assert_eq!(one(loyal(5, 10, 11)), 8, "a raid at its gap is accepted");
        assert_eq!(one(loyal(5, 10, 12)), 7, "above its gap is refused");
        assert_eq!(
            one(loyal(5, 10, 14)),
            7,
            "the same difference of 4 refuses a raid"
        );
        assert_eq!(one(loyal(4, 10, 14)), 8, "and not a delivery");
    }

    #[test]
    fn betrayal_inputs_reach_the_decision() {
        let mut o = NpcObservationAbi {
            betrayal_opportunity: 1,
            ..companion(0, 5, 0)
        };
        assert_eq!(one(o), 9, "no loyalty, a good roll, an opportunity");
        o.noise = u32::MAX;
        assert_ne!(one(o), 9, "a bad roll");
        o.noise = 0;
        o.betrayal_opportunity = 0;
        assert_ne!(one(o), 9, "no opportunity");
        o.betrayal_opportunity = 1;
        o.trustworthiness = 1.0;
        o.affinity = 1.0;
        o.pay_satisfaction = 1.0;
        assert_ne!(one(o), 9, "a loyal companion");
        o.trustworthiness = 0.5;
        o.affinity = 0.5;
        o.pay_satisfaction = 0.5;
        assert_ne!(one(o), 9, "loyalty exactly at the ceiling");
        o.affinity = 0.0;
        assert_eq!(
            one(o),
            9,
            "liking the player less lowers loyalty below the ceiling"
        );
    }

    #[test]
    fn out_of_range_betrayal_numbers_are_clamped_not_rejected() {
        let mut o = NpcObservationAbi {
            betrayal_opportunity: 1,
            trustworthiness: 7.0,
            affinity: 7.0,
            pay_satisfaction: 7.0,
            ..companion(0, 5, 0)
        };
        assert_ne!(one(o), 9, "too large counts as 1");
        o.trustworthiness = -7.0;
        o.affinity = -7.0;
        o.pay_satisfaction = -7.0;
        assert_eq!(one(o), 9, "too small counts as 0");
    }

    #[test]
    fn version_three_callers_never_betray() {
        // The fields of version 3 only. Everything added in version 4 is zero.
        let o = NpcObservationAbi {
            role: 5,
            backend: 1,
            health_fraction: 1.0,
            can_move: 1,
            level: 10,
            ..Default::default()
        };
        assert_eq!(
            one(o),
            5,
            "a companion with no order follows, and never betrays"
        );
    }

    #[test]
    fn the_mission_export_matches_the_rules() {
        assert_eq!(
            gtg_npc_resolve_mission(5, 10, 10, 0),
            0,
            "raid, lowest roll"
        );
        assert_eq!(
            gtg_npc_resolve_mission(5, 10, 10, u32::MAX),
            3,
            "raid, highest roll"
        );
        assert_eq!(
            gtg_npc_resolve_mission(4, 10, 10, 0),
            0,
            "delivery, lowest roll"
        );
        assert_eq!(
            gtg_npc_resolve_mission(4, 10, 10, u32::MAX),
            3,
            "delivery, highest roll"
        );
        for noise in [0u32, 1, 0x8000_0000, 0xDEAD_BEEF, u32::MAX] {
            for (order, kind) in [(4u32, NpcOrder::Deliver), (5u32, NpcOrder::Raid)] {
                let expected = gtg_npc_behavior::resolve_mission(kind, 9, 14, noise).unwrap();
                assert_eq!(
                    gtg_npc_resolve_mission(order, 9, 14, noise),
                    outcome_code(expected)
                );
            }
        }
    }

    #[test]
    fn the_mission_export_rejects_bad_input() {
        for order in [0u32, 1, 2, 3, 6, 99] {
            assert_eq!(
                gtg_npc_resolve_mission(order, 10, 10, 0),
                STATUS_INVALID,
                "order {order}"
            );
        }
        assert_eq!(gtg_npc_resolve_mission(5, 65536, 10, 0), STATUS_INVALID);
        assert_eq!(gtg_npc_resolve_mission(5, 10, 65536, 0), STATUS_INVALID);
        assert_eq!(gtg_npc_resolve_mission(5, u32::MAX, 10, 0), STATUS_INVALID);
        assert_eq!(
            gtg_npc_resolve_mission(5, 65535, 65535, 0),
            0,
            "the largest levels are fine"
        );
    }

    fn shooter(style: u8, distance: f32) -> NpcObservationAbi {
        NpcObservationAbi {
            threat_visible: 1,
            attack_style: style,
            ranged_ready: 1,
            threat_distance: distance,
            ..raw(3)
        }
    }

    #[test]
    fn version_five_callers_keep_their_behavior() {
        // Zero in every field added in version 6 is a melee fighter with an unknown distance.
        let mut enemy = raw(3);
        enemy.threat_visible = 1;
        assert_eq!(one(enemy), 3, "attacks by hand");
        let mut guard = NpcObservationAbi {
            disposition: 1,
            threat_visible: 1,
            ..raw(2)
        };
        assert_eq!(
            one(guard),
            2,
            "a retaliatory guard still ignores the threat"
        );
        guard.crown_heat = 2;
        assert_eq!(one(guard), 3, "and still fights a wanted player");
    }

    #[test]
    fn attack_style_and_distance_reach_the_decision() {
        assert_eq!(
            one(shooter(0, 20.0)),
            3,
            "melee closes in whatever the distance"
        );
        assert_eq!(one(shooter(1, 20.0)), 10, "ranged shoots from far");
        assert_eq!(one(shooter(1, 3.0)), 11, "ranged backs away when close");
        assert_eq!(one(shooter(2, 20.0)), 10, "hybrid shoots from far");
        assert_eq!(one(shooter(2, 2.0)), 3, "hybrid fights by hand up close");
    }

    #[test]
    fn ammo_and_stance_reach_the_decision() {
        let mut empty = shooter(1, 20.0);
        empty.ranged_ready = 0;
        assert_eq!(one(empty), 4, "a ranged fighter with nothing to shoot runs");
        let mut hybrid = shooter(2, 6.0);
        assert_eq!(
            one(hybrid),
            3,
            "a hybrid in the buffer keeps fighting by hand"
        );
        hybrid.ranged_stance = 1;
        assert_eq!(one(hybrid), 10, "and keeps shooting when it was shooting");
        hybrid.ranged_ready = 0;
        assert_eq!(
            one(hybrid),
            3,
            "but with nothing to shoot it fights by hand"
        );
    }

    #[test]
    fn a_negative_distance_means_unknown() {
        for style in [1u8, 2] {
            assert_eq!(
                one(shooter(style, -5.0)),
                one(shooter(style, 0.0)),
                "style {style}"
            );
        }
        assert_eq!(
            one(shooter(1, -5.0)),
            10,
            "a ranged fighter does not back away"
        );
    }

    #[test]
    fn a_rejected_style_field_writes_nothing() {
        let sentinel = NpcDecisionAbi {
            npc_id: 999,
            action: 9,
            reserved: 9,
        };
        for (name, bad) in [
            (
                "style",
                NpcObservationAbi {
                    attack_style: 3,
                    ..shooter(1, 5.0)
                },
            ),
            (
                "ready",
                NpcObservationAbi {
                    ranged_ready: 2,
                    ..shooter(1, 5.0)
                },
            ),
            (
                "stance",
                NpcObservationAbi {
                    ranged_stance: 2,
                    ..shooter(1, 5.0)
                },
            ),
            (
                "distance",
                NpcObservationAbi {
                    threat_distance: f32::NAN,
                    ..shooter(1, 5.0)
                },
            ),
        ] {
            let mut out = [sentinel; 2];
            assert_eq!(call(&[raw(0), bad], &mut out), STATUS_INVALID, "{name}");
            assert_eq!(out, [sentinel; 2], "{name} writes nothing");
        }
    }

    #[test]
    fn favor_reaches_the_decision_of_a_shopkeeper_and_a_civilian() {
        let watched = |role: u32, favor: f32| NpcObservationAbi {
            threat_visible: 1,
            favor,
            ..raw(role)
        };
        assert_eq!(one(watched(0, 0.0)), 0, "neutral shopkeeper stands still");
        assert_eq!(one(watched(0, 0.2)), 1, "liked shopkeeper trades");
        assert_eq!(one(watched(0, -1.0)), 0, "hated shopkeeper stands still");
        assert_eq!(one(watched(1, 0.0)), 4, "neutral civilian retreats");
        assert_eq!(one(watched(1, 0.5)), 2, "liked civilian patrols");
        assert_eq!(one(watched(1, -0.5)), 4, "disliked civilian retreats");
    }

    #[test]
    fn favor_is_clamped_and_zero_is_neutral() {
        let with = |favor: f32| parse(&NpcObservationAbi { favor, ..raw(0) }).map(|o| o.favor);
        assert_eq!(with(7.0), Some(1.0));
        assert_eq!(with(-7.0), Some(-1.0));
        assert_eq!(with(0.25), Some(0.25));
        assert_eq!(with(-0.25), Some(-0.25));
        assert_eq!(with(0.0), Some(0.0));
        assert_eq!(
            parse(&raw(0)).map(|o| o.favor),
            Some(0.0),
            "a caller that never sets it sends zero"
        );
        let liked = NpcObservationAbi {
            threat_visible: 1,
            favor: 100.0,
            ..raw(0)
        };
        assert_eq!(one(liked), 1, "a huge favor counts as 1, so it is liked");
    }

    #[test]
    fn fighters_and_companions_read_no_favor_through_the_abi() {
        for role in [2, 3, 4, 5] {
            let watched = |favor: f32| NpcObservationAbi {
                threat_visible: 1,
                provoked: 1,
                disposition: 1,
                favor,
                ..raw(role)
            };
            let baseline = one(watched(0.0));
            assert_eq!(one(watched(1.0)), baseline, "role {role} liked");
            assert_eq!(one(watched(-1.0)), baseline, "role {role} hated");
        }
    }

    #[test]
    fn mission_outcome_codes_are_distinct_and_stable() {
        assert_eq!(outcome_code(MissionOutcome::Success), 0);
        assert_eq!(outcome_code(MissionOutcome::Failed), 1);
        assert_eq!(outcome_code(MissionOutcome::Caught), 2);
        assert_eq!(outcome_code(MissionOutcome::Killed), 3);
    }
}
