# Deep review: plan 010 maintainer decisions (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 010, C# on hosted Windows runners, maintainer decisions D44 to D46
         (commit c139bc50, diff 2897fcca..c139bc50, branch improve/010-ci-on-hosted-windows)
Date: 2026-09-24

Scope:   MSBuild (GameReferences.targets, Main/TAOM.csproj), one test attribute move
         (Patch86HideoutBossFightBindingTests.cs), a path-scoped rule (.claude/rules/tests.md),
         CHANGELOG and the plan 010 review report. No runtime C#, no ModuleData, no workflow.
Waves:   one wave: Agents 1 to 6. Agent 7 (XML) and Tooling not launched.

STANDARDS:     FAIL, 2 violations (2 LOW, text only)
COMPATIBILITY: PASS, 0 incompatible, 2 unverified (1.4.8 SandBoxCore bin; hosted run)
EFFICIENCY:    PASS, 0 issues
COMPLETENESS:  INCOMPLETE at review: CHANGELOG heading (C1), tagger manifest (C3); both fixed
DATA FLOW:     PASS after fixes: 15 flows traced, 1 gap (C1), 3 inconsistencies (DF-2 needs
               Mike, C4, C5)
DESIGN:        2 KEEP proposals (2 apply-scoped: 1 applied as defect C1, 1 behaviour-changing,
               needs Mike)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Decisions: verified as implemented

Each lens checked the three decisions against `plans/_audit/2026-09-23-opus/DECISIONS.md:50-52`.
The review lead re-read the diff, `CHANGELOG.md`, `.claude/rules/tests.md`, the scratch manifest
and `replay.py` before classifying.

- **D44 (delete the SandBoxCore reference): complete.** The `Main/TAOM.csproj` Reference and both
  `TaomSandBoxCoreModuleBin` properties are gone and nothing else names the property. The installed
  v1.5.3 `Modules\SandBoxCore\bin` holds no DLL; the before and after reference snapshots are
  identical (6 of 6). Its attached condition (re-check on 1.4.8 for the port) was missing from the
  record: C4.
- **D45 (method-level `RequiresGame` on Patch86): correct.** The one method that fails on stubs
  (`FileNotFoundException: 'SandBox'` from its `[HarmonyPatch(typeof(HideoutAmbushMissionController))]`,
  `scratch/010/6a.log:181-190`) carries the tag; the two IL checks on TAOM's own prefixes pass in
  the unit replay (`total=8220 executed=8196 failed=0`). The rule sentence and the tagger manifest
  did not follow it exactly: C2, C3.
- **D46 (unit step on `refasm-game`): measured and reverted.** `csharp.yml` is not in the diff;
  `d46-unit.log` shows 8,210 passed and 10 failed on stub constructors.

## DETAILS

### Agent 1 (Standards)
Two LOW: the CHANGELOG hunk overwrote the convergence entry's heading (C1); the new `tests.md`
sentence orders a method tag the decision only allows, and line 106 ran to 119 columns (C2).
Checks 1 to 10 not engaged beyond one attribute move; commit subject, body and dashes clean.
Follow-ups: the targets header's "holds no DLL" is unqualified for 1.4.8 (C4, taken as in-diff);
the untracked Codex prompt file in `docs/reviews/`; two-part test names in Patch86 (pre-existing).

### Agent 2 (Engine compatibility)
PASS, 8 verified, 0 incompatible, 2 unverified. Every engine claim holds against the installed
v1.5.3 DLLs and the BUTR `1.5.3.122374-beta` stubs: the stub constructors that failed D46
(`Equipment..ctor(EquipmentType)`, `BasicCultureObject..ctor()`), the Patch86 targets and the
engine names the two moved IL checks need. UNVERIFIED: 1.4.8's SandBoxCore bin (no 1.4.x install
on this desktop) and the hosted run. Also found C1 and C2's line width; suggested the
`BindingVerification` tag (NEEDS MIKE 1).

### Agent 3 (Efficiency)
No performance issue. No runtime code changed; D44 removes one empty directory glob in install
mode; the two moved IL checks take under 2 ms together; unit-step time stays within the replay
spread.

### Agent 4 (Completeness)
Tests pass, feature doc N/A, #421 open and commented at `c139bc50`. INCOMPLETE on C1 (CHANGELOG),
C3 (the manifest would restore the class tag on the documented merge replay) and C2 (rule
modality). Follow-ups: three records still said the decisions were pending (REVIEW-LOG Review 133,
the first RCA, the first report's NOT APPLIED list); the D44 row dropped the 1.4.8 caveat (C4);
a lesson owed from D46 (see AGENTS.md lessons below).

### Agent 5 (Data flow)
15 flows traced. DF-1 (C1) gap; DF-2 the registration check runs in no CI step (NEEDS MIKE 1);
DF-3 decision 44's condition missing (C4); DF-4 the replay description omits the scratch NuGet
and temp roots (C5). Merge check: D45's hunk does not overlap `improve/009` or `improve/018` in
the Patch86 file.

### Agent 6 (Design)
Proposal 1 (restore the CHANGELOG heading): KEEP, PRESERVING, applied as C1. Proposal 2 (tag the
registration check `BindingVerification`, drop the new rule sentence): KEEP, CHANGING, needs Mike.
Rejected: deleting `SandBoxCore` from `GameAssemblies.cs:41` (the loader already skips a missing
folder).

## Verification of every finding

| # | Source | Claim | Verdict | Evidence read by the review lead |
|---|---|---|---|---|
| C1 | A1, A2, A4 F1, A5 DF-1, A6 P1 | CHANGELOG heading overwritten | CONFIRMED LOW | `git diff 2897fcca..c139bc50 -- CHANGELOG.md` shows `-### fix(ci): v2.0.30 - convergence fixes for plan 010` |
| C2 | A1 #2, A4 F3, A2 and A5 nits | `tests.md` sentence is a mandate; line 106 at 119 columns | CONFIRMED LOW | Sentence read; a script count this run found 102 class-level and 1 method-level `RequiresGame` tags in 95 files; `awk` width 119 |
| C3 | A4 F2 | Tagger manifest would restore the class tag | CONFIRMED LOW | `scratch/010/manifest.txt:142` holds the class row; `tag_categories.py` inserts a missing class tag under `[TestClass]`; plan Appendix A has no Patch86 class row, so the risk is the scratch manifest only |
| C4 | A5 DF-3, A6, A4, A1 follow-up | D44's 1.4.8 condition missing | CONFIRMED LOW (INFO) | `DECISIONS.md:50` names it; no added line in `git diff 2897fcca..c139bc50` mentions 1.4.8 (0 matches) |
| C5 | A5 DF-4 | Replay description incomplete | CONFIRMED INFO | `replay.py:31-32` sets `NUGET_PACKAGES`, `TEMP`, `TMP`; `snap.sh:13` passes `-p:NuGetPackageRoot` |
| N1 | A2 suggestion, A5 DF-2, A6 P2 | Tag `PatchClasses_AreRegisteredInAllThreePlaces` `BindingVerification` | NEEDS MIKE | `csharp.yml:67,83` confirm it runs in neither step; behaviour-changing and not run on the gate |

No false positives among the lens findings.

## ACTION ITEMS

1. Mike: decide N1 (below).
2. Before re-running the tagger on the merged tree, replace `scratch/010/manifest.txt:142` with the
   method row now named in the first report's action item 1 (the scratch file is outside this
   worktree and was not edited here).
3. The first report's action items 1 to 3 stand; item 3 now carries decision 44's 1.4.8 check.

## IMPROVEMENTS (Step 4)

APPLIED:
- `CHANGELOG.md`: the convergence heading is back (Agent 6 proposal 1, defect C1). Proof: the
  day's `###` headings are, in order, this commit's, the decisions commit's, the convergence
  commit's, the review follow-ups' and the plan's. Docs only; the full suite is unchanged.
- `.claude/rules/tests.md:104-110`: permissive wording and rewrap (C2). Docs only.
- `GameReferences.targets:9-10`: the header says v1.5.3 and names the 1.4.8 re-check (C4). An XML
  comment; the full suite (which builds all three projects in install mode) is unchanged.
- `docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md`: action item 1 names the
  manifest row (C3), action item 3 and the D44 row carry decision 44's condition (C4), the replay
  description names the scratch roots (C5), and the NOT APPLIED list points at the decisions
  section (Agent 4 and 5 follow-up).

NOT APPLIED:
- `TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs:216` and
  `.claude/rules/tests.md:104-106`: `BindingVerification` instead of `RequiresGame` on the
  registration check (Agents 2, 5, 6). Behaviour-changing (hosted gate 338 to 339) and it changes
  D45's literal outcome: needs Mike.

FOLLOW-UP (pre-existing or outside the diff; no issue filed, each is a one-line edit for whoever
lands the branch or belongs to another plan):
- `TAOM.Tests/Migration/GameAssemblies.cs:13-14,75-76`: comments say Main references all five
  module folders; it now references neither SandBoxCore nor StoryMode (plan 008's file).
- `Patch71FillTests` and `TeamCombatantSelectorTests` pass the unit step only by going
  Inconclusive; tagging them `RequiresGame` is FOR-MIKE P1 already.
- `docs/reviews/rca-ci-on-hosted-windows-2026-09-24.md:22-23` still says three proposals are left
  for Mike; this report and the REVIEW-LOG entry record the outcome.
- `docs/reviews/codex-adversarial-010-ci-on-hosted-windows-decisions-2026-09-24.prompt.md` is
  untracked and not ignored; move it to `docs/reviews/raw/` or delete it (not staged here).
- `Patch86HideoutBossFightBindingTests.cs:201,209,217`: two-part test names and no AAA comments
  (pre-existing).

## CODEX REVIEW

Codex (`docs/reviews/raw/codex-adversarial-010-ci-on-hosted-windows-decisions-2026-09-24.md`,
complete, 109,097 tokens) reviewed `2897fcca..c139bc50` from git objects only, with no build or
test run, and found **P1: 0 | P2: 0 | P3: 0.** It disputed 7 of its 10 Known Suspects and left 3
UNVERIFIED (reference snapshots, restore, compile), each with a stated reason.
It quoted the vanilla targets from the v1.5.3 dump and cross-referenced every category, property
and path name against the workflow.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| (none) | no finding | n/a | yes | Codex's no-defect verdict holds for the code and the workflow: every lens agrees D44 to D46 are implemented as decided |
| KS3 | UNVERIFIED | verified | n/a | Lenses 2, 3, 5 and 6 compared the six raw snapshots with `cmp`: identical |
| KS9 | DISPUTED | agree | yes | The two moved checks assert positive call names and have no Inconclusive path (`Patch86HideoutBossFightBindingTests.cs:200-213,258-267`) |
| KS10 | DISPUTED | agree | yes | Right that no gate is unreachable by accident, and right that the registration check "remains excluded": it ran in neither CI step before D45 either (the class tag kept it out of the unit step, and it had no `BindingVerification`), so N1 would add a check, not restore one |

- **Confirmed bugs:** none from Codex.
- **False positives:** none.
- **Design questions:** N1 surfaced only implicitly, in Codex's selection table.
- **Things Codex missed:** C1 to C5, all in text rather than code. Codex read the CHANGELOG hunk
  but did not compare headings with the base; it read the `tests.md` sentence as describing the
  exception, not as a mandate; it read no scratch inputs (C3, C5) and no decision rows (C4).

### Phase 3e root cause (Codex misses)

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| C1 | CHANGELOG heading overwritten | Other: record integrity | Codex reviews the change's effect, not the record's integrity against the base | Lesson in build-tooling-workflow (`-###` grep) |
| C2 | Rule sentence stronger than the decision | Other: rule modality | Read the sentence against its example, not the corpus | Lesson in testing-qa |
| C3 | Tagger manifest keeps the class row | Stale state: generator input | Scratch files outside git were outside Codex's evidence rule | Lesson in build-tooling-workflow |
| C4 | Decision condition dropped | Other: record | Codex was not given `DECISIONS.md` | Same lesson as C3 |

### AGENTS.md lessons (pending, Phase 3h consolidated later)

- Bugs Codex typically misses: a replaced CHANGELOG heading (compare `###` lines with the base);
  a rule sentence whose modality exceeds the decision it records; hand edits to output that a
  scratch generator will overwrite.
- What Codex does well: a selection table per test group and CI step, which made N1 visible even
  though Codex did not call it out.
- From D46 (Agent 4): a measurement of which tests need the game counted tests that went
  Inconclusive through `GameAssemblies` as passing, so it never measured them; treat Inconclusive
  as unmeasured.

## Test evidence

Full suite in the worktree after the fixes, `TEMP`/`TMP` on E:,
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed!  - Failed:     2, Passed: 10256, Skipped:     2, Total: 10260`. The two failures are the
known live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; this branch is based before
`709649c3`, which rewrote both. No C# changed in this pass, so no new test was written; every
fix is text or an XML comment.

RCA: `docs/reviews/rca-ci-on-hosted-windows-decisions-2026-09-24.md`.

VERDICT: READY FOR COMMIT (N1 waits for Mike and does not block)

## Convergence

A convergence pass on `c139bc50..2628c66c` found no code defect and five defects in the evidence
the records state (2 LOW, 3 INFO). The review lead re-checked each against the code or git objects
before fixing; all five were confirmed, none was a false positive.

| # | Sev | Defect | Proof read by the review lead | Fix |
|---|---|---|---|---|
| V1 | LOW | The records said the old `tests.md` sentence contradicted all 102 class-level tags; it governed only classes whose other tests run on the stubs | Four class-tagged classes hold one test method each (`CuratedDropdownIndependenceTests`, `MapLoadDiagnosticsBehaviorTests`, `PlayerPossessionBehaviorPhaseGuardTests`, `SpecialResourcesBehaviorPhaseGuardTests`); the mixed classes were never counted | CHANGELOG, REVIEW-LOG, the RCA (C2, root-cause pattern) and the testing-qa lesson now name the classes it governed with no count; the C2 row above keeps 102 as a plain tag count |
| V2 | LOW | Three records said the registration check left CI because of D45 | At `2897fcca` the Patch86 class carried `RequiresGame` (line 29) and the method (line 217) had no `BindingVerification`; `csharp.yml:67,83` excluded it from both steps, and no other workflow runs `dotnet test` | KS10 re-graded to agree; the RCA and REVIEW-LOG say N1 adds a CI check rather than restoring one |
| V3 | INFO | `GameReferences.targets:8-9` cited | `git diff -U0 c139bc50..HEAD -- GameReferences.targets` gives `@@ -9 +9,2 @@` | Now `:9-10` |
| V4 | INFO | The RCA misquoted a lesson title | `lessons/build-tooling-workflow.md:2285` reads "input, with its conditions" | Quote matches the heading |
| V5 | INFO | The two known failures were blamed on `a39a9c86` | `a39a9c86` changes only `plans/`; `709649c3` renamed both tests; merge-base with trunk is `7f02fc8d` | Test evidence and REVIEW-LOG name `709649c3` |

Not fixable here: commit `2628c66c`'s body repeats V1's count, and only an amend could change it;
this section and the corrected records supersede it. Still open: `scratch/010/manifest.txt:142`
holds the Patch86 class row (action item 2 above; outside this worktree). Older than this diff and
not touched: `LESSONS-LEARNED.md:19,21` lesson counts are stale.

No C# changed, so no test was written. Full suite after the fixes, `TEMP`/`TMP` on E:,
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed!  - Failed:     2, Passed: 10256, Skipped:     2, Total: 10260`, the two known live-Armory
tests (`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`); the branch is based before
`709649c3`.
