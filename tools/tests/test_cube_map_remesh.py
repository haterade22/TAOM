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


class ValidationTests(unittest.TestCase):
    def test_open_mesh_is_rejected(self):
        vertices = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]], dtype=float)
        faces = np.array([[0, 1, 2], [0, 2, 3]], dtype=np.int32)
        with self.assertRaisesRegex(ValueError, "closed"):
            engine.remesh(vertices, faces, target_quads=24)

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
        projected, _ = engine._nearest_points_on_triangles(
            points, vertices[faces], engine.cKDTree(vertices[faces].mean(axis=1)))
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
