"""
Compose Lond Cirion BUILDINGS from the Gondor part families — the forward
path proved necessary by the reverse-engineering probe (shipped buildings
are merged component meshes; recipes are not recoverable, so new ones are
composed from the catalog and joined into the same kit-FBX template).

Two pilot buildings, both on the 3 m module grid:
    lond_cirion_house_01 — 6x6 m harbour house, two 3 m storeys, 45-degree
        gabled roof (ridge along X at z 9.03), tiled floors at z 0 and 3.
    lond_cirion_house_02 — 12x6 m arched hall, one 6 m storey, 26.57-degree
        gabled roof (the gondor_roof_a_30_* family is 1.5 rise over 3.0 run,
        i.e. 26.57 deg, NOT 30 despite its name), tiled floor at z 0.
Both get a below-grade foundation skirt at z -3 (shipped Gondor buildings
bury 3 m; without it sloped terrain shows daylight under the walls).

PLACEMENT RULES the parts force on us (all measured, none assumed):
  * A "part" is a PREFIX naming a group of sub-objects ("<p>.wall",
    ".stonebrick", ".door"...). LODs are ".lodN" siblings PER SUB-OBJECT and
    collision is a single "bo_<p>". Tiers are assembled per sub-object.
  * DECAL TRAP: a ".decalleak" sub-object can hang far below the part body
    (gondor_wall_trim_6m_a's hangs 1.474 m low). Since the anchor is the
    group bbox bottom, including it threw the whole cornice ring 1.54 m too
    high. Decal sub-objects are excluded from the group entirely.
  * BARE ".lod" TRAP: gondor_roof_a_45_3m_side_a_clean's gable infill plate
    is named ".wall.lod" with NO plain ".wall" sibling (source typo). A
    naive "reject anything containing .lod" filter drops it and leaves the
    gable end 50% open. A trailing bare ".lod" counts as a base object.
  * ORIGIN-ANCHORED PARTS: the roof eave strips hang entirely in -y/-z from
    an origin at their top-inner corner (that corner IS the eave line), so
    the bbox-bottom anchor mis-seats them. ANCHOR_ORIGIN pins them by origin.
  * GABLE SEATING: place a gable group so its 12-tri infill PLATE's apex
    lands exactly on the ridge; the coping then self-seats (flush with the
    tile plane on the 45 family, 0.15 proud on the 30 family, as authored).
  * TRIMS SIT FLUSH: trims are authored symmetric about their local y, i.e.
    to straddle a wall plane. Centring them ON the wall centreline projects
    0.15 m (slim) / 0.26 m (heavy) — a string course. Hanging them off the
    face instead cantilevered them 0.70-0.81 m and pushed the footprint off
    the module grid (7.9 m on a 6 m house), inflating collision so two
    houses could not butt.

Output: blockout/lond_cirion_buildings_a.fbx (per building: base + .lod3 +
.lod6 + bo_) + framed elevations/top/three-quarter renders in
E:\\LOTRAOMAssets\\_export\\lond_cirion\\buildings\\

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b ^
        -P tools/oneoff/blender_assemble_lond_cirion_buildings.py
"""

import json
import math
import os
import re
import traceback

import bpy
from mathutils import Matrix, Vector

GONDOR = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor"
FAMILIES = ["gondor_wall_3m_straight_a.fbx", "gondor_wall_6m_straight_a.fbx",
            "gondor_trims_straight_a.fbx", "gondor_roofs_a.fbx",
            "gondor_ground_straight_a.fbx", "gondor_buttres_a.fbx",
            "gondor_stairs_3m_straight_a.fbx"]
OUT_FBX = os.path.join(GONDOR, "blockout", "lond_cirion_buildings_a.fbx")
STAGING = r"E:\LOTRAOMAssets\_export\lond_cirion\buildings"

# most-reduced-first per tier; families number their LODs differently
# (.lod2/.lod4/.lod5 in the wall+roof sheets, .lod3/.lod6 in the trims)
TIER_PICK = {"lod3": (".lod3", ".lod2", ".lod4"),
             "lod6": (".lod6", ".lod5", ".lod4", ".lod2")}
TIER_SUFFIX = {"base": "", "lod3": ".lod3", "lod6": ".lod6"}

FLOOR = "gondor_ground_3m_a_normal.floor"
FLOOR_Z = -0.11        # tile is 0.11 thick from its origin: top lands on grade
RIDGE_CAP = "gondor_wall_trim_3m_c_clean"
# Below-grade skirt: 9 of 10 shipped Gondor buildings bury exactly 3 m. Only
# the 12-tri ".wall" box is placed — the ".stonebrick" relief (648 tris/panel)
# would be invisible underground. The 3 m part is used on BOTH houses: the
# 6 m panel at z -3 would span -3..+3 and z-fight the storey at 0..6.
SKIRT = "gondor_wall_3m_a_clean.wall"
SKIRT_Z = -3.0
# Parts whose authored origin IS their mounting point (the bbox anchor
# mis-seats them). Measured 2026-07-29:
#   *_edge_straight   — hangs in -y/-z from its top-inner corner = the eave line
#   buttress_a_clean  — body hangs in -y from a mounting plane at y=0, z 0..6
#   stairs_3m_a_clean — runs -y and DOWN from a top landing at local (0, 0, 3)
ANCHOR_ORIGIN = {"gondor_roof_a_45_3m_edge_straight",
                 "gondor_roof_a_30_3m_edge_straight",
                 "gondor_buttress_a_clean",
                 "gondor_stairs_3m_a_clean"}
GABLE_RIDGE_OVERLAP = 0.03   # mirrored halves cross y=0 so the apex closes

LOG_LINES = []
REPORT_DIR = None
by_name = {}
PLACEMENTS = []   # (section, prefix, matrix)
WARNINGS = []


def log(msg):
    LOG_LINES.append(str(msg))
    print(msg)
    if REPORT_DIR:
        with open(os.path.join(REPORT_DIR, "log.txt"), "w", encoding="utf-8") as f:
            f.write("\n".join(LOG_LINES))


def lod_stem(name):
    """Strip a trailing bare '.lod' so lod siblings can be found."""
    return name[:-4] if name.endswith(".lod") else name


def is_base_name(n, prefix):
    if not (n == prefix or n.startswith(prefix + ".")):
        return False
    if n.startswith("bo_") or ".decal" in n:
        return False
    return re.search(r"\.lod\d", lod_stem(n)) is None


def base_set(prefix):
    return [o for n, o in by_name.items() if is_base_name(n, prefix)]


def tier_objs(prefix, tier):
    if tier == "bo":
        # sub-object prefixes ("<part>.floor") share the part's bo_ hull
        for cand in (prefix, prefix.rsplit(".", 1)[0]):
            b = by_name.get("bo_" + cand)
            if b is not None:
                return [b]
        return []
    objs = base_set(prefix)
    if tier == "base":
        return objs
    out = []
    for o in objs:
        pick = o
        for suf in TIER_PICK[tier]:
            cand = by_name.get(lod_stem(o.name) + suf)
            if cand is not None:
                pick = cand
                break
        else:
            WARNINGS.append(f"{o.name}: no {tier} variant, base carried over")
        out.append(pick)
    return out


def group_bbox(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            v = Vector(c)
            lo.x, lo.y, lo.z = min(lo.x, v.x), min(lo.y, v.y), min(lo.z, v.z)
            hi.x, hi.y, hi.z = max(hi.x, v.x), max(hi.y, v.y), max(hi.z, v.z)
    return lo, hi


def anchor(prefix):
    """Where the part is gripped. Origin for ANCHOR_ORIGIN parts; otherwise
    the STRUCTURAL body's bottom-centre.

    Anchoring on the whole group's bbox is wrong for wall panels: each panel
    type carries different decorative sub-objects (a window part adds 0.55 m
    of tracery, a plain part does not), so the group centre — and with it the
    panel's wall plane — lands a few mm off its neighbour's. Measured on the
    barracks: two facade planes 5 mm apart, 111.9 m2 at y -3.250 against
    39.9 m2 at -3.255, which z-fights as black patches across the storey.
    Gripping the structural body puts every panel's wall plane on exactly
    the same line regardless of its decoration.
    """
    if prefix in ANCHOR_ORIGIN:
        return Vector((0.0, 0.0, 0.0))
    body = by_name.get(prefix + ".wall") or by_name.get(prefix)
    lo, hi = group_bbox([body] if body is not None else base_set(prefix))
    return Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))


def part_height(prefix):
    lo, hi = group_bbox(base_set(prefix))
    return hi.z - lo.z


def sub_obj(prefix, needle):
    for o in base_set(prefix):
        if needle in o.name:
            return o
    return None


def tris(o):
    o.data.calc_loop_triangles()
    return len(o.data.loop_triangles)


def detail_side(prefix):
    """+1/-1: which y side of the group carries the detail face."""
    objs = base_set(prefix)
    lo, hi = group_bbox(objs)
    cy = (lo.y + hi.y) / 2
    pos = neg = 0
    for o in objs:
        for v in o.data.vertices:
            if v.co.y > cy + 0.01:
                pos += 1
            elif v.co.y < cy - 0.01:
                neg += 1
    return 1.0 if pos >= neg else -1.0


def ridge_side(prefix):
    """+1/-1: which y end of a roof module is the high (ridge) end."""
    objs = base_set(prefix)
    lo, hi = group_bbox(objs)
    zcut = hi.z - (hi.z - lo.z) * 0.15
    acc = n = 0
    for o in objs:
        for v in o.data.vertices:
            if v.co.z >= zcut:
                acc += v.co.y - (lo.y + hi.y) / 2
                n += 1
    return 1.0 if (n == 0 or acc >= 0) else -1.0


def rot_to(authored_sign, out_vec):
    """Rz mapping the authored (0, authored_sign) direction onto out_vec."""
    ang = math.atan2(out_vec[1], out_vec[0]) - math.atan2(authored_sign, 0.0)
    return Matrix.Rotation(ang, 4, "Z")


def place(section, prefix, M):
    PLACEMENTS.append((section, prefix, M))


def wall(section, prefix, cx, cy, out, z):
    place(section, prefix,
          Matrix.Translation((cx, cy, z)) @ rot_to(detail_side(prefix), out))


def roof_metrics(p_slope, roof_base):
    """(tile_z at the eave, ridge_z) for a slope module seated at roof_base."""
    objs = base_set(p_slope)
    lo, hi = group_bbox(objs)
    tile = sub_obj(p_slope, ".roof") or objs[0]
    t_lo, t_hi = group_bbox([tile])
    return roof_base + (t_lo.z - lo.z), roof_base + (t_hi.z - lo.z)


def gable_z(p_gable, ridge_z):
    """Seat a gable so its 12-tri infill plate's apex lands on the ridge."""
    objs = base_set(p_gable)
    lo, _ = group_bbox(objs)
    plate = min(objs, key=tris)
    _, p_hi = group_bbox([plate])
    return ridge_z - (p_hi.z - lo.z)


def gable_quadrants(section, p_gable, x_end, y_eave, ridge_z):
    """Four mirrored half-gables (both ends x +-x_end, both roof halves)."""
    lo, hi = group_bbox(base_set(p_gable))
    run_half = (hi.y - lo.y) / 2
    base_M = (Matrix.Translation((x_end, y_eave + run_half + GABLE_RIDGE_OVERLAP,
                                 gable_z(p_gable, ridge_z)))
              @ rot_to(ridge_side(p_gable), (0, 1)))
    for mx in (1, -1):
        for my in (1, -1):
            place(section, p_gable, Matrix.Diagonal((mx, my, 1, 1)) @ base_M)


def gabled_roof(section, p_slope, p_edge, p_gable, xs, x_end, roof_base):
    """Slopes both sides of a ridge at y=0, eave strips, gables, ridge caps."""
    tile_z, ridge_z = roof_metrics(p_slope, roof_base)
    lo, hi = group_bbox(base_set(p_slope))
    run_half = (hi.y - lo.y) / 2
    r = ridge_side(p_slope)
    re_ = ridge_side(p_edge)
    for cx in xs:
        for sign in (1, -1):
            place(section, p_slope,
                  Matrix.Translation((cx, sign * (-3.0 + run_half), roof_base))
                  @ rot_to(r, (0, sign)))
            # eave strips pin by origin onto the eave line at the tile plane
            place(section, p_edge,
                  Matrix.Translation((cx, sign * -3.0, tile_z))
                  @ rot_to(re_, (0, sign)))
        cap_h = part_height(RIDGE_CAP)
        place(section, RIDGE_CAP,
              Matrix.Translation((cx, 0.0, ridge_z - cap_h / 2)))
    gable_quadrants(section, p_gable, x_end, -3.0, ridge_z)
    return ridge_z


def floors(section, xs, ys, z):
    for cx in xs:
        for cy in ys:
            place(section, FLOOR, Matrix.Translation((cx, cy, z)))


def build_house_01():
    """6x6, two 3 m storeys, 45-degree gable, ridge along X."""
    S = "lond_cirion_house_01"
    P_PLAIN = "gondor_wall_3m_a_clean"
    P_DOOR = "gondor_wall_3m_a_door_a_clean"
    P_WIN1 = "gondor_wall_3m_window_a_clean"
    P_WIN2 = "gondor_wall_3m_window2_a_clean"
    P_COL = "gondor_column_3m_a_1x1"
    P_TRIM = "gondor_wall_trim_3m_b_clean"
    P_SLOPE = "gondor_roof_a_45_3m_straight"
    P_GABLE = "gondor_roof_a_45_3m_side_a_clean"
    P_EDGE = "gondor_roof_a_45_3m_edge_straight"

    # (prefix, cx, cy, outward) — centrelines on the 6x6 footprint lines
    skirt = [(SKIRT, -1.5, -3.0, (0, -1)), (SKIRT, 1.5, -3.0, (0, -1)),
             (SKIRT, 3.0, -1.5, (1, 0)), (SKIRT, 3.0, 1.5, (1, 0)),
             (SKIRT, -1.5, 3.0, (0, 1)), (SKIRT, 1.5, 3.0, (0, 1)),
             (SKIRT, -3.0, -1.5, (-1, 0)), (SKIRT, -3.0, 1.5, (-1, 0))]
    story0 = [
        (P_DOOR, -1.5, -3.0, (0, -1)), (P_WIN1, 1.5, -3.0, (0, -1)),    # S
        (P_WIN1, 3.0, -1.5, (1, 0)), (P_PLAIN, 3.0, 1.5, (1, 0)),       # E
        (P_PLAIN, -1.5, 3.0, (0, 1)), (P_WIN1, 1.5, 3.0, (0, 1)),       # N
        (P_PLAIN, -3.0, -1.5, (-1, 0)), (P_WIN1, -3.0, 1.5, (-1, 0)),   # W
    ]
    story1 = [
        (P_WIN2, -1.5, -3.0, (0, -1)), (P_WIN2, 1.5, -3.0, (0, -1)),
        (P_WIN1, 3.0, -1.5, (1, 0)), (P_WIN1, 3.0, 1.5, (1, 0)),
        (P_WIN1, -1.5, 3.0, (0, 1)), (P_WIN1, 1.5, 3.0, (0, 1)),
        (P_WIN1, -3.0, -1.5, (-1, 0)), (P_WIN1, -3.0, 1.5, (-1, 0)),
    ]
    for z, panels in ((SKIRT_Z, skirt), (0.0, story0), (3.0, story1)):
        for prefix, cx, cy, out in panels:
            wall(S, prefix, cx, cy, out, z)

    # columns are decorative pilasters, not corner closure — the 3 m panels
    # already span each footprint line, so none is buried with the skirt
    for sx in (-3.0, 3.0):
        for sy in (-3.0, 3.0):
            for z in (0.0, 3.0):
                place(S, P_COL, Matrix.Translation((sx, sy, z)))

    # string courses: centred ON the wall centreline (authored intent), top
    # flush with each storey line
    d = detail_side(P_TRIM)
    th = part_height(P_TRIM)
    for top in (3.0, 6.0):
        for cx, cy, out in ((-1.5, -3.0, (0, -1)), (1.5, -3.0, (0, -1)),
                            (-1.5, 3.0, (0, 1)), (1.5, 3.0, (0, 1)),
                            (3.0, -1.5, (1, 0)), (3.0, 1.5, (1, 0)),
                            (-3.0, -1.5, (-1, 0)), (-3.0, 1.5, (-1, 0))):
            place(S, P_TRIM,
                  Matrix.Translation((cx, cy, top - th)) @ rot_to(d, out))

    # ground floor only — the storey-1 window panels are not pierced, so a
    # deck at z 3 would be invisible and would add unreachable collision
    floors(S, (-1.5, 1.5), (-1.5, 1.5), FLOOR_Z)
    return gabled_roof(S, P_SLOPE, P_EDGE, P_GABLE, (-1.5, 1.5), 3.0, 6.0)


def build_house_02():
    """12x6, one 6 m storey, 26.57-degree gable, ridge along X."""
    S = "lond_cirion_house_02"
    P_PLAIN = "gondor_wall_6m_a_clean"
    P_ARCH = "gondor_wall_6m_arch_c_clean"
    P_WIN3 = "gondor_wall_6m_window3_a_clean"
    P_COL = "gondor_column_3m_a_1x1"
    # _6m_b, not _6m_a: identical profile, no water-stain decal attached
    P_TRIM = "gondor_wall_trim_6m_b"
    P_SLOPE = "gondor_roof_a_30_3m_straight"
    P_GABLE = "gondor_roof_a_30_3m_side_a_clean"
    P_EDGE = "gondor_roof_a_30_3m_edge_straight"

    # 12 x 3 m skirt panels (NOT the 6 m P_PLAIN — see SKIRT)
    skirt = ([(SKIRT, cx, -3.0, (0, -1)) for cx in (-4.5, -1.5, 1.5, 4.5)]
             + [(SKIRT, cx, 3.0, (0, 1)) for cx in (-4.5, -1.5, 1.5, 4.5)]
             + [(SKIRT, 6.0, cy, (1, 0)) for cy in (-1.5, 1.5)]
             + [(SKIRT, -6.0, cy, (-1, 0)) for cy in (-1.5, 1.5)])
    # N window bay sits at -X while S's sits at +X so no aperture pair is
    # collinear; W stays the deliberate blank service elevation
    story0 = [(P_ARCH, -3.0, -3.0, (0, -1)), (P_WIN3, 3.0, -3.0, (0, -1)),
              (P_WIN3, -3.0, 3.0, (0, 1)), (P_PLAIN, 3.0, 3.0, (0, 1)),
              (P_WIN3, 6.0, 0.0, (1, 0)), (P_PLAIN, -6.0, 0.0, (-1, 0))]
    for z, panels in ((SKIRT_Z, skirt), (0.0, story0)):
        for prefix, cx, cy, out in panels:
            wall(S, prefix, cx, cy, out, z)

    for sx in (-6.0, 6.0):
        for sy in (-3.0, 3.0):
            for z in (0.0, 3.0):
                place(S, P_COL, Matrix.Translation((sx, sy, z)))

    d = detail_side(P_TRIM)
    th = part_height(P_TRIM)
    for cx, cy, out in ((-3.0, -3.0, (0, -1)), (3.0, -3.0, (0, -1)),
                        (-3.0, 3.0, (0, 1)), (3.0, 3.0, (0, 1)),
                        (6.0, 0.0, (1, 0)), (-6.0, 0.0, (-1, 0))):
        place(S, P_TRIM,
              Matrix.Translation((cx, cy, 6.0 - th)) @ rot_to(d, out))

    floors(S, (-4.5, -1.5, 1.5, 4.5), (-1.5, 1.5), FLOOR_Z)
    return gabled_roof(S, P_SLOPE, P_EDGE, P_GABLE,
                       (-4.5, -1.5, 1.5, 4.5), 6.0, 6.0)


def build_barracks():
    """18x6 barracks: two 3 m storeys, buttressed long walls, an external
    stair to an upper door, 26.57-degree gable. Ridge along X."""
    S = "lond_cirion_barracks_01"
    P_PLAIN = "gondor_wall_3m_a_clean"
    P_DOOR2 = "gondor_wall_3m_a_door2_a_clean"     # double door, main entry
    P_DOOR = "gondor_wall_3m_a_door_a_clean"       # single door, stair head
    P_WIN = "gondor_wall_3m_window_a_clean"
    P_WIN2 = "gondor_wall_3m_window2_a_clean"
    P_COL = "gondor_column_3m_a_1x1"
    P_TRIM = "gondor_wall_trim_3m_b_clean"
    P_BUTT = "gondor_buttress_a_clean"
    P_STAIR = "gondor_stairs_3m_a_clean"
    P_SLOPE = "gondor_roof_a_30_3m_straight"
    P_GABLE = "gondor_roof_a_30_3m_side_a_clean"
    P_EDGE = "gondor_roof_a_30_3m_edge_straight"

    BAYS = (-7.5, -4.5, -1.5, 1.5, 4.5, 7.5)       # 6 bays = 18 m
    END = 9.0                                       # short-face centreline
    HALF = 3.0                                      # half depth
    STAIR_X = 4.5                                   # bay carrying the stair

    def face(prefixes, cy, out):
        return [(p, cx, cy, out) for p, cx in zip(prefixes, BAYS)]

    skirt = (face([P_PLAIN] * 6, -HALF, (0, -1))
             + face([P_PLAIN] * 6, HALF, (0, 1))
             + [(P_PLAIN, END, cy, (1, 0)) for cy in (-1.5, 1.5)]
             + [(P_PLAIN, -END, cy, (-1, 0)) for cy in (-1.5, 1.5)])
    skirt = [(SKIRT, cx, cy, out) for _, cx, cy, out in skirt]

    # south is the parade front: windows + the double-door main entry
    story0 = (face([P_WIN, P_WIN, P_DOOR2, P_WIN, P_WIN, P_WIN], -HALF, (0, -1))
              # north is the deliberate blank service wall (no sightlines)
              + face([P_PLAIN] * 6, HALF, (0, 1))
              + [(P_PLAIN, END, cy, (1, 0)) for cy in (-1.5, 1.5)]
              + [(P_PLAIN, -END, cy, (-1, 0)) for cy in (-1.5, 1.5)])
    # upper: dormitory rhythm; the single door at STAIR_X is the stair head
    story1 = (face([P_WIN2, P_WIN2, P_WIN2, P_WIN2, P_DOOR, P_WIN2],
                   -HALF, (0, -1))
              + face([P_WIN] * 6, HALF, (0, 1))
              + [(P_WIN, END, cy, (1, 0)) for cy in (-1.5, 1.5)]
              + [(P_WIN, -END, cy, (-1, 0)) for cy in (-1.5, 1.5)])
    for z, panels in ((SKIRT_Z, skirt), (0.0, story0), (3.0, story1)):
        for prefix, cx, cy, out in panels:
            wall(S, prefix, cx, cy, out, z)

    for sx in (-END, END):
        for sy in (-HALF, HALF):
            for z in (0.0, 3.0):
                place(S, P_COL, Matrix.Translation((sx, sy, z)))

    # buttresses on the bay joints of both long walls, mounted on the wall
    # OUTER face (origin-anchored: the body hangs off that plane, z 0..6)
    for cx in (-6.0, -3.0, 0.0, 3.0, 6.0):
        wall(S, P_BUTT, cx, -(HALF + 0.25), (0, -1), 0.0)
        wall(S, P_BUTT, cx, HALF + 0.25, (0, 1), 0.0)

    # External stair up to the upper door. MEASURED: the high tread is at
    # local -Y (y -3.02, z 3.01) and the foot at y 0 — the opposite of what
    # the part's bbox suggests, so an unrotated placement makes the stair
    # climb AWAY from the building. Rz(180) maps the high end to +Y, then
    # the translation lands it: high -> (STAIR_X, wall face, 3), foot ->
    # 3 m further out at grade. detail_side() cannot decide this (the whole
    # part sits on one side of its origin), so the matrix is explicit.
    place(S, P_STAIR,
          Matrix.Translation((STAIR_X, -(HALF + 0.25) - 3.0, 0.0))
          @ Matrix.Rotation(math.pi, 4, "Z"))

    d = detail_side(P_TRIM)
    th = part_height(P_TRIM)
    for top in (3.0, 6.0):
        for cx in BAYS:
            for cy, out in ((-HALF, (0, -1)), (HALF, (0, 1))):
                place(S, P_TRIM,
                      Matrix.Translation((cx, cy, top - th)) @ rot_to(d, out))
        for cy in (-1.5, 1.5):
            for cx, out in ((END, (1, 0)), (-END, (-1, 0))):
                place(S, P_TRIM,
                      Matrix.Translation((cx, cy, top - th)) @ rot_to(d, out))

    # both decks: unlike house_01 the upper windows here ARE pierced, so the
    # storey-1 floor is visible from outside and worth placing
    floors(S, BAYS, (-1.5, 1.5), FLOOR_Z)
    floors(S, BAYS, (-1.5, 1.5), 3.0 + FLOOR_Z)
    return gabled_roof(S, P_SLOPE, P_EDGE, P_GABLE, BAYS, END, 6.0)


def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def render_views(section, staging):
    """Framed ortho elevations + top + two three-quarters + a ridge close-up."""
    scene = bpy.context.scene
    body = bpy.data.objects[section]
    for o in scene.objects:
        if o.type == "MESH":
            o.hide_render = o.name != section
    lo, hi = group_bbox([body])
    ctr = (lo + hi) / 2
    size = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    cam = bpy.data.objects["cam"]
    cam_data = cam.data
    res_x, res_y = scene.render.resolution_x, scene.render.resolution_y
    vfov = 2.0 * math.atan(0.5 * cam_data.sensor_width * res_y / res_x
                           / cam_data.lens)
    dist = (0.5 * size) / math.tan(vfov / 2) * 1.45

    shots = {
        "elev_front_negY": ("ORTHO", Vector((0, -1, 0))),
        "elev_rear_posY": ("ORTHO", Vector((0, 1, 0))),
        "elev_left_negX": ("ORTHO", Vector((-1, 0, 0))),
        "elev_right_posX": ("ORTHO", Vector((1, 0, 0))),
        "elev_top": ("ORTHO", Vector((0, 0, 1))),
        "persp_SE": ("PERSP", Vector((1.0, -1.1, 0.6))),
        "persp_NW": ("PERSP", Vector((-1.0, 1.1, 0.6))),
    }
    for nm, (kind, direction) in shots.items():
        cam_data.type = kind
        cam_data.ortho_scale = size * 1.25
        d = direction.normalized()
        cam.location = ctr + d * (size * 2.5 if kind == "ORTHO" else dist)
        up = "Y" if abs(d.z) < 0.99 else "X"
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", up).to_euler()
        scene.render.filepath = os.path.join(staging, f"{section}__{nm}.png")
        bpy.ops.render.render(write_still=True)
        log(f"[render] {section}__{nm}.png")
    # ridge/gable close-up: look at the +X gable apex
    cam_data.type = "PERSP"
    target = Vector((hi.x - 0.5, 0.0, hi.z - 1.0))
    cam.location = target + Vector((1.0, -1.0, 0.45)).normalized() * (size * 0.85)
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(staging, f"{section}__close_gable.png")
    bpy.ops.render.render(write_still=True)
    log(f"[render] {section}__close_gable.png")


def main():
    global REPORT_DIR
    os.makedirs(STAGING, exist_ok=True)
    REPORT_DIR = os.path.join(STAGING, "_report")
    os.makedirs(REPORT_DIR, exist_ok=True)
    done_path = os.path.join(REPORT_DIR, "DONE.txt")
    if os.path.exists(done_path):
        os.remove(done_path)

    summary = {"status": "error"}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        for fname in FAMILIES:
            path = os.path.join(GONDOR, "meshes", fname)
            try:
                bpy.ops.wm.fbx_import(filepath=path)
            except Exception:
                bpy.ops.import_scene.fbx(filepath=path)
        bpy.context.view_layer.update()

        # world-bake every import so bboxes/anchors live in file space
        for o in list(bpy.context.scene.objects):
            if o.type != "MESH":
                continue
            if o.data.users > 1:
                o.data = o.data.copy()
            mw = o.matrix_world.copy()
            o.data.transform(mw)
            if mw.determinant() < 0:
                o.data.flip_normals()
            o.matrix_world = Matrix.Identity(4)
        bpy.context.view_layer.update()

        # material .NNN dedup (the editor binds by exact name)
        for mat in list(bpy.data.materials):
            m = re.match(r"^(.*)\.\d{3}$", mat.name)
            if not m:
                continue
            base = bpy.data.materials.get(m.group(1))
            if base is not None:
                mat.user_remap(base)
                bpy.data.materials.remove(mat)
            else:
                mat.name = m.group(1)

        by_name.update({o.name: o for o in bpy.context.scene.objects
                        if o.type == "MESH"})

        ridges = {"lond_cirion_house_01": build_house_01(),
                  "lond_cirion_house_02": build_house_02(),
                  "lond_cirion_barracks_01": build_barracks()}
        for s, rz in ridges.items():
            log(f"[plan] {s}: ridge z {rz:.3f}, "
                f"{sum(1 for p in PLACEMENTS if p[0] == s)} placements")
        sections = list(ridges)

        outputs = []
        stats = {}
        for tier in ("base", "lod3", "lod6", "bo"):
            for section in sections:
                dups = []
                expect = 0
                for sec, prefix, M in PLACEMENTS:
                    if sec != section:
                        continue
                    objs = tier_objs(prefix, tier)
                    if not objs:
                        WARNINGS.append(f"{prefix}: no {tier} objects")
                        continue
                    a = anchor(prefix)
                    pm = M @ Matrix.Translation(-a)
                    for src in objs:
                        expect += tris(src)
                        dup = src.copy()
                        dup.data = src.data.copy()
                        dup.matrix_world = pm
                        if pm.determinant() < 0:
                            dup.data.flip_normals()
                        bpy.context.scene.collection.objects.link(dup)
                        dups.append(dup)
                mesh = bpy.data.meshes.new("join_target")
                target = bpy.data.objects.new("join_target", mesh)
                bpy.context.scene.collection.objects.link(target)
                bpy.context.view_layer.update()
                select_only(dups + [target])
                bpy.context.view_layer.objects.active = target
                bpy.ops.object.join()
                joined = bpy.context.view_layer.objects.active
                name = (f"bo_{section}" if tier == "bo"
                        else section + TIER_SUFFIX[tier])
                joined.name = joined.data.name = name
                if tier == "bo":
                    joined.data.materials.clear()
                    stone = (bpy.data.materials.get("stone")
                             or bpy.data.materials.new("stone"))
                    joined.data.materials.append(stone)
                bpy.context.view_layer.update()
                got = tris(joined)
                lo, hi = group_bbox([joined])
                stats[name] = {
                    "tris": got, "expected_sum": expect,
                    "dims_m": [round(v, 3) for v in joined.dimensions],
                    "z_range": [round(lo.z, 3), round(hi.z, 3)],
                }
                if got != expect:
                    WARNINGS.append(f"{name}: tris {got} != part sum {expect}")
                log(f"[buildings] {name}: {stats[name]}")
                outputs.append(joined)

        for w in sorted(set(WARNINGS)):
            log(f"[warn] {w}")

        select_only(outputs)
        bpy.ops.export_scene.fbx(
            filepath=OUT_FBX, use_selection=True, object_types={"MESH"},
            use_mesh_modifiers=True, bake_space_transform=True,
            add_leaf_bones=False, path_mode="AUTO")
        log(f"[export] {OUT_FBX}")

        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 40
        scene.render.resolution_x = 1500
        scene.render.resolution_y = 1050
        sun_data = bpy.data.lights.new("sun", type="SUN")
        sun_data.energy = 3.5
        sun_data.angle = 0.3
        sun = bpy.data.objects.new("sun", sun_data)
        scene.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(52), 0.0, math.radians(-35))
        world = bpy.data.worlds.new("w")
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.4, 0.42, 0.45, 1.0)
        scene.world = world
        cam_data = bpy.data.cameras.new("cam")
        cam_data.lens = 50.0
        cam = bpy.data.objects.new("cam", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        for section in sections:
            render_views(section, STAGING)

        summary = {"status": "ok", "out": OUT_FBX, "stats": stats,
                   "warnings": sorted(set(WARNINGS))}
    except Exception:
        log(traceback.format_exc())
        summary["trace"] = traceback.format_exc(limit=3)

    with open(done_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=1)
    log(f"[done] {summary['status']}")


main()
