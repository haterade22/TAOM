# Codex pre-review: campaign army panel (#1532)

You are a read-only adversarial reviewer. Do not edit, build, deploy or run the game. Report defects with file:line
evidence and a severity (P1 breaks behaviour or crashes, P2 wrong in a real case, P3 minor). Mark anything you
could not prove from source as UNVERIFIED. Read AGENTS.md and .ai/roles/reviewer.md first.

## The change (working tree vs origin/bannerlord-1.5.x, uncommitted)

Run `git status --short` and `git diff` to see it. Files:

- `Main/_Module/GUI/PreFabs/FactionUI/ArmyOverlay.xml`: new. Replaces SandBox's `GUI/Prefabs/Map/ArmyOverlay.xml`
  by file name (vanilla: `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/GUI/Prefabs/Map/ArmyOverlay.xml`).
- `Client/Features/FactionUI/Campaign/ArmyOverlayClipWidget.cs`, `CampaignStatsMotion.cs`: new.
- `Client/Features/FactionUI/Campaign/CampaignStatsPanelWidget.cs`: easing moved to `CampaignStatsMotion`.
- `Main/_Module/GUI/Brushes/TAOMCampaignArmy.xml`, `Main/_Module/GUI/FactionUI/RuntimeSprites/cb_army_plate.png`: new.
- `tools/make_army_overlay_plate.py` and its test; tests `TAOM.Tests/Features/FactionUI/ArmyOverlay*Tests.cs`.
- `docs/features/campaign-army-panel.md` explains the design.

Intent: the vanilla army panel sat under the taller TAOM stats plate (`TAOMCampaignStats.xml`,
`CampaignStatsPanelWidget`). The TAOM panel sits in `ArmyOverlayClipWidget` (ClipContents), whose bottom margin
follows the stats plate's frame top on the plate's eased curve; both OverlayNormal states are 166.

## Known suspects (check each)

1. `ArmyOverlayWidget` (TaleWorlds.MountAndBlade.GauntletUI.Widgets.Menu.Overlay) computes the OverlayExtended
   offset from the OverlayNormal offset minus `ArmyListGridWidget.Size.Y * _inverseScaleToUse`. Does nesting
   `OverlayWidget` one level deeper (inside the clip widget) break any of its or `OverlayBaseWidget`'s logic?
2. The clip binds `@IsInfoBarExtended` of the army overlay VM; the stats plate binds `MapInfo.IsInfoBarExtended`.
   `GauntletMapOverlayView` copies `MapScreen.IsBarExtended` into the overlay VM each tick. Can they disagree long
   enough to show a gap or cover the header (first frame, load with the bar extended, overlay created mid-session)?
3. Clipping: can a tooltip, the `OverlayPopup` context menu, or gamepad navigation reach a widget that is clipped
   away, or fail to reach one that is visible?
4. Scale: `MarginBottom` set every frame in `OnUpdate`, against `PositionYOffset` from the visual state. Do both
   use the same (unscaled) units at UI scales other than 1?
5. 12 items per page and 4 columns with `ContainerPageControlWidget` and `NavigatableGridWidget`.
6. Every vanilla binding and command kept; the removed `RelationBackgroundWidget` attribute (not a property of the
   v1.5.7 `GameMenuPartyItemButtonWidget`).
7. `tools/make_army_overlay_plate.py` arithmetic at the joints.

Engine sources: the installed v1.5.7 DLLs (`pwsh -NoProfile tools/taom-src.ps1 path <Type>`, output under
`C:/Users/mikew/.taom-src/`), not `E:/Decompiled_Bannerlord`.
