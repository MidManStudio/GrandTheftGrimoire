# Crown Heat (`MidManStudio.Gtg.Managed.Crown`)

## Overview

Crown Heat is how much the Crown wants the player in one town. A guard who watches the player hurt or
kill somebody the law protects raises the heat of that town. Enough heat and the town's guards fight
the player on sight, even a guard who would otherwise ignore him. The heat fades when no guard has
seen anything for a while.

The split follows the rest of the NPC system. Unity owns the numbers (the meter, the regions, the
crimes, the fade). Rust owns the decision and receives one small number with each observation: the
tier (0 none, 1 wanted, 2 hunted, 3 outlaw) of the region the guard stands in. That is the
`crown_heat` byte at offset 35 of the observation, ABI version 5, see `npc/observation.md`.

Everything lives in `Assets/MidManStudio/Gtg/Managed/Crown/` and has no ECS dependency, so it belongs
to the Managed stack described in `managed.md`. The ECS stack is frozen, so only the shared native
types gained the field.

## How a crime becomes a fight

1. The player hurts an NPC. `ManagedHealth` raises `Damaged` with the source, and the victim's
   `ManagedNpcBrain.OnDamaged` hands the hit to `ManagedCrownRegion.ReportHurt`.
2. `ReportHurt` ignores anything that is not a crime. The victim has to be a merchant, a civilian or a
   guard, the source has to belong to a `ManagedNpcThreatSource` (the player), and the victim has to
   stand inside a region.
3. A blow that does not kill counts once per victim per 3 s. A killing blow always counts.
4. The region asks whether a live guard inside it has that offender in view right now. If none does,
   nothing is added. The `CrimeReported` event fires either way, with a flag for whether it was seen.
5. The heat rises by the amount of the crime kind and is capped. The tier is worked out from the heat.
6. Every tick, each guard in the region builds its observation with `crown_heat` set to the tier of the
   region at its position. Other roles send zero.
7. In Rust, a guard with the retaliatory disposition that sees a threat and was not attacked fights it
   when the tier is 1 or more. Hostile guards fought already, and peaceful guards never fight.
8. The fight itself is the normal combat: attack tokens, ring places, circling and the live reach
   check, see `managed.md`, `ManagedCombatEngagement.cs`.

## Numbers (all Inspector fields on the region, all placeholders)

| Field | Default | Meaning |
|---|---:|---|
| Size | 120 x 40 x 120 m | The box around the object. Not rotated |
| Max heat | 100 | The cap |
| Wanted at | 10 | Tier 1 from this heat |
| Hunted at | 40 | Tier 2 |
| Outlaw at | 75 | Tier 3 |
| Unseen seconds | 20 | Wait after the last crime or sighting before the fade starts |
| Fade per second | 0.5 | Heat lost per second once it fades |
| Assault heat | 5 | Hurting a merchant or civilian |
| Murder heat | 25 | Killing a merchant or civilian |
| Assault on guard heat | 15 | Hurting a guard |
| Murder of guard heat | 40 | Killing a guard |
| Assault repeat seconds | 3 | Hurting the same victim again inside this counts once |

Worked example with the defaults: hitting a merchant once in sight of a guard adds 5, which is tier 0.
A second hit more than 3 s later adds 5 more and reaches tier 1, so the retaliatory guards now fight. Killing the
merchant adds 25 on top, 35 in all. Staying out of sight for 20 s and then 70 s more brings it back to
zero. Staying in sight of a guard keeps the clock restarting, so the heat does not fade.

## Scene setup

1. Add `ManagedCrownRegion` to an empty GameObject at the center of a town. Set its display name and
   size the box so that it covers every place a guard of that town can stand, and the places where
   crimes happen. Select the object to see the box (yellow while clear, red while wanted or worse).
2. Keep the boxes of two towns apart. Where two overlap, the first to register wins.
3. Guards need `Role` Guard on their brain, and a disposition. Retaliatory is the one that reacts to
   Crown Heat. Hostile guards fight anyway and peaceful guards never do.
4. Nothing else is needed. Brains find the region by position, and the region finds guards through the
   director's list.

## For game code

| Call | Use |
|---|---|
| `ManagedCrownRegion.Find(point)` / `TierAt(point)` | Which region a point is in, and its tier (0 outside) |
| `region.Heat`, `region.Tier`, `region.MaxHeat` | Read the meter |
| `region.WantedAt`, `region.HuntedAt`, `region.OutlawAt` | Read the tier thresholds |
| `region.Add(amount)` | A stir that is not a hit, for instance a story event. Restarts the fade clock |
| `region.Reduce(amount)` | A bribe that works in part |
| `region.Clear()` | A pardon, or a bribe to the right official |
| `region.Report(kind, offender)` | A crime of some other kind that a system detects itself |
| `TierChanged(region, old, new)` | Music, UI, bounty hunters |
| `CrimeReported(region, kind, seen)` | Story hooks, including crimes nobody saw |

## On the HUD

Add `ManagedCrownHudHeat` to any GameObject, one is enough, and the HUD's three crowns follow the heat
of the region the player stands in. The crowns are hidden while the player is clear and outside every
region.

| Tier | Crown | Sections lit |
|---|---|---|
| Wanted | First | 1, 2 or 3 as the heat climbs through thirds of the band from wanted to hunted |
| Hunted | Second | 4, 5 or 6 over the band from hunted to outlaw |
| Outlaw | Third | 7, 8 or 9 over the band from outlaw to the most heat the region holds |

With the defaults the sections light at heat 10, 20, 30, 40, 52, 64, 75, 84 and 92. A heat exactly on
a third lights the upper section. The bands are the region's own thresholds, so a region tuned by hand
changes where the sections light. The mapping is `ManagedCrownHeatMap`, the component is
`ManagedCrownHudHeat`, and the HUD names neither. Walking out of a town hides the crowns though the
heat stays in the region, and walking back shows them again. The details are in `managed.md`.

## Debugging

The NPC debugger window (menu GTG, NPC Debugger) lists one line per region with its heat and tier. A
guard standing in a region with heat shows `crown wanted` (or hunted, outlaw) in its scene label.

## Checks

- `npc-behavior`: the rule, its exact threshold, an exhaustive walk proving it changes one case and
  nothing else, and a test that no other role reads the tier.
- `npc-ffi`, `test.c` and the C# ABI test: the byte sits at offset 35, a tier above 3 rejects the whole
  batch, and a caller that sends zero gets the old behavior. Rust, C and C# produce the same decision hash.
- `TestsFeeders.cs` in `building-test/`: the heat map at every boundary, and the HUD reading a heat
  source. The component `ManagedCrownHudHeat` itself is compiled by the Managed NPC harness, in the
  `managed-npc-fallback` job of `managed-csharp-ci.yml` as well, and was checked against real regions
  once by hand. There is no check for it in the repo.
- `TestsCrown.cs` in the Managed harness: the region box, each crime kind and its amount, the repeat
  window and the kill exception, the tiers, the fade and its clock, what a guard sends in its
  observation, and witnessing by a guard. 836 checks pass on the fallback and on the real library, and
  forty deliberate breaks were each caught.

## Not built

- Rules for tiers 2 and 3 (bounty hunters, the Crown's knights, a bigger response than the local guards).
- Crimes other than hurting and killing: theft, trespass, drug dealing once that exists, spell use in town.
- Bribes and pardons as game systems, and the guard who is secretly loyal to the late King
  or on the Uncle's payroll. Personal favor and the Local Favor of a town are built (ABI version 7, see
  `favor.md`), and a guard does not read them yet.
- Telling the player from another threat. A guard fights whatever threat it sees once heat is up, and
  today the player is the only threat source.
- Saving the meter, and witnesses other than guards (a civilian who runs to fetch one).
- A HUD that shows more than the region the player stands in, for instance the heat of the town just left.
- A real Unity run. The harness uses stubs, so witnessing by real sight has not been seen working.

## Fixes and Problems

- Version 5 of the ABI changed with it, see `npc/observation.md` and `gtg-npc-ffi.md`. The reserved
  byte at offset 35 became `crown_heat`, so a library built before this version is refused at load by
  the version check, and the managed fallback takes over until a new build is imported.
- The workflow `npc-rust-ci.yml` lists the folders that start it. `Assets/MidManStudio/Gtg/Managed/Crown/**` is not
  in the list yet, so a change to the region alone does not start the `managed-npc` job. The workflow file is
  outside the replacements archive on purpose, so add the line by hand in both places (push and pull request).
- Favor (observation version 7, `favor.md`) is separate from Crown Heat. Hurting somebody reports to both systems, and neither reads the other. A guard that adores the player still fights it once the heat is up.
- A shopkeeper that fights back (observation version 6, `observation.md`) changes no heat number. The player's blows still count as assault, and a kill as murder, whether or not the shopkeeper was hitting back. A shopkeeper only fights after it was hit, so it never starts a fight, and self defense by the player is not modelled.
- Every number above is a placeholder, and the three design choices (regional meter, heat only when a guard
  sees the crime, fade only while unseen) are my reading of the design notes and need the owner's word.
