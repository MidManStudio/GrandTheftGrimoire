#!/usr/bin/env python3
"""Summarise the GTG NPC Rust-direct vs C-to-Rust FFI benchmark for the Actions step summary.

CI tooling only: it never runs in the game and is not part of the NPC ML pipeline.

Usage: bench_npc_ffi.py RUST_BENCH_TXT FFI_BENCH_CSV

Inputs (produced by .github/workflows/npc-rust-ffi-bench.yml):
  RUST_BENCH_TXT  output of `cargo run --release --example decision_throughput`
                  lines like: batch=  10 iterations=100000 ns/npc=5.2 npc/s=192486409
  FFI_BENCH_CSV   output of the C caller (rust/npc/ffi-smoke-test/bench.c)
                  header: batch,iterations,elapsed_s,ns_per_npc,npc_per_s,ns_per_call

Prints GitHub-flavoured markdown to stdout. Parsing is deliberately tolerant: unparsable
lines are counted and reported, never fatal, so a partial run still produces a summary.
Ratio badges follow docs/npc/benching-standards.md (parity <=1.05x, warn <=1.5x,
error <=5.0x, otherwise overhead-dominated).
"""
import csv
import re
import sys

FRAME_60_NS = 1e9 / 60.0  # one 60 fps frame
FRAME_30_NS = 1e9 / 30.0  # one 30 fps frame (Galaxy A13 realistic target)
RE_ANSI = re.compile(r"\x1b\[[0-9;]*[A-Za-z]")
RE_RUST = re.compile(
    r"batch=\s*(\d+)\s+iterations=\s*(\d+)\s+ns/npc=([0-9.]+)\s+npc/s=([0-9.]+)"
)


def badge(ratio):
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


def parse_rust(text):
    rows, skipped = {}, 0
    for line in (text or "").splitlines():
        if not line.strip().startswith("batch="):
            continue
        m = RE_RUST.search(line)
        if not m:
            skipped += 1
            continue
        rows[int(m.group(1))] = {"ns_npc": float(m.group(3)), "npc_s": float(m.group(4))}
    return rows, skipped


def parse_ffi(text):
    rows, skipped = {}, 0
    if not text:
        return rows, skipped
    reader = csv.DictReader(line for line in text.splitlines() if line.strip())
    for rec in reader:
        try:
            rows[int(rec["batch"])] = {
                "ns_npc": float(rec["ns_per_npc"]),
                "npc_s": float(rec["npc_per_s"]),
                "ns_call": float(rec["ns_per_call"]),
            }
        except (KeyError, TypeError, ValueError):
            skipped += 1
    return rows, skipped


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
    print("| Batch | Rust ns/NPC | C→FFI ns/NPC | FFI ÷ Rust | Batch call (ns) | "
          "Batch % of 60 fps frame | NPCs per 1 ms |")
    print("|---:|---:|---:|---:|---:|---:|---:|")
    for batch in sorted(set(rust) | set(ffi)):
        r, f = rust.get(batch), ffi.get(batch)
        rust_cell = f"{r['ns_npc']:.2f}" if r else "n/a"
        ffi_cell = f"{f['ns_npc']:.2f}" if f else "n/a"
        if r and f and r["ns_npc"] > 0:
            ratio = f["ns_npc"] / r["ns_npc"]
            ratio_cell = f"{ratio:.2f}× {badge(ratio)}"
        else:
            ratio_cell = "n/a"
        call_ns = f["ns_call"] if f else (r["ns_npc"] * batch if r else None)
        call_cell = f"{call_ns:,.0f}" if call_ns is not None else "n/a"
        frame_cell = f"{call_ns / FRAME_60_NS * 100:.4f}%" if call_ns is not None else "n/a"
        ns_npc = f["ns_npc"] if f else (r["ns_npc"] if r else None)
        per_ms = f"{1e6 / ns_npc:,.0f}" if ns_npc else "n/a"
        print(f"| {batch} | {rust_cell} | {ffi_cell} | {ratio_cell} | {call_cell} | {frame_cell} | {per_ms} |")

    worst = max((v["ns_call"] for v in ffi.values()), default=None)
    if worst is not None:
        print(f"\nLargest measured batch call: {worst:,.0f} ns "
              f"({worst / FRAME_60_NS * 100:.4f}% of a 60 fps frame, "
              f"{worst / FRAME_30_NS * 100:.4f}% of a 30 fps frame).")
    if rust_skip or ffi_skip:
        print(f"\n> ⚠️ Skipped unparsable lines — Rust: {rust_skip}, C CSV: {ffi_skip}.")
    print("\n> **Read with care.** Every NPC in a batch has identical input, so branch prediction and "
          "caches are best-case. The decision step is cheap; real cost will sit in Unity "
          "observation gathering, marshalling and action execution. Hosted x86 runners are for "
          "regression spotting, not device numbers — measure the MacBook Pro and Galaxy A13 directly.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
