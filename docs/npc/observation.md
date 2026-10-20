# NPC observation contract (ABI version 7)

Status (2026-10-20): implemented in Rust, C, the C# bridge and the Managed NPC code, and checked by tests. Not run inside Unity, not run on GitHub. The ABI version is 7. This page replaces `observation-v3.md`.

This is the contract between the game and the Rust decision. It lists every field, what it means, how it is validated, and what each rule does with it. Read it before adding a field.

## Principles

1. **The game owns the data, Rust owns the choice.** NPC memory, levels, orders, loyalty and the economy live in Unity and are saved by Unity (owner decision, 2026-10-05). Rust receives a small snapshot with every decision and returns an action. The decision is a pure function of that snapshot: the same observation always gives the same action, and Rust keeps no state. A chance in a rule uses a random number that Unity supplies in the observation (`noise`), so a run stays reproducible and testable. Static, predictable tables, such as the refusal gaps and the mission profiles, live in Rust.
2. **Zero means no information.** Every field added after version 2 has a safe default of zero: hostile, no order, not provoked, level 0, no betrayal opportunity, no Crown Heat, a melee fighter, nothing to shoot, no ranged stance, an unknown distance to the threat, and neutral favor. A caller that sets only older fields behaves as before, and tests check it.
3. **Reserved space must be zero.** A non-zero reserved field is rejected, so a caller built for a later layout fails loudly instead of being half understood. Version 7 used the last reserved word, so the 64 bytes are full and there is no reserved space left. The next field makes the struct bigger, which changes `gtg_npc_observation_size`, and the C# bridge refuses a library whose size differs from its own.
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
| 28 | 4 | `threat_distance` | f32 | Meters from the NPC to the threat it is fighting, on the ground plane. Must be finite. Zero or less means unknown. Version 5 callers sent 0 here as a reserved word |
| 32 | 1 | `disposition` | u8 | 0 hostile, 1 retaliatory, 2 peaceful |
| 33 | 1 | `order` | u8 | 0 none, 1 follow, 2 hold, 3 attack, 4 deliver, 5 raid. Companions only |
| 34 | 1 | `provoked` | u8 | 1 when the NPC was attacked recently, else 0 |
| 35 | 1 | `crown_heat` | u8 | Crown Heat tier of the region the NPC stands in: 0 none, 1 wanted, 2 hunted, 3 outlaw. Guards only. Version 4 callers sent 0 here as a reserved byte |
| 36 | 2 | `level` | u16 | The NPC's level |
| 38 | 2 | `order_level` | u16 | How hard the current order is, as a level. 0 for an order with no difficulty |
| 40 | 4 | `trustworthiness` | f32 | 0 to 1, must be finite, clamped. Companions, betrayal only |
| 44 | 4 | `affinity` | f32 | How much the companion likes the player. Same rules |
| 48 | 4 | `pay_satisfaction` | f32 | How satisfied it is with its pay. Same rules |
| 52 | 4 | `noise` | u32 | A random number from the game, one per decision |
| 56 | 1 | `betrayal_opportunity` | u8 | 1 when the companion could betray the player now, else 0 |
| 57 | 1 | `attack_style` | u8 | 0 melee, 1 ranged, 2 hybrid. Guards, enemies, bosses, companions and shopkeepers that fight use it. Version 5 callers sent 0 here as a reserved byte, which is melee |
| 58 | 1 | `ranged_ready` | u8 | 1 when the NPC has something to shoot (arrows, bolts or mana), else 0. Read by ranged and hybrid fighters only |
| 59 | 1 | `ranged_stance` | u8 | 1 when the NPC's last combat action was shoot or kite, else 0. The game keeps this memory. A hybrid fighter uses it between its two distances |
| 60 | 4 | `favor` | f32 | How much the NPC likes the player, from -1 (hates) to 1 (adores), 0 neutral. Must be finite, and a value outside the range is clamped. Shopkeepers and civilians read it. Version 6 callers sent 0 here as a reserved word, which is neutral |

Decision (16 bytes): `npc_id` u64, `action` i32, `reserved` u32 (always 0).

Actions: 0 idle, 1 trade, 2 patrol, 3 attack, 4 retreat, 5 follow, 6 hold, 7 refuse order, 8 mission, 9 betray, 10 shoot, 11 kite.

Mission outcomes (from `gtg_npc_resolve_mission`): 0 success, 1 failed, 2 caught, 3 killed.

The numbers come from `npc-ffi`, the C header `gtg_npc.h`, the C# enums and structs, and the enums in `archetypes.mdix`. `scripts/check_npc_enum_sync.py` fails CI when the enum values differ, and the C smoke test, the Rust tests and the C# test check sizes and offsets.

Validation (`STATUS_INVALID` for the whole batch, nothing written): an unknown role, backend, disposition, order or attack style; a `crown_heat` above 3; `threat_visible`, `can_move`, `provoked`, `betrayal_opportunity`, `ranged_ready` or `ranged_stance` above 1; a non-finite health, trustworthiness, affinity, pay satisfaction, threat distance or favor; input and output buffers that overlap. A count above 4096, or an output smaller than the input, is `STATUS_CAPACITY`. A null or misaligned pointer is `STATUS_NULL`.

## What each rule does

### Merchant and civilian

A civilian runs from a threat when it can move, and otherwise walks around or stands. Disposition, orders, levels and the companion inputs do nothing for a civilian.

A merchant trades, and stands still when a threat is in view. One exception, added with version 6: a merchant with the retaliatory disposition that was attacked (`provoked`) fights back, in its own attack style, with the same rule as any fighter (so it still runs below 0.2 health when it can move, and a stationary shopkeeper strikes or shoots from its stall). A hostile merchant is unchanged and still only stands still, a peaceful one never fights, and a retaliatory one that was not attacked keeps trading and standing. Orders, levels, Crown Heat and the companion inputs do nothing for a merchant. A merchant that is not attacked is never affected, and a test walks every disposition, backend, provoked and can-move combination to prove it.

Favor (version 7) changes two cases. A merchant that likes the player (`favor` at or above 0.2, the `FRIENDLY_FAVOR` line, which is 20 on the 100 point meter of the game) keeps trading under a visible threat instead of standing still, whatever its disposition, unless it was just attacked: a provoked merchant is no friend, so a retaliatory one still fights back and any other one stands still. A civilian that likes the player and was not just attacked does not take it for a threat and goes about its day (patrols, or idles when it cannot move) instead of running. Favor is read only when a threat is in view, and only for these two roles. Guards, enemies, bosses and companions ignore it, so a hostile guard that adores the player still fights it. Below the line, and for a favor that is not finite, nothing changes.

### Guard, enemy and boss: disposition

With no threat in view the NPC walks around (or stands, when it cannot move). With a threat in view: hostile fights, retaliatory ignores the threat until `provoked` is set and then fights, and peaceful never fights, running when it can move and standing when it cannot, even when provoked.

Fighting means: retreat when health is below 0.2 and the NPC can move, otherwise engage for the state machine backend, and for the utility and hybrid backends compare an attack score (0.6 + 0.4 x health) with a retreat score (1 - health) and retreat when that is higher, otherwise engage. How it engages depends on its attack style, see the next section.

The three dispositions are an interpretation of the project notes. "Good" humanoids are read as peaceful, so they never fight back. If they should defend themselves, that is one more case in `combatant()`.

### Attack styles (version 6)

A fighter that has decided to engage does it by its `attack_style`. The style is the NPC's kit, and the observation carries three more inputs the rules read: `ranged_ready`, `ranged_stance` and `threat_distance`.

| Style | Behavior |
|---|---|
| Melee (0) | Attack. Exactly the behavior before version 6, and the default |
| Ranged (1), something to shoot | Shoot. Kite instead when it can move and the threat is known to be closer than 6 m |
| Ranged (1), nothing to shoot | Retreat when it can move, otherwise idle. It does not melee |
| Hybrid (2), nothing to shoot | Attack by hand |
| Hybrid (2), something to shoot | Attack at 4 m or less, shoot at 8 m or more. In between (the buffer) it keeps what it was doing: shoot when `ranged_stance` is set, otherwise attack |

Details:

- An unknown distance (zero, negative or missing) counts as the buffer. Nothing switches on a guess, so a hybrid that was shooting keeps shooting and a melee one keeps striking, and a ranged fighter does not kite.
- Hybrids never kite. They just step in to melee when the threat is close.
- The buffer exists so a threat standing at the line does not flip a hybrid between two actions every tick. A test walks a hybrid through the distances and checks that the action changes only at 4 m and 8 m.
- Shoot and Kite only come out of this function for a ranged or hybrid fighter. The actions are in the table for every role that fights (guard, enemy, boss, companion on an attack or raid order, a provoked retaliatory shopkeeper), because they all use the same fighting rule.
- The three distances (`KITE_DISTANCE` 6, `HYBRID_MELEE_DISTANCE` 4, `HYBRID_RANGED_DISTANCE` 8) are placeholders in `npc-behavior` and are copied in `NPCManagedFallback.cs`. The Unity stand-off distance of a shooter must stay clear of the kite line, see `GrandTheftGrimoire/managed.md`.
- Boss fights are a separate system later. Until then a boss uses the same style rules as an enemy.

### Guard: Crown Heat

`crown_heat` is the tier of the region the guard stands in, as the game worked it out from the crimes the player committed there. One rule reads it. A guard with the retaliatory disposition that sees a threat and was not attacked fights it when the tier is 1 or more, exactly as it would after being attacked. Everything else is unchanged:

| Role and disposition | Tier 0 | Tier 1 and above |
|---|---|---|
| Guard, retaliatory, threat in view, not provoked | ignores the threat | fights |
| Guard, hostile | fights | fights |
| Guard, peaceful | never fights | never fights |
| Enemy, boss, companion, merchant, civilian | no change | no change, the tier is not read |

A unit test walks every combination of role, disposition, order, backend, threat, provoked, can-move and health at every tier and checks that this one case is the only difference. Tiers 2 and 3 decide the same as tier 1 today. They are kept for rules that are not built yet (bounty hunters, the Crown's knights). The threshold (`WANTED_CROWN_HEAT`, 1) is a placeholder. Zero means no heat or no information, so a caller that does not know about Crown Heat gets the old behavior.

The tier does not say who the threat is. The game only sets it for a guard, and a threat that is not the player is a case the rule does not separate yet, see the planned list.

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
| Attack style | `ManagedNpcBrain` | An Inspector setting, melee by default. A ranged or hybrid brain without a `ManagedNpcShooter` warns once and counts as having nothing to shoot |
| Ranged ready | `ManagedNpcShooter` | Ammo (arrows, bolts, or casts for a mage) above zero. Sent for ranged and hybrid fighters only |
| Ranged stance | `ManagedNpcBrain` | True while the last action was Shoot or Kite. Sent for ranged and hybrid fighters only |
| Threat distance | `ManagedNpcBrain` | The distance on the ground plane to where the threat was last seen, at least 0.01 so a known distance is never zero. Sent for every NPC, and zero when no threat is in view |
| Favor | `ManagedNpcFavor`, `ManagedFavorRegion` | A gauge on the NPC sends its own value over 100. An NPC with no gauge sends the crowd value of the favor region it stands in, and zero outside every region. See `GrandTheftGrimoire/managed.md` and `GrandTheftGrimoire/favor.md` |
| Crown Heat meter, regions, crimes, fade | `ManagedCrownRegion` | A box volume in the scene owns the numbers. The brain reads the tier at its position and sends it for guards only. See `GrandTheftGrimoire/crown-heat.md` |
| Mission scheduling, notification, reward, capture, rescue | not built | Game systems around `ManagedMissionResolver` |
| Pay, profit split, the economy | not built | Feed pay satisfaction |

## Planned and not built

- Allies and enemies nearby and time of day, for rules that act on groups and routines. (`threat_distance` was built with version 6, for the attack styles, and personal favor with version 7. The Local Favor of a town is built in Unity from the gauges of its relevant NPCs, and reaches Rust only through the favor of each NPC, so a rule that reads the Local Favor of a region directly, such as a guard's reaction to a hated or adored town, is not built and needs a field of its own.)
- Favor rules for guards and companions (refuse to help, snitch, misdirect, give a discount). The gauges, regions and tiers exist in Unity, and the decision does not use them yet.
- Rules for tiers 2 and 3 (bounty hunters, the Crown's knights), and a way to tell the player from another threat when a guard decides on Crown Heat.
- Factions, so companions can see enemies at all.
- A scripted betrayal is a story event and is not decided here. The story changes the companion directly.

Adding a field means: add it in `npc-ffi`, the C header, the C# struct, the three benchmark generators and the tests, give it a rule, and bump `ABI_VERSION`, which `NPCNativeLib` checks at load.

## History

| Version | Change |
|---|---|
| 2 | 32-byte observation, five actions, batched call |
| 3 | 64 bytes. Companion role, disposition, order, provoked, level, order level, actions follow, hold and refuse order |
| 4 | Order types deliver and raid with per-type refusal gaps, the betrayal inputs and the actions mission and betray, and the off-screen mission export |
| 5 | `crown_heat` at offset 35 (a reserved byte before), validated up to 3. Retaliatory guards fight a visible threat at tier 1 or more |
| 6 | `threat_distance` at offset 28, `attack_style`, `ranged_ready` and `ranged_stance` at offsets 57 to 59 (reserved before), the actions shoot and kite, and the retaliatory shopkeeper rule. `reserved_c` at 60 stays reserved |
| 7 | `favor` (f32) at offset 60, the last reserved word. A liked shopkeeper keeps trading under a threat and a liked civilian does not run, unless they were just attacked. Nothing is reserved any more |

## Checks

- `npc-behavior`: 79 unit tests, including the exact boundaries (every refusal gap, the 0.2 health threshold, the betrayal line at roll 0.5, the mission lines, the kite line and the two hybrid lines), the Crown Heat rule and the walk that proves it changes one case only, the attack style rules for every backend and can-move case, the hybrid buffer sequence, the shopkeeper walk, ten favor tests (the exact line, the roles that read it and those that do not, provoked, bad numbers), overflow, and distribution checks.
- `npc-ffi`: 32 unit tests for layout, every validation rule (including the style, the two flags, a non-finite distance and a non-finite favor), favor reaching the decision, its clamp, and fighters and companions ignoring it, the Crown Heat tier limit, version 3 and 2 behavior preserved, the mission export, overlap and alignment rejection.
- `test.c`: layout asserts for every offset and the same behaviors through a real C caller.
- Rust, C and C# generate the same 16,384 mixed NPCs (companions with all six orders, trust, liking, pay, noise, betrayal opportunities, Crown Heat tiers, attack styles, ammo, stances, distances and favor) and must produce the same decisions. All twelve actions must occur. The decision hash is `0xdd97a1437f8256ed` on all three (the favor of shopkeepers and civilians joined the generator with version 7, which changed it from `0xcdb9285c1d3ebd8e`). The C# test also runs a table of shopkeeper cases (every disposition, provoked, threat, can-move, style, ammo and distance) on every backend, native and managed, and the C smoke test has a shopkeeper block.
- The C# test also compares 200,000 mission resolutions between the Rust library and the managed copy, with no differences allowed.
- 1130 Managed checks run against Unity stubs on both the fallback and the real library.
