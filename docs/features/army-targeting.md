# AI Strategic Intelligence — Army Targeting

## Overview

Prevents AI Besieger armies from thrashing targets every 3 hours by amplifying the score of the army's current target (commitment stickiness). Faction-specific ordered priority lists (Mordor → Osgiliath → Minas Tirith; Isengard → Helm's Deep → Edoras) guide initial target selection and natural advancement when settlements fall.

## Why This Exists

- **Vanilla behavior:** `AiMilitaryBehavior` re-scores all candidate settlements every 3 hours. A new settlement that scores marginally higher causes the army to divert mid-march, abandoning its current objective and wasting food and influence.
- **TAOM requirement:** LOTR factions need coherent strategic campaigns — Mordor should methodically push west through Osgiliath toward Minas Tirith, not bounce between targets.
- **Without this feature:** Armies never reach their targets. Sieges rarely complete. Dark faction pressure feels random rather than strategic.

## Architecture

### Design Challenge

The AI target-selection system runs deep inside `AiMilitaryBehavior.AiHourlyTick` — patching the private orchestrator is fragile. The cleaner intercept is the public virtual `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction`, which is called for every (army, settlement) candidate pair.

### Solution Approach

`TaomTargetScoreModel` overrides `GetTargetScoreForFaction`. For `Besieger` armies only, it multiplies the vanilla base score by a factor from `IArmyTargetingService`:

1. **Commitment stickiness:** If the candidate settlement is the army's current `AiBehaviorObject`, apply `CommitmentMultiplier` (MCM, default 4×). The alternative must score 4× better before the AI diverts.
2. **Priority list boost:** If the army's faction culture has a priority list in `army_targeting.json`, earlier entries receive up to `MaxPriorityBoost` (MCM, default 3×) decaying linearly to 1× at the last entry.

Priority list advancement is stateless — captured settlements disappear from the enemy settlement pool, so the next unconquered entry naturally becomes the highest-boosted target with no tracking required.

Raider and Defender armies are unaffected (only `Besieger` is intercepted). If `baseScore <= 0`, the guard returns early without multiplying.

### Component Diagram

```
army_targeting.json
        |
ArmyTargetingConfigProvider (loads + caches)
        |
ArmyTargetingService
  - _priorityIndex: Dict<cultureId, Dict<settlementId, index>>  (built once at startup)
  - GetTargetMultiplier(candidateId, committedTargetId?, cultureId?) -> float
        |
TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
  - GetTargetScoreForFaction(Settlement, ArmyTypes, MobileParty, float)
  - extracts StringId primitives at the boundary
  - calls base first, then multiplies by service result

MCM (TaomSettings)
  - EnableArmyStrategicIntelligence
  - ArmyCommitmentMultiplier
  - ArmyPriorityBoost
        |
ArmyTargetingSettingsProvider (reads TaomSettings.Instance with defaults)
```

## Configuration

### MCM Settings (in-game, "AI Strategic Intelligence" group)

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable AI Strategic Intelligence | true | bool | Master toggle. When off, service returns 1.0 immediately (no-op). |
| Commitment Multiplier | 4.0 | 1.0–10.0 | How strongly an army commits to its current target. Vanilla implicit = 1.3. |
| Priority List Boost | 3.0 | 1.0–5.0 | Score multiplier for the first entry in a faction's priority list. Decays to 1.0 at last entry. |

### Config File: `Main/_Module/ModuleData/configs/army_targeting.json`

Faction culture IDs mapped to ordered lists of target settlement IDs. Earlier entries receive higher score boosts. When all entries are captured the list exhausts naturally (no boost applied).

```json
{
  "FactionPriorityTargets": {
    "<culture_id>": ["<settlement_id>", ...]
  }
}
```

### Current Priority Lists

| Faction | Culture ID | Priority Sequence |
|---------|------------|------------------|
| Mordor | `mordor` | EW3 (E.Osgiliath) → EW2 (W.Osgiliath) → EW1 (Minas Tirith) → EW4 (Pelargir) |
| Isengard | `isengard` | V2 (Helm's Deep) → V1 (Edoras) |
| Gundabad | `gundabad` | M1/M2 (Mirkwood) → S1/S5/S4/S3/S2 (Dale region) → E1/E2/E3/E4 (Erebor) → R1 (Rivendell) |
| Dol Guldur | `dolguldur` | L1 (Lothlorien) → S1/S5/S4/S3/S2 (Dale) → M1/M2 (Mirkwood) → E1-E4 (Erebor) → R1 (Rivendell) |
| Rhun/Easterlings | `khuzait` | E4/E3/E2/E1 (Erebor, nearest first) → S5/S4/S3/S1/S2 (Dale) |
| Gondor | `gondor` | Interleaved ES (Mordor) + A (Harad): ES2→A1→ES3→A2→ES1→A3→ES4→A4→ES5→A5→ES6→A6 |
| Dunland | `empire` | V7/V2/V5/V4/V1/V3/V6 (Rohan) → EW3/EW2/EW1/EW4 (Gondor) |
| Dale/Barding | `sturgia` | RU7/RU2/RU1/RU4/RU3/RU5/RU6/RU8 (Rhun, nearest first) → DG1 (Dol Guldur) |
| Erebor | `erebor` | RU7/RU2/RU1/RU4/RU3/RU5/RU6/RU8 (Rhun, nearest first) |

**No priority list (vanilla logic):** Rohan (`vlandia`), Harad/Shaghana/Abanissa (`aserai`), Khand (`battania`), Umbar (`umbar`), Mirkwood (`mirkwood`), Lothlorien (`lothlorien`), Rivendell (`rivendell`) — these factions either defend or have neutral standing.

**Culture ID notes:**
- `empire` = Dunland/Dunlendings (vanilla Northern Empire renamed via XSLT; EN-region towns)
- `khuzait` = Rhun/Easterlings (vanilla Khuzait; RU-region towns)
- `sturgia` = Dale/Barding (vanilla Sturgia; S-region towns)
- `battania` = Khand (vanilla Battania; K-region towns — intentionally no list, neutral faction)
- `dolguldur` = Dol Guldur (custom culture, no underscore)

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ArmyTargeting/Models/TaomTargetScoreModel.cs` | GameModel override — intercepts score, extracts primitives, calls service |
| `Main/Features/ArmyTargeting/ArmyTargetingService.cs` | Core logic — commitment multiplier + O(1) priority index lookup |
| `Main/Features/ArmyTargeting/IArmyTargetingService.cs` | Service interface |
| `Main/Features/ArmyTargeting/ArmyTargetingConfig.cs` | JSON POCO |
| `Main/Features/ArmyTargeting/ArmyTargetingConfigProvider.cs` | Loads + caches `army_targeting.json` |
| `Main/Features/ArmyTargeting/IArmyTargetingSettingsProvider.cs` | Settings interface |
| `Main/Features/ArmyTargeting/ArmyTargetingSettingsProvider.cs` | Reads MCM `TaomSettings.Instance` with fallback defaults |
| `Main/Features/ArmyTargeting/ArmyTargetingIoC.cs` | DryIoc Singleton registration |
| `Main/Features/TaomSettings.cs` | MCM property declarations |
| `Main/_Module/ModuleData/configs/army_targeting.json` | Faction priority lists |

## Dependencies

- `IPathService` (Core) — resolves `ModuleData/configs/` path for config loading
- `IModLogger` (Core) — logs config load success/failure
- `IArmyTargetingSettingsProvider` (Feature) — reads MCM settings with safe defaults
- `IArmyTargetingConfigProvider` (Feature) — provides cached faction priority data

No TaleWorlds adapter interfaces are required — the service works exclusively with string IDs and floats.

## Tests

- `TAOM.Tests/Features/ArmyTargeting/ArmyTargetingServiceTests.cs` — 12 tests covering: feature disabled (no-op), commitment stickiness, non-committed target, null committed target, first/middle/last priority entry, combined commitment+priority, null culture, unknown culture, empty list, single-entry list.

## How to Add a New Faction Priority List

1. Determine the faction's culture ID — check `taom_spcultures.xml` for the `id=` attribute, or look at `battle_balance_config.json` (same IDs used there).
2. Find the target settlement IDs — grep `settlements.xml` for `id="town_` filtered by the target region.
3. Add an entry to `army_targeting.json`:
   ```json
   "your_culture": ["town_X1", "town_X2", "town_X3"]
   ```
4. No code changes needed. The service reads the config at startup and builds the index automatically.

## Performance

`GetTargetScoreForFaction` is called O(armies × settlements) per 3h AI tick (~4000 calls per cycle at TAOM scale). The service hot path (`GetTargetMultiplier`) is allocation-free:

- Feature disabled: immediate `return 1.0f` (single bool check)
- `_priorityIndex` is a `Dictionary<string, Dictionary<string, int>>` built once at service construction — zero rebuilding at runtime
- All lookups use `TryGetValue` (single hash lookup)
- No LINQ, no string allocation, no collection creation per call

## GitHub Issue

- **Issue:** [#64 — feat: AI Strategic Intelligence — army commitment stickiness + faction priority target lists](https://github.com/haterade22/TAOM/issues/64)
- **Status:** Open
