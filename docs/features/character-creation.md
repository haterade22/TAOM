# Character Creation

## Overview
Replaces Bannerlord's vanilla character creation content with TAOM-specific cultures, narrative backstory menus, and race assignment. On finalization it sets the player's race based on their chosen culture and teleports them to a culture-appropriate starting settlement.

## Why This Exists
- **Vanilla behavior:** Character creation offers 6 human cultures (empire, vlandia, sturgia, aserai, battania, khuzait) with fixed narrative options tied to those cultures.
- **TAOM requirement:** The mod adds 10+ custom cultures (Gondor, Erebor, Mordor, Rohan, elves, etc.) each needing their own race assignment, starting settlement, narrative backstory options, and body proportions.
- **Without this feature:** Custom cultures would not appear in character creation; the player would start as a human with default vanilla placement; no-mount cultures (Erebor dwarves) would crash with a `NullReferenceException` when the narrative scene tried to spawn a horse actor.

## Architecture

### Design Challenge
Three separate problems must be solved:

1. **Registering custom cultures** — `CharacterCreationManager` is provided by SandBox's handler at priority 800. TAOM must hook in at a higher priority (1050) via `AfterInitializeContent` so it runs after vanilla setup.
2. **Replacing narrative menus** — The parent/childhood/youth narrative menus exist but contain vanilla option objects. TAOM must remove those and substitute JSON-driven TAOM options without breaking the menu structure.
3. **No-mount crash** — Vanilla's `GetYouthMenuNarrativeMenuCharacterArgs`, `GetAdultMenuNarrativeMenuCharacterArgs`, and `GetAgeSelectionMenuNarrativeMenuCharacterArgs` unconditionally read `DefaultEquipment[Horse].Item.StringId` to build a horse `NarrativeMenuCharacterArgs`. When a culture's CC equipment roster has no horse slot the read returns null and the game crashes. A separate crash fires in `SpawnNonHumanNarrativeMenuCharacter` when it tries to look up the null horse item ID in `MBObjectManager`.

### Solution Approach
- `CharacterCreationRegistrationBehavior` (a `CampaignBehaviorBase`) listens for `OnCharacterCreationInitializedEvent` and registers `TaomCharacterCreationContentHandler` at priority 1050.
- `TaomCharacterCreationContentHandler` implements `ICharacterCreationContentHandler`. Its `AfterInitializeContent` calls into `ICharacterCreationContentService` to register cultures and replace narrative menus.
- `CharacterCreationContentService` reads culture data from `ICultureCreationDataProvider` (cultures.json) and narrative options from `INarrativeDataProvider` (parents/childhood/youth JSON files). Vanilla cultures are skipped by a hard-coded allow-list.
- On `OnCharacterCreationFinalize`, the service sets the player's race via `IRaceManager`/`IHeroRosterAdapter` and teleports `MobileParty.MainParty` to the culture's starting settlement.
- `Patch20_NarrativeHorseGuard` contains four Harmony patches: three Prefix patches intercept the three `GetXxxMenuNarrativeMenuCharacterArgs` methods and short-circuit to return a player-only list when no horse is present; one Finalizer on `SpawnNonHumanNarrativeMenuCharacter` swallows the resulting `ArgumentNullException(key)`.

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
        |-- INarrativeDataProvider      --> charactercreation/{parents,childhood,youth}_menu.json
        |-- IEquipmentRosterProvider    --> MBEquipmentRoster lookups
        |-- IRaceManager / IHeroRosterAdapter --> race assignment on finalize

Harmony Patch20_NarrativeHorseGuard
        |-- Prefix: GetYouthMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAdultMenuNarrativeMenuCharacterArgs
        |-- Prefix: GetAgeSelectionMenuNarrativeMenuCharacterArgs
        |-- Finalizer: SpawnNonHumanNarrativeMenuCharacter
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
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | Core logic: culture registration, narrative menu replacement, finalization |
| `Main/Features/CharacterCreation/CultureCreationDataProvider.cs` | Parses cultures.json |
| `Main/Features/CharacterCreation/NarrativeDataProvider.cs` | Parses narrative menu JSON files with concurrent cache |
| `Main/Features/CharacterCreation/NarrativeMenuBuilder.cs` | Builds `NarrativeMenuOption` objects from `NarrativeOptionDefinition` list |
| `Main/Features/CharacterCreation/EquipmentRosterProvider.cs` | Looks up `MBEquipmentRoster` by string ID |
| `Main/Features/CharacterCreation/Models/CultureCreationData.cs` | POCO: culture metadata including race, settlement, body defaults |
| `Main/Features/CharacterCreation/Models/NarrativeOptionDefinition.cs` | POCO: one narrative backstory choice |
| `Main/Features/CharacterCreation/Hooks/FaceGen_GetRaceNames_Patch.cs` | Patch9_RaceFilter — intentional no-op; previous filtering broke FaceGenVM index lookup |
| `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` | Patch20_NarrativeHorseGuard — 4 patches guarding no-mount culture crashes |
| `Main/_Module/ModuleData/charactercreation/cultures.json` | Culture metadata config |
| `Main/_Module/ModuleData/charactercreation/parents_menu.json` | Parent backstory options |
| `Main/_Module/ModuleData/charactercreation/childhood_menu.json` | Childhood backstory options |
| `Main/_Module/ModuleData/charactercreation/youth_menu.json` | Youth backstory options |

## Dependencies
- `ICultureCreationDataProvider` — reads cultures.json
- `INarrativeDataProvider` — reads narrative JSON files
- `IEquipmentRosterProvider` — looks up CC equipment rosters
- `IRaceManager` — maps race name to race ID
- `IHeroRosterAdapter` — sets hero race on finalization
- `IPathService` — resolves `ModuleDataPath`
- `IModLogger` — logging throughout

## Tests
| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/CharacterCreation/CharacterCreationContentServiceTests.cs` | `RegisterCustomCultures`, `RegisterNarrativeMenus`, `OnCharacterCreationFinalize`, `SetPlayerRace` |
| `TAOM.Tests/Features/CharacterCreation/CultureCreationDataProviderTests.cs` | JSON parsing, missing file handling, `GetCultureData` lookup |
| `TAOM.Tests/Features/CharacterCreation/NarrativeDataProviderTests.cs` | JSON parsing, caching, `GetMenuOptionsForCulture` filtering, missing file handling |
| `TAOM.Tests/Features/CharacterCreation/NarrativeMenuBuilderTests.cs` | Option construction, empty input handling |
| `TAOM.Tests/Features/CharacterCreation/GetRaceNamesHookTests.cs` | No-op postfix behavior |

## How to Add a New TAOM Culture to Character Creation

1. Add an entry to `Main/_Module/ModuleData/charactercreation/cultures.json` with `culture_id`, `races`, `starting_settlement`, body defaults, and skill/focus bonuses.
2. Add narrative option entries (prefixed `taom_`) to each of `parents_menu.json`, `childhood_menu.json`, and `youth_menu.json` with `culture_id` matching the new culture.
3. If the culture has no mounts (e.g., dwarves), ensure the CC equipment roster (`player_char_creation_{culture}_{titleType}_{gender}`) omits the Horse slot. Patch20_NarrativeHorseGuard will handle the crash guard automatically.
4. The culture must exist in `MBObjectManager` (i.e., defined in `spcultures.xml` or `taom_spcultures.xml`) or it will be skipped with a warning.

## GitHub Issue
- **Issue:** Unknown (not referenced in commit messages)
- **Status:** Active
