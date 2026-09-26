# Ghidra Native Decompile

## Overview

`tools/native_decompile.py` prints the native function at an RVA in `TaleWorlds.Native.dll` as
decompiled C, and optionally its callers, using headless Ghidra 12.1 through PyGhidra. It is the
decompiler step of `/native-crash-triage`: `native_crash_triage.py` names the crash site and ends
its report with the exact `native_decompile.py` command for it. Issue
[#688](https://github.com/haterade22/TAOM/issues/688); adoption record
[adopt-ghidra-hindsight-2026-09-26.md](../reviews/adopt-ghidra-hindsight-2026-09-26.md).

## Why This Exists

- **Before:** TAOM's native tooling (`native_crash_triage.py`, `native_sig_author.py`) is capstone
  and pefile. It finds function bounds, strings, callers, RTTI, vtables and xrefs, but it cannot say
  what a function does. The skill told the reader to hand-decode the instructions around the crash
  row, and to read a lookup table's builder the same way.
- **The need:** the native CTDs TAOM has fixed were decided by the logic around the fault: a hash
  map probe that falls off its end, an index read from a record. That logic is a screen of C and
  pages of assembly.
- **Without it:** every native investigation spends its longest step reconstructing control flow by
  hand, and a wrong reading sends the fix to the wrong data table.

## Architecture

### Design challenge

- Auto-analysing a 14 MB DLL takes minutes, and a native offset is only valid for the exact binary
  it came from. The wEditor build updates on its own Steam schedule, and Steam overwrites both builds
  in place (`/native-crash-triage` Phase 1, steps 6 and 7).
- PyGhidra pins a JPype with no wheel for the system Python 3.14, and the only Python 3.13 on the
  desktop is the Microsoft Store build, which redirects `AppData` writes.

### Solution approach

```
python tools/native_decompile.py --rva 0x6590B9 [--callers 1] [--dll <path>]
        |  checks the DLL and $GHIDRA_INSTALL_DIR
        |  no pyghidra here? re-run under $TAOM_GHIDRA_PYTHON (default E:\Tools\ghidra-venv)
        v
key = <build folder>-<sha256[:16]>          e.g. Win64_Shipping_Client-45be32c57c451f78
        |
        v
E:\ghidra\TAOM\<key>  exists with the program?  --no-->  import + auto-analyse + save (once)
        |  yes: open it read-only
        v
function containing imageBase + rva  ->  DecompInterface C  ->  N levels of callers, as C
```

The hash key is what makes a stale answer impossible: new bytes get a new project, whatever path they
sit at. The program is saved only after analysis completes, so a killed first run leaves nothing
half-analysed behind, and the next run imports again. Two runs on one binary at once collide on
Ghidra's project lock, and the second exits 2 saying so (tested 2026-09-26). Once the JVM is up the
tool leaves through `os._exit`: a failure part-way through Ghidra's OSGi start leaves a non-daemon
`FelixDispatchQueue` thread alive, and without it the process never ends
([review](../reviews/adopt-ghidra-hindsight-2026-09-26.md), install trap 3). A `.gpr` left without
its `.rep`, or two first runs racing on a new binary, exit 2 with the files to delete.

**Leaf functions.** A frameless x64 leaf function (no calls, no stack allocation) needs no unwind
data, so it has no `.pdata` entry and `native_crash_triage.py` cannot bound it. Triage then exits 1
saying so and still prints the `decompile:` line, and Ghidra names the function: on v1.5.3,
`0x404B17` is inside `FUN_180404b10`, a 0x23-byte `this->field` accessor, the shape of a null-`this`
crash.

**Upgrade note.** `GhidraProject.importProgram(File)`, used only on a first run, is deprecated for
removal since Ghidra 12.0. At the next Ghidra upgrade, move the import to
`ProgramLoader.builder()...load()`, which returns the program without saving it.

`GhidraBackend` answers three calls (`function_at`, `decompile`, `callers`); `report()` and `main()`
take any object with the same calls, which is how the unit tests run without Ghidra.

## Configuration

No TAOM config. The machine setup (Ghidra, the JDK, the PyGhidra venv, the four
`support\launch.properties` edits) is in
[development-machines.md](../reference/development-machines.md) "Ghidra, desktop only". The laptop
has none of it; the tool exits 2 there naming that page.

## Key Files

| File | Purpose |
|------|---------|
| `tools/native_decompile.py` | the tool: cache key, venv re-run, Ghidra backend, report |
| `tools/native_crash_triage.py` | prints the `native_decompile.py` line after naming a site |
| `.claude/skills/native-crash-triage/SKILL.md` | Phase 2 uses it |
| `tools/tests/test_native_decompile.py` | unit tests plus one opt-in integration test |

## Dependencies

- Ghidra 12.1.4 and Temurin JDK 25 under `E:\Tools` (neither ships with TAOM)
- PyGhidra 3.1.0 and JPype 1.5.2, from Ghidra's own `pypkg\dist`
- `native_crash_triage.DEFAULT_DLL` for the default binary

## Tests

- `tools/tests/test_native_decompile.py`, 27 unit tests: the cache key (stable, changes with the
  bytes, differs between Client and wEditor, safe for any folder name), `Version.xml` reading, the
  report (offset, C text, caller levels, the caller cap, a shared caller and recursion printed once,
  an RVA outside every function), the CLI (header, an absolute project dir, exit 2 for a missing
  DLL, install or PyGhidra, the venv re-run and its no-second-re-run marker), and the hard exit once
  the JVM is up.
- Two integration tests, skipped unless `GHIDRA_INSTALL_DIR` is set and `TAOM_GHIDRA_IT=1`. One
  checks three functions spread across the client DLL's `.pdata` (`0x289D00`, `0x568BC0`,
  `0x87BF90` on v1.5.3): the entry Ghidra reports must equal the start `native_crash_triage.py` reads
  from `.pdata`, two independent readings of the same binary. It runs the whole CLI, venv re-run
  included, and fails on CR CR LF output (Ghidra's C carries CRLF on Windows). The other leaves a
  lone `.gpr` in a scratch project dir and expects exit 2 with the cleanup advice. About 13 s for
  both once the binary is analysed.
- `tools/tests/test_native_crash_triage_dump.py` pins the hint line, including on the no-`.pdata`
  path.

## How to decompile a crash site

1. Name the site: `python tools/native_crash_triage.py --rva 0x<fault_offset>` (or `--dump`).
2. Run the `decompile:` line it prints last. Add `--callers 1` to see who passed the bad value in.
3. For the builder of a lookup table: `python tools/native_sig_author.py xref <table rva>`, then
   `native_decompile.py --rva <builder rva>`.
4. For an editor crash, pass the wEditor DLL with `--dll`; it gets its own project.

## Performance

Measured 2026-09-26 on the desktop, v1.5.3 client DLL (14,209,376 bytes):

| Run | Time | Notes |
|---|---|---|
| First run on a binary | 213 s analysis, 217 s wall | once per distinct binary; writes a 212 MB project |
| Any later run | 3.4 s wall | venv re-run, JVM start, open the saved project read-only, decompile |
| `--callers 1` on a lookup helper | about 1,900 lines of output | the four callers of `0x659030` are large; at most 8 callers are decompiled per function per level |

The first real target was the `+0x6590B9` melee-table crash from `/native-crash-triage`: 26 lines
of C show the bucket-chain walk and the null record pointer it returns on a miss, the pattern the
skill describes as "hash-map miss dereferencing its end-sentinel".

## Changelog

- 2026-09-26: added (#688).

## GitHub Issue

- **Issue:** #688, [tools: headless Ghidra decompile for native crash triage](https://github.com/haterade22/TAOM/issues/688)
- **Status:** Open
