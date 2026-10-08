# NPC authoring data (MDIX)

Status (2026-09-29): **authored and statically checked only.** Nothing in Unity reads these files yet, and the file has not been run through `mdix validate` (DixScript-Rust needs Rust 1.80+, which the authoring environment lacks). The enum values are now guarded by `scripts/check_npc_enum_sync.py` in CI (see below).

Update (2026-10-07): the file has now been run through the real engine, the `libmdix_ffi` that ships in `com.midmanstudio.mdix` and that Unity loads. It did not load. `@CONFIG version` is the DixScript language version, the engine accepts only `"1.0.0"` there, and `"1.1.1"` failed with `CONFIG: Unsupported version: 1.1.1`, so Unity could not have imported the file. It now declares `1.0.0`, loads, and gives the three archetypes below. It also binds cleanly into `NpcArchetypeDatabase` (`Assets/MidManStudio/Gtg/Data/`, see `GrandTheftGrimoire/data.md`), checked outside Unity.

## Files

| File | Purpose |
|---|---|
| `Assets/NPC/Archetypes/archetypes.mdix` | Enums, the `createArchetype` QuickFunc and the `archetypes::` group array (merchant, guard, boss) |
| `Assets/NPC/Dialogue/merchant_default.mdix` | Fixed merchant dialogue, referenced by `dialogue_ref` |
| `Assets/game_enemies.mdix` | Existing enemy data. `AIType` is deliberately untouched and is not an NPC role or learning mode |

`archetypes.mdix` replaces the earlier `merchant.mdix`, `guard.mdix` and `boss.mdix`. Those three each redeclared the same enums and are now redundant; delete them from the repo (an archive cannot delete files). Nothing in the repo referenced them by file name or id.

## Enums

| Enum | Values | Relation to the Rust ABI |
|---|---|---|
| `NpcRole` | MERCHANT 0, CIVILIAN 1, GUARD 2, ENEMY 3, BOSS 4 | Same numbers as `NpcObservationAbi.role` |
| `DecisionBackend` | STATE_MACHINE 0, UTILITY 1, HYBRID_ML 2 | Same numbers as `NpcObservationAbi.backend`. HYBRID_ML currently falls back to the deterministic path |
| `LearningMode` | DISABLED 0, FIXED_WEIGHTS 1, ONLINE_UPDATES 2 | Authoring metadata only, not an ABI field and not an implemented ML feature |

Renumbering `NpcRole` or `DecisionBackend` requires an ABI version bump and matching changes in `npc-ffi` and the C# types.

## Design of `archetypes.mdix`

- One `createArchetype(_id, _role, _backend, _can_move, _learning, _dialogue_ref = "")` QuickFunc. The empty default means "no fixed dialogue"; a loader must treat `""` as none.
- The function derives `uses_ml` (backend is HYBRID_ML) and `may_update_weights` (learning is ONLINE_UPDATES). They are computed from the stated fields, not new decisions.
- `abi_version = 2` lets a loader refuse a file authored for a different ABI.
- `@CONFIG features` is `"quickfuncs,enums,data"`. Adding a section without its keyword is a hard compile error.

To add an archetype, add one `createArchetype(...)` line to the group array. Add a new enum value only when the Rust side and the C# side gain it in the same change.

## Why one self-contained file instead of `@IMPORTS`

`@IMPORTS` is proven in DixScript-Rust's own chemistry database and import tests, so the mechanism works for files loaded from disk. Unity's `MdixAsset.Load()` calls `Dix.LoadStr(rawSource)`, which maps to `mdix_load_str`. Reading the source, that path compiles the text under the name `<string_input>` with no file location, so a relative import has no reliable base directory. This was checked by reading the code, not by running it. Until the Unity loader is shown to resolve imports, each Unity-loaded `.mdix` file must stand alone.

## Enums in C# and the sync check

`Assets/MidManStudio/Gtg/ECS/NPC/Components/NPCEnums.cs` (shared by the ECS and Managed stacks) defines `NpcRole`, `DecisionBackend`, `LearningMode` (all `byte`) and `NpcAction` (`int`) with the same numbers as the MDIX enums and the Rust ABI. `NPCAuthoring`, `NPCIdentity`, `NPCDecision` and `NPCDecisionSystem` use them in place of raw integers, so the Inspector shows dropdowns. Unity stores enums as integers, so existing scene and prefab values are kept.

The native structs in `NPCNativeTypes.cs` stay raw `uint`/`int` on purpose: they mirror the C ABI byte for byte, and the decision system converts explicitly at that boundary. Their sizes and offsets were read against `npc-ffi` (32 and 16 bytes; offsets 0/8/12/16/20/24/28 and 0/8/12) and match. `NPCNativeLib` also checks the sizes at runtime.

`scripts/check_npc_enum_sync.py` runs first in `npc-rust-ci.yml`. It compares names and values of the four enums across MDIX, C# and Rust (`parse()`, `action_code()` and the `LearningMode` declaration order in `npc-ml`) and fails on any difference, an unparsable source, or a missing file. It is CI tooling, not part of the game or the ML pipeline. Names are compared case-insensitively with underscores removed (`STATE_MACHINE` equals `StateMachine`). It does not check `AIType` or anything outside these four enums.

## Changes in this pass

- `town_guard` now uses `LearningMode.DISABLED` (was `FIXED_WEIGHTS`). Only `HYBRID_ML` has a model path, and the architecture plan gives ordinary combatants no ML. `archetypes.mdix` declares `version -> "1.0.0"`, the only version the engine accepts. It first said 1.1.1, which fails to load.
- C# uses the enums above.

## Still open

- **Two different `Rarity` enums.** `game_enemies.mdix` has four values and `inventory_items.mdix` has five (with `EPIC`). Auto-numbering makes `LEGENDARY` 3 in one and 4 in the other. Harmless while nothing compares the raw integers. Needs a decision on the intended set before either file changes. The C# side keeps them apart (`EnemyRarity` and `ItemRarity` in `Assets/MidManStudio/Gtg/Data/`), so baked values stay right whichever way this is decided.
- **Style drift in the two older files.** They lack the signature comment and use unprefixed QuickFunc parameters. `@CONFIG` has no `features` key; the source's default is `"advanced"`, so this should compile, but it was not run.
- **Candidate enums, not added.** A `Race` or faction enum fits the design docs, but the full race list is still open there. `dialogue_ref` stays a string because dialogue ids are open-ended content, not a fixed set.
- **`LearningMode` on `NPCIdentity` is stored but not read by any system.** It carries authoring intent until an ML path exists.
- **How C# was checked.** The changed C# compiles with .NET 8 against hand-written stubs of the Unity types, and the native layer plus managed fallback also run against the real Rust library in CI (`docs/npc/unity-integration.md`). None of that is a Unity build; Burst, source generators and the real ECS API were not exercised.

## Validate locally

```sh
mdix validate Assets/NPC/Archetypes/archetypes.mdix
mdix inspect Assets/NPC/Archetypes/archetypes.mdix --keys
```
