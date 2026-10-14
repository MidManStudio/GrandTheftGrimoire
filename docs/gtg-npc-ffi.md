# gtg-npc-ffi

The C boundary of the NPC decision. It exports a versioned, batched function, so a host calls Rust once per tick instead of once per NPC. Version 0.0.1, ABI version 5. Builds as a `cdylib` for Unity and as an `rlib` for the tests and the benchmark.

The layout and the rules are in `npc/observation.md`.

## Modules

### `lib.rs`

**What it does:** Defines the two C structs (`NpcObservationAbi`, 64 bytes, and `NpcDecisionAbi`, 16 bytes), validates and converts them, and exports `gtg_npc_abi_version`, `gtg_npc_observation_size`, `gtg_npc_decision_size`, `gtg_npc_decide_batch`, `gtg_npc_resolve_mission` and the older scalar `gtg_npc_decide_stub`.

**Decisions:**
- One call per batch, up to 4096 NPCs. All observations are validated before any output is written, so a rejected batch leaves the output buffer as it was.
- The crate opts out of the workspace `unsafe_code` deny with one `#![allow(unsafe_code)]`, because it takes raw pointers and exports `no_mangle` functions. Every `unsafe` block has a `// SAFETY:` comment and the unsafe function has a `# Safety` section.
- The function checks, in this order: the count against the limit, the output capacity, null and alignment, overlap of the two buffers, and then the values. Overlap is rejected because one range becomes a shared slice and the other an exclusive one, which would be undefined behavior if they aliased. The check was added with this version and has a test.
- Every field added after version 2 is zero in an old caller, and zero means the old behavior. A test sets only the version 2 fields and checks the old results.
- Booleans are integers that must be 0 or 1, and a reserved field must be zero. A caller written for a later layout is rejected.
- `crown_heat` (version 5) sits at offset 35, where a reserved byte was. A caller that still sends zero there means no heat and gets the old behavior. A tier above `MAX_CROWN_HEAT` (3, from `gtg-npc-core`) is rejected as a whole batch, so a host built for more tiers fails loudly. Nothing else about the layout moved.
- The version and both sizes are exported, and the C# bridge refuses a library whose version or sizes differ.
- `gtg_npc_resolve_mission` is a scalar export, because a mission is resolved rarely and one at a time. It takes the order as the same number the observation uses (4 deliver, 5 raid), so the host needs one set of numbers.
- `gtg_npc_decide_stub` stays for the first prototype. It builds a hostile, state machine NPC with no order.

### `gtg_npc.h`

**What it does:** The C header used by the smoke test and the C benchmark, with the structs, status codes and the field values.

**Decisions:**
- The header is written by hand and kept in step with `lib.rs` by `test.c`, which asserts the size and every field offset. A generated header is on the backlog.

### `test.c`

**What it does:** A C program that calls the library through the header with `-std=c11 -Wall -Wextra -Werror -pedantic`. It checks the layout, the version, the batch behavior, every validation failure, the version 3 rules, the Crown Heat rule and its tier limit, and overlap rejection.

### `decision_throughput.rs` and `bench.c`

**What they do:** The Rust and C benchmarks, run by the manual workflow `npc-rust-ffi-bench.yml`. Both generate the same deterministic NPC population, run `uniform` and `mixed` scenarios at four batch sizes, and print a median with a spread over nine repetitions. Both print a `# mixed_actions` line with the action counts and a hash of every decision.

**Decisions:**
- The mixed workload now includes companions (10%) with all six orders, trust, liking, pay, a random number and betrayal opportunities, and dispositions, provoked flags and levels for the others. It must produce all ten actions or the program fails.
- The generator is duplicated in three places (Rust, C and the C# ABI test) and the hash proves they agree. A change to one must change all three.
- The population is 1 MiB now (16,384 x 64 bytes), twice the version 2 size, so mixed results from before version 3 are not directly comparable with later ones.

## Fixes and Problems

- Version 5 renamed the reserved byte at offset 35 to `crown_heat`, added the tier limit to validation, and bumped the ABI version to 5. The three generators gained the same Crown Heat line, so the pool and the action hash changed together (the hash in the C#, C and Rust `# mixed_actions` lines is `0x9e47645dd2412b08` now). 24 unit tests cover the layout, every validation rule and the Crown Heat tiers. One clippy warning (`useless_vec`, in the overlap test) was there before this change and is not fixed here.
- Version 3 grew the observation from 32 to 64 bytes. The cost of the larger input is not measured yet. It will show in the next benchmark run.
- The undocumented `unsafe` blocks, the missing crate docs and the version number `0.1.0` were brought in line with `RUST_RUST_CRATE_GUIDE.md` in this change.
- Clippy has not run on this code. The sandbox has no clippy, so the lints were checked by building with warnings as errors and by reading each `unsafe` block. The first CI run decides.
- Not done: a generated C header, fuzzing of the batch entry point, and a Miri run on the unsafe code.
