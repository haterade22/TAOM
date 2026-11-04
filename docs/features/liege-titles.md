# Liege Titles

## Overview

Every kingdom culture has a liege title for its ruler, male and female: "Lord of Gondor", "Lady of the Galadhrim",
"King under the Mountain", "Dark Lord of Mordor". The coronation scene's title line and the lords' dialog lines that
name their liege ("I am Denethor II, Lord of Gondor", the vassal oath) read it.

## Why This Exists

- **Vanilla behavior:** the text `str_liege_title` (and `str_liege_title_female`) has one variation per vanilla culture
  id, each tagged with that culture's conversation tag. The engine reads it two ways:
  - `GameTexts.FindText(id, variation)` matches the id suffix (`str_liege_title.gondor`), first match wins, tags
    ignored. The coronation scene (`BecomeKingSceneNotificationItem.TitleText`) reads it this way with the kingdom's
    culture id.
  - `ConversationManager.FindMatchingTextOrNull(id, character)` ignores the suffix and scores each variation's tags
    against the faction leader: an untagged variation scores 0, a tag that does not apply rules the variation out.
    `LordConversationsCampaignBehavior.GetLiegeTitle` and the vassal oath read it this way.
- **TAOM requirement:** the 17 TAOM-only kingdom cultures had no variation, so the coronation title read "ERROR: Text
  with id str_liege_title doesn't exist!" and the dialog lines got no title (#955). Dunland (`empire`) reads a fixed
  `str_become_king_empire` line instead of a liege title, which said "Emperor of Calradia".
- **Without this feature:** the error text in every TAOM coronation, and empty liege names in dialogs.

## Architecture

A variation for a TAOM culture needs both an id suffix (for the coronation) and a tag of its own (for dialogs): an
untagged row would score 0 for every TAOM-culture leader and win for all of them. No vanilla tag tests a TAOM culture,
so `Main/Features/LiegeTitles/TaomCultureTags.cs` defines one `ConversationTag` per culture, `StringId`
`taom_culture_<culture>`, true when the character's culture is that culture. The engine finds them itself:
`ConversationManager.InitializeTags` instantiates every `ConversationTag` subclass in the game assemblies at each
campaign start and load. That is why each class is concrete and parameterless with a unique id, and why there is no
shared base class (it would be instantiated too); the rule is in `.claude/rules/csharp-patterns.md`.

The six reused vanilla ids keep their vanilla rows, rewritten by `comment_strings.xslt` (Brenin of Dunland, King of
Dale, Taskral of Harad, Khudriag of Khand, King of the Mark, Loke-Kan of Rhun). Dunland's coronation line is TAOM's
own: `module_strings.xslt` removes vanilla's `str_become_king_empire` and `taom_module_strings.xml` supplies "was
hailed as Brenin of Dunland" under a TAOM key.

The coronation keys on the kingdom's culture and dialogs on the ruler's own culture, so a ruler of another culture is
crowned with one title and addressed with the other, as in vanilla.

## Configuration

None. The 48 rows (24 cultures, a male and a female title each) are in `Main/_Module/ModuleData/taom_module_strings.xml` (keys `taom_str_liege_title.<culture>` and
`taom_str_liege_title_female.<culture>`), seeded in English in the 12 language files; translation is owed in #763.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LiegeTitles/TaomCultureTags.cs` | One conversation tag per TAOM-only kingdom culture |
| `Main/_Module/ModuleData/taom_module_strings.xml` | The 48 tagged title rows and Dunland's coronation line |
| `Main/_Module/ModuleData/module_strings.xslt` | Removes vanilla's Calradian emperor line |
| `Main/_Module/ModuleData/comment_strings.xslt` | The six vanilla-id titles (existing) |

## Dependencies

None. The coronation scene is also where Crown Guard ([crown-guard.md](crown-guard.md)) acts.

## Tests

- `TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`: every kingdom culture has a male and female
  title, each TAOM row's only tag is its own culture's tag, every TAOM conversation tag instantiates with a unique id
  that no vanilla tag uses (game install), Dunland's line replaces vanilla's emperor line, no title mentions Calradia.

## Adding a kingdom culture

Add a tag class to `TaomCultureTags.cs` and two rows (male, female) to `taom_module_strings.xml`, then seed the
language files (`/localize`). `EveryKingdomCulture_HasMaleAndFemaleLiegeTitle` fails until both exist.

## In-game check

`taom.play_coronation gondor`, `lothlorien` (a queen: "Lady of the Galadhrim") and Dunland ("hailed as Brenin of
Dunland"); talk to a Gondor lord for the first time ("..., Lord of Gondor") and to a Rohan lord ("King of the Mark",
unchanged). Not yet checked in game.

## Changelog

- 2026-10-20: added (#955).

## GitHub Issue

- **Issue:** #955 Coronation title shows an engine error for TAOM cultures
- **Status:** Closed, `triage-needs-ingame`

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
