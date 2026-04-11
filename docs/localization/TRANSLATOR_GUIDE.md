# TAOM Translation Guide

This guide is for anyone translating TAOM (Tales From the Age of Men) into another language. It covers the files you need to edit, the format rules, and how to test your work.

## Overview

TAOM adds ~1,950 translatable strings across three categories:

| File | What it contains | Entries |
|------|-----------------|---------|
| `std_taom_module_strings_{locale}.xml` | Faction names, titles, culture terms, UI labels | ~645 |
| `std_taom_wanderer_strings_{locale}.xml` | Wanderer backstories for all cultures | ~1,177 |
| `std_taom_named_companion_strings_{locale}.xml` | Named companion dialog (Aragorn, Legolas, Gimli, etc.) | ~126 |

Your translation files live in `Main/_Module/ModuleData/Languages/{LANG}/` where `{LANG}` is your language code (e.g., `PL` for Polish, `DE` for German).

## File Format

Each translation file is XML with this structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xmlns:xsd="http://www.w3.org/2001/XMLSchema"
      type="string">
  <tags>
    <tag language="Polski" />
  </tags>
  <strings>
    <string id="taom_str_faction_ruler.empire" text="Chieftain" />
    <!--          ^^ DO NOT CHANGE               ^^ TRANSLATE THIS -->
  </strings>
</base>
```

**Rules:**
- The `id` attribute is the localization key. **Never change it.**
- The `text` attribute is what you translate.
- Keep the file encoding as UTF-8.
- Use `&amp;` for `&`, `&quot;` for `"`, `&lt;` for `<`, `&gt;` for `>` inside attribute values.

## What to Preserve (Do Not Translate)

### Variable Placeholders

Variables are wrapped in `{CURLY_BRACES}`. Copy them exactly as-is:

```xml
<!-- English -->
<string id="..." text="the King, {RULER.NAME}" />

<!-- Polish -->
<string id="..." text="Krol, {RULER.NAME}" />
```

Common variables: `{RULER.NAME}`, `{FACTION_NAME}`, `{TOWN_NAME}`, `{HERO.NAME}`

### Gender Conditionals

The `{?CONDITION}...{?}...{\?}` syntax handles gender. Translate the words inside, but preserve the syntax exactly:

```xml
<!-- English -->
<string id="..." text="{?RULER.GENDER}Lady{?}Lord{\?} {RULER.NAME}" />

<!-- Polish -->
<string id="..." text="{?RULER.GENDER}Pani{?}Pan{\?} {RULER.NAME}" />
```

### Proper Nouns (Tolkien Names)

Most Tolkien names should stay in their original form unless there is an established translation in your language's published Tolkien works:

| Keep as-is | Examples |
|-----------|----------|
| Place names | Gondor, Mordor, Minas Tirith, Rivendell, Osgiliath |
| Character names | Aragorn, Legolas, Gimli, Faramir, Imrahil |
| Faction names | Rohirrim, Dunlendings, Variags |

For Polish specifically, refer to the Skibniewska and Loziński translations for established conventions (e.g., "Rohan" stays "Rohan", "Mordor" stays "Mordor", "Dwarf" = "Krasnolud").

## Getting Started

### 1. Get the Template Files

Template files with English text are pre-generated in your language folder. If they're empty or you need to regenerate them:

```bash
python tools/generate_translation_template.py --apply PL
```

This populates all three files with English text as placeholder content.

### 2. Translate

Open each file and replace the English `text` values with your translation. You can work through them in any order, but the module strings file is the most impactful since faction names appear everywhere in-game.

### 3. Test In-Game

1. Build the mod: `./build.ps1`
2. Launch Bannerlord
3. Go to **Options > Game > Language** and select your language
4. Start or load a campaign
5. Check: faction names on the map, wanderer dialog when recruiting, companion backstories

### 4. Submit Your Translation

**Option A: GitHub Pull Request (preferred)**
- Fork the repository
- Edit only your 3 translation files in `Languages/{LANG}/`
- Open a PR targeting the `master` branch

**Option B: Send Files Directly**
- Zip your 3 translated XML files
- Share them via Discord or the mod page

## String Categories Explained

### Module Strings (~645)

These define how factions, cultures, and kingdoms appear in-game:

| Pattern | Example | What it controls |
|---------|---------|-----------------|
| `str_faction_ruler.*` | "King", "Chieftain" | Ruler title |
| `str_faction_official.*` | "a noble of Rohan" | How NPCs refer to faction members |
| `str_adjective_for_faction.*` | "Rohirric" | Adjective form |
| `str_neutral_term_for_culture.*` | "Rohirrim" | Plural demonym |
| `TAOM_*_name` / `TAOM_*_text` | Kingdom names and descriptions | Encyclopedia entries |
| `taom_career` | "Career" | UI label |
| `taom_alliance_*` | "These kingdoms can never be allied." | Diplomacy messages |
| `taom_precompile_*` | "Pre-compile Shaders" | Main menu option |

### Wanderer Strings (~1,177)

Each wanderer has 7 dialog entries:

| ID Pattern | Purpose |
|-----------|---------|
| `prebackstory.*` | Opening line when you first meet them |
| `backstory_a.*` | Their background story (part 1) |
| `backstory_b.*` | The conflict or turning point (part 2) |
| `backstory_c.*` | Current situation (part 3) |
| `response_1.*` | Player's sympathetic response |
| `response_2.*` | Player's skeptical response |
| `backstory_d.*` | Wanderer's offer to join |

These are narrative text. Maintain the tone and personality of each character.

### Named Companion Strings (~126)

Same 7-entry structure as wanderers, but for named Tolkien characters (Aragorn, Legolas, Gimli, etc.). These should feel consistent with the characters as portrayed in Tolkien's works.

## Known Limitations

**Character creation narratives** (~386 text+description pairs across 5 JSON files in `ModuleData/charactercreation/`) are not yet translatable. They use a different format that doesn't go through the localization system. This will be addressed in a future update.

## Questions?

Open an issue on GitHub or reach out on the mod's Discord channel.
