# NPC test scene (`Assets/SamlpleScene.unity`)

The scene is a small arena for checking the NPC systems by hand. It was corrected after a first test showed that a melee NPC never attacked a ranged one and that nobody's health went down. This page lists what the scene contains, the numbers behind it and what to expect. The rules behind the numbers are in `managed.md`, section "Scene setup (NPC)", steps 13 to 18.

Nothing here was run in Unity by the author of the change. The expectations below come from reading the scene and the code, so the first run is the real check. If something differs, the NPC Debugger (menu GTG, NPC Debugger, in Play mode) shows each NPC's action, `hp` in percent, `sees threat` while it has one in sight and, in a fight, `striking`, `waiting` or `firing`. That is the first thing to read.

## Layout

| Object | Where | Facing | Notes |
|---|---|---|---|
| Player | (0, 1.15, 0) | +Z | Layer 7 (Player). `CharacterController` centered on the pivot. Visible points (0, 0.6, 0) and (0, -0.1, 0) |
| Npc_Default | (-4, 1.1, 12) | +X | Layer 9 (Npc). Enemy, Hostile, Utility, **Melee**. Threat source added. Eye (0, 0.6, 0) |
| Npc_Ranged | (4, 1.1, 12) | -X | Layer 9 (Npc). Enemy, Hostile, Ranged, shooter at (0.3, 0.4, 0.5). Threat source added. Eye (0, 0.6, 0) |
| Npc_Manager | anywhere | | `ManagedNpcDirector` with the overlay on. No shot driver |
| Floor | y -0.4, scale 100 x 1 x 100 | | Layer 6 (Ground). The top is at y 0.1, so a center pivot rests at about 1.1 |
| Wall | z 17, 30 x 5 x 1 | | Layer 8 (Wall) |

The two NPCs stand 8 m apart and face each other. Each is inside the other's view range (10 for the melee NPC, 20 for the ranged one) and view cone. The player is 12 m and farther from both, outside the melee NPC's view range and outside the ranged NPC's cone, so at the start they see only each other.

## Do this once after opening the scene

1. Select the Floor and open its `NavMeshSurface`. The Layer Mask is now Ground and Wall only. Press Bake. The saved NavMesh was baked with the old layer mask, with the capsules of the player and the NPCs in it.
2. Check the Console after pressing Play. Expected warnings: none from `[GTG NPC]`. A warning about a missing native library is normal, the managed decision takes over (the director overlay says `native` or `managed fallback`).

## What to expect

- Npc_Default sees Npc_Ranged, walks to it and strikes in melee for 10 damage every 1.2 s once it holds a place within reach. Npc_Ranged sees it too, keeps away to its stand-off distance and shoots arrows (12 damage), and kites when the melee NPC is closer than 6 m. In the NPC Debugger both rows show `sees threat`, the actions go through Attack, Shoot and Kite, and `hp` goes down.
- Walk the player toward the arena. Both NPCs now have three targets. Each picks the nearest target in view and prefers the one it already has, so the player gets attacked when it is the nearest. Melee hits on the player lower the HUD health bar by 10 each.
- Destroying the target stops the fight: the survivor walks to where it last saw the target and then wanders.

## Numbers for any capsule character

For a 2 m capsule whose pivot is at its center (a Unity capsule primitive, or an object with a `NavMeshAgent` Base Offset of 1):

| Field | Value |
|---|---|
| Threat source, Visible Points | (0, 0.6, 0) and (0, -0.1, 0) |
| Brain, Eye Offset | (0, 0.6, 0) |
| Shooter, Shot Point Offset | (0.3, 0.4, 0.5) |
| `NavMeshAgent`, Base Offset | 1 |
| `CharacterController` (player), Center | (0, 0, 0) |

For a pivot at the feet use the component defaults, which are exactly these numbers plus 1 on every height.

## What every NPC needs to fight

- `ManagedNpcBrain` (role Enemy, Guard or Boss for a fighter, with a Hostile or Retaliatory disposition), `ManagedHealth`, `ManagedNpcActor`, a collider, and the object on the Npc layer.
- `ManagedNpcThreatSource` to be a target of other NPCs. Without it only the player can be attacked. A scene with no factions makes every NPC with a threat source an enemy of every other.
- `ManagedNpcShooter` for the Ranged and Hybrid styles. No `ManagedNpcShotDriver` anywhere.
- A baked NavMesh under all of them.

## Known limits

- NPC against NPC is a test arrangement. Factions are the real answer and are not built, see the design record in `docs/` (favor and factions).
- `Npc_Ranged` uses the HybridMl backend, which has no model and takes the deterministic path. It behaves like Utility.
- Shooters aim at the last seen position, without leading a moving target, so a target that runs sideways takes fewer hits.
