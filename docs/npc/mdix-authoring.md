# NPC authoring data (MDIX)

Status (2026-09-29): **authored and statically checked only.** Nothing in Unity reads these files yet, and the file has not been run through `mdix validate` (DixScript-Rust needs Rust 1.80+, which the authoring environment lacks).

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

## Not changed, needs a decision

- **`town_guard` pairs `UTILITY` with `FIXED_WEIGHTS`.** Only `HYBRID_ML` has any model path, and the architecture plan says ordinary combatants use no ML. Values were carried over unchanged. Likely `DISABLED`.
- **Two different `Rarity` enums.** `game_enemies.mdix` has four values and `inventory_items.mdix` has five (with `EPIC`). Auto-numbering makes `LEGENDARY` 3 in one and 4 in the other. Harmless while nothing compares the raw integers.
- **Style drift in the two older files.** They lack the signature comment and use unprefixed QuickFunc parameters. `@CONFIG` has no `features` key; the source's default is `"advanced"`, so this should compile, but it was not run.
- **C# authoring still uses magic integers.** `NPCAuthoring` has `int Role`, `Backend` and `LearningMode` with comments, and `NPCIdentity` stores bytes. C# enums with the same values would give Inspector dropdowns and remove the comments. Not changed because Unity compilation cannot be checked here.
- **Candidate enums, not added.** A `Race` or faction enum fits the design docs, but the full race list is still open there. `dialogue_ref` stays a string because dialogue ids are open-ended content, not a fixed set.

## Validate locally

```sh
mdix validate Assets/NPC/Archetypes/archetypes.mdix
mdix inspect Assets/NPC/Archetypes/archetypes.mdix --keys
```
