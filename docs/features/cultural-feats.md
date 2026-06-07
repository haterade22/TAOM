# Custom Culture Feats

## Overview

Each of TAOM's 10 custom cultures now has 3 unique cultural feats (2 bonuses + 1 penalty) that provide gameplay differentiation. This replaces the placeholder Empire feats that all cultures previously shared. Additionally, Dunland (XSLT culture) was reassigned from Empire feats to Battanian feats for better lore fit.

On top of the base feats, **terrain movement-speed feats** give 18 cultures a flat party-speed bonus on their "home" terrain (forest / snow / steppe / desert / plain / swamp) plus a night bonus for Mordor — see the [Terrain Movement-Speed Feats](#terrain-movement-speed-feats) section. **Village volunteer respawn-rate feats** (4 cultures) and **per-settlement notable-count feats** layer on top — see [Village Volunteer Respawn-Rate Feats](#village-volunteer-respawn-rate-feats) and [Per-Settlement Notable-Count Feats](#per-settlement-notable-count-feats). The feat total is now **129**: 97 (the original 59 base + 18 terrain + 3 party-size + 4 volunteer-respawn + 4 village notable + 9 per-occupation town notable) **+ 8 new-culture feats** (Goblin ×4 + Misty Mountain Orcs ×4, both playable cultures added 2026-05/06) **+ 24 Wave 1 expansion feats** (2026-06-07 — economy/raid/morale/loyalty/smithing/food breadth for thinly-covered cultures: Dale, Khand, Harad, Rhûn, Umbar, Mordor, Erebor, Lothlorien, Mirkwood, Goblin, Misty Mountain Orcs — all plug into existing `CulturalFeatsService.Apply*` methods, no new GameModels).

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

### Base Culture Feats (59)

> The two forest feats below (`taom_mirkwood_forest_speed`, `taom_lothlorien_forest_speed`) were reworked into the terrain system — they now apply a **flat +10%** in forest instead of scaling the vanilla forest penalty. See [Terrain Movement-Speed Feats](#terrain-movement-speed-feats).


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
| Mirkwood | `taom_mirkwood_forest_speed` | +10% movement speed in forest (reworked from -60% penalty reduction) | 0.1 | Yes |
| Mirkwood | `taom_mirkwood_militia_production` | +25% veteran militia chance | 0.25 | Yes |
| Mirkwood | `taom_mirkwood_hearth_growth` | -20% hearth growth | -0.2 | No |
| Mirkwood | `taom_mirkwood_food_consumption` | -15% food consumption | -0.15 | Yes |
| Mirkwood | `taom_mirkwood_morale` | +3 party morale | 3.0 | Yes |
| Lothlorien | `taom_lothlorien_forest_speed` | +10% movement speed in forest (reworked from -50% penalty reduction) | 0.1 | Yes |
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
| Gundabad | `taom_gundabad_party_size` | +20% party size (retuned 2026-05-31; was +30%) | 0.2 | Yes |
| Gundabad | `taom_gundabad_raid_damage` | +25% raid damage | 0.25 | Yes |
| Umbar | `taom_umbar_cheaper_caravans` | -25% caravan cost | 0.75 | Yes |
| Umbar | `taom_umbar_renown` | +8% renown from battles | 0.08 | Yes |
| Umbar | `taom_umbar_wage` | +8% party wages | 0.08 | No |
| Umbar | `taom_umbar_tariff_income` | +15% tariff income | 0.15 | Yes |
| Dol Guldur | `taom_dolguldur_army_influence_cost` | -50% army influence cost | -0.5 | Yes |
| Dol Guldur | `taom_dolguldur_militia_production` | +20% veteran militia chance | 0.2 | Yes |
| Dol Guldur | `taom_dolguldur_construction_speed` | -20% construction speed | -0.2 | No |
| Dol Guldur | `taom_dolguldur_party_size` | +20% party size (retuned 2026-05-31; was +25%) | 0.2 | Yes |
| Dol Guldur | `taom_dolguldur_food_consumption` | +10% food consumption | 0.1 | No |
| Gondor | `taom_gondor_garrison_wage` | -20% garrison wage | -0.2 | Yes |
| Gondor | `taom_gondor_army_influence` | +30% army influence award | 0.3 | Yes |
| Gondor | `taom_gondor_hearth_growth` | -15% hearth growth | -0.15 | No |
| Gondor | `taom_gondor_party_size` | +2.5% party size (retuned 2026-05-31; was +10%) | 0.025 | Yes |
| Gondor | `taom_gondor_loyalty` | +1 settlement loyalty/day | 1.0 | Yes |
| Gondor | `taom_gondor_morale` | +5 party morale | 5.0 | Yes |
| Mordor | `taom_mordor_army_influence_cost` | -60% army influence cost | -0.6 | Yes |
| Mordor | `taom_mordor_grain_production` | +20% grain production | 0.2 | Yes |
| Mordor | `taom_mordor_wage` | +20% party wages | 0.2 | No |
| Mordor | `taom_mordor_party_size` | +10% party size (retuned 2026-05-31; was +30%) | 0.1 | Yes |
| Mordor | `taom_mordor_raid_damage` | +25% raid damage | 0.25 | Yes |
| Rohan | `taom_rohan_mounted_cost` | -15% mounted recruit/upgrade cost | -0.15 | Yes |
| Rohan | `taom_rohan_mounted_wage` | -15% mounted troop wages | -0.15 | Yes |
| Rohan | `taom_rohan_infantry_speed` | -10% speed when >50% infantry | -0.1 | No |
| Rohan | `taom_rohan_loyalty` | +0.5 settlement loyalty/day | 0.5 | Yes |
| Rohan | `taom_rohan_morale` | +5 party morale | 5.0 | Yes |

### Terrain Movement-Speed Feats

18 cultures gain a flat party movement-speed `AddFactor` bonus while on their "home" terrain. The bonus **stacks on top of** vanilla's terrain modifiers (e.g. vanilla forest is -30%, desert -10%, night -25%). Terrain is read each speed recalc in `TaomPartySpeedModel`, mapped from the sealed `TerrainType` to the TAOM-owned `TerrainKind` enum at the boundary (`TerrainType.Dune` folds into `Desert`); the Mordor night bonus keys off `Campaign.Current.IsNight` and is terrain-independent.

| Terrain | Cultures (culture StringId) | Feat IDs | Bonus |
|---------|------------------------------|----------|-------|
| Forest | Mirkwood, Lothlorien, Rivendell | `taom_mirkwood_forest_speed`, `taom_lothlorien_forest_speed`, `taom_rivendell_forest_speed` | +10% |
| Snow | Erebor (Dwarves), Gundabad | `taom_erebor_snow_speed`, `taom_gundabad_snow_speed` | +10% |
| Steppe | Khand (battania), Rhûn (khuzait) | `taom_khand_steppe_speed`, `taom_rhun_steppe_speed` | +10% |
| Desert | Umbar, Harad (aserai), Shaghâna, Âbanissa | `taom_umbar_desert_speed`, `taom_harad_desert_speed`, `taom_shaghana_desert_speed`, `taom_abanissa_desert_speed` | +10% |
| Plain | Gondor, Rohan (vlandia), Dale (sturgia), Dunland (empire), Isengard | `taom_gondor_plain_speed`, `taom_rohan_plain_speed`, `taom_dale_plain_speed`, `taom_dunland_plain_speed`, `taom_isengard_plain_speed` | +10% |
| Plain | Mordor | `taom_mordor_plain_speed` | **+5%** |
| Swamp | Isengard | `taom_isengard_swamp_speed` | +10% |
| Swamp | Mordor | `taom_mordor_swamp_speed` | **+5%** |
| Night (any terrain) | Mordor | `taom_mordor_night_speed` | +10% |

**Mordor** deliberately gets a smaller terrain buff (+5%) offset by its unique night bonus (+10%). The elven forest feats were unified to a flat +10% (previously Mirkwood ~+18% / Lothlorien ~+15% net via penalty reduction).

**Note on `TerrainType.Snow` (terrain, not weather — intentional):** the snow bonus keys off the *terrain* type returned by `GetFaceTerrainType`, not snowy *weather*. Vanilla's snow *slowdown* is weather-derived (`MapWeatherModel` Snowy/Blizzard → synthesised `TerrainType.Snow` + −10%), so on the *vanilla* map `GetFaceTerrainType` rarely returns `Snow`. **On the TAOM map this is not a problem:** the `TAOM_Map` navmesh faces around the snowy regions are author-painted with terrain id `3` (= `TerrainType.Snow`), so `GetFaceTerrainType` returns `Snow` there and the Erebor/Gundabad bonus fires as intended. Keep the snow check terrain-based (do not switch it to weather detection) — it matches how the custom map is authored.

### Village Volunteer Respawn-Rate Feats

Four cultures gain a flat `AddFactor` bonus on the per-notable daily volunteer-production probability returned by `DefaultVolunteerModel.GetDailyVolunteerProductionProbability(Hero notable, int index, Settlement settlement)`. The vanilla value (typically 0.7–0.95) is wrapped in `ExplainedNumber`, our factor is added, and the result is clamped to `[0,1]`. Keyed on `settlement.OwnerClan?.Culture` — economic/recruitment effects follow ownership (a Mordor village produces faster while Mordor owns it; conquest by another culture removes the bonus on the next daily tick). Matches how `TaomSettlementMilitiaModel` resolves the same trade-off.

No vanilla culture has a volunteer-rate feat to mirror, so this is a brand-new hook site. The `ExplainedNumber + AddFactor` pattern matches how vanilla itself applies the Cantons kingdom policy and the CavalryTactics perk inside the same `DefaultVolunteerModel` method.

| Culture (StringId) | Feat ID | Bonus |
|--------------------|---------|-------|
| Dunland (`empire`) | `taom_dunland_volunteer_rate` | +10% |
| Gundabad (`gundabad`) | `taom_gundabad_volunteer_rate` | +20% |
| Dol Guldur (`dolguldur`) | `taom_dolguldur_volunteer_rate` | +20% |
| Mordor (`mordor`) | `taom_mordor_volunteer_rate` | +20% |

### Per-Settlement Notable-Count Feats

`TaomNotableSpawnModel : DefaultNotableSpawnModel` overrides `GetTargetNotableCountForSettlement(Settlement, Occupation)`. Maps the sealed TaleWorlds `Occupation` to TAOM-owned `NotableOccupationKind` at the boundary (ADR-007). Keyed on `settlement.Culture` (settlement identity, NOT `OwnerClan.Culture` — an Isengard town stays Isengard-flavored even when conquered).

Vanilla totals: **town = 5 notables** (2 Merchant + 1 Artisan + 2 Gang Leader), **village = 3** (2 RuralNotable + 1 Headman).

**Town uses per-occupation `AddType.Add` feats** (flat extras above vanilla base) so each culture can tune Merchant / Artisan / Gang Leader independently. A uniform per-(culture, town) multiplier could only ever scale all three together, which collapsed at small bases (`ceil(2 × 1.05)` = `ceil(2 × 1.50)` = 3 for both Merchants and Gang Leaders) and prevented asymmetric distributions like Isengard's "few Merchants, many Gang Leaders." Village stays on the per-(culture, village) `AddFactor` model since the user spec doesn't differentiate village occupations.

**Town target distributions:**

| Culture | Merchant | Artisan | Gang Leader | Total | Rationale |
|---------|----------|---------|-------------|-------|-----------|
| Vanilla | 2 | 1 | 2 | **5** | baseline |
| Mordor | 2 | 1 | 4 (+2) | **7** | modest, evil-but-coordinated |
| Gundabad | 2 | 2 (+1) | 5 (+3) | **9** | growing horde |
| **Isengard** | 4 (+2) | 2 (+1) | **14 (+12)** | **20** | Isengard has only 1 town; recruitment hub vs Rohan's distributed map |
| **Dol Guldur** | 3 (+1) | 2 (+1) | **15 (+13)** | **20** | shadow command center |

**Town feats (Add semantics)** — only registered when non-zero:

| Culture | Merchant | Artisan | Gang Leader |
|---------|----------|---------|-------------|
| Isengard | `taom_isengard_notable_count_town_merchant` (+2) | `..._artisan` (+1) | `..._gang_leader` (+12) |
| Dol Guldur | `taom_dolguldur_notable_count_town_merchant` (+1) | `..._artisan` (+1) | `..._gang_leader` (+13) |
| Mordor | — | — | `taom_mordor_notable_count_town_gang_leader` (+2) |
| Gundabad | — | `taom_gundabad_notable_count_town_artisan` (+1) | `taom_gundabad_notable_count_town_gang_leader` (+3) |

**Village feats (legacy AddFactor + ceiling)** — `taom_{culture}_notable_count_village` at 10% (Isengard/DolGuldur/Gundabad) or 5% (Mordor); all currently produce **3 RuralNotable + 2 Headman = 5** since `ceil(2 × 1.05)` = `ceil(2 × 1.10)` = 3 and `ceil(1 × 1.05)` = `ceil(1 × 1.10)` = 2.

**Template-count expansion for Gang Leaders:** the 14-Isengard and 15-Dol Guldur targets exceed each culture's vanilla 6 Gang Leader templates; without more, the engine clones the same archetype multiple times. Authored 8 new Isengard Gang Leaders (`spc_notable_isengard_gl5` … `gl12`) and 9 new Dol Guldur Gang Leaders (`spc_notable_dolguldur_gl5` … `gl13`), all defined in `characters/npcs_*.xml` AND registered in the culture's `<notable_templates>` block in `taom_spcultures.xml` (the two-layer registration rule). Mordor and Gundabad targets stay within their existing 6 GL templates.

**Known characteristic — duplicate archetype selection at target = pool size.** Vanilla `DefaultHeroCreationModel.GetRandomTemplateByOccupation` samples templates from `culture.NotableTemplates` *with replacement* (it filters by occupation, then weighted-random picks without removing the selected template from the pool). For Isengard (14 GL templates, target 14) and Dol Guldur (15 GL templates, target 15), this means each town's authored GL roster will have expected `≈ N × (1 − (1 − 1/N)^N) ≈ 0.632 N` distinct archetypes — roughly 9 distinct of 14 (Isengard) and 9.5 distinct of 15 (Dol Guldur), with the remaining slots being duplicate names/portraits. This is intentional: the design goal was AI-recruitment density (Rohan-tier town hubs), not encyclopedia name diversity. Adding pool headroom (e.g., 20 templates for 14 targets) or patching vanilla to a no-replacement selector would mitigate, but each costs authoring effort or a Harmony patch on a hot path. Codex flagged this MEDIUM on 2026-05-31 (see `docs/reviews/codex-adversarial-cultural-feats-per-occupation-2026-05-31.md`).

**Why these four cultures also needed a 3rd RuralNotable template** (`spc_notable_{isengard,mordor,dolguldur,gundabad}_23`): with +10% village notable count the RuralNotable target ceils from 2 → 3, but each culture had only 2 RuralNotable templates. Without the 3rd, the engine reuses one — same name, same archetype, twice. The new template duplicates `_22`'s structure with a distinct LOTR-flavored name. This extends the `.claude/rules/xml-data.md` convention ("Rural Notables (2)") to **3 for these four cultures only** — every other culture still has 2.

### XSLT Cultures

The six vanilla-wrapped cultures get their terrain feat appended to their `<cultural_feats>` in `spcultures.xslt`. Dunland/Rohan already had a TAOM override block (feat added inline); Harad/Rhûn/Dale/Khand pass vanilla feats through, so a dedicated `Culture[@id='X']/cultural_feats` template copies the vanilla feats and appends the TAOM feat (preserving vanilla bonuses).

| Culture | Feats Used | Terrain feat appended |
|---------|-----------|-----------------------|
| Dunland (empire) | Battanian feats (inline override) | `taom_dunland_plain_speed` |
| Rohan (vlandia) | Custom C# feats (inline override) | `taom_rohan_plain_speed` |
| Harad (aserai) | Aserai feats (unchanged) | `taom_harad_desert_speed` |
| Rhûn (khuzait) | Khuzait feats (unchanged) | `taom_rhun_steppe_speed` |
| Dale (sturgia) | Sturgian feats (unchanged) | `taom_dale_plain_speed` |
| Khand (battania) | Battanian feats (unchanged) | `taom_khand_steppe_speed` |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CulturalFeats/TaomCulturalFeats.cs` | Feat registration + static accessors (77 feats) |
| `Main/Features/CulturalFeats/TerrainKind.cs` | TAOM-owned terrain enum (boundary type for the speed model → service, ADR-007) |
| `Main/Features/CulturalFeats/ICulturalFeatsService.cs` / `CulturalFeatsService.cs` | Per-feat dispatch incl. `ApplyTerrainSpeedFeats` |
| `Main/Features/CulturalFeats/Hooks/Campaign_InitializeDefaultCampaignObjects_Patch.cs` | Harmony postfix for registration timing |
| `Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs` | Army influence award/cost feats |
| `Main/Features/CulturalFeats/Models/TaomPartySpeedModel.cs` | Terrain movement-speed feats + night + Rohan infantry speed |
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
| `Main/_Module/ModuleData/taom_spcultures.xml` | Culture feat XML assignments (custom cultures) |
| `Main/_Module/ModuleData/spcultures.xslt` | Vanilla-wrapped culture feat overrides + terrain-feat append templates |

## Dependencies

- Harmony 2.x (patching `Campaign.InitializeDefaultCampaignObjects`)
- TaleWorlds.CampaignSystem.dll (`FeatObject`, `CultureObject`, `DefaultXxxModel` classes)

## Tests

| File | Coverage |
|------|----------|
| `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` | Feat property count (97), uniqueness, culture distribution, field structure |
| `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` | Per-feat dispatch incl. terrain-speed (per-terrain match, Mordor 5% vs 10%, night, null/wrong-terrain no-ops) |

GameModel overrides are thin entry points (delegate to `base` + apply feat modifier via the service) and are verified via in-game testing. The `TaomPartySpeedModel.MapTerrain` boundary mapping is verified in-game (it consumes the sealed `TerrainType`).

## How-To

### Add a new feat to an existing culture

1. Add a private field + public static property in `TaomCulturalFeats.cs`
2. Register it in `RegisterAll()` and initialize in `InitializeAll()`
3. Add it to `GetAllFeats()` yield return list
4. Add the feat ID to the culture's `<cultural_feats>` block in `taom_spcultures.xml` (custom cultures) or `spcultures.xslt` (vanilla-wrapped cultures)
5. Add the `HasFeat()` check in the appropriate GameModel override / service method
6. Update the feat count in `AllFeatProperties_ReturnFeatObject_CountIs77`, `RegisterAll_UsesCorrectStringIds`, and `GetAllFeats_YieldsZeroOrFullSet`; add a `[DataRow]` to `FeatProperty_Exists_IsPublicStatic`; bump the culture entry in `EachCulture_HasExpectedFeatCount`. If the feat sets an `EffectBonus` read in a service test, add it to the reflection table in `CulturalFeatsServiceTests.EnsureFeatsInitialised`.
7. **Update the CC faction-map page.** Edit the matching faction's entry in [`Main/_Module/ModuleData/factionmap/factions.json`](../../Main/_Module/ModuleData/factionmap/factions.json) — add the feat to `perks[]` (lore-named flagship positives), `bonuses[]` (concrete game-effect line with the correct `positive: true|false` flag), and `weaknesses[]` (if it's a real negative — Rohan infantry penalty, Isengard relationship +25%, Mordor wages +20%, etc.). Without this, the player's starting-culture page in CC silently lies about what shipped. Standing instruction per `feedback_faction_map_update_with_cultural_feats.md`. The JSON ↔ culture-StringId mapping is in that memory file (e.g., `stewardship_of_gondor` → `gondor`, `kingdom_of_rohan` → `vlandia`).

### Add a new terrain movement-speed feat

1. Add field + accessor + `Register` + `Initialize` (flat `AddFactor` bonus) + `GetAllFeats` yield in `TaomCulturalFeats.cs`
2. Add an `ApplyIfHas(...)` line to the matching `case` in `CulturalFeatsService.ApplyTerrainSpeedFeats`
3. Add the feat ID to the culture's `<cultural_feats>` (XML or XSLT per above)
4. Update tests as in step 6 above

### Add a new culture with feats

1. Follow steps above for the feats
2. Add `[DataRow]` entries in `FeatProperty_Exists_IsPublicStatic`
3. Add the culture entry to `EachCulture_HasExpectedFeatCount`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
