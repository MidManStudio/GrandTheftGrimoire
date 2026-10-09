# Magic (`MidManStudio.Gtg.Magic`)

> Code location: `Assets/MidManStudio/Gtg/ECS/Magic/`. The ECS sources compile only when `GTG_ECS` is defined. A MonoBehaviour version lives under `Assets/MidManStudio/Gtg/Managed/Magic/`, see `managed.md`.

Spell casting and projectiles. The first spell is a fireball that flies
straight, used to test the whole path from input to a chemical hazard. What
happens after a hit is handled by the Chemistry system, see `../chemistry.md`.

## Overview

Casting and flight are ECS, because they are part of the simulation that
Netcode for Entities will need to predict. Visuals are placeholder GameObjects
driven from ECS state. The two sides meet at impact entities that Chemistry
reads. Both systems follow the system structure in `GTG_REPO_CONVENTIONS.md`: an
`ISystem` that schedules a nested `IJobEntity`.

The design notes below record how spells fly. The Managed stack has the SP drive
described there. The ECS stack does not, and still flies one straight shot with a
lifetime.

## Design notes

### Spell flight: SP drive

Status: built on the Managed stack, see `ManagedSpellFlight.cs` in `managed.md`. Not
built on the ECS stack. Every number is a placeholder.

**Locked.**
- A spell that flies on its own is powered by SP, held in the crystal of its vessel
  (bottle or orb). The options that were considered and not chosen: thrust from the
  payload's gas and heat, thrust now with SP steering later, and throw or place only.
- The mix changes how much SP flight costs, but only the part of its load above the
  vessel's rating costs extra. A mix at or under the rating flies the vessel's normal
  range.
- When the SP runs out in flight, the bubble collapses where it is and the payload
  releases there.
- Bottles are single-use. Orbs are rechargeable.
- The demo fireball stays an SP-driven bolt. This replaces the demo plan's line that
  the demo bottle is thrown only.

**Proposed, built, not canon until approved.**
- One SP pool per vessel. Flight burns it through a hold cost per second and a drive
  cost per meter. The hold cost is a vessel base plus a tuning constant times the load
  above the vessel's rating. The drive cost is derived from the spell's authored flight
  time. Range is the pool divided by the burn, and a hard cap on flight time stays as a
  safety net.
- Each payload carries one number, its load: how hard the mix pushes on the bubble.
  Each vessel carries a rating on the same scale. The authored loads are placeholders
  until the outcome classifier produces them from energy released, peak temperature and
  gas produced.
- The collapse point is interpolated inside the step, so range does not depend on the
  frame rate.
- A flying spell is a record of position, velocity, SP, profile id, payload id and
  owner, kept in dense arrays with no class reference, the flat-array layout of the
  UnityDodNoEcs reference repo. A straight powered shot is a pure function of that
  record until it hits something, so netcode only has to replicate the spawn and the
  impact for those.
- Flight is a module of its own. It reads no spell definition and no payload, only the
  numbers in its spawn record, so another caster or the ECS port can reuse it.
- Impact carries the payload id and the chemistry side applies the authored footprint,
  see `chemistry-simulation.md`.
- A thrown spell uses the same integrator with the drive off and gravity on. The path
  exists, and no spell uses it yet.
- Drawing goes through `ManagedSphereBatch`, which has the instanced path and the
  combined mesh fallback. Anything new that draws with `DrawMeshInstanced` gets a
  fallback the same way, because the development machine cannot run it. A shot dims and
  shrinks as its SP drains.

**Proposed, not built.**
- Steering as a turn rate on the same drive, 0 for a straight shot, so homing or curved
  flight is a table row and not a new system.
- Vessel items. Until they exist a stand-in vessel, full at every cast, supplies the SP.
- Payloads from the cook. The per-impact Alembic run goes away once they exist, which
  needs two Alembic jobs first: a restricted element set and an observables report
  (section 3 of the chemistry system design draft).

**Open.** How an orb recharges: from a crystal item, at a station, or over time.

The chemistry system design draft (`gtg-chemistry-system-design.md`, section 6)
and the demo plan are not in this repo.

## Modules

Files are in `Assets/MidManStudio/Gtg/Magic/`.

### `SpellComponents.cs`

`SpellCaster` holds cooldown and shot tuning per character. `FireballProjectile`
holds velocity, remaining life and the owning entity. `SpellImpact` is one hit.
Each impact is its own short lived entity, created through a command buffer by
the projectile job and destroyed by the chemistry system after it has been
handled. An entity per impact replaced a shared event buffer. A buffer needs a
singleton created in `OnCreate`, and a system that changes entities while being
created is exactly what fails at startup on the development machine, see
`generalprobs.md`.

### `SpellCasterAuthoring.cs`

Authoring for `SpellCaster`. It goes on the same GameObject as
`CharacterAuthoring`. The muzzle offset is in the character's yaw space,
measured from the feet.

### `FireballCastSystem.cs`

The cast module, gated by the `Cast` feature of the character. A job counts the
cooldown down and, when fire was pressed and the cooldown is over, creates a
projectile entity through a parallel command buffer. The aim direction comes from
`CharacterLook`, so first and third person shoot the same way. The shot goes
along the look direction from the muzzle, not toward the point under the third
person crosshair. That is a known simplification.

### `FireballProjectileSystem.cs`

A job moves each projectile along its velocity. Each step is a ray from the old
position to the new one, so a fast shot cannot pass through a thin collider. The
ray ignores the caster, which matters now that the character has a collider. A
hit creates an impact entity and destroys the projectile. Running out of life
destroys it without an impact.

### `FireballPresentationSystem.cs`

Managed system in the presentation group. Keeps one pooled glowing sphere per
projectile entity and moves it to the entity position. Views for entities that no
longer exist go back to the pool. It runs every frame, including when no
projectile exists, or the last sphere would never be released.

### `SpellVfxMaterials.cs`

Builds unlit test materials. It looks for the URP Unlit shader, then
Sprites/Default, then Unlit/Color, and returns null if none exists. Placeholder
until real particle and shader assets replace it.

## Known gaps

- One spell, one cooldown, no mana or ammunition.
- Hit detection is a ray, so a fireball has no thickness.
- The fireball does not damage anything yet. Its only effect is the chemical
  hazard that Chemistry spawns.
- Flight on this stack is a straight line with a lifetime. The SP drive model in the
  design notes is built on the Managed stack only.

## CI and Workflows

None yet.

## Fixes and Problems

### `FireballProjectileSystem.cs`

- `OnCreate` created an event entity and a buffer. That structural change failed
  at startup, the system was never created, and the log reported
  `ChemistryReactionSystem` ordering against a missing system. The system no
  longer changes entities in `OnCreate`.
- With the character now owning a collider, the hit ray would have stopped on the
  caster. The ray ignores the owner entity.
