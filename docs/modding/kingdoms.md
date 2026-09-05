# Kingdoms

## What this file is

A kingdom is the map faction a clan swears to: the thing that owns fiefs, declares wars and paints the party banners on the campaign map. TAOM declares 14 of its own kingdoms in `taom_spkingdoms.xml` and rewrites the 8 vanilla ones in place with `spkingdoms.xslt`, for 22 live kingdoms in total. <!-- measured: python over taom_spkingdoms.xml + spkingdoms.xslt, see Numbers 2026-09-05 --> The entry itself is small (a name, a culture, a ruling hero, a capital, two pairs of colours, a banner code, its day-one stances and its starting policies), because lords, clans and settlements all point up at the kingdom from their own files rather than being listed here.

## Where it lives and how it is registered

| What | Where |
|---|---|
| TAOM's own 14 kingdoms | [`Main/_Module/ModuleData/taom_spkingdoms.xml`](../../Main/_Module/ModuleData/taom_spkingdoms.xml) |
| The 8 vanilla rewrites | [`Main/_Module/ModuleData/spkingdoms.xslt`](../../Main/_Module/ModuleData/spkingdoms.xslt) |
| Registration | [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) line 70 `<XmlName id="Kingdoms" path="spkingdoms"/>` and line 130 `<XmlName id="Kingdoms" path="taom_spkingdoms"/>` |
| Root element | `<Kingdoms>` |
| Per entry | `<Kingdom>` |
| Engine class | `TaleWorlds.CampaignSystem.Kingdom`, registered as `RegisterType<Kingdom>("Kingdom", "Kingdoms", 20u, ...)` (`Campaign.cs:1545`) |

The lowercase name is the real one. `taom_spkingdoms.xml` is what is on disk and what line 130 registers; `docs/features/kingdom-creation.md` writes `TAOM_spkingdoms.xml` on eight lines, which works on Windows and is still wrong as a path. <!-- measured: rg -c 'TAOM_spkingdoms' docs/features/kingdom-creation.md 2026-09-05 -->

The XSLT does not add kingdoms. It rewrites the 8 entries in `SandBox/ModuleData/spkingdoms.xml` (the vanilla file: `empire`, `empire_w`, `empire_s`, `sturgia`, `aserai`, `vlandia`, `battania`, `khuzait`) into Dunland, Gondor, Mordor, Dale, Harad, Rohan, Khand and Rhun. That file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. TAOM never edits it: the XSLT sits in this repo and is applied at load. `TAOM_Map/ModuleData/spkingdoms.xml` exists too but is an empty `<Kingdoms/>` stub, so nothing kingdom-shaped lives in the map module.

**The whole file is read only when a new campaign starts.** `SandBoxManager.InitializeSandboxXMLs` guards `LoadXML("Kingdoms")` with `if (!isSavedCampaign)` (`SandBoxManager.cs:371-375`), so an existing save never sees your edit, no matter how many times it is loaded. See [Registration](submodule-and-registration.md) for how the two `<XmlNode>` blocks are ordered.

## Attributes

Every one of TAOM's 14 entries carries the same 16 attributes. Fourteen of them are read by the deserializer and are in the table below; the other two are dead and are covered after it. <!-- measured: python -c len(ET.parse(taom_spkingdoms.xml).getroot()[0].attrib) 2026-09-05 -->

<!-- engine-table type="TaleWorlds.CampaignSystem.Kingdom" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Kingdom.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | Yes | none, the load throws | The kingdom's internal handle. Everything else points at it as `Kingdom.<id>`. Two entries with the same id are one kingdom either way: across two files the rows are merged before they are read, inside one file the second re-runs the deserializer over the first (see Gotchas). | `MBObjectManager.cs:1391` |
| `name` | localized string | Yes | none, the load throws | The formal display name on the map and in the encyclopedia. Read unguarded, so an entry without it kills the whole file. | `Kingdom.cs:764` |
| `short_name` | localized string | No | falls back to `name` | The informal form used mid-sentence and in clickable kingdom links. | `Kingdom.cs:764` |
| `culture` | `Culture.<id>` | In practice yes | null, and null crashes | The culture the kingdom belongs to. Sets its basic recruit (`Kingdom.cs:166`) and its starting policies. A missing or misspelled value throws inside `InitializeKingdom` at `Kingdom.cs:573`. | `Kingdom.cs:764` |
| `owner` | `Hero.<id>` | No | no ruling clan, so no leader | The ruling hero. The engine stores that hero's **clan**, not the hero, so the hero must already have a `faction=` in the heroes file or the kingdom ends up leaderless. | `Kingdom.cs:765` |
| `initial_home_settlement` | `Settlement.<id>` | No | null | Fallback capital. Consulted only while the kingdom owns no settlement at all, for map-centre maths and for spawning lords that have nowhere to go (`FactionHelper.cs:497-506`). | `Kingdom.cs:760-762` |
| `color` | hex AARRGGBB | No | `0` (transparent black) | Primary faction colour: party marker on the map and the cloth tint on that faction's troops. | `Kingdom.cs:764` |
| `color2` | hex AARRGGBB | No | `0` | Secondary faction colour, the accent half of the same pair. | `Kingdom.cs:764` |
| `primary_banner_color` | hex AARRGGBB | No | `0` | Banner background colour forced onto every member clan's banner while it belongs to this kingdom (`Clan.cs:1376-1386`). Separate value from `color`; changing one does not change the other. | `Kingdom.cs:766` |
| `secondary_banner_color` | hex AARRGGBB | No | `0` | Banner icon colour forced onto every member clan's banner (`Clan.cs:1381`). | `Kingdom.cs:767` |
| `banner_key` | dot-separated banner code | No | a random banner seeded from `id`, stable across runs (`Kingdom.cs:775`) | The heraldry, as a flat list of numbers. Read in groups of ten, one layer per group (`Banner.cs:580-584`). A malformed code is swallowed and leaves an empty banner rather than crashing (`Banner.cs:245-248`). | `Kingdom.cs:768-771` |
| `text` | localized string | No | empty text | The encyclopedia blurb. Flavour only. | `Kingdom.cs:756` |
| `title` | localized string | No | empty text | The heading on the encyclopedia page. | `Kingdom.cs:757` |
| `ruler_title` | localized string | No | empty text | What this kingdom calls its ruler ("King", "Brenin"). | `Kingdom.cs:758` |

A `banner_key` is not opaque. `TryGetBannerDataFromCode` splits it on `.` and walks it ten numbers at a time, each group building one banner layer: icon id, two colour ids, an x and y position, an x and y size, two flags that are true when the number is `1`, and a rotation that is the tenth number times `0.0027777778` (`Banner.cs:576-593`). Erebor's 90 numbers are therefore exactly nine layers. Numbers left over at the end that do not complete a group of ten are ignored, and a group holding anything that is not an integer clears the whole list and gives you an empty banner with no message. TAOM has no doc that decodes the icon and colour id pools, so the working method is still to copy a code from a kingdom whose banner you like and change one group at a time.

Colours are parsed with `Convert.ToUInt32(value, 16)`. The shipped entries mix the two spellings inside a single kingdom, `color="FF004D26"` bare and `primary_banner_color="0xff0A5730"` prefixed, and both have shipped for as long as the file has existed.

These two are written on all 14 entries and on all 8 XSLT rewrites, and nothing reads them: <!-- measured: rg -c 'settlement_banner_mesh' Main/_Module/ModuleData/taom_spkingdoms.xml Main/_Module/ModuleData/spkingdoms.xslt 2026-09-05 -->

<!-- engine-ref type="TaleWorlds.CampaignSystem.Kingdom" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Kingdom.cs" lines="753-814" -->

| Attribute | Status |
|---|---|
| `settlement_banner_mesh` | Read but has no effect. `Kingdom.Deserialize` never touches it, and the string has 0 hits across the whole v1.4.8 managed decompile (the command is in Numbers below). |
| `flag_mesh` | Same. Copy it forward or delete it, either is safe. What it drove before, and whether native code still consumes it, is not determined from the engine. |

## Child elements

<!-- engine-table type="TaleWorlds.CampaignSystem.Kingdom" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Kingdom.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<relationships>` | wrapper element | No | no stances declared, everything defaults to Neutral | Matched by exact lower-case name. Its children are the stance rows. The element name of each child is not checked, so `<relationship>` is convention only. | `Kingdom.cs:779` |
| `kingdom` | `Kingdom.<id>` | One of `kingdom` or `clan` | the row targets nothing | The other kingdom in this stance. Ignored entirely when `clan` is also present. | `Kingdom.cs:783` |
| `clan` | `Faction.<id>` | One of `kingdom` or `clan` | see above | Targets a single clan instead of a kingdom. Wins over `kingdom` when both are written. | `Kingdom.cs:783` |
| `value` | integer | Yes | the load throws | Only the sign is read. Below zero declares war, zero or above sets neutral. `-1` and `-100` are the same row; `0` and `1` are the same row. | `Kingdom.cs:784` |
| `isAtWar` | `true` or `false` | No | nothing extra happens | `true` declares war on top of whatever `value` already did. `false` does nothing at all: it cannot cancel a war that a negative `value` just declared. | `Kingdom.cs:792` |
| `<policies>` | wrapper element | No | only the culture's default policies apply | Matched by exact lower-case name, in the `else` branch, so one element cannot be both this and `relationships`. | `Kingdom.cs:800` |
| `id` | policy id | Yes on a policy row | the load throws | The raw policy id, for example `policy_castle_charters`, with **no** `Policy.` prefix, unlike every other reference in this file. An id that does not resolve is skipped in silence. | `Kingdom.cs:806` |

Policies are additive. `InitializeKingdom` has already applied the culture's `DefaultPolicyList` by the time this block is read (`Kingdom.cs:573-576`) and `AddPolicy` de-duplicates (`Kingdom.cs:725-731`), so a `<policies>` block can only add. There is no way to remove a culture default from XML.

## Worked example

The head of the Erebor entry, verbatim. All 14 entries have this exact shape.

<!-- example file="Main/_Module/ModuleData/taom_spkingdoms.xml" id="erebor" -->

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
        <relationships>
            <relationship
                kingdom="Kingdom.khuzait"
                value="0"
                isAtWar="false" />
```

Thirteen more `<relationship>` rows follow, then the tail: <!-- measured: python -c len(erebor.find('relationships')) 2026-09-05 -->

<!-- excerpt file="Main/_Module/ModuleData/taom_spkingdoms.xml" -->

```xml
        </relationships>
        <policies>
            <policy
                id="policy_royal_privilege" />
            <policy
                id="policy_lord_prerogative" />
            <policy
                id="policy_religious_privilege" />
            <policy
                id="policy_castle_charters" />
        </policies>
    </Kingdom>
```

The three you change first:

1. **`color` and `color2`.** These are the faction tint, and on a campaign troop wearing grayscale cloth they are the armour colour you see in battle, because the troop's team colour resolves to its clan's map faction, which for a sworn clan is the kingdom. `primary_banner_color` and `secondary_banner_color` are a different pair and only touch banners.
2. **`owner`.** `Hero.lord_E1_1` is a TAOM hero declared in [`Main/_Module/ModuleData/characters/heroes.xml`](../../Main/_Module/ModuleData/characters/heroes.xml). Thirteen of the 14 kingdoms name a TAOM hero there; Dol Guldur names the vanilla hero `lord_1_48`, which `heroes.xslt` retags. Either is legal. <!-- measured: python cross-check of every owner= against characters/heroes.xml 2026-09-05 --> See [Lords and heroes](lords-and-heroes.md).
3. **`culture`.** `Culture.erebor` and the kingdom id `erebor` are the same word but two different objects, and 14 of the 22 live kingdom ids are also culture ids. <!-- measured: python set intersection of kingdom ids with taom_spcultures.xml Culture ids 2026-09-05 --> The 8 rewritten vanilla kingdoms are where this bites: the kingdom id stays `empire_w` while its culture is `Culture.gondor` (`spkingdoms.xslt:55`). Getting the two confused is the single most common way a config entry goes dead. See [Cultures](cultures.md).

## Recipes: Add / Modify / Delete

### Add

**Add a relationship row (declare a war or a peace at day one).**

1. Open [`Main/_Module/ModuleData/taom_spkingdoms.xml`](../../Main/_Module/ModuleData/taom_spkingdoms.xml) and find the `<relationships>` block of the kingdom you want to change.
2. Add one row. War: `<relationship kingdom="Kingdom.empire_w" value="-1" isAtWar="true" />`. Peace: the same row with `value="0" isAtWar="false"`.
3. Write the target as `Kingdom.<id>` with the dot. Without it the load throws `MBInvalidReferenceException` (`MBObjectManager.cs:1524-1533`).
4. Add the row on one side only. The engine writes a single shared stance link for the pair, so the mirror row is redundant (`FactionManager.cs:119-140`). If you write both and they disagree, the kingdom that appears later in the file wins.
5. Keep XML comments outside the `<relationships>` block. The loop walks every child node and reads attributes off it, and a comment node has none, so it throws (`Kingdom.cs:781-784`). Nothing shows you the throw: `LoadXML` swallows it (`MBObjectManager.cs:790-796`), leaving that kingdom half-built and every kingdom after it in the merged file unloaded.
6. Know what the check below proves. Neither of the two mistakes above is a syntax error, so a plain `ElementTree.parse` prints `ok` on a file carrying both. The command asserts the shape instead: every child of a `<relationships>` block has to be a `relationship` element with a `value` and a dotted `Kingdom.` or `Faction.` target. It prints `ok`, or the kingdoms that fail. What it cannot tell you is whether the id after the dot exists, because an unknown one is manufactured as a blank placeholder rather than reported (`MBObjectManager.cs:713-730`).

Check: `python -c "import xml.etree.ElementTree as ET; r=ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml', ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))).getroot(); bad=[(k.get('id'), getattr(c.tag,'__name__',c.tag)) for k in r if k.tag=='Kingdom' for g in k if g.tag=='relationships' for c in g if callable(c.tag) or c.tag!='relationship' or c.get('value') is None or not (c.get('kingdom','').startswith('Kingdom.') or c.get('clan','').startswith('Faction.'))]; print(bad or 'ok')"`
Takes effect: new campaign only
Code: No code changes needed

**Add a whole kingdom.** Thirteen files have to move together, in dependency order (strings before the XML that references them, culture before kingdom, clans before lords). That sequence, the id naming patterns and the crash list live in [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md), and the step-by-step version is [Add a kingdom](recipe-add-a-kingdom.md). The kingdom-level configs your new id must also appear in are listed under Gotchas below.

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: new campaign only
Code: No code changes needed

### Modify

**Change a kingdom's colours.**

1. For one of TAOM's own 14, edit `color` / `color2` on the entry in `taom_spkingdoms.xml` by hand.
2. For one of the 8 rewritten vanilla kingdoms, edit the palette table in [`tools/repaint_kingdom_colors.py`](../../tools/repaint_kingdom_colors.py) and run it. It touches `color` and `color2` in `spkingdoms.xslt` only, never the banner colours or `banner_key`.
3. Run it with no arguments first. That is a dry run and prints an old to new line per kingdom; `--apply` writes the file and a `.bak`.
4. Remember the two pairs are independent. Repainting `color` leaves every clan banner in that kingdom exactly as it was.

Check: `python tools/repaint_kingdom_colors.py`
Takes effect: new campaign only
Code: No code changes needed

**Change a war or an alliance stance.**

1. Day-one stance: edit the `<relationship>` row as above. That is the only lever the XML has.
2. Everything after day one is TAOM's own diplomacy, and it is JSON. Alliance and hostility tiers are pairs in [`Main/_Module/ModuleData/diplomacy/diplomacy.json`](../../Main/_Module/ModuleData/diplomacy/diplomacy.json), one row per pair, `tier` being `Permanent`, `Natural`, `Neutral` or `Hostile`.
3. Scripted escalation is [`Main/_Module/ModuleData/diplomacy/war_of_the_ring.json`](../../Main/_Module/ModuleData/diplomacy/war_of_the_ring.json): `phase1` fires on day 30 and declares its two listed wars, `phase2` on day 44 with `autoWarBetweenHostileTiers` turning every `Hostile` pair into a war. <!-- measured: python -c print(war_of_the_ring.json phase1/phase2) 2026-09-05 -->
4. Both files are read once per process by singletons registered in [`Main/Features/Diplomacy/DiplomacyIoC.cs`](../../Main/Features/Diplomacy/DiplomacyIoC.cs) lines 12 and 15, so relaunch the executable. A save load is not enough.
5. Malformed JSON is not a crash and not a warning you will notice: `LoadConfig` returns an empty config and logs one error, which silently drops every tier in the file. Check the syntax before you launch.

Check: `python -m json.tool Main/_Module/ModuleData/diplomacy/diplomacy.json > /dev/null && echo ok`
Takes effect: full game restart
Code: No code changes needed

### Delete

**Retire a kingdom, do not delete it.** Deleting a `<Kingdom>` entry orphans every clan whose `super_faction` names it, every lord in those clans and every config row keyed on the id, and none of that reports an error at load.

1. For one of TAOM's 14: leave the entry in place and empty its `<relationships>` block, so it starts at peace with everyone and never joins a war.
2. Strip its settlements instead, and give it an `initial_home_settlement` anyway, so `FactionHelper.GetMidSettlementOfFaction` has something to return (`FactionHelper.cs:497-506`). A faction whose culture owns nothing is the #374 daily-tick crash, which is what the `LANDLESS_CULTURE` gate exists to catch.
3. Deleting an `<xsl:template match="Kingdom[@id='...']">` block from `spkingdoms.xslt` is safe and different: it does not remove a kingdom, it restores the vanilla one under its vanilla name and colours.
4. Whatever you do, remove the id from `configs/army_targeting.json` in the same edit or `WarTheaterConfigInvariantsTests` fails: it reads the kingdom list out of `taom_spkingdoms.xml` and `spkingdoms.xslt` rather than a hardcoded list.

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: new campaign only
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A misspelled `culture=` crashes the game at startup. A misspelled `owner=` or `initial_home_settlement=` does not.** Every reference type here is registered with auto-create, so an unknown id quietly manufactures a blank placeholder instead of failing (`MBObjectManager.cs:713-730`). A ghost culture has a null default-policy list, and `InitializeKingdom` walks it immediately (`Kingdom.cs:573`). A ghost hero just leaves the kingdom leaderless.
- **Every reference needs the `Type.id` dot form.** `culture="erebor"` throws `MBInvalidReferenceException`; `culture="Culture.erebor"` is right. The one exception is a policy id, which is bare (`MBObjectManager.cs:1517-1535`, `Kingdom.cs:806`).
- **Rename the root element and the entire file is ignored, with no message.** `LoadXml` scans for a root whose name matches the registered list name `Kingdoms` and returns quietly when it finds none (`MBObjectManager.cs:1371-1386`).
- **Two entries with the same id in two different files become one kingdom before anything is read, and the later attributes win one at a time.** `Kingdoms.xsd` gives `Kingdom` a unique `id` (`Kingdoms.xsd:139-142`), so the merger folds the later row into the earlier one and writes only the attributes the later row actually states (`MBObjectManager.cs:799-817`). An attribute you leave off keeps the earlier file's value; it does not fall back to a default. `<relationships>` and `<policies>` are marked `AlwaysPreferMerge` (`Kingdoms.xsd:14`, `:51`), so their rows accumulate on top of each other as well. To wipe the earlier row instead of layering onto it, put `_replaceWhileMerging="true"` on your `<Kingdom>` (`MBObjectManager.cs:804-808`, `:829-832`).
- **Two entries with the same id inside one file are the case that does reset.** The merger only folds one file into another, so both rows survive it and `Deserialize` runs twice over the same kingdom. The second run reads only what the second row states, and every attribute it omits falls to a default: no `banner_key` gives you a random banner, no `color` gives you transparent black (`MBObjectManager.cs:1387-1393`, `Kingdom.cs:756-776`).
- **A comment inside `<relationships>` or `<policies>` throws.** Those loops iterate every child node and read its attributes, and a comment node has none (`Kingdom.cs:781-784`). A comment between `<Kingdom>` entries is skipped safely (`MBObjectManager.cs:1389`), which is why the shipped file has a comment above every entry and none inside one.
- **`isAtWar="false"` does nothing.** It cannot cancel a war that a negative `value` declared on the same row; only the `true` branch is read (`Kingdom.cs:792`).
- **A pair you never mention is at peace, not at war.** The default stance between two kingdoms is Neutral (`DefaultDiplomacyModel.cs:1082-1089`), so the "list every other kingdom" advice in `docs/features/kingdom-creation.md` is a habit, not a requirement. The shipped file does not follow it: 186 of the 231 possible pairs are declared and the rest are neutral by default. <!-- measured: python pair sweep over taom_spkingdoms.xml + spkingdoms.xslt 2026-09-05 -->
- **For a pair declared twice, the last row processed wins.** Both `DeclareWar` and `SetNeutral` overwrite the shared stance link unconditionally, because the guard they carry only ever trips for bandit factions (`FactionManager.cs:119-140`, `DefaultDiplomacyModel.cs:1073-1080`). No shipped pair currently disagrees with itself.
- **A misspelled attribute name is ignored in silence, and the schema does not stop it.** There is a schema, `Kingdoms.xsd`, but it lives in the game root's `XmlSchemas` folder rather than with the modules: 0 `.xsd` files exist anywhere under `Modules` <!-- measured: find . -name '*.xsd' under the game's Modules folder 2026-09-05 -->, and the engine looks for it at `XmlSchemas/Kingdoms.xsd` beside the executable, or at `<your module>/ModuleData/XmlSchemas/Kingdoms.xsd` if you ship your own (`ModuleHelper.cs:242-250`). What that file governs is the merge above, not your spelling: a validation failure is printed to the log and never thrown (`MBObjectManager.cs:1324-1336`), so `colour=` instead of `color=` still gives you a black kingdom and no warning you will see.
- **An unknown policy id is skipped, not reported** (`Kingdom.cs:806-810`), and there is no XML way to remove a policy the culture already granted.
- **Kingdom display names are English-only for 12 of the 14.** The inline `{=key}Default` is the English text, but 60 of the 70 string keys on these entries have no `<string>` row anywhere in ModuleData <!-- measured: python sweep of Main/_Module/ModuleData/**/*.xml outside Languages 2026-09-05 -->, so nothing can translate them. Only `shaghana` and `abanissa` are registered. The XSLT side does it properly, through `taom_xslt_strings.xml`, and its keys are present in all 12 language folders. <!-- measured: rg -l 'TAOM_dunland"' Main/_Module/ModuleData/Languages/*/*.xml 2026-09-05 --> See [Strings and localization](strings-and-localization.md).
- **In the XSLT, a localization brace must be doubled.** `spkingdoms.xslt` writes `name="{{=TAOM_dunland}}Dunland"` because a single `{` inside an XSLT attribute starts an attribute value template. Copying a name out of `taom_spkingdoms.xml` into the XSLT without doubling the braces changes what the player sees (`spkingdoms.xslt:26`).
- **Adding a kingdom means updating every kingdom-keyed config, not just diplomacy.** The RCA behind that rule is in [`docs/features/new-factions-misty-mountains-lindon.md`](../features/new-factions-misty-mountains-lindon.md).

| Config | Keyed by | Missing key behaves as | Guarded by |
|---|---|---|---|
| `diplomacy/diplomacy.json` | kingdom id pairs | no modifier for that pair | nothing, a typo is silent |
| `diplomacy/war_of_the_ring.json` | kingdom ids in `wars` | that war is never declared | `WarOfTheRingShippedConfigTests` (days and the two phase-1 wars only) |
| `execution/alignment.json` | kingdom id **or** culture id | `FactionSide.Neutral`, which mis-scores executions (`AlignmentService.cs:41`) | nothing |
| `configs/army_targeting.json` | kingdom id | neutral theater weighting | `WarTheaterConfigInvariantsTests`, which fails the build on a dead key or a missing kingdom |
| `siege/siege_defense_config.json` | kingdom id | no bespoke siege message | nothing, and the 4 newest kingdoms are already absent |
| `factionmap/factions.json` | a lore key, with `game_faction` naming a **culture** id | the region does not appear in character creation | `dotnet test TAOM.Tests --filter FactionMap` |

## Numbers in this chapter

All measured 2026-09-05, from the repo at `bannerlord-1.4.5`.

- **14** kingdoms in `taom_spkingdoms.xml`: `python -c "import xml.etree.ElementTree as ET;print(len(ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot().findall('Kingdom')))"` <!-- measured 2026-09-05 -->
- **8** vanilla kingdoms rewritten by the XSLT: `rg -c "Kingdom\[@id=" Main/_Module/ModuleData/spkingdoms.xslt` <!-- measured 2026-09-05 -->
- **22** live kingdom ids, **231** possible pairs, **186** declared: one script over both files, the same pair of sources `WarTheaterConfigInvariantsTests.LoadKingdomIds` reads. <!-- measured: python over taom_spkingdoms.xml + spkingdoms.xslt 2026-09-05 -->
- **16** attributes on every entry, of which **14** are read and **2** are dead: `python -c "import xml.etree.ElementTree as ET;print(len(ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot()[0].attrib))"` <!-- measured 2026-09-05 -->
- **90** numbers in Erebor's `banner_key`, which is 9 layers of 10: `python -c "import xml.etree.ElementTree as ET;print(len(ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot()[0].get('banner_key').split('.')))"` <!-- measured 2026-09-05 -->
- **220** `<relationship>` rows and **56** `<policy>` rows in `taom_spkingdoms.xml`: `rg -c '<relationship\b' <file>` and `rg -c '<policy\b' <file>` <!-- measured 2026-09-05 -->
- **2** rows in `taom_spkingdoms.xml` declare war, both on `empire_w`: `rg -c 'value="-1"' Main/_Module/ModuleData/taom_spkingdoms.xml` <!-- measured 2026-09-05 -->
- **8** relationship rows in the XSLT, forming **4** mutual pairs: `rg -c '<relationship$' Main/_Module/ModuleData/spkingdoms.xslt` plus a per-template extraction. <!-- measured 2026-09-05 -->
- **14** ids that are both a kingdom id and a culture id: a set intersection of the kingdom ids with the `<Culture id=>` ids in `taom_spcultures.xml`. <!-- measured: python over taom_spkingdoms.xml, spkingdoms.xslt, taom_spcultures.xml 2026-09-05 -->
- **70** inline `{=key}` strings on the 14 entries, **60** with no `<string>` row: a python sweep of `Main/_Module/ModuleData/**/*.xml` outside `Languages/`. <!-- measured 2026-09-05 -->
- **0** hits for `settlement_banner_mesh` or `flag_mesh` in the v1.4.8 managed decompile: `rg -l 'settlement_banner_mesh|flag_mesh' -g '*.cs' .` run in the decompile root. <!-- measured 2026-09-05 -->
- **0** `.xsd` files under the game's `Modules` folder: `find . -name '*.xsd' | wc -l` run there. <!-- measured 2026-09-05 -->
- **130** rows in `diplomacy.json` over **22** kingdom ids, split 61 `Hostile`, 38 `Permanent`, 24 `Natural`, 7 `Neutral`: `python -c "import json,collections;d=json.load(open('Main/_Module/ModuleData/diplomacy/diplomacy.json',encoding='utf-8-sig'))['relationships'];print(len(d),collections.Counter(r['tier'] for r in d))"` <!-- measured 2026-09-05 -->
- **24** keys in `execution/alignment.json`, being the 22 kingdom ids plus the culture ids `gondor` and `mordor`: `python -c "import json;print(len(json.load(open('Main/_Module/ModuleData/execution/alignment.json',encoding='utf-8-sig'))))"` <!-- measured 2026-09-05 -->
- **22** `KingdomTheaters` keys in `configs/army_targeting.json`, **18** `KingdomMessages` keys in `siege/siege_defense_config.json` (missing `goblin`, `mistymountainorcs`, `bluecraig`, `lindon`), **45** keys in `factionmap/factions.json`: `python -c "import json;print(len(json.load(open(<file>,encoding='utf-8-sig'))[<section>]))"` per file. <!-- measured 2026-09-05 -->
- **14** `<relationship>` rows and **4** `<policy>` rows on the Erebor entry alone: `python -c "import xml.etree.ElementTree as ET;e=ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot()[0];print(len(e.find('relationships')),len(e.find('policies')))"` <!-- measured 2026-09-05 -->
- **13** of the 14 `owner=` heroes are declared in `characters/heroes.xml`; Dol Guldur's `lord_1_48` is not, and is matched instead by `rg -n "lord_1_48" Main/_Module/ModuleData/heroes.xslt`. <!-- measured 2026-09-05 -->
- **12** of the 14 kingdoms have no registered string at all, and **12** language folders carry the XSLT keys: `ls Main/_Module/ModuleData/Languages/*/std_taom_xslt_strings_*.xml` and `rg -l 'TAOM_dunland"' Main/_Module/ModuleData/Languages/*/*.xml`, both 12. <!-- measured 2026-09-05 -->
- **8** lines of `docs/features/kingdom-creation.md` spell the file `TAOM_spkingdoms.xml`: `rg -c 'TAOM_spkingdoms' docs/features/kingdom-creation.md` <!-- measured 2026-09-05 -->
- **Day 30** and **day 44** are the shipped phase triggers, with **2** wars listed in phase 1 and **0** in phase 2: `python -c "import json;w=json.load(open('Main/_Module/ModuleData/diplomacy/war_of_the_ring.json',encoding='utf-8-sig'));print(w['phase1']['triggerDay'],len(w['phase1']['wars']),w['phase2']['triggerDay'],len(w['phase2']['wars']))"` <!-- measured 2026-09-05 -->
- **PASS**, no issues, from `python tools/validate_moduledata.py --code LANDLESS_CULTURE` against the tree as shipped. <!-- measured 2026-09-05 -->

## Read next

- [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md) for the 13-file ordered sequence, the id naming table and the known crash list.
- [`docs/features/diplomacy.md`](../features/diplomacy.md) for the tier scores and what each one blocks.
- [`docs/features/war-of-the-ring.md`](../features/war-of-the-ring.md) for the kingdom id to Middle-earth name mapping.
- [`docs/features/army-targeting.md`](../features/army-targeting.md) for theaters, priority targets and the aggression multipliers.
- [`docs/features/execution.md`](../features/execution.md) for how `alignment.json` is consumed.
- [`docs/features/faction-map.md`](../features/faction-map.md) for the character-creation faction panel, which is culture-keyed rather than kingdom-keyed.
- [`docs/features/new-factions-misty-mountains-lindon.md`](../features/new-factions-misty-mountains-lindon.md) for the last four kingdoms added and what the first pass missed.
- [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md) for the fatal and silent checklist a new faction's culture has to satisfy.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/banners-and-heraldry.md](./banners-and-heraldry.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/settlements.md](./settlements.md)

<!-- backlinks-end -->
