# RCA: Campaign army panel review (#1532, 2026-11-22)

## Top-line

The themed campaign army panel (`TAOMArmyOverlay.xml`, `ArmyOverlayClipWidget`, `TAOMCampaignArmy.xml`) went to a
tier 2 review: lenses 5 (data flow), 7 (XML) and Tooling, and a Codex pre-review at xhigh. They found one HIGH that
would have shipped a blank panel: the new brush file was in no image group's reload list, so every brush layer
that names a runtime sprite kept a null sprite. The same pass found a 40 px shift of a one-page panel, a file-name
replacement that also applied under co-op, and a clip that started low when a panel was created with the stats bar
open. The fix touched `GauntletLayerLoadMoviePatch`, so it went to a tier 1 re-review: a Codex pass at high, the
second wave lenses 1, 2, 3, 4 and 6, and lenses 5 and 7 again on their own findings. They confirmed rows 1 to 4
closed and found one more MEDIUM in the row 4 fix (row 10), plus LOW test, doc and guard items.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `TAOMCampaignArmy.xml` names runtime sprites (`cb_army_plate`, `cc_slider_track_9`, `fs_hero_frame_9`) but was not in `FrontEndSpriteService`'s character creation brush list. The engine parses brush files at startup, before the sprites register, and binds each layer's sprite once (`BrushFactory.cs:217`), so the plate, cohesion bar, chevron and portrait frames would draw blank (lens 7, lens 5, Codex P2). | Silent engine fallback | The list sits in a service far from the brushes; nothing tied a new brush file to it, and the in-game check had not run. The author added the brush file the way vanilla-sprite brush files are added. | `TAOMCampaignArmy` added to the list. New gate `FrontEndSpriteServiceTests.Acquire_EveryGroup_RereadsEveryBrushFileThatNamesARuntimeSprite` loads every real runtime sprite, acquires every group and fails any brush file that names one and is never re-read. Lesson in `localization-ui.md`. |
| 2 | MEDIUM | On one page, `ArmyOverlayWidget.OnArmyListPageCountChanged` sets `Overlay.PositionXOffset = 40` and `ExtendButton.PositionXOffset = -40`: the plate's right border went past the screen edge (lens 7, lens 5, Codex P3). | Vanilla side effect kept by a copied reference | The prefab kept vanilla's root references; the handler is subscribed only through the `PageControlWidget` setter, which nobody read. | The root names no `PageControlWidget`; `ArmyOverlayPrefabTests.Root_HasNoPageControl_SoVanillaNeverShiftsAOnePagePanel`. |
| 3 | MEDIUM | The prefab replaced SandBox's `ArmyOverlay.xml` by file name, so it loaded under co-op too, where the themed map bar does not, and stood 90 px above vanilla's short bar (lens 7). | Theme gate bypassed | Replacing by file name is the simplest Gauntlet override; the co-op skip lives in the movie swap patch, which a file-name replacement never reaches. | Renamed `TAOMArmyOverlay.xml`, swapped in by `FrontEndMovieService` for `ArmyOverlay` with `ArmyMenuOverlayVM`; `GauntletLayerLoadMoviePatch` skips it under co-op. Tests in `FrontEndMovieServiceTests`. |
| 4 | MEDIUM | The clip ran its own slide from 0, so a panel created while the stats bar was open (forming or joining an army) started at the closed cut and took 0.32 s to rise, under the plate's frame (Codex P2). | Duplicated state | The clip copied the plate's motion instead of reading it, and a new overlay is created mid-session (`MapScreen.cs:1829`). | `CampaignStatsPanelWidget.ShownExpansion` (static, set on the plate's update); the clip reads it; `CampaignStatsMotion` deleted. |
| 5 | LOW | A last page of one row shows 120 px of the panel; the page number at 112 px with a 15 pt font was cut (Codex P3). | Geometry | The prefab test checked a full page only. | Page number left of the page button, both inside one row; `ArmyOverlayPrefabTests.OneRowPage_ShowsThePageButtonAndNumber`. |
| 6 | LOW | The cohesion track (16 px) was shorter than its nine-slice's 20 px of top and bottom borders. | Geometry | | 92 x 20 track, 86 x 14 fill, the banner editor's sizes; `CohesionTrack_IsTallEnoughForItsNineSliceBorders`. |
| 7 | LOW | The binding test missed seven vanilla tokens (`@NameText`, `@DescriptionText`, `@LocationText`, `@Relation`, `{Visual}`, `@ElementID`, `@IsInfoBarExtended`). | Test scope | | Added to `EveryVanillaBindingAndCommand_IsKept`. |
| 8 | LOW | The plate tool's end strip jumped from source row 132 to 140 at its lower joint (Tooling, Codex P3). | Tool arithmetic | The run up started from `BOTTOM_FROM - 1`, outside the end strip's band, and `reflect` folded it back to row 132. | The bottom part starts at the band's end; asserts on both run starts; `test_end_strip_runs_into_its_bottom_part_on_consecutive_source_rows`. Pillow skip guard, straight edge and border tests, README test pointer. |
| 9 | NOT APPLIED | Gamepad Down from the header reaches the clipped portrait grid (Codex P2). | Vanilla parity | | Vanilla's prefab has the same navigation into a grid its map bar hid. Lens 2 found the engine drops a scope whose centre a higher layer covers, and the stats plate's cells cover it (computed); in-game step 13. |
| 10 | MEDIUM | The fix for row 4 read `ShownExpansion` in the clip's `OnUpdate`. The army layer (order 201) updates before the map bar layer (202), so the clip ran one frame behind the plate, about 5 px in the middle of a slide, more than the header's 2.7 px clearance (Codex re-review P2). | Engine update order | The fix assumed the plate updates first; `ScreenManager.Tick` ticks sorted layers in order, and a global layer's screen layer is one of them. | The clip sets its margin in `OnLateUpdate`: every layer's `Tick` runs before any layer's `LateUpdate`, and `UIContext.LateUpdate` lays the change out again (`RecalculateCanvas`). The constructor sets the first cut, so the prefab holds no copy of it (lens 6). |
| 11 | LOW | The tool test's image equality used `ImageChops.difference(...).getbbox()`, which on RGBA looks at alpha only (Codex re-review P3). | Test strength | | Compares `tobytes()`; the joint check starts 6 rows above the pasted part. |
| 12 | LOW | If `TAOMMapBar` is missing or failed to build, the themed army panel still loaded and was cut at the themed plate's height over vanilla's bar (lens 5; lenses 1 and 3 named the case). | Coupled themes | The swap case did not ask whether the screen it is cut against is themed. | `CanTheme("TAOMMapBar")` on the swap case; two tests in `FrontEndMovieServiceTests`. |
| 13 | LOW | The co-op skip test read the patch's source line (lens 6, lens 4). | Test kind | | `GauntletLayerLoadMoviePatch.StaysNativeUnderCoop`, tested directly in `GauntletLayerLoadMoviePatchTests`; ratchet baseline 8 to 7. |
| 14 | LOW | The gate test read all 373 runtime PNGs (115 MB) as text nothing used (lens 3). | Test cost | | Reads only the `.nine` files' text. |
| 15 | LOW | The feature doc said a new panel starts closed (it starts open: `_isOverlayExtended = true`), overclaimed the hit test, gave the wrong reason for gamepad parity, and credited `PrefabCloneWidgetReferenceTests`, which skips this prefab (lens 2, lens 4). | Doc accuracy | Written from the prefab's intent, not from the engine. | Doc corrected; `EveryWidgetReference_ResolvesToAWidget` walks each reference path; in-game steps for low cohesion, hidden bars and the gamepad added. |
| 16 | LOW | The clip widget's summary and the prefab's header comment repeated the feature doc (lens 1). | Comments | | Cut to a contract line and a pointer. Rename to a `Taom` prefix NOT APPLIED: no engine type has the name (lens 2), and the sibling widgets have no prefix. |
| 17 | (merge) | Trunk 40cc17b54 changed the map bar's co-op rule while this branch was in review: a BannerlordCoop session and solo play now get the themed map bar; only a co-op mod TAOM cannot probe keeps vanilla's. The fixes in rows 3 and 13 (a presence skip in the patch, `StaysNativeUnderCoop`) would have left a vanilla panel above a themed plate in a Coop session. | Parallel change | The branch's co-op rule copied the patch's condition instead of naming the movie in one shared place. | Merge resolution: `ThemedMapBarCoopPolicy.IsMapBarMovie` names `ArmyOverlay`, so the panel follows the bar's rule; `StaysNativeUnderCoop` and its test removed; `ThemedMapBarCoopPolicyTests` row added; in-game step 12 checks a Coop session. |

- FOLLOW-UP: in a BannerlordCoop session, Coop's `RobustnessPatches` finalizer on `GauntletMovie.LoadMovie` logs and swallows every prefab build exception, so `GauntletLayerLoadMoviePatch.Finalizer` never sees one: a themed movie that fails to build stays half built instead of falling back to vanilla, `LoadFailed` never records it, and `CanTheme("TAOMMapBar")` cannot see a failed bar. It reaches every TAOM movie swap since trunk 40cc17b54 themes the map bar in a session, and this branch adds the army panel to it (merge lens 5, flow 5; Coop source `RobustnessPatches.cs`). no issue: a fix in the shared movie hook (treat a swap that built no root as failed) is outside #1532, and filing it needs Mike's word.

## Root-cause pattern

Findings 1, 2 and 3 share one cause: the panel was built as a prefab file and reviewed as one, while three
behaviours live in code the prefab never names: the service that re-reads brush files after the theme's sprites
register, a vanilla handler subscribed by a property setter, and the patch that keeps the theme off under co-op.
Gauntlet fails none of them loudly. Finding 4 is the same shape inside TAOM: a second copy of state that already
existed. Its first fix (row 10) shared the state but read it at the wrong point in the frame: a value shared across
layers is only current after the layer that writes it has ticked.

## Why each reviewer caught what it did

- **XML (7):** traced each brush file to the code that loads it, decompiled `ArmyOverlayWidget`'s setters, and
  compared the override path with the map bar's.
- **Data flow (5):** followed the sprite registration order and the page-count event.
- **Codex:** traced the overlay's creation in `MapScreen` and measured the generated PNG's joint rows; in the
  re-review, sorted the layers by order and followed `ScreenManager.Tick` to find the one-frame lag.
- **Engine (2):** read `ArmyOverlayWidget`'s initial state and the hit test between layers, which corrected the doc.
- **Design (6) and Completeness (4):** found the source-reading test and the duplicated first cut, and the doc's
  gate claim.

## Feedback memories to codify

None beyond the lesson.
