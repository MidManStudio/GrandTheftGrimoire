# General problems

Running notes on errors and warnings seen in the Editor, with the current
reading of each. The raw console text pasted on 2026-09-30 is kept at the
bottom of this file.

## Analysis, 2026-09-30

Written after reading the console text below and the package sources for
Entities and Physics 1.3.10.

### Versions

| Item | Version |
| --- | --- |
| Unity Editor | 2022.3.13f1 |
| Entities, Physics | 1.3.10 |
| Burst | 1.8.18 |
| Collections | 2.5.3 |
| Mathematics | 1.3.2 |
| Input System | 1.7.0 |
| Cinemachine | 2.9.7 |
| URP | 14.0.9 |

Entities 1.3.10 and Physics 1.3.10 both declare a minimum editor of
2022.3.11f1 in their `package.json`, so this editor is supported. A version
mismatch is not the cause. The Unity Character Controller package 1.4.5
would need Entities and Physics 1.3.15 and editor 2022.3.50f1, which is one
reason the project does not use it.

### What the console says

- 13 warnings "Ignoring invalid [UpdateAfter] / [UpdateBefore] attribute".
  Unity raises this when a system names an ordering partner that is not in
  the same group, and here the partner was never created. The partners named
  are `Unity.Physics.Systems.ExportPhysicsWorld`,
  `BeginSimulationEntityCommandBufferSystem`,
  `EndSimulationEntityCommandBufferSystem` and `Unity.Scenes.SceneSystem`,
  all Unity systems, plus one GTG system, `FireballProjectileSystem`.
- One `InvalidOperationException: Illegal instruction executed`, thrown from
  Burst compiled code. The path is a system creation that failed, then the
  cleanup that destroys the half built system, which crashes in
  `ChunkDataUtility.RemoveFromEnabledBitsHierarchicalData`. That crash replaces
  the first exception, so the real cause of each failed creation is hidden.
- Three `ArgumentException: The entity does not exist` from
  `ResolveSceneReferenceSystem` and the live conversion system. They read data
  from the scene system's own entity, which the failed creation destroyed.
- Two `JobTempAlloc` leak warnings, which follow from the failed startup.

### What the failing systems have in common

Each failing system changes the archetype of its own entity or creates an
entity while it is being created. The command buffer systems add their
singleton component to their own entity. `BuildPhysicsWorld` creates the
`PhysicsWorldSingleton` in `OnCreate`. The first version of
`FireballProjectileSystem` created an event entity in `OnCreate`, and it is the
one GTG system in the warnings. Systems that do no structural work while being
created, such as the character input system, are not in the list.

So the reading is that structural changes made by Burst compiled Entities code
fail on this machine, and every system that needs one during startup is lost.

### Why the game symptoms follow

- With `BuildPhysicsWorld` lost there is no `PhysicsWorldSingleton`. The
  ground, movement and projectile systems wait for it with `RequireForUpdate`,
  so they never run. No gravity, no movement.
- With the command buffer systems lost, the cast system waits for a command
  buffer singleton that never appears. No fireball.
- With `SceneSystem` lost, a SubScene cannot load at all, so the character
  entity may not exist either.
- The camera still switches, because that part is plain GameObject code.

### Status

The cause is not confirmed. The working theory is Burst code generation on the
development machine, a mid 2010 MacBook Pro, whose CPU lacks the newer
instruction sets Burst may assume. The failing function counts and updates bit
sets, and reports of the same message from Burst say the code runs with Burst
switched off.

Checks, in this order:

1. Turn off `Jobs > Burst > Enable Compilation`, enter Play, and read the
   console. A clean console confirms the theory. Burst can stay off for
   Editor work on that machine.
2. Run `sysctl -n machdep.cpu.brand_string` and
   `sysctl -n machdep.cpu.features | tr ' ' '\n' | grep -E 'SSE4|POPCNT|AVX'`
   in Terminal and note which lines appear.
3. Enter Play and read the overlay at the top left of the Game view. It lists
   the physics world, the command buffer, and any character system that was
   not created, so the state is visible without the console.

If the console is still red with Burst off, the first red message after the
toggle is the next thing to read. For player builds on that machine, the Burst
target CPU list under `Project Settings > Burst AOT Settings` needs the older
targets.

## Raw console text, 2026-09-30

ArgumentException: The entity does not exist. Entity(50:1) was previously destroyed in world Default World.
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

ArgumentException: The entity does not exist.
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7f872c49aaf0 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #11  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #12  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #13  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #16  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #17  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #18 mono_jit_runtime_invoke
 #19 do_runtime_invoke
 #20 mono_runtime_invoke
 #21 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #22 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #23 MonoBehaviour::CallMetho
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System is from a different world.
Unity.Entities.EntityManager.GetCheckedEntityDataAccess (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:114)
Unity.Entities.EntityManager.GetComponentData[T] (Unity.Entities.SystemHandle system) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/EntityManager.cs:432)
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

ArgumentException: The entity does not exist. Entity(22:3) was previously destroyed in world Editor World.
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

Ignoring invalid [Unity.Entities.UpdateAfterAttribute] attribute on MidManStudio.Gtg.Chemistry.ChemistryReactionSystem targeting MidManStudio.Gtg.Magic.FireballProjectileSystem.
This attribute can only order systems that are members of the same ComponentSystemGroup instance.
Make sure that both systems are in the same system group with [UpdateInGroup(typeof(Unity.Entities.SimulationSystemGroup))],
or by manually adding both systems to the same group's update list.
UnityEngine.Debug:LogWarning (object)
Unity.Debug:LogWarning (object) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/Stubs/Unity/Debug.cs:13)
Unity.Entities.ComponentSystemSorter:WarnAboutAnySystemAttributeBadness (int,Unity.Entities.ComponentSystemGroup) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemSorter.cs:498)
Unity.Entities.ComponentSystemGroup:GenerateMasterUpdateList () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:484)
Unity.Entities.ComponentSystemGroup:RecurseUpdate () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:404)
Unity.Entities.ComponentSystemGroup:SortSystems () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/ComponentSystemGroup.cs:592)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:295)
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable () (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak

To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.


