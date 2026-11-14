# Encyclopedia list previews

## Overview

The themed encyclopedia list screen (`TAOMEncyclopediaItemList`) shows a preview of the selected
entry in its right-hand 330 by 590 portrait frame, above the name, subtitle and Open entry button.
Heroes and troops get a live character tableau, kingdoms and clans their banner pennant, and
settlements and concepts a picture or the category illustration.

## Why This Exists

- **Vanilla behavior:** the native hero and unit detail pages build the character view models this
  list copies (`EncyclopediaUnitPageVM` uses `CharacterViewModel.FillFrom(troop, -1, null)`).
- **TAOM requirement:** the themed list shows what the selected entry looks like before it is opened.
- **Without this feature:** the troops list showed the static `ec_window_troops` illustration for every
  troop, and the hero portrait started 105 px below the frame top, leaving an empty band above it.

## Architecture

### Design Challenge

The base `CharacterViewModel` has no `IsDead`; only `HeroViewModel` does, and the encyclopedia
tableau widget (`EncyclopediaCharacterTableauWidget`) adds only that property to the plain
`CharacterTableauWidget` to desaturate fallen heroes. One widget bound to either view model would leave
an unresolved `@IsDead` binding on troops. The tableau camera uses a fixed 45 degree vertical field
of view, and no TAOM widget property moves it; global tableau camera patches are out of scope. The
camera framing comes from the view model's stance instead: `CharacterTableau.FirstTimeInit` starts
from the inventory scene's camera frame, `AdjustCharacterForStanceIndex` leaves that camera alone for
`StanceTypes.None` (it only sets idle actions), and `EmphasizeFace` resets it, then elevates it by 2
and advances it by 0.5 toward the face.

### Solution Approach

`EncyclopediaListScreenVM.Select` routes by category through `UsesCharacterPreview` (heroes, troops)
and `UsesBannerPreview` (kingdoms, clans). The static illustration shows only when neither preview is
active, so no static art draws under a live render.

| Category | Source | View model | Widget |
|---|---|---|---|
| Heroes | `EncyclopediaListItemVM.Object` as `Hero` | `HeroViewModel(None)`, `FillFrom(hero, -1, hero.IsNotable, true)`; horse, harness and helmet cleared | `EncyclopediaCharacterTableauWidget`, keeps `IsDead` |
| Hidden or unknown hero | same | tableau disabled | the `Encyclopedia\hero_silhouette` outline, subtitle empty |
| Troops | `EncyclopediaListItemVM.Object` as `CharacterObject` | `CharacterViewModel(None)`, `FillFrom(troop)`, the native unit page's fill call; horse and harness cleared, helmet kept | plain `CharacterTableauWidget` with only base view-model bindings |
| Troop the fill leaves empty (child age) | same | finalized at once | the `ec_window_troops` illustration |

`FillFrom` clones the character's equipment, so clearing the mount slots changes only the preview.
The troop subtitle is the troop's culture name, the hero subtitle the hero's culture name.

**Lifetime:** `ReleaseCharacterPreview` hides every render flag, disables each tableau and calls
`OnFinalize` on it, on every selection change and from `DisposePresentation` (the list widget's
disconnect and the view model's `OnFinalize`). A category change builds a new list view model, so the
old one is disposed the same way.

**Geometry** (inside the 330 by 590 frame, offsets from its top):

| Element | Width | Height | Top | Bottom |
|---|---|---|---|---|
| `CharacterRenderClip`, the visible portrait bounds | 290 | 540 | 35 | 575 |
| `CharacterRenderScale`, shared render host inside the clip (centred, 15 lower; 29 past each side) | 348 | 648 | -4 | 644 |
| `HeroSilhouette` (245 by 410 scaled uniformly) | 287 | 480 | 80 | 560 |

Both tableaux fill one shared host, `CharacterRenderScale`, exactly 1.2 times the clip on each axis.
The render target therefore keeps the clip's aspect and nothing is stretched on one axis; under the
fixed vertical field of view the figure is projected 1.2 times larger. The host is centred in the
clip and moved 15 px down, and the clip trims the overflow, so the visible portrait keeps the 290 by
540 bounds. A taller target alone would only draw the figure larger under the fixed
vertical field of view, so both list previews build their view model with `StanceTypes.None`: the
native full-body inventory camera, the stance `SPInventoryVM` gives its hero. This is local to the two
list view models; the detail pages and every other tableau keep their own stances. How much head
clearance the inventory camera leaves on the 348 by 648 host once the clip trims it, and how much of the body it shows for tall or
short races, is the in-game check.

### Hero page Allies and Enemies grids

A separate fix on the hero detail page (`TAOMEncyclopediaHeroPage`). The `AlliesGrid` and
`EnemiesGrid` widgets sit in a 288 px viewport with 144 px cells, 3 columns. Without `RowCount`,
the grid layout defaults to 3 rows and splits the 288 px into 96 px cells, so each name runs under
the next row's portraits. Both grids now set `RowCount="2"`, which gives two 144 px rows.

## Key Files

| File | Purpose |
|------|---------|
| `Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs` | Selection routing, hero and troop view models, release |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaItemList.xml` | The frame, raised viewport and both tableaux |
| `Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs` | Calls `Synchronize` each frame and `DisposePresentation` on disconnect |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaHeroPage.xml` | Hero detail page; `RowCount="2"` on the Allies and Enemies grids |

## Tests

- `TAOM.Tests/Features/FactionUI/EncyclopediaItemListCharacterPreviewTests.cs`: category routing,
  the viewport and outline geometry, both tableaux in one clip, the troop tableau without `IsDead`,
  and (`RequiresGame`) every preview binding resolving on the view model of its scope.

## How to verify in game

**Preconditions:** a campaign with the TAOM module set; the result is visual, so only a person can
judge it.

1. Open the encyclopedia, Heroes. Select a known hero: the portrait starts near the frame top and its
   bottom sits where it did before. Select a hero the player does not know: the outline shows, with no
   render behind it.
2. Open Troops. Select several troops, mounted ones included: each shows its own race, equipment and
   helmet, standing, with no horse and no static illustration under it. The subtitle is the troop's
   culture.
3. Switch categories and close the encyclopedia: no stale figure stays on screen.
4. Open a hero page with four or more allies or enemies: each name sits under its own portrait,
   clear of the next row.

## Changelog

- 2026-11-08: the hero viewport is raised to 290 by 540 at top 35; the troops list shows a live tableau
  of the selected troop instead of the static illustration (#1094).
- 2026-11-14: both list previews use the inventory stance on a shared host 1.2 times the clip; the
  hero page Allies and Enemies grids set `RowCount="2"` so names no longer overlap the next row.

## GitHub Issue

- **Issue:** [#1094](https://github.com/haterade22/TAOM/issues/1094)
- **Status:** Open
