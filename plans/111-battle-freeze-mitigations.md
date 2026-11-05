# Plan 111: Cap battle corpses, end AI battles stuck at N vs 0, and make mission behaviour setup run

> **Executor instructions**: Follow this plan step by step in your own worktree on the 1.4.x line
> (Bannerlord v1.4.8). Run every verification command and confirm the expected result before the next
> step. If anything in "STOP conditions" occurs, stop and report; do not improvise. The work is split
> into three Parts (A, B, C). Each Part ends in its own commit(s) and its own done criteria; finish one
> before starting the next. You never push, open a PR, tag, or merge into `bannerlord-1.4.5`. When all
> Parts are done, report the result in your hand-off (Step D1); `plans/README.md` belongs to the
> integrator, so never edit it on this branch.
>
> **Drift check (run first, in the worktree)**: `git diff --stat 1b00d1acd..HEAD -- $P` with `$P` set
> as in "Scope" below. Earlier work packages of this programme (WP101 to WP110) are expected to have
> changed some of these files (settings counts, `FeatureModules.cs`, `harmony-patches.md`). Compare the
> live files with the excerpts in "Current state" and STOP only on a real mismatch: an excerpt this plan
> edits is gone or different. Part C has a stricter precondition (Step C0).

## Status

- **Priority**: P0 for Part A (#701 battle corpses); P1 for Parts B (#748) and C (#606, #699)
- **Effort**: L (three Parts, four commits, about 70 files, most of them ported verbatim)
- **Risk**: MED (every battle gets a corpse override; Siege Dismount runs for the first time on this
  line; new code runs inside `Mission.AfterStart`)
- **Depends on**: WP105 (composition root: `Main/Composition`), WP109 (MissionStartGuard). Through them:
  WP101 (baseline and D20 laptop Armory), WP104 (`TAOM.Tests/Infrastructure/RepoPaths.cs`), WP106, WP107.
- **Category**: bug (freeze and stall mitigation)
- **Wave / lane**: wave 1, lane A
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the changelog entry), D2 (both
  features arrive as their `*Module.cs` plus one `FeatureModules.All` line; nothing hand-wired into
  `IoC.cs`/`SubModule.cs`), D7 (no co-op features; the stuck-battle sweep keeps its co-op stand-down),
  D8 (subjects carry `v2.0.28`), D10 (ordinary session), D19 (git through the Bash tool only), D20 (no
  ModuleData commit before the WP101 laptop Armory update)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: port of #701 and #703 (`c17541f2c`), #748 and #750 (`a913629cd`), #606 (`2a5363e90`,
  `fac17e05c`), #699 (`cfca3c5fd`, `9cb6d49e7`, `bb85e9284`). Audit findings closed: LOADING-HANG-07,
  FREEZE-THREADING-08.

## Why this matters

1. **Mid-battle freezes (#701, LOADING-HANG-07).** Players on the public 1.4.8 build report battles that
   freeze with combat sound still playing; 1.4.8 freezes stopped when players lowered Number of Ragdolls
   and Number of Corpses, and the maintainer's own 1.4.8 history names ragdolls and corpses among the
   causes of endless loading screens. Re-read this session on the installed v1.4.8 native DLL: a body's
   default timeout is 3,600 s, Unlimited keeps up to 1,021 bodies, and the native mission tick walks the
   whole corpse list every frame. Part A fades bodies after 60 s, caps a battle at 25 (never above the
   player's own option), offers once to lower the two options, and logs one `[BattleSettings]` line per
   battle so the next freeze report says what was set. It is a mitigation, not a proven cure (the 1.5.x
   line calls "this is what freezes the game" a hypothesis); the skeleton-buffer guard (WP109) covers the
   other freeze mechanism confirmed present in the v1.4.8 DLL.
2. **Campaign stalls (#748, FREEZE-THREADING-08).** On v1.4.8 `MapEvent.Update` runs a simulation round
   only while both sides have healthy troops and sets a winner only inside a round, so an AI battle whose
   side drops to 0 between rounds never ends; its faction's lords keep piling in and stand still for good.
   The TAOM trigger (#750, alignment desertion inside a battle) is already fixed on this line by
   `1b00d1acd`; Part B adds the hourly sweep that ends a stuck battle whatever the trigger.
3. **Dead setup code (#606).** On v1.4.8 `Mission.AfterStart` sends `OnBehaviorInitialize` before TAOM's
   behaviours exist, so Siege Dismount has been inert since its port and Smart Cavalry AI and Mixed
   Formations never log their settings. The Harad player's logs confirm it: E3 adds all three behaviours
   (lines 196, 198, 199) and E4 too (399, 401, 402), and neither log has a single `[SiegeDismount]`,
   `[SmartCavalryAI]` or `[MixedFormations]` line. Part C moves the setup to `AfterStart`, after first
   landing the #699 guards that stop a Custom Battle siege from throwing out of mission start (on 1.5.x
   the move without the guards made every Custom Battle siege reload forever).

## Current state

### Prerequisites from earlier work packages (checked in Step 0)

- WP105: `Main/Composition/{TaomFeatureModule,FeatureDecls,FeatureModules,ModuleRunner,FeatureModuleHooks}.cs`,
  ported from the plan 018 merge `1efdf2985`. A module overrides `Id`, `RegisterServices(IRegistrator)`,
  `MissionBehaviors` (`MissionBehaviorDecl.Of<T>(Func<Mission, IResolver, T>)`), `CampaignBehaviors`
  (`CampaignBehaviorDecl.Of<T>(Func<IResolver, T>)`) and `OnPhase(ApplyPhase, IResolver)`.
  `FeatureModules.All` is an ordered array; new modules are appended. `SubModule.cs` holds six
  `FeatureModuleHooks.` calls (RunPhase x4, `AddGameStartContent(gameStarterObject)`,
  `AddMissionBehaviors(mission, AddTaomBehavior)`); `IoC.cs` holds `new ModuleRunner(FeatureModules.All, ...)`.
  `FeatureModulesTests` fails if a module's behaviour is also built in `SubModule.cs`.
- WP104: `TAOM.Tests/Infrastructure/RepoPaths.cs` with `ReadSource(string relativePath, bool stripComments = false)`.
- WP109: `Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs` (Patch103 wraps each of the six
  calls inside `Mission.AfterStart` in a try with a filter).

### Part A facts (origin/bannerlord-1.4.5)

- Nothing on 1.4.x calls `SetOverrideCorpseCount`, `SetMissionCorpseFadeOutTimeInSeconds` or reads
  `NumberOfRagDolls`/`NumberOfCorpses` (git grep of `Main`, empty). No `BattleCorpse*`,
  `GraphicsOptionsAdapter` or `taom_bc_advice_*` exists.
- `Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs:27-53`, `IsEligible(Mission)`: false for a
  null mission or a network session; then `Mode == MissionMode.Battle`, then
  `IsFieldBattle || IsSiegeBattle || IsSallyOutBattle`, then `CombatType == Combat`. Part A reuses it.
- `Main/Features/TaomSettings.cs` (Id `TAOM`, `json2`, `using TaleWorlds.Library;` at :7):
  `DeadMountDespawnDelaySeconds` at :760, then `// --- Battle Tactics / Companion Roles ---` at :762 (the
  1.5.x insertion anchor). GroupOrders in use at 1b00d1acd: 10, 15, 20-51, 100 (55 is free).
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: `ActionButtons` set at :80-83 holding
  `"RebuildDistanceCacheAction"`; `PlayerLocal` set at :86-98 ending with `"OfferLeaveOnArrival"`.
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`: `AssertSplit(typeof(TaomSettings),
  reflected: 229, covered: 180)` at :210; `Assert.AreEqual(180, report.Covered, ...)` at :356
  (`ComputeAcross_SkipsNulls`) and :423 (`TheRealSettings_HashWithoutThrowing_AndCoverMostOfThem`);
  `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` requires `docs/features/coop-interop.md` and
  `docs/features/bannerlord-together-compat.md` to quote the total (`**N**` or ` N MCM settings`) and
  `**R are simulation-relevant**`. The numbers will have moved by WP111; always work from the live values.
- `Main/_Module/ModuleData/taom_module_strings.xml` ends with the `<!-- SpecialResources -->` block and
  `</strings>`. `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests` fails
  on a `{=taom_*}` code key with no registered row; `LanguageFileCoverageTests` fails when any of the 12
  `Languages/<L>/std_taom_*.xml` lacks a row for a registered English key.

### Part B facts

- `Main/Features/MapEventGuard/` holds only `Hooks/Patch82_MapEventObserverInvariant.cs` and
  `Hooks/Patch84_SiegeAftermathMenuGuard.cs`; no stuck-battle code exists (git grep `StuckBattle`, empty).
- #750 is already fixed here: `Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs:49`
  `if (party.MapEvent != null)` and :78 `if (garrison.MapEvent != null)` (commit `1b00d1acd`).
- Present on 1.4.x: `IEnlistmentStateQuery` (`bool IsEnlisted` :13, `bool IsCommanderParty(string partyId)`
  :24), registered in `EnlistmentIoC.cs:34`; `ICoopSessionProvider` (`IsSessionActive` :21,
  `ShouldDeferToHost` :42), registered in `CoopInteropIoC.cs:16`; `TaomConsole.RunInCampaign(List<string>,
  string, Func<List<string>, string>)` (`Main/Features/DevConsole/TaomConsole.cs:32`).
  `ConsoleCommandBindingTests` checks every console command's shape, `taom` group, unique name, `print_`
  prefix for read-only commands and the no-campaign path.

### Part C facts

- The four `OnBehaviorInitialize` overrides in `Main`:
  - `Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs:27-36` calls
    `_service.OnMissionStart(mission?.IsSiegeBattle ?? false, mission?.SceneName)`. Load-bearing, never runs.
  - `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs:33-43` and
    `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs:46-57` only log.
  - `Main/Features/CustomBattles/CustomBattleTeamFixBehavior.cs:18-22`, added by postfixes on the
    mission-opening calls (`BannerlordMissions_CustomBattle_Patch.cs:27`, `BannerlordMissions_Siege_Patch.cs:40`)
    before `AfterStart`, so its override does fire. Legitimate.
- `Main/Adapters/PlayerMountAdapter.cs:17,24,...` reads `Hero.MainHero?.BattleEquipment`;
  `Main/Adapters/PartyMountInventoryAdapter.cs:16,28` reads `MobileParty.MainParty?.ItemRoster`.
- `Main/Features/SiegeDismount/SiegeDismountService.cs:45-54`: logs `siege detected`, then calls
  `_mount.HasMount()` at :47 OUTSIDE the `try` that opens at :54.
- Siege Dismount MCM defaults (`TaomSettings.cs:415`, :420): `EnableSiegeDismount = true`,
  `SiegeMountBehavior = 3` (Auto Remount). After Part C the feature acts in every campaign siege.
- These Part C files are byte-identical on 1.4.x to the 1.5.x pre-images (checked this session with
  `git diff`): the three behaviours, `docs/features/{siege-dismount,mixed-formations,smart-cavalry-ai}.md`
  equal `fac17e05c^`; both adapters equal `cfca3c5fd^`; `SiegeDismountService.cs` and
  `SiegeDismountServiceTests.cs` equal `bb85e9284^`. Between `fac17e05c` and `bb85e9284` only
  `9cb6d49e7` and `bb85e9284` touched `docs/features/siege-dismount.md`. So taking a file's version from
  the named 1.5.x commit is exactly the cherry-pick of its hunks (Part C uses `git checkout <sha> <path>`).
  One exception: WP110 edits `docs/features/smart-cavalry-ai.md` (the `EnableSmartCavalryAI` rename at its
  lines 149 and 161) and merges before this plan, so that doc is NOT transplanted: C0 leaves it out and
  C2.3 inserts `fac17e05c`'s block by hand. WP110 does not touch `SmartCavalryAIMissionBehavior.cs` (it reads
  `_settings.IsEnabled`).
- `.claude/rules/harmony-patches.md` has no lifecycle section; its `## Latches & Toggle Gates` header is at
  :89 and `## Which thread runs your target (MANDATORY before the first line of a patch)` at :99.
  `.claude/rules/adapters.md:25` is the computed-getter paragraph and `## Testing` is :27.
- `TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs` does not exist. `GameAssemblies.EnsureLoaded()`
  and `GameAssemblies.Diagnostics` exist (`TAOM.Tests/Migration/GameAssemblies.cs`). `Main/TAOM.csproj`
  grants `InternalsVisibleTo` to `TAOM.Tests` and references the `IsExternalInit` package (records compile).

### Engine facts (installed v1.4.8, `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, cache `C:\Users\mikew\.taom-src\v1.4.8`)

Mission start (`TaleWorlds.MountAndBlade.Mission`):
- `public void AfterStart()` :3799. The `OnBehaviorInitialize` loop calls `MissionBehaviors[i].OnBehaviorInitialize();`
  at :3811; then `cachedSubModule2.OnMissionBehaviorInitialize(this);` at :3815; `EarlyStart` :3819;
  `AfterStart` :3825; `CurrentState = State.Continuing;` :3835.
- `public void AddMissionBehavior(MissionBehavior missionBehavior)` :4615 ends with `missionBehavior.OnCreated();` :4628.
- `public bool IsSiegeBattle => MissionTeamAIType == MissionTeamAITypeEnum.Siege;` :1375;
  `MissionCombatantsLogic.EarlyStart` (:92) sets `Mission.Current.MissionTeamAIType = TeamAIType;` (:94).
- Dump `MountAndBlade/TaleWorlds.MountAndBlade/TaleWorlds.MountAndBlade/DefaultBattleMissionAgentSpawnLogic.cs`:
  `AfterStart` (:158) only wires the banner-bearer logic and `BattleSpawnModel.OnMissionStart()`; spawning
  runs from `OnMissionTick` (:171).
- `TaleWorlds.MountAndBlade.MissionState`: `TickLoading` reloads while `!_missionInitializing` (:221-233);
  `FinishMissionLoading` clears `_missionInitializing` (:335) before `CurrentMission.AfterStart()` (:345).
  1.4.x SaveShield finalizes `MissionState.FinishMissionLoading` under MISSION-INIT
  (`Dependencies/Foundation/SaveShield.cs:84`) and swallows every type outside co-op (`SaveShieldPolicy.ShouldSwallow`).

Corpses and options:
- `Mission.SetMissionCorpseFadeOutTimeInSeconds(float corpseFadeOutTimeInSeconds)` :1723 and
  `Mission.SetOverrideCorpseCount(int overrideCorpseCount)` :1728, each one `MBAPI.IMBMission` call.
  `Mission.Tick(float)` :1830-1833 calls `IMBMission.Tick`, which `MissionState.TickMissionAux` (:212-219)
  calls every frame; `Mission.Initialize()` (:1798-1809) calls `IMBMission.InitializeMission` (:1808).
- `TaleWorlds.Engine.Options.NativeOptions`: `NativeOptionsType.NumberOfRagDolls` (:63),
  `public static float GetConfig(NativeOptionsType type)` :274, `public static void SetConfig(NativeOptionsType type, float value)` :390,
  `public static SaveResult SaveConfig()` :412.
- `TaleWorlds.MountAndBlade.ManagedOptions`: `ManagedOptionsType.NumberOfCorpses` (:16),
  `public static void SetConfig(ManagedOptionsType type, float value)` :311, `public static SaveResult SaveConfig()` :493
  (returns `BannerlordConfig.Save()`).
- `TaleWorlds.MountAndBlade.BannerlordConfig`: `NumberOfCorpses` :279 (`[ConfigPropertyInt(0..5)]`),
  `BattleSize` :304 (0..6); `Save()` :588 calls `MBAPI.IMBBannerlordConfig.ValidateOptions()` :605.
  `TaleWorlds.Library.SaveResult` has `Success`.
- Vanilla options screen `OptionsVM.ApplyChangedOptions` (dump `TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions/OptionsVM.cs:880`)
  ends with `NativeOptions.SaveConfig()` and `ManagedOptions.SaveConfig()` (:941-942); `NumberOfRagDolls`
  appears nowhere in `OptionsVM.cs`. In the whole v1.4.8 dump only `MissionMultiplayerDuel.cs:335` calls a
  corpse setter (`SetMissionCorpseFadeOutTimeInSeconds(1f)`).
- `InformationManager.ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)` :91;
  `InquiryData(string titleText, string text, bool isAffirmativeOptionShown, bool isNegativeOptionShown, string affirmativeText, string negativeText, Action affirmativeAction, Action negativeAction, ...)` :33;
  `InformationMessage(string information, Color color)` :21; `TextObject SetTextVariable(string tag, TextObject variable)` :329.
  Vanilla keys `{=1awQTVqN}Number Of Ragdolls`, `{=h6wUbray}Number Of Corpses`, `{=hdLs35aB}Low (25)`,
  `{=fM9E7frB}Performance` exist in the installed `Modules/Native/ModuleData/global_strings.xml` (:383, :380, :1107, :428).

Native, re-read this session on `TaleWorlds.Native.dll` (14,185,944 bytes, SHA-256
`2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`, Version.xml `v1.4.8`), engine-method ids
resolved through the native registration table and disassembled with capstone:

| Native piece | v1.4.8 fact |
|---|---|
| `set_mission_corpse_fade_out_time_in_seconds`, id 559 | RVA 0x70C160: a value within 0.01 of -1 writes 3,600.0 to mission+0x1044EC0; anything else is clamped to 1.0-86,400.0 |
| `set_override_corpse_count`, id 561 | RVA 0x70C1B0: below -1 becomes -1, otherwise min(value, 1,021) to mission+0x1044EC4 |
| mission reset | RVA 0x5CF3C0 writes 3,600.0 and -1 to both fields; `IMBMission.InitializeMission` (0x7076D0) calls 0x5D1B60, which calls it at 0x5D1D46, so every `Mission.Initialize` restores both defaults |
| corpse limiter | RVA 0x5D3A90: returns early when a native global is 1 or 4 (the 1.5.x reading names these MultiClient and SingleReplay, `MBCommon.GameType` values on v1.4.8, MBCommon.cs:7-15; the global itself was not identified); uses the override unless it is -1, else the option through a jump table at 0x5D3BB4: 0, 25, 75, 125, 250, 1,021 |
| expiry walk | `IMBMission.Tick` (0x7083C0) calls 0x5C37D0, which compares each corpse entry's stamp plus the fade time with the current time (0x5C5800-0x5C5813) every frame |

So the 1.5.x numbers hold on v1.4.8: 3,600 s default fade, 1,021-body Unlimited cap, reset per mission.

Campaign statics and Siege Dismount reads:
- `Hero.MainHero => CharacterObject.PlayerCharacter.HeroObject;` Hero.cs:892; `Hero.BattleEquipment` :219.
- `CharacterObject.PlayerCharacter => Game.Current.PlayerTroop as CharacterObject;` CharacterObject.cs:250; `HeroObject` :88.
- `MobileParty.MainParty => Campaign.Current.MainParty;` MobileParty.cs:256; `ItemRoster => Party.ItemRoster;` :1076.
- `Campaign.Current { get; private set; }` Campaign.cs:376; `Game.PlayerTroop { get; set; }` Game.cs:54;
  `public BasicCharacterObject()` BasicCharacterObject.cs:231; `ItemRoster.AddToCounts(EquipmentElement, int)` :194.

Stuck battles (`TaleWorlds.CampaignSystem.MapEvents.MapEvent`, `public sealed class MapEvent : MBObjectBase` :22, `internal MapEvent()` :473):
- `internal void Update()` :995. A round runs only when `(DefenderSide.TroopCount > 0 && AttackerSide.TroopCount > 0)`
  (or before the first update) and the simulation time is past (:1014); `finish` becomes true once
  `BattleState != None` (:1021-1024) or on `DiplomaticallyFinished`.
- `public void SetOverrideWinner(BattleSideEnum winner)` :1058 sets `BattleState`; the `BattleState` setter
  (:284-301, `internal set`) calls `OnBattleWon()` for a victory, which runs `CalculateMapEventResults()` and,
  for a non-player event, `CalculateAndCommitMapEventResults()` (:1048-1056).
- `internal bool CheckIfOneSideHasLost()` :1275-1290: with both sides at 0 the defenders win.
- `public bool CanPartyJoinBattle(PartyBase party, BattleSideEnum side)` :1447-1454 requires every party on
  both sides to be `IsActive`.
- Members the adapter reads: `IsFinalized` :225, `BattleStartTime` (`CampaignTime`) :227, `IsRaid` :205,
  `IsForcingVolunteers` :207, `IsForcingSupplies` :209, field `DiplomaticallyFinished` :63, `InvolvedParties` :139,
  `MapEventSettlement` :155, `IsPlayerMapEvent` :282 (`PlayerMapEvent => MobileParty.MainParty?.MapEvent` :107).
- `MapEventSide`: `LeaderParty` :94, `TroopCount => RecalculateMemberCountOfSide()` :112,
  `public int RecalculateMemberCountOfSide()` :319 (sum of `NumberOfHealthyMembers`), `RemovePartyInternal` :296-317
  (finalizes the event when a side loses its last party).
- `PartyBase.MapEventSide { get; set; }` :289-322 (public setter); `IsActive` :94; `MobileParty` :122;
  `IsSettlement` :124; `Id` :179; `static MainParty` :208; `MapFaction` :236; `MapEvent` :287; `SiegeEvent` :106.
  `MapEvent.RemoveInvolvedPartyInternal` (:852-858) nulls `TroopUpgradeTracker` only when the removed party
  is the main party (the sweep never detaches the main party).
- `MobileParty.IsCurrentlyUsedByAQuest` :417; `MobileParty.MapEvent` :1070; `PlayerEncounter.Battle` :138,
  `EncounteredParty` :150; `SiegeEvent.BesiegerCamp` (public readonly field) :656; `BesiegerCamp.LeaderParty` :31;
  `CampaignTime.ElapsedHoursUntilNow` (float) :108; `MapEventManager.MapEvents` :16; `Campaign.MapEventManager` :396.
- `CampaignEvents.HourlyTickEvent` :837; `IMbEvent.AddNonSerializedListener(object owner, Action action)` :7;
  `MbEvent.AddNonSerializedListener` prepends to a linked list, so the last listener registered runs first.
- `DestroyPartyAction.ApplyInternal` (DestroyPartyAction.cs:10-27) calls `MobileParty.RemoveParty` (:3516-3561);
  neither it nor `OnRemoveParty` (:3015-3038) writes `MapEventSide`. The 1.5.x claim that vanilla's
  `DestroyPartyAction` detaches every non-quest party was read on v1.5.4 and is UNVERIFIED on v1.4.8.

### Conventions that bind this work

- ADR-007 adapters: services never touch sealed TaleWorlds types; the ported adapters hold them.
- ADR-002 thin entry points (under 150 lines); ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no
  `#if DEBUG` outside IoC registration. The ported code already complies; keep it that way.
- TDD: each step below runs the test that fails first, then the change that makes it pass.
- D2: a ported 1.5.x module keeps its `*Module.cs`, its patch numbers and its 1.5.x wiring tests; one
  `FeatureModules.All` line, appended at the end. A module with `OwnsSaveData = true` is a STOP.
- MCM json2 rule: never flip a persisted setting's default; rename it instead. The five new BattleCorpses
  keys are new on 1.4.x (git grep empty) and the `TaomSettings` Id, folder and format are the same on both
  lines, so keep the 1.5.x names and defaults exactly.
- Localization: new text is `{=key}default`, registered in `taom_module_strings.xml`; the 12 language files
  get English seed rows now; translation is WP135 (a paid run; maintainer only). Never run
  `tools/translate_with_claude.py` as a command.
- `.claude/rules/harmony-patches.md` loads for `Main/**/Hooks/**`; this work adds no Harmony patch.

## Commands

Run every command from the worktree root `/c/Users/mikew/source/repos/taom-port/111` (in Git Bash prefix
each call with `cd /c/Users/mikew/source/repos/taom-port/111 &&`). Git only through the Bash tool (D19).

| Purpose | Command | Expected |
|---|---|---|
| Restore (once) | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` (each step gives its own filter; several terms are joined with a pipe inside the quotes) | as each step states |
| Data | `python tools/validate_moduledata.py` | no ERROR line that the preflight run did not have |
| Docs | `python tools/lint_docs.py` | no finding in a file this plan touched that the preflight run did not have |
| Doc graph | `python tools/check_doc_graph_ratchet.py` | exit 0, or counts no higher than at preflight |

Both `-p:` flags are mandatory on every build and test (on 1.4.x `Main/TAOM.csproj` does not gate
`CopyModule` on `DisableModuleCopy`). Never `./build.ps1`; never two builds at once in one worktree; always
build before `--no-build` tests.

## Scope

**In scope** (set `$P` to this list for the drift check):

```
P="Main/Composition/FeatureModules.cs Main/Features/BattleCorpses Main/Adapters/IGraphicsOptionsAdapter.cs Main/Adapters/GraphicsOptionsAdapter.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs Main/Features/TaomSettings.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages TAOM.Tests/Features/BattleCorpses TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/battle-corpses.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mount-despawn.md docs/reviews/rca-battle-corpses-2026-10-01.md Main/Adapters/IStuckBattleAdapter.cs Main/Adapters/IStuckBattleEventAdapter.cs Main/Adapters/StuckBattleAdapter.cs Main/Adapters/StuckBattleEventAdapter.cs Main/Features/MapEventGuard TAOM.Tests/Features/MapEventGuard docs/features/map-event-guard.md docs/features/dev-console.md docs/reference/doc-lookup.md docs/reference/provenance-register.md docs/reviews/rca-stuck-battle-guard-2026-10-06.md Main/Adapters/PlayerMountAdapter.cs Main/Adapters/PartyMountInventoryAdapter.cs Main/Features/SiegeDismount Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs docs/features/siege-dismount.md docs/features/mixed-formations.md docs/features/smart-cavalry-ai.md docs/reviews/rca-siege-dismount-custom-battle-2026-09-30.md .claude/rules/harmony-patches.md .claude/rules/adapters.md"
```

**Single-owner files: NOT edited.** `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
`Directory.Build.props`, `.claude/settings.json`. Under D2 both features wire through `FeatureModules.All`;
if anything seems to need one of these files, STOP.

**Out of scope** (do not touch, even though the 1.5.x commits carry them):
- `CHANGELOG.md` (D1), `docs/INDEX.md`, `docs/reference/feature-map.md`, `docs/reviews/lessons/*`,
  `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `tools/README.md`, `CLAUDE.md`, `AGENTS.md`
  (WP142 and WP143 sync these once).
- `.claude/skills/deep-review/SKILL.md` (WP138 ports the skill's tip state, which carries the lifecycle line).
- Everything SignatureStrikes (1.5.x-only feature) and everything RealmBorders or realm fill in `cfca3c5fd`.
- Later 1.5.x edits to these files that are not in this pack: `6eb2e1b90` and `685c6a1e0` (extend
  `GraphicsOptionsAdapter`, WP117), `639df895b` and `06b1deaab` (`MountDespawnMissionGate` comment for
  1.5.x-only features), `a4b8f7e21` (co-op note in `siege-dismount.md`, D7), `d4af27d3e`, `2e34cade1`,
  `cd1fbdb6f`, `7cd4920e7` (1.5.x doc upkeep).
- `plans/README.md`: the integrator records results after the merge (Step D1). Reading it from the main
  checkout is fine; no commit on this branch touches it.
- Running a translation, creating an issue, pushing.

## Git workflow

- Create the worktree from the main checkout, which must already hold every WP merged before this one:
  `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/111 -b port148/111-battle-freeze-mitigations bannerlord-1.4.5`
- Work only in the worktree. Stage explicit paths (`git add <paths>`); never `git add -A`, `git add .`,
  `git commit -a`. Never stash, reset or clean anything you did not create.
- 1.5.x commits are applied with `git cherry-pick -n <sha>` (Parts A and B) or, where the 1.4.x file equals
  the 1.5.x pre-image byte for byte, with `git checkout <sha> <path>` (Part C). Never let cherry-pick,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit: on this laptop `GIT_EDITOR=true`
  commits the 1.5.x message unedited.
- Bookkeeping paths in a cherry-pick: conflicted ones get `git checkout --ours <path>` then
  `git add <path>`; cleanly changed ones get `git checkout HEAD <path>`. The same rule holds wherever a
  step below says "take HEAD's side": first read `git status --porcelain`; for a path shown as conflicted
  (`UU`, `AA`, `DU`, `UD`) use `--ours` plus `git add`, and for a path that applied cleanly use
  `git checkout HEAD <path>` (on a clean path `--ours` would restore the 1.5.x version instead).
- Commit: write the message to `/c/Users/mikew/source/repos/taom-port/111-msg.txt` (outside the worktree),
  then run `git commit -F /c/Users/mikew/source/repos/taom-port/111-msg.txt` in the NEXT, separate Bash call.
- Every subject: `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters, body wrapped at 72, no
  em or en dash, no Co-Authored-By or "Generated with" line. After each commit run
  `git log -1 --format=%s | awk '{print length($0)}'` (expect at most 72) and
  `git log -1 --format=%s | grep -E '^[a-z]+\([a-z0-9-]+\): v2\.0\.28 - '` (expect the subject printed).
- If a hook blocks a commit (subject, CHANGELOG, ModuleData, files-tracked gate), STOP. Never `--no-verify`.

## Steps

### Step 0: Preflight (no edits)

1. Create the worktree (Git workflow above), then `dotnet restore TAOM.sln`.
2. Dependencies present (each line must print its OK tag or the stated count):
   ```
   grep -q "TaomFeatureModule\[\] All" Main/Composition/FeatureModules.cs && echo OK-105-list
   grep -q "class MissionBehaviorDecl" Main/Composition/FeatureDecls.cs && grep -q "class CampaignBehaviorDecl" Main/Composition/FeatureDecls.cs && echo OK-105-decls
   grep -q "OnPhase(ApplyPhase phase, IResolver resolver)" Main/Composition/TaomFeatureModule.cs && echo OK-105-module
   grep -c "FeatureModuleHooks\." Main/SubModule.cs        # expect 6
   grep -c "new ModuleRunner(FeatureModules.All" Main/IoC.cs  # expect 1
   grep -q "ReadSource(string relativePath, bool stripComments" TAOM.Tests/Infrastructure/RepoPaths.cs && echo OK-104
   test -f Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs && echo OK-109
   ```
3. Engine identity:
   ```
   ls -l "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.Native.dll" | awk '{print $5}'   # 14185944
   sha256sum "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/TaleWorlds.Native.dll"                    # 2a5e0e0b...6e12a
   pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission
   sed -n '1723p;1728p;3811p;3815p;4628p' /c/Users/mikew/.taom-src/v1.4.8/TaleWorlds.MountAndBlade.Mission.cs
   grep -c 'Value="v1.4.8"' "$BANNERLORD_GAME_DIR/bin/Win64_Shipping_Client/Version.xml"            # 1
   ```
   Expect from the `sed`, in order: the `SetMissionCorpseFadeOutTimeInSeconds(float ...)` declaration, the
   `SetOverrideCorpseCount(int ...)` declaration, `MissionBehaviors[i].OnBehaviorInitialize();`,
   `cachedSubModule2.OnMissionBehaviorInitialize(this);`, `missionBehavior.OnCreated();`.
4. D20: `/c/Users/mikew/source/repos/TAOM/plans/README.md` (the main checkout's copy; read only) shows WP101
   done with the laptop `LOTRLOME_Armory` updated and a validator baseline recorded.
5. Baselines (save outputs outside the worktree, for example `/c/Users/mikew/source/repos/taom-port/111-before-*.txt`):
   Build; Test (record Total, Passed, Failed, Skipped; Failed must be 0); Data; Docs; Doc graph.
6. Drift check: `git diff --stat 1b00d1acd..HEAD -- $P`.

**Verify**: every check above as stated. Any miss is a STOP.

---

## Part A: battle corpse fade and cap (#701), from `c17541f2c`

`c17541f2c` "fix(battle-corpses): v2.0.32 - fade and cap battle corpses (#701)". Method: `cherry-pick -n`,
then resolve. Simulated this session onto `1b00d1acd` (`git merge-tree --write-tree --name-only
--no-messages --merge-base=c17541f2c^ origin/bannerlord-1.4.5 c17541f2c`, exit 1): conflicts in
`Main/Composition/FeatureModules.cs`, `Main/_Module/ModuleData/taom_module_strings.xml`,
`TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `docs/INDEX.md`,
`docs/features/bannerlord-together-compat.md`, `docs/features/coop-interop.md`,
`docs/reviews/lessons/build-tooling-workflow.md`, `docs/reviews/lessons/localization-ui.md`. Everything
else applies cleanly. Earlier WPs may add conflicts in `TaomSettings.cs` or `CoopSettingsRelevance.cs`;
resolve those by keeping HEAD and adding exactly the lines `git show c17541f2c -- <path>` adds.

### Step A1: Apply and settle the bookkeeping

```
git cherry-pick -n c17541f2c
git status --porcelain
git checkout --ours docs/INDEX.md docs/reviews/lessons/build-tooling-workflow.md docs/reviews/lessons/localization-ui.md
git add docs/INDEX.md docs/reviews/lessons/build-tooling-workflow.md docs/reviews/lessons/localization-ui.md
git checkout HEAD docs/reference/feature-map.md
```
(If `docs/INDEX.md` applied cleanly instead, use `git checkout HEAD docs/INDEX.md` for it.)

Then take HEAD's side of the remaining conflicts for now; Steps A3 to A5 add this commit's lines:
```
git checkout --ours Main/Composition/FeatureModules.cs Main/_Module/ModuleData/taom_module_strings.xml TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md
git add Main/Composition/FeatureModules.cs Main/_Module/ModuleData/taom_module_strings.xml TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/coop-interop.md docs/features/bannerlord-together-compat.md
```

**Verify**: `git diff --name-only --diff-filter=U` prints nothing; `git diff --cached --name-only` lists
the 8 `Main/Features/BattleCorpses/**` files (including `BattleCorpsesModule.cs`, which D2 keeps), both
`Main/Adapters/*GraphicsOptionsAdapter.cs`, `CoopSettingsRelevance.cs`, `MountDespawnMissionGate.cs`,
`TaomSettings.cs`, the 5 `TAOM.Tests/Features/BattleCorpses/*` files, `docs/features/battle-corpses.md`,
`docs/features/mount-despawn.md` and `docs/reviews/rca-battle-corpses-2026-10-01.md`, and nothing under
`docs/reviews/lessons`, `docs/INDEX.md` or `feature-map.md`. `grep -c "GroupOrder = 55" Main/Features/TaomSettings.cs`
prints 1 (if 2, an earlier WP took 55: use the lowest free GroupOrder above 55 in the new attribute and
say so in the commit body).

### Step A2: RED

Build, then:
```
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~TAOM.Tests.Features.BattleCorpses|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests|FullyQualifiedName~LanguageFileCoverageTests"
```
**Verify**: build exit 0. Exactly these fail: `BattleCorpsesWiringTests.FeatureModules_ListTheBattleCorpsesModuleOnce`
(module not listed), `SettingsFingerprintTests.EverySettingsClass_HasItsSplitPinned`,
`EveryDocQuotingTheSettingsCounts_AgreesWithReflection`, `ComputeAcross_SkipsNulls`,
`TheRealSettings_HashWithoutThrowing_AndCoverMostOfThem` (five new settings, three of them simulation-relevant),
and `UnregisteredLocalizationKeyBaselineTests.NoNewUnregisteredLocalizationKeys` (the six `taom_bc_advice_*`
keys). Any other failure is a STOP.

### Step A3: GREEN, list the module

Append one line as the LAST entry of `FeatureModules.All` in `Main/Composition/FeatureModules.cs`:
```
        new Features.BattleCorpses.BattleCorpsesModule(),
```
**Verify**: build; filtered `FullyQualifiedName~TAOM.Tests.Features.BattleCorpses|FullyQualifiedName~FeatureModulesTests`
passes (the BattleCorpses classes are 41 methods, 75 cases with data rows); `git grep -n -E "BattleCorpseMissionBehavior\(|BattleCorpsePolicy" -- Main/SubModule.cs Main/IoC.cs` prints nothing.

### Step A4: GREEN, settings counts

1. `SettingsFingerprintTests.cs`: in `EverySettingsClass_HasItsSplitPinned` add 5 to the `TaomSettings`
   `reflected:` value and 3 to its `covered:` value; in `ComputeAcross_SkipsNulls` and
   `TheRealSettings_HashWithoutThrowing_AndCoverMostOfThem` add 3 to the pinned `report.Covered` value.
   (Classification from the cherry-picked `CoopSettingsRelevance.cs`: `ShowBattleSettingsAdvice` is
   player-local, `ApplyRecommendedBattleSettingsAction` an action button, the other three count.)
2. `docs/features/coop-interop.md`, bullet "No MCM settings parity": the bold total `**T**` becomes T+5;
   "the split is S in `TaomSettings`" becomes S+5; every quote of the simulation-relevant figure R in that
   bullet (`**R are simulation-relevant**`, `all R are in`, `instead of R values`, `a setting in the R`)
   becomes R+3; `hashes those N` and `It covers the N` quote a stale 175 at `1b00d1acd`, and both also become
   R+3 (not N+3, the same correction `c17541f2c` made to the first); the excluded figure E becomes E+2, and "one action button" becomes "two action buttons"
   if that phrase is there.
3. `docs/features/bannerlord-together-compat.md`, row "Settings parity is reported, not exchanged":
   `TAOM's T MCM settings` becomes T+5; both quotes of R become R+3.

**Verify**: build; filtered `FullyQualifiedName~SettingsFingerprintTests` passes. If the failure messages
in A2 implied deltas other than +5 total and +3 relevant, STOP.

### Step A5: GREEN, register and seed the strings

1. In `Main/_Module/ModuleData/taom_module_strings.xml`, insert after the last `<string` line and before
   the trailing blank lines and `</strings>` the seven tab-indented lines this command prints (the
   `<!-- BattleCorpses -->` comment and six rows), verbatim:
   `git show c17541f2c:Main/_Module/ModuleData/taom_module_strings.xml | grep -E "<!-- BattleCorpses -->|taom_bc_advice_"`
2. Build; filtered `FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests|FullyQualifiedName~LanguageFileCoverageTests`.
   Expect RED: `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` names the six keys
   in all 12 languages.
3. Seed the 12 language files with English rows through the translator's own seeding function. This calls
   no API and costs nothing; do NOT run the translator as a command. First with `DRY = True`, then again
   with `DRY = False`:
   ```
   python -B - <<'PY'
   import sys
   sys.path.insert(0, "tools")
   import translate_with_claude as t
   DRY = True
   src = t.REPO_ROOT / "Main" / "_Module" / "ModuleData" / "taom_module_strings.xml"
   for lang, (loc, _name) in t.LANGUAGES.items():
       target = t.TAOM_LANG_DIR / lang / f"std_taom_module_strings_{loc}.xml"
       print(lang, t.sync_missing_ids(src, target, dry_run=DRY))
   PY
   ```
   The dry run must print 12 lines, each listing exactly the six `taom_bc_advice_*` ids; any other id is a STOP.

**Verify**: `grep -c 'id="taom_bc_advice_' Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`
prints 12 lines ending `:6`; `git diff --stat -- Main/_Module/ModuleData/Languages` shows 12 files and
only added lines; build; the step 2 filter now passes.

### Step A6: Re-cite to v1.4.8 (comments and docs; no behaviour change)

A quoted phrase below may span a line break in the file; edit it in place and keep the file's wrapping.

1. `Main/Features/BattleCorpses/BattleCorpsePolicy.cs`: in the summary, "read from the v1.5.3
   <c>TaleWorlds.Native.dll</c>" becomes "read from the v1.5.3 <c>TaleWorlds.Native.dll</c> and re-read on
   v1.4.8"; the comment "(the corpse limiter, v1.5.3)" becomes "(the corpse limiter; the same on v1.4.8,
   jump table at RVA 0x5D3BB4)".
2. `Main/Features/BattleCorpses/Hooks/BattleCorpseMissionBehavior.cs`: "(native, v1.5.3)" becomes
   "(native; read on v1.5.3 and re-read on v1.4.8)".
3. `Main/Adapters/GraphicsOptionsAdapter.cs` line 11: "(<c>OptionsVM.ApplyChangedOptions</c>, v1.5.3)" becomes
   "(<c>OptionsVM.ApplyChangedOptions</c>, v1.5.3; the same on v1.4.8)". Change nothing else in either adapter
   file: WP117 cherry-picks `6eb2e1b90` and `685c6a1e0` onto exactly the `c17541f2c` shape.
4. `Main/Features/TaomSettings.cs`: the comment "// GroupOrder 55: 10, 15, 19-54 and 100 were taken as of
   2026-10-01." becomes "// GroupOrder 55, the same as on the 1.5.x line (#701); free on the 1.4.x line when ported."
5. `docs/features/battle-corpses.md`:
   - "What is verified: the mission tick (native `FUN_1805aefc0`, reached from `IMBMission.Tick`) walks the
     whole corpse list every frame" becomes "What is verified on v1.4.8: the native mission tick
     (`IMBMission.Tick`, RVA 0x7083C0) calls the function at 0x5C37D0, which walks every corpse entry each
     frame"; "read beside `[MissionPerf]` and `[MissionStall]`" becomes "read beside `[MissionStall]` (and
     `[MissionPerf]` once the mission profiler is ported)"; "it was dropped because" becomes "it was dropped
     on the 1.5.x line because".
   - "The two mission-level setters were read from the v1.5.3 `TaleWorlds.Native.dll` with
     `tools/native_decompile.py` and a byte scan for the field offsets:" becomes "The two mission-level
     setters were read from the v1.5.3 `TaleWorlds.Native.dll` on the 1.5.x line and re-read on v1.4.8 for
     the 1.4.x port (see the section On the 1.4.x line below); the table holds on both:".
   - In the table, "Mission reset (`FUN_1805bac50`, from the native Mission constructor, destructor and
     battle-scene setup)" becomes "Mission reset (v1.4.8 RVA 0x5CF3C0, reached from `IMBMission.InitializeMission`,
     which every `Mission.Initialize` calls)".
   - The "Single-player is covered" bullet becomes: "**Single-player is covered.** On v1.4.8 the limiter
     returns early only when a native global is 1 or 4, the `MBCommon.GameType` values MultiClient and
     SingleReplay in the v1.5.3 reading; the global and the fade loop's own gate were not re-traced on v1.4.8."
   - Append to the "fade clock" bullet: " (v1.5.3 reading; on v1.4.8 the walk compares a per-entry stamp with
     the current time, and when the stamp is written was not traced)". Append to the "Nothing in vanilla"
     bullet: " (v1.4.8 dump: `MissionMultiplayerDuel.cs:335` is the only caller)". Append to the ragdolls
     bullet: " On v1.4.8 only the options code names `NumberOfRagDolls`; `OptionsVM.ApplyChangedOptions`
     (OptionsVM.cs:880-942) saves both configs; `BannerlordConfig.Save` calls `ValidateOptions` (:605)."
   - In-game check 3: append "(needs `[MissionPerf]`, which arrives with WP117)". Check 5: "Read
     `[MissionStall]` and `[MissionPerf]`" becomes "Read `[MissionStall]` (and `[MissionPerf]` after WP117)".
   - Replace the whole "## 1.4.8 Backport" section (heading and paragraph) with the block below.
   - Under "## Changelog" add a first line `- <today, YYYY-MM-DD>: ported to the 1.4.x line (Bannerlord v1.4.8), WP111.`
   - Under "## GitHub Issue" add `- **Review record (1.5.x line):** [rca-battle-corpses-2026-10-01.md](../reviews/rca-battle-corpses-2026-10-01.md)`.

   Replacement for "## 1.4.8 Backport":
   ```
   ## On the 1.4.x line (v1.4.8)

   Ported in WP111 of the 2026-11-04 port programme as `BattleCorpsesModule` plus one `FeatureModules.All`
   line (the WP105 composition root); `IoC.cs` and `SubModule.cs` carry nothing for it. The managed calls
   exist on v1.4.8 (taom-src `Mission.cs:1723-1731`; `NativeOptions` :274, :390, :412; `ManagedOptions`
   :311, :493; `BannerlordConfig.NumberOfCorpses` :279, `BattleSize` :304). The native behaviour was re-read
   on the installed `TaleWorlds.Native.dll` (14,185,944 bytes, SHA-256
   `2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`), resolving engine-method ids through
   the native registration table:

   | Native | v1.4.8 |
   |---|---|
   | `set_mission_corpse_fade_out_time_in_seconds` (id 559) | RVA 0x70C160: within 0.01 of -1 writes 3,600 s to mission+0x1044EC0; otherwise clamped to 1-86,400 s |
   | `set_override_corpse_count` (id 561) | RVA 0x70C1B0: below -1 becomes -1, otherwise min(value, 1,021) to mission+0x1044EC4 |
   | mission reset | RVA 0x5CF3C0 writes 3,600 s and -1; `IMBMission.InitializeMission` (0x7076D0) reaches it through 0x5D1B60 |
   | corpse limiter | RVA 0x5D3A90: the override unless -1, else the option through the jump table at 0x5D3BB4 (0, 25, 75, 125, 250, 1,021) |
   | expiry walk | `IMBMission.Tick` (0x7083C0) calls 0x5C37D0, which checks every corpse entry each frame (0x5C5800-0x5C5813) |

   The fingerprint pins moved by five settings and three simulation-relevant ones from the counts the
   1.4.x line had when the port landed.
   ```

**Verify**: build; filtered BattleCorpses tests pass; every hit of `grep -n "v1.5.3" docs/features/battle-corpses.md`
also names v1.4.8 or "the 1.5.x line" (read each hit).

### Step A7: Verify Part A

Build; full Test (0 failed; total = preflight total + 75, or + 41 if your runner counts a data-driven
method once); Data (no new ERROR); Docs (no new finding in touched files); Doc graph (no regression);
dash scan (against HEAD, because the A4 and A6 edits are not staged until A8):
`git diff HEAD -- docs | grep "^+" | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'` prints 0.

### Step A8: Commit Part A

Stage: `git add Main/Features/BattleCorpses Main/Adapters/IGraphicsOptionsAdapter.cs Main/Adapters/GraphicsOptionsAdapter.cs Main/Composition/FeatureModules.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs Main/Features/TaomSettings.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages TAOM.Tests/Features/BattleCorpses TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs docs/features/battle-corpses.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/mount-despawn.md docs/reviews/rca-battle-corpses-2026-10-01.md`

**Verify before committing**: `git diff --cached --name-only | wc -l` prints 38; `git status --porcelain`
shows no unstaged or untracked path inside the worktree.

Message (write to the message file, then commit in the next Bash call):
```
fix(battle-corpses): v2.0.28 - fade and cap battle corpses (#701)

Players on 1.4.8 reported mid-battle freezes that stopped when they
lowered the vanilla Number of Ragdolls and Number of Corpses options.
In a normal battle a fallen soldier never times out: the engine's own
corpse timer is an hour, so bodies pile up until the corpse cap
pushes them out, and on Unlimited that is 1,021 bodies, which the
mission tick walks every frame.

Field battles, sieges and sally-outs now fade each body 60 seconds
after it settles and keep at most 25, through the engine's own
per-battle corpse settings. If your Number of Corpses option is
lower, yours is kept; TAOM never raises it. Stealth missions, towns,
arenas and hideouts keep their bodies. Both values are sliders under
Mod Options, TAOM, Performance, Battle Corpses, and apply mid-battle.

Ragdolls cannot be limited per battle, so on the first main menu
after the game starts TAOM offers to lower Number of Ragdolls to 5
and Number of Corpses to Low when yours are higher. Each option is
only ever lowered, nothing changes unless you click Apply, and an
MCM button does the same at any time.

Each battle also logs one [BattleSettings] line with your ragdoll,
corpse and battle-size options, so a freeze report shows what was
set.

Known limitations: the notice reads in English in every language
until the next translation pass. Whether removing a corpse frees its
agent slot, and whether a lowered ragdoll option applies before a
restart, are still to be checked in game.

Research: taom-src Mission.SetMissionCorpseFadeOutTimeInSeconds and
  SetOverrideCorpseCount (Mission.cs:1723-1731), NativeOptions,
  ManagedOptions, BannerlordConfig, OptionsVM.ApplyChangedOptions;
  v1.4.8 TaleWorlds.Native.dll engine methods 559 and 561 (0x70C160,
  0x70C1B0), mission reset 0x5CF3C0 reached from InitializeMission,
  corpse cap jump table 0x5D3BB4 (0, 25, 75, 125, 250, 1021)
Not-tested: the per-battle apply, the [BattleSettings] line and the
  main-menu offer in game
Ported-from: c17541f2c4595149ea22fe0e473397a513c20da9
```

**Part A done criteria**: the commit exists with that subject (72 or fewer characters);
`git show --stat HEAD | tail -1` reports 38 files; `grep -c "BattleCorpsesModule()" Main/Composition/FeatureModules.cs`
prints 1; full suite 0 failed; Data, Docs and Doc graph show nothing new; `git status --porcelain` empty.

---

## Part B: end AI battles stuck at N vs 0 (#748), from `a913629cd`

`a913629cd` "fix(battles): v2.0.34 - end AI battles stuck at N vs 0 (#748)". Method: `cherry-pick -n`,
then resolve. Simulated this session onto `1b00d1acd` (exit 1): conflicts in
`Main/Composition/FeatureModules.cs`, `docs/reference/feature-map.md`,
`docs/reference/provenance-register.md`, `docs/reviews/lessons/adapters-taleworlds-api.md`,
`docs/reviews/lessons/campaign-mechanics.md`, `docs/reviews/lessons/misc.md`; everything else clean
(`docs/INDEX.md` is a clean bookkeeping change). The #750 follow-up is already on this line (`1b00d1acd`).
If an earlier WP causes a further conflict (for example in `dev-console.md` or `coop-interop.md`), keep
HEAD and add exactly the lines `git show a913629cd -- <path>` adds.

### Step B1: Apply and settle the bookkeeping

```
git cherry-pick -n a913629cd
git status --porcelain
git checkout --ours docs/reference/feature-map.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/misc.md
git add docs/reference/feature-map.md docs/reviews/lessons/adapters-taleworlds-api.md docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/misc.md
git checkout HEAD docs/INDEX.md
git checkout --ours Main/Composition/FeatureModules.cs docs/reference/provenance-register.md
git add Main/Composition/FeatureModules.cs docs/reference/provenance-register.md
```
**Verify**: `git diff --name-only --diff-filter=U` prints nothing; `git diff --cached --name-only` lists
the 4 `Main/Adapters/*StuckBattle*` files, the 6 new `Main/Features/MapEventGuard/` files (including
`StuckBattleModule.cs`), the 3 `TAOM.Tests/Features/MapEventGuard/StuckBattle*Tests.cs`,
`docs/features/coop-interop.md`, `docs/features/dev-console.md`, `docs/features/map-event-guard.md`,
`docs/reference/doc-lookup.md` and `docs/reviews/rca-stuck-battle-guard-2026-10-06.md`.

### Step B2: RED

Build, then filtered `FullyQualifiedName~TAOM.Tests.Features.MapEventGuard|FullyQualifiedName~ConsoleCommandBindingTests|FullyQualifiedName~FeatureModulesTests`.
**Verify**: exactly one failure, `StuckBattleWiringTests.FeatureModules_ListTheModuleOnce`. Any other is a STOP
(a `ConsoleCommandBindingTests` failure on `print_stuck_battles` means a name clash: STOP).

### Step B3: GREEN, list the module

Append as the LAST entry of `FeatureModules.All`:
```
        new Features.MapEventGuard.StuckBattleModule(),
```
**Verify**: build; the B2 filter passes (StuckBattle: 39 tests); `git grep -n "StuckBattleCampaignBehavior(" -- Main/SubModule.cs` prints nothing.

### Step B4: Re-cite the code comments to v1.4.8

1. `Main/Adapters/IStuckBattleAdapter.cs` summary: "No engine type leaves the adapter: a v1.5.4
   <c>MapEvent</c> has no id to key on, so each event is handed out wrapped." becomes "No engine type leaves
   the adapter (ADR-007), so each event is handed out wrapped."
2. `Main/Adapters/IStuckBattleEventAdapter.cs`, `DetachWrecks` summary: "out of the event, the write vanilla's
   <c>DestroyPartyAction</c> makes for a party it destroys outside a quest." becomes "out of the event
   through the public <c>PartyBase.MapEventSide</c> setter (v1.4.8 PartyBase.cs:289-322)."
3. `Main/Adapters/StuckBattleEventAdapter.cs`, comment above `IsWreckToDetach`: "and so is a quest's, which
   vanilla's DestroyPartyAction leaves attached on purpose." becomes "and so is a quest's: the 1.5.x line read
   v1.5.4's DestroyPartyAction leaving a quest party attached; v1.4.8's destroy path was not seen to detach
   any party, so the exclusion is kept as the safe side."
4. `Main/Features/MapEventGuard/StuckBattleService.cs` header: "v1.5.4 <c>MapEvent.Update</c>" becomes
   "v1.4.8 <c>MapEvent.Update</c> (MapEvent.cs:995-1046)".
5. `TAOM.Tests/Features/MapEventGuard/StuckBattleServiceTests.cs` summary: "v1.5.4" becomes "v1.4.8".

**Verify**: `git grep -n "v1\.5\.4" -- Main/Adapters/*StuckBattle* Main/Features/MapEventGuard TAOM.Tests/Features/MapEventGuard`
prints only the comment from item 3; build; the B2 filter passes.

### Step B5: Docs and provenance

A quoted phrase below may span a line break in the file; edit it in place and keep the file's wrapping.

1. `docs/features/map-event-guard.md` (the new "Stuck AI battles" section):
   - "re-checked here against the installed v1.5.4." becomes "checked on the 1.5.x line against v1.5.4 and
     re-checked for the 1.4.x port against the installed v1.4.8."
   - "Read from the v1.5.4 decompile (`taom-src path TaleWorlds.CampaignSystem.MapEvents.MapEvent`):" becomes
     "Read from the v1.4.8 decompile (`taom-src path TaleWorlds.CampaignSystem.MapEvents.MapEvent`: `Update`
     :995-1046, `SetOverrideWinner` :1058-1066, the `BattleState` setter :284-301, `CheckIfOneSideHasLost`
     :1275-1290, `CanPartyJoinBattle` :1447-1454; `MapEventSide.TroopCount` :112, `RecalculateMemberCountOfSide`
     :319, `RemovePartyInternal` :296-317):".
   - "TAOM's daily alignment desertion has no such check and is the one known TAOM producer (#750)." becomes
     "TAOM's daily alignment desertion was the one known TAOM producer (#750); on the 1.4.x line `1b00d1acd`
     added the same check (`AlignmentDesertionBehavior.cs:49`, `:78`). The vanilla skips above were read on
     v1.5.4; on v1.4.8 `DesertionCampaignBehavior.cs:21` skips a party in a map event, and the healing path was
     not re-read."
   - Replace the paragraph "A destroyed party left attached is rarer than it sounds: ... leaves the second
     lock." with: "On v1.4.8 no detach was found on vanilla's destroy path: `DestroyPartyAction.ApplyInternal`
     (DestroyPartyAction.cs:10-27) calls `MobileParty.RemoveParty` (MobileParty.cs:3516-3561), and neither it
     nor `OnRemoveParty` (:3015-3038) writes `MapEventSide`. The 1.5.x note that `DestroyPartyAction` detaches
     every non-quest party was read on v1.5.4 and is UNVERIFIED here. A destroyed party left attached is the
     second lock either way; the sweep keeps the 1.5.x exclusion of quest parties as the safe side."
   - Table row: "(`party.MapEventSide = null`, the write `DestroyPartyAction` makes)" becomes
     "(`party.MapEventSide = null`, the public setter at PartyBase.cs:289-322)".
   - "and the next `Update`, in the same campaign frame (`Campaign.Tick` fires the hourly event, then
     `MapEventManager.Tick`), finishes it." becomes "and a later `Update` finishes it (on v1.4.8, whether that
     is the same campaign frame was not traced)."
   - Under "## GitHub Issue" add `- **Review record (1.5.x line, #748):** [rca-stuck-battle-guard-2026-10-06.md](../reviews/rca-stuck-battle-guard-2026-10-06.md)`.
2. `docs/reference/provenance-register.md` (HEAD side kept in B1):
   - Add the row printed by `git show a913629cd:docs/reference/provenance-register.md | grep "^| Stuck Battle Guard"`
     as the last table row, immediately above `<!-- provenance-register-end -->`.
   - Insert the section printed by
     `git show a913629cd:docs/reference/provenance-register.md | sed -n '/^### Stuck Battle Guard (UNCLEARED)/,/^### Kingdom Borders (UNCLEARED)/p'`
     (drop its last line, the Kingdom Borders heading) immediately before the line `## Adding a row`.
     Then make three edits inside it (a phrase may span a line break): "(re-verified against v1.5.4, written up
     in" becomes "(re-verified against v1.5.4 on the 1.5.x line and against v1.4.8 for the 1.4.x port, written
     up in"; delete " (also vanilla's `DestroyPartyAction` write)"; "the detach is vanilla's single write per
     party and spares quest parties as vanilla does" becomes "the detach is a single write per party and spares
     quest parties".

**Verify**: Docs shows no new finding in these files (the anchors `#stuck-ai-battles` and
`#stuck-battle-guard-uncleared` resolve); Doc graph no regression; the dash scan from A7
(`git diff HEAD -- docs ...`) prints 0.

### Step B6: Verify Part B

Build; full Test (0 failed; total = Part A total + 39).

### Step B7: Commit Part B

Stage: `git add Main/Adapters/IStuckBattleAdapter.cs Main/Adapters/IStuckBattleEventAdapter.cs Main/Adapters/StuckBattleAdapter.cs Main/Adapters/StuckBattleEventAdapter.cs Main/Features/MapEventGuard Main/Composition/FeatureModules.cs TAOM.Tests/Features/MapEventGuard docs/features/map-event-guard.md docs/features/coop-interop.md docs/features/dev-console.md docs/reference/doc-lookup.md docs/reference/provenance-register.md docs/reviews/rca-stuck-battle-guard-2026-10-06.md`
**Verify**: `git diff --cached --name-only | wc -l` prints 20.

Message:
```
fix(battles): v2.0.28 - end AI battles stuck at N vs 0 (#748)

An AI battle on the campaign map could freeze for good, for example
at 8000 against 0. Lords of the same faction kept joining it, the
whole faction stood still, and the player could join neither side. A
player traced the cause and sent a workaround module, Stuck Battle
Guard; this fix is written from their diagnosis.

The engine runs a battle round only while both sides have healthy
troops, and picks a winner only inside a round, so a side that drops
to 0 healthy troops between rounds never loses. A destroyed party
still attached also makes the battle refuse every joiner.

Once per in-game hour TAOM now checks every AI battle at least 12
in-game hours old. A destroyed party still attached is removed (a
quest's party is left alone). A side with no healthy troops loses:
the other side wins, with the usual prisoners and loot. When both
sides have none, the defenders hold. The player's own battle, an
enlisted player's commander's battle and raids are never touched,
and the check stands down in a co-op session. The console command
taom.print_stuck_battles lists every running battle with what the
check would do.

Known limitation: while the player is enlisted, a stuck battle
holding their commander is not resolved.

Research: taom-src MapEvent.Update (MapEvent.cs:995-1046),
  SetOverrideWinner (:1058), BattleState setter (:284-301),
  CheckIfOneSideHasLost (:1275-1290), CanPartyJoinBattle
  (:1447-1454), MapEventSide.RecalculateMemberCountOfSide (:319),
  PartyBase.MapEventSide (:289-322), MbEvent listener order
Not-tested: the hourly sweep on a live save
Ported-from: a913629cdb33b2b30e6aaf875146447abe1597bb
```

**Part B done criteria**: the commit exists with that subject; `git show --stat HEAD | tail -1` reports
20 files; `grep -c "StuckBattleModule()" Main/Composition/FeatureModules.cs` prints 1; full suite 0 failed;
Docs and Doc graph show nothing new; `git status --porcelain` empty.

---

## Part C: mission behaviour setup runs (#606), after the Custom Battle guards (#699)

Two commits, in this order. The guards (C1) must land before the move (C2): without them every Custom
Battle siege assault throws out of `Mission.AfterStart` once Siege Dismount starts running.

Simulated this session onto `1b00d1acd` (all exit 1): `2a5363e90` conflicts in `CHANGELOG.md`, the four
SignatureStrikes files, `docs/features/signature-strikes.md` and its RCA; `fac17e05c` in `CHANGELOG.md` and
`TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs` (absent here); `cfca3c5fd` in 22 RealmBorders,
settings and doc paths; `9cb6d49e7` in `docs/features/siege-dismount.md`; `bb85e9284` in
`.claude/rules/harmony-patches.md`, `PlayerMountAdapter.cs`, `SiegeDismountAdaptersNoCampaignTests.cs`,
`siege-dismount.md` and the lessons file. Every conflict is either OUT content or a series-ordering
artefact, so Part C takes each in-scope file's end state with `git checkout <sha> <path>` (exact because
the pre-images match, see "Current state") and hand-writes only the rule text, the doc rewording and the
`smart-cavalry-ai.md` block (WP110 edits that doc).

### Step C0: Precondition

`git diff --stat 1b00d1acd..HEAD -- Main/Adapters/PlayerMountAdapter.cs Main/Adapters/PartyMountInventoryAdapter.cs Main/Features/SiegeDismount Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs docs/features/siege-dismount.md docs/features/mixed-formations.md`
**Verify**: prints nothing. Any change by an earlier WP is a STOP (the transplant would overwrite it).

### Commit C1: Siege Dismount guards (`cfca3c5fd` adapter part, `bb85e9284` code)

**Step C1.1 (RED)**: `git checkout bb85e9284 TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs`
(adds one test). Build; filtered `FullyQualifiedName~SiegeDismountServiceTests`.
**Verify**: exactly `OnMissionStart_HasMountThrows_LogsErrorAndDoesNotPropagate` fails (the exception escapes
`OnMissionStart`).

**Step C1.2 (GREEN)**: `git checkout bb85e9284 Main/Features/SiegeDismount/SiegeDismountService.cs`
(moves the `HasMount()` check inside the `try`). Build; same filter.
**Verify**: all pass (26 methods, 34 cases).

**Step C1.3 (RED)**: `git checkout bb85e9284 TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs`. Build.
**Verify**: the build fails with CS0117 (`PlayerMountAdapter` has no `HeroBattleEquipmentOf`).

**Step C1.4 (GREEN)**: `git checkout bb85e9284 Main/Adapters/PlayerMountAdapter.cs Main/Adapters/PartyMountInventoryAdapter.cs`.
Build; filtered `FullyQualifiedName~SiegeDismountAdaptersNoCampaignTests|FullyQualifiedName~SiegeDismount`.
**Verify**: build exit 0; all pass (7 adapter tests). `git grep -n -E "Hero\.MainHero|MobileParty\.MainParty" -- Main/Adapters/PlayerMountAdapter.cs Main/Adapters/PartyMountInventoryAdapter.cs`
prints only comment or doc-comment lines.

**Step C1.5 (rule)**: in `.claude/rules/adapters.md`, insert after the computed-getter paragraph (the one
starting `**A computed getter throws BEFORE your`) and before `## Testing`, with one blank line each side:
```
**The campaign statics are computed getters too.** `Hero.MainHero` (`CharacterObject.PlayerCharacter.HeroObject`) and `MobileParty.MainParty` (`Campaign.Current.MainParty`) throw with no campaign, which is every Custom Battle, so `Hero.MainHero?.` guards nothing there. Code a mission can reach reads `CharacterObject.PlayerCharacter?.HeroObject` (or `(Game.Current?.PlayerTroop as CharacterObject)?.HeroObject`, which also survives no Game) and `Campaign.Current?.MainParty`, or returns on `Campaign.Current == null` first (#97, #699).
```
(This is `bb85e9284`'s paragraph verbatim; its getters are confirmed on v1.4.8: Hero.cs:892, CharacterObject.cs:250, MobileParty.cs:256.)

**Step C1.6 (verify and commit)**: build; full Test (0 failed). Stage
`git add Main/Adapters/PlayerMountAdapter.cs Main/Adapters/PartyMountInventoryAdapter.cs Main/Features/SiegeDismount/SiegeDismountService.cs TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs .claude/rules/adapters.md`
(`git diff --cached --name-only | wc -l` prints 6). Message:
```
fix(siege-dismount): v2.0.28 - guard adapters outside a campaign (#699)

Siege Dismount read Hero.MainHero and MobileParty.MainParty, whose
getters throw when there is no campaign, which is every Custom
Battle. Its two adapters now read each step guarded and report no
mount outside a campaign, and the service's first adapter call sits
inside the same try as the others, so an adapter fault logs an error
instead of escaping mission start.

Nothing changes for players yet: the feature's start still sits in
OnBehaviorInitialize, which v1.4.8 never sends to a behaviour TAOM
adds. The next commit moves it to AfterStart; these guards land
first so that a Custom Battle siege cannot throw out of mission
start once it does.

Research: taom-src Hero.MainHero (Hero.cs:892),
  CharacterObject.PlayerCharacter (CharacterObject.cs:250),
  MobileParty.MainParty (MobileParty.cs:256), Game.PlayerTroop
  (Game.cs:54)
Not-tested: nothing beyond the unit tests; the code is unreachable
  until the next commit
Ported-from: cfca3c5fd24a127ed367527dfa8dcd1147f53770
Ported-from: bb85e92849a99252dfe0bc9bc1bfed3670c86734
```

### Commit C2: the three siblings leave `OnBehaviorInitialize` (`2a5363e90` ratchet and rule, `fac17e05c`, docs from `9cb6d49e7` and `bb85e9284`)

**Step C2.1 (RED)**: `git checkout fac17e05c TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs`
(the ratchet in its post-`fac17e05c` shape: `KnownOverriders` holds only `CustomBattleTeamFixBehavior`).
Re-cite its comments: "(v1.5.3 Mission.cs:3827)" becomes "(v1.4.8 Mission.cs:3811)", "(:3831)" becomes
"(:3815)", "(:4699)" becomes "(:4628)", "list when :3827 runs" becomes "list when :3811 runs". Build;
filtered `FullyQualifiedName~MissionBehaviorLifecycleTests`.
**Verify**: the test FAILS (not Inconclusive) and its message contains `Declared: [CustomBattleTeamFixBehavior,
MixedFormationsMissionBehavior, SiegeDismountMissionBehavior, SmartCavalryAIMissionBehavior]`. Inconclusive
("Game assemblies not loaded") or any other name in the list is a STOP.

**Step C2.2 (GREEN)**: `git checkout fac17e05c Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs`.
In `SiegeDismountMissionBehavior.cs` change ":3827 vs :3831" to ":3811 vs :3815, v1.4.8". Build; filtered
`FullyQualifiedName~MissionBehaviorLifecycleTests|FullyQualifiedName~SiegeDismount|FullyQualifiedName~SmartCavalryAI|FullyQualifiedName~MixedFormations`.
**Verify**: all pass; `git grep -n "override void OnBehaviorInitialize" -- Main` prints only
`CustomBattleTeamFixBehavior.cs`.

**Step C2.3 (docs)**:
1. `git checkout fac17e05c docs/features/mixed-formations.md`; its new changelog line "- 2026-09-16 (#606):"
   becomes "- <today, YYYY-MM-DD> (#606, ported from the 1.5.x fix of 2026-09-16):". Do NOT check out
   `docs/features/smart-cavalry-ai.md` (it would revert WP110's rename). In that file insert this block
   immediately before the line `<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->`,
   followed by one blank line:
   ```
   ## Changelog

   - <today, YYYY-MM-DD> (#606, ported from the 1.5.x fix of 2026-09-16): the per-mission MCM log moved from
     `OnBehaviorInitialize` (which the engine never dispatches to a behavior added from
     `SubModule.OnMissionBehaviorInitialize`) to `AfterStart`. Diagnostic only; the tick's live
     `IsFieldBattle` read never depended on it.
   ```
   **Verify**: `git diff HEAD --numstat -- docs/features/smart-cavalry-ai.md` shows 0 deleted lines.
2. `git checkout bb85e9284 docs/features/siege-dismount.md docs/reviews/rca-siege-dismount-custom-battle-2026-09-30.md`
   (the doc's end state after `fac17e05c`, `9cb6d49e7` and `bb85e9284`; the RCA verbatim as the 1.5.x
   record). Then in `siege-dismount.md`:
   - "(`Mission.AfterStart`, v1.5.3 `Mission.cs:3827` then `:3831`)" becomes "(`Mission.AfterStart`, v1.4.8
     `Mission.cs:3811` then `:3815`)".
   - In "**Campaign only (2026-09-30).**", replace everything from "Until #699, every Custom Battle siege
     assault" to the end of that paragraph with: "On the 1.5.x line the move to `AfterStart` (#606) shipped
     before these guards, and every Custom Battle siege assault threw `NullReferenceException` out of
     `Mission.AfterStart` and reloaded forever (#699). On the 1.4.x line the guards landed first, in the same
     work package, so that loop never shipped here. On v1.4.8 such a throw is swallowed by SaveShield's
     MISSION-INIT finalizer on `MissionState.FinishMissionLoading`, and because `FinishMissionLoading` clears
     `_missionInitializing` (MissionState.cs:335) before `AfterStart` (:345), `TickLoading` loads the mission
     again (:221-233); MissionStartGuard (Patch103) now catches a throw per call. `HasMount()` sits inside the
     service's `try`, like every other adapter call, so an adapter exception logs an error instead."
   - In the Custom Battle verification paragraph, "Confirmed on Edoras, 2026-09-30 (`taom_debug_2026-09-30_13-13-05.log`)."
     becomes "Confirmed on the 1.5.x line (Edoras, 2026-09-30); owed on the 1.4.x line."
   - Replace the two newest changelog entries (the "2026-09-30 (#699)" and "2026-09-16 (#606)" items) with:
     "- <today, YYYY-MM-DD> (#606, #699, ported from the 1.5.x line): `OnMissionStart` moved from
     `OnBehaviorInitialize`, which v1.4.8 never sends to a TAOM-added behavior, to `AfterStart`, so the
     feature runs for the first time on this line. The adapters read the campaign statics guarded and
     `HasMount()` moved inside the service's `try` first, so a Custom Battle siege stands aside instead of
     throwing. Regression tests `SiegeDismountAdaptersNoCampaignTests` and
     `OnMissionStart_HasMountThrows_LogsErrorAndDoesNotPropagate`; review record
     [rca-siege-dismount-custom-battle-2026-09-30.md](../reviews/rca-siege-dismount-custom-battle-2026-09-30.md).
     In game, a mounted campaign siege and a Custom Battle siege assault are owed."
   - "## GitHub Issue" Status line becomes "- **Status:** Shipped on the 1.5.x line; ported to the 1.4.x line
     in WP111. Owed in game on 1.4.x: a mounted campaign siege (dismount and remount) and a Custom Battle
     siege assault."

**Step C2.4 (rule)**: in `.claude/rules/harmony-patches.md`, insert this block immediately before the line
`## Which thread runs your target (MANDATORY before the first line of a patch)` (if an earlier WP renamed
that heading, insert it directly after the `## Latches & Toggle Gates` section), with one blank line each side:
```
## MissionBehavior lifecycle for a behavior TAOM adds (MANDATORY before overriding a lifecycle virtual)

Every TAOM mission behavior is added from `SubModule.OnMissionBehaviorInitialize` (`AddTaomBehavior`, or a
feature module's `MissionBehaviors` through the composition root). On the installed v1.4.8,
`Mission.AfterStart` runs `OnBehaviorInitialize` over the behaviors ALREADY in the list (`Mission.cs:3811`)
and only then calls the submodules that add TAOM's (`:3815`); `AddMissionBehavior` calls only `OnCreated`
(`:4628`). So for a TAOM behavior:

| Virtual | Fires? | Use it for |
|---|---|---|
| `OnCreated` | yes, from `AddMissionBehavior` itself | per-mission state reset; `Mission` is already set |
| `OnBehaviorInitialize` | **never** | nothing (#606; `MissionBehaviorLifecycleTests` fails a new override) |
| `EarlyStart`, `AfterStart` | yes (`:3819`, `:3825`, after the add) | setup that needs the initialized mission; agents are not spawned yet |
| a lazy first-use gate in `OnAgentBuild` / `OnMissionTick` | yes | a mission gate read once the mission runs |

A behavior added from a postfix on the mission-opening call (before `AfterStart`, the CustomBattles shape)
IS in the list at `:3811` and does get the callback. The general rule (`lessons/state-lifecycle-save.md`,
"An engine lifecycle virtual's firing set is read from its caller, not its name"): open the caller of any
lifecycle virtual before wiring it, and quote the line in the override's comment.

`AddTaomBehavior` adds a behavior to every mission, Custom Battle included, where there is no campaign: its
`EarlyStart`/`AfterStart` reads no campaign static unguarded (adapters.md), keeps every adapter call inside a
`try` (on v1.4.8 an exception leaving `AfterStart` before `CurrentState = Continuing` makes `MissionState`
load the mission again: `FinishMissionLoading` clears `_missionInitializing` at MissionState.cs:335 before
`AfterStart` at :345, and `TickLoading` reloads at :221-233; #699), and a change that makes such code run
for the first time gets one Custom Battle run.
```

**Step C2.5 (verify)**: build; full Test (0 failed); also
`dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter TestCategory=BindingVerification`
(0 failed; the lifecycle test is not Inconclusive); Docs (no new finding in the touched docs; the RCA link
resolves); Doc graph (no regression); the dash scan from A7 (`git diff HEAD -- docs ...`) prints 0.

**Step C2.6 (commit)**: stage
`git add TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs docs/features/siege-dismount.md docs/features/mixed-formations.md docs/features/smart-cavalry-ai.md docs/reviews/rca-siege-dismount-custom-battle-2026-09-30.md .claude/rules/harmony-patches.md`
(`git diff --cached --name-only | wc -l` prints 9). Message:
```
fix(mission): v2.0.28 - siblings leave dead OnBehaviorInitialize (#606)

On v1.4.8 Mission.AfterStart sends OnBehaviorInitialize to the
behaviours already in the list (Mission.cs:3811) before it asks each
submodule to add its own (:3815), and AddMissionBehavior calls only
OnCreated (:4628). TAOM adds every behaviour in that second step, so
an OnBehaviorInitialize override on one never runs.

Three did their setup there. Smart Cavalry AI and Mixed Formations
only logged their settings; those lines now appear once per battle.
Siege Dismount never ran at all on this line. Its start now runs
from AfterStart, after every EarlyStart (where the siege flag is
set) and before the first agent spawns, so in a campaign siege your
horse and harness go to the inventory before you spawn and come back
after the siege, as the Siege Dismount options describe (on by
default, mode 3, Auto Remount).

MissionBehaviorLifecycleTests now fails any new TAOM override of
OnBehaviorInitialize. The one allowed override is
CustomBattleTeamFixBehavior, which a Custom Battle postfix adds
before AfterStart runs. The Harmony rule for Hooks/** states the
firing set.

Research: taom-src Mission.AfterStart (Mission.cs:3799-3836),
  AddMissionBehavior (:4615-4628), IsSiegeBattle (:1375),
  MissionCombatantsLogic.EarlyStart (:92-94); v1.4.8 dump
  DefaultBattleMissionAgentSpawnLogic.AfterStart (:158) and
  OnMissionTick (:171)
Not-tested: a mounted campaign siege (dismount and remount) and a
  Custom Battle siege assault in game
Ported-from: 2a5363e90981d3fe9c7cd241f8163f2c2beb0dde
Ported-from: fac17e05c5203abe38b1f2f0d33bd0847247b518
Ported-from: 9cb6d49e77168f04006330244599a29df321e519
Ported-from: bb85e92849a99252dfe0bc9bc1bfed3670c86734
```

**Part C done criteria**: two commits with those subjects, in that order; `git show --stat HEAD~1 | tail -1`
reports 6 files and `git show --stat HEAD | tail -1` 9 files; `git grep -n "override void OnBehaviorInitialize" -- Main`
prints one line (`CustomBattleTeamFixBehavior.cs`); full suite and the BindingVerification filter 0 failed;
Docs and Doc graph show nothing new; `git status --porcelain` empty.

---

### Step D1: Hand-off report (no commit, no README edit)

`plans/README.md` belongs to the integrator, who records the result after the merge. Put these in your final
report: the status of Parts A, B and C; the four commit hashes and subjects
(`git log --format='%h %s' bannerlord-1.4.5..HEAD`); the final Total/Passed/Failed/Skipped against the Step 0
baseline; any deviation you took (for example a GroupOrder other than 55); and the In-game checks owed. The
row for the integrator to update is WP111's own; no older plan's row is affected.

**Verify**: `git diff --name-only bannerlord-1.4.5...HEAD -- plans/README.md` prints nothing.

## Disposition table

| 1.5.x commit | Content | Disposition |
|---|---|---|
| `2a5363e90` fix(combat): signature strikes gated in a dead callback (#606) | `TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs` | Adapted: created in its post-`fac17e05c` shape, v1.4.8 lines (C2.1) |
| | `.claude/rules/harmony-patches.md` lifecycle section | Adapted: re-cited to v1.4.8 (C2.4; procedure step 2 keeps rule lines for the mechanism ported) |
| | `.claude/skills/deep-review/SKILL.md` line | Deferred to WP138 (the 1.5.x tip carries it in `lenses/2-engine-compat.md:19`, which WP138 ports) |
| | SignatureStrikes code (4 files), `signature-strikes.md`, its RCA | Skipped: SignatureStrikes is a 1.5.x-only feature |
| | `docs/reviews/lessons/state-lifecycle-save.md`, `CHANGELOG.md` | Skipped: bookkeeping (WP142) and D1 |
| `fac17e05c` fix(mission): siblings leave the dead OnBehaviorInitialize (#606) | three behaviours | Transplanted (pre-image equal), one comment re-cited (C2.2) |
| | lifecycle test allowlist shrink | Ported (C2.1) |
| | `mixed-formations.md`, `smart-cavalry-ai.md`, `siege-dismount.md` notes | Transplanted and reworded; the `smart-cavalry-ai.md` block inserted by hand because WP110 edits that doc (C2.3) |
| | `CHANGELOG.md` | Skipped (D1) |
| `cfca3c5fd` Add realm fill functionality and associated tests | `PlayerMountAdapter.cs`, `PartyMountInventoryAdapter.cs` | Transplanted through the `bb85e9284` end state (C1.4) |
| | `SiegeDismountAdaptersNoCampaignTests.cs` | Superseded by `bb85e9284`'s rewrite, transplanted (C1.3) |
| | RealmBorders and realm fill (adapters, feature files, palette, settings, coop and fingerprint lines, tests, docs, provenance rows, RCA) | Skipped: new Realm Borders feature content (scope rules) |
| | adapters lesson line | Skipped: bookkeeping (WP142) |
| `9cb6d49e7` fix(siege-dismount): prevent exceptions in Custom Battle sieges | `siege-dismount.md` | Folded into the `bb85e9284` doc end state, reworded for 1.4.x (C2.3) |
| `bb85e9284` fix(siege-dismount): guard adapter calls to prevent NRE | `SiegeDismountService.cs`, `HeroBattleEquipmentOf`, tests | Transplanted (C1.1 to C1.4) |
| | `.claude/rules/adapters.md` paragraph | Ported verbatim (C1.5) |
| | `.claude/rules/harmony-patches.md` paragraph | Adapted: v1.4.8 MissionState lines (C2.4) |
| | `siege-dismount.md`, `rca-siege-dismount-custom-battle-2026-09-30.md` | Transplanted; doc reworded, RCA verbatim and linked (C2.3) |
| | lessons line | Skipped: bookkeeping (WP142) |
| `c17541f2c` fix(battle-corpses): fade and cap battle corpses (#701) | `Main/Features/BattleCorpses/**` including `BattleCorpsesModule.cs` | Ported; D2 overrides the classifier note to hand-wire (A1) |
| | `IGraphicsOptionsAdapter.cs`, `GraphicsOptionsAdapter.cs` | Ported in the `c17541f2c` shape, one comment re-cited (A1, A6); WP117 extends |
| | `FeatureModules.cs` line | Adapted: appended (A3) |
| | `CoopSettingsRelevance.cs`, `MountDespawnMissionGate.cs` comment, `TaomSettings.cs` | Ported; group comment adapted (A1, A6) |
| | `taom_module_strings.xml` rows | Adapted: appended; plus 12 English language seeds, new work (A5) |
| | five test classes | Ported (A1) |
| | `SettingsFingerprintTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md` | Adapted: live counts +5 total, +3 relevant (A4) |
| | `battle-corpses.md` | Ported; v1.4.8 native facts added (A6) |
| | `mount-despawn.md`, `rca-battle-corpses-2026-10-01.md` | Ported; RCA linked from the feature doc (A1, A6) |
| | `docs/INDEX.md`, `feature-map.md`, lessons `build-tooling-workflow.md`, `localization-ui.md` | Skipped: bookkeeping (WP142) |
| `a913629cd` fix(battles): end AI battles stuck at N vs 0 (#748) | 4 adapters, 6 MapEventGuard files including `StuckBattleModule.cs` | Ported (D2); comments re-cited (B1, B4) |
| | `FeatureModules.cs` line | Adapted: appended (B3) |
| | three test classes | Ported; one summary re-cited (B1, B4) |
| | `map-event-guard.md` | Ported; re-cited to v1.4.8 (B5) |
| | `coop-interop.md` row, `dev-console.md` row, `doc-lookup.md` row | Ported (B1) |
| | `provenance-register.md` row and section | Adapted: hand-merged, three phrase edits (B5) |
| | `rca-stuck-battle-guard-2026-10-06.md` | Ported verbatim, linked (B1, B5) |
| | `docs/INDEX.md`, `feature-map.md`, lessons `adapters-taleworlds-api.md`, `campaign-mechanics.md`, `misc.md` | Skipped: bookkeeping (WP142) |
| Finding LOADING-HANG-07 | corpse and ragdoll cost | Closed by Part A |
| Finding FREEZE-THREADING-08 | stuck map battles | Closed by Part B; trigger #750 already fixed by `1b00d1acd` |

## Test plan

- **New, ported**: `TAOM.Tests/Features/BattleCorpses/{BattleCorpsePolicyTests,BattleSettingsAdvisorTests,
  BattleCorpseSettingsProviderTests,GraphicsOptionsAdapterTests,BattleCorpsesWiringTests}.cs` (41 methods,
  75 cases); `TAOM.Tests/Features/MapEventGuard/{StuckBattleServiceTests,StuckBattleSweepTests,
  StuckBattleWiringTests}.cs` (39); `TAOM.Tests/Adapters/SiegeDismountAdaptersNoCampaignTests.cs` (7);
  one new case in `SiegeDismountServiceTests`; `TAOM.Tests/Migration/MissionBehaviorLifecycleTests.cs` (1,
  BindingVerification).
- **Existing gates that change or must stay green**: `SettingsFingerprintTests` (counts updated),
  `UnregisteredLocalizationKeyBaselineTests`, `LanguageFileCoverageTests`, `ConsoleCommandBindingTests`
  (the new `taom.print_stuck_battles`), `FeatureModulesTests` (WP105 double-wiring guards),
  `SiegeDismountWiringTests`, `SettingRequireRestartPostureTests`.
- **RED evidence per step**: A2 (module unlisted, counts, unregistered keys), A5 (language rows), B2
  (module unlisted), C1.1 (exception escapes), C1.3 (missing seam), C2.1 (four overriders).
- **Structurally untestable** (for `Not-tested:`): the native effect of the two setters in a live battle,
  the main-menu inquiry, the per-event stuck-battle adapter (`MapEvent`'s constructor is internal), and the
  AfterStart callbacks firing in a real mission.
- Expected full-suite growth: +75 (A), +39 (B), +9 (C), or +41/+39/+9 if data rows count once.

## Done criteria (all must hold at the end)

- [ ] Four commits on `port148/111-battle-freeze-mitigations` (A, B, C1, C2), each subject
      `^[a-z]+\([a-z0-9-]+\): v2\.0\.28 - ` and at most 72 characters; no commit touches `CHANGELOG.md`,
      `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `docs/INDEX.md`, `feature-map.md`, `docs/reviews/lessons/` or `plans/README.md`
      (`git diff --name-only bannerlord-1.4.5...HEAD | grep -E "CHANGELOG|IoC.cs|SubModule.cs|TAOM.csproj|INDEX.md|feature-map|reviews/lessons|plans/README"` prints nothing; three dots, so
      only this branch's own changes count even if `bannerlord-1.4.5` moved on meanwhile).
- [ ] Build exit 0; full suite 0 failed; BindingVerification filter 0 failed with the lifecycle test passing.
- [ ] `grep -cE "BattleCorpsesModule\(\)|StuckBattleModule\(\)" Main/Composition/FeatureModules.cs` prints 2.
- [ ] `git grep -n "override void OnBehaviorInitialize" -- Main` prints only `CustomBattleTeamFixBehavior.cs`.
- [ ] Six `taom_bc_advice_*` rows in `taom_module_strings.xml` and in each of the 12 `std_taom_module_strings_*.xml`.
- [ ] `python tools/validate_moduledata.py`, `python tools/lint_docs.py`, `python tools/check_doc_graph_ratchet.py`
      show nothing new against the Step 0 runs.
- [ ] No em or en dash in any added doc line (`git diff bannerlord-1.4.5...HEAD -- docs | grep "^+" | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'` prints 0).
- [ ] `git status --porcelain` empty; nothing pushed (`git branch -r --contains HEAD` prints nothing).

## STOP conditions

Stop and report (do not improvise) if:

1. A Step 0 dependency check fails: WP105's composition files or its six `FeatureModuleHooks.` calls, WP104's
   `RepoPaths.ReadSource`, or WP109's `MissionStartGuardCalls.cs` is missing.
2. The preflight full suite has any failure.
3. The native DLL is not 14,185,944 bytes with SHA-256 `2a5e0e0b...6e12a`, Version.xml is not `v1.4.8`, or a
   Step 0 taom-src line differs: the engine moved, the native table in A6 is stale; recommend `/engine-bump`.
4. A cherry-pick conflicts in a C# path not named in this plan and the fix is more than "keep HEAD and add the
   1.5.x lines", or `FeatureModules.cs` no longer has an `All` array to append to.
5. A RED step fails for a reason other than the one stated, or a GREEN step leaves a failure.
6. The A4 count deltas are not +5 total and +3 simulation-relevant (classification differs from 1.5.x).
7. The A5 dry run lists any id besides the six `taom_bc_advice_*` ids.
8. C0 shows any change to a Part C file since `1b00d1acd`.
9. `MissionBehaviorLifecycleTests` reports Inconclusive, or its RED list holds any name besides the four expected.
10. Any module would need `OwnsSaveData = true` (D2), or wiring seems to need `IoC.cs`, `SubModule.cs`,
    `TAOM.csproj`, `Directory.Build.props` or `.claude/settings.json`.
11. A hook blocks a commit (subject, CHANGELOG, ModuleData validation, tracked-files gate). Never bypass.
12. D20 is not satisfied (no recorded WP101 laptop Armory update and validator baseline) before the Part A commit.
13. Any verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer; v1.4.8, the TAOM debug log `taom_debug_*.log`)

Part A, battle corpses:
1. Custom Battle at the largest size with Number of Ragdolls and Number of Corpses on Unlimited: one
   `[BattleSettings] ragdollOption=5 corpseOption=5 battleSizeOption=<n> taomOverride=on fadeSeconds=60 corpseCap=25`
   line once deployment ends; bodies fade about 60 s after they settle; never more than 25 remain.
2. Turn "Clean Up Battle Corpses" off mid-battle: a second `[BattleSettings] ... taomOverride=off` line.
3. A town fight, an arena and a hideout: no `[BattleSettings]` line, bodies stay.
4. First main menu with high options: one inquiry naming the options as the Options screen does. Back up
   `engine_config.txt` and `BannerlordConfig.txt`, click Apply: `number_of_ragdolls` and `NumberOfCorpses`
   change, Options, Performance shows 5 and Low (25), and a `[BattleCorpses] recommended ragdoll and corpse
   options saved` line. "Keep mine" changes nothing; with "Recommend Battle Settings" off there is no inquiry
   after a restart. Ragdolls at 1 stay at 1 after Apply.
5. After WP117 lands: watch `[MissionPerf]` agents against active as bodies fade (does a fade free an agent
   slot?), and a burst test (cap 250, fade 300, then fade 10, then cleanup off and on) read beside `[MissionStall]`.

Part B, stuck battles:
6. On a long save with cheat mode, `taom.print_stuck_battles` lists every map event with healthy counts,
   age and verdict. Let one in-game hour pass: a battle 12 or more hours old with a side at 0 gets one
   `[StuckBattle] <label>: ...` line (attackers win, defenders win or hold, or wrecked parties removed), and
   it is gone from the next print. The player's own battle and an enlisted player's commander's battle are
   never named in a `[StuckBattle]` line.

Part C, mission setup:
7. Any field battle: one `[SmartCavalryAI] mission init` (or `disabled via MCM`) line and one
   `[MixedFormations] mission init` (or `disabled via MCM`) line per battle.
8. Campaign siege assault as a mounted player (Siege Dismount on, mode 3): a `[SiegeDismount] siege detected`
   line naming `behavior=AutoRemountAfter`, the player spawns on foot, mount and harness are in the inventory;
   after the siege, `[SiegeDismount] mount restored after siege` and both are back in their slots.
9. Custom Battle siege assault: after `[SiegeDismount] siege detected`, the log reaches
   `[BattleLoad] ... phase=MissionAfterStartDone` and then `BattlePlayable` once; FAIL if
   `MissionAfterStartBegin` and `LoadMissionBegin` repeat (the #699 loop).
10. Custom Battle field battle: the two teams still fight each other (CustomBattleTeamFixBehavior unaffected).

## Maintenance notes

- **WP117** cherry-picks `6eb2e1b90` and `685c6a1e0` onto `IGraphicsOptionsAdapter`/`GraphicsOptionsAdapter`;
  this plan leaves both files in the `c17541f2c` shape except the line-11 comment, outside their hunks.
- **`MountDespawnMissionGate`** now gates two features (its summary says so). The 1.5.x commits `639df895b`
  and `06b1deaab` edit the same summary for LostFooting and FellFoes; a later port of either meets a comment
  conflict, nothing more.
- **WP142** syncs the bookkeeping this plan dropped: feature-map rows for BattleCorpses and StuckBattle,
  `docs/INDEX.md`, and the lessons entries (`state-lifecycle-save.md` #606, `adapters-taleworlds-api.md`
  #699 and #748, `build-tooling-workflow.md`, `localization-ui.md`, `campaign-mechanics.md`, `misc.md`).
  **WP143** reconciles `harmony-patches.md` and `adapters.md` with the 1.5.x tip; **WP138** brings the
  deep-review lifecycle line; **WP135** translates the six `taom_bc_advice_*` strings (paid; maintainer).
- **War of the Ring**: a swept battle ends through `OnBattleWon` like a natural win, so momentum and any
  `MapEventEnded` listener sees it.
- **For `/deep-review`** (recommended before merge; C# across many files): (1) no hand-wiring remains for
  either module; (2) the corpse cap never exceeds the player's option (the policy's table equals the v1.4.8
  jump table at 0x5D3BB4); (3) the stuck-battle sweep never detaches the main party
  (`MapEvent.RemoveInvolvedPartyInternal` nulls `TroopUpgradeTracker` only for it, the CLAUDE.md trap);
  (4) the enlisted-commander skip assumes the sweep's hourly listener runs before `EnlistmentBehavior`'s,
  which holds because `MbEvent` runs the last-registered listener first and WP105 adds module behaviours
  after the hand-wired ones; (5) Siege Dismount is live by default from Part C on; (6) the SmartCavalryAI and
  MixedFormations `AfterStart` overrides only log.
- **Deferred, not in this pack**: the 1.5.x doc upkeep commits listed under "Out of scope"; the NavigationCache
  load-loop lead (K1 showed it is server-only on v1.4.8).
