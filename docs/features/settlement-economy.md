# Settlement Economy (town market-gold regeneration)

## Overview

Tunable override of the engine's daily town market-gold regeneration so drained town markets
recover. `TaomSettlementEconomyModel : DefaultSettlementEconomyModel` overrides ONLY
`GetTownGoldChange`, exposing the three constants of the vanilla formula as validated JSON knobs
with an MCM master toggle. Shipped defaults buff the base target (25000 vs vanilla 10000); slope
and rate stay vanilla. Issue #317.

## Why This Exists

Users reported towns quickly run out of gold and never recover — the market has no money to buy
player loot. Verified root cause (no single bug — an equilibrium mismatch):

- The engine regenerates town gold daily toward `10000 + Prosperity×12` at 25% of the deficit/day
  (`DefaultSettlementEconomyModel.GetTownGoldChange`, v1.4.6 verified).
- TAOM's drains run ~2× vanilla: LOTRLOME loot computes to ~2.2× vanilla item values (no explicit
  `value=`; the engine's `2.75^tier` exponential on high armor stats — #318), constant large
  battles mean player + AI lords (`PartiesSellLootCampaignBehavior`) dump that loot into towns,
  and TAOM towns average 2.78 bound villages vs 2.27 vanilla (+22% villager deliveries —
  `SellGoodsForTradeAction` legally spends a town to ~0, capped only by `town.Gold/price`).
- In-play prosperity decay (war, raids) shrinks both the regen target and the prosperity-scaled
  resident-consumption inflow, so broke towns stay broke.

Refuted suspects, for the record: garrison wages are a **clan** expense
(`DefaultClanFinanceModel.AddPartyExpense`) and never touch `Town.Gold`; CultureMarketplace
injection (`AddToCounts`) moves no gold.

Why base-heavy defaults: a prosperity-collapsed town's equilibrium goes 10k→25k and its
regen-at-zero 2,500→6,250/day (the fix targets exactly the broke towns), while a median TAOM town
(P≈3500) gains only ~29%. Raising `perProsperity` instead would reward prosperity the broke towns
lack and over-inflate rich towns; raising `regenRate` changes only the convergence speed and the
loot-farm extraction ceiling, not the equilibrium. Adversarial review confirmed no runaway loop:
every town-gold drain is bounded by physical goods value, the formula is self-damping above its
target, and prosperity never reads gold. Accepted side effects: AI lords and landed clans get
richer from restored trade flow (loot sales complete; village tax at 100% commission flows).

## Architecture

```
ItemConsumptionBehavior.UpdateTownGold (engine, daily tick)
        │ calls
TaomSettlementEconomyModel.GetTownGoldChange     ← thin; Enabled gate + primitives at boundary
        │ delegates (prosperity, gold, config)
SettlementEconomyService.ComputeTownGoldChange   ← pure math, TaleWorlds-free, 100% tested
        │ reads
SettlementEconomyConfig ← SettlementEconomyConfigProvider ← settlement_economy_config.json
```

- **Toggle off / null town ⇒ `base.GetTownGoldChange(town)` passthrough** — vanilla math, and
  drift-safe if a future engine version changes the formula.
- **Rounding parity**: the service uses `(int)Math.Round(rate × deficit)` with float arithmetic —
  identical to TaleWorlds `MathF.Round` (banker's rounding via double promotion), pinned by a
  midpoint test.
- **Castles never reach this override.** The sole engine caller is
  `ItemConsumptionBehavior.UpdateTownGold` (v1.4.6 :73-77), reached only via `DailyTickTownEvent`,
  which iterates `Town.AllTowns` (towns only; castles live in `Town.AllCastles` and are never
  ticked here — chain: `CampaignPeriodicEventManager.cs:238` → `Town.cs:294-296`). No castle gate
  is written (dead code); **re-verify this chain on the next `/engine-bump`.**
- **Save compatibility**: `Town.Gold` is `[SaveableProperty(50)]`; models are never serialized.
  The fix applies to EXISTING saves immediately — gold converges to the new equilibrium at
  `(1−rate)^t` (~90% in 8 days at 0.25). Toggling mid-campaign is safe in both directions.

## Configuration

`Main/_Module/ModuleData/settlement_economy/settlement_economy_config.json` — **shipped values
(NOT vanilla; #317 decision)**:

| Knob | Shipped | Vanilla | Valid range | Meaning |
|------|---------|---------|-------------|---------|
| `townGoldBase` | **25000** | 10000 | [0, 200000] | Flat term of the equilibrium target |
| `townGoldPerProsperity` | 12 | 12 | [0, 100] | Target gold per prosperity point |
| `townGoldRegenRate` | 0.25 | 0.25 | [0, 1] | Fraction of deficit recovered/day (0 = freeze) |

Equilibrium target = `base + prosperity × perProsperity`; the daily change is
`rate × (target − currentGold)` — negative above the target (mean-reversion, intentionally
unclamped). Validation: NaN/Infinity/out-of-range revert to the **shipped** default with a
warning + summary warning (`FiniteFloatValidator`). Provider is `Reuse.Singleton` — **config
edits require a full app restart**.

MCM: **Settlement Economy → Enable Settlement Economy Tuning** (on by default; off = vanilla
engine gold math via base passthrough).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SettlementEconomy/Models/TaomSettlementEconomyModel.cs` | Thin GameModel override (single method) |
| `Main/Features/SettlementEconomy/SettlementEconomyService.cs` | Pure regen math (banker's-rounding parity) |
| `Main/Features/SettlementEconomy/SettlementEconomyConfig.cs` | Knob POCO; defaults = shipped values |
| `Main/Features/SettlementEconomy/SettlementEconomyConfigProvider.cs` | Lazy-singleton JSON load + semantic validation |
| `Main/Features/SettlementEconomy/SettlementEconomyIoC.cs` | DryIoc registrations |
| `Main/_Module/ModuleData/settlement_economy/settlement_economy_config.json` | Shipped knobs |
| `Main/SubModule.cs` (`RegisterCulturalFeatModels`) | `AddModel` registration |
| `Main/Features/TaomSettings.cs` | MCM master toggle |

## Data-side companions

- `tools/analyze_settlement_prosperity.py` — read-only report: TAOM_Map starting prosperity vs
  vanilla per class, flat-cluster flags (89 castles at exactly 600, 31 towns at 3500),
  gold-equilibrium columns. Reports to `tools/reports/settlement-prosperity/`.
- `tools/rebalance_settlement_prosperity.py` — lift-only per-class vanilla quantile map for the
  LIVE `TAOM_Map/ModuleData/settlements.xml` (`--dry-run` default, `--apply` with `.bak`,
  idempotent, byte-round-trip). Seeds NEW campaigns only; this C# feature covers live saves.
- Follow-ups: #318 (LOTRLOME item-value rebaseline — the drain root cause), #319
  (CultureMarketplace foreign-item filter resets the price-crash anti-farming guard).

## Tests

`TAOM.Tests/Features/SettlementEconomy/` — `SettlementEconomyServiceTests` (13: vanilla-formula
parity, mean-reversion above target, per-knob effects, zero-prosperity recovery floor, NaN/Inf
guards, banker's-rounding midpoints, shipped-defaults pin) + `SettlementEconomyConfigProviderTests`
(16: valid/missing/malformed/partial JSON, cache identity, one test per validation rule including
NaN/Infinity literals and the rate-0 accepted extreme).

## How-To

- **Towns still feel broke** → raise `townGoldBase` (e.g. 35000) or `townGoldRegenRate` (e.g.
  0.4; also raises the player loot-sale ceiling). Restart the app after editing.
- **Revert to vanilla** → toggle off in MCM (immediate), or set 10000/12/0.25 (restart).
- **Deliberate extremes** → validation accepts self-defeating-but-finite configs on purpose
  (`townGoldRegenRate: 0` freezes regen; `base: 0` + `perProsperity: 0` drains every town to 0) —
  the knobs are general tuning levers, not relief-only. If towns are broke and you edited the JSON,
  check you haven't zeroed the target. (Codex review 2026-07-02, P3 observation.)
- **Engine bump** → re-verify `GetTownGoldChange`'s formula and the `Town.AllTowns`-only caller
  chain via `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultSettlementEconomyModel`.

## See also

- [settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md) —
  the engine's full food/prosperity/gold loop, including the town-gold drain/inflow map.
- [settlement-food.md](settlement-food.md) — the donor pattern (thin model + pure service +
  validated config + MCM toggle).
- [settlement-building-levels.md](settlement-building-levels.md) — the sibling data-side pass on
  the same LIVE `settlements.xml` (per-fief starting building levels; same dump/apply/`.bak`/idempotent
  machinery as the prosperity tools above).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/reference/engine/settlement-economy-food-prosperity.md](../reference/engine/settlement-economy-food-prosperity.md)

<!-- backlinks-end -->
## Attributing the drain (#391)

This feature fixed the **regen** side: the mint recovers 25% of the deficit toward
`base + Prosperity×12` daily. It did not touch the drain, and field reports show towns still
pinned near zero — Minas Tirith at 173 denars with ~19,242/day owed.

`taom.print_town_ledger [town]` attributes where that money goes, by day and by flow
(`Patch68_EconomyDiagnostics`, read-only). Use it before changing any constant here: the
suspected culprit is villager deliveries, which spend `min(qty, town.Gold / price)` across a
villager's whole roster with **no reserve**. See
[economy-diagnostics.md](economy-diagnostics.md).
