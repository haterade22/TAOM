# Native Loading Screen (first-screen background and the One Ring)

## Overview

TAOM replaces the two things Bannerlord's native loading view draws. The first loading screen at launch
shows TAOM's own background, "Tales from the Age of Men" in molten gold under a moonlit arch of smoke
and Elvish script. In the bottom-right corner of every loading screen, the galloping horse becomes the
One Ring's inscription turning clockwise in glowing gold. Both are asset overrides with no code: TAOM
textures carrying vanilla's texture names.

## Why This Exists

- **Vanilla behavior:** the native view paints the Bannerlord background on the first screen and a dark
  horse and banner rider, looping, in the corner of every loading screen.
- **TAOM requirement:** a Middle-earth front door that matches Kysaro's themed loading window, splash
  and main menu ([faction-ui.md](faction-ui.md), #704).
- **Without this feature:** the first thing a player sees is vanilla's Calradia art, and the horse runs
  over Kysaro's paintings.

## Architecture

### Design Challenge

Neither piece is Gauntlet, so no prefab, PrefabExtension or Harmony patch reaches them. Vanilla's
`LoadingWindow.xml` has a `LoadingAnimWidget`, but it is a development-mode placeholder
(`Sprite="butter"`, `IsVisible="@IsDevelopmentMode"`), and Kysaro's `TAOMLoadingWindow.xml` draws its
paintings below the native layer. The first loading screen is also up before any TAOM code runs.

What the engine draws, read from the installed v1.5.3 on 2026-10-03:

| Piece | Where | What |
|---|---|---|
| `rglLoading_screen_view` | `TaleWorlds.Native.dll`, constructor at RVA 0x2B0B20 (`python tools/native_decompile.py --string loading_bar`) | builds the materials `show_texture_with_smooth_transition` (the background) and `loading_bar` (the corner animation) by name; the DLL's strings beside it name the background textures `loadingscreen_default` and `loadingscreen_naval` |
| texture `loadingscreen_default` | `Native/AssetPackages/core.tpac` | 1920 x 1080 DXT5; the first-screen background |
| texture `loadingscreen_naval` | `Native/AssetPackages/core.tpac` | 1920 x 1080 DXT5; the background with War Sails (`NavalDLC`) enabled, per the community guide below |
| material `loading_bar` | `Native/EmAssetPackages/core/core_materials/core_materials.tpac` | shader flag `use_animated_texture_coords`; `MeshVectorArgument <8, 8, 30, 64>`: 8 columns, 8 rows, 30 frames a second, 64 frames; blend `Modulate` |
| texture `loading_sprite_bannerlord` | `Native/AssetPackages/core.tpac` | 2048 x 2048 BC7 with alpha, 12 mips, `dont_degrade`; the horse sheet |

Decoded, the vanilla horse sheet is 8 x 8 cells of 256 px played row by row from the top left (each
cell's closest match is its row neighbour, and cell 31 wraps to cell 0). Its bottom half is a byte copy
of the top half: 32 gallop frames, twice. `Modulate` is its own value in `Material.MBAlphaBlendMode`,
separate from `Multiply`, so it is the ordinary alpha blend and gold renders as gold.

`warrider_logo`, the other "loading game logo" mesh the native code loads, is the 1024 x 256 game
wordmark, not the horse.

The community guide ([Custom First Loading Screen](https://docs.bannerlordmodding.lt/guides/custom_first_loading_screen/))
names the base background `loadingscreen_texture_1`, the name LOTRAOM shipped. No v1.5.3 package has
that texture; on this engine the name is `loadingscreen_default`.

### Solution Approach

TAOM's module ships textures named `loadingscreen_default`, `loadingscreen_naval` and
`loading_sprite_bannerlord`. A module texture with vanilla's name replaces Native's, even one bound
through a material, which the in-game check confirmed on 2026-10-03.

- **Background:** one 1920 x 1080 picture under both background names: an ImagineArt background (a
  moonlit smoke arch with Elvish script, no text) with `E:\LOTRAOMAssets\Renders\LOTRAOM_text_only.png`
  composited in code at 60 percent of the width, exactly centred. The title is never drawn by the
  model, which garbles lettering.
- **Ring:** the horse sheet's 8 x 8 layout at 1K (128 px cells, the inscription 100 px across). The
  material counts the grid in cells, not pixels, so half vanilla's resolution plays the same for a
  quarter of the memory (Mike, 2026-10-03). Each of the 64 frames turns the inscription a further 360/64
  degrees clockwise, so one turn takes 64 / 30 = 2.13 s and loops without a seam.

```
tools/loading_ring_art/imagineart_inscription.png     tools/loading_screen_art/imagineart_moonlit_smoke_arch.png
        |                                                      |   + LOTRAOM_text_only.png, centred at 60% width
  tools/build_loading_ring_sheet.py                           |
        |                                                      |
  Main/_Module/AssetSources/LoadingScreen/
      loading_sprite_bannerlord.png (1024 x 1024 RGBA)   loadingscreen_default.png, loadingscreen_naval.png (1920 x 1080)
        |                                                      |
        +------------- Modding Kit import into TAOM, saved ----+
                                     |
              Main/_Module/Assets/loadingscreen/*_tex.tpac  (584 / 568 / 564-byte descriptors)
                                     |
              pixels in the install's Modules/TAOM/RuntimeDataCache/*.rdc (Kit-written, not in git)
```

**What a player still sees of vanilla:** about a second of the horse and the vanilla background at
launch. The engine loads Native first and draws at once; TAOM's packages arrive a moment later and take
over. Removing that second would mean editing Native's own files, which an engine update or a file
check reverts, so it stays.

**Speed:** the ring turns at the material's 30 fps. Slowing it means a TAOM copy of the `loading_bar`
material with the same name, shader, blend, flag and vector argument but a lower third value (15 for
4.3 s a turn). `Material.SetMeshVectorArgument` exists in the v1.5.3 dump as a C# alternative, but
whether the native view reads the argument live is unverified, and it would miss the first screen.

## Key Files

| File | Purpose |
|---|---|
| `tools/build_loading_ring_sheet.py` | Ring master image to the 1K sheet |
| `tools/tests/test_build_loading_ring_sheet.py` | 13 tests: layout, row order, margins, centring, clockwise turn, seamless 64-frame loop, keying, colour under transparent texels, CLI |
| `tools/loading_ring_art/` | The ring's ImagineArt master and its `provenance.json` |
| `tools/loading_screen_art/` | The background's ImagineArt original and its `provenance.json` (asset id, prompt, reference, how the title was composited, hashes) |
| `Main/_Module/AssetSources/LoadingScreen/` | The three source PNGs the Kit imports |
| `Main/_Module/Assets/loadingscreen/` | The three compiled texture descriptors, copied back from the install after the Kit import |

## How to change the art

**Background:**
1. Make a 1920 x 1080 picture with the title already on it. Keep the bottom-right corner quiet for the
   ring, and keep gold out of the background so it does not compete with the title.
2. Save it as both `loadingscreen_default.png` and `loadingscreen_naval.png` in the install's
   `Modules/TAOM/AssetSources/LoadingScreen/` and in the repo's `Main/_Module/AssetSources/LoadingScreen/`.

**Ring:**
1. Make a master: script in one circle on pure black, square, 1K or more.
2. `python tools/build_loading_ring_sheet.py MASTER.png Main/_Module/AssetSources/LoadingScreen/loading_sprite_bannerlord.png --preview preview.gif --force`,
   then watch the GIF (30 fps, like the game). `--counter-clockwise` reverses the turn, `--preview-bg`
   composites the preview over a painting. Copy the PNG to the install's `AssetSources/LoadingScreen/`.

**Then, for either:**
1. In the Modding Kit, (re)import the texture under its exact name into TAOM: tick `Dont Degrade` and
   `Dont Delay Loading`; leave mipmaps on; keep alpha for the ring. Save the module: a package without
   its `RuntimeDataCache` entry is skipped silently.
2. Copy `Modules/TAOM/Assets/loadingscreen/*_tex.tpac` back to `Main/_Module/Assets/loadingscreen/`,
   then restart the game.

## Changelog

- 2026-10-03: first-screen background (ImagineArt moonlit smoke arch plus the TAOM title) under
  `loadingscreen_default` and `loadingscreen_naval`; confirmed in game.
- 2026-10-03: the One Ring sheet replaces the horse: sheet builder, ImagineArt master, 1K sheet,
  Kit-imported; confirmed in game, the same-name override works.

## GitHub Issue

- **Issue:** not yet filed
- **Status:** Open; `triage-needs-ingame` for the War Sails background once filed (not seen in game yet)
