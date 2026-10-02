# CharacterController

> Code location: `Assets/MidManStudio/Gtg/ECS/CharacterController/`. The ECS sources compile only when `GTG_ECS` is defined. A MonoBehaviour version lives under `Assets/MidManStudio/Gtg/Managed/CharacterController/`, see `managed.md`.

Custom kinematic character controller, built on ECS and (once Phase 1
lands) networked with Netcode for Entities. See `GTG_REPO_CONVENTIONS.md`
section 5 for why this system is ECS rather than MonoBehaviour, and the
gameplay design reference for what the character needs to do later (melee,
chemistry thrown weapons, Excalibur, summoned weapons).

## Overview

The controller is a set of small modules that share one data component. Each
module is its own system, gated by a flag on the character, so a behavior can
be switched off at runtime or deleted from the project without touching the
others. The structure follows the feature flags and extension hook points of
`boaheck/TheFirstPerson`, and the collision method follows Unity's Character
Controller package, without using either as a dependency.

The character is a kinematic body. It has a `CapsuleCollider` baked into a
`PhysicsCollider`, and no rigidbody. Nothing pushes it, the movement system
moves it by sweeping that capsule through the physics world.

Phase 0 is local movement for one player, with no networking. Phase 1
replaces `CharacterInputSystem` with networked input, adds ghost components,
and moves the simulation into the prediction loop. The movement math should
not change much.

## Pipeline and feature flags

Modules run in this order inside the simulation group, each with an
`UpdateAfter` on the one before it:

1. `CharacterInputSystem`, device polling.
2. `CharacterLookSystem`, feature `Look`.
3. `CharacterGroundSystem`, always runs.
4. `CharacterWalkSystem`, feature `Move`.
5. `CharacterJumpSystem`, feature `Jump`.
6. `CharacterGravitySystem`, feature `Gravity`.
7. `CharacterMovementSystem`, the collide and slide step.
8. `FireballCastSystem`, feature `Cast`, in the Magic system.

Every flag is a bit of `CharacterFeatures.Enabled`. The authoring component
has one checkbox per flag, and the dev overlay in the camera bridge flips them
while the game runs. Any system can switch a behavior by writing the flag, for
example a stun clears `Move` and `Jump`, and a flight mode clears `Gravity`.

Extension hook points follow the same idea as the hooks in TheFirstPerson. A
new module is a system ordered between two existing ones, and it edits
`CharacterMotor`. A dash module sits after `CharacterJumpSystem` and before
`CharacterGravitySystem`. Anything that changes the final position goes
before `CharacterMovementSystem`. Adding a module means adding one flag to
`CharacterFeature`, one authoring checkbox, and one system file.

## Modules

Files are in `Assets/MidManStudio/Gtg/CharacterController/`, flat. The
`Components/`, `Systems/` and `Authoring/` split from the repo conventions
is pending, because moving files needs a move commit and a replacements drop
cannot do that.

### `CharacterComponents.cs`

All ECS data for the character: the tag, the `CharacterFeature` flags and
`CharacterFeatures`, the baked `CharacterSettings`, the per-frame
`CharacterInput`, `CharacterLook`, and `CharacterMotor`. `CharacterLook` stores
yaw and pitch in degrees with the Unity sign convention, so a positive pitch
looks down, and the camera rig and the spell aim read the same numbers.
`CharacterInput.Look` is already in degrees for the frame. `CharacterMotor`
carries the horizontal velocity, the vertical velocity, the grounded flag, the
ground normal, and the time since the character last stood on ground. The
modules write it and the movement system reads it.

### `CharacterAuthoring.cs`

Editor-only `MonoBehaviour` and `Baker`. It goes on an empty GameObject inside
a SubScene, and the pivot of that object is the feet. It requires a
`CapsuleCollider`, which Unity Physics bakes into the entity's
`PhysicsCollider` on its own, so the controller needs no `Physics Shape`
component. That component exists only as a sample in the Physics package and
is not part of it. `Reset` shapes a new capsule to 2 m tall, radius 0.5, centre
at half the height. If the collider is changed, keep its centre at half the
height so the pivot stays at the feet. All tuning values and the module
checkboxes are baked here. The starting yaw comes from the object's Y rotation.

### `CharacterInputSystem.cs`

Polls keyboard, mouse and gamepad through the Input System on the main thread,
then a job writes the result into every `CharacterInput`. Not Burst compiled,
since it reads managed Input System objects. Mouse look and mouse fire count
only while the cursor is locked, so the click that captures the cursor does not
also fire. Fire is left mouse, F, or right trigger. Replaced in Phase 1.

### `CharacterLookSystem.cs`

Adds the frame's look change to yaw and pitch, clamps pitch to the baked
limits, and turns the entity to the yaw. Pitch is only stored, the body stays
upright.

### `CharacterGroundSystem.cs`

Sweeps the character's own capsule a short way down, ignoring the character's
own entity, and records whether the surface is walkable by comparing its normal
with the slope limit. It skips the sweep while the vertical speed is positive,
because the sweep would still see the floor just after takeoff and cancel the
jump. It also tracks the time since the last grounded frame, which the jump
module uses for coyote time.

### `CharacterWalkSystem.cs`

Turns the move input into a target velocity relative to the yaw, then moves the
horizontal velocity toward it at the baked acceleration. In the air only the
air control share of that acceleration applies. With the `Move` flag off the
target is zero, so the character glides to a stop instead of freezing.

### `CharacterJumpSystem.cs`

Jumps when the button went down and the character is grounded or left the
ground within the coyote time. A jump sets the time since grounded past the
coyote window, so a second press in the air does nothing.

### `CharacterGravitySystem.cs`

Adds gravity to the vertical speed up to the fall cap. While grounded it holds
a small downward speed so the character follows the floor down slopes and over
small drops. With the `Gravity` flag off the vertical speed is left alone.

### `CharacterMovementSystem.cs`

The collide and slide step. It builds the frame's displacement from the motor
velocities, sweeps the capsule along it, stops a skin width before the surface
it hits, removes the part of the motion that points into the surface, and
repeats up to the baked iteration count. A surface facing up zeroes a downward
vertical speed, and a surface facing down zeroes an upward one. The skin width
keeps the capsule from resting in contact, which would make every later sweep
report a hit at distance zero.

### `PhysicsRayUtility.cs`

Two queries that skip a given entity. `CastRayIgnoring` is used by the fireball.
`CastColliderIgnoring` sweeps the character's collider and also skips surfaces
the sweep is moving away from or along. Both are needed because the character's
own collider is in the physics world.

## References

Three repositories were read for this controller and none is a dependency.

- `Mid-D-Man/CharacterControllerSamples` and the Unity Character Controller
  package it ships with. Taken: the capsule collider and no rigidbody
  authoring, grounding by a collider sweep, a small collision offset, and the
  repeated sweep and slide loop. Not taken: the package itself. Its latest
  version needs Entities 1.3.15 and Unity 2022.3.50f1, newer than this
  project, and it brings a large amount of machinery the game does not need yet.
- `boaheck/TheFirstPerson`. Taken: one flag per feature, a shared state struct,
  hook points between input, movement calculation and the move itself, coyote
  time, and air control. Not yet taken: variable jump height, jump buffering,
  crouch, sprint, slope sliding, momentum.
- `dyrdadev/first-person-controller-for-unity`. Read for its split between an
  input interface and the controller, and its signals for effects such as
  footsteps and head bob. The signal idea is the likely model for presentation
  events later.

## Known gaps at this phase

- No step handling. Stairs and curbs block the character.
- No moving platforms and no pushing of other bodies.
- Slope handling is a limit on walkable angle. Steeper surfaces are slid along.
- The collision query cost grows with the number of sweeps. At four iterations
  and one character it is small.
- Written against the documented Entities 1.x, Unity Physics and Input System
  APIs and checked against the package sources for version 1.3.10, but not
  compiled in this environment. The first Editor open is the real test.

## CI and Workflows

- `.github/workflows/apply-replacements.yml` applies drops from
  `.mdix/replacements/`. Nothing here is built or tested by CI yet.

## Fixes and Problems

### Startup failures in the ECS world

The console errors the project has been logging are covered in
`generalprobs.md`. The short version: the world fails while creating Unity's own
systems, so the physics world and command buffer singletons never exist, and
every system that waits for them never updates. That is why the character did
not move, no gravity showed, and the fireball did not fire. It is not caused by
controller code. Status there.

### `CharacterMovementSystem.cs`

- The first version had no collision except one ray under the pivot. It walked
  through walls and the ray started at the pivot, so with the pivot in the middle
  of a capsule the floor was never reached. Replaced by the capsule sweep with
  the feet as the pivot.
- The first version waited for the physics world and had no way to show that it
  was waiting. The camera bridge overlay now reports the missing singletons.
- A fall faster than the probe length could step through thin ground. The sweep
  now runs along the real motion, so the step cannot skip a surface.

### `CharacterGroundSystem.cs`

- A jump was cancelled one frame after takeoff, because the check still saw the
  floor. Ground is now ignored while the vertical speed is positive.

### `CharacterAuthoring.cs`

- The entity had no collider, which is why nothing could stand on or collide
  with it. It now requires a `CapsuleCollider`, baked by Unity Physics.
- An earlier assembly definition lacked `Unity.Entities.Hybrid`, so `Baker<T>`
  did not resolve and the compiler reported CS0308. Asmdefs were removed from
  the repo, see `GTG_REPO_CONVENTIONS.md`.

### `CharacterInputSystem.cs`

- Look was passed on in device units. It is now in degrees, using the baked
  sensitivity.
- Mouse look was live while the cursor was free. It is now ignored unless the
  cursor is locked.
