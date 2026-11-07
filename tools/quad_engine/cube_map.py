"""Deterministic cube-map quad remeshing for closed genus-zero triangle meshes.

This is an independent baseline, not an Exoside-compatible algorithm. It favors simple,
watertight topology and measurable output over feature-aligned edge flow. NumPy is required;
SciPy supplies an optional spatial index. The public API accepts NumPy arrays without bpy.
"""

from dataclasses import dataclass

import numpy as np

try:
    from scipy.spatial import cKDTree
except ImportError:
    cKDTree = None


@dataclass
class MeshResult:
    vertices: np.ndarray
    faces: np.ndarray
    material_ids: np.ndarray
    source_triangles: np.ndarray
    metrics: dict


def _edge_counts(faces):
    pairs = [(0, 1), (1, 2)]
    pairs.extend([(2, 0)] if faces.shape[1] == 3 else [(2, 3), (3, 0)])
    edges = np.concatenate([faces[:, list(pair)] for pair in pairs], axis=0)
    edges = np.sort(edges, axis=1)
    _, counts = np.unique(edges, axis=0, return_counts=True)
    return counts


def _validate(vertices, faces, material_ids, target_quads):
    vertices = np.asarray(vertices, dtype=np.float64)
    faces = np.asarray(faces)
    if vertices.ndim != 2 or vertices.shape[1] != 3 or len(vertices) == 0:
        raise ValueError("vertices must be a non-empty Nx3 array")
    if not np.isfinite(vertices).all():
        raise ValueError("vertices must all be finite")
    if faces.ndim != 2 or faces.shape[1] != 3 or len(faces) == 0:
        raise ValueError("faces must contain triangles in an Mx3 array")
    if not np.issubdtype(faces.dtype, np.integer):
        raise ValueError("triangle indices must be integers")
    faces = faces.astype(np.int32, copy=False)
    if faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError("triangle index is outside the vertex array")
    if material_ids is None:
        material_ids = np.zeros(len(faces), dtype=np.int32)
    material_ids = np.asarray(material_ids)
    if material_ids.shape != (len(faces),):
        raise ValueError("material_ids must have one value per source triangle")
    material_ids = material_ids.astype(np.int32, copy=False)
    if int(target_quads) != target_quads:
        raise ValueError("target_quads must be an integer")
    if target_quads < 6:
        raise ValueError("target_quads must be at least 6")
    if not np.all(_edge_counts(faces) == 2):
        raise ValueError("input must be a closed manifold mesh")
    return vertices, faces, material_ids, int(target_quads)


def _query_centers(tree, centers, points, count):
    if tree is not None:
        return tree.query(points, k=count)[1]
    nearest = np.empty((len(points), count), dtype=np.int32)
    for index, point in enumerate(points):
        distances = np.sum((centers - point) ** 2, axis=1)
        nearest[index] = np.argpartition(distances, count - 1)[:count]
    return nearest


def _project_triangle_candidates(points, triangles):
    a = triangles[:, 0]
    b = triangles[:, 1]
    c = triangles[:, 2]
    p = points[:, np.newaxis, :]
    ab = b - a
    ac = c - a
    ap = np.broadcast_to(p, (len(points), len(triangles), 3)) - a
    d00 = np.sum(ab * ab, axis=1)
    d01 = np.sum(ab * ac, axis=1)
    d02 = np.sum(ab * ap, axis=2)
    d11 = np.sum(ac * ac, axis=1)
    d12 = np.sum(ac * ap, axis=2)
    denominator = d00 * d11 - d01 * d01
    v = np.divide(d11 * d02 - d01 * d12, denominator,
                  out=np.zeros_like(d02), where=denominator != 0)
    w = np.divide(d00 * d12 - d01 * d02, denominator,
                  out=np.zeros_like(d02), where=denominator != 0)
    closest = a + ab * v[:, :, np.newaxis] + ac * w[:, :, np.newaxis]
    best_distances = np.full(v.shape, np.inf)
    inside = (denominator != 0) & (v >= 0) & (w >= 0) & (v + w <= 1)
    best_distances[inside] = np.sum((closest - p) ** 2, axis=2)[inside]
    for edge_start, edge_end in ((a, b), (a, c), (b, c)):
        edge = edge_end - edge_start
        length_squared = np.sum(edge * edge, axis=1)
        t = np.divide(np.sum((p - edge_start) * edge, axis=2), length_squared,
                      out=np.zeros((len(points), len(triangles))), where=length_squared != 0)
        t = np.clip(t, 0, 1)
        projection = edge_start + edge * t[:, :, np.newaxis]
        distances = np.sum((projection - p) ** 2, axis=2)
        mask = distances < best_distances
        closest[mask] = projection[mask]
        best_distances[mask] = distances[mask]
    return closest, best_distances


def _nearest_points_on_triangles(points, triangles, tree):
    nearest = np.empty_like(points)
    nearest_triangles = np.empty(len(points), dtype=np.int32)
    candidate_count = min(32, len(triangles))
    triangle_min = triangles.min(axis=1)
    triangle_max = triangles.max(axis=1)
    for start, point in enumerate(points):
        query = point[np.newaxis, :]
        candidates = _query_centers(tree, triangles.mean(axis=1), query, candidate_count)
        candidates = np.asarray(candidates, dtype=np.int32).reshape(-1)
        _, distances = _project_triangle_candidates(query, triangles[candidates])
        best_distance = float(distances.min())
        lower = np.maximum(np.maximum(triangle_min - point, point - triangle_max), 0)
        possible = np.sum(lower * lower, axis=1) <= best_distance + 1e-24
        if not np.any(possible):
            possible[:] = True
        possible_indices = np.flatnonzero(possible)
        closest, distances = _project_triangle_candidates(query, triangles[possible])
        best = int(np.argmin(distances))
        nearest[start] = closest[0, best]
        nearest_triangles[start] = possible_indices[best]
    return nearest, nearest_triangles


def _raycast(directions, origin, triangles):
    points = np.empty_like(directions)
    source_indices = np.empty(len(directions), dtype=np.int32)
    for start, direction in enumerate(directions):
        a = triangles[:, 0]
        edge1 = triangles[:, 1] - a
        edge2 = triangles[:, 2] - a
        pvec = np.cross(direction, edge2)
        determinant = np.sum(edge1 * pvec, axis=1)
        valid = np.abs(determinant) > 1e-14
        inverse = np.divide(1.0, determinant, out=np.zeros_like(determinant), where=valid)
        tvec = origin - a
        u = np.sum(tvec * pvec, axis=1) * inverse
        qvec = np.cross(tvec, edge1)
        v = np.sum(direction * qvec, axis=1) * inverse
        distance = np.sum(edge2 * qvec, axis=1) * inverse
        valid &= (u >= -1e-12) & (v >= -1e-12) & (u + v <= 1 + 1e-12) & (distance > 1e-12)
        if not np.any(valid):
            raise ValueError(
                "a cube-map ray did not intersect the source; input must be star-shaped")
        hit_distances = distance[valid]
        if len(hit_distances) > 1 and np.ptp(hit_distances) > 1e-10:
            raise ValueError(
                "a cube-map ray intersected multiple surfaces; input must be star-shaped")
        source = np.argmin(np.where(valid, distance, np.inf))
        points[start] = origin + direction * distance[source]
        source_indices[start] = source
    return points, source_indices


def _cube_topology(resolution):
    bases = [
        ((1, 0, 0), (0, 1, 0), (0, 0, 1)),
        ((-1, 0, 0), (0, -1, 0), (0, 0, 1)),
        ((0, 1, 0), (-1, 0, 0), (0, 0, 1)),
        ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
        ((0, 0, 1), (1, 0, 0), (0, 1, 0)),
        ((0, 0, -1), (-1, 0, 0), (0, 1, 0)),
    ]
    canonical = []
    lookup = {}
    faces = []
    for normal, u_axis, v_axis in bases:
        normal = np.asarray(normal, dtype=np.float64)
        u_axis = np.asarray(u_axis, dtype=np.float64)
        v_axis = np.asarray(v_axis, dtype=np.float64)
        grid = {}
        for j in range(resolution + 1):
            v = -1 + 2 * j / resolution
            for i in range(resolution + 1):
                u = -1 + 2 * i / resolution
                point = normal + u_axis * u + v_axis * v
                key = tuple(np.round(point, 12))
                if key not in lookup:
                    lookup[key] = len(canonical)
                    canonical.append(point)
                grid[i, j] = lookup[key]
        for j in range(resolution):
            for i in range(resolution):
                faces.append([
                    grid[i, j], grid[i + 1, j],
                    grid[i + 1, j + 1], grid[i, j + 1],
                ])
    return np.asarray(canonical, dtype=np.float64), np.asarray(faces, dtype=np.int32)


def _signed_volume(vertices, faces):
    triangles = np.concatenate((faces[:, [0, 1, 2]], faces[:, [0, 2, 3]]), axis=0)
    a = vertices[triangles[:, 0]]
    b = vertices[triangles[:, 1]]
    c = vertices[triangles[:, 2]]
    return float(np.sum(np.einsum("ij,ij->i", a, np.cross(b, c))) / 6)


def _metrics(vertices, faces, source_triangles, center_errors):
    lengths = np.linalg.norm(vertices[np.roll(faces, -1, axis=1)] - vertices[faces], axis=2)
    positive = lengths[lengths > 0]
    aspect = float(np.max(lengths.max(axis=1) / lengths.min(axis=1))) if len(positive) else np.inf
    a = vertices[faces[:, 0]]
    b = vertices[faces[:, 1]]
    c = vertices[faces[:, 2]]
    d = vertices[faces[:, 3]]
    areas = np.linalg.norm(np.cross(b - a, c - a), axis=1) / 2
    areas += np.linalg.norm(np.cross(c - a, d - a), axis=1) / 2
    return {
        "quad_count": len(faces),
        "boundary_edges": int(np.count_nonzero(_edge_counts(faces) != 2)),
        "signed_volume": _signed_volume(vertices, faces),
        "max_aspect_ratio": aspect,
        "degenerate_quads": int(np.count_nonzero(areas <= 1e-16)),
        "max_center_error": float(np.max(center_errors)),
        "mean_center_error": float(np.mean(center_errors)),
        "source_triangle_count": len(np.unique(source_triangles)),
    }


def remesh(vertices, faces, material_ids=None, target_quads=96):
    vertices, faces, material_ids, target_quads = _validate(
        vertices, faces, material_ids, target_quads)
    resolution = int(np.ceil(np.sqrt(target_quads / 6)))
    canonical, output_faces = _cube_topology(resolution)
    center = (vertices.min(axis=0) + vertices.max(axis=0)) / 2
    lengths = np.linalg.norm(canonical, axis=1)
    directions = canonical / lengths[:, np.newaxis]
    source_triangles = vertices[faces]
    centers = source_triangles.mean(axis=1)
    tree = cKDTree(centers) if cKDTree is not None else None
    output_vertices, _ = _raycast(directions, center, source_triangles)
    output_centers = output_vertices[output_faces].mean(axis=1)
    projected_centers, face_sources = _nearest_points_on_triangles(
        output_centers, source_triangles, tree)
    center_errors = np.linalg.norm(output_centers - projected_centers, axis=1)
    output_materials = material_ids[face_sources].astype(np.int32, copy=False)
    if _signed_volume(output_vertices, output_faces) < 0:
        output_faces = output_faces[:, ::-1].copy()
    metrics = _metrics(output_vertices, output_faces, face_sources, center_errors)
    return MeshResult(output_vertices, output_faces, output_materials, face_sources, metrics)
