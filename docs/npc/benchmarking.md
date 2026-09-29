# NPC Rust and external C FFI benchmarking

Read `docs/npc/benching-standards.md` first. It is the shared standard (ported from mid-engine) plus the GTG
application, results log and known limits.

## Workflows (install separately; YAML and dot-prefixed paths are never in the replacements archive)

- `.github/workflows/npc-rust-ci.yml`: formatting, tests, Clippy on every push/PR touching `rust/**`.
- `.github/workflows/npc-rust-ffi-bench.yml`: **manual only**. Builds the `gtg-npc-ffi` cdylib in release mode,
  compiles an independent C caller with GCC, runs the ABI v2 smoke test, and benchmarks 10/100/500/1000 NPC batches
  through the real shared-library boundary. Inputs: `baseline_note` and `platforms` (`ubuntu-only` or `all`).

Output: a parsed comparison table in the run's Summary tab (via `scripts/bench_npc_ffi.py`), a diagnostics block,
raw output in folds, and untouched raw files in a 30-day artifact.

## Run locally from `rust/` on Linux (Cargo and GCC required)

```sh
cargo fmt --all --check
cargo test --workspace
cargo build -p gtg-npc-ffi --release --examples --lib
gcc -std=c11 -O2 -Wall -Wextra -Werror -pedantic -o /tmp/gtg-npc-c-test npc/ffi-smoke-test/test.c -Inpc/ffi-smoke-test -Ltarget/release -lgtg_npc_ffi -Wl,-rpath,"$PWD/target/release" -lm
/tmp/gtg-npc-c-test
cargo run -p gtg-npc-ffi --release --example decision_throughput | tee /tmp/rust-bench.txt
gcc -std=c11 -O3 -Wall -Wextra -Werror -pedantic -o /tmp/gtg-npc-c-bench npc/ffi-smoke-test/bench.c -Inpc/ffi-smoke-test -Ltarget/release -lgtg_npc_ffi -Wl,-rpath,"$PWD/target/release"
/tmp/gtg-npc-c-bench | tee /tmp/ffi-bench.csv
python3 ../scripts/bench_npc_ffi.py /tmp/rust-bench.txt /tmp/ffi-bench.csv
```

The C benchmark preallocates buffers and excludes setup from timed calls. `ns_per_call` is whole-batch latency,
`ns_per_npc` amortizes it, and `npc_per_s` is throughput. Hosted-runner variation makes comparisons approximate;
test the MacBook Pro and Galaxy A13 separately once native builds exist. The C smoke test validates the exported ABI
version, struct sizes and offsets, successful decisions, null/empty calls, capacity checks and atomic rejection of
invalid input. It cannot establish C# or Unity runtime correctness.

## Formatting note

`rust/rustfmt.toml` pins `style_edition = "2021"`, so CI and any local rustfmt agree. It can be removed later if you
prefer 2024-edition style; run `cargo fmt --all` with a Rust 1.85+ toolchain afterwards and commit the result.

## MDIX archetype cleanup

Merchant, guard and boss archetype files now declare `NpcRole`, `DecisionBackend` and `LearningMode` enums instead of
free-form strings. The same numeric role/backend values correspond to ABI v2, but **MDIX assets are not yet wired to
the Unity authoring loader**. Existing `AIType` in `Assets/game_enemies.mdix` is deliberately untouched.
`LearningMode` is descriptive authoring metadata, not an ABI field or an implemented ML feature. The three archetype
files each declare the same local enums to remain independently parseable. Future consolidated imports require
explicit parser/Unity-loader validation.
