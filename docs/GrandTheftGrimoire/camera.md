# Camera (`MidManStudio.Gtg.Camera`)

Presentation layer for the character. The simulation stays in ECS, and
everything the player sees is ordinary GameObjects that copy the ECS state
each frame. The project has no Entities Graphics package, and the target
hardware has no reliable compute shader path, so entities are not rendered
directly. This follows the companion GameObject rule in
`GTG_REPO_CONVENTIONS.md`.

## Overview

Two Cinemachine cameras share one tracking target. The third person camera
follows the target from behind, and the first person camera sits on it. One
key press swaps them. Cinemachine is not referenced from code, the bridge
only switches GameObjects on and off, so the scripts compile before the
Cinemachine package is installed.

## Modules

Files are in `Assets/MidManStudio/Gtg/Camera/`.

### `CharacterCameraBridge.cs`

A `MonoBehaviour` that runs in `LateUpdate` at execution order 100, which is
before the Cinemachine Brain at 200. Each frame it reads the character's
`LocalTransform`, `CharacterLook` and `CharacterGroundState` from the default
world and does three things.

- Moves the visible body capsule to the character, with a fixed offset for
  the capsule pivot, and turns it to the yaw.
- Moves the camera target to the eye point and gives it the yaw and pitch.
  Both Cinemachine cameras read this one transform.
- Shows a small on-screen overlay with the ECS status, position, grounded
  state, view mode and cursor state.

It also owns the view switch (V or right stick click, or the public
`ToggleFirstPerson` method for a UI button) and cursor capture (Esc
releases, a click captures). The switch turns the two camera GameObjects on
and off. Cinemachine blends between them using the Brain's default blend.
In first person the body renderers switch to shadows only, so the camera is
not inside the mesh but the shadow remains.

The bridge finds the character with a query for exactly one entity with the
character tag. Zero or several entities are reported in the overlay instead
of throwing.

Inside `MidManStudio.Gtg` namespaces the simple name `Camera` resolves to
this namespace. Write `UnityEngine.Camera` in full there.

Recommended Cinemachine setup, entered in the Editor:

- Third person camera: Tracking Target is the camera target, Position
  Control is Third Person Follow, Rotation Control is Rotate With Follow
  Target.
- First person camera: Tracking Target is the camera target, Position Control
  is Hard Lock To Target, Rotation Control is Rotate With Follow Target.

### `MidManStudio.Gtg.Camera.asmdef`

References the CharacterController assembly, Entities, Transforms,
Mathematics and InputSystem.

## CI and Workflows

None yet.

## Fixes and Problems

None yet. Not compiled or run against the Editor at the time of writing.
