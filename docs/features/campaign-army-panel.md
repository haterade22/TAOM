# Campaign army panel

## Overview

When the player leads or joins an army, the campaign map shows the army panel at the bottom right: the army
management button, the cohesion bar, the army's food and men, and a portrait for each party. TAOM replaces
vanilla's panel with one in the campaign bar theme (the stone and gold-leaf plates of the stats, time and
navigation bars). The panel stands above the TAOM stats plate and follows it as the plate extends.

## Why This Exists

- **Vanilla behavior:** `ArmyOverlay.xml` (SandBox) slides the panel down so that only its header shows above the
  vanilla map bar. Its two closed positions, 106 px below the screen edge with the bar closed and 71 px with it
  open, fit that short bar.
- **TAOM requirement:** the TAOM stats plate ([faction-ui.md](faction-ui.md)) is taller: its art reaches 108 px
  above the screen edge closed and 172 px extended. The panel has to stand above it, in the same theme.
- **Without this feature:** the stats plate covered the panel's header and party portraits, and the portraits
  showed through the plate's clear corners and below it (#1532).

## Architecture

### Design Challenge

`ArmyOverlayWidget` (vanilla, `TaleWorlds.MountAndBlade.GauntletUI.Widgets.Menu.Overlay`) owns the panel's motion.
It reads the two `OverlayNormal` visual states and computes both `OverlayExtended` states from them, minus the
portrait grid's height. Its open and closed flag is private. So the prefab can set only the closed position per
stats bar state, and it cannot hide the grid while the panel is closed. Vanilla instead hid the grid behind its
opaque map bar. The TAOM plate has transparent margins, so that does not work here.

### Solution Approach

- **Swap.** `TAOMArmyOverlay.xml` is a themed movie: `FrontEndMovieService` loads it in place of SandBox's
  `ArmyOverlay` when the data source is `ArmyMenuOverlayVM`, like the themed map bar, and holds the theme's images
  while it lives. If the themed prefab fails, vanilla's loads. The panel is cut at the themed stats plate, so it
  stays vanilla wherever the map bar does: under co-op (`GauntletLayerLoadMoviePatch` skips both), and when
  `TAOMMapBar` is not installed or failed to build.
- **Clip.** `ArmyOverlayClipWidget` wraps the panel with `ClipContents`. Its bottom margin is 90 px above the screen
  edge with the plate closed, 64 px higher with it extended. That is just inside the gold border of the plate's
  right shoulder (the step that holds the muster banner, 91.6 px up), so no notch of map shows there; along the
  rest of the plate the frame's top is 101.3 px up and covers the panel down to the cut. The cut reads the stats
  plate's shown expansion (`CampaignStatsPanelWidget.ShownExpansion`, set on the plate's own update), so it moves on
  the plate's eased slide and a panel created while the plate is open starts at the open plate. The army layer
  (order 201) updates before the map bar layer (202), so the clip sets its margin in its late update, which runs
  after every layer's update; the engine lays the change out again in the same frame (`RecalculateCanvas`).
- **Input.** Gauntlet sends hover and click events only to a widget whose rectangle holds the pointer, so the
  cut-off portraits get none. The army layer still takes the mouse over the panel's whole footprint, as vanilla's
  did, so with the panel closed a map click over the stats plate's bare stone inside that footprint does nothing.
- **Position.** With the clip carrying the stats bar's height, both `OverlayNormal` states are the same: 166 px
  down, so 68 px stand above the cut and the whole header clears the plate's frame. Opening the panel raises it by
  the grid's height. The pager sits low enough that a closed panel hides it behind the shoulder too.
- **One page.** The root names no `PageControlWidget`. Vanilla subscribes its page-count handler only through that
  reference, and on one page the handler moves the panel 40 px right (past the screen edge) and the chevron 40 px
  left.
- **Theme.** The plate is `cb_army_plate.png` (396 x 234 UI px). `tools/make_army_overlay_plate.py` builds it from
  Thyrell's `cb_stats_upper.png` at the same pixel density: the plate's right end and its mirror for the left, the
  middle stone with straight borders, and extra stone rows reflected from the plate's own stone band. The leaf
  vine cannot stretch, so the army plate has none; the stats plate's vine leaves overlap the panel's bottom left
  corner. Header icons are the stats bar's food and troops icons, the text is `FS_Garamond`, the button frame is the
  muster plate's frame, and the cohesion track and fill are the character creation slider parts at the banner
  editor's sizes (a 20 px track, a 14 px fill). Portraits sit in thin gold frames (`fs_hero_frame_9`), the leader's
  frame brighter.
- **Brushes.** `TAOMCampaignArmy.xml` names runtime sprites, which exist only once a themed screen acquires the
  character creation group. The engine binds a brush layer's sprite when it parses the file, so the file is in that
  group's reload list in `FrontEndSpriteService`.
- **Layout.** Four portrait columns of 72 x 52, three rows a page (12, vanilla 15). The page button sits in the
  column to the right and the page number just left of it, both within one row's height, so a last page of one row
  still shows them. The army management button shows only to the army's leader, as in vanilla.

The muster plate never shares the corner: vanilla shows the gather army button only when
`MobileParty.MainParty?.Army == null` (`MapBarVM.GetIsGatherArmyVisible`).

A new panel starts open (`ArmyOverlayWidget` starts with its private flag set), as vanilla's does.

Gamepad navigation is vanilla's: Down from the header goes to the portrait grid, which a closed panel hides. The
engine drops a scope when a higher layer takes the hit at its centre; vanilla's map bar covered the closed grid's
centre, and the TAOM stats plate's stat cells cover it now (computed, not yet seen in game).

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMArmyOverlay.xml` | The themed panel |
| `Main/Features/FactionUI/Menus/FrontEndMovieService.cs` | Swaps it in for `ArmyOverlay` with `ArmyMenuOverlayVM` |
| `Main/Features/FactionUI/Hooks/GauntletLayerLoadMoviePatch.cs` | Keeps vanilla's panel under co-op |
| `Main/Features/FactionUI/Resources/FrontEndSpriteService.cs` | Re-reads `TAOMCampaignArmy` after its sprites register |
| `Client/Features/FactionUI/Campaign/ArmyOverlayClipWidget.cs` | Cuts the panel at the stats plate's frame and follows it |
| `Client/Features/FactionUI/Campaign/CampaignStatsPanelWidget.cs` | Publishes the plate's shown expansion |
| `Main/_Module/GUI/Brushes/TAOMCampaignArmy.xml` | The plate, text, cohesion, extend and portrait frame brushes |
| `Main/_Module/GUI/FactionUI/RuntimeSprites/cb_army_plate.png` | The plate art (a runtime sprite; no bake) |
| `tools/make_army_overlay_plate.py` | Rebuilds the plate art from `cb_stats_upper.png` |

## Tests

- `TAOM.Tests/Features/FactionUI/ArmyOverlayClipTests.cs`: the cut's height and rise against the stats plate's
  constants.
- `TAOM.Tests/Features/FactionUI/ArmyOverlayPrefabTests.cs`: the panel sits in the clip and every widget reference
  resolves; the root has no page control; the closed panel shows the whole header with either stats bar state; a
  page fits on the plate left of the pager; a one-row page shows the pager; the cohesion track fits its nine-slice;
  every vanilla binding and command is kept.
- `FrontEndMovieServiceTests`: the swap, its view-model guard, and vanilla when the themed map bar is missing or
  failed. `GauntletLayerLoadMoviePatchTests`: the co-op skip.
- `FrontEndSpriteServiceTests.Acquire_EveryGroup_RereadsEveryBrushFileThatNamesARuntimeSprite`: every brush file
  that names a runtime sprite is re-read when its group loads.
- `tools/tests/test_make_army_overlay_plate.py`: size, the shipped file is the generator's output, straight borders
  and side edge, the end strip's lower joint, a symmetric outline.
- The prefab gates cover attributes, brushes and sprites (`PrefabAttributeNameBindingTests`, `PrefabSpriteTests`,
  `PrefabExtensionBrushTests`). `PrefabCloneWidgetReferenceTests` does not reach this prefab: it pairs a TAOM
  prefab with a vanilla one of the same file name.
- No unit test reaches the clip's per-frame read or the plate's write of `ShownExpansion` (both need a live
  `UIContext`); in-game steps 3 and 4 cover them.

## How to verify in game

**Preconditions:** a campaign save in which the player is in a kingdom and can gather an army, a build deployed
from this branch, game restarted.

1. Gather an army of at least two parties. On the map, look at the bottom right with the stats bar closed. Pass: the
   army panel's plate, cohesion bar, chevron and portrait frames draw (not blank); the panel opens with every
   portrait row above the stats plate's gold frame, and no portrait shows through or below the plate. A result only
   a person can see.
2. Click the chevron. Pass: the panel drops until only its header (army button, cohesion bar, food, men, chevron)
   stands above the plate's frame, with no portrait showing; click it again and it rises by the portrait rows. A
   result only a person can see.
3. Open and close the stats bar with its clasp, with the army panel closed and then open. Pass: the panel rides up
   and down with the plate's frame and no gap or overlap shows during the slide.
4. Extend the stats bar, then form a new army (or join one). Pass: the new panel starts above the open plate, with
   no slide up from below.
5. With fewer than 13 parties, look at the panel's right edge. Pass: the plate's right border lines up with the
   stats plate's, and the chevron stays at the header's right end.
6. Hover the cohesion bar, the food and the men, and click a portrait. Pass: each shows vanilla's tooltip, and the
   portrait opens the party's context menu.
7. As the army's leader, click the army button and the cohesion bar. Pass: army management opens. As a member of
   another lord's army, the button is hidden.
8. With 13 to 16 parties, open the panel and click the page button. Pass: the next page shows one row, and the page
   button and page number stay visible.
9. While in the army, open a screen that hides the campaign bars (the inventory or the party screen), then a game
   menu (enter a town). Pass: the army panel does not stand alone where the stats plate was; if it does, record a
   screenshot for the issue. Close each one. Pass: the panel returns above the plate's frame.
10. As the army's leader, let cohesion fall below the warning level (or join an army with low cohesion). Pass: the
    cohesion fill turns red, and for the leader it pulses.
11. Check at 1920x1080 and at one other UI scale (Options, UI scale). Pass: the same as steps 1 to 3.
12. Under co-op (the Coop module loaded), form an army. Pass: vanilla's army panel and map bar show.
13. With a gamepad, close the panel, then press Down from its header. Record where the focus goes (vanilla parity:
    it should not land on a hidden portrait).

## How to change the plate art

1. Edit `tools/make_army_overlay_plate.py`, or replace `cb_army_plate.png` with painted art of the same size.
2. To rebuild: `python tools/make_army_overlay_plate.py --out Main/_Module/GUI/FactionUI/RuntimeSprites/cb_army_plate.png`.
3. If the size changes, change `SuggestedWidth` and `SuggestedHeight` of `OverlayWidget` and the two `OverlayNormal`
   states to match, then run `ArmyOverlayPrefabTests`.

## Changelog

- 2026-11-22: the army panel stands above the TAOM stats plate and follows it, restyled in the campaign bar theme
  (#1532).

## GitHub Issue

- **Issue:** #1532: [Army panel hidden under the campaign stats plate; restyle it in the campaign bar theme](https://github.com/haterade22/TAOM/issues/1532)
- **Status:** Open
