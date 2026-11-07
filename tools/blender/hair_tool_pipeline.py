"""Author Hair Tool grooms and finalize them as TAOM upper meshes.

Run inside Blender with a scene containing the scalp, armature, and any finished
reference grooms. Hair Tool 4.6.1 is a local dependency; no add-on code or assets
are copied into TAOM. Every write is under the explicit --out directory.
"""
import argparse
import json
from pathlib import Path
import sys
import traceback

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

PROFILES = {
    "dwarf_hair": {
        "family": "hair",
        "target": "Dwarf_Hair_New_lod0",
        "material": "m_dwarf_beards_b",
        "references": ["Dwarf_Hair_A_lod0", "Dwarf_Hair_B_lod0", "Dwarf_Hair_C_lod0"],
        "length": 0.35,
        "density": 40.0,
        "width": 0.012,
        "resolution": 6,
        "seed": 13,
        "guides": 32,
    },
    "dwarf_beard": {
        "family": "beard",
        "target": "SK_Dwarf_Beard_New",
        "material": "m_dwarf_beards_b",
        "references": ["SK_Dwarf_Beard_A_01", "SK_Dwarf_Beard_A_07", "SK_Dwarf_Beard_A_09"],
        "length": 0.45,
        "density": 45.0,
        "width": 0.012,
        "resolution": 6,
        "seed": 17,
        "guides": 32,
    },
    "elf_hair": {
        "family": "hair",
        "target": "Elf_Hair_New_lod0",
        "material": None,
        "references": [],
        "length": 0.55,
        "density": 35.0,
        "width": 0.008,
        "resolution": 8,
        "seed": 19,
        "guides": 40,
    },
}


def profile_options(name, material=None, references=()):
    if name not in PROFILES:
        raise ValueError("unknown Hair Tool profile %r" % name)
    profile = PROFILES[name]
    material = material or profile["material"]
    references = list(references) or list(profile["references"])
    if not material:
        raise ValueError("profile %s requires --material" % name)
    if not references:
        raise ValueError("profile %s requires at least one --reference weight donor" % name)
    return {
        "family": profile["family"],
        "target": profile["target"],
        "material": material,
        "references": references,
    }


def starter_settings(name):
    if name not in PROFILES:
        raise ValueError("unknown Hair Tool profile %r" % name)
    profile = PROFILES[name]
    return {key: profile[key] for key in ("length", "density", "width", "resolution", "seed", "guides")}


def projection_error(stats, limit_mm=500.0):
    if stats["max_mm"] > limit_mm:
        return "weight projection reaches %.1f mm above the %.1f mm limit; check coordinate frames and donors" % (
            stats["max_mm"], limit_mm)
    return None


def final_mesh_errors(info, expected_material):
    errors = []
    if not info["vertices"] or not info["polygons"]:
        errors.append("converted mesh is empty")
    missing = sorted({"UVMap", "HairTool_UV"} - set(info["uv_layers"]))
    if missing:
        errors.append("missing UV layers: " + ", ".join(missing))
    if info["weighted_vertices"] != info["vertices"]:
        errors.append("weights cover %d/%d vertices" % (info["weighted_vertices"], info["vertices"]))
    if info["shape_keys"]:
        errors.append("upper mesh retains morph channels: " + ", ".join(info["shape_keys"]))
    if info["materials"] != [expected_material]:
        errors.append("materials must be exactly [%s], found %s" % (expected_material, info["materials"]))
    return errors


def parse_args(argv):
    argv = argv[argv.index("--") + 1:] if "--" in argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("inspect", "author", "finalize"), required=True)
    parser.add_argument("--out", required=True, help="staging directory; all writes stay below it")
    parser.add_argument("--hair-tool", help="Hair Tool package folder or its parent")
    parser.add_argument("--profile", choices=tuple(PROFILES), default="dwarf_hair")
    parser.add_argument("--mesh", help="source Hair System object; defaults to the active object")
    parser.add_argument("--material", help="Kit material name override")
    parser.add_argument("--reference", action="append", default=[], help="finished weight-donor mesh; repeatable")
    parser.add_argument("--armature", help="target armature; inferred when the scene has exactly one")
    parser.add_argument("--head", help="optional head mesh to include in preview renders")
    parser.add_argument("--preview", action="store_true", help="render four Workbench views and previews.json")
    parser.add_argument("--overwrite", action="store_true", help="allow replacement of an existing staged output")
    return parser.parse_args(argv)


def object_info(obj):
    vertices = len(obj.data.vertices) if obj.type == "MESH" else 0
    return {
        "name": obj.name,
        "type": obj.type,
        "vertices": vertices,
        "polygons": len(obj.data.polygons) if obj.type == "MESH" else 0,
        "uv_layers": [layer.name for layer in obj.data.uv_layers] if obj.type == "MESH" else [],
        "weighted_vertices": sum(
            1 for vertex in obj.data.vertices if any(group.weight > 1e-6 for group in vertex.groups)
        ) if obj.type == "MESH" else 0,
        "shape_keys": [key.name for key in obj.data.shape_keys.key_blocks]
        if obj.type == "MESH" and obj.data.shape_keys else [],
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    }


def load_hair_tool(root):
    import bpy
    import gpu

    if root:
        package = Path(root).resolve()
        parent = package.parent if package.name == "hair_tool" else package
        if not (parent / "hair_tool" / "__init__.py").is_file():
            raise ValueError("no hair_tool/__init__.py below %s" % parent)
        sys.path.insert(0, str(parent))
    if bpy.app.background:
        gpu.init()
    import hair_tool

    version = tuple(hair_tool.bl_info["version"])
    if version != (4, 6, 1):
        raise RuntimeError("Hair Tool 4.6.1 is required; found %s" % (version,))
    bpy.ops.preferences.addon_enable(module="hair_tool")
    return version


def selected_source(name):
    import bpy

    obj = bpy.data.objects.get(name) if name else bpy.context.view_layer.objects.active
    if obj is None:
        raise ValueError("select a scalp or Hair System object, or pass --mesh")
    if obj.type not in {"MESH", "CURVES"}:
        raise ValueError("%s is not a mesh or Hair Curves object" % obj.name)
    return obj


def output_file(args, suffix):
    path = Path(args.out).resolve() / (PROFILES[args.profile]["target"] + suffix)
    if path.exists() and not args.overwrite:
        raise FileExistsError("%s exists; choose another --out or pass --overwrite" % path)
    return path


def downward_direction(normal):
    nx, ny, nz = normal
    tangent = (nx * nz, ny * nz, nz * nz - 1.0)
    length = sum(component * component for component in tangent) ** 0.5
    if length < 1e-8:
        tangent = (nx, ny, -0.25)
        length = sum(component * component for component in tangent) ** 0.5
    if length < 1e-8:
        return (0, 0, -1)
    return tuple(component / length for component in tangent)


def apply_starter(obj, profile):
    settings = starter_settings(profile)
    for modifier in obj.modifiers:
        if modifier.type != "NODES":
            continue
        generator = modifier.node_group.nodes.get("Generate Strands")
        if generator:
            for name, key in (("Length", "length"), ("Density", "density"),
                              ("Seed", "seed"), ("Resolution", "resolution")):
                socket = generator.inputs.get(name)
                if socket is not None:
                    socket.default_value = settings[key]
        flat = modifier.node_group.nodes.get("Flat_Profile_UV")
        if flat:
            flat.inputs["Width"].default_value = settings["width"]


def create_guide_system(source, profile):
    import math
    import bpy
    from mathutils import Vector
    from hair_tool.utils.general_utils import create_new_curve_obj

    settings = starter_settings(profile)
    family = PROFILES[profile]["family"]
    source.data.calc_loop_triangles()
    candidates = []
    for triangle in source.data.loop_triangles:
        center = sum((source.matrix_world @ source.data.vertices[index].co for index in triangle.vertices), Vector()) / 3
        normal = (source.matrix_world.to_3x3() @ triangle.normal).normalized()
        if normal.length_squared < 1e-8:
            continue
        candidates.append((center, normal))
    if not candidates:
        raise ValueError("%s has no usable scalp triangles" % source.name)
    z_values = [center.z for center, _ in candidates]
    z_min, z_span = min(z_values), max(z_values) - min(z_values)
    axis = sum((center for center, _ in candidates), Vector()) / len(candidates)
    filtered = []
    for center, normal in candidates:
        z = (center.z - z_min) / max(z_span, 1e-8)
        if family == "hair":
            allowed = z > 0.35 and not (normal.y > 0.55 and z < 0.72)
        else:
            allowed = z < 0.7 and normal.y > 0.15
        if allowed:
            filtered.append((center, normal))
    filtered.sort(key=lambda item: (math.atan2(item[0].y - axis.y, item[0].x - axis.x), item[0].z))
    if not filtered:
        raise ValueError("no %s guide roots remain on %s" % (family, source.name))
    count = min(settings["guides"], len(filtered))
    selected = [filtered[(index * len(filtered)) // count] for index in range(count)]
    guide = create_new_curve_obj("TAOM_%s_guides" % profile, parent_obj=source)
    guide.matrix_world = source.matrix_world.copy()
    inverse = guide.matrix_world.inverted()
    guide.data.surface = source
    if source.data.uv_layers:
        guide.data.surface_uv_map = source.data.uv_layers[0].name
    guide.data.add_curves([6] * count)
    for curve, (root, normal) in zip(guide.data.curves, selected):
        direction = Vector(downward_direction(tuple(normal)))
        for index, point in enumerate(curve.points):
            t = index / (len(curve.points) - 1)
            world = root + direction * (settings["length"] * t) + normal * (0.003 + 0.02 * math.sin(math.pi * t))
            point.position = inverse @ world
    bpy.ops.object.select_all(action="DESELECT")
    guide.select_set(True)
    bpy.context.view_layer.objects.active = guide
    result = bpy.ops.object.add_hair_system(mode="INTERPOLATED_STRANDS", surface_obj=source.name)
    if "FINISHED" not in result:
        raise RuntimeError("Hair Tool could not interpolate guides on %s" % source.name)
    apply_starter(guide, profile)
    return guide


def author(args, version):
    import bpy

    source = selected_source(args.mesh)
    if source.ht_props.hair_nodes.is_using_gnodes:
        raise ValueError("%s already has a Hair System; style it or choose another scalp" % source.name)
    path = output_file(args, ".author.blend")
    if Path(bpy.data.filepath).resolve() == path:
        raise ValueError("author output must not replace the source blend")
    guide = create_guide_system(source, args.profile)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False)
    return {
        "source": source.name,
        "hair_object": guide.name,
        "output": str(path),
        "hair_tool_version": list(version),
        "starter": starter_settings(args.profile),
        "object": object_info(guide),
    }


def armature_for(name):
    import bpy

    if name:
        armature = bpy.data.objects.get(name)
        if armature is None or armature.type != "ARMATURE":
            raise ValueError("armature %r was not found" % name)
        return armature
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise ValueError("pass --armature; the scene has %d armatures" % len(armatures))
    return armatures[0]


def transfer_weights(target, references, armature):
    import bpy
    from mathutils.bvhtree import BVHTree
    from transfer_upper_mesh_weights import barycentric, mix_weights

    verts, weights, triangles = [], [], []
    for reference in references:
        obj = bpy.data.objects.get(reference)
        if obj is None or obj.type != "MESH":
            raise ValueError("weight donor %r was not found as a mesh" % reference)
        if obj == target:
            raise ValueError("a groom cannot be its own weight donor")
        base = len(verts)
        names = {group.index: group.name for group in obj.vertex_groups}
        verts.extend(tuple(obj.matrix_world @ vertex.co) for vertex in obj.data.vertices)
        weights.extend({names[group.group]: group.weight for group in vertex.groups} for vertex in obj.data.vertices)
        obj.data.calc_loop_triangles()
        triangles.extend(tuple(base + index for index in tri.vertices) for tri in obj.data.loop_triangles)
    if not triangles:
        raise ValueError("weight donors contain no triangles")
    bvh = BVHTree.FromPolygons(verts, triangles)
    bones = {bone.name for bone in armature.data.bones}
    groups = {}
    distances = []
    for vertex in target.data.vertices:
        point = target.matrix_world @ vertex.co
        nearest, _, triangle_index, distance = bvh.find_nearest(point)
        if nearest is None:
            raise ValueError("no donor surface near %s vertex %d" % (target.name, vertex.index))
        a, b, c = triangles[triangle_index]
        shares = barycentric(tuple(nearest), verts[a], verts[b], verts[c])
        mixed = mix_weights([weights[a], weights[b], weights[c]], shares, 4)
        for group, weight in mixed.items():
            if group not in bones:
                raise ValueError("donor bone %r is absent from armature %s" % (group, armature.name))
            if group not in groups:
                groups[group] = target.vertex_groups.get(group) or target.vertex_groups.new(name=group)
            groups[group].add([vertex.index], weight, "REPLACE")
        distances.append(distance)
    stats = {
        "donors": references,
        "median_mm": round(sorted(distances)[len(distances) // 2] * 1000, 4) if distances else 0,
        "max_mm": round(max(distances, default=0) * 1000, 4),
    }
    error = projection_error(stats)
    if error:
        raise ValueError(error)
    return stats


def assign_delivery(target, options, armature):
    import bpy

    material = bpy.data.materials.get(options["material"]) or bpy.data.materials.new(options["material"])
    target.data.materials.clear()
    target.data.materials.append(material)
    for polygon in target.data.polygons:
        polygon.material_index = 0
    if target.data.shape_keys:
        target.shape_key_clear()
    if object_info(target)["weighted_vertices"] == 0:
        weight_stats = transfer_weights(target, options["references"], armature)
        weight_stats["mode"] = "reference_projection"
    else:
        weight_stats = {"mode": "hair_tool_surface_weights"}
    world = target.matrix_world.copy()
    target.parent = armature
    target.matrix_world = world
    modifier = target.modifiers.get("TAOM Upper Mesh") or target.modifiers.new("TAOM Upper Mesh", "ARMATURE")
    modifier.object = armature
    return weight_stats


def render_previews(target, head_name, family, out):
    import bpy
    from mathutils import Vector

    objects = [target]
    if head_name:
        head = bpy.data.objects.get(head_name)
        if head is None or head.type != "MESH":
            raise ValueError("preview head %r was not found as a mesh" % head_name)
        objects.append(head)
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.hide_render = obj not in objects
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    camera_data = bpy.data.cameras.new("TAOM Groom Preview")
    camera = bpy.data.objects.new("TAOM Groom Preview", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    scene.camera = camera
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    center = (low + high) / 2
    size = max((high - low).length, 0.1)
    camera_data.ortho_scale = size * 1.1
    folder = Path(out).resolve() / "preview"
    folder.mkdir(parents=True, exist_ok=True)
    views = {
        "front": Vector((0, 1, 0.12)),
        "side": Vector((1, 0, 0.12)),
        "back": Vector((0, -1, 0.12)),
        "threeq": Vector((0.62, 0.78, 0.15)),
    }
    files = []
    for name, direction in views.items():
        camera.location = center + direction.normalized() * size * 3
        camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
        path = folder / (target.name + "__" + name + ".png")
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        files.append(str(path))
    metadata = {
        "renderer": "workbench",
        "family": family,
        "candidates": [{
            "id": target.name,
            "note": "Hair Tool 4.6.1 TAOM delivery preview",
            "sources": [],
            "tris": sum(len(polygon.vertices) - 2 for polygon in target.data.polygons),
            "verts": len(target.data.vertices),
        }],
        "files": files,
    }
    (folder / "previews.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    return metadata


def finalize(args, version):
    import bpy
    from mathutils import Matrix
    import add_mesh_lods as lods

    options = profile_options(args.profile, args.material, args.reference)
    source = selected_source(args.mesh)
    source_name = source.name
    if not source.ht_props.hair_nodes.is_using_gnodes:
        raise ValueError("%s has no Hair System to finalize" % source.name)
    target_name = options["target"]
    if target_name in bpy.data.objects:
        raise ValueError("an object named %s already exists" % target_name)
    armature = armature_for(args.armature)
    path = output_file(args, ".fbx")
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    result = bpy.ops.hair_system.convert_to_mesh(weld_verts=True)
    if "FINISHED" not in result:
        raise RuntimeError("Hair Tool could not convert %s" % source_name)
    target = bpy.context.view_layer.objects.active
    if target.type != "MESH" or not target.data.polygons:
        raise RuntimeError("Hair Tool produced no mesh for %s" % source_name)
    target.name = target_name
    target.data.name = target_name
    display_matrix = target.matrix_world.copy()
    target.matrix_world = Matrix.Identity(4)
    weight_stats = assign_delivery(target, options, armature)
    target.matrix_world = display_matrix
    if args.preview and not args.overwrite and (Path(args.out) / "preview").exists():
        raise FileExistsError("preview output exists; choose another --out or pass --overwrite")
    preview = render_previews(target, args.head, options["family"], args.out) if args.preview else None
    keep = {target, armature}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    lods.export(str(path))
    lods.load(str(path))
    imported = bpy.data.objects.get(target_name)
    if imported is None:
        raise RuntimeError("staged FBX did not re-import %s" % target_name)
    errors = final_mesh_errors(object_info(imported), options["material"])
    if errors:
        raise RuntimeError("staged FBX failed delivery checks: " + "; ".join(errors))
    return {
        "source": source_name,
        "output": str(path),
        "hair_tool_version": list(version),
        "profile": options,
        "weights": weight_stats,
        "preview": preview,
        "roundtrip": object_info(imported),
    }


def main():
    args = parse_args(sys.argv)
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    report_path = out / "hair-tool-report.json"
    report = {"status": "error", "mode": args.mode, "profile": args.profile}
    try:
        version = load_hair_tool(args.hair_tool)
        if args.mode == "inspect":
            import bpy

            report.update(
                status="ok",
                hair_tool_version=list(version),
                objects=[object_info(obj) for obj in bpy.context.scene.objects],
                profiles=sorted(PROFILES),
            )
        else:
            report.update(author(args, version) if args.mode == "author" else finalize(args, version))
            report["status"] = "ok"
    except BaseException:
        report["trace"] = traceback.format_exc()
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        raise
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
