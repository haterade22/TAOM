# Faction Power

## Overview

Faction Power gives each realm culture a power percent, from 50 to 200, that scales the party size limit of that
realm's AI lord parties, the size limit of its garrisons, its militia growth, its volunteer refill, its auto-resolve
strikes, and (above 100) lowers its AI wages. 100 is the default and changes nothing. The player's own
clan, and every human player's clan in co-op, is never scaled. The percents are set per campaign: on the new-campaign
screen (the "Realm power" category, see "The start screen") and, for a running campaign or an older save, with the
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


### The start screen

The Advanced Start Options screen (see [campaign-start-options.md](campaign-start-options.md)) has a "Realm power" category:

- **Realm power** list: Standard (default, writes nothing), Evil +50%, Free peoples +50% and Custom.
- **One slider per realm culture**, 50 to 200, default 100, built from the catalog, so a new `factions.json` entry with a
  `game_faction` (or `power_culture`) and a `side` gets a slider with no code. The sliders are hidden unless the list says
  Custom (an option condition answers "is this hidden", so `true` hides).
- **Labels.** The screen reads names and descriptions from `Module.CurrentModule.GlobalTextManager`, which reads only
  `global_strings.xml` (#604). The category and the list rows are fixed rows there. The slider rows are not in the file:
  `FactionPowerStartScreen.Add` applies one name row (the realm's `factions.json` name text, which already has its
  translations) and one description row per slider to that manager through `GlobalStringsOverrides.Apply`, the route
  `SubModule` uses for the rest of the file. The description is the inline key `taom_fp_slider_desc`, harvested into
  `taom_module_strings.xml`. `RealmPowerSliders_LabelsResolveFromTheTextManagerTheScreenReads_BeforeAnyFallbackIsNeeded`
  makes the engine's own `GetName` and `GetDescription` calls on a manager that got only those rows. The fallback, 56
  static rows in `global_strings.xml`, was not needed.
- **The pin.** `CampaignStartSession.Begin` calls `FactionPowerSession.Begin(isNewCampaign)`. A new campaign reads the
  screen keys (`taom_fp_preset`, `taom_fp_s_<culture>`; the engine writes only changed, visible options),
  `FactionPowerStartResolver` (pure) turns them into percents, and `IFactionPowerSettings.TrySet` stores them under
  `taom_fp_<culture>`. Then the store reloads. A loaded save is never pinned. The side presets write 150 for that side's
  realms with the same rule as the console presets; Custom writes the moved sliders and skips any outside 50 to 200 with
  a warning. Screen keys and stored keys stay separate, so the screen can change without a save migration.
- **Logs.** `FactionPower.Pinned: preset=<item> <culture>=<percent> ...` when something was pinned,
  `FactionPower.PinFailed` when a write was refused, and the existing `FactionPower.Loaded` after every load.

### Component Diagram

```
factions.json (game_faction / power_culture, side)
        |
  FactionPowerCatalogProvider -> FactionPowerCatalog (one slider per realm culture)
        |
  FactionPowerStore (campaign option data, snapshot) <- FactionPowerEditor <- taom.faction_power
        ^
  FactionPowerSession <- CampaignStartSession.Begin (pins the screen's choice into a new campaign)
        ^
  FactionPowerStartResolver <- screen keys <- FactionPowerStartScreen (category, list, sliders, labels)
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
| `Main/Features/FactionPower/FactionPowerScreenKeys.cs` | Screen keys and list item ids |
| `Main/Features/FactionPower/FactionPowerStartResolver.cs` | Screen choice to percents (pure) |
| `Main/Features/FactionPower/FactionPowerSession.cs` | Pins a new campaign's screen choice, reloads the store |
| `Main/Features/AdvancedStartOptions/FactionPowerStartScreen.cs` | The category, list, sliders and label rows |
| `Main/Features/FactionPower/Hooks/FactionPowerPartyHook.cs` | The seam `TaomPartySizeModel` calls |
| `Main/Features/FactionPower/Hooks/FactionPowerEconomyHook.cs` | The seam the militia and wage models call |
| `Main/Features/FactionPower/Hooks/FactionPowerStrikeHook.cs` | The seam `TaomCombatSimulationModel` calls |
| `Main/Features/FactionPower/Cheats/FactionPowerCheats.cs` | Console entry |
| `Main/Adapters/FactionPowerRealm.cs` | Realm key of a map faction |
| `Main/Features/FactionPower/FactionPowerIoC.cs` | Registration |

## Dependencies

- `IAdvancedStartDataAdapter` (CampaignStartOptions): the campaign's option data
- `CampaignStartSession` (CampaignStartOptions) calls `IFactionPowerSession.Begin`; `TaomStartOptionsProvider` (AdvancedStartOptions) adds the screen
- `GlobalStringsOverrides` (LocalizationOverride): the slider label rows
- `IFactionConfigProvider` (FactionMap): factions.json
- `IPlayerControlQuery` (co-op): which clans belong to human players
- `AiPartySizeService.IsScalableAiLordParty` (AiPartySize): which parties count as AI lord parties

## Tests

- `TAOM.Tests/Features/FactionPower/`: the math, catalog, store, editor, presets, service and wiring.
- `FactionPowerCatalogTests.EveryShippedKingdomCulture_HasASlider`: every TAOM kingdom culture has a slider.
- `FactionPowerEconomyTests`: militia equilibrium, strike, wage relief, volunteers and the model call order.
- `FactionPowerStartTests`: the resolver for every preset and for Custom, and the session over a real store (new campaign,
  loaded save, a second campaign, no campaign, a refused write).
- `TaomStartOptionsProviderTests`: the category, the list, the sliders hidden unless Custom (both polarities), hidden sliders
  absent from `GetChangedOptions`, and the label gate above.
- `TAOM.Tests/Features/AiPartySize/AiPartySizeOrderingTests.cs`: the call order in `TaomPartySizeModel`.

## How to verify in game

**Preconditions:** a campaign (new or loaded), the console open.

1. Run `taom.faction_power set mordor 200`, then `taom.faction_power list`. The list shows `mordor` at 200.
2. Wait a few days and look at the troop count of AI Mordor lord parties on the map. They grow toward twice their
   usual limit. No `[Check]` line exists for this: a person must see it. Where the game shows the limit's breakdown,
   the line is "Realm power", and its percent is the added factor, so it reads more than +100% when the party already
   has other factors.
3. Run `taom.faction_power preset reset`, then `taom.faction_power list`. Every realm shows 100.

**Start screen** (a new campaign, from the main menu):

1. Open the Advanced Start Options. The "Realm power" category has the list at Standard and no sliders. No row reads "ERROR".
2. Pick Evil +50%, start the campaign, then check `taom_debug` for `FactionPower.Pinned: preset=taom_fp_preset_evil50 ...`
   with the evil realms at 150, and `FactionPower.Loaded` with the same table. In the party tooltip of a Mordor lord the
   limit shows a "Realm power" line; a Gondor lord's shows none.
3. Start another campaign with Custom: one slider per realm appears, named like the kingdoms. Move two, start, and `taom.faction_power list` shows them.
4. Load a save from before this change: `list` shows 100 everywhere and the start screen choice is not applied to it.
No `[Check]` line exists for this, and no `tools/ingame_checks` manifest: the manifest file is named for an issue number and this work has none.

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

- 2026-11-28: PR C: the start screen (Realm power list, per-realm sliders, labels from `factions.json`, the pin on a new campaign).
- 2026-11-26: PR B: militia, volunteer refill, auto-resolve strength and wage relief.
- 2026-11-25: PR #1592: the feature, the console command, and the realm key `power_culture` for Shaghâna and
  Âbanissa.

## GitHub Issue

- **Issue:** none. Luiz waived new GitHub issues for this work.
