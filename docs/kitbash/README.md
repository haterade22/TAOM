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
| Gondor | — | Not catalogued yet (referenced in `taom_gondor_kitbash.xml`). 2026-07-28: added harbor ships `sm_gondor_ship_{cog,longship,war}_001` (Tripo AI sources, 1.9M→40k decimation + chart re-UV + high-to-low rebake) + `t_gondor_ship_<name>_{d,n,s}` under `Scenes/Gondor/ships/{,textures/}` — pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) § Single-prop path. |
| Lond Cirion walls | [lond-cirion-walls.md](lond-cirion-walls.md) | Full — 8 ploppable sections (L, gatehouse straight, straight, kinks, gate court, sweep, the assembled ring with open siege frontage) composed programmatically from the Gondor castle L3 pieces; registration facts, the three composition laws, assembler workflow. |
| Mordor | — | Not catalogued yet (referenced in `taom_mordor_kitbash.xml`). 2026-07-25: added `sm_mordor_mm_throne_001` (Witch-king throne, Tripo AI source, chart re-UV + rebake) + `t_mordor_mm_throne_{d,n,s}` — single-prop pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) § Single-prop path. Uses `t_`-prefix material naming like Rivendell/Tents. |
| Rivendell (elven) | — | Converted from the ElvenForestCity UE 5.1 kit (2026-07-15): 458 modular meshes + ~660 textures + 196 generated materials in `TAOM_Map/AssetSources/Scenes/Rivendell/`. Not catalogued yet. Pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md). **Naming exception:** materials are named `t_rivendell_<set>` (== their texture set, user decision) — no `m_` prefix. |
| Tents (culture-neutral) | — | Fab Medieval Tent Collection (2026-07-16): one kit FBX (`Scenes/Tents/tents_medieval_kit.fbx`, 9 tents + `bo_` twins, wood physics) + `t_tent_*` texture sets (white/clear canvas only). Wide-family + On_Sticks textures missing from the vault (pending re-download). Same `t_`-material naming as Rivendell. |

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
