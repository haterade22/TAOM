# Mirkwood meshes

All 136 source base pieces use a numeric `.lod0` suffix. Dimensions are Blender
world bounds in metres. All 136 source LOD0 origins are `(0, 0, 0)`. The pivot
column describes the origin against the bounds on X, Y and Z. The full measurements
for all 760 mesh objects, including bounds, source transforms and UV layers, are in
the local `geometry.json` named in the [catalog](README.md).

Triangles are from the FBX. The LOD column lists levels present in the source. The
body column names the physics shape referenced by the singleton prefab, with its source
triangle count if present. Production placements expand installed TAOM_Map prefabs.
Slot densities use the active FBX UV layer and the ratio of total UV area to world
surface area for that slot. Values assume 2048 px and apply to the source geometry.
Atlas, trim and repeat mapping can make local density differ substantially.

## Stone arcades

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_arch_a01` | 9.000 x 0.901 x 5.000 | mid / mid / min | L0 3,188, L2 2,550, L3 1,594, L4 796, L5 478, L6 318 | `bo_st_mirkwood_arch_a01` (440) | `t_mirkwood_trimsheet_stone` (968)<br>`t_mirkwood_slated_stone_dirty` (684) | 178 |
| `st_mirkwood_arch_a02_centerpiece` | 9.011 x 0.854 x 4.892 | mid / mid / min | L0 3,864, L2 3,482, L4 2,642, L5 1,854, L6 660 | `bo_st_mirkwood_arch_a02_centerpiece` (290) | `t_mirkwood_trimsheet_stone` (276)<br>`t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683)<br>`t_mirkwood_stone_dirty` (667) | 12 |
| `st_mirkwood_arch_a02_centerpiece_column_curved` | 1.208 x 1.044 x 4.108 | mid / mid / min | L0 262, L2 179, L4 149, L6 80 | `bo_st_mirkwood_arch_a02_centerpiece_column_dirty` (30) | `t_mirkwood_trimsheet_stone` (267)<br>`t_mirkwood_stone_clean` (671)<br>`t_mirkwood_slated_stone_dirty` (683)<br>`t_mirkwood_stone_dirty` (686) | 0 |
| `st_mirkwood_arch_a02_centerpiece_column_dirty` | 1.278 x 0.844 x 4.108 | mid / mid / min | L0 296, L2 256, L4 196, L6 98 | `bo_st_mirkwood_arch_a02_centerpiece_column_dirty` (30) | `t_mirkwood_trimsheet_stone` (267)<br>`t_mirkwood_stone_dirty` (671)<br>`t_mirkwood_slated_stone_dirty` (689) | 0 |
| `st_mirkwood_arch_a02_centerpiece_cover` | 1.423 x 0.979 x 0.291 | mid / mid / min | L0 252, L2 200, L3 126, L4 62, L5 36, L6 24 | `bo_st_mirkwood_arch_a02_centerpiece_cover` (36) | `t_mirkwood_stone_dirty` (670) | 14 |
| `st_mirkwood_arch_a02_centerpiece_curved` | 7.821 x 4.934 x 4.892 | mid / mid / min | L0 2,904, L2 1,554, L4 1,130, L6 564 | `bo_st_mirkwood_arch_a02_centerpiece_curved` (290) | `t_mirkwood_trimsheet_stone` (276)<br>`t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683)<br>`t_mirkwood_stone_dirty` (658) | 0 |
| `st_mirkwood_arch_a02_centerpiece_dirty` | 9.011 x 0.854 x 4.892 | mid / mid / min | L0 3,864, L2 3,482, L4 2,642, L5 1,854, L6 660 | `bo_st_mirkwood_arch_a02_centerpiece_dirty` (290) | `t_mirkwood_trimsheet_stone` (276)<br>`t_mirkwood_stone_dirty` (680)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_arch_a02_corner` | 4.503 x 0.854 x 4.892 | mid / mid / min | L0 1,984, L2 1,173, L4 709, L6 353 | `bo_st_mirkwood_arch_a02_corner` (152) | `t_mirkwood_trimsheet_stone` (276)<br>`t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683)<br>`t_mirkwood_stone_dirty` (667) | 96 |
| `st_mirkwood_arch_a02_corner_column` | 0.639 x 0.844 x 4.108 | mid / mid / min | L0 152, L2 112, L6 55 | `bo_st_mirkwood_arch_a02_corner_column` (15) | `t_mirkwood_trimsheet_stone` (267)<br>`t_mirkwood_stone_clean` (677)<br>`t_mirkwood_slated_stone_dirty` (683)<br>`t_mirkwood_stone_dirty` (690) | 10 |
| `st_mirkwood_arch_a02_corner_column_dirty` | 0.639 x 0.844 x 4.108 | mid / mid / min | L0 152, L2 112, L6 55 | `bo_st_mirkwood_arch_a02_corner_column_dirty` (40) | `t_mirkwood_trimsheet_stone` (267)<br>`t_mirkwood_stone_dirty` (677)<br>`t_mirkwood_slated_stone_dirty` (689) | 0 |
| `st_mirkwood_arch_a02_corner_cover` | 0.765 x 1.052 x 0.313 | mid / mid / min | L0 156, L2 124, L3 78, L4 38, L5 22, L6 14 | `bo_st_mirkwood_arch_a02_corner_cover` (22) | `t_mirkwood_stone_dirty` (635) | 0 |
| `st_mirkwood_arch_a02_corner_dirty` | 4.503 x 0.854 x 4.892 | mid / mid / min | L0 1,984, L2 1,004, L4 823, L6 444 | `bo_st_mirkwood_arch_a02_corner_dirty` (152) | `t_mirkwood_trimsheet_stone` (276)<br>`t_mirkwood_stone_dirty` (683)<br>`t_mirkwood_slated_stone_dirty` (669) | 0 |
| `st_mirkwood_arch_a03_column_centerpiece` | 7.001 x 2.670 x 3.734 | mid / mid / min | L0 1,488, L2 1,189, L3 743, L4 371, L6 144 | `bo_st_mirkwood_arch_a03_column_centerpiece` (64) | `t_mirkwood_trimsheet_stone` (278)<br>`t_mirkwood_stone_dirty` (661) | 0 |
| `st_mirkwood_arch_a03_column_left` | 1.773 x 1.058 x 7.598 | mid / mid / min | L0 60, L2 48, L3 30, L4 31, L6 6 | `bo_st_mirkwood_arch_a03_column_left` (6) | `t_mirkwood_trimsheet_stone` (263)<br>`t_mirkwood_stone_dirty` (1569) | 0 |
| `st_mirkwood_arch_a03_column_right` | 1.773 x 1.058 x 7.598 | mid / mid / min | L0 62, L2 31, L3 31, L4 32, L6 6 | `bo_st_mirkwood_arch_a03_column_right` (6) | `t_mirkwood_trimsheet_stone` (263)<br>`t_mirkwood_stone_dirty` (1611) | 0 |
| `st_mirkwood_arch_a04_centerpiece` | 2.342 x 0.881 x 10.012 | mid / mid / min | L0 762, L2 608, L3 380, L4 190, L5 114, L6 76 | `bo_st_mirkwood_arch_a04_centerpiece` (114) | `t_mirkwood_slated_stone_dirty` (675)<br>`t_mirkwood_trimsheet_stone` (357) | 0 |
| `st_mirkwood_arch_a04_extension_a` | 1.716 x 0.551 x 3.418 | mid / mid / min | L0 218, L2 174, L3 108, L4 53, L5 31, L6 20 | `bo_st_mirkwood_arch_a04_extension_a` (31) | `t_mirkwood_slated_stone_dirty` (665)<br>`t_mirkwood_trimsheet_stone` (640) | 0 |
| `st_mirkwood_arch_a04_extension_b` | 1.716 x 0.551 x 3.401 | mid / mid / min | L0 218, L2 174, L3 108, L4 53, L5 31, L6 21 | `bo_st_mirkwood_arch_a04_extension_b` (31) | `t_mirkwood_slated_stone_dirty` (665)<br>`t_mirkwood_trimsheet_stone` (640) | 0 |
| `st_mirkwood_arch_a04_extension_c` | 0.482 x 0.752 x 5.368 | mid / mid / min | L0 198, L2 158, L3 99, L4 49, L5 29, L6 19 | `bo_st_mirkwood_arch_a04_extension_c` (29) | `t_mirkwood_slated_stone_dirty` (676)<br>`t_mirkwood_trimsheet_stone` (621) | 0 |
| `st_mirkwood_arch_a05` | 5.085 x 1.491 x 2.313 | mid / mid / min | L0 2,136, L2 1,708, L3 1,067, L4 534, L5 319, L6 213 | `bo_st_mirkwood_arch_a05` (49) | `t_mirkwood_trimsheet_stone` (373)<br>`t_mirkwood_slated_stone_dirty` (647)<br>`t_mirkwood_decor_arches_a` (320) | 0 |
| `st_mirkwood_arch_a6` | 5.592 x 1.926 x 2.313 | mid / mid / min | L0 2,136, L2 1,708, L3 1,067, L4 533, L5 319, L6 213 | `bo_st_mirkwood_arch_a6` (49) | `t_mirkwood_trimsheet_stone` (354)<br>`t_mirkwood_slated_stone_dirty` (636)<br>`t_mirkwood_decor_arches_a` (331) | 0 |

## Arcade columns

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_arch_column_double_a01` | 2.920 x 0.523 x 9.839 | mid / mid / min | L0 3,928, L2 3,142, L3 1,963, L4 982, L5 588, L6 391 | `bo_st_mirkwood_arch_column_double_a01` (588) | `t_mirkwood_slated_stone_dirty` (543)<br>`t_mirkwood_trimsheet_stone` (761)<br>`t_mirkwood_stone_dirty` (681)<br>`t_mirkwood_tileable_brass` (676) | 0 |
| `st_mirkwood_arch_column_single_a01` | 0.523 x 0.523 x 6.694 | mid / mid / min | L0 1,380, L2 1,104, L3 690, L4 344, L5 206, L6 137 | `bo_st_mirkwood_arch_column_single_a01` (206) | `t_mirkwood_slated_stone_dirty` (543)<br>`t_mirkwood_trimsheet_stone` (761)<br>`t_mirkwood_stone_dirty` (681) | 0 |

## Stone bridges

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_bridge_a01` | 10.520 x 3.803 x 3.187 | mid / mid / min | L0 1,848, L2 1,478, L3 923, L4 462, L5 276, L6 184 | `bo_st_mirkwood_bridge_a01` (183) | `t_mirkwood_stone_dirty` (682)<br>`t_mirkwood_trimsheet_stone` (365) | 6 |
| `st_mirkwood_bridge_a01_broken` | 5.749 x 3.805 x 3.187 | mid / mid / min | L0 1,314, L2 1,050, L3 656, L4 328, L5 197, L6 131 | `bo_st_mirkwood_bridge_a01_broken` (131) | `t_mirkwood_stone_dirty` (511)<br>`t_mirkwood_slated_stone_dirty` (495)<br>`t_mirkwood_trimsheet_stone` (359) | 0 |
| `st_mirkwood_bridge_a02_extension` | 1.998 x 2.000 x 1.285 | mid / mid / min | L0 28, L2 22 | `bo_st_mirkwood_bridge_a02_extension` (22) | `t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_bridge_a02_staircase` | 6.592 x 2.002 x 6.047 | mid / mid / min | L0 404, L2 322, L3 202, L4 100, L5 60, L6 40 | `bo_st_mirkwood_bridge_a02_staircase` (40) | `t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683) | 48 |
| `st_mirkwood_bridge_a03` | 7.664 x 4.762 x 11.014 | mid / mid / 0.01 | L0 25,268, L2 20,214, L3 12,633, L4 6,317, L5 3,789, L6 2,526 | `bo_st_mirkwood_bridge_a03` (2,526) | `t_mirkwood_slated_stone_dirty` (681)<br>`t_mirkwood_bridge_tree_unique` (199)<br>`t_mirkwood_bridge_stairs_unique` (303)<br>`t_mirkwood_stone_dirty` (672) | 0 |
| `st_mirkwood_bridge_a04` | 8.253 x 2.381 x 16.016 | mid / mid / min | L0 1,498, L2 1,198, L3 748, L4 374, L5 224, L6 148 | `bo_st_mirkwood_bridge_a04` (148) | `t_mirkwood_stone_clean` (680)<br>`t_mirkwood_slated_stone_dirty` (681) | 0 |
| `st_mirkwood_bridge_a04_extension` | 1.998 x 2.000 x 1.285 | mid / mid / min | L0 28, L2 22, L4 6 | `bo_st_mirkwood_bridge_a04_extension` (6) | `t_mirkwood_stone_clean` (683)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_bridge_a04_extension_top` | 2.000 x 2.381 x 1.781 | mid / mid / min | L0 64, L2 51, L4 32 | `bo_st_mirkwood_bridge_a04_extension_top` (32) | `t_mirkwood_stone_clean` (712)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_bridge_a05` | 8.391 x 3.175 x 16.016 | mid / mid / min | L0 7,209, L2 5,767, L3 3,604, L4 1,802, L5 1,080, L6 720 | `bo_st_mirkwood_bridge_a05` (719) | `t_mirkwood_stone_clean` (680)<br>`t_mirkwood_slated_stone_dirty` (681) | 0 |
| `st_mirkwood_bridge_a05_extension` | 2.000 x 2.381 x 10.884 | mid / mid / min | L0 1,892, L2 1,512, L3 946, L4 472, L5 283, L6 189 | `bo_st_mirkwood_bridge_a05_extension` (188) | `t_mirkwood_stone_clean` (689)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |

## Carved columns

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_column_a02` | 1.776 x 1.776 x 6.820 | mid / mid / min | L0 160, L4 80, L6 40 | `bo_st_mirkwood_column_a02` (24) | `t_mirkwood_decor_arches_a` (206) | 0 |
| `st_mirkwood_column_a02_arch` | 4.119 x 1.776 x 4.537 | mid / mid / min | L0 448, L4 112, L5 66, L6 43 | `bo_st_mirkwood_column_a02_arch` (66) | `t_mirkwood_decor_arches_a` (206) | 0 |
| `st_mirkwood_column_a02_extension` | 1.305 x 1.305 x 2.028 | mid / mid / min | L0 32, L4 16 | `bo_st_mirkwood_column_a02_extension` (4) | `t_mirkwood_decor_arches_a` (233) | 0 |
| `st_mirkwood_column_a03_left_arch` | 3.888 x 1.776 x 4.518 | mid / mid / min | L0 448, L5 224, L6 32 | `bo_st_mirkwood_column_a03_left_arch` (32) | `t_mirkwood_decor_arches_a` (205) | 0 |
| `st_mirkwood_column_a03_right_arch` | 3.888 x 1.776 x 4.518 | mid / mid / min | L0 448, L5 224, L6 128 | `bo_st_mirkwood_column_a03_right_arch` (128) | `t_mirkwood_decor_arches_a` (205) | 0 |

## Eaves and canopies

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_eave_a01` | 5.933 x 7.090 x 3.557 | mid / 0.49 / min | L0 3,056, L2 2,444, L4 1,528, L5 764, L6 304 | `bo_st_mirkwood_eave_a01` (486) | `t_mirkwood_unique_bakes_c` (262) | 0 |
| `st_mirkwood_eave_a02` | 4.101 x 4.000 x 5.921 | mid / mid / min | L0 1,924, L2 1,538, L3 962, L4 480, L5 288, L6 192 | `bo_st_mirkwood_eave_a02` (288) | `t_mirkwood_trimsheet_wood` (459)<br>`t_mirkwood_tileable_roof_a` (738)<br>`t_mirkwood_tileable_wood` (553) | 0 |
| `st_mirkwood_eave_a02_extension_a` | 3.003 x 3.963 x 2.395 | mid / mid / min | L0 612, L2 488, L3 306, L4 152, L5 90 | `bo_st_mirkwood_eave_a02_extension_a` (90) | `t_mirkwood_trimsheet_wood` (466)<br>`t_mirkwood_tileable_roof_a` (741)<br>`t_mirkwood_tileable_wood` (635) | 0 |
| `st_mirkwood_eave_a02_extension_b` | 3.003 x 3.963 x 2.395 | mid / mid / min | L0 808, L2 646, L3 404, L4 202, L5 120 | `bo_st_mirkwood_eave_a02_extension_b` (120) | `t_mirkwood_tileable_roof_a` (720)<br>`t_mirkwood_tileable_wood` (690)<br>`t_mirkwood_trimsheet_wood` (479) | 0 |
| `st_mirkwood_eave_a03` | 4.101 x 4.000 x 5.921 | mid / mid / min | L0 1,924, L2 1,538, L3 962, L4 480, L5 288, L6 192 | `bo_st_mirkwood_eave_a03` (288) | `t_mirkwood_trimsheet_wood` (459)<br>`t_mirkwood_tileable_roof_a_damaged` (738)<br>`t_mirkwood_tileable_wood` (553) | 0 |
| `st_mirkwood_eave_a03_extension_a` | 3.003 x 3.963 x 2.395 | 0.55 / 0.39 / min | L0 612, L2 488, L3 306, L4 152, L5 90 | `bo_st_mirkwood_eave_a03_extension_a` (90) | `t_mirkwood_trimsheet_wood` (466)<br>`t_mirkwood_tileable_roof_a_damaged` (741)<br>`t_mirkwood_tileable_wood` (635) | 0 |
| `st_mirkwood_eave_a03_extension_b` | 3.003 x 3.963 x 2.395 | mid / mid / min | L0 808, L2 646, L3 404, L4 202, L5 120 | `bo_st_mirkwood_eave_a03_extension_b` (120) | `t_mirkwood_tileable_roof_a_damaged` (720)<br>`t_mirkwood_tileable_wood` (690)<br>`t_mirkwood_trimsheet_wood` (479) | 0 |
| `st_mirkwood_eave_column_a01` | 0.307 x 0.318 x 3.423 | mid / mid / min | L0 44, L2 34 | `bo_st_mirkwood_eave_column_a01` (12) | `t_mirkwood_trimsheet_wood` (703) | 0 |

## Entrance bridges

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_entrance_bridge_a01` | 5.584 x 20.367 x 9.611 | mid / mid / min | L0 4,068, L2 3,254, L4 1,662, L5 880, L6 408 | `bo_st_mirkwood_entrance_bridge_a01` (176) | `t_mirkwood_stone_clean` (272)<br>`t_mirkwood_stone_dirty` (164)<br>`t_decal_dirt_vertical` (410) | 1 |
| `st_mirkwood_entrance_bridge_a01_extension` | 5.318 x 6.547 x 9.611 | mid / mid / min | L0 1,110, L2 555, L4 465, L5 246, L6 138 | `bo_st_mirkwood_entrance_bridge_a01_extension` (30) | `t_mirkwood_stone_clean` (274)<br>`t_decal_dirt_vertical` (402) | 0 |

## Floor blocks

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_floor_base_a01_clean` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_a01_clean` (12) | `t_mirkwood_stone_clean` (683) | 0 |
| `st_mirkwood_floor_base_a01_dirty` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_a01_dirty` (12) | `t_mirkwood_stone_dirty` (683) | 0 |
| `st_mirkwood_floor_base_a01_mossy` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_a01_mossy` (12) | `t_mirkwood_stone_mossy` (683) | 0 |
| `st_mirkwood_floor_base_b01_clean` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_b01_clean` (12) | `t_mirkwood_slated_stone_clean` (683) | 0 |
| `st_mirkwood_floor_base_b01_dirty` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_b01_dirty` (12) | `t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_floor_base_b01_mossy` | 3.000 x 3.000 x 1.000 | mid / mid / min | L0 12 | `bo_st_mirkwood_floor_base_b01_mossy` (12) | `t_mirkwood_slated_stone_mossy` (683) | 0 |

## Gate leaf

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_gate_door_a01` | 4.827 x 0.260 x 10.151 | mid / mid / min | L0 148, L2 118, L3 74, L4 36, L5 22, L6 14 | `bo_st_mirkwood_gate_door_a01` (32) | `t_mirkwood_gate_a` (190) | 0 |

## House doors

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_house_doors_a01` | 1.445 x 0.063 x 2.995 | mid / mid / min | L0 78, L4 46, L6 30 | `bo_st_mirkwood_house_doors_a01` (30) | `t_mirkwood_unique_bakes_a` (512) | 0 |
| `st_mirkwood_house_doors_a02` | 1.445 x 0.063 x 2.995 | mid / mid / min | L0 78, L4 46, L6 30 | `bo_st_mirkwood_house_doors_a02` (30) | `t_mirkwood_unique_bakes_b` (486) | 0 |

## Ornamental columns

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_ornamental_column_a01_bottom` | 2.443 x 2.443 x 4.000 | mid / mid / min | L0 128, L4 80, L6 53 | `bo_st_mirkwood_ornamental_column_a01_bottom` (19) | `t_mirkwood_column_baked` (128) | 0 |
| `st_mirkwood_ornamental_column_a01_centerpiece` | 3.947 x 3.947 x 3.999 | mid / mid / min | L0 3,842, L2 3,073, L3 1,920, L4 959, L5 576, L6 383 | `bo_st_mirkwood_ornamental_column_a01_centerpiece` (576) | `t_mirkwood_column_baked` (128) | 0 |
| `st_mirkwood_ornamental_column_a01_top` | 3.830 x 3.830 x 2.011 | mid / mid / min | L0 128, L2 102, L3 64, L4 32, L5 19, L6 12 | `bo_st_mirkwood_ornamental_column_a01_top` (19) | `t_mirkwood_column_baked` (128) | 0 |

## Prison doors

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_prison_door_a01` | 1.492 x 0.146 x 2.978 | mid / mid / min | L0 3,672, L2 2,937, L3 1,835, L4 918, L5 549, L6 366 | `bo_st_mirkwood_prison_door_a01` (550) | `t_mirkwood_tileable_brass` (663) | 0 |
| `st_mirkwood_prison_door_a01_broken` | 1.492 x 0.778 x 2.978 | mid / mid / min | L0 3,746, L2 2,995, L3 1,872, L4 936, L5 561, L6 374 | `bo_st_mirkwood_prison_door_a01_broken` (561) | `t_mirkwood_tileable_brass` (664) | 0 |

## Roofs and trims

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_roof_30_3m_clean` | 3.008 x 3.036 x 1.531 | mid / mid / 0.01 | L0 1,278, L2 1,022, L4 164 | `bo_st_mirkwood_roof_30_3m_clean` (2) | `t_mirkwood_tileable_roof_a` (622)<br>`t_mirkwood_tileable_wood` (1613) | 0 |
| `st_mirkwood_roof_30_3m_clean_corner_in` | 3.078 x 3.098 x 1.723 | mid / mid / min | L0 2,210, L2 1,768, L4 362, L6 218 | `bo_st_mirkwood_roof_30_3m_clean_corner_in` (82) | `t_mirkwood_tileable_roof_a` (621)<br>`t_mirkwood_tileable_wood` (1495) | 0 |
| `st_mirkwood_roof_30_3m_clean_corner_out` | 3.098 x 3.096 x 1.682 | mid / mid / min | L0 2,178, L2 1,742, L4 362, L6 216 | `bo_st_mirkwood_roof_30_3m_clean_corner_out` (82) | `t_mirkwood_tileable_roof_a` (617)<br>`t_mirkwood_tileable_wood` (1501) | 0 |
| `st_mirkwood_roof_30_3m_damaged` | 3.008 x 3.036 x 1.531 | mid / mid / 0.01 | L0 1,278, L2 1,022, L4 164 | `bo_st_mirkwood_roof_30_3m_damaged` (2) | `t_mirkwood_tileable_roof_a` (575)<br>`t_mirkwood_tileable_wood` (1613)<br>`t_mirkwood_tileable_roof_a_damaged` (647) | 0 |
| `st_mirkwood_roof_30_3m_damaged_corner_in` | 3.078 x 3.098 x 1.723 | mid / mid / min | L0 2,210, L2 1,768, L4 362, L6 218 | `bo_st_mirkwood_roof_30_3m_damaged_corner_in` (82) | `t_mirkwood_tileable_roof_a` (586)<br>`t_mirkwood_tileable_wood` (1495)<br>`t_mirkwood_tileable_roof_a_damaged` (645) | 0 |
| `st_mirkwood_roof_30_3m_damaged_corner_out` | 3.098 x 3.096 x 1.682 | mid / mid / min | L0 2,178, L2 1,742, L4 362, L6 216 | `bo_st_mirkwood_roof_30_3m_damaged_corner_out` (82) | `t_mirkwood_tileable_roof_a` (568)<br>`t_mirkwood_tileable_wood` (1501)<br>`t_mirkwood_tileable_roof_a_damaged` (648) | 0 |
| `st_mirkwood_roof_45_3m_clean` | 3.008 x 3.962 x 3.964 | mid / mid / min | L0 1,736, L2 1,388, L4 210 | `bo_st_mirkwood_roof_45_3m_clean` (4) | `t_mirkwood_tileable_roof_a` (603)<br>`t_mirkwood_tileable_wood` (1461) | 0 |
| `st_mirkwood_roof_45_3m_clean_corner_in` | 3.869 x 4.063 x 4.042 | mid / mid / min | L0 3,044, L2 2,434, L4 576, L6 344 | `bo_st_mirkwood_roof_45_3m_clean_corner_in` (126) | `t_mirkwood_tileable_roof_a` (613)<br>`t_mirkwood_tileable_wood` (1440) | 0 |
| `st_mirkwood_roof_45_3m_clean_corner_out` | 3.816 x 3.946 x 4.163 | mid / mid / min | L0 2,938, L2 2,350, L4 510, L6 306 | `bo_st_mirkwood_roof_45_3m_clean_corner_out` (126) | `t_mirkwood_tileable_roof_a` (614)<br>`t_mirkwood_tileable_wood` (1445) | 0 |
| `st_mirkwood_roof_45_3m_damaged` | 3.008 x 3.962 x 3.964 | mid / mid / min | L0 1,736, L2 1,388, L4 210 | `bo_st_mirkwood_roof_45_3m_damaged` (4) | `t_mirkwood_tileable_roof_a_damaged` (603)<br>`t_mirkwood_tileable_wood` (1461) | 0 |
| `st_mirkwood_roof_45_3m_damaged_corner_in` | 3.894 x 4.063 x 4.042 | mid / mid / min | L0 3,044, L2 2,434, L4 576, L6 344 | `bo_st_mirkwood_roof_45_3m_damaged_corner_in` (126) | `t_mirkwood_tileable_roof_a_damaged` (613)<br>`t_mirkwood_tileable_wood` (1440) | 0 |
| `st_mirkwood_roof_45_3m_damaged_corner_out` | 3.802 x 3.953 x 4.163 | mid / mid / outside | L0 2,938, L2 2,350, L4 510, L6 306 | `bo_st_mirkwood_roof_45_3m_damaged_corner_out` (126) | `t_mirkwood_tileable_roof_a_damaged` (614)<br>`t_mirkwood_tileable_wood` (1445) | 0 |
| `st_mirkwood_roof_ends_a01` | 0.124 x 1.183 x 0.759 | mid / mid / min | L0 228, L2 182, L4 56, L6 34 | `bo_st_mirkwood_roof_ends_a01` (34) | `t_mirkwood_tileable_wood` (1314) | 0 |
| `st_mirkwood_roof_side_a01` | 0.124 x 4.577 x 0.425 | mid / mid / min | L0 548, L2 438, L4 136, L6 82 | `bo_st_mirkwood_roof_side_a01` (82) | `t_mirkwood_tileable_wood` (1284) | 0 |

## Stairs

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_stairs_a01` | 2.240 x 2.271 x 1.907 | mid / mid / min | L0 1,104, L2 882, L3 552, L4 276, L5 164, L6 110 | `bo_st_mirkwood_stairs_a01` (164) | `t_mirkwood_bridge_stairs_unique` (295) | 3 |
| `st_mirkwood_stairs_a02` | 2.422 x 2.271 x 1.907 | mid / mid / min | L0 1,080, L2 864, L3 540, L4 270, L5 162, L6 108 | `bo_st_mirkwood_stairs_a02` (162) | `t_mirkwood_bridge_stairs_unique` (307) | 3 |
| `st_mirkwood_stairs_b01` | 2.004 x 2.000 x 2.000 | mid / mid / min | L0 740, L2 592, L3 370, L4 184, L5 110, L6 74 | `bo_st_mirkwood_stairs_b01` (110) | `t_mirkwood_unique_bakes_c` (304) | 0 |

## Stone entrances

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_stone_entrance_a01` | 2.064 x 0.413 x 3.282 | mid / mid / min | L0 1,040, L2 832, L3 520, L4 260, L5 156, L6 104 | `bo_st_mirkwood_stone_entrance_a01` (156) | `t_mirkwood_stone_clean` (682) | 0 |
| `st_mirkwood_stone_entrance_a01_extension` | 2.064 x 0.413 x 2.256 | mid / mid / min | L0 80, L2 64, L6 48 | `bo_st_mirkwood_stone_entrance_a01_extension` (48) | `t_mirkwood_stone_clean` (696) | 0 |
| `st_mirkwood_stone_entrance_a02` | 2.064 x 0.413 x 3.282 | mid / mid / min | L0 1,404, L2 756, L4 432, L6 208 | `bo_st_mirkwood_stone_entrance_a02` (64) | `t_mirkwood_stone_clean` (649)<br>`t_mirkwood_unique_bakes_a` (531) | 0 |
| `st_mirkwood_stone_entrance_a03` | 3.647 x 0.689 x 4.810 | mid / mid / min | L0 5,613, L2 4,490, L3 2,805, L4 1,403, L5 840, L6 561 | `bo_st_mirkwood_stone_entrance_a03` (841) | `t_mirkwood_stone_dirty` (481)<br>`t_mirkwood_unique_bakes_b` (432) | 27 |
| `st_mirkwood_stone_entrance_a03_mossy` | 1.824 x 0.689 x 4.810 | mid / mid / min | L0 488, L2 390, L3 244, L4 122, L5 73, L6 47 | `bo_st_mirkwood_stone_entrance_a03_mossy` (73) | `t_mirkwood_stone_mossy` (481)<br>`t_mirkwood_slated_stone_dirty` (466) | 0 |

## Town walls

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_town_wall_a01` | 7.993 x 1.354 x 8.321 | mid / mid / min | L0 1,237, L2 989, L4 617, L6 185 | `bo_st_mirkwood_town_wall_a01` (185) | `t_mirkwood_stone_clean` (682)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_town_wall_a01_dirty` | 7.993 x 1.354 x 8.321 | mid / mid / min | L0 1,237, L2 989, L4 617, L6 185 | `bo_st_mirkwood_town_wall_a01_dirty` (185) | `t_mirkwood_stone_dirty` (682)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_town_wall_a01_wide` | 7.993 x 2.707 x 8.321 | mid / mid / min | L0 2,236, L2 1,060, L4 568, L6 324 | `bo_st_mirkwood_town_wall_a01_wide` (334) | `t_mirkwood_stone_clean` (682)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_town_wall_a01_wide_dirty` | 7.993 x 2.707 x 8.321 | mid / mid / min | L0 2,236, L2 1,060, L4 568, L6 324 | `bo_st_mirkwood_town_wall_a01_wide_dirty` (334) | `t_mirkwood_stone_dirty` (682)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |

## Wall panels

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_wall_a01` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_a01` (76) | `t_mirkwood_stone_clean` (683) | 0 |
| `st_mirkwood_wall_a01_dirty` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_a01_dirty` (76) | `t_mirkwood_stone_dirty` (683) | 0 |
| `st_mirkwood_wall_a01_mossy` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_a01_mossy` (76) | `t_mirkwood_stone_mossy` (683) | 0 |
| `st_mirkwood_wall_slated_a01` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_slated_a01` (76) | `t_mirkwood_slated_stone_clean` (683) | 0 |
| `st_mirkwood_wall_slated_a01_dirty` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_slated_a01_dirty` (76) | `t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_wall_slated_a01_mossy` | 3.000 x 0.500 x 3.000 | mid / mid / min | L0 76 | `bo_st_mirkwood_wall_slated_a01_mossy` (76) | `t_mirkwood_slated_stone_mossy` (683) | 0 |

## Window panels

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_window_a01` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 3,413, L2 2,729, L4 853, L5 853, L6 511 | `bo_st_mirkwood_window_a01` (511) | `t_mirkwood_unique_bakes_a` (321)<br>`t_mirkwood_tileable_brass` (511)<br>`t_mirkwood_stone_clean` (683) | 0 |
| `st_mirkwood_window_a01_dirty` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 6,561, L2 5,248, L4 3,280, L5 1,640, L6 984 | `bo_st_mirkwood_window_a01_dirty` (983) | `t_mirkwood_tileable_brass` (915)<br>`t_mirkwood_stone_dirty` (683) | 0 |
| `st_mirkwood_window_a01_mossy` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 2,161, L2 1,727, L4 1,079, L5 539, L6 323 | `bo_st_mirkwood_window_a01_mossy` (323) | `t_mirkwood_tileable_brass` (511)<br>`t_mirkwood_stone_mossy` (683) | 0 |
| `st_mirkwood_window_a01_slated` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 3,413, L2 2,729, L4 1,705, L5 853, L6 511 | `bo_st_mirkwood_window_a01_slated` (511) | `t_mirkwood_unique_bakes_a` (321)<br>`t_mirkwood_tileable_brass` (511)<br>`t_mirkwood_slated_stone_clean` (683) | 0 |
| `st_mirkwood_window_a01_slated_dirty` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 6,561, L2 5,248, L4 3,280, L5 1,640, L6 984 | `bo_st_mirkwood_window_a01_slated_dirty` (983) | `t_mirkwood_tileable_brass` (915)<br>`t_mirkwood_slated_stone_dirty` (683) | 0 |
| `st_mirkwood_window_a01_slated_mossy` | 3.000 x 0.654 x 3.000 | mid / mid / min | L0 2,161, L2 1,079, L4 1,079, L5 539, L6 323 | `bo_st_mirkwood_window_a01_slated_mossy` (323) | `t_mirkwood_tileable_brass` (511)<br>`t_mirkwood_slated_stone_mossy` (683) | 0 |

## Root-supported walks

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `st_mirkwood_wood_path_a01` | 8.000 x 3.610 x 2.422 | mid / mid / min | L0 888, L2 576, L3 360, L4 180, L6 72 | `bo_st_mirkwood_wood_path_a01` (72) | `t_mirkwood_wood_path_b` (511)<br>`t_mirkwood_wood_bark_b` (511)<br>`t_decal_dirt_vertical` (445) | 2 |
| `st_mirkwood_wood_path_a02` | 8.000 x 3.608 x 2.009 | mid / mid / min | L0 708, L2 460, L3 288, L4 144, L6 56 | `bo_st_mirkwood_wood_path_a02` (56) | `t_mirkwood_wood_path_b` (512)<br>`t_mirkwood_wood_bark_b` (512)<br>`t_decal_dirt_vertical` (449) | 0 |
| `st_mirkwood_wood_path_a03` | 12.040 x 3.719 x 4.133 | mid / mid / min | L0 1,368, L2 882, L3 552, L4 276, L6 110 | `bo_st_mirkwood_wood_path_a03` (110) | `t_mirkwood_wood_path_b` (513)<br>`t_mirkwood_wood_bark_b` (506)<br>`t_decal_dirt_vertical` (420) | 0 |
| `st_mirkwood_wood_path_a04` | 4.926 x 3.608 x 4.133 | mid / mid / min | L0 708, L2 460, L3 288, L4 144, L6 56 | `bo_st_mirkwood_wood_path_a04` (56) | `t_mirkwood_wood_path_b` (514)<br>`t_mirkwood_wood_bark_b` (519)<br>`t_decal_dirt_vertical` (424) | 0 |
| `st_mirkwood_wood_path_b01` | 8.000 x 3.610 x 2.422 | mid / mid / min | L0 888, L2 576, L3 360, L4 180, L6 72 | `bo_st_mirkwood_wood_path_b01` (72) | `t_mirkwood_wood_path_a` (511)<br>`t_mirkwood_wood_bark_a` (511)<br>`t_decal_dirt_vertical` (445) | 3 |
| `st_mirkwood_wood_path_b02` | 8.000 x 3.608 x 2.009 | mid / mid / min | L0 708, L2 460, L3 288, L4 144, L6 56 | `bo_st_mirkwood_wood_path_b02` (56) | `t_mirkwood_wood_path_a` (512)<br>`t_mirkwood_wood_bark_a` (512)<br>`t_decal_dirt_vertical` (449) | 0 |
| `st_mirkwood_wood_path_b03` | 12.040 x 3.719 x 4.133 | mid / mid / min | L0 1,368, L2 882, L3 552, L4 276, L6 110 | `bo_st_mirkwood_wood_path_b03` (110) | `t_mirkwood_wood_path_a` (513)<br>`t_mirkwood_wood_bark_a` (506)<br>`t_decal_dirt_vertical` (450) | 0 |
| `st_mirkwood_wood_path_b04` | 4.926 x 3.608 x 4.133 | mid / mid / min | L0 708, L2 460, L3 288, L4 144, L6 56 | `bo_st_mirkwood_wood_path_b04` (56) | `t_mirkwood_wood_path_a` (514)<br>`t_mirkwood_wood_bark_a` (519)<br>`t_decal_dirt_vertical` (443) | 0 |

## Entrance ornament

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `pr_mirkwood_decor_entrance_a01` | 3.025 x 0.332 x 3.125 | mid / mid / min | L0 4,637, L2 3,708, L3 1,159, L4 1,159, L6 694 | `bo_pr_mirkwood_decor_entrance_a01` (1,159) | `t_mirkwood_unique_bakes_b` (432) | 0 |

## Statuary

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `pr_mirkwood_decor_statue_a01` | 0.500 x 0.512 x 2.000 | mid / mid / min | L0 44, L2 34, L4 22 | `bo_pr_mirkwood_decor_statue_a01` (22) | `t_mirkwood_unique_bakes_a` (268) | 0 |

## Lamps

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `pr_mirkwood_hanging_lamp_a01` | 0.513 x 0.514 x 3.875 | mid / mid / min | L0 9,708, L2 7,764, L3 4,854, L4 2,423, L6 547 | `bo_pr_mirkwood_hanging_lamp_a01` (887) | `t_mirkwood_lamps` (1089) | 0 |
| `pr_mirkwood_wall_lamp_a01` | 0.513 x 0.704 x 1.898 | mid / outside / min | L0 4,700, L2 2,348, L3 2,348, L4 1,171, L6 673 | `bo_pr_mirkwood_wall_lamp_a01` (1,123) | `t_mirkwood_lamps` (1089) | 0 |

## Vines

| Mesh | Size X x Y x Z (m) | Pivot X / Y / Z | Source LOD triangles | Prefab body and triangles | LOD0 materials and px/m | Scene placements |
| --- | --- | --- | --- | --- | --- | ---: |
| `pr_mirkwood_vine_a01_clean` | 5.089 x 0.553 x 1.454 | mid / mid / min | L0 516, L1 412, L2 258, L3 128, L4 76, L6 50 | `bo_pr_mirkwood_vine_a01_clean` (50) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_a01_mossy` | 5.089 x 0.553 x 1.454 | mid / mid / min | L0 516, L1 412, L2 258, L3 516, L4 76, L6 50 | `bo_pr_mirkwood_vine_a01_mossy` (50) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_a01_rotten` | 5.089 x 0.553 x 1.454 | mid / mid / min | L0 516, L1 412, L2 258, L3 516, L4 76, L6 50 | `bo_pr_mirkwood_vine_a01_rotten` (50) | `t_mirkwood_vines_rotten` (195) | 2 |
| `pr_mirkwood_vine_a02_clean` | 5.089 x 0.676 x 1.593 | mid / mid / min | L0 516, L1 412, L2 258, L3 516, L4 76, L6 50 | `bo_pr_mirkwood_vine_a02_clean` (50) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_a02_mossy` | 5.089 x 0.676 x 1.593 | mid / mid / min | L0 516, L1 412, L2 258, L3 516, L4 76, L6 50 | `bo_pr_mirkwood_vine_a02_mossy` (50) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_a02_rotten` | 5.089 x 0.676 x 1.593 | mid / mid / min | L0 516, L1 412, L2 258, L3 516, L4 76, L6 50 | `bo_pr_mirkwood_vine_a02_rotten` (50) | `t_mirkwood_vines_rotten` (195) | 0 |
| `pr_mirkwood_vine_a03_clean` | 4.762 x 1.407 x 1.593 | mid / mid / min | L0 480, L1 384, L2 240, L3 480, L4 72, L6 48 | `bo_pr_mirkwood_vine_a03_clean` (48) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_a03_mossy` | 4.762 x 1.407 x 1.593 | mid / mid / min | L0 480, L1 384, L2 240, L3 480, L4 72, L6 48 | `bo_pr_mirkwood_vine_a03_mossy` (48) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_a03_rotten` | 4.762 x 1.407 x 1.593 | mid / mid / min | L0 480, L1 384, L2 240, L3 480, L4 72, L6 48 | `bo_pr_mirkwood_vine_a03_rotten` (48) | `t_mirkwood_vines_rotten` (195) | 2 |
| `pr_mirkwood_vine_b01_clean` | 3.889 x 0.591 x 1.236 | mid / mid / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b01_clean` (122) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_b01_mossy` | 3.889 x 0.591 x 1.236 | mid / mid / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b01_mossy` (122) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_b01_rotten` | 3.889 x 0.591 x 1.236 | mid / mid / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b01_rotten` (122) | `t_mirkwood_vines_rotten` (195) | 2 |
| `pr_mirkwood_vine_b02_clean` | 3.786 x 1.143 x 1.188 | mid / mid / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b02_clean` (122) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_b02_mossy` | 3.786 x 1.143 x 1.188 | mid / 0.59 / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b02_mossy` (122) | `t_mirkwood_vines_mossy` (195) | 4 |
| `pr_mirkwood_vine_b02_rotten` | 3.786 x 1.143 x 1.188 | mid / mid / min | L0 1,220, L1 976, L2 610, L3 1,220, L4 182, L6 122 | `bo_pr_mirkwood_vine_b02_rotten` (122) | `t_mirkwood_vines_rotten` (195) | 2 |
| `pr_mirkwood_vine_c01_clean` | 8.987 x 1.532 x 2.305 | mid / mid / min | L0 628, L1 502, L2 314, L3 628, L4 94, L6 62 | `bo_pr_mirkwood_vine_c01_clean` (62) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_c01_mossy` | 8.987 x 1.532 x 2.305 | mid / mid / min | L0 628, L1 502, L2 314, L3 628, L4 94, L6 62 | `bo_pr_mirkwood_vine_c01_mossy` (62) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_c01_rotten` | 8.987 x 1.532 x 2.305 | mid / mid / min | L0 628, L1 502, L2 314, L3 628, L4 94, L6 62 | `bo_pr_mirkwood_vine_c01_rotten` (62) | `t_mirkwood_vines_rotten` (195) | 3 |
| `pr_mirkwood_vine_d01_clean` | 3.251 x 2.430 x 1.431 | mid / mid / min | L0 812, L1 648, L2 406, L3 812, L4 121, L6 81 | `bo_pr_mirkwood_vine_d01_clean` (81) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_d01_mossy` | 3.251 x 2.430 x 1.431 | mid / mid / min | L0 812, L1 648, L2 406, L3 812, L4 121, L6 81 | `bo_pr_mirkwood_vine_d01_mossy` (81) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_d01_rotten` | 3.251 x 2.430 x 1.431 | mid / mid / min | L0 812, L1 648, L2 406, L3 812, L4 121, L6 81 | `bo_pr_mirkwood_vine_d01_rotten` (81) | `t_mirkwood_vines_rotten` (195) | 4 |
| `pr_mirkwood_vine_d02_clean` | 7.512 x 2.636 x 3.372 | mid / mid / min | L0 728, L1 582, L2 364, L3 728, L4 108, L6 71 | `bo_pr_mirkwood_vine_d02_clean` (71) | `t_mirkwood_vines_clean` (195) | 0 |
| `pr_mirkwood_vine_d02_mossy` | 7.512 x 2.636 x 3.372 | mid / mid / min | L0 728, L1 582, L2 364, L3 728, L4 108, L6 71 | `bo_pr_mirkwood_vine_d02_mossy` (71) | `t_mirkwood_vines_mossy` (195) | 0 |
| `pr_mirkwood_vine_d02_rotten` | 7.512 x 2.636 x 3.372 | mid / mid / min | L0 728, L1 582, L2 364, L3 728, L4 108, L6 71 | `bo_pr_mirkwood_vine_d02_rotten` (71) | `t_mirkwood_vines_rotten` (195) | 14 |
