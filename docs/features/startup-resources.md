# Startup Resources — Culture-Based Gold & Influence + Player Starting Gold + CC Equipment

## Overview

Distributes starting gold to individual Lord heroes and starting influence to clans at new game creation, with amounts configured per culture via XML. The same config file also drives the player's culture-based starting funds, granted at character-creation finalize. A sister feature in `Main/Features/CharacterCreation/` persists each youth option's equipment roster onto the player hero so the equipment shown in the CC preview is what the player actually walks into the campaign with. Together these set the economic and equipment baseline a new campaign opens on. The lord-gold and clan-influence half is **derived from each culture's measured party cost**, not hand-set and not flat; the player's own funds and starting kit stay culture-specific.

## Why This Exists

- **Vanilla behavior:** All factions start with identical default gold/influence regardless of lore
- **TAOM requirement:** every faction should open on a comparable campaign RUNWAY, which is not the same as a comparable pile of denars. A culture's troops cost between 6.91 and 21.66 denars a day per head, so an identical grant funds three times more campaign for Mordor than for Rivendell. Gold is therefore `K x runwayDays x avgTroopWage` with four lore tiers, and influence sits in three tiers keyed on how centrally a realm acts. See "Deriving the gold values" below.
- **Without this feature:** the distribution is vanilla's rather than TAOM's. Lords and clans keep the engine defaults, the player opens on the native 1,000 denars whichever culture he picked, and there is nowhere to express the difference between an elven treasury and a goblin one.

> **A flat table was tried and reversed on 2026-08-14.** Commit `4f72e160` set every culture to 250,000 gold / 1,000 influence (elves 500,000) so that "no faction opens the campaign with a structural economic head start"; it was superseded the same day because flat denars are not flat in effect. If you find text anywhere describing the flat table as current, it is stale.

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

NPC-lord `gold` and clan `influence` are derived, not hand-set: see "Deriving the gold values" below. They briefly went flat on 2026-08-14 (250,000 for every culture, 500,000 for the elves) and were re-derived the same day. They remain tuning knobs and may drift, so consult `startup_resources_config.xml` for the live numbers. `playerGold` was not part of either pass. The three tiers below account for all 22 culture rows, but they were not all set at once: the tier structure comes from the 2026-06-30 downward rebalance, which ran against the 18 rows the config had then, and the four rows added on 2026-08-11 (`goblin`, `mistymountainorcs`, `bluecraig`, `lindon`) were seeded into the tiers that already existed.

| Culture | playerGold | Rationale |
|---------|-----------|-----------|
| rivendell, lothlorien, mirkwood, lindon | 4,000 | Elven wealth, the highest player start |
| erebor | 3,500 | Dwarven hoard culture, just below the elves |
| the other 17: gondor, vlandia (Rohan), sturgia (Dale), empire (Dunland), battania (Khand), aserai (Harad), khuzait (Rhun), shaghana, abanissa, mordor, isengard, gundabad, dolguldur, umbar, goblin, mistymountainorcs, bluecraig | 2,000 | Flat baseline for every human + orc culture |

`shaghana` (eastern Harad reach) and `abanissa` (deep south Harad) are independent Harad-region kingdoms, full peers of Aserai with their own NPC clans, lords, and ruler titles (Taskral / Châjaphân) rather than sub-cultures, and are now selectable in character creation, so their `playerGold` is live.

`goblin`, `mistymountainorcs`, `bluecraig`, and `lindon` are selectable too, so every one of the config's 22 rows is live. What puts a culture in the character-creation picker is `is_main_culture="true"`: 16 of the 22 carry it in `taom_spcultures.xml` and the other six inherit it from vanilla, which `spcultures.xslt` never touches. `Main/_Module/ModuleData/charactercreation/cultures.json` is read *after* the pick, for the race filter and the body/settlement defaults, and it too has an entry for all 22. The narrative menus are the one surface that does not cover everything: 20 cultures have their own youth, parents, education, and adulthood options, while `shaghana` and `abanissa` have none in any of the four. Blue Craig and Lindon were promoted out of borrowed cultures on 2026-08-10 (Blue Craig off goblin, Lindon off rivendell) and needed rows of their own, because these values key on culture and before the split their kingdoms had none.

## Deriving the gold values

`gold` is paid per lord and `influence` per clan, so both multiply by roster size. Lord counts run
from 10 (Umbar, Lindon) to 150 (Moria), which means a rate set per culture in isolation makes a
faction's treasury track how many lords it happens to have rather than what it needs. One rule
replaces that:

```
gold = K x runwayDays x avgTroopWage        K = 55.93
```

**`avgTroopWage` is measured, and it models what the engine actually charges.** Each noble clan's
`default_party_template` is resolved from `characters/clans.xml` and `spclans.xslt` (176 of 193
lord-party templates are per clan, not per culture, so reading only the culture defaults measures
rosters almost nobody fields). Every stack is weighted by its expected spawn count,
`(min + max) / 2`, per [party-template-sizing.md](../reference/party-template-sizing.md). Each
troop's wage is what `TroopCostService.GetCharacterWage` would return: the T0-T10 table under
`tier = clamp(ceil((level - 5) / 5), 0, 10)`, **times 1.3 when the troop is mounted, then truncated
to int exactly as the engine truncates** (a mounted T5 costs 15, not 15.6). Mounted comes from the
troop's `default_group`, which is what `CharacterObject.IsMounted` resolves to for a non-hero.

Party-wage feats are folded in: `taom_mordor_wage` x1.20, `taom_gundabad_wage` x1.10,
`taom_umbar_wage` x1.08, and Rohan's `taom_rohan_mounted_wage` −15% on its mounted share, the only
mounted *wage* feat in TAOM. Garrison-wage feats are not, because they are settlement-scoped and
never touch field-party burn. The mercenary x1.5 multiplier is not modelled because no
mercenary-occupation troop appears in any hero party template; the derivation asserts that rather
than assuming it.

The measured spread is 3.1x, Mordor cheapest at 6.91 denars/head/day and Rivendell dearest at 21.66.
Khand (`battania`) binds no hero party template of its own and falls through to Rhûn's, so it
carries Rhûn's cost, not a Variag one.

**What it still cannot model.** Vanilla layers per-hero perk and kingdom-policy modifiers onto the
same `ExplainedNumber` at runtime (DeepPockets, Frugal, `MilitaryCoronae`, `EfficientCampaigner` and
a dozen more), all depending on which lord holds which perk in a given campaign. This is a
design-time proxy for field-party burn, not the runtime figure, and no static table can be otherwise.

`runwayDays` is the only judgement call: **270** for the mythic treasuries (the four elven realms
and Erebor), **150** for the great realms (Gondor, Rhûn, Umbar, Mordor, Moria), **100** for the
regional powers, **70** for Goblin-town and Blue Craig. `K` scales the whole table to a target pool.
Two knobs move everything; re-derive rather than nudging a single row, or the rate stops meaning
anything. Influence uses three flat tiers (600 / 400 / 200) keyed on how centrally a realm acts,
replacing a 25x per-clan spread that left Rohan's 22 clans sharing less influence than one Rhûn clan
held.

## The structural gap (what startup gold does not fix)

Startup gold is a one-time cushion. It cannot fix a permanent income deficit, and TAOM has a large
one. Measuring each culture's fief income against its own burn, as
`(prosperity + hearth per lord) / avgTroopWage`, gives a **26.1x** spread: Dol Guldur fields 127
lords on 23 fiefs, Umbar 10 lords on 40.

Per-settlement wealth is already uniform across the map, so the gap is not poor settlements. What
the 2026-08-14 pass did was raise the existing fiefs of the eight worst cultures (towns to 4,800,
castles to 950, village hearth to 500, lift-only), which moved the spread from **26.1x to 18.5x**:

| Culture | before | after |
|---|---:|---:|
| bluecraig | 28.5 | 44.2 |
| mirkwood | 28.5 | 40.0 |
| lindon | 24.0 | 38.9 |
| isengard | 27.0 | 32.5 |
| mistymountainorcs | 14.8 | 23.6 |
| goblin | 13.2 | 21.4 |
| gundabad | 13.4 | 18.3 |
| dolguldur | 9.0 | 12.7 |

(Ratios computed from unrounded values. Deriving them from the rounded column above instead gives
slightly different figures, which is how an earlier revision of this doc arrived at 24.6x/17.6x.)

**The four worst stay far below the rest, and no amount of fief wealth fixes them.** Pushing every
Dol Guldur, Gundabad, Goblin-town and Moria fief to vanilla's absolute maxima still leaves them
there, because their problem is lord count, not fief wealth. That fix is roster size and it has not
been made: removing lord definitions breaks existing saves, so it has to ride a major version.
Adding lords does not, which makes Umbar (10 lords, 40 fiefs, the inverse outlier at the top of the
spread) the cheapest remaining correction.

**Why not just add settlements?** Because the engine's `rglConcurrentQueue` assert caps loaded
prefab entities at 131,072 globally, across every loaded module. The size of the remaining headroom
is genuinely unsettled: `tools/check_prefab_budget.py` reports 93,407, but CLAUDE.md's own trap
table warns that the checker counts only `TAOM_Map/Prefabs` and therefore "prints OK at 99% of the
cap", citing an all-module measurement of 130,151 with ~921 spare on 2026-08-08. **Neither figure
has been verified as an all-module total recently**, so treat adding settlements as needing a fresh
measurement first rather than as either safe or blocked. It was not attempted here.

### The floor is not an income-only change

Raising `prosperity` and `hearth` moves several engine systems at once, and the balance model above
accounts for none of them. Measured against the pre-floor values, this pass added 24,920
fortification prosperity and 18,900 hearth, which means:

| System | Effect | Source |
|---|---|---|
| Militia growth | roughly **+72 militia/day** aggregate across the changed settlements: `Town.Prosperity / 1000` and `Village.Hearth / 400` are raw, not bucketed | `DefaultSettlementMilitiaModel` |
| Town market gold | the 12 changed towns gain about **+203,000 aggregate target gold**, since TAOM's economy model targets `25000 + Prosperity x 12` rather than vanilla's `10000 + P x 12` | `TaomSettlementEconomyModel`, `settlement_economy_config.json` |
| Village production | all 110 villages stay at hearth level 1 immediately (the buckets are <200 / 200-599 / >=600), but 500 brings the level-2 crossing forward by roughly 83-167 campaign days, and level 2 raises the production factor 1.0 to 1.5 and food 2 to 3 | `Village.GetHearthLevel`, `DefaultVillageProductionCalculatorModel` |
| Prosperity growth | unaffected: the housing term only goes negative above 6,000 and the floor is 4,800 | `DefaultSettlementProsperityModel` |

None of these destabilise a settlement, and for factions this fief-poor stronger garrisons are
arguably the point. They are recorded because a future retune should know the lever is not
income-only.

The floor is committed at [tools/settlement_economy_floor.json](../../tools/settlement_economy_floor.json)
and applied with:

```
python tools/rebalance_settlement_prosperity.py --culture-floor-file tools/settlement_economy_floor.json --apply
```

It writes the **LIVE** `<game>/Modules/TAOM_Map/ModuleData/settlements.xml`. That module is
unversioned, so a reinstall reverts the pass silently; `validate_moduledata.py`'s
`SETTLEMENT_ECONOMY_FLOOR` check reads the same spec file and fails when the live module drops below
it. Both consumers read the one spec, so neither restates the numbers. Prosperity and hearth are
saved state, so like the gold grant these reach new campaigns only.

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

- 2026-08-14 (later, supersedes the flattening below): Retuned NPC-lord `gold` and clan `influence` against **measured** per-culture burn rates. Flat denars are not flat in effect: a culture's troops cost between 5.31 and 19.03 denars a day per head, so an identical 250,000 funded 1.89x more campaign for Mordor than for Gundabad, and an identical 500,000 bought Rivendell less than Lothlorien. Gold is now `K x runwayDays x avgTroopWage` with four lore tiers (270 / 150 / 100 / 70 days) and `K = 55.93`, putting about 100M denars in AI hands. Influence moved to three tiers (600 / 400 / 200) keyed on how centrally a realm acts. Derivation and the measurement method are in the config file's own header. `playerGold` unchanged. Companion data change in the same pass: eight fief-starved cultures' settlements raised to a committed floor in the LIVE `TAOM_Map` module (see "The structural gap" below), gated by the new `SETTLEMENT_ECONOMY_FLOOR` validator check.
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
