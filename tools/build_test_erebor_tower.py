"""
Generate a dwarven watchtower prefab from the erebor kitbash.

3-storey square tower:
  - 3×3 tile footprint (9m × 9m).
  - 3 storeys of walls (total 9m tall).
  - Ground floor: door + plain walls, no windows except corner slits.
  - Mid floor: arrow-slit windows (`_win_a3`) — defensive layer.
  - Top floor: arched windows (`_win_a1`) — observation/light.
  - Flat stone roof (tiled `sm_dw_roof_top_a1`) at Z=9.
  - Crenellated parapet: `sm_dw_castle_wall_a1_str_mrln_01..10` ring around the
    perimeter at Z=9, cycling merlon variants for visual variety.
  - Decorative column pilasters at the 4 outer corners running the full
    height, with `_clmn_top_c` caps at each storey transition (Z=3, 6, 9).

Usage:
    python tools/build_test_erebor_tower.py [out_path]

Rotation convention (same as hut + longhouse):
  - Walls default +Y at rot 0.
  - N wall: rot 0; E wall: rot -π/2; S wall: rot π; W wall: rot +π/2.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

TILE = 3.0
STORY_H = 3.0
N_STORIES = 3
WIDTH_TILES = 3  # 3x3 square footprint

TOP_Z = STORY_H * N_STORIES   # 9.0  (wall top, parapet base)

PI = math.pi
HALF_PI = PI / 2

ENTRY_NO_SCALE = (
    '\t\t\t<game_entity name="{sm}" old_prefab_name="">\n'
    '\t\t\t\t<transform position="{x:.3f}, {y:.3f}, {z:.3f}" rotation_euler="{rx:.3f}, {ry:.3f}, {rz:.3f}"/>\n'
    '\t\t\t\t<physics shape="{bo}"/>\n'
    '\t\t\t\t<components>\n'
    '\t\t\t\t\t<meta_mesh_component name="{sm}"/>\n'
    '\t\t\t\t</components>\n'
    '\t\t\t</game_entity>\n'
)

ENTRY_WITH_SCALE = (
    '\t\t\t<game_entity name="{sm}" old_prefab_name="">\n'
    '\t\t\t\t<transform position="{x:.3f}, {y:.3f}, {z:.3f}" rotation_euler="{rx:.3f}, {ry:.3f}, {rz:.3f}" scale="{sx:.3f}, {sy:.3f}, {sz:.3f}"/>\n'
    '\t\t\t\t<physics shape="{bo}"/>\n'
    '\t\t\t\t<components>\n'
    '\t\t\t\t\t<meta_mesh_component name="{sm}"/>\n'
    '\t\t\t\t</components>\n'
    '\t\t\t</game_entity>\n'
)


def build() -> str:
    half = WIDTH_TILES * TILE / 2.0  # 4.5
    entries: list[str] = []

    def add(sm: str, x: float, y: float, z: float,
            rz: float = 0.0,
            scale: tuple[float, float, float] | None = None,
            bo: str | None = None) -> None:
        bo = bo or f"bo_{sm}"
        if scale is None:
            entries.append(ENTRY_NO_SCALE.format(
                sm=sm, bo=bo, x=x, y=y, z=z, rx=0.0, ry=0.0, rz=rz,
            ))
        else:
            entries.append(ENTRY_WITH_SCALE.format(
                sm=sm, bo=bo, x=x, y=y, z=z, rx=0.0, ry=0.0, rz=rz,
                sx=scale[0], sy=scale[1], sz=scale[2],
            ))

    def tile_center(i: int) -> float:
        return -half + TILE * (i + 0.5)  # for 3 tiles: -3, 0, +3

    # ----- Floor: 3x3 ground tiles with chamfered outer corners -----
    for i in range(WIDTH_TILES):
        for j in range(WIDTH_TILES):
            x = tile_center(i)
            y = tile_center(j)
            is_nw = (i == 0) and (j == WIDTH_TILES - 1)
            is_ne = (i == WIDTH_TILES - 1) and (j == WIDTH_TILES - 1)
            is_sw = (i == 0) and (j == 0)
            is_se = (i == WIDTH_TILES - 1) and (j == 0)
            if is_nw:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=0.0)
            elif is_ne:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=-HALF_PI)
            elif is_se:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=PI)
            elif is_sw:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=HALF_PI)
            else:
                add("sm_dw_ground_3m_a1", x, y, 0.0)

    # ----- Walls, 3 storeys -----
    # Middle slot on each side (i == 1) is the "face" slot — gets door on
    # ground floor, then window variants on upper floors.
    # Corners (i == 0 or WIDTH_TILES-1) use `corn_a` for clean 90° joins.

    def wall_mesh_for(story: int, i: int, is_corner: bool) -> str:
        if is_corner:
            return "sm_dw_wall_3m_corn_a"
        if story == 0 and i == 1:
            return "sm_dw_wall_3m_door_a1"
        if story == 1:
            return "sm_dw_wall_3m_win_a3"     # arrow slit
        if story == 2:
            return "sm_dw_wall_3m_win_a1"     # arched window
        return "sm_dw_wall_3m_a"              # fallback plain

    for story in range(N_STORIES):
        z = story * STORY_H

        # North wall (y = +half), rot 0 (face +Y).
        for i in range(WIDTH_TILES):
            is_corner_i = (i == 0 or i == WIDTH_TILES - 1)
            corner_rz = 0.0 if i == 0 else -HALF_PI  # NW: 0, NE: -π/2
            mesh = wall_mesh_for(story, i, is_corner_i)
            # The door only goes on the ground floor south side (main entry);
            # rotate the middle-slot mesh per face.
            rz = corner_rz if is_corner_i else 0.0
            # No door on N for ground floor — use plain instead
            if story == 0 and i == 1 and mesh == "sm_dw_wall_3m_door_a1":
                mesh = "sm_dw_wall_3m_a"
            add(mesh, tile_center(i), half, z, rz=rz)

        # South wall (y = -half), rot π (face -Y). Door goes here.
        for i in range(WIDTH_TILES):
            is_corner_i = (i == 0 or i == WIDTH_TILES - 1)
            corner_rz = HALF_PI if i == 0 else PI  # SW: +π/2, SE: π
            mesh = wall_mesh_for(story, i, is_corner_i)
            rz = corner_rz if is_corner_i else PI
            add(mesh, tile_center(i), -half, z, rz=rz)

        # East wall (x = +half), rot -π/2 (face +X). Corners handled by N/S.
        for j in range(1, WIDTH_TILES - 1):
            mesh = wall_mesh_for(story, 1, False)
            # East wall middle: plain on ground, slit on mid, arched on top
            if story == 0:
                mesh = "sm_dw_wall_3m_a"
            add(mesh, half, tile_center(j), z, rz=-HALF_PI)

        # West wall (x = -half), rot +π/2 (face -X).
        for j in range(1, WIDTH_TILES - 1):
            mesh = wall_mesh_for(story, 1, False)
            if story == 0:
                mesh = "sm_dw_wall_3m_a"
            add(mesh, -half, tile_center(j), z, rz=HALF_PI)

    # ----- Corner pilasters (4 outer verticals, 3 storeys tall) -----
    # One `sm_dw_wall_clmn_3m_b` at each outer corner, at each storey Z.
    corners_xy = [
        (-half, +half),  # NW
        (+half, +half),  # NE
        (+half, -half),  # SE
        (-half, -half),  # SW
    ]
    for (cx, cy) in corners_xy:
        for story in range(N_STORIES):
            add("sm_dw_wall_clmn_3m_b", cx, cy, story * STORY_H)

    # Column caps (`_clmn_top_c`) at each storey top (Z = 3, 6, 9) on each corner.
    for (cx, cy) in corners_xy:
        for top_z in (STORY_H, STORY_H * 2, STORY_H * N_STORIES):
            add("sm_dw_wall_clmn_top_c", cx, cy, top_z,
                scale=(1.1, 1.1, 1.1))

    # ----- Flat stone roof at Z = TOP_Z -----
    for i in range(WIDTH_TILES):
        for j in range(WIDTH_TILES):
            add("sm_dw_roof_top_a1", tile_center(i), tile_center(j), TOP_Z)

    # ----- Crenellated parapet REMOVED for this iteration -----
    # `sm_dw_castle_wall_a1_str_mrln_*` is designed to pair with the full
    # castle wall segment `sm_dw_castle_wall_a1_str`, not a regular 3m house
    # wall. Placed standalone at Z=TOP_Z, the merlon geometry floats ~3m
    # above the wall top. Save for a follow-up iteration where the top
    # storey is switched to castle-wall pieces.

    xml = "<prefabs>\n"
    xml += '\t<game_entity name="test_erebor_tower" old_prefab_name="">\n'
    xml += '\t\t<transform position="0.000, 0.000, 0.000" rotation_euler="0.000, 0.000, 0.000"/>\n'
    xml += '\t\t<children>\n'
    xml += "".join(entries)
    xml += '\t\t</children>\n'
    xml += '\t</game_entity>\n'
    xml += "</prefabs>\n"
    return xml


def main(argv: list[str]) -> int:
    out_path = Path(argv[1]) if len(argv) > 1 else Path(
        r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/Prefabs/"
        "test_erebor_tower.xml"
    )
    xml = build()
    out_path.write_text(xml, encoding="utf-8")
    n = xml.count("<game_entity ")
    print(f"wrote {n} entries to {out_path}")
    print(f"  footprint: {WIDTH_TILES}x{WIDTH_TILES} tiles = {WIDTH_TILES*TILE:.0f}m x {WIDTH_TILES*TILE:.0f}m")
    print(f"  height:    {N_STORIES} storeys = {TOP_Z:.0f}m walls + parapet")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
