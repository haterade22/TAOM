# Lore Greetings

## Overview

Characters who know each other in the books greet each other by lore. When the player talks to a lord or companion, the greeting is a line from that person to the one he knows: to the player, when the player is himself a lore person (a lord taken over with the Player Switcher, or a lore companion), or to a lore companion riding in the player's party. Lords who know the person the player plays treat him as that person from the first word, not as a stranger. Text only: the line replaces the met greeting, and every other line of the conversation stays as it was.

## Why This Exists

- **Vanilla behavior:** a lord's met greeting is a tag-scored `str_context_line` row (Encounter Lines adds rows by people and by who the player is to them). A lord who has not met the player opens with "I am X. And who are you?", whoever the player is.
- **TAOM requirement:** Luiz, playing the mod: "characters that have met and know each other should have a custom greeting grounded in the LOTR lore". Thranduil should greet Legolas as his son, Dáin should greet Gimli, son of Glóin, and Elrond should not ask Aragorn who he is.
- **Without this feature:** playing Legolas, his own father introduces himself to him; Dáin greets Gimli in the same words as any dwarf would greet a stranger.

## Architecture

### Design Challenge

- The NPC's first line is the first `start` sentence whose condition is true, highest priority first (`ConversationManager.ProcessPartnerSentence` calls `GetSentenceOptions(onlyPlayer: false, processAfterOneOption: false)`, which returns on the first true condition). A new `start` line would have to repeat every condition of the attack, parley, surrender, quest and grievance lines it might outrank.
- The met greeting is `start_default` (`{=!}{VOICED_LINE}`, priority 100). Its condition, `conversation_lord_greets_over_24_hours_on_condition`, sets `VOICED_LINE` through `conversations_set_voiced_line`, which is where Encounter Lines' rows land.
- A hero with `HasMet` false never reaches it: `start_lord_unmet`, `unmet_in_main_mobile_party`, `start_wanderer_unmet` (110) and `start_attacking_unmet` (100) all end in private `ConversationUseMeetingDialogs()`, which calls `Hero.SetHasMet()` and starts the introduction. `SetHasMet` also sets `LastMeetingTimeWithPlayer = CampaignTime.Now` (`Hero.cs:1379-1383`), which would make the 24-hour salutation win.
- The Player Switcher keeps the hero object (`ChangePlayerCharacterAction.Apply`), so the player's hero id is the lore lord's id, and a lord the old player never met has `HasMet` false.

### Solution Approach

Two patches in `Patch177_LoreGreetings`, applied at GameInit by `LoreGreetingsModule`:

- **Greeting:** a postfix on `conversation_lord_greets_over_24_hours_on_condition`. When `__result` is true, that line is the one shown, and `LoreGreetingsService.OnGreeting` may set `VOICED_LINE` to a pair line. Which line the conversation opens with never changes.
- **Meeting:** a void prefix on `ConversationUseMeetingDialogs`. When the speaker has not met the player and the speaker's person has a pair with the player's person, `OnMeetingCheck` marks him met and puts `LastMeetingTimeWithPlayer` back, so the original returns false and the conversation goes to the met greeting, which then shows the pair's first-meeting line. This is what vanilla does for close relatives (`conversations_automeet_close_relatives`).

`LoreGreetingsService.Choose` is the whole rule, on plain facts from `ILoreGreetingsAdapter` (ADR-007).

### Component Diagram

```
lore_greetings.json -> LoreGreetingsConfigProvider (validated LoreGreetingTable)
                                 |
Patch177_LoreMeeting  ---> LoreGreetingsService.OnMeetingCheck / OnGreeting / Choose
Patch177_LoreGreeting --/        |                \
                         ILoreGreetingsAdapter     IAlignmentService (sides), ICoopSessionProvider
                         (conversation facts, mark met, VOICED_LINE)
```

## Rules

- **Who is who:** a hero is matched to a person by his hero id, his character id and, for a hero made from a template, the template's id. A person whose recorded gender is not the hero's is nobody, so a line written for Éowyn never reaches a male hero with her id.
- **Who listens, in order:** the player first, then each other hero in the player's party in roster order. Companions are skipped when the speaker himself rides in the player's party, so your own companions do not greet each other every day. The first listener whose pair has a line for the case wins; a pair with no such line passes to the next listener.
- **Hostile:** the speaker's realm is at war with the listener's, or their sides are Free against Evil (`FactionSideRules.AreOpposed` on `IAlignmentService.ResolveSide`; Neutral is never opposed). A hostile pair shows only its `hostile` line; with none, the old greeting stands, as Encounter Lines' kin greetings give way at war.
- **First meeting:** only for the player, only when the meeting prefix marked this speaker met in this conversation, and only at peace. With no `first` line the ordinary line shows.
- **Never:** a prisoner speaker; no conversation hero; the meeting write on a co-op client (the guest does not mark the host's heroes met). Conversations do not run on a dedicated server.
- **Unvoiced:** the vanilla greeting it replaces may be voiced; a lore line is not, as no TAOM line is.

## Priority against other lines

- Vanilla `str_context_line` rows and Encounter Lines' lord greeting rows (group by bucket, written by Luiz) are replaced where a pair applies, since the pair is more specific.
- The only greeting row haterade22 wrote himself is `str_context_line.taom_nazgul_ally_first` (commit 184d62715d). None of the Nine speaks a pair line (`ShippedLoreGreetingsTests.NoneOfTheNine_SpeaksAPairLine`), so that row is never outranked.
- Named companion backstories (`OathIntroDialog`, `lord_start` at 110) come after the greeting and are untouched. Named companions are met at placement, so the meeting prefix never fires for them.
- The attack, parley, surrender, quest, grievance and 24-hour salutation lines are untouched: the greeting postfix runs only when `start_default` is the line shown.

## Configuration

### Config File: `Main/_Module/ModuleData/lore_greetings/lore_greetings.json`

| Field | Type | Description |
|-------|------|-------------|
| `persons[].id` | string | Person id, a lower-case word; it is part of the localization key |
| `persons[].female` | bool | The person's gender; required |
| `persons[].characters` | string[] | Every character id that is this person (story hero, named companion, lord); an id belongs to one person |
| `greetings[].speaker`, `listener` | string | Person ids; the pair is one way |
| `greetings[].text` | string | The line at peace |
| `greetings[].hostile` | string | The line when hostile |
| `greetings[].first` | string | The line on the first meeting at peace (falls back to `text`) |

`LoreGreetingsConfigProvider` drops, with a warning each and one summary warning: a person with a bad or repeated id, no gender, no character id, or a character id another person has; a greeting with an unknown speaker or listener, to himself, a repeated pair, no line, or a line with `{`, `[` or `]` (no text variables, no animation marks). A missing or broken file gives an empty table. Read once per process: an edit needs a full game restart.

### Localization

Keys are composed: `taom_loregreet_<speaker>_<listener>`, plus `_hostile` and `_first`. The JSON holds the English, which English shows inline. The English rows for translators belong in `taom_module_strings.xml`, generated from the JSON; `LoreGreetingsRowsTests` pins that every line has its row with the same text and that no row is left without a line.

### Current Values

60 persons and 396 one-way pairs (83 with a hostile line, 11 with a first-meeting line), counted by `ShippedLoreGreetingsTests` loading the file on 2026-11-28. They cover the Fellowship, the house of Elrond and Rivendell, Lothlórien, Lindon, the Woodland Realm, Erebor and Dale, the Beornings, Denethor's and Imrahil's houses and the lords of the fiefs of Gondor, the house of Eorl and the lords of Rohan, Isengard, and Mordor. Every character id exists in the shipped ModuleData (`EveryCharacterId_IsAHeroOfTheShippedModuleData`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/LoreGreetings/LoreGreetingsService.cs` | The pair choice and the two patch flows |
| `Main/Features/LoreGreetings/LoreGreetingsConfigProvider.cs`, `ILoreGreetingsConfigProvider.cs` | Validated JSON |
| `Main/Features/LoreGreetings/Domain/` | `LoreGreetingTable`, persons, lines, conversation facts |
| `Main/Features/LoreGreetings/LoreGreetingsModule.cs`, `LoreGreetingsIoC.cs` | Feature module (one line in `Main/Composition/FeatureModules.cs`) and registrations |
| `Main/Features/LoreGreetings/Hooks/Patch177_LoreGreeting.cs`, `Patch177_LoreMeeting.cs` | The two thin patches |
| `Main/Adapters/ILoreGreetingsAdapter.cs`, `LoreGreetingsAdapter.cs` | Conversation facts, marking met, the greeting variable |
| `Main/_Module/ModuleData/lore_greetings/lore_greetings.json` | Persons and lines |

## Dependencies

- `IAlignmentService` (Execution): the sides of speaker and listener.
- `ICoopSessionProvider` (CoopInterop): no meeting write on a co-op client.
- `IOutcomeLog`: `[Check]` lines `loregreetings.greeting` (key: the line's key) and `loregreetings.meeting` (key: the speaker's person).

## Co-op

The greeting writes only a local text variable. The meeting write (`SetHasMet`) is skipped on a co-op client; on a host and in single player it runs where vanilla's own meeting would write the same flag. No setting, so nothing in `CoopSettingsRelevance`.

## Tests

- `LoreGreetingsServiceTests`: matching by any id, the pair direction, peace, hostile by war and by side, Neutral, first meeting (player only, fallback, hostile wins), listener order, companions skipped when the speaker rides with the player, gender, prisoner, same person, the meeting flow (marks, skips met, companion-only, gender, co-op client, prisoner), the first-meeting flag used once and only for its speaker, the `[Check]` lines.
- `LoreGreetingsConfigProviderTests`: one test per validation rule, trimming, read once.
- `ShippedLoreGreetingsTests`: the shipped file loads clean, every character id is a shipped hero with the person's gender, none of the Nine speaks, every person is in a pair, the writing rules (no em or en dash, no double quote, at most 160 characters, a full sentence).
- `LoreGreetingsBindingTests` (`RequiresGame`): both targets' shapes, the greeting still sets `VOICED_LINE` through `conversations_set_voiced_line`, the meeting helper still calls `SetHasMet` and is over 100 IL bytes (not inlined into its four callers), `SetHasMet` still writes the meeting time, the category attaches one postfix and one prefix and the JIT accepts them.
- `LoreGreetingsWiringTests`: the module once, its category at GameInit, no behavior or model, the registrations, both patches initialised, nothing in `IoC.cs` or `SubModule.cs`.

## How to verify in game

In-game check owed; none of these has been run.

**Preconditions:** a TAOM campaign; no setting.

1. Take over Legolas with the Player Switcher and talk to Thranduil. Check: "My son. The Greenwood has missed your bow." (or the first-meeting line "Legolas! Home at last..." when Thranduil had not met the old player), and `[Check] id=loregreetings.greeting` in the log with the key `taom_loregreet_thranduil_legolas`.
2. As Legolas, talk to a Mirkwood lord with no pair line. Check: the usual greeting.
3. Hire Gimli as a companion, then talk to Dáin in Erebor. Check: Dáin greets Gimli by name.
4. As a lore lord at war with a lord who knows him (Éomer at war with Théoden's realm), talk to him. Check: the hostile line, or the usual war greeting for a pair with none.
5. As Éowyn, talk to Théoden when he has not met the old player. Check: no "who are you" introduction, Théoden's first-meeting line, and `[Check] id=loregreetings.meeting`.
6. Talk to your own companion Gimli with Legolas also in the party. Check: Gimli greets only the player (never Legolas).
7. Log check: `LoreGreetingsConfigProvider: loaded 60 persons and 396 greetings`, and no other `LoreGreetingsConfigProvider:` warning.

## Changelog

- 2026-11-28: first version (Patch177).

## GitHub Issue

- **Issue:** none. Luiz waived new GitHub issues for this work.
- **Status:** In-game check owed.
