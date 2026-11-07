"""Flat, round, curl, braid, multi-curve, and custom mesh profiles."""

from dataclasses import dataclass

import numpy as np

from .curves import parallel_transport
from .mesh import MeshData


@dataclass(frozen=True)
class Profile:
    kind: str
    width: float = 0.01
    resolution: int = 2
    roundness: float = 0.5
    braid_turns: float = 4.0
    uv_cells: int = 2
    mesh_vertices: np.ndarray | None = None
    mesh_edges: tuple[tuple[int, int], ...] = ()

    def __post_init__(self):
        if self.kind not in {"flat", "round", "curl", "braid", "mesh"}:
            raise ValueError(f"unknown profile kind {self.kind!r}")
        if self.width <= 0 or not np.isfinite(self.width):
            raise ValueError("profile width must be finite and positive")
        if int(self.resolution) != self.resolution or self.resolution < 2:
            raise ValueError("profile resolution must be an integer of at least two")
        if self.kind == "round" and self.resolution < 3:
            raise ValueError("round profiles need at least three cross-section points")
        if int(self.uv_cells) != self.uv_cells or self.uv_cells < 1:
            raise ValueError("uv_cells must be a positive integer")
        if self.kind == "mesh":
            vertices = np.asarray(self.mesh_vertices, dtype=np.float64)
            if vertices.ndim != 2 or vertices.shape[1] < 2 or len(vertices) < 2:
                raise ValueError("mesh profiles need at least two Nx2 cross-section vertices")


def _uvs(strand, profile, ring_size, loop_vertices):
    t = np.linspace(0.0, 1.0, len(strand.positions))
    across = np.linspace(0.0, 1.0, ring_size)
    visible = np.zeros((len(strand.positions), ring_size, 2))
    helper = np.zeros_like(visible)
    helper[:, :, 0] = across[np.newaxis, :]
    helper[:, :, 1] = t[:, np.newaxis]
    region = strand.uv_region
    if region < 0:
        visible[:, :, 0] = across[np.newaxis, :]
        visible[:, :, 1] = t[:, np.newaxis]
    else:
        cells = profile.uv_cells
        if region >= cells * cells:
            raise ValueError(f"UV region {region} is outside the {cells}x{cells} layout")
        visible[:, :, 0] = ((region % cells) + across[np.newaxis, :]) / cells
        visible[:, :, 1] = ((region // cells) + t[:, np.newaxis]) / cells
    flat = loop_vertices
    return {
        "UVMap": visible.reshape(-1, 2)[flat],
        "HairTool_UV": helper.reshape(-1, 2)[flat],
    }


def _cross_section(strand, profile, normals, binormals):
    if profile.kind == "round":
        angles = np.linspace(0.0, 2.0 * np.pi, profile.resolution, endpoint=False)
        return np.column_stack((np.cos(angles), np.sin(angles)))
    return np.column_stack((np.linspace(-1.0, 1.0, profile.resolution), np.zeros(profile.resolution)))


def profile_mesh(strand, profile):
    if profile.kind == "braid":
        from .convert import join_meshes
        from .model import Strand
        _, normals, binormals = parallel_transport(strand.positions)
        t = np.linspace(0.0, 1.0, len(strand.positions))
        meshes = []
        for index in range(3):
            angle = 2.0 * np.pi * (profile.braid_turns * t + index / 3.0)
            offset = normals * np.cos(angle)[:, np.newaxis] + binormals * np.sin(angle)[:, np.newaxis]
            child = Strand(strand.positions + offset * (profile.width * 0.25), strand.radius / 3.0,
                           strand.tilt, strand.profile_id, strand.uv_region, strand.group_id, strand.tags)
            meshes.append(profile_mesh(child, Profile("flat", profile.width / 3.0, 2,
                                                       profile.roundness, uv_cells=profile.uv_cells)))
        return join_meshes(meshes)

    _, normals, binormals = parallel_transport(strand.positions)
    vertices = []
    if profile.kind == "mesh":
        cross = np.asarray(profile.mesh_vertices, dtype=np.float64)[:, :2] * (profile.width * 0.5)
        for center, normal, binormal in zip(strand.positions, normals, binormals):
            vertices.extend(center + cross[:, 0, np.newaxis] * binormal
                            + cross[:, 1, np.newaxis] * normal)
        vertices = np.asarray(vertices)
        edges = []
        ring_size = len(cross)
        for ring in range(len(strand.positions)):
            base = ring * ring_size
            edges.extend((base + a, base + b) for a, b in profile.mesh_edges)
        return MeshData(vertices, [], edges=edges)

    cross = _cross_section(strand, profile, normals, binormals)
    ring_size = len(cross)
    for center, normal, binormal in zip(strand.positions, normals, binormals):
        if profile.kind == "round":
            points = center + profile.width * 0.5 * (
                cross[:, 0, np.newaxis] * normal + cross[:, 1, np.newaxis] * binormal)
        else:
            across = np.linspace(0.0, 1.0, ring_size)
            uplift = profile.roundness * profile.width * 0.5 * np.sin(np.pi * across)
            if profile.kind == "flat":
                uplift[:] = 0.0
            points = center + profile.width * 0.5 * cross[:, 0, np.newaxis] * binormal
            points = points + uplift[:, np.newaxis] * normal
        vertices.extend(points)
    vertices = np.asarray(vertices)
    faces = []
    side_count = ring_size if profile.kind == "round" else ring_size - 1
    for ring in range(len(strand.positions) - 1):
        base = ring * ring_size
        next_base = base + ring_size
        for index in range(side_count):
            following = (index + 1) % ring_size
            faces.append((base + index, base + following, next_base + following, next_base + index))
    loop_vertices = np.asarray([index for face in faces for index in face], dtype=np.int32)
    root_t = np.repeat(np.linspace(0.0, 1.0, len(strand.positions)), ring_size)
    return MeshData(vertices, faces, _uvs(strand, profile, ring_size, loop_vertices),
                    point_attributes={"root_t": root_t})
