"""Blender adapter for the independent cube-map quad remesher."""

import json
import os
from pathlib import Path
import sys
import tempfile

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))

from quad_engine.cube_map import remesh as cube_remesh


def remesh_object(obj, target_quads=96, work_dir=None):
    import bpy

    if obj is None or obj.type != "MESH":
        raise ValueError("cube_map_remesh requires a mesh object")
    source = obj.data
    source.calc_loop_triangles()
    vertices = np.asarray([tuple(vertex.co) for vertex in source.vertices], dtype=np.float64)
    triangles = np.asarray([tuple(triangle.vertices) for triangle in source.loop_triangles],
                           dtype=np.int32)
    materials = np.asarray([triangle.material_index for triangle in source.loop_triangles],
                           dtype=np.int32)
    result = cube_remesh(vertices, triangles, materials, target_quads)
    mesh = bpy.data.meshes.new(f"{source.name}_CubeMap")
    mesh.from_pydata(result.vertices.tolist(), [], result.faces.tolist())
    for material in source.materials:
        mesh.materials.append(material)
    for polygon, material_index in zip(mesh.polygons, result.material_ids):
        polygon.material_index = int(material_index)
    smooth = {polygon.use_smooth for polygon in source.polygons}
    if len(smooth) == 1:
        smooth_value = smooth.pop()
        for polygon in mesh.polygons:
            polygon.use_smooth = smooth_value
    remeshed = bpy.data.objects.new(f"{obj.name}_CubeMap", mesh)
    collection = obj.users_collection[0] if obj.users_collection else bpy.context.scene.collection
    collection.objects.link(remeshed)
    remeshed.matrix_world = obj.matrix_world.copy()
    remeshed["quad_engine_metrics"] = json.dumps(result.metrics)
    if work_dir is not None:
        os.makedirs(work_dir, exist_ok=True)
        artifacts = tempfile.mkdtemp(prefix="cube-map-", dir=work_dir)
        np.savez_compressed(
            os.path.join(artifacts, "output.npz"),
            vertices=result.vertices,
            faces=result.faces,
            material_ids=result.material_ids,
            source_triangles=result.source_triangles,
        )
        result.metrics["artifacts"] = artifacts
        with open(os.path.join(artifacts, "report.json"), "w", encoding="utf-8") as handle:
            json.dump(result.metrics, handle, indent=2)
        remeshed["quad_engine_metrics"] = json.dumps(result.metrics)
    return remeshed, result.metrics


def remesh_in_place(obj, target_quads=96, work_dir=None):
    import bpy

    remeshed, metrics = remesh_object(obj, target_quads, work_dir)
    mesh = remeshed.data
    old = obj.data
    obj.data = mesh
    obj["quad_engine_metrics"] = json.dumps(metrics)
    bpy.data.objects.remove(remeshed, do_unlink=True)
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return metrics
