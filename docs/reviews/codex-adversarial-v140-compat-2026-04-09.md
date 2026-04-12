# Codex Adversarial Review: Bannerlord 1.4.0 API Migration Fixes

**Date:** 2026-04-09
**Reviewer:** Codex (via codex-rescue)
**Scope:** 4 modified C# files (signature-only migration fixes)

## Summary

| Severity | Count |
|----------|-------|
| CRITICAL | 0 |
| HIGH | 0 |
| MEDIUM | 0 |
| LOW | 1 |
| OBSERVATION | 2 |

**Verdict:** Migration fixes are correct. All signatures verified against decompiled v1.4.0 source. One LOW follow-up identified for future work.

## Known Suspects Verdicts

### Suspect 1: TaomAllianceModel lost IFaction param
**DISPUTED** -- v1.4.0 `DefaultAllianceModel.GetScoreOfStartingAlliance` completely rewrote scoring logic around threat assessment, marriage ties, trade agreements, and neighbor proximity. The old IFaction parameter was used to differentiate "who is evaluating this alliance" but v1.4.0 removed that concept entirely. Scoring is now purely between the two kingdoms. TAOM's lore modifier keys off `kingdomDeclaresAlliance.StringId` and `kingdomDeclaredAlliance.StringId` which are still the first two parameters. No behavioral impact.

### Suspect 2: renownMultiplierForWinnerSide unused by TAOM
**DISPUTED** -- The base `DefaultBattleRewardModel.CalculateRenownGain` now seeds the `ExplainedNumber` with `contributionShareOfWinnerParty * renownValueOfBattleForWinnerSide * renownMultiplierForWinnerSide`. The multiplier is baked into the base number before TAOM's `AddFactor` (Umbar feat) and `CareerPassiveHelper.ApplyFactor` layer on top. This is correct -- TAOM's cultural/career modifiers stack multiplicatively on the already-multiplied base, which is the intended behavior.

### Suspect 3: Other BattleRewardModel methods changed
**DISPUTED** -- `TaomBattleRewardModel` only overrides `CalculateRenownGain`. It does not override `CalculateInfluenceGain`, `CalculateMoraleGainVictory`, `GetLootMemberChancesForWinnerParties`, or `CalculateMoraleChangeOnRoundVictory`. Confirmed via file read -- the class is 36 lines with one override. No silent non-override problem.

### Suspect 4: Base peace logic rewrite
**DISPUTED as migration bug.** The base `IsPeaceDecisionAllowedBetweenKingdoms` now checks `IsAtWarByCallToWarAgreement` in both directions and blocks peace if a call-to-war agreement is active. TAOM's WotR gate runs first -- if `_wotrService.ShouldBlockPeace` returns true, the base is never reached. If WotR allows peace, the base runs with its new logic. This means the base CAN additionally block peace for call-to-war reasons even when WotR doesn't care. This is additive vanilla behavior, not a TAOM bug. The v1.4.0 comment in the file accurately describes this.

**CONFIRMED as design observation:** In a scenario where two WotR-neutral kingdoms have a call-to-war agreement, the base will block peace. This is vanilla-correct behavior that TAOM inherits. Not a bug, but worth documenting.

### Suspect 5: HideoutBattleEndState unused
**DISPUTED** -- `BattleSideEnum winnerSide == Attacker` already excludes retreat, defeat, and draw scenarios. The `HideoutBattleEndState` enum provides finer granularity (e.g., distinguishing "boss killed" from "all cleared") but TAOM's resource award logic doesn't need that distinction. Rewarding on any attacker victory is correct.

### Suspect 6: New AllianceModel virtual methods
**CONFIRMED as LOW follow-up.** `DefaultAllianceModel.CanMakeAlliance` is now consulted by `StartAllianceDecision.CanMakeDecision` in v1.4.0. TAOM does not override `CanMakeAlliance`, meaning racial enmity hard blocks (e.g., Mordor + Gondor should never ally) are enforced only via negative lore score modifiers, not via a hard gate. If the lore modifier is insufficient to overcome vanilla scoring factors (e.g., massive shared threat), an enmity alliance could theoretically form.

**Recommendation:** In a future session, consider overriding `CanMakeAlliance` to return `false` with a lore explanation for permanently hostile faction pairs. This would complement the existing score modifier with a hard permission gate.

## Signature Verification

| Override | Params Match | Return Match | Base Call Correct |
|----------|-------------|-------------|-------------------|
| `TaomAllianceModel.GetScoreOfStartingAlliance` | Yes (4 params) | Yes (`ExplainedNumber`) | Yes (all 4 passed) |
| `TaomAllianceModel.MaxNumberOfAlliances` | N/A (property) | Yes (`int`) | N/A |
| `TaomAllianceModel.MaxDurationOfAlliance` | N/A (property) | Yes (`CampaignTime`) | N/A |
| `TaomBattleRewardModel.CalculateRenownGain` | Yes (5 params) | Yes (`ExplainedNumber`) | Yes (all 5 passed) |
| `TaomKingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms` | Yes (3 params) | Yes (`bool`) | No base call (correct) |
| `TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` | Yes (3 params) | Yes (`bool`) | Yes (all 3 passed) |
| `TaomKingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms` | Yes (3 params) | Yes (`bool`) | Yes (all 3 passed) |
| `SpecialResourcesBehavior.OnHideoutCompleted` (event handler) | Yes (3 params) | N/A (void) | N/A |

**All 8 signatures verified correct.**

## Config Cross-Reference

- `TaomAllianceModel` passes `kingdomDeclaresAlliance.StringId` and `kingdomDeclaredAlliance.StringId` to `GetAllianceScoreModifier`. These are runtime Kingdom StringIds (e.g., `empire_w`, `mordor`) -- correct per cheatsheet.
- `TaomBattleRewardModel` checks `TaomCulturalFeats.UmbarRenownFeat` -- Umbar is culture ID `umbar` per cheatsheet. Correct.

## Findings

### LOW: CanMakeAlliance not overridden for racial enmity hard blocks
- **File:** `Main/Features/Diplomacy/Models/TaomAllianceModel.cs`
- **Issue:** v1.4.0 added `CanMakeAlliance(Kingdom, Kingdom, IFaction, out TextObject, bool)` as a hard permission gate. TAOM only modifies scores, not permissions.
- **Impact:** Theoretical -- if vanilla scoring factors overwhelm the negative lore modifier, hostile factions could ally.
- **Fix:** Future feature -- override `CanMakeAlliance` to enforce hard blocks for permanently hostile pairs.

### OBSERVATION: v1.4.0 base peace logic is additive
- The comment at line 44-50 of `TaomKingdomDecisionPermissionModel.cs` correctly documents the interaction. No action needed.

### OBSERVATION: HideoutBattleEndState provides unused granularity
- Could be used in future to distinguish boss-kill rewards from full-clear rewards. Not needed for current resource system.
