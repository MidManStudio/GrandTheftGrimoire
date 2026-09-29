# NPC Rust and external C FFI benchmarking

Install `.github/workflows/npc-rust-ffi-bench.yml` separately: dot-prefixed paths and YAML are deliberately excluded from the replacements archive. The workflow builds the existing `gtg-npc-ffi` cdylib in release mode, checks Rust formatting/tests/Clippy, compiles an independent C caller with GCC, tests the ABI v2 layout and error handling, and benchmarks 10/100/500/1000 NPC batches through the actual shared-library boundary. The existing Rust example provides a direct-call comparison, not an isolation of FFI overhead; differences between measurements include caller/compiler and harness effects. Results are published to the GitHub Actions summary and as downloadable text/CSV artifacts.

Run locally from `rust/` on Linux after installing Cargo and GCC:

```sh
cargo test --workspace
cargo build -p gtg-npc-ffi --release --lib
gcc -std=c11 -O2 -Wall -Wextra -Werror -pedantic -o /tmp/gtg-npc-c-test npc/ffi-smoke-test/test.c -Inpc/ffi-smoke-test -Ltarget/release -lgtg_npc_ffi -Wl,-rpath,"$PWD/target/release" -lm
/tmp/gtg-npc-c-test
cargo run -p gtg-npc-ffi --release --example decision_throughput
gcc -std=c11 -O3 -Wall -Wextra -Werror -pedantic -o /tmp/gtg-npc-c-bench npc/ffi-smoke-test/bench.c -Inpc/ffi-smoke-test -Ltarget/release -lgtg_npc_ffi -Wl,-rpath,"$PWD/target/release"
/tmp/gtg-npc-c-bench
```

The C benchmark preallocates buffers and excludes setup from timed calls. `ns_per_call` measures whole batch call latency, `ns_per_npc` amortizes batch latency and `npc_per_s` reports throughput. CI runner variation makes comparisons approximate. Test the actual MacBook Pro and Galaxy A13 separately when native builds are available. C smoke test validates exported ABI version, sizes, offsets, successful decisions, null/empty calls, capacity checks and atomic rejection of invalid inputs. It cannot establish C# or Unity runtime correctness.

## MDIX archetype cleanup

Merchant, guard and boss archetype files now declare `NpcRole`, `DecisionBackend` and `LearningMode` enums instead of free-form strings. The same numeric role/backend values correspond to ABI v2, but **MDIX assets are not yet wired to the Unity authoring loader**. Existing `AIType` in `Assets/game_enemies.mdix` is deliberately untouched. `LearningMode` is descriptive authoring metadata, not an ABI field or an implemented ML feature. The three archetype files each declare the same local enums to remain independently parseable. Future consolidated imports require explicit parser/Unity-loader validation.
