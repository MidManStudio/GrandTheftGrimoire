# NPC Phase 1: batched decision runtime

The Rust FFI uses `gtg_npc_decide_batch` ABI v3, with 64-byte observation and 16-byte decision structs. Rust validates every observation before writing any output; `count` is capped at 4096. Both buffers belong to the caller. Do not pass overlapping buffers. No Rust-owned memory crosses the boundary.

The decision loop runs on two stacks. The ECS `NPCDecisionSystem` and the Managed `ManagedNpcDirector` each process up to 256 NPCs every 0.1 seconds, rotating through the population when more are present. The managed fallback produces equivalent actions when no native library is installed, but **does not benchmark native FFI**. The action component is an intent only: navigation, animations, combat and trade execution are not yet implemented. A scene and actual device profiling are still needed.

Roles: 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss. Backends: 0 FSM, 1 utility, 2 hybrid ML (currently deterministic fallback). Actions: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat. The merchant never patrols. Existing `Assets/game_enemies.mdix` AIType remains unchanged.

## Build and verify

From `rust/`: `cargo fmt --all --check`, `cargo test --workspace`, and `cargo run -p gtg-npc-ffi --release --example decision_throughput`. In CI, `npc-rust-ci.yml` runs the correctness checks on every push and `npc-rust-ffi-bench.yml` runs the benchmarks on demand; see `benching-standards.md`. Native binaries are not shipped by this bundle. Build `gtg-npc-ffi` as a cdylib for each target, import into Unity's plugin locations and check ABI version 2. Unity integration is unverified until a scene and platform binaries are available.

## Replacements packaging

Only ordinary repo-relative files are in the payload tarball. Keep `replacements.mdix` separately in `.mdix/replacements/`; existing GTG workflow is sufficient. Use `dry_run=true` and `delete_processed_archives=false` first. No dot-prefixed paths are bundled.

## Status (2026-09-29)

- **Verified in CI (Rust and C):** format, tests and Clippy; the real C ABI smoke test; ABI v2 struct sizes and offsets. Hosted `ubuntu-latest` batch results were about 5 ns/NPC at batch sizes 10 to 1000 with identical input for every NPC (best case).
- **Bench run #4 (2026-09-29):** all three legs passed (`ubuntu-latest` x86-64, `ubuntu-24.04-arm`, `macos-latest` Apple M1 virtual), each with the C smoke test. C-through-FFI cost was about 5.7 to 9.9 ns/NPC on every runner. That run used the earlier single-window benchmark; details and caveats are in `benching-standards.md`.
- **Added, awaiting its first CI run:** a `mixed` benchmark scenario with warm-up and median-of-9 timing in the Rust and C benchmarks, and the `csharp-abi` job that runs the Unity-side C# native layer against the real library. Both were run in the authoring sandbox (Rust 1.85, .NET 8) and passed, including negative controls; they have not run on GitHub yet.
- **Verified by reading:** the C# structs match `npc-ffi` byte for byte (also checked at runtime by the ABI test once CI runs).
- **Not verified:** a real Unity build, the Unity ECS loop, native plugin packaging for any Unity target, Android/macOS builds, and any device timing.
- **Managed stack (2026-10-04):** health, line of sight, brain and director are written and pass 52 checks against Unity stubs on both the managed fallback and the real Rust library. Not compiled by Unity, not run in a scene. CI job `managed-npc` runs the same checks on every push.
- **Observation version 3 (2026-10-05):** ABI version 3 grew the observation to 64 bytes and added the companion role, disposition, order, provoked, level and order level, with new actions follow, hold and refuse order. Rust, C and C# agree on the same 16,384 mixed NPCs, and the mixed workload uses all eight actions. 20 behavior tests, 19 FFI tests, a C smoke test, a C# ABI test and 208 Managed checks pass in the sandbox, and 44 deliberate breaks were caught across the Rust, C# and Unity-stub tests. See `observation-v3.md`. Not run on GitHub yet.
- **Crate guide conformance (2026-10-05):** crate versions 0.0.1, workspace lints, `SAFETY` comments, notices and one doc per crate (`../gtg-npc-core.md` and the others). Clippy has not run.
- **Managed actor, fireball damage and debugger (2026-10-05):** `ManagedNpcActor` carries out Patrol, Attack (chase and melee) and Retreat with a `NavMeshAgent`, the fireball damages anything with a `ManagedHealth`, and an editor window draws movement and sight. 156 checks pass against Unity stubs, 23 deliberate breaks were caught. Not compiled by Unity, not run on a NavMesh.
- **Known open ends:** the ECS stack acts on nothing; no NavMesh is baked in any scene; Trade has no shop; ice and the hazards do not hurt; no native plugin is in `Assets/`, so Unity would use the managed fallback today; no Intel macOS build exists for the development machine. Details are in `unity-integration.md`, `navigation.md` and `rust-roadmap.md`.
- **Next:** open the project in Unity and fix whatever the compiler finds, bake a NavMesh and try the loop, then the native plugin builds, then the observation version 3 design on the Rust roadmap.

## Enum use in C# (2026-09-29)

`NPCIdentity`, `NPCDecision` and `NPCAuthoring` now use `NpcRole`, `DecisionBackend`, `LearningMode` and `NpcAction` from `Components/NPCEnums.cs` instead of raw integers. The native structs keep raw integers. `NPCDecisionSystem` converts at that boundary. The managed fallback now lives in `Native/NPCManagedFallback.cs` (no Unity dependencies) so the C# ABI test can compare it with the native library. Behavior is intended to be unchanged, and the ABI test checks that on 16384 NPCs.
