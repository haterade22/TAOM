# Banners and Heraldry

## What this file is

`banner_icons.xml` is the catalogue of banner art: it declares every cloth pattern, every sigil emblem and every palette colour that a banner in the game is allowed to use, each one behind a number. Nothing in it draws anything by itself, because the numbers are only addresses that a `banner_key` string on a kingdom, a clan or a culture points at. TAOM's copy holds 33 sigil groups, 368 emblems and 150 palette colours, and takes all of its cloth patterns from Native. <!-- measured: python over Main/_Module/ModuleData/banner_icons.xml counting BannerIconGroup, Icon and Color 2026-09-05 -->

## Where it lives and how it is registered

| What | Where |
|---|---|
| The catalogue | [`Main/_Module/ModuleData/banner_icons.xml`](../../Main/_Module/ModuleData/banner_icons.xml), 681 lines |
| Its registration | [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) line 965, `<XmlName id="BannerIcons" path="banner_icons"/>` |
| Root element | `<base type="string">`, then exactly one `<BannerIconData>` |
| Per-entry element | `<BannerIconGroup>` holding `<Background>` or `<Icon>` children, then one `<BannerColors>` block of `<Color>` rows |
| Engine classes | `TaleWorlds.Core.BannerIconGroup` (groups and their children) and `TaleWorlds.Core.BannerManager` (the document walk and the colour palette) |
| Keys that reference it | `banner_key` on `<Kingdom>` in [`taom_spkingdoms.xml`](../../Main/_Module/ModuleData/taom_spkingdoms.xml) and on `<Faction>` in [`characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml), `faction_banner_key` on `<Culture>` in [`taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml), plus the XSLT overrides in [`spkingdoms.xslt`](../../Main/_Module/ModuleData/spkingdoms.xslt) and [`spclans.xslt`](../../Main/_Module/ModuleData/spclans.xslt) ([banner-injection.md](../features/banner-injection.md) lines 49 to 54) |
| The art the numbers address | banner materials under `Main/_Module/AssetSources/BannerIcons/` and `Main/_Module/Assets/BannerIcons/`, UI sprites under `Main/_Module/GUI/SpriteParts/ui_taom_bannericons/` and [`Main/_Module/GUI/TAOMSpriteData.xml`](../../Main/_Module/GUI/TAOMSpriteData.xml) |

Three active modules ship a `banner_icons.xml`: `Native/ModuleData/banner_icons.xml` (1895 lines), `NavalDLC/ModuleData/banner_icons.xml` (68 lines) and TAOM's. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. The engine glues all three into one document with `MBObjectManager.GetMergedXmlForManaged("BannerIcons", skipValidation: false, ...)` (`BannerManager.cs:198`), so a banner draws from the union and you have to plan an id against every active module, not just your own file. The `skipValidation: false` matters: each contributing file is checked against the game's `XmlSchemas/BannerIcons.xsd` on its way into the merge (`MBObjectManager.cs:966-982`, `1343-1359`), so a shape error is caught rather than ignored. A repeated id is not automatically an error, and what it does depends on where it lands. The Child elements section below says which.

The document shape is strict. `<base>` must occur exactly once and the first child of `<base>` must be named `BannerIconData`, or the load throws `TWXmlLoadException("Incorrect XML document format.")` (`BannerManager.cs:222-231`). The XSD then fixes the order inside: `<Background>` elements before `<Icon>` elements inside a group, and the single `<BannerColors>` block after every group (`BannerIcons.xsd:17, 25, 50`). The `type` attribute on `<base>` is allowed by the XSD and read by nothing; copy it verbatim.

## Attributes

**`<BannerIconGroup>`**

<!-- engine-table type="TaleWorlds.Core.BannerIconGroup" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerIconGroup.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | int | Yes | none, the load throws | Identifies the whole sheet or category. It is never seen in game. Two modules using the same group id are merged into one group before the parser runs, so the second adds to the first rather than replacing it. Group ids are their own number space and do not clash with colour ids. | `BannerIconGroup.cs:42` |
| `name` | string wrapped in a `TextObject` | Yes in practice | none, the load throws | The tab label in the banner editor. Because it becomes a `TextObject` it takes a localisation key. TAOM writes the not-translated form, for example `name="{=!}TAOM Gondor Alpha 01"`. | `BannerIconGroup.cs:43` |
| `is_pattern` | bool | Yes in practice | none, the load throws | Declares which half of a banner this group supplies. `true` means cloth patterns and the children must be `<Background>`. `false` means sigils and the children must be `<Icon>`. Content of the wrong type still loads and can never be reached. | `BannerIconGroup.cs:44` |

**`<Icon>`**

<!-- engine-table type="TaleWorlds.Core.BannerIconGroup" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerIconGroup.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | int | Yes | none, the load throws | The number for one emblem. This is what a `banner_key` names, and in TAOM it is also the filename of the UI sprite. For a new emblem, pick one that is free across every active module. Reusing one inside a group another module also declares is an override, not a new emblem, and reusing it in any other group loses silently. See Child elements. | `BannerIconGroup.cs:49` |
| `material_name` | string | Yes in practice | none, the load throws | The compiled banner material the emblem is drawn from, in other words which art sheet it lives on. The engine builds the asset name it preloads by gluing this to `texture_index` (`Module.cs:684-686`). | `BannerIconGroup.cs:50` |
| `texture_index` | int, through `int.Parse` | Yes in practice | none, the load throws | Which cell of that sheet. TAOM's sheets are a four by four grid read row by row, 0 top left to 15 bottom right. A non-numeric value throws a `FormatException` rather than being coerced. | `BannerIconGroup.cs:51` |
| `is_reserved` | bool | No | `false`, meaning player-selectable | Keeps the emblem out of the campaign banner editor's list and out of the random-banner roll, while leaving it usable from a `banner_key`. Vanilla fences off faction crests this way. TAOM reserves none. | `BannerIconGroup.cs:55` |

**`<Background>`**

<!-- engine-table type="TaleWorlds.Core.BannerIconGroup" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerIconGroup.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | int | Yes | none, the load throws | The number for one cloth pattern. It is the first number of every `banner_key`. | `BannerIconGroup.cs:63` |
| `mesh_name` | string | Yes in practice | none, the load throws | The compiled asset that draws the cloth. There is no cell index here, so one id is one whole pattern. | `BannerIconGroup.cs:64` |
| `is_base_background` | bool | No | `false` | Flags one pattern as the engine's default cloth. It is a global setter, not a per-group flag, so the last module to set it wins. Leave it where Native put it. | `BannerIconGroup.cs:65` |

**`<Color>`** (parsed by `BannerManager`, not by `BannerIconGroup`)

<!-- engine-table type="TaleWorlds.Core.BannerManager" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerManager.cs" method="LoadBannerIconsFromXml" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | int | Yes | none, the load throws | The palette slot every colour field of a `banner_key` refers to. Colour ids are their own number space. | `BannerManager.cs:258` |
| `hex` | uint parsed base 16 | Yes in practice | none, the load throws | The colour, alpha first: `0xffB57A1E` is opaque `B57A1E`. Vanilla always writes `ff` for alpha. A key naming an id that is not in the palette gets the debug value `3735928559`, which is `0xDEADBEEF` (`BannerManager.cs:81`). | `BannerManager.cs:261` |
| `player_can_choose_for_sigil` | bool | No | `false` | Shows the swatch in the sigil-colour column of the campaign banner editor. A colour with neither flag still works from a `banner_key`, it is simply invisible to the player. | `BannerManager.cs:262` |
| `player_can_choose_for_background` | bool | No | `false` | Shows the swatch in the background-colour column. The editor also uses it as a probe: if a banner's primary colour does not carry this flag, `BannerEditorVM` decides the two columns are swapped and silently flips them (`BannerEditorVM.cs:753`). | `BannerManager.cs:263` |

Every bool here goes through `Convert.ToBoolean` on the raw attribute string. Both shipped files write `true` and `false` in full, and the XSD types these two colour flags as `xs:boolean` while typing every other attribute as `xs:string` (`BannerIcons.xsd:60-63`). Write the words out.

## Child elements

<!-- engine-table type="TaleWorlds.Core.BannerIconGroup" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerIconGroup.cs" method="Deserialize" -->

| Child | Belongs in | Merge behaviour | Read at (file:line) |
|---|---|---|---|
| `<Icon>` | a group with `is_pattern="false"` | Two outcomes, and the group decides which. Repeat an icon id **inside a group another module also declares** and the XML merge pairs the two rows on their `id`, so the later module's `material_name` and `texture_index` win: that is a working override. Repeat it **in a different group** and the rows never pair, both reach the parser, and the second is discarded with no error and no log line. | `BannerIcons.xsd:44-47`, `MBObjectManager.cs:833-874`, `BannerIconGroup.cs:47-59` |
| `<Background>` | a group with `is_pattern="true"` | The same two outcomes, keyed on `Background@id`. One ordering trap: `is_base_background` is acted on before the discard check, so a `<Background>` that is otherwise ignored can still move the engine's default cloth. | `BannerIcons.xsd:39-42`, `BannerIconGroup.cs:61-72` |

<!-- engine-table type="TaleWorlds.Core.BannerManager" file="Core/TaleWorlds.Core/TaleWorlds.Core/BannerManager.cs" method="LoadBannerIconsFromXml" -->

| Child | Belongs in | Merge behaviour | Read at (file:line) |
|---|---|---|---|
| `<BannerIconData>` | `<base>`, as the first child | Only the first one is walked. A second sibling block is ignored outright. | `BannerManager.cs:227-231` |
| `<BannerIconGroup>` | `<BannerIconData>` | Merged by id, never replaced, and the merging is already done by the time this code runs: `MBObjectManager` pairs the groups on `id` and folds the later one into the earlier node. The C# `Merge` here copies only the keys the first group is missing, but it is looking at a list the XML merge has already collapsed, so it is close to dead code. You can add ids to a vanilla group, and you can restate one of its rows by id. | `BannerIcons.xsd:78-81`, `MBObjectManager.cs:833-874`, `BannerManager.cs:236-248` |
| `<BannerColors>` | `<BannerIconData>`, after every group, at most once | Additive. The XSD allows one block per document and puts it last. | `BannerManager.cs:250` |
| `<Color>` | `<BannerColors>` | Additive by id. The XML merge collapses same-id rows before the parser sees them, so a colliding id resolves to whichever module loaded last, not to the first one (see the Gotchas). | `BannerManager.cs:256-265` |

## Worked example

The first group in TAOM's file, verbatim. Sixteen emblems on one sheet, ids in a reserved block, all sharing one `material_name` and taking cells 0 to 15.

<!-- example file="Main/_Module/ModuleData/banner_icons.xml" id="100" -->

```xml
    <BannerIconGroup id="100" name="{=!}TAOM Gondor Alpha 01" is_pattern="false">
      <Icon id="10000" material_name="taom_banners_gondor_alpha_01" texture_index="0" />
      <Icon id="10001" material_name="taom_banners_gondor_alpha_01" texture_index="1" />
      <Icon id="10002" material_name="taom_banners_gondor_alpha_01" texture_index="2" />
      <Icon id="10003" material_name="taom_banners_gondor_alpha_01" texture_index="3" />
      <Icon id="10004" material_name="taom_banners_gondor_alpha_01" texture_index="4" />
      <Icon id="10005" material_name="taom_banners_gondor_alpha_01" texture_index="5" />
      <Icon id="10006" material_name="taom_banners_gondor_alpha_01" texture_index="6" />
      <Icon id="10007" material_name="taom_banners_gondor_alpha_01" texture_index="7" />
      <Icon id="10008" material_name="taom_banners_gondor_alpha_01" texture_index="8" />
      <Icon id="10009" material_name="taom_banners_gondor_alpha_01" texture_index="9" />
      <Icon id="10010" material_name="taom_banners_gondor_alpha_01" texture_index="10" />
      <Icon id="10011" material_name="taom_banners_gondor_alpha_01" texture_index="11" />
      <Icon id="10012" material_name="taom_banners_gondor_alpha_01" texture_index="12" />
      <Icon id="10013" material_name="taom_banners_gondor_alpha_01" texture_index="13" />
      <Icon id="10014" material_name="taom_banners_gondor_alpha_01" texture_index="14" />
      <Icon id="10015" material_name="taom_banners_gondor_alpha_01" texture_index="15" />
    </BannerIconGroup>
```

1. `id` on the group and `id` on each `<Icon>` are the two you pick first, and both have to be free across every module. TAOM's convention is group id equal to the faction block times ten, a second sheet at plus one, and icon ids in blocks of a hundred ([banner-icon-generation.md](../reference/banner-icon-generation.md) step 3).
2. `material_name` names the sheet, `texture_index` names the cell on it. Get the pair wrong and the wrong emblem renders, with no error.
3. `is_pattern="false"` is what makes `<Icon>` children legal here. Flip it to `true` and all sixteen load into a dictionary nothing ever reads.

One palette row, from the `<BannerColors>` block that closes the file:

<!-- example file="Main/_Module/ModuleData/banner_icons.xml" id="2002" -->

```xml
      <Color id="2002" hex="0xFFdc0000" player_can_choose_for_sigil="true" />
```

TAOM's player-pickable palette declares most hexes twice, once with the sigil flag and once with the background flag, because the two editor columns read different flags.

### The Erebor key, decoded

<!-- example file="Main/_Module/ModuleData/taom_spkingdoms.xml" id="erebor" -->

```xml
        banner_key="11.100.75.4345.4345.764.764.1.0.0.521.172.100.62.62.630.618.0.0.268.521.172.100.54.54.548.703.0.0.268.521.172.100.42.42.571.810.0.0.268.521.172.100.62.62.899.618.0.1.88.521.172.100.54.54.980.703.0.1.88.521.172.100.42.42.957.810.0.1.88.24019.31.240.400.400.765.873.1.0.0.24510.31.240.200.200.765.556.1.0.0"
```

A `banner_key` is one string of integers separated by dots, read in groups of ten, each group one layer (`Banner.cs:531-564` writes them, `Banner.cs:576-594` reads them back). The ten fields in order are mesh id, colour id, second colour id, size X, size Y, position X, position Y, draw stroke as 1 or 0, mirror as 1 or 0, and rotation in whole degrees. Layer 0 is always the background, so its mesh id is a `<Background id>`, its colour is the primary cloth colour and its second colour the secondary (`Banner.cs:32`). Layers 1 and up are sigils, so the mesh id is an `<Icon id>`, the colour is the sigil colour and the second colour is its stroke (`Banner.cs:34`). The canvas is 1528 by 1528, which is why dead centre is `764.764`, and the player-editable box is the central 512 by 512 (`Banner.cs:24, 26`).

Erebor's key is 90 numbers, so 9 layers:

| Layer | Mesh | Colours | Size | Position | Stroke, mirror, rotation | What it is |
|---|---|---|---|---|---|---|
| 0 | `11` | 100, 75 | 4345 x 4345 | 764, 764 | 1, 0, 0 | Native's base cloth `banner_background_test_11`, scaled well past the canvas, in a dark blue and a pale blue |
| 1 to 3 | `521` | 172, 100 | 62, 54, 42 | left of centre | 0, 0, 268 | Native's shape icon 521, three sizes running down the left |
| 4 to 6 | `521` | 172, 100 | 62, 54, 42 | right of centre | 0, 1, 88 | the same three, mirrored and rotated to match |
| 7 | `24019` | 31, 240 | 400 x 400 | 765, 873 | 1, 0, 0 | TAOM icon 24019, group 241, cell 3 of `taom_banners_dwarves_alpha_02` |
| 8 | `24510` | 31, 240 | 200 x 200 | 765, 556 | 1, 0, 0 | TAOM icon 24510, group 245, cell 10 of `taom_banners_dwarves_ornaments_01` |

Colour 240 in layers 7 and 8 is one of the 46 ids TAOM redefines over Native. Native declares 240 as `0xff5c6868`, a grey; TAOM declares it as `0xffd48806`, an amber, and TAOM's is the one that renders. Change TAOM's row and you have changed the outline of both dwarf sigils here and of every other key that names 240.

## Recipes: Add / Modify / Delete

### Add

**Add a sheet of emblems.**

1. Author the art as one sheet with sixteen cells and place the source under `Main/_Module/AssetSources/BannerIcons/` as `taom_banners_<faction>_alpha_NN.psd`. Slice one PNG per emblem into `Main/_Module/GUI/SpriteParts/ui_taom_bannericons/` named `<Icon id>.png`, one file per id.
2. Add one `<BannerIconGroup>` to [`Main/_Module/ModuleData/banner_icons.xml`](../../Main/_Module/ModuleData/banner_icons.xml), before `<BannerColors>`. Give it a free group id, `is_pattern="false"`, a `{=!}` name, and one `<Icon>` per cell with a free id block.
3. Compile the banner material in the Modding Kit so `taom_banners_<faction>_alpha_NN_mtl.tpac` and `_tex.tpac` land in `Main/_Module/Assets/BannerIcons/`. Without this the sigil renders blank no matter what the XML says.
4. Run the sprite generator against the game install, then pull the bake back with `pwsh tools/sync_sprite_bake.ps1 -WhatIf` first and without `-WhatIf` once the preview looks right. It copies only the manifest and the two `GauntletUI` directories, so it cannot revert source edits ([gui-sprite-system.md](../features/gui-sprite-system.md) line 169).
5. Fully exit and relaunch the game. A re-bake repacks the whole category, so sprites that moved render from the wrong part of the old texture in a running client.

Check: `python -c "import glob,collections,os,xml.etree.ElementTree as ET;fs=[f for f in glob.glob(os.environ['BANNERLORD_GAME_DIR']+'/Modules/*/ModuleData/banner_icons.xml') if '/TAOM/' not in f.replace(os.sep,'/')]+['Main/_Module/ModuleData/banner_icons.xml'];c=collections.Counter((e.tag,e.get('id')) for f in fs for e in ET.parse(f).getroot().iter() if e.tag in ('Icon','Background'));print([k for k,v in c.items() if v>1] or 'no Icon/Background id collisions')"`
Takes effect: full game restart
Code: No code changes needed

**Add a palette colour.**

1. Open the `<BannerColors>` block at the end of [`Main/_Module/ModuleData/banner_icons.xml`](../../Main/_Module/ModuleData/banner_icons.xml). It has to stay the last block in the document.
2. Add a `<Color>` with an id nothing else uses and `hex` in the eight-digit alpha-first form, for example `0xFFdc0000`. Add the row twice with different ids if you want the swatch in both editor columns.
3. Do not append below the current last row unless you mean to. The random-banner generator uses the last entry in the merged palette as the outline colour whenever it decides not to draw a coloured stroke (`Banner.cs:349, 377, 403, 424, 462, 485`), so the last row is load-bearing.

Check: `python -c "import xml.etree.ElementTree as ET; ET.parse('Main/_Module/ModuleData/banner_icons.xml'); print('ok')"`
Takes effect: full game restart
Code: No code changes needed

**Author a key for a new clan or kingdom.**

1. Design the banner with the in-game banner editor and copy the code it produces. That is the only key generator TAOM has ([kingdom-creation.md](../features/kingdom-creation.md) line 569).
2. Paste it into `banner_key` on the `<Faction>` in [`characters/clans.xml`](../../Main/_Module/ModuleData/characters/clans.xml) or the `<Kingdom>` in [`taom_spkingdoms.xml`](../../Main/_Module/ModuleData/taom_spkingdoms.xml). For a vanilla clan or kingdom you are overriding, write an `<xsl:attribute name="banner_key">` inside a matching template in `spclans.xslt` or `spkingdoms.xslt` instead.
3. Check the number count divides by ten and that every mesh and colour id it names exists. A ragged tail is discarded silently rather than reported (`Banner.cs:580`).
4. Set the kingdom's key to its ruling clan's key so the two agree.

Check: `python -c "import xml.etree.ElementTree as ET;bad=[(e.get('id'),len(k.split('.'))) for e in ET.parse('Main/_Module/ModuleData/characters/clans.xml').getroot().iter('Faction') for k in [e.get('banner_key') or ''] if len(k.split('.'))%10 or not all(p.lstrip('-').isdigit() for p in k.split('.'))];print(bad or 'all banner_key values parse')"`
Takes effect: full game restart, then the injection service applies it on new game or save load
Code: No code changes needed

### Modify

**Recolour a faction.** A kingdom carries four colour attributes and a clan carries two, and they are not the same thing.

1. `primary_banner_color` and `secondary_banner_color` on a `<Kingdom>` are written `0xffRRGGBB`. All 14 TAOM kingdoms use that form.
2. `color` and `color2` on a `<Kingdom>` or a `<Faction>` are written as bare eight-digit hex with no `0x`, for example `FFAD8B00`. All 145 clans use that form. Mixing the two forms up is the most common way to get a transparent or wrong colour.
3. `color` and `color2` on a clan are troop armour, not only a UI tint. `Patch23_BannerColorPersistence` prefixes `Mission.SpawnAgent` and rewrites `AgentBuildData.ClothingColor1/2` from the spawning party leader's clan for every party in the mission, after vanilla has already set them from the team colour ([`Mission_SpawnAgent_Patch.cs:54`](../../Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs)). The clan is the last writer, so battlefield armour follows the clan and not the kingdom.
4. Avoid `FFFFFFFF` for a clan's primary. It is `uint.MaxValue`, which the engine's own visuals data uses as its unset marker, so a pure-white primary reads as "no clan colour set" (`AgentVisuals_Create_Patch.cs:36`). Use `FFFEFEFE` instead. Exactly one shipped clan carries `FFFFFFFF`, and it is in `color2`, which the guard does not read.
5. Keep `color`/`color2` in step with the `banner_key`. Since 2026-09-02 each Gondor clan derives `color` from its layer 0 background colour and `color2` from its layer 1 icon colour ([clan-heraldry.md](../features/clan-heraldry.md) lines 78 to 82).

Check: `python -c "import xml.etree.ElementTree as ET;bad=[(e.get('id'),e.get('color'),e.get('color2')) for e in ET.parse('Main/_Module/ModuleData/characters/clans.xml').getroot().iter('Faction') if not (e.get('color') or '').isalnum() or len(e.get('color') or '')!=8];print(bad or 'all clan colours are bare 8-digit hex')"`
Takes effect: full game restart, then the injection service applies it on new game or save load
Code: No code changes needed

**Re-colour or re-roster a clan through the generator.** Edit the clan's entry in `Main/_Module/ModuleData/clan_heraldry/<culture>.json`, then run [`python tools/generate_clan_heraldry.py`](../../tools/generate_clan_heraldry.py) `--spec <culture> --apply`. Never run it on `gondor.json` or `mordor.json`: those specs have drifted from the shipped `spclans.xslt` on `template_id`, and `--all --apply` globs every spec file, so it would revert a deliberate binding fix ([clan-heraldry.md](../features/clan-heraldry.md) lines 135 to 140). Edit those two by hand.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart, then the injection service applies it on new game or save load
Code: No code changes needed

### Delete

1. Never reuse a retired icon, background or colour id. Ids are addresses inside every key already written, and a `banner_key` in an existing save still names the number you freed.
2. To retire an emblem, leave the `<Icon>` row in place and instead stop pointing keys at it. If it must disappear from the banner editor, add `is_reserved="true"` rather than deleting it: reserved icons stay usable from a key and stop being offered.
3. If you do delete a row, sweep every key that named it first. An unknown icon id is not an error at load: `BannerManager.GetIconDataFromIconId` returns `default(BannerIconData)` (`BannerManager.cs:100-109`), so the layer resolves to no material at all. An unknown colour id renders as `0xDEADBEEF` (`BannerManager.cs:81`).
4. Deleting the sprite PNG or the compiled material without deleting the `<Icon>` row leaves a definition that renders blank, which looks like a bake failure rather than a data deletion.

Check: `python -c "import glob,os,xml.etree.ElementTree as ET;R=[ET.parse(f).getroot() for f in [f for f in glob.glob(os.environ['BANNERLORD_GAME_DIR']+'/Modules/*/ModuleData/banner_icons.xml') if '/TAOM/' not in f.replace(os.sep,'/')]+['Main/_Module/ModuleData/banner_icons.xml']];P=lambda t:{e.get('id') for r in R for e in r.iter(t)};I,C,B=P('Icon'),P('Color'),P('Background');T=[('Main/_Module/ModuleData/characters/clans.xml','Faction','banner_key'),('Main/_Module/ModuleData/taom_spkingdoms.xml','Kingdom','banner_key'),('Main/_Module/ModuleData/taom_spcultures.xml','Culture','faction_banner_key')];bad={e.get('id') for p,t,a in T for e in ET.parse(p).getroot().iter(t) for n in [(e.get(a) or '').split('.')] for i in range(0,len(n)-9,10) if n[i] not in (B if i==0 else I) or n[i+1] not in C or n[i+2] not in C};print(sorted(bad) or 'every key resolves')"`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **A duplicate icon or background id is dropped only when it lands in a different group.** The XML merge runs first and pairs children on the schema's unique key, so two `<Icon id="10000">` rows inside group 100 become one row carrying the later module's attributes (`BannerIcons.xsd:44-47`, `MBObjectManager.cs:833-874`). Write the same id into a different group and the rows never pair: both reach `BannerIconGroup.Deserialize`, whose `previouslyAddedGroups` check keeps the first and discards the second with no error and no log line (`BannerIconGroup.cs:52, 69`). The emblem simply does not exist, and nothing in the file tells you which of the two outcomes you got.
- **Same-id groups merge, they never replace.** `MBObjectManager.MergeElements` matches groups on their unique `id` and folds the later one's children in (`MBObjectManager.cs:820-874`). The C# `Merge` behind it copies only the keys the first group is missing (`BannerManager.cs:236-248`, `BannerIconGroup.cs:77-100`), but it reads a list the XML merge has already collapsed, so it fires about as often as the `<Color>` guard below, which is to say almost never. You can add ids to Native's groups, as `NavalDLC` does with 14 icons into group 2, and you can override one of its rows by restating that row with the same id in the same group.
- **A colliding `<Color>` id resolves to the last module, not the first.** `BannerManager` guards with `if (!_colorPalette.ContainsKey(key))`, which reads like first wins, but the guard never fires: the XML merge already collapsed the two nodes into one, and `MergeElementAttributes` writes the later document's attributes over the earlier one's (`MBObjectManager.cs:799-817`). TAOM loads after Native (`SubModule.xml` line 25 pins `order="LoadBeforeThis"` on Native), so for the 46 ids both files define, TAOM's hex is the one that renders. Resolve a palette id against TAOM's file first and fall back to Native's only when the id is absent there ([clan-heraldry.md](../features/clan-heraldry.md) lines 83 to 90). The same mechanism governs an `<Icon>` or `<Background>` id inside a group both modules declare; only an id that survives the merge unpaired, which for those two means one that landed in a different group, ever reaches a first-wins guard.
- **Attributes the XSD calls optional will still crash the load.** `name`, `is_pattern`, `material_name`, `texture_index`, `mesh_name` and `hex` are all `use="optional"` in `BannerIcons.xsd`, and all six are dereferenced with no null guard, so omitting one throws a `NullReferenceException` during startup (`BannerIconGroup.cs:42-64`, `BannerManager.cs:258-261`). Only four attributes are genuinely optional: `is_reserved`, `is_base_background` and the two `player_can_choose_*` flags. Trust the C#, not the schema.
- **Nine shipped entries name ids that do not exist anywhere.** A sweep of `characters/clans.xml`, `taom_spkingdoms.xml` and `taom_spcultures.xml` against the merged pool finds 5 undefined icon ids (`17104`, `17281`, `17299`, `17358`, `17371`) and 2 undefined colour ids (`124`, `128`) across `clan_khuzait_16`, `clan_lothlorien_2`, `clan_mirkwood_5`, `clan_mirkwood_6`, `clan_rivendell_2` and the `rivendell`, `mirkwood`, `lothlorien` and `lindon` cultures. The icons show nothing and the colours draw as `0xDEADBEEF`. No repo validator covers this: `tools/validate_moduledata.py` contains no banner check at all, which is why the Delete recipe above carries its own sweep.
- **The "under 100 characters is a placeholder" rule flags real banners.** [kingdom-creation.md](../features/kingdom-creation.md) lines 526 to 532 and 569 say a short key is always a placeholder. 79 of TAOM's 145 clan keys are under 100 characters, and every one of them is a well-formed two-layer banner naming real ids, for example `clan_empire_west_10` at 70 characters. Vanilla's own Empire key is 62 characters. Layer count on its own proves nothing; check that the ids resolve, which is the real test the placeholder story was reaching for.
- **`is_pattern` partitions the whole file.** Pattern groups feed the cloth pool, non-pattern groups feed the sigil pool, and the counters that drive the random-banner roll are built from that split (`BannerManager.cs:270-282`). A `<Background>` in a sigil group loads and is unreachable, and the reverse is also true. TAOM defines no `<Background>` at all, so all 36 cloth patterns come from Native's group 1.
- **XML alone never makes a sigil visible.** Two pipelines share the icon id: the banner material, which the flag renders from, and the GauntletUI sprite atlas, which the editor and the UI render from. An id wired into `banner_icons.xml` with no compiled asset shows blank, and static review cannot prove otherwise ([banner-icon-generation.md](../reference/banner-icon-generation.md) line 5). All 33 of TAOM's material names now have a compiled `_mtl.tpac`, so that document's "remaining: material compile x9" status line is stale.
- **The sprite side has one orphan and a pile of dead sheets.** `GUI/SpriteParts/ui_taom_bannericons/` holds 369 PNGs for 368 icon ids: `22004.png` has no `<Icon id="22004">`, so nothing can ever reference it. Separately, `AssetSources/GauntletUI/` holds 41 `ui_taom_bannericons_*.png` sheets while `TAOMSpriteData.xml` declares the category as 2 sheets, so 39 of the files on disk are left over from earlier bakes. The manifest is the authority, not the file list.
- **Restart the game after a re-bake.** Repacking moves existing sprites to new atlas rectangles. A running client holds the old texture and reads the new manifest, so anything that moved renders from garbage ([gui-sprite-system.md](../features/gui-sprite-system.md) line 138).
- **Spelling traps in the older docs.** The directory is `Main/_Module/GUI/Prefabs`, not `PreFabs`, and the kingdoms file is `taom_spkingdoms.xml` all lowercase. Several feature docs use the other spelling; the disk does not.

### What TAOM has never determined

- **What `is_base_background` actually does.** It calls `BannerManager.SetBaseBackgroundId`, and a grep of the whole v1.4.8 managed decompile for `BaseBackgroundId` returns 4 hits: the property, its setter, the assignment inside the setter, and the one caller in `BannerIconGroup`. Nothing reads it back. Its consumer is native or UI side and cannot be confirmed from the decompile. Leave it on Native's `<Background id="11">`.
- **Whether `texture_index` may exceed 15.** Nothing in managed code clamps or validates it; the native side resolves `material_name` plus the index as an asset name (`Module.cs:684-686`). Vanilla and TAOM both stop at 15 because their sheets are four by four. Whether a larger sheet works is untested here.
- **Whether a given emblem renders.** Only the running game answers that. Flag any new sigil as in-game-only in the `Not-tested:` line of the commit.

## Numbers in this chapter

All measured 2026-09-05, from the repo at `bannerlord-1.4.5` and the installed v1.4.8 modules.

- **681** lines, **33** groups, **368** `<Icon>` rows, **0** `<Background>` rows and **150** `<Color>` rows in `banner_icons.xml`: `wc -l` plus `rg -c '<BannerIconGroup |<Icon |<Background |<Color '` on the file. <!-- measured 2026-09-05 -->
- **14** of the 33 groups hold a full sixteen emblems; the other **19** hold between 1 and 15. Every group names exactly one `material_name`: `python -c "import xml.etree.ElementTree as ET;print([(g.get('id'),len(g.findall('Icon'))) for g in ET.parse('Main/_Module/ModuleData/banner_icons.xml').getroot().iter('BannerIconGroup')])"` <!-- measured 2026-09-05 -->
- **1895** lines and **6** groups in Native's file, **68** lines and **1** group in NavalDLC's, holding **237** and **14** icons: `wc -l` and an ElementTree count over each module's `ModuleData/banner_icons.xml`. <!-- measured 2026-09-05 -->
- **36** cloth patterns exist in total, all in Native's group 1, which is the only group with `is_pattern="true"` anywhere. <!-- measured: python over Native and TAOM banner_icons.xml 2026-09-05 -->
- **229** Native colours, **150** TAOM colours, **46** ids defined by both, **333** distinct ids in the merged palette: a set intersection and union of the `<Color id>` values in the two files. <!-- measured 2026-09-05 -->
- **0** `<Icon>` or `<Background>` id collisions across Native, NavalDLC and the repo file, using the Check command in the Add recipe. <!-- measured 2026-09-05 -->
- **90** numbers, so **9** layers, in Erebor's `banner_key`, and **304** characters: `python -c "import xml.etree.ElementTree as ET;k=ET.parse('Main/_Module/ModuleData/taom_spkingdoms.xml').getroot()[0].get('banner_key');print(len(k),len(k.split('.')))"` <!-- measured 2026-09-05 -->
- **145** `<Faction>` entries in `characters/clans.xml`, all **145** with both a `banner_key` and a bare eight-digit `color`/`color2`; **79** of the keys are under 100 characters and all 79 are exactly 2 layers; the longest is **4615** characters. **1** clan carries `FFFFFFFF`, in `color2`. <!-- measured: python over Main/_Module/ModuleData/characters/clans.xml 2026-09-05 -->
- **14** kingdoms, all with `primary_banner_color` in the `0x` form and `color`/`color2` in the bare form; **24** cultures, all with a `faction_banner_key`, **19** of them 2 layers. <!-- measured: python over taom_spkingdoms.xml and taom_spcultures.xml 2026-09-05 -->
- **62** characters and **2** layers in vanilla's Empire `faction_banner_key`, at `SandBoxCore/ModuleData/spcultures.xml:26`. <!-- measured 2026-09-05 -->
- **9** shipped entries name **5** undefined icon ids and **2** undefined colour ids, from the pool sweep in the Delete recipe. <!-- measured 2026-09-05 -->
- **369** PNGs in `GUI/SpriteParts/ui_taom_bannericons/` against **368** icon ids, leaving `22004` orphaned; **369** matching `<SpritePart>` rows in `TAOMSpriteData.xml` on **2** declared sheets, against **41** `ui_taom_bannericons_*.png` files on disk. <!-- measured: python over the sprite folder, TAOMSpriteData.xml and banner_icons.xml 2026-09-05 -->
- **33** distinct `material_name` values, **0** of them without a compiled `_mtl.tpac` in `Main/_Module/Assets/BannerIcons/`: `python -c "import os,xml.etree.ElementTree as ET;m={i.get('material_name') for i in ET.parse('Main/_Module/ModuleData/banner_icons.xml').getroot().iter('Icon')};h=set(os.listdir('Main/_Module/Assets/BannerIcons'));print(len(m),[x for x in m if x+'_mtl.tpac' not in h])"` <!-- measured 2026-09-05 -->
- **4** hits for `BaseBackgroundId` in the whole v1.4.8 managed decompile, none of them a read: `rg -r 'BaseBackgroundId' -g '*.cs'` run in the decompile root. <!-- measured 2026-09-05 -->
- **0** hits for `banner` in `tools/validate_moduledata.py` outside the game-install path strings: `rg -n -i 'banner' tools/validate_moduledata.py` <!-- measured 2026-09-05 -->
- **89** lines in the game's `XmlSchemas/BannerIcons.xsd`, one of **51** entries in that folder. <!-- measured 2026-09-05 -->

## Read next

- [`docs/reference/banner-icon-generation.md`](../reference/banner-icon-generation.md) for the full sheet-to-game pipeline, the id-block conventions and the current sheet inventory.
- [`docs/features/clan-heraldry.md`](../features/clan-heraldry.md) for how clan colours are authored, the spec-JSON generator and the Gondor and Mordor prohibition.
- [`docs/features/banner-color-persistence.md`](../features/banner-color-persistence.md) for the patch set that makes armour tint follow the clan.
- [`docs/features/banner-injection.md`](../features/banner-injection.md) for when a key edit reaches an existing save and how player-customised banners are excluded.
- [`docs/features/gui-sprite-system.md`](../features/gui-sprite-system.md) for the sprite bake, the verify-both-failure-modes rule and the safe install-to-repo sync.
- [`docs/features/banner-bearers.md`](../features/banner-bearers.md) for the banner items troops carry, which pick a pole and a bonus tier rather than a faction.
- [`docs/reference/harmony-patch-registry.md`](../reference/harmony-patch-registry.md) for `Patch23_BannerColorPersistence` and the other banner patch categories.
- [`.claude/rules/gui-ui.md`](../../.claude/rules/gui-ui.md) for the sprite reference and bake rules.
- [Clans](clans.md), [Kingdoms](kingdoms.md) and [Cultures](cultures.md) for the entries that carry the keys.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/clans.md](./clans.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
