# NPC observation contract (ABI version 4)

Status (2026-10-06): implemented in Rust, C, the C# bridge and the Managed NPC code, and checked by tests. Not run inside Unity, not run on GitHub. The ABI version is 4. This page replaces `observation-v3.md`.

This is the contract between the game and the Rust decision. It lists every field, what it means, how it is validated, and what each rule does with it. Read it before adding a field.

## Principles

1. **The game owns the data, Rust owns the choice.** NPC memory, levels, orders, loyalty and the economy live in Unity and are saved by Unity (owner decision, 2026-10-05). Rust receives a small snapshot with every decision and returns an action. The decision is a pure function of that snapshot: the same observation always gives the same action, and Rust keeps no state. A chance in a rule uses a random number that Unity supplies in the observation (`noise`), so a run stays reproducible and testable. Static, predictable tables, such as the refusal gaps and the mission profiles, live in Rust.
2. **Zero means no information.** Every field added after version 2 has a safe default of zero: hostile, no order, not provoked, level 0, and no betrayal opportunity. A caller that sets only older fields behaves as before, and tests check it.
3. **Reserved space must be zero.** A non-zero reserved field is rejected, so a caller built for a later layout fails loudly instead of being half understood.
4. **Add a field only when a rule uses it.**
5. **A batch is all or nothing.** Every observation is validated before any output is written.

## Layout (observation, 64 bytes, 8-byte alignment)

| Offset | Size | Field | Type | Meaning |
|---:|---:|---|---|---|
| 0 | 8 | `npc_id` | u64 | Identifies the NPC. Echoed in the decision |
| 8 | 4 | `role` | u32 | 0 merchant, 1 civilian, 2 guard, 3 enemy, 4 boss, 5 companion |
| 12 | 4 | `backend` | u32 | 0 state machine, 1 utility, 2 hybrid ML (uses the utility rules until a model exists) |
| 16 | 4 | `threat_visible` | u32 | 1 when a threat is in view, else 0 |
| 20 | 4 | `health_fraction` | f32 | 0 to 1, must be finite. Clamped on input |
| 24 | 4 | `can_move` | u32 | 1 when the NPC may move, else 0 |
| 28 | 4 | `reserved` | u32 | Must be 0 (version 2 field) |
| 32 | 1 | `disposition` | u8 | 0 hostile, 1 retaliatory, 2 peaceful |
| 33 | 1 | `order` | u8 | 0 none, 1 follow, 2 hold, 3 attack, 4 deliver, 5 raid. Companions only |
| 34 | 1 | `provoked` | u8 | 1 when the NPC was attacked recently, else 0 |
| 35 | 1 | `reserved_byte` | u8 | Must be 0 |
| 36 | 2 | `level` | u16 | The NPC's level |
| 38 | 2 | `order_level` | u16 | How hard the current order is, as a level. 0 for an order with no difficulty |
| 40 | 4 | `trustworthiness` | f32 | 0 to 1, must be finite, clamped. Companions, betrayal only |
| 44 | 4 | `affinity` | f32 | How much the companion likes the player. Same rules |
| 48 | 4 | `pay_satisfaction` | f32 | How satisfied it is with its pay. Same rules |
| 52 | 4 | `noise` | u32 | A random number from the game, one per decision |
| 56 | 1 | `betrayal_opportunity` | u8 | 1 when the companion could betray the player now, else 0 |
| 57 | 1 | `reserved_a` | u8 | Must be 0 |
| 58 | 2 | `reserved_b` | u16 | Must be 0 |
| 60 | 4 | `reserved_c` | u32 | Must be 0. Room for a later field |

Decision (16 bytes): `npc_id` u64, `action` i32, `reserved` u32 (always 0).

Actions: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat, 5 follow, 6 hold, 7 refuse order, 8 mission, 9 betray.

Mission outcomes (from `gtg_npc_resolve_mission`): 0 success, 1 failed, 2 caught, 3 killed.

The numbers come from `npc-ffi`, the C header `gtg_npc.h`, the C# enums and structs, and the enums in `archetypes.mdix`. `scripts/check_npc_enum_sync.py` fails CI when the enum values differ, and the C smoke test, the Rust tests and the C# test check sizes and offsets.

Validation (`STATUS_INVALID` for the whole batch, nothing written): an unknown role, backend, disposition or order; `threat_visible`, `can_move`, `provoked` or `betrayal_opportunity` above 1; a non-finite health, trustworthiness, affinity or pay satisfaction; any non-zero reserved field; input and output buffers that overlap. A count above 4096, or an output smaller than the input, is `STATUS_CAPACITY`. A null or misaligned pointer is `STATUS_NULL`.

## What each rule does

### Merchant and civilian

A merchant trades, and stands still when a threat is in view. A civilian runs from a threat when it can move, and otherwise walks around or stands. Disposition, orders, levels and the companion inputs do nothing for these roles.

### Guard, enemy and boss: disposition

With no threat in view the NPC walks around (or stands, when it cannot move). With a threat in view: hostile fights, retaliatory ignores the threat until `provoked` is set and then fights, and peaceful never fights, running when it can move and standing when it cannot, even when provoked.

Fighting means: retreat when health is below 0.2 and the NPC can move, otherwise attack for the state machine backend, and for the utility and hybrid backends compare an attack score (0.6 + 0.4 x health) with a retreat score (1 - health) and take the higher.

The three dispositions are an interpretation of the project notes. "Good" humanoids are read as peaceful, so they never fight back. If they should defend themselves, that is one more case in `combatant()`.

### Companion

A companion decides in this order:

1. **Betrayal.** Only with `betrayal_opportunity` set. The loyalty is `0.4 x trustworthiness + 0.3 x affinity + 0.3 x pay_satisfaction`. At loyalty 0.5 or more there is no betrayal. Below it, the chance rises linearly to 0.5 at loyalty 0. The roll is the top 24 bits of `noise` divided by 2^24, and the companion betrays when the roll is below the chance. A betrayal beats every order and every refusal. The weights, the ceiling and the maximum chance are placeholders.
2. **Refusal.** An order other than none is refused when its level is above the companion's level plus the gap for that order type. The check uses a saturating addition, so a level near 65535 cannot overflow. An order exactly at the gap is accepted.

| Order | Gap (levels above its own that still count as acceptable) | Why |
|---|---:|---|
| Raid | 1 | Dangerous: the companion needs to be close to the required level |
| Attack | 3 | Fighting alongside the player |
| Follow, Hold | 5 | No difficulty, so the gap rarely matters |
| Deliver | 6 | Low stakes: a larger gap is tolerated |

The gaps are placeholders. The owner said the rule depends on the order type and level, with raids needing a high level and deliveries tolerating more. The direction (dangerous orders allow little, safe ones allow more) is my reading and needs confirming.

3. **The accepted order.**

| Order | Behavior |
|---|---|
| None | Same as follow |
| Follow | Follow the leader. Fight only when a threat is in view and the companion was provoked |
| Hold | Stay put, whatever happens, even when attacked |
| Attack | With a threat in view, fight, otherwise follow the leader |
| Raid | With a threat in view, fight, otherwise carry out the mission |
| Deliver | Fight only when a threat is in view and the companion was provoked, otherwise carry out the mission |

Fighting runs at health below 0.2 when the companion can move. A companion that cannot move stands still instead of following or carrying out a mission. Other roles ignore the order fields.

The refusal is a one-shot result: the game clears the order when it receives action 7, raises an event, and the next decision is made without an order. The same goes for betrayal: the game uses up the opportunity and raises an event.

### Missions off screen: `gtg_npc_resolve_mission`

A mission the player joins is played out. Otherwise the game resolves it off screen with this separate export and tells the player the result with the reward (owner decision, 2026-10-06). It takes the order (4 deliver or 5 raid), the companion's level, the mission's level and a random number, and returns the outcome. It returns `STATUS_INVALID` for another order or a level above 65535.

| Profile | Base chance | Per level of difference | Lowest | Highest | Of failures: failed / caught / killed |
|---|---:|---:|---:|---:|---|
| Deliver | 0.90 | 0.03 | 0.10 | 0.99 | 80% / 15% / 5% |
| Raid | 0.50 | 0.06 | 0.05 | 0.95 | 35% / 40% / 25% |

The success chance is the base plus the per-level number times (companion level minus mission level), clamped. A roll from the top 24 bits of `noise` decides success. For a failure, the low 8 bits of `noise` over 256 split it into failed, caught and killed. All the numbers are placeholders for balance work. A caught companion can be rescued, which is a game system.

## What lives in Unity

| Data | Where | Notes |
|---|---|---|
| Level, order, order level | `ManagedNpcBrain` | `SetOrder`. Levels clamp to 0..65535 |
| Provoked | `ManagedNpcBrain` | Damage with a source other than the NPC itself or its leader, running out after 20 s |
| Disposition | `ManagedNpcBrain` | An Inspector setting, hostile by default |
| Leader | `ManagedNpcBrain` | What a companion follows and never sees as a threat |
| Trustworthiness, affinity, pay satisfaction | `ManagedNpcBrain` | 0 to 1, 0.5 by default (loyalty 0.5, no betrayal) |
| Betrayal opportunity | `ManagedNpcBrain` | Set by game code, never for a companion the story protects. Used up by a betrayal |
| Noise | `ManagedNpcBrain` | Its own sequence per brain, seeded from the id, sent for companions only |
| Mission scheduling, notification, reward, capture, rescue | not built | Game systems around `ManagedMissionResolver` |
| Pay, profit split, the economy | not built | Feed pay satisfaction |

## Planned and not built

- `threat_distance`, allies and enemies nearby, time of day, Crown Heat and Local Favor, for rules that act on range, groups, routines and guard reactions.
- Factions, so companions can see enemies at all.
- A scripted betrayal is a story event and is not decided here. The story changes the companion directly.

Adding a field means: add it in `npc-ffi`, the C header, the C# struct, the three benchmark generators and the tests, give it a rule, and bump `ABI_VERSION`, which `NPCNativeLib` checks at load.

## History

| Version | Change |
|---|---|
| 2 | 32-byte observation, five actions, batched call |
| 3 | 64 bytes. Companion role, disposition, order, provoked, level, order level, actions follow, hold and refuse order |
| 4 | Order types deliver and raid with per-type refusal gaps, the betrayal inputs and the actions mission and betray, and the off-screen mission export |

## Checks

- `npc-behavior`: 40 unit tests, including the exact boundaries (every refusal gap, the 0.2 health threshold, the betrayal line at roll 0.5, the mission lines), overflow, and distribution checks.
- `npc-ffi`: 23 unit tests for layout, every validation rule, version 3 and 2 behavior preserved, the mission export, overlap and alignment rejection.
- `test.c`: layout asserts for every offset and the same behaviors through a real C caller.
- Rust, C and C# generate the same 16,384 mixed NPCs (companions with all six orders, trust, liking, pay, noise and betrayal opportunities) and must produce the same decisions. All ten actions must occur.
- The C# test also compares 200,000 mission resolutions between the Rust library and the managed copy, with no differences allowed.
- 645 Managed checks run against Unity stubs on both the fallback and the real library.
