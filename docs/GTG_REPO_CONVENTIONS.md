# Grand Theft Grimoire — Repo Conventions

*Companion to `grand-theft-grimoire-design-reference.md` and
`grand-theft-grimoire-gameplay-reference.md`. Those two files own story,
world, and gameplay-system decisions. This file owns how the
`GrandTheftGrimoire` code repo itself is namespaced, laid out, and
documented — read it before adding a new system or folder.*

## 1. Namespace

Every script in `GrandTheftGrimoire` lives under `MidManStudio.Gtg.<System>` —
e.g. `MidManStudio.Gtg.CharacterController`, `MidManStudio.Gtg.Weapons`,
`MidManStudio.Gtg.Camera`, `MidManStudio.Gtg.Chemistry` (the game-side
glue around `MidManStudio.Alembic`, not the chemistry sim itself).

This is deliberately distinct from the `MidManStudio.<PackageName>`
namespaces used by the reusable `com.midmanstudio.*` packages (Alembic,
Utilities, ProjectileSystem, Netcode, Questly, Inventorizz, HudMan,
GenSettings, DragDrop). GTG is the consuming game, not another package —
if a GTG system later turns out to be game-agnostic enough to pull out
into its own `com.midmanstudio.*` package, that's a namespace change on
purpose, not an accident of starting them the same.

## 2. Folder layout

Mirrors the pattern already used for packages in `MidManStudio_Unity`'s
`PackageSandbox/Assets/MidManStudio/<PackageName>/` — one subfolder per
system, kept as modular as possible. The repo uses no asmdefs, see
"No asmdefs" below:

```
Assets/
  MidManStudio/
    Gtg/
      CharacterController/
        Runtime/
      Weapons/
        Runtime/
          Melee/
          Ranged/
      Camera/
        Runtime/
      ... (one folder per system, added as they're built)
docs/
  DOCUMENTATION_AND_COMMENTING_GUIDELINES.md   <- copied from unity-chem-sim, verbatim
  GrandTheftGrimoire.md                        <- index (see §4)
  GrandTheftGrimoire/
    character-controller.md
    weapons.md
    camera.md
    ...
```

### No asmdefs

The GTG repo does not use assembly definition files. All GTG code compiles
into the default `Assembly-CSharp` assembly, and code under an `Editor/`
folder compiles into `Assembly-CSharp-Editor`. Do not add a `.asmdef` to any
folder under `Assets/MidManStudio/Gtg/`.

Why: they kept causing problems and gave little back.

- A missing reference does not fail clearly. The NPC and the character
  controller assemblies both lacked `Unity.Entities.Hybrid`, where `Baker<T>`
  lives, and the nested `Baker` class then bound to itself and failed with
  CS0308.
- Every new system meant another reference list to keep in step with the
  packages in the manifest.
- Cross-system calls, such as a spell reading the character's look
  direction, needed a reference in one direction only, which the structure
  does not otherwise need.

What replaces them:

- One namespace per system, `MidManStudio.Gtg.<System>`, as in section 1.
- One folder per system, with the folder convention below.
- Dependencies flow one way and are written down in each system's doc. Today
  that is CharacterController, then Magic, then Chemistry, with Camera reading
  CharacterController and Magic.

Consequences to keep in mind:

- A compile error anywhere blocks every system.
- The reusable packages under `com.midmanstudio.*` keep their own asmdefs.
  They are auto referenced, so GTG code can use them without a reference.
- Inside `MidManStudio.Gtg` namespaces the simple name `Camera` resolves to
  the `MidManStudio.Gtg.Camera` namespace. Write `UnityEngine.Camera` in full.

Split a system's own `Runtime/` further (`Melee/`, `Ranged/`) the moment
it's doing more than one clear job — same rule as the commenting
guideline's file-structure section (§3 below).

## 3. Documentation & commenting

`GrandTheftGrimoire` adopts `unity-chem-sim`'s
`docs/DOCUMENTATION_AND_COMMENTING_GUIDELINES.md` as-is: same top-of-file
NOTICE header template, same inline-comment rules (explain the non-obvious,
skip the obvious, no fix-history in comments), same writing style (third
person or "we", no em dashes, no "leverage"/"utilize"/"seamless"). Copy
that file into this repo's own `docs/` folder rather than paraphrasing
it, so the two repos don't quietly drift apart.

Given GTG's expected size (a whole game, not one crate), it uses the
guideline's "large crate, split by part" layout from day one (§4 of the
guideline) instead of one monolithic doc file: `docs/GrandTheftGrimoire.md`
is the index (Overview, a list of parts with one-line descriptions and
links, and the repo-wide CI/workflow section), and each system gets its
own `docs/GrandTheftGrimoire/<system>.md` with that system's own Modules
section and its own Fixes-and-Problems section at the bottom — the same
split `com.midmanstudio.utilities` already uses for its own parts
(`docs/com.midmanstudio.utilities/singleton.md`, `.../logging.md`, etc.).

## 4. Design recording

The two living design docs already do, at the *design* level, what the
commenting guideline's doc files do at the *code* level:
`grand-theft-grimoire-design-reference.md` for story/world/character
decisions, `grand-theft-grimoire-gameplay-reference.md` for
systems/mechanics decisions. Nothing new is needed there — they keep
growing as design gets locked in, same as before.

The per-system `docs/GrandTheftGrimoire/<system>.md` files (§3) own the
*code*-level "why does it look like this" — the same split the guideline
already draws for `unity-chem-sim` between its `docs/architecture.md`
(design/data decisions) and its inline comments (code-local detail).
Where a piece of code exists specifically to satisfy a locked design
bullet, its doc section links back to that bullet by section number
(e.g. "see gameplay-reference.md §3, Magic Grimoire" on the
containment-slot code) instead of restating the design rationale in two
places that can drift apart.

## 5. ECS / DOTS and Netcode for Entities

> Status: ECS is paused while development hardware cannot run it. The ECS code is kept under `Assets/MidManStudio/Gtg/ECS/` behind `GTG_ECS`, and the game is built in parallel on the Managed stack under `MidManStudio.Gtg.Managed.<System>`, see `GrandTheftGrimoire/managed.md`. The direction below is unchanged.

Confirmed direction: GTG's core simulation (character movement, weapon
state, combat resolution) is built on Unity's Entity Component System
and networked with **Netcode for Entities** (`com.unity.netcode`), not
Netcode for GameObjects. Netcode for Entities requires ECS as its
foundation — there's no version of it that runs on plain MonoBehaviours —
so the "ECS or not" and "networked or not" decisions turned out to be
the same decision. `com.unity.netcode` (Netcode for Entities) supports
Unity 2022.3 LTS.

This is a different package from the studio's existing
`com.midmanstudio.netcode`, which is built on **Netcode for
GameObjects** (`Unity.Netcode` namespace, `NetworkBehaviour`,
`OwnerAuthoritativeNetworkTransform` — see `WeaponController.cs` /
`NetworkedDimensionPlayer.cs` in `MidManStudio_Unity`). The two netcode
packages are not interchangeable for the same authoritative state, so
GTG's networked gameplay does not reuse `com.midmanstudio.netcode` —
that package stays exactly as it is for whatever else it's used for.

### What has to be ECS vs what can stay MonoBehaviour

Mixing the two is well-supported, not a fight — Unity's own workflow is
built around this split, via **baking**: author data with a plain
`MonoBehaviour` "authoring" component in the Editor, a `Baker<T>`
converts it into ECS `IComponentData` once, and the GameObject is gone
by runtime — it never ships in the built game at all. The rule of
thumb: **anything Netcode for Entities needs to predict, sync, or roll
back has to be ECS. Anything purely local and cosmetic can stay
GameObject/MonoBehaviour.**

For character controller + weapons specifically:

| Lives in ECS (networked) | Stays MonoBehaviour (local/cosmetic) |
|---|---|
| Kinematic movement system (position, velocity, grounded state) | Cinemachine camera rig (follows a Transform synced from the entity each frame) |
| Input component (`IInputComponentData`) | Animator/rig — a **Companion GameObject** (`AddComponentObject`-attached), driven each frame by ECS state |
| Weapon state (equipped weapon, ammo, cooldown, aim direction) | VFX (muzzle flash, hit sparks), audio |
| Melee/hitscan resolution | HUD/UI |

Collision and ground checks for the kinematic controller, and any
melee/hitscan queries, go through **Unity Physics** (`com.unity.physics`),
the ECS-native physics package. Classic `UnityEngine.Physics`/`Rigidbody`
raycasts don't see ECS entities, so they can't be used for anything the
networked controller or weapons need to query.

### Packages this adds

- `com.unity.entities` — ECS core
- `com.unity.netcode` — Netcode for Entities
- `com.unity.physics` — collision/raycast queries for the kinematic controller and weapons
- `com.unity.entities.graphics` — only needed if entities render directly rather than through a Companion GameObject

### Folder convention addition

Within a system folder, ECS and presentation code get their own
subfolders, e.g.:

```
Weapons/
  Components/       <- IComponentData, IInputComponentData, ghost fields
  Systems/          <- ISystem (movement, fire resolution, prediction)
  Authoring/        <- MonoBehaviour authoring + Baker<T>
  Presentation/     <- Companion-GameObject-driven Animator/VFX/audio
```

### System structure

Each ECS system is a `partial struct` implementing `ISystem`. The per-entity
work lives in a nested `[BurstCompile] partial struct` implementing
`IJobEntity`, scheduled from `OnUpdate`. `SystemAPI.Query` loops are kept for
main thread work that touches managed objects, such as polling devices.

```csharp
[BurstCompile]
public partial struct BallSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new BallJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
    }

    [BurstCompile]
    public partial struct BallJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref BallData ball, ref LocalTransform transform)
        {
            transform = transform.Translate(ball.Direction * ball.Speed * DeltaTime);
        }
    }
}
```

Two rules go with it. A system never does structural changes in `OnCreate`,
it spawns and destroys through an `EntityCommandBuffer`, so a failed startup
leaves fewer half built worlds. A character module is one small system gated
by a feature flag, see `GrandTheftGrimoire/character-controller.md`.

### Non-ECS packages are unaffected

`com.midmanstudio.questly`, `.inventorizz`, `.hudman`, `.gensettings`,
and `alembic`'s C# FFI bindings stay exactly as they are (plain C#, no
ECS). Only the systems that are actually part of the networked
simulation loop — movement and combat — need to be ECS-native. Something
like Heat or Local Favor affecting a chemistry check doesn't need a
rewrite; it just needs to be readable from wherever the ECS side queries
it.
