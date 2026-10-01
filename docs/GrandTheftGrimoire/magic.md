# Magic (`MidManStudio.Gtg.Magic`)

Spell casting and projectiles. The first spell is a fireball that flies
straight, used to test the whole path from input to a chemical hazard. What
happens after a hit is handled by the Chemistry system, see `../chemistry.md`.

## Overview

Casting and flight are ECS, because they are part of the simulation that
Netcode for Entities will need to predict. Visuals are placeholder GameObjects
driven from ECS state. The two sides meet at impact entities that Chemistry
reads. Both systems follow the system structure in `GTG_REPO_CONVENTIONS.md`: an
`ISystem` that schedules a nested `IJobEntity`.

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
