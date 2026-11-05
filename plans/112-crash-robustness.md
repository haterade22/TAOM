# Plan 112: Port the crash and NRE guards 1.5.x added on shared code paths

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. The plan has three Parts (A, B, C). Run them in that
> order on one branch; each Part can be its own session and ends with its own done criteria. When
> Part C is finished, write the hand-off as Step C3 says. Do not edit `plans/README.md`: it belongs
> to the integrator.
>
> **Drift check (run first, in your worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Composition Main/Adapters Main/Features/StaleCharacterRepair Main/Features/SupplyLines/SupplyCaravanService.cs Main/Features/AlignmentDesertion Main/Features/Messengers/MessengerCampaignBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs Main/Features/CareerSystem/Mutations/MutationParams.cs Main/Features/AdvancedCombat/Hooks Main/Features/Arena/Hooks Main/Features/ArmyTargeting/Hooks Main/Features/CharacterCreation/Hooks Main/Features/HeroRace/Hooks Main/Features/BanditManagement TAOM.Tests/Features/StaleCharacterRepair TAOM.Tests/Features/Messengers TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md .claude/rules/adapters.md .ai/review-reference.md`
> Expected changes from earlier WPs: `Main/SubModule.cs` (WP104 to WP106), `Main/Composition` (WP105),
> `MessengerCampaignBehaviorTests.cs` (WP104), Patch69 comments (WP106), the registry,
> `reflection-sites.md` and `.ai/review-reference.md` (WP103, WP106, WP107), if WP110 merged
> first, one comment in `EnlistmentBattleBehavior.cs`, and, if WP111 merged first, its
> campaign-statics paragraph in `.claude/rules/adapters.md`. STOP only on a real
> mismatch in a file this plan transplants (the per-step pre-checks name them).

## Status

- **Priority**: P0 (three P0 crash fixes: #1076, #824, #1064; the rest P1 to P2)
- **Effort**: L (8 commits in three Parts)
- **Risk**: MED (many shared files; two P0 fixes patch engine methods; every change is local and test-pinned)
- **Depends on**: plans/105-composition-root.md (WP105, which needs WP104) and plans/107-crash-capture-and-patchshield.md
  (WP107, which needs WP106 and brings `Dependencies/Foundation/RethrowStackPreserver.cs`).
  WP101 to WP107 are merged on your base.
- **Category**: bug (crash robustness)
- **Wave / lane**: wave 1, lane D
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`), D2 (module plus one `FeatureModules.All` line;
  `MainMenuStep` in `Main/Composition`), D7 (every `[ClientOnly]` dropped), D8 (`v2.0.28` subjects),
  D9 (no `RequiresGame`, `RequiresGameIL`, `LiveInstall` categories), D10, D12 (`84f0459bc` not
  ported), D19 (git through the Bash tool), D20 (no ModuleData touched)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: port of #1076, #824, #1064 (with #1068 and #750), #1063, #792 (with #765), #807 and
  #663 (plan 002). The NRE guard narrowing (`6ae924638`, `0002cb561`) carries no 1.5.x issue: the
  orchestrator creates one before Part B lands.

## Why this matters

Each change here closes a crash that the v1.4.8 engine and the 1.4.x code can reach today, or stops
a guard from hiding faults that are not its own. Talking to the tavern musician throws in a town
whose culture owns no other town (`TavernEmployeesCampaignBehavior.cs:650-651`), and this laptop's
installed TAOM_Map has seven such towns. A lord's child birth throws in
`CharacterObject.StealthEquipments` when a parent's saved character lost its culture (1.5.x crash
bundle 3fbdc0c5, same engine path and byte-identical StaleCharacterRepair code on 1.4.x). PR #1064
reported a 1.4.8 crash in the supply route's native path query, and 1.4.x can still hand that query
an off-navmesh face. The messenger's hourly tick null-checks `PartyBase.MainParty`, a getter that
throws while the main party is null (`PartyBase.cs:208-218`). A throw in TAOM's main-menu setup
re-runs every module's hook on every tick (`Module.cs:500-509`, `:750-759`). A TAOM and TAOM_Map
version skew, which the public channel makes possible, throws on every new game in
`GetInfestedHideoutCount`. Patch49 swallows every NullReferenceException in the army gathering
method and files unrelated faults as siege dead ends. After this WP each path falls back to vanilla
behaviour or one log line instead of a crash or a per-tick loop, and each guard swallows only the
fault it was written for.

## Current state

### Prerequisites from earlier work packages (verify in Step 0; absent means STOP)

| Prerequisite | From | Check |
|---|---|---|
| `Main/Composition/FeatureModules.cs` with `internal static readonly TaomFeatureModule[] All` (holds `WandererAllegianceModule`), `TaomFeatureModule`, `PatchCategoryDecl`, `ApplyPhase.GameInit` | WP105 (plan 018 end state `1efdf2985`) | `git grep -n "TaomFeatureModule\[\] All" -- Main/Composition/FeatureModules.cs` |
| `docs/features/composition-root.md` (how to port a 1.5.x module) | WP105 | file exists |
| `TryPatchCategory(` in `Main/SubModule.cs`; `Main/PatchCategoryIndex.cs` with `Build(Assembly)` and `Apply(Harmony, string)` | WP104 (plan 009) | `git grep -c "TryPatchCategory(" -- Main/SubModule.cs` is not 0 |
| `TAOM.Tests/Infrastructure/RepoPaths.cs` with `RepoPath(params string[])`, `ReadSource(string, bool)`, `StripComments(string)`, `MentionsOutsideComments(string, string, bool)` | WP104 | `git grep -n "MentionsOutsideComments\|ReadSource" -- TAOM.Tests/Infrastructure/RepoPaths.cs` |
| `Dependencies/Foundation/RethrowStackPreserver.cs` with `public static Exception? PreserveForRethrow(Exception? exception, MethodBase? rethrowSite)` and `ThrowSiteDataKey` | WP107 (from `c79a58521`) | `git grep -n "PreserveForRethrow(Exception" -- Dependencies/Foundation/RethrowStackPreserver.cs` |
| `IOutcomeLog` is NOT on the branch | WP139 lands later | `git grep -n "IOutcomeLog" -- Main` prints nothing |

### Files and their roles (on `origin/bannerlord-1.4.5`)

- `Main/Features/StaleCharacterRepair/`, `Main/Adapters/{StaleCharacterAdapter,IStaleCharacterAdapter}.cs`:
  the Patch83 save-load repair, identical to the parent of `1165a00de` (that diff, with
  `TAOM.Tests/Features/StaleCharacterRepair`, is empty). Constructor
  `StaleCharacterRepairService(IStaleCharacterAdapter adapter, IModLogger logger)` (`:67`); `SafeWarn`
  writes `[StaleCharacterRepair] <message>` (`:175-179`); Patch83 applies in `OnSubModuleLoad` inside a
  try (`Main/SubModule.cs:267-279`).
- `SupplyCaravanService.cs:844-882`: `ComputeNavPathPoints` passes the land rules (`1b00d1acd`) but
  hands `GetPathBetweenAIFaces` the faces unchecked. `AlignmentDesertionBehavior.cs`: two-argument
  constructor (`:26`), both handlers already return in a map event (`:49`, `:78`); no behavior test.
- `Main/SubModule.cs:525-528`: `OnBeforeInitialModuleScreenSetAsRoot` calls
  `IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();` unguarded.
- The five NRE guards (equal to the 1.5.x pre-image apart from co-op `[ClientOnly]` lines and
  `bd1ac8292`'s Patch69 refactor): Patch49 `Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:47-63`
  swallows every NRE; Patch50 `Agent_CheckToDropFlaggedItem_Guard_Patch.cs:96-98` returns non-NREs bare;
  `Patch69_TournamentEndGuard.cs:52-85` logs type and message only and returns non-NREs bare (WP106 adds
  comment lines); Patch56 `GauntletSceneNotification_OpenScene_Guard_Patch.cs:63-82` logs at DEBUG with
  no exception text and sets `CloseRequested` inside the logging try; Patch20
  `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:136-170` is a finalizer that swallows every
  NRE (`NarrativeHorseGuardPatchHelper` at `:238`).
- `MessengerCampaignBehavior.cs:607`: `if (PartyBase.MainParty == null) return false;`, reached hourly
  (`OnHourlyTick` `:194` -> `HandleArrivedMessenger` `:219` -> `:305`). `PartyBase.MainParty?.` reads:
  `EncounterAdapter.cs:86, 124, 140, 143`, `PartyItemRosterAdapter.cs:29, 44, 66, 88`; alias null checks:
  `PlayerEquipmentAdapter.cs:16`, `EnlistmentBattleBehavior.cs:252`, `TroopWeightDisplayHook.cs:238`. The
  six files equal the pre-images of `e9854bbd0` and `2c09a4145`.
- `MutationParams.cs:15-20`: `GetFloat` returns whatever `float.TryParse` parses, NaN and Infinity
  included; `FiniteFloatValidator.IsFinite(float)` exists. Plan 002's TroopWeight half is already here
  (`bee07b487`, `TroopWeightXmlLoader.cs:86`).
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: `EveryBoolPrefix_HasACoopDisposition`
  (`:362`) fails on any `static bool Prefix(` with no registry entry (pattern `:286-288`); the Patch20
  age-selection entry sits at `:246`, `TrySwitchToNextMenu_Patch` at `:248`.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: one `[DataTestMethod]` `[TestCategory("BindingVerification")]` (`:34-35`) with 45 rows; group comments at `:36` (inventory), `:46` (race selector), `:100` (Patch86 hideout rows `:103-105`).
- `docs/reference/harmony-patch-registry.md` headings: Patch20 `:137`, Patch49 `:410`, Patch50
  `:416`, Patch56 `:465`, Patch69_TournamentEndGuard `:627`, Patch83 `:890`, Patch88 `:990`,
  `Patch_MissionTime_SetMovementOrder` `:1004` (new sections go between these two).
- `.claude/rules/adapters.md:25` is the computed-getter paragraph, and on `1b00d1acd` it ends the block
  before `## Testing`. WP111 Step C1.5 brings `bb85e9284`'s "campaign statics" paragraph after it when
  WP111 merged first; `1b00d1acd` itself has no such paragraph and no hollow-character section.
  `.ai/review-reference.md:141-152` is the "Look harder
  here" list (last bullet: "A ratchet or suppression list with no multiplicity").
- `tools/snapshot_api_surface.ps1 -Check` cannot exit 0 on 1.4.x (the committed `patch-targets.md`
  ends with a backlinks footer; fixed by `4ab005dd1` in WP137). Do not use it as a gate here.

### Verified v1.4.8 engine facts (taom-src on the installed DLLs, 2026-11-04)

| Member | Signature or fact | Where |
|---|---|---|
| `SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition` | `private bool ...()`, body `:641-665`; one `Extensions.GetRandomElementWithPredicate<Settlement>(Settlement.All, ...)` at `:650`, `.Name` read unguarded at `:651`; registered as the `talk_bard` start-line condition at `:463` | taom-src |
| `TaleWorlds.Core.Extensions.GetRandomElementWithPredicate<T>` | `public static T GetRandomElementWithPredicate<T>(this MBReadOnlyList<T> e, Func<T, bool> predicate)` returns `default(T)` when nothing matches | `Extensions.cs:288` |
| `Settlement.All`, `Settlement.CurrentSettlement` | `public static MBReadOnlyList<Settlement> All => Campaign.Current.Settlements;` ; `public static Settlement CurrentSettlement` | `Settlement.cs:483`, `:463` |
| `HeroCreator.DeliverOffSpring` | `public static Hero DeliverOffSpring(Hero mother, Hero father, bool isOffspringFemale)` (`:258`), calls `CreateHero` `:265`; `CreateHero` (`:283`) logs "creating hero from template" `:287`, `CharacterObject.CreateFrom` `:288`, `new Hero` `:294` | `HeroCreator.cs` |
| `BasicCharacterObject` | `FillFrom` copies `_culture` (`:193-195`); `public BasicCultureObject Culture { get; set; }` (`:124-134`); `public int Race { get; set; }` (`:76`); `BodyPropertyRange` protected set (`:44`); vanilla body fallback `:472-476` | `BasicCharacterObject.cs` |
| `CharacterObject` | `private CharacterObject _originCharacter;` (`:24`); `OriginalCharacter` `:84`; `IsOriginalCharacter` `:86`; `StealthEquipments` `:136-150` dereferences `Culture` at `:144` when not a hero; `Culture` returns `HeroObject.Culture` on a hero (`:258-272`); `public void InitializeHeroCharacterOnAfterLoad()` `:519` | `CharacterObject.cs` |
| `Hero` | `SetCharacterObject` calls `SetInitialValuesFromCharacter` (`:1238`) before `HeroObject = this` (`:1239`); `SetInitialValuesFromCharacter` (`:2237`) reads `StealthEquipments` at `:2256` | `Hero.cs` |
| `MBObjectBase.IsRegistered` | `[CachedData] [SaveableProperty(3)] internal bool IsRegistered` | `MBObjectBase.cs:23-25` |
| `Army.FindBestGatheringSettlementAndMoveTheLeader` | `private void ...(Settlement focusSettlement)` `:652-728`; kingdomless dead end `Kingdom.Settlements` `:659` (inside `leaderHero.IsActive` `:657`); fallback `SettlementHelper.FindNearestFortificationToMobileParty(LeaderParty, LeaderParty.NavigationCapability)` `:723`; `AiBehaviorObject = settlement;` `:725`; `settlement.GatePosition` `:726`. Guards on `AiBehaviorObject` at `:480`, `:564`; unguarded reads `:518`, `:574-605`, `:753-754` | `Army.cs` |
| `SettlementHelper.FindNearestFortificationToMobileParty` | `public static Settlement ...(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)` | `Helpers.SettlementHelper.cs:245` |
| `MobileParty` | `public NavigationType NavigationCapability` `:465`; `enum NavigationType { None = 0, Default = 1, Naval = 2, All = 3 }` `:37-43`; `ComputePath` checks `Position.IsValid()` and `newTargetPosition.IsValid()` `:3693-3698` | `MobileParty.cs` |
| `Agent.CheckToDropFlaggedItem` callers | `OnMount` `:1747`, `OnDismount` `:1772`; `Mission.OnAgentHit` `:5621` | `Agent.cs`, `Mission.cs` |
| Tick handshake | `tickCompleted = true` field `:1040`; `WaitTickCompletion` spins `:3585-3591`; `TickAgentsAndTeamsImp` `:3601` sets it `:3613`; `OnTick` clears it `:3740` | `Mission.cs` |
| `SandBox.Tournaments.MissionLogics.TournamentBehavior` | `OnMissionTick` `:149` calls `EndCurrentMatch` `:155`; prize `:247`, leaderboard `:248`, `OnTournamentFinished` `:256`, `TournamentEnd` raised `:257-259` | taom-src |
| `CharacterCreationNarrativeStageView.SpawnNonHumanNarrativeMenuCharacter` | `private void ...(NarrativeMenuCharacter character)` `:194`; `ClearMountEntities()` `:203`; reads spawn point `:204`, item ids, `HorseComponent.Monster`, `MBGlobals.GetActionSet(...ActionSetCode)` `:209`, `MountCreationKey` `:214`, frame `:215`; creates the mount entity at `:208`; `private Scene _characterScene;` `:39` | taom-src |
| `NarrativeMenuCharacter` | `StringId` `:7`, `SpawnPointEntityId` `:11`, `MountCreationKey MountCreationKey` `:23`, `Item1Id` `:25`, `Item2Id` `:27` | taom-src |
| `MBGlobals.GetActionSet(string)` | dictionary `TryGetValue` (null key throws `ArgumentNullException`), then `throw new Exception("Invalid action set code")` on an invalid set | `MBGlobals.cs:33-46` |
| `BanditSpawnCampaignBehavior` | `private Dictionary<CultureObject, List<Hideout>> _hideouts` `:23`; `CacheHideouts` fills it from `Hideout.All` `:104-114`; `public MobileParty AddBanditToHideout(Hideout hideoutComponent, PartyTemplateObject overridenPartyTemplate = null, bool isBanditBossParty = false)` `:312`, null clan dereferenced at `clan.DefaultPartyTemplate` `:324`; `AddNewHideouts` weight `1 - n/max` `:285`; `AddBossParty` `:199` dereferences the result `:204`; `private int GetInfestedHideoutCount(Clan banditFaction)` `:490`, unguarded indexer `:493`; new-game stage 11 `:96-99` | taom-src |
| Gold Rush incident | `IncidentEffect.InfestNearbyHideout(Func<Settlement>)` (`IncidentEffect.cs:681`), used by `incident_gold_rush` (`IncidentsCampaignBehaviour.cs:1786`) | v1.4.8 dump |
| `TaleWorlds.CampaignSystem.SandBoxManager.InitializeSandboxXMLs` | loads `Factions` only when not a saved campaign (`:374`), `Settlements` always in campaign mode (`:380`) | v1.4.8 dump `SandBoxManager.cs:360` |
| `PartyBase.MainParty` | `if (Campaign.Current == null) return null; return Campaign.Current.MainParty.Party;` | `PartyBase.cs:208-218` |
| `Clan.PlayerClan` | `=> Campaign.Current.PlayerDefaultFaction` (throws with no campaign) | `Clan.cs:276` |
| `TaleWorlds.CampaignSystem.Map.IMapScene` | `bool GetPathBetweenAIFaces(PathFaceRecord startingFace, PathFaceRecord endingFace, Vec2 startingPosition, Vec2 endingPosition, float agentRadius, NavigationPath path, int[] excludedFaceIds, float extraCostMultiplier, int regionSwitchCostFromLandToSea, int regionSwitchCostFromSeaToLand);` `:34` | `IMapScene.cs` |
| `PathFaceRecord` | `new PathFaceRecord(int index, int groupIndex, int islandIndex)`; `NullFaceRecord` = (-1,-1,-1); `IsValid() => FaceIndex != -1` | `PathFaceRecord.cs:11-23` |
| `NavigationPath` | `public int Size { get; set; }`, indexer `:15`, `OverridePathPointAtIndex(int index, in Vec2 newValue)` `:45` | taom-src |
| `Module` | `OnApplicationTick` `:463` calls `SetInitialModuleScreenAsRootScreen()` while no state is active (`:500-509`); that method calls every module's `OnBeforeInitialModuleScreenSetAsRoot` in a loop with no try (`:750-759`) | v1.4.8 dump `Module.cs` |

Step A3's desertion test stages engine objects through members that are plain field reads on v1.4.8:
`Clan._kingdom` (`:37`), `PartyBase._mapEventSide` (`:36`) and `MapEvent => _mapEventSide?.MapEvent`
(`:287`), `MapEventSide._mapEvent` (`:58`), `Hero._clan` (`:103`), `MobileParty._partyComponent` (`:254`),
`Party`/`IsLordParty` private setters (`:354`, `:1238`), `PartyComponent.Leader` virtual (dump
`PartyComponent.cs:27`), `Fief.GarrisonPartyComponent` field (dump `Fief.cs:12`), `Settlement.Town`
field (`:84`), `Town._ownerClan` (`:77`).

### Line-number map: 1.5.x comment citations to v1.4.8

Every ported comment and doc line that cites an engine line must use the v1.4.8 number.

| File | 1.5.x text | v1.4.8 |
|---|---|---|
| `Army.cs` | 654-731, 661, 727, 728, 482, 566, 520, 576-607, 755 | 652-728, 659, 725, 726, 480, 564, 518, 574-605, 753 |
| `Mission.cs` | 3749, 3594-3599 | 3740, 3585-3591 |
| `TournamentBehavior.cs` | 252-264 | 247-259 |
| `Agent.cs`, `Mission.cs` (Patch50 note) | 1781, 1806, 5677 | 1747, 1772, 5621 |
| `TavernEmployeesCampaignBehavior.cs` | 415-435; maid lines 485-535 | 641-665; the DRINK and FOOD block starts at 774 |
| `BanditSpawnCampaignBehavior.cs` | 499, 331, 211 | 493, 324, 199 and 204 |
| `Module.cs` | 540-549, 790-799 | 500-509, 750-759 |
| `CharacterObject.cs` | 150 | 144 |
| `PartyBase.cs` | 209-219 | 208-218 |
| `BasicCharacterObject.cs` | 477-481 | keep 1.4.x's 472-474 |
| Narrative stage view | v1.5.5 :203, :204-215, :208 | same numbers, label them v1.4.8 |

### Conventions that bind this work

- ADR-007 adapters: services never touch sealed TaleWorlds types; `StaleCharacterRepairService` sees
  only `IHeroCharacterStateAdapter` (pattern: `Main/Adapters/StaleCharacterAdapter.cs`).
- ADR-002 thin entry points: patch classes delegate decisions to a pure helper
  (`HideoutClanGuard`, `BardTownPickHook`, `NarrativeHorseGuardPatchHelper.CanSpawnMount`,
  `Patch69_TournamentEndGuard.Settle`). Patch20 and Patch56 files were already over 150 lines
  (multi-class) before this WP; do not split them here.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration;
  TDD (each step takes its failing test first); the MCM json2 rule (rename a persisted setting,
  never flip its default) is untouched, since no setting changes.
- `.claude/rules/harmony-patches.md`: research the target first (done above), keep patches thin,
  read the patch's registry section before editing it; the `MovementOrder` rule does not apply here.
- A value-returning finalizer that hands an exception back must return it through
  `RethrowStackPreserver.PreserveForRethrow`, or Harmony's `throw` erases the throw site
  (the class summary in `Dependencies/Foundation/RethrowStackPreserver.cs` says why).
- D2 porting rule (`docs/features/composition-root.md`, WP105): port the 1.5.x `XModule.cs` as is,
  append one line to `FeatureModules.All`, keep its wiring tests; a module with `OwnsSaveData = true`
  is a STOP.

## Commands you will need

Run from the worktree root. Both `-p:` flags are mandatory on every build and test
(`Main/TAOM.csproj:133-136` does not gate CopyModule on DisableModuleCopy). Never `./build.ps1`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is gone after WP104) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | 0 failed, the passed count the step names (each `DataRow` counts as one result) |
| Docs | `python tools/lint_docs.py` | no new finding in the files you touched |
| Dash scan | the command below | no output |

```bash
git diff bannerlord-1.4.5...HEAD -- docs .claude .ai | python -c "import sys; [print(l) for l in sys.stdin.buffer.read().decode('utf-8','replace').splitlines() if l.startswith('+') and (chr(0x2013) in l or chr(0x2014) in l)]"
```

Both test commands use `--no-build`: run the Build command first after every edit, or the run tests
the old binaries. Every "Verify" below that gives a `--filter` means Build, then the Filtered command.

`BANNERLORD_GAME_DIR` must be set (the laptop sets it): the `BindingVerification` and game-loading
tests go Inconclusive without it, and Inconclusive is not a pass here.

## Scope

**In scope**: exactly the files each step names, which are, in summary:

- Part A: `Main/Features/StaleCharacterRepair/**`, `Main/Adapters/{StaleCharacterAdapter,IStaleCharacterAdapter}.cs`
  and the new `Main/Adapters/{HeroCharacterStateAdapter,IHeroCharacterStateAdapter}.cs`; the new
  `Main/Features/BardTownGuard/**` plus one line in `Main/Composition/FeatureModules.cs`;
  `Main/Features/SupplyLines/SupplyCaravanService.cs`; their tests, including the new
  `SupplyNativeQueryTests.cs` and `AlignmentDesertionMapEventGateTests.cs`; the docs
  `stale-character-repair.md`, `bard-town-guard.md` (new), `supply-lines.md`, `alignment-desertion.md`.
- Part B: the five guard files; `Main/SubModule.cs` (single owner: the Patch49 comment, one Patch125
  block, the `MainMenuStep` call and its logger helper); the new
  `Main/Features/BanditManagement/{HideoutClanGuard.cs,Hooks/Patch125_HideoutClanGuard.cs}` and
  `Main/Composition/MainMenuStep.cs`; eight new test files; one `CoopVetoClassificationTests` entry;
  `bandit-management.md`, `main-menu-customizer.md`.
- Part C: the six main-party files, `MutationParams.cs`, the new `MainPartyNullCheckScanTests.cs` and
  `MutationParamsTests.cs`, one method in `MessengerCampaignBehaviorTests.cs`, `career-system.md`.
- All Parts: `ReflectionSiteBindingTests.cs` rows, `harmony-patch-registry.md`, `reflection-sites.md`,
  `.claude/rules/adapters.md` (two paragraphs on the exact mechanisms ported), `.ai/review-reference.md`
  (two bullets).

**Single-owner files**: `Main/SubModule.cs` is edited only in Part B, at the three places named.
`Main/IoC.cs`, `Main/TAOM.csproj` (SDK-style, new files compile by glob) and
`Directory.Build.props` are NOT edited. If a step seems to need any of them, STOP.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `plans/README.md` (the integrator's);
`docs/reviews/lessons/*`,
`docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`,
`docs/reference/feature-map.md`, `tools/README.md` (WP142 to WP144 sync these); every RCA, review
record and Codex prompt file the 1.5.x commits add; `docs/reference/taleworlds-api-snapshot/patch-targets.md`
(generated; WP137 regenerates it after fixing `-Check`); `.ai/verification.md` and
`.claude/skills/verify/SKILL.md` (the api-snapshot row and Step 2d belong to WP137's Part D step); anything under
`Main/_Module/ModuleData`; `tools/`; every Feral Orcs, Fire and Tribute, Unlooked-for Aid,
Encyclopedia LastSeenText (Patch123), AutoResolveDiagnostics, career-quest and HeroRace hunk of
`6ae924638`; `Client/`; `.claude/settings*.json`.

## Git workflow

1. Worktree, from the main checkout (Bash tool, D19):
   `git worktree add C:/Users/mikew/source/repos/taom-port/112 -b port148/112-crash-robustness bannerlord-1.4.5`
   (forward slashes: Git Bash eats unquoted backslashes).
   Work only there. Do not touch `stash@{0}` or `.serena/project.yml` in the main checkout.
2. Per-port procedure (decisions brief). For a whole commit, `git cherry-pick -n <sha>`. This WP's
   commits are mostly PORT-PARTIAL with many out-of-scope files, so it uses two equivalent forms that
   take only in-scope paths:
   - **Path-limited pick**: `git diff <sha>^ <sha> -- <paths> | git apply --3way`. It is
     `cherry-pick -n` restricted to those paths. On a conflict it leaves markers in the file:
     resolve as the step says, then `git add <file>`.
   - **End-state transplant**: `git checkout <sha> <paths>`, only after the step's pre-check shows
     the file is unchanged since `1b00d1acd` (so the checkout cannot drop another WP's work), or for
     a file new on 1.4.x. Write it with no `--`: since WP103 `block-dangerous-git.sh` stops for a
     confirmation on the `--` form, and this form does the same thing and also stages the file.
   Never let `cherry-pick`, `--continue` or `--amend --no-edit` create a commit. If git reports a
   cherry-pick in progress, run `git cherry-pick --quit` (keeps your index and worktree).
3. Re-cite every ported engine line to v1.4.8 with the map above; drop every `[ClientOnly]`; drop
   every `[TestCategory("RequiresGame")]`, `[TestCategory("RequiresGameIL")]` and
   `[TestCategory("LiveInstall")]` line (D9; keep `BindingVerification`).
4. Commit: `git add <explicit paths>` (never `-A`, `-u` or `.`): exactly the files the step created or
   edited, which `git status --porcelain` must list and nothing else; then in the NEXT, separate Bash
   call `git commit -F <message file>`. Write the message file outside the repo (for example in your user
   temp folder). Never through PowerShell.
5. Build and run the step's filtered tests after each commit; full suite at the end of each Part.
6. No push, PR, tag or merge. The integrator merges the branch.

Commit message template (the steps give each subject and body):

```
<type>(<scope>): v2.0.28 - <description>        (at most 72 characters)

<body for 1.4.x readers, wrapped at 72, no em or en dashes, no
Co-Authored-By or "Generated with" line>

Research: <what was verified on v1.4.8>
Not-tested: <the in-game check owed>
Ported-from: <40-hex sha>                       (one line per 1.5.x commit)
```

## Step 0: Prepare and check prerequisites (no commit)

```bash
cd /c/Users/mikew/source/repos/taom-port/112
git log -1 --format='%h %s'
git grep -n "TaomFeatureModule\[\] All" -- Main/Composition/FeatureModules.cs
git grep -c "TryPatchCategory(" -- Main/SubModule.cs
git grep -n "MentionsOutsideComments\|ReadSource" -- TAOM.Tests/Infrastructure/RepoPaths.cs
git grep -n "PreserveForRethrow(Exception" -- Dependencies/Foundation/RethrowStackPreserver.cs
git grep -n "IOutcomeLog" -- Main ; echo "IOutcomeLog grep exit $?"
dotnet restore TAOM.sln && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=
```

**Verify**: the four greps each print at least one line; the `IOutcomeLog` grep prints nothing
(exit 1); build exit 0; tests 0 failed. Record the total as `BASE_TOTAL` for the done criteria.

## Part A: P0 crash fixes (Steps A1 to A3)

### Step A1: Repair a hollow parent before a birth (`1165a00de` + `de90a2451`, end-state transplant, IOutcomeLog swapped for IModLogger)

**Pre-check** (must print nothing):
`git diff --stat 1b00d1acd HEAD -- Main/Features/StaleCharacterRepair Main/Adapters/StaleCharacterAdapter.cs Main/Adapters/IStaleCharacterAdapter.cs TAOM.Tests/Features/StaleCharacterRepair`

Simulation (`git merge-tree` onto `1b00d1acd`): `1165a00de` conflicts in `adapters.md`,
`ReflectionSiteBindingTests.cs`, docs and the absent verdict-tool files; `de90a2451` also in every file
`1165a00de` creates. The transplant below avoids all of them.

1. **Tests first (RED).**
   ```bash
   git checkout de90a2451 TAOM.Tests/Features/StaleCharacterRepair/HeroCharacterStateAdapterTests.cs TAOM.Tests/Features/StaleCharacterRepair/Patch83HooksHarmonyTests.cs TAOM.Tests/Features/StaleCharacterRepair/StaleCharacterParentRepairTests.cs TAOM.Tests/Features/StaleCharacterRepair/Patch83StaleCharacterRepairBindingTests.cs
   ```
   Do NOT take `StaleCharacterRepairServiceTests.cs` (its only 1.5.x change adds the IOutcomeLog
   constructor argument). Then edit:
   - Delete the `[TestCategory("RequiresGame")]` line in `HeroCharacterStateAdapterTests.cs` and
     `Patch83HooksHarmonyTests.cs`.
   - Rewrite `StaleCharacterParentRepairTests.cs` to assert IModLogger lines (new work; the production
     format is fixed in item 2). Remove the `_outcomes` field and its setup; construct
     `new StaleCharacterRepairService(_adapter, _logger)`. Add
     `private static string CheckLine(string id, string result, string detail) => $"[Check] id={id} result={result} {detail}";`
     and `using System.Text.RegularExpressions;`. Replace by this table:

     | 1.5.x assertion | 1.4.x assertion |
     |---|---|
     | `OutcomeLog.IsValidId(x)` | `Regex.IsMatch(x, @"^[a-z0-9]+(\.[a-z0-9-]+)+$")` (the 1.5.x id rule, kept so WP139 can switch) |
     | `_outcomes.Received(1).Pass(ID, key, "d")` | `_logger.Received(1).LogInfo(CheckLine(ID, "pass", "d"))` |
     | `_outcomes.Received(1).Fail(ID, key, "d")` | `_logger.Received(1).LogWarning(CheckLine(ID, "fail", "d"))` |
     | `_outcomes.Received(1).Fail(ID, key, Arg.Is<string>(d => d.Contains(X)))` | `_logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Check] id=" + ID + " result=fail ") && m.Contains(X)))` |
     | `_outcomes.DidNotReceive().Pass(...)` | `_logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains(" result=pass")))` |
     | `_outcomes.DidNotReceive().Fail(...)` | `_logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains(" result=fail")))` |
     | `_outcomes.Received(1).Swallowed("StaleCharacterRepair", "SITE", Arg.Any<E>())` | `_logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Swallowed] feature=StaleCharacterRepair site=SITE first=E")))` with `E` the exception type name |
     | `Assert.AreEqual(0, _outcomes.ReceivedCalls().Count(), ...)` | `Assert.AreEqual(0, _logger.ReceivedCalls().Count(), ...)` |
     | `Warnings().Single()` (the three census tests) | `Warnings().Single(w => w.Contains("census:"))`, because a swallowed `DescribeState` now also writes a warning |
   - `ReflectionSiteBindingTests.cs`: after the `_inventoryLogic` row (inventory group, `:38`) add
     `    // --- A hollow parent takes its registered XML twin as origin (HeroCharacterStateAdapter.cs, #1076) ---`
     and `    [DataRow("TaleWorlds.CampaignSystem.CharacterObject", "CharacterObject", "_originCharacter", "Field", "HeroCharacterStateAdapter.cs:13")]`.

   **Verify (RED)**: the build fails with missing `IHeroCharacterStateAdapter`, `FindHollowHeroCharacters`
   and related members. That compile failure is the red state.

2. **Code (GREEN).**
   ```bash
   git checkout de90a2451 Main/Adapters/HeroCharacterStateAdapter.cs Main/Adapters/IHeroCharacterStateAdapter.cs Main/Adapters/IStaleCharacterAdapter.cs Main/Adapters/StaleCharacterAdapter.cs Main/Features/StaleCharacterRepair/Hooks/Patch83_OffspringParentRepair.cs Main/Features/StaleCharacterRepair/Hooks/Patch83_StaleCharacterRepair.cs Main/Features/StaleCharacterRepair/IStaleCharacterRepairService.cs Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs
   ```
   Then edit:
   - `StaleCharacterAdapter.cs`: both `BasicCharacterObject.cs:477-481` become `BasicCharacterObject.cs:472-474`.
   - `Patch83_OffspringParentRepair.cs`: in the summary, `at <c>CharacterObject.cs:150</c>` becomes
     `at <c>CharacterObject.cs:144</c> (v1.4.8)`.
   - `StaleCharacterRepairService.cs` (the swap): delete `private readonly IOutcomeLog _outcomes;`, the
     third constructor parameter and `_outcomes = outcomes;` (constructor back to
     `(IStaleCharacterAdapter adapter, IModLogger logger)`, so IoC needs no change). Replace every
     `_outcomes.Pass(ID, key, detail)` with `Check(ID, "pass", detail)`, every
     `_outcomes.Fail(ID, key, detail)` with `Check(ID, "fail", detail)`, and every
     `_outcomes.Swallowed("StaleCharacterRepair", site, ex)` with `Swallowed(site, ex)`. Add above the
     closing brace of the class:
     ```csharp
     // The [Check] and [Swallowed] line formats of the 1.5.x outcome log, written through IModLogger
     // until that service reaches this line (WP139), so the in-game verdict reader can already read them.
     private void Check(string id, string result, string detail)
     {
         try
         {
             var line = $"[Check] id={id} result={result}" + (string.IsNullOrEmpty(detail) ? string.Empty : " " + detail);
             if (result == "fail") _logger?.LogWarning(line);
             else _logger?.LogInfo(line);
         }
         catch
         {
             // A failed log line must never block a birth or a save load.
         }
     }

     private void Swallowed(string site, Exception ex)
     {
         try
         {
             _logger?.LogWarning(
                 $"[Swallowed] feature=StaleCharacterRepair site={site} first={ex?.GetType().Name ?? "(none)"}. Root cause: {ex}");
         }
         catch
         {
             // Logging is already the fallback path.
         }
     }
     ```
3. **Docs.**
   - `docs/features/stale-character-repair.md`:
     `git diff 1165a00de^ de90a2451 -- docs/features/stale-character-repair.md | git apply --3way`
     (the 1.4.x file lacks a 6-line #670 note; that hunk is unrelated). Then: keep `:472-474` where the
     hunks write `:477-481`; in the header keep crash bundle `3fbdc0c5` but write "engine v1.5.6, same
     path on v1.4.8"; delete the `HollowParent1076Tests` table row and the RCA link sentence ("see
     [the RCA](...)") (the RCA is not on 1.4.x); delete the two Coop sentences; replace the
     `tools/ingame_checks/1076.toml` mentions with "the `[Check]` lines in `taom_debug_*.log`
     (`tools/ingame_checks/1076.toml` arrives with WP139)". Replace the five-row table with:

     | # | Where (v1.4.8) | What it does |
     |---|---|---|
     | 1 | `PregnancyCampaignBehavior.CheckOffspringToDeliver` | calls `HeroCreator.DeliverOffSpring` |
     | 2 | `HeroCreator.cs:265` and `:288` | `CreateHero` clones the template, which `TaomHeroCreationModel` makes the same-sex parent's character, through `CharacterObject.CreateFrom` |
     | 3 | `BasicCharacterObject.cs:193-195` | `FillFrom` copies the raw `_culture` onto the clone |
     | 4 | `Hero.cs:1238-1239`, `:2256` | `SetInitialValuesFromCharacter` reads `StealthEquipments` before `HeroObject` is set |
     | 5 | `CharacterObject.cs:144` | `Culture.DefaultBattleEquipmentRoster` on a null culture throws |

     Drop the engine line numbers the doc cites for `Helpers.CharacterHelper`, `Campaign.cs`,
     `SandBoxManager.cs` and `DefaultHeroCreationModel.cs` (v1.5.6 lines, not re-checked); keep the
     sentences. Replace "Both parents are repaired because `DefaultHeroCreationModel.cs:155` ...
     race." with "Both parents are repaired because either can be the template
     (`TaomHeroCreationModel.GetCharacterTemplateForOffspring` returns the mother's character for a
     girl and the father's for a boy)."
   - `docs/reference/harmony-patch-registry.md`, `## Patch83_StaleCharacterRepair` Target line: append
     ` + HeroCreator.DeliverOffSpring(Hero, Hero, bool) (public static, void Prefix with out bool __state and a void Finalizer, Patch83_OffspringParentRepair, #1076: repairs a hollow parent before the birth clones it; shares the method with Patch13_RaceAge's transpiler DeliverOffSpring_RaceAssert_Patch; applied with the rest of Patch83 at OnSubModuleLoad; [doc](../features/stale-character-repair.md))`
     (format the type and method names as code spans, as the line already does).
   - `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: add as the first data row of the
     Category B table (before the `_currentCharacter` row, `:31`):
     `` | `TaleWorlds.CampaignSystem.CharacterObject` | `_originCharacter` | field | `HeroCharacterStateAdapter.cs:13` | StaleCharacterRepair: a hollow parent takes its registered XML twin as origin | ``
   - `.claude/rules/adapters.md`: as the last paragraph before `## Testing`, after whichever paragraph
     ends the block (the computed-getter paragraph `:25`, or WP111's campaign-statics paragraph if WP111
     merged first; keep it), add:
     ```markdown
     **A hero's character can be hollow (MANDATORY before reading a raw character field).** `IsRegistered` is saved (v1.4.8 `MBObjectBase.cs:23-25`), so a hero can keep a `CharacterObject` that XML never reaches while XML builds a new twin of its id. `CharacterObject.Culture` answers from the hero (`CharacterObject.cs:258-272`) and hides a null raw culture until `BasicCharacterObject.FillFrom` copies it onto a clone (`BasicCharacterObject.cs:193-195`). Read the raw culture through `BasicCharacterObject` and the raw name through its field (`Name` is virtual, so a cast does not help), and before calling a repair done, trace every member the protected method reads after the first throw ([repair](../../docs/features/stale-character-repair.md), #1076).
     ```
   - `.ai/review-reference.md`: append the `de90a2451` bullet ("A load-time engine method called at
     another time", three lines; `git show de90a2451 -- .ai/review-reference.md`) after the last bullet
     of the "Look harder here" list (`:152` on `1b00d1acd`).

**Verify**:
- Build exit 0.
- `--filter "FullyQualifiedName~StaleCharacterParentRepairTests|FullyQualifiedName~HeroCharacterStateAdapterTests|FullyQualifiedName~Patch83|FullyQualifiedName~StaleCharacterRepairServiceTests"`:
  0 failed, 76 passed (15 + 19 + 7 + 16 + 19).
- `--filter "FullyQualifiedName~ReflectionSiteBindingTests"`: 0 failed, one more passed than before.
- `git grep -n "OutcomeLog" -- Main TAOM.Tests` prints nothing; `git grep -n "RequiresGame" -- TAOM.Tests/Features/StaleCharacterRepair` prints nothing.

**Commit** (paths: the 8 production files, the 4 test files, `ReflectionSiteBindingTests.cs`,
`docs/features/stale-character-repair.md`, `reflection-sites.md`, the registry, `.claude/rules/adapters.md`,
`.ai/review-reference.md`):

```
fix(stale-character): v2.0.28 - repair a hollow parent before a birth

A lord's child birth could crash the campaign map when a parent's
character came back from a save with no culture of its own, a hollow
character that XML never reached. The birth copies the missing culture
onto the child, and the game throws while it builds the child.

Before each birth TAOM now repairs a hollow parent: from the
registered character of the same id when there is one, keeping a race
restored at session launch, otherwise from the hero's own or clan
culture. It never invents a culture. A census line at load names every
hollow hero, and [Check] lines record each repair and whether the
birth finished.

Research: taom-src HeroCreator.DeliverOffSpring, CharacterObject,
BasicCharacterObject.FillFrom, Hero.SetCharacterObject (v1.4.8)
Not-tested: a birth with a hollow parent in a real campaign
Constraint: no IOutcomeLog on this line; IModLogger, same line format
Save-compat: writes _originCharacter on a hollow parent only
Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e
```

### Step A2: Talking to a musician no longer crashes (`eed06b9c7`, ported through a feature module, D2)

Simulation: conflicts in `FeatureModules.cs`, `docs/INDEX.md`, `feature-map.md`, the registry,
`patch-targets.md` and a lesson; the new feature files apply cleanly.

1. **Tests first (RED).** `git checkout eed06b9c7 TAOM.Tests/Features/BardTownGuard`. In
   `BardTownGuardBindingTests.cs` delete every `[TestCategory("RequiresGameIL")]` and
   `[TestCategory("RequiresGame")]` line (keep `BindingVerification`). Keep
   `BardTownGuardWiringTests.cs` as is (D2). **Verify (RED)**: build fails (missing
   `BardTownGuardModule`, `BardTownPickHook`, `BardTownTranspiler`).
2. **Code (GREEN).** `git checkout eed06b9c7 Main/Features/BardTownGuard docs/features/bard-town-guard.md`.
   - `Main/Composition/FeatureModules.cs`: append `        new Features.BardTownGuard.BardTownGuardModule(),`
     as the LAST entry of `All`.
   - `Hooks/TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.cs` summary:
     `(v1.5.5, TavernEmployeesCampaignBehavior.cs:415-435)` becomes `(v1.4.8, TavernEmployeesCampaignBehavior.cs:641-665)`.
   - Do not take the RCA, `docs/INDEX.md`, `feature-map.md`, the lesson or the `patch-targets.md` row.
3. **Docs.**
   - `docs/features/bard-town-guard.md`: vanilla behavior line becomes `(private, v1.4.8,
     TavernEmployeesCampaignBehavior.cs:641-665; the pick is at :650 and .Name is read at :651)`
     (code spans as in the file); "When it runs" becomes "the condition of the `talk_bard` start line
     (`:463`); only a musician reaches the pick"; "Without this feature" lists the seven towns on this
     laptop's installed TAOM_Map (2026-09-07): `town_DG1`, `town_GT1`, `town_isengard`, `town_L1`,
     `town_LN1`, `town_R1` and `town_GBC1` (cultures dolguldur, goblin, isengard, lothlorien, lindon,
     rivendell, bluecraig); Known gaps cites the DRINK and FOOD block at `:774`; the In-game check
     lists the same seven towns (Gondor still has 11 towns there); Changelog line
     `- 2026-11-04: ported to the 1.4.x line from 1.5.x eed06b9c7 (#824).`
   - Registry: add a `## Patch110_BardTownGuard` section after the Patch88 section and before
     `## Patch_MissionTime_SetMovementOrder`. Take the `eed06b9c7` section text
     (`git show eed06b9c7:docs/reference/harmony-patch-registry.md`) with these edits: target cited as
     v1.4.8 `TavernEmployeesCampaignBehavior.cs:641-665`, declared by `BardTownGuardModule` and applied
     at GameInit by the feature-module runner; vanilla line cited as `:650-651`; "Callers and thread":
     the condition of the `talk_bard` start line (`:463`), main thread; delete the whole PatchShield
     paragraph (its claims are about 1.5.x PatchShield); keep the Co-op sentence (a transpiler, so
     `CoopVetoClassificationTests` needs no entry); end with `#824 (ported 2026-11-04 from 1.5.x eed06b9c7). **ACTIVE**, not yet checked in game.`

**Verify**: build exit 0; `--filter "FullyQualifiedName~BardTownGuard|FullyQualifiedName~FeatureModulesTests"`:
0 failed, the 11 BardTownGuard results pass (7 + 4) and every `FeatureModulesTests` case passes;
`git grep -n "Patch110_BardTownGuard" -- Main/SubModule.cs` prints nothing.

**Commit**:

```
fix(taverns): v2.0.28 - talking to a musician no longer crashes

Talking to the tavern musician crashed the game in a town whose
culture owns no other town: his song names a random town of the same
culture, and with none to pick the game read the name of nothing. On
the 1.4.8 map checked for this port that is seven towns, among them
Dol Guldur, Isengard, Lothlorien and Rivendell.

The musician now picks the town the game would have picked; only when
there is none does he name another town, and failing that his own. His
songs are unchanged. If a game update changes his code, the fix stands
down and logs one warning.

Research: taom-src conversation_talk_bard_on_condition (:641-665),
Extensions.GetRandomElementWithPredicate (:288)
Not-tested: the musician in the seven single-town cultures
Ported-from: eed06b9c70bc562a4a77d0b71ed08260940d48f2
```

### Step A3: Supply path query skips off-navmesh endpoints; pin the desertion battle gate (`ab78564e0`, partial)

What 1.4.x already has (do not redo): the land rules in `ComputeNavPathPoints`, the arrows'
heightmap check in `SupplyRouteVisualService.cs:285-289`, and both desertion map-event returns
(all from `1b00d1acd`). Simulation: conflicts in `SupplyCaravanService.cs`,
`SupplyRouteVisualService.cs`, `AlignmentDesertionBehaviorTests.cs` (absent), docs and lessons.

1. **Tests first (RED).** `git checkout ab78564e0 TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs`,
   then delete `[TestCategory("RequiresGame")]`, the helpers `FingerprintOf` and `IndexOf`, and the
   tests `ComputeNavPathPoints_ReadsTheActiveModelsLandRules_ForAddLandPath` and
   `SyncArrows_BranchesOnTheHeightmap_BeforeTheTerrainQuery_AndFallsBackToVanillaLandHeight` (both
   need `IlCallScanner.Fingerprint`, which arrives with WP118); remove the usings that become unused
   (`System.Linq`, `System.Reflection`, `TAOM.Tests.Migration`); rewrite the summary to say the file
   drives `AddLandPath` with a fake `IMapScene` and that the IL pins for the caller and the arrows
   come with WP118. Three test methods remain (6 results).
   **Verify (RED)**: build fails (`SupplyCaravanService.AddLandPath` missing).
2. **Code (GREEN).** In `SupplyCaravanService.cs` add `using TaleWorlds.CampaignSystem.Map;` after
   `using TaleWorlds.CampaignSystem.Actions;`, replace the body of the `if (mapScene != null)` block
   in `ComputeNavPathPoints` (keep the method's try, catch and two-point fallback) so the method reads:
   ```csharp
            var campaign = Campaign.Current;
            var mapScene = campaign?.MapSceneWrapper;
            if (mapScene != null)
            {
                // Match the active campaign's land-navigation rules (the exclusions and region costs
                // MobileParty.ComputePath passes), and never hand native an endpoint off the land navmesh.
                var models = campaign.Models;
                AddLandPath(mapScene,
                    new CampaignVec2(from, isOnLand: true).Face, new CampaignVec2(to, isOnLand: true).Face, from, to,
                    models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default),
                    models.MapDistanceModel.RegionSwitchCostFromLandToSea,
                    models.MapDistanceModel.RegionSwitchCostFromSeaToLand,
                    points);
            }
   ```
   and add after `ComputeNavPathPoints` the `AddLandPath` method exactly as in `ab78564e0`
   (`git show ab78564e0:Main/Features/SupplyLines/SupplyCaravanService.cs`, the `internal static void AddLandPath(IMapScene mapScene, PathFaceRecord fromFace, PathFaceRecord toFace, Vec2 from, Vec2 to, int[] excludedTerrain, int landToSea, int seaToLand, List<Vec2> points)`
   method with its summary; in the summary write "vanilla `MobileParty.ComputePath` checks both
   positions too (v1.4.8 `MobileParty.cs:3693-3698`)").
3. **Desertion pin (new 1.4.x test).** Create `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionMapEventGateTests.cs`
   (a new name, so WP123's port of `423d948d8`, which creates `AlignmentDesertionBehaviorTests.cs`,
   cannot collide). Usings: `System`, `System.Reflection`, `System.Runtime.Serialization`, MSTest,
   `NSubstitute`, `TaleWorlds.CampaignSystem`, `.MapEvents`, `.Party`, `.Party.PartyComponents`,
   `.Roster`, `.Settlements`, `TAOM.Core.Logging`, `TAOM.Features.AlignmentDesertion`,
   `TAOM.Features.AlignmentDesertion.Hooks`. Content:
   - A summary: #750 and PR #1064; a field party or garrison in a map event loses nobody that day, as
     in vanilla desertion; 1.4.x has the guard since `1b00d1acd` and this pins it; the parties are
     staged from uninitialized engine objects with no campaign; past the gate the handler reads
     `Clan.PlayerClan` (v1.4.8 `Clan.cs:276`), which throws without a campaign, so "threw" means
     "got past the gate" and "returned" means "stopped at it"; ported from `ab78564e0`, whose 1.5.x
     test observes a co-op seam this line does not have.
   - Copy the helpers `Bare<T>`, `SetField` and `SetProperty` verbatim from
     `git show ab78564e0:TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs`
     (lines 113-136).
   - One `[DataTestMethod]` with the four `DataRow`s of that file (`"OnDailyTickParty"`/`"OnDailyTickSettlement"` x `true`/`false`),
     method `DailyTick_PartyInAMapEvent_StopsBeforeTheOwnerRead(string handler, bool inMapEvent)`:
     ```csharp
        var service = Substitute.For<IAlignmentDesertionService>();
        service.IsEnabled.Returns(true);
        var sut = new AlignmentDesertionBehavior(service, Substitute.For<IModLogger>());
        // staging: copy lines 149-183 of that file verbatim (clan, mobile, party, roster, map event, target)
        var method = typeof(AlignmentDesertionBehavior).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AlignmentDesertionBehavior." + handler);

        Exception? thrown = null;
        try { method!.Invoke(sut, new[] { target }); }
        catch (TargetInvocationException ex) { thrown = ex.InnerException; }

        if (inMapEvent)
            Assert.IsNull(thrown, "a party in a map event must return before the owner read: " + thrown);
        else
            Assert.IsInstanceOfType(thrown, typeof(NullReferenceException),
                "out of battle the handler reaches Clan.PlayerClan, which needs a live campaign");
        service.DidNotReceiveWithAnyArgs().CalculateDesertion(default!, default, default, default!);
     ```
   - Prove it bites: temporarily delete `if (party.MapEvent != null)` and its `return;` in
     `AlignmentDesertionBehavior.cs:49-50`, rebuild, run the filter: the `("OnDailyTickParty", true)`
     row fails. Restore with `git checkout HEAD Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs`
     and confirm `git diff --quiet HEAD -- Main/Features/AlignmentDesertion` exits 0.
4. **Docs.** `docs/features/supply-lines.md`, section `## Legacy 1.4.8 native-query fixes` (`:331`):
   add "`AddLandPath` skips the native query when either endpoint face is invalid (face -1, off the
   land navmesh), as vanilla `MobileParty.ComputePath` checks both positions (v1.4.8
   `MobileParty.cs:3693-3698`); such a route takes the straight two-point line. `SupplyNativeQueryTests`
   drives `AddLandPath` with a fake `IMapScene`." `docs/features/alignment-desertion.md`, section
   `## Battle roster ownership` (`:170`): add "`AlignmentDesertionMapEventGateTests` pins it: a party or
   garrison in a map event returns before the owner read (four runs)."

**Verify**: build exit 0; `--filter "FullyQualifiedName~SupplyNativeQueryTests|FullyQualifiedName~AlignmentDesertionMapEventGateTests"`:
0 failed, 10 passed (6 + 4); `git grep -n "internal static void AddLandPath" -- Main/Features/SupplyLines/SupplyCaravanService.cs` prints one line.

**Commit**:

```
fix(campaign): v2.0.28 - supply path query skips off-navmesh endpoints

A supply caravan's route query now skips the native path search when
either end of the route lies off the land navmesh, as vanilla parties
do. Native code reads its face table without a bounds check, and PR
#1064 reported a crash in this query on Bannerlord 1.4.8; such a route
takes the straight line, as a failed query always did. Routes already
followed the land rules on this line.

A test also pins the desertion rule from the same PR: a party or
garrison in a battle loses nobody that day (#750).

Research: taom-src IMapScene.GetPathBetweenAIFaces (:34),
PathFaceRecord.IsValid, MobileParty.ComputePath (:3693-3698)
Not-tested: a remote supply order to a party off the navmesh
Ported-from: ab78564e064e173bef9f8d23d22d46bf86e3b3aa
```

### Part A done criteria

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] Full suite: 0 failed; passed + skipped grew by 68 over `BASE_TOTAL` (47 + 11 + 10).
- [ ] `git grep -n "OutcomeLog" -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -nE 'TestCategory\("(RequiresGame|RequiresGameIL|LiveInstall)"\)' -- TAOM.Tests` prints nothing.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists no `CHANGELOG.md`, nothing under
      `Main/_Module/`, and no `Main/IoC.cs` or `Main/SubModule.cs`.
- [ ] Three commits, each subject carrying `v2.0.28 - `; `git status --porcelain` is empty.

## Part B: guard narrowing, the pairing guard, the main menu (Steps B1 to B3)

### Step B1: Crash guards contain only their own fault (`0002cb561` with the guard half of `6ae924638`)

**Pre-checks**:
- `git log --oneline 1b00d1acd..HEAD -- Main/Features/AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs` prints nothing.
- `git log --oneline 1b00d1acd..HEAD -- Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs` prints
  at most WP106's commit (its comment lines). Patch69 is edited by hand below, never transplanted.

Simulation: `0002cb561` conflicts in all five guards (no `[ClientOnly]` or `bd1ac8292` refactor here,
and `6ae924638` is not under it), `Main/SubModule.cs`, the new tests, the 1.5.x-only Patch123 files and
docs; `6ae924638` conflicts in Patch69, its 1.5.x-only hunks and docs.

1. **Tests first (RED).**
   ```bash
   git checkout 0002cb561 TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs TAOM.Tests/Features/ArmyTargeting/Patch49GatheringNreRuleTests.cs TAOM.Tests/Features/CharacterCreation/NarrativeMountSpawnGuardTests.cs
   ```
   Delete the `[TestCategory("RequiresGame")]` line in each. Re-cite: `Patch49GatheringNreRuleTests`
   summary to `v1.4.8 Army.cs:652-728`, `:659`, `:726`, `:725`; `NarrativeMountSpawnGuardTests`
   summary and field comment from `v1.5.5 :204-215` to `v1.4.8 :204-215`.
   - `CoopVetoClassificationTests.cs`: after the `CharacterCreationCampaignBehavior_GetAgeSelectionMenuArgs_Patch`
     entry (`:246-247`) add the `CharacterCreationNarrativeStageView_SpawnNonHuman_Patch` entry from
     `6ae924638` (`CoopVeto.ReviewedSafe`, reason "Skips spawning a narrative-menu mount preview
     entity when vanilla would throw on a missing spawn point, item, horse monster or mount key (NRE
     guard audit 2026-10-27). The skipped body builds a local scene entity in the CC view; no campaign
     object involved."). The new Patch20 prefix matches the test's `static bool Prefix(` pattern.
   - `ReflectionSiteBindingTests.cs`: before the race-selector comment (`:46`) add
     `    // --- CharacterCreation narrative mount guard, Patch20 prefix (CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs) ---`
     and the two rows from `0002cb561` (`SpawnNonHumanNarrativeMenuCharacter` Method and
     `_characterScene` Field on `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView`;
     set their source columns to the line numbers in your edited file).
   **Verify (RED)**: build fails (`Settle`, `UseLogger`, `ShouldContain`, `IsKnownDeadEnd`,
   `CanSpawnMount` missing).
2. **Four guards by transplant (GREEN).**
   ```bash
   git checkout 0002cb561 Main/Features/AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs
   ```
   Then edit:
   - Delete every `[ClientOnly]` line: two in the Patch20 file, three in the Patch56 file.
   - Patch49 file: re-cite every 1.5.x line with the Army.cs map (`:661`, `:728`, `:727`, `:482`,
     `:566`, `:520`, `:576-607`, `:755`).
   - Patch20 file: in the `SpawnNonHuman` summary, `v1.5.5` becomes `v1.4.8` (numbers unchanged).
   - Patch50 file: remove `, <c>SignatureStrikesMissionLogic</c>` from the list of TAOM synthetic-blow
     routes (SignatureStrikes does not exist on 1.4.x; `BehaviorTree.RunTree` catches in
     `Main/BehaviorTrees/BehaviorTreesCore.cs:52-79`, and `WargAttackService` and `SpiderAttackService`
     each have their catch).
3. **Patch69 by hand** (keeps WP106's comment lines). In `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs`:
   - Add `using System.Reflection;` and `using TAOM.Dependencies.Foundation;`.
   - Summary: the sentence "On any exception this dumps the entire bracket ... then swallows." becomes
     the `0002cb561` wording ("On any exception this logs it with its stack and dumps the entire
     bracket (...); then it swallows a <see cref="NullReferenceException"/> only and rethrows anything else.").
   - Replace the finalizer method with the `0002cb561` shape: the `// Cold target ...` comment, the
     three-argument `Finalizer(Exception __exception, TournamentVM __instance, MethodBase __originalMethod)`
     that returns `Settle(...)`, and `internal static Exception Settle(Exception exception, IModLogger logger, Func<string> dumpBracket, MethodBase site)`
     with its summary, the logger-null return through `RethrowStackPreserver.PreserveForRethrow(exception, site)`,
     the two `LogError` lines writing `{exception}` and `{dumpBracket()}`, the unchanged dump-failure
     catch, and `if (!ShouldSwallow(exception)) return RethrowStackPreserver.PreserveForRethrow(exception, site);`.
   - Keep every comment line already in the method (the "Swallow ONLY ..." block and WP106's
     tick-handshake and prize lines) in place, and after the "survives with an incomplete winner
     panel ..." comment add: `// That is why the NRE stays contained by a finalizer rather than a prefix (NRE guard`,
     `// audit 2026-10-27). A non-NRE is still rethrown, with the same hang risk; no input is known to`,
     `// produce one here.`
   - Add at the end of the class:
     `    // Internal for TAOM.Tests (InternalsVisibleTo): the decision is pure, the finalizer is not.` and
     `    internal static bool ShouldSwallow(Exception exception) => exception is NullReferenceException;`
     (skip if WP106 already added it).
4. **`Main/SubModule.cs`**: replace the Patch49 comment above `TryPatchCategory("Patch49_ArmyGatheringNreGuard");`
   (on `1b00d1acd`: `:429-433`) with:
   ```csharp
        // Patch49: Finalizer (with a state-setting prefix) guarding vanilla
        // Army.FindBestGatheringSettlementAndMoveTheLeader, which NREs (v1.4.8 Army.cs:726
        // settlement.GatePosition / :659 Kingdom.Settlements) when a besieger army can't resolve a
        // gathering fortification: a map-tick CTD on siege start. It contains only those two dead ends.
        // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
        // Crash report 2026-06-17. See the patch's doc-comment.
   ```
5. **Registry**: in the Patch20, Patch49, Patch50, Patch56 and Patch69_TournamentEndGuard sections,
   take the `0002cb561` text (`git show 0002cb561:docs/reference/harmony-patch-registry.md`): the
   Patch49 Target line, the rewritten Patch49 paragraph and the five "NRE guard audit (2026-10-27)"
   notes. Re-cite with the map (Patch49 `:659`, `:726`, `:480`, `:564`, `:518`, `:574-605`, `:753`;
   Patch50 `Agent.cs:1747`, `:1772`, `Mission.cs:5621`; Patch20 `v1.4.8 :204-215`, `:208`); remove
   `SignatureStrikesMissionLogic` from the Patch50 note; in the Patch56 note write "the candidates are
   in `PopupSceneSpawnPoint` and `GauntletSceneNotification.OpenScene`" with no version.
   `reflection-sites.md`: after the `FaceGenVM` `_selectedRace` row (`:38`) add the two `0002cb561`
   rows (`SpawnNonHumanNarrativeMenuCharacter` method, `_characterScene` field) with your line numbers.

**Verify**:
- Build exit 0. `git grep -n "ClientOnly" -- Main` prints nothing.
- `git grep -nE "v1\.5\.[0-9]" -- Main/Features/AdvancedCombat/Hooks Main/Features/Arena/Hooks Main/Features/ArmyTargeting/Hooks Main/Features/CharacterCreation/Hooks Main/Features/HeroRace/Hooks TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs TAOM.Tests/Features/ArmyTargeting TAOM.Tests/Features/CharacterCreation/NarrativeMountSpawnGuardTests.cs` prints nothing.
- `--filter "FullyQualifiedName~ContainmentFinalizerRethrowTests|FullyQualifiedName~Patch49GatheringNreRuleTests|FullyQualifiedName~NarrativeMountSpawnGuardTests|FullyQualifiedName~CoopVetoClassificationTests|FullyQualifiedName~Patch69"`:
  0 failed; the three new classes pass 31 (10 + 14 + 7); `CoopVetoClassificationTests` passes all 5.
- `--filter "FullyQualifiedName~ReflectionSiteBindingTests"`: 0 failed, two more than before.

**Commit** (paths: the five guards, the three tests, `CoopVetoClassificationTests.cs`,
`ReflectionSiteBindingTests.cs`, `Main/SubModule.cs`, the registry, `reflection-sites.md`):

```
fix(crash-guards): v2.0.28 - crash guards contain only their own fault

The guards that catch a null reference so a vanilla crash cannot end
the game now contain only the fault each was written for:

- Army gathering (Patch49) contains the error only in its two known
  dead ends, and only when the vanilla body actually ran.
- The character creation mount (Patch20) checks before it spawns,
  instead of catching every error afterwards.
- The flagged-item drop guard (Patch50) logs the first swallowed error
  once per game, with its stack.
- The tournament end (Patch69) and race cinematic (Patch56) guards log
  the full error, and the cinematic always releases input.
- Every guard passes other exceptions on with their original stack.

Research: taom-src Army.FindBestGatheringSettlementAndMoveTheLeader
(:652-728), CharacterCreationNarrativeStageView (:194-215),
TournamentBehavior (:149-259), Mission tick handshake (:3585-3740)
Not-tested: siege start, tournament end, become-king cinematic and a
horseless culture's character creation in game
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: 0002cb5614e52fbdd638b402d302613f04678cf5
```

### Step B2: Guard bandit clans and dens that load apart (`cd06da904` + `1ef7d1af6`, plus the `0799af376` pairing test, K2)

Simulation: both conflict in `Main/SubModule.cs` or the hook, Feral Orcs data, strings, `tools/`,
`ReflectionSiteBindingTests.cs` and docs. Take only the guard.

1. **Tests first (RED).** `git checkout cd06da904 TAOM.Tests/Features/BanditManagement/HideoutClanGuardTests.cs TAOM.Tests/Features/BanditManagement/Patch125HideoutClanGuardBindingTests.cs`.
   Create `TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs` from
   `git show 0799af376:TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs` with:
   no `[TestCategory("LiveInstall")]`; no `Languages` array; `ModulesDir()` replaced by
   ```csharp
    private static string ModulesDir()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install not resolved (BANNERLORD_GAME_DIR): " + string.Join("; ", GameAssemblies.Diagnostics));
        return Path.Combine(GameAssemblies.GameDir, "Modules");
    }
   ```
   (`using TAOM.Tests.Migration;`); keep `LiveHideouts()` (Inconclusive when TAOM_Map is absent),
   `BanditClans()` and only `EveryHideoutCultureBanditClan_OwnsALiveHideout` (the `0799af376` shape
   that filters by `CultureDataFixture.HideoutOnlyCultures`), with one added line before the orphan
   assert: the filtered clan list must be non-empty ("no hideout-culture bandit clan in
   characters/clans.xml: the scan reads nothing"). Delete the two Feral Orcs tests. Summary: bandit
   clans against the live, unversioned TAOM_Map; a clan with no den of its culture throws on a new
   game; the release check for each TAOM_Map package (LOADING-HANG-08).
   - `ReflectionSiteBindingTests.cs`: after the Patch86 rows (`:103-105`) add
     `    [DataRow("TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior", "BanditSpawnCampaignBehavior", "_hideouts", "Field", "Patch125_HideoutClanGuard.cs (____hideouts)")]`.
   **Verify (RED)**: build fails (`HideoutClanGuard` missing).
2. **Code (GREEN).**
   ```bash
   git checkout cd06da904 Main/Features/BanditManagement/HideoutClanGuard.cs
   git checkout 1ef7d1af6 Main/Features/BanditManagement/Hooks/Patch125_HideoutClanGuard.cs
   ```
   `Main/SubModule.cs`: directly after `TryPatchCategory("Patch86_HideoutBossFight");` insert:
   ```csharp
        // Patch125 (#1063): a bandit clan ships in TAOM and the hideouts of its culture in TAOM_Map, so
        // either can load alone. Guards GetInfestedHideoutCount (a clan with no den of its culture: the
        // new-game throw) and AddBanditToHideout (a den with no clan: an old save, the Gold Rush
        // incident). Applied in this batch beside Patch86, the batch Patch65 and Patch88 rely on for
        // the new-game dispatch.
        TryPatchCategory("Patch125_HideoutClanGuard");
   ```
3. **Docs.**
   - Registry: add `## Patch125_HideoutClanGuard` after the Patch110 section from Step A2 (before
     `## Patch_MissionTime_SetMovementOrder`), from `git show 1ef7d1af6:docs/reference/harmony-patch-registry.md`,
     with: `v1.5.6` becomes `v1.4.8 (BanditSpawnCampaignBehavior.cs:490 and :312)`; `:499` becomes
     `:493`; `:331` becomes `:324`; `AddBossParty (:211, dereferences it)` becomes `AddBossParty`
     (`:199`, dereferences the result at `:204`); `CampaignIncidentEffects.InfestNearbyHideout`
     becomes `IncidentEffect.InfestNearbyHideout` (v1.4.8 `IncidentEffect.cs:681`, used by
     `incident_gold_rush`, `IncidentsCampaignBehaviour.cs:1786`); cite the `AddNewHideouts` weight as
     `:285`; add "(1.5.x reading, not re-checked on v1.4.8)" after `AiVisitSettlementBehavior`;
     replace "Found by the Codex pre-review of the Feral Orcs (rca link)." with "Found by a 1.5.x Codex
     review (#1063)."; replace "Verified against the v1.5.6 DLLs by the deep-review engine lens,
     2026-10-29." with "Targets and the field verified on the installed v1.4.8 DLLs with taom-src,
     2026-11-04."; add `BanditHideoutLiveDataTests` (the live pairing check) to Tests.
   - `docs/features/bandit-management.md`: before `## Key Files` add:
     ```markdown
     ## Clan and hideout pairing guard (Patch125, #1063)

     A bandit clan is in TAOM's `characters/clans.xml` and the hideouts of its culture are in TAOM_Map's `settlements.xml`, so either can load alone. A clan with no hideout of its culture (TAOM with an older map) makes vanilla `GetInfestedHideoutCount` throw `KeyNotFoundException` on every new game; a hideout with no clan of its culture (a save made before the clan existed, since a saved campaign reloads settlements but not clans) makes `AddBanditToHideout` dereference a null clan, reachable from the Gold Rush incident. `Patch125_HideoutClanGuard` returns 0 and null for those two states, runs vanilla otherwise, and logs one warning per culture. The decisions are `HideoutClanGuard`; the details are in the [registry](../reference/harmony-patch-registry.md#patch125_hideoutclanguard). `BanditHideoutLiveDataTests` checks the pairing against this machine's live TAOM_Map; run it against every TAOM_Map package a release ships. Ported 2026-11-04 from 1.5.x `cd06da904` and `1ef7d1af6`; with today's pairing the guard never fires.
     ```
   - `reflection-sites.md`: after the `_overriddenHideoutBossAgentOrigin` row (`:71`) add the
     `_hideouts` row from `1ef7d1af6`.

**Verify**: build exit 0; `--filter "FullyQualifiedName~HideoutClanGuardTests|FullyQualifiedName~Patch125HideoutClanGuardBindingTests|FullyQualifiedName~BanditHideoutLiveDataTests"`:
0 failed, 10 passed (6 + 3 + 1), none Inconclusive on the laptop; `ReflectionSiteBindingTests` one
more than before, 0 failed; `git grep -n 'TryPatchCategory("Patch125_HideoutClanGuard")' -- Main/SubModule.cs` prints one line.

**Commit**:

```
fix(bandits): v2.0.28 - guard bandit clans and dens that load apart

A bandit clan ships in TAOM and the dens of its culture ship in
TAOM_Map, and a saved campaign reloads settlements but not clans, so
the pair can arrive half. Two vanilla lookups crashed on the missing
half: a clan with no den of its culture threw on every new game, and a
den with no clan threw when the Gold Rush incident tried to fill it.

Both now log one line naming the mismatch, and vanilla runs in every
other case. A test checks that every bandit clan has a den on the
installed map, so a release can check the map it ships.

Research: taom-src BanditSpawnCampaignBehavior (_hideouts :23,
AddBanditToHideout :312, GetInfestedHideoutCount :490)
Not-tested: a new campaign with a mismatched TAOM_Map
Ported-from: cd06da904d35f9160c32ff33030814a325344ab6
Ported-from: 1ef7d1af6c4f43d343eebc4a2f159c209edc44d8
Ported-from: 0799af3765ba1c46a25282fb7f930adcb7b21868
```

### Step B3: Main-menu setup never throws into the engine (`85c608963` + `2afea8878`, final shape only)

Simulation: both conflict in `Main/SubModule.cs`; `2afea8878` also in files built on `LifecycleStep`.

1. **Tests first (RED).** `git checkout 2afea8878 TAOM.Tests/Composition/MainMenuStepTests.cs TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs`.
   - `MainMenuStepTests.cs`: replace `using TAOM.Tests.Features.LoadTimeStamps;` with
     `using System.Collections.Generic;` and `using TAOM.Core.Logging;`, and add inside the class (the
     1.5.x helper is `TAOM.Tests/Features/LoadTimeStamps/RecordingLogger.cs`, which WP109 creates with
     identical content; this plan does not depend on WP109, and this nested helper compiles and is
     acceptable whether or not that file is on your base):
     ```csharp
    // Collects each line with its level prefix; OnLog runs first, so a test can throw from it.
    private sealed class RecordingLogger : IModLogger
    {
        public List<string> Lines { get; } = new();
        public Action<string>? OnLog { get; set; }
        public void LogInfo(string message) => Add("INFO " + message);
        public void LogDebug(string message) => Add("DEBUG " + message);
        public void LogWarning(string message) => Add("WARN " + message);
        public void LogError(string message) => Add("ERROR " + message);
        public string? LogFilePath => null;
        public void Dispose() { }
        private void Add(string line) { OnLog?.Invoke(line); Lines.Add(line); }
    }
     ```
   - `SubModuleMainMenuHookTests.cs`: the summary's last sentence becomes
     "`ShaderCompileNoticeEntry.Stop`, where present, only sets a volatile flag and is called directly."
   **Verify (RED)**: build fails (`MainMenuStep` missing).
2. **Code (GREEN).** `git checkout 2afea8878 Main/Composition/MainMenuStep.cs`; in its summary
   `(v1.5.5 Module.cs 540-549, 790-799)` becomes `(v1.4.8 Module.cs 500-509, 750-759)`.
   `Main/SubModule.cs`, in `OnBeforeInitialModuleScreenSetAsRoot`, replace
   `IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();` with:
   ```csharp
        // The engine calls this hook for every module with no try, and calls it again on every tick while
        // no state is active (v1.4.8 Module.cs:500-509, :750-759); Patch37 can swallow the throw, so one
        // throw would re-run every module's hook each tick (#807). CustomizeMenu goes through
        // MainMenuStep.Run (one WARNING, never rethrows) and is safe to repeat. SubModuleMainMenuHookTests
        // pins this shape.
        MainMenuStep.Run("menu customization", () => IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu(), MainMenuLogger);
   ```
   If the line `Features.ShaderCompileNotice.ShaderCompileNoticeEntry.Stop();` is in the method
   (WP113), put `// Only sets a volatile flag, so it cannot throw.` above it. After the method add
   `    private static IModLogger? MainMenuLogger() => IoC.Resolve<IModLogger>();`. Add
   `using TAOM.Composition;` only if the file lacks it (WP105 adds it).
3. **Doc** `docs/features/main-menu-customizer.md`:
   `git diff 2afea8878^ 2afea8878 -- docs/features/main-menu-customizer.md | git apply --3way`, then:
   constraint 4 cites `v1.4.8 Module.cs 500-509 and 750-759`; delete the issue-section reference to
   `docs/reviews/rca-yotthani-review-2026-10-15.md` (not on 1.4.x); the changelog line reads
   `- 2026-11-04: CustomizeMenu now runs through MainMenuStep.Run (ported from 1.5.x 2afea8878, #807) ...`
   with the rest of its sentence kept.

**Verify**: build exit 0; `--filter "FullyQualifiedName~MainMenuStepTests|FullyQualifiedName~SubModuleMainMenuHookTests|FullyQualifiedName~FeatureModulesTests|FullyQualifiedName~MainMenuCustomizer"`:
0 failed, the new classes pass 11 (10 + 1); `git grep -n "MainMenuStep.Run(" -- Main/SubModule.cs` prints one line.

**Commit**:

```
fix(main-menu): v2.0.28 - main-menu setup never throws into the engine

Bannerlord calls every module's main-menu setup with no try, and calls
the whole chain again on every tick while no game state is active, so
one throw in TAOM's menu setup would re-run every module's setup on
every tick instead of failing once.

TAOM's menu customization now runs through MainMenuStep. A throw is
caught and logged once as a [MainMenuStep] WARNING with the full
exception, and the menu opens without the rename. The step is not
latched, so a later call can still succeed.

Research: v1.4.8 Module.OnApplicationTick (:500-509),
SetInitialModuleScreenAsRootScreen (:750-759)
Not-tested: none needed beyond a normal boot to the main menu
Ported-from: 85c608963ca6f31839071fea30ea76cdd6e28da3
Ported-from: 2afea8878a567c632d9dcb03e0594ec280c8873c
```

### Part B done criteria

- [ ] Build exits 0; full suite 0 failed; passed + skipped grew by 55 over the Part A end total (33 + 11 + 11).
- [ ] `git grep -n "ClientOnly" -- Main` prints nothing.
- [ ] `git grep -nE 'TestCategory\("(RequiresGame|RequiresGameIL|LiveInstall)"\)' -- TAOM.Tests` prints nothing.
- [ ] `git diff bannerlord-1.4.5...HEAD -- Main/SubModule.cs` shows only the three edits of this Part
      (Patch49 comment, Patch125 block, MainMenuStep call and helper).
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` lists no `CHANGELOG.md`, nothing under `Main/_Module/`, no `Main/IoC.cs`.
- [ ] Three more commits with `v2.0.28 - ` subjects; `git status --porcelain` is empty.

## Part C: main party reads, mutation floats, hand-off (Steps C1 to C3)

### Step C1: Read the main party through the campaign (`91f369984`, `dc1c37031`, `e9854bbd0`, `2c09a4145`, `74bdd5eff` using only, `4f27b3bde`, `186156808` fourth bullet)

**Pre-check** (must print nothing):
`git diff --stat 1b00d1acd HEAD -- Main/Features/Messengers/MessengerCampaignBehavior.cs Main/Adapters/EncounterAdapter.cs Main/Adapters/PartyItemRosterAdapter.cs Main/Adapters/PlayerEquipmentAdapter.cs Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs`
The one allowed exception is `EnlistmentBattleBehavior.cs` if WP110 merged first: its port of
`ec2a3026f` changes a comment there, and the 3-way pick below keeps it.

Simulation: `91f369984` conflicts only in `MessengerCampaignBehaviorTests.cs` (WP104's `a0db0558b`
converts it); the scan commits in the absent scan test, `adapters.md` and a lesson; `74bdd5eff` in
1.5.x-only files; `186156808` in `.ai/review-reference.md`.

1. **Tests first (RED).**
   ```bash
   git checkout 4f27b3bde TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
   git diff 91f369984^ 91f369984 -- TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs | git apply --3way
   ```
   In the scan test: delete `HunterPartyAdapter_IsMapFree_ChecksTheStoredMainParty` (HunterPartyAdapter
   is 1.5.x only); in the summary write `(v1.4.8 PartyBase.cs:208-218)` and change "The messenger
   popup (`== null`) and the hunt popup (`!= null`) shipped that check (both fixed in v2.0.34)" to "The
   messenger popup shipped that check". In the new messenger test comment write
   `(v1.4.8 PartyBase.cs:208-218)` and drop `(ChroniclePresenter)`. If the apply conflicts (the file
   was not converted), keep ours and add the new method as the last method of the class, with
   `using TAOM.Tests.Infrastructure;`.
   **Verify (RED)**: build exit 0; `--filter "FullyQualifiedName~MainPartyNullCheckScanTests|FullyQualifiedName~MessengerCampaignBehaviorTests"`
   fails `ProductionSource_NeverNullChecksTheComputedPartyBaseMainParty` (naming
   `MessengerCampaignBehavior.cs`, `EncounterAdapter.cs`, `PartyItemRosterAdapter.cs`), the three
   `AliasedSites_ReadTheMainPartyThroughTheCampaign` rows and
   `IsPlayerAvailable_GuardsTheStoredMainPartyNotTheComputedGetter`.
2. **Code (GREEN).**
   ```bash
   git diff 91f369984^ 91f369984 -- Main/Features/Messengers/MessengerCampaignBehavior.cs | git apply --3way
   git diff e9854bbd0^ e9854bbd0 -- Main/Adapters/EncounterAdapter.cs Main/Adapters/PartyItemRosterAdapter.cs | git apply --3way
   git diff 74bdd5eff^ 74bdd5eff -- Main/Adapters/PartyItemRosterAdapter.cs | git apply --3way
   git diff 2c09a4145^ 2c09a4145 -- Main/Adapters/PlayerEquipmentAdapter.cs Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs | git apply --3way
   ```
   Each applies cleanly (the six files equal the pre-images). The `74bdd5eff` line removes the
   `using TaleWorlds.CampaignSystem.Party;` that `e9854bbd0` leaves unused.
3. **Docs.**
   - `.claude/rules/adapters.md`, as the last paragraph before `## Testing`, after whichever paragraph
     ends the block (normally Step A1's hollow paragraph; keep every paragraph already there):
     ```markdown
     **`PartyBase.MainParty` is a computed getter that throws.** It returns null only without a campaign; with one it reads `Campaign.Current.MainParty.Party` (v1.4.8 `PartyBase.cs:208-218`), so it throws, `?.` included, while the stored main party is null. Check `Campaign.Current?.MainParty` and read `Campaign.Current?.MainParty?.Party`. `MainPartyNullCheckScanTests` fails a null comparison, `is null`, `??` or `?.` on the getter written directly or inside any number of its own parentheses, and passes a call argument (`Find (PartyBase.MainParty)?.X`); it cannot see a check through a local alias (`var p = PartyBase.MainParty; if (p == null)`), so review for that form, and it blanks comments but not strings, so a string holding the check fails it (#792).
     ```
   - `.ai/review-reference.md`: append the fourth `186156808` bullet ("Every syntax that guards a
     throwing getter", four lines; `git show 186156808 -- .ai/review-reference.md`) to the "Look harder
     here" list. Do not change the section header's review totals.

**Verify**: build exit 0; the same filter: 0 failed, 55 passed (49 scan results + 6 messenger);
`git grep -nE "PartyBase[[:space:]]*\.[[:space:]]*MainParty[[:space:]]*(==|!=)[[:space:]]*null|PartyBase\.MainParty\?\." -- Main` prints nothing.

**Commit**:

```
fix(adapters): v2.0.28 - read the main party through the campaign

The engine's PartyBase.MainParty returns null only when there is no
campaign; with one it reads the campaign's main party and throws while
that is null. Eleven places null-checked it or read through it with ?.:
the messenger popup's hourly check, the encounter map-event checks,
the party gear roster, Unequip All, the recruitment screen's weighted
party size and the enlistment battle check. Each now reads the stored
main party through the campaign. Nothing changes while a main party
exists.

A source scan now fails if production code null-checks the getter
again, in any of its written forms.

Research: taom-src PartyBase.MainParty (:208-218)
Not-tested: no visible change in game
Ported-from: 91f3699843d167f8fc0950bde3c8db295ee61988
Ported-from: dc1c370316d40ab4fcb0438fe81f27308337e6cf
Ported-from: e9854bbd0864ac58fe7fcaab425ff63126f5f7ef
Ported-from: 18615680851ce800ccbb0f86278fe8aa421bced2
Ported-from: 2c09a414594fd18d00da80310d6e5cd02c56ace3
Ported-from: 74bdd5eff5f52b5717a209b2a2c531597f27a385
Ported-from: 4f27b3bdeac863f2c6cb99026c49f2225fdec011
```

### Step C2: Reject NaN and Infinity in mutation floats (`78889a85c`, `968325896`, `8186cc9ca`; closes plan 002)

Simulation: `78889a85c` conflicts only in `CHANGELOG.md`; the other two in the absent test file and records.

1. **Test first (RED).** `git checkout 968325896 TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs`.
   **Verify (RED)**: build exit 0, then `--filter "FullyQualifiedName~MutationParamsTests"`: 3 failed
   (`GetFloat_NaN_ReturnsDefault`, `GetFloat_PositiveInfinity_ReturnsDefault`,
   `GetFloat_NegativeInfinity_ReturnsDefault`), 4 passed.
2. **Code (GREEN).** `git diff 78889a85c^ 78889a85c -- Main/Features/CareerSystem/Mutations/MutationParams.cs | git apply --3way`
   (adds `using TAOM.Core.Validation;` and `&& FiniteFloatValidator.IsFinite(result)`).
3. **Doc** `docs/features/career-system.md`: after the `MutationServiceTests` row (`:416`) add
   `| MutationParamsTests | 7 | GetFloat: valid, missing key, unparseable, negative, NaN, ±Infinity |`;
   replace step 1 of "Add a new mutation calculator" (`:435`) with
   ``1. Add function to `BuiltInCalculators.RegisterAll()`. Read float parameters only through``
   ``   `MutationParams.GetFloat`, which returns the default for a missing, unparseable, NaN or``
   ``   Infinity value; a float read any other way needs its own `FiniteFloatValidator` guard.``

**Verify**: `--filter "FullyQualifiedName~MutationParamsTests"`: 0 failed, 7 passed.

**Commit**:

```
fix(config): v2.0.28 - reject NaN and Infinity in mutation floats

A career mutation's float parameter is read with float.TryParse, which
accepts "NaN", "Infinity" and "-Infinity". Such a value then passes
every later range check. MutationParams.GetFloat now returns the
default for them, as it already did for a missing or unparseable
value. No shipped XML holds such a value; this closes plan 002, whose
troop-weight half landed earlier.

Research: Main/Core/Validation/FiniteFloatValidator.IsFinite(float)
Not-tested: nothing in game (latent guard)
Ported-from: 78889a85c4558eae75127cfba85cdce46f45e0a1
Ported-from: 96832589679f84d0ea6f8b6e76cf2928932ab093
Ported-from: 8186cc9caee5da0ae82db588572469329b8c98d8
```

### Step C3: Hand off (no commit, no edit to `plans/README.md`)

Run the full suite. In your final message report to the integrator, who owns `plans/README.md`:

- Status of WP112, the branch `port148/112-crash-robustness` and its head, and each commit's short
  hash with its subject.
- The measured totals: `BASE_TOTAL`, and passed, skipped and failed from the final full suite.
- The old-plan row to update: plan 002 becomes `DONE (<date>: TroopWeight half bee07b487, mutation
  half <Step C2 short sha>)`.
- Nothing to place for the deferred hunks of `6ae924638` and `0002cb561` (the `.ai/verification.md`
  api-snapshot row and the `.claude/skills/verify/SKILL.md` Step 2d): WP137's Part D step takes them.

### Part C done criteria

- [ ] Build exits 0; full suite 0 failed; passed + skipped grew by 57 over the Part B end total
      (C1: 49 scan results and 1 messenger test; C2: 7).
- [ ] The `PartyBase.MainParty` grep of Step C1 prints nothing.
- [ ] `git grep -n "FiniteFloatValidator.IsFinite(result)" -- Main/Features/CareerSystem/Mutations/MutationParams.cs` prints one line.
- [ ] Two more commits; `git status --porcelain` is empty.
- [ ] The Step C3 hand-off is written; `git diff --name-only bannerlord-1.4.5...HEAD -- plans/README.md` prints nothing.

## Disposition (every commit in the pack, plus the shared hunks)

| Commit | Subject | Disposition |
|---|---|---|
| `78889a85c` | reject NaN and Infinity in mutation floats | **Ported** (C2, path-limited: `MutationParams.cs`); CHANGELOG dropped (D1) |
| `968325896` | review follow-ups for plan 002 | **Ported** (C2: test file, career-system.md); review record, RCA, Codex prompt, lessons, REVIEW-LOG dropped |
| `8186cc9ca` | convergence fixes for plan 002 | **Adapted** (C2: career-system.md Tests row and wording); records and LESSONS-LEARNED recount dropped |
| `82ae6cf17` | cite #663 for plan 002 | **Skipped**: CHANGELOG only (D1); #663 is cited in the Status block |
| `91f369984` | check the stored main party, not its getter | **Ported** (C1: messenger fix, scan test base, messenger test) |
| `dc1c37031` | main party scan catches every null-check form | **Ported** (C1, in the `4f27b3bde` end state) minus the HunterPartyAdapter test (1.5.x-only class) |
| `e9854bbd0` | read the main party through the campaign | **Ported** (C1: seven adapter reads, scan rows); adapters.md paragraph written fresh (the 1.5.x text extends `bb85e9284`'s campaign-statics paragraph, which WP111 Step C1.5 brings without the `PartyBase.MainParty` sentence when merged first; both paragraphs stay); lesson to WP142 |
| `186156808` | Codex lessons from #765 and #792 | **Adapted** (C1: the fourth bullet only); the three other bullets cite 1.5.x-only features |
| `2c09a4145` | three more main party reads via the campaign | **Ported** (C1: three sites, scan rows, AliasedSites); Codex prompt record dropped; lesson to WP142 |
| `74bdd5eff` | honest chronicle_open dry-run texts, tidy-ups | **Adapted** (C1: only the `PartyItemRosterAdapter.cs` using removal); War Chronicle and Eye of Sauron hunks are 1.5.x-only |
| `4f27b3bde` | main party scan sees nested parens | **Ported** (C1: final scan regex and rows; its adapters.md sentence is in the fresh paragraph) |
| `ae1a6b553` | daily tick on load, SyncData size, MainParty | **Covered** (`adapters.md` sentence only, deferred here by WP114): C1's fresh `PartyBase.MainParty` paragraph already states everything it says (null only without a campaign, reads `Campaign.Current.MainParty.Party`, throws with `?.` included, check `Campaign.Current?.MainParty`, the scan test) with the v1.4.8 lines, so nothing is taken from this commit here; WP114 owns its other hunks |
| `85c608963` | main-menu setup never throws into the engine | **Transplanted** with `2afea8878` (B3); `LifecycleStep` is never created; the ShaderCompileNotice comment applies only if WP113 brought that line |
| `2afea8878` | MainMenuStep logs the full exception | **Transplanted** (B3: `Main/Composition/MainMenuStep.cs`, two tests with a local RecordingLogger, doc) |
| `eed06b9c7` | talking to a musician no longer crashes | **Ported through the module** (A2, D2: module plus one `FeatureModules.All` line, Patch110 kept); RCA, INDEX, feature-map, lesson dropped; `patch-targets.md` row left to WP137's regeneration |
| `6ae924638` | Add tests for NRE containment finalizers ... | **Partial** (B1: the guard half, folded into the `0002cb561` end state, plus the Patch20 co-op entry). AutoResolveDiagnostics removal and `dread-aura.md` to WP123; HeroRace new-campaign log tweak to WP119; career-quest owner cancel to WP129; LastSeenText (Patch123), Fire and Tribute, Unlooked-for Aid, ArmourAcquisition, ServerSettings, SettingsFingerprint and `gamemodels.md` hunks skipped (1.5.x-only); `.ai/verification.md` api-snapshot row and `.claude/skills/verify/SKILL.md` Step 2d belong to WP137's Part D step (the `-Check` they describe cannot pass before `4ab005dd1`); lessons to WP142 |
| `0002cb561` | crash guards contain only their own fault | **Transplanted** (B1: four guards by checkout, Patch69 by hand, three tests, Patch20 reflection rows, registry notes, Patch49 SubModule comment); `[ClientOnly]` and `RequiresGame` dropped; v1.4.8 lines; LastSeenState, HeroHelperLastSeenTextGuardTests, the Patch123 comment and the RCA skipped (Patch123 guards the 1.5.x rewrite of `HeroHelper.GetLastSeenText`); `.ai/verification.md` row belongs to WP137's Part D step |
| `cd06da904` | guard bandit clans and dens that load apart | **Partial** (B2: `HideoutClanGuard`, Patch125, both tests, SubModule line, registry, bandit-management.md section); every Feral Orcs data, string, tool and doc hunk, the release-process channel check, the Codex prompt and the RCA skipped (content); harmony-il lesson to WP142 |
| `1ef7d1af6` | Feral Orcs review fixes, second wave | **Partial** (B2: Patch125 doc comment, `_hideouts` reflection row and doc row, registry caller list); `patch-targets.md` row left to WP137; Feral content skipped |
| `0799af376` | Feral Orcs, an orc bandit faction in 23 dens | **Skipped (content)** except K2's live pairing test `EveryHideoutCultureBanditClan_OwnsALiveHideout` with `LiveHideouts`/`BanditClans` (B2, game dir through `GameAssemblies`) |
| `ab78564e0` | port PR #1064 supply and desertion fixes | **Partial** (A3: `AddLandPath` with the face guard, three NSubstitute tests, desertion pin adapted to a new file); the two IL-fingerprint tests go to WP118 (need `IlCallScanner.Fingerprint` from `a27c6b338`); `Client/` hunk skipped (`1b00d1acd` already guards the arrows in `Main/`); desertion production hunk already on 1.4.x (`1b00d1acd`); RCA, Codex prompt, lessons dropped |
| `1165a00de` | repair a hollow parent before a birth | **Transplanted** with `de90a2451` (A1), IOutcomeLog swapped for IModLogger lines in the same format; `tools/ingame_checks/1076.toml`, its fixtures, `test_ingame_verdict.py` and `ingame-verdicts.md` to WP139 (need `c8d69e426`); RCA, feature-map, lesson dropped; `patch-targets.md` row left to WP137 |
| `de90a2451` | keep a restored race in hollow repair | **Transplanted** (A1); its `.ai/review-reference.md` bullet taken; REVIEW-LOG, codex-track-record, Codex prompt, RCA dropped; 1076.toml change to WP139 |
| LOADING-HANG-08 | release gates `cd06da904`, `84f0459bc` | `cd06da904` **ported** (B2) with its live pairing test as the release check; `84f0459bc` **not ported** (D12; owned by WP113's group `issue-916-bow-shader-sources`; WP115's packager gate covers it) |

## Test plan

- New results per step are listed in each Verify (A: 47 + 11 + 10; B: 33 + 11 + 11; C: 50 + 7); total
  growth 180, counting each `DataRow` as one result.
- Must stay green: `FeatureModulesTests`, `ModuleRunnerTests`, `CoopVetoClassificationTests`,
  `ResetForUnloadSweepTests`, `HarmonyPatchBindingTests`, `StaleCharacterRepairServiceTests`, any
  `Patch69` test WP106 added.
- Untestable here (for `Not-tested:`): each patch firing in game, a hollow-parent birth in a real
  save, the musician conversation, a supply order off the navmesh, a TAOM and TAOM_Map mismatch.
- Patterns: `Patch86HideoutBossFightBindingTests.cs` (binding), `WandererAllegianceWiringTests.cs` (module wiring).

## Done criteria (whole WP)

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0
      failed and `BASE_TOTAL` + 180 results; the `BindingVerification` and game-loading tests ran
      (none Inconclusive).
- [ ] Eight commits on `port148/112-crash-robustness`, each subject `<type>(<scope>): v2.0.28 - ...`
      at most 72 characters, each port commit with its `Ported-from:` lines.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD` holds no `CHANGELOG.md`, nothing under
      `Main/_Module/`, `tools/`, `Client/`, `docs/reviews/`, `docs/reference/feature-map.md`,
      `docs/INDEX.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`, `Main/IoC.cs`,
      `Main/TAOM.csproj` or `Directory.Build.props`.
- [ ] `git grep -n "OutcomeLog\|ClientOnly" -- Main TAOM.Tests` prints nothing.
- [ ] `python tools/lint_docs.py` reports no new finding in the touched docs; the dash scan prints nothing.
- [ ] `plans/README.md` is untouched and the hand-off of Step C3 is written.
- [ ] Release note for the maintainer: any build containing this WP ships TAOM and TAOM.Dependencies
      from one build (#371), because Part B calls `RethrowStackPreserver` in TAOM.Dependencies.

## STOP conditions

Stop and report (do not improvise) if:

- A Step 0 prerequisite is missing, or `IOutcomeLog` already exists on the branch (the swap in A1
  would then be wrong).
- A pre-check shows a transplanted file changed since `1b00d1acd` (any of the four guard files, the
  StaleCharacterRepair files, the six main party files, except WP110's comment in
  `EnlistmentBattleBehavior.cs`), or Patch69 changed by anything other than WP106's comment lines.
- A ported test fails on v1.4.8 for an engine reason (for example a `HeroCharacterStateAdapterTests`
  case, a BardTownGuard binding test finding other than exactly one pick call, a Patch125 binding
  test). Report the case and the engine member; do not edit production code to fit.
- `MainPartyNullCheckScanTests` still names a file after Step C1's fixes (a production offender this
  plan does not list).
- `CoopVetoClassificationTests` names an unclassified prefix other than
  `CharacterCreationNarrativeStageView_SpawnNonHuman_Patch`, or `FeatureModulesTests` fails after
  Step A2 (double wiring or anchor drift).
- `BanditHideoutLiveDataTests` finds an orphan clan on the laptop (a real TAOM and TAOM_Map pairing
  finding for the maintainer), or is Inconclusive.
- `BANNERLORD_GAME_DIR` is unset or the game-loading tests are Inconclusive (environment; report it).
- A step seems to need `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`,
  `.claude/settings*.json`, ModuleData or a `tools/` script; or a module would need
  `OwnsSaveData = true` (D2).
- A verification fails twice after a reasonable fix attempt, or a hook rejects a commit (never bypass).

## In-game checks owed (maintainer)

Log: the newest `taom_debug_*.log` in TAOM's `Logs` folder (`Main/Core/Logging/FileLogger.cs:26-40`)
unless named otherwise.

1. **Musician (#824)**: in each of `town_DG1`, `town_GT1`, `town_isengard`, `town_L1`, `town_LN1`,
   `town_R1`, `town_GBC1`, talk to the tavern musician several times: no crash; about one lyric in
   four names a town. In a Gondor town a lyric names a Gondor town. No `[BardTownGuard]` warning
   at game start.
2. **Hollow parent (#1076)**: needs an affected save. Expect no crash at the birth, a
   `[StaleCharacterRepair] census:` warning at load, and `[Check] id=taom.stale-character.parent-repair`
   and `[Check] id=taom.stale-character.parent-birth` lines. A healthy game writes none of them.
3. **Supply (#1064)**: order a remote supply caravan to a lord in the field: no crash; the route
   follows land.
4. **Main menu (#807)**: boot to the main menu: the rename shows; no `[MainMenuStep]` warning.
5. **NRE guards**: finish a tournament (winner panel draws); become ruler (the cinematic plays or
   aborts cleanly with input released); start character creation for a horseless culture (no crash;
   at most a `[CharacterCreation] skipped spawning narrative mount` warning); watch an AI siege start
   (no crash; `[SiegeDiag]` lines only for real dead ends).
6. **Patch125 (#1063)**: a new campaign with the matching TAOM_Map shows no `Patch125:` warning.

## Maintainer steps

- Create the GitHub issue for the NRE guard narrowing before Part B lands (no 1.5.x issue exists).
- Before each 1.4.x release: run `--filter "FullyQualifiedName~BanditHideoutLiveDataTests"` against
  the TAOM_Map package the release ships (LOADING-HANG-08), and ship TAOM and TAOM.Dependencies from
  one build (#371; E3's BuildStamp already showed a mismatch).
- Recommended for the orchestrator: `/deep-review` (probe the Patch69 merge, the A1 logger swap and
  the desertion staging), then `/verify-bindings` (three new patch classes, a new Patch49 prefix).

## Maintenance notes

- Later WPs that touch this WP's output: WP118 adds the two `SupplyNativeQueryTests` IL-fingerprint
  tests after `a27c6b338` (on v1.4.8 `NavigationType.Default` is 1, as their `ldc.i4 1` check
  expects); WP123 creates `AlignmentDesertionBehaviorTests.cs` (no clash with the new file name);
  WP133's `1888b6f28` drops the `PlayerEquipmentAdapter` row from `AliasedSites`; WP137 regenerates
  `patch-targets.md` (it must then list `Patch83_OffspringParentRepair`,
  `TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch` and `Patch125_HideoutClanGuard`)
  and its Part D step takes the `.ai/verification.md` api-snapshot row and `/verify` Step 2d; WP139 replaces A1's `[Check]` and `[Swallowed]` lines
  with `IOutcomeLog` calls, adds `tools/ingame_checks/1076.toml` and edits the Patch49 and Patch20
  files again (`cd4fb3719`); WP142 to WP144 sync the lessons and indexes this WP skipped.
- Merge hot spots (append-style, keep both sides): `SubModule.cs`, `FeatureModules.cs`, the reflection
  test and doc, the registry, `CoopVetoClassificationTests.cs`, `.ai/review-reference.md`, `adapters.md`.
  If WP111 merges after this branch, the integrator keeps its campaign-statics paragraph and this plan's
  two paragraphs, all before `## Testing`.
  A later WP113 puts its `ShaderCompileNoticeEntry.Stop();` above the `MainMenuStep.Run` call.
- Review focus: WP106's Patch69 comment survives; `Settle` rethrows non-NREs through
  `RethrowStackPreserver`; Patch49's `Priority.Last` prefix sets `__state`; A1's lines read
  `[Check] id=<id> result=<pass|fail> <detail>`.
- Deferred on purpose: the Feral Orcs content behind Patch125 (content) and the co-op `[ClientOnly]`
  server split (D7).
