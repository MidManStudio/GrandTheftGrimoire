# Plasma (`MidManStudio.Gtg.Managed.Plasma`)

> Code location: `Assets/MidManStudio/Gtg/Managed/Plasma/`. The editor lab is in `Assets/MidManStudio/Gtg/Managed/Plasma/Editor/`. Generic effect only: nothing in the game draws it yet. The impact burst and the ring behind the cast glyph are the next two pieces.

## Overview

A plasma ring is a glowing, turbulent ring with a dark hole, a bright lumpy edge and wisps that reach outward, colored by a ramp. It is drawn on a flat quad in the world.

- **One shader, no textures to render first.** The reference was a texture graph. Here every step of it is a few lines of math in one fragment shader, so there is no render texture and no CPU work per frame.
- **Live, not baked.** The noise runs with the ring's age, so the plasma keeps moving. Seed, radius, band width, turbulence, streaks and palette change the look per ring, and they are per-instance values.
- **Per-spell palettes.** Fire, Water and Arcane, the same three hues the glyph palettes use. A palette is one row of a small color ramp texture that the renderer bakes in code.
- **`Graphics.DrawMeshInstanced` is the primary path** with `Graphics.DrawMesh` as the fallback, like the glyphs. The two calls a caller uses are `Draw` and `Flush`.
- **Own folder, decoupled.** `Plasma/` references nothing in `Glyphs/` or `Magic/`. The hookups that tie a spell to a ring will live at the spell side, as the glyph spell table does.

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

### `ManagedPlasmaLab.cs`

Editor only. Menu `GTG, Plasma` (it uses `GtgLabKit` like the other labs):

| Menu | Does |
|---|---|
| Ensure Preview At Scene View | Adds a `ManagedPlasmaPreview` at the scene view pivot |
| New Seed On Selected Preview | Picks a new seed |
| Cycle Palette On Selected Preview | Fire, Water, Arcane |
| Toggle Additive Blend On Selected Preview | Additive or alpha |
| Print Plasma Stats | `DescribeStats()` |

## Known gaps

- **Generic only.** Nothing in the game draws a plasma ring. Next: a ring that bursts out where a shot hits, and a ring behind the cast glyph. Both will be a small tracker plus view, like the glyph cast view, and the spell side will name the palette.
- **No flame strip.** The reference graph also produced the unwrapped strip (the polar to cartesian output) for use on a cylinder or a trail. The ring here is computed in polar form directly, so the strip is not made. It would be a second shader mode if it is wanted.
- **Faces one direction.** The ring faces the object's local Z, or the camera with `Face Camera` on the preview. The caller's matrix decides, like the glyph.
- **Age precision.** The noise coordinates grow with age. A ring that runs for many minutes loses precision. Effects last seconds, so this is not handled.
- **Additive on a bright background.** It washes out. The alpha mode is there for that.
- **Small rings.** Below a ring radius of about 0.3 the middle fills with radial streaks (see Look and size).
- **Always Included Shaders.** `Shader.Find` finds the plasma shader in a build only if it is referenced or listed under Always Included Shaders, like the glyph shader.
- **Colors are placeholders**, picked by eye.

## Not verified

- **Unity.** Nothing here has run in the Unity Editor: the shader compiling, the look under URP, the per-instance vector arrays with `Graphics.DrawMeshInstanced`, the ramp texture upload, the editor menus.
- **What was checked instead (not Unity).** The harness compiles the types, ramps, renderer and preview against hand-written stand-ins and checks the packing and clamping, the ramp, the draw calls and the per-instance arrays, not pixels. The shader's HLSL was syntax and type checked with a Slang compiler with the Unity macros stubbed, and its body was run on the CPU through Slang and compared with a NumPy copy of the same math: the two agree except for about 0.9 percent of pixels that differ by more than 0.05 (float32 against float64 at noise cell edges). The pictures used to judge the look come from that copy.
- **Frame cost.** Per-pixel counts are above. GPU time is not measured.

## CI

`building-test/TestsPlasma.cs` adds the checks: the spec (packing, clamping, NaN handling, seed folding, palette rows, cloning), the ramps (stop order, black at 0, near white at 1, never darker as brightness rises, hues, the bake and its row alignment) and the renderer (instanced path, per-instance arrays, batching past 1023, the per-frame cap, the stale queue, blend and intensity, dispose, the DrawMesh fallback, no hardware instancing, a missing shader). The Editor lab is excluded from the harness by the `Managed/**/Editor/**` exclusion. `building-test/UnityStubs.cs` gained `Material.SetTexture`, `Texture2D` pixel storage, `MaterialPropertyBlock.SetVector` and `SetVectorArray` that store their values, those values on each recorded draw call, `UnityEngine.Rendering.BlendMode` and the one-argument `Quaternion.LookRotation`.

## Fixes and Problems

### `ManagedPlasmaRing.shader`

- **Starburst in small rings.** The first 2D renders showed radial streaks converging in the middle of a ring with a radius under about 0.35. Making the inner glow steeper and wisp-free, and fading everything toward the center, reduced it. It is still visible below about 0.3 and is left as a look.
- **Seamless ring.** Noise is sampled on the unit circle direction, not on the angle, so there is no seam where the angle wraps.

### `TestsPlasma.cs`

- **Float rounding.** The check that every ramp ends near white asked for 0.8 and Fire's last color is 0.8, which came back a hair under 0.8 after blending. The check now allows 0.79.
