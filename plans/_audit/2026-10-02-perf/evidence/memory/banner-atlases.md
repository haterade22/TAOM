# TAOM's banner icon atlases are uncompressed and 16 times vanilla's size, 2026-10-03

Written for: Mike (a Kit change) and the next reader of REPORT M9. Read from the testing channel's
`TAOM/AssetPackages/pack0.tpac` with `tools/audit_map_scene_memory.py`'s reader (scripts beside this file:
`tpac_big_textures.py`, `find_textures.py`, `texture_stub_census.py`); vanilla read from the installed
v1.5.3 `Native/AssetPackages`.

## What ships

`Main/_Module/ModuleData/banner_icons.xml` maps 368 banner icons onto 33 atlas materials
(`taom_banners_<culture>_alpha_NN`, 29 of them, and four `..._ornaments_NN`), 16 cells per atlas
(`texture_index` 0 to 15). Every one of the 33 is referenced; none is an orphan. Each atlas texture is
**4096 x 4096, one mip, R8G8B8A8_UNORM (uncompressed): 64 MiB decompressed**, 1.7 to 2.4 MB stored in the
pack (it compresses well because it is mostly empty mask). The 33 come to 2,112 MiB decompressed.

Vanilla's own banner atlases (`custom_banner_icons_01` to `_16` in `Native/AssetPackages/core.tpac`) are
**2048 x 2048, one mip, BC7: 4 MiB each**. So a TAOM atlas costs 16 times a vanilla one: four times the
pixels at four times the bytes per pixel.

| Format | Per atlas | All 33 |
|---|---|---|
| TAOM today (4096, RGBA8) | 64 MiB | 2,112 MiB |
| 4096, BC7 | 16 MiB | 528 MiB |
| Vanilla's (2048, BC7) | 4 MiB | 132 MiB |

## What this costs, and what is not known

An atlas is loaded when a banner that uses one of its icons is drawn (party and settlement banners on the
campaign map, battle banners, UI banner images). How many of the 33 a campaign keeps resident, and whether
the engine keeps a system-memory copy beside the GPU one (which decides whether they show in private
bytes), is not knowable offline. Hypothesis, unverified: they are a large share of the about 2.15 GB of
textures the campaign map's first view loads (REPORT M3), since that view draws every visible party's and
settlement's banner.

The decisive measurement: one campaign load with the atlases re-exported as BC7 against today's, compared
on `[MemStation]` private bytes after the map's first view and on VRAM use.

## Which atlases a campaign's banners name (computed 2026-10-03)

`banner_atlas_use.py` (beside this file; output `banner-atlas-use.txt`) decodes every banner key in TAOM's
ModuleData, both `banner_key="..."` attributes and the XSLT overrides' `<xsl:attribute name="banner_key">`
(265 keys: `characters/clans.xml` 147, `spclans.xslt` 72, `taom_spcultures.xml` 24, `taom_spkingdoms.xml`
14, `spkingdoms.xslt` 8; the live `TAOM_Map` ModuleData holds none), and maps each icon layer to its atlas
through `banner_icons.xml`:

- **19 of the 33 atlases** are named by the campaign's own clan, kingdom and culture banners: **1,216 MiB at
  today's format** if a session draws all of them, 304 MiB as 4096 BC7, 76 MiB at vanilla's 2048 BC7.
  `taom_banners_gundabad_alpha_01` alone carries 70 of `clans.xml`'s icon layers.
- The other 14 atlases are named by no banner key (the Anduin Vale, Arnor, goblin, misc and Misty Mountain
  orc sheets, five culture `_02` sheets and two ornament sheets). They load only for a banner built at
  runtime, the banner editor or a randomly generated clan banner, so they cost nothing in most sessions.

Still unknown offline: how many of the 19 the first map view draws, and whether the engine keeps a
system-memory copy beside the GPU one. The re-export removes the question either way.

## Banners that name icons no module defines

`banner_undefined_icons.py` (output `banner-undefined-icons.txt`) checks every icon layer against the ids
that Native, NavalDLC and TAOM define (655). Five clans' banners name ids that exist nowhere (17104,
17281, 17299, 17358, 17371). The engine returns an empty record for an unknown id
(`BannerManager.GetIconDataFromIconId`, v1.5.3) and `BannerVisual` draws a layer only when its material
resolves (`BannerVisual.cs:120-122`), so those layers vanish without an error:

| Clan (`characters/clans.xml`) | Undefined layers |
|---|---|
| `clan_khuzait_16` (Zorian), line 410 | 1 of 1: the banner has no emblem at all |
| `clan_mirkwood_5`, line 688 | 5 of 7 |
| `clan_mirkwood_6`, line 703 | 5 of 7 |
| `clan_rivendell_2`, line 593 | 2 of 38 |
| `clan_lothlorien_2`, line 736 | 2 of 38 |

No TAOM banner uses the 14 icons that only NavalDLC defines, so a player without that DLC loses nothing.

## Not the same as M6

M6's 434.7 MB is the UI sprite sheets (`ui_loading_1`, `_2`, `ui_taom_bannericons_1`, `_2`, five `ui_taom_*`,
the career and font sheets), pinned by their AlwaysLoad categories. The banner atlases are a separate set,
loaded on demand.

## Also from the same reading

Every TAOM texture ships as a full mip chain with no low-resolution stub segment: the Armory's 2,595
textures (2.05 GiB of chains), the map's 1,322 (4.61 GiB), TAOM's 86. Vanilla Native carries the stub for
6,014 of its 6,280 textures, 3,234 of them with the full chain only in `EmAssetPackages` (REPORT M2,
FOR-MIKE item 6).
