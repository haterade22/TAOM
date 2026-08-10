# EconomyDiagnostics

> GitHub issue: **#391**

## Overview

Two read-only console diagnostics for the linked "broke town / parked caravan" symptom:
`taom.print_town_ledger [town]` attributes a town's market-gold movement by day and by who moved it,
and `taom.print_caravans [settlement]` names which engine gate is holding
each caravan. One Harmony category (`Patch68_EconomyDiagnostics`, five targets) plus two pure
services. No gameplay change.

## Why This Exists

A player reported caravans sitting inside Minas Tirith indefinitely, with the town's merchant
holding **173 denars**, and read the two as cause and effect: the town is broke, so the caravans are
waiting for it to be able to pay.

Reading the v1.4.7 engine showed both halves are real problems but the causal link runs the other
way, and neither half is observable in-game:

1. **The town's regen is fine.** `DefaultSettlementEconomyModel.GetTownGoldChange` mean-reverts
   toward `base + Prosperity×12` at 25% of the deficit per day, once per day, as the last step of
   `ItemConsumptionBehavior.MakeConsumptionInTown`. With TAOM's shipped base of 25000 (#317) and
   Minas Tirith's prosperity of 4345, the target is ~77,000 and the mint at 173 gold owes
   **~19,242/day**. The pool is not failing to fill — roughly 19k/day is leaving it. #317 raised the
   ceiling without touching the drain.
2. **Town gold does not park a caravan.** No gate anywhere in the caravan decision path reads
   `Town.Gold`. Its only effect is clamping sale volume to `town.Gold / itemPrice`
   (`CaravansCampaignBehavior` :1179-1182, :1191-1194), which at 173 denars silently rounds to zero
   quantity — no items move, no gold moves, no event, no log.
3. **But it starves them into it.** `HourlyTickParty` calls `BuyGoods` (:672) *before*
   `ThinkNextDestination` (:677), and `BuyGoods` has no "am I broke?" guard. A caravan arrives, can't
   sell, spends its remaining purse anyway, and drops to `PartyTradeGold ≈ 0` — which zeroes the buy
   half of the trade score (capped at `(int)(0.5 × PartyTradeGold)`, :1022) for **every town in the
   world**. With no cargo either, every candidate scores exactly 0,
   `FindNextDestinationForCaravan`'s strictly-greater-than-zero test (:928) never passes, no AI
   action is set, and the caravan is parked permanently.

Neither fault reports anything. Every way a caravan gets stuck is a silent early-return, and no
engine code logs a town-gold movement. The last two economy passes each shipped a fix for one half
of a two-part problem; these commands exist so the third one doesn't have to guess.

## The two commands

### `taom.print_town_ledger [town]`

Per-day market-gold movement broken down by flow, plus window totals and a one-line summary naming
the **largest drain** (not the largest flow — the daily mint is usually the biggest single number
and is an inflow).

| Flow | Engine site |
|------|-------------|
| `DailyMint` | `ItemConsumptionBehavior.UpdateTownGold` → `GetTownGoldChange` |
| `ResidentConsumption` | `ItemConsumptionBehavior.MakeConsumption` — residents buying shelf stock |
| `VillagerDelivery` | `SellGoodsForTradeAction` — the prime suspect, see below |
| `Trade` | `SellItemsAction` — **AI only** (all 7 callers gate on `IsMainParty`): caravan sales/purchases, AI party food + horse purchases, AI lord loot sales |
| `Other` | anything untagged: **the player's own trade screen**, workshops, bandit-loot returns, the 20,000 town init |

**The prime suspect.** `SellGoodsForTradeAction.ApplyInternal` (:52-57) walks a villager's entire
roster buying `min(qty, town.Gold / itemPrice)` of each, with **no reserve and no floor**. One
convoy can legally spend a town to zero every day. TAOM towns average 2.78 bound villages (vs
vanilla's 2.27), and #318 (LOTRLOME items compute ~2.2× vanilla value, still open) scales every
drain because every drain is bounded by goods value.

**Not a suspect, despite appearances.** Caravans are two-way: selling *to* a town pays out
(`SellItemsAction` :60) but buying *from* one credits the town the **full** price (:75) and only
skims the tax back (:86) — a net inflow of `num × (1 − taxRatio)`. More caravan traffic did not
empty Minas Tirith.

### `taom.print_caravans [settlement]`

Every caravan currently inside a settlement, with a per-caravan verdict and a gate histogram. The
histogram is what turns a list into a diagnosis: five caravans all reading `Alerted` says "there is
a battle nearby", which no single line conveys.

| Gate | Engine site | Nature |
|------|-------------|--------|
| `AiDisabled` | `MobileParty.CheckExitingSettlementParallel` :4087 | cannot physically exit at all |
| `HeldInPlace` | same guard, 2nd clause + `OnSiegeEventStarted` :317-326 | set by a siege; outlives it by a few hours, then self-clears on the next re-decide — permanent only if the caravan is *also* trapped |
| `InMapEvent` | `HourlyTickParty` :625 | transient |
| `TradeInactive` | :625 | permanent — `ConvertPartyToCaravanParty` never calls `InitializePartyTrade` |
| `DecisionsSuppressed` | :625 | quest flag; if no quest is live this is a quest-cleanup bug |
| `HoldingForSiege` | :317-326, :631 | transient |
| `Fleeing` / `Alerted` | :631, `MobilePartyAi.cs:557-560` | lasts as long as enemies are near |
| `WoundedCannotLeave` | :635-638 | at/above 40% wounded the chance is exactly zero |
| `WoundedUnlikelyToLeave` | :635-659 | will leave, slowly — the report quotes the hourly rate |
| `NoViableDestination` | :928 + :1022 | permanent without intervention |

**The 40% boundary is inclusive, not exclusive.** The engine writes `(double)num > 0.4`, which reads
as "40% exactly is fine". It is not: `num` is a `float` and the comparison widens it, and the
nearest float to 0.4 is `0.40000000596…`, which beats the double `0.4`. The same widening pushes
*every* band boundary up into the harsher band. Verified against IEEE-754 single→double semantics
and pinned by `Evaluate_WoundedFraction_MatchesEngineLadder`.

## Architecture

Thin patches → pure services, TaleWorlds-free, ids as strings at the boundary (ADR-007).

```
SettlementComponent.ChangeGold (sole mutator)  ──recorder──▶  ITownGoldLedger  ──▶ taom.print_town_ledger
        ▲ ambient tag claimed outermost-wins
        └── 4 flow-tag Prefix/Postfix pairs (TownGoldFlowScope)

MobileParty.AllCaravanParties ──adapt at cheat boundary──▶ CaravanGateDiagnosticsService ──▶ taom.print_caravans
```

**Why one recorder is complete:** `SettlementComponent.Gold` has a private setter and `ChangeGold`
is its only mutator, so a single patch there sees 100% of flows and no site can be missed. That
property is what makes the ledger trustworthy enough to act on, and it is pinned by a binding test.

**Prefix+Postfix, not the argument:** `ChangeGold` hard-floors at zero, so an over-payment is
silently truncated. Recording the requested amount would overstate the drain and hide the clamp.

**The caravan census needs no reflection at all** — `MobilePartyAi.IsAlerted`,
`.DoNotMakeNewDecisions` and `.IsDisabled` are all public getters, as is everything else it reads.
It deliberately does **not** invoke the private `ThinkNextDestination` to answer the zero-score
question: that call mutates a scratch cache, and a diagnostic must not perturb what it measures. It
reports the two inputs that make a zero score inevitable instead.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/EconomyDiagnostics/TownGoldLedger.cs` | Bounded per-town/per-day ring + the `TownGoldFlow` enum |
| `Main/Features/EconomyDiagnostics/TownGoldFlowScope.cs` | `[ThreadStatic]` outermost-wins flow tag |
| `Main/Features/EconomyDiagnostics/EconomyDiagnosticsBehavior.cs` | Day roll + cross-campaign clear |
| `Main/Features/EconomyDiagnostics/Hooks/SettlementComponent_ChangeGold_Patch.cs` | The recorder |
| `Main/Features/EconomyDiagnostics/Hooks/TownGoldFlowTagPatches.cs` | The four tag pairs |
| `Main/Features/EconomyDiagnostics/Cheats/EconomyDiagnosticsCheats.cs` | `taom.print_town_ledger` |
| `Main/Features/CaravanTrade/Diagnostics/CaravanGateDiagnosticsService.cs` | The parking verdict |
| `Main/Features/CaravanTrade/Diagnostics/CaravanGateSnapshot.cs` | POCO + `CaravanGate` + verdict |
| `Main/Features/CaravanTrade/Cheats/CaravanTradeCheats.cs` | `taom.print_caravans` |

Wiring: `Main/IoC.cs`, `Main/SubModule.cs` (`Patch68_EconomyDiagnostics` in the campaign-phase block
+ `EconomyDiagnosticsBehavior`).

## Tests

- `CaravanGateDiagnosticsServiceTests` — one test per gate, gate precedence, the wounded ladder
  including all five float-widening boundaries, the zero-men NaN guard, and scope (fortification-only
  gates must not fire on the open map).
- `TownGoldLedgerTests` — accumulation, per-flow and per-town separation, ring eviction, blank-id and
  zero-delta rejection, newest-first ordering, cross-campaign clear.
- `TownGoldFlowScopeTests` — the outermost-wins rule and the stuck-tag reset.
- `EconomyDiagnosticsBindingTests` — all five patch targets against the installed engine, plus
  `ChangeGold`'s arity and `Gold`'s getter.
- Both `*CheatsFormatTests` — report shape, including "an inflow is never reported as a drain" and
  "a capped list says it was capped".

## Known limitations

- **Workshops are untagged** and land in `Other`. Deliberate: a large `Other` is the signal to
  instrument them next, and adding a fifth tag before the data asks for it is speculative. Note that
  `WorkshopsCampaignBehavior.CanNotableWorkshopProduceThisCycle` (:776-792) *halts production* when
  `Town.Gold < outputIncome`, so a broke town stops putting goods on the shelf, which kills the
  `ResidentConsumption` inflow — a self-sustaining spiral the ledger should make visible.
- **`Trade` is AI-only, and one bucket.** All seven `SellItemsAction.Apply` callers gate on
  `IsMainParty`, so the player's own market trades are **not** in it — they reach `ChangeGold` through
  `InventoryLogic.DoneLogic` → `MerchantInventoryListener.SetGold` and land in `Other`. **A large
  `Other` is therefore not evidence of a workshop drain — check your own trading first.** Note the
  asymmetry: a player *sale* is clamped by the zero-floor, but a player *purchase* credits the town
  uncapped and can mask a real drain in the NET line. If splitting is ever warranted the target is
  the public `InventoryLogic.DoneLogic`, never the private nested listener.
- **The ledger starts empty at campaign load** — allow 3–5 in-game days for a clear read.
- **No parked-duration tracking.** The census reports *why* a caravan is blocked, not how long it has
  been. Duration would need its own behavior; deferred until the gate data says it is worth it.

## References

- Engine background: `docs/reference/engine/settlement-economy-food-prosperity.md`
- `docs/features/settlement-economy.md` (#317 — the regen side), `docs/features/caravan-trade.md`
  (#329/#335 — caravan *routing*, distinct from whether they move at all)
- Open amplifier: #318 LOTRLOME item-value rebaseline

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/auto-resolve-diagnostics.md](./auto-resolve-diagnostics.md)
- [docs/features/caravan-trade.md](./caravan-trade.md)
- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/settlement-economy.md](./settlement-economy.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
