# Codex Adversarial Review: ArmyTargeting

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** approve

No blocking no-ship case is supported by the current ArmyTargeting implementation.

## Section 1: Vanilla Behavior

### DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction

Vanilla besieger scoring feeds from: defender strength around the target, `ourStrength`, navigation distance (`num21`), settlement value, walls/food, nearby allied enemy parties, relation modifiers. Returns `0f` on the besieger strength gate (`if (ourStrength < num15 * num16) return 0f;`).

### AiMilitaryBehavior.CalculateDistanceScoreForBesieging

Vanilla computes: `bestDistanceScore = num2 / num + num4 / num3 * 0.25f; if (bestDistanceScore < 0.1f) bestDistanceScore = 0f;` — drops to zero when the attacker has effectively no friendly border presence near the target.

### base.GetTargetScoreForFaction() Interaction

TAOM's model calls `base.GetTargetScoreForFaction(...)` first with optionally inflated `ourStrength`, then only for positive besieger scores applies post-base multipliers. Non-priority factions fall through unchanged.

## Section 2: Math Verification

### a) Mordor besieging town_EW3 (priority position 0 of 4, committed)

- CommitmentMultiplier = 4.0
- MaxPriorityBoost = 3.0
- Position 0 of 4: `t = 0/3 = 0`, boost = `3.0 - (0) * (2.0) = 3.0`
- Total multiplier: `4.0 × 3.0 = 12.0×` target score
- Plus configured distance compensation `1.5×` for `empire_s` priority targets

### b) Mordor evaluating town_EW1 (position 2 of 4, no commitment)

- Position 2 of 4: `t = 2/3`
- Boost = `3.0 - (2/3) * (2.0) = 1.6667`
- No commitment multiplier applied
- Total multiplier: `1.6667×` target score

### c) Gondor evaluating a non-priority target

- Falls through to vanilla scoring unmodified (`1.0×`)

### d) Edge: CommitmentMultiplier=0 in MCM

- Would zero the committed target score
- However, shipped MCM setting range is `1.0-10.0`, so this edge does not exist through normal configuration

## Section 3: Config Validation

- All settlement IDs in `army_targeting.json` follow valid TAOM naming conventions with expected prefixes
- Custom faction StringIds (`isengard`, `gundabad`, `dolguldur`, `erebor`) match module XML culture definitions
- No duplicate or typo issues found in priority lists

## Section 4: Findings

**No material findings.**

## Recommended Next Steps

- Optional hardening only: add one integration-style regression test around the border-floor patch to prove a priority target with vanilla `bestDistanceScore == 0f` still becomes selectable via the 0.15 floor
