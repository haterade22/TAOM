"""Transactional curve-to-mesh conversion, joining, welding, and pin weights."""

from dataclasses import dataclass

import numpy as np

from .mesh import MeshData
from .model import Hair
from .profiles import Profile, profile_mesh


@dataclass
class ConversionResult:
    mesh: MeshData
    source: Hair


def join_meshes(meshes):
    vertices = []
    faces = []
    uv_layers = {}
    materials = []
    material_indices = []
    attributes = {}
    edges = []
    vertex_offset = 0
    for mesh in meshes:
        vertices.append(mesh.vertices)
        faces.extend(tuple(vertex_offset + index for index in face) for face in mesh.faces)
        edges.extend((vertex_offset + a, vertex_offset + b) for a, b in mesh.edges)
        for name, values in mesh.uv_layers.items():
            uv_layers.setdefault(name, []).append(values)
        local_materials = []
        for material in mesh.materials:
            if material not in materials:
                materials.append(material)
            local_materials.append(materials.index(material))
        material_indices.extend(local_materials[index] for index in mesh.material_indices)
        for name, values in mesh.point_attributes.items():
            attributes.setdefault(name, []).append(values)
        vertex_offset += len(mesh.vertices)
    merged_attributes = {}
    for name, chunks in attributes.items():
        expected = vertex_offset
        merged = np.zeros(expected, dtype=np.asarray(chunks[0]).dtype)
        offset = 0
        for mesh in meshes:
            if name in mesh.point_attributes:
                values = mesh.point_attributes[name]
                merged[offset:offset + len(mesh.vertices)] = values
            offset += len(mesh.vertices)
        merged_attributes[name] = merged
    return MeshData(
        np.concatenate(vertices) if vertices else np.empty((0, 3)),
        faces,
        {name: np.concatenate(chunks) for name, chunks in uv_layers.items()},
        materials or ["TAOM_Hair"],
        np.asarray(material_indices, dtype=np.int32),
        merged_attributes,
        edges,
    )


def curves_to_mesh(hair, profiles=None, default_profile=None):
    profiles = profiles or {}
    default_profile = default_profile or Profile("flat")
    meshes = []
    for strand in hair.strands:
        profile = profiles.get(strand.profile_id, default_profile)
        mesh = profile_mesh(strand, profile)
        count = len(mesh.vertices)
        mesh.point_attributes.update({
            "profile_id": np.full(count, strand.profile_id, dtype=np.int32),
            "group_id": np.full(count, strand.group_id, dtype=np.int32),
            "uv_region": np.full(count, strand.uv_region, dtype=np.int32),
        })
        meshes.append(mesh)
    return ConversionResult(join_meshes(meshes), hair.copy())


def weld_mesh(mesh, tolerance=1e-6):
    if tolerance <= 0 or not np.isfinite(tolerance):
        raise ValueError("weld tolerance must be finite and positive")
    keys = np.round(mesh.vertices / tolerance).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    order = np.argsort(first)
    remap = np.empty_like(inverse)
    remap[order] = np.arange(len(first))
    inverse = remap[inverse]
    faces = []
    kept_loop_positions = []
    kept_face_indices = []
    loop_position = 0
    for face_index, face in enumerate(mesh.faces):
        welded = tuple(int(inverse[index]) for index in face)
        if len(set(welded)) >= 3:
            faces.append(welded)
            kept_loop_positions.extend(range(loop_position, loop_position + len(face)))
            kept_face_indices.append(face_index)
        loop_position += len(face)
    kept_loop_positions = np.asarray(kept_loop_positions, dtype=np.int32)
    uv_layers = {name: values[kept_loop_positions] for name, values in mesh.uv_layers.items()}
    material_indices = mesh.material_indices[np.asarray(kept_face_indices, dtype=np.int32)]
    attributes = {name: values[first[order]] for name, values in mesh.point_attributes.items()}
    edges = list({tuple(sorted((int(inverse[a]), int(inverse[b])))) for a, b in mesh.edges
                  if inverse[a] != inverse[b]})
    return MeshData(mesh.vertices[first[order]], faces, uv_layers, list(mesh.materials),
                    material_indices, attributes, edges)


def pin_weights(mesh, power=1.0):
    if power <= 0 or not np.isfinite(power):
        raise ValueError("pin-weight power must be finite and positive")
    if "root_t" not in mesh.point_attributes:
        raise ValueError("mesh has no root_t attribute")
    return np.asarray(mesh.point_attributes["root_t"], dtype=np.float64) ** power
