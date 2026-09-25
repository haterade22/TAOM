# Deep review: plan 008, the binding gate fails loudly instead of passing by skipping

```
DEEP REVIEW REPORT
===================
Feature: plan 008, make the binding gate fail loudly instead of passing by skipping
         (branch improve/008-binding-gate-no-silent-skips, diff 7f02fc8d..8c89e042)
Date: 2026-09-24

Scope:   C# (TAOM.Tests: GameAssemblies, GameAssembliesResolutionTests, the two gate classes),
         XML (TAOM.Tests.csproj, binding-gate.runsettings), CI (build.yml), scripts
         (notify-test-results.sh, test_hooks.sh), harness (verify-bindings SKILL.md), docs
         (CHANGELOG, hooks-catalog, agent-operating-manual, s6-runtime-punchlist, api-snapshot
         README). No live TAOM_Map or Armory file is in scope: the change touches no ModuleData.
Waves:   the orchestrator ran the lenses; this report compiles Agents 1, 2, 5, Tooling, 3, 4, 6
         and the Codex adversarial review.

STANDARDS:     FAIL, 4 violations (1 MED, 3 LOW), all fixed
COMPATIBILITY: PASS, 0 incompatible, 1 unverified (typesFailedToLoad is 0 today; FOLLOW-UP);
               3 LOW doc findings, all fixed
EFFICIENCY:    PASS, 0 issues in the changed hunks (1 FOLLOW-UP)
COMPLETENESS:  INCOMPLETE: GitHub issue missing (NEEDS MIKE); resolver guard tests added here
DATA FLOW:     FAIL, 1 gap (the skip banner is never delivered), 3 inconsistencies; the
               inconsistencies are fixed, and the gap's claims are corrected (its channel is NEEDS MIKE)
DESIGN:        3 KEEP proposals (2 apply-scoped, both behaviour-changing: NEEDS MIKE; 1 follow-up)
XML:           NOT IN SCOPE (no ModuleData; the csproj and runsettings were read by Tooling and
               Agent 2)
TOOLING:       FAIL, 6 findings (2 MED, 4 LOW): 3 fixed, 2 NEEDS MIKE, 1 not applied
CODEX:         ISSUES, 1 P2 and 1 P3, both confirmed (P2 overlaps Agents 1 and 5)
```

## Details

Every finding below was re-checked against the worktree before it was classified. The RCA
(`docs/reviews/rca-binding-gate-no-silent-skips-2026-09-24.md`) numbers the confirmed defects F1
to F13. This section maps each lens's findings to those numbers.

### Agent 1: Standards

| Lens item | Verdict | Evidence and action |
|---|---|---|
| 1 MED: the banner goes to a channel nobody reads | CONFIRMED (F1) | The hook writes to stderr and ends `exit 0`. `harness-facts.md:60` says that stderr never reaches Claude, and so does `hooks-catalog.md:53-56`. Claims corrected; the channel is NEEDS MIKE. |
| 2 LOW: two resolver guards unpinned | CONFIRMED (F3) | Two tests added. Deleting each guard (a scripted mutation, then restore) turned exactly one test red: `OverrideWithoutBannerlordExe_FallsThroughToTheGameDir` and `GameDirNotOnDisk_FallsBackToTheBuildFolder`. |
| 3 LOW: "every gate test calls Assert.Inconclusive" | CONFIRMED (F4) | `6c.log`: Passed 33, Failed 335. Fixed. |
| 4 LOW: CHANGELOG date | CONFIRMED (F9) | `git log -1 --format=%ci 8c89e042` gives 2026-09-23 23:25:03 -0500. Moved under `## 2026-09-23`. |

### Agent 2: Engine compatibility

7 API usages verified, 0 incompatible, 1 unverified (FOLLOW-UP 1 below).

| Lens item | Verdict | Evidence and action |
|---|---|---|
| F1 LOW: `SKILL.md:42` omits the two new red forms | CONFIRMED (F5) | The Step 2 table listed three classes. The `6c.log` messages are `Game assemblies not loaded` (326), `unavailable` (8) and `Game dir unresolved` (1). Fixed: an environment sentence and a floor row. |
| F2 LOW: quantifier and "when neither is set" | CONFIRMED (F4, F8) | `GameAssemblies.cs:137-140` falls back on a missing folder too. Fixed in `SKILL.md:27` and the doc comment. |
| F3 LOW: stale counts in the floor messages | CONFIRMED (F6) | `gamemodel-bases.md:7` says `Models: 46` and `patch-targets.md:7` says `Patches: 247`. The messages now point to those files. |

### Agent 3: Efficiency

No issues in the changed hunks. FOLLOW-UP 1 (both PostToolUse Bash hooks start Python on every Bash
call) is listed below.

### Agent 4: Completeness

| Lens item | Verdict | Evidence and action |
|---|---|---|
| GitHub issue missing | CONFIRMED process gap, NEEDS MIKE | This delegate cannot file a public issue: `/issue` is never auto-invoked, and the GitHub MCP is unauthenticated in this session. |
| F1 MED: resolver guards | CONFIRMED (F3) | Fixed, as for Agent 1 item 2. |
| F2 LOW: metadata test accepts `""` | FALSE POSITIVE | Plan 010's reference-assembly build deliberately emits an empty `TaomGameFolder` (`plans/010-ci-on-hosted-windows.md:272-276`, `:782-783`), so a non-empty assert would break it. |
| F3 LOW: CHANGELOG date | CONFIRMED (F9) | Fixed. |
| F4 LOW: RCA closure overclaim | CONFIRMED (F10) | `rca-lord-identity-2026-08-29.md:77-80` names only `LordFamilyTransformTests`. Reworded. |
| F5 LOW: skill quantifier; README gives no reason for `--settings` | CONFIRMED (F4); README part not applied | The skill is fixed. The README reason is already given one click away, in the skill Pre-flight and the runsettings header comment. |
| F6 LOW: zero-match filter passes | CONFIRMED (F12), NEEDS MIKE | Measured: the zero-match run exits rc 0 today. |
| F7 LOW: no banner for skip-only output | CONFIRMED (F2) | Fixed, test first. |
| F8 LOW: CI step skipped when Test fails | CONFIRMED (F13), NEEDS MIKE | `build.yml` gives the step no `if:`. |
| Untracked Codex prompt file | CONFIRMED | Committed with this record. |

### Agent 5: Data flow

10 flows traced. Gaps and inconsistencies:

| Lens item | Verdict | Evidence and action |
|---|---|---|
| F7 GAP MED: banner never delivered | CONFIRMED (F1) | See Agent 1 item 1. |
| F5 LOW: `reflection-sites.md:10` stale command | CONFIRMED (F7) | Fixed to the strict command with its own filter. |
| F6 LOW: skill triage table and quantifier | CONFIRMED (F4, F5) | Fixed. |
| F10 LOW: doc comment's "-p: property" case | CONFIRMED (F8) | `BANNERLORD_GAME_DIR` wins at `:137` whenever it names an existing folder. Fixed. |
| F1 minor: missing-folder fallback unpinned | CONFIRMED (F3) | Fixed by `GameDirNotOnDisk_FallsBackToTheBuildFolder`. |

### Agent 6: Design and elegance

Three KEEP proposals. See IMPROVEMENTS.

### Agent 7: XML and ModuleData

NOT IN SCOPE.

### Tooling correctness

| Lens item | Verdict | Evidence and action |
|---|---|---|
| 1 MED: silent on an all-skipped run at normal verbosity | CONFIRMED (F2) | RED: the hook, driven with `Test Run Successful. / Total tests: 5 / Skipped: 5`, printed nothing. GREEN: it now prints `PASSED WITH SKIPS (Passed: 0, Skipped: 5; ...)`. Pinned by a new 7c case. |
| 2 MED: strict gate exits 0 on a zero-match filter | CONFIRMED (F12), NEEDS MIKE | Today: rc 0. With a scratch copy of the settings plus `TreatNoTestsAsError`, the zero-match run gives rc 1 and the gate gives rc 0 (368 passed, 0 skipped). |
| 3 LOW: CI step skipped after a red Test step | CONFIRMED (F13), NEEDS MIKE | Changes CI behaviour. |
| 4 LOW: two guards tested one way only | CONFIRMED (F3) | Fixed. |
| 5 LOW: skill "every gate test" | CONFIRMED (F4) | Fixed. |
| 6 LOW (optional): unresolved diagnostic names no paths | NOT APPLIED | Simplicity criterion. The message fires only when both variables and the metadata fail. The metadata value is always the build's `GameFolder` (`obj/.../TAOM.Tests.AssemblyInfo.cs:13`), and this session could not exercise the path without a build that has no `GameFolder`. |

## Codex review

`docs/reviews/raw/codex-adversarial-008-binding-gate-no-silent-skips-2026-09-24.md` is complete (it
ends with `END OF CODEX REVIEW`; 117,503 tokens). Codex found 1 P2 and 1 P3. It verified its vanilla
excerpts against the v1.5.3 dump and cross-referenced every identifier the change adds. Of its ten
Known Suspects, 2 were confirmed (both are finding 1) and 8 were disputed or left unverified, with
reasons.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MED | Yes | The hook prints the banner to stderr and exits 0 (`notify-test-results.sh`). `harness-facts.md:60` says that stderr reaches the debug log only. 7c pins the text through `2>&1 >/dev/null`, not the delivery. Claims corrected. Codex's fix (`hookSpecificOutput.additionalContext` on stdout) is a delivery-channel change that needs `/research` and a live proof: NEEDS MIKE. |
| 2 | P3 | LOW | Yes | `head -1` on each count (`:49`, `:50`, `:54`) keeps the first summary only. Confirmed by reading; not run. Not fixed: it changes the old first-match parsing of Failed and Passed, and it only matters once finding 1's channel is chosen. NEEDS MIKE, together with finding 1. The catalog now says "from the first summary line". |

- **Confirmed bugs:** 1 and 2 (see above).
- **False positives:** none.
- **Design questions:** the delivery channel for finding 1, and whether to aggregate summaries
  (finding 2).
- **Things Codex missed:** F2 to F10 (the normal-verbosity silence, the unpinned guards, and the
  doc and skill claims). Codex audited code paths and identifiers, not prose claims.

### Phase 3e root cause (Codex findings)

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Skip banner never reaches Claude | Dead / no-op code | The plan specified stderr. `hook-authoring.md:128` advises stderr for advisory hooks, against `harness-facts.md:60`. 7c tested the text, not the delivery. | Lesson "An advisory hook's output must reach Claude" in `lessons/build-tooling-workflow.md`. Recommended edit to `hook-authoring.md:128`. |
| 2 | Later summaries' skips dropped | Logic error | Every fixture held one summary. | Deferred with finding 1: a fixture with a zero-skip summary followed by a non-zero one belongs with the aggregation fix. |

### AGENTS.md lessons (pending, Phase 3h consolidated later)

- **What Codex does well:** it cited the vendor hook contract to show that a hook's output channel
  reaches no one, and it wrote a counterexample input (two summaries) that the fixtures did not
  cover.
- **Bugs Codex typically misses:** prose and skill claims contradicted by a measured run ("every
  gate test" against 33 passes), a triage table not updated for new failure messages, and a guard
  whose deletion no test catches. Codex reviewed code paths and left docs and test adequacy
  unaudited.
- **False positives:** none new.

## Action items

1. NEEDS MIKE: choose the skip banner's channel. Either (a) drop the hook hunk and 7c and rely on
   the tool result's `Skipped:` count, or (b) emit `hookSpecificOutput.additionalContext` on stdout
   after `/research` and a live check. Note for (b): the full suite always carries 2 `[Ignore]`
   Warg skips, so the banner would fire on every full run. Codex P3 (sum the counts over every
   summary) goes with (b).
2. NEEDS MIKE: add `<RunConfiguration><TreatNoTestsAsError>true</TreatNoTestsAsError></RunConfiguration>`
   to `binding-gate.runsettings`. Measured effect: the zero-match run goes from rc 0 to rc 1, and
   the gate is unchanged.
3. NEEDS MIKE: `if: ${{ !cancelled() }}` on the new CI step.
4. NEEDS MIKE: resolve the build folder first (Agent 6 P1). This reverses the plan's deliberate
   deferral.
5. NEEDS MIKE or the orchestrator: file the GitHub issue for plan 008 and add `(#N)` to both
   CHANGELOG headings.

## Improvements (Step 4)

**APPLIED:**
- `TAOM.Tests/Migration/GameAssembliesResolutionTests.cs`: two guard tests (Tooling #4, which is
  behaviour-preserving). Green before and after. Each guard deletion turns one test red.
- `.claude/skills/verify-bindings/SKILL.md:27` wording (Tooling #5, behaviour-preserving, prose).
- Defect fixes (not Step 4 proposals): the hook's zero-Passed branch (F2, with a RED 7c case
  first), the floor-message counts, the doc comment, `reflection-sites.md`, the hooks catalog row,
  the CHANGELOG date and wording, and the skill's triage table.

**NOT APPLIED:**
- `TAOM.Tests/Migration/GameAssemblies.cs:131-143`, Agent 6 P1 (build folder first):
  behaviour-changing, and it reverses the plan's deliberate deferral. Needs Mike.
- `TAOM.Tests/binding-gate.runsettings`, Agent 6 P2 and Tooling #2 (`TreatNoTestsAsError`):
  behaviour-changing. Needs Mike. The evidence is in action item 2.
- `.github/workflows/build.yml:282`, Tooling #3 (`if: ${{ !cancelled() }}`): behaviour-changing in
  CI. Needs Mike.
- `TAOM.Tests/Migration/GameAssemblies.cs:51`, Tooling #6 (name the inputs in the diagnostic): the
  simplicity criterion rejects it (small win, and the path cannot be exercised here).
- `.claude/hooks/notify-test-results.sh`, Codex P3 (aggregate summaries): deferred with the
  channel decision. Needs Mike.
- Step 4.6 convergence pass: not run. This delegate cannot spawn agents, so the orchestrator's
  second pass covers the fix diff.

**FOLLOW-UP** (pre-existing code; no issue filed, since this delegate cannot file issues):
- `HarmonyPatchBindingTests.cs:84-88` and `GameModelOverrideBindingTests.cs:157-159`: fail on
  `typesFailedToLoad > 0` and print the LoaderExceptions (Agent 2 FU1, Agent 5 FU1, Agent 6 P3).
  Measure first; it is UNVERIFIED that the count is 0 today.
- `notify-test-results.sh` and `mark-verification-run.sh`: exit early before Python starts on a
  non-test Bash call, about 185 ms per call (Agent 3 FU1). Also the missing `[ -n "$PYBIN" ]`
  guard, and a Bash-only matcher that misses PowerShell runs (Agent 1).
- `.claude/rules/hook-authoring.md:128`: the stderr advice for an advisory hook contradicts
  `harness-facts.md:60`. This is the root cause of F1, and the rule is left unedited here to avoid
  conflicts between parallel branches.
- `GameAssemblies.cs:145-152` against `Directory.Build.props:40-41`: the bin-folder precedence
  differs (Agent 2 FU2, Agent 5 FU4). There is no divergence on this install.
- Stale gate text: `HarmonyPatchBindingTests.cs:21,71` (v1.4.5), the api-snapshot `README.md:37,41`
  (v1.5.2, "All 220"), `verify-bindings/SKILL.md:35` ("Three test classes"; the category spans
  about 67 files), and `s6-runtime-punchlist.md:10` ("All 39 GameModels").
- Ten default-suite tests read `BANNERLORD_GAME_DIR` with a hard-coded `E:\Steam` fallback and
  skip the new resolver (Agent 5 FU3). This belongs to the locator-consolidation plan.
- An `[Ignore]` on a gate test still skips green under strict settings (Agent 5 FU5).
- `.ai/verification.md` has no row for the strict step. `development-machines.md:27` does not list
  the test gate as a reader of the variable.
- `GameAssemblies.cs:33-34`: `GameDir` and `BinFolder` should be `string?` (Agent 1).
- BUTR BHA0001 on `Patch88_InitializeLordPartyPropertiesScope.cs:22` is an analyzer false positive
  (Agent 2 FU4).

## Verification

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~GameAssembliesResolutionTests"`:
  6/6 before the change, 8/8 after, and 7/8 with each guard deleted in turn.
- The strict gate (`--settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"`):
  `Passed: 368, Skipped: 0`, rc 0.
- The full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10243, Skipped: 2, Total: 10247`. The two failures are the known live-Armory
  tests, `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
- `bash tools/test_hooks.sh`: `287 passed, 0 failed`, rc 0.

VERDICT: READY FOR COMMIT. The remaining open items are the five NEEDS MIKE items above: the banner
channel (with Codex P3), `TreatNoTestsAsError`, the CI `if:`, build-folder-first, and the GitHub
issue.

## Convergence

The orchestrator's convergence pass reviewed the fix diff `8c89e042..549afffd` and reported three
LOW defects. Each was checked against the code before it was fixed; none was a false positive.

| # | Defect | Verified | Fix |
|---|---|---|---|
| D1 | The new all-skipped branch in `notify-test-results.sh` ran before the `grep -q "Failed"` fallback, so `Test Run Failed.\nTotal tests: 5\n Skipped: 5` and `Test Run Aborted.\nTotal tests: Unknown\n Skipped: 5` both printed `PASSED WITH SKIPS`. At `8c89e042` they printed `FAILED (counts unavailable)` and nothing | Two new 7c cases went red on the `549afffd` hook (`287 passed, 2 failed`), both with `PASSED WITH SKIPS (Passed: 0, Skipped: 5 ...)` | With no `Passed:` count, the skip branch now needs `Test Run Successful.`; failed and aborted runs fall through as before |
| D2 | `verify-bindings/SKILL.md:42` said "Every other failure is one of the classes below". `GameModelOverrideBindingTests.cs:58` (`Main/SubModule.cs not found`) and `HarmonyPatchBindingTests.cs:178` (`... grew a body ...`) are in the category and match no row | Read both assertions; `grep -rl 'TestCategory("BindingVerification")' TAOM.Tests` lists 66 files | Step 2 names `Main/SubModule.cs not found` as a precondition to report, calls the table the common classes, and says an unmatched failure is still a finding |
| D3 | `GameAssembliesResolutionTests.cs:7-10` said the fallback fires "when neither is set", which the class's own two new tests contradict; `GameModelOverrideBindingTests.cs:14` said "~37" models against `gamemodel-bases.md:7` `Models: 46.` | Read both comments and the snapshot line | The first now says "when neither names a usable folder"; the second points at the snapshot for the count |

The stale-text FOLLOW-UP above is unchanged; the `:14` line it missed is fixed here.

**Convergence verification:**
- `bash tools/test_hooks.sh`: `289 passed, 0 failed` (the two new 7c cases included).
- The full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10243, Skipped: 2, Total: 10247`. The two failures are the known live-Armory
  tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
- The C# edits are comment-only, so the strict gate was not re-run.

## Maintainer decisions applied (2026-09-24)

Mike answered three of the NEEDS MIKE action items on 2026-09-24; the issue is #652. All three are
in one commit on top of `2ca0805b`: `fix(bindings): v2.0.30 - apply maintainer decisions for plan
008`, the commit that adds this section.

| # | Action item | Decision | Applied |
|---|---|---|---|
| 1 | The skip banner's channel (with Codex P3) | Option (a): drop the banner change. The signal is the `Skipped:` count in `dotnet test`'s own output plus the strict gate runsettings | `.claude/hooks/notify-test-results.sh` restored to its content at `7f02fc8d` and `tools/test_hooks.sh` section 7c removed (`git diff 7f02fc8d` on both is empty). The hooks catalog row now says the hook reports no skips and names the two signals. The CHANGELOG, the RCA (a resolution section) and REVIEW-LOG note the removal. Codex P3 (F11) lapses with the banner, and so do F2, the convergence fix D1 and F1's correction to the hook header (`:5` again reads "prominently"). |
| 2 | `TreatNoTestsAsError` | Add it to `binding-gate.runsettings` | Added under `<RunConfiguration>`. Test first: `BindingGateRunSettingsTests` failed with `Assert.AreEqual failed. Expected:<true>. Actual:<(null)>. binding-gate.runsettings must set <RunConfiguration><TreatNoTestsAsError>true</TreatNoTestsAsError></RunConfiguration>`, then passed (2 of 2; the second row pins `MapInconclusiveToFailed`). The verify-bindings skill's Step 2 names the zero-match message. |
| 4 | Build folder first (Agent 6 P1) | Keep the order as built: `BANNERLORD_OVERRIDE_DIR`, then `BANNERLORD_GAME_DIR`, then the build's `TaomGameFolder` | No code change. |

Action item 3 (`if: ${{ !cancelled() }}` on the CI step, F13) is moot, as the decisions on #652
record: plan 010's hosted CI deletes this job. A #652 comment records no port to
`bannerlord-1.4.5`. Action item 5's issue is #652; the three earlier plan 008 CHANGELOG headings
carry it since the round-two follow-ups
(`deep-review-008-binding-gate-no-silent-skips-decisions-2026-09-24.md`).

**Verification of the decisions:**
- A filter that matches no test under the strict settings,
  `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=NoSuchCategory008"`:
  rc 0 before the change, rc 1 after, both printing `No test matches the given testcase filter`.
- The strict gate,
  `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter TestCategory=BindingVerification`:
  `Passed!  - Failed:     0, Passed:   368, Skipped:     0, Total:   368`, rc 0.
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: 0 errors.
- The full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10245, Skipped: 2, Total: 10249`. The two failures are the known live-Armory
  tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; the two new passes are
  `BindingGateRunSettingsTests`.
- `bash tools/test_hooks.sh`: `283 passed, 0 failed`, rc 0 (the six 7c cases are gone).
