# Liege Titles

## Overview

Every kingdom culture has a liege title for its ruler, male and female: "Lord of Gondor", "Lady of the
Galadhrim", "King under the Mountain", "Dark Lord of Mordor". The coronation scene's title line and the lords'
dialog lines that name their liege ("I am Denethor II, Lord of Gondor") read it. Ported from the 1.5.x line
(#955) by plan 133.

## Why This Exists

- **Vanilla behavior (v1.4.8):** the text `str_liege_title` (and `str_liege_title_female`) has one variation per
  vanilla culture id, each tagged with that culture's conversation tag. `GameTextManager.LoadFromXML` splits a row
  id on `.` into the text id and the variation id and reads the row's `<tags>`. The engine reads the text two ways:
  - `GameTexts.FindText(id, variation)` matches the id suffix. The coronation scene
    (`BecomeKingSceneNotificationItem.TitleText`, dump `:23-34`) reads it this way with the kingdom's culture id,
    except for the `empire` culture, which reads `str_become_king_empire` instead (`:30`).
  - `ConversationManager.FindMatchingTextOrNull(id, character)` (`ConversationManager.cs:983`) ignores the suffix
    and scores each variation's tags: an untagged variation scores 0, a tag that does not apply rules the
    variation out. `LordConversationsCampaignBehavior.GetLiegeTitle` reads it this way for the faction leader.
- **TAOM requirement:** the 16 TAOM-only kingdom cultures had no variation, so the coronation title read an engine
  error ("ERROR: Text with id str_liege_title doesn't exist! Variation: ...", `GameTextManager.cs:91`) and the
  dialog lines got no title (#955). Dunland (`empire`) read vanilla's "Emperor of Calradia" line.
- **Without this feature:** the error text in every TAOM coronation, and empty liege names in dialogs.

## Architecture

A variation for a TAOM culture needs both an id suffix (for the coronation) and a tag of its own (for dialogs): an
untagged row would score 0 for every TAOM-culture leader and win for all of them. No vanilla tag tests a TAOM
culture, so `Main/Features/LiegeTitles/TaomCultureTags.cs` defines one `ConversationTag` per culture, `StringId`
`taom_culture_<culture>`, true when the character's culture is that culture. The engine finds them itself:
`ConversationManager.InitializeTags` (run by its constructor, `ConversationManager.cs:239`) instantiates every
`ConversationTag` subclass in the game assemblies at each campaign start and load. That is why each class is
concrete and parameterless with a unique id, and why there is no shared base class (it would be instantiated too);
the rule is in `.claude/rules/csharp-patterns.md`.

The six reused vanilla ids keep their vanilla rows, rewritten by `comment_strings.xslt` (Brenin of Dunland, King of
Dale, Taskral of Harad, Khudriag of Khand, King of the Mark, Loke-Kan of Rhun). Dunland's coronation line is
TAOM's own: `module_strings.xslt` removes vanilla's `str_become_king_empire` and `taom_module_strings.xml`
supplies "was hailed as Brenin of Dunland" under a TAOM key.

The coronation keys on the kingdom's culture and dialogs on the ruler's own culture, so a ruler of another culture
is crowned with one title and addressed with the other, as in vanilla.

## Configuration

None. The 32 rows are in `Main/_Module/ModuleData/taom_module_strings.xml` (keys `taom_str_liege_title.<culture>`
and `taom_str_liege_title_female.<culture>`), seeded in English in the 12 language files until the translation
pass.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LiegeTitles/TaomCultureTags.cs` | One conversation tag per TAOM-only kingdom culture (16) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 32 tagged title rows and Dunland's coronation line |
| `Main/_Module/ModuleData/module_strings.xslt` | Removes vanilla's Calradian emperor line |
| `Main/_Module/ModuleData/comment_strings.xslt` | The six vanilla-id titles (existing) |

## Tests

- `TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`: every kingdom culture has a male and female
  title, each TAOM row's only tag is its own culture's tag, every TAOM conversation tag instantiates with a unique
  id that no vanilla tag uses, Dunland's line replaces vanilla's emperor line, no title mentions Calradia.

## Adding a kingdom culture

Add a tag class to `TaomCultureTags.cs` and two rows (male, female) to `taom_module_strings.xml`, then seed the
language files (`/localize`). `EveryKingdomCulture_HasMaleAndFemaleLiegeTitle` fails until both exist.

## In-game check

The next coronation in a TAOM-only realm shows its title (for example "Lord of Gondor"), not the error text;
Dunland's says "hailed as Brenin of Dunland"; a first conversation with a Gondor lord names "Lord of Gondor"; a
Rohan lord still says "King of the Mark". Not yet checked in game.

## Changelog

- 2026-11-05: ported from the 1.5.x line (#955, plan 133).

## GitHub Issue

- **Issue:** #955 Coronation title shows an engine error for TAOM cultures
- **Status:** ported to 1.4.x; in-game check owed
