# TaleWorlds Reflection-Site Catalogue

**Purpose.** TAOM reaches into private/internal TaleWorlds members by *string name* in many places. The C# compiler cannot verify those — a rename/move/removal in a Bannerlord update makes the lookup return `null`, and the reflecting code logs-and-survives, so the feature silently degrades with **no crash to investigate**. This file is the authoritative inventory of every reflection touchpoint, and the data source for the offline binding gate `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`.

It also exists so that an agent working on TAOM does not need the external decompile dump (`E:\Decompiled_Bannerlord\`) just to answer "what private member does feature X reach into, and is it still there in v1.4.8?"

**How the gate uses this.** Each row in [Category B](#category-b--auxiliary-static-engine-reflection-gated) is a `[DataRow]` in `ReflectionSiteBindingTests`. The test resolves the type (full name, then simple-name fallback) and asserts the member exists on the installed engine. Run it with:

```
dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "FullyQualifiedName~ReflectionSiteBindingTests"
```

**Maintenance.** When you add a reflection site against an engine member, add a row to Category B *and* a `[DataRow]` to the test. When you change a site, update both. Label the line that spells the member (its string literal, its `nameof`, or the name filter inside the lookup), not the first line of a multi-line call and not a declaration; `EveryLineLabel_PointsAtALineThatNamesItsMember` checks this with comments blanked, and `EveryDataRow_HasAMatchingRowInTheCatalogue` checks that every `[DataRow]` has its row here. When an engine update removes a member, the gate goes red here before the silent breakage ships.

---

## Category A — Harmony patch targets (gated elsewhere, not catalogued here)

Every `[HarmonyPatch(...)]` target — including patches whose target is resolved by a `TargetMethod()` / `TargetMethods()` body (e.g. SettlementGuards' manual patches, the `MapConversationTableau` / `CultureStageView` `TypeByName` lookups) — is **auto-discovered and resolved** by `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`. That suite enumerates all 110 `[HarmonyPatch]` / `TargetMethod`-bearing types in `TAOM.dll` and resolves each target exactly as Harmony does at `PatchAll` time. No manual catalogue is needed for them; do not duplicate them below.

The one hand-attached exception: `CrashReport/Hooks/Native2ManagedTargets.cs` names the `ManagedCallbacks.*CallbacksGenerated` shims in `Native2ManagedTargets.All` by string (assembly, type and method) for `Native2ManagedPatcher` to `harmony.Patch`. They are engine members an update can rename, and they are gated by `TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs` (`BindingVerification`), not by the suite above or by a Category B `[DataRow]`.

> First run of that gate (2026-05-28) caught a real defect: `HeroViewModel_FillFrom_Patch` was name-only on an overloaded method (`HeroViewModel` inherits two more `FillFrom` overloads from `CharacterViewModel`), so Harmony's `AccessTools.Method` threw `AmbiguousMatchException` at patch time — the postfix never applied in v1.4.5. Fixed by pinning the argument types.

A third-party reflection site with its own gate: `Adapters/ButterLibDistanceMatrixAdapter.cs` (#740) finds ButterLib's internal `Bannerlord.ButterLib.Implementation.DistanceMatrix.DistanceMatrixSubSystem` by full name and reads its static `Instance`, then calls `IsEnabled` and `Disable()` on it. The type lives in a version-specific ButterLib DLL that ReflectionSiteBindingTests does not load, so `TAOM.Tests/Features/ButterLibDistanceMatrix/ButterLibDistanceMatrixBindingTests.cs` (`BindingVerification`) checks it against the newest implementation DLL tracked in `Dependencies/_Module/bin/`.

---

## Category B — Auxiliary static-engine reflection (GATED)

Reflection against engine members performed *outside* a patch's target resolution: private state read from patch bodies, adapters, and services. The target type is known statically (`typeof(EngineType)` or a literal `TypeByName("...")`), so the lookup is verifiable offline. **Each row below is a test `[DataRow]`.**

| Engine type | Member | Kind | Source site | What it drives |
|---|---|---|---|---|
| `TaleWorlds.CampaignSystem.CharacterObject` | `_originCharacter` | field | `HeroCharacterStateAdapter.cs:13` | StaleCharacterRepair: a hollow parent takes its registered XML twin as origin |
| `…ViewModelCollection.Inventory.SPInventoryVM` | `_currentCharacter` | field | `InventoryScreenAdapter.cs:29` | EquipPresets active hero |
| `…ViewModelCollection.Inventory.SPInventoryVM` | `_inventoryLogic` | field | `InventoryScreenAdapter.cs:32` | EquipPresets transfer commands |
| `…ViewModelCollection.Inventory.SPInventoryVM` | `UpdateRightCharacter` | method | `InventoryVMAdapter.cs:29` | QuickActions and EquipPresets paper doll and stats after a batch transfer |
| `…ViewModelCollection.Inventory.SPInventoryVM` | `RefreshInformationValues` | method | `InventoryVMAdapter.cs:30` | QuickActions and EquipPresets capacity and Done state after a batch transfer |
| `…GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler` | `_isActive` | field | `OOBOverlayService.cs:57` | CompanionTactics OOB overlay attach |
| `…MissionGauntletOrderOfBattleUIHandler` | `_dataSource` | field | `OOBOverlayService.cs:58` | CompanionTactics OOB overlay data |
| `…ViewModelCollection.Party.PartyCharacterVM` | `TypeIconData` | property | `RoleTooltipDecorator.cs:40` | Companion role tooltip |
| `…ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM` | `_cachedTooltipProperties` | field | `RoleTooltipDecorator.cs:41` | Companion role tooltip cache bust |
| `…OrderOfBattleHeroItemVM` | `GetCaptainTooltip` | method | `ManualPatchApplicator.cs:48` (manual patch) | Captain tooltip role hint |
| `…ViewModelCollection.FaceGenerator.FaceGenVM` | `_selectedRace` | field | `FaceGenRaceSelectorRebuilder.cs:208` | CC culture-restricted race dropdown |
| `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView` | `SpawnNonHumanNarrativeMenuCharacter` | method | `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:153` | Patch20 narrative mount guard: the `TargetMethod` of its prefix |
| `SandBox.GauntletUI.CharacterCreation.CharacterCreationNarrativeStageView` | `_characterScene` | field | `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:157` | Patch20 narrative mount guard: injected as `____characterScene` to find the spawn point |
| `…Core.ViewModelCollection.Selector.SelectorVM`1` | `_selectedIndex` | field | `FaceGenRaceSelectorRebuilder.cs:211` | Selector reset trick (no-op-setter bypass) |
| `…SelectorVM`1` | `_selectedItem` | field | `FaceGenRaceSelectorRebuilder.cs:212` | Selector reset trick |
| `…SelectorVM`1` | `_onChange` | field | `FaceGenRaceSelectorRebuilder.cs:213`, `CommanderSelectorRebuilder.cs:21` | Selector callback rewire |
| `…CustomBattle.CustomBattleSideVM` | `OnCultureSelection` | method | `CustomBattleSideVM_Constructor_Patch.cs:23` | CustomBattles faction injection |
| `…ViewModelCollection.ClanManagement.Categories.ClanPartiesVM` | `OnPartySelection` | method (private) | `RefugeClanScreenPatch.cs:41` | Refuge clan screen: a refuge row's selection handler is the VM's own. Missing: the method handle is null and the postfix adds no refuge rows |
| `TaleWorlds.MountAndBlade.Mission` | `RegisterBlow` | method | `CustomAttacksUtils.cs:60` | AdvancedCombat custom attacks |
| `TaleWorlds.MountAndBlade.Mission` | `_initialPlayerAgent` | field (private) | `ShaderPrecompilePlayerAgentGuard.cs:44` | ShaderPrecompilation deployment fallback, shader-precompile battles only: when no player agent set this field, the guard writes the first player-team agent into it, so the engine's deployment dereference of InitialPlayerAgent does not throw. Missing: the lookup returns null and the fallback cannot seed the field |
| `TaleWorlds.MountAndBlade.Mission` | `WaitTickCompletion` | method | `MissionTickProfilerInstaller.cs:31,54` | Patch97 tick profiler: the wait's open delegate and its call-site swap |
| `TaleWorlds.Engine.ScriptComponentBehavior` | `OnTick` | method (protected internal virtual) | `MissionAttributionInstaller.cs:35,66` | Patch97 attribution (plan 041): the script tick's open delegate and its call-site swap. Missing: one WARNING, per-component script attribution off, the script totals stay |
| `TaleWorlds.CampaignSystem.MbEvent`1` | `_nonSerializedListenerList` | field | `TickEventListenerWalker.cs:47` | Patch101 map profiler: the TickEvent listener walk. Missing: the walk stays unbound and listeners are not attributed (one warning) |
| `…MbEvent`1+EventHandlerRec`1` | `Next` | field | `TickEventListenerWalker.cs:51` | Patch101 map profiler: the TickEvent listener walk. Missing: the walk stays unbound and listeners are not attributed (one warning) |
| `…MbEvent`1+EventHandlerRec`1` | `<Action>k__BackingField` | field | `TickEventListenerWalker.cs:54` | Patch101 map profiler: the TickEvent listener walk. Missing: the walk stays unbound and listeners are not attributed (one warning) |
| `…MbEvent`1+EventHandlerRec`1` | `<Owner>k__BackingField` | field | `TickEventListenerWalker.cs:57` | Patch101 map profiler: the TickEvent listener walk. Missing: the walk stays unbound and listeners are not attributed (one warning) |
| `SandBox.GauntletUI.BannerEditor.BannerEditorView` | `RefreshShieldAndCharacter` | method | `BannerEditorView_OnTick_Patch.cs:22` | Banner paste refresh |
| `…Party.PartyScreenLogic+PartyCommand` | `TotalNumber` | member | `PartyScreenLogic_AddCommand_Patch.cs:72` | SpecialResources transactional spend |
| `…ViewModelCollection.Encyclopedia.Items.EncyclopediaUnitVM` | `_character` | field (private) | `EncyclopediaUnitBadgeMixin.cs:32` | SpecialResources encyclopedia troop badge (#590). The unit VM keeps the troop only here, so the badge reads its `StringId` once at construction; a null read hides the badge |
| `…Map.DistanceCache.NavigationCache`1` | `_settlementToSettlementDistanceWithLandRatio` | field | `NavigationCacheAdapter.cs:71` | Distance cache rebuild |
| `…NavigationCache`1` | `_fortificationNeighbors` | field | `NavigationCacheAdapter.cs:73` | Neighbor cache |
| `…NavigationCache`1` | `_navigationType` | property | `NavigationCacheAdapter.cs:76` | Nav type (property, not field, in v1.4.5) |
| `…NavigationCache`1` | `GetAllRegisteredSettlements` | method | `NavigationCacheAdapter.cs:79` | Settlement enumeration |
| `…NavigationCache`1` | `GetUpdatedSettlementsForNeighborDetection` | method | `NavigationCacheAdapter.cs:81` | Neighbor detection |
| `…NavigationCache`1` | `AddClosestEntrancePairBase` | method | `NavigationCacheAdapter.cs:83` | Entrance-pair build |
| `…NavigationCache`1` | `AddNeighbor` | method | `NavigationCacheAdapter.cs:85` | Neighbor build |
| `…NavigationCache`1` | `CheckBeingNeighbor` | method | `NavigationCacheAdapter.cs:374` | Neighbor predicate (3-arg overload) |
| `…NavigationCache`1` | `GetCacheElement` | method | `NavigationCacheAdapter.cs:388` | Cache element lookup |
| `…NavigationCache`1` | `GetRealDistanceAndLandRatioBetweenSettlements` | method | `NavigationCacheAdapter.cs:404` | Distance compute |
| `…NavigationCache`1` | `SetSettlementToSettlementDistanceWithLandRatio` | method | `NavigationCacheAdapter.cs:420` | Distance write |
| `…NavigationCache`1` | `GenerateClosestSettlementToFaceCache` | method | `NavigationCacheAdapter.cs:104` | Closest-settlement cache |
| `…NavigationCache`1` | `Serialize` | method | `NavigationCacheAdapter.cs:110` | Cache serialize |
| `…NavigationCache`1` | `Deserialize` | method | `NavigationCacheAdapter.cs:113` | Cache deserialize |
| `…Map.DistanceCache.NavigationCacheElement`1` | `Sort` | method (static) | `NavigationCacheAdapter.cs:101` | Element sort |
| `…Map.DistanceCache.SandBoxNavigationCache` | `GetSceneXmlCrcValues` | method | `NavigationCacheAdapter.cs:107` | Scene CRC validation |
| `SandBox.CampaignBehaviors.GuardsCampaignBehavior` | `PrepareGuardAgentDataFromGarrison` | method (static) | `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:32` | SettlementGuards config-pool guard build (backfilled 2026-07-14; predates the gate) |
| `…GuardsCampaignBehavior` | `_garrisonTroops` | field | `GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.cs:33` | Excluded-race guard scrub (#346) |
| `TaleWorlds.CampaignSystem.Campaign` | `PlayerDefaultFaction` | property (internal) | `PlayerIdentityAdapter.cs:35` | Player Switcher (#514). `Clan.PlayerClan` is a computed getter over it and `ChangePlayerCharacterAction` never updates it. Probed at construction; a failed probe disables the feature for the session |
| `…GauntletUI.BodyGenerator.BodyGeneratorView` | `_dressedEquipment` | field (private readonly) | `BodyGeneratorPreviewSink.cs:26` | Player Switcher (#514). Readonly, so the preview mutates its slots in place. Soft-fails to an undressed preview |
| `…ViewModelCollection.FaceGenerator.FaceGenVM` | `_faceGenerationParams` | field (private) | `BodyGeneratorPreviewSink.cs` | Player Switcher (#514). The preview clamps this struct to the target race via the engine's own `SetRaceGenderAndAdjustParams`, which `SetBodyProperties` omits. Without it a lord whose body key carries a voice index his new race lacks aborts `Refresh` and the race never commits |
| `…ViewModelCollection.FaceGenerator.FaceGenVM` | `_characterRefreshEnabled` | field (private) | `BodyGeneratorPreviewSink.cs` | Player Switcher (#514). `Refresh` early-returns unless set, and the aborted call leaves it false, so the repair must re-arm it |
| `SandBox.Missions.MissionLogics.Hideout.HideoutAmbushMissionController` | `_allEnemyTroops` | field (private, `List<IAgentOriginBase>`) | `Patch86_HideoutAmbushBossFight.cs` | Hideout boss fight (#564). The sneak-in route's unspawned enemy list; the prefix trims it to the N highest levels. Injected as `____allEnemyTroops` |
| `…HideoutAmbushMissionController` | `_overriddenHideoutBossAgentOrigin` | field (private, `IAgentOriginBase`) | `Patch86_HideoutAmbushBossFight.cs` | Hideout boss fight (#564). Non-null when a boss spawns from his own origin; decides whether a zero bodyguard count must keep one troop for `SelectBossAgent` |
| `TaleWorlds.CampaignSystem.CampaignBehaviors.BanditSpawnCampaignBehavior` | `_hideouts` | field (private, `Dictionary<CultureObject, List<Hideout>>`) | `Patch125_HideoutClanGuard.cs` | Clan and hideout pairing guard (#1063). The prefix on `GetInfestedHideoutCount` checks the clan's culture has an entry before vanilla's unguarded indexer. Injected as `____hideouts` |
| `…HideoutAmbushMissionController` | `_allEnemyTroopTypesCache` | field (private, `List<IAgentOriginBase>`) | `Patch86_HideoutAmbushBossFight.cs` | Hideout boss fight (#564). Vanilla pads from it (`GetNewRandomEnemyTroop` derefs `GetRandomElement` on it), so the prefix only asks for padding when it is non-empty |
| `TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.KingdomDecisionsVM` | `_examinedDecisionsSinceInit` | field (private, `List<KingdomDecision>`) | `KingdomVoteDeadlockBinding.cs` | Kingdom vote deadlock (#547). Seams A and C record a withdrawn ballot here so `OnFrameTick` stops offering it |
| `…KingdomDecisionsVM` | `_shouldCheckForDecision` | property (private) | `KingdomVoteDeadlockBinding.cs` | Kingdom vote deadlock (#547). Re-armed after a withdrawal; vanilla's own bail-out leaves it false for the session |
| `…Decisions.ItemTypes.DecisionItemBaseVM` | `_decision` | field (protected readonly) | `KingdomVoteDeadlockBinding.cs` | Kingdom vote deadlock (#547, #550). Names the ballot a window was built for; seam D matches it against the `RefreshWith` argument |
| `…DecisionItemBaseVM` | `_onDecisionOver` | field (private readonly, `Action`) | `KingdomVoteDeadlockBinding.cs` | Kingdom vote deadlock (#547). Seam B's close path on a CANCELLED election, where `ExecuteDone` would NRE |
| `…DecisionItemBaseVM` | `ExecuteDone` | method (protected) | `KingdomVoteDeadlockBinding.cs` | Kingdom vote deadlock (#550). Seam D runs vanilla's own close on a window whose election concluded inside the view model's constructor; safe there because `_chosenOutcome` is set |
| `TaleWorlds.MountAndBlade.MBSubModuleBase` | `OnBeforeMissionBehaviorInitialize` | method (public virtual) | `MissionStartGuardSwaps.cs:23` | MissionStartGuard (Patch103). One of six start calls the transpiler swaps for a same-named helper, found by `AccessTools.Method`. Missing: the fit check rejects the null target and `Mission.AfterStart` stays vanilla (`OFF: wrapped 0 of 6`) |
| `TaleWorlds.MountAndBlade.MissionBehavior` | `OnBehaviorInitialize` | method (public virtual) | `MissionStartGuardSwaps.cs:24` | MissionStartGuard. As above |
| `TaleWorlds.MountAndBlade.MBSubModuleBase` | `OnMissionBehaviorInitialize` | method (public virtual) | `MissionStartGuardSwaps.cs:25` | MissionStartGuard. As above |
| `TaleWorlds.MountAndBlade.MissionBehavior` | `EarlyStart` | method (public virtual) | `MissionStartGuardSwaps.cs:26` | MissionStartGuard. As above |
| `TaleWorlds.MountAndBlade.MissionBehavior` | `AfterStart` | method (public virtual) | `MissionStartGuardSwaps.cs:27` | MissionStartGuard. As above |
| `TaleWorlds.MountAndBlade.MissionObject` | `AfterMissionStart` | method (public virtual) | `MissionStartGuardSwaps.cs:28` | MissionStartGuard. As above |
| `SandBox.ViewModelCollection.Nameplate.SettlementNameplatesVM` | `_mapCamera` | field (private) | `NameplateCullAdapter.cs:49` | NameplateCull (Patch104). Read through `AccessTools.FieldRefAccess` to get the map camera the vanilla `Update` reads. Missing: `Initialize` returns a reason, the cull stays off and the vanilla nameplate update runs |
| `SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM` | `_bindIsVisibleOnMap` | field (private) | `NameplateCullAdapter.cs:50` | NameplateCull. The bound visibility a plate must agree with before it may be skipped. Missing: as `_mapCamera` |
| `SandBox.ViewModelCollection.Nameplate.SettlementNameplateVM` | `_worldPos` | field (private) | `NameplateCullAdapter.cs:51` | NameplateCull. The plate's world position, for the distance half of the visibility test. Missing: as `_mapCamera` |
| `` TaleWorlds.CampaignSystem.MbEvent`1 `` | `_nonSerializedListenerList` | field (private) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1 `` | `Next` | field (public) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1 `` | `Action` | property (internal) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1 `` | `set_Action` | method (private setter) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`1+EventHandlerRec`1 `` | `Owner` | property (internal) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`2 `` | `_nonSerializedListenerList` | field (private) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2 `` | `Next` | field (public) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2 `` | `Action` | property (internal) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2 `` | `set_Action` | method (private setter) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `` TaleWorlds.CampaignSystem.MbEvent`2+EventHandlerRec`2 `` | `Owner` | property (internal) | `CampaignListenerAdapter.cs` | LoadTimeStamps per-handler timing; missing: dispatch totals only |
| `TaleWorlds.ObjectSystem.MBObjectManager` | `CreateDocumentFromXmlFile` | method (private static) | `XmlMergeEngineAdapter.cs:23` | XmlMerge (plan 042). The fast path loads and validates every module XML file through the engine's own loader, bound once to a delegate. Missing: the fast path turns itself off at start (`[XmlMerge] fast path off`) and every merge runs the engine's own code |
| `TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestItemVM` | `PopulateQuestLog` | method (private) | `QuestJournalAdapter.cs:30` | Quest Log Fast Open (#854). Cached once at `Bind` and replayed per journal entry to build a deferred finished quest when it is selected. Missing: `Bind` returns a reason, the install line is a warning, and vanilla builds every quest at open |
| `TaleWorlds.CampaignSystem.ViewModelCollection.Quests.QuestsVM` | `SetSelectedItem` | method (private) | `QuestJournalAdapter.cs:39` | Quest Log Fast Open (#854). Looked up by name for the attached-patch check (`Bind`, and again on every open). Missing: the `SetSelectedItem` patch reads as not attached, `Bind` returns a reason and no open defers |
| `TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior` | `CheckRecruiting` | method (private) | `CastleAiRecruitDispatcher.cs:34` | CastleRecruitment (#886). Bound once per campaign to a closed delegate; an AI lord in a castle recruits hourly through vanilla's own logic (caravans never recruit in a castle). Missing: one warning per session launch and the hourly in-castle top-up stops; lords still recruit on entry through vanilla's OnBeforeSettlementEntered |
| `TaleWorlds.ModuleManager.ModuleHelper` | `GetActiveModules` | method (public static) | `IncompatibleModDetector.cs:226` | TAOM.Dependencies reads the launcher's active-module list by name (`CoopPresence` uses the same read). Missing: `IncompatibleModDetector` falls back to a directory scan, but `CoopPresence` has no fallback and reads co-op as absent |
| `TaleWorlds.Library.ApplicationVersion` | `FromParametersFile` | method (public static) | `VersionProbe.cs:78` | TAOM.Dependencies detects the game version through it. Missing: the version stays undetected and is logged as such; nothing crashes |

Status (2026-11-05): plan 137 adds two TAOM.Dependencies rows (`ModuleHelper.GetActiveModules`, `ApplicationVersion.FromParametersFile`); both resolve against installed v1.4.8 (`ModuleHelper.cs:196`, `ApplicationVersion.cs:40`, taom-src).
Status (2026-11-05): the `PathReuseCache._store` row (added in `41258657`) is removed together with the unwired path-reuse scaffold it covered (1.5.x plan 025, ported). It was never engine reflection: no installed engine assembly defines a `PathReuseCache` type, and the row passed only through the simple-name fallback, which found TAOM's own class. The scaffold is in `6a80bac6`; if it is ever restored, its `_store` self-reflection belongs in Category D, not in this gate.
Status (2026-11-04): NameplateCull (Patch104) adds three private field rows; they resolve against installed v1.4.8 (ReflectionSiteBindingTests 58/58).
Status (2026-11-04): MissionStartGuard (Patch103) adds six method rows; they resolve against installed v1.4.8 (ReflectionSiteBindingTests 51/51).
Status (2026-11-04, 1.4.x): XmlMerge adds one row (MBObjectManager.CreateDocumentFromXmlFile); it resolves against installed v1.4.8.
Status (2026-11-04, 1.4.x): LoadTimeStamps adds ten rows (the MbEvent listener records); all resolve against installed v1.4.8 (ReflectionSiteBindingTests).
Status (2026-09-15): Patch80 seam D (#550) adds `DecisionItemBaseVM.ExecuteDone`, and the four members `KingdomVoteDeadlockBinding` has cached since #547 are catalogued at the same time; they had only ever been pinned by `Patch80KingdomVoteDeadlockBindingTests`. All five resolve against installed v1.5.3.
Status (2026-09-11): Hideout boss fight (#564) adds three `HideoutAmbushMissionController` field rows, all three injected by `Patch86_HideoutAmbushBossFight` and pinned by the `ReflectionSiteBindingTests` rows plus a field-TYPE assertion in `Patch86HideoutBossFightBindingTests`; **all resolve against installed v1.4.8** (`BindingVerification` filtered run green, see the CHANGELOG entry).
Status (2026-05-28): **all 32 resolve against installed v1.4.5.**
Status (2026-07-14): SettlementGuards rows added (`PrepareGuardAgentDataFromGarrison` backfill + `_garrisonTroops`, #346) — **all 35 gate rows resolve against installed v1.4.7.**
Status (2026-08-28): the Player Switcher race-repair adds two more `FaceGenVM` rows, both pinned by `PlayerSwitcherBindingTests.TheRaceRepairSeam_StillResolves` and both confirmed against installed v1.4.8. They exist because `FaceGenVM.SetBodyProperties` never applies the post-decode clamp that `BodyGenerator.InitBodyGenerator` does.
Status (2026-08-27): Player Switcher (#514) adds two rows — `Campaign.PlayerDefaultFaction` and `BodyGeneratorView._dressedEquipment` — **both resolve against installed v1.4.8**, asserted by `PlayerSwitcherBindingTests` and the `ReflectionSiteBindingTests` row.
Status (2026-08-10): v1.4.8 engine bump — **every gate row still resolves; no row added or removed.** `BindingVerification` ran 106/106 green against the installed v1.4.8 DLLs. 1.4.8 rewrote `NavigationCache` for speed (`GetClosestSettlementToPosition` gained an optional `useEarlyOut` parameter), but no member this table names changed shape.

---

## Category C — Runtime-dynamic reflection (NOT offline-verifiable)

These resolve the target *type* from a live instance (`instance.GetType()…`), so they cannot be checked without a running game. They are verified by the in-game smoke test, not this gate — see [`docs/migration/s6-runtime-punchlist.md`](../../migration/s6-runtime-punchlist.md). Listed here for completeness so a future audit knows they exist and why they are excluded.

| Source site(s) | Pattern | Why dynamic |
|---|---|---|
| `FactionMap/CultureSettingService.cs:24,28,32,38,55,61,65` | `activeState.GetType()`, `content.GetType()`, `cultureVM.GetType()` etc. | CC manager/content/culture-VM types resolved from the live `GameStateManager` instance |
| `FactionMap/Hooks/CultureStageViewCreatedHook.cs`, `CultureStageViewTickHook.cs`, `CultureStageProgressionService.cs:30,38` | `viewInstance.GetType().GetField(...)` | CC culture-stage view type resolved from the live view instance |
| `CharacterCreation/Hooks/CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:38`, `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:198` | `__instance?.GetType().GetField("_characterCreationManager", …)` | field name fixed, but type comes from the patched instance |
| `CrashReport/Collectors/*` (`HarmonyCorrelationCollector`, `StackFrameSnapshotBuilder`, `McmSettingsCollector`), `CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs` | `frame.GetMethod()`, `settingsInstance.GetType().GetProperty(...)` | reflects over arbitrary stack frames / optional third-party (ButterLib, MCM) types that may be absent |
| `Dependencies/Foundation/IncompatibleModDetector.cs:222` | `moduleInfo.GetType().GetProperty("Id")` | `ModuleInfo.Id` is read off each element `ModuleHelper.GetActiveModules` returns (the method itself is a Category B row); the element type comes from the live list |

---

## Category D — TAOM-internal reflection (not engine drift)

Reflection whose target is a TAOM-owned type or a dynamic member name. Not affected by Bannerlord updates; intentionally not gated.

| Source site | Target | Note |
|---|---|---|
| `Core/Infrastructure/Reflection/ReflectionService.cs` | caller-supplied `(Type, name)` keys | generic cached-reflection helper; targets are at the call sites (Category B/C above) |
| `CareerSystem/Mutations/MutationService.cs:105` | `typeof(AbilityTemplateData).GetProperty(propertyName)` | `AbilityTemplateData` is a TAOM type; `propertyName` is data-driven |
| `CrashReport/Hooks/Native2ManagedPatcher.cs` | `new HarmonyMethod(typeof(Native2ManagedBridge), nameof(Native2ManagedBridge.Finalizer))` | TAOM bridge type; `nameof` makes it compiler-verified. The engine shim names in `Native2ManagedTargets.cs` are engine reflection and are listed under Category A |
| `CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs:43` | `typeof(AgentVisualsData).GetMethod(nameof(AgentVisualsData.ActionSet))` | `nameof` → compiler-verified member; no string drift risk |

---

## Referenced by

- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (Category B is its data source)
- [`README.md`](./README.md) (snapshot overview)
- [`docs/migration/TRACKING.md`](../../migration/TRACKING.md) (S6 binding-verification)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/migration/s6-runtime-punchlist.md](../../migration/s6-runtime-punchlist.md)
- [docs/reference/taleworlds-api-snapshot/README.md](./README.md)

<!-- backlinks-end -->
