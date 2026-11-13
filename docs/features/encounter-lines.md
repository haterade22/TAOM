# Encounter Lines

## Overview

Lords, caravans, villagers, bandits and hideout bosses met on the map speak in the voice of their people and by who the player is to them. A lord greets, threatens, yields, is released and takes leave in one of nine speaker groups (Men of the West, Rohirrim, Northmen, Dunlendings, Southrons, Easterlings, Elves, Dwarves, Orcs) and one of five buckets (liege, ally, rival, foe, kin). Caravans and villagers greet and surrender by group and bucket, and ten bandit clans rob, greet and challenge the player by bucket. One MCM switch, default on. Text only: nothing about the encounter changes.

## Why This Exists

- **Vanilla behavior:** a lord's greeting and war lines come from vanilla rows scored by conversation tags written for Calradian cultures and personas; caravans, villagers and the hideout boss have one fixed line each, and only the eight Bandit Voices clans have their own robbery words.
- **TAOM requirement:** an orc chieftain of Mordor should not greet a Gondor captain like a Vlandian baron, and a Dwarf of Erebor should speak differently to kin than to a stranger.
- **Without this feature:** every people speaks in the same generic voice, whoever the player is.

## Buckets

Pure rules in `EncounterBuckets`; sides from `execution/alignment.json` through `IAlignmentService.ResolveSide`.

The player always has a side: `FactionSides.OfPlayer` reads the player clan's map faction, and a landless player's clan is not listed in `alignment.json`, so `ResolveSide` falls back to the clan's culture. A landless player is therefore ally or foe by culture like anyone else (or kin, which does not depend on side), never rival for being landless. The code still answers rival when no player side reads at all; that branch is kept and tested, but no campaign reaches it.

| Speaker | Buckets, checked in order |
|---|---|
| Lord | `liege` the player leads the speaker's kingdom; `foe` at war while not on the same side, or Free against Evil at any time; `kin` the same race family, and for Men the same speaker group; `ally` at peace; `rival` at war while on the same side |
| Caravan, villagers | `foe` at war with the party's realm; `kin` the home settlement's culture is in the player's group; `friend` anything else |
| Bandit clan, hideout boss | `kin` the player's culture is in the clan's `home_group`, or for a clan with no home group the same race family (orc, dwarf and elf clans; looters for any human); `side` the player's side equals the clan's, Neutral included; `foe` anything else |

## Beats and chances

| Beat | How | Chance |
|---|---|---|
| Lord greeting (first meeting, met before), surrender demand, yield, release, leave | rows in `taom_encounter_lord_strings.xml` under vanilla ids, picked by tag score | always, where the tags pass |
| Caravan greeting | postfix on `CaravansCampaignBehavior.caravan_start_talk_on_condition` sets `CARAVAN_GREETING` | 0.5, first meeting on land, not the player's own caravan |
| Villager greeting | postfix on `VillagerCampaignBehavior.village_farmer_talk_start_on_condition` sets `VILLAGER_GREETING` | 0.5, first meeting on land |
| Caravan and villager surrender | text seam on sentences `caravan_accepted_to_give_everything` and `village_farmer_accepted_to_give_everything` | 1.0 |
| Hideout boss challenge | text seam on sentence `bandit_hideout_start_defender` | 1.0 |
| Bandit greeting (player attacks) | Bandit Voices' Patch122 postfix: one roll, below 0.25 the legacy clan greeting, then (or when that row is missing) below 0.75 the bucket greeting `<clan>_<bucket>_<1 or 2>` | 0.5 bucket for clans with a legacy greeting, 0.75 for Feral Orcs and looters; `_1` and `_2` split each band evenly. Needs Bandit Voices' greeting switch on |
| Robbery | postfix on `BanditInteractionsCampaignBehavior.bandit_start_defender_condition`: all four `ROBBERY_*` from one variation `<clan>_<bucket>_<n>`, only when all four rows exist | 0.5 for clans with legacy rows, 0.8 for Feral Orcs and looters; land only |

**Where vanilla stays:** non-lords (wanderers, companions, notables), lords of the player's clan, lords under the player's command (except vassals of the realm the player rules, who get the liege rows), messenger conversations, conversations inside a settlement (any settlement encounter, sieges and settlement battles included, so a lord's yield and the player's release of him after a siege keep vanilla's lines), a speaker whose race family is not the group's (Sauron, Saruman, the Nazgul, trolls and Men of Mordor), a dedicated server, the switch off. Parley (`str_parley_comment`), the caravan's refusal (`CARAVAN_DEFIANCE`) and "Heaven will avenge us" are untouched. The two surrender lines and the hideout boss line are unvoiced while TAOM's line shows.

## Architecture

### Design Challenge

- `ConversationManager.FindMatchingTextOrNull` adds the absolute weight of every tag on a row whose tags all pass, stops at the first failing tag, and keeps the first row with the strictly highest sum; the highest vanilla lord row scores 101 (Istiana), 19 for any other lord.
- Conversation tags are created by the engine with `Activator.CreateInstance` and looked up by `StringId`, so they take no services and need no shared base.
- Two surrender demands per bucket would tie, and the second would never show.
- The caravan and villager conditions record the meeting while they run, and the three fixed sentences have no text variable.

### Solution Approach

Lord rows carry `taom_speaker_<group>` (weight 100) first and `taom_player_<bucket>` (weight 100) second, so 200 beats every vanilla row with no tie possible. `taom_alt_line` splits the two surrender demands: `_1` rows carry it at weight -1, `_2` rows at +1, and it is true when `(StableHash(heroId) + day of year) % 2 == 1`. Surrender offers carry `CombatantTag` so non-combatant lords keep vanilla's line; greetings and partings carry `RomanticallyInvolvedTag` -1.

The tags ask `EncounterTagGate`, which describes the character through `EncounterLinesAdapter.DescribeLord` (a delegate, because the adapter interface may not name an engine class) and asks `EncounterLinesService`. A missing service or a fault answers false, so the row drops out and vanilla's line stands.

The text seam is one postfix on `ConversationManager.UpdateCurrentSentenceText`: for the three sentence ids it sets `TAOM_ENC_LINE` and stores `{=!}{TAOM_ENC_LINE}` as the sentence text. The `{=!}` id keeps `CurrentSentenceText` from re-resolving the line by its game-text id.

The greeting patches read the first meeting in a prefix (vanilla's private `_interactedCaravans` and `_interactedVillagers`, bound once as `object` and read as `IDictionary`) and pass it through `__state`; a stripped prefix leaves `false` and the postfix does nothing.

```
encounter_groups.json -> EncounterGroupsConfigProvider -> EncounterGroupMap
                                                              |
tags (Tags/) -> EncounterTagGate -> EncounterLinesService <- 4 patches (Hooks/)
                                     |            |
                         IEncounterLinesAdapter   IEncounterBucketSource -> BanditVoicesService
```

## Configuration

### Config File: `Main/_Module/ModuleData/encounter_lines/encounter_groups.json`

| Section | Shape | Meaning |
|---|---|---|
| `groups` | `{ "<group>": { "family": "man", "cultures": [...] } }` | the nine speaker groups, 30 cultures |
| `races` | `{ "<race>": "<family>" }` | race to `man`, `elf`, `dwarf`, `orc` or `none` (never kin, never a speaker) |
| `bandits` | `{ "<clan>": { "side": "evil", "family": "man", "home_group": "southrons" } }` | the ten speaking clans |

The provider validates: a missing file or bad JSON gives an empty map (every line vanilla) and one warning; a group, race or clan with an unknown family, a clan with an unknown side or an unlisted home group is dropped with a warning; a culture in two groups stays in the first. Loaded once per process: an edit needs a full game restart.

MCM "World/Encounter Lines" (`GroupOrder = 89`), "Enable Encounter Lines" (`EnableEncounterLines`, default on, `RequireRestart = false`), read live.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/EncounterLines/EncounterLinesModule.cs`, `EncounterLinesIoC.cs` | module, category `Patch149_EncounterLines` at GameInit, registrations |
| `Main/Features/EncounterLines/EncounterBuckets.cs` | the pure bucket rules, `StableHash`, `IsAltLine` |
| `Main/Features/EncounterLines/EncounterGroupMap.cs`, `EncounterGroupsConfigProvider.cs` | the validated data |
| `Main/Features/EncounterLines/EncounterLinesService.cs` | every choice; holds no per-campaign state |
| `Main/Features/EncounterLines/Tags/` | 9 speaker tags, 5 bucket tags, `taom_alt_line` |
| `Main/Features/EncounterLines/Hooks/` | `EncounterTagGate` and the four patches |
| `Main/Adapters/IEncounterLinesAdapter.cs`, `EncounterLinesAdapter.cs` | campaign facts as plain records, the text variables |
| `Main/_Module/ModuleData/taom_encounter_lord_strings.xml` | 243 tagged lord rows |
| `Main/_Module/ModuleData/taom_encounter_strings.xml` | 438 keyed rows: caravans and villagers 108, robbery 240, bandit greetings 60, hideout 30 |

## Co-op

No gate. The patches skip nothing and write only local conversation text variables and the stored sentence text; the module declares no campaign behavior. It stands down on a dedicated server and during a messenger conversation, and rolls on TAOM's own random source. The setting is presentation in `CoopSettingsRelevance`. No co-op session has run this build.

## Tests

- `EncounterBucketsTests`: every bucket branch and its order, the alternation of `IsAltLine`.
- `EncounterGroupsConfigProviderTests`, `ShippedEncounterGroupsTests`: one test per validation rule; the shipped table equals the spec and covers every main culture, every race and the ten clans.
- `EncounterLinesServiceTests`: every gate that leaves vanilla, the greeting, robbery and seam paths and their chances.
- `EncounterLinesDataTests`: the exact 681 ids and keys, one dot per id, the four robbery rows, the writing rules, the tag order and weights per beat.
- `EncounterLinesScoreTests` (`LiveInstall`, `RequiresGame`): the lowest TAOM score per id beats vanilla's highest; every tag name is a real tag.
- `EncounterLinesBindingTests`, `EncounterLinesPatchTests`, `EncounterLinesWiringTests`, `EncounterLinesSettingsProviderTests`; `BanditVoicesServiceTests` for the greeting order.

## How to verify in game

In-game check owed; none of these has been run.

**Preconditions:** a campaign, TAOM only, MCM "World/Encounter Lines" on, no restart needed.

1. Meet an orc lord of Mordor on the map as a Gondor player at peace and at war, and a Gondor lord as a Gondor player: the greeting fits the bucket (foe, kin), and Sauron or a Man of Mordor keeps vanilla's line.
2. Lose to a lord who demands surrender on two campaign days: the demand changes between the two lines.
3. Meet several caravans and villager parties for the first time: about half greet in their people's voice; rob one and check the surrender line.
4. Let a bandit party rob the player and attack another; assault a hideout and check the boss's challenge. No "ERROR: Text with id" line anywhere.
5. Switch the setting off: every line is vanilla's. Check the log for `[EncounterLines]` lines (none expected).

## Changelog

- 2026-11-13: first version (Patch149).
- 2026-11-13: review fixes: the hideout boss speaks for the clan of his own party (the hideout's map faction only when that reads nothing); notables are no longer described as lords; the bucket greeting's `_1` and `_2` split evenly.

## GitHub Issue

- **Issue:** none yet; whether one is wanted is a question for the maintainer.
- **Status:** In-game check owed.
