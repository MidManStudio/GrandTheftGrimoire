# Building (`MidManStudio.Gtg.Managed.Building`)

## Overview

Station placement for the demo, in the style of Minecraft: the player opens a tray, takes a station
out of it and puts it on the ground. A ghost follows the pointer and is blue where the station can
stay and red where it cannot. A placed station can be clicked, carried and put down again.

Everything lives in `Assets/MidManStudio/Gtg/Managed/Building/` and has no ECS dependency, so it
belongs to the Managed stack described in `managed.md`. The ECS stack is frozen, so nothing was
added there. The parity row is in the table of `managed.md`.

Three decisions shape the system.

- **Placed pieces are data, not objects.** The store keeps parallel arrays indexed by piece id, and
  one matrix array per definition, in blocks of 1023. Each block goes to the GPU with one
  `Graphics.DrawMeshInstanced` call straight from its array. No piece is a GameObject. This is the
  method of the `DanVioletSagmiller/UnityDodNoEcs` repository, applied to our own store. That
  repository does not cull either, so culling is backlog here too.
- **The rules exist once.** Dragging from the tray, clicking a tray slot and picking a piece up from
  the world all end in one `Placing` state of `ManagedPlacementController`. The controller knows
  nothing about mice, screens or UI. The caller feeds it an aim point.
- **The UI is built in code.** The tray is UI Toolkit, created from C# with no UXML or USS asset,
  so there is nothing to wire in the editor except an optional `PanelSettings` asset.

### Controls

| Input | Effect |
|---|---|
| `B` | Opens and closes build mode. The key is a field on `ManagedBuildMode`. |
| Press a tray slot, drag out, release | Places one station where the ghost is blue. Releasing on a red spot keeps the station in hand. Releasing over the tray cancels. |
| Click a tray slot, then click the ground | Same, with a click. It keeps placing while the inventory has more of that station. |
| Click a placed station | Picks it up. Click again to put it down. Moving a station costs nothing. |
| `R`, `Shift+R`, mouse wheel | Turns the station a quarter turn either way. The next station starts with the last rotation. |
| Right click or `Esc` | Cancels. A carried station goes back to where it was lifted. |
| Click the tray with a station in hand | Puts the station into the inventory. |
| Inventory and Blueprints tabs | Inventory lists stations the player holds. Blueprints lists unlocked schematics, which can be placed without holding the station. |

While the tray is open the cursor is free, so mouse look stops and the Cast module of the character is
switched off. Movement works as usual and the keys `I`, `J`, `K`, `L` still turn the view, which is
how the player looks around without leaving build mode.

### Scene setup

1. Add `ManagedBuildMode` to any GameObject. Adding it through the inspector runs `Reset`, which
   puts one crafting table and one butchery station into the starting inventory and unlocks both
   blueprints. A component added from code starts empty.
2. Put the player on its own layer and clear that layer from `Ground Mask`. Otherwise the top of the
   player counts as ground for the five samples under a footprint. `Blocker Mask` keeps the player,
   so a station cannot be dropped on top of the player.
3. Optional: assign a URP Lit material to `Piece Material`. Without one, the renderer looks up
   `Universal Render Pipeline/Lit` by name, which a player build only finds when something else
   references that shader.
4. Optional but needed for a player build: create a Panel Settings asset (Create, UI Toolkit, Panel
   Settings Asset) and assign it. Without one, a runtime copy is created, which works in the editor.
5. `ManagedCameraRig` and `ManagedCharacter` are found automatically. A scene without the rig works
   too, the component then sets the cursor itself.
6. Press Play, press `B`.

Unity creates `.meta` files for the new scripts on first import. Commit them.

To give the player more stations from code: `buildMode.Stock.Give("crafting_table", 2)`. To unlock a
schematic: `buildMode.Blueprints.Unlock("butchery_station")`. A game that keeps its items elsewhere
implements `IManagedBuildStock` over that store and calls `SetStock` before `Start`.

## Modules

### `ManagedBuildableDefinition.cs`

**What it does:** `ManagedBuildableDefinition` (id, display name, category, footprint in cells,
height, color, whether a blueprint gates it, material cost) and the static `ManagedBuildableCatalog`
that holds the table.

**Decisions:**
- The table lives in code for now and moves to an mdix table later, like the spell definitions. The
  position in the table is the type index the store uses, so entries are appended and never reordered.
- The footprints (3 by 2 cells for the crafting table, 4 by 2 for the butchery station), the heights
  and the colors are placeholders. There is no art and no measured design for either station yet.
- Both definitions require a blueprint, and every cost list is empty. The Blueprints tab therefore
  shows only unlocked ones and placing from it is free until costs are filled in.
- The same string is the definition id and the stock item id, so a stock entry whose id is in the
  catalog is a placeable station and any other entry is a material.

### `ManagedBuildRules.cs`

**What it does:** The numbers every placement check shares, serialized on `ManagedBuildMode`: cell
size (0.5 m), reach (8 m), largest ground spread (0.15 m), blocker skin (0.1 m) and edge inset (0.05 m).

**Decisions:**
- The skin lifts the blocker box off the ground, because the ground is itself a collider. The inset
  pulls the samples and the box in from the footprint edge, so a neighbor that touches the edge does not block.
- A reach of zero or less turns the limit off.

### `ManagedBuildGrid.cs`

**What it does:** Square cells on the XZ plane that record which piece owns them. Pure arithmetic.

**Decisions:**
- A footprint is anchored at its minimum corner cell. A quarter turn swaps width and depth, and the
  footprint center does not move, so a rotated piece turns around its middle.
- The anchor is `floor(x / cell - size / 2 + 0.5)`, which puts the footprint center on the grid
  position nearest to the aim for every size, negative coordinates included.
- Cells are keyed by `(x << 32) | (uint)z` in one dictionary, so negative cells do not collide.

### `ManagedPlacedStore.cs`

**What it does:** Every placed piece as parallel arrays by id, one `ManagedPieceBucket` per
definition, and the grid. Adding and removing keep the three in step.

**Decisions:**
- A bucket is a list of 1023 element blocks, which is the instance limit of `DrawMeshInstanced`.
  Removal moves the last element into the hole (swap remove), so every block but the last stays full
  and no array is copied.
- Ids are reused after a removal, so a holder of an id listens to `PieceRemoved`. `PieceAdded` and
  `PieceRemoved` carry a snapshot of the piece.
- `Add` returns -1 and changes nothing when the type is unknown or a cell is taken.
- `Clear` raises `PieceRemoved` once per piece after the store is empty, so the collider proxies follow.
- `BuildMatrix` scales the unit box to the unrotated footprint and then turns it, so the grid cells
  and the drawn box agree for every quarter.
- The store is the thing to save and load: `CopyPieces` reads it and `Add` fills it. No save format exists yet.

### `ManagedPlacementValidator.cs`

**What it does:** Decides whether a footprint can stand at the aimed spot and says why not.

**Decisions:**
- Cheap checks run first: aim, reach, then the grid. A taken spot costs no physics query.
- Five ground samples (four inset corners and the center) must all hit ground and differ by no more
  than the allowed spread. The piece stands on the highest sample.
- Then one box overlap, raised by the skin. A piece lower than the skin has no blocker box.
- `IManagedBuildWorldProbe` is the only door to physics, so the validator is plain C# apart from the vector type.
- A default `ManagedPlacementResult` reads as valid, because `Valid` is the zero value of the status.
  The controller therefore always starts and ends with `NoAim`, see Fixes and Problems.

### `ManagedUnityBuildProbe.cs`

**What it does:** Answers the two probe questions with Unity Physics. The ground ray starts 1.5 m
above the reference height and travels 6 m down, so a roof above the player is not hit. Triggers
never count. Ground and blocker masks are separate.

### `ManagedBuildStock.cs`

**What it does:** `IManagedBuildStock` (what building reads and spends), the in-memory
`ManagedBuildInventory`, and `ManagedBlueprintBook` (which schematics are unlocked).

**Decisions:**
- The interface keeps the door open for an Inventorizz adapter. Nothing in Building references it.
- A blueprint that does not require unlocking is always available.

### `ManagedPlacementController.cs`

**What it does:** The placing state machine. A plain C# class with events, no MonoBehaviour.

**Decisions:**
- Three sources. Stock spends one item on commit. Blueprint spends the definition's cost, which is
  nothing for now. Carried is a picked-up piece, which is free to put down.
- After a click placement from stock or a blueprint the controller keeps placing while the source
  can pay for another. A drag places one and leaves.
- A source that cannot pay overrides the spot, so the ghost turns red instead of silently refusing.
- A carried piece is removed from the store while in hand, so its own old spot is valid for it. Cancel
  restores it. If that ever failed, the station goes to the stock rather than being lost.
- `ArmForClick` turns a drag into click placement, used when a drop lands on a bad spot.
- If paying fails after the piece was added, the piece is removed again.

### `ManagedBuildAim.cs`

**What it does:** Turns the pointer ray into an aim point on the ground and the placed piece under the pointer.

**Decisions:**
- One `RaycastNonAlloc` answers both. The hits come back unordered, so the nearest of each kind is
  picked by hand.
- A collider with a `ManagedPlacedPieceProxy` is a piece and never ground. The aim point lands on the
  floor behind a station, and the piece is hovered only when it is in front of that floor.
- Everything under `IgnoreRoot` (the player) is skipped, so the camera behind the player does not aim at the player.
- The hit buffer holds 24. A ray through more colliders than that may miss the nearest.

### `ManagedPlacedColliders.cs`

**What it does:** Gives each piece a pooled BoxCollider proxy that follows the store events, so the
player walks into stations, rays can pick them and the blocker box sees them.

**Decisions:**
- The proxies are inactive while they are positioned and become active when ready, so physics sees
  them in their final place.
- The box size is the unrotated footprint and the rotation is the quarter turn, so the world bounds
  equal the grid cells for every quarter.
- One collider per piece suits stations. A structure with thousands of pieces would need merged colliders.
- The proxy root is a scene root object with no rotation or scale, because positions are in world
  space and sizes are local.

### `ManagedBoxMesh.cs`

**What it does:** Vertex data of a unit box: 24 vertices, flat shaded, 36 indices.

**Decisions:** Pure arrays, so it is testable without Unity. Winding was checked face by face, so the
geometric normal of every triangle points out.

### `ManagedPlacedRenderer.cs`

**What it does:** Draws every piece with one instanced call per block, plus the single-box ghost and
hover marker.

**Decisions:**
- Piece materials copy `Piece Material` or look up URP Lit, then Standard, then `Sprites/Default`.
  The tint goes to `_BaseColor` and `_Color`, and instancing is switched on.
- The overlays use `Sprites/Default`, which every build includes, and are 2 percent larger than the
  piece so a ghost standing on the ground does not fight it for depth.
- Blue is `(0.25, 0.55, 1, 0.55)` and red is `(1, 0.2, 0.2, 0.55)`. The ghost is skipped without an aim.
- No frustum culling. Every block is drawn each frame. The reference repository does not cull either.

### `ManagedBuildInput.cs`

**What it does:** Polls the keyboard and mouse once per frame, in the same direct style as `ManagedCharacterInput`.

**Decisions:**
- The toggle key is looked up by index. The Input System indexer throws for `Key.None` and `IMESelected`, and the key is an inspector field.
- Gamepad and touch have no path into build mode. The world aim is the mouse.

### `ManagedBuildUiHost.cs`

**What it does:** Adds the `UIDocument`, creates a `PanelSettings` when none is given, sets the
built-in font on the root, and makes sure an EventSystem with the Input System UI module exists.

**Decisions:**
- A runtime `PanelSettings` has no theme style sheet, so Unity may log a warning and text needs the
  font set on the root. An asset is the reliable choice.
- UI Toolkit reads input through an EventSystem. With the Input System package that must be
  `InputSystemUIInputModule`, and a scene with `StandaloneInputModule` gets one warning.

### `ManagedBuildPanel.cs`

**What it does:** The tray. A bottom dock with Inventory and Blueprints tabs, slots with a swatch,
name and count or cost, a status line and a hint line.

**Decisions:**
- Plain C# that takes its collaborators through the constructor. There is no hierarchy to search.
- A slot press asks `ManagedBuildMode` whether placing started. If so, the slot captures the pointer,
  so the release arrives at the slot wherever it happens. A move under 6 pixels counts as a click.
- Slots are updated in place while placing. A stock change schedules a rebuild for the next tick,
  because rebuilding inside the pointer event would pull the element out from under its own handler.
- Hiding the tray releases a held pointer without a callback.
- The status line shows the reason a spot is red. `ShowNotice` shows why a press was refused.
- `IsOverTray` converts the screen position with `RuntimePanelUtils.ScreenToPanel` and flips Y,
  because the panel counts from the top and the screen from the bottom.

### `ManagedBuildMode.cs`

**What it does:** The scene component. Builds the parts in `Start`, runs the per-frame loop in
`LateUpdate` (execution order 200, after the character and the rig), and owns the cursor.

**Decisions:**
- Each frame: read input, aim, then act on the state the frame started in. A press that picks a
  piece up cannot also put it down.
- While the pointer is over the tray there is no aim, so no ghost shows behind the tray and a click
  on the tray never places.
- A drop is re-aimed at the release position first, because the aim of the last frame can lag by one frame.
- Opening build mode releases the cursor through `ManagedCameraRig` and remembers whether it was locked.
  Closing restores that, so a cursor the player had unlocked with Esc stays unlocked.
- Opening switches the character's Cast flag off, and closing switches it back on only if this
  component switched it off. The mouse cannot fire with a free cursor, but `F` and the gamepad could.
- `OnDisable` leaves build mode, so a disabled component never leaves the cursor or the Cast flag changed.
- `UnityEngine.UIElements` is not imported in this file, because its `Cursor` struct collides with
  `UnityEngine.Cursor`. Inside `MidManStudio.Gtg` namespaces the simple name `Camera` is a namespace,
  so `UnityEngine.Camera` is written in full.
- `SetStock` throws after `Start`, because the controller and the tray take the stock in their constructors.

## CI and Workflows

- `.github/workflows/apply-replacements.yml` applies the replacements archive that delivered these
  files. See `managed.md`.
- No CI job compiles this system yet. It was verified with a harness that lives outside the repository,
  see Fixes and Problems.

## Fixes and Problems

### Verification

- Not compiled by Unity and not run in the Editor. The Building files and the edited
  `ManagedCameraRig.cs` compile with .NET 8 at C# 9, the language level of Unity 2022.3, against hand
  written stand-ins for the `UnityEngine`, `UnityEngine.InputSystem`, `UnityEngine.EventSystems` and
  `UnityEngine.UIElements` types they use. 553 checks pass there.
- The checks cover the grid, the store (a fuzz of 6000 random adds and removes, 2050 pieces across
  three blocks), box winding, every validator status, the controller flows (click, drag, blueprint with
  and without cost, carry, stash, cancel, rotate, payment rollback), the aim, the probe, the collider
  proxies, the renderer calls, the input, the tray event flow, the UI host, the whole scene component
  against a small physics world, and the camera rig hook.
- Thirty-three deliberate breaks were applied one at a time, and 32 made a check fail. The one that did not
  (removing the tray test from the click commit condition) changes nothing, because no aim exists over the tray.
- The stand-ins prove the code is consistent with them and with the signatures read from the Unity C#
  reference source, not that Unity behaves the same. The physics stand-in uses axis aligned bounds, and
  the UI stand-in models capture, bubbling and stop propagation as the tray relies on them.
- Check on first Play: the Console, the tray appearing at the bottom, the ghost color, and a drag from the tray.

### `ManagedBuildUiHost.cs`

- The built-in font is loaded with `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`. The name
  matches the default font of Unity 2022.2 and later, but the exact string was not confirmed against a
  running editor. If it is wrong, the tray text shows no glyphs. The call is wrapped, so it fails
  quietly. The fix is a font assigned through the Panel Settings asset's theme.

### `ManagedPlacementController.cs`

- Found by the checks: the result of a controller that had never placed anything was the default
  result, which reads as a valid spot. The constructor and `Leave` now set `NoAim`.

### `ManagedPlacedStore.cs`

- Found while reviewing: `Clear` raised no events, so collider proxies stayed active after a reset.
  It now raises `PieceRemoved` per piece.

### `ManagedBuildInput.cs`

- Found while reviewing: the Input System keyboard indexer throws for `Key.None`, which the inspector
  can select. The toggle key is looked up by index now.

### `ManagedBuildMode.cs`

- A drag from the tray needs a mouse. Gamepad and touch cannot aim in the world.
- Pieces stand on the ground only. The aim ignores placed pieces, so a station cannot be put on top of another.
- Placed pieces are not saved. The store can be read and filled, but nothing writes it to disk.
- Frustum culling is backlog. A few hundred boxes cost nothing, but a large structure would want it.
- Definitions, footprints and colors are placeholders, see `ManagedBuildableDefinition.cs`.
- "Blueprints" is built as unlockable schematics next to the blue and red hologram. If only the
  hologram was meant, the Blueprints tab can go and `RequiresBlueprint` can be dropped.
- `Sprites/Default` as the ghost shader was chosen from knowledge of the URP unlit pass tags and was
  not seen rendering. If the ghost is invisible, check the layer of the ghost against the camera culling mask first.
