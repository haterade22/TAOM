# Codex re-review: campaign army panel fixes (#1532)

You are a read-only adversarial reviewer. Do not edit, build, deploy or run the game. Report defects with file:line
evidence and a severity (P1 breaks behaviour or crashes, P2 wrong in a real case, P3 minor). Mark anything you
could not prove from source as UNVERIFIED. Read AGENTS.md and .ai/roles/reviewer.md first.

## The change (working tree vs HEAD, uncommitted)

Run `git status --short` and `git diff`; new files are untracked. Your earlier pre-review of this change is
`docs/reviews/raw/codex-prereview-army-overlay-2026-11-22.md`. This pass reviews the fixes for its findings and for
the Claude lenses' findings, and anything the fixes broke.

The fixes:

1. Brush reload: `TAOMCampaignArmy` added to the character creation group's brush list
   (`Main/Features/FactionUI/Resources/FrontEndSpriteService.cs:50`). New gate
   `FrontEndSpriteServiceTests.Acquire_EveryGroup_RereadsEveryBrushFileThatNamesARuntimeSprite`.
2. One-page shift: the root `ArmyOverlayWidget` no longer names `PageControlWidget`.
3. Co-op: the prefab is renamed `TAOMArmyOverlay.xml` and swapped in by `FrontEndMovieService.BeginLoad`
   (`"ArmyOverlay"` with `"ArmyMenuOverlayVM"`), instead of replacing SandBox's `ArmyOverlay.xml` by file name;
   `GauntletLayerLoadMoviePatch` skips `ArmyOverlay` under `CoopPresence.IsActive`, as it does `MapBar`.
4. Clip start: `CampaignStatsPanelWidget` publishes `public static float ShownExpansion`, set on each of its
   updates; `ArmyOverlayClipWidget` reads it each frame instead of running its own slide. `CampaignStatsMotion` is
   deleted and the stats widget's slide code is back to its trunk form.
5. Pager: the page number moved left of the page button, both within one 52 px row.
6. Cohesion track 92 x 20, fill 86 x 14 (the banner editor's sizes for these sprites).
7. Plate tool: the end strip's lower joint now pastes source rows from the band's end (136), so the run up starts
   on a continuous row; asserts on both run starts; the PNG regenerated.
8. Not applied: gamepad Down into the clipped grid. Vanilla's prefab has the same navigation over a grid its map
   bar hid. Challenge this if the reading is wrong.

## Known suspects (check each)

1. The swap: does `ThemedMovie`/`SwapState` (hold, release, vanilla fallback) work for a movie loaded on the
   `MapArmyOverlay` layer by `GauntletMapOverlayView` (engine `SandBox.GauntletUI.Map.GauntletMapOverlayView`), which
   can be created and released many times in one session as armies form and break up? Is the hold released?
2. Does anything else load or reference `ArmyOverlay` by name (other TAOM prefabs, UIExtenderEx extensions,
   `CampaignPresentation.Bind`, other mods listed in `docs/reference/external-mod-couplings.md`)?
3. `ShownExpansion` is static: a stale value across a campaign reload, a second map bar instance, or a hidden map
   bar layer whose widgets do not update. What does the clip show in each case?
4. The new gate: does it model what the engine and `FrontEndSpriteService` actually do (which groups load which
   brush files, `.nine` sprites registered as `<name>_9`, the resident `TAOMLoading` file)?
5. The co-op skip test reads the patch's source line. Is that line the only co-op gate on this path?
6. Pager and cohesion geometry against the prefab tests' arithmetic.

Engine sources: the installed v1.5.7 DLLs (`pwsh -NoProfile tools/taom-src.ps1 path <Type>`, output under
`C:/Users/mikew/.taom-src/`), not `E:/Decompiled_Bannerlord`.
