# Plan 123: Cut always-on costs: no auto-resolve battle log, cheaper mission diagnostics, MCM settings read once

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. The plan has four Parts (A to D). Each Part is
> landable on its own and ends with its own gate; run them in order on one branch. When the last
> Part is done, write the Step D4 hand-off. Do not edit `plans/README.md`: it belongs to the integrator.
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/AutoResolveDiagnostics Main/Adapters/MapEventBattleLogAdapter.cs Main/Features/TaomSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/Enlistment Main/Features/MissionDiagnostic Main/Features/BannerColorPersistence Main/Features/CompanionTactics Main/Features/BattleLoadDiagnostics Main/Features/BattleBalance Main/Features/CombatMechanics Main/Features/BlowDiagnostics Main/Features/DreadAura Main/Features/MixedFormations Main/Features/SiegePropDiagnostics Main/Features/SmartCavalryAI Main/Features/LocalizationOverride Main/Features/AlignmentDesertion Main/Features/CaravanTrade Main/Features/CastleRecruitment Main/Features/CulturalFeats Main/Features/CultureMarketplace Main/Features/FieldCommission Main/Features/PartyIconScale Main/Features/QuickActions Main/Features/TimeAcceleration Main/Features/Refuge Main/Features/SupplyLines Main/Features/FieldCamp Main/Features/CrashReport Main/Features/TroopWeight Main/Features/NameplateCull Main/Adapters/TownRosterAdapter.cs Main/IoC.cs Main/SubModule.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/NameplateCull docs/features/nameplate-cull.md TAOM.Tests/Composition`
> Earlier WPs of this programme change several of these files (listed under "What earlier WPs
> change"). That drift is expected: compare the live file with the excerpts below and STOP only
> on a real mismatch (an excerpt this plan edits is missing or reads differently).

## Status

- **Priority**: P1 for Part A (the auto-resolve log is the largest always-on writer in player logs); P2 for Parts B to D
- **Effort**: L (12 commits, four Parts)
- **Risk**: MED (many files; every change is meant to leave game results unchanged, and the tests pin that)
- **Depends on**: WP105 (`plans/105-composition-root.md`, which needs WP104), WP117 (frame and memory instruments) and WP120 (`plans/120-map-perf-and-vram.md`: `NameplateCullSettingsProvider` and the test class Step C2 folds), all merged into `bannerlord-1.4.5`
- **Category**: perf
- **Wave / lane**: 3 / A
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the changelog entry), D2 (every feature here is hand-wired on both lines, so no `*Module.cs`), D7 (13fb30f9c's co-op ServerSettings half stays out), D8 (subjects carry `v2.0.28`), D9 (no plan 010: drop `[TestCategory("RequiresGame")]` from ported tests), D10, D13 (bb68eeca3's lord-template XSLT data fix is not ported here; plan 134's disposition row for `bb68eeca3` item 3 decides it), D19 (git only through the Bash tool), D20 (this WP edits no ModuleData)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`, 2026-11-04 (1.5.x moved on to `ca1cba35f` during planning; every commit used here is an ancestor of `e45f6df2c`)
- **Issue**: tracking references from the 1.5.x commits: port of #430 (closed; the feature is removed), #840, #664 (plan 003), #712 (plan 030), #713 (plan 031), #719 (plan 037), #741, #745, #746, #982 and the client half of #968. `36d4dda14` carries no issue. New work (Step A4 date line, Step D3 measurement): the orchestrator creates the issue before implementation lands.

## Why this matters

The public 1.4.8 build pays several costs in every session that buy nothing for a player. In
player log E4 (`taom_debug_2026-11-01_20-16-56.log`, 35 minutes, 1,092,378 bytes) the
auto-resolve battle log wrote 225 records, about 421 KB or 39 percent of the file, and each record
is a synchronous `WriteLine` plus `Flush` on the campaign thread (`FileLogger.cs:79-130`); the
feature is on by default. Every mission walks every agent on every frame for its first 5 seconds
and calls a native action-set name marshal per agent, an empty Companion Tactics postfix runs on
every `Mission.OnTick`, and a banner colour store is written twice per spawned agent and never
read. Battle and campaign settings providers resolve `TaomSettings.Instance` on every read, and each
resolve is a dictionary check plus a search of MCM's settings containers (`GlobalSettings<T>.Instance`, read on MCMv5 5.12.1); 268 lines in `Main` mention
`TaomSettings.Instance` at `1b00d1acd`. The enlistment trace still
ships on, crash bundles list none of TAOM's own settings while reflecting over every third-party
assembly, and the session snapshot prints `<time read failed: DivideByZeroException>` in every
load (E3:56, E4:55). After this WP those costs are gone with game results unchanged. None of
these is shown to cause the reported freezes (verify reports V02 and V05); this is cost and
hygiene, not a freeze fix.

## Current state

### What earlier WPs change in these files (expect this drift)

- **WP104** converts every `_harmony.PatchCategory("X")` in `Main/SubModule.cs` to `TryPatchCategory("X")`
  and adds `TAOM.Tests/Infrastructure/RepoPaths.cs` (`RepoPaths.ReadSource(path, stripComments)`). It
  also rewrites `SharedMovementOrderPostfixTests.cs` to read through `RepoPaths`.
- **WP105** adds `Main/Composition/*` and `TAOM.Tests/Composition/FeatureModulesTests.cs`. Its kernel test
  anchors the mission-behavior hook after the string `new AgentColorStoreCleanupBehavior(colorStore)`;
  Step A3 deletes that behavior and must move the anchor (WP105 maintenance note, rule 8).
- **WP106** edits `CoopSettingsRelevance.cs`, `BattleLoadDiagnosticsSettings.cs`, `SettingsFingerprintTests.cs`,
  `coop-interop.md`, `bannerlord-together-compat.md` and adds a Patch91 registry section that names
  `Patch35_Mission_OnTick`.
- **WP110** renames 13 MCM settings (#1017). The ones this WP meets: `EnableSmartCavalryAI` to
  `EnableSmartCavalryCharge`; `EnableFormationPresets` to `EnableOobFormationPresets` (the TaomSettings
  property AND the `ICompanionTacticsSettingsProvider` member); `FieldCommissionMeritThreshold` to
  `FieldCommissionMeritToPromote`; `FieldCommissionMaxOffersPerBattle` to `FieldCommissionOffersPerBattle`.
  It edits `Patch35_Mission_OnTick.cs`, and adds `tools/check_mcm_default_flips.py` with
  `tools/mcm_default_flips_allowlist.json`, whose `TaomSettings.EnableEnlistmentDiagnostics` entry turns
  stale when Step A2 renames that setting (WP110 maintenance note: delete it in the same commit).
  WP110 is wave 1 but not in this plan's "Depends on": everywhere below, use whichever name the live
  `Main/Features/TaomSettings.cs` declares.
- **WP118** (Step D1) moves the `Bannerlord.MCM` pin in `Main/TAOM.csproj` from 5.12.1 to 5.12.3 and owns
  `docs/modding/module-dependencies.md`. It is not in this plan's "Depends on": Step A0 records the pin the base carries.
- **WP120** adds `Main/Features/NameplateCull/`, `TAOM.Tests/Features/NameplateCull/` (with `NameplateCullSettingsProviderTests.cs`,
  which Step C2 folds) and `docs/features/nameplate-cull.md`.
- **WP115** sets `FileLogger.DefaultRetainedLogs` to 30. **WP117** adds settings (counts move) and may add
  the opt-in `TAOM_RUN_BENCHMARKS` benchmark pattern. **WP122** may add test gates (patch-number gate,
  quality gates). **WP127** depends on this WP, so `Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` does not exist yet.

### Files and roles (excerpts at `1b00d1acd`)

Part A
- `Main/Features/AutoResolveDiagnostics/` (10 files), `Main/Adapters/IMapEventBattleLogAdapter.cs`,
  `MapEventBattleLogAdapter.cs`, `ITroopCensusAdapter.cs`, `TroopCensusAdapter.cs`,
  `TAOM.Tests/Features/AutoResolveDiagnostics/` (5 files, 56 test methods), `tools/analyze_battle_logs.py`:
  the battle log. `AutoResolveDiagnosticsSettingsProvider.cs:15` is `IsEnabled => TaomSettings.Instance?.LogAutoResolvedBattles ?? true;`.
  `AutoResolveDiagnosticsBehavior.cs:84` is `public override void SyncData(IDataStore dataStore) { }` (no save data;
  no saveable type definer names these types). `git grep` finds no consumer outside these files.
- `Main/Features/TaomSettings.cs:441-462`: the block from `// --- Battle Tactics / Auto-Resolve Diagnostics ---`
  to `public bool LogAutoResolveTroopCensus { get; set; } = false;` (`:457` is `LogAutoResolvedBattles { get; set; } = true;`).
  `:516-518`: `[SettingPropertyBool("Enable Enlistment Diagnostics", ...)]` and `public bool EnableEnlistmentDiagnostics { get; set; } = true;`.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs:55` `"LogAutoResolvedBattles", "LogAutoResolveTroopCensus",`;
  `:68` `"EnableEnlistmentDiagnostics", "EnableFieldCommissionDiagnostics",`.
- `Main/IoC.cs:161` `Features.AutoResolveDiagnostics.AutoResolveDiagnosticsIoC.RegisterAutoResolveDiagnosticsFeature(container);`
- `Main/SubModule.cs:1225-1227` (two comment lines, then `campaignStarter.AddBehavior(IoC.Resolve<Features.AutoResolveDiagnostics.AutoResolveDiagnosticsBehavior>());`);
  `:500-502` the colour store initialisation; `:1853` `AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());` (once in the file);
  `:1855-1857` the `AgentColorStoreCleanupBehavior` registration; `:706` the `LogSessionSnapshot()` call in `OnGameStart`;
  `:1309` `public override void OnGameInitializationFinished(Game game)`, whose body stamps
  `SaveLoadPhase.GameInitializationFinished` and then hits `if (_gameInitPatchesApplied) return;`.
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:210`
  `AssertSplit(typeof(TaomSettings), reflected: 229, covered: 180);` (the numbers will have moved by
  WP106/WP117); `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` requires `docs/features/coop-interop.md`
  (`:310-316`, "TAOM ships **243** settings", "the split is 229 in `TaomSettings`", "The 59 excluded") and
  `docs/features/bannerlord-together-compat.md:291` ("TAOM's 243 MCM settings") to quote the reflected total.
  Both auto-resolve settings are in the Instrumentation (excluded) set, so removing them moves the
  TaomSettings reflected count and the doc totals by 2 and leaves `covered` unchanged.
- `Main/Features/Enlistment/EnlistmentDiagnosticsSettingsProvider.cs:25` `internal static bool ResolveEnabled(bool? raw) => raw ?? true;`, `:27` reads `EnableEnlistmentDiagnostics`.
- `Main/Features/MissionDiagnostic/MissionDiagnosticService.cs:53-66`: the session snapshot calls
  `LogCampaignContext()` when `Campaign.Current.GameStarted`; `:74-79` formats it with
  `CampaignContextFormatter.Describe(ReadCampaignTime, ReadMainHero)` and `ReadCampaignTime() => $"now={CampaignTime.Now}"`.
  `CampaignContextFormatter.cs:22-41` turns a throwing reader into `<time read failed: X>`.
- `Main/Features/MissionDiagnostic/Hooks/MissionDiagnosticBehavior.cs:76-92`: per agent per frame for 5 s,
  `agent.ActionSet.GetName()`, `agent.Name`, a race-name lookup, then `LogActionSetSeen`, which builds a
  `"{name}|{race}|{female}"` key before its dedupe (`MissionDiagnosticService.cs:169-183`).
- `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs:15-36`: Postfix on
  `Mission.OnTick(float, float, bool, bool)`, category `Patch35_CompanionTactics`; body is a settings
  read and `return` ("Body intentionally empty for now").
- `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs:14,16,58-67` (the `_colorStore`
  field, the three-argument `Initialize`, and a `Postfix` whose only effect is `_colorStore?.Register`);
  `Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs:8-36` (a Prefix whose only effect is `_colorStore?.Register`);
  `IAgentColorStore.cs`, `AgentColorStore.cs` (`TryGetColors` has no production reader), `AgentColorStoreCleanupBehavior.cs`.
- `Main/Core/Logging/FileLogger.cs:79-82` `LogInfo`/`LogWarning`/`LogError` enqueue with `durable: true`
  and `:88` call `Drain()` on the caller; `:93-130` `Drain` writes every queued line and calls `Flush()` under
  `_writeLock`; `:132-139` the writer thread drains DEBUG, sleeping 50 ms when idle.

Part B
- `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs:5-17`: twelve getters, each `TaomSettings.Instance?.X ?? default`.
  `BattleBalanceIoC.cs` registers it `Reuse.Singleton`; `IoC.Configure` builds `new Container()` (DryIoc 4.8.8,
  `Main/TAOM.csproj:100`; `Bannerlord.MCM` 5.12.1 at `:99`, 5.12.3 once WP118 is merged).
- `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs:19-56`: `MasterEnabled` and 8 enabled getters,
  each reading `TaomSettings.Instance` twice; `CrushThroughMaxChance`; `ChargeAutoKnockdownWeightRatio` (clamp
  `SettingClamp.Clamp(mcm.Value, 8, neutralFloor, 30)`). The 1.4.x interface has exactly 10 members (8 bools, 2 floats);
  the 1.5.x file adds #610 knobs and `CultureChargeDamageEnabled` that 1.4.x does not have.
- Identical on 1.4.x and the 1.5.x parent of `74e9172d4` (so its hunks apply as written): `BlowDiagnosticsSettingsProvider`,
  `CreatureCombatService`, `CrushThroughService`, `Patch35_Formation_SetMovementOrder`, `CompanionTacticsSettingsProvider`,
  `DreadAuraSettingsProvider`, `MBTextManager_GetLocalizedText_Patch`, `MixedFormationsSettingsProvider`,
  `SiegePropDiagnosticsSettingsProvider`, `SmartCavalryAISettingsProvider` (before WP110's renames).

Part C
- Identical on 1.4.x and the 1.5.x parent of `423d948d8`: the AlignmentDesertion service, settings provider and
  interface, CaravanTrade, CastleRecruitment, FieldCommission (before WP110), QuickActions and TimeAcceleration providers,
  `CulturalFeatsService`, `ICulturalFeatsService`, `TaomPartySpeedModel`, `RefugeCampaignBehavior`.
- `Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs:26-43` (`EnsureGuaranteedStock`: one
  `_townAdapter.GetItemCount` per routed item) and `:51-84` (`FilterForeignCultureItems` rebuilds the routed id set
  per town per day). The 1.5.x parent also keeps items of the culture's own pool (`pooledHere`, added by
  `301cd338a`, the 1.5.x-only armour acquisition feature); 1.4.x does not, and must not after this WP.
- `Main/Features/Refuge/RefugeSettingsProvider.cs`, `SupplyLines/SupplyLinesSettingsProvider.cs`,
  `FieldCamp/CampSettingsProvider.cs`: identical on 1.4.x and the parent of `bb68eeca3`; every getter reads `TaomSettings.Instance`.
- `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs:58-68` `ApplyDamageReductions` reads
  `VictimPartyId(attackInformation.VictimAgentOrigin)`; `:70-79` `VictimPartyId` switches on `PartyAgentOrigin`
  and `PartyGroupAgentOrigin` only, so a mount (no origin of its own) never gets the refuge reduction (#741).
  The parent model already uses the mount branch: `TaomAgentApplyDamageModel` reads
  `info.IsVictimAgentMount ? info.VictimRiderAgentOrigin : info.VictimAgentOrigin` and `origin?.BattleCombatant as PartyBase`.

Part D
- `Main/Features/CrashReport/Collectors/McmSettingsCollector.cs:37` walks every loaded assembly; `:108-116`
  `ResolveInstance` uses `GetProperty("Instance", Public | Static)` without `FlattenHierarchy`, and none of
  `TaomSettings`, `CrashReportSettings`, `BlowDiagnosticsSettings`, `BattleLoadDiagnosticsSettings` declares
  its own `Instance` (git grep, exit 1), so bundles list no TAOM settings. The file equals the 1.5.x parent of
  `13fb30f9c`; only `13fb30f9c` and `a59a48bb6` touched it and its test file through `e45f6df2c`.
- `Main/Features/TroopWeight/Diagnostics/` holds `TroopCountDiagnosticsBehavior.cs` and its formatter;
  `Main/SubModule.cs:1431` applies `Patch17_TroopWeight` (as `TryPatchCategory` after WP104) and already has
  `using TAOM.Features.TroopWeight.Diagnostics;` (`:36`).

### Engine facts (installed v1.4.8, `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, this session)

- `Campaign.OnInitialize`: `base.GameManager.OnGameStart(...)` at `Campaign.cs:1391`, `CampaignTime.Initialize()` at
  `:1394`, `base.GameManager.OnGameInitializationFinished(base.CurrentGame)` at `:1452`. `SetLoadingParameters` sets
  `GameStarted = true` for a saved campaign (`:1357`); a new campaign sets it on its first `RealTick` (`:894-896`).
  `[SaveableProperty(100)] internal MapTimeTracker MapTimeTracker` (`:348-349`); `MapTimeTracker.Now => new CampaignTime(_numTicks)` (`MapTimeTracker.cs:18`).
- `CampaignTime`: `public static void Initialize()` (`CampaignTime.cs:178`) fills the tick constants from
  `Campaign.Current.Models.CampaignTimeModel`; `GetYear => (int)(_numTicks / TimeTicksPerYear)` (`:160`);
  `ToString()` reads `GetYear` first (`:357-359`). Before `Initialize`, `TimeTicksPerYear` is 0, hence the DivideByZero.
- `Agent.ActionSet => new MBActionSet(MBAPI.IMBAgent.GetActionSetNo(GetPtr()))` (`Agent.cs:696`); `Agent.Monster { get; }`
  (`:782`); `Agent.Character` (`:1392`). `public struct MBActionSet` (`MBActionSet.cs:7`), `GetHashCode() => Index`
  (`:31-33`), `GetName()` marshals through `MBAPI.IMBActionSet.GetNameWithIndex(Index)` (`:36-42`).
  `BasicCharacterObject.Race { get; set; }` int (`:76`), `IsFemale` virtual bool (`:78`).
- `public void Mission.OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)` (`Mission.cs:3636`);
  `public Agent Mission.SpawnAgent(AgentBuildData agentBuildData, bool spawnFromAgentVisuals = false)` (`:4086`);
  `public void Agent.EquipItemsFromSpawnEquipment(bool, bool, bool, int)` (`Agent.cs:4529`).
- `TroopRoster`: `internal PartyBase OwnerParty` (`:44`), `Count` (`:46`), `TotalRegulars` (`:51`),
  `public int AddToCountsAtIndex(int index, int countChange, int woundedCountChange = 0, int xpChange = 0, bool removeDepleted = true)` (`:312`),
  `GetElementCopyAtIndex(int)` (`:489`), `public void SetElementNumber(int index, int number)` (`:494`),
  `GetElementNumber(int)` returns 0 out of range (`:505-511`). `Campaign.CurrentTickCount` field (`Campaign.cs:137`), `MainParty` (`:514`).
- `public struct AttackInformation` (`AttackInformation.cs:7`): `IsVictimAgentMount` (`:71`), `VictimAgentOrigin` (`:91`),
  `VictimRiderAgentOrigin` (`:93`); set from `victimAgent.IsMount` (`:255`), `victimAgent.Origin` (`:280`) and
  `riderAgent.Origin` (`:286`). `IAgentOriginBase.BattleCombatant` (`IAgentOriginBase.cs:13`);
  `PartyAgentOrigin.BattleCombatant => Party` (`:48`), `PartyGroupAgentOrigin.BattleCombatant => Party` (`:31`),
  `SimpleAgentOrigin.BattleCombatant => Party`, the hero's `PartyBelongedTo.Party` or null (`:115-127`).
  `public sealed class PartyBase : IBattleCombatant` (`PartyBase.cs:21`), `MobileParty` (`:122`), `ItemRoster { get; private set; }` (`:135`).
- `ItemRoster`: `_data` (`:16`), `_count` (`:19`), `Count` (`:25`), `AddToCounts(ItemObject, int)` (`:185`),
  `AddToCounts(EquipmentElement, int)` (`:194`), `GetItemAtIndex` (`:232`), `GetElementNumber` (`:242`).
  `Settlement.Party { get; private set; }` (`:97`), `Settlement.ItemRoster => Party.ItemRoster` (`:316`).
  `MBObjectManager.Instance` (`:343`), `Init()` (`:363`), `Destroy()` (`:370`), `RegisterType<T>(...)` (`:376`),
  `RegisterObject<T>(T)` (`:446`), `GetObject<T>(string)` (`:573`). `ItemModifier()` (`:53`), `ItemObject(string stringId)` (`:291`).
- No howdah or mumak crew origin type exists on 1.4.x (`git grep HowdahCrewAgentOrigin` is empty).

### MCM facts (read on the shipped MCMv5 5.12.1, `C:\Decompiled_Bannerlord\_modules_build\TAOM.Dependencies__MCMv5.cs`)

These were read on 5.12.1 only. WP118 moves the pin to 5.12.3, and 5.12.3 has not been decompiled for this plan (the laptop's
NuGet cache holds 5.12.1). The 1.5.x line carries the same `CountingMcm.cs` while pinning 5.12.3 (`bb68eeca3` and `e45f6df2c`),
which is evidence, not a reading. Step B2's Verify carries the STOP for the version the base pins.

- `GlobalSettings<T>.Instance` (`:6319-6330`) adds the type to a cache on first use, then returns
  `BaseSettingsProvider.Instance?.GetSettings(id) as T` on EVERY read: the per-read cost this WP removes.
- `public abstract class BaseSettingsProvider` with `public static BaseSettingsProvider? Instance { get; internal set; }` (`:4266-4268`):
  what `TAOM.Tests/Infrastructure/CountingMcm.cs` swaps in a test.
- `MCM.Common.Dropdown<T>` (`:879`): `SelectedIndex` settable (`:887-897`), `SelectedValue => base[SelectedIndex]` (`:899`), ctor `(IEnumerable<T>, int)` (`:920`).
- MCM keeps one object per global settings id and copies resets and presets into it, so a provider may keep the
  reference it got on its first non-null read and read through it (the 1.5.x pattern, guarded by
  `CachedSettingsClasses_AreMcmGlobalSettings`). Cache only a non-null result, never in a constructor.

### Conventions (binding; read the file named when you touch that area)

- **ADR-007 adapters**: services never touch sealed TaleWorlds types (`Main/Adapters/*`). The ported code keeps the 1.5.x shape.
- **ADR-002 thin entry points**: behaviours and patches under 150 lines, delegating to services.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside the IoC registration.
- **TDD**: new work (Steps A4, D3) writes the failing test first. Ports bring their tests with the code.
- **MCM json2 rule**: never flip a persisted MCM default; rename the setting (Step A2 does exactly this).
- **`.claude/rules/harmony-patches.md`**: a postfix with `MovementOrder` in its signature goes through the shared
  deferred category `Patch_MissionTime_SetMovementOrder`. Step B2 only reorders checks inside
  `Patch35_Formation_SetMovementOrder`, which already is in that category; do not move it.
- **`.claude/rules/csharp-architecture.md`** "Config Providers MUST Validate": cached providers keep every clamp.

## Commands you will need

Run every command through the Bash tool, prefixed with `cd /c/Users/mikew/source/repos/taom-port/123 &&`.
Machine: the maintainer's laptop, Bannerlord v1.4.8 (build 119303) at `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
(env `BANNERLORD_GAME_DIR`); never write into the game install. Base: local `bannerlord-1.4.5` with every earlier WP merged.

| Purpose | Command | Expected on success |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is a known false positive) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | `Failed: 0` |
| Benchmark (Step D3) | `TAOM_RUN_BENCHMARKS=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~FileLoggerDurableWriteBenchmarkTests" --logger "console;verbosity=detailed"` | 1 passed, one `[LogBench]` line in the output |
| MCM default gate | `python tools/check_mcm_default_flips.py; echo "exit=$?"` (only if the file exists) | `exit=0` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs Step A0 |
| Docs | `python tools/lint_docs.py --fail-on-drift; echo "exit=$?"` then `python tools/lint_docs.py` | `exit=0` (or the same exit as Step A0); no new finding in a file this WP touched |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj` does not gate CopyModule on
DisableModuleCopy; without `-p:ModuleId=` a build deploys into the game install). Never `./build.ps1`.
Never two builds at once in this worktree. If `python` is not on PATH, use `/c/Python314/python.exe`.
`python tools/validate_moduledata.py` is not needed: no step touches ModuleData.

## Scope

**In scope** (the only paths you may change):
- Part A: the 20 deleted auto-resolve paths listed in Step A1; `Main/Features/TaomSettings.cs`;
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`; `Main/Features/Enlistment/EnlistmentDiagnosticsSettingsProvider.cs`,
  `IEnlistmentDiagnosticsSettingsProvider.cs`; `Main/Features/MissionDiagnostic/**`; `Main/Features/BannerColorPersistence/`
  (`AgentColorStore.cs`, `AgentColorStoreCleanupBehavior.cs`, `IAgentColorStore.cs`, `BannerColorPersistenceIoC.cs`,
  `Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs`, `Hooks/Mission_SpawnAgent_Patch.cs`);
  `Main/Features/BattleLoadDiagnostics/Hooks/Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs`;
  `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs`; tests:
  `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/Enlistment/{EnlistmentDiagnosticsSettingsProviderTests,EnlistmentDiagnosticsGateTests,DischargeServiceTests}.cs`,
  `TAOM.Tests/Features/MissionDiagnostic/**`, `TAOM.Tests/Features/BannerColorPersistence/AgentColorStoreTests.cs`,
  `TAOM.Tests/Composition/FeatureModulesTests.cs`; `tools/mcm_default_flips_allowlist.json` (one entry, if present);
  docs: `docs/features/{auto-resolve-diagnostics,coop-interop,bannerlord-together-compat,dread-aura,enlistment,mission-diagnostic,banner-color-persistence,battle-load-diagnostics,companion-tactics}.md`,
  `docs/modding/{file-catalogue,validation-and-testing,balance-levers}.md`, `docs/reference/harmony-patch-registry.md`,
  `docs/reference/taleworlds-api-snapshot/patch-targets.md`.
- Part B: `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`; the twelve 1.4.x files `74e9172d4` changes
  (listed in Step B2) plus the new `Main/Features/LocalizationOverride/IdSlice.cs`; tests named in Steps B1 and B2,
  `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs` and `TAOM.Tests/Infrastructure/CountingMcm.cs` (new);
  docs `docs/features/{battle-balance,combat-mechanics,companion-tactics,localization-override}.md`, `docs/modding/file-catalogue.md`.
- Part C: `Main/Adapters/{ITownRosterAdapter,TownRosterAdapter}.cs`; the AlignmentDesertion, CaravanTrade,
  CastleRecruitment, CulturalFeats, CultureMarketplace (not `Domain/CultureItemPool.cs`), FieldCommission,
  PartyIconScale, QuickActions, Refuge, SupplyLines, FieldCamp and TimeAcceleration files named in Steps C1 to C3;
  `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs`; `Main/Features/Refuge/Hooks/RefugeDamageHooks.cs`
  (new); `TAOM.Tests/Features/NameplateCull/NameplateCullSettingsProviderTests.cs` (deleted, Step C2); the tests named there; docs `docs/features/{alignment-desertion,cultural-feats,culture-marketplace,mcm,refuge,field-camp,supply-lines,nameplate-cull}.md`,
  `docs/modding/{file-catalogue,configs-balance}.md`; `docs/reference/harmony-patch-registry.md` (the Patch104 Tests list, Step C2); `.claude/rules/csharp-architecture.md` (one rule line, Step C2).
- Part D: `Main/Features/TaomSettings.cs`, `Main/Features/CrashReport/CrashReportSettings.cs`,
  `Main/Features/BlowDiagnostics/BlowDiagnosticsSettings.cs`, `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`
  (one line each), `Main/Features/CrashReport/Collectors/McmSettingsCollector.cs`, `TAOM.Tests/Features/CrashReport/McmSettingsCollectorTests.cs`;
  the three new `Main/Features/TroopWeight/Diagnostics/` files and `TAOM.Tests/Features/TroopWeight/MainRosterLossDiagnosticsTests.cs`;
  `TAOM.Tests/Core/Logging/FileLoggerDurableWriteBenchmarkTests.cs` (new); docs `docs/features/{crash-report,troop-weight-system,battle-load-diagnostics}.md`,
  the registry and snapshot.
- **Single-owner files, explicitly in scope**: `Main/IoC.cs` (Step A1: delete one line) and `Main/SubModule.cs`
  (Steps A1, A3, A4, D2: the edits named there and nothing else). `Main/TAOM.csproj`: not touched.

**Out of scope** (do not touch, even though they look related):
- `plans/README.md` (the integrator's; Step D4 reports to it), `CHANGELOG.md` (D1), `CLAUDE.md`, `AGENTS.md`, `docs/reviews/**` (lessons, REVIEW-LOG, RCAs, deep-review records,
  Codex prompts), `docs/INDEX.md`, `docs/reference/feature-map.md` (its stale AutoResolveDiagnostics row is WP142's),
  `tools/README.md`, `docs/reference/engine/**` (WP144).
- `Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs` and the cycle-hotkey parse cache (skipped, see Disposition).
- `Main/Features/CultureMarketplace/Domain/CultureItemPool.cs` (its `ContainsItem` serves only the 1.5.x armour pool keep).
- `Main/Core/Logging/FileLogger.cs`: Step D3 measures the logger, it does not change it.
- `Dependencies/**` (no TAOM.Dependencies change in this WP), `Main/_Module/**` (D20), `.claude/settings*.json`, `Directory.Build.props`, `docs/adrs/**`.
- Anything in `6ae924638` other than the auto-resolve removal. Other hunks of `6ae924638`: WP112, WP119, WP129, WP137 (NRE finalizers: WP112; HeroRace flag: WP119; career quest owner: WP129; the api-snapshot row and `/verify` step: WP137; Fire and Tribute: 1.5.x-only).

## Git workflow

- Worktree (from the main checkout, Bash tool): `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/123 -b port148/123-always-on-cost-cuts bannerlord-1.4.5`. Work only there.
- Per-port procedure (binding): apply a 1.5.x commit with `git cherry-pick -n <sha>` only. Never let `cherry-pick`,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit (`GIT_EDITOR=true` on this laptop commits the 1.5.x
  message unedited). If `git status` still reports a cherry-pick in progress after your own commit, run `git cherry-pick --quit`.
  Where a step says "file transplant", the file's 1.4.x content equals the 1.5.x parent content (verified for this
  plan and re-checked by the step's `git diff --quiet` guard), so `git checkout <sha> <path>` yields 1.4.x plus that commit.
- Bookkeeping hunks: a conflicted bookkeeping path gets `git checkout --ours <p>` then `git add <p>`; a cleanly changed one
  gets `git checkout HEAD <p>`; a bookkeeping file the commit adds gets `git rm -q -f <p>`.
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>` in the NEXT
  sequential Bash call (never the same command, never a parallel call, never through PowerShell). `cherry-pick -n` and
  `git checkout <sha> <path>` stage a file before you hand-edit it, so `git add` every file again after editing it;
  `git status --porcelain` must show no unstaged edit of this step's paths before the commit. Message files live
  outside the worktree: `C:/Users/mikew/source/repos/taom-port/123-msg-<step>.txt` (write them with the Write tool).
- Commit message: `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters; subjects given per step), a
  blank line, a body written for 1.4.x players (wrap 72; no em or en dashes; no `Co-Authored-By` or "Generated with"
  line; under D1 the body becomes the release note), then trailers: `Research:`, `Not-tested:`, optional
  `Save-compat:` / `Rejected:` / `Constraint:`, and one `Ported-from: <40-hex sha>` per included 1.5.x commit.
- If the `block-dangerous-git` hook asks about a `git checkout HEAD <p>`, `git checkout --ours/--theirs` or
  `git rm` that this plan names, approve it: it discards only this cherry-pick's own output in this worktree.
- Never push, open a PR, tag or merge. Before each commit re-read `git diff --cached --stat` and confirm only this
  step's paths are staged.

## Part A: the auto-resolve log, the enlistment trace and the mission diagnostics

### Step A0: Preconditions and baseline

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git log -1 --format='%h %s' && ls Main/Composition/FeatureModuleHooks.cs TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Composition/FeatureModulesTests.cs Main/Features/NameplateCull/NameplateCullSettingsProvider.cs TAOM.Tests/Features/NameplateCull/NameplateCullSettingsProviderTests.cs && grep -c 'TryPatchCategory("Patch17_TroopWeight")' Main/SubModule.cs && grep -c "public static string ReadSource" TAOM.Tests/Infrastructure/RepoPaths.cs && git ls-files Main/Features/AutoResolveDiagnostics | wc -l
```

**Verify**: five paths listed, then `1`, `1`, `10`. Also read the main checkout's index (read only):
`grep -E '^\| \[(105|117|120)\]' /c/Users/mikew/source/repos/TAOM/plans/README.md` must show all three rows as DONE.
Any miss: STOP (a dependency is not merged). Then run Restore, Build and Test and record the
totals (`Total`, `Passed`, `Skipped`) as **T0**; `Failed` must be 0. Run Tool tests and record failures (expected none).
Run Docs and record its exit and findings as the baseline.
Record which WP110 names the base declares:

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && grep -c -E "bool (EnableSmartCavalryCharge|EnableOobFormationPresets) " Main/Features/TaomSettings.cs; ls tools/check_mcm_default_flips.py 2>&1
```

`2` and a path means WP110 is merged (use its names in Steps B2 and C1); `0` and "No such file" means it is not.

Record the MCM version the base pins as **MCMV**, for the Step B2 and D1 `Research:` trailers:

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && grep -o 'Include="Bannerlord.MCM" Version="[^"]*"' Main/TAOM.csproj
```

**Verify**: one line, version `5.12.1` (WP118 not merged) or `5.12.3` (WP118 merged). Anything else: STOP.

### Step A1: Remove Auto-Resolve Diagnostics (`158408c73` with the removal hunks of `6ae924638`)

Sources: `158408c73` fix(logging): v2.0.35 - auto-resolved battles are no longer logged (docs half);
`6ae924638` "Add tests for NRE containment finalizers and improve documentation" (an 81-file mixed commit that
carries the code half of the removal). Method: hand port (`git rm` plus targeted edits). Do not cherry-pick
`6ae924638`: it drags NRE-guard, career, HeroRace and Fire and Tribute hunks that other WPs own.
`git cherry-pick -n 158408c73` would conflict on both its doc files (simulated this session); write its end state instead.

1. RED. In `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, in `EverySettingsClass_HasItsSplitPinned`,
   lower the `reflected:` number of the `typeof(TaomSettings)` line by exactly 2 (keep `covered:`). Build, then
   Filtered `SettingsFingerprintTests`. **Verify**: exactly one failure, in `EverySettingsClass_HasItsSplitPinned`,
   saying TaomSettings exposes 2 settings more than pinned.
2. Delete the code, tests and tool:

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git rm -q -r Main/Features/AutoResolveDiagnostics TAOM.Tests/Features/AutoResolveDiagnostics && git rm -q Main/Adapters/IMapEventBattleLogAdapter.cs Main/Adapters/MapEventBattleLogAdapter.cs Main/Adapters/ITroopCensusAdapter.cs Main/Adapters/TroopCensusAdapter.cs tools/analyze_battle_logs.py && git status --porcelain | grep -c "^D "
```

   **Verify**: `20`.
3. `Main/Features/TaomSettings.cs`: delete from the line `    // --- Battle Tactics / Auto-Resolve Diagnostics ---`
   through `    public bool LogAutoResolveTroopCensus { get; set; } = false;` and the one blank line after it (22 lines plus the blank at `1b00d1acd`).
4. `CoopSettingsRelevance.cs`: delete the line `        "LogAutoResolvedBattles", "LogAutoResolveTroopCensus",`.
5. `Main/IoC.cs`: delete `        Features.AutoResolveDiagnostics.AutoResolveDiagnosticsIoC.RegisterAutoResolveDiagnosticsFeature(container);`.
6. `Main/SubModule.cs`: delete these three lines (nothing else):
   `        // Records one raw record per completed map battle so auto-resolve balance can be tuned`,
   `        // against real armies rather than party templates. Gated by MCM; one line per battle.`,
   `        campaignStarter.AddBehavior(IoC.Resolve<Features.AutoResolveDiagnostics.AutoResolveDiagnosticsBehavior>());`.
7. Docs:
   - `docs/features/auto-resolve-diagnostics.md`: `git show 158408c73:docs/features/auto-resolve-diagnostics.md > docs/features/auto-resolve-diagnostics.md`,
     then replace `**Status: REMOVED 2026-10-26 (v2.0.35).**` with `**Status: REMOVED.** Removed from the 1.4.x line by WP123 (the first release after v2.0.28); on the 1.5.x line in v2.0.35.`
     and, at the end of the "## Why it was removed" paragraph, add: `On the public 1.4.8 build it was 39 percent of one 35-minute player log (225 records, about 421 KB of 1,092,378 bytes, 2026-11-01).`
   - `docs/features/coop-interop.md` ("No MCM settings parity" paragraph): the bold total, the `TaomSettings` split
     number and "The N excluded" each go down by 2 from their current values.
   - `docs/features/bannerlord-together-compat.md`: "TAOM's N MCM settings" goes down by 2.
   - `docs/features/dread-aura.md`: in the "Auto-resolve does not see the aura" bullet delete the sentence
     `This is also a new confound for the` / `` `AutoResolveDiagnostics` census (#430), which compares manual and auto-resolved outcomes.`` so the bullet ends at `harsher than auto-resolving one.`
   - `docs/modding/file-catalogue.md`: in the `configs/battle_balance_config.json` row delete ``; analysed by `tools/analyze_battle_logs.py` ``.
   - `docs/modding/validation-and-testing.md`: delete the table row that starts ``| `python tools/analyze_battle_logs.py` |``.
   - `docs/modding/balance-levers.md`: replace ``[`auto-resolve-diagnostics.md:9`](../features/auto-resolve-diagnostics.md)`` with
     ``[`auto-resolve-diagnostics.md`, "Why this exists"](../features/auto-resolve-diagnostics.md#why-this-exists)``, the words
     `Its own line 13 already carries` with `Its own formula already carries`, and ``([auto-resolve-diagnostics.md:8-27](../features/auto-resolve-diagnostics.md))``
     with ``([auto-resolve-diagnostics.md, "Why this exists"](../features/auto-resolve-diagnostics.md#why-this-exists))``.

**Verify**: Build exit 0; Filtered `SettingsFingerprintTests|CoopVeto` (use `--filter "FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~CoopVeto"`) `Failed: 0`;
`git grep -n -E "AutoResolveDiagnostics|LogAutoResolve|MapEventBattleLogAdapter|TroopCensusAdapter|analyze_battle_logs" -- Main TAOM.Tests tools .ai` prints nothing;
MCM default gate `exit=0` (if present); Tool tests no new failure; Docs `exit=0`.

Commit (`123-msg-A1.txt`), staging the 20 deletions plus the edited files:
- Subject: `fix(logging): v2.0.28 - auto-resolved battles are no longer logged`
- Body: Auto-Resolve Diagnostics is removed. It wrote one record for every battle fought on the campaign map, was on
  by default, and wrote each record to disk on the campaign thread; in one 35-minute 1.4.8 session it was 39 percent
  of the TAOM log. Its balance study is finished. The two MCM switches ("Log Auto-Resolved Battles", "Log Troop
  Census") and tools/analyze_battle_logs.py go with it. Only the auto-resolve removal of 6ae924638 is ported.
- Trailers: `Research: git grep of the 1.4.x tree finds no consumer of the removed types; the behaviour's SyncData was empty`,
  `Save-compat: nothing was saved; a TAOM.json value for either removed switch is ignored`,
  `Not-tested: in game, a campaign session writes no [AutoResolve] line and MCM shows no Auto-Resolve Diagnostics group`,
  `Ported-from: 158408c7350f4de661901ae17a81bd852d22113e`, `Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3`.

### Step A2: The enlistment trace ships off again under a new name (`890fe29de`)

Source: `890fe29de` fix(enlistment): v2.0.35 - the enlistment trace ships off again. Method: `cherry-pick -n`.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git cherry-pick -n 890fe29de; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected (simulated on `1b00d1acd`): `exit=1`, unmerged `Main/Features/CoopInterop/CoopSettingsRelevance.cs`,
`Main/Features/Enlistment/EnlistmentDiagnosticsSettingsProvider.cs`, `TAOM.Tests/Features/Enlistment/EnlistmentDiagnosticsSettingsProviderTests.cs`.
`Main/Features/TaomSettings.cs`, `DischargeServiceTests.cs`, `EnlistmentDiagnosticsGateTests.cs` or `docs/features/enlistment.md`
may also appear if an earlier WP edited them. Any other path: STOP. Resolve:

1. `CoopSettingsRelevance.cs`: `git checkout --ours` it, then change only `"EnableEnlistmentDiagnostics", "EnableFieldCommissionDiagnostics",`
   to `"EnableEnlistmentTrace", "EnableFieldCommissionDiagnostics",`. Add no other name.
2. `EnlistmentDiagnosticsSettingsProvider.cs`: `git checkout --theirs` it, then in the summary replace the 1.5.x-only words
   `is null in tests, before` / `/// MCM initializes, or on a client whose MCM failed to load (a dedicated server always gets the` /
   `/// instance built from <c>taom-server-settings.json</c>), so the setting falls back to its compiled default: off.`
   with `is null whenever MCM is` / `/// absent or not yet initialized, so the setting falls back to its compiled default: off.`
3. `EnlistmentDiagnosticsSettingsProviderTests.cs`: `git checkout --theirs` it; replace `v2.0.24 to v2.0.35` with
   `v2.0.24 to v2.0.28 and the public 1.4.8 build`.
4. `TaomSettings.cs` (conflicted or not): the property must read `public bool EnableEnlistmentTrace { get; set; } = false;`
   under `[SettingPropertyBool("Enable Enlistment Trace", ...)]` (take theirs for those lines if conflicted). In the
   comment block replace `it shipped ON in every release from v2.0.24 (2026-09-01) through v2.0.35 (2026-10-19)` with
   `it shipped ON in every 1.4.x release from v2.0.24 (2026-09-01) through v2.0.28 (2026-09-04) and the public 1.4.8 build`,
   and `(v2.0.24 to v2.0.35)` with `(v2.0.24 to v2.0.28)`.
5. `IEnlistmentDiagnosticsSettingsProvider.cs`: replace `v2.0.24 through v2.0.35` with `v2.0.24 through v2.0.28`.
   `docs/features/enlistment.md`: replace `so v2.0.34 shipped it on` with `so every 1.4.x release from v2.0.24 through v2.0.28 shipped it on`.
6. If `tools/mcm_default_flips_allowlist.json` exists and has the key `"TaomSettings.EnableEnlistmentDiagnostics"`, delete
   that entry (keep valid JSON). `git add` every resolved path.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~Enlistment|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~McmRenamedDefaults"` `Failed: 0`;
`git grep -n -E "\.EnableEnlistmentDiagnostics|bool EnableEnlistmentDiagnostics" -- Main TAOM.Tests` prints nothing;
`git grep -n -E "taom-server-settings|v2\.0\.3[0-9]" -- Main/Features/Enlistment Main/Features/TaomSettings.cs docs/features/enlistment.md TAOM.Tests/Features/Enlistment` prints nothing;
MCM default gate `exit=0` (if present).

Commit (`123-msg-A2.txt`): subject `fix(enlistment): v2.0.28 - the enlistment trace ships off again`. Body: the routine
enlistment trace was switched on for a test in late August and shipped on in every release since; it buried
crash-report lines. It is off again under a new MCM name ("Enable Enlistment Trace"), because MCM keeps a saved
value per setting name and a flipped default would not have reached players who saved settings. Real faults always log.
Trailers: `Save-compat: the old key is ignored on load and dropped by the next settings save`, `Not-tested: in game, an enlisted session logs no routine [EnlistDiag] TICK or SYNC line`, `Ported-from: 890fe29dec3a44a4a706376089e8dc375b65e692`.

### Step A3: Cheaper always-on mission diagnostics (`e0ad0db76`, partial)

Source: `e0ad0db76` perf(diagnostics): v2.0.32 - cheaper always-on mission diagnostics. Keep: the action-set census
integer key, the deletion of `Patch35_Mission_OnTick`, the deletion of the agent colour store (two patch members,
cleanup behaviour, IoC line, tests), the BattleLoad comment line, the `SubModule.cs` cleanup and the
`FeatureModulesTests` anchor move. Drop: the CareerSystem hit tallies (the 1.4.x `CareerAgentStatService` has no
per-hit lines; they come from 1.5.x-only #613), CreatureBandits and TrollBruteForce (1.5.x-only features), the engine
doc (WP144) and the review records.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git cherry-pick -n e0ad0db76; echo "exit=$?"; git diff --name-only --diff-filter=U
```

Expected: `exit=1`. Then drop every out-of-scope path in one loop (restores what exists at HEAD, removes what does not):

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && for p in Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs Main/Features/CareerSystem/CareerPerkMissionBehavior.cs Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiag.cs Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagTicker.cs Main/Features/CreatureBandits/Diagnostics/CreatureBanditDiagnosticsBehavior.cs Main/Features/CreatureBandits/Diagnostics/CreatureDiagLedger.cs Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs Main/Features/TrollBruteForce/TrollPresence.cs TAOM.Tests/Features/CareerSystem/CareerAgentStatServiceTests.cs TAOM.Tests/Features/CareerSystem/CareerPerkMissionTeardownTests.cs TAOM.Tests/Features/CreatureBandits/CreatureBanditDiagTests.cs TAOM.Tests/Features/CreatureBandits/CreatureDiagLedgerTests.cs TAOM.Tests/Features/TrollBruteForce/TrollPresenceTests.cs docs/features/career-system.md docs/features/creature-bandits.md docs/features/dev-console.md docs/features/troll-brute-force.md docs/reference/engine/mission-and-missionbehavior-lifecycle.md docs/reviews/REVIEW-LOG.md docs/reviews/lessons/gamemodels-services.md docs/reviews/lessons/misc.md docs/reviews/deep-review-030-mission-diagnostics-diet-2026-10-02.md docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; ls -d Main/Features/CreatureBandits Main/Features/TrollBruteForce 2>&1; git diff --name-only --diff-filter=U
```

**Verify**: both `ls` lines say "No such file or directory"; the remaining unmerged list holds only paths from:
`Main/SubModule.cs`, `TAOM.Tests/Composition/FeatureModulesTests.cs`, `docs/features/banner-color-persistence.md`,
`docs/features/companion-tactics.md`, `docs/reference/harmony-patch-registry.md`,
`docs/reference/taleworlds-api-snapshot/patch-targets.md`, `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs`.
Any other path: STOP. Resolve each (and apply the same edit to any of these that merged cleanly but still holds the old text):

1. `Main/SubModule.cs`: `git checkout --ours`. Replace the three lines
   `        var agentColorStore = IoC.Resolve<IAgentColorStore>();` /
   `        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);` /
   `        Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);`
   with `        Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter);`. Delete
   `        var colorStore = IoC.Resolve<IAgentColorStore>();` / `        if (colorStore != null)` /
   `            AddTaomBehavior(new AgentColorStoreCleanupBehavior(colorStore));` and the blank line after them.
2. `TAOM.Tests/Composition/FeatureModulesTests.cs`: take ours if conflicted; replace the anchor string
   `"new AgentColorStoreCleanupBehavior(colorStore)"` with
   `"AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());"`. First check that line
   occurs exactly once in `Main/SubModule.cs` (`grep -c -F` prints `1`) and before `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);`;
   otherwise use the nearest line above the hook that occurs once (never delete the assertion).
3. `Patch35_Mission_OnTick.cs` (modify/delete when WP110 edited it): `git rm -q -f` it.
4. `docs/features/banner-color-persistence.md` (ours, then): the sentence that begins with the file name
   Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs and the words "covers hero agents only" (line 26 at `1b00d1acd`)
   and ends with "prefix is what covers ordinary troops." (line 27) becomes: The `SpawnAgent` prefix is the only
   spawn-time recolour (an agent whose caller already chose its equipment keeps the colours it was given). Keep the
   rest of line 27 ("Anyone editing a clan's ..."). Delete the table rows for `IAgentColorStore.cs` / `AgentColorStore.cs`,
   `AgentColorStoreCleanupBehavior.cs`, ``Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs`` and `AgentColorStoreTests.cs`;
   `Registers all 4 singletons` to `Registers all 3 singletons`; `6 test files, 33 test methods` to `5 test files, 29 test methods`;
   `6 files, 33 [TestMethod] as of 2026-09-02` to `5 files, 29 [TestMethod] as of <today>` (keep the backticks around `[TestMethod]`).
5. `docs/features/companion-tactics.md`: `(5 Harmony patches)` to `(4 Harmony patches)`; delete the
   `Patch35_Mission_OnTick.cs` Key Files row and the Performance bullet that starts ``- `Patch35_Mission_OnTick` is on the engine-tick hot path``.
6. `docs/reference/harmony-patch-registry.md` (ours, then): Patch23 **Target** list: delete ``` `Agent.EquipItemsFromSpawnEquipment`, ```;
   Patch35 **Target**: delete ``, `Mission.OnTick(float,float,bool,bool)` (Postfix)``; Patch35 description: delete
   ``; the `Mission.OnTick` Postfix is a near-no-op hot path (`` through ``button commands)`` (keep `. See ...companion-tactics.md`.`);
   Patch43: `Phases coexist with the pre-existing Patch16 / Patch23 hooks on the shared targets.` to
   ``Phases coexist with the pre-existing Patch16 hook on the shared target (`Mission.Initialize`).``; if a Patch91 section says
   ``Composes with `Patch35_Mission_OnTick` (postfix on `Mission.OnTick`, which runs inside the frame probe) and Patch37's finalizer on `Mission.Tick`.``,
   make it ``Composes with Patch37's finalizer on `Mission.Tick`.``
7. `docs/reference/taleworlds-api-snapshot/patch-targets.md` (ours, then): delete the rows for
   `TAOM.Features.BannerColorPersistence.Hooks.Agent_EquipItemsFromSpawnEquipment_Patch` and
   `TAOM.Features.CompanionTactics.FormationPresets.Hooks.Patch35_Mission_OnTick`; lower `Patches: N.` by 2.
8. `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticBehaviorTests.cs`: delete the line `[TestCategory("RequiresGame")]` (D9).
   `git add` every resolved path.

**Verify**: `git diff --name-only --diff-filter=U` prints nothing; Build exit 0; Filtered
`--filter "FullyQualifiedName~MissionDiagnostic|FullyQualifiedName~BannerColor|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~CompanionTactics|FullyQualifiedName~HarmonyPatchBindingTests"` `Failed: 0`;
`git grep -n -E "IAgentColorStore|AgentColorStoreCleanupBehavior|class AgentColorStore|Patch35_Mission_OnTick|Agent_EquipItemsFromSpawnEquipment_Patch\b" -- Main TAOM.Tests` prints nothing;
`grep -c -E "_colorStore|Postfix|agentSpawnEquipment" Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs` prints `0`;
`git diff --cached --name-only | grep -c -E "CareerSystem|CreatureBandits|TrollBruteForce|docs/reviews"` prints `0`.

Commit (`123-msg-A3.txt`): subject `perf(diagnostics): v2.0.28 - cheaper always-on mission diagnostics`. Body: three
diagnostics that run in every battle stop paying per-frame or per-spawn costs for lines they rarely write. The action-set
census (first 5 seconds of a mission) checks each agent on an integer key and reads names only for a new action set,
race and sex; its lines are unchanged and two INFO lines now open and close the window. An empty Companion Tactics
postfix on Mission.OnTick is deleted. A banner colour store that every spawned agent wrote twice and nothing read is
deleted with its patch member and cleanup behaviour; troop armour tint still comes from the Mission.SpawnAgent prefix.
Trailers: `Research: taom-src v1.4.8 MBActionSet.GetHashCode returns its index (MBActionSet.cs:31-33); Agent.ActionSet builds a new MBActionSet per read (Agent.cs:696)`,
`Not-tested: in game, census lines in a mission's first 5 s, clan-coloured troops keep their tint, OOB preset buttons work`,
`Ported-from: e0ad0db765d3f6f47cbce580eb8eb5662377d120`.

### Step A4: Log the campaign date once the clock is set (new work, TDD)

Why: the session snapshot runs in `SubModule.OnGameStart`, which v1.4.8 calls before `CampaignTime.Initialize()`
(`Campaign.cs:1391`, `:1394`), so on every save load it prints `<time read failed: DivideByZeroException>` (E3:56,
E4:55). E3 froze before its first mission, so after Step A1 its log would carry no date at all (its only date stamps
were auto-resolve records). Fix: the session snapshot never reads the clock and says where the date is; a new line
writes the date at `OnGameInitializationFinished` (`Campaign.cs:1452`, after the clock and the loaded `MapTimeTracker` exist).

1. RED. Add to `TAOM.Tests/Features/MissionDiagnostic/CampaignContextFormatterTests.cs`:

```csharp
    // The session snapshot runs at SubModule.OnGameStart, before v1.4.8 Campaign.OnInitialize calls
    // CampaignTime.Initialize() (Campaign.cs:1391, 1394), so it never reads the clock (WP123).
    [TestMethod]
    public void DescribeBeforeClock_NamesWhereTheDateIsLogged_AndKeepsTheHero()
    {
        var line = CampaignContextFormatter.DescribeBeforeClock(() => "MainHero='Drel'");

        Assert.AreEqual("date=<logged at game init>, MainHero='Drel'", line);
    }

    [TestMethod]
    public void DescribeBeforeClock_HeroReaderThrows_StillOneLine()
    {
        var line = CampaignContextFormatter.DescribeBeforeClock(() => throw new NullReferenceException());

        Assert.AreEqual("date=<logged at game init>, <hero read failed: NullReferenceException>", line);
    }
```

   Add to `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticServiceTests.cs` (add `using System.Linq;` and `using TAOM.Tests.Migration;`):

```csharp
    [TestMethod]
    public void LogCampaignDate_NoCampaign_WritesNothing()
    {
        _sut.LogCampaignDate();

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void LogSessionSnapshot_DescribesTheCampaign_WithoutReadingTheClock()
    {
        var method = typeof(MissionDiagnosticService).GetMethod(nameof(MissionDiagnosticService.LogSessionSnapshot))!;
        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToList();

        Assert.IsTrue(calls.Any(m => m.Name == "DescribeBeforeClock"), "the session line uses DescribeBeforeClock");
        Assert.IsFalse(calls.Any(m => m.Name == "LogCampaignContext" || m.Name == "ReadCampaignTime"),
            "the session snapshot runs before CampaignTime.Initialize and must not read the clock");
    }
```

   Create `TAOM.Tests/Features/MissionDiagnostic/MissionDiagnosticWiringTests.cs`:

```csharp
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MissionDiagnostic;

[TestClass]
public class MissionDiagnosticWiringTests
{
    // Every game init logs the date once the clock exists, the load that freezes before a mission included,
    // so the call sits before the once-per-process patch guard.
    [TestMethod]
    public void OnGameInitializationFinished_LogsTheCampaignDate_BeforeTheOncePerProcessGuard()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        int method = src.IndexOf("public override void OnGameInitializationFinished(", StringComparison.Ordinal);
        int call = src.IndexOf("IMissionDiagnosticService>()?.LogCampaignDate();", StringComparison.Ordinal);
        int guard = src.IndexOf("if (_gameInitPatchesApplied) return;", StringComparison.Ordinal);

        Assert.IsTrue(method >= 0 && call > method && guard > call, $"method={method} call={call} guard={guard}");
    }
}
```

   **Verify (RED)**: Build fails with CS0117/CS1061 naming `DescribeBeforeClock` and `LogCampaignDate`, and nothing else.

2. GREEN.
   - `CampaignContextFormatter.cs`: add below `Describe`:
     ```csharp
     // The session snapshot runs before CampaignTime.Initialize() (v1.4.8 Campaign.cs:1391, 1394): it names
     // where the date is logged instead of reading a clock that would divide by zero.
     public const string DateBeforeClock = "date=<logged at game init>";

     public static string DescribeBeforeClock(Func<string> readHero) =>
         $"{DateBeforeClock}, {Read(readHero, "hero", "<no hero>")}";
     ```
   - `MissionDiagnosticService.LogSessionSnapshot`: in the `GameStarted` branch replace `LogCampaignContext();` with
     `_logger.LogInfo($"[MissionDiag] Campaign: {CampaignContextFormatter.DescribeBeforeClock(ReadMainHero)}");`, and rewrite the
     comment above the `if` to say the snapshot runs at `OnGameStart`, before `CampaignTime.Initialize()`, so it never reads
     the clock; the date is logged by `LogCampaignDate` at game init and by every mission start.
   - Add to `MissionDiagnosticService` and to `IMissionDiagnosticService` (with a one-line comment):
     ```csharp
     public void LogCampaignDate()
     {
         try
         {
             if (Campaign.Current == null || !Campaign.Current.GameStarted) return;
             _logger.LogInfo($"[MissionDiag] Campaign at game init: {CampaignContextFormatter.Describe(ReadCampaignTime, ReadMainHero)}");
         }
         catch (Exception ex)
         {
             _logger.LogWarning($"[MissionDiag] LogCampaignDate failed: {ex.GetType().Name}: {ex.Message}");
         }
     }
     ```
   - `Main/SubModule.cs`, in `OnGameInitializationFinished`, directly after the line
     `        StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);` insert:
     ```csharp

        // [MissionDiag] the campaign date, once per game init: the clock exists by now (v1.4.8 Campaign.cs:1394
        // runs before :1452); the session snapshot at OnGameStart is too early to read it.
        try { IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogCampaignDate(); }
        catch { /* diagnostic is best-effort, never break a load */ }
     ```
   - `docs/features/mission-diagnostic.md`: replace `driven by an `OnSessionLaunched` boundary registered in `SubModule``
     with `called from `SubModule.OnGameStart``; add one sentence after that paragraph: the session line reads
     `Campaign: date=<logged at game init>, MainHero=...` because the clock is not initialised at `OnGameStart`, and
     `LogCampaignDate` writes `[MissionDiag] Campaign at game init: now=<date>, MainHero=...` from
     `OnGameInitializationFinished` on a loaded save; the interface row: `7 methods` to `8 methods` and add `LogCampaignDate`.

**Verify**: Build exit 0; Filtered `MissionDiagnostic` `Failed: 0` (the formatter, service and wiring tests included).

Commit (`123-msg-A4.txt`): subject `fix(diagnostics): v2.0.28 - log the campaign date once the clock is set`. Body: the
session snapshot in every TAOM log printed "time read failed: DivideByZeroException" on a save load, because it runs
before the game sets up its calendar. It now says where the date is logged, and a new line writes the in-game date and
hero right after the load finishes, so a log that stops before the first battle still carries the date.
Trailers: `Research: taom-src v1.4.8 Campaign.OnInitialize calls OnGameStart (Campaign.cs:1391) before CampaignTime.Initialize (:1394) and OnGameInitializationFinished last (:1452)`,
`Not-tested: in game, load a save: the session snapshot shows date=<logged at game init> and a "Campaign at game init: now=" line follows`.
No `Ported-from` (new work).

### Step A5: Part A gate

Run Build, Test, Tool tests and Docs. **Verify**: `Failed: 0`; `Skipped` equals T0's; `Total` = T0 minus 56
(auto-resolve) minus 4 (`AgentColorStoreTests`) plus the tests added in A2 to A4 (A2: +1 net; A3: +10; A4: +5; adjust if
an earlier WP already changed these files; explain any other difference in the hand-off). Record the totals as **TA**.
`git log --format=%s bannerlord-1.4.5..HEAD` lists the four Part A subjects, each containing `v2.0.28 - `.

## Part B: battle settings read once

### Step B1: Battle Balance reads its MCM object once (`7feca96be`, `02157b18b`, `ab70555db`; end state)

Sources: `7feca96be` perf(battlebalance): v2.0.30 - read the MCM settings once per process; `02157b18b`
fix(battlebalance): v2.0.30 - review follow-ups for plan 003; `ab70555db` fix(battlebalance): v2.0.30 - convergence fixes
for plan 003. Method: file transplant of the series end state. The three commits rewrite one provider and one new test
file in sequence; the plan 003 merge `40615c226` carries no resolution for them (`git diff ab70555db 40615c226` on these
files is empty), no later 1.5.x commit touches them, and the 1.4.x files equal the series' pre-state.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git diff --quiet 1b00d1acd -- Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs docs/features/battle-balance.md && echo SAME && git checkout ab70555db Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs docs/features/battle-balance.md && grep -c "_settings ??= TaomSettings.Instance" Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs
```

**Verify**: `SAME` then `1`. No `SAME`: STOP (the base drifted). Then:
- In the new test file, the comment above `Provider_ResolvesFromARealContainer` spans two lines: replace `at registration (IoC.cs:127,` /
  `// the container build), before any resolve.` with `at registration (the` /
  `// BattleBalanceIoC.RegisterBattleBalanceFeature call in IoC.Configure), before any resolve.`
- In `docs/features/battle-balance.md` change the changelog line `- 2026-09-24: plan 003 (PERF-04):` to `- 2026-11: 1.5.x plan 003 (PERF-04), ported by WP123:`.
- `docs/modding/file-catalogue.md`: in the `configs/battle_balance_config.json` row replace `BattleBalanceSettingsProvider.cs:5-17` with `BattleBalanceSettingsProvider.cs:20-32`
  (confirm with `grep -n "public bool EnableCustomTroopPower\|public bool EnableCulturalSurvivalBonuses" Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`: lines 20 and 32).

**Verify**: Build exit 0; Filtered `BattleBalance` `Failed: 0` (10 new `BattleBalanceSettingsProviderTests` pass).
Commit (`123-msg-B1.txt`): subject `perf(battlebalance): v2.0.28 - read the MCM settings once per process`. Body: the
battle balance settings were looked up in Mod Options on every read, and the engine reads them once per casualty, twice
per hit that earns experience and once per roster row in every strength sum. The provider now keeps the settings object
from its first successful lookup and reads through it; a change in Mod Options still applies at once. Trailers:
`Research: DryIoc 4.8.8 still selects the single public constructor (Provider_ResolvesFromARealContainer)`,
`Not-tested: in game, change a Battle Balance slider mid-campaign and see auto-resolve follow it`,
`Ported-from: 7feca96bed1a91cade702ac4432c35b8408b2288`, `Ported-from: 02157b18ba69c48c5a172024ebe4dd0685f5a806`, `Ported-from: ab70555dbbd1b5107d8c345cc730ab1d40719e9d`.

### Step B2: Battle providers keep their MCM object; cheap facts first (`74e9172d4`, partial)

Source: `74e9172d4` perf(battle): v2.0.32 - read MCM settings once, not per blow or frame. Keep: the BlowDiagnostics,
CombatMechanics, CompanionTactics, DreadAura, MixedFormations, SiegePropDiagnostics and SmartCavalryAI providers; the
check reorders in `CreatureCombatService`, `CrushThroughService`, `RaceCombatModifiersResolver` and
`Patch35_Formation_SetMovementOrder`; `IdSlice` in the English override (WP135's `407b1e767` needs it); the tests.
Drop: CultureDoctrine and Howdah providers (absent on 1.4.x); the Mixed Formations cycle-hotkey parse cache and its
stricter key rule (see Disposition); `docs/modding/module-dependencies.md` (WP118 owns that doc); records.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git cherry-pick -n 74e9172d4; echo "exit=$?"; for p in Main/Features/CultureDoctrine/CultureDoctrineSettingsProvider.cs TAOM.Tests/Features/CultureDoctrine/CultureDoctrineSettingsProviderTests.cs Main/Features/Elephant/HowdahDiagnosticsSettingsProvider.cs TAOM.Tests/Features/Elephant/HowdahDiagnosticsSettingsProviderTests.cs Main/Features/MixedFormations/CachedEnumParse.cs Main/Features/MixedFormations/CycleHotkeyParser.cs Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs TAOM.Tests/Features/MixedFormations/CachedEnumParseTests.cs TAOM.Tests/Features/MixedFormations/CycleHotkeyInputKeyTests.cs docs/features/mixed-formations.md docs/modding/module-dependencies.md docs/reviews/REVIEW-LOG.md docs/reviews/lessons/harmony-il.md docs/reviews/lessons/testing-qa.md docs/reviews/deep-review-031-settings-reads-off-hot-paths-2026-10-02.md docs/reviews/rca-settings-reads-off-hot-paths-2026-10-02.md TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; git diff --name-only --diff-filter=U
```

Expected remaining unmerged (simulated): `Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs`,
`TAOM.Tests/Features/CombatMechanics/RaceCombatModifiersResolverTests.cs`. When WP110 is merged also possible:
`SmartCavalryAISettingsProvider.cs`, `CompanionTacticsSettingsProvider.cs`, `docs/features/companion-tactics.md`. Any other path: STOP. Resolve:

1. `CombatMechanicsSettingsProvider.cs`: `git checkout --ours` it. After `private readonly CombatMechanicsConfig _defaults;` insert
   ```csharp

    // HOT PATH: read per melee blow (crush-through, cleave, stagger, shield penetration) and per mount stat update.
    // Resolving TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first non-null
    // read and read THROUGH, never snapshotted: MCM edits its one registered instance in place (reset and presets copy
    // values into it), so live MCM edits still apply. Lazy, not in the constructor, so a resolve before MCM is up cannot
    // pin the JSON fallbacks. Same contract as BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;
   ```
   after the public constructor insert
   ```csharp

    internal CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider, TaomSettings settings)
        : this(configProvider) => _settings = settings;
   ```
   then replace every remaining `TaomSettings.Instance?.` with `Settings?.`. Add no property. **Verify** (comments
   mention `TaomSettings.Instance`, so count the two code shapes): `grep -c -E "TaomSettings\.Instance\?\." Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs`
   prints `0` and `grep -c "??= TaomSettings.Instance" Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` prints `1`.
2. `RaceCombatModifiersResolverTests.cs`: `git checkout --ours` it and append inside the class, before its closing brace:
   ```csharp

    // Per-agent gate order: an agent with no race id never reads the toggle.
    [TestMethod]
    public void Resolve_NullRaceId_NeverReadsTheToggle()
    {
        _settings.ClearReceivedCalls();

        var result = _sut.Resolve(null);

        Assert.AreSame(RaceCombatModifiers.Neutral, result);
        _ = _settings.DidNotReceive().RaceCombatModifiersEnabled;
    }
   ```
3. `SmartCavalryAISettingsProvider.cs` / `CompanionTacticsSettingsProvider.cs` when conflicted: take ours and apply the same
   pattern as item 1 (the two fields, a public parameterless constructor, `internal X(TaomSettings settings) => _settings = settings;`,
   every `TaomSettings.Instance?.` to `Settings?.`), keeping the base's (WP110's) setting and member names.
   `companion-tactics.md` when conflicted: ours plus the three `74e9172d4` line edits (the provider row, the
   `ICompanionTacticsSettingsProvider` bullet, `5 tests` to `6 tests` for `SharedMovementOrderPostfixTests.cs`).
4. Create `TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs` (the 1.5.x file tests #610 knobs
   that 1.4.x does not have):
   `git show 74e9172d4:TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs > TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs`, then:
   delete the tests `NoMcm_ChargeNeutralWeightRatio_FallsBackToJson`, `NoMcm_ChargeHorsePenetration_FallsBackToJson`,
   `NoMcm_ChargeMinPenetrationFactor_FallsBackToJson`, `NoMcm_CultureChargeDamageEnabled_FallsBackToJson`,
   `AutoFloor_DerivesFromTheNeutralRatio` and `AutoRatioFloor_FollowsALiveNeutralEdit_ThroughTheCachedSettings`; in
   `McmDefaults_MatchTheCompiledJsonDefaults` keep only the `AutoKnockdownWeightRatio` assertion; in
   `Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply` delete the four rows for `EnableCultureChargeDamage`,
   `ChargeNeutralWeightRatio`, `ChargeHorsePenetration` and `ChargeMinPenetrationFactor` (10 rows remain; the
   `ChargeAutoKnockdownWeightRatio = 10` row passes on 1.4.x because the floor is the JSON neutral ratio 6); in
   `AssertEveryEnabledGetterFalse` delete the `CultureChargeDamageEnabled` line; in the class summary delete ` (#610)`.
5. Adapt the new test files:
   - `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs`: delete `using TAOM.Features.CultureDoctrine;` and
     `using TAOM.Features.Elephant;`, every `DataRow` and `Probes` entry naming `HowdahDiagnostics` or `CultureDoctrine`,
     and the nested `private sealed class CountingMcm` at the end of the file. Then create the shared helper:
     `git show bb68eeca3:TAOM.Tests/Infrastructure/CountingMcm.cs > TAOM.Tests/Infrastructure/CountingMcm.cs`, then replace the line `/// and the static readers they cannot construct (CreatureBanditTuningLiveTests).` with `/// and the static readers they cannot construct.`
   - If WP110 is merged: in `HotPathSettingsProvidersTests.cs` and `SmartCavalryAISettingsProviderTests.cs` replace
     `EnableSmartCavalryAI` with `EnableSmartCavalryCharge`; in `CompanionTacticsSettingsProviderTests.cs` replace
     `EnableFormationPresets` with `EnableOobFormationPresets`.
   - Delete any `[TestCategory("RequiresGame")]` line in the files this step adds (D9).
   `git add` every resolved and new path.

**Verify**: `git diff --name-only --diff-filter=U` prints nothing; Build exit 0; Filtered
`--filter "FullyQualifiedName~HotPathSettingsProviders|FullyQualifiedName~CombatMechanics|FullyQualifiedName~BlowDiagnostics|FullyQualifiedName~CompanionTactics|FullyQualifiedName~DreadAura|FullyQualifiedName~MixedFormations|FullyQualifiedName~SiegePropDiagnostics|FullyQualifiedName~SmartCavalry|FullyQualifiedName~LocalizationOverride|FullyQualifiedName~CreatureCombat|FullyQualifiedName~CrushThrough|FullyQualifiedName~RaceCombatModifiers"` `Failed: 0`;
for each of the seven providers (BlowDiagnostics, CombatMechanics, CompanionTactics, DreadAura, MixedFormations,
SiegePropDiagnostics, SmartCavalryAI), `grep -c -E "(TaomSettings|BlowDiagnosticsSettings)\.Instance\?\." <file>` prints `0`
and `grep -c -E "\?\?= (TaomSettings|BlowDiagnosticsSettings)\.Instance" <file>` prints `1`;
`git grep -n -E "CycleHotkeyParser|CachedEnumParse|EnumNames" -- Main TAOM.Tests` prints nothing;
`git diff --cached --name-only | grep -c -E "CultureDoctrine|Elephant|module-dependencies|docs/reviews"` prints `0`.
If a test fails with `FileNotFoundException` or `TypeLoadException` for `MCMv5` or `MCM.Abstractions`: STOP (environment: the MCM runtime is not in the test output).
If `CountingMcm.cs` does not compile against MCMV, its static `SetInstance` lookup throws, or `Provider_AsksMcmUntilItHasTheSettings_ThenNeverAgain_AndReadsThrough` fails on its lookup count: STOP and report MCMV (the "MCM facts" above were read on 5.12.1; do not adapt the helper or the test).

Commit (`123-msg-B2.txt`): subject `perf(battle): v2.0.28 - read MCM settings once, not per blow or frame`. Body: battle
code looked up the Mod Options object on every read, and some reads run on every melee blow, every frame or every agent
stat update. The combat mechanics, blow diagnostics, Mixed Formations, Companion Tactics, Dread Aura, Smart Cavalry AI
and siege prop diagnostics providers now keep the object from their first successful lookup; a change in Mod Options
still applies at once, mid-battle included. Cleave, stagger immunity, crush-through and the race modifiers test the cheap
facts of a hit before reading a setting, the stance-cancel patch checks the player's team first, and the English text
override looks an id up without cutting a new string. Every result is unchanged. Trailers:
`Research: GlobalSettings<T>.Instance asks BaseSettingsProvider.GetSettings on every read (read on MCMv5 5.12.1; CountingMcm counts it on the pinned <MCMV>)`,
`Not-tested: in game, change a Combat Mechanics toggle, Smart Cavalry AI and Dread Aura in Mod Options during a field battle and see each apply`,
`Ported-from: 74e9172d42e7482453b6caa25112cab16f64242d`.

### Step B3: Part B gate

Run Build, Test, Docs. **Verify**: `Failed: 0`; `Skipped` unchanged from TA; the new test classes from B1 and B2 all ran.
Record totals as **TB**.

## Part C: campaign settings, the town market and the refuge mount

### Step C1: Cheaper campaign settings reads and filters (`423d948d8`, partial)

Source: `423d948d8` perf(campaign): v2.0.32 - cheaper campaign settings reads and filters. Keep everything except the
RealmBorders provider (1.5.x-only feature) and the pool-keep half of the culture market (`CultureItemPool.ContainsItem`
and its use), which serves the 1.5.x-only armour acquisition feature.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git cherry-pick -n 423d948d8; echo "exit=$?"; for p in Main/Features/RealmBorders/RealmBordersSettingsProvider.cs Main/Features/CultureMarketplace/Domain/CultureItemPool.cs docs/reviews/REVIEW-LOG.md docs/reviews/lessons/build-tooling-workflow.md docs/reviews/lessons/gamemodels-services.md docs/reviews/lessons/testing-qa.md docs/reviews/deep-review-037-campaign-hot-paths-2026-10-02.md docs/reviews/rca-campaign-hot-paths-2026-10-02.md; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; git diff --name-only --diff-filter=U
```

Expected remaining unmerged (simulated): `Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs`,
`docs/features/culture-marketplace.md`. When WP110 is merged also possible: `Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs`.
`AlignmentDesertionBehavior.cs` merges cleanly: keep the 1.4.x `MapEvent` guards (from `1b00d1acd`) AND the new
`ShouldEvaluate` gate. Any other unmerged path: STOP. Resolve:

1. `CultureMarketplaceMaintenanceService.cs`: `git checkout --ours`, then make these edits and nothing else:
   - add `using System.Linq;` after `using System.Collections.Generic;` and `using TAOM.Features.CultureMarketplace.Domain;` after `using TAOM.Adapters;`;
   - after the `_townAdapter` field add the `_routed` dictionary and the nested `RoutedSets` class exactly as in
     `git show 423d948d8:Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs` (from the line starting
     `// Per-culture routed sets` through the closing brace of `RoutedSets`), but drop the words `A pool indexes its own ids (CultureItemPool.ContainsItem).` from the comment;
   - replace the body of `EnsureGuaranteedStock` with the `423d948d8` body (one `_townAdapter.GetItemCounts(settlement, routed.GuaranteedIds)` call, then the loop over `routed.Guaranteed`);
   - in `FilterForeignCultureItems` replace the block from `var routedHere = _poolService.GetRoutedItemsForCulture(cultureId);`
     through the end of its `if (routedHere.Count > 0) { ... }` with `var routedIdsHere = RoutedFor(cultureId).Ids;`. Add NO `GetPool` call and NO pool check;
   - add the private `RoutedFor` method from `423d948d8` at the end of the class.
   **Verify**: `grep -c -E "GetPool|ContainsItem|pooledHere" Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs` prints `0`; `grep -c "GetItemCounts" ...` prints `1`.
2. `TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceMaintenanceServiceCacheTests.cs` (new): delete
   `FilterForeignCultureItems_ReadsNoPoolEntries_ThePoolIndexesItsOwnIds`, `CultureItemPool_ContainsItem_IsOrdinalAndCoversEveryEntry`,
   `FilterForeignCultureItems_NewPoolObject_UsesTheNewPoolsIds` and the nested `CountingEntries` class (and `using System.Collections;`
   if nothing else needs it). In `FilterForeignCultureItems_TwoCulturesOnOneService_KeepTheirOwnSets` delete both `GetPool` lines, use
   `var everyRow = new[] { "isen_routed", "lin_routed" };`, expect `1` removal per call, and delete every assertion naming
   `isen_pooled` or `lin_pooled`. In the class summary delete `asks each pool's own id index (CultureItemPool.ContainsItem), `.
3. `FieldCommissionSettingsProvider.cs` when conflicted: take ours and apply `423d948d8`'s cached `Settings` accessor and
   internal test constructor, keeping the base's (WP110's) setting names (`FieldCommissionMeritToPromote`, `FieldCommissionOffersPerBattle`).
4. `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` (new): delete `using TAOM.Features.RealmBorders;`, both
   `DataRow`s naming RealmBorders and the test `RealmBorders_ReadsThroughTheCachedSettings`. If WP110 is merged and the
   FieldCommission test sets `FieldCommissionMeritThreshold` or `FieldCommissionMaxOffersPerBattle`, use the new names.
5. Delete any `[TestCategory("RequiresGame")]` line in the files this step adds or edits (D9).
6. `docs/features/culture-marketplace.md`: ours, then: `GetItemCount` to `GetItemCounts` in the data-flow line and the Key
   Files row; add the `423d948d8` bullets for `CultureMarketplaceMaintenanceServiceCacheTests.cs` (7 tests on 1.4.x: the routed
   cache and the single walk) and `TownRosterAdapterGetItemCountsTests.cs`; the per-process bullet without the
   `CultureItemPool.ContainsItem` clause; replace the "Total: N tests across M classes" line with the count your Filtered run
   `CultureMarketplace|ItemPoolAdapter` reports; changelog line dated today, "ported by WP123".
7. `docs/features/mcm.md`: in the `CampaignHotPathSettingsProvidersTests.cs` line say seven providers (no Realm Borders) and the method count of the adapted file.
8. Line citations this step makes stale: in `docs/modding/file-catalogue.md` replace the line ranges after
   `AlignmentDesertionSettingsProvider.cs`, `CaravanTradeSettingsProvider.cs` (including its separate `:25` reference)
   and `CastleRecruitmentSettingsProvider.cs` with the getter names they cite; in `docs/modding/configs-balance.md`
   re-point `FieldCommissionSettingsProvider.cs:87` and `:104` to the lines `grep -n` now shows for the same getters.
   `git add` every resolved and new path.

**Verify**: unmerged list empty; Build exit 0; Filtered
`--filter "FullyQualifiedName~CampaignHotPath|FullyQualifiedName~AlignmentDesertion|FullyQualifiedName~CulturalFeats|FullyQualifiedName~TaomPartySpeed|FullyQualifiedName~CultureMarketplace|FullyQualifiedName~TownRosterAdapter|FullyQualifiedName~Refuge|FullyQualifiedName~FieldCommission|FullyQualifiedName~QuickActions|FullyQualifiedName~TimeAcceleration|FullyQualifiedName~CastleRecruitment|FullyQualifiedName~CaravanTrade|FullyQualifiedName~PartyIconScale"` `Failed: 0`;
`git grep -n "GetItemCount(" -- Main/Adapters/ITownRosterAdapter.cs Main/Adapters/TownRosterAdapter.cs Main/Features/CultureMarketplace TAOM.Tests/Features/CultureMarketplace` prints nothing (`IPartyItemRosterAdapter.GetItemCount` is a different adapter and stays); `git grep -n "RealmBorders" -- Main TAOM.Tests` prints nothing;
for each of `AlignmentDesertionSettingsProvider.cs`, `CaravanTradeSettingsProvider.cs`, `CastleRecruitmentSettingsProvider.cs`,
`FieldCommissionSettingsProvider.cs`, `QuickActionsSettingsProvider.cs`, `TimeAccelerationSettingsProvider.cs` and
`PartyIconScaleConfig.cs`, `grep -c -E "TaomSettings\.Instance\?\." <file>` prints `0` and `grep -c "??= TaomSettings.Instance" <file>` prints `1`.
Commit (`123-msg-C1.txt`): subject `perf(campaign): v2.0.28 - cheaper campaign settings reads and filters`. Body: campaign
code that runs per party per hour, per party and garrison per day, per caravan score or every map frame looked up Mod
Options on every read. Seven settings providers (castle recruitment, caravan trade, alignment desertion, battlefield
promotions, quick actions, time acceleration, map figure scale) now keep the object. Alignment desertion skips owners
that cannot lose troops before copying a roster, the refuge listener skips battles while the player has no refuge,
party speed walks a roster only for the Rohan infantry feat, and the culture market builds its routed item sets once and
counts a town's guaranteed items in one walk. Every recruitment, desertion, speed, price and stock result is unchanged.
Trailers: `Research: the culture market keeps 1.4.x behaviour: no pool-keep (1.5.x armour acquisition, 301cd338a)`,
`Not-tested: in game, Map Figure Scale and the fast-forward multiplier apply without a restart; a Free lord sheds Evil troops overnight; an Isengard town stocks its warg items`,
`Ported-from: 423d948d89f115ddcde6709ed9a8a1d23b88bb13`.

### Step C2: Refuge, supply-line and camp settings read once (`bb68eeca3` item 1)

Source: `bb68eeca3` "Refactor and enhance various components of TAOM" (69 files; mostly 1.5.x-only GeneratedLordKits,
CreatureBandits and SignatureStrikes). Method: file transplant of the three providers (identical on 1.4.x and the
parent) plus hand edits; do not cherry-pick.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git diff --quiet 1b00d1acd -- Main/Features/Refuge/RefugeSettingsProvider.cs Main/Features/SupplyLines/SupplyLinesSettingsProvider.cs Main/Features/FieldCamp/CampSettingsProvider.cs && echo SAME && git checkout bb68eeca3 Main/Features/Refuge/RefugeSettingsProvider.cs Main/Features/SupplyLines/SupplyLinesSettingsProvider.cs Main/Features/FieldCamp/CampSettingsProvider.cs TAOM.Tests/Features/Refuge/RefugeSettingsProviderTests.cs && grep -c "TaomSettings.Instance" Main/Features/Refuge/RefugeSettingsProvider.cs Main/Features/SupplyLines/SupplyLinesSettingsProvider.cs Main/Features/FieldCamp/CampSettingsProvider.cs
```

**Verify**: `SAME`, then `1` for each file. No `SAME`: STOP. Then by hand (the `bb68eeca3` hunks):
- `HotPathSettingsProvidersTests.cs`: add `using TAOM.Features.Refuge;`; add `[DataRow(typeof(RefugeSettingsProvider))]` to
  `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance` and to `Provider_AsksMcmUntilItHasTheSettings_ThenNeverAgain_AndReadsThrough`,
  `[DataRow(typeof(IRefugeSettingsProvider), typeof(RefugeSettingsProvider))]` to `Provider_ResolvesFromARealContainer`, and the
  `Probes` entry `[typeof(RefugeSettingsProvider)] = (() => new RefugeSettingsProvider(), nameof(IRefugeSettingsProvider.Enabled), nameof(TaomSettings.EnableRefuges), () => new TaomSettings()),`
  with the comment `// #745: read per hit on a refuge's defenders, real-time and auto-resolve.`
- `CampaignHotPathSettingsProvidersTests.cs`: add `using TAOM.Features.FieldCamp;` and `using TAOM.Features.SupplyLines;`;
  the `DataRow`s for `SupplyLinesSettingsProvider` and `CampSettingsProvider` (IL rule) and for their interfaces (container
  test); and the tests `SupplyLines_ReadsThroughTheCachedSettings` and `FieldCamp_ReadsThroughTheCachedSettings` from
  `git show bb68eeca3:TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`.
- `.claude/rules/csharp-architecture.md`, "Config Providers MUST Validate" list: after rule 7 add
  ``8. **A provider read on a hot path (per hit, per frame, per agent update, per campaign tick) takes the MCM object once** in a private lazy `Settings` accessor, reads through it, and joins `HotPathSettingsProvidersTests` (mission) or `CampaignHotPathSettingsProvidersTests` (campaign) in the same commit, because nothing else points a new provider at those gates.``
- Docs (the `bb68eeca3` lines): `docs/features/field-camp.md` (`CampSettingsProvider` takes the MCM object once, #746),
  `docs/features/supply-lines.md` (the same, #746), `docs/features/refuge.md` (the settings paragraph, #745),
  `docs/features/mcm.md` (the two test files' lines: ten campaign providers on 1.4.x, counting the NameplateCull row below; the mission file gains Refuge).
- NameplateCull rows (the `ecaa67d26` hunks of these two test files; WP120 left them to this step):
  - `CampaignHotPathSettingsProvidersTests.cs`: add `using TAOM.Features.BattleLoadDiagnostics;` and `using TAOM.Features.NameplateCull;`.
    Widen `IsInstanceGetter` as `ecaa67d26` does, to `m.Name == "get_Instance" && m.DeclaringType != null && (m.DeclaringType.IsAssignableFrom(typeof(TaomSettings)) || m.DeclaringType.IsAssignableFrom(typeof(BattleLoadDiagnosticsSettings)))`,
    because this provider reads the Battle Load Diagnostics page and the IL rule would not see its getter otherwise. Add
    `// Read every campaign-map frame, from the Battle Load Diagnostics page.` and `[DataRow(typeof(NameplateCullSettingsProvider))]` as the last DataRow of `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance`, and
    `[DataRow(typeof(INameplateCullSettingsProvider), typeof(NameplateCullSettingsProvider))]` as the last DataRow of `Provider_ResolvesFromARealContainer`.
  - `HotPathSettingsProvidersTests.cs`: add `using TAOM.Features.BattleLoadDiagnostics;` and `[DataRow(typeof(BattleLoadDiagnosticsSettings))]` to `CachedSettingsClasses_AreMcmGlobalSettings`.
  - Fold: move the tests from `TAOM.Tests/Features/NameplateCull/NameplateCullSettingsProviderTests.cs` into `CampaignHotPathSettingsProvidersTests.cs` where `ecaa67d26` puts them
    (`NameplateCull_ReadsThroughTheCachedSettings` above the `// No-MCM pins` comment, `NameplateCull_NoMcm_CullDefaultsOn` after `PartyIconScale_NoMcm_GetScaleIsTheDefault`), then `git rm -q -f TAOM.Tests/Features/NameplateCull/NameplateCullSettingsProviderTests.cs`.
  - `docs/features/nameplate-cull.md`: replace `` `NameplateCullSettingsProviderTests` pins that it reads through that reference (1.4.x has no hot-path IL gate until WP123 brings one). `` with
    `` the provider is in the hot-path settings gate (`CampaignHotPathSettingsProvidersTests`), so a regression to a per-frame `Instance` lookup fails a test. ``; replace every other `NameplateCullSettingsProviderTests` with `CampaignHotPathSettingsProvidersTests`;
    add the changelog line `- <today>: WP123 moved the settings-provider pins into the campaign hot-path gate.`
  - `docs/reference/harmony-patch-registry.md`, section `## Patch104_NameplateCull`: in its Tests list replace `NameplateCullSettingsProviderTests` with `CampaignHotPathSettingsProvidersTests`.
  `git add` the two test files, `docs/features/nameplate-cull.md` and the other paths this step edits.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~HotPathSettingsProviders|FullyQualifiedName~CampaignHotPath|FullyQualifiedName~Refuge|FullyQualifiedName~SupplyLines|FullyQualifiedName~FieldCamp|FullyQualifiedName~NameplateCull"` `Failed: 0`;
`git grep -n "NameplateCullSettingsProviderTests" -- Main TAOM.Tests docs` prints nothing; `grep -c "NameplateCull" TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` prints `7` (the using, two DataRows, four lines of the two moved tests).
Commit (`123-msg-C2.txt`): subject `perf(campaign): v2.0.28 - refuge, supply and camp settings read once`. Body: the
refuge defence bonuses are read on every hit against a refuge's defenders, and the supply-line and field-camp switches
every campaign frame; their providers now keep the Mod Options object and read through it, still clamped. Trailers:
`Not-tested: in game, change the Refuge defence bonus and the Supply Lines switch in Mod Options and see each apply`,
`Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93`, `Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7`.

### Step C3: A refuge defender's mount shares the reduction (`bb68eeca3` item 2, #741)

`ls Main/Features/Refuge/Hooks/RefugeDamageHooks.cs 2>&1` must say "No such file or directory"; if the file exists: STOP (WP127 depends on this WP and cannot have merged first).

Create `Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` from
`git show bb68eeca3:Main/Features/Refuge/Hooks/RefugeDamageHooks.cs`, then rewrite its summary for 1.4.x: cite v1.4.8
`PartyAgentOrigin.cs:48` and `PartyGroupAgentOrigin.cs:31` (same lines on v1.4.8); delete the howdah and mumak crew
sentences and the `#737` "moved out of" words; replace the v1.5.4 `Mission.cs:4219,4608-4618` citation with
`v1.4.8 AttackInformation.cs:255, 280, 286`. In `TaomCombatMechanicsModel.ApplyDamageReductions` replace
`var reduction = _refugeDefense?.DefenderDamageReduction(VictimPartyId(attackInformation.VictimAgentOrigin)) ?? 0f;` and the
`result = RefugeDamageReduction.Apply(result, reduction);` line (with its two comment lines) with
`result = Features.Refuge.Hooks.RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result);`, and delete the
private `VictimPartyId` method (remove `using TaleWorlds.CampaignSystem.AgentOrigins;` if nothing else uses it).
Also `IRefugeDefenseService.DefenderDamageReduction(string? partyStringId)` and the same `string?` in
`RefugeDefenseService` (annotation only).

Tests: create `TAOM.Tests/Features/Refuge/RefugeDamageHooksTests.cs` from
`git show bb68eeca3:TAOM.Tests/Features/Refuge/RefugeDamageHooksTests.cs`. Delete
`using TAOM.Features.Elephant;`, `using TAOM.Features.Mumakil;`, the tests `Reduce_HowdahCrewOfARefugeParty_GetsTheReduction`
and `Reduce_MumakCrewOfARefugeParty_GetsTheReduction`, the `[TestCategory("RequiresGame")]` line, the crew words in the
summary and the `v1.5.4 PartyBase.cs:123` citation (v1.4.8: `PartyBase.cs:122`). Keep the 8 other tests (no service,
a quarter reduction, NaN, mount of a refuge rider, riderless mount, foot victim reads its own origin, another party, no party).
Docs: `docs/features/refuge.md`: add after the sentence ending `drifted from real-time under vanilla factors)` the
sentence `Real-time finds the victim's party through its origin's BattleCombatant (a SimpleAgentOrigin hero's own party
included); a mount has no origin of its own, so a mount hit counts for its rider's party: a defender's horse shares the
refuge, and a riderless mount gets nothing (#741).`, and a Key Files row for
`Hooks/RefugeDamageHooks.cs`.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~Refuge|FullyQualifiedName~CombatMechanicsModelInvariants|FullyQualifiedName~GameModelOverrideBinding"` `Failed: 0`;
`grep -c "IsVictimAgentMount" Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` prints `1`;
`grep -c "VictimAgentOrigin)" Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` prints `0`.
Commit (`123-msg-C3.txt`): subject `fix(refuge): v2.0.28 - a refuge defender's mount shares the reduction`. Body: a
refuge's defenders take less damage, but their horses did not: the refuge code looked at the struck agent's own origin,
and a mount has none. A mount hit now counts for its rider's party, as the career passives already do, and a riderless
mount gets nothing. Trailers: `Research: taom-src v1.4.8 AttackInformation sets VictimRiderAgentOrigin from the rider (AttackInformation.cs:286); PartyAgentOrigin.BattleCombatant => Party (:48)`,
`Not-tested: in game, a refuge defence with a mounted defender (Blow Diagnostics shows the horse taking reduced damage against the MCM bonus at 0)`,
`Ported-from: bb68eeca3aeee49ba95ea4956f2b3941820a3f93`.

### Step C4: Part C gate

Run Build, Test, Docs. **Verify**: `Failed: 0`; `Skipped` unchanged from TB. Record **TC**.

## Part D: crash bundles, the roster-loss trace and the logger measurement

### Step D1: Crash bundles list only TAOM's settings (`13fb30f9c` client half, `a59a48bb6`)

Sources: `13fb30f9c` feat(coop): v2.0.35 - TAOM server settings file for dedicated servers (take only the four `Instance`
hides, client form, and the Dropdown rendering); `a59a48bb6` fix(crashreport): v2.0.35 - crash bundles read only TAOM's
settings. Method: hand edits plus a file transplant of the collector series end state (the 1.4.x collector equals the
parent of `13fb30f9c`).

1. In each of `Main/Features/TaomSettings.cs`, `Main/Features/CrashReport/CrashReportSettings.cs`,
   `Main/Features/BlowDiagnostics/BlowDiagnosticsSettings.cs` and `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`,
   directly after the class's opening brace, add (with `X` the class name):
   ```csharp
    // Declared here so the crash collector, which reads only an Instance the class declares itself (#982), finds
    // TAOM's settings. Forwards to MCM's own; name the base explicitly: a bare Instance here would recurse.
    public static new X? Instance => GlobalSettings<X>.Instance;
   ```
2. Collector and its tests:

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git diff --quiet 1b00d1acd -- Main/Features/CrashReport/Collectors/McmSettingsCollector.cs && echo SAME && git checkout a59a48bb6 Main/Features/CrashReport/Collectors/McmSettingsCollector.cs TAOM.Tests/Features/CrashReport/McmSettingsCollectorTests.cs && grep -c -E "BindingFlags.DeclaredOnly|asm != ownAssembly|\"DiscoveryType\"|internal static string StringifyValue" Main/Features/CrashReport/Collectors/McmSettingsCollector.cs
```

   **Verify**: `SAME` then `4`. No `SAME`: STOP.
3. `docs/features/crash-report.md`: the MCM Settings row and the `McmSettingsCollectorTests` bullet (8 tests) as in
   `a59a48bb6`, plus a changelog line: "TAOM's settings appear in bundles (the four classes declare their own `Instance`);
   third-party settings classes are not read (#982); dropdowns show their selected value". Do not mention a dedicated server.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~McmSettingsCollector|FullyQualifiedName~SettingsFingerprint|FullyQualifiedName~HotPathSettingsProviders|FullyQualifiedName~CampaignHotPath|FullyQualifiedName~BattleBalanceSettingsProvider"` `Failed: 0`
(the IL rules still find the getter: its declaring type is now `TaomSettings`, which `IsInstanceGetter` accepts);
`git grep -n -E "ServerSettingsSource|taom-server-settings" -- Main TAOM.Tests` prints nothing; MCM default gate `exit=0` (if present).
Commit (`123-msg-D1.txt`): subject `fix(crashreport): v2.0.28 - crash bundles list only TAOM's settings`. Body: crash
bundles were meant to list TAOM's Mod Options values but listed none, and the collector read every mod's settings
classes during crash handling, which can run another mod's code at the worst moment and copy its free-text settings into
a file players upload. Bundles now list TAOM's four settings pages, skip other mods' settings by design (the bundle
already names every mod and version), and show a dropdown's selected value. Trailers:
`Research: Dropdown<T>.SelectedValue indexes the list by SelectedIndex and throws out of range, handled (read on MCMv5 5.12.1; the pinned version is <MCMV>)`,
`Rejected: the 13fb30f9c dedicated-server settings source and fingerprint float bits (co-op, D7)`,
`Not-tested: in game, trigger the QA crash and read the bundle's MCM Settings section`,
`Ported-from: 13fb30f9c9579c3f8cb3ecaf87a0a38f63aa34fc`, `Ported-from: a59a48bb6a8464f854e56c4c5a4964fcb602c409`.

### Step D2: Trace main-party troop removals (`36d4dda14`, TEMPORARY diagnostic)

Source: `36d4dda14` feat(troopweight): v2.0.34 - trace main-party troop removals. Elite Emissary exists on 1.4.x; the
diagnostic is still present and unresolved at the 1.5.x tip. Two observe-only prefixes write DEBUG lines (queued, no
synchronous flush) only for non-hero removals from the main party.

```bash
cd /c/Users/mikew/source/repos/taom-port/123 && git cherry-pick -n 36d4dda14; echo "exit=$?"; for p in docs/reference/feature-map.md docs/reviews/lessons/harmony-il.md docs/reviews/rca-main-roster-loss-diag-2026-10-17.md; do if git cat-file -e HEAD:"$p" 2>/dev/null; then git checkout HEAD "$p"; else git rm -q -f --ignore-unmatch -- "$p"; fi; done; git diff --name-only --diff-filter=U
```

Expected remaining unmerged (simulated): `Main/SubModule.cs`, `docs/features/troop-weight-system.md`,
`docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`. Any other: STOP. Resolve:
1. `Main/SubModule.cs`: ours, then directly after `        TryPatchCategory("Patch17_TroopWeight");` insert
   ```csharp
        // TEMPORARY: logs the caller of every non-hero removal from the main party roster (player report
        // 2026-10-17, troops vanished after an emissary purchase). Remove when that loss is root-caused.
        TroopRoster_AddToCountsAtIndex_Patch.Initialize(IoC.Resolve<IModLogger>());
        TryPatchCategory("Patch_MainRosterLossDiag");
   ```
   (`TryPatchCategory("Patch17_TroopWeight")` missing: STOP, WP104 is not merged.)
2. `troop-weight-system.md`: ours plus the `36d4dda14` additions (the "Main-roster loss diagnostic (TEMPORARY, 2026-10-17)"
   section, the Diagnostics Key Files row, the test bullet).
3. `harmony-patch-registry.md`: ours plus the new `## Patch_MainRosterLossDiag` section. Re-cite for v1.4.8: the targets are
   `TroopRoster.cs:312` and `:494`; keep the PatchShield paragraph only if `Dependencies/Foundation/PatchShield.cs` still wraps
   patched engine methods (read it; if a later WP changed that, adapt the sentence).
4. `patch-targets.md`: ours plus two rows, `TAOM.Features.TroopWeight.Diagnostics.TroopRoster_AddToCountsAtIndex_Patch` |
   `TaleWorlds.CampaignSystem.Roster.TroopRoster.Int32 AddToCountsAtIndex(Int32 index, Int32 countChange, Int32 woundedCountChange, Int32 xpChange, Boolean removeDepleted)`
   and `...TroopRoster_SetElementNumber_Patch` | `TaleWorlds.CampaignSystem.Roster.TroopRoster.Void SetElementNumber(Int32 index, Int32 number)`, in
   alphabetical position; raise `Patches: N.` by 2. Replace any `v1.5.5` decompile citation in the two patch files' comments with v1.4.8.
   `git add` all.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~MainRosterLossDiagnostics|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~PatchCategory|FullyQualifiedName~TroopWeight"` `Failed: 0`
(if `TAOM.Tests/Composition/PatchCategoryNumberTests.cs` exists from WP122 it is in that filter and must pass);
`grep -c "v1.5" Main/Features/TroopWeight/Diagnostics/*.cs` prints `0` for each file.
Commit (`123-msg-D2.txt`): subject `feat(troopweight): v2.0.28 - trace main-party troop removals`. Body (players): a player
lost about 60 troops right after buying elite units from the faction emissary, and no TAOM code removes a whole party. Until
the cause is found, the TAOM debug log gets one [RosterLoss] line each time regular troops leave your party, naming the troop,
the count and the code that removed them; nothing in game changes. If you lose troops without a reason, send the taom_debug
log. Trailers: `Research: taom-src v1.4.8 TroopRoster.AddToCountsAtIndex (TroopRoster.cs:312) and SetElementNumber (:494)`,
`Not-tested: in game, buy Elite Emissary troops and confirm [RosterLoss] DEBUG lines appear only for main-party regulars`,
`Ported-from: 36d4dda14001262cb5660e84c7bae79e13f6883a`.

### Step D3: Measure what a durable log line costs, and decide (RUNTIME-PERF-09; new work)

The binding note for this WP: decide the synchronous `FileLogger` flush with a measurement; a bounded background writer
is new work, kept only if it measurably helps. Existing evidence bounds it from above: in E4 the 534-agent arena load
wrote 1,602 INFO lines between `t=+109604ms` and `t=+110316ms` (E4 lines 3456 to 5135), 0.44 ms per line including
all engine work; `docs/features/battle-load-diagnostics.md` "Performance" records 145 ms for 429 agents (three flushes each)
including engine work. A background writer would also reopen #350: INFO lines are synchronous so a native crash cannot lose them.

1. Create `TAOM.Tests/Core/Logging/FileLoggerDurableWriteBenchmarkTests.cs` (if WP117 already added the
   `TAOM_RUN_BENCHMARKS` gate pattern, keep this file's gate identical to it):

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// RUNTIME-PERF-09 (WP123): what one durable INFO line costs the thread that logs it. FileLogger drains INFO, WARNING
/// and ERROR on the caller (WriteLine plus Flush under one lock) so a native crash cannot lose the line; DEBUG only
/// enqueues. The line is shaped like a per-agent [BattleLoad] phase, the largest INFO stream left once auto-resolve
/// logging is gone (E4: 1,602 such lines in one arena load). Opt-in (TAOM_RUN_BENCHMARKS=1).
/// </summary>
[TestClass]
public class FileLoggerDurableWriteBenchmarkTests
{
    private const int Lines = 4000;
    private const int ArenaBurst = 1602;
    private const string Line =
        "[BattleLoad] seq=1234 t=+109604ms phase=AgentEquipOk agent#17 'Townsman' char='townsman_gondor' culture='gondor' race='human'";

    private string _testDir = null!;
    private string _originalDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TAOM_LogBench_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_testDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.SetCurrentDirectory(_originalDir);
        try { Directory.Delete(_testDir, true); } catch { /* best effort */ }
    }

    private static double MicrosPerLine(Action body, int lines)
    {
        var watch = Stopwatch.StartNew();
        body();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds * 1000.0 / lines;
    }

    [TestMethod]
    [TestCategory("Benchmark")]
    public void DurableInfoLine_CostOnTheLoggingThread()
    {
        if (Environment.GetEnvironmentVariable("TAOM_RUN_BENCHMARKS") != "1")
            Assert.Inconclusive("Benchmark: set TAOM_RUN_BENCHMARKS=1");

        var logger = new FileLogger();
        double info, debug, contended;
        try
        {
            for (int i = 0; i < 200; i++) logger.LogInfo(Line); // warm-up: JIT, file open, OS cache
            info = MicrosPerLine(() => { for (int i = 0; i < Lines; i++) logger.LogInfo(Line); }, Lines);
            debug = MicrosPerLine(() => { for (int i = 0; i < Lines; i++) logger.LogDebug(Line); }, Lines);
            contended = MicrosPerLine(() => Parallel.For(0, 4, _ => { for (int i = 0; i < Lines / 4; i++) logger.LogInfo(Line); }), Lines);
        }
        finally
        {
            logger.Dispose();
        }

        Console.WriteLine(
            "[LogBench] INFO {0:0.0} us/line, DEBUG {1:0.0} us/line, INFO on 4 threads {2:0.0} us/line; " +
            "a {3}-line arena burst costs {4:0} ms durable, {5:0} ms queued",
            info, debug, contended, ArenaBurst, info * ArenaBurst / 1000.0, debug * ArenaBurst / 1000.0);

        var written = File.ReadAllLines(Directory.GetFiles(Path.Combine(_testDir, "Logs")).Single()).Length;
        Assert.AreEqual(200 + 3 * Lines, written, "every line reached the file");
    }
}
```

2. Build. Run Test (the default run): the new test is reported as skipped (Inconclusive). Run the Benchmark command three
   times and keep the median `INFO` figure (game closed, laptop on mains power).
3. Decide by the median INFO figure:
   - **At most 50 us per line** (a 1,602-line burst costs at most about 80 ms on this laptop): REJECT the bounded
     background writer. Add a bullet to `docs/features/battle-load-diagnostics.md` "## Performance":
     `- **Measured <date> on the maintainer's laptop (WP123): a durable INFO line costs <X> us on the calling thread, <Y> us when four threads log at once, and a DEBUG line <Z> us. A 1,602-line arena load burst therefore costs about <A> ms. The synchronous INFO path stays: a background writer would save at most that much per large load and would lose the last lines before a native crash (#350). Re-measure: TAOM_RUN_BENCHMARKS=1 and FileLoggerDurableWriteBenchmarkTests.**`
   - **Above 50 us per line**: commit the benchmark with the numbers in the body and STOP after this step's commit:
     report the numbers. Do not write a background writer; a follow-up WP chooses between demoting the per-agent
     `[BattleLoad]` phases to DEBUG and a writer that keeps phase markers durable.

**Verify**: Build exit 0; Test `Failed: 0` and `Skipped` = TC's skipped + 1; the Benchmark command passes and prints one
`[LogBench]` line.
Commit (`123-msg-D3.txt`): subject `test(logging): v2.0.28 - measure what a durable log line costs`. Body: records the
three measured figures and the decision in plain words (no player-facing change). Trailers:
`Research: FileLogger drains INFO, WARNING and ERROR on the caller (FileLogger.cs:79-130)`, `Rejected: a bounded background writer (<reason with the numbers>)` (REJECT case only),
`Not-tested: the cost on players' disks under 80 to 96 percent memory load (E4 memLoad) is unmeasured`.

### Step D4: WP gate and hand-off

Run Restore, Build, Test, Tool tests and Docs. **Verify**: `Failed: 0`; `Skipped` = T0's + 1;
`git log --format=%s bannerlord-1.4.5..HEAD | grep -c "v2.0.28 - "` prints `12`; `git status --porcelain` prints nothing.
This step makes no commit.

Hand-off (do not edit `plans/README.md`; the integrator records these after the merge): the WP123 status; the 12 commit
hashes; `Total`, `Passed` and `Skipped` from this run, plus T0, TA, TB and TC; the three Step D3 figures and the decision.
Name the old-plan row the integrator should update: plan 003 becomes `DONE (WP123: PERF-04 ported from 1.5.x plan 003; PERF-01 and PERF-07 go to WP124 with plans 033 and 015; PERF-03 is obsolete, its TroopWeight targets no longer exist)`.

## Disposition table

| 1.5.x commit | Subject | Owner group | Disposition |
|---|---|---|---|
| `7feca96be` | perf(battlebalance): v2.0.30 - read the MCM settings once per process | plan-003 | Transplanted (series end state), Step B1 |
| `02157b18b` | fix(battlebalance): v2.0.30 - review follow-ups for plan 003 | plan-003 | Transplanted (end state), Step B1; review records and lessons dropped (WP142) |
| `ab70555db` | fix(battlebalance): v2.0.30 - convergence fixes for plan 003 | plan-003 | Transplanted (end state), Step B1; test comment re-pointed to the 1.4.x registration call |
| `cf6e06aa5` | docs(reviews): v2.0.30 - Codex prompt for plan 003 | plan-003 | Skipped: 1.5.x review record (the 1.4.x port gets its own review) |
| `aee172900` | docs(changelog): v2.0.30 - cite #664 for plan 003 | plan-003 | Skipped: CHANGELOG only (D1) |
| `40615c226` | merge(improve): plan 003 (#664) | plan-003 | Not needed: no resolution on the transplanted files (verified empty diff) |
| `e0ad0db76` | perf(diagnostics): v2.0.32 - cheaper always-on mission diagnostics | plan-030 | Ported partially, Step A3: census, Patch35 OnTick deletion, colour store deletion, FeatureModulesTests anchor (needed after WP105, unlike the pack note). Skipped: CareerSystem tallies (needs 1.5.x-only #613), CreatureBandits and TrollBruteForce (1.5.x-only), engine doc (WP144), records |
| `74e9172d4` | perf(battle): v2.0.32 - read MCM settings once, not per blow or frame | plan-031 | Ported partially, Step B2. Skipped: CultureDoctrine and Howdah providers (absent); the Mixed Formations cycle-hotkey parse cache and stricter key rule (decided: the per-frame saving is one Trim and one Enum.TryParse, the strict rule needs `EnumNames` which WP133 ports and changes player-visible behaviour; not porting either: the cache alone is not worth a separate change); module-dependencies.md (WP118 owns that doc) |
| `bb68eeca3` | Refactor and enhance various components of TAOM | plan-031 | Item 1 (Refuge, SupplyLines, Camp cached providers, CountingMcm, test rows, rule line) Step C2 and B2; item 2 (#741 mount branch) Step C3. Skipped: item 3 `lord_template_rosters.xslt` (a data fix; D13: plan 134's disposition row for `bb68eeca3` item 3 tests it on 1.4.x data and ports or skips it), GeneratedLordKits, CreatureBandits, SignatureStrikes, #742 console change, `plans/043` (a 1.5.x lead), records |
| `ecaa67d26` (shared hunk) | feat(stability): v2.0.34 - mission start, freeze and map guards | (WP109, WP120) | Test hunks only, Step C2: the two `NameplateCullSettingsProvider` DataRows, the `BattleLoadDiagnosticsSettings` row, the `IsInstanceGetter` widening and the two tests, folded from WP120's `NameplateCullSettingsProviderTests`. Every other hunk: WP109 or WP120 |
| `423d948d8` | perf(campaign): v2.0.32 - cheaper campaign settings reads and filters | plan-037 | Ported partially, Step C1. Skipped: RealmBorders provider (1.5.x-only) and the culture-pool keep (`CultureItemPool.ContainsItem`, 1.5.x armour acquisition `301cd338a`) |
| `36d4dda14` | feat(troopweight): v2.0.34 - trace main-party troop removals | troopweight-roster-loss-diag | Ported, Step D2 (TEMPORARY; remove on both lines together). RCA and lessons dropped (WP142) |
| `13fb30f9c` | feat(coop): v2.0.35 - TAOM server settings file for dedicated servers | crashreport-mcm-settings-collector | Adapted, Step D1: the four client-form `Instance` hides and the Dropdown rendering only. Skipped: ServerSettings, FeatureModules, provider comment edits, ModuleData configs, fingerprint float bits (D7) |
| `a59a48bb6` | fix(crashreport): v2.0.35 - crash bundles read only TAOM's settings | crashreport-mcm-settings-collector | Transplanted (collector series end state), Step D1. Skipped: coop-interop.md, server-settings.md, Codex prompts, RCAs |
| `890fe29de` | fix(enlistment): v2.0.35 - the enlistment trace ships off again | issue-840 | Ported, Step A2, with the WP110 allowlist entry removed |
| `158408c73` | fix(logging): v2.0.35 - auto-resolved battles are no longer logged | autoresolve-diagnostics-removal | Ported, Step A1 (end state of its doc; server-settings.md skipped, 1.5.x-only) |
| `6ae924638` (shared hunk) | Add tests for NRE containment finalizers and improve documentation | autoresolve-diagnostics-removal | Auto-resolve removal hunks only, Step A1 (incl. CoopSettingsRelevance -2 names, fingerprint -2). Other hunks of `6ae924638`: WP112, WP119, WP129, WP137; Fire and Tribute 1.5.x-only |
| `43ca92ee9` | fix(perf): v2.0.32 - integration fixups and review for the perf plans | (WP117) | Not here: it touches no plan 030 code file (only `squash/030.txt` records) |
| `f75288ebf` | fix(mission): v2.0.30 - park off-thread callback writes (#634) | (WP106) | Not here: its off-thread reports at WARNING belong to WP106 (RUNTIME-PERF-09's third cost) |

Deep-dive findings owned here: **RUNTIME-PERF-06** (Step A1); **RUNTIME-PERF-07** (Step A3; plan 030 items B, C, D are
1.5.x-only); **RUNTIME-PERF-08** (Steps B1, B2; plan 003's PERF-01 and PERF-07 go to WP124, PERF-03 is obsolete);
**RUNTIME-PERF-09** (Steps A1, D3; f75288ebf's part is WP106's); **RUNTIME-PERF-12** (Step C1; caravan distance reuse
was dropped in the 1.5.x review and is not done; the HasFeat shield tax is WP107's plan 034); **RELEASE-INTEGRITY-06**
(fix 1 Step A3, fix 2 Step A1, fix 3 decided by Step D3's measurement, fix 4 the `tLoad=` token is WP110's). Binding
notes: the `[MissionDiag]` DivideByZero is Step A4; Step D4's hand-off asks the integrator to close the plan 003 row.

## Test plan

- New or ported test classes, by step (method counts at the source commit): A2 `EnlistmentDiagnosticsSettingsProviderTests` (6, was 5);
  A3 `MissionDiagnosticBehaviorTests` (3), `MissionDiagnosticServiceTests` (7); A4 two `CampaignContextFormatterTests`, two
  `MissionDiagnosticServiceTests`, `MissionDiagnosticWiringTests` (1); B1 `BattleBalanceSettingsProviderTests` (10); B2
  `BlowDiagnosticsSettingsProviderTests` (3), `CombatMechanicsSettingsProviderTests` (5 on 1.4.x), `CompanionTacticsSettingsProviderTests` (3),
  `DreadAuraSettingsProviderTests` (2), `IdSliceTests` (5), `MixedFormationsSettingsProviderTests` (5),
  `SiegePropDiagnosticsSettingsProviderTests` (4), `SmartCavalryAISettingsProviderTests` (2), `HotPathSettingsProvidersTests`
  (4 methods, data rows for seven providers), additions to `CreatureCombatServiceTests`, `CrushThroughServiceTests`,
  `RaceCombatModifiersResolverTests`, `SharedMovementOrderPostfixTests`, `MBTextManager_GetLocalizedText_PatchTests`;
  C1 `CampaignHotPathSettingsProvidersTests` (11 after removing RealmBorders), `AlignmentDesertionBehaviorTests` (3),
  additions to `AlignmentDesertionServiceTests`, `CulturalFeatsServiceTests`, `TaomPartySpeedModelTests` (3),
  `CultureMarketplaceMaintenanceServiceCacheTests` (7), `TownRosterAdapterGetItemCountsTests` (4), the rewritten
  `CultureMarketplaceMaintenanceServiceGuaranteedStockTests`, `RefugeCampaignBehaviorTests`; C2 `RefugeSettingsProviderTests` (3)
  and the new rows (`CampaignHotPathSettingsProvidersTests` reaches 15 with the two tests folded from `NameplateCullSettingsProviderTests`); C3 `RefugeDamageHooksTests` (8); D1 `McmSettingsCollectorTests` (8); D2 `MainRosterLossDiagnosticsTests` (10);
  D3 the opt-in benchmark (1, skipped by default).
- Removed: the 5 auto-resolve test files (56 methods) and `AgentColorStoreTests` (4); `NameplateCullSettingsProviderTests` (2, moved in Step C2, no net change).
- Structurally untestable (name them in `Not-tested:`): live Harmony invocation of the TroopRoster prefixes, the MCM page,
  the census on real agents, crash-bundle output in a running game, disk cost on players' machines.
- Patterns to follow: `TAOM.Tests/Core/Logging/FileLoggerTests.cs` (temp directory), `BattleBalanceSettingsProviderTests` (read-through and IL rule).

## Done criteria

Part A
- [ ] Build exit 0; Test `Failed: 0`, `Skipped` = T0's.
- [ ] `git grep -n -E "AutoResolveDiagnostics|LogAutoResolve|MapEventBattleLogAdapter|TroopCensusAdapter|analyze_battle_logs" -- Main TAOM.Tests tools` prints nothing.
- [ ] `git grep -n -E "IAgentColorStore|AgentColorStoreCleanupBehavior|Patch35_Mission_OnTick|\.EnableEnlistmentDiagnostics" -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -n "DescribeBeforeClock" -- Main/Features/MissionDiagnostic` prints 2 or more lines; `grep -c "LogCampaignDate" Main/SubModule.cs` prints `1`.
Part B
- [ ] Build and Test as above; for `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs` and the seven Step B2 providers,
  `grep -c -E "(TaomSettings|BlowDiagnosticsSettings)\.Instance\?\." <file>` prints `0` and `grep -c -E "\?\?= (TaomSettings|BlowDiagnosticsSettings)\.Instance" <file>` prints `1`.
- [ ] `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs` and `TAOM.Tests/Infrastructure/CountingMcm.cs` exist and pass.
Part C
- [ ] Build and Test as above; `git grep -n "GetItemCount(" -- Main/Adapters/ITownRosterAdapter.cs Main/Adapters/TownRosterAdapter.cs Main/Features/CultureMarketplace TAOM.Tests/Features/CultureMarketplace` prints nothing; `grep -c "IsVictimAgentMount" Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` prints `1`.
- [ ] `git grep -n -E "GetPool|ContainsItem" -- Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs` prints nothing.
- [ ] The seven Step C1 providers and the three Step C2 providers each show `0` direct `TaomSettings.Instance?.` reads and `1` `??= TaomSettings.Instance`.
- [ ] `git grep -n "NameplateCullSettingsProviderTests" -- Main TAOM.Tests docs` prints nothing and `CampaignHotPathSettingsProvidersTests` carries the NameplateCull rows.
Part D and the whole WP
- [ ] Build exit 0; Test `Failed: 0`, `Skipped` = T0's + 1 (the benchmark).
- [ ] `git grep -n -E "public static new (TaomSettings|CrashReportSettings|BlowDiagnosticsSettings|BattleLoadDiagnosticsSettings)\? Instance" -- Main` prints 4 lines; `git grep -n "ServerSettingsSource" -- Main` prints nothing.
- [ ] `grep -c 'TryPatchCategory("Patch_MainRosterLossDiag")' Main/SubModule.cs` prints `1`.
- [ ] The D3 decision is recorded (doc bullet, or STOP report with numbers).
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD | grep -c "v2.0.28 - "` prints `12`; no commit stages `CHANGELOG.md` (`git log --name-only bannerlord-1.4.5..HEAD | grep -c "^CHANGELOG.md$"` prints `0`).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0 (or as recorded at Step A0); `git status --porcelain` is empty; `git diff --name-only bannerlord-1.4.5..HEAD -- plans/README.md` prints nothing; the Step D4 hand-off is written.

## STOP conditions

- A precondition in Step A0 fails (WP104, WP105, WP117 or WP120 not merged; an MCM pin other than 5.12.1 or 5.12.3; `RepoPaths.ReadSource` or `TryPatchCategory` absent).
- A cherry-pick leaves an unmerged path outside the step's expected list, or a `git diff --quiet 1b00d1acd -- ...` guard
  does not print `SAME` (the base drifted under a transplant).
- An excerpt this plan edits is missing or reads differently (for example the colour-store lines in `SubModule.cs`, the
  `LogAutoResolveTroopCensus` block, `"EnableEnlistmentDiagnostics"` in `CoopSettingsRelevance.cs`).
- The anchor in Step A3 item 2 cannot be placed on a line that occurs once before the hook.
- A step seems to need a ModuleData, `Dependencies/`, `Main/TAOM.csproj`, `.claude/settings*.json`, `Directory.Build.props`
  or `CHANGELOG.md` edit (D20, D1).
- An MCM test fails with `FileNotFoundException` or `TypeLoadException` for `MCMv5` or `MCM.Abstractions` (environment; report it).
- `CountingMcm.cs` does not compile or run against MCMV, or the counting test fails on its lookup count (Step B2).
- `Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` already exists at Step C3.
- A `CultureMarketplaceMaintenanceServiceFilterTests` or `GuaranteedStockTests` test fails after Step C1 (a stock decision changed).
- The MCM default gate reports a FLIP, UNPARSED or stale entry after Steps A1, A2 or D1 that the step did not predict.
- A test gate added by WP122 fails on a file this WP touched and the fix is not a mechanical rename or row.
- A TaleWorlds signature differs from "Engine facts" (do not decompile and improvise; report).
- Step D3 measures above 50 us per line (stop after the D3 commit and report).
- A verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Log: `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_<timestamp>.log`.
1. Load a save, play 20 minutes on the map: no `[AutoResolve]` line; Mod Options has no Auto-Resolve Diagnostics group;
   the save loads with no error. Compare the log size with an E4-like session (expect roughly 39 percent smaller).
2. The session snapshot shows `[MissionDiag] Campaign: date=<logged at game init>, MainHero=...`, then
   `[MissionDiag] Campaign at game init: now=<date>, ...`; no `DivideByZeroException`.
3. Enlisted session: Mod Options shows "Enable Enlistment Trace" off; no routine `[EnlistDiag]` TICK or SYNC lines.
4. First battle: `[MissionDiag] ActionSet census open` and `... closed: agentChecks=... newKeys=... lines=...`; the
   `ActionSet '...'` lines as before; clan-coloured troops keep their armour tint; OOB preset buttons work.
5. During a field battle change a Combat Mechanics toggle, Smart Cavalry AI, Dread Aura radius and Mixed Formations in Mod
   Options: each applies without a restart. Change a Battle Balance slider on the map: auto-resolve follows it.
6. Map: Map Figure Scale and the fast-forward multiplier apply at once; an AI lord recruits in a castle; a Free lord's
   party sheds Evil troops overnight; a Rohan infantry party is slowed; an Isengard town keeps its warg items in stock.
7. Refuge defence with a mounted defender and Blow Diagnostics on: the horse takes reduced damage (compare MCM bonus 0).
8. Trigger the QA crash (Crash Report, Dev Triggers): the bundle's MCM Settings section lists TAOM's four pages and no
   other mod's; dropdowns show their selected value.
9. Buy Elite Emissary troops: `[RosterLoss]` DEBUG lines appear only for main-party regulars.

## Maintenance notes

- **Later WPs that edit what this WP creates**: WP125 and WP131 edit `CampaignHotPathSettingsProvidersTests.cs`;
  WP124 builds on the cached providers (plan 032 reads `MixedFormationsSettingsProvider`); WP135's `407b1e767` needs `IdSlice`;
  WP127 depends on this WP and must keep `RefugeDamageHooks.Reduce(..., in AttackInformation, ...)` with the mount branch
  (`RefugeDamageHooksTests` fails otherwise); WP130 re-anchors the FeatureModulesTests hook when it changes the
  BattleActionBar registration.
- **Renamed or removed symbols** later ports must not resurrect: `TaomSettings.EnableEnlistmentDiagnostics` (now
  `EnableEnlistmentTrace`), `LogAutoResolvedBattles`, `LogAutoResolveTroopCensus`, `ITownRosterAdapter.GetItemCount` (now
  `GetItemCounts`), `IAgentColorStore`, `Patch35_Mission_OnTick`.
- **Reviewer focus** (`/deep-review` before merge): DryIoc resolution with two constructors (tests cover it); every cached
  accessor caches only a non-null instance and no constructor reads `Instance`; the culture market stayed routed-only;
  the `Instance` hides forward to `GlobalSettings<X>.Instance` (a bare `Instance` would recurse); the refuge mount branch.
- **Not porting**: the strict Mixed Formations hotkey rule and its parse cache (the cache alone is not worth a separate change, and the rule changes player-visible behaviour).
- **Deferred**: the lord-template roster XSLT
  (plan 134's disposition row for `bb68eeca3` item 3, D13); demoting per-agent `[BattleLoad]` lines (only if Step D3 escalates). Remove the roster-loss trace on
  both lines together once the report is root-caused (checklist in its registry section).
- **Release**: no TAOM.Dependencies change, so no #371 pairing is needed for this WP.
