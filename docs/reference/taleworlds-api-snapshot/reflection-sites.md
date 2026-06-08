# TaleWorlds Reflection-Site Catalogue

**Purpose.** TAOM reaches into private/internal TaleWorlds members by *string name* in many places. The C# compiler cannot verify those — a rename/move/removal in a Bannerlord update makes the lookup return `null`, and the reflecting code logs-and-survives, so the feature silently degrades with **no crash to investigate**. This file is the authoritative inventory of every reflection touchpoint, and the data source for the offline binding gate `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`.

It also exists so that an agent working on TAOM does not need the external decompile dump (`E:\Decompiled_Bannerlord\`) just to answer "what private member does feature X reach into, and is it still there in v1.4.5?"

**How the gate uses this.** Each row in [Category B](#category-b--auxiliary-static-engine-reflection-gated) is a `[DataRow]` in `ReflectionSiteBindingTests`. The test resolves the type (full name, then simple-name fallback) and asserts the member exists on the installed engine. Run it with:

```
dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~ReflectionSiteBindingTests"
```

**Maintenance.** When you add a reflection site against an engine member, add a row to Category B *and* a `[DataRow]` to the test. When you change a site, update both. When an engine update removes a member, the gate goes red here before the silent breakage ships.

---

## Category A — Harmony patch targets (gated elsewhere, not catalogued here)

Every `[HarmonyPatch(...)]` target — including patches whose target is resolved by a `TargetMethod()` / `TargetMethods()` body (e.g. SettlementGuards' manual patches, the `MapConversationTableau` / `CultureStageView` `TypeByName` lookups) — is **auto-discovered and resolved** by `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`. That suite enumerates all 110 `[HarmonyPatch]` / `TargetMethod`-bearing types in `TAOM.dll` and resolves each target exactly as Harmony does at `PatchAll` time. No manual catalogue is needed for them; do not duplicate them below.

> First run of that gate (2026-05-28) caught a real defect: `HeroViewModel_FillFrom_Patch` was name-only on an overloaded method (`HeroViewModel` inherits two more `FillFrom` overloads from `CharacterViewModel`), so Harmony's `AccessTools.Method` threw `AmbiguousMatchException` at patch time — the postfix never applied in v1.4.5. Fixed by pinning the argument types.

---

## Category B — Auxiliary static-engine reflection (GATED)

Reflection against engine members performed *outside* a patch's target resolution: private state read from patch bodies, adapters, and services. The target type is known statically (`typeof(EngineType)` or a literal `TypeByName("...")`), so the lookup is verifiable offline. **Each row below is a test `[DataRow]`.**

| Engine type | Member | Kind | Source site | What it drives |
|---|---|---|---|---|
| `…ViewModelCollection.Inventory.SPInventoryVM` | `_currentCharacter` | field | `InventoryScreenAdapter.cs:29` | EquipPresets active hero |
| `…ViewModelCollection.Inventory.SPInventoryVM` | `_inventoryLogic` | field | `InventoryScreenAdapter.cs:32` | EquipPresets transfer commands |
| `…GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler` | `_isActive` | field | `OOBOverlayService.cs:57` | CompanionTactics OOB overlay attach |
| `…MissionGauntletOrderOfBattleUIHandler` | `_dataSource` | field | `OOBOverlayService.cs:58` | CompanionTactics OOB overlay data |
| `…ViewModelCollection.Party.PartyCharacterVM` | `TypeIconData` | property | `RoleTooltipDecorator.cs:40` | Companion role tooltip |
| `…ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM` | `_cachedTooltipProperties` | field | `RoleTooltipDecorator.cs:41` | Companion role tooltip cache bust |
| `…OrderOfBattleHeroItemVM` | `GetCaptainTooltip` | method | `SubModule.cs:503` (manual patch) | Captain tooltip role hint |
| `…ViewModelCollection.FaceGenerator.FaceGenVM` | `_selectedRace` | field | `FaceGenRaceSelectorRebuilder.cs:208` | CC culture-restricted race dropdown |
| `…Core.ViewModelCollection.Selector.SelectorVM`1` | `_selectedIndex` | field | `FaceGenRaceSelectorRebuilder.cs:211` | Selector reset trick (no-op-setter bypass) |
| `…SelectorVM`1` | `_selectedItem` | field | `FaceGenRaceSelectorRebuilder.cs:212` | Selector reset trick |
| `…SelectorVM`1` | `_onChange` | field | `FaceGenRaceSelectorRebuilder.cs:213`, `CommanderSelectorRebuilder.cs:21` | Selector callback rewire |
| `…CustomBattle.CustomBattleSideVM` | `OnCultureSelection` | method | `CustomBattleSideVM_Constructor_Patch.cs:23` | CustomBattles faction injection |
| `TaleWorlds.MountAndBlade.Mission` | `RegisterBlow` | method | `CustomAttacksUtils.cs:55` | AdvancedCombat custom attacks |
| `SandBox.GauntletUI.BannerEditor.BannerEditorView` | `RefreshShieldAndCharacter` | method | `BannerEditorView_OnTick_Patch.cs:21` | Banner paste refresh |
| `SandBox.Objects.Usables.MusicianGroup` | `_trackEvent` | field | `MusicianGroupSuppressionAdapter.cs:9` | Music tavern vanilla SoundEvent release |
| `…Party.PartyScreenLogic+PartyCommand` | `TotalNumber` | member | `PartyScreenLogic_AddCommand_Patch.cs:71` | SpecialResources transactional spend |
| `TaleWorlds.Engine.PathReuseCache` | `_store` | field | `PersistentPathCache.cs:149` | EditorCacheRebuild path-cache extract |
| `…Map.DistanceCache.NavigationCache`1` | `_settlementToSettlementDistanceWithLandRatio` | field | `NavigationCacheAdapter.cs:71` | Distance cache rebuild |
| `…NavigationCache`1` | `_fortificationNeighbors` | field | `NavigationCacheAdapter.cs:73` | Neighbor cache |
| `…NavigationCache`1` | `_navigationType` | property | `NavigationCacheAdapter.cs:76` | Nav type (property, not field, in v1.4.5) |
| `…NavigationCache`1` | `GetAllRegisteredSettlements` | method | `NavigationCacheAdapter.cs:79` | Settlement enumeration |
| `…NavigationCache`1` | `GetUpdatedSettlementsForNeighborDetection` | method | `NavigationCacheAdapter.cs:81` | Neighbor detection |
| `…NavigationCache`1` | `AddClosestEntrancePairBase` | method | `NavigationCacheAdapter.cs:83` | Entrance-pair build |
| `…NavigationCache`1` | `AddNeighbor` | method | `NavigationCacheAdapter.cs:85` | Neighbor build |
| `…NavigationCache`1` | `CheckBeingNeighbor` | method | `NavigationCacheAdapter.cs:88` | Neighbor predicate (3-arg overload) |
| `…NavigationCache`1` | `GetCacheElement` | method | `NavigationCacheAdapter.cs:91` | Cache element lookup |
| `…NavigationCache`1` | `GetRealDistanceAndLandRatioBetweenSettlements` | method | `NavigationCacheAdapter.cs:94` | Distance compute |
| `…NavigationCache`1` | `SetSettlementToSettlementDistanceWithLandRatio` | method | `NavigationCacheAdapter.cs:97` | Distance write |
| `…NavigationCache`1` | `GenerateClosestSettlementToFaceCache` | method | `NavigationCacheAdapter.cs:104` | Closest-settlement cache |
| `…NavigationCache`1` | `Serialize` | method | `NavigationCacheAdapter.cs:110` | Cache serialize |
| `…NavigationCache`1` | `Deserialize` | method | `NavigationCacheAdapter.cs:113` | Cache deserialize |
| `…Map.DistanceCache.NavigationCacheElement`1` | `Sort` | method (static) | `NavigationCacheAdapter.cs:101` | Element sort |
| `…Map.DistanceCache.SandBoxNavigationCache` | `GetSceneXmlCrcValues` | method | `NavigationCacheAdapter.cs:107` | Scene CRC validation |

Status (2026-06-06): **all 33 resolve against installed v1.4.5.**

---

## Category C — Runtime-dynamic reflection (NOT offline-verifiable)

These resolve the target *type* from a live instance (`instance.GetType()…`), so they cannot be checked without a running game. They are verified by the in-game smoke test, not this gate — see [`docs/migration/s6-runtime-punchlist.md`](../../migration/s6-runtime-punchlist.md). Listed here for completeness so a future audit knows they exist and why they are excluded.

| Source site(s) | Pattern | Why dynamic |
|---|---|---|
| `FactionMap/CultureSettingService.cs:24,28,32,38,55,61,65` | `activeState.GetType()`, `content.GetType()`, `cultureVM.GetType()` etc. | CC manager/content/culture-VM types resolved from the live `GameStateManager` instance |
| `FactionMap/Hooks/CultureStageViewCreatedHook.cs`, `CultureStageViewTickHook.cs`, `CultureStageProgressionService.cs:30,38` | `viewInstance.GetType().GetField(...)` | CC culture-stage view type resolved from the live view instance |
| `CharacterCreation/Hooks/CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs:38`, `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs:198` | `__instance?.GetType().GetField("_characterCreationManager", …)` | field name fixed, but type comes from the patched instance |
| `CrashReport/Collectors/*` (`HarmonyCorrelationCollector`, `StackFrameSnapshotBuilder`, `McmSettingsCollector`), `CrashReport/Adapters/ButterLibExceptionHandlerAdapter.cs` | `frame.GetMethod()`, `settingsInstance.GetType().GetProperty(...)` | reflects over arbitrary stack frames / optional third-party (ButterLib, MCM) types that may be absent |

---

## Category D — TAOM-internal reflection (not engine drift)

Reflection whose target is a TAOM-owned type or a dynamic member name. Not affected by Bannerlord updates; intentionally not gated.

| Source site | Target | Note |
|---|---|---|
| `Core/Infrastructure/Reflection/ReflectionService.cs` | caller-supplied `(Type, name)` keys | generic cached-reflection helper; targets are at the call sites (Category B/C above) |
| `CareerSystem/Mutations/MutationService.cs:105` | `typeof(AbilityTemplateData).GetProperty(propertyName)` | `AbilityTemplateData` is a TAOM type; `propertyName` is data-driven |
| `CrashReport/Hooks/Native2ManagedPatcher.cs:41,52` | `typeof(CrashReportPatchHelper)`, `typeof(Native2ManagedBridge)` | TAOM finalizer/bridge types |
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
