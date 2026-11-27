"""Show kingdom kit pieces with their texture sets: renders, and a VIEW .blend to orbit.

The pieces stand in one row on a ground plane, each with a 1.8 m figure. A piece that has UVs and
final material names (t_rivendell_wall_stone_a and so on) shows its textures through those UVs.
A blockout has no UVs and placeholder material names (rivendell_stone and so on); it borrows the
same sets through a box projection in world space at the set's own density, so every piece of the
set can be seen in one look before all of them are final. A box projection is a preview: it has no
trim layout and no normal map.

Headless (the Store launcher detaches: wait for <out>/_report/DONE.txt):

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup
        -P tools/oneoff/blender_view_textured.py --
        --piece [label=]<a.fbx> [--piece [label=]<b.fbx> ...] --tex <folder of <set>_d/_n/_s.png>
        --out <dir> [--gap 4] [--res-x 1920] [--res-y 1080] [--save-blend <VIEW.blend>]
        [--masonry-uv-scale 0.6] [--normal-green up|down]
        [--sun main|low]
        [--control <fbx> --control-tex <folder> [--control-lift M] [--control-normal-green up|down]
            [--control-gap 3]]
        [--vertex-colour on|off] [--close-piece <label>]
        [--detail-normal <png> [--detail-scale N]]

Shown: the visual LOD0 meshes only (no .lodN objects, no bo_ bodies). Writes <out>/all_3q_outside.png,
all_3q_inside.png, all_front.png, all_front_far.png (twice the distance of all_front, to judge the
repeat and the joints at a game camera distance) and one <label>_3q.png per piece, then the close views
(face_close, face_close_sun, face_close_graze) and the three warm views of round 5 (see below).

Arguments added in round 2a (every one is optional, the old calls work as before):

--masonry-uv-scale F   a factor on the mesh UVs of the stone and rubble sets (0.6 shows 0.45 UV per
                       metre on a 0.75 mesh). The trim sheet never scales.
--normal-green         the green sense of the sets' normal maps. The engine form is down.
--sun low              every view is also rendered with a low raking sun (18 degrees above the
                       horizon, coming along the wall at 25 degrees to its face, from the -X end)
                       into <name>_low.png. Bevels, joint depth and normal strength read only there.
--control FBX          a control piece with its own textures, in the same scene, at the +X end of
--control-tex DIR      the row. --control-tex is the folder of its textures. Each material finds
                       its files through the Gondor material records (--control-dump,
                       --control-textures-dump; slots 0 diffuse, 2 normal, 4 specular); a material
                       whose texture ids are not in the texture record falls back to the files named
                       after it (route "name" in the report, a guess), and one with no file is a
                       flat grey. Decal materials are not drawn. --control-lift raises it (the Gondor
                       wall: -3, so its deck is level with ours at 12 m). --control-normal-green
                       (default down). --control-gap is the space to our row (default 3 m). The control
                       is hidden in the usual views and shown, with a figure, in two more:
                       pair_outside (the last piece beside the control, from the outside) and
                       pair_close (level at 1.7 m, the gap and 3 m of each face).
--vertex-colour on|off a mesh with a colour attribute has its base colour multiplied by it, as the
                       engine does with the flag use_vertex_colors (default on).
--detail-normal PNG    a second, finer normal map for the stone and rubble sets: its x and y are added
--detail-scale N       to the base normal's x and y, the base z is kept, the result is normalised.
                       The detail UV is the base UV times N (default 1, the engine's own default).
                       Green down when --normal-green is down.

Added in round 2b: the view face_close (and face_close_low with --sun low). The camera stands level at the
eye height of a standing figure (1.7 m) above the plinth top (z 3.0), 2.5 m in front of the middle of the
outer face (y 2.5) of the last --piece, so it looks at the bay field between two piers.

Example, two walls and the Gondor control in both lights:

    blender-launcher.exe -b --factory-startup -P tools/oneoff/blender_view_textured.py --
        --piece a=<wall.fbx> --piece b=<wall.fbx> --gap 0 --tex <set folder> --masonry-uv-scale 0.6
        --normal-green down --sun low --control <gondor_castle_wall_20m_l3_a.fbx>
        --control-tex <Gondor textures> --control-lift -3 --out <dir>

Added in round 4:

Two more final sets, dressed through the mesh UVs like the stone set (the detail normal and the
masonry UV scale treat them the same way): t_rivendell_roof_slate_a (roof slate) and
t_rivendell_wall_heap_a (rubble with no courses). A set that is not in --tex falls back: the roof slate
to the flat colour #34405f, the heap to the files of t_rivendell_wall_rubble_a. The report lists each
fallback under "fallbacks". The sets are now five: stone, rubble (the plinth), trim, roof slate, heap.

face_close_sun: the camera of face_close with its own sun, 45 degrees from the face normal toward the
-X end and 40 degrees above the horizon, energy 2 like the main sun. Written with every run, with no
_low copy. The main sun of face_close stands 38 degrees up, 28 degrees round Z from the face normal, so
it meets the face at cosine 0.70 and the face is in direct sun there; face_close_sun meets it at 0.54.
The report key "light" holds both setups and the sky.

pair_face (and pair_face_low with --sun low), with --control: level at Z 5.5, 6 m in front of the
outer faces, aimed at the seam. For this view the control moves by the gap so that the faces butt: the
left half of the frame is our ashlar above the plinth, the right half the control's face at the same
height. pair_close is unchanged.

Added in round 5 (every call of the older rounds works as before):

--close-piece LABEL    the piece that the close views (face_close, face_close_sun, face_close_graze,
                       face_close_warm and the _low copy) aim at. Before round 5 they aimed at the last
                       piece, which with a gatehouse last was the view through its arch. Without the
                       argument the tool takes the first piece whose file name holds "wall_20m_a" and not
                       "_dest" (a solid wall), and if there is none, the first piece. The report key
                       "close_piece" names the piece that was used. The pair views still use the last piece.
face_close_graze       the camera of face_close, lit by one sun that meets the face at cosine 0.2 (12
                       degrees from the face plane), from the -X side, 30 degrees above the horizon, energy
                       5, a 2 degree disc, with the sky as it is. Written with every run, no _low copy.
a_3q_warm, face_close_warm, all_front_warm
                       the cameras of the first piece's _3q view, of face_close and of all_front, under the
                       main sun in a colour of about 5500 K (linear RGB 1.0, 0.93, 0.84, energy and
                       direction as the main sun) and the sky at strength 0.3 (0.4 in the other views).
                       Written with every run, no _low copy. The present views do not change.
Report keys: "light" now holds main_sun, face_sun, graze_sun, warm and sky; "colour_attributes" lists, per
object of each piece, the colour attributes that the FBX import gave it (name, domain, data type, count,
and the value range as Blender reads it and as the stored byte value: Blender gives a byte colour as a
linear value, so a stored 0.80 reads 0.604).

Tone warning: Blender does not show these maps as the engine does. Judge colour and material in
the Modding Kit beside the control piece (docs/kitbash/kingdom-kits-prompt.md, Phase 5).
"""
import argparse
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
sys.path.insert(0, _HERE)
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402
from mesh_preflight import classify_name  # noqa: E402  (bpy is imported lazily there)
from blender_render_concept_pair import (  # noqa: E402
    FIGURE_SIZE, above_ground, perspective_distance, srgb_hex_to_linear, union_box)

UV_PER_METRE = 0.75  # the density every set is authored for (15 tiles along a 20 m wall)
# The five strips of the trim sheet, as (v_low, v_high) with v = 0 at the bottom of the image.
STRIPS = {"S1": (0.0, 0.28), "S2": (0.28, 0.49), "S3": (0.49, 0.7), "S4": (0.7, 0.805), "S5": (0.805, 1.0)}
FINAL_SETS = ("t_rivendell_wall_stone_a", "t_rivendell_wall_rubble_a", "t_rivendell_wall_trim_a",
              "t_rivendell_roof_slate_a", "t_rivendell_wall_heap_a")
# What a set shows when its files are not in the --tex folder (round 4: both sets are new).
SET_FALLBACKS = {
    "t_rivendell_roof_slate_a": {"flat": "#34405f"},
    "t_rivendell_wall_heap_a": {"set": "t_rivendell_wall_rubble_a"},
}
BLOCKOUT_ROUTES = {
    "rivendell_stone": {"set": "t_rivendell_wall_stone_a", "mode": "box"},
    "rivendell_rubble": {"set": "t_rivendell_wall_rubble_a", "mode": "box"},
    "rivendell_trim": {"set": "t_rivendell_wall_trim_a", "mode": "box_strip", "strip": STRIPS["S1"]},
    "rivendell_slate": {"mode": "flat", "color": "#34405f"},  # Rivendell map icon, roof
    "rivendell_wood": {"mode": "flat", "color": "#c8b6a1"},   # elven kit, bleached wood
    "rivendell_gate": {"mode": "flat", "color": "#8a8f96"},   # placeholder for the vanilla gates
}
GROUND_COLOR = "#8d9080"
FIGURE_COLOR = "#1f1f22"
SKY_COLOR = "#cfd9e4"
FOV = math.radians(40.0)
# The low raking sun: 18 degrees above the horizon, its horizontal part at 25 degrees to the wall face.
LOW_SUN_ELEVATION_DEG = 18.0
LOW_SUN_FACE_ANGLE_DEG = 25.0
FAR_FACTOR = 2.0  # all_front_far stands this many times as far as all_front
LOW_SUN_ENERGY = 3.0  # the light meets the face at a shallow angle, so it needs more than the main sun
# The control piece. Its material records: what each Gondor material reads in each texture slot
# (0 diffuse, 2 normal, 4 specular), and what each texture id is called.
_ENGINE_RECORDS = "E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/painter_lab/research/engine"
CONTROL_MATERIALS_DUMP = os.path.join(_ENGINE_RECORDS, "gondor_materials_dump.json")
CONTROL_TEXTURES_DUMP = os.path.join(_ENGINE_RECORDS, "gondor_bricks_dump.json")
CONTROL_SLOTS = {"d": "0", "n": "2", "s": "4"}
CONTROL_GREY = "#aaa8a6"  # a control material with no textures: the measured mean of the Gondor wall stone
EYE_HEIGHT = 1.7  # a standing figure's eyes
EYE_FACE_SHOWN = 3.0  # metres of each face that the eye level view shows beside the gap
FACE_Y = 2.5  # the outer face of the wall body in a piece's own frame (rivendell_joint_profile Y_OUT)
PLINTH_TOP_Z = 3.0  # the top of the plinth: the bay field starts here
FACE_CLOSE_STANDOFF = 2.5  # metres in front of the face for the face_close view
PAIR_FACE_STANDOFF = 6.0  # metres in front of the two outer faces for pair_face
PAIR_FACE_Z = 5.5  # the middle of the bay field of our wall
# The main sun's rotation (Euler XYZ, degrees), as _setup sets it. A sun shines along its local -Z.
MAIN_SUN_ROTATION_DEG = (-52.0, 0.0, 28.0)
MAIN_SUN_ENERGY = 2.0
# The sun of face_close_sun: 40 degrees above the horizon, its horizontal part 45 degrees to the face normal.
FACE_SUN_ELEVATION_DEG = 40.0
FACE_SUN_SIDE_DEG = 45.0
FACE_SUN_ENERGY = 2.0
# The grazing sun of face_close_graze: it meets the face at cosine 0.2 (12 degrees from the face plane),
# comes from the low X side and stands 30 degrees above the horizon.
GRAZE_FACE_COSINE = 0.2
GRAZE_ELEVATION_DEG = 30.0
GRAZE_SUN_ENERGY = 5.0  # the light is shallow on the face (cosine 0.2), so it needs more than the main sun
# The warm views: the main sun in a colour of about 5500 K (linear RGB) and a weaker sky.
WARM_SUN_COLOUR = (1.0, 0.93, 0.84)
WARM_SKY_STRENGTH = 0.3
SKY_STRENGTH = 0.4
CLOSE_PIECE_NAME = "wall_20m_a"


# ------------------------------------------------------------------ pure helpers

def material_route(name):
    """How a material is shown: its own set through UVs, a borrowed set by projection, a flat
    colour, or left as it is."""
    key = name.lower().split(".")[0]
    if key in FINAL_SETS:
        return {"set": key, "mode": "uv"}
    if key in BLOCKOUT_ROUTES:
        return dict(BLOCKOUT_ROUTES[key])
    return {"mode": "keep"}


def resolve_set(tex_dir, set_name):
    """What a final set shows. {'mode': 'files', 'files': {...}, 'note': None or text} when its
    diffuse is in the folder or its fallback set has one; {'mode': 'flat', 'color', 'note'} for a flat
    fallback; {'mode': 'none', 'note'} when there is nothing. A note names a fallback that was used."""
    files = texture_files(tex_dir, set_name)
    if files["d"]:
        return {"mode": "files", "files": files, "note": None}
    fallback = SET_FALLBACKS.get(set_name, {})
    if "flat" in fallback:
        return {"mode": "flat", "color": fallback["flat"],
                "note": f"{set_name}: no files in the texture folder, flat {fallback['flat']}"}
    if "set" in fallback:
        other = texture_files(tex_dir, fallback["set"])
        if other["d"]:
            return {"mode": "files", "files": other,
                    "note": f"{set_name}: no files in the texture folder, shows {fallback['set']}"}
    return {"mode": "none", "note": f"{set_name}: no diffuse file, left as it is"}


def texture_files(tex_dir, set_name):
    """The diffuse, normal and specular files of a set; None for a map that is not there."""
    out = {}
    for key in ("d", "n", "s"):
        path = os.path.join(tex_dir, f"{set_name}_{key}.png")
        out[key] = path if os.path.exists(path) else None
    return out


def row_offsets(boxes, gap):
    """X offsets that stand the pieces side by side in the given order, `gap` apart, centred on 0."""
    if not boxes:
        return []
    total = sum(b[1][0] - b[0][0] for b in boxes) + gap * (len(boxes) - 1)
    cursor = -total / 2.0
    out = []
    for lo, hi in boxes:
        out.append(cursor - lo[0])
        cursor += (hi[0] - lo[0]) + gap
    return out


def parse_piece(text):
    """('label', 'path') from 'label=path' or from a bare path (the label is the file stem)."""
    if "=" in text and not os.path.exists(text):
        label, path = text.split("=", 1)
        return label, path
    return os.path.splitext(os.path.basename(text))[0], text


def material_key(name):
    """A material name as the dump spells it: lower case, without Blender's .001 duplicate suffix."""
    return name.lower().split(".")[0]


def is_decal(name):
    """A decal material (dirt, leaks) is a blended overlay that this preview does not draw."""
    return "decal" in material_key(name)


def parse_slots(text):
    """{'0': id, '2': id, ...} from the dump's 'slot=id;slot=id' text."""
    out = {}
    for part in text.split(";"):
        if "=" in part:
            slot, guid = part.split("=", 1)
            out[slot] = guid
    return out


def _file_if_there(tex_dir, base):
    path = os.path.join(tex_dir, base)
    return path if os.path.exists(path) else None


def control_textures(name, materials, textures, tex_dir):
    """The diffuse, normal and specular files of a control material, and how they were found.
    route 'dump': the material's slots name texture ids, and the texture dump names their files.
    route 'name': the ids are not in the texture dump, so the files named after the material are
    used when they are there (m_gondor_stone_trim_a1 reads t_gondor_stone_trim_a1_d.png), a guess
    to be read as one. route 'none': no texture, the caller shows a flat grey."""
    key = material_key(name)
    sources = {t["guid"]: t["source"].replace("\\", "/").rsplit("/", 1)[-1] for t in textures}
    slots = {}
    for m in materials:
        if m["item"].lower() == key:
            slots = parse_slots(m["slots"])
            break
    found = {ch: None for ch in CONTROL_SLOTS}
    for ch, slot in CONTROL_SLOTS.items():
        base = sources.get(slots.get(slot, ""))
        if base:
            found[ch] = _file_if_there(tex_dir, base)
    if found["d"]:
        found["route"] = "dump"
        return found
    core = key[2:] if key.startswith("m_") else key
    core = core[:-4] if core.endswith("_mat") else core
    core = core if core.startswith("t_") else "t_" + core
    for ch in CONTROL_SLOTS:
        found[ch] = _file_if_there(tex_dir, f"{core}_{ch}.png")
    found["route"] = "name" if found["d"] else "none"
    return found


def control_offset(row_box, control_box, gap, lift):
    """(dx, dy, dz) that stands the control at the +X end of the row, `gap` metres from it, with its
    deck raised by `lift`. Both keep their own frame, so +Y stays the outer face of each."""
    return (row_box[1][0] + gap - control_box[0][0], 0.0, lift)


def eye_view(x, face_y, standoff, eye_height=EYE_HEIGHT):
    """(camera location, target) of a view from the outside at standing height, level, at the faces."""
    return (x, face_y + standoff, eye_height), (x, face_y, eye_height)


def face_close_view(x, face_y=FACE_Y, base_z=PLINTH_TOP_Z, standoff=FACE_CLOSE_STANDOFF, eye_height=EYE_HEIGHT):
    """(camera location, target) of the face_close view: level, at the eye height of a standing figure
    above `base_z` (the top of the plinth), `standoff` metres in front of the wall face at x."""
    z = base_z + eye_height
    return (x, face_y + standoff, z), (x, face_y, z)


def pair_face_view(x, face_y=FACE_Y, z=PAIR_FACE_Z, standoff=PAIR_FACE_STANDOFF):
    """(camera location, target) of pair_face: level at height z, `standoff` metres in front of the
    two outer faces, aimed at x (the seam between the faces)."""
    return (x, face_y + standoff, z), (x, face_y, z)


def sun_direction_from_euler(rotation_deg):
    """The unit vector from the scene toward a sun with this Euler XYZ rotation (degrees). The sun
    shines along its local -Z, so the way to it is the rotated +Z."""
    ax, ay, az = (math.radians(a) for a in rotation_deg)
    x, y, z = 0.0, 0.0, 1.0
    y, z = y * math.cos(ax) - z * math.sin(ax), y * math.sin(ax) + z * math.cos(ax)
    x, z = x * math.cos(ay) + z * math.sin(ay), -x * math.sin(ay) + z * math.cos(ay)
    x, y = x * math.cos(az) - y * math.sin(az), x * math.sin(az) + y * math.cos(az)
    return (x, y, z)


def main_sun_direction():
    """The unit vector toward the main sun of every view."""
    return sun_direction_from_euler(MAIN_SUN_ROTATION_DEG)


def face_cosine(direction):
    """How squarely a light meets the outer face (normal +Y): 1 head on, 0 grazing, below 0 behind."""
    return direction[1]


def face_sun_direction(elevation_deg=FACE_SUN_ELEVATION_DEG, side_deg=FACE_SUN_SIDE_DEG, from_x=-1):
    """The unit vector toward the sun of face_close_sun: it leans `side_deg` from the face normal
    toward the -X end (+X when from_x is 1) and stands `elevation_deg` above the horizon."""
    el = math.radians(elevation_deg)
    side = math.radians(side_deg)
    horizontal = math.cos(el)
    return (math.copysign(horizontal * math.sin(side), from_x), horizontal * math.cos(side), math.sin(el))


def graze_sun_direction(face_cosine_value=GRAZE_FACE_COSINE, elevation_deg=GRAZE_ELEVATION_DEG, from_x=-1):
    """The unit vector toward the grazing sun of face_close_graze: its Y part is the cosine to the
    outer face, its Z part is the elevation, and the rest runs along X (from the -X end by default)."""
    z = math.sin(math.radians(elevation_deg))
    along = math.sqrt(max(0.0, 1.0 - face_cosine_value ** 2 - z ** 2))
    return (math.copysign(along, from_x), face_cosine_value, z)


def close_piece_index(pieces, label=None):
    """The index of the piece that the close views aim at. `pieces` is a list of (label, path). A given
    label wins (ValueError when no piece has it). Without one: the first piece whose file name holds
    "wall_20m_a" and not "_dest" (a solid wall), else the first piece."""
    if label is not None:
        for i, (name, _) in enumerate(pieces):
            if name == label:
                return i
        raise ValueError(f"--close-piece {label}: no piece has this label")
    for i, (_, path) in enumerate(pieces):
        stem = os.path.basename(path).lower()
        if CLOSE_PIECE_NAME in stem and "_dest" not in stem:
            return i
    return 0


def scaled_distance(distance, factor):
    return distance * factor


def ground_span(box):
    """The side of the ground plane: it lies under every camera, the far front view included."""
    half = math.sqrt(sum((box[1][k] - box[0][k]) ** 2 for k in range(3))) / 2.0
    far = scaled_distance(perspective_distance(half, FOV), FAR_FACTOR)
    return max(max(box[1][0] - box[0][0], box[1][1] - box[0][1]) * 3.0 + 60.0, 2.0 * far + 20.0)


def uses_detail(set_name):
    """The detail normal map goes on the tiling stone and rubble sets, not on the trim sheet."""
    return "_trim_" not in set_name


def detail_uv_scale(masonry_factor, detail_scale):
    """The detail's factor on the mesh UVs: the base UV (with the density preview factor) times the
    detail scale."""
    return masonry_factor * detail_scale


# Blender gives a byte colour attribute to a material as a linear value (0.80 stored reads 0.604).
# The engine's shader multiplies the diffuse by the stored value (standart.rsh: albedo_color.rgb *
# vertex_color.rgb, and get_vertex_color only swaps channels), so the preview turns it back.
VERTEX_COLOUR_GAMMA = 1.0 / 2.2


def vertex_colour_wanted(mode, has_attribute):
    """The engine multiplies the diffuse by the mesh's vertex colour (material flag use_vertex_colors).
    The preview does it when the argument is on and the mesh has a colour attribute."""
    return mode == "on" and has_attribute


def eye_standoff(gap, face_shown=EYE_FACE_SHOWN):
    """How far from the faces the eye level camera stands so that its picture is as wide as the gap
    plus `face_shown` metres of each face."""
    return (gap + 2.0 * face_shown) / (2.0 * math.tan(FOV / 2.0))


def low_sun_direction(elevation_deg=LOW_SUN_ELEVATION_DEG, face_angle_deg=LOW_SUN_FACE_ANGLE_DEG, from_x=-1):
    """The unit vector from the scene toward the low sun. The wall face is the XZ plane and its
    outside is +Y, so the horizontal part of the light runs along the wall (from the -X end when
    from_x is -1, from the +X end when it is 1) and leans toward +Y by `face_angle_deg`."""
    el = math.radians(elevation_deg)
    az = math.radians(face_angle_deg)
    horizontal = math.cos(el)
    return (math.copysign(horizontal * math.cos(az), from_x), horizontal * math.sin(az), math.sin(el))


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    p.add_argument("--piece", action="append", required=True, help="[label=]path to an FBX; repeatable")
    p.add_argument("--tex", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--gap", type=float, default=4.0)
    p.add_argument("--res-x", type=int, default=1920)
    p.add_argument("--res-y", type=int, default=1080)
    p.add_argument("--save-blend")
    p.add_argument("--masonry-uv-scale", type=float, default=1.0,
                   help="a factor on the mesh UVs of the stone and rubble sets: a preview of another "
                        "texture density without a rebuild (0.6 shows 0.45 UV per metre on a 0.75 mesh)")
    p.add_argument("--normal-green", choices=("up", "down"), default="up",
                   help="the green sense of the sets' normal maps. The engine form is down (DirectX); "
                        "Blender reads up, so down is turned over in the material")
    p.add_argument("--sun", choices=("main", "low"), default="main",
                   help="main: the present light only. low: every view is also rendered with a low raking "
                        "sun (18 degrees up, 25 degrees to the wall face) into <name>_low.png")
    p.add_argument("--vertex-colour", choices=("on", "off"), default="on",
                   help="on: a mesh with a colour attribute has its base colour multiplied by it, as the "
                        "engine does with use_vertex_colors. off: the attribute is ignored")
    p.add_argument("--close-piece", help="the label of the piece that the close views aim at (default: the "
                   "first piece whose file name holds wall_20m_a and not _dest, else the first piece)")
    p.add_argument("--detail-normal", help="a finer second normal map for the stone and rubble sets, added to "
                   "the base normal as the engine does (green down when --normal-green is down)")
    p.add_argument("--detail-scale", type=float, default=1.0,
                   help="how often the detail repeats inside the base tile (the engine's DetailnormalScale)")
    p.add_argument("--control", help="FBX of a control piece shown beside ours with its own textures")
    p.add_argument("--control-tex", help="folder of the control's textures (needed with --control)")
    p.add_argument("--control-lift", type=float, default=0.0,
                   help="metres to raise the control (the Gondor wall: -3, so its deck is level with ours)")
    p.add_argument("--control-normal-green", choices=("up", "down"), default="down",
                   help="the green sense of the control's normal maps (the Gondor maps are down)")
    p.add_argument("--control-gap", type=float, default=3.0, help="metres between our row and the control")
    p.add_argument("--control-dump", default=CONTROL_MATERIALS_DUMP,
                   help="the material records that name each control material's texture ids")
    p.add_argument("--control-textures-dump", default=CONTROL_TEXTURES_DUMP,
                   help="the records that name each texture id's file")
    args = p.parse_args(argv)
    if args.control and not args.control_tex:
        p.error("--control needs --control-tex")
    return args


def uv_scale_for(set_name, factor):
    """The factor for a tiling stone or rubble set. The trim sheet holds strips, so it never scales."""
    return 1.0 if "_trim_" in set_name else factor


# ------------------------------------------------------------------ Blender side

def _world_box(objs):
    import bpy
    from mathutils import Vector

    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (tuple(min(c[k] for c in corners) for k in range(3)),
            tuple(max(c[k] for c in corners) for k in range(3)))


def _load_piece(path, label):
    import bpy

    meshes = import_fbx(path)
    keep = [o for o in meshes if classify_name(o.name)["kind"] == "base"]
    for o in [m for m in meshes if m not in keep]:
        bpy.data.objects.remove(o, do_unlink=True)
    if not keep:
        raise RuntimeError(f"no visual LOD0 mesh in {path}")
    root = bpy.data.objects.new("PIECE_" + label, None)
    bpy.context.scene.collection.objects.link(root)
    for o in keep:
        top = o
        while top.parent is not None:
            top = top.parent
        if top is not root:
            world = top.matrix_world.copy()
            top.parent = root
            top.matrix_world = world
    return root, keep


def _image(path, non_color=False):
    import bpy

    img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def _principled(mat):
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.78
    mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.use_backface_culling = True
    return nodes, mat.node_tree.links, bsdf


def _flip_green(nodes, links, colour):
    """The colour with its green turned over (1 minus green): a green down map read as green up."""
    split = nodes.new("ShaderNodeSeparateXYZ")
    turn = nodes.new("ShaderNodeMath")
    turn.operation = "SUBTRACT"
    turn.inputs[0].default_value = 1.0
    join = nodes.new("ShaderNodeCombineXYZ")
    links.new(colour, split.inputs["Vector"])
    links.new(split.outputs["X"], join.inputs["X"])
    links.new(split.outputs["Y"], turn.inputs[1])
    links.new(turn.outputs["Value"], join.inputs["Y"])
    links.new(split.outputs["Z"], join.inputs["Z"])
    return join.outputs["Vector"]


def _normal_vector(nodes, links, image_path, vector, normal_green):
    """A normal map read through `vector` and decoded to a vector in -1 to 1 (green up, as Blender
    reads it). Without an image the vector is the flat normal (0, 0, 1)."""
    if image_path is None:
        flat = nodes.new("ShaderNodeCombineXYZ")
        flat.inputs["Z"].default_value = 1.0
        return flat.outputs["Vector"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = _image(image_path, non_color=True)
    if vector:
        links.new(vector, tex.inputs["Vector"])
    colour = tex.outputs["Color"]
    if normal_green == "down":
        colour = _flip_green(nodes, links, colour)
    decode = nodes.new("ShaderNodeVectorMath")
    decode.operation = "MULTIPLY_ADD"
    decode.inputs[1].default_value = (2.0, 2.0, 2.0)
    decode.inputs[2].default_value = (-1.0, -1.0, -1.0)
    links.new(colour, decode.inputs[0])
    return decode.outputs["Vector"]


def _blend_detail(nodes, links, base, detail):
    """The engine's detail blend: the detail's x and y added to the base normal's x and y, the base
    z kept, then normalised. Both are decoded vectors; the result is encoded to 0 to 1 again."""
    base_split = nodes.new("ShaderNodeSeparateXYZ")
    detail_split = nodes.new("ShaderNodeSeparateXYZ")
    links.new(base, base_split.inputs["Vector"])
    links.new(detail, detail_split.inputs["Vector"])
    join = nodes.new("ShaderNodeCombineXYZ")
    for axis in ("X", "Y"):
        add = nodes.new("ShaderNodeMath")
        add.operation = "ADD"
        links.new(base_split.outputs[axis], add.inputs[0])
        links.new(detail_split.outputs[axis], add.inputs[1])
        links.new(add.outputs["Value"], join.inputs[axis])
    links.new(base_split.outputs["Z"], join.inputs["Z"])
    unit = nodes.new("ShaderNodeVectorMath")
    unit.operation = "NORMALIZE"
    links.new(join.outputs["Vector"], unit.inputs[0])
    encode = nodes.new("ShaderNodeVectorMath")
    encode.operation = "MULTIPLY_ADD"
    encode.inputs[1].default_value = (0.5, 0.5, 0.5)
    encode.inputs[2].default_value = (0.5, 0.5, 0.5)
    links.new(unit.outputs["Vector"], encode.inputs[0])
    return encode.outputs["Vector"]


def _dress_uv(mat, files, scale=1.0, normal_green="up", colour_layer=None, detail=None):
    """The set through the mesh's own UVs: diffuse and, when present, the normal map. A scale other
    than 1 multiplies the UVs about their origin (the density preview). A normal map with green down
    (the engine form) has its green turned over, because Blender reads green up. `detail` is None or
    (png path, uv factor): a finer second normal map, read through the base UV times that factor,
    added to the base normal as the engine does, with the same green sense."""
    nodes, links, bsdf = _principled(mat)

    def uv_vector(factor):
        if factor == 1.0:
            return None
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (factor, factor, 1.0)
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        return mapping.outputs["Vector"]

    vector = uv_vector(scale)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = _image(files["d"])
    if vector:
        links.new(vector, tex.inputs["Vector"])
    colour = tex.outputs["Color"]
    if colour_layer:  # the engine multiplies the diffuse by the vertex colour
        vcol = nodes.new("ShaderNodeVertexColor")
        vcol.layer_name = colour_layer
        stored = nodes.new("ShaderNodeGamma")  # linear back to the stored value
        stored.inputs["Gamma"].default_value = VERTEX_COLOUR_GAMMA
        links.new(vcol.outputs["Color"], stored.inputs["Color"])
        mul = nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs[0].default_value = 1.0
        links.new(colour, mul.inputs[6])
        links.new(stored.outputs["Color"], mul.inputs[7])
        colour = mul.outputs[2]
    links.new(colour, bsdf.inputs["Base Color"])
    if detail:
        base = _normal_vector(nodes, links, files["n"], vector, normal_green)
        fine_vector = uv_vector(detail[1])
        if fine_vector is None:  # a factor of 1 still needs the UV itself
            fine_vector = nodes.new("ShaderNodeTexCoord").outputs["UV"]
        fine = _normal_vector(nodes, links, detail[0], fine_vector, normal_green)
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(_blend_detail(nodes, links, base, fine), nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    elif files["n"]:
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = _image(files["n"], non_color=True)
        if vector:
            links.new(vector, ntex.inputs["Vector"])
        nmap = nodes.new("ShaderNodeNormalMap")
        colour = ntex.outputs["Color"]
        if normal_green == "down":
            colour = _flip_green(nodes, links, colour)
        links.new(colour, nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    mat.diffuse_color = (*[c for c in _mean_color(files["d"])], 1.0)


def _dress_box(mat, files, strip=None):
    """The set by a projection of the world position along the world axes, at the set's density
    (a preview). Blender's own BOX projection picks its axis from the object's normal, and an
    imported FBX object is rotated, so it would use the wrong pair of axes on every face."""
    nodes, links, bsdf = _principled(mat)
    geo = nodes.new("ShaderNodeNewGeometry")
    scale = nodes.new("ShaderNodeVectorMath")
    scale.operation = "SCALE"
    scale.inputs["Scale"].default_value = UV_PER_METRE
    links.new(geo.outputs["Position"], scale.inputs[0])
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(scale.outputs["Vector"], sep.inputs[0])

    def second(axis):
        """The second texture coordinate: as it is, or kept inside one strip of the trim sheet."""
        if strip is None:
            return sep.outputs[axis]
        low, high = strip
        height = high - low
        div = nodes.new("ShaderNodeMath")
        div.operation = "DIVIDE"
        div.inputs[1].default_value = height
        links.new(sep.outputs[axis], div.inputs[0])
        frac = nodes.new("ShaderNodeMath")
        frac.operation = "FRACT"
        links.new(div.outputs[0], frac.inputs[0])
        mul = nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY_ADD"
        mul.inputs[1].default_value = height * 0.92
        mul.inputs[2].default_value = low + height * 0.04
        links.new(frac.outputs[0], mul.inputs[0])
        return mul.outputs[0]

    def sample(first_axis, second_axis):
        comb = nodes.new("ShaderNodeCombineXYZ")
        links.new(sep.outputs[first_axis], comb.inputs["X"])
        links.new(second(second_axis), comb.inputs["Y"])
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = _image(files["d"])
        links.new(comb.outputs["Vector"], tex.inputs["Vector"])
        return tex.outputs["Color"]

    on_x_faces = sample("Y", "Z")
    on_y_faces = sample("X", "Z")
    on_z_faces = sample("X", "Y")

    absn = nodes.new("ShaderNodeVectorMath")
    absn.operation = "ABSOLUTE"
    links.new(geo.outputs["Normal"], absn.inputs[0])
    nsep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(absn.outputs["Vector"], nsep.inputs[0])

    def greater(a, b):
        node = nodes.new("ShaderNodeMath")
        node.operation = "GREATER_THAN"
        links.new(nsep.outputs[a], node.inputs[0])
        links.new(nsep.outputs[b], node.inputs[1])
        return node.outputs[0]

    def both(a, b):
        node = nodes.new("ShaderNodeMath")
        node.operation = "MULTIPLY"
        links.new(a, node.inputs[0])
        links.new(b, node.inputs[1])
        return node.outputs[0]

    x_wins = both(greater("X", "Y"), greater("X", "Z"))
    y_wins = greater("Y", "Z")

    def mix(factor, a, b):
        node = nodes.new("ShaderNodeMix")
        node.data_type = "RGBA"
        links.new(factor, node.inputs[0])
        links.new(a, node.inputs[6])
        links.new(b, node.inputs[7])
        return node.outputs[2]

    color = mix(x_wins, mix(y_wins, on_z_faces, on_y_faces), on_x_faces)
    links.new(color, bsdf.inputs["Base Color"])
    bump = nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.25
    bump.inputs["Distance"].default_value = 0.02
    links.new(color, bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    mat.diffuse_color = (*[c for c in _mean_color(files["d"])], 1.0)


def _dress_flat(mat, hex_color):
    nodes, links, bsdf = _principled(mat)
    rgba = srgb_hex_to_linear(hex_color)
    bsdf.inputs["Base Color"].default_value = rgba
    mat.diffuse_color = rgba


_MEANS = {}


def _mean_color(path):
    """A viewport colour for the material: the image's mean, in linear light."""
    if path not in _MEANS:
        img = _image(path)
        px = img.pixels[:]
        n = max(1, len(px) // 4)
        step = max(1, n // 20000)
        acc = [0.0, 0.0, 0.0]
        count = 0
        for i in range(0, n, step):
            acc[0] += px[4 * i]
            acc[1] += px[4 * i + 1]
            acc[2] += px[4 * i + 2]
            count += 1
        _MEANS[path] = [a / count for a in acc]
    return _MEANS[path]


def _colour_attribute_report(objs):
    """What the FBX import gave each mesh as colour attributes: name, domain, data type and the range of
    the red, green and blue values as Blender reads them and as the tool uses them (the stored value)."""
    out = {}
    for o in objs:
        rows = []
        for attr in o.data.color_attributes:
            vals = [0.0] * (4 * len(attr.data))
            attr.data.foreach_get("color", vals)
            rgb = [v for i, v in enumerate(vals) if i % 4 != 3]
            lo, hi = (min(rgb), max(rgb)) if rgb else (None, None)
            rows.append({"name": attr.name, "domain": attr.domain, "data_type": attr.data_type,
                         "count": len(attr.data), "read_min": lo, "read_max": hi,
                         "stored_min": None if lo is None else lo ** VERTEX_COLOUR_GAMMA,
                         "stored_max": None if hi is None else hi ** VERTEX_COLOUR_GAMMA})
        out[o.name] = rows
    return out


def _colour_layer(objs, mat, mode):
    """The name of the colour attribute that multiplies material `mat`, or None. A mesh of the
    material that has no colour attribute gets a white one (the engine's default), so that one
    material node serves every mesh."""
    users = [o for o in objs if any(sl.material == mat for sl in o.material_slots)]
    names = [o.data.color_attributes[0].name for o in users if len(o.data.color_attributes)]
    if not vertex_colour_wanted(mode, bool(names)):
        return None
    for o in users:
        if not len(o.data.color_attributes):
            attr = o.data.color_attributes.new(names[0], "BYTE_COLOR", "CORNER")
            attr.data.foreach_set("color", [1.0] * (4 * len(attr.data)))
    return names[0]


def dress(objs, tex_dir, masonry_uv_scale=1.0, normal_green="up", vertex_colour="on",
          detail_normal=None, detail_scale=1.0, fallbacks=None):
    """Give every material of the objects its look; return what was done per material name. A fallback
    of a new set (roof slate, heap) is appended to the `fallbacks` list."""
    done = {}
    fallbacks = fallbacks if fallbacks is not None else []
    for o in objs:
        for slot in o.material_slots:
            mat = slot.material
            if mat is None or mat.name in done:
                continue
            route = material_route(mat.name)
            mode = route["mode"]
            if mode in ("uv", "box", "box_strip"):
                got = resolve_set(tex_dir, route["set"])
                if got["note"] and mode == "uv" and route["set"] in SET_FALLBACKS:
                    fallbacks.append(got["note"])
                if got["mode"] == "flat":
                    _dress_flat(mat, got["color"])
                    done[mat.name] = f"flat {got['color']} (fallback for {route['set']})"
                    continue
                if got["mode"] == "none":
                    done[mat.name] = f"{mode}: no diffuse file for {route['set']}, left as it is"
                    continue
                files = got["files"]
                if mode == "uv":
                    layer = _colour_layer(objs, mat, vertex_colour)
                    factor = uv_scale_for(route["set"], masonry_uv_scale)
                    fine = None
                    if detail_normal and uses_detail(route["set"]):
                        fine = (detail_normal, detail_uv_scale(factor, detail_scale))
                    _dress_uv(mat, files, factor, normal_green, layer, fine)
                else:
                    _dress_box(mat, files, route.get("strip"))
                note = f" x vertex colour {layer}" if mode == "uv" and layer else ""
                note += " + detail normal" if mode == "uv" and fine else ""
                note += f" (fallback: shows {os.path.basename(files['d'])})" if got["note"] else ""
                done[mat.name] = f"{mode}: {route['set']}{note}"
            elif mode == "flat":
                _dress_flat(mat, route["color"])
                done[mat.name] = f"flat {route['color']}"
            else:
                mat.use_backface_culling = True
                done[mat.name] = "kept"
    return done


def _dress_hidden(mat):
    """A material that draws nothing (a decal, which this preview leaves out)."""
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    clear = nodes.new("ShaderNodeBsdfTransparent")
    mat.node_tree.links.new(clear.outputs["BSDF"], out.inputs["Surface"])
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    mat.use_backface_culling = False


def dress_control(objs, args):
    """Give the control's materials their own textures; return what each one got."""
    import json

    with open(args.control_dump, encoding="utf-8") as f:
        materials = json.load(f)
    with open(args.control_textures_dump, encoding="utf-8") as f:
        textures = json.load(f)
    done = {}
    for o in objs:
        for slot in o.material_slots:
            mat = slot.material
            if mat is None or mat.name in done:
                continue
            if is_decal(mat.name):
                _dress_hidden(mat)
                done[mat.name] = "decal, not drawn"
                continue
            files = control_textures(mat.name, materials, textures, args.control_tex)
            if files["d"]:
                _dress_uv(mat, files, 1.0, args.control_normal_green, _colour_layer(objs, mat, args.vertex_colour))
                done[mat.name] = {"route": files["route"], "files": {k: files[k] for k in "dns"}}
            else:
                _dress_flat(mat, CONTROL_GREY)
                done[mat.name] = {"route": "none", "flat": CONTROL_GREY}
    return done


def _box_object(name, size, location, hex_color):
    import bmesh
    import bpy

    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= size[0]
        v.co.y *= size[1]
        v.co.z *= size[2]
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    mat = bpy.data.materials.new(name + "_mat")
    _dress_flat(mat, hex_color)
    mesh.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _setup(scene, res_x, res_y, with_low_sun=False):
    import bpy

    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x, scene.render.resolution_y = res_x, res_y
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("view_world")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg is not None:
        bg.inputs["Color"].default_value = srgb_hex_to_linear(SKY_COLOR)
        bg.inputs["Strength"].default_value = SKY_STRENGTH
    scene.world = world
    sun = bpy.data.objects.new("view_sun", bpy.data.lights.new("view_sun", "SUN"))
    sun.data.energy = MAIN_SUN_ENERGY  # a pale stone facing the sun must stay under white
    sun.data.angle = math.radians(8.0)
    sun.rotation_euler = tuple(math.radians(a) for a in MAIN_SUN_ROTATION_DEG)  # from outside and above
    scene.collection.objects.link(sun)
    lights = [("", sun)]
    if with_low_sun:
        low = bpy.data.objects.new("view_sun_low", bpy.data.lights.new("view_sun_low", "SUN"))
        low.data.energy = LOW_SUN_ENERGY
        low.data.angle = math.radians(2.0)  # a small disc keeps the shadows of bevels and joints sharp
        from mathutils import Vector

        low.rotation_euler = Vector(low_sun_direction()).to_track_quat("Z", "Y").to_euler()
        scene.collection.objects.link(low)
        lights.append(("_low", low))
    camera = bpy.data.objects.new("view_cam", bpy.data.cameras.new("view_cam"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera, lights


def _aim(camera, location, target):
    from mathutils import Vector

    camera.location = Vector(location)
    camera.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()


def _render(scene, lights, path):
    """Render the current camera once per light into `path` (and `<stem>_low.png` for the low sun).
    Only the light of the run is on."""
    import bpy

    out = {}
    for suffix, sun in lights:
        for _, other in lights:
            other.hide_render = other is not sun
        stem, ext = os.path.splitext(path)
        scene.render.filepath = stem + suffix + ext
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        out[suffix or "main"] = scene.render.filepath
    for i, (_, sun) in enumerate(lights):
        sun.hide_render = i > 0  # the saved scene shows the main light only
    return out


def _extra_sun(scene, name, direction, energy, angle_deg=8.0):
    """A sun that shines from `direction` (the unit vector toward it), hidden until a view asks for it."""
    import bpy
    from mathutils import Vector

    obj = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
    obj.data.energy = energy
    obj.data.angle = math.radians(angle_deg)
    obj.rotation_euler = Vector(direction).to_track_quat("Z", "Y").to_euler()
    obj.hide_render = True
    scene.collection.objects.link(obj)
    return obj


def _face_sun(scene):
    """The sun of face_close_sun: hidden until that view asks for it."""
    return _extra_sun(scene, "view_sun_face", face_sun_direction(), FACE_SUN_ENERGY)


def _graze_sun(scene):
    """The sun of face_close_graze: hidden until that view asks for it. A small disc keeps the
    shadows of the bevels sharp."""
    return _extra_sun(scene, "view_sun_graze", graze_sun_direction(), GRAZE_SUN_ENERGY, angle_deg=2.0)


def _warm_views(scene, camera, sun, shots, out):
    """Render the (view name, location, target, clip far) shots under the main sun in a warm colour and
    a weaker sky, then put the colour and the sky back. Every other light is off."""
    bg = scene.world.node_tree.nodes.get("Background")
    old_colour = tuple(sun.data.color)
    old_strength = bg.inputs["Strength"].default_value
    sun.data.color = WARM_SUN_COLOUR
    bg.inputs["Strength"].default_value = WARM_SKY_STRENGTH
    views = {}
    for name, loc, target, clip_far in shots:
        views[name] = _shot_at(scene, camera, [("", sun)], loc, target, clip_far, out(name))
    sun.data.color = old_colour
    bg.inputs["Strength"].default_value = old_strength
    return views


def _shot_at(scene, camera, lights, loc, target, clip_far, path):
    camera.data.type = "PERSP"
    camera.data.sensor_fit = "HORIZONTAL"
    camera.data.angle = FOV
    camera.data.clip_start = 0.1
    camera.data.clip_end = clip_far
    _aim(camera, loc, target)
    files = _render(scene, lights, path)
    return {"location": list(loc), "target": list(target), "files": files}


def _shot_view(box, direction, distance_scale=1.0):
    """(location, target, clip far, distance) of the camera that _shot uses for this box."""
    centre = [(box[0][k] + box[1][k]) / 2.0 for k in range(3)]
    half = math.sqrt(sum((box[1][k] - box[0][k]) ** 2 for k in range(3))) / 2.0
    dist = scaled_distance(perspective_distance(half, FOV), distance_scale)
    n = math.sqrt(sum(d * d for d in direction))
    loc = [centre[k] + direction[k] / n * dist for k in range(3)]
    return loc, centre, dist + half * 6.0, dist


def _shot(scene, camera, lights, box, direction, path, distance_scale=1.0):
    loc, centre, clip_far, dist = _shot_view(box, direction, distance_scale)
    view = _shot_at(scene, camera, lights, loc, centre, clip_far, path)
    view["distance"] = dist
    return view


def _viewport(camera, target, distance):
    import bpy
    from mathutils import Vector

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "MATERIAL"
                    space.clip_end = 3000.0
                    region = space.region_3d
                    if region is not None:
                        region.view_perspective = "PERSP"
                        region.view_location = Vector(target)
                        region.view_rotation = camera.rotation_euler.to_quaternion()
                        region.view_distance = distance


def _place_control(args, row_box, report):
    """Load the control, stand it at the +X end of the row, dress it; return its objects and box."""
    root, objs = _load_piece(args.control, "control")
    raw = _world_box(objs)
    dx, dy, dz = control_offset(row_box, raw, args.control_gap, args.control_lift)
    root.location = (dx, dy, dz)
    materials = dress_control(objs, args)
    box = _world_box(objs)
    report["control"] = {"fbx": args.control, "tex": args.control_tex, "lift": args.control_lift,
                         "gap": args.control_gap, "normal_green": args.control_normal_green,
                         "offset": [dx, dy, dz], "raw_box": raw, "placed_box": box, "objects": len(objs),
                         "materials": materials}
    return {"root": root, "objs": objs, "box": box, "figure": None}


def _show_control(control, shown):
    for o in control["objs"] + ([control["figure"]] if control["figure"] else []):
        o.hide_render = not shown


def build(args, report):
    import bpy

    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    pieces = []
    for text in args.piece:
        label, path = parse_piece(text)
        root, objs = _load_piece(path, label)
        pieces.append({"label": label, "path": path, "root": root, "objs": objs, "raw": _world_box(objs)})
    for piece, dx in zip(pieces, row_offsets([p["raw"] for p in pieces], args.gap)):
        piece["root"].location = (dx, 0.0, 0.0)

    all_objs = [o for p in pieces for o in p["objs"]]
    report["colour_attributes"] = {p["label"]: _colour_attribute_report(p["objs"]) for p in pieces}
    fallbacks = []
    report["materials"] = dress(all_objs, args.tex, args.masonry_uv_scale, args.normal_green, args.vertex_colour,
                              args.detail_normal, args.detail_scale, fallbacks)
    report["fallbacks"] = fallbacks
    report["normal_green"] = args.normal_green
    report["masonry_uv_scale"] = args.masonry_uv_scale
    report["vertex_colour"] = args.vertex_colour
    report["detail_normal"] = args.detail_normal
    report["detail_scale"] = args.detail_scale

    boxes = [_world_box(p["objs"]) for p in pieces]
    box = boxes[0]
    for b in boxes[1:]:
        box = union_box(box, b)
    box = above_ground(box)
    control = None
    if args.control:
        control = _place_control(args, box, report)
    ground_box = box if control is None else above_ground(union_box(box, control["box"]))
    span = ground_span(ground_box)
    _box_object("ground", (span, span, 0.02),
                ((ground_box[0][0] + ground_box[1][0]) / 2.0, (ground_box[0][1] + ground_box[1][1]) / 2.0, -0.01),
                GROUND_COLOR)
    for piece, b in zip(pieces, boxes):
        _box_object("figure_" + piece["label"], FIGURE_SIZE,
                    ((b[0][0] + b[1][0]) / 2.0, b[1][1] + 1.5, FIGURE_SIZE[2] / 2.0), FIGURE_COLOR)
    if control is not None:
        b = control["box"]
        control["figure"] = _box_object("figure_control", FIGURE_SIZE,
                                        ((b[0][0] + b[1][0]) / 2.0, b[1][1] + 1.5, FIGURE_SIZE[2] / 2.0),
                                        FIGURE_COLOR)

    camera, lights = _setup(scene, args.res_x, args.res_y, with_low_sun=args.sun == "low")
    os.makedirs(args.out, exist_ok=True)

    def out(name):
        return os.path.join(args.out, name + ".png")

    if control is not None:
        _show_control(control, False)  # the usual views are of our pieces alone
    views = {}
    views["all_3q_outside"] = _shot(scene, camera, lights, box, (0.4, 1.0, 0.36), out("all_3q_outside"))
    views["all_3q_inside"] = _shot(scene, camera, lights, box, (-0.4, -1.0, 0.42), out("all_3q_inside"))
    views["all_front"] = _shot(scene, camera, lights, box, (0.0, 1.0, 0.12), out("all_front"))
    views["all_front_far"] = _shot(scene, camera, lights, box, (0.0, 1.0, 0.12), out("all_front_far"),
                                   distance_scale=FAR_FACTOR)
    for piece, b in zip(pieces, boxes):
        views[piece["label"] + "_3q"] = _shot(scene, camera, lights, above_ground(b), (0.5, 1.0, 0.4),
                                              out(piece["label"] + "_3q"))
    close_index = close_piece_index([(p["label"], p["path"]) for p in pieces], args.close_piece)
    last = boxes[-1]
    close_box = boxes[close_index]
    report["close_piece"] = {"label": pieces[close_index]["label"], "fbx": pieces[close_index]["path"]}
    loc, target = face_close_view((close_box[0][0] + close_box[1][0]) / 2.0)
    views["face_close"] = _shot_at(scene, camera, lights, loc, target, 100.0, out("face_close"))
    face_sun = _face_sun(scene)  # the same camera, the sun on the face (always written, no _low copy)
    for _, other in lights:
        other.hide_render = True  # only the face sun lights this view
    views["face_close_sun"] = _shot_at(scene, camera, [("", face_sun)], loc, target, 100.0, out("face_close_sun"))
    for _, other in lights:
        other.hide_render = True
    face_sun.hide_render = True
    graze_sun = _graze_sun(scene)  # the same camera, one sun at cosine 0.2 to the face, the sky as it is
    views["face_close_graze"] = _shot_at(scene, camera, [("", graze_sun)], loc, target, 100.0,
                                         out("face_close_graze"))
    graze_sun.hide_render = True
    warm = _warm_views(scene, camera, lights[0][1], [
        (pieces[0]["label"] + "_3q_warm",) + _shot_view(above_ground(boxes[0]), (0.5, 1.0, 0.4))[:3],
        ("face_close_warm", loc, target, 100.0),
        ("all_front_warm",) + _shot_view(box, (0.0, 1.0, 0.12))[:3]], out)
    views.update(warm)
    for _, other in lights:
        other.hide_render = True
    lights[0][1].hide_render = False  # the saved scene shows the main light only
    report["light"] = {
        "main_sun": {"energy": MAIN_SUN_ENERGY, "rotation_deg": list(MAIN_SUN_ROTATION_DEG),
                     "to_sun": list(main_sun_direction()), "elevation_deg": math.degrees(math.asin(main_sun_direction()[2])),
                     "face_cosine": face_cosine(main_sun_direction())},
        "face_sun": {"energy": FACE_SUN_ENERGY, "to_sun": list(face_sun_direction()),
                     "elevation_deg": FACE_SUN_ELEVATION_DEG, "side_deg": FACE_SUN_SIDE_DEG,
                     "face_cosine": face_cosine(face_sun_direction())},
        "graze_sun": {"energy": GRAZE_SUN_ENERGY, "to_sun": list(graze_sun_direction()),
                      "elevation_deg": GRAZE_ELEVATION_DEG, "face_cosine": face_cosine(graze_sun_direction())},
        "warm": {"sun_colour_linear": list(WARM_SUN_COLOUR), "sky_strength": WARM_SKY_STRENGTH,
                 "sun_energy": MAIN_SUN_ENERGY, "sun_rotation_deg": list(MAIN_SUN_ROTATION_DEG)},
        "sky": {"colour": SKY_COLOR, "strength": SKY_STRENGTH},
    }
    if control is not None:
        _show_control(control, True)
        pair_box = above_ground(union_box(last, control["box"]))
        views["pair_outside"] = _shot(scene, camera, lights, pair_box, (0.0, 1.0, 0.12), out("pair_outside"))
        loc, target = eye_view((last[1][0] + control["box"][0][0]) / 2.0, max(last[1][1], control["box"][1][1]),
                               eye_standoff(args.control_gap))
        views["pair_close"] = _shot_at(scene, camera, lights, loc, target, 300.0, out("pair_close"))
        # pair_face: the faces butt for this view (the control moves by the gap), so that half of the
        # frame is our face and half the control's. Level at the middle of the bay field, 6 m out.
        control["root"].location = (control["root"].location[0] - args.control_gap,
                                    control["root"].location[1], control["root"].location[2])
        loc, target = pair_face_view(last[1][0])
        views["pair_face"] = _shot_at(scene, camera, lights, loc, target, 300.0, out("pair_face"))
        views["pair_face"]["control_moved_by_x"] = -args.control_gap
        control["root"].location = (control["root"].location[0] + args.control_gap,
                                    control["root"].location[1], control["root"].location[2])
        _show_control(control, False)
    report["views"] = views
    report["pieces"] = [{"label": p["label"], "fbx": p["path"], "objects": len(p["objs"]), "placed_box": b}
                        for p, b in zip(pieces, boxes)]

    if args.save_blend:
        main = _shot(scene, camera, lights[:1], box, (0.4, 1.0, 0.36), out("all_3q_outside"))
        _viewport(camera, main["target"], main["distance"])
        for o in bpy.data.objects:
            o.select_set(False)
        os.makedirs(os.path.dirname(args.save_blend) or ".", exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=args.save_blend)
        for img in list(bpy.data.images):  # the FBX import names textures that are not on this disk
            missing = img.source == "FILE" and not os.path.exists(bpy.path.abspath(img.filepath))
            if img.users == 0 or missing:
                bpy.data.images.remove(img)
        bpy.ops.file.pack_all()
        bpy.ops.wm.save_mainfile()
        report["saved_blend"] = args.save_blend


def main():
    raw = script_args()
    out_dir = flag_value(raw, "--out") or "."
    with job(report_path(out_dir)) as report:
        args = parse_args(raw)
        build(args, report)


if __name__ == "__main__":
    main()
