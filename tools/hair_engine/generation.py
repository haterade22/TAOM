"""Seeded surface, guide, UV-grid, and border generation."""

import numpy as np

from .curves import resample_arc_length
from .model import Strand


def _surface(vertices, faces):
    vertices = np.asarray(vertices, dtype=np.float64)
    faces = np.asarray(faces)
    if vertices.ndim != 2 or vertices.shape[1] != 3 or not np.isfinite(vertices).all():
        raise ValueError("vertices must be a finite Nx3 array")
    if faces.ndim != 2 or faces.shape[1] != 3 or not np.issubdtype(faces.dtype, np.integer):
        raise ValueError("faces must be an integer Mx3 triangle array")
    if not len(faces) or faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError("faces must reference at least one valid triangle")
    return vertices, faces.astype(np.int32, copy=False)


def _normals(vertices, faces):
    triangles = vertices[faces]
    normals = np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0])
    lengths = np.linalg.norm(normals, axis=1)
    if np.any(lengths == 0):
        raise ValueError("surface contains a degenerate triangle")
    return normals / lengths[:, np.newaxis]


def distribute_roots(vertices, faces, count, seed=0, face_mask=None):
    vertices, faces = _surface(vertices, faces)
    if int(count) != count or count < 1:
        raise ValueError("count must be a positive integer")
    if face_mask is None:
        face_mask = np.ones(len(faces), dtype=bool)
    face_mask = np.asarray(face_mask, dtype=bool)
    if face_mask.shape != (len(faces),) or not np.any(face_mask):
        raise ValueError("face_mask must select at least one triangle")
    triangle_ids = np.flatnonzero(face_mask)
    triangles = vertices[faces[triangle_ids]]
    double_areas = np.linalg.norm(np.cross(triangles[:, 1] - triangles[:, 0],
                                           triangles[:, 2] - triangles[:, 0]), axis=1)
    if np.any(double_areas == 0):
        raise ValueError("selected surface contains a degenerate triangle")
    rng = np.random.default_rng(seed)
    chosen = rng.choice(triangle_ids, size=int(count), p=double_areas / double_areas.sum())
    barycentrics = rng.random((int(count), 3))
    barycentrics /= barycentrics.sum(axis=1)[:, np.newaxis]
    roots = np.sum(vertices[faces[chosen]] * barycentrics[:, :, np.newaxis], axis=1)
    return roots, _normals(vertices, faces)[chosen], chosen, barycentrics


def _downward(normal):
    nx, ny, nz = normal
    tangent = np.array([nx * nz, ny * nz, nz * nz - 1.0])
    if np.linalg.norm(tangent) < 1e-8:
        tangent = np.array([nx, ny, -0.25])
    length = np.linalg.norm(tangent)
    return tangent / length if length >= 1e-8 else np.array([0.0, 0.0, -1.0])


def _guide(root, normal, length, resolution):
    if length <= 0 or not np.isfinite(length):
        raise ValueError("length must be finite and positive")
    if int(resolution) != resolution or resolution < 2:
        raise ValueError("resolution must be an integer of at least two")
    direction = _downward(normal)
    points = []
    for t in np.linspace(0.0, 1.0, int(resolution)):
        lift = 0.003 + 0.02 * np.sin(np.pi * t)
        points.append(root + direction * (length * t) + normal * lift)
    return Strand(np.asarray(points))


def surface_guides(vertices, faces, count, length=0.1, resolution=6, seed=0, face_mask=None):
    roots, normals, _, _ = distribute_roots(vertices, faces, count, seed, face_mask)
    return [_guide(root, normal, length, resolution) for root, normal in zip(roots, normals)]


def interpolate_guides(guides, children_per_span, resolution=6, seed=0):
    if not guides:
        raise ValueError("at least one guide is required")
    if int(children_per_span) != children_per_span or children_per_span < 1:
        raise ValueError("children_per_span must be a positive integer")
    rng = np.random.default_rng(seed)
    parents = [guide.resampled(resolution) for guide in guides]
    output = []
    spans = len(parents) if len(parents) > 2 else len(parents) - 1
    for index in range(spans):
        first = parents[index]
        second = parents[(index + 1) % len(parents)]
        for child in range(1, int(children_per_span) + 1):
            fraction = child / (int(children_per_span) + 1)
            positions = first.positions * (1.0 - fraction) + second.positions * fraction
            if len(parents) > 2:
                positions += rng.uniform(-1e-6, 1e-6, positions.shape)
            result = Strand(positions, (1.0 - fraction) * first.radius + fraction * second.radius,
                            (1.0 - fraction) * first.tilt + fraction * second.tilt)
            output.append(result)
    return output


def uv_grid_guides(vertices, faces, uvs, cells=2, count=16, length=0.1, resolution=6, seed=0):
    vertices, faces = _surface(vertices, faces)
    uvs = np.asarray(uvs, dtype=np.float64)
    if uvs.shape != (len(vertices), 2) or not np.isfinite(uvs).all():
        raise ValueError("uvs must have one finite UV coordinate per vertex")
    if int(cells) != cells or cells < 1:
        raise ValueError("cells must be a positive integer")
    roots, normals, chosen, barycentrics = distribute_roots(vertices, faces, count, seed)
    root_uvs = np.sum(uvs[faces[chosen]] * barycentrics[:, :, np.newaxis], axis=1)
    output = []
    for root, normal, uv in zip(roots, normals, root_uvs):
        wrapped = np.mod(uv, 1.0)
        region = min(int(wrapped[0] * cells), int(cells) - 1)
        region += min(int(wrapped[1] * cells), int(cells) - 1) * int(cells)
        strand = _guide(root, normal, length, resolution)
        strand.uv_region = region
        output.append(strand)
    return output


def border_guides(vertices, faces, length=0.1, resolution=6):
    vertices, faces = _surface(vertices, faces)
    edges = np.concatenate((faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]), axis=0)
    face_ids = np.repeat(np.arange(len(faces)), 3)
    order = np.argsort(edges, axis=1)
    sorted_edges = np.take_along_axis(edges, order, axis=1)
    unique, inverse, counts = np.unique(sorted_edges, axis=0, return_inverse=True, return_counts=True)
    normals = _normals(vertices, faces)
    output = []
    for edge_id in np.flatnonzero(counts == 1):
        edge = unique[edge_id]
        face_id = face_ids[int(np.flatnonzero(inverse == edge_id)[0])]
        normal = normals[face_id]
        root = vertices[list(edge)].mean(axis=0)
        output.append(_guide(root, normal, length, resolution))
    return output
