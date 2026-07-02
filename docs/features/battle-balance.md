# Battle Balance

## Overview

Three GameModel overrides that tune large-scale battle math: troop power values for tier-7 through tier-10 troops, blunt-vs-cut damage ratios per battle type (player vs AI-only), and per-culture casualty rates. All three are configurable through both an `Main/_Module/ModuleData/configs/battle_balance_config.json` file and the in-game MCM panel; MCM gates take precedence and the JSON values supply data-driven baselines.

## Why This Exists

- **Vanilla behavior:**
  - `DefaultMilitiaModel.GetDefaultTroopPower` uses the formula `(2 + tier) * (10 + tier) * 0.02` and never sees a tier above 6.
  - `DefaultCombatSimulationModel.GetBluntDamageChance` returns a fixed ~10% blunt chance regardless of who's fighting.
  - `DefaultPartyHealingModel.GetSurvivalChance` produces uniform survival chances across cultures.
- **TAOM requirement:** Tier 7-10 troops exist (Cultural Feats system, lots of elite LOTR units like Dúnedain Rangers, Royal Guard); their power values must be authored, not extrapolated. LOTR cultures should also have distinct casualty profiles — Mordor armies shrug off losses (or rather, players shrug off losing them), elite Eldar formations should rarely take real casualties — and the player should be able to dial wound-vs-kill ratios for their own battles separately from AI-vs-AI sim.
- **Without this feature:** T7+ troops fall back to a polynomial extrapolation that produces unintuitive AutoCalc results; cultural identity in losses is invisible; the player can't tune lethality without editing source.

## Architecture

### Design Challenge

Three independent vanilla GameModels each need overriding with different shapes of data — a tier-keyed dictionary, a binary player/AI switch, and a culture-keyed dictionary. The override classes need access to both the JSON config (defaults, comprehensive coverage) and the MCM settings (live override knobs). Constructor injection through DryIoc keeps each model thin and testable.

A fourth wrinkle: `TaomPartyHealingModel` integrates the [career system](career-system.md)'s `TroopRegeneration` passive — survival chance gets a multiplicative bonus when the party leader has the passive. This crosses feature boundaries deliberately (career passives are an opt-in modifier on top of the cultural baseline, not a replacement).

### Solution Approach

Standard GameModel override (see [.claude/rules/gamemodels.md](../../.claude/rules/gamemodels.md)) for each of the three vanilla models. Two providers fan in the data:

- [IBattleBalanceConfigProvider](../../Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs) — wraps `battle_balance_config.json` deserialization, single-pass cached, falls back to baked-in defaults if the file is missing or malformed.
- [IBattleBalanceSettingsProvider](../../Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs) — exposes MCM switches and float properties, falling back to compile-time defaults if `TaomSettings.Instance` is null.

Both providers register as `Reuse.Singleton`. The cache means JSON edits require a full Bannerlord process restart — see Configuration > Reload Scope below.

### Component Diagram

```
battle_balance_config.json
        |
BattleBalanceConfigProvider (Singleton, IPathService + IModLogger)
        |
        +--+
        |  |
        |  IBattleBalanceConfigProvider
        |  IBattleBalanceSettingsProvider  ← reads TaomSettings.Instance
        |  |
   +----+--+----+----------+
   |       |    |          |
   v       v    v          v
TaomMilitaryPowerModel       TaomCombatSimulationModel       TaomPartyHealingModel
  : DefaultMilitaryPowerModel  : DefaultCombatSimulationModel  : DefaultPartyHealingModel
  GetDefaultTroopPower         GetBluntDamageChance            GetSurvivalChance
                                                                    |
                                                                    +-> ICareerPassiveService.GetPassiveMagnitude(TroopRegeneration)
```

## Configuration

### Config file

[Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json)

```json
{
  "TroopPower": {
    "TierPower": {
      "T0": 0.40, "T1": 0.66, "T2": 0.96, "T3": 1.30,
      "T4": 1.68, "T5": 2.10, "T6": 2.56, "T7": 2.91,
      "T8": 3.26, "T9": 3.61, "T10": 3.96
    }
  },
  "CasualtyRatios": {
    "EnableCulturalSurvivalBonuses": true,
    "CulturalSurvivalBonuses": {
      "gondor": 0.3, "vlandia": 0.2, "lothlorien": 0.5,
      "erebor": 0.3, "rivendell": 0.4,
      "mordor": -0.2, "gundabad": -0.1, "dolguldur": -0.1
    }
  }
}
```

| Section | Field | Type | Effect |
|---|---|---|---|
| `TroopPower.TierPower` | `T0..T10` | float | Per-tier power; consulted when `OverrideVanillaTierPower=true` for ≤T6, always for ≥T7 |
| `CasualtyRatios.EnableCulturalSurvivalBonuses` | bool | Master kill switch on cultural bonus application |
| `CasualtyRatios.CulturalSurvivalBonuses` | `<cultureId>` | float | bonus > 0 reduces death chance; bonus < 0 increases it. Formula: `newDeathChance = vanillaDeathChance * (1 - bonus)` |

### MCM (TaomSettings)

| Group | Setting | Default | Effect |
|---|---|---|---|
| Troop Power | `EnableCustomTroopPower` | true | Master switch; off → fall through to vanilla |
| Troop Power | `OverrideVanillaTierPower` | false | If true, also use JSON values for T0-T6 (otherwise vanilla formula) |
| Troop Power | `Tier7Power` / `Tier8Power` / `Tier9Power` / `Tier10Power` | 2.91 / 3.26 / 3.61 / 3.96 | Live MCM overrides for the high tiers |
| Troop Power | `HeroMultiplier` | 1.5 | Multiplier applied to hero `GetDefaultTroopPower` result |
| Troop Power | `MountedMultiplier` | 1.2 | Multiplier applied to non-hero mounted troops |
| Casualty Ratios | `EnableCustomCasualtyRatios` | true | Master switch for blunt-chance overrides |
| Casualty Ratios | `PlayerBluntDamageChance` | 0.30 | Blunt chance during player-involved map events |
| Casualty Ratios | `AIBluntDamageChance` | 0.10 | Blunt chance during AI-only map events |
| Casualty Ratios | `EnableCulturalSurvivalBonuses` | true | Gates the culture-keyed survival adjustment |

### Reload scope

Both providers register `Reuse.Singleton` — the JSON file is cached for the entire Bannerlord process. **JSON edits require a full Bannerlord restart**, not a save-load and not a new campaign. MCM switches **do** apply live (each model reads `IBattleBalanceSettingsProvider` properties on every call, and the provider proxies to `TaomSettings.Instance` per access).

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs](../../Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs) | Override — tier power table + hero/mounted multipliers |
| [Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs](../../Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs) | Override — player vs AI blunt-damage chance |
| [Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs](../../Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs) | Override — cultural survival bonus + career passive integration |
| [Main/Features/BattleBalance/BattleBalanceConfig.cs](../../Main/Features/BattleBalance/BattleBalanceConfig.cs) | POCOs (`TroopPowerSection`, `CasualtyRatiosSection`) with default-valued dictionaries baked in |
| [Main/Features/BattleBalance/BattleBalanceConfigProvider.cs](../../Main/Features/BattleBalance/BattleBalanceConfigProvider.cs) | Loads + caches `battle_balance_config.json`; logs warnings on missing/malformed |
| [Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs](../../Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs) | MCM proxy with compile-time fallbacks |
| [Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs](../../Main/Features/BattleBalance/IBattleBalanceConfigProvider.cs) | Interface |
| [Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs](../../Main/Features/BattleBalance/IBattleBalanceSettingsProvider.cs) | Interface (12 properties) |
| [Main/Features/BattleBalance/BattleBalanceIoC.cs](../../Main/Features/BattleBalance/BattleBalanceIoC.cs) | Two singleton registrations |
| [Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json) | The JSON config file |
| [Main/SubModule.cs:291-295](../../Main/SubModule.cs) | `campaignStarter.AddModel(...)` for all three models |

## Dependencies

- `IBattleBalanceConfigProvider` (this feature)
- `IBattleBalanceSettingsProvider` (this feature)
- `IPathService` (Core/Infrastructure) — locates `Main/_Module/ModuleData/`
- `IModLogger` (Core/Logging)
- `ICareerPassiveService` (`CareerSystem`) — resolved lazily inside `TaomPartyHealingModel.GetSurvivalChance` via `IoC.Resolve` to avoid hard coupling at construction. Provides `TroopRegeneration` passive magnitude per hero.

## Tests

- [TAOM.Tests/Features/BattleBalance/TaomMilitaryPowerModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomMilitaryPowerModelTests.cs) — **10 tests**: T7-T10 configured values, T6 vanilla/override fallback, T11+ formula extension, low-tier override behavior.
- [TAOM.Tests/Features/BattleBalance/TaomCombatSimulationModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomCombatSimulationModelTests.cs) — **5 tests**: player vs AI blunt-chance routing, fallback to vanilla when `EnableCustomCasualtyRatios=false`.
- [TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs](../../TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs) — **13 tests**: cultural bonus zero / positive / negative, boundary clamping (0..1), career passive multiplier integration, null-safety paths.

The models themselves test the static helpers (`CalculateTierPower`, `CalculateBluntChance`, `ApplyCulturalSurvivalBonus`) — the `override`-method paths that touch `IoC.Resolve` and live game state are exercised in-game.

## How to Add a Cultural Survival Bonus for a New Culture

1. Edit [Main/_Module/ModuleData/configs/battle_balance_config.json](../../Main/_Module/ModuleData/configs/battle_balance_config.json).
2. Under `CasualtyRatios.CulturalSurvivalBonuses`, add `"<cultureId>": <bonus>` where `cultureId` matches the `<Culture id="...">` value in the relevant XML and `<bonus>` is a float in `[-1.0, +1.0]` (positive = better survival, negative = worse).
3. Restart Bannerlord (the provider caches for the whole process).
4. **Verify in-game:** Trigger or sim a battle for a party of that culture. Cultural cultures (`gondor`, `mordor`, etc.) should see materially different survival rates compared to a culture without an entry.

## How to Tune T7-T10 Power Live

Use the MCM panel: **TAOM → Troop Power → Tier7Power / Tier8Power / Tier9Power / Tier10Power**. Changes apply on the next `GetDefaultTroopPower` call, no restart needed. To make ≤T6 also use JSON values, flip MCM **OverrideVanillaTierPower** to `true`.

## Changelog

- 2026-05-13 — Phase 9b: `BattleBalanceConfigProvider` now validates per-key (TierPower T0-T10 finite + > 0, CulturalSurvivalBonuses finite + [-1, +1]); invalid values revert to compiled default with a warning (partial closes #140).
- 2026-05-07 — Feature doc `battle-balance.md` created (backfilled one of 5 missing feature docs flagged by `detect-docs-gaps.sh`).
- 2026-04-06 — Config key fixes (`rohan`→`vlandia`, `dol_guldur`→`dolguldur`) plus test DataRows, from the full-codebase adversarial review.
- 2026-03-31 — Fixed `TaomPartyHealingModel.GetSurvivalChance` NRE in arena practice by guarding the null `party` parameter (#52).

## GitHub Issue

- **Issue:** None — feature predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/troop-skill-balance.md](./troop-skill-balance.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
