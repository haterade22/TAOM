# Voice Accents

## Overview

Dialogue voice-over for TAOM characters. A `VoiceOverModel` override gives each TAOM culture a fitting
vanilla accent, so elves, Gondor, Dwarves and the southern cultures speak with an imperial voice, and keeps
orcs, creatures (Nazgul, trolls, Sauron, spiders) and any unmapped TAOM culture silent instead of speaking
with a random vanilla voice. An accent voices only the lines vanilla recorded in it.

## Why This Exists

- **Vanilla behavior:** `DefaultVoiceOverModel.GetAccentClass(CultureObject, bool)` hard-codes the twelve
  vanilla ids (`empire`, `vlandia`, `sturgia`, `khuzait`, `aserai`, `battania` and the six bandit classes)
  and returns `""` for everything else. `GetSoundPathForCharacter` reads its accent back through
  `Campaign.Current.Models.VoiceOverModel.GetAccentClass`, tries three tiers of regexes, and with an empty
  accent its last tier is `".+" + accent + "_.+"`, which is `.+_.+` and matches every voice path.
- **TAOM requirement:** a TAOM character speaks with an accent that suits the culture, or not at all.
- **Without this feature:** the elf wanderer in #635 logged `accentClass: ` (empty, a v1.4.8 log) and then
  played `vlandian_male_softspoken_005`; a Nazgul or a troll spoke with a random human voice and lip-sync.

## Architecture

### Design Challenge

`GetAccentClass` receives a `CultureObject` and a bool, never the character, so the accent cannot see the
race directly. Silence has to be returned from `GetSoundPathForCharacter`, before `base` runs, because `base` always
finds a path once the last tier matches everything. The engine reference is v1.5.5
`TaleWorlds.CampaignSystem.GameComponents.DefaultVoiceOverModel`.

### Solution Approach

`TaomVoiceOverModel : DefaultVoiceOverModel` is thin ([gamemodels.md](../../.claude/rules/gamemodels.md) rule 4).
Both overrides ask `VoiceAccentService`, a pure class with no TaleWorlds types, and fall through to `base`
when it has no opinion. The model passes `CharacterObject.Race` as a raw id and the service resolves the name
through `IRaceManager`, checking `IsValidRaceId` first because `GetRaceNameFromId` falls back to "human" for an
unknown id. `VoiceAccentsModule` declares the model for the campaign through `GameModelDecl`.

The service rules:

- `SoundPath(cultureId, raceId, vanillaSoundPath)`: a race row decides first, for both outcomes. A `silent`
  race row returns `""` without calling the vanilla model; a race row with an accent is never silent, even when
  the culture row is `silent` or unmapped. With no race row (including an invalid race id), a `silent` culture
  row returns `""`, and so does a culture that is unmapped and not one of the twelve vanilla ids (that id is
  logged once as a warning). A null or empty culture id is not silent, so vanilla decides. Otherwise it records
  the speaker's race, calls the vanilla model, and restores the previous race in a `finally`.
- `ResolveAccent(cultureId, isHighClass)`: the current speaker's race row first (when not silent), then the
  culture row. `imperial` becomes `imperial_high` or `imperial_low` by register, no row returns null and `base`
  decides. A `silent` row that reaches here passes through as the accent class `silent`, which matches no voice
  path (the engine cannot reach this today).

### Why non-tribal cultures use imperial

Vanilla picks the text variant of a line by `ConversationTagHelper.TribalVoiceGroup(character)`, which is true
only for the culture ids `sturgia`, `aserai`, `khuzait`, `battania`, `vlandia`, `nord` and `vakken`. Every other
culture gets the high or low register variants, and vanilla recorded those almost only in `imperial_high` and
`imperial_low`. A non-tribal culture on a tribal accent (`vlandian`, `sturgian`, `khuzait`, `aserai`,
`battanian`) is therefore nearly silent. In SandBox's 226 voiced lines, `mountain_bandits` has recordings for 5
lines and `forest_bandits` for none. Hence the map below: tribal cultures keep a tribal accent, everything else
that speaks is imperial. `empire` (Dunland) has no row, because vanilla already returns the imperial classes
for it. `ShippedVoiceAccentsConfigTests` pins the rule in both directions: no tribal culture maps to imperial, and no
non-tribal culture or race row uses a tribal accent (a race row applies in any culture).

### Speaker scope

`GetAccentClass` is called only by the base `GetSoundPathForCharacter`, synchronously, through
`Campaign.Current.Models.VoiceOverModel`. The service therefore holds the speaker's race row accent (null when
the race has no row, never silent) in a `[ThreadStatic]` field for the length of that one call, so a race row
can set the accent. Outside a
`SoundPath` call there is no speaker and only the culture row applies. Nested calls and exceptions restore
the previous value.

### Component Diagram

```
voice_accents/voice_accents.json
        |
  VoiceAccentsConfigProvider (validates)
        |
  VoiceAccentService (decisions)       IRaceManager (race names)
        |                                   |
        +---------- TaomVoiceOverModel -----+
                  (GetAccentClass, GetSoundPathForCharacter)
```

## Configuration

### Config File: `Main/_Module/ModuleData/voice_accents/voice_accents.json`

```text
{ "cultures": { "<culture id>": "<accent>" }, "races": { "<race name>": "<accent>" } }
```

| Field | Type | Description |
|-------|------|-------------|
| `cultures` | object | Culture id to accent: `imperial`, `vlandian`, `sturgian`, `khuzait`, `aserai`, `battanian`, `forest_bandits`, `sea_raiders`, `mountain_bandits`, `desert_bandits`, `steppe_bandits`, `looters` or `silent` |
| `races` | object | Race name to the same values as `cultures`; a race row wins over the culture row |

Keys and values are trimmed and case-insensitive. A missing file or a null section uses the built-in map
(equal to the shipped file) with a warning; a parse failure does the same with an error. An empty key or an
unknown value in either section drops that row with a warning. Two keys that differ only in case or
whitespace warn, and the first row is kept. An exact duplicate key is not caught: the JSON reader keeps its
last value without a warning, and `ShippedConfig_EqualsTheBuiltInDefault` guards the shipped file.

### Current Values

| Accent | Cultures |
|--------|----------|
| `imperial` | gondor, gondor_soldiers, arthedain, cardolan, rhudaur, neutral_culture, rivendell, lothlorien, mirkwood, lindon, erebor, erebor_warriors, eredluin, anduinvale, angaladh, dorwinion, southrhovanion, umbar, abanissa, shaghana, darshi |
| `khuzait` | battania (Khand) |
| `sturgian` | nord, vakken |
| `looters` | dunland_raiders, mirkwood_stalkers |
| `steppe_bandits` | rhun_raiders |
| `desert_bandits` | harad_raiders |
| `sea_raiders` | umbar_corsairs |
| `silent` | mordor, isengard, gundabad, dolguldur, goblin, bluecraig, mistymountainorcs, gundabad_raiders, feral_orcs, wild_trolls, mirkwood_spiders |

No row for `empire`, `vlandia`, `sturgia`, `khuzait` and `aserai`: vanilla keeps them. Race rows: `nazghul`,
`cave_troll`, `hill_troll` and `sauron` are `silent`; `saruman` is `imperial`. Race precedence means Saruman
speaks imperial although his culture (`isengard`) is silent.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/VoiceAccents/VoiceAccentService.cs` | The decisions |
| `Main/Features/VoiceAccents/Models/TaomVoiceOverModel.cs` | Engine hook, no logic |
| `Main/Features/VoiceAccents/VoiceAccentsConfigProvider.cs` | Validating loader, plus `IVoiceAccentsConfigProvider.cs` and `VoiceAccentMap.cs` (the validated map and built-in default) |
| `Main/Features/VoiceAccents/VoiceAccentsModule.cs` | Registration and model declaration |
| `Main/_Module/ModuleData/voice_accents/voice_accents.json` | The map |

## Dependencies

- `IRaceManager` (Core.Domain): race name from the raw race id
- `IPathService`, `IModLogger` (Core): config path and logging

## Tests

- `TAOM.Tests/Features/VoiceAccents/VoiceAccentServiceTests.cs`: the decision matrix
- `TAOM.Tests/Features/VoiceAccents/VoiceAccentsConfigProviderTests.cs`: one test per validation rule
- `TAOM.Tests/Features/VoiceAccents/ShippedVoiceAccentsConfigTests.cs`: the shipped file parses clean, equals the built-in map, names only real culture ids and skins.xml races, covers every culture in `taom_spcultures.xml`, and keeps non-tribal cultures off tribal accents and every row off `mountain_bandits` and `forest_bandits`
- `TAOM.Tests/Features/VoiceAccents/VoiceAccentsWiringTests.cs`: the module listing, its single campaign `VoiceOverModel` declaration and its singleton services

## How to add a culture

1. Add the culture to `taom_spcultures.xml`.
2. Add its row to `voice_accents.json` and to `VoiceAccentMap.Default()`. Without a row its characters are
   silent and the log carries one `VoiceAccentService` warning naming the id. `ShippedVoiceAccentsConfigTests`
   fails until the row exists.

## In-game check

Talk to an elf or a Gondor lord and a Dunland lord, then read the `[VOICEOVER]Sound path found` lines in the
game log. Each should show an `imperial_high` or `imperial_low` path. Saruman should log an imperial path. An orc
lord, a Nazgul and a troll should log no path.

## Changelog

- 2026-10-18: added (#821).

## GitHub Issue

- **Issue:** #821 Voiced dialogue plays a random vanilla voice for TAOM cultures
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/hero-race.md](./hero-race.md)
- [docs/features/kingdom-voices.md](./kingdom-voices.md)
- [docs/migration/v1.5.5-impact.md](../migration/v1.5.5-impact.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
