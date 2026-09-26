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
system, kept as modular as possible, one `asmdef` per system so each
compiles independently:

```
Assets/
  MidManStudio/
    Gtg/
      CharacterController/
        Runtime/
        MidManStudio.Gtg.CharacterController.asmdef
      Weapons/
        Runtime/
          Melee/
          Ranged/
        MidManStudio.Gtg.Weapons.asmdef
      Camera/
        Runtime/
        MidManStudio.Gtg.Camera.asmdef
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

Each system's `asmdef` references only the packages it actually needs
(e.g. `Weapons` references `MidManStudio.ProjectileSystem` and
`MidManStudio.Utilities`; it does not reference `CharacterController`
unless it genuinely needs to call into it). Example, matching the shape
already used across `com.midmanstudio.*`:

```json
{
    "name": "MidManStudio.Gtg.Weapons",
    "rootNamespace": "MidManStudio.Gtg.Weapons",
    "references": [
        "MidManStudio.ProjectileSystem",
        "MidManStudio.Utilities"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "autoReferenced": true
}
```

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
  MidManStudio.Gtg.Weapons.asmdef
```

### Non-ECS packages are unaffected

`com.midmanstudio.questly`, `.inventorizz`, `.hudman`, `.gensettings`,
and `alembic`'s C# FFI bindings stay exactly as they are (plain C#, no
ECS). Only the systems that are actually part of the networked
simulation loop — movement and combat — need to be ECS-native. Something
like Heat or Local Favor affecting a chemistry check doesn't need a
rewrite; it just needs to be readable from wherever the ECS side queries
it.
