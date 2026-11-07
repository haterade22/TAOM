"""Deterministic curve interpolation, simplification, ordering, and frames."""

import numpy as np


def _points(points, minimum=2):
    result = np.asarray(points, dtype=np.float64)
    if result.ndim != 2 or result.shape[1] != 3 or len(result) < minimum:
        minimum_text = "two" if minimum == 2 else str(minimum)
        raise ValueError(f"points must be an Nx3 array with at least {minimum_text} rows")
    if not np.isfinite(result).all():
        raise ValueError("points must all be finite")
    return result


def catmull_rom(points, samples_per_segment=4, centripetal=False):
    points = _points(points)
    if int(samples_per_segment) != samples_per_segment or samples_per_segment < 1:
        raise ValueError("samples_per_segment must be a positive integer")
    samples = int(samples_per_segment)
    padded = np.concatenate((points[:1], points, points[-1:]), axis=0)
    alpha = 0.5 if centripetal else 0.0
    output = []
    for index in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[index - 1:index + 3]
        if np.array_equal(p1, p2):
            continue
        t0 = 0.0
        t1 = t0 + (np.linalg.norm(p1 - p0) ** alpha if alpha else 1.0)
        t2 = t1 + (np.linalg.norm(p2 - p1) ** alpha if alpha else 1.0)
        t3 = t2 + (np.linalg.norm(p3 - p2) ** alpha if alpha else 1.0)
        for t in np.linspace(t1, t2, samples + 1)[:-1]:
            a1 = p1 if t1 == t0 else (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1
            a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
            a3 = p2 if t3 == t2 else (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
            b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2
            b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
            output.append((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2)
    output.append(points[-1])
    return np.asarray(output)


def resample_arc_length(points, count):
    points = _points(points)
    if int(count) != count or count < 2:
        raise ValueError("count must be an integer of at least two")
    lengths = np.linalg.norm(np.diff(points, axis=0), axis=1)
    cumulative = np.concatenate(([0.0], np.cumsum(lengths)))
    if cumulative[-1] <= 0:
        raise ValueError("cannot resample a zero-length curve")
    targets = np.linspace(0.0, cumulative[-1], int(count))
    output = np.empty((int(count), 3))
    for column in range(3):
        output[:, column] = np.interp(targets, cumulative, points[:, column])
    return output


def simplify_rdp(points, tolerance):
    points = _points(points)
    if tolerance < 0 or not np.isfinite(tolerance):
        raise ValueError("tolerance must be finite and nonnegative")
    kept = {0, len(points) - 1}
    stack = [(0, len(points) - 1)]
    while stack:
        start, end = stack.pop()
        if end <= start + 1:
            continue
        direction = points[end] - points[start]
        length = np.linalg.norm(direction)
        if length == 0:
            distances = np.linalg.norm(points[start + 1:end] - points[start], axis=1)
        else:
            offsets = points[start + 1:end] - points[start]
            distances = np.linalg.norm(np.cross(offsets, direction), axis=1) / length
        if not len(distances):
            continue
        local = int(np.argmax(distances))
        if distances[local] > tolerance:
            split = start + local + 1
            kept.add(split)
            stack.extend(((start, split), (split, end)))
    return sorted(kept)


def simplify_vw(points, tolerance, normalize=False):
    points = _points(points)
    if tolerance < 0 or not np.isfinite(tolerance):
        raise ValueError("tolerance must be finite and nonnegative")
    threshold = tolerance * np.linalg.norm(np.diff(points, axis=0), axis=1).sum() if normalize else tolerance
    active = list(range(len(points)))
    while len(active) > 2:
        candidates = []
        for position in range(1, len(active) - 1):
            a, b, c = points[active[position - 1:position + 2]]
            area = np.linalg.norm(np.cross(b - a, c - a)) * 0.5
            candidates.append((area, position, active[position]))
        area, position, _ = min(candidates)
        if area >= threshold:
            break
        del active[position]
    return active


def parallel_transport(points):
    points = _points(points)
    segments = np.diff(points, axis=0)
    if np.any(np.linalg.norm(segments, axis=1) == 0):
        raise ValueError("parallel transport requires no zero-length segments")
    tangents = np.empty_like(points)
    tangents[0] = segments[0]
    tangents[-1] = segments[-1]
    if len(points) > 2:
        tangents[1:-1] = segments[:-1] + segments[1:]
    tangents /= np.linalg.norm(tangents, axis=1)[:, np.newaxis]
    axis = np.eye(3)[int(np.argmin(np.abs(tangents[0])))]
    normal = np.cross(tangents[0], axis)
    normal /= np.linalg.norm(normal)
    normals = np.empty_like(points)
    normals[0] = normal
    for index in range(1, len(points)):
        projected = normal - tangents[index] * np.dot(normal, tangents[index])
        if np.linalg.norm(projected) < 1e-12:
            axis = np.eye(3)[int(np.argmin(np.abs(tangents[index])))]
            projected = np.cross(tangents[index], axis)
        normal = projected / np.linalg.norm(projected)
        normals[index] = normal
    binormals = np.cross(tangents, normals)
    binormals /= np.linalg.norm(binormals, axis=1)[:, np.newaxis]
    return tangents, normals, binormals


def sort_by_guide(strands, guide):
    guide = _points(guide)
    if np.any(np.linalg.norm(np.diff(guide, axis=0), axis=1) == 0):
        raise ValueError("guide requires no zero-length segments")
    if not strands:
        return []
    roots = np.asarray([_points(strand)[0] for strand in strands])
    starts = guide[:-1]
    directions = np.diff(guide, axis=0)
    lengths_squared = np.sum(directions * directions, axis=1)
    best_distances = np.full(len(roots), np.inf)
    best_positions = np.zeros(len(roots))
    cumulative = 0.0
    for start, direction, length_squared in zip(starts, directions, lengths_squared):
        offsets = roots - start
        fractions = np.clip((offsets @ direction) / length_squared, 0.0, 1.0)
        projected = start + fractions[:, np.newaxis] * direction
        distances = np.linalg.norm(roots - projected, axis=1)
        better = distances < best_distances
        best_distances[better] = distances[better]
        best_positions[better] = cumulative + fractions[better] * np.sqrt(length_squared)
        cumulative += np.sqrt(length_squared)
    return sorted(range(len(strands)), key=lambda index: (best_positions[index], index))
