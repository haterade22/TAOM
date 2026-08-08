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
| Evil Faction Aggression Scale | 1.0 | 0.5–3.0 | Global multiplier on top of per-faction `FactionAggressionMultipliers`. Raise to make evil factions siege when outnumbered. |
| Long-Range Priority Boost Scale | 1.0 | 1.0–5.0 | Global multiplier on top of per-faction `FactionDistanceRangeMultipliers`. Raise if priority targets are ignored due to distance. |
| Border Proximity Floor | 0.15 | 0.0–1.0 | Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range (no neighboring friendly forts). 0.0 = vanilla behaviour. |

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
| `Main/Features/ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs` | Harmony Postfix — substitutes border proximity floor for priority-list targets |
| `Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs` | Patch49 Harmony Finalizer — swallows the vanilla siege-start NRE + records diagnostics |
| `Main/Features/ArmyTargeting/Diagnostics/SiegeGatheringFailureInfo.cs` | Boundary DTO + `FromArmy` factory (sole sealed-type reader; fortification census) |
| `Main/Features/ArmyTargeting/Diagnostics/SiegeGatheringFailureReason.cs` | Failure classification enum |
| `Main/Features/ArmyTargeting/Diagnostics/ISiegeGatheringDiagnosticsService.cs` | Diagnostics service interface |
| `Main/Features/ArmyTargeting/Diagnostics/SiegeGatheringDiagnosticsService.cs` | Classify + dedup + format + WARNING/DEBUG log |
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
- `TAOM.Tests/Features/ArmyTargeting/SiegeGatheringDiagnosticsServiceTests.cs` — 14 tests:
  - `Classify` (6): kingdom-null, no-fortifications, all-under-siege, fortifications-available (NoReachable), counts-unavailable, null-info
  - `Record` (4): first occurrence → WARNING once, same siege twice → WARNING+DEBUG, distinct sieges → WARNING each, null info → no log
  - `Format` (4): key fields present, counts-unavailable → `n/a`, NaN positions → `?`, null/blank fields render safely
  - (`FromArmy` boundary + the finalizer are in-game-validated per ADR-008 — no unit tests)

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

### Phase 2 — Border Proximity Harmony Patch

`AiMilitaryBehavior.CalculateDistanceScoreForBesieging` runs **before** our `GetTargetScoreForFaction` override. It checks how many of a target settlement's topological fortification neighbors belong to the attacker. If the answer is 0 (typical for distant priority targets on the large TAOM map), it returns `bestDistanceScore = 0` — causing `finalScore = 0 × base = 0` regardless of what our model returns.

**Implemented:** `Patch22_ArmyTargeting` — Harmony Postfix on `CalculateDistanceScoreForBesieging`. When `bestDistanceScore == 0` and the target is in the faction's priority list, substitutes `BorderProximityFloor` (MCM, default 0.15). This ensures `GetTargetScoreForFaction` is at least called for priority targets.

## Patch49 — Siege-gathering dead-end guard + diagnostics

### The vanilla crash

`Army.FindBestGatheringSettlementAndMoveTheLeader` (called from `Army.OnSiegeStarted` on the map tick that starts a siege) dereferences `settlement.GatePosition` at `Army.cs:726` **with no null guard** when a besieger army can't resolve a gathering fortification — every `Kingdom.Settlements` fortification is under siege / out of range **and** `SettlementHelper.FindNearestFortificationToMobileParty` returns null. A null `Kingdom` (kingdomless leader clan) throws in the same method at `Army.cs:659`. This is a vanilla missing-null-guard; TAOM's aggressive cross-map siege targeting (Patch22, above) only makes it more reachable. Crash report 2026-06-17 (issue #285), Bannerlord v1.4.6.

### The guard

`Patch49_ArmyGatheringNreGuard` — a Harmony **Finalizer** on `FindBestGatheringSettlementAndMoveTheLeader` that swallows **only** `NullReferenceException` (rethrows anything else) and returns null. The broken army skips relocating its gathering leader this tick (vanilla already null-guards `AiBehaviorObject` downstream at Army.cs:480-490/564) and re-plans next tick — strictly better than a CTD. Finalizer (not Prefix) is the right pattern for a *managed* NRE; contrast Patch47/48/57's prevent-the-call Prefixes for *native* AVs a finalizer can't catch. Registered unconditionally in `SubModule.OnSubModuleLoad` right after `Patch22_ArmyTargeting`.

> **Debugger note:** a Finalizer runs *after* the throw, so under an attached debugger with break-on-`NullReferenceException` enabled this still surfaces as a **first-chance exception** at `Army.cs:726` (`Source = "0Harmony"`, a `[Lightweight Function]` frame). That is expected — pressing Continue lets the finalizer suppress it; there is no CTD in shipped play. Don't chase the first-chance break; read the diagnostics log instead.

### The diagnostics (what makes dead-end sieges reviewable)

The finalizer records the failure context to `ISiegeGatheringDiagnosticsService` before swallowing, turning a silent breadcrumb into a to-fix list. The whole diagnostic path is inside the finalizer's try/catch, so if it ever throws the NRE is still suppressed — the crash guard is never weakened.

- **Boundary DTO** `SiegeGatheringFailureInfo.FromArmy(Army, Settlement)` — the sole reader of sealed types (ADR-007 boundary, mirrors `TownFoodSnapshot.FromTown`). Every access is null-guarded; it walks `Kingdom.Settlements` **once** to census `total` / `under-siege` fortifications (no MapDistanceModel re-run).
- **Classification** (`SiegeGatheringFailureReason`, inferred from the census): `KingdomNull` → `NoFortifications` → `AllFortificationsUnderSiege` → `NoReachableFortification` (the interesting map/navmesh case — fortifications exist but none is navigable from the leader party) → `Unknown`.
- **Service** `SiegeGatheringDiagnosticsService` dedups by `(kingdomId, focusSettlementId)`: the **first** occurrence logs full detail at **WARNING**; repeats increment a counter and drop to DEBUG so WARNINGs never spam. Routed through the existing `IModLogger` → `Logs/taom_debug_{timestamp}.log`; grep the `[SiegeDiag]` tag.

Example line (one per distinct problem siege):

```
[WARNING] [SiegeDiag] Army 'Yazdâr Army' (leader Yazdâr, clan clan_sh_8, kingdom Shaghâna [Shaghana])
could not resolve a gathering fortification for focus 'Kôth Rau' [town_SH_koth_rau, culture shaghana,
faction Shaghana]. Reason=NoReachableFortification. Fortifications: 6 total, 2 under siege.
Leader@(412.3,180.7) focus@(455.1,205.9). Time=Winter 3, 1118.
```

To fix a flagged siege: look at the kingdom's fortification census + the leader/focus positions. `AllFortificationsUnderSiege` is usually transient; `NoFortifications` / `NoReachableFortification` point at map data (give the kingdom a reachable fortress, or a navmesh/region gap between the leader party and its nearest fort — e.g. the TAOM_Map naval navmesh gaps, #120).

## Changelog

- 2026-07-05 — Enriched `Patch49_ArmyGatheringNreGuard` with `ISiegeGatheringDiagnosticsService`: the finalizer now records army/kingdom/focus-settlement context + a fortification census, deduplicated into one `[SiegeDiag]` WARNING per problem siege, so dead-end sieges are reviewable instead of a silent breadcrumb. +14 tests. Guard behavior unchanged.
- 2026-06-17 — Added `Patch49_ArmyGatheringNreGuard` (issue #285): Harmony Finalizer swallowing the vanilla siege-start NRE in `Army.FindBestGatheringSettlementAndMoveTheLeader` (`Army.cs:726` null `GatePosition` / `:659` null `Kingdom`).
- 2026-04-04 — Phase 2: added `Patch22_ArmyTargeting` Harmony Postfix on `CalculateDistanceScoreForBesieging` substituting a "Border Proximity Floor" (default 0.15) for priority targets vanilla scores at 0.
- 2026-04-04 — Added evil-faction aggression (`FactionAggressionMultipliers` strength-gate bypass) + large-map distance compensation (`FactionDistanceRangeMultipliers`) to `TaomTargetScoreModel`, with MCM aggression/long-range sliders.
- 2026-04-03 — Initial feature: `TaomTargetScoreModel` adds Besieger army commitment stickiness + JSON faction priority lists, with MCM toggle, Commitment Multiplier, and Priority List Boost.

## GitHub Issue

- **Issue:** [#64 — feat: AI Strategic Intelligence — army commitment stickiness + faction priority target lists](https://github.com/haterade22/TAOM/issues/64)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
