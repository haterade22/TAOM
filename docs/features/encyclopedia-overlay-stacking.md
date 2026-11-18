# Encyclopedia Overlay Stacking

## Overview

The TAOM encyclopedia header (back, forward, breadcrumb, search and close) and its search results stay above every home, list and detail page. Every page also sits against the same 1728 by 972 frame as the header, so the header stays clear of the page at any viewport height down to 938 logical units.

## Why This Exists

- **Vanilla behavior:** `EncyclopediaData` loads the bar movie first and each page movie after it on the same Gauntlet layer. Gauntlet renders the layer root's children in list order, and `EventManager.CollectEnableWidgetsAt` hit-tests them in reverse, so the last movie loaded is on top.
- **TAOM requirement:** the TAOM bar is a framed header with a search dropdown, and the detail pages are full-height folios. Both must be readable and clickable together.
- **Without this feature:** every page covered the header and the search results (#1212). The results were invisible, and a click on a result reached the page under it.

## Architecture

### Design Challenge

The bar and the page are separate movies, so no prefab can order one above the other. The bar's root was a full-screen dimmer that dismissed the search results on an outside click, and that dimmer must stay under the pages so it does not darken them or take their clicks.

### Solution Approach

Two widgets in TAOM.Client:

- `EncyclopediaOverlayWidget` is the bar movie's root. In each late update it:
  - moves its own movie node to the last child of the layer root, which puts it above every page in render and hit-test order;
  - moves `BackdropWidget` (the dimmer, a child in the prefab) to index 0 and sizes it to `EventManager.PageSize`.

  The checks are two index reads when nothing changed. A click outside every page still reaches the dimmer, which closes the search results as before. On `OnDisconnectedFromRoot` it detaches the dimmer from the layer root and clears its cached node. This keeps the dimmer from outliving the bar.
- `EncyclopediaFrameAnchorWidget` places a top-aligned canvas against the shared frame:
  - The frame top is `max(0, min((h - 972) / 2, h - 1026))`, where `h` is the logical viewport height.
  - The canvas top is the frame top plus `FrameOffset`.
  - A positive `BottomClearance` raises the canvas to keep it that far above the viewport bottom, but never above frame top + 80 (just under the header).

  `EncyclopediaPageLayoutWidget` (the native detail movies that have no TAOM prefab, such as the concept and ship pages) calls the same `Offset` helper with `PageTop` 130 and `PageClearance` 78.

| Prefab element | FrameOffset | BottomClearance |
|---|---|---|
| `EncyclopediaFrame` (bar) | 0 | none |
| `HomeCanvas`, `ListCanvas` | 0 | none |
| Hero, unit, clan, kingdom, settlement and Realms title folios | 130 | 78 |
| Native detail movies through `EncyclopediaPageLayoutWidget` | 130 | 78 |
| `PageNavigation` | 956 | 22 |

Each prefab's `PositionYOffset` is the 1080 result, which shows until the first late update.

### Clicks stay on the encyclopedia

`GauntletLayer.HitTest()` asks only whether a widget that accepts events covers the cursor, and ScreenManager gives the mouse to the screen under a layer that reports no hit. The dimmer sits on the layer root, outside every movie, so while the bar root took no events a click on the dim area reached the screen under the encyclopedia: the Clan screen's Done button popped the Clan screen and finalized the encyclopedia layer, and vanilla `EncyclopediaData.OnTick` then threw a NullReferenceException every frame in `GauntletLayer.IsFocusedOnInput` (the layer's `UIContext` was null). The bar root now accepts events and covers the viewport, so the layer reports a hit everywhere. Gauntlet hands each event to the first widget under the cursor whose preview accepts it, children before parents, and the root is above every page, so `EncyclopediaOverlayWidget` returns false from the press, release, alternate press, alternate release and mouse move previews. The header buttons, the pages and the dimmer under it get those events as before. Scroll, drag, drop and right stick previews are false by default.

### Component Diagram

```
EncyclopediaData (native)
   |-- loads TAOMEncyclopediaBar movie  --> EncyclopediaOverlayWidget (root)
   |                                          |-- keeps its node last on the layer root
   |                                          |-- keeps EncyclopediaBackdrop at index 0
   |-- loads page movie --> EncyclopediaFrameAnchorWidget / EncyclopediaPageLayoutWidget
                              (canvas top from the shared frame)
```

## Configuration

None. The offsets are prefab attributes listed above.

## Key Files

| File | Purpose |
|------|---------|
| `Client/Features/FactionUI/Campaign/EncyclopediaOverlayWidget.cs` | Layer order, the dimmer, and the root that passes clicks on |
| `Client/Features/FactionUI/Campaign/EncyclopediaFrameAnchorWidget.cs` | Frame math and canvas placement |
| `Client/Features/FactionUI/Campaign/EncyclopediaPageLayoutWidget.cs` | Native detail page canvas (`IsDetailMovie`), same anchor |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaBar.xml` | Overlay root, dimmer, frame, search results |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaPageNavigation.xml` | Navigation row anchor |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopedia{Home,ItemList,HeroPage,UnitPage,ClanPage,FactionPage,SettlementPage}.xml` | Page canvas anchors |
| `Main/_Module/GUI/PreFabs/Realms/RealmsEncyclopediaTitlePage.xml` | Title folio anchor (only the folio element) |

## Dependencies

- Gauntlet `EventManager.Root`, `EventManager.PageSize` and `Widget._inverseScaleToUse`.
- `EventManager.PageSize` is `System.Numerics.Vector2` from the game's `System.Numerics.Vectors.dll`, not the net472 reference assembly. The widgets read `.X` and `.Y` and never name the type.

## Tests

- `TAOM.Tests/Features/FactionUI/EncyclopediaFrameAnchorWidgetTests.cs`: the frame top, the shipped 1080 coordinates, full-height and short viewports, 938 as the shortest height with no overlap, a NaN clearance, and the physical-to-logical conversion before and after the first layout.
- `TAOM.Tests/Features/FactionUI/EncyclopediaOverlayPrefabTests.cs`: the bar root (accepts events, passes every mouse event on) and its dimmer, the frame, list, detail page and navigation anchors.

## How to verify in game

**Preconditions:** any campaign; deploy TAOM.Client.dll and the prefabs listed above; restart the game.

1. Open the encyclopedia on a settlement page and type three letters in search. The results must show above the folio, and a click on a result must open that page (a person must look).
2. Click outside every page. The results must close.
3. Go back and forward several times between pages. The header buttons must keep working.
4. Repeat at UI scales that give logical heights 900, 1080, 1280 and 1440, and on an ultrawide screen.
5. With a gamepad, step through the header and the page. The focus order is not yet verified.
6. Open the encyclopedia from the Clan screen and click the dim area above the Clan screen's Done button. Nothing must happen under the encyclopedia, and the game must not crash. Over the map, a click on the dim area must not move the party.

## Known limits

- Below a 938 logical height the 780 tall folio, the header and the navigation row cannot all fit. The folio then runs under the navigation row, by 30 at 900. No content is removed, and the navigation row stays on top and clickable.
- The search results keep their shipped 52.2 height (about two rows, then the list scrolls). This fix changes only their stacking order.

## Changelog

- 2026-11-14: header and search results kept above every encyclopedia page; pages anchored to the shared frame (#1212).
- 2026-11-17: clicks on the themed encyclopedia no longer reach the screen under it; the bar root accepts events and passes them on.

## GitHub Issue

- **Issue:** [#1212](https://github.com/haterade22/TAOM/issues/1212): detail panels cover the encyclopedia header and search dropdown
- **Status:** Open
