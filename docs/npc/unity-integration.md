# NPC Unity integration: status and audit

Status (2026-10-04): the native boundary is verified from C# against the real Rust library. Two stacks now sit on top of it, and the ECS stack does not run on the development machine, see `../GrandTheftGrimoire/managed.md`. The Managed stack now closes the whole loop: health, line of sight, observation, decision, and an actor that patrols, chases, hits and retreats, plus fireball damage and an editor debugger. The ECS stack acts on nothing. Nothing has been built or run inside Unity.

## What exists

### ECS stack and shared files (`Assets/MidManStudio/Gtg/ECS/NPC/`)

The four files marked shared have no ECS dependency and are compiled by both stacks.

| File | Role |
|---|---|
| `Components/NPCEnums.cs` (shared) | `NpcRole`, `DecisionBackend`, `LearningMode`, `NpcAction` (ABI v2 numbers) |
| `Components/NPCComponents.cs` | `NPCTag`, `NPCIdentity`, `NPCObservation`, `NPCDecision` |
| `Authoring/NPCAuthoring.cs` | Baker that adds the four components with default values |
| `Native/NPCNativeTypes.cs` (shared) | 32-byte observation and 16-byte decision structs, explicit layout |
| `Native/NPCNativeLib.cs` (shared) | P/Invoke, ABI version and size check, pinned-array batch call, warn-once fallback signal |
| `Native/NPCManagedFallback.cs` (shared) | Managed copy of the Rust decision, used when the native library is missing or rejects a batch (no Unity dependencies) |
| `Systems/NPCDecisionSystem.cs` | Every 0.1 s: up to 256 NPCs, one native batch call, writes `NPCDecision` |

### Managed stack (`Assets/MidManStudio/Gtg/Managed/NPC/` and `Managed/Health/`)

| File | Role |
|---|---|
| `Health/ManagedHealth.cs` | Hit points as a component of its own, `Fraction` 0 to 1, `Damaged` and `Died` events |
| `Health/IManagedDamageable.cs` | One-method contract for anything that takes damage |
| `NPC/ManagedNpcThreatSource.cs` | Marks what NPCs can see, normally the player. Several visible points on the body |
| `NPC/ManagedNpcSight.cs` | View cone test, then one `Physics.Raycast` from the eye to the point |
| `NPC/ManagedNpcBrain.cs` | One NPC: settings, sight, observation, last action, `ActionChanged` event |
| `NPC/ManagedNpcDirector.cs` | Batches the brains, one native call per tick, ray budget, rotation for large populations |
| `NPC/ManagedNpcActor.cs` | Carries out the decision with a `NavMeshAgent`: patrol, chase and melee, retreat, stand still |
| `Magic/ManagedSpellDamage.cs` | Fireball explosion damage with falloff, through `IManagedDamageable` |
| `NPC/Editor/ManagedNpcDebugView.cs`, `ManagedNpcDebuggerWindow.cs` | Editor-only live list and Scene view drawing of paths, routes, ranges and sight |

Each Managed tick (every 0.1 s): take up to 256 brains from a rotating cursor, run sight for the living ones within a ray budget (128 per tick by default), build the observations, make one `gtg_npc_decide_batch` call (managed fallback if the library is missing), and apply each action to its brain. Threat visibility is a cone plus line of sight: an NPC sees a threat when one of its visible points is inside range and the cone and the first thing the ray meets is that threat. Seeing lingers for 0.5 s after the last ray that saw it. Health comes from `ManagedHealth.Fraction`, or 1 when the NPC has none.

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

| Gap | ECS stack | Managed stack |
|---|---|---|
| Something writes the observation | no, the baker sets `ThreatVisible = 0` and `HealthFraction = 1` and nothing changes them | yes, sight and `ManagedHealth` (not run in Unity) |
| A health component exists | no | yes, `ManagedHealth` |
| Something reads the decision | no | yes, `ManagedNpcActor` carries out Patrol, Attack and Retreat; Idle and Trade stand still; there is no shop or dialogue |
| Anything deals damage | no | yes, the fireball (`ManagedSpellDamage`) and NPC melee. Ice and the chemical hazards do not |
| A native plugin is in `Assets/` | no | no. Every batch takes the managed fallback after one warning until a build is imported |
| NPC ids stable across sessions | no, entity index and version | no, a per-session counter |
| `archetypes.mdix` read by the game | no | no. Settings are typed in the Inspector |
| `LearningMode` read by anything | no | no |

Native plugin builds: CI builds Linux x86-64 and the benchmark workflow builds Linux ARM64 and macOS arm64. There is no Windows build, no Intel macOS build (the development machine is an Intel MacBook Pro) and no Android arm64 build for Unity. The Rust library is not affected by the Burst `Illegal instruction` abort that stopped the ECS stack, because that abort comes from Burst code generation. Whether the library runs on the development machine is untested.

## Decided and still open

Decided (2026-10-04 and 2026-10-05):
- `ThreatVisible` is line of sight, in the style of the Assassin's Creed games: a view cone plus a raycast. It is not distance only and not faction hostility.
- Health is its own component. The Managed stack has `ManagedHealth`. The ECS stack still has none, and it is on the ECS backlog.
- Gameplay is built on the Managed stack first, the ECS stack stays frozen.
- Patrol and Attack are wired through `ManagedNpcActor`, and fireballs damage anything with a `ManagedHealth`.
- The movement engine is Unity's NavMesh for now, see `navigation.md`.

Still open, game-design choices that were not guessed:
- **Detection model.** The current model is binary and instant, with a short lingering. A suspicion meter that fills while the player is seen, as in the Assassin's Creed games, would need a new ABI field or a Unity-side filter before the observation. It is not built.
- **Factions and companions.** Every threat source is a threat to every NPC. Companions need their own handling, see `rust-roadmap.md`.
- **Trade.** A merchant only stands still. A shop and dialogue system does not exist.
- **Damage sources.** Ice and the chemical hazards do not hurt yet, and the fireball ignores walls.
- **Native library targets.** Which targets first for Unity, and whether CI publishes them as artifacts to copy into `Assets/Plugins/`. An Intel macOS build is needed for the development machine.
