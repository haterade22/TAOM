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
1. **Hand-written C# `InitializeXxx*()` methods** — Gondor, Dol Guldur, Erebor, Lothlórien, Shaghâna, Âbanissa, Rhûn, Gundabad. Static-constructor wiring; one method per (culture, scope) combination.
2. **JSON file `recruitment_pools/gondor.json`** — for Gondor only as of 2026-05-23. Loaded lazily once per process via `EnsureGondorJsonLoaded` in the instance constructor (idempotent via `Interlocked.CompareExchange`). JSON entries OVERWRITE matching hand-written keys when present; absent JSON file = hand-written behaviour (which is what tests see).

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

23 chance groups covering all Gondor regions (Anórien, Lebennin, Lossarnach, Belfalas, Lamedon, Pinnath Gelin, Anfalas, Harondor) plus per-town pools for capital settlements (Minas Tirith, Pelargir, Cair Andros, Dol Amroth, Linhir, Caras Tolfalas, Ost Arndir, Morlad, Serelond, Lond Cirion, Methir).

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Human-readable label (ignored by loader). |
| `notes` | string[] | Documentary notes (ignored by loader). |
| `chance_groups[].description` | string | Group label, used for log lines. |
| `chance_groups[].settlements` | string[] | Settlement IDs (`town_*`, `castle_*`, `village_*`, `castle_village_*`). |
| `chance_groups[].troops` | object\<string, number\> | Map of troop-id → percentage. Percentages should sum to 100 per group, but the cumulative-weight algorithm normalises proportionally. |
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
| [`Main/_Module/ModuleData/recruitment_pools/gondor.json`](../../Main/_Module/ModuleData/recruitment_pools/gondor.json) | Gondor pool data (23 chance groups). Hand-edited; reloaded on game start (Singleton lifetime — full restart required to retune). |

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

## GitHub Issue

- **Issue:** #215 — feat(troops): Rhun recruitment + Easterling → Loke-Rim + conditional-pool API
- **Status:** Closed (delivered in commit `bce0824`)
