# Settlement Food (TaomSettlementFoodModel)

## Overview

Overrides `DefaultSettlementFoodModel` to (1) fix a Troop-Weight side effect that inflated garrison
food consumption for elite garrisons, and (2) expose vanilla's hardcoded food constants as MCM/JSON
knobs so the high-prosperity food squeeze can be tuned. Defaults are vanilla, so out of the box the
only behavioral change is the garrison correction.

## Why This Exists

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
  per-village `(hearthLevel+1)×(mult−6)`, flat bonus). Returns 0 when disabled.
- **`TownFoodSnapshot.FromTown`** — boundary factory converting sealed `Town` → primitives
  (raw `MemberRoster.TotalManCount` vs patched `NumberOfAllMembers`, per-Normal-village hearth levels).
- **`SettlementFoodConfigProvider`** — loads + validates JSON; reverts invalid values to vanilla.

### Garrison correction math

Vanilla `PartyBase.NumberOfAllMembers == MemberRoster.TotalManCount` (`PartyBase.cs:381`); Patch17 only
ever raises it. So `weighted − raw` is exactly the inflation, and adding back `(weighted−raw)/divisor`
makes the garrison term use the raw body count. The global getter stays weighted, so AI strength reads
and `DefaultSettlementGarrisonModel` capacity are unchanged (food-model-only fix). No-op when Troop
Weight is off (weighted == raw).

## Configuration

`Main/_Module/ModuleData/settlement_food/settlement_food_config.json` (ships at vanilla values):

| Knob | Vanilla | Effect | Recommended relief |
|------|---------|--------|--------------------|
| `garrisonFoodDivisor` | 20 | ↑ = garrisons cheaper to feed | 25–30 |
| `prosperityFoodDivisor` | 40 | ↑ = relieves the dominant civilian term | 55–60 |
| `townBaseFood` / `castleBaseFood` | 15 / 10 | flat production floor | +5–10 |
| `villageFoodMultiplier` | 6 | scales `(hearthLevel+1)×mult` per village | 8–10 |
| `flatFoodBonus` | 0 | flat daily production add | 0–10 |
| `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | storage caps | as desired |

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
| `Main/_Module/ModuleData/settlement_food/settlement_food_config.json` | The knobs |
| `Main/Features/TaomSettings.cs` | MCM master toggle |

Registered: `IoC.cs` (`SettlementFoodIoC.RegisterSettlementFoodFeature`), `SubModule.cs`
(`AddModel(new TaomSettlementFoodModel(...))`).

## Dependencies

- `IPathService` (config path), `IModLogger` (validation warnings) — TAOM core infrastructure.
- `TaomSettings.Instance` (MCM master toggle).
- Reads, but does not modify, the Troop Weight feature's effect on `NumberOfAllMembers`.

## Tests

`TAOM.Tests/Features/SettlementFood/`:

- `SettlementFoodServiceTests` (13) — garrison correction (inflated/not-inflated/raised-divisor/under
  siege), production knobs (town/castle base, village multiplier, flat, siege suppression), combined,
  disabled, `ApplyFoodAdjustment` integration, default-config = vanilla constants.
- `SettlementFoodConfigProviderTests` (14) — valid parse, missing/malformed/partial JSON, cached,
  one test per validation rule (zero/negative divisor, negative/NaN floats, zero cap → revert + warn).

27 tests, all green.

## How-To

**Relieve starvation:** edit `settlement_food_config.json` — raise `prosperityFoodDivisor` (biggest
lever) and/or `garrisonFoodDivisor`, optionally bump `villageFoodMultiplier`/`townBaseFood`. Restart
the app (singleton config). Verify via a town's management-screen food tooltip (`Town.FoodChangeExplanation`).

**Disable entirely:** MCM → Settlement Food → Enable Settlement Food Tuning → off (reverts to vanilla
engine math, including the weighted garrison food).

## Performance

`CalculateTownFoodStocksChange` runs per fief per day (not a per-frame hot path). The snapshot walks
bound villages once; the service is O(villages). Config is cached at first access.

## Changelog

- 2026-06-18 — `feat(settlement-food)` (#289): added `TaomSettlementFoodModel` fixing the Troop-Weight garrison food starvation (garrison term uses raw body count) plus MCM/JSON-tunable food knobs (consumption divisors, base/village/flat production, storage caps); MCM master toggle on by default.
