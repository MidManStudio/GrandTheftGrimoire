# NPC Unity integration: status and audit

Status (2026-09-29): the native boundary is verified from C# against the real Rust library. The ECS loop around it is **not closed**, and nothing has been built or run inside Unity.

## What exists

| File (`Assets/MidManStudio/Gtg/NPC/`) | Role |
|---|---|
| `Components/NPCEnums.cs` | `NpcRole`, `DecisionBackend`, `LearningMode`, `NpcAction` (ABI v2 numbers) |
| `Components/NPCComponents.cs` | `NPCTag`, `NPCIdentity`, `NPCObservation`, `NPCDecision` |
| `Authoring/NPCAuthoring.cs` | Baker that adds the four components with default values |
| `Native/NPCNativeTypes.cs` | 32-byte observation and 16-byte decision structs, explicit layout |
| `Native/NPCNativeLib.cs` | P/Invoke, ABI version and size check, pinned-array batch call, warn-once fallback signal |
| `Native/NPCManagedFallback.cs` | Managed copy of the Rust decision, used when the native library is missing or rejects a batch (no Unity dependencies) |
| `Systems/NPCDecisionSystem.cs` | Every 0.1 s: up to 256 NPCs, one native batch call, writes `NPCDecision` |

## What one decision tick does

1. Every 0.1 s, `ToEntityArray` on the NPC query, then take up to 256 entities starting at a rotating cursor.
2. Copy identity and observation into a reused managed array. Health is clamped to 0..1 (NaN becomes 1). The NPC id is `Identity.Id`, or entity version and index when that is 0.
3. One `gtg_npc_decide_batch` call. If the library is missing, the ABI differs, or the batch is rejected, the managed fallback decides instead (one warning per session).
4. A decision is written only if its id matches the observation's and its action is 0..4.

## Verified, and how

| Claim | How it was checked | Where it runs |
|---|---|---|
| C# struct sizes (32/16) and every field offset match the Rust ABI | `Marshal.SizeOf` and `Marshal.OffsetOf` in the ABI test, plus the runtime size check in `NPCNativeLib` | CI job `csharp-abi` |
| `NPCNativeLib` can load the real `gtg_npc_ffi`, pass the ABI check, pin arrays and get correct results | 16384 mixed NPCs sent in batches of 256 through the real library | CI job `csharp-abi` |
| The managed fallback makes the same decision as Rust for valid input | Native and fallback compared on all 16384 NPCs, zero differences allowed | CI job `csharp-abi` |
| Rust, C and C# agree on the mixed workload | Same generator in all three; action histogram and FNV-1a hash of all decisions compared (`0xef210c5c4e3c6198` in the authoring sandbox) | Rust and C in the bench workflow; C# in `csharp-abi` |
| Bad input is rejected atomically, count 0 succeeds, count above 4096 throws | Same test | CI job `csharp-abi` |
| The changed Unity files (system, authoring, components, fallback) compile | Built once with .NET 8 against hand-written stubs of the Unity types | Authoring sandbox only, not CI |

The ABI test runs on plain .NET 8. It proves the boundary code and the P/Invoke marshalling. It does not prove Unity compiles the project, that ECS source generators accept the system, that Burst is happy, or that Unity's Mono/IL2CPP marshals the same way. IL2CPP on Android in particular is untested.

## Not verified

- A real Unity build, and Unity's ECS source generators on `NPCDecisionSystem`.
- Any run inside Unity, including the decision system's per-frame cost with `ToEntityArray` and per-entity `GetComponentData` at hundreds of NPCs.
- Native library packaging for any Unity target.

## Gaps that keep the loop open

1. **Nothing writes `NPCObservation`.** The baker sets `ThreatVisible = 0` and `HealthFraction = 1`, and no system changes them. Every NPC therefore sees no threat and full health, so the merchant always trades and the enemy always patrols.
2. **Nothing reads `NPCDecision`.** No system turns Idle/Trade/Patrol/Attack/Retreat into movement, animation, trading or combat.
3. **No health source exists.** The repository has no health or damage component outside the NPC folder, so `HealthFraction` has nothing to read.
4. **No native plugin is in the Unity project.** `Assets/` contains no `.so`, `.dll`, `.dylib` or `Plugins` folder, so in Unity today every call takes the managed fallback path after one warning. CI builds only Linux x86-64 debug and release libraries. Windows, macOS, Android arm64 and iOS builds and their Unity import settings do not exist yet.
5. **`NPCIdentity.Id` is always 0 from the baker.** Ids come from entity index and version, so they are not stable across sessions and cannot key persisted memory.
6. **`LearningMode` is stored but read by nothing.** **`archetypes.mdix` is not read by `NPCAuthoring`**, so archetype data does not reach entities.

## Decisions needed before closing the loop

These are game-design choices, not implementation details, so they are left open rather than guessed:

- **What makes `ThreatVisible` true?** Options include distance to the `CharacterTag` entity, line of sight through Unity Physics (`CharacterMovementSystem` already uses `PhysicsWorldSingleton`), or faction hostility. The character controller uses `LocalTransform`, so position is available.
- **Where does health come from?** A new health component, or the future combat system.
- **How does each action execute?** Trade needs a dialogue and shop interaction, Patrol needs a path or waypoint source, Attack and Retreat need the combat and navigation systems. Unity keeps authority over all of them; Rust only chooses.
- **Native library build and delivery.** Which targets first (Windows/macOS editor for development, Android arm64 for the A13), and whether CI publishes them as artifacts to copy into `Assets/Plugins/`.
