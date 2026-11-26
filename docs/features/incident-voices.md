# Incident Voices

## Overview

The 101 random incidents the map shows (a rooster thief, lepers at the gate, a sack of a captured town) speak of Middle-earth instead of Calradia. The text a player reads depends on the player's side and people: Free Peoples, Shadow, or, for a Neutral player, the side the player's people lean to. An incident that does not fit the player's people never appears (no sickness for Elves, no horse trouble for Dwarves and Orcs), and a random place the text names comes from the player's own side.

This is the code seam (PR 1). The texts arrive as data later: every text is looked up by key, and a key with no row leaves vanilla's English, so the seam ships safely before any text.

## Why This Exists

- **Vanilla behavior:** `IncidentsCampaignBehaviour` registers 101 incidents with Calradian words in them ("Heaven", "denars of the Empire", Aserai, Khuzait, the Senate), and picks places like Market Manipulation's `PLACE` from any town in the world (`Town.AllTowns.GetRandomElement()`, `IncidentsCampaignBehaviour.cs:1832`).
- **TAOM requirement:** An orc captain should not pray to Heaven, and a Gondor player should not hear of a caravan from Barad-dur. Elves do not sicken; Dwarves and Orcs keep no horses.
- **Without this feature:** every player reads Calradian incident text, and sees events that make no sense for their people.

## Architecture

### Design Challenge

An incident's title, description and option texts are `TextObject`s the engine fills with variables inside its own condition lambda, and an option's effects (gold, traits, troops) are fixed in vanilla's code. Re-registering an incident would drop those lambdas. A new incident id would put a TAOM id into every save's cooldown dictionary (`IncidentManager._incidentsOnCooldown`).

### Solution Approach

One Harmony postfix, `IncidentCanBeInvokedPatch`, on the public non-virtual `Incident.CanIncidentBeInvoked()` (`Incident.cs:120`). Vanilla calls it for every incident of a trigger (`IncidentManager.GetOccurableEventsForTrigger`) and once more right before the popup (`Campaign.cs:1006`), after the condition has set the text variables. The postfix returns at once when vanilla said false. Otherwise `IncidentVoiceService.Review`:

1. stays vanilla for an id not in the catalog, a dedicated server, a co-op session, or no player;
2. reads the player's side (`IAlignmentService.ResolveSide`) and people (`encounter_groups.json`: race family, speaker group) and picks the voice (`IncidentVersionPicker`);
3. closes the incident when it is disabled, when the restrict lists do not fit, or when it is a sickness and the player is an Elf (`IncidentEligibility`);
4. for a place fix, sets the vanilla variable to a place of the player's own side, or closes the incident when the side has none (`IncidentPlacePicker`);
5. writes TAOM's texts in place through the public `TextObject.Value` field followed by `CacheTokens()` (the token cache is keyed by language only), then sets the side vocabulary variables. A slot with no text row gets vanilla's own text back (`IncidentTextHandle` keeps the first value it saw per id and slot).

Nothing is registered, renamed or saved: a disabled incident stays registered, because a saved cooldown entry names it. Any fault in the postfix keeps vanilla's answer and warns once.

**The voice.** Free reads Free, Evil reads Shadow. Neutral is decided by people (Luiz, 2026-11-26): Neutral Orcs, Southrons and Easterlings read Shadow; every other Neutral people (West, Rohirrim, Northmen, Dunlendings, Elves, Dwarves) and an unknown people read Free. The people is the player character's own, from race and culture, never the roster.

**Gates.** `restrict` lists are allow-lists on three axes (speaker `groups`, race `families`, voice `versions`); an empty list limits nothing, and a listed axis the player's data cannot answer closes the incident. `disease` closes it for Elves. `disable` closes it always (Gnostic Boys: no counterpart in Middle-earth). `vanilla_text` keeps every text vanilla's while the gates still apply (Migrant Influx and Job Offer carry another developer's overrides, `taom_the_fix_2KaXj3Qa`, `taom_training_grounds_guEjfYhO`, `taom_training_grounds_Qb3axqdZ`).

**Places.** A `places` row names a vanilla variable and a kind (`town`, `vineyard`). The candidates rank 1 the player's own realm, 2 the player's own people, 3 the same side and voice; the engine's incident seed and the id pick one, so the check and the pre-popup check agree. The town the player stands in is never picked. Two rows ship: Market Manipulation `PLACE` (town) and Vintage of Victory `WINE_SETTLEMENT_NAME` (vineyard; no vineyard on the side closes the incident).

### Component Diagram

```
incidents_lotr/incidents_lotr.json            taom_encounter_strings.xml (generated block)
        |                                              |
IncidentCatalogProvider                        IIncidentVoiceAdapter.Text
        |                                              |
   IncidentVoiceService  <-- IEncounterGroupsConfigProvider, IAlignmentService
   (version, eligibility, places, text keys)
        ^                                              
IncidentCanBeInvokedPatch --> IncidentTextHandle (engine Incident, TextObject.Value + CacheTokens)
taom.incident <id> --> IncidentCommands --> IIncidentVoiceAdapter.Queue (MapState.NextIncident)
```

## Configuration

### Config File: `Main/_Module/ModuleData/incidents_lotr/incidents_lotr.json`

One object with `vocabulary` (the side words) and `incidents` (one row per vanilla id, 101). Loaded once per process; an edit needs a game restart. A missing or broken file keeps every incident vanilla's; a bad row is dropped with a warning.

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Vanilla incident id, `incident_<name>` |
| `verdict` | string | `KEEP-REWORD` (one text), `SPLIT` (free and shadow), `RESTRICT`, `DISABLE` |
| `versions` | string[] | `["all"]` or `["free","shadow"]`; none for a DISABLE row |
| `options` | int | Vanilla's option count; the texts need exactly these `optN` |
| `vars` | string[] | Variables vanilla's condition sets; a draft text may use only these and the vocabulary |
| `restrict` | object | `groups`, `families`, `versions` allow-lists, each optional |
| `disable`, `disease`, `vanilla_text` | bool | Always closed; closed for Elves; no TAOM text |
| `places` | object[] | `{variable, kind}`, kind `town` or `vineyard` |

### Text keys

A text is the game-text row `str_taom_inc.<variation>` whose text is `{=taom_inc_<variation>}English`, in the generated block of `taom_encounter_strings.xml` (a GameText source since Encounter Lines). Variation: `<id without incident_>_<all|free|shadow>_<title|desc|optN>`, key `taom_inc_` plus that; for example `taom_inc_camp_fever_all_desc`. A one-text incident uses `all`, a SPLIT one `free` and `shadow`. A Shadow set with no title falls back to the whole Free set (a half-written set never mixes in).

Vocabulary: `taom_inc_voc_<scope>_<word>` for the words `GODS`, `POWER_OATH`, `HOLY_PLACE`, `HOLY_PERSON`, `DARK_FORCE`, `LORD_TITLE`, `COIN` (lower-case in the key). Each is set as `{WORD}` on every text of the incident when at least one TAOM text was written, and only when a row exists.

Vocabulary by people (Luiz, 2026-11-26: every line must fit whoever hears it). The scope is tried most specific first, and the first row that exists wins (`IncidentTextKeys.VocabScopes`):

1. `isengard`, when the player's own culture or realm culture is `isengard` (the White Hand);
2. `<group>_<free|shadow>`, the player's people on the voice they read (`southrons_shadow`, `easterlings_shadow`: the Dark Lord as god-king);
3. `<group>`, the people alone: `west`, `rohirrim`, `northmen`, `dunlendings`, `southrons`, `easterlings`, `elves`, `dwarves`, `orcs` (from `encounter_groups.json`);
4. `free` or `shadow`, the side row. It is the fallback for every people with no row of its own, so its words are neutral ("the Powers", "wayside shrine"; "the Dark Lord", "dark altar").

Shipped people rows (the four god words each): west and elves (the Valar; elves swear by Elbereth), dwarves (Aulë, the Maker), dunlendings (the old powers, unnamed in lore), orcs (the Dark Lord), isengard (the White Hand), southrons and easterlings on the Shadow voice. Rohirrim, northmen and a Free-voice southron or easterling use the neutral side row. A holy-place value must read after "the local" (no leading "the"). A text that must fit several peoples names no god outright: it uses `{GODS}`, `{POWER_OATH}`, `{HOLY_PLACE}` or `{HOLY_PERSON}`, and wording that fits a singular and a plural god ("as {GODS} would wish").

### Drafts to rows

`python tools/incidents_drafts_to_xml.py <drafts.tsv>...` (a people's vocabulary row has that people as its side, for example `dwarves` or `southrons_shadow`) checks the writers' drafts (columns key, incident_id, side, slot, text, notes) and `--apply` rewrites the generated block (between `INCIDENT-VOICES-BEGIN` and `INCIDENT-VOICES-END`) from them. It refuses a key that does not match, an unknown incident or side, an option vanilla does not have, a `{VARIABLE}` that is neither vanilla's for that incident nor a vocabulary word, a vocabulary word without both its rows, an em or en dash, and the banned words (Calradia, Heaven, Empire, imperial). The catalog lists variables per incident, not per slot, so the check is per incident. The 12 language files are the project lead's pass (`localize` skill): new keys leave `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey` red until then.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/IncidentVoices/IncidentVoiceService.cs` | `Review`: gate, voice, places, texts |
| `Main/Features/IncidentVoices/IncidentVersionPicker.cs`, `IncidentEligibility.cs`, `IncidentPlacePicker.cs`, `IncidentTextKeys.cs` | Pure rules |
| `Main/Features/IncidentVoices/IncidentCatalogProvider.cs` | Validating loader |
| `Main/Features/IncidentVoices/Hooks/IncidentCanBeInvokedPatch.cs` | The Patch176 postfix |
| `Main/Features/IncidentVoices/IncidentVoicesModule.cs`, `IncidentVoicesIoC.cs` | Module (listed in `FeatureModules`), registrations |
| `Main/Adapters/IIncidentVoiceAdapter.cs`, `IncidentVoiceAdapter.cs`, `IncidentTextHandle.cs` | Engine access |
| `Main/Features/DevConsole/Cheats/IncidentCheats.cs`, `IncidentVoices/IncidentCommands.cs` | `taom.incident` |
| `Main/_Module/ModuleData/incidents_lotr/incidents_lotr.json` | The data |
| `tools/incidents_drafts_to_xml.py` | Drafts to English rows |

## Dependencies

- `IEncounterGroupsConfigProvider` and `IEncounterLinesAdapter.Player()` (Encounter Lines): the player's culture, race and side.
- `IAlignmentService`: Free, Evil or Neutral for a side id.
- `IDedicatedServerProvider`, `ICoopPresenceProvider`: the feature stands down on a dedicated server and in a co-op session.

## Co-op

Not co-op safe by design, so it stands down: the postfix changes whether an incident may start, and the incident chooser and its effects change replicated campaign state. With `ICoopPresenceProvider.IsCoopActive` or a dedicated server every incident is vanilla's. The postfix has no `bool` prefix, so `CoopVetoClassificationTests` needs no row. No MCM switch ships in PR 1; the catalog file is the off switch (remove it and every incident is vanilla's).

## Tests

- `TAOM.Tests/Features/IncidentVoices/IncidentRulesTests.cs`: voice by side and people (the Neutral rule), people description, restrict axes, elves and disease, place picker, key shapes.
- `IncidentVoiceServiceTests.cs`: vanilla stays (no catalog row, server, co-op, no player), gates, texts and their fallbacks, vanilla-text incidents, place fixes, the console command.
- `IncidentVoicesDataTests.cs`: loader rules and the shipped file (101 ids, 55/37/8/1 verdicts, restrict lists, the two place fixes, haterade's overrides untouched).
- `IncidentVoicesBindingTests.cs` (RequiresGame): the target's shape and size, the public text seams, the 101 ids and option counts read from `InitializeIncidents`' IL equal the catalog, the place variables, the category attaches one postfix.
- `IncidentVoicesWiringTests.cs`: module wiring, the patch's fault handling, the text handle on a real `Incident`.
- `tools/tests/test_incidents_drafts_to_xml.py`: the converter.

## How to verify in game

**Preconditions:** a campaign in cheat mode on the map, no co-op module; one player per side (Gondor, Mordor, a Neutral Khand or Umbar clan).

1. `taom.incident camp_fever` as an Elf: the answer says Elves do not sicken. As a Man: queued, and the popup opens at the next tick (vanilla's own condition must hold where you stand).
2. `taom.incident market_manipulation` in a town with Trade skill 20: the caravan comes from a town of your own side. Repeat a few times.
3. `taom.incident gnostic_boys`: closed. `taom.incident desperate_times` as Gondor and as Mordor: two different texts (after the writers' rows land; before that, vanilla's).
4. Switch realm in one session, then step 3 again: the new side's text shows (the token cache).
5. Save, resolve an incident, load: the cooldown still holds and the log has no "Null object reference".
6. A language other than English shows the translated TAOM text once the lead's language pass is in.

Only a person can confirm what the popup looks like (clipping of long texts, the option hint lines still matching the new option text). `MapIncidentView` is not in the decompile cache, so how the popup reads the texts is UNVERIFIED.

## How to add a text

1. The writers add rows to a drafts TSV. 2. `python tools/incidents_drafts_to_xml.py --apply <drafts>`. 3. The lead runs the language pass. No code change.

## Changelog

- 2026-11-26: vocabulary by people (`VocabScopes`), the incident rows that named a god, a gender or a people's habit reworded (v2.0.43).
- 2026-11-26: the seam, the catalog of 101 incidents, the voice and gate rules, the place fixes, `taom.incident`, the drafts converter (v2.0.43).

## GitHub Issue

- **Issue:** none; the maintainer waived a new issue for this work (2026-11-26).
- **Status:** Open
