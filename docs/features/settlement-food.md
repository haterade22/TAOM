# Settlement Food (TaomSettlementFoodModel)

## Overview

Overrides `DefaultSettlementFoodModel` to (1) expose vanilla's hardcoded food constants as JSON knobs
under an MCM master toggle, (2) add a **prosperity-scaled "hinterland" production term** vanilla has
no equivalent of, and (3) **ship tuned defaults** that clear the map-wide starvation described below.
A historical Troop-Weight garrison correction also lives here; it is an inert no-op today (see
"Garrison correction math").

**Shipped behaviour is NOT vanilla.** Every knob shipped at its vanilla value until 2026-09-06, which
meant the feature existed but relieved nothing.

## Why This Exists

### The map-wide measurement (2026-09-06)

Measuring the LIVE `TAOM_Map/ModuleData/settlements.xml` against the vanilla formula:
**70 of 72 towns start with a negative daily food balance, mean -38.0/day, before garrison
consumption is counted at all.** Isengard's Orthanc (prosperity 4000, one bound village) sits at
-73.0/day and is only the 6th worst.

The structural cause: vanilla consumption is **linear in prosperity** (`Prosperity/40`) while
production is **flat** (base 15, plus at most 18 per bound village). Any town above roughly
`production * 40` prosperity is arithmetically guaranteed to starve. Vanilla Calradia is tuned right
at that line (54 towns, 2 to 3 villages, break-even prosperity around 1,500 to 2,000). TAOM ships 64
towns above 3,000 and two at 5,100, so the map moved prosperity well past vanilla's design centre
while production stayed vanilla. The 2026-08-14 economy floor pass, which lifts eight cultures to
town prosperity 4,800 for income reasons, makes the food side strictly worse.

**This is why fiefs cannot support lords.** A starving settlement kills **10% of its garrison
regulars per day** (`DefaultPartyHealingModel.GetDailyHealingForRegulars`), gated on
`SettlementHelper.IsGarrisonStarving`, which reduces to `production < garrison / 20`. Before this
fix, 17 of 72 towns could not hold 800 men and Orthanc capped at 540; after it, the lowest threshold
on the map is 1,860 men.

### Original root causes (#289)

Towns/castles ran chronic food deficits — garrisons and civilians outpacing production. Root causes
(full mechanics + decompile cites: [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)):

1. **Troop-Weight leak (TAOM bug).** `Patch17_TroopWeight` postfixes the global
   `PartyBase.NumberOfAllMembers` getter and bumps it to the *weighted* count. `DefaultSettlementFoodModel`
   reads exactly that getter for the garrison food term (`NumberOfAllMembers / 20`), so an elite
   garrison (troop weights 2.0–3.0) consumed 2–3× the intended food. The Troop Weight feature was
   designed for field-party size budgeting; weighting garrisons for food was never intended.
2. **Vanilla high-prosperity squeeze (not a bug, but the dominant term).** `Prosperity / 40` is the
   largest consumer while production caps low (base 15 + ≤18/village). Vanilla self-limits prosperous
   towns into deficit; TAOM amplifies it with large elite garrisons, frequent raids (looted villages
   produce 0 food), and hearth-growth penalty feats.

Not contributors (ruled out): cultural food-consumption feats (mobile-party only — garrisons are
exempt from `DoesPartyConsumeFood`), caravans (trade parties, deliver no food), SettlementGuards
(cosmetic battle agents, not in the garrison roster).

## Architecture

Thin GameModel → pure service → primitive snapshot (ADR-002 / ADR-007), mirroring
`TaomSettlementLoyaltyModel` + `RevoltTuningConfigProvider`.

- **`TaomSettlementFoodModel : DefaultSettlementFoodModel`** — overrides the four virtual constants
  (`NumberOfMenOnGarrisonToEatOneFood`, `NumberOfProsperityToEatOneFood`, `FoodStocksUpperLimit`,
  `CastleFoodStockUpperLimitBonus`) to return config values (or vanilla when the master toggle is off),
  and overrides `CalculateTownFoodStocksChange` to call `base(...)` then add the service delta.
- **`SettlementFoodService.ComputeFoodDelta`** — pure (no TaleWorlds types): garrison raw-count
  correction `(weighted − raw)/divisor` (always) + siege-gated production knobs (base-food delta,
  per-village `(hearthLevel+1)×(mult−6)`, flat bonus, and the `prosperity × hinterlandRate`
  hinterland term). Returns 0 when disabled. The production deltas are siege-gated because vanilla
  zeroes food production while a settlement is under siege; the hinterland term is inside that same
  gate, so a besieged high-prosperity town is never more food-secure than a peaceful one.
  `Prosperity` is ENGINE-sourced, so the hinterland multiply is gated on
  `FiniteFloatValidator.IsFinite` as a positive requirement, and `ApplyFoodAdjustment` refuses any
  non-finite delta before it reaches the engine's `ExplainedNumber`. This is not theoretical:
  `Town.Prosperity`'s setter only floors at zero (`NaN < 0f` is false, so NaN is storable), and
  `Town.DailyTick`'s `< 0f` and `> cap` clamps are BOTH false for NaN, so one NaN would leave
  `FoodStocks` permanently NaN inside a `[SaveableProperty]`. See
  [rca-settlement-food-2026-09-06.md](../reviews/rca-settlement-food-2026-09-06.md).
- **`TownFoodSnapshot.FromTown`** — boundary factory converting sealed `Town` → primitives
  (raw `MemberRoster.TotalManCount` vs patched `NumberOfAllMembers`, per-Normal-village hearth levels,
  and `Prosperity` for the hinterland term).
- **`SettlementFoodConfigProvider`** — loads + validates JSON; reverts invalid values to vanilla.
  The hinterland rate is validated against the **sanitized** `ProsperityFoodDivisor`, not the parsed
  one, since a rejected divisor has already reverted (and a raw `0` would divide by zero here).

### Garrison correction math

Vanilla `PartyBase.NumberOfAllMembers == MemberRoster.TotalManCount` (`PartyBase.cs:381`); Patch17 only
ever raises it. So `weighted − raw` is exactly the inflation, and adding back `(weighted−raw)/divisor`
makes the garrison term use the raw body count. The global getter stays weighted, so AI strength reads
and `DefaultSettlementGarrisonModel` capacity are unchanged (food-model-only fix). No-op when Troop
Weight is off (weighted == raw).

## Configuration

`Main/_Module/ModuleData/settlement_food/settlement_food_config.json`:

| Knob | Vanilla | **Shipped** | Effect |
|------|---------|-------------|--------|
| `garrisonFoodDivisor` | 20 | **20** | ↑ = garrisons cheaper to feed (left vanilla: garrisons should still cost) |
| `prosperityFoodDivisor` | 40 | **45** | ↑ = relieves the dominant civilian term |
| `townBaseFood` / `castleBaseFood` | 15 / 10 | **30** / 10 | flat production floor; helps village-poor towns most |
| `villageFoodMultiplier` | 6 | **8** | scales `(hearthLevel+1)×mult` per village |
| `flatFoodBonus` | 0 | **5** | flat daily production add |
| `hinterlandFoodPerProsperity` | n/a (new, default 0) | **0.02** | food per point of prosperity; the term that carries the fix |
| `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | 300 / 150 | storage caps (left vanilla: raising them would blunt siege starvation) |

### The hinterland term, and the one invariant that matters

Flat knobs cannot hold a balance across a 600 to 5,100 prosperity range, and prosperity **changes
during play**, so a town tuned to break even today starves again once it grows. Scaling production
with prosperity too makes the shape stable at any size:

```
production  = base + Σ (hearthLevel+1) × villageMultiplier + prosperity × hinterlandRate + flatBonus
consumption = prosperity / prosperityFoodDivisor + garrison / garrisonFoodDivisor
```

**`hinterlandFoodPerProsperity` must stay STRICTLY below `1 / prosperityFoodDivisor`.** At or above
it, net food stops falling as prosperity rises, so a surplus fief overflows its store forever,
vanilla converts the overflow into prosperity (`+0.1` per point), and prosperity, town gold
(`10000 + Prosperity×12`) and garrison caps inflate map-wide with nothing to arrest them. Shipped
`0.02 < 1/45 = 0.0222…`, so net still declines as a fief grows (Orthanc: +42.1/day at prosperity
4,000, falling to +24.3/day at 12,000). The provider rejects a violating value at load, and
`SettlementFoodShippedConfigTests` fails the build rather than trusting a runtime warning.

Measured effect of the shipped values across the 72 towns, before garrison: **0 negative** (was 70),
mean **+75.1/day** (was -38.0), worst **+27.2/day** (was -88.5), Orthanc **+42.1/day** (was -73.0).
Two zero-village Gondor towns (`town_EW10` Serelond, `town_EW11` Methir) sit near break-even under a
very large garrison; that is map data, not a knob problem.

Validation: divisors must be ≥ 1 (a 0 would poison the formula with Infinity); floats must be finite
and ≥ 0; out-of-range/NaN reverts to the vanilla default with a logged warning. The base/village knobs
are **absolute replacements** for the vanilla constant (default = vanilla), so a value below vanilla
*lowers* production — these tune both directions, not relief-only (the divisors do too); only
`flatFoodBonus` is purely additive. Validation never enforces "≥ vanilla".

**MCM:** Settlement Food → **Enable Settlement Food Tuning** (on by default). Off = vanilla engine math
(garrison food reverts to the weighted count). The JSON is loaded once (`Reuse.Singleton`), so **edits
require an app restart**, not a save reload.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs` | GameModel override (thin boundary) |
| `Main/Features/SettlementFood/SettlementFoodService.cs` | Pure food-delta math |
| `Main/Features/SettlementFood/ISettlementFoodService.cs` | Service interface |
| `Main/Features/SettlementFood/TownFoodSnapshot.cs` | Sealed-`Town` → primitive boundary snapshot |
| `Main/Features/SettlementFood/SettlementFoodConfig.cs` | Config POCO (vanilla defaults) |
| `Main/Features/SettlementFood/SettlementFoodConfigProvider.cs` | JSON load + validation |
| `Main/Features/SettlementFood/SettlementFoodIoC.cs` | Singleton registrations |
| `Main/_Module/ModuleData/settlement_food/settlement_food_config.json` | The knobs (ships tuned, NOT vanilla) |
| `TAOM.Tests/Features/SettlementFood/SettlementFoodShippedConfigTests.cs` | Pins the shipped JSON against the ratio invariant |
| `Main/Features/TaomSettings.cs` | MCM master toggle |

Registered: `IoC.cs` (`SettlementFoodIoC.RegisterSettlementFoodFeature`), `SubModule.cs`
(`AddModel(new TaomSettlementFoodModel(...))`).

## Dependencies

- `IPathService` (config path), `IModLogger` (validation warnings) — TAOM core infrastructure.
- `TaomSettings.Instance` (MCM master toggle).
- Reads, but does not modify, the Troop Weight feature's effect on `NumberOfAllMembers`.

## Tests

`TAOM.Tests/Features/SettlementFood/`:

- `SettlementFoodServiceTests` (25): garrison correction (inflated/not-inflated/raised-divisor/under
  siege), production knobs (town/castle base, village multiplier, flat, siege suppression), combined,
  disabled, `ApplyFoodAdjustment` integration, default-config = vanilla constants, and the hinterland
  term (applied, default-off, siege-suppressed, castles, composed with the other knobs, disabled).
- `SettlementFoodConfigProviderTests` (22): valid parse, missing/malformed/partial JSON, cached,
  one test per validation rule (zero/negative divisor, negative/NaN floats, zero cap → revert + warn),
  plus the hinterland rate: valid, at the boundary, above it, negative, NaN, Infinity, and validated
  against the sanitized divisor.
- `SettlementFoodShippedConfigTests` (4): pins the SHIPPED JSON: the strict ratio invariant, that it
  survives its own validator with no warning, that it clears the worst town on the map, and that net
  food still falls as prosperity rises.

51 tests, all green (full suite 8175 passed; 4 failures on this branch are pre-existing and unrelated,
confirmed against a pristine `HEAD`).

## How-To

**Retune:** edit `settlement_food_config.json`. `hinterlandFoodPerProsperity` is the main lever and
must stay strictly below `1 / prosperityFoodDivisor` (the provider reverts it and warns otherwise, and
`SettlementFoodShippedConfigTests` fails the build). `prosperityFoodDivisor` is the next biggest, then
`townBaseFood` for village-poor fiefs. Restart the app (singleton config). Verify via a town's
management-screen food tooltip (`Town.FoodChangeExplanation`), where the adjustment shows as its own
"Settlement food (TAOM)" line.

**If the tuning seems to do nothing in game:** check that `Modules/TAOM/ModuleData/settlement_food/`
exists in the DEPLOYED module. If the directory is absent the provider silently falls back to
compiled defaults, which are vanilla, and logs a "not found" warning.

**Disable entirely:** MCM → Settlement Food → Enable Settlement Food Tuning → off (reverts to vanilla
engine math, including the weighted garrison food).

## Performance

`CalculateTownFoodStocksChange` runs per fief per day (not a per-frame hot path). The snapshot walks
bound villages once; the service is O(villages). Config is cached at first access.

## Changelog

- 2026-09-06, `feat(settlement-food)` (#546): added the prosperity-scaled `hinterlandFoodPerProsperity` production term (siege-gated, default 0) with a strict `< 1/prosperityFoodDivisor` validation invariant, and shipped tuned defaults. Measured on the LIVE map, towns starting with a negative food balance go from 70/72 to 0/72; Orthanc from -73.0 to +42.1/day. Also corrects the engine doc's claim that garrison troops never starve to death (they lose 10%/day once production drops below `garrison/20`).
- 2026-06-18 — `feat(settlement-food)` (#289): added `TaomSettlementFoodModel` fixing the Troop-Weight garrison food starvation (garrison term uses raw body count) plus MCM/JSON-tunable food knobs (consumption divisors, base/village/flat production, storage caps); MCM master toggle on by default.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-economy.md](./settlement-economy.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](../modding/balance-levers.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
