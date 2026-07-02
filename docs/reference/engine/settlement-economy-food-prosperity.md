# Settlement economy: food, prosperity, hearth, caravans

How a town/castle's food balance, prosperity, and village hearth actually compute in Bannerlord
v1.4.5, what feeds each into the others, and where TAOM's `TaomSettlementFoodModel` intervenes. All
formulas below are read from the v1.4.5 decompile (file:line cited), not inferred.

Companion feature doc: [docs/features/settlement-food.md](../../features/settlement-food.md).

## TL;DR

- A fief's daily food = **production − consumption** on the `Town.FoodStocks` pool (cap 300 town / 450 castle).
- **Consumption** is dominated by `Prosperity / 40`; the garrison adds `NumberOfAllMembers / 20`.
- **Production** is small: base +15 town / +10 castle, plus only `(hearthLevel+1) × 6` per village
  where `hearthLevel ∈ {0,1,2}` — so **≤18 food/day per village**.
- High prosperity is *designed* to outrun production and push food to a deficit — that's vanilla's
  negative feedback that caps town growth. Starvation then bleeds prosperity (`foodChange × 0.5`).
- **Garrison troops never starve to death** — they eat from the town pool, not the mobile-party path.
- **Caravans do not feed towns.** They are trade parties; food only enters a town via marketplace sales.
- **TAOM-specific:** the Troop Weight feature inflates the garrison's `NumberOfAllMembers`, so elite
  garrisons ate 2–3× the food vanilla intends. `TaomSettlementFoodModel` corrects this and exposes
  vanilla's hardcoded constants as tunable knobs.

## Food — `DefaultSettlementFoodModel`

`TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementFoodModel` (`CalculateTownFoodChangeInternal`,
lines 43–97). Net daily change = production − consumption:

### Consumption (subtracted)

| Term | Formula | Constant |
|------|---------|----------|
| Civilians (prosperity) | `town.Prosperity / 40` | `NumberOfProsperityToEatOneFood = 40` (line 32, used 47) |
| Garrison | `town.GarrisonParty.Party.NumberOfAllMembers / 20` | `NumberOfMenOnGarrisonToEatOneFood = 20` (line 34, used 48) |
| Buildings | `FoodConsumption` building effect | line 57 |
| Siege perks | Gourmet / TriageTent reduce consumption, **only while besieged** | lines 49–53 |

`Prosperity / 40` is almost always the largest consumer: a 3000-prosperity town eats **75 food/day**
from civilians alone, vs ~25 for a 500-man garrison.

### Production (added — only when NOT under siege, lines 63–77)

| Term | Formula |
|------|---------|
| Lands around settlement | **+15** town / **+10** castle (line 65) |
| Per bound village (Normal state) | `(village.GetHearthLevel() + 1) × 6` (line 72) |
| Buildings | `FoodProduction` building effect (line 76) |
| `HuntingRights` policy | +2 (lines 59–61) |
| Marketplace | each sold item whose category is `BonusToFoodStores` adds its count (lines 82–91) |

`Village.GetHearthLevel()` (`Village.cs:320`) returns only **0 / 1 / 2** (`Hearth ≥ 600 → 2`,
`≥ 200 → 1`, else `0`). So a village produces **6 / 12 / 18** food/day — never more. A 3-village town
caps around **+15 + 3×18 = +69/day**, and that requires every village at max hearth.

**Under siege, ALL production is dropped** (the `else` branch, lines 78–81) — only consumption applies.
This is the intended siege-starvation pressure.

### Storage & the daily update

- `FoodStocksUpperLimit` = **300** (town); castles add `CastleFoodStockUpperLimitBonus` = **150** → 450;
  building effects can raise it (`Town.cs:460-467`).
- Daily (`Town.cs:600-616`): `FoodStocks += FoodChange`, clamped to `[0, cap]`. At 0 →
  `Owner.RemainingFoodPercentage = -100` (the `IsStarving` flag); above 0 → `RemainingFoodPercentage = 0`.

## Prosperity — `DefaultSettlementProsperityModel`

`CalculateProsperityChangeInternal` (lines 72–200). The load-bearing terms:

| Term | Effect | Lines |
|------|--------|-------|
| **Starvation penalty** | if `IsStarving`: `prosperity += foodChange × 0.5` (foodChange < 0 → loss) | 74–79 |
| **Housing costs** | +6/+5/+4/+3/+2/+1 per day as prosperity climbs through 250/500/750/1000/1250/1500; negative above 6000…21000 | 81–131 |
| **Surplus food** | if `FoodStocks + foodChange` overflows the cap: `prosperity += overflow × 0.1` | 132–137 |
| **Market goods** | `BonusToProsperity` sold items × 0.1 | 138–145 |
| **Loyalty** | high loyalty AND `foodChange > 0` → +bonus; low loyalty → −penalty | 168–175 |
| Buildings / governor perks / kingdom policies | RoadTolls/CrownDuty/WarTax −, ImperialTowns + | 160–198 |

### The death-spiral (why "struggling" snowballs)

Prosperity is both a **food consumer** (`Prosperity/40`) and the thing food shortage attacks:

```
high prosperity ─▶ Prosperity/40 consumption ─▶ food deficit ─▶ FoodStocks hits 0 (IsStarving)
       ▲                                                                    │
       └──────────── prosperity recovers ◀── (foodChange × 0.5 penalty) ◀───┘   [drains prosperity]
```

A starving town loses prosperity at half its daily food deficit, which *eventually* lowers the
`Prosperity/40` consumption until it re-balances — that's the vanilla self-limiter. The pain the
player sees (red food, stalled growth) is this loop settling at a low equilibrium, made worse in TAOM
by large elite garrisons (next section), frequent raids zeroing village food, and the hearth-growth
penalty feats (Gondor −15%, Mirkwood −20%) keeping villages at a lower `GetHearthLevel`.

## Hearth (village growth) — `CalculateHearthChange`

Same model, lines 41–70: `+4/day` if `Hearth < 300`, `+1.2` if `< 600`, `+0.2` above; looted villages
`−1`; `GrazingRights` policy `−0.25`. Hearth feeds village **food production** (above) and militia. It
is slow to move, so the village-food term is effectively fixed in the short term.

## Garrison food is NOT the mobile-party path

`FoodConsumptionBehavior.DailyTickParty` (`CampaignBehaviors/FoodConsumptionBehavior.cs:41-48`) only
runs `PartyConsumeFood` when `MobilePartyFoodConsumptionModel.DoesPartyConsumeFood(party)` is true —
which **excludes garrisons, militia, caravans, villagers, bandits**. Consequences:

- **Garrison troops do not die from starvation.** They consume from the town `FoodStocks` pool via the
  food model above; a starving fief damages *prosperity* (and indirectly recruitment/militia), not the
  garrison roster directly.
- **The cultural food-consumption feats do not touch garrisons.** `TaomFoodConsumptionModel` extends
  `DefaultMobilePartyFoodConsumptionModel`; its Goblin +20% / Dol Guldur +10% etc. apply only to
  **mobile field parties**. A "ravenous orc garrison eating its town dry" is not a real mechanic.

## Caravans — `DefaultCaravanModel`

Caravans are trade parties: they buy/sell goods in town markets for money (`TaomCaravanModel` only
tweaks Umbar's forming cost). They **do not deliver food to a garrison or town**. Food enters a town
only as marketplace sales of `BonusToFoodStores` items (food a caravan happens to sell counts there,
like any seller). If towns are food-starved, caravans are not the lever.

## TAOM intervention — `TaomSettlementFoodModel`

### The Troop Weight leak (the bug)

`PartyBase.NumberOfAllMembers => MemberRoster.TotalManCount` in vanilla (`PartyBase.cs:381`). The Troop
Weight feature (`Patch17_TroopWeight`) postfixes that getter and bumps the result up to the *weighted*
member count (`PartyBase_NumberOfAllMembers_Patch.cs` → `TroopWeightService.CalculateWeightedMemberCount`,
which has **no garrison guard**). The food model reads exactly that getter for the garrison term
(`/20`), so an elite garrison (troop weights up to 2.0–3.0) consumed **2–3× the food vanilla intends**.
This is the "globally-weighted getter leaking into an unrelated gameplay consumer" bug-class (cf. the
phantom-wounded UI leak).

### What the model does

`Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs` (registered in `SubModule.cs`):

1. **Garrison raw-count correction (always on when the feature is enabled, siege or not):** since
   vanilla `NumberOfAllMembers == TotalManCount`, the inflation equals `weighted − raw`. The model adds
   back `(weighted − raw) / garrisonDivisor` so the garrison term uses the **raw body count**. This is
   a no-op when Troop Weight is off (weighted == raw). The global getter stays weighted, so AI strength
   reads and garrison-capacity (`DefaultSettlementGarrisonModel`) are unchanged.
2. **Tunable knobs** — vanilla's hardcoded constants become config values
   (`settlement_food/settlement_food_config.json`), so the high-prosperity squeeze can be dialed out:

   | Knob | Vanilla | Effect |
   |------|---------|--------|
   | `garrisonFoodDivisor` | 20 | ↑ = garrisons cheaper to feed |
   | `prosperityFoodDivisor` | 40 | ↑ = relieves the dominant civilian-consumption term |
   | `townBaseFood` / `castleBaseFood` | 15 / 10 | flat production floor |
   | `villageFoodMultiplier` | 6 | scales `(hearthLevel+1) × mult` per village |
   | `flatFoodBonus` | 0 | flat daily production add |
   | `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | storage caps |

   Production knobs (base/village/flat) are **siege-gated** — they never apply under siege, preserving
   the siege-starvation mechanic. Divisor and storage-cap knobs flow through the model's overridden
   virtual constants (so vanilla's own formula uses them); the garrison correction + production knobs
   are added on top of `base.CalculateTownFoodStocksChange`.

The pure math lives in `SettlementFoodService.ComputeFoodDelta` (100% unit-tested); the JSON is
validated by `SettlementFoodConfigProvider` (divisors must be ≥ 1; floats finite ≥ 0; invalid → revert
to the vanilla default with a warning). Master toggle: MCM **Settlement Food → Enable Settlement Food
Tuning** (on by default; off = vanilla engine math, garrison food stays weighted). JSON is loaded once
(`Reuse.Singleton`) → **editing it requires an app restart**, not a save reload.

### Worked example (high-prosperity Gondor city)

Prosperity 3000, a 500-man elite garrison (avg troop weight ~1.5 → reads 750), 3 villages at hearth
level 1:

| | Vanilla | TAOM (defaults, garrison fix only) |
|---|---|---|
| Production | 15 + 3×12 = **+51** | +51 |
| Civilian consumption | 3000/40 = −75 | −75 |
| Garrison consumption | 750/20 = **−37.5** (weighted) | 500/20 = **−25** (raw) |
| **Net** | **−61.5/day** | **−49/day** |

The fix recovers ~12.5 food/day here; raising `prosperityFoodDivisor` to 60 would cut the civilian
term to −50 and bring the example net positive. The prosperity term is the bigger absolute lever — the
knobs exist for exactly that.

## Town gold — the market wallet (`Town.Gold`)

Verified on installed v1.4.6 (taom-src) during the #317 investigation. Town market gold is a
separate pool from food/prosperity — it is what the player sees as the merchant's money in the
trade screen (`InventoryScreenHelper.cs:123-128`).

### Daily regeneration (the only minting source besides resident consumption)

`ItemConsumptionBehavior.DailyTickTown` → `UpdateTownGold` (:73-77) →
`DefaultSettlementEconomyModel.GetTownGoldChange` (:75-79):

```csharp
float num = 10000f + town.Prosperity * 12f - (float)town.Gold;
return MathF.Round(0.25f * num);
```

Equilibrium target `10000 + Prosperity×12`; 25% of the deficit recovered per day; **negative above
the target** (self-damping mean-reversion). Initial town gold: 20,000 (`Town.InitialTownGold`).
**Castles never receive this tick** — `DailyTickTownEvent` iterates `Town.AllTowns` (towns only;
`CampaignPeriodicEventManager.cs:238` → `Town.cs:294-296`), and `GetTownGoldChange` has exactly one
engine caller.

### Drains and inflows (all drains bounded by goods value, never by pool size)

| Flow | Direction | Site |
|------|-----------|------|
| Daily regen (above) | ± | `ItemConsumptionBehavior.cs:76` |
| Resident consumption — town sells stock to simulated residents, budget = prosperity-scaled demand × priceIndex^0.3 | + | `MakeConsumption` :170, `CalculateDailySettlementBudgetForItemCategory` |
| Villager deliveries — town buys, spend capped at `town.Gold/price` (can pin a town at ~0) | − | `SellGoodsForTradeAction.cs:52-57` |
| AI-lord loot sales (proceeds → lord's personal gold) | − | `PartiesSellLootCampaignBehavior.cs:25-39` |
| Caravan sales to town (double-capped by town gold) | − | `CaravansCampaignBehavior.cs:1179-1193` |
| Player loot sales in the trade screen | − | `InventoryScreenHelper.cs:123-128` |
| Workshops: town pays for outputs / workshop pays for inputs | −/+ | `WorkshopsCampaignBehavior.cs:844-868` |
| Tariff skim when anyone buys FROM the town (70% town commission → `TradeTaxAccumulated` → owner clan) | − | `SellItemsAction.cs:70,86-91` |

**Garrison wages never touch town gold** — they are a clan expense
(`DefaultClanFinanceModel.AddPartyExpense` :825-862 deducts from the clan leader or the party's
`PartyTradeGold`). Prosperity never reads gold, so there is no gold→prosperity feedback loop.

**TAOM-specific:** drains run ~2× vanilla (2.2× LOTRLOME computed item values #318; +22% villager
deliveries from 2.78 avg bound villages/town), which pinned towns at ~0 gold —
`TaomSettlementEconomyModel` (Main/Features/SettlementEconomy/, #317) exposes the three formula
constants as knobs, shipping base 25000. See
[docs/features/settlement-economy.md](../../features/settlement-economy.md).

## See also

- [campaign-tick-time-and-party-ai.md](campaign-tick-time-and-party-ai.md) — the DailyTick heartbeat
  that drives `FoodStocks`/prosperity updates.
- [campaign-object-graph.md](campaign-object-graph.md) — `Town`/`Village`/`Settlement` relationships
  (`Settlement.Culture` not engine-saved; castle `.Village == null`).
- [docs/features/troop-weight-system.md](../../features/troop-weight-system.md) — the weighted
  `NumberOfAllMembers` getter that this model corrects for the garrison food term.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-economy.md](../../features/settlement-economy.md)
- [docs/features/settlement-food.md](../../features/settlement-food.md)
- [docs/INDEX.md](../../INDEX.md)

<!-- backlinks-end -->
