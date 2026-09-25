# Deep review: plan 014, Enlistment session reset on load and new campaign (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 014, reset Enlistment's per-session clocks and caches on load and on a new campaign
         (branch improve/014-enlistment-session-scope, diff 7f02fc8d..d1221b7f, 5 commits, 14 files)
Date: 2026-09-24

Scope:   C# (7 production files under Main/Features/Enlistment, 4 test files), docs
         (docs/features/enlistment.md, CHANGELOG.md). No XML, XSLT, scripts or harness files.
         No live TAOM_Map or LOTRLOME_Armory file belongs to this change.
Waves:   wave 1: Agents 1, 2, 5; wave 2: Agents 3, 4, 6. Codex gpt-6-astra (ultra) in parallel.

STANDARDS:     PASS: 0 violations of the numbered checks; 3 LOW, 1 NIT, 1 PROCESS
COMPATIBILITY: PASS: 12 verified, 0 incompatible, 0 unverified; 2 LOW text findings, 1 NIT
EFFICIENCY:    PASS: 0 issues in the changed hunks (1 LOW follow-up, UNVERIFIED)
COMPLETENESS:  INCOMPLETE: GitHub issue missing (needs Mike); a test comment made false, a
               CHANGELOG overclaim and an unpinned plan decision (all fixed)
DATA FLOW:     PASS after fixes: 11 flows traced, 1 gap (the CHANGELOG overclaim), 1
               inconsistency (the stale test comment)
DESIGN:        7 KEEP proposals (3 apply, 4 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
CODEX:         P1 0, P2 0, two P3 observations, both confirmed and fixed
```

## DETAILS

Every finding below was re-read against the worktree before it was classified. Line numbers are at
`d1221b7f` unless a fix moved them.

### Agent 1: Standards

The numbered checks 1 to 10 pass: no TaleWorlds type in the four services, no banned construct,
`EnlistmentBehavior.cs` at 149 lines (under the ADR-002 ceiling), no new types, no DryIoc cycle, no
`IoC.Resolve`, commit subjects carry `v2.0.30` and no trailer.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| S1 | LOW | `ServiceMaintenanceService` now depends on the wait-menu presenter with no comment saying why (the feature's own rule is that a service does not call presentation) | CONFIRMED, fixed: a one-line comment on the field |
| S2 | LOW | The new-campaign reset is ungated while the load reset sits behind the authority gate, and nothing in code states the invariant that makes the ungated call safe | CONFIRMED, minimum fix applied (the "every peer, field clears only" invariant is now in the interface and method docs). The zero-line reorder is behaviour-changing: NEEDS MIKE |
| S3 | LOW | `enlistment.md` bold claim "Every clock-keyed latch ... on both lifecycle edges" overclaims (co-op client load; `FieldDutyRuntime`) | CONFIRMED, fixed: narrowed to campaign-clock latches on a host's load and a new campaign, with the unreset values named |
| S4 | PROCESS | No GitHub issue for plan 014 | CONFIRMED, NEEDS MIKE (a public issue; `/issue` is never auto-invoked, and the GitHub MCP needs authorisation in this session) |
| S5 | NIT | New test names lead with the scenario, not the method | Not applied: the test family already uses scenario-first names (`FreshCampaign_NoSyncData_ResetsTheService`) |

### Agent 2: Engine compatibility

12 engine usages verified against the installed v1.5.3 decompile, including the firing set
(`Campaign.cs:1709` to `:1607`, after `RegisterEvents` at `:1645`), that a new campaign never
raises `OnGameLoaded`, and that every reset callee only clears a field.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| E1 | LOW | `EnlistmentReconcilerTests.cs:835-836` still says the reset is wired to `OnGameLoaded` only | CONFIRMED, fixed |
| E2 | LOW | The same overbroad doc claim as S3 | CONFIRMED, fixed (same edit) |
| E3 | NIT | `IServiceAttachmentService` doc says a future anchor holds "until the new clock passes it"; the check holds until anchor plus 6 hours | CONFIRMED, fixed |

### Agent 3: Efficiency

No performance issue in the changed hunks: every reset is a field clear, called once per session
(`ResetSessionCaches` has two callers, both lifecycle hooks), and a pump test pins that the pump
never calls the new resets. No APPLY-scoped fixes.

### Agent 4: Completeness

Tests, feature doc, CHANGELOG and IoC pass. The executor's full-suite log matched the CHANGELOG.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| C1 | MED (process) | No GitHub issue | Same as S4: NEEDS MIKE |
| C2 | LOW | The stale test comment | Same as E1, fixed |
| C3 | LOW | CHANGELOG "Both paths now clear every per-session value" | CONFIRMED, fixed |
| C4 | LOW | The plan's "reset not gated on `_justLoadedFromSave`" decision has no test | CONFIRMED, fixed: `NewCampaign_AfterALoadingSyncData_StillResets_ButKeepsTheLoadedRecord` |
| C5 | NIT | `OnNewGameCreated(null)` raised CS8625 | CONFIRMED, fixed (`null!`) |

### Agent 5: Data flow

All three latches, both lifecycle edges, the DI graph, the co-op client path and the no-per-tick
rule traced CONNECTED.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| D1 | LOW (MED for the pre-existing silent grace) | CHANGELOG completeness claim false: `EnlistmentReconciler._lossAnnouncedFor` is a shown-flag on the same singleton that no reset clears | Overclaim CONFIRMED and fixed (C3). The latch itself: CONFIRMED pre-existing (writers only at `EnlistmentReconciler.cs:216-219` and `:377`), behaviour-changing, reconciler code out of the plan's scope: NEEDS MIKE |
| D2 | LOW | Stale test comment | Same as E1, fixed |
| D3 | NIT | The reset's comment files the rhythm cache under "a stamp left in the future"; its hazard is an equal-hour reload | CONFIRMED, fixed (the rhythm call has its own comment) |
| D4 | gap | The load hook's reset is pinned only by container resolvability | CONFIRMED, fixed with Codex's sentinel test (below) |

### Agent 6: Design and elegance

| # | Proposal | Behaviour | Verdict |
|---|---|---|---|
| A1 | Narrow the CHANGELOG sentence | PRESERVING | APPLIED |
| A2 | Fold `InvalidateCommanderCache` into `IServiceAttachmentService.ResetForNewSession`, delete the pass-through | PRESERVING | APPLIED (RED test first) |
| A3 | "is dropped from" reads as "removed from" | PRESERVING | APPLIED ("is called from") |
| F1 | Delete `_lossAnnouncedFor`, or clear it in `ResetForNewSession` | CHANGING, pre-existing | FOLLOW-UP, NEEDS MIKE |
| F2 | Move the reset above the authority gate in `OnGameLoaded` | CHANGING (co-op client only) | NOT APPLIED, NEEDS MIKE (the plan froze `OnGameLoaded`) |
| F3 | `OnGameLoaded` normalizes the previous session's record when the loaded save has no Enlistment data | CHANGING, pre-existing | FOLLOW-UP, NEEDS MIKE. Premise verified: `CampaignBehaviorDataStore.LoadBehaviorData` calls `SyncData` only when the save holds an entry for the behavior (installed v1.5.3 decompile, `:86-106`), and the store is cleared only in `OnSessionLaunched`, which runs after `OnGameLoaded` |
| F4 | Comments the change made false | PRESERVING | The test comment is fixed (E1); the adapter comments ("session launch") are pre-existing: FOLLOW-UP |

## CODEX REVIEW

Codex gpt-6-astra at ultra, prompt
[codex-adversarial-014-enlistment-session-scope-2026-09-24.prompt.md](codex-adversarial-014-enlistment-session-scope-2026-09-24.prompt.md),
output `docs/reviews/raw/codex-adversarial-014-enlistment-session-scope-2026-09-24.md` (complete: it
ends with "END OF CODEX REVIEW"; 135,437 tokens). It reviewed `7f02fc8d` to `d1221b7f` read-only and
ran nothing. Verdict: no P1 or P2, two P3 observations. It decompiled the lifecycle order from the
installed DLL, walked five concrete paths (first campaign, earlier save, same-hour reload, second
campaign, co-op) and cross-referenced the test settlement ids and string keys against ModuleData.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 (testing) | LOW | Yes | The load hook's reset had no direct test (the container test proves resolution, not invocation). Its proposed test works: `_playerParty.GetMainHeroId()` is evaluated before `CampaignTime.Now` at `EnlistmentBehavior.cs:121`, so a sentinel thrown there stops the hook after the reset. Added as `GameLoad_OnTheHost_ResetsTheSessionCaches_BeforeNormalizing`, which also pins the order |
| 2 | P3 (docs) | LOW | Yes | "every per-session value" is false (`_lossAnnouncedFor`), and the doc's bold claim sits one line after the co-op client exception it contradicts. Both narrowed |
| S1 to S10 | suspects | n/a | Yes | Its dispositions (DISPUTED, UNVERIFIED for unrun history) match the code; no suspect needed a change |

**Confirmed bugs:** none in production behaviour. Two text and coverage defects, fixed as above.
**False positives:** none.
**Design questions:** none raised by Codex; the lenses' design questions are under NEEDS MIKE.
**Things Codex missed:** the stale test comment in `EnlistmentReconcilerTests.cs` (it checked only
the reconciler's own comments, as the plan's grep did), the dwell doc's arithmetic slip, and the
rhythm cache filed under the wrong hazard.

## ACTION ITEMS

1. NEEDS MIKE: file the GitHub issue for plan 014, put its number in the CHANGELOG heading, and
   label it `triage-needs-ingame` at close (the in-game load and second-campaign checks are owed).
2. NEEDS MIKE: `_lossAnnouncedFor` (clear it in `EnlistmentReconciler.ResetForNewSession` and on
   discharge, or delete the latch as Agent 6 F1 argues).
3. NEEDS MIKE: move `ResetSessionCaches` above the authority gate in `OnGameLoaded`.
4. NEEDS MIKE: clear the store before normalizing when the loaded save has no Enlistment data.
5. Resolve the CHANGELOG `## 2026-09-24` heading against the main checkout's staged entry at merge.

## IMPROVEMENTS (Step 4)

APPLIED:
- `Main/Features/Enlistment/ServiceAttachmentService.cs:46-50`, `IServiceAttachmentService.cs`,
  `ServiceMaintenanceService.cs:224-233`: `ResetForNewSession` now also invalidates the adapter's
  cached commander party; the `IServiceAttachmentService.InvalidateCommanderCache` member, its
  pass-through and the separate call in `ResetSessionCaches` are deleted (Agent 6 A2). Proof:
  `AttachmentReset_AlsoDropsTheAdaptersCachedCommanderParty` failed before the fold and passes
  after; `ServiceMaintenanceServiceTests.ResetSessionCaches_AlsoDropsTheArmyAdapterHandle` now
  asserts `ResetForNewSession`. `DischargeService.cs:123` still calls the adapter directly,
  unchanged.
- `CHANGELOG.md:13` narrowed (Agent 6 A1); `docs/features/enlistment.md:1693` "is called from"
  (Agent 6 A3). Prose only.
- New characterisation tests, green before and after: the load hook's routing and order (Codex 1),
  the new-campaign reset after a loading `SyncData` (Agent 4 C4).
- Comments: the presenter dependency (S1), the every-peer invariant (S2 minimum), the dwell
  arithmetic (E3), the rhythm hazard (D3), the stale test comment (E1).

NOT APPLIED:
- `EnlistmentBehavior.cs:115-121`, reset above the authority gate: behaviour-changing for a co-op
  client and the plan froze `OnGameLoaded`; needs Mike.
- `EnlistmentReconciler.cs:64`, `_lossAnnouncedFor`: behaviour-changing, pre-existing, reconciler
  code out of the plan's scope; needs Mike.
- `EnlistmentBehavior.cs:121`, clear before normalizing a save with no Enlistment data:
  behaviour-changing, pre-existing; needs Mike.
- Test names (S5): the family's scenario-first precedent.
- Convergence pass (Step 4.6): not run; the review lead cannot spawn a `deep-reviewer`. Owed to the
  orchestrator on the fix diff.

FOLLOW-UP (pre-existing code; no issue filed, since filing a public issue needs Mike's OK):
- `EnlistmentReconciler._lossAnnouncedFor` also survives a grace-expiry discharge, so re-enlisting
  under the same lord can give a silent grace (Agent 5 F1, Agent 4 FU1).
- `FieldDutyRuntime.cs:66-75`: the pace estimate survives a load and a new campaign, and its "After
  a save/load it is null" comment is false for a singleton (cosmetic).
- `BattleMeritAccumulator._pending` survives a session change; reachability UNVERIFIED (Agent 5 F2).
- Discharge clears only the commander cache: a re-enlistment within 6 hours can inherit a dwell
  hold, and within 24 hours the first arrival can be silent (Agent 5 F4).
- The offer's settlement-id latch is never cleared when a stop ends (Agent 5 F5): designer to
  confirm the intent.
- `ArmyRhythmSnapshotService` keys on the hour only, not the commander (Agent 3).
- `SubModule.OnGameEnd` could call `ResetSessionCaches` so the dead campaign's `MobileParty`, `Army`
  and `_lastSessionStarter` are released at the menu (Agents 2 and 3; single-owner file, heap
  benefit UNVERIFIED). Fold into the plan's deferred `ISessionScoped` item.
- "session launch" wording in `IMobilePartyAttachmentAdapter.cs:36` and
  `MobilePartyAttachmentAdapter.cs:163` (the reset never runs from `OnSessionLaunched`).
- Hook files over the ADR-002 ceiling: `EnlistmentMenuBehavior.cs` 160 lines,
  `EnlistmentBattleBehavior.cs` 302, `EnlistmentContentBehavior.cs` 151.

## NEEDS MIKE

1. File the plan 014 GitHub issue (public) and put its number in the CHANGELOG heading.
2. Reset before the authority gate in `OnGameLoaded` (co-op client load), which also makes the
   doc's "every peer" story uniform.
3. `_lossAnnouncedFor`: clear on session reset and discharge, or delete the latch.
4. `OnGameLoaded` normalizes a stale record when the save has no Enlistment data.
5. `SubModule.OnGameEnd` teardown call (single-owner file).
6. Whether the offer latch should survive the end of a stop (Agent 5 F5).

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for every branch. Lines to add to "Lessons From Prior Reviews":
- Bugs Codex typically misses: a stale copy of a rewritten claim outside the files the plan names
  (here a test comment, with the phrase split across two lines).
- What Codex does well: finding a test seam in a hook that cannot finish outside a campaign (a
  sentinel thrown from an argument evaluated before the first engine read).

## Verification

Full suite in the worktree, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, before
the fixes: 10243 passed, 2 skipped, 2 failed. After: 10246 passed, 2 skipped, 2 failed, total 10250.
The two failures both times are the known live-Armory tests
`TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

RCA: [rca-enlistment-session-scope-2026-09-24.md](rca-enlistment-session-scope-2026-09-24.md).

VERDICT: READY FOR COMMIT (the missing GitHub issue must be settled before merge; the convergence
pass on the fix diff is owed)

## Convergence

A convergence reviewer ran on the fix diff `d1221b7f..b9d18458`. Behaviour parity: PASS (the
folded `InvalidateCommanderCache` pass-through had one caller, now gone; every reset callee is a
field clear, so the new order does not matter; the new hook tests hold under NSubstitute 5.1.0).
Standards: PASS. It raised two text defects; the review lead re-read the cited code and confirmed
both. No false positives.

| # | Sev | Defect | Verified against | Fix |
|---|---|---|---|---|
| C1 | LOW | The narrowed wording still overclaimed: CHANGELOG "the feature's cached engine handles", closed "not reset" lists in the CHANGELOG and `enlistment.md` ("Both are follow-ups"), and `IServiceMaintenanceService` "Drop the feature's per-session caches" | `CommanderLordAdapter._lastSeenMapEvent` is assigned only in `TokenFor` and never cleared, on a singleton (`EnlistmentIoC.cs:13`); `BattleMeritAccumulator._pending` is cleared only by `Consume`, also a singleton (`EnlistmentIoC.cs:86`); `ResetSessionCaches` calls neither | CHANGELOG names the cached commander party and the army handle; both lists add the two fields and say the list comes from the review's field sweep and may not be complete; the interface doc points at `ResetSessionCaches` |
| C2 | NIT | RCA row 3 called "Each held an absolute campaign hour" false for the rhythm cache, and the test class summary gave every covered field the "a moment ago" hazard | `ArmyRhythmSnapshotService.GetSnapshot` stores `Math.Floor(nowDays * 24.0)` and tests it with `==`, so a future stamp misses rather than reading as recent | RCA row 3 names the "a moment ago" clause as the false one; the test summary covers the clock stamps, the rhythm cache's same-hour reload, the cached `MobileParty` handle and the hooks |

Both fixes are text only (one doc comment in `Main`, one in `TAOM.Tests`), so no test was added.
`_lastSessionStarter` (listed above under the `OnGameEnd` follow-up) is a hook-registration guard,
not a per-session value the reset should clear, so it stays out of the "not reset" lists.

Full suite after the convergence fixes: 10246 passed, 2 skipped, 2 failed, total 10250. The two
failures are the known live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

CONVERGENCE: DEFECTS 2, both fixed; the text-only fixes have not had a fresh review. The missing
GitHub issue must still be settled before merge.

## Maintainer decisions applied (2026-09-24)

Mike answered the NEEDS MIKE items on 2026-09-24; the public issue is #656. All six are applied in
one commit on this branch, `fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014`
(parent `41754a03`; a commit cannot name its own hash, so `git log` on the branch gives it). Each
behaviour change was test-first: the new tests were run and seen failing on the unchanged code, with
the failure quoted below, then passed after the change.

| NEEDS MIKE | Decision | What changed | RED seen before the change |
|---|---|---|---|
| 3 | Clear `_lossAnnouncedFor` on the session reset and on discharge | `EnlistmentReconciler.ResetForNewSession` clears it; the reconciler's constructor subscribes `DischargeService.EnlistmentEnded` and clears it there, so every discharge path counts, not only those the reconciler raises. The subscription sits in the reconciler because `DischargeService` cannot take the reconciler (the reconciler already depends on `IDischargeService`, so that would be a DryIoc cycle); both are singletons, so it subscribes once per process | `ResetForNewSession_ReArmsTheLossModal_ForTheSameCommander`: "Expected:<2>. Actual:<1>. a loss in a new session was silent"; `Discharge_ReArmsTheLossModal_ForAReEnlistmentUnderTheSameCommander`: "Expected:<2>. Actual:<1>. a loss in a new term was silent" |
| 2 | Reset above the co-op authority gate in `OnGameLoaded` | `ResetSessionCaches` now runs first on every peer; only `Normalize` stays behind the gate. The interface, method and reconciler comments and `enlistment.md` now say "every peer's load, a new campaign and game end" | `GameLoad_OnACoopClient_StillResetsTheSessionCaches_ButDoesNotNormalize`: "Expected to receive exactly 1 call matching: ResetSessionCaches() Actually received no matching calls." |
| 4 | Clear the store first when the loaded save has no Enlistment data | `OnGameLoaded` calls `_store.Clear()` when no loading `SyncData` ran (`_justLoadedFromSave` false), before the gate, so it applies on every peer; the host then normalizes the empty record, so the ownerless-parked rescue still runs. Engine order re-checked in the installed v1.5.3 decompile: `LoadBehaviorData` runs in the saved-campaign initialize branch (`Campaign.cs:1448`), `OnGameLoaded` later in `PostInitializeFourthState` (`:1685`) | `GameLoad_SaveWithNoEnlistmentData_ClearsThePreviousRecord_BeforeNormalizing`: "Expected:<False>. Actual:<True>. the previous session's term reached normalization"; the co-op client twin: "Assert.IsFalse failed." A characterisation test (`GameLoad_AfterALoadingSyncData_KeepsTheLoadedRecord`) pins that a load with data is not cleared |
| 6 | The offer latch clears when the stop ends | `ServiceAttachmentService.ExitSettlementForService` raises a new `ColumnLeftSettlement` event once the player is out (also when only the re-park fails; not when the leave fails); `EnlistmentMenuBehavior` routes it to the new `IEnlistmentWaitMenuPresenter.OnStopEnded`, which clears the settlement id only. The 24-hour cooldown is kept, so a commander dipping straight back in still gets one modal a day | Against no-op stubs of the new members: `OfferTownLeave_SameSettlementAfterTheStopEnded_AsksAgain`: "Expected to receive exactly 2 calls ... Actually received 1 matching call"; `Exit_RaisesColumnLeftSettlement_WhenThePlayerLeaves` and `..._EvenWhenOnlyTheReParkFails`: "Expected:<1>. Actual:<0>." |
| 5 | `SubModule.OnGameEnd` calls the reset | One call, `IoC.Resolve<Features.Enlistment.IServiceMaintenanceService>()?.ResetSessionCaches()`, plus a one-line comment, placed last inside the existing best-effort teardown `try`, so a throw cannot skip the two ArmyTargeting resets before it. Nothing else in `SubModule.cs` changed | `GameEnd_ReachesTheSessionReset_SoTheDeadCampaignsObjectsAreReleased`: "StringAssert.Contains failed." An honest RED was possible only at the source level: `OnGameEnd` needs a live `Game` and cannot run in a unit test, so the test reads `Main/SubModule.cs` and checks the `OnGameEnd` body, the same pattern as `ExitStallDisarmTests.DisarmWiring_IsPresentInBothClosers`. The heap benefit stays UNVERIFIED |
| 1 | Cite #656 | The CHANGELOG heading now reads `(#656, plan 014)` | Text only |

Notes for the next reviewer:

- `EnlistmentBehavior.cs` stays at 149 lines. `EnlistmentMenuBehavior.cs` goes from 160 to 162
  lines (it was already over the ADR-002 ceiling, listed above as a follow-up); the two lines are
  the `-=`/`+=` pair for `ColumnLeftSettlement`, mirroring `ColumnEnteredSettlement` beside it.
- The `ColumnLeftSettlement` subscription in `EnlistmentMenuBehavior.RegisterEvents` has no unit
  test, like its `ColumnEnteredSettlement` sibling: `RegisterEvents` touches `CampaignEvents` first,
  which needs a live campaign. The event and the presenter's handler are each tested.
- The stop's end is defined as `ExitSettlementForService`, the path that walks the player out to
  rejoin the column. A player leaving by another route (discharge, the siege leave in
  `ServiceBattleService`) does not clear the settlement latch; a later stop there is still covered
  once the next exit or a different settlement changes it.
- Clearing the store on a co-op client's load with no Enlistment data follows decision 2's "every
  peer resets its session state"; it is an in-memory `_record.Reset()`, and `OnSessionLaunched`
  would have cleared the same record on a new starter anyway.
- These changes have not had a fresh `/deep-review` or Codex pass; one is owed on this commit.

Verification: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` succeeded
(0 errors). All Enlistment tests (`FullyQualifiedName~TAOM.Tests.Features.Enlistment`): 1105
passed, 0 failed. Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
"Failed: 2, Passed: 10259, Skipped: 2, Total: 10263"; the two failures are the known live-Armory
tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
