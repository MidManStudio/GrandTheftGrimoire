# NPC Rust and external C FFI benchmarking

Read `docs/npc/benching-standards.md` first. It is the shared standard (ported from mid-engine) plus the GTG
application, results log and known limits.

## Workflows (install separately; YAML and dot-prefixed paths are never in the replacements archive)

- `.github/workflows/npc-rust-ci.yml`: on every push/PR touching the NPC paths. Job `test` runs the enum sync check, formatting, tests and Clippy. Job `csharp-abi` compiles the Unity-side native layer with .NET 8 and runs it against the real Rust library (see `docs/npc/unity-integration.md`).
- `.github/workflows/npc-rust-ffi-bench.yml`: **manual only**. Builds the `gtg-npc-ffi` cdylib in release mode,
  compiles an independent C caller with GCC, runs the ABI v3 smoke test, and benchmarks 10/100/500/1000 NPC batches
  through the real shared-library boundary in two scenarios, `uniform` and `mixed`, each timed 9 times and reported as a median with its spread (see `benching-standards.md`). Inputs: `baseline_note` and `platforms` (`ubuntu-only` or `all`). The x86 leg is pinned to `ubuntu-24.04`.

Cargo colour is disabled in the workflow (`CARGO_TERM_COLOR=never`) so raw output has no escape codes. Output: a parsed comparison table in the run's Summary tab (via `scripts/bench_npc_ffi.py`), a diagnostics block,
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

The C# ABI test needs the .NET 8 SDK and a built native library on the loader path (from the repository root):

```sh
(cd rust && cargo build -p gtg-npc-ffi)
LD_LIBRARY_PATH="$PWD/rust/target/debug" dotnet run --project rust/npc/csharp-abi-test -c Release
```

On macOS use `DYLD_LIBRARY_PATH`; on Windows put the DLL next to the executable or on `PATH`.

The C benchmark preallocates buffers and excludes setup and the whole-pool verification pass from timed calls. The CSV starts with a `scenario` column, carries `reps`, `min_ns_per_npc` and `max_ns_per_npc`, and ends with a `# mixed_actions` comment line. `ns_per_npc` is the median of the repetitions. `ns_per_call` is whole-batch latency,
`ns_per_npc` amortizes it, and `npc_per_s` is throughput. Hosted-runner variation makes comparisons approximate;
test the MacBook Pro and Galaxy A13 separately once native builds exist. The C smoke test validates the exported ABI
version, struct sizes and offsets, successful decisions, null/empty calls, capacity checks and atomic rejection of
invalid input. It cannot establish C# or Unity runtime correctness; the C# ABI test covers the C# boundary but not Unity itself.

## Formatting note

`rust/rustfmt.toml` pins `style_edition = "2021"`, so CI and any local rustfmt agree. It can be removed later if you
prefer 2024-edition style; run `cargo fmt --all` with a Rust 1.85+ toolchain afterwards and commit the result.

## MDIX authoring

NPC archetype data is documented in `docs/npc/mdix-authoring.md`; it does not affect the benchmarks.
