# Reference Material

External reference material the studio has collected — papers, guides,
seminar decks — organized by topic. Not studio-authored docs (those
live under each package's own `docs/`); this is material we didn't
write, kept around because it's genuinely useful.

## Structure

```
reference/
  README.md
  netcode/
  ecs/
  input-system/
  shader-graph/
  rendering/
```

One subfolder per topic. Add a new topic folder when the first file for
it shows up — don't pre-create empty ones beyond what's listed below.

## Index

### netcode/

- **`netcode-for-entities-vs-gameobjects-thesis.pdf`** — Alex Šunjajev,
  *Multiplayer Game Development in Unity: Comparing Netcode for Game
  Objects and Netcode for Entities* (Tallinn University of Technology,
  Bachelor's thesis, 2025). Built two small multiplayer games — four
  players, AI enemies — one on each framework, and benchmarked them.
  Key takeaways: NGO has lower overhead and wins at low enemy counts;
  NFE's multithreading lets it scale much further as enemy counts climb
  into the hundreds. Also confirms something worth remembering for GTG
  directly — as of the version tested, DOTS has no native story for UI,
  cameras, or animation; those need a hybrid approach or a third-party
  asset, and baking rigged/animated characters can cause rendering
  issues. Matches the ECS/MonoBehaviour split in
  `GrandTheftGrimoire`'s own conventions doc.

  (This file was uploaded as `836390b820374f8187df85a3f3e18e79_pdf.json`
  — it's not JSON, just a real PDF saved with the wrong extension.
  Rename it before it goes in the repo.)

### ecs/

- **`unity-ecs-intro-seminar-nael.pdf`** — Daniel Nael, "Unity ECS"
  computer graphics seminar slides. Solid conceptual intro
  (Entity/Component/System, data-oriented vs. object-oriented memory
  layout), but sourced against `com.unity.entities@0.14` — a pre-1.0
  preview version that predates the current Baking/Authoring workflow.
  Treat it as conceptual background, not a source for current API
  patterns.

### input-system/

_(empty for now — see the note below)_

### shader-graph/, rendering/

_(empty until the next batch of docs comes in)_

## A note on the two Scribd exports

Two uploads — `Unity_Netcode_Complete_Reference...pdf` and
`Unity_Input_System__Mouse___Keyboard_Guide...pdf` — turned out to be
browser print-to-PDF captures of Scribd's paywalled preview page, not
the underlying document. Every "page" is the same page-4 (or page-2)
preview plus the site's own UI chrome — Search, "Download free for 30
days," Overview / Find / Related documents / Ask AI — repeated. No real
Netcode or Input System content made it into either file, so neither is
in the index above. If they're worth having, they'd need an actual
purchased/downloaded export, or the original source PDF if it exists
somewhere other than Scribd.
