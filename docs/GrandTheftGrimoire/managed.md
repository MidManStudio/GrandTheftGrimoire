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
3. To make the NPC move and fight, add `ManagedNpcActor`. It brings a `NavMeshAgent` with it. Bake a NavMesh for the scene (a `NavMeshSurface` from the AI Navigation package, installed at 1.1.5) and place the NPC on it. Give the NPC a collider, otherwise the player's fireball passes through it. For a patrol route, drag transforms into the Waypoints list, or leave it empty to wander near the start. A merchant needs no actor.
4. Fireballs hurt anything with a `ManagedHealth` inside the explosion, with no scene setup. The values are in `ManagedSpellDefinition`.
5. The first brain creates a `GTG NPC Director` object. Add a `ManagedNpcDirector` to the scene yourself to change the tick rate or batch limits, or to turn the overlay on.
6. Select an NPC in the Scene view to see its view cone and eye. The cone is yellow while idle and red while it sees a threat.
7. Check the Console at Play start. A missing native library logs one warning and the managed decision takes over. The director overlay says `native` or `managed fallback`. An NPC that is not on the NavMesh logs one warning and stands still.
8. For a companion, choose the role Companion, set its Level, its Trustworthiness, Affinity and PaySatisfaction, and set its Leader to the player object. Give it orders from code with `brain.SetOrder(NpcOrder.Raid, 12)` and listen to `brain.OrderRefused` and `brain.Betrayed`. For a mission, resolve it with `brain.TryResolveMission` when it happens off screen. No UI for orders exists yet. A companion with no order follows its leader.
9. For a non-hostile NPC, set its Disposition. Retaliatory ignores the player until it has been hit, and Peaceful runs and never fights.

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
| Explosion at range end | backlog | done, now where the SP runs out |
| SP drive, payload load, collapse at zero SP | design notes in `magic.md` | done, `ManagedSpellFlight` |
| Steering: homing and curved flight, third spell | design notes in `magic.md` | done, `ManagedSpellSeeker` and `Steer` |
| Vessels: single-use bottles, rechargeable orbs | design notes in `magic.md` | done, `ManagedVesselBelt` |
| Currency: gold, silver and SP crystals | design notes in `magic.md` | done, `ManagedWallet` and `ManagedVesselCharging` |
| Cheat code manager | backlog | done, Editor and development builds only, `ManagedCheatManager` |
| Shops, item bag, flags | backlog | engine and test panel done, no shop window yet, `ManagedShop` |
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
| `Economy`, `Flags`, `Health`, `Rendering`, `CharacterController` | nothing |
| `Items` | `Data`, in one adapter file |
| `NPC` | `Health` |
| `Magic` | `CharacterController`, `Economy`, `Health`, `Rendering` |
| `Chemistry` | `Magic`, `Rendering` |
| `Camera` | `CharacterController`, `Economy`, `Magic` |
| `Shop` | `Economy`, `Items`, `Flags`, and `Camera` in the test panel only |
| `Cheats` | `Camera`, `CharacterController`, `Chemistry`, `Economy`, `Health`, `Magic`, `NPC` |

The shop reaches all four of its neighbours through small interfaces:
`IManagedItemCatalog`, `IManagedItemHolder`, `IManagedFlagSource` and
`IManagedCoinPurse`. Replacing what stands behind one of them changes one file and
leaves the shop alone. The patterns package of `MidManStudio_Unity` (command, state,
event queue, dependency injection) is not used yet. These interfaces and the plain C#
events are the places it would attach to.

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
  the caster aims at, so it must stay at the center.
- The spell hint in the overlay lists keys 1, 2 and 3 and the state of the selected
  vessel, such as `orb 62 percent` or `bottles 4 of 5`.
- A purse line under the spell line shows gold, silver and crystals by size, and names
  the crystal key.
- `CursorCaptureBlocked` is a static flag. While it is true a click does not capture
  the cursor, so a click on a panel button does not also lock the mouse. The cheat
  panel sets it while it is open. The rig knows nothing about the cheats. The flag is
  cleared at `SubsystemRegistration`, because static state survives a play session when
  domain reload is off.

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
- Opening the panel frees the cursor and sets `ManagedCameraRig.CursorCaptureBlocked`.
  Closing it gives the cursor back only when the panel was the one that took it. The
  dependency runs one way: the cheats know the camera rig, and the rig knows nothing about
  the cheats.
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

### `IManagedFlagSource.cs`

**What it does:** One method, `IsSet(flag)`.

**Decisions:**
- A shop asks this to learn whether it is open and never learns who set the flag.
- `ManagedWorldFlags` is the plain answer today. The Questly package of `unity-chem-sim` has
  `QuestRuntime.GetQuestState(questId)` and a `QuestState.COMPLETED` value, so an adapter
  can later answer `IsSet(id)` with "that quest is complete". Questly is not in this
  project's `Packages/manifest.json`, and nothing here depends on it.

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

**What it does:** Three sample items (the rows of `Assets/inventory_items.mdix` as they are
today) and two sample shops: a general store that is always open, and a hidden shop that
needs the flag `sample_hidden_shop_open`.

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
- Opening it frees the cursor and sets `ManagedCameraRig.CursorCaptureBlocked`, the same way
  the cheat panel does. It sits on the left so it does not cover the cheat panel.
- It is the only file of `Shop/` that reads `Camera`. The drawn shop window replaces it.

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

### `ManagedNpcSight.cs`

**What it does:** The two tests behind sight. `InView` checks range and the view cone with arithmetic only. `HasLineOfSight` casts one `Physics.Raycast` from the eye to the point.

**Decisions:**
- The cone test runs first, so an NPC that cannot face the player costs no ray. Out of range and behind both skip the physics call.
- The ray ignores triggers. It succeeds when the first thing it meets belongs to the source's transform hierarchy, or when it meets nothing.
- A ray that starts inside a collider does not hit it, so the NPC's own capsule does not block its sight. The mask should still leave the NPC's layer out where possible.
- A ray stops a small margin past the target point. A target point inside a collider is found by the surface hit well before that.

### `ManagedNpcBrain.cs`

**What it does:** One NPC. It holds role, backend, can-move, learning mode, disposition, level, the standing order and the sight settings. It builds the 64 byte observation for the Rust decision and stores the action that comes back. It raises `ActionChanged` when the action changes and `OrderRefused` when a companion declines an order. It does not move, fight or trade.

**Decisions:**
- Movement, combat, animation and trading stay out of this file on purpose. Rust chooses, other components act, as in the ECS design.
- A threat lingers for `_loseSightDelay` seconds after the last ray that saw it, so a guard does not flicker between attack and patrol when the player steps behind a pillar for one frame.
- Each ray costs one from a per-tick budget. An NPC that runs out keeps its previous result, see the director.
- A dead brain is left out of the batch and reports Idle.
- The id is a counter that is unique among live NPCs in one play session. It is not stable between sessions, so it cannot key saved memory.
- `LearningMode` is stored and exposed. Nothing reads it yet.
- A threat source on the NPC itself, or on one of its parents or children, is ignored.
- `Threat` and `LastKnownThreatPosition` record what was seen and where. The memory ends with the lingering time, so an NPC chases the last seen position and not the live one.
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
### `ManagedNpcActor.cs`

**What it does:** Carries out the action Rust chose, driving a `NavMeshAgent`. Patrol walks the waypoints in order, or wanders near where the NPC began. Attack chases the last seen position and hits the target in melee. Retreat runs directly away. Follow walks to the brain's `Leader` and stops 3 m from it. Idle, Trade, Hold and RefuseOrder stand still. It raises `Attacked` for every hit.

**Decisions:**
- The actor never decides. It listens to `ManagedNpcBrain.ActionChanged`, so the Rust decision stays the single source of what an NPC does.
- Movement uses a `NavMeshAgent`, because the AI Navigation package is installed. The scene needs a baked NavMesh. An NPC that is not on it logs one warning, sends nothing to the agent, and is configured as soon as it is on the NavMesh.
- A chase goes to `LastKnownThreatPosition`, not to the live position of the target, so stepping out of sight works as hiding. The destination is refreshed every 0.25 s.
- Melee has a range (1.8 m), a damage (10) and a cooldown (1.2 s), all placeholders in the Inspector. The agent stops at 80 percent of the range and the NPC turns to face the target, so its view cone keeps pointing at it.
- A hit goes through `ManagedNpcThreatSource.Damageable`. It is skipped when the target is already dead, which matters when two attackers are ready in the same frame.
- Follow stops inside `_followDistance` (3 m) and starts again only when the leader is more than a meter farther, so a follower does not shuffle at the edge. A follower with no leader stands still.
- A patrol stop lasts `_pauseSeconds`. A wander point must be on the NavMesh, otherwise the NPC stays where it is until the next try.
- A chase target that is off the NavMesh gives no destination, so the NPC stands. A dead NPC stops. It is destroyed after `_removeAfterDeathSeconds`, and zero leaves the body.
- Every NPC with an actor runs a small `Update`. The work per frame is a few comparisons, and the agent does the movement.

### `ManagedNpcDebugView.cs`

**What it does:** Editor-only drawing and text for the NPC debugger, in `Managed/NPC/Editor/` so it compiles into `Assembly-CSharp-Editor` and never reaches a build. It draws one NPC in the Scene view: a label, the path to the destination, a marker on the destination, the patrol route with numbered stops, the wander, attack or retreat range, the sight cone, and a cross where the threat was last seen. Colors follow the action: grey Idle, cyan Trade, green Patrol, red Attack, amber Retreat, blue Follow, purple Hold, pink RefuseOrder, teal Mission, dark red Betray. A companion's line shows its level, order, loyalty and whether it could betray, a non-hostile disposition is named, and provoked is shown. A follower draws a dotted line to its leader and a ring of the follow distance around the leader.

**Decisions:**
- It reads public state only and changes nothing in the game. The brain, the actor and the director gained read-only accessors for it (`Current`, `GetBrain`, `ViewRange`, `FieldOfViewDegrees`, `Home`, `AttackRange`, `WanderRadius`, `RetreatDistance`, waypoints, `HasPath`, `Destination`, `GetPathCorners`). No game code reads them.
- The visualization lives in editor code and uses `Handles`, so the runtime components carry no `UnityEditor` references and no `#if UNITY_EDITOR`.
- The path comes from `NavMeshAgent.path`, which allocates inside Unity on every read. That is acceptable in the Editor and is the reason the call exists only for the debugger.
- At most 300 NPCs are drawn per repaint, so a crowd stays usable. The list shows the same cap.
- Switches: scene drawing (off, selected only, all), an action filter, only NPCs that see a threat, labels, paths, ranges and sight cones. Sight cones start off, because they are noisy with many NPCs.

### `ManagedNpcDebuggerWindow.cs`

**What it does:** The window behind it, opened from the menu GTG, NPC Debugger. It shows the director line (NPC count, batch size, rays this tick, native library or managed fallback), the switches above, and a live list of NPCs. A click on a row selects the NPC and frames it in the Scene view. It subscribes to `SceneView.duringSceneGui` while it is open and unsubscribes when it closes.

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
- A decision is applied only if its NPC id matches the row it was made for and the action is in range (0 to 9 since ABI version 4). The ECS system still stops at 4, which is correct for it because it has no companions.
- A scene director is reused even when it is disabled, because a disabled director is the scene's choice. A second enabled director disables itself with one warning.
- With no `ManagedNpcThreatSource` in the scene the director warns once, because that is the usual reason for NPCs that never see anything.
- `Brains` and the id counter are static and cleared at `SubsystemRegistration`. `Current` and `GetBrain(index)` are public for the NPC debugger.
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
  `Managed/Health`, `Managed/NPC` (including its `Editor/` folder) and the spell damage files (C# 9, as in Unity 2022.3)
  against hand written `UnityEngine`, `UnityEditor` and `NavMeshAgent` stand-ins in
  `rust/npc/managed-npc-test/` and runs 645 checks, once on the managed fallback and
  once on the real library. The spell caster itself is replaced by a stub that has only
  the `Impact` event. The spell definition is compiled too, so the flight, payload,
  modifier and vessel type files that it depends on are part of the project.

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
  so mission code cannot set the flag yet. The cheat panel and the shop panel share one cursor
  block flag, so closing one while the other is open gives the cursor back to the camera. A
  shop does not restock, and does not stock what it buys. Nothing is saved. The item table has
  no raw material type, so what a shop buys is a list in its data. The hidden shop's mission
  and the shopkeeper names are not decided, and the shops plan was not available, so no real
  shop exists.
- Packages: still none. Inventorizz and Questly are not in `Packages/manifest.json`. They
  attach to `IManagedItemHolder` and `IManagedFlagSource`.
- The ECS stack is unchanged.
- Edits to existing files: `ManagedWallet.cs` gained `: IManagedCoinPurse` on its class line
  and nothing else. `data.md` gained a note under `ItemDatabase.cs`.

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
  written stubs of the `UnityEngine`, `UnityEngine.AI` and `UnityEditor` types, and 645 checks pass there on
  both the managed fallback and the real Rust library. CI job `managed-npc` repeats this on every push. The
  stub raycast is a callback, the NavMesh is a callback and the agent moves in a straight line, so the checks
  prove the logic around them (cone, budget, lingering, memory, patrol, chase, cooldown, retreat, damage
  falloff, debugger filters and drawing) and not real colliders, layers, NavMesh pathing or `Physics.Raycast`
  behavior. Twenty-three deliberate breaks of the earlier logic and the version 3 and 4 additions (provocation, orders, refusal, follow, hold, betrayal, missions, the resolver) were each caught by a failing check.
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
- Damage sources: only the fireball and NPC melee call `TakeDamage`. Ice and the chemical hazards do not.
  The fireball explosion ignores walls and its overlap buffer holds 32 colliders.
- The actor needs a baked NavMesh. Nothing in the repo bakes one, so a scene needs a `NavMeshSurface`.
  Trade is not carried out: a merchant only stands still, and there is no shop or dialogue system yet.
- The NPC does not animate, die visibly or drop anything. A dead NPC stops and optionally is destroyed.
- Edit to existing Managed files for damage: `ManagedSpellTypes.cs` gained `Source`, `ManagedSpellDefinition.cs`
  gained the damage fields and `For`, and `ManagedSpellCaster.cs` sets `Source` in `Detonate`. No other
  caster behavior changed. Not verified in the Editor, the caster is stubbed in the harness.
- The native library is still not in `Assets/`. Until a build is imported as a plugin, every batch takes the
  managed fallback after one warning. Rust itself is not affected by the Burst `Illegal instruction` abort,
  because that comes from Burst code generation. Whether the Rust library runs on the 2010 MacBook is
  untested: the CI macOS leg is arm64, and no Intel macOS build exists yet.
