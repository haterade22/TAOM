# Custom Culture Feats

## Overview

Each of TAOM's 10 custom cultures now has 3 unique cultural feats (2 bonuses + 1 penalty) that provide gameplay differentiation. This replaces the placeholder Empire feats that all cultures previously shared. Additionally, Dunland (XSLT culture) was reassigned from Empire feats to Battanian feats for better lore fit.

## Why This Exists

- **Vanilla behavior:** Each of Bannerlord's 6 cultures has 3 unique feats affecting economy, military, and production
- **TAOM requirement:** All 10 custom cultures used identical Empire feats — no gameplay differentiation between Erebor Dwarves and Mordor Orcs
- **Without this feature:** Every culture plays identically in terms of garrison wages, army costs, production bonuses

## Architecture

### Design Challenge

`FeatObject` is a sealed class — cannot be subclassed. Feats must be registered via `RegisterPresumedObject()` BEFORE `CultureObject.Deserialize()` reads XML. The vanilla `DefaultCulturalFeats` is created during `Campaign.InitializeDefaultCampaignObjects()` (private method).

### Solution Approach

1. **Harmony postfix** on `Campaign.InitializeDefaultCampaignObjects()` — registers 30 custom `FeatObject` instances right after vanilla feats
2. **GameModel overrides** — 9 new thin models + 2 extended existing models check `culture.HasFeat()` for custom feats
3. **XML assignment** — each culture's `<cultural_feats>` block references the custom feat string IDs

### Component Diagram

```
TaomCulturalFeats.cs (static singleton, 30 FeatObject instances)
        |
Harmony Postfix: Campaign.InitializeDefaultCampaignObjects()
        |
taom_spcultures.xml (<cultural_feats> references feat IDs)
        |
9 GameModel overrides (check culture.HasFeat() and apply bonuses)
```

## Configuration

### Config: `Main/_Module/ModuleData/taom_spcultures.xml`

Each culture has a `<cultural_feats>` block with 3 feat IDs:

```xml
<cultural_feats>
  <feat id="taom_erebor_garrison_wage" />
  <feat id="taom_erebor_animal_production" />
  <feat id="taom_erebor_construction_speed" />
</cultural_feats>
```

### All 59 Feats

| Culture | Feat ID | Effect | Bonus | Positive |
|---------|---------|--------|-------|----------|
| Erebor | `taom_erebor_garrison_wage` | -25% garrison wage | -0.25 | Yes |
| Erebor | `taom_erebor_production` | +10% all village production | 0.1 | Yes |
| Erebor | `taom_erebor_construction_speed` | -15% construction speed | -0.15 | No |
| Erebor | `taom_erebor_loyalty` | +1 settlement loyalty/day | 1.0 | Yes |
| Erebor | `taom_erebor_morale` | +5 party morale | 5.0 | Yes |
| Erebor | `taom_erebor_smithing` | -30% smithing energy cost | -0.3 | Yes |
| Rivendell | `taom_rivendell_army_influence` | +35% army influence award | 0.35 | Yes |
| Rivendell | `taom_rivendell_hearth_growth` | +20% village hearth growth | 0.2 | Yes |
| Rivendell | `taom_rivendell_army_influence_cost` | +25% army influence cost | 0.25 | No |
| Rivendell | `taom_rivendell_food_consumption` | -15% food consumption | -0.15 | Yes |
| Rivendell | `taom_rivendell_loyalty` | +0.5 settlement loyalty/day | 0.5 | Yes |
| Mirkwood | `taom_mirkwood_forest_speed` | -60% forest speed penalty | 0.6 | Yes |
| Mirkwood | `taom_mirkwood_militia_production` | +25% veteran militia chance | 0.25 | Yes |
| Mirkwood | `taom_mirkwood_hearth_growth` | -20% hearth growth | -0.2 | No |
| Mirkwood | `taom_mirkwood_food_consumption` | -15% food consumption | -0.15 | Yes |
| Mirkwood | `taom_mirkwood_morale` | +3 party morale | 3.0 | Yes |
| Lothlorien | `taom_lothlorien_forest_speed` | -50% forest speed penalty | 0.5 | Yes |
| Lothlorien | `taom_lothlorien_garrison_wage` | -20% garrison wage | -0.2 | Yes |
| Lothlorien | `taom_lothlorien_construction_speed` | -10% construction speed | -0.1 | No |
| Lothlorien | `taom_lothlorien_food_consumption` | -15% food consumption | -0.15 | Yes |
| Lothlorien | `taom_lothlorien_loyalty` | +0.5 settlement loyalty/day | 0.5 | Yes |
| Lothlorien | `taom_lothlorien_morale` | +3 party morale | 3.0 | Yes |
| Isengard | `taom_isengard_cheaper_recruits` | -15% mounted recruit cost | -0.15 | Yes |
| Isengard | `taom_isengard_garrison_wage` | -20% garrison wage | -0.2 | Yes |
| Isengard | `taom_isengard_decision_penalty` | +25% decision penalty | 0.25 | No |
| Isengard | `taom_isengard_party_size` | +20% party size | 0.2 | Yes |
| Isengard | `taom_isengard_construction_speed` | +15% construction speed | 0.15 | Yes |
| Isengard | `taom_isengard_smithing` | -20% smithing energy cost | -0.2 | Yes |
| Isengard | `taom_isengard_raid_damage` | +20% raid damage | 0.2 | Yes |
| Gundabad | `taom_gundabad_army_influence_cost` | -40% army influence cost | -0.4 | Yes |
| Gundabad | `taom_gundabad_grain_production` | +15% grain production | 0.15 | Yes |
| Gundabad | `taom_gundabad_wage` | +10% party wages | 0.1 | No |
| Gundabad | `taom_gundabad_party_size` | +30% party size | 0.3 | Yes |
| Gundabad | `taom_gundabad_raid_damage` | +25% raid damage | 0.25 | Yes |
| Umbar | `taom_umbar_cheaper_caravans` | -25% caravan cost | 0.75 | Yes |
| Umbar | `taom_umbar_renown` | +8% renown from battles | 0.08 | Yes |
| Umbar | `taom_umbar_wage` | +8% party wages | 0.08 | No |
| Umbar | `taom_umbar_tariff_income` | +15% tariff income | 0.15 | Yes |
| Dol Guldur | `taom_dolguldur_army_influence_cost` | -50% army influence cost | -0.5 | Yes |
| Dol Guldur | `taom_dolguldur_militia_production` | +20% veteran militia chance | 0.2 | Yes |
| Dol Guldur | `taom_dolguldur_construction_speed` | -20% construction speed | -0.2 | No |
| Dol Guldur | `taom_dolguldur_party_size` | +25% party size | 0.25 | Yes |
| Dol Guldur | `taom_dolguldur_food_consumption` | +10% food consumption | 0.1 | No |
| Gondor | `taom_gondor_garrison_wage` | -20% garrison wage | -0.2 | Yes |
| Gondor | `taom_gondor_army_influence` | +30% army influence award | 0.3 | Yes |
| Gondor | `taom_gondor_hearth_growth` | -15% hearth growth | -0.15 | No |
| Gondor | `taom_gondor_party_size` | +10% party size | 0.1 | Yes |
| Gondor | `taom_gondor_loyalty` | +1 settlement loyalty/day | 1.0 | Yes |
| Gondor | `taom_gondor_morale` | +5 party morale | 5.0 | Yes |
| Mordor | `taom_mordor_army_influence_cost` | -60% army influence cost | -0.6 | Yes |
| Mordor | `taom_mordor_grain_production` | +20% grain production | 0.2 | Yes |
| Mordor | `taom_mordor_wage` | +20% party wages | 0.2 | No |
| Mordor | `taom_mordor_party_size` | +30% party size | 0.3 | Yes |
| Mordor | `taom_mordor_raid_damage` | +25% raid damage | 0.25 | Yes |
| Rohan | `taom_rohan_mounted_cost` | -15% mounted recruit/upgrade cost | -0.15 | Yes |
| Rohan | `taom_rohan_mounted_wage` | -15% mounted troop wages | -0.15 | Yes |
| Rohan | `taom_rohan_infantry_speed` | -10% speed when >50% infantry | -0.1 | No |
| Rohan | `taom_rohan_loyalty` | +0.5 settlement loyalty/day | 0.5 | Yes |
| Rohan | `taom_rohan_morale` | +5 party morale | 5.0 | Yes |

### XSLT Cultures

| Culture | Feats Used | Rationale |
|---------|-----------|-----------|
| Dunland (empire) | Battanian feats | Hill tribe / forest guerrilla fighters |
| Harad (aserai) | Aserai feats (unchanged) | Desert traders, perfect lore match |
| Rohan (vlandia) | Custom C# feats | Horse-lords, mounted cost/wage reduction |
| Rhun (khuzait) | Khuzait feats (unchanged) | Steppe warriors, perfect match |
| Barding (sturgia) | Sturgian feats (unchanged) | Northern folk, perfect match |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CulturalFeats/TaomCulturalFeats.cs` | Feat registration + static accessors (59 feats) |
| `Main/Features/CulturalFeats/Hooks/Campaign_InitializeDefaultCampaignObjects_Patch.cs` | Harmony postfix for registration timing |
| `Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs` | Army influence award/cost feats |
| `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs` | Forest speed + Rohan infantry speed feats |
| `Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs` | Party size limit feats |
| `Main/Features/CulturalFeats/Models/TaomFoodConsumptionModel.cs` | Food consumption feats |
| `Main/Features/CulturalFeats/Models/TaomSettlementProsperityModel.cs` | Hearth growth feats |
| `Main/Features/CulturalFeats/Models/TaomSettlementMilitiaModel.cs` | Veteran militia feats |
| `Main/Features/CulturalFeats/Models/TaomBuildingConstructionModel.cs` | Construction speed feats |
| `Main/Features/CulturalFeats/Models/TaomVillageProductionModel.cs` | Production feats |
| `Main/Features/CulturalFeats/Models/TaomCaravanModel.cs` | Caravan cost feat |
| `Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs` | Renown feat |
| `Main/Features/CulturalFeats/Models/TaomPartyTroopUpgradeModel.cs` | Recruit cost feat |
| `Main/Features/CulturalFeats/Models/TaomSettlementLoyaltyModel.cs` | Settlement loyalty feats |
| `Main/Features/CulturalFeats/Models/TaomPartyMoraleModel.cs` | Party morale feats |
| `Main/Features/CulturalFeats/Models/TaomSmithingModel.cs` | Smithing energy cost feats |
| `Main/Features/CulturalFeats/Models/TaomClanFinanceModel.cs` | Tariff income feats |
| `Main/Features/CulturalFeats/Models/TaomRaidModel.cs` | Raid damage feats |
| `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs` | Wage/garrison feat checks (extended) |
| `Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs` | Decision penalty feat (extended) |
| `Main/_Module/ModuleData/taom_spcultures.xml` | Culture feat XML assignments |
| `Main/_Module/ModuleData/spcultures.xslt` | Dunland + Rohan feat overrides |

## Dependencies

- Harmony 2.x (patching `Campaign.InitializeDefaultCampaignObjects`)
- TaleWorlds.CampaignSystem.dll (`FeatObject`, `CultureObject`, `DefaultXxxModel` classes)

## Tests

| File | Coverage |
|------|----------|
| `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` | Feat property count, uniqueness, culture distribution, field structure |

64 tests total. GameModel overrides are thin entry points (delegate to `base` + apply feat modifier) and are verified via in-game testing.

## How-To

### Add a new feat to an existing culture

1. Add a private field + public static property in `TaomCulturalFeats.cs`
2. Register it in `RegisterAll()` and initialize in `InitializeAll()`
3. Add it to `GetAllFeats()` yield return list
4. Add the feat ID to the culture's `<cultural_feats>` block in `taom_spcultures.xml`
5. Add the `HasFeat()` check in the appropriate GameModel override
6. Update the test count in `AllFeatProperties_ReturnFeatObject_CountIs59`

### Add a new culture with feats

1. Follow steps above for 3 feats
2. Add `[DataRow]` entries in `FeatProperty_Exists_IsPublicStatic`
3. Add culture name to `EachCulture_HasExactly3Feats` cultures array
