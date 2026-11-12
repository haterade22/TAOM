# Campaign map icons

What draws a settlement on the campaign map, where icon sources live, the conventions a new icon
follows, and a registry of the custom icons TAOM has made. The procedure for a new icon from a Tripo
model, decimated or rebuilt, is the [`/prep-tripo-prop`](../../.claude/skills/prep-tripo-prop/SKILL.md)
skill. Any quad-retopology stage first follows the
[required toolkit selection](../features/cube-map-remesh.md#required-workflow-selection).

## How an icon is drawn

A settlement entity in `TAOM_Map/SceneObj/Main_map/scene.xscene` (named by its id, for example
`town_G3`) has a child entity whose `meta_mesh_component` names the icon metamesh. Settlement data
has no icon field: the `map_icon` attribute has no managed reader
([settlements.md](../modding/settlements.md)). The entity also carries the walls, `town_gate`,
`map_icon_siege` slots and its own `bo_town` collision, so an icon mesh needs no `bo_`.

A faction metamesh is shared. `gundabad_town` draws three settlement entities (2026-10-16:
`rg -c 'meta_mesh_component name="gundabad_town"'` on the scene file), so giving one settlement its
own icon means pointing only that entity's child at a new metamesh. Re-importing over a shared
metamesh changes every settlement that uses it.

## Where sources live

`TAOM_Map/AssetSources/Campaign Map/Map Icons/<faction>/`, one folder per faction (Dale, Dol Guldur,
Dunland, Erebor, Gondor, Gundabad, Isengard, Lothlorien, Mirkwood, Mordor, Others, Rhun, Rivendell,
Rohan, Umbar): the FBX at the top, textures in `textures/`. TAOM_Map is unversioned, so a reinstall
removes these files; the staging folder in the registry below is the source of truth, and the skill
rebuilds an icon from it.

## Conventions for a new icon

- **Mesh:** visual only, no `bo_` (the shipped `gundabad icons.fbx` has none; every town entity
  carries its own `bo_town`). On the remesh or decimate route the resulting `<name>_low.fbx` is
  that file, copied to the faction folder as `<name>.fbx`; on the rebuild route it is the unwrap's
  `uv\<name>.fbx`.
- **Budget:** measure the faction's existing icons first (`tools/blender/probe_fbx.py`). The
  Gundabad set, as a reference:

  | Mesh | Triangles | Size (m) |
  |---|---|---|
  | `gundabad_town` | 91,089 | 60.5 × 62.4 × 29.2 |
  | `gundabad_castle_3` | 23,249 | 30.4 × 34.8 × 29.5 |
  | `gundabad_map_fortress` | 11,449 | 75.9 × 27.0 × 36.1 |
  | `gundabad_castle_2` | 9,158 | 30.1 × 34.5 × 26.0 |
  | `gundabad_village_warg_pit` | 5,874 | 6.6 × 6.1 × 1.2 |
  | `gundabad_village_camp` | 4,711 | 5.4 × 5.0 × 1.5 |

  The icon child carries scale 0.1 and the three Gundabad town entities 1.5 (`scene.xscene`), so an
  icon draws at 0.15 of its modelled size; a 60 m model is the size of `gundabad_town`.
- **Textures:** 1K at most for a new icon (Mike, 2026-10-16), packed `t_<name>_{d,n,s}`; an icon
  with several texture sets gets one triple per set, and a small set (iron, openings) can be 256.
  The shipped faction sets use `<faction>_<set>_{d,h,n,s}` at 2K; a new icon does not need an `_h`.
- **Materials:** one Kit material per FBX material, named exactly as it (the Kit binds by name
  module-wide), with `use_specular` on: without it the shader never reads `_s`
  (`standart.rsh`, `gbuffer_functions.rsh`). `_s` blue is ambient occlusion: it dims ambient light
  and the sun's specular, not the sun's diffuse (`pbr_shading_functions.rsh`).
- **Frame:** the FBX reads back in Blender at rotation X 90 degrees and scale 0.01 (centimetre mesh
  data); `carn_dum_town.fbx` probed that way on 2026-10-16. That is the Kit's proven Y-up format
  ([lond-cirion-buildings.md](lond-cirion-buildings.md), [gondor/design-patterns.md](gondor/design-patterns.md)):
  in the Kit it lands upright, turned 180 degrees about Z, which the entity's rotation absorbs.

## Rebuilding an icon from solids

A Tripo model is a sculpt: rounded corners and dripping walls. Decimated to 25,000 or 90,000
triangles, the Carn Dûm model read as melted wax in the Kit, so it was rebuilt instead
(2026-10-16). The hand-made Gondor icons are box-modelled the same way (`minas_tirith`, 63,597
triangles of flat planes and hard edges).

1. **Measure the Tripo layout** with `tools/blender/measure_tripo_layout.py` (height profile, tower
   peaks) and write it into a builder script's layout table; `tools/oneoff/blender_build_carn_dum_icon.py`
   is the pattern. Every face carries a material slot and a `part` attribute (towers, walls, keep
   and so on), and no face runs down into the solid that hides it.
2. **Spend the budget where the map shows it.** The icon draws LOD0, so Mike set the whole 90,000
   triangles; walls stay plain, the towers, keep and gate carry the detail.
3. **Unique UVs:** `tools/blender/unwrap_texture_sets.py` with a rules file
   (`tools/oneoff/carn_dum_texture_sets.json`) splits the faces into texture sets, gives hidden faces
   almost no space and fails on inverted faces. Carn Dûm v11: towers, walls, town and paving at 1K
   (10.9 to 15.2 px per model metre on visible faces), roof 512, iron and dark 256. An estimate from
   the map camera's constants (`MapCameraView`: 40 degree vertical field of view, the icon at 0.15
   scale), not measured in game: that matches a 1080p screen at the default camera distance (about
   13 px per model metre) and is about four times coarser than full zoom (about 50).
4. **Painter:** `painter_new_project.py` with `low_as_high` and the unwrap's report bakes AO,
   curvature, position, thickness and world-space normals off the mesh itself;
   `painter_stacks.py` paints each set; `tools/blender/render_painter_preview.py` renders the
   export, lit or `--albedo`.
5. **Convert and hand off** as in the skill: one triple and one Kit material per texture set.

What 1K shows on a 55 m icon: features under 2 texels (about 18 cm) vanish, height relief tilts the
normal map instead of adding detail, and worn edges, soot, streaks and stone courses do not read
(Mike, 2026-10-16). Colour, broad tone and the baked occlusion do.

## Kysaro's measurements and authoring traps

From Kysaro's notes under `map-icons/` (`project_map_icons_research.md`, 2026-10-24; `project_dwarf_mapicons.md`,
2026-10-29), unverified in TAOM. The triangle budget stays Mike's decision (he set the whole 90,000 triangles for the
Carn Dûm rebuild, step 2 of "Rebuilding an icon from solids"); the numbers below are data, not a new budget.

- **Native baseline, measured in the Meta Mesh Editor:** `map_castle_a` 1,261 polys, `map_castle_d` 1,978, `map_town_a`
  1,272. One material each, shared per culture (the `fake_houses` and `steppe_fake_houses` atlases), one LOD, visible
  distance `FLT_MAX`. That visible distance disagrees with the "map icons 200" in
  [mission-frame-threads-and-native-costs.md](../reference/engine/mission-frame-threads-and-native-costs.md) section 7.
- **TAOM's icons, measured by his scratch script over `TAOM_Map/AssetSources/Campaign Map/Map Icons`:** 3 of 28 FBX files
  carry LODs. Mordor has 54 icons, a median (p50) of 31k triangles, a maximum of 194k and 2.16M in all. Materials per
  faction: Mirkwood 22, Gundabad 19, Mordor 14.
- **His own working ceilings are far lower:** village 0.5 to 1k, castle and town about 3 to 4k, landmark about 6 to 8k
  triangles, one material per faction, no LODs. They differ from the 90,000 above and from the Gundabad set in the
  budget bullet; reconciling them is Mike's call.
- **Traps:**
  - A battered (sloped) wall covers a door decal in game. Stand the door in front of the batter. This caused the "bricks
    on the doors" bug on his Lindon, Angmar, Khand and Rhun icons, fixed 2026-10-29.
  - A metallic `_s` map over a dark albedo renders near black (his copper domes). Leave metal off coloured roofs.
  - A thin two-sided surface needs offset front and back faces (his builder helper `Kit.face2`). `face(n)` plus
    `face(-n)` merged into one-sided sails.

## Custom icons

| Icon | Settlement | Source | Kit files | Staging (source of truth) | State |
|---|---|---|---|---|---|
| `carn_dum_town` | `town_G3` Carn Dûm (Gundabad) | Rebuilt from solids (`blender_build_carn_dum_icon.py` v11) on the layout of Mike's Tripo model: 83,797 triangles, 54.8 × 55.9 × 40.8 m, 7 texture sets (unique UVs); Painter stone colour, tone and baked occlusion | Obsolete Tripo version still there: `Gundabad/carn_dum_town.fbx` (25,000 triangles) and `Gundabad/textures/t_carn_dum_town_{d,n,s}.png`, copied 2026-10-16 and imported by Mike the same day (five packages in `Assets/Campaign Map/Map Icons/Gundabad/`: `carn_dum_town_geo.tpac`, `textures/t_carn_dum_town_mtl.tpac` and `_{d,n,s}_tex.tpac`), not placed: the map scene does not name it. The rebuild is not copied yet | `E:\LOTRAOMAssets\_export\carn_dum_rebuild\v11\` (builder `.blend` and renders, `uv\carn_dum_town.fbx` and `_uv.blend`); the round 14 paint is on v10 (`v10\carn_dum_fortress.spp`, `v10\paint_r14\`); the Tripo version in `carn_dum_icon_final\` | Texturing in progress; Mike's look so far: stone colour, broad tone and occlusion, no snow, edges, soot, streaks or courses. Next: a Painter project from v11 (its unwrap gives the town set 11.9 px/m, v10 had 9.7), every set painted as stone (round 14 still had the paving lighter), conversion. Decisions owed first: the `carn_dum_town` set converts to `t_carn_dum_town_*`, the Tripo version's names (rename the set or replace the Tripo files); occlusion in the albedo or in `_s` blue, not both (`_s` dims ambient light again); whether 1K is enough, being about four times coarser than full zoom. Then the Kit copy (backup of the Tripo FBX), 7 materials with `use_specular`, the `town_G3` child swap (`Gundabad`, now `gundabad_town`) and the in-game check |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/README.md](./README.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md)

<!-- backlinks-end -->
