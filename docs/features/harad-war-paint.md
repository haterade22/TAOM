# Harad war paint

## Overview

Harad characters wear face paint. The set has 8 designs for men and 7 for women, made by yotthani. The face editor's tattoo slider also offers them to every human player. Umbar stays clean-faced.

## Why This Exists

- **Vanilla behavior:** the 31 male and 34 female tattoos are generic, and TAOM's Harad preset selected `Cleanface`.
- **TAOM requirement:** the Haradrim need a look of their own.
- **Without this feature:** every Haradrim has a bare face.

## Architecture

### Design Challenge

A face key stores a tattoo as a number, which is a position in the merged tattoo list. Three engine rules shape the data. All three are from `FUN_18057d0d0` in `TaleWorlds.Native.dll` v1.5.6 (reach it with `python tools/native_decompile.py --rva 0x57D0D0`).

- **Selection:** each tag in the character's list collects every entry with that exact style tag. The engine then picks evenly among the entries it collected.
- **No match:** if nothing matches, the engine picks a random index between the preset's minimum and maximum keys.
- **Range:** in a roll, an index past the end of the list for that age becomes 0.

### Solution Approach

Data only. There is no C#.

- `skins.xml` appends the designs to the `human` lists. TAOM loads after Native, so vanilla numbers do not move. Men go from 31 to 39 entries and women from 34 to 41. The engine limit is 64. NavalDLC adds 10 to each list, but it is in TAOM's `IncompatibleModules`.
- Men get the 8 designs baked to the male head UV and women the 7 baked to the female head UV, so each gender sees only its own set. Every design carries the style tag `harad`, so the one tag `harad` selects all of them for either gender. The vanilla tag `aserai` is not usable here: all 31 vanilla male tattoos carry it too.
- The 8 child skins get unnamed entries, as NavalDLC's do. These keep the numbers aligned across ages, and give a child's `harad` tag a blank match. Without them the child falls back to a random vanilla tattoo.
- `fighter_haradrim` selects `harad`. Umbar has its own preset, `fighter_umbar`, with the same face range and `Cleanface`.
- 53 `Culture.aserai` lords have their own fixed key in `characters/lords.xml` or `lords.xslt`, and 28 of the ids are in both files. An original character whose minimum and maximum keys are equal does not roll, so a tag alone paints nothing on these lords. Each key now holds a design in KeyPart1 bits 24 to 29 and a colour in bits 18 to 23 (`FUN_18057b4e0`). In sorted id order, men take position 31 plus `i % 8`, women take 34 plus `i % 7`, and colours cycle through palette indexes 12 to 23. Their `harad` tag serves children and the face variety pass, which roll from tags. `named_companion_igor` shares Aragorn's key on purpose, so he keeps it and the face variety pass rolls him from his `harad` tag. Two pairs that share one face (`lord_3_13_2` with `lord_3_14_1`, and `lord_3_22_4` with `lord_A9_s`) hold one design and colour, because the face variety pass groups on the whole key. For the 28 ids in both files, `characters/lords.xml` wins the merge. A lord under 19 shows no paint until the adult skin applies.

**Delivery:** the Modding Kit builds the textures and materials into TAOM's loose `Assets/` tree.

- TAOM's `AssetPackages/` is not read while `Assets/` exists, so yotthani's `tattoo_harad.tpac` cannot go there.
- `TAOM.Dependencies` has no `Assets/`, but it loads before Native. A skins file there would take the first tattoo numbers and shift every vanilla tattoo.
- Do not also install yotthani's `HaradTattoos` module with TAOM. The two would declare the same asset names and append the designs twice.

## Configuration

### Config File: `Main/_Module/ModuleData/skins.xml`

Registered in `Main/_Module/ModuleData/project.mbproj` as `soln_skins`, type `skin`.

| Field | Type | Description |
|-------|------|-------------|
| `tattoo_material/@name` | string | The material, exactly as the Kit named it: `tattoo_harad_a`, with no `_mat` suffix. A name with no material draws nothing and logs nothing. Absent on a child skin |
| `style_tag/@name` | string | The design's own tag, `aserai`, and `harad` |
| `tattoo_materials/@zero_probability` | int | Must equal Native's value for that skin: 0 for `man`, `woman` and `kid_2_*`; 85 for `kid_1_*`, `kid_3_*` and `toddler_*` |

`tattoo_color_gradient_points` appends 12 Harad colours (crimson, oxblood, rust, terracotta, saffron, gold, bone, indigo, teal, turquoise, violet, charcoal) to Native's 12, on all 10 skins so a character keeps its colour as it ages. The R channel of a design takes the palette colour. The native skin holds at most 32 points (`FUN_180578810`), so at most 8 more fit. A character stores its colour as a value from 0 to 63, a position along the list, so a longer list moves the colour of existing tattooed characters (see Known limits). Append only. A new colour string must differ from every Native string on that skin: the merge keys on `@point` (`soln_skins.xsd`), so a match merges into the Native row and every later position moves.

`fighter_haradrim` holds colour values 34 and 63 in its two keys. The roll is continuous between them (`FUN_18057d0d0`), so a rolled Harad lands on palette position 12.4 to 22.8. The renderer clamps the value at 0.99 and blends two adjacent colours (`FUN_18056e9c0`). At variation 0 that is the Harad colours only; a roll at variation 0.2, such as a child or the face variety pass, can reach Native's last colours.

### Current Values

| Tag | Men | Women |
|---|---|---|
| Design tags | `HaradA`, `HaradA2`, `HaradB`, `HaradHunter`, `HaradRitual`, `HaradElite`, `HaradAsym`, `HaradScarred` | `HaradCrown`, `HaradFilm`, `HaradPriestess`, `HaradElegant`, `HaradWarrior`, `HaradDots`, `HaradRitualElite` |

`fighter_haradrim` covers the `aserai`, `shaghana`, `abanissa` and `harad_raiders` cultures. Characters include villagers, notables, wanderers, troops, lords and the Shaghana and Abanissa child-education templates.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/skins.xml` | The tattoo entries and child padding |
| `Main/_Module/ModuleData/TAOM_bodyproperties.xml` | `fighter_haradrim` and `fighter_umbar` |
| `Main/_Module/ModuleData/characters/lords.xml` | Fixed-key Harad lords with a design in the key and the `harad` tag; Umbar lords on `fighter_umbar` |
| `Main/_Module/ModuleData/lords.xslt` | 53 Harad lords with a design in the key and the `harad` tag |
| `Main/_Module/ModuleData/named_companions/named_companions.xml` | `named_companion_igor` with the `harad` tag |
| `Main/_Module/ModuleData/troops/troops_umbar.xml`, `fords_of_war/fords_troops.xml` | Umbar troops on `fighter_umbar` |
| `Main/_Module/ModuleData/project.mbproj` | Registers `skins.xml` as `soln_skins` |
| `Main/_Module/AssetSources/tattoos/` | The 15 source PNGs: 1024 RGBA, R palette area, G white area, B ink, A coverage |
| `Main/_Module/Assets/tattoos/` | The Kit's texture and material packages |

## Dependencies

The tag list in a preset needs the entries in `skins.xml`. The 170 preset lords in `characters/lords.xml` depend on `LordFaceVarietyService` rule A: without it, a hero copies the preset's minimum key, which holds design 0. Troops and template heroes roll from the preset directly.

## Tests

- `tools/tests/test_harad_war_paint.py`: the design order, tags, child padding, palette and registration; that each package declares its name, binds its own texture and imported as albedo; the two presets and their colour range; that the painted preset holds exactly the four Harad cultures, so no Umbar character uses it; that no Harad-culture character has `Cleanface`; that preset lords leave the tag to the preset; that every fixed-key Harad lord holds a design for its gender and a Harad colour; that lords with one face share one key; that Igor stays in Aragorn's face group with the tag; and that no other culture's lord holds a Harad design.

## Known limits

**Face editor thumbnails:** the grid draws each tile from the Native sprite `FaceGen\Tattoo\<Gender>\img<N>` (`FaceGenVM`). Native ships 41 male and 44 female sprites, which covers the vanilla lists plus NavalDLC's 10. So tiles 31 to 38 (men) and 34 to 40 (women) show unrelated NavalDLC thumbnails, while the paint on the head is correct.

**Other cultures' tattoo colours:** a stored value maps to `min(value / 63, 0.99) * (count - 1)`, and no appended palette can keep that position. On 2026-11-08 this moves the colour of `fighter_rhun` (value 51, Native index 9 before, about 18.6 now) and of 270 fixed-key rows with a tattoo in non-Harad cultures in `lords.xml` and `lords.xslt`. Restoring them means rewriting those keys or dropping the extra colours, which is the maintainer's decision.

**Heroes outside the set:** the vanilla Aserai lord templates, for example `spc_ghilman_leader_*`, stay on vanilla presets, so heroes generated from them do not select `harad`. The vanilla Aserai child-education templates are also outside the set.

**Player start:** a Harad player starts with tattoo 0 (`charactercreation/cc_body_properties.xml`) and picks paint in the face editor.

## How to verify in game

**Preconditions:** a new campaign for lords. Saved lords keep their stored face. Run `/armory-audit` first if the ARMORY ART DRIFT banner shows. These results are visible only to a person.

1. In the character creator, select the last tattoo tile. The grid holds 39 tiles for men and 41 for women, and the last designs show paint on the head. The tile pictures are wrong (see Known limits). The tattoo colour selector offers 24 colours; 12 means the engine replaced Native's list.
2. Examine the paint on the head. It follows the face, and is not black or upside down.
3. In a custom battle against Harad, men and women wear paint. An Umbar troop is clean.
4. A Rhun character still shows its Eastern design, which proves vanilla numbers did not move. Record its tattoo colour: the colour can differ from before (see Known limits).
5. In a new campaign, open the encyclopedia page of a `Culture.aserai` lord such as Tariq (`lord_3_3_1`) and of a Shaghana or Abanissa lord. Both wear paint.
6. In `rgl_log_*.txt`, find `opening ..\..\Modules\TAOM/ModuleData/skins.xml`. A missing material logs nothing, so step 2 is the proof that the materials load.

## How to add a design

1. Add the source PNG to `Main/_Module/AssetSources/tattoos/`, and import it in the Kit with the settings of `tattoo_male_a`. Read the Type after the import: the Kit set `tattoo_harad_h` to a height map (BC4, one channel), and `test_harad_war_paint.py` now rejects that.
2. Create its material from `tattoo_male_a_mat`, then copy both packages into `Main/_Module/Assets/tattoos/` before any build.
3. Append the entry at the end of `man` or `woman` in `skins.xml`, with the `harad` tag.
4. Append one unnamed entry to each child skin of that gender.
5. Update the lists in `tools/tests/test_harad_war_paint.py`.

Never insert or remove an entry in the middle: stored faces would show a different design. To get some clean faces, add `Cleanface` beside `harad`; with both, about 1 man in 9 and 1 woman in 8 is clean.

## Changelog

- 2026-11-08: designs added from yotthani's HaradTattoos package, imported through the Kit. Male and female paint seen on the head in the face editor (Mike's screenshots of the last tile, one design each). Palette extended from 12 to 24 colours. 53 fixed-key lords painted through their keys after the review; `named_companion_igor` paints through his tag. Still owed in game: the palette count in the face editor, Harad and Umbar troops in a battle, the Rhun check and a painted fixed-key lord.

## GitHub Issue

- **Issue:** #1100: [Harad war paint](https://github.com/haterade22/TAOM/issues/1100)
- **Status:** Open
