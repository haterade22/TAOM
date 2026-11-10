from __future__ import annotations

from collections import Counter
import math

from .geometry import (
    add,
    average,
    cross,
    lerp,
    multiply,
    nearest_on_segment,
    normalize,
    signed_area,
    smooth,
    subdivide_quads,
    subtract,
    tessellate,
    vertex_normals,
    weld,
)
from .model import Mesh, RetopoSettings


def _components(mesh: Mesh):
    parents = list(range(len(mesh.faces)))

    def root(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    def union(first, second):
        first_root = root(first)
        second_root = root(second)
        if first_root != second_root:
            parents[second_root] = first_root

    return parents, root, union


def _face_uv_maps(mesh: Mesh):
    maps = []
    cursor = 0
    for face in mesh.faces:
        maps.append({vertex: mesh.uv[cursor + index] for index, vertex in enumerate(face)})
        cursor += len(face)
    return maps


def _pattern_faces(mesh: Mesh):
    parents, root, union = _components(mesh)
    uv_maps = _face_uv_maps(mesh)
    edge_faces = {}
    for face_index, face in enumerate(mesh.faces):
        for index, vertex in enumerate(face):
            following = face[(index + 1) % len(face)]
            coordinates = sorted((
                tuple(round(value, 8) for value in uv_maps[face_index][vertex]),
                tuple(round(value, 8) for value in uv_maps[face_index][following]),
            ))
            edge_faces.setdefault(tuple(coordinates), []).append(face_index)
    for adjacent in edge_faces.values():
        for face_index in adjacent[1:]:
            union(adjacent[0], face_index)
    roots = []
    for face_index in range(len(mesh.faces)):
        face_root = root(face_index)
        if face_root not in roots:
            roots.append(face_root)
    return {index: roots.index(root(index)) for index in range(len(mesh.faces))}


def _part_faces(mesh: Mesh):
    parents, root, union = _components(mesh)
    edge_faces = {}
    for face_index, face in enumerate(mesh.faces):
        for index, vertex in enumerate(face):
            edge = Mesh.edge_key(vertex, face[(index + 1) % len(face)])
            edge_faces.setdefault(edge, []).append(face_index)
    for adjacent in edge_faces.values():
        for face_index in adjacent[1:]:
            union(adjacent[0], face_index)
    roots = []
    for face_index in range(len(mesh.faces)):
        face_root = root(face_index)
        if face_root not in roots:
            roots.append(face_root)
    return {index: roots.index(root(index)) for index in range(len(mesh.faces))}


def flatten_to_patterns(
    source: Mesh,
    uv_name: str | None = None,
    simplify: bool = False,
    merge_distance: float = 0.001,
) -> Mesh:
    uv_name = uv_name or source.uv_name
    expected_loops = sum(len(face) for face in source.faces)
    if uv_name != source.uv_name or len(source.uv) != expected_loops:
        raise ValueError(f"source does not contain a complete UV map named {uv_name}")
    if not math.isfinite(merge_distance) or merge_distance < 0:
        raise ValueError("merge_distance must be finite and non-negative")
    part_faces = _part_faces(source)
    lookup = {}
    vertices = []
    source_positions = []
    source_vertices = []
    source_parts = []
    uv = []
    faces = []
    cursor = 0
    for face_index, face in enumerate(source.faces):
        flat_face = []
        for vertex in face:
            coordinate = source.uv[cursor]
            cursor += 1
            key = tuple(round(value, 12) for value in coordinate)
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append((coordinate[0], coordinate[1], 0.0))
                source_positions.append(source.vertices[vertex])
                source_vertices.append(vertex)
                source_parts.append(part_faces[face_index])
            flat_face.append(lookup[key])
            uv.append(coordinate)
        faces.append(tuple(flat_face))
    flat = Mesh(
        vertices=vertices,
        faces=faces,
        uv=uv,
        uv_name=uv_name,
        attributes={
            "source_position": source_positions,
            "source_vertex": source_vertices,
            "source_part": source_parts,
        },
    )
    if simplify and merge_distance > 0:
        simplified = weld(flat, merge_distance)
        for name, values in flat.attributes.items():
            simplified.attributes[name] = [
                min(range(len(flat.vertices)), key=lambda old: math.dist(vertex, flat.vertices[old]))
                for vertex in simplified.vertices
            ]
            simplified.attributes[name] = [values[index] for index in simplified.attributes[name]]
        simplified.uv = [simplified.vertices[vertex][:2] for face in simplified.faces for vertex in face]
        flat = simplified
    pattern_faces = _pattern_faces(flat)
    pattern_by_vertex = {}
    for face_index, pattern in pattern_faces.items():
        for vertex in flat.faces[face_index]:
            pattern_by_vertex[vertex] = pattern
    flat.attributes["pattern_id"] = [pattern_by_vertex.get(index, 0) for index in range(len(flat.vertices))]
    centers = {}
    for face_index, pattern in pattern_faces.items():
        centers.setdefault(pattern, []).extend(flat.vertices[index] for index in flat.faces[face_index])
    centers = {pattern: average(points) for pattern, points in centers.items()}
    flat.attributes["pattern_center"] = [centers[value] for value in flat.attributes["pattern_id"]]
    boundary_vertices = {index for edge in flat.boundary_edges() for index in edge}
    valence = Counter(vertex for face in flat.faces for vertex in face)
    flat.attributes["open_seam"] = [index in boundary_vertices for index in range(len(flat.vertices))]
    if "point_guide" in source.attributes:
        flat.attributes["point_guide"] = [
            bool(source.attributes["point_guide"][vertex]) for vertex in source_vertices
        ]
    else:
        flat.attributes["point_guide"] = [
            (index in boundary_vertices and valence[index] != 2)
            or (index not in boundary_vertices and valence[index] != 4)
            for index in range(len(flat.vertices))
        ]
    flat.attributes["simplify_on"] = [bool(simplify)] * len(flat.vertices)
    boundary = set(flat.boundary_edges())
    flat.edge_attributes["part_border"] = {
        edge: edge in boundary or flat.attributes["source_part"][edge[0]] != flat.attributes["source_part"][edge[1]]
        for edge in flat.edges()
    }
    return flat


def non_manifold_edges(mesh: Mesh):
    counts = {edge: 0 for edge in mesh.edges()}
    for face in mesh.faces:
        for index, vertex in enumerate(face):
            counts[Mesh.edge_key(vertex, face[(index + 1) % len(face)])] += 1
    return sorted(edge for edge, count in counts.items() if count > 2)


def inspect_topology(mesh: Mesh, fix_split_edges: bool = False):
    bad_edges = non_manifold_edges(mesh)
    result = mesh.copy()
    if fix_split_edges:
        adjacency = {edge: [] for edge in bad_edges}
        for face_index, face in enumerate(mesh.faces):
            for index, vertex in enumerate(face):
                edge = Mesh.edge_key(vertex, face[(index + 1) % len(face)])
                if edge in adjacency:
                    adjacency[edge].append(face_index)
        for edge, face_indices in adjacency.items():
            for face_index in face_indices[1:]:
                replacements = {}
                for old in edge:
                    replacements[old] = len(result.vertices)
                    result.vertices.append(mesh.vertices[old])
                    for name, values in result.attributes.items():
                        values.append(values[old])
                result.faces[face_index] = tuple(replacements.get(index, index) for index in result.faces[face_index])
    result.edge_attributes["bad_mesh"] = {edge: True for edge in bad_edges}
    return result, bad_edges


def validate_guides(guide: Mesh):
    invalid = [(index, len(face)) for index, face in enumerate(guide.faces) if len(face) != 4]
    if invalid:
        details = ", ".join(f"face {index} has {size} vertices" for index, size in invalid)
        raise ValueError(f"guide meshes require quads: {details}")


def _boundary_loops(mesh: Mesh):
    adjacency = {}
    for first, second in mesh.boundary_edges():
        adjacency.setdefault(first, []).append(second)
        adjacency.setdefault(second, []).append(first)
    remaining = set(adjacency)
    loops = []
    while remaining:
        start = min(remaining)
        loop = [start]
        previous = None
        current = start
        while True:
            remaining.discard(current)
            candidates = [value for value in adjacency[current] if value != previous]
            if not candidates:
                break
            following = candidates[0]
            if following == start:
                break
            if following in loop:
                break
            loop.append(following)
            previous, current = current, following
        loops.append(loop)
    return loops


def _corner_candidates(mesh: Mesh, loop, settings: RetopoSettings):
    explicit = mesh.attributes.get("point_guide", [])
    marked = [index for index in loop if index < len(explicit) and explicit[index]]
    if marked and len(marked) <= 4:
        return marked
    selected = []
    threshold = math.pi * settings.angle_threshold * (0.5 + settings.median_corners_threshold)
    for position, index in enumerate(loop):
        previous = mesh.vertices[loop[position - 1]]
        current = mesh.vertices[index]
        following = mesh.vertices[loop[(position + 1) % len(loop)]]
        first = normalize(subtract(previous, current))
        second = normalize(subtract(following, current))
        angle = math.acos(max(-1.0, min(1.0, sum(a * b for a, b in zip(first, second)))))
        if angle <= threshold + 1e-9:
            selected.append(index)
    heights = [mesh.vertices[index][1] for index in loop]
    minimum = min(heights)
    height = max(heights) - minimum
    pool = [
        index for index in loop
        if min(
            abs(mesh.vertices[index][1] - minimum),
            abs(mesh.vertices[index][1] - minimum - height),
        ) <= height * settings.corners_threshold + 1e-9
    ]
    filtered = [index for index in selected if index in pool]
    if len(filtered) >= 4:
        return filtered
    ordered = sorted(pool or loop, key=lambda index: (mesh.vertices[index][1], mesh.vertices[index][0]))
    candidates = ordered[:2] + ordered[-2:]
    result = []
    for index in candidates:
        if index not in result:
            result.append(index)
    while len(result) < min(4, len(loop)):
        remaining = [index for index in loop if index not in result]
        farthest = max(
            remaining,
            key=lambda index: min(math.dist(mesh.vertices[index], mesh.vertices[other]) for other in result),
        )
        result.append(farthest)
    return result


def initialize_planes(flat: Mesh, settings: RetopoSettings) -> Mesh:
    settings.validate()
    loops = _boundary_loops(flat)
    if not loops:
        raise ValueError("plane initialization requires an open pattern border")
    if len(loops) > 1 and all(len(face) == 4 for face in flat.faces):
        result = flat.copy()
        result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
        return result
    vertices = []
    faces = []
    for loop in loops:
        corners = _corner_candidates(flat, loop, settings)
        if len(corners) == 4:
            offset = len(vertices)
            vertices.extend(flat.vertices[index] for index in corners)
            face = tuple(range(offset, offset + 4))
            if signed_area(face, vertices) < 0:
                face = tuple(reversed(face))
            faces.append(face)
            continue
        ordered = loop
        center = average(flat.vertices[index] for index in ordered)
        center_index = len(vertices)
        vertices.append(center)
        mids = {}
        for position, index in enumerate(ordered):
            following = ordered[(position + 1) % len(ordered)]
            mids[index] = len(vertices)
            vertices.append(lerp(flat.vertices[index], flat.vertices[following], 0.5))
        point_indices = {}
        for index in ordered:
            point_indices[index] = len(vertices)
            vertices.append(flat.vertices[index])
        for position, index in enumerate(ordered):
            following = ordered[(position + 1) % len(ordered)]
            face = (point_indices[index], mids[index], center_index, mids[ordered[position - 1]])
            if signed_area(face, vertices) < 0:
                face = tuple(reversed(face))
            faces.append(face)
    result = Mesh(vertices=vertices, faces=faces, uv_name=flat.uv_name)
    result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
    return result


def _mirror_pattern_centers(source: Mesh):
    flat = source if "pattern_center" in source.attributes else flatten_to_patterns(source)
    centers = {}
    for index, pattern in enumerate(flat.attributes["pattern_id"]):
        centers[pattern] = flat.attributes["pattern_center"][index]
    return list(centers.values())


def _mirror_axes(guide: Mesh):
    center = average(
        guide.vertices[index]
        for face in guide.faces
        for index in face
    )
    axes = set()
    for edge in guide.lines:
        target = guide.vertices[edge[1]]
        delta = subtract(target, center)
        axes.add("x" if abs(delta[0]) >= abs(delta[1]) else "y")
    return axes


def mirror_guides(guide: Mesh, axes=("x",), source: Mesh | None = None) -> Mesh:
    axes = tuple(dict.fromkeys(axes))
    if any(axis not in {"x", "y"} for axis in axes):
        raise ValueError("mirror axes must be x or y")
    centers = [0.0, 0.0]
    targets = {}
    if source is not None:
        pattern_centers = _mirror_pattern_centers(source)
        guide_center = average(guide.vertices[index] for face in guide.faces for index in face)
        for axis in axes:
            matching = [edge for edge in guide.lines if _mirror_axes(guide) == {axis}]
            if matching:
                target = guide.vertices[matching[0][1]]
            else:
                target = guide_center
            targets[axis] = min(pattern_centers, key=lambda center: math.dist(target, center))
        if guide.lines and _mirror_axes(guide) != set(axes):
            return guide.copy()
        if len(_mirror_axes(guide)) > 1:
            return guide.copy()
    result = guide.copy()
    for axis in axes:
        component = result.copy()
        if source is None:
            center = (0.0, 0.0)
        else:
            target = targets[axis]
            center = (
                (guide_center[0] + target[0]) / 2,
                (guide_center[1] + target[1]) / 2,
            )
        component.vertices = [
            (2 * center[0] - vertex[0] if axis == "x" else vertex[0],
             2 * center[1] - vertex[1] if axis == "y" else vertex[1], vertex[2])
            for vertex in component.vertices
        ]
        component.faces = [tuple(reversed(face)) for face in component.faces]
        offset = len(result.vertices)
        result.vertices.extend(component.vertices)
        result.faces.extend(tuple(index + offset for index in face) for face in component.faces)
        result.lines.extend(tuple(index + offset for index in edge) for edge in component.lines)
    result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
    return result


def auto_fill(guide: Mesh, point_guides, max_iterations: int = 5, across_patterns: bool = True) -> Mesh:
    if max_iterations < 0:
        raise ValueError("max_iterations must be non-negative")
    validate_guides(guide)
    points = [tuple(point) for point in point_guides]

    def fill(current, candidate_points):
        if max_iterations == 0:
            return current
        nearby_points = [
            point for point in candidate_points
            if all(
                min(vertex[axis] for vertex in current.vertices) - 0.5
                <= point[axis]
                <= max(vertex[axis] for vertex in current.vertices) + 0.5
                for axis in range(3)
            )
        ]
        if len(current.faces) == 1 and len(nearby_points) in {1, 4}:
            current = subdivide_quads(current)
            for point in nearby_points:
                nearest = min(range(len(current.vertices)), key=lambda index: math.dist(point, current.vertices[index]))
                current.vertices[nearest] = point
            return current
        if len(current.faces) == 1 and len(nearby_points) >= 6:
            face = current.faces[0]
            center = average(current.vertices[index] for index in face)
            first = lerp(current.vertices[face[0]], current.vertices[face[1]], 0.5)
            second = lerp(current.vertices[face[2]], current.vertices[face[3]], 0.5)
            if (
                min(current.vertices[face[0]][0], current.vertices[face[1]][0]) <= center[0] <= max(current.vertices[face[0]][0], current.vertices[face[1]][0])
                and min(current.vertices[face[2]][0], current.vertices[face[3]][0]) <= center[0] <= max(current.vertices[face[2]][0], current.vertices[face[3]][0])
            ):
                current.vertices.extend([first, second])
                current.faces = [
                    (face[0], len(current.vertices) - 2, len(current.vertices) - 1, face[3]),
                    (len(current.vertices) - 2, face[1], face[2], len(current.vertices) - 1),
                ]
                return current
        for _ in range(max_iterations):
            missing = [
                point for point in candidate_points
                if not any(math.dist(point, vertex) < 1e-7 for vertex in current.vertices)
            ]
            if not missing:
                break
            best = None
            for point in missing:
                for face_index, face in enumerate(current.faces):
                    for edge_index, first in enumerate(face):
                        second = face[(edge_index + 1) % 4]
                        start = current.vertices[first]
                        end = current.vertices[second]
                        direction = subtract(end, start)
                        length_squared = sum(value * value for value in direction)
                        if length_squared <= 1e-15:
                            continue
                        factor = max(0.0, min(1.0, sum(
                            (point[axis] - start[axis]) * direction[axis] for axis in range(3)
                        ) / length_squared))
                        projected = add(start, multiply(direction, factor))
                        distance = math.dist(point, projected)
                        length = math.sqrt(length_squared)
                        if not 0.05 < factor < 0.95 or distance > length * 0.5 + 1e-7:
                            continue
                        candidate = (distance, point, face_index, edge_index, projected, length)
                        if best is None or distance < best[0]:
                            best = candidate
            if best is None:
                break
            distance, point, face_index, edge_index, projected, length = best
            if distance >= length * 0.49:
                current = subdivide_quads(current)
                nearest = min(range(len(current.vertices)), key=lambda index: math.dist(point, current.vertices[index]))
                current.vertices[nearest] = point
                continue
            face = current.faces[face_index]
            first = face[edge_index]
            second = face[(edge_index + 1) % 4]
            third = face[(edge_index + 2) % 4]
            fourth = face[(edge_index + 3) % 4]
            opposite = lerp(current.vertices[third], current.vertices[fourth], 0.5)

            def vertex_at(coordinate):
                for index, vertex in enumerate(current.vertices):
                    if math.dist(vertex, coordinate) < 1e-7:
                        return index
                current.vertices.append(coordinate)
                return len(current.vertices) - 1

            first_new = vertex_at(projected)
            opposite_new = vertex_at(opposite)
            current.faces.pop(face_index)
            current.faces.append((first, first_new, opposite_new, fourth))
            current.faces.append((first_new, second, third, opposite_new))
            break
        return current

    if across_patterns:
        result = fill(guide.copy(), points)
    else:
        result = Mesh(vertices=[], faces=[], uv_name=guide.uv_name)
        for component in guide.connected_components():
            used = sorted({index for face_index in component for index in guide.faces[face_index]})
            remap = {old: new for new, old in enumerate(used)}
            part = Mesh(
                vertices=[guide.vertices[index] for index in used],
                faces=[tuple(remap[index] for index in guide.faces[face_index]) for face_index in component],
            )
            filled = fill(part, points)
            offset = len(result.vertices)
            result.vertices.extend(filled.vertices)
            result.faces.extend(tuple(index + offset for index in face) for face in filled.faces)
    result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
    return result


def apply_density(guide: Mesh, settings: RetopoSettings) -> Mesh:
    settings.validate()
    validate_guides(guide)
    creases = guide.edge_attributes.get("crease_edge", {})
    sharps = guide.edge_attributes.get("sharp_edge", {})
    counts = {edge: 1 for edge in guide.edges()}
    for edge in guide.edges():
        if edge in creases:
            value = max(0.0, min(1.0, float(creases[edge])))
            if settings.full_crease_control:
                if value >= 0.5:
                    counts[edge] = max(1, round(settings.base_density * 0.02))
                else:
                    counts[edge] = max(1, round(settings.base_density * value * 0.04))
            else:
                counts[edge] = max(1, round(settings.base_density * value * 0.22))
        if (
            edge in sharps
            and sharps[edge]
            and settings.density_add_threshold <= 1
            and settings.base_density >= 50
        ):
            counts[edge] = max(
                counts[edge], round(settings.base_density * 0.04 * settings.density_add)
            )
    incident = {index: [] for index in range(len(guide.vertices))}
    for edge in guide.edges():
        for vertex in edge:
            incident[vertex].append(edge)
    for _ in range(max(1, len(guide.faces) * 4)):
        previous = dict(counts)
        for face in guide.faces:
            edges = [Mesh.edge_key(face[index], face[(index + 1) % 4]) for index in range(4)]
            for first, second in ((0, 2), (1, 3)):
                value = max(counts[edges[first]], counts[edges[second]])
                counts[edges[first]] = counts[edges[second]] = value
        if settings.auto_density and not settings.full_crease_control and settings.base_density < 50:
            for first, second in guide.edges():
                for vertex, other in ((first, second), (second, first)):
                    direction = normalize(subtract(guide.vertices[other], guide.vertices[vertex]))
                    for candidate in incident[vertex]:
                        if candidate == (first, second):
                            continue
                        candidate_other = candidate[0] if candidate[1] == vertex else candidate[1]
                        candidate_direction = normalize(subtract(guide.vertices[candidate_other], guide.vertices[vertex]))
                        if sum(a * b for a, b in zip(direction, candidate_direction)) <= -0.9:
                            value = max(counts[first, second], counts[candidate])
                            counts[first, second] = counts[candidate] = value
        if not settings.auto_density or counts == previous:
            break
    return tessellate(guide, {edge: count for edge, count in counts.items() if count > 1})


def neutralize_crease(guide: Mesh, edges) -> Mesh:
    result = guide.copy()
    values = result.edge_attributes.setdefault("crease_edge", {})
    for first, second in edges:
        values[Mesh.edge_key(first, second)] = 0.5
    return result


def snap_guides_to_pattern(guide: Mesh, flat: Mesh, distance: float) -> Mesh:
    if distance <= 0:
        return guide.copy()
    result = guide.copy()
    borders = flat.boundary_edges()
    for index, vertex in enumerate(result.vertices):
        candidates = [
            nearest_on_segment(vertex, flat.vertices[first], flat.vertices[second])
            for first, second in borders
        ]
        if not candidates:
            continue
        nearest, measured = min(candidates, key=lambda item: item[1])
        if measured <= distance:
            result.vertices[index] = nearest
    return result


def _barycentric_2d(point, first, second, third):
    denominator = (
        (second[1] - third[1]) * (first[0] - third[0])
        + (third[0] - second[0]) * (first[1] - third[1])
    )
    if abs(denominator) < 1e-15:
        return None
    a = (
        (second[1] - third[1]) * (point[0] - third[0])
        + (third[0] - second[0]) * (point[1] - third[1])
    ) / denominator
    b = (
        (third[1] - first[1]) * (point[0] - third[0])
        + (first[0] - third[0]) * (point[1] - third[1])
    ) / denominator
    c = 1.0 - a - b
    weights = [max(0.0, value) for value in (a, b, c)]
    total = sum(weights)
    return tuple(value / total for value in weights)


class SourceSurface:
    def __init__(self, source: Mesh):
        if len(source.uv) != sum(len(face) for face in source.faces):
            raise ValueError(f"source does not contain a complete UV map named {source.uv_name}")
        self.uv_name = source.uv_name
        self.triangles = []
        patterns = _pattern_faces(source)
        parts = _part_faces(source)
        cursor = 0
        for face_index, face in enumerate(source.faces):
            for index in range(1, len(face) - 1):
                indices = (face[0], face[index], face[index + 1])
                uv = (source.uv[cursor], source.uv[cursor + index], source.uv[cursor + index + 1])
                positions = tuple(source.vertices[vertex] for vertex in indices)
                self.triangles.append((uv, positions, patterns[face_index], parts[face_index]))
            cursor += len(face)
        if not self.triangles:
            raise ValueError("source garment has no faces")

    def project(self, coordinate):
        best = None
        for uv, positions, pattern, part in self.triangles:
            weights = _barycentric_2d(coordinate, *uv)
            if weights is None:
                continue
            projected = tuple(sum(weights[index] * uv[index][axis] for index in range(3)) for axis in range(2))
            distance = math.dist(coordinate, projected)
            if best is None or distance < best[0]:
                best = (distance, weights, positions, pattern, part)
        if best is None:
            raise ValueError("source UV map contains no projectable triangles")
        _, weights, positions, pattern, part = best
        position = tuple(sum(weights[index] * positions[index][axis] for index in range(3)) for axis in range(3))
        return position, pattern, part


def _fit_uv(uv):
    if not uv:
        return []
    minimum_x = min(value[0] for value in uv)
    minimum_y = min(value[1] for value in uv)
    width = max(value[0] for value in uv) - minimum_x
    height = max(value[1] for value in uv) - minimum_y
    scale = max(width, height)
    if scale <= 1e-15:
        return [(0.0, 0.0) for _ in uv]
    return [
        (
            0.0075 + (value[0] - minimum_x) / scale * 0.985,
            0.0075 + (value[1] - minimum_y) / scale * 0.985,
        )
        for value in uv
    ]


def _separate(mesh: Mesh, face_values) -> Mesh:
    vertices = []
    faces = []
    uv = []
    lookup = {}
    for face_index, face in enumerate(mesh.faces):
        new_face = []
        for corner, old in enumerate(face):
            key = (old, face_values[face_index])
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(mesh.vertices[old])
            new_face.append(lookup[key])
            uv.append(mesh.uv[sum(len(value) for value in mesh.faces[:face_index]) + corner])
        faces.append(tuple(new_face))
    result = Mesh(vertices=vertices, faces=faces, uv=uv, uv_name=mesh.uv_name)
    result.attributes = {name: list(values) for name, values in mesh.attributes.items()}
    return result


def _merge_seams(mesh: Mesh, distance: float) -> Mesh:
    if distance <= 0:
        return mesh
    result = mesh.copy()
    protected = {
        index
        for edge, value in result.edge_attributes.get("bevel_weight_edge", {}).items()
        if value > 0
        for index in edge
    }
    cursor = 0
    remap = list(range(len(result.vertices)))
    representatives = {}
    for face in result.faces:
        for vertex in face:
            coordinate = result.uv[cursor]
            cursor += 1
            key = tuple(round(value, 8) for value in coordinate)
            if key in representatives:
                other = representatives[key]
                if (
                    vertex not in protected
                    and other not in protected
                    and math.dist(result.vertices[vertex], result.vertices[other]) <= distance
                ):
                    remap[vertex] = other
            else:
                representatives[key] = vertex
    unique = sorted(set(remap))
    indices = {old: new for new, old in enumerate(unique)}
    result.vertices = [result.vertices[index] for index in unique]
    result.faces = [tuple(indices[remap[index]] for index in face) for face in result.faces]
    return result


def merge_output_seams(mesh: Mesh, distance: float) -> Mesh:
    return _merge_seams(mesh, distance)


def thicken(guide: Mesh, thickness: float, offset: float = 0.0) -> Mesh:
    if not math.isfinite(thickness) or thickness < 0:
        raise ValueError("thickness must be finite and non-negative")
    if not math.isfinite(offset) or not -1 <= offset <= 1:
        raise ValueError("offset must be between -1 and 1")
    normals = vertex_normals(guide)
    front = [add(vertex, multiply(normal, thickness * (offset + 1) / 2)) for vertex, normal in zip(guide.vertices, normals)]
    back = [add(vertex, multiply(normal, thickness * (offset - 1) / 2)) for vertex, normal in zip(guide.vertices, normals)]
    count = len(guide.vertices)
    vertices = front + back
    faces = list(guide.faces) + [tuple(index + count for index in reversed(face)) for face in guide.faces]
    for first, second in guide.boundary_edges():
        faces.append((first, first + count, second + count, second))
    result = Mesh(vertices=vertices, faces=faces, uv_name=guide.uv_name)
    if guide.uv:
        result.uv = list(guide.uv) + list(guide.uv)
        for first, second in guide.boundary_edges():
            result.uv.extend([(0.0, 0.0)] * 4)
    return result


def diagnose(guide: Mesh, settings: RetopoSettings, source: Mesh | None):
    errors = {}
    if source is None:
        errors["source"] = "assign a flattened source garment"
    try:
        settings.validate()
    except ValueError as error:
        key = str(error).split()[0]
        errors[key] = str(error)
    non_quads = [index for index, face in enumerate(guide.faces) if len(face) != 4]
    if non_quads:
        errors["non_quad_faces"] = "guide faces must all be quads"
    return {
        "status": "error" if errors else "ok",
        "errors": errors,
        "non_quad_faces": non_quads,
        "guide_vertices": len(guide.vertices),
        "guide_faces": len(guide.faces),
    }


def retopologize(source: Mesh, guide: Mesh, settings: RetopoSettings | None = None) -> Mesh:
    settings = settings or RetopoSettings()
    settings.validate()
    validate_guides(guide)
    surface = SourceSurface(source)
    flat = flatten_to_patterns(source)
    working = guide.copy()
    if settings.initialize_planes:
        working = initialize_planes(flat, settings)
    unused = set(range(len(working.vertices))) - {index for face in working.faces for index in face}
    if unused:
        axes = _mirror_axes(working)
        if axes:
            working = mirror_guides(working, axes=axes, source=source)
    if settings.auto_fill_prepass or settings.auto_fill:
        points = [flat.vertices[index] for index, value in enumerate(flat.attributes["point_guide"]) if value]
        working = auto_fill(
            working, points, settings.auto_fill_max_iterations,
            across_patterns=settings.auto_fill,
        )
    if settings.output_generated_planes:
        working.uv = [working.vertices[index][:2] for face in working.faces for index in face]
        return working
    working = snap_guides_to_pattern(working, flat, settings.snapping_distance * 0.001)
    working = apply_density(working, settings)
    if settings.smooth_low_poly and settings.smooth_uvs:
        working = smooth(working, settings.smooth_low_poly, preserve_boundary=True)
    for _ in range(settings.subdiv_reproject):
        working = subdivide_quads(working)
    positions = []
    patterns = []
    parts = []
    for vertex in working.vertices:
        position, pattern, part = surface.project(vertex[:2])
        positions.append(position)
        patterns.append(pattern)
        parts.append(part)
    result = Mesh(vertices=positions, faces=list(working.faces), uv_name=source.uv_name)
    result.uv = [working.vertices[index][:2] for face in working.faces for index in face]
    face_patterns = []
    face_parts = []
    for face in working.faces:
        center = average(working.vertices[index] for index in face)
        _, pattern, part = surface.project(center[:2])
        face_patterns.append(pattern)
        face_parts.append(part)
    result.attributes["pattern_id"] = face_patterns
    result.attributes["part_id"] = face_parts
    face_planes = [0] * len(working.faces)
    for plane, component in enumerate(working.connected_components()):
        for face_index in component:
            face_planes[face_index] = plane
    result.attributes["retopo_plane_id"] = face_planes
    if settings.fit_uv:
        result.uv = _fit_uv(result.uv)
    result = _merge_seams(result, settings.seams_merge_distance)
    if settings.separate_patterns:
        result = _separate(result, face_patterns)
    if settings.separate_parts:
        result = _separate(result, face_parts)
    if settings.thickness_enabled:
        result = thicken(result, settings.thickness, settings.thickness_offset)
    return result
