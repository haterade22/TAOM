import math
import unittest

from tools.retopo_engine import Mesh, RetopoSettings
from tools.retopo_engine import engine


def curved_source():
    return Mesh(
        vertices=[
            (-0.5, -0.5, 0.0), (0.0, -0.5, 0.1), (0.5, -0.5, 0.0),
            (-0.5, 0.0, 0.1), (0.0, 0.0, 0.2), (0.5, 0.0, 0.1),
            (-0.5, 0.5, 0.0), (0.0, 0.5, 0.1), (0.5, 0.5, 0.0),
        ],
        faces=[(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7)],
        uv=[
            (0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5),
            (0.5, 0), (1, 0), (1, 0.5), (0.5, 0.5),
            (0, 0.5), (0.5, 0.5), (0.5, 1), (0, 1),
            (0.5, 0.5), (1, 0.5), (1, 1), (0.5, 1),
        ],
        uv_name="UVMap",
    )


def unit_guide():
    return Mesh(
        vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
        faces=[(0, 1, 2, 3)],
    )


def flat_square(size=1.0):
    return Mesh(
        vertices=[(0, 0, 0), (size, 0, 0), (size, size, 0), (0, size, 0)],
        faces=[(0, 1, 2, 3)],
    )


def ring_guide():
    vertices = []
    for radius in (1.0, 0.5):
        for index in range(8):
            angle = math.tau * index / 8
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), 0))
    faces = []
    for index in range(8):
        following = (index + 1) % 8
        faces.append((index, following, 8 + following, 8 + index))
    return Mesh(vertices=vertices, faces=faces)


class FlattenTests(unittest.TestCase):
    def test_flatten_preserves_grid_uv_and_source_attributes(self):
        result = engine.flatten_to_patterns(curved_source())
        self.assertEqual(9, len(result.vertices))
        self.assertEqual(4, len(result.faces))
        self.assertEqual("UVMap", result.uv_name)
        self.assertEqual(16, len(result.uv))
        self.assertIn((0.0, 0.0, 0.2), result.attributes["source_position"])
        self.assertIn("source_vertex", result.attributes)
        self.assertIn("source_part", result.attributes)
        self.assertIn("pattern_id", result.attributes)
        self.assertIn("point_guide", result.attributes)
        self.assertEqual(4, sum(result.attributes["point_guide"]))
        self.assertEqual(1, len(engine.initialize_planes(result, RetopoSettings()).faces))
        self.assertIn("pattern_center", result.attributes)
        self.assertIn("open_seam", result.attributes)
        self.assertIn("part_border", result.edge_attributes)

    def test_flatten_does_not_use_source_id_attributes_for_internal_ids(self):
        source = curved_source()
        source.attributes["id"] = [99] * 9
        source.attributes["ID"] = [98] * 9
        result = engine.flatten_to_patterns(source)
        self.assertEqual(9, len(set(result.attributes["source_vertex"])))
        self.assertNotEqual([99] * 9, result.attributes["source_vertex"])

    def test_simplification_merges_only_within_requested_distance(self):
        source = curved_source()
        merged = engine.flatten_to_patterns(source, simplify=True, merge_distance=0.51)
        untouched = engine.flatten_to_patterns(source, simplify=True, merge_distance=0.01)
        self.assertLess(len(merged.vertices), 9)
        self.assertEqual(9, len(untouched.vertices))
        self.assertEqual(9, len(curved_source().vertices))

    def test_missing_uv_map_is_an_actionable_error(self):
        source = curved_source()
        source.uv_name = "Other"
        with self.assertRaisesRegex(ValueError, "UVMap"):
            engine.flatten_to_patterns(source, uv_name="UVMap")


class GuideTests(unittest.TestCase):
    def test_non_quad_guides_are_rejected_with_face_index(self):
        guide = Mesh(vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0)], faces=[(0, 1, 2)])
        with self.assertRaisesRegex(ValueError, "face 0.*3"):
            engine.validate_guides(guide)

    def test_initialize_square_returns_one_editable_quad(self):
        result = engine.initialize_planes(flat_square(), RetopoSettings())
        self.assertEqual(4, len(result.vertices))
        self.assertEqual([(0, 1, 2, 3)], result.faces)

    def test_initialize_more_than_four_corners_produces_only_quads(self):
        vertices = [
            (math.cos(math.tau * i / 6), math.sin(math.tau * i / 6), 0)
            for i in range(6)
        ]
        result = engine.initialize_planes(Mesh(vertices=vertices, faces=[tuple(range(6))]), RetopoSettings())
        self.assertTrue(result.faces)
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_corner_thresholds_change_generated_plane_layout(self):
        vertices = [
            (math.cos(math.tau * i / 6), math.sin(math.tau * i / 6), 0)
            for i in range(6)
        ]
        flat = Mesh(vertices=vertices, faces=[tuple(range(6))])
        narrow = engine.initialize_planes(flat, RetopoSettings(angle_threshold=1.0, corners_threshold=0.1))
        wide = engine.initialize_planes(flat, RetopoSettings(angle_threshold=1.0, corners_threshold=0.5))
        self.assertEqual(1, len(narrow.faces))
        self.assertEqual(6, len(wide.faces))

    def test_initialize_ring_preserves_hole(self):
        result = engine.initialize_planes(ring_guide(), RetopoSettings())
        self.assertEqual(8, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        self.assertEqual(16, len(result.boundary_edges()))

    def test_point_guides_override_generated_corners(self):
        flat = flat_square()
        flat.attributes["point_guide"] = [False, True, False, False]
        result = engine.initialize_planes(flat, RetopoSettings(angle_threshold=0.0))
        self.assertIn(flat.vertices[1], result.vertices)

    def test_mirror_adds_geometry_and_restores_outward_winding(self):
        result = engine.mirror_guides(unit_guide(), axes=("x",))
        self.assertEqual(2, len(result.faces))
        self.assertEqual(6, len(result.vertices))
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        self.assertTrue(all(math.isclose(vertex[2], 0) for vertex in result.vertices))

    def test_auto_fill_is_bounded_and_inserts_requested_point(self):
        unchanged = engine.auto_fill(unit_guide(), [(0.5, 0.5, 0)], max_iterations=0)
        self.assertEqual(1, len(unchanged.faces))
        result = engine.auto_fill(unit_guide(), [(0.5, 0.5, 0)], max_iterations=1)
        self.assertEqual(4, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        self.assertTrue(any(math.dist(vertex, (0.5, 0.5, 0)) < 1e-6 for vertex in result.vertices))


class DensityTests(unittest.TestCase):
    def test_crease_adds_cuts_without_ngons(self):
        guide = unit_guide()
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=20, full_crease_control=False)
        result = engine.apply_density(guide, settings)
        self.assertEqual(2, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_full_crease_neutral_value_does_not_subdivide(self):
        guide = unit_guide()
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=4, full_crease_control=True)
        self.assertEqual(1, len(engine.apply_density(guide, settings).faces))

    def test_density_controls_match_measured_reference_contract(self):
        low = engine.apply_density(unit_guide(), RetopoSettings(base_density=25))
        high = engine.apply_density(unit_guide(), RetopoSettings(base_density=100))
        self.assertEqual(len(low.faces), len(high.faces))
        guide = unit_guide()
        results = {}
        for value in (0.25, 0.5, 0.75, 1.0):
            guide.edge_attributes["crease_edge"] = {edge: value for edge in guide.edges()}
            results[value] = len(engine.apply_density(guide, RetopoSettings(base_density=100, full_crease_control=True)).faces)
        self.assertLess(results[0.25], results[0.5])
        self.assertEqual(results[0.5], results[0.75])
        self.assertEqual(results[0.75], results[1.0])

    def test_sharp_edge_adds_configured_cuts(self):
        guide = unit_guide()
        guide.edge_attributes["sharp_edge"] = {(0, 1): True}
        settings = RetopoSettings(base_density=100, density_add=2, density_add_threshold=0.7)
        self.assertGreater(len(engine.apply_density(guide, settings).faces), 1)

    def test_neutralize_crease_sets_every_selected_edge_to_half(self):
        guide = unit_guide()
        guide.edge_attributes["crease_edge"] = {(0, 1): 1.0}
        result = engine.neutralize_crease(guide, [(0, 1)])
        self.assertEqual(0.5, result.edge_attributes["crease_edge"][(0, 1)])
        self.assertEqual(1.0, guide.edge_attributes["crease_edge"][(0, 1)])

    def test_snapping_moves_to_border_but_never_welds_guides(self):
        guide = Mesh(
            vertices=[(0.49, 0.49, 0), (0.5, 0.5, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            faces=[(0, 2, 3, 4)],
        )
        result = engine.snap_guides_to_pattern(guide, flat_square(size=0.5), 0.02)
        self.assertEqual(5, len(result.vertices))
        self.assertTrue(any(math.dist(vertex, (0.5, 0.49, 0)) < 1e-6 for vertex in result.vertices))
        self.assertTrue(any(math.dist(vertex, (0.5, 0.5, 0)) < 1e-6 for vertex in result.vertices))

    def test_auto_density_propagates_only_through_connected_quad_flow(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (2, 0, 0), (0, 1, 0), (1, 1, 0), (2, 1, 0), (4, 0, 0), (5, 0, 0), (5, 1, 0), (4, 1, 0)],
            faces=[(0, 1, 4, 3), (1, 2, 5, 4), (6, 7, 8, 9)],
        )
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=20, auto_density=False)
        local = engine.apply_density(guide, settings)
        settings.auto_density = True
        connected = engine.apply_density(guide, settings)
        self.assertEqual(4, len(local.faces))
        self.assertEqual(5, len(connected.faces))
        self.assertEqual(1, len(connected.connected_components()[1]))

    def test_prepass_does_not_subdivide_unrelated_patterns(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (3, 0, 0), (4, 0, 0), (4, 1, 0), (3, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        )
        result = engine.auto_fill(guide, [(0.5, 0.5, 0)], max_iterations=1, across_patterns=False)
        self.assertEqual(5, len(result.faces))
        self.assertEqual([4, 1], [len(component) for component in result.connected_components()])

    def test_bevel_weight_keeps_seam_vertices_unmerged(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (0, 0, 0), (2, 0, 0), (2, 2, 0)],
            faces=[(0, 2, 3, 1)],
            uv=[(0, 0)] * 4,
        )
        mesh.edge_attributes["bevel_weight_edge"] = {(0, 1): 1.0}
        result = engine.merge_output_seams(mesh, 1.0)
        self.assertEqual(4, len(result.vertices))

    def test_invalid_density_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "base_density"):
            RetopoSettings(base_density=-1).validate()


class ReprojectionTests(unittest.TestCase):
    def test_base_guide_reprojects_to_source_corners(self):
        result = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(fit_uv=False))
        self.assertEqual(4, len(result.vertices))
        self.assertEqual(1, len(result.faces))
        self.assertEqual("UVMap", result.uv_name)
        self.assertEqual(4, len(result.uv))
        self.assertEqual([0], result.attributes["retopo_plane_id"])
        self.assertEqual({(-0.5, -0.5, 0.0), (0.5, -0.5, 0.0), (0.5, 0.5, 0.0), (-0.5, 0.5, 0.0)}, set(result.vertices))

    def test_subdivision_reproduces_source_edge_midpoints_and_center(self):
        settings = RetopoSettings(subdiv_reproject=1, fit_uv=False)
        result = engine.retopologize(curved_source(), unit_guide(), settings)
        self.assertEqual(9, len(result.vertices))
        self.assertEqual(4, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        self.assertIn((0.0, 0.0, 0.2), result.vertices)
        self.assertEqual(4, sum(math.isclose(vertex[2], 0.1) for vertex in result.vertices))

    def test_zero_subdivision_still_reprojects(self):
        result = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(subdiv_reproject=0))
        self.assertTrue(any(vertex[2] == 0.0 for vertex in result.vertices))
        self.assertNotEqual(unit_guide().vertices, result.vertices)

    def test_smooth_uv_choice_preserves_boundaries_and_relaxes_interior_only_when_enabled(self):
        boundary_relaxed = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(smooth_uvs=True, smooth_low_poly=2, fit_uv=False))
        boundary_unrelaxed = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(smooth_uvs=False, smooth_low_poly=2, fit_uv=False))
        self.assertEqual(boundary_relaxed.uv, boundary_unrelaxed.uv)
        grid = Mesh(
            vertices=[(x * 0.5, y * 0.5, 0.0) for y in range(3) for x in range(3)],
            faces=[(0, 1, 4, 3), (1, 2, 5, 4), (3, 4, 7, 6), (4, 5, 8, 7)],
        )
        grid.vertices[4] = (0.4, 0.4, 0.0)
        relaxed = engine.retopologize(curved_source(), grid, RetopoSettings(smooth_uvs=True, smooth_low_poly=1, fit_uv=False))
        unrelaxed = engine.retopologize(curved_source(), grid, RetopoSettings(smooth_uvs=False, smooth_low_poly=1, fit_uv=False))
        self.assertNotEqual(relaxed.uv, unrelaxed.uv)

    def test_fit_uvs_normalizes_complete_layout_into_first_tile(self):
        result = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(fit_uv=True))
        self.assertAlmostEqual(0.0075, min(value[0] for value in result.uv))
        self.assertAlmostEqual(0.0075, min(value[1] for value in result.uv))
        self.assertAlmostEqual(0.9925, max(value[0] for value in result.uv))
        self.assertAlmostEqual(0.9925, max(value[1] for value in result.uv))

    def test_final_mesh_ignores_morph_preview_slider(self):
        result = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(subdiv_reproject=1, morph_2d_3d=0.5))
        center = min(result.vertices, key=lambda vertex: math.dist(vertex[:2], (0.0, 0.0)))
        self.assertAlmostEqual(0.2, center[2])
        self.assertAlmostEqual(0.0, center[0])

    def test_pattern_separation_disconnects_faces_from_different_uv_islands(self):
        source = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (2, 0, 0), (3, 0, 0), (3, 1, 0), (2, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            uv=[(0, 0), (0.4, 0), (0.4, 1), (0, 1), (0.6, 0), (1, 0), (1, 1), (0.6, 1)],
        )
        guide = Mesh(vertices=[(0, 0, 0), (0.5, 0, 0), (1, 0, 0), (0, 1, 0), (0.5, 1, 0), (1, 1, 0)], faces=[(0, 1, 4, 3), (1, 2, 5, 4)])
        joined = engine.retopologize(source, guide, RetopoSettings(separate_patterns=False, separate_parts=False))
        separated = engine.retopologize(source, guide, RetopoSettings(separate_patterns=True, separate_parts=False))
        self.assertEqual(len(joined.vertices) + 2, len(separated.vertices))
        self.assertEqual(2, len(separated.connected_components()))

    def test_part_separation_disconnects_source_parts(self):
        source = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (2, 0, 0), (3, 0, 0), (3, 1, 0), (2, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            uv=[(0, 0), (0.5, 0), (0.5, 1), (0, 1), (0.5, 0), (1, 0), (1, 1), (0.5, 1)],
        )
        guide = Mesh(vertices=[(0, 0, 0), (0.5, 0, 0), (1, 0, 0), (0, 1, 0), (0.5, 1, 0), (1, 1, 0)], faces=[(0, 1, 4, 3), (1, 2, 5, 4)])
        result = engine.retopologize(source, guide, RetopoSettings(separate_parts=True))
        self.assertEqual(2, len(result.connected_components()))


class TopologyInspectorTests(unittest.TestCase):
    def non_manifold_mesh(self):
        return Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)],
            faces=[(0, 1, 2), (1, 0, 3), (0, 1, 4)],
        )

    def test_inspector_finds_edges_with_more_than_two_adjacent_faces(self):
        result, bad_edges = engine.inspect_topology(self.non_manifold_mesh())
        self.assertEqual([(0, 1)], bad_edges)
        self.assertEqual(3, len(result.faces))
        self.assertEqual({(0, 1): True}, result.edge_attributes["bad_mesh"])

    def test_split_edge_fix_gives_each_face_its_own_edge(self):
        result, bad_edges = engine.inspect_topology(self.non_manifold_mesh(), fix_split_edges=True)
        self.assertEqual([(0, 1)], bad_edges)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(9, len(result.vertices))
        self.assertEqual(3, len(result.faces))


class ThicknessAndDiagnosticsTests(unittest.TestCase):
    def test_thickness_matches_reference_shape(self):
        result = engine.thicken(unit_guide(), thickness=0.05, offset=0.0)
        self.assertEqual(8, len(result.vertices))
        self.assertEqual(6, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))
        zs = sorted(set(round(vertex[2], 6) for vertex in result.vertices))
        self.assertEqual([-0.025, 0.025], zs)

    def test_thickness_builds_inner_and_outer_hole_walls(self):
        result = engine.thicken(ring_guide(), thickness=0.1, offset=0.0)
        self.assertEqual(32, len(result.vertices))
        self.assertEqual(32, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_negative_thickness_is_supported(self):
        result = engine.thicken(unit_guide(), thickness=-0.05, offset=0.0)
        self.assertEqual(8, len(result.vertices))
        self.assertEqual(6, len(result.faces))
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_original_numeric_limits_are_enforced(self):
        for settings in (
            RetopoSettings(base_density=101),
            RetopoSettings(subdiv_reproject=7),
            RetopoSettings(auto_fill_max_iterations=51),
            RetopoSettings(smooth_low_poly=21),
            RetopoSettings(debug_material_seed=-1),
            RetopoSettings(debug_material_seed=2147483648),
        ):
            with self.subTest(settings=settings):
                with self.assertRaises(ValueError):
                    settings.validate()

    def test_loop_iteration_bounds_density_reconciliation(self):
        guide = unit_guide()
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        settings = RetopoSettings(base_density=20, loop_iteration=1)
        settings.validate()
        with self.assertRaisesRegex(ValueError, "loop_iteration"):
            RetopoSettings(loop_iteration=51).validate()
        self.assertEqual(2, len(engine.apply_density(guide, settings).faces))

    def test_morph_preview_is_separate_from_final_reprojection(self):
        final = engine.retopologize(curved_source(), unit_guide(), RetopoSettings(subdiv_reproject=1, morph_2d_3d=0.5))
        preview = engine.morph_preview(curved_source(), unit_guide(), RetopoSettings(subdiv_reproject=1, morph_2d_3d=0.5))
        center = min(preview.vertices, key=lambda vertex: math.dist(vertex[:2], (0.25, 0.25)))
        self.assertAlmostEqual(0.1, center[2])
        self.assertAlmostEqual(0.25, center[0])
        self.assertIn((0.0, 0.0, 0.2), final.vertices)

    def test_diagnostic_report_names_all_invalid_inputs(self):
        report = engine.diagnose(
            Mesh(vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0)], faces=[(0, 1, 2)]),
            RetopoSettings(base_density=-1),
            source=None,
        )
        self.assertIn("source", report["errors"])
        self.assertIn("base_density", report["errors"])
        self.assertIn("non_quad_faces", report["errors"])
        self.assertEqual([0], report["non_quad_faces"])


if __name__ == "__main__":
    unittest.main()
