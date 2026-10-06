# gtg-npc-core

The shared types of the NPC decision: what an NPC is, what it can be told, what it can do. No logic and no dependencies. The C layout of these types is in `gtg-npc-ffi`, and the rules that use them are in `gtg-npc-behavior`. The full contract is in `npc/observation-v3.md`.

Version 0.0.1. Part of the workspace in `rust/Cargo.toml`, which sets the shared lints.

## Modules

### `lib.rs`

**What it does:** Defines `NpcRole` (merchant, civilian, guard, enemy, boss, companion), `DecisionBackend`, `NpcDisposition` (hostile, retaliatory, peaceful), `NpcOrder` (none, follow, hold, attack), `NpcAction` (idle, trade, patrol, attack, retreat, follow, hold, refuse order), and the `Observation` and `Decision` structs.

**Decisions:**
- The enums are plain Rust enums. Their numbers are decided in `gtg-npc-ffi`, not here, so the Rust types can be reordered without touching the ABI. `scripts/check_npc_enum_sync.py` compares the numbers with C# and MDIX.
- `Observation` carries only what a rule uses. Memory (provoked, level, orders) is collected by the game, and the decision keeps no state.
- Every public item has a doc comment, because the workspace lint `missing_docs` is on.
- `NpcOrder::None` shadows `Option::None` only if someone imports the variants. Use the qualified name.

## Fixes and Problems

- Version 3 added the companion role, disposition, order, provoked, level and order level. See `npc/observation-v3.md`.
- Not tested here: the crate has no logic, so it has no tests of its own. Its types are exercised by the tests of the two crates that use them.
