# GTG NPC architecture: initial scaffold

> Code location: `Assets/MidManStudio/Gtg/ECS/NPC/`. The ECS sources compile only when `GTG_ECS` is defined. A Managed version of the NPC loop (health, sight, brain and director) lives in `Assets/MidManStudio/Gtg/Managed/NPC/` and `Managed/Health/`, see `../GrandTheftGrimoire/managed.md`. The enums and the native bridge are shared by both stacks and stay under `ECS/NPC/`.

Unity owns authoritative world state, physics, navigation, animation, inventory, damage and dialogue, through ECS on the ECS stack and through MonoBehaviours on the Managed stack. Rust provides a deterministic baseline and future optional ML policies. The native ABI is deliberately minimal. `NPCDecisionSystem` calls it in batches, but nothing yet feeds it real observations or acts on its decisions; see `unity-integration.md`. Keep stationary merchant NPCs on FSMs; use utility AI for common combatants; benchmark learned policies before enabling them for selected important NPCs. Online updates must be explicitly opt-in and time-budgeted. No Python is used for core ML.

This is a scaffold, not a production-ready NPC implementation. Follow-up: benchmark SoA batches, native binary packaging and actual Galaxy A13 performance.

## Authoring data (MDIX)

NPC archetypes are authored in `Assets/NPC/Archetypes/archetypes.mdix`: one file holding the shared enums, one `createArchetype` QuickFunc and an `archetypes::` group array. The numeric enum values are the Rust ABI v2 values. The file is **not yet read by the Unity authoring loader** and is only statically checked. C# mirrors the enums in `Components/NPCEnums.cs`, and `scripts/check_npc_enum_sync.py` (first step of `npc-rust-ci.yml`) fails CI if MDIX, C# and Rust disagree. See `mdix-authoring.md` for the design, the reasons it is a single self-contained file, and open findings.

The Unity-side native layer is tested against the real library by `rust/npc/csharp-abi-test` (CI job `csharp-abi`). See `unity-integration.md` for what is and is not verified.
