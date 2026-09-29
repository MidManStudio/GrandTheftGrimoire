# GTG NPC architecture — initial scaffold

Unity ECS owns authoritative world state, physics, navigation, animation, inventory, damage and dialogue. Rust provides a deterministic baseline and future optional ML policies. The native ABI is deliberately minimal and not yet wired into the Unity update loop. Keep stationary merchant NPCs on FSMs; use utility AI for common combatants; benchmark learned policies before enabling them for selected important NPCs. Online updates must be explicitly opt-in and time-budgeted. No Python is used for core ML.

This is a scaffold, not a production-ready NPC implementation. Follow-up: benchmark SoA batches, native binary packaging and actual Galaxy A13 performance.

## Authoring data (MDIX)

NPC archetypes are authored in `Assets/NPC/Archetypes/archetypes.mdix`: one file holding the shared enums, one `createArchetype` QuickFunc and an `archetypes::` group array. The numeric enum values are the Rust ABI v2 values. The file is **not yet read by the Unity authoring loader** and is only statically checked. See `mdix-authoring.md` for the design, the reasons it is a single self-contained file, and open findings.
