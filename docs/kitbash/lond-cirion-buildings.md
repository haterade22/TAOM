# Lond Cirion buildings — composing houses from Gondor part families

Sibling to [`lond-cirion-walls.md`](lond-cirion-walls.md). The walls kit assembles whole
*pieces* (`Scenes/Gondor/walls/*.fbx`); buildings assemble *part families*
(`Scenes/Gondor/meshes/*.fbx` — wall panels, trims, roofs, columns, ground tiles), which is a
different and trappier problem. Everything below is measured, not assumed.

Assembler: [`tools/oneoff/blender_assemble_lond_cirion_buildings.py`](../../tools/oneoff/blender_assemble_lond_cirion_buildings.py)
→ `Scenes/Gondor/blockout/lond_cirion_buildings_a.fbx`.
Parts index: `E:\LOTRAOMAssets\_export\lond_cirion\parts_catalog\` (JSON + numbered contact
sheets, from [`blender_catalog_parts.py`](../../tools/oneoff/blender_catalog_parts.py)).

## Why forward composition (not recipe recovery)

The shipped `Scenes/Gondor/buildings/*.fbx` are **merged component meshes** — `building.wall`,
`.floor`, `.roof`, `.brick` plus `.lod3`/`.lod6`/`bo_`/`_dest` siblings, 26–33 objects — not loose
part compositions. A signature matcher scored 0/26 against the part catalog
([`blender_rebuild_building.py`](../../tools/oneoff/blender_rebuild_building.py), 2026-07-29), so the
artists' recipes are unrecoverable. New buildings are composed forward from parts and joined into
the same 4-tier template.

## Sections

| Section | Footprint | Storeys | Roof | Base tris (lod3 / lod6 / bo) |
|---|---|---|---|---|
| `lond_cirion_house_01` | 6 × 6 m (7.04 outer, columns bind) | 2 × 3 m | 45°, ridge along X, z 9.03 | 41,240 (8,832 / 3,752 / 1,820) |
| `lond_cirion_house_02` | 12 × 6 m (13.04 outer) | 1 × 6 m | 26.57°, ridge along X, z 7.55 | 31,122 (12,754 / 2,614 / 1,176) |

Both: below-grade skirt to z −3, tiled floor flush at grade, ridge caps, eave strips, closed
gables. All eight tiers verified `tris == expected_sum` (the assembler asserts it and warns on
mismatch — this is what catches a silently-dropped sub-object).

## The six placement rules the parts force

1. **A part is a PREFIX, not an object.** `<p>.wall`, `<p>.stonebrick`, `<p>.door`… LODs are
   `.lodN` siblings **per sub-object**; collision is a single `bo_<p>`. Tiers are assembled per
   sub-object, so a tier ladder that misses a family's numbering silently keeps full-detail
   geometry. Ladder used: lod3 → `.lod3`, `.lod2`, `.lod4`; lod6 → `.lod6`, `.lod5`, `.lod4`,
   `.lod2` (the wall/roof sheets number 2/4/5; the trim sheets number 3/6).
   *A sub-object name can also be used as a prefix* (`gondor_ground_3m_a_normal.floor`) to select
   one component out of a group — the `bo_` lookup falls back to the parent's hull.
2. **DECAL TRAP.** A `.decalleak` sub-object can hang far below its part body —
   `gondor_wall_trim_6m_a`'s hangs **1.474 m** low. Since a part is anchored by its group bounding
   box bottom, including the decal threw house_02's whole cornice ring 1.54 m up to ridge level,
   where it hid the roof and read as a walled box. Decals are excluded from part groups entirely
   (104 catalog names contain `decal`; `gondor_wall_trim_3m_a` has the same trap).
3. **BARE `.lod` TRAP.** `gondor_roof_a_45_3m_side_a_clean`'s solid gable tympanum plate is named
   `.wall.lod` with **no plain `.wall`** sibling — an artist typo. A "reject anything containing
   `.lod`" filter drops it and leaves the gable end **50 % open** (4.5 m² void per end,
   see-through end to end). A trailing bare `.lod` counts as a base object; `.lodN` still does not.
4. **ORIGIN-ANCHORED PARTS.** The roof eave strips hang entirely in −y/−z from an origin at their
   top-inner corner — that corner *is* the eave line. The bbox-bottom anchor mis-seats them by
   ~0.13 m through the tile surface. `ANCHOR_ORIGIN` pins them by origin instead.
5. **GABLE SEATING.** Seat a gable group so its **12-tri infill plate's apex** lands on the ridge
   (the plate is the lowest-tri sub-object in both families). The coping then self-seats correctly
   for free: flush with the tile plane on the 45° family, 0.15 m proud on the 30° family, exactly
   as authored. Mirrored halves cross y = 0 by 3 cm so the apex closes.
6. **TRIMS STRADDLE, THEY DO NOT HANG.** Trims are authored symmetric about their local y — to sit
   *on* a wall plane. Centred on the wall centreline they project 0.15 m (slim) / 0.26 m (heavy), a
   string course. Hanging them off the wall face cantilevers them 0.70–0.81 m and pushes the
   footprint off the 3 m module grid (7.9 m on a 6 m house), inflating collision so two houses
   cannot butt.

Plus one part-choice rule: **the skirt uses the 3 m panel on both houses.** house_02's 6 m panel at
z −3 spans −3..+3 and z-fights the storey at 0..6. Only the 12-tri `.wall` box is placed — the
648-tri `.stonebrick` relief would be invisible underground.

## Deliberate deviations — do NOT "fix" these

* **Trims sit flush (0.15 m projection), by user instruction** after an in-editor check. An
  adversarial verifier measured the 0.79 m cantilever and argued it was correct cornice practice;
  the user's call overrides it. Flush also restores the module grid.
* **`gondor_roof_a_30_*` is 26.57°, not 30°** despite the name (1.5 rise / 3.0 run). The kit has no
  30° part; a true 30° would need a 1.155 Z-scale that stretches the tile texture.
* **No door leaf and no window glazing exist** in any of the 24 Gondor mesh families (`glass` = 0
  catalog hits; the `.door`/`.window` sub-objects are stone surround and stone tracery). Open
  apertures are kit parity, not a defect.
* **The export's non-identity node transform is not a bug.** Our FBXs reimport at rot X +90 /
  scale 0.01 with centimetre mesh data where shipped Gondor assets are identity/metres — but
  `lond_cirion_gatehouse_a.fbx` carries the identical transform and lands correctly in the Modding
  Kit, so the importer handles it. Do not touch `bake_space_transform` in any sibling exporter.

## Verification stack

The assembler logs per-tier `tris`, `expected_sum`, `dims_m`, `z_range`, then warns on any
mismatch, any part with no objects for a tier, and any sub-object with no authored LOD. It renders
5 framed ortho views + 2 three-quarters + a gable close-up per building to
`E:\LOTRAOMAssets\_export\lond_cirion\buildings\`.

Independent probe (read-only, run against the **exported** FBX — reusing the methods that found
each defect, because a log can only confirm what the script believed it did):

| Check | Method | Pass |
|---|---|---|
| Gable closure | rays across the gable triangle, inset 0.35 m | 21/21 and 15/15 blocked (was 16/23 open per end) |
| Floor | down-rays on a 0.5 m interior grid, base + `bo_` | 121/121 and 253/253 hit, level z −0.01 |
| No doubled wall | coincident-triangle scan | 56 / 32 duplicated positions (panel end-caps butting; a doubled skirt shows thousands) |
| Skirt | per-tier z_min | −3.000 on all 8 tiers |

The audit that produced these rules: 4 finder dimensions (numeric geometry, visual, part-choice
semantics, kit-template conformance) → 40 findings → 12 adversarial verifiers, 9 confirmed and 3
refuted. The three refutations are as valuable as the confirmations — see the deviations above.

## Known-open (accepted for a blockout)

* An open doorway lines up with a far-wall window on house_02, so you can see daylight through the
  building. With a floor placed this reads as a normal window on the far wall, and it is
  unavoidable in a hollow shell without authoring an interior.
* Adjacent wall panels' end caps land ~0.5 mm apart (perpendicular to the facade, visible only
  edge-on). Snapping placement coordinates to 1e-4 would remove it.
* Corner columns straddle the footprint corner, burying 0.5 m of each adjacent panel end. The kit's
  dedicated `gondor_column_corner_trim_a_clean` is authored asymmetric for one specific corner and
  would need per-corner rotation plus an origin anchor — 4.3× the tris for a blockout.
* The parts catalog covers 15 of the 24 mesh families. `gondor_ground_straight_a` (floors) had to be
  added to the assembler directly; re-running the catalog pass with the other nine would close it.
