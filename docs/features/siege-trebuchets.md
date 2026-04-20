# Siege Trebuchets (Defender)

## Overview

Adds trebuchets to the list of siege engines defenders can construct on the campaign-map siege UI. In vanilla, only attackers can build trebuchets; defenders are limited to ballistas and catapults. This feature grants equal trebuchet access to defenders.

## Why This Exists

- **Vanilla behavior:** `DefaultSiegeEventModel.GetAvailableDefenderSiegeEngines` yields only Ballista/FireBallista/Catapult/FireCatapult. The attacker ranged list adds Trebuchet on top.
- **TAOM requirement:** Minas Tirith and other major LOTR strongholds should be able to return long-range fire when besieged. A Gondor defender lobbing stones back at an invading Mordor army matches the source material.
- **Without this feature:** Defenders are out-ranged by attacker trebuchets, making heroic defensive holds (Helm's Deep, Minas Tirith) feel underpowered on the campaign map.

## Architecture

### Design Challenge

The defender engine list is hardcoded in a C# iterator method on `DefaultSiegeEventModel`. Not data-driven via XML (`siegeengines.xml` only defines engine *properties*, not availability). No configuration knob exists.

### Solution Approach

Standard TAOM GameModel override pattern (ADR). `TaomSiegeEventModel` inherits `DefaultSiegeEventModel` and overrides only `GetAvailableDefenderSiegeEngines`. All other methods (attacker ranged, ram, tower, sally-out roster) fall through to base behavior.

Perk gating for `FireBallista`/`FireCatapult` is preserved — the override reproduces vanilla's `HasPerk(Stonecutters || SiegeEngineer)` check. `Trebuchet` is yielded unconditionally (no perk gate), mirroring how vanilla exposes it to attackers.

### Component Diagram

```
SubModule.OnGameStart
        |
campaignStarter.AddModel(new TaomSiegeEventModel())
        |
TaomSiegeEventModel : DefaultSiegeEventModel
        |
override GetAvailableDefenderSiegeEngines(PartyBase party)
  -> yields Ballista, [FireBallista], Catapult, [FireCatapult], Trebuchet
        |
MapSiegeProductionVM (vanilla) populates defender dropdown
```

## Configuration

None. Trebuchets are unconditionally available to every defender. To change this to a per-culture or per-settlement gate, convert the override to inject a service that reads a JSON/XML config — but that's a non-goal for the current scope.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Siege/Models/TaomSiegeEventModel.cs` | GameModel override (16 lines) |
| `Main/SubModule.cs` | Registers the model in `OnGameStart` |
| `CLAUDE.md` | Entry in the GameModel Overrides table |

## Dependencies

- `TaleWorlds.CampaignSystem.GameComponents.DefaultSiegeEventModel` (base class)
- `TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks` (for fire-variant perk checks — preserves vanilla gating)
- `TaleWorlds.Core.DefaultSiegeEngineTypes` (static engine type registry, populated at game init)

## Tests

No direct unit tests. `DefaultSiegeEngineTypes.Trebuchet` resolves via `Game.Current.DefaultSiegeEngineTypes._siegeEngineTypeTrebuchet` — which is null in unit test context. Per `csharp-architecture.md`: entry-point models are verified in-game, not via unit tests.

**Manual verification:**
1. Start a campaign, join a kingdom.
2. When an allied settlement is besieged, travel inside.
3. Open siege management UI. Confirm **Trebuchet** appears in the defender engine dropdown alongside Ballista and Catapult.
4. Construct it and verify it fires during the siege.

## How To Change the Defender Engine List

Edit `Main/Features/Siege/Models/TaomSiegeEventModel.cs`. Each `yield return` adds one engine. Candidates available in v1.3.15 `DefaultSiegeEngineTypes`: `Ballista`, `FireBallista`, `Catapult`, `FireCatapult`, `Onager`, `FireOnager`, `Bricole`, `Trebuchet`, `FireTrebuchet` **(avoid — v1.3.15 getter bug returns non-fire Trebuchet field)**.

## Known Issues

- **`FireTrebuchet` is broken in v1.3.15** — the getter returns the non-fire Trebuchet backing field. Do not yield it or players get duplicated Trebuchet entries. Re-check in a future Bannerlord update.
- Global scope: every defender everywhere gets trebuchets, not just Gondor. If this feels unthematic for, e.g., steppe castle defenders (Rhûn), convert to a culture-gated service.
