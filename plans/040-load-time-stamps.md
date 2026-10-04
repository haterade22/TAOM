# Plan 040: Every load says where its time went: XML merge per type, patch groups, TAOM hooks and campaign handlers

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat 0912e1b7..HEAD -- Main/SubModule.cs Main/PatchCategoryApplier.cs Main/Composition/FeatureModules.cs Main/Core/Diagnostics Main/Adapters/ICampaignListenerAdapter.cs Main/Adapters/CampaignListenerAdapter.cs Main/Features/LoadTimeStamps Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Dependencies/Foundation/PatchShieldPolicy.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs TAOM.Tests/Features/LoadTimeStamps TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/load-time-stamps.md docs/features/battle-load-diagnostics.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/reference/feature-map.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Two kinds of change are expected and are NOT a
> mismatch, because other plans of the same run touch the same files: a settings count that moved
> (Step 3 says how to handle it), new lines elsewhere in `SubModule.cs`, the registry or the
> feature map, and new entries appended to `PatchShieldPolicy.ExcludedTargetMethods` (plans 028, 039,
> 041 and possibly 042 add their own). The excerpts this plan anchors on must still be there, word
> for word.

## Status

- **Priority**: P2
- **Effort**: L (three commits: patch groups and hooks; XML loads; campaign handlers)
- **Risk**: MED. Always on, for every player: a stopwatch around each patch group, and two Harmony
  categories applied at process load whatever the toggle says, which patch seven engine methods
  (`MBObjectManager.LoadXML` and `CreateMergedXmlFile`, five `CampaignEventDispatcher` lifecycle
  methods) with observe-only `void` prefixes and finalizers. Those seven go on PatchShield's
  exclusion list (design decision 7), or PatchShield's second pass would wrap them and start
  swallowing exceptions that propagate today. Trade-off, stated in the commit bodies: another mod's
  patch on one of those seven methods loses PatchShield's rescue. Load cost: seven `Harmony.Patch`
  calls per process (about 35 ms to 1.3 s on the maintainer's desktop, where one patch has cost 5 to
  186 ms), which this plan's own `[PatchApply]` lines then report. Default off: the campaign-handler
  timing swaps the engine's listener delegates for the length of one dispatch.
- **Depends on**: none. Plan 042 (the XML merge fast path) is judged by stamp (1) of this plan, and
  either may land first.
- **Category**: perf / diagnostics
- **Planned at**: commit `0912e1b7`, 2026-10-02 (code identical to `dffdf879`; the commits between
  are run records only)
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472), failing `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the
  other languages; the paid translator run waits on the maintainer). Skipped:
  `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`. Python
  suite (not touched by this plan): `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`, failing
  `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
  `test_default_is_on_the_e_drive`. Hosted CI under reference assemblies also fails
  `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
  (`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`).
- **Issue**: filed by the orchestrator before execution (draft title "diagnostics: stamp the load
  phases nobody times"); use the number it gives you in the feature doc and the commit bodies.

## Why this matters

A new campaign's loading screen lasted 50.2 s on the maintainer's desktop on 2026-10-02 (engine log
window 12:45:00 to 12:45:51), and a custom battle about 25 s. The engine's own log attributes about
28 s of the campaign load to the module XML merge, but TAOM's `taom_debug.log` says nothing about it,
nothing about how long TAOM's own Harmony patch groups take to apply, and nothing about TAOM's
`OnGameStart` and `OnGameInitializationFinished` work. Two stretches of the same load, 3.37 s and
3.18 s, have no line in either log at all: both are inside the new-game event fan-out, where every
campaign behaviour's handlers run. After this plan, every load in every player's log is split into
the engine's XML loading (per type, always on), TAOM's patch application (per phase, always on; per
group with the new toggle) and, with the toggle on, TAOM's hook steps and every campaign handler of a
new game, a loaded save and the session start. Plan 042 (which replaces the merge) and any later
load-time fix are then chosen and confirmed by numbers from the field, not by inference.

## Current state

### The run's measurements (read from the run folder; leads, re-derived here)

- `plans/_audit/2026-10-02-perf/evidence/load/campaign-load-gaps.txt` (a new campaign, rgl log of
  2026-10-02): "window span 50.239 s"; "3.372 s after [12:45:42.126000] creating hero from template
  with id: spc_notable_bluecraig_3"; "3.184 s after [12:45:46.182000] creating hero from template with
  id: lord_LN2_3, next: Dump integrity is compromised due to cheat usage". Both stretches end before
  the engine's "Finished starting a new game." line, which `Campaign.DoLoadingForGameType` prints
  right after `OnNewGameCreated(gameStarter); OnSessionStart(gameStarter);` (engine excerpt below).
- `campaign-load-xml.txt` (same load): 329 ModuleData files opened; NPCCharacters 56 files 13.302 s,
  Items 142 files 9.592 s, GameText 30 files 1.668 s, EquipmentRosters 31 files 1.381 s.
  `custom-battle-load-xml.txt`: 274 files; NPCCharacters 8.286 s, Items 5.709 s.
- `Main/_Module/SubModule.xml` at `0912e1b7`: 110 `<XmlNode>` entries; 48 `NPCCharacters`, 28
  `EquipmentRosters`, 19 `GameText`.

### Engine facts (installed v1.5.3, from `pwsh tools/taom-src.ps1 path <full type name>`)

Use full type names with `taom-src` (`TaleWorlds.ObjectSystem.MBObjectManager`); the bare name scans
every DLL and can miss.

`TaleWorlds.ObjectSystem.MBObjectManager` (`public sealed class`), lines 786-797. One method named
`LoadXML` (the other similarly named method is `LoadXml(XmlDocument doc, bool isDevelopment = false)`,
a different name):

```csharp
public void LoadXML(string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor = false)
{
    bool ignoreGameTypeInclusionCheck = skipXmlFilterForEditor || isDevelopment;
    XmlDocument mergedXmlForManaged = GetMergedXmlForManaged(id, skipValidation: false, ignoreGameTypeInclusionCheck, gameType);
    try
    {
        LoadXml(mergedXmlForManaged, isDevelopment);
    }
    catch (Exception)
    {
    }
}
```

So the merge (`GetMergedXmlForManaged`, which builds the file list and returns
`CreateMergedXmlFile(list, xsltList, skipValidation)` at :913) can throw out of `LoadXML`, while the
object creation (`LoadXml(XmlDocument)`) swallows its own exceptions. `CreateMergedXmlFile`,
lines 962-978:

```csharp
public static XmlDocument CreateMergedXmlFile(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation)
{
    XmlDocument xmlDocument = CreateDocumentFromXmlFile(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation);
    for (int i = 1; i < toBeMerged.Count; i++)
    {
        if (xsltList[i] != "")
        {
            xmlDocument = ApplyXslt(xsltList[i], xmlDocument);
        }
        if (toBeMerged[i].Item1 != "")
        {
            XmlDocument xmlDocument2 = CreateDocumentFromXmlFile(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation);
            xmlDocument = MergeTwoXmls(xmlDocument, xmlDocument2, toBeMerged[i].Item2, keepDuplicates: false);
        }
    }
    return xmlDocument;
}
```

An entry whose file path is `""` is a placeholder the engine skips; `xsltList[0]` is never applied.
`CreateMergedXmlFile` is also called by `GetMergedXmlForNative` (:942), `GameTextManager`
(GameText), and TAOM's own `Main/Adapters/ObjectManagerAdapter.cs` and
`Main/Adapters/MonsterSizeCatalogAdapter.cs`, none of them through `LoadXML`.

Who calls `LoadXML`: `TaleWorlds.Core.MBObjectManagerExtensions.LoadXML(this MBObjectManager, string
id, bool skipXmlFilterForEditor = false)` (lines 7-18) null-guards `Game.Current`: with a game it
passes `current.GameType.IsDevelopment` and `current.GameType.GameTypeStringId` (which defaults to
`GetType().Name`, `TaleWorlds.Core.GameType.cs:40`), without one `isDevelopment: false` and
`gameType: ""`, then calls the instance method. So `gameType` can be `""`, which the summary's
`game=none` covers. Callers: `Campaign.InitializeBasicObjectXmls` (:1481-1482),
`Campaign.InitializeDefaultCampaignObjects` (:1490-1492), `SandBoxManager.InitializeSandboxXMLs`
(:371-389), `Game` (`TaleWorlds.Core.Game.cs:437-445`), `SandBox.SandBoxSubModule` (:113-114) and
`CustomGame.LoadCustomGameXmls` (`TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs:123-126`).
All of them run inside the game type's `OnInitialize`, which ends by calling
`GameManager.OnGameInitializationFinished` (`Campaign.cs:1471`), so a summary taken in TAOM's
`OnGameInitializationFinished` covers every `LoadXML` of that load.

`TaleWorlds.CampaignSystem.Campaign`, the order of a load (`DoLoadingForGameType`,
`PostInitializeFourthState`, :1675-1714): a saved campaign runs `OnGameLoaded(gameStarter);
OnSessionStart(gameStarter);` (:1685-1686), a new one `OnNewGameCreated(gameStarter);
OnSessionStart(gameStarter); Debug.Print("Finished starting a new game.");` (:1709-1711). This
happens AFTER `Campaign.OnInitialize`, so after TAOM's `OnGameInitializationFinished`. The private
`Campaign.OnNewGameCreated` (:1603-1609):

```csharp
private void OnNewGameCreated(CampaignGameStarter gameStarter)
{
    OnNewGameCreatedInternal();
    base.GameManager?.OnNewGameCreated(base.CurrentGame, gameStarter);
    CampaignEventDispatcher.Instance.OnNewGameCreated(gameStarter);
    OnAfterNewGameCreatedInternal();
}
```

`Campaign.OnGameLoaded` (:703-720) calls `CampaignEventDispatcher.Instance.OnGameEarlyLoaded(starter);`
then `CampaignEventDispatcher.Instance.OnGameLoaded(starter);`. `Campaign.OnSessionStart` (:752-771)
calls `CampaignEventDispatcher.Instance.OnSessionStart(starter);` then `OnAfterSessionStart(starter);`.

`TaleWorlds.CampaignSystem.CampaignEventDispatcher` (`public class CampaignEventDispatcher :
CampaignEventReceiver`; receivers built in `Campaign.OnInitialize` as `{ CampaignEvents, IssueManager,
QuestManager }`). Five public overrides, one overload each, each taking `(CampaignGameStarter
campaignGameStarter)`: `OnSessionStart` (:1053), `OnAfterSessionStart` (:1062), `OnNewGameCreated`
(:1071), `OnGameEarlyLoaded` (:1080), `OnGameLoaded` (:1089). Shape (:1071-1078):

```csharp
public override void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
{
    CampaignEventReceiver[] eventReceivers = _eventReceivers;
    for (int i = 0; i < eventReceivers.Length; i++)
    {
        eventReceivers[i].OnNewGameCreated(campaignGameStarter);
    }
}
```

`TaleWorlds.CampaignSystem.CampaignEvents` (the receiver every behaviour listens on), :2088-2116:

```csharp
public override void OnSessionStart(CampaignGameStarter campaignGameStarter)
{
    Instance._onSessionLaunchedEvent.Invoke(campaignGameStarter);
}
public override void OnAfterSessionStart(CampaignGameStarter campaignGameStarter)
{
    Instance._onAfterSessionLaunchedEvent.Invoke(campaignGameStarter);
}
public override void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
{
    Instance._onNewGameCreatedEvent.Invoke(campaignGameStarter);
    for (int i = 0; i < 100; i++)
    {
        Instance._onNewGameCreatedPartialFollowUpEvent.Invoke(campaignGameStarter, i);
    }
    Instance._onNewGameCreatedPartialFollowUpEndEvent.Invoke(campaignGameStarter);
}
public override void OnGameEarlyLoaded(CampaignGameStarter campaignGameStarter)
{
    Instance._onGameEarlyLoadedEvent.Invoke(campaignGameStarter);
}
public override void OnGameLoaded(CampaignGameStarter campaignGameStarter)
{
    Instance._onGameLoadedEvent.Invoke(campaignGameStarter);
}
```

The public static accessors (:857-869): `OnSessionLaunchedEvent`, `OnAfterSessionLaunchedEvent`,
`OnNewGameCreatedEvent`, `OnGameEarlyLoadedEvent`, `OnGameLoadedEvent` are
`IMbEvent<CampaignGameStarter>`; `OnNewGameCreatedPartialFollowUpEvent` is
`IMbEvent<CampaignGameStarter, int>` (:863) and `OnNewGameCreatedPartialFollowUpEndEvent` is
`IMbEvent<CampaignGameStarter>` (:865). Each returns `Instance._...` where
`private static CampaignEvents Instance => Campaign.Current.CampaignEvents;` (:597), so they throw
without a campaign; inside a dispatch there always is one. The backing fields are
`MbEvent<CampaignGameStarter>` and `MbEvent<CampaignGameStarter, int>` (:293-307).

`TaleWorlds.CampaignSystem.MbEvent<T>` (whole file, `MbEvent`1.cs`):

```csharp
public class MbEvent<T> : IMbEvent<T>, IMbEventBase
{
    internal class EventHandlerRec<TS>
    {
        public EventHandlerRec<TS> Next;
        internal Action<TS> Action { get; private set; }
        internal object Owner { get; private set; }
        public EventHandlerRec(object owner, Action<TS> action) { Action = action; Owner = owner; }
    }
    private EventHandlerRec<T> _nonSerializedListenerList;
    public void AddNonSerializedListener(object owner, Action<T> action)
    {   // head insert: the last listener added runs first
        EventHandlerRec<T> eventHandlerRec = new EventHandlerRec<T>(owner, action);
        EventHandlerRec<T> nonSerializedListenerList = _nonSerializedListenerList;
        _nonSerializedListenerList = eventHandlerRec;
        eventHandlerRec.Next = nonSerializedListenerList;
    }
    public void Invoke(T t) { InvokeList(_nonSerializedListenerList, t); }
    private void InvokeList(EventHandlerRec<T> list, T t)
    {
        while (list != null) { list.Action(t); list = list.Next; }
    }
    public void ClearListeners(object o) { ClearListenerOfList(ref _nonSerializedListenerList, o); }
    // ClearListenerOfList unlinks the first record whose Owner == o; it never reads Action.
}
```

`MbEvent<T1, T2>` (`MbEvent`2.cs`) is the same with `EventHandlerRec<TS, TQ>`, `Action<TS, TQ>`,
`Invoke(T1 t1, T2 t2)`. No catch anywhere: a handler's exception leaves `Invoke` as is. Metadata read
from the installed `TaleWorlds.CampaignSystem.dll` on 2026-10-02: the nested types are
`TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1` (2 generic arguments) and
`TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2` (4); each declares field `Next`, properties
`Action` and `Owner`, methods `get_Action`, `set_Action`, `get_Owner`, `set_Owner`; `MbEvent`1` and
`MbEvent`2` each declare field `_nonSerializedListenerList`. An existing binding test already pins
the list field: `TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs:103`.

TAOM registers, at `0912e1b7`: 20 `OnNewGameCreatedEvent`, 19 `OnGameLoadedEvent`, 43
`OnSessionLaunchedEvent` and 5 `OnNewGameCreatedPartialFollowUpEvent` listeners (grep of
`CampaignEvents.<Event>.AddNonSerializedListener` in `Main/`). The `CultureMarketplace` initial seed
(`Main/Features/CultureMarketplace/CultureMarketplaceBehavior.cs:72`) is a PartialFollowUp handler.
No TAOM patch targets `CampaignEvents`, `CampaignEventDispatcher` or `MBObjectManager.LoadXML`/
`CreateMergedXmlFile` today; the only `MBObjectManager` patch is
`Main/Features/StaleCharacterRepair/Hooks/Patch83_StaleCharacterRepair.cs:33` (`PreAfterLoad`).

### The repo side

**`Main/PatchCategoryApplier.cs`** (84 lines, `internal sealed class PatchCategoryApplier`, namespace
`TAOM`): applies one Harmony category, contains its failure. Lines 21-52:

```csharp
internal sealed class PatchCategoryApplier
{
    private readonly Action<string> _apply;
    private readonly IModLogger _logger;
    private readonly List<string> _failed = new();

    internal PatchCategoryApplier(Action<string> apply, IModLogger logger)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal bool TryApply(string category)
    {
        try
        {
            _apply(category);
            return true;
        }
        catch (Exception ex)
        {
            _failed.Add(category);
            _logger.LogError(
                $"[PatchApply] {category} FAILED (Harmony stops a category at its first failing class): {ex}");
            return false;
        }
    }
```

It also has `RecordSkippedClasses` (:58-67) and `TakeFailureSummary(TextObject phase)` (:74-83).
Constructed once, `Main/SubModule.cs:206-209`:

```csharp
        _patches = new PatchCategoryApplier(
            category => categoryIndex.Apply(_harmony, category),
            IoC.Resolve<IModLogger>());
        _patches.RecordSkippedClasses(categoryIndex.SkippedClasses);
```

`TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs:194-198` pins that statement shape with a
regex (`_patches = new PatchCategoryApplier\(\s*category => \1\.Apply\(_harmony, category\),[^;]+;`):
keep the two-argument call in `SubModule.cs` unchanged. Other constructions:
`PatchCategoryApplierTests.cs:42,53,59,158` and `PatchCategoryIndexTests.cs:85`, all two-argument.

**Where categories are applied** (`Main/SubModule.cs`, single owner; every `TryPatchCategory` call is
inside one of these four blocks):

- `OnSubModuleLoad`, ends at :655-658:
  ```csharp
          TryPatchCategory("Patch42_CastleRecruitment");
          FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);
          // No ReportPatchFailures here: nothing receives a message yet (see the startup report in
          // OnBeforeInitialModuleScreenSetAsRoot), so this phase's failures wait for it.
  ```
- `OnBeforeInitialModuleScreenSetAsRoot`, :675-685:
  ```csharp
          if (!_basicTableauGuardApplied)
          {
              _basicTableauGuardApplied = true;
              TryPatchCategory("Patch55_BasicTableauRaceGuard");
              FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);
              // Reports OnSubModuleLoad's failures and Patch55's together. ...
              ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_startup}startup"), persistent: true);
          }
  ```
- `OnGameInitializationFinished`, :1541-1566 (the every-game part, then the once-per-process guard):
  ```csharp
      public override void OnGameInitializationFinished(Game game)
      {
          base.OnGameInitializationFinished(game);

          // [SaveLoad] campaign-launch memory stamp, BEFORE the once-per-process guard below so every
          // game init in the process gets one, not just the first.
          StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameInitializationFinished);

          // Mount sizes live on the Monster (...). Every game init, before
          // the once-per-process guard: each game reloads its items from XML, and no mission has built a mount yet.
          IoC.Resolve<Features.MonsterSize.IMonsterSizeService>().ApplyMonsterSizes();

          // Armour acquisition (...): every game init too, ...
          IoC.Resolve<Features.ArmourAcquisition.IArmourGateService>().ApplyGating(game?.GameType is Campaign);

          // Harmony patches are process-global ...
          if (_gameInitPatchesApplied) return;
          _gameInitPatchesApplied = true;
  ```
  and its patch batch ends at :1953-1960:
  ```csharp
          TryPatchCategory("Patch69_TournamentEndGuard");
          FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);
          ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));

          // Manual patches for PRIVATE engine methods ...
          ManualPatchApplicator.ApplyAll(_harmony);
  ```
  The method ends at :1995-2000 with the Harmony census `try { ... } catch (System.Exception ex) {
  IoC.Resolve<IModLogger>().LogWarning($"[HarmonyCensus] wiring failed: ..."); }` and its closing `}`.
- `OnMissionBehaviorInitialize`, :2010-2016:
  ```csharp
          if (!_missionTimePatchesApplied)
          {
              _missionTimePatchesApplied = true;
              TryPatchCategory("Patch_MissionTime_SetMovementOrder");
              FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);
              ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_mission_start}mission start"));
          }
  ```

`OnGameStart` (:849-891): `base.OnGameStart(...)`; a guarded `LogSessionSnapshot()` try/catch
(:856-860); `RegisterCustomBattleModels(gameStarterObject);`; the
`if (gameStarterObject is CampaignGameStarter campaignStarter) { ... RegisterCampaignLifeBehaviors(campaignStarter); }`
block closing at :886; then :888-890:

```csharp
        // Feature modules last: their behaviors and models follow every hand-wired one (a Custom
        // Battle starter gets only CustomBattle-target models).
        FeatureModuleHooks.AddGameStartContent(gameStarterObject);
    }
```

Source gates that read `SubModule.cs` and must stay green without being edited:
`TAOM.Tests/Composition/FeatureModulesTests.cs` `Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`
(:144-175: each `FeatureModuleHooks.RunPhase(...)` call appears once and sits between named
anchors; `AddGameStartContent` must follow a `}` after `RegisterCampaignLifeBehaviors(campaignStarter);`),
`IoCResolver_IsReadOnlyByTheFeatureModuleHooks` (:116-127: no file but
`Composition/FeatureModuleHooks.cs` may contain the text `IoC.Resolver`), and the four
`SubModuleSource_*` tests in `PatchCategoryApplierTests.cs`.

**MCM toggles**: `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` (62 lines,
`AttributeGlobalSettings<BattleLoadDiagnosticsSettings>`, `FormatType => "json2"`, the page "TAOM
Battle Load Diagnostics"). Its last group, :58-61:

```csharp
    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Mission Frame-Time Heartbeat", Order = 0, RequireRestart = false,
        HintText = "Writes a [MissionPerf] line to the TAOM debug log every 5 seconds ... Default ON.")]
    public bool EnableMissionPerfHeartbeat { get; set; } = true;
```

Read through `IBattleLoadDiagnosticsSettingsProvider` (31 lines) and
`BattleLoadDiagnosticsSettingsProvider` (61 lines; pattern :41-42
`public bool MemorySamplerEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableMemorySampler ?? true;`),
registered `Reuse.Singleton` in `BattleLoadDiagnosticsIoC.cs:10`. MCM settings are not localized
anywhere in TAOM (no `{=` in any `SettingPropertyBool` of `TaomSettings.cs`,
`BattleLoadDiagnosticsSettings.cs` or `CrashReportSettings.cs`). `BattleLoadDiagnosticsSettings.Instance`
is null in `OnSubModuleLoad` (MCM builds it in its own `OnBeforeInitialModuleScreenSetAsRoot`;
`docs/features/mcm.md:96-102`, `SettingRequireRestartPostureTests.cs:23-30`) and readable by
`OnGameStart` and `OnGameInitializationFinished` (`SubModule.cs:1991` reads it there today).
Whether it is readable inside TAOM's own `OnBeforeInitialModuleScreenSetAsRoot` is UNVERIFIED, so
this plan never reads the toggle there.

A new MCM property moves three gates, by design:

- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211`
  `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 9, covered: 0);` (its comment,
  :205-209: "add a setting anywhere below and one of these numbers moves, which is the moment to
  decide what it is").
- `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (:217-252) requires
  `docs/features/coop-interop.md` and `docs/features/bannerlord-together-compat.md` to contain
  `**<total>**` or ` <total> MCM settings`. Today coop-interop.md:310 "TAOM ships **337** settings
  across", :312 "the split is 320 in `TaomSettings`, 9 in `BattleLoadDiagnosticsSettings`, 7 in",
  :316 "The 120 excluded (67 counted 2026-09-22, ..., and the thirteen Menus & Loading Screens settings
  added 2026-10-01, #704) are instrumentation, ..."; bannerlord-together-compat.md:291 "TAOM's 337 MCM
  settings". The simulation-relevant count (217) does not change: the new setting is instrumentation.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs` `Instrumentation` set, :70-72:
  ```csharp
          // The doctrine status line and the [MissionPerf] heartbeat; EnableCultureDoctrine itself
          // changes which tactics an AI team can pick and stays relevant.
          "CultureDoctrineDebug", "EnableMissionPerfHeartbeat",
  ```
  `NoExcludedName_IsDead` (:277-) fails if a listed name is not a real property.
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` fails on any value setting without
  `RequireRestart = false`; the new toggle is read live, so it carries `RequireRestart = false` and
  needs no allowlist entry.

**Composition** (plan 018): a feature owns its wiring in a `TaomFeatureModule`
(`Main/Composition/TaomFeatureModule.cs`): `RegisterServices(IRegistrator)`,
`InitializeStatics(IResolver)` (runs inside `IoC.Configure`, before any category applies),
`PatchCategories` as `new PatchCategoryDecl(category, ApplyPhase.ProcessLoad)`. The list,
`Main/Composition/FeatureModules.cs:16-24`:

```csharp
    internal static readonly TaomFeatureModule[] All =
    {
        new Features.WandererAllegiance.WandererAllegianceModule(),
        new Features.CreatureBandits.CreatureBanditsModule(),
        new Features.ArmourAcquisition.ArmourAcquisitionModule(),
        new Features.RealmBorders.RealmBordersModule(),
        new Features.BattleCorpses.BattleCorpsesModule(),
        new Features.TournamentRewards.TournamentRewardsModule(),
    };
```

Exemplars: `Main/Features/TournamentRewards/TournamentRewardsModule.cs` (a `const string
PatchCategory`, `RegisterServices` with `registrator.Register<X>(Reuse.Singleton)`) and
`Main/Features/CreatureBandits/CreatureBanditsModule.cs:57-58` (`InitializeStatics` handing a static a
resolved logger). `FeatureModulesTests.EveryDeclaredPatchCategory_IsDeclaredOnce_AndSubModuleNoLongerAppliesIt`
requires a module's category to be absent from `SubModule.cs`. Services are `public sealed` with
public constructors (`TournamentRewardsService.cs:15,25`); DryIoc resolves them by constructor.
`InternalsVisibleTo("TAOM.Tests")` exists. Main and the tests are SDK-style projects: new `.cs` files
compile without a csproj edit.

**Logging**: `Main/Core/Logging/IModLogger.cs` (`LogInfo`, `LogDebug`, `LogWarning`, `LogError`,
`LogFilePath`). `Main/Core/Logging/FileLogger.cs:8-11`: "INFO/WARNING/ERROR therefore drain to disk
synchronously on the calling thread; DEBUG (the bulk of the volume) stays async." The test logger
pattern: `SettingsFingerprintTests.cs:449-458` (`RecordingLogger` adding `"INFO " + m` and so on).

**Line format exemplar**: `Main/Features/MissionPerf/MissionPerfLine.cs` (a pure static builder,
`string.Format(CultureInfo.InvariantCulture, ...)`). No existing timing clock abstraction exists in
`Main/` (only raw `Stopwatch.GetTimestamp()` in
`Main/Features/MissionPerf/Hooks/MissionPerfHeartbeatBehavior.cs:52` and
`Main/Features/MapLoadDiagnostics/Hooks/Campaign_RealTick_MapLoad_Patch.cs:58`).

**Binding and reflection gates**: `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` auto-discovers
every `[HarmonyPatch]` class and resolves its target. Private engine members reached by reflection
are listed by hand in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (rows
`[DataRow(fullName, simpleName, member, kind, sourceSite)]`; `ResolveType` uses
`Assembly.GetType(fullName)`, which accepts `+` for nested types; the last row is the FactionUI
`ButtonWidget.HandleClick` one) and in `docs/reference/taleworlds-api-snapshot/reflection-sites.md`
"Category B" (table header `| Engine type | Member | Kind | Source site | What it drives |`, then dated
`Status (...)` lines). Per-patch binding exemplar:
`TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs`
(`GameAssemblies.EnsureLoaded()` in `[ClassInitialize]`, `Assert.Inconclusive` without the game,
`[TestCategory("BindingVerification")]`). Bool-returning prefixes need a
`CoopVetoClassificationTests` entry; this plan adds only `void` prefixes, so none.

**Test categories** (`.claude/rules/tests.md`): `RequiresGame` when a test executes engine code
(constructing an `MbEvent`, using a patch class whose attributes name an engine type),
`BindingVerification` for binding checks. Hosted CI runs unit tests with
`TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification` and the
binding gate with `TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall`
on reference assemblies.

**Patch numbering**: the highest category in code at `0912e1b7` is `Patch96_TournamentRewards`;
plans 028, 041 and 042 of this run reserve `Patch97`, `Patch98` and `Patch99`, and plan 039 reserves
`Patch101` (`Patch101_MapFrameProfiler`, its "Targets" paragraph). Some of those plan files are
untracked in the run's worktree, and `git grep` does not see untracked files, so Step 1 searches
`plans/` with plain `grep`. Run on 2026-10-03: for 100, `git grep -n "Patch100_" -- Main Dependencies
TAOM.Tests docs` and `grep -rln "Patch100_" plans --include=*.md --exclude=040-load-time-stamps.md
--exclude-dir=_audit` both printed nothing; for 101 the second printed
`plans/039-campaign-map-frame-profiler.md`. Step 1 picks the number (written `PatchNNN` below); at
the planned-at commit it is `Patch100`.

**Docs**: `docs/reference/harmony-patch-registry.md` has one `## PatchNN_Name` section per category;
the last at `0912e1b7` is `## Patch96_TournamentRewards` (:1101), followed by an auto-generated
`<!-- backlinks-start ...` block (:1107). Exemplar section: `## Patch91_MissionTickStall` (:1051).
`docs/reference/feature-map.md` has one row per feature (`| SaveLoadDiagnostics | ... |` is :85).
`docs/features/battle-load-diagnostics.md` "## Configuration" (:594) has a settings table whose last
row is `| MemorySampleIntervalSeconds | 30 | ... |`. `tools/lint_docs.py` requires a
`docs/features/<kebab-name>.md` for every `Main/Features/<Name>/` folder
(`check_missing_feature_docs`), so the folder `LoadTimeStamps` needs `docs/features/load-time-stamps.md`.
Feature doc template: `docs/features/TEMPLATE.md`.

### PatchShield (read at `0912e1b7`)

`TAOM.Dependencies` attaches a finalizer of its own to every Harmony-patched method in the process,
in two passes: pass 1 in its `OnSubModuleLoad`, pass 2 in its `OnGameInitializationFinished`
(`Dependencies/SubModule.cs:293`, `PatchShield.Install()`), which runs at every game start before
TAOM's own `OnGameInitializationFinished` and so before the first `OnNewGameCreated` or
`OnGameLoaded` dispatch (`Campaign.cs:1471` comes before `DoLoadingForGameType`). This plan's two
categories apply at TAOM's `OnSubModuleLoad`, so pass 2 of the first game sees all seven targets.
Its "don't shield our own methods" skip reads the TARGET's assembly (`PatchShield.cs:165-176`),
here `TaleWorlds.ObjectSystem` and `TaleWorlds.CampaignSystem`, so it does not skip them. What it
skips (`PatchShield.cs:55-63`, `:184-189`):

```csharp
return PatchShieldPolicy.IsExcludedTargetNamespace(method.DeclaringType?.Namespace)
    || PatchShieldPolicy.IsExcludedTargetMethod(method.DeclaringType?.FullName, method.Name);
```

`Dependencies/Foundation/PatchShieldPolicy.cs:134-148`, the method list (ordinal match on
`<DeclaringType.FullName>.<Name>`, one entry per name covers every overload, `:151-160`):

```csharp
    public static readonly IReadOnlyList<string> ExcludedTargetMethods = new[]
    {
        "TaleWorlds.MountAndBlade.Formation.get_UnitDiameter",
        "TaleWorlds.MountAndBlade.Formation.GetUnitPositionWithIndexAccordingToNewOrder",
        "TaleWorlds.MountAndBlade.Formation.GetUnitSpawnFrameWithIndex",
        // Patch93's weapon-state guards (#692): ...
        "TaleWorlds.MountAndBlade.Agent.GetPrimaryWieldedItemIndex",
        "TaleWorlds.MountAndBlade.Agent.GetOffhandWieldedItemIndex",
        "TaleWorlds.MountAndBlade.Agent.GetMissileRange",
        // Patch93_CreatureBanditNoRout: ...
        "TaleWorlds.MountAndBlade.Mission.CanAgentRout",
    };
```

Its summary (`:119-133`) calls it a "Method-level hot-target exclusion list" and says "PatchShield
skips an excluded method for every owner, so a third-party patch on one of these methods also loses
the rescue."

What the shield would do on this plan's targets if they were not excluded:

- `ShieldFinalizerVoid` and `ShieldFinalizerWithResult` (`PatchShield.cs:246-271`) return `null`,
  swallowing the exception, when `ShouldSwallow` (`:273-306`) sees a `MissingMethodException`,
  `MissingFieldException` or `TypeLoadException` (the usual failure of a mod built against an older
  Bannerlord), and then `TryUnpatchOffendingPatches` (`:315-`) strips every unprotected owner's
  prefixes, postfixes and transpilers on that method. `com.taom.mod` (`Main/SubModule.cs:204`) is not
  protected: the `"TAOM"` entry of `CompiledProtectedOwnerPrefixes` is a prefix match.
- On a dispatcher method, a swallow ends the dispatch early: the rest of that event's listeners, the
  remaining PartialFollowUp rounds and the `IssueManager` and `QuestManager` receivers are skipped,
  and the load continues half-initialized with only a `diag.log` line. Today, with the dispatcher
  unpatched, that exception propagates.
- `CreateMergedXmlFile` returns a value, so a swallow returns `null`; `LoadXML`'s
  `try { LoadXml(null) } catch {}` eats the follow-on exception and that ModuleData type silently
  fails to load.
- Any other exception is handed back as a value, which switches Harmony's wrapper from `rethrow` to
  `throw` for the whole method (lesson "A value-returning finalizer that hands back its exception
  erases the throw site", `docs/reviews/lessons/harmony-il.md:577`), so this plan's own `void`
  finalizers would no longer keep the stack on their own.
- Each attach is one more `Harmony.Patch`: 5 to 10 ms or about 186 ms per attach on the maintainer's
  desktop (`PatchShieldPolicy.cs:162-170`).

The precedent for excluding a TAOM target, with a binding test that walks the real targets:
`TAOM.Tests/Features/CreatureBandits/CreatureBanditsWiringTests.cs:468-487`
(`HotCreatureTargets_AreOnPatchShieldsExclusionList`, `[TestCategory("BindingVerification")]`,
`Assert.Inconclusive` without the game, then for each patch class
`Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name), ...)`,
its target taken by `TargetOf` at `:47-54`, which merges the class's `[HarmonyPatch]` attributes and
calls `AccessTools.Method(info.declaringType, info.methodName)`). Lessons: "PatchShield wraps TAOM's
own patch on an engine method: exclude a hot target in the same change"
(`docs/reviews/lessons/harmony-il.md:665`) and the one after it (`:688`). Plans 028, 039 and 041 of
this run each append their own entries to the same list; plan 042, which adds a prefix on
`CreateMergedXmlFile`, says it needs no exclusion and treats PatchShield's Harmony id as a known
co-owner of that method.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2`)

Re-run on 2026-10-03 against a graph built from `5b7f5b1b` (`git diff --stat 5b7f5b1b..0912e1b7 --
Main TAOM.Tests Dependencies` is empty; `status` reports it stale only on 22 run-record files under
`plans/_audit/`). `PatchCategoryApplier`, `IBattleLoadDiagnosticsSettingsProvider` and `CoopSettingsRelevance` were
re-queried that day and match the lines below; the `BattleLoadDiagnosticsSettingsProvider`,
`BattleLoadDiagnosticsSettings` and `FeatureModules` lines are from the 2026-10-02 run:

- `PatchCategoryApplier` (node `main_patchcategoryapplier_taom_patchcategoryapplier`; the bare name is
  ambiguous with a test node): `SubModule` [references] `Main/SubModule.cs:L104`; `.TryPatchCategory()`
  :907; `.OnSubModuleLoad()` :209; `.ReportPatchFailures()` :915; `.OnBeforeInitialModuleScreenSetAsRoot()`
  :678; `.OnGameInitializationFinished()` :1592; `.OnMissionBehaviorInitialize()` :2013;
  `PatchCategoryApplierTests` :160; `PatchCategoryIndexTests` :88.
- `IBattleLoadDiagnosticsSettingsProvider`: `BattleLoadDiagnosticsService`, `BattleLoadStallWatchdog`,
  `ExitStallSampler`, `MemoryPressureSampler`, `MemoryStationSampler`, `MissionTickStallWatchdog`,
  their six test classes (all fake it with `Substitute.For`, which tolerates a new member), and the
  implementer `BattleLoadDiagnosticsSettingsProvider`.
- `BattleLoadDiagnosticsSettingsProvider`: the six `ValidateSampleIntervalSeconds_*` tests.
- `BattleLoadDiagnosticsSettings`: "No affected nodes found."
- `CoopSettingsRelevance`: `SettingsFingerprintTests` `.AssertSplit()` :268, `.NoExcludedName_IsDead()`
  :288, `.EverySettingsClass_HasItsSplitPinned()` :210.
- `FeatureModules`: "No affected nodes found." `Main/SubModule.cs` (by path):
  `BehaviorTreeMissionLogic.cs [imports]` only.
- `PatchShieldPolicy` (2026-10-03): `PatchShield.IsExcludedTarget` (`PatchShield.cs:L59`),
  `PatchShield.Install` (:L227), `PatchShield.ShouldSwallow` (:L305),
  `PatchShield.TryUnpatchOffendingPatches` (:L366), `Dependencies/SubModule.cs`
  `OnSubModuleLoad` (:L234) and `OnGameInitializationFinished` (:L293),
  `CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList` (:L484),
  `Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList` (:L53), and the test methods of
  `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`, none of which pins the
  list's length or contents (its `IsExcludedTargetMethod_*` tests at :245-263 check other names and
  null parts). Appending entries only widens the exclusion.
- Every other type this plan touches is new.

### Conventions that bind this change

- **ADR-002** (`docs/adrs/002-thin-entry-points.md`): Harmony patches and the module are thin and
  under 150 lines; logic lives in services.
- **ADR-007** (`007-adapter-pattern.md`): services never touch TaleWorlds types; the listener-list
  reflection lives in an adapter with an interface in `Main/Adapters/`.
- **ADR-008** (`008-testability-requirements.md`): services 100% covered, hooks 80%, entry points by
  binding and game tests.
- **ADR-003, ADR-004, ADR-005**: no `#region`, no `[Obsolete]`, no `#if`.
- `.claude/rules/csharp-architecture.md`: constructor injection, `Reuse.Singleton` services, an
  interface only when a test fakes it or a second class implements it (the clock and the adapter have
  one; the services do not).
- `.claude/rules/harmony-patches.md` and `docs/reviews/lessons/harmony-il.md` (read before Steps 11
  and 16; entries "Harmony binds prefix/postfix parameters by NAME, so pin the names" (:384), "Static
  cache state inside a Harmony patch is untestable by construction, so extract the decision" (:468),
  "A value-returning finalizer that hands back its exception erases the throw site" (:577)): an
  observe-only finalizer is `void`, which keeps Harmony's rethrow and the original stack; parameter
  names are pinned by a binding test; patch classes only forward to services. Also read
  "PatchShield wraps TAOM's own patch on an engine method" (:665) and the entry after it (:688):
  every target of a new category is checked against PatchShield in the same change, and the
  category's binding test walks the real targets through `IsExcludedTargetMethod`.
- `.claude/rules/tests.md`: names `MethodName_StateUnderTest_ExpectedBehavior`, MSTest, NSubstitute or
  hand-written fakes, AAA.
- **Logging (the maintainer's instruction, DECISIONS D6, binding)**: `taom_debug.log` is TAOM's
  critical record. Every instrument logs a one-line configuration header when it starts; every measured
  unit logs its fields; totals, maxima and counts come in a summary per phase, event or game; anything
  that disables itself, skips work or falls back logs one line with the reason and the consequence,
  once, never per frame; a cost cut keeps the information as an aggregate; per-frame lines are never
  INFO (this plan has none: every line is per load); the feature doc lists every new line with its
  fields and an example, and tests pin each format literally.

### Design decisions (taken; implement as written)

1. **What is always on**: the `[LoadXml]` per-type lines and the per-game `[LoadXml] summary`
   (about 25 to 40 INFO lines per load, comparable to plan 042's always-on `[XmlMerge]` lines; the
   brief's addendum keeps this stamp to confirm plan 042 "per type, on players' machines", and players
   do not turn on a default-off toggle), and the `[PatchApply]` phase totals (4 lines per process).
   Everything else (per-category `[PatchApply]`, `[LoadPhase]`, `[Lifecycle]`) follows a new MCM
   toggle, **Enable Load-Time Stamps**, default OFF, on the Battle Load Diagnostics page.
   (Superseded 2026-10-03 for one `[Lifecycle]` line: `dispatch=` is written for every player; the
   handler and event lines still follow the toggle.)
2. **What the always-on part costs.** The phase totals cost far under a millisecond in total: two
   `Stopwatch.GetTimestamp()` reads and one list add per category (about 100 categories per process)
   plus four INFO lines. Step 5 adds a test that measures the per-category overhead with the real
   clock and fails above 50 microseconds, and the executor reports the measured number. Separately,
   applying this plan's two categories is seven `Harmony.Patch` targets at process load for every
   player, toggle on or off: about 35 ms to 1.3 s per process on the maintainer's desktop (5 to
   186 ms per patch, `PatchShieldPolicy.cs:162-170`). That cost is not hidden: the plan's own
   `[PatchApply] phase=OnSubModuleLoad total` line includes it, and with the toggle on the two
   per-category lines name it. Decision 7 keeps PatchShield from doubling it. Applying the
   `Lifecycle` category only when the toggle is on would save five of the seven for most players; it
   is deferred (Maintenance notes) because the composition root has no conditional category today.
3. **Per-category lines are written at game initialization, not at each phase end**: the toggle cannot
   be read in `OnSubModuleLoad` (MCM is not built yet) and its readability in TAOM's main-menu hook is
   unverified. So each phase end logs its total line at once and keeps its per-category records; the
   first game initialization (and the first mission) writes the held records when the toggle is on
   and drops them when it is off. Each line names its phase.
4. **Stamp (1) counts files and XSLTs itself** although plan 042's `[XmlMerge]` also does: this plan
   can land before 042, and the brief fixes the format `files=<n> ... xslt=<n>`. The counts come from
   a `void` finalizer on `CreateMergedXmlFile` (which runs whether or not 042's prefix skipped the
   original) attributing the merge to the `LoadXML` call in flight on that thread. `merge_ms` is the
   time from `LoadXML` entry to the end of its merge (file list plus merge); `objects_ms` is the rest
   (object creation). Merges outside `LoadXML` (GameText, the native merges, TAOM's two adapters) get
   no `[LoadXml]` line; 042's `[XmlMerge]` covers the validated ones.
5. **Campaign handlers are timed by swapping each listener's delegate for the length of one dispatch**:
   a prefix on each of the five `CampaignEventDispatcher` lifecycle methods, when the toggle is on,
   walks the event's `_nonSerializedListenerList` and replaces each record's `Action` (private setter)
   with a wrapper that times the original call in a `try/finally`; the `void` finalizer puts every
   original back. Order, arguments and exceptions are unchanged (the wrapper calls the original
   exactly once and has no catch, and the patches' prefixes and finalizers are `void`), provided
   PatchShield does not attach to the dispatcher methods: decision 7. Rejected: patching `MbEvent<T>.Invoke` (generic code shared by
   every reference-type instantiation, called per frame), dynamic Harmony patches on each handler
   method (patch cost at load, process-wide, and PatchShield wraps each), and editing 87 registration
   sites. **All listeners are timed, TAOM's and others'**, each line naming its assembly: the brief
   asks for TAOM's handlers and the fan-out around them, and the two silent stretches may be vanilla
   hero creation, which only a per-handler line for every owner attributes. Lines are written for a
   handler whose summed time is 10.00 ms or more; every event gets a total with the TAOM and other
   split and the count over the threshold.
6. **The five dispatcher methods**, not `CampaignEvents`: the dispatcher's time also covers the
   `IssueManager` and `QuestManager` receivers, so `ms - listeners_ms` on the dispatch line is the
   non-listener part of the fan-out.
7. **PatchShield skips all seven targets.** `MBObjectManager.LoadXML`, `MBObjectManager.CreateMergedXmlFile`
   and the five dispatcher methods go on `PatchShieldPolicy.ExcludedTargetMethods`, in the stage that
   patches them, each stage with a `BindingVerification` test walking its real targets (the
   `CreatureBanditsWiringTests` precedent). Without them, PatchShield's pass 2 at the first game start
   wraps all seven (see "PatchShield" above): a missing-API exception from any campaign handler,
   which propagates today, would be swallowed at the dispatcher and end the fan-out early, a merge
   failure would become a silently missing ModuleData type, and every player would pay seven more
   patches. These targets are not hot (once per type or per event per load), so this is a new
   reason for the list; the list's summary comment gains one sentence saying so. Trade-off, stated
   in the stage B and C commit bodies and the registry: PatchShield skips an excluded method for
   every owner, so another mod's patch on one of these seven methods loses the rescue. Plan 042 adds
   a prefix on `CreateMergedXmlFile` and today expects no exclusion there; whichever of the two lands
   second keeps exactly one entry for it (Step 11 checks).

## Step 0: the maintainer's edit

None. This plan edits no protected file (`.claude/settings.json`, `.claude/settings.local.json`,
`Directory.Build.props`, `docs/adrs/*.md`). If any step seems to need one, STOP.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the base's totals (Step 1) plus the new tests, failing set unchanged |
| One test class | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named tests run (a filter matching nothing proves nothing: check the count) |
| RefAsm unit step (hosted CI) | `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` then `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | the failure set Step 1 recorded, no new names |
| RefAsm binding gate | right after the unit step: `BANNERLORD_GAME_DIR="$(pwd -W)/TAOM.Tests/bin/Debug/net472/refasm-game" dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"` | the failure set Step 1 recorded, no new names, no skipped test |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs (no data changes; a sanity check) |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Blast radius | `python tools/graphify_taom.py refresh --if-stale`, then `python tools/graphify_taom.py affected "<Type>" --depth 2` | as in Current state |

Both MSBuild flags go on build AND test. Prefix every dotnet command with the `TEMP` and `TMP` your
dispatch rules give. Never `./build.ps1`: it deploys into the game install. After a RefAsm build,
run a normal `Build` again before the next normal `Tests` run (the RefAsm build replaces the outputs).

## Scope

**In scope** (the only files you create or modify):

- New, `Main/Core/Diagnostics/IStampClock.cs`, `Main/Core/Diagnostics/StopwatchStampClock.cs`.
- `Main/PatchCategoryApplier.cs` (timing, `EndPhase`, `WriteHeldCategoryLines`, a third constructor).
- New, `Main/Features/LoadTimeStamps/`: `LoadTimeStampsModule.cs`, `LoadTimeStampsHooks.cs`,
  `LoadTimeStampLines.cs`, `LoadStampDetailGate.cs`, `HookStampService.cs`, `HookTimer.cs`,
  `LoadXmlStampService.cs`, `LoadXmlCall.cs`, `LifecycleTimingService.cs`,
  `LifecycleDispatchScope.cs`, `Domain/LifecycleDispatch.cs`, `Domain/LifecycleEvent.cs`,
  `Domain/ListenerInfo.cs`; `Hooks/MBObjectManager_LoadXML_StampPatch.cs`,
  `Hooks/MBObjectManager_CreateMergedXmlFile_StampPatch.cs`,
  `Hooks/CampaignEventDispatcher_Lifecycle_StampPatches.cs`.
- New, `Main/Adapters/ICampaignListenerAdapter.cs`, `Main/Adapters/CampaignListenerAdapter.cs`.
- `Main/Composition/FeatureModules.cs`: append one line.
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`,
  `IBattleLoadDiagnosticsSettingsProvider.cs`, `BattleLoadDiagnosticsSettingsProvider.cs`: one
  property each.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: one name in `Instrumentation`.
- `Dependencies/Foundation/PatchShieldPolicy.cs`: seven entries appended to `ExcludedTargetMethods`
  (two in Step 11, five in Step 16) with their comments, and one sentence in that list's summary
  comment (Step 11). Nothing else in `Dependencies/`.
- `Main/SubModule.cs` (single owner): exactly the edits listed in Steps 7 and 11, nothing else.
- Tests: new `TAOM.Tests/Features/LoadTimeStamps/` (`FakeStampClock.cs`, `RecordingLogger.cs`,
  `FakeCampaignListenerAdapter.cs`, `LoadTimeStampLinesTests.cs`, `LoadStampDetailGateTests.cs`,
  `HookStampServiceTests.cs`, `LoadTimeStampsHooksTests.cs`, `LoadXmlStampServiceTests.cs`,
  `LoadXmlStampPatchShapeTests.cs`,
  `LifecycleTimingServiceTests.cs`, `CampaignListenerAdapterTests.cs`, `LoadTimeStampsWiringTests.cs`,
  `LoadTimeStampsBindingTests.cs`); edits to `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`,
  `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs`,
  `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs` (one number),
  `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (ten rows).
- Docs: new `docs/features/load-time-stamps.md`; a row in `docs/reference/feature-map.md`; two
  sections in `docs/reference/harmony-patch-registry.md`; rows and a status line in
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md`; a settings row in
  `docs/features/battle-load-diagnostics.md`; the counts in `docs/features/coop-interop.md` and
  `docs/features/bannerlord-together-compat.md`.

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`: no edit is needed (the module
  registers its services; SDK-style projects pick up new files). If one seems needed, STOP and report
  the exact line.
- `MBObjectManager.ApplyXslt`, `MergeTwoXmls`, `ToXDocument`, `ToXmlDocument`,
  `CreateDocumentFromXmlFile`, `LoadXmlWithValidation`: never patch them. Plan 042's fast path
  bypasses or mirrors them and stands aside when it sees patches there.
- What loads or how it loads: no XML, XSLT, XSD or ModuleData change, no change to any handler, to
  `ManualPatchApplicator`, or to any existing patch. Consolidating XML is plan 042 and later work.
- The rest of `Dependencies/`: `PatchShield.cs`, its other lists (`ExcludedTargetNamespacePrefixes`,
  `CompiledProtectedOwnerPrefixes`) and `PatchShieldPolicyTests.cs`. Also `CHANGELOG.md`,
  `plans/README.md`, any `.claude/` file, and any other plan file.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, editing the regex in `MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper`
  or the anchors in `Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`, or adding an
  allowlist entry. STOP and report instead. The one count this plan moves
  (`AssertSplit(typeof(BattleLoadDiagnosticsSettings), ...)`) moves together with the classification,
  which is the procedure that test's comment prescribes.

## Git workflow

- Commit on the branch you were given; never push or open a PR. Three commits, one per stage.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at `0912e1b7`; a hook refuses
  any other):
  - Stage A: `feat(load-stamps): <version> - time patch groups and TAOM's load hooks`
  - Stage B: `feat(load-stamps): <version> - stamp every module XML load per type`
  - Stage C: `feat(load-stamps): <version> - time each campaign handler of a load`
- Stage explicit paths only; write the message to a file in your scratch folder and run
  `git commit -F "<file>"`. Never `--no-verify`.
- The body is the changelog entry, wrapped at 72: what a player's log now says and why (which load
  phases it splits, which lines are always on, what the toggle adds, and that what loads and in what
  order is unchanged). Stages B and C also say, in plain words: the patches apply for every player,
  toggle off included, and their apply time shows in the `[PatchApply]` lines; PatchShield no longer
  wraps the methods this stage patches (name them), so a missing-API exception there propagates as
  it does today, and another mod's patch on them loses PatchShield's rescue. Name the issue number. No AI attribution trailer. Trailers: `Not-tested:` (stage A: the
  phase lines and hook steps in a real game load; stage B: the two Harmony patches inside a real
  load; stage C: the listener swap inside a real new game and save load) and `Research:` (the v1.5.3
  `MBObjectManager`, `CampaignEventDispatcher`, `CampaignEvents` and `MbEvent` excerpts this plan
  quotes).

## Steps

### Step 1: drift check, base, and the patch number

Run the drift check at the top. Record `git rev-parse HEAD` as your base and the output of
`git status --porcelain` (run files you did not create may already be listed; the done criteria
compare against this list). Before any edit run, in this order: the RefAsm unit step and the RefAsm
binding gate (record totals and failing names), a normal `Build`, then the full `Tests` command.
Then, for N = 100, 101, ... up to 109, run both
`git grep -n "PatchN_" -- Main Dependencies TAOM.Tests docs` and
`grep -rln "PatchN_" plans --include=*.md --exclude=040-load-time-stamps.md --exclude-dir=_audit`
(the second because `git grep` does not see untracked plan files), and take the first N for which
both print nothing. At the planned-at commit that is 100 (101 is plan 039's). Write it as
`PatchNNN` everywhere below (`PatchNNN_LoadTimeStamps_LoadXml`, `PatchNNN_LoadTimeStamps_Lifecycle`).

**Verify**: `Tests` prints `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` with the one
failure `EveryLanguage_DeclaresARowForEveryEnglishKey`, or the difference is explained by another
plan that landed in your base (name it and its new totals). The RefAsm runs are recorded (at
`dffdf879` hosted CI failed `EveryLanguage_DeclaresARowForEveryEnglishKey`, `Patch93_HasTheSevenPatchesInItsCategory`
and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`). `PatchNNN` is chosen; if 100 to 109 are all
taken, STOP.

---

**Stage A: the clock, the toggle, patch-group timing and hook stamps**

### Step 2: the clock and the stage A line formats (TDD)

Create `Main/Core/Diagnostics/IStampClock.cs`:

```csharp
namespace TAOM.Core.Diagnostics;

/// <summary>A monotonic timestamp source; ticks per second is Frequency. Faked in tests.</summary>
public interface IStampClock
{
    long Now { get; }
    long Frequency { get; }
}
```

and `StopwatchStampClock.cs` (`public sealed class StopwatchStampClock : IStampClock` with a public
parameterless constructor, `public static readonly StopwatchStampClock Instance = new();`,
`Now => Stopwatch.GetTimestamp()`, `Frequency => Stopwatch.Frequency`).

Create `Main/Features/LoadTimeStamps/LoadTimeStampLines.cs`, `internal static class
LoadTimeStampLines`, every method first as `=> throw new NotImplementedException();`:

```csharp
internal const double HandlerThresholdMs = 10.0;
internal static double ToMs(long ticks, long frequency);                 // frequency <= 0 gives 0
internal static string Ready();                                          // H1, stage A header
internal static string Detail(bool on);                                  // H2
internal static string PatchCategory(string phase, string category, double ms, bool ok);          // P1
internal static string PatchPhaseTotal(string phase, int categories, int failed, double ms,
                                       double maxMs, string? maxCategory);                         // P2
internal static string HookStep(string hook, string step, double ms);    // L1
internal static string HookTotal(string hook, string? game, double ms, int steps);                // L2
```

Implementation: `FormattableString.Invariant($"...")` with every millisecond value as `{x:0.00}`;
`ToMs` is `ticks * 1000.0 / frequency`. The exact strings (the test oracles; re-check each against
your implementation, character for character):

| Call | Returns |
|---|---|
| `Ready()` | `[LoadStamps] ready: [PatchApply] phase totals are always written; per-category [PatchApply] and per-hook [LoadPhase] lines follow "Enable Load-Time Stamps" (Battle Load Diagnostics page, default off), read at each game start and game initialization` |
| `Detail(true)` | `[LoadStamps] detail on: the per-category, per-hook and per-handler load-time lines are written` |
| `Detail(false)` | `[LoadStamps] detail off: only the always-on load-time totals are written; turn on "Enable Load-Time Stamps" for the per-category, per-hook and per-handler lines` |
| `PatchCategory("GameInit", "Patch11_Diplomacy", 4.2149, true)` | `[PatchApply] phase=GameInit category=Patch11_Diplomacy ms=4.21 result=ok` |
| `PatchCategory("OnSubModuleLoad", "Patch37_CrashReport", 0.5, false)` | `[PatchApply] phase=OnSubModuleLoad category=Patch37_CrashReport ms=0.50 result=failed` |
| `PatchPhaseTotal("GameInit", 74, 1, 312.4, 41.07, "Patch2_RefreshTableau")` | `[PatchApply] phase=GameInit total categories=74 failed=1 ms=312.40 max_ms=41.07 max_category=Patch2_RefreshTableau` |
| `PatchPhaseTotal("Mission", 0, 0, 0, 0, null)` | `[PatchApply] phase=Mission total categories=0 failed=0 ms=0.00 max_ms=0.00 max_category=none` |
| `HookStep("OnGameStart", "hand_wired", 120.5)` | `[LoadPhase] hook=OnGameStart step=hand_wired ms=120.50` |
| `HookTotal("OnGameStart", "Campaign", 133.25, 3)` | `[LoadPhase] hook=OnGameStart game=Campaign total ms=133.25 steps=3` |
| `HookTotal("GameInitOnce", null, 1.0, 2)` | `[LoadPhase] hook=GameInitOnce game=none total ms=1.00 steps=2` |
| `ToMs(1500, 1000)` / `ToMs(5, 0)` | `1500.0` / `0.0` |

Create `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampLinesTests.cs` (no category) with one
`Assert.AreEqual` test per row, named like `PatchCategory_Ok_FormatsTwoDecimalsAndResultOk`, plus
`PatchCategory_UnderAGermanCulture_StillUsesADot` (set `Thread.CurrentThread.CurrentCulture` to
`de-DE` in a try/finally and check `ms=4.21`).

**Verify (RED)**: `One test class` with `LoadTimeStampLinesTests` builds and every test fails with
`NotImplementedException`. Then implement. **Verify (GREEN)**: the same command, all pass, and the
count equals the number of tests you wrote.

### Step 3: the toggle (TDD)

Add to `BattleLoadDiagnosticsSettingsProviderTests.cs`:

```csharp
[TestMethod]
public void LoadTimeStampsEnabled_NoMcmInstance_DefaultsFalse()
{
    var sut = new BattleLoadDiagnosticsSettingsProvider();
    Assert.IsFalse(sut.LoadTimeStampsEnabled);
}
```

**Verify (RED)**: building the tests fails with a missing-member error naming `LoadTimeStampsEnabled`.

Then:

- `BattleLoadDiagnosticsSettings.cs`, after `EnableMissionPerfHeartbeat` (no em or en dash in the hint):
  ```csharp
  [SettingPropertyGroup("Load-Time Stamps")]
  [SettingPropertyBool("Enable Load-Time Stamps", Order = 0, RequireRestart = false,
      HintText = "Writes the detailed load-time lines to the TAOM debug log: one [PatchApply] line per Harmony patch group with its apply time (written at game initialization), [LoadPhase] steps of TAOM's game start and game initialization, and [Lifecycle] lines timing every campaign handler of a new game, a loaded save and the session start (a line for each handler taking 10 ms or more, and a total per event). The per-type [LoadXml] lines and the patch phase totals are always written. Costs a few microseconds per handler while a campaign loads and nothing during play. Default OFF.")]
  public bool EnableLoadTimeStamps { get; set; } = false;
  ```
- `IBattleLoadDiagnosticsSettingsProvider.cs`: `bool LoadTimeStampsEnabled { get; }` with a one-line
  doc comment (default false; read at game start, game initialization and each campaign dispatch).
- `BattleLoadDiagnosticsSettingsProvider.cs`:
  `public bool LoadTimeStampsEnabled => BattleLoadDiagnosticsSettings.Instance?.EnableLoadTimeStamps ?? false;`
- `CoopSettingsRelevance.cs` :70-72: append `"EnableLoadTimeStamps"` to the line holding
  `"EnableMissionPerfHeartbeat"` and extend the comment above it: "The doctrine status line, the
  [MissionPerf] heartbeat and the load-time stamps; ...".
- `SettingsFingerprintTests.cs:211`: the `reflected:` number of the `BattleLoadDiagnosticsSettings`
  row goes up by exactly one from what your base has (9 at `0912e1b7`; higher if another plan
  landed). `covered` stays 0.
- `docs/features/coop-interop.md`: the total in `TAOM ships **N** settings` goes up by one (337 to 338
  at `0912e1b7`); `N in \`BattleLoadDiagnosticsSettings\`` up by one (9 to 10); `The N excluded` up by
  one (120 to 121) and its parenthesis gains ", and the Load-Time Stamps toggle added 2026-10-0X"
  (the date you commit) before the closing `)`. `docs/features/bannerlord-together-compat.md`:
  `TAOM's N MCM settings` up by one (337 to 338). If any of these phrases is not in the file, STOP.

**Verify**: `One test class` for each of `BattleLoadDiagnosticsSettingsProviderTests`,
`SettingsFingerprintTests` and `SettingRequireRestartPostureTests`: all pass, the new test included.

### Step 4: the detail gate and the hook stamps (TDD)

Test helpers in `TAOM.Tests/Features/LoadTimeStamps/`:

- `FakeStampClock.cs`: `internal sealed class FakeStampClock : IStampClock` with
  `public long Now { get; set; }`, `public long Frequency { get; set; } = 1000;` (so one tick is one
  millisecond) and `public void Advance(long ticks) => Now += ticks;`.
- `RecordingLogger.cs`: `internal sealed class RecordingLogger : IModLogger` collecting
  `"INFO " + m`, `"DEBUG " + m`, `"WARN " + m`, `"ERROR " + m` into `public List<string> Lines`
  (shape of `SettingsFingerprintTests.cs:449-458`).

`LoadStampDetailGateTests.cs` (the provider via `Substitute.For<IBattleLoadDiagnosticsSettingsProvider>()`):
`Enabled_ReflectsTheProvider`; `Enabled_FirstRead_LogsTheDetailLineOnce` (two reads of `true`, one
`INFO [LoadStamps] detail on: ...` line); `Enabled_ValueChanges_LogsTheNewDetailLine` (true, true,
false: two lines, the second `detail off`); `Enabled_ProviderThrows_ReturnsFalse` (`.Returns(_ => throw new
InvalidOperationException())`).

`HookStampServiceTests.cs`: build the gate with its own `RecordingLogger`, separate from the one the
`HookStampService` and its timers write to (the gate logs `INFO [LoadStamps] detail on: ...` on its
first read, which would otherwise break the exact-sequence oracles below). Tests:
`Start_DetailOff_ReturnsNull`;
`Mark_LogsTheTimeSinceThePreviousMark` (start at 0, advance 5, `Mark("a")`, advance 7, `Mark("b")`:
lines `INFO [LoadPhase] hook=H step=a ms=5.00` then `... step=b ms=7.00`);
`End_LogsTheTotalSinceStartAndTheStepCount` (`... game=Campaign total ms=12.00 steps=2`);
`End_Twice_LogsOnce`; `End_NullGame_PrintsNone`.

Then implement:

- `LoadStampDetailGate` (`public sealed class`, ctor `(IBattleLoadDiagnosticsSettingsProvider settings,
  IModLogger logger)`), property `public bool Enabled`: read `settings.LoadTimeStampsEnabled` in a
  try/catch (false on any exception); when the value differs from the last one logged (or nothing was
  logged yet), log `LoadTimeStampLines.Detail(value)` at INFO; return the value.
- `HookStampService` (`public sealed class`, ctor `(LoadStampDetailGate gate, IStampClock clock,
  IModLogger logger)`), `public HookTimer? Start(string hook, string? game)`: null when
  `gate.Enabled` is false, else a new `HookTimer`.
- `HookTimer` (`public sealed class`, internal ctor `(string hook, string? game, IStampClock clock,
  IModLogger logger)` that records start and last mark), `public void Mark(string step)` (logs `HookStep`
  with the time since the last mark, then moves the mark and counts the step) and `public void End()`
  (logs `HookTotal` once). Both catch every exception and swallow it: a stamp must never break a load.
  Corrected after the Codex review (2026-10-03): `Mark` moves the mark after the line's own write (a
  second clock read), so a slow write is not charged to the next step; `HookTotal` stays the wall
  clock from the start.

**Verify (RED then GREEN)**: `One test class` with `LoadStampDetailGateTests`, then `HookStampServiceTests`:
first every new test fails (stubs throw `NotImplementedException`), then all pass.

### Step 5: patch-group timing in the applier (TDD)

Add to `PatchCategoryApplierTests.cs` (keep every existing test as it is, and keep `_logger`, the
class's NSubstitute logger at :30 and :36, for them) two fields set in `Setup`, `FakeStampClock _clock`
and `RecordingLogger _recording`, and a helper `ApplierTiming(Dictionary<string, long> durations,
params string[] failing)` that builds `new PatchCategoryApplier(category => { _clock.Advance(durations[category]);
if (failing contains category) throw new InvalidOperationException("boom " + category); }, _recording, _clock)`.
Every oracle below reads `_recording.Lines` (the `"INFO " + m` / `"ERROR " + m` form). The test file
needs `using TAOM.Core.Diagnostics;` and the namespace of the Step 4 helpers. Tests:

- `Constructor_WithANullClock_Throws`.
- `TryApply_ThenEndPhase_LogsThePhaseTotalWithTheSlowestCategory`: A 3 ms, B 9 ms; `EndPhase("GameInit")`
  logs exactly `INFO [PatchApply] phase=GameInit total categories=2 failed=0 ms=12.00 max_ms=9.00 max_category=B`.
- `TryApply_WhenTheApplyThrows_StillRecordsItsTimeAndCountsItFailed`: A 2 ms ok, B 4 ms failing:
  total line `categories=2 failed=1 ms=6.00 max_ms=4.00 max_category=B`; a line starting
  `ERROR [PatchApply] B FAILED (Harmony stops a category at its first failing class): ` is still
  logged and `TryApply("B")` still returns false.
- `EndPhase_WithNoCategories_LogsZerosAndNone`.
- `EndPhase_StartsAFreshPhase`: A in phase "OnSubModuleLoad", `EndPhase`, B in "MainMenu", `EndPhase`:
  the second total counts only B.
- `WriteHeldCategoryLines_Enabled_WritesEveryHeldCategoryInApplyOrderWithItsPhase`: A (OnSubModuleLoad),
  B (MainMenu), C (GameInit) each ended, then `WriteHeldCategoryLines(true)` writes three
  `[PatchApply] phase=<its phase> category=<name> ms=<x> result=ok` lines in that order.
- `WriteHeldCategoryLines_Disabled_WritesNothingAndDropsThem`: then a second call with `true` writes
  nothing.
- `TryApply_TimingOverhead_IsUnderFiftyMicrosecondsPerCategory`: the two-argument constructor (real
  clock), `apply` a no-op, 1,000 `TryApply` calls with distinct names timed by a local `Stopwatch`,
  assert the elapsed time is under 50 ms, and write the measured microseconds per call to the test
  output (`Console.WriteLine`). Report the number.

Then change `Main/PatchCategoryApplier.cs`:

```csharp
private readonly IStampClock _clock;
private readonly List<CategoryTiming> _phase = new();
private readonly List<(string Phase, CategoryTiming Timing)> _held = new();

internal PatchCategoryApplier(Action<string> apply, IModLogger logger)
    : this(apply, logger, StopwatchStampClock.Instance) { }

internal PatchCategoryApplier(Action<string> apply, IModLogger logger, IStampClock clock)
{
    _apply = apply ?? throw new ArgumentNullException(nameof(apply));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _clock = clock ?? throw new ArgumentNullException(nameof(clock));
}

internal bool TryApply(string category)
{
    var start = _clock.Now;
    var ok = false;
    try
    {
        _apply(category);
        ok = true;
        return true;
    }
    catch (Exception ex)
    {
        _failed.Add(category);
        _logger.LogError(
            $"[PatchApply] {category} FAILED (Harmony stops a category at its first failing class): {ex}");
        return false;
    }
    finally
    {
        _phase.Add(new CategoryTiming(category, _clock.Now - start, ok));
    }
}

/// <summary>Logs the phase's total line (always) and keeps its per-category records for
/// WriteHeldCategoryLines, because the toggle that decides them cannot be read in OnSubModuleLoad.</summary>
internal void EndPhase(string phase) { ... }

/// <summary>Writes every held per-category line when enabled, in apply order, then drops them.</summary>
internal void WriteHeldCategoryLines(bool enabled) { ... }
```

`CategoryTiming` is a private nested `sealed class` (Category, Ticks, Ok). `EndPhase` computes
count, failed, summed ms, the largest and its category (first one wins on a tie), logs
`LoadTimeStampLines.PatchPhaseTotal` at INFO, moves the records to `_held` with the phase name, and
clears `_phase`. Update the class summary with one sentence on the timing. Keep the ERROR text exactly.
Add `using TAOM.Core.Diagnostics;` (the clock) and `using TAOM.Features.LoadTimeStamps;` (the line
builder) to the file's usings (:1-4); the root-namespace applier already sits beside `SubModule.cs`,
which references features the same way.

**Verify**: `One test class` with `PatchCategoryApplierTests`: every old and new test passes (the
`RequiresGame` ones included on this machine), and the overhead test's printed figure is recorded.
Then `One test class` with `PatchCategoryIndexTests`: all pass.

### Step 6: the module and its static entry

Test first: `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampsHooksTests.cs` (no category;
`[TestCleanup]` calls `LoadTimeStampsHooks.Initialize(null, null)` so no wiring leaks into another
test): `DetailEnabled_Unwired_IsFalse`; `StartHook_Unwired_ReturnsNull`;
`DetailEnabled_Wired_ReflectsTheToggle` (a gate over a substitute provider returning true);
`StartHook_WhenTheServiceThrows_ReturnsNull` (wire a gate whose logger is a substitute
`IModLogger` that throws from `LogInfo`, so the gate's first read throws inside `Start`; assert null
and no exception). **Verify (RED)**: building the tests fails on the missing `LoadTimeStampsHooks`.

Create `Main/Features/LoadTimeStamps/LoadTimeStampsHooks.cs`, `internal static class
LoadTimeStampsHooks`, the only static state: references to the services, set once by the module.

```csharp
internal static LoadStampDetailGate? Gate { get; private set; }
internal static HookStampService? Hooks { get; private set; }

internal static void Initialize(LoadStampDetailGate? gate, HookStampService? hooks)
{ Gate = gate; Hooks = hooks; }
// Stage B adds InitializeLoadXml(LoadXmlStampService? service) and stage C
// InitializeLifecycle(LifecycleTimingService? service): separate setters, so a test can wire or
// clear one part without the others.

/// <summary>The toggle, or false when the module is not wired or the read throws.</summary>
internal static bool DetailEnabled { get { try { return Gate?.Enabled ?? false; } catch { return false; } } }

internal static HookTimer? StartHook(string hook, string? game)
{ try { return Hooks?.Start(hook, game); } catch { return null; } }
```

Create `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs`, `internal sealed class
LoadTimeStampsModule : TaomFeatureModule`, `Id => "LoadTimeStamps"`, with a class summary naming the
plan's purpose and the docs page. `RegisterServices`:
`registrator.Register<IStampClock, StopwatchStampClock>(Reuse.Singleton);`,
`registrator.Register<LoadStampDetailGate>(Reuse.Singleton);`,
`registrator.Register<HookStampService>(Reuse.Singleton);`. `InitializeStatics(IResolver resolver)`:
`LoadTimeStampsHooks.Initialize(resolver.Resolve<LoadStampDetailGate>(), resolver.Resolve<HookStampService>());`
then `resolver.Resolve<IModLogger>().LogInfo(LoadTimeStampLines.Ready());`. No categories yet.
Append `new Features.LoadTimeStamps.LoadTimeStampsModule(),` as the last entry of
`FeatureModules.All` (order-free: it adds no behaviour or model).

**Verify (GREEN)**: `Build` exits 0; `One test class` with `LoadTimeStampsHooksTests`, then with
`FeatureModulesTests`: all pass.

### Step 7: wire the phase ends and the hook stamps into SubModule.cs (TDD)

Create `TAOM.Tests/Features/LoadTimeStamps/LoadTimeStampsWiringTests.cs` (no category). Copy the
private `SubModuleMethodBody` brace-matching helper from `PatchCategoryApplierTests.cs:256-273`, and
replace its first line (which uses that class's private `CommentPattern` and `File.ReadAllText`)
with `var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);`. On
comment-stripped `SubModule.cs` at `0912e1b7` the helper closes `OnGameInitializationFinished` at
:2000 and `OnGameStart` at :891 (the plan review simulated it), so it works on both bodies:

- `SubModule_EndsEachPatchPhase_RightAfterItsRunnerCall`: regexes (whitespace between statements
  allowed) for `RunPhase\(ApplyPhase\.ProcessLoad, TryPatchCategory\);\s*_patches\.EndPhase\("OnSubModuleLoad"\);`,
  the same for `MainMenu` / `"MainMenu"`, and for `GameInit` / `"GameInit"` and `FirstMission` /
  `"Mission"` each followed by
  `\s*_patches\.WriteHeldCategoryLines\(Features\.LoadTimeStamps\.LoadTimeStampsHooks\.DetailEnabled\);`.
- `SubModule_OnGameStart_StampsItsSteps`: the `protected override void OnGameStart(Game game, IGameStarter gameStarterObject)`
  body contains `LoadTimeStampsHooks.StartHook("OnGameStart"`, the marks `"session_snapshot"`,
  `"hand_wired"`, `"feature_modules"` in that order, and `hookStamp?.End();` after the last.
- `SubModule_OnGameInitializationFinished_StampsTheEveryGamePartBeforeTheGuard`: in the
  `public override void OnGameInitializationFinished(Game game)` body,
  `StartHook("OnGameInitializationFinished"` and `hookStamp?.End();` both come before
  `if (_gameInitPatchesApplied) return;`, and `StartHook("GameInitOnce"` comes after
  `_gameInitPatchesApplied = true;`, with `onceStamp?.End();` as the last statement of the body.

**Verify (RED)**: `One test class` with `LoadTimeStampsWiringTests`: all three fail on the missing text.

Then edit `Main/SubModule.cs`, exactly these insertions (the `Features.` prefix is the file's own style):

1. After `FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);` (:656):
   `_patches.EndPhase("OnSubModuleLoad");`
2. After `FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu, TryPatchCategory);` (:679):
   `_patches.EndPhase("MainMenu");`
3. In `OnGameStart`: after `base.OnGameStart(game, gameStarterObject);`:
   `var hookStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("OnGameStart", game?.GameType?.GetType().Name);`;
   after the `LogSessionSnapshot` try/catch: `hookStamp?.Mark("session_snapshot");`; after the `}`
   closing the `CampaignGameStarter` block (:886) and before the "Feature modules last" comment:
   `hookStamp?.Mark("hand_wired");`; after `FeatureModuleHooks.AddGameStartContent(gameStarterObject);`:
   `hookStamp?.Mark("feature_modules");` and `hookStamp?.End();`.
4. In `OnGameInitializationFinished`: after `base.OnGameInitializationFinished(game);`:
   `var hookStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("OnGameInitializationFinished", game?.GameType?.GetType().Name);`;
   after the `StampSaveLoadPhase(...)` line: `hookStamp?.Mark("save_load_stamp");`; after
   `...ApplyMonsterSizes();`: `hookStamp?.Mark("monster_size");`; after `...ApplyGating(game?.GameType is Campaign);`:
   `hookStamp?.Mark("armour_gate");` and then `hookStamp?.End();` (before the guard's comment).
   After `_gameInitPatchesApplied = true;`:
   `var onceStamp = Features.LoadTimeStamps.LoadTimeStampsHooks.StartHook("GameInitOnce", null);`.
5. After `FeatureModuleHooks.RunPhase(ApplyPhase.GameInit, TryPatchCategory);` (:1954):
   `_patches.EndPhase("GameInit");` and
   `_patches.WriteHeldCategoryLines(Features.LoadTimeStamps.LoadTimeStampsHooks.DetailEnabled);`.
   After `ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));`:
   `onceStamp?.Mark("patch_categories");`. After `ManualPatchApplicator.ApplyAll(_harmony);`:
   `onceStamp?.Mark("manual_patches");`. As the last statement of the method, after the census
   `catch { ... }` block: `onceStamp?.End();`.
6. After `FeatureModuleHooks.RunPhase(ApplyPhase.FirstMission, TryPatchCategory);` (:2014):
   `_patches.EndPhase("Mission");` and
   `_patches.WriteHeldCategoryLines(Features.LoadTimeStamps.LoadTimeStampsHooks.DetailEnabled);`.

Nothing else in `SubModule.cs` changes. Add a one-line comment above the first insertion of each
block only where it helps a reader (for example "Load-time stamps (docs/features/load-time-stamps.md):
phase total now, per-category lines at game init."); no other comment edits.

**Verify (GREEN)**: `Build` exits 0. `One test class` with `LoadTimeStampsWiringTests`,
`FeatureModulesTests` and `PatchCategoryApplierTests`: all pass, none edited in this step except
the new class.

### Step 8: stage A docs, full suite, commit

- `docs/features/load-time-stamps.md` from `docs/features/TEMPLATE.md`: Overview, Why This Exists
  (the 50.2 s load and the two silent stretches, with their source files), Architecture (the
  applier's phase records and why per-category lines wait for game initialization; the hook steps),
  Configuration (the toggle, default off, what is always on), Key Files, and a **"Log lines"**
  section: a table with every stage A line (H1, H2, P1, P2, L1, L2): its level (all INFO), when it
  is written, every field, and an example taken from the test oracles, marked "example values".
  List the hook steps: `OnGameStart` (`session_snapshot`, `hand_wired`, `feature_modules`),
  `OnGameInitializationFinished` (`save_load_stamp`, `monster_size`, `armour_gate`), `GameInitOnce`
  (`patch_categories`, `manual_patches`, then the census in the total). Name the issue.
- `docs/reference/feature-map.md`: after the `| SaveLoadDiagnostics |` row add
  `| LoadTimeStamps | \`Main/Features/LoadTimeStamps/\`: load-time stamps in \`taom_debug.log\`: ... See [load-time-stamps.md](../features/load-time-stamps.md) |`
  (one sentence on what it stamps).
- `docs/features/battle-load-diagnostics.md` "## Configuration" table: after the
  `MemorySampleIntervalSeconds` row add
  `| \`EnableLoadTimeStamps\` | \`false\` | The detailed load-time lines ([load-time-stamps.md](load-time-stamps.md)). Independent of the master toggle. Local-only for co-op (\`CoopSettingsRelevance\`). |`.

**Verify**: normal `Build`, then `Tests`: the base's totals plus the new tests, the failing set
unchanged. `Docs` exits 0. `git status --porcelain` lists nothing beyond the Step 1 entries and the
stage A files. Then commit stage A (Git workflow).

---

**Stage B: the per-type XML load stamp**

### Step 9: stage B line formats (TDD)

Add to `LoadTimeStampLines` (stubs first) and pin in `LoadTimeStampLinesTests`:

```csharp
internal static string LoadXmlReady();
internal static string LoadXml(string id, int files, double ms, int xslt, double? mergeMs, double? objectsMs, string result);   // X1
internal static string LoadXmlSummary(string? game, int calls, int files, int xslt, double ms,
                                      double mergeMs, double objectsMs, double maxMs, string? maxId, int failed);    // X2
internal static string LoadXmlFault(Exception exception);
```

| Call | Returns |
|---|---|
| `LoadXmlReady()` | `[LoadXml] ready: one line per MBObjectManager.LoadXML call and a summary at every game initialization, always written` |
| `LoadXml("NPCCharacters", 56, 13302, 2, 13001.5, 300.5, "ok")` | `[LoadXml] id=NPCCharacters files=56 ms=13302.00 xslt=2 merge_ms=13001.50 objects_ms=300.50 result=ok` |
| `LoadXml("Items", 0, 3.25, 0, null, null, "XmlException")` | `[LoadXml] id=Items files=0 ms=3.25 xslt=0 merge_ms=none objects_ms=none result=XmlException` |
| `LoadXmlSummary("Campaign", 26, 329, 6, 28000, 26500, 1500, 13302, "NPCCharacters", 0)` | `[LoadXml] summary game=Campaign calls=26 files=329 xslt=6 ms=28000.00 merge_ms=26500.00 objects_ms=1500.00 max_ms=13302.00 max_id=NPCCharacters failed=0` |
| `LoadXmlSummary(null, 0, 0, 0, 0, 0, 0, 0, null, 0)` | `[LoadXml] summary game=none calls=0 files=0 xslt=0 ms=0.00 merge_ms=0.00 objects_ms=0.00 max_ms=0.00 max_id=none failed=0` |
| `LoadXmlFault(new InvalidOperationException("x"))` | `[LoadXml] stamp fault, some [LoadXml] lines may be missing this session: InvalidOperationException: x` |

**Verify**: RED (the new tests fail with `NotImplementedException`), then GREEN.

### Step 10: the stamp service (TDD)

`Main/Features/LoadTimeStamps/LoadXmlCall.cs`: `public sealed class LoadXmlCall` holding `Id`,
`GameType`, `Start` (ticks), `Parent` (`LoadXmlCall?`), and settable `MergeEnd` (`long?`), `Files`,
`Xslt`. `LoadXmlStampService` (`public sealed class`, ctor `(IStampClock clock, IModLogger logger)`):

```csharp
public LoadXmlCall? Begin(string? id, string? gameType);                       // pushes the call for this thread
public void MergeFinished(IList<Tuple<string, string>>? toBeMerged, IList<string>? xsltList);   // first merge of the call in flight
public void End(LoadXmlCall? call, Exception? exception);                      // pops, logs X1, aggregates
public void LogSummary();                                                      // logs X2 since the last summary, then resets
internal static int CountFiles(IList<Tuple<string, string>>? toBeMerged);      // entries whose Item1 is not null or ""
internal static int CountXslt(IList<string>? xsltList);                        // entries at index >= 1 that are not null or ""
```

Rules: the call in flight is a `ThreadLocal<LoadXmlCall?>`; `Begin` sets it to the new call (its
`Parent` is the previous value); `End` restores `Parent` first, then logs. `MergeFinished` does
nothing when no call is in flight or its `MergeEnd` is already set. `End`: `ms` is now minus `Start`;
with `MergeEnd`, `merge_ms` is `MergeEnd - Start` and `objects_ms` is `now - MergeEnd`, else both
`none`; `result` is `ok` or `exception.GetType().Name`. The summary sums calls, files, xslt, ms, and
merge and object ms over calls whose merge was seen; `max_id` is the slowest call's id (first on a
tie); `failed` counts calls with an exception; `game` is the last non-empty `gameType` since the last
summary, else `none`. Aggregation under one `lock`. Every public method catches every exception,
logs `LoadXmlFault` once per process at WARNING, and returns (`Begin` returns null). `id` null
prints `null`.

`LoadXmlStampServiceTests.cs` (no category; `FakeStampClock`, `RecordingLogger`):
`End_WithoutAMerge_LogsNoneForMergeAndObjects`; `End_AfterAMerge_SplitsMergeAndObjectTime`
(start 0, merge at 40 with 3 entries `("a","x")`, `("","")`, `("b","x")` and xslt `["", "", "t.xslt"]`,
end at 55: `files=2 ms=55.00 xslt=1 merge_ms=40.00 objects_ms=15.00 result=ok`);
`CountFiles_SkipsEmptyAndNullPaths_AndANullListIsZero`; `CountXslt_IgnoresIndexZeroAndEmptyEntries`;
`End_WithAnException_LogsItsTypeNameAndCountsItFailed`; `MergeFinished_WithNoCallInFlight_IsIgnored`;
`MergeFinished_Twice_KeepsTheFirst`; `NestedCalls_AttributeTheMergeToTheInnerCall_AndRestoreTheOuter`;
`LogSummary_AggregatesSinceTheLastSummary_ThenResets`; `LogSummary_WithNoCalls_LogsZerosAndNone`;
`LogSummary_GameIsTheLastNonEmptyGameType`; `End_NullCall_DoesNothing`;
`Begin_WhenTheClockThrows_WarnsOnceAndReturnsNull` (a clock fake whose `Now` throws; two `Begin` calls,
one WARN line).

**Verify**: RED, then GREEN with `One test class` and `LoadXmlStampServiceTests`.

### Step 11: the two patches, the wiring and the summary (TDD)

Read `.claude/rules/harmony-patches.md` and the three `harmony-il.md` entries named in Current state
first.

`Hooks/MBObjectManager_LoadXML_StampPatch.cs`:

```csharp
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.LoadXML),
    new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) })]
[HarmonyPatchCategory(LoadTimeStampsModule.LoadXmlCategory)]
public static class MBObjectManager_LoadXML_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(string id, string gameType, out LoadXmlCall? __state)
        => __state = LoadTimeStampsHooks.BeginLoadXml(id, gameType);

    // void: observe only, so Harmony rethrows the original exception with its stack.
    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LoadXmlCall? __state)
        => LoadTimeStampsHooks.EndLoadXml(__state, __exception);
}
```

`Hooks/MBObjectManager_CreateMergedXmlFile_StampPatch.cs` (`CreateMergedXmlFile` has one overload,
`MBObjectManager.cs:962`, so no argument-type array):

```csharp
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile))]
[HarmonyPatchCategory(LoadTimeStampsModule.LoadXmlCategory)]
public static class MBObjectManager_CreateMergedXmlFile_StampPatch
{
    // void: observe only. Runs whether or not another prefix skipped the original; never add a prefix here.
    [HarmonyFinalizer]
    public static void Finalizer(List<Tuple<string, string>> toBeMerged, List<string> xsltList)
        => LoadTimeStampsHooks.MergeFinished(toBeMerged, xsltList);
}
```

with a class summary saying the same.

`LoadTimeStampsHooks` gains `LoadXml` (the service), `InitializeLoadXml(LoadXmlStampService? service)`,
and never-throwing forwarders `BeginLoadXml`, `EndLoadXml`, `MergeFinished`, `LogLoadXmlSummary`
(each `try { ... } catch { }`, returning null where a value is due). The module: `internal const string
LoadXmlCategory = "PatchNNN_LoadTimeStamps_LoadXml";`, `PatchCategories` = `{ new(LoadXmlCategory,
ApplyPhase.ProcessLoad) }`, `RegisterServices` adds `registrator.Register<LoadXmlStampService>(Reuse.Singleton);`,
`InitializeStatics` calls `LoadTimeStampsHooks.InitializeLoadXml(resolver.Resolve<LoadXmlStampService>())`
and logs `LoadTimeStampLines.LoadXmlReady()`.

Tests first:

- `LoadXmlStampPatchShapeTests.cs`, `[TestCategory("RequiresGame")]` (the patch class's attributes name
  an engine type). In the test class declare `public static void Target(string id, bool isDevelopment,
  string gameType, bool skipXmlFilterForEditor)` that appends `"target:" + id` to a static list and,
  when `id == "Throw"`, calls a separate `[MethodImpl(MethodImplOptions.NoInlining)] static void Thrower()`
  that throws `new InvalidOperationException("merge failed")` (a separate frame, so a lost throw site
  is visible: Harmony copies `Target`'s own body into its replacement, but `Thrower` stays a frame of
  its own). In `[TestInitialize]`, call `LoadTimeStampsHooks.InitializeLoadXml(new LoadXmlStampService(clock, logger))`
  over a `FakeStampClock` and a `RecordingLogger`, and patch `Target` with
  `new Harmony("taom.tests.loadxmlstamp")` using `AccessTools.Method(typeof(MBObjectManager_LoadXML_StampPatch), "Prefix")`
  as prefix and `"Finalizer"` as finalizer; in `[TestCleanup]` `UnpatchAll("taom.tests.loadxmlstamp")`
  and `LoadTimeStampsHooks.InitializeLoadXml(null)`. Tests:
  `Patched_TargetSucceeds_RunsOnceAndLogsResultOk`; `Patched_TargetThrows_RethrowsTheSameExceptionWithItsThrowSite`
  (`Assert.ThrowsException<InvalidOperationException>`, message `merge failed`, the target ran exactly
  once, the exception's `StackTrace` contains `Thrower`, and the logged line ends
  `result=InvalidOperationException`); `Patched_NoServiceWired_StillRunsTheTargetAndPropagates` (after
  `InitializeLoadXml(null)`).
- `LoadTimeStampsBindingTests.cs` (`[TestCategory("BindingVerification")]`, the Patch85 exemplar's
  `RequireGame()` pattern): `LoadXML_IsTheOnlyMethodOfThatName_WithTheParameterNamesThePrefixBinds`
  (declared methods named `LoadXML` on `MBObjectManager`: exactly one; parameter names `id`,
  `isDevelopment`, `gameType`, `skipXmlFilterForEditor`; types string, bool, string, bool; returns
  void); `CreateMergedXmlFile_IsStatic_WithTheParameterNamesTheFinalizerBinds` (one overload, static,
  names `toBeMerged`, `xsltList`, `skipValidation`, types `List<Tuple<string, string>>`,
  `List<string>`, `bool`); `LoadXmlPatches_CarryTheirCategory_AndTheModuleDeclaresIt` (both classes
  carry `[HarmonyPatchCategory(LoadTimeStampsModule.LoadXmlCategory)]` and the module's
  `PatchCategories` contains it at `ApplyPhase.ProcessLoad`);
  `LoadXmlTargets_AreOnPatchShieldsExclusionList` (for each of the two patch classes, take its
  target as `CreatureBanditsWiringTests.TargetOf` does, by merging the class's `[HarmonyPatch]`
  attributes, but resolve it with `AccessTools.Method(info.declaringType, info.methodName,
  info.argumentTypes)` so the `LoadXML` overload array is honoured; assert the target is not null
  and `PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name)` is
  true, with the message `"<FullName>.<Name> must be in PatchShieldPolicy.ExcludedTargetMethods"`).
  Put the target-reading helper in this class; Step 16 reuses it.
- `LoadTimeStampsHooksTests`: `LoadXmlForwarders_Unwired_DoNothingAndReturnNull` (`BeginLoadXml`
  returns null; `EndLoadXml(null, null)`, `MergeFinished(null, null)` and `LogLoadXmlSummary()` do
  not throw). `[TestCleanup]` also calls `InitializeLoadXml(null)`.
- `LoadTimeStampsWiringTests`: `SubModule_OnGameInitializationFinished_LogsTheLoadXmlSummaryBeforeTheGuard`
  (`LoadTimeStampsHooks.LogLoadXmlSummary();` appears after `hookStamp?.Mark("armour_gate");` and
  before `hookStamp?.End();` and the guard).

**Verify (RED)**: the test classes fail (missing types and members, then the missing summary call).
Once the patch classes compile, `LoadXmlTargets_AreOnPatchShieldsExclusionList` must fail on its
`must be in PatchShieldPolicy.ExcludedTargetMethods` assertion for `LoadXML` (it runs on this
machine with the game installed; an Inconclusive result is not a RED). Quote that failure.

Then the single `SubModule.cs` edit of this stage: in `OnGameInitializationFinished`, between
`hookStamp?.Mark("armour_gate");` and `hookStamp?.End();`, insert
`Features.LoadTimeStamps.LoadTimeStampsHooks.LogLoadXmlSummary();`.

And the PatchShield edit, `Dependencies/Foundation/PatchShieldPolicy.cs`. First run
`git grep -n '"TaleWorlds.ObjectSystem.MBObjectManager.CreateMergedXmlFile"' -- Dependencies/Foundation/PatchShieldPolicy.cs`:
if it prints a line (plan 042 landed with that entry), append only the `LoadXML` entry below.
Append as the last entries of `ExcludedTargetMethods`, after whatever entry is last in your base:

```csharp
        // The load-time stamps (docs/features/load-time-stamps.md): observe-only TAOM patches that run
        // once per XML type, or once per lifecycle event, per load, so they are not hot. A shield
        // finalizer here would swallow a missing-API exception that propagates today (ending a
        // dispatch early, or returning a null merged document) and add one Harmony.Patch per target
        // to every player's first game start. LoadTimeStampsBindingTests walks the real targets.
        "TaleWorlds.ObjectSystem.MBObjectManager.LoadXML",
        "TaleWorlds.ObjectSystem.MBObjectManager.CreateMergedXmlFile",
```

and in the list's summary comment, after the sentence ending "applied at method granularity instead
of namespace granularity.", add: "It also lists targets that are not hot but where a shield
finalizer would change behaviour an observe-only TAOM patch promises to leave alone (the load-time
stamps)." Edit nothing else in the file.

**Verify (GREEN)**: `Build` exits 0; `One test class` with each of `LoadXmlStampPatchShapeTests`,
`LoadTimeStampsBindingTests`, `LoadTimeStampsHooksTests`, `LoadTimeStampsWiringTests`,
`FeatureModulesTests`, `HarmonyPatchBindingTests`, `PatchShieldPolicyTests` and
`CreatureBanditsWiringTests`: all pass, and `LoadXmlTargets_AreOnPatchShieldsExclusionList` passed
rather than reporting Inconclusive.

### Step 12: stage B docs, suites, commit

- The feature doc: an "XML loads" part of Architecture (the two patches, the thread-local call,
  `merge_ms` versus `objects_ms`, merges outside `LoadXML` not stamped, the coexistence with plan
  042's prefix) and the stage B lines (`[LoadXml] ready` INFO at process start, X1 INFO per call, X2
  INFO per game initialization, the fault WARNING once) in the Log lines table.
- `docs/reference/harmony-patch-registry.md`: a `## PatchNNN_LoadTimeStamps_LoadXml` section after the
  last `## PatchNN_` section and before `<!-- backlinks-start`, modelled on `## Patch91_MissionTickStall`:
  **Target** (`MBObjectManager.LoadXML(string, bool, string, bool)`, prefix plus void finalizer;
  `MBObjectManager.CreateMergedXmlFile`, void finalizer), why, the always-on posture, applied at
  `OnSubModuleLoad` by the module, and "never a prefix on `CreateMergedXmlFile`, never a patch on the
  methods plan 042 watches". Add a **PatchShield** line in the style of the Patch92 and Patch93
  sections: both targets are on `PatchShieldPolicy.ExcludedTargetMethods` (why, and that another
  mod's patch on them loses the rescue).
- The feature doc names the category `PatchNNN_LoadTimeStamps_LoadXml` (your number) in its
  Architecture section, and says the patches apply for every player whatever the toggle, with their
  apply time in the `[PatchApply]` lines, and that PatchShield skips both targets.

**Verify**: normal `Build`; `Tests`: base totals plus every new test, failing set unchanged. The RefAsm
unit step and binding gate: the failure sets Step 1 recorded, no new names, no skipped binding
check. `Docs` exits 0. `git status --porcelain` lists nothing beyond the Step 1 entries and the stage B
files. Commit stage B.

---

**Stage C: campaign handlers**

### Step 13: stage C line formats (TDD)

Add to `LoadTimeStampLines` (stubs first) and pin:

```csharp
internal static string LifecycleReady(string? bindingProblem);
internal static string Handler(string evt, string handler, string assembly, int calls, double ms, double maxMs, int maxIndex);  // C1
internal static string EventTotal(string evt, int listeners, int taomListeners, double ms, double taomMs, double otherMs,
                                  int overThreshold, double maxMs, string? maxHandler);                                         // C2
internal static string Dispatch(string dispatch, double ms, double? listenersMs, string result);                                // C3
internal static string LifecycleOff(string problem);                                                                             // C4
```

| Call | Returns |
|---|---|
| `LifecycleReady(null)` | `[Lifecycle] ready: listener binding ok; with "Enable Load-Time Stamps" on, every new-game, game-loaded and session-start handler is timed, with a line for each at or over 10.00 ms` |
| `LifecycleReady("MbEvent<CampaignGameStarter>._nonSerializedListenerList not found")` | `[Lifecycle] ready: listener binding missing (MbEvent<CampaignGameStarter>._nonSerializedListenerList not found); with "Enable Load-Time Stamps" on, only dispatch totals are written` |
| `Handler("OnNewGameCreatedPartialFollowUp", "CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp", "TAOM", 100, 3021.5, 2990.25, 1)` | `[Lifecycle] event=OnNewGameCreatedPartialFollowUp handler=CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp asm=TAOM calls=100 ms=3021.50 max_ms=2990.25 max_index=1` |
| `Handler("OnSessionLaunched", "X.OnSessionLaunched", "SandBox", 1, 12, 12, -1)` | `[Lifecycle] event=OnSessionLaunched handler=X.OnSessionLaunched asm=SandBox calls=1 ms=12.00 max_ms=12.00 max_index=none` |
| `EventTotal("OnNewGameCreated", 84, 20, 3400, 120, 3280, 3, 2100, "HeroSpawnCampaignBehavior.OnNewGameCreated")` | `[Lifecycle] event=OnNewGameCreated total listeners=84 taom_listeners=20 ms=3400.00 taom_ms=120.00 other_ms=3280.00 over_threshold=3 max_ms=2100.00 max_handler=HeroSpawnCampaignBehavior.OnNewGameCreated` |
| `EventTotal("OnGameEarlyLoaded", 0, 0, 0, 0, 0, 0, 0, null)` | `[Lifecycle] event=OnGameEarlyLoaded total listeners=0 taom_listeners=0 ms=0.00 taom_ms=0.00 other_ms=0.00 over_threshold=0 max_ms=0.00 max_handler=none` |
| `Dispatch("OnNewGameCreated", 6650, 6600, "ok")` | `[Lifecycle] dispatch=OnNewGameCreated ms=6650.00 listeners_ms=6600.00 result=ok` |
| `Dispatch("OnSessionStart", 40, null, "NullReferenceException")` | `[Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=NullReferenceException` |
| `LifecycleOff("boom")` | `[Lifecycle] per-handler timing off for this session: boom; dispatch totals are still written` |

The example handler names are shapes, not measurements. **Verify**: RED, then GREEN.

### Step 14: the lifecycle service against a fake adapter (TDD)

Domain types (`Main/Features/LoadTimeStamps/Domain/`, no engine types):
`public enum LifecycleDispatch { OnNewGameCreated, OnGameEarlyLoaded, OnGameLoaded, OnSessionStart, OnAfterSessionStart }`;
`public enum LifecycleEvent { OnNewGameCreated, OnNewGameCreatedPartialFollowUp, OnNewGameCreatedPartialFollowUpEnd, OnGameEarlyLoaded, OnGameLoaded, OnSessionLaunched, OnAfterSessionLaunched }`;
`public sealed class ListenerInfo` (ctor `(string handler, string assembly, bool isTaom)`, three
get-only properties).

`Main/Adapters/ICampaignListenerAdapter.cs`:

```csharp
public interface ICampaignListenerAdapter
{
    /// <summary>Null when every listener-list member resolved; otherwise the first one missing.</summary>
    string? BindingProblem { get; }

    /// <summary>Swaps each listener of the event for a wrapper that times the original call and
    /// reports (listener index in invoke order, elapsed ticks, the int argument or -1); returns the
    /// listeners in invoke order. Throws only on a reflection failure, after undoing its own swaps.</summary>
    IReadOnlyList<ListenerInfo> WrapListeners(LifecycleEvent lifecycleEvent, Action<int, long, int> record);

    /// <summary>Puts back every original this adapter swapped for the event; safe to call twice.</summary>
    void RestoreListeners(LifecycleEvent lifecycleEvent);
}
```

`LifecycleTimingService` (`public sealed class`, ctor `(ICampaignListenerAdapter adapter,
LoadStampDetailGate gate, IStampClock clock, IModLogger logger)`):

- `internal static IReadOnlyList<LifecycleEvent> EventsOf(LifecycleDispatch d)`: OnNewGameCreated gives
  `[OnNewGameCreated, OnNewGameCreatedPartialFollowUp, OnNewGameCreatedPartialFollowUpEnd]`;
  OnGameEarlyLoaded `[OnGameEarlyLoaded]`; OnGameLoaded `[OnGameLoaded]`; OnSessionStart
  `[OnSessionLaunched]`; OnAfterSessionStart `[OnAfterSessionLaunched]`.
- `public LifecycleDispatchScope? Begin(LifecycleDispatch dispatch)`: null when `gate.Enabled` is
  false. Otherwise a scope with the start tick. If `adapter.BindingProblem` is set, or a previous
  wrap failed this session, log `LifecycleOff(<problem>)` at WARNING once per process and return the
  scope without listeners. Else wrap each event of `EventsOf` in order, keeping per listener: calls,
  summed ticks, max ticks and the argument of the max call. If a wrap throws: restore every event
  wrapped so far (and the failing one), mark per-handler timing off for the session, log
  `LifecycleOff("<ExceptionType>: <message>")` once, and continue with a scope without listeners.
- `public void End(LifecycleDispatchScope? scope, Exception? exception)`: nothing for null. In a
  `try/finally` whose `finally` restores every wrapped event: for each event in order, one `Handler`
  line per listener whose summed ms is at least `HandlerThresholdMs`, in invoke order, then its
  `EventTotal` (`taom_ms` sums listeners with `IsTaom`; `other_ms` is the rest; `max_handler` is the
  listener with the largest summed ms, first on a tie, `none` without listeners); then `Dispatch`
  (`ms` from the scope's start, `listeners_ms` the sum over its events or `null` without listeners,
  `result` `ok` or the exception's type name). All INFO. `Begin` and `End` catch their own
  exceptions and log `LifecycleOff` once.

`LifecycleDispatchScope` (`public sealed class`) holds the dispatch, start tick and the per-event
records; nothing else uses it.

`TAOM.Tests/Features/LoadTimeStamps/FakeCampaignListenerAdapter.cs`: settable `BindingProblem`,
`Listeners` per event, `ThrowOnWrap` (an event), lists `Wrapped` and `Restored`, the captured
`record` callback per event and `Fire(LifecycleEvent e, int index, long ticks, int arg)` to simulate a
call. `LifecycleTimingServiceTests.cs` (no category):

- `Begin_DetailOff_ReturnsNullAndWrapsNothing`.
- `Begin_EachDispatch_WrapsItsEventsInOrder` (`[DataTestMethod]`, one row per dispatch).
- `End_AHandlerAtTheThreshold_GetsALine_AndOneJustUnderDoesNot` (10 ms gets a line, 9 ms does not;
  with `Frequency = 1000`, ticks are milliseconds).
- `End_EventTotal_SplitsTaomAndOtherTime_AndCountsOverThreshold`.
- `End_PartialFollowUp_AggregatesCallsAndKeepsTheArgumentOfTheSlowestCall`.
- `End_LinesComeHandlersThenEventTotalPerEvent_ThenTheDispatchLine`.
- `End_RestoresEveryWrappedEvent_EvenWhenTheLoggerThrows`.
- `End_DispatchThrew_LogsTheExceptionTypeAsResult`.
- `Begin_BindingProblem_WarnsOnceAcrossDispatches_AndEndLogsTheDispatchWithListenersNone`.
- `Begin_WrapThrows_RestoresWhatItWrapped_WarnsOnce_AndLaterDispatchesWrapNothing`.
- `End_NullScope_DoesNothing`.

**Verify**: RED, then GREEN with `LifecycleTimingServiceTests`.

### Step 15: the adapter on real engine events (TDD)

`Main/Adapters/CampaignListenerAdapter.cs` (`public sealed class CampaignListenerAdapter :
ICampaignListenerAdapter`, ctor `(IStampClock clock)`). At construction resolve, for
`typeof(MbEvent<CampaignGameStarter>)` and `typeof(MbEvent<CampaignGameStarter, int>)`:
`AccessTools.Field(eventType, "_nonSerializedListenerList")`; its `FieldType` (the closed record
type); on it `AccessTools.Field(recType, "Next")`, `AccessTools.Property(recType, "Action")` with
`GetSetMethod(true)`, `AccessTools.Property(recType, "Owner")`. Cache them; the first one missing
sets `BindingProblem` to `"<EventType>.<member> not found"` (for example
`MbEvent<CampaignGameStarter>._nonSerializedListenerList not found`) and the constructor never throws.

`WrapListeners` maps the event to its `MbEvent` through the public statics
(`CampaignEvents.OnNewGameCreatedEvent`, `OnNewGameCreatedPartialFollowUpEvent`,
`OnNewGameCreatedPartialFollowUpEndEvent`, `OnGameEarlyLoadedEvent`, `OnGameLoadedEvent`,
`OnSessionLaunchedEvent`, `OnAfterSessionLaunchedEvent`) cast with `as` to `MbEvent<CampaignGameStarter>`
or `MbEvent<CampaignGameStarter, int>` (null: throw `InvalidOperationException` naming the event), and
calls one of two internal methods the tests use directly:

```csharp
internal IReadOnlyList<ListenerInfo> WrapOne(MbEvent<CampaignGameStarter> mbEvent, LifecycleEvent key, Action<int, long, int> record);
internal IReadOnlyList<ListenerInfo> WrapTwo(MbEvent<CampaignGameStarter, int> mbEvent, LifecycleEvent key, Action<int, long, int> record);
```

Each walks the list from its head (invoke order), and for listener `i` reads the original delegate
and the owner, builds

```csharp
Action<CampaignGameStarter> wrapper = starter =>
{
    long start = _clock.Now;
    try { original(starter); }
    finally { Report(record, index, _clock.Now - start, -1); }   // Report swallows any exception of record
};
```

(the two-argument form passes its `int` as the argument), sets it through the setter, and remembers
`(record object, original, wrapper)` under `key`. `RestoreListeners(key)` sets each original back
only where the record still holds this adapter's wrapper (`ReferenceEquals`), then forgets the key.
`ListenerInfo`: the owner's type, or `original.Method.DeclaringType` when the owner is null;
`handler` = `<type Name>.<original.Method.Name>`; `assembly` = the type's assembly `GetName().Name`;
`isTaom` = that assembly is `typeof(CampaignListenerAdapter).Assembly`. A reflection failure mid-walk
undoes this call's swaps and rethrows. Corrected after the Codex review (2026-10-03): `assembly` and
`isTaom` come from `original.Method.DeclaringType` (the owner's type only when that is null, as for a
dynamic method), because `MbEvent` stores the owner as a removal token that need not implement the
callback; `handler` keeps the owner's type name. Corrected again after the convergence review: a
callback that is this adapter's own wrapper (a closure nested in the adapter, which a record keeps
after a failed restore or a second wrap) is no evidence either, so the owner's type stands in for it
too. The adapter also gained an internal `BeforeWrite` seam, so a test can fail a write midway and
check the rollback on real records.

`CampaignListenerAdapterTests.cs`, `[TestCategory("RequiresGame")]` (it runs engine code), on fresh
`new MbEvent<CampaignGameStarter>()` and `new MbEvent<CampaignGameStarter, int>()` instances (no
campaign needed; invoke with a null starter):

- `Constructor_AgainstTheInstalledEngine_ResolvesEveryMember` (`BindingProblem` is null).
- `WrapOne_KeepsTheInvokeOrder_AndReportsEachListenerOnce` (three listeners added A, B, C run C, B, A
  both before and after wrapping; record called once per listener with indexes 0, 1, 2 matching the
  returned infos).
- `WrapOne_AListenerThrows_TheSameExceptionLeavesInvoke_AndItsTimeIsStillReported`.
- `RestoreListeners_PutsBackTheOriginalDelegates` (read `Action` through reflection and compare by
  reference with the delegates the test added).
- `RestoreListeners_Twice_IsHarmless`.
- `WrapOne_AListenerThatClearsAnotherOwnersListener_DoesNotBreakTheDispatchOrTheRestore`.
- `WrapTwo_ReportsTheIntArgument` (invoke with `(null, 7)`: argument 7).
- `ListenerInfo_NamesTheOwnerTypeAndMethod_AndMarksTaomOwners` (an owner from the test assembly is
  not TAOM; `new StopwatchStampClock()`, a type from `Main` with a public parameterless constructor,
  is).

Add to `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, after the last row, a comment line and
ten rows (source site `CampaignListenerAdapter.cs`):
`("TaleWorlds.CampaignSystem.MbEvent`1", "MbEvent`1", "_nonSerializedListenerList", "Field")`,
`("TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1", "EventHandlerRec`1", ...)` with `Next`
Field, `Action` Property, `set_Action` Method, `Owner` Property; and the same five for `MbEvent`2` and
`MbEvent`2+EventHandlerRec`2`.

**Verify**: RED (the adapter tests do not compile, then fail on stubs), then GREEN with
`CampaignListenerAdapterTests` and `ReflectionSiteBindingTests`. If any adapter test shows a changed
invoke order, a swallowed or replaced exception, or an original not restored, STOP.

### Step 16: the five dispatch patches and the module wiring (TDD)

`Hooks/CampaignEventDispatcher_Lifecycle_StampPatches.cs`: five public static classes, one per
dispatcher method, each

```csharp
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnNewGameCreated))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnNewGameCreated_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnNewGameCreated);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}
```

and the same for `OnGameEarlyLoaded`, `OnGameLoaded`, `OnSessionStart` and `OnAfterSessionStart`.
`LoadTimeStampsHooks` gains `Lifecycle`, `InitializeLifecycle(LifecycleTimingService? service)`, and
never-throwing `BeginDispatch`/`EndDispatch`. The module: `internal const string LifecycleCategory =
"PatchNNN_LoadTimeStamps_Lifecycle";` declared at `ApplyPhase.ProcessLoad` (its own category, so an
engine drift here never costs the XML stamp: the `Patch89` precedent);
`registrator.Register<ICampaignListenerAdapter, CampaignListenerAdapter>(Reuse.Singleton);` and
`registrator.Register<LifecycleTimingService>(Reuse.Singleton);`; `InitializeStatics` passes the
service and logs `LoadTimeStampLines.LifecycleReady(resolver.Resolve<ICampaignListenerAdapter>().BindingProblem)`.

Tests first, in `LoadTimeStampsBindingTests`:
`CampaignEventDispatcher_LifecycleMethods_AreSingleOverloadsTakingTheStarter` (each of the five names:
exactly one declared method on `CampaignEventDispatcher`, one parameter of type
`CampaignGameStarter`); `CampaignEvents_LifecycleEventAccessors_HaveTheListenerTypes` (the seven
static properties exist; six are `IMbEvent<CampaignGameStarter>`, `OnNewGameCreatedPartialFollowUpEvent`
is `IMbEvent<CampaignGameStarter, int>`); `LifecyclePatches_CarryTheirCategory_AndTheModuleDeclaresIt`;
`LifecycleTargets_AreOnPatchShieldsExclusionList` (the Step 11 helper over the five patch classes,
the same assertion). In `LoadTimeStampsHooksTests`:
`LifecycleForwarders_Unwired_DoNothingAndReturnNull` (`BeginDispatch` returns null for every
`LifecycleDispatch`; `EndDispatch(null, null)` does not throw); `[TestCleanup]` also calls
`InitializeLifecycle(null)`.

**Verify (RED)**: the tests fail on the missing types, then, once the patch classes compile,
`LifecycleTargets_AreOnPatchShieldsExclusionList` fails on its assertion for a dispatcher method
(not Inconclusive). Quote it.

Then append to `ExcludedTargetMethods` in `Dependencies/Foundation/PatchShieldPolicy.cs`, right
after the stage B entries:

```csharp
        // The load-time stamps' campaign-handler timing: one call per lifecycle event per load. A shield
        // swallow here would end the dispatch early and skip the remaining listeners and receivers.
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnNewGameCreated",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnGameEarlyLoaded",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnGameLoaded",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnSessionStart",
        "TaleWorlds.CampaignSystem.CampaignEventDispatcher.OnAfterSessionStart",
```

**Verify (GREEN)**: `One test class` with each of `LoadTimeStampsBindingTests` (both PatchShield
walks pass, not Inconclusive), `LoadTimeStampsHooksTests`, `HarmonyPatchBindingTests`,
`PatchShieldPolicyTests` and `FeatureModulesTests`: all pass.

### Step 17: stage C docs, every check, commit

- The feature doc: the "Campaign handlers" part of Architecture (the swap, why it is behaviour-neutral,
  why every owner is timed, the five dispatcher targets and what `ms - listeners_ms` means, what is
  not timed: the rest of `Campaign.OnSessionStart` such as `ConversationManager.Build`); the stage C
  lines in the Log lines table (`[Lifecycle] ready` INFO at process start, C1, C2 and C3 INFO per
  dispatch with the toggle on, C4 WARNING once); a "Reading a load" paragraph: a new campaign writes
  `[LoadXml]` lines and a summary, the `OnGameInitializationFinished` stamps, then the
  `OnNewGameCreated`, `OnSessionStart` and `OnAfterSessionStart` dispatches; a save load writes
  `OnGameEarlyLoaded` and `OnGameLoaded` instead of `OnNewGameCreated`; a custom battle has no
  `[Lifecycle]` lines. Add a caution: with the toggle on, an exception thrown inside any campaign
  handler during these dispatches carries one TAOM wrapper frame in its stack. Add a limit: a
  listener added during a dispatch (for example a PartialFollowUp listener registered by an
  `OnNewGameCreated` handler; `AddNonSerializedListener` inserts at the head) is not wrapped, so it
  runs untimed and is missing from `listeners=`. Name the category
  `PatchNNN_LoadTimeStamps_Lifecycle` and say it applies for every player whatever the toggle, and
  that PatchShield skips the five dispatcher methods.
- `docs/reference/harmony-patch-registry.md`: `## PatchNNN_LoadTimeStamps_Lifecycle` after the stage B
  section (targets, the swap, default off, restore in the finalizer, and a **PatchShield** line as in
  stage B: the five targets are excluded, why, and the trade-off).
- `docs/reference/taleworlds-api-snapshot/reflection-sites.md` Category B: ten rows (the
  `MbEvent`1`/`MbEvent`2` list fields and the record members, source `CampaignListenerAdapter.cs`,
  "What it drives": "LoadTimeStamps per-handler timing; missing: dispatch totals only") and a dated
  `Status (...)` line saying they were added and resolve against the installed v1.5.3 (quote the test
  run).

**Verify**: normal `Build`; `Tests`: base totals plus every new test, the failing set unchanged.
RefAsm unit step and binding gate: the Step 1 failure sets, no new names, no skipped binding check.
`Docs` exits 0. `Data` reports 0 ERRORs. The dash check (below) prints 0. `git status --porcelain`
lists nothing beyond the Step 1 entries and the stage C files. Commit stage C.

## Test plan

- New pure tests (run on hosted CI): `LoadTimeStampLinesTests` (every format, one row each, plus the
  culture test), `LoadStampDetailGateTests`, `HookStampServiceTests`, the new
  `PatchCategoryApplierTests` (fake clock, held lines, the overhead measurement),
  `LoadTimeStampsHooksTests` (the never-throwing forwarders, unwired and with a throwing service),
  `LoadXmlStampServiceTests`, `LifecycleTimingServiceTests`, `LoadTimeStampsWiringTests` (source),
  `BattleLoadDiagnosticsSettingsProviderTests.LoadTimeStampsEnabled_NoMcmInstance_DefaultsFalse`.
- `RequiresGame`: `LoadXmlStampPatchShapeTests` (real Harmony around a stand-in with `LoadXML`'s
  parameter names: the exception type, message and throw site survive, the target runs once) and
  `CampaignListenerAdapterTests` (real `MbEvent` instances: order, exceptions, restore).
- `BindingVerification`: `LoadTimeStampsBindingTests` (signatures, categories, and the two PatchShield
  walks over all seven real targets) and the ten `ReflectionSiteBindingTests` rows.
- Patterns: `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` (real Harmony in a unit test),
  `TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs` (binding),
  `SettingsFingerprintTests.cs:449-458` (recording logger).
- Not testable offline, for the `Not-tested:` trailers: the stamps inside a real game load (the patch
  groups' real times, the hook steps, the two `MBObjectManager` patches inside the engine's load, the
  listener swap inside a real new game and save load, and the MCM toggle read in game).

## Done criteria

ALL must hold:

- [ ] `Build` exits 0, and after the final stage the full `Tests` run shows the base's totals plus
      every test this plan added, all of them passing, and the failing set of Step 1 unchanged.
- [ ] The RefAsm unit step and binding gate show the failure sets of Step 1 and no new names; the
      gate reports no skipped check.
- [ ] `git grep -l "PatchNNN_LoadTimeStamps_LoadXml" -- Main Dependencies TAOM.Tests docs` and the
      same for `PatchNNN_LoadTimeStamps_Lifecycle` (your number) each print exactly
      `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs`, `docs/features/load-time-stamps.md` and
      `docs/reference/harmony-patch-registry.md` (the patch classes and tests use the module's
      constants, and the feature-map row names no category); `git grep -n "PatchNNN_" -- Main/SubModule.cs`
      prints nothing.
- [ ] PatchShield: `git grep -nE '"TaleWorlds\.(ObjectSystem\.MBObjectManager\.(LoadXML|CreateMergedXmlFile)|CampaignSystem\.CampaignEventDispatcher\.On(NewGameCreated|GameEarlyLoaded|GameLoaded|SessionStart|AfterSessionStart))"' -- Dependencies/Foundation/PatchShieldPolicy.cs`
      prints exactly 7 lines (0 at `0912e1b7`; 7 on a scratch copy with the entries, checked when
      this plan was revised), and `LoadXmlTargets_AreOnPatchShieldsExclusionList` and
      `LifecycleTargets_AreOnPatchShieldsExclusionList` passed (not Inconclusive) in the final run.
      `git diff <your base>..HEAD -- Dependencies` touches only `PatchShieldPolicy.cs`.
- [ ] `git grep -n "LoadTimeStampsHooks" -- Main/SubModule.cs` lists exactly the insertions of Steps 7
      and 11 (StartHook three times, DetailEnabled twice, LogLoadXmlSummary once).
- [ ] `git grep -n "ApplyXslt\|MergeTwoXmls\|ToXDocument\|ToXmlDocument\|CreateDocumentFromXmlFile\|LoadXmlWithValidation" -- Main/Features/LoadTimeStamps`
      prints nothing.
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0 and `python tools/validate_moduledata.py`
      reports 0 ERRORs.
- [ ] Dash check: `git diff <your base>..HEAD -U0 -- Main Dependencies TAOM.Tests docs | grep '^+' | grep -c -e $'\xe2\x80\x94' -e $'\xe2\x80\x93'`
      prints `0`.
- [ ] `git log --oneline <your base>..HEAD` shows three commits with the subjects of Git workflow;
      `git status --porcelain` lists no entry beyond those recorded at Step 1.
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the formats in the tables above are drafts: your implementation and its tests are
      the authority, and the doc copies the tests).

## STOP conditions

Stop and report (do not improvise) if:

- The code at a "Current state" anchor does not match its excerpt (other than the expected moves
  named in the drift check).
- Wrapping `LoadXML` changes its exception behaviour or order: `LoadXmlStampPatchShapeTests` shows a
  different exception type or message, a lost throw site, or the target running other than once.
- `CampaignListenerAdapterTests` shows a changed invoke order, a swallowed or replaced exception, or an
  original delegate not restored.
- A TaleWorlds signature or member differs from "Current state" (a binding test fails): report the
  mismatch; do not decompile and adapt.
- An existing source gate (`MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper`,
  `Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`, `IoCResolver_IsReadOnlyByTheFeatureModuleHooks`,
  the `SubModuleSource_*` tests) fails after a `SubModule.cs` insertion: do not edit the gate.
- The overhead test measures 50 microseconds or more per category: do not raise the bound; report
  the number (decision 1 then needs the maintainer).
- The settings-count phrases of Step 3 are not in the docs, or the fingerprint test needs anything but
  the one `reflected` number moved.
- The work seems to need `Main/IoC.cs`, a csproj, a protected file, a patch on one of the
  `MBObjectManager` methods listed out of scope, or a change to what loads.
- A target this plan patches is not on `PatchShieldPolicy.ExcludedTargetMethods` by the end of its
  stage, or a PatchShield walk test reports Inconclusive on this machine (the game assemblies did not
  load, so the check proved nothing), or the PatchShield exclusion seems to need any edit to
  `Dependencies/` besides the appended entries and the one summary sentence (for example to
  `PatchShield.cs`, the namespace list or the owner list).
- `PatchNNN` cannot be chosen in 100 to 109.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (labels `diagnostics`, `perf`; draft in
  `plans/_audit/2026-10-02-perf/issue-drafts.md` "## 040") and give the executor its number.
- No `/localize`: log lines are not player-facing text, and MCM setting strings are not localized
  anywhere in TAOM.
- After review, `/verify-bindings` to refresh `docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (seven new targets: `MBObjectManager.LoadXML`, `MBObjectManager.CreateMergedXmlFile` and five
  `CampaignEventDispatcher` methods).
- `/deep-review` on the branch (C# plus Harmony): ask it to probe the listener swap and restore, the
  `void` finalizers, the always-on posture of stamp (1) and of the two categories, and the seven
  PatchShield exclusions with their trade-off.
- When plan 028, 041 or 042 merges before or after this one: re-anchor the settings counts (each adds
  its own) and the `SubModule.cs` insertions (042 also inserts one line before the
  `_gameInitPatchesApplied` guard). Plans 028, 039 and 041 also append to
  `PatchShieldPolicy.ExcludedTargetMethods`: a textual merge conflict at the end of that list keeps
  every side's entries.
- Plan 042 coordination: this plan puts `TaleWorlds.ObjectSystem.MBObjectManager.CreateMergedXmlFile`
  on PatchShield's exclusion list. Plan 042 says (its "Current state", the call-rate bullet) that no
  `PatchShieldPolicy` exclusion is needed there and lists PatchShield's Harmony id as a known
  co-owner of that method. Before 042 is dispatched, or when its reviser next runs, tell it: the
  entry exists (or will) because of 040, 042 must not add a second copy, and its stand-aside filter
  simply no longer sees a PatchShield finalizer on that method once 040 lands.
- Decide with the maintainer whether to follow up on Maintenance note "Lifecycle only when on"
  once the field `[PatchApply]` lines show what the `Lifecycle` category costs to apply. (Superseded
  2026-10-03: the dispatch line is written for every player, so the follow-up no longer fits.)

## After merge: the maintainer's actions

- Pull, build and deploy as usual. With the toggle OFF, start a new campaign: expect at boot
  `[LoadStamps] ready: ...`, `[LoadXml] ready: ...`, `[Lifecycle] ready: listener binding ok; ...`,
  `[PatchApply] phase=OnSubModuleLoad scope=total ...` and `phase=MainMenu scope=total ...`; during
  the load one `[LoadXml] id=...` line per type, `[LoadStamps] detail off: ...`,
  `[LoadXml] summary game=Campaign ...`, `[PatchApply] phase=GameInit scope=total ...` and one
  `[Lifecycle] dispatch=...` line each for `OnNewGameCreated`, `OnSessionStart` and
  `OnAfterSessionStart` (`listeners_ms=none result=ok`), with no `[Lifecycle] event=` line. A save
  load writes `OnGameEarlyLoaded`, `OnGameLoaded`, `OnSessionStart` and `OnAfterSessionStart`
  instead of `OnNewGameCreated`.
- Turn ON "Enable Load-Time Stamps" (Battle Load Diagnostics page), start another new campaign and
  load a save, in the same session: expect `[LoadPhase] hook=OnGameStart ...`,
  `[LoadPhase] hook=OnGameInitializationFinished ...`, `[Lifecycle] event=... scope=total ...` and
  `[Lifecycle] dispatch=OnNewGameCreated ...` (new game) or `dispatch=OnGameLoaded ...` (save).
  These read the toggle live, with no restart. Check that the two silent stretches of a new game now
  have handler lines, and that the game behaves as before.
- For the per-category lines, restart the game with the toggle still ON and start a new campaign:
  expect `[PatchApply] phase=... category=...` lines at its game initialization. They are decided
  once per process, at the first game initialization, so the toggle-OFF campaign above (and any
  campaign after it in that session) cannot write them.
- Start a custom battle: `[LoadXml]` lines and summary, the hook stamps, no `[Lifecycle]` lines.
- In `diag.log` after the first game start, PatchShield's pass 2 line: compare its `attached:` count
  with a pre-merge log; it should not have grown by this plan's seven targets.

## Maintenance notes

- Plan 042 adds a prefix on `CreateMergedXmlFile`; this plan's finalizer there still runs when 042
  skips the original, and 042 stands aside only for patches by owners other than `com.taom.mod` and
  PatchShield, so the two coexist. With this plan's exclusion, PatchShield no longer attaches to that
  method at all, for 042's prefix too. Once 042 lands, `[XmlMerge]` gives the merge breakdown per
  type and `[LoadXml]` gives merge plus object creation per `LoadXML`; the review should confirm both
  lines appear for each type and that 042's fast path still reports `path=fast`.
- **Lifecycle only when on** (deferred): the five dispatcher patches apply for every player although
  only toggle-on players use them. Applying `PatchNNN_LoadTimeStamps_Lifecycle` only when the toggle
  is on at the first game initialization would save five `Harmony.Patch` calls per process for
  everyone else, at the price of a conditional category in the composition root (none exists:
  `PatchCategoryDecl` holds only a name and a phase) and a restart to turn per-handler timing on. The
  per-category `[PatchApply]` line for that category measures the saving; decide from the field
  numbers. The PatchShield exclusions stay either way: pass 2 reruns at every game start.
  Superseded 2026-10-03: the `[Lifecycle] dispatch=` line is now written for every player, so every
  player uses those patches and this follow-up no longer fits (the feature doc's changelog has the
  decision).
- PatchShield's exclusion list now carries two kinds of entry: hot targets (the original reason)
  and observe-only targets where a shield would change behaviour. A future reviewer pruning the list
  as "not hot" must read the comment above the load-time entries.
- Merges outside `LoadXML` (GameText, about 1.7 s of the 2026-10-02 campaign load, and BannerIcons)
  are not stamped here; 042's `[XmlMerge]` covers them. If 042 is rejected, a follow-up can time them
  from the same `CreateMergedXmlFile` finalizer plus a prefix.
- The `[Lifecycle]` swap depends on `MbEvent`'s private record layout (pinned by the reflection rows).
  An engine change there turns per-handler timing off with one WARNING and keeps dispatch totals.
- Not timed: the rest of `Campaign.OnSessionStart` after the dispatcher (`ConversationManager.Build`,
  each settlement's `OnSessionStart`, the managers' `RegisterEvents`) and `Campaign.OnGameLoaded`'s
  `AfterLoad` calls. If the dispatch lines do not explain a silent stretch, a follow-up adds a
  `[LoadPhase]` stamp around those private methods.
- Review probes: `PatchCategoryApplier.TryApply`'s `finally` (the time is recorded on both paths and
  the return values are unchanged); the order of the `SubModule.cs` insertions relative to the
  `_gameInitPatchesApplied` guard; that every forwarder in `LoadTimeStampsHooks` swallows its
  exceptions and never runs inside a `finally` that could replace an engine exception.
- Deferred: making the per-type `[LoadXml]` lines toggle-gated if their volume ever matters (about
  30 INFO lines per load today); timing `ManualPatchApplicator` per patch (it is one step of
  `GameInitOnce`).
