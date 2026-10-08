# Data (`MidManStudio.Gtg.Data`)

Code: `Assets/MidManStudio/Gtg/Data/`. The classes have no ECS dependency, so the ECS stack and the Managed stack can read the same assets.

## Overview

The game's tuning data is authored in `.mdix` files. This part holds the ScriptableObject each file bakes into, so gameplay code reads typed assets and never parses text. Every data class carries `[MdixBakeable]`, which is what makes MDIX Studio offer it when a file is baked.

| File | ScriptableObject | Row type |
|---|---|---|
| `Assets/game_enemies.mdix` | `EnemyDatabase` | `EnemyDefinition` |
| `Assets/inventory_items.mdix` | `ItemDatabase` | `ItemDefinition` |
| `Assets/NPC/Archetypes/archetypes.mdix` | `NpcArchetypeDatabase` | `NpcArchetypeDefinition` |
| `Assets/NPC/Dialogue/merchant_default.mdix` | `NpcDialogue` | none, the file is flat |

Nothing in gameplay code reads the baked assets yet. This part makes them possible.

Status (2026-10-07): written and checked outside Unity. All four files were loaded with the engine that ships in `com.midmanstudio.mdix`, the one Unity loads, and bound into these classes with the package's binder. Every member found its value, every key was read, and every enum number landed on the right member. The bake itself has not been run inside the Unity Editor.

## Baking

1. In the Project window, right click the `.mdix` file, then MDIX, then Generate ScriptableObject.
2. The wizard lists every `[MdixBakeable]` class, and the class that reads the most of this file comes first, tagged "fits exactly" when every member and every key lines up.
3. Generate. The asset is written next to the file as `<name>_data.asset`. Baking again updates the same asset in place, so scenes and prefabs that point at it keep working.
4. Read the message. It lists members the file has no value for, keys nothing reads, and members Unity would not store. A value that cannot be converted refuses the bake.

## How keys map to members

- A member called `SpawnCap`, `spawnCap` or `spawn_cap` reads the key `spawn_cap`. `KeepSake` reads `keepSake`, the key as written in the file.
- Enums are matched by number, which is what the data stores. The numbers of a C# enum must equal the numbers in the file's `@ENUMS` section.
- A division in the data gives a decimal number (`health / 10`), so those members are floats.
- A `List<T>` reads an array, and a nested `[Serializable]` class reads an object. Unity drops a nested class that is not `[Serializable]`, and the bake says so.

## Modules

Files are in `Assets/MidManStudio/Gtg/Data/`.

### `EnemyDatabase.cs`

`EnemyAiType`, `EnemyRarity`, `EnemyDefinition` and `EnemyDatabase`, for `game_enemies.mdix`. The file holds `spawn_cap`, `respawn_delay` and an `enemies` table of four rows (Goblin, Orc, Troll, Dragon).

- Enemies have their own rarity enum because their `Rarity` has four values and no Epic, while the item one has five. With one shared C# enum a Legendary enemy (3) would read as Epic. See Known gaps.
- `Armor` and `Xp` are floats because the file derives them from the health with a division.
- `RespawnDelay` holds the number as written. The file does not state a unit.

### `ItemDatabase.cs`

`ItemType`, `ItemRarity`, `ItemDefinition` and `ItemDatabase`, for `inventory_items.mdix`: `max_stack_size`, a long text under `keepSake`, and an `items` table of three rows.

- `KeepSake` exists only so the bake reports no unread key. The text is a note that nothing uses, and both can go when the file is cleaned up.
- `Weight` is a float because the file derives it from the value with a division.

### `NpcArchetypeDatabase.cs`

`NpcArchetypeDefinition` and `NpcArchetypeDatabase`, for `archetypes.mdix`: `schema`, `abi_version` and an `archetypes` table of three rows.

- The enums are the ones in `ECS/NPC/Components/NPCEnums.cs`, which `scripts/check_npc_enum_sync.py` keeps in step with the file and the Rust ABI. No second copy exists.
- `UsesMl` and `MayUpdateWeights` are derived inside the file from the backend and the learning mode. They are read as data and not decided here.
- A loader should refuse a table whose `AbiVersion` differs from the one it was built for.

### `NpcDialogue.cs`

`NpcDialogue`, for a flat dialogue file such as `merchant_default.mdix`: `Id`, `Greeting`, `Farewell`. An archetype points at one through its `DialogueRef`, which holds the `Id`. Nothing resolves that reference yet.

## Known gaps

- The two `Rarity` enums in the `.mdix` files are still different lists. `docs/npc/mdix-authoring.md` records that a decision is needed. The C# side keeps them apart so baked values stay right either way.
- `keepSake` in `inventory_items.mdix` is a leftover test text.
- No gameplay code reads these assets yet.

## Fixes and Problems

- 2026-10-07: `archetypes.mdix` declared `version -> "1.1.1"`. The engine accepts only `"1.0.0"` there, because the value is the DixScript language version, and failed with `CONFIG: Unsupported version: 1.1.1`. Unity could not have imported the file. It now declares `1.0.0`.
- 2026-10-07: baking could not fill list members, because the serializer behind `Generate ScriptableObject` maps properties only and skips lists, so an `enemies` list would have come out empty. The package binder now does the filling. The fix is in `com.midmanstudio.mdix`, see its changelog.
