# Specter butterflies (`MidManStudio.Gtg.Managed.Specter`)

> Code location: `Assets/MidManStudio/Gtg/Managed/Specter/`. The editor lab is in `Assets/MidManStudio/Gtg/Managed/Specter/Editor/`. This is the generic effect only: one shader, two ways to move the butterflies (code-moved quads and the built-in Particle System) and test tools to compare them. Nothing in the game spawns a butterfly yet.

## Overview

A specter butterfly is a soft glowing shape with two mirrored wings, a bright core and rays that reach outward, colored by a ramp. Its pattern changes as it ages, so it seems to breathe, and it fades out at the end of its life. It is drawn on a flat quad that faces the camera.

- **One shader, no textures to render first.** The reference was a Blender shader graph. Here each step of it is a few lines of math in one fragment shader. There is no render texture and no CPU work per pixel.
- **Per-spell palettes.** Fire, Water and Arcane are the plasma palettes (`ManagedPlasmaPalette`, one row of the plasma ramp texture). A spell can give its plasma ring and its butterflies the same colors.
- **Two motion variants, same look.** Variant A moves the quads in code and draws them with `Graphics.DrawMeshInstanced`, with `Graphics.DrawMesh` as the fallback (`ManagedSpecterSwarm`). Variant B is the built-in Particle System with the same shader, reading each particle's age and random number from custom vertex streams (`ManagedSpecterParticles`). They are both kept so the two can be compared in the editor and one dropped later.
- **Own folder, one-way dependency.** Specter references `Managed/Plasma/` for the palette enum, the ramp, the palette row lookup and the draw path enum. It references nothing else. Plasma knows nothing about specter.

## Design notes

### The reference graph and what replaced each node

The reference is a Blender shader node graph (a tutorial, seen as screenshots). It builds a polar coordinate, folds it so the left and right halves mirror, offsets it with the object's age and a random value, feeds it to a noise texture, uses the noise to bend a radial gradient, then turns the gradient into a bright core with a long soft tail through a log based falloff node group, and colors that with a ramp. Bloom in the compositor makes the glow.

| Graph step | Shader step |
|---|---|
| Polar coordinates, folded | `fold = 0.5 - atan2(abs(p.x), p.y) * 0.15915494`, which is 0.5 straight up and 0 straight down, the same on both sides, so the butterfly is mirrored. `rt = rq * reach * 0.4`, where `rq` is the distance from the quad center, 0 to 1 |
| Time offset, random offset | `v = (fold, rt, 0) + life * 4 * churn * (0.1, -0.1, -0.05) + seed`. The lookup slides with life, so the pattern changes as the butterfly ages, and each butterfly looks somewhere else in the noise |
| Noise Texture (3D, fBM, detail 2, distortion) | `SpecterNoise`: value noise, three octaves, one more noise that bends the lookup. Scaled `(5, 3.7, 10)`, then a contrast of `(n - 0.5) * 1.5 + 0.5` |
| Gradient bent by the noise | `rr = rt + n * wobble`, `g = 1 - smoothstep(-0.07, 1.5, rr)` |
| Life fade | `g -= 0.12 * smootherstep(0.75, 1, life)`: in the last quarter of the life the gradient drops, so the butterfly fades away |
| Light falloff node group | `e = glow * 15 * log(0.999) / log(clamp(g, 1e-4, 0.999))`, about `0.001 / (1 - g)`: a bright core with a long soft tail |
| Compositor Bloom | The first 0.045 of `e` is cut off so the tail ends inside the quad, then `Intensity` multiplies the color. Above 1 the color is HDR and the URP Bloom spreads it (see "Setup") |
| Color ramp | A lookup in the plasma ramp texture, brightness across and palette down |

What was **not** copied: the tutorial's iridescent rainbow color (three groups the tutorial calls "specter"). Per-spell palettes were chosen instead. It could come back later as a fourth palette mode. The inside of the "specter" group and of the falloff group were only partly visible in the screenshots, so those two parts, the constants `0.045`, `0.4` and `2.5` and the default wobble of 0.3 (the tutorial used 0.17) were tuned by eye on renders of the shader math. Expect to tune them in Unity.

### Parameters

| Field | Range | Effect |
|---|---|---|
| `Seed` | any int | Picks the pattern. Folded into 0 to 250 and scaled by 0.37, so a huge seed cannot push the noise out of float range |
| `Palette` | Fire, Water, Arcane | Which row of the ramp |
| `Wobble` | 0 to 0.6 | How much the noise bends the shape. 0 is a smooth round glow, higher is more wing-like rays |
| `Glow` | 0.1 to 4 | Brightness multiplier of the falloff |
| `Churn` | 0 to 3 | How fast the pattern changes as the butterfly ages. 0 freezes it |
| `Reach` | 0.5 to 3 | Above 1 the shape shrinks inside the quad, below 1 it grows |
| life (per draw) | 0 to 1 | Age as a fraction of the lifetime. Drives the pattern and the fade out |
| fade (per draw) | 0 to 1 | Overall opacity |
| `Intensity` (per material) | 0 to 8, default 2 | HDR brightness multiplier |

Out of range values are clamped. A NaN or an infinity becomes the default (life 0, fade 1, wobble 0.3, glow, churn and reach 1), so one bad value cannot blank a butterfly.

### Per-instance layout

Two `float4` per butterfly:

- `_ParamA` = (life, fade, seed value, palette row)
- `_ParamB` = (wobble, glow, churn, reach)

On the code path they are per-instance property values (`UNITY_INSTANCING_BUFFER_START(Props)`), read in the vertex stage. On the Particle System path the same numbers come from elsewhere, selected by the keyword `_SPECTER_STREAMS`:

| Value | Code path | Particle System path |
|---|---|---|
| life | `_ParamA.x` | the `AgePercent` vertex stream |
| fade | `_ParamA.y` | the particle's color alpha, so Color over Lifetime and the start color work |
| seed | `_ParamA.z` | the `StableRandomX` vertex stream times 92 (about the 251 x 0.37 range of a spec seed) |
| palette row, wobble, glow, churn, reach | `_ParamA.w`, `_ParamB` | the material's `_PaletteRow`, `_Wobble`, `_Glow`, `_Churn`, `_Reach`, the same for every particle of that material |

The Particle System path takes its streams in this order: Position, Color, UV, AgePercent, StableRandomX. Unity packs consecutive float streams into texture coordinate slots, so UV is `TEXCOORD0.xy`, AgePercent `TEXCOORD0.z` and StableRandomX `TEXCOORD0.w`. The keyword is `multi_compile_local _ _SPECTER_STREAMS`, not `shader_feature_local`, so a material made at run time still gets the variant in a build.

### Look and size

- Seen in 2D renders of the shader math (Fire, Water, Arcane, several seeds, wobbles and lives): a bright core with soft mirrored wings and rays that move as the life runs, dimming over the last quarter of the life. At wobble 0 it is a plain round glow.
- The quad is one unit wide before the matrix scales it. The swarm scales the quad's width by the wing flap, so a flap is the quad narrowing and opening again. The shape ends inside the inscribed circle of the quad.
- Blending is additive by default (the glow adds to what is behind it). `Additive` can be switched off and the butterfly then blends with its alpha, which suits a bright background.

### Cost

- Per pixel: one `SpecterNoise`, which is 4 value noise evaluations (32 hashes), one `atan2`, one `log`, one ramp sample. Pixels outside the quad's inscribed circle (about 21 percent of the quad) are discarded before any noise. This is well under the plasma ring's cost (15 noise evaluations).
- Variant A: one `DrawMeshInstanced` call per 1023 butterflies. Variant B: one Particle System draw for all particles of the system.
- Not measured: GPU or CPU time on any machine.

## Setup

The glow needs bloom. Without it the butterfly is a flat soft shape, a lot dimmer than intended.

1. In the URP asset, turn on **HDR**.
2. Add a **Bloom** override to a global Volume. Threshold around 1, Intensity 0.5 to 1.5 to start.
3. Keep `Intensity` on the butterflies at 1 or more. Above 1 the colors go over 1 and the bloom spreads them.

The shader is found by name (`MidManStudio/Gtg/SpecterButterfly`). In a build, keep it referenced by a material in a scene or list it under Always Included Shaders, like the glyph and plasma shaders.

## The two variants

| | A: code-moved (`ManagedSpecterSwarm`) | B: Particle System (`ManagedSpecterParticles`) |
|---|---|---|
| Motion | `ManagedSpecterFlock`: smooth wandering push, drag, lift, a speed limit, all in plain C# | The Noise module for the wander, negative gravity for the rise, start speed for the first push |
| Flap | Per-butterfly flap phase, the quad's width scaled | Size over lifetime, width only, a random pair of curves a quarter flap apart so they do not flap in step |
| Draw | `Graphics.DrawMeshInstanced`, `Graphics.DrawMesh` fallback | The Particle System renderer, Billboard mode |
| Life, fade, seed | Per-instance vectors | Vertex streams and the color alpha |
| Per-butterfly look | Every butterfly has its own palette, wobble, glow, churn, reach | One look for the whole system (one material) |
| Control in code | Full: a butterfly is a value the caller owns | Emit and the module settings |
| Cost | One call per 1023 | One draw, simulation on the Particle System's own jobs |

Both are driven by the same Inspector values where they overlap, so they can be set to match and compared side by side.

## Modules

### `ManagedSpecterTypes.cs`

`ManagedSpecterLimits`, `ManagedSpecterSpec` (seed, palette, wobble, glow, churn, reach, with `PackA(life, fade)`, `PackB()`, `SeedValue` and `Clone()`), `ManagedSpecterFlap` and `ManagedSpecterFade`. `ManagedSpecterFlap.WidthScale(phaseCycles, min)` gives the width scale of a flap (1 is wings open, `min` is nearly closed), and `BuildCurveKeys(cycles, min, phaseOffset, out times, out values)` gives the key times and values for the Particle System's size over lifetime (8 keys per cycle, cycles clamped to 1 to 40). `ManagedSpecterFade.At(ageSeconds, life, fadeInSeconds, fadeOutFraction)` is the fade in and the smooth fade out the flock and the preview share. No Unity objects, so the CI harness uses it.

### `ManagedSpecterButterfly.shader`

Hand-written URP unlit shader `MidManStudio/Gtg/SpecterButterfly`. Properties: `_RampTex`, `_Intensity`, `_DstBlend`, the particle path's `_PaletteRow`, `_Wobble`, `_Glow`, `_Churn`, `_Reach`, and the toggle `_UseStreams` that sets the keyword. The fragment stage is the math in the table above. The noise is value noise on a hash with no `sin()`, so it gives the same pattern on every GPU. Like the plasma shader it is not SRP Batcher compatible (`_DstBlend` and `_UseStreams` are outside the per-material buffer), which is accepted because butterflies are drawn through instancing and property blocks or through the Particle System. `ZWrite Off`, `Cull Off`, queue Transparent.

### `ManagedSpecterAssets.cs`

The shared pieces: `ShaderName`, `StreamsKeyword`, `FindShader()` (null when missing or unsupported), `BuildQuad()`, `BuildRamp()` (the plasma ramp bake, bilinear and clamped so rows never blend) and `DestroyObject()`.

### `ManagedSpecterRenderer.cs`

Draws the code-moved butterflies. The queue contract is the plasma renderer's: `Draw(spec, matrix, life, fade)` queues and returns false when the per-frame cap refused it or nothing can draw (a fade of 0 is accepted and not drawn), `Flush()` sends once a frame. Instanced primary (`InstanceBatchSize` 1023, constant-size `Vector4[]` scratch arrays through `SetVectorArray`), `DrawMesh` fallback with `SetVector` when `SystemInfo.supportsInstancing` is false or the caller forces it. `Additive`, `Intensity` (0 to 8, default 2), `Path`, `CanDraw`, `SpecterMaterial`, `DrawsThisFrame`, `LastFlushCalls`, `MaxDrawsPerFrame` (default 64), `Dispose()`. A missing shader logs one error and draws nothing. A queue not flushed in its frame is dropped on the next frame with one warning.

### `ManagedSpecterFlock.cs`

Plain values, no Unity objects, its own xorshift32 generator (so a test run is repeatable and it never touches `UnityEngine.Random`). `Spawn(template, center, count, radius, lifetime, size)` adds butterflies inside a sphere, each on its own clone of the template with a seed that counts up from the template's seed, and returns how many were added. It adds none for a missing template, a count, lifetime or size of 0 or less (or NaN, or infinite) or a center that is not a number. Past `MaxButterflies` (default 32, hard cap `HardMaxButterflies` 256) the oldest are replaced. `Update(dt, list)` advances every butterfly with a wandering push of three slow sines, drag, lift and a speed limit, in steps of at most `MaxStep` (0.1 s), and fills the list with the live ones, oldest first (`Item {Spec, Position, Diameter, WidthScale, Life, Fade}`). A butterfly whose time is up is removed in that call and not returned. A negative or NaN `dt` counts as 0. `Item.Spec` belongs to the flock, so draw it at once and do not keep it. Public fields: `MaxButterflies`, `Wander`, `Drag`, `Lift`, `MaxSpeed`, `FlapRate`, `FlapMin`, `FadeInSeconds`, `FadeOutFraction`, `MaxStep`. `Reseed(int)`, `Clear()`, `Count`.

### `ManagedSpecterSwarm.cs` (variant A)

A component. `Burst(count)` and `Burst(position, count)` release butterflies, `autoPerSecond` releases them on its own (the fractional remainder carries between frames). Every `LateUpdate` with a live butterfly builds the renderer once (again when `Force Draw Mesh` changes), advances the flock by `Time.deltaTime`, queues a camera-facing quad per butterfly scaled `(Diameter * WidthScale, Diameter, Diameter)` and sends one `Flush`. A frame with no butterfly builds and draws nothing. Inspector: palette, seed, wobble, glow, churn, reach, intensity, additive, size (default 0.7 m), lifetime (3 s), spawn radius, max butterflies, auto rate, flap rate, wander, lift, force DrawMesh and log bursts. `Describe()` returns one line. `FacingRotation(direction)` points the quad's Z axis along a direction, with a sideways reference for near-vertical directions.

### `ManagedSpecterParticles.cs` (variant B)

A component that builds a child object (`GTG Specter Particles`) with a `ParticleSystem` when it enables, and removes it when it disables. The child exists only in Play mode, so no scene is dirtied. The look and motion values are on this component, not on the child: the child is set up again from them whenever a value changes (`OnValidate`, or the palette and additive setters), on the next `Update`. Settings: looping, not play on awake, world space, start lifetime 75 to 125 percent of `lifetime`, start size 80 to 120 percent of `size`, start speed 30 to 100 percent of `startSpeed`, `gravityModifier = -rise`, sphere shape, the Noise module (`flutter`, frequency 0.6, scroll 0.4), size over lifetime with separate axes (width is the flap, a random pair of curves a quarter flap apart, height is 1), color over lifetime alpha (in over the first 6 percent, out over the last 12), Billboard renderer with shadows off and the five vertex streams. `Burst(count)` and `Burst(position, count)` emit through `ParticleSystem.Emit` with the shape applied, and take only as many as fit under `Max Particles`. `AliveCount`, `SpawnedCount` (Burst only), `Particles`, `SpecterMaterial`, `Describe()`.

### `ManagedSpecterPreview.cs`

A test component that draws one butterfly at its own transform, through the same renderer as variant A. Seed, palette, wobble, glow, churn, reach, the quad height in metres, animate (the life loops over `Loop Seconds`) or hold at `Life`, flap, fade, additive, intensity, face camera and a force-DrawMesh switch are in the Inspector. `ExecuteAlways`, so it draws in Edit mode when Unity runs the scene update. `DescribeStats()` returns one line.

### `ManagedSpecterLab.cs`

Editor only. Menu `GTG, Specter` (it uses `GtgLabKit` like the other labs):

| Menu | Does |
|---|---|
| Ensure Single Butterfly Preview At Scene View | Adds a `ManagedSpecterPreview` at the scene view pivot |
| New Seed On Selected Preview | Picks a new seed |
| Cycle Palette On Selected (Fire, Water, Arcane) | On a preview, swarm or particles object |
| Toggle Additive Blend On Selected | Additive or alpha, on a preview, swarm or particles object |
| Ensure Swarm At Scene View (A: Code-Moved Quads) | Adds a `ManagedSpecterSwarm` with an auto rate of 3 a second |
| Ensure Particles At Scene View (B: Particle System) | Adds a `ManagedSpecterParticles` with an auto rate of 3 a second |
| Ensure Both Side By Side (A Left, B Right) | Both, 3.2 m apart, both selected |
| Burst Selected, Or Both If Nothing Selected (Play) | Releases 12 butterflies from every selected swarm or particles object, or from the first of each in the scene |
| Print Specter State | `Describe()` of each specter object in the scene |

## Known gaps

- **Not hooked to anything.** No spell, NPC or event spawns a butterfly. Spawning on a spell, a death or a pickup is the next step, once the look is liked.
- **No iridescence.** The tutorial's rainbow spectrum is not built (see the table above).
- **The look is tuned by eye.** The tail trim, the `0.4` radius scale, the `2.5` alpha gain, the default wobble and the pattern speed came from renders of the shader math and the reading of part of the tutorial, not from Unity. They are in the shader and in `ManagedSpecterLimits` and the Inspector defaults.
- **Particle variant is one look per system.** The Particle System path shares palette, wobble, glow, churn and reach across every particle of one material. A second palette needs a second `ManagedSpecterParticles`.
- **Particle flap is approximate.** Each particle takes a random blend of two curves a quarter flap apart. The blend of two flaps is a flap of a smaller depth, so some butterflies flap a little shallower than others.
- **Unsorted.** Butterflies are additive and unsorted. Several overlapping in alpha mode may blend in an odd order.
- **Camera facing.** Both variants face the camera. A butterfly that should lie on a surface is not supported.
- **Age precision.** Life is a 0 to 1 fraction, so a long lifetime loses no precision. The noise coordinates stay small: life adds at most 12 and the seed at most about 93.
- **Always Included Shaders.** `Shader.Find` finds the shader in a build only if it is referenced or listed, like the glyph and plasma shaders.
- **Colors are the plasma placeholders**, picked by eye.

## Not verified

- **Unity.** Nothing here has run in the Unity Editor: the shader compiling under URP, the look and the bloom, the per-instance vector arrays with `Graphics.DrawMeshInstanced`, the ramp upload, the editor menus, and above all the Particle System path. Not checked in Unity: that the five vertex streams are accepted for this shader, that AgePercent and StableRandomX land in `TEXCOORD0.z` and `.w` (derived from Unity's packing rule in the documentation, not seen), how the Billboard renderer applies separate-axis size over lifetime to the width, how `Emit` behaves with the shape applied in world space, and the Noise module's look.
- **What was checked instead (not Unity).** The shader's fragment stage was run on the CPU through a Slang compiler with the Unity macros stubbed (the ramp replaced by a grey ramp of brightness), 18 checks over the image: finite output, left and right halves mirror, nothing outside the inscribed circle, alpha and brightness in 0 to 1, fade 0 draws nothing, the glow has ended before the quad edge, bright near the center, deterministic, seed and life change the picture, the last quarter of the life dims it and it ends smoothly, no NaN or infinity over a 648 case sweep of the parameters, glow, reach and wobble each change it the right way. The particle path's vertex stage compiled with the keyword on. The C# is run in the harness against hand-written Unity stand-ins (see "CI"), not in Unity.
- **Frame cost.** Per-pixel counts are above. GPU and CPU time are not measured.

## CI

`building-test/TestsSpecter.cs` (called from `PlasmaTests()`) checks: the spec (packing, clamping, NaN and infinity handling, seed folding, palette rows shared with plasma, cloning), the flap (width scale range, phase, curve keys: count, time order, minimum, cycle clamping, offset) and the fade, the assets, the renderer (instanced path with per-instance arrays and draw order, batching past 1023, the per-frame cap, the stale queue, blend and intensity, dispose, the DrawMesh fallback, no hardware instancing, a missing shader), the flock (refusals, seeds, the limit and the oldest replaced, determinism by reseed, a whole life frame by frame, NaN, negative and infinite frame times, a long frame capped to one step, lift), the swarm (the draw call, the layer, facing the camera, size and flap, palette per butterfly, a burst at a point, the limit, the DrawMesh fallback and back, blend and intensity reaching the material, the run out, the auto rate and its carry, disable, a missing shader, the rotation helper for up and down), the Particle System component against `building-test/ParticleStubs.cs` (the child, the five streams in order, the keyword and its toggle, every module value, bursts and the room limit, a value change applied on the next update, disable and enable again, a missing shader), the preview, and a source check that reads the `.shader` file and looks for every property name, the keyword, the pragma and the blend line the C# relies on (it is skipped with a note when the file cannot be found from the working directory).

Fifteen deliberate bugs were tried to confirm the checks bite, among them: the vertex streams swapped, the keyword not enabled, no cap on a burst, gravity with the wrong sign, a property not sent, no eviction, the same seed for every butterfly, no step cap, a wrong fade slope, a wrong flap cycle count, the wrong layer, the flock never updated, a ramp not clamped. Twelve made a check fail. Three (a property not sent, the preview life not looping, and the fallback's `_ParamA` not sent) stopped the run with an exception, which also catches them but is a blunter signal.

`building-test/ParticleStubs.cs` is new: `ParticleSystem` with its module types as structs that point at shared data (so `ps.main.loop = true` is a compile error here as in Unity, and a module copied into a variable still writes through), `MinMaxCurve`, `MinMaxGradient`, `EmitParams`, `ParticleSystemRenderer` with `SetActiveVertexStreams` and `GetActiveVertexStreams`, `AnimationCurve`, `Keyframe`, `Gradient`, the keys and the enums, with Unity's member names. It does not simulate particles: `Emit` only counts them up to Max Particles and records where they were asked for. `building-test/UnityStubs.cs` gained `Material` keyword methods, `Renderer.sharedMaterial` and `receiveShadows`, and an exact `==` on `Vector3`. The Editor lab is excluded from the harness by the `Managed/**/Editor/**` exclusion. It was compiled once on the side against stand-ins for `UnityEditor` and for the `GtgLabKit` members it calls, which checks its calls into the specter code. It has not run.

## Fixes and Problems

### `ManagedSpecterButterfly.shader`

- **A wide dim disc.** The first look showed the falloff's long tail as a dim disc across the whole quad, where the tutorial leaves that tail to the compositor's Bloom. The first 0.045 of the light is cut off, the radius is scaled by 0.4 and the default wobble is 0.3, and the shape now ends inside the quad.
- **Too dark.** The first pass was dim next to the plasma ring. The brightness and alpha gain of 2.5 fixed that. The check "bright near the center" had failed at an alpha of 0.41 before it.

### `ManagedSpecterTypes.cs`

- **`Mathf.Round` is not in the harness stand-ins** and returns a float. The flap cycle count uses `Mathf.RoundToInt` with a NaN guard.
- **NaN passes through `Mathf.Clamp`.** The packing guards NaN and infinity first, so one bad number cannot blank a butterfly.

### `ManagedSpecterParticles.cs`

- **A new Particle System plays at once with its default look.** Unity starts a system on `AddComponent` (play on awake is on by default), so the component stops it at once, sets it up and plays it again at the end. The system loops, so `Emit` always has a running system.
