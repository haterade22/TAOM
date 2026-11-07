import unittest

import numpy as np

from tools.hair_engine.convert import curves_to_mesh, join_meshes, pin_weights, weld_mesh
from tools.hair_engine.model import Hair, Strand
from tools.hair_engine.profiles import Profile, profile_mesh


def strand(profile_id=0):
    result = Strand(np.column_stack((np.zeros(3), np.zeros(3), -np.linspace(0., 1., 3))))
    result.profile_id = profile_id
    result.uv_region = 1
    return result


class ProfileTests(unittest.TestCase):
    def test_flat_profile_has_ribbon_topology_and_both_uv_contracts(self):
        result = profile_mesh(strand(), Profile("flat", width=0.2))
        self.assertEqual(result.vertices.shape, (6, 3))
        self.assertEqual(result.faces, [(0, 1, 3, 2), (2, 3, 5, 4)])
        self.assertEqual(set(result.uv_layers), {"UVMap", "HairTool_UV"})
        np.testing.assert_allclose(result.uv_layers["HairTool_UV"][:, 1],
                                   [0., 0., 0.5, 0.5, 0.5, 0.5, 1., 1.])
        self.assertTrue(np.all(result.uv_layers["UVMap"][:, 0] >= 0.5))

    def test_round_profile_uses_requested_cross_section(self):
        result = profile_mesh(strand(), Profile("round", width=0.1, resolution=4))
        self.assertEqual(result.vertices.shape, (12, 3))
        self.assertEqual(len(result.faces), 8)
        radii = np.linalg.norm(result.vertices[:4, :2], axis=1)
        np.testing.assert_allclose(radii, 0.05, atol=1e-12)

    def test_curl_profile_bends_cross_section(self):
        flat = profile_mesh(strand(), Profile("flat", width=0.2, resolution=5))
        curled = profile_mesh(strand(), Profile("curl", width=0.2, resolution=5, roundness=1.))
        self.assertGreater(np.max(np.abs(curled.vertices[:, 1])), np.max(np.abs(flat.vertices[:, 1])))

    def test_braid_contains_three_offsets(self):
        result = profile_mesh(strand(), Profile("braid", width=0.1, resolution=2, braid_turns=2.))
        self.assertEqual(result.vertices.shape, (18, 3))
        self.assertGreater(np.max(np.abs(result.vertices[:, 0])), 0.025)

    def test_profile_id_selects_multi_curve_profile(self):
        hair = Hair([strand(1)])
        profiles = {0: Profile("flat", width=0.1), 1: Profile("flat", width=0.4)}
        result = curves_to_mesh(hair, profiles)
        self.assertAlmostEqual(np.ptp(result.mesh.vertices[:2, 0]), 0.4)

    def test_custom_mesh_profile_is_instanced_along_strand(self):
        custom = Profile("mesh", width=0.2, mesh_vertices=np.array([[-1., 0., 0.], [1., 0., 0.]]),
                         mesh_edges=[(0, 1)])
        result = profile_mesh(strand(), custom)
        self.assertEqual(result.vertices.shape, (6, 3))
        self.assertEqual(result.faces, [])


class ConversionTests(unittest.TestCase):
    def test_conversion_does_not_mutate_source(self):
        hair = Hair([strand()])
        before = hair.strands[0].positions.copy()
        result = curves_to_mesh(hair, {0: Profile("flat", width=0.2)})
        np.testing.assert_array_equal(hair.strands[0].positions, before)
        self.assertIsNot(result.source, hair)
        self.assertEqual(result.mesh.vertices.shape, (6, 3))

    def test_weld_preserves_loop_uvs(self):
        hair = Hair([strand(), strand()])
        mesh = curves_to_mesh(hair, {0: Profile("flat", width=0.2)}).mesh
        welded = weld_mesh(mesh, tolerance=1e-6)
        self.assertEqual(len(welded.vertices), 6)
        self.assertEqual(len(welded.loop_vertices), len(mesh.loop_vertices))
        np.testing.assert_array_equal(welded.uv_layers["UVMap"], mesh.uv_layers["UVMap"])

    def test_join_remaps_materials(self):
        first = curves_to_mesh(Hair([strand()]), {0: Profile("flat")}).mesh
        first.materials = ["a"]
        second = curves_to_mesh(Hair([strand()]), {0: Profile("flat")}).mesh
        second.materials = ["b"]
        second.material_indices = np.zeros(len(second.faces), dtype=np.int32)
        joined = join_meshes([first, second])
        self.assertEqual(joined.materials, ["a", "b"])
        self.assertTrue(np.all(joined.material_indices == [0, 0, 1, 1]))

    def test_pin_weights_follow_root_to_tip(self):
        mesh = curves_to_mesh(Hair([strand()]), {0: Profile("flat")}).mesh
        weights = pin_weights(mesh, power=2.)
        self.assertEqual(weights.shape, (6,))
        np.testing.assert_allclose(weights[[0, 1, -2, -1]], [0., 0., 1., 1.])


if __name__ == "__main__":
    unittest.main()
