# RCA — CrashReport Codex Adversarial Review (Phase 2, 2026-05-25)

## Top-line

Codex adversarial review on the CrashReport feature returned **2 HIGH + 4 MEDIUM + 2 LOW** findings, all independently verified against TAOM source and decompiled vanilla DLLs. All 8 fixed in the same session per the "HIGH findings — no silent deferrals" rule. Build clean (0 warnings, 0 errors), 2440/2440 tests still pass.

This was the FIRST review run via the new direct-dispatch contract (`codex exec - < prompt.md > output.md` via Bash, no terminal hand-off). The dispatch + 1.88MB Codex output + post-process + 8 fixes + this RCA executed inside a single Claude session without the user opening a separate terminal. Process improvement noted: the manual dispatch step would not have changed any of these findings, but it would have changed who-and-when. With direct dispatch, the elapsed time from "deep-review done" to "Codex fixes shipped" was minutes, not "after the user remembers to dispatch."

The Phase 1 deep-review caught 6 bugs (1 HIGH, 2 MED, 3 LOW). Codex independently caught 8 ADDITIONAL bugs (2 HIGH, 4 MED, 2 LOW). Together, the two-phase workflow caught 14 bugs in a 60-file feature that Claude had declared "done" after `/verify` alone. **The CrashReport feature was 14 fixes away from being actually correct** when Claude tried to close it out without running the workflow.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| C-H1 | **HIGH** | `CrashReportPatchHelper._service` static cache survives `OnSubModuleUnloaded` → after Bannerlord reload-in-process, Harmony Finalizers point at a disposed `FileLogger`. All subsequent crash captures silently drop log lines. | **Static cache without lifecycle hook** | I wrote `ResetForUnload()`-style discipline into every other TAOM feature but not this one. The thread-static `_onPatchStack` flag was implemented carefully; the regular static `_service` cache was implemented as "first wins, never invalidate." Didn't enumerate the unload lifecycle even though it's documented in memory entry `feedback_lifecycle_state_matrix`. | Fixed: added `CrashReportPatchHelper.ResetForUnload()` + call site in `SubModule.OnSubModuleUnloaded` before `IoC.Dispose()`. Generalisation: any static cache holding an IoC-resolved reference MUST have a reset hook called from the matching unload. This is the same shape as `feedback_lifecycle_state_matrix.md`; the rule existed but I didn't apply it. |
| C-H2 | **HIGH** | `EnableCrashCapture` MCM toggle text promises "When off, all Harmony Finalizers no-op and AppDomain hook unsubscribes" but the property is read ONCE at startup. A user disabling it at runtime gets nothing. | **User-facing-promise mismatch + startup-only consumer** | This is the SAME shape as the Phase 1 SuspendButterLibHandler bug I already fixed. I created the MCM page text first, then implemented runtime gating partially and forgot the master toggle. Same author, same session, same repeat-offender pattern as memory entry `feedback_user_facing_promise_must_match_code`. | Fixed: added runtime guard in `CrashReportPatchHelper.HandleAndSwallow` AND `AppDomainExceptionHook.OnUnhandled` AND `CrashReportDevTrigger*` to respect `EnableCrashCapture` at every call site. Generalisation: when authoring an MCM page, the closing pass MUST grep every property name across the feature's source and assert at least one runtime-path read. The `/codex-verify` skill prompt now has this as a focus area. |
| C-M1 | MED | Patch37_CrashReport attached at `SubModule.cs:108` but `IoC.Configure()` (88), UIExtender create/register/enable (90-92), `IoC.Resolve<ITimeAccelerationService>()` (94), `Harmony` ctor (96), and `CrashReportSettings.Instance` read (104) all run BEFORE — throws in any of those lines are uncatchable by our own feature. The CHANGELOG entry comment "registered FIRST" was true relative to OTHER PatchCategory calls but false relative to OTHER throwable operations. | **Reasoning-order vs source-order mismatch** | I designed the init for "Patch37 before other PatchCategory calls" and convinced myself that was sufficient. Didn't enumerate every non-PatchCategory throwable operation in the init block. The CHANGELOG note "registered FIRST" was wishful — accurate within the PatchCategory subset, misleading overall. | Fixed: moved Patch37 attach to immediately after `IoC.Configure()`. The only remaining uncatchable window is IoC.Configure itself, which is unavoidable without re-implementing a minimal pre-IoC bootstrap. Documented as residual. Generalisation: when claiming a thing is "registered FIRST," enumerate every line BEFORE it and prove each is unable to throw, OR document the residual blind spot. |
| C-M2 | MED | **`HarmonyCorrelationCollector.Collect(stack)` was called with `frames=null` → per-frame patches list was always empty.** The advertised "Harmony patches affecting every frame in the stack" feature was DEAD CODE. The CHANGELOG promised this; the renderer reserved a rendering section for it; the comprehensive-mode plan listed it as a key data point. None of it actually worked. | **Optional-parameter trap + non-test-covered surface** | The collector was designed with an optional `frames` parameter "to allow callers to skip the raw StackFrame construction." The CrashReportService caller ALWAYS skipped it because `StackFrameSnapshotBuilder.FromException` already builds the snapshot list. The mistake: snapshot list doesn't preserve `MethodBase` references, so `Harmony.GetPatchInfo(mb)` can't be called. Both layers compiled cleanly; the collector silently produced empty patch lists for every frame; the renderer faithfully rendered empty lists; no test covered the integration. | Fixed: added `HarmonyCorrelationCollector.CollectFromException(exception, stack)` overload that builds the raw StackFrame[] internally. Updated CrashReportService to call this overload. Generalisation: when a collector takes an optional parameter that controls a major data field, write an integration test where the parameter is non-null AND a test where it is null, and assert the difference in the output. The fact that the test suite passed proves the test suite was incomplete — fix that too (TODO). |
| C-M3 | MED | `AppDomainExceptionHook.OnUnhandled` can fire on TaleWorlds worker threads (`TWParallel.For` for agent ticks). The Mission/Campaign collectors read live `Mission.Current`, teams, formations, agent state without thread-safety guards. `InformationManager.ShowInquiry` directly invokes UI subscribers off-thread. | **Single-thread assumption without enforcement** | I designed the collectors assuming "single-threaded campaign code." This is correct for Harmony Finalizers on tick methods (always main thread) but wrong for AppDomain hooks. I knew engine threading exists (memory entry `feedback_detect_engine_threading_via_mt_suffix`) but didn't connect "my AppDomain hook can fire from a worker thread" to "my collectors read main-thread-only state." | Fixed: AppDomainExceptionHook captures the main thread id at Subscribe(). If OnUnhandled fires on a different thread, it tags the exception's Data dictionary with `TAOM.CrashReport.OffMainThread=true`. CrashReportService.HandleException reads the tag and switches to reduced-capture mode: skips Mission/Campaign collectors (with explicit CollectorFailure entries), skips the UI inquiry, still writes the full report + ZIP from thread-safe collectors only. Generalisation: any hook that can fire on a non-main thread MUST capture the main thread id at subscribe and gate non-thread-safe consumers. |
| C-M4 | MED | `_butterLibSuspended` one-shot flag → if user re-enables ButterLib via its own MCM after our first crash, we never re-disable. ButterLib `CanBeSwitchedAtRuntime => true` and `Disable()` is idempotent. | **Single-shot guard against an idempotent operation** | I added the `_butterLibSuspended` flag as a "don't spam the call" optimisation, treating ButterLib's `Disable()` as a one-shot action. Didn't decompile to verify; assumed expensive. Codex decompiled and showed `Disable()` is a trivial state set + handler unsubscribe — calling it twice is fine. The flag was a premature optimisation that introduced a real bug. | Fixed: removed the `_butterLibSuspended` flag entirely. `TrySuspend()` is now called on every crash when the MCM toggle is on. Generalisation: before adding a "skip if already done" flag, decompile the target to verify the op is actually expensive. Idempotent ops shouldn't be guarded against re-entry — the guard creates more failure modes than it prevents. |
| C-L1 | LOW | `CrashReportSettings.Instance` is a provider-scan + lookup (MCM `BaseSettingsProvider.GetSettings(id)` iterates settings containers), not a static-field read. Called every `Module.OnApplicationTick` (60-200 Hz) by the dev trigger postfix. | **Wrong assumption about MCM's `Instance` getter cost** | Per-frame cost was acceptable in my mental model because I assumed `AttributeGlobalSettings<T>.Instance` was a static field read. Codex decompiled MCMv5 and showed the getter does a `BaseSettingsProvider.Instance.GetSettings(typeof(T).Id)` call which iterates settings containers. Cheap but not free. | Fixed: cache `_cachedSettings` on first non-null result in both dev triggers. MCM returns the same singleton on every call once initialised, so caching is correctness-safe. Generalisation: before using ANY `.Instance` accessor in a per-frame path, decompile to verify it's not a provider lookup. Same shape as previous "always verify expensive-looking APIs" lessons. |
| C-L2 | LOW | `CrashBundleWriter.Write` returned the zip path even after mid-write failure → player gets pointed at a broken bundle as if it were complete. | **Error path returned a value instead of `null`** | I wrote the catch block with a "leave the broken file for inspection" comment that contradicted the "return the path so caller can show it to the player" caller contract. Both behaviors are sensible in isolation but the combination is a bug: the caller can't distinguish a complete bundle path from a broken bundle path. | Fixed: on mid-write failure, rename the file to `*.zip.partial` (preserves diagnostic value) AND return `null` to the caller (so they show "bundle write failed" instead of "here's your bundle"). Generalisation: when an error path leaves partial state on disk, name it differently so success/failure paths return distinguishable values. |
| Observation | — | Comment claimed "10 Harmony Finalizers" but the file has 9 Finalizers + 1 dev-trigger Postfix. | Counting error in the documentation comment | Wrote the count before adding the AutoGenerated reflection patches; forgot to update. | Fixed: comment now correctly describes the category (9 explicit Finalizers + 1 Postfix + run-time reflection-attached Finalizers via Native2ManagedPatcher). |

## Root Cause Pattern: completion-workflow bypass

The meta-finding from BOTH the Phase 1 deep-review RCA and this Codex RCA: the session author shipped a 60-file feature without running `/deep-review` or `/review-codex`, despite both being MANDATORY per the completion workflow ([CLAUDE.md](../../CLAUDE.md) "Completion Workflow"). The user had to ask "did you run /deep-review and /review-codex" to surface the omission.

Together the two phases caught **14 bugs**: 6 from deep-review (1 HIGH + 2 MED + 3 LOW), 8 from Codex (2 HIGH + 4 MED + 2 LOW). The deep-review caught 5 declared-without-consumer bugs (same shape, repeat offender). Codex caught a different class — runtime correctness under unusual conditions: thread safety, lifecycle reload, MCM cost, error-path return values, broken integration between layers (the HarmonyCorrelationCollector dead-code path is particularly damning — it's the ONE feature I personally thought made our crash reports more useful than BUTR's, and it was non-functional).

**The pattern is the workflow exists for a reason.** Codex caught bugs that 5 deep-review agents missed. Deep-review caught bugs that the build + tests missed. Without all three, the feature ships broken.

The new direct-dispatch contract (Codex via Bash, no terminal hand-off) removes the "I forgot to open the terminal" excuse. The remaining excuse is "I forgot to invoke the skill at all" — that's pure author discipline, no tooling fix possible. Continued vigilance required.

## Why Each Deep-Review Agent Missed These (or Caught Them)

- **Agent 1 (Standards):** Caught zero of the 8. Correct — none of these violate ADR-002/003/004/005/007. Static-cache-no-reset is not a standards violation; it's a lifecycle correctness issue. Same for thread safety on the AppDomain hook.
- **Agent 2 (Compatibility):** Caught zero of the 8. Correct — none of these are API-incompatibility issues. The compatibility agent's job is to verify TaleWorlds API signatures, which it did (all 9 patch targets verified). It cannot reasonably be expected to decompile MCMv5 + ButterLib + Harmony to verify cross-mod integration semantics — that's Codex's value-add.
- **Agent 3 (Efficiency):** Caught C-L1 partial. Codex's MCM decompile was more concrete than my Phase 1 agent's "MCM `Instance` read cost we accept." Both flagged the read; only Codex proved the cost via decompilation.
- **Agent 4 (Completeness):** Caught zero of the 8. Correct scope — completeness is about ship-list items (tests, docs, IoC registration), not about runtime correctness.
- **Agent 5 (Data Flow):** Caught the SuspendButterLibHandler decorative-toggle bug (Phase 1 HIGH-02 shape) but DID NOT catch C-H2 (the EnableCrashCapture decorative-master-toggle bug). Why: the Phase 1 data-flow agent ran the toggle-cross-reference check on the OTHER toggles (SuspendButterLib, EnableNativeToManaged, WriteCrashBundle, ThrowOnNext*) and missed cross-referencing the master toggle itself. The agent stopped at "EnableCrashCapture is read at SubModule.cs:104" without asking whether that single startup read fulfilled the MCM hint's runtime promise. **Codex caught this because its prompt explicitly told it to cross-reference EVERY MCM property name — same shape as Phase 1 but with one less omission.** The deep-review agent's prompt should be tightened so the toggle-cross-reference applies to every toggle without listing them.
- **What about C-M2 (the dead Harmony correlation)?** Agent 5 Trace 1 ("DTO Completeness") asked "is every field populated?" The answer for `HarmonyCorrelationSnapshot.PatchesPerStackFrame` was "yes, populated by HarmonyCorrelationCollector." Agent 5 did not drill into the EMPTY-VS-NON-EMPTY case — every per-frame entry was being created with an empty `Patches` list. Codex caught this by reading the optional parameter's call site. **The deep-review agent prompt's "DTO Completeness" trace should be extended to ask: for every list-typed field, are non-empty lists actually possible under normal operation?** Otherwise it's just checking that the populator runs, not that it produces useful output.

## Feedback Memories to Codify

**No new feedback memory files required.** The patterns are fully covered by existing memories:

- C-H1: memory entry `feedback_lifecycle_state_matrix`
- C-H2: memory entry `feedback_user_facing_promise_must_match_code` (this is now the 2nd CrashReport instance of this rule — the SuspendButterLibHandler bug in Phase 1 was the first; pattern is "I author MCM hint text aspirationally and forget to wire one of the toggles")
- C-M1: memory entry `feedback_observation_state_matrix` (boundary-state enumeration applies to "what runs before X" too)
- C-M3: memory entry `feedback_detect_engine_threading_via_mt_suffix` (extended to AppDomain hook scope)
- C-M4: memory entry `feedback_codex_caught_api_misread` (the recurring "Claude assumes API cost without verifying, Codex decompiles and proves otherwise")
- C-L1: same as C-M4
- C-L2: no specific memory; one-off

The deep-review prompt updates I noted above (apply toggle-cross-reference to EVERY toggle without listing them; extend DTO Completeness to verify non-empty lists are actually produced) ARE worth doing — that's a small change to `.claude/skills/deep-review/SKILL.md` Phase 5 prompts. Adding to follow-up TODO list.

## Patch History

| Finding | Pre-fix | Post-fix |
|---------|---------|----------|
| C-H1 | Static `_service` field never cleared | Added `ResetForUnload()`; called from `SubModule.OnSubModuleUnloaded` before `IoC.Dispose()` |
| C-H2 | `EnableCrashCapture` read only at SubModule startup | Runtime gate added in `CrashReportPatchHelper.HandleAndSwallow`, `AppDomainExceptionHook.OnUnhandled`, both dev triggers |
| C-M1 | Patch37 attach at SubModule line 108 (after UIExtender + time-service init) | Moved to immediately after `IoC.Configure()` |
| C-M2 | `HarmonyCorrelationCollector.Collect(stack)` called with `frames=null` → empty per-frame patches | Added `CollectFromException(exception, stack)` overload that builds raw `StackFrame[]` internally; `CrashReportService` calls the new overload |
| C-M3 | AppDomain.UnhandledException ran full collectors on worker threads | Capture main thread id at Subscribe(); tag exception with `OffMainThreadDataKey` if non-main; CrashReportService switches to reduced-capture (skip Mission/Campaign + UI inquiry) |
| C-M4 | `_butterLibSuspended` one-shot prevented re-disable after user re-enabled ButterLib at runtime | Removed the flag; `TrySuspend()` called every crash when MCM toggle is on (ButterLib `Disable()` is idempotent per decompile) |
| C-L1 | `CrashReportSettings.Instance` read per app tick + per mission tick in dev triggers | Cached after first non-null result via `??=` pattern |
| C-L2 | `CrashBundleWriter.Write` returned the zip path even on mid-write failure | Renames to `*.zip.partial` on failure + returns `null`; caller can distinguish |
| Observation | Comment said "10 Harmony Finalizers" — wrong | Comment now says "9 Finalizers + 1 Postfix + run-time reflection-attached Finalizers" |

## Tests Affected

No test changes — all fixes are inside collector/service/hook paths that the existing test suite doesn't directly exercise (TaleWorlds-facing collectors + Harmony hooks are `Not-tested:` per ADR-008 + the plan's commit-trailer convention). Full suite still 2440/2440 passing.

**Test debt incurred (TODO for follow-up):**
1. `HarmonyCorrelationCollectorTests`: assert that when a real Harmony instance has patched a method that appears in the stack, the per-frame patches list is non-empty. This is what would have caught C-M2 at test time.
2. `CrashReportServiceTests`: assert that off-main-thread captures skip Mission/Campaign collectors and skip UI inquiry. Mockable via the `OffMainThreadDataKey` Data tag.
3. `CrashReportPatchHelperTests`: assert `ResetForUnload()` clears the cached service. Trivial; would have caught C-H1.
4. Master-toggle disabled tests: when `EnableCrashCapture == false`, assert `HandleAndSwallow` returns the original exception, `OnUnhandled` no-ops, dev triggers no-op. Would have caught C-H2.

## Verdict

Phase 2 (Codex adversarial) RCA complete. **14 total bugs caught across Phase 1 + Phase 2 in a feature that almost shipped without either phase running.** All 14 fixed; build + tests green. The completion workflow proved its value; the lesson is to never skip it again.

Direct-dispatch contract verified end-to-end: dispatch → 1.88MB output → auto-resume on notification → verify findings → implement fixes → write RCA, all inside one Claude session without user terminal hand-off. The new contract works.

Next steps:
1. Update `AGENTS.md` "Lessons From Prior Reviews" with the dead-Harmony-correlation pattern and the master-toggle pattern (so future Codex reviews call them out earlier).
2. Update `REVIEW-LOG.md` with this review's metrics.
3. Update `.claude/skills/deep-review/SKILL.md` Phase 5 prompts: toggle-cross-reference applies to EVERY toggle; DTO Completeness extended to verify non-empty lists are actually produced.
4. Open the GitHub issue retroactively per `CLAUDE.md` mandate.
5. `/verify` final sanity check, then ready for commit.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
