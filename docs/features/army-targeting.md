# AI Strategic Intelligence — Army Targeting

## Overview

Decides which settlement an AI army marches at. Four mechanisms, in the order they matter:

1. **Reach falloff** keeps armies on fronts they can actually supply. Vanilla's own distance factor clamps to a floor of 0.9x at *any* range, so the far side of the map costs an AI army about ten percent of score.
2. **War theaters** (`north`, `central`, `south`, `east`) softly bias each kingdom toward its own front. Weighting only: no war is ever forbidden.
3. **Commitment stickiness** stops Besieger armies re-optimising every 3 hours and abandoning a march.
4. **Priority lists** give scripted factions a coherent axis of advance (Mordor toward Osgiliath then Minas Tirith; Isengard toward Helm's Deep then Edoras).

A fifth, smaller lever raises the score of defending your own settlements.

## Why This Exists

- **Vanilla thrashing:** `AiMilitaryBehavior` re-scores all candidate settlements every 3 hours. A settlement that scores marginally higher makes the army divert mid-march, wasting food and influence.
- **Vanilla ignores distance for sieges.** `GetTargetScoreForFaction`'s distance factor is `MBMath.Map(...)`, which clamps to a **0.9x floor** no matter how far the target is, and its sibling `CalculateDistanceScoreForBesieging` uses pure two-hop fortification topology with no metric distance at all. Raiding and defending both use real distance; sieging does not. On a map as wide as TAOM's that makes a cross-map siege nearly free, which is what produced the player report of kingdoms fighting one map-wide brawl instead of a war with fronts.
- **TAOM made it worse before it made it better.** The priority lists shipped entries hundreds of map units away, and `Patch22` substituted a border floor *precisely when vanilla had scored the target unreachable*, which turned the priority list into an ignore-geography list. The `Patch49` registry entry already blamed that steering for the army-gathering NRE it guards.
- **Without this feature:** armies never reach their targets, sieges rarely complete, and a kingdom under attack at home marches to the opposite corner of the map.

## Architecture

### Design Challenge

The AI target-selection system runs deep inside `AiMilitaryBehavior.AiHourlyTick` — patching the private orchestrator is fragile. The cleaner intercept is the public virtual `DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction`, which is called for every (army, settlement) candidate pair.

### Solution Approach

`TaomTargetScoreModel` overrides `GetTargetScoreForFaction`. For `Besieger` armies only, it multiplies the vanilla base score by a factor from `IArmyTargetingService`:

1. **Commitment stickiness:** If the candidate settlement is the army's current `AiBehaviorObject`, apply `CommitmentMultiplier` (MCM, default 4×). The alternative must score 4× better before the AI diverts.
2. **Priority list boost:** If the army's faction has a priority list in `army_targeting.json`, earlier entries receive up to `MaxPriorityBoost` (MCM, default 3×) decaying linearly to 1× at the last entry.
3. **Strength gate bypass:** Inflates `ourStrength` **before** calling base, bypassing the vanilla `2× defender strength` hard gate that causes evil factions to sit idle. A per-faction `FactionAggressionMultipliers` value of 2.0 lets a faction siege at 1:1 parity.
4. **Reach falloff:** replaces vanilla's never-below-0.9 distance factor. `IMapReachAdapter` measures the candidate against the attacking faction's **nearest owned fortification**, normalised by the engine's average distance between the closest two towns ("town gaps", G). Score is flat at 1.0 out to `ReachInnerRadiusInTownGaps` (1.5 G), then decays linearly to `ReachFloor` (0.05) at `ReachRadiusInTownGaps` (3.0 G, about 280 map units).
5. **Theater weighting:** each kingdom holds an ordered theater list whose first entry is its primary front. A target owned by a primary-theater member scores `PrimaryTheaterWeight` (1.25), a shared non-primary theater `SecondaryTheaterWeight` (1.0), no shared theater `ForeignTheaterWeight` (0.35). Never zero.
6. **Home defence:** `Defender` missions take `ArmyDefenderPriority` (MCM, default 1.6).

**Why nearest fortification and not `FactionMidSettlement`.** The medoid distorts wide empires by up to 3.4x (Rhûn to Gondor is 167 path units from its nearest fort but 567 from its mid), and `Kingdom.CalculateMidSettlement` re-runs on `AddClanInternal`, its removal twin, `OnFortificationAdded` and `OnFortificationRemoved`, so the anchor drifts toward whatever a kingdom is currently conquering, widening its reach in the direction it is already winning.

**Why weighting and not a gate.** A hard theater gate was designed and rejected on measurement. Minimum fortification-to-fortification gaps put Rohan to Mordor at 148 map units, Gondor to Rhûn at 167 and Rohan to Rhûn at 183: those kingdoms border each other, and a partition drawn on kingdom centroids severs four genuine fronts. Corrected so it stops severing them, a gate vetoes six pairs, all of which the reach falloff already kills. Weighting also avoids stranding a kingdom whose enemies are all foreign, which `Army.CheckInactivity` punishes by disbanding its army roughly two days later.

**Fail open, always.** A kingdom absent from `KingdomTheaters` weights at 1.0, not as foreign. Player-founded kingdoms get the runtime StringId `new_kingdom`, rebels get `<settlementId>_rebel_clan`, and neither can appear in a shipped config; failing closed would silently make the player's own realm un-besiegeable. The same rule covers an unmeasurable distance: `GetReachMultiplier(NaN)` returns 1.0 rather than suppressing on garbage.

**Commitment cannot outrun suppression.** `Army.AiBehaviorObject` is saved, so an existing campaign can hold an army mid-march on a now-distant target. A committed cross-map siege scores `4.0 x 0.35 x 0.05 = 0.07` against a legal near target's `1.0 x 1.25 x 1.0 = 1.25`, so it re-targets rather than pinning. `ArmyTargetingServiceTests` pins that arithmetic.

Priority list advancement is stateless — captured settlements disappear from the enemy settlement pool, so the next unconquered entry naturally becomes the highest-boosted target with no tracking required.

Only `Besieger` receives the priority, theater and reach terms; only `Defender` receives the defence multiplier. `Raider` is deliberately untouched because vanilla already hard-zeroes raiders past 5 town gaps in `GetDistanceScoreForRaiding`. A non-finite or non-positive `BaseScore` returns unchanged.

### Component Diagram

```
army_targeting.json
        |
ArmyTargetingConfigProvider (loads + caches)
        |
ArmyTargetingService
  - _priorityIndex:   Dict<factionId, Dict<settlementId, index>>  (built once at startup)
  - _aggressionIndex: Dict<factionId, float>                      (built once at startup)
  - _theaterIndex:    Dict<kingdomId, string[]>                   (built once at startup, [0] = primary)
  - GetTargetMultiplier(candidateId, committedTargetId?, factionId?) -> float
  - GetStrengthMultiplier(factionId?) -> float          (inflates ourStrength pre-gate)
  - GetReachMultiplier(normalizedDistance) -> float     (1.0 flat, then linear to ReachFloor)
  - IsWithinReach(normalizedDistance) -> bool           (gates the Patch22 border floor)
  - GetTheaterWeight(attackerId?, targetOwnerId?) -> float
  - ApplyTargetScoreModifiers(TargetScoreContext) -> float
        |
TaomTargetScoreModel : DefaultTargetScoreCalculatingModel
  - GetTargetScoreForFaction(Settlement, ArmyTypes, MobileParty, float)
  - maps ArmyTypes -> ArmyTargetingMission via ArmyMissionMapper
  - extracts factionId / target owner / settlement StringIds at the boundary
  - inflates ourStrength -> calls base -> hands a TargetScoreContext to the service
        |
IMapReachAdapter / MapReachAdapter   (Main/Adapters/)
  - GetNormalizedDistanceToNearestFortification(Settlement, IFaction) -> float (town gaps, NaN if unmeasurable)
  - day-scoped memo: settlementId -> factionId -> distance, plus a per-faction fief list
  - six-argument MapDistanceModel.GetDistance overload (the five-arg form discards navigationCapability)

MCM (TaomSettings)
  - EnableArmyStrategicIntelligence
  - ArmyCommitmentMultiplier
  - ArmyPriorityBoost
  - EvilFactionAggressionScale
  - ArmyBorderProximityFloor
  - EnableWarTheaters
  - ArmyReachRadiusInTownGaps
  - ArmyDefenderPriority
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
| Border Proximity Floor | 0.15 | 0.0-1.0 | Minimum border-proximity score substituted for priority-list targets that vanilla rejects as out-of-range, **and only for targets inside the march radius**. 0.0 = vanilla behaviour. |
| Enable War Theaters | true | bool | Softly bias each kingdom toward its own front. Weighting only; no war is ever forbidden. Off = every enemy weights at 1.0. |
| Army March Radius | 3.0 | 1.0-20.0 | How far, in town gaps, an army will march to besiege. Beyond it the target is pinned at `ReachFloor`. 3.0 is about 280 map units. The config's inner radius is clamped against this so the two can never invert. |
| Home Defence Priority | 1.6 | 1.0-5.0 | Score multiplier on defending one of your own settlements. |

### Config File: `Main/_Module/ModuleData/configs/army_targeting.json`

All sections optional, missing entries default to vanilla behaviour. **Every value is validated on load**; a rejected field reverts to the compiled default with a warning, and one summary warning fires so the earlier lines get read.

```json
{
  "Theaters": ["north", "central", "south", "east"],
  "KingdomTheaters": { "<kingdom_id>": ["<primary_theater>", "<secondary>", ...] },
  "ReachInnerRadiusInTownGaps": 1.5,
  "ReachRadiusInTownGaps": 3.0,
  "ReachFloor": 0.05,
  "PrimaryTheaterWeight": 1.25,
  "SecondaryTheaterWeight": 1.0,
  "ForeignTheaterWeight": 0.35,
  "FactionPriorityTargets": { "<kingdom_id>": ["<settlement_id>", ...] },
  "FactionAggressionMultipliers": { "<kingdom_id>": 2.0 }
}
```

- **`Theaters`:** the closed set of legal theater names. A membership entry naming anything else is skipped with a warning, so a typo cannot become a private theater of one.
- **`KingdomTheaters`:** kingdom StringId to its ordered theater list. **The first entry is that kingdom's primary front.** An empty list marks a deliberately passive kingdom. A kingdom *absent* from the map weights neutral, which is what keeps player-founded and rebel kingdoms working.
- **`ReachInnerRadiusInTownGaps` / `ReachRadiusInTownGaps` / `ReachFloor`:** the falloff curve. The inner radius must clear the map's genuine fronts, which measure 1.58 to 1.95 town gaps.
- **`PrimaryTheaterWeight` / `SecondaryTheaterWeight` / `ForeignTheaterWeight`:** must be ordered foreign ≤ secondary ≤ primary, enforced on load. Foreign must be above zero: a veto strands a kingdom and costs it its army to `Army.CheckInactivity`.
- **`FactionPriorityTargets`:** ordered target lists. Earlier entries score higher, decaying from `MaxPriorityBoost` to 1.0 across the list. Because the decay spans the list, pruning an entry re-steepens it for the survivors.
- **`FactionAggressionMultipliers`:** how much to inflate `ourStrength` before the vanilla `2x defender` strength gate. At 2.0 the faction can siege at 1:1 parity instead of requiring 2:1.

### War theaters

Kingdom to theaters, first entry being its primary front. Produced with
`python tools/analyze_war_theaters.py`, which measures every `Hostile` pair's minimum
fortification-to-fortification gap on the live map; the rule for adding a membership is that the
pair's gap is inside the march radius, because anything past it is governed by geometry regardless.

| Theater | Kingdoms (primary in **bold**) |
|---|---|
| `north` | **gundabad**, **mirkwood**, **rivendell**, **goblin**, **mistymountainorcs**, **dolguldur**, **lothlorien**, **erebor**, **sturgia** (Dale), empire (Dunland), isengard |
| `central` | **vlandia** (Rohan), **isengard**, **empire** (Dunland), empire_w (Gondor), empire_s (Mordor), khuzait (Rhûn), goblin, mistymountainorcs, dolguldur, lothlorien |
| `south` | **empire_w** (Gondor), **empire_s** (Mordor), **aserai** (Harad), **umbar**, **shaghana**, **abanissa**, battania (Khand) |
| `east` | **khuzait** (Rhûn), **battania** (Khand), erebor, sturgia (Dale), empire_s (Mordor) |
| *(passive)* | `bluecraig`, `lindon` |

`bluecraig` and `lindon` carry no theater deliberately: all 20 Bluecraig settlements and 4 of
Lindon's 5 sit in a closed land-navigation component, so Bluecraig's nearest hostile kingdom is
5.13 town gaps away and it can reach nothing. That is a pre-existing map defect, not something this
feature caused, and it has its own issue.

### Current priority lists

Pruned 2026-08-21 by `tools/analyze_war_theaters.py --apply`: 26 of 80 entries sat beyond the march
radius, where the reach falloff pins them at the floor no matter how high their priority boost. They
were inert, and leaving them in the file misled anyone editing it.

| Faction | Kingdom ID (JSON key) | Priority Sequence |
|---------|------------|------------------|
| Mordor | `empire_s` | EW3 (E.Osgiliath) → EW2 (W.Osgiliath) → EW1 (Minas Tirith) → EW4 (Pelargir) |
| Isengard | `isengard` | V2 (Helm's Deep) → V1 (Edoras) |
| Gundabad | `gundabad` | M1/M2 (Mirkwood) → S1/S2 (Dale) → E1 (Erebor) → R1 (Rivendell) |
| Dol Guldur | `dolguldur` | L1 (Lothlórien) → S1/S5/S4/S3/S2 (Dale) → M1/M2 (Mirkwood) → E1 (Erebor) → R1 (Rivendell) |
| Rhûn / Easterlings | `khuzait` | E4/E3/E2 (Erebor) → S5/S4/S3 (Dale) |
| Gondor | `empire_w` | Interleaved ES (Mordor) + A (Harad): ES2→A1→ES3→A2→ES1→A3→ES4→A4→ES5→ES6 |
| Dunland | `empire` | V7/V2/V5/V4/V1/V3/V6 (Rohan) |
| Dale / Barding | `sturgia` | RU7/RU2/RU4/RU3 (Rhûn) → DG1 (Dol Guldur) |
| Erebor | `erebor` | RU1/RU4/RU3/RU5 (Rhûn) |

Gondor keeps most of its Harad entries because Gondor and Harad genuinely border each other: their
closest fortifications are 50.9 map units apart, well inside the radius. Dunland lost all four of
its Gondor targets, which were 4.3 to 4.5 town gaps out.

**No priority list (vanilla logic):** Rohan (`vlandia`), Harad (`aserai`), Shaghâna, Âbanissa, Khand
(`battania`), Umbar, Mirkwood, Lothlórien, Rivendell, Lindon, Blue Craig. These either defend, or
have no scripted axis of advance.

**Kingdom ID notes (keys are kingdom StringIds, not culture StringIds).** `WarTheaterConfigInvariantsTests` fails the build if any key here stops resolving, and rejects the six lore names by name, because this exact dead-key class has shipped five or more times:
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
| `Main/Features/ArmyTargeting/ArmyTargetingService.cs` | Core logic: commitment, priority, theater weighting, reach falloff, defence multiplier |
| `Main/Features/ArmyTargeting/ArmyTargetingMission.cs` | TAOM's mission enum, mapped from `Army.ArmyTypes` at the boundary |
| `Main/Features/ArmyTargeting/ArmyMissionMapper.cs` | The `Army.ArmyTypes` to `ArmyTargetingMission` converter, kept out of the model so its body stays branch-free |
| `Main/Features/ArmyTargeting/TargetScoreContext.cs` | Primitives extracted at the model boundary (base score, mission, both faction ids, settlement ids, normalised distance) |
| `Main/Adapters/IMapReachAdapter.cs` | Reach measurement interface |
| `Main/Adapters/MapReachAdapter.cs` | Nearest-owned-fortification distance in town gaps, day-scoped memo |
| `tools/analyze_war_theaters.py` | Read-only hostile-pair, border-gap and reach report; `--apply` prunes out-of-range priority entries |
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

- `TAOM.Tests/Features/ArmyTargeting/ArmyTargetingServiceTests.cs` (17 tests):
  - `GetTargetMultiplier` (12): feature disabled, commitment stickiness, non-committed, null committed, first/middle/last priority entry, combined commitment+priority, null faction, unknown faction, empty list, single-entry list
  - `GetStrengthMultiplier` (5): feature disabled, null faction, configured value, unknown faction, scale applied
- `TAOM.Tests/Features/ArmyTargeting/WarTheaterAndReachTests.cs` (38 tests):
  - reach curve (13): origin, inside the inner radius, at and beyond the outer radius, `float.MaxValue`, the 1e30 unreachable sentinel, NaN and infinity both declining to suppress, negative distance, **monotonic non-increase across 0 to 40 gaps** (the guard against a sign error inverting the whole feature), feature disabled, NaN radius setting, and an inner radius above the outer one proving the span cannot go non-positive
  - `IsWithinReach` (4): near, beyond, NaN false (opposite polarity to the multiplier, and deliberately so), feature disabled
  - theater weighting (10): primary, shared-but-not-primary, foreign, foreign-is-damped-not-vetoed, `new_kingdom`, `player_faction` and `<settlement>_rebel_clan` all neutral, passive kingdom, null ids, both toggles
  - `ApplyTargetScoreModifiers` (11): NaN and infinite base score, vanilla rejection, null context, Defender multiplier, Raider and Patrolling pass-through, master switch, boosted near primary target, damped far foreign target, and **commitment cannot outrun suppression**
- `TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs` (11 tests against the SHIPPED JSON):
  - every `KingdomTheaters` and `FactionPriorityTargets` key resolves against `taom_spkingdoms.xml` + `spkingdoms.xslt`; the six lore names are rejected by name; every shipped kingdom has a theater decision recorded; every theater name used is declared; declared names are unique; passive kingdoms are exactly the acknowledged pair; no duplicate entry within a kingdom's list; both ordering invariants; and the shipped file survives its own validator with no warning
- `TAOM.Tests/Features/ArmyTargeting/SiegeGatheringDiagnosticsServiceTests.cs` — 14 tests:
  - `Classify` (6): kingdom-null, no-fortifications, all-under-siege, fortifications-available (NoReachable), counts-unavailable, null-info
  - `Record` (4): first occurrence → WARNING once, same siege twice → WARNING+DEBUG, distinct sieges → WARNING each, null info → no log
  - `Format` (4): key fields present, counts-unavailable → `n/a`, NaN positions → `?`, null/blank fields render safely
  - (`FromArmy` boundary + the finalizer are in-game-validated per ADR-008 — no unit tests)

## How to Add a New Faction Priority List

1. Determine the faction's **faction StringId** — for custom TAOM kingdoms check `TAOM_spkingdoms.xml` for `id=`; for vanilla kingdoms (empire_s, empire_w, empire, khuzait, sturgia, vlandia, battania, aserai) use the vanilla kingdom id. **Do NOT use culture StringId** — Mordor, Gondor, and Dunland all share `Culture.empire` so culture IDs are ambiguous.
2. Find the target settlement IDs — grep `settlements.xml` for `id="town_` filtered by the target region.
3. Add an entry to `army_targeting.json`, keyed on the **kingdom** id:
   ```json
   "empire_s": ["town_X1", "town_X2", "town_X3"]
   ```
4. Give the kingdom a `KingdomTheaters` entry too, primary front first. `WarTheaterConfigInvariantsTests` fails if you skip it.
5. Run `python tools/analyze_war_theaters.py` and check none of your entries lands beyond the march radius. Anything past it is inert.
6. No code changes needed. The service reads the config at startup and builds its indexes once.

## Performance

`GetTargetScoreForFaction` is called O(armies × settlements) per 3h AI tick (~500–2000 calls per cycle at TAOM scale). All three service methods are allocation-free:

| Method | Cost | Notes |
|--------|------|-------|
| `GetStrengthMultiplier` | O(1) — 1 dict lookup | Called before base — must be minimal |
| `GetTargetMultiplier` | O(1) — 2 dict lookups | Commitment + priority index |
| `GetTheaterWeight` | O(1): 2 dict lookups + a scan of ≤4 strings | Linear scan beats a HashSet alloc at this list length |
| `GetReachMultiplier` | O(1): arithmetic only | The distance itself comes from the adapter |
| `IMapReachAdapter` | O(1) on a hit; O(fiefs) on a miss | Day-scoped memo, so a miss costs one walk per (settlement, faction) per campaign day. Measured only for `Besieger`, since a Raider is already distance-gated by vanilla and a Defender's target is its own fief |

- Feature disabled: immediate `return 1.0f` (single bool check) in all three methods
- All three indexes (`_priorityIndex`, `_aggressionIndex`, `_theaterIndex`) built once at service construction, zero rebuilding at runtime
- The theater verdict is two dictionary lookups and needs no distance, so an out-of-theater target is cheap to weight
- All lookups use `TryGetValue` — no `ContainsKey` + indexer double-lookup
- No LINQ, no string allocation, no collection creation per call

### Phase 2 — Border Proximity Harmony Patch

`AiMilitaryBehavior.CalculateDistanceScoreForBesieging` runs **before** our `GetTargetScoreForFaction` override. It checks how many of a target settlement's topological fortification neighbors belong to the attacker. If the answer is 0 (typical for distant priority targets on the large TAOM map), it returns `bestDistanceScore = 0` — causing `finalScore = 0 × base = 0` regardless of what our model returns.

**Implemented:** `Patch22_ArmyTargeting`, a Harmony Postfix on `CalculateDistanceScoreForBesieging`. When `bestDistanceScore == 0`, the target is in the faction's priority list, **and the target is inside the march radius**, substitutes `BorderProximityFloor` (MCM, default 0.15).

The reach condition was added 2026-08-21 and is the important half. Without it the floor rescued any priority-list entry at any distance, which is what turned the list into an ignore-geography list and is named in the `Patch49` registry entry as the cause of cross-map siege steering. Note the polarity: an unmeasurable distance makes `IsWithinReach` return **false**, so the floor is not applied. That looks inverted next to `GetReachMultiplier`, which returns 1.0 on NaN, but both defer to vanilla, here vanilla had already rejected the target, and garbage is no grounds to overrule it. A refusal logs once at DEBUG.

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

- 2026-08-21: **Reach falloff + war theaters + home defence.** Replaced vanilla's never-below-0.9 distance factor with a real falloff measured from the attacking faction's nearest owned fortification (`IMapReachAdapter`), flat to 1.5 town gaps then linear to a 0.05 floor at 3.0. Added soft theater weighting over four fronts, failing open for kingdoms absent from the table so player-founded and rebel realms are unaffected. Added a `Defender` multiplier, after establishing that overriding `DefendingFactor` cannot work: it has exactly one engine read, inside `CurrentObjectiveValue`, whose only consumer is `Army.ThinkAboutCohesionBoost`, while the defender weighting that reaches target selection is a hardcoded literal. Gated `Patch22`'s border floor on being in reach. Fixed a NaN gate in `ApplyTargetScoreModifiers` that read `baseScore <= 0f` and so let NaN into the multiply chain. Deleted `FactionDistanceRangeMultipliers`, `GetDistanceCompensation` and the Long-Range Priority Boost Scale setting, being the mechanism that pushed armies far. Pruned 26 of 80 inert priority entries. Added `tools/analyze_war_theaters.py`. +49 tests.
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
