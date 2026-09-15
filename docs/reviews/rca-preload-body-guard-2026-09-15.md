# RCA: Patch90_PreloadBodyGuard deep review (2026-09-15)

**Feature:** `Patch90_PreloadBodyGuard`, issue [#601](https://github.com/haterade22/TAOM/issues/601); the runtime half of [#599](https://github.com/haterade22/TAOM/issues/599) (the elf start hanging on the Rivendell tournament, mechanism #352).
**Review:** 5 agents (standards, engine-API compatibility on the installed v1.5.3 DLLs, efficiency, completeness, cross-system data flow).
**Outcome:** 0 HIGH. 2 MED from the efficiency agent, 1 doc inconsistency from the data-flow agent, 1 UNVERIFIED native claim already stated as unproven in the feature doc. Both confirmed findings re-read against the source before action; one fixed in session, one accepted with the reason recorded here, one doc sentence added.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | `PreloadBodyGuardService.DrainUnresolvable` evaluated `pending.RemoveAll(name => Resolves(isResolved, name))` inside the polling loop. The lambda captures `isResolved`, so Roslyn allocates a fresh delegate per evaluation: one per 1 ms pass, about five thousand across the 5 s budget on the exact path the guard exists for. | efficiency, closure in a polling loop | The loop was written for the healthy path (one pass, one allocation) and the failure path was reasoned about as "five seconds once", not as five thousand iterations. The existing efficiency rule names closures in per-frame loops; a bounded polling loop with a `Thread.Sleep(1)` is the same shape and was not read as one. | FIXED: the predicate is built once before the loop. Lesson below. |
| 2 | MED | `Resolves` wraps each resolver call in a catch-all that swallows the exception and reports the name as unresolved, so the reason a native lookup threw never reaches the log. | diagnostics, swallowed reason | Deliberate: a guard that throws inside the load it protects is worse than the hang, and the drop path already logs every name. | ACCEPTED as designed. If a `[PreloadGuard]` line ever names a body that the audit says ships, the resolver threw rather than returned null; the patch's own outer catch logs a resolver failure of the guard itself, so the two cases are distinguishable from the log shape. |
| 3 | LOW (doc) | The feature doc described `GauntletEducationScreen.OnFrameTick` as a per-frame caller without saying that the screen's own `_startedRendering` latch makes the wait run once per screen instance, so character sets preloaded by later `OnOptionSelect` picks are never waited on by vanilla and therefore never guarded. | doc scope of a caller-driven guard | The caller list was enumerated from a grep of the decompile; the latch inside one caller was not read. "Called from OnFrameTick" was written as if it meant "called every frame". | FIXED: "Coverage follows the caller" subsection in `docs/features/preload-body-guard.md`. Lesson below. |
| 4 | n/a | Whether `PhysicsShape.ProcessPreloadQueue()` is synchronous cannot be proven from managed code; a body that exists but is disk-slow could in principle read null past the 5 s budget on the main thread. | UNVERIFIED native | Not missed: the feature doc's "Design Challenge" states it. | No action. The budget is generous for that reason; the in-game probe with a bogus body and a normal load will show the healthy timing (`[BattleLoad] WaitingForRender waitedMs=0..1` on healthy loads today). |

## Root-cause pattern: the failure path of a guard is the path to cost

Finding 1 is small and its lesson is not. A guard is written for the case where it does nothing, so it is costed for the case where it does nothing. Its whole reason to exist is the other case, and that is the case nobody profiles because it needs a broken install to reach. The per-pass closure was invisible on the healthy path (one evaluation) and only costs on the path the guard was built for. The same reading error produced finding 3 from the other side: the caller list was enumerated for "does the guard attach" and not for "what does each caller actually do around the call", so a once-per-screen latch was described as per-frame.

## Why each agent missed these

- Standards: correct scope, none of these are standards.
- Compatibility: read the target and its callers for signatures and threads, which it got right; caller-internal latches are not an API question.
- Efficiency: found both MED findings. Its two LOW notes (a `Stopwatch` per call, a string join on the drop path) were correctly rated no-action.
- Completeness: correct scope.
- Data flow: found finding 3 by reading the education screen's `OnFrameTick` body rather than its name, which is the trace the others did not do.

## Lessons to codify

Two entries appended to `docs/reviews/lessons/harmony-il.md`: cost a guard's failure path as the loop it is, not as the once it feels like; and a caller-driven guard's coverage is the caller's coverage, so read each caller's gate before describing when the guard runs.

## Verification

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PreloadBodyGuard|FullyQualifiedName~CoopVetoClassification"`: 13/13 after the hoist. Full suite before the review: 9,215 passed, 2 skipped. In game: owed, the bogus-body probe on the player's kit.
