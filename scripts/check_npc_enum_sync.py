#!/usr/bin/env python3
"""Fail if the NPC enums drift between MDIX, C# and Rust.

CI tooling only: it never runs in the game and is not part of the NPC ML pipeline.

Compares names and numeric values of NpcRole, DecisionBackend, LearningMode and NpcAction across:
  Assets/NPC/Archetypes/archetypes.mdix                          (@ENUMS)
  Assets/MidManStudio/Gtg/NPC/Components/NPCEnums.cs             (C# enums)
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
CS = "Assets/MidManStudio/Gtg/NPC/Components/NPCEnums.cs"
FFI = "rust/npc/crates/npc-ffi/src/lib.rs"
ML = "rust/npc/crates/npc-ml/src/lib.rs"
# NpcAction is not authored in MDIX (it is a runtime result), so it is compared as C# vs Rust only.
ENUMS = {"NpcRole": True, "DecisionBackend": True, "LearningMode": True, "NpcAction": False}


def norm(name):
    return name.replace("_", "").lower()


def read(root, rel):
    return (root / rel).read_text(encoding="utf-8")


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
    out = {"NpcRole": {}, "DecisionBackend": {}, "NpcAction": {}, "LearningMode": {}}
    for enum in ("NpcRole", "DecisionBackend"):
        for num, name in re.findall(r"(\d+)\s*=>\s*%s::(\w+)" % enum, ffi):
            out[enum][norm(name)] = int(num)
    m = re.search(r"fn action_code[^{]*\{(.*?)\n\}", ffi, re.S)
    if m:
        for name, num in re.findall(r"NpcAction::(\w+)\s*=>\s*(\d+)", m.group(1)):
            out["NpcAction"][norm(name)] = int(num)
    m = re.search(r"enum\s+LearningMode\s*\{([^}]*)\}", ml)
    if m:
        names = [n for n in re.findall(r"[A-Za-z_]\w*", m.group(1))]
        out["LearningMode"] = {norm(n): i for i, n in enumerate(names)}
    return out


def main(argv):
    root = Path(argv[1]) if len(argv) > 1 else Path(".")
    try:
        sources = {
            "mdix": parse_mdix(read(root, MDIX)),
            "csharp": parse_cs(read(root, CS)),
            "rust": parse_rust(read(root, FFI), read(root, ML)),
        }
    except OSError as err:
        print(f"ERROR: cannot read source file: {err}")
        return 1

    problems = []
    for enum, in_mdix in ENUMS.items():
        expected = sources["csharp"].get(enum)
        if not expected:
            problems.append(f"{enum}: not found in {CS}")
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
