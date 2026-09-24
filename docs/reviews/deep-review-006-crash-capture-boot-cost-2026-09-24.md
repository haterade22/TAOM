# Deep review: plan 006, crash capture boot cost (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 006, crash-capture sweep cut to a six-shim allowlist, live toggles, four dead
         finalizers deleted (branch improve/006-crash-capture-boot-cost, 7f02fc8d..6fe83bca)
Date: 2026-09-24

Scope:   C# (Main/Features/CrashReport, Main/SubModule.cs hunks), tests, docs, CHANGELOG.
         No XML/XSLT, no scripts, no harness files.
Waves:   Wave 1: lenses 1 to 6 in parallel. Lens 7 (XML) and tooling: NOT IN SCOPE.
         Codex adversarial (gpt-6-astra, ultra): complete ("END OF CODEX REVIEW" present).
         Review lead (this report): verification, fixes, Step 4, RCA.

STANDARDS:     FAIL, 2 MED + 8 LOW + 5 nits (all fixed or routed below)
COMPATIBILITY: PASS on signatures (19 verified, 0 incompatible, 3 unverified); 5 wrong
               engine claims in text (fixed); 1 allowlist design question (needs Mike)
EFFICIENCY:    PASS, 0 H, 0 M, 3 L in changed code (1 fixed, 2 not applied); 3 follow-ups
COMPLETENESS:  INCOMPLETE: no GitHub issue (needs Mike); everything else fixed
DATA FLOW:     FAIL, 2 gaps (T3 BUTR, T5 verification; both fixed in text), 5 inconsistencies
               (fixed in text; T14 risk UNVERIFIED, needs Mike)
DESIGN:        7 KEEP proposals (5 apply, 2 follow-up): 2 applied, 3 not applied (needs Mike)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

Every finding below was re-read against the worktree at `6fe83bca` (and the v1.5.3 engine cache
`C:\Users\mikew\.taom-src\v1.5.3\` or the `E:\Decompiled_Bannerlord\_categories_v1.5.3` dump where an
engine claim was involved) before it was classified. Merged IDs refer to the RCA,
`docs/reviews/rca-crash-capture-boot-cost-2026-09-24.md`.

### Agent 1: Standards

| Finding | Verdict | Action |
|---|---|---|
| M1 no GitHub issue | CONFIRMED, NEEDS MIKE | Filing is public (`/issue` is never auto-invoked) |
| M2 bridge test cannot see the throw site | CONFIRMED (F1) | Test throws and checks `TAOM.ThrowSite`; mutating line 96 to `return exception;` turned it red, reverted |
| L1 priority 800 claimed for the bridge | CONFIRMED (F7) | Docs corrected; pinning the bridge to 800 not applied (needs Mike) |
| L2 two engine tests outside `BindingVerification` | CONFIRMED (F6) | Category added to both; `--filter TestCategory=BindingVerification` now runs them |
| L3 skip paths untested | CONFIRMED (F8) | Type-missing and lookup-throws tests added |
| L4 shape test copies the binding resolver | CONFIRMED | Applied (Step 4): shares `HarmonyPatchBindingTests.DiscoverPatchTypes`/`MergeSpec`, adds a `MethodType` check |
| L5 engine names in reflection category D | CONFIRMED (F6) | Moved to category A prose (patch targets gated elsewhere), D keeps the bridge row |
| L6 registry grep finds no shim names | CONFIRMED (F13) | Six shims named |
| L7 wrong dates | CONFIRMED (F14) | Commits are 2026-09-24 (`git log --date=iso`); six places fixed |
| L8 dev triggers cannot reach the shims | CONFIRMED (F4) | Doc rewritten |
| N1, N2, N4 | CONFIRMED (F20, F21, F22) | Fixed |
| N3 bridge in its own file | Not applied | Optional; no parity or clarity gain worth a file move |
| N5 86-character trailer in `fb52e619` | CONFIRMED, not fixable | History is not rewritten on this branch (no rebase) |

### Agent 2: Engine compatibility

| Finding | Verdict | Action |
|---|---|---|
| F1 entry 6 may be dead; `RenderTargetComponent_OnPaintNeeded` dropped | CONFIRMED fact, NEEDS MIKE for the swap | Verified: v1.5.3 `BannerlordTableauManager.cs` only nulls `RequestCallback` (:25) and no file in the v1.5.3 dump assigns it. Comment and known-limitation text added; the list is unchanged |
| F2 hero-race names `Managed.ApplicationTick` | CONFIRMED (F5) | Verified `ScreenManager.Tick` has one caller, `EngineScreenManager.Tick` (:23), from the native callback (:414); `Managed.ApplicationTick` (:290-301) never reaches it. Rewritten |
| F3 no engine-bump gate | CONFIRMED (F6) | As Agent 1 L2/L5 |
| F4 priority | CONFIRMED (F7) | As Agent 1 L1 |
| F5 "live for the rest of TAOM's load" | CONFIRMED (F12) | Reworded |
| F6 "empty base virtuals" | CONFIRMED (F19) | "empty or assert-only" |

### Agent 3: Efficiency

| Finding | Verdict | Action |
|---|---|---|
| 1 cost counts one attach per target | CONFIRMED (F17) | Verified `ManagedCallbacks` is not in `PatchShield.ExcludedTargetNamespacePrefixes` and pass 2 skips only TAOM-declared methods; comment, test message and doc now include the PatchShield attach |
| 2 powers-of-ten logging loses recency | CONFIRMED trade-off | Time floor is behaviour-changing: NOT APPLIED (needs Mike); trade-off stated in crash-report.md |
| 3 `HarmonyMethod` per loop pass | Not applied | Negligible next to the attach; the `nameof` form (Agent 6 #3) keeps one construction per target |
| 4, 5, 6 | FOLLOW-UP | Pre-existing code; listed below |

### Agent 4: Completeness

| Finding | Verdict | Action |
|---|---|---|
| GitHub issue missing | CONFIRMED, NEEDS MIKE | As Agent 1 M1 |
| 1 boot-time overclaim | CONFIRMED (F2) | Verified `plans/_audit/2026-09-23-opus/followup-patch-tax.md` "Verdict": 11 player processes did the 247 attaches in 0 to 1 s. CHANGELOG, comment, test message and doc corrected; the body of `1d94df9c` cannot be changed |
| 2 vacuous owed smoke (c), doc :224 | CONFIRMED (F4) | Verified both triggers return early when the master toggle is off (`CrashReportApplicationTickTrigger.cs:28`, `CrashReportDevTrigger.cs:27`); doc fixed, redesigned check listed for Mike |
| 3 priority | CONFIRMED (F7) | |
| 4 bridge test | CONFIRMED (F1) | Also added `Finalizer(null)` returns null |
| 5 missing-assembly test ambiguous | CONFIRMED (F8) | Uses a real shim name and asserts the exact missing entry |
| 6 "untouched" | CONFIRMED (F10) | Doc fixed; code in `CrashReportPatchHelper` is a follow-up |
| 7 coverage loss not a known limitation | CONFIRMED (F15) | Added |
| 8 test list "by class:" | CONFIRMED (F16) | "among them:" |
| 9 dates | CONFIRMED (F14) | |
| Nits | CONFIRMED | Fixed (F20, F21) |
| CHANGELOG conflicts with the main checkout's staged 2026-09-24 section | NEEDS MIKE (merge) | Not a branch defect; resolve under one date header at merge |
| Untracked Codex prompt file | CONFIRMED | Committed with this review, as the other 114 `*.prompt.md` files are |

### Agent 5: Data flow

| Finding | Verdict | Action |
|---|---|---|
| T3 BUTR is never re-enabled | CONFIRMED (F3) | Verified `CrashReportService.cs:89-92` suspends on every capture and `IButterLibExceptionHandlerAdapter` has only `IsPresent`/`TrySuspend`. Hint and how-to corrected |
| T5 toggles have no real in-game check | CONFIRMED (F4) | |
| T12 raw rethrow on master-off | CONFIRMED (F10) | Doc fixed; code follow-up |
| T13 priority | CONFIRMED (F7) | |
| T14 `Mission.Tick` row and dropped-callback risk | CONFIRMED text (F11); risk UNVERIFIED, NEEDS MIKE | Row rewritten; the in-game probe is Mike's open question (2) |
| T15 hero-race | CONFIRMED (F5) | |
| T16 reflection category | CONFIRMED (F6) | |
| F1, F3 stale "conditional" text | CONFIRMED (F18), introduced by this change | `CrashReportApplicationTickTrigger.cs:10-11` and `submodule-lifecycle-and-harmony.md:30,74` corrected |

### Agent 6: Design and elegance

| Proposal | Verdict | Action |
|---|---|---|
| 1 one preserved exit in `HandleOrPassThrough` | KEEP, behaviour-CHANGING | NOT APPLIED, needs Mike |
| 2 `[HarmonyPriority(800)]` on the bridge | KEEP, CHANGING only against other mods' earlier patches | NOT APPLIED, needs Mike (Codex advises against changing priority to match docs; docs corrected instead) |
| 3 attach by `nameof`, delete the dead guard | KEEP, PRESERVING | APPLIED |
| 4 BUTR text | KEEP, PRESERVING | APPLIED (F3) |
| 5 shared resolver in the shape test | KEEP, PRESERVING | APPLIED |
| 6 `CrashReportPatchHelper` raw returns | FOLLOW-UP | Listed |
| 7 two wrong comments | Split | Dev-trigger comment was made false by this change: fixed (F18). `Patch37_CrashReport.cs:67-71` is pre-existing: FOLLOW-UP |

## ACTION ITEMS

1. Mike: approve filing the GitHub issue for plan 006 (public), carrying the owed in-game checks and
   the decisions below; label `triage-needs-ingame` if closed before they run.
2. Mike: decide the allowlist's entry 6 (keep `BannerlordTableauManager_RequestCharacterTableauSetup`,
   which nothing in v1.5.3 arms, or replace it with `RenderTargetComponent_OnPaintNeeded`).
3. Mike: decide the in-game probe for dropped callbacks (a QA throw from a behaviour's
   `OnAgentRemoved`) before closing.
4. Replace owed check (c) in plan 006: the dev triggers read the master toggle, so "no bundle with
   capture off" proves nothing. Check instead that toggling shows no restart prompt and persists,
   and use a throw source that does not read the master gate.
5. Resolve the CHANGELOG `## 2026-09-24` header against the main checkout's staged section at merge.

## IMPROVEMENTS (Step 4)

The suite was green (known failures only) before Step 4: 10254 passed, 2 skipped, 2 failed.

APPLIED:
- `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:43-46`: bridge attached with
  `new HarmonyMethod(typeof(Native2ManagedBridge), nameof(Native2ManagedBridge.Finalizer))`; the
  runtime lookup and its unreachable null guard are gone. PRESERVING; proof: the build and
  `Native2ManagedBridgeTests`, `Native2ManagedTargetsTests` green before and after (the live attach
  has no unit test; owed in-game check (a), "attached 6 of 6").
- `TAOM.Tests/Features/CrashReport/Patch37TargetShapeTests.cs`: uses
  `HarmonyPatchBindingTests.DiscoverPatchTypes` and `MergeSpec` (made `internal`), asserts
  `MethodType` is absent or `Normal`; 41 lines removed. PRESERVING; proof: green before and after,
  and a temporary `MissionBehavior.OnMissionTick` finalizer in the category turned it red
  (`TempVirtualFinalizerMutant -> TaleWorlds.MountAndBlade.MissionBehavior.OnMissionTick`), reverted.
- `Main/Features/CrashReport/CrashReportSettings.cs:21` and `docs/features/crash-report.md` how-to:
  BUTR text (Agent 6 #4). PRESERVING, text only.

NOT APPLIED:
- `Native2ManagedPatcher.cs:92-98` single preserved exit (Agent 6 #1): behaviour-changing, needs Mike.
- `Native2ManagedPatcher.cs` bridge `[HarmonyPriority(800)]` (Agent 6 #2): behaviour-changing
  against another mod's earlier patch on a shim, needs Mike; the docs now state 400.
- `CrashBundleThrottle` time floor (Agent 3 #2): behaviour-changing, needs Mike; trade-off
  documented in crash-report.md "Risks & Known Limitations".
- `Native2ManagedPatcher.cs` hoisted `HarmonyMethod` (Agent 3 #3): negligible win (six allocations
  at boot).
- `Native2ManagedBridge` in its own file (Agent 1 N3): optional, no parity gain.
- The convergence pass (Step 4.6, one `deep-reviewer` on the applied diff) was not launched: this
  delegate cannot spawn agents. The orchestrator's second pass covers it.

FOLLOW-UP (pre-existing code outside the change; no issue filed, because filing is public and
needs Mike):
- `CrashReportPatchHelper.cs:32,39,47,51` return the raw exception (`harmony-patches.md:50`); the
  RCA `rca-shield-rethrow-stack-2026-09-22.md` follow-up (Agents 1, 4, 5, 6).
- `Patch37_CrashReport.cs:67-71` has the two `ScreenManager.Update` overloads backwards (Agents 1,
  2, 3, 5, 6).
- `SubModule.cs:187-190` MED-01 comment claims Patch37 covers the rest of `OnSubModuleLoad`; plan
  009 rewrites it (single-owner file).
- `Native2ManagedPatcher` has no interface and `AttachAll` no test (Agent 1 F4).
- `docs/reference/feature-map.md` has no CrashReport row (Agent 1 F5).
- Suppressed occurrences still pay `TrySuspend` reflection and a file-info stack trace before the
  throttle (Agent 3 #4).
- PatchShield's per-call finalizer on the six shims and five Patch37 targets (Agent 3 #5; plan 007).
- The cause of about 186 ms per `harmony.Patch` on the desktop (Agent 3 #6): `/investigate`.
- `CrashReportSettings.cs:7-9` class comment and `CrashReportPatchHelper.cs:26-28` quote retired
  posture and hint text (Agent 5 F4, F6).
- A persisted OFF is ignored during boot, before MCM's provider exists (Agent 5 F7).
- The API snapshot drifted independently: HEAD's DLL generates 248 patch rows against 243
  committed (Agent 5 F8, not re-run here).
- crash-report.md lacks the template's "## GitHub Issue" section, and its "not unit-tested" list
  names the Logs collector although `LogTailCollectorTests` exists (Agent 4).
- UNVERIFIED: which thread runs the thumbnail and tableau callbacks (Agents 2, 4).

VERDICT: READY FOR COMMIT (the NEEDS MIKE items are decisions and a public issue, not defects in
the branch; the Step 4.6 convergence pass is owed by the orchestrator)

## Test evidence

- Filtered run after the fixes (`Patch37TargetShapeTests`, `Native2ManagedTargetsTests`,
  `Native2ManagedBridgeTests`, `CrashBundleThrottleTests`, `SettingRequireRestartPostureTests`,
  `RethrowStackPreserverTests`, `HarmonyPatchBindingTests`, `ReflectionSiteBindingTests`): 101
  passed, 0 failed.
- Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree:
  before, `Failed: 2, Passed: 10254, Skipped: 2, Total: 10258`; after,
  `Failed: 2, Passed: 10258, Skipped: 2, Total: 10262`. The two failures are the known live-Armory
  tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

## CODEX REVIEW

Raw output: `docs/reviews/raw/codex-adversarial-006-crash-capture-boot-cost-2026-09-24.md`
(gitignored); prompt: `docs/reviews/codex-adversarial-006-crash-capture-boot-cost-2026-09-24.prompt.md`.
Codex quoted installed-engine and decompiled MCM and Harmony code for every claim, answered all ten
Known Suspects (four disputed, three unverified with reasons, one confirmed, two with no defect
established), and cross-referenced
the settings, category and origin strings.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | Verified: the tests compared resolution with the production list and bounded only the maximum; an empty list passed. Fixed with a literal six-name pin (F9) |
| 2 | P3 | MED | Yes, raised | Verified both triggers return early with the master toggle off and neither reaches a shim. Raised to MED because the doc claimed coverage that does not exist (F4) |
| 3 | P3 | LOW | Yes | Verified: bridge has no `[HarmonyPriority]`, attached with `new HarmonyMethod(...)`; docs corrected, runtime priority left as is, as Codex advised (F7) |
| Suspect 9 | CONFIRMED | | Yes | Same as 1 and 2 |
| Suspects 2, 5, 6, 7 | DISPUTED | | Yes | Rechecked: all six shims resolve (`Native2ManagedTargetsTests` green), `PreserveForRethrow(Exception?, MethodBase?)` exists, defaults stay true |

- **Confirmed bugs:** 1, 2, 3 (all fixed in tests and docs).
- **False positives:** none.
- **Design questions:** none raised by Codex; its note that `Mission.Tick` does not prove the
  dropped callbacks are covered joins NEEDS MIKE item 3.
- **Things Codex missed:** the player boot-time figure (F2), BUTR staying suspended (F3), the wrong
  `Managed.ApplicationTick` claim, which Codex judged "supported" (F5), the missing
  `BindingVerification` category (F6), the missing known limitation (F15), and the possibly dead
  entry 6.

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Allowlist tests pass on an empty list | Logic error (test) | Plan prescribed a self-referential test | Literal pin; lesson in `testing-qa.md` |
| 2 | Owed smoke (c) cannot fail; doc claims trigger coverage | Other: verification gap | Step written from the hint, not the trigger code | Doc corrected; lesson in `testing-qa.md` |
| 3 | Bridge priority documented as 800 | Convention inconsistency | Category text reused for a hand-attached patch | Lesson in `harmony-il.md` |

## NEEDS MIKE

1. File the plan 006 GitHub issue (public).
2. Allowlist entry 6: keep `RequestCharacterTableauSetup` or swap to `RenderTargetComponent_OnPaintNeeded`.
3. In-game probe for a throw in a dropped callback (for example a behaviour's `OnAgentRemoved`).
4. Bridge single preserved exit (Agent 6 #1).
5. Bridge `[HarmonyPriority(800)]` (Agent 6 #2).
6. Suppression-log time floor (Agent 3 #2).
7. Whether `EnableNativeToManagedCapture` still earns its place now the attach always runs (Agent 6).
8. CHANGELOG date-header merge with the main checkout's staged section.

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed lines:
- **Bugs Codex typically misses:** claims that need data outside the diff (a committed audit's
  player measurements, a third-party DLL's disable semantics, a test-category convention); Codex
  judged a wrong engine catch point "supported" because it checked the prescribed chain, not the
  named method's callers.
- **What Codex does well:** caught that the plan's own smoke recipe and allowlist tests could not
  fail, and declined to recommend a runtime priority change just to match documentation.
