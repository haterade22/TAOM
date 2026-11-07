"""Exact nearest-triangle projection and barycentric sampling."""

import numpy as np


def _inputs(points, vertices, faces):
    points = np.asarray(points, dtype=np.float64)
    vertices = np.asarray(vertices, dtype=np.float64)
    faces = np.asarray(faces)
    if points.ndim != 2 or points.shape[1] != 3 or not np.isfinite(points).all():
        raise ValueError("points must be a finite Nx3 array")
    if vertices.ndim != 2 or vertices.shape[1] != 3 or not np.isfinite(vertices).all():
        raise ValueError("vertices must be a finite Nx3 array")
    if faces.ndim != 2 or faces.shape[1] != 3 or not len(faces):
        raise ValueError("faces must contain at least one triangle")
    if not np.issubdtype(faces.dtype, np.integer) or faces.min() < 0 or faces.max() >= len(vertices):
        raise ValueError("triangle indices are invalid")
    return points, vertices, faces.astype(np.int32, copy=False)


def _barycentric(point, a, b, c, clamp=True):
    ab, ac, ap = b - a, c - a, point - a
    d00, d01, d11 = np.dot(ab, ab), np.dot(ab, ac), np.dot(ac, ac)
    d20, d21 = np.dot(ap, ab), np.dot(ap, ac)
    denominator = d00 * d11 - d01 * d01
    if denominator <= 1e-24:
        raise ValueError("cannot sample a degenerate triangle")
    v = (d11 * d20 - d01 * d21) / denominator
    w = (d00 * d21 - d01 * d20) / denominator
    weights = np.array([1.0 - v - w, v, w])
    if clamp:
        weights = np.clip(weights, 0.0, 1.0)
        weights /= weights.sum()
    return weights


def nearest_triangles(points, vertices, faces):
    points, vertices, faces = _inputs(points, vertices, faces)
    nearest = np.empty_like(points)
    triangle_ids = np.empty(len(points), dtype=np.int32)
    barycentrics = np.empty((len(points), 3))
    for point_index, point in enumerate(points):
        best_distance = np.inf
        for face_index, face in enumerate(faces):
            a, b, c = vertices[face]
            ab, ac = b - a, c - a
            denominator = np.dot(ab, ab) * np.dot(ac, ac) - np.dot(ab, ac) ** 2
            if denominator <= 1e-24:
                continue
            weights = _barycentric(point, a, b, c, clamp=False)
            candidates = []
            if np.all(weights >= 0):
                candidates.append(a * weights[0] + b * weights[1] + c * weights[2])
            for start, end in ((a, b), (b, c), (c, a)):
                direction = end - start
                length_squared = np.dot(direction, direction)
                if length_squared == 0:
                    continue
                t = np.clip(np.dot(point - start, direction) / length_squared, 0.0, 1.0)
                candidates.append(start + direction * t)
            for candidate in candidates:
                distance = np.sum((candidate - point) ** 2)
                if distance < best_distance:
                    best_distance = distance
                    nearest[point_index] = candidate
                    triangle_ids[point_index] = face_index
                    barycentrics[point_index] = _barycentric(candidate, a, b, c)
        if not np.isfinite(best_distance):
            raise ValueError("surface has no nondegenerate triangles")
    return nearest, triangle_ids, barycentrics
