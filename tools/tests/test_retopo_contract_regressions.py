import math
import unittest

from tools.retopo_engine import Mesh, RetopoSettings, differential, engine


class ContractRegressionTests(unittest.TestCase):
    def test_weld_preserves_faces_and_never_duplicates_a_corner(self):
        result = engine.flatten_to_patterns(differential.curved_grid(), simplify=True, merge_distance=0.01)
        self.assertEqual(9, len(result.vertices))
        self.assertEqual(4, len(result.faces))
        self.assertTrue(all(len(set(face)) >= 3 for face in result.faces))
        welded = engine.weld(differential.unit_guide(), 0.5)
        self.assertEqual(1, len(welded.faces))
        self.assertTrue(all(len(set(face)) >= 3 for face in welded.faces))

    def test_initialization_orders_corners_around_the_boundary(self):
        for source in (differential.hexagon(),):
            result = engine.initialize_planes(source, RetopoSettings())
            self.assertEqual(1, len(result.faces))
            self.assertGreater(abs(engine.signed_area(result.faces[0], result.vertices)), 0.01)

    def test_ring_initialization_preserves_hole_with_non_quad_inputs(self):
        source = differential.ring_guide()
        source.faces = [face[:3] for face in source.faces]
        result = engine.initialize_planes(source, RetopoSettings())
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        self.assertEqual(16, len(result.boundary_edges()))

    def test_initialization_keeps_disconnected_triangulated_patterns_separate(self):
        source = Mesh(
            vertices=[
                (0, 0, 0), (1, 0, 0), (0, 1, 0),
                (3, 0, 0), (4, 0, 0), (3, 1, 0),
            ],
            faces=[(0, 1, 2), (3, 4, 5)],
        )
        result = engine.initialize_planes(source, RetopoSettings())
        self.assertEqual(2, len(result.connected_components()))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_seam_merge_joins_distinct_uv_islands_and_preserves_bevel_weight(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (0, 0, 0), (1, 0, 0), (1, 1, 0)],
            faces=[(0, 2, 3, 1)],
            uv=[(0, 0), (1, 0), (1, 1), (0, 1)],
        )
        merged = engine.merge_output_seams(mesh, 0.5)
        self.assertLess(len(merged.vertices), 4)
        mesh.edge_attributes["bevel_weight_edge"] = {(0, 1): 1.0}
        protected = engine.merge_output_seams(mesh, 0.5)
        self.assertEqual(4, len(protected.vertices))

    def test_thickness_preserves_metadata_uv_and_outward_winding(self):
        guide = differential.unit_guide()
        guide.uv = [(0, 0), (1, 0), (1, 1), (0, 1)]
        guide.attributes["pattern_id"] = [2]
        guide.attributes["part_id"] = [3]
        guide.attributes["retopo_plane_id"] = [4]
        result = engine.thicken(guide, 0.05, 0.0)
        self.assertEqual([2] * 6, result.attributes["pattern_id"])
        self.assertEqual([3] * 6, result.attributes["part_id"])
        self.assertEqual([4] * 6, result.attributes["retopo_plane_id"])
        self.assertEqual(list(reversed(guide.uv)) + guide.uv, result.uv[:8])
        self.assertEqual([-0.025] * 4 + [0.025] * 4, [vertex[2] for vertex in result.vertices])
        self.assertEqual(6, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_smooth_lowpoly_relaxes_3d_without_requiring_uv_smoothing(self):
        guide = Mesh(
            vertices=[(x * 0.5, y * 0.5, 0.0) for y in range(3) for x in range(3)],
            faces=[(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7)],
        )
        guide.vertices[4] = (0.4, 0.4, 0.0)
        settings = RetopoSettings(smooth_low_poly=1, smooth_uvs=False, fit_uv=False)
        relaxed = engine.retopologize(differential.curved_grid(), guide, settings)
        settings.smooth_low_poly = 0
        unrelaxed = engine.retopologize(differential.curved_grid(), guide, settings)
        self.assertEqual(relaxed.uv, unrelaxed.uv)
        self.assertNotEqual(relaxed.vertices, unrelaxed.vertices)

    def test_sharp_threshold_changes_density(self):
        guide = differential.two_guide()
        guide.edge_attributes["sharp_edge"] = {(0, 1): True}
        low = engine.apply_density(guide, RetopoSettings(base_density=100, density_add_threshold=0.1))
        high = engine.apply_density(guide, RetopoSettings(base_density=100, density_add_threshold=0.9))
        self.assertNotEqual(len(low.faces), len(high.faces))

    def test_preview_guides_expose_contacts_polyloops_mismatches_and_density_counts(self):
        guide = differential.two_guide()
        self.assertEqual({(1, 4)}, engine.sibling_contact_edges(guide))
        self.assertEqual(guide.edges(), engine.polyloop_guides(guide).lines)
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=20)
        self.assertEqual(2, engine.requested_density_counts(guide, settings)[(0, 1)])
        self.assertEqual({(0, 1), (3, 4)}, engine.mismatch_edges(guide, settings))
        counts = engine.density_edge_counts(guide, settings)
        self.assertEqual(2, counts[(0, 1)])
        self.assertEqual(2, counts[(3, 4)])


if __name__ == "__main__":
    unittest.main()
