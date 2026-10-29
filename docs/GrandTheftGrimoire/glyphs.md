# Glyphs (`MidManStudio.Gtg.Managed.Glyphs`)

> Code location: `Assets/MidManStudio/Gtg/Managed/Glyphs/`. The editor lab is in `Assets/MidManStudio/Gtg/Managed/Glyphs/Editor/`. Wired into the player's spell caster through `ManagedGlyphCastView`, which reads the caster's wind-up. Fireball, Ice and Seeker draw a glyph while they wind up.

## Overview

A glyph is the glowing line-art figure drawn while a spell is cast. It comes in two styles. **Circle** is the magic circle: rings made of lines, joined by lines to smaller rings, which grow rings of their own, with swirls, drips and dots around them. **Molecule** is a chemistry diagram: a root ring with bonds, letters for the atoms, and rings, chains and groups that grow outward shell by shell. Both are drawn on a flat plane in the world, in front of the hand or around a target.

- **A glyph is data, not an object.** A seed and a few numbers decide every line. The same spec always gives the same glyph.
- **Baked once, animated by the shader.** The lines become one mesh per glyph and LOD. The mesh never changes. A number called reveal (0 to 1) grows the lines in the shader, so charging a spell costs no CPU work per frame.
- **Depth 0 to 7.** Level 0 is the root ring. Each level adds the next ring or shell. A level is drawn when the reveal reaches its slice of the 0 to 1 range.
- **No GameObject per glyph.** `Graphics.DrawMeshInstanced` is the primary draw path: glyphs that share a mesh go out in one call, with their reveal and fade as per-instance arrays. When instancing is not supported (or is forced off) the renderer falls back to one `Graphics.DrawMesh` per glyph. Callers use the same two calls either way: `Draw` and `Flush`.
- **Own folder, one-way dependency.** `Glyphs/` reads `Magic/` (the cast status and the spell kind). Nothing in `Magic/` references `Glyphs/`.

## Design notes

### What the reference art is made of

**Circle.** A graph of strokes, not particles: a white core with a colored halo, 1 to 3 concentric rings as nodes, lines between nodes, swirl curls, drips ending in dots, loose dots, and flat wheels stacked on one axis. "2-wheel composite" is two such figures at once. The generator makes exactly those parts.

**Molecule.** Taken from textbook chemistry sheets (cyclic compounds, functional groups, a ring that gains substituents step by step): hexagon and pentagon rings with alternating double bonds, N, O and S in the ring, fused rings, rings joined by a single bond, zigzag chains, and groups such as =O, OH, NH2, Cl, F, SH, CH3. The same white core and colored halo as the circle, so it glows the same way.

### Parts of a circle glyph

| Kind | What it is | Math |
|---|---|---|
| Ring | 1 to 3 concentric circles per node | x = cx + r cos t, y = cy + r sin t |
| Edge | Hand-drawn line between two rims | Cubic curve with two seeded sideways bends. It leaves the rim along the radius, which is the normal of the circle there (the tangent is perpendicular to it) |
| Rosette | Spirograph figure inside a ring | Hypotrochoid x = (K - m) cos t + d cos(((K - m) / m) t), y = (K - m) sin t - d sin(((K - m) / m) t). Epitrochoid x = (K + m) cos t - d cos(((K + m) / m) t), y = (K + m) sin t - d sin(((K + m) / m) t). d is below m, so the loops are soft and there are no cusps. K and m are coprime, so it closes after m turns and has K petals |
| Star | Star polygon {n/k} | Vertex i joins vertex i + k. Each chord is its own stroke with round ends, so the points stay sharp. {6/2} is the hexagram |
| Nest | Harmonograph | z(t) = (a1 e^(i p t) + a2 e^(i (-q t + f))) e^(-c t). Two counter-rotating arms with a slow decay spiral inward into a woven nest |
| Curl | Swirl that winds inward from the rim | Spiral with radius shrinking to 10 percent over 1.1 to 1.8 turns |
| Drip | Short stub from the rim ending in a dot | |
| Dot | A zero-length stroke, drawn as a round dot | |

### The circle tree

- A node is one to three concentric rings (level 0: 1 to 3, deeper: usually 1). It may carry one figure (rosette, star or nest), a curl, a drip and up to two dots.
- A node with depth left grows 2 or 3 children at levels 0 to 2, 1 or 2 at levels 3 and 4, and mostly 1 beyond that (the taper keeps depth 7 from exploding). A child is 46 to 60 percent of its parent's radius and sits 45 to 95 percent of a parent radius away. Children of a non-root node fan out on the far side, away from the node they hang from.
- Line width falls from the full width at level 0 to 40 percent at level 7, never below 0.6 percent of the root radius.
- Level colors come from the palette (`Fire`, `Water`, `Arcane`, `Prism`), so deeper rings change color. Wheel w uses the palette shifted by w.
- Overall size at depth 7 is 4.1 to 4.8 times `Radius` (measured on four seeds). `Radius` is the root ring, so the figure grows outward while level 0 stays put.

### The molecule shape

- **Root ring (level 0).** A hexagon (78 percent of seeds) or a pentagon (22 percent). The bond length is b = 2 R sin(pi / n), where R is `Radius`, the circumradius of the root ring. A hexagon gets Kekule double bonds drawn as a second, shortened line inside the ring. It may swap 0 to 2 ring atoms for N, a pentagon one atom for N, O or S. An N with a hydrogen shows the H pointing outward.
- **Shells (levels 1 and up).** Each level grows from the open sites left by the level before it: free ring vertices, free ring edges and the tips of open chains. Level 1 and 2 place 3 to 4 pieces, levels 3 and 4 place 2 to 3, deeper levels 1 to 2. Sites are tried newest first, then the leftovers from older shells.
- **Pieces.** Substitute (=O, OH, NH2, Cl, F, SH, CH3, PH2, picked by weight), Chain (1 to 3 bonds in a zigzag of plus and minus 60 degrees; it may end open, so the next shell grows from its tip, or end in a substituent; 20 percent contain a double bond), Link (a ring joined by one single bond) and Fuse (a ring that shares an edge, six or five sided).
- **Overlap.** Every placed ring and atom is a disc. A new piece is skipped if its discs come within 0.95 of an earlier disc's radius sum (its own parent is excluded). Edge sites that touch an already used vertex are skipped, so no ring is fused at a peri position. A used parent vertex is closed, so a vertex takes one piece only.
- **Draw order inside a level.** The pieces of one level draw on one after another: piece i starts at 0.05 + 0.5 * i / count of the level's slice and takes 0.45 of it.
- **Atoms.** `Letters` (default): element letters from `ManagedGlyphFont`, 0.5 of a bond tall, the bond is trimmed by 0.62 of the letter height where it meets a letter. A group on the left side is mirrored (`HO`, `H2N`), and digits are small and lowered. The letters and hetero atoms use the next palette color, so they stand out from the bonds. `Balls`: a small ring (radius 0.17 of a bond) with a dot in it, no letters.
- **Orbits were left out.** The "atom with electron orbits" drawing on one of the reference sheets is not used. The Dot ornament draws electron dots next to a heteroatom instead. This was a decision of the project owner.
- **Size.** A molecule grows about twice as wide as a circle for the same `Radius`: at depth 4 it spans 5.4 to 9 times `Radius` (a circle about 3.5), and at depth 7 the widest of 60 seeds was 17.3 times `Radius`. That is why the Ice spell uses a root radius of 0.13 against 0.22 for the circle spells, and why the lab halves or doubles `Radius` when it switches style.
- **Wheels.** `WheelCount` stacks several molecules along the glyph's Z, as it does for circles. Seen face on they overlap, which is expected. They show their depth when the glyph is tilted.

### Reveal and charge

- `reveal` runs 0 to 1. Level L owns the slice from L / (depth + 1) to (L + 1) / (depth + 1). Inside a slice the edges grow first, then the rings, then the figures, then the dots. In a molecule the pieces of a level run one after another, as above.
- `BaseDepth` is the number of levels already drawn at charge 0 (the default ring). 0 is the root ring alone. -1 draws nothing until charging starts.
- `RevealForCharge(charge)` goes from the base reveal to 1. `VisibleLevels(reveal)` says how many levels have started.
- The caster's wind-up feeds `charge` from 0 to 1 over the spell's `CastSeconds`, so the glyph draws itself on while the cast runs. **Working choice, not locked:** the spell sets `BaseDepth` and `MaxDepth` (its tier), and the charge fills the rest, up to the fixed ceiling of 7.
- **Charge is visual only for now.** Holding longer does not make a spell stronger. The owner wants some spells to be chargeable for more power later; that will need a hold-to-charge input, which does not exist yet.

### Mesh and shader

- Every stroke becomes a ribbon: two vertices per point, and a round end cap on every open stroke. A closed ring wraps with no cap, so there is no seam.
- Vertex data: `Uv0` = (distance along the stroke, side -1 to 1), `Uv1` = (stroke length, outer half width), `Uv2` = (reveal value where the stroke starts, where it is whole), color = halo tint.
- The fragment shader draws a white core (30 percent of the width) with a soft halo, grows the stroke up to `drawn * length` with a round bright tip, and adds a brightness wave that runs along the lines. It uses ordinary alpha blending, which should also read on a bright sky (not seen in Unity).
- `_Reveal` and `_Fade` are per-instance properties (`UNITY_INSTANCING_BUFFER_START(Props)`), read in the vertex stage and passed to the fragment stage in `TEXCOORD3`. The other properties stay in `UnityPerMaterial`. A shader with per-instance properties is not SRP Batcher compatible. That is accepted here, because glyphs are drawn through instancing and property blocks, not through renderers.
- Triangles are sorted by level, so the `Sprites/Default` fallback can draw the first N levels with a shorter index count.

### Drawing

`Draw` only queues a glyph. `Flush` sends the queue.

- **Instanced path (primary).** `Flush` sorts the queue by cache entry and sends one `Graphics.DrawMeshInstanced` per mesh. A call holds at most 1023 instances (`InstanceBatchSize`). The matrices go as one array, `_Reveal` and `_Fade` as `float[]` through `MaterialPropertyBlock.SetFloatArray`. The arrays keep a constant size, because Unity rejects a property array whose size changes.
- **DrawMesh fallback.** When `SystemInfo.supportsInstancing` is false, or the caller forces it (`forceDrawMesh`), or the glyph shader is missing and `Sprites/Default` is in use, `Flush` sends one `Graphics.DrawMesh` per glyph with a property block.
- **Sprites/Default fallback.** No growing and a flat color. Whole levels are drawn one after another from per-level mesh clones, so the reveal still steps through the levels.
- **Contract.** Every `Draw` must be followed by a `Flush` in the same frame. A queue that was not flushed is dropped on the next frame, with one warning. Cache eviction never drops a mesh that a queued draw still needs.
- `Path` says which path the renderer uses (`ManagedGlyphDrawPath.Instanced` or `DrawMesh`). `LastFlushCalls` is the number of draw calls the last `Flush` made, which is 1 for any number of copies of one glyph on the instanced path.

### Cost

Measured by the CI harness on 60 seeds at depth 7 with 1 to 3 wheels (vertices of the worst circle glyph):

| LOD | Depth cap | Point budget | Worst vertices | Used when |
|---|---|---|---|---|
| Near | 7 | 9000 | 22176 | under 12 m, at most 3 per frame |
| Mid | 4 | 2400 | 6112 | under 35 m, or a Near past the cap |
| Far | 2 | 600 | 506 | beyond that |

- The point budget is split between the wheels. When it runs out, deeper levels are dropped, the same way every time.
- A molecule is far lighter than a circle: its worst Near glyph at depth 7 has 1093 stroke points (one wheel).
- Mid allows letters (`Label`), Far draws no letters.
- A bake is plain array work done once per spec and LOD, then cached (24 glyphs by default, least recently drawn dropped first). Casting the same spell again costs nothing to bake.
- `MaxDrawsPerFrame` (12) and `MaxNearPerFrame` (3) bound the draw cost. `Draw` returns false when it refuses.
- Not measured: GPU time and frame time on a real machine.

### The cast flow

1. **Press.** `ManagedSpellCaster.Cast` draws the vessel (the SP it spends), starts a wind-up of `ManagedSpellDefinition.CastSeconds` and sets the cooldown to cast time plus the spell's cooldown. Fireball and Ice wind up for 0.5 s, Seeker for 0.6 s. A spell with `CastSeconds` 0 fires at once, as before.
2. **Wind-up.** `ManagedGlyphCastView` reads `ManagedSpellCaster.ReadCastStatus()` in `LateUpdate` (execution order 300, after the character and camera have moved). The status has the spell kind, charge 0 to 1, the shot point and the aim direction, all read live, so the glyph follows the hand and the crosshair without a frame of lag. `ManagedGlyphCastTracker` turns it into one item in front of the shot point, and the view draws it with the spell's reveal.
3. **Release.** When the wind-up ends the caster aims at the crosshair and launches the shot, exactly as the instant path does. The glyph finishes drawing, grows by 25 percent and fades over 0.35 s at the spot it was.
4. **No input change.** One press does everything. There is no hold.

The glyph and the caster stay separate: the view reads a plain status struct, and nothing in `Magic/` names a glyph type.

## Modules

### `ManagedGlyphTypes.cs`

Limits, the style, atom look, LOD and ornament enums, the palette, `ManagedGlyphSpec` (everything that decides the look), `ManagedGlyphLodSettings` (depth cap, segment density, point budget and allowed ornaments per LOD) and the stroke types the generator returns. `CacheKey(lod)` hashes the fields that change geometry, `Style` and `AtomLook` included. `BaseDepth` is left out because it does not change geometry.

- `ManagedGlyphStyle`: `Circle`, `Molecule`.
- `ManagedGlyphAtomLook`: `Letters`, `Balls`. Only a Molecule reads it.
- `ManagedGlyphOrnaments` is a flags enum: `Rosette` 1, `Star` 2, `Nest` 4, `Curl` 8, `Drip` 16, `Dot` 32, `Label` 64, `All` 127.
- `ManagedGlyphStrokeKind` gained `Bond`, `Letter` and `Atom` for the molecule.

### `ManagedGlyphGenerator.cs`

`ManagedGlyphRandom` (seeded xorshift32, never `UnityEngine.Random`) and `ManagedGlyphGenerator.Generate(spec, lod)`. For a Circle it builds the tree level by level, so the point budget always cuts the deepest nodes first. For a Molecule it hands over to `ManagedGlyphMolecule.Generate`. Pure arithmetic with no Unity objects, so it runs in the CI harness.

### `ManagedGlyphMolecule.cs`

The molecule generator described under "The molecule shape". Pure arithmetic, deterministic for a spec. It keeps a list of open sites (a vertex with an outward direction, a free ring edge, or a chain tip) and a list of discs for the overlap check. Each level `TryGrow` picks a site, picks a piece by weight, builds it, rejects it if its discs hit an older disc, and otherwise commits it and closes the vertices it used. `Radius` of the spec is the root ring's circumradius.

### `ManagedGlyphFont.cs`

A tiny stroke font for the element letters: N, O, S, H, C, l, P, F, B and the digits 2 and 3. A letter is one to three polylines in a one unit tall cell with the origin at the bottom left. `Width(c)` gives the cell width (0 for a letter the font does not have), `Has(c)`, `Gap` (0.22) is the space between two letters, and `Append(c, origin, height, output)` adds the lines. There are no textures and no TextMesh: a letter is drawn like any stroke, so it grows and glows with the rest.

### `ManagedGlyphBaker.cs`

Turns strokes into `ManagedGlyphMeshData` arrays: positions, three UV sets, vertex colors, triangles sorted by level, the end index of every level and the bounds. Pure array work.

### `ManagedGlyphRenderer.cs`

Caches one `Mesh` per spec and LOD and draws through the queue described under "Drawing". Constructor `(layer, forceFallback, forceDrawMesh, label)`; the shorter overloads fill the rest with false. `Draw(spec, lod, matrix, reveal, fade)` queues, `Flush()` sends. `ChooseLod(distance)` picks the LOD. `GetData(spec, lod)` returns the baked arrays (used by the harness and the stats). Without the glyph shader it falls back to `Sprites/Default` and draws whole levels one after another (no growing, flat color). With no shader at all it logs one error and draws nothing. `Dispose` destroys the meshes and the material.

### `ManagedGlyphLine.shader`

Hand-written URP unlit shader `MidManStudio/Gtg/GlyphLine`, found by name like `ManagedSphereUnlit.shader`. Properties: `_CoreColor`, `_Intensity`, `_CoreWidth`, `_GlowPower`, `_HeadBoost`, `_PulseSpeed`, `_PulseAmount`, and `_Reveal` and `_Fade`, which the renderer sets per glyph and which are per-instance. The material is `ManagedGlyphRenderer.GlyphMaterial`, so one change tunes every glyph of that renderer.

### `ManagedGlyphPreview.cs`

A test component that draws one glyph at its own transform. Style, atom look, seed, depth, base depth, radius, line width, wheels, palette, charge, auto charge, fade, a forced LOD and a force-DrawMesh switch are in the Inspector. It calls `Flush` after `Draw`. The renderer is rebuilt only when the force-DrawMesh switch changes. `DescribeStats()` bakes the glyph at every LOD and reports the style, the draw path, vertices, triangles, strokes and bake time.

### `ManagedGlyphSpellTable.cs`

`For(ManagedSpellKind)` returns the fixed glyph spec of a spell. It is the one place that connects spells to glyphs. The specs are built once and shared, and callers must not change them (the renderer caches by value).

| Spell | Style | Seed | Depth | Radius | Line width | Wheels | Palette |
|---|---|---|---|---|---|---|---|
| Fireball | Circle | 1 | 4 | 0.22 | 0.05 | 1 | Fire |
| Ice | Molecule, Letters | 4 | 4 | 0.13 | 0.07 | 1 | Water |
| Seeker | Circle | 3 | 3 | 0.22 | 0.05 | 2 | Arcane |

Base depth is 0 for all three. A spell with no entry gets the Fireball glyph. The table is code now and moves to an mdix table later, like the spell table.

### `ManagedGlyphCastTracker.cs`

Pure logic, no Unity objects. `Update(status, dt, output)` turns a `ManagedCastStatus` into the list of glyphs to draw this frame (`Item`: spec, position, forward, spin, scale, reveal, fade). While a wind-up runs there is one item at the shot point plus `ForwardDistance` along the aim (0.6 m), spinning at 18 degrees per second, with `reveal = spec.RevealForCharge(charge)`. When the wind-up ends the item finishes drawing, grows by `BurstGrowth` (25 percent) and fades over `FadeSeconds` (0.35 s). At most 4 fading glyphs are kept, so rapid casts do not pile up. `SizeScale` multiplies everything. `Clear()` drops all of it. `FadingCount` and `IsActive` are for the lab and the checks.

### `ManagedGlyphCastView.cs`

A MonoBehaviour that goes on the player next to `ManagedSpellCaster` (`RequireComponent`, one per object). It runs in `LateUpdate` with execution order 300. Each frame it reads the caster's status, feeds the tracker, picks the LOD from the distance to the main camera, builds the matrix (face the aim, roll by the spin, scale), calls `Draw` for every item and `Flush` once. Inspector fields: forward distance, size scale, spin, fade seconds, burst growth, the layer the glyphs are drawn on (default 0, the Default layer, which every camera draws), a force-DrawMesh switch and a log switch. `Describe()` returns one line (draw path, shader, whether a wind-up is running, how many glyphs are fading). It logs when a wind-up starts and ends. It owns one renderer, which it disposes in `OnDisable`.

### `ManagedGlyphLab.cs`

Editor only. Menu `GTG, Glyphs` (it uses `GtgLabKit` like the other labs):

| Menu | Does |
|---|---|
| Ensure Preview At Scene View | Adds a `ManagedGlyphPreview` at the scene view pivot |
| New Seed On Selected Preview | Picks a new seed |
| Toggle Auto Charge On Selected Preview | Charge cycles 0 to 1 in Play mode |
| Toggle Style On Selected Preview | Circle or Molecule. Halves the radius going to Molecule and doubles it going back |
| Toggle Atom Look On Selected Preview | Letters or Balls. Only a Molecule changes |
| Ensure Cast View On Player | Adds `ManagedGlyphCastView` to the player's caster (uses `ManagedMagicLab.EnsureCaster`, so it adds the caster if it is missing) |
| Print Glyph Stats | Vertices, triangles, strokes and bake time at every LOD, plus style and draw path |
| Print Cast View State (Play) | `ManagedGlyphCastView.Describe()` |

## Known gaps

- **Charge does not add power.** The glyph draws on over the cast time, and nothing more. Hold-to-charge for stronger shots is planned for certain spells later.
- **Size in first person is untuned.** The glyph sits 0.6 m in front of the shot point. A circle at 0.22 radius is large next to the hand and may fill the screen. Tune `Forward Distance` and `Size Scale` on the cast view in the Inspector.
- **Only the player winds up.** Mage NPCs still fire at once. They share `ManagedSpellDefinition`, which only gained a field that defaults to 0.
- **Not SRP Batcher compatible.** See the shader notes. Glyphs are batched by instancing in the renderer.
- **Batching is per renderer.** Two renderers never share a call. The cast view owns one, so it is one call per mesh, not one per scene.
- **No impact burst.** The glyph fades where it was. There is no burst when the shot hits.
- **No ink outline.** The red art in the reference has a black outline around the glow. The shader has core and halo only.
- **No wobble animation.** The hand-drawn wobble is baked in. Only a brightness wave moves.
- **No facing control** beyond the cast view's "face the aim". The preview faces the object's local Z.
- **Circle overlap.** Circle children can overlap each other at depth 5 to 7. It reads as hand drawn, but it can clutter. The Molecule has an overlap check and does not have this.
- **Edit mode.** The preview redraws only when Unity runs the scene update. Play mode is the reliable way to watch it.
- **Always Included Shaders.** `Shader.Find` finds the glyph shader in a build only if it is referenced or listed under Always Included Shaders. `ManagedSphereUnlit.shader` has the same condition.

## Not verified

- **Unity.** Nothing here has run in the Unity Editor except what the owner tried: the first glyph drop (circle, shader, preview) was tested in the Editor and "looks awesome". Not run in Unity: the per-instance shader properties, `Graphics.DrawMeshInstanced` with property block arrays under URP, the Molecule style and its letters, the `Balls` look, the cast view, the wind-up feel, the editor menus. The CI harness compiles the generator, baker, renderer, preview, tracker, view and the real caster against hand-written stand-ins and checks counts, ordering, budgets and draw calls, not pixels. A green harness is not a Unity check.
- **Frame cost.** The vertex and point counts above are measured. The GPU and CPU time are not.
- **Shader picture.** The images used to judge the generators were drawn with a simple 2D line renderer, not the shader.
- **Wind-up feel.** 0.5 s was chosen by judgment. The size and placement of the glyph in first person were not seen.
- **Layer and culling.** The cast view draws on the Default layer. A camera that culls it would hide the glyph.

## CI

Checks live in `building-test/TestsGlyphs.cs` (generator determinism and levels, figure bounds, per-LOD budgets, baker layout and level order, renderer caps, cache, instanced and DrawMesh paths, the `Sprites/Default` fallback), `building-test/TestsGlyphMolecule.cs` (the font, the molecule's rings, bonds, letters, limits and determinism) and `building-test/TestsGlyphCasting.cs` (the wind-up timer, the spell table and the cast tracker, frame by frame). The harness also compiles the real `ManagedSpellCaster`, `ManagedSpellDefinition` and `ManagedGlyphCastView`.

The Editor lab is excluded from the harness by the `Managed/**/Editor/**` exclusion in `building-test/BuildingTest.csproj`, and `Managed/Inventory/**` is excluded too. Until that csproj is on origin, the harness compiles the lab and fails. `building-test/UnityStubs.cs` gained `ExecuteAlways`, `Mesh.SetUVs` and `subMeshCount`, `Material.SetFloat`, `MaterialPropertyBlock.SetFloat` and `SetFloatArray`, the nine-argument `Graphics.DrawMesh`, `Graphics.DrawMeshInstanced` (with a record of each call and a snapshot of its arrays), `Matrix4x4` equality, `Quaternion.AngleAxis` and `Quaternion.LookRotation`. The stand-ins have no `Mathf.Tan`, so the glyph code does not use it.

## Fixes and Problems

### `ManagedGlyphRenderer.cs`

- **Cache stayed over its cap.** The first version dropped one glyph per new bake, so a cache whose `MaxCached` was lowered stayed above the cap. The harness caught it. It now drops until there is room.
- **One call per glyph.** The first version used `Graphics.DrawMesh` only. The owner asked for `Graphics.DrawMeshInstanced` as the primary path with `DrawMesh` as the fallback. The renderer now queues and flushes, which changed the contract: every `Draw` needs a `Flush`.

### `ManagedGlyphMolecule.cs`

- **Rings overlapped and fused at the wrong place.** Early renders showed rings crowding each other and a ring fusing across two used vertices. The overlap factor went from 0.82 to 0.95 and used parent vertices are now closed, with edge sites that touch a used vertex skipped.
- **Orbits removed.** The first version drew an atom with electron orbits as an ornament, from one of the reference sheets. The owner did not want it in the molecule style, so it is gone. The wheels and the other parts stay.

### `ManagedGlyphPreview.cs`

- **`Camera` is a namespace here.** Inside `MidManStudio.Gtg.Managed`, `Camera` means the `Managed.Camera` namespace. The file writes `UnityEngine.Camera` in full. `ManagedGlyphCastView.cs` does the same.
- **Renderer rebuilt every frame.** Toggling the force-DrawMesh switch rebuilds the renderer, and an early version compared the wrong value. It now remembers the value the renderer was built with.
