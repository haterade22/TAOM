---
paths:
  - "Dependencies/**/*.cpp"
  - "Dependencies/**/*.h"
  - "Main/SceneScripts/**"
---

# Native C++ Port Discipline

Loads when native C++ (vendored hooks, scene scripts) is being edited. Moved verbatim from CLAUDE.md Working Discipline (repo-reorg 2026-07-12).

**When porting C++ code from an upstream mod into TAOM (`Dependencies/*.NativeHooks/`, scene scripts, or any other vendored native), audit the port from scratch. "Upstream worked" only means "produced correct output" — it does NOT mean the port is fit to ship in TAOM.**

The recurring failure mode: architectural changes (rename functions, change export signatures, retarget output path) consume the audit budget; behavioral preservation (logging, exception handling, lock balance) flies through unaudited. Three review findings on the NativeSkinFixes port (2026-05-26) traced to this — all inherited verbatim from the upstream Nexus mod. See `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

Before you commit a C++ port, walk this checklist:

1. **Hot-path logging.** Every `OutputDebugString` / `fprintf` / `fputs` / `LogLine` call inside a function the engine calls per-frame / per-Face_mesh / per-agent must be sample-gated (atomic counter + summary on uninstall). Upstream debug logging is rarely audited; it's almost always a HIGH finding when it survives the port.
2. **SEH filter specificity.** `__except (EXCEPTION_EXECUTE_HANDLER)` is a code smell. Use `GetExceptionCode()` to narrow to the specific class you expect (typically `EXCEPTION_ACCESS_VIOLATION`); let heap corruption / stack overflow propagate to the OS crash dumper.
3. **Inter-function offsets.** Any computation like `helperFunc = mainFunc - 0xF6A0` is fragile across engine versions and must be replaced with an independent signature scan. The original NativeSkinFixes had one such offset for `NotifyPhysics`; the port replaced it with a 7th pattern.
4. **Atomic counters.** Any `static int counter` touched from a hook body is racy if the engine fires on multiple threads. Use `volatile LONG64` + `InterlockedIncrement64`.
5. **SRWLock balance.** Reads use `AcquireSRWLockShared`; writes use `AcquireSRWLockExclusive`. Verify both sides — upstream mods routinely take the exclusive lock for reads ("just in case") and silently serialise everything.
6. **`/deep-review` with C++ in scope.** The skill prompt now includes C++ HOT-PATH CHECKS and C++ Native Hook Standards blocks that fire automatically when `.cpp`/`.h` files are in the changeset (per the post-mortem on the same RCA). Run it.

This is a project-level discipline, not a one-off feature note — every future C++ port has the same risk profile. See `feedback_native_port_hot_path_audit.md`, `feedback_seh_filter_specificity.md`.

