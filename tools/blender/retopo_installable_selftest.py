"""Install and verify the packaged TAOM and Petik add-ons in Blender."""
import json
import os
import traceback
from pathlib import Path

import bpy


ROOT = Path(os.environ.get("RETOPO_VERIFY_ROOT", str(Path(__file__).resolve().parents[2])))
ARCHIVE_ROOT = Path(os.environ.get("RETOPO_PACKAGE_ROOT", str(ROOT / "installable")))
OUT = ROOT / "TestResults" / "Blender" / "installable_report.json"
OUT.parent.mkdir(parents=True, exist_ok=True)


def mesh_object(name, vertices, faces, uv=None, collection=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    (collection or bpy.context.scene.collection).objects.link(obj)
    if uv is not None:
        layer = mesh.uv_layers.new(name="UVMap")
        for loop, coordinate in zip(layer.data, uv):
            loop.uv = coordinate
    return obj


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def reset_preview(settings):
    for name in (
        "points_guide", "mismatch_lines", "only_patterns", "only_seams", "wireframe",
        "seams", "polyloops", "hide_viewport_info", "sibling_contact", "edge_count",
        "manual_density", "show_snapping_distance", "preview_source_cloth",
    ):
        setattr(settings, name, False)


def main():
    if not os.environ.get("BLENDER_USER_SCRIPTS"):
        raise RuntimeError("set BLENDER_USER_SCRIPTS to a disposable directory before installing test add-ons")
    report = {"installed": []}
    for name in ("taom_retopology", "Petik"):
        archive = ARCHIVE_ROOT / f"{name}.zip"
        bpy.ops.preferences.addon_install(filepath=str(archive), overwrite=True)
        bpy.ops.preferences.addon_enable(module=name)
        report["installed"].append(name)
    for operator in (bpy.ops.mdutil.multi_petik, bpy.ops.mdutil.petik_line):
        operator.get_rna_type()
    report["petik_operators"] = True

    source = mesh_object(
        "Package Source",
        [
            (-0.5, -0.5, 0.0), (0.0, -0.5, 0.1), (0.5, -0.5, 0.0),
            (-0.5, 0.0, 0.1), (0.0, 0.0, 0.2), (0.5, 0.0, 0.1),
            (-0.5, 0.5, 0.0), (0.0, 0.5, 0.1), (0.5, 0.5, 0.0),
        ],
        [(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7)],
        [
            (0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5),
            (0.5, 0), (1, 0), (1, 0.5), (0.5, 0.5),
            (0, 0.5), (0.5, 0.5), (0.5, 1), (0, 1),
            (0.5, 0.5), (1, 0.5), (1, 1), (0.5, 1),
        ],
    )
    guides = bpy.data.collections.new("RetopoPlanes")
    bpy.context.scene.collection.children.link(guides)
    guide = mesh_object(
        "Package Guide", [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], [(0, 1, 2, 3)], collection=guides
    )
    settings = bpy.context.scene.taom_retopology
    settings.source_object = source
    settings.guide_collection = guides
    settings.fit_uv = False
    settings.separate_parts = False
    settings.hide_debug = True
    settings.subdiv_reproject = 1
    settings.debug_material = "None"
    activate(guide)
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    output = bpy.context.active_object
    assert len(output.data.vertices) == 9
    assert len(output.data.polygons) == 4
    assert output.data.uv_layers.active.name == "UVMap"
    report["base"] = {"vertices": len(output.data.vertices), "faces": len(output.data.polygons)}

    activate(source)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT.parent / "example_input.blend"))

    report["materials"] = {}
    for mode in ("Patterns", "Parts", "Retopo Planes", "None"):
        settings.debug_material = mode
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        output = bpy.context.active_object
        report["materials"][mode] = len(output.data.materials)
    OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    assert report["materials"]["None"] == 0
    assert all(report["materials"][mode] > 0 for mode in ("Patterns", "Parts", "Retopo Planes"))

    contact = mesh_object(
        "Package Contact Guide", [(1, 0, 0), (2, 0, 0), (2, 1, 0), (1, 1, 0)], [(0, 1, 2, 3)], collection=guides
    )
    crease = guide.data.attributes.get("crease_edge") or guide.data.attributes.new(name="crease_edge", type="FLOAT", domain="EDGE")
    bottom = next(edge for edge in guide.data.edges if all(guide.data.vertices[index].co.y == 0 for index in edge.vertices))
    crease.data[bottom.index].value = 0.5
    report["previews"] = {}
    for name in (
        "points_guide", "mismatch_lines", "wireframe", "seams", "polyloops",
        "sibling_contact", "edge_count", "manual_density", "show_snapping_distance",
        "preview_source_cloth",
    ):
        reset_preview(settings)
        settings.hide_debug = False
        setattr(settings, name, True)
        if name == "manual_density":
            crease = guide.data.attributes.get("crease_edge") or guide.data.attributes.new(name="crease_edge", type="FLOAT", domain="EDGE")
            for item in crease.data:
                item.value = 0.0
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        output = bpy.context.active_object
        if name == "wireframe":
            report["previews"][name] = output.modifiers.get("TAOM Wireframe") is not None
        elif name == "seams":
            report["previews"][name] = any(edge.use_edge_sharp for edge in output.data.edges)
        else:
            helpers = bpy.data.collections[output.name + " Helpers"]
            report["previews"][name] = len(helpers.objects)
        OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
        assert report["previews"][name]
    reset_preview(settings)
    settings.hide_debug = False
    settings.morph_2d_3d = 0.5
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    report["morph_setting"] = settings.morph_2d_3d
    report["morph_objects"] = [obj.name for obj in bpy.data.objects if "Morph" in obj.name]
    OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    assert bpy.data.objects.get("TAOM Morph Preview") is not None
    report["morph_preview"] = True
    settings.morph_2d_3d = 0.0
    bpy.data.objects.remove(contact, do_unlink=True)
    guide.data.attributes.remove(guide.data.attributes["crease_edge"])
    settings.hide_debug = True

    settings.subdiv_reproject = 0
    settings.thickness_enabled = True
    settings.thickness = 0.05
    settings.thickness_offset = 0.0
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    output = bpy.context.active_object
    report["thickness"] = {"vertices": len(output.data.vertices), "faces": len(output.data.polygons)}
    assert report["thickness"] == {"vertices": 8, "faces": 6}
    settings.thickness_enabled = False

    settings.output_generated_planes = True
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    output = bpy.context.active_object
    assert len(output.data.vertices) == 4
    assert len(output.data.polygons) == 1
    report["generated_planes"] = True
    settings.output_generated_planes = False

    settings.only_patterns = True
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    output = bpy.context.active_object
    assert len(output.data.vertices) == 9
    report["only_patterns"] = True
    settings.only_patterns = False

    settings.only_seams = True
    assert bpy.ops.taom.retopologize() == {"FINISHED"}
    seams = bpy.context.active_object
    assert len(seams.data.edges) == 8
    assert len(seams.data.polygons) == 0
    report["only_seams"] = True
    settings.only_seams = False

    activate(guide)
    assert bpy.ops.taom.mirror_guides(axis="xy") == {"FINISHED"}
    mirrored = bpy.context.active_object
    assert len(mirrored.data.polygons) >= 1
    report["mirror"] = True

    settings.auto_fill_max_iterations = 1
    activate(guide)
    assert bpy.ops.taom.auto_fill() == {"FINISHED"}
    assert len(bpy.context.active_object.data.polygons) >= 1
    assert all(len(polygon.vertices) == 4 for polygon in bpy.context.active_object.data.polygons)
    report["auto_fill"] = True

    activate(guide)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    assert bpy.ops.taom.neutralize_crease() == {"FINISHED"}
    bpy.ops.object.mode_set(mode="OBJECT")
    crease = guide.data.attributes["crease_edge"]
    assert all(item.value == 0.5 for item in crease.data)
    report["crease"] = True

    activate(guide)
    assert bpy.ops.taom.validate_retopology() == {"FINISHED"}
    report["validation"] = True
    for name in reversed(report["installed"]):
        bpy.ops.preferences.addon_disable(module=name)
    try:
        bpy.ops.taom.retopologize.get_rna_type()
    except (KeyError, RuntimeError):
        report["clean_disable"] = True
    assert report.get("clean_disable") is True
    report["status"] = "ok"
    return report


try:
    result = main()
except BaseException:
    partial = json.loads(OUT.read_text()) if OUT.exists() else {}
    result = {"status": "error", "trace": traceback.format_exc(), "partial": partial}
OUT.write_text(json.dumps(result, indent=2), encoding="utf-8")
