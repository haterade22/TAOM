# Codex Adversarial Review: BattleBalance

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. Vanilla math checks out: `DefaultMilitaryPowerModel.GetDefaultTroopPower` is `(2+tier)*(10+tier)*0.02 * (hero ? 1.5 : mounted ? 1.2 : 1)`, so TAOM's T7/T10 stacking and T6 fallback are correct; `DefaultCombatSimulationModel.GetBluntDamageChance` is still `0.3/0.1`; `DefaultPartyHealingModel.GetSurvivalChance` returns a bounded probability and TAOM applies a second bounded transform. The blocking issue is config correctness: two configured cultural survival bonuses are keyed to culture IDs that do not exist in TAOM runtime data, so those bonuses never apply.

## Section 1: Vanilla Code

### DefaultMilitaryPowerModel.GetDefaultTroopPower (decompiled)

Formula: `(2+tier)*(10+tier)*0.02 * (hero ? 1.5 : mounted ? 1.2 : 1)`

### DefaultCombatSimulationModel.GetBluntDamageChance (decompiled)

Returns `0.3` for player battles, `0.1` for AI battles.

### DefaultPartyHealingModel.GetSurvivalChance (decompiled)

Returns a bounded probability based on medicine skill and battle conditions.

## Section 2: Power Calculation Verification

- **T7 troop (not hero, not mounted):** Vanilla formula `(2+7)*(10+7)*0.02 = 9*17*0.02 = 3.06`. TAOM returns custom config value for T7 with same hero/mounted multiplier stacking. Correct.
- **T10 mounted hero:** TAOM applies hero and mounted multipliers on top of the custom tier power. Stacking is consistent with vanilla's approach.
- **T6 troop (vanilla max tier):** TAOM falls through to `base.GetDefaultTroopPower()` for tiers within vanilla range. No regression.

## Section 3: Survival Bonus Validation

- **Culture ID cross-reference:** Most IDs match. Two do not — see findings below.
- **Negative bonuses:** Applied additively to the base survival chance. With vanilla base ~0.5 and Mordor penalty -0.2, result is 0.3 (clamped to [0,1] range). No risk of negative.
- **Gondor troop walkthrough:** Base ~0.5 + 0.3 bonus = 0.8 survival chance, additive application. Correctly bounded.

## Findings

### [HIGH] Rohan survival bonus keyed to non-existent culture ID — bonus never applies

**File:** `battle_balance_config.json:21`

**TAOM code:** `config.CasualtyRatios.GetCulturalSurvivalBonus(culture.StringId)` looks up the bonus with the runtime culture StringId.

**Config:** Key is `"rohan"` with bonus `0.2`.

**Evidence:** Rohan content uses `Culture.vlandia` throughout (`troops/troops_rohan.xml:16`, `:90`, `:173`). `taom_spcultures.xml` contains no `id="rohan"`. The runtime `culture.StringId` is `"vlandia"`, so the configured `0.2` bonus is unreachable for every Rohan party.

**Remediation:** Rename the key from `"rohan"` to `"vlandia"` in both `battle_balance_config.json` and `BattleBalanceConfig.cs` defaults. Add a test that validates bonus keys against real TAOM culture StringIds.

### [MEDIUM] Dol Guldur survival bonus also dead — config key doesn't match culture ID

**File:** `battle_balance_config.json:27`

**Config:** Key is `"dol_guldur"` with penalty `-0.1`.

**Evidence:** TAOM defines Dol Guldur as `id="dolguldur"` in `taom_spcultures.xml:2540`. Live content references `Culture.dolguldur` in `characters/clans.xml:1031`, `:1043`, `:1055`. The underscore variant `dol_guldur` never matches at runtime.

**Remediation:** Rename from `"dol_guldur"` to `"dolguldur"` in both JSON and defaults.

### [MEDIUM] Tests encode the same wrong IDs — false confidence on broken config

**File:** `TaomPartyHealingModelTests.cs:144-151`

**Evidence:** Tests assert `DataRow("rohan", 0.2f)` and `DataRow("dol_guldur", -0.1f)` instead of using real TAOM culture StringIds. The code path is `GetCulturalSurvivalBonus(culture.StringId)`, so tests ratify two unreachable config entries while staying green.

**Remediation:** Replace literal-key tests with assertions against actual culture IDs (`vlandia`, `dolguldur`), or cross-check configured keys against culture definitions so typos fail tests immediately.

## Observations

- Military power formula correctly extends vanilla for T7-T10 with config-driven values
- Combat simulation correctly separates player vs AI blunt/cut ratios
- T0-T6 troops fall through to vanilla via `base.GetDefaultTroopPower()` — no regression risk
- Survival bonus application is additive and correctly bounded to [0,1]

## Recommended Next Steps

1. Fix `"rohan"` -> `"vlandia"` and `"dol_guldur"` -> `"dolguldur"` in config and defaults
2. Update test DataRow values to match corrected culture IDs
3. Add a cross-validation test that checks all configured survival bonus keys exist in TAOM culture definitions
