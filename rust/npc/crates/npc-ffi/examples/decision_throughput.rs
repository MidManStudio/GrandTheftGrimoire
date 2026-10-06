// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/gtg-npc-ffi.md, section "decision_throughput.rs"
// ============================================================================

// A benchmark that calls the exported C function through raw pointers, like a host does.
#![allow(unsafe_code)]

//! Throughput of the batched decision ABI, called directly from Rust.
//!
//! Two scenarios, both through `gtg_npc_decide_batch`:
//! - `uniform`: every NPC has identical input (best case for branch prediction and caches).
//! - `mixed`: a deterministic town-like population (12% merchants, 38% civilians, 12% guards,
//!   20% enemies, 8% bosses, 10% companions; ~25% see a threat; random health; ~10% of
//!   non-merchants immobile; combatants split 60/25/15 between hostile, retaliatory and peaceful;
//!   20% provoked; levels 1 to 30; companions get a random order and order level).
//!   The batch window slides across a 16384-NPC pool every call, so successive calls see
//!   different data and the branch predictor cannot memorize one batch.
//!
//! Each point is timed `REPS` times (about `NPC_DECISIONS_PER_REP` decisions each, after a warm-up) and
//! reported as the median with the min and max, so one preempted repetition on a shared runner cannot
//! decide the result. `min` and `max` expose how noisy the run was.
//!
//! The generator is duplicated bit for bit in `ffi-smoke-test/bench.c`. Both print a
//! `# mixed_actions` line (action histogram and FNV-1a hash of every pool decision) so CI can
//! check that Rust and C saw the same data and produced the same decisions.
use gtg_npc_ffi::*;
use std::hint::black_box;
use std::time::Instant;

const POOL: usize = 16384;
const SEED: u64 = 0x4754_4700;
const SEED2: u64 = 0x4754_4701;
const ACTIONS: usize = 8;
const COUNTS: [usize; 4] = [10, 100, 500, 1000];
const WINDOW_STEP: usize = 977;
const REPS: usize = 9;
const NPC_DECISIONS_PER_REP: usize = 4_000_000;

fn mix(x: u64) -> u64 {
    let x = x.wrapping_add(0x9E37_79B9_7F4A_7C15);
    let z = (x ^ (x >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
    let z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
    z ^ (z >> 31)
}

fn uniform_pool() -> Vec<NpcObservationAbi> {
    (0..POOL)
        .map(|i| NpcObservationAbi {
            npc_id: i as u64 + 1,
            role: 3,
            backend: 1,
            health_fraction: 0.8,
            can_move: 1,
            threat_visible: 1,
            ..Default::default()
        })
        .collect()
}

fn mixed_pool() -> Vec<NpcObservationAbi> {
    (0..POOL)
        .map(|i| {
            let r = mix(SEED + i as u64);
            let r2 = mix(SEED2 + i as u64);
            let pct = r % 100;
            let role: u32 = if pct < 12 {
                0
            } else if pct < 50 {
                1
            } else if pct < 62 {
                2
            } else if pct < 82 {
                3
            } else if pct < 90 {
                4
            } else {
                5
            };
            let sel = ((r >> 8) % 2) as u32;
            let backend = match role {
                0 | 1 => 0,
                2 => 1,
                3 | 5 => sel,
                _ => 1 + sel,
            };
            let can_move = if role == 0 || (r >> 40) % 100 < 10 {
                0
            } else {
                1
            };
            let disposition = if (2..=4).contains(&role) {
                match r2 % 100 {
                    0..=59 => 0,
                    60..=84 => 1,
                    _ => 2,
                }
            } else {
                0
            };
            NpcObservationAbi {
                npc_id: i as u64 + 1,
                role,
                backend,
                threat_visible: u32::from((r >> 16) % 100 < 25),
                health_fraction: ((r >> 24) % 1001) as f32 / 1000.0,
                can_move,
                disposition,
                provoked: u8::from((r2 >> 8) % 100 < 20),
                level: 1 + ((r2 >> 16) % 30) as u16,
                order: if role == 5 { ((r2 >> 24) % 4) as u8 } else { 0 },
                order_level: if role == 5 {
                    ((r2 >> 32) % 40) as u16
                } else {
                    0
                },
                ..Default::default()
            }
        })
        .collect()
}

/// Untimed pass over the whole pool: action histogram and FNV-1a hash of the decisions.
fn verify(pool: &[NpcObservationAbi]) -> ([u64; ACTIONS], u64) {
    let mut hist = [0u64; ACTIONS];
    let mut hash = 0xcbf2_9ce4_8422_2325u64;
    let mut out = vec![NpcDecisionAbi::default(); 1024];
    for chunk in pool.chunks(1024) {
        // SAFETY: `chunk` and `out` are valid, aligned, non-overlapping slices that outlive the call, and
        // `out` has room for at least `chunk.len()` decisions.
        let status = unsafe {
            gtg_npc_decide_batch(
                chunk.as_ptr(),
                chunk.len() as u32,
                out.as_mut_ptr(),
                out.len() as u32,
            )
        };
        assert_eq!(status, 0);
        for d in &out[..chunk.len()] {
            hist[d.action as usize] += 1;
            hash = (hash ^ (d.action as u64 + 1)).wrapping_mul(0x0000_0100_0000_01b3);
        }
    }
    (hist, hash)
}

fn bench(scenario: &str, pool: &[NpcObservationAbi], count: usize, step: usize) {
    let iterations = (NPC_DECISIONS_PER_REP / count).max(1000);
    let span = pool.len() - count;
    let mut out = vec![NpcDecisionAbi::default(); count];
    let mut off = 0usize;
    let mut run = |calls: usize| {
        for _ in 0..calls {
            // SAFETY: `pool[off..]` holds at least `count` observations because `off` stays below
            // `pool.len() - count`, and `out` holds exactly `count` decisions. Both are valid, aligned and
            // do not overlap.
            let status = unsafe {
                gtg_npc_decide_batch(
                    pool[off..].as_ptr(),
                    count as u32,
                    out.as_mut_ptr(),
                    count as u32,
                )
            };
            assert_eq!(status, 0);
            black_box(&out);
            off += step;
            if off >= span {
                off -= span;
            }
        }
    };
    run((iterations / 10).max(100));
    let mut samples = [0f64; REPS];
    for sample in samples.iter_mut() {
        let start = Instant::now();
        run(iterations);
        *sample = start.elapsed().as_nanos() as f64 / (count * iterations) as f64;
    }
    samples.sort_by(|a, b| a.total_cmp(b));
    let median = samples[REPS / 2];
    println!(
        "scenario={scenario} batch={count:4} iterations={iterations:7} reps={REPS} ns/npc={median:.2} min={:.2} max={:.2} npc/s={:.0}",
        samples[0],
        samples[REPS - 1],
        1e9 / median
    );
}

fn main() {
    let uniform = uniform_pool();
    let mixed = mixed_pool();
    let (hist, hash) = verify(&mixed);
    assert!(
        hist.iter().all(|&n| n > 0),
        "mixed workload must exercise every action, got {hist:?}"
    );
    for count in COUNTS {
        bench("uniform", &uniform, count, 0);
    }
    for count in COUNTS {
        bench("mixed", &mixed, count, WINDOW_STEP);
    }
    println!(
        "# mixed_actions idle={} trade={} patrol={} attack={} retreat={} follow={} hold={} refuse={} fnv1a=0x{hash:016x}",
        hist[0], hist[1], hist[2], hist[3], hist[4], hist[5], hist[6], hist[7]
    );
}
