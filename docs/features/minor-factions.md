# LOTR-Themed Minor Factions

## Overview

All 14 vanilla Bannerlord minor factions (mercenaries, mafias, sects, nomads) have been renamed and re-lored to fit Middle-earth. Each faction retains its vanilla mechanical type and leader NPCs but receives a new LOTR-appropriate name, description, and (where needed) settlement and culture assignment.

## Why This Exists

- **Vanilla behavior:** Minor factions have Calradian names and lore (Ghilman, Skolderbroda, etc.)
- **TAOM requirement:** Every faction in the game world should feel like it belongs in Middle-earth
- **Without this feature:** Players encounter immersion-breaking Calradian faction names in a LOTR total conversion

## Architecture

### Design Challenge

Minor factions are defined in SandBox/ModuleData/spclans.xml. TAOM cannot replace this file entirely (other modules depend on it), so changes must be applied via XSLT transformation at load time.

### Solution Approach

XSLT templates in `spclans.xslt` match each minor faction by ID and override specific attributes (`name`, `short_name`, `text`) while passing through all other vanilla attributes and child elements unchanged. For factions that need geographic relocation, `initial_home_settlement` is also overridden. Three factions also have their clan `culture` changed: `brotherhood_of_woods` (Dunland), `legion_of_the_betrayed` (Arthedain, #957) and `company_of_the_boar` (Umbar, #957). A save keeps the clan culture and the clan's leader template list and does not reload `spclans.xml`, so `MinorClanMigration` ([minor-clan-migration.md](minor-clan-migration.md)) brings an old save to the same values on load.

### Component Diagram

```
SandBox/spclans.xml (vanilla)
        |
  spclans.xslt (TAOM XSLT transform)
        |
  Engine loads transformed XML
        |
  taom_module_strings.xml (localization keys)
```

## The 14 Factions

### Mercenary Clans (`is_clan_type_mercenary`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Ghilman | **Serpent Guard** | darshi | castle_A7 (Harad) | Haradrim mounted mercenaries sworn to the Serpentlord |
| Legion of the Betrayed | **The Grey Company** | arthedain (**changed**, #957) | castle_village_EW2_2 (Gondor) | Last brotherhood of the Dúnedain Rangers |
| Skolderbroda | **Axemen of Erebor** | nord | castle_S1 (Dale) | Dwarven veterans fighting in shield wall with axe and mattock |
| Company of the Golden Boar | **Corsair Blades** | umbar (**changed**, #957) | castle_V3 | Umbar ex-pirates fighting as crossbowmen on land |

### Mafia Factions (`is_mafia`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Beni Zilal | **The Blind Eye** | aserai | village_A3_1 (Harad) | Haradrim smuggler-assassin brotherhood |
| Wolfskins | **Variag Ravagers** | battania | castle_K5 (Khand) | Young Variag warriors on ritual wildling raids |
| Brotherhood of the Woods | **Dunlending Reavers** | empire (**changed**) | castle_EN3 (**changed**, Tûr Morva) | Hill-clan remnants driven from their lands by Rohirrim ancestors |
| Hidden Hand | **The Mouth's Servants** | empire | castle_ES6 (Mordor) | Mordor intelligence network beyond the Black Gate |
| Lake Rats | **Wreckers of the Long Lake** | sturgia | village_S4_1 (Dale) | Esgaroth smugglers who lure barges onto shoals |

### Sect (`is_sect`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Embers of the Flame | **Cult of the Lidless Eye** | empire | castle_EN5 (Dunland) | Black Númenórean sect venerating Sauron |

### Nomad Factions (`is_nomad`, `is_outlaw`)

| Vanilla Name | LOTR Name | Culture | Home Settlement | Description |
|-------------|-----------|---------|-----------------|-------------|
| Jawwal | **The Sand-Riders** | aserai | town_A2 (Harad) | Nomadic desert Haradrim demanding caravan tribute |
| Karakhergit | **The Wild Easterlings** | khuzait | castle_RU10 (**changed**, Nîrakh) | Unconquered steppe clans of Rhûn |
| Forest People | **The Drúedain** | vakken | village_V1_1 | Ancient Woses of Drúadan Forest |
| Eleftheroi | **The Beornings** | empire | castle_M1 (**changed**, Glad Thaw) | Skin-changers and woodmen of the Anduin vale |

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/spclans.xslt` | 14 XSLT templates overriding faction names, descriptions, settlements; the two clan culture retags (#957) and the four clans' `minor_faction_character_templates` pointed at the clones |
| `Main/_Module/ModuleData/lords.xslt` | Retags the Grey Company, Corsair Blades, Beornings and Dunlending Reavers troops (`*_tier_*`) to their side's culture and clones the 16 leader templates as `taom_spc_*` (#957); a hand block that `tools/complete_lords_xslt.py --apply` never rewrites |
| `Main/_Module/ModuleData/execution/alignment.json` | Four minor clan rows (`ghilman`, `skolderbrotva`, `forest_people`, `eleftheroi`); the clan's side while it serves no kingdom ([alignment-aware-execution.md](alignment-aware-execution.md)) |
| `Main/_Module/ModuleData/custom_battle/custom_battle_harad_troop_filter.xslt` | Custom Battle only: marks the four clans' troops obsolete so the retag does not leak them into the Arthedain, Umbar, Dale and Dunland pickers |
| `Main/Features/MinorClanMigration/` | `MinorClanMigrationService` (the decision), `Hooks/MinorClanMigrationBehavior.cs` (the `OnGameLoaded` boundary), `MinorClanMigrationModule.cs` (registration), `MinorClanFacts.cs` |
| `Main/Adapters/IMinorClanMigrationAdapter.cs`, `MinorClanMigrationAdapter.cs` | Reads and writes `Clan.Culture` and `Clan.MinorFactionCharacterTemplates` (both public members; no reflection) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | 42 localization strings (name + short_name + text per faction) |
| `Main/_Module/ModuleData/comment_strings.xslt`, `taom_xslt_strings.xml` | The 11 first-meeting lines (`taom_clan_intro_<clan id>`) that replace vanilla's `str_comment_special_clan_intro_*` rows, and their English rows |

## Dependencies

- SandBox/ModuleData/spclans.xml (vanilla source data)
- Vanilla `minor_faction_character_templates` NPC definitions (4 leader NPCs per faction). The four clans in `lords.xslt` use clones of them (`taom_spc_*`, the original with the clan's culture); the originals stay unchanged because Dunland and Rohan list them as lord and rebellion templates
- Vanilla party templates for each faction type (unchanged)

## Configuration

No runtime configuration. All data is in the XSLT and string table. To change a faction's name or description, edit the corresponding XSLT template in `spclans.xslt` and the matching string entry in `taom_module_strings.xml`.

## How to Add or Modify a Minor Faction

1. Find the vanilla faction ID in `SandBox/ModuleData/spclans.xml`
2. Add/update an `<xsl:template match="Faction[@id='...']">` in `spclans.xslt`
3. Use `<xsl:apply-templates select="@*[local-name() != 'name' and ...]"/>` to exclude attributes you're overriding
4. Add `<xsl:attribute name="...">` for each override
5. Always end with `<xsl:apply-templates select="node()"/>` to pass through child elements
6. Add corresponding `<string id="TAOM_..." text="..."/>` entries in `taom_module_strings.xml`
7. Give the clan a deliberate side: add its row to `IntendedMinorClanSides` in `ShippedClanSideCoverageTests` and, unless its culture already resolves that side, a `free` or `evil` row to `execution/alignment.json` (never `neutral`). A clan whose troops sit on the opposite side's culture also needs a `lords.xslt` retag and a clone block; see [alignment-aware-execution.md](alignment-aware-execution.md) ("How to Add a Minor Clan")

## Design Decisions

- **Clan culture changes are limited to the Dunlending Reavers, the Grey Company and the Corsair Blades.** Every minor clan keeps vanilla's explicit `default_party_template`, so a changed clan culture does not change the roster of its parties. The clan culture decides the contract weighting (kingdoms of the clan's culture count ten to one in `DiplomaticBartersBehavior`) and, with `alignment.json`, the clan's side while it serves no kingdom (#957)
- **Corsair Blades are Umbar's (#957):** the vanilla `vlandia` culture made them Rohan's and Free; `umbar` makes them Neutral, matching TAOM's Umbar
- **First-meeting lines name the new factions:** on a first meeting, `PlayerMeetLordLogEntry.GetConversationScoreAndComment` (1.5.7) gives a minor-faction hero the comment `str_comment_special_clan_intro_<clan id>` when that row exists and the hero's map faction is not the player's. TAOM keeps the vanilla clan ids, so the vanilla rows named the Ghilman, the Lake Rats, the Jawwal and the rest. `comment_strings.xslt` replaces the 11 rows that named a Calradian clan, people or place with `taom_clan_intro_*` keys, so no language keeps the old translation. The Drúedain keep vanilla's `forest_people` line, which names none. The `skolderbroda` and `karakhergit` rows match no clan id (the clans are `skolderbrotva` and `karakhuzaits`), so the Axemen of Erebor and the Wild Easterlings say no such line, as in vanilla
- **Drúedain keep vakken culture** — vanilla passthrough, lore-accurate cultural distinctness
- **Old saves (#957):** a save keeps `Clan.Culture` and the clan's template list (`Clan.MinorFactionCharacterTemplates`) and the campaign does not reload the Factions file on a load, while the `NPCCharacters` always reload, so troops are retagged and the clan is not. `MinorClanMigration` closes that on every load (`OnGameLoadedEvent`): the Grey Company's culture becomes `arthedain`, the Corsair Blades' `umbar`, and each of the four clans' `spc_*` templates is replaced by its `taom_spc_*` clone when the clone exists (a missing clone keeps the original). It is idempotent, uses no RNG, logs one INFO line per clan changed, and a new campaign changes nothing. Heroes already spawned keep the culture they were created with.
- **Battle-side effects of the troop retag are intended (#957):** a human soldier fights with the Race Abilities profile of the soldier's culture, so the retagged troops fight with the Arthedain, Umbar, Dale or Dunland profile; a formation's banner bearer carries the banner of its majority culture; a leader spawned from a clone speaks with the new culture's voice accent, and keeps the original's name key. A hired company's troops therefore look and fight like their side, which is the point of the retag.
- **Vanilla leader NPCs preserved:** the 4 leader character templates per faction pass through unchanged (visual appearance is still vanilla). The leader templates are also the `empire` (Dunland) and `vlandia` (Rohan) culture's `lord_templates` and `rebellion_hero_templates`, so the originals keep their vanilla cultures (#957). `lords.xslt` adds a clone `taom_<id>` of each of the 16 templates of the Grey Company, Corsair Blades, Beornings and Dunlending Reavers with the clan's culture, `spclans.xslt` points `minor_faction_character_templates` at the clones, and the clans' troops are retagged in place; see "Troop and leader cultures" in [alignment-aware-execution.md](alignment-aware-execution.md)

## Tests

- `TAOM.Tests/Features/Execution/ShippedClanSideCoverageTests.cs` (`LiveInstall`): the data gate for the clans' sides, troops, leader clones and the migration's targets, described once in [alignment-aware-execution.md](alignment-aware-execution.md) ("Tests")
- `TAOM.Tests/Features/MinorClanMigration/MinorClanMigrationServiceTests.cs` (old culture to new, already new, missing clone, unknown clan, a refusing adapter, a second run) and `MinorClanMigrationWiringTests.cs` (the module line, no hand wiring, the guarded load handler)
- `TAOM.Tests/Core/CustomBattleTroopFilterTests.cs`: the filter marks the four clans' troops obsolete in Custom Battle, leaves leaders, clones and other troops alone, keeps the Harad rule, and is registered for `CustomGame` only
- `TAOM.Tests/Core/CommentStringLoreTests.cs`: each of the 11 first-meeting rows comes out with its own `taom_clan_intro_*` key, keeps its `<tags>` child and names no Calradian clan; on the installed game (`LiveInstall`), every row exists, and every special clan intro row a minor clan id can reach is either one of the 11 replaced rows or the approved `forest_people` line, and names no Calradian clan (a clan or row an engine update adds fails until it gets a line)
- `tools/tests/test_complete_lords_xslt.py`: `--apply` leaves the shipped `lords.xslt` (retags and clone template included) byte-identical
- XSLT passthrough validation (all vanilla attributes preserved); string key format matches TAOM conventions

### In game

1. New campaign: the Grey Company's clan page shows the Arthedain culture and the Corsair Blades' the Umbar one; their leaders spawn with the clan's culture and keep their faces and names.
2. An old save, loaded after the update: the log carries one `[MinorClanMigration]` line for the Grey Company and one for the Corsair Blades (and one for each clan whose templates were swapped); a second load of the same save logs none. Save, reload, and check that the Grey Company still prefers Arthedain-side contracts.
3. Custom Battle: the Arthedain, Umbar, Dale and Dunland troop pickers do not offer the Grey Company, Corsair Blades, Beornings or Dunlending Reavers troops.
4. Hire the Grey Company with a Free kingdom, then fight a battle: its troops carry the Arthedain banner and fight with the Arthedain ability profile, and none desert at the next daily tick.
5. Precondition: the hero's clan does not serve your kingdom (`PlayerMeetLordLogEntry` uses the special row only when `talkTroop.Clan.MapFaction != Hero.MainHero.MapFaction`, else the generic `str_comment_intro`) and you have not met the hero. Talk to a Sand-Riders, Serpent Guard, Wreckers or Dunlending Reavers hero: the line after "I am ..." is the TAOM line, not the Jawwal, the Ghilman or the Lake Rats (visual only, a person must read it). A Drúedain hero says vanilla's forest line; an Axemen of Erebor or Wild Easterlings hero says a generic line.

## Changelog

- 2026-11-14: The 11 vanilla first-meeting lines of minor-faction heroes that named Calradian clans are replaced with Middle-earth lines (`taom_clan_intro_*` keys in `comment_strings.xslt`); follow-up to #1179. Translations are owed (#1129).
- 2026-10-21 (#957, review fixes): `MinorClanMigration` brings an old save's clan cultures and leader templates to the shipped values on load; the Custom Battle troop filter hides the four clans' troops there; `tools/complete_lords_xslt.py --apply` never rewrites the retag block; the `spclans.xslt` and `lords.xslt` comments say that the clone is saved by id and that the clan culture is saved.
- 2026-10-20 (#957): The Grey Company's clan culture is `arthedain` and the Corsair Blades' is `umbar` (`spclans.xslt`), so each clan's own culture is its side; their `alignment.json` rows were not needed. Their troops (and the Beornings' and Dunlending Reavers') are retagged in `lords.xslt`, and the leader templates are cloned with the new cultures and named by the clans (#957).
- 2026-04-06 — All 14 vanilla minor factions replaced with lore-appropriate Middle-earth equivalents via `spclans.xslt` (14 templates) + `taom_module_strings.xml` (42 strings); 3 settlement remaps and the Dunlending Reavers vlandia→empire culture change.
- 2026-03-26 — Fixed `NullReferenceException` at `CharacterObject.get_StealthEquipments()` when spawning minor faction heroes (e.g. Ghilman) on new-game start, by setting `default_stealth_equipment_roster` in the 4 XSLT culture templates (`spcultures.xslt`).

## GitHub Issue

- **Issue:** #71 — feat: LOTR-themed minor factions (mercenaries, mafias, sects, nomads)
- **Status:** Closed (2026-08-08 issue triage)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/alignment-aware-execution.md](./alignment-aware-execution.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/minor-clan-migration.md](./minor-clan-migration.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](../modding/clans.md)
- [docs/modding/configs-factions-and-world.md](../modding/configs-factions-and-world.md)

<!-- backlinks-end -->
