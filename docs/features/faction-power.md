# Faction Power

## Overview

Faction Power gives each realm culture a power percent, from 50 to 200, that scales the party size limit of that
realm's AI lord parties and the size limit of its garrisons. 100 is the default and changes nothing. The player's own
clan, and every human player's clan in co-op, is never scaled. The percents are set per campaign with the
`taom.faction_power` console command.

## Why This Exists

- **Vanilla behavior:** every realm's lords and garrisons use the same party size rules.
- **TAOM requirement:** a player can make the War of the Ring harder or easier by realm, for example a stronger Mordor,
  without editing data files.
- **Without this feature:** the only lever is the global AI lord and garrison scaling of
  [ai-party-size.md](ai-party-size.md), which moves every realm at once.

## Architecture

### Design Challenge

`ExplainedNumber` sums its factors, so a plain `AddFactor(m - 1)` gives `base * (1 + feats + m - 1)`, not the limit
times `m`. The party size model also runs on worker threads (moving party speed reads `PartyBase.PartySizeLimit`), so
the stored percents are read through an immutable snapshot.

### Solution Approach

- `FactionPowerMath.MultiplyResult` adds the factor `(m - 1) * (1 + SumOfFactors)`, which multiplies the number as it
  stands by `m`. Flat bonuses added later (`ApplyFlat`, the Eye of Sauron host bonus, the cheat bonus) divide the factors
  back out, so they are not scaled.
- `TaomPartySizeModel` calls `IFactionPowerPartyHook.ApplyLordLimit` right after `ApplyAiLordScaling`, and
  `ApplyGarrisonLimit` right after `ApplyGarrisonScaling`. The order is pinned by `AiPartySizeOrderingTests`.
- The realm key is the culture id of the party's or settlement's map faction (`FactionPowerRealm.KeyOf`), read at call
  time.
- The percents live in the campaign's own option data (`IAdvancedStartDataAdapter`, category `taom_faction_power`, keys
  `taom_fp_<culture>` and `taom_fp_version`), which the engine saves. There is no TAOM save definer.
  `CampaignStartSession.Begin` reloads them for every new game and every load.

### Component Diagram

```
factions.json (game_faction / power_culture, side)
        |
  FactionPowerCatalogProvider -> FactionPowerCatalog (one slider per realm culture)
        |
  FactionPowerStore (campaign option data, snapshot) <- FactionPowerEditor <- taom.faction_power
        |
  FactionPowerService (multiplier, player clan rule)
        |
  FactionPowerPartyHook <- TaomPartySizeModel (lord limit, garrison limit)
```

## Configuration

### Config File: `Main/_Module/ModuleData/factionmap/factions.json`

The catalog has one slider for each distinct realm culture id in this file.

| Field | Type | Description |
|-------|------|-------------|
| `power_culture` | string | The kingdom culture id the slider keys on. Optional; only Faction Power reads it. |
| `game_faction` | string | The start culture of the map entry. Used as the slider key when `power_culture` is empty. |
| `side` | string | `evil`, `free` or anything else (neutral). The side presets use it. |
| `playable` | bool | Where several entries share a culture, the playable entry's name and side win. |

`power_culture` exists for a kingdom whose culture is not its map entry's start culture: Shaghâna (`shaghana`, starts as
`aserai`) and Âbanissa (`abanissa`, no start culture). `EveryShippedKingdomCulture_HasASlider` fails when a kingdom
culture in `taom_spkingdoms.xml` has no slider.

### Console command

| Command | Effect |
|---------|--------|
| `taom.faction_power list` | Every realm culture, its side and its percent |
| `taom.faction_power set <culture> <percent>` | One realm, 50 to 200 |
| `taom.faction_power preset standard` | Writes nothing |
| `taom.faction_power preset evil50` | Every evil realm to 150 |
| `taom.faction_power preset free50` | Every free realm to 150 |
| `taom.faction_power preset reset` | Every stored realm back to 100 |

A write clears the party size cache of every party (`PartySizeCacheInvalidator.InvalidateAll`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FactionPower/FactionPowerMath.cs` | Range and the multiplier factor |
| `Main/Features/FactionPower/FactionPowerCatalog.cs` | Sliders from factions.json |
| `Main/Features/FactionPower/FactionPowerStore.cs` | Campaign option data and the snapshot |
| `Main/Features/FactionPower/FactionPowerService.cs` | Multiplier lookup and the player clan rule |
| `Main/Features/FactionPower/FactionPowerEditor.cs` | `list`, `set` and `preset` |
| `Main/Features/FactionPower/Hooks/FactionPowerPartyHook.cs` | The seam `TaomPartySizeModel` calls |
| `Main/Features/FactionPower/Cheats/FactionPowerCheats.cs` | Console entry |
| `Main/Adapters/FactionPowerRealm.cs` | Realm key of a map faction |
| `Main/Features/FactionPower/FactionPowerIoC.cs` | Registration |

## Dependencies

- `IAdvancedStartDataAdapter` (CampaignStartOptions): the campaign's option data
- `IFactionConfigProvider` (FactionMap): factions.json
- `IPlayerControlQuery` (co-op): which clans belong to human players
- `AiPartySizeService.IsScalableAiLordParty` (AiPartySize): which parties count as AI lord parties

## Tests

- `TAOM.Tests/Features/FactionPower/`: the math, catalog, store, editor, presets, service and wiring.
- `FactionPowerCatalogTests.EveryShippedKingdomCulture_HasASlider`: every TAOM kingdom culture has a slider.
- `TAOM.Tests/Features/AiPartySize/AiPartySizeOrderingTests.cs`: the call order in `TaomPartySizeModel`.

## How to verify in game

**Preconditions:** a campaign (new or loaded), the console open.

1. Run `taom.faction_power set mordor 200`, then `taom.faction_power list`. The list shows `mordor` at 200.
2. Wait a few days and look at the troop count of AI Mordor lord parties on the map. They grow toward twice their
   usual limit. No `[Check]` line exists for this: a person must see it. Where the game shows the limit's breakdown,
   the line is "Realm power", and its percent is the added factor, so it reads more than +100% when the party already
   has other factors.
3. Run `taom.faction_power preset reset`, then `taom.faction_power list`. Every realm shows 100.

## Known limits

- A garrison's culture party size feat is added after the garrison limit returns, so a feat culture's garrison gets a
  little less than the full percent (at 200 percent with a +10% feat: 420, not 440). `ApplyGarrisonScaling` has the same
  order.
- `PartyBase.PartySizeLimit` caches on the roster version. When a clan changes kingdom, its parties keep the old realm's
  limit until their next roster change.
- The key is the culture, so a player-founded kingdom and a rebel kingdom follow the slider of their culture.

## Changelog

- 2026-11-25: PR #1592: the feature, the console command, and the realm key `power_culture` for Shaghâna and
  Âbanissa.

## GitHub Issue

- **Issue:** none. Luiz waived new GitHub issues for this work.
