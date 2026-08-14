# Startup Resources — Culture-Based Gold & Influence + Player Starting Gold + CC Equipment

## Overview

Distributes starting gold to individual Lord heroes and starting influence to clans at new game creation, with amounts configured per culture via XML. The same config file also drives the player's culture-based starting funds, granted at character-creation finalize. A sister feature in `Main/Features/CharacterCreation/` persists each youth option's equipment roster onto the player hero so the equipment shown in the CC preview is what the player actually walks into the campaign with. Together these set the economic and equipment baseline a new campaign opens on. The lord-gold and clan-influence half is deliberately flat across factions apart from the elven realms; the player's own funds and starting kit stay culture-specific.

## Why This Exists

- **Vanilla behavior:** All factions start with identical default gold/influence regardless of lore
- **TAOM requirement:** a deliberate, near-uniform economic start. Every culture's lords open on 250,000 denars and every clan on 1,000 influence, with the four elven realms the single exception at 500,000 gold, keeping the "old, wealthy, few" character they had before the 2026-08-14 flattening. Nothing else separates one kingdom's opening position from another's. Player starting funds are a separate knob and still vary by culture.
- **Without this feature:** the distribution is vanilla's rather than TAOM's. Lords and clans keep the engine defaults, the player opens on the native 1,000 denars whichever culture he picked, and there is nowhere to express the elven exception.

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

NPC-lord `gold` and clan `influence` have been flat since 2026-08-14: 250,000 gold and 1,000 influence for every culture, except the four elven realms at 500,000 gold. They remain tuning knobs and may drift, so consult `startup_resources_config.xml` for the live numbers. `playerGold` was not flattened. The three tiers below account for all 22 culture rows, but they were not all set at once: the tier structure comes from the 2026-06-30 downward rebalance, which ran against the 18 rows the config had then, and the four rows added on 2026-08-11 (`goblin`, `mistymountainorcs`, `bluecraig`, `lindon`) were seeded into the tiers that already existed.

| Culture | playerGold | Rationale |
|---------|-----------|-----------|
| rivendell, lothlorien, mirkwood, lindon | 4,000 | Elven wealth, the highest player start |
| erebor | 3,500 | Dwarven hoard culture, just below the elves |
| the other 17: gondor, vlandia (Rohan), sturgia (Dale), empire (Dunland), battania (Khand), aserai (Harad), khuzait (Rhun), shaghana, abanissa, mordor, isengard, gundabad, dolguldur, umbar, goblin, mistymountainorcs, bluecraig | 2,000 | Flat baseline for every human + orc culture |

`shaghana` (eastern Harad reach) and `abanissa` (deep south Harad) are independent Harad-region kingdoms, full peers of Aserai with their own NPC clans, lords, and ruler titles (Taskral / Châjaphân) rather than sub-cultures, and are now selectable in character creation, so their `playerGold` is live.

`goblin`, `mistymountainorcs`, `bluecraig`, and `lindon` are selectable too, so every one of the config's 22 rows is live. What puts a culture in the character-creation picker is `is_main_culture="true"`: 16 of the 22 carry it in `taom_spcultures.xml` and the other six inherit it from vanilla, which `spcultures.xslt` never touches. `Main/_Module/ModuleData/charactercreation/cultures.json` is read *after* the pick, for the race filter and the body/settlement defaults, and it too has an entry for all 22. The narrative menus are the one surface that does not cover everything: 20 cultures have their own youth, parents, education, and adulthood options, while `shaghana` and `abanissa` have none in any of the four. Blue Craig and Lindon were promoted out of borrowed cultures on 2026-08-10 (Blue Craig off goblin, Lindon off rivendell) and needed rows of their own, because these values key on culture and before the split their kingdoms had none.

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
| `Main/Features/StartupResources/PlayerStartupGoldService.cs` | Looks up `PlayerGold` from config; calls `IGoldGiftAdapter.GiveGoldToHero`. Not idempotent — second caller is PlayerPossession |
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

## Re-granting After a Multiplayer Join

`IPlayerStartupGoldService.GrantPlayerStartupGold` has a second caller. Every co-op base discards the
character-creation hero at the join hand-off and gives the joining player a host-authored one, so the
grant that ran at CC finalize landed on a hero that no longer exists — field-confirmed as a Mirkwood
player receiving the native 1,000 gold instead of 1,000 + 4,000. [player-possession.md](player-possession.md)
detects the hand-off and re-invokes the grant against the hero the player actually ends up with, keyed
on the **character-creation** culture rather than whatever culture the host's hero carries.

**`GrantPlayerStartupGold` is not idempotent — do not add a third caller casually.** It validates its
arguments, looks up the culture entry, skips a non-positive `playerGold`, and otherwise calls
`IGoldGiftAdapter.GiveGoldToHero` unconditionally; there is no already-granted guard. Nothing
double-grants today only because the two callers target different hero ids. All duplicate protection
lives in PlayerPossession — the co-op presence gate, single consumption, and a `SyncData` marker per
hero id.

**Youth-option equipment is NOT re-applied.** The reconciliation re-invokes exactly four grants: race,
startup gold, career, and the special-resource seed. `IPlayerEquipmentService.ApplyPlayerStartingEquipment`
is not among them, so a joiner keeps whatever equipment the host's hero came with. That is a stated
limitation, not something the 2026-08-03 work fixed.

The NPC-lord gold and clan-influence half is unaffected: `StartupResourcesBehavior` fires at
`OnNewGameCreatedPartialFollowUpEvent` index 1 against world state, not player state.

## TaleWorlds API Notes

- `GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true)` — null source is safe when `disableNotification: true` (short-circuits the `giverHero == Hero.MainHero` check)
- `Clan.Influence` has a public setter; increasing it does not trigger `SkillLevelingManager.OnInfluenceSpent` (only decreasing does)
- `Hero.AllAliveHeroes` returns `MBReadOnlyList<Hero>`, safe to iterate at campaign start index 1

## Changelog

- 2026-08-14: Flattened NPC-lord `gold` and clan `influence` so no faction opens the campaign with a structural economic head start. Every culture is now 250,000 gold / 1,000 influence, except the four elven realms (rivendell, lothlorien, mirkwood, lindon) at 500,000 gold. Notable movers: erebor is the one large move DOWN, from 800k, where it had been the richest culture on the map by a factor of four, and the elves came down from 600k to the 500k exception. Everything else moved up: bluecraig from 40k, vlandia (Rohan) and sturgia (Dale) from 50k gold and 50 influence (a twentyfold influence jump), umbar from 200k. `playerGold` was deliberately not flattened and still varies by culture (elves 4,000, erebor 3,500, everyone else 2,000). Data-only edit to `startup_resources_config.xml`.
- 2026-08-03 — The player gold grant is re-invoked after a multiplayer join hand-off, against the hero the join actually hands the player and with the character-creation culture (see [player-possession.md](player-possession.md)). Wiring only — no config, tuning or data change; the youth-option equipment is not re-applied.
- 2026-07-03 — Retuned NPC-lord `gold` + clan `influence`: elves (rivendell/lothlorien/mirkwood) influence 1,000 → 1,250 (gold stays 600k); erebor 50k → 800k / 150 → 1,000; gondor 50k → 100k / 500 → 1,000; khuzait gold 50k → 75k; isengard/dolguldur 200k → 75k / 2,000 → 500; gundabad 200k → 75k / 2,000 → 1,000; umbar influence 500 → 1,000 (gold stays 200k). All `playerGold` values unchanged. Data-only edit to `startup_resources_config.xml`.
- 2026-06-30 — Rebalanced `playerGold` downward across all cultures: Elves (rivendell/lothlorien/mirkwood) 4,000, erebor 3,500, every other culture 2,000 (previously 4,000–10,000). NPC `gold`/`influence` unchanged. Data-only edit to `startup_resources_config.xml`.
- 2026-05-13 — Added `ParseGold`/`ParseInfluence` validation to the config provider (TryParse + range/finite checks, matching `ParsePlayerGold`); negative gold and NaN influence now revert with a warning instead of flowing through (closes #136).
- 2026-05-06 — Added per-culture `playerGold` (player starting funds at CC finalize) and youth-option equipment persistence onto `Hero.MainHero.BattleEquipment`/`CivilianEquipment`; seeded `playerGold` per culture and added missing `empire`/`shaghana`/`abanissa` rows.
- 2026-04-06 — Initial feature: culture-based startup gold to Lord heroes and influence to clans at new-game creation via `StartupResourcesBehavior` (index 1), data-driven by `startup_resources_config.xml`.

## GitHub Issue

- **Issue:** #42 — [feat: culture-based startup gold and influence distribution](https://github.com/haterade22/TAOM/issues/42)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/character-creation.md](./character-creation.md)
- [docs/features/player-possession.md](./player-possession.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
