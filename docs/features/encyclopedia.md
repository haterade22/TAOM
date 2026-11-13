# Encyclopedia

## Overview
Overrides Bannerlord's `DefaultInformationRestrictionModel` to add a MCM setting that reveals all characters in the encyclopedia regardless of whether the player has encountered them. When the setting is off, vanilla behavior is preserved.

The home page (six painted panels) stays as is (Mike, 2026-10-07).

## Why This Exists
- **Vanilla behavior:** `DefaultInformationRestrictionModel.DoesPlayerKnowDetailsOf(Hero)` returns `false` for heroes the player has not met, hiding their encyclopedia entries. Vanilla also provides the console cheat `campaign.toggle_information_restrictions` to bypass this.
- **TAOM requirement:** Players and modders often need to inspect all lords and characters in the encyclopedia without using the console, especially during testing or when exploring the new LOTR factions.
- **Without this feature:** No in-game toggle exists; players must open the console to reveal encyclopedia characters.

## Architecture

### Design Challenge
`DefaultInformationRestrictionModel` is registered as a `GameModel`. It must be subclassed and the override registered in `SubModule.cs` (or via the standard `GameModel` replacement mechanism). The `TaomSettings` MCM class lives outside the feature folder and its `Instance` property can be null before MCM is initialized, so the model must guard against null safely.

### Solution Approach
`TaomInformationRestrictionModel` extends `DefaultInformationRestrictionModel`. It takes an `IEncyclopediaSettingsProvider`, so tests fake the "show all" check without a live MCM instance. The production `EncyclopediaSettingsProvider` reads `TaomSettings.Instance?.ShowAllEncyclopediaCharacters ?? true`. When the check is `true`, `DoesPlayerKnowDetailsOf` short-circuits and returns `true` without calling `base`. When it is `false`, it delegates to `base.DoesPlayerKnowDetailsOf(hero)` exactly as vanilla.

### Component Diagram
```
TaomSettings (MCM) -- ShowAllEncyclopediaCharacters
        |
        | Func<bool> delegate
        v
TaomInformationRestrictionModel : DefaultInformationRestrictionModel
        |
        | DoesPlayerKnowDetailsOf(hero)
        |-- true  -> return true (show all, skip base)
        |-- false -> base.DoesPlayerKnowDetailsOf(hero)
```

## Configuration

The reveal-all setting is controlled through the MCM settings UI (Mod Configuration Menu). The hero filters (#907) read their own JSON file, described under "Hero filters" below.

| Setting | Group | Default | Description |
|---------|-------|---------|-------------|
| Show All Characters | Encyclopedia | `true` | Reveals all heroes in encyclopedia including unencountered ones |

The code default is `true` (`ShowAllEncyclopediaCharacters { get; set; } = true` in `Main/Features/TaomSettings.cs`). The setting is stored by MCM in its own JSON store under the `TAOM` folder, and a stored value wins over the code default.

## Hero filters and hero page layout (#907)

The hero page of the encyclopedia gains two filter groups (Race, and a race-aware Age), a Family block moved above Allies with tighter spacing, and a level in the skills heading. All of it is presentation: no campaign behavior, no save data, no MCM toggle.

### Race filter
The Race group has 8 items covering all 15 races of the RaceAge config. Membership is the JSON file's `races` list (the FaceGen race names); the labels live in C#.

| Item | Races |
|---|---|
| Men | `human` |
| Elves | `elf` |
| Dwarves | `dwarf` |
| Orcs | `orc` |
| Goblins | `goblin` |
| Uruk-hai | `uruk`, `uruk_hai`, `pale_uruk`, `dg_uruk`, `berserker` |
| Trolls | `cave_troll`, `hill_troll` |
| Maiar and Wraiths | `nazghul`, `saruman`, `sauron` |

A hero whose race id is invalid, or whose race is in no group, matches no race item. The id is validated before the name lookup, because `GetRaceNameFromId` answers "human" for an unknown id, which would file it under Men.

### Age filter
The Age group reuses vanilla's own "Age" label key and has five life stages: Young, Adult, Mature, Elder and Venerable. Each race group has 4 upper bounds in the JSON; the stages are `[18, b0)`, `[b0, b1)`, `[b1, b2)`, `[b2, b3)` and `[b3, ...)`.

| Group | Bounds (b0, b1, b2, b3) |
|---|---|
| Men | 25, 35, 45, 55 |
| Elves | 24, 28, 32, 40 |
| Dwarves | 20, 25, 30, 38 |
| Orcs | 23, 27, 32, 40 |
| Goblins | 30, 35, 40, 45 |
| Uruk-hai | 22, 26, 32, 40 |
| Trolls | 25, 35, 45, 55 |
| Maiar and Wraiths | 25, 35, 45, 55 |

The encyclopedia list only shows heroes at or above the engine's coming-of-age age, so the first stage starts at 18 (`HeroFilterRules.FirstStageAge`); a younger age falls in no stage. The bounds are fitted to the ages of the authored heroes, not to lore lifespans, which would put every elf and dwarf in Young. A hero of an unknown or ungrouped race uses the `defaultGroup` brackets (men in the shipped file). The bounds are a starting balance and can be retuned in the JSON.

A hero whose details the player does not know (`InformationRestrictionModel.DoesPlayerKnowDetailsOf` is false) matches no Age stage, because vanilla hides an unmet hero's age on the page; with Show All Characters on, `TaomInformationRestrictionModel` answers true, so default play is unchanged. The Race filter is unaffected: the page shows the model either way.

Every predicate catches: the engine's `EncyclopediaPage.IsFiltered` has no catch, so a throw would break the whole list. A predicate that cannot read the hero logs once and lets the hero through.

### Config file
`Main/_Module/ModuleData/encyclopedia/hero_filters.json`, loaded by `HeroFilterConfigProvider`.

| Field | Meaning |
|---|---|
| `defaultGroup` | The group whose brackets an unknown-race hero uses |
| `groups[].id` | One of the 8 known ids: `men`, `elves`, `dwarves`, `orcs`, `goblins`, `uruks`, `trolls`, `maiar` |
| `groups[].races` | Race names (case-insensitive) that make up the Race item |
| `groups[].ageUpperBounds` | Exactly 4 upper bounds for the first four stages |

Validation never repairs a value; a bad entry is dropped with a warning and the rest keep working.

| Rule | Result |
|---|---|
| Group id not in the known list | Group dropped |
| Group id appears twice | The later group is dropped |
| `ageUpperBounds` is not 4 values, or one is NaN or Infinity, is 18 or lower, or is above 10000, or the bounds do not strictly increase | Group dropped |
| Empty race id | Race entry dropped |
| Race already claimed by an earlier group | Removed from the later group |
| `defaultGroup` missing or not a surviving group | The first surviving group is used, with a warning |
| File missing, or it fails to parse | Error logged and the hero filters are off (no groups added) |

### Reload scope
The adapter loads the config once and keeps it (`_config ??= LoadConfig()`), and the hero page builds its filter list once, in its constructor. A change to the JSON therefore needs a full game restart; loading another save does not re-read it.

### Patch119 hook
`Patch119_EncyclopediaHeroFilters` is one Harmony postfix on `DefaultEncyclopediaHeroPage.InitializeFilterItems()` (`DefaultEncyclopediaHeroPageInitializeFilterItemsPatch`), owned by `EncyclopediaFiltersModule`. Full entry: [harmony-patch-registry.md](../reference/harmony-patch-registry.md).

- **Phase:** ProcessLoad (OnSubModuleLoad), not GameInit. SandBox builds the encyclopedia pages in its own `OnGameInitializationFinished`, which runs before TAOM's, so a GameInit patch would miss the first campaign.
- **Copy, not cast:** the co-op module postfixes the same method with a lazy `AddItem`, so the result is never cast to a `List`. The postfix copies it (`ToList()`), appends the two groups and assigns the copy back, which composes with the co-op postfix.
- **Failure:** a throw is caught and logged, and the incoming result stays untouched.

### Hero page layout
TAOM's copy of `EncyclopediaHeroPage.xml` moves the Family block (divider, the `EncyclopediaHeroFamilyContentScope` targeter and the grid) above the Allies divider. It is a plain prefab edit with no runtime reorder hook. All siblings stay in the same `ListPanel`, so every collapse target (`Parameter.ItemList`, `ScopeParent`) still resolves, and Family collapse is unchanged.

| Widget | Margin |
|---|---|
| `FamilyDivider` | top 30 |
| `FamilyGrid` | top 25, bottom 10 |
| `AlliesDivider` | top 20 |
| `EnemiesDivider` | top 20 |

These are starting values; the look is the in-game check. The Lines of Descent button ([lines-of-descent.md](lines-of-descent.md)) is prepended before the Family scope targeter, so it moves with the block. The Messengers extension anchors on the `@InformationText` rich text, which this change does not touch. `EncyclopediaHeroPageLayoutTests` pins the contiguous run, the comment order, the margins, unique ids, that every `ItemList` and `ScopeParent` reference names a sibling, and that both extension anchors match exactly once.

### Level header
A hero of age shows "Lvl: {LEVEL}, Skills:" as the skills heading instead of vanilla's text. The rule (`HeroLevelHeader.ShouldShowLevel`): the information is not hidden and the hero's age is at or above `Campaign.Current.Models.AgeModel.HeroComesOfAge`; otherwise vanilla's text stays. The key is `taom_ency_hero_level_skills`. `EncyclopediaHeroLevelHeaderMixin` (a UIExtenderEx mixin hooked on `Refresh` of `EncyclopediaHeroPageVM`, the third mixin on that view model) writes `SkillsText` after the engine sets it. The heading line shows only when the hero has a skill of 50 or more (vanilla binds its visibility to `HasAnySkills`); the mixin sets the text either way. No Harmony and no reflection.

### What was dropped, and why
- **Removing cultures from the culture filter:** nothing qualifies. All 26 main cultures own live settlements (Goblin-town 7 to Gondor 97; Ered Luin 31, counted in the live `settlements.xml` on 2026-10-27), so no culture is empty and a removal rule would hide none.
- **Showing the civilian outfit:** vanilla already shows it for notables and for noncombatant heroes who lead no party; combatant heroes keep their battle gear. Forcing it for the rest risked showing the wrong gear. Not built.

### Co-op
The filters, the layout and the header are per-peer UI: they change no campaign behavior and save nothing, so there is no gate (see "Deliberately NOT gated" in [coop-interop.md](coop-interop.md)). The Patch119 postfix skips nothing and writes no campaign state.

### Translation
The 15 `taom_ency_*` rows in `taom_module_strings.xml` (the race filter label, the 8 race items, the 5 stage labels and the level header) were seeded in English and are translated in all 12 languages (2026-11-12, #763).

### In-game checklist
Not yet checked in game.
1. Open the hero list: the Race filter shows 8 items and each narrows the list to those races.
2. The Age filter shows 5 stages; combine it with a race (an elf, a dwarf, a man) and confirm the stage splits follow the table above.
3. Open a hero page of each race: Family sits above Allies, the heading reads "Lvl: N, Skills:" for an adult with a skill of 50 or more (the line is hidden without one), vanilla's text for a child, and the spacing looks even.
4. Collapse and expand the Family divider: the grid hides and shows.
5. The Lines of Descent button and the Messengers button are both in place on the page.
6. Open the page of an AI lord who leads an army: it reads "Last seen leading an army near X" (or "at X" inside a settlement), not a missing-text error. The player's own page shows no last-seen line, so it cannot show this.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/Encyclopedia/Models/TaomInformationRestrictionModel.cs` | The `GameModel` override — sole implementation file for this feature |
| `Main/Features/TaomSettings.cs` | MCM settings class containing `ShowAllEncyclopediaCharacters` |
| `TAOM.Tests/Features/Encyclopedia/TaomInformationRestrictionModelTests.cs` | Unit tests for both short-circuit and delegate-to-base paths |
| `Main/Features/Encyclopedia/EncyclopediaFiltersModule.cs` | Feature module for the hero filters: services and the Patch119 category (ProcessLoad) |
| `Main/Features/Encyclopedia/HeroFilterConfig.cs`, `HeroFilterConfigProvider.cs`, `HeroFilterRules.cs` | Config shape, loader with validation, and the pure stage and race rules |
| `Main/Features/Encyclopedia/Adapters/EncyclopediaHeroFilterAdapter.cs` | Builds the Race and Age filter groups |
| `Main/Features/Encyclopedia/Hooks/DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.cs` | The Patch119 postfix |
| `Main/Features/Encyclopedia/HeroLevelHeader.cs`, `Client/Features/Encyclopedia/UI/EncyclopediaHeroLevelHeaderMixin.cs` | The level header rule and its mixin |
| `Main/_Module/ModuleData/encyclopedia/hero_filters.json` | Race groups and age bounds |
| `Main/_Module/GUI/PreFabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` | Family block above Allies, retuned margins |

## Dependencies
- `TaomSettings` (MCM `AttributeGlobalSettings<TaomSettings>`) — provides the setting value via `TaomSettings.Instance`
- `DefaultInformationRestrictionModel` (TaleWorlds) — base class

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/Encyclopedia/TaomInformationRestrictionModelTests.cs` | `DoesPlayerKnowDetailsOf_WhenShowAllEnabled_ReturnsTrueWithoutCallingBase` — verifies short-circuit; `DoesPlayerKnowDetailsOf_WhenShowAllDisabled_DelegatesToBase` — verifies base is called (evidenced by the `NullReferenceException` that base throws without campaign state) |
| `HeroFilterRulesTests.cs`, `HeroFilterConfigProviderTests.cs`, `ShippedHeroFiltersConfigTests.cs` | Stage and race rules; every validation rule; the shipped JSON loads clean, every RaceAge race is in exactly one group, the default group holds the default race |
| `EncyclopediaHeroFilterAdapterTests.cs`, `EncyclopediaHeroFilterPatchTests.cs` | The filter groups and predicates; the postfix keeps a lazy input in order, handles a null result, and leaves the result untouched on a throw or with no adapter; the patch category and ProcessLoad phase |
| `HeroLevelHeaderTests.cs`, `EncyclopediaHeroLevelHeaderMixinTests.cs` | The header rule; the mixin hooks `Refresh`, does not call `OnRefresh` in its constructor |
| `EncyclopediaHeroPageLayoutTests.cs` | The prefab layout described above |

## How to Add a New Encyclopedia Restriction Override

1. Add a new `override` method to `TaomInformationRestrictionModel` for the relevant `DefaultInformationRestrictionModel` method.
2. Gate the new behavior on an existing or new `TaomSettings` property.
3. Add a test that covers both the short-circuit and delegate-to-base paths using the `Func<bool>` constructor.

## Changelog

- 2026-05-13 — #145: replaced concrete-singleton coupling (`TaomSettings.Instance?.ShowAllEncyclopediaCharacters`) in `TaomInformationRestrictionModel` with an injected `IEncyclopediaSettingsProvider` (new `EncyclopediaSettingsProvider` + `EncyclopediaIoC`, registered in `Main/IoC.cs`); tests updated to NSubstitute on the new interface.

- 2026-11-10 (v2.0.37): Native v1.5.7 defines both `str_last_seen_encyclopedia_entry_leading_army` ids itself (`module_strings.xml`, placeholder key `{=*}`), so TAOM's two rows in `taom_module_strings.xml` and their 24 language rows are deleted (#1112). The page shows vanilla's English text in every language. Checklist item 6 is still owed, now on v1.5.7.
- 2026-10-27 (v2.0.36): Patch123 (the `HeroHelper.GetLastSeenText` guard for crash 9b2a103e) was retired. Vanilla v1.5.6 null-guards every army and party leader read the patch covered, so the guard, `LastSeenState` and their tests are deleted. The two vanilla strings v1.5.6 adds for a hero leading an army (`str_last_seen_encyclopedia_entry_leading_army` and `..._at_sea_leading_army`) are defined in `taom_module_strings.xml`, because no installed module defines them (#1036). Needs an in-game check (checklist item 6).
- 2026-10-26: crash 9b2a103e: Patch123 contains the vanilla v1.5.x `HeroHelper.GetLastSeenText` NRE. Its new prisoner and army branches read the captor's or army leader's hero without a null check, so the page of a known lord held by a leaderless party (bandits, for example) crashed. The page now shows "last seen near X" for him. Needs an in-game check.

- 2026-10-24: #907: race and age filters on the hero page (Patch119), Family block above Allies, level in the skills heading; corrected the documented default of Show All Characters to `true`.

## GitHub Issue
- **Issue:** #907 (filters and hero page layout). The reveal-all model: unknown (commits reference `9f14d24` and `b68feab` but no issue number)
- **Status:** Active

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
