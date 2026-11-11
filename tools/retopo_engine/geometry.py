"""Geometry helpers for the behavioral contract in docs/research/retopo-planes-1-6-3-clean-room.md."""

from __future__ import annotations

import math

from .model import Edge, Mesh


def add(first, second):
    return tuple(a + b for a, b in zip(first, second))


def subtract(first, second):
    return tuple(a - b for a, b in zip(first, second))


def multiply(vector, scalar):
    return tuple(value * scalar for value in vector)


def average(vectors):
    vectors = list(vectors)
    if not vectors:
        return (0.0, 0.0, 0.0)
    return tuple(sum(values) / len(vectors) for values in zip(*vectors))


def lerp(first, second, factor):
    return add(multiply(first, 1.0 - factor), multiply(second, factor))


def dot(first, second):
    return sum(a * b for a, b in zip(first, second))


def cross(first, second):
    return (
        first[1] * second[2] - first[2] * second[1],
        first[2] * second[0] - first[0] * second[2],
        first[0] * second[1] - first[1] * second[0],
    )


def normalize(vector):
    length = math.sqrt(dot(vector, vector))
    return multiply(vector, 1.0 / length) if length > 1e-15 else (0.0, 0.0, 1.0)


def signed_area(face, vertices):
    return 0.5 * sum(
        vertices[face[index]][0] * vertices[face[(index + 1) % len(face)]][1]
        - vertices[face[(index + 1) % len(face)]][0] * vertices[face[index]][1]
        for index in range(len(face))
    )


def nearest_on_segment(point, first, second):
    direction = subtract(second, first)
    length_squared = dot(direction, direction)
    if length_squared <= 1e-30:
        return first, math.dist(point, first)
    factor = max(0.0, min(1.0, dot(subtract(point, first), direction) / length_squared))
    nearest = add(first, multiply(direction, factor))
    return nearest, math.dist(point, nearest)


def weld(mesh: Mesh, distance: float, protected_edges: set[Edge] | None = None) -> Mesh:
    if distance <= 0 or not mesh.vertices:
        return mesh.copy()
    protected_edges = protected_edges or set()
    protected_vertices = {index for edge in protected_edges for index in edge}
    cells = {}
    representatives = []
    for index, vertex in enumerate(mesh.vertices):
        cell = tuple(math.floor(value / distance) for value in vertex)
        found = None
        if index not in protected_vertices:
            for x in range(cell[0] - 1, cell[0] + 2):
                for y in range(cell[1] - 1, cell[1] + 2):
                    for z in range(cell[2] - 1, cell[2] + 2):
                        for candidate in cells.get((x, y, z), []):
                            if candidate in protected_vertices:
                                continue
                            if math.dist(vertex, mesh.vertices[candidate]) <= distance:
                                found = candidate
                                break
                        if found is not None:
                            break
                    if found is not None:
                        break
                if found is not None:
                    break
        if found is None:
            found = index
            cells.setdefault(cell, []).append(index)
        representatives.append(found)
    unique = sorted(set(representatives))
    remap = {old: new for new, old in enumerate(unique)}
    result = Mesh(
        vertices=[mesh.vertices[index] for index in unique],
        faces=[tuple(remap[representatives[index]] for index in face) for face in mesh.faces],
        uv_name=mesh.uv_name,
    )
    result.faces = [face for face in result.faces if len(set(face)) >= 3]
    return result


def subdivide_quads(mesh: Mesh) -> Mesh:
    if any(len(face) != 4 for face in mesh.faces):
        raise ValueError("quad subdivision requires quad faces")
    edge_faces = {edge: [] for edge in mesh.edges()}
    for face_index, face in enumerate(mesh.faces):
        for index, vertex in enumerate(face):
            edge_faces[Mesh.edge_key(vertex, face[(index + 1) % 4])].append(face_index)
    face_points = [average(mesh.vertices[index] for index in face) for face in mesh.faces]
    edge_points = {}
    for edge, adjacent in edge_faces.items():
        endpoints = [mesh.vertices[index] for index in edge]
        if len(adjacent) == 1:
            edge_points[edge] = average(endpoints)
        else:
            edge_points[edge] = average(endpoints + [face_points[index] for index in adjacent])
    boundary_edges = set(mesh.boundary_edges())
    vertex_neighbors = {index: set() for index in range(len(mesh.vertices))}
    for first, second in mesh.edges():
        vertex_neighbors[first].add(second)
        vertex_neighbors[second].add(first)
    vertex_faces = {index: [] for index in range(len(mesh.vertices))}
    for face_index, face in enumerate(mesh.faces):
        for vertex in face:
            vertex_faces[vertex].append(face_index)
    new_vertices = []
    vertex_map = {}
    edge_map = {}
    face_map = {}

    def new_index(point):
        new_vertices.append(tuple(point))
        return len(new_vertices) - 1

    boundary_neighbors = {index: [] for index in range(len(mesh.vertices))}
    for first, second in boundary_edges:
        boundary_neighbors[first].append(second)
        boundary_neighbors[second].append(first)
    for index, point in enumerate(mesh.vertices):
        if boundary_neighbors[index]:
            updated = point
        else:
            adjacent_faces = [face_points[face] for face in vertex_faces[index]]
            face_average = average(adjacent_faces)
            edge_average = average(
                average((point, mesh.vertices[neighbor]))
                for neighbor in vertex_neighbors[index]
            )
            count = len(vertex_faces[index])
            updated = point if count == 0 else add(
                multiply(face_average, 1.0 / count),
                add(
                    multiply(edge_average, 2.0 / count),
                    multiply(point, (count - 3.0) / count),
                ),
            )
        vertex_map[index] = new_index(updated)
    for edge in sorted(edge_points):
        edge_map[edge] = new_index(edge_points[edge])
    for face_index, point in enumerate(face_points):
        face_map[face_index] = new_index(point)
    new_faces = []
    for face_index, face in enumerate(mesh.faces):
        for index, vertex in enumerate(face):
            following = face[(index + 1) % 4]
            previous = face[(index - 1) % 4]
            new_faces.append((
                vertex_map[vertex],
                edge_map[Mesh.edge_key(vertex, following)],
                face_map[face_index],
                edge_map[Mesh.edge_key(vertex, previous)],
            ))
    edge_attributes = {}
    for name, values in mesh.edge_attributes.items():
        children = {}
        for edge, value in values.items():
            if edge not in edge_map:
                continue
            for endpoint in edge:
                children[Mesh.edge_key(vertex_map[endpoint], edge_map[edge])] = value
        edge_attributes[name] = children
    attributes = {}
    unresolved_names = mesh.unresolved_attribute_names()
    for name, values in mesh.attributes.items():
        if name in unresolved_names:
            continue
        domain = mesh.attribute_domain(name)
        if domain == "POINT":
            remapped = [False] * len(new_vertices) if name in {"point_guide", "open_seam", "simplify_on"} else [None] * len(new_vertices)
            for old, new in vertex_map.items():
                remapped[new] = values[old]
            if name not in {"point_guide", "open_seam", "simplify_on"}:
                for edge, new in edge_map.items():
                    remapped[new] = values[edge[0]]
                for face_index, new in face_map.items():
                    remapped[new] = values[mesh.faces[face_index][0]]
            attributes[name] = remapped
        elif domain == "FACE":
            attributes[name] = [values[face_index] for face_index in range(len(mesh.faces)) for _ in range(4)]
    result = Mesh(
        vertices=new_vertices, faces=new_faces,
        lines=[
            child
            for first, second in mesh.lines
            for child in (
                (vertex_map[first], edge_map[Mesh.edge_key(first, second)]),
                (edge_map[Mesh.edge_key(first, second)], vertex_map[second]),
            )
        ],
        uv_name=mesh.uv_name,
        attributes=attributes, edge_attributes=edge_attributes,
        attribute_domains=mesh.resolved_attribute_domains(),
    )
    result.discard_unresolved_attributes(unresolved_names)
    return result


def coons_point(corners, u, v):
    first, second, third, fourth = corners
    bottom = lerp(first, second, u)
    top = lerp(fourth, third, u)
    left = lerp(first, fourth, v)
    right = lerp(second, third, v)
    bilinear = lerp(lerp(first, second, u), lerp(fourth, third, u), v)
    return subtract(add(lerp(bottom, top, v), lerp(left, right, u)), bilinear)


def tessellate(mesh: Mesh, edge_cuts: dict[Edge, int]) -> Mesh:
    if not edge_cuts or all(value <= 1 for value in edge_cuts.values()):
        return mesh.copy()
    vertices = []
    faces = []
    lookup = {}
    edge_attributes = {name: {} for name in mesh.edge_attributes}

    def index_for(point, key):
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(point)
        return lookup[key]

    for face_index, face in enumerate(mesh.faces):
        if len(face) != 4:
            raise ValueError("density tessellation requires quad faces")
        counts = [
            max(1, int(edge_cuts.get(Mesh.edge_key(face[index], face[(index + 1) % 4]), 1)))
            for index in range(4)
        ]
        columns = max(counts[0], counts[2])
        rows = max(counts[1], counts[3])
        grid = {}
        corners = [mesh.vertices[index] for index in face]
        corner_keys = {
            (0, 0): ("vertex", face[0]),
            (columns, 0): ("vertex", face[1]),
            (columns, rows): ("vertex", face[2]),
            (0, rows): ("vertex", face[3]),
        }
        for row in range(rows + 1):
            for column in range(columns + 1):
                key = corner_keys.get((column, row))
                if key is None and row == 0:
                    side, fraction = 0, column / columns
                elif key is None and column == columns:
                    side, fraction = 1, row / rows
                elif key is None and row == rows:
                    side, fraction = 2, 1.0 - column / columns
                elif key is None and column == 0:
                    side, fraction = 3, 1.0 - row / rows
                else:
                    side = None
                if key is None and side is not None:
                    first, second = face[side], face[(side + 1) % 4]
                    if first > second:
                        fraction = 1.0 - fraction
                    key = ("edge", Mesh.edge_key(first, second), round(fraction, 9))
                elif key is None:
                    key = ("face", face_index, column, row)
                grid[column, row] = index_for(coons_point(corners, column / columns, row / rows), key)
        paths = (
            [grid[column, 0] for column in range(columns + 1)],
            [grid[columns, row] for row in range(rows + 1)],
            [grid[column, rows] for column in range(columns, -1, -1)],
            [grid[0, row] for row in range(rows, -1, -1)],
        )
        for side, path in enumerate(paths):
            original_edge = Mesh.edge_key(face[side], face[(side + 1) % 4])
            for first, second in zip(path, path[1:]):
                child_edge = Mesh.edge_key(first, second)
                for name, values in mesh.edge_attributes.items():
                    if original_edge in values:
                        edge_attributes[name][child_edge] = values[original_edge]
        for row in range(rows):
            for column in range(columns):
                faces.append((
                    grid[column, row],
                    grid[column + 1, row],
                    grid[column + 1, row + 1],
                    grid[column, row + 1],
                ))
    return Mesh(vertices=vertices, faces=faces, uv_name=mesh.uv_name, edge_attributes=edge_attributes)


def smooth(mesh: Mesh, iterations: int, factor: float = 0.5, preserve_boundary: bool = True) -> Mesh:
    result = mesh.copy()
    for _ in range(iterations):
        neighbors = {index: set() for index in range(len(result.vertices))}
        for first, second in result.edges():
            neighbors[first].add(second)
            neighbors[second].add(first)
        boundary = {index for edge in result.boundary_edges() for index in edge}
        result.vertices = [
            tuple(vertex) if (preserve_boundary and index in boundary) or not neighbors[index]
            else lerp(vertex, average(result.vertices[other] for other in neighbors[index]), factor)
            for index, vertex in enumerate(result.vertices)
        ]
    return result


def vertex_normals(mesh: Mesh):
    face_normals = []
    for face in mesh.faces:
        points = [mesh.vertices[index] for index in face]
        face_normals.append(normalize(cross(subtract(points[1], points[0]), subtract(points[-1], points[0]))))
    normals = []
    for index in range(len(mesh.vertices)):
        adjacent = [face_normals[face_index] for face_index, face in enumerate(mesh.faces) if index in face]
        normals.append(normalize(average(adjacent)))
    return normals
