# Startup Resources — Culture-Based Gold & Influence + Player Starting Gold + CC Equipment

## Overview

Distributes starting gold to individual Lord heroes and starting influence to clans at new game creation, with amounts configured per culture via XML. The same config file also drives the player's culture-based starting funds, granted at character-creation finalize. A sister feature in `Main/Features/CharacterCreation/` persists each youth option's equipment roster onto the player hero so the equipment shown in the CC preview is what the player actually walks into the campaign with. Together these establish faction-appropriate economic and equipment baselines reflecting Middle-earth power dynamics.

## Why This Exists

- **Vanilla behavior:** All factions start with identical default gold/influence regardless of lore
- **TAOM requirement:** Elven factions (ancient, wealthy) should start rich; Orcish warchest factions (Isengard, Gundabad) need military funding; Human kingdoms start modest
- **Without this feature:** Economic parity breaks immersion — Rivendell starts as poor as a human frontier settlement

## Architecture

### Design Challenge

Gold must target individual Lord heroes (TaleWorlds' `GiveGoldAction` operates on `Hero`), while influence targets clans (set via `Clan.Influence`). Both use the same trigger and culture-based config, so they share a feature but have separate services and adapters.

### Solution Approach

Single `CampaignBehavior` registers on `OnNewGameCreatedPartialFollowUpEvent` at index 1 (after InitialChildGeneration at index 0). Delegates to two services, each with their own adapter. An idempotency flag prevents double-distribution.

### Component Diagram

```
startup_resources_config.xml
        |
  ConfigProvider (XDocument, cached)
       / \
      /   \
GoldService    InfluenceService
    |               |
StartupHero    ClanStartup
Adapter        Adapter
    |               |
GoldGift       clan.Influence
Adapter            +=
    |
GiveGoldAction
```

## Configuration

### Config File: `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml`

Each `<Culture>` element maps a culture ID to gold (per Lord hero) and influence (per clan).

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | string | Culture string ID (case-insensitive match) |
| `gold` | int | Gold given to each alive Lord hero in this culture (0 = skip). Player clan is excluded — see `playerGold`. |
| `influence` | float | Influence added to each eligible clan in this culture (0 = skip) |
| `playerGold` | int | Gold given to the **player hero** at CC finalize. Range `[0, 10_000_000]`; out-of-range or non-numeric values revert to 0 with a logged warning. Missing attribute defaults to 0 (no warning). |

### Current Values

NPC-lord gold/influence values are tuning knobs and may drift; consult `startup_resources_config.xml` for the live values. The table below reflects the live `playerGold` values after the 2026-06-30 downward rebalance.

| Culture | playerGold | Rationale |
|---------|-----------|-----------|
| rivendell, lothlorien, mirkwood | 4,000 | Elven wealth — the highest player start |
| erebor | 3,500 | Dwarven hoard culture, just below the elves |
| all other cultures — gondor, vlandia (Rohan), sturgia (Dale), empire (Dunland), battania (Khand), aserai (Harad), khuzait (Rhun), shaghana, abanissa, mordor, isengard, gundabad, dolguldur, umbar | 2,000 | Flat baseline for every human + orc culture |

`shaghana` (eastern Harad reach) and `abanissa` (deep south Harad) are independent Harad-region kingdoms — full peers of Aserai with their own NPC clans, lords, and ruler titles (Taskral / Châjaphân), not sub-cultures — and are now selectable in character creation, so their `playerGold` is live.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StartupResources/StartupResourcesBehavior.cs` | CampaignBehavior — fires at index 1, delegates to both services |
| `Main/Features/StartupResources/StartupGoldService.cs` | Iterates Lord heroes, gives culture-based gold |
| `Main/Features/StartupResources/IStartupGoldService.cs` | Service interface |
| `Main/Features/StartupResources/StartupInfluenceService.cs` | Iterates eligible clans, adds culture-based influence |
| `Main/Features/StartupResources/IStartupInfluenceService.cs` | Service interface |
| `Main/Features/StartupResources/StartupResourcesConfigProvider.cs` | XDocument XML parser with caching |
| `Main/Features/StartupResources/IStartupResourcesConfigProvider.cs` | Config provider interface |
| `Main/Features/StartupResources/Config/StartupResourcesConfig.cs` | Config POCOs |
| `Main/Features/StartupResources/StartupResourcesIoC.cs` | DryIoc registration |
| `Main/Features/StartupResources/IPlayerStartupGoldService.cs` | Interface — `GrantPlayerStartupGold(cultureId, playerHeroId)` |
| `Main/Features/StartupResources/PlayerStartupGoldService.cs` | Looks up `PlayerGold` from config; calls `IGoldGiftAdapter.GiveGoldToHero` |
| `Main/Adapters/IPlayerEquipmentAdapter.cs` | Interface — `ApplyRosterToPlayer(rosterId, playerHeroId)` returning a `PlayerEquipmentApplyResult` |
| `Main/Adapters/PlayerEquipmentAdapter.cs` | Wraps `MBEquipmentRoster.AllEquipments` filter + `Hero.BattleEquipment.FillFrom` / `CivilianEquipment.FillFrom` |
| `Main/Features/CharacterCreation/IPlayerEquipmentService.cs` | Interface — `ApplyPlayerStartingEquipment(cultureId, titleType, isFemale, playerHeroId)` |
| `Main/Features/CharacterCreation/PlayerEquipmentService.cs` | Builds roster ID, delegates to adapter, logs each result |
| `Main/Features/CharacterCreation/PlayerEquipmentRosterIds.cs` | Shared roster-ID helper: `player_char_creation_{culture}_{titleType}_{m\|f}` (consumed by NarrativeMenuBuilder + PlayerEquipmentService) |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Calls both new services from `OnCharacterCreationFinalize` after `AssignCareer` |
| `Main/Adapters/IStartupHeroAdapter.cs` | Interface — `GetAliveLordHeroes()` |
| `Main/Adapters/StartupHeroAdapter.cs` | Wraps `Hero.AllAliveHeroes`, filters `Occupation.Lord` |
| `Main/Adapters/IGoldGiftAdapter.cs` | Interface — `GiveGoldToHero(heroId, amount)` |
| `Main/Adapters/GoldGiftAdapter.cs` | Wraps `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, true)` |
| `Main/Adapters/IClanStartupAdapter.cs` | Interface — `GetEligibleClans()`, `AddInfluence()` |
| `Main/Adapters/ClanStartupAdapter.cs` | Wraps `Clan.All` filtering + `clan.Influence +=` |
| `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` | Culture gold/influence values |

## Dependencies

- `IPathService` (Core) — resolves `ModuleDataPath` for config file location
- `IModLogger` (Core) — logging
- `IStartupHeroAdapter` (Adapters) — wraps `Hero.AllAliveHeroes` + `Occupation.Lord` filter
- `IGoldGiftAdapter` (Adapters) — wraps `GiveGoldAction.ApplyBetweenCharacters`
- `IClanStartupAdapter` (Adapters) — wraps `Clan.All` filtering + `Clan.Influence` setter

## Tests

- `TAOM.Tests/Features/StartupResources/StartupResourcesConfigProviderTests.cs` — 11 tests: valid XML parsing, missing file, malformed XML, caching, decimal influence, missing attributes, `playerGold` happy-path parse, negative `playerGold` rejected, over-cap rejected, non-numeric rejected, missing-attribute defaults to 0 silently
- `TAOM.Tests/Features/StartupResources/StartupGoldServiceTests.cs` — 8 tests: culture match, player skip, missing culture, multiple lords, zero gold, case-insensitive, no heroes, logging
- `TAOM.Tests/Features/StartupResources/PlayerStartupGoldServiceTests.cs` — 8 tests: configured culture grant, case-insensitive culture match, unknown culture warns, zero `playerGold` skip, null/empty culture no-op, null hero ID no-op, info-log includes amount + culture
- `TAOM.Tests/Features/StartupResources/StartupInfluenceServiceTests.cs` — 6 tests: culture match, missing culture, multiple clans, zero influence, no clans, logging
- `TAOM.Tests/Features/StartupResources/StartupResourcesBehaviorTests.cs` — 4 tests: index 1 triggers, index 0/2 skip, idempotency guard
- `TAOM.Tests/Features/CharacterCreation/PlayerEquipmentServiceTests.cs` — 9 tests: male/female roster-ID format, null/empty input no-ops, RosterNotFound / NoSuitableEquipment / HeroNotFound result handling, success info-log

## How to Add or Adjust a Culture's Starting Resources

1. Open `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml`
2. Add or edit a `<Culture>` element: `<Culture id="culture_id" gold="LORD_AMOUNT" influence="AMOUNT" playerGold="PLAYER_AMOUNT" />`
3. No code changes needed — the config provider loads all entries dynamically
4. Set any attribute to `"0"` to skip that dimension. Omitting `playerGold` also defaults to 0.
5. Cultures not listed in the config receive nothing (no fallback/default).
6. `playerGold` is range-validated `[0, 10_000_000]`. Out-of-range or non-numeric values revert to 0 with a warning in `rgl_log.txt` — check the log if an edit doesn't take effect.
7. Reload scope: edits take effect on the next **Bannerlord process restart**, not save-load. The config provider is registered as `Reuse.Singleton` and caches for the entire process lifetime.

## Player Starting Equipment (Per-Youth-Option)

In addition to gold, the player's youth-option choice determines starting equipment via existing equipment rosters under the naming convention `player_char_creation_{culture}_{titleType}_{m|f}`. The same rosters were already wired for the CC preview (`NarrativeMenuBuilder.UpdateYouthEquipment`) — this feature persists them onto `Hero.MainHero.BattleEquipment` and `CivilianEquipment` at finalize, so the equipment shown in CC is what the player actually carries into the campaign.

The `titleType` is sourced from `manager.CharacterCreationContent.SelectedTitleType` (set by the youth menu's `onSelect` callback in `NarrativeMenuBuilder.BuildOption`). To add a new youth option:

1. Add the option entry to `Main/_Module/ModuleData/charactercreation/youth_menu.json` with a `title_type` field.
2. Add four matching `<EquipmentRoster id="player_char_creation_{culture}_{titleType}_{m|f}">` elements (male battle, male civilian, female battle, female civilian) to whichever equipment XML covers that culture.
3. No code changes needed — `PlayerEquipmentService` builds the roster ID at runtime and `PlayerEquipmentAdapter.ApplyRosterToPlayer` looks it up via `MBObjectManager`.
4. Missing rosters log a warning and the player keeps the vanilla default equipment (no crash).

## TaleWorlds API Notes

- `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true)` — null source is safe when `disableNotification: true` (short-circuits the `giverHero == Hero.MainHero` check)
- `Clan.Influence` has a public setter; increasing it does not trigger `SkillLevelingManager.OnInfluenceSpent` (only decreasing does)
- `Hero.AllAliveHeroes` returns `MBReadOnlyList<Hero>`, safe to iterate at campaign start index 1

## Changelog

- 2026-07-03 — Retuned NPC-lord `gold` + clan `influence`: elves (rivendell/lothlorien/mirkwood) influence 1,000 → 1,500 (gold stays 600k); erebor 50k → 800k / 150 → 1,000; gondor 50k → 100k / 500 → 1,000; khuzait gold 50k → 75k; isengard/dolguldur 200k → 75k / 2,000 → 500; gundabad 200k → 75k / 2,000 → 1,000; umbar influence 500 → 1,000 (gold stays 200k). All `playerGold` values unchanged. Data-only edit to `startup_resources_config.xml`.
- 2026-06-30 — Rebalanced `playerGold` downward across all cultures: Elves (rivendell/lothlorien/mirkwood) 4,000, erebor 3,500, every other culture 2,000 (previously 4,000–10,000). NPC `gold`/`influence` unchanged. Data-only edit to `startup_resources_config.xml`.
- 2026-05-13 — Added `ParseGold`/`ParseInfluence` validation to the config provider (TryParse + range/finite checks, matching `ParsePlayerGold`); negative gold and NaN influence now revert with a warning instead of flowing through (closes #136).
- 2026-05-06 — Added per-culture `playerGold` (player starting funds at CC finalize) and youth-option equipment persistence onto `Hero.MainHero.BattleEquipment`/`CivilianEquipment`; seeded `playerGold` per culture and added missing `empire`/`shaghana`/`abanissa` rows.
- 2026-04-06 — Initial feature: culture-based startup gold to Lord heroes and influence to clans at new-game creation via `StartupResourcesBehavior` (index 1), data-driven by `startup_resources_config.xml`.

## GitHub Issue

- **Issue:** #42 — [feat: culture-based startup gold and influence distribution](https://github.com/haterade22/TAOM/issues/42)
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/character-creation.md](./character-creation.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
