# Kit worklist: UI sprite sheets, banner atlases and loading screens (FOR-MIKE item 13 a, b, c)

Written for: Mike, at the Modding Kit, if you say yes to item 13. Read 2026-10-03 against trunk
`bannerlord-1.5.x` at `7f0c8446`, the dev install and the three release folders. Every number below comes
from the texture descriptors and packs as they are today; each section says what to back up, what to change,
the bytes before and after, and what to look at in game. Where something could not be checked from files,
it says UNVERIFIED.

The Kit's menus are not described here: this says which setting must change, not where its button is.

## Before you start (applies to every section)

**Where each texture lives.** A TAOM texture is three files in the install's `Modules\TAOM`:

| Piece | Path inside `Modules\TAOM` | What it holds |
|---|---|---|
| Source image | `AssetSources\GauntletUI\*.png`, `AssetSources\BannerIcons\*.psd`, `AssetSources\LoadingScreen\*.png` | The picture you edit |
| Descriptor | `Assets\GauntletUI\*_tex.tpac`, `Assets\BannerIcons\*_tex.tpac`, `Assets\loadingscreen\*_tex.tpac` | 500 to 600 bytes: name, format, size, mip count |
| Compiled pixels | `RuntimeDataCache\<package id>.rdc` | The Kit's compiled texture; the id is in each table below (first 8 characters) |

The release folders (testing, patreon, public) hold none of these: their `Modules\TAOM\AssetPackages\pack0.tpac`
carries every TAOM texture. All four copies of `pack0.tpac` (dev install, testing, patreon, public) are
identical today: 188,902,921 bytes, the same MD5.

**The repo tracks the source images and descriptors.** `Main/_Module/AssetSources` and `Main/_Module/Assets`
are in git and byte-identical to the install today (52 sheets and 52 descriptors under GauntletUI, 33 PSDs and
33 descriptors plus 33 materials under BannerIcons, 3 and 3 under LoadingScreen); `RuntimeDataCache` is
git-ignored. This corrects FOR-MIKE item 13's "not in the repo" for (a) and (b). It matters because
`./build.ps1` copies all of `Main/_Module` into the install, overwriting what is there and never deleting
(Bannerlord.BuildResources `CopyModule`, `Clean="false"`, with `ExcludeSourceFilesFromModule` false in
`Main/TAOM.csproj`). So:

- **Do not build between the Kit work and the copy back into the repo.** A build puts the old descriptors
  back over the new ones and brings the 39 deleted sheets back into the install.
- After the Kit work, copy the changed descriptors back into the repo and delete the removed files there too,
  the same round trip as the loading-screen feature (`docs/features/native-loading-screen.md`, "How to change
  the art", step 2). The list is at the end of this page. Committing it waits for your word.

**The dev install also has its own `AssetPackages\pack0.tpac`** (2026-10-02 15:00, identical to the release
packs), which your packaging notes say the live install does not need. Which copy the game uses when a loose
descriptor and the pack both carry the same texture name is UNVERIFIED, so do the in-game checks once both
agree: after packaging, if the editor writes the new pack there, or after removing that pack from the dev
install as your packaging notes allow.

**Standing rules:** back up every original per file on the E: drive first; no renames (every texture keeps its
exact name; the change is a format setting or a deletion); you package in the editor.

**Suggested order:** (a), then (b), then (c) in two batches, then the loading screens, then package once and
run all the in-game checks in one session.

### The backup step

Make a new folder at the root of the E: drive named like the earlier ones, `taom-texture-backup-YYYY-MM-DD`
(the day you do it), with a `TAOM` folder inside, the same shape as the 2026-09-13 and 2026-09-29 trees. The
script below copies, per texture, the source image, the descriptor and the `.rdc`, keeping each file's path
relative to the module. It copies only, refuses to overwrite an existing backup file, stops on a missing file
or a size mismatch, and with `-Check` copies nothing and reports what it would copy. It was run on
`loadingscreen_default` and `loadingscreen_naval` (6 files, sizes matched) and with `-Check` on every list
below.

Save it as `backup-textures.ps1` anywhere outside the repo and the game folder:

```powershell
param(
    [Parameter(Mandatory)] [string] $Module,    # <game>\Modules\TAOM
    [Parameter(Mandatory)] [string] $Backup,    # the new dated backup folder's TAOM subfolder
    [Parameter(Mandatory)] [string[]] $Names,   # texture names
    [switch] $Check                             # resolve every file and report the total; copy nothing
)
$ErrorActionPreference = 'Stop'
$sources = Get-ChildItem (Join-Path $Module 'AssetSources') -Recurse -File
$stubs = Get-ChildItem (Join-Path $Module 'Assets') -Recurse -File -Filter '*_tex.tpac'
$copied = 0; $bytes = 0
foreach ($n in $Names) {
    $src = @($sources | Where-Object { $_.BaseName -eq $n })
    $stub = @($stubs | Where-Object { $_.Name -eq "${n}_tex.tpac" })
    if ($src.Count -ne 1 -or $stub.Count -ne 1) { throw "$n : found $($src.Count) source(s) and $($stub.Count) descriptor(s), expected 1 each" }
    $head = [System.IO.File]::ReadAllBytes($stub[0].FullName)[8..23]
    $rdc = Join-Path $Module ('RuntimeDataCache\' + ([guid]::new([byte[]]$head)).ToString().ToUpper() + '.rdc')
    if (-not (Test-Path $rdc)) { throw "$n : no RuntimeDataCache entry $rdc" }
    foreach ($f in @($src[0].FullName, $stub[0].FullName, $rdc)) {
        $rel = $f.Substring($Module.TrimEnd('\').Length + 1)
        $dest = Join-Path $Backup $rel
        if ($Check) { $copied++; $bytes += (Get-Item -LiteralPath $f).Length; continue }
        New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
        if (Test-Path $dest) { throw "$dest already exists: pick a new dated folder rather than overwrite a backup" }
        Copy-Item -LiteralPath $f -Destination $dest
        if ((Get-Item -LiteralPath $dest).Length -ne (Get-Item -LiteralPath $f).Length) { throw "size mismatch: $dest" }
        $copied++; $bytes += (Get-Item -LiteralPath $f).Length
    }
}
$verb = if ($Check) { 'would back up' } else { 'backed up' }
"$verb $copied files for $($Names.Count) textures, $([math]::Round($bytes / 1MB, 1)) MiB, into $Backup"
```

The name lists, set once in the same PowerShell window:

```powershell
$taom = '<game>\Modules\TAOM'
$bk   = '<the new dated backup folder>\TAOM'
$a    = 3..41 | ForEach-Object { "ui_taom_bannericons_$_" }
$b    = 'ui_loading_1', 'ui_loading_2', 'ui_taom_bannericons_1', 'ui_taom_bannericons_2'
$c19  = 'gundabad_alpha_01', 'harad_alpha_01', 'rhun_alpha_01', 'mordor_alpha_01', 'rohan_alpha_01',
        'silvan_alpha_01', 'dwarves_ornaments_01', 'dolguldur_alpha_01', 'dunland_alpha_01', 'dwarves_alpha_02',
        'isengard_alpha_01', 'abanissa_alpha_01', 'gondor_alpha_01', 'umbar_alpha_01', 'dale_alpha_01',
        'gondor_alpha_02', 'noldor_alpha_01', 'sindar_alpha_01', 'gondor_ornaments_01' | ForEach-Object { "taom_banners_$_" }
$c14  = 'anduinvale_alpha_01', 'arnor_alpha_01', 'dale_ornaments_01', 'dolguldur_alpha_02', 'dunland_alpha_02',
        'dwarves_alpha_01', 'goblin_alpha_01', 'gundabad_alpha_02', 'isengard_alpha_02', 'misc_alpha_01',
        'mistymountainorcs_alpha_01', 'mistymountainorcs_alpha_02', 'mordor_alpha_02', 'rhun_ornaments_01' | ForEach-Object { "taom_banners_$_" }
$ls   = 'loadingscreen_default', 'loadingscreen_naval'
```

What `-Check` reported on 2026-10-03, so you know the run is complete:

| List | Files | Size |
|---|---|---|
| `$a` | 117 | 2,477.9 MiB |
| `$b` | 12 | 345.9 MiB |
| `$c19` | 57 | 1,432.1 MiB |
| `$c14` | 42 | 988.2 MiB |
| `$ls` | 6 | 21.0 MiB |

About 5.1 GiB in all; E: had 141 GB free. Also copy one `pack0.tpac` from a release folder into the same dated
folder (180 MiB; all four are identical), since packaging replaces it.

## (a) Delete the 39 leftover banner-icon sheets

**Why it is safe.** The engine asks for sheets `1` to `SpriteSheetCount` of each sprite category and nothing
else (v1.5.3 `TaleWorlds.TwoDimension.SpriteCategory.Load`, re-read today), then pins and preloads each one
(`TwoDimensionEngineResourceContext.LoadTexture`). The checks for these 39:

- `TAOMSpriteData.xml` declares `ui_taom_bannericons` with `SpriteSheetCount` 2, and all 369 of its sprite
  parts sit on sheets 1 and 2. The file is identical in trunk, the dev install and all three release folders.
- No item in 7,517 tpac files (TAOM's loose `Assets` and packs, the testing pack, the live TAOM_Map and Armory
  `Assets`; 15,925 items) refers to any of the 39 by id or by name.
- No file in trunk outside `docs/` and `plans/`, and no ModuleData or GUI file of the three live modules, names
  them. `GUI/SpriteParts/Config.xml` names only the category.

**Backup:** `.\backup-textures.ps1 -Module $taom -Backup $bk -Names $a` (117 files, 2,477.9 MiB).

**Change:** remove the 39 textures from TAOM in the Kit, then save the module. Afterwards their source PNG
should be gone from `AssetSources\GauntletUI` as well; if the Kit leaves it, delete it, because whether the
Kit re-imports a stray source image on its next start is UNVERIFIED. Whether the Kit also deletes the 39 `.rdc`
files (2.4 GiB of disk) is UNVERIFIED; they never ship either way.

**Bytes after:** 0 for each. Today they cost no memory (never requested) and 41,529,118 bytes (39.6 MiB) of
the 188,902,921-byte `pack0.tpac` every player downloads, so the pack should come out about 147 MB (estimate:
the pack's own index shrinks a little too).

| Sheet | Source image in `AssetSources\GauntletUI` (bytes) | Format | Pixels | Mips | Bytes if loaded | Stored in `pack0.tpac` | `.rdc` id |
|---|---|---|---|---|---|---|---|
| `ui_taom_bannericons_3` | `ui_taom_bannericons_3.png` (1,416,477) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 846,183 | `1EB1CC9B` |
| `ui_taom_bannericons_4` | `ui_taom_bannericons_4.png` (1,858,495) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,131,113 | `5E0CE651` |
| `ui_taom_bannericons_5` | `ui_taom_bannericons_5.png` (1,358,880) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 803,457 | `3610757B` |
| `ui_taom_bannericons_6` | `ui_taom_bannericons_6.png` (1,663,922) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 947,249 | `3126445B` |
| `ui_taom_bannericons_7` | `ui_taom_bannericons_7.png` (2,628,718) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,531,553 | `CC9095E6` |
| `ui_taom_bannericons_8` | `ui_taom_bannericons_8.png` (2,114,926) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,239,862 | `D41E96CC` |
| `ui_taom_bannericons_9` | `ui_taom_bannericons_9.png` (1,385,183) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 824,781 | `6EE2D919` |
| `ui_taom_bannericons_10` | `ui_taom_bannericons_10.png` (2,283,900) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,313,195 | `6FA9C7C6` |
| `ui_taom_bannericons_11` | `ui_taom_bannericons_11.png` (2,208,776) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,283,918 | `37E804B7` |
| `ui_taom_bannericons_12` | `ui_taom_bannericons_12.png` (2,283,630) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,329,336 | `0594F0D4` |
| `ui_taom_bannericons_13` | `ui_taom_bannericons_13.png` (2,192,126) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,259,495 | `0E64CC96` |
| `ui_taom_bannericons_14` | `ui_taom_bannericons_14.png` (1,822,810) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,031,232 | `1C7196C8` |
| `ui_taom_bannericons_15` | `ui_taom_bannericons_15.png` (2,102,310) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,215,972 | `B3E004E1` |
| `ui_taom_bannericons_16` | `ui_taom_bannericons_16.png` (2,276,174) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,302,204 | `FAB7F7FA` |
| `ui_taom_bannericons_17` | `ui_taom_bannericons_17.png` (1,587,486) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 901,312 | `142F41C9` |
| `ui_taom_bannericons_18` | `ui_taom_bannericons_18.png` (1,618,332) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 917,977 | `34060833` |
| `ui_taom_bannericons_19` | `ui_taom_bannericons_19.png` (2,580,855) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,511,467 | `3B594A33` |
| `ui_taom_bannericons_20` | `ui_taom_bannericons_20.png` (2,244,833) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,319,508 | `32189837` |
| `ui_taom_bannericons_21` | `ui_taom_bannericons_21.png` (2,228,116) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,363,508 | `B88DD751` |
| `ui_taom_bannericons_22` | `ui_taom_bannericons_22.png` (1,907,877) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,228,283 | `88F3FBE0` |
| `ui_taom_bannericons_23` | `ui_taom_bannericons_23.png` (1,756,157) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,093,981 | `61A6253F` |
| `ui_taom_bannericons_24` | `ui_taom_bannericons_24.png` (1,470,100) | DXT5 | 4096 x 4096 | 13 | 22,369,648 | 769,833 | `2F1387B7` |
| `ui_taom_bannericons_25` | `ui_taom_bannericons_25.png` (1,405,476) | DXT5 | 4096 x 4096 | 13 | 22,369,648 | 679,619 | `13F9D9A3` |
| `ui_taom_bannericons_26` | `ui_taom_bannericons_26.png` (1,122,571) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 645,669 | `936CE09B` |
| `ui_taom_bannericons_27` | `ui_taom_bannericons_27.png` (1,552,394) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 976,835 | `D17FA400` |
| `ui_taom_bannericons_28` | `ui_taom_bannericons_28.png` (1,349,205) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 852,136 | `6A98876B` |
| `ui_taom_bannericons_29` | `ui_taom_bannericons_29.png` (1,441,709) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 926,308 | `938F0016` |
| `ui_taom_bannericons_30` | `ui_taom_bannericons_30.png` (1,516,677) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 985,651 | `B5D92B7C` |
| `ui_taom_bannericons_31` | `ui_taom_bannericons_31.png` (1,584,701) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 966,500 | `14099D54` |
| `ui_taom_bannericons_32` | `ui_taom_bannericons_32.png` (2,497,310) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,447,275 | `203A50C7` |
| `ui_taom_bannericons_33` | `ui_taom_bannericons_33.png` (2,086,787) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,227,721 | `12BBB906` |
| `ui_taom_bannericons_34` | `ui_taom_bannericons_34.png` (1,214,065) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 703,015 | `75F6A1B9` |
| `ui_taom_bannericons_35` | `ui_taom_bannericons_35.png` (988,163) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 530,567 | `9579B536` |
| `ui_taom_bannericons_36` | `ui_taom_bannericons_36.png` (1,215,410) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 715,660 | `1FC04BA8` |
| `ui_taom_bannericons_37` | `ui_taom_bannericons_37.png` (1,659,143) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 984,514 | `D89819D0` |
| `ui_taom_bannericons_38` | `ui_taom_bannericons_38.png` (1,535,565) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 941,647 | `C3E3735D` |
| `ui_taom_bannericons_39` | `ui_taom_bannericons_39.png` (1,848,881) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,134,932 | `0FC79C03` |
| `ui_taom_bannericons_40` | `ui_taom_bannericons_40.png` (1,574,666) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 965,933 | `90B367B4` |
| `ui_taom_bannericons_41` | `ui_taom_bannericons_41.png` (2,896,690) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,679,717 | `2ED707CF` |

**Check afterwards.** On disk: `Assets\GauntletUI` holds 13 descriptors and `AssetSources\GauntletUI` 13
PNGs (52 of each today). After packaging, `python -B tpac_big_textures.py <repo>\tools <new pack0.tpac> 200`
(the script beside this file; it only reads) lists 13 textures whose names start `ui_`, not 52. In game:
nothing should change; glance at the banner icon sprites (sheets 1 and 2, the next section) and a loading
screen.

## (b) Re-save the four uncompressed always-loaded sheets as BC7

All five TAOM sprite categories are `AlwaysLoad`, so these four sheets are pinned in texture memory from the
main menu to quit. `ui_loading_1` and `_2` hold the nine loading pictures (`ui_loading_2` to `_10` in the sprite
data, 1500 x 1000 to 3840 x 2160); `ui_taom_bannericons_1` and `_2` hold the 369 banner icon sprites (256 px
each, named by icon id from 10000 up), which the UI uses wherever it draws a banner icon as a sprite (the
banner editor's icon list is the likely place; UNVERIFIED which screens).

**Backup:** `.\backup-textures.ps1 -Module $taom -Backup $bk -Names $b` (12 files, 345.9 MiB).

**Change:** on each of the four textures, set the compression format to **BC7** and save. Keep the mip count
as it is (13 on the loading sheets, 1 on the banner icon sheets) and keep alpha (all four carry `has_alpha`).
The source PNGs do not change. BC7 is 1 byte per pixel with full alpha; DXT5 is the same size but bands on
gradients, and the loading sheets are painted pictures.

| Sheet | Source image in `AssetSources\GauntletUI` (bytes) | Format now | Pixels | Mips | Bytes now | Stored in `pack0.tpac` | `.rdc` id | Target | Bytes after | `.rdc` size after (expected) |
|---|---|---|---|---|---|---|---|---|---|---|
| `ui_loading_1` | `ui_loading_1.png` (16,971,225) | R8G8B8A8_UNORM | 4096 x 4096 | 13 | 89,478,484 | 26,469,369 | `2D6ED9C7` | BC7 | 22,369,648 | 22,369,813 |
| `ui_loading_2` | `ui_loading_2.png` (21,879,893) | R8G8B8A8_UNORM | 4096 x 4096 | 13 | 89,478,484 | 29,995,328 | `58FB8850` | BC7 | 22,369,648 | 22,369,813 |
| `ui_taom_bannericons_1` | `ui_taom_bannericons_1.png` (6,631,075) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 4,411,211 | `4E108A0E` | BC7 | 16,777,216 | 16,777,381 |
| `ui_taom_bannericons_2` | `ui_taom_bannericons_2.png` (3,994,523) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,599,170 | `2E48623C` | BC7 | 16,777,216 | 16,777,381 |

**Bytes after:** 313,174,696 bytes (298.7 MiB) of always-resident texture memory becomes 78,293,728 (74.7 MiB),
224.0 MiB less for the whole session. Download: today the four store as 63,475,078 bytes in the pack.
Vanilla's 4096 px BC7 sheets store at 6 to 24 percent of their size (sparse icon sheets low, painted ones
high), which would put the four at roughly 12 to 19 MB; that is an estimate, read the real figure after
packaging.

The expected `.rdc` size is the texture's bytes plus a 165-byte header. The 165 holds for all 78 textures on
this page today (RGBA8 and DXT5, one mip and full chains); for BC7 it is an inference. If the Kit keeps each package id (expected, UNVERIFIED),
the `.rdc` id stays as listed, and its size is the quickest proof the change took.

**Two things to know, no action needed now:**

- Vanilla's own UI sheets (in Native and SandBox) are mostly BC7 with one mip and carry the `dont_degrade` and
  `dont_delay_loading` flags; TAOM's four carry neither flag. What the texture-quality slider does to a UI
  sheet without `dont_degrade` is UNVERIFIED. This pass leaves the flags alone so only one thing changes.
  Dropping the loading sheets' mips to vanilla's one would save another 10.7 MiB, but changes how a
  downscaled loading picture is filtered; also left for later.
- These four PNGs are output of the sprite sheet generator. The next time you regenerate sheets, check the
  format still reads BC7 afterwards: whether the Kit keeps a texture's format when its source image is
  rewritten is UNVERIFIED.

**Check afterwards, in game:** let three or four loading screens pass (several pictures, at your screen
resolution) and look for blockiness or banding in skies and dark gradients; open the screen that shows TAOM
banner icons as sprites (the banner editor's icon list) and compare edges against the old look. On disk: each
`.rdc` matches the size in the table.

## (c) Re-save the 33 banner atlases as BC7

`banner_icons.xml` maps 368 icons onto 33 atlas materials, up to 16 cells each (`texture_index` 0 to 15). Every atlas texture is 4096 x 4096,
one mip, uncompressed RGBA: 64 MiB apiece, 2,112 MiB for all 33. Vanilla's 16 banner atlases (`custom_banner_icons`
and `_02` to `_16` in Native's `core.tpac`) are 2048 x 2048 BC7, one mip, 4 MiB each. An atlas loads when a
banner using one of its icons is drawn ([banner-atlases.md](banner-atlases.md)).

**First batch, the 19 the campaign's own banners use.** Re-run today against trunk `7f0c8446`, the clan,
kingdom and culture banners (`characters/clans.xml`, `spclans.xslt`, `spkingdoms.xslt`, `taom_spcultures.xml`,
`taom_spkingdoms.xml`) name these 19 and no others; the output matches `banner-atlas-use.txt` exactly. The
"layers" column counts icon layers across those five files that sit on the atlas.

**Second batch, the other 14.** No banner key in TAOM's ModuleData names them; they load only for a banner
built at runtime (the banner editor, a randomly generated clan banner), so they cost nothing in most sessions.
Same change, lower priority.

**Backup:** `.\backup-textures.ps1 -Module $taom -Backup $bk -Names $c19` (57 files, 1,432.1 MiB), and later
`-Names $c14` (42 files, 988.2 MiB).

**Change:** on each atlas texture, set the compression format to **BC7** and save; keep 4096 x 4096 and one
mip. The PSD sources and the 33 materials (`*_mtl.tpac`) do not change. Only 9 atlases carry the `has_alpha`
flag, all of them in the second batch; the other 24, including all 19 of the first batch, do not. Pick BC7
whatever the Kit proposes for those 24: a no-alpha format such as DXT1 (BC1) keeps 1-bit alpha at best. Which
channel the banner shader reads as the mask is UNVERIFIED; BC7 keeps all four, so either way the mask survives.

| Atlas | Layers | Source image in `AssetSources\BannerIcons` (bytes) | Format now | Pixels | Mips | Bytes now | Stored in `pack0.tpac` | `.rdc` id | Target | Bytes after | `.rdc` size after (expected) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `taom_banners_gundabad_alpha_01` | 80 | `taom_banners_gundabad_alpha_01.psd` (6,401,618) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 535,514 | `204E37F7` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_harad_alpha_01` | 29 | `taom_banners_harad_alpha_01.psd` (7,661,378) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 577,729 | `BBD003F5` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_rhun_alpha_01` | 26 | `taom_banners_rhun_alpha_01.psd` (16,764,341) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,146,483 | `18830D23` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_mordor_alpha_01` | 25 | `taom_banners_mordor_alpha_01.psd` (14,084,229) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,889,545 | `C19B1338` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_rohan_alpha_01` | 23 | `taom_banners_rohan_alpha_01.psd` (10,912,029) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,648,661 | `7FF2C3A4` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_silvan_alpha_01` | 16 | `taom_banners_silvan_alpha_01.psd` (13,611,070) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 931,124 | `3470E6AD` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dwarves_ornaments_01` | 15 | `taom_banners_dwarves_ornaments_01.psd` (8,733,120) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 840,332 | `EE6AEED3` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dolguldur_alpha_01` | 14 | `taom_banners_dolguldur_alpha_01.psd` (7,609,513) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 569,576 | `279F123E` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dunland_alpha_01` | 12 | `taom_banners_dunland_alpha_01.psd` (12,864,054) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,091,630 | `48AA370A` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dwarves_alpha_02` | 12 | `taom_banners_dwarves_alpha_02.psd` (10,256,003) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,375,335 | `3D60A3D7` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_isengard_alpha_01` | 12 | `taom_banners_isengard_alpha_01.psd` (5,488,049) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 370,957 | `1B7FE1FB` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_abanissa_alpha_01` | 10 | `taom_banners_abanissa_alpha_01.psd` (27,762,354) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,682,545 | `AA1F24C8` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_gondor_alpha_01` | 10 | `taom_banners_gondor_alpha_01.psd` (13,181,679) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,532,865 | `8012DF0D` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_umbar_alpha_01` | 10 | `taom_banners_umbar_alpha_01.psd` (12,467,505) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,059,353 | `5ABA7CCC` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dale_alpha_01` | 9 | `taom_banners_dale_alpha_01.psd` (4,937,196) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 484,664 | `043E659A` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_gondor_alpha_02` | 8 | `taom_banners_gondor_alpha_02.psd` (11,961,820) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,414,575 | `FA858520` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_noldor_alpha_01` | 8 | `taom_banners_noldor_alpha_01.psd` (15,225,976) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,509,654 | `B2946DF8` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_sindar_alpha_01` | 4 | `taom_banners_sindar_alpha_01.psd` (14,716,385) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 829,465 | `6C8D2916` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_gondor_ornaments_01` | 2 | `taom_banners_gondor_ornaments_01.psd` (11,931,814) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 997,743 | `C2FD6E81` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_anduinvale_alpha_01` | 0 | `taom_banners_anduinvale_alpha_01.psd` (11,290,692) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 675,348 | `5AABFE33` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_arnor_alpha_01` | 0 | `taom_banners_arnor_alpha_01.psd` (9,720,550) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 918,460 | `C2ECDEB3` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dale_ornaments_01` | 0 | `taom_banners_dale_ornaments_01.psd` (5,876,307) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 645,682 | `37165446` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dolguldur_alpha_02` | 0 | `taom_banners_dolguldur_alpha_02.psd` (4,719,132) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,540,557 | `1EEE56C5` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dunland_alpha_02` | 0 | `taom_banners_dunland_alpha_02.psd` (3,354,743) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,699,951 | `A338FB61` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_dwarves_alpha_01` | 0 | `taom_banners_dwarves_alpha_01.psd` (27,504,412) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,753,596 | `762C6ADC` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_goblin_alpha_01` | 0 | `taom_banners_goblin_alpha_01.psd` (4,051,289) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,091,924 | `FCE64310` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_gundabad_alpha_02` | 0 | `taom_banners_gundabad_alpha_02.psd` (4,522,510) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,390,719 | `60AB3B34` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_isengard_alpha_02` | 0 | `taom_banners_isengard_alpha_02.psd` (4,122,219) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,170,832 | `F7FF870B` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_misc_alpha_01` | 0 | `taom_banners_misc_alpha_01.psd` (3,605,243) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 1,708,869 | `46DE747F` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_mistymountainorcs_alpha_01` | 0 | `taom_banners_mistymountainorcs_alpha_01.psd` (4,590,466) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,436,180 | `4F4C0FAF` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_mistymountainorcs_alpha_02` | 0 | `taom_banners_mistymountainorcs_alpha_02.psd` (4,228,085) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,214,415 | `0F6CB2C5` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_mordor_alpha_02` | 0 | `taom_banners_mordor_alpha_02.psd` (4,308,988) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 2,268,988 | `904ECA35` | BC7 | 16,777,216 | 16,777,381 |
| `taom_banners_rhun_ornaments_01` | 0 | `taom_banners_rhun_ornaments_01.psd` (4,763,518) | R8G8B8A8_UNORM | 4096 x 4096 | 1 | 67,108,864 | 377,589 | `A3D53C3E` | BC7 | 16,777,216 | 16,777,381 |

The first 19 rows are the first batch; the 14 rows with 0 layers are the second.

**Bytes after:** first batch 1,216 MiB to 304 MiB if a session draws all 19 (912 MiB less); second batch 896
MiB to 224 MiB; all 33, 2,112 MiB to 528 MiB. How many of the 19 a campaign actually holds, and whether that
memory shows in the process or only on the GPU, is not knowable offline: one campaign load before and after,
read from `[MemStation]` after the map's first view (FOR-MIKE item 13 c), settles it. Download: today the 33
store as 45,380,860 bytes; vanilla's BC7 banner atlases store at 3 to 11 percent of their size, which would put a
16 MiB atlas near 0.5 to 1.8 MB against 0.4 to 2.5 MB today, so the download should change little (estimate).

**Not in this pass:** vanilla's own size, 2048 x 2048 BC7 (4 MiB each, 132 MiB for all 33). That is a resize of
the PSDs, a bigger change, and whether anything in TAOM assumes 4096 px atlases is UNVERIFIED.

**Check afterwards, in game:** load a campaign and look at banners with many layers on the first-batch atlases:
a Gundabad clan (80 layers on `gundabad_alpha_01`), Harad, Rhun, Mordor and Rohan clans and kingdoms, a dwarf
clan (`dwarves_ornaments_01`). The encyclopedia's clan and kingdom pages, parties and settlements on the map,
and a battle's banners all draw them. Look for soft or blocky icon edges, a halo around an emblem, or a layer
that has gone missing; five clans already show missing layers today for an unrelated reason (13e:
`clan_khuzait_16`, `clan_mirkwood_5` and `_6`, `clan_rivendell_2`, `clan_lothlorien_2`), so do not count those.
For the second batch, open the banner editor and scroll through TAOM's icons. On disk: each `.rdc` matches the
size in the table.

## The two loading-screen textures from trunk `d9a8f46f`: yes, same pass

`loadingscreen_default` and `loadingscreen_naval` (the first-screen background, by vanilla's texture names)
were imported today. Their descriptors confirm the report: 1920 x 1080, R8G8B8A8_UNORM, one mip, 8,294,400 bytes
each, flags `dont_degrade` and `dont_delay_loading`. Vanilla's textures of the same names are 1920 x 1080 DXT5,
one mip, 2,073,600 bytes. Neither is in any `pack0.tpac` yet (all four packs predate the import), so they will
first ship with your next packaging.

**Recommendation: include them.** It is the same kind of change, it saves 6,220,800 bytes per texture (11.9 MiB
for the pair), and doing it now means players never download them uncompressed. The art was confirmed in game today,
so the check is one game start.

**Backup:** `.\backup-textures.ps1 -Module $taom -Backup $bk -Names $ls` (6 files, 21.0 MiB).

**Change:** set the compression format to **BC7** on both (DXT5, vanilla's choice, is the same size); keep one
mip and keep both flags. The feature doc's import steps say "leave mipmaps on", yet the descriptors record one
mip, as vanilla's do; nothing to change there.

| Texture | Source image in `AssetSources\LoadingScreen` (bytes) | Format now | Pixels | Mips | Bytes now | Stored in `pack0.tpac` | `.rdc` id | Target | Bytes after | `.rdc` size after (expected) |
|---|---|---|---|---|---|---|---|---|---|---|
| `loadingscreen_default` | `loadingscreen_default.png` (2,722,546) | R8G8B8A8_UNORM | 1920 x 1080 | 1 | 8,294,400 | not packed | `EB8C35A8` | BC7 | 2,073,600 | 2,073,765 |
| `loadingscreen_naval` | `loadingscreen_naval.png` (2,722,546) | R8G8B8A8_UNORM | 1920 x 1080 | 1 | 8,294,400 | not packed | `D129F160` | BC7 | 2,073,600 | 2,073,765 |

Whether these stay resident after the first screen is UNVERIFIED, so the 11.9 MiB is an upper bound.

**Check afterwards, in game:** start the game and watch the first screen: the title lettering and the dark
smoke gradients are where compression would show. The naval background shows only with War Sails enabled and
has not been seen in game yet (feature doc), so it can wait for the War Sails check that is already owed.

## After the Kit work: back into the repo, then package

Before any `./build.ps1`, bring the repo in line with the install (a commit on your word):

| Section | In `Main/_Module` |
|---|---|
| (a) | delete the 39 `AssetSources/GauntletUI/ui_taom_bannericons_<3..41>.png` and the 39 `Assets/GauntletUI/ui_taom_bannericons_<3..41>_tex.tpac` |
| (b) | copy the 4 changed `Assets/GauntletUI/*_tex.tpac` back from the install |
| (c) | copy the 33 changed `Assets/BannerIcons/taom_banners_*_tex.tpac` back from the install |
| Loading screens | copy the 2 changed `Assets/loadingscreen/loadingscreen_*_tex.tpac` back from the install |

The source images should not change in (b), (c) or the loading screens; if a byte comparison of
`AssetSources` between the install and the repo shows otherwise, copy those back too. `RuntimeDataCache` stays
out of git.

Then package TAOM in the editor as usual. Read the new pack with `tpac_big_textures.py` (beside this file):
13 `ui_` textures; `ui_loading_1`, `_2`, `ui_taom_bannericons_1`, `_2`, every `taom_banners_*` and both
`loadingscreen_*` in BC7; no `R8G8B8A8_UNORM` row left among them. Then run the in-game checks above.

## Totals

| Section | Texture memory now | After | Stored in `pack0.tpac` today |
|---|---|---|---|
| (a) 39 leftover sheets | 0 (never requested; 2,410.7 MiB if they were) | removed | 39.6 MiB |
| (b) 4 always-loaded sheets | 298.7 MiB, whole session | 74.7 MiB | 60.5 MiB |
| (c) first 19 atlases | up to 1,216 MiB, when drawn | 304 MiB | 20.5 MiB |
| (c) other 14 atlases | up to 896 MiB, rarely drawn | 224 MiB | 22.8 MiB |
| Loading screens | up to 15.8 MiB | 4.0 MiB | not packed yet |

## Still UNVERIFIED after this reading

- Whether the Kit deletes a removed texture's source image and `.rdc`, and whether it re-imports a stray source
  image on its next start.
- Whether the Kit keeps each texture's package id on a format change (the `.rdc` ids above assume it does) and
  keeps the format when the sprite generator rewrites a sheet.
- Which copy the game uses when the dev install's `pack0.tpac` and a loose descriptor carry the same texture.
- How many banner atlases a campaign holds, and whether texture memory shows in private bytes or only in VRAM.
- The download size after (b), (c) and the loading screens: estimates only, read it after packaging.
- Which screens draw the banner icon sprites of sheets 1 and 2.

Also noticed, no action proposed: `Assets/BannerIcons/taom_banners_dunland_alpha_01_tex.tpac.ptemp` (543 bytes,
2026-04-08) is a Kit temporary file that sits in the install and has been in git since `55870667`.
