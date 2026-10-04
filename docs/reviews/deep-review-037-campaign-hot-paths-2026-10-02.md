# Deep review: plan 037, campaign hot paths (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 037: campaign MCM settings read once (A), cheap filters before per-party work (B),
         marketplace id sets per culture and one roster walk (C), caravan distance reuse through a
         Patch59 transpiler (D)
Branch:  perf/037-campaign-hot-paths, diff ba3f2a57..f9e277ce (4 commits, one per stage)
Date:    2026-10-02

Scope:   C# (8 settings providers, 4 Stage B services and hooks, the town roster adapter and the
         marketplace maintenance service, 2 new CaravanTrade types and a transpiler), their tests,
         4 feature docs and the Patch59 registry section. No XML, XSLT, scripts or harness.
Blast radius: graphify affected (depth 2) on the types the follow-ups changed: CultureItemPool has
         one construction site (CultureItemPoolService.cs:115) and two readers (SelectItems,
         FilterForeignCultureItems); CultureMarketplaceMaintenanceService and CulturalFeatsService
         have no callers outside their feature and tests.
Waves:   lenses 1, 2, 5 and 3, 4, 6 (reports supplied to the review lead). Lens 7 (XML) and the
         tooling lens NOT IN SCOPE. Codex not run.

STANDARDS:     PASS: 0 HIGH, 0 MED; 6 LOW and 6 NIT (Stage D items moot after the drop)
COMPATIBILITY: PASS: 33 verified, 0 incompatible, 4 unverified; F1 (MED, latent) moot after the drop
EFFICIENCY:    PASS on A to C; Stage D fails the simplicity criterion (Reject), now dropped
COMPLETENESS:  INCOMPLETE at f9e277ce: GitHub issue not filed (DECISIONS D4), stale doc counts,
               Stage B docs untouched; the doc items are fixed here, the issue stays with the run
               (filed since as #719)
DATA FLOW:     PASS: 18 flows, 3 gaps and 6 inconsistencies, all LOW; the Stage D ones moot
DESIGN:        8 KEEP proposals (6 apply, 2 follow-up): Block 1 applied (Stage D dropped),
               Blocks 3, 4, 6 and 7 applied, Block 2 moot, Blocks 5 and 8 follow-up
XML:           NOT IN SCOPE (no ModuleData, XSLT or prefab in the diff)
TOOLING:       NOT IN SCOPE (no scripts, hooks or CI in the diff)
```

## Details

Every finding below was re-read against the worktree before it was classified. Load-bearing engine
claims were spot-checked: `MBObjectManager.GetObject<T>` for a sealed type returns through
`ObjectTypeRecord<T>.GetObject` (taom-src v1.5.3 `TaleWorlds.ObjectSystem.MBObjectManager.cs:573-585`),
and the 2026-07-04 caravan RCA row the orchestrator cited (`rca-caravan-trade-2026-07-04.md` row 2)
accepted the second distance read as a cache read on an infrequent path.

### Probe answers

1. **Behaviour parity.** Stages A to C hold parity for every reachable input class, as lenses 1 to 6
   each traced independently: the cached MCM reference is MCM's one registered instance, edited in
   place by override, reset and presets; a null instance is re-resolved on every read, so it never
   pins the fallbacks; the desertion gate keeps the gate order and the NaN pass (`!(rate <= 0f)`); the
   refuge early return skips only a walk whose ids the service would ignore; a `(0, 0)` mount count
   returns where the feat gate returned; the routed and pooled sets come from inputs written once per
   process. The one claimed divergence (efficiency item 4, a failed walk zeroing every item) is not
   reachable: see the FALSE POSITIVE row below.
2. **Stage D under the simplicity criterion: Reject, and dropped.** Win: one cache-backed
   `AiHelper` distance read skipped per positively scored town on a caravan's destination re-think
   (the base postfix asked only after its `__result > 0f` gate), unmeasured. Cost: about 240
   production and 360 test lines, a transpiler that must find exactly one call site in a private
   engine method, per-thread state keyed by reference with a stale-record case, a fallback path, four
   new log formats, and a reversal of a reviewed decision with no new measurement. Binding and
   threading were verified correct by lenses 2, 3, 5 and 6 (one call at IL_001b in v1.5.3; scoring
   reached only from `HourlyTickParty`, dispatched with `doParallel: false`), so this is a cost
   judgement, not a defect. Commit `b282aaf2` removes the stage; restoring it is a revert of that
   commit plus F1, S1, S3 and D3 below.
3. **Stage C caches.** No staleness: routing and pools are built once per process and hold only
   strings and TAOM domain objects; a town that changes culture reads another key. The pooled-id
   cache with its reference-equality rebuild is replaced by an index on the immutable pool itself
   (design Block 6), so nothing is left to invalidate. Memory: one id set per pool (25 pools, 8,006
   items in Mike's 2026-10-02 log), sharing the pool's strings, plus one routed set per culture that
   owns a town.
4. **D6 logging.** Stages A to C change one line: Stage C's count failure logs one ERROR line naming
   every item (pinned by `CountFailureLine_IsTheLiteralErrorLine`). Stage D's four formats and its D6
   gaps (unlatched catch path, no closing tally) left with the stage.
5. **Executor deviations.** The stale "117 tests across 10 classes" line is now 128 across 11
   (the filter run, quoted below); the five em dashes: three were Stage D doc lines (gone with the
   drop), the culture-marketplace one is fixed, the registry one was Stage D's; the kept class comment
   is fixed; the extra assertions only strengthen tests.

### Findings and classification

| # | Lens | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| D-1 | 1, 2, 3, 4, 5, 6 | MED | Stage D: tiny unmeasured win plus a transpiler, thread-static hand-off, fallback and four log formats (simplicity Reject) | CONFIRMED | Dropped in `b282aaf2` |
| S1, L3 | 1, 4 | LOW | Stage D log latches untested in the patch class; the transpiler catch path skips the latch | CONFIRMED, moot | Left with Stage D |
| S2 | 1 | NIT | `t_handoff` field name | moot | Left with Stage D |
| S3, D4a | 1, 5 | NIT | The catch path could return IL already swapped | CONFIRMED, moot | Left with Stage D |
| F1, D5, Block 2 | 2, 5, 6 | MED (latent) | The transpiler matched on name and declaring type, not on `target`, so a future overload would bind and emit unbalanced IL | CONFIRMED, moot | Left with Stage D; must be fixed (`Calls(target)`) if Stage D is ever restored |
| F2, D4 | 2, 5 | LOW | "There is no campaign-end hook" is wrong (`SubModule.OnGameEnd`); no closing tally | CONFIRMED, moot | Left with Stage D |
| F3, item 3, L2, S10, S11, D7 | 2, 3, 4, 1, 5 | LOW | Stage D docs overstated the saving and kept "4 postfixes" | CONFIRMED, moot | Docs restored to `ae7eff12` |
| F4, D3, item 2 | 2, 5, 3 | LOW | The hand-off record outlives a throwing call | CONFIRMED, moot | Left with Stage D |
| S4, B4, Block 4 | 1, 5, 6 | LOW | The Rohan penalty repeated the feat predicate `NeedsMountedCount` derives, so a second trigger feat would silently stop the penalty | CONFIRMED | Fixed: `ApplyRohanInfantryPenalty` gates on `NeedsMountedCount` (`CulturalFeatsService.cs:137-139`) |
| S5, L1, C4 | 1, 4, 5 | LOW | `culture-marketplace.md` test total stale (117/10), Filter count 10 (is 11), no changelog entry | CONFIRMED | Fixed: 128 across 11 from the filter run; Filter 11; Cache 10; 2026-10-02 changelog entry |
| C4 (:158) | 5 | LOW | The doc credited `CultureMarketplaceBehavior.EnsureGuaranteedStock`; the method is on the maintenance service | CONFIRMED | Fixed, with the em dash on the same line |
| S6 | 1 | LOW | Em dashes on five edited doc lines | CONFIRMED | One fixed; four went with the Stage D docs; `lint_docs --dash-base ba3f2a57` reports `ai_dashes: 0` |
| S7, C3, Block 7 | 1, 5, 6 | LOW | Comments still named the removed `GetItemCount` and a note that no longer exists | CONFIRMED | Fixed in `ITownRosterAdapter.cs:13-16`, `TownRosterAdapter.cs:66, 71-73, 113` |
| S8, A2 | 1, 5 | NIT | `CaravanTradeSettingsProvider` class comment said the fields read `TaomSettings.Instance?.X` | CONFIRMED | Fixed |
| S9 | 1 | NIT | `RoutedSets.Ids` and `PooledIdsFor` documented null without `?` | CONFIRMED | `Ids` is `HashSet<string>?`; `PooledIdsFor` deleted by Block 6 |
| S12 | 1 | NIT | The same six-line rationale comment in seven providers | NOT APPLIED | Each copy is where a reader of that provider learns why the accessor must read through, not snapshot; a pointer moves it a file away |
| L4 | 4 | LOW | No test fails if a Stage B gate is inverted | CONFIRMED, partly fixed | `CountMountedAndTotal_Needed_ReadsTheRoster` added (fails against an inverted helper); the refuge, desertion and speed-model call-site inversions need a live `MapEvent` or `MobileParty` and stay on the Stage B Not-tested list, per `lessons/harmony-il.md` |
| L5 | 4 | LOW | `TownRosterAdapter.GetItemCounts` had no direct test | CONFIRMED | `GetItemCounts_NullSettlementOrIds_ReturnsZeroesOnePerId` added (fails against a mutated length) |
| L6 | 4 | LOW | Stage B docs untouched: desertion gate and counts, cultural-feats tests, refuge early return, the provider IL rule | CONFIRMED | Fixed in `alignment-desertion.md`, `cultural-feats.md`, `refuge.md`, `mcm.md` |
| item 4 | 3 | LOW | A failed Stage C walk zeroes every item where the old per-item read isolated one | FALSE POSITIVE | Only a per-id throw could differ, and `GetObject<ItemObject>` for a non-empty id is a dictionary read; a roster throw zeroed every item before too (each per-item call walked the same roster) |
| B1 note | 5 | info | The 384-case gate matrix compares two members that both call `TryGetPurge` | Accepted | Base parity rests on the 21 pre-existing `CalculateDesertion_*` tests, which still pass |
| Issue | 4 | MED (process) | No GitHub issue for plan 037 | CONFIRMED, not this lead's | DECISIONS D4: filed by the run before the branch lands; filed as #719 |

## IMPROVEMENTS (Step 4)

APPLIED:
- **Block 1 (lens 6) and efficiency item 1:** Stage D dropped, `b282aaf2`. PRESERVING (the hand-off
  reused a value the postfix computes again). Proof: the CaravanTrade and TranspilerSiteBinding
  filter, `Passed! - Failed: 0, Passed: 130, Skipped: 0, Total: 130`, then the full suite.
- **Block 3 (lens 6):** `Patch42_HourlyTickParty_Postfix.cs` restored to the base guard (no diff
  against `ba3f2a57`); `Patch42HourlyTickPartyPostfixTests.cs` deleted. PRESERVING (every operand is a
  side-effect-free read). After Stage A the toggle reads cost about what the castle filter costs, so
  the reorder bought nothing and cost a RequiresGame IL test. Proof: full suite.
- **Block 4 (lens 6), S4, B4:** one predicate for the Rohan penalty. PRESERVING. Proof: the
  `CulturalFeats` filter (inside the 416-test run below), unchanged tests green before and after.
- **Block 6 (lens 6):** `CultureItemPool` indexes its item ids in its constructor and exposes
  `ContainsItem`; the maintenance service's `_pooled` cache and `PooledIdsFor` are deleted.
  PRESERVING (ordinal, as the per-call set was; an empty pool keeps nothing, as the null set did).
  Proof: `CultureItemPool_ContainsItem_IsOrdinalAndCoversEveryEntry` and
  `FilterForeignCultureItems_ReadsNoPoolEntries_ThePoolIndexesItsOwnIds` written first; the second
  failed against the cache (`Expected:<0>. Actual:<3>`) and passes now; the TwoCultures and
  NewPoolObject cases stay green.
- **Block 7 (lens 6):** the stale `GetItemCount` comments (with S7).

NOT APPLIED:
- S12 (lens 1, NIT): reason in the table above.
- Block 2 (lens 6): applies only if Stage D ships.

FOLLOW-UP (pre-existing code, not applied; no issue filed by this lead, issues are the run's call):
- Lens 6 Block 5: use `PartyBase.NumberOfMenWithHorse` and `NumberOfAllMembers` and delete
  `CountMountedAndTotal` and `NeedsMountedCount`. Behaviour-CHANGING (a hero changing horse with no
  roster change is counted stale until the roster version moves), so it needs Mike's word.
- Lens 6 Block 8: merge the three "only the accessor reads Instance" IL rules (this branch, plan 003,
  plan 031) at the 031/037 merge, keeping 031's `CachedSettingsClasses_AreMcmGlobalSettings` guard.
- Lens 3 FU1: `CultureFeatAdapter.FromOrNull` allocates per `CalculateFinalSpeed`, on worker threads.
- Lens 3 FU2, FU3: per-row snapshot classes in the desertion and marketplace walks.
- Lens 1 F1: `IAlignmentDesertionService` and `ICulturalFeatsService` have one implementation and no
  fake; removing them needs `Main/SubModule.cs` (single-owner).
- Lens 1 F2: `ICultureFeatAdapter.HasFeat(FeatObject)` and `ICultureMarketplaceMaintenanceService`'s
  `Settlement` parameter carry TaleWorlds types.
- Lens 1 F3: `RefugeCampaignBehavior.cs` is 226 lines, over ADR-002's 150.
- Lens 1 F4: the MCM `AlignmentDesertionRate` has no NaN or range clamp (the plan deferred it).
- Lens 2: `Patch42_HourlyTickParty_Postfix.cs:62` says "(line 293)"; the v1.5.3 line is 302.
- Lens 5 and 4: the Patch59 score postfix's empty catch degrades silently (D6 wants a first reason
  line); it computes distance and recency for caravans the service then ignores (naval, lever off).

## ACTION ITEMS

1. Mike: confirm the Stage D drop, or revert `b282aaf2` after plan 039's profiler shows caravan
   re-thinks matter, and then fix F1 (`Calls(target)`), S1, S3 and D3 before it ships.
2. The run: file the plan 037 issue (DECISIONS D4) before the branch lands on a trunk. Filed as #719.
3. The run: Step 4's convergence pass on the follow-up diff was not run; this lead cannot spawn a
   reviewer. The follow-up diff is `f9e277ce..HEAD`.
4. In game (Stage B and C Not-tested lists stand): castle recruitment, desertion, refuge rally and the
   Rohan penalty still behave; a marketplace day logs no `GetItemCounts` error.

## Verification

- **Base, before any edit (HEAD `f9e277ce`):** `Failed! - Failed: 1, Passed: 12423, Skipped: 2,
  Total: 12426`; the failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- **Touched areas:** `CulturalFeats|CultureMarketplace|ItemPoolAdapter|CastleRecruitment`:
  `Passed! - Failed: 0, Passed: 416, Skipped: 0, Total: 416`. Marketplace doc filter
  (`CultureMarketplace|ItemPoolAdapter`): `Passed! - Failed: 0, Passed: 128, Skipped: 0, Total: 128`.
- **Final full suite:** `Failed! - Failed: 1, Passed: 12400, Skipped: 2, Total: 12403`, the same known
  failure. The total fell by 23: 25 Stage D tests and the Patch42 order test removed, 3 tests added.
- **Reference-assembly unit step** (`-p:TaomGameRefs=RefAsm`, game variables unset):
  `Failed! - Failed: 3, Passed: 10086, Skipped: 28, Total: 10117`; the three failures are the known
  base ones (`EveryLanguage_DeclaresARowForEveryEnglishKey`, `Patch93_HasTheSevenPatchesInItsCategory`,
  `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`).
- **Docs:** `python -B tools/lint_docs.py --fail-on-drift --summary` exit 0;
  `--dash-base ba3f2a57` reports `ai_dashes: 0`.
- **Gate sweep:** none needed; the diff changes no hook, validator or CI step. Hook suite not run (no
  hook or `tools/test_hooks.sh` change).

## CODEX REVIEW

Codex was not run when this report was written (no paid dispatch was asked for this item). The orchestrator
ran it afterwards on `c8c96c09`; its findings and their outcome are in "Codex follow-up" at the end of this
file.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | | | | Not run at report time; see "Codex follow-up" below |

**AGENTS.md lessons (pending):** none from Codex. For the "Look harder here" list, one candidate from
this review: a perf plan stage that reverses a reviewed "cheap enough" decision needs a measurement,
and each later stage is re-weighed after the earlier ones land.

VERDICT: READY FOR COMMIT

## Convergence round 1

The convergence reviewer read `f9e277ce..f58ddb09` and raised two LOW findings, both on comments or
docs. Both were checked against the code and both are fixed in the commit
`fix(campaign): v2.0.32 - convergence fixes for plan 037`.

| # | Severity | Location | Outcome |
|---|---|---|---|
| 1 | LOW | `CultureMarketplaceMaintenanceServiceCacheTests.cs:14-19` | **Fixed.** The class summary still described a per-service pooled id cache that `f58ddb09` deleted; the service keeps only `_routed` and asks `pool.ContainsItem` (`CultureMarketplaceMaintenanceService.cs:19`, `:86`). The summary now says routed sets are cached per service and each pool indexes its own ids. Comment only. |
| 2 | LOW | `docs/features/mcm.md:68` | **Fixed.** The Tests line claimed every one of the eight providers resolves from a container, reads every value through the cached reference and falls back without MCM. The file pins less: the IL rule covers eight, container resolution six (`CampaignHotPathSettingsProvidersTests.cs:73-79`), read-through samples seven (none for the static `PartyIconScaleConfig`), and the no-MCM fallback three. The line now states each scope. |

**Verification:** comment and doc changes only, no behaviour change, so no new test. Full suite before
the edits: `Failed! - Failed: 1, Passed: 12400, Skipped: 2, Total: 12403`; after them the same
line, with the same known failure (`EveryLanguage_DeclaresARowForEveryEnglishKey`).
`python -B tools/lint_docs.py --fail-on-drift --summary` exit 0, `ai_dashes: 0` against `f58ddb09`.
Gate sweep: none; no hook, validator or CI step changed.

## Convergence round 2

The convergence reviewer read `f58ddb09..212d397d` and found nothing. The orchestrator recorded both rounds
in the RCA ("Convergence rounds") and the REVIEW-LOG entry, and re-ran the full suite at `212d397d`:
`Failed! - Failed: 1, Passed: 12400, Skipped: 2, Total: 12403`, the known
`EveryLanguage_DeclaresARowForEveryEnglishKey` only.

## Codex follow-up

The orchestrator ran Codex (adversarial, read-only) on the resolved head `c8c96c09`, after both convergence
rounds. It found no P1 or P2, and its analysis supported the settings caches, the three retained early
returns, the marketplace counts and the Stage D drop. It reported two P3 test gaps. Each was re-read against
the code and the engine (taom-src v1.5.3) before anything changed; both reproduce, and both are closed by
tests alone: no file under `Main/` changed.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | P3 | Yes | The 64-case top-up oracle takes its counts from `StubCounts`, and the one adapter test passes a null settlement, so nothing ran the new roster-count loop. Proved: a `GetItemCounts` that returns an aligned zero array for every real settlement passed all 10 `CultureMarketplaceMaintenanceServiceCacheTests`. Fixed by `TownRosterAdapterGetItemCountsTests` (4 tests on a real `ItemRoster`, a bare `Settlement` and real `ItemObject`s in a real `MBObjectManager`); the independent service oracle stays. |
| 2 | P3 | P3 | Yes | The three refuge tests pass a null `MapEvent`, which walks to no party whichever way `AllRefuges.Count == 0` points. Proved: with the guard inverted in either listener, all 13 existing tests passed. Codex asked for two things. A positive dispatch case: four tests on a staged battle (a lord party, a party with no `MobileParty`, a refuge party), where a refuge in the book dispatches that party's id only and an empty book dispatches nothing for the same battle, for both listeners. And an observable boundary showing that an empty book avoids the walk: none exists (`RefugePartyIds` swallows every exception and each step of the walk reads a plain member), so convergence round 3 pins it in IL, one row per listener: the `IRefugeService.get_AllRefuges` call comes before the `RefugePartyIds` call. That pins call order, not control flow; a walk inside the empty-book branch itself would pass. |
| 16, extras | P3 | P3 | In part | "Linear call-order checks are not full branch proofs" holds for the desertion gate: with the `ShouldEvaluate` answer inverted, the IL-order test still passed. Fixed by two tests that run `ApplyDesertion` on a real `TroopRoster`, each for a player-owned party and for an AI garrison (the first version ran with both flags false only; see round 3). It is not fixable for the speed model's call site, which runs after `base.CalculateFinalSpeed` and needs a live `Campaign`; that stays on the Stage B Not-tested list. "The IL rule proves where `Instance` is called, not first-non-null caching" is accurate and not testable here: MCM is never initialised under MSTest, so the read-through tests (seven providers) and the no-MCM tests (three) bracket it. |

Every other row in Codex's suspect tables was disputed, unverified or a clean result (the known failing
test, S1, S4), and needed no change. Lens 4's L4 deferral ("need a live `MapEvent` or `MobileParty`") is
closed for the refuge and desertion call sites by the tests above.

**Mutation proof** (counts as of `8fc531d7`; round 3 below has the current ones for the desertion and refuge
tests). Each new test was run against a planted mutant of the production file, restored byte for
byte (SHA-256 checked) after every run. Codex's counterexample, `GetItemCounts` returning zeros for every
real settlement: 4 new tests fail, the 10 existing ones pass. First stack only: 3 fail. Mirrored slots: 3.
Null id unguarded: 1. A failed walk returning its partial counts: 1. A failed walk logging nothing: 1.
Refuge guard inverted: 2 fail per listener, and the 13 existing tests pass (Codex's counterexample). Refuge
guard dropped: 2 fail per listener. Desertion gate inverted: 2 fail and the IL-order test passes. Gate
ignored: 1 fails.

**Verification.**
- Full suite after that commit (`8fc531d7`): `Failed! - Failed: 1, Passed: 12410, Skipped: 2, Total: 12413`, the known
  `EveryLanguage_DeclaresARowForEveryEnglishKey` only; the total rose by the 10 new tests.
- Marketplace filter (`CultureMarketplace|ItemPoolAdapter`): `Passed! - Failed: 0, Passed: 132, Skipped: 0,
  Total: 132`, the figure `culture-marketplace.md` now quotes.
- Not tested in game: nothing here changes runtime behaviour; every change is a test or a document.

## Convergence round 3 (the Codex follow-up)

The convergence reviewer read the Codex follow-up commit `8fc531d7` and raised two LOW findings, both gaps in
that commit's own tests. Each was re-read against the code and reproduced with a planted mutant before
anything changed, and both are fixed in `fix(campaign): v2.0.32 - convergence fixes for the plan 037 follow-up`
by tests, comments and documents alone: no file under `Main/` changed.

| # | Severity | Location | Outcome |
|---|---|---|---|
| 1 | LOW | `AlignmentDesertionBehaviorTests.cs`, the two `ApplyDesertion` tests | **Fixed.** Both ran only with `isPlayerOwned` and `isGarrison` false and stubbed `ShouldEvaluate("gondor", false, false)`, so a gate call that passed `false` for `isGarrison`, passed `isPlayerOwned` twice or swapped the two made the same call as the real one. The effect is real: with the MCM Garrisons toggle on and Parties off, an AI garrison would be checked against the Parties toggle and stop deserting. Proved against those tests: five gate-flag mutants, three calculation-flag mutants and `IsHero` replaced by `true` each passed all 20 tests of the two classes. Both tests are now data-driven over `(true, false)` (a player-owned party) and `(false, true)` (an AI garrison). The refusal test answers yes to every other flag combination, so a mangled gate call is answered yes and calculates; the calculation test asserts the same two flags and a non-hero row. A further review found those two rows too few; see "Convergence round 4" below. |
| 2 | LOW | `RefugeCampaignBehaviorTests.cs`, report row 2 above, RCA C2 | **Fixed.** Codex's second P3 asked for a positive dispatch case and for an observable boundary showing that an empty book avoids the walk. Only the first was built, yet row 2 and RCA C2 marked the finding fixed, and two tests were named `*_NoRefuge_DoesNotWalkTheBattle` while asserting only that nothing is dispatched. Proved: with the guard moved below the walk in either listener (`var walked = RefugePartyIds(mapEvent);` before the `AllRefuges.Count == 0` return), all 20 tests passed. The walk cannot be observed from a test: `RefugePartyIds` swallows every exception, and the staged graph is plain fields and auto-properties (`MapEvent._sides`, `MapEventSide._battleParties`, `MapEventParty.Party`, `PartyBase.MobileParty`, `MobileParty.PartyComponent`, v1.5.3). `MapEventListeners_ReadTheBookBeforeWalkingTheBattle_InIlOrder` pins it in IL instead, one row per listener, the shape of the desertion gate's IL-order test. The two behavioural tests are renamed `*_NoRefuge_DispatchesNothingForABattleWithARefugeParty`, and row 2 and RCA C2 now say what each test pins. A further review found two more tests of the same pattern; see "Convergence round 4" below. |

**Mutation proof.** Each mutant was applied to the production file for one run of the two test classes, and the
file was restored byte for byte (SHA-256 checked) after every run. Against the follow-up's 20 tests, all passed:
the gate's flags (`isGarrison` to `false`, to `isPlayerOwned`, the two swapped; `isPlayerOwned` to `false`, to
`isGarrison`), the calculation's flags (`isGarrison` to `false`, `isPlayerOwned` to `false`, swapped), `IsHero`
to `true`, and the guard moved below the walk in either listener. Against the new 24 tests, failures per mutant:
gate `isGarrison` to `false` 2, to `isPlayerOwned` 4, swapped 4, to `true` 2; gate `isPlayerOwned` to `false` 2,
to `isGarrison` 4, to `true` 2; calculation `isGarrison` to `false` 1, `isPlayerOwned` to `false` 1, swapped 2;
`IsHero` to `true` 2; gate inverted 4; gate ignored 2; guard below the walk 1 per listener (the IL-order row
only); guard inverted 2 per listener; guard dropped 3 per listener. Not caught, by design: a walk emitted after
the book read but still run on an empty book (inside the guard's own branch), 24 of 24 pass. The IL-order test
pins call order, not control flow.

**Verification.**
- Both test classes: `Passed! - Failed: 0, Passed: 24, Skipped: 0, Total: 24` (20 before).
- Full suite: `Failed! - Failed: 1, Passed: 12414, Skipped: 2, Total: 12417`, the known
  `EveryLanguage_DeclaresARowForEveryEnglishKey` only; the total rose by the 4 new runs (two desertion rows,
  two IL-order rows).
- Docs: `python -B tools/lint_docs.py --fail-on-drift --summary` exit 0; `--dash-base 8fc531d7` reports
  `ai_dashes: 0`.
- Gate sweep: none; no hook, validator or CI step changed.
- Not tested in game: nothing here changes runtime behaviour; every change is a test, a comment or a document.

## Convergence round 4 (the round 3 fix)

The review of the round 3 fix (`53cbcb58`) left two LOW findings, both gaps in that fix's own tests and text.
Each was re-read against the code and reproduced with a planted mutant before anything changed, and both are
fixed in `fix(campaign): v2.0.32 - residual review follow-ups for plan 037` by tests, comments and documents
alone: no file under `Main/` changed.

| # | Severity | Location | Outcome |
|---|---|---|---|
| 1 | LOW | `AlignmentDesertionBehaviorTests.cs`, the two `ApplyDesertion` tests | **Fixed.** Round 3 replaced the single `(false, false)` row with `(true, false)` and `(false, true)`. On those two rows `isGarrison` is always `!isPlayerOwned`, so a call that passed `!isPlayerOwned` for `isGarrison`, `!isGarrison` for `isPlayerOwned`, or both negated and swapped made exactly the real call on every row. Proved: the gate mutant `ShouldEvaluate(kingdomId, isPlayerOwned, !isPlayerOwned)` passed all 24 runs of the two classes, and each of the six such mutants (three in the gate call, three in the calculation call) passed all 5 desertion runs. In production that gate mutant makes `TryGetPurge` (`AlignmentDesertionService.cs:68-69`) check every AI lord party (`OnDailyTickParty`, both flags false) against `ApplyToGarrisons` instead of `ApplyToParties`. Both tests now run once for every combination of the two flags: the two rows round 3 added, plus `(false, false)` (an AI lord party) and `(true, true)` (a player-owned garrison). The fourth row is not decoration: a mutant that differs from the real call only on `(true, true)` (`isGarrison && !isPlayerOwned`, or `isPlayerOwned && !isGarrison`) passes the other three rows. |
| 2 | LOW | `RefugeCampaignBehaviorTests.cs`, report row 2 of the Codex follow-up | **Fixed.** The round 3 rename covered only the two tests the finding named. `OnMapEventStarted_NoRefuge_ChecksTheBookFirst` and `OnMapEventEnded_NoRefuge_ChecksTheBookFirst` still claimed an order that a null `MapEvent` cannot show: with the guard moved below the walk both passed, and only the IL-order row failed. They are renamed `*_NoRefuge_NullEvent_ReadsTheBookOnceAndDispatchesNothing` and their section header now says it is about a null event. They are renamed rather than deleted because that is the smaller change and the plan's Step 13 specifies them; of the guard mutants tried, the only one they catch (the guard dropped) also fails the `DispatchesNothing` tests and the IL-order row. The two `*_NoRefuge_DispatchesNothingForABattleWithARefugeParty` tests staged a single refuge party, so row 2's "the same battle" (a lord party, a party with no `MobileParty`, a refuge party) was not literally true; they now stage that battle, as the tests with a refuge in the book do, and row 2 stands as written. The comment above the battle tests said the IL test pins "whether the walk ran"; it now says that the book is read before the walk is pinned in IL, as call order and not control flow. |

**Mutation proof.** Each mutant was applied to the production file for one run of the filtered test classes, and
the file was restored byte for byte (SHA-256 checked) after every run. A control first: the gate's
`isGarrison` replaced by `false` fails 2 of the 5 desertion runs, as the round 3 table says. Against the
round 3 tests, the six negation mutants passed. The guard moved below the walk (either listener) failed only
`MapEventListeners_ReadTheBookBeforeWalkingTheBattle_InIlOrder`, so both `*_ChecksTheBookFirst` tests passed it;
the guard dropped failed the `ChecksTheBookFirst` test, the `DispatchesNothing` test and the IL-order row (3 per
listener); the guard inverted failed 2 per listener, and not the pair. Against the new 28 tests, the gate
mutants fail 4 (the `(false, false)` and `(true, true)` rows of both tests) and the calculation mutants 2 (those
rows of the calculation test). A mutant that differs only on `(true, true)` fails that row alone: gate 2,
calculation 1. The earlier mutants still fail (the gate-ignored one was not rerun): gate `isGarrison` to `false`, to `true`, `isPlayerOwned` to
`false`, to `isGarrison`, to `true`, and the two swapped, 4 each; gate inverted 8 (every row of both tests);
calculation `isGarrison` to `false`, `isPlayerOwned` to `false` and swapped, 2 each; `IsHero` to `true` 4. The
refuge counts are unchanged by the renames and the shared battle: guard below the walk 1 per listener (the
IL-order row only), dropped 3, inverted 2.

**Verification.**
- Both test classes: `Passed! - Failed: 0, Passed: 28, Skipped: 0, Total: 28` (24 before).
- Build: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` succeeded with 0 errors.
- Full suite: `Failed! - Failed: 1, Passed: 12418, Skipped: 2, Total: 12421`, the known
  `EveryLanguage_DeclaresARowForEveryEnglishKey` only; the total rose by the 4 new runs (the two agreeing rows
  in each of the two desertion tests).
- Gate sweep: none; no hook, validator or CI step changed.
- Not tested in game: nothing here changes runtime behaviour; every change is a test, a comment or a document.
