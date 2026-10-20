# NPC Unity integration: status and audit

Status (2026-10-20): the native boundary is verified from C# against the real Rust library, at ABI version 7. Two stacks now sit on top of it, and the ECS stack does not run on the development machine, see `../GrandTheftGrimoire/managed.md`. The Managed stack now closes the whole loop: health, line of sight, observation, decision, and an actor that patrols, chases, hits and retreats, plus fireball damage and an editor debugger. Since version 6 the Managed actor also fights at range: archers and crossbow guards shoot and back away, mages cast, hybrids switch between shooting and melee, and a shopkeeper that was attacked can fight back. The ECS stack acts on nothing. Nothing has been built or run inside Unity.

## What exists

### ECS stack and shared files (`Assets/MidManStudio/Gtg/ECS/NPC/`)

The four files marked shared have no ECS dependency and are compiled by both stacks.

| File | Role |
|---|---|
| `Components/NPCEnums.cs` (shared) | `NpcRole`, `DecisionBackend`, `LearningMode`, `NpcAction` (12 actions since v6: shoot is 10, kite is 11), plus `NpcDisposition`, `NpcOrder` and `AttackStyle` (melee, ranged, hybrid) |
| `Components/NPCComponents.cs` | `NPCTag`, `NPCIdentity`, `NPCObservation`, `NPCDecision` |
| `Authoring/NPCAuthoring.cs` | Baker that adds the four components with default values |
| `Native/NPCNativeTypes.cs` (shared) | 64-byte observation and 16-byte decision structs, explicit layout |
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
| `NPC/ManagedNpcBrain.cs` | One NPC: settings, disposition, level, order, provoked, leader, sight, observation, last action, `ActionChanged` and `OrderRefused` events |
| `NPC/ManagedNpcDirector.cs` | Batches the brains, one native call per tick, ray budget, rotation for large populations |
| `NPC/ManagedCombatEngagement.cs` | Where each attacker of one target stands: places on a melee ring, attack tokens with a limit and rotation, a waiting ring with circling. Shooters have their own pool, limit and outer ring at their stand-off distance |
| `NPC/ManagedNpcActor.cs` | Carries out the decision with a `NavMeshAgent`: patrol, attack from a place on the target's ring with a live reach check, shoot from the shooter ring, kite (back away facing the target), retreat, follow the leader, hold, stand still. An NPC that cannot move stays at its post and strikes or fires from there |
| `NPC/ManagedNpcShooter.cs` | The ranged weapon of one NPC (bow, crossbow or spell): ammo, cooldown, regeneration, aim and line of fire. Rust still chooses Shoot or Kite |
| `NPC/ManagedNpcShots.cs`, `ManagedNpcShotDriver.cs`, `ManagedNpcBallistics.cs` | The shots in the air: a store over `ManagedSpellFlight`, one hidden driver that steps and draws them, and the aiming maths. Arrows and bolts arc and hurt through `IManagedDamageable`, spells raise the same impact as the player's |
| `Magic/ManagedSpellDamage.cs` | Fireball explosion damage with falloff, through `IManagedDamageable` |
| `NPC/Editor/ManagedNpcDebugView.cs`, `ManagedNpcDebuggerWindow.cs` | Editor-only live list and Scene view drawing of paths, routes, ranges and sight |

Each Managed tick (every 0.1 s): take up to 256 brains from a rotating cursor, run sight for the living ones within a ray budget (128 per tick by default), build the observations, make one `gtg_npc_decide_batch` call (managed fallback if the library is missing), and apply each action to its brain. Threat visibility is a cone plus line of sight: an NPC sees a threat when one of its visible points is inside range and the cone and the first thing the ray meets is that threat. Seeing lingers for 0.5 s after the last ray that saw it. Health comes from `ManagedHealth.Fraction`, or 1 when the NPC has none.

## What one decision tick does

1. Every 0.1 s, `ToEntityArray` on the NPC query, then take up to 256 entities starting at a rotating cursor.
2. Copy identity and observation into a reused managed array. Health is clamped to 0..1 (NaN becomes 1). The NPC id is `Identity.Id`, or entity version and index when that is 0.
3. One `gtg_npc_decide_batch` call. If the library is missing, the ABI differs, or the batch is rejected, the managed fallback decides instead (one warning per session).
4. A decision is written only if its id matches the observation's and its action is 0..4 (`MaxAction` is Retreat). The ECS stack is frozen and stays on the first five actions, so it never carries out Follow, Shoot or the others. The Managed director accepts 0..11.

## Verified, and how

| Claim | How it was checked | Where it runs |
|---|---|---|
| C# struct sizes (64/16) and every field offset match the Rust ABI | `Marshal.SizeOf` and `Marshal.OffsetOf` in the ABI test, plus the runtime size check in `NPCNativeLib` | CI job `csharp-abi` |
| `NPCNativeLib` can load the real `gtg_npc_ffi`, pass the ABI check, pin arrays and get correct results | 16384 mixed NPCs sent in batches of 256 through the real library | CI job `csharp-abi` |
| The managed fallback makes the same decision as Rust for valid input | Native and fallback compared on all 16384 NPCs, zero differences allowed | CI job `csharp-abi` |
| Rust, C and C# agree on the mixed workload | Same generator in all three; action histogram and FNV-1a hash of all decisions compared (`0xdd97a1437f8256ed` in the authoring sandbox, ABI version 7) | Rust and C in the bench workflow; C# in `csharp-abi` |
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
| Something reads the decision | no | yes, `ManagedNpcActor` carries out Patrol, Attack, Shoot, Kite and Retreat; Idle and Trade stand still; there is no shop or dialogue |
| Anything deals damage | no | yes, the fireball (`ManagedSpellDamage`), NPC melee, and NPC arrows, bolts and spells (`ManagedNpcShots`). Ice and the chemical hazards do not |
| A native plugin is in `Assets/` | no | no. Every batch takes the managed fallback after one warning until a build is imported |
| NPC ids stable across sessions | no, entity index and version | no, a per-session counter |
| `archetypes.mdix` read by the game | no | no. Settings are typed in the Inspector |
| `LearningMode` read by anything | no | no |
| Companion orders and levels | no | yes, `SetOrder`, level and refusal. No order UI, and no factions, so a companion sees no enemy |

Native plugin builds: CI builds Linux x86-64 and the benchmark workflow builds Linux ARM64 and macOS arm64. There is no Windows build, no Intel macOS build (the development machine is an Intel MacBook Pro) and no Android arm64 build for Unity. The Rust library is not affected by the Burst `Illegal instruction` abort that stopped the ECS stack, because that abort comes from Burst code generation. Whether the library runs on the development machine is untested.

## Decided and still open

Decided (2026-10-04 and 2026-10-05):
- `ThreatVisible` is line of sight, in the style of the Assassin's Creed games: a view cone plus a raycast. It is not distance only and not faction hostility.
- Health is its own component. The Managed stack has `ManagedHealth`. The ECS stack still has none, and it is on the ECS backlog.
- Gameplay is built on the Managed stack first, the ECS stack stays frozen.
- Patrol and Attack are wired through `ManagedNpcActor`, and fireballs damage anything with a `ManagedHealth`.
- A group attacking one target does not stack. At most three strike at once (a setting on the target's `ManagedNpcThreatSource`), the rest wait on a wider ring and circle, and the turn rotates every five seconds. This is a Unity-side layer after the Rust decision: Rust still returns Attack for every attacker. Targets are chosen nearest first, with a preference for the current target and for whatever hurt the NPC. See `managed.md`.
- Attack styles (owner decisions, ABI version 6): combatants are melee (most common), ranged (bow and crossbow guards, mages) or hybrid, a setting on the brain. Rust decides Shoot, Kite or Attack from the style, the distance and whether the NPC has anything to shoot; Unity owns the weapon, the ammo and the flight. Shooters have their own attack limit and outer ring, separate from the melee limit. Arrows and bolts are projectiles, mages reuse the player's spell flight, and a hybrid switches on a distance band (with a buffer so it does not flip) and drops to melee when out of ammo or mana. Boss fights are a separate system later. See `observation.md` and `managed.md`.
- Shopkeepers (owner note, ABI version 6): a merchant with the retaliatory disposition fights back once attacked, in its own attack style. A hostile merchant does not, and a shop that refuses service while its keeper is provoked is a game-side rule that is not built.
- Favor (2026-10-20, ABI version 7) is bottom-up in Unity. A relevant NPC carries a `ManagedNpcFavor` gauge from -100 to 100, a `ManagedFavorRegion` builds the Local Favor of a town from the gauges standing in it and a crowd value for everybody else, and Rust receives only the favor of each NPC. An NPC with no gauge feels the crowd of its region. Hurting somebody costs the victim, the watchers and the crowd. Kingdom, race and faction standing will use the same pipeline. See `../GrandTheftGrimoire/favor.md`.
- Crown Heat (2026-10-14) is a regional meter in Unity, never a per-NPC value. A `ManagedCrownRegion` box volume counts crimes the player commits in view of a guard (assault and murder of a merchant, civilian or guard), turns the meter into a tier (1 wanted, 2 hunted, 3 outlaw), and lets it fade after 20 s without being seen. The guard's brain reads the tier of the region it stands in and sends it as `crown_heat`, for guards only. Rust has one rule on it. See `crown-heat.md`.
- The movement engine is Unity's NavMesh for now, see `navigation.md`.

Still open, game-design choices that were not guessed:
- **Detection model.** The current model is binary and instant, with a short lingering. A suspicion meter that fills while the player is seen, as in the Assassin's Creed games, would need a new ABI field or a Unity-side filter before the observation. It is not built.
- **Factions and companions.** Next after favor. Every threat source is a threat to every NPC. Companions need their own handling, see `rust-roadmap.md`.
- **Trade.** A merchant trades or stands still, and one that fights back does so by the rule above. A shop and dialogue system does not exist.
- **Damage sources.** Ice and the chemical hazards do not hurt yet, and the fireball ignores walls.
- **Native library targets.** Which targets first for Unity, and whether CI publishes them as artifacts to copy into `Assets/Plugins/`. An Intel macOS build is needed for the development machine.
