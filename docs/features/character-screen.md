# Character page and Career screen

## Overview

The campaign character page (vanilla `CharacterDeveloper`) is redrawn in TAOM's stone and valley style,
and the Career screen it opens shares that look. Both reuse the native view models, fonts, buttons and
existing sprites: no rule, skill, perk or career mechanic changes.

## Why This Exists

- **Vanilla behavior:** the character page is the stock Calradian screen, and TAOM's Career screen was a
  separate full-screen grid that dropped the player back on the map when closed.
- **TAOM requirement:** one look from the faction screen through the character record to the career
  tree, with the open perk tiers and spendable points visible at a glance.
- **Without this feature:** the page breaks the themed front end, and closing the Career screen loses
  the character page the player came from.

## Architecture

### Design Challenge

The native `CharacterDeveloperVM` owns every action (add point, add focus, choose perk, open career).
The redesign must keep those bindings and their disabled states while drawing a different layout, and
the valley background must exist before the movie binds it by name.

### Solution Approach

- **Movie swap:** `FrontEndMovieService` loads `TAOMCharacterDeveloper` in place of `CharacterDeveloper`
  and holds the character creation chrome plus the faction art group. Before the movie binds, it
  registers only `FactionScreenArt.DefaultBackground` (`fs_bg_default`), and the hold is released with
  the movie. `CareerScreen` keeps its own movie name with the same holds.
- **View model:** `CampaignPresentation` wraps the native VM in `CharacterScreenVM` (TAOM.Client). Each
  row VM exposes the native one as `Native`, so the prefab binds the vanilla commands directly.
- **Backdrop:** `CharacterBackdropWidget` centres the whole valley image without cropping or stretching
  it, and never enlarges it past 1.25 physical pixels per source pixel. Dark fades cover the edges.
- **Spend indicators:** an attribute tab shows a gold-rimmed `+` while the native `CanAddPoint` is
  true. A skill tab shows the same `+` while `CharacterOpenTiers` finds a perk tier selectable now.
- **Perk progression:** `CharacterSkillProgress` places the level marker and the filled rail on the
  perk tier columns. `CharacterScreenWidget` draws each perk medallion as chosen, available or locked.
  The page also lists the hero's traits inline, or `taom_char_no_traits` when there are none.
- **Career screen:** a modal layout (`CareerScreen.xml` with the `CareerChoiceGroup` item template). Each
  group header states its pick rule (`SelectionRuleText`: "Choose one" or "Choose N"). The sidebar gets
  the Passive and Career Ability headings.
- **Return to the character page:** the career button (Patch27) closes the character page, then opens
  the Career screen with `returnToCharacter: true`. An explicit close (Done, Close, Escape) pops the
  Career state and pushes a fresh `CharacterDeveloperState`. An engine finalize (leaving the campaign)
  never does. A switch or retrain inquiry answered after the screen closed changes nothing.

### Component Diagram

```
CharacterDeveloperVM (native)            CareerScreenGameState (ReturnToCharacter)
        |                                         |
CampaignPresentation -> CharacterScreenVM   GauntletCareerScreen -> CareerScreenVM
        |        (CharacterOpenTiers,             |                     |
        |         CharacterSkillProgress)         |          CareerChoiceGroupObjectVM
TAOMCharacterDeveloper.xml                  CareerScreen.xml + CareerChoiceGroup.xml
        \_______ FrontEndMovieService holds: chrome + faction art (fs_bg_default) ______/
```

## Configuration

No config file or MCM option. The visual tokens are brushes:

| File | Holds |
|------|-------|
| `Main/_Module/GUI/Brushes/TAOMCharacterDeveloper.xml` | `TAOM.Character.*` brushes for the page and the badges |
| `Main/_Module/GUI/Brushes/CareerSystem.xml` | Career screen brushes; `FrontEndSpriteService` re-reads this file with the chrome group so it resolves the front-end fonts |

## Key Files

| File | Purpose |
|------|---------|
| `Client/Features/FactionUI/Campaign/CharacterScreenVM.cs` | Page VM over the native one: tabs, traits, perk rows, progress |
| `Client/Features/FactionUI/Campaign/CharacterScreenWidget.cs` | Perk medallion looks and the perk row scroll |
| `Client/Features/FactionUI/Campaign/CharacterBackdropWidget.cs` | Contained, capped valley backdrop and its fades |
| `Client/Features/FactionUI/Campaign/CharacterOpenTiers.cs` | Open perk tier counts per skill and attribute |
| `Client/Features/FactionUI/Campaign/CharacterSkillProgress.cs` | Marker, rail and scroll geometry on the tier columns |
| `Main/Features/FactionUI/Menus/FrontEndMovieService.cs` | Movie swap and image holds for both screens |
| `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs` | Career screen lifecycle and the return to the character page |
| `Main/Features/CareerSystem/UI/CareerScreenGameState.cs` | `ReturnToCharacter` flag |
| `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs` | `SelectionRuleText` |
| `Main/Features/CareerSystem/UI/CareerScreenVM.cs` | Passive, Career Ability and Tier labels |
| `Main/_Module/GUI/PreFabs/FactionUI/TAOMCharacterDeveloper.xml` | Character page prefab |
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml`, `CareerChoiceGroup.xml` | Career screen prefabs |
| `Main/_Module/ModuleData/taom_module_strings.xml` (`taom_char_*`), `taom_career_strings.xml` | English strings |

## Dependencies

- `FrontEndSpriteService` and `FactionScreenArt` (FactionUI): image groups and the valley image.
- The native `CharacterDeveloperVM`, `CharacterDeveloperState` and Gauntlet widgets; no new adapter.
- [faction-ui.md](faction-ui.md) for the front end, [career-system.md](career-system.md) for careers.

## Tests

- `TAOM.Tests/Features/FactionUI/CharacterScreenPrefabTests.cs`: prefab structure, bindings, badge
  geometry, English registration of the page's keys.
- `TAOM.Tests/Features/FactionUI/CharacterOpenTiersTests.cs`, `CharacterSkillProgressTests.cs`,
  `CharacterBackdropWidgetTests.cs`: open tier counts, progress geometry, backdrop containment.
- `TAOM.Tests/Features/FactionUI/FrontEndMovieServiceTests.cs`: the two movie holds and the valley
  release.
- `TAOM.Tests/Features/CareerSystem/CareerScreenPrefabBindingTests.cs`, `CareerButtonClickTests.cs`,
  `CareerChoiceGroupObjectVMTests.cs`: Career bindings, the button path and the pick rule text.

The new keys have English rows only. The language rows come with the project lead's translation pass,
so `EveryLanguage_DeclaresARowForEveryEnglishKey` lists them until then.

## How to verify in game

**Preconditions:** a campaign save; no MCM option.

1. Open the character page. A person checks the valley backdrop, the stone header, the traits and the
   perk rail. No log line proves the look.
2. With an unspent attribute point, the `+` shows on each attribute tab that can take it. With a perk
   tier selectable now, the `+` shows on that skill's tab. Spend the point or choose the perk: the `+`
   goes.
3. Click Career. The log shows `[Check] id=taom.career-screen.open result=pass source=button`.
4. Press Escape on the Career screen: the character page opens again. Close the character page as
   usual: the map returns.

## Changelog

- 2026-11-14: character page redesign and Career modal published together (attribute and skill `+`
  indicators, inline traits, perk progression, return from Career to the character page).

## GitHub Issue

- **Issue:** none; the maintainer waived the issue for this work.
