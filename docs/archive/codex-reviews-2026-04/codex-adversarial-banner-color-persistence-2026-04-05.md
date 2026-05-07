# Codex Adversarial Review: BannerColorPersistence

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The branch has at least three user-visible correctness regressions: the unique secondary-color fix is effectively disabled, drift guard suppresses vanilla kingdom-color syncing for every clan, and the persistence logic is scoped broadly enough to repaint AI clans instead of staying player-only.

## Files Reviewed

### Core
- `Main/Features/BannerColorPersistence/BannerColorService.cs`
- `Main/Features/BannerColorPersistence/BannerColorConfig.cs`
- `Main/Features/BannerColorPersistence/BannerColorConfigProvider.cs`
- `Main/Features/BannerColorPersistence/IBannerColorService.cs`
- `Main/Features/BannerColorPersistence/IBannerColorConfigProvider.cs`
- `Main/Features/BannerColorPersistence/BannerColorPersistenceIoC.cs`

### Patches (highest risk)
- `Main/Features/BannerColorPersistence/Hooks/Banner_GetFirstIconColor_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs`
- `Main/Features/BannerColorPersistence/Hooks/BannerEditorView_OnTick_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs`
- `Main/Features/BannerColorPersistence/Hooks/CampaignUIHelper_GetCharacterCode_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/Clan_UpdateBannerColor_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/ClanPartyItemVM_GetCharacterCode_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/HeroViewModel_FillFrom_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/PartyCharacterVM_GetCharacterCode_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/PartyVM_RefreshCurrentCharacterInformation_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/SandBoxUIHelper_GetCharacterCode_Patch.cs`
- `Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs`

### Tests
- `TAOM.Tests/Features/BannerColorPersistence/BannerColorConfigProviderTests.cs`
- `TAOM.Tests/Features/BannerColorPersistence/BannerColorServiceTests.cs`
- `TAOM.Tests/Features/BannerColorPersistence/Clan_UpdateBannerColor_PatchTests.cs`
- `TAOM.Tests/Features/BannerColorPersistence/Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs`

## Focus Areas

1. Transpiler correctness — IL injection point assumptions for v1.3.15
2. Drift guard completeness — other code paths that mutate clan banner colors
3. Player-only scoping — patches should only affect player clan
4. Thread safety / state races — stale-cache scenarios
5. Null/edge cases — Owner null, no kingdom, malformed banner code
6. Test coverage gaps

## Findings

### [HIGH] Unique secondary-color patch falls back to vanilla exactly when it should fix the overlap

**File:** `Banner_GetFirstIconColor_Patch.cs:17-25`

`BannerColorService.GetUniqueIconColor()` returns `uint.MaxValue` when the icon color matches the banner background, which is the sentinel the feature uses for "no distinct secondary color". But this prefix treats that sentinel as "do nothing" and returns `true`, so vanilla `Banner.GetFirstIconColor()` runs and returns the original duplicate icon color instead. Decompiling v1.3.15 confirms the vanilla method simply returns the first icon color when present, so the feature never fixes the overlap case it was introduced for.

**Remediation:** When `GetUniqueIconColor()` returns `uint.MaxValue`, set `__result = uint.MaxValue` and return `false` so the sentinel is preserved. Add a regression test that covers `primaryColor == iconColor` against the prefix behavior, not just the service helper.

### [HIGH] Drift guard disables kingdom banner syncing for every clan, not just the player clan

**File:** `Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs:14-18`

This prefix has no `Clan __instance` parameter and no scope check; when drift guard is enabled it skips `Clan.UpdateBannerColorsAccordingToKingdom()` unconditionally. In vanilla v1.3.15, that method is called from `Clan.Deserialize()` and `SetKingdomInternal()`, and its job is to apply kingdom banner colors to clans in kingdoms. Blocking it globally means AI clans stop inheriting kingdom colors on load and on kingdom membership changes, which is a broad visible regression unrelated to player banner persistence.

**Remediation:** Pass `Clan __instance` into the prefix and only suppress vanilla behavior for the exact clan(s) meant to keep custom colors, likely `Clan.PlayerClan` or another explicit allowlist. Keep vanilla execution for all other clans and add a test that exercises a non-player clan in a kingdom.

### [HIGH] Persistence logic is global to any clan with nonzero colors, so AI clan visuals are rewritten too

**File:** `BannerColorService.cs:16-21`

`ShouldUseClanColor()` only checks that the feature is enabled and the clan colors are nonzero. `BannerHeroAdapter.GetClanColorInfoFromHero()` produces that info for any hero with a clan, and UI/battle patches such as `CampaignUIHelper_GetCharacterCode_Patch` apply it directly. There is no player-clan guard anywhere in this decision path. Because vanilla v1.3.15 scene/UI code uses `hero.MapFaction` colors, this change rewrites much more than the player banner persistence path and will recolor AI clans as soon as the feature is on.

**Remediation:** Make the persistence decision explicit about scope: include a player-clan or intended-owner check in the service path, and cover it with tests for player clan vs AI clan. If some screens are intentionally global, separate those paths from the player-only persistence logic instead of sharing one permissive predicate.

## Recommended Next Steps

1. Fix `Banner_GetFirstIconColor_Patch` to propagate the `uint.MaxValue` sentinel instead of falling through to vanilla
2. Scope `Clan_UpdateBannerColorsAccordingToKingdom_Patch` to player clan only via `Clan __instance` check
3. Add player-clan guard to `BannerColorService.ShouldUseClanColor()` or the calling patches
4. Re-test after fixes with at least: duplicate icon/background colors, non-player clan kingdom syncing on load/join, and player-vs-AI character code rendering paths
