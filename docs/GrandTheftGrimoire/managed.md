# Managed stack (`MidManStudio.Gtg.Managed`)

## Overview

GTG keeps two implementations of the same gameplay systems side by side.

- `Assets/MidManStudio/Gtg/ECS/` holds the original Entities, Unity Physics and
  Burst version. Every ECS-dependent file is wrapped in `#if GTG_ECS`, so the folder
  compiles to nothing unless that define is set. The sources are frozen, only the
  wrapper and the new location changed.
- `Assets/MidManStudio/Gtg/Managed/` holds a MonoBehaviour version with no ECS,
  Burst or Unity Physics dependency in its own code. It needs only `UnityEngine`,
  the Input System package and, for chemistry, `com.midmanstudio.alembic`.

Why two: the ECS stack does not run on the current development machine, see the
first entry in Fixes and Problems and `generalprobs.md`. Gameplay is built on the
Managed stack now, and the ECS stack stays ready for hardware that can run it.
The ECS folder moved with its `.meta` files, so script GUIDs did not change.

### Working rule for both stacks

- A feature is built and tested on the Managed stack first, because that is the
  stack that runs on the development machine.
- The ECS sources stay frozen. They are edited only for a trivial, low risk
  change, and every such edit is listed in the parity table and in the Fixes and
  Problems section of the ECS doc that covers the file. The ECS packages are not
  installed in the project, so nothing under `ECS/` is compiled or run.
- Every other feature goes into the ECS backlog in the parity table. The backlog is
  ported in one pass when the ECS packages come back and the code can be compiled.
- A Managed feature is documented here, with a `Modules` section per file. Rules and
  numbers that both stacks share, such as the chemistry recipes, are documented once
  in the shared doc and linked from both.

### Namespaces and names

The NPC enums and the native Rust bridge are shared by both stacks. They have no ECS dependency, so `NpcRole`, `DecisionBackend`, `LearningMode`, `NpcAction`, `NPCNativeLib`, `NPCNativeTypes` and `NPCManagedFallback` live in `ECS/NPC/` in the namespaces `MidManStudio.Gtg.NPC.Components` and `MidManStudio.Gtg.NPC.Native`, and the Managed NPC code uses them directly. The one ECS edit this needed is listed in Fixes and Problems.

Managed code lives under `MidManStudio.Gtg.Managed.<System>`, and every public type
carries a `Managed` prefix. Both stacks can therefore compile in one assembly
without a name clash. Inside these namespaces the simple names `Camera` and
`CharacterController` resolve to namespaces, so the Unity components are written as
`UnityEngine.Camera` and `UnityEngine.CharacterController`.

### Switching stacks

Managed only (current state):

1. Remove `com.unity.physics`, then `com.unity.entities`, in the Package Manager.
2. Leave `GTG_ECS` undefined.
3. `com.unity.burst`, `com.unity.collections` and `com.unity.mathematics` stay
   installed, because `com.midmanstudio.alembic` depends on them. The Managed code
   and the Alembic calls it makes run no Burst compiled code. Only
   `BondBatchAdapter` in Alembic has a Burst job, and GTG does not use it.

ECS and Managed together, or ECS only:

1. Add `com.unity.entities` and `com.unity.physics` at 1.3.10 back to the project.
2. Add `GTG_ECS` under Project Settings, Player, Other Settings, Scripting Define Symbols.
3. A scene uses one stack at a time. Remove the other stack's components from it.

A scene object that carries a component from a stack that is not compiled shows a
missing script. Delete it, or switch the define back.

### Scene setup (NPC)

1. On the player object add `ManagedNpcThreatSource`. Add `ManagedHealth` to it as well if the player can die, so a dead player stops being seen.
2. On each NPC add `ManagedNpcBrain`, plus `ManagedHealth` if it can be hurt. Pick the role and backend. A merchant uses role Merchant, backend StateMachine and Can Move off. A goblin uses Enemy and Utility. Put the NPC's forward direction along its local Z axis, because the view cone points along it.
3. Nothing else is needed. The first brain creates a `GTG NPC Director` object. Add a `ManagedNpcDirector` to the scene yourself to change the tick rate or batch limits, or to turn the overlay on.
4. Select an NPC in the Scene view to see its view cone and eye. The cone is yellow while idle and red while it sees a threat.
5. Check the Console at Play start. A missing native library logs one warning and the managed decision takes over. The director overlay says `native` or `managed fallback`.

### Scene setup (Managed)

1. Create an empty GameObject at the feet position, on solid ground. Add
   `ManagedCharacter`, which also adds a `CharacterController` shaped as a 2 m
   capsule, then `ManagedSpellCaster`.
2. Create a second empty GameObject. Add `ManagedCameraRig` and
   `ManagedChemicalHazardField`.
3. Press Play. With no camera assigned, the rig drives the main camera. A Cinemachine
   setup assigns its two camera objects on the rig and points the Follow target at
   the rig's `GTG Camera Target`.
4. Delete the old SubScene, `CharacterAuthoring` and `CharacterCameraBridge` objects.

Controls: move with WASD or the arrow keys, look with the mouse, the right stick or
the keys J and L (turn) and I and K (up and down), jump with Space, cast with the
left mouse button, F or the right trigger. Pick a spell with 1 or 2, Tab, or the
d-pad. V switches the view, Escape frees the cursor.

Shots leave the `GTG Shot Point` child of the character and fly to whatever the
screen center looks at. A white crosshair marks that point. Move the shot point in
the Scene view, or assign a staff or hand bone to the caster.

Draw path: shots and hazards are drawn with `Graphics.DrawMeshInstanced` when the
GPU supports instancing and `MidManStudio/Gtg/SphereUnlit` compiled, otherwise as
one combined mesh. The Console prints the chosen path at Play start, one line for
shots and one for hazards. The `Force Combined Mesh` option on the caster and the
hazard field tests the fallback on any machine.

For a player build, add `MidManStudio/Gtg/SphereUnlit` under Project Settings,
Graphics, Always Included Shaders. The Editor finds it without that.

Unity creates `.meta` files for the new scripts on first import. Commit them.

### Parity with the ECS stack

| System | ECS | Managed |
|---|---|---|
| Look, walk, jump, gravity | one `ISystem` per module, Burst jobs | one component, same order and same tuning values |
| Move keys | WASD and arrows | WASD and arrows |
| Keyboard look (J, L, I, K) | backlog | done |
| Collision | own capsule sweep through Unity Physics | `UnityEngine.CharacterController` |
| Camera | `CharacterCameraBridge` copies the entity to GameObjects | `ManagedCameraRig` reads the component directly |
| Crosshair aim and shot point | backlog | done |
| Fireball | entities, command buffers, presentation system | list of shots swept with a sphere cast, one draw call |
| Ice spell and spell slots | backlog | done |
| Explosion at range end | backlog | done |
| Chemistry | impact entity, hazard entity, presentation system | static event, hazard list, one draw call |
| Ice recipe and Freeze hazard | backlog | done |
| Instanced and combined mesh drawing | backlog, ECS draws through its own presentation | done |
| Health | backlog | done, `ManagedHealth` |
| NPC | decision system only, nothing feeds it and nothing reads it | brain, sight, director done, nothing acts on the decision yet |

The ECS backlog items need the ECS packages installed, so they are ported together.
The arrow keys are the one ECS edit since the split, two lines per axis in
`CharacterInputSystem.cs`.

Networking differs. Netcode for Entities needs the ECS stack. The Managed stack has
no prediction or rollback, and networked play on it would need Netcode for
GameObjects, see `GTG_REPO_CONVENTIONS.md` section 5.

## Modules

### `ManagedCharacterFeature.cs`

**What it does:** Flags enum with one bit per character module: Look, Move, Jump,
Gravity, Cast, plus `All`.

**Decisions:**
- Same module names as the ECS `CharacterFeature`, so a feature toggle means the
  same thing in both stacks.

### `ManagedCharacterInput.cs`

**What it does:** Polls keyboard, mouse and gamepad and returns one
`ManagedCharacterInputFrame` per call.

**Decisions:**
- Devices are read directly, as in the ECS input system.
- Mouse look and mouse fire count only while the cursor is locked, so the click that
  captures the cursor does not also fire.
- Move accepts WASD and the arrow keys at the same time. Opposite keys cancel, and
  the vector is clamped to length 1 so a diagonal is not faster.
- J and L turn, I and K look up and down, at `_keyboardLookDegreesPerSecond`. This is
  the way to aim without a mouse. Arrow keys are not used for look, because they
  move.
- The frame also carries the picked spell slot (keys 1 and 2) and a cycle step (Tab,
  d-pad left and right), which the caster reads.

### `ManagedCharacter.cs`

**What it does:** Kinematic character on `UnityEngine.CharacterController`. Each
frame it reads the input and runs look, walk, jump and gravity, then calls `Move`.

**Decisions:**
- The module order matches the ECS update order, and the defaults match
  `CharacterAuthoring`.
- The ground state comes from the `CollisionFlags` of the move. A rising character
  reports no floor contact, which gives the same "ground ignored while rising"
  behavior as the ECS ground system.
- Floor contact zeroes a downward speed, and gravity then holds the small ground
  stick speed, as in the ECS movement and gravity systems.
- The slope limit and skin width are written to the controller in `Awake`.
- When the character has no renderer, a capsule mesh is added as a child. Its
  collider is disabled before it is destroyed, because `Destroy` runs at the end of
  the frame and the controller would collide with its own body on the first move.
- Execution order is -50, so the caster and the rig read this frame's input.

### `ManagedSpellTypes.cs`

**What it does:** `ManagedSpellKind` (Fireball, Ice) and the `ManagedSpellImpact`
struct.

### `ManagedSpellDefinition.cs`

**What it does:** One tuning record per spell: cooldown, speed, lifetime, sweep
radius, drawn diameter and color. The slot order is the table order, so key 1 picks
the first entry. The values live in code and move to an mdix table later, like the
chemistry recipes.

**Decisions:**
- The sweep radius is a size of its own and is not the drawn diameter. A sphere of
  0.15 m catches a thin collider that a plain ray misses, and the shot still looks
  small.

### `ManagedSpellVfxMaterials.cs`

**What it does:** Builds unlit placeholder materials. It tries the URP Unlit shader,
then `Sprites/Default`, then `Unlit/Color`, and returns null if none exists. Spell
drawing no longer uses it, see `ManagedSphereBatch.cs`. It stays for placeholder
objects.

### `ManagedSpellCaster.cs`

**What it does:** Casts the selected spell. A cast leaves the shot point and flies to
the crosshair target. Each step sweeps a sphere from the old position to the new one
and raises `Impact` on a hit. Shots are drawn through one `ManagedSphereBatch`, with
no GameObject per shot.

**Decisions:**
- The aim ray goes through the screen center of the main camera. The first collider
  on that ray gives the target point, or a point `_aimMaxDistance` away if nothing is
  hit. The shot flies from the shot point to that target, so it lands on the
  crosshair although it leaves the hand. This is the usual third person rule, and it
  works with Cinemachine because the main camera is the one that renders.
- Two cases fall back to the camera direction: a target nearer than
  `_minAimDistance` and a target behind the shot point. Without a main camera the
  caster aims by the character's yaw and pitch.
- The shot point is a child object named `GTG Shot Point`, created in `Awake` at
  `_shotPointOffset` when no transform is assigned. It can be moved in the Scene view
  or replaced by a bone.
- The sweep skips the caster's own colliders. A hit on the caster restarts the sweep
  just past it, up to four times, so the result is the closest foreign collider and
  the character's capsule never swallows a shot.
- A shot point inside a collider cannot sweep correctly, so that cast detonates at
  once.
- A shot that reaches the end of its lifetime detonates where it is
  (`_detonateAtRangeEnd`). This makes a miss visible and also shows the chemistry
  working on open ground. Turn it off for shots that should fizzle out.
- Impact times, positions and the collider name are logged (`_logImpacts`). This
  tells apart "the shot hit nothing" from "the shot hit but nothing was drawn".
- `Impact` is a static event, so the chemistry field needs no reference to the
  caster. It is cleared at `SubsystemRegistration`, because static state survives a
  play session when domain reload is off.
- The batch is filled and drawn in `LateUpdate`.

### `ManagedIcosphere.cs`

**What it does:** Builds the vertices and triangles of a sphere with diameter 1.
Subdivision 1 gives 42 vertices and 80 triangles. Every vertex is on the radius 0.5
and every edge is shared by exactly two triangles.

### `ManagedSphereBatch.cs`

**What it does:** Draws many spheres without a GameObject each. Call `Clear`, `Add`
for each sphere, then `Draw`, once per frame.

**Decisions:**
- This follows the two render paths of `ProjectileRenderer2D` in
  `com.midmanstudio.projectilesystem`: instanced drawing when the hardware supports
  it, one combined mesh otherwise. The package itself is not used, because it
  depends on Netcode for GameObjects, the utilities and netcode packages and the
  Rust simulation.
- The instanced path draws one shared mesh with `Graphics.DrawMeshInstanced`, in
  batches of 1023, with the color per instance from a `MaterialPropertyBlock`.
- The combined path writes every sphere into the vertex array of one dynamic mesh,
  with the color in the vertex color, and draws it with one `Graphics.DrawMesh`.
  Indices are filled once. The bounds are set huge so the mesh is never culled. A
  batch of more than 65535 vertices switches the mesh to 32 bit indices.
- The instanced path needs `SystemInfo.supportsInstancing`, the custom shader and the
  absence of the force flag. Anything else takes the combined path. A custom shader
  that failed to compile is detected with `Shader.isSupported`.
- The fallback shader is `Sprites/Default`. It multiplies by vertex color, so the
  combined path works with it.
- The class logs its path once at construction.

### `ManagedSphereUnlit.shader`

**What it does:** Unlit transparent URP shader with an instanced `_Color`. The
instanced draw reads the color from the property block, and the combined mesh draw
reads the vertex color, the same split as `InstancedProjectile_URP.shader`.

**Decisions:**
- Blend is `SrcAlpha OneMinusSrcAlpha`, ZWrite is off and culling is off, so the
  shell and the core of a hazard blend. Draw order inside a batch is the order the
  spheres were added.
- It targets the Universal pipeline. In another pipeline the shader draws nothing,
  and the batch uses `Sprites/Default` only if this shader is reported unsupported.

### `ManagedCameraRig.cs`

**What it does:** Owns the cursor and the view mode. Moves the follow target to the
eye point with yaw and pitch, switches the two camera objects, draws the crosshair
and draws the debug overlay with the module toggles and the selected spell.

**Decisions:**
- With no camera objects assigned, the rig drives the main camera itself, and in
  third person it pulls the camera in front of walls with a linecast. This keeps the
  first test free of Cinemachine wiring.
- Execution order is 100 and the work is in `LateUpdate`, after the character.
- The overlay drops the ECS world, physics and command buffer lines, because there
  is no world.
- The crosshair is a small white cross at the screen center. It marks the point that
  the caster aims at, so it must stay at the center.

### `ManagedChemistryTypes.cs`

**What it does:** `ManagedHazardType` (Explosion, Gas, Fire, Freeze),
`ManagedChemicalHazard` and `ManagedReactionResult`, which now also carries the count
of broken bonds.

**Decisions:**
- `CurrentRadius` stays the authoritative footprint for later damage checks. The
  views only mirror it.

### `ManagedChemistryRecipe.cs`

**What it does:** Same fireball values as the ECS `ChemistryRecipe`, with `Mathf`
instead of `Unity.Mathematics`, plus the Ice recipe. How a recipe turns into a
reaction and a hazard is described in `chemistry-simulation.md`.

**Decisions:**
- `MaxBrokenBonds` and `RunFullBudget` are new. The fireball leaves both at their
  neutral values, so its behavior is unchanged.
- The Ice recipe uses three water groups at 150 K, runs the whole step budget and
  asks that no bond breaks. Its hazard is a Freeze zone of 3.5 m that grows for half
  a second and lasts six seconds.

### `ManagedChemistryReactor.cs`

**What it does:** Runs the short Alembic simulation and counts formed and broken
bonds. The calls are the same as in the ECS `ChemistryReactionSystem`.

**Decisions:**
- The native library is probed once. A missing library logs one warning, and every
  later impact returns an unconfirmed result without logging again.
- The reaction code is duplicated from the ECS system on purpose. The ECS file is
  frozen. The two copies merge into one shared class when the stacks are reunited.

### `ManagedChemicalHazardField.cs`

**What it does:** Subscribes to `ManagedSpellCaster.Impact`, runs the reaction,
creates a hazard, grows it, expires it and draws it through one `ManagedSphereBatch`.

**Decisions:**
- `Count` and `Get(index)` expose the live hazards for damage checks.
- Subscription happens in `OnEnable` and `OnDisable`, so a disabled field takes no
  impacts.
- Each hazard draws two spheres, a shell at the authoritative radius and a brighter
  core inside it. The fire core fades faster than the shell, which reads as a flash.
  Ice is pale blue, and grey blue when Alembic did not confirm it. Fire is orange,
  and yellow when unconfirmed.
- The field holds at most `_maxHazards` hazards. A new one drops the oldest.
- No GameObject is created. Hazards are plain objects, drawn from `LateUpdate`.

### `IManagedDamageable.cs`

**What it does:** One-method contract for anything that can take damage: `IsAlive` and `TakeDamage(amount, source)`.

**Decisions:**
- Spells, melee and hazards call the interface, so none of them needs to know whether the target is an NPC or the player. Nothing calls it yet, because the fireball and the hazards do not deal damage in this repo today.

### `ManagedHealth.cs`

**What it does:** Hit points as a component of its own, on the player, an NPC or anything else that can be hurt. `Fraction` (0 to 1) is the value the NPC observation carries.

**Decisions:**
- Health is its own component, as asked, and it implements `IManagedDamageable`. Systems read it and subscribe to `Damaged` and `Died`, they do not write the value.
- Damage that is zero, negative or NaN is ignored, and so is damage while invulnerable or dead. The hit that kills removes only the health that was left, and `Died` fires once.
- A dead object does not heal. `ResetToFull` is the way back, for a respawn.
- The events are plain C# events, not static, because several NPCs have one each.

### `ManagedNpcThreatSource.cs`

**What it does:** Marks an object that NPCs can see, normally the player. It keeps a static list of active sources and a few local visible points, by default head and chest of a 2 m character.

**Decisions:**
- The NPC checks several points, not one. Seeing any one point is enough, so a half hidden target is still seen, which is the Assassin's Creed behavior of being spotted by an exposed arm.
- Every registered source is a threat to every NPC. Factions are not modelled yet, so a guard and a bandit see the player alike.
- A source with a `ManagedHealth` that is dead is not seen. A source that is disabled is not seen.
- The static list is cleared at `SubsystemRegistration`, as in `ManagedSpellCaster`.

### `ManagedNpcSight.cs`

**What it does:** The two tests behind sight. `InView` checks range and the view cone with arithmetic only. `HasLineOfSight` casts one `Physics.Raycast` from the eye to the point.

**Decisions:**
- The cone test runs first, so an NPC that cannot face the player costs no ray. Out of range and behind both skip the physics call.
- The ray ignores triggers. It succeeds when the first thing it meets belongs to the source's transform hierarchy, or when it meets nothing.
- A ray that starts inside a collider does not hit it, so the NPC's own capsule does not block its sight. The mask should still leave the NPC's layer out where possible.
- A ray stops a small margin past the target point. A target point inside a collider is found by the surface hit well before that.

### `ManagedNpcBrain.cs`

**What it does:** One NPC. It holds role, backend, can-move, learning mode and the sight settings. It builds the 32 byte observation for the Rust decision and stores the action that comes back. It raises `ActionChanged` when the action changes. It does not move, fight or trade.

**Decisions:**
- Movement, combat, animation and trading stay out of this file on purpose. Rust chooses, other components act, as in the ECS design.
- A threat lingers for `_loseSightDelay` seconds after the last ray that saw it, so a guard does not flicker between attack and patrol when the player steps behind a pillar for one frame.
- Each ray costs one from a per-tick budget. An NPC that runs out keeps its previous result, see the director.
- A dead brain is left out of the batch and reports Idle.
- The id is a counter that is unique among live NPCs in one play session. It is not stable between sessions, so it cannot key saved memory.
- `LearningMode` is stored and exposed. Nothing reads it yet.
- A threat source on the NPC itself, or on one of its parents or children, is ignored.

### `ManagedNpcDirector.cs`

**What it does:** Drives every brain. Each tick takes a bounded batch, runs sight for the living NPCs, makes one native call for the whole batch and hands the actions back. A brain creates the director when the scene has none.

**Decisions:**
- One native call per tick, never one per NPC. The tick is 0.1 s and the batch is capped at 256, the same numbers as the ECS decision system. A larger population is served in turns through a rotating cursor.
- Sight rays have their own cap per tick, 128 by default, so a crowd cannot spend an unbounded number of raycasts in one frame. When the cap is hit, the NPC that ran out leads the next tick, so every NPC is served in turn and none starves.
- The tick works in three phases. Phase 1 copies the batch out of the list. Phase 2 runs sight and builds observations. Phase 3 applies actions. `ActionChanged` listeners run only in phase 3, so a listener that disables or destroys an NPC cannot change the list under the loop.
- A decision is applied only if its NPC id matches the row it was made for and the action is in range, the same guard as the ECS system.
- A scene director is reused even when it is disabled, because a disabled director is the scene's choice. A second enabled director disables itself with one warning.
- With no `ManagedNpcThreatSource` in the scene the director warns once, because that is the usual reason for NPCs that never see anything.
- `Brains` and the id counter are static and cleared at `SubsystemRegistration`.
- Tick cost is about one managed array fill, one pinned native call and at most the ray cap, all on the main thread.

## CI and Workflows

- `.github/workflows/run-patch.yml` - runs `.mdix/patches/patch.mdix`. The current
  patch moves the system folders under `ECS/`, wraps the ECS files and adds the
  location notes to the docs. It is a one-shot pass. A second run skips the moves,
  reports an error for every file that is already wrapped, and repeats the doc notes.
- `.github/workflows/apply-replacements.yml` - applies the replacements archive that
  added the `Managed/` folder and this file.
- `.github/workflows/npc-rust-ci.yml` - checks the shared NPC layer. The `csharp-abi` job
  compiles `NPCNativeLib`, `NPCNativeTypes`, `NPCManagedFallback` and `NPCEnums` with
  plain .NET and runs them against the real Rust library. The `managed-npc` job compiles
  `Managed/Health` and `Managed/NPC` (C# 9, as in Unity 2022.3) against hand written
  `UnityEngine` stand-ins in `rust/npc/managed-npc-test/` and runs 52 checks, once on the
  managed fallback and once on the real library.

## Fixes and Problems

### Stack split

- The ECS stack does not run on the development machine, a MacBook Pro from 2010
  on patched macOS Catalina. Burst compiled Entities code aborts with "Illegal
  instruction executed" while the default world is created. The abort leaves Unity
  Physics, the command buffer systems and the scene system half built, and the
  later errors (no physics world, no command buffer, entity from a different world,
  entity does not exist) follow from that. An empty SubScene and a scene with only
  the character reproduce it. The cause is not confirmed, and the working theory is
  Burst code generation for this CPU. The decision is to keep the ECS sources
  frozen behind `GTG_ECS` and build gameplay on the Managed stack.
- The Managed NPC was added afterwards, see the NPC modules above. It does not move,
  fight or trade yet, it only decides.

### `ManagedCharacter.cs`

- `CharacterController.isGrounded` is not used, because it reflects only the last
  move. The `CollisionFlags` of the current move give the ground state.

### `ManagedSpellCaster.cs`

- Reported: the fireball did not collide, there was no explosion, and shots only left
  at one angle. The first version raycast along a path that started at a fixed
  offset in the character's yaw space and ended only in an impact event. A shot that
  flew into open space was removed without a trace, which looked like a missing
  collision. The aim also depended on the character's angles alone, so a camera that
  did not follow them, or a missing mouse, left one direction.
- Now: shots leave the shot point and fly to the crosshair target, a sphere is swept
  instead of a ray, a shot that reaches its range detonates, and every impact is
  logged with the collider name. The keyboard keys J, L, I and K turn and look.
- Not verified in the Editor: the sweep and the aim were tested against a simulated
  physics world, not against Unity Physics.
- The shots were pooled GameObjects with a light each. They are now drawn through one
  `ManagedSphereBatch`, and the light is gone.

### `ManagedChemicalHazardField.cs`

- Hazards were one GameObject each with their own material. They are drawn through
  one `ManagedSphereBatch` now.

### `ManagedChemistryRecipe.cs`

- The Ice recipe is untested against the real Alembic library. Whether three water
  groups at 150 K keep every bond for 32 steps is unknown, and the first Ice impact
  line in the Console shows it. The hazard spawns either way, because
  `RequireConfirmation` is off.

### NPC files (`ManagedHealth.cs`, `ManagedNpcBrain.cs`, `ManagedNpcDirector.cs`, `ManagedNpcSight.cs`, `ManagedNpcThreatSource.cs`)

- ECS edit: `ECS/NPC/Native/NPCManagedFallback.cs` lost its `#if GTG_ECS` wrapper. The file has no ECS or
  Unity dependency, and the Managed director needs it when the native library is missing. The ECS decision
  system still uses it unchanged. The alternative was a second copy of the decision, which the Rust parity
  test would not cover. `NPCNativeLib.cs`, `NPCNativeTypes.cs` and `NPCEnums.cs` were already unwrapped.
- Not verified in the Editor, not compiled by Unity. The files compile with .NET 8 (C# 9) against hand
  written stubs of the `UnityEngine` types, and 52 checks pass there on both the managed fallback and the
  real Rust library. CI job `managed-npc` repeats this on every push. The stub raycast is a callback, so the checks prove the logic around the ray (cone, budget,
  lingering, dead and self sources, rotation) and not real colliders, layers or `Physics.Raycast` behavior.
- Sight is a cone plus one ray per visible point. There is no suspicion meter, no hearing and no faction
  logic. An NPC either sees a threat or does not.
- The sight ray starts at the eye offset, 1.6 m above the NPC pivot by default. An NPC whose own collider
  contains that point, or whose layer is in the sight mask, can block itself on some setups. The scene setup
  names the two ways around it.
- Nothing calls `TakeDamage` yet. Fireball impacts and chemical hazards do not hurt anything in the repo
  today, so an NPC's health cannot change in play until a damage source is connected.
- Nothing acts on the decision. Subscribe to `ManagedNpcBrain.ActionChanged` or read `Action` to drive
  movement, combat and trade.
- The native library is still not in `Assets/`. Until a build is imported as a plugin, every batch takes the
  managed fallback after one warning. Rust itself is not affected by the Burst `Illegal instruction` abort,
  because that comes from Burst code generation. Whether the Rust library runs on the 2010 MacBook is
  untested: the CI macOS leg is arm64, and no Intel macOS build exists yet.
