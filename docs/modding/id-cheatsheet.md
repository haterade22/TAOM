# Id cheatsheet

## What this file is

This chapter is the one table nobody should have to reconstruct: every id family a TAOM data file can name, with the exact spelling the engine reads. It covers culture and kingdom ids, settlement and lord prefixes, the 15 race ids in merge order, the item-id prefix to Armory folder map, equipment slot names, roster template flags, the enums, the 18 skills, the 25 traits, the 20 item modifier groups, the 22 village types, the dotted-reference rule with its bare-id exceptions, and which ids a save file binds. Every count here was measured on 2026-09-05 with the command shown beside it; when a dev doc and a file disagreed, the file won and the disagreement is listed under Gotchas.

## Culture ids

The trap first. **Six cultures keep their vanilla Calradian id** because `spcultures.xslt` rewrites their `name` attribute and never their `id` (`Main/_Module/ModuleData/spcultures.xslt:14-20, 322-328, 612-618, 911-917, 1191-1197, 1315-1321`). Writing the lore name produces a dead key: `culture_exists("rohan")` returns `false` <!-- measured: mcp__taom-moduledata__culture_exists rohan 2026-09-05 -->, and the alignment tables resolve such a key to Neutral without a warning ([prisoner-recruitment](../features/prisoner-recruitment.md), lines 137-155).

| Lore name | Culture id | Also wrong |
|---|---|---|
| Rohan | `vlandia` | `rohan` |
| Dunland | `empire` | `dunland` |
| Harad | `aserai` | `harad` |
| Rhûn (Easterlings) | `khuzait` | `rhun` |
| Dale (Bardings) | `sturgia` | `dale` |
| Khand (Variags) | `battania` | `khand` |

The full set. 24 cultures are authored in `Main/_Module/ModuleData/taom_spcultures.xml` <!-- measured: python re.findall(r'<Culture\b[^>]*?\bid="([^"]+)"', taom_spcultures.xml, re.S) 2026-09-05 --> and the six above are XSLT-wrapped, so TAOM data may name 30 culture ids. The last column counts the `culture=` attribute on `<Settlement>` elements in the live map, hideouts included, 988 settlements in all <!-- measured: python re.findall over TAOM_Map/ModuleData/settlements.xml, culture per <Settlement> 2026-09-05 -->. `TAOM_Map/ModuleData/settlements.xml` lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix.

| Culture id | Display name (`name=`) | Kind | Live-map settlements |
|---|---|---|---|
| `gondor` | Gondorian | custom, settled | 93 |
| `mordor` | Mordor | custom, settled | 56 |
| `erebor` | Dwarves | custom, settled | 52 |
| `shaghana` | Shaghâna | custom, settled | 50 |
| `abanissa` | Âbanissa | custom, settled | 45 |
| `umbar` | Umbar | custom, settled | 40 |
| `mistymountainorcs` | Misty Mountain Orcs | custom, settled | 33 |
| `gundabad` | Gundabad Orcs | custom, settled | 27 |
| `mirkwood` | Silvan Elves | custom, settled | 24 |
| `dolguldur` | Dol Guldur Orcs | custom, settled | 23 |
| `rivendell` | Ñoldor Elves | custom, settled | 21 |
| `bluecraig` | Blue Craig Goblins | custom, settled | 20 |
| `lothlorien` | Galadhrim Elves | custom, settled | 19 |
| `isengard` | Isengard | custom, settled | 12 |
| `goblin` | Goblins | custom, settled | 7 |
| `lindon` | Falathrim Elves | custom, settled | 5 |
| `umbar_corsairs` | Corsairs of Umbar | custom, bandit (hideouts) | 36 |
| `harad_raiders` | Haradrim Raiders | custom, bandit (hideouts) | 34 |
| `rhun_raiders` | Rhûn Raiders | custom, bandit (hideouts) | 20 |
| `gundabad_raiders` | Gundabad Orc Raiders | custom, bandit (hideouts) | 20 |
| `dunland_raiders` | Dunlending Raiders | custom, bandit (hideouts) | 19 |
| `gondor_soldiers` | Gondor Soldiers | custom, bandit (hideouts) | 10 |
| `erebor_warriors` | Blacklocks | custom, bandit (hideouts) | 10 |
| `mirkwood_stalkers` | Mirkwood Stalkers | custom, bandit (hideouts) | 10 |
| `khuzait` | Easterlings | vanilla id, XSLT-renamed | 92 |
| `aserai` | Haradrim | vanilla id, XSLT-renamed | 50 |
| `vlandia` | Rohirrim | vanilla id, XSLT-renamed | 48 |
| `battania` | Variag | vanilla id, XSLT-renamed | 44 |
| `sturgia` | Barding | vanilla id, XSLT-renamed | 37 |
| `empire` | Dunlendings | vanilla id, XSLT-renamed | 31 |

The validator accepts 40 culture ids <!-- measured: mcp__taom-moduledata__list_cultures 2026-09-05 -->: the 30 above plus ten vanilla ids that `spcultures.xslt` never strips (`darshi`, `desert_bandits`, `forest_bandits`, `looters`, `mountain_bandits`, `neutral_culture`, `nord`, `sea_raiders`, `steppe_bandits`, `vakken`). Those ten own no TAOM settlement; naming one on a lord is the `LANDLESS_CULTURE` crash class ([tools README](../../tools/README.md), row `validate_moduledata.py`). Which cultures borrow another culture's troops and party templates is in [cultures](../cultures.md), lines 17-37; the menu link colours keyed on these ids are in [menu-link-colors](../features/menu-link-colors.md), lines 77-95.

## Kingdom ids

22 kingdoms exist: 14 in `Main/_Module/ModuleData/taom_spkingdoms.xml` <!-- measured: python re.findall(r'<Kingdom\b[^>]*?\bid="([^"]+)"', taom_spkingdoms.xml, re.S) 2026-09-05 --> and 8 vanilla kingdoms that `Main/_Module/ModuleData/spkingdoms.xslt` rewrites in place <!-- measured: rg -c "xsl:template match=\"Kingdom\[@id=" spkingdoms.xslt 2026-09-05 -->. Three kingdom ids do not match their culture id, and that is where every `alignment.json`-style config goes wrong: `empire_w` is Gondor, `empire_s` is Mordor, and plain `empire` is Dunland. There is no `Kingdom.gondor`, no `Kingdom.mordor` and no `Culture.empire_w`. The Side column is quoted from [war-of-the-ring](../features/war-of-the-ring.md), lines 243-263, which lists 16 of the 22; the six it omits are marked.

| Kingdom id | Display name | `culture=` | Defined in | Side (war-of-the-ring.md) |
|---|---|---|---|---|
| `empire_w` | Gondor | `Culture.gondor` | `spkingdoms.xslt:45-76` | Free |
| `vlandia` | Rohan | `Culture.vlandia` | `spkingdoms.xslt:165-192` | Free |
| `erebor` | Erebor | `Culture.erebor` | `taom_spkingdoms.xml:4-89` | Free |
| `sturgia` | Dale | `Culture.sturgia` | `spkingdoms.xslt:109-136` | Free |
| `rivendell` | Imladris | `Culture.rivendell` | `taom_spkingdoms.xml` | Free |
| `lothlorien` | Lothlorien | `Culture.lothlorien` | `taom_spkingdoms.xml` | Free |
| `mirkwood` | Lasgalen | `Culture.mirkwood` | `taom_spkingdoms.xml` | Free |
| `lindon` | Lindon | `Culture.lindon` | `taom_spkingdoms.xml` | not in that table |
| `empire_s` | Mordor | `Culture.mordor` | `spkingdoms.xslt:77-108` | Dark Power |
| `isengard` | Isengard | `Culture.isengard` | `taom_spkingdoms.xml` | Dark Power |
| `gundabad` | Gundabad | `Culture.gundabad` | `taom_spkingdoms.xml` | Dark Power |
| `dolguldur` | Dol Guldur | `Culture.dolguldur` | `taom_spkingdoms.xml` | Dark Power |
| `khuzait` | Rhun | `Culture.khuzait` | `spkingdoms.xslt:221-246` | Dark Power |
| `goblin` | Goblins | `Culture.goblin` | `taom_spkingdoms.xml` | not in that table |
| `mistymountainorcs` | Misty Mountain Orcs | `Culture.mistymountainorcs` | `taom_spkingdoms.xml` | not in that table |
| `bluecraig` | Goblins of Blue Craig | `Culture.bluecraig` | `taom_spkingdoms.xml` | not in that table |
| `empire` | Dunland | `Culture.empire` | `spkingdoms.xslt:13-42` | Evil (independent) |
| `aserai` | Harad | `Culture.aserai` | `spkingdoms.xslt:137-164` | Southern |
| `umbar` | Umbar | `Culture.umbar` | `taom_spkingdoms.xml` | Southern |
| `shaghana` | Shaghâna | `Culture.shaghana` | `taom_spkingdoms.xml` | not in that table |
| `abanissa` | Âbanissa | `Culture.abanissa` | `taom_spkingdoms.xml` | not in that table |
| `battania` | Khand | `Culture.battania` | `spkingdoms.xslt:193-220` | Neutral |

Do not lift the kingdom tables in `docs/features/alignment-aware-execution.md` (lines 233-234 label `empire` Rohan and `vlandia` Arthedain) or `docs/features/execution.md` (lines 77-78 label `empire_w` Rohan and `vlandia` Gondor); the XSLT above says otherwise. A player-founded kingdom gets an engine-generated `new_kingdom*` id that is in no shipped table, which is why the alignment lookups fall back to the culture id (`docs/reviews/agents-md-review-lessons-archive.md:5`, review 70).

## Region prefixes and lord prefixes

Settlement ids follow `town_<P><n>`, `castle_<P><n>`, `village_<P><n>` and `castle_village_<P><n>_<m>`, where `<P>` is a region prefix. The table below is measured from the live `TAOM_Map/ModuleData/settlements.xml` (id prefix and `culture=` on each `<Settlement>`), with the region name and naming idiom quoted from [taom-map-settlement-naming](../reference/taom-map-settlement-naming.md), lines 74-108, where that doc has the row. <!-- measured: python census of (town|castle_village|castle|village)_([A-Z]+) id prefixes against culture= over TAOM_Map/ModuleData/settlements.xml 2026-09-05 -->

| Prefix | Region | Culture on the live map (count) | Settlements |
|---|---|---|---|
| `A` | Khand / Harad, Aserai-mapped | `aserai` 50, `shaghana` 42, `abanissa` 17 | 109 |
| `DG` | Dol Guldur | `dolguldur` 23 | 23 |
| `E` | Erebor | `erebor` 52 | 52 |
| `EN` | Dunland (Empire-North) | `empire` 30 | 30 |
| `ES` | Mordor | `mordor` 56 | 56 |
| `EW` | Gondor | `gondor` 93 | 93 |
| `FH` | Far Harad | `abanissa` 28, `shaghana` 8 | 36 |
| `G` | Gundabad | `gundabad` 27 | 27 |
| `GBC` | Blue Craig (not in the naming doc) | `bluecraig` 20 | 20 |
| `GT` | Goblin-town (not in the naming doc) | `goblin` 7 | 7 |
| `I` | Isengard | `isengard` 8 | 8 |
| `K` | Khand (Variag) | `battania` 44, `khuzait` 4 | 48 |
| `L` | Lothlórien | `lothlorien` 19 | 19 |
| `LN` | Lindon (not in the naming doc) | `lindon` 5 | 5 |
| `M` | Northern Mirkwood | `mirkwood` 24 | 24 |
| `MM` | Misty Mountains (not in the naming doc) | `mistymountainorcs` 33 | 33 |
| `R` | Rivendell | `rivendell` 21 | 21 |
| `RU` | Rhûn | `khuzait` 88 | 88 |
| `S` | Dale | `sturgia` 37 | 37 |
| `U` | Umbar | `umbar` 40 | 40 |
| `V` | Rohan | `vlandia` 48 | 48 |

Five ids break the pattern and four of them are Isengard's: `town_isengard`, `castle_orthanc_gate`, `village_isengard_a`, `castle_village_isengard_a`, plus `retirement_retreat` (`Culture.empire`). Hideouts are `hideout_<biome>_<n>` over eight biome words (`desert`, `erebor`, `forest`, `gondor`, `mirkwood`, `mountain`, `seaside`, `steppe`) and carry the bandit cultures; 159 of the 988 settlements are hideouts <!-- measured: same census, ids starting hideout_ 2026-09-05 -->.

Lord ids follow `lord_{PREFIX}{CLAN_N}_{MEMBER_N}` ([kingdom-creation](../features/kingdom-creation.md), line 74). The prefix is a separate id space from the settlement prefix: goblin lords are `GB` while goblin settlements are `GT`, and Blue Craig lords are `BC` while its settlements are `GBC` ([xml-data rule](../../.claude/rules/xml-data.md), line 39, plus the census below). Measured from the 1,184 `<NPCCharacter>` elements in `Main/_Module/ModuleData/characters/lords.xml` <!-- measured: python census of lord_([A-Za-z]+) id prefixes against culture= over characters/lords.xml 2026-09-05 -->:

| Lord prefix | Culture | Lords |
|---|---|---|
| `MM` | `mistymountainorcs` | 150 |
| `D` | `dolguldur` | 126 |
| `G` | `gundabad` | 100 |
| `SH` | `shaghana` | 90 |
| `AB` | `abanissa` | 80 |
| `M` | `mordor` 42 and `mirkwood` 30 (one prefix, two cultures) | 72 |
| `I` | `isengard` | 45 |
| `BC` | `bluecraig` | 40 |
| `GB` | `goblin` | 40 |
| `EW` | `gondor` | 38 |
| `E` | `erebor` | 36 |
| `R` | `rivendell` | 12 |
| `L` | `lothlorien` | 11 |
| `WE` | `gondor` | 11 |
| `U` | `umbar` | 10 |
| `LN` | `lindon` | 10 |
| `NE` | `empire` | 8 |
| `K` | `khuzait` | 5 |
| `S` | `sturgia` | 5 |
| `SE` | `mordor` | 5 |
| `V` | `vlandia` | 5 |
| `A` | `aserai` | 4 |
| `B` | `battania` | 3 |
| `lord_rohan_N_N` | `vlandia` | 12 |
| numeric `lord_N_N` and `lord_N_N_N` (vanilla form) | `khuzait` 93, `gondor` 41, `vlandia` 28, `aserai` 24, `mordor` 24, `sturgia` 22, `empire` 19, `battania` 15 | 266 |

So the "2-char uppercase, unique across all kingdoms" rule in kingdom-creation.md line 73 is the convention for a **new** kingdom; shipped data uses one to two letters and shares `M`. The other id patterns from the same table (kingdom-creation.md, lines 69-81) and the notable slots from the xml-data rule (lines 16-23):

| Concept | Pattern | Example |
|---|---|---|
| Clan | `clan_{culture_id}_{N}` | `clan_shaghana_1` |
| Notable NPC | `spc_notable_{culture_id}_{slot}` (slots `_0` to `_4b`, `_5` to `_13`, `_gl1`, `_gl4`, `_21`, `_22`) | `spc_notable_shaghana_0` |
| Headman | `spc_{culture_id}_headman_{N}` | `spc_shaghana_headman_1` |
| Wanderer | `spc_wanderer_{culture_id}_{N}` | `spc_wanderer_shaghana_3` |
| Wanderer skill set | `spc_wanderer_{culture_id}_{N}_skills` | `spc_wanderer_shaghana_3_skills` |
| Kingdom strings | `taom_{id}_*` | `taom_shaghana_name` |
| Culture and lord strings | `aom_{id}_*` | `aom_lord_SH1_1_name` |

## Race ids in merge order

A race id is not an object reference. `BasicCharacterObject.Deserialize` sets `Race = 0` and, only if `race=` is present, looks the string up in FaceGen's dictionary (`BasicCharacterObject.cs:323-328`). That dictionary is built once from the native race-id list, split on `;`, and each race's integer is its position in the merged `skins.xml` order (`FaceGen.cs:17-27`, the `TaleWorlds.MountAndBlade` one). `GetRaceOrDefault` is a bare dictionary indexer with no fallback (`FaceGen.cs:115-118`), so a misspelt or mis-cased `race=` throws `KeyNotFoundException` at load, and the Monster for a race is fetched by the same string (`FaceGen.cs:49-60`). Index 0 being `human` is a strong inference, not decompile proof, because the list comes from native code ([black-numenorean](../features/black-numenorean.md), lines 162-176); everything else in the table is read straight off the files. `skins.xml` is registered through `project.mbproj` (`soln_skins`), not `SubModule.xml`, and there is no managed deserializer for it. <!-- measured: python re.finditer(r'<race\b[^>]*?\bid="([^"]+)"', skins.xml, re.S) over Native and LOTRLOME_Armory; rg -o 'race="[^"]+"' Main/_Module/ModuleData | sort | uniq -c 2026-09-05 -->

| Index | Race id | Declared at | `race=` uses in repo data |
|---|---|---|---|
| 0 | `human` | `Native/ModuleData/skins.xml:3` | 8 (3 wanderers, 5 named companions) |
| 1 | `dwarf` | `LOTRLOME_Armory/ModuleData/skins.xml:3` | 194 |
| 2 | `uruk` | `LOTRLOME_Armory/ModuleData/skins.xml:14953` | 163 |
| 3 | `nazghul` | `LOTRLOME_Armory/ModuleData/skins.xml:30860` | 0 |
| 4 | `orc` | `LOTRLOME_Armory/ModuleData/skins.xml:45257` | 295 |
| 5 | `uruk_hai` | `LOTRLOME_Armory/ModuleData/skins.xml:59204` | 171 |
| 6 | `berserker` | `LOTRLOME_Armory/ModuleData/skins.xml:75869` | 10 |
| 7 | `cave_troll` | `LOTRLOME_Armory/ModuleData/skins.xml:92404` | 1 |
| 8 | `hill_troll` | `LOTRLOME_Armory/ModuleData/skins.xml:108723` | 0 |
| 9 | `pale_uruk` | `LOTRLOME_Armory/ModuleData/skins.xml:125563` | 212 |
| 10 | `dg_uruk` | `LOTRLOME_Armory/ModuleData/skins.xml:141470` | 238 |
| 11 | `goblin` | `LOTRLOME_Armory/ModuleData/skins.xml:157356` | 289 |
| 12 | `elf` | `LOTRLOME_Armory/ModuleData/skins.xml:173244` | 458 |
| 13 | `saruman` | `LOTRLOME_Armory/ModuleData/skins.xml:190015` | 0 |
| 14 | `sauron` | `LOTRLOME_Armory/ModuleData/skins.xml:204237` | 0 |

Human troops carry no `race=` at all; the eight `race="human"` uses are all on wanderers and named companions (`taom_wanderers.xml:490, 516, 662` and `named_companions/named_companions.xml:4, 147, 399, 557, 653`). The Armory file is 220,975 lines <!-- measured: wc -l LOTRLOME_Armory/ModuleData/skins.xml 2026-09-05 -->; `TAOM_Map/ModuleData/skins.xml` is a 7-line file declaring no race <!-- measured: same finditer over TAOM_Map/ModuleData/skins.xml 2026-09-05 -->. Because the integer is a position, inserting a race anywhere but the end renumbers every race after it; that is why the `sauron` block was appended last (worked example below) and why hero races are persisted by name, not by index ([hero-race](../features/hero-race.md)). A race also needs five `<Monster>` ids (`<race>`, `<race>_child`, `<race>_settlement`, `<race>_settlement_slow`, `<race>_settlement_fast`) and an `as_<race>_facegen` action set, or townsfolk fail to spawn and the mesh T-poses ([culture-playability-wiring](../features/culture-playability-wiring.md), row 14).

## Item id prefix to Armory folder

`LOTRLOME_Armory/ModuleData/LOTRLOME_items/` has 18 culture folders <!-- measured: ls -d LOTRLOME_items/*/ | wc -l 2026-09-05 --> (`arnor`, `dale`, `dol_guldur`, `dunland`, `erebor`, `gondor`, `gundabad`, `harad`, `iron_hills`, `isengard`, `mercenary`, `mirkwood`, `mordor`, `rhun`, `rivendell`, `rohan`, `thenn`, `troll`) plus the three root files `LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml` and `LOTRAOM_horses.xml`. The rule from [armory-guide](../reference/armory-guide.md), lines 19-36: before authoring an item, grep every folder for the prefix, because a second folder holding the same prefix produces silent duplicate-id shadowing. The folder column below is where the items actually are today, counted per folder; two rows disagree with the guide and are marked. <!-- measured: python count of <Item|CraftedItem id= per prefix per LOTRLOME_items subfolder 2026-09-05 --> A troop wearing an id that resolves to nothing appears in underwear, no error; the gate is `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`.

| Item prefix | Folder holding it (items) | Note |
|---|---|---|
| `sk_gd_*` | `gondor/` (318) | |
| `sk_md_orc_*` | `mordor/` (61) | |
| `sk_gn_orc_*` | `mordor/` (42) | generic orc pool shared across factions |
| `sk_uruk_mordor_*` | `mordor/` (90) | |
| `ar_ardunian_*` | `mordor/` (1) | |
| `urukscout_*` | `mordor/` (4) | the guide says `isengard/`; the files are `mordor/{body,head,leg,arm}_armors.xml` |
| `clo_urukscout_*` | no folder (0) | listed by the guide, no item carries it |
| `sk_uruk_hai_*` | `isengard/` (137) | |
| `sk_is_orc_*` | `isengard/` (13) | |
| `sk_dg_uruk_*` | `dol_guldur/` (118) | |
| `sk_dg_orc_*` | `dol_guldur/` (14) | |
| `sk_dg_khml_*` | `rhun/` (194) | Khamul, cross-faction with Dol Guldur |
| `sk_gb_uruk_*` | `gundabad/` (101) | |
| `sk_dwarf_erebor_*` | `erebor/` (117) | |
| `sk_dwarf_iron_*` | `iron_hills/` (130) | not `erebor/` |
| `sk_dwarf_dain_*` | `iron_hills/` (38) | the guide says `erebor/` |
| `sk_rh_loke_*` | `rhun/` (198) | |
| `sk_rh_drag_*` | `rhun/` (194) | |

## Equipment slot names

An `<equipment slot="..." id="..."/>` row names its slot with the XML vocabulary in the first column; the engine maps the four `Item` names onto the weapon indices and passes every other name to a case-sensitive `Enum.Parse` on `EquipmentIndex` (`Equipment.cs:225-236`, enum values in `EquipmentIndex.cs:3-26`). `slot="body"` therefore throws; `slot="Body"` works. The `id` accepts either `Item.<id>` or a bare `<id>` (`Equipment.cs:211`). Counts are `slot=` occurrences across `Main/_Module/ModuleData` <!-- measured: rg -o 'slot="[^"]+"' Main/_Module/ModuleData | sort | uniq -c 2026-09-05 -->. Twelve slots in all (`Equipment.cs:39`, `EquipmentSlotLength = 12`). The C# names `Weapon0` to `Weapon3` are never written in XML; the `item-equipment-model.md` engine doc that lists them as slot names is on the do-not-lift list for exactly that reason.

<!-- engine-ref type="TaleWorlds.Core.Equipment" file="Core/TaleWorlds.Core/TaleWorlds.Core/Equipment.cs" lines="225-236" -->

| XML `slot=` | `EquipmentIndex` | Index | Uses in repo data |
|---|---|---|---|
| `Item0` | `Weapon0` | 0 | 4,449 |
| `Item1` | `Weapon1` | 1 | 3,089 |
| `Item2` | `Weapon2` | 2 | 1,869 |
| `Item3` | `Weapon3` | 3 | 760 |
| `Item4` | `ExtraWeaponSlot` | 4 | 0 |
| `Head` | `Head` | 5 | 4,119 |
| `Body` | `Body` | 6 | 9,648 |
| `Leg` | `Leg` | 7 | 9,473 |
| `Gloves` | `Gloves` | 8 | 4,284 |
| `Cape` | `Cape` | 9 | 3,858 |
| `Horse` | `Horse` | 10 | 1,235 |
| `HorseHarness` | `HorseHarness` | 11 | 1,226 |

## Equipment roster template flags

A shared `<EquipmentRoster>` in `equipmentsets/*.xml` may carry one `<Flags .../>` child. Each attribute name on it is parsed as an `EquipmentCategories` enum member, case-sensitively, and a misspelt name throws (`MBEquipmentRoster.cs:73-84`; values `EquipmentCategories.cs:5-14`). The five flags: <!-- measured: for each flag: rg -o '<flag>="true"' Main/_Module/ModuleData/equipmentsets | wc -l 2026-09-05 -->

<!-- engine-ref type="TaleWorlds.Core.MBEquipmentRoster" file="Core/TaleWorlds.Core/TaleWorlds.Core/MBEquipmentRoster.cs" lines="73-84" -->

| Flag attribute | Bit | Rosters setting it to `true` in repo data |
|---|---|---|
| `IsFemaleTemplate` | 1 | 155 |
| `IsLordTemplate` | 2 | 222 |
| `IsChildEquipmentTemplate` | 4 | 90 |
| `IsTeenagerEquipmentTemplate` | 8 | 44 |
| `IsKingdomRulerTemplate` | 16 | 88 |

310 `<Flags>` elements exist in the repo's equipment sets <!-- measured: rg -o '<Flags' Main/_Module/ModuleData/equipmentsets | wc -l 2026-09-05 -->. The set type is a separate attribute on `<EquipmentSet>`: `equipmentType="Battle"`, `"Civilian"` or `"Stealth"` (`MBEquipmentRoster.cs:91-98`, enum `Equipment.cs:14-20`). The older `civilian="true"` is still honoured: on an inline `<EquipmentRoster>` with no complaint (`MBEquipmentRoster.cs:99-103`), and on a troop's `<EquipmentSet>` reference with a debug assert (`BasicCharacterObject.cs:395-401`). Repo data has 2,562 `equipmentType="Civilian"` and 1,569 `civilian="true"` <!-- measured: rg -o '(civilian|equipmentType)="[^"]+"' over equipmentsets, troops, characters | sort | uniq -c 2026-09-05 -->; the validator's `MISSING_CIVILIAN_TYPE` check wants the new spelling.

## Enums the XML spells out

Three attributes are enum names, not object ids, and each is parsed differently.

**`occupation`** (`CharacterObject.cs:539-542`): case-sensitive `Enum.Parse`, so a misspelling throws and the file fails to load. The 33 values in `Occupation.cs:3-39` <!-- measured: sed -n 5,37p Occupation.cs | rg -c '^\s*\w+,?$' 2026-09-05 -->, with TAOM's usage counts across `Main/_Module/ModuleData` (27 distinct values used) <!-- measured: rg -o 'occupation="[^"]+"' Main/_Module/ModuleData | sort | uniq -c 2026-09-05 -->: The same string is read a second time as a case-insensitive substring test: `IsSoldier` is true when the word `soldier` appears anywhere in it (`BasicCharacterObject.cs:329-333`).

<!-- engine-ref type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" lines="539-542" -->

| Value | Uses | Value | Uses | Value | Uses |
|---|---|---|---|---|---|
| `NotAssigned` | 0 | `Weaponsmith` | 18 | `RuralNotable` | 59 |
| `Tavernkeeper` | 18 | `Armorer` | 18 | `PrisonGuard` | 18 |
| `Mercenary` | 22 | `HorseTrader` | 18 | `Guard` | 0 |
| `Lord` | 1,325 | `TavernWench` | 18 | `ShopWorker` | 18 |
| `GoodsTrader` | 17 | `TavernGameHost` | 18 | `Musician` | 18 |
| `ArenaMaster` | 18 | `Bandit` | 8 | `Gangster` | 0 |
| `Villager` | 40 | `Wanderer` | 227 | `Blacksmith` | 18 |
| `Soldier` | 900 | `Artisan` | 46 | `BannerBearer` | 0 |
| `Townsfolk` | 366 | `Merchant` | 227 | `CaravanGuard` | 66 |
| `RansomBroker` | 10 | `Preacher` | 66 | `Special` | 0 |
| | | `Headman` | 72 | `ShipWright` | 0 |
| | | `GangLeader` | 157 | | |

<!-- engine-ref type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" lines="489-498, 534-541" -->

**`default_group`** (`BasicCharacterObject.cs:489-496`): case-insensitive `TryParse` on `FormationClass`; an unknown word yields `-1` with no error (`BasicCharacterObject.cs:534-541`), and `IsRanged` and `IsMounted` are derived from this value, not from the equipment (`:495-496`). The ten real classes in `FormationClass.cs:3-20` <!-- measured: rg -c named classes Infantry..Bodyguard in FormationClass.cs 2026-09-05 --> are `Infantry`, `Ranged`, `Cavalry`, `HorseArcher`, `Skirmisher`, `HeavyInfantry`, `LightCavalry`, `HeavyCavalry`, `General`, `Bodyguard`; the rest are counters. TAOM uses four: `Infantry` 2,906, `Cavalry` 531, `Ranged` 294, `HorseArcher` 43 <!-- measured: rg -o 'default_group="[^"]+"' Main/_Module/ModuleData | sort | uniq -c 2026-09-05 -->, and the validator's `INVALID_ENUM` accepts only those four (`tools/validate_moduledata.py:23`). `formation_position_preference` (`Back`, `Middle`, `Front`) is parsed case-sensitively and throws on a typo (`BasicCharacterObject.cs:497-498`).

<!-- engine-ref type="TaleWorlds.Core.ItemObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/ItemObject.cs" lines="24-53, 625-628" -->

**`Type` on `<Item>`** (`ItemObject.cs:625-628`): case-insensitive `Enum.Parse` on `ItemTypeEnum`, 27 values (`ItemObject.cs:24-53`) <!-- measured: sed -n 26,52p ItemObject.cs | rg -c '^\s*\w+,?$' 2026-09-05 -->: `Invalid`, `Horse`, `OneHandedWeapon`, `TwoHandedWeapon`, `Polearm`, `Arrows`, `Bolts`, `SlingStones`, `Shield`, `Bow`, `Crossbow`, `Sling`, `Thrown`, `Goods`, `HeadArmor`, `BodyArmor`, `LegArmor`, `HandArmor`, `Pistol`, `Musket`, `Bullets`, `Animal`, `Book`, `ChestArmor`, `Cape`, `HorseHarness`, `Banner`. The Armory uses twelve: `HeadArmor` 1,021, `BodyArmor` 746, `Cape` 448, `HandArmor` 350, `LegArmor` 339, `Shield` 225, `Bow` 35, `HorseHarness` 34, `Arrows` 28, `Horse` 10, `Crossbow` 3, `Bolts` 2 <!-- measured: rg -o 'Type="[^"]+"' -g '*.xml' LOTRLOME_items | sort | uniq -c 2026-09-05 -->. The same grep also returns `Blade` 347, `Handle` 347, `Pommel` 187 and `Guard` 143: those sit on `<Piece Type=...>` rows inside a `<CraftedItem>` (`ItemObject.cs:457`) and are piece kinds, not item types. A weapon's `<Weapon weapon_class=...>` component is parsed with a case-sensitive `Enum.Parse` on `WeaponClass` (`WeaponComponentData.cs:364`), so unlike `Type` it throws on `onehandedsword`.

## Skill ids

Exactly 18 skills are registered, in `DefaultSkills.cs:115-132` <!-- measured: sed -n 115,132p DefaultSkills.cs | rg -c 'Create\("' 2026-09-05 -->. A `<skill id="..." value="..."/>` row resolves its id with a bare `GetObject`; an unknown id is silently dropped and the skill stays 0 (`PropertyOwner.cs:71-88`). The id is `Crafting`; there is no `Smithing` id. The attribute grouping is the `CharacterAttribute` each skill is initialised with (`DefaultSkills.cs:88-105`, three per attribute in that order), and line 96 is where `Crafting` gets its on-screen name "Smithing". Every one of the 145 lord skill sets in `Main/_Module/ModuleData/taom_lord_skill_sets.xml` names all 18 <!-- measured: rg -o '<skill[^>]*id="[^"]+"' taom_lord_skill_sets.xml | rg -o 'id="[^"]+"' | sort | uniq -c 2026-09-05 -->. A troop with `skill_template="SkillSet.<id>"` ignores its inline `<skills>` block (`BasicCharacterObject.cs:337-345, 353-355`); pick one.

<!-- engine-ref type="TaleWorlds.Core.DefaultSkills" file="Core/TaleWorlds.Core/TaleWorlds.Core/DefaultSkills.cs" lines="88-132" -->

| Vigor | Control | Endurance | Cunning | Social | Intelligence |
|---|---|---|---|---|---|
| `OneHanded` | `Bow` | `Riding` | `Tactics` | `Charm` | `Steward` |
| `TwoHanded` | `Crossbow` | `Athletics` | `Scouting` | `Trade` | `Medicine` |
| `Polearm` | `Throwing` | `Crafting` | `Roguery` | `Leadership` | `Engineering` |

## Trait ids

Exactly 25 traits are registered in `DefaultTraits.cs:129-153` <!-- measured: sed -n 129,153p DefaultTraits.cs | rg -c 'Create\("' 2026-09-05 -->; their ranges come from `DefaultTraits.cs:164-188`. A `<Trait id="..." value="..."/>` row goes through the same `PropertyOwner` reader as skills, so an unknown id is dropped without a message and the value is never clamped to the range (`PropertyOwner.cs:78-85`). No module registers a `Traits` XML (`XmlName id="Traits"` appears in no `SubModule.xml` <!-- measured: rg 'XmlName id="(Traits|VillageTypes)"' over every SubModule.xml 2026-09-05 -->), so this list cannot be extended from data. `lords.xml` sets the five personality traits and the three policy traits on nearly every lord (1,169 to 1,172 rows each), `Commander` on 35, and five ids the engine never registers: `Politician` 37, `Manager` 37, `KnightFightingSkills` 30, `CavalryFightingSkills` 3, `ArcherFightingSkills` 1 <!-- measured: rg -o '<Trait[^>]*id="[^"]+"' characters/lords.xml | rg -o 'id="[^"]+"' | sort | uniq -c 2026-09-05 -->. Those five rows are dead data: no file in the v1.4.8 decompile contains the strings <!-- measured: rg -l 'Politician|KnightFightingSkills|CavalryFightingSkills|ArcherFightingSkills' over the v1.4.8 decompile, no hits 2026-09-05 -->, and vanilla's own `SandBox/ModuleData/lords.xml` carries the same dead ids (`Manager` 368, `Politician` 344, `KnightFightingSkills` 23, `CavalryFightingSkills` 10) <!-- measured: rg -o 'id="(Politician|Manager|KnightFightingSkills|CavalryFightingSkills|ArcherFightingSkills)"' SandBox/ModuleData/lords.xml | sort | uniq -c 2026-09-05 -->.

<!-- engine-ref type="TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs" lines="129-188" -->

| Trait id | Range | Shown to the player | Note |
|---|---|---|---|
| `Mercy`, `Valor`, `Honor`, `Generosity`, `Calculating` | -2 to 2 | yes | the five personality traits |
| `curt`, `ironic`, `earnest`, `softspoken` | -2 to 2 | no | lowercase; these are what `voice=` names (`CharacterObject.cs:572-575`) |
| `Egalitarian`, `Oligarchic`, `Authoritarian` | 0 to 20 | yes | |
| `Frequency`, `Commander`, `Surgeon`, `Tracking`, `Blacksmith`, `SergeantCommandSkills`, `EngineerSkills`, `RogueSkills`, `ScoutSkills`, `Trader`, `Thug`, `Smuggler`, `NavalSoldier` | 0 to 20 | no | the C# property `Siegecraft` is the id `EngineerSkills` (`DefaultTraits.cs:97, 145`) |

## Item modifier groups

The 20 group ids live in one file, `Native/ModuleData/item_modifiers_groups.xml` <!-- measured: rg -c '^\s*id="' Native/ModuleData/item_modifiers_groups.xml 2026-09-05 -->, and Native is the only module registering `ItemModifierGroups`. `modifier_group="<id>"` is a **bare** id on the `<Armor>`, `<Weapon>` or `<Horse>` component (and on a `<CraftedItem>`); an unknown id resolves to null with no warning and the item never rolls a quality modifier (`ItemComponent.cs:18-26`). The ItemModifier ids in `Native/ModuleData/item_modifiers.xml` (`legendary_sword` at line 6, `legendary_bow` at line 57, and the rest of the `legendary_*` family) are a different namespace and are never valid here. The Armory uses 15 of the 20 (`plate` 1,629, `chain` 722, `leather` 399, `shield` 215, `cloth` 135, `polearm` 112, `bow` 35, `arrow` 28, `axe` 27, `mace` 22, `cloth_unarmoured` 21, `sword` 7, `crossbow` 3, `spear_dart_throwing` 2, `bolt` 2) and three values that are not groups at all: `shield_wood` 10, `mail` 1, `false` 1 <!-- measured: rg -o 'modifier_group="[^"]+"' -g '*.xml' LOTRLOME_items | sort | uniq -c 2026-09-05 -->. Those 12 items silently never roll Fine or Masterwork; `chain` is the id `mail` was meant to be.

| Weapons | Ammunition and thrown | Armour and other |
|---|---|---|
| `sword`, `mace`, `axe`, `polearm`, `cheap_weapon`, `bow`, `crossbow`, `shield` | `arrow`, `bolt`, `axe_throwing`, `knife_throwing`, `spear_dart_throwing` | `plate`, `chain`, `leather`, `cloth`, `cloth_unarmoured`, `horse`, `companion` |

## Village types

22 village types are registered in C#, `DefaultVillageTypes.cs:107-132` <!-- measured: sed -n 109,130p DefaultVillageTypes.cs | rg -c 'Create\("' 2026-09-05 -->. The XML attribute is dotted, `village_type="VillageType.<id>"`, read on every load including a save (`Village.cs:293`). No module registers a `VillageTypes` XML, so a new type needs C#, not data. Three ids do not match their C# property name, which is the trap when reading the decompile. The last column counts `village_type=` on the live map <!-- measured: rg -o 'village_type="[^"]+"' TAOM_Map/ModuleData/settlements.xml | sort | uniq -c 2026-09-05 -->; 21 of the 22 are in use <!-- measured: rg -o 'village_type="VillageType\.[^"]+"' TAOM_Map/ModuleData/settlements.xml | sort -u | wc -l 2026-09-05 -->, and `battanian_horse_ranch` is the one nobody placed.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Settlements.DefaultVillageTypes" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/DefaultVillageTypes.cs" lines="107-132" -->

| Id | C# property | Villages on the live map |
|---|---|---|
| `wheat_farm` | `WheatFarm` | 78 |
| `cattle_farm` | `CattleRange` | 69 |
| `iron_mine` | `IronMine` | 64 |
| `lumberjack` | `Lumberjack` | 58 |
| `fisherman` | `Fisherman` | 55 |
| `silver_mine` | `SilverMine` | 43 |
| `swine_farm` | `HogFarm` | 41 |
| `trapper` | `Trapper` | 37 |
| `sheep_farm` | `SheepFarm` | 18 |
| `salt_mine` | `SaltMine` | 16 |
| `vineyard` | `VineYard` | 15 |
| `clay_mine` | `ClayMine` | 14 |
| `date_farm` | `DateFarm` | 13 |
| `silk_plant` | `SilkPlant` | 13 |
| `steppe_horse_ranch` | `SteppeHorseRanch` | 13 |
| `europe_horse_ranch` | `EuropeHorseRanch` | 12 |
| `olive_trees` | `OliveTrees` | 12 |
| `vlandian_horse_ranch` | `VlandianHorseRanch` | 12 |
| `desert_horse_ranch` | `DesertHorseRanch` | 11 |
| `flax_plant` | `FlaxPlant` | 10 |
| `sturgian_horse_ranch` | `SturgianHorseRanch` | 3 |
| `battanian_horse_ranch` | `BattanianHorseRanch` | 0 |

## The dotted reference rule and its bare-id exceptions

Most cross-file attributes are read by `ReadObjectReferenceFromXml`, which splits the value on the first `.` into a type name and an id; a value with no dot, or with an empty half, throws `MBInvalidReferenceException` (`MBObjectManager.cs:1517-1535`, generic twin at `:1497-1515`). The type name is the element name each class was registered with (`Campaign.cs:1531-1570`): `NPCCharacter`, `Culture`, `Faction` for a clan, `Kingdom`, `Trait`, `VillageType`, `PartyTemplate`, `Settlement`, `Hero`, `Policy`, and `Item` for items. A name that matches no type record fails an assert and comes back `null` (`MBObjectManager.cs:733-734`); an id that matches nothing gets a placeholder, one log line and an unregister, the forward-safe mechanism in [load-order-and-dependencies](load-order-and-dependencies.md), section C. This table fixes the spelling only.

<!-- engine-ref type="TaleWorlds.ObjectSystem.MBObjectManager" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectManager.cs" lines="713-735, 1437-1459, 1517-1535" -->

| Attribute | Written as | Read at |
|---|---|---|
| `culture=` on a troop, hero, roster, clan, kingdom | `Culture.gondor` | `BasicCharacterObject.cs:484`, `MBEquipmentRoster.cs:59-61`, `Clan.cs:872`, `Kingdom.cs:764` |
| `skill_template=` | `SkillSet.taom_lady_skills` | `BasicCharacterObject.cs:337` |
| `<face_key_template value=...>` | `BodyProperty.fighter_gondor` | `BasicCharacterObject.cs:455-458` |
| `<upgrade_target id=...>` | `NPCCharacter.gondor_footman_t2` | `CharacterObject.cs:561-566` |
| `upgrade_requires=` | `ItemCategory.horse` | `CharacterObject.cs:586` |
| `owner=`, `initial_home_settlement=` on a kingdom or clan | `Hero.lord_E1_1`, `Settlement.town_E1` | `Kingdom.cs:762-765`, `Clan.cs:862-868` |
| `<relationship kingdom=...>` / `clan=` | `Kingdom.khuzait` / `Faction.<clan>` | `Kingdom.cs:783`, `Clan.cs:916` |
| `super_faction=`, `default_party_template=` on a clan | `Kingdom.erebor`, `PartyTemplate.erebor_lord` | `Clan.cs:863, 888` |
| `<PartyTemplateStack troop=...>` | `NPCCharacter.<id>` | `PartyTemplateObject.cs:39` |
| `father=`, `mother=`, `spouse=`, `faction=` on a `<Hero>` | `Hero.<id>`, `Faction.<clan>` | `Hero.cs:1828-1834` |
| `monster=` on a `<Horse>` component | `Monster.horse` | `HorseComponent.cs:150` |
| `village_type=`, `bound=` on a `<Village>` | `VillageType.wheat_farm`, `Settlement.town_E1` | `Village.cs:293, 300` |
| the culture's troop, template and roster attributes | `NPCCharacter.<id>`, `PartyTemplate.<id>`, `EquipmentRoster.<id>` | `CultureObject.cs:270-299` |

The exceptions, each read with a plain `GetObject` or not through the object manager at all, take a **bare** id, and most of them fail silently:

| Attribute | Written as | What a wrong value does | Read at |
|---|---|---|---|
| `<equipment id=...>` in any roster | `Item.<id>` or `<id>`, both accepted | null item, empty slot, no message | `Equipment.cs:204-221` |
| `<EquipmentSet id=...>` inside a troop | `<roster id>` | null roster; the roster must already be loaded, so equipment-set files go before character files in `SubModule.xml` | `BasicCharacterObject.cs:407` |
| `<skill id=...>`, `<Trait id=...>` | `OneHanded`, `Honor` | row dropped, value stays 0 | `PropertyOwner.cs:78-85` |
| `voice=` | `curt` | null, falls back to softspoken | `CharacterObject.cs:572-575` |
| `modifier_group=` | `plate` | null, item never rolls a modifier | `ItemComponent.cs:21-25` |
| `<policy id=...>` in a kingdom | `policy_royal_privilege` | skipped | `Kingdom.cs:804-810` |
| `skeleton_scale=` on a `<Horse>` | `<id>` | null | `HorseComponent.cs:152-155` |
| `<Hero id=...>` in `heroes.xml` | the bare `NPCCharacter` id, `lord_E1_1` | null character, crash on the next line | `Hero.cs:1806` |
| `race=` | `orc` | `KeyNotFoundException` at load | `BasicCharacterObject.cs:324-327`, `FaceGen.cs:115-118` |
| `occupation=`, `default_group=`, `Type=`, `equipmentType=` | enum names | see Enums above | see Enums above |

`python tools/validate_moduledata.py` sweeps the dotted refs for `Item.`, `NPCCharacter.`, `Culture.`, `PartyTemplate.` and `BodyProperty.` targets in the repo and the Armory; it does not open `TAOM_Map`'s 988 `culture=` attributes ([moduledata-validation](../features/moduledata-validation.md)). The MCP server exposes the same registries to an agent (`list_cultures`, `culture_exists`, `item_exists`, `troop_exists`, `find_references`).

## Which ids a save binds

Every registry object writes its `StringId` and its `MBGUID` into the save (`MBObjectBase.cs:11-15`, both `[SaveableProperty]`), and the string id is read straight off the XML `id=` attribute (`MBObjectBase.cs:61`). Renaming an id therefore orphans every reference a save holds to it; the display `name=` is free to change. Which files a saved campaign re-reads and which are new-campaign-only is the reload matrix in [load-order-and-dependencies](load-order-and-dependencies.md), section F; the id-specific rules are these.

- **Heroes, clans, kingdoms and mobile parties are temporary types** (`Campaign.cs:1536, 1543, 1545, 1555`, `isTemporary: true`). A saved campaign never re-reads the `Heroes`, `Kingdoms` or `Factions` XML (`SandBoxManager.cs:360-375`) and `RemoveTemporaryTypes` unregisters whatever the XML did create (`SandBoxManager.cs:344-347`, `MBObjectManager.cs:655-669`). Edits to `heroes.xml`, `clans.xml`, `taom_spkingdoms.xml` and `spkingdoms.xslt` reach a **new campaign only**; the save carries its own copies.
- **Troop ids never change and troops are never deleted**: orphan the troop by removing it from every `upgrade_targets` and keep the element, or mark it `is_obsolete="true"` (`BasicCharacterObject.cs:336`), the flag vanilla uses for its own `obsolete_characters.xml` ([troops rule](../../.claude/rules/troops.md), lines 149-151).
- **Settlement ids are save-bound; never rename one** ([xslt-moduledata lesson](../reviews/lessons/xslt-moduledata.md), line 11). The live file is `TAOM_Map/ModuleData/settlements.xml`; the repo copy under `Main/_Module/ModuleData/` is a stale shadow that is not registered.
- **Race integers are positions, not ids**, so a save written before a race was inserted mid-file would remap every later race; TAOM persists hero races by name for that reason ([hero-race](../features/hero-race.md)).

## Worked example

The `erebor` kingdom shows the three reference families side by side: a dotted `Hero.` owner whose id carries the lord prefix `E`, a dotted `Culture.` id that equals the kingdom id, and a dotted `Settlement.` home whose id carries the region prefix `E`. Lines 4 to 20 of the file are the opening element; lines 21 to 89 (not shown) hold the `<relationships>` block, which names the vanilla ids `Kingdom.khuzait`, `Kingdom.empire_w`, `Kingdom.empire_s`, `Kingdom.aserai`, `Kingdom.vlandia`, `Kingdom.sturgia` and `Kingdom.empire`, and the `<policies>` block, whose `<policy id=...>` rows are bare ids.

<!-- example file="Main/_Module/ModuleData/taom_spkingdoms.xml" id="erebor" lines="4-20" -->
```xml
    <Kingdom
        id="erebor"
        owner="Hero.lord_E1_1"
        initial_home_settlement="Settlement.town_E1"
        banner_key="11.100.75.4345.4345.764.764.1.0.0.521.172.100.62.62.630.618.0.0.268.521.172.100.54.54.548.703.0.0.268.521.172.100.42.42.571.810.0.0.268.521.172.100.62.62.899.618.0.1.88.521.172.100.54.54.980.703.0.1.88.521.172.100.42.42.957.810.0.1.88.24019.31.240.400.400.765.873.1.0.0.24510.31.240.200.200.765.556.1.0.0"
        primary_banner_color="0xff0A5730"
        secondary_banner_color="0xffFFD700"
        color="FF004D26"
        color2="FFB8860B"    
        culture="Culture.erebor"
        settlement_banner_mesh="encounter_flag_a"
        flag_mesh="info_screen_flags_b"
        name="{=taom_erebor_name}Erebor"
        short_name="{=taom_erebor_short_name}Erebor"
        title="{=taom_erebor_title}Kingdom of Erebor"
        ruler_title="{=taom_erebor_ruler_title}King"
        text="{=taom_erebor_desc}The Lonely Mountain, Erebor, stands as the greatest of the Dwarven kingdoms in the north. Rich in gold and mithril, it is home to the line of Durin. The Dwarves of Erebor are renowned craftsmen and fierce warriors, their halls echoing with the songs of their ancestors.">
```

1. `owner="Hero.lord_E1_1"`: dotted `Hero.` plus the lord id `lord_{E}{1}_{1}`, prefix `E`, clan 1, member 1. The hero must exist in `characters/lords.xml` and `characters/heroes.xml`, and `heroes.xml` names it by the bare id.
2. `culture="Culture.erebor"`: dotted `Culture.`; for a TAOM kingdom the culture id equals the kingdom id. For `empire_w` it would be `Culture.gondor`.
3. `initial_home_settlement="Settlement.town_E1"`: dotted `Settlement.` plus the region prefix `E`. The id lives in the live `TAOM_Map` file, and the validator does not check it.

The second example is the comment above the last race in the Armory skin registry, the one line in TAOM that states the merge-order rule.

<!-- example file="LOTRLOME_Armory/ModuleData/skins.xml" id="sauron" lines="204236-204238" -->
```xml
	<!-- sauron: verbatim elf clone (adult min_scale 1.40, NPC-only). Appended at END - race ints are skins.xml merge-order indices (issue #321). -->
	<race
		id="sauron">
```

1. `id="sauron"` on its own line: the id attribute of a `<race>` may sit below the tag, so a one-line grep for `<race id=` misses two of the 14 blocks (`elf` at line 173244 and this one).
2. "Appended at END": a new race goes after `sauron`, never before, or every troop with `race="elf"` or `race="saruman"` silently changes body.

## Gotchas: what fails silently and what crashes

- **The xml-data rule's region-code line disagrees with the map.** It says `EN=Rohan`, `B=Dunland`, `K=Easterlings`; the live file has `EN` = `Culture.empire` (Dunland), no `B` prefix at all, `K` = `Culture.battania` (Khand) with four `khuzait` holdouts, and Rohan under `V`. Trust the census above (`.claude/rules/xml-data.md:37` versus the `TAOM_Map/ModuleData/settlements.xml` census).
- **The naming doc's `K` note is stale too.** It reports 26 of 27 `K` settlements as `battania`; the live count is 44 `battania` and 4 `khuzait` of 48 (`docs/reference/taom-map-settlement-naming.md:88`).
- **Two Armory prefixes live where the guide says they do not** (`urukscout_*` in `mordor/`, `sk_dwarf_dain_*` in `iron_hills/`) and `clo_urukscout_*` names nothing (`docs/reference/armory-guide.md:27-33`). Adding a new item under the guide's folder would create the very shadowing the guide warns about.
- **black-numenorean.md says TAOM has zero `race="human"`; it has eight**, all on wanderers and named companions, none on troops (`docs/features/black-numenorean.md:167-168`).
- **Five trait ids in `lords.xml` do nothing** (`Politician`, `Manager`, `KnightFightingSkills`, `CavalryFightingSkills`, `ArcherFightingSkills`) and a trait value is never clamped to its range (`PropertyOwner.cs:78-85`; `DefaultTraits.cs:129-153`).
- **Twelve Armory items carry a `modifier_group` that is not a group** (`shield_wood`, `mail`, `false`) and will never roll a quality modifier (`ItemComponent.cs:21-25`; census above).
- **`occupation` and `formation_position_preference` crash on a typo; `default_group` does not** and gives formation `-1` (`CharacterObject.cs:542`, `BasicCharacterObject.cs:498, 534-541`).
- **A `<Flags>` attribute name is an enum member.** `IsFemale="true"` throws; `IsFemaleTemplate="true"` works (`MBEquipmentRoster.cs:77-84`).
- **`race=` is case-sensitive and throws on a miss**, unlike almost every other bare id (`FaceGen.cs:115-118`).
- **A hero's `faction=` is `Faction.<clan>`, never `Clan.<clan>`.** The clan type's registered element name is `Faction` (`Campaign.cs:1543`); `Clan` matches no type record, so `GetPresumedObject` returns `null` after a failed assert (`MBObjectManager.cs:733-734`) and `Hero.cs:1835` dereferences it, a `NullReferenceException` that ends the `Heroes` load.

## Numbers in this chapter

All measured 2026-09-05. Live-module paths are relative to the game's `Modules` folder; decompile paths are relative to the v1.4.8 category tree.

| Number | What | Command |
|---|---|---|
| 24 | cultures in `taom_spcultures.xml` | `python: re.findall(r'<Culture\b[^>]*?\bid="([^"]+)"', text, re.S)` over `Main/_Module/ModuleData/taom_spcultures.xml` |
| 6 | cultures rewritten by `spcultures.xslt` | `rg -o "Culture\[@id='[^']+'\]" Main/_Module/ModuleData/spcultures.xslt \| sort -u` |
| 30 | distinct culture ids on the live map | `python: set of culture= per <Settlement>` over `TAOM_Map/ModuleData/settlements.xml` |
| 40 | culture ids the validator accepts | `mcp__taom-moduledata__list_cultures` |
| false | `rohan` as a culture id | `mcp__taom-moduledata__culture_exists rohan` |
| 988 | `<Settlement>` elements on the live map (159 hideouts) | `python: re.finditer(r'<Settlement\b[^>]*?\bid="([^"]+)"', text, re.S)` over `TAOM_Map/ModuleData/settlements.xml` |
| per-culture and per-prefix settlement counts | see the two tables | same census, grouped by `culture=` and by id prefix |
| 14 | kingdoms in `taom_spkingdoms.xml` | `python: re.findall(r'<Kingdom\b[^>]*?\bid="([^"]+)"', text, re.S)` |
| 8 | kingdom templates in `spkingdoms.xslt` | `rg -c "xsl:template match=\"Kingdom\[@id=" Main/_Module/ModuleData/spkingdoms.xslt` |
| 1,184 | `<NPCCharacter>` in `characters/lords.xml`; per-prefix counts | `python: re.match(r'lord_([A-Za-z]+)', id)` grouped against `culture=` |
| 15 | race ids (1 Native + 14 Armory) with line numbers | `python: re.finditer(r'<race\b[^>]*?\bid="([^"]+)"', text, re.S)` over `Native/ModuleData/skins.xml`, `LOTRLOME_Armory/ModuleData/skins.xml`, `TAOM_Map/ModuleData/skins.xml` |
| 220,975 | lines in the Armory `skins.xml` | `wc -l LOTRLOME_Armory/ModuleData/skins.xml` |
| 8, 194, 163, 295, 171, 10, 1, 212, 238, 289, 458 | `race=` uses per id in repo data (11 distinct) | `rg -o 'race="[^"]+"' Main/_Module/ModuleData \| sort \| uniq -c` |
| 18 | Armory item folders | `ls -d LOTRLOME_Armory/ModuleData/LOTRLOME_items/*/ \| wc -l` |
| per-prefix item counts | Armory prefix table | `python: count <Item\|CraftedItem id= per prefix per subfolder` |
| 11 | distinct `slot=` names in repo data, with counts | `rg -o 'slot="[^"]+"' Main/_Module/ModuleData \| sort \| uniq -c` |
| 12 | equipment slots | `Equipment.cs:39`, `EquipmentIndex.cs:3-26` |
| 310 | `<Flags>` elements in repo equipment sets | `rg -o '<Flags' Main/_Module/ModuleData/equipmentsets \| wc -l` |
| 155, 222, 90, 44, 88 | rosters setting each of the five flags to `true` | `rg -o '<flag>="true"' Main/_Module/ModuleData/equipmentsets \| wc -l`, once per flag name |
| 2,562 / 1,569 | `equipmentType="Civilian"` / `civilian="true"` | `rg -o '(civilian\|equipmentType)="[^"]+"'` over `equipmentsets`, `troops`, `characters` |
| 5 | `EquipmentCategories` flags | `sed -n 9,13p EquipmentCategories.cs \| rg -c 'u,?$'` |
| 33 | `Occupation` values; 27 used | `sed -n 5,37p Occupation.cs \| rg -c '^\s*\w+,?$'`; `rg -o 'occupation="[^"]+"' Main/_Module/ModuleData \| sort \| uniq -c` |
| 10 | `FormationClass` classes; 4 used | `rg -c` on the ten named members of `FormationClass.cs`; `rg -o 'default_group="[^"]+"' Main/_Module/ModuleData \| sort \| uniq -c` |
| 27 | `ItemTypeEnum` values; 12 used in the Armory | `sed -n 26,52p ItemObject.cs \| rg -c '^\s*\w+,?$'`; `rg -o 'Type="[^"]+"' -g '*.xml' LOTRLOME_items \| sort \| uniq -c` |
| 18 | skills; 145 lord skill sets naming all 18 | `sed -n 115,132p DefaultSkills.cs \| rg -c 'Create\("'`; `rg -o '<skill[^>]*id="[^"]+"' taom_lord_skill_sets.xml \| rg -o 'id="[^"]+"' \| sort \| uniq -c` |
| 25 | traits; per-id use in `lords.xml`; 0 decompile hits for the five dead ids; vanilla counts | `sed -n 129,153p DefaultTraits.cs \| rg -c 'Create\("'`; `rg -o '<Trait[^>]*id="[^"]+"' characters/lords.xml \| rg -o 'id="[^"]+"' \| sort \| uniq -c`; `rg -l` over the decompile; `rg -o` over `SandBox/ModuleData/lords.xml` |
| 0 | modules registering a `Traits` or `VillageTypes` XML | `rg -n 'XmlName id="(Traits\|VillageTypes)"'` over every `SubModule.xml` |
| 20 | modifier groups; 18 distinct values in the Armory (15 legal, 3 not) | `rg -c '^\s*id="' Native/ModuleData/item_modifiers_groups.xml`; `rg -o 'modifier_group="[^"]+"' -g '*.xml' LOTRLOME_items \| sort \| uniq -c` |
| 22 | village types; 21 in use | `sed -n 109,130p DefaultVillageTypes.cs \| rg -c 'Create\("'`; `rg -o 'village_type="VillageType\.[^"]+"' TAOM_Map/ModuleData/settlements.xml \| sort -u \| wc -l` |
| per-type village counts | village table | `rg -o 'village_type="[^"]+"' TAOM_Map/ModuleData/settlements.xml \| sort \| uniq -c` |

## Read next

- [war-of-the-ring](../features/war-of-the-ring.md), the kingdom side table.
- [prisoner-recruitment](../features/prisoner-recruitment.md), the six vanilla culture ids as a trap.
- [xml-data rule](../../.claude/rules/xml-data.md), notable naming, region codes, the config id cross-reference checklist.
- [black-numenorean](../features/black-numenorean.md), why humans carry no `race=` and why index 0 is an inference.
- [armory-guide](../reference/armory-guide.md), the canonical-folder rule.
- [kingdom-creation](../features/kingdom-creation.md), the naming conventions for a new kingdom.
- [taom-map-settlement-naming](../reference/taom-map-settlement-naming.md), region prefixes and their naming idioms.
- [cultures](../cultures.md), which cultures share rosters.
- [menu-link-colors](../features/menu-link-colors.md), the culture ids as a colour key.
- [object-system-mbobjectmanager](../reference/engine/object-system-mbobjectmanager.md) and [save-system](../reference/engine/save-system.md), the registry and how it persists (their line numbers are v1.4.5).
- [hero-race](../features/hero-race.md), race indices across a save.
- [culture-playability-wiring](../features/culture-playability-wiring.md), the five monsters and the facegen action set a race needs.
- [moduledata-validation](../features/moduledata-validation.md) and the [tools README](../../tools/README.md), what the validator reaches.
- [troops rule](../../.claude/rules/troops.md) and the [xslt-moduledata lesson](../reviews/lessons/xslt-moduledata.md), the save-compatibility rules for troop and settlement ids.
