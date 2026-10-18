# Managed stack (`MidManStudio.Gtg.Managed`)

## Overview

GTG keeps two implementations of the same gameplay systems side by side.

- `Assets/MidManStudio/Gtg/ECS/` holds the original Entities, Unity Physics and
  Burst version. Every ECS-dependent file is wrapped in `#if GTG_ECS`, so the folder
  compiles to nothing unless that define is set. The sources are frozen, only the
  wrapper and the new location changed.
- `Assets/MidManStudio/Gtg/Managed/` holds a MonoBehaviour version with no ECS,
  Burst or Unity Physics dependency in its own code. It needs only `UnityEngine`,
  the Input System package and, for chemistry, `com.midmanstudio.alembic`. The studio
  packages come in as pinned git URLs, see `GTG_REPO_CONVENTIONS.md` section 6.

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
3. To make the NPC move and fight, add `ManagedNpcActor`. It brings a `NavMeshAgent` with it. Bake a NavMesh for the scene (a `NavMeshSurface` from the AI Navigation package, installed at 1.1.5) and place the NPC on it. Give the NPC a collider, otherwise the player's fireball passes through it. For a patrol route, drag transforms into the Waypoints list, or leave it empty to wander near the start. A merchant needs no actor.
4. Fireballs hurt anything with a `ManagedHealth` inside the explosion, with no scene setup. The values are in `ManagedSpellDefinition`.
5. The first brain creates a `GTG NPC Director` object. Add a `ManagedNpcDirector` to the scene yourself to change the tick rate or batch limits, or to turn the overlay on.
6. Select an NPC in the Scene view to see its view cone and eye. The cone is yellow while idle and red while it sees a threat.
7. Check the Console at Play start. A missing native library logs one warning and the managed decision takes over. The director overlay says `native` or `managed fallback`. An NPC that is not on the NavMesh logs one warning and stands still.
8. For a companion, choose the role Companion, set its Level, its Trustworthiness, Affinity and PaySatisfaction, and set its Leader to the player object. Give it orders from code with `brain.SetOrder(NpcOrder.Raid, 12)` and listen to `brain.OrderRefused` and `brain.Betrayed`. For a mission, resolve it with `brain.TryResolveMission` when it happens off screen. No UI for orders exists yet. A companion with no order follows its leader.
9. For a non-hostile NPC, set its Disposition. Retaliatory ignores the player until it has been hit, and Peaceful runs and never fights.
10. To change how a group fights, use the two Engagement fields on the `ManagedNpcThreatSource` of the target (the player). Max Melee Attackers (3) is how many NPCs may strike at once, and zero lets everyone strike. Attacker Rotation Seconds (5) is how long a striker keeps its turn before a waiting NPC gets it, and zero lets a striker keep it. The Engagement fields on each `ManagedNpcActor` set the waiting ring, the spacing and the circling speed. Select an attacker with the NPC debugger's Ranges switch on to see its place: a red ring means it holds a turn, an amber ring means it is waiting.
11. For an archer, a crossbow guard or a mage, set the brain's Attack Style to Ranged, and add a `ManagedNpcShooter` to the same object (the brain warns once if it is missing). Pick the kind (Projectile for arrows and bolts, Spell for a mage and then the spell), the range, the stand-off distance, the cooldown and the ammo. A quiver does not refill (regeneration 0), a mage sets seconds per cast so its mana returns. The NPC needs `ManagedNpcActor` to move and fire. For a hybrid set the style to Hybrid, which shoots from afar, fights by hand close up and when it has nothing left to shoot. The target's `ManagedNpcThreatSource` has two more Engagement fields: Max Ranged Attackers (4) and Ranged Rotation Seconds (5), a pool of their own so archers never keep swordsmen from the target. Arrows and spells hurt anything with a `ManagedHealth` (or any `IManagedDamageable`) they hit, other NPCs included. Nothing needs adding to the scene for the shots themselves: the first shot creates a hidden `GTG NPC Shots` object.
12. For a shopkeeper that fights back when attacked, give the merchant `ManagedNpcActor` and the Retaliatory disposition. A hostile or peaceful shopkeeper does not fight. A shopkeeper with Can Move off stays behind its stall and strikes (or fires, with a shooter) at what is in reach. A crossbow shopkeeper takes the Ranged style and a `ManagedNpcShooter`. The shop UI does not refuse service while its keeper is provoked, because that rule is not built.

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
left mouse button, F or the right trigger. Pick a spell with 1, 2 or 3, Tab, or
the d-pad. V switches the view, Escape frees the cursor.

Shots leave the `GTG Shot Point` child of the character and fly to whatever the
screen center looks at. A white crosshair marks that point. Move the shot point in
the Scene view, or assign a staff or hand bone to the caster.

Spell 3 is the Seeker. It locks onto a living target near the crosshair, or onto the
crosshair point when there is none, and turns toward the lock in flight. Every turn
costs SP, so a shot that turns hard flies a shorter way.

Each key picks a slot of the vessel belt, and the overlay shows what is in it. A bottle
is used up by one cast. An orb is emptied by one cast and charges back slowly with game
time. The caster component has a context menu entry, Refill vessels, that restores
every orb and every bottle count, for testing.

SP is also money. The player carries a purse of gold, silver and SP crystals, and the
overlay shows it under the spell line. R, or the west button of a gamepad, uses the
smallest crystal that fills the selected orb, or the largest one held when none does. The
orb fills at once and the crystal is used up. The caster adds a `ManagedPlayerWallet` to
its own object, where the starting purse is set. Nothing is saved.

In the Editor and in development builds a cheat manager starts by itself, so the scene
needs no setup for it. The backquote key opens a panel with every cheat, and a click on
a button uses it. A code typed at any time, with or without the panel, uses it too. The
codes are listed under `ManagedBuiltInCheats.cs`. A toggle stays on until it is switched
off, a full scene load or the end of play. A release build leaves the cheats out.

The shop engine has no window yet. In the Editor and in development builds a shop test
panel starts by itself, and F9 opens it. It lists two sample shops and three sample items,
buys and sells with the player's purse, and flips the flag that opens the hidden sample
shop. It needs the player's wallet, which the spell caster adds when play starts. The
bought items go in a bag the panel owns. A release build leaves the panel out.

The HUD is one component. Add `ManagedHud` to any GameObject, an empty one is fine, and press
Play. It builds its own canvas, event system and text, and needs no prefab. It finds the
player, adds a `ManagedPlayerItemBag` to the player when it has none, and draws the missions
panel at the top left, the compass at the top middle, the crowns and the coins at the top
right, the health bar, the SP bar and five quick slots at the bottom middle, and the wheel
button at the bottom right. Q or a click on the wheel button opens the four section wheel.
Pointing at a section and clicking it makes that section active. A right click, or a click
outside the wheel, closes it. The keys 4 to 8 use the quick slots. A slot is moved by
dragging it to another slot, and emptied by dragging it off the slots or by a right click.
Filling a slot by dragging needs a bag window, which does not exist yet, so a slot is filled
from code for now. In the Editor and in development builds `]` raises the
heat by one section, `[` lowers it and `\` fills the bag, two quick slots and the wheel with
sample entries.

Text uses TextMeshPro when its essentials are imported. Without them the HUD falls back to
the built-in UI text and logs one warning. Import them once with Window, TextMeshPro, Import
TMP Essential Resources. Every piece has a plain coloured placeholder, so the HUD works
before any art exists. A transparent PNG with the name listed under `ManagedHudSprites.cs`,
put in a `GtgHud` folder inside any `Resources` folder, replaces its placeholder with no
other change. The camera rig's debug overlay also draws at the top left, over the missions
panel. Untick `Show Debug Overlay` on the rig to see the panel clear.

Draw path: shots and hazards are drawn with `Graphics.DrawMeshInstanced` when the
GPU supports instancing and `MidManStudio/Gtg/SphereUnlit` compiled, otherwise as
one combined mesh. The Console prints the chosen path at Play start, one line for
shots and one for hazards. The `Force Combined Mesh` option on the caster and the
hazard field tests the fallback on any machine.

For a player build, add `MidManStudio/Gtg/SphereUnlit` under Project Settings,
Graphics, Always Included Shaders. The Editor finds it without that.

Unity creates `.meta` files for the new scripts on first import. Commit them.

To give a town a Crown Heat meter, add `ManagedCrownRegion` to an empty object at the
town's center and size its box to cover the town. Guards inside the box enforce the law
there. The rules, the numbers and the debugger line are in `crown-heat.md`.

To show Crown Heat on the HUD's crowns, add `ManagedCrownHudHeat` to any object, one is enough.
To fill the missions panel, add `ManagedMissionBoard` to any object and list missions in the
Inspector, or add them from code through its `Tracker`. A scene with neither still works, and the
HUD's `SetHeat` and `SetMissions` and the dev keys drive the panels as before.

To place stations, add `ManagedBuildMode` to any GameObject and press B in Play mode.
The setup, controls and layer advice are in `building.md`. While the tray is open the HUD hides
the health and SP bars and the quick slots, and shows them again when it closes.

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
| Explosion at range end | backlog | done, now where the SP runs out |
| SP drive, payload load, collapse at zero SP | design notes in `magic.md` | done, `ManagedSpellFlight` |
| Steering: homing and curved flight, third spell | design notes in `magic.md` | done, `ManagedSpellSeeker` and `Steer` |
| Vessels: single-use bottles, rechargeable orbs | design notes in `magic.md` | done, `ManagedVesselBelt` |
| Currency: gold, silver and SP crystals | design notes in `magic.md` | done, `ManagedWallet` and `ManagedVesselCharging` |
| Cheat code manager | backlog | done, Editor and development builds only, `ManagedCheatManager` |
| Shops, item bag, flags | backlog | engine and test panel done, no shop window yet, `ManagedShop` |
| HUD: missions, crown heat, coins, bars, compass, quick slots, item wheel | backlog | frame done with placeholders, `ManagedHud`; the heat follows Crown Heat through `ManagedCrownHudHeat`, the missions follow `ManagedMissionBoard`, nothing yet fills the wheel |
| Mission list for the HUD panel | backlog | done, a plain tracker, `ManagedMissionTracker`; a quest package attaches behind `IManagedMissionSource` later |
| Station placement, tray UI, instanced pieces | backlog | done, see `building.md` |
| Chemistry | impact entity, hazard entity, presentation system | static event, hazard list, one draw call |
| Ice recipe and Freeze hazard | backlog | done |
| Instanced and combined mesh drawing | backlog, ECS draws through its own presentation | done |
| Health | backlog | done, `ManagedHealth` |
| NPC | decision system only, nothing feeds it and nothing reads it | brain, sight, director and actor done: patrol, chase and melee, retreat |
| Fireball damage | backlog | done, `ManagedSpellDamage` |

The ECS backlog items need the ECS packages installed, so they are ported together.
The arrow keys are the one ECS edit since the split, two lines per axis in
`CharacterInputSystem.cs`.

Networking differs. Netcode for Entities needs the ECS stack. The Managed stack has
no prediction or rollback, and networked play on it would need Netcode for
GameObjects, see `GTG_REPO_CONVENTIONS.md` section 5.

### Folder dependencies (Managed)

Each system has its own folder under `Managed/`, and a folder reads from the folders in
the right column and from no other. The table is taken from the `using` lines. The
project has no assembly definitions, so nothing stops a new line from breaking it. Keep
the arrows pointing one way, and put a new link in this table when it is added.

| Folder | Reads from |
|---|---|
| `Economy`, `Flags`, `Health`, `Rendering`, `CharacterController`, `Missions` | nothing |
| `Items` | `Data`, in two files: the catalog adapter and `ManagedPlayerItemBag` |
| `NPC` | `Health`; `Crown` in the brain, which reads the tier of the region it stands in; `Magic` and `Rendering` in `ManagedNpcShooter.cs`, `ManagedNpcShots.cs` and `ManagedNpcShotDriver.cs` only (the spell table, the flight and the sphere batch) |
| `Crown` | `Health`, `NPC`; `Hud` in `ManagedCrownHudHeat.cs` only, which implements the interface of `ManagedHudHeatSource.cs` |
| `Magic` | `CharacterController`, `Economy`, `Health`, `Rendering` |
| `Chemistry` | `Magic`, `Rendering` |
| `Camera` | `CharacterController`, `Economy`, `Magic` |
| `Shop` | `Economy`, `Items`, `Flags`, and `Camera` in the test panel only |
| `Cheats` | `Camera`, `CharacterController`, `Chemistry`, `Economy`, `Health`, `Magic`, `NPC` |
| `Building` | `Camera` (cursor block), `CharacterController` |
| `Hud` | `Camera` (cursor block), `Items` (quick slots and bag), `Missions` (the empty panel text in `ManagedHud.cs`); `Building`, `CharacterController`, `Economy`, `Health`, `Magic` and `Missions` in `ManagedHudFeed.cs` only. Never `Crown` |

The shop reaches all four of its neighbours through small interfaces:
`IManagedItemCatalog`, `IManagedItemHolder`, `IManagedFlagSource` and
`IManagedCoinPurse`. Replacing what stands behind one of them changes one file and
leaves the shop alone. The patterns package of `MidManStudio_Unity` (command, state,
event queue, dependency injection) is not used yet. These interfaces and the plain C#
events are the places it would attach to.

The HUD reads the game through one file. `ManagedHudFeed.cs` is the only file of `Hud/`
that touches the character, health, the caster, the wallet, build mode or a mission source,
and it copies what it finds into `ManagedHudState`, which the widgets read. The heat comes
the other way round. `Hud/` holds a one file interface, `IManagedHudHeatSource`, and a
system that knows the heat registers an implementation of it, so `Hud/` never names `Crown/`
and `Crown/` names only that one file of `Hud/`. The math, the state, the layout and the
wheel and quick slot models have no scene dependency. Dragging uses `ManagedItemDrag`, a
static carrier in `Items/`, so a slot and a future bag window never reference each other.

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
- The frame also carries the picked spell slot (keys 1, 2 and 3) and a cycle step (Tab,
  d-pad left and right), which the caster reads.
- `UseCrystalPressed` is true the frame R or the west button of a gamepad was pressed.
  The caster reads it to recharge the selected orb.

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
- `MoveSpeedScale` multiplies the walking speed and is 1 by default. Whatever wants to
  change how fast the character walks sets it, a cheat today and an effect later, and
  nothing else in the character changes. A negative value counts as 0.

### `ManagedSpellTypes.cs`

**What it does:** `ManagedSpellKind` (Fireball, Ice, Seeker) and the
`ManagedSpellImpact` struct.

**Decisions:**
- The impact carries `Source`, the GameObject that cast the spell, so damage can skip
  the caster. Existing consumers ignore the field.
- The impact carries `PayloadId`, the id of the payload the shot carried, so the
  chemistry side can later pick the footprint from the payload and not from the kind.
  A collapse of the bubble raises the same impact as a hit. No consumer reads the id
  yet.
- `Seeker` is the first steered spell, and its name is a placeholder. It has no recipe
  branch of its own: `ManagedChemistryRecipe.ForSpell` returns the fireball recipe for
  any kind it does not list. Damage reads the definition, so the Seeker does its own
  numbers.

### `ManagedSpellDefinition.cs`

**What it does:** One tuning record per spell: cooldown, speed, flight time, flight
shape, vessel values, sweep radius, drawn diameter and color. It also builds
the flight record for a cast. The slot order is the table order, so key 1 picks
the first entry. The values live in code and move to an mdix table later, like the
chemistry recipes.

**Decisions:**
- The sweep radius is a size of its own and is not the drawn diameter. A sphere of
  0.15 m catches a thin collider that a plain ray misses, and the shot still looks
  small.
- `ImpactDamage`, `ImpactRadius` and `ImpactEdgeFraction` describe the explosion. The
  fireball does 40 damage at the center, 3 m radius, and 25 percent of that at the
  edge. Ice has no damage. These are placeholder tuning numbers, chosen so three direct
  hits kill a 100 health NPC, and they move to the mdix table with the rest.
- `For(kind)` looks a definition up by kind, because the slot order is not a stable key.
- `LifetimeSeconds` is how long a clean mix flies at full speed. The drive cost per
  meter is derived from it (`DriveSpPerMeter`), so a clean mix flies `Speed` times
  `LifetimeSeconds` meters: 75 m for the fireball and 63 m for ice, the same as the
  old fixed lifetime. The hard cap on flight time is twice that (`MaxFlightSeconds`).
- `VesselSp` and `VesselRating` are what a vessel for this spell is made with. The
  caster copies them into the belt, and a cast reads them back from the vessel it drew,
  so a crafted vessel can carry its own and these two leave the table later.
  `HoldSpPerSecond` and `OverloadSpPerLoad` are tuning for the bubble and stay.
- Only the part of a payload's load above `VesselRating` costs extra
  (`HoldSpPerSecondFor`). A mix at or under the rating flies the normal range.
- `Powered`, `GravityScale` and `DragPerSecond` shape the path. A thrown spell is a
  row with `Powered` false and a gravity scale. No spell uses it yet.
- `CreateSpawn` is the one place that turns these numbers and a vessel into flight
  values, and `ManagedSpellFlight` does not change. It takes a `ManagedVesselDraw`. The
  shot starts with the SP drawn. The drive cost per meter comes from the vessel's
  capacity (`DriveSpPerMeterFor`), not from the SP drawn, so a half charged orb burns
  at the normal rate and flies half as far. The overload cost uses the vessel's rating
  (`HoldSpPerSecondFor(load, rating)`).
- `CreateSpawn` also takes a `ManagedSpellModifiers`, which is where a cheat or a later
  effect changes a spell. The range scale multiplies the SP the shot starts with and the
  flight cap, so a scale of 2 flies twice as far and a scale under 1 flies less. The
  steer cost scale multiplies the SP per radian. A negative scale counts as 0. The call
  without modifiers still works and gives the same shot as neutral ones.
- `IndexOf(kind)` gives the table index of a kind. A shot carries that index as its
  profile, because a belt slot is no longer the same as a table row.
- `TurnDegreesPerSecond`, `SteerSpPerRadian`, `SeekRange` and `SeekConeHalfAngleDegrees`
  describe steering. A turn rate of 0 is a straight shot (`Seeks` is false), so the
  fireball and ice rows did not change and fly as before.
- The `Seeker` row turns 140 degrees per second at 10 SP per radian, looks 40 m ahead
  inside a cone of 20 degrees either side of the aim, flies 20 m/s for 3.5 s (70 m when
  it never turns) and does 30 damage in a 2.5 m radius. A 90 degree turn costs 15.7 SP,
  about 16 percent of a full vessel's 100. The name, the numbers and the color are
  placeholders.

### `ManagedSpellPayload.cs`

**What it does:** `ManagedSpellPayload` holds the load of a mix, how hard it pushes
on the bubble. `ManagedSpellPayloads` is the table, indexed by payload id, with
`IdFor(kind)` and `LoadOf(id)`.

**Decisions:**
- Load is on the same scale as a vessel's rating, and flight costs only the part above
  the rating. The numbers are placeholders: fireball 6, ice 3, seeker 6, against a
  rating of 10.
- For now the id equals the spell kind. The id is what the impact carries, so a cook
  result from the outcome classifier can replace the table without touching flight.
- An id the table does not have has load 0.

### `ManagedSpellFlight.cs`

**What it does:** Shots in flight as parallel arrays of plain values, with no class
reference per shot. `Plan` updates a shot's velocity and returns the segment it wants
to fly. The caller sweeps that segment. `Commit` moves the shot, burns its SP and says
whether it is still flying, collapsed or at its flight cap. `Steer` turns a shot toward
a direction the caller gives it, before `Plan`.

**Decisions:**
- The module knows nothing about spell definitions, payloads or physics. Every number
  arrives in `ManagedShotSpawn`, and gravity arrives per call. Another caster, a trap
  or the ECS port can use it as it is.
- A step burns `hold * dt + drive * distance`. When that is more than the SP left, the
  shot is released at the point of the segment where the SP ran out. Range therefore
  does not depend on the frame rate: a clean fireball releases at 75.000 m at 120, 60,
  30 and 10 frames per second.
- A powered shot keeps its speed, so gravity and drag only bend the path. An unpowered
  shot gains speed from gravity and loses it to drag.
- `RemoveAt` swaps the last shot into the gap. The caster counts down from the end, so
  no shot is visited twice or skipped.
- `CapReached` is a safety net for a shot whose burn is zero. A shot with SP 0 at the
  cast collapses on its first step.
- Collision stays with the caller, because the sweep needs Unity Physics and the caster
  owns the rule about its own colliders.
- `Steer` rotates a shot's velocity toward a desired direction by at most its turn rate
  times the step, and keeps the speed. It returns the radians turned. A shot with turn
  rate 0 does not turn, whatever it is asked. The module does not choose the direction,
  so homing, a waypoint or a scripted curve all use the same call.
- Turning costs SP: radians turned times `SteerBurnPerRadian`. The cost is held back and
  charged in the next `Commit` with the hold and drive cost, so the point where the SP
  runs out inside a step accounts for the turn. A 90 degree turn burns 15.7 SP at 10 per
  radian.
- A turn straight back has no rotation axis, because the two directions are parallel.
  `Steer` picks an axis square to the heading, so the shot still turns.
- A shot carries `Target`, an int the module never reads. The caster puts a lock id in
  it, so a shot stays a record of plain values.

### `ManagedSpellSeeker.cs`

**What it does:** Decides what a steered spell flies toward and remembers it for the life
of the shot. A lock is a living target found in a cone around the aim, or, when the cone
is empty, the point the player aimed at. A shot keeps only the id of its lock, and asks
`TryGetPoint` each step where to turn.

**Decisions:**
- `Lock` scans a sphere of `SeekRange` around the shot point with
  `Physics.OverlapSphereNonAlloc`, so the scan allocates nothing. The buffer holds 32
  colliders, and any beyond that are not considered.
- A candidate must have an `IManagedDamageable` that is alive, sit between 0.75 m and the
  range away, and lie inside the cone. The one nearest the aim line wins, and a tie goes
  to the closer one. The caster's own colliders are skipped.
- A candidate must also be in line of sight, checked once with `Physics.Linecast`. The
  first thing the line meets has to be the target, or the caster's own body, because the
  shot point sits near it. A target behind a wall is not locked.
- With no candidate the lock is the fallback point, which the caster passes as the
  crosshair target. That gives the rule "the target if there is one, else the crosshair
  point".
- A lock follows its target each step while it is alive. When the target dies or is
  destroyed the lock keeps its last point, so the shot flies to where it was.
- A point lock, and the last point of a dead target, is dropped once the shot has flown
  past it. This is judged from the shot's heading: when the point is behind it, the lock
  is marked passed and stays passed. A living target is never dropped this way, so a
  shot that overshoots a living target turns back for it.
- Locks live in a slot table with a free list, and the table doubles when it is full. An
  id is the slot index plus one, so 0 means no lock. `Release` returns the slot, and
  releasing an id twice or an unknown id does nothing.
- The seeker holds no reference to flight or to the caster, only the transform it is
  told to ignore.

### `ManagedVesselTypes.cs`

**What it does:** `ManagedVesselKind` (Bottle, Orb), `ManagedVesselDraw` and
`ManagedLoadoutEntry`.

**Decisions:**
- `ManagedVesselDraw` is what one cast takes out of a vessel: the SP the shot starts with,
  the SP of a full vessel of that kind, and the vessel's rating. The capacity travels with
  the draw so the drive cost can be worked out from a full vessel and not from the SP drawn.
  That is what makes a half charged orb fly half as far.
- `ManagedLoadoutEntry` is the Inspector record for one belt slot: spell, vessel kind,
  bottle count and orb recharge seconds. The SP and the rating of the vessel come from
  the spell's definition until crafted vessels exist.

### `ManagedVesselBelt.cs`

**What it does:** The vessels the player can cast from, one per slot. A bottle slot holds a
count of full bottles. An orb slot holds one orb. `TryDraw` takes what one cast uses,
`Tick` charges the orbs, `MissingSpAt` and `Charge` put SP into one, `Refill` restores
everything, and `Describe` gives a short line for the overlay.

**Decisions:**
- Bottles are single-use. Each cast uses one full bottle, and a slot with none left stays
  in place and cannot cast.
- An orb keeps its spell and its payload, and only its SP changes. A cast takes all the SP
  the orb has, however little, and leaves it at 0. The SP a shot does not burn is not
  given back, so a shot that hits early wastes the rest. An orb at 0.01 SP or less cannot
  cast.
- `Tick(dt)` charges an orb in a straight line, capacity divided by its recharge seconds,
  and stops at full. The step is game time: the caster passes the scaled frame time, so
  charging pauses with the time scale, and waiting or sleeping could pass a larger step.
  The default of 45 seconds from empty is a placeholder.
- The belt holds plain values and knows nothing about spell definitions, flight or
  physics. Another carrier, such as an inventory package, can fill it with `Set` and read
  it with the `...At` getters.
- `Charge(slot, sp)` fills an orb up to its capacity and returns what it took. The rest
  is lost. `MissingSpAt(slot)` is the room an orb has left. A bottle slot, an unused slot,
  a zero, negative or NaN amount takes nothing, and has no room. `ManagedVesselCharging`
  uses both.
- A slot index outside the belt does nothing and never throws, and a belt always has at
  least one slot.

### `ManagedCrystal.cs`

**What it does:** `ManagedCrystalSize` (Small, Medium, Large) and `ManagedCrystals`,
which gives the SP value and the name of a size.

**Decisions:**
- SP is both what powers a vessel and a currency, and a crystal is how it is carried, as a
  soul stone is in Skyrim. The sizes are fixed, so a crystal is a count per size and never
  a loose amount.
- The values are 25, 100 and 250 SP. They are placeholders. A medium crystal equals the
  SP of a full orb of the current spells, so one fills an empty orb.
- An unknown size is worth 0 SP and is named "unknown", so a bad value does nothing.

### `ManagedWallet.cs`

**What it does:** Gold, silver and crystals as plain values, with no Unity types: `AddGold`,
`AddSilver`, `CanAfford`, `TrySpend`, `AddCrystals`, `TryTakeCrystal`, `TryTakeCrystalFor`
and `Describe`.

**Decisions:**
- One silver total sits underneath. 100 silver make a gold, and `Gold` and `Silver` are
  how that total is shown. There is no coin to break or give change for, so paying 30
  silver from 1 gold leaves 70 silver.
- A price is a gold part and a silver part. `TrySpend` fails whole and changes nothing
  when a part is negative, the price overflows or the total is too small.
- `AddGold` and `AddSilver` ignore a zero or negative amount and an add that would
  overflow, and `AddCrystals` does the same for counts and unknown sizes, so a bad call
  cannot leave the wallet in a wrong state.
- `TryTakeCrystalFor(missing, out sp)` picks the crystal for a gap: the smallest one that
  covers it, so little is wasted, or the largest one held when none does. `sp` is what the
  crystal was worth. Nothing changes when no crystal is held or the gap is not above 0,
  NaN included.
- `Describe` gives a line such as `3 gold 50 silver, crystals S2 M1 L0` for the overlay.
- `ManagedWallet` implements `IManagedCoinPurse`, the coin side of it, and the shop reads
  the wallet through that interface. See `IManagedCoinPurse.cs`.

### `ManagedPlayerWallet.cs`

**What it does:** Holds the `ManagedWallet` as a component, so the overlay, the caster and
the cheats can find it, and carries the starting purse for testing: 3 gold, 50 silver, two
small crystals, one medium crystal and no large one. The numbers are Inspector fields.

**Decisions:**
- The caster adds this component to its own object when it has none, so a scene needs no
  wiring.
- The wallet is built on first use, so a read before `Awake` still works.
- Nothing is saved. Every play starts from the starting purse.

### `ManagedSpellModifiers.cs`

**What it does:** Four values the caster applies on top of the spell tables:
`CooldownScale`, `RangeScale`, `SteerCostScale` and `InfiniteVessels`. All are neutral by
default. `IsNeutral` and `Reset` read and clear them.

**Decisions:**
- The caster owns one and passes it to `CreateSpawn`. Anything that changes how spells
  behave, a cheat today and an item or an effect later, writes a field here, and no spell,
  flight or vessel code needs to know who did.
- The range scale multiplies the SP a shot starts with and its flight cap, and leaves the
  burn rate alone, so 2 gives twice the distance.
- The cooldown scale and the steer cost scale count a negative value as 0.
- `InfiniteVessels` is read by the caster, which refills the belt before every draw.

### `ManagedVesselCharging.cs`

**What it does:** `ManagedVesselCharging.Recharge(belt, slot, wallet, out crystalSp, out
taken)` and the result enum `ManagedChargeResult` (Charged, NotAnOrb, Full, NoCrystal).

**Decisions:**
- The soul gem rule: a crystal fills an orb at once and is used up. The part that does not
  fit is lost, the same as SP a shot does not burn.
- The checks run in this order: the slot holds an orb, the orb has room, the wallet has a
  crystal. A full orb keeps the crystal, and nothing changes unless a crystal is used.
- It is a function of its own with no state, so a shop, a pickup or a menu can call it the
  way the caster does. The caster turns the result into a log line.

### `ManagedSpellVfxMaterials.cs`

**What it does:** Builds unlit placeholder materials. It tries the URP Unlit shader,
then `Sprites/Default`, then `Unlit/Color`, and returns null if none exists. Spell
drawing no longer uses it, see `ManagedSphereBatch.cs`. It stays for placeholder
objects.

### `ManagedSpellCaster.cs`

**What it does:** Casts the selected spell. A cast leaves the shot point and flies to
the crosshair target, powered by the SP of a vessel drawn from the belt. Each step
sweeps a sphere from the old position to the new one and raises `Impact` on a hit, or
when the bubble collapses. Flight state lives in `ManagedSpellFlight` and vessel state
in `ManagedVesselBelt`. Shots are drawn through one `ManagedSphereBatch`, with no
GameObject per shot.

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
- A shot that runs out of SP collapses where it is and the payload releases there.
- `Raise(impact)` (public, static) raises `Impact` for a spell this class did not fly itself, the
  spell of an NPC mage. The spell damage and the chemistry field listen to the event, so they treat
  it like any other impact. It does nothing when nobody listens. See `ManagedNpcShots.cs`.
  This makes a miss visible and also shows the chemistry working on open ground. It
  replaces the fixed lifetime and the `_detonateAtRangeEnd` switch.
- A shot dims to 35 percent brightness and shrinks to 60 percent size as its SP
  drains, so a dying spell can be seen.
- Impact times, positions and the collider name are logged (`_logImpacts`). This
  tells apart "the shot hit nothing" from "the shot hit but nothing was drawn".
- The impact raised on a hit carries `Source`, the caster's GameObject, set in
  `Detonate`. Nothing else about the caster changed for damage.
- `Impact` is a static event, so the chemistry field needs no reference to the
  caster. It is cleared at `SubsystemRegistration`, because static state survives a
  play session when domain reload is off.
- The batch is filled and drawn in `LateUpdate`.
- A spell that `Seeks` locks at the cast. `ManagedSpellSeeker.Lock` is given the shot
  point, the aim direction, the crosshair point as the fallback, the spell's range and
  cone, the hit mask and the caster's transform. The lock id goes into the spawn, and
  the log says what was locked onto.
- Each step, `SteerShot` runs before `Plan`. It asks the seeker for the point, skips
  when the shot is within 0.5 m of it, because a direction from almost no distance is
  unstable, and otherwise calls `Steer`. Sweeping and hits are unchanged, so a curving
  shot still stops on the first collider it meets.
- Every way a shot ends goes through `Retire`, which releases the lock and then removes
  the shot. A lock cannot leak from one ending path and not another.
- The belt is built in `Awake` from the serialized loadout. An empty array uses the
  default: a fireball orb, five ice bottles and a seeker orb. An entry whose spell has no
  definition gets no slot, so a key never points at a hole. Keys 1 to 3 and Tab pick
  belt slots in order.
- A cast draws from the belt first. When the slot cannot give anything the cast logs why
  and the next try waits 0.3 s, so a held fire button does not flood the log. The draw
  comes before the check for a shot point inside a wall, so a cast that explodes in the
  hand still uses its vessel. The log also says how much SP the cast started with.
- Orbs charge in `Update` with the frame's game time.
- `RefillVessels` is a context menu entry on the component, for testing, and the refill
  cheat calls it. `AdvanceTime(seconds)` charges the orbs as if that much game time had
  passed, and the time skip cheats call it.
- The caster owns a `ManagedSpellModifiers`, given out as `Modifiers`, and passes it to
  `CreateSpawn` on every cast. The cooldown after a cast is the spell's cooldown times
  `CooldownScale`. With `InfiniteVessels` on, the belt is refilled before the draw, so
  every cast finds a full vessel and uses nothing up.
- `Awake` gets the `ManagedPlayerWallet` on the same object or adds one. `Wallet` gives
  the purse to the overlay and the cheats.
- R calls `UseCrystal`, which hands the selected slot and the purse to
  `ManagedVesselCharging.Recharge` and logs the outcome: how much the orb took and how
  much of the crystal was lost, or why nothing happened. Like firing, it only works while
  the Cast module is on.
- `HasSelectedVessel` and `SelectedVesselFraction` read the selected belt slot for the HUD's
  SP bar: whether the slot holds a vessel, and how full it is from 0 to 1 (an orb's charge,
  or the share of bottles left). Both are read-only and change no state.

### `ManagedSpellDamage.cs`

**What it does:** Turns a spell impact into damage. It subscribes to `ManagedSpellCaster.Impact` by itself, so no scene object is needed. It hurts every `IManagedDamageable` inside the explosion radius, once each, with less damage toward the edge.

**Decisions:**
- The subscription happens at `BeforeSceneLoad`, which runs after the `SubsystemRegistration` reset of the caster's static event, so it is not wiped.
- A body with several colliders is hurt once, using its nearest collider. Distance is measured to the collider bounds, not to the pivot, so a direct hit on a large body does full damage.
- The caster is not hurt by its own spell unless `HurtCaster` is set. `Enabled` turns all spell damage off.
- The overlap buffer holds 32 colliders. A crowd larger than that is not fully hurt by one explosion, which is a known limit.
- The explosion does not check line of sight, so it also hurts targets behind a wall.
- Ice does nothing here, because its definition has no damage. The chemical hazards do not call `TakeDamage` either.

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
  the caster aims at, so it must stay at the center. It is hidden while any panel holds a
  cursor block, because a free cursor does not aim at the center.
- The spell hint in the overlay lists keys 1, 2 and 3 and the state of the selected
  vessel, such as `orb 62 percent` or `bottles 4 of 5`.
- A purse line under the spell line shows gold, silver and crystals by size, and names
  the crystal key.
- `CursorCaptureBlocked` is a read-only static property that returns
  `ManagedCursorBlock.Active`. While any panel holds a block, a click does not capture
  the cursor, so a click on a panel button does not also lock the mouse. The cheat
  panel, the shop panel, the HUD and build mode hold blocks. The rig knows nothing about them. The
  count is cleared at `SubsystemRegistration`, because static state survives a play
  session when domain reload is off.

### `ManagedCursorBlock.cs`

**What it does:** A counted claim on the mouse cursor. A panel that needs the cursor free
for clicking owns one instance and calls `Set(true)` while it is open and `Set(false)` when
it closes. `ManagedCursorBlock.Active` is true while any instance holds a claim, and the
camera rig does not capture the cursor then.

**Decisions:**
- It replaces the settable flag `ManagedCameraRig.CursorCaptureBlocked`. Two panels shared
  that flag, so closing one while the other was open gave the cursor back to the camera.
  Each holder now has its own claim and the cursor stays free until the last one lets go.
- `Set(true)` on a holder that already holds, and `Set(false)` on one that does not, change
  nothing. A panel opened twice or closed twice counts once.
- The count has a generation. `Reset()` zeroes the count and moves to the next generation,
  and a holder set before a reset reads as not holding afterwards. When it releases later it
  does not take a count off a claim made after the reset.
- The reset runs at `SubsystemRegistration`, because static state survives a play session
  when domain reload is off.
- The holders today are the cheat panel, the shop panel, the HUD wheel, the HUD while the
  pointer is over a clickable piece, and build mode while the tray is open. The rig reads
  `Active` and knows no holder.

### `IManagedCheatHost.cs`

**What it does:** Everything a cheat is allowed to touch: the spell modifiers, the wallet,
refilling vessels, skipping time, the time scale, invulnerability, healing, the walk speed
scale, killing NPCs, clearing hazards, and logging.

**Decisions:**
- The built-in cheats are written against this interface and never against the scene, so
  they are tested without one and another host can stand in.
- A method that cannot do its job returns false, null or 0 and logs why. The registry then
  leaves a toggle off.
- The whole `Cheats/` folder is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so a release
  build compiles it to nothing. The `.meta` files stay, so GUIDs do not change between
  build kinds.

### `ManagedCheat.cs`

**What it does:** One cheat: code, name, group, description, an apply step and an optional
revert step. `ManagedCheat.Toggle` makes one that stays on, `ManagedCheat.OneShot` one that
runs each time it is used.

**Decisions:**
- A cheat with a revert step is a toggle, so every toggle can be switched back. A one-shot
  has none.
- `On` is set only by the registry and lives in memory. Nothing is saved, so every play
  starts with every cheat off.

### `ManagedCheatRegistry.cs`

**What it does:** The list of cheats, the buffer of typed characters, and the on and off
bookkeeping: `Add`, `Find`, `Feed`, `Activate`, `RevertAll` and `ClearBuffer`. It has no
Unity types, so the panel, the typed codes and the tests all drive it the same way.

**Decisions:**
- A code is 4 to 24 characters of a to z and 0 to 9. `Add` refuses a code that appears
  inside another, in either direction, because typing the longer one would trigger the
  shorter one on the way. It also refuses a repeated code and a null cheat.
- `Feed` lowercases a character, ignores anything that is not a letter or a digit, keeps
  the last 24 characters and forgets a half typed code after 2 seconds of silence. A hit
  clears the buffer, so a code cannot borrow letters from the one before it. The caller
  passes the time, so tests need no clock and the manager can pass unscaled time, which
  keeps slow motion from stretching the gap.
- `Activate` switches a toggle that is on off, and otherwise runs the apply step. A toggle
  turns on only when the apply step returned true.
- `RevertAll` switches every toggle that is on back off. The manager runs it on a full
  scene load and when it goes away.

### `ManagedBuiltInCheats.cs`

**What it does:** The cheats that ship, 16 codes in 5 groups. `CreateRegistry` builds a
registry that holds them.

| Code | Group | Kind | Effect |
|---|---|---|---|
| `loadup` | Vessels and spells | run | Fills every orb and restores every bottle. |
| `endlessmagic` | Vessels and spells | toggle | A cast never uses a vessel up. |
| `quickcast` | Vessels and spells | toggle | No cooldown between casts. |
| `longshot` | Vessels and spells | toggle | Shots fly twice as far on the same vessel. |
| `freesteer` | Vessels and spells | toggle | Steering costs no SP. |
| `godmode` | Player | toggle | The player takes no damage. |
| `healme` | Player | run | Full health. A dead player is not revived. |
| `fastfeet` | Player | toggle | The player walks twice as fast. |
| `skipshort` | Time | run | Orbs charge as if 15 seconds of game time passed. |
| `skiplong` | Time | run | Orbs charge as if 60 seconds of game time passed. |
| `slowmotion` | Time | toggle | Game time runs at a quarter speed. |
| `wipenpcs` | World | run | Kills every living NPC. |
| `cleanup` | World | run | Removes every chemical hazard. |
| `givegold` | Money | run | Adds 10 gold. |
| `givesilver` | Money | run | Adds 50 silver. |
| `givecrystals` | Money | run | Adds 3 crystals of each size. |

**Decisions:**
- Every toggle changes one thing, and its off step puts that thing back to normal, so
  `RevertAll` can undo all of them. Each modifier field belongs to one cheat, so two toggles
  never fight over a value. For the same reason slow motion is the only cheat on game time,
  and there is no fast forward toggle.
- The time skips and the toggles that need the caster fail, and stay off, when the scene
  has none. `wipenpcs` and `cleanup` count as failed when there was nothing to do, so the
  message says so.
- `CreateRegistry` throws when a code breaks the registry rules, so a typo shows up in the
  first test run.
- The numbers sit in constants at the top of the class. They are placeholders.
- The time skips call the same `AdvanceTime` that waiting or sleeping would, so they test
  the open question in `magic.md` about whether waiting recharges orbs.

### `ManagedCheatSceneHost.cs`

**What it does:** The cheat host for a running scene. It looks the caster, the character,
the NPCs and the hazard fields up when a cheat needs them.

**Decisions:**
- It keeps a reference to the caster and to the character and nothing else besides the last
  message. A reference to a destroyed object compares equal to null, so the host looks the
  object up again, and a respawn does not leave it pointing at the old player.
- `KillAllNpcs` deals each living NPC its current health and counts the ones that died. An
  NPC that is invulnerable by design survives and is not counted.
- `HealPlayer` does not revive. A dead player stays dead until a respawn.
- `ClearHazards` clears every field in the scene and returns the total.
- Messages go to the Console with a `[GTG Cheat]` prefix and are kept with their time, so
  the panel can show the last one.

### `ManagedCheatManager.cs`

**What it does:** Feeds typed characters to the registry through `Keyboard.onTextInput`,
opens and closes the panel with the backquote key, and draws a line of the cheats that are
on while the panel is closed. It creates itself when play starts and survives scene loads.

**Decisions:**
- A manager that the scene already has, even a disabled one, stops the automatic one. A
  second manager disables itself, as the NPC director does.
- Opening the panel frees the cursor and holds a `ManagedCursorBlock`. Closing it gives
  the cursor back only when the panel was the one that took it. The dependency runs one
  way: the cheats know the camera folder, and the rig knows nothing about the cheats.
- A full scene load switches every toggle off and clears the typing buffer, because the
  objects the toggles changed are replaced. An additive load does nothing. When the manager
  goes away, at the end of play, it does the same, which also puts `Time.timeScale` back.
- The manager follows `Keyboard.current` when the keyboard object changes.
- The panel is IMGUI, like the overlay, so it needs no prefab or UI package. It sits at
  the right, scrolls, and shows one button per cheat with its state (`[on]`, `[off]` or
  `[run]`), its code and its description. The line for a closed panel shows the cheats that
  are on and the last message for four seconds.
- A code typed in the game also presses the game keys. See Fixes and Problems.

### `IManagedCoinPurse.cs`

**What it does:** Four members: `TotalSilver`, `CanAfford`, `TrySpend(gold, silver)` and
`AddSilver`. `ManagedWallet` implements it with no change to its code.

**Decisions:**
- The shop needs the coin and nothing else, so it holds this and not the wallet. A test can
  hand the shop a purse that misbehaves, and a later currency carrier can fit the same four
  members.
- The members are the wallet's own, with the same meaning, so the interface adds no rule.

### `ManagedItemInfo.cs`

**What it does:** A plain value with an item's `Id`, `Name`, `ValueSilver` and `MaxStack`.
`Stackable` is true when `MaxStack` is above 1.

**Decisions:**
- It carries only what a shop and a bag need. Rarity, weight and type stay in the item
  table, and a catalog can add them to its own side when something needs them.
- The constructor repairs bad numbers: a negative value becomes 0, a stack below 1 becomes
  1 and an empty name becomes `item <id>`.

### `IManagedItemCatalog.cs`

**What it does:** One method, `TryGet(itemId, out ManagedItemInfo)`.

**Decisions:**
- The shop and the bag ask this and never learn where the answer is stored. The item table,
  a list built in a test or an inventory package can stand behind it.

### `ManagedItemCatalog.cs`

**What it does:** A catalog filled from code with `Add`. An item with the same id is
replaced.

**Decisions:**
- Tests and the shop test panel use it. It has no Unity types.

### `ManagedItemDatabaseCatalog.cs`

**What it does:** Reads a baked `ItemDatabase` once and answers as a catalog.

**Decisions:**
- It is the only file of `Items/` that knows `Gtg/Data`. If the table changes shape, this
  file changes and nothing else does.
- The table has no unit for `Value`. It is read as silver, and that is a flagged default.
- The table says per item whether it stacks and gives one stack size, `MaxStackSize`. A
  stackable item gets that size and any other item gets 1.
- A null row is skipped, and when two rows share an id the first is used. A null table or
  a null list gives an empty catalog.
- Nothing builds one in a scene yet. The shop test panel uses the sample items.

### `IManagedItemHolder.cs`

**What it does:** `CountOf`, `CanAdd`, `TryAdd` and `TryRemove`. Adding and removing are all
or none.

**Decisions:**
- This is what the shop sees of the player's carried items. `ManagedItemBag` is the plain
  one, and an inventory package can be wrapped to fit the same four methods.

### `ManagedItemBag.cs`

**What it does:** A bag of slots. A slot holds one kind of item up to its stack size. An item
that does not stack takes a slot of its own. `SlotCount`, `UsedSlots` and `TryGetSlot` let an
overlay draw it.

**Decisions:**
- Slots, not a bare count, because that is how a real inventory refuses an item, and the
  shop's bag-full result needs a meaning.
- Adding fills the stacks the item already has and then opens empty slots from the front.
  Removing takes from the last slot first, so the earlier stacks stay full.
- An item the catalog does not know is refused. So is a bag built with no catalog.
- A stack already above a smaller stack size, after the catalog shrank, counts as having no
  room, and a free slot still takes a full new stack.
- The slot count is 20 when none is given. It is a placeholder. A count below 1 gives 1.
- Nothing is saved.

### `ManagedQuickSlots.cs`

**What it does:** A short row of slots, five by default, each empty or holding an item id. The
player assigns items to it. It holds ids only, so the count shown on a slot is read from the
bag when the slot is drawn. `Assign`, `Clear`, `Swap`, `TryGetItem`, `SlotOf` and `TryUse`,
with the events `Changed` and `Used(slot, itemId)`.

**Decisions:**
- An item sits in one quick slot. Assigning it to a second slot moves it, so the row never
  shows the same item twice.
- Assigning the item a slot already holds is true and silent. With a catalog, an item the
  catalog does not know is refused, and with no catalog any id is taken.
- A slot keeps its item when the bag runs out of it, and the HUD dims it. Dropping the item
  from the row is the player's act, so running out does not clear it.
- `TryUse(slot, holder)` raises `Used` when the slot has an item and the holder carries at
  least one. It does not consume anything. What using an item does is not decided yet, and the
  listener that decides it removes the item from the bag.
- `Swap` of two empty slots, or of a slot with itself, does nothing and raises nothing.
- A slot count below 1 gives 1.
- It knows no Unity types and nothing of the HUD, so any later window can show the same row.
- Nothing is saved.

### `ManagedItemDrag.cs`

**What it does:** A static carrier for one drag in progress. `Begin(itemId, count, origin,
sourceIndex)` starts it, a target that takes the item calls `Accept()`, and the source calls
`End()` when the drag finishes and learns whether anything took it. `Payload` says what is
being carried and where it came from (`Other`, `Bag` or `QuickSlot`).

**Decisions:**
- It exists so a drag source and a drag target never reference each other. A quick slot, a bag
  window and an NPC's inventory can drag to and from one another through this and nothing
  else.
- `Begin` replaces a stale drag. `Accept` with no drag in progress is ignored, so an accept
  from an earlier drag cannot carry over to the next one.
- A slot dragged onto nothing that takes it is emptied. A drag that was accepted elsewhere
  leaves the slot as the target left it.
- The payload count is at least 1.
- The carrier is cleared at `SubsystemRegistration`, because static state survives a play
  session when domain reload is off.

### `ManagedPlayerItemBag.cs`

**What it does:** The component that gives the player a `ManagedItemBag` and a
`ManagedQuickSlots`, built over one catalog. `Bag`, `QuickSlots` and `Catalog` build on first
use, and `UseCatalog` replaces the catalog and starts both empty.

**Decisions:**
- The catalog comes from an assigned `ItemDatabase`, or from an asset named `ItemDatabase` in a
  Resources folder. With neither, the bag knows no items and the component logs one warning, so
  nothing is added until a catalog exists. A null passed to `UseCatalog` is an empty catalog.
- It is the second file of `Items/` that reads `Gtg/Data`, next to the catalog adapter.
- The HUD adds it to the player when the player has none and the HUD's option is on. The bag
  and the quick slots are what the shop, a bag window and the HUD will share.
- The slot counts are serialized and are placeholders, 20 and 5.
- Nothing is saved.

### `ManagedItemSamples.cs`

**What it does:** Three sample items: an Iron Sword (id 1), a Health Potion (id 2) and a Dragon
Scale (id 3), the rows of `Assets/inventory_items.mdix` as they are today. `Catalog()` builds
them, and `StockPlayerBag` gives a player ten potions, the sword and the scale, with the potion
in quick slot 0 and the sword in quick slot 1.

**Decisions:**
- It moved here from `ManagedShopSamples.cs`, which now takes its items from this file, so the
  shop test panel and the HUD dev keys use the same three items.
- None of it is game content. It is left out of release builds.

### `IManagedFlagSource.cs`

**What it does:** One method, `IsSet(flag)`.

**Decisions:**
- A shop asks this to learn whether it is open and never learns who set the flag.
- `ManagedWorldFlags` is the plain answer today. The Questly package of `unity-chem-sim` has
  `QuestRuntime.GetQuestState(questId)` and a `QuestState.COMPLETED` value, so an adapter
  can later answer `IsSet(id)` with "that quest is complete". Questly is in this
  project's `Packages/manifest.json` since 2026-10-16, and nothing here depends on it yet.

### `ManagedWorldFlags.cs`

**What it does:** A set of named flags with `Set`, `Clear`, `ClearAll`, `IsSet` and `Count`.
`Set` and `Clear` report whether anything changed.

**Decisions:**
- Names are exact: `Sus` and `sus` are two flags. A null or empty name is never set and
  setting it does nothing.
- Nothing is saved. Nothing in a scene owns one yet, so mission code has no way to reach one
  until the shop window and the scene wiring exist.

### `ManagedShopTypes.cs`

**What it does:** `ManagedShopLine` (an item the shop sells: item id, price, stock),
`ManagedShopBuyLine` (an item it buys: item id, price), the result enum
`ManagedShopResult`, `ManagedTradeKind`, the event value `ManagedShopTrade` and the window
row `ManagedShopOffer`.

**Decisions:**
- The line classes are serializable and hold whole numbers, so a data file, a
  ScriptableObject or code can fill them. A missing number reads as 0, which is the safe
  side: a price of 0 means the item's own value, and a stock of 0 means sold out.
  `ManagedShopLine.Unlimited` (-1) is how a shop that never runs out is written, and any
  negative stock reads as unlimited.
- A free item cannot be written, because a price of 0 means "use the value". An item with
  no value and no price has no price, and the shop refuses to trade it.
- `ManagedShopResult` has one success, `Ok`, and a refusal for each reason: `Closed`,
  `BadCount`, `NotSold`, `NotBought`, `UnknownItem`, `NoPrice`, `OutOfStock`,
  `CannotAfford`, `BagFull`, `NotEnoughItems`, `MissingWallet` and `MissingBag`.

### `ManagedShopDefinition.cs`

**What it does:** The data of one shop: `Id`, `DisplayName`, `ShopkeeperName`,
`RequiredFlag`, `BuyPercent`, and the lists `Sells` and `Buys`. It has no logic.

**Decisions:**
- An empty `RequiredFlag` means the shop is always open. The hidden shop names the flag that
  its mission sets.
- `BuyPercent` is what the shop pays for an item that has no price of its own, as a percent
  of the item's value. The default is 20, a placeholder for "dirt cheap".
- The shop has no stock of its own. It is all data, and where the data lives is still open:
  when the shops plan is available the natural home is a `shops.mdix` file and a baked
  ScriptableObject in `Gtg/Data`, in the style of the item table. These classes were written
  so that binder can fill them.
- Shopkeeper names are empty until they are decided.

### `ManagedShop.cs`

**What it does:** One shop at run time. `TryBuy` and `TrySell` do the trade, `QuoteBuy` and
`QuoteSell` give a price for a screen, `TryGetSellOffer` and `TryGetBuyOffer` read a row of
the window by index without allocating, `IsOpen` and the `Traded` event report state.

**Decisions:**
- It works on a copy of the definition, so editing the definition after the shop is built
  changes nothing. A null line is skipped, and when two lines name the same item the first
  is used.
- It gets the catalog and the flags when built, and the purse and the bag with each trade.
  A null catalog makes every trade report an unknown item. A null flag source keeps a shop
  that needs a flag closed.
- Price of one item bought: the line's price when it is above 0, otherwise the item's value.
  Price of one item sold: the line's price when above 0, otherwise the buy percent of the
  value, rounded down but never below 1 silver for an item that has a value. A percent of 0
  or less, or an item with no value, gives no price. A percent above 100 pays more than the
  value, as written.
- Buying checks, in order: the shop is open, the count is at least 1, the shop sells the
  item, the catalog knows it, it has a price, the stock covers the count, the total fits in
  a number, the purse and the bag exist, the purse can pay and the bag takes the items. Only
  then does coin leave, items enter and stock drop. Selling checks the same way, with the
  bag holding enough and the purse able to take the coin. Any refusal changes nothing. The
  bag is not touched when the purse cannot pay, and if the purse refuses after the bag
  accepted, the items are taken back out and no trade is recorded.
- The shop does not add what it buys to its stock. Whether it should is open.
- A closed shop refuses a quote too, and the flag is read on every call, so the shop opens
  the moment the flag is set.
- `Traded` carries the shop id, the kind, the item, the count and the total. It is a plain
  C# event now. An event queue or a quest objective can subscribe later.
- Totals and the percent price are worked so that huge values stop at the largest number
  and never wrap negative, and a total that cannot be counted is refused.

### `ManagedShopSamples.cs`

**What it does:** Two sample shops: a general store that is always open, and a hidden shop
that needs the flag `sample_hidden_shop_open`. Its `Catalog()` returns the three sample items
of `ManagedItemSamples.cs`.

**Decisions:**
- It exists so buying can be tried before the real shops exist. None of it is game content,
  the names and prices are made up, and the real shops never read it.
- It is left out of release builds, along with the session and the panel.

### `ManagedShopDevSession.cs`

**What it does:** Everything the test panel does except drawing: it holds the shops, a bag
and a flag set, runs a buy, a sell or a sell-all, toggles a flag, words each result and
describes the bag.

**Decisions:**
- It has no Unity types, so it is covered by plain tests, and the panel is only drawing and
  calls. The purse is passed in, because the player's wallet belongs to the scene.
- The price in a message is read before the trade, because a trade can empty the stock the
  price depends on.
- Sell-all with nothing carried says so, instead of reporting a bad count.

### `ManagedShopDevPanel.cs`

**What it does:** A throwaway panel for trying the shop engine. F9 opens and closes it. It
shows the purse, the bag, the shops with their open state, the selected shop's items with
buy and sell buttons, and the flag button of a gated shop. Every action is also logged as
`[GTG Shop]`.

**Decisions:**
- It starts by itself at `AfterSceneLoad`, in the Editor and in development builds only, and
  survives scene loads. A panel the scene already has is left alone, and a second one
  disables itself.
- It looks for the player's `ManagedPlayerWallet` when opened and again at most once a
  second while there is none. It never creates one, because the caster adds it, and a second
  wallet would split the purse.
- Opening it frees the cursor and holds a `ManagedCursorBlock`, the same way the cheat
  panel does. It sits on the left so it does not cover the cheat panel.
- It is the only file of `Shop/` that reads `Camera`. The drawn shop window replaces it.

### `ManagedHudMath.cs`

**What it does:** The functions behind the HUD, with no Unity types: bar clamping, heat to
crown sections, compass offset and fade, and the wheel hit test.

**Decisions:**
- Heat is nine sections, three crowns of three. `CrownSections(heat, crown)` says how many of a
  crown's three sections are lit, and `CrownFill` turns that into a fill amount in thirds. The
  crowns fill one after the other, so heat 4 is one full crown and one third of the second.
- `BarFill` clamps to 0 to 1 and turns NaN into 0, so a broken source never draws a bar past its
  frame. `ClampHeat` clamps to 0 to 9.
- `WrapDegrees` gives an angle in the range above -180 and up to 180, and 0 for NaN or infinity.
- `CompassOffset(yaw, bearing, pixelsPerDegree)` is the wrapped difference of bearing and yaw
  times the scale. A mark to the right of the view has a positive offset. `CompassAlpha` is 1
  inside the bar, falls to 0 across the fade width at each end, and is 0 at or beyond the half
  width.
- `WheelSectionAt(dx, dy, count, dead, outer)` takes canvas units from the wheel's centre with y
  up and returns the section, or -1. Sections run clockwise from straight up, so with four
  sections 0 is the upper right, 1 the lower right, 2 the lower left and 3 the upper left. A
  point exactly on the dead radius or the outer radius is inside the ring. A boundary angle
  belongs to the clockwise section. A point so close to straight up, on the left, that its angle
  rounds to 360 degrees is section 0. NaN gives -1.
- `WheelSectionAngle` and `WheelSectionDirection` give the middle of a section, which is where
  the wheel's icon and name sit.

### `ManagedHudState.cs`

**What it does:** The numbers and text the widgets draw: health, the SP bar, coins, heat, yaw,
the mission text, a version of it, and whether the bottom group is hidden. The feed and the callers write it. The
widgets read it. It has no Unity types.

**Decisions:**
- Every setter cleans its input. Health with a NaN or a maximum of 0 or less zeroes the bar and
  the numbers. The current value is clamped to 0 up to the maximum. A negative coin count is 0.
- `SetVessel(hasVessel, fraction)` gives an SP fraction of 0 when there is no vessel, so a stale
  fraction never draws, and clamps the fraction otherwise.
- Heat is clamped to 0 to 9, and `Wanted` is true above 0. Yaw of NaN or infinity is 0.
- The missions are a title and at most four lines. A null line is an empty line, a null list
  clears the lines and a null title is empty. `MissionLineAt` of an index out of range is
  empty.
- `BottomHidden` is false until the feed or a caller sets it with `SetBottomHidden`. It is true
  while another screen, such as the build tray, needs the bottom of the display.
- `SetMissions` with what the panel shows already changes nothing and does not count.
  `MissionsVersion` goes up once for each real change, so the panel redraws when the version
  differs from the one it drew, and the feed may set the missions every frame at no cost. The
  lines compared are the first four, so a change only in a line that is cut does not count.

### `ManagedHudLayout.cs`

**What it does:** Every rectangle of the HUD, in a 1920 by 1080 reference, and the numbers
behind them. `ManagedHudRect` has an anchor and a pivot that are always the same (0, 0.5 or 1 on
each axis), an offset with y up, and a size, with `Left`, `Bottom`, `Right`, `Top`, `Overlaps`
and `FitsIn`.

**Decisions:**
- The canvas scales with the screen, matching width and height equally, so the numbers hold at
  any resolution. These are placeholders cut from Su's drawn draft. A change of placement is a
  change of a number here and nowhere else.
- Placement: the missions panel at the top left (40, -40) at 440 by 260. The compass at the top
  middle, 640 by 56, 24 below the top edge, 180 degrees across with a tick every 15 degrees and
  a letter every 45. The status panel at the top right (-40, -40) at 460 by 160 with three
  120 by 90 crowns and a coin row. The health bar at the bottom middle 140 up, 520 by 28, and the
  SP bar 106 up, 520 by 20. Five 72 by 72 quick slots 80 apart, 24 up. The wheel button at the
  bottom right (-40, 40) at 170 by 170. The wheel in the middle of the screen at 560 by 560 with
  a dead radius of 70, an outer radius of 280 and its icons 175 from the centre.
- The tests check that every piece lies inside the screen, that no two pieces overlap, that every
  child lies inside its parent and that the children of one parent do not overlap, so a number
  that moves a piece onto another fails a check.

### `ManagedHudWheel.cs`

**What it does:** The model of the item wheel: a fixed number of sections, four by default, each
empty or holding an entry (a label and an icon key), and at most one active section. `SetEntry`,
`Clear`, `TryGetEntry`, `Pick` and `Active`, with the events `Changed` (an entry changed) and
`Picked(section)` (a different section became active).

**Decisions:**
- Each section is one usable thing, and picking it makes it the active one. What an entry stands
  for, a weapon, a spell or an item, is the caller's business. Nothing fills the wheel yet.
- `Pick` of an empty or out of range section is false and changes nothing. Picking the section
  that is already active is true and raises nothing.
- Clearing the active section leaves no section active.
- The wheel knows nothing of the HUD, the mouse or the screen.

### `ManagedHudStyle.cs`

**What it does:** The colours of the placeholder pieces and of the text.

**Decisions:**
- A piece with a drawn sprite is shown untinted, so these colours never tint finished art. Only
  the text colours apply to it.

### `ManagedHudSprites.cs`

**What it does:** Finds the HUD's art by name in `Resources/GtgHud/`, and icons in
`Resources/GtgHud/Icons/`. It also makes the two placeholder sprites, a 1 pixel white square and
a 64 pixel disc with a soft edge.

**Decisions:**
- A PNG may be imported as a Sprite or left as a Texture. Both load, and a Texture is wrapped in
  a sprite. A name that finds nothing returns null and is remembered, so a missing piece is not
  looked up again every frame.
- The placeholders are `HideAndDontSave`, so they never reach a scene file.
- An item's icon is `item_<id>`, for example `item_2`. A wheel entry names its own icon key, for
  example `spell_fireball`.
- The cache and the placeholders are cleared at `SubsystemRegistration`.
- The art, with the size each piece is drawn at in reference pixels: `hud_frame_missions` 440 by
  260, `hud_frame_status` 460 by 160, `hud_compass_bar` 640 by 56, `hud_compass_marker` 12 by 10,
  `hud_hp_back` and `hud_hp_fill` 520 by 28, `hud_sp_back` and `hud_sp_fill` 520 by 20,
  `hud_crown_dim` and `hud_crown_lit` 120 by 90, `hud_coin_gold` and `hud_coin_silver` 40 by 40,
  `hud_slot` 72 by 72, `hud_wheel_button` 170 by 170, `hud_wheel_ring` 560 by 560 and
  `hud_wheel_highlight` 560 by 560. Icons are about 128 by 128.
- A fill piece is drawn as a filled image, so `hud_hp_fill`, `hud_sp_fill` and `hud_crown_lit`
  are cut off from the right as the value falls. The lit crown is drawn over the dim crown at the
  same size.
- `hud_wheel_highlight` is the whole 560 by 560 disc with only the upper right quarter lit. The
  HUD turns it by 90 degrees for each other section.

### `ManagedHudText.cs`

**What it does:** One text object the HUD sets by string and colour, backed by TextMeshPro or by
the built-in UI text.

**Decisions:**
- TextMeshPro draws nothing until its essentials are imported, because the default font lives in
  them. The HUD checks for the settings asset and a default font directly, which keeps the
  importer window from opening in the middle of play. When either is missing it uses the built-in
  text and logs one warning that names the menu entry. The check runs once.
- The built-in text uses `LegacyRuntime.ttf`, and an OS font when that name is not found.
- Setting the text it already has does nothing, so a refresh that finds no change costs no layout.
- Text never takes clicks.

### `ManagedHudBuilder.cs`

**What it does:** The helpers that build the canvas and its pieces: a rectangle from a
`ManagedHudRect`, a stretched child, an image that shows its drawn sprite or its placeholder, the
canvas, and the event system.

**Decisions:**
- The canvas is a screen space overlay with a scaler on 1920 by 1080, matching width and height
  half and half.
- An existing event system is reused. One with the old input module gets it replaced by the Input
  System module with its default actions, because the project uses the Input System.
- Only the pieces that must take clicks have raycasting on.

### `ManagedHudClickView.cs`

**What it does:** Raises `Clicked` on a left click.

**Decisions:**
- It is not a uGUI Button. A Button is selectable, and a selected button fires on Space or Enter,
  which the game uses. This view cannot be selected.

### `ManagedHudSlotView.cs`

**What it does:** The behaviour of one quick slot: a left click uses it, a right click empties
it, and it can be dragged, and dropped on.

**Decisions:**
- Dragging a filled slot starts a `ManagedItemDrag` and shows a ghost icon at the pointer. A
  slot dragged onto another slot swaps the two. A slot dropped on itself stays as it is. A slot
  dragged onto nothing that takes it is emptied.
- An item dropped on a slot from somewhere else is assigned to it, and the carrier says it was
  taken. The source is not named, so a bag window will work with no change here.
- Only the left button starts a drag.

### `ManagedHudFeed.cs`

**What it does:** Copies the game into `ManagedHudState` each frame: the character's yaw, the
health, the selected vessel's charge, the wallet and whether build mode is open. It finds the
player and their health, caster and bag, and the build mode component. It also copies the heat
of the registered heat source and the lines of the registered mission source, when there are any.

**Decisions:**
- It is the only HUD file that reads the character, health, the caster, the wallet, build mode or
  a mission source.
- Build mode is optional. The feed looks for a `ManagedBuildMode` in the scene every
  `BuildSearchSeconds` (2 seconds) while it has none, which is slower than the player search
  because the search walks the scene. While one is found and `BuildModeActive` is true the state
  says the bottom group is hidden. A destroyed build mode no longer hides anything, and a scene
  without one never hides the group.
- It searches again every half second while anything is missing, and not every frame, so a scene
  with no player costs almost nothing. A lost player clears everything it held.
- With no health it zeroes the health bar. With no caster it shows no vessel and no coins.
- Heat. While a heat source is registered the feed asks it for the sections at the player's
  position every frame and sets the heat, 0 when there is no player to place. While none is
  registered the feed does not touch the heat, so `SetHeat` from code and the dev keys work. When
  a source that was read goes away the heat is cleared once, so crowns that nothing owns do not
  stay lit, and after that it is the caller's again. A registered source overwrites a `SetHeat` or
  a dev key press within the frame.
- Missions. The same rules: with a mission source the title and the first four lines are copied
  every frame, nothing is touched without one, and when a source that was read goes away the
  panel shows "Missions" and "No active mission" once, then belongs to the caller again.
- The bag it finds is the one on the player. When there is none and the HUD's option is on, it
  adds a `ManagedPlayerItemBag`.

### `ManagedHudHeatSource.cs`

**What it does:** The seam between the HUD and whatever knows how wanted the player is. It holds
`IManagedHudHeatSource`, one method, `SectionsAt(point)`, which answers with the filled heat
sections, 0 to 9, for a player standing at the point, and `ManagedHudHeatSources`, a static class
that keeps the one registered source. It needs only `UnityEngine.Vector3`.

**Decisions:**
- A source registers itself when it is enabled and unregisters when it is disabled. The feed
  reads `Current` every frame.
- The last source to register wins. A source that is displaced does not come back when the newer
  one leaves, because the registry keeps one reference and not a list. Unregistering a source
  that is not the current one, and registering null, change nothing.
- The interface is in `Hud/` so that `Hud/` never names the system behind it. The file has no
  other dependency, which is why the NPC harness can compile it with a single line.
- The value a source returns is held to 0 through 9 by the state, so a source cannot overflow the
  crowns.
- The registry is cleared at `SubsystemRegistration`, as in the other static registries.

### `ManagedHud.cs`

**What it does:** Builds the HUD and runs it: the missions panel, the compass, the crowns and the
coins, the two bars, the five quick slots, the wheel button and the wheel. It reads input and
draws from `ManagedHudState`, the wheel model and the quick slots.

**Decisions:**
- One HUD at a time. A second `ManagedHud` disables itself with a warning.
- It builds in `Start`, reads input in `Update` and refreshes in `LateUpdate`, after the
  character and the camera. Each piece redraws only when its value changed.
- The crowns are hidden until the heat is above 0. Heat fills crown by crown, and each crown is
  lit in thirds from the left.
- The compass shows the character's yaw, with north as world +Z. Marks slide across the bar, fade
  out near its ends and are clipped to it.
- The SP bar shows the selected vessel's charge, and is empty when the slot holds no vessel.
- A quick slot shows the item's icon, or its name when there is no icon, the count read from the
  bag, and its key. A slot whose item is no longer carried is dimmed and shows 0. Using a slot
  makes it flash briefly and raises `QuickSlotUsed(slot, itemId)`. What using an item does is
  not decided yet.
- The wheel opens with Q or a click on the wheel button. The mouse position is read directly and
  turned into canvas units, and `ManagedHudMath.WheelSectionAt` decides the section, so a click
  is judged by the same function the tests use and not by the shape of a graphic. A click on an
  empty section does nothing and the wheel stays open. A click in the middle ring does nothing. A
  right click or a click outside the wheel closes it. Escape is not handled, because the camera
  rig frees the cursor itself. While the wheel is open the quick slot keys are off.
- Opening the wheel frees the cursor and holds a cursor block, and closing it releases the block
  and locks the cursor again, but only when the wheel was the one that took it and no other
  panel holds a block.
- While the cursor is free, a pointer over a clickable piece of the HUD holds a cursor block, so
  a click on a slot or on the wheel button does not also make the camera capture the cursor.
- Only the five slots, the wheel button and the wheel's backdrop take clicks.
- The quick slot keys are 4 to 8, because 1, 2 and 3 pick spells. The wheel key is Q.
- The two bars and the five quick slots sit under one object, `Bottom Group`, directly under the
  canvas root, so their anchors read as the canvas's. While `ManagedHudState.BottomHidden` is
  true the group is switched off, which hides the bars and the slots and stops their clicks.
  The missions, the compass, the crowns and coins, the wheel button and the wheel stay on. It
  is applied in `LateUpdate` right after the refresh, and only when the value changed.
- While the group is hidden the quick slot keys do nothing. `UseQuickSlot` called from code
  still works, because only the keys are gated. A drag that started on a quick slot is dropped
  when the group hides, because the slot cannot receive the end of the drag once it is
  inactive. A drag from anywhere else, such as a future bag window, is left alone.
- The missions panel redraws when `ManagedHudState.MissionsVersion` differs from the version it
  drew, and not when `SetMissions` is called. Earlier the call set a flag, so a feed that wrote to
  the state directly never refreshed the panel. A change from any writer shows on the next frame
  and setting what is shown already redraws nothing.
- `SetHeat`, `SetMissions`, `Wheel`, `QuickSlots`, `Bag` and `QuickSlotUsed` are the public
  surface. The heat follows a registered heat source and the missions a registered mission source,
  see `ManagedHudFeed.cs`. Both calls still work while no source is registered. Nothing feeds
  the wheel yet.

### `ManagedHudDevKeys.cs`

**What it does:** Dev keys for trying the HUD, in the Editor and in development builds only. `]`
raises the heat by one section and wraps from 9 to 0, `[` lowers it and wraps from 0 to 9, and
`\` stocks the player's bag with the sample items and fills the four wheel sections with a
Fireball, an Ice, an Iron Sword and a Health Potion.

**Decisions:**
- The HUD adds it to its own object when it starts. A release build leaves it out.
- It logs a `[GTG HUD]` line for every wheel pick and every quick slot use, so what the HUD does
  can be seen in the Console before anything is wired to it.
- With no player bag yet, the stock key logs that and changes nothing.
- The heat keys change the heat the dev way. While a heat source is registered, for instance
  `ManagedCrownHudHeat`, the feed overwrites the heat on the next frame, so the keys then show
  nothing. Disable the source to try the crowns by hand.

### `ManagedMissionSource.cs`

**What it does:** The seam between the missions panel and whatever holds the missions. It holds
`IManagedMissionSource`, a title, a line count and a line by index, and `ManagedMissionSources`,
a static class that keeps the one registered source. The tracker of this folder implements the
interface, and so can an adapter for a quest package.

**Decisions:**
- The same registry as `ManagedHudHeatSource.cs`: the last source to register wins, a displaced
  source does not come back, unregistering a source that is not current and registering null
  change nothing, and the registry is cleared at `SubsystemRegistration`.
- The interface says nothing about what a mission is. A quest package that has its own model
  implements three members over it and registers itself, and nothing in `Hud/` changes.
- It has no dependency, so `Missions/` reads from no other folder.

### `ManagedMissionTracker.cs`

**What it does:** A plain list of missions with one active. Each mission has an id, a title and
a list of objectives, each of which is done or not. The active mission is what the panel shows:
its title, then its objectives as lines, with " (done)" added to the ones that are done. With no
active mission the title is "Missions" and the one line is "No active mission". It has no Unity
types.

**Decisions:**
- The first mission added becomes the active one when none is. A later one waits until
  `SetActive` or until the active one is complete or removed, when the first mission that is not
  complete, in the order they were added, takes over. With none left there is no active mission.
- An id must be non-empty and unique. A null or empty title is the id, a null list has no
  objectives, and a null objective is an empty line. The objectives are copied, so a list the
  caller changes later does not change the mission.
- Ticking off the last open objective completes the mission. A mission with no objectives
  completes only through `Complete`, which also ticks every objective. A complete mission takes no
  more ticks, cannot be made active again, and stays held, so `IsComplete` can still be asked.
- Every call that cannot do its job (an unknown id, an index out of range, an objective that is
  done already) changes nothing and returns false.
- Nothing here decides what a mission is for or who gives it. A caller adds and ticks them. There
  is no saving and no event, the feed polls the tracker each frame.

### `ManagedMissionBoard.cs`

**What it does:** The scene component. It owns a `ManagedMissionTracker`, registers it as the
HUD's mission source while it is enabled, and loads the missions listed in the Inspector.

**Decisions:**
- `Tracker` is created on first use, so code may add a mission before the component has started.
- The Inspector list is loaded once, in `Awake`. An entry with no id, a null entry and an id used
  before are skipped. Missions removed or completed in play do not come back when the component is
  disabled and enabled again.
- It unregisters only itself. A board that another source displaced leaves the current source
  alone when it goes.
- To add a mission from game code: `board.Tracker.Add("id", "Title", new[] { "Objective" })`. The
  board is found like any scene component, `FindFirstObjectByType<ManagedMissionBoard>()`.
- Placeholders: there are no missions in the project yet, so the list starts empty and the panel
  shows "No active mission". The names and wording of real missions are not decided.

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
- `Clear()` removes every live hazard at once. The clear hazards cheat calls it. The
  drawing follows on the next frame, because the batch is rebuilt from the list every
  `LateUpdate`.

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
- `Damageable` is the `IManagedDamageable` on the object or a parent. An NPC melee hit uses it, so a source without health can be seen but not hurt.
- Each source owns the engagement of the NPCs that attack it, created on first use (see `ManagedCombatEngagement.cs`). `MaxMeleeAttackers` (default 3, zero is unlimited) and `AttackerRotationSeconds` (default 5, zero is no rotation) live here because the limit belongs to the target, not to the attackers. `AttackerCount` and `StrikingCount` are read-only for tests and debug tools. Negative values clamp to zero.
- Shooters have a pool of their own on the same target: `MaxRangedAttackers` (default 4, zero is unlimited) and `RangedRotationSeconds` (default 5, zero is no rotation). `AttackerCount` counts melee attackers and shooters together, `StrikingCount` counts only the melee attackers that hold a place, and `ShooterCount` and `ShootingCount` are the same two numbers for shooters. A shooter never takes a melee token and a swordsman never takes a shooter's, so a crowd of archers does not keep the swordsmen from the target (owner answer: shooters get their own pool and an outer ring, separate from the melee limit). The 4 and the 5 s are placeholders.
- `AimPoint` is the middle of the visible points (the body, not the feet), or the transform position when there are none. A shooter aims at the last seen position plus the offset of the aim point from the transform, so it shoots at the chest of where the target was.

### `ManagedNpcSight.cs`

**What it does:** The two tests behind sight. `InView` checks range and the view cone with arithmetic only. `HasLineOfSight` casts one `Physics.Raycast` from the eye to the point.

**Decisions:**
- The cone test runs first, so an NPC that cannot face the player costs no ray. Out of range and behind both skip the physics call.
- The ray ignores triggers. It succeeds when the first thing it meets belongs to the source's transform hierarchy, or when it meets nothing.
- A ray that starts inside a collider does not hit it, so the NPC's own capsule does not block its sight. The mask should still leave the NPC's layer out where possible.
- A ray stops a small margin past the target point. A target point inside a collider is found by the surface hit well before that.

### `ManagedNpcBrain.cs`

**What it does:** One NPC. It holds role, backend, can-move, learning mode, disposition, level, the standing order and the sight settings. It builds the 64 byte observation for the Rust decision and stores the action that comes back. It also holds the attack style (melee, ranged or hybrid) and tells Rust how far the threat is and whether the NPC has anything to shoot. It raises `ActionChanged` when the action changes and `OrderRefused` when a companion declines an order. It does not move, fight or trade.

**Decisions:**
- Movement, combat, animation and trading stay out of this file on purpose. Rust chooses, other components act, as in the ECS design.
- A threat lingers for `_loseSightDelay` seconds after the last ray that saw it, so a guard does not flicker between attack and patrol when the player steps behind a pillar for one frame.
- Each ray costs one from a per-tick budget. An NPC that runs out keeps its previous result, see the director.
- A dead brain is left out of the batch and reports Idle.
- The id is a counter that is unique among live NPCs in one play session. It is not stable between sessions, so it cannot key saved memory.
- `LearningMode` is stored and exposed. Nothing reads it yet.
- A threat source on the NPC itself, or on one of its parents or children, is ignored.
- `Threat` and `LastKnownThreatPosition` record what was seen and where. The memory ends with the lingering time, so an NPC chases the last seen position and not the live one.
- Target choice: sight walks the candidate sources nearest first, measured from the eye, and the first one that is in view and in line of sight becomes the threat, so the rays stop there. The current threat counts as 30 percent nearer in distance (its squared distance is halved, `StickyTargetKeyFactor` 0.49), so two sources at similar range do not swap places every tick. While the NPC is provoked, the source that last hurt it counts as 50 percent nearer in distance (squared distance quartered, `AttackerTargetKeyFactor` 0.25), so it goes for what hurt it before a stranger who stands closer. A leader and the NPC's own sources are skipped as before. The ranking uses two scratch arrays shared by all brains, sight never re-enters, and they are emptied after every pass so no destroyed source stays referenced. `LastSeenTime` is internal and tells the engagement whose sighting is the freshest.
- The NPC data lives here, in Unity, and Rust receives a snapshot with every decision: level, order, order level, provoked and disposition. Rust keeps no state.
- Provoked is set by `ManagedHealth.Damaged` with a source, and it runs out after `_provokedMemorySeconds` (20 s). Damage with no source, such as a hazard, damage from the NPC itself, and damage from its leader do not provoke, so friendly fire does not turn a companion on the player. The handler is removed when the brain is disabled.
- `SetOrder(order, level)` gives a companion an order. A level is clamped to 0 to 65535 in the observation, and the order None carries no level. Other roles ignore orders.
- Companion inputs: `Trustworthiness`, `Affinity` and `PaySatisfaction` (0 to 1, 0.5 by default, so loyalty 0.5 and no betrayal), and `BetrayalOpportunity`, which game code sets and which a betrayal uses up. They are sent for companions only, and every other role sends zeros. `Loyalty` is the weighted mix. A scripted betrayal does not use any of this: the story changes the companion directly.
- Each brain has its own random sequence, seeded from its id (xorshift32, never zero), so a run is reproducible. It is the `noise` of the observation, sent for companions only.
- `Betrayed` is raised when Rust returns Betray. The opportunity is used up first, so one opportunity gives one betrayal, and the handler decides what it means (steal, leak, switch sides).
- `TryResolveMission(order, missionLevel, out outcome)` resolves an off-screen mission with this companion's level and its own random numbers.
- A refusal always clears the order and raises `OrderRefused`, even twice in a row. The order is already cleared when the event fires, so a listener can give a new one. A refusal result for a companion that has no order any more raises no event.
- A companion never sees its `Leader`, or anything above or below it, as a threat. A companion with no leader warns once and sees the player as a threat. There is no faction system, so a companion sees no enemy at all today: only the player has a threat source. Companion combat waits for factions.
- The Inspector tooltip says the refusal gap depends on the order type. The gaps live in Rust (`refuse_gap`) and in the C# fallback, and CI compares them on 16,384 NPCs.
- Attack style (ABI version 6). `AttackStyle` is an Inspector setting, melee by default, and is sent as `attack_style`. A ranged or hybrid brain looks for a `ManagedNpcShooter` on its object (again each tick while it is missing, so one added in code after `Awake` is found), and sends `ranged_ready` as the shooter's `IsReady`. With no shooter it warns once and sends not ready, which makes a ranged fighter retreat and a hybrid fight by hand, the two outcomes the rules give for an empty quiver. `ranged_stance` is true while the last action was Shoot or Kite, which is how a hybrid keeps its stance between its two distances (the game keeps this memory, the decision keeps none). A melee brain sends zeros for the three shooting fields.
- `threat_distance` is the distance on the ground plane to where the threat was last seen, so height is not part of it and a target on a roof is as far as one below it. It is at least 0.01, so a known distance is never taken for an unknown one, and zero when no threat is in view. It is sent for every NPC, and only ranged and hybrid fighters read it.
- Shopkeepers (ABI version 6). A merchant with the Retaliatory disposition that was hurt (provoked, see above) now fights back in its own attack style, which Rust decides. Nothing changes in the brain for this, it only sends the same fields. Provoked is global, not per target: a shopkeeper hurt by anyone but its own side fights whatever it sees next, which the actor then aims at the nearest or the sticky threat as usual. Hostile and peaceful merchants are unchanged.
- Crown Heat (ABI version 5). A guard sends the tier of the `ManagedCrownRegion` that holds its position as `CrownHeat`, and every other role sends 0, so only guards can react. Outside every region the tier is 0. The tier is read when the observation is built, so a guard reacts at the next tick after the tier changes. When something hurts the brain, `OnDamaged` hands the hit to `ManagedCrownRegion.ReportHurt`, which decides whether it is a crime. `CrimeRepeatTime` is internal state of that check: the earliest time another hit on this NPC counts as a new assault.
### `ManagedCombatEngagement.cs`

**What it does:** Says where each NPC that attacks one target should stand, and which of them may strike now. Every attacker holds a `Member`. A few members hold an attack token and take evenly spaced places on a ring just inside melee range (the melee ring). The rest wait on a wider ring (the waiting ring), circle slowly, and take over when a token comes free. The actors read their place from here and walk to it. It moves nothing and decides nothing about whether to fight, Rust still chooses Attack. There is one engagement per `ManagedNpcThreatSource`, and it runs a pass every 0.2 s, started by whichever attacker asks first, so there is no driver to add to a scene.

**Decisions:**
- This is the Assassin's Creed style of fight: three attackers at most strike at once (the default, set on the target), the others circle and wait. Everyone still counts as attacking, so the debugger and the observation are unchanged. Unlimited (zero) is available for a design that wants a mob.
- Tokens go to the member that has waited longest, and the nearest wins a tie. A holder gives its token up after the rotation time (5 s) and then queues behind everyone who was already waiting, so over time every attacker gets its turn. A lowered limit takes tokens back from the farthest holders first.
- Holders get places spaced evenly around the melee ring, 70 percent of the attack range from the target, so a striker is always inside its reach with room to spare. The circle is cut into as many equal steps as there are holders, in the order the holders stand around the target, and the whole ring is turned by the circular mean so the group walks the least. Turning it by whole steps would give the same places to the same members, so no search over turns is made.
- Waiters get the same even spacing on the waiting ring, 2.5 m outside the melee ring. The ring widens to give each waiter 1.2 m of arc, so forty waiters do not stack on top of each other. The place sits a step ahead of the waiter along the circle (1.5 m/s of travel), and the direction flips every 8 s, so the group drifts around the target instead of standing in a line.
- The ring is centred on where the freshest sighting among the attackers put the target. Each attacker saw the target within the lingering time, so nobody learns more than it would have known alone, and hiding still works because the rings stay on the last seen position.
- A member that cannot make progress reports itself blocked (the actor does this, see below). The next pass shifts its place along the ring by 0.6 rad for 3 s. The first direction is chosen by the NPC id parity so two neighbours that block each other do not both step the same way. A repeated block inside the window turns the step around and grows it by half, to a limit of 1.8 rad. When the time is up the bias ends and the member goes back to its place.
- A new member starts as a waiter and walks to the waiting ring along the line it stands on, or stays where it is when it is already closer. The first pass comes 0.2 s after the first join, when the others that decided in the same tick have joined too, so the attackers are weighed together and the first to join gets no head start. That delay is the attackers' reaction time.
- Shooters (ABI version 6) are a second class of member with a pool of their own. A shooter joins with its stand-off distance as the ring radius (12 m by default), so its holders sit on a ring of that size, and its waiters on a ring 2.5 m wider (the same waiting distance). The pool has its own limit (`MaxRangedAttackers`, 4) and rotation (`RangedRotationSeconds`, 5 s), with the same rules as the melee pool: longest waiting first, give a token up after the rotation time and queue behind the others, take tokens back from the farthest when the limit is lowered. A pass assigns tokens for each pool separately and spaces the holders and the waiters of each class evenly on its own ring, so four rings are filled each pass (melee holders, melee waiters, shooter holders, shooter waiters). `Join` without the extra flag is a swordsman, so the old call still works.
- `TokenHolderCount` counts melee holders only, and `ShooterTokenHolderCount` and `ShooterCount` count shooters. A shooter switching between Attack and Shoot (a hybrid) leaves the engagement and joins again as the other class, which means it loses its place and queues. That is deliberate, because the two classes have different rings, and it is a cost to watch if hybrids switch often.
- Cost: one pass over two hundred attackers took about 0.1 ms in the harness. The work is a sort by bearing of each group (an insertion sort, the groups are small) and a loop. No allocations after the arrays have grown.

### `ManagedNpcActor.cs`

**What it does:** Carries out the action Rust chose, driving a `NavMeshAgent`. Patrol walks the waypoints in order, or wanders near where the NPC began. Attack joins the target's engagement, walks to its place and hits the target in melee when it holds a turn. Shoot does the same on the shooters' ring at the weapon's stand-off distance and fires from there. Kite backs away from a threat that came too close and fires on the way. Retreat runs directly away. Follow walks to the brain's `Leader` and stops 3 m from it. Idle, Trade, Hold and RefuseOrder stand still. It raises `Attacked` for every hit.

**Decisions:**
- The actor never decides. It listens to `ManagedNpcBrain.ActionChanged`, so the Rust decision stays the single source of what an NPC does.
- Movement uses a `NavMeshAgent`, because the AI Navigation package is installed. The scene needs a baked NavMesh. An NPC that is not on it logs one warning, sends nothing to the agent, and is configured as soon as it is on the NavMesh.
- Attack goes to the place the engagement gave, which is computed around `LastKnownThreatPosition`, not around the live position of the target, so stepping out of sight works as hiding. The destination is refreshed every 0.25 s, and a new place that is less than 0.4 m from the last one is not sent to the agent.
- Melee has a range (1.8 m), a damage (10) and a cooldown (1.2 s), all placeholders in the Inspector. The agent stops 0.25 m short of its place. A holder strikes once it is within 0.6 m of its place, or after 1.5 s inside the attack range whatever the place, so a blocked place never leaves an NPC standing idle in reach of the target.
- A waiter within 1 m of its ring moves at the circling speed (1.5 m/s, zero makes it stand). Everyone else closes in at the chase speed.
- Close to the target (within the waiting ring plus 3 m) the NPC turns to face the target itself and the agent's own rotation is turned off, because an agent turns to where it walks. A circling or flanking NPC would turn its side to the target, leave its view cone, lose sight and drop out of the fight. Rotation is restored when it leaves the engagement.
- Holders get a lower avoidance priority number (they push through) and waiters a higher one (they give way), by 10 around the base value the agent had at start.
- Stuck detection: every 0.5 s the actor checks that an attacker that wants to move has moved more than 0.1 m. After `_blockedSeconds` (1 s) without movement it tells the engagement it is blocked and plans a new path. A place that is more than 1.5 m from the NavMesh, for instance inside a wall, is reported blocked at once and nothing is sent to the agent. A holder out of range that stands still counts as trying to move.
- A hit goes through `ManagedNpcThreatSource.Damageable`. It is skipped when the target is already dead, which matters when two attackers are ready in the same frame. It is also skipped, with no event, when the target really is farther than the attack range plus `_hitLeewayMeters` (0.75 m), measured to where the target is now. The swing aims at the last seen position, and a target that has stepped away since is missed. A miss raises no event.
- The engagement is left when the mode changes, when the NPC dies and when the component is disabled, so no dead or retreating NPC keeps holding a token.
- Follow stops inside `_followDistance` (3 m) and starts again only when the leader is more than a meter farther, so a follower does not shuffle at the edge. A follower with no leader stands still.
- A patrol stop lasts `_pauseSeconds`. A wander point must be on the NavMesh, otherwise the NPC stays where it is until the next try.
- Attack and Shoot are one routine on two rings (`UpdateEngaged`). A shooter joins the engagement as a shooter, walks to its place on the ring at the weapon's stand-off distance, and fires when it holds a shooter token, the target is within the weapon's range, and the settling rule below lets it. Everything else (facing, circling of waiters, blocked places, stuck detection, leaving the engagement) is shared with melee.
- Shooters take a long walk into reach. The melee settling shortcut (strike after 1.5 s in range whatever the place) would have an archer fire 14 m out, so a shooter's settling time is that plus the time to walk to its place at the chase speed (`_settleSeconds`). This was a real bug in the first version, found by the checks (see Fixes and Problems).
- A shot is aimed at the last seen position plus the chest offset of the target. `TryFire` refuses when something stands in the straight line of fire, and then the actor marks itself blocked and asks the engagement for another place, so an archer behind a pillar sidesteps. Being out of ammo or waiting on the cooldown is not a block. A hit on the target itself, or on any part of it, does not block.
- An NPC that cannot move (`Can Move` off: a sentry, a shopkeeper behind a stall) stays where it stands. Before version 6 it still walked toward its place, which made a stationary guard chase. It now strikes when the target is in melee reach, and fires when the target is within the weapon's range, and does not move otherwise. This changes the behavior of stationary guards and merchants that fight, for melee as well.
- Kite (`UpdateKite`) takes no place in the engagement, because a retreating shooter is not holding a position. It turns to face the threat and switches the agent's own rotation off (the same reason as in melee), so it keeps the target in its view cone while it walks backwards. Every repath interval it picks a point on the line from the threat through itself, one stand-off distance from the last seen position, snapped to the NavMesh within 4 m, and fires whenever the target is within the weapon's range. With no NavMesh point to back into it stops and fires, so a cornered archer still shoots. It moves at the retreat speed.
- Stand-off distance and the kite line: the decision tells a ranged fighter to kite when the threat is closer than 6 m, so the shooter's stand-off distance is kept at least 7 m (`OnValidate` in `ManagedNpcShooter`) and at most its range. Otherwise an archer standing at its ring would back away, be told to shoot, walk back and back away again.
- A Shoot or Kite action with no `ManagedNpcShooter` warns once and stands still.
- A dead NPC stops. It is destroyed after `_removeAfterDeathSeconds`, and zero leaves the body.
- Every NPC with an actor runs a small `Update`. The work per frame is a few comparisons, and the agent does the movement.

### `ManagedNpcBallistics.cs`

**What it does:** The aiming maths for a shot that gravity bends. `TryLaunchDirection(from, to, speed, gravity, out direction)` returns the direction that makes a shot of that speed pass through the point. `Spread(direction, degrees, unitX, unitY)` bends a direction by up to the spread angle. Pure arithmetic, no scene access.

**Decisions:**
- It takes the flatter of the two paths that reach the point, so a shot does not lob when it can fly level. Zero or negative gravity flies straight along the line.
- It returns false, and the direction is then the straight line, when the point is out of reach at that speed, when it is straight above or below (less than 1 cm of ground distance), or when the speed is zero. The shooter treats false as a blocked shot and fires nothing. At 35 m/s and half of world gravity the reach on level ground is v squared over g, about 249.7 m, and the checks pin 249 m as reachable and 250 m as not.
- The spread takes a point in the unit disc for each shot (the shooter draws it with `Random.insideUnitCircle`), so the offset is uniform over the cone and never more than the spread angle. A zero spread returns the direction unchanged and draws nothing, so a test with no spread is exact. The random numbers are Unity's, not seeded, so a run with spread is not reproducible, unlike the decision's noise.

### `ManagedNpcShooter.cs`

**What it does:** The ranged weapon of one NPC: a bow, a crossbow or a staff. It holds the ammo, the cooldown and the aim, and looses the shot when the actor asks (`TryFire`). It decides nothing: Rust chooses Shoot or Kite and the actor picks the place. Put it on any NPC whose attack style is Ranged or Hybrid. The brain reads `IsReady` as `ranged_ready`.

**Decisions:**
- One pool of shots serves arrows, bolts and the casts of a mage: a capacity (12, starts full) and optional regeneration in seconds per shot (zero means it never refills, as a quiver does, and a mage sets a time so its mana comes back). Parts of a shot accumulate, and `IsReady` is at least one whole shot. Treating arrows and mana as one pool is my reading of "ammo or mana", and the numbers are placeholders.
- `TryFire` checks in this order: no ammo (`NoAmmo`), the cooldown (`Waiting`), the line of fire, a free place to spawn the shot, the aim solution, then the air is not full. Only a shot that leaves spends a unit and starts the cooldown. A blocked shot costs nothing, and neither does one refused because 256 shots are already in the air.
- The line of fire is a straight ray from the shot point to the aim point, ignoring the shooter's own colliders. It is blocked unless the first thing it meets belongs to the threat. The straight line is used for arcing arrows too, so a shooter never lobs over a wall (an interpretation: it is the cautious choice and the real arc would need a second solve and sweep). A body in the way blocks, so a guard behind another guard does not fire through him.
- An arrow or bolt is aimed with gravity (`_projectileGravityScale`, half of world gravity by default) so it arcs onto the target, and gets the spread. A spell flies straight along the line to the target. For a spell the shot is a full vessel's worth of SP of that spell, the same numbers the player's belt gives a new vessel, and the turn rate and target are zeroed, so a spell that steers (Seeker) flies straight for an NPC. NPC steering is not built.
- The shot point is an offset in the NPC's local space (0.3 right, 1.4 up, 0.5 forward), not a child object. A shot that would spawn inside a collider is refused as blocked.
- `Range` (20 m) is how far the NPC fires. `StandOffDistance` (12 m) is where it takes its ring. `OnValidate` keeps the range at least 7 m (the kite line of 6 m from the decision, plus a 1 m margin) and the stand-off between 7 m and the range. The kite distance is read from `NPCManagedFallback.KiteDistance`, which is a copy of the Rust constant and must be changed by hand with it.
- `Fired` is raised for every shot, for an animation or a sound. Nothing listens yet. `AddAmmo` takes shots away with a negative number and `Refill` fills the pool. Both work before `Awake` has run, for a shooter made in code.
- All numbers (range, stand-off, cooldown 1.5 s, spread 2 degrees, speed 35 m/s, damage 12, radius 0.1 m) are placeholders for balance work.

### `ManagedNpcShots.cs`

**What it does:** Every shot an NPC has in the air: the arrows and bolts of archers and crossbow guards, and the spells of mages. They fly through one `ManagedSpellFlight`, the same flight code the player's spells use, so a shot is a few array entries and not a GameObject. An arrow or bolt that hits hurts what it hit. A spell that hits or runs out of SP raises the same `Impact` event as the player's spells, through `ManagedSpellCaster.Raise`, so the spell damage and the chemistry field treat it like any other impact.

**Decisions:**
- At most 256 shots at once (`MaxShots`). A shooter whose shot does not fit keeps its ammo and tries again.
- Each step sweeps a sphere from the old position to the new one (a sphere of the arrow's radius, 0.1 m by default, or the spell's sweep radius), so a fast shot cannot pass through a thin collider. The sweep ignores triggers, skips the colliders of the shooter itself (restarting just past them, up to four times) and uses `HitMask`, every layer by default.
- An arrow or bolt is unpowered: it carries one point of SP that never burns, so it ends on a hit or at its time cap (4 s), and a spent arrow just drops. A spell that runs out of SP collapses where it is and releases its payload there, as the player's does.
- Damage goes through `IManagedDamageable` on the collider or a parent, and a body that says it is down is not hurt at all, whatever it would do with the hit. Arrows and spell explosions hurt anything with a damageable, other NPCs and the shooter's allies included, because there are no factions yet. Spell damage goes through the existing `ManagedSpellDamage`, so the mage is not hurt by its own explosion unless `ManagedSpellDamage.HurtCaster` is set.
- The side arrays (source, damage, radius, diameter, color) move in step with the flight store when a shot is removed (a swap with the last one), so index i always describes the same shot. A check covers each array.
- `FillBatch` writes one sphere per shot into a `ManagedSphereBatch`: an arrow as a sphere of its diameter and color (there is no arrow model), a spell at its own size and color, dimmer and smaller as its SP drains (down to 35 percent brightness and 60 percent size, as the player's are).
- The store is static, created by the first shot, and cleared at `SubsystemRegistration` and when its driver is destroyed.

### `ManagedNpcShotDriver.cs`

**What it does:** The hidden object behind `ManagedNpcShots`, named `GTG NPC Shots`. `Update` steps the shots by the frame time, and `LateUpdate` fills one `ManagedSphereBatch` and draws it. The first shot creates it, nobody adds it to a scene. When it is destroyed, for instance on a scene change, the batch is disposed and the shots in the air go with it.

**Decisions:**
- It is `internal sealed`, and `ManagedNpcShots` is the only thing that knows it exists.
- The step is the frame time, so a long frame makes a long step. The sweep covers the whole segment, so a long step cannot tunnel through a thin collider, but an arrow can reach its time cap sooner in game time than in frames.
- Without a physics world or a batch material in the harness the batch is a stub that records what was added, so the drawing itself is not checked, only what is handed to it.

### `ManagedNpcDebugView.cs`

**What it does:** Editor-only drawing and text for the NPC debugger, in `Managed/NPC/Editor/` so it compiles into `Assembly-CSharp-Editor` and never reaches a build. It draws one NPC in the Scene view: a label, the path to the destination, a marker on the destination, the patrol route with numbered stops, the wander, attack or retreat range, the sight cone, and a cross where the threat was last seen. Colors follow the action: grey Idle, cyan Trade, green Patrol, red Attack, amber Retreat, blue Follow, purple Hold, pink RefuseOrder, teal Mission, dark red Betray, orange Shoot, rose Kite. A ranged or hybrid NPC's line shows its style and its ammo (`ammo 7/12`), or `no weapon`. A companion's line shows its level, order, loyalty and whether it could betray, a non-hostile disposition is named, and provoked is shown. A guard that stands in a region with Crown Heat adds `crown` and the tier name (wanted, hunted, outlaw) to its line. A follower draws a dotted line to its leader and a ring of the follow distance around the leader. An attacker that is part of an engagement shows `striking` or `waiting` in its line (`firing` for a shooter that holds a turn), and with the Ranges switch on it draws a ring at its place with a dotted line to it, red for a holder and amber for a waiter, orange for a shooter that may fire. A shooting or kiting NPC also draws a disc of the weapon's range.

**Decisions:**
- It reads public state only and changes nothing in the game. The brain, the actor and the director gained read-only accessors for it (`Current`, `GetBrain`, `ViewRange`, `FieldOfViewDegrees`, `Home`, `AttackRange`, `WanderRadius`, `RetreatDistance`, waypoints, `HasPath`, `Destination`, `GetPathCorners`). No game code reads them.
- The visualization lives in editor code and uses `Handles`, so the runtime components carry no `UnityEditor` references and no `#if UNITY_EDITOR`.
- The path comes from `NavMeshAgent.path`, which allocates inside Unity on every read. That is acceptable in the Editor and is the reason the call exists only for the debugger.
- At most 300 NPCs are drawn per repaint, so a crowd stays usable. The list shows the same cap.
- Switches: scene drawing (off, selected only, all), an action filter, only NPCs that see a threat, labels, paths, ranges and sight cones. Sight cones start off, because they are noisy with many NPCs.

### `ManagedNpcDebuggerWindow.cs`

**What it does:** The window behind it, opened from the menu GTG, NPC Debugger. It shows the director line (NPC count, batch size, rays this tick, native library or managed fallback), one line per Crown region (name, heat and tier), the switches above, and a live list of NPCs. A click on a row selects the NPC and frames it in the Scene view. It subscribes to `SceneView.duringSceneGui` while it is open and unsubscribes when it closes.

**Decisions:**
- Nothing is drawn in Edit mode, and the window says so. The registry of brains only fills in Play mode.
- The window repaints ten times a second through `OnInspectorUpdate`, and repaints the Scene view only while drawing is on and the game is running.
- Scene drawing needs the window open. Closing it removes every overlay.
- "Selected only" matches the selected root object. Selecting a child of the NPC does not count.
- Waypoints are shown but not editable here. Moving them is still done with the transforms in the Scene view.

### `ManagedMissionResolver.cs`

**What it does:** Decides how a companion mission that happens off screen ends: success, failed, caught or killed. It calls `gtg_npc_resolve_mission` in the Rust library and uses a managed copy of the same rules when the library is not there.

**Decisions:**
- Only a delivery and a raid are missions. Any other order, or a level outside 0 to 65535, returns false with no outcome.
- A mission the player joins is played out and does not come through here. The player gets a notification and the reward when the result is success. Scheduling, the notification, rewards, capture and rescue are not built.
- The managed copy keeps the float arithmetic in the same order as Rust, and the C# test compares both on 200,000 inputs with no difference allowed. The numbers are placeholders for balance work.

### `ManagedNpcDirector.cs`

**What it does:** Drives every brain. Each tick takes a bounded batch, runs sight for the living NPCs, makes one native call for the whole batch and hands the actions back. A brain creates the director when the scene has none.

**Decisions:**
- One native call per tick, never one per NPC. The tick is 0.1 s and the batch is capped at 256, the same numbers as the ECS decision system. A larger population is served in turns through a rotating cursor.
- Sight rays have their own cap per tick, 128 by default, so a crowd cannot spend an unbounded number of raycasts in one frame. When the cap is hit, the NPC that ran out leads the next tick, so every NPC is served in turn and none starves.
- The tick works in three phases. Phase 1 copies the batch out of the list. Phase 2 runs sight and builds observations. Phase 3 applies actions. `ActionChanged` listeners run only in phase 3, so a listener that disables or destroys an NPC cannot change the list under the loop.
- A decision is applied only if its NPC id matches the row it was made for and the action is in range (0 to 9 since ABI version 4, 0 to 11 since ABI version 6, up to Kite). The ECS system still stops at 4, which is correct for it because it has no companions and no ranged fighters.
- A scene director is reused even when it is disabled, because a disabled director is the scene's choice. A second enabled director disables itself with one warning.
- With no `ManagedNpcThreatSource` in the scene the director warns once, because that is the usual reason for NPCs that never see anything.
- `Brains` and the id counter are static and cleared at `SubsystemRegistration`. `Current` and `GetBrain(index)` are public for the NPC debugger.
- Tick cost is about one managed array fill, one pinned native call and at most the ray cap, all on the main thread.

### `ManagedCrownTypes.cs`

**What it does:** The two small types of Crown Heat. `ManagedCrimeKind` names what the player did (Assault and Murder of a merchant or civilian, AssaultOnGuard and MurderOfGuard). `ManagedCrownTier` holds the tier numbers (Clear 0, Wanted 1, Hunted 2, Outlaw 3, Max 3) and `Name(tier)`.

**Decisions:**
- The tier numbers are the ones the native decision receives, so they are constants and not an enum, and the brain casts one to a byte without a conversion table. `Max` equals `MAX_CROWN_HEAT` in Rust and a test checks it.
- Hunted and Outlaw decide the same as Wanted today. They exist so a region can already tell the difference for the debugger and for the rules that are not built (bounty hunters, the Crown's knights).
- A kind is about the victim and not the weapon. Crimes against anyone else (an enemy, a boss, a companion) are not crimes, see `ManagedCrownRegion.cs`.

### `ManagedCrownRegion.cs`

**What it does:** One area where the Crown's law holds, and the heat the player has earned in it. A box volume (size in meters, not rotated) on an empty GameObject. Heat rises when somebody hurts or kills a merchant, civilian or guard while a guard of the region sees the offender, and fades while no guard sees anything. The tier of the heat (10, 40 and 75 by default) is what guards in the box send to the native decision. It raises `TierChanged` and `CrimeReported`. It also offers `Add`, `Reduce` and `Clear` for bribes, pardons and story events, and static `Count`, `Get`, `Find` and `TierAt` for lookups.

**Decisions:**
- Heat is regional and not global. The same crime in another town earns nothing there. A point outside every box has tier 0, so a crime in the wilderness is not a crime. Where boxes overlap, the first to register wins, so keep them apart.
- A crime counts only when a live guard of the region has the offender in view right now (`ThreatVisible` and `Threat` match). No witness means no heat, and the event still fires with `seen` false so a story system can react to it.
- The offender is whatever `ManagedNpcThreatSource` the damage source belongs to, found on the source object or a parent. Today that is the player. A second threat source would earn heat the same way, which is the reason the Rust rule cannot tell who the threat is yet.
- Amounts are per crime kind (assault 5, murder 25, assault on a guard 15, murder of a guard 40) and the cap is 100. All are Inspector fields and all are placeholders.
- Hurting the same victim again inside `_assaultRepeatSeconds` (3 s) counts once, so a flurry of blows on one victim is one assault and not twelve. A killing blow always counts, even inside the window, and it adds the murder heat on top of any assault already counted.
- Fade: every 0.5 s the region scans whether any guard in the box sees any threat, and a sighting restarts the clock, as does every crime and every `Add`. After `_unseenSeconds` (20 s) without one, heat falls by `_fadePerSecond` (0.5) per second. So a wanted player who hides loses it, and one who stays in sight of a guard keeps it. The scan sees any threat, not only the offender, which is the same single-player simplification.
- Only a region with heat runs the scan, and the scan walks the director's brain list, so an idle town costs one float compare per frame.
- `Reduce` does not touch the fade clock, and `Clear` is `SetHeat(0)`. A tier change raises the event once, whatever caused it.
- The thresholds are clamped in `OnValidate` so wanted is at most hunted is at most outlaw is at most the cap, and nothing can be negative.
- The region list is static and cleared at `SubsystemRegistration`, as in the other registries. `OnDrawGizmosSelected` draws the box, yellow when clear and red while wanted or worse.
- The meter is not saved. Persistence is a game system that does not exist yet.
- `WantedAt`, `HuntedAt` and `OutlawAt` are read-only properties over the three tier thresholds,
  next to `MaxHeat`. They were added for `ManagedCrownHeatMap.cs`, which measures its thirds from
  them. They change no behaviour.

### `ManagedCrownHeatMap.cs`

**What it does:** Turns the heat of a region into the nine sections of the HUD, three crowns of
three. One static function, `Sections(tier, heat, wantedAt, huntedAt, outlawAt, maxHeat)`. It
is plain arithmetic with no Unity or NPC types.

**Decisions:**
- One crown for each tier: wanted lights the first, hunted the second, outlaw the third, and the
  crowns before it are full. Inside a tier the three sections fill in thirds as the heat climbs
  from the tier's threshold to the next one. The outlaw tier runs up to the most heat the region
  holds. With the defaults 10, 40, 75 and 100: heat 10 shows 1 section, 20 shows 2, 30 shows 3, 40
  shows 4, 75 shows 7, and 92 and more shows 9.
- A tier of clear or less is 0, because the HUD hides the crowns then. From wanted on, at least
  one section is lit, so the crowns appear the moment the player is wanted.
- The tier is a parameter, so the map always agrees with the region's own tier rule and does not
  copy it. A tier above outlaw counts as outlaw.
- A heat exactly on a third falls in the upper third. The comparisons multiply and do not divide,
  so a whole number heat on a whole number band is exact.
- A band with no width, which the region's `OnValidate` allows, is full. A heat that is not a
  number is 0. The function never divides by zero.
- Placeholders: the thirds, and the choice of one crown for each tier. The HUD has no say in it.

### `ManagedCrownHudHeat.cs`

**What it does:** The component that feeds the HUD's crowns from Crown Heat. It implements
`IManagedHudHeatSource` and registers itself while it is enabled. When the HUD asks, it finds the
region around the player's position with `ManagedCrownRegion.Find` and answers with
`ManagedCrownHeatMap.Sections` of that region, or 0 outside every region.

**Decisions:**
- Add it to any object, one is enough. If more than one is enabled the last to register is the
  one the HUD asks.
- The player's position comes from the HUD, which already knows the player. This file does not
  look for the player or the HUD, and names no type of `CharacterController/`.
- The heat is the region the player stands in. Walking out of a town shows no crowns, though the
  heat stays in the region. Walking back in shows it again.
- Its only reference to `Hud/` is the interface of `ManagedHudHeatSource.cs`. The component itself
  needs `ManagedCrownRegion`, so the building harness does not compile it. The NPC harness does,
  see CI and Workflows.

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
  `Managed/Health`, `Managed/NPC` (including its `Editor/` folder), `Managed/Crown` and the spell damage files (C# 9, as in Unity 2022.3)
  against hand written `UnityEngine`, `UnityEditor` and `NavMeshAgent` stand-ins in
  `rust/npc/managed-npc-test/` and runs 1130 checks, once on the managed fallback and
  once on the real library. The spell caster itself is replaced by a stub that has only
  the `Impact` event and `Raise` (and the sphere batch by one that only records what it is given). The spell definition is compiled too, so the flight, payload,
  modifier and vessel type files that it depends on are part of the project.
- `.github/workflows/managed-csharp-ci.yml` - the compile gate for the rest of `Managed/`.
  The project has no assembly definitions, so one compile error in any Managed file stops
  every system in Unity, and nothing else in CI catches it. The `managed-compile` job builds
  `building-test/BuildingTest.csproj` and runs it. The project compiles every file under
  `Managed/` except `NPC/`, `Crown/` and `Chemistry/` (the first two need the NPC native
  bindings and are compiled by the `managed-npc-fallback` job below, the third needs the
  Alembic package), plus `Data/ItemDatabase.cs` and the two plain arithmetic files of `Crown/`,
  `ManagedCrownTypes.cs` and `ManagedCrownHeatMap.cs`, at C# 9 with .NET 8, against hand written
  stand-ins for `UnityEngine`, uGUI, TextMeshPro and the Input System in `building-test/`. Then
  it runs the checks in that folder: 803 at the time of writing, covering building, the camera
  rig's cursor hook, the cursor block, the HUD and the bottom group, the Crown heat map, the heat
  and mission sources, the mission tracker and board, and how the feed and the HUD read them. It
  runs on a push or pull request that touches `Managed/`, `Data/`, `building-test/`,
  `rust/npc/managed-npc-test/` or the workflow, and by hand. The folder is named for the first
  system it covered and now covers the whole core stack.
  - The `managed-npc-fallback` job runs `rust/npc/managed-npc-test` on the managed fallback,
    which needs no native library. It compiles `Health/`, `NPC/` and `Crown/`, which includes
    `ManagedCrownHudHeat.cs`, and runs their checks (1130 at the time of writing). The path filter
    of `npc-rust-ci.yml` does not list `Managed/Crown/` or `Managed/Hud/`, so without this job a
    change to the heat interface in `Hud/` would break `Crown/` unseen. That workflow stays the one
    that runs the real native library.
  - The NPC harness project has one extra line for the HUD: it includes
    `Hud/ManagedHudHeatSource.cs`, the interface that `ManagedCrownHudHeat` implements. Without it
    that file does not compile there. The bridge itself, which is `Find` and the map and no more,
    has no check in that harness. It was checked once by hand there and the map has its own
    checks in `building-test/`.
  - It needs no network. `building-test/nuget.config` clears every package source and the
    project has no package references.
  - A red compile step means a Managed file would break the Unity compile, or it uses a Unity
    member the stand-ins do not have yet. Read the first error. In the second case add the
    member to a stand-in file in `building-test/` and keep it as close as possible to the
    signature in the Unity reference source.
  - It proves the code compiles at C# 9 against the stand-ins and that the checks pass. It does
    not prove that Unity behaves the same, and the real TextMeshPro, uGUI and Input System
    packages are not part of it.
  - The check files are `Tests*.cs`. The stand-ins are `UnityStubs.cs`, `UnityMoreStubs.cs`,
    `UguiStubs.cs`, `UiStubs.cs` and `PackageStubs.cs`. `RigStubs.cs` is a comment only and the
    project skips it. It stays because the replacements Action replaces files and cannot delete one.
  - Neither job covers `Managed/Chemistry/`, which needs Alembic.

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

### SP drive flight (`ManagedSpellFlight.cs`, `ManagedSpellPayload.cs`)

- Reported: every spell flew at a constant speed for a fixed lifetime, so neither the
  vessel nor the mix could change how far a shot went.
- Now: a shot carries SP from its vessel and burns it in flight. The mix changes the
  burn only through the part of its load above the vessel's rating. A shot with no SP
  left collapses in place and releases its payload. The numbers are in
  `ManagedSpellDefinition`, and the design is in `magic.md`.
- Checked with a console harness against stubs of `UnityEngine`: 21 checks pass,
  covering the range at four frame rates, the overload cost, a thrown arc, drag, the
  flight cap, swap-remove and growth. The harness is not in the repo. The five Magic
  files also compile under C# 9 against the stubs. Not verified in the Editor: the
  sweep, the draw path and the dimming ran only as a compile.
- The stand-in vessel was full at every cast in this change, so nothing was consumed or
  recharged. See the Vessels subsection.
- The payload loads are placeholders. The fireball and ice have the same range as before
  because both sit under the rating.
- Steering was left for the next change, see the Steering subsection. The thrown row
  exists in the definition and in flight, and no spell uses it.
- `_detonateAtRangeEnd` is gone. A scene that serialized it drops the value without an
  error.
- The chemistry field and the damage code still choose by kind. `PayloadId` is carried
  and unused.
- The ECS stack is unchanged and still flies a straight shot with a lifetime.
- Edit to existing Managed files: `ManagedSpellTypes.cs` gained `PayloadId`,
  `ManagedSpellDefinition.cs` gained the flight and vessel fields, `CreateSpawn` and the
  derived values, and `ManagedSpellCaster.cs` flies through `ManagedSpellFlight`.
  `ManagedCameraRig` reads `ShotCount` as before.

### Steering (`ManagedSpellSeeker.cs`, `ManagedSpellFlight.cs`, `ManagedSpellCaster.cs`)

- Asked for: homing and curved flight on the SP drive. The lock is the target if there
  is one and the crosshair point if not, turning costs SP per radian, and the first
  steered spell is a third placeholder spell so it can be felt in the game.
- Now: spell 3, the Seeker, locks at the cast and turns toward its lock each step at a
  limited rate. A turn burns SP in proportion to the angle, so a shot that chases a fast
  target flies a shorter way than a straight one. The design is in `magic.md`.
- Found in testing: a shot locked on an open-air point flew past it, turned back and
  circled it until the SP ran out. It collapsed at 34.6 m of a 70 m flight. The seeker
  now marks a point as passed once the shot's heading points away from it, and the shot
  flies on straight.
- Checked with the console harness against the `UnityEngine` stubs, with a fake physics
  world for the seeker: 46 checks pass. They cover the turn rate cap, 15.7 SP for a 90
  degree turn, the collapse point counting a turn, swap-remove keeping the steering
  fields, the lock rules (nearest the aim line, dead, outside the cone, behind a wall,
  own collider, out of range), slot growth and reuse, a shot that hits a walking target,
  a dead target, and a shot with no target that collapses at the end of its range. A
  mutation check with the passed latch removed fails three of them. The Managed files
  also compile under C# 9 against the stubs. The harness is not in the repo.
- Not verified in the Editor: how `OverlapSphereNonAlloc` and `Linecast` behave with real
  colliders and layers, whether NPC colliders sit in the hit mask, key 3, and the draw
  path of the third spell.
- A shot that misses a living target turns back for it until it hits, or the SP runs out.
- Line of sight is checked at the cast only. After that the shot turns toward the target
  through walls, and the sweep stops it on the first collider like any other shot.
- The shot leaves along the aim direction and turns from there. A launch angle that
  curves from the hand toward the crosshair is not built.
- The stand-in vessel was still full at every cast in this change, see the Vessels
  subsection. The Seeker name, its numbers and its color are placeholders.
- The ECS stack is unchanged. The cheat code manager is not started, and `CreateSpawn`
  is where a cheat would change a spell's numbers.
- Edits to existing Managed files: `ManagedSpellTypes.cs` gained `Seeker`,
  `ManagedSpellDefinition.cs` gained the steering fields, the Seeker row and the third
  slot, `ManagedSpellPayload.cs` gained a load for it, `ManagedSpellFlight.cs` gained
  `Target`, the turn fields and `Steer`, `ManagedSpellCaster.cs` locks, steers and
  retires, `ManagedCharacterInput.cs` reads key 3, and `ManagedCameraRig.cs` lists it in
  the hint.

### Vessels (`ManagedVesselBelt.cs`, `ManagedVesselTypes.cs`, `ManagedSpellDefinition.cs`, `ManagedSpellCaster.cs`)

- Asked for: bottles that are used up and orbs that recharge slowly, so the player plans a
  fight. An orb keeps its payload and only its SP recharges. SP a shot does not burn is
  lost with it. An orb can cast partly charged and flies a shorter way. Reuse the studio
  packages where they fit.
- Now: the caster owns a belt of vessels, drawn from on every cast. A bottle gives one full
  charge and is used up. An orb gives all the SP it has and is empty after, and charges
  back over 45 seconds of game time. A shot starts with the SP drawn, and its burn rate is
  worked out from a full vessel, so a 40 percent orb flies 30 m of the fireball's 75.
- Packages checked, not used. Inventorizz (`unity-chem-sim`) is a slot container with a
  lock, drag and drop adapters and mdix authored content. Its slot rows hold an item id and
  a count only, so the SP of an orb cannot live there. The SP stays in the belt, and an
  inventory bridge would map slot item ids to belt vessels. The package is not in this
  project: the manifest lists Alembic only, and it also needs `com.midmanstudio.dragndrop`
  and the mdix package. A package that fails to resolve blocks every system, so the Managed
  stack takes no dependency on it for now. `MID_Logger`, the level gated logger in the
  utilities package of `MidManStudio_Unity`, is not in the project either, so the Magic
  files still log through `Debug.Log` with a `[GTG Magic]` prefix. Both would be added with
  a `file:` entry in `Packages/manifest.json`, the way Alembic is.
- Checked with the console harness against the `UnityEngine` stubs: 64 checks pass, 18 of
  them new. They cover bottle counts, an orb emptied by a cast, recharge at three frame
  rates, a partly charged orb flying 30 m at three frame rates, the rating changing the
  overload cost, refill, and out of range slots. Seven deliberate breakages, such as a
  bottle that is not used up or a drive cost taken from the SP drawn, each fail at least
  one check. The Managed files also compile under C# 9 against the stubs. The harness is
  not in the repo.
- Not verified in the Editor: the Inspector array of loadout entries and its default, the
  context menu entry, and the overlay text.
- The belt is filled from the Inspector loadout at every play. Nothing is saved, and there
  is no way to gain bottles in the game yet, only Refill vessels.
- The recharge time of 45 seconds, the five bottles and the vessel numbers are placeholders.
  The vessel numbers still come from the spell's definition.
- Waiting does not skip time, so it does not recharge orbs. Whether it should is open in
  `magic.md`.
- The ECS stack is unchanged. `RefillVessels` and `CreateSpawn` are where a cheat acts, see
  the entry for currency and cheats below.
- Edits to existing Managed files: `ManagedSpellDefinition.cs` gained `DriveSpPerMeterFor`,
  `HoldSpPerSecondFor(load, rating)` and `IndexOf`, and `CreateSpawn` takes a vessel draw.
  `ManagedSpellCaster.cs` builds and ticks the belt and draws on every cast.
  `ManagedCameraRig.cs` shows the vessel in the overlay.

### Currency and cheats (`Economy/`, `Cheats/`, `ManagedVesselCharging.cs`, `ManagedSpellModifiers.cs`)

- Asked for: a cheat code manager that makes testing easier, with every cheat reset after
  play, and currency: gold, silver and SP, where SP both powers things and is money, like
  the soul stones in Skyrim. Decided with Su: cheats by typed codes plus a panel, in the
  groups vessels and spells, player, time and world, with give-money cheats beside them. A
  cheat stays on until it is switched off or play ends. Cheats exist in the Editor and in
  development builds only. 100 silver make a gold. SP is carried as crystals of fixed sizes.
  A crystal refills an orb at once and is used up. The scope is the wallet, the overlay, the
  give-money cheats and the manager, with no shops and no price list yet.
- Now: the player has a purse of gold, silver and crystals, shown in the overlay. R uses
  the smallest crystal that fills the selected orb and the orb fills at once. The caster
  carries a `ManagedSpellModifiers` that `CreateSpawn` reads, and the 16 cheats change the
  modifiers, the character, the health, the NPCs and the hazard field through a host
  interface. Codes are typed at any time. The backquote key opens a panel with every cheat.
- Checked with the console harness against the `UnityEngine` stubs: 180 checks pass, 116
  of them new. They cover the coin arithmetic and refused payments, crystal choice, room
  in an orb and charging at every result, the modifiers changing a shot (twice the range
  flies 150 m of the fireball's 75), the registry (code rules, typing with junk, gaps and
  buffer clearing, toggles, one-shots, failures, `RevertAll`), every built-in cheat and
  every code typed in full without setting off another, the real caster driven through R,
  endless vessels and the cooldown, the scene host against a stub scene (damage, heal,
  killing, clearing, respawn), the manager (typing, panel, cursor, scene loads, keyboard
  swap, one manager only, bootstrap) and the rig (cursor gate, purse line). 28 deliberate
  breakages each fail at least one check. The Economy, Magic, Cheats, input, camera rig and
  health files compile under C# 9 against the stubs, with the real sources. The character
  and the hazard field are stand-ins in the harness, because they need the Unity character
  controller and Alembic. Their edits are one property and one method, read by eye. The
  harness is not in the repo.
- Not verified in the Editor: the panel drawing and clicks, typed input through
  `Keyboard.onTextInput`, the backquote key, the cursor release and relock, R and the west
  button, `FindFirstObjectByType` and `FindObjectsByType` in a real scene, the manager
  creating itself at `AfterSceneLoad`, slow motion through `Time.timeScale`, and the purse
  line in the overlay.
- Placeholders: the crystal values (25, 100, 250), the starting purse, the cheat numbers
  (twice the range, twice the speed, a quarter time, skips of 15 and 60 seconds, 10 gold,
  50 silver, 3 crystals of each size) and the codes.
- Known limits. A code typed in the game also presses the game keys: the letters of a code
  can move the character, F fires, R uses a crystal, a digit picks a spell and Space jumps.
  The toggles switch off on a full scene load and at the end of play, and not on a respawn
  inside a scene, so a toggle can show on while a new player object is back to normal. A
  release build has no cheats, and the wallet stays. The crystals and coin are not saved.
  Nothing can spend gold or silver yet.
- Packages: still none. Inventorizz and the logger of `MidManStudio_Unity` are not in the
  project, so the cheats log through `Debug.Log` with a `[GTG Cheat]` prefix. An inventory
  bridge for the crystals would need the package added to `Packages/manifest.json` first.
- The managed NPC CI job had stopped compiling after the SP drive drop. `ManagedSpellDefinition.cs`
  needs `ManagedShotSpawn` and `ManagedVesselDraw`, which live in files the project did
  not include. The project now includes `ManagedSpellFlight.cs`, `ManagedSpellPayload.cs`,
  `ManagedSpellModifiers.cs` and `ManagedVesselTypes.cs`, and the stubs gained `Mathf.Sin`,
  `Mathf.Acos`, `Mathf.Abs`, `Vector3.Cross` and `Vector3.right`. All 645 checks pass on
  the managed fallback. The run against the real native library was not repeated.
- The ECS stack is unchanged.
- Edits to existing Managed files: `ManagedVesselBelt.cs` gained `MissingSpAt` and `Charge`.
  `ManagedSpellDefinition.cs` gained a `CreateSpawn` overload that takes modifiers.
  `ManagedSpellCaster.cs` owns the modifiers and the purse, reads R and has `AdvanceTime`.
  `ManagedCharacterInput.cs` reads R and the west button. `ManagedCharacter.cs` gained
  `MoveSpeedScale`. `ManagedChemicalHazardField.cs` gained `Clear`. `ManagedCameraRig.cs`
  gained `CursorCaptureBlocked` and the purse line.

### Shop, items and flags (`Items/`, `Flags/`, `Shop/`, `IManagedCoinPurse.cs`)

- Asked for: start the shop, with each system in its own folder and decoupled, so that the
  patterns package of `MidManStudio_Unity` can be adopted later when Su has a computer. The
  art and the window come later from Su's drawings. Decided with Su: bought items go in a
  small bag in its own folder, shops take coin only, the hidden shop opens on a plain flag
  for now with the Questly package to be used later, and a test panel for the Editor and
  development builds.
- Now: three small folders and one interface. `Items/` has the item value, a catalog
  interface with a code-filled catalog and an adapter over the baked item table, a holder
  interface and a slot bag. `Flags/` has a one-method flag interface and a set. `Shop/`
  has the data classes, the shop with buying, selling, quotes, stock, the gate and the
  `Traded` event, the sample data, and the test panel with its session. `ManagedWallet` now
  implements `IManagedCoinPurse`. The Currency and cheats entry below says nothing can spend
  gold or silver yet, and the shop is the first thing that can.
- Checked with the console harness against the `UnityEngine` stubs: 315 checks pass, 135 of
  them new. They cover the bag (stacking, topping up, slots, removal order, refused adds
  and removes, a shrinking catalog), the catalog and the adapter against the real
  `ItemDatabase` class, the flags, every shop rule listed under `ManagedShop.cs` including
  each refusal leaving the wallet, bag and stock unchanged, a holder that lies, a purse that
  lies, a bag that must not be touched, the percent rounding and the overflow limits, the
  session's wording of every result, and the panel (F9, cursor, wallet lookup, one panel
  only, bootstrap). 102 deliberate breakages, 29 from before and 73 for this drop, each fail
  at least one check. The `Items`, `Flags`, `Shop` and `Economy` files compile under C# 9
  with the real sources, and the real `ItemDatabase.cs` compiles against a stand-in for the
  mdix attribute. The harness is not in the repo.
- Not verified in the Editor: the panel drawing and clicks, F9, finding the wallet in a real
  scene, the adapter against a baked asset, and the panel's cursor handling next to the cheat
  panel.
- Placeholders: the sample items, shops and flag name, the buy percent of 20, the bag's 20
  slots, and reading the item `Value` as silver.
- Known limits. The panel's bag lives in the panel and not on the player, so nothing else
  sees what was bought, and nothing else reads a bag yet. Nothing in a scene owns a flag set,
  so mission code cannot set the flag yet. The cheat panel and the shop panel shared one cursor
  block flag when this was written, so closing one while the other was open gave the cursor
  back to the camera. The HUD entry below replaced the flag with a counted block. A
  shop does not restock, and does not stock what it buys. Nothing is saved. The item table has
  no raw material type, so what a shop buys is a list in its data. The hidden shop's mission
  and the shopkeeper names are not decided, and the shops plan was not available, so no real
  shop exists.
- Packages: still none. Inventorizz and Questly are not in `Packages/manifest.json`. They
  attach to `IManagedItemHolder` and `IManagedFlagSource`.
- The ECS stack is unchanged.
- Edits to existing files: `ManagedWallet.cs` gained `: IManagedCoinPurse` on its class line
  and nothing else. `data.md` gained a note under `ItemDatabase.cs`.

### HUD, quick slots and the counted cursor block (`Hud/`, `Items/`, `Camera/ManagedCursorBlock.cs`)

- Asked for: the HUD, from Su's drawn draft: missions at the top left, crown heat and coins at the
  top right, health and SP bars with quick inventory slots at the bottom middle, a button at the
  bottom right that opens a wheel for the weapon or spell to use, and a compass at the top middle.
  Decided with Su: three crowns of three sections that fill as heat rises and are hidden until the
  player is wanted, the whole frame built with placeholders, one transparent PNG per piece, the SP
  bar showing the selected vessel's charge, quick slots holding items the player assigns by drag
  and drop, a wheel of four sections each holding one usable thing, and TextMeshPro for text.
- Now: a `Hud/` folder with the pure models (math, state, layout, wheel) and the widgets (the
  builder, text, sprites, click and slot views, the feed, `ManagedHud`, the dev keys). `Items/`
  gained the quick slots, the drag carrier, the player bag component and the sample items.
  `Camera/` gained `ManagedCursorBlock`, which replaces the settable capture flag, and the cheat
  panel and the shop panel hold blocks of their own. The caster gained two read-only members for
  the SP bar.
- Checked with the console harness against the `UnityEngine` and uGUI stubs: 663 checks pass.
  The tests cover the math at every edge (the wheel's radii and boundary angles, the compass wrap
  and fade, the crown thirds), the state setters, the layout rules, the wheel and quick slot
  models, the drag carrier, the counted block across a reset, the player bag and its catalog
  fallbacks, the feed against built scenes, the slot view's drag, drop and click rules, and the
  HUD built and driven frame by frame: the cursor and key rules, the wheel's open, pick, close and
  re-lock, the hover block, each refresh, and the text and sprite fallbacks. 369 deliberate
  breakages for this drop were added to the earlier ones, 471 in all, and each one fails at
  least one check. The `Hud`, `Items` and `Camera` files compile under C# 9 with the real sources.
  The harness is not in the repo.
- Not verified in the Editor: how the HUD looks, the real TextMeshPro and the importer, the Input
  System module on a real event system, drag and drop through a real raycaster, the scaler at other
  aspect ratios, the placeholder wheel highlight and the compass clipping, and the cursor handling
  next to the cheat and shop panels.
- Placeholders: every number in `ManagedHudLayout.cs`, the colours, the quick slot count of five,
  the wheel's four sections, the keys Q and 4 to 8, the sample items and the dev keys, and the
  reading of an item's `Value` as silver.
- Known limits. Nothing feeds the missions panel, the heat or the wheel, so they show what the
  caller or the dev keys set. Using a quick slot raises an event and consumes nothing. There is no
  bag or NPC inventory window yet, so the live drags are slot to slot and slot off the row, and
  `ManagedItemDrag` is ready for the windows. Closing the wheel does not lock the cursor when
  another panel holds a block, and if that panel opened while the wheel was open the cursor stays
  free until a click. Without the TextMeshPro essentials the HUD uses the built-in text. The
  camera rig's debug overlay draws over the missions panel. Nothing is saved, neither the bag nor
  the quick slots. The wheel's meaning is not settled beyond one usable thing per section.
- Packages: TextMeshPro, which the Unity project already lists, and the Input System.
- The ECS stack is unchanged.
- Edits to existing files: `ManagedCameraRig.cs` (`CursorCaptureBlocked` reads the block, and the
  reset clears it), `ManagedCheatManager.cs` and `ManagedShopDevPanel.cs` (each holds a block),
  `ManagedSpellCaster.cs` (`HasSelectedVessel` and `SelectedVesselFraction`),
  `ManagedShopSamples.cs` (the items moved to `ManagedItemSamples.cs`), `managed.md` and `data.md`.

### Build mode and the cursor block, the HUD bottom group, the Managed compile check (`Camera/`, `Building/`, `Hud/`, `building-test/`)

- Reported: the project did not compile. `ManagedBuildMode.cs` used `CursorHeldByUi`,
  `CaptureCursor()` and `ReleaseCursor()` on `ManagedCameraRig`, which no longer had them (four
  CS1061 errors). The project has no assembly definitions, so that one file stopped every Managed
  system in Unity.
- Cause: drops are whole file replacements. Commit `6195f58` (the Building drop) added the three
  members to the rig, and the Building paragraph and the parity row to this file. Commit `381c9b0`
  (the HUD and cursor block drop) replaced `ManagedCameraRig.cs` and `managed.md` from copies that
  predate the Building drop and removed all of it. The replacement of the rig also removed the
  crosshair hide. Nothing compiled the Managed code in CI, so the break stayed until Unity opened.
- Now: build mode holds a `ManagedCursorBlock` like the cheat panel, the shop panel and the HUD, and
  frees and restores the cursor itself. The camera rig has no build mode hook, and it hides the
  crosshair while any block is held. `ManagedBuildMode` lost its `_cameraRig` field, so a scene that
  serialized it drops the value without an error. The Building paragraph and the parity row are back
  in this file. The HUD hides its bottom group while build mode is open, see `ManagedHud.cs`.
  `.github/workflows/managed-csharp-ci.yml` compiles `Managed/` on every push that touches it, see
  CI and Workflows.
- Checked with the harness in `building-test/` (C# 9, .NET 8, the Unity stand-ins): 601 checks pass.
  The harness reproduces the four original errors on the old files. The checks cover the cursor
  block holders and their release order with the cheat panel and the HUD, the rig hook, the feed
  reading build mode (open, closed, destroyed, absent, paced search), the bottom group's members,
  hide and show, the key gate, a drag from a quick slot and a drag from elsewhere. 23 deliberate
  breakages for this change were applied one at a time and 22 failed a check. The survivor removes
  the early return in `ManagedHud.ApplyBottomGroup` that skips an unchanged value. It changes
  nothing visible, because switching a group to the state it already has does nothing. The earlier
  breakages of the Building drop were not repeated.
- Not verified in the Editor: that the project compiles in Unity (the harness proves it against the
  stand-ins, not the real packages), the tray and the HUD together on screen, and the cursor
  handoff when build mode and another panel are open at once.
- Known limits. Closing build mode locks the cursor again when it was locked at the opening, even if
  another panel took the cursor meanwhile, for example a cheat panel opened while the tray was open.
  The block of that panel keeps the rig from recapturing, but the cursor stays locked until a click
  frees it. The HUD wheel avoids this by checking for other holders, and build mode does not yet.
- The ECS stack is unchanged.
- Edits to existing files: `ManagedCameraRig.cs` (the crosshair check and a comment),
  `ManagedBuildMode.cs` (the block), `ManagedHudState.cs`, `ManagedHudFeed.cs` and `ManagedHud.cs`
  (the bottom group), `managed.md` and `building.md`.

### Heat and missions feeders (`Hud/`, `Missions/`, `Crown/`)

- Asked for: the systems that drive the HUD's `SetHeat` and `SetMissions`, which nothing called
  yet. Decided with Su: one crown for each Crown Heat tier with the thirds filling by the meter
  inside a tier, a bridge file in `Crown/` and not a `Hud/` file that names Crown, and a small
  mission tracker of our own with a quest package attaching behind an interface later.
- Now: `Hud/ManagedHudHeatSource.cs` (the heat interface and its registry), `Crown/ManagedCrownHeatMap.cs`
  (tier and meter to nine sections), `Crown/ManagedCrownHudHeat.cs` (the component), and a
  `Missions/` folder: `ManagedMissionSource.cs`, `ManagedMissionTracker.cs` and `ManagedMissionBoard.cs`.
  The feed copies both sources when they are registered.
- The design differs from the first idea in one way. The bridge does not call `SetHeat`. The HUD
  asks the registered source through the interface, because the HUD already knows where the player
  is and this keeps `Crown/` from looking for the player or the HUD, which would have needed the
  character and the HUD in the NPC harness.
- Found on the way: the missions panel redrew only through `ManagedHud.SetMissions`, which set a
  flag. A feed that writes to the state directly would never have refreshed it. The panel now
  redraws on `ManagedHudState.MissionsVersion`, which goes up only on a real change.
- Existing Crown file edited, with Su's approval: `ManagedCrownRegion.cs` gained `WantedAt`,
  `HuntedAt` and `OutlawAt`, read-only. They were private and the thirds need them. If another
  chat ships a whole `ManagedCrownRegion.cs` that lacks them, `ManagedCrownHudHeat.cs` stops
  compiling, and the `managed-npc-fallback` job says so on the push.
- Checked with the building harness: 803 checks pass, 202 of them new. They cover every boundary of
  the heat map (each third, each tier, a retuned region, a band with no width, a tier above the
  highest, a sweep of the whole range for order and range), both registries, the tracker's
  rules, the board, and the feed and the HUD reading the sources (heat and missions follow, clear
  when the source goes, stay when it never came, redraw on a change from the feed). 49 deliberate
  breaks were applied one at a time and each fails a check. Two were missed at first and needed a
  sharper check, and a third attempt changed nothing and was dropped. The NPC harness compiles the new Crown
  component and passes its 836 checks, and a scratch copy of it with a hand written check ran the
  component against real regions: default and retuned thresholds, outside every region, a second
  region, enabling and disabling. That check is not in the repo.
- Not verified in the Editor: that the crowns and the panel look right when fed, the component
  on a real scene, and that Unity serializes the Inspector mission list as intended.
- Known limits. Only one source of each kind is read, the last to register. The heat shown is the
  region the player stands in, so leaving a town hides the crowns while the heat stays there. Heat
  is not saved. The missions have no saving, no rewards and no giver, the tracker only holds what a
  caller puts in it. The dev keys cannot move the heat while a heat source is registered.
- The ECS stack is unchanged.
- Edits to existing files: `ManagedCrownRegion.cs` (three getters), `ManagedHudState.cs`,
  `ManagedHudFeed.cs`, `ManagedHud.cs`, `BuildingTest.csproj`, `Tests.cs` (the new checks and the
  registries in `Reset`), `ManagedNpcTest.csproj` (one include line), `managed-csharp-ci.yml`,
  `managed.md` and `crown-heat.md`.

### Unity packages as git URLs (`Packages/manifest.json`, `Packages/packages-lock.json`)

- Asked for: the studio packages of `unity-chem-sim` to be usable in GTG. Su asked to vendor
  them, then chose git URLs in the manifest over copying them into the repo. The native
  libraries of all of them come to about 250 MB, 175 MB of it for iOS and WebGL, and the repo
  is worked on from GitHub only.
- Now: the manifest lists `com.midmanstudio.mdix` (from `Mid-D-Man/DixScript-Rust`) and
  `alembic`, `dragndrop`, `gensettings`, `hudman`, `inventorizz`, `nuclear` and `questly` (from
  `MidManStudio/unity-chem-sim`). Every URL is pinned with `#<commit>`: `unity-chem-sim`
  `0c5ce9e`, `DixScript-Rust` `1b692ec`. The old entry for Alembic was a `file:../../unity-chem-sim/...`
  path that only resolved on the old laptop layout, so on any other machine Alembic did not
  resolve. The lock file loses that stale entry and Unity writes the rest when it next opens the
  project.
- Found on the way: the asmdef of Alembic references `MidManStudio.Mdix.Runtime`, but its
  `package.json` does not list mdix and the manifest has had no mdix entry since commit `6f6ef97`.
  The entry is back, so that reference resolves.
- Risk, not verified in Unity: Questly, Inventorizz, HudMan and GenSettings declare mdix and
  dragndrop in their `package.json` as git URLs, and the Unity manual says Package Manager does
  not support git dependencies between packages. The manifest lists every package itself, which
  is the documented way round it, but how Unity treats the URL strings in those four files is
  not tested. If the first open of the project in Unity reports an unresolved dependency, the
  fix is to change those four `dependencies` values to plain versions (`"1.0.0"`) in
  `unity-chem-sim`, or to take the new manifest lines out again. A package that fails to
  resolve blocks every system.
- Nothing in `Managed/` uses these packages yet. The Managed CI does not touch the manifest, so
  its result does not say anything about package resolution.
- To move a package on, change the commit after `#` in the manifest. The Package Manager needs
  git installed on the machine that opens the project.
- Earlier entries above (Vessels, Currency and cheats, Shop) say the packages are not in the
  manifest. That was true when they were written and is no longer.
- Edits to existing files: `Packages/manifest.json`, `Packages/packages-lock.json`, `managed.md`
  and `GTG_REPO_CONVENTIONS.md` (new section 6).

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
- Crown Heat (`Managed/Crown/`, the brain, the debug view and window, and the ABI version 5 files in Rust, C and C#).
  Before, guards ignored a visible player unless they had been attacked themselves, whatever the player had done
  in front of them, and nothing tracked crimes. Now a region counts witnessed assaults and murders into a meter,
  turns it into a tier, fades it when unseen, and a retaliatory guard in a region at tier 1 or more fights the
  player on sight. Forty deliberate breaks (a wrong threshold, a missing witness check, no repeat window, the kill
  exception, victim roles, the guard-only send, fade timing and rate, the clock restart on crime or sighting, region
  lookup, clamping, and in Rust the role check, the tier check and the validation limit) were each caught by a
  failing check. One of my own tests had wrong arithmetic first (it expected tier 1 after 23 s from 11 heat, but 3 s of fade takes
  1.5 and leaves 9.5, which is tier 0) and was corrected, and the harness needed a `GetComponentInParent` stub. The harness checks the logic
  with stubs. It does not run real guards with real sight, so witness detection has not seen a real raycast.
- Crown Heat not built: rules for tiers 2 and 3, crimes other than hurting and killing (theft, trespass, drug dealing
  once that exists), bribes and pardons as game systems, saving the meter, a UI for the meter, and a way for a guard
  to tell the player from another threat. Local Favor is separate and not built. Every number is a placeholder.
- Not verified in the Editor, not compiled by Unity. The files compile with .NET 8 (C# 9) against hand
  written stubs of the `UnityEngine`, `UnityEngine.AI` and `UnityEditor` types, and 1130 checks pass there on
  both the managed fallback and the real Rust library (836 before the ranged and shopkeeper work). CI job `managed-npc` repeats this on every push. The
  stub raycast is a callback, the NavMesh is a callback and the agent moves in a straight line, so the checks
  prove the logic around them (cone, budget, lingering, memory, patrol, chase, cooldown, retreat, damage
  falloff, debugger filters and drawing) and not real colliders, layers, NavMesh pathing or `Physics.Raycast`
  behavior. Twenty-three deliberate breaks of the earlier logic and the version 3 and 4 additions (provocation, orders, refusal, follow, hold, betrayal, missions, the resolver) were each caught by a failing check.
- Combat engagement (`ManagedCombatEngagement.cs`, and the Attack code of `ManagedNpcActor.cs`, `ManagedNpcBrain.cs`,
  `ManagedNpcThreatSource.cs`, `ManagedNpcDebugView.cs`). Before, every attacker chased the same last seen point and
  stopped at 80 percent of its range, so a group stacked into one spot on the player's line, and a hit landed
  whenever the cooldown was ready, even on a target that had moved away since the last sighting. Now attackers hold
  places on a ring, only a limited number strike at once, the rest circle and wait, and a swing at a target that is
  out of reach misses. Targets are chosen nearest first, with stickiness and revenge. Thirty-one deliberate breaks of
  these rules (the token limit, rotation, order of the queue, even spacing, the turn of the ring, circling, crowd
  widening, blocked handling and its direction by id, the freshest sighting, the live hit check and its leeway,
  holders only strike, the settling fallback, facing and rotation, avoidance priority, stuck detection, leaving the
  engagement, and the three target rules) were each caught by a failing check. Four of them were not caught at
  first, and one showed that a search over turns of the ring did nothing, so it was removed. Existing checks
  changed on purpose: the chase destination is now the place on the ring (z about 8.74 for a target at 10), the
  stopping distance is 0.25, and a redundant destination is no longer re-sent.
- Combat not built: companions as targets or fighters (needs factions), group spacing while
  following, Rust-side tactics (flanking, morale, calling for help), which need the number of allies in
  the observation (ranged attackers and kiting were added with ABI version 6, see below), and an event for a swing that misses. More than about
  eight holders on the melee ring crowd it, so the default of three is the number to design around. The
  numbers (3 strikers, 5 s rotation, 2.5 m waiting distance, 1.5 m/s circling, 0.75 m leeway, 30 and 50 percent
  preferences) are placeholders. The agent stub does not collide, so the harness proves where attackers are sent and
  when they strike, and not how a real NavMesh crowd squeezes past each other.
- The `UnityEditor` stubs were written from knowledge of the API and prove that the editor files are
  consistent with them, not with the real `UnityEditor`. An editor file that does not compile in Unity blocks
  Play mode for the whole project, so after importing, look at the Console first. The calls used are
  `Handles.Label`, `DrawLine`, `DrawDottedLine`, `DrawWireDisc`, `DrawWireArc`, `DrawSolidDisc`,
  `SceneView.duringSceneGui`, `SceneView.RepaintAll`, `SceneView.FrameSelected`, `Selection.activeGameObject`,
  `EditorWindow.GetWindow<T>(string)`, `OnInspectorUpdate` and the `EditorGUILayout` popup, toggle, help box and
  scroll view calls.
- Sight is a cone plus one ray per visible point. There is no suspicion meter, no hearing and no faction
  logic. An NPC either sees a threat or does not.
- The sight ray starts at the eye offset, 1.6 m above the NPC pivot by default. An NPC whose own collider
  contains that point, or whose layer is in the sight mask, can block itself on some setups. The scene setup
  names the two ways around it.
- Damage sources: the fireball, NPC melee and NPC arrows, bolts and spells call `TakeDamage`. Ice and the chemical hazards do not.
  The fireball explosion ignores walls and its overlap buffer holds 32 colliders.
- The actor needs a baked NavMesh. Nothing in the repo bakes one, so a scene needs a `NavMeshSurface`.
  Trade is not carried out: a merchant stands still (or fights back, if retaliatory and attacked), and there is no shop or dialogue system yet.
- The NPC does not animate, die visibly or drop anything. A dead NPC stops and optionally is destroyed.
- Edit to existing Managed files for damage: `ManagedSpellTypes.cs` gained `Source`, `ManagedSpellDefinition.cs`
  gained the damage fields and `For`, and `ManagedSpellCaster.cs` sets `Source` in `Detonate`. No other
  caster behavior changed. Not verified in the Editor, the caster is stubbed in the harness.
- The native library is still not in `Assets/`. Until a build is imported as a plugin, every batch takes the
  managed fallback after one warning. Rust itself is not affected by the Burst `Illegal instruction` abort,
  because that comes from Burst code generation. Whether the Rust library runs on the 2010 MacBook is
  untested: the CI macOS leg is arm64, and no Intel macOS build exists yet.

### Ranged, hybrid and shopkeeper fighters (`ManagedNpcShooter.cs`, `ManagedNpcShots.cs`, `ManagedNpcShotDriver.cs`, `ManagedNpcBallistics.cs`, `ManagedCombatEngagement.cs`, `ManagedNpcActor.cs`, `ManagedNpcBrain.cs`, `ManagedNpcThreatSource.cs`, `ManagedSpellCaster.cs`)

- What changed. Combatants now come in three attack styles (melee, ranged, hybrid), as the owner described: archers and crossbow guards shoot, mages cast, hybrids switch by distance and by what they have left. Rust decides (ABI version 6, see `npc/observation.md`), Unity owns the weapon, the ammo and the flight. Owner answers: Unity plus ABI v6 with distance and style in the observation, arrows and bolts as projectiles with mages reusing the player's spell flight, hybrids switching on a distance band and dropping to melee when out of ammo or mana, shooters with their own limit and ring. Shopkeepers: some fight back when attacked first (owner note). Boss fights are a separate system and use the same style rules until then.
- Shopkeeper retaliation was not already there. Merchants never fought, and docs and tests said so on purpose. It is opt-in through the existing Retaliatory disposition: a retaliatory merchant that was attacked fights in its own style, a hostile one does not, and unprovoked ones are unchanged. The Rust rule and its managed copy are checked on every combination, and by a director check in the harness (every disposition, a hit with no source does not provoke, a stationary shopkeeper strikes in reach and never leaves its stall, provocation fades after 20 s, a crossbow shopkeeper shoots). Hurting a shopkeeper still earns Crown Heat, see `crown-heat.md`.
- Real bug found by the new checks: `StrikingCount` and the token holder count included shooters, so a crowd of archers would have shown as strikers and the melee numbers would have been wrong. Both now count melee members only, and shooters have their own counts.
- Real bug found by the new checks: an archer stopped about 14 m from the target instead of on its 12 m ring, because it reached weapon range with the melee settling shortcut (strike after 1.5 s in range whatever the place). A shooter's settling time now includes the walk to its place.
- Behavior change that reaches beyond ranged fighters: an NPC with Can Move off no longer walks toward its place, for melee too. A stationary guard or shopkeeper used to chase a target it could not reach and now stays at its post and strikes when the target is in reach.
- Test hardening. The first run of 20 deliberate breaks of the new code missed two, and both gaps were closed: the batch stub now records every sphere (it only kept the last, so a size or color left behind by a swap-remove went unseen), and a test body that counts every hit shows that the dead-body guard is applied by the shot and not only by `ManagedHealth`. Two more checks were added for the sweep radius and the damage source of a shot after a swap-remove. The 23 breaks now run so: shooter settling, each side array after a swap-remove (damage, color, size, radius, source), stationary melee, a blocked shot spending ammo, the stance never sent, shooters counted as strikers, the high arc, the distance never sent or including height, regeneration past the capacity, the cooldown ignored, the shooter's own body blocking its line, the sweep radius, shots that never expire, a dead body hurt again, the spell's source lost, a shared melee limit, kiting without firing, and the director's action bound. Each is caught by a failing check. In Rust, 12 breaks of the style and shopkeeper rules are caught by the unit tests (see `gtg-npc-behavior.md`).
- The harness grew to 1130 checks (from 836) and passes on both decision paths. The two alignment tests in `npc-ffi` were rewritten (see `gtg-npc-ffi.md`). Not compiled by Unity and not run in a scene: the physics is a stub (a box sweep and a callback raycast, a straight-line agent), so the checks prove the logic around it and not real colliders, layers, projectile tunnelling at real frame times, NavMesh paths or animation.
- Interpretations and placeholders to confirm:
  - Ranged fighters with nothing to shoot retreat (or idle when they cannot move) and never melee. Hybrids with nothing to shoot fight by hand.
  - The distances are placeholders: kite below 6 m, hybrids melee at 4 m or less and shoot at 8 m or more, with the in-between keeping the current stance, and an unknown distance counts as that buffer. The stand-off distance must stay clear of the kite line (7 m minimum is enforced).
  - Distance is on the ground plane. Arrows and bolts and mana share one ammo pool with optional regeneration.
  - Friendly fire is on for arrows and spell explosions, because nothing separates factions yet. A guard can be hit by another guard's arrow.
  - The line of fire is a straight ray, so no shot lobs over a wall. A steered spell (Seeker) flies straight for an NPC. Arrows are drawn as spheres.
  - A hybrid loses its place in the engagement when it switches between Attack and Shoot and queues again.
  - Provoked is global, not per target. A ranged NPC that backs away in a real scene may lose sight when its agent rotates away, and then it falls back to Patrol. The stub agent never rotates, so this was not seen. The actor turns its own rotation off while kiting to reduce it.
- Not built: NPC shooters aimed at moving targets with lead (the aim is at the last seen position), steering for NPC spells, arrow models, sounds and animation (the `Fired` event is the hook), a shop UI that refuses service while the keeper is provoked, pickup of spent arrows, and cover.
