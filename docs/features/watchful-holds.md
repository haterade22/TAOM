# Watchful Holds

## Overview

Every castle and town has a zone of control. When a lord party marches within a few map units of the walls of a fort whose realm is at war with it, the garrison harasses the march. The player's party is slowed and takes small losses; AI lords are only slowed. The player sees one red message on every harassment and hears a horn at most once every 24 campaign hours.

## Why This Exists

- **Vanilla behavior:** an enemy stronghold has no effect on a host that walks past it. A Rohan army can pass Orthanc and an orc host can pass under Caras Galadhon without a cost.
- **TAOM requirement:** garrisons should make enemy marches near their walls costly: Cirith Ungol and Minas Morgul watching the passes, Dol Guldur over southern Mirkwood, Lothlorien and the Woodland Realm at their borders.
- **Without this feature:** borders and fortresses are scenery on the campaign map, and a march past the heart of an enemy realm is free.

## What the player sees

- A harassed march: the party becomes disorganized (see "Vanilla disorganized effect" below), loses a few men, some food and some morale. One red message names the fort and the losses: "The garrison of {FORT} harries your march, slowing it: {KILLED} killed, {WOUNDED} wounded and {FOOD} food lost." (`taom_watchful_holds_harried`). The notice adapter looks the fort's name up from its settlement id.
- A horn (the vanilla 2D alert `event:/alerts/horns/attack`) sounds with the first harassment and then at most once every 24 campaign hours (`hornCooldownHours`). The message still shows on every harassment. An AI harassment never starts the horn clock.
- If the player leads an army, every party merged into it (`AttachedParties`; lords still travelling to join are skipped) takes its own losses and loses food from its own stores, every member loses morale, and the army's cohesion drops once by `cohesionLoss` (5), never below `cohesionFloor` (10) and never raised if already below it, so harassment alone never disbands the army. Only the leader is disorganized, and the message reports the totals across the army. The `minPartySize` gate counts the leader party's own men, not the army total.
- If the horn sound event is missing or fails, a warning goes to the log and the message still shows.

## Trigger rules and exemptions

Each campaign hour, `HourlyTickPartyEvent` calls the behavior for one party. The behavior drops non-lord parties first (before any co-op probe), then the service runs the remaining cheap checks, so an exempt party costs a few field reads.

A party can be harassed when all of these hold:

1. It is a lord party or the main party, and it is moving.
2. A castle or town lies within `radius` map units (2D distance).
3. That fort is not under siege and its garrison party holds at least `minGarrison` men (wounded included).
4. The fort's map faction is at war with the party's map faction.
5. The province gate passes (below).
6. The hourly roll succeeds: `playerChancePerHour` or `aiChancePerHour`, times the `chanceMultiplier` of the fort owner's culture, clamped to 0..1.

The nearest fort within the radius that passes checks 3 to 5 is the one used. Forts are sorted by distance, ties broken by id.

**Exempt:**

| Case | Why |
|---|---|
| The enlisted player | Enlisted service is the commander's march; the commander's party is still treated as an AI lord |
| An army follower | The army leader is the one checked |
| A party already disorganized, or one the engine's `CanGetDisorganized` refuses | Respects the engine's own rule |
| A party with fewer than `minPartySize` men | Too small to be worth a garrison's attention |
| A party in a settlement, a map event or a siege event | Not on the road |
| A party that is not moving | Same |
| A party with a NaN or infinite position | A positive finite check; the party is skipped |
| Caravans, villagers, bandits and any non-lord party | Dropped in the behavior, before the service is called |

## Player and AI effects

| | Player | AI lord |
|---|---|---|
| Chance per hour | 30% | 15% |
| Disorganized | yes | yes |
| Men wounded | `floor(men * woundFraction * lossMultiplier)` plus a random 0 to `woundExtraMax` (the extra is not scaled) | none |
| Men killed | `floor(men * killFraction * lossMultiplier)` plus a random 0 to `killExtraMax` (the extra is not scaled) | none |
| Food lost | `foodLossFraction` of the party's food, spread over random food stacks, at least one unit; only `IsFood` stacks are taken, livestock never | none |
| Morale | minus `moraleLoss` | none |
| Army cohesion | minus `cohesionLoss`, never below `cohesionFloor` (when leading an army) | none |

Kills are applied first, then wounds, and each count is clamped to the healthy regulars at that moment. Only `RemoveNumberOfNonHeroTroopsRandomly` asserts on too large a request (and then clamps itself); `WoundNumberOfNonHeroTroopsRandomly` silently wounds fewer when no healthy regular is left. The clamp keeps the reported counts true. Both pick only non-hero troops, so heroes are never hit. The message reports what was actually applied, not what was asked.

The MCM toggle "Harass AI lords" turns the AI half off, so the campaign pace can be protected without losing the player feature.

## Architecture

### Design Challenge

One hourly check per party must be cheap: there are hundreds of parties, most of them not lords. A once-per-hour scan of every party was rejected because it puts the whole cost in one frame; the engine spreads `HourlyTickPartyEvent` across the hour instead. A movement-speed penalty for being in enemy territory was left out: it would need a snapshot on the parallel party tick for a small gain. Castles only was rejected because Minas Morgul, Osgiliath, Dol Guldur, Orthanc and Caras Galadhon are towns.

### Solution Approach

A feature module (`WatchfulHoldsModule`) declares one campaign behavior, `WatchfulHoldsBehavior`, which listens to `HourlyTickPartyEvent` and `DailyTickEvent`. There is no Harmony patch and no GameModel override. The service holds every rule and sees parties as opaque handles; the adapter does all the TaleWorlds reads and writes (ADR-007).

### Province gate and distance fallback

While Realm Borders is on and has computed its provinces (`TerritoryState.Ready`), the party must stand inside the fort's own province, so a fort does not harass a party across a river or a mountain range. `RealmProvinceLookup` asks `RealmTerritoryService` which province fief holds the party's position. A party in a different fort's province, or in no province, is not harassed by this fort. The lookup runs once per party, before the candidate forts are read.

Otherwise (the Realm Borders switch `EnableRealmBorders` off, or the provinces not built: still sampling, failed, or a dedicated server), the lookup reports no provinces and distance alone decides.

### Component Diagram

```
watchful_holds/watchful_holds.json    MCM (2 toggles)
        |                                  |
  WatchfulHoldsConfigProvider      WatchfulHoldsSettingsProvider
        \                                 /
         WatchfulHoldsService  --  IWatchfulProvinceLookup (RealmProvinceLookup)
              /        \
WatchfulHoldsBehavior   IWatchfulHoldsAdapter / IWatchfulHoldsNoticeAdapter
(HourlyTickPartyEvent,  (TaleWorlds reads, losses, message, horn)
 DailyTickEvent)
```

## Configuration

### Config File: `Main/_Module/ModuleData/watchful_holds/watchful_holds.json`

Read once per game session: an edit needs a full restart of Bannerlord. A missing file gives the compiled defaults with a warning, a parse failure the same with an error, and an invalid field reverts alone to its default with a warning in the TAOM log. Every float goes through `FiniteFloatValidator` before the range check.

| Field | Shipped | Valid range | Description |
|-------|---------|-------------|-------------|
| `radius` | 5.0 | above 0, at most 50 | Map units from the fort inside which its garrison watches the road |
| `minGarrison` | 30 | 0 to 100000 | Fewest garrison troops a fort needs |
| `minPartySize` | 30 | 0 to 100000 | Fewest men a party needs to be harassed |
| `playerChancePerHour` | 0.30 | 0 to 1 | Hourly chance for the player |
| `aiChancePerHour` | 0.15 | 0 to 1 | Hourly chance for an AI lord |
| `woundFraction` | 0.02 | 0 to 1 | Share of the party's men wounded |
| `woundExtraMax` | 5 | 0 to 10000 | Random extra wounded, 0 to this |
| `killFraction` | 0.01 | 0 to 1 | Share of the party's men killed |
| `killExtraMax` | 1 | 0 to 10000 | Random extra killed, 0 to this |
| `foodLossFraction` | 0.05 | 0 to 1 | Share of the party's food lost |
| `moraleLoss` | 10 | 0 to 100 | Morale lost |
| `cohesionLoss` | 5 | 0 to 100 | Army cohesion lost per hit |
| `cohesionFloor` | 10 | 0 to 100 | A hit never takes army cohesion below this, and never raises a cohesion already below it |
| `hornCooldownHours` | 24 | at least 0 | Hours between two horn blasts |
| `cultures` | see below | each multiplier 0 to 5 | Multipliers by the culture of the fort's owner |

`cultures` is keyed by lowercase culture id (keys are trimmed and lowercased on load). `default` covers any culture without a row, and a missing `default` row is added at 1 and 1. The owner's culture is the owning clan's, with the realm's as the fallback. `chanceMultiplier` scales the hourly chance; `lossMultiplier` scales the fraction part of the kill and wound counts only, not the random extras.

| Culture | `chanceMultiplier` | `lossMultiplier` |
|---|---|---|
| `default` | 1.0 | 1.0 |
| `mordor` | 1.25 | 1.0 |
| `dolguldur` | 1.25 | 1.0 |
| `isengard` | 1.25 | 1.0 |
| `lothlorien` | 1.25 | 1.0 |
| `mirkwood` | 1.25 | 1.0 |

### MCM settings

Group "World/Watchful Holds" in `Main/Features/TaomSettings.cs`. Both apply at once, no restart. Neither is in `CoopSettingsRelevance`, since they change what the campaign simulates. The hints explain disorganized, the cohesion floor and what the AI toggle leaves on the player's army.

| Setting | Property | Default | Effect |
|---|---|---|---|
| Enable Watchful Holds | `EnableWatchfulHolds` | on | Master switch; off means forts never harass anyone and the service reads nothing |
| Harass AI lords | `WatchfulHoldsHarassAiLords` | on | Off keeps the effect on the player's party and the army it leads (AI lords marching with the army still take their share); does nothing while the master switch is off |

## Vanilla disorganized effect

"Disorganized" is the engine's own state, set through `MobileParty.SetDisorganized(true)`. TAOM changes none of its numbers:

- **Duration:** a base of 6 hours (`DefaultPartyImpairmentModel`); the leader's Calculating trait and the Scouting Foragers perk change it.
- **Speed:** `DefaultPartySpeedCalculatingModel` adds a -0.4 factor to the base map speed.
- **Battle morale:** `SandboxBattleMoraleModel` gives the party's troops a -0.2 factor on starting battle morale, unless the party has the Improviser perk.

The adapter asks the model's `CanGetDisorganized` for each party first.

TAOM adds one effect of its own: the field camp cannot be made while the player's party is disorganized
(`FieldCampOverlayVM` through `MapScreenCampMenuActivationQuery`).

## Diagnostics and performance budget

A daily debug line, written from `DailyTickEvent`, logs and then resets the counters:

```
[WatchfulHolds] day N: checks=..., aiTriggers=..., playerTriggers=..., maxHourMs=...
```

`checks` counts every lord or main party the service was called for while the feature was on, and `maxHourMs` is the most time the service itself spent in any single campaign hour that day. It excludes the behavior's own lord filter and co-op probe.

**Proposed budget:** one hourly pass over every party costs under 1 ms in total, read from `maxHourMs`. This is a target to confirm in game, not a measured result. It stays cheap this way: the behavior drops a non-lord party after two flag reads, the service reads the fort positions once per campaign into an array, a lord near no fort scans every fort but allocates nothing (the in-range list allocates only when a fort is inside the radius), and live owner, siege and garrison reads happen only for forts inside the radius that pass the province gate.

## Co-op, save data and IoC

- **Co-op:** Watchful Holds runs in singleplayer only. Both handlers return while `IsSessionActive || ShouldDeferToHost`, so it stands down in any co-op session (BannerlordCoop host and client) and under an unprobeable co-op mod (BannerlordTogether), like Eye of Sauron: it changes the outcome of parties other peers control, and `IsAuthority` fails open under BannerlordTogether. Each handler is wrapped in a try and catch that logs the error.
- **Save data:** none. `SyncData` is empty. The horn clock, the diagnostic counters and the cached fort list are per campaign, reset by the behavior's constructor through `ResetForNewSession`, because the module builds the behavior once per campaign start.
- **IoC:** `WatchfulHoldsIoC.RegisterWatchfulHoldsFeature` registers all services as singletons. `IRandomSource` (an InitialChildGeneration type), `IEnlistmentStateQuery`, `ICoopSessionProvider`, `IPathService` and the logger are wired earlier in `IoC.Configure`; `RealmTerritoryService` and `IRealmBordersSettings` come from the Realm Borders module.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/WatchfulHolds/WatchfulHoldsService.cs` | Every rule: filters, fort search, roll, losses, horn clock, daily summary |
| `Main/Features/WatchfulHolds/IWatchfulHoldsService.cs` | Service interface |
| `Main/Features/WatchfulHolds/Hooks/WatchfulHoldsBehavior.cs` | Campaign behavior, lord filter, single-player gate, guarded handlers |
| `Main/Features/WatchfulHolds/WatchfulHoldsModule.cs` | Feature module declaration |
| `Main/Features/WatchfulHolds/WatchfulHoldsIoC.cs` | DryIoc registration |
| `Main/Features/WatchfulHolds/WatchfulHoldsConfig.cs` | Config model, limits and compiled defaults |
| `Main/Features/WatchfulHolds/WatchfulHoldsConfigProvider.cs` | JSON load and validation |
| `Main/Features/WatchfulHolds/WatchfulHoldsSettingsProvider.cs` | Reads the two MCM toggles |
| `Main/Features/WatchfulHolds/RealmProvinceLookup.cs` | Province gate over Realm Borders |
| `Main/Features/WatchfulHolds/WatchfulLossMath.cs` | Loss counts, clamping, cohesion floor and food spreading, no engine |
| `Main/Features/WatchfulHolds/Models/` | `WatchfulPartySnapshot`, `FortSite`, `FortStatus` |
| `Main/Adapters/IWatchfulHoldsAdapter.cs`, `Main/Adapters/WatchfulHoldsAdapter.cs` | Party snapshots, fort list, war check, losses, food, morale, cohesion |
| `Main/Adapters/IWatchfulHoldsNoticeAdapter.cs`, `Main/Adapters/WatchfulHoldsNoticeAdapter.cs` | The red message and the horn |
| `Main/_Module/ModuleData/watchful_holds/watchful_holds.json` | Configuration data |
| `Main/Features/TaomSettings.cs` | The two MCM toggles |

## Dependencies

- `IEnlistmentStateQuery` (Enlistment): the enlisted player is exempt.
- `IWatchfulProvinceLookup` over `RealmTerritoryService` and `IRealmBordersSettings` (Realm Borders): the province gate.
- `ICoopSessionProvider` (CoopInterop): the single-player gate.
- `IRandomSource` (InitialChildGeneration), `IModLogger`, `IPathService` (Core).

## Tests

MSTest with NSubstitute and hand-written fakes, in `TAOM.Tests/Features/WatchfulHolds/`:

- `WatchfulHoldsServiceTests.cs`: every exemption, the radius and two-dimensional distance, nearest-fort choice, the equal-distance tie-break and fall-through, siege, garrison and war checks, the province gate (one lookup per party, only the province's fort read) and its fallback, the fort list read once per campaign, player and AI chances, culture multipliers, loss rounding and clamping with every loss field varied from the config, per-party food and morale in a player army, cohesion loss and floor from the config, the horn cooldown, the toggles, the daily line and the reset.
- `WatchfulHoldsConfigProviderTests.cs`: one test per validation rule, the shipped JSON against the compiled defaults, and its culture keys against the main culture ids.
- `WatchfulHoldsWiringTests.cs`: module, behavior and registration wiring, including the per-campaign reset and the single-player gate in both handlers.
- `WatchfulLossMathTests.cs`: counts, clamping, the cohesion floor, food spreading.
- `RealmProvinceLookupTests.cs`: the province lookup, including Realm Borders switched off.
- `WatchfulHoldsSettingsProviderTests.cs`: the two toggles.
- `WatchfulHoldsRig.cs`: the shared test rig and fakes.

## How to change the tuning

1. Edit `Main/_Module/ModuleData/watchful_holds/watchful_holds.json` (for a culture, add or change a row under `cultures` keyed by the lowercase culture id).
2. Keep each value inside its range above, or it reverts to the default with a warning in the TAOM log.
3. Restart Bannerlord: the file is read once per session. No code change is needed.

## Still owed

- **In-game check:** march the player past an enemy castle and an enemy town and check the message, the losses and the horn. Check that nothing happens while enlisted, under siege or at peace.
- **Balance runs:** two fast-forward runs from one save to day 120, with AI harassment on and off. Compare the fiefs captured and the daily trigger counts from the `[WatchfulHolds]` lines.
- **Translation:** `taom_watchful_holds_harried` is translated in all 12 languages (2026-11-12, #763).
- **Budget check:** confirm the under 1 ms hourly pass from `maxHourMs` in a real campaign.

## Changelog

- 2026-10-18: Watchful Holds added (#822).

## GitHub Issue

- **Issue:** #822, Watchful Holds: hostile castles and towns harass passing lord parties
- **Status:** Shipped on `bannerlord-1.5.x` in `af66af81f` (v2.0.35). Issue open: the in-game check and the two balance runs are owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/realm-borders.md](./realm-borders.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
