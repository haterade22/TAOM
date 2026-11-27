# Kitbash catalogs

Persistent reference documentation for the kitbash asset families in TAOM.
Each subfolder catalogs one kit with its textures, materials, meshes, and
composition patterns — so future design sessions can compose prefabs from
outside the editor without re-learning the asset layout every time.

## Kits

| Kit | Location | Coverage |
|---|---|---|
| Erebor (dwarven) | [erebor/](erebor/) | Full, measured 2026-10-15: every one of the 145 pieces (size, pivot, slope, openings, LODs, collision body), the frame rule (the engine reads each Erebor FBX turned 180 degrees about Z from Blender's import), the join matrix tested in Blender and recipes taken from the authored prefabs |
| Erebor buildings | [erebor-buildings.md](erebor-buildings.md) | Whole buildings assembled from the Erebor kit and exported as one FBX each (base, `.lod3`, `.lod6`, `bo_`), #797: the pipeline, the export proof for the frame rule, the probe stack. Three buildings exported, probed and imported in the Kit 2026-10-15: `erebor_dwarf_house_01` (approved), `erebor_great_hall_01` and `erebor_barracks_01` |
| Campaign map icons | [map-icons.md](map-icons.md) | How a settlement's icon is drawn (a scene entity's child metamesh, shared per faction), the per-faction source folders, conventions for a new icon (visual-only FBX, 1K `t_<name>_{d,n,s}`), the Gundabad set as a budget reference, and the registry of custom icons: `carn_dum_town` (2026-10-16, rebuilt from solids on a Tripo layout; texturing in progress) |
| Mirkwood (elven) | — | Not catalogued yet (referenced in `taom_mirkwood_kitbash.xml`) |
| Gondor | [gondor/](gondor/) | Full, measured 2026-10-15 (the kit is referenced in `taom_gondor_kitbash.xml`): 330 kit parts (part families: sub-objects, LODs per sub-object, one `bo_` per prefix) and 166 props and references, the frame rule proved again (the Erebor 180 degrees about Z), 15 joins mined from 51,491 prefab placements and tested in Blender, the Lond Cirion rules re-checked and a proposed building list; [gondor/blender-build-prompt.md](gondor/blender-build-prompt.md) is the prompt behind it. 2026-07-28: added harbor ships `sm_gondor_ship_{cog,longship,war}_001` (Tripo AI sources, 1.9M→40k decimation + chart re-UV + high-to-low rebake) + `t_gondor_ship_<name>_{d,n,s}` under `Scenes/Gondor/ships/{,textures/}`; pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) § Single-prop path. |
| Gondor buildings | [gondor-buildings.md](gondor-buildings.md) | Six buildings (town house, townhouse, watch tower, barracks, hall, market arcade) assembled from the Gondor part families and exported in one FBX, `buildings_taom/gondor_buildings_a.fbx` (four tier objects each), 2026-10-15: recipes, probe results, shared faces judged per plane |
| Lond Cirion walls | [lond-cirion-walls.md](lond-cirion-walls.md) | Full — 8 ploppable sections (L, gatehouse straight, straight, kinks, gate court, sweep, the assembled ring with open siege frontage) composed programmatically from the Gondor castle L3 pieces; registration facts, the three composition laws, assembler workflow. |
| Lond Cirion buildings | [lond-cirion-buildings.md](lond-cirion-buildings.md) | Full — 2 pilot houses composed from the Gondor `meshes/` **part families** (panels, trims, roofs, columns, ground tiles) rather than whole pieces; the six placement rules the parts force (decal trap, bare-`.lod` trap, origin-anchored parts, gable seating, flush trims, skirt part choice), deliberate deviations, and the ray-probe verification stack. |
| Mordor | — | Not catalogued yet (referenced in `taom_mordor_kitbash.xml`). 2026-07-25: added `sm_mordor_mm_throne_001` (Witch-king throne, Tripo AI source, chart re-UV + rebake) + `t_mordor_mm_throne_{d,n,s}` — single-prop pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) § Single-prop path. Uses `t_`-prefix material naming like Rivendell/Tents. |
| Rivendell (elven) | (none) | Converted from the ElvenForestCity UE 5.1 kit (2026-07-15): 458 modular meshes + ~660 textures + 196 generated materials in `TAOM_Map/AssetSources/Scenes/Rivendell/`. Not catalogued yet. Pipeline: [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md). **Naming exception:** materials are named `t_rivendell_<set>` (the name of their texture set, user decision), with no `m_` prefix. **2026-11-24:** that folder is not on disk and no cooked package holds a kit mesh (`python tools/validate_mesh_refs.py --check-name sm_rivendell_gate_wall` reports the name free). The raw Unreal export is at `E:\LOTRAOMAssets\_export\rivendell` (460 FBX, 725 TGA), and the converter can run again from it; see [kingdom-coverage.md](kingdom-coverage.md). |
| Tents (culture-neutral) | — | Fab Medieval Tent Collection (2026-07-16): one kit FBX (`Scenes/Tents/tents_medieval_kit.fbx`, 9 tents + `bo_` twins, wood physics) + `t_tent_*` texture sets (white/clear canvas only). Wide-family + On_Sticks textures missing from the vault (pending re-download). Same `t_`-material naming as Rivendell. |
| Tiling texture sets | [tiling-texture-sets.md](tiling-texture-sets.md) | How a kingdom kit's 1024 px stone sets are made (in work since 2026-11-24): what the engine reads from `_d`, `_n` and `_s` (R metallic, G gloss, B occlusion; normal maps green down; detail normal, vertex colour and height options), the five stone sources tried and the one kept (Substance 3D Assets materials in Painter), the Painter and converter steps, the tile size table, the catalogue of the 35 downloaded materials |
| Rivendell siege set | [rivendell-siege-set.md](rivendell-siege-set.md) | Control records and build record for the pilot of the kingdom kits work (issue #1600): a wall in two, three and four bays, its breached state, two gatehouses and two wall towers on 1K tiling sets with a shared end section, so that any two pieces meet with no seam. In work since 2026-11-24, one piece at a time since 2026-11-25; the status table at the top of the record says where each piece stands. Nothing is in `AssetSources` or in the game yet. Textures: [tiling-texture-sets.md](tiling-texture-sets.md). |
| Kingdom kits | [kingdom-kits-prompt.md](kingdom-kits-prompt.md) | The session prompt of 2026-11-24 for settlement art for the kingdoms that have none of their own (Rohan, Rhûn, the Orcs, the Goblins, Harad, Khand, Dale, Rivendell): reuse the kit meshes, retexture them per kingdom and build what is missing, in six phases with four gates. Pilot chosen 2026-11-24: Rivendell, a siege set of a solid wall, a broken wall and a gatehouse, which grew to a wall in three lengths, its breached state, two gatehouses and two wall towers ([rivendell-siege-set.md](rivendell-siege-set.md)). |
| Kit layout standard | [kingdom-kit-layout.md](kingdom-kit-layout.md) | Where a kingdom's kit lives in the map module: the folders, the file names and the ten rules (one asset a piece is rule 10). |
| Lothlorien kit handover | [lothlorien-kit-handover.md](lothlorien-kit-handover.md) | The brief for a session that starts the Lothlorien kit: the method, the owner's way of work, the tools to reuse, the first steps. |
| Mirkwood kit handover | [mirkwood-kit-handover.md](mirkwood-kit-handover.md) | The brief for a session that improves the Mirkwood kit that exists: what is held, who depends on it, the first steps. |
| Scene needs of a settlement | [settlement-scene-needs.md](settlement-scene-needs.md) | What each scene needs from a kit: the solid wall, the broken wall, the towers, a gatehouse for two gates, the gates, the siege placement of both sides, the other scenes a settlement loads; every number with its source. Every kit handover names it. |
| Erebor gate handover | [erebor-gate-handover.md](erebor-gate-handover.md) | The brief for a session that builds the great gate of Erebor as one prefab with its mountain, siegeable, with carved detail: the engine's needs, what is held for Erebor, the maintainer's references, the first steps. |
| Erebor gate build & design | [erebor-gate.md](erebor-gate.md), [erebor-gate-design-language.md](erebor-gate-design-language.md) | Research, design language, Round 1 massing, Hero v4.2 build and staged delivery verification (118 FBX meshes, 33 1K textures, 48-entity prefab XML), and the 2026-11-27 owner review pause handover (mountain, broken wall craft, and siege envelopes kept; dwarven statues, dual broken-wall preview, and exact 1:1 facade fidelity prioritized next). Nothing is imported into `TAOM_Map`. |

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

Kit work (a design, a build, a retexture, a judgement of a piece) is briefed to the `kitbash-architect` agent
(`.claude/agents/kitbash-architect.md`). It holds the role and the order of work, and it names these catalogs as
its sources.

When you learn something new during a build iteration (pivot quirk,
dimension, rotation convention), update the relevant file immediately so
the knowledge persists to the next session.
