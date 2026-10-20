# Favor (`MidManStudio.Gtg.Managed.Favor`)

## Overview

Favor is how much people like the player. It starts with individuals and builds up into towns:

- Every **relevant NPC** (a shopkeeper, a quest giver, a guard captain, anyone whose opinion the story
  cares about) can carry a **personal favor** gauge, from -100 to 100.
- A **favor region** is a town or a district, a box in the scene. Its **Local Favor** is built from the
  gauges of the relevant NPCs standing in it and from a **crowd value** that stands for everybody who has
  no gauge, the nameless townsfolk.
- An NPC with no gauge of its own feels the crowd value of the region it stands in.
- Hurting somebody lowers the favor of the victim, of the relevant NPCs who watched, and of the crowd.

The split follows the rest of the NPC system. Unity owns the numbers. Rust owns the decision and receives
one number per NPC: its favor from -1 to 1. That is the `favor` field at offset 60 of the observation,
ABI version 7, see `npc/observation.md`. The Local Favor of a region never goes to Rust directly. It
reaches the decision only as the favor of each NPC.

Everything lives in `Assets/MidManStudio/Gtg/Managed/Favor/` and has no ECS dependency, so it belongs to
the Managed stack described in `managed.md`.

## What favor changes today

Only two cases, both in Rust and both only while a threat is in view:

| Role | Favor below 20 (0.2) | Favor at or above 20 (0.2) |
|---|---|---|
| Shopkeeper | Stands still, as before | Keeps trading under the player's eye |
| Civilian | Runs, as before | Goes about its day (patrols, or idles when it cannot move) |

Exception: an NPC the player has just attacked (provoked, 20 s) is no friend, whatever it felt. A
provoked retaliatory shopkeeper still fights back, any other provoked shopkeeper stands still, and a
provoked civilian runs. Guards, enemies, bosses and companions never read favor.

The line, 20 on the meter and 0.2 on the wire, is `FRIENDLY_FAVOR` in `npc-behavior` and the Liked tier
of `ManagedFavorTier`. A test checks that the two agree.

## Design decisions (owner, 2026-10-18)

1. **Bottom-up.** Personal favor is what builds up into local favor. Relevant NPCs hold individual favor,
   and the NPC population of a town accumulates into the Local Favor of its region.
2. **Kingdom, races, factions, in that order.** Every NPC will carry all three tags, and where rules
   conflict the most specific wins: faction over race over kingdom. Only the Local Favor of towns exists
   so far, see "Not built".
3. **Relevant NPCs plus a crowd value.** The Local Favor is the average of the relevant NPCs' gauges plus a
   crowd value that counts as a few more members, so a town with few relevant NPCs leans on the crowd and
   a town with many is their average.
4. **One pipeline.** Race, faction and kingdom standing are meant to build up the same way as the local
   one, so there is one set of code.
5. **No vendoring.** The studio packages are pinned git URLs in `Packages/manifest.json`, not copies.
6. **Build order.** Favor first, factions next.

## The pieces

| File | What it is |
|---|---|
| `ManagedFavorTypes.cs` | The tiers (Hated, Disliked, Neutral, Liked, Adored) and how heavy each kind of violence is. Plain data |
| `ManagedFavorMath.cs` | The arithmetic: clamp, normalize to -1..1, and `Blend` of members and a crowd. No Unity types |
| `ManagedNpcFavor.cs` | The gauge of one NPC. Next to its `ManagedNpcBrain` |
| `ManagedFavorRegion.cs` | One town. The crowd value, the Local Favor, and what hurting somebody costs |

The details and decisions of each file are in `managed.md`.

## Numbers (all Inspector fields, all placeholders)

| Field | Where | Default | Meaning |
|---|---|---:|---|
| Favor | gauge | 0 | Start value. Above zero likes the player |
| Home region | gauge | none | The region it counts toward wherever it walks. Left empty, the region whose box holds it now |
| Hurt penalty | gauge | 20 | Lost when the player hurts this NPC |
| Witness penalty | gauge | 5 | Lost when the player hurts somebody else while this NPC has the player in sight |
| Size | region | 120 x 40 x 120 m | The box around the object. Not rotated |
| Start crowd | region | 0 | Favor of everybody with no gauge, when the scene starts |
| Crowd weight | region | 3 | How many relevant NPCs the crowd is worth in the Local Favor |
| Crowd penalty | region | 4 | Crowd favor lost when the player hurts a merchant, civilian or guard |
| Assault repeat seconds | region | 3 | Hurting the same victim again inside this counts once. A kill always counts |

Tiers: Hated at -60 or below, Disliked at -20 or below, Neutral between, Liked at 20 or above, Adored at
60 or above. A kill weighs three times a blow.

Worked example with the defaults. A town with no gauges and a crowd of 0 is neutral. A baker with a gauge
at 60 joins: the Local Favor is (60 + 3 x 0) / (1 + 3) = 15, still neutral. Two more relevant NPCs at 100:
(260 + 0) / 6 = 43, liked. The player hits the baker: the baker loses 20 (60 to 40), every relevant NPC who
had the player in sight loses 5, the crowd loses 4, and the Local Favor is recalculated.

## How hurting costs favor

1. The player hurts an NPC. `ManagedHealth` raises `Damaged` with the source, and the victim's
   `ManagedNpcBrain.OnDamaged` hands the hit to `ManagedFavorRegion.ReportHurt`, right after the Crown
   Heat report. The two systems are independent.
2. The offender is whatever `ManagedNpcThreatSource` the damage source belongs to, as long as it is not an
   NPC. Hits from NPCs, from objects with no threat source and hits with no source cost nothing.
3. A blow that does not kill counts once per victim per 3 s (the region's repeat time, or 3 s outside
   every region). A killing blow always counts.
4. The victim loses `Hurt penalty` times the severity (1 for a blow, 3 for a kill), if it has a gauge.
5. Only for a victim who is a merchant, a civilian or a guard: every other live gauge whose NPC has the
   offender in view right now loses `Witness penalty` times the severity, and the crowd of the region
   around the victim loses `Crowd penalty` times the severity. A victim in no region costs no crowd.
6. The Local Favor of the region is rebuilt on its next read, and every half second by itself.

## Scene setup

1. Add `ManagedFavorRegion` to an empty GameObject at the center of a town and size its box to cover the
   town. Select the object to see the box (green while the people like the player, red while they dislike
   him, yellow between). Where two boxes overlap, the first to register wins, so keep them apart.
2. Set the **Start crowd** for how the town feels about the player at the start.
3. Add `ManagedNpcFavor` to every NPC whose opinion matters, next to its `ManagedNpcBrain`. Any order
   works. Set its start favor.
4. Nothing else. Brains find their gauge on their object, and gauges find their region by position.

## For game code

| Call | Use |
|---|---|
| `ManagedFavorRegion.Find(point)` / `CrowdAt(point)` | Which region a point is in, and the crowd value there from -1 to 1 (0 outside) |
| `region.Local`, `region.Tier`, `region.MemberCount` | The Local Favor from -100 to 100, its tier and how many gauges counted |
| `region.Crowd`, `region.SetCrowd(x)`, `region.AddCrowd(x)` | Read and move the crowd value. A gift to the whole town, a rumor |
| `region.TierChanged(region, old, new)` | Music, UI, shop prices |
| `gauge.Value`, `gauge.Tier`, `gauge.Set(x)`, `gauge.Add(x)` | One NPC's favor. A gift, a favor done, an insult |
| `gauge.Changed(gauge, old, new)` | A reaction line, an unlock |

## Debugging

The NPC debugger window (menu GTG, NPC Debugger) lists one `Favor:` line per region with its Local Favor,
tier, crowd and member count. An NPC with a gauge shows `favor 45 liked` in its list row.

## Checks

- `npc-behavior`: ten favor tests: the exact line, the roles that read favor and those that do not,
  provoked, bad numbers.
- `npc-ffi`, `test.c` and the C# ABI test: favor sits at offset 60, a favor that is not finite rejects the
  whole batch, a favor outside -1..1 is clamped, and a caller that sends zero gets the old behavior. Rust,
  C and C# produce the same decision hash.
- `TestsFavor.cs` in the Managed harness (144 checks): the arithmetic and the tiers, the gauge (clamping,
  events, the registry, a gauge added before its brain), the region box and the crowd, the Local Favor and
  its tier events, members that walk, home regions and the dead, what each NPC sends to the decision,
  shopkeepers and civilians deciding by favor through the director, and every rule of what hurting costs.
  Twenty-six deliberate breaks were injected: twenty-five were caught, and the one survivor is an equivalent
  mutant (a witness who has the offender as its threat always has it in view, so the extra sight check
  cannot be told apart).

## Not built

- **Factions.** The kingdom, race and faction tags, the "most specific wins" rule, and NPC against NPC
  combat. They are the next stage, and they reuse this pipeline: the standing of a race, a faction and the
  kingdom builds up from the NPCs that carry the tag, the same way a town's does.
- Favor of a town reaching the decision directly (a guard who treats a hated town differently). Only each
  NPC's own favor is sent. A rule that needs the Local Favor needs a new field, and the 64 bytes of the
  observation are full.
- Seeding favor from data, gossip between NPCs, favor for good deeds (only violence moves it today), decay
  over time, and saving.
- What favor unlocks: refusing service, snitching to guards, misdirecting the player, discounts. The
  gauges and tiers exist. Nothing reads them but the two rules above.
- A real Unity run. The harness uses stubs, so witnessing by real sight has not been seen working.

## Fixes and Problems

- The shipped observation had no room left for a field. Favor took the last reserved word, so the next
  field makes the struct bigger.
- Interpretations to confirm: a liked NPC does not fear the player; an NPC with no gauge feels the crowd of
  its region; only objects with a threat source that is not an NPC count as "the player"; a victim outside
  every region costs no crowd; only merchants, civilians and guards cost the crowd and the watchers.
- Every number above, the tiers and the severities are placeholders.
