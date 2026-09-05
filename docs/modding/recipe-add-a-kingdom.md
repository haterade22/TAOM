# Add a kingdom

## What this file is

The end-to-end recipe for a new playable realm: the order the files have to be written in, the id
patterns you fix once and never change, the configs that have to learn the new id, and the places it
breaks if you skip a step. Per-file attribute detail is not repeated here, so every step points at
the chapter that owns that file. The written source is
[`docs/features/kingdom-creation.md`](../features/kingdom-creation.md); every name and count below
was re-checked against disk on 2026-09-05, and where the two disagree the disk wins.

## Two kinds of kingdom, and 22 live ids

A kingdom reaches the game by one of two routes.

| Route | File | Count | Use it when |
|---|---|---|---|
| A new `<Kingdom>` entry | [`Main/_Module/ModuleData/taom_spkingdoms.xml`](../../Main/_Module/ModuleData/taom_spkingdoms.xml) | 14 <!-- measured: rg -c '<Kingdom\b' Main/_Module/ModuleData/taom_spkingdoms.xml 2026-09-05 --> | The realm has no vanilla counterpart worth reusing |
| An `<xsl:template>` that rewrites a vanilla kingdom | [`Main/_Module/ModuleData/spkingdoms.xslt`](../../Main/_Module/ModuleData/spkingdoms.xslt) | 8 <!-- measured: rg -c 'Kingdom\[@id=' Main/_Module/ModuleData/spkingdoms.xslt 2026-09-05 --> | You want a vanilla kingdom's whole world position, renamed |

That is **22 kingdom ids in play**, and every config, test and JSON map keyed on a kingdom is keyed
on one of those 22. The eight rewritten ones keep their vanilla `id` and only change what the player
reads: `empire` is Dunland, `empire_w` is Gondor, `empire_s` is Mordor, `sturgia` is Dale, `aserai`
is Harad, `vlandia` is Rohan, `battania` is Khand and `khuzait` is Rhun
(`spkingdoms.xslt:13-247`, one `<xsl:template>` each).

**A kingdom id and a culture id are different things, and eight of TAOM's realms prove it.** Gondor's
culture is `gondor` and its kingdom is `empire_w`; Mordor's culture is `mordor` and its kingdom is
`empire_s`. Writing `gondor` where a kingdom id belongs produces a key that resolves to nothing and
logs nothing. The id lists live in [id-cheatsheet](id-cheatsheet.md).

**Two spelling corrections to the older docs.** The file on disk is `taom_spkingdoms.xml`, all
lowercase, and `Main/_Module/SubModule.xml:130` registers it as
`<XmlName id="Kingdoms" path="taom_spkingdoms"/>`. `docs/features/kingdom-creation.md` writes
`TAOM_spkingdoms.xml` at its lines 27, 54 and 206. Harmless on Windows, wrong as documentation.

## Filing order

Each step needs the previous ones to already resolve. Strings before the XML that references them,
culture before kingdom, clans before lords, lords before heroes
([`kingdom-creation.md:49-65`](../features/kingdom-creation.md)).

| # | Write | Chapter that owns it |
|---|---|---|
| 1 | Decide the ids: kingdom id, culture id (same value), 2-char lord prefix, capital settlement id | [id-cheatsheet](id-cheatsheet.md) |
| 2 | `taom_module_strings.xml`: every name, title, ruler title and description | [strings-and-localization](strings-and-localization.md) |
| 3 | `taom_spcultures.xml`: the `<Culture>` | [cultures](cultures.md), [recipe-add-a-culture](recipe-add-a-culture.md) |
| 4 | `taom_spkingdoms.xml`: the `<Kingdom>` | [kingdoms](kingdoms.md) |
| 5 | `characters/clans.xml`: one `<Faction>` per clan, the tier 6 ruling clan first | [clans](clans.md) |
| 6 | `characters/lords.xml`: one `<NPCCharacter>` per lord | [lords-and-heroes](lords-and-heroes.md) |
| 7 | `characters/heroes.xml`: one `<Hero>` per lord, same id | [lords-and-heroes](lords-and-heroes.md) |
| 8 | `characters/npcs_{id}.xml`: the settlement notables, plus its `SubModule.xml` row | [npcs-notables-and-townsfolk](npcs-notables-and-townsfolk.md) |
| 9 | `taom_education_character_templates.xml`: the stage-2 tutors | [lords-and-heroes](lords-and-heroes.md) |
| 10 | `taom_wanderers.xml` and `taom_wanderer_skill_sets.xml` | [wanderers-and-named-companions](wanderers-and-named-companions.md) |
| 11 | `equipmentsets/taom_equipment_sets_{id}.xml`, only if the culture gets its own gear | [equipment-rosters](equipment-rosters.md) |
| 12 | `settlements.xml`: `owner` and `culture` on every fief you assign | [settlements](settlements.md) |
| 13 | `charactercreation/cultures.json`: one entry so the culture is selectable | [cultures](cultures.md) |

Step 1 writes nothing and is still the step people get wrong. Steps 4, 5 and 7 land in objects the
engine registers as temporary and reloads only on a new campaign, so none of this reaches a save
already in progress ([load-order-and-dependencies](load-order-and-dependencies.md), steps 16, 18
and 19).

## The ids you decide once

From [`kingdom-creation.md:67-81`](../features/kingdom-creation.md). Every one of these is a hard
string match somewhere; a mismatch orphans the content without an error.

| Concept | Pattern | Shipped example |
|---|---|---|
| Kingdom id | lowercase, no spaces, no diacritics | `mistymountainorcs` |
| Culture id | identical to the kingdom id | `mistymountainorcs` |
| Lord prefix | 2 uppercase chars, unique across every kingdom | `MM`, `LN`, `BC` |
| Lord id | `lord_{PREFIX}{CLAN_N}_{MEMBER_N}` | `lord_LN1_1` |
| Clan id | `clan_{culture_id}_{N}` | `clan_lindon_1` |
| Notable | `spc_notable_{culture_id}_{slot}` | `spc_notable_lindon_0` |
| Headman | `spc_{culture_id}_headman_{N}` | `spc_lindon_headman_1` |
| Wanderer | `spc_wanderer_{culture_id}_{N}` | `spc_wanderer_lindon_3` |
| Wanderer skill set | `spc_wanderer_{culture_id}_{N}_skills` | `spc_wanderer_lindon_3_skills` |
| Kingdom strings | `taom_{id}_*` | `taom_lindon_name` |
| Culture and lord strings | `aom_{id}_*` | `aom_lord_LN1_1_name` |

The lord prefix does not have to match the culture id, and on the four newest realms it does not:
Goblin Town's culture is `goblin` and its lords are `lord_GB*`, Blue Craig's culture is also
`goblin` and its lords are `lord_BC*`
([`new-factions-misty-mountains-lindon.md:24-40`](../features/new-factions-misty-mountains-lindon.md)).

## The floor: what a realm needs before it stops crashing

A kingdom is a shell around a culture, so most of the failure modes are culture failure modes. The
14-row playability checklist, with the fatal rows marked, is
[`culture-playability-wiring.md:100-116`](../features/culture-playability-wiring.md). Four of those
rows are what actually crashes a new campaign:

- **Owns at least one settlement.** Vanilla `SpawnLordParty` ends in an unguarded
  `Settlement.All.First(culture)`, so a lord of a culture that owns nothing throws on the daily
  clan tick. `Patch65` guards it and the `LANDLESS_CULTURE` validator code gates it
  ([lord-spawn-guard](../features/lord-spawn-guard.md)).
- **Stage-2 education tutor templates** in `taom_education_character_templates.xml`, or the campaign
  dies when the player character turns 8.
- **Child, teen, lord and education equipment templates**, or the new game throws an NRE.
- **An `as_<race>_facegen` action set**, only if the culture introduces a race, or its people T-pose.

The measured floor is Lindon, the smallest realm TAOM ships: **2 clans, 10 heroes, 5 settlements.**
<!-- measured: rg -c 'super_faction="Kingdom.lindon"' Main/_Module/ModuleData/characters/clans.xml ; rg -c 'faction="Faction.clan_lindon' Main/_Module/ModuleData/characters/heroes.xml ; rg -c 'culture="Culture.lindon"' "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/settlements.xml" 2026-09-05 -->
That is one town and four villages, two clans, one of them tier 6, and ten lords. It boots, it holds
fiefs and it passes every gate. Everything above that is ambition: Misty Mountain Orcs run 15 clans
and 150 heroes off the same 13 files.

`settlements.xml` lives in the game install, not the repo; a module reinstall reverts hand edits, so
land a repo-side validator gate with any fix. The repo's own
`Main/_Module/ModuleData/settlements.xml` is a stale shadow and edits to it never reach the game.

## The config fan-out

Adding a faction means updating every config that enumerates kingdoms, not only the one you came for.
These are the seven surfaces, all under `Main/_Module/ModuleData/` and all read once per process by a
singleton provider, so each needs a full application restart rather than a save reload
([configs-factions-and-world](configs-factions-and-world.md), [configs-balance](configs-balance.md)).

| File | Keyed on | Skipping it costs | Enforced by |
|---|---|---|---|
| [`execution/alignment.json`](../../Main/_Module/ModuleData/execution/alignment.json) | kingdom id, mostly | `GetKingdomSide` falls back to Neutral and mis-scores execution relations and the war block | nothing |
| [`diplomacy/diplomacy.json`](../../Main/_Module/ModuleData/diplomacy/diplomacy.json) | pairs of kingdom ids | the realm has no alliances and no enemies past day one | nothing |
| [`diplomacy/war_of_the_ring.json`](../../Main/_Module/ModuleData/diplomacy/war_of_the_ring.json) | kingdom ids inside `phase1` / `phase2` wars | the realm sits out the scripted escalation | nothing |
| [`configs/army_targeting.json`](../../Main/_Module/ModuleData/configs/army_targeting.json) | kingdom id, under `KingdomTheaters` | the realm is invisible to army-target weighting | `WarTheaterConfigInvariantsTests`, and it fails the build |
| [`factionmap/factions.json`](../../Main/_Module/ModuleData/factionmap/factions.json) and [`regions.json`](../../Main/_Module/ModuleData/factionmap/regions.json) | its own faction key, with `game_faction` naming a culture | the realm cannot be picked on the faction-select map | `dotnet test TAOM.Tests --filter FactionMap` |
| [`special_resources/special_resources_config.xml`](../../Main/_Module/ModuleData/special_resources/special_resources_config.xml) | `<Kingdom id>` and `<Culture id>` children | the realm earns no special resource | nothing |
| [`startup_resources/startup_resources_config.xml`](../../Main/_Module/ModuleData/startup_resources/startup_resources_config.xml) | culture id | its lords open the campaign with 0 gold and its clans with 0 influence | nothing |

Only one of the seven has teeth. `WarTheaterConfigInvariantsTests` reads the kingdom list out of
`taom_spkingdoms.xml` and `spkingdoms.xslt` rather than a hardcoded array, so the moment your
`<Kingdom>` lands, `EveryKingdom_HasATheaterDecisionRecorded` fails until `KingdomTheaters` gains the
id. An empty list is legal and means "deliberately passive"; the two kingdoms that use it are pinned
by name in `ExpectedPassiveKingdoms`, so a third cannot go passive quietly
([`TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs`](../../TAOM.Tests/Features/ArmyTargeting/WarTheaterConfigInvariantsTests.cs)).

`alignment.json` is the one to read before trusting any doc about it. It holds **24 keys**, and 22
of them are kingdom ids while `gondor` and `mordor` are culture ids sitting in the same flat map.
<!-- measured: python -c "import json;d=json.load(open('Main/_Module/ModuleData/execution/alignment.json'));print(len(d))" 2026-09-05 -->
Two feature docs call it a kingdom map with 16 and with 22 entries. Both are wrong, and the file is
one line to check.

## Worked example: the parametrised generator

The four newest realms were not hand written.
[`tools/generate_new_faction_kingdoms.py`](../../tools/generate_new_faction_kingdoms.py) reads an
existing kingdom, clan and lord out of the live files as templates, rewrites the attributes that
differ, and inserts the result. One dict entry is a whole realm.

<!-- excerpt file="tools/generate_new_faction_kingdoms.py" -->

```python
    "lindon": dict(
        region="LN", culture="rivendell", race="elf", capital="town_LN1", tmpl="rivendell",
        equip="rivendell", side="free", color="0xFF50A090", color2="0xFFC8E0D8",
        name="Lindon", short="Lindon", title="High Kingdom of Lindon", ruler="Lord of the Havens",
        desc="Lindon, the green land west of the Blue Mountains, is the last realm of the High Elves upon the shores of Middle-earth. From the Grey Havens of Mithlond the white ships sail into the West, while Círdan the Shipwright keeps watch over the fading light of the Eldar.",
        lord_skill=("taom_elf_king_skills", "taom_elf_warrior_skills", "taom_elf_lady_skills"),
        clans=[("clan_lindon_1", 6, "town_LN1", "Falathrim"), ("clan_lindon_2", 4, "town_LN1", "Edhil Mithlond")],
    ),
```

The three fields to get right first:

1. **`tmpl`** picks which shipped kingdom is copied. Lindon copies `rivendell`, the three orc realms
   copy `gundabad`. Everything the rewrite does not name is inherited from that template, banner key
   included.
2. **`culture`** is a reference to an existing culture, not a new one. Lindon runs on `rivendell`
   and Blue Craig on `goblin`, which is why neither needed its own troop tree
   ([`new-factions-misty-mountains-lindon.md:24-40`](../features/new-factions-misty-mountains-lindon.md)).
3. **`clans`** is `(clan id, tier, home settlement, display name)`, ruling clan first at tier 6. The
   lord ids come from `region`, not from the clan id.

Day-one stances are generated the same way, sibling by sibling:

<!-- excerpt file="tools/generate_new_faction_kingdoms.py" -->

```python
        rels = ""
        for other in sib:
            if other == kid:
                continue
            same_side = KINGDOMS[other]["side"] == k["side"]
            val = "1" if same_side else "0"
            rels += (f'            <relationship\n                kingdom="Kingdom.{other}"\n'
                     f'                value="{val}"\n                isAtWar="false" />\n')
        blk = blk.replace("</relationships>", rels + "        </relationships>", 1)
```

Note what that loop does not do: it cross-links the four new realms to each other and stops. None of
the ten older kingdoms carries a `<relationship>` row naming any of them.
<!-- measured: python -c "import xml.etree.ElementTree as ET;r=ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot();new={'goblin','mistymountainorcs','bluecraig','lindon'};old=[k for k in r.iter('Kingdom') if k.get('id') not in new];print(len(old),sum(1 for k in old for x in k.iter('relationship') if x.get('kingdom','').replace('Kingdom.','') in new))" 2026-09-05 -->
That is survivable rather than broken, because an undeclared pair defaults to neutral, but it does
mean the new realms open at peace with everyone who was there first. The mechanism is in
[kingdoms](kingdoms.md).

Every insertion is wrapped in a marker pair by the script's `upsert` helper
(`generate_new_faction_kingdoms.py:97-102`), so a re-run strips its own previous block instead of
duplicating it. If you open `characters/clans.xml` and find `<!-- TAOM-NEWFACTIONS:goblin:BEGIN -->`
above a run of clans, that run is generated. Edit the script, not the block, or the next run throws
your edit away.

## Recipes

### Add a kingdom

1. Pick the ids from the naming table above and check none of them is taken:
   `rg -n 'id="<your id>"' Main/_Module/ModuleData/`. A duplicate id is not an error at load; the
   second entry re-runs the deserializer over the first.
2. Write the strings into `Main/_Module/ModuleData/taom_module_strings.xml` first, all of them.
3. Write the `<Culture>` into `Main/_Module/ModuleData/taom_spcultures.xml`, or point the kingdom at
   an existing culture as Lindon and Blue Craig do. A new culture means walking the 14-row checklist
   in [`culture-playability-wiring.md:100-116`](../features/culture-playability-wiring.md) and
   binding all twelve party templates ([party-templates](party-templates.md)) before going further.
4. Write the `<Kingdom>` into `Main/_Module/ModuleData/taom_spkingdoms.xml`. Copy a shipped entry of
   a realm the same size, then change `id`, `culture`, `owner`, `initial_home_settlement`, the four
   colours, the five text attributes and `banner_key`.
5. Write the clans into `Main/_Module/ModuleData/characters/clans.xml`, tier 6 first, each with
   `super_faction="Kingdom.<your id>"`.
6. Write the lords into `characters/lords.xml`, then a `<Hero>` of the **same id** into
   `characters/heroes.xml` for every one. Skipping this is what crashes a new game.
7. Write `characters/npcs_{id}.xml` and add its row to `Main/_Module/SubModule.xml` beside the other
   `NPCCharacters` rows ([submodule-and-registration](submodule-and-registration.md)). The shipped
   files run from 28 to 80 notables.
8. Add the education templates, the wanderers and the wanderer skill sets. Ten wanderers is the
   shipped norm; 17 of the 20 cultures have exactly ten.
9. Assign fiefs in `TAOM_Map/ModuleData/settlements.xml`: set both `owner` and `culture` on every
   town and castle. Villages inherit from the fief they are bound to.
10. Add the culture to `charactercreation/cultures.json` so it can be picked at character creation.
11. Add the id to all seven configs in the fan-out table. Do `configs/army_targeting.json` first,
    because that is the one a test will stop you on.
12. If the culture gets its own troop file, append the culture name to the `cultures` list in
    [`tools/validate_all_troop_refs.py`](../../tools/validate_all_troop_refs.py) or its troops are
    never swept for broken item references.
13. Restart Bannerlord and start a **new campaign**. A save already in progress will not see any of
    it.

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: new campaign only
Code: No code changes needed

### Retire a kingdom

Deleting a `<Kingdom>` entry orphans every clan whose `super_faction` names it, every lord in those
clans and every config row keyed on the id, and none of that reports an error at load. Retire it
instead.

1. Leave the `<Kingdom>` entry in `taom_spkingdoms.xml` where it is, and keep
   `initial_home_settlement` pointing at a real settlement.
2. Empty its `<relationships>` block so it starts at peace with everyone and joins no war. Leave the
   element itself in place and put no XML comment inside it.
3. Reassign its fiefs in `TAOM_Map/ModuleData/settlements.xml` by changing `owner` and `culture` on
   each one.
4. Move or delete its lords in the same edit. A lord of a culture that now owns nothing is the
   daily-tick crash, which is exactly what the `LANDLESS_CULTURE` code reports.
5. Leave its rows in all seven configs. `WarTheaterConfigInvariantsTests` fails from both sides here:
   on a kingdom with no `KingdomTheaters` key, and on a key with no kingdom.
6. To retire one of the eight rewritten vanilla kingdoms instead, delete its `<xsl:template>` block
   from `spkingdoms.xslt`. That does not remove a kingdom, it restores the vanilla one under its
   vanilla name and colours.

Check: `dotnet test TAOM.Tests --filter WarTheaterConfigInvariants`
Takes effect: new campaign only
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A `<Hero>` missing for an `is_hero` lord crashes the new game.** The log line is
  `Null object reference found with ID: lord_X`, the throw surfaces in
  `LordNeedsHorsesIssueBehavior.ConditionsHold`, and the repair is one row,
  `<Hero id="lord_X_1" faction="Faction.clan_{id}_{N}" />`
  ([`kingdom-creation.md:516-525`](../features/kingdom-creation.md)).
- **A generated clan keeps its template's banner.** The generator rewrites ids, names, tiers and
  colours and deliberately leaves `banner_key` alone, so 34 of the 145 clans in `characters/clans.xml`
  fly the same banner as `clan_gundabad_1`, both Lindon elf clans included.
  <!-- measured: python -c "import xml.etree.ElementTree as ET;f=[e for e in ET.parse('Main/_Module/ModuleData/characters/clans.xml').getroot().iter('Faction')];k={e.get('id'):e.get('banner_key') for e in f};print(sum(1 for v in k.values() if v==k['clan_gundabad_1']))" 2026-09-05 -->
  Fixing it is a per-clan copy from a source clan, or the in-game banner editor
  ([banners-and-heraldry](banners-and-heraldry.md)).
- **Wanderers with the wrong `culture` never appear.** They spawn only in settlements of the culture
  named on the `<NPCCharacter>`, so a set copied from another realm spawns in that realm's towns and
  nowhere in yours ([`kingdom-creation.md:539-545`](../features/kingdom-creation.md)).
- **A settlement with the right `owner` and the wrong `culture` looks wrong and validates fine.**
  Banners, guards and visuals follow `culture`, not ownership.
- **A lore name is not an id.** `rohan`, `dunland`, `harad`, `rhun`, `dale` and `khand` are dead keys
  in every config; the live ids are `vlandia`, `empire`, `aserai`, `khuzait`, `sturgia` and
  `battania`. `WarTheaterConfigInvariantsTests` names those six and catches every other unresolvable
  key through `EveryKingdomTheaterKey_ResolvesToARealKingdom`.
- **A new culture with its own troop file is invisible to the item-reference sweep until you add
  it.** `tools/validate_all_troop_refs.py` walks a hardcoded list of 10 cultures against 16 troop
  files, so 6 files are never opened by it.
  <!-- measured: ls Main/_Module/ModuleData/troops/ | rg '^troops_[a-z_]+\.xml$' | wc -l 2026-09-05 -->
- **The four newest kingdoms have no special resource.** `special_resources_config.xml` names 18 of
  the 22 kingdom ids; `goblin`, `mistymountainorcs`, `bluecraig` and `lindon` are the four absent
  ones. Nothing reports it.
  <!-- measured: rg -o '<Kingdom id="[a-z_]+"' Main/_Module/ModuleData/special_resources/special_resources_config.xml | sort -u | wc -l 2026-09-05 -->
- **TAOM has never written down whether the row order inside `SubModule.xml` matters.** What is on
  disk is that the XSLT rows sit above the plain-XML rows (`SubModule.xml:70` and `:130` for
  `Kingdoms`, `:96` and `:157` for `NPCCharacters`) and no doc says whether that is load-bearing.
  Follow the shipped order, and read [load-order-and-dependencies](load-order-and-dependencies.md)
  before assuming either way.

## Numbers in this chapter

| Number | What | Command, run 2026-09-05 |
|---|---|---|
| 14 | `<Kingdom>` entries in `taom_spkingdoms.xml` | `rg -c '<Kingdom\b' Main/_Module/ModuleData/taom_spkingdoms.xml` |
| 8 | vanilla kingdoms rewritten by XSLT | `rg -c 'Kingdom\[@id=' Main/_Module/ModuleData/spkingdoms.xslt` |
| 22 | live kingdom ids (14 plus 8) | the two rows above |
| 24 | keys in `execution/alignment.json`, of which 22 are kingdom ids and 2 (`gondor`, `mordor`) are culture ids | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/execution/alignment.json'));print(len(d))"` |
| 22 | keys under `KingdomTheaters` in `configs/army_targeting.json`, exactly the 22 kingdom ids | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/configs/army_targeting.json'));print(len(d['KingdomTheaters']))"` |
| 130 | rows in `diplomacy/diplomacy.json`, covering the same 22 ids | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/diplomacy/diplomacy.json'));print(len(d['relationships']))"` |
| 45 / 20 / 46 | entries in `factionmap/factions.json`, how many are `playable`, and entries in `factionmap/regions.json` | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/factionmap/factions.json'));r=json.load(open('Main/_Module/ModuleData/factionmap/regions.json'));print(len(d),sum(1 for v in d.values() if v.get('playable')),len(r))"` |
| 18 | kingdom ids named in `special_resources_config.xml`, 4 short of 22 | `rg -o '<Kingdom id="[a-z_]+"' Main/_Module/ModuleData/special_resources/special_resources_config.xml \| sort -u \| wc -l` |
| 22 | culture entries in `startup_resources_config.xml` | `python -c "import xml.etree.ElementTree as ET;print(len(ET.parse('Main/_Module/ModuleData/startup_resources/startup_resources_config.xml').getroot().findall('.//Culture')))"` |
| 22 | entries in `charactercreation/cultures.json` | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/charactercreation/cultures.json'));print(len(d))"` |
| 145 | `<Faction>` entries in `characters/clans.xml` | `rg -c '<Faction\b' Main/_Module/ModuleData/characters/clans.xml` |
| 34 | of those clans share `clan_gundabad_1`'s `banner_key` | the ElementTree one-liner in Gotchas |
| 1001 / 1184 | `<Hero>` entries in `characters/heroes.xml` and `<NPCCharacter>` entries in `characters/lords.xml` | `rg -c '<Hero\b' Main/_Module/ModuleData/characters/heroes.xml ; rg -c '<NPCCharacter\b' Main/_Module/ModuleData/characters/lords.xml` |
| 22 / 1409 / 28 to 80 | `npcs_*.xml` files, notables in them, and the per-file range | `python -c "import glob,xml.etree.ElementTree as ET;n=[len([x for x in ET.parse(p).getroot().iter('NPCCharacter')]) for p in glob.glob('Main/_Module/ModuleData/characters/npcs_*.xml')];print(len(n),sum(n),min(n),max(n))"` |
| 210 / 20 / 17 | wanderer entries, cultures covered, and cultures with exactly 10 | `python -c "import xml.etree.ElementTree as ET,collections;c=collections.Counter(e.get('culture') for e in ET.parse('Main/_Module/ModuleData/taom_wanderers.xml').getroot().iter('NPCCharacter'));print(len(c),sum(1 for v in c.values() if v==10))"` |
| 16 | troop files, against the 10 cultures `validate_all_troop_refs.py` sweeps | `ls Main/_Module/ModuleData/troops/ \| rg '^troops_[a-z_]+\.xml$' \| wc -l` |
| 988 | settlements in the live map module | `rg -c '<Settlement\b' "$BANNERLORD_GAME_DIR/Modules/TAOM_Map/ModuleData/settlements.xml"` |
| 2 / 10 / 5 | Lindon's clans, heroes and settlements, the smallest shipped realm | the three `rg -c` commands quoted under "The floor" |
| 10 / 0 | older kingdoms, and how many `<relationship>` rows among them name any of the four newest | the ElementTree one-liner under "Worked example" |
| 78.79 / 236 | average town gap over 78 towns, and the resulting march radius | `python tools/analyze_war_theaters.py` |
| PASS | `LANDLESS_CULTURE` on the shipped data, over a registry of 5,900 items and 5,291 NPCCharacters | `python tools/validate_moduledata.py --code LANDLESS_CULTURE` |

## Read next

- [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md): the full 13-file reference and the per-file XML shapes.
- [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md): the playability checklist and the party-template binding contract.
- [`docs/features/new-factions-misty-mountains-lindon.md`](../features/new-factions-misty-mountains-lindon.md): the four newest realms as a worked case, region codes and village economy included.
- [`docs/features/army-targeting.md`](../features/army-targeting.md): how to add a faction priority list and a theater.
- [`docs/features/diplomacy.md`](../features/diplomacy.md): relationship tiers and the War of the Ring phases.
- [`docs/features/faction-map.md`](../features/faction-map.md): the faction-select entry and its localization workflow.
- [`docs/features/lord-spawn-guard.md`](../features/lord-spawn-guard.md): the landless-culture crash and the guard that covers it.
- [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md): what the validator does and does not check.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/kingdoms.md](./kingdoms.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
