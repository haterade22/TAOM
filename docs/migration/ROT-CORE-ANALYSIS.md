# ROT-Core DLL Analysis for Bannerlord 1.3.13 Migration

**Source:** `E:\LOTRAOMAssets\Modules_ROT\Modules\ROT-Core\bin\Win64_Shipping_Client\ROT.dll`
**Analysis Date:** 2026-01-24
**Target Version:** Bannerlord 1.3.13
**Decompiler Used:** ilspycmd

---

## Executive Summary

ROT-Core (Realm of Thrones Core) is a comprehensive Bannerlord mod that:
- Patches 50+ TaleWorlds methods via Harmony
- Implements 24 custom GameModels using decorator pattern
- Adds 25+ CampaignBehaviors for custom gameplay
- Provides custom character creation with LOTR-themed occupations
- Implements naval combat, dragons, and other fantasy elements

**Migration Risk Level: HIGH** - Extensive Harmony patching means any API changes in 1.3.13 will break the mod silently.

---

## External Dependencies

| Package | Version | Purpose | Migration Notes |
|---------|---------|---------|-----------------|
| `HarmonyLib` | Unknown | Runtime method patching | Ensure compatible with .NET 4.7.2 |
| `Bannerlord.UIExtenderEx` | Unknown | UI modifications | Check for 1.3.13 compatible version |
| `MCM.Abstractions` | Unknown | Mod Configuration Menu | Check for 1.3.13 compatible version |
| `psai.net` | Unknown | Dynamic music system | Likely stable |

---

## SubModule Entry Point

**Class:** `ROT.SubModule`
**Base Class:** `MBSubModuleBase`

### Lifecycle Methods Used

```csharp
protected override void OnSubModuleLoad()
// - Creates Harmony instance "realmofthrones.core"
// - Patches MBInitialScreenBase methods (8 patches)
// - Patches psai.net Logik.LoadSoundtrackFromProjectFile
// - Checks for banned/incompatible modules
// - Removes vanilla initial state options
// - Adds custom "Into the Realm" menu option

protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
// - Registers all CampaignBehaviors
// - Registers all GameModels
// - Sets up UIExtender
// - Loads custom sprite categories

public override void OnMissionBehaviorInitialize(Mission mission)
// - Adds mission behaviors for dragons, naval, etc.

public override void OnAfterGameInitializationFinished(Game game, object starterObject)
// - Creates special hero for bandit faction leader

public override void OnGameInitializationFinished(Game game)
// - Fixes village TradeBound for bandit settlements
// - Validates GameModel load order

protected override void OnApplicationTick(float dt)
// - Handles duel menu activation
// - Updates enlistment behavior

protected override void OnBeforeInitialModuleScreenSetAsRoot()
// - Creates StorylinePopupLayer
```

### Module Compatibility Checks

**Banned Modules (will cause exit):**
- `TOR_Core`
- `CA_EagleRising`

**Integrated Modules (warns user):**
- `StopStarvingYourselves`
- `BannerColorPersistence`
- `MolochsDuels`

**Overlapping Modules (warns user):**
- `RecruitYourOwnCulture`
- `SmartRBMpatch`

**RBM Load Order Check:**
- ROT must load after RBM if both are present

---

## Custom GameModels (24 Total)

All models use **decorator pattern** - they wrap the vanilla model and extend/modify behavior.

### Registration Pattern
```csharp
val.AddModel<ModelType>((MBGameModel<ModelType>)(object)new ROTModelType(GetGameModel<ModelType>(gameStarterObject)));
```

### Complete Model List

| ROT Model | Base TaleWorlds Class | Purpose |
|-----------|----------------------|---------|
| `ROTKingdomDecisionPermissionModel` | `KingdomDecisionPermissionModel` | Kingdom decision rules |
| `ROTSettlementMilitiaModel` | `SettlementMilitiaModel` | Militia calculations |
| `ROTSettlementFoodModel` | `SettlementFoodModel` | Food production/consumption |
| `ROTMilitaryPowerModel` | `MilitaryPowerModel` | Army strength calculations |
| `ROTCombatSimulationModel` | `CombatSimulationModel` | Auto-resolve combat |
| `ROTEasyCastleMilitiaModel` | `SettlementMilitiaModel` | Castle-specific militia |
| `ROTVoiceOverModel` | `VoiceOverModel` | Character voice selection |
| `ROTPartyFoodBuyingModel` | `PartyFoodBuyingModel` | AI food purchasing |
| `ROTBanditDensityModel` | `BanditDensityModel` | Bandit spawn rates |
| `ROTSettlementAccessModel` | `SettlementAccessModel` | Settlement entry rules |
| `ROTPartySpeedModel` | `PartySpeedModel` | Party movement speed |
| `ROTClanFinanceModel` | `ClanFinanceModel` | Clan income/expenses |
| `ROTMobilePartyFoodConsumptionModel` | `MobilePartyFoodConsumptionModel` | Party food usage |
| `ROTArmyManagementCalculationModel` | `ArmyManagementCalculationModel` | Army cohesion/influence |
| `ROTPartySizeLimitModel` | `PartySizeLimitModel` | Max party sizes |
| `ROTSettlementGarrisonModel` | `SettlementGarrisonModel` | Garrison sizes |
| `ROTPartyImpairmentModel` | `PartyImpairmentModel` | Wounded/prisoner effects |
| `ROTKingdomCreationModel` | `KingdomCreationModel` | Kingdom founding rules |
| `ROTMarriageModel` | `MarriageModel` | Marriage rules |
| `ROTAgentStatCalculateModel` | `AgentStatCalculateModel` | Combat stats calculation |
| `ROTAgeModel` | `AgeModel` | Character aging |
| `ROTPartyWageModel` | `PartyWageModel` | Troop wages |
| `ROTTroopUpgradeModel` | `PartyTroopUpgradeModel` | Upgrade requirements |
| `ROTPrisonerRecruitmentCalculationModel` | `PrisonerRecruitmentCalculationModel` | Prisoner recruitment |
| `ROTCampaignTimeModel` | `CampaignTimeModel` | Time passage |

### Migration Checklist for Models
- [ ] Check each base class for new abstract methods in 1.3.13
- [ ] Check method signature changes in overridden methods
- [ ] Verify constructor parameters haven't changed
- [ ] Test decorator chain still works

---

## Harmony Patches (50+ Patches)

### Patch Organization by Namespace

#### `ROT.HarmonyPatches.Core`

| Target Class | Target Method | Patch Type | Purpose |
|--------------|---------------|------------|---------|
| `CampaignEventDispatcher` | `AiHourlyTick` | Unknown | AI behavior modification |
| `AiPartyThinkBehavior` | `PartyHourlyAiTick` | Unknown | Party AI decisions |
| `AiVisitSettlementBehavior` | `FillSettlementsToVisitWithDistancesAsDays` | Unknown | Settlement visit AI |
| `HeroCreator` | `CreateNotable` | Unknown | Notable creation |
| `CharacterCreationCultureStageVM` | `InitializePlayersFaceKeyAccordingToCultureSelection` | Unknown | Character creation |
| `SandBoxManager` | `Initialize` | Unknown | Sandbox initialization |
| `FactionDiscontinuationCampaignBehavior` | `CanClanBeDiscontinued` | Unknown | Clan destruction rules |
| `DefaultDiplomacyModel` | `GetScoreOfClanToJoinKingdom` | Unknown | Diplomacy scoring |
| `Campaign` | `InitializeScenes` | Unknown | Scene loading |
| `MissionSettlementPrepareView` | `SetOwnerBanner` | Unknown | Banner display |
| `EncounterGameMenuBehavior` | `game_menu_encounter_capture_the_enemy_on_condition` | Unknown | Encounter menus |
| `EncounterGameMenuBehavior` | `game_menu_encounter_leave_on_condition` | Unknown | Encounter menus |
| `PlayerEncounter` | `DoLootParty` | Unknown | Looting behavior |
| `MissionCombatMechanicsHelper` | `IsCollisionBoneDifferentThanWeaponAttachBone` | Unknown | Combat mechanics |
| `KingSelectionKingdomDecision` | `CalculateMeritOfOutcomeForClan` | Unknown | King election |
| `SettlementClaimantDecision` | `CalculateMeritOfOutcome` | Unknown | Settlement claims |
| `LordConversationsCampaignBehavior` | `conversation_set_oath_phrases_on_condition` | Unknown | Oath dialogues |
| `LordConversationsCampaignBehavior` | `conversation_liege_states_obligations_to_vassal_on_condition` | Unknown | Vassal dialogues |
| `SandBoxGauntletGameNotification` | `RegisterEvents` | Unknown | Notification system |
| `SandBoxGauntletGameNotification` | `UnregisterEvents` | Unknown | Notification system |
| `TakePrisonerAction` | `ApplyInternal` | Unknown | Prisoner taking |
| `FightTournamentGame` | `GetTournamentPrize` | Unknown | Tournament rewards |
| `MissionCombatMechanicsHelper` | `DecideWeaponCollisionReaction` | Unknown | Combat reactions |
| `Mission` | `MeleeHitCallback` | Unknown | Melee combat |
| `MissionCombatMechanicsHelper` | `GetDefendCollisionResults` | Unknown | Defense mechanics |
| `DefaultDiplomacyModel` | `GetReasonForDeclaringPeace` | Unknown | Peace diplomacy |
| `SPInventoryVM` | `RefreshInformationValues` | Unknown | Inventory UI |
| `MenuContext` | `OpenTroopSelection` | Unknown | Troop selection |
| `SiegeAftermathCampaignBehavior` | `menu_settlement_taken_on_init` | Unknown | Siege aftermath |

#### `ROT.HarmonyPatches.Dragon`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `UpgradeTargetVM` | `Refresh` | Dragon upgrade UI |
| `FoodConsumptionBehavior` | `CheckAnimalBreeding` | Dragon breeding |
| `PartyCharacterVM` | `ExecuteExecuteTroop` | Dragon execution prevention |

#### `ROT.HarmonyPatches.Boats`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `MapScreen` | `StepSounds` | Naval movement sounds |
| `ExplainedNumber` | `AddFactor` | Naval speed modifiers |
| `MapScene` | `GetHeightAtPoint` | Water detection |
| `MobilePartyVisual` | `Tick` | Ship visuals |
| `CaravanBattleMissionHandler` | `AfterStart` | Naval battles |

#### `ROT.HarmonyPatches.CustomCompanions`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `KillCharacterAction` | `ApplyInLabor` | Childbirth deaths |
| `HeroSpawnCampaignBehavior` | `SpawnMinorFactionHeroes` | Hero spawning |
| `CompanionsCampaignBehavior` | `GetCompanionTemplateToSpawn` | Companion templates |

#### `ROT.HarmonyPatches.NativeBugfixes`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `LeaveKingdomAsClanBarterable` | `Apply` | Barter fix |
| `MarriageOfferCampaignBehavior` | `DailyTickClan` | Marriage AI fix |

#### `ROT.HarmonyPatches.NativeCrashfixes`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `GangLeaderNeedsToOffloadStolenGoodsIssueBehavior` | `ConditionsHold` | Quest crash fix |

#### `ROT.HarmonyPatches.ModCompatibility`

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `BasicCharacterObject` | `Deserialize` | XML loading |
| `Equipment` | `Deserialize` | Equipment loading |
| `ArmorComponent` | `Deserialize` | Armor loading |
| `MBObjectManager` | `CreateDocumentFromXmlFile` | XML document creation |
| `BladeData` | `Deserialize` | Weapon loading |
| `WeaponComponent` | `Deserialize` | Weapon loading |
| `DeclareWarAction` | `ApplyInternal` | War declaration |

#### UI/Screen Patches (in SubModule)

| Target Class | Target Method | Patch Type |
|--------------|---------------|------------|
| `MBInitialScreenBase` | `RefreshScene` | Prefix |
| `MBInitialScreenBase` | `OnInitialize` | Prefix |
| `MBInitialScreenBase` | `OnFinalize` | Prefix |
| `MBInitialScreenBase` | `OnDeactivate` | Prefix |
| `MBInitialScreenBase` | `OnFrameTick` | Prefix |
| `MBInitialScreenBase` | `StartedRendering` | Prefix |
| `MBInitialScreenBase` | `OnPause` | Prefix |
| `MBInitialScreenBase` | `OnResume` | Prefix |

#### Music Patches (in SubModule)

| Target Class | Target Method | Purpose |
|--------------|---------------|---------|
| `psai.net.Logik` | `LoadSoundtrackFromProjectFile` | Custom music |

### Migration Checklist for Patches
- [ ] Verify each target method still exists in 1.3.13
- [ ] Check method signatures haven't changed
- [ ] Test patches apply without errors at runtime
- [ ] Check for renamed methods/classes
- [ ] Verify private method access still works (reflection)

---

## CampaignBehaviors (25+ Behaviors)

### Complete Behavior List

| Behavior Class | Purpose | Events Used |
|----------------|---------|-------------|
| `ROTStorylineWars` | Storyline war events | Unknown |
| `ROTDuelsBehavior` | Duel system | Unknown |
| `ROTRavenBehavior` | Messenger raven system | Unknown |
| `ROTEventBehavior` | Custom events | Unknown |
| `ROTGankBehavior` | Ambush mechanics | Unknown |
| `ROTSpawnUniqueWanderers` | Special wanderer spawning | Unknown |
| `ROTTownTradersBehavior` | Town merchant tweaks | Unknown |
| `ROTTradeBoundBehavior` | Trade route bindings | Unknown |
| `ROTCoreBehavior` | Core mod functionality | Unknown |
| `ROTRaceMap` | Race system | Unknown |
| `ROTTroopRecruiter` | Recruitment mechanics | Unknown |
| `ROTDragonBehavior` | Dragon system | Unknown |
| `ValyrianThiefCampaignBehavior` | Valyrian steel theft | Unknown |
| `ROTClanRecruitBehavior` | Clan recruitment | Unknown |
| `ROTHouseTroopsBehavior` | House-specific troops | Unknown |
| `ROTEnlistmentBehavior` | Player enlistment | Unknown |
| `ROTEnlistmentMenuBehavior` | Enlistment menus | Unknown |
| `ROTRedWeddingBehavior` | Red Wedding events | Unknown |
| `ROTSendToWallBehavior` | Night's Watch sending | Unknown |
| `ROTOthersCampaignBehavior` | Misc functionality | Unknown |
| `ROTEasterEggWeapons` | Special weapons | Unknown |
| `ROTBanditLeaveHideouts` | Bandit AI | Unknown |
| `ROTRelationshipsBehavior` | Relationship modifiers | Unknown |
| `ROTRBMCompatibility` | RBM mod compat | Unknown |
| `ROTCharacterCreationCampaignBehavior` | Character creation | `OnCharacterCreationInitializedEvent` |

### Migration Checklist for Behaviors
- [ ] Check `CampaignEvents` for removed/renamed events
- [ ] Verify event handler signatures
- [ ] Test `RegisterEvents()` patterns still work
- [ ] Check `SyncData()` serialization compatibility

---

## MissionBehaviors (7 Behaviors)

| Behavior Class | Purpose | Trigger |
|----------------|---------|---------|
| `ROTPoppyMissionView` | Visual effects | Campaign missions |
| `ROTDragonMissionBehavior` | Dragon combat | Campaign missions |
| `ValyrianThiefMissionBehavior` | Theft mechanics | Campaign missions |
| `EasterWeaponSpawner` | Special weapon spawns | Campaign missions |
| `ChariotMissionBehavior` | Chariot combat | Campaign missions |
| `NavalBattleLogic` | Naval battles | `mission.IsNavalBattle()` |
| `ROTEnlistmentMissionBehavior` | Enlistment combat | When `EnlistmentBehavior.IsEnlisted` |

---

## Character Creation System

### Interface Implementation
```csharp
public class ROTCharacterCreationCampaignBehavior : CampaignBehaviorBase, ICharacterCreationContentHandler
```

### Custom Occupation Types

**Rural Occupations:**
- `retainer` - Noble servant
- `bard` - Entertainer
- `hunter` - Game hunter
- `farmer` - Agricultural worker
- `herder` - Livestock keeper
- `healer` - Medical practitioner
- `mercenary` - Sellsword
- `infantry` - Foot soldier
- `skirmisher` - Light fighter
- `kern` - Irish warrior
- `guard` - Settlement guard

**Urban Occupations:**
- `retainer_urban` - City noble servant
- `mercenary_urban` - City sellsword
- `merchant_urban` - Trader
- `vagabond_urban` - Wanderer
- `artisan_urban` - Craftsman
- `physician_urban` - Doctor
- `healer_urban` - City healer
- `bard_urban` - City entertainer

### Equipment Mapping
Each occupation maps to equipment template IDs:
- `mother_char_creation_{occupation}_{culture}`
- `father_char_creation_{occupation}_{culture}`
- `player_char_creation_childhood_age_{culture}_{occupation}_{gender}`
- `player_char_creation_education_age_{culture}_{occupation}_{gender}`
- `player_char_creation_{culture}_{occupation}_{gender}`

### Narrative Menu Characters
- `mother_character` - Mother NPC
- `father_character` - Father NPC
- `player_childhood_character` - Child player
- `player_education_character` - Education age player
- `player_youth_character` - Youth player
- `player_adulthood_character` - Adult player
- `player_age_selection_character` - Age selection
- `narrative_character_horse` - Horse

### Age Constants
```csharp
const int ChildhoodAge = 7;
const int EducationAge = 12;
const int YouthAge = 17;
const int AccomplishmentAge = 20;
const int ParentAge = 33;
const int YoungAdultAge = 20;
const int AdultAge = 30;
const int MiddleAge = 40;
const int ElderAge = 50;
```

### Focus/Attribute Points by Age
| Age Start | Focus Points | Attribute Points |
|-----------|--------------|------------------|
| Youth | 2 | 1 |
| Adult | 4 | 2 |
| Middle-Aged | 6 | 3 |
| Elderly | 8 | 4 |

### Migration Checklist for Character Creation
- [ ] Verify `ICharacterCreationContentHandler` interface unchanged
- [ ] Check `CharacterCreationStageBase` subclasses
- [ ] Test `NarrativeMenu` and `NarrativeMenuCharacter` APIs
- [ ] Verify equipment template loading

---

## UIExtender Usage

### Registered Extensions (conditional on settings)

| Extension Class | Type | Target | Condition |
|-----------------|------|--------|-----------|
| `EncyclopediaHeroPagePrefabExtension` | PrefabExtensionInsertPatch | Encyclopedia | `ROTSettings.Ravens` |
| `EncyclopediaHeroPageVMMixin` | ViewModelMixin | Encyclopedia | `ROTSettings.Ravens` |
| `InquiryElementVMMixin` | ViewModelMixin | Inquiries | Always |
| `HeroVMMixin` | ViewModelMixin | Hero display | Always |
| `StorylineWarsButton` | PrefabExtensionInsertPatch | Kingdom UI | Always |
| `KingdomManagementVMMixin` | ViewModelMixin | Kingdom UI | Always |
| `MissionNameMarkerTargetVMMixin` | ViewModelMixin | Name markers | Always |

### Sprite Categories Loaded
- `ui_enlistment`

---

## TaleWorlds Namespaces Used

### CampaignSystem (Most Critical)
```
TaleWorlds.CampaignSystem
TaleWorlds.CampaignSystem.Actions
TaleWorlds.CampaignSystem.AgentOrigins
TaleWorlds.CampaignSystem.BarterSystem.Barterables
TaleWorlds.CampaignSystem.CampaignBehaviors
TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors
TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors
TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors
TaleWorlds.CampaignSystem.CharacterCreationContent
TaleWorlds.CampaignSystem.CharacterDevelopment
TaleWorlds.CampaignSystem.ComponentInterfaces
TaleWorlds.CampaignSystem.Conversation
TaleWorlds.CampaignSystem.Election
TaleWorlds.CampaignSystem.Encounters
TaleWorlds.CampaignSystem.Encyclopedia
TaleWorlds.CampaignSystem.Extensions
TaleWorlds.CampaignSystem.GameComponents
TaleWorlds.CampaignSystem.GameMenus
TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers
TaleWorlds.CampaignSystem.GameState
TaleWorlds.CampaignSystem.Inventory
TaleWorlds.CampaignSystem.Issues
TaleWorlds.CampaignSystem.Map
TaleWorlds.CampaignSystem.Map.DistanceCache
TaleWorlds.CampaignSystem.MapEvents
TaleWorlds.CampaignSystem.Naval
TaleWorlds.CampaignSystem.Party
TaleWorlds.CampaignSystem.Party.PartyComponents
TaleWorlds.CampaignSystem.Roster
TaleWorlds.CampaignSystem.SceneInformationPopupTypes
TaleWorlds.CampaignSystem.Settlements
TaleWorlds.CampaignSystem.Settlements.Locations
TaleWorlds.CampaignSystem.Settlements.Workshops
TaleWorlds.CampaignSystem.Siege
TaleWorlds.CampaignSystem.TournamentGames
TaleWorlds.CampaignSystem.ViewModelCollection
TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation
TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages
TaleWorlds.CampaignSystem.ViewModelCollection.Inventory
TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement
TaleWorlds.CampaignSystem.ViewModelCollection.Map
TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes
TaleWorlds.CampaignSystem.ViewModelCollection.Party
TaleWorlds.CampaignSystem.ViewModelCollection.Quests
```

### MountAndBlade
```
TaleWorlds.MountAndBlade
TaleWorlds.MountAndBlade.GauntletUI
TaleWorlds.MountAndBlade.GauntletUI.Mission
TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory
TaleWorlds.MountAndBlade.Objects
TaleWorlds.MountAndBlade.Source.Missions
TaleWorlds.MountAndBlade.Source.Missions.Handlers
TaleWorlds.MountAndBlade.View
TaleWorlds.MountAndBlade.View.MissionViews
TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer
TaleWorlds.MountAndBlade.View.Screens
TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator
TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries
```

### Core/Engine/Other
```
TaleWorlds.Core
TaleWorlds.Core.ImageIdentifiers
TaleWorlds.Core.ViewModelCollection
TaleWorlds.Core.ViewModelCollection.ImageIdentifiers
TaleWorlds.Core.ViewModelCollection.Information
TaleWorlds.Core.ViewModelCollection.Selector
TaleWorlds.DotNet
TaleWorlds.Engine
TaleWorlds.Engine.GauntletUI
TaleWorlds.Engine.Screens
TaleWorlds.GauntletUI
TaleWorlds.GauntletUI.BaseTypes
TaleWorlds.InputSystem
TaleWorlds.Library
TaleWorlds.LinQuick
TaleWorlds.Localization
TaleWorlds.ModuleManager
TaleWorlds.ObjectSystem
TaleWorlds.SaveSystem
TaleWorlds.SaveSystem.Resolvers
TaleWorlds.ScreenSystem
TaleWorlds.TwoDimension
```

---

## 1.3.13 Migration Priority Matrix

### P0 - Critical (Will crash if broken)
1. Harmony patch target methods
2. GameModel abstract method implementations
3. `ICharacterCreationContentHandler` interface
4. `CampaignBehaviorBase` event registration

### P1 - High (Will cause major bugs)
1. ViewModel property bindings
2. SaveSystem serialization
3. XML deserialization patches
4. Menu/UI callbacks

### P2 - Medium (Will cause minor issues)
1. Model calculation changes
2. AI behavior patches
3. Naval system APIs
4. Tournament/duel mechanics

### P3 - Low (Cosmetic/polish issues)
1. Sprite loading
2. Sound/music patches
3. Scene initialization
4. Notification system

---

## Testing Strategy for 1.3.13

### Phase 1: Compile Test
1. Update TaleWorlds references to 1.3.13
2. Fix any compile errors from API changes
3. Document all changes made

### Phase 2: Load Test
1. Launch game with mod
2. Check Harmony patches apply without errors (check logs)
3. Verify UIExtender loads without errors

### Phase 3: Functional Test
1. Start new campaign
2. Test character creation (all occupations)
3. Test each major feature:
   - Dragons
   - Naval combat
   - Duels
   - Ravens
   - Storyline wars
   - Enlistment

### Phase 4: Save Compatibility
1. Load 1.3.12 save in 1.3.13
2. Test all CampaignBehavior data loads
3. Verify no null references from missing data

---

## Files Reference

**DLL Location:** `E:\LOTRAOMAssets\Modules_ROT\Modules\ROT-Core\bin\Win64_Shipping_Client\ROT.dll`
**PDB Location:** `E:\LOTRAOMAssets\Modules_ROT\Modules\ROT-Core\bin\Win64_Shipping_Client\ROT.pdb`
**DLL Size:** 984,064 bytes
**PDB Size:** 1,887,744 bytes

---

## Appendix: Decompilation Commands Used

```powershell
# Full decompile
ilspycmd "E:\LOTRAOMAssets\Modules_ROT\Modules\ROT-Core\bin\Win64_Shipping_Client\ROT.dll"

# Specific type
ilspycmd "E:\...\ROT.dll" -t "ROT.SubModule"

# Search for patterns
ilspycmd "E:\...\ROT.dll" 2>&1 | grep -E "HarmonyPatch|namespace ROT"
ilspycmd "E:\...\ROT.dll" 2>&1 | grep -E "^using TaleWorlds" | sort -u
ilspycmd "E:\...\ROT.dll" 2>&1 | grep -E "class.*Patch"
```

---

## Appendix B: Bannerlord 1.3.13 API Verification

**Verification Date:** 2026-01-24
**Source DLLs:** `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

### Verified Interface Signatures

#### ICharacterCreationContentHandler
```csharp
namespace TaleWorlds.CampaignSystem.CharacterCreationContent;

public interface ICharacterCreationContentHandler
{
    void InitializeContent(CharacterCreationManager characterCreationManager);
    void AfterInitializeContent(CharacterCreationManager characterCreationManager);
    void OnStageCompleted(CharacterCreationStageBase stage);
    void OnCharacterCreationFinalize(CharacterCreationManager characterCreationManager);
}
```
**Status:** ✅ Interface unchanged - ROT implementation should work

### Verified Harmony Patch Target Signatures

#### CampaignEventDispatcher.AiHourlyTick
```csharp
public override void AiHourlyTick(MobileParty party, PartyThinkParams partyThinkParams)
```
**Status:** ✅ Signature matches expected pattern

#### HeroCreator.CreateNotable
```csharp
public static Hero CreateNotable(Occupation occupation, Settlement settlement = null)
```
**Status:** ✅ Signature verified

#### TakePrisonerAction.ApplyInternal
```csharp
private static void ApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled = true)
```
**Status:** ✅ Signature verified

#### DeclareWarAction.ApplyInternal
```csharp
private static void ApplyInternal(IFaction faction1, IFaction faction2, DeclareWarDetail declareWarDetail)
```
**Status:** ✅ Signature verified

#### KillCharacterAction.ApplyInLabor
```csharp
public static void ApplyInLabor(Hero lostMother, bool showNotification = true)
```
**Status:** ✅ Signature verified

#### Mission.MeleeHitCallback
```csharp
internal void MeleeHitCallback(
    ref AttackCollisionData collisionData,
    Agent attacker,
    Agent victim,
    GameEntity realHitEntity,
    ref float inOutMomentumRemaining,
    ref MeleeCollisionReaction colReaction,
    CrushThroughState crushThroughState,
    Vec3 blowDir,
    Vec3 swingDir,
    ref HitParticleResultData hitParticleResultData,
    bool crushedThroughWithoutAgentCollision)
```
**Status:** ⚠️ Complex signature - verify against ROT patch implementation

### Verified GameModel Base Classes

#### SettlementMilitiaModel
```csharp
public abstract class SettlementMilitiaModel : MBGameModel<SettlementMilitiaModel>
{
    public abstract int MilitiaToSpawnAfterSiege(Town town);
    public abstract ExplainedNumber CalculateMilitiaChange(Settlement settlement, bool includeDescriptions = false);
    public abstract ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement);
    public abstract void CalculateMilitiaSpawnRate(Settlement settlement, out float meleeTroopRate, out float rangedTroopRate);
}
```
**Status:** ✅ Base class verified

#### PartySpeedModel
```csharp
public abstract class PartySpeedModel : MBGameModel<PartySpeedModel>
{
    public abstract float BaseSpeed { get; }
    public abstract float MinimumSpeed { get; }
    public abstract ExplainedNumber CalculateBaseSpeed(MobileParty party, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0);
    public abstract ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed);
}
```
**Status:** ✅ Base class verified

#### AgeModel
```csharp
public abstract class AgeModel : MBGameModel<AgeModel>
{
    public abstract int BecomeInfantAge { get; }
    public abstract int BecomeChildAge { get; }
    public abstract int BecomeTeenagerAge { get; }
    public abstract int HeroComesOfAge { get; }
    public abstract int BecomeOldAge { get; }
    public abstract int MiddleAdultHoodAge { get; }
    public abstract int MaxAge { get; }
    public abstract void GetAgeLimitForLocation(CharacterObject character, out int minimumAge, out int maximumAge, string additionalTags = "");
}
```
**Status:** ✅ Base class verified

#### MarriageModel
```csharp
public abstract class MarriageModel : MBGameModel<MarriageModel>
{
    public abstract int MinimumMarriageAgeMale { get; }
    public abstract int MinimumMarriageAgeFemale { get; }
    public abstract bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero);
    public abstract int GetEffectiveRelationIncrease(Hero firstHero, Hero secondHero);
    public abstract Clan GetClanAfterMarriage(Hero firstHero, Hero secondHero);
    public abstract bool IsSuitableForMarriage(Hero hero);
    public abstract bool IsClanSuitableForMarriage(Clan clan);
    public abstract float NpcCoupleMarriageChance(Hero firstHero, Hero secondHero);
    public abstract bool ShouldNpcMarriageBetweenClansBeAllowed(Clan consideringClan, Clan targetClan);
    public abstract List<Hero> GetAdultChildrenSuitableForMarriage(Hero hero);
}
```
**Status:** ✅ Base class verified

#### KingdomCreationModel
```csharp
public abstract class KingdomCreationModel : MBGameModel<KingdomCreationModel>
{
    public abstract int MinimumClanTierToCreateKingdom { get; }
    public abstract int MinimumNumberOfSettlementsOwnedToCreateKingdom { get; }
    public abstract int MinimumTroopCountToCreateKingdom { get; }
    public abstract int MaximumNumberOfInitialPolicies { get; }
    public abstract bool IsPlayerKingdomCreationPossible(out List<TextObject> explanations);
    public abstract bool IsPlayerKingdomAbdicationPossible(out List<TextObject> explanations);
    public abstract IEnumerable<CultureObject> GetAvailablePlayerKingdomCultures();
}
```
**Status:** ✅ Base class verified

#### AgentStatCalculateModel (from MountAndBlade)
```csharp
public abstract class AgentStatCalculateModel : MBGameModel<AgentStatCalculateModel>
{
    public abstract void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData);
    public virtual void InitializeMissionEquipment(Agent agent) { }
    public virtual void InitializeAgentStatsAfterDeploymentFinished(Agent agent) { }
    public virtual void InitializeMissionEquipmentAfterDeploymentFinished(Agent agent) { }
    public abstract void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties);
    public abstract float GetDifficultyModifier();
    public abstract bool CanAgentRideMount(Agent agent, Agent targetMount);
    public virtual bool HasHeavyArmor(Agent agent);
    public virtual float GetEffectiveArmorEncumbrance(Agent agent, Equipment equipment);
    public virtual float GetEffectiveMaxHealth(Agent agent);
    public virtual float GetEnvironmentSpeedFactor(Agent agent);
    // ... many more virtual methods
    public abstract float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon);
    public abstract float GetEquipmentStealthBonus(Agent agent);
    public abstract float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon);
    public abstract float GetKnockBackResistance(Agent agent);
    public abstract float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid);
    public abstract float GetDismountResistance(Agent agent);
    public abstract float GetBreatheHoldMaxDuration(Agent agent, float baseBreatheHoldMaxDuration);
}
```
**Status:** ✅ Base class verified - Note the many abstract methods that must be implemented

### Remaining Verification Needed

The following patches/classes still need verification against ROT-Core's actual implementation:

1. **BasicCharacterObject.Deserialize** - XML deserialization patch
2. **Equipment.Deserialize** - Equipment loading patch
3. **MBInitialScreenBase** methods - UI screen lifecycle
4. **FightTournamentGame.GetTournamentPrize** - Tournament rewards
5. **PlayerEncounter.DoLootParty** - Looting behavior
6. **EncounterGameMenuBehavior** methods - Menu conditions

### Commands to Verify Additional Methods

```powershell
# Decompile specific TaleWorlds DLLs
$dll_path = "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"

# BasicCharacterObject deserialization
ilspycmd "$dll_path\TaleWorlds.Core.dll" -t "TaleWorlds.Core.BasicCharacterObject" 2>&1 | grep -A20 "Deserialize"

# Tournament prizes
ilspycmd "$dll_path\TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.TournamentGames.FightTournamentGame" 2>&1 | grep -A10 "GetTournamentPrize"

# Player encounter looting
ilspycmd "$dll_path\TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.PlayerEncounter" 2>&1 | grep -A20 "DoLootParty"
```
