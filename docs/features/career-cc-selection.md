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

## Career Screen UI Sprites

### Sprite Atlas

Career sprites use a **dedicated atlas** `ui_taom_career_system` (registered in `GUI/SpriteParts/Config.xml` with `<AlwaysLoad />`). This prevents large career images from overflowing the main `ui_taom` atlas and corrupting other UI.

### Sprite Dimensions

| Sprite Type | Widget Size | Generate At | Format | Location |
|------------|-------------|-------------|--------|----------|
| Career portrait | 400x200 | 800x400 | Landscape 16:9 | `GUI/SpriteParts/ui_taom_career_system/CareerSystem/Portraits/` |
| Ability icon | 120x120 | 256x256 | Square | `GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/` |
| Career button | varies | 1024x256 | Wide strip | `GUI/SpriteParts/ui_taom_career_system/CareerSystem/` |

Generate at 2x the widget size for sharpness — the engine downscales at runtime.

**CRITICAL:** Images MUST be resized to the target dimensions BEFORE placing in SpriteParts. Oversized images (1024x1024+) overflow the sprite atlas and corrupt ALL UI.

### Sprite Naming Convention

| Type | Filename | Registered Name (TAOMSpriteData.xml) |
|------|----------|--------------------------------------|
| Portrait | `career_{career_id}_portrait.png` | `CareerSystem\Portraits\career_{career_id}_portrait` |
| Ability icon | `{ability_template_id}.png` | `CareerSystem\Abilities\{ability_template_id}` |
| Career button | `career_button_placeholder.png` | `CareerSystem\career_button_placeholder` |

### How to Add a Career Portrait

1. Generate a landscape image (Midjourney: `--ar 16:9`, ChatGPT: specify landscape)
2. Resize to **800x400** using Python: `Image.open(f).resize((800, 400), Image.LANCZOS).save(f)`
3. Save as `GUI/SpriteParts/ui_taom_career_system/CareerSystem/Portraits/career_{career_id}_portrait.png`
4. Register in `Main/_Module/GUI/TAOMSpriteData.xml` — add a `<GenericSprite>` entry:
   ```xml
   <GenericSprite>
     <Name>CareerSystem\Portraits\career_{career_id}_portrait</Name>
     <SpritePartName>CareerSystem\Portraits\career_{career_id}_portrait</SpritePartName>
   </GenericSprite>
   ```
5. Copy to game install: `E:\Steam\...\Modules\TAOM\GUI\SpriteParts\ui_taom_career_system\CareerSystem\Portraits\`
6. Run sprite generator, then rebuild
7. The VM auto-prefixes `CareerSystem\Portraits\` to the `portrait_sprite` value from `taom_careers.xml`

### How to Add an Ability Icon

1. Generate a square image (Midjourney: `--ar 1:1`)
2. Resize to **256x256**
3. Save as `GUI/SpriteParts/ui_taom_career_system/CareerSystem/Abilities/{ability_template_id}.png`
4. Register in `Main/_Module/GUI/TAOMSpriteData.xml` — add a `<GenericSprite>` entry:
   ```xml
   <GenericSprite>
     <Name>CareerSystem\Abilities\{ability_template_id}</Name>
     <SpritePartName>CareerSystem\Abilities\{ability_template_id}</SpritePartName>
   </GenericSprite>
   ```
5. Copy to game install and run sprite generator
6. The VM constructs the path as `CareerSystem\Abilities\{ability_template_id}`

### Sprite Migration Checklist

When moving sprites between atlas categories:
1. Move PNGs in repo
2. **Delete old PNGs from game install** (build only copies, doesn't delete)
3. Update `CategoryName` in TAOMSpriteData.xml `<SpritePart>` entries (or remove — generator recreates them)
4. Register new category in `GUI/SpriteParts/Config.xml` if it's new
5. Run sprite generator
6. Verify no "duplicate key" errors

### AI Art Generation

**Art style:** Gritty painterly oil painting, Alan Lee / John Howe LOTR concept art. Muted battlefield tones, desaturated palette, thick brushstrokes, atmospheric dust and haze.

**Midjourney prompt template (portraits):**
```
/imagine [scene description], single warrior, gritty painterly oil painting, classic fantasy concept art, muted [palette] tones, thick brushstrokes, atmospheric haze, wide cinematic landscape composition --ar 16:9 --s 250 --no text, borders, watermark, cartoon, anime, bright colors, multiple soldiers, army, photorealistic, studio lighting, sharp focus
```

**Midjourney prompt template (ability icons):**
```
/imagine [ability visual], painterly fantasy ability icon, muted [palette] tones, dark atmosphere, game UI ability button, square format, centered --ar 1:1 --s 250 --no text, borders, watermark, glowing, magic, bright colors, people, hands
```

**ChatGPT/DALL-E prompt template (portraits):**
```
Generate a landscape image. [Scene description]. Single warrior only. Art style: gritty painterly oil painting, classic fantasy concept art, muted [palette] tones, thick brushstrokes, atmospheric dust and haze. Wide cinematic landscape composition. No cartoon, no anime, no text, no watermarks.
```

### Current Portrait Status

| Faction | Career | Portrait | Ability Icon |
|---------|--------|----------|-------------|
| Gondor | Ranger of Ithilien | Done | Done (Ambush) |
| Gondor | Captain of Osgiliath | Done | Done (Hold the Line) |
| Gondor | Knight of Belfalas | Done | Done (Stampede) |
| Rohan | Marksman of Aldburg | Done | Done (Light Fletching) |
| Rohan | Eotheod Windrider | Done | Done (Warcry of Eorl) |
| Rohan | Watchman of Stangard | Done | Done (Stand Fast) |
| All others | — | Pending | Pending |

## Performance

Character creation runs once per new game. All data is loaded and cached on first access. No hot-path concerns.

## GitHub Issues

- **Issue:** #84 — [feat: Career selection in character creation](https://github.com/haterade22/TAOM/issues/84)
- **Status:** Closed

## Codex Review

- **Review:** `docs/reviews/codex-adversarial-career-cc-2026-04-14.md`
- **Review Log:** Review #24 in `docs/reviews/REVIEW-LOG.md`
- **Findings:** 1 HIGH (empty menu crash — fixed), 1 MEDIUM (test gap — fixed)
