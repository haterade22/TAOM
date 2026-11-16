"""Run the independent differential cases against a supplied reference .blend."""
import json
from pathlib import Path
import sys
import traceback

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
OUTPUT = Path(sys.argv[sys.argv.index("--") + 1])

try:
    from tools.retopo_engine import Mesh, differential  # noqa: E402
except BaseException:
    OUTPUT.write_text(json.dumps({"fatal": traceback.format_exc()}, indent=2), encoding="utf-8")
    raise


SETTING_NAMES = {
    "scale_gui": "Scale GUI",
    "offset_3d_cloth": "Offset 3D Cloth",
    "initialize_planes": "Initialize Planes",
    "angle_threshold": "Angle Threshold",
    "corners_threshold": "Corners Threshold",
    "median_corners_threshold": "Median Corners Threshold",
    "auto_fill_prepass": "Auto Fill Prepass (Point Guides)",
    "auto_fill": "Auto Fill",
    "auto_fill_max_iterations": "Auto Fill Max Iterations",
    "output_generated_planes": "Output Generated Planes",
    "base_density": "Base Density",
    "snapping_distance": "Snapping Distance",
    "seams_merge_distance": "Seams Merge Distance",
    "smooth_low_poly": "Smooth LowPoly",
    "auto_density": "Auto Density",
    "full_crease_control": "Full Crease Control",
    "density_add": "Density Add",
    "density_add_threshold": "Density Add Threshold ",
    "subdiv_reproject": "SubDiv + Reproject",
    "smooth_uvs": "Smooth UVs",
    "fit_uv": "Fit UVs to 1st Tile",
    "separate_patterns": "Seperate Patterns",
    "separate_parts": "Seperate Parts",
    "thickness_enabled": "ON/OFF",
    "thickness": "Thickness",
    "thickness_offset": "Offset",
    "hide_debug": "Hide Debug",
    "points_guide": "Points Guide",
    "mismatch_lines": "Mismatch Lines",
    "only_patterns": "Only Patterns",
    "only_seams": "Only Seams",
    "wireframe": "Wireframe",
    "seams": "Seams",
    "polyloops": "PolyLoops",
    "debug_material": "Debug Material",
    "hide_viewport_info": "Hide Viewport Info",
    "morph_2d_3d": "Morph 2D > 3D",
    "sibling_contact": "Sibling Contact",
    "edge_count": "Edge Count",
    "manual_density": "Manual Density (Crease)",
    "show_snapping_distance": "SnappingDistance",
    "preview_source_cloth": "Preview Source Cloth",
    "debug_material_seed": "Debug Mat Seed",
    "ui_hue": "UI Hue",
}


def mesh_object(name, mesh, collection=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(mesh.vertices, mesh.lines, mesh.faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    (collection or bpy.context.scene.collection).objects.link(obj)
    if mesh.uv:
        layer = data.uv_layers.new(name=mesh.uv_name)
        for loop, coordinate in zip(layer.data, mesh.uv):
            loop.uv = coordinate
    for name, values in mesh.edge_attributes.items():
        attribute = data.attributes.new(name=name, type="BOOLEAN" if name == "sharp_edge" else "FLOAT", domain="EDGE")
        for index, item in enumerate(attribute.data):
            item.value = values.get(Mesh.edge_key(*data.edges[index].vertices), False if name == "sharp_edge" else 0.0)
    return obj


def set_input(modifier, name, value):
    for item in modifier.node_group.interface.items_tree:
        if item.item_type == "SOCKET" and item.in_out == "INPUT" and item.name == name:
            getattr(modifier.properties.inputs, item.identifier).value = value
            return
    raise KeyError(name)


def add_modifier(obj, group_name):
    modifier = obj.modifiers.new(group_name, "NODES")
    modifier.node_group = bpy.data.node_groups[group_name]
    return modifier


def evaluated_mesh(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = evaluated.to_mesh()
    vertices = [tuple(vertex.co) for vertex in data.vertices]
    faces = [tuple(polygon.vertices) for polygon in data.polygons]
    layer = data.uv_layers.active
    if layer:
        uv = [tuple(loop.uv) for loop in layer.data]
        uv_name = layer.name
    else:
        attribute = data.attributes.get("UVMap")
        uv = [tuple(item.vector) for item in attribute.data] if attribute else []
        uv_name = "UVMap"
    mesh = Mesh(vertices=vertices, faces=faces, uv=uv, uv_name=uv_name)
    evaluated.to_mesh_clear()
    return mesh


def flatten(source):
    modifier = add_modifier(source, "3DtoPatterns")
    set_input(modifier, "UV Map Name", "UVMap")
    set_input(modifier, "Simplify On/Off", False)
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(source.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    obj = bpy.data.objects.new(source.name + "_Flat", mesh)
    bpy.context.scene.collection.objects.link(obj)
    source.modifiers.remove(modifier)
    return obj


def run_case(case):
    source = mesh_object("Reference Source", case["source"])
    flat = flatten(source)
    if case["point_guides"]:
        attribute = flat.data.attributes.get("PointGuideBool") or flat.data.attributes.new(name="PointGuideBool", type="BOOLEAN", domain="POINT")
        for index in case["point_guides"]:
            attribute.data[index].value = True
    collection = bpy.data.collections.new("Reference Guides")
    bpy.context.scene.collection.children.link(collection)
    guide = mesh_object("Reference Guide", case["guide"], collection)
    host = guide.copy()
    host.data = guide.data.copy()
    host.name = "Reference Host"
    bpy.context.scene.collection.objects.link(host)
    modifier = add_modifier(host, "RetopoPlanes")
    set_input(modifier, "Cloth Pattern", flat)
    set_input(modifier, "Retopo Planes", collection)
    settings = case["settings"]
    for field, original_name in SETTING_NAMES.items():
        set_input(modifier, original_name, getattr(settings, field))
    for name in (
        "Points Guide", "Mismatch Lines", "Only Patterns", "Only Seams", "Wireframe",
        "Seams", "PolyLoops", "Hide Viewport Info", "Preview Source Cloth",
    ):
        set_input(modifier, name, False)
    set_input(modifier, "Hide Debug", True)
    bpy.context.view_layer.update()
    result = differential.summary(evaluated_mesh(host))
    result["warnings"] = [warning.message for warning in modifier.node_warnings]
    if case["name"].startswith(("mirror", "auto_fill")):
        result["flat_attributes"] = {
            attribute.name: [str(getattr(item, "value", getattr(item, "vector", ""))) for item in attribute.data]
            for attribute in flat.data.attributes
            if attribute.name in {"PatternCenter", "PatternID", "PointGuideBool", "3DPosition"}
        }
        result["guide_loose_edges"] = [
            [tuple(guide.data.vertices[index].co) for index in edge.vertices]
            for edge in guide.data.edges
            if not any(set(edge.vertices).issubset(set(polygon.vertices)) for polygon in guide.data.polygons)
        ]
    for obj in (host, guide, flat, source):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(collection)
    return result


def run_inspector(fix, only_debug):
    obj = mesh_object("Reference Inspector", differential.non_manifold())
    modifier = add_modifier(obj, "0RetopoInspector")
    set_input(modifier, "Fix (Split Edges)", fix)
    set_input(modifier, "Only Debug", only_debug)
    bpy.context.view_layer.update()
    result = differential.summary(evaluated_mesh(obj))
    bpy.data.objects.remove(obj, do_unlink=True)
    return result


def main():
    output = Path(sys.argv[sys.argv.index("--") + 1])
    report = {"cases": {}, "inspector": {}}
    for case in differential.cases():
        try:
            report["cases"][case["name"]] = run_case(case)
        except BaseException:
            report["cases"][case["name"]] = {"error": traceback.format_exc()}
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    for fix in (False, True):
        for only_debug in (False, True):
            key = f"fix_{fix}_debug_{only_debug}"
            try:
                report["inspector"][key] = run_inspector(fix, only_debug)
            except BaseException:
                report["inspector"][key] = {"error": traceback.format_exc()}
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    try:
        main()
    except BaseException:
        OUTPUT.write_text(json.dumps({"fatal": traceback.format_exc()}, indent=2), encoding="utf-8")
        raise
