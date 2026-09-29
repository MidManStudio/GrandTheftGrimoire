# NPC Phase 1: batched decision runtime

The Rust FFI uses `gtg_npc_decide_batch` ABI v2, with 32-byte observation and 16-byte decision structs. Rust validates every observation before writing any output; `count` is capped at 4096. Both buffers belong to the caller. Do not pass overlapping buffers. No Rust-owned memory crosses the boundary.

Unity `NPCDecisionSystem` currently processes up to 256 NPCs every 0.1 seconds, rotating through the entity query when more are present. The managed fallback produces equivalent actions when no native library is installed, but **does not benchmark native FFI**. The action component is an intent only: navigation, animations, combat and trade execution are not yet implemented. A scene and actual device profiling are still needed.

Roles: 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss. Backends: 0 FSM, 1 utility, 2 hybrid ML (currently deterministic fallback). Actions: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat. The merchant never patrols. Existing `Assets/game_enemies.mdix` AIType remains unchanged.

## Build and verify

From `rust/`: `cargo fmt --all --check`, `cargo test --workspace`, and `cargo run -p gtg-npc-ffi --release --example decision_throughput`. In CI, `npc-rust-ci.yml` runs the correctness checks on every push and `npc-rust-ffi-bench.yml` runs the benchmarks on demand; see `benching-standards.md`. Native binaries are not shipped by this bundle. Build `gtg-npc-ffi` as a cdylib for each target, import into Unity's plugin locations and check ABI version 2. Unity integration is unverified until a scene and platform binaries are available.

## Replacements packaging

Only ordinary repo-relative files are in the payload tarball. Keep `replacements.mdix` separately in `.mdix/replacements/`; existing GTG workflow is sufficient. Use `dry_run=true` and `delete_processed_archives=false` first. No dot-prefixed paths are bundled.

## Status (2026-09-29)

- **Verified in CI (Rust and C only):** the Rust workspace passes format, tests and Clippy; the real C ABI smoke test passes; ABI v2 struct sizes and offsets match. Hosted `ubuntu-latest` batch results were about 5 ns/NPC at batch sizes 10, 100, 500 and 1000, with identical input for every NPC (best case).
- **Not verified:** the Unity ECS loop (observation gathering, batch call, applying decisions), the C# struct layout against ABI v2, Android/macOS native builds, and any device timing.
- **Next:** confirm the Unity side against ABI v2, add a mixed-role benchmark workload, then NPC memory and decision scheduling.
