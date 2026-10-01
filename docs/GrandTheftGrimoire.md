# GrandTheftGrimoire

## Overview

Index for `GrandTheftGrimoire`'s own per-system docs. See
`GTG_REPO_CONVENTIONS.md` for the namespace/folder/documentation
conventions this repo follows, and the two living design docs
(`grand-theft-grimoire-design-reference.md`,
`grand-theft-grimoire-gameplay-reference.md`) for story/world and
systems/mechanics decisions -- neither lives in this repo; this index
only covers the code-level docs that do.

## Parts

- [`GrandTheftGrimoire/character-controller.md`](GrandTheftGrimoire/character-controller.md)
  -- kinematic character controller, ECS/Netcode for Entities, currently
  Phase 0 (local movement and look only, no networking yet)
- [`GrandTheftGrimoire/camera.md`](GrandTheftGrimoire/camera.md)
  -- `MidManStudio.Gtg.Camera`, GameObject presentation of the ECS
  character and the Cinemachine third person and first person rig
- [`GrandTheftGrimoire/magic.md`](GrandTheftGrimoire/magic.md)
  -- `MidManStudio.Gtg.Magic`, spell casting and projectiles, first spell
  is a fireball that flies straight
- [`GrandTheftGrimoire/generalprobs.md`](GrandTheftGrimoire/generalprobs.md)
  -- running notes on Editor errors and warnings, with the current analysis
- [`chemistry.md`](chemistry.md)
  -- `MidManStudio.Gtg.Chemistry`, the game-side glue around
  `com.midmanstudio.alembic`; a fireball impact runs a short Alembic
  reaction and spawns a chemical hazard

## Repo rules

- No asmdefs under `Assets/MidManStudio/Gtg/`, see `GTG_REPO_CONVENTIONS.md`.
- ECS systems are `ISystem` structs with a nested `IJobEntity`, see the same file.

## CI and Workflows

- `.github/workflows/apply-replacements.yml` - applies `.mdix/replacements/`
  drops, same pipeline `unity-chem-sim` uses
