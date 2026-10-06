# NPC Rust side: what is done and what is left

Status (2026-10-05, after observation version 3). Facts below come from the repository unless marked as a design source or an owner decision. Items marked **decision** need an answer from the project owner before anyone builds them.

## What exists

| Crate | Tests | What it holds |
|---|---:|---|
| `npc-core` | 0 | `NpcRole` (6), `DecisionBackend` (3), `NpcDisposition` (3), `NpcOrder` (4), `NpcAction` (8), `Observation`, `Decision` |
| `npc-behavior` | 20 | `decide()`: merchant, civilian, combatant (guard, enemy, boss) and companion rules, disposition, orders, the level refusal |
| `npc-ml` | 0 | `LearningMode`, a `Policy` trait and `PolicyError`. No model, no framework, no weights |
| `npc-ffi` | 19 | ABI version 3, `gtg_npc_decide_batch` (up to 4096 per call, atomic validation, overlap rejection), size and version getters, and the older one-NPC stub |

Every crate follows `RUST_RUST_CRATE_GUIDE.md` now: version 0.0.1, the workspace lint table, `SAFETY` comments on every `unsafe` block, a source notice and a doc per crate. The decision has 11 inputs (see `observation-v3.md`) and returns one of eight actions. It runs at about 5 to 10 ns per NPC on hosted runners with the version 2 layout. The version 3 cost is not measured.

## Decided by the owner

- The crate guide applies to the GTG Rust crates.
- NPC data lives in Unity. Rust is given a snapshot and returns an action, and keeps no state. Data that is completely static and predictable can live in Rust in a hash map or a `match`.
- A companion has a level. It refuses an order that is far above its level.
- Betrayals are both scripted and emergent. Some are scripted story events, others arise from the companion's state.
- A companion can be caught or killed on a raid.
- Native plugin builds wait, because the development charger is out. Work continues without them.

## What the decision still cannot know

- **Distance to the threat**, so an NPC can choose to close in, hold or kite. It needs rules and actions that act on range.
- **Allies and enemies nearby**, for morale and group tactics.
- **Time of day**, for routines.
- **Crown Heat and Local Favor**, which the design reference makes the basis of guard reactions (the guard who is secretly loyal to the late King, the bribable one, the one on the Uncle's payroll, and the "Do you know who I am?" outcome).
- **Loyalty and pay**, for emergent betrayal.
- **A random number**, for a chance in a rule that stays reproducible. Unity would supply it.

These are the planned uses of the reserved space in the observation. Adding any of them changes the ABI version.

## Backlog

| # | Item | Status | Notes |
|---|---|---|---|
| 1 | Crate guide conformance | done | Clippy has not run on it yet |
| 2 | Observation and action version 3 | done for disposition, provoked, level, order | Distance, allies, time, heat and favor, loyalty and noise are reserved, not built |
| 3 | Disposition | done | Hostile, retaliatory, peaceful. "Good humanoids never fight back" is an interpretation to confirm |
| 4 | NPC memory | decided | In Unity. The Managed brain holds level, order, provoked and the last attacker. Persistence (save and load) is not built |
| 5 | Utility AI with considerations and curves | open | Replaces the hard-coded `utility_combat`. Scoring tables belong in `.mdix` data |
| 6 | Combat tactics and morale | open | Flank, kite, hold a chokepoint, break and run. Needs allies and distance in the observation |
| 7 | Schedules and routines | open | Time of day to activity. Unity still picks where |
| 8 | Decision scheduling and level of detail | open | The director rotates through 256 NPCs per 0.1 s tick. Which NPCs deserve a decision, and how often, depends on distance to the player. Unity owns it |
| 9 | Headless simulation (`npc-simulation`) | open | A small world with no Unity, for tests and balance |
| 10 | Small ML policy | open | Tiny network with fixed weights first, batch inference, a deterministic fallback, a benchmark against the utility rules. No Python |
| 11 | Selective online learning | open | Designated NPCs only, with a hard time budget per update |
| 12 | Persistence | open | Save and load of NPC memory and weights, in Unity |
| 13 | Robustness tests | open | Property tests, fuzzing the batch entry with random bytes, a Miri pass on the unsafe code |
| 14 | Native builds | paused | See below |
| 15 | Factions | open | Which sources are threats to which NPC. Companions need it before they can fight anything |
| 16 | Emergent betrayal rule | open, needs input | See the companion section |
| 17 | Generated C header and C# bindings | open | The header is written by hand today, and tests cover the layout |

## Native builds

Paused until the charger is back. What is needed:

- **Intel macOS** for the development MacBook Pro. `macos-latest` is arm64, so this needs a cross build. The deployment target must be set low enough for macOS Catalina, and the Rust version used by CI may have raised its own minimum, which has not been checked.
- **Windows x86-64**, **Android arm64** (the Galaxy A13), and **iOS** (a static library) if iOS is a target.
- A step that publishes the libraries as artifacts, and an import step into `Assets/Plugins/`, with the Unity import settings per platform.
- The library must not be built with `-C target-cpu=native`, because the build machine's CPU would then decide which instructions it needs. The default x86-64 target needs only SSE2. The Burst `Illegal instruction` abort that stopped the ECS stack comes from Burst code generation and does not touch this library. Whether it runs on the 2010 machine is untested.
- Until then Unity uses the managed fallback, which the C# test proves equal to the Rust decision on 16,384 NPCs.

## Companions

What the design and project notes say:

- Companions are special NPCs who do tasks for Arthur. Bartholomew can be directed to attack enemies in combat and to permanently deal with problem NPCs in town (design reference).
- Most companions are mercenaries who take a profit split and are not loyal. Some betray Arthur. One is a "definitely not undercover" cop, Ditter. The mule companion is Clarence (project notes).
- Richard skims profits and leaks intel through the Enchanted Scroll's transaction logs, and his betrayal follows a fixed nine-step story sequence ending with Bartholomew saving Arthur (design reference).
- Owner answers (2026-10-05): companions have levels and refuse an order that is far above their level. Betrayals are both scripted and emergent. A companion can be caught or killed on a raid.

What is built:

- Role `Companion`, orders follow, hold and attack, the level refusal (5 levels above its own is accepted, 6 is refused), and the actions follow, hold and refuse order. A companion without an order follows its leader.
- In Unity: `ManagedNpcBrain` holds level, order, leader and the refusal event, and `ManagedNpcActor` walks to the leader, holds, and stands still on a refusal.

What is not built, and why:

1. **Faction handling.** Only the player is a threat source, and a companion never sees its leader as one, so a companion sees no enemy at all. Companion combat waits for item 15.
2. **The refusal gap.** "Way above their level" is a placeholder of 5 levels. **Decision:** the real rule. It could be a fixed gap, a ratio, or depend on the order type.
3. **Emergent betrayal.** It needs loyalty and an opportunity as inputs, and a rule that turns them into a betrayal. **Decision:** which conditions cause one. The pay model and the economy are not built, so a "pay satisfaction" input from Unity is the likely shape. A scripted betrayal is a story event and must be able to override the rules.
4. **Missions and raids.** A raid is a mission that the game resolves, and the companion may be caught or killed. That is a game event, not an action choice. If the resolution should be deterministic and testable it can be a separate Rust export that takes the companion's level, the mission level, equipment and a random number from Unity. **Decision:** whether raids are resolved off screen with a formula or played out.
5. **Task execution beyond follow, hold and attack.** "Deal with a problem NPC", guard a place, haul or fetch need new orders and the systems behind them (shops, inventory, the economy).
6. **A mule is not a fighter.** Clarence needs carrying and following, and no combat. A non-combatant companion role, or an order set without attack, may be needed.
7. **Pay and profit split** feed loyalty and are not built.

## Suggested order

1. Factions (item 15), so companions and enemies can see each other, then a first companion scene in Unity.
2. The betrayal and refusal decisions above, then the loyalty fields in version 4.
3. Memory persistence and the companion mission resolution, both in Unity, with the mission formula in Rust if wanted.
4. Utility AI, tactics and schedules (items 5 to 7).
5. Native builds, when the charger is back.
6. Simulation, then the small ML policy (items 9 and 10). The utility rules are the baseline the policy must beat.
