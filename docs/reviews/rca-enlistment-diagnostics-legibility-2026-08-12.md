# RCA: enlistment diagnostics legibility pass (2026-08-12)

**Scope.** A live field session produced zero errors, so the changeset was a legibility pass on
diagnostics: rewrite the duty result line, snapshot check inputs before the reward mutates them,
reword the `#424` battle-command line, correct a stale interface doc, and tier the `#371`
build-pair verdict. No gameplay behaviour was intended to change.

**Outcome.** `/deep-review` ran 5 agents. Two agent findings were disputed on evidence and not
acted on; two were accepted and fixed; one change was reverted outright. Six of the seven findings
below are failures in **how the change was reasoned about**, not in the shipped code, and five of
those are mine rather than the reviewers'. Suite after fixes: 6,454 passed, 0 failed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | HIGH | The `INFO` to `DEBUG` downgrade of `ServiceBattleService`'s join-refusal line rested on a frequency I never measured. The code comment I wrote claimed the line "lands after every single fight"; the field log open in the same session shows 3 occurrences across 5 joins in 39 minutes. `FileLogger` drops queued DEBUG on a hard native CTD, so the change traded away the only record of a refused join, and which trigger asked, for nothing. | Evidence | I had the log open and did not grep the line I was justifying a change to. This is `evidence-over-claims.md` §C trap 1 (writing the artifact before its evidence is in hand) applied to a **code comment** rather than a doc, which is a surface the rule's examples never name. | Reverted to INFO with the measured count in the comment. Rule extension below. |
| 2 | HIGH | `SkillCheckService.Passes` had **no direct test** and I refactored its body. `FieldDutyRuntimeTests` mocks `ISkillCheckService`, so no test in the suite ever executed the formula. A `Math.Min` for `Math.Max` slip would have passed all 6,444 tests. | Testing | A green suite was read as coverage. The caller's tests exist and pass, which looks like protection but is the opposite: mocking the collaborator guarantees its real body is never run. | Added `SkillCheckServiceTests` (12 tests) covering `EffectiveSkill`, `TrustBonus`, the assembled `Passes`, and the statics-match-the-check contract. Rule extension below. |
| 3 | MED | Duty-gate analysis was wrong twice before it was right. First pass read top-level `minRank`/`minTrust` keys that do not exist (they nest under `gates`), so every row looked Recruit-gated with no trust floor. Second pass repeated it. The published claim "11 of 13 unpassable" and "`hideout_strike` needs skill 26" were both wrong, and the latter reported an **already-fixed** bug as live. | Evidence | A Python dump used `.get(key)` with defaults, so absent keys silently became plausible values instead of failing. The real number is 8 of 13, verified independently by Agent 4. | Corrected in CHANGELOG and the feature doc. When dumping structured config, print the key set first and let absent keys raise rather than default. |
| 4 | MED | Engine semantics from a subagent's decompile went into a **shipped code comment** and the CHANGELOG before I read the decompile myself. The claim (`MapEventSide.LeaderParty` is set once in the constructor and reassigned only in `RemovePartyInternal`) held up when finally checked, but that was luck, not process. | Evidence | `evidence-over-claims.md` §A.4 requires spot-verifying a subagent's load-bearing claims, and I applied it to what I told the user while exempting what I wrote into the repo. Durable artifacts are the higher-stakes surface, not the lower. | Verified first-hand at `MapEventSide.cs:235` and `:303-308`. Rule extension below. |
| 5 | MED | I told the user to save the campaign because `privMB` 15.2 GB was nearing "#385's 20.3 GB", without checking which meter that figure used. `privMB` is `PROCESS_MEMORY_COUNTERS_EX.PrivateUsage`; the audit decomposes #385's death as 20.3 GB process commit = 15.65 GB private + 4.04 GB mapped + 0.65 GB image. Wrong axis, and the urgency was wrong too: #385 was commit exhaustion on a 16 GB machine, while this one has roughly 65 GB of commit headroom. | Evidence | A number was pulled from memory of an issue summary and compared against a live meter without opening either definition. | Corrected to the user. Before comparing a live telemetry field to a referenced threshold, open both and confirm they measure the same thing. |
| 6 | LOW | Rewiring `BuildReport` onto `Classify` left `IsMismatched` with no production caller. It survived review only because three tests still referenced it. | Simplicity | Caller-rewiring did not include a back-check for orphaned helpers. Its own doc comment even said "kept for existing tests", which reads as a decision rather than the debt it was. | Deleted the method and its three tests; the same cases are covered by the `Classify` tests. After rewiring a caller, grep the old helper for remaining production callers. |
| 7 | LOW | `DescribeVerdict`'s `default:` branch produced the MISMATCH text, so a future fourth `BuildPairVerdict` tier would silently render as the loudest wording. | Correctness | Writing the switch with two named cases and a catch-all felt exhaustive because the enum was exhaustive **that day**. | `Mismatched` is now an explicit case; `default` renders a visibly-unhandled string. Pinned by `DescribeVerdict_UnknownVerdict_DoesNotRenderAsMismatch`. |

## Root-cause pattern

Findings 1, 3, 4 and 5 are one failure wearing four hats: **a claim was written down before the
cheap check that would have settled it.** In every case the evidence was seconds away. The log was
already open (1). The JSON was already on disk (3). The decompile was already in the repo (4). The
audit doc was already indexed (5). Nothing was blocked on research; the check was simply skipped
because the claim felt obviously true.

The common trigger is that each claim was **incidental to the change rather than its subject**. The
duty log format was the task; the frequency assertion was a throwaway justification. The `#424`
reword was the task; the engine semantics were background. Attention followed the deliverable, and
the supporting claims rode along unverified. `evidence-over-claims.md` §C names the traps in terms
of the *primary* artifact, which is why none of its examples fired here.

## Why each agent missed these

- **Agent 1 (Standards)** raised two findings, both disputed on evidence. It called co-locating
  `BuildPairVerdict` with `BuildStampReport` a convention breach after citing three separate-file
  enums, missing roughly nine co-located ones including `AttachmentAssessment.cs` in the very
  feature under review. It then called the new public statics a DI smell without noticing that
  `RollRange` and `RankBonusPerLevel` are already public statics on the same class, consumed
  directly by `FieldDutyRuntime` and `InteractiveDutyPresenter` before this change. **Lesson: an
  agent asserting a convention must sample both sides of it.** A grep that finds only supporting
  cases has not measured a convention.
- **Agent 2 (Compatibility)** found nothing wrong, and that is the useful part. It confirmed the
  finding-4 claim by a method stronger than the one I eventually used: it decompiled the whole of
  `TaleWorlds.CampaignSystem.dll` as a project, grepped every file for `LeaderParty = `, found
  exactly the constructor and the `RemovePartyInternal` fallback, then established that the setter
  is `internal` and the assembly declares no `InternalsVisibleTo`, so no other assembly can reach
  it either. My own check had read one file and stopped. **Confirmation by exhaustion beats
  confirmation by sampling**, and the gap between the two is the difference between "I found no
  third site" and "no third site exists." It also noted namespace drift worth tracking separately:
  `MapEvent` and `MapEventSide` now live in `TaleWorlds.CampaignSystem.MapEvents`, not the bare
  `TaleWorlds.CampaignSystem` the prose implies. Harmless here (the code only ever says `var`), but
  it belongs in `docs/migration/v1.4.8-impact.md`.
- **Agent 3 (Efficiency)** caught finding 1, and it is the only agent that did, because its prompt
  carries a standing instruction to read `FileLogger.cs` before costing any logging change and to
  treat unverified cost claims as UNVERIFIED rather than HIGH. That instruction exists because of
  the 2026-08-03 battle-load incident. **It worked, and it worked against the orchestrator rather
  than against the code**, which is the case a review is least likely to catch.
- **Agent 4 (Completeness)** caught finding 2 and independently recomputed finding 3's corrected
  number. Its prompt asked explicitly whether the refactored method had a direct test, which is
  what surfaced the mocked-collaborator gap.
- **Agent 5 (Data Flow)** caught findings 6 and 7 and verified the arithmetic seam, the snapshot
  ordering, the refactor equivalence, and the "exactly five gated statements" doc claim. It
  correctly reported the missing `SkillCheckService` test file too, at LOW.

## Rule extensions

1. **`.claude/rules/evidence-over-claims.md` §C: name code comments and commit messages as covered
   artifacts.** The trap list currently says "doc / CHANGELOG / commit message". A justification
   written into a code comment is the same act and is harder to audit later, because it reads as
   settled fact to the next person in the file. Finding 1 and finding 4 both landed there.
2. **A frequency, volume, or cost claim used to justify a diagnostics change must carry its
   measurement inline.** "This is noisy" is not a reason; "3 lines across 5 joins in 39 minutes,
   from `taom_debug_2026-08-12_12-50-32.log`" is. If the number cannot be produced, the change does
   not go in. This is the orchestrator-facing companion to the rule Agent 3 already enforces.
3. **Before refactoring a method, confirm a test EXECUTES it.** A passing caller test that mocks the
   collaborator proves nothing about the collaborator. The check is: does any test construct the
   real type and call the method? If not, write that test first, then refactor. Finding 2 is the
   fourth TAOM instance of green-suite-as-false-confidence.
4. **A subagent claim that becomes a durable repo artifact needs first-hand verification, at the
   same bar as one relayed to the user.** §A.4 currently reads as being about user-facing summaries.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
