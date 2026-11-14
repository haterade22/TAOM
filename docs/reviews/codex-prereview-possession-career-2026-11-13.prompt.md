# Codex pre-review: capture the career the hero received (#1207)

You are an independent reviewer. Read only; change no file. Report findings with file:line evidence, severity (CRITICAL, HIGH, MEDIUM, LOW) and a failure scenario. Missing engine or Coop evidence stays UNVERIFIED. Return the report as your final message.

## Scope

The uncommitted change in `E:\repos\taom-coop-rules` (branch `fix/1207-capture-assigned-career` from trunk `39f1b8374`; `git diff` plus one untracked test file):

- `Main/Features/PlayerPossession/PlayerPossessionBehavior.cs`: the constructor takes `ICareerDataService` instead of `ICareerMenuService`. At `OnCharacterCreationIsOver` phase 9 the captured career is now `ICareerDataService.GetCareerStringId(hero.StringId)` (new `internal static ChoicesFor`), not `ICareerMenuService.SelectedCareerStringId`.
- `Main/SubModule.cs` (about line 1061): the behavior's construction resolves `ICareerDataService`.
- `TAOM.Tests/Features/PlayerPossession/PlayerPossessionCaptureTests.cs` (new): two tests of `ChoicesFor`. The two other test files change only the substitute type.
- `docs/features/player-possession.md` (a Key Decisions paragraph, the tests list) and `docs/features/character-creation.md` line 27.

Intent: with no career-menu pick, `CharacterCreationContentService.AssignCareer` auto-assigns the first career eligible for the culture and leaves `SelectedCareerStringId` null. The capture read the menu, so it recorded no career, and a co-op join re-applied none. A co-op client log on 2026-11-13 shows `Auto-assigned career 'avanc_luth_raider' during CC` at 12:02:01 and `Captured character-creation choices for 'main_hero' (culture=empire, race=0, career=none)` at 12:02:03.

Facts read for this change (verify them; do not trust them):
- `CharacterCreationContentService.cs:190` calls `AssignCareer`; the fallback branch starts at about line 262 and calls `_careerHandler.OnCareerSelected(heroStringId, career.Id)`.
- `CareerDataService.GetCareerStringId` (`CareerDataService.cs:46-49`) returns null for a hero with no entry. `ResetForNewCampaign` (line 98) runs on `OnNewGameCreatedEvent` (`CareerPersistenceBehavior.cs:33-34`).
- v1.5.7 `CharacterCreationManager` runs `ApplyFinalEffects` before `FinalizeCharacterCreationState`, and `CharacterCreationState` raises `OnCharacterCreationIsOver` (ten phases, 0 to 9).

## Questions

1. At phase 9, does `GetCareerStringId(Hero.MainHero.StringId)` return the career `AssignCareer` gave? Check the engine order from the content handlers to phase 9, and whether anything clears or replaces the career data in between (`ResetForNewCampaign`, a load, another TAOM handler at phases 0 to 8).
2. TAOM's Player Switcher hands the player a lord inside the finalize loop (priority 1100) and assigns that lord a career. At phase 9, which hero is `Hero.MainHero`, and which career does the capture now record, compared with the menu pick before? Is the new value the right one to re-apply after a join?
3. Every case where the new capture differs from the old one: a menu pick (same?), a menu pick whose `OnCareerSelected` failed, an auto-assigned career, no eligible career, a Player Switcher hand-over. Is any of them now worse?
4. `JoinReconciliationService.ReapplyCharacterCreationPackage` now receives a career more often. Does `ICareerCreationHandler.OnCareerSelected` on the joined hero do harm when that hero already has a career (a rejoin, or a career the host synced)? The re-apply is single-consumption and guarded by the per-hero `SyncData` marker.
5. Composition: is `ICareerDataService` registered (singleton) before `SubModule` constructs the behavior? Does any code still need `ICareerMenuService` here?
6. Are the tests sound: can either pass for the wrong reason? Is the doc text accurate?

Engine source: the installed DLLs are the truth (`pwsh tools/taom-src.ps1 path <Type>`); the dump at `E:\Decompiled_Bannerlord\_modules_build` and `_shipping_build` is v1.5.7. Coop source: `E:\repos\coop-4593` (bannerlord-1.5.x 995517e60).
