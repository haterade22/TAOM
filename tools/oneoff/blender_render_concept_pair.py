"""Render a concept blockout beside its control piece, at one scale, from fixed cameras.

The kingdom kits work judges every new piece beside the piece it came from, in the same view
(docs/kitbash/kingdom-kits-prompt.md). This script puts the control piece on the left and the
concept on the right of one ground plane, with a 1.8 m figure at each foot, and renders the same
views of both. It can also save the scene as a VIEW .blend to orbit in a Blender window.

Headless (the Store launcher detaches: wait for <out>/_report/DONE.txt):

    "%LOCALAPPDATA%\\Microsoft\\WindowsApps\\blender-launcher.exe" -b --factory-startup
        -P tools/oneoff/blender_render_concept_pair.py --
        --control <control.fbx> --concept <concept.fbx> --out <dir>
        [--control-lift M] [--control-scale X Y Z] [--concept-lift M] [--gap 6]
        [--res-x 1600] [--res-y 900] [--save-blend <VIEW.blend>]

--control-lift and --control-scale place the control the way a scene uses it. The Gondor walls
stand with their deck about 12 m above the ground (lift -3), and scenes scale the Gondor
gatehouse to about 0.8 (docs/kitbash/rivendell-siege-set.md). Both pieces keep their own frame:
+Y is the outer face, as in the Gondor kit's Blender import.

Shown: the visual LOD0 meshes only (no .lodN objects, no bo_ bodies). The control is drawn in one
flat grey, the measured mean of the Gondor wall stone. The concept keeps its materials; a material
named in PALETTE gets that measured colour. Back faces are culled, as the engine draws them.

Writes <out>/pair_3q_outside.png, pair_3q_inside.png, pair_front.png, pair_top.png,
control_side.png and concept_side.png (the two side views share one scale).
"""
import argparse
import math
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(_HERE), "blender"))
from blender_job import flag_value, import_fbx, job, report_path, script_args  # noqa: E402
from mesh_preflight import classify_name  # noqa: E402  (bpy is imported lazily there)

# Measured means, WORK\palette\palette.md (2026-11-24). Keys are material names, lower case.
PALETTE = {
    "rivendell_stone": "#c7b89e",   # Rivendell map icon, walls
    "rivendell_trim": "#ded6cb",    # Rivendell map icon, pillars
    "rivendell_rubble": "#877a68",  # Rivendell map icon, stone
    "rivendell_slate": "#34405f",   # Rivendell map icon, roof
    "rivendell_wood": "#c8b6a1",    # elven kit, bleached wood
    "rivendell_metal": "#999898",   # Rivendell armour, silver
    "rivendell_gate": "#8a8f96",    # placeholder for the vanilla gate leaves
}
CONTROL_COLOR = "#aaa8a6"           # Gondor wall stone, t_gondor_bricks_large_a_dirty_d2.png
GROUND_COLOR = "#9a9c8c"
EXPOSURE = 0.8  # the studio light alone renders the measured colours about a stop too dark
FIGURE_COLOR = "#1f1f22"
SKY_COLOR = "#d5dde6"
FIGURE_SIZE = (0.5, 0.3, 1.8)
FOV = math.radians(40.0)


# ------------------------------------------------------------------ pure helpers

def srgb_hex_to_linear(text):
    """(r, g, b, 1.0) in linear light for an sRGB hex string such as '#c7b89e'."""
    s = text.lstrip("#")
    if len(s) != 6:
        raise ValueError(f"not a six digit hex colour: {text!r}")
    out = []
    for i in (0, 2, 4):
        c = int(s[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def layout_offsets(control_box, concept_box, gap):
    """X offsets that put the control's right edge at -gap/2 and the concept's left edge at +gap/2."""
    return (-gap / 2.0 - control_box[1][0], gap / 2.0 - concept_box[0][0])


def above_ground(box):
    """The part of a box that is above z = 0."""
    lo, hi = box
    return (lo[0], lo[1], max(lo[2], 0.0)), (hi[0], hi[1], hi[2])


def union_box(a, b):
    return (tuple(min(a[0][k], b[0][k]) for k in range(3)), tuple(max(a[1][k], b[1][k]) for k in range(3)))


def transformed_box(box, scale=(1.0, 1.0, 1.0), lift=0.0, dx=0.0):
    """A box after a scale about the origin, a lift in z and a shift in x."""
    lo = [box[0][k] * scale[k] for k in range(3)]
    hi = [box[1][k] * scale[k] for k in range(3)]
    lo, hi = [min(lo[k], hi[k]) for k in range(3)], [max(lo[k], hi[k]) for k in range(3)]
    return (lo[0] + dx, lo[1], lo[2] + lift), (hi[0] + dx, hi[1], hi[2] + lift)


def perspective_distance(half_diagonal, fov_radians, margin=1.15):
    """How far a camera stands back so a sphere of that radius fits its field of view."""
    return half_diagonal * margin / math.sin(fov_radians / 2.0)


def parse_args(argv):
    p = argparse.ArgumentParser(allow_abbrev=False)
    p.add_argument("--control", required=True)
    p.add_argument("--concept", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--control-lift", type=float, default=0.0)
    p.add_argument("--control-scale", type=float, nargs=3, default=[1.0, 1.0, 1.0])
    p.add_argument("--concept-lift", type=float, default=0.0)
    p.add_argument("--gap", type=float, default=6.0)
    p.add_argument("--res-x", type=int, default=1600)
    p.add_argument("--res-y", type=int, default=900)
    p.add_argument("--save-blend")
    return p.parse_args(argv)


# ------------------------------------------------------------------ Blender side

def _world_box(objs):
    import bpy
    from mathutils import Vector

    bpy.context.view_layer.update()
    corners = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (tuple(min(c[k] for c in corners) for k in range(3)),
            tuple(max(c[k] for c in corners) for k in range(3)))


def _load_group(path, name):
    """Import an FBX, keep its visual LOD0 meshes, and hang them under one empty at the origin."""
    import bpy

    meshes = import_fbx(path)
    keep = [o for o in meshes if classify_name(o.name)["kind"] == "base"]
    drop = [o for o in meshes if o not in keep]
    for o in drop:
        bpy.data.objects.remove(o, do_unlink=True)
    if not keep:
        raise RuntimeError(f"no visual LOD0 mesh in {path}")
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for o in keep:
        top = o
        while top.parent is not None:
            top = top.parent
        if top is not root and top.parent is None:
            world = top.matrix_world.copy()
            top.parent = root
            top.matrix_world = world
    return root, keep


def _material(name, hex_color):
    import bpy

    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = srgb_hex_to_linear(hex_color)
    mat.use_backface_culling = True
    return mat


def _paint_control(objs):
    mat = _material("control_gondor_grey", CONTROL_COLOR)
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)


def _paint_concept(objs):
    seen = {}
    for o in objs:
        for slot in o.material_slots:
            m = slot.material
            if m is None:
                continue
            key = m.name.lower().split(".")[0]
            if key in PALETTE:
                m.diffuse_color = srgb_hex_to_linear(PALETTE[key])
            m.use_backface_culling = True
            seen[m.name] = key in PALETTE
    return seen


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
    mesh.materials.append(_material(name + "_mat", hex_color))
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _ground(box):
    span = max(box[1][0] - box[0][0], box[1][1] - box[0][1]) * 3.0 + 40.0
    cx, cy = (box[0][0] + box[1][0]) / 2.0, (box[0][1] + box[1][1]) / 2.0
    return _box_object("ground", (span, span, 0.02), (cx, cy, -0.01), GROUND_COLOR)


def _setup_render(scene, res_x, res_y):
    import bpy

    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_backface_culling = True
    shading.show_cavity = True
    shading.show_shadows = False  # cast shadows hid the faces under test more than they helped
    shading.show_object_outline = True
    shading.show_specular_highlight = False
    scene.view_settings.exposure = EXPOSURE
    scene.render.resolution_x, scene.render.resolution_y = res_x, res_y
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("pair_world")
    world.color = srgb_hex_to_linear(SKY_COLOR)[:3]
    scene.world = world
    camera = bpy.data.objects.new("pair_cam", bpy.data.cameras.new("pair_cam"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def _aim(camera, location, target):
    from mathutils import Vector

    camera.location = Vector(location)
    direction = Vector(target) - Vector(location)
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def _render(scene, camera, path):
    import bpy

    scene.render.filepath = path
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)


def _perspective_view(scene, camera, box, direction, path):
    centre = [(box[0][k] + box[1][k]) / 2.0 for k in range(3)]
    half = math.sqrt(sum((box[1][k] - box[0][k]) ** 2 for k in range(3))) / 2.0
    dist = perspective_distance(half, FOV)
    n = math.sqrt(sum(d * d for d in direction))
    loc = [centre[k] + direction[k] / n * dist for k in range(3)]
    camera.data.type = "PERSP"
    camera.data.sensor_fit = "HORIZONTAL"
    camera.data.angle = FOV
    camera.data.clip_start = 0.1
    camera.data.clip_end = dist + half * 4.0
    _aim(camera, loc, centre)
    _render(scene, camera, path)
    return {"location": loc, "target": centre}


def _ortho_view(scene, camera, box, direction, scale, path):
    centre = [(box[0][k] + box[1][k]) / 2.0 for k in range(3)]
    reach = max(box[1][k] - box[0][k] for k in range(3)) * 3.0 + 20.0
    loc = [centre[k] + direction[k] * reach for k in range(3)]
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = scale
    camera.data.clip_start = 0.1
    camera.data.clip_end = reach * 3.0
    _aim(camera, loc, centre)
    _render(scene, camera, path)
    return {"ortho_scale": scale}


def _show_only(groups, names):
    for name, objs in groups.items():
        for o in objs:
            o.hide_render = name not in names


def _viewport_material_shading(camera, target, distance):
    """Make every 3D viewport of the saved file show the material colours with culled back faces,
    framed like the camera: a file saved from a background session otherwise opens on the origin,
    inside the mesh."""
    import bpy
    from mathutils import Vector

    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != "VIEW_3D":
                continue
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "SOLID"
                    space.shading.color_type = "MATERIAL"
                    space.shading.show_backface_culling = True
                    space.shading.show_cavity = True
                    space.clip_end = 2000.0
                    region = space.region_3d
                    if region is not None:
                        region.view_perspective = "PERSP"
                        region.view_location = Vector(target)
                        region.view_rotation = camera.rotation_euler.to_quaternion()
                        region.view_distance = distance


def build(args, report):
    import bpy

    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    control_root, control_objs = _load_group(args.control, "CONTROL")
    concept_root, concept_objs = _load_group(args.concept, "CONCEPT")
    raw_control, raw_concept = _world_box(control_objs), _world_box(concept_objs)
    scale = tuple(args.control_scale)
    dx_control, dx_concept = layout_offsets(
        transformed_box(raw_control, scale=scale), raw_concept, args.gap)
    control_root.scale = scale
    control_root.location = (dx_control, 0.0, args.control_lift)
    concept_root.location = (dx_concept, 0.0, args.concept_lift)

    _paint_control(control_objs)
    report["concept_materials_in_palette"] = _paint_concept(concept_objs)

    control_box, concept_box = _world_box(control_objs), _world_box(concept_objs)
    box = above_ground(union_box(control_box, concept_box))
    ground = _ground(box)
    figures = []
    for label, b in (("control", control_box), ("concept", concept_box)):
        figures.append(_box_object(
            "figure_" + label, FIGURE_SIZE,
            ((b[0][0] + b[1][0]) / 2.0, b[1][1] + 1.5, FIGURE_SIZE[2] / 2.0), FIGURE_COLOR))

    report.update({
        "control": {"fbx": args.control, "raw_box": raw_control, "placed_box": control_box,
                    "scale": scale, "lift": args.control_lift, "objects": len(control_objs)},
        "concept": {"fbx": args.concept, "raw_box": raw_concept, "placed_box": concept_box,
                    "lift": args.concept_lift, "objects": len(concept_objs)},
        "frame_box": box,
    })

    camera = _setup_render(scene, args.res_x, args.res_y)
    groups = {"control": control_objs, "concept": concept_objs}
    views = {}
    os.makedirs(args.out, exist_ok=True)

    def out(name):
        return os.path.join(args.out, name + ".png")

    _show_only(groups, {"control", "concept"})
    views["pair_3q_outside"] = _perspective_view(scene, camera, box, (0.45, 1.0, 0.38), out("pair_3q_outside"))
    views["pair_3q_inside"] = _perspective_view(scene, camera, box, (-0.45, -1.0, 0.45), out("pair_3q_inside"))
    width = box[1][0] - box[0][0]
    height = box[1][2] - box[0][2]
    depth = box[1][1] - box[0][1]
    aspect = args.res_x / float(args.res_y)
    front_scale = max(width, height * aspect) * 1.08
    views["pair_front"] = _ortho_view(scene, camera, box, (0.0, 1.0, 0.0), front_scale, out("pair_front"))
    top_scale = max(width, depth * aspect) * 1.08
    views["pair_top"] = _ortho_view(scene, camera, box, (0.0, 0.001, 1.0), top_scale, out("pair_top"))

    side_scale = max(depth, height * aspect) * 1.12
    for label, b in (("control", control_box), ("concept", concept_box)):
        _show_only(groups, {label})
        for f in figures:
            f.hide_render = not f.name.endswith(label)
        side_box = above_ground((b[0], b[1]))
        side_box = ((side_box[0][0], box[0][1], 0.0), (side_box[1][0], box[1][1], box[1][2]))
        views[label + "_side"] = _ortho_view(scene, camera, side_box, (1.0, 0.0, 0.0), side_scale, out(label + "_side"))
    _show_only(groups, {"control", "concept"})
    for f in figures:
        f.hide_render = False
    report["views"] = views
    report["ground"] = ground.name

    if args.save_blend:
        view = _perspective_view(scene, camera, box, (0.45, 1.0, 0.38), out("pair_3q_outside"))
        distance = math.sqrt(sum((view["location"][k] - view["target"][k]) ** 2 for k in range(3)))
        _viewport_material_shading(camera, view["target"], distance)
        for o in bpy.data.objects:  # the import leaves its meshes selected, which outlines them
            o.select_set(False)
        os.makedirs(os.path.dirname(args.save_blend) or ".", exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=args.save_blend)
        report["saved_blend"] = args.save_blend


def main():
    raw = script_args()
    out_dir = flag_value(raw, "--out") or "."
    with job(report_path(out_dir)) as report:
        args = parse_args(raw)
        build(args, report)


if __name__ == "__main__":
    main()
