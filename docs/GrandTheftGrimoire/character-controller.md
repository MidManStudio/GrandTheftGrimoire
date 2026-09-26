# CharacterController

Custom kinematic character controller, built on ECS and (once Phase 1
lands) networked with Netcode for Entities. See
`GTG_REPO_CONVENTIONS.md` §5 for why this system is ECS rather than
MonoBehaviour, and the story/gameplay design references for what the
character needs to eventually do (melee, chemistry-thrown weapons,
Excalibur, summoned weapons — gameplay-reference.md §12).

## Current phase

**Phase 0 — local movement only, no networking.** The goal of this
phase is narrow on purpose: prove the kinematic movement feels right
for one local player before adding Netcode for Entities' ghosting and
prediction on top of it. Mixing "does this feel good" with "is this a
valid networked system" at the same time is a bad way to learn either
one.

Phase 1 (next) replaces `CharacterInputSystem` with per-connection
networked input (`IInputComponentData`), adds ghost components for
whatever needs to sync (position, velocity, grounded state), and moves
this system's simulation into NFE's prediction loop. The movement math
in `CharacterMovementSystem` itself shouldn't need to change much —
only how its inputs arrive and how its outputs get replicated.

## Modules

- **Components/CharacterComponents.cs** — all ECS data for the
  character: tag, tunable move settings (baked once, read-only),
  per-frame input, vertical velocity, grounded state.
- **Authoring/CharacterAuthoring.cs** — Editor-only `MonoBehaviour` +
  `Baker`. Drop on a GameObject inside a SubScene; the GameObject
  itself doesn't exist at runtime.
- **Systems/CharacterInputSystem.cs** — polls the local
  keyboard/mouse/gamepad via the new Input System and writes the result
  into every `CharacterInput` in the world. Correct for exactly one
  local player, which is all Phase 0 needs.
- **Systems/CharacterMovementSystem.cs** — ground check via a Unity
  Physics raycast, gravity/jump on the vertical axis, direct position
  move on the horizontal plane. Movement is world-axis-relative, not
  camera-relative, until the Cinemachine presentation layer exists to
  make "camera-relative" mean something.

## Known gaps at this phase

- No camera, so there's nothing to test movement feel against except
  the Scene view. Presentation layer (Cinemachine rig, companion
  Animator) is intentionally out of scope for Phase 0.
- No slopes/steps handling in the ground check — it's a single straight
  ray, not a shape cast. Fine for flat test geometry, will need
  revisiting before real terrain.
- Written against the documented Entities 1.x / Unity Physics /
  new Input System APIs but not compiled against the actual project —
  there's no Unity Editor in the environment these files were written
  in. Treat the first Editor open as the real test.

## Fixes and Problems

_(none yet — first real Editor run hasn't happened)_
