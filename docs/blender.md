# Blender export (`tools/blender/`)

> Code location: `tools/blender/`. These are Python scripts for Blender 3.3 or newer. They run inside Blender, not in Unity, and nothing under `Assets/` depends on them.

## Overview

`gtg_prep_male_blocky.py` turns the `Male_Blocky` collection of `Gtg_Character_Base_Template.blend` into one FBX that Unity can import as a skinned character. The source file has parts and a rig but no skinning, so the script does the skinning too.

- **The scene is not changed.** All work happens on temporary copies that are removed at the end. The script never saves the `.blend`. It adds one small text block, `GTG_prep_report`, to the open file.
- **One file out.** `Gtg_Male_Blocky.fbx` plus a report next to it.
- **Male blocky only.** The female body has different proportions and no armature in the source, so she gets her own rig and her own run later.
- **No Unity code yet.** The import settings and the appearance builder come in the next drop.

## Run it

In Blender:

1. Open `Gtg_Character_Base_Template.blend`.
2. Switch to the Scripting workspace and open `gtg_prep_male_blocky.py` in the Text Editor.
3. Press Run Script.

The FBX is written next to the `.blend` as `Gtg_Male_Blocky.fbx`. The report is written next to it as `Gtg_Male_Blocky_prep_report.txt` and is also in the Text Editor under `GTG_prep_report`. If the file was never saved, the output goes to the home folder.

Headless, for a build machine:

```
blender -b Gtg_Character_Base_Template.blend --python gtg_prep_male_blocky.py -- --out /path/Gtg_Male_Blocky.fbx
```

Options after the double dash: `--out FILE`, `--keep` (leave the temporary objects in the scene so they can be looked at), `--no-verify` (skip the re-import check). A run that was started in Edit Mode ends in Object Mode, with the same objects selected as before.

## Contract with the Unity side

The appearance builder will rebind parts to the base skeleton by bone name, so these names are fixed from here on. The exported hierarchy has 56 bones:

```
Root
  Hip
    Spine > Chest > Neck > Head
                   Clavicle.L > Arm_Upper.L > Arm_Lower.L > Wrist.L > 5 fingers x 3 bones
                   Clavicle.R > Arm_Upper.R > Arm_Lower.R > Wrist.R > 5 fingers x 3 bones
                   Chest.L (under Clavicle.L), Chest.R (under Clavicle.R)
    Leg_Upper.L > Leg_Lower.L > Foot.L > Toe.L
    Leg_Upper.R > Leg_Lower.R > Foot.R > Toe.R
    Waist.L, Waist.R
```

Finger bones are `ThumbFinger_n`, `IndexFinger_n`, `MiddleFinger_n`, `RingFinger_n` and `PinkyFinger_n` with n from 1 to 3 and a `.L` or `.R` suffix.

- **Size.** The body is 1.8017 m tall, 1.72 m wide with the arms out and 0.44 m deep. The soles are at z = 0 and `Root` is at the ground.
- **Facing.** The front of the character is Blender -Y. With the exporter axes in `FBX_OPTIONS` the file records Y up (UpAxis 1) and a front axis of Z (FrontAxis 2, sign 1). That this ends up as Unity +Z is derived from the file, not seen in Unity.
- **Units.** The file records a unit scale of 100 (centimetres), which Unity converts with the importer's file scale on.
- **Nodes.** `Body` and `GTG_Rig` are exported with a rotation of -90 on X, and `Root` with +90. That is the exporter's axis conversion. Unity's `ModelImporter` has a `bakeAxisConversion` setting that moves the conversion into the geometry instead of the root object (2022.3 script reference).

## Modules

### gtg_prep_male_blocky.py

Reads the parts, fixes the rig, skins the parts to it, scales the result and exports one FBX.

#### Steps

1. **Read the parts.** Each mesh listed in `PARTS` is copied and shown, then evaluated so its Mirror modifier is applied. This works for hidden source objects too. The result is one list of world-space vertices, faces and UVs, plus a list of islands (connected pieces, one per block).
2. **Copy the rig and fix it.** The `Armature` object is copied, then:
   - the two name typos are fixed: `RingFibger_1.*` becomes `RingFinger_1.*` and `ThumbFinger_1.*.001` becomes `ThumbFinger_3.*`;
   - `Foot.*` moves under `Leg_Lower.*` and `Wrist.*` under `Arm_Lower.*` (they hung under IK bones);
   - `Hip` moves to the middle between the leg roots and the start of `Spine`, so Hips and Spine no longer share one position;
   - the eight Blender-only helper bones are removed: `Leg_IK`, `Leg_Target`, `Wrist_IK` and `Arm_IK`, left and right.
3. **Weight the vertices.** Weights are rigid, one bone per island, with two exceptions (torso and hands). See the weight rules below.
4. **Move and scale.** The mesh and rig are shifted so the lowest point of the mesh is at z = 0, then scaled by 0.2. `Root` is placed exactly at z = 0.
5. **Build the Body object.** One mesh, `Body`, with a vertex group per weighted bone, an Armature modifier, and the rig as its parent. Materials are `Gtg_Face` (the head block) and `Gtg_Skin` (everything else).
6. **Export** with the options in `FBX_OPTIONS`. Options the running Blender does not have are left out and named in the report.
7. **Check.** The FBX is imported back into the scene, measured, and removed again.

#### Weight rules

| Part (object name) | Rule |
|---|---|
| `Face`, `Ear` | all vertices to `Head` |
| `Neck` | `Neck` |
| `Torso` | `Spine` below the blend band, `Chest` above it, a linear blend in between. The band is `TORSO_BLEND` (0.7 source units) wide and centred on the start of `Chest`. |
| `Pelvis_N` | `Hip` |
| `Theigh` | `Leg_Upper.L` or `.R`, by the side of the island |
| `Leg.002` | `Leg_Lower.L` or `.R` |
| `Feet` | `Foot.L` or `.R` (`Toe` gets no vertices) |
| `Bicep` | `Arm_Upper.L` or `.R` (this block is the upper arm) |
| `Tricep` | `Arm_Lower.L` or `.R` (this block is the forearm) |
| `Hand` | the palm island to `Wrist.*`. Each finger island picks its finger, then every vertex goes to the nearest of that finger's three bones. |

Left and right follow the Blender rule: positive X is `.L`.

#### Checks

The script stops with `STOPPED: ...` and a named reason, and changes nothing in the scene, if an expected object or bone is missing, if a sided part sits on the centre line, or if any vertex ends up without weights that sum to 1.

After the export it imports the FBX back and lists each check as `ok` or `FAIL` in the report:

- one armature and one mesh in the file;
- the bone names match the rig (56) and no IK helper bone is present;
- the vertex count is unchanged (2666) and the vertex groups and Armature modifier are present;
- the mesh stands on z = 0 and is about 1.8 m tall after import.

The report also lists the vertices per bone, the bones that carry no vertices, and a `WARNING` for any island of a fixed or sided part whose assigned bone is not the nearest bone to it.

#### Verified in the sandbox

Run on a copy of `Gtg_Character_Base_Template.blend` with Blender 4.2.0 and 5.0.1:

- the script runs to `RESULT: OK`, and the scene afterwards has no new objects, meshes, armatures, collections or materials (only the report text block);
- a missing bone stops the run cleanly with no FBX written;
- running again after `--keep` cleans up the earlier copies;
- a run started in Edit Mode, and a run with every source part visible, both work;
- in the re-imported file the rest pose matches the mesh to 7e-7, and posing one bone moves only the expected vertices (forearm, hand and fingers for `Arm_Lower.L`, 48 vertices for `Leg_Upper.L`, 1024 for `Head`, all 2666 for `Hip`).

#### Not verified

- **Blender 3.3.** The `.blend` was last saved in 3.3.6, so that is the version Su runs. The sandbox has no 3.3 build. The script uses only calls that exist in 3.3 and filters FBX options against what the running Blender offers. A first run in 3.3 is still a test.
- **Unity.** Nothing here has been imported into Unity: the node rotations, the facing direction and the import scale are unchecked. The Humanoid avatar mapping is also unchecked, since names such as `Hip`, `Wrist` and `ThumbFinger_3` may not map automatically. If they do not, they are mapped by hand or by an importer script.
- **Textures.** UVs are copied as they are. Nine of the eleven parts each cover a large part of the 0-1 square, so a texture would repeat on all of them. The UVs need packing before texturing, and the face atlas needs its own UV area on the head block.

#### Known limits

- Bones with no vertices stay in the rig: `Clavicle.*`, `Chest.*`, `Waist.*` and `Toe.*`. Moving a clavicle moves the arm but not the torso.
- Weights are rigid except on the torso. A finger bends as three rigid pieces, which suits the blocky look.
- The soft variant is not covered. Its parts have other object names and carry Subdivision, so it needs its own `PARTS` table. The read step applies modifiers as the viewport shows them and prints a `WARNING` for any modifier other than Mirror.
- The materials have no colour or texture. `Gtg_Skin` and `Gtg_Face` only separate the head block into its own submesh.

## Fixes and Problems

### gtg_prep_male_blocky.py

- **Hidden source parts.** Most parts in `Male_Blocky` are hidden in the viewport. A hidden object may not be evaluated, so its Mirror modifier would be skipped. The script evaluates visible copies instead.
- **Helper bones flagged as deform.** `Leg_Target.*` and `Arm_IK.*` are marked as deform bones in the source rig, so exporting deform bones only would still have written them. All eight helpers are removed by name.
- **Same start for Hip and Spine.** In the source both bones start at the same point. Hip now starts at (0, 0.014, -0.510) in source units.
- **Part names.** `Bicep` is the upper arm and `Tricep` is the forearm (checked by position), and `Theigh` is a typo for thigh. The names are kept because they are the names in Su's file.
- **IK constraints.** The source armature has IK constraints and a few posed bones. The copy is built from the rest pose, so none of that reaches the export.
