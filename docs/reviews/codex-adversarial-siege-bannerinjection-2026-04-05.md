# Codex Adversarial Review: Siege + BannerInjection

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship: the banner edit exclusion only protects the player clan, not the player kingdom, so ruler banner edits can be overwritten on the next inject pass; the BesiegerCamp fallback also discards vanilla spacing and can collapse broken-scene sieges onto one gate coordinate.

## Section 1: Vanilla Code

### BesiegerCamp.GetSiegeCampPartyPosition (decompiled)

Repeatedly samples camp frames and rejects candidates when `(campaignVec - item.MobileParty.VisualPosition2DWithoutError).LengthSquared < 0.25f`. If camp1 fails 20 times, retries against camp2 with the same spacing check.

### GauntletBannerEditorScreen.OnDone (decompiled)

Updates `_clan.Color2`, calls `_clan.UpdateBannerColor(...)`, then `Game.Current.GameStateManager.PopState(0)`. Banner persistence completes before returning.

## Section 2: Patch Analysis

### BesiegerCamp patch

Vanilla's anti-overlap logic spaces parties across camp frames with collision checks. TAOM preserves vanilla when frames exist, but when both frame arrays are missing, assigns `__result = settlement.GatePosition` — bypassing all spacing. See Finding 2.

### GauntletBannerEditorScreen.OnDone patch

TAOM's postfix fires after vanilla completes banner save. Timing is correct — postfix runs after persistence. The issue is in the exclusion logic, not timing. See Finding 1.

## Section 3: Siege Defense Logic

Siege defense events are config-driven via watched faction IDs. Events activate when a watched faction's settlement is besieged and the player is allied. Deadline uses `CampaignTime`. Rewards (relation + influence) on arrival; timeout clears the event. No hardcoded faction/settlement IDs — all config-driven. No dead config values found.

## Section 4: Banner Injection Logic

### BannerInjectionService

After banner editor closes, injects configured banner codes into clan/kingdom state. Operates independently from BannerColorPersistence — BannerInjection handles initial banner codes from XML/XSLT, while BannerColorPersistence handles runtime color persistence.

### BannerExclusionService

Exclusion rules are config-driven. Tracks which clans the player has manually modified to prevent reinjection. The gap: kingdom IDs are never excluded.

### BannerColorPersistence interaction

No direct code-level conflict. Features operate at different layers: BannerInjection sets initial banner codes, BannerColorPersistence preserves runtime color changes. However, both modify banner state, so ordering matters.

## Findings

### [HIGH] Player ruler banner edits can be overwritten on load — kingdom IDs never excluded

**File:** `BannerInjectionService.cs:56-63`

**TAOM code:** Postfix records `Clan.PlayerClan?.StringId` as player-modified (lines 21-24). `InjectKingdomBanners()` checks `_exclusionService.IsPlayerModified(kingdom.StringId)` (lines 56-63), but the kingdom ID was never marked. `InjectClanBanners()` skips ruling clans before consulting exclusions (line 77).

**Vanilla code:** `GauntletBannerEditorScreen.OnDone()` completes save before returning.

**Evidence:** Player ruler's clan edit is remembered under clan ID, but the associated kingdom ID is never excluded. Next `InjectBanners()` pass resets the kingdom banner back to XML/XSLT. User-visible persistence hole — loses ruler-side custom banner changes across session start/load.

**Remediation:** When the player saves a banner and `Clan.PlayerClan.Kingdom` is non-null, mark both the clan ID and kingdom ID as player-modified. Or teach `InjectKingdomBanners()` to skip the player/ruling kingdom explicitly.

### [MEDIUM] BesiegerCamp fallback bypasses vanilla anti-overlap logic — stacks all parties on gate

**File:** `BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:41-49`

**TAOM code:** When both camp frame arrays are missing, assigns `__result = settlement.GatePosition` and returns `false`.

**Vanilla code:** Repeatedly samples camp frames with collision check: `if (distance.LengthSquared < 0.25f) reject`. Retries up to 20 times per camp.

**Evidence:** Every placement request for a broken siege scene resolves to the same fixed point. No collision/spacing behavior. Result: party stacking or visibly incorrect siege camp placement at the gate.

**Remediation:** Synthesize fallback positions around the gate and run distance checks before returning. Avoid returning one identical gate coordinate for every party.

## Observations

- Siege defense config is fully data-driven with no hardcoded faction IDs — clean
- BannerInjection and BannerColorPersistence operate at different layers with no direct code conflict
- Banner exclusion service tests cover clan exclusion but not the kingdom gap
- No dead config values found in either feature

## Recommended Next Steps

1. Fix banner exclusion to mark kingdom ID when player is ruler
2. Add regression test: player edits banner as ruling clan, reloads, verify kingdom banner persists
3. Improve BesiegerCamp fallback to distribute positions around gate with spacing
4. Add test for settlement with no siege camp frames
