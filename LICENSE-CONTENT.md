# TAOM Licensing: overview, and the content license

**Read this before forking or redistributing TAOM.** The project is licensed in parts, and
[LICENSE](LICENSE) covers only one of them. It is kept as plain MIT text so automated license
detection keeps working, which means it cannot carry this map. This file is the map.

| Part | License | Where |
|---|---|---|
| Software | MIT | [LICENSE](LICENSE) |
| Art, 3D and 2D assets, world map, game data, lore text | CC BY-NC-SA 4.0 | this file |
| Audio, fonts, third-party binaries | **Not TAOM's to license** | [THIRD-PARTY-LICENSES.txt](Main/_Module/THIRD-PARTY-LICENSES.txt) |
| The TAOM name and branding | **Granted by no license here** | [TRADEMARK.md](TRADEMARK.md) |

The code is genuinely free. What does not come with it is the audio and fonts, which are not ours
to pass on, and the project's name, which no open license grants. A fork of TAOM is welcome. A fork
*called* TAOM is not.

The rest of this file is the content license and its path-by-path scope.

The content below is licensed under **Creative Commons Attribution-NonCommercial-ShareAlike 4.0
International (CC BY-NC-SA 4.0)**:

- Deed (human-readable summary): https://creativecommons.org/licenses/by-nc-sa/4.0/
- Legal code (full text): https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode

In short: you are free to **share** and **adapt** this content for **non-commercial** purposes, as
long as you give **attribution** and distribute any derivatives under the **same license**.

## Scope, by path

Earlier versions of this file split the repository by category ("C# code" versus "game data,
XML"), which left files claimed by two clauses at once. It is now split by path.

### MIT (software)

| Path | What |
|---|---|
| `Main/**/*.cs` | Mod source (1,801 files) |
| `Main/_Module/GUI/PreFabs/**`, `Main/_Module/GUI/Brushes/**` | Gauntlet UI definitions and styling |
| `TAOM.Tests/**` | Test suite |
| `Dependencies/**/*.{cs,cpp,h}` and its project files | Dependency module source |
| `Stubs/**` | Build stubs |
| `tools/**` except the image outputs listed below | Python, PowerShell, and shell tooling |
| `build.ps1`, `setup-dev-env.ps1`, `Directory.Build.props`, `*.sln`, `*.csproj`, `*.vcxproj` | Build configuration |
| `.claude/**`, `.github/**`, `.vscode/**`, `.codex/**`, `.serena/**` | Development harness |

### CC BY-NC-SA 4.0 (content)

| Path | What |
|---|---|
| `Main/_Module/GUI/SpriteParts/**` | Sprite art (1,023 files) |
| `Main/_Module/GUI/SpriteData/**` | Sprite atlases and their metadata (227 files) |
| `Main/_Module/Assets/**` | Banner icons, Gauntlet UI assets, main map textures (121 files) |
| `Main/_Module/AssetSources/**` | Layered art sources (52 PNG, 34 PSD) |
| `Main/_Module/ModuleData/**` | Game data: troops, cultures, items, settlements, lore strings (346 files) |
| `tools/factionmap_output/**` image outputs | The world map renders (98 files) |
| `tools/runes/**` image outputs | Rune art (6 files) |
| `docs/**` prose and diagrams | Lore text, feature writing, and documentation art |

### Neither: material TAOM does not own

| Path | Status |
|---|---|
| `Main/_Module/ModuleSounds/**` | **Third-party audio** (436 files). Not TAOM's to license. Redistributed as part of a non-commercial fan project |
| `Main/_Module/GUI/Fonts/**` | **Third-party fonts** (Aniron, Minion Pro, Ringbearer). Not TAOM's to license |
| `Main/_Module/AssetPackages/**` | **Commissioned art, redistributed as delivered.** The four Yotthani meshes (`fieldcamp_camp_a`, `fieldcamp_palisade_ring`, `refuge_camp_a`, `refuge_palisade_ring`). Cleared for TAOM's use; not TAOM's to sublicense onward |
| `Main/_Module/Prefabs/taom_howdah_agent.xml` | **Purchased asset** (ADOD_Beasts). Cleared for TAOM's use; not TAOM's to sublicense onward |
| `Main/_Module/bin/**`, `Dependencies/**/*.dll` | Third-party binaries under their own licenses |

Redistributed binaries: [THIRD-PARTY-LICENSES.txt](Main/_Module/THIRD-PARTY-LICENSES.txt).
**Every third-party source TAOM derives from, with its license and derivation type, is recorded in
[docs/reference/provenance-register.md](docs/reference/provenance-register.md), which is the
authoritative record.** What TAOM itself authored, and on what basis:
[docs/reference/asset-provenance.md](docs/reference/asset-provenance.md).

### Precedence

**Where a path could fall in more than one bucket, the content license governs over MIT, and the
third-party section governs over both.** A file whose path is not listed above is software if it
is source code and content otherwise.

## Original & transformative work

The art, 3D models, and other assets we create for this mod are original works **inspired by**
Tolkien's legendarium and its adaptations. They are **not one-to-one reproductions** of any source
material. Our artists take creative liberties when interpreting Middle-earth; the designs are our
own interpretation rather than direct copies of any specific copyrighted work.

## Fan-project disclaimer

This mod is a fan project and is not affiliated with or endorsed by the Tolkien Estate, New Line
Cinema, Middle-earth Enterprises, or TaleWorlds Entertainment. *The Lord of the Rings* and all
related names, characters, and places are the property of their respective rights holders. No
commercial use is intended.
