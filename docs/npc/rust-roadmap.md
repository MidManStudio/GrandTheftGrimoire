# NPC Rust side: what is done and what is left

Status (2026-10-06, after observation version 4). Facts below come from the repository unless marked as a design source or an owner decision. Items marked **decision** need an answer from the project owner before anyone builds them.

## What exists

| Crate | Tests | What it holds |
|---|---:|---|
| `npc-core` | 0 | `NpcRole` (6), `DecisionBackend` (3), `NpcDisposition` (3), `NpcOrder` (6), `NpcAction` (10), `MissionOutcome` (4), `Observation`, `Decision` |
| `npc-behavior` | 40 | `decide()`: merchant, civilian, combatant (guard, enemy, boss) and companion rules, disposition, orders, the per-type refusal gap, emergent betrayal, and `resolve_mission` |
| `npc-ml` | 0 | `LearningMode`, a `Policy` trait and `PolicyError`. No model, no framework, no weights |
| `npc-ffi` | 23 | ABI version 4, `gtg_npc_decide_batch` (up to 4096 per call, atomic validation, overlap rejection), `gtg_npc_resolve_mission`, size and version getters, and the older one-NPC stub |

Every crate follows `RUST_RUST_CRATE_GUIDE.md` now: version 0.0.1, the workspace lint table, `SAFETY` comments on every `unsafe` block, a source notice and a doc per crate. The decision has 16 inputs (see `observation.md`) and returns one of ten actions. It ran at about 5 to 10 ns per NPC on hosted runners with the version 2 layout. The version 3 and 4 cost is not measured.

## Decided by the owner

- The crate guide applies to the GTG Rust crates.
- NPC data lives in Unity. Rust is given a snapshot and returns an action, and keeps no state. Data that is completely static and predictable can live in Rust in a hash map or a `match`.
- A companion has a level. It refuses an order that is above its level by more than a gap that depends on the order type and its level (a raid needs a high level, a delivery tolerates more).
- Companion data that weighs on betrayal: trustworthiness, how much the companion likes Arthur, and pay satisfaction. Unity supplies pay satisfaction and an opportunity flag, and the story can force scripted betrayals.
- A mission the player joins is played out. Otherwise it happens off screen, and the player gets a notification and rewards if it goes well.
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
| 2 | Observation and action versions 3 and 4 | done for disposition, provoked, level, orders, betrayal inputs, missions | Distance, allies, time, heat and favor are not built |
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
| 16 | Emergent betrayal rule | done, placeholder numbers | Loyalty weights 0.4 / 0.3 / 0.3, ceiling 0.5, maximum chance 0.5. Needs balance and the pay model |
| 18 | Mission scheduling, notification, rewards, capture and rescue | open | Unity systems around `ManagedMissionResolver` |
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

- Role `Companion`, orders follow, hold, attack, deliver and raid, a refusal gap per order type (raid 1, attack 3, follow and hold 5, deliver 6), the actions follow, hold, refuse order and mission, emergent betrayal from loyalty (trust, liking, pay), a roll and an opportunity, and `gtg_npc_resolve_mission` for off-screen missions.
- In Unity: `ManagedNpcBrain` holds level, order, leader, the loyalty numbers, the opportunity and the events `OrderRefused` and `Betrayed`, `ManagedNpcActor` walks to the leader, holds, and stands still on a refusal, a mission or a betrayal, and `ManagedMissionResolver` resolves a mission with the native library or the managed copy.

What is not built, and why:

1. **Faction handling.** Only the player is a threat source, and a companion never sees its leader as one, so a companion sees no enemy at all. Companion combat waits for item 15.
2. **The numbers.** The refusal gaps, the loyalty weights, the betrayal ceiling and chance, and the mission profiles are placeholders. **Decision:** confirm the direction of the gaps (dangerous orders allow little, safe ones allow more) and the weights.
3. **Scripted betrayal** is a story event that must be able to override the rules. Unity changes the companion directly, and never sets the opportunity for a companion the story protects.
4. **Mission systems.** Scheduling, the notification, rewards, capture and rescue of a caught companion, and the played-out raid are Unity systems and are not built.
5. **Task execution beyond follow, hold, attack, deliver and raid.** "Deal with a problem NPC", guard a place, haul or fetch need new orders and the systems behind them (shops, inventory, the economy).
6. **A mule is not a fighter.** Clarence needs carrying and following, and no combat. A non-combatant companion role, or an order set without attack, may be needed.
7. **Pay and profit split** feed pay satisfaction and are not built.

## Suggested order

1. Factions (item 15), so companions and enemies can see each other, then a first companion scene in Unity.
2. Confirm the placeholder numbers above, then a first companion scene in Unity with orders, refusals, a mission and a betrayal.
3. Memory persistence and the mission systems (scheduling, notification, rewards, capture), all in Unity.
4. Utility AI, tactics and schedules (items 5 to 7).
5. Native builds, when the charger is back.
6. Simulation, then the small ML policy (items 9 and 10). The utility rules are the baseline the policy must beat.
