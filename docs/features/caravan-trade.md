# CaravanTrade

## Overview

Makes AI (and optionally player) caravans range across the map instead of shuttling between very-close towns (e.g. Minas Tirith ↔ East/West Osgiliath), trade across TAOM's endless Free-vs-Evil war, and carry fuller baskets of goods. Four coordinated Harmony postfixes on the vanilla `CaravansCampaignBehavior` plus two `TaomCaravanModel` overrides, all delegating to one pure `ICaravanTradeService`.

## Why This Exists

Players observed caravans orbiting a dense town cluster and appearing to trade a single good. Research into the decompiled v1.4.6 `CaravansCampaignBehavior` (2248 lines, all decision logic in **private** methods — `AiVisitSettlementBehavior` `return`s on `IsCaravan`, so it is irrelevant) found three root causes, and a fourth latent opportunity:

1. **Distance is a penalty, not a reward.** `GetTradeScoreForTown` multiplies expected profit by `1/days` (land) plus an escalating `veryFarAddition`, and `distanceCut` hard-rejects towns past ~5× the average nearest-two-town distance. The closest town almost always wins the argmax → the shuttle.
2. **Perpetual war collapses the reachable set.** `CanTradeWith` excludes any town whose faction the caravan is at war with. In TAOM's endless war this leaves only friendly, clustered towns → forces the ping-pong. (This one method feeds both the destination filter and the mid-route abandon.)
3. **"One item" is budget-gated, not a hard cap.** `BuyGoods` attempts the top-5 (land)/top-10 (naval) categories, but `BuyCategory` skips any category whose buy-value `< 7f`, and buy-value scales with `budgetFactor = 0.1 + clamp(PartyTradeGold/5000, 0, 1)`. A poor caravan sits at `budgetFactor ≈ 0.1` → only the single best category clears the gate → buys one thing.
4. **"Further = more money" already exists, latent.** Prices are pure local supply/demand (`DefaultTradeItemPriceFactorModel`, up to 10× base at undersupplied towns) with zero distance term. Distant towns are *already* more profitable — vanilla just vetoes reaching them. So the fix is to **lift the vetoes and re-weight selection**, not fabricate gold.

## Architecture

Mirrors the `ArmyTargeting` precedent (which solves the identical "AI thrashes between close targets + distance-decays away far ones" for besieger armies): thin Harmony postfixes → pure `ICaravanTradeService` → validating config provider + MCM-over-JSON settings. Every service method short-circuits to the vanilla value when the master toggle is off (or when it's a player caravan and `ApplyToPlayerCaravans` is off), so **master-off restores exact vanilla behavior**. No new GameModel file — the diversity overrides live on the already-owned `TaomCaravanModel`.

### The four levers

| # | Lever | Engine seam (all private) | Mechanism |
|---|-------|---------------------------|-----------|
| 1 | **War gate** (highest impact) | `CanTradeWith(IFaction, IFaction)` postfix (`ref bool __result`) | Flips a war-caused `false → true` per `WarTradePolicy`. Guards: only when `IsAtWarWith` (a peacetime false is the player's prohibited-kingdom exclusion — respected); the player's `_prohibitedKingdomsForPlayerCaravans` list is honored even during war (cached reflection). Policy resolves via `IAlignmentService.GetKingdomSide`, falling back to `GetCultureSide` when the kingdom isn't classified (player-founded kingdoms). Scoped at the player's **faction** level (matches vanilla's own player-caravan marker in this method). |
| 2 | **Range re-weight + recency penalty** | `GetTradeScoreForTown(...)` postfix (`ref float __result`) + `CaravanVisitMemoryBehavior` | Recomputes raw travel days from the same public inputs vanilla used (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` + caravan-speed props), strips vanilla's `1/days` spike and re-applies `1/(nearFieldFlatten+days)^decayExponent` clamped by `maxCompensation`; then multiplies by a per-caravan **recency penalty** (from `ICaravanVisitMemory`) that deprioritizes the last few towns visited so caravans circulate. The home town is compressed like any other (`homeDistanceReweight`, default on — fixes the home rubber-band) while vanilla's upstream home-gravity (`num5`) is preserved. Selection-only; profit + payout untouched. Naval passes through. Scoped per **clan** (`Owner.Clan == PlayerClan`). |
| 3 | **Range envelope** | `GetDistanceLimitVeryFarAsDaysForNavigationType(bool)` postfix (`ref float __result`) | Scales the vanilla "very far" ceiling by `RangeMultiplier` on each **read** (the single read-point — the Close/Med/Far bands + the `distanceCut` veto all derive from it). Reads the master toggle live, so master-off reverts instantly. **Engine-global** — the getter has no per-caravan context, so this lever cannot be player-scoped. (Earlier the write-once `CacheVeryFarDistances` cache was scaled, but that left the ceiling scaled after a mid-session master-off — Codex 2026-07-04 MED.) |
| 4 | **Basket diversity** | `CalculateBudgetFactor(MobileParty)` postfix + `TaomCaravanModel` overrides | Floors the vanilla `budgetFactor` to `BudgetFactorFloor` so even poor caravans clear the `< 7f` gate on several categories. `TaomCaravanModel.GetInitialTradeGold` raises the starting-gold floor (never lowers vanilla's large/main-hero bonus); `GetMaxGoldToSpendOnOneItemCategory` is exposed for tuning (default = vanilla). |

### "Further = more money" — emergent, not injected

Levers 1–3 let caravans reach the undersupplied far / same-alignment towns vanilla already prices up to 10× — real market arbitrage, which flows to the owner through the existing `ClanFinance` 10%-of-surplus daily drip. **No `TaomClanFinanceModel` change, no injected gold, no `SyncData`.** The feature is fully **save-clean**: toggles apply to existing saves immediately, and master-off leaves no residue.

### Data flow

`caravan_trade_config.json` → `CaravanTradeConfigProvider` (validate-and-fall-back) → `CaravanTradeSettingsProvider` (MCM-over-JSON merge) → `CaravanTradeService` (pure decisions) ← the 4 hooks + `TaomCaravanModel`. For the recency lever, `CaravanVisitMemoryBehavior` records town entries into the singleton `ICaravanVisitMemory`, and the `GetTradeScoreForTown` hook reads the recency penalty from it and passes it into `CaravanTradeService.ReweightTradeScore` (so the `IsActiveFor` player-scope gate governs it). War policy additionally consults `IAlignmentService` (Execution feature) — resolving `GetKingdomSide` directly and branching on `FactionSide.Neutral`, **not** `AreEnemyAlignments` (whose Neutral-as-enemy-of-everyone semantics are inverted for this purpose — see RCA below).

## Configuration

`Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json` (singleton-cached — edits need an app restart). Validated field-by-field; invalid values revert to the shipped default with a logged warning. Validation covers finite-float checks (`FiniteFloatValidator`), ordering constraints between related fields, and the `warTradePolicy` known-string set. MCM group **"Caravan Trade"** exposes the headline knobs (which override the matching JSON fields at runtime); the curve internals stay JSON-only.

| Field | Default | Range | MCM? | Meaning |
|-------|---------|-------|------|---------|
| `enabled` | `true` | — | ✅ master | Off = exact vanilla. |
| `applyToPlayerCaravans` | `true` | — | ✅ | Scope all levers off player caravans when false. |
| `rangeMultiplier` | `1.6` | [1, 4] | ✅ | Scale of the vanilla "very far" ceiling. |
| `distanceDecayExponent` | `0.5` | [0.25, 4] | JSON | Curve alpha; lower = ranges further. |
| `nearFieldFlattenDays` | `2.0` | [0, 20] | JSON | Ties near towns so profit decides. |
| `maxCompensation` | `6.0` | [1, 20] | JSON | Clamp so one far town can't pull caravans map-wide. |
| `antiShuttlePenalty` | `0.5` | [0, 1] | JSON | Recency penalty strength: max score cut on the most-recently-visited town, decaying over the caravan's last 4 visited towns. Raise toward 0.6–0.7 if shuttling persists. |
| `homeDistanceReweight` | `true` | — | JSON | `true` = distance-compress the home town like any other (fixes the home rubber-band); `false` = restore the old home distance exemption if caravans return home too rarely. Home-gravity preserved either way. |
| `warTradePolicy` | `SameAlignmentAndNeutral` | enum | ✅ dropdown | `None` (vanilla) / `IgnoreWar` / `SameAlignmentAndNeutral`. |
| `budgetFactorFloor` | `0.35` | [0, 1] | ✅ | Fuller baskets for poor caravans. |
| `initialTradeGold` | `15000` | [1000, 100000] | JSON | Starting-gold floor. |
| `maxGoldPerCategory` | `1500` | [100, 20000] | JSON | Per-category gold cap (default = vanilla). |

**War policy default (`SameAlignmentAndNeutral`):** a Free caravan trades at any Free or Neutral town despite the war, but not Evil towns (and vice-versa); Neutral factions (Umbar, etc.) trade with anyone. Lore-coherent and still hugely widens the reachable set.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CaravanTrade/ICaravanTradeService.cs` | Pure decision surface + `WarTradePolicy` enum. |
| `Main/Features/CaravanTrade/CaravanTradeService.cs` | All logic (reweight, recency, war policy, budget floor, gold resolution). TaleWorlds-free. |
| `Main/Features/CaravanTrade/ICaravanVisitMemory.cs` / `CaravanVisitMemory.cs` | Pure per-caravan ring of the last 4 visited towns → recency penalty factor (string-keyed, ADR-007). |
| `Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs` | Thin `CampaignBehaviorBase` — records town entries (`SettlementEntered`), evicts on `MobilePartyDestroyed`. No `SyncData`. |
| `Main/Features/CaravanTrade/CaravanTradeConfig.cs` | JSON DTO + `WarTradePolicyParser` (known-set validation). |
| `Main/Features/CaravanTrade/CaravanTradeConfigProvider.cs` | Load + field-by-field validation. |
| `Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs` | MCM-over-JSON merge; dropdown-index → enum. |
| `Main/Features/CaravanTrade/CaravanTradeIoC.cs` | 3 singleton registrations. |
| `Main/Features/CaravanTrade/Hooks/*.cs` | The 4 postfixes (Patch59_CaravanTrade). |
| `Main/Features/CulturalFeats/Models/TaomCaravanModel.cs` | +2 diversity overrides (existing forming-cost override kept). |
| `Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json` | Config + inline docs. |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` (`Patch59_CaravanTrade` in the campaign-phase block + the `TaomCaravanModel` ctor injection), `Main/Features/TaomSettings.cs` (MCM group).

## Dependencies

- **Execution feature** — `IAlignmentService` + `execution/alignment.json` for the war-policy side resolution.
- **CulturalFeats** — owns `TaomCaravanModel` (single GameModel owner for `DefaultCaravanModel`).
- **MCM** (`TaomSettings`), **DryIoc**, `TAOM.Core.Validation.FiniteFloatValidator`, `TAOM.Core.Logging`, `TAOM.Core.Infrastructure.IPathService`.

## Tests

- `TAOM.Tests/Features/CaravanTrade/CaravanTradeServiceTests.cs` — every lever + the war-policy matrix (same-side / opposite-side / **Neutral-on-each-side** regression) + NaN/disabled/player-scope gates + the home-compression regression (`ReweightTradeScore_HomeTown_NowCompressed`) + recency-factor + NaN-factor gates.
- `TAOM.Tests/Features/CaravanTrade/CaravanVisitMemoryTests.cs` — recency decay, ring bounding, most-recent-rank, **`GetRecencyPenaltyFactor_PreviousTown_IsPenalized`** (the inert-penalty regression sentinel), `NeverReturnsZero_NoStranding`, NaN/zero/out-of-range strength gates, `Clear`.
- `TAOM.Tests/Features/CaravanTrade/CaravanTradeConfigProviderTests.cs` — one test per validation rule, incl. the `warTradePolicy` M1 typo-trap.
- `TAOM.Tests/Features/CaravanTrade/CaravanTradeBindingTests.cs` — `[BindingVerification]` drift-guards for the 4 private methods, the 2 `FieldRef` targets, the `AiHelper` helper, and the `DefaultCaravanModel` override targets (all pass against installed v1.4.6). The 4 postfixes also auto-enroll in `HarmonyPatchBindingTests`.

## How-To

- **Retune ranging:** lower `distanceDecayExponent` or raise `rangeMultiplier` (MCM) for more aggressive spreading; raise `nearFieldFlattenDays` to make profit dominate more among near towns.
- **Change war behavior:** MCM "War Trade Policy" dropdown, or the JSON `warTradePolicy` string.
- **Fuller/leaner baskets:** raise/lower `budgetFactorFloor` (MCM) and `initialTradeGold` (JSON).
- **Revert to vanilla:** MCM master toggle off — exact vanilla, existing saves included.

## Performance

All 4 hooks lazy-cache their `IoC.Resolve` (`??=`); the `CanTradeWith` hook lazy-caches the prohibited-kingdoms `FieldInfo`. `GetTradeScoreForTown` runs in the destination argmax loop (per caravan, on re-think — infrequent, not per-frame) and recomputes the distance via `AiHelper`; this was reviewed twice (deep-review + Codex) and **verified cache-backed** — `AiHelper` → `DistanceHelper` → `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` serves from the precomputed settlement distance cache (`_navigationCache.GetSettlementToSettlementDistanceWithLandRatio`) plus a couple of `Vec2.Distance` ops, not a live navmesh pathfind — so the recompute is cheap and terrain-accurate (a straight-line proxy was rejected because it would ignore the LOTR map's mountains/water). The range-envelope lever is a `ref float` postfix on `GetDistanceLimitVeryFarAsDaysForNavigationType` (a cheap float multiply per read; called a few times per score evaluation).

## Known limitations / playtest items

- **Player-scope is not uniform across the four levers** (Codex 2026-07-04). The engine seams have different context, so `ApplyToPlayerCaravans` scopes at different granularities: the **re-weight + basket-diversity** levers scope per **clan** (correct "your caravans"); the **war gate** scopes per **faction** (all caravans in your kingdom — matches vanilla's own player-caravan marker in `CanTradeWith`, which has no owner context); the **range envelope** is **engine-global** (the ceiling getter has no caravan context at all). In practice, with the toggle off your caravans still route by vanilla's nearest-first selection (the re-weight is off), so the global ceiling rarely changes their behavior; documented in the MCM hint.
- **Home rubber-band — FIXED (2026-07-11).** The original home exemption kept the home town's full `1/days` near-field spike while non-home towns were compressed, so a caravan homed at a hub (e.g. Minas Tirith) re-selected home the moment it parked at any neighbor — "leaves and immediately returns." Two root causes: (1) the old anti-shuttle penalty was **inert** — it keyed on `LastVisitedSettlement`, which equals the parked/current town at decision time (that town is already excluded by vanilla), so it never fired on a selectable town; (2) the home distance exemption. Fix: a per-caravan **recency memory** (`ICaravanVisitMemory`) penalizes the genuinely-previous towns, and the home town is now distance-compressed like any other (`homeDistanceReweight`, default on). Vanilla's upstream home-gravity (`num5`) is preserved, and caravan income is paid to the owner wherever the caravan is (verified: `DefaultClanFinanceModel.AddIncomeFromParty` is not home-gated), so payouts are unaffected. Escape hatch: set `homeDistanceReweight=false` if playtest shows home visits are too rare. Known residual: the recency memory enlarges the loop to ~5 distinct towns rather than guaranteeing map-wide circulation (tunable via `antiShuttlePenalty`).
- **Naval caravans unchanged:** the shuttle is a land problem; naval caravans pass through vanilla (naval travel is parked in TAOM anyway, #296).
- **Category-count cap:** the vanilla top-5/top-10 category *breadth* cap is unchanged in v1; the budget-floor + initial-gold levers make more of those slots fill, which is the primary "one item" fix. Raising the count itself would need a `BuyGoods` transpiler (deferred).

## References

- Deep-review RCA: `docs/reviews/rca-caravan-trade-2026-07-04.md` (HIGH war-gate Neutral-inversion caught + fixed).
- Codex adversarial review (2026-07-04): 4 MED findings, all fixed.
- GitHub issue: #329.
- Engine background: `docs/reference/engine/settlement-economy-food-prosperity.md` §Caravans.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
