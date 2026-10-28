# Glyphs (`MidManStudio.Gtg.Managed.Glyphs`)

> Code location: `Assets/MidManStudio/Gtg/Managed/Glyphs/`. The editor lab is in `Assets/MidManStudio/Gtg/Managed/Glyphs/Editor/`. Not wired into the spell caster yet.

## Overview

A glyph is the glowing line-art magic circle drawn while a spell is cast: rings made of lines, joined by lines to smaller rings, which grow rings of their own, with swirls, drips and dots around them. It is drawn on a flat plane in the world, in front of the hand or around a target.

- **A glyph is data, not an object.** A seed and a few numbers decide every line. The same spec always gives the same glyph.
- **Baked once, animated by the shader.** The lines become one mesh per glyph. The mesh never changes. A number called reveal (0 to 1) grows the lines in the shader, so charging a spell costs no CPU work per frame.
- **Depth 0 to 7.** Level 0 is the root ring. Each level adds the next ring of child nodes. A level is drawn when the reveal reaches its slice of the 0 to 1 range.
- **No GameObject per circle, no `Graphics.DrawMeshInstanced`.** One `Graphics.DrawMesh` per glyph, so the fallback rule for instanced drawing does not apply.
- **Own folder, decoupled.** Nothing in `Magic/` references it. `ManagedGlyphPreview` and the lab are the only users so far.

## Design notes

### What the reference art is made of

A graph of strokes, not particles: a white core with a colored halo, 1 to 3 concentric rings as nodes, lines between nodes, swirl curls, drips ending in dots, loose dots, and flat wheels stacked on one axis. "2-wheel composite" is two such figures at once. The generator makes exactly those parts.

### Parts

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

### The tree

- A node is one to three concentric rings (level 0: 1 to 3, deeper: usually 1). It may carry one figure (rosette, star or nest), a curl, a drip and up to two dots.
- A node with depth left grows 2 or 3 children at levels 0 to 2, 1 or 2 at levels 3 and 4, and mostly 1 beyond that (the taper keeps depth 7 from exploding). A child is 46 to 60 percent of its parent's radius and sits 45 to 95 percent of a parent radius away. Children of a non-root node fan out on the far side, away from the node they hang from.
- Line width falls from the full width at level 0 to 40 percent at level 7, never below 0.6 percent of the root radius.
- Level colors come from the palette (`Fire`, `Water`, `Arcane`, `Prism`), so deeper rings change color. Wheel w uses the palette shifted by w.
- Overall size at depth 7 is 4.1 to 4.8 times `Radius` (measured on four seeds). `Radius` is the root ring, so the figure grows outward while level 0 stays put.

### Reveal and charge

- `reveal` runs 0 to 1. Level L owns the slice from L / (depth + 1) to (L + 1) / (depth + 1). Inside a slice the edges grow first, then the rings, then the figures, then the dots.
- `BaseDepth` is the number of levels already drawn at charge 0 (the default ring). 0 is the root ring alone. -1 draws nothing until charging starts.
- `RevealForCharge(charge)` goes from the base reveal to 1. `VisibleLevels(reveal)` says how many levels have started.
- **Working choice, not locked:** the spell sets `BaseDepth` and `MaxDepth` (its tier), and the charge fills the rest, up to the fixed ceiling of 7. This follows the user's answer: depth comes from both charge and tier, some spells start with a default ring, and the deepest nesting is 7.

### Mesh and shader

- Every stroke becomes a ribbon: two vertices per point, and a round end cap on every open stroke. A closed ring wraps with no cap, so there is no seam.
- Vertex data: `Uv0` = (distance along the stroke, side -1 to 1), `Uv1` = (stroke length, outer half width), `Uv2` = (reveal value where the stroke starts, where it is whole), color = halo tint.
- The fragment shader draws a white core (30 percent of the width) with a soft halo, grows the stroke up to `drawn * length` with a round bright tip, and adds a brightness wave that runs along the lines. It uses ordinary alpha blending, which should also read on a bright sky (not seen in Unity).
- Triangles are sorted by level, so the fallback can draw the first N levels with a shorter index count.

### Cost

Measured by the CI harness on 60 seeds at depth 7 with 1 to 3 wheels (vertices of the worst glyph):

| LOD | Depth cap | Point budget | Worst vertices | Used when |
|---|---|---|---|---|
| Near | 7 | 9000 | 22176 | under 12 m, at most 3 per frame |
| Mid | 4 | 2400 | 6112 | under 35 m, or a Near past the cap |
| Far | 2 | 600 | 506 | beyond that |

- The point budget is split between the wheels. When it runs out, deeper levels are dropped, the same way every time.
- A bake is plain array work done once per spec and LOD, then cached (24 glyphs by default, least recently drawn dropped first). Casting the same spell again costs nothing to bake.
- `MaxDrawsPerFrame` (12) and `MaxNearPerFrame` (3) bound the draw cost. `Draw` returns false when it refuses.
- Not measured: GPU time and frame time on a real machine.

### Why not the other ways

- A `LineRenderer` per stroke is hundreds of components and CPU work every frame.
- Particle trails cannot make clean circles. Particles are fine for loose sparks only.
- A pure fragment shader on a quad costs per pixel and the graph has too many nodes.

## Modules

### `ManagedGlyphTypes.cs`

Limits, the LOD and ornament enums, the palette, `ManagedGlyphSpec` (everything that decides the look), `ManagedGlyphLodSettings` (depth cap, segment density, point budget and allowed ornaments per LOD) and the stroke types the generator returns. `CacheKey(lod)` hashes the fields that change geometry. `BaseDepth` is left out because it does not.

### `ManagedGlyphGenerator.cs`

`ManagedGlyphRandom` (seeded xorshift32, never `UnityEngine.Random`) and `ManagedGlyphGenerator.Generate(spec, lod)`. Builds the tree level by level, so the point budget always cuts the deepest nodes first. Pure arithmetic with no Unity objects, so it runs in the CI harness.

### `ManagedGlyphBaker.cs`

Turns strokes into `ManagedGlyphMeshData` arrays: positions, three UV sets, vertex colors, triangles sorted by level, the end index of every level and the bounds. Pure array work.

### `ManagedGlyphRenderer.cs`

Caches one `Mesh` per spec and LOD and draws with `Graphics.DrawMesh` and a property block holding `_Reveal` and `_Fade`. `Draw(spec, lod, matrix, reveal, fade)` is called every frame a glyph is alive. `ChooseLod(distance)` picks the LOD. Without the glyph shader it falls back to `Sprites/Default` and draws whole levels one after another (no growing, flat color). With no shader at all it logs one error and draws nothing.

### `ManagedGlyphLine.shader`

Hand-written URP unlit shader `MidManStudio/Gtg/GlyphLine`, found by name like `ManagedSphereUnlit.shader`. Properties: `_CoreColor`, `_Intensity`, `_CoreWidth`, `_GlowPower`, `_HeadBoost`, `_PulseSpeed`, `_PulseAmount`, and `_Reveal` and `_Fade`, which the renderer sets per glyph. The material is `ManagedGlyphRenderer.GlyphMaterial`, so one change tunes every glyph of that renderer.

### `ManagedGlyphPreview.cs`

A test component that draws one glyph at its own transform. Seed, depth, base depth, radius, line width, wheels, palette, charge, auto charge, fade and a forced LOD are in the Inspector. `DescribeStats()` bakes the glyph at every LOD and reports vertices, triangles, strokes and bake time.

### `ManagedGlyphLab.cs`

Editor only. Menu `GTG, Glyphs`: Ensure Preview At Scene View, New Seed On Selected Preview, Toggle Auto Charge On Selected Preview, Print Glyph Stats. It uses `GtgLabKit` like the other labs. The `lab.md` menu table does not list these four yet.

## Known gaps

- **Not hooked into casting.** Nothing in `Magic/` draws a glyph yet. The planned hook is a small interface call from the caster: charge drives `RevealForCharge`, release hands the glyph to the shot.
- **No ink outline.** The red art in the reference has a black outline around the glow. The shader has core and halo only.
- **No wobble animation.** The hand-drawn wobble is baked in. Only a brightness wave moves.
- **No facing control.** The glyph faces the object's local Z. Facing a camera or a target is the caller's matrix.
- **No collision or overlap test.** Children can overlap each other at depth 5 to 7. It reads as hand drawn, but it can clutter.
- **Edit mode.** The preview redraws only when Unity runs the scene update. Play mode is the reliable way to watch it.
- **Always Included Shaders.** `Shader.Find` finds the glyph shader in a build only if it is referenced or listed under Always Included Shaders. `ManagedSphereUnlit.shader` has the same condition.

## Not verified

- **Unity.** Nothing here has run in the Unity Editor: the shader compiling, the look, the draw-on, the brightness, URP behavior of `Graphics.DrawMesh`, and the editor menu. The CI harness compiles the generator, baker, renderer and preview against hand-written stand-ins and checks counts, ordering, budgets and draw calls, not pixels.
- **Frame cost.** The vertex counts above are measured. The GPU and CPU time are not.
- **Shader picture.** The images used to judge the generator were drawn with a simple 2D line renderer, not the shader.

## CI

`building-test/TestsGlyphs.cs` adds the checks (generator determinism and levels, figure bounds, per-LOD budgets, baker layout and level order, renderer caps, cache and fallback). The Editor lab is excluded from the harness by the `Managed/**/Editor/**` exclusion in `building-test/BuildingTest.csproj`. Until that csproj is on origin, the harness compiles the lab and fails. `building-test/UnityStubs.cs` gained `ExecuteAlways`, `Mesh.SetUVs` and `subMeshCount`, `Material.SetFloat`, `MaterialPropertyBlock.SetFloat` and the nine-argument `Graphics.DrawMesh`.

## Fixes and Problems

### `ManagedGlyphRenderer.cs`

- **Cache stayed over its cap.** The first version dropped one glyph per new bake, so a cache whose `MaxCached` was lowered stayed above the cap. The harness caught it. It now drops until there is room.

### `ManagedGlyphPreview.cs`

- **`Camera` is a namespace here.** Inside `MidManStudio.Gtg.Managed`, `Camera` means the `Managed.Camera` namespace. The file writes `UnityEngine.Camera` in full.
