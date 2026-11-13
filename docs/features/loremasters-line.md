# Loremaster's Line

## Overview

On the character screen, the selected skill's bar gets a red arrow and a red band that run from the level past which
learning is at its slowest to the end of the bar. Hovering the band shows a tooltip that names that level and what
learning is worth past it; the arrow above it is decoration. Vanilla's learning limit tooltip gains one line on
full-speed learning, only while the marker shows. An MCM switch (default on) turns it all off, which gives the vanilla
screen.

## Why This Exists

- **Vanilla behavior:** the bar shows the learning limit (the point past which learning slows) as a yellow indicator
  under the bar, but not where the slowing ends. A player can see a skill is over its limit without learning how far
  they can still gain.
- **TAOM requirement:** show where the rate stops falling, read from the game's own learning-rate model, so any model change
  is followed.
- **Without this feature:** players guess how much a skill can still be raised, or open each skill's tooltip and do the sum.

## Architecture

### Design Challenge

The floor is not stored anywhere. With the default model the rate is `1.25 x (1 + 0.4 x attribute + focus - (1 + 0.1 x n))`
once `n = skill - limit` is positive, floored at 0, so it reaches 0 at `limit + 4 x attribute + 10 x focus`. A model
override or a third-party mod can change that, so the line must come from the model, not from a copy of its formula.
The bar's pixel maths is private to `PerkSelectionBarWidget`, and a Harmony patch on a per-frame widget method was not
needed.

### Solution Approach

No Harmony patch, no static cache and no TAOM widget.

- **Calculator (core, pure):** `LearningFloorCalculator.Find(Func<int, float> rateAt)` probes skill 0 and 4096. A
  non-finite probe (mid-search included) gives none, and so does a curve that does not fall from skill 0 to skill 4096
  (flat, or rising: there is no slowest point to mark; the test is `!(full - floor > 1e-4)`). Otherwise it bisects (12
  steps, 14 calls in all) to the lowest skill within 1e-4 of the floor rate and returns the level, the floor rate and the
  full rate (the rate at skill 0). `BarLevels(maxLevel)` is the bar's width in levels (`MaxLevel + 25`, the one copy of
  `BarOverhang`), `IsOnBar` says whether the floor lies before the bar's end (`MaxLevel + 24` shows, `MaxLevel + 25`
  hides, and a bar of zero or negative width hides), and `BandLevels` is the span from the floor to the end of the bar.
  `PercentOfFull` rounds the floor against the full rate and clamps to 0..999 (a negative floor counts as 0; a full rate
  that is not positive and finite gives no answer). `CannotLearn` is vanilla's own rule (`SkillVM.CanLearnSkill`): the
  floor rounded to two decimals is not above 0, so NaN cannot learn. `ShownPercent` is the percent the tooltip shows, or
  null when the skill cannot learn. The calculator takes no TaleWorlds type, so it needs no adapter (ADR-007): the
  mixin's delegate is the seam.
- **Mixin (client):** `[ViewModelMixin("RefreshWithCurrentValues")]` on `SkillVM`. Vanilla calls that method when the
  screen builds the skill and after every attribute click, focus click and reset, so the line follows the preview before
  Confirm. The screen builds every hero's skill view models when it opens, and a hero switch only rebinds them; each one
  refreshes on its own hero's clicks. The mixin exposes `HasLoremastersLine` (enabled, a floor exists, and it lies on the
  bar), `LoremastersLineBarLevels` (the bar's width in levels), `LoremastersLineBandLevels` (the band's width in levels)
  and `LoremastersLineTooltip`. The flag needs a bar above 0 (`IsOnBar`), so the fill widget never shows with a
  `MaxAmount` of 0; both ints are also set before the flag turns true. The delegate is `level => model.CalculateLearningRate(attributes, focus, level, skill).ResultNumber`.
  Like vanilla's own rate line, it reads the preview attributes and the preview focus, and the rate takes no hero. The
  cost is 14 cheap model calls per skill per click, so nothing is cached.
- **Hero item:** `SkillVM._heroItem` is private and no public route reaches it. `LoremastersLineSkillAccess` binds it
  once with a `FieldRef` in a guarded static and builds the rate delegate. A miss logs one warning and leaves the screen
  vanilla. The class is split from the mixin to keep both under 150 lines (ADR-002).
- **Widget:** the engine's `FillBarHorizontalWidget` is the prefab root. Its `OnRender` sets the `Band` widget's width to
  `InitialAmount / MaxAmount` of its parent's width, right-aligned, as vanilla's `DeclareWarDecisionPanel` does. There is
  no TAOM widget and no per-frame TAOM code.
- **Prefab:** one `PrefabExtensionInsertPatch` on `CharacterDeveloper`, XPath
  `descendant::PerkSelectionBarWidget[@Id='PerkSelectionBarWidget']/Children`, `InsertType.Child`, `Index => 3`: before
  `PercentageIndicatorWidget`, so the level stick and the perks draw over it. The root takes no events, so the perks
  stay clickable. The tooltip is on the band, not the arrow: a `HintWidget` fills the `Band`, and the band sets
  `DoNotPassEventsToChildren` and is not disabled, because the engine collects a widget's children before the widget
  and a `HintWidget` relays only its parent's hover. The arrow is a flipped, disabled sprite with no children.
- **Vanilla's indicator:** needs no code. Vanilla already puts its learning limit indicator at `max(level, limit)`.
- **Tooltips:** `LoremastersLineTooltips` (client, because `TooltipProperty` is not loaded by the headless server) builds
  the lines. The mixin replaces `LearningLimitTooltip` with vanilla's `CampaignUIHelper.GetLearningLimitTooltip` plus one
  line, added only while enabled and only while `HasLoremastersLine` is true. The band's tooltip reads "Skill Limit",
  "Slowest learning after N", then either "Past it, learning keeps P of full speed" or "Past it, practice no longer
  improves this skill".
- **Log latches:** the refresh and tooltip failure sets are process-wide, since there is one mixin per skill view model
  and the screen builds every hero's skill view models twice at open.

**Floor past the bar:** with high attributes and focus the floor can be at or above `MaxLevel + 25`. The marker hides,
and so does the extra limit line.

**Non-monotonic models:** bisection finds a boundary (the rate at the level is at the floor, the rate one level below is
not), not always the lowest one. The default model is monotonic.

**Seasoned Hosts floor (#890):** with "Learning Never Stops" on (the default), `TaomCharacterDevelopmentModel` raises the
rate's minimum from 0 to 0.05 for every hero. The marker stays where it was: past the limit the rate falls 0.125 per
point, so the first level at or under 0.05 is the level where vanilla's rate reached 0. Only the band tooltip changes: it
reads "learning keeps P of full speed" instead of "practice no longer improves this skill", because the floor is above 0,
and P can read 0 when the full rate is above 10. Switching the setting off restores vanilla's tooltip. See
[seasoned-hosts.md](seasoned-hosts.md).

**Co-op:** local, read-only UI with nothing to gate. The setting is classed Presentation, outside the fingerprint.

### Component Diagram

```
SkillVM.RefreshWithCurrentValues
        |
LoremastersLineSkillMixin ---- LearningFloorCalculator (pure, core)
        |  bindings                 ^
        v                           | rateAt(level) via LoremastersLineSkillAccess
FillBarHorizontalWidget (engine)  CharacterDevelopmentModel.CalculateLearningRate
 Band (clip + fill), Arrow, hint
```

## Configuration

The MCM switch `EnableLoremastersLine`, group "Character Screen/Loremaster's Line", default on. It applies the next time
the character screen opens: the mixin reads it once per skill view model, when the screen builds it. It is read through
`LoremastersLineSettingsProvider`, which answers on when MCM is absent.

## Art

None new. Both sprites are redeclared by `TAOMSpriteData.xml` in `ui_taom` (AlwaysLoad), the TAOM re-skin that draws:
`CharacterDeveloper\learning_rate_limit_triangle` (50x50) and `CharacterDeveloper\progress_bar_fill`. The arrow is the
triangle tinted red and flipped so it points at the bar (TAOM's triangle points up). The band is the fill sprite tinted
`#FF3333FF`, inside a clipped parent so the sprite does not squeeze. If the triangle's shape or direction looks wrong in
game, swap to `CharacterDeveloper\indicator` (40x70), tinted the same.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LoremastersLine/LearningFloorCalculator.cs` | Pure floor search, bar and band maths, percent, cannot-learn rule |
| `Main/Features/LoremastersLine/LoremastersLineSettingsProvider.cs` | The MCM switch, on when MCM is absent |
| `Main/Features/LoremastersLine/LoremastersLineModule.cs` | Registers the provider |
| `Main/Composition/FeatureModules.cs` | One appended line |
| `Main/Features/TaomSettings.cs` | `EnableLoremastersLine` |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | Listed under Presentation |
| `Client/Features/LoremastersLine/UI/LoremastersLineSkillMixin.cs` | Data and tooltip callbacks on `SkillVM` (`TAOM.Client`) |
| `Client/Features/LoremastersLine/UI/LoremastersLineSkillAccess.cs` | The hero item field reference and the rate delegate |
| `Client/Features/LoremastersLine/UI/LoremastersLineTooltips.cs` | The tooltip lines |
| `Client/Features/LoremastersLine/UI/LoremastersLinePrefab.cs` | The insertion into `CharacterDeveloper` |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The seven `taom_ll_` strings |

## Dependencies

- UIExtenderEx (mixin and prefab host), `IModLogger`, `LoremastersLineSettingsProvider`.
- Vanilla `CampaignUIHelper.GetLearningLimitTooltip`, `CharacterDevelopmentModel.CalculateLearningRate`, the
  `str_NUMBER_percent` text, and the engine's `FillBarHorizontalWidget`.

## Tests

- `TAOM.Tests/Features/LoremastersLine/LearningFloorCalculatorTests.cs`: vanilla-shaped, steep, flat, rising, floor at
  skill 1, floor only at 4096, non-finite probes (end and mid-search), negative rates, non-monotonic boundary invariant,
  probe count, bar levels, on-bar boundary (`MaxLevel + 24` shows, `+ 25` hides, bars of zero or negative width), band
  levels, shown percent, percent, and the cannot-learn rule including NaN.
- `TAOM.Tests/Features/LoremastersLine/LoremastersLineSettingsProviderTests.cs`: default on, on without MCM, off, and the
  setting's localized name, hint and group.
- `TAOM.Tests/Features/LoremastersLine/LoremastersLineWiringTests.cs`: the mixin's target and method, the prefab's
  XPath, type and index, the fill bar's attributes and every binding resolving, the hint on the band, the band's
  event flags and geometry, the arrow as decoration, both sprites in TAOM's sheet, the guarded field reference, a
  try and catch on each engine-called member, the process-wide latches, the extra line following the flag, widths set
  before the flag, the 150-line limit, the tooltip lines kept in the client, and the module wiring (listed once, no save data, resolves from a real container,
  not registered in `IoC.cs`).
- `TAOM.Tests/Migration/LoremastersLineEngineMirrorTests.cs` (`RequiresGameIL`): the bar still divides by
  `MaxLevel + 25`, child 3 of the installed bar is still the level indicator, and `SkillVM._heroItem` is still a
  `CharacterDeveloperHeroItemVM` the typed field reference can bind.
- Rows in `ReflectionSiteBindingTests` and `ClientIsolationGateTests`, plus a settings provider row in
  `CampaignHotPathSettingsProvidersTests`.

## How to

**Read the marker.** Open the character screen and select a skill. The red band runs from the level where learning is
slowest to the end of the bar, and the red arrow marks its start. Hover the band: the tooltip names the level and says
what learning keeps past it (a share of full speed, or that practice no longer improves the skill). No marker means the
floor is past the end of the bar, or the curve has no slowest point. The learning limit tooltip (hover vanilla's yellow
indicator) gains a line about full-speed learning while the marker shows.

**Turn it off.** In MCM, open "Character Screen", then "Loremaster's Line", and switch it off. It applies the next time
the character screen opens, and the screen is then vanilla.

## Performance

About 1 us of model calls per refresh; UIExtenderEx's mixin plumbing costs about 2 us per refresh and 4 us per skill
view model; about 11 ms once at screen open for a 20-hero clan, also paid with the switch off.

## Translation

The seven strings are translated, and the two reworded ones (`taom_ll_tt_capped`, `taom_ll_mcm_enable_hint`) are
re-translated in all twelve language files (2026-11-12, #763).

## Owed in-game checks

- Hovering the band shows the tooltip.
- Perks inside the band keep their hints and clicks.
- The gap tooltip is not noisy.
- The arrow points at the bar.
- The red reads as red.
- A skill already past its floor (raised by a direct skill grant) still reads well: the red band covers the progress
  fill from the floor to the current level, under the level stick.
- No extra limit line while the marker is hidden.
- Arrow and band at the right level for several skills and heroes. With the default model, floor = limit + 4 x average
  attribute + 10 x focus.
- Vanilla's indicator sits at the larger of the level and the limit.
- The extra line shows on the learning limit tooltip while the marker shows.
- Preview focus and attribute clicks move the marker before Confirm.
- MCM off gives the vanilla screen.
- Switching heroes.

## Changelog

- 2026-10-23: Loremaster's Line added (#877).
- 2026-10-24: noted that Seasoned Hosts' learning floor changes the band tooltip and leaves the marker in place (#890).

## GitHub Issue

- **Issue:** [#877](https://github.com/haterade22/TAOM/issues/877): Skill limit marker on the character screen skill bar
- **Status:** Open
