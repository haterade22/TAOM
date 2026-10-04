# RCA: worker-thread formation patch review (plan 032, 2026-10-02)

## Top line

Plan 032 made `Patch30_FormationGetOrderPositionOfUnit`, which the engine calls for every unit of every
formation on its TWParallel workers, answer a formation without a TAOM layout with no lock, no adapter
allocation and no `FormationQuerySystem` read, and reordered `CreatureBanditAgents.Is` to rule out a
humanoid by its flags before the troop id. A six-lens `/deep-review` found no CRITICAL or HIGH and no
engine incompatibility: the snapshot was thread-safe and the predicate reorder kept every answer. It found
three MEDIUM and a LOW tail, every one confirmed against the code and the v1.5.3 engine and fixed in the
review commit. Codex was not run at that stage; it ran afterwards on the final tip (last section).
Report: `docs/reviews/deep-review-032-worker-thread-formation-patch-2026-10-02.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Patch30's catch logged the first throw once per process (a static latch that never re-arms, commented "once per session") and dropped every later throw uncounted, against D6's "aggregate or sample, never drop" | Stale state / lifecycle; logging completeness | The executor added the line beyond the plan to satisfy D6 rule 3 (a reason line on a fallback) and read rule 4 as satisfied by the sample alone. The lifetime word "session" was not checked against what resets the latch. This is the fifth recurrence of the static-latch lesson in `lessons/state-lifecycle-save.md` | `FormationLayoutService.NoteFallback`: first per mission in full, every throw counted, the count at mission end, re-armed there; five tests and an IL pin. New lesson in `lessons/harmony-il.md` |
| 2 | MEDIUM | The comments, the adapter's refresh contract, the registry and the commit trailer said the laid-out path no longer evaluates the query system on the worker; `UnitPitch` three lines later still does, through `Formation.Interval` and `UnitDiameter` | Assumed an API worked a certain way | The swap was reasoned about one member at a time: `IsCavalryFormationReadOnly` was verified to read a cached field, and nobody asked which other engine reads the same call makes, or that `QueryData` refreshes its whole sync group on any expired `.Value` read | Comments and docs corrected; lesson in `lessons/adapters-taleworlds-api.md` |
| 3 | MEDIUM | `ComputeUnitPlanePosition_FeatureDisabled_ReturnsNull` passed for the wrong reason: the new lock-free check returned before the `IsEnabled` gate it exists to guard, so deleting that gate kept every test green | Test passing for the wrong reason | The plan added an early return ahead of existing gates and re-ran the suite; a green gate test was read as proof the gate still worked. Nothing asks whether each guard test still reaches its guard | The test sets a layout first, proven RED against a copy without the gate; lesson in `lessons/testing-qa.md` |
| 4 | LOW | The snapshot keyed engine `Formation`s with the default comparer, so every worker lookup called `Formation.GetHashCode`, which dereferences the Team | Convention inconsistency (a repeat) | `lessons/adapters-taleworlds-api.md` (2026-09-26) says to key engine objects by reference; the snapshot used the default comparer like the older `_layoutByFormation` beside it, which predates the lesson and is touched only under the lock. Latent: no caller passes a team-less formation | One `ConcurrentDictionary` keyed by `ReferenceIdentity` (Lens 6's P1), RED-first test with a key whose `GetHashCode` throws. Recurrence, no new lesson |
| 5 | LOW | Stale or wrong text in files the change touched: `FormationAdapter.cs:139-140` ("an adapter allocation per call"), the feature doc's diagram, boundary paragraph, test bullet and adapter note, `OrderPositionLock` for `MovementOrderPositionLock`, Patch30's "every AI formation", `creature-bandits.md`'s tests and Performance bullet | Stale claim after a change | The plan's stale-claim sweep missed these lines (Lens 2): they describe the behaviour that changed (an adapter per call, the lock on every read) in words the sweep did not search for; `creature-bandits.md` was not among the files the change edited | Reworded; recurrence of the docs-sweep lesson in `lessons/build-tooling-workflow.md`, no new entry |
| 6 | LOW | Tests missing for the null key, the `FormationKey` identity the lookup depends on, the locked re-read after a stale lookup, and a strict "one key read and nothing else" on the no-layout path; two engine-free creature tests sat in a `RequiresGame` class that hosted CI skips | Missing test | The plan's test list covered the behaviour it changed, not the contracts the new fast path newly relied on | Added; the two engine-free tests moved to the untagged `CreatureBanditAgentsTests` and passed in the reference-assembly unit step |
| 7 | LOW | No feature-doc Changelog entry for the design | Process | `TEMPLATE.md` asks for one per change; the plan listed the doc sections to edit and the Changelog was not among them | Added |
| 8 | LOW | No GitHub issue | Process | Held by run decision D4 | Filed afterwards as #714 |

## Root-cause pattern

Findings 2, 3 and 6 share one cause: a new fast path in front of existing logic changes what the old
code and tests depend on, and the review of the change looked at the new lines only. The cached cavalry
read was checked in isolation (finding 2), the early return was checked for its own answer but not for
the gates it now shadows (finding 3), and the identity the lookup newly depends on was left implicit
(finding 6). The check that would have caught all three is the same: for every early return or swapped
read added to a hot path, list what used to run after that point, and confirm each guard still has a test
that reaches it and each engine read the call still makes is accounted for.

Finding 1 is a lifecycle recurrence: a process-static latch described as "session". The lesson existed,
in the state-lifecycle file; the code was written as logging, so that file was not read.

## Why each agent missed these

The lenses here reviewed the executor's work, so "missed" refers to the plan, its review and the executor.

- **Plan review (`plans/_audit/2026-10-02-perf/plan-review-032.md`):** checked the snapshot's writer paths
  (its B2 added the stored-Vanilla case) and the `IsCavalryFormationReadOnly` grep, but never mentions
  `UnitPitch`, `Interval` or the gate tests the new early return shadows.
- **Executor:** wrote RED tests for the new behaviour and kept the old gate tests green, which at
  `a00363cc` included one that no longer reached its gate. It added the D6 line beyond the plan and chose
  the sample half of rule 4.
- **Lenses:** all five non-design lenses found finding 1; Lenses 2, 3 and 4 found finding 2 independently
  by walking `UnitPitch` into `Formation.Interval`; only Lens 4 (Completeness) found finding 3, by asking
  which line each gate test actually reaches. Lens 6 found finding 4 indirectly (its P1 removes the
  `GetHashCode` call). No lens missed a confirmed finding that another did not catch.

## Feedback memories to codify

None beyond the lessons: each pattern is now a `### rule` in its category file, which the next
review of that subsystem reads (three from the review, two from the Codex round below).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `a00363cc..1fb072c5` | C1: three test lines (a class summary, an assertion message, a section header) still described the `_laidOut` snapshot that D1 replaced with one `ConcurrentDictionary` | `c46ef0c1` | D1's sweep covered production code and docs, not the test files it edited | A design change greps its removed concept across tests as well (`git grep -i snapshot`) |
| 2 | `1fb072c5..c46ef0c1` | none (clean) | | | |

This section and the REVIEW-LOG entry's convergence line were written by the orchestrator after round 2:
the review workflow's fix pass records a round only in the report.

## Codex adversarial review (2026-10-03)

Codex read `80176e2a..e6343e53`, the tip after both convergence rounds, and modified nothing. It reported four
numbered findings: a P2 and a P3 on the branch, a P2 it called inherited, and a P3 about what the tests prove.
Each was re-read against the worktree and the v1.5.3 engine before it was classified.

| # | Sev | Finding | Verdict | Outcome |
|---|---|---|---|---|
| 1 | P2 | The cached cavalry gate can return a TAOM slot after its own pitch read refreshes the flag to cavalry, and refuse one the base would give | CONFIRMED | Gate back on the evaluating read; two RED-first tests; the now unused `RepresentativeIsCavalryReadOnly` deleted |
| 2 | P3 | The feature doc names `taom_debug.log`, a file the logger does not create | CONFIRMED | The doc names `Logs/taom_debug_<timestamp>.log` |
| 3 | P2, inherited | Patch30's static `_service` can outlive a module reload | REJECTED | None (below) |
| 4 | P3 | The new tests prove narrower properties than the docs say | CONFIRMED as a claim problem | Claims narrowed; the behaviour test it asks for is finding 1's pair |

**Finding 1 (CONFIRMED).** `FormationQuerySystem.Expire()` clears the lifetimes of `_cavalryUnitRatio` and
`_isCavalryFormation` without evaluating either (`FormationQuerySystem.cs:688-729`), and
`Formation.OnMassUnitTransferEnd` (`Formation.cs:1945-1949`), `TransferUnits` (`:2150-2151`) and `Split` (`:2122`)
call it. `QueryData<T>.Value` evaluates the whole sync group when its member has expired, and
`GetCachedValueUnlessTooOld` returns the field (`QueryData`1.cs:18-37, 73-76`). After a transfer the base's gate
therefore evaluated and decided on the new composition, while the cached gate decided on the old one, and
`UnitPitch` (`Formation.Interval` and `UnitDiameter` read `CavalryUnitRatio`, `Formation.cs:220, 516-538`) then
refreshed the flag with the call already on its way to a position. Both directions differ from the base: a stale
false gives a cavalry-dominant formation a TAOM position for that call, a stale true withholds one from a
formation that has stopped being cavalry. A re-check after the pitch read would have fixed only the first, because
a stale true returns before the pitch.

The fix puts the gate back on `RepresentativeIsCavalry`, as before plan 032, and deletes the adapter member that
nothing else read. The review's open question (ACTION ITEM 2: keep the cached read or revert) is settled by pricing
the saving: while the group is fresh the evaluating read is a clock check (`Mission.CurrentTime` is a field read,
`Mission.cs:1194`), and `UnitPitch` refreshes the same group anyway. Two tests on a fake that follows `QueryData`'s
refresh rule, `ComputeUnitPlanePosition_FlagExpiredAndNowCavalry_ReturnsNullOnTheSameCall` and
`ComputeUnitPlanePosition_FlagExpiredAndNoLongerCavalry_PositionsTheUnitOnTheSameCall`, failed against `e6343e53`
(`Failed: 2, Passed: 60, Total: 62` in the service test class), passed with the gate restored, and failed again
with the gate deleted. A third mutation, the adapter's `RepresentativeIsCavalry` reading the cached accessor,
fails the existing IL pin `RepresentativeIsCavalry_StillReadsTheEvaluatingQuery`.

Category: behaviour change presented as an optimisation. Why missed: the plan promised unchanged answers
(`plans/032-worker-thread-formation-patch.md:46-47`) and, in its maintenance notes, accepted a stale flag
(`:1322-1326`); the review corrected the claim but kept the swap and left the conflict to a maintainer decision, and
the tests pinned which accessor the service read, not what it decided after an expiry. Preventive action: the
transition tests and the lesson in `lessons/adapters-taleworlds-api.md`.

**Finding 2 (CONFIRMED).** `FileLogger` opens `Logs/taom_debug_<yyyy-MM-dd_HH-mm-ss>.log` per launch and prunes by
the pattern `taom_debug_*.log` (`FileLogger.cs:26-27,45-46`); the new log section said `taom_debug.log`. Category:
wrong claim. Why missed: the name was written from memory (older docs also say `taom_debug.log` loosely) and
nothing checks a log file name in prose. Preventive action: corrected; a log destination is named from
`FileLogger`, not from another doc.

**Finding 3 (REJECTED).** The premise is an in-process module reload, and the engine has none. The only caller of
`OnSubModuleUnloaded` is `Module.FinalizeSubModulesBases` (`Module.cs:242-248`), reached only through
`FinalizeModule` (`:1296-1315`) and `FinalizeCurrentModule` (`:1317-1321`, which leaves `CurrentModule` null) from
the native callback `CoreManaged.Finalize` (`CoreManaged.cs:113-118`), the shutdown path that
`.claude/rules/harmony-patches.md` records. A search of the whole-assembly v1.5.3 and v1.5.2 decompiles finds no
other caller. Decision 22 (Mike, 2026-09-24, `plans/_audit/2026-09-23-opus/DECISIONS.md`) records that nothing
reloads TAOM in a process, and `.claude/rules/harmony-patches.md` says a new patch needs no `ResetForUnload()`;
the review's own FOLLOW-UP bullet for this static is closed the same way. The fix would also need a line in
`Main/SubModule.cs`, which this pass may not edit. Should decision 22 ever reverse, the edit is
`public static void ResetForUnload() => _service = null;` in `Patch30_FormationGetOrderPositionOfUnit` plus a call
to it after the last `ResetForUnload()` call in `SubModule.OnSubModuleUnloaded` (`ResetForUnloadSweepTests`
requires that call).

**Finding 4 (CONFIRMED as a claim problem, not a coverage defect).** The position test builds its expectation with
`LayoutPositioner.BuildInitialAssignment` and `UnitPitch`, so it is a composition check on one fake geometry, not an
independent oracle; the IL tests find a call anywhere in the prefix, so they show neither its order nor that the
catch is where `NoteFallback` sits. The feature doc's test bullets, its thread-safety bullet (which called the pitch
read "pure math" although it reaches the engine's query system), the test comments and the IL class summary now say
what the tests assert. The behaviour-level cavalry test the finding asks for is finding 1's pair. Category: claim
stronger than its proof. Why missed: the docs were written from the tests' intent. Preventive action: lesson in
`lessons/testing-qa.md`.

Codex's suspect table adds no defect beyond these: the evaluating pitch read on the worker and the slot cache built
on whichever thread misses first are already in the review's findings and FOLLOW-UP list. Fresh review of this
round's diff: owed.
