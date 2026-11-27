# Faction Power

## Overview

Faction Power gives each realm culture a power percent, from 50 to 200, that scales the party size limit of that
realm's AI lord parties, the size limit of its garrisons, its militia growth, its volunteer refill, its auto-resolve
strikes, and (above 100) lowers its AI wages. 100 is the default and changes nothing. The player's own
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

### The economy levers

All levers return before touching a number when the realm is at 100, and none touches the player's clan (every human
player's clan in co-op) or the main party.

| Lever | Where | Effect |
|-------|-------|--------|
| Militia | `TaomSettlementMilitiaModel.CalculateMilitiaChange` | Scales the daily inflow (change plus the 2.5 percent retired, read through the factors), so the militia equilibrium (40 x inflow) scales by the percent. Vanilla's 0.025 is line 98 of `DefaultSettlementMilitiaModel`. A village that is not in its Normal state (looted, being raided, or forced for volunteers or supplies) is skipped, as the base returns nothing for it. |
| Militia after a siege | `TaomSettlementMilitiaModel.MilitiaToSpawnAfterSiege` | The 90 to 108 militia a won siege spawns, times the percent, rounded. |
| Volunteers | `VolunteerProductionService.Compute` | The probability times the percent, after the War of the Ring step, one clamp to 0..1. A hit on a filled slot also rolls the notable's small tier-up chance, so tier-ups speed up too. Castles go through the same call. |
| Auto-resolve | `TaomCombatSimulationModel.SimulateHit` | Damage of the striker party's realm times the square root of the percent (k = 0.5, strength then moves by about m^2.1 with full rosters: roughly 4.3x at 200 percent and 0.23x at 50 percent, approximate because damage is truncated to an int and saturates at the target's hit points). |
| Wages | `TaomPartyWageModel.GetTotalWage` | Above 100: AI lord parties and garrisons of the realm pay wages x 1/percent. At or below 100: normal wages. Applied after the AI wage relief and before free garrison upkeep. `GetTotalWage` also feeds `GetAvailableWageBudget`, so the relief widens AI recruiting and garrison budgets as well; that is intended. The upgrade budget widens less than 1/percent, because `PartyUpgraderCampaignBehavior` compares the relieved total against unrelieved per-troop wages, as with the AI wage relief. |


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
  FactionPowerEconomyHook <- TaomSettlementMilitiaModel, TaomPartyWageModel
  FactionPowerStrikeHook <- TaomCombatSimulationModel
  VolunteerProductionService <- TaomVolunteerModel, CastleRecruitmentService
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
| `Main/Features/FactionPower/Hooks/FactionPowerEconomyHook.cs` | The seam the militia and wage models call |
| `Main/Features/FactionPower/Hooks/FactionPowerStrikeHook.cs` | The seam `TaomCombatSimulationModel` calls |
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
- `FactionPowerEconomyTests`: militia equilibrium, strike, wage relief, volunteers and the model call order.
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

- A realm above 100 percent also gets faster volunteer tier-ups (a hit on a filled slot rolls the small tier-up chance).
- `MilitiaToSpawnAfterSiege` is read by `MilitiasCampaignBehavior.OnAfterSiegeCompleted`, which in v1.5.7 adds it only when an attacker wins a siege assault (`SiegeAssaultEventComponent` sends the only `isWin` true; a sally out and a sea blockade add none), after `ChangeOwnerOfSettlementAction.ApplyBySiege` ran in the earlier `SiegeCompleted` event. So the settlement's current owner is the realm that took it, and that realm is scaled.
- Siege veto asymmetry: the militia and garrison of a stronger realm raise the strength an attacker must beat, so a
  100 percent realm attacking a 150 percent realm needs about 1.5x the force it needs today.

- A garrison's culture party size feat is added after the garrison limit returns, so a feat culture's garrison gets a
  little less than the full percent (at 200 percent with a +10% feat: 420, not 440). `ApplyGarrisonScaling` has the same
  order.
- `PartyBase.PartySizeLimit` caches on the roster version. When a clan changes kingdom, its parties keep the old realm's
  limit until their next roster change.
- The key is the culture, so a player-founded kingdom and a rebel kingdom follow the slider of their culture.

## Changelog

- 2026-11-26: PR B: militia, volunteer refill, auto-resolve strength and wage relief.
- 2026-11-25: PR #1592: the feature, the console command, and the realm key `power_culture` for Shaghâna and
  Âbanissa.

## GitHub Issue

- **Issue:** none. Luiz waived new GitHub issues for this work.
