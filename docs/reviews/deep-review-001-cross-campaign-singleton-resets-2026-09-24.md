# Deep review and Codex: plan 001, the SpecialResources new-campaign reset (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 001, SpecialResources half (branch improve/001-specialresources-reset)
Date: 2026-09-24 (review lead pass 2026-09-25)

Scope:   C# (1 production file, 1 new test file), docs (CHANGELOG, feature doc)
         Diff a39a9c86..4263535a; review fixes in the follow-up commit
Waves:   six lenses (Standards, Engine compatibility, Efficiency, Completeness,
         Data flow, Design); the wave split is in the orchestrator's run log.
         Codex gpt-6-astra (reasoning ultra) in parallel, complete
         ("END OF CODEX REVIEW" present).

STANDARDS:     FAIL: 3 findings (1 MEDIUM, 2 LOW); checks 1 to 10 pass
COMPATIBILITY: PASS: 0 incompatible, 1 unverified (external SyncData callers)
EFFICIENCY:    PASS: 0 issues in the changed hunks; 2 follow-ups
COMPLETENESS:  INCOMPLETE: no GitHub issue; 4 LOW doc and fixture items
DATA FLOW:     PASS for the changed code: 1 pre-existing gap (T5), 3 inconsistencies
DESIGN:        6 KEEP proposals (2 apply, 4 follow-up)
XML:           NOT IN SCOPE (no XML touched)
TOOLING:       NOT IN SCOPE (no tools touched)
```

## Verification of every finding

Each finding below was re-read against the worktree at `4263535a` and, for engine claims, against the
v1.5.3 taom-src cache (`TaleWorlds.CampaignSystem.CampaignBehaviorDataStore.cs:17-38,84-106`,
`Campaign.cs:1444-1449,1683-1686`, `Hero.cs:958`). Duplicates across lenses are merged.

| # | Sev | Finding | Raised by | Verdict | Evidence I read |
|---|---|---|---|---|---|
| F1 | MEDIUM | A save with no `SpecialResourcesBehavior` record (older than `e154747e`, 2026-04-07), loaded after another campaign in the same process, keeps that campaign's balances; the `Contains` gate at `SpecialResourcesBehavior.cs:181` then skips the legacy seed and the next save persists them | Agent 1 (MEDIUM), Agents 2, 3, 4, 5 (T5), 6 (P2) as follow-up, Codex P2 #1 | **CONFIRMED, pre-existing; fix NEEDS MIKE** | `LoadBehaviorData` calls `SyncData` only when a record matches by StringId or type name (:86-106); `OnGameLoaded` (:1685) runs before `OnSessionStart` (:1686). The fix is behaviour-changing and plan 001 scoped it out (`plans/001-cross-campaign-singleton-resets.md`, "Explicitly deferred"). Recorded now as a CHANGELOG "Known limitation" and an "Open gap" in the feature doc |
| F2 | LOW | CHANGELOG and feature doc say the null-local load fixes "a save without the balances key", which reads as covering F1's saves; no TAOM build ever wrote a record without the key | Agents 1, 2, 4, 5 (I1), 6 (P1), Codex (doc note under #1) | **CONFIRMED, fixed** | `git grep` at `e154747e` shows the key written from the first commit (`SpecialResourceStorageService.cs:29` there); the engine save always `_records.Add`s it (:30) |
| F3 | LOW | CHANGELOG says balances were kept "for the player and every lord"; every storage writer keys `Hero.MainHero` (plus a co-op joiner) | Agents 1, 3, 4, 5 (I2) | **CONFIRMED, fixed** | grep: `SpecialResourcesBehavior.cs:164,184,190,306,353,364,384-386,397,411,512`, `SpecialResourceCheats.cs:60,83`, the party-screen hooks, `JoinReconciliationService.cs:109` |
| F4 | LOW (Agent 4: MEDIUM) | No GitHub issue for the fix; the owed in-game smoke has no issue to carry `triage-needs-ingame` | Agents 1, 4 | **CONFIRMED; NEEDS MIKE** (public action) | Lens `gh` searches quoted in their reports; the CHANGELOG heading cites no issue |
| F5 | LOW | Test comment says `Hero.MainHero` "is null" outside a campaign; it throws. The test name `NoMainHeroYet`, the doc's "with no main hero yet" and the production comment "whether or not Hero.MainHero is resolvable yet" describe a state the engine never reaches in `OnNewGameCreated` | Agents 2, 4, 5 (I3), 6 (P3), Codex | **CONFIRMED, fixed** | `Hero.cs:958` `MainHero => CharacterObject.PlayerCharacter.HeroObject`; `PlayerCharacter` reads `Game.Current` |
| F6 | LOW | Fixture uses resource id `castar`; the shipped id is `caster` (display name "Castar") | Agent 4, Codex P3 #2 | **CONFIRMED, fixed** | `special_resources_config.xml:40` `<Resource id="caster" display_name="Castar"` |
| F7 | NIT | `FakeDataStore` claimed to mirror the engine but skipped null on save and overwrote a duplicate key; the engine records null and throws on a duplicate (`_records.Add`) | Agent 2 #3 (Agents 4, 5 noted it as harmless) | **CONFIRMED, fixed** | `CampaignBehaviorDataStore.cs:26-38` |
| F8 | LOW | Plan 001's RED step builds `Main/TAOM.csproj`, which cannot compile the test project, so the stated RED is not a test RED | Codex P3 #3 | **CONFIRMED, plan text only** | Plan lines 229 and 276. The plan is executed; recorded here and in `lessons/misc.md`, not edited |
| F9 | (Agent 1 sub-point) | The reset is `RestoreData(null)`, not a named `ResetForNewSession()` as the MANDATORY rule names it, and the interface does not document null | Agent 1 | **FALSE POSITIVE as a defect of this diff** | `SpecialResourceStorageService.cs:42` coalesces null to a new dictionary, and `OnNewGameCreated_AfterPriorCampaignBalances_StartsWithEmptyStorage` pins it. A named method touches the storage interface, which plan 001 put out of scope; it belongs with the lifecycle-service follow-up below |

**UNVERIFIED, still:** whether TAOM_Online calls a behavior's `SyncData` with a loading store outside the
engine (Agents 2, 5; the repo is not on this machine). Whether a pre-2026-04-07 save still loads on
v1.5.3 at all, which decides whether F1 reaches any player.

**Now verified:** the suite count the lenses could not run. Baseline at `4263535a` and after the fixes:
`Passed: 10318, Skipped: 2, Failed: 0, Total: 10320`.

## DETAILS

The six lens reports are kept in the orchestrator's run log; their findings are merged in the table
above. Highlights by lens:

- **Agent 1 Standards:** checks 1 to 10 pass (no ADR-007, #region, Obsolete, preprocessor, IoC or
  naming violation; `internal` backed by `InternalsVisibleTo`, `TAOM.csproj:117-122`). Findings F1, F2
  wording, F3, F4, F9.
- **Agent 2 Engine compatibility:** 9 verified, 0 incompatible, 1 unverified. `OnNewGameCreatedEvent`
  is raised only on the new-campaign branch (`Campaign.cs:1709`), after `RegisterEvents` (:1645) and
  before character creation, so the wipe can neither erase loaded balances nor the phase-9 seed. The
  removed `Hero.MainHero` guard was dead code.
- **Agent 3 Efficiency:** no issue in the changed hunks; the save path is slightly cheaper. Two
  follow-ups (static event, `OnGameEnd`).
- **Agent 4 Completeness:** tests, doc and CHANGELOG present; issue missing (F4); F2, F5, F6, F3.
- **Agent 5 Data flow:** 13 flows traced, T1 to T4 and T6 to T10 connected, T5 = F1, I1 to I3 =
  F2, F3, F5; F2 sibling leak in `CareerPersistenceBehavior`.
- **Agent 6 Design:** P1 and P3 apply-scoped, P2, P4, P5, P6 follow-up. Already optimal: the dead-guard
  removal, `internal` over the plan's `public`, the hook choice, reusing `RestoreData(null)`, the
  direction-split `SyncData`.

## ACTION ITEMS

1. **Mike:** decide F1: add the `_syncedThisSession` flag and a reset at the top of `OnGameLoaded` (the
   FiefGrantingCampaignBehavior and FieldCampCampaignBehavior shape; decision D14 chose "clear the store
   first" for Enlistment's version of this case), or leave it as the recorded known limitation. The
   reset must not go in `OnSessionLaunched`: `OnGameLoaded` runs first and would have its legacy seed
   wiped.
2. **Mike or orchestrator:** file the GitHub issue (F4), then cite it in the CHANGELOG heading and the
   feature doc's changelog line; label `triage-needs-ingame` for the owed smoke when it closes.
3. **Orchestrator:** run the Step 4 convergence checker on the follow-up commit's diff.

## IMPROVEMENTS (Step 4)

**APPLIED** (both behaviour-preserving; the five tests were green in the baseline full run and green
after, `SpecialResourcesBehaviorSessionResetTests` 5 of 5, full suite 10318 / 2 / 0):

- **P1**, `CHANGELOG.md` 2026-09-24 entry and `docs/features/special-resources.md` (SyncData bullet,
  Tests, Changelog): the key-miss wording narrowed to "a behavior record without the balances key",
  marked defensive, the record-less gap stated as a known limitation, and "every lord" replaced by
  "every hero the player had controlled". Proof: docs only.
- **P3**, `TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs`: one
  construction path (`_service` field, `NewBehavior(storage, service)`), the second test renamed
  `OnNewGameCreated_ReadsNoHero_StillResetsTheServiceSessionState` and driven through `_sut`, the
  comment corrected (reading `Hero.MainHero` outside a game throws). With it, the F5 production comment
  in `SpecialResourcesBehavior.cs` (above `OnNewGameCreated`), the F6 fixture id and the F7 fake
  fidelity. Proof: the same five assertions, unchanged, green.

**NOT APPLIED:**

- **P2** (Agent 6), `SpecialResourcesBehavior.cs` load branch and `OnGameLoaded`: behaviour-CHANGING
  (a pre-feature save loaded after another campaign would start empty and receive its seed); needs Mike
  (action item 1). Its RED tests are named in Agent 6's report:
  `ResetIfNoLoadedRecord_NoLoadingSyncData_WipesThePreviousCampaign` and
  `ResetIfNoLoadedRecord_AfterALoadingSyncData_KeepsTheLoadedBalances`.
- Agent 3 raised no APPLY-scoped fix.

**FOLLOW-UP** (pre-existing code; no issue filed, since filing is a public action reserved for Mike in
this sprint):

- Agent 3 FU1: `ScreenManager.OnPushScreen` is a static event, so every behavior instance since the
  last game over stays subscribed (and rooted); the comment at `SpecialResourcesBehavior.cs:79-89` that
  calls the old instance GC-eligible is wrong. Behaviour-preserving fix sketched in the lens report.
- Agent 3 FU2: reset the storage in `SubModule.OnGameEnd` (single-owner file; whether `OnGameEnd` fires
  on a load from inside a campaign is UNVERIFIED). An alternative to P2.
- Agent 6 P4 and Agent 5 F2: `CareerPersistenceBehavior.SyncData` seeds its four refs from the live
  singleton, the same key-miss leak this change removed here (a save from 2026-04-07 to 2026-06-01 has
  no `_taom_careerFlags`), plus the same no-record hole. Behaviour-changing.
- Agent 6 P5: one shared `TAOM.Tests/Infrastructure/FakeDataStore.cs` for the three hand-rolled
  `IDataStore` fakes (`CareerPersistenceTests.cs:197`, `RacePersistenceServiceTests.cs:822`, this file).
- Agent 6 P6: delete the unused `ClampAll` from the storage interface and its two tests.
- Agent 1 FU1: `SpecialResourcesBehavior.cs` is about 570 lines against ADR-002's 150; a lifecycle service
  (the `f4273639` Career precedent) would also host a named `ResetForNewSession()` (F9).
- Agent 1 FU3: `ResetSessionState` runs only on a new game, not on load (bounded, Agent 5 T8).
- Agent 4: the feature doc says "8 events" (`RegisterEvents` subscribes 12 `CampaignEvents` plus
  `ScreenManager.OnPushScreen`), and its Tests list omits `SpecialResourcesBehaviorPhaseGuardTests.cs`.
- `plans/README.md:39` still reads PARTIAL for plan 001; the orchestrator maintains the index.

## CODEX REVIEW

Codex gpt-6-astra, reasoning ultra, read-only from git objects
(`docs/reviews/raw/codex-adversarial-001-cross-campaign-singleton-resets-2026-09-24.md`, gitignored;
prompt `docs/reviews/codex-adversarial-001-cross-campaign-singleton-resets-2026-09-24.prompt.md`).
Quality: it quoted the v1.5.3 code for every engine assumption (`BehaviorSaveData.SyncData`,
`LoadBehaviorData`, the new-game dispatch order, `FinalizeCharacterCreationState`), ran a config
cross-reference, and walked a nine-row scenario matrix. Verdict: no introduced P1 or P2 defect.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 (pre-existing follow-up) | MEDIUM, pre-existing | Yes | Re-read `LoadBehaviorData:86-106` and `OnGameLoaded:171-190`; same as F1. Fix is behaviour-changing, so NEEDS MIKE; the known limitation is now written down |
| 2 | P3 | LOW | Yes | `castar` vs `caster` confirmed at `special_resources_config.xml:40`; fixed (F6) |
| 3 | P3 (plan text) | LOW | Yes | Plan lines 229 and 276 build `Main/TAOM.csproj` for a test-project RED; recorded (F8) |

**Known suspects:** agreed with all ten dispositions. KS2 (two failed verifications) and KS5 (an
NSubstitute attempt) stay UNVERIFIED, as Codex said: committed source cannot show them.

**Confirmed bugs:** F6 (fixed), F8 (plan text, recorded). **False positives:** none. **Design
questions:** F1's load-path reset. **Things Codex missed:** F3 (the "every lord" scope claim; its config
table checked ids, not the prose), F7 (it judged the fake's load side only and called it matching), and
the CHANGELOG overclaim F2 only as a side note rather than a finding.

**Root cause (Phase 3e) for the Codex-confirmed bugs:**

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 2 | Fixture id `castar` for Gondor's resource | Config ID mismatch | The executor typed the display name it saw in docs and CHANGELOG prose; storage keys are opaque, so no assertion could fail | Lesson in `lessons/testing-qa.md` (fixture ids come from the shipped config's `id`) |
| 3 | Plan's RED step builds the wrong project | Other: plan procedure error | The plan author reasoned "the method is missing, so the build fails" without asking which project holds the test | Lesson in `lessons/misc.md` (a RED step names the test project and the test filter) |

**AGENTS.md lessons (pending, Phase 3h consolidated later):**

- Bugs Codex typically misses: a CHANGELOG sentence naming WHO holds state ("every lord") is not checked
  against the writers' keys; a test double's save side is not compared with the engine store.
- What Codex does well: separating an engine "no record" path from a "missing key" path by quoting
  `LoadBehaviorData`, and catching a display name used as a content id through its config
  cross-reference.
- False positives: none new.

VERDICT: READY FOR COMMIT. The two NEEDS MIKE items (F1's load-path fix, F4's issue) are recorded, not
silently deferred; the final full suite is green (10318 passed, 2 skipped, 0 failed).

RCA: `docs/reviews/rca-cross-campaign-singleton-resets-2026-09-24.md`.
