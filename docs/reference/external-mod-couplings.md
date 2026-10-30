# Other mods that patch or read TAOM by name

Reference list of the TAOM members that other mods reach by string: a Harmony patch target found by type and method name,
reflection, or a lookup through `TAOM.IoC`. The mods are yotthani's (repo `yotthani/bannerlord`, read at 6265f4a2); TAOM
does not own them and does not ship them. Names were read in yotthani's source and re-checked against TAOM at 0c5d81408
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

## Armory_TAOM (armor and auto-resolve for TAOM's scripted attacks)

| TAOM member | How it is reached | Upstream line | TAOM path at 0c5d81408 |
|---|---|---|---|
| `TrollBruteForce.Hooks.BruteForceRing.Deliver` | Harmony patch; one overload only (`FindSingle`) | `Armory_TAOMSubModule.cs`:43, 287-293 | `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs`:25 |
| `SignatureStrikes.Hooks.SignatureStrikeRunner.Run` | Harmony patch | `:44` | `Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs`:51 |
| `ElephantLike.BehaviorTreeElements.ElephantLikeAttackTaskBase.Hit` (private) | Harmony patch | `:45` | `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs`:89 |
| `Warg.WargAttackService.CalculateWargAttackDamage` | patch, after a fit test: returns `int`, first parameter named `target`, a `float armorEffectivenessPercent` | `:51`, 253-255 | `Main/Features/Warg/WargAttackService.cs`:27 |
| `Spider.SpiderAttackService.CalculateSpiderBiteDamage` | same fit test | `:52`, 253-255 | `Main/Features/Spider/SpiderAttackService.cs`:26 |
| `AdvancedCombat.CustomAttacksUtils.TakeDamage`, the 8-parameter overload | patch found by exact parameter types; the prefix binds by NAME: `victim`, `damage`, `knockDown`, `damageType` | `:276-285`, 330-331 | `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`:140-141 |
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
| `IoC.Resolve<T>()` (generic, no parameters) | looked up by name in the assembly called `TAOM` | `Inventory/Bridges/PresetBridge.cs`:82-99, `Flow/PlayAs.cs`:107-138, `Flow/RaceRules.cs`:70-74 | `Main/IoC.cs`:273 |
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
| `ExecuteFastForward`, `ExecuteExtraFastForward` on the time-speed view model | bound by `Command.Click` in RealmUI's prefab | `GUI/Prefabs/RealmMapBar.xml`:219, 229 | `Client/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs`:71, 81 |

Moving a `PlayAs` or `PresetBridge` target type into `TAOM.Client` (the ADR-013 direction) turns that RealmUI feature off,
because the lookup needs `IoC` and the type in one assembly. `FieldCampMapView` already lives in `TAOM.Client` and still
resolves, because `TypeByName` scans every assembly.

## CampaignProbe (unattended campaign runner)

| TAOM member | How it is reached | Upstream line | TAOM path |
|---|---|---|---|
| `TaomMilitaryPowerModel._settings` (private readonly `IBattleBalanceSettingsProvider`) | reflection on the field, after unwrapping the probe's own model through `BaseModel` | `HoN/CampaignProbe/Integration/TaomPowerSettingsReader.cs`:23-28 | `Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs`:9 |
| `EnableCustomTroopPower`, `OverrideVanillaTierPower`, `Tier7Power` to `Tier10Power`, `MountedMultiplier`, `HeroMultiplier` | property reflection on that object | `:35-40` and the lines after | `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs`:20-27 |

## VanillaTuning_TAOM (optional side rules)

It reads TAOM's side table as a file, `Modules/TAOM/ModuleData/execution/alignment.json` (`SideRules.cs`:36); a missing or
empty file switches its rules off with a log line. TAOM's copy is `Main/_Module/ModuleData/execution/alignment.json`. It also
prefixes `MakePeaceAction.ApplyInternal` next to TAOM's own prefix; see [diplomacy.md](../features/diplomacy.md)
"Free/Shadow side boundary".
