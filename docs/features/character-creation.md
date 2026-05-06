# Character Creation

## Overview
Replaces Bannerlord's vanilla character creation content with TAOM-specific cultures, narrative backstory menus, and race assignment. On finalization it sets the player's race based on their chosen culture and teleports them to a culture-appropriate starting settlement.

## Why This Exists
- **Vanilla behavior:** Character creation offers 6 human cultures (empire, vlandia, sturgia, aserai, battania, khuzait) with fixed narrative options tied to those cultures, an unfiltered race dropdown showing all engine races, and a random body silhouette per culture.
- **TAOM requirement:** The mod adds 10+ custom cultures (Gondor, Erebor, Mordor, Rohan, elves, etc.) each needing their own race assignment, starting settlement, narrative backstory options, body proportions, and culture-restricted race dropdown so a Gondor player can't pick "Cave Troll".
- **Without this feature:** Custom cultures would not appear in character creation; the player would start as a human with default vanilla placement; no-mount cultures (Erebor dwarves) would crash with a `NullReferenceException` when the narrative scene tried to spawn a horse actor; the race dropdown would offer lore-inappropriate choices for every culture; the body silhouette would not reflect the culture (Erebor would render at human-default proportions instead of dwarven).

## Architecture

### Design Challenge
Five separate problems must be solved:

1. **Registering custom cultures** — `CharacterCreationManager` is provided by SandBox's handler at priority 800. TAOM must hook in at a higher priority (1050) via `AfterInitializeContent` so it runs after vanilla setup.
2. **Replacing narrative menus** — The parent/childhood/youth narrative menus exist but contain vanilla option objects. TAOM must remove those and substitute JSON-driven TAOM options without breaking the menu structure.
3. **No-mount crash** — Vanilla's `GetYouthMenuNarrativeMenuCharacterArgs`, `GetAdultMenuNarrativeMenuCharacterArgs`, and `GetAgeSelectionMenuNarrativeMenuCharacterArgs` unconditionally read `DefaultEquipment[Horse].Item.StringId` to build a horse `NarrativeMenuCharacterArgs`. When a culture's CC equipment roster has no horse slot the read returns null and the game crashes. A separate crash fires in `SpawnNonHumanNarrativeMenuCharacter` when it tries to look up the null horse item ID in `MBObjectManager`.
4. **Culture-restricted race dropdown** — `FaceGenVM` builds its `RaceSelector` from `FaceGen.GetRaceNames()` and uses the array index as the engine's global race ID. Filtering the array shifts indices and breaks the dropdown's race-mesh lookup. The fix must filter the visible items while preserving the index→race-name contract that vanilla code depends on.
5. **Per-culture body silhouette** — Vanilla initializes `_faceGenerationParams` with culture-driven random ranges. TAOM cultures need a deterministic per-culture default body so a Mordor uruk and a Rivendell elf don't both spawn at human-default proportions. Vanilla has no extension point; we must hook the culture-selection event and apply a culture-specific `BodyProperties` key string to the player character.

### Solution Approach
- `CharacterCreationRegistrationBehavior` (a `CampaignBehaviorBase`) listens for `OnCharacterCreationInitializedEvent` and registers `TaomCharacterCreationContentHandler` at priority 1050.
- `TaomCharacterCreationContentHandler` implements `ICharacterCreationContentHandler`. Its `AfterInitializeContent` calls into `ICharacterCreationContentService` to register cultures and replace narrative menus.
- `CharacterCreationContentService` reads culture data from `ICultureCreationDataProvider` (cultures.json) and narrative options from `INarrativeDataProvider` (parents/childhood/youth JSON files). Vanilla cultures are skipped by a hard-coded allow-list.
- On `OnCharacterCreationFinalize`, the service sets the player's race via `IRaceManager`/`IHeroRosterAdapter` and teleports `MobileParty.MainParty` to the culture's starting settlement. Race assignment honors the player's actual FaceGen choice when valid + in the culture's allow-list (validated via `IRaceManager.IsValidRaceId`); otherwise falls back to `cultureData.Races[0]`.
- `Patch20_NarrativeHorseGuard` contains four Harmony patches: three Prefix patches intercept the three `GetXxxMenuNarrativeMenuCharacterArgs` methods and short-circuit to return a player-only list when no horse is present; one Finalizer on `SpawnNonHumanNarrativeMenuCharacter` swallows the resulting `ArgumentNullException(key)`.
- `Patch9_RaceFilter` Postfixes `FaceGenVM.Refresh(bool)`, delegates to `FaceGenRaceSelectorRebuilder.Apply(faceGenVM, filterService)`. The rebuilder constructs a fresh `SelectorVM<SelectorItemVM>` with only allowed races, wires a wrapped `_onChange` that translates the user's filtered selection back to the engine's global race index via reflection on `_selectedIndex`, and assigns through the public `RaceSelector` property setter (which fires the change notification correctly). See [Race Filter](#race-filter-patch9_racefilter) for full details.
- `Patch29_CCBodyProperties` Postfixes `CharacterCreationContent.SetSelectedCulture(CultureObject)`, delegates to `ICCBodyPropertiesService.ApplyForCulture`, which loads the culture's `<BodyProperties version="4" key="..."/>` element from `cc_body_properties.xml` and applies it via `IPlayerBodyPropertiesAdapter` (wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`). See [Per-Culture Default BodyProperties](#per-culture-default-bodyproperties-patch29_ccbodyproperties) for full details.

### Component Diagram
```
CampaignEvents.OnCharacterCreationInitializedEvent
        |
CharacterCreationRegistrationBehavior
        |  registers at priority 1050
TaomCharacterCreationContentHandler (ICharacterCreationContentHandler)
        |  AfterInitializeContent
ICharacterCreationContentService
        |-- ICultureCreationDataProvider --> charactercreation/cultures.json
        |-- ICultureRaceFilterService    --> per-culture races[] allow-lists (from cultures.json)
        |-- INarrativeDataProvider       --> charactercreation/{parents,childhood,youth}_menu.json
        |-- IEquipmentRosterProvider     --> MBEquipmentRoster lookups
        |-- IRaceManager (+ IsValidRaceId gate) / IHeroRosterAdapter --> race assignment on finalize

Harmony Patch9_RaceFilter
        └── Postfix: FaceGenVM.Refresh(bool)
                └── FaceGenRaceSelectorRebuilder.Apply
                        ├── ICultureRaceFilterService.GetAllowedRaces
                        └── faceGenVM.RaceSelector = newSelector  (public setter)

Harmony Patch20_NarrativeHorseGuard
        |-- Prefix: GetYouthMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAdultMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAgeSelectionMenuNarrativeMenuCharacterArgs
        |-- Finalizer: SpawnNonHumanNarrativeMenuCharacter

Harmony Patch29_CCBodyProperties
        └── Postfix: CharacterCreationContent.SetSelectedCulture(CultureObject)
                └── ICCBodyPropertiesService.ApplyForCulture(cultureId)
                        ├── ICCBodyPropertiesProvider.GetBodyPropertiesXml --> cc_body_properties.xml
                        └── IPlayerBodyPropertiesAdapter.TryApplyFromXml
                                └── CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties
```

## Configuration

### `Main/_Module/ModuleData/charactercreation/cultures.json`
JSON array of culture entries:
```json
{
  "culture_id": "erebor",
  "races": ["dwarf"],
  "starting_settlement": "town_E1",
  "default_age": 40,
  "default_weight": 0.6,
  "default_build": 0.7,
  "focus_to_add": 1,
  "skill_level_to_add": 10
}
```

### `Main/_Module/ModuleData/charactercreation/parents_menu.json`
### `Main/_Module/ModuleData/charactercreation/childhood_menu.json`
### `Main/_Module/ModuleData/charactercreation/youth_menu.json`
JSON arrays of narrative option entries:
```json
{
  "string_id": "taom_gondor_noble_parents",
  "culture_id": "empire_w",
  "text": "...",
  "description": "...",
  "skills": ["OneHanded", "Leadership"],
  "attribute": "Social",
  "occupation_type": "Noble",
  "title_type": "Lord",
  "focus_to_add": 1,
  "skill_level_to_add": 10,
  "attribute_level_to_add": 1
}
```
Options without `string_id` prefixed with `taom_` are treated as vanilla and removed.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/CharacterCreation/CharacterCreationIoC.cs` | DryIoc registrations for the feature |
| `Main/Features/CharacterCreation/CharacterCreationRegistrationBehavior.cs` | `CampaignBehaviorBase` entry point — registers the handler at priority 1050 |
| `Main/Features/CharacterCreation/TaomCharacterCreationContentHandler.cs` | Thin `ICharacterCreationContentHandler` delegating to the service |
| `Main/Features/CharacterCreation/ICharacterCreationContentService.cs` | Service interface |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Core logic: culture registration, narrative menu replacement, finalization. `SetPlayerRace` honors player's FaceGen choice when valid + in allow-list (gated on `IsValidRaceId`) |
| `Main/Features/CharacterCreation/CultureCreationDataProvider.cs` | Parses cultures.json |
| `Main/Features/CharacterCreation/NarrativeDataProvider.cs` | Parses narrative menu JSON files with concurrent cache |
| `Main/Features/CharacterCreation/NarrativeMenuBuilder.cs` | Builds `NarrativeMenuOption` objects from `NarrativeOptionDefinition` list |
| `Main/Features/CharacterCreation/EquipmentRosterProvider.cs` | Looks up `MBEquipmentRoster` by string ID |
| `Main/Features/CharacterCreation/Models/CultureCreationData.cs` | POCO: culture metadata including race, settlement, body defaults |
| `Main/Features/CharacterCreation/Models/NarrativeOptionDefinition.cs` | POCO: one narrative backstory choice |
| `Main/Features/CharacterCreation/ICultureRaceFilterService.cs` | Race filter interface |
| `Main/Features/CharacterCreation/CultureRaceFilterService.cs` | Reads `cultures.json` `races[]` per culture; one-time fallback warning for unknown cultures |
| `Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs` | Race-filter engine helper. Pure static helpers: `BuildGlobalIndexMap`, `MapFilteredIndexToGlobal`, `MapGlobalIndexToFiltered`. Apply uses public `RaceSelector` property setter (not field reflection) for correct UI rebinding |
| `Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs` | Patch9_RaceFilter — Postfix on `FaceGenVM.Refresh(bool)`. Thin entry point delegating to `FaceGenRaceSelectorRebuilder.Apply` |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | Patch20_NarrativeHorseGuard — 4 patches guarding no-mount culture crashes |
| `Main/Features/CharacterCreation/ICCBodyPropertiesProvider.cs` | CC body-properties config interface |
| `Main/Features/CharacterCreation/CCBodyPropertiesProvider.cs` | Parses `cc_body_properties.xml`; validates 128-hex key length, lowercases culture IDs, last-wins on duplicates |
| `Main/Features/CharacterCreation/ICCBodyPropertiesService.cs` | CC body-properties application interface |
| `Main/Features/CharacterCreation/CCBodyPropertiesService.cs` | Resolves XML for a culture and delegates to adapter; logs reject reasons |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs` | Patch29_CCBodyProperties — Postfix on `CharacterCreationContent.SetSelectedCulture` triggers per-culture body-properties application |
| `Main/Adapters/IPlayerBodyPropertiesAdapter.cs` + `PlayerBodyPropertiesAdapter.cs` | Wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`; preserves Race + IsFemale |
| `Main/_Module/ModuleData/charactercreation/cultures.json` | Culture metadata + race-filter allow-lists |
| `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml` | Per-culture default `BodyProperties` (key=128 hex chars) for the CC preview |
| `Main/_Module/ModuleData/charactercreation/parents_menu.json` | Parent backstory options |
| `Main/_Module/ModuleData/charactercreation/childhood_menu.json` | Childhood backstory options |
| `Main/_Module/ModuleData/charactercreation/youth_menu.json` | Youth backstory options |

## Dependencies
- `ICultureCreationDataProvider` — reads cultures.json
- `ICultureRaceFilterService` — exposes per-culture race allow-lists from cultures.json
- `ICCBodyPropertiesProvider` — reads cc_body_properties.xml
- `ICCBodyPropertiesService` — applies body XML to player character on culture change
- `IPlayerBodyPropertiesAdapter` — wraps `BodyProperties.FromString` + `CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties`
- `INarrativeDataProvider` — reads narrative JSON files
- `IEquipmentRosterProvider` — looks up CC equipment rosters
- `IRaceManager` — maps race name to race ID; `IsValidRaceId` gates fallback acceptance
- `IHeroRosterAdapter` — gets/sets hero race on finalization
- `IPathService` — resolves `ModuleDataPath`
- `IModLogger` — logging throughout

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs` | `RegisterCustomCultures`, `RegisterNarrativeMenus`, `OnCharacterCreationFinalize`, `SetPlayerRace` (16 tests including FaceGen-preservation, invalid-ID fallback, case-insensitive matching) |
| `TAOM.Tests/Features/CharacterCreation/CultureCreationDataProviderTests.cs` | JSON parsing, missing file handling, `GetCultureData` lookup |
| `TAOM.Tests/Features/CharacterCreation/NarrativeDataProviderTests.cs` | JSON parsing, caching, `GetMenuOptionsForCulture` filtering, missing file handling |
| `TAOM.Tests/Features/CharacterCreation/NarrativeMenuBuilderTests.cs` | Option construction, empty input handling |
| `TAOM.Tests/Features/CharacterCreation/CultureRaceFilterServiceTests.cs` | 24 tests: per-culture allow-lists, fallback, case-insensitive matching, empty-array handling |
| `TAOM.Tests/Features/CharacterCreation/FaceGenRaceSelectorRebuilderTests.cs` | 12 tests for pure helpers: `BuildGlobalIndexMap`, `MapFilteredIndexToGlobal`, `MapGlobalIndexToFiltered`, round-trip property |
| `TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesProviderTests.cs` | XML parsing, 128-hex validation, missing file handling, duplicate-id last-wins, malformed XML |
| `TAOM.Tests/Features/CharacterCreation/CCBodyPropertiesServiceTests.cs` | Apply-for-culture happy path, missing-config no-op, adapter-rejection logging |

## Race Filter (Patch9_RaceFilter)

The Character Customization Race dropdown is filtered to the races defined in each culture's `races` array in `cultures.json`. The filter is applied by `FaceGenVM_Refresh_RaceFilter_Patch` as a Postfix on `FaceGenVM.Refresh(bool clearProperties)`.

### Why a postfix on `Refresh`, not on `GetRaceNames`
`FaceGenVM` builds its `RaceSelector` as `new SelectorVM<SelectorItemVM>(FaceGen.GetRaceNames(), _selectedRace, OnSelectRace)`. The VM uses **the array index from `GetRaceNames()`** as the engine's global race ID. Filtering the array shifts indices and decouples the dropdown from the race table — the original Patch9 attempt did exactly that and broke. The current patch instead replaces `_raceSelector` after vanilla construction and translates filtered ↔ global indices in a wrapped `_onChange` callback.

### Index-translation flow
```
User clicks filtered position N
  → SelectorVM.SelectedIndex setter fires (= N)
  → wrapped _onChange (our wrapper):
      saved = N
      mutate s._selectedIndex = globalIndices[N]   (via reflection, bypassing setter)
      call vanilla OnSelectRace(s):
          _selectedRace = s.SelectedIndex          (= globalIndices[N], correct global race ID)
          UpdateRaceAndGenderBasedResources()
              → UpdateFace(-20, _selectedRace) → SetRaceGenderAndAdjustParams updates _faceGenerationParams.CurrentRace
          Refresh(true) → rebuilds vanilla RaceSelector at line 1925 → our Postfix re-applies filter
      restore s._selectedIndex = saved
```

### Dropdown order follows cultures.json, not engine order

`BuildGlobalIndexMap` iterates the **allow-list** (config order) and resolves each race name to its position in the engine's `FaceGen.GetRaceNames()` array. The result is a `globalIndices: List<int>` whose order matches the user's `races[]` config — `[uruk, orc, human]` → `[engineIdxOfUruk, engineIdxOfOrc, engineIdxOfHuman]`. This is what makes the dropdown show `uruk` in position 1 for Mordor instead of `human` (engine puts human at index 0).

### Force-switch logic ([`ShouldForceSwitchToDefault`](../../Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs))

The rebuilder force-switches the dropdown to filtered position 0 (`Races[0]`) under two conditions:

| Trigger | Behavior | Example |
|---------|----------|---------|
| `_selectedRace` is **not in** the culture's allow-list | Always switch | Player switched culture from Mordor (uruk) to Erebor (dwarf only) — uruk no longer allowed → switch to dwarf |
| **First Apply for a culture** AND `_selectedRace` is in the allow-list but NOT at position 0 | Switch to Races[0] | Player picks Isengard culture in narrative menus; vanilla initializes `_selectedRace = 0` (human, the engine default); human IS in Isengard's allow-list at filtered position 2; dropdown would otherwise default to human → switch to uruk_hai |
| Subsequent Apply (gender/age refresh) on the same culture | Preserve player's selection | Player chose berserker for Isengard; gender change triggers `Refresh(true)` → no switch, berserker stays |

Per-`FaceGenVM`-instance session tracking via `ConditionalWeakTable<FaceGenVM, RaceFilterSession>` records the last applied culture id. Switching culture mid-CC re-triggers "first Apply for this culture" → resets to the new Races[0]. A `[ThreadStatic]` recursion guard on the force-switch path prevents the downstream `Refresh(true)` from looping.

### Index-translation flow
```
User clicks filtered position N
  → SelectorVM.SelectedIndex setter fires (= N)
  → wrapped _onChange (our wrapper):
      saved = N
      mutate s._selectedIndex = globalIndices[N]   (via reflection, bypassing setter)
      call vanilla OnSelectRace(s):
          _selectedRace = s.SelectedIndex          (= globalIndices[N], correct global race ID)
          UpdateRaceAndGenderBasedResources()
              → UpdateFace(-20, _selectedRace) → SetRaceGenderAndAdjustParams updates _faceGenerationParams.CurrentRace
          Refresh(true) → rebuilds vanilla RaceSelector at line 1925 → our Postfix re-applies filter
      restore s._selectedIndex = saved
```

### Configuration
- File: [`Main/_Module/ModuleData/charactercreation/cultures.json`](../../Main/_Module/ModuleData/charactercreation/cultures.json)
- Per-culture entry: `"races": ["race_id_1", "race_id_2", ...]`
- **Order matters:** the first race in the array is the canonical default for that culture. Mordor's `["uruk", "orc", "human"]` defaults to uruk on first FaceGen open; Isengard's `["uruk_hai", "berserker", "human"]` defaults to uruk_hai.
- Reload scope: read once at the first `LoadCultures()` call by `CultureCreationDataProvider` (`Reuse.Singleton`). **Edits to cultures.json require a full Bannerlord restart**, not a save-load.
- Adding a new race: add the race ID to the `races` array of every culture that should permit it. Position determines whether it becomes the new default.
- Adding a new culture: add a culture entry with `culture_id` and `races` array. If `races` is empty or the culture is unknown, the filter falls back to showing all races (with a one-time warning per culture in the log).

### Current allow-lists (as of 2026-05-06)
| Culture | Allowed races |
|---------|---------------|
| `erebor` | `dwarf` |
| `mordor` | `uruk`, `orc`, `human` |
| `gundabad` | `pale_uruk`, `goblin`, `orc`, `human` |
| `dolguldur` | `dg_uruk`, `goblin`, `orc`, `human` |
| `mirkwood` / `lothlorien` / `rivendell` | `elf`, `human` |
| `isengard` | `uruk_hai`, `berserker`, `human` |
| `gondor` / `umbar` / `shaghana` / `abanissa` | `human` |
| `empire` / `vlandia` / `sturgia` / `aserai` / `battania` / `khuzait` | `human` |

### Race finalization (review-driven hardening)

`SetPlayerRace` honors the player's actual FaceGen choice rather than always assigning `cultureData.Races[0]` at finalize. Bannerlord assigns `Hero.CharacterObject.Race` from FaceGen output before finalize runs — `SetPlayerRace` reads that value, validates it via `IRaceManager.IsValidRaceId`, and accepts it only when both checks pass:

1. `IsValidRaceId(faceGenRaceId)` is `true` (gates against `GetRaceNameFromId`'s "human" fallback for unknown IDs — the fallback is for logging-and-survival, not for security decisions; see `csharp-architecture.md` "Lookup Functions With Fallbacks").
2. The resolved race name is in the culture's `races[]` allow-list (case-insensitive).

If either check fails, `SetPlayerRace` falls back to `cultureData.Races[0]`. This catches corrupt save migration, mid-mod-update transitions, and the silent-coercion bug where an invalid ID resolves to `"human"` and slips past the allow-list for cultures that allow `human`.

## Per-Culture Default BodyProperties (Patch29_CCBodyProperties)

When the player picks a culture during Character Creation, the player-character preview adopts a TAOM-defined `BodyProperties` key string for that culture instead of the vanilla random-within-min/max default. The body re-applies on every culture change, mirroring vanilla's "switch culture resets body" mental model. Cultures not configured fall back to vanilla behavior with no errors.

### Architecture

```
CharacterCreationContent.SetSelectedCulture(CultureObject)   ← vanilla
        │
        ▼  Postfix (Patch29_CCBodyProperties)
CharacterCreationContent_SetSelectedCulture_Patch
        │
        ▼  IoC.Resolve<ICCBodyPropertiesService>
ICCBodyPropertiesService.ApplyForCulture(string cultureId)
        │
        ├─ ICCBodyPropertiesProvider.GetBodyPropertiesXml(cultureId) → XML string or null
        └─ IPlayerBodyPropertiesAdapter.TryApplyFromXml(xml)
                │
                ├─ BodyProperties.FromString(xml, out parsed)        ← vanilla parser
                └─ CharacterObject.PlayerCharacter.UpdatePlayerCharacterBodyProperties(
                        parsed, playerChar.Race, playerChar.IsFemale)
```

Patch is a thin Postfix (≤35 lines) that delegates to the service. Service uses constructor-injected provider + adapter (no service-locator inside the service).

### Configuration

[`Main/_Module/ModuleData/charactercreation/cc_body_properties.xml`](../../Main/_Module/ModuleData/charactercreation/cc_body_properties.xml)

```xml
<Culture id="erebor">
  <BodyProperties version="4" key="<128-hex-character-key>" />
</Culture>
```

- Paste the `<BodyProperties version="4" key="..."/>` element exactly as produced by the in-game `BodyProperties.ToString()` (or copied from a save / face-customizer export).
- The key must be exactly **128 hex characters**; otherwise the entry is skipped with a warning in `rgl_log.txt`.
- Optional `weight` / `build` attributes on `<BodyProperties .../>` are honored if present; absent values default to `weight=0 build=0` (vanilla parser default).
- The `age` attribute is parsed by vanilla but NOT applied to the player — `Hero.Age` is computed from `BirthDay`, which we do not touch. Including `age=` has no visible effect.
- Cultures not listed fall back to the vanilla per-culture random body.
- The provider is `Reuse.Singleton`: edits to this file require restarting Bannerlord, not just reloading a save.
- Culture ID lookup is case-insensitive (provider lowercases on load); duplicate culture IDs log a warning, last-wins.
- The XML's leading comment block also serves as the editor cheatsheet for which `culture_id` maps to which LOTR faction (vlandia=Rohan, empire=Dunland, battania=Khand, etc.).

### Why Postfix on `SetSelectedCulture` (not on FaceGen open)

`SetSelectedCulture` is the canonical "player picked a culture" event. It fires:
- Once per culture-change in the narrative menus
- Idempotently if the player re-picks the same culture (vanilla setter doesn't gate)
- Before any subsequent CC stage (FaceGen, narrative continuation) renders

Patching the FaceGen open instead would miss the case where the body should update mid-narrative without opening FaceGen. Patching `ApplyCulture` (on finalize) would be too late — the preview wouldn't reflect the chosen body until after the player committed.

### What if the body XML fails to parse?

`PlayerBodyPropertiesAdapter.TryApplyFromXml` returns `false` if `BodyProperties.FromString` fails or if `CharacterObject.PlayerCharacter` is null. The service logs a warning ("adapter rejected body XML for X, could not apply") and the player character keeps its previous body. No crash, no exception bubble — the postfix catches and logs at warning level.

### Adding a new culture's default body

1. Open Bannerlord, enter Character Creation, design the body you want
2. Export the `<BodyProperties .../>` XML (in-game face-customizer's "copy" button, or from a save's `BodyProperties.ToString()` output)
3. Paste into `cc_body_properties.xml` under a new `<Culture id="...">` block
4. Restart Bannerlord (Singleton lifetime — save-load won't re-read)

## How to Add a New TAOM Culture to Character Creation

1. Add an entry to `Main/_Module/ModuleData/charactercreation/cultures.json` with `culture_id`, `races`, `starting_settlement`, body defaults, and skill/focus bonuses.
2. Add narrative option entries (prefixed `taom_`) to each of `parents_menu.json`, `childhood_menu.json`, and `youth_menu.json` with `culture_id` matching the new culture.
3. If the culture has no mounts (e.g., dwarves), ensure the CC equipment roster (`player_char_creation_{culture}_{titleType}_{gender}`) omits the Horse slot. Patch20_NarrativeHorseGuard will handle the crash guard automatically.
4. The culture must exist in `MBObjectManager` (i.e., defined in `spcultures.xml` or `taom_spcultures.xml`) or it will be skipped with a warning.

## GitHub Issue
- **Race filter (Patch9_RaceFilter re-implementation):** [#107](https://github.com/haterade22/TAOM/issues/107) — closed 2026-05-06
- **Per-culture default BodyProperties (Patch29_CCBodyProperties):** added in same session, not separately ticketed
