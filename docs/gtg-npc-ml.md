# gtg-npc-ml

The boundary for a future learned policy. Nothing is implemented: no framework, no weights, no training. Version 0.0.1.

## Modules

### `lib.rs`

**What it does:** Defines `LearningMode` (disabled, fixed weights, online updates), the `Policy` trait (observation vector in, action scores out), and `PolicyError`.

**Decisions:**
- The deterministic rules in `gtg-npc-behavior` stay the fallback and the baseline. A learned policy must beat them on the same workload to be worth enabling, and it must never be the only path.
- The backend `HybridMl` already exists in the ABI and uses the utility rules until a policy exists.
- `LearningMode` is authoring data. Nothing reads it, and it is not an ABI field.
- No Python anywhere in training or inference. The framework choice is open: a small hand-written inference loop, or a Rust-native framework, decided by a benchmark on the target devices.

## Fixes and Problems

- Not done: any model, any batch inference, any weight file format, any online update. See the backlog in `npc/rust-roadmap.md`.
- No tests, because there is no behavior yet.
