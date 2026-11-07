import unittest

import numpy as np

from tools.hair_engine import grooming, rigging
from tools.hair_engine.model import Hair, Strand


def line(z=0.):
    return Strand(np.column_stack((np.linspace(0., 1., 3), np.zeros(3), np.full(3, z))))


def surface():
    return (np.array([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.], [1., 1., 0.]]),
            np.array([[0, 1, 2], [1, 3, 2]], dtype=np.int32))


class AttachmentTests(unittest.TestCase):
    def test_attach_projects_onto_surface_and_preserves_source(self):
        strand = line(1.)
        vertices, faces = surface()
        result = grooming.attach_to_surface(strand, vertices, faces)
        np.testing.assert_allclose(result.positions[:, 2], 0., atol=1e-12)
        np.testing.assert_allclose(strand.positions[:, 2], 1.)

    def test_partial_attachment_blends_positions(self):
        strand = line(1.)
        vertices, faces = surface()
        result = grooming.attach_to_surface(strand, vertices, faces, strength=0.25)
        np.testing.assert_allclose(result.positions[:, 2], 0.75, atol=1e-12)

    def test_grid_deform_falls_off_and_preserves_far_points(self):
        hair = Hair([line()])
        controls = [(np.array([0., 0., 0.]), np.array([0., 1., 0.]))]
        result = grooming.grid_deform(hair, controls, radius=0.6)
        np.testing.assert_allclose(result.strands[0].positions[0], [0., 1., 0.])
        self.assertLess(result.strands[0].positions[1, 1], 1.)
        np.testing.assert_allclose(result.strands[0].positions[2], [1., 0., 0.])

    def test_shape_transfer_and_application(self):
        source = Hair([line()])
        target = Hair([line(1.)])
        shapes = rigging.shape_transfer(source, target)
        self.assertEqual(shapes["Basis"].shape, source.strands[0].positions.shape)
        np.testing.assert_allclose(shapes["Basis"], source.strands[0].positions)
        target_shapes = rigging.shape_transfer(target, target)
        restored = rigging.apply_shape(target, target_shapes, "Basis", weight=1.)
        np.testing.assert_allclose(restored.strands[0].positions, target.strands[0].positions)


class WeightTests(unittest.TestCase):
    def test_root_weight_sampling_interpolates_barycentrically(self):
        vertices, faces = surface()
        weights = [
            {"head": 1.0},
            {"head": 1.0},
            {"neck": 1.0},
            {"neck": 1.0},
        ]
        points = np.array([[0.25, 0.25, 1.], [0.75, 0.75, 1.]])
        result = rigging.sample_surface_weights(vertices, faces, weights, points)
        self.assertAlmostEqual(result[0]["head"], 0.75)
        self.assertAlmostEqual(result[0]["neck"], 0.25)
        self.assertAlmostEqual(result[1]["head"], 0.25)
        self.assertAlmostEqual(result[1]["neck"], 0.75)

    def test_weight_sampling_validates_rows(self):
        vertices, faces = surface()
        with self.assertRaisesRegex(ValueError, "weights"):
            rigging.sample_surface_weights(vertices, faces, [], np.zeros((1, 3)))


if __name__ == "__main__":
    unittest.main()
