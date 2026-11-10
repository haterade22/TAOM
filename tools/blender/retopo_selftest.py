"""Run inside Blender to verify the TAOM retopology adapter and operators."""
import json
import sys
import traceback
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))


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


def main():
    from tools.blender import retopo_operators

    retopo_operators.register()
    try:
        source = mesh_object(
            "TAOM_Selftest_Source",
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
        activate(source)
        original_source = [tuple(vertex.co) for vertex in source.data.vertices]
        result = bpy.ops.taom.flatten_patterns()
        assert result == {"FINISHED"}, result
        flat = bpy.context.active_object
        assert flat != source
        assert len(flat.data.vertices) == 9
        assert len(flat.data.polygons) == 4
        assert "source_position" in flat.data.attributes

        guides = bpy.data.collections.new("TAOM_Selftest_Guides")
        bpy.context.scene.collection.children.link(guides)
        guide = mesh_object(
            "TAOM_Selftest_Guide",
            [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            [(0, 1, 2, 3)],
            collection=guides,
        )
        settings = bpy.context.scene.taom_retopology
        settings.source_object = source
        settings.guide_collection = guides
        settings.subdiv_reproject = 1
        settings.fit_uv = False
        settings.separate_parts = False
        activate(guide)
        result = bpy.ops.taom.retopologize()
        assert result == {"FINISHED"}, result
        output = bpy.context.active_object
        assert output != guide
        assert len(output.data.vertices) == 9, len(output.data.vertices)
        assert len(output.data.polygons) == 4
        assert output.data.uv_layers.active is not None
        assert output.data.uv_layers.active.name == "UVMap"
        assert any(abs(vertex.co.z - 0.2) < 1e-5 for vertex in output.data.vertices)
        assert [tuple(vertex.co) for vertex in source.data.vertices] == original_source
        assert len(guide.data.vertices) == 4

        activate(flat)
        result = bpy.ops.taom.initialize_planes()
        assert result == {"FINISHED"}, result
        initialized = bpy.context.active_object
        assert initialized != flat
        assert all(len(polygon.vertices) == 4 for polygon in initialized.data.polygons)

        bad = mesh_object(
            "TAOM_Selftest_Bad",
            [(0, 0, 0), (1, 0, 0), (0, 1, 0)],
            [(0, 1, 2)],
            collection=guides,
        )
        activate(bad)
        failed = False
        try:
            bpy.ops.taom.retopologize()
        except RuntimeError as error:
            failed = "quad" in str(error)
        assert failed, "non-quad guide was not rejected"
        non_manifold = mesh_object(
            "TAOM_Selftest_NonManifold",
            [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)],
            [(0, 1, 2), (1, 0, 3), (0, 1, 4)],
        )
        activate(non_manifold)
        assert bpy.ops.taom.inspect_topology(only_debug=True) == {"FINISHED"}
        debug = bpy.context.active_object
        assert len(debug.data.edges) == 1
        assert len(debug.data.polygons) == 0
        activate(non_manifold)
        assert bpy.ops.taom.inspect_topology(fix_split_edges=True) == {"FINISHED"}
        repaired = bpy.context.active_object
        assert len(repaired.data.vertices) == 9
        assert repaired["non_manifold_edges"] == 1
        return {
            "status": "ok",
            "flatten_vertices": len(flat.data.vertices),
            "output_vertices": len(output.data.vertices),
            "output_faces": len(output.data.polygons),
            "uv_layer": output.data.uv_layers.active.name,
            "initialized_faces": len(initialized.data.polygons),
            "invalid_guide_rejected": failed,
            "inspector_debug_edges": len(debug.data.edges),
            "inspector_repaired_vertices": len(repaired.data.vertices),
        }
    finally:
        retopo_operators.unregister()


out = Path(__file__).resolve().with_name("retopo_selftest_report.json")
try:
    report = main()
except BaseException:
    report = {"status": "error", "trace": traceback.format_exc()}
out.write_text(json.dumps(report, indent=2), encoding="utf-8")
