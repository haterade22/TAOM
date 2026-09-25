# Deep review: plan 007 maintainer decisions (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 007, PatchShield skips the callback shims: maintainer-decisions follow-up
Date: 2026-09-24

Scope:   C# (TAOM.Dependencies Foundation + SubModule), tests, docs; diff 31a31f16..0bf2409e on
         improve/007-patchshield-skip-callback-shims (worktree E:\repos\taom-improve\wt-007)
Waves:   one wave of six lenses (1 standards, 2 engine compatibility, 3 efficiency,
         4 completeness, 5 data flow, 6 design); Agent 7 (XML) and tooling NOT IN SCOPE;
         Codex adversarial second pass included (complete, "END OF CODEX REVIEW" present)

STANDARDS:     FAIL (LOW only): 3 violations (0 HIGH, 0 MED, 3 LOW); code checks 1 to 10 pass
COMPATIBILITY: PASS: 0 incompatible, 0 unverified TaleWorlds APIs (14 verified)
EFFICIENCY:    PASS: 0 issues
COMPLETENESS:  INCOMPLETE: stale comment, Foundation class count 18 vs 19, reword list
               missing dr3:261, untracked decisions prompt (all fixed below)
DATA FLOW:     PASS with notes: 1 gap (Trace 11, LOW), 1 inconsistency (Trace 8, LOW); 16 flows
DESIGN:        2 KEEP proposals (1 apply, 1 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Decisions verified

All five lenses and Codex agree, and the orchestrator re-read the code: the four maintainer
decisions are implemented as recorded.

| # | Decision | Verified against |
|---|---|---|
| 1 | Label `game start: campaign, custom battle or editor` | `Dependencies/SubModule.cs:281`; engine raise sites `Campaign.cs:1471`, `CustomGame.cs:56`, `EditorGame.cs:32` (v1.5.3, lenses 2, 3, 5 and Codex) |
| 2 | Seen/attached split | `ShieldCoverage.cs:15-35`; the four old `_shielded.Add` sites became three `RecordSkipped` and one `RecordAttached`, all writing `_seen`, which `HasSeen` (the dedupe) reads, so the set of methods that get a finalizer is unchanged |
| 3a | Whole `ManagedCallbacks` namespace stays excluded | `CHANGELOG.md` "Known limitation"; `PatchShieldPolicy.cs:104` unchanged |
| 3b | Stack preservation fixed on plan 006's branch | `git show 42624b95`: all four hand-back paths of `HandleAndSwallow` return `HandBack`, which calls `RethrowStackPreserver.PreserveForRethrow` |
| 4 | Issue #651 | `CHANGELOG.md:7`; lenses 3, 4 and 5 read it with `gh issue view 651` (OPEN) |

## Findings: verification and classification

Every finding was re-read against the worktree before it was acted on.

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| 1 | Lenses 1, 2, 4, 5, 6 | LOW | `PatchShieldPolicyTests.cs:264` comment says `alreadyShielded == 0`; the local is `alreadySeen` (`PatchShield.cs:217`). `git grep alreadyShielded -- '*.cs'` found only this comment besides SaveShield's own unrelated local | CONFIRMED | Fixed |
| 2 | Lens 1, lens 4 #9, Codex P3 | LOW | `ShieldCoverageTests` names break `MethodName_StateUnderTest_ExpectedBehavior` (`.claude/rules/tests.md:16`); one test had four segments and asserted a false case its name did not state; its message ("a failed attach is recorded nowhere, so the next pass retries it") claims behaviour of `PatchShield.Install`'s catch that the test cannot exercise | CONFIRMED | Fixed: renamed to `SeenCountAndAttachedCount_TwoSkippedOneAttached_ReturnThreeAndOne`, `HasSeen_SkippedOrAttachedMethod_ReturnsTrue`, `HasSeen_UnrecordedMethod_ReturnsFalse` (split out), `RecordAttachedAndRecordSkipped_SameMethodTwice_CountItOnce`; the message now says only "an unrecorded method has not been seen" |
| 3 | Lens 1 #3, lens 4 #4, lens 5 Trace 11 | LOW | The decisions record's reword list (texts that go stale when plan 006 lands) named two texts and missed `dr3-maintenance.md:261`, written on this branch; lens 5 also found `dr3-maintenance.md:304` and `lessons/harmony-il.md:572` (older) | CONFIRMED | Fixed: the Follow-up paragraph of `deep-review-007-patchshield-skip-callback-shims-2026-09-24.md` now lists all six (the sixth, `lessons/harmony-il.md:613`, added by the convergence pass below) |
| 4 | Lens 4 #2 | LOW | `Dependencies/Foundation/` went from 18 to 19 types; `dr3-maintenance.md:248` said "18 classes" and did not name `ShieldCoverage`; `feature-map.md:102` said "18-class" | CONFIRMED (`git ls-tree` at `0bf2409e`: 18 files, plus `CoopModuleListResult` inside `CoopModuleList.cs`) | Fixed: both say 19; dr3:248 names `ShieldCoverage` the way it names `RethrowStackPreserver`; the older em dash on each edited line became a semicolon or colon (`lint_docs.py --dash-base 0bf2409e`: 0 dashes, 0 dead links) |
| 5 | Lens 1 follow-up, lens 4 #5 | INFO | `docs/reviews/codex-adversarial-007-patchshield-skip-callback-shims-decisions-2026-09-24.prompt.md` untracked; round 1 committed its prompt | CONFIRMED | Committed with this report |
| 6 | Lens 4 #8 | NIT | `dr3-maintenance.md:287` ended "conflated them:", so the new-format sample read as the old conflated line | CONFIRMED | Fixed: the sentence ends with a period; the paragraph's first sentence already introduces the sample |
| 7 | Lens 4 #3 | LOW | `provenance-register.md` does not classify `ShieldCoverage.cs` | FALSE POSITIVE | `.claude/rules/provenance.md` requires a row for code derived from a third-party source. `ShieldCoverage` is TAOM's own split (the seen/attached distinction does not exist in the port), and the BetaDeps row's per-file glob (line 77) correctly leaves it out, as it leaves out `RethrowStackPreserver`. The "five files ... are TAOM originals" sentence (lines 346-348) is now two short; FOLLOW-UP |
| 8 | Lens 2 INFO | INFO | "editor" in the label never prints, because TAOM.Dependencies is not deployed to `Win64_Shipping_wEditor` | FALSE POSITIVE (as a defect) | The label is engine-true and maintainer-decided (decision 1); the missing Kit deploy predates this diff, FOLLOW-UP |
| 9 | Lens 2 INFO | INFO | `dr3-maintenance.md:270` "which fires at the first game start" | FALSE POSITIVE | The sentence is about when the marker is deleted, which is the first start (`IncompatibleModDetector.cs:106-110`); the hook does fire there. `dr3:287` states the every-start behaviour |
| 10 | Lens 5 Traces 14, 15 | INFO | `skipped` also counts failed attaches and null entries, which enter neither set; a skip-only pass grows `seen` without a pass line | FALSE POSITIVE (as a defect) | Both predate this diff (the old `total` behaved the same) and dr3:287 states the failed-attach case |
| 11 | Lens 4 #6 | INFO | The #651 comment says the change is ported to `bannerlord-1.4.5` after merge; the decisions table does not record it | NEEDS MIKE | Confirm the port, and that `ManagedCallbacks` is the namespace on v1.4.8, before it |
| 12 | Lens 4 #7 | INFO | #651 design item 3 still says the `(total: N)` prefix is byte-identical; decision 2 changed the line | NEEDS MIKE | Public issue edit; no repo reader depends on the old format (lenses 1, 4, 6 grepped) |
| 13 | Lens 4 follow-up | INFO | Plan 007's deferred follow-ups 1 to 4 have no issues of their own, only #651's body | NEEDS MIKE | Filing public issues is Mike's call |

Totals: 6 confirmed (4 LOW, 1 INFO, 1 NIT), 4 false positives, 3 NEEDS MIKE. No HIGH or MED
finding; nothing deferred.

## Codex review

Codex gpt-6-astra, second pass on `31a31f16..0bf2409e`, 158,323 tokens, raw output
`docs/reviews/raw/codex-adversarial-007-patchshield-skip-callback-shims-decisions-2026-09-24.md`
(gitignored). **0 P1, 0 P2, 1 P3.** It quoted the v1.5.3 raise sites (`MBGameManager.cs:110-115`,
`Campaign.cs:1459-1471`, `CustomGame`, `EditorGame.cs:30-32`), the three `ManagedCallbacks`
declarations and Harmony 2.4.2's `PatchProcessor.Patch`, tabled the six outcomes of the install
loop against the old set, and walked first boot, first and later game starts, mission, co-op,
dedicated server and process exit.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | `ShieldCoverageTests.cs:43` (at `0bf2409e`) asserted only that an untouched method is unseen, while its message claimed the failed-attach retry. Re-read: the retry holds by inspection (`PatchShield.cs:201-209` records nothing in the catch), but no test exercises it. Fixed with finding 2 above |
| KS1-KS8, KS10 | DISPUTE | n/a | Yes | Re-checked the load-bearing ones: `ExcludedTargetNamespacePrefixes` hits only `PatchShield.cs` and `PatchShieldPolicy.cs`; `_seen` holds exactly the old set's entries; both counters are consumed (`PatchShield.cs:213-221`, `:423`) |
| KS9 | PARTLY CONFIRM | n/a | Yes | Same as #1 |

- **Confirmed bugs:** none in behaviour; #1 is a test-message and naming defect, fixed.
- **False positives:** none.
- **Design questions:** none raised by Codex.
- **Things Codex missed:** findings 1, 3 and 4 above (a stale identifier in a test comment, a
  cross-branch reword list missing a line, a doc class count outside the diff). Codex checked the
  new log strings against dr3 and the formatter tests but not the comment above the call it
  updated, and it read no doc outside the diff's own files.

### Phase 3e root cause (Codex-found)

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| 1 | Test message claims a retry the test cannot exercise | Other: test claims more than it proves | The builder wrote the message from the production design (the catch records nothing) rather than from what the test body does | Renamed and split; the message now states only what the assertion shows. One-off, no lesson |

### AGENTS.md lessons (pending, for the consolidated Phase 3h)

- **Bugs Codex typically misses:** stale inventories outside the diff (a folder's class count in a
  maintenance doc and the feature map) and stale identifiers in comments next to a call the diff
  renamed; a "reword when branch X lands" list that misses a line containing the same claim.
- **What Codex does well:** a per-outcome table proving a bookkeeping refactor keeps the same
  dedupe set, and lifecycle scenarios with concrete counts through a static cache.

## Action items

1. (Done) Fix findings 1 to 6.
2. NEEDS MIKE: the three items in the list below.
3. Cross-branch, for the orchestrator: whichever of plans 006 and 007 merges second rewords the
   stack half of the six texts now listed in the plan 007 review record's Follow-up paragraph.

## NEEDS MIKE

- Confirm the `bannerlord-1.4.5` port promised in the #651 comment, after checking the
  `ManagedCallbacks` namespace on v1.4.8.
- Edit #651's body: design item 3's "byte-identical prefix" is superseded by decision 2.
- File issues for plan 007's deferred follow-ups 1 to 4, or accept that they live only in #651's
  body and drop off when it closes.

## Improvements (Step 4)

```
APPLIED:
- TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs:264: comment names
  `alreadySeen` (lens 6 P1, KEEP, APPLY, PRESERVING). Proof: compile, and
  FormatShieldPassSummary_NoAttaches_DoesNotDivideByZero green before and after.
- Dependencies/Foundation/PatchShield.cs:219-221: the FormatShieldPassSummary call passes named
  arguments, so reordering its parameters can no longer silently transpose two counts (lens 5
  Trace 2 INFO; changed line; PRESERVING). Proof: compile, and the named order matches the old
  positional order argument for argument. No test reaches `PatchShield.Install`; the three
  FormatShieldPassSummary tests call the formatter directly.
- TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs:23, 35, 46, 56: renames and the
  split (finding 2); test-only, 4 of 4 green.
NOT APPLIED:
- Dependencies/Foundation/PatchShield.cs:101-106 doc comment ("methods already shielded are
  skipped"): lens 6 P2 is scoped FOLLOW-UP (lines unchanged by this diff).
- No behaviour-changing proposal was made, so nothing waits on Mike here. Lens 3 made none.
FOLLOW-UP (pre-existing; no issue filed, because filing is public and Mike's call):
- PatchShield.cs:103 "Idempotent, methods already shielded are skipped" contradicts the seen
  vocabulary and carries an em dash (lens 1, lens 4, lens 6 P2).
- TAOM.Dependencies.csproj:30-31: the InternalsVisibleTo comment names only
  AssemblyRedirectListTests; ShieldCoverageTests depends on it too.
- provenance-register.md:346-348: "five files ... are TAOM originals" omits RethrowStackPreserver
  and ShieldCoverage.
- TAOM.Dependencies has no Win64_Shipping_wEditor deploy, so no shield runs in the Modding Kit;
  whether that matters is UNVERIFIED (lens 2).
- IncompatibleModDetector.cs:69 and :117 still say "main menu" (plan 007 deferred item 3).
- The "247" wording goes stale when plan 006 lands (006 allowlists 16 shims; cross-plan note X2).
- PatchShield.cs is 436 lines after this change (ADR-002 size note from lens 1).
- LESSONS-LEARNED.md:19 says build-tooling-workflow has 167 lessons; `grep -c '^### '` gives 173
  after this review's entry. Left for the merge consolidation, since every improve branch appends.
```

Convergence pass (Step 4.6): run by the orchestrator over `0bf2409e..900920dc`; results in the
Convergence section below.

## Verification

- Before any edit: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter
  "FullyQualifiedName~ShieldCoverageTests|FullyQualifiedName~PatchShieldPolicyTests"`:
  `Passed: 27, Failed: 0`.
- After the edits, same filter: `Passed: 28, Failed: 0` (24 + 4).
- Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10247, Skipped: 2, Total: 10251`. The two failures are the known live-Armory
  tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; this branch is based before
  `a39a9c86`.

RCA: [rca-patchshield-skip-callback-shims-decisions-2026-09-24.md](rca-patchshield-skip-callback-shims-decisions-2026-09-24.md).

## Convergence

Step 4.6 `deep-reviewer` pass over the review-fix commit, `0bf2409e..900920dc`. It found no
defect in code or tests: the named arguments at `PatchShield.cs:219-221` bind the same variables
in the same order as the old positional call, the `ShieldCoverageTests` split keeps all three
original assertions, and the Foundation count of 19 is right (18 files, 19 class declarations).
It found five defects in the review record, all text; each was re-read against the worktree and
all five are confirmed.

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| C1 | LOW | The reword list said "four texts on this branch" but named three; `docs/reviews/lessons/harmony-il.md:613` (written on this branch by `578ac7d6`) says PatchShield "preserved the stack on exactly those paths. Nothing on a shim does either now.", which goes stale when plan 006's `42624b95` routes every `HandleAndSwallow` hand-back through `HandBack` and `RethrowStackPreserver.PreserveForRethrow` (both re-read with `git show 42624b95`) | CONFIRMED | Added as the fourth branch text in the plan 007 review record's Follow-up; finding 3 and action item 3 above now say six; RCA finding 3 names it; the `build-tooling-workflow.md` lesson now says two lines were missed and gives a search that matches every inflection: `git grep -n -i -E "preserv[a-z]* (the|its) stack\|stack preservation\|fallback path"`, which hits all six (plus unrelated lines to read past) |
| C2 | LOW | The report said the convergence pass did not run and still issued READY FOR COMMIT, against `.claude/skills/deep-review/SKILL.md` Step 4 | CONFIRMED | That paragraph now points here; the verdict below is set from this pass |
| C3 | NIT | `grep -c '^### '` on `build-tooling-workflow.md` gives 173 at `900920dc`, not 174 (172 at `0bf2409e`) | CONFIRMED | Changed to 173 |
| C4 | NIT | Phase 3d cited `PatchShield.cs:421` for the SESSION SUMMARY; the named-argument call moved it to `:423` | CONFIRMED | Changed to `:423` |
| C5 | NIT | The APPLIED bullet said a swap of the parameters "no longer compiles silently", proven by the three formatter tests. A call-site transposition still compiles, and those tests call the formatter directly; no test reaches `PatchShield.Install` | CONFIRMED | Reworded: reordering the parameters can no longer silently transpose two counts; proof is the compile and the named order matching the old positional order |

No false positives. No code changed, so no test was written; the full suite was rerun on the
edited tree.

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  `Failed: 2, Passed: 10247, Skipped: 2, Total: 10251`, the same two live-Armory tests
  (`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`); the branch is based before
  `a39a9c86`.
- `python tools/lint_docs.py --dash-base 900920dc --summary`: `dead_links 0`, `ai_dashes 0`; the 7
  `context_budget` findings are in files this change does not touch.

```
VERDICT: READY FOR COMMIT
```
