# Mirkwood materials and textures

The installed source directory contains 85 verified PNG images in 30 map families.
They are all 2048 x 2048. There are 30 `_d`, 26 `_n`, 28 `_s` and
one `_d2` map. The table preserves the on-disk spellings. Three source slot names
need aliases to find their diffuse. The vertical dirt decal comes from the shared
`Scenes/Decals` source directory.

The inspected FBX has 32 material slots across all objects and 29 used at LOD0.
Distinct FBX names and map families reflect different authoring roles. The slot names
and use counts below come from the FBX. The review renders use diffuse maps and
alpha for vines and the decal. Installed material flags, normal-map channel sense,
and exact game shading were outside this Blender pass.

| LOD0 FBX slot | Diffuse used in review | LOD0 pieces |
| --- | --- | ---: |
| `t_decal_dirt_vertical` | `t_decal_dirt_vertical_a_d.png` | 10 |
| `t_mirkwood_bridge_stairs_unique` | `t_mirkwood_bridge_stairs_unique_d.png` | 3 |
| `t_mirkwood_bridge_tree_unique` | `t_mirkwood_bridge_tree_unique_d.png` | 1 |
| `t_mirkwood_column_baked` | `t_mirkwood_column_baked_d.png` | 3 |
| `t_mirkwood_decor_arches_a` | `t_mirkwood_decor_arches_a_d.png` | 7 |
| `t_mirkwood_gate_a` | `t_mirkwood_gate_a_d.png` | 1 |
| `t_mirkwood_lamps` | `t_mirkwood_lamps_d.png` | 2 |
| `t_mirkwood_slated_stone_clean` | `t_mirkwood_slated_stone_d.png` | 3 |
| `t_mirkwood_slated_stone_dirty` | `t_mirkwood_slated_stone_dirty_d.png` | 35 |
| `t_mirkwood_slated_stone_mossy` | `t_mirkwood__slated_stone_mossy_d.png` | 3 |
| `t_mirkwood_stone_clean` | `t_mirkwood_stone_d.png` | 22 |
| `t_mirkwood_stone_dirty` | `t_mirkwood_stone_dirty_d.png` | 26 |
| `t_mirkwood_stone_mossy` | `t_mirkwood_stone_mossy_d.png` | 4 |
| `t_mirkwood_tileable_brass` | `t_mirkwood_tileable_brass_d.png` | 9 |
| `t_mirkwood_tileable_roof_a` | `t_mirkwood_tileable_roof_a_d.png` | 12 |
| `t_mirkwood_tileable_roof_a_damaged` | `t_mirkwood_tileable_roof_a_damaged_d.png` | 9 |
| `t_mirkwood_tileable_wood` | `t_mirkwood_tileable_wood_d.png` | 20 |
| `t_mirkwood_trimsheet_stone` | `t_mirkwood_trimsheet_stone_d.png` | 23 |
| `t_mirkwood_trimsheet_wood` | `t_mirkwood_trimsheet_wood_d.png` | 7 |
| `t_mirkwood_unique_bakes_a` | `t_mirkwood_unique_bakes_a_d.png` | 5 |
| `t_mirkwood_unique_bakes_b` | `t_mirkwood_unique_bakes_b_d.png` | 3 |
| `t_mirkwood_unique_bakes_c` | `t_mirkwood_unique_bakes_c_d.png` | 2 |
| `t_mirkwood_vines_clean` | `t_mirkwood_vines_clean_d.png` | 8 |
| `t_mirkwood_vines_mossy` | `t_mirkwood_vines_mossy_d.png` | 8 |
| `t_mirkwood_vines_rotten` | `t_mirkwood_vines_rotten_d.png` | 8 |
| `t_mirkwood_wood_bark_a` | `t_mirkwood_wood_bark_a_d.png` | 4 |
| `t_mirkwood_wood_bark_b` | `t_mirkwood_wood_bark_b_d.png` | 4 |
| `t_mirkwood_wood_path_a` | `t_mirkwood_wood_path_a_d.png` | 4 |
| `t_mirkwood_wood_path_b` | `t_mirkwood_wood_path_b_d.png` | 4 |

## Source map families

| Filename stem | Channels on disk |
| --- | --- |
| `t_mirkwood__slated_stone_mossy` | `_d`, `_n`, `_s` |
| `t_mirkwood_bridge_stairs_unique` | `_d`, `_n`, `_s` |
| `t_mirkwood_bridge_tree_unique` | `_d`, `_n`, `_s` |
| `t_mirkwood_column_baked` | `_d`, `_n`, `_s` |
| `t_mirkwood_decor_arches_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_gate_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_lamps` | `_d`, `_d2`, `_n`, `_s` |
| `t_mirkwood_slated_stone` | `_d`, `_n`, `_s` |
| `t_mirkwood_slated_stone_dirty` | `_d`, `_n`, `_s` |
| `t_mirkwood_stone` | `_d`, `_n`, `_s` |
| `t_mirkwood_stone_dirty` | `_d`, `_n`, `_s` |
| `t_mirkwood_stone_mossy` | `_d`, `_n`, `_s` |
| `t_mirkwood_tileable_brass` | `_d`, `_n`, `_s` |
| `t_mirkwood_tileable_roof_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_tileable_roof_a_damaged` | `_d`, `_n`, `_s` |
| `t_mirkwood_tileable_wood` | `_d`, `_n`, `_s` |
| `t_mirkwood_trimsheet_stone` | `_d`, `_n`, `_s` |
| `t_mirkwood_trimsheet_wood` | `_d`, `_n`, `_s` |
| `t_mirkwood_unique_bakes_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_unique_bakes_b` | `_d`, `_n`, `_s` |
| `t_mirkwood_unique_bakes_c` | `_d`, `_n`, `_s` |
| `t_mirkwood_vines_clean` | `_d`, `_n`, `_s` |
| `t_mirkwood_vines_mossy` | `_d`, `_s` |
| `t_mirkwood_vines_rotten` | `_d`, `_s` |
| `t_mirkwood_wood_bark_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_wood_bark_b` | `_d` |
| `t_mirkwood_wood_bark_c` | `_d`, `_n`, `_s` |
| `t_mirkwood_wood_bark_d` | `_d`, `_n`, `_s` |
| `t_mirkwood_wood_path_a` | `_d`, `_n`, `_s` |
| `t_mirkwood_wood_path_b` | `_d` |

The source already combines repeated stone, wood and roof surfaces with trim
sheets and local bakes. Preserve that split when comparing a retexture with a
shape change. Before treating a map named `unique` as a tiling surface,
inspect its image and audit the corresponding UV islands.
