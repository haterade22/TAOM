# Asset Provenance: what TAOM authored

The record behind the CC BY-NC-SA claim in [LICENSE-CONTENT.md](../../LICENSE-CONTENT.md). A
license claim is only as strong as the record behind it, and this is the affirmative half: what
TAOM made, and on what basis it is TAOM's to license onward.

**This file covers TAOM-owned content only.** Every third-party source, with its license, derivation
type, and clearance status, belongs in
[provenance-register.md](provenance-register.md), which is the single authoritative record and the
one to update when something new comes in. The governing rule is
[`.claude/rules/provenance.md`](../../.claude/rules/provenance.md): name the source, state its
license.

**Counts were taken from `git ls-files` on 2026-08-25 and will drift. Recount before relying on
them.**

## TAOM-authored content (CC BY-NC-SA 4.0)

All 2D and 3D art in the directories below is made by the project, hand-authored or AI-assisted and
then worked by hand. None of it is extracted from another game.

| Directory | Files | What |
|---|---|---|
| `Main/_Module/GUI/SpriteParts/` | 1,023 | UI sprite art |
| `Main/_Module/GUI/SpriteData/` | 227 | Sprite atlases and metadata, generated from the above |
| `Main/_Module/Assets/` | 121 | Banner icons, Gauntlet UI assets, main map textures |
| `Main/_Module/AssetSources/` | 86 | Layered art sources (52 PNG, 34 PSD) |
| `tools/factionmap_output/` | 98 images | The Middle-earth world map renders |
| `tools/runes/` | 6 images | Rune art |
| `Main/_Module/ModuleData/` | 346 | Troops, cultures, items, settlements, lore strings |
| `docs/` | 722 | Lore and project writing, 9 diagrams |

The `LOTR` appearing in some directory names is thematic, not a source.

**Two directories that look like they belong above and do not:**
`Main/_Module/AssetPackages/` (four commissioned Yotthani meshes, redistributed as delivered) and
`Main/_Module/Prefabs/taom_howdah_agent.xml` (a purchased ADOD_Beasts asset). Both are cleared for
TAOM's use and neither is TAOM's to sublicense onward. Their rows are in
[provenance-register.md](provenance-register.md).

> **TODO for the maintainer:** name the individual artists and the rough date range per directory.
> A manifest that can point at a person is worth considerably more in a dispute than one that says
> "the project." Nobody else can fill this in.

## On the AI-assisted art

Some sprite and icon art was produced with AI assistance and then selected, arranged, retouched, or
used as a base for further work.

Worth recording precisely, because the copyright position differs by how much a human did. Under
current US law, output with no human authorship is not copyrightable (US Copyright Office guidance,
2023; *Thaler v. Perlmutter*). Human selection, arrangement, and modification of AI output **is**
protectable, which is how *Zarya of the Dawn* was registered for its human-authored elements while
its raw generated images were disclaimed.

Practically, for TAOM:

- The 3D models, the world map, the hand-authored art, the lore text, and the data compilation are
  on solid ground.
- Raw generated images used without further human work are the weakest part of the claim.
- The compilation itself, meaning the selection and arrangement of thousands of assets into this
  mod, is protectable regardless.

**This is a reason to record what the human did, not a reason to avoid claiming the art.** It is
also not legal advice, and it is worth ten minutes of a lawyer's time before any enforcement action.

> **TODO for the maintainer:** note which directories are AI-assisted and what the human
> contribution was. "Generated, then recolored and composited into the atlas" is a materially
> stronger record than silence.

## Third-party content: where it is recorded

Audio (`Main/_Module/ModuleSounds/`) and fonts (`Main/_Module/GUI/Fonts/`) are third-party, excluded
from the CC grant, and carry rows in [provenance-register.md](provenance-register.md) plus notices
in [THIRD-PARTY-LICENSES.txt](../../Main/_Module/THIRD-PARTY-LICENSES.txt).

The register also carries the two uncleared software rows that any credible claimant would look at
first, both predating this file and both already tracked there: **NativeSkinFixes** (a verbatim C++
port with no identified upstream, whose built DLL ships even though the feature is parked, and which
the register calls its highest-priority row) and **BetaDeps** (a behavioural port, license
`UNKNOWN`). Neither is what the August 2026 "stolen code" comments referred to, since those named
nothing at all. Both are real work items, and the register is where their status lives.

## Related

- [provenance-register.md](provenance-register.md), the authoritative third-party record
- [`.claude/rules/provenance.md`](../../.claude/rules/provenance.md), the rule
- [LICENSE-CONTENT.md](../../LICENSE-CONTENT.md), the path-by-path license split
- [TRADEMARK.md](../../TRADEMARK.md), the name, which no license grants
- [scene-scripts/ATTRIBUTION.md](../scene-scripts/ATTRIBUTION.md), the clean-room procedure
