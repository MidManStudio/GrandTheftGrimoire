# Plasma (`MidManStudio.Gtg.Managed.Plasma`)

> Code location: `Assets/MidManStudio/Gtg/Managed/Plasma/`. The editor lab is in `Assets/MidManStudio/Gtg/Managed/Plasma/Editor/`. Two layers: the generic ring (types, ramp, renderer, shader, preview) and the spell impact burst (tracker, spell table, view) built on it. The ring behind the cast glyph is the next piece.

## Overview

A plasma ring is a glowing, turbulent ring with a dark hole, a bright lumpy edge and wisps that reach outward, colored by a ramp. It is drawn on a flat quad in the world.

- **One shader, no textures to render first.** The reference was a texture graph. Here every step of it is a few lines of math in one fragment shader, so there is no render texture and no CPU work per frame.
- **Live, not baked.** The noise runs with the ring's age, so the plasma keeps moving. Seed, radius, band width, turbulence, streaks and palette change the look per ring, and they are per-instance values.
- **Per-spell palettes.** Fire, Water and Arcane, the same three hues the glyph palettes use. A palette is one row of a small color ramp texture that the renderer bakes in code.
- **`Graphics.DrawMeshInstanced` is the primary path** with `Graphics.DrawMesh` as the fallback, like the glyphs. The two calls a caller uses are `Draw` and `Flush`.
- **Own folder, one-way dependency.** The generic ring (`ManagedPlasmaTypes`, `Ramp`, `Renderer`, the shader, `Preview`) references nothing in `Glyphs/` or `Magic/`. The burst files (`ManagedPlasmaSpellTable`, `ManagedPlasmaBurstView`) reference `Magic/` to hear spell impacts and to read the blast radius. `Magic/` and `Glyphs/` know nothing about plasma, so deleting the burst view removes the effect and leaves the spells untouched.
- **Spell impact burst.** `ManagedPlasmaBurstView` listens to `ManagedSpellCaster.Impact`, which the player's spells and NPC mages' spells both raise, and draws one expanding ring per hit in the spell's palette (see "Impact burst").

## Design notes

### The reference graph and what replaced each node

The reference is a texture node graph: 3D particles scatter, a particle motion render, a UV flow, a UV blur, a star shape warped by that flow, a slope blur, levels, an edge detect, a polar to cartesian unwrap and a color ramp. Its result is a blue ring of plasma with a dark center, next to the unwrapped flame strip the ring was made from.

| Graph node | Shader step |
|---|---|
| 3D particles, render particle motion, UV flow, UV blur | `flow`: two smooth three-octave noise fields sampled on the unit circle and drifting with age. They bend the lookups the way a blurred flow field bends UVs |
| Shape (star), warped | `edge`: a third noise sampled on the warped circle moves the ring's radius. This is the lumpy outline. `Turbulence` scales both the warp and the lump size |
| Slope blur | `w`: noise stretched along the radius (high frequency around the ring, low along it) and drifting outward with age. It gives the wisps |
| Levels | A remap (`(v - 0.08) / 0.85`, then clamp) and the `Intensity` multiplier |
| Edge detect | The profile around the outline: a thin bright line (`core`), a soft glow inside (`inner`) and wisps outside (`outer`, `reach`) |
| Polar to cartesian | The shader works in polar form to begin with: noise is sampled on the unit circle direction, so the ring has no seam and there is no unwrap step |
| Color ramp | A lookup in the ramp texture, brightness across and palette down |

### Parameters

| Field | Range | Effect |
|---|---|---|
| `Seed` | any int | Picks the pattern. Folded into 0 to 250 and scaled by 0.37, so a huge seed cannot push the noise out of float range |
| `Palette` | Fire, Water, Arcane | Which row of the ramp |
| `RingRadius` | 0.15 to 0.85 | Radius of the ring, as a fraction of half the quad. 0.55 leaves room for the wisps |
| `BandWidth` | 0.03 to 0.25 | Width of the bright band, same unit |
| `Turbulence` | 0 to 1 | 0 is a round ring, 1 is a lumpy cloud edge |
| `Streaks` | 0 to 1 | How far the wisps reach outward |
| age (per draw) | 0 and up | Seconds the noise has run |
| fade (per draw) | 0 to 1 | Overall opacity |

Out of range values are clamped. A NaN or an infinity becomes the default, so one bad value cannot blank a ring.

### Per-instance layout

Two `float4` per ring, read in the vertex stage:

- `_ParamA` = (age, fade, ring radius, band width)
- `_ParamB` = (seed value, palette row, turbulence, streaks)

The quad is one unit wide before the matrix scales it, so the matrix scale is the drawn diameter in metres.

### Look and size

- Seen in 2D renders of the shader math (Fire, Water, Arcane, several seeds and ages): the ring is a lumpy bright band with a dark hole and outward wisps. At `Turbulence` 0 and `Streaks` 0 it is a clean glowing circle. At a ring radius below about 0.3 the hole fills with radial streaks and the ring reads as a starburst, which suits a burst that is just starting.
- Blending is additive by default (the glow adds to what is behind it), which suits a dark scene and a spell. On a bright sky an additive ring washes out, so `Additive` can be switched off and the ring then blends with its alpha.

### Cost

- Per pixel: 15 value noise evaluations (two flow fields at three octaves each, the edge, the wisps and the sparkle, three octaves each) and about 120 hashes. Pixels outside the ring's disc (about a quarter of the quad) are discarded before any noise.
- `MaxDrawsPerFrame` is 16. A ring is a full quad of pixel work, so many large rings close to the camera are the expensive case.
- Not measured: GPU time on any machine.

## Impact burst

A burst is one ring that starts small and thick, expands fast and slows down (radius follows an ease-out curve), thins, and fades out. It lies flat on the surface that was hit, or faces the camera.

| Value | Start | End | Shape over the burst's life `u` (0 to 1) |
|---|---|---|---|
| Ring radius | 0.2 | 0.7 | `1 - (1 - u)^3` blend, so it covers most of the distance in the first third |
| Band width | 0.16 | 0.05 | straight line, thick to thin |
| Streaks | template value | 60 percent of it | straight line |
| Fade | 0 | 0 | fades in over the first 6 percent of life, full until 55 percent, then a smooth fall to 0 |
| Noise age | 0 | 2 x real seconds | `NoiseSpeed` 2, so a half-second burst still churns |

- **Size.** The quad is `2 x blastRadius x SizeScale / EndRadius` metres wide, so the ring's edge ends at about 0.9 of the blast radius (3 m fireball: a 7.7 m quad). Clamped between `MinDiameter` (1.5 m) and `MaxDiameter` (40 m). A blast radius that is NaN, infinite or negative counts as 0, which gives the minimum. The view's `Size Scale` Inspector value multiplies it.
- **Per-spell look** (`ManagedPlasmaSpellTable`, in code for now, like the other spell tables):

| Spell | Palette | Seconds | Blast radius used |
|---|---|---|---|
| Fireball | Fire | 0.6 | `ManagedSpellDefinition.ImpactRadius` (3) |
| Ice | Water | 0.7 | the spell has none (0), so a fallback of 1.8 |
| Seeker | Arcane | 0.55 | `ManagedSpellDefinition.ImpactRadius` (2.5) |
| Unknown kind | Fireball's style | | Fireball's |

- **Every burst looks different.** The tracker copies the template spec and gives each burst `template.Seed + serial`, so two hits of the same spell never share a pattern. The template is never changed.
- **Limits.** At most `MaxBursts` (view default 8, hard cap 32) are alive. A new one past the limit replaces the oldest. The renderer's per-frame cap is raised to the hard cap by the view. A burst with no template, a length of 0 or less, or a position that is NaN or infinite is refused.
- **Surface.** The ring is moved `SurfaceOffset` (0.08 m) along the normal so it does not sit inside the wall. A zero or NaN normal means no offset and the ring faces the camera.
- **One view serves every caster.** `ManagedSpellCaster.Raise` is the path NPC mage spells take, so one `ManagedPlasmaBurstView` in the scene covers all of them. A second view gets its own copy of every burst.

## Drawing

Same contract as the glyph renderer.

- **Instanced path (primary).** `Flush` sends every queued ring in one `Graphics.DrawMeshInstanced` call (at most 1023 per call, `InstanceBatchSize`). All rings share one quad, so no sorting is needed. The matrices go as one array, `_ParamA` and `_ParamB` as `Vector4[]` through `MaterialPropertyBlock.SetVectorArray`. The arrays keep a constant size, because Unity rejects a property array that changes size.
- **DrawMesh fallback.** When `SystemInfo.supportsInstancing` is false, or the caller forces it, `Flush` sends one `Graphics.DrawMesh` per ring with a property block (`SetVector`).
- **Missing shader.** Unlike the glyphs there is no `Sprites/Default` stand-in: a ring without its shader has nothing to show. The renderer logs one error and draws nothing.
- **Contract.** Every `Draw` must be followed by a `Flush` in the same frame. A queue that was not flushed is dropped on the next frame, with one warning.

## Modules

### `ManagedPlasmaTypes.cs`

`ManagedPlasmaPalette` (Fire 0, Water 1, Arcane 2, the numbers are the ramp rows), `ManagedPlasmaLimits` (palette count, radius and band limits, seed modulus) and `ManagedPlasmaRingSpec`, the serializable record of one ring's look. `PackA(age, fade)` and `PackB()` build the two shader vectors with the clamping described above. `SeedValue` folds the seed. `RowFor(palette)` gives the middle of the palette's row as a v coordinate, and an unknown palette uses row 0. `Clone()` copies it. No Unity objects, so the CI harness uses it.

### `ManagedPlasmaRamp.cs`

The color ramps. Each palette is five stops (black, deep, mid, bright, white hot) with black at brightness 0, so an additive ring fades into the scene. `Evaluate(palette, brightness)` blends between the neighbouring stops and clamps. `Bake()` fills a `Width` (128) by `PaletteCount` (3) `Color32` array, row 0 first, which is the texture the renderer uploads. Every texel is opaque: brightness is the only thing that varies, and the ring's alpha comes from the shader. The colors were chosen by eye to match the glyph palettes' hues and are placeholders.

### `ManagedPlasmaRenderer.cs`

Builds the shared quad, the ramp texture (`Bilinear`, `Clamp`, so rows never blend into each other) and the material, and draws through the queue described under "Drawing". Constructor `(layer, forceDrawMesh, label)`. `Draw(spec, matrix, age, fade)` queues and returns false when the per-frame cap refused the ring or nothing can draw. A fade of 0 is accepted and not drawn. `Flush()` sends. `Path`, `CanDraw`, `PlasmaMaterial`, `DrawsThisFrame` and `LastFlushCalls` are read-only. `Additive` (default true) sets the material's `_DstBlend` to One or OneMinusSrcAlpha. `Intensity` (0 to 4) is a brightness multiplier on the material. `Dispose` destroys the material, the quad and the ramp texture.

### `ManagedPlasmaRing.shader`

Hand-written URP unlit shader `MidManStudio/Gtg/PlasmaRing`, found by name like the glyph shader. Properties: `_RampTex`, `_Intensity` and `_DstBlend`. The per-ring values are per-instance (`UNITY_INSTANCING_BUFFER_START(Props)`), read in the vertex stage and passed on in `TEXCOORD1` and `TEXCOORD2`. The noise is value noise on a hash with no `sin()`, so it gives the same pattern on every GPU. Like the glyph shader it is not SRP Batcher compatible, which is accepted because rings are drawn through instancing and property blocks. `ZWrite Off`, `Cull Off`, queue Transparent.

### `ManagedPlasmaPreview.cs`

A test component that draws one ring at its own transform. Seed, palette, ring radius, band width, turbulence and streaks, the quad diameter in metres, animate (or hold at an age), fade, additive, intensity, face camera and a force-DrawMesh switch are in the Inspector. `DescribeStats()` returns one line: the shape, the draw path, the draw calls of the last frame and the blend mode. The renderer is rebuilt only when the force-DrawMesh switch changes.

### `ManagedPlasmaBurstTracker.cs`

Plain values, no Unity objects, so the CI harness drives it frame by frame. `Add(template, position, normal, blastRadius, seconds)` starts a burst and returns false when it is refused. `Update(dt, list)` advances every burst, fills the list with the live ones (oldest first, as `Item {Spec, Position, Normal, Diameter, Age, Fade}`) and removes the ones whose time is up. A negative `dt` counts as 0. `DiameterFor(blastRadius)`, `Clear()`, `Count`. The curves and limits are public fields (`StartRadius`, `EndRadius`, `StartBand`, `EndBand`, `FadeStart`, `FadeInFraction`, `SizeScale`, `MinDiameter`, `MaxDiameter`, `NoiseSpeed`, `SurfaceOffset`, `MaxBursts`) with the values in the table above. `Item.Spec` belongs to the tracker and changes every frame, so draw it at once and do not keep it.

### `ManagedPlasmaSpellTable.cs`

`For(kind)` returns the `Style` (template spec, seconds, fallback radius) of a spell, never null: an unknown kind gets the Fireball style. `BlastRadius(kind)` is the spell definition's `ImpactRadius` when it is above 0, otherwise the style's fallback, so the burst follows the spell tuning. This is the only plasma file that knows spell kinds, besides the view.

### `ManagedPlasmaBurstView.cs`

A component to put once in the scene. It subscribes to `ManagedSpellCaster.Impact` in `OnEnable` (taking the handler off first, so enabling twice never listens twice) and unsubscribes in `OnDisable`, which also drops the bursts and frees the renderer. `Spawn(kind, position, normal)` starts a burst by hand (the lab uses it). Every `LateUpdate` with a live burst builds the renderer once, advances the tracker by `Time.deltaTime`, queues every burst and sends one `Flush`. A frame with no burst does nothing and builds nothing. Inspector: `Size Scale` (0.3 to 3), `Intensity` (default 1.2), `Additive`, `Face Camera`, `Max Bursts` (1 to 32), `Force Draw Mesh` (tests the fallback) and `Log Bursts`. `Describe()` returns bursts alive, started and refused, the draw path, the draw calls of the last frame and the blend mode. `FacingRotation(direction)` turns the ring's Z axis along a direction; for a direction nearly straight up or down, which is every hit on flat ground, it passes a sideways reference to `LookRotation` instead of relying on how Unity treats a parallel reference.

### `ManagedPlasmaLab.cs`

Editor only. Menu `GTG, Plasma` (it uses `GtgLabKit` like the other labs):

| Menu | Does |
|---|---|
| Ensure Preview At Scene View | Adds a `ManagedPlasmaPreview` at the scene view pivot |
| New Seed On Selected Preview | Picks a new seed |
| Cycle Palette On Selected Preview | Fire, Water, Arcane |
| Toggle Additive Blend On Selected Preview | Additive or alpha |
| Print Plasma Stats | `DescribeStats()` |
| Ensure Burst View (Rings At Spell Impacts) | Adds the `GTG Plasma Bursts` object with a `ManagedPlasmaBurstView` |
| Test Fireball / Ice / Seeker Burst At Scene View (Play) | Starts one burst of that spell at the scene view pivot, flat on the ground, without casting |
| Print Burst View State (Play) | `Describe()` of the burst view |

## Known gaps

- **Behind the glyph is not built.** The ring behind the cast glyph is the next piece. It will be another small view like the burst view.
- **Burst look is untuned.** Start and end radius, band, fade times, the 0.6 second lengths, the intensity of 1.2 and the Ice fallback radius of 1.8 are first guesses. They are public fields on the tracker, the style table and the view, so they can be tuned without touching logic.
- **Hits on a wall.** A burst lies flat on whatever surface it hit, so on a wall it is a ring on the wall, seen from the side at a shallow angle. `Face Camera` turns every burst to the camera instead. There is no per-spell choice yet.
- **Ice has no blast radius.** Its spell definition has an impact radius of 0, so the burst uses the table fallback instead of a real damage area. If ice gets a radius later, the burst follows it.
- **Unsorted.** Bursts are additive and unsorted. Several overlapping in alpha mode may blend in an odd order.
- **No flame strip.** The reference graph also produced the unwrapped strip (the polar to cartesian output) for use on a cylinder or a trail. The ring here is computed in polar form directly, so the strip is not made. It would be a second shader mode if it is wanted.
- **Faces one direction.** The ring faces the object's local Z, or the camera with `Face Camera` on the preview. The caller's matrix decides, like the glyph.
- **Age precision.** The noise coordinates grow with age. A ring that runs for many minutes loses precision. Effects last seconds, so this is not handled.
- **Additive on a bright background.** It washes out. The alpha mode is there for that.
- **Small rings.** Below a ring radius of about 0.3 the middle fills with radial streaks (see Look and size).
- **Always Included Shaders.** `Shader.Find` finds the plasma shader in a build only if it is referenced or listed under Always Included Shaders, like the glyph shader.
- **Colors are placeholders**, picked by eye.

## Not verified

- **Unity.** Nothing here has run in the Unity Editor: the shader compiling, the look under URP, the per-instance vector arrays with `Graphics.DrawMeshInstanced`, the ramp texture upload, the editor menus, and how a burst reads on screen (size, speed, brightness at the hit).
- **Real impacts.** The view was driven in the harness through `ManagedSpellCaster.Raise`, which is what an NPC spell uses. A real fireball, ice shard and seeker hitting a wall or an NPC in Play mode have not been tried.
- **What was checked instead (not Unity).** The harness compiles the types, ramps, renderer and preview against hand-written stand-ins and checks the packing and clamping, the ramp, the draw calls and the per-instance arrays, not pixels. The shader's HLSL was syntax and type checked with a Slang compiler with the Unity macros stubbed, and its body was run on the CPU through Slang and compared with a NumPy copy of the same math: the two agree except for about 0.9 percent of pixels that differ by more than 0.05 (float32 against float64 at noise cell edges). The pictures used to judge the look come from that copy.
- **Frame cost.** Per-pixel counts are above. GPU time is not measured.

## CI

`building-test/TestsPlasmaBurst.cs` (called from `PlasmaTests()`) checks the burst: one burst's start values, its curves at half time and frame by frame (radius never shrinks, band never thickens, both stay inside the ring limits, fade rises then only falls, removal at the end), the template left untouched, normals (zero, NaN, long), frame time edge cases, the quad size (minimum, maximum, NaN, infinite, zero end radius), the refusals, the limit and the seeds, oldest-first order, separate clocks, a soak of 400 frames, the spell table (palettes, lengths, unknown kind, radius from the spell definition or the fallback), and the view (nothing drawn without a burst, one call for three spells with their own palettes, placement and size of the quad, flat on the surface, facing the camera, the rotation helper for up, down and sideways, the run out, refusals, the view cap, the DrawMesh fallback and back, blend, disable and re-enable, two views). Three mutations were tried to confirm the checks bite: removing the fade out, dropping the newest burst instead of the oldest, and not taking the handler off before subscribing.

`building-test/TestsPlasma.cs` adds the checks: the spec (packing, clamping, NaN handling, seed folding, palette rows, cloning), the ramps (stop order, black at 0, near white at 1, never darker as brightness rises, hues, the bake and its row alignment) and the renderer (instanced path, per-instance arrays, batching past 1023, the per-frame cap, the stale queue, blend and intensity, dispose, the DrawMesh fallback, no hardware instancing, a missing shader). The Editor lab is excluded from the harness by the `Managed/**/Editor/**` exclusion. It was compiled once on the side against stand-ins for `UnityEditor` and for the `GtgLabKit` members it calls (same signatures as on origin), which checks the calls into the plasma and magic code. It has not run. `building-test/UnityStubs.cs` gained `Material.SetTexture`, `Texture2D` pixel storage, `MaterialPropertyBlock.SetVector` and `SetVectorArray` that store their values, those values on each recorded draw call, `UnityEngine.Rendering.BlendMode` and the one-argument `Quaternion.LookRotation`.

## Fixes and Problems

### `ManagedPlasmaRing.shader`

- **Starburst in small rings.** The first 2D renders showed radial streaks converging in the middle of a ring with a radius under about 0.35. Making the inner glow steeper and wisp-free, and fading everything toward the center, reduced it. It is still visible below about 0.3 and is left as a look.
- **Seamless ring.** Noise is sampled on the unit circle direction, not on the angle, so there is no seam where the angle wraps.

### `ManagedPlasmaBurstTracker.cs`

- **NaN blast radius gave a NaN quad.** `Mathf.Max(0, NaN)` returns NaN, so a blast radius of NaN produced a quad of NaN width. Found by a test. `DiameterFor` now treats NaN, infinite and negative radii as 0 and clamps the result between `MinDiameter` and `MaxDiameter`.

### `ManagedPlasmaBurstView.cs`

- **Looking straight up.** Every hit on flat ground has a normal of `Vector3.up`, so `LookRotation(up)` with the default up reference is a parallel case. `FacingRotation` picks a sideways reference for near-vertical directions, and a test checks that up, down and sideways all give a rotation whose Z axis points along the direction.

### `TestsPlasma.cs`

- **Float rounding.** The check that every ramp ends near white asked for 0.8 and Fire's last color is 0.8, which came back a hair under 0.8 after blending. The check now allows 0.79.
