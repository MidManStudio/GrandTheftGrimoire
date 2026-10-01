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
New Dump

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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

TempJob leak at address 0x157ab7d00:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab7d80:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab7e00:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab7ec0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9600:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9680:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9700:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9780:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9800:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9880:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9900:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9980:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9a00:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9ac0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9b40:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x157ab9bc0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak

To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.

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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(20:3) was previously destroyed in world Editor World.
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

A meta data file (.meta) exists but its folder 'Assets/DefaultScene' can't be found, and has been created. Empty directories cannot be stored in version control, so it's assumed that the meta data file is for an empty directory in version control. When moving or deleting folders outside of Unity, please ensure that the corresponding .meta file is moved or deleted along with it.

A meta data file (.meta) exists but its asset 'Assets/DefaultScene.unity' can't be found. When moving or deleting files outside of Unity, please ensure that the corresponding .meta file is moved or deleted along with it.

Assembly for Assembly Definition File 'Packages/com.midmanstudio.alembic/Editor/MidManStudio.Alembic.Editor.asmdef' will not be compiled, because it has no scripts associated with it.
UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface:TickCompilationPipeline (UnityEditor.Scripting.ScriptCompilation.EditorScriptCompilationOptions,UnityEditor.BuildTargetGroup,UnityEditor.BuildTarget,int,string[],bool) (at /Users/bokken/build/output/unity/unity/Editor/Mono/Scripting/ScriptCompilation/EditorCompilationInterface.cs:221)

Leak Detected : TempJob allocates 78 individual allocations.

Leak Detected : Persistent allocates 3110 individual allocations.

Leak Detected : TransformAccessArray allocates 2 individual allocations.

Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:167] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyColliderEdges_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.CreateEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 c2a99021e1b9003db67e0e5cba47df11
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.CreateEntity_000011E9$BurstDirectCall:wrapper_native_indirect_0x7fede4510920 (intptr&,Unity.Entities.EntityComponentStore*,void*,Unity.Entities.Entity*,int)
 #3 ???
 #4 ???
 #5 ???
 #6 ???
 #7 ???
 #8 ???
 #9 ???
 #10 ???
 #11  (Mono JIT Code) (wrapper managed-to-native) System.Buffer:InternalMemcpy (byte*,byte*,int)
 #12 ???
 #13 ???
 #14 ???
 #15 ???
 #16 ???
 #17 ???
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 725d3475e185d4069e8b06178c1fda16
 #1  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #14 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #15 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #16 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #17 LoadSceneOperation::CompleteAwakeSequence()
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #4  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #5 mono_jit_runtime_invoke
 #6 do_runtime_invoke
 #7 mono_runtime_invoke
 #8 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #9 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #10 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #11 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #12 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #13 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #14 LoadSceneOperation::CompleteAwakeSequence()
 #15 LoadSceneOperation::CompletePreloadManagerLoadSceneEditor()
 #16 LoadSceneOperation::IntegrateMainThread()
 #17 PreloadManager::UpdatePreloadingSingleStep(PreloadManager::UpdatePreloadingFlags, int)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00

Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelMultiHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeParallelMultiHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Scenes.WeakAssetReferenceLoadingSystem.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 0d949dc424e3bc2ea15b8daf7133dcf1
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca5340 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 f079b00c9eeeec6a66b3d8987366de93
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 f079b00c9eeeec6a66b3d8987366de93


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelMultiHashMap`2[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[Unity.Physics.BoundingVolumeHierarchy+ElementLocationData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeParallelMultiHashMap`2[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[Unity.Physics.BoundingVolumeHierarchy+ElementLocationData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 f079b00c9eeeec6a66b3d8987366de93
 #5  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #6  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Cod

Found 2 leak(s) from callstack:
 #0 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 f079b00c9eeeec6a66b3d8987366de93
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #5 mono_jit_runtime_invoke
 #6 do_runtime_invoke
 #7 mono_runtime_invoke
 #8 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #9 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #10 MonoBehaviour::CallMethodIfAvailable(int)
 #11 MonoBehaviour::AddToManager()
 #12 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #13 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #14 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #15 MonoAddComponentWithType(GameObject&, ScriptingSystemTypeObjectPtr)
 #16 GameObject_CUSTOM_Internal_AddComponentWithType(ScriptingBackendNativeObjectPtrOpaque*, ScriptingBackendNativeObjectPtrOpaque*)
 #17  (Mono JIT Code) (wrapper managed-to-native) UnityEngine.GameObject:Internal_AddComponentWithType (UnityEngine.GameObject,System.Type)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00

Found 25 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:1017] Unity.Entities.WorldUnmanaged:Create (Unity.Entities.World,Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #6  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #7  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #13 mono_jit_runtime_invoke
 #14 do_runtime_invoke
 #15 mono_runtime_invoke
 #16 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #17 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelHashMap`2[[System.UInt32, netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51],[System.Int64, netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Collections.NativeParallelHashMap`2[[System.UInt32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Int64, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Systems.IntegrityCheckSystem, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.OnCreate(Unity.Physics.Systems.IntegrityCheckSystem*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 15446a5bec8a41eac6fbb5b465474fcb
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4c00 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:166] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyCollidersSystem_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGr

Found 1 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:1089] Unity.Entities.WorldUnmanaged:AllocateSystemStateForManagedSystem (Unity.Entities.World,Unity.Entities.ComponentSystemBase)
 #3 ???
 #4 ???
 #5 ???
 #6 ???
 #7 ???
 #8 ???
 #9 ???
 #10 ???
 #11 ???
 #12 ???
 #13 core::StringStorageDefault<char>::grow(unsigned long)
 #14 ???
 #15 ???
 #16 ???
 #17 ???
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:165] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyCollidersSystem_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00c9eeeec6a66b3d8987366de93
 #7  (

Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:169] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyCollidersSystem_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:68] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometryArray (Unity.Physics.Authoring.MeshType,Unity.Physics.Authoring.ColliderGeometry&)
 #1  (Mono JIT Code) [DrawColliderUtility.cs:165] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #2  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyCollidersSystem_Editor:__codegen__OnCreate (intptr,intptr)
 #3  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #9 mono_jit_runtime_invoke
 #10 do_runtime_invoke
 #11 mono_runtime_invoke
 #12 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #14 MonoBehaviour::CallMethodIfAvailable(int)
 #15 MonoBehaviour::AddToManager()
 #16 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #17 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0

Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldI

Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.CompanionGameObjectUpdateTransformSystem, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.OnCreate(Unity.Entities.CompanionGameObjectUpdateTransformSystem*, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 725d3475e185d4069e8b06178c1fda16
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[System.Int32, netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.DynamicsWorld, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.DynamicsWorld*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numMotions, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numJoints) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 f079b00c9eeeec6a66b3d8987366de93
 #5  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #6  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr,

Found 5 leak(s) from callstack:
 #0 Unity.Collections.AutoFreeAllocator, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Try(Unity.Collections.AutoFreeAllocator*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+Block&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null block) -> System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 07fd39dcd121e0de66cd5435146dd2c2
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #6  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #7  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #12  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #13 mono_jit_runtime_invoke
 #14 do_runtime_invoke
 #15 mono_runtime_invoke
 #16 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #17 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #18 ???
 #19 LeakDetection::Record(void*, NativeCollection::LeakCategory, int)


Found 5 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:307] Unity.Entities.World:Dispose ()
 #6  (Mono JIT Code) [World.cs:330] Unity.Entities.World:DisposeAllWorlds ()
 #7  (Mono JIT Code) [DefaultWorldInitializationProxy.cs:29] Unity.Entities.DefaultWorldInitializationProxy:OnDisable ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::InvokeChecked(ScriptingExceptionPtr*)
 #14 SerializableManagedRef::CallMethod(Object&, ScriptingMethodPtr)
 #15 MonoBehaviour::RemoveFromManager()
 #16 GameObject::ActivateAwakeRecursivelyInternal(DeactivateOperation, AwakeFromLoadQueue&)
 #17 GameObject::ActivateAwakeRecursively(DeactivateOperation)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00

Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToR

Found 2 leak(s) from callstack:
 #0 725d3475e185d4069e8b06178c1fda16
 #1  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:166] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyColliderEdges_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelMultiHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeParallelMultiHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Scenes.WeakAssetReferenceLoadingSystem.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 0d949dc424e3bc2ea15b8daf7133dcf1
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca5340 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:68] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometryArray (Unity.Physics.Authoring.MeshType,Unity.Physics.Authoring.ColliderGeometry&)
 #1  (Mono JIT Code) [DrawColliderUtility.cs:165] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #2  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyColliderEdges_Editor:__codegen__OnCreate (intptr,intptr)
 #3  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #9 mono_jit_runtime_invoke
 #10 do_runtime_invoke
 #11 mono_runtime_invoke
 #12 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #14 MonoBehaviour::CallMethodIfAvailable(int)
 #15 MonoBehaviour::AddToManager()
 #16 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #17 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldI

Found 1 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:595] Unity.Entities.WorldUnmanagedImpl:CreateUnmanagedSystem (Unity.Entities.SystemTypeIndex,long,bool)
 #3  (Mono JIT Code) [WorldUnmanaged.cs:674] Unity.Entities.WorldUnmanagedImpl:CreateUnmanagedSystem (Unity.Entities.SystemTypeIndex,bool)
 #4  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #9 mono_jit_runtime_invoke
 #10 do_runtime_invoke
 #11 mono_runtime_invoke
 #12 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #14 MonoBehaviour::CallMethodIfAvailable(int)
 #15 MonoBehaviour::AddToManager()
 #16 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #17 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #18 ???
 #19 scripting_icall_string_to_utf8(ScriptingReferenceWrapper<MonoString*>)


Found 25 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:1017] Unity.Entities.WorldUnmanaged:Create (Unity.Entities.World,Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #6  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #7  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #12  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #13 mono_jit_runtime_invoke
 #14 do_runtime_invoke
 #15 mono_runtime_invoke
 #16 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #17 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Scenes.WeakAssetReferenceLoadingData+LoadedPrefab, Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Scenes.WeakAssetReferenceLoadingData+LoadedPrefab, Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Scenes.WeakAssetReferenceLoadingSystem.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 0d949dc424e3bc2ea15b8daf7133dcf1
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca5340 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 10 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #18 ???
 #19 ???


Found 10 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


Found 23 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 24 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:307] Unity.Entities.World:Dispose ()
 #5  (Mono JIT Code) [World.cs:330] Unity.Entities.World:DisposeAllWorlds ()
 #6  (Mono JIT Code) [DefaultWorldInitializationProxy.cs:29] Unity.Entities.DefaultWorldInitializationProxy:OnDisable ()
 #7 mono_jit_runtime_invoke
 #8 do_runtime_invoke
 #9 mono_runtime_invoke
 #10 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #11 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::InvokeChecked(ScriptingExceptionPtr*)
 #13 SerializableManagedRef::CallMethod(Object&, ScriptingMethodPtr)
 #14 MonoBehaviour::RemoveFromManager()
 #15 GameObject::ActivateAwakeRecursivelyInternal(DeactivateOperation, AwakeFromLoadQueue&)
 #16 GameObject::ActivateAwakeRecursively(DeactivateOperation)
 #17 GameObject::Deactivate(DeactivateOperation)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #1  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #2  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #6  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #7 mono_jit_runtime_invoke
 #8 do_runtime_invoke
 #9 mono_runtime_invoke
 #10 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #11 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #12 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #13 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #14 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #15 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #16 LoadSceneOperation::CompleteAwakeSequence()
 #17 LoadSceneOperation::CompletePreloadManagerLoadSceneEditor()
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Entities.CompanionGameObjectUpdateTransformSystem, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.OnCreate(Unity.Entities.CompanionGameObjectUpdateTransformSystem*, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 725d3475e185d4069e8b06178c1fda16
 #4  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #5  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #11 mono_jit_runtime_invoke
 #12 do_runtim

Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[System.Int32, netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 f079b00c9eeeec6a66b3d8987366de93
 #4  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #5  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #10  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #11 mono_jit_runtime_invoke
 #12 do_runtime_invoke
 #13 mono_runtime_invoke
 #14 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #15 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #16 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #17 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelMultiHashMap`2[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[Unity.Physics.BoundingVolumeHierarchy+ElementLocationData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeParallelMultiHashMap`2[[System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[Unity.Physics.BoundingVolumeHierarchy+ElementLocationData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 f079b00c9eeeec6a66b3d8987366de93
 #5  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #6  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Cod

Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00c9eeeec6a66b3d8987366de93
 #7  (

Found 5 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:1017] Unity.Entities.WorldUnmanaged:Create (Unity.Entities.World,Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #6 mono_jit_runtime_invoke
 #7 do_runtime_invoke
 #8 mono_runtime_invoke
 #9 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #10 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #11 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #12 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #13 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #14 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #15 LoadSceneOperation::CompleteAwakeSequence()
 #16 LoadSceneOperation::CompletePreloadManagerLoadSceneEditor()
 #17 LoadSceneOperation::IntegrateMainThread()
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.AutoFreeAllocator, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Try(Unity.Collections.AutoFreeAllocator*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+Block&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null block) -> System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 07fd39dcd121e0de66cd5435146dd2c2
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #3 ???
 #4 ???
 #5 ???
 #6 ???
 #7 ???
 #8 ???
 #9 ???
 #10  (Mono JIT Code) [UnsafeParallelHashMap.cs:204] Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData:ReallocateHashMap<long, uint16> (Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData*,int,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11 ???
 #12 ???
 #13  (Mono JIT Code) [UnsafeParallelHashMap.cs:204] Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData:ReallocateHashMap<long, uint16> (Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData*,int,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #14 ???
 #15 mono_breakpoint_clean_code
 #16 ???
 #17  (Mono JIT Code) [UnsafeParallelHashMap.cs:204] Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData:ReallocateHashMap<long, uint16> (Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData*,int,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.CreateEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 c2a99021e1b9003db67e0e5cba47df11
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.CreateEntity_000011E9$BurstDirectCall:wrapper_native_indirect_0x7fede4510920 (intptr&,Unity.Entities.EntityComponentStore*,void*,Unity.Entities.Entity*,int)
 #3 ???
 #4 ???
 #5 ???
 #6 ???
 #7 ???
 #8 ???
 #9 ???
 #10 ???
 #11 ???
 #12 Unity.Burst.SharedStatic`1<Unity.Entities.EntityComponentStore/PerChunkArray> Unity.Entities.EntityComponentStore/PerChunkArray/StaticIdentifier::Ref
 #13 ???
 #14 ???
 #15 ???
 #16 ???
 #17 Unity.Entities.Archetype, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.AddToChunkList(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.SharedComponentValues, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null sharedComponentIndices, System.UInt32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 changeVersion, Unity.Entities.EntityComponentStore+ChunkListChanges&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null changes) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #18 ???
 #19 ???


Found 5 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:1017] Unity.Entities.WorldUnmanaged:Create (Unity.Entities.World,Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #6 mono_jit_runtime_invoke
 #7 do_runtime_invoke
 #8 mono_runtime_invoke
 #9 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #10 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #11 MonoBehaviour::CallMethodIfAvailable(int)
 #12 MonoBehaviour::AddToManager()
 #13 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #14 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #15 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #16 MonoAddComponentWithType(GameObject&, ScriptingSystemTypeObjectPtr)
 #17 GameObject_CUSTOM_Internal_AddComponentWithType(ScriptingBackendNativeObjectPtrOpaque*, ScriptingBackendNativeObjectPtrOpaque*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #1  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #2  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #7 mono_jit_runtime_invoke
 #8 do_runtime_invoke
 #9 mono_runtime_invoke
 #10 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #11 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #12 MonoBehaviour::CallMethodIfAvailable(int)
 #13 MonoBehaviour::AddToManager()
 #14 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #15 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #16 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #17 MonoAddComponentWithType(GameObject&, ScriptingSystemTypeObjectPtr)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 f079b00

Found 6 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede44ff080 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


Found 5 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #4  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #18 ???
 #19 ???


Found 5 leak(s) from callstack:
 #0 Unity.Collections.AutoFreeAllocator, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Try(Unity.Collections.AutoFreeAllocator*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+Block&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null block) -> System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 07fd39dcd121e0de66cd5435146dd2c2
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #6  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #7  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #13 mono_jit_runtime_invoke
 #14 do_runtime_invoke
 #15 mono_runtime_invoke
 #16 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #17 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #18 ???
 #19 LeakDetection::Record(void*, NativeCollection::LeakCategory, int)


Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:167] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyCollidersSystem_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGr

Found 3 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:169] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyColliderEdges_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToR

Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToR

Found 2 leak(s) from callstack:
 #0  (Mono JIT Code) [DrawColliderUtility.cs:165] Unity.Physics.Authoring.DrawColliderUtility:CreateGeometries (Unity.Physics.Authoring.PrimitiveColliderGeometries&)
 #1  (Mono JIT Code) Unity.Physics.Authoring.DisplayBodyColliderEdges_Editor:__codegen__OnCreate (intptr,intptr)
 #2  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #8 mono_jit_runtime_invoke
 #9 do_runtime_invoke
 #10 mono_runtime_invoke
 #11 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #12 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #13 MonoBehaviour::CallMethodIfAvailable(int)
 #14 MonoBehaviour::AddToManager()
 #15 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #16 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #17 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Entities.CompanionGameObjectUpdateTransformSystem, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.OnCreate(Unity.Entities.CompanionGameObjectUpdateTransformSystem*, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 725d3475e185d4069e8b06178c1fda16
 #4  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #5  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #10  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #11 mono_jit_runtime_invoke
 #12 do_runtime_invoke
 #13 mono_runtime

Found 1 leak(s) from callstack:
 #0 Unity.Collections.AutoFreeAllocator, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Try(Unity.Collections.AutoFreeAllocator*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+Block&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null block) -> System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 07fd39dcd121e0de66cd5435146dd2c2
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #6 mono_jit_runtime_invoke
 #7 do_runtime_invoke
 #8 mono_runtime_invoke
 #9 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #10 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #11 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #12 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #13 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #14 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #15 LoadSceneOperation::CompleteAwakeSequence()
 #16 LoadSceneOperation::CompletePreloadManagerLoadSceneEditor()
 #17 LoadSceneOperation::IntegrateMainThread()
 #18 ???
 #19 LeakDetection::Record(void*, NativeCollection::LeakCategory, int)


Found 5 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #3  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #4  (Mono JIT Code) [ComponentSystemBase.cs:210] Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.BoundingVolumeHierarchy+Node, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0

Found 1 leak(s) from callstack:
 #0 Unity.Collections.AutoFreeAllocator, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Try(Unity.Collections.AutoFreeAllocator*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+Block&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null block) -> System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 07fd39dcd121e0de66cd5435146dd2c2
 #2  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #3  (Mono JIT Code) [World.cs:240] Unity.Entities.World:Init (Unity.Entities.WorldFlags,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:214] Unity.Entities.World:.ctor (string,Unity.Entities.WorldFlags)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #6 mono_jit_runtime_invoke
 #7 do_runtime_invoke
 #8 mono_runtime_invoke
 #9 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #10 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #11 MonoBehaviour::CallMethodIfAvailable(int)
 #12 MonoBehaviour::AddToManager()
 #13 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #14 AddComponentUnchecked(GameObject&, Unity::Type const*, ScriptingClassPtr, MonoScript*, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*)
 #15 AddComponent(GameObject&, Unity::Type const*, ScriptingClassPtr, core::basic_string<char, core::StringStorageDefault<char> >*, AwakeFromLoadQueue*, char const*, dynamic_array<Unity::Component*, 0ul>*)
 #16 MonoAddComponentWithType(GameObject&, ScriptingSystemTypeObjectPtr)
 #17 GameObject_CUSTOM_Internal_AddComponentWithType(ScriptingBackendNativeObjectPtrOpaque*, ScriptingBackendNativeObjectPtrOpaque*)
 #18 ???
 #19 LeakDetection::Record(void*, NativeCollection::LeakCategory, int)


Found 5 leak(s) from callstack:
 #0 Unity.Entities.Archetype, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.AddToChunkList(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.SharedComponentValues, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null sharedComponentIndices, System.UInt32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 changeVersion, Unity.Entities.EntityComponentStore+ChunkListChanges&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null changes) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.AddEmptyChunk(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.SharedComponentValues, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null sharedComponentValues) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.CreateEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 c2a99021e1b9003db67e0e5cba47df11
 #4  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.CreateEntity_000011E9$BurstDirectCall:wrapper_native_indirect_0x7fede4510920 (intptr&,Unity.Entities.EntityComponentStore*,void*,Unity.Entities.Entity*,int)
 #5 ???
 #6 ???
 #7 ???
 #8 ???
 #9 ???
 #10  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.CreateEntity_000011E9$BurstDirectCall:wrapper_native_indirect_0x7fede4510920 (intptr&,Unity.Entities.EntityComponentStore*,void*,Unity.Entities.Entity*,int)
 #11 ???
 #12 ???
 #13 ???
 #14 ???
 #15 ???
 #16 ???
 #17 ???
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Physics.CollisionFilter, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.SetCapacity(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 Unity.Physics.Broadphase+Tree, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Broadphase+Tree*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 numBodies, Unity.Collections.Allocator, UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #3 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #4 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 f079b00c9eeeec6a66b3d8987366de93
 #6  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #7  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToR

Found 2 leak(s) from callstack:
 #0 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 f079b00c9eeeec6a66b3d8987366de93
 #2  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #3  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #4  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #8  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #9 mono_jit_runtime_invoke
 #10 do_runtime_invoke
 #11 mono_runtime_invoke
 #12 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #14 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #15 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #16 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #17 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 1 leak(s) from callstack:
 #0 Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Initialize<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.NativeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 initialCapacity, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.CompanionGameObjectUpdateTransformSystem, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.OnCreate(Unity.Entities.CompanionGameObjectUpdateTransformSystem*, Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Transforms.Hybrid, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 725d3475e185d4069e8b06178c1fda16
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca40c0 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 MonoBehaviour::AddToManager()


Found 1 leak(s) from callstack:
 #0 07fd39dcd121e0de66cd5435146dd2c2
 #1  (Mono JIT Code) (wrapper managed-to-native) Unity.Collections.AutoFreeAllocator/Unity.Collections.Try_00000100$BurstDirectCall:wrapper_native_indirect_0x7fedc4e77160 (intptr&,intptr,Unity.Collections.AllocatorManager/Block&)
 #2  (Mono JIT Code) [WorldUnmanaged.cs:595] Unity.Entities.WorldUnmanagedImpl:CreateUnmanagedSystem (Unity.Entities.SystemTypeIndex,long,bool)
 #3  (Mono JIT Code) [WorldUnmanaged.cs:674] Unity.Entities.WorldUnmanagedImpl:CreateUnmanagedSystem (Unity.Entities.SystemTypeIndex,bool)
 #4  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #8  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #9 mono_jit_runtime_invoke
 #10 do_runtime_invoke
 #11 mono_runtime_invoke
 #12 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #13 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #14 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #15 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #16 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #17 CallbackArray4<int const, AwakeFromLoadQueue&, SceneLoadingMode, bool>::Invoke(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19  (Mono JIT Code) [UnsafeParallelHashMap.cs:728] Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapBase`2<long, uint16>:TryAdd (Unity.Collections.LowLevel.Unsafe.UnsafeParallelHashMapData*,long,uint16,bool,Unity.Collections.AllocatorManager/AllocatorHandle)


Found 3 leak(s) from callstack:
 #0 Unity.Physics.Systems.PhysicsWorldData, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Physics.Systems.PhysicsWorldData*, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.SystemState&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null state, Unity.Physics.PhysicsWorldIndex&, Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null worldIndex) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Physics.Systems.BuildPhysicsWorld.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Physics, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 f079b00c9eeeec6a66b3d8987366de93
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca4940 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [AutomaticWorldBootstrap.cs:17] Unity.Entities.AutomaticWorldBootstrap:Initialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 RuntimeInitializeOnLoadManager::Execute(RuntimeInitializeOnLoadCall const&)
 #16 RuntimeInitializeOnLoadManager::ExecuteInitializeOnLoad(RuntimeInitializeLoadType)
 #17 RuntimeInitializeOnSceneLoadedBeforeAwake(int, AwakeFromLoadQueue&, SceneLoadingMode, bool)
 #18 ???
 #19 ???


Found 2 leak(s) from callstack:
 #0 Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Scenes.WeakAssetReferenceLoadingData+LoadedPrefab, Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null..ctor(Unity.Collections.NativeParallelHashMap`2[[Unity.Entities.Serialization.EntityPrefabReference, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null],[Unity.Scenes.WeakAssetReferenceLoadingData+LoadedPrefab, Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 capacity, Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Scenes.WeakAssetReferenceLoadingSystem.__codegen__OnCreate(System.IntPtr self, System.IntPtr state) -> void_42509ef455527178a788510ba1cb0606 from Unity.Scenes, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 0d949dc424e3bc2ea15b8daf7133dcf1
 #3  (Mono JIT Code) (wrapper managed-to-native) object:wrapper_native_0x158ca5340 (intptr,intptr)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SetupScriptForIManagedObjectHost(Object*, ScriptingClassPtr, MonoScript*)
 #18 ???
 #19 ???


InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #10  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #11  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #12  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #13  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #14  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #15  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #16 mono_jit_runtime_invoke
 #17 do_runtime_invoke
 #18 mono_runtime_invoke
 #19 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #20 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #21 MonoBehaviour::CallMethodIfAvailable(int)
 #22 MonoBehaviour::AddToManager()
 #23 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #24 MonoManager::FinalizeReload()
 #25 ScriptingInitializer::FinalizeReload()
 #26 RefreshIntern
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
Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Entities/DefaultWorldInitialization.cs:356)
Unity.Scenes.SubScene:OnEnable() (at ./Library/PackageCache/com.unity.entities@1.3.10/Unity.Scenes/SubScene.cs:317)

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

InvalidOperationException: System.InvalidOperationException: Illegal instruction executed
This Exception was thrown from a function compiled with Burst, which has limited exception support.
 #3 burst_abort_deferred()
 #4 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.RemoveFromEnabledBitsHierarchicalData(Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 startIndex, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #5 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DeallocateDataEntitiesInChunk(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.ChunkIndex, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null chunk, Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 indexInChunk, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 batchCount) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #6 Unity.Entities.ChunkDataUtility, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.Deallocate(Unity.Entities.Archetype*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null archetype, Unity.Entities.EntityBatchInChunk&, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null batch) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #7 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #8 7f7833cbf7de76462ac9a76fb532a7d2
 #9  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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

TempJob leak at address 0x1fdc90a80:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90b00:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90b80:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90c00:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90c80:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90d40:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90dc0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90e40:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90ec0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90f40:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc90fc0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc91040:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc8f1c0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc8f240:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc8f2c0:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [World.cs:1282] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #5  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #6  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #7  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #10 mono_jit_runtime_invoke
 #11 do_runtime_invoke
 #12 mono_runtime_invoke
 #13 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #14 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #15 MonoBehaviour::CallMethodIfAvailable(int)
 #16 MonoBehaviour::AddToManager()
 #17 SerializableManagedRefsUtilities::AwakeInstancesAfterBackupRestoration(DomainReloadingData const&)
 #18 ???
 #19 ???


TempJob leak at address 0x1fdc8f340:
 #0 Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.ResizeExact<Unity.Collections.AllocatorManager+AllocatorHandle, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null>(Unity.Collections.LowLevel.Unsafe.UnsafeList`1[[Unity.Entities.Entity, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]]*, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Collections.AllocatorManager+AllocatorHandle&, Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null allocator, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 newCapacity) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_869e2526097689aacbeb19602b6f33d0 from Unity.Collections, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #1 Unity.Entities.EntityComponentStore, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null.DestroyEntities(Unity.Entities.EntityComponentStore*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null this, Unity.Entities.Entity*, Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null entities, System.Int32, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089 count) -> System.Void, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089_5c5dd5b9612805025ece83b0d90cff0f from Unity.Entities, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
 #2 7f7833cbf7de76462ac9a76fb532a7d2
 #3  (Mono JIT Code) (wrapper managed-to-native) Unity.Entities.StructuralChange/Unity.Entities.DestroyEntity_000011EA$BurstDirectCall:wrapper_native_indirect_0x7fede61de648 (intptr&,Unity.Entities.EntityComponentStore*,Unity.Entities.Entity*,int)
 #4  (Mono JIT Code) [WorldUnmanaged.cs:457] Unity.Entities.WorldUnmanagedImpl:DestroyManagedSystem (Unity.Entities.SystemState*)
 #5  (Mono JIT Code) [World.cs:464] Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase)
 #6  (Mono JIT Code) [World.cs:1291] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle)
 #7  (Mono JIT Code) [World.cs:1321] Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle)
 #8  (Mono JIT Code) [DefaultWorldInitialization.cs:255] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups)
 #9  (Mono JIT Code) [DefaultWorldInitialization.cs:294] Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>)
 #10  (Mono JIT Code) [DefaultWorldInitialization.cs:149] Unity.Entities.DefaultWorldInitialization:Initialize (string,bool)
 #11  (Mono JIT Code) [DefaultWorldInitialization.cs:361] Unity.Entities.DefaultWorldInitialization:DefaultLazyEditModeInitialize ()
 #12 mono_jit_runtime_invoke
 #13 do_runtime_invoke
 #14 mono_runtime_invoke
 #15 scripting_method_invoke(ScriptingMethodPtr, ScriptingObjectPtr, ScriptingArguments&, ScriptingExceptionPtr*, bool)
 #16 ScriptingInvocation::Invoke(ScriptingExceptionPtr*, bool)
 #17 MonoBehaviour::CallMethodIfAvailable(int)
 #18 ???
 #19 ???


Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak

To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.

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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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

ArgumentException: The entity does not exist. Entity(18:1) was previously destroyed in world Editor World.
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




