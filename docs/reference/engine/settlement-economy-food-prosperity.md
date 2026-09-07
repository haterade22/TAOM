# Settlement economy: food, prosperity, hearth, caravans

How a town/castle's food balance, prosperity, hearth, market gold and caravan movement actually
compute, what feeds each into the others, and where TAOM intervenes. Every formula below is read
from a decompile with the file:line cited, never inferred.

**Verification baseline per section.** The food section (constants, production terms, hearth
thresholds, the storage cap, and the garrison-starvation path) was re-read from the **installed
v1.4.8** `TaleWorlds.CampaignSystem.dll` on 2026-09-06 and every constant still held; the prosperity
section is still **v1.4.5** and unrefreshed; the town-gold and caravan sections were read from
installed **v1.4.6** (#317) and **v1.4.7** (#391) and are marked as such inline. Treat the older
sections as accurate-but-unrefreshed: re-verify before relying on an exact constant after an engine
bump. Note `taom-src.ps1` needs PowerShell 7, which is not installed everywhere; `ilspycmd -t <Type>`
against the installed DLL is the fallback and is equally authoritative.

Companion feature docs: [settlement-food.md](../../features/settlement-food.md),
[settlement-economy.md](../../features/settlement-economy.md),
[economy-diagnostics.md](../../features/economy-diagnostics.md).

## TL;DR

- A fief's daily food = **production − consumption** on the `Town.FoodStocks` pool (cap 300 town / 450 castle before buildings; 800 / 750 fully built).
- **Consumption** is dominated by `Prosperity / 40`; the garrison adds `NumberOfAllMembers / 20`.
- **Production** is small: base +15 town / +10 castle, plus only `(hearthLevel+1) × 6` per village
  where `hearthLevel ∈ {0,1,2}` — so **≤18 food/day per village**.
- High prosperity is *designed* to outrun production and push food to a deficit — that's vanilla's
  negative feedback that caps town growth. Starvation then bleeds prosperity (`foodChange × 0.5`).
- **Garrison troops eat from the town pool, not the mobile-party path, and they DO starve to death**:
  10%/day once production drops below `garrison / 20` (see below; this bullet said the opposite until
  2026-09-06).
- **Caravans do not feed towns.** They are trade parties; food only enters a town via marketplace sales.
- **TAOM-specific:** vanilla's flat production cannot support TAOM's map. Consumption is linear in
  prosperity while production is flat, so any fief above roughly `production * 40` prosperity starves
  by arithmetic; vanilla is tuned right at that line and TAOM ships 64 towns above 3,000. Measured
  2026-09-06: **70 of 72 towns started food-negative before garrison**. `TaomSettlementFoodModel`
  exposes vanilla's constants as knobs, adds a prosperity-scaled production term vanilla has no
  equivalent of, and since #546 ships tuned values rather than vanilla ones.
  (The historical Troop Weight garrison inflation described below is an inert no-op today.)

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

- `FoodStocksUpperLimit` = **300** (town); castles add `CastleFoodStockUpperLimitBonus` = **150** → 450.
  Building effects raise it on top (`Town.cs:460-469`): `FoodStock` is granted by exactly two
  buildings, Warehouse `+100/300/500` and Castle Granary `+100/200/300`
  (`DefaultBuildingTypes.cs:220-222,275-277`), so a fully upgraded town caps at **800** and a castle
  at **750**. Building levels therefore decide how long a fief survives a deficit, which is why
  [settlement-building-levels.md](../../features/settlement-building-levels.md) is part of this story.
- New campaigns start every town at **full** stocks
  (`FoodConsumptionBehavior.OnNewGameCreatedPartialFollowUpEnd`), so a structural deficit presents as
  a slow slide over the first weeks rather than an immediate failure.
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

- **Garrison troops eat from the town pool, not the mobile-party path.** They consume from
  `FoodStocks` via the food model above rather than through `FoodConsumptionBehavior`.
- **But they DO die from starvation, through a different route.** (Corrected 2026-09-06 against
  installed v1.4.8; this section previously claimed they never do, which sent at least one
  investigation down the wrong path.) `DefaultPartyHealingModel.GetDailyHealingForRegulars:132-141`
  applies a negative "healing" of `TotalRegulars * 0.1` to a garrison whose settlement is starving,
  so **10% of garrison regulars die per day**. Two gates must both hold:

  | Gate | Source | Meaning |
  |---|---|---|
  | `settlement.IsStarving` | `Settlement.IsStarving => Town.FoodStocks <= 0f` | the store is actually empty |
  | `SettlementHelper.IsGarrisonStarving` (:549-557) | `Town.FoodChange < -Town.Prosperity / NumberOfProsperityToEatOneFood` | see below |

  Substituting the food formula into the second gate, the `Prosperity/40` terms cancel and it reduces
  to **`production < garrison / 20`**: the bleed starts once a fief's food production falls below its
  garrison's own consumption. So a deficit driven purely by civilians never kills troops, however
  deep it gets; a deficit where the garrison alone outweighs production does. The threshold garrison
  size is therefore `production * 20`, which is why a village-poor high-prosperity town cannot hold
  troops. Field parties are hit far harder, losing 25%/day (:144-145).

  **TAOM does not modify this path, despite appearances.** `TaomPartyHealingModel` (BattleBalance) is
  in the GameModel registry against `DefaultPartyHealingModel`, so it looks like a TAOM-owned
  mechanic. It overrides only `GetSurvivalChance` and `GetDailyHealingHpForHeroes`; the regulars
  path that carries the starvation kill is untouched, so the 10%/day above is pure vanilla behaviour
  (verified 2026-09-06).
- **The cultural food-consumption feats do not touch garrisons.** `TaomFoodConsumptionModel` extends
  `DefaultMobilePartyFoodConsumptionModel`; its Goblin +20% / Dol Guldur +10% etc. apply only to
  **mobile field parties**. A "ravenous orc garrison eating its town dry" is not a real mechanic.

## Caravans — `DefaultCaravanModel`


Caravans are trade parties: they buy/sell goods in town markets for money. (`TaomCaravanModel` tweaks
Umbar's forming cost + CaravanTrade's initial-trade-gold / per-category buy caps, and `Patch59_CaravanTrade`
reshapes caravan destination ranging, war-time trade, and basket breadth — see
[caravan-trade](../../features/caravan-trade.md).) They **do not deliver food to a garrison or town**. Food enters a town
only as marketplace sales of `BonusToFoodStores` items (food a caravan happens to sell counts there,
like any seller). If towns are food-starved, caravans are not the lever.

### Why a caravan parks in a town (verified v1.4.7, #391)

`CaravansCampaignBehavior.HourlyTickParty` (:617-683) is the **sole** source of movement orders for a
caravan — every generic AI behavior early-returns on `IsCaravan` (`AiVisitSettlementBehavior.cs:140-143`,
`AiMilitaryBehavior.cs:489`, `AiPatrollingBehavior.cs:74`, `AiEngagePartyBehavior.cs:36`). If it issues
no order, nothing else will, and `MobileParty.CheckExitingSettlementParallel` (:4085-4104) will not
release a party whose `ShortTermTargetSettlement` is its current settlement. Every way a caravan gets
stuck is a **silent early-return** — no log, no event.

Inside a fortification it only re-decides when all of :631 holds (not besieged; `ShortTermBehavior !=
FleeToPoint`; `!Ai.IsAlerted`; and `IsCurrentlyUsedByAQuest || randomFloat < 1/3`), then rolls the
wounded ladder (:633-659):

| Wounded | Chance to re-decide |
|---|---|
| ≥ 40% | **0 — never** |
| ≥ 20% / 10% / 5% / 2.5% | 0.1 / 0.2 / 0.3 / 0.4 |
| < 2.5% | 1.0 |

**The boundaries are inclusive, not exclusive**, despite being written `(double)num > 0.4`: `num` is a
`float`, widening puts the nearest float to 0.4 at `0.40000000596…` — above the double — and the same
holds at every band edge. So exactly 40% wounded means *never leaves*.

Two gates are easy to miss. `Ai.IsAlerted` is set **only** in the flee branch (`MobilePartyAi.cs:557-560`)
and recomputed each think, so a caravan sheltering from a nearby battle stays alerted indefinitely.
`ShortTermBehavior == Hold` — applied by `OnSiegeEventStarted` (:317-326) to every caravan in a
besieged settlement — is the second disjunct of the exit guard; it outlives the siege and clears only
on the next re-decide.

Even when it re-decides it can fail to leave: `FindNextDestinationForCaravan` (:911-939) requires a
**strictly positive** score, and `GetTradeScoreForTown`'s buy half is capped at
`(int)(0.5 × PartyTradeGold)` (:1022). A caravan with ~0 gold and no cargo scores exactly 0 at every
town, `ThinkNextDestination` returns null, and it is parked permanently.

**How a broke town causes this indirectly.** Town gold appears nowhere in the caravan decision path;
it only clamps sale volume to `town.Gold / itemPrice` (:1179-1182, :1191-1194), which at a near-zero
pool rounds to zero quantity. But `HourlyTickParty` calls `BuyGoods` (:672) *before*
`ThinkNextDestination` (:677) with no broke-check, so a caravan that cannot sell spends its purse
anyway — and lands in the zero-score trap above. Diagnose with `taom.print_caravans`
([economy-diagnostics](../../features/economy-diagnostics.md)).

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
2. **Tunable knobs, shipping NON-vanilla since #546** (`settlement_food/settlement_food_config.json`).
   The compiled defaults all equal the vanilla constant, but the shipped JSON does not, and that
   reversal is the fix: it shipped fully vanilla from #289 until 2026-09-06, which is why the knobs
   existed while every town still starved.

   | Knob | Vanilla | Shipped | Effect |
   |------|---------|---------|--------|
   | `garrisonFoodDivisor` | 20 | 20 | ↑ = garrisons cheaper to feed |
   | `prosperityFoodDivisor` | 40 | **45** | ↑ = relieves the dominant civilian-consumption term |
   | `townBaseFood` / `castleBaseFood` | 15 / 10 | **30** / 10 | flat production floor |
   | `villageFoodMultiplier` | 6 | **8** | scales `(hearthLevel+1) × mult` per village |
   | `flatFoodBonus` | 0 | **5** | flat daily production add |
   | `hinterlandFoodPerProsperity` | none | **0.02** | adds `prosperity × rate` to production |
   | `foodStocksUpperLimit` / `castleFoodStockUpperLimitBonus` | 300 / 150 | 300 / 150 | storage caps |

   Production knobs (base/village/flat/hinterland) are **siege-gated**: they never apply under siege,
   preserving the siege-starvation mechanic. Divisor and storage-cap knobs flow through the model's
   overridden virtual constants (so vanilla's own formula uses them); the garrison correction +
   production knobs are added on top of `base.CalculateTownFoodStocksChange`.

3. **The hinterland term (new in #546), and why it exists.** Every other knob is flat, and flat knobs
   cannot hold a balance across a 600 to 5,100 prosperity range because consumption scales with
   prosperity and production does not. Worse, prosperity MOVES during play, so a town tuned to break
   even starves again once it grows. `hinterlandFoodPerProsperity` adds `prosperity × rate` to
   production, making the balance shape stable at any size.

   **It must stay strictly below `1 / prosperityFoodDivisor`.** At or above that the two terms cancel,
   net food stops falling as prosperity rises, the store overflows daily, and vanilla's surplus rule
   (`prosperity += overflow × 0.1`, above) inflates prosperity without limit, dragging town gold
   (`10000 + Prosperity×12`) and garrison caps up with it. The provider enforces this against the
   sanitized divisor; `SettlementFoodShippedConfigTests` fails the build if the shipped file violates it.
   `Town.Prosperity` is engine-sourced, so the multiply is gated on `FiniteFloatValidator.IsFinite`:
   a NaN would otherwise leave `FoodStocks` permanently NaN (both `Town.DailyTick` clamps are false for
   NaN) inside a `[SaveableProperty]`. See [rca-settlement-food-2026-09-06.md](../../reviews/rca-settlement-food-2026-09-06.md).

The pure math lives in `SettlementFoodService.ComputeFoodDelta` (100% unit-tested); the JSON is
validated by `SettlementFoodConfigProvider` (divisors must be ≥ 1; floats finite ≥ 0; the hinterland
rate strictly below `1/divisor`; invalid → revert to the compiled default with a warning). Master
toggle: MCM **Settlement Food → Enable Settlement Food Tuning** (on by default; off = vanilla engine
math). JSON is loaded once (`Reuse.Singleton`) → **editing it requires an app restart**, not a save
reload. Note the config only reaches the game if `Modules/TAOM/ModuleData/settlement_food/` exists in
the DEPLOYED module; if it is absent the provider silently falls back to the vanilla compiled defaults
and logs a "not found" warning.

### Worked example (high-prosperity Gondor city)

Prosperity 3000, a 500-man garrison, 3 villages at hearth level 1:

| | Vanilla | TAOM shipped (#546) |
|---|---|---|
| Base production | 15 | 30 |
| Village production | 3×12 = +36 | 3×(1+1)×8 = **+48** |
| Flat | 0 | **+5** |
| Hinterland | none | 3000×0.02 = **+60** |
| **Production total** | **+51** | **+143** |
| Civilian consumption | 3000/40 = −75 | 3000/45 = **−66.7** |
| Garrison consumption | 500/20 = −25 | −25 |
| **Net** | **−49/day** | **+51.3/day** |

The hinterland term is doing most of the work (+60 of the +92 production swing), and it is the only
one that keeps working as the town grows: at prosperity 6,000 the same town nets +44/day rather than
sliding further negative, because the rate sits below `1/45`. Raising `prosperityFoodDivisor` alone
was the pre-#546 advice and it does not scale, since it shifts the break-even prosperity without
changing the fact that consumption grows and flat production does not.

(An older version of this example modelled a Troop-Weight-inflated garrison reading 750 instead of
500. That inflation ended with the 2026-07-11 count-to-limit rework, so the garrison term is the raw
body count in both columns now.)

## Town gold — the market wallet (`Town.Gold`)

Verified on installed v1.4.6 (taom-src) during the #317 investigation; re-verified against v1.4.7
during #391. Town market gold is a separate pool from food/prosperity — it is what the player sees as
the merchant's money in the trade screen (`InventoryScreenHelper.cs:123-128`).

**`SettlementComponent.ChangeGold(int)` is the pool's SOLE mutator.** `Gold` has a private setter
(`SettlementComponent.cs:23-24`) and every writer routes through `ChangeGold` (:117-124), which also
**hard-floors the pool at zero**:

```csharp
public void ChangeGold(int changeAmount)
{
    Gold += changeAmount;
    if (Gold < 0) Gold = 0;
}
```

Two consequences worth holding on to. A payment larger than the balance is **silently truncated** —
the excess vanishes rather than creating debt, so a naive read of a caller's intended amount
overstates the real drain. And because there is exactly one write path, a single instrumentation
point observes 100% of movement with no possibility of a missed site — which is what makes
`Patch68_EconomyDiagnostics` ([economy-diagnostics](../../features/economy-diagnostics.md))
trustworthy enough to act on.

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
- [docs/features/settlement-building-levels.md](../../features/settlement-building-levels.md) — the
  LIVE-file data pass that seeds each fief's starting building LEVELS (the buildings whose effects
  feed the food-production and prosperity terms above).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-economy.md](../../features/settlement-economy.md)
- [docs/features/settlement-food.md](../../features/settlement-food.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/modding/balance-levers.md](../../modding/balance-levers.md)
- [docs/modding/settlements.md](../../modding/settlements.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)

<!-- backlinks-end -->
