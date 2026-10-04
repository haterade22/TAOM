# Deep review: plan 032, the worker-thread formation patch (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 032: Patch30 answers a formation without a TAOM layout lock-free, with no adapter
         allocation and no FormationQuerySystem read; CreatureBanditAgents.Is reads Character, then
         IsHuman, before the troop id
Branch:  perf/032-worker-thread-formation-patch, diff 80176e2a..a00363cc (15 files)
Date:    2026-10-02

Scope:   C# (2 services and hooks, 2 adapter files, 3 test files), 2 path-scoped rules, 3 docs.
         No XML, XSLT, scripts, hooks or CI.
Blast radius: graphify affected --depth 2 (refreshed at a00363cc): FormationLayoutService reaches only
         its test class; Patch30 has no callers; the interface has no unique node, so its consumers
         came from git grep (Patch30, MixedFormationsMissionBehavior, MixedFormationsIoC, no fake).
         CreatureBanditAgents.Is: 16 call sites, traced by Lens 5
Waves:   wave 1 lenses 1, 2, 3, 5; wave 2 lenses 4, 6. Codex not run.

STANDARDS:     FAIL, 6 violations (1 MEDIUM, 5 LOW); no CRITICAL, so no adversarial escalation
COMPATIBILITY: PASS, 22 verified, 0 incompatible, 1 unverified (a test-only CLR comment); 2 MEDIUM claim/log findings
EFFICIENCY:    FAIL, 3 issues (0 high, 2 medium, 1 low follow-up)
COMPLETENESS:  INCOMPLETE at a00363cc: no GitHub issue, a gate test passing for the wrong reason,
               stale docs, wrong refresh contract, reference-assembly run unproven
DATA FLOW:     PASS, 12 flows, 0 gaps, 4 inconsistencies (all LOW)
DESIGN:        4 KEEP proposals (4 apply, 0 follow-up)
XML:           NOT IN SCOPE (no ModuleData, XSLT or prefab in the diff)
TOOLING:       NOT IN SCOPE (no scripts or hooks in the diff)
```

## Details

Every finding was re-read against the worktree before it was classified, and the engine facts the
verdicts rest on were re-checked in the v1.5.3 `taom-src` cache this session:

- `FormationQuerySystem.IsCavalryFormationReadOnly => _isCavalryFormation.GetCachedValueUnlessTooOld()`
  (`FormationQuerySystem.cs:199`), and `GetCachedValueUnlessTooOld` is `return _cachedValue;`
  (``QueryData`1.cs``). Safe on any thread: no clock, no evaluation, a bool read.
- `QueryData<T>.Value` re-evaluates every member of its sync group when expired; `_isCavalryFormation`
  sits in the 11-member class-ratio group (`FormationQuerySystem.cs:446`) with `CavalryUnitRatio`
  (2.5 s lifetime, `:437`).
- `Formation.Interval` and `UnitDiameter` read `CalculateHasSignificantNumberOfMounted`, which reads
  `QuerySystem.CavalryUnitRatio + RangedCavalryUnitRatio` (`Formation.cs:220, 516-537`); that is the read
  `LayoutPositioner.UnitPitch` makes on the worker.
- `OnMassUnitTransferEnd` calls only `QuerySystem.Expire()` (`Formation.cs:1945-1949`); the per-unit path
  calls `ExpireAfterUnitAddRemove`, which evaluates (`FormationQuerySystem.cs:731-739`).
- `Formation.GetHashCode` is `Team.TeamIndex * 10 + FormationIndex` (`Formation.cs:2595-2598`); the order
  preview reaches the target through `agent.Formation`, a real formation (`OrderController.cs:1452`).
- `Formation.MovementOrderPositionLock` (`Formation.cs:188`); there is no `OrderPositionLock`.
- D6 (`plans/_audit/2026-10-02-perf/DECISIONS.md`, row D6, and the executor's ORCHESTRATOR NOTE rules 1 to 6):
  "aggregate or sample, never drop"; summaries and reason lines are INFO; every new line is listed in the
  feature doc and pinned literally.

### Agent 1: Standards

C# checks 1 to 10 pass (no banned construct, no new interface beyond the adapter member, no service
locator outside the patch, Patch30 at 100 lines). No CRITICAL, so Step 2b does not apply.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| S1 | MEDIUM | Patch30's fallback WARNING drops every failure after the first, with no count, and its static latch never re-arms (D6) | CONFIRMED, fixed (F-A below) |
| S2 | LOW | `IFormationLayoutService` was touched; ADR-002's amendment retires an unfaked single-implementer interface when its file is next touched | CONFIRMED as a rule match; NOT APPLIED here: the retirement touches `MixedFormationsMissionBehavior` and `MixedFormationsIoC`, which plan 031 also edits on its branch. FOLLOW-UP after both merge |
| S3 | LOW | The snapshot lookup depends on `FormationKey => _formation` with nothing pinning it | CONFIRMED, fixed: `Patch30ThreadSafetyIlTests.FormationKey_IsTheWrappedFormation` (untagged, IL) |
| S4 | LOW | The null-key branch of `FindLaidOutFormation` is untested | CONFIRMED, fixed: `FindLaidOutFormation_NullKey_ReturnsNull` |
| S5 | LOW | The rule amendments allow "an immutable snapshot swapped by reference" without requiring a volatile publication | CONFIRMED; superseded by D1 (the snapshot is gone). Both rule lines now name a concurrent collection written under the lock |
| S6 | LOW | Feature doc: no Changelog bullet, a "25 tests" count nothing computes, "rebuilt on each layout write" wrong for a cycle between two layouts | CONFIRMED, fixed (bullet added, count dropped, the sentence went with D1) |

### Agent 2: Engine compatibility

22 claims verified, 0 incompatible, 1 UNVERIFIED (the CLR half of a test comment about `FieldInfo.SetValue`
running Agent's type initializer; test-only, left as is).

| # | Sev | Finding | Verdict |
|---|---|---|---|
| C1 | MEDIUM | The laid-out path still evaluates the class-ratio sync group on the worker through `UnitPitch`; the comment at `FormationLayoutService.cs:93`, `IFormationAdapter.cs:47-50`, the registry and the `a00363cc` `Constraint:` trailer say otherwise | CONFIRMED (chain re-read above), fixed in code comments and docs (F-B). The trailer cannot change without rewriting history; the fix commit's body corrects it. Lens 2's simplicity call (revert to the evaluating read) is a design question: NEEDS MIKE |
| C2 | MEDIUM | Same as S1 (D6) | CONFIRMED, fixed (F-A) |
| C3 | LOW | `FormationAdapter.cs:139-140` still says "(an adapter allocation per call)" | CONFIRMED, fixed |
| C4 | LOW | `mixed-formations.md:153` names `Formation.OrderPositionLock` | CONFIRMED (`MovementOrderPositionLock`, `Formation.cs:188`), fixed there and in the service comment the diff edited |
| C5 | LOW | Patch30's "every AI formation" is wrong: layouts go to the player team's formations whether or not the AI commands them | CONFIRMED (`MixedFormationsMissionBehavior.TryGetTeamAdapters` reads `PlayerTeam` only), fixed |
| C6 | LOW | The rule lines should name the publication requirement | Same as S5, superseded by D1 |

### Agent 3: Efficiency

| # | Sev | Finding | Verdict |
|---|---|---|---|
| E1 | MEDIUM | Same as C1, with the cost: up to 4 evaluating reads per laid-out unit call, one 11-query refresh per 2.5 s per laid-out formation; the swap removed 1 of up to 5 | CONFIRMED; comment-only fix applied (F-B). The worker-safe pitch (read-only ratios plus a reflection read of `_overridenHasAnyMountedUnit`) is a FOLLOW-UP for Mike |
| E2 | MEDIUM | Same as S1 (D6), with the cost rule: count with `Interlocked` only on the exception path, never on the lock-free path | CONFIRMED, fixed (F-A); the counter is touched only inside the catch and at mission end |
| E3 | LOW | `ApplyDefaultsToFormations` runs `IsMixedFormationInternal`'s LINQ every second for every unqualified player formation; the doc says "No allocations in the hot path" | CONFIRMED; FOLLOW-UP (the doc line and the LINQ predate this diff) |

### Agent 4: Completeness

Tests and IoC OK; CHANGELOG untouched. Executor totals (base `Passed: 12345`, tip `Passed: 12394`) were
reproduced at the tip this session: `Failed: 1, Passed: 12394, Skipped: 2, Total: 12397`.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| M1 | MEDIUM | `ComputeUnitPlanePosition_FeatureDisabled_ReturnsNull` now returns null at the new lock-free check and never reaches the live `IsEnabled` gate | CONFIRMED. Fixed: the test sets a layout first. RED proven by deleting the `IsEnabled` line from the service: the test failed, then passed with the line restored |
| M2 | MEDIUM | Same as C1, plus the refresh contract is wrong both ways (a mass transfer only expires the flag; any thread's expired group read refreshes it) | CONFIRMED (`Formation.cs:1945-1949`), fixed in `IFormationAdapter.cs`, registry and feature doc |
| L1 | LOW | No GitHub issue for plan 032 | CONFIRMED; NEEDS MIKE (decision D4 holds issues; `/issue` is public) |
| L2 | LOW | Stale text: `FormationAdapter.cs:139-141`, `mixed-formations.md` diagram (:55), boundary paragraph (:72), test bullet (:134), adapter note (:137), the #595 "Never remove it" entry | CONFIRMED, fixed; the #595 entry stays as history and the new Changelog bullet says the lock still guards every write |
| L3 | LOW | `creature-bandits.md` lists no `CreatureBanditWieldGuardTests` and its Performance bullet keeps the "first managed field read" claim | CONFIRMED, fixed |
| L4 | LOW | Test gaps: copy-on-write pinned for one writer only; no test of the locked re-read after a stale lookup; `ReadsNoFormationState` could pin "one key read and nothing else"; the two engine-free creature tests sit in a `RequiresGame` class | CONFIRMED. Copy-on-write: moot after D1. The other three fixed: `ComputeUnitPlanePosition_LayoutClearedAfterTheLockFreeLookup_ReturnsNull`, the stricter `ReadsNoFormationState`, and `CreatureBanditAgentsTests` (untagged), which passed in the reference-assembly unit step |
| L5 | LOW | The fallback WARNING has no count and nothing tests that the catch writes it | CONFIRMED, fixed (F-A): `Prefix_ReportsAThrowToTheService` (IL) and three `NoteFallback` tests |
| U1 | UNVERIFIED | The plan's Step 9 reference-assembly run | Run this session: see Verification |

### Agent 5: Data flow

12 flows, 0 gaps. Trace 6 (`CreatureBanditAgents.Is`, 16 call sites) was re-derived: old
`id != null && !human && !rider && IsCreatureTroop(id)` and new `character != null && !human && !rider &&
IsCreatureTroop(id)` agree on every input because `IsCreatureTroop(null)` is false; a soldier costs 2 reads
instead of 4.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| D-1 | LOW | Docs drift (diagram, boundary paragraph, `FormationAdapter.cs:139-140`) | CONFIRMED, fixed (same as L2) |
| D-2 | LOW | Fallback log: uncounted, process-lifetime latch, catch not pinned | CONFIRMED, fixed (F-A) |
| D-3 | LOW | `FormationKey` identity newly load-bearing and unpinned | CONFIRMED, fixed (same as S3) |
| D-4 | LOW | Trace 4: with SmartCavalryAI off by default, a laid-out formation that turns cavalry-majority by mounting keeps its layout until the flag refreshes | CONFIRMED as stated; bounded tighter than the plan says, because the same call's `UnitPitch` refreshes the group within one 2.5 s lifetime. The plan's FOR-MIKE acceptance is still owed: NEEDS MIKE |

### Agent 6: Design and elegance

| # | Proposal | Behaviour | Verdict |
|---|---|---|---|
| P1 | One layout store: a `ConcurrentDictionary<object, (adapter, layout)>` keyed by `ReferenceIdentity`, written under `_lock`, read lock-free; drops the second dictionary, the four publication sites, the stored-Vanilla special case in `CycleLayouts` and the test-only `LaidOutSnapshot` | PRESERVING | APPLIED (D1) |
| P2 | `ComputeUnitPlanePosition` asks `FindLaidOutFormation` rather than re-deriving "has a layout" | PRESERVING | APPLIED with P1 |
| P3 | Fallback accounting in the service: first per mission in full, count at mission end | CHANGING (log output only) | Applied as the fix for the confirmed D6 defect S1, not as an improvement (F-A) |
| P4 | One agent-kind table for the three wield guards | PRESERVING (27 decisions, now 9 rows of 3 asserts) | APPLIED |

## Fixes

**F-A, D6 fallback accounting (S1, C2, E2, L5, D-2, P3).** `FormationLayoutService.NoteFallback(Exception)`
counts every Patch30 throw with `Interlocked.Increment` (exception path only) and logs the first of each
mission in full at WARNING; `OnMissionEnd` takes the count with `Interlocked.Exchange`, logs it at INFO and
re-arms. Patch30's catch calls `(_service ?? IoC.Resolve<IFormationLayoutService>())?.NoteFallback(ex)`
inside its own try, and its static `_fallbackLogged` and `FallbackLine` are gone. The two line formats are
pinned literally (`FallbackLine_NamesTheConsequenceAndCarriesTheWholeException`,
`FallbackSummaryLine_CountsTheMissionsFallbacks`), the sample and count by
`NoteFallback_FirstOfTheMissionInFull_RestCountedAtMissionEnd` and
`NoteFallback_AfterMissionEnd_LogsTheNextMissionsFirstInFull`, the quiet path by
`OnMissionEnd_NoFallback_WritesNoFallbackSummary`, and the presence of the prefix's call to `NoteFallback` by
the IL test `Prefix_ReportsAThrowToTheService` (it does not show that the call sits in the catch). RED: the
test project did not compile without `NoteFallback`. The feature doc's log table lists both lines with examples.

**F-B, the worker-thread claim (C1, E1, M2).** The service comment, the adapter's refresh contract, the
registry entry and the feature doc now say the cavalry gate reads the cached flag while `UnitPitch` still
evaluates the class-ratio group on the worker when it has expired, as vanilla's own
`HumanAIComponent.ParallelUpdateFormationMovement` does through `Formation.GetCurrentGlobalPositionOfUnit`.

**D1, one layout store (P1, P2, and the lesson in `lessons/adapters-taleworlds-api.md`, 2026-09-26).** The
snapshot keyed engine `Formation`s with the default comparer, so every lookup on a non-empty snapshot called
`Formation.GetHashCode`, which dereferences the Team. No caller passes a team-less formation today (the
preview passes `agent.Formation`), so this was latent, but it is the shape that lesson forbids. Both maps
now use `ReferenceIdentity`. RED first: `FindLaidOutFormation_ComparesKeysByReference` (a key whose
`GetHashCode` and `Equals` throw) failed with "GetHashCode read" against `a00363cc`. Characterisation, green
before and after: the four `EveryLayout` position rows, the `FindLaidOutFormation_*` writer tests,
`DoesNotWaitForTheLock` (three repeat runs), `ReadsNoFormationState`, the cycle and mission-end tests. The two
`LaidOutSnapshot_*` tests pinned the removed swap mechanism and its cross-formation atomicity, which no
consumer reads (each unit asks about its own formation); they were deleted.

## Verification

- Full suite before any edit, at `a00363cc`: `Failed!  - Failed:     1, Passed: 12394, Skipped:     2, Total: 12397`.
- Full suite after the fixes: `Failed!  - Failed:     1, Passed: 12383, Skipped:     2, Total: 12386`. The one
  failure in both is `EveryLanguage_DeclaresARowForEveryEnglishKey`, known at the base. The total falls by 11:
  P4 turns 27 grid results into 9 (-18), two snapshot tests go (-2), the Patch30 literal pin moves to the
  service tests (0), and 9 tests are new (+9).
- Reference-assembly unit step (`.github/workflows/csharp.yml`, `BANNERLORD_GAME_DIR` and
  `BANNERLORD_OVERRIDE_DIR` unset): `unit: total=10081 executed=10053 passed=10050 failed=3`. The three are the
  known base failures (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `CreatureBanditsWiringTests`
  `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`). All six
  `Patch30ThreadSafetyIlTests` and both `CreatureBanditAgentsTests` passed there.
- `python -B tools/lint_docs.py --dash-base 80176e2a`: exit 0, dead links 0, new dashes 0 (the dashes on the
  feature-doc and registry lines this review rewrote were replaced). `--drift-only`: exit 0. The two path rules
  were already over their size caps at the base; this review's net change to them is a few bytes.
- No hook, validator or CI step changed, so no gate sweep and no hook-suite run.

## ACTION ITEMS

1. (Mike) File the plan 032 issue when D4 releases issues, and link both branch commits.
2. (Mike) Keep `RepresentativeIsCavalryReadOnly`, or revert the gate to the evaluating read now that the
   pitch read is known to evaluate the same group (Lens 2 recommends revert, Lens 4 keep; this review kept
   it: it is cheap and skips the evaluation when the cached flag says cavalry).
3. (Mike) The plan's FOR-MIKE acceptance of the cavalry-flag freshness constraint (D-4).
4. (Orchestrator) Step 4's convergence pass over this review's diff: one `deep-reviewer` on standards and
   behaviour parity of D1, F-A and P4. A review lead cannot spawn it.

## IMPROVEMENTS (Step 4)

APPLIED:
- `Main/Features/MixedFormations/FormationLayoutService.cs`: P1 and P2, one `ConcurrentDictionary` of
  layouts keyed by reference; proven by `FindLaidOutFormation_ComparesKeysByReference` (RED first) and the
  characterisation tests listed under D1.
- `TAOM.Tests/Features/CreatureBandits/CreatureBanditWieldGuardTests.cs`: P4, one nine-row table asserting
  all three guards; green at the tip as the three separate grids were.

NOT APPLIED:
- S2, retire `IFormationLayoutService`: its consumers are in files plan 031 edits on its own branch.
- Lens 2's revert of the cached cavalry read: a design choice (ACTION ITEM 2).
- Lens 4's optional `SmartCavalryAIBindingTests` row for `IsCavalryFormationReadOnly`: the IL test
  `RepresentativeIsCavalryReadOnly_ReadsTheCachedQueryValue` already resolves the member against the
  installed DLL locally and the reference assemblies in CI.
- Lens 3's worker-safe pitch: changes engine logic TAOM would duplicate; FOLLOW-UP for Mike.

FOLLOW-UP (pre-existing, no issue filed: D4 holds issues for this run):
- The slot cache is built lazily on whichever thread misses first, usually a Patch30 worker
  (`EnsureAssignmentLocked` then `FormationAdapter.Units`, LINQ over `UnitsWithoutLooseDetachedOnes`, inside
  `_lock`); the plan calls it main-thread driven. Building it when the layout is set would remove the worker
  walk (Lenses 1, 3, 4, 5, 6).
- `IsMixedFormationInternal`'s LINQ runs every second for every unqualified player formation; the feature
  doc's "No allocations in the hot path" overstates (E3).
- `IFormationAdapter.cs:43-44` still cites v1.3.15; the `IsAligned` doc says it measures against
  `GetOrderPositionOfUnit` while the implementation reads the arrangement slot (Lenses 1, 2).
- The cavalry gate suspends a layout silently; under D6 rule 3 a once-per-formation reason line would show
  it (Lens 5).
- Patch30 caches `_service` in a static with no `ResetForUnload()` (`lessons/state-lifecycle-save.md`, the
  unload-sweep lesson); a reload in process keeps the disposed container's singleton.
- If a behaviour before Mixed Formations throws in `Mission.EndMissionInternal`'s loop, the service keeps the
  dead mission's formations until the next mission end (no position effect; Lens 2).

VERDICT: READY FOR COMMIT (every confirmed defect fixed; the convergence pass and three decisions are owed
above).

## CODEX REVIEW

Codex not run: the orchestrator dispatched no paid review for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| (none) | n/a | n/a | n/a | Codex not run |

**AGENTS.md lessons (pending)**, for the orchestrator's wrap-up (`.ai/review-reference.md` "Look harder here"):
- A `QueryData` "read-only" accessor makes only that read cheap: any `.Value` read of an expired member of
  the same sync group re-evaluates the whole group on the calling thread, so check every engine read the
  call makes, not only the one swapped.
- A diagnostic latch in a patch is process-scoped unless something resets it; ask what lifetime it claims
  ("session", "mission") and where it re-arms.

## Convergence round 1

Reviewed range `a00363cc..1fb072c5`. One finding.

| # | Severity | Finding | Outcome |
|---|---|---|---|
| C1 | LOW | Three test lines still described the volatile `_laidOut` snapshot that D1 replaced with a reference-keyed `ConcurrentDictionary`: the `Patch30ThreadSafetyIlTests` class summary, the failure message of `Prefix_AsksTheServiceForTheLaidOutFormation`, and a section header in `FormationLayoutServiceTests`. A failing run would have sent a maintainer looking for a mechanism that no longer exists. | **Fixed** in the commit that adds this section (`fix(mixed-formations): v2.0.32 - convergence fixes for plan 032`). Confirmed first: `git grep -n -i snapshot` over both MixedFormations trees printed exactly those three lines, and `git grep -n -E "_laidOut\|LaidOutSnapshot"` over `Main` and `TAOM.Tests` printed nothing. The three lines now name the lock-free `FindLaidOutFormation` lookup. Comment and message text only, so no test can fail first; the same `snapshot` grep now prints nothing. |

No finding was a false positive and none was left unfixed. No gate changed, so no differential sweep ran.

## Convergence round 2

The convergence reviewer read `1fb072c5..c46ef0c1` and found nothing. The orchestrator recorded both
rounds in the RCA ("Convergence rounds") and the plan's REVIEW-LOG entry, in the commit
`docs(mixed-formations): v2.0.32 - record plan 032's convergence rounds`.

## Codex adversarial review (2026-10-03)

Codex ran after the convergence rounds, on `80176e2a..e6343e53`; the "Codex not run" section above was true when
it was written. Its four findings and their outcomes are in the RCA's "Codex adversarial review" section: the
cached cavalry gate (CONFIRMED, fixed: the gate is back on the evaluating read), the log file name in the feature
doc (CONFIRMED, fixed), the static `_service` and a module reload (REJECTED: the engine reloads no module in a
process, decision 22), and test claims stronger than their proof (CONFIRMED, claims narrowed).

The action items above, after this round: item 1 is done (issue #714 is filed). Items 2 and 3 are closed by the
first finding's fix: the gate is back on the evaluating read, so no freshness acceptance is owed and Lens 2's
revert is applied. The FOLLOW-UP bullet about a `ResetForUnload()` for Patch30's static `_service` is closed by the
rejection of the third finding. The other FOLLOW-UP bullets stand.
