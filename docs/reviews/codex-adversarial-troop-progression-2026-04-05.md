# Codex Adversarial Review: TroopProgression + TroopWeight

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. Two gameplay-facing wage/count intercepts diverge from vanilla semantics in ways that will misprice parties and misreport regular troop counts; the Rohan wage reduction is also computed against headcount instead of wage share.

## Section 1: Vanilla Code

*Note: Codex read decompiled sources from `E:\Decompiled_Bannerlord\` but output was truncated by the review format. Key references below.*

### DefaultPartyWageModel (decompiled)

- **GetCharacterWage:** Tier-based wage table for tiers 0-6
- **Garrison wage gate (line ~124):** `if (mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null)` before applying `EmpireGarrisonWageFeat` and building reductions (lines 143-147)
- **Mounted wage accumulation (line ~88):** `num3 += num9` accumulates mounted wage subtotal; `troopRatio3 = (float)num3 / bonuses2.BaseNumber` used for mounted wage adjustment (lines 135-136)

### PartyBase (decompiled)

- **NumberOfRegularMembers (line ~377):** Returns exact `MemberRoster.TotalRegulars` — a simple count with no weighting

## Section 2: Vanilla Analysis

- Vanilla `GetCharacterWage` uses a tier lookup table for tiers 0-6. TAOM extends to T0-T10 via `TroopCostService`.
- Vanilla `NumberOfAllMembers` is a plain roster count. TroopWeight postfix replaces this with a weighted sum.
- Vanilla `NumberOfRegularMembers` is exact (`TotalRegulars`). TroopWeight replaces with a wounded-ratio approximation.

## Findings

### [HIGH] Garrison wage feats applied to any party in a settlement, not just garrisons

**File:** `TaomPartyWageModel.cs:39-46`

**TAOM code:** `TaomPartyWageModel.GetTotalWage` applies Erebor/Lothlorien/Isengard/Gondor garrison feats whenever `mobileParty.CurrentSettlement?.Owner?.Culture` is non-null.

**Vanilla code:** `DefaultPartyWageModel` gates garrison wage modifiers behind `if (mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null)` (decompiled line ~124) before applying `EmpireGarrisonWageFeat` and building reductions (lines 143-147).

**Evidence of divergence:** A field army merely parked in a town/castle gets the garrison discount in TAOM, which changes wage-limit checks and AI/player upkeep outside the actual garrison case.

**Remediation:** Match vanilla's gate: only apply cultural garrison reductions when `mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null`.

### [HIGH] NumberOfRegularMembers replaced with wounded-ratio estimate instead of exact weighted count

**File:** `PartyBaseNumberOfRegularMembersHook.cs:35-45`

**TAOM code:** The hook recomputes from weighted total and a global wounded ratio: `weightedWounded = (int)(weightedTotal * woundedRatio)` and `weightedResult = ceil(weightedTotal) - weightedWounded`.

**Vanilla code:** `PartyBase.NumberOfRegularMembers` is exact: `MemberRoster.TotalRegulars` (PartyBase.cs line ~377).

**Evidence of divergence:** This only works if wounded troops have the same average weight as healthy troops. Counterexample: 50 healthy weight-1 troops plus 5 wounded cave trolls at weight 4 gives weighted total 70, wounded ratio 5/55, and TAOM returns 64 regulars even though the true weighted healthy count is 50. Consumers like `PartiesBuyHorseCampaignBehavior` and `GarrisonTroopsCampaignBehavior` read this property, distorting horse purchasing, garrison math, and AI decisions.

**Remediation:** Compute weighted healthy regulars directly from roster elements, subtracting wounded counts per troop before applying troop weight, instead of deriving them from the all-members total and wounded ratio.

### [MEDIUM] Rohan mounted wage reduction scaled by troop headcount, not wage share

**File:** `TaomPartyWageModel.cs:62-75`

**TAOM code:** Counts mounted bodies and applies `EffectBonus * (mountedCount / totalCount)`.

**Vanilla code:** Partial wage modifiers scale off wage share: `num3 += num9` accumulates mounted wage subtotal in `DefaultPartyWageModel` (line ~88), then `troopRatio3 = (float)num3 / bonuses2.BaseNumber` is used for the mounted wage adjustment (lines 135-136).

**Evidence of divergence:** Because mounted troops have higher wages (from the mounted multiplier and often from higher tiers), mixed Rohan parties get the wrong discount magnitude whenever mounted troops are costlier than foot troops.

**Remediation:** Base the Rohan reduction on mounted wage share of the roster's base wage total, not on mounted headcount share.

## Observations

- Sampled hardcoded volunteer troop IDs and settlement IDs were present in XML files
- Custom recruitment pools currently cover only a small subset of TAOM cultures; most cultures still fall back to vanilla/basic recruitment behavior
- The garrison gate bug and Rohan mounted-share math are the two places most likely to regress CulturalFeats again after fixes

## Recommended Next Steps

1. Fix garrison wage gate to match vanilla's `mobileParty.IsGarrison` check
2. Rework `NumberOfRegularMembers` to weight healthy regulars exactly — test against mixed healthy/wounded rosters with asymmetric weights (e.g., wounded trolls + healthy infantry)
3. Fix Rohan mounted wage to use wage share instead of headcount share
4. Re-run wage walkthroughs after fixes
