"""Independent garment engine based on docs/research/retopo-planes-1-6-3-clean-room.md."""

from __future__ import annotations

from collections import Counter
from dataclasses import replace
from itertools import combinations, permutations
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
    roots = {}
    for face_index in range(len(mesh.faces)):
        face_root = root(face_index)
        if face_root not in roots:
            roots[face_root] = len(roots)
    return {index: roots[root(index)] for index in range(len(mesh.faces))}


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
    roots = {}
    for face_index in range(len(mesh.faces)):
        face_root = root(face_index)
        if face_root not in roots:
            roots[face_root] = len(roots)
    return {index: roots[root(index)] for index in range(len(mesh.faces))}


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
        source_indices = {vertex: index for index, vertex in enumerate(flat.vertices)}
        representatives = [source_indices[vertex] for vertex in simplified.vertices]
        for name, values in flat.attributes.items():
            simplified.attributes[name] = [values[index] for index in representatives]
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
            bool(source.attributes["point_guide"][vertex]) for vertex in flat.attributes["source_vertex"]
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
    if not fix_split_edges:
        result.edge_attributes["bad_mesh"] = {edge: True for edge in bad_edges}
        return result, bad_edges

    if not bad_edges:
        result.edge_attributes["bad_mesh"] = {}
        return result, []

    point_attribute_names = {
        name
        for name, values in mesh.attributes.items()
        if mesh.attribute_domains.get(name) == "POINT"
        or (
            name not in mesh.attribute_domains
            and len(values) == len(mesh.vertices)
            and name not in {"part_id", "retopo_plane_id"}
            and (name != "pattern_id" or "source_position" in mesh.attributes)
        )
    }
    face_order = sorted(
        range(len(mesh.faces)),
        key=lambda index: (tuple(mesh.vertices[vertex] for vertex in mesh.faces[index]), mesh.faces[index]),
    )
    result.faces = [mesh.faces[index] for index in face_order]
    if all(len(face) == 3 for face in mesh.faces):
        incident_faces = {
            face_index
            for face_index, face in enumerate(mesh.faces)
            if any(Mesh.edge_key(vertex, face[(index + 1) % 3]) in bad_edges for index, vertex in enumerate(face))
        }
        components = {}
        for face_index, component in _part_faces(mesh).items():
            components.setdefault(component, []).append(face_index)
        split_faces = {
            face_index
            for component_faces in components.values()
            if len(bad_edges) == 1 and len(component_faces) == 3
            and len({frozenset(mesh.faces[index]) for index in component_faces}) == 3
            and all(face_index in incident_faces for face_index in component_faces)
            for face_index in component_faces
        }
        for canonical_index, original_index in enumerate(face_order):
            if original_index not in split_faces:
                continue
            face = mesh.faces[original_index]
            replacements = {}
            for old in face:
                replacements[old] = len(result.vertices)
                result.vertices.append(result.vertices[old])
                for name, values in result.attributes.items():
                    if name in point_attribute_names:
                        values.append(values[old])
            result.faces[canonical_index] = tuple(replacements[index] for index in face)

    protected_edges = set(bad_edges)
    while non_manifold_edges(result):
        edge = min(
            non_manifold_edges(result),
            key=lambda candidate: tuple(sorted(result.vertices[index] for index in candidate)),
        )
        face_edges = [
            {
                Mesh.edge_key(face[index], face[(index + 1) % len(face)])
                for index in range(len(face))
            }
            for face in result.faces
        ]
        face_indices = [index for index, edges in enumerate(face_edges) if edge in edges]
        original_faces = {index: result.faces[index] for index in face_indices}
        vertex_faces = {index: [] for index in range(len(result.vertices))}
        for face_index, face in enumerate(result.faces):
            for vertex in face:
                vertex_faces[vertex].append(face_index)
        parents = {index: index for index in range(len(result.faces))}

        def root(index):
            while parents[index] != index:
                parents[index] = parents[parents[index]]
                index = parents[index]
            return index

        candidates = set(vertex_faces[edge[0]]) | set(vertex_faces[edge[1]])
        for first in candidates:
            for second in candidates:
                if second <= first:
                    continue
                shared = face_edges[first] & face_edges[second]
                if any(
                    candidate not in protected_edges and any(vertex in candidate for vertex in edge)
                    for candidate in shared
                ):
                    first_root, second_root = root(first), root(second)
                    if first_root != second_root:
                        parents[second_root] = first_root
        fans = {}
        for face_index in candidates:
            fans.setdefault(root(face_index), []).append(face_index)
        retained_candidates = set()
        for fan in fans.values():
            incident = sorted(index for index in fan if index in face_indices)
            for size in (1, 2):
                retained_candidates.update(combinations(incident, size))
        connectors = []
        for candidate in set().union(*face_edges):
            if candidate in protected_edges or not any(vertex in candidate for vertex in edge):
                continue
            incident = [index for index, edges in enumerate(face_edges) if candidate in edges]
            if len(incident) != 2:
                continue
            corners = []
            for face_index in incident:
                face = result.faces[face_index]
                corner = next(
                    index for index, vertex in enumerate(face)
                    if Mesh.edge_key(vertex, face[(index + 1) % len(face)]) == candidate
                )
                corners.append((face_index, corner))
            connectors.append(corners)

        def simulate(retained, order):
            simulated = list(result.faces)
            created = []
            for old in order:
                parents = {index: index for index in range(len(result.faces))}

                def root(index):
                    while parents[index] != index:
                        parents[index] = parents[parents[index]]
                        index = parents[index]
                    return index

                candidates = vertex_faces[old]
                for first in candidates:
                    for second in candidates:
                        if second <= first:
                            continue
                        shared = face_edges[first] & face_edges[second]
                        if any(
                            candidate not in protected_edges and old in candidate
                            for candidate in shared
                        ):
                            first_root, second_root = root(first), root(second)
                            if first_root != second_root:
                                parents[second_root] = first_root
                fans = {}
                for face_index in candidates:
                    fans.setdefault(root(face_index), []).append(face_index)
                for fan in fans.values():
                    incident = sorted(index for index in fan if index in face_indices)
                    if not incident or all(index in retained for index in incident):
                        continue
                    if retained.intersection(fan) or len(incident) > 2:
                        groups = [[index] for index in incident]
                    else:
                        groups = [incident]
                    for group in groups:
                        if all(index in retained for index in group):
                            continue
                        if retained.intersection(fan) and not any(
                            Mesh.edge_key(simulated[face_index][corner], simulated[face_index][(corner + 1) % len(simulated[face_index])]) == edge
                            for face_index in group
                            for corner in range(len(simulated[face_index]))
                        ):
                            continue
                        replacement = len(result.vertices) + len(created)
                        created.append(old)
                        if len(groups) == 1:
                            target_faces = fan
                        else:
                            target_faces = []
                            pending = [group[0]]
                            while pending:
                                current = pending.pop()
                                if current in target_faces:
                                    continue
                                target_faces.append(current)
                                for candidate in fan:
                                    if candidate in target_faces or (candidate in incident and candidate != group[0]):
                                        continue
                                    if any(
                                        candidate_edge != edge and candidate_edge not in protected_edges and old in candidate_edge
                                        for candidate_edge in face_edges[current] & face_edges[candidate]
                                    ):
                                        pending.append(candidate)
                        for face_index in target_faces:
                            simulated[face_index] = tuple(
                                replacement if index == old else index for index in simulated[face_index]
                            )
            return simulated, created

        best = None
        best_score = None
        for retained in sorted(retained_candidates):
            for order in (edge, tuple(reversed(edge))):
                simulated, created = simulate(set(retained), order)
                broken = 0
                for (first, first_corner), (second, second_corner) in connectors:
                    first_face, second_face = simulated[first], simulated[second]
                    first_edge = Mesh.edge_key(first_face[first_corner], first_face[(first_corner + 1) % len(first_face)])
                    second_edge = Mesh.edge_key(second_face[second_corner], second_face[(second_corner + 1) % len(second_face)])
                    broken += first_edge != second_edge
                positions = result.vertices + [result.vertices[old] for old in created]
                signature = tuple(sorted(
                    tuple(sorted(positions[index] for index in face))
                    for face in simulated
                ))
                residual = len(non_manifold_edges(Mesh(vertices=[], faces=simulated)))
                score = (broken, residual, len(created), -len(retained), signature)
                if best_score is None or score < best_score:
                    best = simulated, created
                    best_score = score
        result.faces = best[0]
        for old in best[1]:
            result.vertices.append(result.vertices[old])
            for name, values in result.attributes.items():
                if name in point_attribute_names:
                    values.append(values[old])
        for face_index, original_face in original_faces.items():
            corner = next(
                index for index, vertex in enumerate(original_face)
                if Mesh.edge_key(vertex, original_face[(index + 1) % len(original_face)]) == edge
            )
            new_face = result.faces[face_index]
            protected_edges.add(Mesh.edge_key(new_face[corner], new_face[(corner + 1) % len(new_face)]))

    original_order_faces = [None] * len(mesh.faces)
    for canonical_index, original_index in enumerate(face_order):
        original_order_faces[original_index] = result.faces[canonical_index]
    result.faces = original_order_faces
    result.uv = list(mesh.uv)
    original_vertex_count = len(result.vertices)
    edge_attributes = {name: {} for name in mesh.edge_attributes}
    for face_index, new_face in enumerate(result.faces):
        original_face = mesh.faces[face_index]
        for corner, first in enumerate(original_face):
            second = original_face[(corner + 1) % len(original_face)]
            original_edge = Mesh.edge_key(first, second)
            new_edge = Mesh.edge_key(new_face[corner], new_face[(corner + 1) % len(new_face)])
            for name, values in mesh.edge_attributes.items():
                if original_edge in values:
                    edge_attributes[name][new_edge] = values[original_edge]
    for line in result.lines:
        for name, values in mesh.edge_attributes.items():
            if line in values:
                edge_attributes[name][line] = values[line]
    result.edge_attributes = edge_attributes
    originally_used = {index for face in mesh.faces for index in face} | {index for edge in mesh.lines for index in edge}
    originally_loose = set(range(len(mesh.vertices))) - originally_used
    used = {index for face in result.faces for index in face} | {index for edge in result.lines for index in edge}
    used.update(originally_loose)
    ordered = sorted(used)
    remap = {old: new for new, old in enumerate(ordered)}
    result.vertices = [result.vertices[index] for index in ordered]
    result.faces = [tuple(remap[index] for index in face) for face in result.faces]
    result.lines = [tuple(remap[index] for index in edge) for edge in result.lines]
    for name, values in result.attributes.items():
        if name in point_attribute_names:
            result.attributes[name] = [values[index] for index in ordered]
    result.edge_attributes = {
        name: {
            Mesh.edge_key(remap[first], remap[second]): value
            for (first, second), value in values.items()
            if first in remap and second in remap
        }
        for name, values in result.edge_attributes.items()
    }

    def debug_edge(original_edge):
        for face_index in face_order:
            original_face = mesh.faces[face_index]
            for corner, first in enumerate(original_face):
                second = original_face[(corner + 1) % len(original_face)]
                if Mesh.edge_key(first, second) != original_edge:
                    continue
                new_face = result.faces[face_index]
                return Mesh.edge_key(new_face[corner], new_face[(corner + 1) % len(new_face)])
        raise ValueError("non-manifold edge has no incident face")

    bad_edges = [debug_edge(edge) for edge in bad_edges]
    result.edge_attributes["bad_mesh"] = {edge: True for edge in bad_edges}
    return result, bad_edges


def validate_guides(guide: Mesh, allow_empty: bool = False):
    if not guide.faces:
        if allow_empty:
            return
        raise ValueError("guide mesh must contain at least one quad face")
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


def _ordered_loop(mesh: Mesh, loop, indices):
    center = average(mesh.vertices[index] for index in loop)
    return sorted(
        indices,
        key=lambda index: math.atan2(
            mesh.vertices[index][1] - center[1], mesh.vertices[index][0] - center[0]
        ),
    )


def _corner_candidates(mesh: Mesh, loop, settings: RetopoSettings):
    explicit = mesh.attributes.get("point_guide", [])
    marked = [index for index in loop if index < len(explicit) and explicit[index]]
    if marked and len(marked) <= 4:
        return _ordered_loop(mesh, loop, marked)
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
        return _ordered_loop(mesh, loop, filtered)
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
    return _ordered_loop(mesh, loop, result)


def initialize_planes(flat: Mesh, settings: RetopoSettings) -> Mesh:
    settings.validate()
    components = flat.connected_components()
    if len(components) > 1:
        combined = Mesh(vertices=[], faces=[], uv_name=flat.uv_name)
        for component in components:
            used = sorted({index for face_index in component for index in flat.faces[face_index]})
            remap = {old: new for new, old in enumerate(used)}
            part = Mesh(
                vertices=[flat.vertices[index] for index in used],
                faces=[tuple(remap[index] for index in flat.faces[face_index]) for face_index in component],
                uv_name=flat.uv_name,
                attributes={
                    name: [values[index] for index in used]
                    for name, values in flat.attributes.items()
                    if len(values) == len(flat.vertices)
                },
            )
            initialized = initialize_planes(part, settings)
            offset = len(combined.vertices)
            combined.vertices.extend(initialized.vertices)
            combined.faces.extend(tuple(index + offset for index in face) for face in initialized.faces)
            combined.uv.extend(initialized.uv)
        return combined
    loops = _boundary_loops(flat)
    if not loops:
        raise ValueError("plane initialization requires an open pattern border")
    if len(loops) > 1:
        if all(len(face) == 4 for face in flat.faces):
            result = flat.copy()
            result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
            return result
        adjacency = {}
        for first, second in flat.boundary_edges():
            adjacency.setdefault(first, set()).add(second)
            adjacency.setdefault(second, set()).add(first)
        closed_simple_loops = [
            loop for loop in loops
            if all(len(adjacency[index]) == 2 for index in loop)
        ]
        outer = max(closed_simple_loops or loops, key=lambda loop: abs(signed_area(loop, flat.vertices)))
        remaining = sorted(
            {index for face in flat.faces for index in face} - set(outer),
            key=lambda index: math.atan2(
                flat.vertices[index][1] - average(flat.vertices[item] for item in outer)[1],
                flat.vertices[index][0] - average(flat.vertices[item] for item in outer)[0],
            ),
        )
        inner = remaining or min(loops, key=len)
        count = max(len(outer), len(inner))
        vertices = [flat.vertices[index] for index in outer + inner]
        faces = []
        for index in range(count):
            following = (index + 1) % count
            faces.append((
                index,
                following,
                len(outer) + following % len(inner),
                len(outer) + index % len(inner),
            ))
        result = Mesh(vertices=vertices, faces=faces, uv_name=flat.uv_name)
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
    point_attribute_names = {
        name
        for name, values in guide.attributes.items()
        if guide.attribute_domains.get(name) == "POINT"
        or (
            name not in guide.attribute_domains
            and len(values) == len(guide.vertices)
            and name not in {"part_id", "retopo_plane_id"}
            and (name != "pattern_id" or "source_position" in guide.attributes)
        )
    }
    face_attribute_names = {
        name
        for name, values in guide.attributes.items()
        if guide.attribute_domains.get(name) == "FACE"
        or (
            name not in guide.attribute_domains
            and len(values) == len(guide.faces)
            and (name != "pattern_id" or "source_position" not in guide.attributes)
        )
    }

    def face_signature(mesh, face):
        return tuple(sorted(
            tuple(round(value, 9) for value in mesh.vertices[index])
            for index in face
        ))

    signatures = {face_signature(result, face) for face in result.faces}
    for axis in axes:
        component = result.copy()
        if source is None:
            center = (0.0, 0.0)
        elif guide.lines:
            target = targets[axis]
            center = (
                (guide_center[0] + target[0]) / 2,
                (guide_center[1] + target[1]) / 2,
            )
        else:
            center = targets[axis][:2]
        component.vertices = [
            (2 * center[0] - vertex[0] if axis == "x" else vertex[0],
             2 * center[1] - vertex[1] if axis == "y" else vertex[1], vertex[2])
            for vertex in component.vertices
        ]
        component.faces = [tuple(reversed(face)) for face in component.faces]
        unique_face_indices = [
            index for index, face in enumerate(component.faces)
            if face_signature(component, face) not in signatures
        ]
        if not unique_face_indices:
            continue
        position_indices = {
            tuple(round(value, 9) for value in vertex): index
            for index, vertex in enumerate(result.vertices)
        }

        def mirrored_index(old):
            key = tuple(round(value, 9) for value in component.vertices[old])
            if key not in position_indices:
                position_indices[key] = len(result.vertices)
                result.vertices.append(component.vertices[old])
                for name, values in result.attributes.items():
                    if name in point_attribute_names:
                        values.append(component.attributes[name][old])
            return position_indices[key]

        for face_index in unique_face_indices:
            face = component.faces[face_index]
            mirrored_face = tuple(mirrored_index(index) for index in face)
            result.faces.append(mirrored_face)
            for name in face_attribute_names:
                result.attributes[name].append(component.attributes[name][face_index])
            signatures.add(face_signature(component, face))
            for corner, first in enumerate(face):
                second = face[(corner + 1) % len(face)]
                original_edge = Mesh.edge_key(first, second)
                new_edge = Mesh.edge_key(mirrored_face[corner], mirrored_face[(corner + 1) % len(face)])
                for name, values in component.edge_attributes.items():
                    target = result.edge_attributes.setdefault(name, {})
                    if original_edge in values and new_edge not in target:
                        target[new_edge] = values[original_edge]
        for edge in component.lines:
            mirrored_edge = Mesh.edge_key(*(mirrored_index(index) for index in edge))
            result.lines.append(mirrored_edge)
            for name, values in component.edge_attributes.items():
                target = result.edge_attributes.setdefault(name, {})
                if edge in values and mirrored_edge not in target:
                    target[mirrored_edge] = values[edge]
    result.uv = [result.vertices[index][:2] for face in result.faces for index in face]
    return result


def auto_fill(guide: Mesh, point_guides, max_iterations: int = 5, across_patterns: bool = True) -> Mesh:
    if max_iterations < 0:
        raise ValueError("max_iterations must be non-negative")
    validate_guides(guide)
    points = [tuple(point) for point in point_guides]
    face_attribute_names = {
        name
        for name, values in guide.attributes.items()
        if guide.attribute_domains.get(name) == "FACE"
        or (
            name not in guide.attribute_domains
            and len(values) == len(guide.faces)
            and (name != "pattern_id" or "source_position" not in guide.attributes)
        )
    }

    def subdivide(current):
        result = subdivide_quads(current)
        for name in face_attribute_names:
            result.attributes[name] = [value for value in current.attributes[name] for _ in range(4)]
        result.attribute_domains = dict(current.attribute_domains)
        return result

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
            current = subdivide(current)
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
                parent_attributes = {name: current.attributes[name][0] for name in face_attribute_names}
                current.vertices.extend([first, second])
                current.faces = [
                    (face[0], len(current.vertices) - 2, len(current.vertices) - 1, face[3]),
                    (len(current.vertices) - 2, face[1], face[2], len(current.vertices) - 1),
                ]
                for name in face_attribute_names:
                    current.attributes[name] = [parent_attributes[name]] * 2
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
                current = subdivide(current)
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
            parent_attributes = {name: current.attributes[name][face_index] for name in face_attribute_names}
            current.faces.pop(face_index)
            for name in face_attribute_names:
                current.attributes[name].pop(face_index)
                current.attributes[name].extend([parent_attributes[name]] * 2)
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


def requested_density_counts(guide: Mesh, settings: RetopoSettings):
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
                counts[edge],
                round(settings.base_density * 0.07 * settings.density_add * (1.7 - settings.density_add_threshold)),
            )
    return counts


def density_edge_counts(guide: Mesh, settings: RetopoSettings):
    counts = requested_density_counts(guide, settings)
    incident = {index: [] for index in range(len(guide.vertices))}
    for edge in guide.edges():
        for vertex in edge:
            incident[vertex].append(edge)
    iteration_limit = max(1, settings.loop_iteration or len(guide.faces) * 4)
    for iteration in range(max(iteration_limit, len(guide.faces) * 4)):
        previous = dict(counts)
        for face in guide.faces:
            edges = [Mesh.edge_key(face[index], face[(index + 1) % 4]) for index in range(4)]
            for first, second in ((0, 2), (1, 3)):
                value = max(counts[edges[first]], counts[edges[second]])
                counts[edges[first]] = counts[edges[second]] = value
        if (
            iteration < iteration_limit
            and settings.auto_density
            and not settings.full_crease_control
            and settings.base_density < 50
        ):
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
        if counts == previous:
            break
    return counts


def apply_density(guide: Mesh, settings: RetopoSettings) -> Mesh:
    counts = density_edge_counts(guide, settings)
    return tessellate(guide, {edge: count for edge, count in counts.items() if count > 1})


def polyloop_guides(guide: Mesh) -> Mesh:
    validate_guides(guide)
    return Mesh(vertices=guide.vertices, faces=[], lines=guide.edges(), uv_name=guide.uv_name)


def sibling_contact_edges(guide: Mesh):
    edge_faces = {edge: 0 for edge in guide.edges()}
    for face in guide.faces:
        for index, first in enumerate(face):
            edge_faces[Mesh.edge_key(first, face[(index + 1) % len(face)])] += 1
    contacts = {edge for edge, count in edge_faces.items() if count == 2}
    positional = {}
    for edge in guide.boundary_edges():
        coordinates = tuple(sorted(
            tuple(round(value, 6) for value in guide.vertices[vertex])
            for vertex in edge
        ))
        if coordinates in positional:
            contacts.update((edge, positional[coordinates]))
        else:
            positional[coordinates] = edge
    return contacts


def mismatch_edges(guide: Mesh, settings: RetopoSettings):
    counts = requested_density_counts(guide, settings)
    mismatches = set()
    for face in guide.faces:
        edges = [Mesh.edge_key(face[index], face[(index + 1) % 4]) for index in range(4)]
        for first, second in ((0, 2), (1, 3)):
            if counts[edges[first]] != counts[edges[second]]:
                mismatches.update((edges[first], edges[second]))
    return mismatches


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
        self.vertex_positions = {}
        patterns = _pattern_faces(source)
        parts = _part_faces(source)
        cursor = 0
        for face_index, face in enumerate(source.faces):
            for index, vertex in enumerate(face):
                key = tuple(round(value, 12) for value in source.uv[cursor + index])
                self.vertex_positions.setdefault(key, {})[vertex] = source.vertices[vertex]
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
        key = tuple(round(value, 12) for value in coordinate)
        if key in self.vertex_positions:
            position = average(self.vertex_positions[key].values())
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
    group_edges = {}
    cursor = 0
    for face_index, face in enumerate(mesh.faces):
        group = face_values[face_index]
        new_face = []
        for corner, old in enumerate(face):
            key = (old, group)
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(mesh.vertices[old])
            new_face.append(lookup[key])
            uv.append(mesh.uv[cursor + corner])
        faces.append(tuple(new_face))
        for index, first in enumerate(new_face):
            edge = Mesh.edge_key(first, new_face[(index + 1) % len(new_face)])
            group_edges.setdefault(group, set()).add(edge)
        cursor += len(face)
    result = Mesh(
        vertices=vertices, faces=faces, uv=uv, uv_name=mesh.uv_name,
        attribute_domains=dict(mesh.attribute_domains),
    )
    for name, values in mesh.attributes.items():
        domain = mesh.attribute_domains.get(name)
        if domain == "POINT":
            result.attributes[name] = [values[old] for old, _ in lookup]
        else:
            result.attributes[name] = list(values)
    endpoint_groups = {}
    for (old, group), new in lookup.items():
        endpoint_groups.setdefault(old, {})[group] = new
    for name, values in mesh.edge_attributes.items():
        result.edge_attributes[name] = {}
        for (first, second), value in values.items():
            for group in set(endpoint_groups.get(first, {})) & set(endpoint_groups.get(second, {})):
                edge = Mesh.edge_key(endpoint_groups[first][group], endpoint_groups[second][group])
                if edge in group_edges.get(group, set()):
                    result.edge_attributes[name][edge] = value
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
    eligible = {index for edge in result.boundary_edges() for index in edge} - protected
    vertex_faces = {index: set() for index in range(len(result.vertices))}
    for face_index, face in enumerate(result.faces):
        for vertex in face:
            vertex_faces[vertex].add(face_index)
    remap = list(range(len(result.vertices)))
    cluster_faces = {index: set(faces) for index, faces in vertex_faces.items()}
    cells = {}
    for vertex in sorted(eligible):
        position = result.vertices[vertex]
        cell = tuple(math.floor(value / distance) for value in position)
        found = None
        for x in range(cell[0] - 1, cell[0] + 2):
            for y in range(cell[1] - 1, cell[1] + 2):
                for z in range(cell[2] - 1, cell[2] + 2):
                    for other in cells.get((x, y, z), []):
                        if not cluster_faces[vertex] & cluster_faces[other] and math.dist(position, result.vertices[other]) <= distance:
                            found = other
                            cluster_faces[found].update(cluster_faces[vertex])
                            break
                    if found is not None:
                        break
                if found is not None:
                    break
            if found is not None:
                break
        if found is None:
            found = vertex
            cells.setdefault(cell, []).append(vertex)
        remap[vertex] = found
    unique = sorted(set(remap))
    indices = {old: new for new, old in enumerate(unique)}
    result.vertices = [result.vertices[index] for index in unique]
    faces = []
    uv = []
    kept_faces = []
    cursor = 0
    for face_index, face in enumerate(result.faces):
        clean_face = []
        clean_uv = []
        for corner, old in enumerate(face):
            new = indices[remap[old]]
            if new not in clean_face:
                clean_face.append(new)
                if result.uv:
                    clean_uv.append(result.uv[cursor + corner])
        cursor += len(face)
        if len(clean_face) >= 3:
            faces.append(tuple(clean_face))
            uv.extend(clean_uv)
            kept_faces.append(face_index)
    original_face_count = len(result.faces)
    original_vertex_count = len(result.vertices)
    result.faces = faces
    result.uv = uv
    for name, values in result.attributes.items():
        domain = result.attribute_domains.get(name)
        if domain == "FACE" or (domain is None and len(values) == original_face_count):
            result.attributes[name] = [values[index] for index in kept_faces]
        elif domain == "POINT" or (domain is None and len(values) == original_vertex_count):
            result.attributes[name] = [values[index] for index in unique]
    result.edge_attributes = {
        name: {
            Mesh.edge_key(indices[remap[first]], indices[remap[second]]): value
            for (first, second), value in values.items()
            if remap[first] != remap[second]
        }
        for name, values in result.edge_attributes.items()
    }
    return result


def merge_output_seams(mesh: Mesh, distance: float) -> Mesh:
    return _merge_seams(mesh, distance)


def thicken(guide: Mesh, thickness: float, offset: float = 0.0) -> Mesh:
    if not math.isfinite(thickness):
        raise ValueError("thickness must be finite")
    if not math.isfinite(offset) or not -1 <= offset <= 1:
        raise ValueError("offset must be between -1 and 1")
    normals = vertex_normals(guide)
    front = [add(vertex, multiply(normal, thickness * (offset + 1) / 2)) for vertex, normal in zip(guide.vertices, normals)]
    back = [add(vertex, multiply(normal, thickness * (offset - 1) / 2)) for vertex, normal in zip(guide.vertices, normals)]
    count = len(guide.vertices)
    vertices = back + front
    faces = [tuple(reversed(face)) for face in guide.faces] + [
        tuple(index + count for index in face) for face in guide.faces
    ]
    boundary = set(guide.boundary_edges())
    directed = []
    for face_index, face in enumerate(guide.faces):
        for index, first in enumerate(face):
            second = face[(index + 1) % len(face)]
            if Mesh.edge_key(first, second) in boundary:
                directed.append((first, second, face_index))
    for first, second, _ in directed:
        faces.append((first, second, second + count, first + count))
    result = Mesh(vertices=vertices, faces=faces, uv_name=guide.uv_name, attribute_domains=dict(guide.attribute_domains))
    for name, values in guide.attributes.items():
        domain = guide.attribute_domains.get(name)
        if domain == "FACE" or (domain is None and len(values) == len(guide.faces)):
            result.attributes[name] = values + values + [values[face_index] for _, _, face_index in directed]
        elif domain == "POINT" or (domain is None and len(values) == len(guide.vertices)):
            result.attributes[name] = values + values
        else:
            result.attributes[name] = list(values)
    result.edge_attributes = {
        name: dict(values) for name, values in guide.edge_attributes.items()
    }
    if guide.uv:
        front_uv = list(guide.uv)
        back_uv = []
        offset = 0
        for face in guide.faces:
            back_uv.extend(guide.uv[offset + index] for index in reversed(range(len(face))))
            offset += len(face)
        result.uv = back_uv + front_uv
        face_uv = _face_uv_maps(guide)
        for first, second, face_index in directed:
            first_uv = face_uv[face_index][first]
            second_uv = face_uv[face_index][second]
            result.uv.extend([first_uv, second_uv, second_uv, first_uv])
    if thickness < 0:
        original_faces = result.faces
        result.faces = [tuple(reversed(face)) for face in original_faces]
        if result.uv:
            uv = []
            cursor = 0
            for face in original_faces:
                uv.extend(result.uv[cursor + index] for index in reversed(range(len(face))))
                cursor += len(face)
            result.uv = uv
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
    if not guide.faces and not settings.initialize_planes:
        errors["empty_guide"] = "guide mesh must contain at least one quad face"
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


def morph_preview(source: Mesh, guide: Mesh, settings: RetopoSettings) -> Mesh:
    preview_settings = replace(
        settings,
        fit_uv=False,
        thickness_enabled=False,
        separate_patterns=False,
        separate_parts=False,
    )
    projected = retopologize(source, guide, preview_settings)
    flat = [None] * len(projected.vertices)
    cursor = 0
    for face in projected.faces:
        for vertex in face:
            if flat[vertex] is None:
                flat[vertex] = projected.uv[cursor]
            cursor += 1
    result = projected.copy()
    result.vertices = []
    for index, vertex in enumerate(projected.vertices):
        coordinate = flat[index] if flat[index] is not None else vertex[:2]
        result.vertices.append(lerp((coordinate[0], coordinate[1], 0.0), vertex, settings.morph_2d_3d))
    return result


def retopologize(source: Mesh, guide: Mesh, settings: RetopoSettings | None = None) -> Mesh:
    settings = settings or RetopoSettings()
    settings.validate()
    validate_guides(guide, allow_empty=settings.initialize_planes)
    surface = SourceSurface(source)
    flat = flatten_to_patterns(source)
    working = guide.copy()
    if settings.initialize_planes:
        working = initialize_planes(flat, settings)
        marked = guide.attributes.get("point_guide", [])
        manual_points = [point for index, point in enumerate(guide.vertices) if index < len(marked) and marked[index]]
        if len(manual_points) >= 4:
            for face in working.faces:
                if len(face) != 4:
                    continue
                center = average(working.vertices[index] for index in face)
                candidates = sorted(manual_points, key=lambda point: math.dist(center, point))[:8]
                assignment = min(
                    permutations(candidates, 4),
                    key=lambda points: sum(math.dist(working.vertices[index], point) for index, point in zip(face, points)),
                )
                for index, point in zip(face, assignment):
                    working.vertices[index] = point
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
        working.uv = []
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
    if settings.smooth_low_poly and not settings.smooth_uvs:
        result.vertices = smooth(result, settings.smooth_low_poly).vertices
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
    if "bevel_weight_edge" in working.edge_attributes:
        result.edge_attributes["bevel_weight_edge"] = dict(working.edge_attributes["bevel_weight_edge"])
    if settings.fit_uv:
        result.uv = _fit_uv(result.uv)
    result = _merge_seams(result, settings.seams_merge_distance)
    if settings.separate_patterns:
        result = _separate(result, result.attributes["pattern_id"])
    if settings.separate_parts:
        result = _separate(result, result.attributes["part_id"])
    if settings.thickness_enabled:
        result = thicken(result, settings.thickness, settings.thickness_offset)
    return result
