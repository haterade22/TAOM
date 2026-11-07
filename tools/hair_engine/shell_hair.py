"""Layered shell hair with explicit per-layer mesh ownership."""

import numpy as np

from .convert import join_meshes
from .mesh import MeshData


def _vertex_normals(mesh):
    normals = np.zeros_like(mesh.vertices)
    for face in mesh.faces:
        a, b, c = (mesh.vertices[index] for index in face[:3])
        normal = np.cross(b - a, c - a)
        length = np.linalg.norm(normal)
        if length == 0:
            raise ValueError("shell base contains a degenerate face")
        normal /= length
        for index in face:
            normals[index] += normal
    lengths = np.linalg.norm(normals, axis=1)
    if np.any(lengths == 0):
        raise ValueError("shell base has an isolated vertex")
    return normals / lengths[:, np.newaxis]


def generate(base, layers=12, spacing=0.001):
    if int(layers) != layers or layers < 1:
        raise ValueError("layers must be a positive integer")
    if spacing < 0 or not np.isfinite(spacing):
        raise ValueError("shell spacing must be finite and nonnegative")
    normals = _vertex_normals(base)
    shells = []
    for layer in range(int(layers)):
        mesh = base.copy()
        mesh.vertices = base.vertices + normals * (spacing * layer)
        mesh.point_attributes["shell_layer"] = np.full(len(mesh.vertices), layer, dtype=np.int32)
        mesh.materials = ["ShellHairMaterial"]
        mesh.material_indices = np.zeros(len(mesh.faces), dtype=np.int32)
        shells.append(mesh)
    return shells


def join_shells(shells):
    if not shells:
        raise ValueError("at least one shell is required")
    return join_meshes(shells)
