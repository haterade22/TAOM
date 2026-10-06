# RCA: ButterLib Distance Matrix switch (#740), deep review 2026-10-06

## Top-line

`/deep-review` ran twice on worktree branch `perf/740-butterlib-lazy-distance`, both times on uncommitted work.

1. **First pass:** the first design, a lazy rebuild built from four manual Harmony patches on ButterLib's private
   `GeopoliticsBehavior`. Standards, Engine compatibility, Data flow and Efficiency ran. They found no incompatibility,
   but did find:
   - a design gap: ButterLib's public `DistanceMatrix<Kingdom>.Create()` and its DI registration read the deferred clan
     table directly;
   - four MEDIUM test and doc defects (F3, F4, F6 below, and an untested campaign guard on the kingdom read, which went
     with the deleted code);
   - and, from the efficiency lens, that ButterLib supports switching the subsystem off at runtime, which also removes the
     load-time table build the lazy rebuild kept.

   Mike chose the runtime switch, and the lazy-rebuild code was deleted unshipped.
2. **Second pass:** the switch. All six lenses in scope ran (XML and Tooling were not in scope). They found 0
   incompatibilities, 1 MEDIUM gap in the design itself (MCM's ButterLib page owns a second key in the same
   `Options.json`, so the planned opt-in could not hold), 1 MEDIUM missing test, several LOW defects, and 3 KEEP
   proposals. Mike dropped the opt-in. Every finding below was fixed before the commit, or is listed as not applied.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The planned escape hatch (honour ButterLib's own `DistanceMatrixSubSystem Enabled` key) could not hold. MCM's ButterLib page stores `Distance Matrix Enabled` in the same file, applies it through `Enable()`/`Disable()` in MCM's first main-menu hook before TAOM's, and rewrites the file without any other key on every save. | Second reader and writer of a shared file | The research grepped MCM for the setting and found nothing: MCM's ButterLib page (`MBOptionScreen`) enumerates `IEnumerable<ISubSystem>` and builds the key at runtime as `Id + " Enabled"`, so neither the key nor the subsystem name appears in its bytes. | Opt-in dropped by Mike's decision. Lesson added to `adapters-taleworlds-api.md`: before relying on a third-party settings file, find every reader and writer. |
| F2 | MED | The first design's doc said the patched readers caught every read. ButterLib's public `DistanceMatrix<Kingdom>.Create()` and its `AddScoped(DistanceMatrix<>)` registration read the clan table directly. | Engine API misread | The builder enumerated the property's readers inside the behavior's assembly, not the public API of the main ButterLib DLL. | Moot: the design was replaced. The same lesson as F1 covers it. |
| F3 | MED | The first design's binding test read the live install but was not tagged `LiveInstall`, so the RefAsm binding gate would fail on it every run. | Test category | `tests.md` "Test categories" was not consulted when the test was written. | The switch's binding test now reads the repo-tracked DLL in `Dependencies/_Module/bin/`. That needs no install and also catches a ButterLib update before it is deployed. |
| F4 | MED | The first design's hooks could be broken without failing any test (`return false` to `return true`, or swapped readers). The builder wrote the production code before the tests. | TDD breach | Code came first; the mutation check covered only the service. | Moot: deleted. The switch's tests were written first. |
| F5 | MED | Nothing tested that the switch runs at `ApplyPhase.MainMenu`. A regression to ProcessLoad would log "switched off" while MCM turned the subsystem back on. | Untested load-bearing decision | The wiring tests copied a sibling's shape (listing, declarations, container), and the sibling's phase does not matter. | `OnPhase_SwitchesOnlyAtMainMenu` added. |
| F6 | MED | The feature doc credited MithrilForge with "no licence". It is MIT; the unlicensed repository is `yotthani/bannerlord`. The provenance register was not updated. | Provenance | The builder conflated the two repositories named in #740. | Doc corrected; register rows and a fourth reading pass recorded. |
| F7 | LOW | The log claimed "switched off" when ButterLib's saved options had it off already, and a failure to switch it off was logged at Info. | Log truthfulness | One success branch covered two states. | `TryDisable(out wasAlreadyOff)`, a separate "already off" line, and a warning on failure. |
| F8 | LOW | The binding gate's `ShapeProblem` had one negative test for three checks. | Skip-guard exhaustion | `tests.md` "Skip-Guard Exhaustion" was not applied to a validation method. | Two more negative stand-ins and a positive test added. |
| F9 | LOW | The first binding test left an `AssemblyResolve` handler registered for the whole test run, then a second version added a handler that could never fire (LoadFrom already binds from the DLL's folder). | Test process state | Copied from the earlier design's binding test. | Handler removed; LoadFrom's own binding is enough. |
| F10 | LOW | Doc and comment errors: "ButterLib's log shows the work" (it logs nothing about the tables), "honour MCM's key... ignored" (MCM applies it), "500,000 pairs for 1,002 settlements" (843 counted settlements, 354,903 pairs), "every owner change" (only a change of clan). | Unverified doc claims | Written from the research summary, not from the decompile. | All corrected, including the switch's class comment and its warning text, which the convergence pass found still wrong. | The in-game check now has a step that can fail (MCM's page shows the subsystem off). |
| F11 | LOW | The vendored-DLL update recipe did not name the new binding test. | Gate not wired into a procedure | New gate, old recipe. | `docs/modding/module-dependencies.md` names it. |

**Not applied:** the efficiency lens's alternative of ButterLib's public `ISubSystem` API (`GetSubSystem("Distance Matrix")`).
It needs TAOM's first compile-time ButterLib reference. The feature doc records the choice.

## Root-cause pattern

F1, F2 and F10 share one cause: **a third-party mod was studied by searching for names, and a reader that builds
those names at runtime cannot be found that way.** ButterLib is split across a main DLL, version-specific implementation
DLLs and MCM's `MBOptionScreen`, which owns ButterLib's settings page. Each finding came from code one hop away from what
was searched: a settings page that enumerates an interface, and a constructor reached through DI.

## Why each lens missed what it missed

- **Research subagent (before any lens):** asked about the setting's default and storage, it read ButterLib only.
- **First-pass lenses** found F2 to F4 and the runtime switch, but reviewed the opt-in only as a doc line.
- **Second-pass data flow and engine compatibility** found F1 by tracing every writer of `Options.json`, which the
  brief asked for.
- **Completeness** found F5, F8 and F11. No earlier lens owns phase-gate tests.

## Feedback memories to codify

None. The lesson is in `docs/reviews/lessons/adapters-taleworlds-api.md`.
