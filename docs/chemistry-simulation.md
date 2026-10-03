# Chemistry simulation (`MidManStudio.Gtg.Chemistry`)

## Overview

This doc explains, in plain terms, what happens between a spell hitting something
and a hazard appearing, and which parts of that are simulated and which are
authored. It covers both stacks. The per-file notes for the ECS code are in
`chemistry.md`, and the notes for the Managed code are in
`GrandTheftGrimoire/managed.md`.

The short version: Alembic (`com.midmanstudio.alembic`) runs a few atoms for a few
femtoseconds and reports whether bonds formed or broke. The game reads that report
as a confirmation. The size, duration and look of the hazard are authored numbers
in the recipe, and Alembic does not choose them.

## Pipeline

1. A spell ends. A shot hit a collider and the caster raises an impact with the
   position, the surface normal and the spell kind. On the Managed stack a shot that
   reaches the end of its range raises one too. On the ECS stack the impact is an
   entity made by the projectile job.
2. The chemistry side looks up the recipe for that spell kind.
3. The reactor spawns the recipe's atoms in one native Alembic context, gives them
   velocities for the recipe's temperature and steps the simulation.
4. It counts the bond events. Formed and broken bonds are totalled over the run.
5. The result is confirmed when enough bonds formed and, if the recipe asks for it,
   few enough broke.
6. A hazard is created at the impact position, with the radius and timing of the
   recipe. A confirmed result and an unconfirmed one differ in color only, unless
   the recipe sets `RequireConfirmation`, which then cancels the hazard.
7. The atoms are removed from the context. The context lives as long as the system.

## What Alembic simulates

These facts come from the `unity-chem-sim` architecture notes.

- Units: positions in Angstrom, time in femtoseconds, energy in eV, temperature in
  Kelvin.
- Forces: Lennard-Jones between atom pairs, cut off at 10 Angstrom by default.
- Integration: velocity Verlet.
- Start: `chem_init` gives every live atom a velocity from a Maxwell-Boltzmann
  distribution at the requested temperature.
- Bonds: two atoms bond when they sit within 1.15 times `r_min` of each other and
  their combined reactivity clears a threshold. A bond adds a harmonic spring toward
  its rest length and breaks when stretched past 1.8 times that length. An atom
  gains at most one new bond per bond pass, so a molecule assembles over a few steps.
- Temperature reading: `chem_temperature` is computed from kinetic energy by
  equipartition. There is no thermostat, so the value drifts as energy moves between
  the spring and the motion.
- Events: `chem_take_bond_events` returns formed, broken and order-changed events
  since the last read.

The architecture notes list no hydrogen bond, phase change or heat flow model, so
the game treats Alembic as a bond forming and breaking test and nothing more. The
atom counts are a stylized sample, not a physical amount of reagent.

## What the game authors

The recipe holds every number that Alembic does not own.

| Field | Meaning |
|---|---|
| `Molecules` | Number of O + 2 H groups spawned, three atoms each |
| `MoleculeSeparationAngstrom` | Spacing between the groups along one axis, 6 |
| `SpawnSpacingFactor` | O to H spacing as a multiple of `r_min`, 1.08, inside the bonding window |
| `TemperatureK`, `DtFemtoseconds`, `CutoffAngstrom` | Alembic start temperature, step size and force cutoff |
| `MaxSteps` | Step budget, 32 |
| `MinFormedBonds` | Bonds that must form for a confirmation |
| `MaxBrokenBonds` | Bonds allowed to break. A negative value turns the check off |
| `RunFullBudget` | Keep stepping after the bond goal is met, so a late break is seen |
| `RequireConfirmation` | When true, an unconfirmed reaction spawns no hazard |
| `Hazard`, `BaseRadiusMeters`, `HazardDurationSeconds`, `GrowSeconds` | The hazard type and its authored footprint |

The radius is `BaseRadiusMeters` times the square root of the reagent quantity, and
a spell cast has quantity 1. The footprint is gameplay state. The visuals read it
and never decide it, so a visual that looks bigger does not make the hazard bigger.

## Recipes

### Fireball

Two groups, six atoms, at 1000 K, 1 fs steps. The run stops as soon as one bond has
formed, or after 32 steps. The result is confirmed when at least one bond formed.
The hazard is an Explosion of 3 m that appears at full size and lasts 1.5 s. The
fireball recipe is the same on both stacks.

### Ice

Three groups, nine atoms, at 150 K, 1 fs steps. The run always uses all 32 steps.
The result is confirmed when at least one bond formed and none broke. The hazard is
a Freeze zone of 3.5 m that grows from 0 to full size in 0.5 s and lasts 6 s.

The cold is authored, not simulated. With no freezing model in Alembic, the
reaction only tests that the water groups keep their bonds at a low temperature.
Because a cold start rarely breaks bonds, that test is weak, and the confirmation
for ice says less than the one for fire. The Managed stack has this recipe. The ECS
stack does not yet.

## Reading the log

Each impact writes one line, for example (the numbers are illustrative):

`[GTG Chemistry] ice impact at (4.0, 0.0, 12.5): alembic confirmed, bonds formed 3, broken 0, steps 32, temperature 148 K, radius 3.50 m`

- `alembic` is `unavailable` when the native library did not load, `confirmed` when
  the recipe goal was met, and `not confirmed` otherwise.
- `bonds formed` and `broken` are the totals over the run.
- `steps` is how many steps ran. A fireball that stops at 1 or 2 steps formed its
  bond at once.
- `temperature` is the Alembic reading at the end, for tuning only. No gameplay
  reads it.
- `radius` is the authored footprint for the recipe.

A second kind of line comes from the caster, for example
`[GTG Magic] Fireball hit 'Terrain' at (4.0, 0.0, 12.5) after 12.4 m`. It shows
where the shot ended and which collider it hit, or that it reached the end of its
range.

## Adding a recipe

1. Add a value to the spell kind enum and a spell definition in the table.
2. Add a recipe with its atoms, temperature and goals, and its hazard numbers.
3. Map the spell kind to the recipe in `ForSpell`.
4. Cast it once and read the log line. Tune the temperature and the bond goals until
   the line shows what the recipe is meant to prove.
5. Give the hazard type a color in the hazard field if it needs its own.

## Known gaps

- Hazards do not affect anything yet. There is no damage, slow or knockback.
- Only Explosion and Freeze have behavior. Gas and Fire are reserved names.
- The Ice recipe is untested against the real library. The numbers are a first guess.
- Hazard time uses the scaled game clock and is not network safe.
- The two stacks keep separate copies of the reaction code, see `managed.md`.

## CI and Workflows

None. Nothing here is built or tested by CI.

## Fixes and Problems

- The chemistry numbers were documented only inside the ECS module notes, and the
  reaction in plain terms, the ice recipe and the log format were not documented at
  all. This doc closes that gap.
