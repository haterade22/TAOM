# RCA: plan 008 round two, the maintainer decisions follow-up (2026-09-24)

## Top-line

The second review of plan 008 covered `2ca0805b..37306bca`, the commit that applies Mike's
decisions on #652: the skip banner removed, `TreatNoTestsAsError` added, the resolver order kept.
Six lenses (Standards, Engine compatibility, Efficiency, Completeness, Data flow, Design) and the
Tooling lens ran, and a Codex adversarial review ran beside them (0 P1, 0 P2, 1 P3). All three
decisions are implemented correctly. Eight findings were confirmed, all LOW or NIT, and none is in
executable gate logic. There were no false positives.

The pattern: **the records describe a mechanism more broadly than the mechanism.** "Fails a skipped
test" for a setting that maps only Inconclusive. "Fix the filter" for a message that has three
causes. "F11 lapses" for a revert that also undid F2, D1 and the F1 header fix. "Still open" for a
CI question that #652 records as moot. Each sentence was true of the case its writer had in mind
and false of the whole.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| R1 | LOW | `hooks-catalog.md:42` said `binding-gate.runsettings` "fails a skipped test" and `CHANGELOG.md:12-13` said "fails the skip". The file maps only Inconclusive to Failed (`MapInconclusiveToFailed`). MSTest 3.1.1 `UnitTestOutcomeHelper.ToTestOutcome` always returns Skipped for `Ignored`, so an `[Ignore]`d gate test still skips green. Found by Agents 1, 2, 4, 5 and 6, the Tooling lens and Codex P3. | Other: doc overclaim | The writer meant the gate's own skip (`Assert.Inconclusive` when the game is missing) and wrote the umbrella word. The first review's FOLLOW-UP (`[Ignore]` still skips green) was on the same page and not re-read. | Fixed: both now say `Assert.Inconclusive`, and the catalog says a gate run is green only at `Skipped: 0`. A repeat of the first round's lesson "a change to how a gate behaves updates every doc", whose quantifier check covers it; no new lesson. |
| R2 | LOW | `verify-bindings/SKILL.md:42` mapped `No test matches the given testcase filter` to one cause, a wrong filter, and said to fix it. vstest prints the same message for any filtered run that executed nothing, including a test DLL MSTest could not load (`Unable to load types from the test source`, present in the 3.1.1 adapter) and gate tests that lost their category. On the canonical command those are the likely causes. Found by Agents 1, 2 and 5. | Other: incomplete triage | The sentence was written from the zero-match probe, where the filter really was wrong (`NoSuchCategory008`). | Fixed: on the Step 1 command as written the message is a finding (DLL load warning, or a lost category); only a hand-written command gets its filter fixed. |
| R3 | LOW | The first RCA's resolution and the report's decisions row said only F11 lapses with the banner. The byte-exact revert also undid F2 (the all-skipped normal-verbosity case) and the convergence fix D1, and the F2 row still read "Fixed". Found by Agents 1 and 4 and the Tooling lens, which ran the restored hook on the F2 input and got no output. | Stale state across a revert | The resolution was written from the decision ("drop the banner") rather than from the list of findings the reverted file had carried. | Fixed: both records now name F2, D1 and the header. Lesson in build-tooling-workflow. |
| R4 | LOW | The revert restored `notify-test-results.sh:5` "summarize dotnet test results prominently", which contradicts the catalog row written in the same commit and `harness-facts.md:60`. The first RCA's F1 row still says the header was corrected. Found by Agents 1, 3, 4, 5 and 6. | Stale state across a revert | Same as R3: a whole-file revert took an unrelated comment fix with it. | Recorded in the first RCA's resolution. The header is left byte-identical to `7f02fc8d`, because plan 013 Step 4 anchors on that exact line and replaces it; plan 013's replacement is the place to correct it (NOT APPLIED here). Same lesson as R3. |
| R5 | LOW | The report said "the two earlier CHANGELOG headings" lack #652. Three plan 008 headings did (`CHANGELOG.md:23`, `:36`, `:57`). Found by Agents 1, 4, 5 and 6 and the Tooling lens. | Other: wrong count | Counted the two review commits and forgot the original `test(bindings)` entry, which sits under a different date. | Fixed: all three headings carry `(#652)`, as #652's own "still open" item asks. One-off. |
| R6 | LOW | Three records said F13 (the CI `if:`) was "not among the decisions; still open". #652's Decisions section, written before the commit, says it is moot because plan 010's hosted CI deletes the job, and a #652 comment records no port to `bannerlord-1.4.5`. Found by Agent 4. | Other: record drift | The records were written from the session's summary of Mike's answers, not re-read from the issue where the decisions were written down. | Fixed: the RCA, report and REVIEW-LOG now say moot (per #652) and record the port decision. Same lesson as R3. |
| R7 | NIT | `REVIEW-LOG.md` put the decisions paragraph before counts taken before the decisions (10243, 287), so the old counts read as current. Found by Agent 1 and the Tooling lens. | Other: record order | The paragraph was inserted where the decisions were discussed, not where the numbers were. | Fixed: the counts are labelled before and after the decisions. One-off. |
| R8 | LOW | `BindingGateRunSettingsTests` added a private cwd-walking `FindRepoRoot` while `TAOM.Tests/Infrastructure/RepoPaths.cs` already provides `RepoPath(params string[])`. Found by Agents 1, 3 and 5 and the Tooling lens. | Convention inconsistency (duplicated helper) | The test was modelled on its `Migration/` neighbours, which walk up from the working directory. Nobody grepped for a shared helper first. **Third occurrence:** `rca-field-commission-races-2026-09-17.md` F3 and `rca-race-fertility-2026-09-19.md` F2 fixed the same thing. | Fixed: the test uses `RepoPath("TAOM.Tests", "binding-gate.runsettings")`; 2 of 2 before and after, and deleting `TreatNoTestsAsError` still turns its row red. Lesson in testing-qa. A repeat offender needs a gate: a ratchet test on the count of private `TAOM.sln` walkers is recommended with the locator consolidation (TEST-L5-03). |

## Root-cause pattern

R1, R2, R3, R4 and R6 share one cause: each record was written from the writer's picture of the
change, not re-derived from the thing it describes. R1 and R2 describe a runner setting and a
runner message by the one case in view. R3, R4 and R6 describe a revert and a set of decisions by
their headline, without walking the findings the reverted file carried or re-reading the issue where
the decisions were recorded. The first round's lesson ("check a quantifier against the measured
counts") already names the check for R1; R3 to R6 need its counterpart for records.

## Why each agent missed these

Every finding was caught by at least one lens, so this section covers the builder and each lens's
gaps.

- **Builder (the decisions commit):** wrote the catalog row and CHANGELOG from intent, reverted a
  whole file without listing what else the file's history had fixed, and wrote the F13 status from
  memory rather than from #652.
- **Agent 1 (Standards):** caught R1, R3, R4, R5, R7 and R8. Missed R6 because it confirmed #652
  was open but did not read its Decisions section.
- **Agent 2 (Engine compatibility):** caught R1 and R2 by decompiling MSTest and vstest. The records
  (R3 to R7) are outside its lens.
- **Agent 3 (Efficiency):** caught R8 and flagged R4 for other lenses. No defects expected from it.
- **Agent 4 (Completeness):** caught R1, R3, R4, R5, R6. It was the only lens to read #652's body.
  Missed R2 and R8, which it listed as FOLLOW-UP rather than findings.
- **Agent 5 (Data flow):** caught R1, R2, R4, R5 and R8. It traced the decision record chain but
  took the report's F13 wording as the source rather than the issue.
- **Tooling lens:** caught R1, R3, R5, R7 and R8, and proved R3 by running the restored hook on the
  F2 input.
- **Agent 6 (Design):** caught R1, R4 and R5 as KEEP proposals. Its R4 proposal (restore the header)
  was not applied because of plan 013's anchor.
- **Codex:** caught R1 with the vendor docs. It checked the F13 wording against the report, not
  against #652, and did not audit the records against the reverted findings.

## Feedback to codify

- `docs/reviews/lessons/build-tooling-workflow.md`: "A whole-file revert reverts every fix in the
  file: re-record each finding it touched, from the record where it was decided" (R3, R4, R6).
- `docs/reviews/lessons/testing-qa.md`: "A test that reads a repo file uses `RepoPaths.RepoPath`"
  (R8, third occurrence).
- Recommended, not made on this branch: a default-suite ratchet on private repo-root walkers in
  `TAOM.Tests`, with the locator-consolidation plan.
