# gtg-npc-core

The shared types of the NPC decision: what an NPC is, what it can be told, what it can do. No logic and no dependencies. The C layout of these types is in `gtg-npc-ffi`, and the rules that use them are in `gtg-npc-behavior`. The full contract is in `npc/observation.md`.

Version 0.0.1. Part of the workspace in `rust/Cargo.toml`, which sets the shared lints.

## Modules

### `lib.rs`

**What it does:** Defines `NpcRole` (merchant, civilian, guard, enemy, boss, companion), `DecisionBackend`, `NpcDisposition` (hostile, retaliatory, peaceful), `NpcOrder` (none, follow, hold, attack, deliver, raid), `NpcAction` (idle, trade, patrol, attack, retreat, follow, hold, refuse order, mission, betray), `MissionOutcome` (success, failed, caught, killed), the `Observation` and `Decision` structs, and `MAX_CROWN_HEAT`, the highest Crown Heat tier.

**Decisions:**
- The enums are plain Rust enums. Their numbers are decided in `gtg-npc-ffi`, not here, so the Rust types can be reordered without touching the ABI. `scripts/check_npc_enum_sync.py` compares the numbers with C# and MDIX.
- `Observation` carries only what a rule uses. Memory (provoked, level, orders) is collected by the game, and the decision keeps no state.
- `crown_heat` is the tier (0 none, 1 wanted, 2 hunted, 3 outlaw) of the region the NPC stands in. The game keeps the meter, the regions and the crimes, and sends only the tier. Zero means no heat or no information.
- Every public item has a doc comment, because the workspace lint `missing_docs` is on.
- `NpcOrder::None` shadows `Option::None` only if someone imports the variants. Use the qualified name.

## Fixes and Problems

- Version 3 added the companion role, disposition, order, provoked, level and order level. Version 4 added the order types deliver and raid, the actions mission and betray, the mission outcomes, and the betrayal inputs (trustworthiness, affinity, pay satisfaction, noise, betrayal opportunity). See `npc/observation.md`. Version 5 added `crown_heat` and `MAX_CROWN_HEAT`.
- Not tested here: the crate has no logic, so it has no tests of its own. Its types are exercised by the tests of the two crates that use them.
