# Plan 125: Open the campaign screens faster (Shadowfax screen-open series) and cut the enlistment board's render cost

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Do not edit `plans/README.md`: every WP branch shares
> it, so the integrator records this plan's status and test count when merging. Put the numbers
> listed in Step 15's hand-off report in your final message instead.
>
> **Drift check (run first, Step 0.3)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs Main/Features/Enlistment/ServiceStatusService.cs Main/Adapters/EncounterAdapter.cs Main/Composition/FeatureModules.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/MapPerf Dependencies/Foundation/PatchShieldPolicy.cs TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs TAOM.Tests/Features/CoopInterop TAOM.Tests/Migration/ReflectionSiteBindingTests.cs TAOM.Tests/Features/MapPerf docs/features/enlistment.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/features/map-perf-profiler.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot`
> Earlier WPs of this programme are expected to have changed most of these (WP105 the module list,
> WP107 and WP117 `PatchShieldPolicy.cs` and `Main/Features/MapPerf`, WP112 `EncounterAdapter.cs`,
> several WPs the settings census). Compare the live files against "Current state" and STOP only on
> a real mismatch: an anchor this plan names is missing, or `ServiceStatusTextWriter.cs` /
> `ServiceStatusService.cs` no longer match the excerpts (no earlier WP is meant to touch them).

## Status

- **Priority**: P2
- **Effort**: L (three parts: a small hand port, a 55-file end-state transplant with shared-file
  edits and a feature doc, and an optional profiler extension)
- **Risk**: MED (Harmony transpilers and caches on four vanilla view models; every rewrite
  soft-fails to vanilla and is pinned against the installed IL; the four fast-open switches ship ON)
- **Depends on**: WP117 (`plans/117-frame-and-memory-instruments.md`: MapPerf `f6bf0e588`, needed by
  Part C only) and WP118 (`plans/118-campaign-load-time.md`: Load-Time Stamps `346f3e093`, needed by
  Part B). Transitively also WP104 (`TAOM.Tests/Infrastructure/RepoPaths.cs`, `TryPatchCategory`),
  WP105 (`Main/Composition`), WP107 (`PatchShieldPolicy.ExcludedTargetMethods`) and WP109
  (`Main/Features/MissionPerf/TickProfilerTranspiler.cs`). Step 0 checks every one.
- **Category**: perf
- **Wave / lane**: wave 3, lane C (parallel with WP123, WP124, WP126; WP127 follows WP123)
- **Executor**: laptop
- **Decisions applied**: D6 (the screen-open series as ONE squashed change at its pre-merge net
  state; `f48ef74dc` instant transitions NOT ported), D2 (it arrives as `ShadowfaxScreensModule` plus
  one `FeatureModules.All` line, keeping the 1.5.x patch numbers Patch127 to Patch130 and its wiring
  tests; no `Main/IoC.cs` or `Main/SubModule.cs` edit), D7 (no co-op client split: no `Client/`, no
  `[ClientOnly]`, no `NullQuestJournalAdapter`, no dedicated-server branch), D1 (no plan stages
  `CHANGELOG.md`; the commit body is the changelog entry), D8 (subjects carry `v2.0.28`), D9 (no CI;
  every gate runs locally), D10 (ordinary session), D19 (git only through the Bash tool)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04. (The 1.5.x tip has since moved, to `ca1bc5f2e` at review; no commit after the
  `963218f7f` merge touches `Main/Features/ShadowfaxScreens`, `TAOM.Tests/Features/ShadowfaxScreens`,
  `Main/Features/MapPerf` or the two enlistment board files, re-checked this session.)
- **Issue**: Port of #1019 (enlistment board render and probes) and #851, #852, #853, #854 (clan,
  inventory and trade, party, quest log fast open). No new issue.

## Why this matters

On v1.4.8 four campaign screens repeat work while they build: the clan screen builds its parties list
three times per open, the inventory and trade screen name-sorts rows it re-sorts a moment later, the
party screen recomputes a character code per troop row, and the quest log builds every finished
quest's stages up front (engine lines in "Current state"). TAOM saves carry large clans, stocks,
armies and quest histories, so every open pays for it; the 1.5.x line removed the repeats behind four
MCM switches plus an off-by-default timer that measures each switch. Separately, the enlisted
soldier's wait menu re-renders its whole nested status board every frame, because
`GameMenuVM.IsMenuTextChanged` reports a text with no attributes as changed on every frame
(`GameMenuVM.cs:522`); rendering the board once per change makes each frame a flat pass. Nothing the
player sees changes, and the timer and the map profiler's menu probes say whether each saving is real.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install (reading it is fine).
- Base branch: local `bannerlord-1.4.5` after WP101 (fast-forwarded to origin, merged with the #697
  public tree) plus every WP merged before this one; WP117 and WP118 must be among them.
- Worktree: `C:\Users\mikew\source\repos\taom-port\125`, branch `port148/125-campaign-ui-perf`. The
  folder is outside the session's working directory, so a `cd` does not persist: start EVERY Bash
  command with `cd /c/Users/mikew/source/repos/taom-port/125 &&`.
- Scratch and message files go in `C:\Users\mikew\source\repos\taom-port\` (outside the worktree, so
  they can never be staged). Python: `python`, or `/c/Python314/python.exe` if Git Bash cannot find it.
- Set `MSYS_NO_PATHCONV=1` before any `git show <ref>:.<dir>/...` (Git Bash mangles `ref:.dir`).

### What the three parts port

| Part | Source (1.5.x) | Method | 1.4.x target |
|---|---|---|---|
| A | `943ab74c0` enlistment half (flat board, `IsStale`, language re-push, #1019 join log line) | hand re-implementation (both board files drifted on 1.4.x) | `Main/Features/Enlistment/...`, `Main/Adapters/EncounterAdapter.cs` |
| B | the 12 series commits `ea7cce7bc` to `b747cf398` (11 ported, `e77ef9654` superseded), net state at `b747cf398` | end-state transplant: `git show b747cf398:<path> > <path>`, then the listed adaptations | new `Main/Features/ShadowfaxScreens/**`, two adapters, tests, shared-file edits, docs |
| C | `943ab74c0` MapPerf half (menu probes, one PatchShield exclusion) | hand port onto WP117's MapPerf | `Main/Features/MapPerf/**`, `Dependencies/Foundation/PatchShieldPolicy.cs` |

The series squash is `git diff 337228885 b747cf398` (73 files; `337228885` is `ea7cce7bc^` and the
merge base with trunk). `f48ef74dc` (instant transitions) is an ancestor of `337228885`, so the squash
never touches its files (`InstantTransitionRewriter.cs`, `UI/ShadowfaxScreensPrefab.cs`, their two
tests, the four prefab XMLs). The 1.5.x merge `963218f7f` then added `[ClientOnly]`, moved
`QuestJournalAdapter` to `Client/`, moved the clan swap helper into the patch class, a
dedicated-server branch, and assigned the numbers Patch127 to Patch130 to the four `PatchXXX_`
placeholders. Of that, this plan takes only the four numbers (D2 keeps 1.5.x numbers; `git grep`
shows `Patch127_` to `Patch130_` belong to these four categories only on 1.5.x, and 1.4.x has no
three-digit category).

### Part A files on 1.4.x (`origin/bannerlord-1.4.5`, read this session)

- `Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs` (205 lines). `:12-15`
  `public interface IServiceStatusTextWriter { void Write(ServiceStatusModel status); }`. `:19-23`
  the `<summary>` above `private const string WaitTextVariable = "TAOM_ENLISTMENT_WAIT_TEXT";` (`:24`).
  `:29-68` `Write`, which builds one nested `TextObject` (`:52-53`), sets 11 variables (`:55-65`, the
  last being `text.SetTextVariable("DUTY", DutyLine(status.ActiveDutyId));`) and ends with
  `MBTextManager.SetTextVariable(WaitTextVariable, text);` (`:67`). It has no `WriteNotServing`, no
  retinue or kit lines (those are 1.5.x-only).
- `Main/Features/Enlistment/ServiceStatusService.cs` (149 lines). `:31-35` the `RefreshIfChanged`
  summary; `:135-146`:

```csharp
    public bool RefreshIfChanged()
    {
        var current = Build();
        if (current == null)
            return false;
        if (current.Equals(_last))      // :140
            return false;

        _last = current;
        _writer.Write(current);
        return true;
    }
```

- `IServiceStatusTextWriter` has one implementation and one NSubstitute fake
  (`TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs:38`); no test pins the writer's text.
- `Main/Features/Enlistment/Domain/ServiceStatusModel.cs:72-86`: the constructor's named parameters
  are `commanderName, activity, settlementName, rank, assignment, daysServed, trust, deferredWages,
  activeDutyId, serviceXp, dailyWage, nextRank, nextRequirementKey, nextRequirementTarget`.
  `ServiceRank` (`Content/Domain/ServiceEnums.cs:4-10`) is `Recruit, Soldier, Veteran, Sergeant`.
- The board refresh cadence is 2 s (`ServiceMaintenanceService.cs:31`, `:199-203`).
- `Main/Adapters/EncounterAdapter.cs`: `EnsureEncounterAgainst` `:187-237`; the leader null check
  `:217-223`; `PlayerEncounter.RestartPlayerEncounter(defenderLeader, attackerLeader,
  forcePlayerOutFromSettlement: false);` `:225`; `public bool JoinBattle` `:239`. WP112 edits other
  lines of this file earlier in the programme, so numbers may shift; the anchors stay.
- Callers: `EnlistmentBattleBehavior.OnMapEventStarted` (`Hooks/EnlistmentBattleBehavior.cs:88`) to
  `ServiceBattleService.TryJoin` (`ServiceBattleService.cs:39`) to `EnsureEncounterAgainst` (`:109`,
  `:117`). `EnlistmentDiagnosticsGateTests` scans three other files only, so an ungated log line in
  `EncounterAdapter.cs` breaks no gate.
- `docs/features/enlistment.md`: the row starting `| Live status board |` at `:374`;
  `## Interactions with the rest of TAOM` at `:1709`.

### Part B shared files on 1.4.x (read this session; earlier WPs append to most of them)

- `Main/Composition/FeatureModules.cs` (WP105, from `1efdf2985`): `internal static readonly
  TaomFeatureModule[] All = { ... };`, append-only. WP118 adds `LoadTimeStampsModule` to it.
- `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`: at the 1.4.x tip 7
  properties, the class closes at `:52`. Groups use `[SettingPropertyGroup("...")]` plus
  `[SettingPropertyBool(..., RequireRestart = false, HintText = ...)]`.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: sets `Instrumentation` (`:52-69`),
  `ActionButtons`, `PlayerLocal`, `Presentation` (`:101-113`). Every property of the three diagnostics
  pages must be excluded (comment `:19`).
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:211`:
  `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` (covered 0 means
  every page property must be on an exclusion list); `EveryDocQuotingTheSettingsCounts_AgreesWithReflection`
  requires `docs/features/coop-interop.md` to contain `**<total>**` and
  `docs/features/bannerlord-together-compat.md` to contain ` <total> MCM settings`. At the tip both say
  243 (`coop-interop.md:310`, `bannerlord-together-compat.md:291`); earlier WPs will have moved it.
- `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: every class with a
  `static bool Prefix(` needs a `Registry` entry (`:361-377`), no stale entries (`:379-388`); the
  `// --- Parked ---` line is `:270`.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: DataRows end `:105`, the method is `:106`.
- `docs/reference/harmony-patch-registry.md`: one `## PatchNN_Name` section per category; at the tip
  the last numbered one is `## Patch88_LordPartyTemplate` (`:990`), then
  `## Patch_MissionTime_SetMovementOrder` (`:1004`).
- `docs/reference/taleworlds-api-snapshot/reflection-sites.md`: Category B table header `:29`, the
  first `Status (` line after it `:79`.
- `docs/reference/taleworlds-api-snapshot/patch-targets.md`: generated by
  `tools/snapshot_api_surface.ps1` from `TAOM.Tests\bin\Debug\net472\TAOM.dll` of the worktree;
  `Patches: 229.` at `:7`; the `TAOM.Features.ShaderPrecompilation.` row is `:216`; it ends with a
  backlinks footer (the 1.5.x RCA records that the generator drops it; restore by hand).
- Conditional: `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` (plan 037, WP123, same
  wave) and `TAOM.Tests/Composition/PatchCategoryNumberTests.cs` (WP122). Neither exists at the tip.

### Part B new code (all from `b747cf398`, read in full this session)

- 36 production files: `Main/Adapters/IQuestJournalAdapter.cs`, `Main/Adapters/QuestJournalAdapter.cs`
  and 34 files under `Main/Features/ShadowfaxScreens/` (all of `b747cf398`'s 36 except
  `InstantTransitionRewriter.cs` and `UI/ShadowfaxScreensPrefab.cs`).
- 19 test files under `TAOM.Tests/Features/ShadowfaxScreens/` (all 21 except
  `InstantTransitionRewriterTests.cs` and `ShadowfaxScreensPrefabTests.cs`): 215 `[TestMethod]`s, three
  of them with 2, 3 and 5 `[DataRow]`s (222 results if the runner reports each row).
- `ShadowfaxScreensModule : TaomFeatureModule` overrides `Id`, `PatchCategories` (four
  `PatchCategoryDecl(..., ApplyPhase.GameInit)`), `RegisterServices(IRegistrator)`,
  `InitializeStatics(IResolver)` and `OnPhase(ApplyPhase, IResolver)` (clan install line, quest adapter
  bind). It does not set `OwnsSaveData`, declares no game model, campaign or mission behavior, and the
  composition API is byte-identical between `1efdf2985` (what WP105 copies) and `b747cf398`
  (`git diff --stat 1efdf2985 b747cf398 -- Main/Composition` shows only `FeatureModules.cs` and
  `MainMenuStep.cs`).
- Dependencies the code binds, each from an earlier WP: `IStampClock` (`Main/Core/Diagnostics`,
  `Now`, `Frequency`) and `LoadTimeStampLines.ToMs(long ticks, long frequency)` (internal static) from
  WP118, registered by `LoadTimeStampsModule` on 1.5.x; `TickProfilerTranspiler.Rewrite(IEnumerable<CodeInstruction>,
  IReadOnlyList<CallSwap>, string methodLabel, IModLogger? logger, out int swapped)` and `CallSwap(MethodInfo,
  MethodInfo)` from WP109; `ICoopPresenceProvider.IsCoopActive` (1.4.x `CoopInteropIoC.cs:10`); tests
  use `FakeStampClock` and `RecordingLogger` (namespace `TAOM.Tests.Features.LoadTimeStamps`, both
  unchanged on 1.5.x since `346f3e093`) and `GameAssemblies.EnsureLoaded()` / `.Diagnostics` (exist on
  1.4.x, `TAOM.Tests/Migration/GameAssemblies.cs:30`, `:36`).
- Existing 1.4.x patches on the same targets, all compatible: Patch75 `RefugeClanScreenPatch`
  (postfix on `ClanPartiesVM.RefreshPartiesList`; runs only on real builds after the call-site swap),
  Patch23 `PartyCharacterVM_GetCharacterCode_Patch` (postfix that writes `__result.Color1` and
  `Color2`, `Main/Features/BannerColorPersistence/Hooks/PartyCharacterVM_GetCharacterCode_Patch.cs:31-44`),
  Patch34 `Patch34_SPInventoryVMCapture` (another `SPInventoryVM` constructor patch). No 1.4.x patch
  targets `ClanPartiesVM.RefreshValues`, `QuestsVM` or `QuestItemVM`.

### Engine facts verified on the installed v1.4.8 (taom-src, this session)

| Member | Where (`C:\Users\mikew\.taom-src\v1.4.8\...`) |
|---|---|
| `public ClanManagementVM(Action onClose, Action<Hero> showHeroOnMap, Action<Hero> openPartyAsManage, Action openBannerEditor)`, the only constructor; it creates `ClanPartiesVM` (`:1127`) then calls `UpdateKingdomRelatedProperties()` (`:1157`), which calls `UpdateBannerVisuals()` (`:1316`), which calls `RefreshValues()` (`:1260`), which calls `_clanParties?.RefreshValues()` (`:1203`); `RefreshCategoryValues` calls `ClanParties.RefreshPartiesList()` directly (`:1363`); `OnChangeClanNameDone` pairs `RefreshCategoryValues()` and `RefreshValues()` (`:1380-1381`) | `...ClanManagement.ClanManagementVM.cs` |
| `public ClanPartiesVM(Action, Action<Hero>, Action, Action<ClanCardSelectionInfo>)` calls `RefreshPartiesList()` (`:387`) then `RefreshValues()` (`:388`); `public override void RefreshValues()` resets `GarrisonsText` and `CaravansText` (`:399-400`) then calls `RefreshPartiesList()` once (`:401`); `public void RefreshPartiesList()` (`:425`, one overload) ends with `GameTexts.SetVariable("CURRENT", Caravans.Count)` / `CaravansText` / `Garrisons.Count` / `GarrisonsText` (`:465-468`); `Caravans`, `Garrisons` (`:304`, `:321`), `CaravansText`, `GarrisonsText` (`:134`, `:151`) public | `...ClanManagement.Categories.ClanPartiesVM.cs` |
| `GauntletClanScreen.OnActivate` calls `RefreshCategoryValues()` then `UpdateBannerVisuals()` (`:226-239`) | `SandBox.GauntletUI.GauntletClanScreen.cs` |
| `GameTexts.FindText(string id, string variation = null)` `:69`; `SetVariable(string, int)` `:94` | `TaleWorlds.Core.GameTexts.cs` |
| `SPInventoryVM` has ONE constructor, `(InventoryLogic inventoryLogic, bool isInCivilianModeByDefault, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags)` `:3060`, setting the private `InventoryScreenHelper.InventoryMode _usageType` (`:97`) first (`:3063`); `private void InitializeInventory()` `:4480` (one overload) sorts each side with `orderby ... select i).ToArray()` (`:4507-4509`, `:4510-4512`); its callers are the constructor (`:3124`) and `AfterReset` (`:3796`), both re-sorting after it (`:3134`, `:3142` (loot mode sorts the other side by cost, `:3136-3139`), `:3799-3800`) | `...Inventory.SPInventoryVM.cs` |
| `SPInventorySortControllerVM.ItemComparer.ResolveEquality` compares `ItemDescription` (`:34-37`); `SortByDefaultState()` `:293` | `...Inventory.SPInventorySortControllerVM.cs` |
| `InventoryLogic.GetElementCountOnSide(InventorySide side)` `:1133`; `InventorySide.OtherInventory = 0`, `PlayerInventory = 1` (`:30-36`); `InventoryScreenHelper.InventoryMode { Default, Trade, Loot, Stash, Warehouse }` | `...Inventory.InventoryLogic.cs`, `Helpers.InventoryScreenHelper.cs:17-24` |
| `private CharacterCode PartyCharacterVM.GetCharacterCode(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side)` `:1476` (one overload); callers `:175`, `:183` pass the row's own `Type` and `Side`; the body reads the screen's owner parties and builds `CharacterCode.CreateFrom(...)` from equipment code and body properties; non-hero body properties use a deterministic seed (`CharacterObject.cs:724-735`); no TAOM patch targets `GetBodyProperties` | `...Party.PartyCharacterVM.cs` |
| `CharacterCode.Code { get; private set; }` `:18`, `Color1`/`Color2` settable `:30`, `:32`; `CharacterImageIdentifier(CharacterCode)` copies only `characterCode?.Code` into the id (`:5-10`); `CharacterImageIdentifierVM(CharacterCode)` wraps it (`:7-10`); `UpgradeTargetVM(int, CharacterObject, CharacterCode, Action<int,int>, Action<UpgradeTargetVM>)` passes the code only to `CharacterImageIdentifierVM` (`:234-246`) | `TaleWorlds.Core.CharacterCode.cs`, `TaleWorlds.Core.ImageIdentifiers.CharacterImageIdentifier.cs`, `TaleWorlds.Core.ViewModelCollection.ImageIdentifiers.CharacterImageIdentifierVM.cs`, `...Party.UpgradeTargetVM.cs` |
| `public PartyVM(PartyScreenLogic partyScreenLogic)` `:1898` (one constructor); `private void InitializeTroopLists()` `:2057`, called by the constructor `:1945`, the reset `:2495`, `TransferAllTroops` `:2834`; `public override void RefreshValues()` `:1959`; `private void TransferAllTroops(PartyScreenLogic.PartyCommand command)` `:2832`; `public override void OnFinalize()` `:3330`; each one overload | `...Party.PartyVM.cs` |
| `QuestItemVM(JournalLogEntry questLogEntry, Action<QuestItemVM> onSelection, QuestsVM.QuestCompletionType completion)` `:346` calls `PopulateQuestLog(entry, false)` per entry (`:358-361`), then `UpdateIsUpdated()` and `RefreshValues()`; `private void PopulateQuestLog(JournalLog log, bool isLastStage)` `:464` (callers: the three constructors `:360`, `:383`, `:408`); `public void UpdateIsUpdated()` `:484`; `QuestLogEntry` `:55`; `IsUpdated` `:160` | `...Quests.QuestItemVM.cs` |
| `public QuestsVM(Action closeQuestsScreen)` `:447`; `private void SetSelectedItem(QuestItemVM quest)` `:535` (one overload); nested `QuestCompletionType` `:18` | `...Quests.QuestsVM.cs` |
| `private abstract class QuestItemSortControllerVM.QuestItemComparerBase` `:15` with `protected enum JournalLogIndex { First, Last }` `:17-21` and `protected JournalLog GetJournalLogAt(QuestItemVM questItem, JournalLogIndex logIndex)` `:25-39` | `...Quests.QuestItemSortControllerVM.cs` |
| `IViewDataTracker.UnExaminedQuestLogs` (`IReadOnlyList<JournalLog>`) `:17`; `JournalLogEntry.GetEntries()` `:124` | `TaleWorlds.CampaignSystem.IViewDataTracker.cs`, `...LogEntries.JournalLogEntry.cs` |
| Finished-quest rows bind `IsVisible="@IsUpdated"` at `QuestsScreen.xml:277` | game install `Modules/SandBox/GUI/Prefabs/QuestsScreen/QuestsScreen.xml` (read only) |
| `MBTextManager`: `private static readonly TextProcessingContext TextContext` `:30`; `_languageProcessor = new EnglishTextProcessor()` `:32`; `_activeTextLanguageId = "English"` `:36`; `public static string ActiveTextLanguage` `:52`; `SetTextVariable(string variableName, string text, bool sendClients = false)` wraps the string in `new TextObject(text)` (`:170-176`); `SetTextVariable(string, TextObject, bool = false)` `:178-184` | `TaleWorlds.Localization.MBTextManager.cs` |
| `TextProcessingContext._variables` is `private readonly Dictionary<string, TextObject>` (`:12`); `TextObject.Value` is a public field (`:14`); `TextObject.ToString()` catches and returns `"Error at id: ..."` on failure (`:197-210`) | `TaleWorlds.Localization.TextProcessor.TextProcessingContext.cs`, `TaleWorlds.Localization.TextObject.cs` |
| The `NEWLINE` global is set only by `CampaignUIHelper`, lazily, to `"\n"` (`CampaignUIHelper.cs:1242`, `:2904`) | `C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\...` |
| `GameMenuVM.OnFrameTick()` `:481` (one overload) re-renders `ContextText = _menuText.ToString()` (`:487`) whenever `_requireContextTextUpdate = IsMenuTextChanged()` (`:512`); `IsMenuTextChanged` returns true when `_menuTextAttributes.Count != _menuText?.Attributes?.Count` (`:522`), which is always true for a text with no attributes; `public string ContextText` `:190` | `...GameMenu.GameMenuVM.cs` |
| `StartBattleAction.ApplyInternal` interrupts a player encounter when the main party is in a settlement and the event is not a sally-out (`:32-35`); `PlayerEncounter.InterruptEncounter(string)` leaves the battle and activates that menu (`:2016-2024`); `RestartPlayerEncounter(PartyBase, PartyBase, bool forcePlayerOutFromSettlement = true, bool isPlayerEncounterRestartedForRaid = false)` `:639` | `...Actions.StartBattleAction.cs`, `...Encounters.PlayerEncounter.cs` |
| `MapEvent.MapEventSettlement` `:155`, `EventType` `:196`; `MobileParty.MainParty => Campaign.Current.MainParty` `:256`, `CurrentSettlement` `:578`, `MapFaction` `:1080`; `Settlement.MapFaction` `:293` | `...MapEvents.MapEvent.cs`, `...Party.MobileParty.cs`, `...Settlements.Settlement.cs` |
| `MapScreen.OnFrameTick(float)` `:1332` calls `_menuViewContext.OnFrameTick(dt)` `:1340`; `GauntletMenuBaseView.OnFrameTick` calls `GameMenuDataSource.OnFrameTick()` with no catch; `MenuViewContext.OnFrameTick` has no catch | `SandBox.View.Map.MapScreen.cs`; dump `_modules_build\SandBox__SandBox.GauntletUI.cs:6796-6800`, `SandBox__SandBox.View.cs:9855-9866` |

1.4.x gate facts: `Main/Features/MenuLinkColors/Hooks/Patch64_MenuLinkColors.cs:24-26` prefixes the
`GameMenuVM.ContextText` setter; `Patch36_MapScreenF6` patches `MapScreen.OnFrameTick`; no 1.4.x patch
targets `GameMenuVM.OnFrameTick`. Harmony is 2.4.2 on both lines; MSTest 3.1.1 and NSubstitute 5.1.0
on 1.4.x; `net472`, `LangVersion 10.0`, `<Nullable>enable</Nullable>` on both lines (identical
`Directory.Build.props` settings).

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. The four Shadowfax services hold
  only opaque objects; hooks and `*Calls` facades touch engine types; `QuestJournalAdapter` is the
  quest log's engine boundary in `Main/Adapters` (the pre-merge layout).
- **ADR-002 thin entry points**: every Shadowfax hook file stays under 150 lines
  (`ShadowfaxScreensModuleTests.EveryShadowfaxHookFile_StaysUnder150Lines` enforces it).
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside the IoC
  registration exception.
- **TDD**: the failing test comes first (Steps 1, 6 and 12 are RED steps).
- **MCM json2 rule**: rename a persisted setting, never flip its default. All five settings here are
  new names with no persisted value, so their defaults are safe.
- **`.claude/rules/harmony-patches.md`**: no `MovementOrder` postfix here. Void finalizers keep
  Harmony's rethrow; each prefix and finalizer pair links only through `__state`.
- **Composition root** (`docs/features/composition-root.md`, WP105, "How to port a 1.5.x feature
  module onto 1.4.x"): copy the module, append one `FeatureModules.All` line, take dependencies from
  the `IRegistrator`/`IResolver` arguments, never read `IoC.Resolver`, never call `.PatchCategory(`.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/125 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (an analyzer warning on Patch88 may still print; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; give the call a 600000 ms timeout |
| Filtered | see the block below the table | as each step states |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step 0.6's values; `ai_dashes: 0` |
| Snapshot | `pwsh -NoProfile -File tools/snapshot_api_surface.ps1` (after a build) | regenerates `docs/reference/taleworlds-api-snapshot/patch-targets.md` and `gamemodel-bases.md` |

Filtered: when a step says "Filtered with `A|B|C`", run (one `FullyQualifiedName~` per name, joined by
a plain `|`, no backslash):

```bash
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~A|FullyQualifiedName~B|FullyQualifiedName~C"
```

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game
install. Never `./build.ps1`. Never two builds at once in this worktree. Always build before a
`--no-build` test run. No ModuleData, hook or tool script changes in this WP, so the Data, Hooks and
Tool-test commands of the programme are not needed.

## Scope

**In scope** (the only files you create or modify):

- Part A (7): `Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs`,
  `Main/Features/Enlistment/ServiceStatusService.cs`, `Main/Adapters/EncounterAdapter.cs`,
  `TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs`,
  `TAOM.Tests/Features/Enlistment/ServiceStatusTextWriterRenderTests.cs` (new),
  `TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs` (new),
  `docs/features/enlistment.md`.
- Part B: the 36 production and 19 test files listed in Step 7 and Step 6 (new);
  `Main/Composition/FeatureModules.cs`, `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`,
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`,
  `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`, `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`,
  only if it exists `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`;
  `docs/features/shadowfax-screens.md` (new), `docs/features/coop-interop.md`,
  `docs/features/bannerlord-together-compat.md`, `docs/reference/harmony-patch-registry.md`,
  `docs/reference/taleworlds-api-snapshot/reflection-sites.md`,
  `docs/reference/taleworlds-api-snapshot/patch-targets.md`.
- Part C: `Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs`, `MapFrameProfilerInstaller.cs`,
  `MapProfilerTargets.cs`, `Patch101_MapFrameProfilerMenu.cs` (new), `Main/Features/MapPerf/MapProfileLines.cs`,
  `Dependencies/Foundation/PatchShieldPolicy.cs`, `TAOM.Tests/Features/MapPerf/MapFrameProfilerHooksTests.cs`,
  `MapFrameProfilerInstallerTests.cs`, `MapProfileLinesTests.cs`, `MapFrameProfilerBindingTests.cs`,
  `MapProfilerTargetsTests.cs` (new or edited), only if they exist
  `TAOM.Tests/Composition/PatchCategoryNumberTests.cs` and
  `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`; `docs/features/map-perf-profiler.md`,
  `docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs` and `Main/TAOM.csproj` are NOT edited by
this WP (the module and the installer apply everything; SDK globbing compiles new files). If any
step seems to need them, STOP.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/**` (lessons, RCAs, REVIEW-LOG), `docs/INDEX.md`, `docs/reference/feature-map.md`,
`docs/ai-includes/agent-teams.md`, `tools/README.md` (WPs 142 to 144 sync these); `docs/adrs/**`;
`plans/README.md`; every `f48ef74dc` file (`InstantTransitionRewriter.cs`, `UI/ShadowfaxScreensPrefab.cs`,
their tests, the four prefab XMLs, `PrefabExtensionBindingTests.cs`); `Client/**`,
`ClientOnlyAttribute`, `NullQuestJournalAdapter`; any ModuleData or localization file; renaming or
reformatting anything nearby.

## Git workflow

- Step 0.1 creates the worktree from the main checkout; work and commit only there, on
  `port148/125-campaign-ui-perf`. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`. Run git
  only through the Bash tool (D19).
- Per-port procedure: no `git cherry-pick` in this WP (every simulation conflicts and the series is
  ported from its end state). New files come from `git show <sha>:<path> > <path>` (LF endings; a
  "LF will be replaced by CRLF" warning on `git add` is expected). Hand edits use the Edit tool or the
  scripts given. Every 1.5.x bookkeeping hunk stays behind.
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>` in
  the NEXT sequential Bash call: never the same command, never a parallel sibling call, never through
  PowerShell. Write each message with the Write tool to the file the step names and pass it as
  `C:/Users/mikew/source/repos/taom-port/125-msg-<X>.txt`.
- Never `--no-verify`, `--amend`, `-C` or `-c`. If a hook denies a commit, STOP and report it verbatim.
- Commit template: subject `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters, keep the
  1.5.x type and scope); body for 1.4.x readers wrapped at 72, no long dashes; trailers `Research:`,
  `Not-tested:`, `Save-compat:` as relevant, then one `Ported-from: <40-hex sha>` per included 1.5.x
  commit; no `Co-Authored-By` and no "Generated with" line. The exact messages are in Steps 5, 11, 14.

## Steps

### Step 0: Worktree, prerequisites, baseline

**0.1 Worktree.**

```bash
git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/125 -b port148/125-campaign-ui-perf bannerlord-1.4.5 && cd /c/Users/mikew/source/repos/taom-port/125 && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: a commit line; `git status --porcelain` prints nothing.

**0.2 Prerequisites (WP104, WP105, WP109, WP118).**

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && for f in Main/Composition/TaomFeatureModule.cs Main/Composition/FeatureDecls.cs Main/Composition/FeatureModules.cs Main/Features/MissionPerf/TickProfilerTranspiler.cs Main/Core/Diagnostics/IStampClock.cs Main/Features/LoadTimeStamps/LoadTimeStampLines.cs TAOM.Tests/Features/LoadTimeStamps/FakeStampClock.cs TAOM.Tests/Features/LoadTimeStamps/RecordingLogger.cs TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Migration/GameAssemblies.cs docs/features/composition-root.md; do test -f "$f" && echo "OK $f" || echo "MISSING $f"; done; \
grep -c 'internal static readonly TaomFeatureModule\[\] All' Main/Composition/FeatureModules.cs; \
grep -c 'public virtual void OnPhase(ApplyPhase phase, IResolver resolver)' Main/Composition/TaomFeatureModule.cs; \
grep -c 'internal sealed class CallSwap' Main/Features/MissionPerf/TickProfilerTranspiler.cs; \
grep -c 'internal static List<CodeInstruction> Rewrite(' Main/Features/MissionPerf/TickProfilerTranspiler.cs; \
grep -c 'internal static double ToMs(long ticks, long frequency)' Main/Features/LoadTimeStamps/LoadTimeStampLines.cs; \
grep -rln 'Register<IStampClock' Main | head -3; \
grep -c 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
test -e Main/Features/ShadowfaxScreens && echo "SHADOWFAX ALREADY EXISTS" || echo "no ShadowfaxScreens yet"; \
git grep -n '"Patch12[7-9]_\|"Patch130_' -- Main | head -3; echo "end"
```

**Verify**: eleven `OK` lines; then `1`, `1`, `1`, `1`, `1`; at least one file path for
`Register<IStampClock` (for example `Main/Features/LoadTimeStamps/LoadTimeStampsModule.cs`); `1`;
`no ShadowfaxScreens yet`; no `Patch127_` to `Patch130_` match; `end`. Anything else: STOP.

**0.3 Drift check.** Run the command at the top of this plan. Then confirm the Part A excerpts still
hold:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && grep -n 'MBTextManager.SetTextVariable(WaitTextVariable, text);' Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs; grep -n 'if (current.Equals(_last))' Main/Features/Enlistment/ServiceStatusService.cs; grep -c 'bool IsStale' Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs; grep -n 'PlayerEncounter.RestartPlayerEncounter(defenderLeader, attackerLeader, forcePlayerOutFromSettlement: false);' Main/Adapters/EncounterAdapter.cs; grep -c 'public bool EnsureEncounterAgainst\|public bool JoinBattle' Main/Adapters/EncounterAdapter.cs
```

**Verify**: one line each for the first two (about `:67` and `:140`); `0`; one line for the third
grep; `2`. Otherwise STOP (a later WP touched the board files early).

**0.4 Part C prerequisites (WP107, WP117).** Part C runs only if all of these hold; record the result.

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && f=Main/Features/MapPerf; test -f $f/Hooks/MapFrameProfilerInstaller.cs && echo installer-ok; grep -c 'internal const string ViewsCategory = "Patch101_MapFrameProfiler_Views";' $f/Hooks/MapFrameProfilerInstaller.cs; grep -c 'var views = tryPatchCategory(ViewsCategory);' $f/Hooks/MapFrameProfilerInstaller.cs; grep -c '// A later game init in the same process' $f/Hooks/MapFrameProfilerInstaller.cs; grep -c 'internal static ProbeStamp BeginProbe()' $f/Hooks/MapFrameProfilerHooks.cs; grep -c 'internal static void RecordView(object instance, ProbeStamp start)' $f/Hooks/MapFrameProfilerHooks.cs; grep -c 'internal static IReadOnlyList<MethodBase?> CoreTargets()' $f/Hooks/MapProfilerTargets.cs; grep -c 'public const string StatusTag' $f/MapProfileLines.cs; grep -c 'public static string BuildViewsShortLine' $f/MapProfileLines.cs; grep -c '"TaleWorlds.CampaignSystem.CampaignEvents.Tick",' Dependencies/Foundation/PatchShieldPolicy.cs; grep -c 'public static bool IsExcludedTargetMethod' Dependencies/Foundation/PatchShieldPolicy.cs; grep -c 'ProfilerOnlyTargets_AreOnPatchShieldsExclusionList\|SharedMapTargets_StayUnderPatchShield' TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs
```

**Verify**: `installer-ok`, then `1` ten times, then `2` (these anchors are the `f6bf0e588` shapes, unchanged at
`685c6a1e0`, the commit WP117 transplants MapPerf from). If any differs, Part C is skipped: do Parts A and B, and report "Part C skipped:
<which check>" in the hand-off.

**0.5 Baseline T0.** Restore, Build, then the full Test command. Record T0 (Total, Passed, Skipped).

**Verify**: build 0 errors; `Failed: 0`. A failing baseline is a STOP (it is not this WP's to fix).

**0.6 Docs baseline.** Run the Docs command; record `dead_links` and `orphan_features`.

---

## Part A: the enlistment board is drawn once per change (`943ab74c0`, enlistment half)

Source: `943ab74c0` "perf(enlistment): v2.0.35 - the service board is drawn once per change" (#1019).
Simulation: exit 1, 21 paths; `ServiceStatusTextWriter.cs` and `ServiceStatusService.cs` conflict because
the 1.4.x files never got the 1.5.x `WriteNotServing`, retinue, kit and oath changes (about 80 and 74
lines of drift), while `EncounterAdapter.cs` merges cleanly. Resolution: keep both 1.4.x files as they
are and add only the flat render, `IsStale` and the language check (Step 2); add the join log line at
the same anchor by hand (Step 3); the MapPerf half waits for Part C.

### Step 1 (RED): the board tests

1. In `TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs` add `using TAOM.Tests.Infrastructure;`
   after the last `using` line, and insert these two methods directly above the `[TestMethod]` line of
   `public void RefreshIfChanged_FirstCall_Writes()` (verbatim from `943ab74c0`):

```csharp
    [TestMethod]
    public void RefreshIfChanged_SameModel_ButTheBoardIsInAnOldLanguage_WritesAgain()
    {
        // A language switch changes no field of the model, so the value comparison alone would leave the
        // board in the old language until the next real change (#1019, the board is rendered once per push).
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();
        _writer.IsStale.Returns(true);

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void TheBoardWriter_RendersTheBoardOncePerPush_AndRecordsTheLanguageItRenderedIn()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs", stripComments: true);
        StringAssert.Contains(src, "SetTextVariable(WaitTextVariable, text.ToString())");
        StringAssert.Contains(src, "ActiveTextLanguage");
        Assert.IsFalse(src.Contains("SetTextVariable(WaitTextVariable, text)"), "The nested TextObject must not be handed to the global variable.");
    }

```

2. Create `TAOM.Tests/Features/Enlistment/ServiceStatusTextWriterRenderTests.cs` with exactly this
   content (the `943ab74c0` test adapted to the 1.4.x model and board: no `WriteNotServing`, no kit,
   column or retinue lines; one added test pins `IsStale`):

```csharp
using System.Collections;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #1019: the board is stored as a flat string, which the menu re-parses as text every frame. A stored string that
/// parsed differently from what was rendered (a leftover brace, a tag) would show on screen, so rendering the variable
/// must give back exactly the stored string, with every optional line present.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServiceStatusTextWriterRenderTests
{
    private const string Variable = "TAOM_ENLISTMENT_WAIT_TEXT";

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static IDictionary Variables()
    {
        var context = typeof(MBTextManager).GetField("TextContext", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
        return (IDictionary)context.GetType().GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context);
    }

    private static string Stored() => ((TextObject)Variables()[Variable]).Value;

    private static ServiceStatusModel EveryLine() => new ServiceStatusModel(
        commanderName: "Aragorn", activity: CommanderActivity.InBattle, settlementName: "Minas Tirith",
        rank: ServiceRank.Veteran, daysServed: 24, trust: 9, deferredWages: 40, activeDutyId: "recon_sweep",
        serviceXp: 100, dailyWage: 12, nextRank: ServiceRank.Sergeant, nextRequirementKey: "xp",
        nextRequirementTarget: 200);

    [TestMethod]
    public void Write_WhenNewlineWasNeverSet_StillStoresTheBoardWithLineBreaks()
    {
        // The engine sets the NEWLINE global lazily inside its UI builders, and a new campaign clears all globals.
        Variables().Remove("NEWLINE");
        var status = new ServiceStatusModel(
            commanderName: "Aragorn", activity: CommanderActivity.Marching, rank: ServiceRank.Veteran,
            daysServed: 24, trust: 9, serviceXp: 100, dailyWage: 12);

        new ServiceStatusTextWriter().Write(status);

        StringAssert.Contains(Stored(), "\n");
    }

    [TestMethod]
    public void Write_EveryOptionalLine_RendersTheStoredStringUnchanged()
    {
        new ServiceStatusTextWriter().Write(EveryLine());
        var rendered = new TextObject("{" + Variable + "}").ToString();

        Assert.AreEqual(Stored(), rendered);
        StringAssert.Contains(rendered, "Toward ");
        StringAssert.Contains(rendered, "Pay owed to you: 40");
        StringAssert.Contains(rendered, "You have orders: ");
        Assert.IsFalse(rendered.Contains("{") || rendered.Contains("}"), rendered);
    }

    [TestMethod]
    public void IsStale_IsTrueBeforeTheFirstWrite_AndFalseAfterIt()
    {
        var writer = new ServiceStatusTextWriter();
        Assert.IsTrue(writer.IsStale, "a writer that never rendered has no language to match");

        writer.Write(EveryLine());

        Assert.IsFalse(writer.IsStale, "the board was just rendered in the active language");
    }
}
```

3. Create `TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs` from `943ab74c0`:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git show 943ab74c0:TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs > TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs && grep -c 'RepoPaths.ReadSource("Main/Adapters/EncounterAdapter.cs", stripComments: true)' TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs
```

**Verify**: the grep prints `1`. Then Build: it FAILS with `CS1061` naming `IsStale` (on
`IServiceStatusTextWriter` and `ServiceStatusTextWriter`) and nothing else. Any other error: STOP.

### Step 2 (GREEN): the flat board

In `Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs`:

1. In the interface, after `void Write(ServiceStatusModel status);` add:

```csharp

    /// <summary>True when the board was last rendered in a language other than the active one.</summary>
    bool IsStale { get; }
```

2. Replace the whole `<summary>` block directly above `private const string WaitTextVariable` (lines
   about 19-23; keep the constant itself) with:

```csharp
    /// <summary>
    /// The global the wait menu's registered text (<c>"{TAOM_ENLISTMENT_WAIT_TEXT}"</c>) resolves against, holding the
    /// board as a flat string so each per-frame resolve is one flat pass instead of a nested render (#1019).
    /// </summary>
```

3. Replace the line `        MBTextManager.SetTextVariable(WaitTextVariable, text);` with:

```csharp
        // The flat board resolves NEWLINE once, here; only engine UI builders set it, lazily, to this same value
        // (v1.4.8 CampaignUIHelper.cs:1242, :2904).
        MBTextManager.SetTextVariable("NEWLINE", "\n");
        MBTextManager.SetTextVariable(WaitTextVariable, text.ToString());
        _renderedLanguage = MBTextManager.ActiveTextLanguage;
```

4. Directly after the closing brace of `Write`, add:

```csharp

    private string? _renderedLanguage;

    public bool IsStale => _renderedLanguage != MBTextManager.ActiveTextLanguage;
```

(`string?` instead of 1.5.x's `string` only avoids a nullable warning; behaviour is identical.)

In `Main/Features/Enlistment/ServiceStatusService.cs`:

5. Replace `        if (current.Equals(_last))` with `        if (current.Equals(_last) && !_writer.IsStale)`.
6. Replace the `RefreshIfChanged` summary text (the two `///` lines between `/// <summary>` and
   `/// </summary>` directly above `bool RefreshIfChanged();`, at about `:32-33`) with these three lines:

```csharp
    /// Rebuild and push the menu text if anything changed, or if the board was last rendered in another
    /// language. Cheap on the unchanged path: one snapshot read, a value comparison and a language check,
    /// no allocation of a TextObject.
```

7. In the class summary, directly above its closing `/// </summary>` (the one before
   `public interface IServiceStatusService`), add:

```csharp
///
/// Since #1019 the board itself is rendered to a plain string once per push, so the per-frame re-render
/// resolves one variable instead of the whole nested board. The price is that the pushed text no longer
/// follows a language switch by itself: <see cref="RefreshIfChanged"/> also writes again when the writer
/// reports the board is in a language other than the active one.
```

Build, then Filtered with `ServiceStatusServiceTests|ServiceStatusTextWriterRenderTests`.

**Verify**: 0 errors; `Failed: 0`. If `Write_EveryOptionalLine_RendersTheStoredStringUnchanged`
fails, or `Stored()` starts with `Error at id:`, STOP: the flat string does not re-render identically
in this host, and shipping it could change what players see. Then run Filtered with
`InsideSettlementJoinDiagnosticTests`: expect `Failed: 2` (the RED half of Step 3; the line is not
there yet).

### Step 3 (GREEN): the #1019 join log line

In `Main/Adapters/EncounterAdapter.cs`, inside `EnsureEncounterAgainst`, insert directly above the line
`            PlayerEncounter.RestartPlayerEncounter(defenderLeader, attackerLeader, forcePlayerOutFromSettlement: false);`:

```csharp
            // #1019: a join seeded from inside the settlement the party stands in (a raid reaching the soldier's
            // village). StartBattleAction.ApplyInternal (v1.4.8 :32-35) then interrupts the encounter this seeds, unless
            // it is a sally-out: InterruptEncounter (PlayerEncounter.cs:2016-2024) leaves the battle and opens vanilla's
            // 'encounter_interrupted' menu. Rare, so it is not behind the trace toggle.
            var main = MobileParty.MainParty;
            var place = mapEvent.MapEventSettlement;
            if (place != null && main?.CurrentSettlement == place)
                _logger?.LogInfo($"[EnlistDiag] seeding the join from inside '{place.StringId}' (event={mapEvent.EventType}, sameFactionAsSettlement={main.MapFaction == place.MapFaction}); a battle start interrupts this encounter (not a sally-out) and opens 'encounter_interrupted'");

```

Build, then Filtered with
`InsideSettlementJoinDiagnosticTests|EnlistmentDiagnosticsGateTests|ServiceStatusServiceTests|ServiceStatusTextWriterRenderTests`;
if `grep -rl 'class MainPartyNullCheckScanTests' TAOM.Tests` finds a file (WP112 adds that scan), add
`MainPartyNullCheckScanTests` to the filter.

**Verify**: 0 errors; `Failed: 0`.

### Step 4: Part A docs

In `docs/features/enlistment.md`:

1. Replace the whole line that starts with `| Live status board |` with:

```markdown
| Live status board | `ServiceStatusService` + `Presentation/ServiceStatusTextWriter.cs` | Rebuilt on the pump's 2 s budget, pushed only when the model differs, or when the board was rendered in another language than the active one (#1019). **The value equality IS the throttle**, so a field omitted from `Equals` is a status line that never updates; one `DataRow` per field guards it. The board is rendered to a plain string once per push (`SetTextVariable(name, text.ToString())`), not handed to the menu as a nested `TextObject`, because `GameMenuVM.IsMenuTextChanged` reports a text with no attributes as changed on every frame (v1.4.8 `GameMenuVM.cs:522`). |
```

2. Insert this section directly above the line `## Interactions with the rest of TAOM`:

```markdown
## Inside the raided village (#1019)

A raid can reach the village the soldier stands in while his commander is inside it. TAOM's
battle-start join (`EnlistmentBattleBehavior.OnMapEventStarted` to `ServiceBattleService.TryJoin`)
then seeds an encounter through `IEncounterAdapter.EnsureEncounterAgainst` while the main party's
`CurrentSettlement` is the raided village. On v1.4.8 `StartBattleAction.ApplyInternal` interrupts a
player encounter when the main party is inside a settlement and the event is not a sally-out
(`StartBattleAction.cs:32-35`); `PlayerEncounter.InterruptEncounter` then leaves the battle and opens
vanilla's `encounter_interrupted` menu (`PlayerEncounter.cs:2016-2024`). Which menu the player then
sees on this line, and what its options do, has not been observed in a game.

`EnsureEncounterAgainst` therefore writes one INFO line when it seeds a join from inside the map
event's settlement, not behind the trace toggle because it is rare:
`[EnlistDiag] seeding the join from inside '<settlement id>' (event=<type>, sameFactionAsSettlement=<True|False>); ...`.
`InsideSettlementJoinDiagnosticTests` pins its place (after the settlement read, before
`RestartPlayerEncounter`, ungated). Ported from the 1.5.x line (#1019).

```

Run the Docs command.

**Verify**: `dead_links` and `orphan_features` unchanged from Step 0.6; `ai_dashes: 0`.

### Step 5: Commit A

Full Test command. **Verify**: `Failed: 0`; `Total` = T0 + 7.

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git add Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs Main/Features/Enlistment/ServiceStatusService.cs Main/Adapters/EncounterAdapter.cs TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs TAOM.Tests/Features/Enlistment/ServiceStatusTextWriterRenderTests.cs TAOM.Tests/Features/Enlistment/InsideSettlementJoinDiagnosticTests.cs docs/features/enlistment.md && git status --porcelain
```

Write `C:\Users\mikew\source\repos\taom-port\125-msg-A.txt` with exactly:

```text
perf(enlistment): v2.0.28 - the service board is drawn once per change

The service wait menu redraws its text every frame, because the game
treats a menu text with no attributes as changed on every frame, and
TAOM handed it the status board as a nest of texts that was rebuilt
from scratch each time. The board is now turned into plain text once
each time it changes, so each frame only redraws finished text. A
change of game language reaches the board within one status refresh
(about 2 seconds). The board's line breaks no longer depend on the
game having set its newline marker earlier, so a board written right
after a campaign starts keeps its lines apart.

A log line now records when a soldier's battle join is seeded from
inside the village he stands in while a raid reaches it, so a game
run can confirm which menu the game hands him there.

Research: taom-src v1.4.8 GameMenuVM, MBTextManager, StartBattleAction
Not-tested: the board in game; the raid-from-inside join line
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git commit -F C:/Users/mikew/source/repos/taom-port/125-msg-A.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` prints nothing.

**Part A done when**: the commit exists; build 0 errors; full suite `Failed: 0` at T0 + 7; the
lint values unchanged and `ai_dashes: 0`.

---

## Part B: Shadowfax screen-open speed, one squashed change (D6)

Source: `ea7cce7bc` (timer and switches), `11aeb8aee` (clan), `33c3bdf1a` (inventory), `3f8168228`
(party), `3db115423` (quest log), `e77ef9654` (superseded), `c32662872`, `22695bd01`, `579bb93f0`,
`642f92e39` (review fixes), `402280dc2` (docs), `b747cf398` (convergence fixes); subjects in the
Disposition table. Simulation: every commit exits 1, mostly modify/delete conflicts on files that do
not exist on 1.4.x (the series edits files created earlier in the series), plus content conflicts in
`FeatureModules.cs`, `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`,
`CampaignHotPathSettingsProvidersTests.cs`, `SettingsFingerprintTests.cs`,
`CoopVetoClassificationTests.cs`, `ReflectionSiteBindingTests.cs` and the docs. Resolution: no
cherry-pick; every series file is written from `b747cf398` and adapted (Steps 6, 7, 9), and in every
shared file all 1.4.x lines stay and only the Shadowfax entries are appended (Steps 6 to 9).

### Step 6 (RED): transplant the tests and the shared test edits

1. Copy the 19 series test files (never the two `f48ef74dc` tests):

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && S=b747cf398 && for f in $(git ls-tree -r --name-only $S TAOM.Tests/Features/ShadowfaxScreens | grep -v -e InstantTransitionRewriterTests -e ShadowfaxScreensPrefabTests); do mkdir -p "$(dirname "$f")"; git show "$S:$f" > "$f"; done && git status --porcelain -uall | grep -c '^?? TAOM.Tests/Features/ShadowfaxScreens/'
```

**Verify**: `19`.

2. Category numbers in the two tests that name them:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && grep -rl 'PatchXXX_Shadowfax' TAOM.Tests/Features/ShadowfaxScreens | xargs sed -i -e 's/PatchXXX_ShadowfaxClan/Patch127_ShadowfaxClan/g' -e 's/PatchXXX_ShadowfaxInventory/Patch128_ShadowfaxInventory/g' -e 's/PatchXXX_ShadowfaxParty/Patch129_ShadowfaxParty/g' -e 's/PatchXXX_ShadowfaxQuests/Patch130_ShadowfaxQuests/g' && sed -i 's|(v1\.5\.5 when written)|(v1.5.5 when written; re-verified on v1.4.8)|' TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenBindingTests.cs TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogFastOpenBindingTests.cs && grep -rn 'PatchXXX' TAOM.Tests/Features/ShadowfaxScreens; grep -c 'Patch127_ShadowfaxClan", "Patch128_ShadowfaxInventory", "Patch129_ShadowfaxParty", "Patch130_ShadowfaxQuests"' TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensModuleTests.cs; grep -c 're-verified on v1.4.8' TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenBindingTests.cs TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogFastOpenBindingTests.cs
```

**Verify**: no `PatchXXX` line; `1`; `...ClanFastOpenBindingTests.cs:1` and `...QuestLogFastOpenBindingTests.cs:1`.

3. `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`: add
   `using TAOM.Features.ShadowfaxScreens.Party;` after `using TAOM.Features.CoopInterop;`, and insert
   these two entries directly above the line that starts with `        // --- Parked` (verbatim from
   the series):

```csharp
        ["PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch"] = new(CoopVeto.ReviewedSafe,
            PartyFastOpenCalls.PatchCategory + ": client-side UI. Returns false only on a per-screen cache hit, handing back the " +
            "CharacterCode vanilla built earlier for the same character, troop type and side; the code feeds a portrait " +
            "image id and mutates no campaign state, so there is nothing replicated to skip. Under co-op the roster refresher " +
            "calls the screen's OnReset, which rebuilds rows through the same hook, so codes stay correct for the " +
            "current roster. The toggle is presentation."),
        ["QuestItemVM_PopulateQuestLog_QuestLogPatch"] = new(CoopVeto.ReviewedSafe,
            "Quest Log Fast Open (#854): client-side UI. The prefix skips QuestItemVM.PopulateQuestLog, which only adds " +
            "QuestStageVM view models to a finished quest's list inside the QuestsVM constructor; it reads journal logs and " +
            "writes no campaign state, so there is no replicated mutation to skip. A skipped item is built when it is " +
            "selected, and every other outcome (toggle off, adapter unbound, replay, disarmed) returns true and runs vanilla. " +
            "The toggle is presentation in CoopSettingsRelevance."),

```

4. `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: insert directly above the line
   `    public void ReflectionSite_ResolvesAgainstInstalledEngine(string fullName, string simpleName, string member, string kind, string source)`:

```csharp
    // --- Quest Log Fast Open (#854): the replay of the private PopulateQuestLog for a finished quest built on demand.
    // A miss makes Bind return a reason, the quest log is never deferred and vanilla builds every quest at open.
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestItemVM", "QuestItemVM", "PopulateQuestLog", "Method", "QuestJournalAdapter.cs:30")]
    // The guard check resolves QuestsVM.SetSelectedItem by name too; a miss reads as the SetSelectedItem patch not attached.
    [DataRow("TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestsVM", "QuestsVM", "SetSelectedItem", "Method", "QuestJournalAdapter.cs:39")]
```

5. `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`: in the line
   `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: R, covered: 0);` replace R by R + 5
   (7 becomes 12 if no earlier WP added a page setting).

6. Only if `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` exists (WP123): add
   `using TAOM.Features.ShadowfaxScreens;` with the other `TAOM.Features.*` usings; append
   `    // Shadowfax Screens: read on every screen open and by each screen's timer points, from the Battle Load Diagnostics page.`
   and `    [DataRow(typeof(ShadowfaxScreensSettingsProvider))]` as the last DataRow above
   `OnlyTheLazySettingsAccessor_ReadsTheMcmInstance`, and
   `    [DataRow(typeof(IShadowfaxScreensSettingsProvider), typeof(ShadowfaxScreensSettingsProvider))]`
   as the last DataRow above `Provider_ResolvesFromARealContainer`. If the file does not exist, skip
   this and note it in the hand-off (Maintenance notes route the rows to whichever WP lands later).

Build.

**Verify**: the build FAILS, and every error is `CS0246`, `CS0234` or `CS0103` naming a
`TAOM.Features.ShadowfaxScreens` namespace, type or member, or `QuestJournalAdapter`/`IQuestJournalAdapter`,
or `CS1061`/`CS0117` naming `BattleLoadDiagnosticsSettings` and one of the five new settings
(`ShadowfaxScreensSettingsProviderTests` reads them directly; Step 7.5 adds them). Any other error (for example on `FakeStampClock`, `RecordingLogger`, `CallSwap`, `TaomFeatureModule`):
STOP.

### Step 7 (GREEN): transplant the production code

1. Copy the 34 feature files and the two adapters:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && S=b747cf398 && for f in $(git ls-tree -r --name-only $S Main/Features/ShadowfaxScreens | grep -v -e InstantTransitionRewriter.cs -e '/UI/ShadowfaxScreensPrefab.cs'); do mkdir -p "$(dirname "$f")"; git show "$S:$f" > "$f"; done && git show $S:Main/Adapters/IQuestJournalAdapter.cs > Main/Adapters/IQuestJournalAdapter.cs && git show $S:Main/Adapters/QuestJournalAdapter.cs > Main/Adapters/QuestJournalAdapter.cs && git status --porcelain -uall | grep -c '^?? Main/' && test ! -e Main/Features/ShadowfaxScreens/UI && test ! -e Main/Features/ShadowfaxScreens/InstantTransitionRewriter.cs && echo no-instant-transitions
```

**Verify**: `36`, then `no-instant-transitions`.

2. Adapt: category numbers, the three placeholder summaries, and the v1.5.5 cites.

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && D=Main/Features/ShadowfaxScreens && grep -rl 'PatchXXX_Shadowfax' $D | xargs sed -i -e 's/PatchXXX_ShadowfaxClan/Patch127_ShadowfaxClan/g' -e 's/PatchXXX_ShadowfaxInventory/Patch128_ShadowfaxInventory/g' -e 's/PatchXXX_ShadowfaxParty/Patch129_ShadowfaxParty/g' -e 's/PatchXXX_ShadowfaxQuests/Patch130_ShadowfaxQuests/g' && \
sed -i 's|; the number is assigned when the batch is rebased\.</summary>|.</summary>|' $D/Clan/ClanFastOpenCalls.cs $D/Party/PartyFastOpenCalls.cs && \
sed -i 's| PatchXXX until the patch number is assigned at rebase\.</summary>|</summary>|' $D/Inventory/ShadowfaxInventoryCalls.cs && \
sed -i 's|ClanPartiesVM\.cs:500-503|ClanPartiesVM.cs:465-468 in v1.4.8|' $D/Clan/ClanFastOpenCalls.cs && \
sed -i 's|// Verified on the installed v1\.5\.5: SPInventoryVM has one constructor|// Verified on the installed v1.4.8 (SPInventoryVM.cs:3060, _usageType at :97): SPInventoryVM has one constructor|' $D/Inventory/Hooks/SPInventoryVM_Ctor_ShadowfaxPatch.cs && \
sed -i 's|// Verified on the installed v1\.5\.5: private void SPInventoryVM\.InitializeInventory()|// Verified on the installed v1.4.8 (SPInventoryVM.cs:4480, sites :4507-4512): private void SPInventoryVM.InitializeInventory()|' $D/Inventory/Hooks/SPInventoryVM_InitializeInventory_ShadowfaxPatch.cs && \
grep -rn 'PatchXXX\|rebase\|v1\.5\.5' $D Main/Adapters/QuestJournalAdapter.cs Main/Adapters/IQuestJournalAdapter.cs; grep -rn 'ClientOnly\|NullQuestJournalAdapter\|IDedicatedServerProvider\|TAOM\.Client' $D Main/Adapters/QuestJournalAdapter.cs; echo adapted
```

**Verify**: only `adapted` prints (no `PatchXXX`, `rebase`, `v1.5.5`, `ClientOnly`, `NullQuestJournalAdapter`,
`IDedicatedServerProvider` or `TAOM.Client` anywhere in the transplant).

3. Confirm the transplant equals the series tip except for the 14 adapted files:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && S=b747cf398 && for f in $(git ls-tree -r --name-only $S Main/Features/ShadowfaxScreens TAOM.Tests/Features/ShadowfaxScreens | grep -v -e InstantTransitionRewriter -e '/UI/ShadowfaxScreensPrefab' -e ShadowfaxScreensPrefabTests) Main/Adapters/IQuestJournalAdapter.cs Main/Adapters/QuestJournalAdapter.cs; do git show "$S:$f" | diff -q --strip-trailing-cr - "$f" >/dev/null || echo "DIFF $f"; done
```

**Verify**: exactly 14 `DIFF` lines: `Clan/ClanFastOpenCalls.cs`, the three `Clan/Hooks/*` files,
`Inventory/ShadowfaxInventoryCalls.cs`, the two `Inventory/Hooks/*` files, `Party/PartyFastOpenCalls.cs`,
`Party/Hooks/PartyVM_Ctor_ShadowfaxParty_Patch.cs`, `Quests/QuestLogDeferCalls.cs` (under
`Main/Features/ShadowfaxScreens/`), and `Party/PartyScreenFastPathServiceTests.cs`,
`ShadowfaxScreensModuleTests.cs`, `Clan/ClanFastOpenBindingTests.cs`,
`Quests/QuestLogFastOpenBindingTests.cs` (under `TAOM.Tests/Features/ShadowfaxScreens/`). A 15th
line, or a missing one: STOP.

4. `Main/Composition/FeatureModules.cs`: add
   `        new Features.ShadowfaxScreens.ShadowfaxScreensModule(),` as the LAST entry of
   `FeatureModules.All`, directly above its closing `    };`. No ordering constraint: every module's
   `RegisterServices` runs before any `InitializeStatics`, so `LoadTimeStampsModule` (earlier in the
   list) has registered `IStampClock` before the timer is resolved.

5. `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`: insert, directly above the
   class's final closing `}`, the Screen Performance group (verbatim from `ea7cce7bc`):

```csharp

    [SettingPropertyGroup("Screen Performance")]
    [SettingPropertyBool("Log Screen Open Times", Order = 0, RequireRestart = false,
        HintText = "Writes one [ScreenOpen] line per screen open to taom_debug.log, naming the screen, the phase and the milliseconds it took. Use it to compare a Fast Open switch off against on. Default OFF.")]
    public bool LogScreenOpenTimes { get; set; } = false;

    [SettingPropertyGroup("Screen Performance")]
    [SettingPropertyBool("Clan Fast Open", Order = 1, RequireRestart = false,
        HintText = "Opens the Clan screen faster by building its parties list once instead of three times. Turning it off restores vanilla's build for this screen. Read on every open. Default ON.")]
    public bool ClanFastOpen { get; set; } = true;

    [SettingPropertyGroup("Screen Performance")]
    [SettingPropertyBool("Inventory Fast Open", Order = 2, RequireRestart = false,
        HintText = "Opens the Inventory and trade screen faster by skipping work the screen repeats. Turning it off restores vanilla's build for this screen. Read on every open. Default ON.")]
    public bool InventoryFastOpen { get; set; } = true;

    [SettingPropertyGroup("Screen Performance")]
    [SettingPropertyBool("Party Fast Open", Order = 3, RequireRestart = false,
        HintText = "Opens the Party screen faster by caching lookups the screen repeats for each troop. Turning it off restores vanilla's build for this screen. Read on every open. Default ON.")]
    public bool PartyFastOpen { get; set; } = true;

    [SettingPropertyGroup("Screen Performance")]
    [SettingPropertyBool("Quest Log Fast Open", Order = 4, RequireRestart = false,
        HintText = "Opens the Quest log faster by building finished quests only when you select them. Turning it off restores vanilla's build for this screen. Read on every open. Default ON.")]
    public bool QuestLogFastOpen { get; set; } = true;
```

6. `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: append as the last entries of the
   `Instrumentation` set (directly above its closing `};`):

```csharp
        // Shadowfax Screens: the [ScreenOpen] timer lines (docs/features/shadowfax-screens.md); log lines only.
        "LogScreenOpenTimes",
```

   and as the last entries of the `Presentation` set (directly above its closing `};`):

```csharp
        // Shadowfax Screens fast-open switches: how fast this client builds its own UI screens, never what they show
        // or the campaign state behind them.
        "ClanFastOpen", "InventoryFastOpen", "PartyFastOpen", "QuestLogFastOpen",
```

Build, then Filtered with `TAOM.Tests.Features.ShadowfaxScreens|CoopVetoClassificationTests|ReflectionSiteBindingTests|TAOM.Tests.Composition|HarmonyPatchBindingTests`.

**Verify**: 0 errors; `Failed: 0`; the ShadowfaxScreens binding tests PASS rather than report
inconclusive (the laptop has the game; an inconclusive one means `GameAssemblies` did not load: STOP).
If a Shadowfax binding test fails (an IL shape or signature differs on v1.4.8), STOP: never edit a
rewrite or a binding test to make it pass. `SettingsFingerprintTests` is expected to fail until Step 8.

### Step 8: the settings census docs

1. Read the current totals:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && grep -o 'TAOM ships \*\*[0-9]*\*\* settings' docs/features/coop-interop.md; grep -o '[0-9]* in `BattleLoadDiagnosticsSettings`' docs/features/coop-interop.md; grep -o "TAOM's [0-9]* MCM settings" docs/features/bannerlord-together-compat.md
```

   Both totals must be the same number T (243 at the tip; earlier WPs may have moved it). If they
   differ, STOP.

2. Replace T by T + 5 in both docs (`**T**` in `coop-interop.md`, `TAOM's T MCM settings` in
   `bannerlord-together-compat.md`). In `coop-interop.md` also raise the split number M printed by the
   second grep (the phrase ``M in `BattleLoadDiagnosticsSettings` ``) to M + 5. Do not change the
   simulation-relevant counts (all five new settings are excluded).

Build, then Filtered with `SettingsFingerprintTests|SettingRequireRestartPostureTests`.

**Verify**: 0 errors; `Failed: 0`.

### Step 9: the feature doc, the registry sections and the reflection-site rows

1. **Feature doc.** Create it from the series tip:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git show b747cf398:docs/features/shadowfax-screens.md > docs/features/shadowfax-screens.md && sed -i -e 's/PatchXXX_ShadowfaxClan/Patch127_ShadowfaxClan/g' -e 's/PatchXXX_ShadowfaxInventory/Patch128_ShadowfaxInventory/g' -e 's/PatchXXX_ShadowfaxParty/Patch129_ShadowfaxParty/g' -e 's/PatchXXX_ShadowfaxQuests/Patch130_ShadowfaxQuests/g' docs/features/shadowfax-screens.md && wc -l docs/features/shadowfax-screens.md
```

   (Expect 460 lines.) Then make these edits with the Edit tool, in order:

   a. Replace everything from the line `## Overview` down to (not including) the line
      `## Clan fast open` with the block below.
   b. In the inventory section, ` (the patch number is assigned at rebase),` becomes `,` (the line then
      reads ``under the category `Patch128_ShadowfaxInventory`, named by``).
   c. `` (`ClanPartiesVM.cs:500-503`) `` becomes `` (`ClanPartiesVM.cs:465-468` in v1.4.8) ``.
   d. `` (`ClanManagementVM.cs:1389-1390`) `` becomes `` (`ClanManagementVM.cs:1380-1381`) ``. Leave
      `GauntletClanScreen.cs:226-239` as it is (v1.4.8 has the same lines).
   e. In the party section, replace the three lines that run from
      `any of them touches another member or passes the code anywhere else. Side finding:` to
      `` `CharacterImageTextureProvider` (`CreateFrom(id)`), which drops the postfix's colour writes. `` with
      block E below.
   f. In `## Configuration`, delete the first paragraph (the three lines starting
      `The instant screen transitions are always on`) and the blank line after it.
   g. In `## Key Files`, delete the four rows for `InstantTransitionRewriter.cs`,
      `UI/ShadowfaxScreensPrefab.cs`, the Party prefabs (`PartyScreen.xml`, ...) and `CareerScreen.xml`.
   h. In `## Dependencies`, delete the `Bannerlord.UIExtenderEx 2.13.3` bullet.
   i. In `## Tests`, delete the three bullets for `InstantTransitionRewriterTests.cs`,
      `ShadowfaxScreensPrefabTests.cs` and `PrefabExtensionBindingTests.cs` (with their wrapped lines).
   j. Delete the whole `## How to make another screen instant` section (heading and its four items).
   k. Replace the two `## Changelog` bullets with block K below (today's date for `<YYYY-MM-DD>`).
   l. In `## GitHub Issue`, delete the `#850` line, and append ` Ported to 1.4.x; in-game check owed.`
      to each of the four remaining lines.

Block E:

```markdown
any of them touches another member or passes the code anywhere else. On v1.4.8 that premise holds:
`CharacterImageIdentifier`'s constructor copies only `CharacterCode.Code` into the image id
(`CharacterImageIdentifier.cs:5-10`), so colour writes made after `CreateFrom` never reach the portrait.
```

Block K:

```markdown
- <YYYY-MM-DD>: ported to the 1.4.x line from the 1.5.x line (#851 to #854): the four fast opens,
  the "Screen Performance" switches and the open timer. Not yet measured in game: each screen's change
  is kept only if the A/B shows a median saving of at least 16 ms.
```

Block for edit a (outer fence is `~~~~`; keep the inner triple-backtick fence):

~~~~markdown
## Overview

Four campaign screens skip work they repeat while they build: the clan screen builds its parties
list once instead of three times, the inventory and trade screens skip a name sort they replace a
moment later, the party screen builds each troop portrait code once per screen, and the quest log
builds a finished quest's stages only when the player selects it. Each has a switch in the
"Screen Performance" group of the Battle Load Diagnostics MCM page (on by default), and "Log Screen
Open Times" (off by default) writes one timing line per open so each switch can be compared off
against on. What the screens show is unchanged.

Ported to the 1.4.x line from the 1.5.x line's screen open speed series (#851 to #854). The 1.5.x
line also gives these screens instant transitions; that part is not on this line (decision D6 of the
2026-11-04 port programme: a visible change with no setting).

## Why This Exists

- **Vanilla behavior (v1.4.8):** the clan screen builds its party lists three times per open
  (`ClanPartiesVM.cs:387`, then `:388` to `:401`, then the `ClanManagementVM` constructor's chain
  `:1157`, `:1316`, `:1260`, `:1203`); `SPInventoryVM.InitializeInventory` sorts both item lists by
  name (`SPInventoryVM.cs:4507-4512`) and both of its callers sort them again right after
  (`:3134-3142`, `:3799-3800`); the party screen computes a character code for every troop row
  (`PartyCharacterVM.cs:175`, `:183`, `:1476`); the quest log builds every finished quest's stage
  list inside the `QuestsVM` constructor (`QuestItemVM.cs:358-361`).
- **TAOM requirement:** TAOM saves carry large clans, merchant stocks, armies and quest histories,
  and players open these screens many times a session.
- **Without this feature:** every open repeats that work before the screen appears.

## Architecture

### Design Challenge

The repeated work sits inside vanilla view-model constructors, and the same methods also serve
callers that must keep working: the clan list build is how a category refresh shows a disbanded
party, the inventory sort runs again after a Reset, and a party row's code depends on its side and
the screen's owner parties. Each skip has to stand only where vanilla provably redoes the work, leave
every other caller alone, and fall back to vanilla when the engine's code changes shape.

### Solution Approach

- **One feature module:** `ShadowfaxScreensModule` (see [composition-root.md](composition-root.md))
  registers the settings provider, the `[ScreenOpen]` timer and the four screen services, hands each
  hook facade its service, and declares four patch categories applied at GameInit:
  `Patch127_ShadowfaxClan`, `Patch128_ShadowfaxInventory`, `Patch129_ShadowfaxParty` and
  `Patch130_ShadowfaxQuests` (the 1.5.x numbers, kept).
- **Call-site swaps, not skipping prefixes:** the clan skip and the inventory name-sort skip each
  rewrite one call inside one vanilla method and leave the method vanilla, with one install line at
  game start, when the expected call is not found. A bool prefix would still run every postfix of
  the skipped method.
- **Per-screen state with a lifetime:** the party portrait cache lives for one `PartyVM` session; the
  quest log marks deferred items in a weak table and builds each one when it is selected.
- **Switches read per open:** a change applies to the next open without a restart; an unreadable
  switch returns its shipped default.
- **The timer:** with "Log Screen Open Times" on, `ScreenOpenTimer` writes one
  `[ScreenOpen] <screen> <phase>: <ms> ms (<detail>)` line per open, timed with `IStampClock` from
  Load-Time Stamps.

### Component Diagram

```
BattleLoadDiagnosticsSettings ("Screen Performance") -> ShadowfaxScreensSettingsProvider
ShadowfaxScreensModule -> ScreenOpenTimer, ClanScreenFastPathService, InventoryOpenFastPathService,
                          PartyScreenFastPathService, QuestLogDeferService (+ QuestJournalAdapter)
Harmony hooks (four categories, GameInit) -> *Calls facades -> the services
```

~~~~

   Verify the doc:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && grep -n 'PatchXXX\|v1\.5\.5\|rebase\|InstantTransition\|ShadowfaxScreensPrefab\|#850\|UIExtenderEx\|How to make another screen' docs/features/shadowfax-screens.md; grep -c '^## ' docs/features/shadowfax-screens.md; grep -c '<YYYY-MM-DD>' docs/features/shadowfax-screens.md
```

   **Verify**: the first grep prints nothing; then `13` (Overview, Why This Exists, Architecture, Clan
   fast open, Inventory and trade fast open, Party fast open, Quest log fast open, Configuration, Key
   Files, Dependencies, Tests, Changelog, GitHub Issue); then `0`.

2. **Registry sections.** Extract the four series sections, adapt them, and insert them:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && for h in PatchXXX_ShadowfaxClan PatchXXX_ShadowfaxInventory PatchXXX_ShadowfaxParty PatchXXX_ShadowfaxQuests; do git show b747cf398:docs/reference/harmony-patch-registry.md | awk -v h="## $h" 'index($0,h)==1 && length($0)==length(h){p=1; print; next} p && /^## /{p=0} p && /^<!-- backlinks-start/{p=0} p{print}'; done > ../125-registry-sections.md && grep -c '^## PatchXXX_Shadowfax' ../125-registry-sections.md
```

   **Verify**: `4`. Then adapt the scratch file `C:\Users\mikew\source\repos\taom-port\125-registry-sections.md`:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && F=../125-registry-sections.md && sed -i -e 's/PatchXXX_ShadowfaxClan/Patch127_ShadowfaxClan/g' -e 's/PatchXXX_ShadowfaxInventory/Patch128_ShadowfaxInventory/g' -e 's/PatchXXX_ShadowfaxParty/Patch129_ShadowfaxParty/g' -e 's/PatchXXX_ShadowfaxQuests/Patch130_ShadowfaxQuests/g' \
 -e 's|; the number is assigned when the Shadowfax batch is rebased)|)|' \
 -e 's| (the number is assigned at rebase)||' \
 -e 's|\*\*Target (number assigned at rebase; `PatchXXX` is a placeholder):\*\*|**Target:**|' \
 -e 's|The category number is not assigned yet; it applies|The category is `Patch128_ShadowfaxInventory`; it applies|' \
 -e 's|(v1\.5\.5)|(v1.4.8)|g' -e 's|Targets (v1\.5\.5)|Targets (v1.4.8)|' -e 's|All v1\.5\.5 `TaleWorlds|All v1.4.8 `TaleWorlds|' -e 's|lines 175 and 183 in v1\.5\.5|lines 175 and 183 in v1.4.8|' \
 -e 's|`ClanPartiesVM\.cs:414-415, 428`|`ClanPartiesVM.cs:387-388, 401`|' -e 's|`ClanManagementVM\.cs:1166, 1325, 1269, 1212`|`ClanManagementVM.cs:1157, 1316, 1260, 1203`|' -e 's|(`:1372`)|(`:1363`)|' -e 's|`ClanManagementVM\.cs:1389-1390`|`ClanManagementVM.cs:1380-1381`|' -e 's|`ClanPartiesVM\.cs:500-503`|`ClanPartiesVM.cs:465-468`|' \
 -e 's|(`GetJournalLogAt` lines 15-39 in the decompile)|(`QuestItemSortControllerVM.cs:25-39` in v1.4.8)|' \
 -e 's|Patch23.s party-row colours never show on v1\.5\.5 regardless: the portrait renderer rebuilds the image from the `Code` string through `CharacterImageTextureProvider` (`CreateFrom(id)`)\.|On v1.4.8 `CharacterImageIdentifier` copies only `CharacterCode.Code` into the image id (`CharacterImageIdentifier.cs:5-10`), so those colour writes never reach a party row.|' \
 $F && grep -n 'PatchXXX\|rebase\|placeholder\|v1\.5\.5\|1\.5\.3' $F; git grep -c '"Patch103_MissionStartGuard"' -- Main | head -1
```

   **Verify**: the first grep prints at most one line, the quest section's sentence
   `...so there is no placeholder to show` (that "placeholder" is about portraits; keep it). The last
   grep prints a count of at least 1; if it prints nothing, Patch103 is not on this line yet: delete
   the clause `, as Patch103 does` from the scratch file. Then insert the scratch file into the
   registry with this script (it keeps the file's own line endings):

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && python - <<'EOF'
import re
p = 'docs/reference/harmony-patch-registry.md'
raw = open(p, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in raw else '\n'
block = open('../125-registry-sections.md', encoding='utf-8', newline='').read().replace('\r\n', '\n').rstrip('\n').split('\n')
lines = raw.split(nl)
idx = None
for i, l in enumerate(lines):
    m = re.match(r'^## Patch(\d+)[a-z]?_', l)
    if (m and int(m.group(1)) > 130) or l.startswith('## Patch_MissionTime_SetMovementOrder'):
        idx = i
        break
assert idx is not None, 'anchor not found'
assert not any(l.startswith('## Patch127_ShadowfaxClan') for l in lines), 'already present'
lines[idx:idx] = block + ['']
open(p, 'w', encoding='utf-8', newline='').write(nl.join(lines))
print('inserted before line', idx + 1)
EOF
grep -c '^## Patch12[7-9]_Shadowfax\|^## Patch130_ShadowfaxQuests' docs/reference/harmony-patch-registry.md
```

   **Verify**: `inserted before line N`; then `4`.

3. **Reflection-site rows.** Append two rows to the end of the Category B table:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && python - <<'EOF'
p = 'docs/reference/taleworlds-api-snapshot/reflection-sites.md'
raw = open(p, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in raw else '\n'
lines = raw.split(nl)
start = next(i for i, l in enumerate(lines) if l.startswith('## Category B'))
idx = next(i for i in range(start, len(lines)) if lines[i].startswith('Status ('))
while idx > 0 and lines[idx - 1].strip() == '':
    idx -= 1
rows = [
 "| `TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestItemVM` | `PopulateQuestLog` | method (private) | `QuestJournalAdapter.cs:30` | Quest Log Fast Open (#854). Cached once at `Bind` and replayed per journal entry to build a deferred finished quest when it is selected. Missing: `Bind` returns a reason, the install line is a warning, and vanilla builds every quest at open |",
 "| `TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestsVM` | `SetSelectedItem` | method (private) | `QuestJournalAdapter.cs:39` | Quest Log Fast Open (#854). Looked up by name for the attached-patch check (`Bind`, and again on every open). Missing: the `SetSelectedItem` patch reads as not attached, `Bind` returns a reason and no open defers |",
]
assert lines[idx - 1].startswith('|'), 'the line above the insertion point is not a table row'
lines[idx:idx] = rows
open(p, 'w', encoding='utf-8', newline='').write(nl.join(lines))
print('inserted at line', idx + 1)
EOF
grep -c 'QuestJournalAdapter.cs:30\|QuestJournalAdapter.cs:39' docs/reference/taleworlds-api-snapshot/reflection-sites.md
```

   **Verify**: `inserted at line N`; then `2`.

Run the Docs command.

**Verify**: `dead_links` and `orphan_features` equal Step 0.6's values (the registry sections link
the new feature doc, so it is not an orphan); `ai_dashes: 0`.

### Step 10: regenerate the patch-target snapshot

Build first (the generator reads `TAOM.Tests\bin\Debug\net472\TAOM.dll`), then:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && cp docs/reference/taleworlds-api-snapshot/patch-targets.md ../125-patch-targets.before.md && cp docs/reference/taleworlds-api-snapshot/gamemodel-bases.md ../125-gamemodel-bases.before.md && pwsh -NoProfile -File tools/snapshot_api_surface.ps1 && git diff --stat -- docs/reference/taleworlds-api-snapshot/ && git diff -U0 -- docs/reference/taleworlds-api-snapshot/patch-targets.md | grep '^[-+]| ' | grep -vc 'TAOM\.Features\.ShadowfaxScreens\.'
```

Then decide:

- `gamemodel-bases.md` changed: restore it (`cp ../125-gamemodel-bases.before.md docs/reference/taleworlds-api-snapshot/gamemodel-bases.md`);
  this WP adds no game model.
- The last command printed `0` (only the 17 Shadowfax rows changed): keep the regenerated
  `patch-targets.md`, and if its backlinks footer (the `<!-- backlinks-start` ... `<!-- backlinks-end -->`
  block at the end) is gone, copy that block back from `../125-patch-targets.before.md` by hand.
- It printed more than `0` (an earlier WP did not regenerate): put the previous file back and merge
  only our 17 rows into it, taken from the regenerated output:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && cp docs/reference/taleworlds-api-snapshot/patch-targets.md ../125-patch-targets.regen.md && cp ../125-patch-targets.before.md docs/reference/taleworlds-api-snapshot/patch-targets.md && python - <<'EOF'
import re
p = 'docs/reference/taleworlds-api-snapshot/patch-targets.md'
raw = open(p, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in raw else '\n'
regen = open('../125-patch-targets.regen.md', encoding='utf-8', newline='').read().replace('\r\n', '\n').split('\n')
rows = [l for l in regen if l.startswith('| `TAOM.Features.ShadowfaxScreens.')]
assert len(rows) == 17, len(rows)
lines = raw.split(nl)
anchor = max(i for i, l in enumerate(lines) if l.startswith('| `TAOM.Features.ShaderPrecompilation.'))
lines[anchor + 1:anchor + 1] = rows
lines = [re.sub(r'^Patches: (\d+)\.', lambda m: 'Patches: %d.' % (int(m.group(1)) + 17), l) for l in lines]
open(p, 'w', encoding='utf-8', newline='').write(nl.join(lines))
print('merged 17 rows after line', anchor + 1)
EOF
```

**Verify** (either path): `git diff --stat -- docs/reference/taleworlds-api-snapshot/` lists only
`patch-targets.md` and `reflection-sites.md` (Step 9's two rows);
`grep -c '^| \`TAOM.Features.ShadowfaxScreens\.' docs/reference/taleworlds-api-snapshot/patch-targets.md`
prints `17`; the file still ends with the `<!-- backlinks-end -->` line.

### Step 11: Commit B

Full Test command. **Verify**: `Failed: 0`; `Total` = (Part A total) + 224, or + 226 with the
conditional `CampaignHotPathSettingsProvidersTests` rows (222 Shadowfax results plus 2 reflection-site
rows; if the runner reports each data-driven method once instead of per row, the Shadowfax files
give 215 and the reflection rows 0: then `Total` = Part A total + 215). Any other delta: STOP and
report the filtered counts.

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git add Main/Features/ShadowfaxScreens Main/Adapters/IQuestJournalAdapter.cs Main/Adapters/QuestJournalAdapter.cs TAOM.Tests/Features/ShadowfaxScreens Main/Composition/FeatureModules.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs docs/features/shadowfax-screens.md docs/features/coop-interop.md docs/features/bannerlord-together-compat.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reference/taleworlds-api-snapshot/patch-targets.md && { test -f TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs && git add TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs; true; } && git status --porcelain
```

**Verify**: `git status --porcelain` prints nothing unstaged or untracked (every line starts with a
staged code such as `A ` or `M `).

Write `C:\Users\mikew\source\repos\taom-port\125-msg-B.txt` with exactly:

```text
feat(ui): v2.0.28 - four campaign screens open faster (Shadowfax)

The clan, inventory and trade, party and quest screens skip work they
repeat while they build. The clan screen builds its parties list once
per open instead of three times. The inventory and trade screens skip
a name sort that the screen's own sort replaces a moment later. The
party screen builds each troop portrait code once per screen instead
of once per row. The quest log builds a finished quest's stages only
when the player selects it. What the screens show is unchanged.

Each change has a switch in the new "Screen Performance" group of the
Battle Load Diagnostics mod options page, on by default and read on
every open; turning one off restores the vanilla build. "Log Screen
Open Times" (off by default) writes one timing line per screen open
to the TAOM debug log, so each switch can be measured off against on.
Each rewrite checks the game's code when a campaign starts and leaves
its screen vanilla, with one log line, if the code is not as expected.

This is the screen speed half of the 1.5.x Shadowfax Screens work; the
instant screen transitions are not part of this change.

Research: taom-src v1.4.8 of the eight vanilla view models patched
Not-tested: every screen in game, and the A/B timing per switch
Save-compat: none; no saved state, five new MCM settings
Ported-from: ea7cce7bc06475aea4e0a4d48a5087d3e1c5ec01
Ported-from: 11aeb8aee7e4976ba22b7d814096b433aa982c83
Ported-from: 33c3bdf1a15732d014a9a8920bdf7e3acea07418
Ported-from: 3f816822837914717ac1d33ce214c0218203ae07
Ported-from: 3db115423b16355e711c00f0c86a4f63102cef3e
Ported-from: c3266287205bc14f049bab5ff693c3888172386f
Ported-from: 22695bd017a35bf6a6fe6e537755d3c36c694142
Ported-from: 579bb93f00f3581996f451ea2353de832c1ba93d
Ported-from: 642f92e3966ee4d33255c9ed2b26d92d26fd8c77
Ported-from: 402280dc23cdbc26054cf76922ae77b94c74ac0a
Ported-from: b747cf398b74914d832462c7b119470df4f7ef9a
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git commit -F C:/Users/mikew/source/repos/taom-port/125-msg-B.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` prints nothing.

**Part B done when**: the commit exists; build 0 errors; full suite `Failed: 0` with the Step 11
delta; `git diff --name-only HEAD~1 HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj` prints
nothing; `grep -c 'new Features.ShadowfaxScreens.ShadowfaxScreensModule()' Main/Composition/FeatureModules.cs`
prints `1`; `grep -rn 'PatchXXX' Main TAOM.Tests docs/features docs/reference` prints nothing; lint
values unchanged and `ai_dashes: 0`.

If your session must end inside Part B, stop at a step boundary without committing and report the
last completed step; the next session resumes in the same worktree.

---

## Part C: the map profiler times the service wait menu (`943ab74c0`, MapPerf half)

Run Part C only if Step 0.4 passed. It changes TAOM.Dependencies (`PatchShieldPolicy.cs`), so any
release that contains it must ship TAOM and TAOM.Dependencies from one build (#371).

Source: the MapPerf half of `943ab74c0`. Simulation (onto the 1.4.x tip): the four MapPerf files, five
MapPerf tests, `PatchShieldPolicy.cs` and `PatchCategoryNumberTests.cs` conflict (absent at the tip).
After WP117 they exist, but `943ab74c0^` also carries the co-op edits `a15563a3c`, `d021426b0` and
`6d7e67f3f` (client assembly scan, `[ClientOnly]`) that WP117 does not port, so the hunks' context
differs. Resolution: hand-apply each hunk at the anchors named below, without the co-op parts.

### Step 12 (RED): the probe tests

Apply these `943ab74c0` test hunks by hand (read them with
`git show 943ab74c0 -- TAOM.Tests/Features/MapPerf`), adapted as stated:

1. `TAOM.Tests/Features/MapPerf/MapFrameProfilerHooksTests.cs`: add the two tests
   `RecordProbe_NotMeasuring_RecordsNoEntry` and
   `RecordProbe_Measuring_TaomOwnedCountsTowardTaomMs_AndTheOtherDoesNot` verbatim, directly above
   `Step_FirstBoundaryOfACampaign_OpensASessionAndLogsTheHeader` (they use the file's existing
   `ProbeView`, `OwnerA`, `OwnerB`, `Step`, `CloseFrame`, `_campaignA` and `Profiler`).
2. `TAOM.Tests/Features/MapPerf/MapFrameProfilerInstallerTests.cs`, following the hunks:
   - `Apply(bool core = true, bool views = true, int sites = 1)` gains `, bool menu = true`, and the
     line `if (category == MapFrameProfilerInstaller.MenuCategory) return menu;` goes directly above its
     `Assert.AreEqual(MapFrameProfilerInstaller.ViewsCategory, category);`;
   - `CollectionAssert.AreEqual(new[] { MapFrameProfilerInstaller.Category, MapFrameProfilerInstaller.ViewsCategory }, _applied);`
     gets `MapFrameProfilerInstaller.MenuCategory` as a third element, followed by the new line
     `_logger.Received(1).LogInfo(MapProfileLines.BuildMenuProbesLine(3, 3));`;
   - the three tests `OnGameInitialized_ToggleOff_NeverAppliesTheMenuCategory`,
     `OnGameInitialized_MenuCategoryFails_StillInstallsAndCountsWhatIsPatched` and
     `OnGameInitialized_MenuApplyThrows_StillInstalls` are added verbatim;
   - `Assert.AreEqual(2, _applied.Count, "Each category is applied once per process.");` becomes `3`,
     and the `_logger.Received(1).LogInfo(Arg.Any<string>());` two lines below it becomes `Received(2)`;
   - the `Assert.AreEqual(2, _applied.Count);` after `Install(Apply(core: false));` becomes `3`.
   Ignore the hunk context line `ClientComposition.Current = null;` (1.5.x co-op only).
3. `TAOM.Tests/Features/MapPerf/MapProfileLinesTests.cs`: add `BuildMenuProbesLine_MatchesThePinnedLiteral`
   and `BuildMenuFailedLine_QuotesTheMessageWithoutSquareBrackets` verbatim; add
   `MapProfileLines.BuildMenuProbesLine(2, 3),` and
   `MapProfileLines.BuildMenuFailedLine(new InvalidOperationException("[x]")),` after
   `MapProfileLines.BuildViewsShortLine(1, 3),` in the list of built lines, and the two matching
   literals (`"[MapProfiler] menu probes: 2/3 patched",` and
   `"[MapProfiler] menu probes failed, the profiler stays installed without them: InvalidOperationException: (x)",`)
   after the `"... TAOM map view overrides patched; the others run unattributed inside mapScreenMs",`
   literal in the expected list.
4. `TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs`: add `using TAOM.Features.Enlistment;`;
   add `MenuProbes_AreASubCategory_WithTheProfilerSuffix_AndTheBracketPriorities` WITHOUT the
   `// ADR-013 gate 4` comment line and the two-line `Assert.AreEqual(...)` under it that reads
   `ClientOnlyAttribute` (1.4.x has no such attribute); add
   `MenuProbes_EachPatchResolvesItsOwnMethod` verbatim; add
   `typeof(GameMenuVM_OnFrameTick_MapProfiler_Patch)` to the array in
   `ProfilerOnlyTargets_AreOnPatchShieldsExclusionList` (and its comment's list of methods); add
   `ProfilerOnlyTargets_AreSharedWithNoOtherTaomPatchClass` with
   `var assemblies = new[] { typeof(MapFrameProfilerInstaller).Assembly };` (1.4.x has no client
   assembly; drop the `ManyHammers` reference and the "client assembly" comment) and its `Key` helper;
   add `typeof(GameMenuVM_ContextText_MapProfiler_Patch),` to the list in
   `SharedMapTargets_StayUnderPatchShield`; in the class summary say three profiler-only targets
   (`Campaign.Tick`, `CampaignEvents.Tick`, `GameMenuVM.OnFrameTick`) and four shared ones.
5. `TAOM.Tests/Features/MapPerf/MapProfilerTargetsTests.cs`: if the file exists, add the
   `[ClassInitialize]` below (if absent), the three usings and the test; if it does not exist (on 1.5.x
   it was created by the co-op commit `a15563a3c`, not ported), create it with exactly:

```csharp
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.MapPerf.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapPerf;

[TestClass]
[TestCategory("RequiresGame")]
public class MapProfilerTargetsTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void MenuTargets_ResolveToTheThreeMenuProbeMethods_InOrder()
    {
        var targets = MapProfilerTargets.MenuTargets();

        Assert.AreEqual(3, targets.Count);
        Assert.IsTrue(targets.All(t => t != null), "A menu probe target did not resolve.");
        Assert.AreEqual("GameMenuVM.OnFrameTick", targets[0]!.DeclaringType!.Name + "." + targets[0]!.Name);
        Assert.AreEqual(0, targets[0]!.GetParameters().Length);
        Assert.AreEqual("GameMenuVM.set_ContextText", targets[1]!.DeclaringType!.Name + "." + targets[1]!.Name);
        CollectionAssert.AreEqual(new[] { typeof(string) }, targets[1]!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(ServiceStatusService).GetMethod(nameof(ServiceStatusService.RefreshIfChanged)), targets[2]);
        Assert.AreEqual(0, targets[2]!.GetParameters().Length);
    }
}
```

6. Only if `TAOM.Tests/Composition/PatchCategoryNumberTests.cs` exists (WP122): add
   `        ["Patch101_MapFrameProfiler_Menu"] = "sub-category documented under ## Patch101_MapFrameProfiler",`
   to `NoRegistryHeading`, after the `Patch101_MapFrameProfiler_Views` entry.

Build.

**Verify**: the build FAILS only with `CS0117`/`CS0246`/`CS1061` naming `MenuCategory`, `MenuTargets`,
`RecordProbe`, `BuildMenuProbesLine`, `BuildMenuFailedLine`, `GameMenuVM_OnFrameTick_MapProfiler_Patch`,
`GameMenuVM_ContextText_MapProfiler_Patch` or `ServiceStatusService_RefreshIfChanged_MapProfiler_Patch`.
Anything else: STOP.

### Step 13 (GREEN): the probes and the exclusion

1. `Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs`: add `using System;` if absent, and add the
   method `internal static void RecordProbe(Type key, ProbeStamp start, bool taomOwned)` (with its
   `<summary>` line) exactly as the hunk in `git show 943ab74c0 -- Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs`
   adds it, directly after the closing brace of `RecordView`.
2. `Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs`: from
   `git show 943ab74c0 -- Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs` take, exactly as
   added there: the constant `internal const string MenuCategory = "Patch101_MapFrameProfiler_Menu";`
   (after `ViewsCategory`), the call `ApplyMenuProbes(logger, tryPatchCategory, isPatched);` (on the line
   after `var views = tryPatchCategory(ViewsCategory);`), and the method
   `private static void ApplyMenuProbes(IModLogger logger, Func<string, bool> tryPatchCategory, Func<MethodBase, bool> isPatched)`
   (directly above the comment line `    // A later game init in the same process`). In the class summary
   change `applies both categories` to `applies its categories (core, views, menu probes)` if that phrase
   is there. Add `using System.Linq;` if the file lacks it (the method uses `Count(...)`).

3. `Main/Features/MapPerf/Hooks/MapProfilerTargets.cs`: add `using TAOM.Features.Enlistment;` after
   `using TaleWorlds.CampaignSystem.GameState;`, and insert directly after the closing `};` of
   `CoreTargets()`:

```csharp

    /// <summary>The menu probe targets (#1019): <c>GameMenuVM.OnFrameTick()</c>, the <c>ContextText</c> setter and <c>ServiceStatusService.RefreshIfChanged()</c>; null where one does not resolve.</summary>
    internal static IReadOnlyList<MethodBase?> MenuTargets()
    {
        // Found by name, as on the 1.5.x line, so this class never names the view-model assembly in a signature.
        var menu = Type.GetType("TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.GameMenuVM, TaleWorlds.CampaignSystem.ViewModelCollection", false);
        return new MethodBase?[]
        {
            menu == null ? null : AccessTools.Method(menu, "OnFrameTick", Type.EmptyTypes),
            menu == null ? null : AccessTools.PropertySetter(menu, "ContextText"),
            AccessTools.Method(typeof(ServiceStatusService), nameof(ServiceStatusService.RefreshIfChanged), Type.EmptyTypes),
        };
    }
```

4. Create the probe file from `943ab74c0` and strip the two `[ClientOnly]` markers and the ADR-013
   sentence (1.4.x has no client split, D7):

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && F=Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerMenu.cs && git show 943ab74c0:$F > $F && sed -i -e '/^\[ClientOnly\]$/d' -e 's|^// MapProfilerTargets.MenuTargets(). The two GameMenuVM classes are \[ClientOnly\] (they name a client-only assembly,$|// MapProfilerTargets.MenuTargets(). Main thread.|' -e '/^\/\/ ADR-013); the ServiceStatusService one names none, so the gate would flag the marker as stale\. Main thread\.$/d' $F && grep -c 'ClientOnly\|ADR-013' $F; grep -c '^public static class .*_MapProfiler_Patch$' $F; tail -n +17 $F | head -2
```

   **Verify**: `0`, then `3`, then the two comment lines `// The three entries are nested ... Targets come from`
   and `// MapProfilerTargets.MenuTargets(). Main thread.` (the three classes keep `[HarmonyPatch]`,
   `[HarmonyPatchCategory(MapFrameProfilerInstaller.MenuCategory)]`, `TargetMethod()`, a `First`
   prefix and a `Last` postfix each).

5. `Main/Features/MapPerf/MapProfileLines.cs`: add `BuildMenuProbesLine(int patched, int total)` and
   `BuildMenuFailedLine(Exception ex)` (with their `<summary>` lines) exactly as the hunk in
   `git show 943ab74c0 -- Main/Features/MapPerf/MapProfileLines.cs` adds them, directly after
   `BuildViewsShortLine`.

6. `Dependencies/Foundation/PatchShieldPolicy.cs`: in `ExcludedTargetMethods`, directly after the
   `"TaleWorlds.CampaignSystem.CampaignEvents.Tick",` entry, add:

```csharp
        // The map profiler's menu probe target (#1019): only the profiler patches GameMenuVM.OnFrameTick, so it is
        // excluded like Campaign.Tick and CampaignEvents.Tick, for parity with the profiler off, when nothing TAOM
        // patches it. A missing-API error thrown inside it is then not caught here: it leaves through
        // GauntletMenuBaseView.OnFrameTick and MenuViewContext.OnFrameTick, which have no catch on v1.4.8, into
        // MapScreen.OnFrameTick (MapScreen.cs:1332, :1340), which Patch36 patches and so stays shielded
        // (docs/features/map-perf-profiler.md, "PatchShield"). The ContextText setter beside it is Patch64's too
        // and stays shielded. MapFrameProfilerBindingTests.ProfilerOnlyTargets_AreOnPatchShieldsExclusionList walks it.
        "TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.GameMenuVM.OnFrameTick",
```

   If the list's summary text names "the two campaign-map entries" in so many words, make it "the three
   campaign-map and menu entries"; otherwise leave the summary.

Build, then Filtered with `TAOM.Tests.Features.MapPerf|PatchShieldPolicyTests|PatchShieldAgentTickExclusionBindingTests|PatchCategoryNumberTests|HarmonyPatchBindingTests|TAOM.Tests.Features.Enlistment`.

**Verify**: 0 errors; `Failed: 0`. If a `PatchShieldPolicyTests` test pins the exact list of
`ExcludedTargetMethods` entries, add the new entry to that list in the same step (never weaken an
assertion), then re-run.

### Step 14: Part C docs and Commit C

1. `docs/reference/harmony-patch-registry.md`, section `## Patch101_MapFrameProfiler`: append the text
   below (one space first, same line) to the end of its `**Target:**` paragraph; and where the section
   lists the methods on `PatchShieldPolicy.ExcludedTargetMethods` (`Campaign.Tick` and
   `CampaignEvents.Tick`), add ``and (#1019) `GameMenuVM.OnFrameTick` `` to that list.

```markdown
Menu category `Patch101_MapFrameProfiler_Menu` (#1019, applied only with the toggle on): `GameMenuVM.OnFrameTick()`, the `GameMenuVM.ContextText` setter and `ServiceStatusService.RefreshIfChanged()`, each a prefix `First` and a postfix `Last` bound by `TargetMethod()` (`MapProfilerTargets.MenuTargets()`), entries `GameMenuVM`, `Patch64_MenuLinkColors` (counts toward `taomMs`) and `ServiceStatusService`, nested and not additive. `Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerMenu.cs`.
```

2. `docs/features/map-perf-profiler.md`: apply the `943ab74c0` hunks
   (`git show 943ab74c0 -- docs/features/map-perf-profiler.md`) at their matching anchors, adapted:
   drop the sentence about the two `GameMenuVM` classes being `[ClientOnly]` and every mention of
   `TAOM.Client.dll`; delete `` (`PatchShield.cs` ~200-209) `` (keep the claim that PatchShield never
   shields a method declared in a TAOM assembly); in the "Given up on `GameMenuVM.OnFrameTick`"
   paragraph replace `` (`SandBox.View.Map.MapScreen`, v1.5.5, :1340) `` with
   `` (`SandBox.View.Map.MapScreen`, v1.4.8, `MapScreen.cs:1332`, `:1340`) ``. Keep its Patch89 mentions
   only if `git grep -n '"Patch89_' -- Main` finds a category (WP118 ports it); otherwise replace
   `` (Patch89 is applied in `OnSubModuleLoad`) `` with `(Patch36 patches it)` and drop `Patch89's` from
   the list of stripped patches. If an anchor of a hunk is missing in the 1.4.x doc, skip that hunk and
   list it in the hand-off.
3. Snapshot: build, then:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && cp docs/reference/taleworlds-api-snapshot/patch-targets.md ../125-patch-targets.before-c.md && cp docs/reference/taleworlds-api-snapshot/gamemodel-bases.md ../125-gamemodel-bases.before-c.md && pwsh -NoProfile -File tools/snapshot_api_surface.ps1 && git diff -U0 -- docs/reference/taleworlds-api-snapshot/patch-targets.md | grep '^[-+]| ' | grep -v -e 'GameMenuVM_ContextText_MapProfiler_Patch' -e 'GameMenuVM_OnFrameTick_MapProfiler_Patch' -e 'ServiceStatusService_RefreshIfChanged_MapProfiler_Patch' | wc -l
```

   Restore `gamemodel-bases.md` from `../125-gamemodel-bases.before-c.md` if it changed. If the count
   is `0`, keep the regenerated `patch-targets.md` (restore the backlinks footer by hand if it is
   gone). Otherwise merge only the three rows:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && cp docs/reference/taleworlds-api-snapshot/patch-targets.md ../125-patch-targets.regen-c.md && cp ../125-patch-targets.before-c.md docs/reference/taleworlds-api-snapshot/patch-targets.md && python - <<'EOF'
import re
p = 'docs/reference/taleworlds-api-snapshot/patch-targets.md'
raw = open(p, encoding='utf-8', newline='').read()
nl = '\r\n' if '\r\n' in raw else '\n'
regen = open('../125-patch-targets.regen-c.md', encoding='utf-8', newline='').read().replace('\r\n', '\n').split('\n')
pick = lambda name: [l for l in regen if l.startswith('| `TAOM.Features.MapPerf.Hooks.' + name)]
menu = pick('GameMenuVM_ContextText_MapProfiler_Patch') + pick('GameMenuVM_OnFrameTick_MapProfiler_Patch')
board = pick('ServiceStatusService_RefreshIfChanged_MapProfiler_Patch')
assert len(menu) == 2 and len(board) == 1, (menu, board)
lines = raw.split(nl)
a = max(i for i, l in enumerate(lines) if l.startswith('| `TAOM.Features.MapPerf.Hooks.CampaignEvents_Tick_MapProfiler_Patch'))
lines[a + 1:a + 1] = menu
b = max(i for i, l in enumerate(lines) if l.startswith('| `TAOM.Features.MapPerf.Hooks.MapView_'))
lines[b + 1:b + 1] = board
lines = [re.sub(r'^Patches: (\d+)\.', lambda m: 'Patches: %d.' % (int(m.group(1)) + 3), l) for l in lines]
open(p, 'w', encoding='utf-8', newline='').write(nl.join(lines))
print('merged 3 rows')
EOF
```

   **Verify**: `grep -c 'MapProfiler_Patch' docs/reference/taleworlds-api-snapshot/patch-targets.md`
   prints exactly 3 more than `grep -c 'MapProfiler_Patch' ../125-patch-targets.before-c.md`, and the
   file still ends with the `<!-- backlinks-end -->` line. If the merge script's assertion fails
   (WP117 named a row differently), STOP.
4. Docs command. **Verify**: lint values unchanged; `ai_dashes: 0`.
5. Full Test command. **Verify**: `Failed: 0`; `Total` = (Part B total) + 11.

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git add Main/Features/MapPerf Dependencies/Foundation/PatchShieldPolicy.cs TAOM.Tests/Features/MapPerf docs/features/map-perf-profiler.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md && { for f in TAOM.Tests/Composition/PatchCategoryNumberTests.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs; do git diff --quiet -- "$f" 2>/dev/null || git add "$f"; done; true; } && git status --porcelain
```

**Verify**: nothing unstaged or untracked remains.

Write `C:\Users\mikew\source\repos\taom-port\125-msg-C.txt` with exactly:

```text
perf(enlistment): v2.0.28 - map profiler times the service wait menu

With "Enable Map Profiler" on, the campaign map profiler now also
times three things on the per-frame path of the enlisted soldier's
service wait menu: the game's menu redraw, TAOM's menu link colouring
and the status board refresh. They answer whether the service menu
costs more per frame than a vanilla wait menu. Nothing is patched
while the profiler is off. The menu redraw joins the two other
methods only the profiler patches on PatchShield's exclusion list,
so switching the profiler on does not change how an error there is
handled.

This changes TAOM.Dependencies: a release that contains it must ship
TAOM and TAOM.Dependencies from the same build.

Research: taom-src v1.4.8 GameMenuVM.OnFrameTick and ContextText
Not-tested: the menu probes in game (steps in map-perf-profiler.md)
Ported-from: 943ab74c03edfcc6cc124ef14a1b75db7ef038f4
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/125 && git commit -F C:/Users/mikew/source/repos/taom-port/125-msg-C.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` prints nothing.

**Part C done when**: the commit exists; build 0 errors; full suite `Failed: 0` at Part B total + 11;
`grep -c '"TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.GameMenuVM.OnFrameTick",' Dependencies/Foundation/PatchShieldPolicy.cs`
prints `1`; `grep -c 'ClientOnly' Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerMenu.cs` prints `0`;
`grep -c 'ClientOnlyAttribute\|ManyHammers' TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs`
prints `0`.

### Step 15: Review gate, final verification, hand-off

1. Run the Done criteria below; every one must hold.
2. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD` and ask
   it to probe the spots in "Maintenance notes". Act only on findings that change runtime behaviour or
   break this plan; land each fix as one extra commit `fix(ui): v2.0.28 - review follow-ups` (tests
   first, same commit procedure) and re-run the Done criteria. Never rewrite earlier commits. If your
   session cannot invoke skills, say so in the hand-off and the integrator runs it. Never run
   `/review-codex` or `/codex-verify` (paid; maintainer only).
3. Hand-off report (your final message): the branch name; each commit's short SHA and subject; T0
   and the final totals; whether Part C ran (and why not); whether the conditional
   `CampaignHotPathSettingsProvidersTests`, `PatchCategoryNumberTests` and `PatchShieldPolicyTests`
   edits were made; which Step 10 path was taken; any skipped doc hunk; the file Step 0.2 found
   registering `IStampClock` (if it is not `LoadTimeStampsModule.cs`, the module comment that names it
   is stale: report, do not edit); the `/deep-review` outcome; the "In-game checks owed" list.

## Disposition (every commit in the pack)

Cherry-pick simulation re-run this session:
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| 1.5.x commit | Subject | Simulation (exit, conflicted paths) | Disposition |
|---|---|---|---|
| `943ab74c0` | perf(enlistment): v2.0.35 - the service board is drawn once per change | 1; 21 (`PatchShieldPolicy.cs`, both board files, four MapPerf files, five MapPerf tests, `PatchCategoryNumberTests.cs`, 8 docs) | **Adapted**, split: enlistment half and the #1019 join log line in Part A (hand port: both board files drifted; render test adapted to the 1.4.x model); MapPerf menu probes and the `GameMenuVM.OnFrameTick` PatchShield exclusion in Part C (hand port onto WP117's MapPerf; `[ClientOnly]` and client-assembly scan dropped, D7). RCA `rca-enlistment-board-render-and-probes-2026-10-26.md`, the three lessons hunks and `gamemodel-bases.md` (a regeneration artifact) not taken (WP142, bookkeeping) |
| `ea7cce7bc` | feat(ui): v2.0.35 - screen open timer and Screen Performance switches | 1; 8 (`FeatureModules.cs`, `BattleLoadDiagnosticsSettings.cs`, `CoopSettingsRelevance.cs`, `CampaignHotPathSettingsProvidersTests.cs`, `SettingsFingerprintTests.cs`, 3 docs) | **Transplanted** in Part B (D6 squash at `b747cf398`); registered through the module (D2) |
| `11aeb8aee` | feat(ui): v2.0.35 - clan screen builds its party list once | 1; 5 | **Transplanted** (net clan design is `c32662872`'s; the `GauntletClanScreen_OnActivate` patch it added is gone at the tip) |
| `33c3bdf1a` | feat(ui): v2.0.35 - inventory and trade screens skip wasted work | 1; 3 | **Transplanted** (only the post-`22695bd01` shape: name-sort skip and timer) |
| `3f8168228` | feat(ui): v2.0.35 - party screen reuses troop portraits and item counts | 1; 4 | **Transplanted** (only the post-`579bb93f0` shape: portrait cache) |
| `3db115423` | feat(ui): v2.0.35 - quest log builds finished quests when selected | 1; 6 | **Transplanted** (adapter stays in `Main/Adapters`; reflection rows added) |
| `e77ef9654` | test(ui): v2.0.35 - module wiring tests cover all four screens | 1; 3 (two files it edits do not exist at the tip) | **Skipped**: superseded by `c32662872`, which deletes or rewrites every file it touched; the tip's `ShadowfaxScreensModuleTests.cs` carries what survives |
| `c32662872` | fix(ui): v2.0.35 - clan fast open swaps one call, timer on the VM | 1; 15 | **Transplanted** |
| `22695bd01` | fix(ui): v2.0.35 - inventory fast open keeps the name sort skip only | 1; 12 | **Transplanted** |
| `579bb93f0` | fix(ui): v2.0.35 - party fast open keeps the portrait cache only | 1; 15 | **Transplanted** |
| `642f92e39` | fix(ui): v2.0.35 - quest log sort stops boxing, rechecks its hooks | 1; 15 | **Transplanted** |
| `402280dc2` | docs(ui): v2.0.35 - Shadowfax Screens doc, feature map and patch targets | 1; 3 (`shadowfax-screens.md`, `feature-map.md`, `patch-targets.md`) | **Adapted** in Part B: screen-speed sections of the feature doc (instant-transition parts dropped, D6), `patch-targets.md` regenerated on 1.4.x. The `feature-map.md` row is deferred to WP142 (per-port procedure step 2) |
| `b747cf398` | fix(ui): v2.0.35 - Shadowfax Screens convergence fixes and RCA | 1; 15 | **Transplanted** (code fixes are in the end state). Its two lessons, the `agent-teams.md` checklist line and `rca-shadowfax-screens-speed-2026-10-19.md` are not taken (WP142/WP143 bookkeeping) |

Related changes outside this pack, recorded so nobody re-audits them:

| Change | Why not here |
|---|---|
| `f48ef74dc` feat(ui): v2.0.35 - Shadowfax Screens: screens open without slides | D6 (a UX change with no setting). Its `PrefabExtensionBindingTests` hunk belongs to WP140's `prefab-binding-gate` group |
| merge `963218f7f` (Shadowfax into trunk) | only its four category numbers are taken; `[ClientOnly]`, `Client/Adapters/QuestJournalAdapter.cs`, `NullQuestJournalAdapter`, the moved clan helper and the dedicated-server branch are 1.5.x co-op split (D7) |
| `ddb496d28` docs(api): v2.0.35 - regenerate the patch target and model snapshots | SKIP-BOOKKEEPING in the classification; 1.4.x regenerates its own snapshot in Steps 10 and 14 |
| `a15563a3c`, `d021426b0`, `6d7e67f3f` (co-op moves that also edited MapPerf) | D7; Part C applies `943ab74c0` without them |

## Test plan

- Part A (+7): `ServiceStatusServiceTests` +2 (`RefreshIfChanged_SameModel_ButTheBoardIsInAnOldLanguage_WritesAgain`,
  `TheBoardWriter_RendersTheBoardOncePerPush_AndRecordsTheLanguageItRenderedIn`);
  `ServiceStatusTextWriterRenderTests` 3 (line breaks without `NEWLINE`, stored string re-renders
  unchanged with every optional line, `IsStale` before and after a write);
  `InsideSettlementJoinDiagnosticTests` 2 (line placement, not gated).
- Part B: 19 files, 215 methods, 222 results with data rows: module wiring, settings defaults, timer,
  each service's decisions, real-Harmony patch shapes on dummy classes, and binding tests against the
  installed v1.4.8 IL (the clan three-build chain and single swap, the two inventory sort sites and
  their re-sort and tie-break, the three `CharacterCode` consumers, the quest log targets and enum
  values), plus JIT-acceptance tests that apply each transpiler to the real method and unpatch it.
  Shared: 2 `ReflectionSiteBindingTests` rows, 2 `CoopVetoClassificationTests` registry entries, the
  `SettingsFingerprintTests` census, and conditionally 2 `CampaignHotPathSettingsProvidersTests` rows.
- Part C (+11): hooks 2, installer 3, lines 2, targets 1, binding 3 (plus edited assertions).
- Structural pattern for later screens: `TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensModuleTests.cs`.
- Not unit-testable (commit trailers say so): the screens in a running campaign, the A/B timings, the
  board's look in game, the raid-from-inside menu, the menu probes' numbers.

## Done criteria

Run in the worktree after the last commit (and again after any review follow-up). ALL must hold:

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` reports `Failed: 0`
      and `Total` = T0 + 7 + the Step 11 delta (+ 11 with Part C).
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (two or three subjects, plus any review follow-up).
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -cE '^(CHANGELOG\.md|Main/IoC\.cs|Main/SubModule\.cs|Main/TAOM\.csproj|\.claude/|docs/reviews/|docs/reference/feature-map\.md|docs/INDEX\.md|plans/README\.md)'`
      prints `0`.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -cE 'InstantTransition|ShadowfaxScreensPrefab|PrefabExtensionBindingTests|GUI/PreFabs|^Client/'`
      prints `0`.
- [ ] `ls Main/Features/ShadowfaxScreens/*/Hooks/*.cs | wc -l` prints `17`;
      `grep -c 'new Features.ShadowfaxScreens.ShadowfaxScreensModule()' Main/Composition/FeatureModules.cs` prints `1`.
- [ ] `grep -rn 'PatchXXX' Main TAOM.Tests docs/features docs/reference` prints nothing, and
      `grep -rln 'ClientOnly\|NullQuestJournalAdapter\|IDedicatedServerProvider' Main/Features/ShadowfaxScreens Main/Adapters/QuestJournalAdapter.cs TAOM.Tests/Features/ShadowfaxScreens docs/features/shadowfax-screens.md`
      prints nothing (with Part C, also `grep -c 'ClientOnly' Main/Features/MapPerf/Hooks/Patch101_MapFrameProfilerMenu.cs`
      prints `0`).
- [ ] `grep -c 'text.ToString()' Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs` prints `1`
      and `grep -c '!_writer.IsStale' Main/Features/Enlistment/ServiceStatusService.cs` prints `1`.
- [ ] `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)`:
      `dead_links` and `orphan_features` equal Step 0.6's values; `ai_dashes: 0`.
- [ ] `git status --porcelain` prints nothing.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.2 fails: `Main/Composition`, `TickProfilerTranspiler.cs` (with `CallSwap`), `IStampClock`,
  `LoadTimeStampLines.ToMs`, an `IStampClock` registration, `FakeStampClock`, `RecordingLogger` or
  `RepoPaths.ReadSource` is missing (WP104, WP105, WP109 or WP118 not merged), or
  `Main/Features/ShadowfaxScreens` or a `Patch127_` to `Patch130_` category already exists.
- Step 0.3: `ServiceStatusTextWriter.cs` or `ServiceStatusService.cs` no longer match the excerpts
  (for example WP128 or WP132 merged early), or the `EncounterAdapter.cs` anchors are missing.
- A RED step fails for a reason other than the one stated, or a GREEN step leaves a failure other
  than the ones named.
- The board render test fails, or the stored board starts with `Error at id:` (Step 2).
- Any Shadowfax binding or JIT-acceptance test fails, or reports inconclusive on this laptop: the
  v1.4.8 IL differs from what a rewrite expects. Never change a rewrite, a cache key or a binding test
  to make it pass.
- `CoopVetoClassificationTests` names a Shadowfax bool prefix other than the two registered, or
  `HarmonyPatchBindingTests` fails for a Shadowfax or Part C class.
- The two settings-count docs quote different totals before Step 8.
- The Step 7.3 identity check shows a file other than the 14 named.
- `PatchCategoryNumberTests` (if present) fails for any reason other than a missing registry heading
  for Patch127 to Patch130 (Part B) or the missing `NoRegistryHeading` entry (Part C).
- The work seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, a ModuleData file, a
  localization file or any file outside Scope; or a module would need `OwnsSaveData => true` (D2).
- A hook denies a commit (report its message verbatim; never bypass it).
- The full suite at any part boundary shows a failure outside this WP's tests.
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer)

Executors cannot run the game. On the merged branch, in a build that ships TAOM and TAOM.Dependencies
together (Part C changes Dependencies). The TAOM log is `Logs\taom_debug_<timestamp>.log` under the
game's working directory (`Main/Core/Logging/FileLogger.cs`).

1. **Campaign start lines.** Load a save. Expected once each:
   `[Shadowfax] Clan fast open installed: 1 list build site swapped in ClanPartiesVM.RefreshValues`,
   `[ShadowfaxInventory] name sort: swapped 2 of 2 call sites`,
   `[QuestLog] fast open installed; finished quests build when selected (fast open on).`
   A `not installed`, `unavailable` or `left vanilla` line means that screen stays vanilla: report it.
2. **Clan.** Switch on (default): Parties, Caravans and Garrisons headers show their counts, refuge
   rows appear once; disband a party or change a leader and the lists update; rename the clan and
   lists and headers update; close and reopen three times, no stale rows. With "Log Screen Open
   Times" on: `[ScreenOpen] Clan open: ... (fast open on; list builds: 1 run, 2 skipped, ...)`; with
   "Clan Fast Open" off: `3 run, 0 skipped`.
3. **Inventory and trade.** Open the largest merchant: sort by each column and reopen (the saved sort
   holds); trade an item (order holds); open a loot screen and a stash (rows sorted); switch hero with
   the arrows and press Reset (rows stay sorted); compare with the switch off.
4. **Party.** A mixed army with prisoners: every row shows its own portrait (two troops of one type on
   both sides, a hero, a prisoner); expand an upgrade row and check the upgrade portraits; Transfer
   All and Reset; Talk, return and reopen; repeat once with the switch off.
5. **Quest log.** Finished quests list; select one and its stages appear; sort by date started and by
   last updated with the switch on and off (same order); a finished quest with an unexamined log
   shows the updated dot before it is built; turn the switch off while the log is open and select a
   deferred quest (it still builds); the open line reads `fast open on;` and `fast open off;`.
6. **A/B timing (decides whether each switch stays).** "Log Screen Open Times" on; per screen, six
   opens with its switch off and six on, on a large save; drop the first open of each arm; compare the
   medians of the `[ScreenOpen] <screen> open` lines. Keep a screen's change only if the median drops
   by at least 16 ms and by more than the off arm's spread; otherwise remove it with the removal
   checklist in `docs/features/shadowfax-screens.md` (that removal is a later change, not this WP).
7. **Enlistment board.** Enlist, open the service wait menu: the board shows separate lines (also on
   a campaign started in this session); it updates when the column stops or the day changes. Switch
   the game language in Options while serving: the board follows within about 2 s.
8. **Raid from inside (#1019).** While serving, have a raid reach the village the column rests in
   (once in a village of the soldier's faction, once in his own clan's village), with "Enable
   Enlistment Diagnostics" (MCM, Enlistment/Diagnostics) on for the surrounding trace. Report the
   `[EnlistDiag] seeding the join from inside ...` line (it is written whatever that switch says),
   which menu appeared, and what its options did.
9. **Menu probes (Part C).** MCM "Enable Map Profiler" on, "Tick Profiler Top Behaviours" 20 (both are
   WP117 settings), restart.
   Expected at the first campaign start: `[MapProfiler] menu probes: 3/3 patched` before the install
   line. Then 60 s each on the service wait menu at play speed and at Fast Forward Multiplier 4, 32 and
   128, and the same four runs on a vanilla town wait menu while not enlisted. Send every
   `[MapProfile]` line per run, the `[MapProfileSummary]` line, the multiplier and the build.

## Maintenance notes

- **WP128 and WP132 (enlistment fixes, wave 4)** run after this WP. WP128 edits
  `ServiceStatusTextWriter.cs` and `ServiceStatusService.cs`; WP132 edits `ServiceStatusService.cs`
  only (its board pay step, B2) and leaves the writer alone. They must keep the flat render
  (`text.ToString()`, the `NEWLINE` line, `_renderedLanguage`, `IsStale`) and the
  `&& !_writer.IsStale` check when they re-apply their hunks. A 1.5.x `WriteNotServing`, if ported
  later, stores an unflattened `TextObject` on purpose (see `943ab74c0`'s render test).
- **WP133 ports #993** (Patch23 rebuilds the code in the clan colours instead of writing `Color1` and
  `Color2`). The party cache re-check belongs to WP133 Part B, in its step after the Patch23 retarget
  (Step B3), not to this WP: on 1.5.x the merge comment on
  `PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch` was rewritten for the recoloured code
  (`git show 963218f7f -- Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs`);
  that step takes the comment and confirms `PartyFastOpenBindingTests` still passes.
- **WP123 (same wave)**: Step 6 item 6 reads this branch's base. If it was skipped (the hand-off says so),
  the integrator applies item 6 after both WPs merge, as one commit, and the Filtered run of
  `CampaignHotPathSettingsProvidersTests` must pass. The integrator re-runs `SettingsFingerprintTests` and the two count docs on every merge
  of a WP that adds an MCM setting.
- **Kept on purpose**: the clan swap helper stays in `ClanFastOpenCalls` (the pre-merge layout; 1.5.x
  moved it into the patch class only for the co-op client split); `QuestJournalAdapter` stays in
  `Main/Adapters`; there is no dedicated-server branch in `OnPhase` (co-op is not supported on this
  line, D7). PatchShield keeps shielding all 17 Shadowfax targets (none is on an exclusion list; a
  stripped quest patch turns deferral off at the next open by design).
- **WP142/WP143** sync the bookkeeping this WP left behind: the `feature-map.md` row for Shadowfax
  Screens, the two `b747cf398` lessons (`build-tooling-workflow.md`, `harmony-il.md`), the
  `agent-teams.md` parallel-builder checklist line, the `943ab74c0` lessons, and whether the two 1.5.x
  RCAs come along as review records.
- **Measurement first**: the 1.5.x feature doc's rule (keep a screen's change only on a 16 ms median
  saving) applies here too; the removal checklists in the feature doc and the registry sections say
  exactly what to delete per screen.
- **What a reviewer should probe**: (1) the four category constants and every `[HarmonyPatchCategory]`
  agree with the module's declarations (the module test checks it); (2) no file in the transplant
  differs from `b747cf398` beyond the 14 adapted ones; (3) the clan swap leaves direct callers
  (`RefreshCategoryValues`, `GauntletClanScreen.OnActivate`) building; (4) the party cache key
  (character, troop type, side) covers every input `GetCharacterCode` reads on v1.4.8, including the
  instance `Side` read at `PartyCharacterVM.cs:1485`, which equals the side argument for both callers;
  (5) the board's per-frame cost is one flat variable and its language follows within one status
  tick; (6) Part C's exclusion changes nothing for a player with the profiler off and that no other
  TAOM patch targets `GameMenuVM.OnFrameTick`.
- **Deferred**: an in-repo A/B harness for the timer lines (a log parser); no `tools/` script on the
  1.5.x line reads `[ScreenOpen]` lines either.
