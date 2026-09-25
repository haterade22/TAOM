# RCA: plan 010 maintainer decisions, second review (2026-09-24)

## Top-line

The second `/deep-review` of plan 010 covered commit `c139bc50` (`2897fcca..c139bc50`, branch
`improve/010-ci-on-hosted-windows`), which applied Mike's decisions D44 (delete the empty
SandBoxCore reference), D45 (move `RequiresGame` from the Patch86 binding class to its one
game-bound method) and D46 (point the unit step at `refasm-game`: measured, reverted). Six lenses
ran (Standards, Engine compatibility, Efficiency, Completeness, Data flow, Design) beside a Codex
adversarial pass, which found **0 P1, 0 P2, 0 P3**.

All three decisions are implemented as decided. Every confirmed finding is in the text around
them, not in the code or the workflow: 5 confirmed, all LOW or INFO, all fixed on the branch. One
proposal (tag the Patch86 registration check `BindingVerification` so it runs on CI) changes
behaviour and waits for Mike.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | LOW | The CHANGELOG hunk replaced the heading `### fix(ci): v2.0.30 - convergence fixes for plan 010` with the new commit's heading instead of inserting above it, so commit `2897fcca`'s three bullets were credited to `c139bc50`. Found by Agents 1, 2, 4, 5 and 6. | Other: record integrity | The executor edited the top of the day's section in place; the report's "Other checks" line described it as "new entry" without reading back the diff, which shows a `-###` line. | Fixed: heading restored. Lesson in build-tooling-workflow: a CHANGELOG diff that removes a `###` line is a replaced entry unless the commit says otherwise. |
| C2 | LOW | The `tests.md` sentence added for D45 read as an order ("When the class's other tests run on the stubs, tag only the method"), while the decision, the CHANGELOG and the commit body say the rule allows it. Read literally it contradicts the rule's own class-tag default and the class-level tags on every class whose other tests pass on the stubs. The suite holds 102 class-level and 1 method-level `RequiresGame` tags (counted this run); how many of those classes mix stub-safe tests was not counted, though at least one does (`plans/_audit/2026-09-23-opus/verify-b-batch-02.md:83-87`: 288 of 1,598 stub results pass). The reflow also left one line at 119 columns. Found by Agents 1, 4 (F3), 2 and 5 (line width). | Other: rule text stronger than the decision | The sentence was written from the one example it describes, not checked against the corpus it governs. | Fixed: a class tag stays the default; a method tag is permitted when it returns checks worth running to CI. Line rewrapped under 100. Lesson in testing-qa. |
| C3 | LOW | D45 was not carried into the tagger's input. The executor's `scratch/010/manifest.txt:142` still holds `class|...|Patch86HideoutBossFightBindingTests|RequiresGame`, and the report's action item 1 tells the next executor to replay plan Step 6 on the merged tree, which re-runs `tag_categories.py` from that manifest and would put the class tag back with both CI steps still green. Found by Agent 4 (F2). | Stale state: generator input | The decision was applied to the generated output (the attribute) and verified there; the manifest is outside the repo, so no diff or lens read it. | Fixed in the repo record: action item 1 names the manifest row and the method row that replaces it. The scratch file itself is outside this worktree and is left to the orchestrator. Lesson in build-tooling-workflow. |
| C4 | LOW (INFO) | Decision 44's condition ("the 1.4.5 CI port re-checks against 1.4.8", `plans/_audit/2026-09-23-opus/DECISIONS.md:50`) appeared nowhere on the branch; the `GameReferences.targets` header said the SandBoxCore bin "holds no DLL" without a version. Found by Agents 5 (DF-3), 6, 4 and 1 (follow-up). | Other: decision condition dropped | The D44 row recorded the outcome and its proof, not the condition attached to the decision. | Fixed: the targets header says v1.5.3 and names the 1.4.8 re-check; action item 3 and the D44 row carry it. Same lesson as C3. |
| C5 | INFO | The report's replay description omitted that `replay.py` also pointed `NUGET_PACKAGES`, `TEMP` and `TMP` at scratch folders and that the RefAsm snapshots passed `-p:NuGetPackageRoot`. Found by Agent 5 (DF-4). | Other: evidence record incomplete | The description listed what makes the replay CI-like, not every departure from CI. | Fixed: one clause added. One-off; no lesson. |

**Not a defect, needs Mike.** `PatchClasses_AreRegisteredInAllThreePlaces` runs in no CI step:
the unit step drops `RequiresGame` and the gate selects only `BindingVerification`
(`.github/workflows/csharp.yml:67,83`). That was already true before D45: at `2897fcca` the class
tag (line 29) excluded it from the unit step and the method (line 217) had no
`BindingVerification`. Agents 2, 5 (DF-2) and 6 (proposal 2) propose tagging it
`BindingVerification`, as its Patch82, 84, 85 and 87 siblings are, which adds a CI check rather
than restoring one. Behaviour-changing (gate 338 to 339) and not run on the gate; it also changes
D45's literal outcome.

## Root-cause pattern

C1, C3 and C4 share one cause: **the executor verified the decisions' outcomes and did not read
back what the commit removed or left behind.** The CHANGELOG `-###` line, the manifest row and
the decision's condition each sat one step outside the thing being proved (the test run, the
snapshots). C2 is the mirror case: a rule sentence written from its one example, not read against
the other classes it governs.

## Why each agent missed these

Every confirmed finding was caught by at least one lens; this records which lens did not and why.
Codex caught none of them.

- **Agent 1 (Standards)** caught C1 and C2. It treated C4 as follow-up (outside the hunks), though
  the targets header line it named is in the diff. It does not read scratch inputs (C3).
- **Agent 2 (Engine compatibility)** caught C1 and C2's line width. It recorded the 1.4.8 check as
  UNVERIFIED rather than as a condition missing from the record (C4), and it does not read the
  tagger (C3).
- **Agent 3 (Efficiency)** found no defect, correctly: none of the five is a cost problem.
- **Agent 4 (Completeness)** caught C1, C2 and C3, and raised C4 as follow-up. It was the only lens
  to ask what re-applies the tags on merge.
- **Agent 5 (Data flow)** caught C1, C4 and C5, and C2's line width. It traced the tag into both CI
  filters but not back to the manifest that generated it (C3).
- **Agent 6 (Design)** caught C1 and C4. Its scope is the changed code's shape, so neither the
  rule's modality (C2) nor the tagger input (C3) was in view.
- **Codex** reviewed git objects only and said so; it read no scratch files and no CHANGELOG
  history, and judged the rule sentence as describing "this method-level exception" rather than
  reading it as a mandate.

## Feedback memories to codify

None beyond the three lessons appended to `docs/reviews/lessons/`:
- build-tooling-workflow: "A CHANGELOG diff that removes a `###` heading replaced an entry"
- build-tooling-workflow: "A decision applied to a tool's output is also applied to the tool's
  input, with its conditions"
- testing-qa: "A rule sentence keeps its decision's modality and is checked against the corpus it
  governs"
