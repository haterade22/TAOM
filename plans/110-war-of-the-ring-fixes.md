# Plan 110: Fix the War of the Ring: real Full War declarations, per-campaign reset, settings migration, honest victory

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. The plan has three Parts (A, B, C). Each lands on its own; do them in
> order (one Part per session is fine), all on the same branch and worktree. Do not edit
> `plans/README.md`: every WP branch shares it, so the integrator records this plan's status and
> test counts when merging. Put the numbers listed under "Hand-off report" in your final message.
>
> **Drift check (run first, Step A0.3)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Diplomacy Main/Features/WarOfTheRingMomentum Main/Adapters/AllianceAdapter.cs Main/Adapters/IAllianceAdapter.cs Main/Adapters/WarEventSnapshotAdapter.cs Main/Features/TaomSettings.cs Main/SubModule.cs Main/Features/CareerSystem/Quests/CareerQuest.cs Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs Main/Features/LotrIssues/Templates/CombatLotrIssue.cs Main/Features/AiPartySize Main/Features/BanditManagement/BanditScalingSettingsProvider.cs Main/Features/CompanionTactics Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs TAOM.Tests/Features/Diplomacy TAOM.Tests/Features/WarOfTheRingMomentum Main/_Module/ModuleData/taom_wotr_strings.xml`
> Earlier WPs change `Main/SubModule.cs` (WP104, WP105) and may touch other listed paths. Compare
> the live files with "Current state" and STOP only on a real mismatch (an anchor a step names is
> missing, duplicated or different).

## Status

- **Priority**: P1
- **Effort**: L (Part A: six ported commits; Part B: one adapted port plus a new gate; Part C: six small new-work commits)
- **Risk**: MED. Every change is save-compatible, but the Full War transition now declares real
  wars (vanilla then asks non-Hostile permanent allies to join), 13 renamed options reset any value
  a player customised, and the momentum behavior moves from a container singleton to a per-campaign `new`.
- **Depends on**: WP103 (`plans/103-executor-safety-harness.md`: working commit gates) and WP104
  (`plans/104-guarded-patch-categories.md`: `TAOM.Tests/Infrastructure/RepoPaths.cs`). Wave 0
  (WP101 to WP105) is merged before any wave 1 WP.
- **Category**: bug
- **Wave / lane**: wave 1, lane E
- **Executor**: laptop
- **Decisions applied**: D1 (no step stages `CHANGELOG.md`; the commit body is the changelog
  entry), D2 (the War of the Ring has no 1.5.x feature module, so it stays hand-wired in
  `SubModule.cs`), D7 (co-op stays out: `ec2a3026f` is adapted without `IPlayerControlQuery`),
  D8 (subjects carry `v2.0.28`), D9 (no CI: every gate runs locally), D10 (ordinary session),
  D19 (git only through the Bash tool), D20 (step C4 edits ModuleData string files, so it needs
  the WP101 validator baseline)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`; 2026-11-04
- **Issue**: Port of #764, #772, #792, #1015 and #1017. Part C fixes the momentum half of #773 and all of
  #774 (both open, no fix on 1.5.x). The rest of Part C (WOTR-03, WOTR-04, WOTR-06/10 diagnostics,
  WOTR-08) is new work with no issue: orchestrator creates the issue before implementation lands.

## Why this matters

A player reported that the War of the Ring "ended for me after 4 Days ... no battle was fought and
the indicator stayed in its starting position and caused a win for the Free Kingdoms". On 1.4.x the
only Free-win display is the "End of the War" inquiry, which needs the MCM option "Enable Victory
(End the War)" (default off since `601a55c57`). With victory on, a Free decision with the bar centred
needs an empty Evil roster or an absolute momentum lead the ratio bar never shows (verify report V06).
Around that, the code carries real defects that 1.5.x already fixed: Full War declares none of its
Hostile-pair wars, so the stored stances stay at peace and AI target lists stay stale until the next
cache refresh (WOTR-01, #772); a second campaign in one game process inherits Full War or
`WarEnded` (WOTR-02, #764); 13 MCM settings, the War of the Ring phase days among them, keep a value
saved before their default changed, so a `TAOM.json` saved on v2.0.13 or earlier still runs Phase 1
at day 2 and Full War at day 14 while the log prints 30/44 (WOTR-06, #1017); a corrupt momentum chunk
count throws during load (WOTR-09); a relief-force win outside the walls scores a capture (WOTR-07).
Part C closes the paths 1.5.x left open: an empty roster deciding the war, a non-finite strength
deciding it, a record-less load inheriting another campaign's momentum, a raid scored twice, and it
makes the deciding log line and the popup say what actually decided the war.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one (wave 0
  at least, WP103 and WP104 required).
- Worktree: `C:\Users\mikew\source\repos\taom-port\110`, branch `port148/110-war-of-the-ring-fixes`.
  The folder is outside the session's working directory, so `cd` does not persist between Bash
  calls: start EVERY command with `cd /c/Users/mikew/source/repos/taom-port/110 &&`.
- Python: `python` (if Git Bash cannot find it, use `/c/Python314/python.exe`).
- Commit message files live outside the worktree in `C:/Users/mikew/source/repos/taom-port/`.

### Files and roles

| File (1.4.x) | Role in this WP |
|---|---|
| `Main/Features/Diplomacy/WarOfTheRingService.cs` | Phase machine (process singleton). A1 adds `ResetForNewSession`, A3 the stance guard and `ReconcileDeclaredWars`, C5 `DescribePhaseDays` |
| `Main/Features/Diplomacy/WarOfTheRingBehavior.cs` | Per-campaign behavior (`new` in SubModule). A1 resets the service in its constructor, A3 calls the repair on host session launch, C5 extends the launch log line |
| `Main/Features/Diplomacy/IWarOfTheRingService.cs`, `WarOfTheRingConfigProvider.cs`, `TaomSettingsProvider.cs` | Interface; JSON validation of the `wars` lists (A3); MCM phase-day read (B) |
| `Main/Adapters/AllianceAdapter.cs`, `IAllianceAdapter.cs` | `HasDeclaredWar` and the stance-guarded `DeclareWar` (A3) |
| `Main/Adapters/WarEventSnapshotAdapter.cs` | Snapshot DTOs for momentum. A5 side rule for sieges, C6 `VillageLooted` |
| `Main/Features/WarOfTheRingMomentum/*` | Momentum services, behavior, store, popup VM. A4, A5, C1 to C4, C6 |
| `Main/Core/Persistence/SyncDataChunker.cs` (new, A4) | Guarded count-and-chunks SyncData pass, replaces `MomentumSyncChunker.cs` |
| `Main/Features/CareerSystem/Quests/CareerQuest.cs`, `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs`, `Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs` | Siege and capture credit (A5; the last one is a comment only) |
| `Main/Features/TaomSettings.cs` plus the providers listed in Part B | 13 MCM renames (B) and three hint texts (C4) |
| `Main/SubModule.cs` (single-owner) | A1 comment, B comment, C3 replaces the momentum behavior registration |
| `Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs` | C3 deletes the behavior's container registration |
| `tools/check_mcm_default_flips.py`, `tools/mcm_default_flips_allowlist.json`, `tools/tests/test_check_mcm_default_flips.py` (new, B) | Gate: an MCM default changed under an unchanged name |
| `.ai/review-reference.md`, `.ai/verification.md`, `.ai/routing.json` | Two reviewer-calibration bullets (A1, A3); the `mcm-defaults` check (B) |
| `.claude/rules/csharp-architecture.md`, `.claude/skills/release/SKILL.md` | Rule text documenting the mechanisms ported (A2, B); one release check line (B) |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, read this session)

`Main/Features/Diplomacy/WarOfTheRingService.cs:128-178` (WOTR-01): `TransitionToPhase` sets
`CurrentPhase = newPhase` (`:131`) before `DeclareConfiguredWars` and `DeclareHostileTierWars`, and
both guard each declaration on `_allianceAdapter.AreAtWar` (`:152`, `:170-171`), which in Full War
answers true for every Hostile pair. The 1.4.x diplomacy production files equal `2719e59e8`'s parent
(`git diff 2719e59e8^` shows only test-file differences), so A1 and A3 apply cleanly.
`Main/Features/Diplomacy/WarOfTheRingBehavior.cs:23-40` (WOTR-02): the constructor resets nothing;
an `OnNewGameCreatedEvent` listener zeroes only the behavior's own two ints, never the singleton service.

`Main/SubModule.cs:978-985` (inside `RegisterDiplomacyAndConflict`, called once per campaign from
`OnGameStart` at `:727`):

```csharp
        campaignStarter.AddModel(new TaomDiplomacyModel(wotrService, coopSession));

        var wotrLogger = IoC.Resolve<IModLogger>();
        campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger,
            IoC.Resolve<Features.CoopInterop.ICoopSessionProvider>()));
        // WotR Momentum #327 ... behavior is a
        // Reuse.Singleton (it carries the state store's persistence dict).
        campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());
```

`Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs`: `:81` resets the store only on
`OnNewGameCreatedEvent`; `:109-117` passes the saved count to `new List<string>(count)` (WOTR-09);
`:258` filters sieges on `isWin` alone (WOTR-07). `WarOfTheRingMomentumIoC.cs:24`:
`container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton);` (WOTR-05).

`Main/Features/WarOfTheRingMomentum/MomentumVictoryService.cs:52-64` (WOTR-03; the log line players send):

```csharp
        float internalMomentum = state.InternalMomentum;
        int threshold = _settings.VictoryThreshold;

        var outcome = WarOutcome.None;
        if (internalMomentum >= threshold || state.Evil.KingdomIds.Count == 0)
            outcome = WarOutcome.FreeVictory;
        else if (internalMomentum <= -threshold || state.Free.KingdomIds.Count == 0)
            outcome = WarOutcome.EvilVictory;
        ...
        _logger.LogInfo($"[Momentum] War of the Ring decided: {outcome} (momentum {internalMomentum:F0}, threshold {threshold})");
```

`MomentumEnrollmentService.cs:66` starts the war when EITHER side holds a realm.
`MomentumEventService.cs:197-206` (WOTR-08): `if (loserSideStrength == 0f) return 0;` then
`(int)Math.Round(percentageLost * ...)`. `MomentumWarState.cs:24`:
`InternalMomentum => (Free.SideMomentum - Evil.SideMomentum) / (float)MomentumScale` (int subtraction).
`MomentumEventService.cs:111-126` scores a raid on `raid.AttackerVictory` alone (#774).
`MomentumQueryService.cs:40-47`: the bar is a ratio of the positive parts.

`Main/Adapters/WarEventSnapshotAdapter.cs:80-108` (`FromSiege`): `PlayerInvolved =
IsPlayerRelated(capturerParty?.Party, ...) || (_enlistment.IsEnlisted && _enlistment.IsCommanderParty(capturerParty?.StringId))`.
The adapter is registered by type (`WarOfTheRingMomentumIoC.cs:21`), so a constructor change needs no IoC edit.

`Main/Features/TaomSettings.cs` (13 old names, 1.4.x line): `AiLordPartySizeFactor` 59,
`AiLordPartySizeFlatBonus` 64, `AiGarrisonSizeFactor` 69, `AiFoodConsumptionRelief` 74,
`AiWageRelief` 79, `Phase1TriggerDay` 202 (= 30), `Phase2TriggerDay` 207 (= 44),
`FieldCommissionMaxOffersPerBattle` 539 (= 2), `FieldCommissionMeritThreshold` 554 (= 32),
`EnableSmartCavalryAI` 717 (= false), `EnableFormationPresets` 786 (= false),
`BanditMaxPartiesPerHideout` 852 (= 6), `BanditInitialHideoutsPerFaction` 857 (= 7).
`ChargeAutoKnockdownWeightRatio` is 8 at 1042 and was 8 at every 1.4.x tag: not renamed.
Momentum hints: 233 (threshold), 243 (require participation), 248 (minimum events).

### Engine facts the code binds (installed v1.4.8 via `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, read this session)

| Member | Signature / behaviour (file:line in the taom-src cache) |
|---|---|
| `Kingdom.IsAtWarWith` | `public bool IsAtWarWith(IFaction other)` returns `FactionManager.IsAtWarAgainstFaction(this, other)` (`Kingdom.cs:652-655`) |
| `Kingdom.GetStanceWith` | `public StanceLink GetStanceWith(IFaction other)` returns `FactionManager.Instance.GetStanceLinkInternal(this, other)` (`Kingdom.cs:662-665`) |
| `Kingdom.IsEliminated`, `CurrentTotalStrength` | `public bool IsEliminated => _isEliminated;` (`:258`); `public float CurrentTotalStrength` sums its clans (`:222-233`) |
| `StanceLink.IsAtWar` | `public bool IsAtWar => _stanceType == StanceType.War;` (`StanceLink.cs:59`) |
| `FactionManager.IsAtWarAgainstFaction` | returns true on `DiplomacyModel.IsAtConstantWar` before reading any stance (`FactionManager.cs:144-160`) |
| `FactionManager.GetStanceLinkInternal` | creates a missing link typed from `GetDefaultDiplomaticStance` and stores it (`:78-87`) |
| `DefaultDiplomacyModel.GetDefaultDiplomaticStance` | War whenever `IsAtConstantWar` (`DefaultDiplomacyModel.cs:1082-1089`); `TaomDiplomacyModel.IsAtConstantWar` is true for Hostile pairs in Full War (1.4.x `TaomDiplomacyModel.cs:26-36`) |
| `DeclareWarAction.ApplyByDefault` | `public static void ApplyByDefault(IFaction faction1, IFaction faction2)` (`DeclareWarAction.cs:62`); `ApplyInternal` (`:21-55`) raises `OnWarDeclared`, no map-event continuity update |
| `AllianceCampaignBehavior.OnWarDeclared` | calls each ally with `!item.IsAtWarWith(enemy)` into the war; a player kingdom gets a prompt (`AllianceCampaignBehavior.cs:685-710`) |
| `CampaignEvents.SiegeCompletedEvent` | `IMbEvent<Settlement, MobileParty, bool, MapEvent.BattleTypes>` (`CampaignEvents.cs:995`) |
| `MapEvent.BattleTypes` | `None, FieldBattle, Raid, IsForcingVolunteers, IsForcingSupplies, Siege, Hideout, SallyOut, SiegeOutside, BlockadeBattle, BlockadeSallyOutBattle` (`MapEvent.cs:24-37`) |
| `KingdomManager.SiegeCompleted` | returns unless (`SallyOut`, `Siege` or `BlockadeSallyOutBattle`) and `isWin` (`KingdomManager.cs:238-243`) |
| `MapEvent.FinalizeEventAux` | raises `SiegeCompleted` at `:2093` (assault or siege-outside attacker win) and `:2115` (sally-out defender win), before `sides[i].HandleMapEventEnd()` at `:2158`, which sets `party.MapEventSide = null` (`MapEventSide.cs:434`) |
| `PartyBase` | `public MapEventSide MapEventSide` (`PartyBase.cs:289`); `public MobileParty MobileParty { get; private set; }` (`:122`); `MainParty` getter `Campaign.Current == null ? null : Campaign.Current.MainParty.Party` (`:208-218`) |
| `MobileParty` | `public PartyBase Party { get; private set; }` (`MobileParty.cs:354`); `public bool IsMainParty => this == MainParty;` with `MainParty => Campaign.Current.MainParty` (`:1078`, `:256`): throws with no campaign, so test seams pass the player party in |
| `MapEventSide`, `MapEventParty`, `MapEvent`, `Settlement` | `_battleParties` (`MapEventSide.cs:43`), `_mapEvent` (`:58`), `Parties => _battleParties` (`:96`); `MapEventParty.Party { get; private set; }` (`MapEventParty.cs:43`); `MapEvent._sides` (`MapEvent.cs:61`); `Settlement.Party { get; private set; }` (`Settlement.cs:97`), `public Village Village;` (`:86`) |
| `MapEvent.CaptureDefeatedPartyMembers` | picks the winning party that receives a prisoner by chance, then `TakePrisonerAction.Apply` (`MapEvent.cs:1954-1993`) |
| `RaidEventComponent.OnBeforeFinalize` | sets the village Looted (hit points at most 1e-5) or Normal, then raises `RaidCompleted` with the attacker as winner on `AttackerVictory` whether or not it looted (`RaidEventComponent.cs:128-138`); `Village.VillageStates` includes `Looted` (`Village.cs:16-22`), `VillageState` (`:51`) |
| `IDataStore` | `bool IsSaving { get; }`, `bool IsLoading { get; }`, `bool SyncData<T>(string key, ref T data);` |
| `CampaignBehaviorDataStore` | `BehaviorSaveData.SyncData` adds on save (throws on a duplicate key), returns false and leaves the ref on a missing key (`CampaignBehaviorDataStore.cs:26-39`); `LoadBehaviorData` calls `SyncData` only when the save holds a record (`:86-105`) |
| Campaign start order | `Campaign.OnInitialize` calls `GameManager.OnGameStart` (`Campaign.cs:1391`); a load then runs `InitializeCampaignBehaviors`, `LoadBehaviorData`, `RegisterEvents` (`:1427-1430`); a new game dispatches `OnNewGameCreated` then `OnSessionStart` (`:1694-1695`); `OnSessionStart` (`:727-745`) runs session-launch listeners at `:729`, before `MapEventManager.OnAfterLoad` (`:740`) and `KingdomManager.RegisterEvents` (`:742`) |
| `MbEvent.AddNonSerializedListener` | head-inserts; `Invoke` walks from the head, so listeners run newest-first (`MbEvent.cs:24-44`) |
| `SaveContext.SaveStringTo` | writes `(short)GetStringSizeInBytes(text)`, which is `4 + UTF-8 bytes`, so one string holds at most 32,763 UTF-8 bytes (`SaveContext.cs:308-321`) |
| `TextObject.SetTextVariable` | `(string tag, int variable)` overload exists (`TextObject.cs:346`) |
| `CampaignBehaviorBase` | not `IDisposable`; parameterless constructor sets `StringId = GetType().Name` |

### Cherry-pick simulation (this session, `git merge-tree --write-tree --name-only --no-messages`)

Each commit alone onto `1b00d1acd`: `2719e59e8` conflicts only in `.ai/review-reference.md` and
bookkeeping; `9ab3952a7` clean; `d1b9fd91a` conflicts in `IWarOfTheRingService.cs`,
`WarOfTheRingBehavior.cs` and `WarOfTheRingBehaviorSessionResetTests.cs` (all from `2719e59e8`) plus
bookkeeping; `16a34027d` conflicts only in the four WarChronicle paths (absent on 1.4.x);
`ec2a3026f` conflicts in `Main/Adapters/WarEventSnapshotAdapter.cs` and the momentum doc;
`cf7486cba` conflicts in three feature docs; `429713746` conflicts in 22 paths.
Chained in this plan's order (A1 to A6, then 429713746), every code conflict except
`WarEventSnapshotAdapter.cs` disappears: `d1b9fd91a` then conflicts only in bookkeeping and
`.ai/review-reference.md`, `cf7486cba` only in `docs/features/war-of-the-ring-momentum.md`.
Part B does not cherry-pick: `429713746` is re-implemented by hand (its providers differ in shape
on 1.4.x and four of its test files do not exist here).

### Not on 1.4.x yet (git grep / ls-tree this session)

No `ResetForNewSession`, `ReconcileDeclaredWars` or `HasDeclaredWar`; no `Main/Core/Persistence`;
no `TAOM.Tests/Infrastructure/EngineDataStore.cs`; no `WarOfTheRingWiringTests.cs` or
`WarOfTheRingBehaviorSessionResetTests.cs`; no `tools/check_mcm_default_flips.py`; no
`docs/ai-includes/orientation.md`, `docs/features/server-settings.md`,
`TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs`,
`TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs`,
`TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs` or
`TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs`. The War Chronicle feature does not
exist on 1.4.x (no `WarChronicle` text anywhere in `Main` or `TAOM.Tests`).
Present: `FiniteFloatValidator` (`Main/Core/Validation/FiniteFloatValidator.cs`),
`FlushArmyLeaveAfterBattle` (#557, `ServiceBattleService.cs:261`), `RoundTripDataStore`
(`RacePersistenceServiceTests.cs:723`), `BreakdownVM(string label, string freeValue, string evilValue)`
(`UI/BreakdownVM.cs:39`). Both csproj files are SDK-style and glob source files, so new `.cs` files
need no project edit.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types; engine reads stay in `Main/Adapters`.
- ADR-002 thin entry points (under 150 lines, delegate to services); ADR-003 no `#region`;
  ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: Part C writes the failing test first (RED), then the fix (GREEN). Ported commits bring their
  own tests.
- MCM json2 rule: MCM loads a saved value by property name over the compiled default, so rename a
  persisted setting to change its default; never flip a default in place (Part B is this rule).
- `.claude/rules/csharp-architecture.md`: NaN must fail an engine-float gate (write the gate as a
  positive requirement); validate finiteness at every float to int cast, one NaN test per cast;
  singletons holding per-campaign state reset in the constructor of a behavior built with `new` per
  campaign (the A2 text).
- `.claude/rules/harmony-patches.md`: no Harmony patch is added or changed here.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/110 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print if WP104 did not silence it; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "<expr>"` | as each step states |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure against Step A0.6 |
| Gate | `python tools/check_mcm_default_flips.py` | as Part B states |
| Data | `python tools/validate_moduledata.py > C:/Users/mikew/source/repos/taom-port/110-validate.txt 2>&1` | no new ERROR against the WP101 baseline (C4 only) |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step A0.7's values; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game
install. Never `./build.ps1`. Never two builds at once. Always build before a `--no-build` test run.

## Scope

**In scope** (create or modify only these):

- Part A: `Main/Features/Diplomacy/{IWarOfTheRingService,WarOfTheRingBehavior,WarOfTheRingService,WarOfTheRingConfigProvider}.cs`,
  `Main/Adapters/{AllianceAdapter,IAllianceAdapter,WarEventSnapshotAdapter}.cs`,
  `Main/Core/Persistence/SyncDataChunker.cs` (new), `Main/Features/WarOfTheRingMomentum/{WarOfTheRingMomentumBehavior,MomentumSyncChunker}.cs`
  (the second is deleted), `Main/Features/WarOfTheRingMomentum/Cheats/MomentumCheats.cs`,
  `Main/Features/CareerSystem/Quests/CareerQuest.cs`, `Main/Features/LotrIssues/Templates/CombatLotrIssue.cs`,
  `Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs` (comment), the test files each step
  names, `TAOM.Tests/Infrastructure/EngineDataStore.cs` (new), `.ai/review-reference.md`,
  `.claude/rules/csharp-architecture.md`, `docs/features/{diplomacy,war-of-the-ring,war-of-the-ring-momentum,coop-interop,enlistment}.md`,
  `docs/reviews/rca-wotr-session-reset-2026-10-08.md` and `docs/reviews/rca-wotr-phase2-wars-2026-10-08.md` (new).
- Part B: `Main/Features/TaomSettings.cs`, the call sites in Step B3, the tests in Step B4,
  `TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs` (new), the three `tools/` files (new),
  `.ai/verification.md`, `.ai/routing.json`, `.claude/rules/csharp-architecture.md`,
  `.claude/skills/release/SKILL.md` (one line, see Step B5), the docs in Step B6.
- Part C: the momentum and diplomacy files and tests each step names,
  `Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs`,
  `Main/_Module/ModuleData/taom_wotr_strings.xml` and the 12
  `Main/_Module/ModuleData/Languages/*/std_taom_wotr_strings_*.xml` files (one row each).
- Single-owner, this WP edits three regions only: `Main/SubModule.cs` (A1 comment above
  `new WarOfTheRingBehavior(`; B comment `EnableFormationPresets` at about `:1162`; C3 the
  momentum behavior registration). Other wave 1 WPs edit other regions in their own worktrees.
  `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`: no edit.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/lessons/*`, `docs/reviews/REVIEW-LOG.md`, `docs/reviews/codex-*.md`,
`docs/reviews/agents-md-review-lessons-archive.md`, `docs/INDEX.md`,
`docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144 sync these); `.claude/settings*.json`,
`Directory.Build.props`, `docs/adrs/**` (maintainer only); `plans/README.md` (integrator); every
WarChronicle, Arthedain, ServerSettings or co-op (`IPlayerControlQuery`) file; the
`ChargeAutoKnockdownWeightRatio` setting; `MomentumEnrollmentService.ResolveSide` (no WP changes it: WP134 skips the `5e43518df` refactor of it as behaviour-equivalent on 1.4.x);
`AiPartySizeSettingsWatcherTests.cs` (its `"AiLordPartySizeFactor"` string is an arbitrary
non-matching property name, leave it); any balance number.

## Git workflow

- Create the worktree in Step A0.1; work and commit only there, on `port148/110-war-of-the-ring-fixes`.
  Never push, open a PR, tag, or merge into `bannerlord-1.4.5`. Run git only through the Bash tool (D19).
- Per-port procedure: apply a 1.5.x commit with `git cherry-pick -n <sha>` (never let cherry-pick,
  `--continue`, `-C`, `-c` or `--amend` create a commit). Then:
  - a conflicted bookkeeping path: `git checkout --ours <path> && git add -- <path>`;
  - a cleanly added bookkeeping file: `git rm -f -q -- <path>`;
  - a cleanly modified bookkeeping file: `git checkout HEAD <path>`;
  - a modify/delete conflict on a path 1.4.x does not have (`DU` in `git status`): `git rm -f -q -- <path>`.
  Bookkeeping here means `CHANGELOG.md`, `docs/reviews/lessons/*`, `docs/reviews/REVIEW-LOG.md`,
  `docs/reviews/agents-md-review-lessons-archive.md`, `docs/reviews/codex-track-record.md`,
  `docs/reviews/codex-adversarial-*.prompt.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`
  and `tools/README.md`. Kept on purpose: feature docs, the two War of the Ring RCAs, `.ai/*`, and
  `.claude/rules/csharp-architecture.md` lines that document the mechanism ported.
- After resolving, `git diff --cached --name-status` must equal the step's expected list. Stage
  any extra edit with an explicit `git add -- <paths>` (never `-A`, `-u` or `.`).
- Write each commit message with the Write tool to the file the step names, then run
  `git commit -F C:/Users/mikew/source/repos/taom-port/<file>` in the NEXT sequential Bash call
  (never the same call, never a parallel call). Never `--no-verify`. If a hook denies a commit,
  STOP and report its message verbatim. After each commit, `git status --porcelain` must be empty,
  and plain `git status` must not say a cherry-pick is in progress (if it does, run
  `git cherry-pick --quit`, which forgets the sequencer state and leaves files alone).
- Commit message template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72
  characters; a body for 1.4.x players and developers, wrapped at 72, no long dashes, no
  `Co-Authored-By` or "Generated with" line; then trailers `Research:`, `Not-tested:`,
  `Save-compat:`, `Constraint:` as needed, then one `Ported-from: <40-hex sha>` per included
  1.5.x commit. Each step gives the subject and the body.

## Steps

### Part A: port the 1.5.x War of the Ring fixes

#### Step A0: worktree, prerequisites, baseline

**A0.1 Worktree.** If the worktree already exists from an earlier session, skip to A0.2 and check
that `git -C /c/Users/mikew/source/repos/taom-port/110 branch --show-current` prints the branch.

```bash
mkdir -p /c/Users/mikew/source/repos/taom-port && cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/110 -b port148/110-war-of-the-ring-fixes bannerlord-1.4.5
```

**Verify**: prints `Preparing worktree (new branch 'port148/110-war-of-the-ring-fixes')`.

**A0.2 Prerequisites.**

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string StripComments(string source)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -c 'ResetForNewSession' Main/Features/Diplomacy/IWarOfTheRingService.cs; \
grep -c 'HasDeclaredWar' Main/Adapters/IAllianceAdapter.cs; \
ls -d Main/Core/Persistence TAOM.Tests/Infrastructure/EngineDataStore.cs TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs tools/check_mcm_default_flips.py 2>&1 | grep -c 'No such file'; \
test -f Main/Features/WarOfTheRingMomentum/MomentumSyncChunker.cs && echo chunker-present
```

**Verify**: `1`, `1`, `1`, `0`, `0`, `4`, `chunker-present`. Anything else: STOP (WP104 missing, or
part of this WP already landed).

**A0.3 Drift check**: the command in the header. Read every listed change; continue unless a step's
anchor (A0.4) is affected.

**A0.4 Anchors.** Each must print `1`.

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && for a in \
 'campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger,' \
 'campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());' \
 'unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.' \
 ; do printf '%s  %s\n' "$(grep -cF "$a" Main/SubModule.cs)" "$a"; done; \
grep -cF 'container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton);' Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs; \
grep -cF 'if (internalMomentum >= threshold || state.Evil.KingdomIds.Count == 0)' Main/Features/WarOfTheRingMomentum/MomentumVictoryService.cs; \
grep -cF '_chunks = new List<string>(count);' Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs
```

**Verify**: six lines, each `1`. Otherwise STOP.

**A0.5 Baseline.** Run Restore, Build, Test. **Verify**: build exit 0, `Failed: 0`. Record
`Total`, `Passed`, `Skipped` as **T0** (the WP101 baseline was 9154 total, 9151 passed, 3 skipped;
WP102 to WP105 added tests, so T0 is higher). Any failure: STOP.

**A0.6 Tool tests baseline.** Run Tool tests; record the `Ran N tests` line and any failures as **P0**.

**A0.7 Docs baseline.** Run Docs; record `dead_links` and `orphan_features`.

#### Step A1: port `2719e59e8` (reset the War of the Ring per campaign, #764, WOTR-02)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n 2719e59e8c81648a0b5cc15364a8548f755c00ce; echo "exit=$?"; git status --porcelain
```

Expected conflicts (`UU`): `.ai/review-reference.md`, `docs/reviews/REVIEW-LOG.md`,
`docs/reviews/agents-md-review-lessons-archive.md`, `docs/reviews/codex-track-record.md`,
`docs/reviews/lessons/state-lifecycle-save.md`, `docs/reviews/lessons/testing-qa.md`. All code
applies cleanly (the SubModule hunk lands at an offset). A conflict in any other path: STOP.

Resolve:
1. The five bookkeeping conflicts: `git checkout --ours <path> && git add -- <path>` each.
2. `git rm -f -q -- docs/reviews/codex-adversarial-wotr-session-reset-2026-10-08.prompt.md`.
3. `.ai/review-reference.md`: `git checkout --ours .ai/review-reference.md`, then insert this
   bullet directly below the line that starts `**What Codex does especially well` (about line 44),
   then `git add -- .ai/review-reference.md`:

```markdown
- **Mutation-checks a new guard in memory** (2026-10-08, War of the Ring session reset #764, 1 of 1
  MEDIUM real): it rewrote the guarded line into a cached construction as a string and showed every
  predicate of the new source-text pin still held. For a new wiring or source-text test, ask for the
  cheapest regression that keeps it green.
```

4. 1.4.x accuracy edits, then `git add` each file:
   - `TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs`: in the `FakeDataStore`
     summary replace `(v1.5.4 BehaviorSaveData.SyncData)` with
     `(v1.4.8 CampaignBehaviorDataStore.BehaviorSaveData.SyncData, CampaignBehaviorDataStore.cs:26-39)`.
   - `TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs`: in the class summary delete
     ` and also run in the reference-assembly build` so the line ends `RequiresGame tag.` (1.4.x has
     no reference-assembly build, D9).
   - `docs/features/war-of-the-ring.md`: replace `(untagged, so the reference-assembly build runs them)`
     with `(untagged)`.
   - `docs/reviews/rca-wotr-session-reset-2026-10-08.md`: insert after the title line:

```markdown
> **1.4.x port note (WP110, 2026-11-04).** Ported with `2719e59e8` and `d1b9fd91a`. The engine line
> numbers below are the 1.5.x review's v1.5.4 citations. The same facts on the installed v1.4.8:
> listeners run newest-first (`MbEvent.cs:24-44`); `Campaign.OnInitialize` calls `OnGameStart`
> (`Campaign.cs:1391`) before a load's `LoadBehaviorData` (`:1429`); a new game dispatches
> `OnNewGameCreated` then `OnSessionStart` (`:1694-1695`); `LoadBehaviorData` calls `SyncData`
> only when the save holds a record (`CampaignBehaviorDataStore.cs:86-105`). The War Chronicle
> work this record mentions does not exist on 1.4.x.
```

**Expected staged set** (`git diff --cached --name-status`): `M .ai/review-reference.md`,
`M Main/Features/Diplomacy/IWarOfTheRingService.cs`, `M Main/Features/Diplomacy/WarOfTheRingBehavior.cs`,
`M Main/Features/Diplomacy/WarOfTheRingService.cs`, `M Main/SubModule.cs`,
`A TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs`,
`M TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs`,
`A TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs`, `M docs/features/diplomacy.md`,
`M docs/features/war-of-the-ring.md`, `A docs/reviews/rca-wotr-session-reset-2026-10-08.md`.

**Verify**: `grep -cF '#764: must stay' Main/SubModule.cs` prints `1`; Build exit 0; Filtered
`--filter "FullyQualifiedName~WarOfTheRing|FullyQualifiedName~CoopAuthorityGate"` passes with
`WarOfTheRingBehaviorSessionResetTests` (6), `WarOfTheRingWiringTests` (7) and 34 in
`WarOfTheRingServiceTests`.

Commit message file `msg-110-A1.txt`:

```
fix(wotr): v2.0.28 - reset the War of the Ring per campaign

Starting a new campaign without restarting the game no longer carries
the previous campaign's War of the Ring into it. Before, a campaign
started after one that had reached the full war began in the full war
on day 0: it never declared the Isengard war or the full war, and its
momentum war started at once. After a war that had ended, the new
campaign never got a war at all (#764).

The War of the Ring state lives for the whole game process. Its
campaign behavior is built fresh for every campaign and now resets the
state in its constructor, before any campaign event can read it; a
loaded save then restores its own phase. A save already written by an
affected campaign keeps the phase it saved.

Research: v1.4.8 Campaign.cs:1391 OnGameStart precedes LoadBehaviorData
Research: v1.4.8 Campaign.cs:1694-1695; MbEvent.cs:24-44 newest-first
Not-tested: a second campaign in one game process (in-game check 1)
Ported-from: 2719e59e8c81648a0b5cc15364a8548f755c00ce
```

#### Step A2: port `9ab3952a7` (rule: reset per-campaign state in the constructor)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n 9ab3952a7d6298044e09f3ab61b9feb598c253e7; echo "exit=$?"; git diff --cached --name-status
```

**Verify**: exit 0; staged set is exactly `M .claude/rules/csharp-architecture.md`; this prints `1`:

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && grep -cF 'builds with `new` in every campaign' .claude/rules/csharp-architecture.md
```

(Kept despite the `.claude/` drop rule: it is the rule text for the mechanism A1 ports.)

Commit message file `msg-110-A2.txt`:

```
docs(rules): v2.0.28 - reset per-campaign state in the constructor

The session-reset rule for process-lifetime singletons now says to
reset in the constructor of a behavior built fresh for every campaign,
never in OnSessionLaunched, OnNewGameCreated or RegisterEvents, and to
pin the per-campaign construction with a source-text test. The
OnSessionLaunched reset it used to prescribe ran too late for the War
of the Ring service (#764): campaign-event listeners run newest-first.

Ported-from: 9ab3952a7d6298044e09f3ab61b9feb598c253e7
```

#### Step A3: port `d1b9fd91a` (declare the Full War wars for real, #772, WOTR-01)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n d1b9fd91ac32dd2dce5facc0567c577772f4acc6; echo "exit=$?"; git status --porcelain
```

Expected conflicts: `.ai/review-reference.md`, `docs/reviews/REVIEW-LOG.md`,
`docs/reviews/agents-md-review-lessons-archive.md`, `docs/reviews/codex-track-record.md`,
`docs/reviews/lessons/gamemodels-services.md`, `docs/reviews/lessons/state-lifecycle-save.md`,
`docs/reviews/lessons/testing-qa.md`. A conflict in any code or test path: STOP (it means A1 did
not land as planned).

Resolve:
1. The six bookkeeping conflicts: `git checkout --ours` and `git add` each.
2. `git rm -f -q -- docs/reviews/codex-adversarial-wotr-phase2-wars-2026-10-08.prompt.md`.
3. `.ai/review-reference.md`: `git checkout --ours`, insert directly ABOVE the A1 bullet
   (`- **Mutation-checks a new guard in memory**`), then `git add`:

```markdown
- **Deletes and reorders a call the unit tests cannot reach** (2026-10-08, War of the Ring Phase 2 wars
  #772, 3 of 3 findings real): handed a host-only handler that reads `Campaign.Current`, it ran the real
  fixtures in memory, then removed `ReconcileDeclaredWars()` and moved it after the phase check; both
  mutants kept all six behavior tests green. For a handler with a client gate, ask for the deletion and
  reorder mutants of the host call.
```

4. `docs/features/war-of-the-ring.md` (1.4.x accuracy), then `git add`:
   - replace `The Phase 1 pairs (Isengard and Dunland against Rohan, Gundabad against Arthedain)` with
     `The Phase 1 pairs (Isengard and Dunland against Rohan)`;
   - replace `on a build without the fix (v2.0.34 has the old code)` with
     `on a build without the fix (v2.0.28 and earlier have the old code)`;
   - replace `(a kingdom aggressiveness increase that nothing in v1.5.4 reads)` with
     `(a kingdom aggressiveness increase that nothing in v1.4.8 reads outside its own updates, KingdomManager.cs:94-121 and AllianceCampaignBehavior.cs:645)`.
5. `docs/reviews/rca-wotr-phase2-wars-2026-10-08.md`: insert after the title line, then `git add`:

```markdown
> **1.4.x port note (WP110, 2026-11-04).** Ported with `d1b9fd91a`. The engine line numbers below are
> the 1.5.x review's v1.5.4 citations. On the installed v1.4.8: `FactionManager.IsAtWarAgainstFaction`
> asks `IsAtConstantWar` first (`FactionManager.cs:144-160`); `GetStanceLinkInternal` creates a missing
> link typed from `GetDefaultDiplomaticStance` (`:78-87`, `DefaultDiplomacyModel.cs:1082-1089`);
> `AllianceCampaignBehavior.OnWarDeclared` calls allies in (`AllianceCampaignBehavior.cs:685-710`);
> `Campaign.OnSessionStart` runs session-launch listeners (`Campaign.cs:729`) before
> `KingdomManager.RegisterEvents` (`:742`) and `CampaignInformationManager.RegisterEvents` (`:744`).
> The War Chronicle (#765) and Arthedain do not exist on 1.4.x.
```

**Expected staged set**: `M .ai/review-reference.md`, `M Main/Adapters/AllianceAdapter.cs`,
`M Main/Adapters/IAllianceAdapter.cs`, `M Main/Features/Diplomacy/IWarOfTheRingService.cs`,
`M Main/Features/Diplomacy/WarOfTheRingBehavior.cs`, `M Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs`,
`M Main/Features/Diplomacy/WarOfTheRingService.cs`, `M TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`,
`M TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs`,
`M TAOM.Tests/Features/Diplomacy/WarOfTheRingConfigProviderTests.cs`,
`M TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs`, `M docs/features/coop-interop.md`,
`M docs/features/diplomacy.md`, `M docs/features/war-of-the-ring.md`,
`A docs/reviews/rca-wotr-phase2-wars-2026-10-08.md`, `M docs/reviews/rca-wotr-session-reset-2026-10-08.md`.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~WarOfTheRing|FullyQualifiedName~CoopVeto|FullyQualifiedName~CoopAuthorityGate"`
passes, with 58 in `WarOfTheRingServiceTests`, 8 in `WarOfTheRingBehaviorSessionResetTests`, 18 in
`WarOfTheRingConfigProviderTests`; `grep -c 'HasDeclaredWar' Main/Adapters/IAllianceAdapter.cs` is at least 1.

Commit message file `msg-110-A3.txt`:

```
fix(wotr): v2.0.28 - declare the Full War wars for real

When the War of the Ring reaches the full war (day 44 by default),
every pair of kingdoms set as Hostile now really goes to war: each war
is declared, the war notice and the history entry appear, and each
kingdom's list of enemies is rebuilt at once (#772). Before, no
Hostile-pair war was ever declared. Peace stayed blocked, but the
stored stances stayed at peace, so AI armies could miss the new enemies
as siege and raid targets until the next load.

Each declaration first asked whether the two kingdoms were already at
war, and TAOM's diplomacy model answers "at war" for every Hostile pair
once the full war starts, so every declaration was skipped. The
declarations now check the stored stance instead. Saves already in the
full war are repaired when they load: on the host, every Hostile-pair
or scripted Phase 2 war still at peace in its stored stance is
declared once. Scripted war lists in war_of_the_ring.json now drop
empty and self pairs and name an unknown kingdom id with a warning.

Research: v1.4.8 FactionManager.cs:144-160 asks IsAtConstantWar first
Research: v1.4.8 FactionManager.cs:78-87; Kingdom.GetStanceWith :662
Research: v1.4.8 AllianceCampaignBehavior.OnWarDeclared :685-710
Not-tested: Full War transition and old-save repair (in-game checks 2-3)
Ported-from: d1b9fd91ac32dd2dce5facc0567c577772f4acc6
```

#### Step A4: port `16a34027d`, momentum half (guarded SyncData chunk loop, #792, WOTR-09, SAVE-TIME-06)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n 16a34027dcd60e51baa9bacb47342eb582aa844f; echo "exit=$?"; git status --porcelain
```

Expected: four `DU` paths (modified there, absent here): `Main/Features/WarChronicle/WarChronicleBehavior.cs`,
`Main/Features/WarChronicle/WarChronicleStateService.cs`,
`TAOM.Tests/Features/WarChronicle/Events/ChroniclePersistenceTests.cs`,
`TAOM.Tests/Features/WarChronicle/WarChronicleStateServiceTests.cs`. Anything else conflicted: STOP.

Resolve:
1. `git rm -f -q --` the four WarChronicle paths.
2. `git checkout HEAD docs/reviews/lessons/gamemodels-services.md` (bookkeeping).
3. Create the shared test fake (from `ea8f2ad52`'s content; WP122 owns the conversions of other tests):
   `git show 16a34027dcd60e51baa9bacb47342eb582aa844f:TAOM.Tests/Infrastructure/EngineDataStore.cs > TAOM.Tests/Infrastructure/EngineDataStore.cs`,
   then replace `(v1.5.5, decompile` with `(v1.4.8, decompile` (the cited lines 26 to 39, 28 to 32
   and 33 to 38 are the same in v1.4.8). `git add -- TAOM.Tests/Infrastructure/EngineDataStore.cs`.
4. 1.4.x accuracy edits (each `old` text becomes its `new` text), then `git add` each file:

```text
Main/Core/Persistence/SyncDataChunker.cs, class summary (old spans two /// lines):
  old: Used by the War of the Ring momentum and the War
       /// Chronicle behaviors.
  new: Used by the War of the Ring momentum behavior.
  old: v1.5.5 <c>SaveContext.SaveStringTo</c>
  new: v1.4.8 <c>SaveContext.SaveStringTo</c>, SaveContext.cs:308-321
Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs, the CountKey field comment:
  old: v1.5.5        (the word ending the line before "SaveContext.SaveStringTo)")
  new: v1.4.8
TAOM.Tests/Core/Persistence/SyncDataChunkerSyncTests.cs:
  old: The one count-and-chunks SyncData loop that the momentum and War Chronicle behaviors share:
  new: The count-and-chunks SyncData loop the momentum behavior uses:
TAOM.Tests/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehaviorSyncDataTests.cs:
  old: warning and the state loads fresh, as the War Chronicle's does.
  new: warning and the state loads fresh.
docs/features/war-of-the-ring-momentum.md (two occurrences of the first pair):
  old: v1.5.5 `SaveContext.SaveStringTo`
  new: v1.4.8 `SaveContext.SaveStringTo` (`SaveContext.cs:308-321`)
  old: `Main/Core/Persistence/SyncDataChunker.cs` (shared with the War Chronicle)
  new: `Main/Core/Persistence/SyncDataChunker.cs`
```

**Expected staged set**: `A Main/Core/Persistence/SyncDataChunker.cs`,
`M Main/Features/WarOfTheRingMomentum/Cheats/MomentumCheats.cs`,
`D Main/Features/WarOfTheRingMomentum/MomentumSyncChunker.cs`,
`M Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs`,
`A TAOM.Tests/Core/Persistence/SyncDataChunkerSyncTests.cs`,
`R TAOM.Tests/Features/WarOfTheRingMomentum/MomentumSyncChunkerTests.cs -> TAOM.Tests/Core/Persistence/SyncDataChunkerTests.cs`
(or a `D` plus `A` pair), `M TAOM.Tests/Features/WarOfTheRingMomentum/MomentumCheatsFormatTests.cs`,
`A TAOM.Tests/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehaviorSyncDataTests.cs`,
`A TAOM.Tests/Infrastructure/EngineDataStore.cs`, `M docs/features/war-of-the-ring-momentum.md`.

**Verify**: `git grep -n -E "WarChronicle|War Chronicle|MomentumSyncChunker" -- Main TAOM.Tests` prints
nothing; Build exit 0; Filtered `--filter "FullyQualifiedName~SyncDataChunker|FullyQualifiedName~Momentum"`
passes (`SyncDataChunkerTests` 7, `SyncDataChunkerSyncTests` 7, `WarOfTheRingMomentumBehaviorSyncDataTests` 4).

Commit message file `msg-110-A4.txt`:

```
refactor(core): v2.0.28 - one guarded SyncData chunk loop for momentum

The War of the Ring momentum save passed its saved chunk count straight
to new List<string>(count), so a corrupt save threw on load (a negative
count) or ran out of memory (an absurd one). The chunker moves to
Core/Persistence as SyncDataChunker, and SyncChunks does the whole
pass: a count below 0 or above 4,096 is ignored with a warning and the
momentum loads fresh. The saved keys are unchanged, so existing saves
load as before, and a save from before the _v3 keys still loads fresh
momentum without a warning. Comments now give the engine's real string
limit, 32,763 UTF-8 bytes.

Research: v1.4.8 SaveContext.cs:308-321 writes (short)(4 + UTF-8 bytes)
Research: v1.4.8 CampaignBehaviorDataStore.cs:26-39 for EngineDataStore
Save-compat: keys _taom_wotr_momentum_v3_count and _v3_<i> unchanged
Ported-from: 16a34027dcd60e51baa9bacb47342eb582aa844f
Ported-from: ea8f2ad52d3a8bb0cfac3ddc9119b4b8e780bf5c
```

#### Step A5: port `ec2a3026f` adapted (siege and capture credit follow the player, #1015, WOTR-07)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n ec2a3026f7391467c0ea0f19de6b4a0af59f4cda; echo "exit=$?"; git status --porcelain
```

Expected conflicts: `Main/Adapters/WarEventSnapshotAdapter.cs`, `docs/features/war-of-the-ring-momentum.md`.
Clean: `CareerQuest.cs`, `EnlistmentBattleBehavior.cs`, `CombatLotrIssue.cs`,
`WarOfTheRingMomentumBehavior.cs`, the two new test files, `docs/features/enlistment.md`, and the
review record `docs/reviews/rca-enlistment-followups-siege-credit-2026-10-26.md`. Any other conflict: STOP.

Resolve:
1. `git rm -f -q -- docs/reviews/rca-enlistment-followups-siege-credit-2026-10-26.md` (a 1.5.x review record).
2. `Main/Adapters/WarEventSnapshotAdapter.cs`: `git checkout --ours Main/Adapters/WarEventSnapshotAdapter.cs`
   (the 1.4.x file; 1.5.x's version needs `IPlayerControlQuery`, which D7 keeps out), then edit by hand:
   - delete the field `private readonly Features.Enlistment.IEnlistmentStateQuery _enlistment;`, the
     constructor parameter `Features.Enlistment.IEnlistmentStateQuery enlistment,` and the
     assignment `_enlistment = enlistment;`;
   - in `FromSiege`, replace the eight-line comment that starts `// Same participation test as FromMapEvent above`
     and the `PlayerInvolved = ...` expression below it with:

```csharp
                PlayerInvolved = IsPlayerRelated(capturerParty?.Party, _playerContext.GetPlayerKingdomId())
                                 || PlayerFought(capturerParty, Campaign.Current?.MainParty?.Party),
```

   - insert this method between the end of `FromSiege` and `public RaidOutcomeSnapshot FromRaid(`:

```csharp
    // #1015: the capturer is the leader of the side that won (AttackerSide.LeaderParty after an assault,
    // DefenderSide.LeaderParty after a failed sally-out), so credit follows the player's own party being
    // on that side, whatever it is led by. The sides are still populated here: v1.4.8 raises
    // SiegeCompleted inside MapEvent.FinalizeEventAux (MapEvent.cs:2093 and :2115) before
    // MapEventSide.HandleMapEventEnd clears each party's side (MapEvent.cs:2158, MapEventSide.cs:434).
    // 1.4.x has no co-op player-control query, so the player's party is the local main party, passed in
    // so the rule is testable without a campaign (MobileParty.IsMainParty throws without one).
    internal static bool PlayerFought(MobileParty capturerParty, PartyBase player)
    {
        var playerSide = player?.MapEventSide;
        return playerSide != null && playerSide == capturerParty?.Party?.MapEventSide;
    }
```

   Then `git add -- Main/Adapters/WarEventSnapshotAdapter.cs`.
3. `TAOM.Tests/Features/Enlistment/AttributionAudit/EnlistedSiegeCreditTests.cs` (adapt to the
   1.4.x seam), then `git add`:
   - delete the `_control` and `_adapter` fields and the whole `Setup` method;
   - in `SoldierParty`, delete the line `_control.OfParty(soldier).Returns(PlayerControl.Player);` and
     make the method `private static`;
   - in the five `Adapter_*` tests, replace `_adapter.PlayerFought(<party>)` with
     `WarEventSnapshotAdapter.PlayerFought(<party>, <player base>)`, where `<player base>` is the
     test's `soldierBase` or `playerBase`; where a test calls `SoldierParty(out _);` without keeping
     the result, change it to `var soldierBase = SoldierParty(out _);` (or `playerBase`) and pass it;
   - in the class summary, replace the v1.5.5 sentence and the last sentence (text below);
   - remove `using TAOM.Features.CoopInterop;` if nothing else in the file uses it.

```text
old (four /// lines): SiegeCompleted fires inside
     SiegeAssaultEventComponent.OnBeforeMapEventFinalize (:248), which MapEvent.FinalizeEventAux
     (:1738) runs before it clears the sides (:1750), so the sides are still populated when the
     adapter and the quest read them. Each test stages bare engine objects the way
     RefugeCampaignBehaviorTests.BattleOf does.
new: On v1.4.8 SiegeCompleted is raised inside MapEvent.FinalizeEventAux (MapEvent.cs:2093 and
     :2115) before MapEventSide.HandleMapEventEnd clears the sides (:2158), so the sides are still
     populated when the adapter and the quest read them. Each test stages bare engine objects
     through reflection.
```

4. `docs/features/war-of-the-ring-momentum.md`: `git checkout --ours` the file, then in the
   paragraph that starts `Sieges used to be narrower.` make this replacement and `git add`:

```text
old: Sieges now use the identical `IsPlayerRelated` test as battles.
new: Sieges now use the identical `IsPlayerRelated` test as battles, plus a side rule (#1015): the player is involved when his own party is on the capturing side of the siege's map event (`WarEventSnapshotAdapter.PlayerFought`), enlisted or not. The kingdom clause still applies, so a vassal is credited for any capture by his own realm. Only `Siege`, `SallyOut` and `BlockadeSallyOutBattle` completions count as captures, the set `KingdomManager.SiegeCompleted` transfers on (v1.4.8 `KingdomManager.cs:240`); a `SiegeOutside` win (a defender joining from outside the walls) captures nothing and scores nothing.
```

5. `docs/features/enlistment.md` (the old text wraps across two lines; keep the surrounding words), then `git add`:

```text
old: (`SiegeCompleted` comes from `OnBeforeMapEventFinalize`, before `MapEvent.FinalizeEventAux` clears them)
new: (v1.4.8 raises `SiegeCompleted` inside `MapEvent.FinalizeEventAux`, `MapEvent.cs:2093` and `:2115`, before `MapEventSide.HandleMapEventEnd` clears them at `:2158`)
```

**Expected staged set**: `M Main/Adapters/WarEventSnapshotAdapter.cs`,
`M Main/Features/CareerSystem/Quests/CareerQuest.cs`, `M Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs`,
`M Main/Features/LotrIssues/Templates/CombatLotrIssue.cs`,
`M Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs`,
`A TAOM.Tests/Features/Enlistment/AttributionAudit/EnlistedSiegeCreditTests.cs`,
`A TAOM.Tests/Features/WarOfTheRingMomentum/WarOfTheRingMomentumSiegeGateTests.cs`,
`M docs/features/enlistment.md`, `M docs/features/war-of-the-ring-momentum.md`.

**Verify**: `git grep -n -E "IPlayerControlQuery|_enlistment" -- Main/Adapters/WarEventSnapshotAdapter.cs TAOM.Tests/Features/Enlistment/AttributionAudit`
prints nothing; Build exit 0; Filtered `--filter "FullyQualifiedName~EnlistedSiegeCredit|FullyQualifiedName~WarOfTheRingMomentumSiegeGate|FullyQualifiedName~Career|FullyQualifiedName~LotrIssue|FullyQualifiedName~Momentum"`
passes (`EnlistedSiegeCreditTests` 19, `WarOfTheRingMomentumSiegeGateTests` 5).

Commit message file `msg-110-A5.txt`:

```
fix(career): v2.0.28 - siege and capture credit follow the player

The career quest credited a siege only when the player's own party, or
an enlisted soldier's commander, led the attack. A player fighting
inside another lord's army got nothing for a town he helped take, and
an enlisted soldier who sat a siege out still got credit when his
commander led it. It now credits the player when his party fought on
the side that took the town, whoever led it. War momentum adds the same
side rule to its realm test, so a vassal is still credited for a
capture by his own realm.

The career objective and the issue quest for capturing enemy lords now
count every lord captured by the side the player fought on: the game
hands a captured lord to a winning party chosen by chance. War momentum
also counted a win by defenders who joined a siege from outside the
walls as a captured town; the game takes no town there, and momentum
no longer counts one (#1015).

Constraint: no co-op player-control query on 1.4.x; the side rule reads
Constraint: the local main party's map event side, passed in
Research: v1.4.8 KingdomManager.cs:238-243; MapEvent.cs:2093,2115,2158
Research: v1.4.8 MapEvent.CaptureDefeatedPartyMembers :1954-1993
Not-tested: enlisted and army siege credit end to end (in-game check 5)
Ported-from: ec2a3026f7391467c0ea0f19de6b4a0af59f4cda
```

#### Step A6: port `cf7486cba` partially (#764 and #772 in the feature docs)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git cherry-pick -n cf7486cba5999c88324bc2ba5ee609e7758896b1; echo "exit=$?"; git status --porcelain
```

Expected conflict: `docs/features/war-of-the-ring-momentum.md` only. Resolve:
1. `git checkout --ours docs/features/war-of-the-ring-momentum.md`, make this replacement, and
   `git add` (leave its key count, test count and Status line as they are):

```text
old: A corrupt/absent count loads as fresh state (logged);
new: A count below 0 or above 4,096 is ignored with a warning and loads fresh state; an absent count (a save from before `_v3`) loads fresh without a warning;
```

2. `docs/features/diplomacy.md`: replace `Phase 1 (Isengard and Dunland attack Rohan, Gundabad attacks Arthedain) triggers on day 30`
   with `Phase 1 (Isengard and Dunland attack Rohan) triggers on day 30`; delete the table row that
   names `WarOfTheRingPhaseDayTests.cs`. `git add`.
3. `docs/features/war-of-the-ring.md` (remove 1.5.x-only content), then `git add`:
   - `Isengard and Dunland declare war on Rohan, and Gundabad on Arthedain. No peace blocking yet.` becomes
     `Isengard and Dunland declare war on Rohan. No peace blocking yet.`;
   - delete the list item `- **Gundabad** → **Arthedain** (arthedain)`;
   - `opening salvo of the War of the Ring, and the Arthedain pair opens the northern front.` becomes
     `opening salvo of the War of the Ring.`;
   - delete the bullet that names `WarOfTheRingPhaseDayTests.cs`;
   - `check that Rohan is at war with Isengard and Dunland, and Arthedain with Gundabad` becomes
     `check that Rohan is at war with Isengard and Dunland`;
   - in the new `## GitHub Issue` section delete the last sentence of the Issues bullet (it starts
     `#765 added` and names `GetEffectivePhaseDay`).

**Expected staged set**: `M docs/features/diplomacy.md`, `M docs/features/war-of-the-ring.md`,
`M docs/features/war-of-the-ring-momentum.md`.

**Verify**: `git grep -n -i -E "arthedain|GetEffectivePhaseDay([^s]|$)|PhaseDayTests" -- docs/features/diplomacy.md docs/features/war-of-the-ring.md docs/features/war-of-the-ring-momentum.md`
prints nothing (the plural `GetEffectivePhaseDays` is a real 1.4.x method and its three doc lines stay);
Docs run shows `ai_dashes: 0`.

Commit message file `msg-110-A6.txt`:

```
docs(wotr): v2.0.28 - #764 and #772 in diplomacy and momentum docs

The diplomacy, War of the Ring and momentum feature docs now describe
the per-campaign reset (#764), the stored-stance guard and the load
repair (#772), the four War of the Ring phases including WarEnded, the
skipped scripted war that names an unknown or eliminated kingdom, and
the momentum load of an absent chunk count without a warning.

Ported-from: cf7486cba5999c88324bc2ba5ee609e7758896b1
```

#### Step A7: Part A gate

Run Build, Test, Docs. **Verify**: `Failed: 0`; `Total` = T0 + 83 (WarOfTheRingServiceTests +27,
WarOfTheRingBehaviorSessionResetTests +8, WarOfTheRingWiringTests +7, WarOfTheRingConfigProviderTests
+6, SyncDataChunkerSyncTests +7, WarOfTheRingMomentumBehaviorSyncDataTests +4,
WarOfTheRingMomentumSiegeGateTests +5, EnlistedSiegeCreditTests +19; the chunker tests move, net 0);
docs values as A0.7 and `ai_dashes: 0`; `git log --format=%s bannerlord-1.4.5..HEAD` lists the six
Part A subjects. Record **T_A**. A different total: report the per-class counts in the hand-off.

### Part B: settings migration (adapted port of `429713746`, #1017, WOTR-06)

#### Step B0: baseline

Run Build and Test in the worktree. **Verify**: `Failed: 0`. Record **T_A** if this is a new session.

#### Step B1: add the gate and see it fail on 1.4.x (RED for the gate)

```bash
cd /c/Users/mikew/source/repos/taom-port/110 && git show 42971374650eee9546589d0177ae852597199f95:tools/check_mcm_default_flips.py > tools/check_mcm_default_flips.py && git show 42971374650eee9546589d0177ae852597199f95:tools/tests/test_check_mcm_default_flips.py > tools/tests/test_check_mcm_default_flips.py
```

Write `tools/mcm_default_flips_allowlist.json` with the Write tool, exactly (the four 1.4.x flips
that are safe to leave as saved; each verified this session against the 1.4.x tags):

```json
{
  "TaomSettings.CultureConversionHoldDays": {
    "from": "45",
    "to": "1",
    "reason": "b8a7c9cb9 (#333, first tag v2.0.13) moved the default 45 to 1; 45 was only ever saved on v2.0.7 to v2.0.10. A rename would convert every pending fief at once at the next check, so those few saved values are left alone."
  },
  "TaomSettings.EnableEnlistmentDiagnostics": {
    "from": "false",
    "to": "true",
    "reason": "39b0b3ce7 (v2.0.24) turned the enlistment trace on temporarily; a value saved on v2.0.20 to v2.0.22 keeps it off, which is the intended shipping state. WP123 ports 890fe29de, which renames the setting and ships it off again: delete this entry then, the gate reports it as stale."
  },
  "TaomSettings.EnableNativeSkinFixes": {
    "from": "true",
    "to": "false",
    "reason": "NativeSkinFixes is parked and disabled at the wiring level: the install branch in SubModule.OnSubModuleLoad is commented out, so nothing reads the setting and a value saved on v2.0.9 or v2.0.10 does nothing."
  },
  "TaomSettings.EnableShieldPenetration": {
    "from": "true",
    "to": "false",
    "reason": "ae57fba08 (#320, v2.0.24) turned javelin shield penetration off with empty grant lists in combat_mechanics_config.json (shieldPenetration.weaponClasses and itemIds are []), so a saved true grants nothing."
  }
}
```

**Verify**:
- `python -m unittest tools.tests.test_check_mcm_default_flips` prints `Ran 32 tests` and `OK`.
- `python tools/check_mcm_default_flips.py; echo "exit=$?"` prints four `ALLOWED` lines (the four
  keys above), exactly 13 `FLIP` lines for `TaomSettings.` + `AiLordPartySizeFactor`,
  `AiLordPartySizeFlatBonus`, `AiGarrisonSizeFactor`, `AiFoodConsumptionRelief`, `AiWageRelief`,
  `Phase1TriggerDay`, `Phase2TriggerDay`, `FieldCommissionMaxOffersPerBattle`,
  `FieldCommissionMeritThreshold`, `EnableSmartCavalryAI`, `EnableFormationPresets`,
  `BanditMaxPartiesPerHideout`, `BanditInitialHideoutsPerFaction`, the line
  `compared <N> settings against 19 release tags`, and `exit=1`.
- Any other `FLIP`, any `UNPARSED` or `STALE-ALLOWLIST` line, `exit=2`, or a tag count other than 19: STOP.

#### Step B2: the rename test (RED)

Create `TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs` from
`git show 42971374650eee9546589d0177ae852597199f95:TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs`
with the row `new object[] { "ChargeAutoKnockdownWeightRatio", "ChargeAutoKnockdownRatio", 6 },`
deleted (13 rows remain; 1.4.x never flipped that default).

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~McmRenamedDefaults"` FAILS with
`still exists` and `missing` for the 13 rows.

#### Step B3: rename the 13 settings (GREEN)

In `Main/Features/TaomSettings.cs`, for each row: rename the property (default unchanged), add the
two-line comment above its `[SettingPropertyGroup(...)]` line, and append
` A value saved by an earlier TAOM version was reset to this default; if you had chosen your own, set it again.`
to the end of its `HintText` string. Model (the War of the Ring pair):

```csharp
    // Renamed from Phase1TriggerDay (#1017): its default went 2 to 30 in e273cd23f (v2.0.15) under the same name,
    // and MCM loads the saved value by name, so a TAOM.json saved on v2.0.13 or earlier kept 2.
    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1, RequireRestart = false,
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan. Default 30. A value saved by an earlier TAOM version was reset to this default; if you had chosen your own, set it again.")]
    public int WarPhase1TriggerDay { get; set; } = 30;
```

For the eleven numeric renames the comment is
`// Renamed from <Old> (#1017): its default went <X> to <Y> in <Commit> under the same name,`
`// and MCM loads the saved value by name, so <Who> kept <X>.` (verified this session against every
1.4.x tag):

| Old name | New name | X to Y | Commit (first tag) | Who kept the old value |
|---|---|---|---|---|
| `AiLordPartySizeFactor` | `AiLordPartyScale` | 5 to 1 | cd5a6f5e8 (v2.0.26) | an install that saved settings on v2.0.24 or v2.0.25 |
| `AiLordPartySizeFlatBonus` | `AiLordPartyFlatBonus` | 150 to 0 | cd5a6f5e8 (v2.0.26) | the same |
| `AiGarrisonSizeFactor` | `AiGarrisonScale` | 3 to 1 | cd5a6f5e8 (v2.0.26) | the same |
| `AiFoodConsumptionRelief` | `AiPartyFoodRelief` | 0.9 to 0 | cd5a6f5e8 (v2.0.26) | the same |
| `AiWageRelief` | `AiPartyWageRelief` | 0.9 to 0 | cd5a6f5e8 (v2.0.26) | the same |
| `Phase1TriggerDay` | `WarPhase1TriggerDay` | 2 to 30 | e273cd23f (v2.0.15) | a TAOM.json saved on v2.0.13 or earlier |
| `Phase2TriggerDay` | `WarPhase2TriggerDay` | 14 to 44 | e273cd23f (v2.0.15) | the same |
| `FieldCommissionMaxOffersPerBattle` | `FieldCommissionOffersPerBattle` | 1 to 2 | 14160f0a4 (v2.0.21) | a TAOM.json saved on v2.0.20 |
| `FieldCommissionMeritThreshold` | `FieldCommissionMeritToPromote` | 8 to 32 | 14160f0a4 (v2.0.21) | the same |
| `BanditMaxPartiesPerHideout` | `BanditPartiesPerHideoutCap` | 3 to 6 | 2652fe2f1 (#559, after the v2.0.28 tag) | a TAOM.json saved on v2.0.4 to v2.0.28 |
| `BanditInitialHideoutsPerFaction` | `BanditStartingHideoutsPerFaction` | 14 to 7 | 2652fe2f1 (#559, after the v2.0.28 tag) | the same |

The two switches:

```text
EnableSmartCavalryAI -> EnableSmartCavalryCharge:
  // Renamed from EnableSmartCavalryAI (#1017): it was switched off in 625396dde (v2.0.7) while war-elephant
  // interaction is tuned, but a saved "on" (v2.0.0 to v2.0.5) survived under the same name.
EnableFormationPresets -> EnableOobFormationPresets:
  // Renamed from EnableFormationPresets (#1017): it was switched off in 53a8cfc21 (v2.0.8) while loading a
  // preset is unwired, but a saved "on" (v2.0.0 to v2.0.7) survived under the same name.
```

For the two bandit settings, REPLACE (do not append to) the old tails: delete
`Played an earlier build? MCM has already saved its default of 3 in your TAOM.json, so reset this group to defaults to pick up 6.`
and `Earlier builds shipped 14, up to 100 hideouts on a fresh map against vanilla's 35; if you played one, MCM has already saved 14 in your TAOM.json and only a reset of this group to defaults picks up 7.`,
then append the suffix. Keep every other hint word as it is on 1.4.x.

Call sites (1.4.x line numbers; match by text):

| File | Change |
|---|---|
| `Main/Features/AiPartySize/AiPartySizeService.cs` 135, 136, 160, 171, 182 | `AiLordPartySizeFactor`, `AiLordPartySizeFlatBonus`, `AiGarrisonSizeFactor`, `AiFoodConsumptionRelief`, `AiWageRelief` to the new names |
| `Main/Features/AiPartySize/Cheats/AiPartySizeCheats.cs` 47 | `AiFoodConsumptionRelief` to `AiPartyFoodRelief` |
| `Main/Features/Diplomacy/TaomSettingsProvider.cs` 7, 8 | `TaomSettings.Instance?.Phase1TriggerDay` to `?.WarPhase1TriggerDay`, same for Phase 2. The provider members `Phase1TriggerDay` and `Phase2TriggerDay` (and `ITaomSettingsProvider`) keep their names |
| `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs` 121, 123 | `FieldCommissionMeritThreshold`, `FieldCommissionMaxOffersPerBattle` to the new names |
| `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs` 8 | `EnableSmartCavalryAI` to `EnableSmartCavalryCharge` |
| `Main/Features/CompanionTactics/ICompanionTacticsSettingsProvider.cs` 13 and `CompanionTacticsSettingsProvider.cs` 14 | member `EnableFormationPresets` to `EnableOobFormationPresets`, and its `TaomSettings.Instance?.EnableFormationPresets` read |
| `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs` 70 and `FormationPresets/Hooks/Patch35_Mission_OnTick.cs` 31 | `_settings.EnableFormationPresets` to `_settings.EnableOobFormationPresets` |
| `Main/Features/BanditManagement/BanditScalingSettingsProvider.cs` 26 (comment), 44, 47 | `BanditMaxPartiesPerHideout` to `BanditPartiesPerHideoutCap`, `BanditInitialHideoutsPerFaction` to `BanditStartingHideoutsPerFaction` |
| `Main/SubModule.cs` about 1162 (comment) | `EnableFormationPresets` to `EnableOobFormationPresets` |

**Verify**: Build exit 0 (the compiler proves no stale reference in `Main`).

#### Step B4: update the existing tests

| File (1.4.x line) | Change |
|---|---|
| `TAOM.Tests/Features/AiPartySize/AiPartySizeServiceTests.cs` 556, 561, 581, 601, 602 | the five AI party member accesses to the new names |
| `TAOM.Tests/Features/BanditManagement/BanditScalingSettingsProviderTests.cs` 42, 43, 52, 53 | bandit names |
| `TAOM.Tests/Features/BanditManagement/TaomBanditDensityModelTests.cs` 59, 69 | `BanditMaxPartiesPerHideout` to `BanditPartiesPerHideoutCap` |
| `TAOM.Tests/Features/CompanionTactics/FormationPresets/FormationPresetServiceTests.cs` 23 | `_settings.EnableOobFormationPresets.Returns(true);` |
| `TAOM.Tests/Features/FieldCommission/FieldCommissionSettingsProviderTests.cs` 349, 351 | the source-text patterns `"FieldCommissionMeritToPromote { get; set; } = 32;"` and `"FieldCommissionOffersPerBattle { get; set; } = 2;"` |
| `TAOM.Tests/Features/TroopWeight/PartyUpgraderShedGuardTests.cs` 13 (comment) | `AiGarrisonSizeFactor` to `AiGarrisonScale` |

**Verify**: Build exit 0; Filtered
`--filter "FullyQualifiedName~McmRenamedDefaults|FullyQualifiedName~AiPartySize|FullyQualifiedName~BanditScaling|FullyQualifiedName~TaomBanditDensity|FullyQualifiedName~FormationPreset|FullyQualifiedName~FieldCommissionSettingsProvider|FullyQualifiedName~PartyUpgraderShedGuard|FullyQualifiedName~WarOfTheRing|FullyQualifiedName~SettingsFingerprint"`
passes; `python tools/check_mcm_default_flips.py; echo "exit=$?"` prints the four `ALLOWED` lines,
`OK: no MCM default changed under an unchanged name` and `exit=0`.

#### Step B5: harness wiring

- `.ai/verification.md`: insert row R1 (below) directly below the `managed-tests` row.
- `.ai/routing.json`: append `"mcm-defaults"` to the `checks` arrays of the `managed` lane and the
  `tooling` lane (keep the 1.4.x patterns as they are).
- `.claude/rules/csharp-architecture.md` (the rule text for the mechanism this Part ports): in the
  `**Doc requirement:**` paragraph, replace the sentence that starts `When a compiled MCM default CHANGES`
  and ends `second occurrence on one group).` with text R2.
- `.claude/skills/release/SKILL.md`: add paragraph R3 at the end of the section that runs `/verify`
  before the version bump (on 1.4.x the `## Phase 2` section). This one `.claude/` line is kept
  deliberately: the gate this Part adds must run before a tag, and no other WP owns `429713746`
  (WP143 reconciles the skill). If WP102 left no such verify section, skip R3 and say so in the hand-off.

```text
R1: | `mcm-defaults` | `python tools/check_mcm_default_flips.py`: exit 1 when an MCM setting's default changed under an unchanged name since any release tag reachable from HEAD (rename it, or allowlist `Class.Name` with from, to and reason; a stale entry fails). |
R2: Never flip a compiled MCM default under an unchanged name: MCM persists per property and loads the saved value over the default, so the new value would reach fresh installs only. Rename the setting (comment why) and the new default reaches every player; `python tools/check_mcm_default_flips.py` checks it (#1017). The renamed setting's `HintText` tells the player their saved value was reset to the new default and to set their own again (RCA `docs/reviews/rca-bandit-scaling-mcm-2026-09-11.md`).
R3: Then `python tools/check_mcm_default_flips.py`. It must exit 0: a changed MCM default under an unchanged name reaches no player who saved settings (rename it, or allowlist it with a reason).
```

**Verify**: `python -c "import json;d=json.load(open('.ai/routing.json'));print([l['checks'] for l in d['lanes'] if l['id'] in ('managed','tooling')])"`
shows `mcm-defaults` in both lists; Tool tests: no new failure against P0 (32 more tests).

#### Step B6: feature docs

Plain renames (old name to new name, nothing else): `docs/features/field-commission.md` 178, 181;
`docs/features/companion-tactics.md` 184; `docs/features/smart-cavalry-ai.md` 161;
`docs/modding/balance-levers.md` 248 (the five AI party names). Leave the 2026-06-21 changelog line
in `companion-tactics.md` (about line 211) as history. The rest, as `old` to `new` (an `old` that
wraps across lines in the file is matched by its words; write each `new` as ONE line):

```text
docs/features/mcm.md 102-106, the whole paragraph:
  old: **A moved compiled default reaches fresh `TAOM.json` files only.** ... the tooltip is what the player reads.
  new: **Never flip a compiled default; rename the setting.** MCM persists per property and loads the file over the compiled default from then on, so a default moved under an unchanged name reaches fresh installs only. Rename the setting (comment why) and the new default reaches every player; the gate `python tools/check_mcm_default_flips.py` fails on a flip (#1017), unless it is allowlisted with a reason. The renamed setting's `HintText` tells the player their saved value was reset and to set their own again (`.claude/rules/csharp-architecture.md`, "Doc requirement"); the commit body is for us, the tooltip is what the player reads.
docs/features/diplomacy.md 169:
  old: MCM overrides `phase1TriggerDay` and `phase2TriggerDay` at runtime and carries the same defaults.
  new: MCM overrides these at runtime through `WarPhase1TriggerDay` and `WarPhase2TriggerDay` (renamed in #1017 so the 30 and 44 reach saved settings) and carries the same defaults.
docs/features/ai-party-size.md 155-157:
  old: the key is absent from its settings json, so every install that ran v2.0.24 or earlier still has `AiFoodConsumptionRelief = 0.9` persisted and is running the pre-rework composition until this ships. Going neutral in v2.0.26 fixed fresh installs only.
  new: the key is absent from its settings json, so every install that ran v2.0.24 or earlier kept `AiFoodConsumptionRelief = 0.9` persisted and ran the pre-rework composition. Going neutral in v2.0.26 fixed fresh installs only; #1017 then renamed the five numeric settings (`AiLordPartyScale`, `AiLordPartyFlatBonus`, `AiGarrisonScale`, `AiPartyFoodRelief`, `AiPartyWageRelief`) so the neutral defaults reach every install.
docs/features/ai-party-size.md 206-207:
  old: `AiLordPartySizeFactor` and `AiLordPartySizeFlatBonus`. Existing players keep the old values until they move the sliders themselves.
  new: `AiLordPartySizeFactor` and `AiLordPartySizeFlatBonus`. Those players kept the old values until #1017 renamed the settings (`AiLordPartyScale`, `AiLordPartyFlatBonus`), which reset them.
docs/features/bandit-management.md 67:
  old: `BanditInitialHideoutsPerFaction` (default 7 since #559; 14 before)
  new: `BanditStartingHideoutsPerFaction` (default 7 since #559; 14 before; renamed in #1017 so the 7 reaches saved settings)
docs/features/bandit-management.md 68:
  old: `BanditMaxPartiesPerHideout` (default 6 since #559;
  new: `BanditPartiesPerHideoutCap` (renamed in #1017 so the 6 reaches saved settings; default 6 since #559;
docs/features/bandit-management.md 260:
  old: `BanditInitialHideoutsPerFaction` added 2026-05-29
  new: `BanditStartingHideoutsPerFaction` (first `BanditInitialHideoutsPerFaction`) added 2026-05-29
docs/features/companion-tactics.md 109 (table row):
  old: | `EnableFormationPresets` |
  new: | `EnableOobFormationPresets` (renamed from `EnableFormationPresets` in #1017) |
docs/features/smart-cavalry-ai.md 149 (table row):
  old: | `EnableSmartCavalryAI` | bool | false | Master toggle. Off = vanilla.
  new: | `EnableSmartCavalryCharge` | bool | false | Master toggle. Off = vanilla. Renamed from `EnableSmartCavalryAI` in #1017 so the off default reaches saved settings.
```

**Verify**: `git grep -n -w -E "AiLordPartySizeFactor|AiLordPartySizeFlatBonus|AiGarrisonSizeFactor|AiFoodConsumptionRelief|AiWageRelief|FieldCommissionMaxOffersPerBattle|FieldCommissionMeritThreshold|EnableSmartCavalryAI|EnableFormationPresets|BanditMaxPartiesPerHideout|BanditInitialHideoutsPerFaction" -- docs/features docs/modding | grep -v -E "#1017|2026-06-21|2026-05-29"`
prints nothing; Docs run: `ai_dashes: 0`, `dead_links` unchanged.

#### Step B7: commit and Part B gate

Stage the Part B paths explicitly (`git add -- <each path from Steps B1 to B6>`). **Verify** the
staged set holds only those paths, then commit with message file `msg-110-B.txt`:

```
fix(settings): v2.0.28 - changed defaults reach every player's settings

The mod options keep a saved value for each setting by its name and
load it over the built-in default from then on, so a default changed
without renaming the setting never reached a player who had saved his
options before. Thirteen settings were caught this way on the 1.4
line. Each now has a new internal name, so everyone gets the intended
value (the labels in the options are unchanged):

- War of the Ring: Phase 1 Start Day 30 and Phase 2 Start Day 44 (an
  options file saved on v2.0.13 or earlier still held 2 and 14).
- World / Bandit Scaling: Initial Hideouts Per Faction 7 (was 14), Max
  Parties Per Hideout Cap 6 (was 3).
- Battle Tactics: Enable Smart Cavalry AI and Enable Formation Presets
  off, as shipped since v2.0.7 and v2.0.8.
- Battlefield Promotions: Merit To Promote 32, Offers Per Battle 2
  (were 8 and 1).
- AI Party Size: the lord party multiplier and flat bonus, the garrison
  multiplier and the food and wage relief return to neutral, as shipped
  since v2.0.26.

A player who had chosen his own value for one of these must set it
again. A new check, tools/check_mcm_default_flips.py, fails when a
default changes under an unchanged name; four older changes that are
safe to leave as saved are allowlisted with their reasons (#1017).

Research: 1.4.x tags v2.0.0 to v2.0.28 compared: 13 flips, 4 harmless
Not-tested: an old TAOM.json picking up the new defaults (check 6)
Ported-from: 42971374650eee9546589d0177ae852597199f95
```

Then run Build, Test, Tool tests, Docs. **Verify**: `Failed: 0`; `Total` = T_A + 1; Tool tests: P0
plus 32 passing tests; the gate exits 0. Record **T_B**.

### Part C: hardening 1.5.x lacks (new work, TDD)

#### Step C0: baseline

Build and Test; `Failed: 0`. Record **T_B** if this is a new session. Confirm the WotR filter passes:
`--filter "FullyQualifiedName~WarOfTheRing|FullyQualifiedName~Momentum"`.

#### Step C1: a non-finite or non-positive realm strength scores nothing (WOTR-08)

**RED.** Add to `TAOM.Tests/Features/WarOfTheRingMomentum/MomentumEventServiceTests.cs`:

```csharp
    // ---- WOTR-08: a realm strength that is not a finite positive number scores nothing ----

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-5f)]
    public void ProcessBattle_LoserStrengthNotFinitePositive_ScoresNoBattleMomentum(float strength)
    {
        _strengthAdapter.GetTotalStrength(Arg.Any<string>()).Returns(strength);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessDailyTick_SideStrengthNaN_AwardsNoStrengthMomentum()
    {
        _config.Events.MaxStrengthMomentum = 100;
        _strengthAdapter.GetTotalStrength("empire_w").Returns(float.NaN);
        _strengthAdapter.GetTotalStrength("vlandia").Returns(1000f);
        _strengthAdapter.GetTotalStrength("empire_s").Returns(1000f);
        _strengthAdapter.GetTotalStrength("isengard").Returns(1000f);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }
```

Add to `MomentumWarStateTests.cs` a test `InternalMomentum_SidesNearTheIntLimits_DoesNotOverflow`
(the fixture's `_sut` is the state: `_sut.Free.EditMomentum(2_000_000_000); _sut.Evil.EditMomentum(-2_000_000_000);` then
`Assert.AreEqual(40_000_000f, _sut.InternalMomentum, 1f);`) and to `MomentumSideDataTests.cs` a test
`AddEvent_NegativeValue_IsRefused` (the fixture's `_sut` is the side: `_sut.AddEvent(new MomentumEvent(-5, "poisoned", MomentumActionType.BattleWon, 10.0));`
then `SideMomentum` is 0 and `GetEvents(MomentumActionType.BattleWon).Count()` is 0; add `using System.Linq;` if missing).
**Verify RED**: Filtered `--filter "FullyQualifiedName~MomentumEventService|FullyQualifiedName~MomentumWarState|FullyQualifiedName~MomentumSideData"`
shows exactly these 5 new cases failing.

**GREEN.**
- `MomentumEventService.CalculateBattleMomentum`: replace `if (loserSideStrength == 0f) return 0;` with
  `if (!(FiniteFloatValidator.IsFinite(loserSideStrength) && loserSideStrength > 0f)) return 0;` and a
  one-line comment `// WOTR-08: positive requirement, so NaN fails; (int)Math.Round(NaN) is int.MinValue on net472/x64.`
  Add `using TAOM.Core.Validation;`.
- `AwardDailyStrengthMomentum`: replace `if (freeStrength == 0f || evilStrength == 0f) return;` with
  `if (!(FiniteFloatValidator.IsFinite(freeStrength) && freeStrength > 0f && FiniteFloatValidator.IsFinite(evilStrength) && evilStrength > 0f)) return;`.
- `MomentumWarState.InternalMomentum`: `((long)Free.SideMomentum - Evil.SideMomentum) / (float)MomentumScale`.
- `MomentumSideData.AddEvent`: `if (momentumEvent == null || momentumEvent.Value < 0) return;` with the
  comment `// WOTR-08: config event values are validated to [0, MaxIntValue]; a negative value can only come from a poisoned cast.`

**Verify GREEN**: the same filter passes. Commit (`msg-110-C1.txt`):

```
fix(momentum): v2.0.28 - non-finite realm strength scores nothing

A battle's war momentum divides the losers' casualties by their realms'
strength. A strength that is not a finite positive number now scores
nothing, instead of casting to the smallest integer and swinging the
war for either side with the bar at the centre. The momentum lead is
computed without integer overflow, and a negative event value is
refused.

Research: v1.4.8 Kingdom.cs:222 CurrentTotalStrength sums clans
Not-tested: trigger not reproduced; shipped strength models are bounded
```

#### Step C2: an empty roster never decides the war (WOTR-03), and the decided line says why

**RED.** In `MomentumVictoryServiceTests.cs` add:
- `CheckAndApplyVictory_BothRostersEmpty_DecidesNothing`: remove `empire_w`, `vlandia` and
  `empire_s`; expect `WarOutcome.None` and `_wotrService.DidNotReceive().EndWar(Arg.Any<WarOutcome>())`.
- `CheckAndApplyVictory_BothRostersEmptyPastThreshold_DecidesNothing`: same plus `SetInternalMomentum(600)`.
- `CheckAndApplyVictory_Elimination_LogsTheRuleRostersAndGate`: remove `empire_s`;
  `_settings.MinimumPlayerEventsForVictory.Returns(5)`; expect `_logger.Received(1).LogInfo(Arg.Is<string>(m =>
  m.StartsWith("[Momentum] War of the Ring decided: FreeVictory (momentum 0, threshold 500)")
  && m.Contains("by elimination") && m.Contains("Free 2, Evil 0") && m.Contains("player gate on, minimum 5")))`.
- `CheckAndApplyVictory_Threshold_LogsTheThresholdRule`: `SetInternalMomentum(500)`; expect a LogInfo
  starting `[Momentum] War of the Ring decided: FreeVictory (momentum 500, threshold 500)` and containing `by threshold`.

In `MomentumEnrollmentServiceTests.cs` add:
- `SweepEnrollment_OnlyFreeRealmsEnroll_DoesNotStartTheWarAndWarnsOnce`:
  `_alignmentService.GetKingdomSide("empire_s").Returns(FactionSide.Neutral);` sweep twice; expect
  `HasWarStarted` false and `_logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("has not started")))`.
- `SweepEnrollment_EvilRealmEnrollsLater_StartsTheWar`: first sweep with `empire_s` Neutral; expect
  `HasWarStarted` false (this assert is what fails on the old code); then
  `Returns(FactionSide.Evil)` and sweep again; expect `HasWarStarted` true.

**Verify RED**: Filtered `--filter "FullyQualifiedName~MomentumVictoryService|FullyQualifiedName~MomentumEnrollmentService"`
shows these 6 failing.

**GREEN.** `MomentumVictoryService.CheckAndApplyVictory`, after `int threshold = ...;`:

```csharp
        int freeRealms = state.Free.KingdomIds.Count;
        int evilRealms = state.Evil.KingdomIds.Count;

        // WOTR-03: elimination needs a survivor. With both rosters empty nobody is left to win, so the
        // war stays open (the next sweep re-enrolls any realm that still exists) instead of defaulting
        // to the first branch below.
        if (freeRealms == 0 && evilRealms == 0)
            return WarOutcome.None;

        var outcome = WarOutcome.None;
        bool byThreshold = false;
        if (internalMomentum >= threshold || evilRealms == 0)
        {
            outcome = WarOutcome.FreeVictory;
            byThreshold = internalMomentum >= threshold;
        }
        else if (internalMomentum <= -threshold || freeRealms == 0)
        {
            outcome = WarOutcome.EvilVictory;
            byThreshold = internalMomentum <= -threshold;
        }

        if (outcome == WarOutcome.None)
            return WarOutcome.None;

        // The prefix is what a player sends in: keep it verbatim. The tail says which rule fired.
        string gate = _settings.RequireParticipationForVictory
            ? $"on, minimum {_settings.MinimumPlayerEventsForVictory}"
            : "off";
        _logger.LogInfo($"[Momentum] War of the Ring decided: {outcome} (momentum {internalMomentum:F0}, threshold {threshold}); "
            + $"by {(byThreshold ? "threshold" : "elimination")}; enrolled realms Free {freeRealms}, Evil {evilRealms}; player gate {gate}");
```

(The old `var outcome`, `if`/`else if`, `if (outcome == None)` and log lines are replaced; the ordered
`MarkWarEnded`, `EndWar`, `PeaceOutCrossSidePairs` block after them stays.) `MomentumEnrollmentService.SweepEnrollment`:
replace the `if (!state.HasWarStarted && (... || ...))` block with:

```csharp
        if (!state.HasWarStarted)
        {
            int freeRealms = state.Free.KingdomIds.Count;
            int evilRealms = state.Evil.KingdomIds.Count;
            // WOTR-03: a side that never enrolled must not count as eliminated, which would hand the other
            // side an elimination victory the day the war starts. Start only when both sides hold a realm.
            if (freeRealms > 0 && evilRealms > 0)
            {
                state.MarkWarStarted();
                _logger.LogInfo("[Momentum] The War of the Ring momentum tracking has begun");
                changed = true;
            }
            else if (changed)
            {
                _logger.LogWarning($"[Momentum] The momentum war has not started: Free {freeRealms} and Evil {evilRealms} realms enrolled, and both sides need one (check execution/alignment.json against the loaded kingdoms)");
            }
        }
```

Docs: in `docs/features/war-of-the-ring-momentum.md`, section "Victory ↔ phase-machine integration",
add this paragraph:

```text
Elimination needs a survivor: with both rosters empty the check decides nothing, and the momentum war starts only once both sides hold an enrolled realm (a side that never enrolled is not "eliminated"; the sweep warns once, naming both counts). The decided log line keeps its prefix and appends the rule (`by threshold` or `by elimination`), both roster sizes and the player gate.
```

**Verify GREEN**: the filter passes; Commit (`msg-110-C2.txt`):

```
fix(momentum): v2.0.28 - empty rosters never decide the war

With victory enabled, the war could end in a Free Peoples win the day
it started when no Evil realm had enrolled, or when both rosters were
empty, with the bar at the centre and no battle fought. The momentum
war now starts only when both sides have an enrolled realm, and empty
rosters on both sides decide nothing. The "War of the Ring decided"
log line keeps its wording and now also names the rule that fired
(threshold or elimination), both roster sizes and the player gate, so
a player's log shows why the war ended.

Not-tested: a one-sided enrollment in a live campaign (in-game check 7)
```

#### Step C3: reset the momentum war per campaign (#773, WOTR-05)

**RED.** Create `TAOM.Tests/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehaviorSessionResetTests.cs`
(`[TestClass]`, `[TestCategory("RequiresGame")]`, namespace `TAOM.Tests.Features.WarOfTheRingMomentum`) with a
helper that builds the behavior exactly as `WarOfTheRingMomentumBehaviorSyncDataTests.NewBehavior` does
(substitutes for every dependency except the store; copy that file's `using` lines and add
`using TAOM.Features.WarOfTheRingMomentum.Domain;`) and four tests:
1. `Constructor_ResetsTheStore`: a substitute store receives `ResetForNewGame()` once.
2. `NewBehavior_AfterAPopulatedCampaign_StartsFromAFreshWar`: a real `new MomentumStateStore(Substitute.For<IModLogger>())`;
   build a behavior; `store.State.MarkWarStarted(); store.State.Free.AddKingdom("empire_w"); store.PlayerEvents.Add(MomentumActionType.BattleWon);`
   build a second behavior on the same store; expect `HasWarStarted` false, no Free kingdom, no player event.
3. `SyncData_SaveThenLoadIntoTheNextCampaign_RestoresTheSavedWar`: real store, behavior A, populate as
   above, `A.SyncData(new EngineDataStore())` into a kept store `data`; `data.Loading = true`; build
   behavior B (the store is now fresh), then `B.SyncData(data)`; expect `HasWarStarted` true and
   `Free.ContainsKingdom("empire_w")`.
4. `Source_NoLongerResetsFromOnNewGameCreated`: `RepoPaths.ReadSource("Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs", stripComments: true)`
   does not contain `OnNewGameCreatedEvent`.

Create `TAOM.Tests/Features/WarOfTheRingMomentum/WarOfTheRingMomentumWiringTests.cs` (no RequiresGame),
modelled on `TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs`:

```csharp
    private static readonly Regex InlinePerCampaign = new Regex(
        @"campaignStarter\.AddBehavior\(\s*new\s+(?:Features\.WarOfTheRingMomentum\.)?WarOfTheRingMomentumBehavior\(");
    private static readonly Regex AnyConstruction = new Regex(@"new\s+(?:[\w.]+\.)?WarOfTheRingMomentumBehavior\(");
    private static readonly Regex ContainerUse = new Regex(@"(?:Register|Resolve)\s*<\s*(?:[\w.]+\.)?WarOfTheRingMomentumBehavior\s*>");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1
        && !ContainerUse.IsMatch(source);
```

Tests: `SubModule_BuildsTheMomentumBehaviorInlineAsTheAddBehaviorArgument` (stripped `Main/SubModule.cs`
passes the predicate); `ProductionSource_NoOtherFileConstructsRegistersOrResolvesTheBehavior` (every
`Main/**/*.cs` except `SubModule.cs` and `WarOfTheRingMomentumBehavior.cs`, comments stripped, matches
neither `AnyConstruction` nor `ContainerUse`; `MomentumCheats.cs`'s static `BuildSavePayload` call is
fine); `Predicate_RejectsACachedOrContainerShape` with three `[DataRow]`s:
`"campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());"`,
`"campaignStarter.AddBehavior(_cached ??= new WarOfTheRingMomentumBehavior(a));"`,
`"container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton); campaignStarter.AddBehavior(new WarOfTheRingMomentumBehavior(a));"`;
and `Predicate_AcceptsTheInlineConstructionAcrossLines` with
`"campaignStarter.AddBehavior(new Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior(\n    a,\n    b));"`.

**Verify RED**: Filtered `--filter "FullyQualifiedName~WarOfTheRingMomentumBehaviorSessionReset|FullyQualifiedName~WarOfTheRingMomentumWiring"`:
`Constructor_ResetsTheStore`, `NewBehavior_AfterAPopulatedCampaign_StartsFromAFreshWar`,
`Source_NoLongerResetsFromOnNewGameCreated`, `SubModule_Builds...` and `ProductionSource_...` fail;
the save-then-load test and the four predicate rows pass.

**GREEN.**
- `WarOfTheRingMomentumBehavior` constructor: after `_coopSession = coopSession;` add `_stateStore.ResetForNewGame();`
  with the comment `// #773: the momentum store is a process-lifetime singleton and this behavior is built with new in every campaign's OnGameStart (SubModule), before any campaign event and before a load's SyncData. Resetting here means a new campaign, or a load of a save with no momentum record (the engine then never calls SyncData), never sees the previous campaign's war. Same pattern as WarOfTheRingBehavior (#764).`
  (wrap at about 110 columns). Delete the `OnNewGameCreatedEvent` listener line in `RegisterEvents`.
- `WarOfTheRingMomentumIoC.cs`: delete `container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton);`.
- `Main/SubModule.cs`: replace the two comment lines above the momentum `AddBehavior` and the
  `campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());` line with:

```csharp
        // WotR Momentum #327: Evil-vs-Good progress tracking and victory. #773: built with `new` in every
        // campaign, like WarOfTheRingBehavior above; its constructor resets the process-lifetime momentum
        // store (WarOfTheRingMomentumWiringTests).
        campaignStarter.AddBehavior(new Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior(
            IoC.Resolve<Features.WarOfTheRingMomentum.IMomentumStateStore>(),
            IoC.Resolve<Features.WarOfTheRingMomentum.IMomentumEventService>(),
            IoC.Resolve<Features.WarOfTheRingMomentum.IMomentumEnrollmentService>(),
            IoC.Resolve<Features.WarOfTheRingMomentum.IMomentumVictoryService>(),
            IoC.Resolve<IWarEventSnapshotAdapter>(),
            IoC.Resolve<Features.WarOfTheRingMomentum.IMomentumSettingsProvider>(),
            wotrService,
            IoC.Resolve<Features.WarOfTheRingMomentum.UI.IMomentumUIService>(),
            IoC.Resolve<IModLogger>(),
            coopSession));
```

  (`wotrService` and `coopSession` are locals of `RegisterDiplomacyAndConflict`; `using TAOM.Adapters;`
  and `using TAOM.Core.Logging;` are already at the top of the file.)
- Docs (`war-of-the-ring-momentum.md`): in "Key Files" and the architecture prose state that the
  behavior is built with `new` per campaign and resets the store in its constructor (#773), and that
  the container no longer registers it.

**Verify GREEN**: the filter passes; Build exit 0;
`git grep -n -E "OnNewGameCreatedEvent|Register<WarOfTheRingMomentumBehavior>" -- Main/Features/WarOfTheRingMomentum`
prints nothing. Commit (`msg-110-C3.txt`):

```
fix(momentum): v2.0.28 - reset the momentum war per campaign (#773)

Loading a save made before the momentum war existed, after playing
another campaign in the same game session, no longer carries that
campaign's momentum, rosters and player events into it. The momentum
behavior is now built fresh for every campaign and resets the momentum
state in its constructor, like the War of the Ring behavior (#764); a
loaded save with a momentum record then restores its own war.

Research: v1.4.8 CampaignBehaviorDataStore.cs:86-105 skips SyncData
Research: when a save holds no record; Campaign.cs:1391, :1427-1430
Save-compat: save keys unchanged
Not-tested: a record-less load after another campaign (in-game check 8)
```

#### Step C4: show the lead that decides the war; honest option texts (WOTR-04)

Precondition (D20): `docs/reference/development-machines.md` holds the laptop validator baseline WP101
recorded (search it for `validate_moduledata.py` and `baseline`). Not there: STOP before this step.

**RED.** In `MomentumQueryServiceTests.cs` add `VictoryLead_FreeAhead_IsPositiveInDisplayedUnits`
(Free `EditMomentum(600 * MomentumWarState.MomentumScale)`, Evil `100 * ...`; expect `VictoryLead` 500),
`VictoryLead_EvilAhead_IsNegative` (Evil 300, Free 0; expect -300) and
`VictoryEnabled_ReadsTheSetting` (`_settings.VictoryEnabled.Returns(true)`; expect true).
**Verify RED**: Build fails (`VictoryLead`, `VictoryEnabled` missing).

**GREEN.**
- `IMomentumQueryService`: add `bool VictoryEnabled { get; }` and `int VictoryLead { get; }` with doc
  comments: the lead the victory check compares with `VictoryThreshold`, in displayed units, rounded,
  positive = Free ahead; absolute, unlike `SliderValue`, so a long war can reach the threshold with
  the bar near the centre (WOTR-04).
- `MomentumQueryService`: `public bool VictoryEnabled => _settings.VictoryEnabled;` and
  `public int VictoryLead { get { float lead = _stateStore.State.InternalMomentum; return FiniteFloatValidator.IsFinite(lead) ? (int)Math.Round(lead) : 0; } }`
  (add `using TAOM.Core.Validation;`).
- `UI/MomentumPopupVM.cs`, end of `RebuildStats()`:

```csharp
        // WOTR-04: the victory check compares an absolute lead with the threshold while the bar shows a
        // ratio, so with victory on the popup also shows the lead that decides the war.
        if (_query.VictoryEnabled)
        {
            int lead = _query.VictoryLead;
            var label = new TextObject("{=taom_wotr_victory_lead}Victory lead (needs {THRESHOLD})");
            label.SetTextVariable("THRESHOLD", _query.VictoryThreshold);
            StatsBreakdown.Add(new BreakdownVM(label.ToString(),
                lead > 0 ? lead.ToString() : "0", lead < 0 ? (-lead).ToString() : "0"));
        }
```

- Register the string: in `Main/_Module/ModuleData/taom_wotr_strings.xml` add, directly below the
  `taom_wotr_total` row (tab indent): `<string id="taom_wotr_victory_lead" text="Victory lead (needs {THRESHOLD})" />`.
  In each of the 12 `Main/_Module/ModuleData/Languages/<LANG>/std_taom_wotr_strings_*.xml` files add,
  below its `taom_wotr_total` row (4-space indent), the same row with the English text (a presence
  row; `/localize` translates it later, a maintainer step that costs money).
- `Main/Features/TaomSettings.cs` hints (plain strings, not localized):
  match each by its current text (Part B moved the line numbers; on `1b00d1acd` they were 233, 243
  and 248), and replace only the string inside `HintText = "..."`:

```text
Victory Threshold
  old: Momentum lead one side needs to win the war outright. Default 500.
  new: Momentum lead (the Free Peoples' total minus the Dark Powers' total) one side needs to win the war outright. The map bar shows the ratio of the two totals, not this lead, so after a long war a side can reach it with the bar near the middle; with victory on, the War of the Ring popup shows the lead. Default 500.
Require Player Participation For Victory
  old: When on, the war cannot end until you have fought in enough momentum events yourself. Default: on.
  new: When on, the war cannot end until enough momentum events have involved you. A battle counts when your party fought in it or a lord of the realm you serve led either side, won or lost; a siege counts when your side took the settlement. Default: on.
Minimum Player Events For Victory
  old: How many battles/sieges you must take part in before either side can win. Default 5.
  new: How many of those momentum events (see the setting above) must have happened before either side can win. Default 5.
```
- Docs (`war-of-the-ring-momentum.md`): Key Files string count `24` becomes `25`; under "Known
  limitations", in the bullet `**Momentum accumulates without bound.**`, add that with victory on the popup shows the
  deciding lead against the threshold; in "What counts as player participation" state that realm-wide
  credit is deliberate (#375) and the option texts now say so.

**Verify GREEN**: Build exit 0; Filtered `--filter "FullyQualifiedName~MomentumQueryService|FullyQualifiedName~Localization|FullyQualifiedName~WarOfTheRing"`
passes (the localization tests prove the key is registered and present in all 12 languages); Data
run shows no new ERROR against the WP101 baseline. Commit (`msg-110-C4.txt`):

```
fix(momentum): v2.0.28 - show the lead that decides the war

With victory enabled, the War of the Ring popup now shows the momentum
lead the victory check uses, against the threshold. The map bar shows
the ratio of the two sides' totals, so after a long war a side could
win with the bar near the middle. The option texts now say that
battles and sieges by any lord of your realm count toward the player
requirement, won or lost, which is how it has always worked (#375).
The new popup label is English in every language until the next
translation run.

Not-tested: popup row rendering (in-game check 7)
```

#### Step C5: log the effective phase days and their source (WOTR-06 diagnostics, WOTR-10 cheap part)

**RED.** In `WarOfTheRingServiceTests.cs` (use the existing `UseMcmPhaseDays` helper) add six tests
asserting `_sut.DescribePhaseDays()`: `UseMcmPhaseDays(30, 44)` gives `"30/44 from MCM"`; plus
`_settingsProvider.TestMode.Returns(true)` gives `"1/3 from MCM Test Mode"`; a config with
`TestMode.Enabled = true` and `IsAvailable` false gives `"1/3 from war_of_the_ring.json test mode"` (the service
reads its config in the constructor: return that config from `_configProvider.LoadConfig()` and rebuild `_sut`,
as `CheckPhaseTransition_TestMode_UsesShortDays` does);
the default fixture gives `"30/45 from war_of_the_ring.json"`; `UseMcmPhaseDays(30, 20)` gives
`"30/31 from MCM"` (the clamp); `UseMcmPhaseDays(30, 44)` with `WarOfTheRingEnabled` false gives
`"30/44 from MCM (War of the Ring disabled)"`. In `WarOfTheRingBehaviorSessionResetTests.cs` add
`OnSessionLaunched_Host_LogsTheEffectivePhaseDays`: `_service.DescribePhaseDays().Returns("30/44 from MCM")`,
a behavior built with its own logger substitute and `() => 4f`, then `OnSessionLaunched(null!)`;
expect one LogInfo containing `phase restored at day 4` and `phase days 30/44 from MCM`.
**Verify RED**: Build fails (`DescribePhaseDays` missing).

**GREEN.**
- `IWarOfTheRingService`: `string DescribePhaseDays();` with a comment (WOTR-06: the "Loaded War of
  the Ring config" line prints the JSON pair only).
- `WarOfTheRingService`: make `GetRawPhaseDays` also return a source label (`"MCM Test Mode"`,
  `"war_of_the_ring.json test mode"`, `"MCM"`, `"war_of_the_ring.json"`, in its existing branch
  order), keep `GetEffectivePhaseDays` and its clamp returning the same pair, and add
  `public string DescribePhaseDays()` returning `$"{p1}/{p2} from {source}"`, plus
  `" (War of the Ring disabled)"` when `GetEffectiveEnabled()` is false.
- `WarOfTheRingBehavior.OnSessionLaunched`: append `; phase days {_wotrService.DescribePhaseDays()}`
  inside the existing interpolated `Session launched` string, just before its closing quote.
- Docs: `war-of-the-ring.md` test counts (64 in `WarOfTheRingServiceTests`, 9 in
  `WarOfTheRingBehaviorSessionResetTests`) and one sentence in the persistence section naming the
  new log tail; `war-of-the-ring.md` "How to Test In-Game" gains a step: every load logs the phase
  days and their source.

**Verify GREEN**: Filtered `--filter "FullyQualifiedName~WarOfTheRing"` passes. Commit (`msg-110-C5.txt`):

```
feat(wotr): v2.0.28 - log the phase days and their source on load

The session-launch log line now names the phase days the War of the
Ring actually uses and where they come from: the mod options, Test
Mode or war_of_the_ring.json. The "Loaded War of the Ring config" line
prints the JSON values only, which hid an options file that still held
day 2 and day 14.

Not-tested: the line in a live load (in-game check 9)
```

#### Step C6: a resisted raid no longer scores twice (#774)

**RED.** In `MomentumEventServiceTests.cs` set `VillageLooted = true` in the snapshot of
`ProcessRaid_EnrolledAttackerVictory_AddsRaidValueAndStat` and add
`ProcessRaid_AttackerWonTheMilitiaFightButDidNotLoot_NoChange` (`AttackerVictory = true`,
`VillageLooted = false`, `AttackerFactionId = "isengard"`; expect `Evil.SideMomentum` 0 and
`Evil.TotalStats.TotalVillagesRaided` 0). **Verify RED**: Build fails (`VillageLooted` missing).

**GREEN.**
- `Snapshots/RaidOutcomeSnapshot.cs`: `public bool VillageLooted { get; set; }` with a summary: true
  when the raid looted the village; v1.4.8 `RaidEventComponent.OnBeforeFinalize` sets Looted or
  Normal before it raises `RaidCompleted` and reports the attacker as winner for a won militia fight
  too (`RaidEventComponent.cs:128-138`), so `AttackerVictory` alone counted a resisted raid twice (#774).
- `WarEventSnapshotAdapter.FromRaid`: add
  `VillageLooted = component?.MapEventSettlement?.Village?.VillageState == Village.VillageStates.Looted,`.
- `MomentumEventService.ProcessRaid`: add `|| !raid.VillageLooted` to the first guard.
- Docs (`war-of-the-ring-momentum.md`): a raid scores only when the village is looted (#774).

**Verify GREEN**: Filtered `--filter "FullyQualifiedName~MomentumEventService"` passes. Commit (`msg-110-C6.txt`):

```
fix(momentum): v2.0.28 - a resisted raid no longer scores twice (#774)

The game reports a raid party that beats a village's militia as a raid
win even when the village is not looted yet, so war momentum scored
that raid twice: once for the fight and again for the looting. War
momentum now scores a raid only when the village is looted.

Research: v1.4.8 RaidEventComponent.cs:128-138; Village.cs:16-22
Not-tested: a militia-resisted raid in a live campaign (check 10)
```

#### Step C7: Part C gate

Run Build, Test, Docs, and the Gate. **Verify**: `Failed: 0`; `Total` = T_B + 32 (C1 5, C2 6, C3 10,
C4 3, C5 7, C6 1); gate exit 0; `ai_dashes: 0`. If a localization test iterates keys and reports a
different delta, record the per-class numbers in the hand-off.

## Disposition (every commit in the pack, plus the files this plan borrows)

| Commit | Subject (1.5.x) | Group | Disposition | Step |
|---|---|---|---|---|
| `2719e59e8` | fix(wotr): reset the War of the Ring per campaign | issue-764 | Ported. Bookkeeping, the Codex prompt, review log and lessons dropped; review-reference bullet hand-merged; RCA kept with a port note | A1 |
| `9ab3952a7` | docs(rules): reset per-campaign state in the constructor | issue-764 | Ported (clean; rule for the A1 mechanism) | A2 |
| `cf7486cba` | docs(wotr): #764 and #772 in diplomacy and momentum docs | issue-764 | Ported partially: WarEnded, stance guard, repair, skipped scripted war, absent-count sentence. Dropped: Gundabad/Arthedain (content), `WarOfTheRingPhaseDayTests` and `GetEffectivePhaseDay` (#765, War Chronicle), the 73-key and 174-test counts, the Status edit | A6 |
| `d1b9fd91a` | fix(wotr): declare the Full War wars for real | issue-772 | Ported; three doc lines re-cited for 1.4.x; bookkeeping dropped; RCA kept with a port note | A3 |
| `16a34027d` | refactor(core): one guarded SyncData chunk loop for two saves | wotr-syncdata-chunker-guard | Ported partially: chunker, momentum behavior, cheats and tests; four WarChronicle paths dropped (feature absent); lesson dropped (WP142); v1.5.5 citations re-cited to v1.4.8 | A4 |
| `ea8f2ad52` (file only) | test(infra): one engine-faithful data store fake | owned by WP122 (tests-engine-data-store-fake) | Borrowed: `TAOM.Tests/Infrastructure/EngineDataStore.cs` only, needed by A4's tests. The test conversions stay with WP122 | A4 |
| `ec2a3026f` | fix(career): siege and capture credit follow the player | issue-1015 | Adapted: the side rule reads the main party's map event side (no `IPlayerControlQuery`, D7); enlisted clause removed as on 1.5.x; tests adapted to the seam; review record dropped | A5 |
| `429713746` | fix(settings): changed defaults reach every player's settings | issue-1017 | Adapted by hand: 13 renames; `ChargeAutoKnockdownWeightRatio` skipped (8 at every 1.4.x tag, the 8 to 6 flip is 1.5.x balance); gate and its tests verbatim; allowlist rewritten for 1.4.x (4 entries); `.ai` wiring, rule line and release line kept; ServerSettings, HotPath, SmartCavalry, CombatMechanics and CompanionTactics provider test edits skipped (files absent on 1.4.x); orientation.md, server-settings.md, combat-mechanics.md and shader-precompilation.md hunks skipped; RCA, lesson, feature-map and tools/README dropped | B |
| `3fe86e22b` | docs(reviews): RCA, lessons and generator region gate | issue-818 | Skipped here. Lessons are synced once by WP142 (rule 2, D18; verdicts below). The RCA covers `2410aec51` (out). `tools/tests/test_generator_regions_in_sync.py` has no 1.4.x owner: `generate_arthedain_troops.py` is absent and `generate_new_faction_kingdoms.py` has no `--check` | none |
| `2410aec51` | feat(diplomacy): no wars before the War of the Ring | issue-818 | Skipped (SKIP-FEATURE: a new rule plus kingdom content) | none |
| `a0db0558b` | test(composition): read SubModule and IoC through one reader | prerequisite | Provided by WP104 (`RepoPaths`); A0.2 stops if absent | A0 |

Lesson verdicts for WP142 (`3fe86e22b`, checked on v1.4.8 this session): (1) "a war veto gates
where the engine prices or offers the war": `Kingdom.AddDecision` charges influence before the
election (`Kingdom.cs:696-703`) and a declined call to war costs 50 relation
(`AllianceCampaignBehavior.cs:142`); the crime-rating charge is UNVERIFIED. (2) "do not make peace or
war in OnSessionLaunched on a load": holds, session-launch listeners run at `Campaign.cs:729`, before
`MapEventManager.OnAfterLoad` and `SiegeEventManager.OnAfterLoad` (`:740-741`). (3) "a negative
relationship value declares war whatever isAtWar says": holds, `Kingdom.Deserialize`
(`Kingdom.cs:780-795`) declares war for a value below 0 and again for `isAtWar="true"`. (4) the
region-owner gate: no 1.4.x owner.

## Deep-dive findings this WP owns

| Finding | Where handled |
|---|---|
| WOTR-01 Full War declares no wars | A3 |
| WOTR-02 process singleton phase | A1 (rule A2) |
| WOTR-03 empty-roster tie and one-sided start | C2 |
| WOTR-04 absolute lead hidden; gate hint | C4 (realm-wide credit kept, #375) |
| WOTR-05 record-less load inherits momentum (#773) | C3 |
| WOTR-06 legacy MCM phase days | B (rename) and C5 (log) |
| WOTR-07 SiegeOutside counted as capture | A5 |
| WOTR-08 non-finite strength | C1 |
| WOTR-09 / SAVE-TIME-06 corrupt chunk count | A4 |
| WOTR-10 phase clock | C5 logs the clock day and the effective days; persisting the start ticks is rejected (trigger UNVERIFIED, would change the save format) |
| #774 raid counted twice (batch 16 lead) | C6 (mechanism verified on v1.4.8) |

## Test plan

- Part A brings the 1.5.x tests: `WarOfTheRingBehaviorSessionResetTests` (8), `WarOfTheRingWiringTests` (7),
  `WarOfTheRingServiceTests` (+27), `WarOfTheRingConfigProviderTests` (+6), the `CoopVetoClassificationTests`
  mutator rows, `SyncDataChunkerTests` (7, moved), `SyncDataChunkerSyncTests` (7),
  `WarOfTheRingMomentumBehaviorSyncDataTests` (4), `WarOfTheRingMomentumSiegeGateTests` (5),
  `EnlistedSiegeCreditTests` (19, adapted to the static `PlayerFought(MobileParty, PartyBase)` seam).
- Part B: `McmRenamedDefaultsTests` (1 method, 13 rows) and the gate's 32 Python tests; existing
  tests updated in B4.
- Part C (TDD): 32 cases listed in C1 to C6. Patterns: `MomentumEventServiceTests` fixture for C1 and
  C6; `MomentumVictoryServiceTests.SetInternalMomentum` for C2; `WarOfTheRingWiringTests` for the
  momentum wiring test; `WarOfTheRingMomentumBehaviorSyncDataTests` for the behavior constructor helper.
- Structurally untestable here (commit `Not-tested:` trailers): engine-raised campaign events, the
  live war declarations and their notices, the popup rendering, the MCM file migration.
- Verification: the full Test command after each Part, `Failed: 0`, totals as A7, B7, C7 state.

## Done criteria

Part A:
- [ ] Six commits, subjects as A1 to A6; `git status --porcelain` empty.
- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 83 (or the per-class counts reported).
- [ ] `git grep -n -E "WarChronicle|War Chronicle|MomentumSyncChunker|IPlayerControlQuery" -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -n -E "v1\.5\.[0-9]" -- Main/Core/Persistence Main/Features/WarOfTheRingMomentum Main/Adapters/WarEventSnapshotAdapter.cs Main/Features/CareerSystem/Quests/CareerQuest.cs Main/Features/Diplomacy/WarOfTheRingBehavior.cs Main/Features/Diplomacy/WarOfTheRingService.cs TAOM.Tests/Infrastructure/EngineDataStore.cs TAOM.Tests/Core/Persistence TAOM.Tests/Features/Diplomacy/WarOfTheRing*.cs TAOM.Tests/Features/WarOfTheRingMomentum TAOM.Tests/Features/Enlistment/AttributionAudit docs/features/war-of-the-ring.md docs/features/war-of-the-ring-momentum.md docs/features/diplomacy.md docs/features/enlistment.md` prints nothing.
- [ ] Docs: `ai_dashes: 0`, `dead_links` and `orphan_features` as A0.7.

Part B:
- [ ] One commit, subject as B7. `python tools/check_mcm_default_flips.py` exits 0 with four `ALLOWED` lines.
- [ ] `python -m unittest tools.tests.test_check_mcm_default_flips` passes (32).
- [ ] Test `Failed: 0`, `Total` = T_A + 1; `McmRenamedDefaultsTests` passes.
- [ ] B6's doc grep prints nothing; `git grep -n -E "Instance\?\.Phase[12]TriggerDay" -- Main` prints nothing.

Part C:
- [ ] Six commits, subjects as C1 to C6. Test `Failed: 0`, `Total` = T_B + 32 (or reported).
- [ ] `grep -cF 'War of the Ring decided: {outcome} (momentum {internalMomentum:F0}, threshold {threshold})' Main/Features/WarOfTheRingMomentum/MomentumVictoryService.cs` prints `1`.
- [ ] `git grep -n "Register<WarOfTheRingMomentumBehavior>" -- Main` and `grep -c OnNewGameCreatedEvent Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs` give nothing and `0`.
- [ ] Data run: no new ERROR against the WP101 baseline. Gate exit 0. Docs `ai_dashes: 0`.

## STOP conditions

Stop and report (do not improvise) if:

- A0.2 or A0.4 output differs (WP104's `RepoPaths` API missing, or part of this WP already landed).
- A cherry-pick conflicts in a code or test path its step does not list, or the staged set differs
  from the step's expected list after resolution.
- Any `Main/Features/WarChronicle` or `TAOM.Tests/Features/WarChronicle` path remains after A4.
- `TAOM.Tests/Infrastructure/EngineDataStore.cs` already exists at A4 and is not the same class
  (`public sealed class EngineDataStore : IDataStore` with `Seed`, `Get`, `GetSaved`, `Has`).
- A test outside this WP's scope fails after a step (do not fix unrelated code).
- The gate in B1 reports anything other than the 13 `FLIP` and 4 `ALLOWED` lines, reports
  `UNPARSED` or `STALE-ALLOWLIST`, exits 2, or compares against a number of tags other than 19.
- A rename in B3 would need a default different from the one listed (someone changed 1.4.x since planning).
- The fix appears to need `Main/IoC.cs`, a csproj, `Directory.Build.props`, `.claude/settings*.json`
  or a balance number.
- A TaleWorlds member differs from "Engine facts" (for example `Kingdom.GetStanceWith` missing): report
  the mismatch, do not decompile and improvise.
- The C4 precondition (WP101's validator baseline in `docs/reference/development-machines.md`) is not met (D20).
- C2's log assertion cannot keep the prefix `[Momentum] War of the Ring decided: <outcome> (momentum X, threshold Y)` verbatim.
- A commit hook denies a commit (report the message verbatim), or a verification fails twice after a
  reasonable fix attempt.

## In-game checks owed (maintainer)

Logs: the newest `Logs\taom_debug_<timestamp>.log` beside the game executable
(`bin\Win64_Shipping_Client`; `FileLogger` writes `Logs/taom_debug_*.log`, keeps 10).

1. (#764, A1) Play past Full War (MCM "Enable Test Mode" puts it on day 3), exit to the main menu
   without closing the game, start a new campaign. Expect `War of the Ring: phase reset to Peace at
   campaign start; a load restores the saved phase next`, no `[Momentum] ... enrolled` line before
   `War of the Ring: Transitioning to FullWar`, and no meter until Full War.
2. (#772, A3) Test Mode on, new campaign: on day 3 expect `War of the Ring: <a> declares war on <b>
   (hostile tier)` lines with map notices and encyclopedia history entries, and no `had no effect`
   warning naming a shipped pair. As a kingdom ruler, count the call-to-war popups at the transition
   and report the number.
3. (#772 repair) Load a Full War save made by v2.0.28 or earlier: expect `War of the Ring: declared N
   missing Full War wars (#772)` once; save and reload: the line does not appear again.
4. (#792, A4) Load a long campaign past Full War: no `[Momentum] Save: the chunk count ... is out of
   range` warning, and the meter matches its pre-update state.
5. (#1015, A5) As a vassal inside another lord's army, take a town: the career quest's
   SettlementsCaptured advances and momentum logs a player siege event. Enlisted and sitting a siege
   out: no credit.
6. (#1017, B) With an old `Configs/ModSettings/Global/TAOM/TAOM.json` (for example one holding
   `Phase1TriggerDay` 2), launch: MCM shows Phase 1 Start Day 30 and Phase 2 Start Day 44; after
   saving options the file holds `WarPhase1TriggerDay`. The bandit, Smart Cavalry, Formation
   Presets, Battlefield Promotions and AI Party Size options show their defaults.
7. (C2, C4) Turn on "Enable Victory (End the War)" in a Full War campaign: the popup shows
   "Victory lead (needs 500)" with the lead in one column; when the war ends, the `[Momentum] War of
   the Ring decided:` line ends with `by threshold` or `by elimination`, both roster sizes and the gate.
8. (C3, #773) After playing a campaign in one process, load a save made before the momentum feature
   (before 2026-07-03): the momentum starts fresh (no inherited rosters or ended war).
9. (C5) Every load logs `[WarOfTheRing] Session launched` with `phase days 30/44 from MCM` (or the real source).
10. (C6, #774) Watch a raid where the raiders beat a village's militia: Villages Raided rises by one
    per looted village, not two.

## Hand-off report

Report in your final message, for the integrator (who records it in `plans/README.md`; no old-plan row
needs updating for this WP): the status of each Part (done or blocked, with the reason); T0, T_A, T_B, T_C (Total, Passed, Skipped); P0 and the final Python
`Ran N tests` line; the A0.7 and final docs values; each commit's short SHA and subject; the B1 and
B4 gate output (counts of `FLIP` and `ALLOWED` lines, exit codes); whether the release-skill line
was added; any per-class count that differed from the plan; and the in-game checks owed (list above).

## Maintenance notes

- **WP122** (`ea8f2ad52`): `TAOM.Tests/Infrastructure/EngineDataStore.cs` already exists after this WP
  (content from `ea8f2ad52`, citation moved to v1.4.8). Keep ours on the add/add conflict; convert
  the other tests as `ea8f2ad52` does, including this WP's `WarOfTheRingBehaviorSessionResetTests`
  `FakeDataStore`.
- **WP123** (`890fe29de`): renaming `EnableEnlistmentDiagnostics` makes this WP's allowlist entry
  stale and the gate fail; delete that entry in the same commit.
- **WP114**: `MomentumSyncChunker.cs` is gone; cite `Main/Core/Persistence/SyncDataChunker.cs` in any
  save-system doc that points at the momentum chunking.
- **WP134** (`5e43518df`): does not touch `MomentumEnrollmentService`; it skips that commit's side
  resolution refactor as behaviour-equivalent on 1.4.x, so it never meets C2's both-sides start rule.
- **WP129**: career fixes touch `CareerQuest.cs` after A5 changed `OnSettlementOwnerChanged` and
  `OnHeroPrisonerTaken`; port on top of the side rule.
- **WP135**: translate `taom_wotr_victory_lead` (English in 12 languages after C4).
- **WP142**: take the lessons of `2719e59e8`, `d1b9fd91a`, `16a34027d`, `429713746` and `3fe86e22b`
  (verdicts above); the refuge.md #773 note it skipped can now cite the A2 rule.
- **WP143**: reconcile the A2 and B rule text and the B release-skill line with the 2026-11-03 rules review.
- **Review focus** (`/deep-review` before merge): A5's adapted `PlayerFought` (main party side vs
  capturer side; enlisted parked party gets no credit), C3's `SubModule` construction (argument order
  matches the constructor), C2's both-sides start (an already-started one-sided war stays started),
  B's hint texts and that no `TaomSettings` default moved.
- **Known behaviour kept**: when Full War declares its wars, vanilla asks each permanent ally that is
  not itself Hostile with the enemy to join (1.4.x data: Harad is Permanent with Mordor and Hostile
  with Gondor). `ReconcileDeclaredWars` runs at session launch, which on a load is before
  `MapEventManager.OnAfterLoad`; `DeclareWarAction` raises no map-event continuity update, and the
  existing load-time `CheckPhaseTransition` already declared wars at the same point.
- **A load re-runs the save's daily tick** on v1.4.8 (SAVE-TIME-08). The War of the Ring handlers are
  idempotent per day; the daily strength award is not, and it ships disabled (`momentum.json`
  `maxStrengthMomentum` 0).
- **Deferred, with reasons**: restricting the player gate to the player's own party (rejected:
  realm-wide credit is deliberate, #375); a bounded-momentum rebalance (balance, out of scope);
  persisting the campaign start ticks (WOTR-10, trigger unverified); the other features #773 names (only
  the momentum store is in this pack); `06252ae2c`'s one-line
  `MentionsOutsideComments` speed-up of `WarOfTheRingWiringTests` (optional, later).
