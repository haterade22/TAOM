# Codex Adversarial Review: Session Bug Fixes (43 fixes across 18 reviews)

**Date:** 2026-04-07
**Target:** working tree diff (all fixes from review session)
**Verdict:** needs-attention

Several review-session fixes still leave correctness holes: the banner-color fallback produces indistinguishable gray-on-gray banners, the shader-precompile abort leaks its global active latch, siege fallback spacing packs parties closer than vanilla's collision threshold, and the child sex fix uses unnecessary reflection.

## Findings

### [HIGH] Bitwise RGB inversion does not guarantee a visible secondary color

**File:** `BannerColorService.cs:30-37`

**What the fix does:** Inverts RGB channels when icon and background colors match: `alpha | (~background & 0x00FFFFFF)`.

**What's wrong:** For grayscale backgrounds, inversion yields effectively the same color. `0x808080` becomes `0x7F7F7F` — visually indistinguishable. Defeats the fix's purpose for the exact cases it targets.

**Fix:** Replace raw inversion with a contrast-aware fallback. Compute luminance and choose guaranteed-contrast color (black/white), or invert only if contrast ratio exceeds minimum threshold. Add regression test for mid-gray inputs.

### [HIGH] Shader abort path leaves global precompile state latched on

**File:** `LoadingScreen_ShaderProgress_Patch.cs:79-85`

**What the fix does:** Added `ResetShaderBattleActive()` on successful completion (`remaining == 0`) and startup exception.

**What's wrong:** When stuck detection fires, the patch calls `MBGameManager.EndGame()` but never clears `IsShaderBattleActive`. After one aborted precompile, later unrelated loading screens inherit the shader text override and 120s forced abort.

**Fix:** Clear `IsShaderBattleActive` before or immediately after `EndGame()`. Add cleanup path on mission/game shutdown so every exit path resets shader state.

### [MEDIUM] Fallback siege positions violate vanilla party-separation math

**File:** `BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:49-57`

**What the fix does:** Distributes parties in a ring at radius `0.5f` with 8 evenly spaced slots.

**What's wrong:** Adjacent positions on that ring are `~0.382` units apart (`2r*sin(pi/8)`), but vanilla rejects positions closer than `0.5` units (`LengthSquared < 0.25`). Fallback parties are still packed too close.

**Fix:** Increase starting radius to `0.7f` or reduce slots per ring so adjacent positions stay >= `0.5` units apart. Use `minRadius = minSeparation / (2 * sin(pi/slotsPerRing))`.

### [MEDIUM] Goblin adulthood config still contradicts design intent

**File:** `race_age_config.json:12`

**What the fix does:** Set all races' `comesOfAge` to `18` to match vanilla `HeroComesOfAge` gate.

**What's wrong:** Goblins were supposed to reach adulthood at age 2 per original design. The blanket `comesOfAge=18` overwrite removed the intended fast-maturation for goblins.

**Fix:** Set goblin `comesOfAge` to the intended value (2 or whatever design specifies). Add regression test covering race-age service for goblin maturation.

### [MEDIUM] Child sex fix uses brittle reflection despite public setter existing

**File:** `ChildCreatorAdapter.cs:26-30`

**What the fix does:** Uses `ReflectionHelper.SetFieldValue` to write `<IsFemale>k__BackingField` on `BasicCharacterObject`.

**What's wrong:** `BasicCharacterObject.IsFemale` is a public virtual property with a setter in v1.3.15. The reflection write bypasses setter logic, hard-codes a private implementation detail, and already crashed once (targeting wrong type). Unnecessarily fragile.

**Fix:** Use the public property setter directly: `characterObject.IsFemale = isFemale`. Remove the reflection call.

## Sections Not Covered (Codex limitations)

**Section 1a (Kingdom reflection fields):** Codex could not directly decompile `Kingdom` to verify `<PrimaryBannerColor>k__BackingField` and `<SecondaryBannerColor>k__BackingField`. These should be verified manually against `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds\CampaignSystem\Kingdom.cs`.

**Section 2c (OnAgentBuild signature):** Codex did not verify `MissionLogic.OnAgentBuild` parameter list in v1.3.15. The Banner parameter addition needs manual verification.

**Section 3 (Fail-safe consistency):** Codex did not enumerate all `?? true` vs `?? false` patterns across BannerColorPersistence. A grep for `?? true` across all patch files should be run manually.

**Section 5a (alignment.json):** Codex did not flag the shaghana/abanissa alignment values. Per user design notes, these should be `neutral`, not `evil`. This needs manual correction.

**Section 6a (Banner.ChangePrimaryColor):** Codex did not verify these methods exist in v1.3.15 Banner. Check decompiled `TaleWorlds.Core.Banner`.

**Section 6b (MissionAdapterFactory.ClearCache coverage):** Codex confirmed it's called from `WargMissionBehavior.OnRemoveBehavior` but did not verify if `AdvancedCombatBehavior` also clears it. Stale-cache window for non-warg combat missions likely exists.

## Recommended Next Steps

1. Fix banner color contrast logic for gray-on-gray (HIGH)
2. Clear shader latch on abort path (HIGH)
3. Increase siege fallback ring radius for vanilla-compatible spacing (MEDIUM)
4. Correct goblin comesOfAge to design-intended value (MEDIUM)
5. Replace IsFemale reflection with public setter (MEDIUM)
6. **Manual verification needed:** Kingdom reflection fields, OnAgentBuild signature, alignment.json shaghana/abanissa, Banner API methods, MissionAdapterFactory coverage
