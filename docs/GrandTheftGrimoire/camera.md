# Camera (`MidManStudio.Gtg.Camera`)

> Code location: `Assets/MidManStudio/Gtg/ECS/Camera/`. The ECS sources compile only when `GTG_ECS` is defined. A MonoBehaviour version lives under `Assets/MidManStudio/Gtg/Managed/Camera/`, see `managed.md`.

Presentation layer for the character. The simulation stays in ECS, and
everything the player sees is ordinary GameObjects that copy the ECS state
each frame.

## Overview

Entities are not rendered directly. The project has no Entities Graphics
package, and the development machine has no reliable compute shader path, so a
mesh baked into a SubScene would simply not draw. This follows the companion
GameObject rule in `GTG_REPO_CONVENTIONS.md`. The visible body is a capsule
outside the SubScene, moved to the entity each frame. The character entity
itself carries only the collider and the simulation data.

Two Cinemachine virtual cameras share one Follow target, the camera target
object. The third person camera sits behind and to the side of it, and the first
person camera sits on it. One key press swaps them. The scripts do not
reference Cinemachine at all, the bridge only turns the two camera
GameObjects on and off, so they compile with Cinemachine 2.9.7 as installed and
would also compile with a later version.

## Modules

Files are in `Assets/MidManStudio/Gtg/Camera/Presentation/`.

### `CharacterCameraBridge.cs`

A `MonoBehaviour` that runs in `LateUpdate` at execution order 100, before the
Cinemachine Brain at 200. Each frame it reads the character's `LocalTransform`,
`CharacterLook` and `CharacterMotor` from the default world and does three
things.

- Moves the visible body capsule to the character, with a fixed offset for the
  capsule pivot, and turns it to the yaw.
- Moves the camera target to the eye point and gives it the yaw and pitch. Both
  Cinemachine cameras read this one transform.
- Draws the debug overlay.

It also owns the view switch (V or right stick click, or the public
`ToggleFirstPerson` method for a UI button) and cursor capture (Esc releases, a
click captures). The switch turns the two camera GameObjects on and off, and
Cinemachine blends between them with the Brain's default blend. In first person
the body renderers switch to shadows only, so the camera is not inside the mesh
but the shadow remains.

The overlay is the main tool for finding out why nothing moves. It reports
whether the physics world and the command buffer singletons exist, which
character systems were not created, whether the character entity was found and
has a collider, and the position, grounded flag and vertical speed. Below it a
row of toggles switches the Look, Move, Jump, Gravity and Cast modules on and off
while the game runs, by writing `CharacterFeatures` on the entity.

The bridge finds the character with a query for exactly one entity with the
character tag. Zero or several entities are reported in the overlay and do not
throw.

Inside `MidManStudio.Gtg` namespaces the simple name `Camera` resolves to this
namespace. Write `UnityEngine.Camera` in full there.

## Cinemachine setup, version 2.9.7

Entered in the Editor.

- Third person camera: a Virtual Camera with Follow set to the camera target,
  Body set to 3rd Person Follow, and Aim set to Do Nothing. The 3rd Person Follow
  body keeps the camera rotation parallel to the Follow target, so the aim comes
  from the target, which the bridge rotates with yaw and pitch.
- First person camera: a Virtual Camera with Follow set to the camera target,
  Body set to Hard Lock To Target, and Aim set to Same As Follow Target.
- The Brain on the main camera gets a default blend of Ease In Out, 0.25 s.

## Known gaps

- The 3rd Person Follow body avoids obstacles using classic physics. The
  world colliders live in the SubScene as ECS colliders, so the camera passes
  through walls for now.
- The overlay is IMGUI and is meant to be removed or hidden in builds.

## CI and Workflows

None yet.

## Fixes and Problems

### `CharacterCameraBridge.cs`

- The bridge read `CharacterGroundState`, which no longer exists. It reads
  `CharacterMotor` instead.
- The overlay used to say only whether a character entity was found. A missing
  physics world or command buffer looked the same as a working game, so the
  missing parts are now listed.
