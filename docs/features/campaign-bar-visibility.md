# Campaign bar visibility

## Overview

TAOM's campaign map bars (the left navigation, the centre time controls, the right stats panel and the
muster plate) hide while a native screen or a full-screen map overlay is open, and show again on the
bare map. No art, binding or command of the bars changes.

## Why This Exists

- **Vanilla behavior:** `GauntletMapBarGlobalLayer` keeps the map bar layer enabled over Character,
  Inventory, Party, Quests, Clan, Kingdom and the other screens pushed over the map.
- **TAOM requirement:** the illustrated bars in `TAOMMapBar.xml` are large; they must not sit over a
  full-screen page or catch its clicks.
- **Without this feature:** the bars draw on top of those screens and take input meant for them.

## Architecture

### Design Challenge

The map bar layer stays enabled, so its own state cannot tell that a screen is on top. The flags that
can are on `MapTimeControlVM`, which the layer rewrites every frame (v1.5.7): `IsInMap` is true only
while the map screen is the top screen, and the overlay flags are copied from `MapScreen`.

### Solution Approach

- **Wrapper widget:** `CampaignBarsWidget` (TAOM.Client) wraps the four bars in `TAOMMapBar.xml`. Each
  `OnUpdate` it sets only its own `IsVisible` from `ShouldShow(MapBarVM.IsEnabled, MapBarVM.MapTimeControl)`.
  It holds no latched state, so closing a nested screen (Party opened from Character) keeps the bars
  hidden until the map is on top again.
- **Hidden states:** native disabled, `IsInMap` false, or any of the encyclopedia, army management,
  town management, recruitment, hideout troop management, campaign options and battle simulation
  flags. Small popups (escape menu, marriage offer, heir selection, map incident, map cheats, overlay
  context menu) keep the bars visible.
- **Model attach:** `CampaignPresentation.OnLoaded` finds the wrapper with `FindChildrenWithType` and
  sets its `Model` to the `MapBarVM`. That lookup skips a widget with no `Id`, so the prefab gives the
  wrapper `Id="CampaignBars"`. With no model attached, the widget leaves its visibility to the root.
- **Sibling paths:** the four bars stay direct siblings inside the wrapper, so the muster plate's
  `..\InfoBarWidget` path still resolves. The wrapper has `DoNotAcceptEvents="true"`.

### Component Diagram

```
GauntletMapBarGlobalLayer (native, writes MapTimeControlVM flags each frame)
        |
     MapBarVM --- CampaignPresentation.OnLoaded (attaches Model)
        |
TAOMMapBar.xml: CampaignBarsWidget Id=CampaignBars
        +-- TAOMCampaignNavigation, TAOMCampaignTime, TAOMCampaignStats (InfoBarWidget), TAOMCampaignMuster
```

## Configuration

None. No MCM setting, no ModuleData.

## Key Files

| File | Purpose |
|------|---------|
| `Client/Features/FactionUI/Campaign/CampaignBarsWidget.cs` | Wrapper widget and the `ShouldShow` rule |
| `Client/Features/FactionUI/Campaign/CampaignPresentation.cs` | Attaches the `MapBarVM` to the wrapper on movie load |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMMapBar.xml` | Wraps the four bars in `CampaignBarsWidget` |

## Dependencies

- FactionUI's `CampaignPresentation` movie load hook ([faction-ui.md](faction-ui.md)).
- Native `MapBarVM` and `MapTimeControlVM`.

## Tests

- `TAOM.Tests/Features/FactionUI/CampaignBarsWidgetTests.cs`: the show and hide rule for the bare map,
  a screen on top, each full-screen overlay, the popups that keep the bars, nested and repeated opens,
  the native disabled state and no model; the prefab wraps the four bars as siblings; and the
  production `OnLoaded` attaches the model through the installed `FindChildrenWithType` over a tree
  built from the shipped prefab. Category `RequiresGame`.

## How to verify in game

**Preconditions:** a loaded campaign on the map. No restart or setting needed.

1. Open Character, Inventory, Party, Quests, Clan and Kingdom in turn. Each time the navigation, time,
   stats and muster bars are gone; close the screen and they return. A person must see this; no log line
   reports it.
2. Open Party from the Character screen, close Party: the bars stay hidden. Close Character: they return.
3. Open the encyclopedia from the map: the bars hide; close it: they return.
4. Press Escape on the map: the bars stay visible.

## Changelog

- 2026-11-14: bars hide over native screens and full-screen map overlays.

## GitHub Issue

- **Issue:** none; the maintainer waived the issue for this work.
