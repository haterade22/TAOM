# Lord Banners

## Overview

Lords of TAOM cultures can now receive a banner item. A `BannerItemModel` override widens the vanilla culture
filter, so Gondor lords carry the empire banners, Erebor lords the sturgia banners, and every non-vanilla culture
can also take the neutral ones. A second override, `VassalRewardsModel`, gives a player who joins a TAOM-culture kingdom a level 2 banner too.

## Why This Exists

- **Vanilla behavior:** `DefaultBannerItemModel.GetPossibleRewardBannerItemsForHero(Hero)` takes every banner item
  except `campaign_banner_small` and keeps the ones where `item.Culture == null || item.Culture == hero.Culture`
  and the banner level equals `GetBannerItemLevelForHero(hero)` (1 for a lord, 2 for a clan leader, 3 for a
  ruling clan's leader). Its only reader is `BannerHelper.GetRandomBannerItemForHero`, on the give paths of
  `BannerCampaignBehavior`. Vanilla `SandBoxCore/ModuleData/items/banners.xml` has 45 items: 6 each for empire,
  aserai, vlandia, khuzait, sturgia and battania (2 effects at levels 1 to 3) and 9 for `neutral_culture`
  (3 effects at levels 1 to 3). No TAOM module adds banner items.
- **TAOM requirement:** a lord of a TAOM culture carries a banner like any other lord.
- **Without this feature:** no lord of a TAOM culture ever gets a banner item (#823), because no banner item
  belongs to a TAOM culture.

## Architecture

### Design Challenge

The engine's culture filter is an exact match and no banner item belongs to a TAOM culture. Widening it routes TAOM lords into vanilla's captain banner path (`GeneralsAndCaptainsAssignmentLogic`), which has no heraldry guard, hence the `banner_key` gate in `ShippedHeraldryBannerKeyTests`.

### Solution Approach

`TaomBannerItemModel : DefaultBannerItemModel` overrides only `GetPossibleRewardBannerItemsForHero` and delegates to
`LordBannerService.Allowed`. The service is generic over the item type with a projection to
(culture id, banner level), so it never names `ItemObject` (ADR-007); the model supplies the projection as a lambda
(an item without a banner component has level 0 and never matches). The level still comes from the base model.

The rule, for the hero's culture id C and level L: an item at level L is allowed when its culture is null, equals C,
or

- C is one of the six vanilla banner cultures and has no map row: nothing more, exactly the engine's rule
  (Rohan, Dale, Dunland, Harad, the Easterlings and the Variags are unchanged);
- any other culture, mapped or not: the item's culture is `neutral_culture` or listed in C's row.

A null culture id keeps only the culture-less items.

### Component Diagram

```
lord_banners/lord_banner_cultures.json
        |
  LordBannerCulturesConfigProvider (validates)
        |
  LordBannerService (the rule)  <--  projection lambda in the model (item -> culture, level)
        |
  TaomBannerItemModel (GetPossibleRewardBannerItemsForHero)
  TaomVassalRewardsModel (joining a kingdom): a level 2 draw from the same Allowed pool
```

## Configuration

### Config File: `Main/_Module/ModuleData/lord_banners/lord_banner_cultures.json`

```text
{ "cultures": { "<culture id>": ["<vanilla banner culture>", ...] } }
```

| Field | Type | Description |
|-------|------|-------------|
| `cultures` | object | Culture id to the vanilla banner cultures its lords may also carry: `empire`, `aserai`, `vlandia`, `khuzait`, `sturgia`, `battania`. `neutral_culture` is implicit for every row |

Keys and values are trimmed and case-insensitive. A missing, empty, null or unparseable file uses the built-in map
(equal to the shipped file). An empty key, a null list, an unknown value or a key that duplicates an earlier one
after normalizing drops the row with a warning (the first duplicate is kept; only keys that differ in case or whitespace are caught, an exact duplicate key keeps its last value in the JSON reader, and the shipped-config equality test guards the shipped file), and any rejection adds one summary
warning. Repeated values in one row collapse silently.

### Current Values

| Banner culture | Cultures | Effects (levels 1 to 3) |
|----------------|----------|-------------------------|
| `empire` | gondor, gondor_soldiers, arthedain, cardolan, rhudaur | less morale shock, less ranged damage taken |
| `sturgia` | erebor, erebor_warriors, eredluin, anduinvale, angaladh, dorwinion | less shield damage, less ranged damage taken |
| `vlandia` | southrhovanion | melee damage against mounted, charge damage |
| `battania` | mirkwood, mirkwood_stalkers, dunland_raiders | troop speed, melee damage against mounted |
| `aserai` | umbar, umbar_corsairs, abanissa, shaghana, harad_raiders | ranged accuracy, troop speed |
| `khuzait` | rhun_raiders | ranged accuracy, mount speed |
| `neutral_culture` (every non-vanilla culture) | all | more ranged damage, more melee damage, less melee damage taken |

Elves, orcs and creatures have no row: neutral banners only.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LordBanners/LordBannerService.cs` | The rule |
| `Main/Features/LordBanners/Models/TaomBannerItemModel.cs` | Engine hook, one delegate |
| `Main/Features/LordBanners/Models/TaomVassalRewardsModel.cs`, `VassalBannerReward.cs` | The joining-kingdom reward: one-line override and its boundary helper |
| `Main/Features/LordBanners/LordBannerCulturesConfigProvider.cs` | Validating loader, plus `ILordBannerCulturesConfigProvider.cs` and `LordBannerMap.cs` (map and built-in default) |
| `Main/Features/LordBanners/LordBannersModule.cs` | Registration and model declaration |
| `Main/_Module/ModuleData/lord_banners/lord_banner_cultures.json` | The map |

## Dependencies

- `IPathService`, `IModLogger` (Core): config path and logging

## Tests

- `TAOM.Tests/Features/LordBanners/LordBannerServiceTests.cs`: the decision matrix
- `TAOM.Tests/Features/LordBanners/LordBannerCulturesConfigProviderTests.cs`: one test per validation rule
- `TAOM.Tests/Features/LordBanners/ShippedLordBannerCulturesConfigTests.cs`: the shipped file parses clean, equals the built-in map, names real culture ids; with the game installed, every culture that has a lord in repo ModuleData gets a non-empty pool at levels 1 to 3 (inconclusive without the install)
- `TAOM.Tests/Features/LordBanners/ShippedHeraldryBannerKeyTests.cs`: every clan and kingdom `banner_key` in `clans.xml`, `taom_spkingdoms.xml`, `spclans.xslt` and `spkingdoms.xslt` is whole groups of ten integers; since #823 vanilla's captain path banners TAOM lord-led formations without TAOM's heraldry guard
- `tools/tests/test_banner_icon_ids.py` (#951): the icon and colour ids those keys name (and `taom_spcultures.xml`'s `faction_banner_key`) exist in Native's or TAOM's `banner_icons.xml`; an unknown icon draws as nothing
- `TAOM.Tests/Features/LordBanners/LordBannersWiringTests.cs`: module listing, no patch or behavior, the two model declarations, and IL scans that the vassal override calls the base then the helper with no branch and that the helper checks the roster, draws from `Allowed`, then adds one banner

## Consequences

- Joining a kingdom: `DefaultVassalRewardsModel.GetEquipmentRewardsForJoiningKingdom` picks a level 2 banner of the kingdom's culture, which never matched a TAOM culture. Closed by #933: `TaomVassalRewardsModel` keeps the base roster and, only when it holds no banner item, adds one drawn with the engine's own `GetRandomElement` from `LordBannerService.Allowed` at level 2 for the kingdom's culture. A vanilla culture's kingdom gets exactly vanilla's result. The reward is given once per campaign behind vanilla's saved `_receivedVassalRewards` flag, so a save whose player already swore fealty gets none. Under the co-op mod the client calls the model.
- A lord-captained formation now flies the lord's own banner, level 1 to 3 by rank. `GeneralsAndCaptainsAssignmentLogic`
  uses the captain's `FormationBanner`, which comes from `Hero.BannerItem` (`BattleEquipment[ExtraWeaponSlot]`),
  instead of the level 1 culture banner from the BannerBearers feature ([banner-bearers.md](banner-bearers.md)).
- Existing saves: every eligible lord gets a banner as soon as the save finishes loading (`BannerCampaignBehavior.GiveBannersToHeroes` on `OnGameLoadFinishedEvent`, no roll, prisoners included). A hero who comes of age, a new lord and a companion who founds a clan get one at once (`HeroComesOfAgeEvent`, `HeroCreated`, `OnClanCreated`). A lord who loses his banner, or one the other paths missed, gets one through the daily tick (25 percent per day, never while a prisoner). Under the co-op mod only the host gives banners, `Hero.BannerItem` is synced, and the load-time give is blocked, so in a co-op save lords get their banners through the daily tick.
- Defeated TAOM lords can drop their banner as loot.
- Two writers clear a lord's banner: vanilla's ruling-clan kit change (`NPCEquipmentsCampaignBehavior.OnRulingClanChanged` writes all 12 slots; the daily tick gives one again) and the Hard Questions strip (it takes the banner with the kit; given again after release).
- BannerBearers keys on a formation's majority troop culture, and the lords' own parties fielded `rivendell` and `goblin` troops, so Lindon and Blue Craig lord parties flew those banners (Lindon's templates name Lindon troops since #1039). Their own-culture troops (Lindon militia, garrisons and volunteers, and `bluecraig_bolgs_ironfang`) had no `CultureBanners` row. Closed by #932: `lindon` takes the elves' flag, `bluecraig` the orcs' standard, and `ShippedConfig_MapsEveryInfantryTroopCultureToABanner` keeps every fielded infantry troop culture mapped.

## How to add a culture

1. Add the culture to `taom_spcultures.xml` and a lord for it.
2. Optionally add a row to `lord_banner_cultures.json` and to `LordBannerMap.Default()`. Without a row the culture
   gets the neutral banners.
3. Add the culture to the BannerBearers `CultureBanners` map (`banner_bearers_config.json` and the compiled defaults), or its troop formations raise no banner; the gate above fails until it is there.

## In-game check

Load a save, then fight a battle against or alongside a Gondor, Erebor or Mordor lord and see the bearer carry a level 1 to 3 banner (the equipment screen hides the banner slot). A clan leader carries level 2, a ruler level 3. Also run a siege with a TAOM lord defending alongside a garrison, past deployment and one reinforcement wave. Start a new campaign and swear fealty to a Gondor kingdom and to a Rohan kingdom: the Gondor reward holds a level 2 banner, and the Rohan reward is vanilla's.

## Changelog

- 2026-10-19: added (#823); joining-kingdom banner reward (#933).

## GitHub Issue

- **Issue:** #823 Lords of custom cultures never receive a banner item
- **Issue:** #933 Joining a TAOM-culture kingdom gives no banner reward
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/banner-bearers.md](./banner-bearers.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
