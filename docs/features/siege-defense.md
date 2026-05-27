# Siege Defense

## Overview

When a town belonging to the player's kingdom (or a kingdom they are serving as mercenary) is besieged, the player receives a popup asking whether to help defend. Accepting opens a 3-day campaign-time window; if the player arrives at the settlement while the siege is still active, they receive a relation boost with the defending faction and an influence reward. The besieged settlement shows the native tracking circle on the campaign map while the event is active.

## Why This Exists

Vanilla Bannerlord has no mechanism to alert the player when their allies' settlements are attacked. The player only learns about sieges by stumbling across them on the map. In TAOM's LOTR setting — where Mordor besieging Minas Tirith or Isengard attacking Helm's Deep are defining story moments — having a passive discovery model breaks narrative immersion.

- **Vanilla behavior:** No notification; player discovers sieges organically by map scanning
- **TAOM requirement:** Active alert with a timed response window, matching the "call to arms" mechanic seen in other LOTR total conversion mods
- **Without this feature:** Players routinely miss sieges of their own kingdom's key towns

## Architecture

### Design Challenge

`SiegeEvent` (the class holding siege state) is sealed. `Settlement` is sealed. Both must be wrapped at the adapter boundary before entering the service layer. Additionally, the player's kingdom membership must be checked dynamically — the earlier config-driven `WatchedFactionIds` list was replaced because it fired even when the player had no political relationship to the faction.

### Solution Approach

Pure `CampaignEvents` listeners — no Harmony patches. `SiegeDefenseBehavior` subscribes to `OnSiegeEventStartedEvent`, `OnSiegeEventEndedEvent`, `OnSettlementOwnerChangedEvent`, and `HourlyTickEvent`, delegating all logic to `SiegeDefenseService`. The service uses `IPlayerContextAdapter` to read the player's current kingdom ID at runtime, covering both regular membership and mercenary service (Bannerlord sets `Clan.Kingdom` in both cases).

Map tracking uses `Campaign.Current.VisualTrackerManager.RegisterObject(settlement)` — the same API quests use — which renders the native white tracking circle above the settlement.

### Component Diagram

```
siege_defense_config.json
          |
  SiegeDefenseConfigProvider
          |
   SiegeDefenseService ──── IPlayerContextAdapter (Clan.PlayerClan.Kingdom)
          |
   SiegeDefenseBehavior (CampaignBehaviorBase)
     |    |    |    |
     |    |    |    └── HourlyTickEvent → check player arrival, grant reward
     |    |    └── OnSiegeEventEnded / OnSettlementOwnerChanged → cleanup
     |    └── OnSiegeEventStarted → ISiegeEventAdapter → eligibility check → popup
     └── VisualTrackerManager.RegisterObject / RemoveTrackedObject
```

## Configuration

### Config File: `Main/_Module/ModuleData/siege/siege_defense_config.json`

| Field | Type | Description |
|-------|------|-------------|
| `WatchedSettlementIds` | `string[]` | Explicit settlement IDs that always trigger, regardless of player kingdom. Empty by default. |
| `RelationshipThreshold` | int | Reserved for future relationship-gated filtering. Currently unused in eligibility. |
| `ResponseWindowDays` | int | Default response window in campaign days. Overridden by MCM if set. |
| `RewardRelation` | int | Relation points granted to defender faction leader on arrival. |
| `RewardInfluence` | int | Influence granted to player clan on arrival. |

### Current Values

| Field | Value | Notes |
|-------|-------|-------|
| `ResponseWindowDays` | 3 | MCM overrides this at runtime (range 1–14) |
| `RewardRelation` | 5 | Modest — helping a siege is not a major political event |
| `RewardInfluence` | 10 | Scaled to be meaningful without being exploitable |
| `WatchedSettlementIds` | `[]` | Empty — player kingdom check is the primary filter |

### MCM Settings (`TaomSettings.cs`, group "Siege Defense")

| Setting | Default | Range |
|---------|---------|-------|
| Enable Siege Defense Events | `true` | on/off |
| Response Window (Days) | 3 | 1–14 |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Siege/SiegeDefenseService.cs` | All eligibility, tracking, and reward logic |
| `Main/Features/Siege/ISiegeDefenseService.cs` | Service interface + `ActiveEvents` for test inspection |
| `Main/Features/Siege/SiegeDefenseBehavior.cs` | Thin CampaignBehaviorBase — event registration only |
| `Main/Features/Siege/SiegeDefenseIoC.cs` | DryIoc registrations (all Singleton) |
| `Main/Features/Siege/ISiegeDefenseConfigProvider.cs` | Config loader interface |
| `Main/Features/Siege/SiegeDefenseConfigProvider.cs` | Loads JSON; returns safe defaults on any failure |
| `Main/Features/Siege/ISiegeDefenseSettingsProvider.cs` | MCM wrapper interface |
| `Main/Features/Siege/SiegeDefenseSettingsProvider.cs` | Reads `TaomSettings.Instance` |
| `Main/Features/Siege/Models/SiegeDefenseConfig.cs` | Config POCO |
| `Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs` | Runtime tracking state per settlement |
| `Main/Adapters/ISiegeEventAdapter.cs` | Adapter interface for sealed `SiegeEvent` |
| `Main/Adapters/SiegeEventAdapter.cs` | Wraps `SiegeEvent` with `?.` throughout |
| `Main/Adapters/IPlayerContextAdapter.cs` | Interface — `GetPlayerKingdomId()`, `IsUnderMercenaryService()` |
| `Main/Adapters/PlayerContextAdapter.cs` | Wraps `Clan.PlayerClan` (sealed) |
| `Main/_Module/ModuleData/siege/siege_defense_config.json` | Default config |
| `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs` | 17 unit tests |

## Dependencies

- `IPathService` (Core) — provides `ModuleDataPath` for JSON loading
- `IModLogger` (Core) — structured logging throughout
- `IPlayerContextAdapter` (Adapters) — reads `Clan.PlayerClan.Kingdom.StringId`; covers regular members and mercenaries via the same property
- `Campaign.Current.VisualTrackerManager` — native tracking circle; reference-counted, `forceRemove: true` used on cleanup to avoid leaving orphaned trackers
- `InformationManager.ShowInquiry` / `DisplayMessage` — player UI; wrapped in try/catch (unavailable outside live campaign)

## Tests

- `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs` — 17 tests covering:
  - Player kingdom match → fires; different kingdom → suppressed; no kingdom → suppressed
  - Mercenary service → fires (same code path as regular member)
  - `WatchedSettlementIds` override → fires regardless of player kingdom
  - Not a town (castle/village) → suppressed
  - MCM disabled → suppressed
  - Duplicate suppression (second `OnSiegeStarted` for same settlement)
  - `OnSiegeEnded` removes from active tracking
  - Unknown settlement `OnSiegeEnded` does not throw
  - Config loaded at construction
  - Active event defaults (`PlayerAccepted = false`, `RewardClaimed = false`, correct `DefenderFactionId`)

Not tested (require live `Campaign.Current`): `OnHourlyTick` reward granting, `GrantReward`, `TrackSettlement`/`UntrackSettlement`.

## How to Add a Specific Settlement Override

If a particular settlement should always trigger the event regardless of player kingdom (e.g., a scripted story settlement):

1. Open `Main/_Module/ModuleData/siege/siege_defense_config.json`
2. Add the settlement's `StringId` to `WatchedSettlementIds`

```json
{
  "WatchedSettlementIds": ["town_EW1"],
  ...
}
```

No code changes needed.

## How to Tune Rewards

Edit `siege_defense_config.json`:
- `RewardRelation` — relation points with defender faction leader (int)
- `RewardInfluence` — clan influence awarded (int)

Changes take effect on next game load (config is loaded at construction).

## Performance

`IsWatchedSiege` is called once per `OnSiegeEventStarted` — not on any tick. The `OnHourlyTick` loop only iterates `_activeEvents` (typically 0–3 entries). No allocations outside of dictionary lookups. `VisualTrackerManager` uses reference counting internally; `forceRemove: true` ensures clean teardown even if multiple callers registered the same settlement.

## GitHub Issue

- **Issue:** haterade22/TAOM#67 — feat: Siege Defense — timed settlement defense events for player kingdom
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
