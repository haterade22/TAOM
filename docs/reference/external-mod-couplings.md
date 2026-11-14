# Other mods that patch or read TAOM by name

Reference list of the TAOM members that other mods reach by string: a Harmony patch target found by type and method name,
reflection, or a lookup through `TAOM.IoC`. Most of the mods are yotthani's (repo `yotthani/bannerlord`,
read at 6265f4a2); Kysaro's and Coodsz's have their own sections below, with their own sources. TAOM does not own
them and does not ship them. Names were read in yotthani's source and re-checked against TAOM at 0c5d81408
(v2.0.36, Bannerlord 1.5.6) with grep on 2026-10-29 [Certain]. The source comments name older TAOM versions ("TAOM 2.0.31
attack methods" in `Armory_TAOMSubModule.cs`:39), so a name here is a name that still resolves today, not a promise.

## Why it matters

A rename, a signature change or a move to another assembly (ADR-013 moves types into `TAOM.Client`) silently turns the other
mod's feature off. The other mod looks the member up by string, so TAOM's build and `TAOM.Tests` do not see the break, and
nothing in TAOM marks these members as externally read. What the other mod does on a miss, from its source:

- Armory_TAOM prints a line and carries on: "that attack keeps ignoring armor" for an attack method (`Armory_TAOMSubModule.cs`:209) or "that attack keeps TAOM's own armor" for a damage calculation (:258).
- RealmUI logs "TAOM's EquipPresets found, but its API is not as expected" (`PresetBridge.cs`:102) and drops that feature.
- CampaignProbe's reader returns null when "the model is not what we expect" (`TaomPowerSettingsReader.cs`:13).

Before renaming, retyping or moving a member in the tables, grep this file, and say so in the commit body.

The sections for SmithingExtended, RingSystem, TAOM_UI, DynamicPOIs, ThemeSwitcher, the standalone HoN modules, RTS Camera,
RaceArmorFit and ArmoryBalance_TAOM, and the added CampaignProbe and RealmUI rows, were read in yotthani's source at
`yotthani/bannerlord` `c45d8374` (cited `@c45d8374`). Each TAOM member in them was re-checked against trunk at v2.0.38 with
Grep on 2026-11-13 [TAOM-verified, names only; no mod was run]. How the other mod behaves on a miss is its source's claim.
Two couplings read an enum as an integer. Reordering or inserting a member in `FactionSide` or `WarPhase` shifts the meaning in
the other mod and breaks nothing in TAOM's build; `ExternalModContractTests` (`TAOM.Tests/Infrastructure/ExternalModContractTests.cs`, tests `FactionSide_KeepsTheValuesOtherModsRead` and `WarPhase_KeepsTheValuesOtherModsAndSavesRead`; #1186) pins the values. Append a new value at the end.

## Armory_TAOM (armor and auto-resolve for TAOM's scripted attacks)

| TAOM member | How it is reached | Upstream line | TAOM path (lines re-read 2026-11-03) |
|---|---|---|---|
| `TrollBruteForce.Hooks.BruteForceRing.Deliver` | Harmony patch; one overload only (`FindSingle`) | `Armory_TAOMSubModule.cs`:43, 287-293 | `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs`:25 |
| `SignatureStrikes.Hooks.SignatureStrikeRunner.Run` | Harmony patch | `:44` | `Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs`:55 |
| `ElephantLike.BehaviorTreeElements.ElephantLikeAttackTaskBase.Hit` (private) | Harmony patch | `:45` | `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs`:89 |
| `Warg.WargAttackService.CalculateWargAttackDamage` | patch, after a fit test: returns `int`, first parameter named `target`, a `float armorEffectivenessPercent` | `:51`, 253-255 | `Main/Features/Warg/WargAttackService.cs`:27 |
| `Spider.SpiderAttackService.CalculateSpiderBiteDamage` | same fit test | `:52`, 253-255 | `Main/Features/Spider/SpiderAttackService.cs`:26 |
| `AdvancedCombat.CustomAttacksUtils.TakeDamage`, the 8-parameter overload | patch found by exact parameter types; the prefix binds by NAME: `victim`, `damage`, `knockDown`, `damageType` | `:276-285`, 330-331 | `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`:152-153 |
| `Adapters.AgentAdapter.GetUnderlyingAgent()` | reflection: an instance method returning `Agent`, to unwrap the warg and spider `IAgentAdapter` target | `:420-432` | `Main/Adapters/AgentAdapter.cs`:58 |
| `TaomCombatSimulationModel.SimulateHit` (9 parameters) | the auto-resolve postfix walks `Campaign.Current.Models.CombatSimulationModel.GetType()` upward and patches the most-derived declared override; arguments by position (`__0`, `__1`) | `AutoResolvePatch.cs`:40-44, 52-59, 98-112 | `Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs`:30 |

The last row is why `TaomCombatSimulationModel` must stay the most-derived `SimulateHit` override on the runtime model type:
a subclass or a later model registered above it moves the patch target, and Armory's factor on map battles goes with it
([gamemodel-registry.md](gamemodel-registry.md)). Only the signature was re-checked on 1.5.6: `DefaultCombatSimulationModel.cs`:17
has the same nine parameters [Certain]. Whether the runtime type is TAOM's in a given mod list was not checked; yotthani saw it
land on TAOM in that run (`docs/balance-sim/autoresolve.md` 8.4).

**Troll knockdown.** Armory_TAOM also gates TAOM's troll ring knockdown by damage (`SmashKnockdownGate`, through the by-name `knockDown` parameter of the `TakeDamage` row). Its observation and TAOM's decision are in [troll-brute-force.md](../features/troll-brute-force.md) "Known limitations".

## RealmUI (a full interface re-skin)

All of these live in `TAOM.dll` (`Main/`) except where marked. Most are found by looking through every loaded assembly for one
named `TAOM`, then calling `TAOM.IoC.Resolve<T>()` on the interface; the type and `IoC` must sit in the same assembly.

| TAOM member | How it is reached | Upstream line (`RealmUI/Game/`) | TAOM path |
|---|---|---|---|
| `FieldCamp.UI.FieldCampMapView` | `AccessTools.TypeByName` (scans all assemblies) | `Map/MapBarCamp.cs`:19 | `Client/Features/FieldCamp/UI/FieldCampMapView.cs`:22 (`TAOM.Client`) |
| `CareerSystem.UI.GauntletCareerScreen`, `CareerScreenGameState` | `TypeByName` | `Map/MapBarTiles.cs`:116-117 | `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs`:20, `CareerScreenGameState.cs`:5 |
| `IoC.Resolve<T>()` (generic, no parameters) | looked up by name in the assembly called `TAOM` | `Inventory/Bridges/PresetBridge.cs`:82-99, `Flow/PlayAs.cs`:107-138, `Flow/RaceRules.cs`:70-74 | `Main/IoC.cs`:274 |
| `EquipPresets.IEquipmentPresetService`: `GetPresets(string)`, `SaveCurrent(string,string,bool,bool)`, `LoadPresetWithReport(string,string)`, `DeletePreset(string,string)` | reflection on exact parameter types | `PresetBridge.cs`:82-99 | `Main/Features/EquipPresets/IEquipmentPresetService.cs`:15, 22, 29, 31 |
| `EquipPresets.IEquipPresetsSettingsProvider` | type by name | `PresetBridge.cs`:82-99 | `Main/Features/EquipPresets/IEquipPresetsSettingsProvider.cs`:3 |
| `Adapters.IInventoryScreenAdapter`: `SetActive(SPInventoryVM)`, `ActiveHeroStringId` | reflection | `PresetBridge.cs`:82-99 | `Main/Adapters/IInventoryScreenAdapter.cs`:26, 33 |
| `Patch33_GauntletInventoryScreen._layer` (private static field) | `GetField`; RealmUI nulls it so TAOM's `OnFinalize` prefix does not remove the layer twice | `PresetBridge.cs`:82-99 | `Main/Features/EquipPresets/Hooks/Patch33_GauntletInventoryScreen.cs`:35 |
| `PlayerSwitcher.IHeroPickerService.BuildPickList`, `IPlayerSwitchPolicyProvider.Current`, `IPlayerSwitchSessionWriter.Select` and `Clear` | all four from one assembly, by `GetType` | `Flow/PlayAs.cs`:107-138 | `Main/Features/PlayerSwitcher/IHeroPickerService.cs`:11, `IPlayerSwitchPolicyProvider.cs`:10, `IPlayerSwitchSessionWriter.cs`:12, 16 |
| `PlayerSwitcher.Domain.HeroPickList` properties `RulingHouse`, `ClanLeaders`, `Wanderers`; row properties `HeroId`, `Name` | property reflection | `PlayAs.cs`:107-138 | `Main/Features/PlayerSwitcher/Domain/HeroPickList.cs`:23-25, `HeroPickRow.cs`:27-28 |
| `CareerSystem.ICareerDataService.HasCareer` | reflection | `PlayAs.cs`:107-138 | `Main/Features/CareerSystem/ICareerDataService.cs`:12 |
| `CharacterCreation.ICultureRaceFilterService.GetAllowedRaces` | reflection | `Flow/RaceRules.cs`:70-74 | `Main/Features/CharacterCreation/ICultureRaceFilterService.cs`:8 |
| `RealmBorders.RealmAtlasService` | existence of the type in any assembly | `Map/MapWidgets.cs`:27 | `Main/Features/RealmBorders/RealmAtlasService.cs`:20 |
| `ModuleData/special_resources/special_resources_config.xml` | read as a file under the `TAOM` module path | `Map/MapBarSeal.cs`:59-60 | `Main/_Module/ModuleData/special_resources/special_resources_config.xml` |
| `ExecuteFastForward`, `ExecuteExtraFastForward` on the time-speed view model | bound by `Command.Click` in RealmUI's prefab | `GUI/Prefabs/RealmMapBar.xml`:219, 229 | `Client/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs`:81, 71 |

Moving a `PlayAs` or `PresetBridge` target type into `TAOM.Client` (the ADR-013 direction) turns that RealmUI feature off,
because the lookup needs `IoC` and the type in one assembly. `FieldCampMapView` already lives in `TAOM.Client` and still
resolves, because `TypeByName` scans every assembly.

More RealmUI rows, read at `@c45d8374` and re-checked on trunk v2.0.38 (names only):

| TAOM member | How it is reached | Upstream line | TAOM path |
|---|---|---|---|
| `ModuleData/factionmap/factions.json` keys `game_faction`, `playable`, `name`, `side`, `description`, `difficulty`, `strengths`, `weaknesses`, `bonuses[]`, `special_units[]`, `perks[]` | parsed as data; only entries with `playable: true` and a `game_faction` are kept, keyed by `game_faction`; a missing file logs "faction sheets: none" and nothing else | `RealmUI/Game/Data/RealmInfo.cs@c45d8374`:55-92 | `Main/_Module/ModuleData/factionmap/factions.json` |
| Gauntlet layer and movie named `TaomRealmNames` | RealmUI finds the layer by that name to shrink every `TextWidget` font size in quiet map mode and to show or hide it | `RealmUI/Game/Map/QuietMapMode.cs@c45d8374`:48-83; `Flow/FlowController.cs`:184; `Map/MapCam.cs`:350, 358, 475-476 | `Client/Features/RealmBorders/UI/RealmBordersMapView.cs`:52-53 |
| Menu option ids `taom_armoury_visit`, `taom_sl_order`, `taom_sl_order_supplies`, `taom_castle_recruit_volunteers`, `taom_emissary_speak`, `taom_fc_dismiss_menu` | keys of an icon-group table | `RealmUI/Game/ModuleData/menu_groups.json@c45d8374`:29-36 | `ArmouryMenuBehavior`, `SupplyOrderTownMenu`, `CastleRecruitmentBehavior`, `EliteEmissaryBehavior`, `FieldCommissionDismissMenuBehavior` under `Main/Features/` |

The table lists both `taom_sl_order` and `taom_sl_order_supplies`. TAOM's option id is `taom_sl_order_supplies`
(`Main/Features/SupplyLines/Hooks/SupplyOrderTownMenu.cs`:16); `taom_sl_order` survives only inside a localization key there.
The table maps each id to a group and an icon, so a renamed id loses its icon group.

## CampaignProbe (unattended campaign runner)

| TAOM member | How it is reached | Upstream line | TAOM path |
|---|---|---|---|
| `TaomMilitaryPowerModel._settings` (private readonly `IBattleBalanceSettingsProvider`) | reflection on the field, after unwrapping the probe's own model through `BaseModel` | `HoN/CampaignProbe/Integration/TaomPowerSettingsReader.cs`:23-28 | `Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs`:9 |
| `EnableCustomTroopPower`, `OverrideVanillaTierPower`, `Tier7Power` to `Tier10Power`, `MountedMultiplier`, `HeroMultiplier` | property reflection on that object | `:35-40` and the lines after | `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`:20-27 |
| `TroopWeight.TroopWeightService.ApplyPartySizeWeightPenalty(PartyBase, ref ExplainedNumber, bool)` | Harmony prefix found by `AccessTools.TypeByName` and exact parameter types; it snapshots the pre-penalty party size limit, so it relies on being the LAST step of `TaomPartySizeModel.GetPartyMemberSizeLimit` | `HoN/CampaignProbe/Integration/Levers/TroopWeightPatch.cs@c45d8374`:8-35 | `Main/Features/TroopWeight/TroopWeightService.cs`:103; called last at `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs`:106 |
| `TaomMilitaryPowerModel.GetDefaultTroopPower`, `TaomCombatSimulationModel` (its `SimulateHit` override calls `base`), `TaomPartySizeModel`, `TaomPartyMoraleModel.GetEffectivePartyMorale` | the probe registers its own wrapper models with `AddModel` after TAOM's, so the wrapper's `BaseModel` is TAOM's model | `HoN/CampaignProbe/Integration/CampaignProbeSubModule.cs@c45d8374`:174-207; `HoN/CampaignProbe/Core/CallSiteCensus.cs@c45d8374`:21 (the type name as a string) | `BattleBalance/Models/TaomMilitaryPowerModel.cs`:7, 19; `TaomCombatSimulationModel.cs`:10, 30; `CulturalFeats/Models/TaomPartySizeModel.cs`:18; `TaomPartyMoraleModel.cs`:10, 21 (all under `Main/Features/`) |
| `ModuleData/execution/alignment.json` of the TAOM module | read as a file by the lever applier | `HoN/CampaignProbe/Integration/Levers/LeverApplier.cs@c45d8374`:162-166 | `Main/_Module/ModuleData/execution/alignment.json` |

Changing the parameter list of `ApplyPartySizeWeightPenalty`, or calling it before the other size feats in `TaomPartySizeModel`,
makes the probe report "not found" and end the lever run (read from its source, not run).

## VanillaTuning_TAOM (optional side rules)

It reads TAOM's side table as a file, `Modules/TAOM/ModuleData/execution/alignment.json` (`SideRules.cs`:36); a missing or
empty file switches its rules off with a log line. TAOM's copy is `Main/_Module/ModuleData/execution/alignment.json`. It also
prefixes `MakePeaceAction.ApplyInternal` next to TAOM's own prefix; see [diplomacy.md](../features/diplomacy.md)
"Free/Shadow side boundary".

## SmithingExtended (camp-smith repair and gear wear)

| TAOM member | How it is reached | Upstream line (`@c45d8374`) | TAOM path |
|---|---|---|---|
| `TAOM.IoC` (type found by name in the `TAOM` assembly), then `Resolve<T>()` for `Features.FieldCamp.ICampService` | reflection: `GetType("TAOM.IoC")`, `GetType("TAOM.Features.FieldCamp.ICampService")` | `HoN/SmithingExtended/Integration/Repair/CampProbe.cs`:116-122 | `Main/IoC.cs`:278; `Main/Features/FieldCamp/ICampService.cs`:4 (namespace), 31 |
| `ICampService.PlayerCamp` (property), `CampState.IsReady` (property; the probe also tries a field of that name) | `GetProperty("PlayerCamp")`, `GetProperty("IsReady") ?? GetField("IsReady")`; the player party only | `CampProbe.cs`:116-122 | `ICampService.cs`:31; `Main/Features/FieldCamp/Domain/CampState.cs`:64 |
| `TaomAgentStatCalculateModel`, `TaomBattleRewardModel` | SmithingExtended registers `WearAgentStatModel` and `WearBattleRewardModel` after TAOM's models, so each wrapper's `BaseModel` is TAOM's; its `SubModule.xml` lists `TAOM` and `Armory_TAOM` as optional `LoadBeforeThis` | `Integration/Wear/WearAgentStatModel.cs`:14; `WearBattleRewardModel.cs`:24, 35 | `Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs`:28; `Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs`:15 |

A rename of `PlayerCamp` or `IsReady` turns the camp-smith repair off without a build error. Replacing either TAOM model class in its
`AddModel` slot moves the wrapper's base. The wrapper copies the non-virtual AI level multiplier into `BaseModel` itself; see
[gamemodel-system.md](engine/gamemodel-system.md) "Non-virtual setters on a model".

## RingSystem (hunter war and culture alignment)

| TAOM member | How it is reached | Upstream line (`@c45d8374`) | TAOM path |
|---|---|---|---|
| `TAOM.IoC`, `Diplomacy.IWarOfTheRingService`, `Execution.IAlignmentService` | `Type.GetType("TAOM.IoC, TAOM")` and the two interface names with `, TAOM`; the resolved service is cached statically and cleared at campaign end | `HoN/RingSystem.Threat/TaomHunterWarProvider.cs`:21-22, 29-48; `HoN/RingSystem/RingCultureAlignment.cs`:52-58 | `Main/IoC.cs`:278; `Main/Features/Diplomacy/IWarOfTheRingService.cs`:5; `Main/Features/Execution/IAlignmentService.cs`:3 |
| `IWarOfTheRingService.IsWarOfTheRingActive` (bool, FullWar only) | property reflection | `TaomHunterWarProvider.cs`:30-36 | `IWarOfTheRingService.cs`:8 |
| `IWarOfTheRingService.CurrentPhase`, read with `Convert.ToInt32` | **enum as int**: upstream lists Peace 0, IsengardWar 1, FullWar 2 (`TaomHunterWarProvider.cs`:39) | `TaomHunterWarProvider.cs`:39-51 | `IWarOfTheRingService.cs`:7; `Main/Features/Diplomacy/Models/WarPhase.cs` |
| `IAlignmentService.GetKingdomSide(string)`, read as `FactionSide` | **enum as int**: Free 0, Evil 1, Neutral 2 | `RingCultureAlignment.cs`:52-58; `TaomHunterWarProvider.cs`:101-122 | `IAlignmentService.cs`:5; `Main/Features/Execution/FactionSide.cs` (no explicit values) |

`WarPhase` has a fourth value, `WarEnded` = 3, appended for saves that hold the older ints (comment in `WarPhase.cs`); upstream
lists only 0 to 2. Whether RingSystem handles 3 was not read [TAOM-verified, v2.0.38: the enum; RingSystem's handling is open].
A reorder or an insertion in either enum remaps values in RingSystem and in TAOM_UI (below) and fails `ExternalModContractTests` (#1186).

## TAOM_UI (nameplate re-skin, `HoN/TAOM_UI` v1.1.0)

| TAOM member | How it is reached | Upstream line (`@c45d8374`) | TAOM path |
|---|---|---|---|
| `IoC.Resolve<IAlignmentService>().GetKingdomSide(string)` | reflection; the result is mapped by ordinal, **enum as int**: `FactionSide` Free 0, Evil 1, Neutral 2 | `HoN/TAOM_UI/AlignmentProvider.cs`:8-16, 91-150 | `IoC.cs`:278; `IAlignmentService.cs`:3-5; `FactionSide.cs`:3-8 |
| `ModuleData/execution/alignment.json` | fallback reader of a flat `{"kingdomId": "free\|evil\|neutral"}` map that pairs quoted tokens in order; a nested object or array would shift the pairing | `AlignmentProvider.cs`:190-262 | `Main/_Module/ModuleData/execution/alignment.json` (flat on trunk v2.0.38) |
| `GUI/PreFabs/Nameplate/SettlementNameplateItemLarge.xml`, `Medium`, `Small` | replaced by the same relative path; its source says TAOM is `LoadBeforeThis`, so its prefab wins | `docs/realm-ui/research/ui-inventory.md`:500 | `Main/_Module/GUI/PreFabs/Nameplate/SettlementNameplateItem{Large,Medium,Small}.xml` |
| Harmony postfix on `SettlementNameplatesVM.Update`, and on `SettlementNameplateWidget.UpdateNameplateTransparencyAndBrightness` | TAOM patches the same `Update`: its cull prefix returns false once its own update ran, and Harmony still runs postfixes on a skipped original, so TAOM_UI's postfix runs every frame (cost not measured) | `HoN/TAOM_UI/SettlementNameplatesVMPatch.cs`:8-50; `SettlementNameplateWidgetPatch.cs`:10-40 | `Main/Features/NameplateCull/Hooks/SettlementNameplatesVM_Update_NameplateCull_Patch.cs`:14; `Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs`:27 (TAOM patches `DetermineTargetAlphaValue`, which `UpdateNameplateTransparencyAndBrightness` calls, `SettlementNameplateWidget.cs:519` on v1.5.7) |

A TAOM edit to the three nameplate prefabs is hidden while TAOM_UI is loaded. Reordering `FactionSide` swaps good and evil plates.

## DynamicPOIs (`TAOM_POI`)

DynamicPOIs adds two styles to the brush `Settlement.Event.Type.Image` with `BaseBrush="Settlement.Event.Type.Image"`, so the new brush
inherits the existing brush of that name and adds to it (`TAOM_POI/GUI/Brushes/dynamicpois_brushes.xml@c45d8374`:1-25). The header
comment says the last module loaded wins, so the inheritance includes another module's styles only if TAOM_POI loads after it.
TAOM defines the same brush name in `Main/_Module/GUI/Brushes/Nameplates.xml`:276 [TAOM-verified, v2.0.38], so TAOM_POI must load
after TAOM to extend TAOM's copy. A rename of that brush in TAOM breaks the inheritance (read from the XML, not run).

## ThemeSwitcher (`BannerlordThemeSwitcher`)

Its theme manifests bind to vanilla culture and kingdom ids (Gondor: `BaseCulture empire`, `BoundKingdoms empire`; Erebor `sturgia`;
Harad `aserai`; Lothlorien `battania`; Rhun `khuzait`): `Themes/<Theme>/ThemeManifest.xml@c45d8374`:7-10. TAOM reuses the ids for other
peoples (`empire` is Dunland, `sturgia` Dale, `aserai` Harad, `khuzait` Rhun, `battania` Khand; Gondor is `empire_w`), see
[id-cheatsheet.md](../modding/id-cheatsheet.md). With both mods installed a Dunland player would get the Gondor theme and a Gondor player
none. yotthani's inventory finds no in-game test result of the ThemeSwitcher on 1.5.3 in the files it searched (`ui-inventory.md`:611). Not a TAOM defect; the id table is the check
if TAOM ever lists ThemeSwitcher as compatible.

## yotthani's standalone HoN modules (SiegeDismount, SmartCavalryAI, MixedFormations, CompanionTactics, EquipPresets, FiefManagement, TransferbuttonMenu)

Each carries a `TaomGuard` (`HoN/<Module>/TaomGuard.cs@c45d8374`, seven files): `Utilities.GetModulesNames().Contains("TAOM")`. When
true the module applies no Harmony patch, adds no behaviour, registers no save definition and shows one notice. If the module list is
unreadable it re-checks later. CompanionTactics and EquipPresets also skip their save definitions because TAOM uses the same SaveBaseIds;
their commit bodies (`c775dd30`, `6a52a281`) say a duplicate save id throws; `c775dd30` names `DefinitionContext`.

| TAOM value | Where | Pinned by a test? [TAOM-verified, v2.0.38] |
|---|---|---|
| Module id `TAOM` | `Main/_Module/SubModule.xml`:4 | Yes, `ExternalModContractTests.SubModuleXml_DeclaresTheModuleIdTaom` (`TAOM.Tests/Infrastructure/ExternalModContractTests.cs`, no game needed; #1186) |
| SaveBaseId 726900501 (EquipPresets; class ids 101 and 102) | `PresetSaveableTypeDefiner` | The base id only: `TAOM.Tests/Features/EquipPresets/PresetSaveableTypeDefinerTests.cs`:15-25 asserts it; the test is `RequiresGame`, so it runs only where the game DLLs load |
| SaveBaseId 726900601 (FormationPreset, class id 101) | `Main/Features/CompanionTactics/FormationPresets/Models/FormationPresetSaveableTypeDefiner.cs`:18 | Yes, `FormationPresetSaveableTypeDefinerTests.BaseId_Is726900601` (`TAOM.Tests/Features/CompanionTactics/FormationPresets/FormationPresetSaveableTypeDefinerTests.cs`, #1186); it is `RequiresGame`, so it runs only where the game DLLs load |

The two tests pin the base ids only. The class ids, the SyncData keys, the behaviour class names and the saved property ids are not
pinned (#1186 holds the follow-up).

**What each value protects** [TAOM-verified, v2.0.38 and v1.5.7, code read 2026-11-13; no save was loaded]:

- **The module id.** Only the module id switches the standalone modules on: their guard reads nothing else. A rename lets both copies
  of a feature activate and patch the same targets, and CompanionTactics and EquipPresets then register the save ids TAOM already
  registered (a save id is base id plus class id, v1.5.7 `SaveableTypeDefiner.cs:87-90`; upstream's own comment says a second
  definition throws at save-system init).
- **The base ids.** A changed base id does not switch a standalone module on. It changes where TAOM's own saves find their types:
  an older save's preset then has no definition, the engine creates no object for it (v1.5.7 `ObjectHeaderLoadData.cs:40-48`), and
  for formation presets the list loads as null and TAOM substitutes an empty one (Codex's source trace, not run).
- **Formation presets are never imported from the standalone mod.** TAOM reads the key `"TAOM_FormationPresets"`
  (`Main/Features/CompanionTactics/FormationPresets/Hooks/FormationPresetCampaignBehavior.cs`:59); the standalone mod wrote
  `"FormationPresets"` (`HoN/CompanionTactics/FormationPresets/FormationPresetCampaignBehavior.cs@c45d8374`:52). The same type id does
  not bridge two keys. TAOM's definer comment (`FormationPresetSaveableTypeDefiner.cs`:9-10) still says the opposite.
- **EquipPresets meet, but not field for field.** The behaviour class name `EquipmentPresetCampaignBehavior` (a behaviour's store
  record is keyed by `StringId`, which defaults to the type name, v1.5.7 `CampaignBehaviorBase.cs:12-15`), the key
  `"EquipPresets_HeroPresets"`, the base id and the class ids 101 and 102 match on both sides, and so do the six properties of
  `HoNEquipmentPreset`. `HoNPresetItemReference` does not: TAOM saves `SlotIndex` as property 1 and `ItemStringId` as property 2
  (`Main/Features/EquipPresets/Models/HoNPresetItemReference.cs`:26-29), and the standalone mod saves them the other way round
  (`HoN/EquipPresets/Data/HoNPresetItemReference.cs@c45d8374`:9-13, its release of 2026-04-29). A save with the standalone mod's presets
  loads without an error, and the item ids are lost [TAOM-verified, v1.5.7, code read 2026-11-13; no such save was loaded]. The
  loader matches a saved member by its id alone and sets a property only when the saved value fits the property's type or
  converts to it (`PropertyLoadData.FillObject`, `LoadContext.TryConvertType`: number to number, number to string). So the saved
  string under id 1 is not written into TAOM's `int SlotIndex`, which stays 0, and the saved slot number under id 2 becomes TAOM's
  `ItemStringId` as text ("5", "9"). Such a preset shows under its name, equips nothing and reports the slot numbers as missing
  items; the first TAOM save then writes that state, so the original item ids are gone for good. TAOM's layout stays, because
  TAOM's saves have used it since 2026-05-07, and a repair at load could only help a save that TAOM has not saved since then
  (#1194).

## RTS Camera

RTS Camera / Command System v5.4.16 (built for Bannerlord 1.4.8, read from a decompile on 23.09.2026, not run;
`docs/analyse-rbm-rtscamera.md@c45d8374`:3-5, 66) detects TAOM and then switches off its own patch on
`Formation.CalculateHasSignificantNumberOfMounted` (a getter). How it detects TAOM is not in that file. TAOM's `MarshalSpacingPatch`
(`Main/Features/MarshalsWord/Hooks/MarshalSpacingPatch.cs`:13-17, 50-69) does not patch that getter. It rewrites the one `HasAnyMountedUnit`
call in each of seven `Formation` methods (the four spacing-limit getters and the public `GetUnitPositionWithIndexAccordingToNewOrder`
overloads) into a call of `CalculateHasSignificantNumberOfMounted`, leaves a method unchanged (log only) when it finds other than one call,
and applies only with Marshal's Word on and neither co-op nor a dedicated server [TAOM-verified, v2.0.38, by reading]. So the two mods
touch different methods: with RTS Camera's patch on, TAOM's seven methods route through it; with it off, nothing overlaps.

## RaceArmorFit and ArmoryBalance_TAOM (reach TAOM and Armory assets by name)

RaceArmorFit's config (`HoN/RaceArmorFit/README.md@c45d8374`, "Konfiguration" and "Per-item settings"; `ModuleData/racearmorfit.xml`)
names Armory items, meshes and skeletons: item prefixes `sk_dwarf_` and `sm_dwarf_`; skeletons `dwarf_skeleton_a`, `human_skeleton`; body
meshes `sm_dwarf_basemesh_a1_body`, `_shoulder`, `_arms`, `_legs`, `body_male_a`, `feet_male_a`, `hands_male_a`; per-item override files
for `sk_gd_ano_boots_a`, `sk_dwarf_dain_boots_*`, `sk_dwarf_erebor_chest_*`, `gondor_noble_coat_a`, `mirkwood_shoulder`. It ships its own
`cloth_bodies.xml` and `*_dwarf_cloth.tpac`, and its cloth carriers follow a naming pattern: the race's cook is `clo_<name>_<race>` next
to a carrier `<visible>_<race>_cloth` ([armory-guide.md](armory-guide.md) "A carrier metamesh for a race fit"). A rename of any of these
can make the fit skip an item: with no pair configured for the item's mesh, `FieldFit.cs@c45d8374:433-437` returns without a log line (read from code, not run; what a renamed body mesh does was not read). `sm_dwarf_basemesh_a1_body` resolves in the live
`LOTRLOME_Armory/ModuleData/skins.xml`, checked 2026-11-13; the other names were not each looked up.

`HoN/ArmoryBalance_TAOM` (`BalanceSubModule.cs`; `armory_balance.xml`, `armory_item_grades_balance.xml`) overrides item values after the
data load by item id, for example `sk_uruk_hai_helmet_spear_med_a4` (`check:head` 20, per the sim record
`docs/balance-sim/app-0.4-acceptance.md@c45d8374`). That id is in `Main/_Module/ModuleData/armour_acquisition/armour_classes.xml`
[TAOM-verified, v2.0.38]. A renamed TAOM or Armory item id makes its override row fail.

## Kysaro's modules (TAOM_Kysaro_Diplomacy, KysaroMod, TAOM_FactionUI, TAOM_CustomBattleUI)

Kysaro's source is not on this machine. The names below come from his notes (`Kysaro/bannerlord-knowledge-Kysaro`
at `f0a4370b5`, `gameplay/project_taom_kysaro_diplomacy.md` and `gameplay/project_legions_of_angmar.md`), and each
TAOM side was re-read by grep on 2026-11-12 at `abc5d0172` (v2.0.38) [Certain for the TAOM side; how his code
reaches each one is his note's claim]. What his code does on a miss is not known.

| TAOM member | How it is reached (his note) | TAOM path |
|---|---|---|
| `IoC.Resolve<T>()` on `Relations.IRelationGrantService`, `Execution.IAlignmentService`, `Diplomacy.IWarOfTheRingService`, `CoopInterop.ICoopSessionProvider`, `UncapturableHeroes.IUncapturableRegistry` | "public `TAOM.IoC.Resolve<T>()`" (diplomacy note, line 82) | `Main/IoC.cs`:276; `Main/Features/Relations/IRelationGrantService.cs`:7, `Execution/IAlignmentService.cs`:3, `Diplomacy/IWarOfTheRingService.cs`:5, `CoopInterop/ICoopSessionProvider.cs`:18, `UncapturableHeroes/IUncapturableRegistry.cs`:10 |
| `IWarOfTheRingService.ShouldBlockPeace(string, string)`, `GetWarVeto(string, string, bool)` | called through the resolved interface | `Main/Features/Diplomacy/IWarOfTheRingService.cs`:14, 44 |
| `TokensOfEsteem.GiftService.Give(string, GiftEntry, GiftOption, GiftCooldownLedger)` | Harmony postfix found by reflection ("King's Gift") | `Main/Features/TokensOfEsteem/GiftService.cs`:154 |
| `TaomArmyManagementModel` | postfix on the runtime army model (`ArmyPatch`) | `Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs`:14 |
| `TaomPartySizeModel`, `TaomPartyWageModel` | postfixes (`RemnantHostPatch`) | `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs`:18, `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs`:15 |
| `TaomPartyHealingModel.GetSurvivalChance` | postfix on the runtime model (`DeathlessPatch`) | `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs`:29 |
| Dialogue line `taom_nc_intro_a` (priority 110) | his dialogue is ordered around it | `Main/Features/NamedCompanions/Oaths/Hooks/OathIntroDialog.cs`:53 |
| Ids `town_G3` (Carn Dum) and `clan_gundabad_4` | his "Legions of Angmar" trigger | live `TAOM_Map/ModuleData/settlements.xml`; `Main/_Module/ModuleData/characters/clans.xml` |

He patches a model by walking to "the first non-abstract DeclaredMethod", so moving a model's override into a base
class, or replacing the model class in its `AddModel` slot, can move or drop his postfix.

**Duplicate-module hazard.** Two of his modules now have TAOM copies, and each pair patches the same screens:
TAOM_FactionUI was merged into TAOM (#704, [faction-ui.md](../features/faction-ui.md)), and TAOM integrated a muster
screen with his `Tcb*` names on 2026-11-12 (`df8ba8484`, `Client/Features/CustomBattles/Muster/`), while his note of
2026-11-10 keeps TAOM_CustomBattleUI as a separate mod and says loading both "would stack screens". Whether that module
is retired is open with Kysaro (PR #1 on his knowledge repo asks).

## Coodsz-TAOM (client launcher and server tool)

Coodsz's tools (`Coodsz/Coodsz-TAOM`, read 2026-11-12) reach a TAOM module folder by name, not a member:

| TAOM name | How it is reached | What a change does |
|---|---|---|
| The module id and folder `TAOM.CoopCompat` | "Install or repair TAOM" writes Sternab's 0.3.25 build into `Modules/TAOM.CoopCompat` | On a v1.5.x install it overwrites this module, and Coop's join check then refuses the peer ([coop-compat.md](../features/coop-compat.md), "Distribution") |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/features/career-system.md](../features/career-system.md)
- [docs/features/troll-brute-force.md](../features/troll-brute-force.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/engine/campaign-tick-time-and-party-ai.md](engine/campaign-tick-time-and-party-ai.md)
- [docs/reference/gamemodel-registry.md](./gamemodel-registry.md)

<!-- backlinks-end -->
