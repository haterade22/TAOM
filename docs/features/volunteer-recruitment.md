# Volunteer Recruitment

## Overview

Per-settlement, per-clan, and per-culture overrides for which troop types notables sell to the player. Replaces vanilla `DefaultVolunteerModel.GetBasicVolunteer()` so visiting Minas Tirith gives Citadel Guard candidates, Pelargir gives Pelargir militia, Mardun gives Dragon-Wrath acolytes, etc. — the recruitable pool reflects the settlement's identity instead of always picking the culture's single `basic_troop`.

## Why This Exists

- **Vanilla behavior:** Vanilla picks one of two characters per culture (`basic_troop` or `elite_basic_troop`) for every notable in that culture. Every town and village of the same culture produces the same recruits.
- **TAOM requirement:** Middle-earth fiefs have distinct identities. Minas Tirith should produce Citadel Guard trainees; Dol Amroth should produce Swan Knight squires; Khûndol (deep Rhûn) should produce Dragon-Wrath acolytes. The recruitable pool is also one of the strongest "this place feels different" signals available to the player.
- **Without this feature:** Every Gondor town/castle would give the same `gondor_ano_peasant` recruits; every Rhûn settlement would give an Easterling Recruit (the legacy filler troop the user retired in 2026-05-23).

## Architecture

### Design Challenge

Vanilla `DefaultVolunteerModel.GetBasicVolunteer(Hero sellerHero)` returns `CharacterObject` for the troop type a notable will sell. It takes no settlement context — just the notable Hero. The model must read settlement / clan / culture state itself and map to the right troop.

Conditional pools (Ithil Guard at `town_ES2` only when Gondor-owned) need predicates evaluated at lookup time, not at registration time — kingdom ownership flips mid-session.

### Solution Approach

`TaomVolunteerModel` extends `DefaultVolunteerModel` and delegates `GetBasicVolunteer` to `IVolunteerRecruitmentService`. The service holds three static dictionaries (settlement / clan / culture) plus a fourth conditional-settlement map for predicated pools. The model also overrides `MaxVolunteerTier => 6` (vanilla 4).

The resolution chain is:

```
GetVolunteerTroopId(context)
  → ConditionalSettlementMap[settlementId]   if predicate passes
  → ConditionalSettlementMap[boundSettlementId] if predicate passes
  → SettlementMap[settlementId]
  → SettlementMap[boundSettlementId]   (villages fall through to bound town/castle)
  → ClanMap[ownerClanId]
  → CultureMap[cultureId]
  → null   → vanilla DefaultVolunteerModel takes over
```

Weighted selection within each pool uses cumulative-weight roll-out (see `PickWeighted`).

Pool data has two sources:
1. **Hand-written C# `InitializeXxx*()` methods** — Gondor, Dol Guldur, Erebor, Lothlórien, Shaghâna, Âbanissa, Rhûn, Gundabad, Goblin, Misty Mountain Orcs, Rivendell, Mordor, Dale, Rohan, Harad, Dunland, Isengard, Mirkwood, Umbar. Static-constructor wiring; one method per (culture, scope) combination.
2. **JSON file `recruitment_pools/gondor.json`** — for Gondor only as of 2026-05-23. Loaded lazily once per process via `EnsureGondorJsonLoaded` in the instance constructor (idempotent via `Interlocked.CompareExchange`). JSON entries OVERWRITE matching hand-written keys when present; absent JSON file = hand-written behaviour (which is what tests see).

### Reachability invariant (every troop is accounted for)

A pool only injects a few **root** troop IDs; the player obtains the rest of a line by *upgrading* those roots up the troop tree. So a troop is recruitable **iff it is a pool root, OR a pool root upgrades into it** (transitively). A line whose entry troop is an upgrade-graph orphan AND absent from every pool is fielded by AI lords but unobtainable by the player.

The `AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot` test enforces this: it parses the upgrade graph from every `troops_*.xml`, floods from `VolunteerRecruitmentService.AllPooledTroopIds()` (plus the production `gondor.json` conditional pool, via the real loader), and fails the build if any troop is unreachable **except** the intentionally non-recruited set — settlement militia (`*_militia_*`, spawned via `militia_*_template`), bandit-hideout bosses (`*_boss`), and `cave_troll` (a non-humanoid monster deferred until it has spider-style `Mission.SpawnAgent` swap support). When you author a new line, add its entry troop to a pool or this test tells you which IDs you left orphaned.

**Shadowing gotcha:** `CultureMap` is the *lowest-priority* pool. If a culture's fiefs all have a `SettlementMap`/`ClanMap` pool, a troop placed only in that culture's `CultureMap` entry never surfaces at those fiefs (it only fires for converted fiefs and unmapped settlements). To make a line recruitable at a culture's *own* settlements, add its root to the **settlement + clan** pools, not just culture. (This was the root cause of the Dol Guldur "uruk line not recruitable" report — `dg_uruk_warrior` sat in `CultureMap["dolguldur"]` but every DG fief had a settlement/clan pool that shadowed it.)

**Clan-restricting an elite (shadowing used deliberately):** the same `ClanMap`-over-`CultureMap` priority can gate a troop to ONE clan. The war-elephant rider (`harad_elephant_rider`, level 51 — see [elephant.md](elephant.md)) is recruitable only by `clan_aserai_1` (Ayerikkä): `InitializeHaradClans` gives that clan a pool that **copies** the aserai culture fallback (`harad_levy` 7 / `harad_noble` 3) and **adds** the rider at weight 1. Because the clan pool shadows culture and no other aserai clan has a pool, the rider surfaces only at Ayerikkä's fiefs; everywhere else falls through to the rider-less culture pool. (Note the copy: a clan pool *replaces* — not merges with — the culture fallback, so you must re-list the normal recruits or the clan would offer ONLY the elite.)

### Component Diagram

```
recruitment_pools/gondor.json       hand-written C# Initialize*() methods
       │                                          │
       │ (lazy, idempotent)                       │ (static ctor)
       ▼                                          ▼
GondorRecruitmentJsonLoader  ───────────► SettlementMap / ClanMap / CultureMap
       │                                  ConditionalSettlementMap
       │ (predicates for conditional rules)         │
       ▼                                            │
ResolveCondition  ──────────────────────────────────┤
                                                    ▼
                                       VolunteerRecruitmentService
                                                    │
                                                    ▼
                                            TaomVolunteerModel
                                                    │
                                                    ▼
                                      DefaultVolunteerModel.GetBasicVolunteer
                                                    │
                                                    ▼
                                        Notable's "Volunteers" list
```

## Configuration

### JSON config: `Main/_Module/ModuleData/recruitment_pools/gondor.json`

24 chance groups (23 regular + the conditional Ithil Guard rule) covering all Gondor regions (Anórien, Lebennin, Lossarnach, Belfalas, Lamedon, Pinnath Gelin, Anfalas, Harondor) plus per-town pools for capital settlements (Minas Tirith, Pelargir, Cair Andros, Dol Amroth, Linhir, Caras Tolfalas, Ost Arndir, Morlad, Serelond, Lond Cirion, Methir). Together they cover all 93 live `TAOM_Map` EW settlements, each in exactly one group.

**Weighting standard (2026-07-27).** Where a group mixes a regular line with a noble / settlement-specific line, the regular line totals **80%** and the noble line **20%**; single-line groups total 100%. The three Anórien capitals are the one structural variant: **70% Anórien / 20% settlement-specific / 10% Ithilien Ranger**. This replaced an earlier 60/40 split. Dol Amroth is not exempt — it previously inverted the rule at 90/10 in the Swan Knights' favour and now follows the standard, so `gondor_da_noble` + `gondor_da_footman` share the 20%. Trailing remainders absorb into the last troop of a split (`26.6667 / 26.6667 / 26.6666`) so every group totals exactly 100.

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Human-readable label (ignored by loader). |
| `notes` | string[] | Documentary notes (ignored by loader). |
| `chance_groups[].description` | string | Group label, used for log lines. |
| `chance_groups[].settlements` | string[] | Settlement IDs (`town_*`, `castle_*`, `village_*`, `castle_village_*`). |
| `chance_groups[].troops` | object\<string, number\> | Map of troop-id → percentage. Percentages MUST sum to 100 per group — enforced by `GondorJsonLoader_ProductionJson_EveryGroupTotals100AndNoSettlementIsListedTwice`, which also rejects a settlement listed in two groups (the second registration silently overwrites the first). The cumulative-weight algorithm normalises proportionally, so an over-100 group never crashes — it just quietly delivers a distribution the file doesn't describe, which is how one group shipped at 120%. |
| `chance_groups[].condition` | string (optional) | If present, the group is registered via `AddSettlementConditional`. Only the Ithil Guard rule ("town_ES2 + Gondor-owned") is currently recognised by `ResolveCondition`; unrecognised conditions skip the group fail-closed. |

Percentages → integer weights via `* 10000` (preserves 4-digit precision: `33.3334` → `333334`). NaN / Infinity / negative / blank entries are rejected with a warning and skipped (per `csharp-architecture.md` "Config Providers MUST Validate"). Missing-file and malformed-JSON both fail gracefully (no entries added; hand-written safety net stays in effect).

### Hand-written pools

Each non-Gondor culture lives entirely in `VolunteerRecruitmentService.cs` as a series of `InitializeXxxSettlements` / `InitializeXxxClans` / `InitializeXxxCulture` methods. Pool entries use:

```csharp
AddSettlement("town_RU7",
    ("dragon_wrath_acolyte",  3),
    ("dragon_wrath_archer",   1),
    ("darkhun_recruit",       2),
    ("loke_rim_initiate",     1));
```

Integer weights, no normalisation required — `PickWeighted` rolls in `[0, sum(weights))`.

### Conditional pool: Ithil Guard at Minas Morgul

```csharp
AddSettlementConditional("town_ES2",
    ctx => ctx.OwnerCultureId == "gondor",
    ("gondor_ith_watcher", 500000),
    ("gondor_ith_veteran", 500000));
```

When Mordor owns `town_ES2` (default), the predicate fails → conditional pool skipped → no Gondor pool exists for that settlement → falls through to Mordor's culture pool. When Gondor captures the settlement (`OwnerClan.Culture.StringId == "gondor"`), the predicate flips true → Ithil Guard candidates appear.

The predicate is evaluated **per-lookup**, not at registration time — kingdom flips take effect for the next volunteer pick. `VolunteerContextAdapter` reads `settlement.OwnerClan?.Culture?.StringId` live without caching.

## Key Files

| File | Purpose |
|------|---------|
| [`Main/Features/TroopProgression/VolunteerRecruitmentService.cs`](../../Main/Features/TroopProgression/VolunteerRecruitmentService.cs) | Core service. Hand-written pools + conditional API + weighted-random selection. |
| [`Main/Features/TroopProgression/IVolunteerRecruitmentService.cs`](../../Main/Features/TroopProgression/IVolunteerRecruitmentService.cs) | Service interface. |
| [`Main/Features/TroopProgression/VolunteerContext.cs`](../../Main/Features/TroopProgression/VolunteerContext.cs) | Immutable struct carried through resolution chain. Holds settlement id, bound settlement id, owner clan id, culture id, owner culture id. |
| [`Main/Features/TroopProgression/GondorRecruitmentJsonLoader.cs`](../../Main/Features/TroopProgression/GondorRecruitmentJsonLoader.cs) | JSON loader for Gondor pools. Internal static. Test seam: `LoadFromPath(string, ...)`. |
| [`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`](../../Main/Features/TroopProgression/Models/TaomVolunteerModel.cs) | GameModel override. Thin — delegates to service. |
| [`Main/Adapters/IVolunteerContextAdapter.cs`](../../Main/Adapters/IVolunteerContextAdapter.cs) | Adapter interface. |
| [`Main/Adapters/VolunteerContextAdapter.cs`](../../Main/Adapters/VolunteerContextAdapter.cs) | TaleWorlds `Hero` → `VolunteerContext` boundary. Populates `OwnerCultureId` from `settlement.OwnerClan?.Culture?.StringId`. |
| [`Main/Features/TroopProgression/TroopProgressionIoC.cs`](../../Main/Features/TroopProgression/TroopProgressionIoC.cs) | DryIoc Singleton registrations for service + adapter. |
| [`Main/_Module/ModuleData/recruitment_pools/gondor.json`](../../Main/_Module/ModuleData/recruitment_pools/gondor.json) | Gondor pool data (24 chance groups). Hand-edited; reloaded on game start (Singleton lifetime — full restart required to retune). |

## Dependencies

- `IRandomProvider` (Core) — wraps `Random.Next` for testability.
- `IModLogger` (Core) — diagnostic logging. NB: hot-path `LogDebug` interpolation happens per-notable per-day; a logger-enabled guard is deferred (`IModLogger` has no `IsDebugEnabled`).
- `IVolunteerContextAdapter` (Adapters) — bridges `Hero` → `VolunteerContext`. Wraps `Hero.CurrentSettlement`, `Settlement.OwnerClan`, `Village.Bound`.

## Tests

- [`TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`](../../TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs) — ~150 test methods covering:
  - Per-culture pool returns (Gondor, Dol Guldur, Erebor, Lothlórien, Shaghâna, Âbanissa, Rhûn)
  - Settlement → bound-settlement → clan → culture fallback ordering
  - Weighted-random boundary rolls (every settlement pool's cumulative buckets validated at edge rolls)
  - Conditional pool: Mordor-owned `town_ES2` returns null; Gondor-owned returns Ithil Guard
  - `AddSettlementConditional` null-predicate rejection
  - JSON loader: missing file, malformed JSON, NaN/Infinity/negative weight skipping, percentage→weight conversion, recognised vs unrecognised condition routing
  - Integration test against the real `gondor.json` file (walks up from test bin to find repo root)
  - `BuildPool` validation: empty entries, non-positive weight, blank troop id
  - **Reachability guard** (`AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot`): floods the `troops_*.xml` upgrade graph from all pool roots and asserts only militia/`*_boss`/`cave_troll` are unreachable — see [Reachability invariant](#reachability-invariant-every-troop-is-accounted-for). Plus `AllPooledTroopIds_ResolveToRealTroops_NoTypos` (every pooled id resolves to a real troop, except the `taom_spider_creature` anchor).
  - Newly-wired cultures Mirkwood (`mirkwood_recruit`) + Umbar (`aux_basic` / `umbar_elite`), and the reachability-fix line entries (Gundabad archer/scout, Isengard orc/Orthanc, Dol Guldur orc/uruk at settlement + clan).
  - Harad `clan_aserai_1` (Ayerikkä) elephant-rider pool: the rider rolls at the clan's top weight bucket; the clan still rolls its normal `harad_levy`; the aserai culture fallback + any other aserai clan never roll the rider (`GetVolunteerTroopId_ClanAserai1_*` / `_AseraiCulture_NoClanPool_*` / `_OtherAseraiClan_*`).

## How to add a new culture's pool

1. **Decide the resolution layer.** Per-settlement gives finest control (one pool per `town_*` / `castle_*`). Per-clan covers all of a noble house's settlements. Per-culture is the catch-all fallback.
2. **Write the C# init method** in `VolunteerRecruitmentService.cs`:
   ```csharp
   private static void InitializeMyCultureSettlements()
   {
       AddSettlement("town_XY1", ("my_culture_troop_a", 7), ("my_culture_troop_b", 3));
       // ... etc
   }
   ```
   Use the engine culture ID for `CultureMap` keys, NOT the LOTR display name. See `.claude/rules/xml-data.md` for the cheat-sheet (e.g., `khuzait` not `rhun`, `vlandia` not `rohan`, `aserai` not `harad`).
3. **Wire it into the static constructor** — add `InitializeMyCultureSettlements()` after the existing inits.
4. **Verify every troop ID exists** — grep `Main/_Module/ModuleData/troops/troops_*.xml` for each `("troop_id", weight)` entry. **Sibling-naming-symmetry is a false-positive signal** (`feedback_verify_troop_ids_against_canonical_xml.md`). A typo like `wain_cavalry` (doesn't exist — real ID is `wainrider_cavalry`) silently produces null at runtime; `MBObjectManager.GetObject<CharacterObject>` returns null, dropping the volunteer slot.
5. **Write tests first (TDD).** Cover at minimum: roll-0 returns first entry, roll-N returns last entry, boundary rolls between entries land in expected buckets. See the Khundol / Urushban / Sart test cluster for the canonical pattern.
6. **Run `dotnet test TAOM.Tests --filter FullyQualifiedName~VolunteerRecruitmentService`** — must be green.

## How to add a conditional pool

For state-sensitive pools (owner culture, season, prosperity threshold, etc.):

1. **Extend `VolunteerContext`** with the field you need to read (the field is populated by `VolunteerContextAdapter` from live engine state).
2. **Use `AddSettlementConditional`** in your init method:
   ```csharp
   AddSettlementConditional("town_ABC",
       ctx => ctx.MyNewField == "expected_value",
       ("special_troop_a", 1),
       ("special_troop_b", 1));
   ```
3. **Conditional pools resolve BEFORE regular `SettlementMap` entries.** If the predicate fails, resolution falls through to the next stage in the chain (bound settlement → clan → culture). If you ALSO want a default pool for the same settlement when the condition is false, register both via `AddSettlement` (default) and `AddSettlementConditional` (special).
4. **Test both predicate states.** See `GetVolunteerTroopId_Town_ES2_OwnerCultureMordor_DoesNotReturnIthilGuard` + `GetVolunteerTroopId_Town_ES2_OwnerCultureGondor_ReturnsIthilGuard`.
5. **For JSON-driven conditionals**, extend `GondorRecruitmentJsonLoader.ResolveCondition` to recognise the new condition string. Fail-closed: an unrecognised condition string MUST skip the group, NOT silently degrade to a non-conditional pool.

## Performance

`GetVolunteerTroopId` runs per-notable per-day during the daily campaign tick (potentially hundreds of times per day across all settlements). Tight constraints:

- **No LINQ, no per-call list/array allocation.** Two-pass weighted-pick loop sums weights, then picks — no allocations.
- **All lookups are O(1) hashtables.** Worst case: 2 conditional + 4 regular = 6 `Dictionary.TryGetValue` calls before falling through.
- **Conditional predicates are simple equality checks.** The Ithil Guard predicate is `ctx.OwnerCultureId == "gondor"` — string comparison, no engine calls.
- **JSON load is one-shot.** `EnsureGondorJsonLoaded` uses `Interlocked.CompareExchange` to ensure single load across all `VolunteerRecruitmentService` instances.

Known overhead: the `LogDebug` call in `GetVolunteerTroopId` interpolates a string on every call regardless of whether Debug logs are enabled, because `IModLogger` doesn't expose `IsDebugEnabled`. This was inherited from pre-existing code, not introduced by this session; it's tracked for a future logger-interface improvement.

## Changelog

- 2026-06-24 — Restored `gondor_ithilien_ranger` to 10% in the **live** Gondor JSON pools (Minas Tirith / Osgiliath / Cair Andros) after the loader's straight-replace overwrote the hand-written ranger weight; added a production-JSON regression test.
- 2026-06-10 — Closed recruitment-reachability gaps: flood-filled the upgrade graph from every pool root, added orphaned root troops (Gundabad/Dol Guldur/Isengard lines) and wired Mirkwood + Umbar pools, plus the reachability guard test.
- 2026-05-23 — Added Rhûn per-settlement pools (Easterling → Loke-Rim), introduced the `AddSettlementConditional` conditional-pool API + `ConditionalSettlementMap`, and moved Gondor pools to `recruitment_pools/gondor.json` via `GondorRecruitmentJsonLoader` (#215).

## GitHub Issue

- **Issue:** #215 — feat(troops): Rhun recruitment + Easterling → Loke-Rim + conditional-pool API
- **Status:** Closed (delivered in commit `bce0824`)

## Migrated notes (from CLAUDE.md, 2026-07-12)

- **Per-culture partial-file split (2026-07-01, #308).** The hand-written `InitializeXxx*()` pool methods no longer all live in `VolunteerRecruitmentService.cs` — they are split into per-culture partial files under `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs` (Dale, DolGuldur, Dunland, Erebor, Gondor, Harad, Isengard, Lothlorien, Mirkwood, Mordor, OrcKingdoms, Rhun, Rivendell, Rohan, Umbar). The core `VolunteerRecruitmentService.cs` file keeps the maps, the lookup cascade, and the weighted pick. Where sections above say a culture "lives entirely in `VolunteerRecruitmentService.cs`", read that as its `RecruitmentPools` partial.
- **Gondor JSON coverage (full list).** The 23 regular chance groups in `recruitment_pools/gondor.json` cover Anórien / Osgiliath / Cair Andros / Lebennin / Pelargir / Lossarnach / Belfalas / Dol Amroth / Linhir / Tolfalas / Lamedon / Calembel / Pinnath Gelin / Arndir / Blackroot Vale / Anfalas / Serelond / Lond Cirion / Harondor / Methir, plus the conditional Ithil Guard rule (Osgiliath, Calembel, and Blackroot Vale are not named in the Configuration section's list above).
- **Hand-written Gondor safety net by name.** The hand-written `InitializeGondorSettlements` method is kept as the safety net for the JSON: JSON entries overwrite the hand-written keys at runtime; test runs where the JSON file is missing fall back to hand-written behaviour.

- **The safety net is held in lockstep with the JSON (2026-07-27).** Because the JSON overwrites every key at runtime, the hand-written layer is live only in degraded mode — and in the tests, which means any divergence makes the suite assert behaviour the game never exhibits. The two had drifted: the C#-only path stranded the whole 7-troop Ithil Guard line (`gondor_ith_*`), pooled three ids the JSON never offered (`gondor_anf_guardsman`, `gondor_mt_fountain_guard`, `gondor_ser_pikeman`), and gave `castle_EW10` Harondor troops where the JSON says Belfalas. `InitializeGondorSettlements` now mirrors all 27 towns/castles plus the `town_ES2` conditional, and `GondorPools_HandWrittenFallback_MatchesProductionJson` compares **normalised shares** per settlement so they cannot separate again. The C# side uses the smallest integers holding each ratio (`8/8/8/3/3`) rather than the JSON's percentages, so raw weights differ by construction and only the distribution is comparable. Villages are deliberately *not* mirrored — a village with no pool of its own inherits its bound town's via the `BoundSettlementId` leg of the cascade, which is close to but not identical with the JSON's per-village regional pools; that residual gap is accepted rather than duplicating 66 entries into a degraded-mode net.

- **JSON troop ids are typo-gated (2026-07-27).** `AllPooledTroopIds_ResolveToRealTroops_NoTypos` only ever saw the hand-written maps (in the test bin `AllPooledTroopIds()` holds no JSON id), and the reachability guard drops unknown ids through an `if (nodes.Contains(...))` filter — so a misspelled id in `gondor.json` passed every check while resolving to null in-game and silently voiding its weight share. `GondorJsonLoader_ProductionJson_EveryTroopIdResolvesToARealTroop` collects ids **unfiltered** and closes that hole. This is the failure class of [`rca-rhun-gondor-recruitment-2026-05-23.md`](../reviews/rca-rhun-gondor-recruitment-2026-05-23.md) (`wain_cavalry` vs `wainrider_cavalry`), whose "add a script-level check" follow-up went unbuilt for two months.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-conversion.md](./culture-conversion.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
