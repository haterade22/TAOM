# RCA: ShaderCacheKeeper adoption review (2026-10-06)

**Summary.** The `/deep-review` of the ShaderCacheKeeper adoption (`Native/ShaderCacheKeeper/**` and its docs) could
not run its reviewer agents: all four first-wave lenses (Standards, Engine compatibility, Data flow, Tooling) ended
before reviewing anything because the account hit its weekly `claude-opus-5-5` limit (resets 2026-10-10). Per the
skill's "run the checks manually", the orchestrator reviewed every changed file against the lens checklists. One
LOW finding, in the new test harness, was confirmed and fixed. The Opus lenses are still owed before the change is
treated as fully reviewed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `test/run_tests.ps1` case 7 expected the launcher's selection as the new list whenever the real `LauncherData.xml` said `Singleplayer`, so a profile with nothing selected would fail the case although the keeper correctly does nothing | test fixture | The case reads a file outside the test's control (the real `LauncherData.xml`), and only the state on the author's machine (7 modules selected) was considered | One-off: the case now expects "nothing done" when nothing is selected. A test that reads a real per-user file must branch on every state of that file it can meet |

## Checked and clean (manual pass)

- **Native-port checklist** (`.claude/rules/native-cpp-ports.md`): no logging in the per-frame XInput stubs; the
  `DllMain` filter catches access violations and in-page errors only; no offsets; no shared counters; no locks
  besides `InitOnceExecuteOnce`, which also publishes `g_xinput` to every thread.
- **Data flow:** every `keep()` branch either rewrites with equal signatures, leaves the engine to compile and notes
  what it compiles for, or does nothing. A sidecar that names another list is ignored. The one pessimistic case (a
  game update rebuilding the cache while a note from before it is kept) costs one extra compile, never a stale cache.
- **Loader:** `LoadLibraryW` of the full system path loads the system DLL beside the same-named proxy (the test's
  `XInputGetState(4) = 160` proves it); the load happens on the first XInput call, not under the loader lock.
- **Tooling:** a failed `host.c` compile or a missing DLL stops `run_tests.ps1` with a non-zero exit
  (`$ErrorActionPreference = 'Stop'`); a missing `dumpbin` cannot pass case 0 vacuously, because its second check
  needs `XInputGetState` in the same output; a failed `parse_tests.c` compile is its own failing check.

## Feedback memories to codify

None: one first-time, test-only finding.
