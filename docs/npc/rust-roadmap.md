# NPC Rust side: what is done and what is left

Status (2026-10-05). Facts below come from the repository unless marked as a design source. Items marked **decision** need an answer from the project owner before anyone builds them.

## What exists

| Crate | Lines | Tests | What it holds |
|---|---|---|---|
| `npc-core` | 37 | 0 | `NpcRole` (5), `DecisionBackend` (3), `NpcAction` (5), `Observation`, `Decision` |
| `npc-behavior` | 113 | 5 | `decide()`: merchant state machine, civilian rules, and a utility rule for guards, enemies and bosses |
| `npc-ml` | 19 | 0 | `LearningMode`, a `Policy` trait and `PolicyError`. No model, no framework, no weights |
| `npc-ffi` | 245 | 7 | ABI version 2, `gtg_npc_decide_batch` (up to 4096 per call, atomic validation), size and version getters, and the older one-NPC stub |

An NPC decision today has seven inputs: id, role, backend, threat visible, health fraction, can move and the reserved field. It returns one of five actions. There is no memory, no schedule, no faction, no distance, no orders and no learning. The decision runs at about 5 to 10 ns per NPC on hosted runners, see `benching-standards.md`.

## What the decision cannot know yet

This is the biggest functional gap, ahead of machine learning. The utility rule for combat uses health and nothing else, because the observation carries nothing else. Missing inputs that the game design already needs:

- **Distance to the threat**, so an NPC can choose to close in, hold or kite.
- **Whether it was attacked**, and by whom. The design says some humanoids "only fight if attacked", and mobs "always attack like animals" (project notes). That is a disposition: always hostile, retaliatory, or friendly.
- **Allies and enemies nearby**, for morale and group tactics.
- **Time of day**, for routines.
- **Crown Heat and Local Favor**, which the design reference makes the basis of guard reactions (the guard who is secretly loyal to the late King, the bribable one, the one on the Uncle's payroll, and the "Do you know who I am?" outcome).
- **Orders**, for companions (below).

Each new field is an ABI change. The change process already exists: bump `ABI_VERSION`, update the C header, the C# structs and the layout tests, and the enum sync script covers the enums. The risk is doing it field by field. Better to design one observation version 3 with all of the above, with the sizes and offsets written down first.

## Backlog

| # | Item | Depends on | Notes |
|---|---|---|---|
| 1 | Crate guide conformance | nothing | `RUST_RUST_CRATE_GUIDE.md` asks for version `0.0.1` (the crates are `0.1.0`), a `[workspace.lints]` table (`unsafe_code` deny, `missing_docs` warn, clippy `undocumented_unsafe_blocks`), `#![allow(unsafe_code)]` in `npc-ffi`, and a `// SAFETY:` comment on each `unsafe` block. `npc-ffi` has a `# Safety` doc already and two unsafe blocks without comments. Per-crate docs and source notices are also missing. The guide was written for mid-engine, so **decision:** confirm it applies here |
| 2 | Observation and action version 3 | design below | Distance, attacked-by, disposition, allies, time of day, heat and favor, order. New actions are needed for companions (follow, hold, assist). Unity validates actions 0 to 4 today and must change with it |
| 3 | Disposition and faction | 2 | Mobs always hostile, humanoids hostile, retaliatory or friendly, per race and per individual. Pure rules, easy to test |
| 4 | NPC memory | 2 | Last attacker, grudges, relationship to the player, loyalty. **Decision:** keep memory in Unity as a small fixed block passed in and out of each call (Rust stays stateless, saves are Unity's job), or in Rust keyed by NPC id (needs a save format and a lifetime). The first fits the current batch ABI and keeps decisions reproducible |
| 5 | Utility AI with considerations and curves | 2 | Replaces the hard-coded `utility_combat`. Scoring tables belong in `.mdix` data |
| 6 | Combat tactics and morale | 5 | Flank, kite, hold a chokepoint, break and run. Group tactics need allies in the observation |
| 7 | Schedules and routines | 2 | Time of day to activity: work, sleep, patrol, socialize. Unity still picks where |
| 8 | Decision scheduling and level of detail | nothing | Today the director rotates through 256 NPCs per 0.1 s tick. Which NPCs deserve a decision, and how often, depends on distance to the player. Unity owns this, and the Rust side can offer a cheap "needs decision" test |
| 9 | Headless simulation (`npc-simulation`) | 2 | A small world with no Unity, for tests, balance and training data |
| 10 | Small ML policy | 9 | Tiny network with fixed weights first, batch inference, a weight file format and validation, a deterministic fallback, and a benchmark against the utility rule on the same workload. No Python anywhere |
| 11 | Selective online learning | 10 | Only for designated NPCs, with a hard time budget per update. Last in the list |
| 12 | Persistence | 4, 10 | Save and load of memory and weights |
| 13 | Robustness tests | nothing | Property tests (any input, no panic, same input same output), fuzzing the batch entry with random bytes, and a Miri pass on the unsafe code |
| 14 | Native builds | nothing | See below |

Companions have their own section next.

## Native builds

CI builds Linux x86-64 and the benchmark workflow builds Linux ARM64 and macOS arm64. Unity needs a library per target, and none is in `Assets/` yet:

- **Intel macOS** for the development MacBook Pro. `macos-latest` is arm64, so this needs a cross build. The deployment target must be set low enough for macOS Catalina, and the Rust version used by CI may have raised its own minimum, which has not been checked.
- **Windows x86-64**, **Android arm64** (the Galaxy A13), and **iOS** (a static library) if iOS is a target.
- A step that publishes the libraries as artifacts, and an import step into `Assets/Plugins/`, with the Unity import settings per platform.
- The Rust library is not touched by the Burst `Illegal instruction` abort that stopped the ECS stack, since that comes from Burst. It must not be built with `-C target-cpu=native`, because the build machine's CPU would then decide which instructions the library needs. The default x86-64 target needs only SSE2. Whether it runs on the 2010 machine is untested.
- The C header is written by hand today. Generating it (cbindgen) and the C# bindings (csbindgen) would remove a class of drift, and the enum sync script and the layout tests cover part of that already.

## Companions

What the design and project notes say:

- Companions are special NPCs who do tasks for Arthur. Bartholomew can be directed to attack enemies in combat and to permanently deal with problem NPCs in town (design reference).
- Most companions are mercenaries who take a profit split and are not loyal. Some betray Arthur. One is a "definitely not undercover" cop, Ditter. The mule companion is Clarence (project notes).
- Richard skims profits and leaks intel through the Enchanted Scroll's transaction logs, and his betrayal follows a fixed nine-step story sequence ending with Bartholomew saving Arthur (design reference).

What that means for the Rust side:

1. **Two kinds of behavior.** Task execution and loyalty drift can be decided by rules. The big betrayals are scripted story events and must not be overridden by a rule. So a companion needs a way for the story to force an action or a state, and for the rules to stand down while it does.
2. **A role and states.** A `Companion` role in `NpcRole`, plus a small set of numbers that change over time: loyalty, greed or profit expectation, suspicion that Arthur knows. That is a deterministic model with memory, a good fit for item 4 and for tests, and it needs no machine learning.
3. **Orders.** The player or the Enchanted Scroll gives an order (follow, hold, attack this target, deal with this NPC, guard this place, haul or fetch). Unity keeps the task queue and the execution, and Rust receives the current order in the observation and returns the action. New actions are needed: follow, hold position, assist.
4. **Pay and profit split.** Whether a companion stays, complains, skims or leaves depends on money events in the economy. The economy is not built, so the model should take a small "pay satisfaction" input that Unity computes.
5. **Disobedience.** A mercenary who refuses an order, or does it badly, is a design choice. **Decision:** may companions refuse orders, and is betrayal ever emergent, or only scripted?
6. **A mule is not a fighter.** Clarence needs carrying and following, and no combat at all. The role set may need a non-combatant follower, or `can_move` and an order set that excludes attacks.
7. **Combat help.** Companions are also combatants, so they use the same combat rules as other NPCs, with their own threat sources (enemies, not the player). The threat source in the Managed code treats everything registered as a threat to every NPC today. Companions need factions for that to work, which is item 3.

Not done and not guessed: the task list for the demo, who the companions are beyond the names above, and whether companions are part of the first demo at all.

## Suggested order

1. Crate guide conformance (item 1), after the owner confirms the guide applies. Small and safe.
2. One design pass for observation and action version 3, written as a table of fields, sizes and offsets before any code (items 2 and 3, with the companion order field).
3. Memory model decision, then memory and companions (items 4 and the companion section).
4. Native builds, in parallel, because nothing runs in Unity on the target devices without them.
5. Utility AI, tactics and schedules (items 5 to 7).
6. Simulation, then the small ML policy (items 9 and 10). The utility rule is the baseline the policy must beat.
