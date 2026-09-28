# GTG NPC — foundation scaffold

Pure Rust NPC core with deterministic behavior and a stable C ABI for Unity. `npc-ml` is an intentionally minimal placeholder; no framework or online-training strategy has been chosen. The `npc-ffi` library is not yet connected to Unity. No combat, navigation, dialogue, training, or real inference is implemented.

From `rust/`: `cargo test --workspace` and `cargo build -p gtg-npc-ffi --release`.

Architecture: Unity owns world queries, movement, collision, dialogue, inventory and combat authority. Rust owns lightweight decision logic and optional future ML inference. Batch observations/actions across the ABI once profiling warrants it. Never call Unity APIs from Rust.
