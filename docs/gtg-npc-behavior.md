# gtg-npc-behavior

The deterministic decision: one `decide` function that turns an observation into an action. No allocation, no random numbers, no state. Version 0.0.1.

## Modules

### `lib.rs`

**What it does:** `decide(Observation) -> Decision`, with one rule set per role. Merchants trade, civilians run, guards, enemies and bosses fight according to their disposition, and companions betray, refuse or carry out orders. `refuse_gap(order)` is how many levels above its own a companion still accepts an order of that type. `loyalty` and `betrayal_chance` expose the betrayal numbers. `resolve_mission` decides how an off-screen delivery or raid ends.

**Decisions:**
- The function is pure, so the same observation always gives the same action. The one chance in a rule, betrayal, takes its random number from `noise` in the observation, which the game supplies.
- Fighting is one shared function: retreat below 0.2 health when the NPC can move, then attack for the state machine backend, or compare an attack and a retreat score for the others. Guards, enemies, bosses and companions all use it, so a change to fighting changes all of them.
- Disposition applies to guards, enemies and bosses. Peaceful never fights, even when provoked. Retaliatory waits for `provoked`. These are an interpretation of the project notes and are flagged in `npc/observation.md`.
- The refusal gap depends on the order type: raid 1, attack 3, follow and hold 5, deliver 6. The owner said the rule depends on the order type and level. The numbers and the direction (dangerous orders allow little) are placeholders to confirm. The check is strict, exactly at the gap is accepted, and it uses a saturating addition, so the top level cannot overflow.
- Betrayal is checked first, only for a companion with an opportunity, and it beats refusal and every order. Loyalty is 0.4 trust + 0.3 liking + 0.3 pay satisfaction, there is no betrayal at 0.5 or more, and the chance rises linearly to 0.5 at no loyalty. A number that is not finite counts as 1, so bad input never causes a betrayal. All these numbers are placeholders.
- A raid fights on sight and otherwise carries out the mission. A delivery fights only when provoked. A mission needs the NPC to be able to move, otherwise it stands still.
- `resolve_mission` uses two profiles (deliver, raid) and the same roll as betrayal (the top 24 bits of the noise), then splits a failure with the low 8 bits. Raids kill and capture more often than deliveries. The numbers are placeholders for balance work.
- Merchants and civilians ignore disposition, orders, levels and the companion inputs, and tests check it.
- `HybridMl` uses the utility rules until a policy exists, see `gtg-npc-ml`.

## Fixes and Problems

- Version 3 added disposition, orders, levels and the companion rules. Version 4 added the order types, betrayal and missions. 40 unit tests cover each rule, the exact boundaries (every refusal gap, the 0.2 health threshold, the betrayal line and the mission lines), overflow, distributions, and that other roles ignore orders and the companion inputs.
- Mutation runs changed the rules, the boundaries and the validation. One change survived at first (the health threshold, with the utility backend hiding it), and the boundary test was added.
- Not done: distance, allies, schedules, memory and factions. They need new observation fields, see `npc/rust-roadmap.md`.
