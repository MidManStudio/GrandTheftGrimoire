# CharacterController

Custom kinematic character controller, built on ECS and (once Phase 1
lands) networked with Netcode for Entities. See
`GTG_REPO_CONVENTIONS.md` section 5 for why this system is ECS rather than
MonoBehaviour, and the gameplay design reference for what the character
needs to do later (melee, chemistry-thrown weapons, Excalibur, summoned
weapons).

## Current phase

**Phase 0: local movement only, no networking.** The goal is narrow on
purpose: prove the kinematic movement feels right for one local player
before adding Netcode for Entities ghosting and prediction on top of it.

Phase 0 now covers mouse and stick look, yaw-relative movement, a fixed
ground check, and the fire input that the Magic system reads. The camera
lives in `camera.md`, and the fireball in `magic.md`.

Phase 1 replaces `CharacterInputSystem` with per-connection networked input
(`IInputComponentData`), adds ghost components for position, velocity and
grounded state, and moves the simulation into the prediction loop. The
movement math should not change much, only how inputs arrive and how
outputs replicate.

## Modules

Files are flat in `Assets/MidManStudio/Gtg/CharacterController/`. The
`Components/`, `Systems/` and `Authoring/` split from the repo conventions
is pending, because moving files needs a move commit and cannot be done
through a replacements drop.

### `CharacterComponents.cs`

All ECS data for the character: tag, baked settings, per-frame input, look
angles, vertical velocity and ground state. `CharacterLook` stores yaw and
pitch in degrees with the Unity sign convention (positive pitch looks down),
so the camera rig and the spell aim read the same numbers. `CharacterInput.Look`
is already in degrees for the frame, so the systems after the input system
never see device units.

### `CharacterAuthoring.cs`

Editor-only `MonoBehaviour` and `Baker`. It goes on an empty GameObject
inside a SubScene. The pivot of that GameObject is the character's feet, and
the ground ray and the muzzle offset both measure from it. The starting yaw
comes from the GameObject's Y rotation. The body mesh is not part of this
object, it lives outside the SubScene and is moved by the camera bridge.

### `CharacterInputSystem.cs`

Polls keyboard, mouse and gamepad through the new Input System and writes
the result into every `CharacterInput`. Not Burst compiled, since it reads
managed Input System objects. Mouse look and mouse fire count only while the
cursor is locked, so the click that captures the cursor does not also fire.
Fire is left mouse, F, or right trigger. Replaced, not extended, in Phase 1.

### `CharacterLookSystem.cs`

Adds the frame's look change to yaw and pitch, clamps pitch to the baked
limits, and rotates the entity to the yaw. Pitch is stored only, the body
stays upright.

### `CharacterMovementSystem.cs`

Ground check by ray, gravity, jump, and a direct position move. Movement is
relative to the current yaw. The ray starts `GroundSkin` above the feet and
reaches `GroundCheckDistance` below them, plus the distance the character
will fall this frame, so a fast fall cannot step through thin ground. Ground
is ignored while the vertical speed is positive. When grounded and not
jumping, the feet snap to the hit height.

### `PhysicsRayUtility.cs`

Casts a ray and returns the closest hit that does not belong to a given
entity. The ground check and the fireball both use it, so the caster never
hits itself if a collider is ever baked onto it.

### `MidManStudio.Gtg.CharacterController.asmdef`

References Entities, Entities.Hybrid, Transforms, Physics, Mathematics,
Burst, Collections and InputSystem. `Baker<T>` lives in
`Unity.Entities.Hybrid`, so authoring code does not compile without it.

## Known gaps at this phase

- Ground check is a single straight ray, not a shape cast. There is no
  slope or step handling. Fine for flat test geometry.
- The character has no collider of its own. Other systems that need to hit
  the player will need one added later.
- Written against the documented Entities 1.x, Unity Physics and Input System
  APIs and checked against Unity's official samples, but not compiled in this
  environment. The first Editor open is the real test.

## CI and Workflows

- `.github/workflows/apply-replacements.yml` applies drops from
  `.mdix/replacements/`. Nothing here is built or tested by CI yet.

## Fixes and Problems

### Editor startup errors (Burst, world initialization)

The console at first Play showed four kinds of message. None of the stack
frames were GTG code, every failing system was a Unity package system.

- Many warnings of the form "Ignoring invalid [UpdateAfter] attribute"
  on Unity.Scenes, Unity.Entities and Unity.Physics systems. These are a
  consequence of the next item: the target systems were never created, so
  the sorter cannot find them.
- A `JobTempAlloc` leak warning. Also a consequence of the failed startup.
- Repeated `InvalidOperationException: Illegal instruction executed`,
  thrown from Burst compiled code. The path is
  `World.GetOrCreateSystemsAndLogException`, then a system creation that
  fails, then the cleanup that destroys the system entity, which crashes in
  the Burst compiled `ChunkDataUtility.RemoveFromEnabledBitsHierarchicalData`.
  The traces show the same crash for each failing system creation, so it
  is not tied to one system.
- `ArgumentException: The entity does not exist` in
  `EditorSubSceneLiveConversionSystem.OnUpdate`. Its system entity was
  destroyed by the same failed cleanup.

Status: root cause not yet confirmed. Working theory is Burst code
generation on the development machine, a mid 2010 MacBook Pro whose CPU has
no AVX and an older SSE level. "Illegal instruction executed" from Burst
code is the reported symptom of that class of problem, and reports of the
same message say the code runs with Burst switched off.

Check: turn off `Jobs > Burst > Enable Compilation`, enter Play, and read
the console. A clean console confirms the theory. Then leave Burst off for
Editor work on that machine, and for player builds restrict the Burst target
CPU list under `Project Settings > Burst AOT Settings` to SSE2 and SSE4.

Version note: the project uses editor 2022.3.13f1 with Entities and Physics
1.3.10. The package changelogs raise the editor minimum in a later 1.3.x
release, so if the packages are upgraded the editor has to be upgraded with
them. A newer 2022.3 patch is also a cheap second thing to try if the Burst
check does not clear the errors.

The raw log that used to sit in this file was about 1,660 lines of repeated
traces and was removed. The messages above are the complete set of distinct
ones.

### `CharacterMovementSystem.cs`

- The ground ray started at the entity pivot and reached only 0.2 m down.
  With the pivot in the middle of a capsule the ray never reached the floor,
  so the character fell forever. Fixed by making the pivot the feet, starting
  the ray at a small skin height above them, and snapping to the hit.
- A jump was cancelled one frame after takeoff, because the ray still saw the
  floor and reset the vertical speed. Fixed by ignoring ground while rising.
- A fall faster than the ray length could step through thin ground. Fixed by
  extending the ray by the fall distance of the frame.
- A collider on the character would have made the ray hit the character
  itself. Fixed by ignoring the caster's own entity.
- Movement was world-axis relative. It is now relative to yaw.

### `CharacterAuthoring.cs` and `MidManStudio.Gtg.CharacterController.asmdef`

- The asmdef did not reference `Unity.Entities.Hybrid`, where `Baker<T>`
  is defined. The nested `Baker` class then binds to itself and the compiler
  reports CS0308. Fixed by adding the reference.

### `CharacterInputSystem.cs`

- Look was passed on in device units. It is now converted to degrees at
  input time, using the baked sensitivity.
- Mouse look was live while the cursor was free. It is now ignored unless
  the cursor is locked.
