# Deep review: plan 012, the loading-window lower traced only on a real drop (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 012, loading-window-trace-per-frame
         (branch improve/012-loading-window-trace-per-frame, diff 7f02fc8d..6f7ddd39)
Date: 2026-09-24

Scope:   C# (one Harmony patch class, one pure gate, two test classes), docs (feature doc,
         registry, CHANGELOG)
Waves:   one wave: Agents 1 (Standards), 2 (Engine), 3 (Efficiency), 4 (Completeness),
         5 (Data flow), 6 (Design). Codex gpt-6-astra ultra in parallel.

STANDARDS:     FAIL, 5 violations (1 MED process: no GitHub issue; 4 LOW doc and naming)
COMPATIBILITY: PASS, 0 incompatible, 0 unverified (1 LOW engine-claim prose finding)
EFFICIENCY:    PASS, 0 issues
COMPLETENESS:  INCOMPLETE: GitHub issue missing (needs Mike); feature-map row, Prefix test and
               lesson were owed and are now done
DATA FLOW:     PASS, 2 gaps (LOW: untested Prefix body, two stale doc lines), 1 inconsistency
               (LOW: raises traced per call, lowers per transition; deliberate, now documented)
DESIGN:        1 KEEP proposal (1 apply, 0 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

Lead verification (this delegate, 2026-09-24): every finding below was re-read against the worktree
at `6f7ddd39` before it was classified. Engine facts rechecked from the taom-src v1.5.3 cache:
`TaleWorlds.Engine.LoadingWindow.cs:31-44` (the clear at `IsLoadingWindowActive = false` sits inside
`if (LoadingWindowManager != null)` and outside the `IsLoadingWindowActive` branch),
`SandBox.GauntletUI.GauntletClanScreen.cs:55` and `SandBox.GauntletUI.GauntletInventoryScreen.cs:45-47`
(unconditional and `!_closed` per-frame lowers). The two-raise, one-lower new-campaign sequence was
read from `taom_debug_2026-09-23_13-43-40.log` (lines 123922, 123928, 123939). `gh issue list
--search "loading window"` returns nothing for this change.

## Findings and classification

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| 1 | A1-1, A4-F1 | MED | No GitHub issue; CHANGELOG heading lacks `(#N)` | CONFIRMED | NEEDS MIKE (`/issue` is public, never auto-invoked) |
| 2 | A1-2, A5-T8 | LOW | Class summary `LoadingWindow_Transitions_Patch.cs:7` still says "every raise and lower" | CONFIRMED | Fixed |
| 3 | A1-3, A4-F3, A5-T8 | LOW | `feature-map.md:34` still says "every raise and lower" | CONFIRMED | Fixed |
| 4 | A1-4 | LOW | Registry `:1024` names `IsLoadingWindowActive` without backticks | CONFIRMED | Fixed |
| 5 | A1-5 | LOW | Two signature tests lack the state segment | CONFIRMED | Fixed (renamed `Prefix_Signature_IsOutBoolNamedState`, `Postfix_Signature_TakesStateByValue`) |
| 6 | A2, A6 KEEP | LOW | Screen list "main menu, party screen, character creation" incomplete (patch comment, gate test docstring, feature doc, CHANGELOG) | CONFIRMED (clan, inventory rechecked by the lead) | Fixed |
| 7 | A5-T8 nit, A6, Codex | LOW | Gate doc says the engine clears the flag "unconditionally"; the clear is inside the manager guard | CONFIRMED | Fixed |
| 8 | A5-T9, A4-F2, Codex P3-1 | LOW | No test runs the Prefix body; `__state = true` passes all eight tests | CONFIRMED (mutant run) | Fixed with a round-trip test; the mutant now fails it |
| 9 | Codex P3-2 | LOW | Lowered-trace test accepts `callers: <none>`, `<unavailable>` or a helper-shifted chain, and counts only matching calls | CONFIRMED (mutant run) | Fixed; a `NoInlining` helper-hop mutant now fails it |
| 10 | A5-T5 | LOW | Raises traced per call, lowers per transition: a healthy load logs two raises and one lower | CONFIRMED as a doc gap; the asymmetry itself is deliberate | One sentence added to `map-load-diagnostics.md` |
| 11 | A1, A2, A4, A5 | n/a | CHANGELOG suite count UNVERIFIED | Resolved | Full suite re-run by the lead (below); CHANGELOG now quotes that run |
| 12 | A4 owed | n/a | Plan Step 6 lesson not appended; REVIEW-LOG entry owed | Resolved | harmony-il and testing-qa lessons appended; REVIEW-LOG entry added |
| 13 | A4 merge note | n/a | Untracked `docs/reviews/codex-adversarial-012-...prompt.md` | Resolved | Committed with the review record (sibling `*.prompt.md` files are tracked) |

False positives: none. Codex's integration-test suggestion is not a false positive but is not
applied (see NOT APPLIED).

## DETAILS

**Agent 1 (Standards).** All ten checks pass on the C#. Violations: findings 1 to 5 above. Its
UNVERIFIED suite count is resolved by the lead's run. FOLLOW-UP items are listed below.

**Agent 2 (Engine).** 10 verified, 0 incompatible, 0 unverified: `LoadingWindow` is a static class
with an empty `.cctor`; `IsLoadingWindowActive` is `public static bool` with a private setter;
`DisableGlobalLoadingWindow()` has one overload; Harmony 2.4.2 keys `__state` per patch class, and a
void Prefix whose only injection is `__state` does not affect the original, so it runs even after a
foreign prefix returns `false`. The PatchShield finalizer on the same method has its own state local.
One LOW (finding 6). One FOLLOW-UP (pre-existing doc on `MapScreen.HandleIfBlockerStatesDisabled`).

**Agent 3 (Efficiency).** No issues. A no-op frame now costs two static reads, two patch calls and
one comparison, no allocation. The log figures (84 MB, 1.16 GB, about 360 a second) match the logs.
Patching a narrower engine seam was considered and rejected under the simplicity criterion.

**Agent 4 (Completeness).** Tests, feature doc and CHANGELOG present; IoC and SubModule need nothing
(the existing `PatchCategory` call applies the new Prefix). Missing: the GitHub issue (finding 1).
LOW: findings 3 and 8. Owed: lesson, REVIEW-LOG entry, in-game smoke. No 1.4.5 port owed (no
MapLoadDiagnostics on `bannerlord-1.4.5`).

**Agent 5 (Data flow).** 10 flows traced, all CONNECTED except trace 5 (finding 10), trace 8
(findings 2, 3) and trace 9 (finding 8). The Prefix, `__state`, gate, tracer and logger chain is
intact, the patch category is applied in `OnSubModuleLoad` before the first lower, and nothing in
`Main/` or `tools/` parses the lowered line.

**Agent 6 (Design).** One KEEP, PRESERVING, APPLY-scoped: the screen list and the gate doc's
"unconditionally" (findings 6, 7). The hook point, the Prefix plus `__state` shape, the gate class
(the only coverage of the (true, true) cell) and the test design were found already optimal.

## ACTION ITEMS

1. NEEDS MIKE: file the GitHub issue, add `(#N)` to the CHANGELOG heading, and at close label it
   `triage-needs-ingame` with the owed smoke (a minute on the main menu and the party screen, then a
   campaign load; expect a single-digit `LOADING-WINDOW lowered` count, and a first chain frame of
   `DisableGlobalLoadingWindow_Patch2` is normal, not a regression).
2. Convergence pass owed: Step 4.6 asks for one `deep-reviewer` on the applied improvements. This
   delegate cannot spawn agents; the orchestrator runs it on `6f7ddd39..HEAD` of this branch. The
   lead's own parity check: the only production change is comment text (`git diff` shows no code
   line changed in `Main/`), and the full suite is green apart from the two known Armory tests.
3. Merge planning (not defects): plans 008, 009 and 012 each insert a `## 2026-09-24` CHANGELOG
   block at the same point; keep one heading and stack the entries. `E:\repos\TAOM` has another
   session's uncommitted `CHANGELOG.md` and `feature-map.md` edits; never stash them to merge.

## IMPROVEMENTS (Step 4)

APPLIED:
- `Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs:7,17-22`: class summary
  now "every raise, and every lower that actually took the window down"; the full screen list.
  Comment only. Proof: full suite green before (builder's run, re-verified by the filtered baseline,
  26 passed) and after (below).
- `Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs:5-9`: the clear happens "whenever a
  loading-window manager exists", with the null-manager and skipped-original cases named. Comment
  only; `LoadingWindowTraceGateTests` (4) green before and after.
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs:7-8`: docstring screen
  list.
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs`: renamed the two
  signature tests; new `PrefixThenPostfix_WhenTheWindowIsAlreadyDown_CapturesFalseAndLogsNothing`;
  `Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers` now asserts exactly one
  `LogInfo`, no `<none>` or `<unavailable>` chain, and a first caller that is not
  `LoadingWindow_Disable_Patch`. Proof: 5 passed on the correct code; mutant A (`__state = true`)
  fails the round-trip test; mutant B (a `NoInlining` helper between the Postfix and
  `TraceWithCallers`) fails the lowered test; both mutants reverted, and `git status` showed the patch file clean before the comment edits.
- `docs/features/map-load-diagnostics.md:49-58`: screen list; raises are traced per call.
- `docs/reference/feature-map.md:34`, `docs/reference/harmony-patch-registry.md:1024`: wording and
  backticks.
- `CHANGELOG.md`: screen list, "calls the lower" wording, nine tests, the lead's suite totals, and a
  `Reviewed:` paragraph.

NOT APPLIED:
- Codex P3-1 integration case (raise the window in the test host, lower it, require one trace, then
  repeated disables require none): needs reflection on the engine's private `IsLoadingWindowActive`
  setter or `EnableGlobalLoadingWindow` (a manager plus native `Utilities`), both ruled out by the
  plan; a Prefix hard-coded to `false` therefore stays unpinned in unit tests and is left to the
  in-game smoke. Not behaviour-changing, so not a Mike question; recorded as a test-design limit.
- Codex P3-2 "require a deliberately identifiable caller from a non-inlined test wrapper": replaced
  by the negative assertion on the first caller, which catches the helper hop without depending on
  the JIT keeping the test method's frame.
- Behaviour-changing proposals: none were raised, so no Mike batch was needed.

FOLLOW-UP (pre-existing code, not applied; no issue filed because this delegate cannot file public
issues):
- `docs/features/map-load-diagnostics.md:65-68` and the unchanged sentence in
  `harmony-patch-registry.md:1024` omit `_isSceneViewEnabled` and the conversation branch of
  `MapScreen.HandleIfBlockerStatesDisabled` (Agent 2).
- Some lowers bypass the managed flag (`MBInitialScreenBase.cs:111` and a MountAndBlade.View call to
  native `Utilities.DisableGlobalLoadingWindow()`, `LoadingWindow.Destroy()`), so neither the old
  nor the new trace sees them (Agent 5, informational).
- `LoadingWindow_Transitions_Patch.cs` holds two patch classes and none named after the file; this
  is the feature's established style (Agent 1).
- `FileLogger` has no size cap (the plan's Maintenance notes already defer it; Agent 3).

**Final full suite** (worktree, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`):
`Failed!  - Failed: 2, Passed: 10244, Skipped: 2, Total: 10248` (net472). The two failures are the
known live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

VERDICT: READY FOR COMMIT (the MED issue gap is a process item for Mike, not a code defect; the
Step 4.6 convergence pass is owed by the orchestrator)

## CODEX REVIEW

Codex gpt-6-astra, reasoning ultra, 110,352 tokens, raw output
`docs/reviews/raw/codex-adversarial-012-loading-window-trace-per-frame-2026-09-24.md` (complete: ends
with "END OF CODEX REVIEW"). It read the branch through git refs, decompiled the installed v1.5.3
`TaleWorlds.Engine.dll`, `TaleWorlds.MountAndBlade.dll`, `SandBox.GauntletUI.dll` and
`TaleWorlds.MountAndBlade.GauntletUI.dll` fresh, cross-referenced every identifier (config table),
and answered ten Known Suspects (5 DISPUTED, 4 UNVERIFIED as historical, 1 PARTLY CONFIRMED).
Disposition: 0 P1, 0 P2, 2 P3.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| P3-1 | P3 (coverage) | LOW | Yes | The Prefix test checks metadata only; the `__state = true` mutant passed all eight tests before the fix and fails the new round-trip test after it. Codex's stronger integration case not applied (engine flag cannot be raised in the test host without means the plan ruled out) |
| P3-2 | P3 (coverage) | LOW | Yes | `callers:` matched the fallback strings; a helper-hop mutant passed before and fails after. The "identifiable caller" form was replaced by a negative first-caller assertion (JIT-robust) |
| KS-1 | DISPUTED | n/a | Yes | The engine excerpt matches; the manager-guard qualification is handled by the gate |
| KS-3 | UNVERIFIED | n/a | Yes | The getter is managed and the `.cctor` empty; the new round-trip test reads it in the host without error |
| KS-5 | DISPUTED | n/a | Yes | Codex noted the CHANGELOG edit was reserved for the orchestrator in the plan; it is a separate commit and is kept |
| KS-6 | UNVERIFIED | n/a | Resolved | `HarmonyFieldInjectionNamingTests` and `HarmonyPatchBindingTests` pass in the lead's full run |

- **Confirmed bugs:** none in production code. Two test-coverage gaps (P3-1, P3-2), fixed.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** the stale "every raise and lower" phrase in `feature-map.md:34` and the
  class summary (finding 2, 3); the incomplete screen list beyond the three screens the prompt named
  (finding 6; Codex verified only the three it was given and qualified character creation's
  render-ready guard); the raise-per-call reading trap (finding 10).

Root cause table for the Codex findings: `docs/reviews/rca-loading-window-trace-per-frame-2026-09-24.md`,
"Codex adversarial review".

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed lines for "Lessons From Prior Reviews":

- **What Codex does well:** re-derives engine behaviour from freshly decompiled installed DLLs and
  qualifies absolute words in the briefing ("unconditionally", "every frame") against the guard it
  finds; also treats a test that asserts shape under a behaviour name as a coverage gap.
- **Bugs Codex typically misses:** stale copies of a changed phrase in files outside the diff (the
  feature map, a class summary above the edited paragraph), and a caller list it was handed in the
  prompt; it verifies the named callers rather than enumerating all of them.
- **False positives:** none new.
