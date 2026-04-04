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
2. **Priority list boost:** If the army's faction has a priority list in `army_targeting.json`, earlier entries receive up to `MaxPriorityBoost` (MCM, default 3×) decaying linearly to 1× at the last entry.
3. **Strength gate bypass:** Inflates `ourStrength` **before** calling base, bypassing the vanilla `2× defender strength` hard gate that causes evil factions to sit idle. A per-faction `FactionAggressionMultipliers` value of 2.0 lets a faction siege at 1:1 parity.
4. **Distance compensation:** Post-multiplier applied to base score for priority-list targets, countering vanilla's `num21` distance curve that makes targets >5× average-town-distance score ~11× lower. Configured via `FactionDistanceRangeMultipliers` in the JSON — only applies to settlements already in the faction's priority list.

Priority list advancement is stateless — captured settlements disappear from the enemy settlement pool, so the next unconquered entry naturally becomes the highest-boosted target with no tracking required.

Raider and Defender armies are unaffected (only `Besieger` is intercepted). If `baseScore <= 0`, the guard returns early without multiplying.

### Component Diagram

```
army_targeting.json
        |
ArmyTargetingConfigProvider (loads + caches)
        |
ArmyTargetingService
  - _priorityIndex:      Dict<factionId, Dict<settlementId, index>>  (built once at startup)
  - _aggressionIndex:    Dict<factionId, float>                       (built once at startup)
  - _distanceRangeIndex: Dict<factionId, float>                       (built once at startup)
  - GetTargetMultiplier(candidateId, committedTargetId?, factionId?) -> float
  - GetStrengthMultiplier(factionId?) -> float     (inflates ourStrength pre-gate)
  - GetDistanceCompensation(factionId?, targetId?) -> float   (post-multiplier for distant priority targets)
        |
TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
  - GetTargetScoreForFaction(Settlement, ArmyTypes, MobileParty, float)
  - extracts factionId/StringId primitives at boundary
  - inflates ourStrength → calls base → multiplies by targetMultiplier × distanceCompensation

MCM (TaomSettings)
  - EnableArmyStrategicIntelligence
  - ArmyCommitmentMultiplier
  - ArmyPriorityBoost
  - EvilFactionAggressionScale
  - LongRangePriorityBoostScale
        |
ArmyTargetingSettingsProvider (reads TaomSettings.Instance with defaults)
```

## Configuration

### MCM Settings (in-game, "AI Strategic Intelligence" group)

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Enable AI Strategic Intelligence | true | bool | Master toggle. When off, all service methods return 1.0 immediately (no-op). |
| Commitment Multiplier | 4.0 | 1.0–10.0 | How strongly an army commits to its current target. Vanilla implicit = 1.3. |
| Priority List Boost | 3.0 | 1.0–5.0 | Score multiplier for the first entry in a faction's priority list. Decays to 1.0 at last entry. |
| Evil Faction Aggression Scale | 1.0 | 0.5–3.0 | Global multiplier applied on top of per-faction `FactionAggressionMultipliers` values. Raise to make evil factions siege even when outnumbered; set to 0.5 for vanilla-like caution. |
| Long-Range Priority Boost Scale | 1.0 | 1.0–5.0 | Global multiplier applied on top of per-faction `FactionDistanceRangeMultipliers` values. Raise if priority-list targets are ignored due to distance penalty. |

### Config File: `Main/_Module/ModuleData/configs/army_targeting.json`

Three sections. All optional — missing entries default to vanilla behaviour.

```json
{
  "FactionPriorityTargets": {
    "<faction_id>": ["<settlement_id>", ...]
  },
  "FactionAggressionMultipliers": {
    "<faction_id>": 2.0
  },
  "FactionDistanceRangeMultipliers": {
    "<faction_id>": 1.5
  }
}
```

- **`FactionPriorityTargets`:** Ordered target lists. Earlier entries score higher (decaying from `MaxPriorityBoost` to 1.0).
- **`FactionAggressionMultipliers`:** How much to inflate `ourStrength` before the vanilla `2× defender` strength gate check. At 2.0, the faction can siege at 1:1 parity instead of requiring 2:1.
- **`FactionDistanceRangeMultipliers`:** Post-score multiplier for priority-list targets suffering the vanilla distance penalty (`num21`). Only applies to settlements already in `FactionPriorityTargets` for that faction.

### Current Priority Lists

| Faction | Faction ID (JSON key) | Priority Sequence |
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

**Faction ID notes (keys are faction StringIds, not culture StringIds):**
- `empire_s` = Mordor (Southern Empire, `Culture.empire` — distinct from Gondor/Dunland by faction ID)
- `empire_w` = Gondor (Western Empire, `Culture.empire`)
- `empire` = Dunland/Dunlendings (Northern Empire, `Culture.empire`)
- `khuzait` = Rhun/Easterlings (faction and culture IDs match)
- `sturgia` = Dale/Barding (faction and culture IDs match)
- `battania` = Khand (intentionally no list — neutral faction)
- `dolguldur` = Dol Guldur (no underscore in both faction and culture ID)

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

**Important:** The config keys are **faction StringIds** (e.g. `empire_s`, `empire_w`), NOT culture StringIds. Three factions share `Culture.empire` (Mordor=empire_s, Gondor=empire_w, Dunland=empire), so using culture IDs would cause Mordor armies to receive Dunland's priority list.

## Tests

- `TAOM.Tests/Features/ArmyTargeting/ArmyTargetingServiceTests.cs` — 20 tests:
  - `GetTargetMultiplier` (12): feature disabled, commitment stickiness, non-committed, null committed, first/middle/last priority entry, combined commitment+priority, null faction, unknown faction, empty list, single-entry list
  - `GetStrengthMultiplier` (5): feature disabled, null faction, configured value, unknown faction, scale applied
  - `GetDistanceCompensation` (3): faction not in priority list, target not in priority list, target in list with scale

## How to Add a New Faction Priority List

1. Determine the faction's **faction StringId** — for custom TAOM kingdoms check `TAOM_spkingdoms.xml` for `id=`; for vanilla kingdoms (empire_s, empire_w, empire, khuzait, sturgia, vlandia, battania, aserai) use the vanilla kingdom id. **Do NOT use culture StringId** — Mordor, Gondor, and Dunland all share `Culture.empire` so culture IDs are ambiguous.
2. Find the target settlement IDs — grep `settlements.xml` for `id="town_` filtered by the target region.
3. Add an entry to `army_targeting.json`:
   ```json
   "your_culture": ["town_X1", "town_X2", "town_X3"]
   ```
4. No code changes needed. The service reads the config at startup and builds the index automatically.

## Performance

`GetTargetScoreForFaction` is called O(armies × settlements) per 3h AI tick (~500–2000 calls per cycle at TAOM scale). All three service methods are allocation-free:

| Method | Cost | Notes |
|--------|------|-------|
| `GetStrengthMultiplier` | O(1) — 1 dict lookup | Called before base — must be minimal |
| `GetTargetMultiplier` | O(1) — 2 dict lookups | Commitment + priority index |
| `GetDistanceCompensation` | O(1) — 3 dict lookups | Priority check + distance scale |

- Feature disabled: immediate `return 1.0f` (single bool check) in all three methods
- All three indexes (`_priorityIndex`, `_aggressionIndex`, `_distanceRangeIndex`) built once at service construction — zero rebuilding at runtime
- All lookups use `TryGetValue` — no `ContainsKey` + indexer double-lookup
- No LINQ, no string allocation, no collection creation per call

### Phase 2 — Harmony Patch (Future)

If evil factions still completely ignore priority targets despite high aggression values, the cause is `AiMilitaryBehavior.CalculateDistanceScoreForBesieging` — a private method that runs **before** our override and returns `0` if the target has no topological fortification neighbors from the attacking faction. Our override is never called when this returns 0. Fix: Harmony Postfix on `CalculateDistanceScoreForBesieging` to substitute a minimum floor score (e.g. 0.15) for priority-list targets, gated on a `MinBorderProximityFloor` config value.

## GitHub Issue

- **Issue:** [#64 — feat: AI Strategic Intelligence — army commitment stickiness + faction priority target lists](https://github.com/haterade22/TAOM/issues/64)
- **Status:** Open
