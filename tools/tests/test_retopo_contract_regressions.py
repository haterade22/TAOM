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
            vertices=[(0, 0, 0), (0, 0, 0), (2, 0, 0), (2, 2, 0), (0, 2, 0), (0, 4, 0), (2, 4, 0)],
            faces=[(0, 2, 3, 4), (1, 4, 5, 6)],
            uv=[(0, 0), (1, 0), (1, 1), (0, 1), (0, 0), (0, 1), (0, 2), (1, 2)],
        )
        merged = engine.merge_output_seams(mesh, 0.5)
        self.assertLess(len(merged.vertices), 7)
        mesh.edge_attributes["bevel_weight_edge"] = {(0, 1): 1.0}
        protected = engine.merge_output_seams(mesh, 0.5)
        self.assertEqual(7, len(protected.vertices))

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

    def test_reference_validity_exceptions_produce_usable_geometry(self):
        flat = engine.flatten_to_patterns(differential.curved_grid())
        initialized = engine.initialize_planes(flat, RetopoSettings())
        self.assertGreater(abs(engine.signed_area(initialized.faces[0], initialized.vertices)), 0.5)
        mirrored = engine.mirror_guides(differential.unit_guide(), axes=("y",))
        self.assertTrue(all(engine.signed_area(face, mirrored.vertices) > 0 for face in mirrored.faces))
        shell = engine.thicken(differential.unit_guide(), -0.05)
        volume = 0.0
        for face in shell.faces:
            first, second, third = (shell.vertices[index] for index in face[:3])
            volume += sum(a * b for a, b in zip(first, engine.cross(second, third))) / 6
        self.assertGreater(volume, 0)

    def test_density_reconciliation_is_independent_of_face_order(self):
        vertices = [(x, y, 0) for y in range(4) for x in (0, 1)]
        faces = [(0, 1, 3, 2), (2, 3, 5, 4), (4, 5, 7, 6)]
        guide = Mesh(vertices=vertices, faces=faces)
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=40, auto_density=False)
        expected = engine.density_edge_counts(guide, settings)
        reversed_guide = Mesh(vertices=vertices, faces=list(reversed(faces)))
        reversed_guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        actual = engine.density_edge_counts(reversed_guide, settings)
        self.assertEqual(expected, actual)
        self.assertTrue(all(actual[edge] == 4 for edge in ((0, 1), (2, 3), (4, 5), (6, 7))))

    def test_self_mirror_uses_pattern_center_and_avoids_duplicate_faces(self):
        guide = Mesh(
            vertices=[(0.6, 0.65, 0), (0.8, 0.65, 0), (0.8, 1, 0), (0.6, 1, 0)],
            faces=[(0, 1, 2, 3)],
        )
        mirrored = engine.mirror_guides(guide, axes=("x",), source=differential.mirror_source())
        self.assertEqual(2, len(mirrored.faces))
        self.assertEqual(2, len({tuple(sorted(mirrored.vertices[index] for index in face)) for face in mirrored.faces}))
        full = engine.mirror_guides(differential.mirror_guide(extra_points=0), axes=("x",), source=differential.mirror_source())
        self.assertEqual(1, len(full.faces))
        partial = Mesh(
            vertices=[(0.6, 0.65, 0), (0.7, 0.65, 0), (0.9, 0.65, 0), (0.9, 1, 0), (0.7, 1, 0), (0.6, 1, 0)],
            faces=[(0, 1, 4, 5), (1, 2, 3, 4)],
        )
        mirrored = engine.mirror_guides(partial, axes=("x",), source=differential.mirror_source())
        self.assertEqual(3, len(mirrored.faces))
        signatures = {
            tuple(sorted(tuple(round(value, 8) for value in mirrored.vertices[index]) for index in face))
            for face in mirrored.faces
        }
        self.assertEqual(3, len(signatures))

    def test_dense_seam_merge_does_not_collapse_output_faces(self):
        source = differential.curved_grid()
        source.vertices = [tuple(value * 0.08 for value in vertex) for vertex in source.vertices]
        result = engine.retopologize(
            source, differential.unit_guide(), RetopoSettings(subdiv_reproject=5, fit_uv=False)
        )
        self.assertEqual(1024, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_separation_uses_face_attributes_after_seam_merge(self):
        source = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (1, 0, 0), (2, 0, 0), (2, 1, 0), (1, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            uv=[(0, 0), (0.4, 0), (0.4, 1), (0, 1), (0.6, 0), (1, 0), (1, 1), (0.6, 1)],
        )
        guide = Mesh(
            vertices=[
                (0, 0, 0), (0.4, 0, 0), (0.4, 1, 0), (0, 1, 0),
                (0.1, 0.5, 0), (0.1002, 0.5, 0), (0.1002, 0.5002, 0), (0.1, 0.5002, 0),
                (0.6, 0, 0), (1, 0, 0), (1, 1, 0), (0.6, 1, 0),
            ],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7), (8, 9, 10, 11)],
        )
        result = engine.retopologize(
            source, guide,
            RetopoSettings(fit_uv=False, separate_parts=True, separate_patterns=False),
        )
        self.assertEqual(3, len(result.connected_components()))
        self.assertEqual([0, 0, 1], result.attributes["part_id"])

    def test_bevel_weight_protection_survives_subdivision(self):
        source = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (1, 0, 0), (2, 0, 0), (2, 1, 0), (1, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            uv=[(0, 0), (0.4, 0), (0.4, 1), (0, 1), (0.6, 0), (1, 0), (1, 1), (0.6, 1)],
        )
        guide = Mesh(
            vertices=[(0, 0, 0), (0.4, 0, 0), (0.4, 1, 0), (0, 1, 0), (0.6, 0, 0), (1, 0, 0), (1, 1, 0), (0.6, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        )
        guide.edge_attributes["bevel_weight_edge"] = {(1, 2): 1.0, (4, 7): 1.0}
        result = engine.retopologize(
            source, guide,
            RetopoSettings(subdiv_reproject=1, fit_uv=False, separate_patterns=False, separate_parts=False),
        )
        self.assertEqual(2, len(result.connected_components()))
        self.assertTrue(all(len(set(face)) >= 3 for face in result.faces))

    def test_split_edge_repair_preserves_unaffected_face_fans(self):
        mesh = Mesh(
            vertices=[(x, y, 0) for y in range(3) for x in range(3)] + [(1, 0, 1), (0, 0, 1)],
            faces=[(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7), (4, 1, 9, 10)],
        )
        result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([(1, 4)], bad_edges)
        self.assertEqual([], engine.non_manifold_edges(result))
        incidence = sum(
            Mesh.edge_key(face[index], face[(index + 1) % len(face)]) == (4, 5)
            for face in result.faces for index in range(len(face))
        )
        self.assertEqual(2, incidence)

    def test_split_edge_repair_keeps_each_split_fan_connected(self):
        mesh = Mesh(
            vertices=[(index, 0, 0) for index in range(12)],
            faces=[(0, 1, 3, 2), (1, 0, 4, 5), (0, 2, 10, 4), (0, 1, 7, 6), (1, 0, 8, 9), (0, 6, 11, 8)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        for first, second in ((3, 4), (3, 5), (4, 5)):
            self.assertTrue(set(result.faces[first]) & set(result.faces[second]), (first, second))

    def test_split_edge_repair_splits_even_when_all_fans_are_connected(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, -1, 0)],
            faces=[(0, 1, 2), (1, 0, 3), (0, 1, 4), (0, 2, 3), (1, 3, 4)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertNotEqual(mesh.faces, result.faces)

    def test_simplification_preserves_remapped_point_guides(self):
        source = differential.curved_grid()
        source.attributes["point_guide"] = [index == 8 for index in range(9)]
        result = engine.flatten_to_patterns(source, simplify=True, merge_distance=0.51)
        self.assertEqual(5, len(result.vertices))
        self.assertEqual(4, result.attributes["source_vertex"].index(8))
        self.assertEqual(4, result.attributes["point_guide"].index(True))

    def test_empty_guides_are_rejected_without_initialization(self):
        with self.assertRaisesRegex(ValueError, "at least one quad"):
            engine.retopologize(differential.curved_grid(), Mesh(vertices=[], faces=[]))
        report = engine.diagnose(Mesh(vertices=[], faces=[]), RetopoSettings(), differential.curved_grid())
        self.assertEqual("error", report["status"])
        self.assertIn("empty_guide", report["errors"])

    def test_staged_reports_use_output_specific_names(self):
        from pathlib import Path
        from tools.retopo_engine.staging import report_path_for_output

        first = report_path_for_output(Path("staging/first.blend"))
        second = report_path_for_output(Path("staging/second.blend"))
        self.assertNotEqual(first, second)
        self.assertEqual(Path("staging/first-retopo-report.json"), first)

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
