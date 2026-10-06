#!/usr/bin/env python3
"""Fail if the NPC enums drift between MDIX, C# and Rust.

CI tooling only: it never runs in the game and is not part of the NPC ML pipeline.

Compares names and numeric values of NpcRole, DecisionBackend, LearningMode, NpcDisposition, NpcOrder and NpcAction across:
  Assets/NPC/Archetypes/archetypes.mdix                          (@ENUMS)
  Assets/MidManStudio/Gtg/ECS/NPC/Components/NPCEnums.cs         (C# enums, shared by both stacks)
  rust/npc/crates/npc-ffi/src/lib.rs                             (parse() and action_code())
  rust/npc/crates/npc-ml/src/lib.rs                              (LearningMode declaration order)

Names are compared case-insensitively with underscores removed (STATE_MACHINE == StateMachine).
Usage: check_npc_enum_sync.py [REPO_ROOT]   (default: current directory)
Exit 0 = in sync, 1 = drift or a source could not be parsed.
"""
import re
import sys
from pathlib import Path

MDIX = "Assets/NPC/Archetypes/archetypes.mdix"
# The folder moved under ECS/ when the Managed stack was added. The old location is still tried, so
# the check also works on a checkout from before the move.
CS_CANDIDATES = (
    "Assets/MidManStudio/Gtg/ECS/NPC/Components/NPCEnums.cs",
    "Assets/MidManStudio/Gtg/NPC/Components/NPCEnums.cs",
)
FFI = "rust/npc/crates/npc-ffi/src/lib.rs"
ML = "rust/npc/crates/npc-ml/src/lib.rs"
# NpcAction is not authored in MDIX (it is a runtime result), so it is compared as C# vs Rust only.
ENUMS = {
    "NpcRole": True,
    "DecisionBackend": True,
    "LearningMode": True,
    "NpcDisposition": True,
    "NpcOrder": True,
    "NpcAction": False,
}


def norm(name):
    return name.replace("_", "").lower()


def read(root, rel):
    return (root / rel).read_text(encoding="utf-8")


def find_cs(root):
    for rel in CS_CANDIDATES:
        if (root / rel).is_file():
            return rel
    return CS_CANDIDATES[0]


def parse_mdix(text):
    out = {}
    block = re.search(r"@ENUMS\((.*?)\n\)", text, re.S)
    if not block:
        return out
    for m in re.finditer(r"(\w+)\s*\{([^}]*)\}", block.group(1)):
        vals = {}
        for name, num in re.findall(r"(\w+)\s*=\s*(\d+)", m.group(2)):
            vals[norm(name)] = int(num)
        out[m.group(1)] = vals
    return out


def parse_cs(text):
    out = {}
    for m in re.finditer(r"enum\s+(\w+)\s*(?::\s*\w+)?\s*\{([^}]*)\}", text):
        vals = {}
        for name, num in re.findall(r"(\w+)\s*=\s*(\d+)", m.group(2)):
            vals[norm(name)] = int(num)
        out[m.group(1)] = vals
    return out


def parse_rust(ffi, ml):
    out = {name: {} for name in ENUMS}
    for enum in ("NpcRole", "DecisionBackend", "NpcDisposition", "NpcOrder"):
        for num, name in re.findall(r"(\d+)\s*=>\s*%s::(\w+)" % enum, ffi):
            out[enum][norm(name)] = int(num)
    m = re.search(r"fn action_code[^{]*\{(.*?)\n\}", ffi, re.S)
    if m:
        for name, num in re.findall(r"NpcAction::(\w+)\s*=>\s*(\d+)", m.group(1)):
            out["NpcAction"][norm(name)] = int(num)
    m = re.search(r"enum\s+LearningMode\s*\{([^}]*)\}", ml)
    if m:
        # Doc comments sit inside the enum body now, so drop them before reading the variant names.
        body = re.sub(r"//[^\n]*", "", m.group(1))
        names = re.findall(r"[A-Za-z_]\w*", body)
        out["LearningMode"] = {norm(n): i for i, n in enumerate(names)}
    return out


def main(argv):
    root = Path(argv[1]) if len(argv) > 1 else Path(".")
    cs = find_cs(root)
    try:
        sources = {
            "mdix": parse_mdix(read(root, MDIX)),
            "csharp": parse_cs(read(root, cs)),
            "rust": parse_rust(read(root, FFI), read(root, ML)),
        }
    except OSError as err:
        print(f"ERROR: cannot read source file: {err}")
        return 1

    problems = []
    for enum, in_mdix in ENUMS.items():
        expected = sources["csharp"].get(enum)
        if not expected:
            problems.append(f"{enum}: not found in {cs}")
            continue
        for label in ("rust", "mdix"):
            if label == "mdix" and not in_mdix:
                continue
            got = sources[label].get(enum)
            if not got:
                problems.append(f"{enum}: not found or empty in {label} source")
            elif got != expected:
                problems.append(f"{enum}: {label} {dict(sorted(got.items()))} != csharp {dict(sorted(expected.items()))}")

    if problems:
        print("NPC enum drift detected:")
        for p in problems:
            print(f"  - {p}")
        return 1
    counts = ", ".join(f"{e}={len(sources['csharp'][e])}" for e in ENUMS)
    print(f"NPC enums in sync across MDIX, C# and Rust ({counts}).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
