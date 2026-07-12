"""
Generate a dwarven house prefab from the erebor kitbash, modelled on the
artist's `dwarf_house_b` composition inside `taom_erebor_kitbash.xml` but
scaled to an arbitrary `WIDTH x LENGTH` tile footprint.

Layout conventions derived from `dwarf_house_b` (A-family throughout):
  - 3m × 3m ground tiles of `sm_dw_ground_3m_a2`, with `_a1_corner` at the
    four outer corners.
  - Ground trim strips (`_trim_3m_a1`) on the interior tile boundaries
    running along X, rotated strips (`_trim_3m_a2` at rot +π/2) along Y,
    plus `_trim_corner_a1` nodes at trim intersections.
  - `sm_dw_wall_3m_corn_a` at the four outer corners.
  - Plain walls + `_win_a1/a3` + `_door_a1` around the perimeter.
  - Pitched hip roof: `_roof_str_a1` panels at Z=3, `_roof_top_a1` ridge
    cap at Z=4.5 (1.5m above walls), `_roof_side_cor_out_a1` at eave
    edges, `_roof_cor_tri_a1` at the four hip corners.
  - Decorative beams (`_wall_beam_3m_b` at Z=0 base, `_c` at Z=3 top)
    running along wall lines.
  - `_trim_corner_a1` stud at Z=4.4 on wall intersections for a roof-edge
    decorative detail (copied from house_b).

Usage:
    python tools/build_test_erebor_house.py [width=4] [length=8] [out_path]

Rotation convention (from the calibration hut in `test_erebor_hut.xml`):
  - Wall default face is +Y at rot 0.
  - North wall at Y=+half_l: rot 0 (face +Y outward).
  - South wall at Y=-half_l: rot π (face -Y outward).
  - East wall at X=+half_w: rot -π/2 (face +X outward).
  - West wall at X=-half_w: rot +π/2 (face -X outward).
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

TILE = 3.0
WALL_H = 3.0
RIDGE_H = 4.5
TRIM_STUD_Z = 4.4

PI = math.pi
HALF_PI = math.pi / 2

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


def build(width_tiles: int, length_tiles: int) -> str:
    half_w = width_tiles * TILE / 2.0
    half_l = length_tiles * TILE / 2.0
    entries: list[str] = []

    def add(sm: str, x: float, y: float, z: float,
            rz: float = 0.0, rx: float = 0.0, ry: float = 0.0,
            scale: tuple[float, float, float] | None = None,
            bo: str | None = None) -> None:
        bo = bo or f"bo_{sm}"
        if scale is None:
            entries.append(ENTRY_NO_SCALE.format(
                sm=sm, bo=bo, x=x, y=y, z=z, rx=rx, ry=ry, rz=rz,
            ))
        else:
            entries.append(ENTRY_WITH_SCALE.format(
                sm=sm, bo=bo, x=x, y=y, z=z, rx=rx, ry=ry, rz=rz,
                sx=scale[0], sy=scale[1], sz=scale[2],
            ))

    def tile_center_x(i: int) -> float:
        return -half_w + TILE * (i + 0.5)

    def tile_center_y(j: int) -> float:
        return -half_l + TILE * (j + 0.5)

    # ----- Floor -----
    # Outer-corner tiles use `_a1_corner` (diagonal chamfer); interior uses `_a2`.
    for i in range(width_tiles):
        for j in range(length_tiles):
            x = tile_center_x(i)
            y = tile_center_y(j)
            is_nw = (i == 0) and (j == length_tiles - 1)
            is_ne = (i == width_tiles - 1) and (j == length_tiles - 1)
            is_sw = (i == 0) and (j == 0)
            is_se = (i == width_tiles - 1) and (j == 0)
            if is_nw:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=0.0)
            elif is_ne:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=-HALF_PI)
            elif is_se:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=PI)
            elif is_sw:
                add("sm_dw_ground_3m_a1_corner", x, y, 0.0, rz=HALF_PI)
            else:
                add("sm_dw_ground_3m_a2", x, y, 0.0)

    # ----- Trim grid (internal tile boundaries) -----
    # Horizontal trim strips (along X) at each interior Y boundary.
    for j in range(1, length_tiles):
        y_bound = -half_l + TILE * j
        for i in range(width_tiles):
            add("sm_dw_ground_trim_3m_a1", tile_center_x(i), y_bound, 0.0)

    # Vertical trim strips (along Y) at each interior X boundary (rot +π/2).
    for i in range(1, width_tiles):
        x_bound = -half_w + TILE * i
        for j in range(length_tiles):
            add("sm_dw_ground_trim_3m_a2", x_bound, tile_center_y(j), 0.0, rz=HALF_PI)

    # Trim corner studs where interior boundaries cross.
    for i in range(1, width_tiles):
        for j in range(1, length_tiles):
            x_bound = -half_w + TILE * i
            y_bound = -half_l + TILE * j
            add("sm_dw_ground_trim_corner_a1", x_bound, y_bound, 0.0)

    # ----- Walls (perimeter) -----
    # North side (Y = +half_l), facing +Y (rot 0). Corners use `corn_a`.
    for i in range(width_tiles):
        x = tile_center_x(i)
        if i == 0:
            add("sm_dw_wall_3m_corn_a", x, half_l, 0.0, rz=0.0)
        elif i == width_tiles - 1:
            add("sm_dw_wall_3m_corn_a", x, half_l, 0.0, rz=-HALF_PI)
        else:
            # Alternate plain and win_a1 for variety
            mesh = "sm_dw_wall_3m_win_a1" if (i % 2 == 1) else "sm_dw_wall_3m_a"
            add(mesh, x, half_l, 0.0, rz=0.0)

    # South side (Y = -half_l), facing -Y (rot π). Door slot at i=1.
    door_slot = 1
    for i in range(width_tiles):
        x = tile_center_x(i)
        if i == 0:
            add("sm_dw_wall_3m_corn_a", x, -half_l, 0.0, rz=HALF_PI)
        elif i == width_tiles - 1:
            add("sm_dw_wall_3m_corn_a", x, -half_l, 0.0, rz=PI)
        elif i == door_slot:
            add("sm_dw_wall_3m_door_a1", x, -half_l, 0.0, rz=PI)
        else:
            mesh = "sm_dw_wall_3m_win_a3" if (i % 2 == 0) else "sm_dw_wall_3m_a"
            add(mesh, x, -half_l, 0.0, rz=PI)

    # East side (X = +half_w), facing +X (rot -π/2). Corners handled by N/S.
    window_slots = {1, length_tiles // 2, length_tiles - 2}
    for j in range(1, length_tiles - 1):  # skip j=0 and j=last (corners)
        y = tile_center_y(j)
        mesh = "sm_dw_wall_3m_win_a1" if j in window_slots else "sm_dw_wall_3m_a"
        add(mesh, half_w, y, 0.0, rz=-HALF_PI)

    # West side (X = -half_w), facing -X (rot +π/2).
    for j in range(1, length_tiles - 1):
        y = tile_center_y(j)
        mesh = "sm_dw_wall_3m_win_a1" if j in window_slots else "sm_dw_wall_3m_a"
        add(mesh, -half_w, y, 0.0, rz=HALF_PI)

    # ----- Decorative beams along wall bases (Z=0) and tops (Z=3) -----
    # Base beams (`_b`) along N/S walls
    for i in range(width_tiles):
        x = tile_center_x(i)
        add("sm_dw_wall_beam_3m_b", x, half_l, 0.0, rz=0.0)
        add("sm_dw_wall_beam_3m_b", x, -half_l, 0.0, rz=PI)
    # Base beams along E/W walls
    for j in range(1, length_tiles - 1):
        y = tile_center_y(j)
        add("sm_dw_wall_beam_3m_b", half_w, y, 0.0, rz=-HALF_PI)
        add("sm_dw_wall_beam_3m_b", -half_w, y, 0.0, rz=HALF_PI)

    # Top beams (`_c`) at Z=3 along all perimeter walls
    for i in range(width_tiles):
        x = tile_center_x(i)
        add("sm_dw_wall_beam_3m_c", x, half_l, WALL_H, rz=0.0)
        add("sm_dw_wall_beam_3m_c", x, -half_l, WALL_H, rz=PI)
    for j in range(1, length_tiles - 1):
        y = tile_center_y(j)
        add("sm_dw_wall_beam_3m_c", half_w, y, WALL_H, rz=-HALF_PI)
        add("sm_dw_wall_beam_3m_c", -half_w, y, WALL_H, rz=HALF_PI)

    # ----- Pitched hip roof -----
    # Ridge runs along Y (long axis) at X=0, Z=RIDGE_H.
    # North + south short edges get hip panels; E + W long edges get slope panels.

    # Long-side slope panels (east/west slopes, rot so slope falls outward).
    # Each 3m slot along Y gets two panels (one east, one west).
    for j in range(length_tiles):
        y = tile_center_y(j)
        # East slope: panel at X = +half_w, rot -π/2 (sloping from ridge at X=0 down to +X)
        add("sm_dw_roof_str_a1", half_w, y, WALL_H, rz=-HALF_PI)
        # West slope
        add("sm_dw_roof_str_a1", -half_w, y, WALL_H, rz=HALF_PI)

    # Short-end hip panels at N and S ends, rot to slope outward along Y.
    for i in range(width_tiles):
        x = tile_center_x(i)
        add("sm_dw_roof_str_a1", x, half_l, WALL_H, rz=0.0)   # north slope
        add("sm_dw_roof_str_a1", x, -half_l, WALL_H, rz=PI)   # south slope

    # Ridge cap along Y at X=0, Z=RIDGE_H.
    # Ridge covers the INNER length only — 3m hip inset at each short end.
    # For length_tiles=8, ridge tiles at j = 1..length-2 (6 tiles).
    for j in range(1, length_tiles - 1):
        y = tile_center_y(j)
        add("sm_dw_roof_top_a1", 0.0, y, RIDGE_H, rz=HALF_PI)

    # REMOVED: outer-corner triangle pieces (`sm_dw_roof_cor_tri_a1`). In
    # `dwarf_house_b` this mesh is used at INTERIOR valley corners, not outer
    # hip corners — placing it at outer corners produced the 4 "tan wings"
    # visible in iteration 1's screenshot.

    # REMOVED: eave-edge trim studs at Z=4.4. In `dwarf_house_b` these sit
    # around an interior roof valley, not along exterior eaves — they read
    # as visible bumps when placed along outer perimeter.

    xml = "<prefabs>\n"
    xml += f'\t<game_entity name="test_erebor_house_{width_tiles}x{length_tiles}" old_prefab_name="">\n'
    xml += '\t\t<transform position="0.000, 0.000, 0.000" rotation_euler="0.000, 0.000, 0.000"/>\n'
    xml += '\t\t<children>\n'
    xml += "".join(entries)
    xml += '\t\t</children>\n'
    xml += '\t</game_entity>\n'
    xml += "</prefabs>\n"
    return xml


def main(argv: list[str]) -> int:
    width = int(argv[1]) if len(argv) > 1 else 4
    length = int(argv[2]) if len(argv) > 2 else 8
    out_path = Path(argv[3]) if len(argv) > 3 else Path(
        r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/Prefabs/"
        f"test_erebor_house_{width}x{length}.xml"
    )

    xml = build(width, length)
    out_path.write_text(xml, encoding="utf-8")

    n_entries = xml.count("<game_entity ")
    print(f"wrote {n_entries} entries to {out_path}")
    print(f"  footprint: {width}x{length} tiles = {width*TILE:.0f}m x {length*TILE:.0f}m")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
