# Hearth Roll

## Overview

Hearth Roll adds two small badges to the Members list of the clan screen. A companion of the player's clan whose relation with the player has fallen below zero gets a faint red tint behind the row, and each child's row shows the child's age in years after the name ("7 years old"). It is read-only: no Harmony patch, no campaign behavior, nothing saved.

## Why This Exists

- **Vanilla behavior:** the Members list shows a name, a portrait and a few stats. A companion whose relation with the player falls below -10 on a relation loss is removed from the game (vanilla records them as having disappeared), with no earlier sign on this screen, and a child's age is visible only on the encyclopedia page.
- **TAOM requirement:** a player running a large household of companions and children wants the warning and the ages where the whole clan is listed.
- **Without this feature:** the player finds out a companion is unhappy when they are gone, and opens each child's page to learn how old they are.

## What the player sees

- **The tint.** A red wash (colour `#680A0D`, alpha 0.25) behind the row of a player companion whose relation with the player is below 0. A companion at 0 or above shows nothing. Lords, family members and every other row are never tinted.
- **The age.** After the name of a child's row, in the name's own brush: "{AGE} year old" for 1, "{AGE} years old" for any other number.

## Rules

- **Effective relation.** The relation is `Hero.MainHero.GetRelation(hero)`, which adds up to plus or minus 4 from personality traits on top of the stored value. It is the number the hero tooltip shows and the one vanilla tests when it removes a companion.
- **Early warning.** Vanilla removes a companion on the next relation loss once that relation is below -10, so the tint (below 0) warns 10 points early.
- **Tint rule.** The toggle is on, the hero is a player companion (`IsPlayerCompanion`) and the relation is below 0 (`HearthRollRules.ShowsUnhappyTint`). The relation is read only when the toggle is on and the hero is a companion, straight from `Hero.MainHero` so a Disabled companion (away on an issue's alternative solution, which `Hero.AllAliveHeroes` omits) is still tinted. A fault in the read is caught and logged once per row, and the row stays plain.
- **Age rule.** The age shows only on rows where vanilla's `IsChild` is true for the row's view model. The years are `(int)Hero.Age`, the whole-year figure the encyclopedia prints. A negative or non-finite age shows nothing (`HearthRollRules.AgeYears`).
- **Races.** The [Race Age System](race-age-system.md) does not change how heroes age, so an elf child shows the real years of its age.
- **Teens.** Only rows vanilla draws as children get an age, so a hero above that threshold shows none.

## Configuration

One MCM setting on `TaomSettings`:

| Field | Value |
|-------|-------|
| Group | "Hearth Roll", `GroupOrder = 69` (after Help Unlooked-For at 68) |
| Name | "Tint Unhappy Companions" (`EnableUnhappyCompanionTint`) |
| Default | on |
| Restart | not required; the switch is read each time a row refreshes, so it applies the next time the clan screen opens |

The age is always shown; the toggle controls the tint only. **Never flip the default.** MCM's json2 keeps a player's stored value, so a changed default reaches nobody who has opened the settings. To change it, rename the property (with a comment saying why).

## Architecture

### Design Challenge

The vanilla `ClanLordTuple` prefab has no widget `Id` attributes, so there is nothing stable to anchor an injection on. The UI types are also client-only, so they cannot live in the server-safe core (ADR-013).

### Solution Approach

- **Core (`Main/`):** `HearthRollRules` holds the two decisions as pure static functions. `HearthRollTexts` builds the age line. `HearthRollSettingsProvider` reads the MCM switch through one cached settings reference, and is on when MCM has not built its settings yet. `HearthRollModule` is a feature module that registers only the provider (`OwnsSaveData` is false). The mixin reads the relation itself as `Hero.MainHero?.GetRelation(hero)`; no adapter.
- **Client (`Client/`):** one view-model mixin and three prefab extensions, found by UIExtenderEx's assembly scan, so there is no line in `TaomClientComposition.cs`.
- **No Harmony.** Nothing is saved.

## Hooks

| Hook | Where | Anchor or method |
|------|-------|------------------|
| View-model mixin | `ClanLordItemBadgeMixin` on `ClanLordItemVM`, method `UpdateProperties` | Beside Lines of Descent's `ClanLordItemFamilyMixin` on the same view model. Adds `IsTaomUnhappyCompanion` and `TaomAgeText`. The constructor does not call `OnRefresh`, because UIExtenderEx 2.13.3 replays the view model constructor's first `UpdateProperties` on the mixin. A throw is logged once per row and leaves the row plain. |
| Prefab extension | `ClanLordTupleTintPrefab` on `ClanLordTuple` | `/Prefab/Window/ButtonWidget/Children`, `InsertType.Child`, index 0: a stretch-to-parent `BlankWhiteSquare_9` widget with `IsVisible="@IsTaomUnhappyCompanion"` and `DoNotAcceptEvents`, drawn behind the row's content. |
| Prefab extension | `ClanLordTupleNameWidthPrefab` on `ClanLordTuple` | `.../ListPanel/Children/TextWidget[1]`, sets `WidthSizePolicy` to `CoverChildren` and `MaxWidth` to 226 on the name widget, so the age can sit right after a short name while a long name wraps at the vanilla width. |
| Prefab extension | `ClanLordTupleAgeLabelPrefab` on `ClanLordTuple` | Same node, `InsertType.Append`: a sibling `TextWidget` bound to `@TaomAgeText`, `AutoHideIfEmpty`, brush `Clan.Tuple.Name.Text`. |

The three XPaths are structural because the prefab has no Ids. `HearthRollUiWiringTests` pins each one (it must match exactly once) and that the name node binds `@Name`, so a changed vanilla prefab fails a test instead of a row.

### The Player Switcher picker

TAOM's Player Switcher picker (`PreBuildCharacterSelection.xml`) reuses `ClanLordTuple`, bound to `HeroPickItemVM`, which has neither new property. There the tint stays hidden and the age label stays empty (a binding to a missing property), but the name-width change applies to those rows too.

## Co-op

Read-only UI with nothing saved, so it is not gated. Each peer sees its own companions. See "Deliberately NOT gated" in [coop-interop.md](coop-interop.md). The setting is classified as presentation in `CoopSettingsRelevance`, so it is not part of the settings fingerprint.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HearthRoll/HearthRollRules.cs` | The tint and age decisions |
| `Main/Features/HearthRoll/HearthRollTexts.cs` | The two `taom_hr_*` age literals |
| `Main/Features/HearthRoll/HearthRollSettingsProvider.cs` | The cached MCM switch |
| `Main/Features/HearthRoll/HearthRollModule.cs` | Feature module; registers the provider |
| `Client/Features/HearthRoll/UI/ClanLordItemBadgeMixin.cs` | The view-model mixin |
| `Client/Features/HearthRoll/UI/ClanLordTuplePrefabs.cs` | The three prefab extensions |
| `Main/Features/TaomSettings.cs` | `EnableUnhappyCompanionTint` |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | The presentation entry |
| `Main/Composition/FeatureModules.cs` | One registration line |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 2 `taom_hr_*` rows |

## Dependencies

- `Hero.MainHero.GetRelation` (engine): the effective relation, read directly in the mixin.
- `TaomSettings` (MCM): the switch.
- UIExtenderEx and the vanilla `ClanLordTuple` prefab.

## Tests

- `TAOM.Tests/Features/HearthRoll/HearthRollRulesTests.cs`: the tint and age rules.
- `TAOM.Tests/Features/HearthRoll/HearthRollTextsTests.cs`: the singular and plural age lines.
- `TAOM.Tests/Features/HearthRoll/HearthRollSettingsProviderTests.cs`: the switch and its default before MCM is up.
- `TAOM.Tests/Features/HearthRoll/HearthRollUiWiringTests.cs`: the three XPaths, the name binding, the mixin hook and its constructor.
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`: the settings counts, which this setting moved by one.

## Known limits and risks

- **Tint strength.** Colour and alpha are prefab constants, not settings. The look in game is unchecked.
- **Long names.** The name widget covers its text up to 226 px and the age label follows it. A long name wraps at about that width as in vanilla; the longest name in the data is 35 characters.
- **Child threshold.** The age follows vanilla's `IsChild`. The native maturity threshold behind it was not verified here.

## In-game checklist

1. A player companion at relation -1 has a tinted row; at 0 there is no tint. Check a lord and a family member: no tint.
2. Turn **Tint Unhappy Companions** off, reopen the clan screen: no tint. The child ages stay.
3. A child shows the age after the name: "1 year old" for a one-year-old, "N years old" otherwise. Check the label in English, and in Russian and German (these show the translated label).
4. A hero just above the child threshold shows no age.
5. Long names wrap at about 226 px as in vanilla and the row's right-hand text still reads. Check the longest name (35 characters, "Muzgash, Uruk-hai Berserker Captain") in the Members list and in the Player Switcher picker.
6. The Player Switcher picker rows: no tint, no age, names intact.
7. A companion away on an alternative-solution quest, with negative relation, is tinted.

## Translation

The 2 `taom_hr_*` rows (`taom_hr_age_one`, `taom_hr_age_many`) are translated in all 12 language files (2026-11-12, #763).

**Translator note.** Russian and Polish need a 2 to 4 form that a one/many pair cannot give. A label form without number agreement is acceptable, as vanilla does for its own year text ("Годы: {YEAR}" in RU, "Liczba lat: {YEAR}" in PL).

## Changelog

- 2026-10-23: added (#906).

## GitHub Issue

- **Issue:** #906, Hearth Roll
- **Status:** Open
