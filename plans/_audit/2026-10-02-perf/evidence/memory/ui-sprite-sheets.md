# TAOM's UI sprite sheets, 2026-10-02

Written for: Mike, deciding two content changes in the Modding Kit, and the next engineer on memory.

## What loads, from the engine code (v1.5.3 decompile)

- `SpriteData.Load` loads every category marked `<AlwaysLoad />` as soon as the sprite data loads
  (`TaleWorlds.TwoDimension.SpriteData.cs:205-208`).
- `SpriteCategory.Load` requests sheets `1` to `SpriteSheetCount` and nothing else
  (`TaleWorlds.TwoDimension.SpriteCategory.cs:56-58`).
- Each requested sheet is pinned and preloaded: `Texture.GetFromResource(name)`, then
  `SetTextureAsAlwaysValid()` and `PreloadTexture(true)`
  (`TaleWorlds.Engine.GauntletUI.TwoDimensionEngineResourceContext.cs:8-19`).

So a declared sheet of an `AlwaysLoad` category stays resident for the whole session, and a sheet
beyond `SpriteSheetCount` is never requested.

## What TAOM declares and ships

`Main/_Module/GUI/TAOMSpriteData.xml` (the repo and the live install are byte-identical) declares five
categories, all `AlwaysLoad`. The sheets themselves are Kit texture assets: loose in the dev install's
`Modules/TAOM/Assets/GauntletUI/*_tex.tpac`, and packed into `AssetPackages/pack0.tpac` in all three
release channels (testing, patreon, public: the same 52 sheets in each). Formats and sizes are read
from the tpac headers with `tools/audit_map_scene_memory.py`'s reader; "resident" is the full mip
chain the header declares.

| Sheet | Format | Size, mips | Resident | Declared? |
|---|---|---|---|---|
| `ui_custom_fonts_1` to `_3` | BC4 | 2048 x 2048, 12 | 2.7 MB each, 8.0 MB | yes |
| `ui_loading_1`, `_2` | R8G8B8A8 (uncompressed) | 4096 x 4096, 13 | 85.3 MB each, 170.7 MB | yes |
| `ui_taom_1` to `_5` | DXT5 | 4096 x 4096, 13 | 21.3 MB each, 106.7 MB | yes |
| `ui_taom_bannericons_1`, `_2` | R8G8B8A8 (uncompressed) | 4096 x 4096, 1 | 64.0 MB each, 128.0 MB | yes |
| `ui_taom_career_system_1` | DXT5 | 4096 x 4096, 13 | 21.3 MB | yes |
| `ui_taom_bannericons_3` to `_41` | 37 R8G8B8A8, 2 DXT5 (`_24`, `_25`) | 4096 x 4096 | 2,410 MB if ever loaded | **no** |

**Declared, always resident: 434.7 MB of texture data, 298.7 MB of it uncompressed** (the two loading
sheets and the two banner-icon sheets).

**The 39 undeclared banner-icon sheets** are left over from the category's 41-sheet layout: the
sprite data declared 25 sheets until 2026-07-04, 41 until commit `62abaeb6` on 2026-08-08, and 2 since.
The loose files in `Assets/GauntletUI` date from 2026-08-06 (39 files: exactly sheets 3 to 41) and
2026-08-08 (the 13 current sheets), so the regeneration on 08-08 replaced sheets 1 and 2 and left the
rest. They are never requested, so they cost no runtime memory, but they are **39.6 MB of the 180.2 MB
`pack0.tpac`** every player downloads (22%, stored size inside the pack).

## Whether this memory is in RAM or on the GPU

A texture is GPU memory; whether the engine also keeps a system-memory copy of a pinned UI texture is
UNVERIFIED. For scene textures it does show in the process: on 2026-09-12 the texture-quality slider
alone moved 2.3 GB of private commit (`docs/investigations/native-commit-audit-2026-08.md:649-666`).
Either way, 299 MB of uncompressed UI sheets held for the whole session matters most on 4 to 6 GB
GPUs and 16 GB machines.

## The two levers (content, in the Kit)

1. **Delete the 39 orphan sheets** (`ui_taom_bannericons_3` to `_41`) from the Kit project before the
   next packaging. No runtime risk (the engine never asks for them); saves 39.6 MB of every download.
   Verify afterwards that the packed `pack0.tpac` lists 13 `ui_` textures, not 52.
2. **Compress the four uncompressed sheets** (`ui_loading_1`, `_2`, `ui_taom_bannericons_1`, `_2`) to
   BC7 (1 byte per pixel, alpha kept, near-lossless; DXT5 is the same size with visible banding on
   gradients). Saves about **224 MB** of always-resident texture memory (434.7 to 210.7 MB). Check the
   loading screens and the banner editor icons by eye after the change.

Not recommended without a test: dropping `AlwaysLoad` from `ui_loading` (another 43 to 171 MB outside
loading screens), because a category loaded on demand can show a blank frame on the first loading
screen.
