# gtg-npc-core

The shared types of the NPC decision: what an NPC is, what it can be told, what it can do. No logic and no dependencies. The C layout of these types is in `gtg-npc-ffi`, and the rules that use them are in `gtg-npc-behavior`. The full contract is in `npc/observation.md`.

Version 0.0.1. Part of the workspace in `rust/Cargo.toml`, which sets the shared lints.

## Modules

### `lib.rs`

**What it does:** Defines `NpcRole` (merchant, civilian, guard, enemy, boss, companion), `DecisionBackend`, `NpcDisposition` (hostile, retaliatory, peaceful), `NpcOrder` (none, follow, hold, attack, deliver, raid), `AttackStyle` (melee, ranged, hybrid), `NpcAction` (idle, trade, patrol, attack, retreat, follow, hold, refuse order, mission, betray, shoot, kite), `MissionOutcome` (success, failed, caught, killed), the `Observation` and `Decision` structs, and `MAX_CROWN_HEAT`, the highest Crown Heat tier.

**Decisions:**
- The enums are plain Rust enums. Their numbers are decided in `gtg-npc-ffi`, not here, so the Rust types can be reordered without touching the ABI. `scripts/check_npc_enum_sync.py` compares the numbers with C# and MDIX.
- `Observation` carries only what a rule uses. Memory (provoked, level, orders) is collected by the game, and the decision keeps no state.
- `favor` (version 7) says how much the NPC likes the player, from -1 to 1. Zero is neutral and is what an old caller sends. The decision reads it for shopkeepers and civilians only.
- `attack_style`, `ranged_ready`, `ranged_stance` and `threat_distance` (version 6) say how the NPC fights, whether it has something to shoot, whether it was shooting or backing away when it last fought, and how far the threat is. A melee fighter ignores the last three. Distance zero or less means unknown. The game keeps the stance memory, not the decision.
- `crown_heat` is the tier (0 none, 1 wanted, 2 hunted, 3 outlaw) of the region the NPC stands in. The game keeps the meter, the regions and the crimes, and sends only the tier. Zero means no heat or no information.
- Every public item has a doc comment, because the workspace lint `missing_docs` is on.
- `NpcOrder::None` shadows `Option::None` only if someone imports the variants. Use the qualified name.

## Fixes and Problems

- Version 3 added the companion role, disposition, order, provoked, level and order level. Version 4 added the order types deliver and raid, the actions mission and betray, the mission outcomes, and the betrayal inputs (trustworthiness, affinity, pay satisfaction, noise, betrayal opportunity). See `npc/observation.md`. Version 5 added `crown_heat` and `MAX_CROWN_HEAT`. Version 6 added `AttackStyle`, the actions shoot and kite, and the four fields above. Version 7 added `favor`, how much the NPC likes the player from -1 to 1 (0 neutral), which only shopkeepers and civilians read.
- Not tested here: the crate has no logic, so it has no tests of its own. Its types are exercised by the tests of the two crates that use them.
