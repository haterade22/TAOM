"""Short-hair topology rebuilding and card generation."""

import numpy as np

from .convert import curves_to_mesh
from .generation import surface_guides
from .mesh import MeshData
from .model import Hair


def rebuild_topology(vertices, faces, mode="CELLS_TRIS", smoothing=0):
    if mode not in {"KEEP", "CELLS_TRIS", "DIAMOND", "DIAMOND_TRIS"}:
        raise ValueError(f"unknown short-hair topology {mode!r}")
    vertices = np.asarray(vertices, dtype=np.float64)
    faces = np.asarray(faces, dtype=np.int32)
    if vertices.ndim != 2 or vertices.shape[1] != 3 or faces.ndim != 2 or faces.shape[1] != 3:
        raise ValueError("short hair requires Nx3 vertices and Mx3 triangles")
    if not len(faces):
        raise ValueError("short hair requires at least one triangle")
    if mode == "KEEP":
        return MeshData(vertices.copy(), [tuple(face) for face in faces])
    output_vertices = vertices.tolist()
    output_faces = []
    midpoint_cache = {}

    def midpoint(a, b):
        key = tuple(sorted((int(a), int(b))))
        if key not in midpoint_cache:
            midpoint_cache[key] = len(output_vertices)
            output_vertices.append(((vertices[a] + vertices[b]) * 0.5).tolist())
        return midpoint_cache[key]

    for a, b, c in faces:
        center = len(output_vertices)
        output_vertices.append(vertices[[a, b, c]].mean(axis=0).tolist())
        if mode == "CELLS_TRIS":
            output_faces.extend(((int(a), int(b), center), (int(b), int(c), center),
                                 (int(c), int(a), center)))
            continue
        ab, bc, ca = midpoint(a, b), midpoint(b, c), midpoint(c, a)
        quads = ((int(a), ab, center, ca), (int(b), bc, center, ab), (int(c), ca, center, bc))
        for quad in quads:
            if mode == "DIAMOND":
                output_faces.append(quad)
            else:
                output_faces.extend(((quad[0], quad[1], quad[2]), (quad[0], quad[2], quad[3])))
    result = MeshData(np.asarray(output_vertices), output_faces)
    if smoothing:
        if int(smoothing) != smoothing or smoothing < 0:
            raise ValueError("smoothing must be a nonnegative integer")
        for _ in range(int(smoothing)):
            sums = np.zeros_like(result.vertices)
            counts = np.zeros(len(result.vertices))
            for face in result.faces:
                for index in face:
                    neighbors = [vertex for vertex in face if vertex != index]
                    sums[index] += result.vertices[neighbors].mean(axis=0)
                    counts[index] += 1
            mask = counts > 0
            result.vertices[mask] = (result.vertices[mask] + sums[mask] / counts[mask, np.newaxis]) * 0.5
    return result


def generate(vertices, faces, mode="CELLS_TRIS", count=100, length=0.05,
             resolution=4, seed=0, profile=None, smoothing=0):
    from .profiles import Profile
    base = rebuild_topology(vertices, faces, mode, smoothing)
    guides = surface_guides(base.vertices, np.asarray(base.faces, dtype=np.int32), count,
                            length=length, resolution=resolution, seed=seed)
    result = curves_to_mesh(Hair(guides), default_profile=profile or Profile("flat", width=0.005))
    result.mesh.uv_layers["SampledUV"] = result.mesh.uv_layers["UVMap"].copy()
    result.mesh.materials = ["ShortHairMaterial"]
    result.mesh.material_indices = np.zeros(len(result.mesh.faces), dtype=np.int32)
    return result
