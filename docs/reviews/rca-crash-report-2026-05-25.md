# RCA — CrashReport (Deep-Review Phase 1, 2026-05-25)

## Top-line

`/deep-review crash-report` returned **1 HIGH, 2 MEDIUM, 3 LOW** confirmed findings. All 6 fixed in the same session per the "HIGH findings — no silent deferrals" rule. Build clean (0 errors), 2440/2440 tests still pass post-fix.

The session author skipped the `/deep-review` + `/review-codex` completion workflow entirely after the build went green — declared the feature done after `/verify`-equivalent only. The user had to ask "did you run /deep-review and /review-codex" to surface that the mandatory step was skipped. This is itself an RCA item (process violation, captured below as the meta-finding).

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | **HIGH** | `CrashReportSettings.SuspendButterLibHandler` MCM toggle declared and visible to players, but `CrashReportService.HandleException` always calls `_butterLib.TrySuspend()` without reading the toggle. The setting was decorative — a player who turned it off expecting BUTR's window to remain visible would still have it suppressed. | **MCM-declared-without-consumer** (variant of aspirational scaffolding) | Authored the MCM page first as a "complete settings surface," then composed the service with hardcoded behaviour and never went back to wire the toggle. The MCM page lies to the user — promises a knob that doesn't exist. Same shape as `feedback_user_facing_promise_must_match_code.md` (Codex review #34, 2026-05-06, SiegeDismount `DismountKeepOnMap` was a silent no-op). | **Apply the existing user-facing-promise rule earlier.** When authoring an MCM toggle, the closing pass must include `grep -r "<PropertyName>" Main/Features/<Feature>/` and assert at least one read. If zero reads exist, either delete the toggle or wire the consumer in the same session. |
| F2 | MED | `CampaignSnapshot.AutosaveEnabled` hardcoded to `null` in the collector; renderer printed `"?"` for every report. Diagnostic value zero. | **DTO-field-without-populator** (variant of aspirational scaffolding) | Wrote the DTO with the field at design time (sensible — players' "I had autosave off when I crashed" is real diagnostic signal). At collector-author time hit `Campaign.AutoSaveEnabled` API uncertainty, hardcoded `null` as a placeholder, never came back. The `null` was intentional (TODO) but indistinguishable from the field being structurally complete. | Fix: wire `Campaign.SaveHandler?.AutoSaveInterval > -1` (the v1.4.5 vanilla truth source). Generalisation: **placeholders need explicit markers.** A hardcoded literal in a populator is too quiet — at minimum add a `// TODO: populate from <api>` comment so a `grep -rE "TODO|FIXME" Main/Features/<Feature>/` pre-ship sweep catches it. |
| F3 | MED | `AppDomainExceptionHook.Unsubscribe()` exists on the hook class but is never called on `OnSubModuleUnloaded`. Across a same-process game-restart (rare but possible), the `AppDomain.UnhandledException` event still references a stale `CrashReportService` after `IoC.Dispose()`. | **Symmetric-method-without-caller** (variant of aspirational scaffolding) | Wrote `Subscribe()` and `Unsubscribe()` together for symmetry. Wired `Subscribe()` in `OnSubModuleLoad`. Never wired `Unsubscribe()` in `OnSubModuleUnloaded` — that file was edited a single time and the unsubscribe was forgotten. | Fix: invoke `Unsubscribe()` in `OnSubModuleUnloaded` BEFORE `IoC.Dispose()`. Generalisation: for every `EventSubscribe` call in `OnSubModuleLoad`, the unsubscribe in `OnSubModuleUnloaded` is mandatory paired work — a checklist item before declaring a feature complete. |
| F4 | LOW | `ModuleSnapshot.XmlFilePaths` populated by `ModuleListCollector` (parses each module's `SubModule.xml`), but the plain-text renderer drops it — only the JSON bundle round-trips. Wasted parse cost on every crash, asymmetric output. | **DTO-field-without-renderer** | Same shape as F2/F3 — declared-without-consumer at the renderer layer. Renderer was authored from the DTO surface but cross-check (every DTO field → at least one render line) was not run. | Render the field (one `if (m.XmlFilePaths.Count > 0)` line added). Generalisation: at minimum, the renderer's section-method must cover every field in the corresponding DTO. A test that exercises every DTO field non-empty and asserts each appears in the rendered text would mechanise this. |
| F5 | LOW | `AppDomainSnapshot.RelativeSearchPath` populated but never rendered (same shape as F4). | **DTO-field-without-renderer** | Same as F4. | Render it. Same mechanisation as F4. |
| F6 | LOW (perf) | `GpuDisplayCollector` issued two separate `ManagementObjectSearcher` queries against `Win32_VideoController` — one for GPU adapter inventory, one for current resolution. Both could be one `SELECT *` query. ~5-10ms savings per crash on systems with slow WMI. | **Code-organisation cost not measured** | Natural domain-driven split (GPU info vs display info → two methods → two queries) without accounting for the underlying WMI class being shared. Each method was correct in isolation. | Consolidated to one `ManagementObjectSearcher` querying both column sets. Added a `Lazy`-style cache (`EnsureSnapshot`) so the WMI hit is once per process, not once per crash — WMI hardware state doesn't change during a session, so the additional cache is free correctness. |
| **Meta** | — | `/deep-review` and `/review-codex` were skipped after the build went green. The session author declared the feature done after `/verify`-equivalent only. | **Workflow skipped** | The author treated "green build + green tests + docs + CHANGELOG" as sufficient for a 60-file feature. CLAUDE.md "Completion Workflow (MANDATORY)" explicitly requires `/deep-review` then `/review-codex` BEFORE close-out. The rule was already in CLAUDE.md; the session author didn't follow it. | Already corrected: the user prompted `/deep-review` and `/review-codex`. The author should not need to be reminded. Generalisation: **after a feature with >10 new files / any Harmony patches, the next action is `/deep-review` — no exceptions.** This is the third time the same author has shipped past the completion workflow on a non-trivial feature without prompting (per CHANGELOG history). |

## Root Cause Pattern: declared-without-consumer (THIRD occurrence in 19 days)

Five of the six concrete findings (F1, F2, F3, F4, F5) share one shape: **a name was introduced at one layer but the corresponding consumer at another layer was forgotten or stubbed.** F1 declares an MCM toggle but the service doesn't read it. F2 declares a DTO field but the collector hardcodes a placeholder. F3 declares an `Unsubscribe()` method but the lifecycle call site is missing. F4/F5 declare DTO fields but the plain-text renderer drops them.

This is the same pattern as:
- **EquipPresets review #5 (2026-05-06)** — `SlotApplyOutcome.SlotLocked`, `PresetLoadResult.SkippedLockedSlots` declared, never produced. Removed. Memory: memory entry `feedback_no_aspirational_enum_values`.
- **CultureMarketplace (2026-05-20)** — 3 aspirational artifacts (`CountPerInjection` field, `EnumerateTowns()` method, `TownInjectionContext` POCO). All removed. RCA: [`rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md`](rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md).
- **CrashReport (today, 2026-05-25)** — 5 of 6 findings, all the same shape.

The rule memory entry `feedback_no_aspirational_enum_values` has existed for 19 days. The session author has not consulted it at design time on any of the three occurrences. **The rule is in memory; the discipline to apply it is not.**

A second related pattern: **F1 specifically is a "user-facing promise doesn't match code" bug**, the same shape as Codex review #34 (SiegeDismount `DismountKeepOnMap` mode was a silent no-op despite an MCM hint promising it would work). Memory: memory entry `feedback_user_facing_promise_must_match_code`. Also pre-existing. Also not consulted at design time.

## Why Each Deep-Review Agent Missed These (or Caught Them)

- **Agent 1 (Standards):** Caught zero of the 6. Correct — none of these violate ADR-002/003/004/005/007. Declared-but-unused fields are not a standards violation.
- **Agent 2 (Compatibility):** Caught zero of the 6. Correct — none of these are API-incompatibility issues; every signature is valid against v1.4.5.
- **Agent 3 (Efficiency):** Caught F6 (the WMI consolidation). Working as designed — its rule set includes "WMI calls" and noticed the duplication.
- **Agent 4 (Completeness):** Caught the meta finding (the missing GitHub issue). Correct scope. Did not catch F1-F5 — completeness is about ship-list items (tests, docs, CHANGELOG, IoC registration), not about cross-layer wiring consistency.
- **Agent 5 (Data Flow): CAUGHT ALL 5 of F1-F5.** This is the canonical Agent 5 finding shape — its rule set explicitly traces "declared field → consumer" and flags zero-call-site results. Specifically:
  - Trace 2 ("Renderer ↔ DTO Consistency") caught F4 and F5
  - Trace 4 ("MCM Toggle ↔ Feature Behavior") caught F1
  - Trace 1 ("DTO Completeness") caught F2
  - Trace 5 ("Subscribe/Unsubscribe Lifecycle") caught F3

The deep-review agent prompts are working exactly as designed. **The failure was not running them.** Once they ran, they caught everything that could be caught at this stage.

## Feedback Memories to Codify

**No new feedback memory files.** The patterns are fully covered by the existing:
- memory entry `feedback_no_aspirational_enum_values` — declared-without-consumer (F1, F2, F3, F4, F5)
- memory entry `feedback_user_facing_promise_must_match_code` — MCM toggle promises (F1 specifically)
- memory entry `feedback_completion_workflow` — 4-phase workflow mandatory (meta finding)

**What's needed instead is the discipline to consult them during design + completion.** Three occurrences of the same pattern in 19 days = the rule is in memory but the author is not reading memory at the right moments.

Process improvement (not a new rule): **At the start of any feature with >10 new files, the `Skill` tool should be used to invoke `/think-before-coding` discipline manually — enumerate every declared type/field/method and name its producer + consumer.** This catches declared-without-consumer at design time, not at deep-review time. The `think-before-coding.md` rule already exists in scope; what's missing is the trigger to use it on larger features.

## Patch History

| Finding | Pre-fix | Post-fix |
|---------|---------|----------|
| F1 (HIGH) | `CrashReportService.HandleException` line 83-85: unconditional `_butterLib.TrySuspend()` (guarded only by `_butterLibSuspended` flag) | Added `&& (CrashReportSettings.Instance?.SuspendButterLibHandler ?? true)` to the guard. Toggle now respected. |
| F2 (MED) | `CampaignStateCollector.cs:54`: `AutosaveEnabled: null` hardcoded | Reads `campaign.SaveHandler?.AutoSaveInterval` and infers enabled = `interval > -1` (v1.4.5 truth source from decompiled `SaveHandler._isAutoSaveEnabled` body). |
| F3 (MED) | `SubModule.OnSubModuleUnloaded` (line 633-638): only `_harmony.UnpatchAll` + `IoC.Dispose()`. | Inserted `IoC.Resolve<AppDomainExceptionHook>()?.Unsubscribe()` (try-wrapped) BEFORE `IoC.Dispose()`. |
| F4 (LOW) | `PlainTextCrashReportRenderer.RenderModules`: no `XmlFilePaths` line | Added `if (m.XmlFilePaths.Count > 0) sb.AppendLine(...)` line. |
| F5 (LOW) | `PlainTextCrashReportRenderer.RenderAppDomain`: no `RelativeSearchPath` line | Added `sb.AppendLine($"RelativeSearchPath: {d.RelativeSearchPath ?? "(none)"}")`. |
| F6 (LOW perf) | `GpuDisplayCollector`: two `ManagementObjectSearcher` queries against `Win32_VideoController` | Consolidated to one query; added `EnsureSnapshot()` lazy cache so WMI hit is once per process, not once per crash. |
| Meta | `/deep-review` + `/review-codex` skipped after build went green | This RCA exists. `/review-codex` runs next. |

## Tests Affected

No test changes — all fixes are inside collector/service paths that the existing test suite doesn't exercise (TaleWorlds-facing collectors are `Not-tested:` per ADR-008 + the plan's commit-trailer convention). Full suite still 2440/2440 passing.

## Verdict

Phase 1 (deep-review) RCA complete. Five of six findings are the same root-cause pattern that has shipped THREE times in 19 days — the prevention rule exists but isn't being applied at design time. Process improvement is required at the author level, not new rule documentation.

Proceeding to Phase 2 (`/review-codex` adversarial pass) next.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/rca-cultural-feats-wave1-2026-06-07.md](./rca-cultural-feats-wave1-2026-06-07.md)

<!-- backlinks-end -->
