"""Run inside Blender to verify the TAOM retopology adapter and operators."""
import json
import sys
import traceback
from pathlib import Path

import bpy
from mathutils import Matrix

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
        shifted_coordinates = [(loop.uv.x + 0.25, loop.uv.y + 0.5) for loop in source.data.uv_layers.active.data]
        alternate_uv = source.data.uv_layers.new(name="GarmentUV")
        for loop, coordinate in zip(alternate_uv.data, shifted_coordinates):
            loop.uv = coordinate
        source.data.uv_layers.active_index = 0
        activate(source)
        assert bpy.ops.taom.flatten_patterns(uv_name="GarmentUV") == {"FINISHED"}
        named_flat = bpy.context.active_object
        assert named_flat.data.uv_layers.active.name == "GarmentUV"
        assert len(named_flat.data.vertices) == 9
        assert min(vertex.co.x for vertex in named_flat.data.vertices) == 0.25
        assert min(vertex.co.y for vertex in named_flat.data.vertices) == 0.5
        assert source.data.uv_layers.active_index == 0

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
        named_guides = bpy.data.collections.new("TAOM_Selftest_NamedGuides")
        bpy.context.scene.collection.children.link(named_guides)
        named_guide = mesh_object(
            "TAOM_Selftest_NamedGuide",
            [(0.25, 0.5, 0), (1.25, 0.5, 0), (1.25, 1.5, 0), (0.25, 1.5, 0)],
            [(0, 1, 2, 3)],
            collection=named_guides,
        )
        settings.guide_collection = named_guides
        settings.fit_uv = False
        settings.separate_parts = False
        activate(named_guide)
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        named_output = bpy.context.active_object
        assert named_output.data.uv_layers.active.name == "GarmentUV"
        assert min(loop.uv.x for loop in named_output.data.uv_layers.active.data) >= 0.25
        assert min(loop.uv.y for loop in named_output.data.uv_layers.active.data) >= 0.5
        settings.source_uv_name = "UVMap"
        settings.guide_collection = guides
        settings.subdiv_reproject = 1
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
        settings.morph_2d_3d = 0.5
        settings.hide_debug = False
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        output = bpy.context.active_object
        preview = bpy.data.objects.get("TAOM Morph Preview")
        assert preview is not None, "morph preview helper was not created"
        assert len(preview.data.vertices) == len(output.data.vertices)
        assert any(abs(vertex.co.z - 0.1) < 1e-5 for vertex in preview.data.vertices)
        assert any(abs(vertex.co.z - 0.2) < 1e-5 for vertex in output.data.vertices)
        settings.morph_2d_3d = 0.0
        settings.base_density = 20
        crease = guide.data.attributes.new(name="crease_edge", type="FLOAT", domain="EDGE")
        bottom = next(edge for edge in guide.data.edges if all(guide.data.vertices[index].co.y == 0 for index in edge.vertices))
        crease.data[bottom.index].value = 0.5
        mesh_object(
            "TAOM_Selftest_ContactGuide",
            [(1, 0, 0), (2, 0, 0), (2, 1, 0), (1, 1, 0)],
            [(0, 1, 2, 3)],
            collection=guides,
        )
        settings.mismatch_lines = True
        settings.polyloops = True
        settings.sibling_contact = True
        settings.edge_count = True
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        for name in ("TAOM Mismatch Lines", "TAOM PolyLoops", "TAOM Sibling Contact"):
            assert bpy.data.objects.get(name) is not None, name
            assert len(bpy.data.objects[name].data.polygons) == 0, name
        helpers = bpy.data.collections[bpy.context.active_object.name + " Helpers"]
        assert any(obj.data.body == "2" for obj in helpers.objects if obj.name.startswith("TAOM Edge"))
        settings.hide_debug = True

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
        transformed_guides = bpy.data.collections.new("TAOM_Selftest_TransformedGuides")
        bpy.context.scene.collection.children.link(transformed_guides)
        transformed_guide = mesh_object(
            "TAOM_Selftest_TransformedGuide",
            [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            [(0, 1, 2, 3)],
            collection=transformed_guides,
        )
        transformed_guide.location = (0.25, 0.25, 0)
        source.location = (5, 6, 0)
        settings.guide_collection = transformed_guides
        settings.subdiv_reproject = 0
        activate(transformed_guide)
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        transformed_output = bpy.context.active_object
        assert transformed_output.matrix_world == source.matrix_world
        assert min(loop.uv.x for loop in transformed_output.data.uv_layers.active.data) >= 0.25
        assert min(loop.uv.y for loop in transformed_output.data.uv_layers.active.data) >= 0.25
        settings.only_patterns = True
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        assert bpy.context.active_object.matrix_world == Matrix.Identity(4)
        settings.only_patterns = False
        settings.output_generated_planes = True
        assert bpy.ops.taom.retopologize() == {"FINISHED"}
        assert bpy.context.active_object.matrix_world == Matrix.Identity(4)
        settings.output_generated_planes = False
        point_guides = transformed_guide.data.attributes.new(name="point_guide", type="BOOLEAN", domain="POINT")
        for index, item in enumerate(point_guides.data):
            item.value = index % 2 == 0
        activate(transformed_guide)
        assert bpy.ops.taom.mirror_guides(axis="x") == {"FINISHED"}
        mirrored = bpy.context.active_object
        assert "point_guide" in mirrored.data.attributes
        assert len(mirrored.data.attributes["point_guide"].data) == len(mirrored.data.vertices)
        assert sum(item.value for item in mirrored.data.attributes["point_guide"].data) > 0
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


out_dir = Path(__file__).resolve().parents[2] / "TestResults" / "Blender"
out_dir.mkdir(parents=True, exist_ok=True)
out = out_dir / "retopo_selftest_report.json"
try:
    report = main()
except BaseException:
    report = {"status": "error", "trace": traceback.format_exc()}
out.write_text(json.dumps(report, indent=2), encoding="utf-8")
