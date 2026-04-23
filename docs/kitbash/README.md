# Kitbash catalogs

Persistent reference documentation for the kitbash asset families in TAOM.
Each subfolder catalogs one kit with its textures, materials, meshes, and
composition patterns — so future design sessions can compose prefabs from
outside the editor without re-learning the asset layout every time.

## Kits

| Kit | Location | Coverage |
|---|---|---|
| Erebor (dwarven) | [erebor/](erebor/) | Full — textures, materials, meshes, design patterns |
| Mirkwood (elven) | — | Not catalogued yet (referenced in `taom_mirkwood_kitbash.xml`) |
| Gondor | — | Not catalogued yet (referenced in `taom_gondor_kitbash.xml`) |
| Mordor | — | Not catalogued yet (referenced in `taom_mordor_kitbash.xml`) |

## Naming conventions (shared across all TAOM kits)

Prefixes:
- `t_<kit>_...` — raw texture asset (.dds / .png files)
- `m_<kit>_...` — material asset (references textures, applies shader parameters)
- `sm_<kit>_...` — static mesh asset (references materials)
- `bo_<mesh_id>` — collision body paired with a visual mesh of the same base name

Kit prefixes used in TAOM: `dw` (dwarven — Erebor), `gd` (Gondor), `mordor`
(Mordor), `mirkwood` (Mirkwood), `nat` (nature — shared across kits).

Channel suffixes on textures:
- `_d` — diffuse (base colour)
- `_d2`, `_d3`, `_d4` — alternate diffuse maps that share the same `_n`/`_s`
  (used for color-swap families like obsidian black/blue/green/red)
- `_n` — normal map
- `_s` — specular map
- `_h` — height map (parallax / displacement)

## How to use these catalogs

When you need to compose a building prefab by writing positional XML:
1. Start at the kit's `design-patterns.md` — reusable templates.
2. Use `meshes.md` to select specific pieces (each entry notes shape + typical use).
3. Reference `materials.md` only when a mesh's implied texture is unclear.
4. Reference `textures.md` rarely — mostly when authoring new materials.

When you learn something new during a build iteration (pivot quirk,
dimension, rotation convention), update the relevant file immediately so
the knowledge persists to the next session.
