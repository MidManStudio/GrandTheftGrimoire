# Lab (`MidManStudio.Gtg.Managed.Lab` and one editor file per feature folder)

> Code location: `Assets/MidManStudio/Gtg/Managed/<Feature>/Editor/`. Every file here is editor only. Unity compiles any folder named `Editor` into `Assembly-CSharp-Editor`, so none of it reaches a build and none of it needs `#if UNITY_EDITOR`.

## Overview

Each feature folder has an `Editor` subfolder with one lab script. The script adds a menu under `GTG` that sets the feature up in the open scene and, in Play mode, pokes it, so the feature can be seen working without writing a scene by hand.

- **Start here:** `GTG, Lab, Build Test Arena`. It builds ground, player, camera rig, melee, spell caster, body facing, health, build mode, a hazard field and three dummies. Press Play after.
- **Ensure items are safe to repeat.** Every `Ensure` menu finds what the scene already has and adds only what is missing, so a second click changes nothing.
- **Play items need Play mode.** Menu items marked `(Play)` log a warning and do nothing outside Play mode.
- **No UI.** The labs draw no HUD or window. The player HUD stays a separate job.
- **No models.** Nothing in the labs references a character model. The player and the NPCs are capsules. A model is mounted only when the user selects one and runs `GTG, Facing, Use Selected As Player Model`.

## Menus

| Menu | What it does |
|---|---|
| GTG, Lab | Build Test Arena, Print What The Scene Has |
| GTG, Player | Ensure Test Ground, Ensure Player, Select Player, Print State (Play) |
| GTG, Camera | Ensure Rig And Main Camera, Toggle First Person (Play), Print Wiring |
| GTG, Facing | Use Selected As Player Model, Report Selected Model, Put Capsule Body Back |
| GTG, Health | Spawn Target Dummy At Scene View, Add Health To Selected, Damage 25, Heal 25, Kill, Reset, Toggle Invulnerable (all Play except the first two) |
| GTG, Melee | Ensure Melee On Player, Spawn 3 Dummies In Front, Equip Sword, Unequip Sword, Strike Now Without Input |
| GTG, Magic | Ensure Caster On Player, Spawn Dummy Row At 8 To 24 m, Refill Vessels, Print Caster State |
| GTG, Items | Add Player Bag To Selected, Stock Sample Items, Print Bag And Quick Slots |
| GTG, Economy | Give 1 Gold, Give 100 Silver, Give 3 Medium Crystals, Print Purse |
| GTG, Building | Ensure Build Mode, Toggle Build Mode |
| GTG, Chemistry | Ensure Hazard Field, Count And Clear Hazards |
| GTG, NPC | Spawn Merchant, Spawn Melee Enemy, Spawn Archer, Spawn Fighting Pair, Make Selected A Target, Ensure NavMesh Surface On Test Ground |
| GTG, Crown | Ensure Region And HUD Feeder, Add 10 Heat, Remove 10 Heat, Clear Heat |
| GTG, Favor | Ensure Region, Add Favor To Selected NPCs, Add 10 Crowd Favor, Remove 10 Crowd Favor |
| GTG, Glyphs | Ensure Preview At Scene View, New Seed On Selected Preview, Toggle Auto Charge On Selected Preview, Toggle Style On Selected Preview (Circle or Molecule), Toggle Atom Look On Selected Preview (Letters or Balls), Ensure Cast View On Player, Print Glyph Stats, Print Cast View State (Play) |

The existing `GTG, NPC Debugger` window is not part of the lab and is unchanged.

## Layers and masks

The labs create and use the layers the game code already assumes.

| Layer | Name |
|---|---|
| 6 | Ground |
| 7 | Player |
| 8 | Wall |
| 9 | Npc |

`GroundMask` is 64, `SightMask` is 448 (Ground, Player, Wall) and `HitMask` is 832 (Ground, Wall, Npc). `GtgLabKit.EnsureLayerNames` writes a layer name into the TagManager only when that slot is empty. It never renames a layer that already has a name.

## Dependencies

The `Lab` folder reads nothing at runtime. In the editor only `ManagedLabArena` reads every feature folder.

Each feature lab reads its own feature folder and, for the player it sets up, the CharacterController folder. A lab never makes a feature folder depend on another one, because the labs live in `Editor` folders that no runtime code references.

## CI

The Managed C# harness (`building-test`) excludes every `Managed/**/Editor/**` path. Editor code needs `UnityEditor`, which the harness has no stand-in for. The NPC harness (`rust/npc/managed-npc-test`) compiles only `NPC/Editor/ManagedNpcDebug*.cs`, because it has no stand-ins for `Undo` or `SerializedObject`, which the NPC lab uses.

## Modules

### `GtgLabKit.cs`

**What it does:** Shared helpers for every lab, in `Managed/Lab/Editor/`. It is `internal`, so only the editor assembly sees it. It holds the layer numbers and masks, `Log` and `Warn` (both prefixed `[GTG Lab]`), `RequirePlaying`, `EnsureLayerNames`, `FindRoot`, `NewObject` (registers undo), `Ensure<T>`, `Primitive` (a primitive with a flat color material and an optional collider removal), `ColorMaterial`, `Finish` (marks the scene dirty, selects and pings the object, logs the message) and `ViewPivot` (the point the Scene view looks at). It also holds the `SerializedObject` setters used to write private `[SerializeField]` fields by name: `SetFloat`, `SetInt` (also enums and layer masks), `SetBool`, `SetVector3`, `SetObject`, `SetVector3Array`, `ClearObjectArray`.

**Decisions:**
- Fields are written through `SerializedObject`, so the labs reach private serialized fields without reflection, the change is undoable and the Inspector shows it at once. A field name that does not exist logs a warning that names the field, so a rename in a runtime file shows up the first time the lab runs.
- Materials are assets in `Assets/MidManStudio/Gtg/Lab/Materials/Lab_<name>.mat`, created on first use. They use URP Lit and fall back to the Standard shader when URP is not found. They are assets, not runtime materials, so a saved scene keeps its colors.
- Every object a lab creates is registered with `Undo`, so one undo removes what a menu click made.

### `ManagedPlayerLab.cs`

**What it does:** Menu `GTG, Player`. `EnsureGround` adds `GTG Test Ground`, a 100 x 1 x 100 m slab on layer Ground with its top face at y 0, and `GTG Test Landmarks`, four 3 m pillars at 12 m on each axis (red +Z, blue -Z, green +X, yellow -X) on layer Wall, so direction is readable on screen. `EnsurePlayer` adds a root named `Player` at (0, 0.05, 0) on layer Player with a `UnityEngine.CharacterController` (height 2, radius 0.5, center (0, 1, 0), step offset 0.3), `ManagedCharacter`, and a `BodyView` capsule child with a yellow `Nose` cube on +Z that shows where the body faces. `Print State` writes yaw, pitch, grounded, position and the feature flags.

**Decisions:**
- The root is at the feet and the controller is centered at 1 m, which is the pivot rule the NPC and player defaults assume, so no height override is written anywhere.
- An existing `ManagedCharacter` in the scene is used as the player. The lab never makes a second one.
- The capsule is named `BodyView` because `ManagedBodyFacing` finds a child by that name.

### `ManagedCameraLab.cs`

**What it does:** Menu `GTG, Camera`. `EnsureRig` adds `GTG Camera Rig` with `ManagedCameraRig`, and a main camera with an `AudioListener` when the scene has no camera tagged MainCamera. `Toggle First Person` calls the rig in Play mode. `Print Wiring` writes whether the rig has a character, the cameras it drives and whether it is in first person.

**Decisions:**
- With no cameras assigned the rig drives the main camera, so the lab assigns nothing by hand.
- `UnityEngine.Camera` is written in full because the namespace `MidManStudio.Gtg.Managed.Camera` shadows the short name.

### `ManagedModelReport.cs`

**What it does:** A text report for a model, used by the Facing lab and by `Report Selected Model`. `Describe` measures the rendered bounds and prints lines marked `OK` or `FIX`: height (above 5 m means the file is in Blender units, 1.4 to 2.4 m is fine), feet offset from the player's feet (tolerance 0.1 m), scale, skinned mesh count and bone count, whether an Animator has an Avatar and whether the Avatar is valid and humanoid, and the triangle count. `DescribeImporter` reports the model importer's global scale, file scale, animation type and blend shape import for a model asset.

**Decisions:**
- It reads only. It never changes a model or its importer.
- Triangles are counted with `GetIndexCount` per triangle submesh, so a mesh with line or point submeshes does not inflate the count.

### `ManagedFacingLab.cs`

**What it does:** Menu `GTG, Facing`. `Use Selected As Player Model` instantiates the selected prefab, scene object or imported model under the player as `Model` (replacing an earlier `Model`), puts every layer on Player, hides the capsule `BodyView`, makes sure `ManagedBodyFacing` exists and points its `_body` at the model, clears the camera rig's `_bodyRenderers` so first person hides the new body, and logs the report. `Report Selected Model` logs the report without changing anything. `Put Capsule Body Back` undoes the mount. A gizmo draws the aim direction in cyan and, in Play mode, the body direction in green.

**Decisions:**
- This is the only place a model enters the player, and it runs only on a menu click. The arena and the other labs never mount one.
- The rig fills `_bodyRenderers` at Start only when the list is empty, which is why the lab clears it instead of filling it. A model that is already a child of the player is used as it is, not copied.
- The model origin must be at the soles and the model must face +Z. The report names what is off, and nothing is corrected silently.

### `ManagedHealthLab.cs`

**What it does:** Menu `GTG, Health`. `SpawnDummy` places a capsule on layer Npc with `ManagedHealth` (100) at the point the Scene view looks at. `Add Health To Selected` adds `ManagedHealth` to the selection. In Play mode the menu damages, heals, kills, resets and toggles invulnerable on the selection, and a gizmo draws `hp x/y` over every health in the scene.

**Decisions:**
- Damage and kill call `ManagedHealth.TakeDamage`, the same method a weapon calls. Kill deals the current health plus the maximum plus one.
- Labels draw in Play mode only.

### `ManagedMeleeLab.cs`

**What it does:** Menu `GTG, Melee`. `EnsureMelee` adds `ManagedMelee` to the player with `_hitMask` set to 832, plus `ManagedMeleeEquipBridge`. `Spawn 3 Dummies` places three dummies 1.8 m ahead, inside the 2.2 m reach. In Play mode `Equip` and `Unequip` set the weapon, and `Strike Now Without Input` runs one strike without a key press. A gizmo draws the 100 degree swing cone in orange, red while a swing is running.

**Decisions:**
- `_hitMask` is written as Ground, Wall and Npc (832). The component's own default is Everything.
- `Strike Now` calls `ManagedMeleeStrike.Strike`, the same call the swing makes after its hit delay, with the weapon the player has equipped.

### `ManagedMagicLab.cs`

**What it does:** Menu `GTG, Magic`. `EnsureCaster` adds `ManagedSpellCaster` to the player with `_hitMask` set to 832. `Spawn Dummy Row` places five dummies at 8, 12, 16, 20 and 24 m, so a shot that runs out of SP short of a dummy shows the range limit. `Refill Vessels` and `Print Caster State` work in Play mode.

**Decisions:**
- The caster adds the wallet at Play, so the Economy menus work after this menu has run.

### `ManagedItemsLab.cs`

**What it does:** Menu `GTG, Items`. `Add Player Bag To Selected` adds `ManagedPlayerItemBag` to the selected object. `Stock Sample Items` fills the bag through `ManagedItemSamples.StockPlayerBag`, and `Print Bag And Quick Slots` lists what the bag and the quick slots hold.

**Decisions:**
- `ManagedItemSamples` is the editor and development build stocker, so the lab adds no stock list of its own.
- The HUD adds a bag by itself at Play, so the first menu matters only in a scene without a HUD.

### `ManagedEconomyLab.cs`

**What it does:** Menu `GTG, Economy`. In Play mode it adds 1 gold, 100 silver or 3 medium crystals to the player's wallet and prints the purse.

**Decisions:**
- It calls the wallet's own methods and adds no state of its own.

### `ManagedBuildingLab.cs`

**What it does:** Menu `GTG, Building`. `EnsureBuildMode` adds `GTG Build Mode` with `ManagedBuildMode`, `_groundMask` set to 64, five crafting tables and five butchery stations as starting stock, and the starting blueprints. `Toggle Build Mode` opens and closes it in Play mode.

**Decisions:**
- The stock is written as fields on the component, so what the lab gives can be read and changed in the Inspector.

### `ManagedChemistryLab.cs`

**What it does:** Menu `GTG, Chemistry`. `EnsureField` adds a `ManagedChemicalHazardField`, which fire and ice impacts need to leave a burn or freeze patch. `Count And Clear Hazards` prints how many hazards are alive and clears them in Play mode.

**Decisions:**
- The field reaches Alembic when an impact lands, so the lab only adds the component and does not run a reaction.

### `ManagedNpcLab.cs`

**What it does:** Menu `GTG, NPC`. `SpawnNpc` builds an NPC with its pivot at its feet: a root on layer Npc with a capsule collider (height 2, radius 0.5, center (0, 1, 0)), a capsule view with a white nose, `ManagedHealth` and `ManagedNpcBrain` (role, backend, disposition, attack style, `_canMove` and `_sightMask` are written), `ManagedNpcActor` when it can move, and `ManagedNpcThreatSource` when it is a target. The menu spawns a merchant (state machine, stands still, peaceful), a melee enemy, an archer (adds `ManagedNpcShooter`) and a fighting pair 8 m apart facing each other. `Make Selected A Target` adds health and a threat source. `Ensure NavMesh Surface` adds the NavMesh Surface to the test ground with its layer mask set to Ground and Wall.

**Decisions:**
- The NavMesh Surface type is found by name, `Unity.AI.Navigation.NavMeshSurface`, so the lab compiles without the AI Navigation package. A missing package logs a warning.
- The bake is a button on that component and cannot be pressed from outside the package, so the lab sets everything and leaves the click to the user. Bake again after moving the ground or the walls.
- No height override is written, because the pivot at the feet matches the defaults.

### `ManagedCrownLab.cs`

**What it does:** Menu `GTG, Crown`. `EnsureRegion` adds `GTG Crown Region`, a 120 x 40 x 120 m box at the origin with `ManagedCrownRegion`, and the `ManagedCrownHudHeat` feeder that moves the HUD crowns. In Play mode the menu adds 10 heat, removes 10 heat or clears every region and prints each one. Wanted starts at 10 heat, hunted at 40 and outlaw at 75.

**Decisions:**
- The menu moves every region, because a scene can hold more than one.

### `ManagedFavorLab.cs`

**What it does:** Menu `GTG, Favor`. `EnsureRegion` adds `GTG Favor Region`, a 120 x 40 x 120 m box at the origin. `Add Favor To Selected NPCs` adds `ManagedNpcFavor`. In Play mode the menu moves the crowd favor of every region by 10. An NPC that likes the player (20 or more) stops treating it as a threat.

**Decisions:**
- `SpawnNpc` does not add `ManagedNpcFavor`. It is added only by this menu.

### `ManagedGlyphLab.cs`

**What it does:** Menu `GTG, Glyphs`. It sets up a glyph preview at the scene view pivot, toggles the preview's style and atom look, and adds `ManagedGlyphCastView` to the player's spell caster, which is the menu to run before pressing F or left click to see the fireball glyph. Its full description is in `glyphs.md`, section `ManagedGlyphLab.cs`.

**Decisions:**
- `Ensure Cast View On Player` calls `ManagedMagicLab.EnsureCaster`, so it adds the caster when the scene has none. `Build Test Arena` does not call it, so a scene built by the arena needs this menu once for the glyph to show.
- The lab is not part of `Build Test Arena`, to keep the arena as it was.

### `ManagedLabArena.cs`

**What it does:** Menu `GTG, Lab`. `Build Test Arena` calls the Ensure methods of the labs above and adds `ManagedBodyFacing`, `ManagedHealth`, `ManagedNpcThreatSource`, `ManagedPlayerItemBag` and `ManagedPlayerWallet` to the player. A first run also places three dummies: one at 1.8 m, one at 8 m and one at 14 m. A scene that already has `Melee Dummy 1` keeps what it has. `Print What The Scene Has` logs which of the main components exist and how many healths and NPC brains there are.

**Decisions:**
- It is the one lab that reads every feature folder, and it only calls the other labs, so each lab stays usable alone.
- It adds no HUD. A `ManagedHud` placed by hand gives the bars.

## Not verified

The lab scripts compile against the repo's Managed C# and a hand-written set of `UnityEditor` stand-ins, which checks every call into game code. Unity itself has not run them. Not checked: menu placement, the URP material, the `m_LayerMask` field name on the NavMesh Surface, gizmo drawing, and how an imported model behaves under `Use Selected As Player Model`. `ManagedGlyphLab.cs` is left out of the harness by the `Managed/**/Editor/**` exclusion. Its first four menus ran in Unity with the first glyph drop. The style, atom look and cast view menus were checked by reading only and not compiled.

## Fixes and Problems

### Managed C# CI was red on origin 146992d and d0fd1d5 (`building-test/`, `rust/npc/managed-npc-test/`)

- **Cause:** `building-test/BuildingTest.csproj` compiled `Managed/Inventory/`, which needs the Inventorizz package, so the compile step failed with 16 errors (CS0246 and CS0234). The workflow header and `managed.md` already said Inventory stays out of this job, the project file did not.
- **Fix:** `Managed/Inventory/**` and `Managed/**/Editor/**` are in the Exclude list. `packages-test/` still compiles Inventory against the real package.
- **What the compile then exposed:** with Inventory out, the melee, facing and camera rig code compiled for the first time, and the checks found two harness faults, not game faults. The stand-in `Transform.name` returned an empty string, so the camera rig wiring check could not find its cameras. Fixed in `building-test/UnityStubs.cs` with a `name` that returns the name of the GameObject. Four HUD bag checks read `hud.Bag` before any frame had run, so it was null. Fixed in `building-test/TestsItems.cs` with one `HudFrame(hud, 1f)` before the first quick slot check.
- **Result:** 1030 checks, 0 failed. The NPC harness is unchanged at 1282 checks.
- **Not fixed here:** `managed.md`, section "CI and Workflows", still lists the old exclusions. It needs the Editor exclusion added the next time that file is edited.
