#!/usr/bin/env python3
import importlib.util
import os
import unittest

import numpy as np

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "quad_engine", "cube_map.py")
_spec = importlib.util.spec_from_file_location("cube_map_remesh", SCRIPT)
engine = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(engine)


def cube_mesh():
    vertices = np.array([
        [-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1],
        [-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1],
    ], dtype=float)
    faces = np.array([
        [0, 3, 2], [0, 2, 1], [4, 5, 6], [4, 6, 7],
        [0, 1, 5], [0, 5, 4], [1, 2, 6], [1, 6, 5],
        [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7],
    ], dtype=np.int32)
    materials = np.repeat(np.arange(6, dtype=np.int32), 2)
    return vertices, faces, materials


def octahedron_mesh():
    vertices = np.array([
        [1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1],
    ], dtype=float)
    faces = np.array([
        [0, 2, 4], [2, 1, 4], [1, 3, 4], [3, 0, 4],
        [2, 0, 5], [1, 2, 5], [3, 1, 5], [0, 3, 5],
    ], dtype=np.int32)
    return vertices, faces, np.zeros(len(faces), dtype=np.int32)


def blind_hole_mesh():
    coords = [-1.0, -0.1, 0.1, 1.0]
    vertices, lookup, faces = [], {}, []

    def vid(point):
        key = tuple(round(coordinate, 9) for coordinate in point)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(key)
        return lookup[key]

    def quad(a, b, c, d):
        faces.extend(([a, b, c], [a, c, d]))

    bases = [
        ((1, 0, 0), (0, 1, 0), (0, 0, 1)),
        ((-1, 0, 0), (0, -1, 0), (0, 0, 1)),
        ((0, 1, 0), (-1, 0, 0), (0, 0, 1)),
        ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
        ((0, 0, 1), (1, 0, 0), (0, 1, 0)),
        ((0, 0, -1), (-1, 0, 0), (0, 1, 0)),
    ]
    for normal, u_axis, v_axis in bases:
        normal, u_axis, v_axis = map(np.array, (normal, u_axis, v_axis))
        for j in range(3):
            for i in range(3):
                if normal[0] == 1 and i == 1 and j == 1:
                    continue
                points = [normal + u_axis * x + v_axis * y for x, y in (
                    (coords[i], coords[j]), (coords[i + 1], coords[j]),
                    (coords[i + 1], coords[j + 1]), (coords[i], coords[j + 1]))]
                quad(*(vid(point) for point in points))
    ring = [(-0.1, -0.1), (0.1, -0.1), (0.1, 0.1), (-0.1, 0.1)]
    for index, (y0, z0) in enumerate(ring):
        y1, z1 = ring[(index + 1) % 4]
        quad(vid((1, y0, z0)), vid((0.5, y0, z0)),
             vid((0.5, y1, z1)), vid((1, y1, z1)))
    quad(vid((0.5, -0.1, -0.1)), vid((0.5, -0.1, 0.1)),
         vid((0.5, 0.1, 0.1)), vid((0.5, 0.1, -0.1)))
    return np.asarray(vertices), np.asarray(faces, dtype=np.int32)


class ValidationTests(unittest.TestCase):
    def test_open_mesh_is_rejected(self):
        vertices = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], dtype=float)
        faces = np.array([[0, 1, 2], [0, 2, 3]], dtype=np.int32)
        with self.assertRaisesRegex(ValueError, "closed"):
            engine.remesh(vertices, faces, target_quads=24)

    def test_nested_shells_are_rejected(self):
        outer_vertices, outer_faces, _ = cube_mesh()
        inner_vertices, inner_faces, _ = cube_mesh()
        inner_vertices = inner_vertices * 0.5
        vertices = np.concatenate((outer_vertices, inner_vertices))
        faces = np.concatenate((outer_faces, inner_faces + len(outer_vertices)))
        with self.assertRaisesRegex(ValueError, "star-shaped"):
            engine.remesh(vertices, faces, target_quads=24)

    def test_blind_hole_is_rejected(self):
        vertices, faces = blind_hole_mesh()
        with self.assertRaisesRegex(ValueError, "star-shaped"):
            engine.remesh(vertices, faces, target_quads=24)

    def test_large_convex_mesh_is_not_rejected(self):
        vertices, faces, materials = octahedron_mesh()
        result = engine.remesh(vertices * 1e6, faces, materials, target_quads=24)
        self.assertEqual(result.faces.shape, (24, 4))
        self.assertEqual(result.metrics["boundary_edges"], 0)

    def test_nonfinite_vertices_are_rejected(self):
        vertices, faces, _ = cube_mesh()
        vertices[0, 0] = np.nan
        with self.assertRaisesRegex(ValueError, "finite"):
            engine.remesh(vertices, faces, target_quads=24)

    def test_non_triangle_input_is_rejected(self):
        vertices, faces, _ = cube_mesh()
        with self.assertRaisesRegex(ValueError, "triangles"):
            engine.remesh(vertices, [0, 3, 2, 1], target_quads=24)

    def test_target_must_cover_six_cube_faces(self):
        vertices, faces, _ = cube_mesh()
        with self.assertRaisesRegex(ValueError, "at least 6"):
            engine.remesh(vertices, faces, target_quads=5)

    def test_surface_points_have_zero_projection_error(self):
        vertices, faces, _ = cube_mesh()
        points = np.array([
            [1, -0.25, 0.75], [1, 0.25, 0.25], [-1, 0.2, -0.4],
            [0.3, 1, -0.7], [-0.2, -1, 0.6], [0.5, -0.5, 1],
        ])
        tree = engine.cKDTree(vertices[faces].mean(axis=1)) if engine.cKDTree else None
        projected, _ = engine._nearest_points_on_triangles(points, vertices[faces], tree)
        np.testing.assert_allclose(projected, points, atol=1e-12, rtol=0)


class RemeshTests(unittest.TestCase):
    def test_cube_is_closed_deterministic_and_quad_based(self):
        vertices, faces, materials = cube_mesh()
        first = engine.remesh(vertices, faces, materials, target_quads=96)
        second = engine.remesh(vertices, faces, materials, target_quads=96)
        self.assertEqual(first.vertices.shape, second.vertices.shape)
        np.testing.assert_array_equal(first.vertices, second.vertices)
        np.testing.assert_array_equal(first.faces, second.faces)
        self.assertEqual(first.faces.shape, (96, 4))
        self.assertEqual(first.metrics["boundary_edges"], 0)
        self.assertAlmostEqual(first.metrics["signed_volume"], 8.0)
        self.assertTrue(np.isfinite(first.vertices).all())

    def test_materials_are_projected_from_source_faces(self):
        vertices, faces, materials = cube_mesh()
        result = engine.remesh(vertices, faces, materials, target_quads=24)
        self.assertEqual(result.material_ids.shape, (24,))
        self.assertTrue(np.isin(result.material_ids, np.arange(6)).all())
        self.assertGreater(len(np.unique(result.material_ids)), 2)
        self.assertEqual(result.metrics["boundary_edges"], 0)
        source_normals = np.cross(vertices[faces[:, 1]] - vertices[faces[:, 0]],
                                  vertices[faces[:, 2]] - vertices[faces[:, 0]])
        output_normals = np.cross(result.vertices[result.faces[:, 1]] - result.vertices[result.faces[:, 0]],
                                  result.vertices[result.faces[:, 2]] - result.vertices[result.faces[:, 0]])
        alignment = output_normals @ source_normals.T
        expected_sources = np.argmax(alignment, axis=1)
        np.testing.assert_array_equal(result.material_ids, materials[expected_sources])

    def test_material_nearest_is_not_limited_by_centroid_shortlist(self):
        vertices, faces, materials = cube_mesh()
        refined_vertices = vertices.tolist()
        refined_faces = []
        refined_materials = []

        def split(triangle, depth, material):
            if depth == 0:
                refined_faces.append(triangle)
                refined_materials.append(material)
                return
            a, b, c = triangle
            ab = (np.asarray(refined_vertices[a]) + np.asarray(refined_vertices[b])) / 2
            bc = (np.asarray(refined_vertices[b]) + np.asarray(refined_vertices[c])) / 2
            ca = (np.asarray(refined_vertices[c]) + np.asarray(refined_vertices[a])) / 2
            refined_vertices.extend((ab.tolist(), bc.tolist(), ca.tolist()))
            i = len(refined_vertices) - 3
            for child in ([a, i, i + 2], [i, b, i + 1], [i + 2, i + 1, c], [i, i + 1, i + 2]):
                split(child, depth - 1, material)

        for index, triangle in enumerate(faces):
            split(triangle, 3 if index in (4, 5) else 0, materials[index])
        refined_faces = np.asarray(refined_faces, dtype=np.int32)
        refined_materials = np.asarray(refined_materials, dtype=np.int32)
        triangles = np.asarray(refined_vertices)[refined_faces]
        tree = engine.cKDTree(triangles.mean(axis=1)) if engine.cKDTree else None
        _, source = engine._nearest_points_on_triangles(
            np.array([[0.25, 1.0, 0.25]]), triangles, tree)
        self.assertEqual(refined_materials[source[0]], 4)

    def test_octahedron_metrics_are_finite(self):
        vertices, faces, materials = octahedron_mesh()
        result = engine.remesh(vertices, faces, materials, target_quads=54)
        self.assertEqual(result.faces.shape, (54, 4))
        self.assertEqual(result.metrics["boundary_edges"], 0)
        self.assertGreaterEqual(result.metrics["max_center_error"], 0)
        self.assertGreaterEqual(result.metrics["mean_center_error"], 0)
        self.assertTrue(np.isfinite(result.metrics["max_center_error"]))
        self.assertTrue(np.isfinite(result.metrics["mean_center_error"]))
        self.assertGreater(result.metrics["signed_volume"], 0)

    def test_numpy_only_index_fallback(self):
        vertices, faces, materials = cube_mesh()
        original = engine.cKDTree
        engine.cKDTree = None
        try:
            result = engine.remesh(vertices, faces, materials, target_quads=24)
        finally:
            engine.cKDTree = original
        self.assertEqual(result.faces.shape, (24, 4))
        self.assertEqual(result.metrics["boundary_edges"], 0)

    def test_target_is_rounded_up_to_whole_cube_faces(self):
        vertices, faces, materials = cube_mesh()
        result = engine.remesh(vertices, faces, materials, target_quads=20)
        self.assertEqual(result.faces.shape, (24, 4))


if __name__ == "__main__":
    unittest.main()
