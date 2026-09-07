# RCA: SettlementFood hinterland production term, deep-review (2026-09-06)

**Top line:** A player reported cities running out of food and being unable to support their lords
(#546). The cause was not a code bug but a map/formula mismatch: 70 of 72 towns start food-negative
because vanilla consumption is linear in prosperity while production is flat, and TAOM's map ships 64
towns above 3,000 prosperity. The fix adds a prosperity-scaled production term. The 5-agent
deep-review then found **one HIGH on the fix itself**: the new term feeds an engine-sourced float
(`Town.Prosperity`) into a decision gate written as an equality check, which NaN passes. Fixed
in-session with a positive-requirement gate at the input plus a finiteness refusal at the output, 6
regression tests, and a proof that all 4 NaN tests fail without the gates. Full suite 8175 passed (4
failures pre-existing on this branch, confirmed against a pristine `HEAD`).

## Findings

| # | Sev | Bug | Category | Why the implementation had it | Preventive action |
|---|-----|-----|----------|-------------------------------|-------------------|
| 1 | **HIGH** | `ApplyFoodAdjustment` gated on `if (delta == 0f) return;`. `NaN == 0f` is false, so a NaN delta reaches `ExplainedNumber.Add`. Source: the new `snapshot.Prosperity * config.HinterlandFoodPerProsperity` term, the first engine-sourced float in this path. | Engine-float decision gate | The author reasoned about the CONFIG float (validated finite by `SettlementFoodConfigProvider`) and not about the ENGINE float the new term introduced. Before this change every input to `ComputeFoodDelta` was a validated config value or an int, so the equality gate was genuinely safe; the change silently invalidated that precondition without the gate being revisited. | Gate the input as a positive requirement (`if (FiniteFloatValidator.IsFinite(...))`), plus refuse any non-finite delta at the service's exit. Scope-widen the rule (below). |
| 2 | LOW | No test for `ProsperityFoodDivisor` at its extreme valid values (1 and 10000) interacting with the hinterland bound. | Test coverage | Tests covered the invariant at the shipped divisor (45) and at the boundary, but not that the bound *tracks* the divisor rather than being a constant. | Two tests added: at divisor 1 the bound widens to 1.0 and accepts 0.5; at divisor 10000 it tightens to 0.0001 and rejects the shipped 0.02. |
| 3 | INFO (no change) | `TownFoodSnapshot.FromTown` allocates a `List<int>` per call; the efficiency agent rated it MEDIUM and proposed caching by last-town. | Performance | Not introduced by this change, and the severity was asserted without verifying call frequency. | Rejected, with reasoning recorded below. |

## Why the HIGH consequence is worse than "a wrong number for one day"

Verified against installed v1.4.8, not inferred:

- `Town.Prosperity`'s setter is `_prosperity = value; if (_prosperity < 0f) _prosperity = 0f;`.
  `NaN < 0f` is false, so **NaN passes the clamp and is stored**.
- `Town.DailyTick` does `FoodStocks += FoodChange`, then `if (FoodStocks < 0f)` and
  `if (FoodStocks > FoodStocksUpperLimit())`. **Both are false for NaN**, so neither clamp fires.
- `FoodStocks` is `[SaveableProperty(100)]` on `Fief`.

So a single NaN would leave a settlement's food stock permanently NaN, with the starvation flag never
set and the value written into the save file. This is why the finding was fixed rather than deferred
on low reachability.

Reachability is genuinely low: vanilla's own `town.Prosperity / NumberOfProsperityToEatOneFood` term
already consumes the same unguarded field every tick, so TAOM is adding a second consumer of an
existing exposure rather than creating one. It would take a separately broken `SettlementProsperityModel`
to inject the NaN. The rule is mandatory regardless, and the fix is one line.

## Root-cause pattern: the precondition that changed underneath the gate

Finding 1 is not "the author forgot a NaN check." The gate `if (delta == 0f)` was **correct when
written**, because every input to `ComputeFoodDelta` was either an int or a config float the provider
had already validated finite. The change added the first input that bypasses that validation, which
retroactively made a previously-safe gate unsafe without touching the gate's line.

That is the generalisable lesson, and it is not what the existing rule says. `csharp-architecture.md`
"Engine-Float Decision Gates" is written as *"when you write a gate on an engine float, write it as a
positive requirement."* It does not say *"when you introduce a new input to an existing calculation,
re-audit every downstream gate against the new input's provenance."* The author followed the rule as
written for the code being added and still shipped the bug, because the defective line was
pre-existing and unmodified.

**This is instance #6 of the NaN-gate class** (Career cooldown #31, EditorCacheRebuild #38, CS_Road
2026-05-13, CombatMechanics 2026-07-02, TroopWeight 2026-07-17). The rule itself says: *"If a 6th
instance appears in a category this section doesn't name, widen the scope again rather than patching
the instance."* The category this instance adds is **provenance change**, not a new float type.

## Why each deep-review agent did or did not catch it

| Agent | Result | Why |
|---|---|---|
| 1 Standards | **CAUGHT** | Its prompt carries the engine-float rule explicitly. Note its suggested fix (`float.IsFinite`) would NOT compile: net472 has no `float.IsFinite`, which is precisely why `FiniteFloatValidator` exists. A finding can be right about the bug and wrong about the fix. |
| 2 Compatibility | Correctly out of scope | It verified `Town.Prosperity` is a plain stored field with no reentrancy (the risk it was asked about) and confirmed all 20 API shapes. Establishing the field is unguarded was its contribution to the finding. |
| 3 Efficiency | Not its remit | Asserted a MEDIUM allocation cost without verifying call frequency, which its own prompt forbids. See below. |
| 4 Completeness | Missed | It checked that every *validation rule* had a test, and every config-side rule did. It had no reason to ask whether an un-validated engine input needed one. Test-coverage review keys on the code that exists, not the guard that is absent. |
| 5 Data Flow | **CAUGHT**, and traced furthest | It followed the value past TAOM's boundary into `Town.DailyTick` and found the permanent-corruption consequence and the `[SaveableProperty]` persistence, which is what escalated this from "one bad day of food" to HIGH. This is again the highest-value agent. |

Two independent agents finding it is the reason it was treated as confirmed rather than as a
hypothesis, but both were still re-verified against the decompiled source before the fix landed, per
`evidence-over-claims.md` §A.

## The rejected finding, and why

The efficiency agent rated the `List<int>` allocation in `FromTown` MEDIUM and proposed caching the
snapshot on the model keyed by `_lastCachedTown != town`. Rejected on three grounds:

1. **The frequency claim was unverified.** Checking every reader of `Town.FoodChange` in the installed
   assemblies: `SettlementHelper.IsGarrisonStarving`, `DefaultSettlementProsperityModel`, and
   `Town.DailyTick`, all daily; the UI readers are tooltip- and refresh-driven, not per-frame. That is
   roughly 3 calls per fief per game day, about 600 small short-lived allocations per in-game day.
   Negligible. The agent's own prompt says an unverified cost claim is reported as UNVERIFIED, never
   as a severity.
2. **The proposed fix is a correctness regression.** A one-entry cache keyed on the town reference
   returns a stale snapshot when garrison size or village state changes between two reads in the same
   day, and the engine iterates all ~204 fiefs, so the hit rate would be near zero while the staleness
   risk is real.
3. **It is pre-existing.** The allocation predates this change; only the `Prosperity` field was added.

Per `simplicity-criterion.md`: tiny win, added complexity, plus a correctness risk. Reject.

## Preventive actions

1. **Scope-widen the engine-float rule** in `.claude/rules/csharp-architecture.md` to cover provenance
   change: when adding a new input to an existing calculation, re-audit the downstream gates against
   the new input's provenance, because a gate that was safe under the old input set can be silently
   invalidated without its line changing.
2. **Add the same question to deep-review Agent 5's prompt**, which is where it would most reliably
   fire, phrased as: *for every value newly introduced into an existing calculation, identify which
   validation the old inputs enjoyed that the new one does not.*
3. No new feedback memory: this is a scope extension of an existing mandatory rule, not a new pattern.
   Manufacturing a second rule alongside the one that already covers it would make both weaker.

## Lessons entry

Appended to `docs/reviews/lessons/gamemodels-services.md` (the cross-feature record; this file is the
incident report).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-food.md](../features/settlement-food.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)
- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
