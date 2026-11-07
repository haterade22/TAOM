import unittest

import numpy as np

from tools.hair_engine import curves


class InterpolationTests(unittest.TestCase):
    def test_catmull_rom_includes_endpoints_and_segment_samples(self):
        points = np.array([[0., 0., 0.], [1., 0., 0.], [2., 1., 0.]])
        result = curves.catmull_rom(points, samples_per_segment=4)
        np.testing.assert_allclose(result[0], points[0])
        np.testing.assert_allclose(result[-1], points[-1])
        self.assertEqual(len(result), 9)
        np.testing.assert_allclose(result[4], points[1])

    def test_centripetal_handles_repeated_control_points(self):
        points = np.array([[0., 0., 0.], [0., 0., 0.], [1., 0., 0.], [1., 0., 0.]])
        result = curves.catmull_rom(points, 8, centripetal=True)
        self.assertTrue(np.isfinite(result).all())
        np.testing.assert_allclose(result[0], points[0])
        np.testing.assert_allclose(result[-1], points[-1])

    def test_rejects_too_few_points(self):
        with self.assertRaisesRegex(ValueError, "at least two"):
            curves.catmull_rom(np.zeros((1, 3)))

    def test_arc_length_resample_is_uniform(self):
        points = np.array([[0., 0., 0.], [1., 0., 0.], [1., 2., 0.]])
        result = curves.resample_arc_length(points, 7)
        expected = np.array([0., 0.5, 1., 1., 1., 1., 1.])
        np.testing.assert_allclose(result[:, 0], expected)
        np.testing.assert_allclose(result[:, 1], [0., 0., 0., 0.5, 1., 1.5, 2.])

    def test_arc_length_resample_preserves_duplicate_free_endpoints(self):
        points = np.array([[0., 0., 0.], [1., 0., 0.], [1., 0., 0.], [2., 0., 0.]])
        result = curves.resample_arc_length(points, 5)
        np.testing.assert_allclose(result[:, 0], [0., 0.5, 1., 1.5, 2.])

    def test_rdp_keeps_curvature_and_endpoints(self):
        points = np.array([[0., 0., 0.], [0.5, 0., 0.], [1., 1., 0.], [2., 1., 0.]])
        kept = curves.simplify_rdp(points, 0.4)
        self.assertEqual(kept, [0, 2, 3])

    def test_vw_keeps_significant_triangle(self):
        points = np.array([[0., 0., 0.], [0.5, 0., 0.], [1., 1., 0.], [2., 1., 0.]])
        kept = curves.simplify_vw(points, 0.3)
        self.assertEqual(kept, [0, 2, 3])

    def test_simplification_rejects_empty_input(self):
        with self.assertRaisesRegex(ValueError, "at least two"):
            curves.simplify_rdp(np.zeros((0, 3)), 0.1)


class FrameTests(unittest.TestCase):
    def test_parallel_transport_frames_are_orthonormal(self):
        points = np.array([[0., 0., 0.], [1., 0., 0.], [1., 1., 0.], [1., 1., 1.]])
        tangents, normals, binormals = curves.parallel_transport(points)
        np.testing.assert_allclose(np.sum(tangents * normals, axis=1), 0., atol=1e-12)
        np.testing.assert_allclose(np.sum(tangents * binormals, axis=1), 0., atol=1e-12)
        np.testing.assert_allclose(np.sum(normals * binormals, axis=1), 0., atol=1e-12)
        np.testing.assert_allclose(np.linalg.norm(tangents, axis=1), 1., atol=1e-12)
        np.testing.assert_allclose(np.linalg.norm(normals, axis=1), 1., atol=1e-12)
        np.testing.assert_allclose(np.linalg.norm(binormals, axis=1), 1., atol=1e-12)

    def test_parallel_transport_rejects_zero_length_segment(self):
        with self.assertRaisesRegex(ValueError, "zero-length"):
            curves.parallel_transport(np.array([[0., 0., 0.], [0., 0., 0.]]))

    def test_sort_strands_by_guide_is_deterministic(self):
        strands = [
            np.array([[2., 0., 0.], [2., 1., 0.]]),
            np.array([[0., 0., 0.], [0., 1., 0.]]),
            np.array([[1., 0., 0.], [1., 1., 0.]]),
        ]
        order = curves.sort_by_guide(strands, np.array([[0., 0., 0.], [3., 0., 0.]]))
        self.assertEqual(order, [1, 2, 0])


if __name__ == "__main__":
    unittest.main()
