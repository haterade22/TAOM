# Deep review: plan 008 round two, the maintainer decisions follow-up

```
DEEP REVIEW REPORT
===================
Feature: plan 008, make the binding gate fail loudly (maintainer decisions on #652)
Date: 2026-09-25 (the decisions and the first review are dated 2026-09-24)

Scope:   diff 2ca0805b..37306bca on improve/008-binding-gate-no-silent-skips (worktree
         E:\repos\taom-improve\wt-008): 1 C# test, 1 runsettings, 1 hook and tools/test_hooks.sh
         (reverted), 1 skill, CHANGELOG, hooks catalog, three review records
Waves:   one wave: Agents 1 to 6 and the Tooling lens; Codex adversarial beside it; review lead
         (this report) for Steps 3, 3e and 4. No Agent 7: no ModuleData or XSLT in the diff

STANDARDS:     PASS (checks 1 to 10 and H1 to H6); 3 LOW and 4 NIT on prose and reuse
COMPATIBILITY: PASS: 0 incompatible, 2 unverified (hook stderr visibility, a harness claim; the
               zero-match rc, now recorded on disk as rc=1)
EFFICIENCY:    PASS: 0 issues in the changed hunks (1 LOW reuse, 1 pre-existing MEDIUM follow-up)
COMPLETENESS:  INCOMPLETE (records only): 5 LOW
DATA FLOW:     PASS: 13 flows, 0 gaps, 5 inconsistencies (all LOW)
DESIGN:        3 KEEP proposals (2 APPLY, 1 FOLLOW-UP)
XML:           NOT IN SCOPE
TOOLING:       PASS: 4 LOW findings, 4 follow-ups
CODEX:         0 P1, 0 P2, 1 P3 (confirmed; the same as R1)
```

## The three decisions

Every lens and Codex agree that all three are implemented as decided, and I re-checked each:

- **1, drop the banner:** `git diff 7f02fc8d 37306bca -- .claude/hooks/notify-test-results.sh
  tools/test_hooks.sh` is empty.
- **2, `TreatNoTestsAsError`:** present under `<RunConfiguration>` and pinned by
  `BindingGateRunSettingsTests`. The zero-match strict run,
  `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=NoSuchCategory008"`,
  printed `No test matches the given testcase filter` and `rc=1`, now recorded in
  `E:\repos\taom-improve\scratch\008r2\zero-match.log`. The rc 0 before the change is the first
  round's F12 measurement.
- **4, resolver order:** `GameAssemblies.cs` is not in the diff.

## Findings, verified

Each lens finding and the Codex P3 was checked against the worktree before any edit. Several lenses
reported the same defect, so they are merged below (R numbers as in the RCA).

| # | Sev | Finding | Reported by | Verdict | Evidence I checked |
|---|---|---|---|---|---|
| R1 | LOW | `hooks-catalog.md:42` "fails a skipped test" and `CHANGELOG.md:12-13` "fails the skip" overstate the settings | A1 #1, A2 F1, A4 #5, A5 #3, Tooling #1, A6 KEEP 2, Codex P3 | CONFIRMED | The runsettings holds only `MapInconclusiveToFailed` and `TreatNoTestsAsError`. The MSTest 3.1.1 adapter's only map-to-failed settings are those for Inconclusive and NotRunnable, so `[Ignore]` stays Skipped |
| R2 | LOW | `verify-bindings/SKILL.md:42` maps the zero-match message to a wrong filter only | A1 #7, A2 F2, A5 #2 | CONFIRMED | The adapter DLL contains `Unable to load types from the test source '{0}'. Some or all of the tests in this source may not be discovered.`; a load failure or a lost category also executes nothing under the filter |
| R3 | LOW | The first RCA and report record only F11 as lapsed; F2 and D1 lapsed too | A1 #4, A4 #3, Tooling #3 | CONFIRMED | `rca-...-2026-09-24.md` F2 row said "Fixed" and the resolution named F11 only; the restored hook has no all-skipped branch |
| R4 | LOW | The revert restored the "prominently" hook header, contradicting the catalog and the F1 row | A1 #5, A3, A4 #4, A5 #8, A6 KEEP 1 | CONFIRMED | `notify-test-results.sh:5`; `2ca0805b:.claude/hooks/notify-test-results.sh:5-7` held the corrected header |
| R5 | LOW | The report says two CHANGELOG headings lack #652; three do | A1 #3, A4 #2, A5 #11, Tooling #4, A6 | CONFIRMED | `CHANGELOG.md:23`, `:36`, `:57` at `37306bca` |
| R6 | LOW | Records call F13 "still open"; #652 records it moot, plus a no-port decision | A4 #1 | CONFIRMED | `gh issue view 652`: Decisions section ("The `if: ${{ !cancelled() }}` question is moot") and the 19:07Z comment ("no port to `bannerlord-1.4.5`") |
| R7 | NIT | REVIEW-LOG shows pre-decision counts after the decisions paragraph | A1 #6, Tooling #4 | CONFIRMED | `REVIEW-LOG.md:3803-3811` at `37306bca` |
| R8 | LOW | The pin test adds a private `FindRepoRoot` beside `RepoPaths.RepoPath` | A1 #2, A3 #1, A5 #12, Tooling #2 | CONFIRMED | `TAOM.Tests/Infrastructure/RepoPaths.cs:14`; `TAOM.Tests.csproj` sets no `PathMap`; 6 test files already use `RepoPaths` |

**False positives:** none. Two lens statements were observations, not defects: the strict gate's
own filter does not run the pin test (A5 #4; the default suite does, by design), and the pin test
proves configuration, not runner behaviour (Codex suspect 9; the runner evidence is the recorded
gate and zero-match runs).

**NEEDS MIKE:** none of the eight. The design items that need Mike are all follow-ups (below).

## Fixes applied (defects)

All are prose except R8.

- **R1:** the catalog row now reads "fails an `Assert.Inconclusive` (the gate's skip when it cannot
  load the game) and a filter that matches no test. An `[Ignore]`d test still reports Skipped and
  exits 0, so a gate run is green only at `Skipped: 0`." The decisions CHANGELOG entry says "fails
  an `Assert.Inconclusive` instead of skipping it".
- **R2:** Step 2 now says that on the Step 1 command as written a zero match is a finding (the
  MSTest load warning means the DLL did not load, `/investigate`; otherwise the gate tests lost
  their category), that a hand-written command gets its filter fixed, and never the settings.
- **R3, R4, R6:** the first RCA's resolution names F2, D1 and the header as lapsed, records F13 as
  moot per #652, and records the no-port decision. The first report's decisions row and its
  "still open" paragraph say the same.
- **R5:** `(#652)` added to all three earlier plan 008 CHANGELOG headings, as #652's own open item
  asks; the report's count now says three.
- **R7:** the REVIEW-LOG entry labels its counts before and after the decisions.
- **R8:** see APPLIED below (it is also Agent 3's APPLY fix).

## Verification

- Characterisation of the pin test, before and after R8:
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~BindingGateRunSettingsTests"`:
  `Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2`, rc 0 both times
  (`scratch\008r2\char-before.log`, `char-after.log`).
- Mutation after R8: with `<TreatNoTestsAsError>true</TreatNoTestsAsError>` removed from a working
  copy of the settings, the `(RunConfiguration,TreatNoTestsAsError)` row failed with
  `Expected:<true>. Actual:<(null)>` (rc 1, `mutation.log`); the file was restored and
  `git diff` on it is empty.
- Zero-match strict run: `rc=1` (`zero-match.log`).
- Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed!  - Failed: 2, Passed: 10245, Skipped: 2, Total: 10249`. The two failures are the known
  live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; the branch is based on `7f02fc8d`,
  before `a39a9c86` (`full.log`).
- `tools/test_hooks.sh` was not re-run: no hook, settings or skill frontmatter changed in this
  round (Agent 1 ran it on `37306bca`: 283 passed, 0 failed).

## ACTION ITEMS

1. None blocking. Every confirmed finding is fixed or recorded in this round.
2. Mike, at merge: the NEEDS MIKE follow-ups below.

## IMPROVEMENTS (Step 4)

**APPLIED:**
- `TAOM.Tests/Migration/BindingGateRunSettingsTests.cs:1-23`: the private `FindRepoRoot` and
  `using System.IO;` are gone; the test loads `RepoPath("TAOM.Tests", "binding-gate.runsettings")`
  through `using static TAOM.Tests.Infrastructure.RepoPaths;`. Behaviour-preserving (Agent 3 #1,
  Tooling #2, Agent 1 #2, Agent 5 #12). Proof: 2 of 2 before and after, and the mutation above.
- `docs/reference/hooks-catalog.md:42` and `CHANGELOG.md`: Agent 6 KEEP 2 (R1). Docs only.

**NOT APPLIED:**
- `.claude/hooks/notify-test-results.sh:5`, restore the corrected three-line header from
  `2ca0805b` (Agent 6 KEEP 1): plan 013 Step 4 (`plans/013-bash-hook-prefilter.md:465-477`)
  anchors on the exact restored line and replaces it, and decision 1 was implemented as a
  byte-exact revert. The lapse is recorded in the first RCA's resolution instead; correct the
  wording in plan 013's replacement block.
- Caching the parsed `XDocument` across the two data rows (Agent 3, rejected by Agent 3 itself):
  a tiny win for added complexity (simplicity criterion).
- Delete `notify-test-results.sh`, its `settings.json` registration and its catalog row (Agent 6
  FOLLOW-UP): behaviour-changing for anyone reading the debug log, touches `settings.json` outside
  the plan's scope, and decision 1 kept the hook. Needs Mike.

**FOLLOW-UP** (pre-existing code or outside this diff; no issue filed, since `/issue` is never
auto-invoked and #652 carries the gate's open items):
- **Needs Mike:** delete the now-readerless `notify-test-results.sh` (starts Python on every Bash
  call, 230 to 315 ms measured by Agent 3, to write a line only the debug log sees), or give it a
  visible channel.
- **Needs Mike:** a guard against `[Ignore]` in the gate: a default-suite test failing when a
  `BindingVerification` class or method carries `[Ignore]`, or a CI check that the strict step's
  summary has `Skipped: 0` (Agent 2, first round FU5).
- **Needs Mike (or the orchestrator at close):** #652's body still lists the banner and 7c as part
  of the change and "Three entries" for the CHANGELOG; update it at close.
- Both PostToolUse(Bash) hooks start Python before checking their trigger (Agent 3, MEDIUM): plan
  013.
- A default-suite test that pins `--settings TAOM.Tests/binding-gate.runsettings` on every gate
  command (`build.yml:283`, `SKILL.md:32`, api-snapshot `README.md:46`, `:66`,
  `agent-operating-manual.md:43`, `s6-runtime-punchlist.md:13`, `reflection-sites.md:10`)
  (Tooling FU2): worth doing with plan 010's CI rewrite.
- The same overclaim in older lines: the CI step name `Binding gate (a skip fails)`
  (`build.yml:282`) and the `CHANGELOG.md` bullet "A skip in the gate is a failure." in the
  2026-09-23 entry (Agent 2).
- Until plan 010 lands, a manual dispatch of `build.yml` skips the strict step whenever the default
  Test step fails, which it does on this runner (Agent 2). Moot by decision once plan 010 deletes
  the job.
- The 35 private `TAOM.sln` walkers in `TAOM.Tests`: the locator consolidation (TEST-L5-03), ending
  with a ratchet test (this RCA's R8 lesson).
- `docs/reviews/LESSONS-LEARNED.md` per-category counts were not bumped: they already drift from
  `grep -c '^### '` (167 against 174 in build-tooling-workflow at `37306bca`), and every parallel
  improve branch appends lessons; recount once after the merges.
- `hook-authoring.md:128` stderr advice, `.ai/verification.md` strict-step row: still open from
  round one.

**Convergence pass:** not run. Step 4.6 asks for one `deep-reviewer` on the applied improvements;
this review lead cannot spawn agents, so it is left to the orchestrator's second pass. The applied
diff is a 13-line test simplification with a characterisation and a mutation proof, plus prose.

## CODEX REVIEW

Codex (gpt-6-astra, reasoning ultra, 145,091 tokens) reviewed `2ca0805b..37306bca` read-only from
git refs; the raw output is `docs/reviews/raw/codex-adversarial-008-binding-gate-no-silent-skips-decisions-2026-09-24.md`
(gitignored) and its prompt is committed beside this report. Result: 0 P1, 0 P2, 1 P3.

**Phase 3d assessment:**

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | Verified: `binding-gate.runsettings:10-15` maps only Inconclusive and zero-match; `[Ignore]` stays Skipped. Same as R1; fixed with its suggested sense, in the catalog and the CHANGELOG |

**Known suspects:** 5 disputed (2, 5, 7, 8, 10), 1 confirmed as a coverage boundary (9, not a
defect), 4 unverified as historical executor behaviour (1, 3, 4, 6). I agree with each: suspects 1,
3, 4 and 6 concern the first executor's runs, and this round's evidence (the gate at 368/0/0, the
resolver untouched) does not contradict them.

- **Confirmed bugs:** R1 (above).
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** R2 to R8. It checked F13's status against the report rather than #652
  (R6), did not list the findings the revert lapsed (R3, R4), and did not look at test-helper reuse
  (R8).

**Phase 3e, root cause for the Codex finding:**

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Catalog says the strict settings fail every skip | Other: doc overclaim | Written from the gate's own skip (`Assert.Inconclusive`); the first round's `[Ignore]` FOLLOW-UP on the same report was not re-read | Fixed; the first round's lesson "a change to how a gate behaves updates every doc" and its quantifier check cover it, so no new lesson |

## AGENTS.md lessons (pending)

Not edited here (Phase 3h is consolidated after all branches merge). Proposed lines:
- **What Codex does well:** separating MSTest outcomes precisely (Inconclusive against `[Ignore]`)
  with vendor documentation, and walking every banner outcome of a restored hook.
- **Bugs Codex typically misses:** record drift against the decision record itself (it checked a
  status against the report, not the issue that holds the decision); findings silently lapsed by a
  whole-file revert; duplicated test helpers where a shared one exists.
- **False positives:** no new pattern.

## Review files

- RCA: `docs/reviews/rca-binding-gate-no-silent-skips-decisions-2026-09-24.md`
- Lessons: `docs/reviews/lessons/build-tooling-workflow.md` (revert lapses), `docs/reviews/lessons/testing-qa.md`
  (`RepoPaths`)
- REVIEW-LOG: Review 133 (number provisional)
- Codex prompt: `docs/reviews/codex-adversarial-008-binding-gate-no-silent-skips-decisions-2026-09-24.prompt.md`

VERDICT: READY FOR COMMIT
