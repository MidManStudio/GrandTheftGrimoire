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
  Phase 0 (local movement only, no networking yet)
- [`GrandTheftGrimoire/chemistry.md`](GrandTheftGrimoire/chemistry.md)
  -- `MidManStudio.Gtg.Chemistry`, the game-side glue around
  `com.midmanstudio.alembic`; no code yet, currently design notes only

## CI and Workflows

- `.github/workflows/apply-replacements.yml` - applies `.mdix/replacements/`
  drops, same pipeline `unity-chem-sim` uses
