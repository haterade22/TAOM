# Realms title tree

## Overview

The title tree popup shows a realm's whole hierarchy of feudal titles, opened from any title section of TAOM's
encyclopedia. The left of the modal draws the tree top down: the realm's pennant at the top centre, its great titles in
a row below, and further down each title's vassals. A chain of titles runs straight down as pennants; leaf vassals stack
as compact rows. On the right, past a thin gold divider, a details panel shows the selected title: its pennant, name,
holder and folded lordships, its liege and its subordinate titles. Clicking a title in the tree, or a liege or vassal in
the details, selects it; clicking the selected title's name closes the tree and opens that title's encyclopedia page.
Hovering a tree title shows its rank and holder. Part of [Realms](realms.md).

## Why This Exists

- **Vanilla behavior:** Bannerlord has no feudal titles and no title hierarchy view; the encyclopedia's unit tree
  (`EncyclopediaUnitTreeNodeItemBrushWidget`) draws troop upgrade trees top down.
- **TAOM requirement:** a realm such as Gondor carries a realm title, several great titles and many baronies and
  lordships. The first popup reused the unit tree in a fullscreen native frame, which clashed with TAOM's stone
  encyclopedia, and a click went straight to a title's page with no way to read the relations first. Issue #1255: a
  centred stone modal, not fullscreen, with the tree on the left and the selected title's details on the right.
- **Without this feature:** players cannot see who holds what under a realm, or which lands a vacant titular realm
  would gather once formed.

## Architecture

### Design Challenge

Gauntlet has no tree layout. A recursive prefab (the earlier `RealmsTitleTreeNode.xml`) can only stack items, so
connector lines and centring a parent on its vassals are out of its reach. The tree must also keep every title however
wide or deep the realm is, scroll inside its own area while the details stay put, and bring the selected title into
view.

### Solution Approach

- `TitleTreeCanvasLayout` (pure, no TaleWorlds types) places every card and connector segment on one canvas in pixels,
  cards in pre-order so each subtree is one run of indices. The realm's great titles, and any only vassal, are pennant
  nodes; other vassals with vassals of their own are pennant nodes too, and leaf vassals become compact rows in evened
  stacks of at most five. Each parent is centred over its vassals. A canvas smaller than the 1048 by 790 viewport is
  centred in it; a larger one grows and scrolls.
- Each connector segment records the run of cards below it, so `TitleTreeSegment.Lights` lights exactly the path from
  the realm down to the selected title.
- `TitleTreeCanvas.LiegeOf` gives no liege for the realm or for lands (titles drawn under a vacant titular realm without
  being its vassals). `SubordinatesOf` lists vassals in the tree's order and leaves lands out. Neither invents a
  relation the domain tree does not hold.
- `TitleTreePopupVM` turns the layout into flat `MBBindingList`s of cards and lines plus the canvas size, and keeps the
  selection: `Selected`, `Liege` (zero or one card), `Subordinates`, `HasLiege`, `HasSubordinates`, and
  `SelectionVersion`, which tells `RealmsTitleTreeScrollablePanel` to bring the new selection into view. The initial
  selection is the title the tree was opened at.
- `RealmsTitleTreePopup.xml` draws the cards with no box: a pennant (a second, brightened rim when selected) with its
  text below or beside it. The details panel binds the same card view models and scrolls on its own. Done sits under the
  stone as a sibling of the modal, so its clicks land.
- The modal reuses existing sprites and brushes only: `ec_panel_9` and `ec_header` stone, `fs_divider_thin`,
  `fs_diamond`, `TAOM.Encyclopedia.*` text brushes and `Close`, `TAOM.Character.Action` for Done, and the encyclopedia
  pennant stack.

### Component Diagram

```
TitleTree.Build (domain, unchanged)
        |
TitleTreeCanvasLayout.Build (cards, segments, canvas size; LiegeOf, SubordinatesOf)
        |
TitleTreePopupVM -> TitleCardVM / TitleTreeLineVM; Selected, Liege, Subordinates
        |
RealmsTitleTreePopup.xml in RealmsPopupLayer (order 320)
        |
RealmsTitleTreeScrollablePanel (scrolls the selected card into view)
```

## Configuration

None. The sizes are constants in `TitleTreeCanvasLayout`; the prefab's clip rect must match `ViewportWidth` and
`ViewportHeight`, which `TitleTreePrefabTests` checks.

## Key Files

| File | Purpose |
|------|---------|
| `Client/Features/Realms/Popups/TitleTreeCanvasLayout.cs` | Pure top-down layout: cards, lit connectors, pennant geometry, liege and vassal lookups |
| `Client/Features/Realms/Popups/TitleTreeVMs.cs` | `TitleTreePopupVM`, `TitleCardVM`, `TitleTreeLineVM` |
| `Client/Features/Realms/Popups/RealmsTitleTreeScrollablePanel.cs` | Scroll panel that centres the selected title on open and on each new selection |
| `Main/_Module/GUI/PreFabs/Realms/RealmsTitleTreePopup.xml` | The modal, the tree canvas templates, the details panel and the scrollbars |
| `Main/Features/Realms/Domain/Titles/TitleTree.cs` | `TitleTree.Build`: which titles the tree holds (unchanged) |

## Dependencies

- `RealmsPopupLayer` and `RealmsPopups`: open and close the popup layer.
- `TitleVisuals`: the title's banner and encyclopedia link.

## Tests

- `TAOM.Tests/Features/Realms/TitleTreeCanvasLayoutTests.cs`: every title placed once in pre-order, pennant nodes and
  compact rows by position in the tree, six leaves stacked three and three, vassals below their liege with no overlap,
  chains straight down and parents centred, small trees centred and large ones growing the canvas, the lit path to a
  selected title, the focus index, and the liege and subordinate rules for the realm and for lands.
- `TAOM.Tests/Features/Realms/TitleTreePrefabTests.cs` (`RequiresGame`): every binding and command names a member of
  the view model its scope resolves to (the four lists, the `Selected` scopes and the popup), the selected name alone
  opens the page, the pennant and text fade through `Brush.GlobalAlphaFactor`, text stacks title, holder and folded
  count, a tree card selects and hints, the clip rect matches the layout viewport with both scrollbars and follows
  `SelectionVersion`, and the native frame is gone.

## How to verify in game

**Preconditions:** a campaign at least one in-game hour old, so titles exist.

1. Open any title page in the encyclopedia and open its title tree. Pass (a person sees it): a centred stone modal with
   the dark backdrop around it, the realm at the top of the left area, its great titles below, gold lines joining each
   parent to its vassals, and the details panel on the right showing the title the tree was opened at.
2. Open the tree from a barony deep in a large realm (Gondor). Pass: that barony's pennant has the bright rim, its path
   from the realm is lit, and it is in view without scrolling.
3. Click another title in the tree, then its liege and a vassal in the details. Pass: each click moves the selection,
   the details follow, and the tree scrolls the new selection into view.
4. Select the realm, then a title with no vassals. Pass: no liege section for the realm, no subordinate section for the
   title without vassals.
5. Click the selected title's name in the details. Pass: the tree closes and the title's page opens.
6. Scroll the tree with the wheel, Shift and the wheel, and drag; scroll the details of a title with many vassals.
   Pass: each area scrolls on its own inside the modal.
7. Repeat step 11 of [realms.md](realms.md) "How to verify in game". Pass: the lands titles are dimmed and show no
   liege.

## Changelog

- 2026-11-14: redesigned as a centred stone modal with a top-down tree and a details panel for the selected title
  (#1255).

## GitHub Issue

- **Issue:** #1255: [Realms title tree redesign](https://github.com/haterade22/TAOM/issues/1255)
- **Status:** Closed (in-game check owed)
