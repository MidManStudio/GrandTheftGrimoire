# NPC observation version 3

Status (2026-10-05): implemented in Rust, C, the C# bridge and the Managed NPC code, and checked by tests. Not run inside Unity. The ABI version is 3.

This page is the contract between the game and the Rust decision. It lists every field, what it means, how it is validated, and what each rule does with it. Read it before adding a field.

## Principles

1. **The game owns the data, Rust owns the choice.** NPC memory, levels, orders, loyalty and the economy live in Unity and are saved by Unity. Rust receives a small snapshot with every decision and returns an action. The decision is a pure function of that snapshot: the same observation always gives the same action, with no hidden state and no random numbers. Static, predictable tables, such as default dispositions by race, may live in Rust in a hash map or a `match`, because they never change in play.
2. **Zero means no information.** Every field added after version 2 has a safe default of zero: hostile, no order, not provoked, level 0. A caller that sets only the version 2 fields behaves exactly as before, and a test checks that.
3. **Reserved space must be zero.** A non-zero reserved field is rejected, so a caller built for a later layout fails loudly instead of being half understood.
4. **Add a field only when a rule uses it.** The fields that are described but not implemented are in the table below as reserved, with their planned meaning.
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
| 33 | 1 | `order` | u8 | 0 none, 1 follow, 2 hold, 3 attack. Companions only |
| 34 | 1 | `provoked` | u8 | 1 when the NPC was attacked recently, else 0 |
| 35 | 1 | `reserved_byte` | u8 | Must be 0 |
| 36 | 2 | `level` | u16 | The NPC's level |
| 38 | 2 | `order_level` | u16 | How hard the order is, as a level. 0 for an order with no difficulty |
| 40 | 24 | `reserved_tail` | 3 x u64 | Must be 0. Room for later fields, see below |

Decision (16 bytes): `npc_id` u64, `action` i32, `reserved` u32 (always 0). The decision size did not change.

Actions: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat, 5 follow, 6 hold, 7 refuse order.

The numbers come from `npc-ffi`, the C header `gtg_npc.h`, the C# enums and structs, and the enums in `archetypes.mdix`. `scripts/check_npc_enum_sync.py` fails CI when the enum values differ, and the C smoke test, the Rust test and the C# test check sizes and offsets. 64 bytes is one cache line on the common CPUs.

Validation (`STATUS_INVALID` for the whole batch, nothing written): unknown role, backend, disposition or order; `threat_visible`, `can_move` or `provoked` above 1; a non-finite health; any non-zero reserved field; input and output buffers that overlap. A count above 4096, or an output smaller than the input, is `STATUS_CAPACITY`. A null or misaligned pointer is `STATUS_NULL`.

## What each rule does

### Merchant and civilian (unchanged from version 2)

A merchant trades, and stands still when a threat is in view. A civilian runs from a threat when it can move, and otherwise walks around or stands. Disposition, order and level do nothing for these roles.

### Guard, enemy and boss: disposition

If no threat is in view the NPC walks around (or stands, when it cannot move). With a threat in view:

| Disposition | Behavior |
|---|---|
| Hostile (0, the default) | Fights, see below. This is the version 2 behavior and what mobs always do |
| Retaliatory | Ignores the threat and walks around until `provoked` is set, then fights |
| Peaceful | Never fights. Runs when it can move, stands when it cannot, even when provoked |

Fighting means: retreat when health is below 0.2 and the NPC can move, otherwise attack for the state machine backend, and for the utility and hybrid backends compare an attack score (0.6 + 0.4 x health) with a retreat score (1 - health) and take the higher.

These three dispositions are an interpretation of the project notes (some humanoids are hostile, some fight only if attacked, some are good). "Good" is read as peaceful, which means it never fights back. If good humanoids should defend themselves, that is one more case in `combatant()` and the owner should say so.

### Companion: orders and levels

A companion first checks its order. An order whose level is more than `REFUSE_LEVEL_GAP` (5, a placeholder) above the companion's own level is refused with the action 7, whatever the order is. An order exactly at the gap is accepted. The check uses a saturating addition, so a level near 65535 cannot overflow.

| Order | Behavior once accepted |
|---|---|
| None | Same as follow |
| Follow | Follow the leader. Fight only when a threat is in view and the companion was provoked, running at health below 0.2 |
| Hold | Stay put, whatever happens, even when attacked |
| Attack | With a threat in view, fight. Otherwise follow the leader |

A companion that cannot move stands still instead of following. Other roles ignore the order fields completely, and a test checks that.

The refusal is a one-shot result: the game clears the order when it receives action 7, raises an event, and the next decision is made without an order.

## What lives in Unity

| Data | Where | Notes |
|---|---|---|
| Level, order, order level | `ManagedNpcBrain` | Set from game code with `SetOrder`. Levels clamp to 0..65535 |
| Provoked | `ManagedNpcBrain` | Set by damage that has a source other than the NPC itself or its leader, and it runs out after 20 seconds. Damage with no source (a hazard) does not provoke |
| Disposition | `ManagedNpcBrain` | An Inspector setting, defaults to hostile |
| Leader | `ManagedNpcBrain` | What a companion follows and never sees as a threat. A companion with no leader warns once and sees the player as a threat |
| Loyalty, pay, missions, capture and death on a raid | not built | Unity systems, see the companion section of `rust-roadmap.md` |

## Reserved space and the next fields

The 24 reserved bytes and the reserved byte are for fields whose rules do not exist yet. Their planned meaning, not yet in the ABI:

- `threat_distance` (f32): needs actions or rules that act on range, such as holding at range or kiting.
- `loyalty`, `pay_satisfaction` and an opportunity flag: for emergent betrayal. The formula needs the economy and the list of conditions from the owner. Scripted betrayals do not go through this path, the story forces the action.
- `noise` (u32): a random number that Unity supplies per decision, so a chance in a rule stays reproducible and testable.
- Allies and enemies nearby, time of day, Crown Heat and Local Favor.

Adding any of these changes the ABI version. The procedure: add the field in `npc-ffi`, the C header, the C# struct, the three benchmark generators and the tests, give it a rule, and bump `ABI_VERSION`, which `NPCNativeLib` checks at load.

## Companion mechanics that are not decisions

The owner's answers shape what Rust must not do:

- A companion may refuse an order above its level. That is the refusal rule above.
- Betrayal is both scripted and emergent. A scripted betrayal is a story event and is not decided here. An emergent one needs loyalty and an opportunity as inputs (reserved above), and it is not implemented.
- A companion can be caught or killed on a raid. A raid is a mission the game resolves, and the result is a game event (the companion is captured or dies), not an action choice. If that resolution wants a deterministic function, it can be a second, separate export that takes the companion's level, the mission level and a random number. It is not part of this ABI.

## Checks

- `rust/npc/crates/npc-behavior`: 20 unit tests, including exact boundaries (the refusal gap, the 0.2 health threshold), overflow, and that non-companions ignore orders.
- `rust/npc/crates/npc-ffi`: 19 unit tests for layout, validation of every new field, version 2 behavior preserved, overlap and alignment rejection.
- `rust/npc/ffi-smoke-test/test.c`: layout asserts for every offset and the same behaviors through a real C caller.
- Rust, C and C# generate the same 16,384 mixed NPCs, now with dispositions, provoked flags, levels and companion orders, and must produce the same decisions. All eight actions must occur in the mixed workload.
- Sandbox mutation checks: 26 deliberate changes to the Rust rules, the validation and the C# fallback were each caught by a failing test. One mutant survived at first (the health threshold for companions) and led to the boundary test.
