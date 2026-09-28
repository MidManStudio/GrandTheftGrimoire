# CharacterController

Custom kinematic character controller, built on ECS and (once Phase 1
lands) networked with Netcode for Entities. See
`GTG_REPO_CONVENTIONS.md` §5 for why this system is ECS rather than
MonoBehaviour, and the story/gameplay design references for what the
character needs to eventually do (melee, chemistry-thrown weapons,
Excalibur, summoned weapons — gameplay-reference.md §12).

## Current phase

**Phase 0 — local movement only, no networking.** The goal of this
phase is narrow on purpose: prove the kinematic movement feels right
for one local player before adding Netcode for Entities' ghosting and
prediction on top of it. Mixing "does this feel good" with "is this a
valid networked system" at the same time is a bad way to learn either
one.

Phase 1 (next) replaces `CharacterInputSystem` with per-connection
networked input (`IInputComponentData`), adds ghost components for
whatever needs to sync (position, velocity, grounded state), and moves
this system's simulation into NFE's prediction loop. The movement math
in `CharacterMovementSystem` itself shouldn't need to change much —
only how its inputs arrive and how its outputs get replicated.

## Modules

- **Components/CharacterComponents.cs** — all ECS data for the
  character: tag, tunable move settings (baked once, read-only),
  per-frame input, vertical velocity, grounded state.
- **Authoring/CharacterAuthoring.cs** — Editor-only `MonoBehaviour` +
  `Baker`. Drop on a GameObject inside a SubScene; the GameObject
  itself doesn't exist at runtime.
- **Systems/CharacterInputSystem.cs** — polls the local
  keyboard/mouse/gamepad via the new Input System and writes the result
  into every `CharacterInput` in the world. Correct for exactly one
  local player, which is all Phase 0 needs.
- **Systems/CharacterMovementSystem.cs** — ground check via a Unity
  Physics raycast, gravity/jump on the vertical axis, direct position
  move on the horizontal plane. Movement is world-axis-relative, not
  camera-relative, until the Cinemachine presentation layer exists to
  make "camera-relative" mean something.

## Known gaps at this phase

- No camera, so there's nothing to test movement feel against except
  the Scene view. Presentation layer (Cinemachine rig, companion
  Animator) is intentionally out of scope for Phase 0.
- No slopes/steps handling in the ground check — it's a single straight
  ray, not a shape cast. Fine for flat test geometry, will need
  revisiting before real terrain.
- Written against the documented Entities 1.x / Unity Physics /
  new Input System APIs but not compiled against the actual project —
  there's no Unity Editor in the environment these files were written
  in. Treat the first Editor open as the real test.

## Fixes and Problems

_(none yet — first real Editor run hasn't happened)_ 


Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Scenes.ResolveSceneReferenceSystem targeting Unity.Scenes.SceneSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Scenes.SceneSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:294)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Entities.FixedStepSimulationSystemGroup targeting Unity.Entities.BeginSimulationEntityCommandBufferSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Entities.VariableRateSimulationSystemGroup targeting Unity.Entities.BeginSimulationEntityCommandBufferSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Unity.Entities.LateSimulationSystemGroup targeting Unity.Entities.EndSimulationEntityCommandBufferSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:471)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Unity.Physics.GraphicsIntegration.BufferInterpolatedRigidBodiesMotion targeting Unity.Physics.Systems.ExportPhysicsWorld.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Unity.Physics.GraphicsIntegration.RecordMostRecentFixedTime targeting Unity.Physics.Systems.ExportPhysicsWorld.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateBeforeAttribute] attribute on Unity.Physics.Systems.PhysicsSimulationGroup targeting Unity.Physics.Systems.ExportPhysicsWorld.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on Unity.Physics.Systems.AfterPhysicsSystemGroup targeting Unity.Physics.Systems.ExportPhysicsWorld.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Physics.Systems.PhysicsSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:413)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak

To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #22 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #23 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #24 CallbackArray4<int const, AwakeFromLoa
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.CallSystemOnCreateWithCleanup (Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:634)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1282)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fb2a494a898 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 RuntimeInitializeOnLoadManager::Execute(RuntimeIni
Unity.Entities.StructuralChange+DestroyEntity_000011EA$BurstDirectCall.Invoke (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at <ddc9ed63194442bd91597bfd9e7a2864>:0)
Unity.Entities.StructuralChange.DestroyEntity (Unity.Entities.EntityComponentStore* entityComponentStore, Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/StructuralChange.cs:155)
Unity.Entities.EntityDataAccess.DestroyEntityInternalDuringStructuralChange (Unity.Entities.Entity* entities, System.Int32 count, Unity.Entities.SystemHandle& originSystem) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:588)
Unity.Entities.EntityManager.DestroyEntityInternal (Unity.Entities.Entity* entities, System.Int32 count) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:5002)
Unity.Entities.WorldUnmanagedImpl.FreeSlotWithoutOnDestroy (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:328)
Unity.Entities.WorldUnmanagedImpl.FreeSlot (System.UInt16 handle, Unity.Entities.SystemState* statePtr) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:360)
Unity.Entities.WorldUnmanagedImpl.DestroyManagedSystem (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:456)
Unity.Entities.WorldUnmanaged.DestroyManagedSystemState (Unity.Entities.SystemState* state) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/WorldUnmanaged.cs:1120)
Unity.Entities.ComponentSystemBase.OnAfterDestroyInternal () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:379)
Unity.Entities.ComponentSystemBase.CreateInstance (Unity.Entities.World world) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemBase.cs:219)
Unity.Entities.World.AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:468)
Unity.Entities.World.GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1[T] types, System.Int32 typesCount, Unity.Collections.AllocatorManager+AllocatorHandle allocator) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1289)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, Int32, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1295)
Unity.Entities.World:GetOrCreateSystemsAndLogException(NativeList`1, AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/World.cs:1321)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1, ComponentSystemGroup, DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:252)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal(World, NativeList`1) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:291)
Unity.Entities.DefaultWorldInitialization:Initialize(String, Boolean) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:147)
Unity.Entities.AutomaticWorldBootstrap:Initialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/EditorSubSceneLiveConversionSystem.cs:50)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Scenes.Editor.LiveConversionEditorSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/LiveConversion/LiveConversionEditorSystemGroup.cs:15)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.ResolveSceneReferenceSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/ResolveSceneReferenceSystem.cs:128)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/EditorSubSceneLiveConversionSystem.cs:50)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Scenes.Editor.LiveConversionEditorSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/LiveConversion/LiveConversionEditorSystemGroup.cs:15)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.ResolveSceneReferenceSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/ResolveSceneReferenceSystem.cs:128)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/EditorSubSceneLiveConversionSystem.cs:50)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Scenes.Editor.LiveConversionEditorSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/LiveConversion/LiveConversionEditorSystemGroup.cs:15)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.ResolveSceneReferenceSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/ResolveSceneReferenceSystem.cs:128)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/EditorSubSceneLiveConversionSystem.cs:50)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Scenes.Editor.LiveConversionEditorSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/LiveConversion/LiveConversionEditorSystemGroup.cs:15)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.ResolveSceneReferenceSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/ResolveSceneReferenceSystem.cs:128)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)

ArgumentException: The entity does not exist. Entities Journaling may be able to help determine more information. Please enable Entities Journaling for a more helpful error message.
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.ComponentType componentType) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:342)
Unity.Entities.EntityComponentStore.AssertEntityHasComponent (Unity.Entities.Entity entity, Unity.Entities.TypeIndex componentTypeIndex) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityComponentStoreDebug.cs:364)
Unity.Entities.EntityDataAccess.GetComponentData[T] (Unity.Entities.Entity entity) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityDataAccess.cs:1236)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:433)
Unity.Scenes.Editor.EditorSubSceneLiveConversionSystem.OnUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/EditorSubSceneLiveConversionSystem.cs:50)
Unity.Entities.SystemBase.Update () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup.UpdateAllSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
UnityEngine.Debug:LogException(Exception)
Unity.Debug:LogException(Exception) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:17)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:728)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Scenes.Editor.LiveConversionEditorSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes.Editor/LiveConversion/LiveConversionEditorSystemGroup.cs:15)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.ComponentSystemGroup:UpdateAllSystems() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:723)
Unity.Entities.ComponentSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:681)
Unity.Entities.InitializationSystemGroup:OnUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorld.cs:169)
Unity.Entities.SystemBase:Update() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/SystemBase.cs:418)
Unity.Entities.DummyDelegateWrapper:TriggerUpdate() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ScriptBehaviourUpdateOrder.cs:523)


