//! Throughput of the batched decision ABI, called directly from Rust.
//!
//! Two scenarios, both through `gtg_npc_decide_batch`:
//! - `uniform`: every NPC has identical input (best case for branch prediction and caches).
//! - `mixed`: a deterministic town-like population (15% merchants, 45% civilians, 15% guards,
//!   20% enemies, 5% bosses; ~25% see a threat; random health; ~10% of non-merchants immobile).
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
            let pct = r % 100;
            let role: u32 = if pct < 15 {
                0
            } else if pct < 60 {
                1
            } else if pct < 75 {
                2
            } else if pct < 95 {
                3
            } else {
                4
            };
            let sel = ((r >> 8) % 2) as u32;
            let backend = match role {
                0 | 1 => 0,
                2 => 1,
                3 => sel,
                _ => 1 + sel,
            };
            let can_move = if role == 0 || (r >> 40) % 100 < 10 {
                0
            } else {
                1
            };
            NpcObservationAbi {
                npc_id: i as u64 + 1,
                role,
                backend,
                threat_visible: u32::from((r >> 16) % 100 < 25),
                health_fraction: ((r >> 24) % 1001) as f32 / 1000.0,
                can_move,
                ..Default::default()
            }
        })
        .collect()
}

/// Untimed pass over the whole pool: action histogram and FNV-1a hash of the decisions.
fn verify(pool: &[NpcObservationAbi]) -> ([u64; 5], u64) {
    let mut hist = [0u64; 5];
    let mut hash = 0xcbf2_9ce4_8422_2325u64;
    let mut out = vec![NpcDecisionAbi::default(); 1024];
    for chunk in pool.chunks(1024) {
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
        "# mixed_actions idle={} trade={} patrol={} attack={} retreat={} fnv1a=0x{hash:016x}",
        hist[0], hist[1], hist[2], hist[3], hist[4]
    );
}
