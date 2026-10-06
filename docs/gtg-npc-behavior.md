# gtg-npc-behavior

The deterministic decision: one `decide` function that turns an observation into an action. No allocation, no random numbers, no state. Version 0.0.1.

## Modules

### `lib.rs`

**What it does:** `decide(Observation) -> Decision`, with one rule set per role. Merchants trade, civilians run, guards, enemies and bosses fight according to their disposition, and companions follow orders unless an order is too hard for their level. `REFUSE_LEVEL_GAP` is the number of levels above its own that a companion accepts.

**Decisions:**
- The function is pure, so the same observation always gives the same action. A chance in a rule would need a random number in the observation, and none exists yet.
- Fighting is one shared function: retreat below 0.2 health when the NPC can move, then attack for the state machine backend, or compare an attack and a retreat score for the others. Guards, enemies, bosses and companions all use it, so a change to fighting changes all of them.
- Disposition applies to guards, enemies and bosses. Peaceful never fights, even when provoked. Retaliatory waits for `provoked`. These are an interpretation of the project notes and are flagged in `npc/observation-v3.md`.
- A companion without an order follows. A following companion fights only when it was provoked, and an attack order fights on sight. A hold order never fights and never moves.
- The refusal gap is 5 levels. It is a placeholder, because the project notes say only that an order "way above" a companion's level is refused. The check is strict: exactly at the gap is accepted. It uses a saturating addition, so the top level cannot overflow.
- Merchants and civilians ignore disposition, orders and levels, and tests check it.
- `HybridMl` uses the utility rules until a policy exists, see `gtg-npc-ml`.

## Fixes and Problems

- Version 3 added disposition, orders, levels and the companion rules. 20 unit tests cover each rule, the exact boundaries (the refusal gap and the 0.2 health threshold), overflow, and that non-companions ignore orders.
- A mutation run changed 11 rules and the boundaries. One change survived at first (the health threshold, with the utility backend hiding it), and the boundary test was added. It is now caught.
- Not done: distance, allies, schedules, memory and factions. They need new observation fields, see `npc/rust-roadmap.md`.
