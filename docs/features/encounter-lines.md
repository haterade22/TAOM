# Encounter Lines

## Overview

Lords, caravans, villagers, bandits and hideout bosses met on the map speak in the voice of their people and by who the player is to them. A lord greets, threatens, yields, is released and takes leave in one of nine speaker groups (Men of the West, Rohirrim, Northmen, Dunlendings, Southrons, Easterlings, Elves, Dwarves, Orcs) and one of five buckets (liege, ally, rival, foe, kin). Caravans and villagers greet and surrender by group and bucket, ten bandit clans rob and greet the player by bucket, and the nine that hold hideouts (not looters) challenge the player from one. One MCM switch, default on. Text only: nothing else about the encounter changes, but TAOM lines are not voiced.

## Why This Exists

- **Vanilla behavior:** a lord's greeting and war lines come from vanilla rows scored by conversation tags written for Calradian cultures and personas; caravans, villagers and the hideout boss have one fixed line each, and only the eight Bandit Voices clans have their own robbery words.
- **TAOM requirement:** an orc chieftain of Mordor should not greet a Gondor captain like a Vlandian baron, and a Dwarf of Erebor should speak differently to kin than to a stranger.
- **Without this feature:** every people speaks in the same generic voice, whoever the player is.

## Buckets

Pure rules in `EncounterBuckets`; sides from `execution/alignment.json` through `IAlignmentService.ResolveSide`.

The player always has a side: `FactionSides.OfPlayer` reads the player clan's map faction, and a landless player's clan is not listed in `alignment.json`, so `ResolveSide` falls back to the clan's culture. A landless player is therefore ally or foe by culture like anyone else (or kin, which does not depend on side), never rival for being landless. The code still answers rival when no player side reads at all; that branch is kept and tested, but no campaign reaches it.

| Speaker | Buckets, checked in order |
|---|---|
| Lord | `liege` the player leads the speaker's kingdom; `foe` at war while not on the same side, or Free against Evil at any time; `kin` the same race family, and for Men the same speaker group (also at war on the same side, where the kin greeting and farewell give way to vanilla's and the surrender, yield and release rows still apply); `ally` at peace; `rival` at war while on the same side |
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

**Where vanilla stays:** non-lords (wanderers, companions, notables), lords of the player's clan, messenger conversations, lords you speak to inside a settlement (any settlement encounter, sieges and settlement battles included, so a lord's yield and the player's release of him after a siege keep vanilla's lines; the hideout boss's challenge is spoken inside a hideout and is still TAOM's line), a speaker whose race family is not the group's (Sauron, Saruman, the Nazgul, trolls and Men of Mordor), a dedicated server, the switch off. Lords outside the player's clan are not excluded for serving under the player: `HeroHelper.UnderPlayerCommand` is true for a lord outside the clan only when the player leads his faction, which is the vassal of the player's realm who gets the liege rows, and a lord in the player's army is not excluded. Parley (`str_parley_comment`), the caravan's refusal (`CARAVAN_DEFIANCE`) and "Heaven will avenge us" are untouched. Romance (`RomanticallyInvolvedTag` -1) and the yield of a non-combatant (`CombatantTag`) stay vanilla.

**What TAOM lines displace.** Every TAOM lord row scores 200 or more, so wherever its tags pass it outranks vanilla's situational rows for the same id: the relationship greetings (`FriendlyRelationshipTag`, `HostileRelationshipTag`), the under-command and attacking greetings, the war farewells (`PlayerIsEnemyTag`) and the courtship farewells (`AttractedToPlayerTag`). The one exception is the kin greeting and farewell: those 27 rows carry `PlayerIsEnemyTag` -1 because the kin bucket also wins at war on the same side, and at war vanilla's greeting and farewell show instead.

**Voice and animation.** TAOM lines are unvoiced. Vanilla voices the lord greeting (`str_context_line`), the lord's yield (`str_surrender_offer`), the robbery threat and the hideout boss challenge, and each of these goes silent when TAOM's line shows. Orc speakers were silent already. The caravan and villager surrender lines never had a voice. The lines also carry no animation tags (the data rule forbids `[` and `]`), so the face and body animation vanilla sets on the lines it replaces (for example `[if:convo_mocking_revenge]` on the robbery threat, `[if:idle_angry][ib:nervous]` on the caravan surrender) is not applied while a TAOM line shows.

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
| `Main/_Module/ModuleData/taom_encounter_strings.xml` | 435 keyed rows: caravans and villagers 108, robbery 240, bandit greetings 60, hideout 27 (nine clans) |

## Co-op

No gate. The patches skip nothing and write only local conversation text variables and the stored sentence text; the module declares no campaign behavior. It stands down on a dedicated server and during a messenger conversation, and rolls on TAOM's own random source. The setting is presentation in `CoopSettingsRelevance`. No co-op session has run this build.

## Tests

- `EncounterBucketsTests`: every bucket branch and its order, the alternation of `IsAltLine`.
- `EncounterGroupsConfigProviderTests`, `ShippedEncounterGroupsTests`: one test per validation rule; the shipped table equals the spec and covers every main culture, every race and the ten clans.
- `EncounterLinesServiceTests`: every gate that leaves vanilla (a missing conversation party and a missing player included), the greeting, robbery and seam paths and their chances.
- `EncounterLinesDataTests`: the exact 678 ids and keys, one dot per id, the four robbery rows, the writing rules, the tag order and weights per beat, and the reversed war tag on the 27 kin greetings and farewells.
- `EncounterLinesScoreTests` (`LiveInstall`, `RequiresGame`): the lowest TAOM score per id beats vanilla's highest; every tag name is a real tag.
- `EncounterSentenceTextPatchTests` (`RequiresGame`): runs the text seam's postfix on a `ConversationManager` shell for a replaced line, a sentence that is not patched, the switch off, no conversation party, a missing row, an index outside the list and a fault, and pins that the postfix calls `TrySetSentenceLine`.
- `EncounterLinesBindingTests` (also pins the five vanilla lord text ids and `ProcessSentence`'s call to `UpdateCurrentSentenceText`), `EncounterLinesPatchTests`, `EncounterLinesWiringTests`, `EncounterLinesSettingsProviderTests`; `BanditVoicesServiceTests` for the greeting order.

## How to verify in game

In-game check owed; none of these has been run. No `taom.*` command drives these beats, so every result is one only a person can see, read from the conversation text, plus the log check in step 9.

**Preconditions:** a TAOM campaign, MCM "World/Encounter Lines" on, no restart needed.

1. As a Gondor-culture player, talk to a Mordor orc lord at peace and at war, then to a Gondor lord. Check: an orc foe line both times, a west kin line from the Gondor lord, and Sauron or a Man of Mordor keeps vanilla's line. The greeting is silent where vanilla's was voiced; accept or refuse that trade.
2. As a Neutral (Khand or Umbar) player at peace, talk to an orc lord. Check: the orc ally line reads as "no enemy of ours today", with no claim that you march on the orcs' side.
3. At war with a realm of your own people, let one of its lords attack you. Check: the greeting before the demand is vanilla's war greeting, not a kin line, and the demand is a kin line.
4. Get a surrender demand from one lord on two campaign days. Check: the demand changes between the two lines.
5. Meet several caravans and villager parties for the first time, then rob one. Check: about half greet in their people's voice, and the surrender line is TAOM's. This proves the `UpdateCurrentSentenceText` postfix runs. If the surrender and hideout boss lines stay vanilla while the greetings are TAOM's, the seam postfix did not run: check the log for the Patch149 apply and any `[EncounterLines]` warning; if the patch applied, move the seam to the `CurrentSentenceText` getter or to `ProcessSentence`.
6. Get robbed by Feral Orcs or looters several times (about 80% TAOM) and by a Bandit Voices clan (about 50%). Attack a bandit party for the greeting, and assault a hideout for the boss's challenge.
7. Take a lord's yield after a field battle and release him. Check: TAOM lines in the field, vanilla inside a settlement.
8. Switch the setting off. Check: every line is vanilla.
9. Log check, with the switch on after the first conversation: `rgl_log` has no `ERROR: Text with id` line and no `[EncounterLines]` warning, shows `EncounterGroupsConfigProvider: Loaded encounter_groups.json (...)`, and has no other `EncounterGroupsConfigProvider:` warning. Grep for both prefixes; the config loader does not use the `[EncounterLines]` one, and with the switch off it reads nothing.

## Changelog

- 2026-11-13: first version (Patch149).
- 2026-11-13: follow-up to the merged review: kin greetings and farewells step aside at war; the orc ally greeting, three foe greetings and six bandit lines no longer claim a side or an act the listener may not have; the hideout rows for looters are removed (looters never hold a hideout); the text seam's postfix is run in tests; the voice, animation, settlement and displacement notes above.
- 2026-11-13: review fixes: the hideout boss speaks for the clan of his own party (the hideout's map faction only when that reads nothing); notables are no longer described as lords; the bucket greeting's `_1` and `_2` split evenly.

## GitHub Issue

- **Issue:** none. The owed in-game checks above need an issue with the `triage-needs-ingame` label, or a waiver from the maintainer recorded here; neither exists yet.
- **Status:** In-game check owed.
