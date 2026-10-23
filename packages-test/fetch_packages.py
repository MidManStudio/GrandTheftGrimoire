#!/usr/bin/env python3
"""Fetches the package sources that packages-test/ compiles, at the commits pinned in Packages/manifest.json.

Writes packages-test/fetched/:
  questly/      the Runtime folder of com.midmanstudio.questly
  inventorizz/  the Runtime folder of com.midmanstudio.inventorizz
  mdix/         the managed MidManStudio.Mdix.Core.dll of com.midmanstudio.mdix, and none of its native libraries
  PINS.txt   what was fetched, one package per line

Needs git and Python 3 only, and reads public repositories. Run from anywhere:
  python3 packages-test/fetch_packages.py
Documentation: docs/GrandTheftGrimoire/managed.md, section "CI and Workflows".
"""

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MANIFEST = os.path.join(ROOT, "Packages", "manifest.json")
OUT = os.path.join(HERE, "fetched")

# (package name, folder or file taken from the package, folder it lands in under fetched/)
WANTED = [
    ("com.midmanstudio.questly", "Runtime", "questly"),
    ("com.midmanstudio.inventorizz", "Runtime", "inventorizz"),
    ("com.midmanstudio.mdix", "Runtime/Plugins/MidManStudio.Mdix.Core.dll", "mdix"),
]

PIN = re.compile(r"^https://github\.com/([^/]+)/([^/?#]+?)(?:\.git)?\?path=([^#]+)#([0-9a-f]{40})$")


def fail(message):
    print("fetch_packages: " + message, file=sys.stderr)
    sys.exit(1)


def git(args, cwd=None):
    env = dict(os.environ)
    env["GIT_TERMINAL_PROMPT"] = "0"
    result = subprocess.run(["git"] + args, cwd=cwd, env=env, capture_output=True, text=True)
    if result.returncode != 0:
        fail("git " + " ".join(args) + " failed:\n" + result.stdout + result.stderr)


def read_pins():
    with open(MANIFEST, "r", encoding="utf-8") as handle:
        dependencies = json.load(handle).get("dependencies", {})

    pins = {}
    for name, _, _ in WANTED:
        value = dependencies.get(name)
        if value is None:
            fail(name + " is not in Packages/manifest.json")

        match = PIN.match(value)
        if match is None:
            fail(name + " is not pinned as https://github.com/ORG/REPO.git?path=DIR#<40 hex>: " + value)

        pins[name] = match.groups()

    return pins


def fetch_one(name, folder, target, pin):
    owner, repo, path, sha = pin
    source = path.rstrip("/") + "/" + folder
    work = tempfile.mkdtemp(prefix="gtg-fetch-")
    try:
        git(["init", "-q", work])
        git(["remote", "add", "origin", "https://github.com/" + owner + "/" + repo + ".git"], cwd=work)
        git(["sparse-checkout", "set", "--no-cone", "/" + source], cwd=work)
        git(["fetch", "-q", "--depth", "1", "--filter=blob:none", "origin", sha], cwd=work)
        git(["checkout", "-q", "FETCH_HEAD"], cwd=work)

        taken = os.path.join(work, *source.split("/"))
        destination = os.path.join(OUT, target)
        if os.path.isdir(taken):
            shutil.copytree(taken, destination)
        elif os.path.isfile(taken):
            os.makedirs(destination)
            shutil.copy2(taken, destination)
        else:
            fail(name + ": " + source + " does not exist at " + sha)
    finally:
        shutil.rmtree(work, ignore_errors=True)

    return owner + "/" + repo + " " + sha


def main():
    pins = read_pins()
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)

    os.makedirs(OUT)
    lines = []
    for name, folder, target in WANTED:
        lines.append(name + " " + fetch_one(name, folder, target, pins[name]))

    dll = os.path.join(OUT, "mdix", "MidManStudio.Mdix.Core.dll")
    if not os.path.isfile(dll):
        fail("MidManStudio.Mdix.Core.dll is not in the fetched mdix folder")

    with open(os.path.join(OUT, "PINS.txt"), "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")

    print("\n".join(lines))


if __name__ == "__main__":
    main()
