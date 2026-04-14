# Career Selection in Character Creation

## Overview

Adds a 6th narrative menu stage to Bannerlord's character creation flow that lets players choose their career from culture-eligible options. Each career grants skill and attribute bonuses during CC, and the selected career is assigned via the existing CareerSystem during finalization.

## Why This Exists

- **Vanilla behavior:** Bannerlord has 5 narrative stages (parent, childhood, education, youth, adulthood). No career/class system.
- **TAOM requirement:** TAOM has 50 careers across 16 cultures, but the CC flow auto-assigned the first eligible career with no player choice. Most cultures have 2-4 careers, so players always got the same one.
- **Without this feature:** Players start every game with the same career for their culture. No meaningful class selection during character creation.

## Architecture

### Design Challenge

Bannerlord's `CharacterCreationManager` manages narrative menus as a linked list via `InputMenuId` -> `StringId`. Adding a new stage requires inserting into this chain without Harmony patches or reflection — just using the public `AddNewMenu()` API.

Cultures with no eligible careers (shaghana, abanissa) would produce an empty menu, causing a `KeyNotFoundException` crash in vanilla's `TrySwitchToNextMenu` when `SelectedOptions` has no entry.

### Solution Approach

- **Extension point:** `CharacterCreationManager.AddNewMenu()` — inserts a NarrativeMenu with `InputMenuId = "narrative_adulthood_menu"` so the CC flow naturally traverses to it after adulthood.
- **Data source:** Career definitions from `ICareerRegistry` (display names, descriptions, eligible cultures) + CC bonus data from `career_menu.json` (skills, attributes).
- **Fallback safety:** A universal "No specialization" option is always present but only visible for cultures with no eligible careers, preventing the empty-menu crash.

### Component Diagram

```
taom_careers.xml (career defs + EligibleCultures)
        |
  ICareerRegistry (runtime career data)
        |
career_menu.json -----> CareerMenuDataProvider (CC bonus data)
                              |
                        CareerMenuService (builds NarrativeMenu + options)
                              |
                    TaomCharacterCreationContentHandler (registers menu)
                              |
                    CharacterCreationContentService.AssignCareer()
                              |
                    ICareerCreationHandler.OnCareerSelected()
```

## Configuration

### Config File: `Main/_Module/ModuleData/charactercreation/career_menu.json`

Each entry maps a career to its CC skill/attribute bonuses. Career names, descriptions, and culture eligibility come from `taom_careers.xml` via the career registry.

| Field | Type | Description |
|-------|------|-------------|
| `career_string_id` | string | Must match a `Career id` in `taom_careers.xml` |
| `skills` | string[] | Skills to boost (from: OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding, Athletics, Crafting, Scouting, Tactics, Roguery, Charm, Leadership, Trade, Steward, Medicine, Engineering) |
| `attribute` | string | Attribute to boost (from: Vigor, Control, Endurance, Cunning, Social, Intelligence) |
| `focus_to_add` | int | Focus points added to each skill (default: 1) |
| `skill_level_to_add` | int | Skill XP added to each skill (default: 10) |
| `attribute_level_to_add` | int | Attribute points added (default: 1) |

### Current Values

50 entries, one per career. All use standard bonuses (1 focus, 10 skill XP, 1 attribute point). Skills and attributes are thematically matched to each career.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CharacterCreation/CareerMenuService.cs` | Builds NarrativeMenu with career options, stores player selection |
| `Main/Features/CharacterCreation/ICareerMenuService.cs` | Service interface |
| `Main/Features/CharacterCreation/CareerMenuDataProvider.cs` | Loads and caches `career_menu.json` |
| `Main/Features/CharacterCreation/ICareerMenuDataProvider.cs` | Data provider interface |
| `Main/Features/CharacterCreation/Models/CareerMenuOptionDefinition.cs` | CC bonus data model |
| `Main/Features/CharacterCreation/CharacterCreationContentService.cs` | CC orchestrator — `AssignCareer()` uses stored selection |
| `Main/Features/CharacterCreation/TaomCharacterCreationContentHandler.cs` | Entry point — calls `RegisterCareerMenu()` |
| `Main/Features/CharacterCreation/CharacterCreationIoC.cs` | DryIoc registration |
| `Main/_Module/ModuleData/charactercreation/career_menu.json` | 50 career CC bonus definitions |

## Dependencies

- `ICareerRegistry` (CareerSystem) — provides career definitions, display names, eligible cultures
- `ICareerCreationHandler` (CareerSystem) — assigns career during finalization
- `IPathService` (Core) — resolves ModuleData path for JSON loading

## Tests

- `TAOM.Tests/Features/CharacterCreation/CareerMenuServiceTests.cs` — 12 tests covering option building, culture filtering, selection storage, fallback option, registry edge cases
- `TAOM.Tests/Features/CharacterCreation/CareerMenuDataProviderTests.cs` — 9 tests covering JSON loading, parsing, caching, error handling, career lookup

## How to Add a Career to CC

1. Add the `<Career>` element to `Main/_Module/ModuleData/career_system/taom_careers.xml` with `<EligibleCultures>`
2. Add a matching entry to `Main/_Module/ModuleData/charactercreation/career_menu.json` with the same `career_string_id` and appropriate skill/attribute bonuses
3. No code changes needed — the service reads both at runtime

## How to Add Careers for a New Culture

1. Ensure the culture is registered in `cultures.json` for CC
2. Add career(s) to `taom_careers.xml` with the culture in `<EligibleCultures>` (use correct culture ID — vanilla IDs for XSLT cultures)
3. Add matching entries to `career_menu.json`
4. The fallback "No specialization" option will stop appearing for that culture once it has at least one career

## Performance

Character creation runs once per new game. All data is loaded and cached on first access. No hot-path concerns.
