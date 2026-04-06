# Codex Adversarial Review: RaceAge

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship: the checked-in RaceAge data and age-model wiring materially change human fertility/lifespan balance and leave several race-specific age fields ineffective in vanilla call paths.

## Section 1: Vanilla Code

### DefaultPregnancyModel.GetDailyChanceOfPregnancyForHero (decompiled)

Uses `(1.2 - (age - 18)*0.04) / (num*num) * 0.12 * num3` with hard-stop at age 45. Age decline is `0.04` per year from 18.

### DefaultAgeModel (decompiled)

- `MaxAge => 128`
- `BecomeOldAge => 47`
- `HeroComesOfAge => 18`

### DefaultHeroDeathProbabilityCalculationModel (decompiled)

Computes old-age mortality from `AgeModel.BecomeOldAge` to `AgeModel.MaxAge`. `AgingCampaignBehavior.IsItTimeOfDeath` only starts old-age death handling once `hero.Age >= AgeModel.BecomeOldAge`.

## Findings

### [HIGH] Checked-in human config makes humans fertile far past vanilla and boosts baseline pregnancy odds

**File:** `race_age_config.json:4`

**TAOM code:** `TaomPregnancyModel` stretches vanilla's age-decline curve across the full `[comesOfAge, fertilityEnd]` window (lines 36-50). With checked-in human config (`maxAge: 200`, `becomeOld: 150`, `fertilityEnd: 195`), a 30-year-old human with 2 children gets `ageFactor = 1.2 - 12*(1.08/177) = 1.1268`, so pre-perk chance is `1.1268 / 9 * 0.12 = 0.0150 * populationFactor`.

**Vanilla code:** At age 30 with 2 children: `(1.2 - (30-18)*0.04) / 9 * 0.12 = 0.72/9*0.12 = 0.0096 * populationFactor`. Hard-stops at 45.

**Evidence:** ~56% increase for ordinary humans. Config keeps fertility non-zero until age 195. Feature docs say humans are the 85/45 baseline. Tests hardcode 85/45. This looks like an accidental live-config regression.

**Remediation:** Either restore the human entry to documented 85/45-style baseline, or explicitly rebalance around 200/195 values and update docs/tests to match. Add a test that exercises the checked-in config file, not just synthetic JSON.

### [HIGH] Race-specific early adulthood is dead for conception — vanilla gates pregnancy at global HeroComesOfAge

**File:** `TaomAgeModel.cs:15-17`

**TAOM code:** Config advertises early reproduction for short-lived races (`orc comesOfAge=12`, `uruk=10`, `uruk_hai=8`, `berserker=6`). But `TaomAgeModel` only overrides `MaxAge` and `BecomeOldAge` (lines 15-17).

**Vanilla code:** `PregnancyCampaignBehavior.DailyTickHero` evaluates pregnancy only when `hero.Age > Campaign.Current.Models.AgeModel.HeroComesOfAge` (lines 92-94). `AgingCampaignBehavior` raises `OnHeroComesOfAge` from the same global threshold (lines 113-116).

**Evidence:** `HeroComesOfAge` remains default 18. Sub-18 `comesOfAge` entries are never reached by natural conception flow. Fast-breeding races cannot start breeding before 18.

**Remediation:** Make the hero-age gate race-aware where vanilla uses `AgeModel.HeroComesOfAge`, or drop sub-18 `comesOfAge` values from config if the engine will not honor them.

### [MEDIUM] becomeOld is effectively unused — global 5000 disables vanilla old-age state for every race

**File:** `TaomAgeModel.cs:15-17`

**TAOM code:** Race config carries per-race `becomeOld` values and `IRaceAgeService.GetBecomeOldAge()` exists, but no production code uses it. `TaomAgeModel` hardcodes `BecomeOldAge => 5000` and `MaxAge => 10000`.

**Vanilla code:** `DefaultHeroDeathProbabilityCalculationModel` computes mortality from `AgeModel.BecomeOldAge` to `AgeModel.MaxAge`. `AgingCampaignBehavior.IsItTimeOfDeath` starts old-age death at `hero.Age >= AgeModel.BecomeOldAge`. `OldTag` applies when `character.Age > AgeModel.BecomeOldAge`.

**Evidence:** With 5000/10000, no human/dwarf/orc hero will ever enter vanilla 'old' state or gradual old-age mortality. Only custom hard kill at max age remains. Per-race `becomeOld` config entries are dead.

**Remediation:** Either implement race-aware old-age handling for code paths that consult `AgeModel.BecomeOldAge`, or remove `becomeOld` from config so it doesn't imply behavior the runtime doesn't provide.

## Recommended Next Steps

1. Fix human config values to match documented baseline, or explicitly update docs/tests for new values
2. Make `HeroComesOfAge` race-aware or remove sub-18 `comesOfAge` from config
3. Implement per-race `becomeOld` handling or remove dead config
4. Add test that loads actual `race_age_config.json` and validates against docs
