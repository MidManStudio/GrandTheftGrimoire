#!/usr/bin/env python3
"""Summarise the GTG NPC Rust-direct vs C-to-Rust FFI benchmark for the Actions step summary.

CI tooling only: it never runs in the game and is not part of the NPC ML pipeline.

Usage: bench_npc_ffi.py RUST_BENCH_TXT FFI_BENCH_CSV

Inputs (produced by .github/workflows/npc-rust-ffi-bench.yml):
  RUST_BENCH_TXT  output of `cargo run --release --example decision_throughput`
                  key=value lines like:
                  scenario=mixed batch=  10 iterations=400000 reps=9 ns/npc=5.20 min=5.10 max=6.00 npc/s=192307692
                  ns/npc is the median of `reps` repetitions; min and max are the fastest and slowest.
                  Older files without scenario/reps/min/max still parse (scenario defaults to `uniform`).
                  An optional `# mixed_actions idle=.. trade=.. patrol=.. attack=.. retreat=.. fnv1a=0x..`
                  line is used to check that Rust and C ran the same mixed workload.
  FFI_BENCH_CSV   output of the C caller (rust/npc/ffi-smoke-test/bench.c)
                  header: scenario,batch,iterations,reps,elapsed_s,ns_per_npc,min_ns_per_npc,
                          max_ns_per_npc,npc_per_s,ns_per_call
                  Older CSVs without scenario/reps/min/max still parse. Same `# mixed_actions` line.

Prints GitHub-flavoured markdown to stdout. Parsing is deliberately tolerant: unparsable
lines are counted and reported, never fatal, so a partial run still produces a summary.
Ratio badges follow docs/npc/benching-standards.md (parity <=1.05x, warn <=1.5x,
error <=5.0x, otherwise overhead-dominated). GTG addition: a ratio below 0.8x is labelled
"C faster" instead of parity, and a spread (max/min across repetitions) above 1.5x is
flagged as unstable, because neither is evidence of anything until the run is repeatable.
"""
import csv
import re
import sys

FRAME_60_NS = 1e9 / 60.0  # one 60 fps frame
FRAME_30_NS = 1e9 / 30.0  # one 30 fps frame (Galaxy A13 realistic target)
RE_ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
RE_KV = re.compile(r"([A-Za-z_/]+)=\s*(\S+)")
UNSTABLE_SPREAD = 1.5
LOW_RATIO = 0.8
RE_ACTIONS = re.compile(r"^#\s*mixed_actions\s+(.*)$", re.M)
SCENARIOS = ("uniform", "mixed")
SCENARIO_NOTE = {
    "uniform": "every NPC identical (best case for branch prediction and caches)",
    "mixed": "town-like population, sliding window over 16384 pseudo-random NPCs",
}


def badge(ratio):
    if ratio < LOW_RATIO:
        return "⚪ C faster (noise or harness difference)"
    if ratio <= 1.05:
        return "🟢 parity"
    if ratio <= 1.5:
        return "🟡 warn"
    if ratio <= 5.0:
        return "🟠 error"
    return "🔴 overhead-dominated"


def read_text(path):
    try:
        with open(path, encoding="utf-8", errors="replace") as fh:
            return RE_ANSI.sub("", fh.read())
    except OSError:
        return None


def parse_actions(text):
    """The `# mixed_actions` line as a dict of str -> str, or None."""
    m = RE_ACTIONS.search(text or "")
    if not m:
        return None
    return dict(kv.split("=", 1) for kv in m.group(1).split() if "=" in kv)


def parse_rust(text):
    """Returns ({scenario: {batch: row}}, skipped_line_count)."""
    rows, skipped = {}, 0
    for line in (text or "").splitlines():
        if "batch=" not in line or line.lstrip().startswith("#"):
            continue
        kv = dict(RE_KV.findall(line))
        try:
            ns = float(kv["ns/npc"])
            rows.setdefault(kv.get("scenario", "uniform"), {})[int(kv["batch"])] = {
                "ns_npc": ns,
                "npc_s": float(kv["npc/s"]),
                "min": float(kv.get("min", ns)),
                "max": float(kv.get("max", ns)),
                "reps": int(kv.get("reps", 1)),
            }
        except (KeyError, ValueError):
            skipped += 1
    return rows, skipped


def parse_ffi(text):
    """Returns ({scenario: {batch: row}}, skipped_line_count)."""
    rows, skipped = {}, 0
    if not text:
        return rows, skipped
    lines = [ln for ln in text.splitlines() if ln.strip() and not ln.lstrip().startswith("#")]
    for rec in csv.DictReader(lines):
        try:
            scenario = (rec.get("scenario") or "uniform").strip()
            ns = float(rec["ns_per_npc"])
            rows.setdefault(scenario, {})[int(rec["batch"])] = {
                "ns_npc": ns,
                "npc_s": float(rec["npc_per_s"]),
                "ns_call": float(rec["ns_per_call"]),
                "min": float(rec.get("min_ns_per_npc") or ns),
                "max": float(rec.get("max_ns_per_npc") or ns),
                "reps": int(rec.get("reps") or 1),
            }
        except (KeyError, TypeError, ValueError, AttributeError):
            skipped += 1
    return rows, skipped


def spread(row):
    """max/min across repetitions, or None when only one repetition (or an old file) was recorded."""
    if not row or row["reps"] <= 1 or row["min"] <= 0:
        return None
    return row["max"] / row["min"]


def print_scenario(name, rust, ffi):
    print(f"#### {name}: {SCENARIO_NOTE.get(name, 'custom scenario')}\n")
    print("| Batch | Rust ns/NPC | C→FFI ns/NPC | FFI ÷ Rust | Spread max÷min (Rust / C) | "
          "Batch call (ns) | Batch % of 60 fps frame | NPCs per 1 ms |")
    print("|---:|---:|---:|---:|---:|---:|---:|---:|")
    for batch in sorted(set(rust) | set(ffi)):
        r, f = rust.get(batch), ffi.get(batch)
        rust_cell = f"{r['ns_npc']:.2f}" if r else "n/a"
        ffi_cell = f"{f['ns_npc']:.2f}" if f else "n/a"
        if r and f and r["ns_npc"] > 0:
            ratio = f["ns_npc"] / r["ns_npc"]
            ratio_cell = f"{ratio:.2f}× {badge(ratio)}"
        else:
            ratio_cell = "n/a"
        sr, sf = spread(r), spread(f)
        if sr is None and sf is None:
            spread_cell = "n/a"
        else:
            spread_cell = " / ".join(f"{v:.2f}×" if v is not None else "n/a" for v in (sr, sf))
            if any(v is not None and v > UNSTABLE_SPREAD for v in (sr, sf)):
                spread_cell += " ⚠ unstable"
        call_ns = f["ns_call"] if f else (r["ns_npc"] * batch if r else None)
        call_cell = f"{call_ns:,.0f}" if call_ns is not None else "n/a"
        frame_cell = f"{call_ns / FRAME_60_NS * 100:.4f}%" if call_ns is not None else "n/a"
        ns_npc = f["ns_npc"] if f else (r["ns_npc"] if r else None)
        per_ms = f"{1e6 / ns_npc:,.0f}" if ns_npc else "n/a"
        print(f"| {batch} | {rust_cell} | {ffi_cell} | {ratio_cell} | {spread_cell} | {call_cell} | {frame_cell} | {per_ms} |")
    print()


def main(argv):
    if len(argv) != 3:
        print("usage: bench_npc_ffi.py RUST_BENCH_TXT FFI_BENCH_CSV", file=sys.stderr)
        return 2
    rust_text, ffi_text = read_text(argv[1]), read_text(argv[2])
    rust, rust_skip = parse_rust(rust_text)
    ffi, ffi_skip = parse_ffi(ffi_text)

    if not rust and not ffi:
        print("**No benchmark rows parsed.** The bench steps probably failed before printing; "
              "see the raw output below and the uploaded artifact.")
        return 0

    print("### Rust direct vs C→Rust FFI (release, hosted runner)\n")
    reps = sorted({row["reps"] for table in (rust, ffi) for rows in table.values() for row in rows.values()})
    if reps and reps != [1]:
        print(f"Each cell is the **median of {', '.join(str(n) for n in reps if n > 1)} repetitions** after a warm-up. "
              "Spread is the slowest repetition divided by the fastest; above "
              f"{UNSTABLE_SPREAD}× the point is marked unstable and its ratio should not be trusted.\n")
    elif reps == [1]:
        print("> Single-repetition results (older benchmark format): noise cannot be measured.\n")
    names = [n for n in SCENARIOS if n in rust or n in ffi]
    names += sorted((set(rust) | set(ffi)) - set(SCENARIOS))
    for name in names:
        print_scenario(name, rust.get(name, {}), ffi.get(name, {}))

    worst = max((v["ns_call"] for rows in ffi.values() for v in rows.values()), default=None)
    if worst is not None:
        print(f"Largest measured batch call: {worst:,.0f} ns "
              f"({worst / FRAME_60_NS * 100:.4f}% of a 60 fps frame, "
              f"{worst / FRAME_30_NS * 100:.4f}% of a 30 fps frame).\n")

    rust_actions, ffi_actions = parse_actions(rust_text), parse_actions(ffi_text)
    if rust_actions and ffi_actions:
        if rust_actions == ffi_actions:
            print(f"✅ Rust and C generated the same mixed workload and made the same decisions "
                  f"(`{rust_actions.get('fnv1a', '?')}`).")
        else:
            print(f"🔴 **Rust and C mixed workloads disagree** — Rust `{rust_actions}` vs C `{ffi_actions}`. "
                  "The two generators or the native library diverged; the mixed timings are not comparable.")
        print()
    elif "mixed" in rust or "mixed" in ffi:
        print("> ⚠️ No `# mixed_actions` line found in one or both outputs, so the mixed workloads "
              "could not be cross-checked.\n")
    if rust_actions:
        print("Mixed decisions by action (whole pool): " +
              ", ".join(f"{k} {v}" for k, v in rust_actions.items() if k != "fnv1a") + "\n")
    if rust_skip or ffi_skip:
        print(f"> ⚠️ Skipped unparsable lines — Rust: {rust_skip}, C CSV: {ffi_skip}.\n")
    print("> **Read with care.** `uniform` is a best case. `mixed` varies role, health and threat per NPC "
          "and moves the window every call, so it is closer to real data but still has no navigation, "
          "animation or Unity marshalling cost, and it is one synthetic distribution. Hosted runners "
          "are for regression spotting, not device numbers — measure the MacBook Pro and Galaxy A13 directly.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
