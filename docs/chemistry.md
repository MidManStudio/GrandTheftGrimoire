# Chemistry (`MidManStudio.Gtg.Chemistry`)

## Overview

The game-side glue around `com.midmanstudio.alembic` -- not the
chemistry sim itself (that's Alembic/`chemistry_core`), this is where
GTG turns "a bond formed/broke/upgraded in the sim" into an actual
gameplay moment: a recipe being recognized, a hazard spawning, a visual
playing. The first code has landed, see Modules: a fireball impact runs a
short Alembic reaction and spawns a chemical hazard. The design notes
below stay as the reference for the visual and radius decisions.

Per `GTG_REPO_CONVENTIONS.md` §5: anything Netcode for Entities needs to
predict, sync, or roll back has to be ECS; anything purely local and
cosmetic can stay MonoBehaviour. A reaction's *hazard* (does it damage,
slow, or poison a player) is squarely the first kind. Its *visual*
(particles, decals, shaders) is squarely the second. The design below
keeps those two as separate as the convention already asks for.

## Design notes (pre-implementation)

### Visual feedback: Particle System + Shader Graph, not VFX Graph

VFX Graph earns its keep at GPU-simulated, hundreds-of-thousands-of-
particles scale. A reaction's visual beat is a few hundred particles at
most -- Shuriken's actual home turf, CPU-simulated, which is what you
want on hardware with no reliable compute shader path. Checked against
the actual project: Unity 2022.3.13f1, URP 14.0.9.

**The data bridge.** `ChemistryLib` already exposes `chem_temperature()`
and `chem_kinetic_energy()` (coarse "how energetic does this look"),
per-bond `BondGeometry.Strain` (a "how close to snapping" driver), and,
as of this batch, `chem_take_bond_events` -- a formed/broken/order-
changed feed, exactly the discrete trigger a VFX burst wants instead of
polling strain every frame.

**Shuriken modules, mapped to archetypes:**
- **Explosive** (Na+H2O): burst emission fired on the reaction's
  `BondEvent`, Sub Emitters (Birth) for a secondary spark burst, a short
  Force-over-Lifetime kick outward, Trails for streaking embers.
- **Effervescent/fizzing** (CaC2+H2O): continuous low-rate emission,
  Shape set to the reacting surface, Noise for irregular bubble drift, a
  small grow-then-pop Size-over-Lifetime curve.
- **Gas/vapor/boil-off**: Shape = cone off the surface, near-zero
  gravity, Noise for billowing -- the Shader Graph dissolve trick below
  does the actual "fades as it disperses" work.
- **Surface/contact reactions**: `OnParticleCollision` as the trigger for
  a decal or localized effect at the hit point, rather than the particles
  themselves doing surface damage.

**Shader Graph techniques (URP 14):**
- Additive blend for fire/glow/energy, Alpha for smoke/gas, Premultiply
  for translucent liquid.
- Flipbook UV -- baked animated sprite sheets. The cheap, convincing route
  for fire/smoke specifically; prioritize over procedural per-pixel noise
  given the rig this has to run on.
- Vertex Color / Custom Interpolator blocks -- how per-particle Custom
  Vertex Stream data (set on the `ParticleSystemRenderer`) actually
  reaches the shader. Landed in Shader Graph 12 (Unity 2021.2), so
  available here.
- Gradient node keyed by a 0-1 "heat" value -> a blackbody-style ramp
  (dark red -> orange -> white -> pale blue). One reusable "how hot/
  reactive is this" visual language across every compound.
- Alpha Clip + a noise texture -- the dissolve/evaporation look.
- Fresnel Effect node -- rim glow for gas bubbles or an energy field.
- Distortion (Scene Color node, needs Opaque Texture on) -- heat haze.
  Real cost (extra copy pass); reserve for one hero moment, not an
  ambient default.

**Performance discipline for the target hardware:** `MaterialPropertyBlock`
never `material.Set*` per-instance (the latter silently instances the
material and kills GPU instancing); enable GPU Instancing in the Shader
Graph's Graph Settings and on the Particle System Renderer; sprite sheets
over procedural shader math wherever both are an option; pool a small
fixed set of `ParticleSystem` GameObjects per reaction archetype rather
than `Instantiate`/`Destroy` per event; keep max-particles low (tens, not
thousands) per burst.

### Effect radius: how big is a reaction's hazard footprint

The core rule: **the hazard footprint is gameplay state, decided once,
explicitly -- every visual reads from it, nothing ever derives it from a
visual.** Concretely, a `ChemicalHazard` (name placeholder)
`IComponentData`, spawned by whichever system resolves "a recipe just
triggered": `origin`, `currentRadius`, `maxRadius`, `hazardType`,
`spawnTime`, `duration`, `growDuration`. This lives in ECS because it
affects players (damage, slow, poison) -- exactly the "needs to predict/
sync/rollback" category from `GTG_REPO_CONVENTIONS.md` §5, unlike the
particles/decals/shaders reading it, which stay MonoBehaviour/Companion-
GameObject presentation.

**Where the number actually comes from.** Alembic's own sim runs at
Angstrom/molecule scale; gameplay runs at meter scale. There's no
physically-consistent conversion between the two -- it has to be a
designed mapping, not a derived one. Concretely: each recipe gets an
authored base radius (a designer-set value, e.g. "Sodium + Water:
3m, Explosion"), optionally scaled by a designed, sub-linear function
(sqrt or log, not linear -- doubling the reagent shouldn't double the
blast radius) of how much reagent quantity the player actually used.
Alembic's own reaction event (the `BondEvent`s a recipe's completion
produces) is the *trigger* -- confirmation this recipe fired, and
optionally which specific atoms/molecules to key the quantity scale off
of -- not the source of the radius number itself. The atom count Alembic
actually simulates for a recipe is a stylized sample, not a real physical
quantity of reagent; fitting gameplay balance to it directly would be
fitting balance to an implementation detail.

**Different archetypes need different time behavior, not just different
radii:**
- **Explosion**: instantaneous. `currentRadius` is set to `maxRadius` at
  spawn, hazard applies once (a single damage/knockback pass) or ticks
  briefly then despawns. Visual appears at full radius immediately.
- **Freeze**: grows over `growDuration`, `currentRadius` animating 0 ->
  `maxRadius` (a lerp or `AnimationCurve` evaluated inside the ECS
  system itself, so it's the deterministic, network-safe value, not a
  presentation-side guess). This is the direct mechanism for "a freezing
  visual that extends all the way here": a Decal Projector or ground
  shader reads `currentRadius` every frame and sets its own reveal-mask
  distance-from-center threshold to match it exactly -- the visual is a
  function of the mechanical radius, never animated on its own timeline.
- **Poison gas**: wants actual dispersal/dissipation, ideally affected by
  environment (wind, enclosed space) -- a single scalar radius under-
  serves this. The honest answer is a small coarse density grid (a fixed
  array of cells around the origin, each tick bleeding a fraction of its
  density to neighbors and losing some to decay), with "radius" really
  meaning "extent of cells above a minimum concentration." Bigger scope
  than the other two archetypes; a symmetric grow-then-shrink
  `currentRadius` (same shape as Freeze, mirrored) is a reasonable v1
  placeholder if the full grid isn't worth building yet.

**Debug visualization.** Draw `origin`/`currentRadius` as a wire-sphere
Gizmo while tuning. This is what catches "the VFX artist scaled the
particle system up because it looked better" quietly implying a bigger
hazard than the actual collider -- cheap, easy to forget, worth doing
from the start.

**Where the recipe -> radius table lives.** Alembic itself has no
concept of meters or game balance and shouldn't gain one. The mapping is
game-side Gtg.Chemistry data -- a per-recipe authored asset, keyed by
whatever identifies a recipe (the specific compound/reactant pair, or a
recipe ID Gtg.Chemistry defines on top of Alembic's raw atom-level
events).

## Modules

Files are in `Assets/MidManStudio/Gtg/Chemistry/`. The assembly references
`MidManStudio.Alembic`, so `com.midmanstudio.alembic` and its
`com.midmanstudio.mdix` dependency must be installed or the project will not
compile. Spell impacts come from `GrandTheftGrimoire/magic.md`.

### `ChemistryComponents.cs`

`ChemicalHazard` and `HazardType`. The fields are the ones from the effect
radius note above, plus `AlembicConfirmed`, which records whether the sim
reported a formed bond for the recipe. Only `Explosion` is implemented, `Gas`
and `Fire` are reserved names.

### `ChemistryRecipe.cs`

Game side description of one reaction: which atoms Alembic simulates, the
temperature and step budget, and the authored hazard numbers. Values live in
code for now and move to an mdix table later, matching the note above that
the recipe to radius table is game-side data.

Decisions:

- The fireball recipe is two O plus 2 H groups, six atoms. Bonds in Alembic
  form when a pair sits between 1.0 and 1.15 times its `r_min` (the
  `BondParams` defaults), so each O to H pair is spawned at 1.08 times
  `chem_bond_r_min`, inside that window.
- 1000 K, 1 fs steps, 10 Angstrom cutoff, at most 32 steps. The step and
  cutoff values are the Playground sample defaults.
- The radius is the authored base radius. `RadiusFor` applies a square root
  to a reagent quantity, as the note above asks, and a spell cast has
  quantity 1. The sim's own atom or bond count is deliberately not used,
  because it is a stylized sample and not a physical amount.
- These numbers are unproven against the real library. The first log line
  after an impact shows what Alembic actually did, and the recipe is the
  place to tune.

### `ChemistryReactionSystem.cs`

Managed system that reads the spell impact buffer and, for each impact, runs
the recipe in Alembic and then creates a `ChemicalHazard` at the hit point.
Alembic is the trigger and confirmation, and it does not choose the radius.

Decisions:

- One persistent native context, created on first use and destroyed with the
  system. Each reaction spawns its atoms, initializes velocities, steps until
  a bond forms or the step budget ends, then despawns its own atoms.
- The reaction runs inside the frame of the impact. The cost is bounded by
  the step budget and six atoms.
- Bond events are read after each step and the formed ones are counted.
  Alembic's own note recommends one read per frame, but reading more often
  only splits events across reads, and the counts are summed here.
- The native library can be missing or fail to load. The system checks
  `ChemistryLib.IsAvailable` and the struct sizes once, logs, and carries on.
  An unconfirmed reaction still spawns its hazard, marked unconfirmed, so
  gameplay can be tested without the library. `RequireConfirmation` on the
  recipe turns that into a hard requirement.
- Not Burst compiled, since P/Invoke calls cannot be.

### `ChemicalHazardSystem.cs`

Sets `CurrentRadius` from the hazard's age and removes it when its duration
ends. The Explosion recipe has a grow time of zero, so it is at full radius
on the frame it spawns, as the archetype note above describes. The radius is
computed here and not in the presentation, so it stays a simulation value.
Time comes from the variable rate world clock, which Phase 1 networking will
replace with the tick.

### `ChemicalHazardPresentationSystem.cs`

Draws each hazard as a translucent sphere sized from `CurrentRadius`. Orange
means Alembic confirmed the reaction, yellow means it did not. Because it
reads only the simulation radius, it also works as the debug view that the
note above asks for.

### `MidManStudio.Gtg.Chemistry.asmdef`

References the Magic assembly (for the impact event), Alembic, Entities,
Transforms, Mathematics, Burst and Collections.

## Known gaps

- Hazards do not affect anything yet. There is no damage, slow or knockback.
- Only the Explosion archetype exists. Freeze and Gas need their own time
  behavior.
- Hazard time uses the variable rate clock and is not network safe.

## CI and Workflows

None yet. Nothing here is built or tested by CI.

## Fixes and Problems

None yet. Not compiled or run against the Editor at the time of writing.
