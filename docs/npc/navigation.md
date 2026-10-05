# NPC navigation: what exists and what to choose

Status (2026-10-05): the Managed NPC walks with Unity's NavMesh through a `NavMeshAgent`. The repository contains no pathfinding code of its own, and neither does the Rust side. This page records what that means, how it compares with an A* package, and what would make us change.

## What exists

- `ManagedNpcActor` sets a destination on a `NavMeshAgent` and starts or stops it. Unity's navigation computes the path and moves the agent.
- The AI Navigation package (`com.unity.ai.navigation` 1.1.5) and the built-in AI module are already in the manifest. Nothing else is installed for navigation.
- Patrol uses waypoints or a random point on the NavMesh near the start. Chase uses the last seen position and refreshes every 0.25 s. Retreat picks a point away from the threat.
- Rust never sees a path. It chooses an action, and Unity decides where to go. That split was decided earlier: Unity owns navigation.
- The NPC debugger window draws the path, destination, patrol route and ranges (`managed.md`, `ManagedNpcDebugView.cs`).

## What is not there

- No baked NavMesh in any scene yet. The actor stands still and warns once without one.
- No `NavMeshLink` (jumps, doors, ladders), no area costs (roads cheaper than mud), no dynamic obstacles, no avoidance tuning, no agent types other than the default one.
- No flying or swimming movement. The design lists merfolk and dragons, and a NavMesh agent only walks on baked surfaces, so those races need another mover.
- No measurement of agent cost on the MacBook Pro or the Galaxy A13.

## NavMesh versus A*

These are not two alternatives. Unity's NavMesh finds paths with A*-style search over a graph of polygons. The real choice is between Unity's built-in navigation system and a third-party navigation package, the A* Pathfinding Project, which offers grid, point and navmesh graphs, its own movement scripts and its own local avoidance.

What the research found, and its limits:

- No direct comparison between the two exists in the sources found. The package author says so himself. Forum answers from other users say the package is "very performant" and that Unity's navigation is not terrible, both opinions without numbers.
- Unity's navigation is already multithreaded through the job system, according to a Unity forum answer, and `NavMesh.pathfindingIterationsPerFrame` and the timing of `SetDestination` calls are the usual tuning points. One user reported 30 fps with 4000 NavMesh agents on unspecified hardware, which says little about a 2010 laptop or a mid-range phone.
- In the A* package, local avoidance (RVO/ORCA) is a Pro feature, according to its documentation. A forum answer from the author says Unity's system and the package use very similar local avoidance.
- One forum report describes the package's newer `FollowerEntity` agents misbehaving on a mobile headset with local avoidance, and the same report mentions the Entities package. The ECS stack does not run on the development machine, so any part of that package that needs Entities is out. Whether its classic MonoBehaviour movers (`AIPath`, `RichAI`) need it was not checked.
- None of this was measured on our hardware, and none of it can be, because Unity is not available in the authoring environment.

## Decision

Stay on the NavMesh for now.

- It is installed, free, has no extra dependency and does not need ECS.
- The target is hundreds of NPCs, not thousands, and many of them stand still (merchants) or will not be near the player.
- All navigation calls (destination, stop, speed, sampling) sit in one file, `ManagedNpcActor.cs`. Replacing the engine later means replacing that file and the debugger accessors, and the Rust side does not change.

## What would make us change

Measure first, with the NPC debugger window and the Unity Profiler on the target devices, at 50, 100, 200 and 400 moving NPCs. Revisit when one of these happens:

- Navigation shows up as a frame time problem on the MacBook Pro or the Galaxy A13 after the cheap fixes: fewer repaths, turning avoidance off for far NPCs, and not giving an agent to NPCs that are far from the player.
- The world is generated at runtime and baking or updating the NavMesh is too slow or too coarse. This is likely to matter, because the character and environment pipeline is procedural. Test runtime baking with `NavMeshSurface` per chunk before blaming Unity.
- Flying and swimming races need their own movement. That calls for a separate mover whatever the engine is, and a grid or volume search in code of our own is the likely answer.
- Large battles where hundreds of NPCs path to one target. A flow field computed once per target is cheaper than hundreds of path requests. It could live in Rust later, but it would break the rule that Unity owns navigation, so it needs an explicit decision.

## Cheap steps before any change

1. Bake a NavMesh in a test scene and look at the debugger window with a dozen NPCs.
2. Raise the NPC count until the Profiler shows a cost, and record the number per device in `benching-standards.md`.
3. Keep agents off NPCs that are far from the player, and wake them when the player approaches. This ties in with decision scheduling on the Rust roadmap.
