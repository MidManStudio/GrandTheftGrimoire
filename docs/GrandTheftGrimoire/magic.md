# Magic (`MidManStudio.Gtg.Magic`)

Spell casting and projectiles. The first spell is a fireball that flies
straight, used to test the whole path from input to a chemical hazard. What
happens after a hit is handled by the Chemistry system, see `../chemistry.md`.

## Overview

Casting and flight are ECS, because they are part of the simulation that
Netcode for Entities will need to predict. Visuals are placeholder
GameObjects driven from ECS state. The two sides meet at one buffer of
impact events that Chemistry reads.

## Modules

Files are in `Assets/MidManStudio/Gtg/Magic/`.

### `SpellComponents.cs`

`SpellCaster` holds cooldown and shot tuning per character. `FireballProjectile`
holds velocity, remaining life and the owning entity. `SpellImpactEvent` is a
buffer element on a single event entity. The projectile system clears that
buffer at the start of every frame, so a consumer has to run after it in the
same frame. That gives every consumer the same view without any acknowledge
step.

### `SpellCasterAuthoring.cs`

Authoring for `SpellCaster`. It goes on the same GameObject as
`CharacterAuthoring`. The muzzle offset is in the character's yaw space,
measured from the feet.

### `FireballCastSystem.cs`

Counts the cooldown down, and when fire was pressed and the cooldown is over,
creates a projectile through the end of simulation command buffer. The aim
direction comes from `CharacterLook`, so first and third person shoot the same
way. The shot goes along the look direction from the muzzle, not toward the
crosshair point of the third person camera. That is a known simplification.

### `FireballProjectileSystem.cs`

Moves each projectile along its velocity. Each step is a ray from the old
position to the new one, so a fast shot cannot pass through a thin collider.
The ray ignores the caster. A hit records an impact event and destroys the
projectile. Running out of life destroys it without an event. It also creates
the impact event entity in `OnCreate`.

### `FireballPresentationSystem.cs`

Managed system in the presentation group. Keeps one pooled glowing sphere per
projectile entity and moves it to the entity position. Views for entities that
no longer exist go back to the pool. Runs every frame, including when no
projectile exists, or the last sphere would never be released.

### `SpellVfxMaterials.cs`

Builds unlit test materials. It looks for the URP Unlit shader, then
Sprites/Default, then Unlit/Color, and returns null if none exists.
Placeholder until real particle and shader assets replace it.

### `MidManStudio.Gtg.Magic.asmdef`

References the CharacterController assembly, Entities, Entities.Hybrid,
Transforms, Physics, Mathematics, Burst and Collections.

## CI and Workflows

None yet.

## Fixes and Problems

None yet. Not compiled or run against the Editor at the time of writing.
