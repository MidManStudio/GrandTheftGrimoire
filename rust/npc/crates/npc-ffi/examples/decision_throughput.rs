use gtg_npc_ffi::{gtg_npc_decide_batch, NpcDecisionAbi, NpcObservationAbi};
use std::time::Instant;
fn main() {
    for count in [10usize, 100, 500, 1000] {
        let inputs = vec![NpcObservationAbi { role:3,backend:1,health_fraction:0.8,can_move:1,threat_visible:1,..Default::default() }; count];
        let mut outputs = vec![NpcDecisionAbi::default();count];
        let iterations = (1_000_000 / count).max(1000);
        let start=Instant::now();
        for _ in 0..iterations {
            let status=unsafe {gtg_npc_decide_batch(inputs.as_ptr(),count as u32,outputs.as_mut_ptr(),count as u32)};
            assert_eq!(status,0);
            std::hint::black_box(&outputs);
        }
        let elapsed=start.elapsed();
        println!("batch={count:4} iterations={iterations:6} ns/npc={:.1} npc/s={:.0}",elapsed.as_nanos() as f64 / (count*iterations) as f64, (count*iterations) as f64 / elapsed.as_secs_f64());
    }
}
