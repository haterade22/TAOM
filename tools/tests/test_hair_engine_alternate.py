import unittest

import numpy as np

from tools.hair_engine import particle, shell_hair, short_hair
from tools.hair_engine.mesh import MeshData
from tools.hair_engine.model import Hair, Strand
from tools.hair_engine.profiles import Profile


def triangle():
    return np.array([[0., 0., 0.], [1., 0., 0.], [0., 1., 0.]]), np.array([[0, 1, 2]])


def line(x=0.):
    return Strand(np.column_stack((np.full(3, x), np.zeros(3), -np.linspace(0., 1., 3))))


class ShortHairTests(unittest.TestCase):
    def test_keep_preserves_topology(self):
        vertices, faces = triangle()
        mesh = short_hair.rebuild_topology(vertices, faces, "KEEP")
        np.testing.assert_array_equal(mesh.vertices, vertices)
        self.assertEqual(mesh.faces, [(0, 1, 2)])

    def test_diamond_builds_three_quads_per_triangle(self):
        vertices, faces = triangle()
        mesh = short_hair.rebuild_topology(vertices, faces, "DIAMOND")
        self.assertEqual(len(mesh.faces), 3)
        self.assertTrue(all(len(face) == 4 for face in mesh.faces))
        self.assertEqual(len(mesh.vertices), 7)

    def test_diamond_tris_doubles_quad_count(self):
        vertices, faces = triangle()
        mesh = short_hair.rebuild_topology(vertices, faces, "DIAMOND_TRIS")
        self.assertEqual(len(mesh.faces), 6)
        self.assertTrue(all(len(face) == 3 for face in mesh.faces))

    def test_cells_builds_center_fan(self):
        vertices, faces = triangle()
        mesh = short_hair.rebuild_topology(vertices, faces, "CELLS_TRIS")
        self.assertEqual(mesh.faces, [(0, 1, 3), (1, 2, 3), (2, 0, 3)])

    def test_short_hair_has_required_uvs_and_material(self):
        vertices, faces = triangle()
        result = short_hair.generate(vertices, faces, "CELLS_TRIS", count=3,
                                     length=0.1, resolution=3, seed=2,
                                     profile=Profile("flat", width=0.05))
        self.assertEqual(set(result.mesh.uv_layers), {"UVMap", "HairTool_UV", "SampledUV"})
        self.assertEqual(result.mesh.materials, ["ShortHairMaterial"])
        self.assertGreater(len(result.mesh.vertices), 0)

    def test_unknown_topology_fails(self):
        vertices, faces = triangle()
        with self.assertRaisesRegex(ValueError, "topology"):
            short_hair.rebuild_topology(vertices, faces, "OTHER")


class ShellHairTests(unittest.TestCase):
    def test_shell_layers_are_complete_and_independent(self):
        vertices, faces = triangle()
        mesh = MeshData(vertices, faces)
        shells = shell_hair.generate(mesh, layers=12, spacing=0.01)
        self.assertEqual(len(shells), 12)
        self.assertEqual(len(shells[0].vertices), 3)
        shells[0].vertices[0, 2] = 9.
        self.assertEqual(shells[1].vertices[0, 2], 0.01)
        self.assertIsNot(shells[0].vertices, shells[1].vertices)

    def test_joined_shells_have_layer_attributes_and_unique_data(self):
        vertices, faces = triangle()
        shells = shell_hair.generate(MeshData(vertices, faces), layers=3, spacing=0.02)
        joined = shell_hair.join_shells(shells)
        self.assertEqual(len(joined.vertices), 9)
        self.assertEqual(len(joined.faces), 3)
        np.testing.assert_array_equal(joined.point_attributes["shell_layer"], [0]*3 + [1]*3 + [2]*3)
        self.assertEqual(joined.materials, ["ShellHairMaterial"])

    def test_shell_layer_count_is_validated(self):
        vertices, faces = triangle()
        with self.assertRaisesRegex(ValueError, "layers"):
            shell_hair.generate(MeshData(vertices, faces), layers=0)


class ParticleTests(unittest.TestCase):
    def test_particle_round_trip_preserves_positions_and_metadata(self):
        hair = Hair([line()])
        hair.strands[0].group_id = 5
        particles = particle.curves_to_particles(hair)
        restored = particle.particles_to_curves(particles)
        np.testing.assert_array_equal(restored.strands[0].positions, hair.strands[0].positions)
        self.assertEqual(restored.strands[0].group_id, 5)

    def test_scale_is_root_anchored(self):
        particles = [line().positions]
        scaled = particle.scale_particles(particles, 0.5)
        np.testing.assert_allclose(scaled[0][:, 2], [0., -0.25, -0.5])

    def test_particle_conversion_copies_input(self):
        positions = line().positions
        hair = particle.particles_to_curves([positions])
        hair.strands[0].positions[0, 0] = 9.
        self.assertEqual(positions[0, 0], 0.)


if __name__ == "__main__":
    unittest.main()
