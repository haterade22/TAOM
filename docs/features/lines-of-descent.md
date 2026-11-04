# Lines of Descent

## Overview

Lines of Descent shows a hero's dynasty as a tree. A **Family Tree** button on the clan screen's Members tab and on the encyclopedia hero page opens a popup that draws the head of the selected hero's line and every listed descendant under it, with couples side by side and a relation label on each card ("Husband of Z, daughter of W"). The same labels replace the one-word relation text on the selected member's panel of the clan screen. It is read-only: no Harmony patch, no campaign behavior, nothing saved.

## Why This Exists

- **Vanilla behavior:** the encyclopedia hero page lists a hero's immediate family as a flat grid, and the clan member list prints one word under each name ("Son", "Cousin" or nothing). Neither shows how a grandchild, an in-law or a former spouse connects to anyone, and a family spanning several generations cannot be seen at once.
- **TAOM requirement:** Middle-earth lords are dynasties (Gondor's stewards, the houses of Rohan, the elven and dwarven lines that run for centuries). A player should be able to see a line from its head down, and read how each card relates to the hero they are looking at.
- **Without this feature:** the player pieces a family together page by page through the encyclopedia.

## What the player sees

- **Two entry points.** On the clan screen's Members tab, a 180 by 40 **Family Tree** button sits at the end of the selected member's "Last Seen Location" row. It shows for any selected member. On the encyclopedia hero page the same button sits between the Family divider and the family grid, and shows only when the page lists any family (`Family.Count > 0`; vanilla fills that list only while the hero's information is not hidden).
- **The popup.** A framed full-screen popup titled "The Line of {root}", with a dimmed backdrop that holds the mouse, a close button, and a scroll area that pans by dragging when the tree is wider than the view; the wheel scrolls it, Shift for sideways. The selected hero's card carries a selection border.
- **Cards.** Each card is vanilla's encyclopedia family card (portrait, name) with the relation label under the name. The selected hero has no label. A hero already drawn higher up shows "See above" in place of a label.
- **Card click.** Clicking a card follows vanilla's link and opens that hero's encyclopedia page. The page change closes the popup.
- **Clan member list.** On the **selected** member's panel (a fixed 500 by 30 box, not under every row of the list), the vanilla relation word is replaced by the chain, for example "Husband of Z, daughter of W"; a long chain may wrap. Where no path to the main hero exists within the hop cap, vanilla's text is left alone. The main hero's own row is not touched.

## Architecture

### Design Challenge

The popup needs the whole family graph, but the engine's family lists are raw: `Father` and `Mother` setters append to the parent's `Children` without a duplicate check, and `Spouse` appends to `ExSpouses` on every change. Vanilla's tree widget draws its connecting lines as list items are added, so the view model tree must be complete before the movie loads. The UI types are also client-only (UIExtenderEx, `GauntletLayer`), so they cannot live in the server-safe core (ADR-013).

### Solution Approach

- **Core (`Main/`):** `FamilyTreeService` is pure logic over `IHeroFamilyAdapter`. It finds the root, builds the tree and phrases each relation. It holds no state. `HeroFamilyAdapter` resolves heroes by string id through `Hero.Find`, returns the engine's lists raw (the service cleans them) and never caches.
- **Client (`Client/`):** `FamilyTreePopup` opens one popup at a time on the screen that is on top. `FamilyTreeLayer` is a `GauntletLayer`. `FamilyTreePopupVM`, `FamilyTreeNodeVM` and `FamilyTreeCardVM` are the data sources. Cards are vanilla's `EncyclopediaFamilyMemberVM` built with no base hero (which skips vanilla's own relation call), with the role text set afterwards.
- **No Harmony.** Both entry points are UIExtenderEx view-model mixins and prefab extensions. The module registers no behavior, `OwnsSaveData` is false, and the tree is rebuilt from the engine's lists each time it opens.

### The root rule

The root is found by climbing from the selected hero one parent at a time. A parent is a candidate only if they exist, are listed in the encyclopedia, have a clan and have not been visited. Of the father and mother, the one with the better tier wins; **the father wins a tie**. The climb stops when no candidate is left or after **32 steps** (`FindRoot`). The tree itself climbs at most **11 steps** (`MaxGenerations - 1`), so the selected hero always falls inside the 12 drawn generations. With no candidate at all, the root is the selected hero.

| Tier | Parent is |
|------|-----------|
| 1 | a kingdom leader |
| 2 | a member of the ruling clan of their kingdom |
| 3 | a clan leader in a kingdom |
| 4 | any other member of a kingdom |
| 5 | a minor faction leader |
| 6 | a minor faction member |
| 7 | a clan leader |
| 8 | any other parent with a clan |

Tiers 5 and 6 apply only to a hero whose clan is a minor faction and who is not in a kingdom. A parent with no clan, or one the encyclopedia does not list, is never a candidate, so the climb ends at the child.

### The tree rules

- **Children only.** The tree goes down from the root. Aunts, uncles and the root's own siblings are not drawn.
- **Couple row.** Each node is one row: the hero, the current spouse, then former spouses in the engine's `ExSpouses` order. Duplicates, the hero and the current spouse are removed from the former list. Unlisted heroes are skipped.
- **Children.** Taken once each, sorted by birth day then string id. A child is kept only if its father or mother is this hero, so a stale entry in `Children` is dropped.
- **"See above".** The service tracks the heroes already drawn (as a node hero or as a spouse) apart from the ones expanded (a node). A hero's second and later card anywhere in the tree is a single "See above" card with no children and does not count against the card cap; each branch expands once. Every hero therefore has exactly one real card, and `CardCount` counts each once.
- **Caps.** At most **12 generations** and **300 cards**. A couple row is admitted whole (the hero plus each spouse not drawn yet) only when it fits in the remaining budget; otherwise that row and the later children go into the parent's "N more not shown" card. The count is per parent, so several such cards can appear.
- **The selected hero is always drawn.** The root-to-selected line is drawn first, before any cap applies, and placed in its birth-order slot under its parent. A link the engine's child list does not confirm ends that line early (not seen in practice; the engine keeps the two in step).

### The relation labels

One breadth-first search from the selected hero labels every card. The edges are taken in a fixed order: father, mother, current spouse, children (oldest first, ties by id, as the tree draws them), siblings, then former spouses. A child edge exists only when the child's own father or mother is that hero, and a sibling edge only when the two share a current parent, so a stale entry in a parent's child list is no relation. Unlisted heroes are skipped, as in the tree. The first path to reach a hero keeps it, so equal-length paths always resolve the same way. The search expands no hero at 6 hops and stops at 5,000 visited, checked before each hero is added. A hero beyond the cap, or one the search never reaches, gets "Kin of {selected hero}" in the tree. The clan list treats the same case as "no path" and keeps vanilla's text.

Each step is a relation word chosen by the gender of the hero being described, pointing at the next hero: the path from a card to the selected hero reads "Husband of Z, daughter of W". The last step of a child-to-parent chain uses the two-parent form ("Daughter of A and B") when both parents are known; middle steps name one parent. There are ten words: father, mother, son, daughter, husband, wife, brother, sister, former husband, former wife. There are no elder or younger words.

**String keys.** All 18 rows are `taom_lod_*` and are inline `{=KEY}default` literals in `Main/Features/LinesOfDescent/UI/FamilyTreeTexts.cs`:

| Key | English |
|-----|---------|
| `taom_lod_button` | Family Tree |
| `taom_lod_title` | The Line of {ROOT.NAME} |
| `taom_lod_kin` | Kin of {HERO.NAME} |
| `taom_lod_more` | {COUNT} more not shown |
| `taom_lod_repeat` | See above |
| `taom_lod_chain` | {FIRST}, {REST} |
| `taom_lod_link` | {RELATION} of {HERO.NAME} |
| `taom_lod_link_parents` | {RELATION} of {FATHER.NAME} and {MOTHER.NAME} |
| `taom_lod_rel_father`, `_mother`, `_son`, `_daughter`, `_husband`, `_wife`, `_brother`, `_sister` | the word, lower case |
| `taom_lod_rel_ex_husband`, `taom_lod_rel_ex_wife` | former husband, former wife |

**How chains compose.** Each link is its own `TextObject` (`taom_lod_link`) that names its own target through a character tag, so gender and grammar attach to the name. The chain nests pieces as `{FIRST}, {REST}`: a two-link phrase is `chain(link1, link2)`, a three-link phrase is `chain(link1, chain(link2, link3))`. A translation can reorder `{FIRST}` and `{REST}`, or reshape one link, without touching code. The relation word is lower case in the table; the code upper-cases the first letter of the phrase's first link only, as vanilla does.

## Long-lived races and the Nazgul

The code has no race-specific branch (a search of the feature's code for race or age handling found none). Two consequences follow from other features:

- **Long-lived lines.** Under the [Race Age System](race-age-system.md) elves and dwarves live far longer than vanilla heroes, so one living line can span many more generations than a vanilla clan. This is what the 12-generation and 300-card caps and the "N more not shown" card are for.
- **Wraiths.** The nine Ringwraiths have no spouse, parents or children ([nazgul-family.md](nazgul-family.md)). Their encyclopedia pages list no family, so the hero-page button is hidden. On the clan screen the button still shows for a wraith, and the popup draws a single card. [Unconfirmed in game.]

## Configuration

None. There is no config file, no MCM setting and no toggle. The caps are constants in `FamilyTreeService` (`MaxClimbSteps` 32, `MaxGenerations` 12, `MaxCards` 300) and `RelationPathFinder` (`HopCap` 6, `VisitedCap` 5000).

## Hooks

| Hook | Where | Anchor or method |
|------|-------|------------------|
| View-model mixin | `ClanLordItemFamilyMixin` on `ClanLordItemVM`, method `UpdateProperties` | Adds the button command and rewrites `RelationToMainHeroText`. UIExtenderEx 2.13.3 replays the view model constructor's first `UpdateProperties` on the mixin as `OnRefresh`, so the mixin constructor does not call it. |
| View-model mixin | `EncyclopediaHeroFamilyTreeMixin` on `EncyclopediaHeroPageVM`, method `Refresh` | Sets `HasFamilyTree` from the page's `Family` list. The hero is the page's `Obj`. |
| Prefab extension | `ClanMembersFamilyTreePrefab` on `ClanMembers` | `descendant::ListPanel[@Id='LastSeenLocationParent']/Children`, `InsertType.Child`, index `int.MaxValue` (appended as the last child). Two nodes: a `NavigationScopeTargeter` and the button (`GamepadNavigationIndex` 0), the pattern of the vanilla Show On Map pair in the same row. |
| Prefab extension | `EncyclopediaHeroFamilyTreePrefab` on `EncyclopediaHeroPage` | `descendant::NavigationScopeTargeter[@ScopeID='EncyclopediaHeroFamilyContentScope']`, `InsertType.Prepend` (siblings just before it: the button's scope targeter, then the button with `GamepadNavigationIndex` 0). The anchor exists once in TAOM's `EncyclopediaHeroPage.xml`; Lines of Descent does not edit that shared prefab. |

Both buttons use the brushes `Popup.Button.Base` and `Popup.Button.Text`, and so does the Messengers hero-page button (#989 replaced its two undeclared brush names; `PrefabExtensionBrushTests` now checks every brush-typed attribute in a PrefabExtension against the brushes the engine loads for TAOM's module set).

### Popup layer and close triggers

The popup is a `GauntletLayer` of order **320**, above the encyclopedia bar (310) and the clan screen (1). It takes input restrictions and focus, and registers `GenericPanelGameKeyCategory` so Escape (`Exit`) is readable. Every way out ends in one idempotent `Close`, checked in the layer's `Tick` on the UI thread so the movie is never released from inside its own update:

1. The close button (sets the close flag; the same tick closes it).
2. Escape.
3. The host screen deactivates (a flag set in `OnDeactivate` closes it on the next tick).
4. Opened from the clan screen and its Members tab is no longer selected (a tab change).
5. The encyclopedia changes page or closes (`EncyclopediaPageChangedEvent`), which is also what a card click causes.
6. The host finalizing under the layer (`OnFinalize`).

There is no "host is not on top" check: a layer is never ticked while its host is not the top screen, and a screen pushed over the host deactivates it (trigger 3).

Opening is guarded: the layer joins the host before the view model and movie are built, so a throw while building or showing the popup finds a layer `Close` can always remove. The throw is logged, `IsOpen` is cleared before `Close` runs, the teardown reports to the controller in a `finally`, and the click is the only thing lost. Each mixin logs a throw once per row or page.

## Co-op

Read-only UI with no campaign behavior and nothing saved, so it is not gated. See "Deliberately NOT gated" in [coop-interop.md](coop-interop.md). The tree reads the engine's family lists, which every peer already holds.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LinesOfDescent/FamilyTreeModule.cs` | Feature module: registers the service and adapter; no behaviors |
| `Main/Features/LinesOfDescent/IFamilyTreeService.cs`, `FamilyTreeService.cs` | `FindRoot`, `Build`, `Describe`; the caps |
| `Main/Features/LinesOfDescent/RelationPathFinder.cs` | The breadth-first search and phrase building |
| `Main/Features/LinesOfDescent/FamilyTreeTypes.cs` | `RootTier`, `RelationKind`, `RelationPhrase`, `FamilyTreeNode`, `FamilyTreeCard` |
| `Main/Features/LinesOfDescent/UI/FamilyTreeTexts.cs` | The `taom_lod_*` literals and phrase rendering |
| `Main/Adapters/IHeroFamilyAdapter.cs`, `HeroFamilyAdapter.cs` | Engine boundary (`Hero.Find` by string id) |
| `Client/Features/LinesOfDescent/UI/FamilyTreePopup.cs`, `FamilyTreeLayer.cs` | Controller and layer |
| `Client/Features/LinesOfDescent/UI/FamilyTreePopupVM.cs`, `FamilyTreeNodeVM.cs` | Popup, node and card view models |
| `Client/Features/LinesOfDescent/UI/ClanLordItemFamilyMixin.cs`, `ClanMembersFamilyTreePrefab.cs` | Clan screen hooks |
| `Client/Features/LinesOfDescent/UI/EncyclopediaHeroFamilyTreeMixin.cs`, `EncyclopediaHeroFamilyTreePrefab.cs` | Hero page hooks |
| `Main/_Module/GUI/PreFabs/LinesOfDescent/TaomFamilyTreePopup.xml`, `TaomFamilyTreeNode.xml` | Popup and node prefabs |
| `Main/Composition/FeatureModules.cs`, `Client/TaomClientComposition.cs` | One registration line each |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 18 `taom_lod_*` rows |

## Dependencies

- `IHeroFamilyAdapter` (Adapters): the family graph, listing test and clan, kingdom and faction facts.
- `IFamilyTreeService` (Core): injected into the popup's constructor by the container, and resolved once, in its constructor, by the clan mixin (the hero-page mixin captures the popup the same way).
- UIExtenderEx and vanilla encyclopedia widgets (`EncyclopediaTroopScrollablePanel`, `EncyclopediaUnitTreeNodeItemBrushWidget`, `EncyclopediaSubPageElement`, the `Encyclopedia.CharacterTree.Line` brush).

## Tests

- `TAOM.Tests/Features/LinesOfDescent/FamilyTreeServiceTests.cs`: 41 tests on the root tiers and climb, the tree rules and caps, and the relation paths (words by gender, two-parent form, shortest path, hop cap, cycles, unreachable).
- `TAOM.Tests/Features/LinesOfDescent/FamilyTreeTextsTests.cs`: 6 tests on phrase rendering.
- `TAOM.Tests/Features/LinesOfDescent/FamilyTreeWiringTests.cs`: 5 tests on module registration and string keys.
- `TAOM.Tests/Features/LinesOfDescent/FamilyTreeUiWiringTests.cs`: 16 tests pinning the prefab anchors, mixin method names, scrollbar names, bindings, gamepad scopes, the close and failed-open sequence, that no mixin constructor calls `OnRefresh`, and that no mixin resolves from the container outside its constructor.
- `TAOM.Tests/Features/LinesOfDescent/FakeFamilyGraph.cs`: the in-memory `IHeroFamilyAdapter` the service tests use.

## Known limits and risks

These need the in-game check below; none is confirmed.

- **Button placement.** The clan button is appended to the "Last Seen Location" row, whose width and wrap were not seen in game. Nothing in the engine indexes that row's children, but the look is unchecked.
- **Tree line geometry.** The couple row is a fixed 176 tall and the lines and child row are offset to match. A wide couple row or a four-line label may misalign the connecting lines.
- **Escape.** Escape cannot reach the clan screen or the encyclopedia while the popup has focus (`ScreenManager` gives keys to the focused layer only). Kept on the in-game list as a check.
- **Card click overlap.** A card click opens the encyclopedia page and the popup closes on a following tick; an overlap of a frame is unlikely and is not claimed either way.
- **Sprite category.** The popup loads `ui_encyclopedia` defensively; the campaign map's encyclopedia view already loads it, and the load is idempotent, so the popup never unloads it.
- **Hidden information.** With "Show all encyclopedia characters" off (`TaomSettings`, on by default), the popup still shows relatives the player has not met; vanilla hides only the hero page's family list.
- **Clan screen cost.** Each clan row runs one relation search through linear `Hero.Find` lookups. Not timed; to be measured in game on a late save with large families.
- **Clan label.** The relation text shows on the selected member's panel only, in a fixed 500 by 30 box, so a long chain may wrap.
- **Role text.** Labels wrap inside a 186 by 76 box. The row height assumes up to four lines; a longer chain (a translation, or a long name) has not been seen in game.
- **Background.** The popup uses the shared `Standard.Background` inside a fixed 1560 by 900 frame; its look is unchecked.
- **Wide families.** Beyond the caps the player sees "N more not shown" with no way to expand it.

## In-game checklist

1. Open **Family Tree** from the clan screen's Members tab and from an encyclopedia hero page. The page button should be hidden on a hero whose family is hidden.
2. A Gondor line: the root is the Steward or the king's line, the title reads "The Line of ...", and the selected hero has the border.
3. A hero with several marriages: current spouse first, then former spouses in order, each marked "Former husband/wife of ...".
4. A large dynasty: scroll both ways, drag with the mouse, and check the connecting lines at every depth and the "N more not shown" card.
5. Close on each trigger: the close button, Escape, a clan tab change, an encyclopedia page change, closing the encyclopedia, and a card click. After each, the screen underneath should respond normally and a second open should work.
6. The clan member list: relation text reads as a chain for relatives, vanilla's text stays for anyone with no path, and the main hero's row is unchanged.
7. A Ringwraith on the clan screen (single card, no errors) and an elf or dwarf line (caps behave).
8. A married hero with exactly one child: the line is centred under the couple.
9. A row with several spouses.
10. A three-step chain in the clan panel: it reads and wraps acceptably.
11. A two-line hero name on a card.
12. The clan row's width with the new button; collapsing the Family divider leaves the button visible (vanilla collapses only the family grid).
13. Time the clan screen opening on a late save with large families.
14. Gamepad focus reaches both buttons.
15. With the popup open, Escape closes only the popup, and the clan screen or encyclopedia underneath still responds.

## Translation

The 18 `taom_lod_*` rows are seeded in English in all language files. The paid translation run is tracked in a backlog issue; until it lands, every language shows the English text.

## Changelog

- 2026-10-23: added in v2.0.35 (#905).

## GitHub Issue

- **Issue:** #905, Lines of Descent
- **Status:** Open
