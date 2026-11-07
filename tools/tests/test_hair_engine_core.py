import unittest

import numpy as np

from tools.hair_engine import generation, grooming
from tools.hair_engine.model import Hair, Strand


def surface():
    vertices = np.array([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.], [1., 1., 0.]])
    faces = np.array([[0, 1, 2], [1, 3, 2]], dtype=np.int32)
    return vertices, faces


def line(x=0., length=1., count=5):
    return Strand(np.column_stack((np.full(count, x), np.zeros(count), np.linspace(0., -length, count))))


class ModelTests(unittest.TestCase):
    def test_strand_defaults_and_copy_are_independent(self):
        strand = line()
        self.assertEqual(strand.radius.shape, (5,))
        self.assertEqual(strand.tilt.shape, (5,))
        clone = strand.copy()
        clone.positions[0, 0] = 9.
        clone.tags = frozenset({"beard"})
        self.assertEqual(strand.positions[0, 0], 0.)
        self.assertEqual(strand.tags, frozenset())

    def test_strand_rejects_attribute_length_mismatch(self):
        with self.assertRaisesRegex(ValueError, "point attribute"):
            Strand(line().positions, point_attributes={"mask": np.ones(2)})

    def test_hair_copy_preserves_metadata(self):
        hair = Hair([line()])
        hair.strands[0].profile_id = 3
        hair.strands[0].uv_region = 2
        clone = hair.copy()
        self.assertEqual(clone.strands[0].profile_id, 3)
        self.assertEqual(clone.strands[0].uv_region, 2)
        self.assertIsNot(clone.strands[0], hair.strands[0])

    def test_resample_interpolates_radius_tilt_and_point_attributes(self):
        strand = line(count=2)
        strand.radius = np.array([1., 0.])
        strand.tilt = np.array([0., 1.])
        strand.point_attributes = {"mask": np.array([0., 1.])}
        result = strand.resampled(3)
        np.testing.assert_allclose(result.radius, [1., 0.5, 0.])
        np.testing.assert_allclose(result.tilt, [0., 0.5, 1.])
        np.testing.assert_allclose(result.point_attributes["mask"], [0., 0.5, 1.])


class GenerationTests(unittest.TestCase):
    def test_surface_distribution_is_seeded_on_surface_and_masked(self):
        vertices, faces = surface()
        first = generation.distribute_roots(vertices, faces, 12, seed=7)
        second = generation.distribute_roots(vertices, faces, 12, seed=7)
        roots, normals, triangle_ids, barycentrics = first
        np.testing.assert_allclose(roots, second[0])
        np.testing.assert_allclose(normals, [[0., 0., 1.]] * 12)
        np.testing.assert_allclose(roots[:, 2], 0.)
        np.testing.assert_allclose(barycentrics.sum(axis=1), 1.)
        np.testing.assert_array_equal(triangle_ids, second[2])
        masked = generation.distribute_roots(vertices, faces, 3, seed=7, face_mask=np.array([True, False]))
        self.assertTrue(np.all(masked[2] == 0))

    def test_distribution_rejects_empty_mask(self):
        vertices, faces = surface()
        with self.assertRaisesRegex(ValueError, "at least one"):
            generation.distribute_roots(vertices, faces, 3, face_mask=np.zeros(2, dtype=bool))

    def test_surface_guides_drape_and_are_deterministic(self):
        vertices, faces = surface()
        first = generation.surface_guides(vertices, faces, 4, length=0.5, resolution=5, seed=4)
        second = generation.surface_guides(vertices, faces, 4, length=0.5, resolution=5, seed=4)
        self.assertEqual(len(first), 4)
        np.testing.assert_allclose(first[0].positions, second[0].positions)
        self.assertTrue(np.all(first[0].positions[:, 2] <= 0.003 + 1e-12))
        self.assertAlmostEqual(first[0].positions[-1, 2], -0.497)

    def test_interpolated_guides_hold_parent_endpoints(self):
        guides = [line(0.), line(1.)]
        children = generation.interpolate_guides(guides, 2, resolution=5, seed=3)
        self.assertEqual(len(children), 2)
        np.testing.assert_allclose(children[0].positions[:, 0], 1/3)
        np.testing.assert_allclose(children[1].positions[:, 0], 2/3)

    def test_uv_grid_selects_expected_cell(self):
        vertices, faces = surface()
        uv = np.array([[0., 0.], [1., 0.], [0., 1.], [1., 1.]])
        guides = generation.uv_grid_guides(vertices, faces, uv, cells=2, count=4, length=0.1, resolution=3, seed=1)
        self.assertEqual(len(guides), 4)
        for strand in guides:
            self.assertLessEqual(strand.uv_region, 3)
            self.assertEqual(len(strand.positions), 3)

    def test_border_generator_creates_rooted_guides(self):
        vertices, faces = surface()
        guides = generation.border_guides(vertices, faces, length=0.2, resolution=3)
        self.assertEqual(len(guides), 4)
        for strand in guides:
            self.assertEqual(len(strand.positions), 3)
            self.assertAlmostEqual(strand.positions[-1, 2], -0.197)


class GroomingTests(unittest.TestCase):
    def test_length_and_trim_preserve_root(self):
        strand = line()
        longer = grooming.adjust_length(strand, 2.)
        np.testing.assert_allclose(longer.positions[:, 2], [0., -0.5, -1., -1.5, -2.])
        trimmed = grooming.trim(strand, 0.5)
        np.testing.assert_allclose(trimmed.positions[:, 2], [0., -0.125, -0.25, -0.375, -0.5])

    def test_taper_and_twist_preserve_metadata(self):
        strand = line()
        strand.profile_id = 4
        tapered = grooming.taper(strand, root=1., tip=0., power=2.)
        np.testing.assert_allclose(tapered.radius, [1., 0.9375, 0.75, 0.4375, 0.])
        twisted = grooming.twist(tapered, np.pi)
        np.testing.assert_allclose(twisted.tilt, np.linspace(0., np.pi, 5))
        self.assertEqual(twisted.profile_id, 4)

    def test_smooth_preserves_root_and_tip(self):
        strand = Strand(np.array([[0., 0., 0.], [1., 0., -1.], [0., 0., -2.]]))
        result = grooming.smooth(strand, iterations=1, factor=1.)
        np.testing.assert_allclose(result.positions[0], strand.positions[0])
        np.testing.assert_allclose(result.positions[-1], strand.positions[-1])
        np.testing.assert_allclose(result.positions[1], [0., 0., -1.])

    def test_curl_preserves_root_and_tip(self):
        result = grooming.curl(line(), radius=0.2, turns=2., phase=0.)
        np.testing.assert_allclose(result.positions[0], [0., 0., 0.], atol=1e-12)
        np.testing.assert_allclose(result.positions[-1], [0., 0., -1.], atol=1e-12)
        self.assertGreater(np.max(np.abs(result.positions[:, :2])), 0.1)

    def test_noise_is_seeded_and_zero_at_endpoints(self):
        first = grooming.noise(line(), amplitude=0.1, frequency=3., seed=8)
        second = grooming.noise(line(), amplitude=0.1, frequency=3., seed=8)
        np.testing.assert_allclose(first.positions, second.positions)
        np.testing.assert_allclose(first.positions[[0, -1]], line().positions[[0, -1]], atol=1e-12)

    def test_clump_moves_children_toward_nearest_guide(self):
        guide = line(0.)
        child = line(1.)
        result = grooming.clump([guide, child], guide_indices=[0], strength=0.5)[1]
        np.testing.assert_allclose(result.positions[:, 0], 0.5)

    def test_mirror_and_selection(self):
        hair = Hair([line(-1.), line(2.)])
        hair.strands[0].group_id = 2
        hair.strands[0].tags = frozenset({"beard"})
        mirrored = grooming.mirror(hair, axis=0)
        np.testing.assert_allclose(mirrored.strands[0].positions[:, 0], 1.)
        selected = grooming.select(hair, groups={2}, tags={"beard"})
        self.assertEqual(len(selected.strands), 1)
        self.assertEqual(len(grooming.select(hair, groups={1}).strands), 0)


if __name__ == "__main__":
    unittest.main()
