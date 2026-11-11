import math
import unittest
from unittest import mock

from tools.retopo_engine import Mesh, RetopoSettings, differential, engine


def assert_manifold_connectors_preserved(test, mesh, result):
    incidence = {}
    for face_index, face in enumerate(mesh.faces):
        for corner, vertex in enumerate(face):
            edge = Mesh.edge_key(vertex, face[(corner + 1) % len(face)])
            incidence.setdefault(edge, []).append((face_index, corner))
    for edge, faces in incidence.items():
        if len(faces) != 2 or edge in engine.non_manifold_edges(mesh):
            continue
        descendants = []
        for face_index, corner in faces:
            face = result.faces[face_index]
            descendants.append(Mesh.edge_key(face[corner], face[(corner + 1) % len(face)]))
        test.assertEqual(descendants[0], descendants[1], edge)


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
            faces=[(0, 1, 3, 2), (1, 0, 4, 5), (0, 2, 10, 4), (0, 1, 7, 6), (1, 0, 8, 9), (0, 6, 11, 8), (6, 7, 8, 9)],
        )
        mesh.edge_attributes["crease_edge"] = {edge: 0.5 for edge in mesh.edges()}
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(set(range(len(result.vertices))), {index for face in result.faces for index in face})
        self.assertTrue(set(result.edge_attributes["crease_edge"]) <= set(result.edges()))
        for first, second in ((3, 4), (3, 5), (4, 5)):
            first_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[first],) for index in range(4)}
            second_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[second],) for index in range(4)}
            if (first, second) == (3, 4):
                self.assertTrue(set(result.faces[first]) & set(result.faces[second]), (first, second))
            else:
                self.assertTrue(first_edges & second_edges, (first, second))

    def test_split_edge_repair_keeps_single_incident_sheets_connected(self):
        mesh = Mesh(
            vertices=[(index, 0, 0) for index in range(14)],
            faces=[(0, 1, 2, 3), (0, 3, 4, 5), (1, 0, 6, 7), (0, 6, 8, 9), (0, 1, 10, 11), (0, 11, 12, 13)],
        )
        mesh.edge_attributes["crease_edge"] = {edge: 0.5 for edge in mesh.edges()}
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual([0, 1], [index for index, face in enumerate(result.faces) if 0 in face])
        self.assertTrue(set(result.edge_attributes["crease_edge"]) <= set(result.edges()))
        for first, second in ((0, 1), (2, 3), (4, 5)):
            first_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[first],) for index in range(4)}
            second_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[second],) for index in range(4)}
            self.assertTrue(first_edges & second_edges, (first, second))

    def test_non_preserved_multi_face_fans_keep_their_connectors(self):
        mesh = Mesh(
            vertices=[(index, 0, 0) for index in range(18)],
            faces=[(0, 1, 2, 3), (0, 1, 4, 5), (0, 1, 6, 7), (0, 3, 8, 5), (1, 4, 9, 6), (0, 1, 10, 11), (0, 1, 12, 13), (0, 1, 14, 15), (0, 11, 16, 13), (1, 12, 17, 14)],
        )
        mesh.edge_attributes["crease_edge"] = {edge: 0.5 for edge in mesh.edges()}
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(set(range(len(result.vertices))), {index for face in result.faces for index in face})
        self.assertTrue(set(result.edge_attributes["crease_edge"]) <= set(result.edges()))
        for first, second in ((5, 8), (6, 8), (6, 9), (7, 9)):
            self.assertNotEqual(result.faces[first], result.faces[second])
            first_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[first],) for index in range(4)}
            second_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[second],) for index in range(4)}
            original_shared_edges = set(mesh.edges()) & {
                Mesh.edge_key(mesh.faces[first][index], mesh.faces[first][(index + 1) % 4])
                for index in range(4)
            } & {
                Mesh.edge_key(mesh.faces[second][index], mesh.faces[second][(index + 1) % 4])
                for index in range(4)
            }
            if original_shared_edges:
                self.assertTrue(first_edges & second_edges, (first, second))
            else:
                self.assertTrue(set(result.faces[first]) & set(result.faces[second]), (first, second))

    def test_split_edge_repair_splits_even_when_all_fans_are_connected(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, -1, 0)],
            faces=[(0, 1, 2), (1, 0, 3), (0, 1, 4), (0, 2, 3), (1, 3, 4)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertNotEqual(mesh.faces, result.faces)

    def test_split_edge_repair_handles_four_connected_faces_without_unused_vertices(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0)] + [(index, 2, 0) for index in range(2, 8)],
            faces=[(0, 1, 3, 2), (1, 0, 2, 4), (0, 1, 4, 5), (1, 0, 5, 6), (4, 5, 7, 6)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(set(range(len(result.vertices))), {index for face in result.faces for index in face})

    def test_adjacent_bad_edges_leave_no_unused_vertices(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 1, 1), (0, 1, -1)],
            faces=[(0, 1, 2, 3), (2, 1, 0, 4), (0, 1, 2, 5)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(set(range(len(result.vertices))), {index for face in result.faces for index in face})

    def test_triangle_split_preserves_unrelated_valid_sheet(self):
        mesh = Mesh(
            vertices=[
                (0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1),
                (10, 0, 0), (11, 0, 0), (10, 1, 0), (11, 1, 0),
            ],
            faces=[(0, 1, 2), (1, 0, 3), (0, 1, 4), (5, 6, 8), (6, 7, 8)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(13, len(result.vertices))
        self.assertEqual(14, len(result.edges()))
        self.assertTrue(set(result.faces[3]) & set(result.faces[4]))

    def test_triangle_components_preserve_internal_manifold_edges(self):
        cases = [
            (
                [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, -1, 0), (-1, 0, 0)],
                [(0, 1, 2), (1, 0, 3), (0, 1, 4), (0, 2, 3), (0, 2, 5)],
            ),
            (
                [(0, 0, 0), (1, 0, 0), (2, 0, 0), (1, 1, 0), (1, 0, 1), (1, -1, 0)],
                [(0, 1, 3), (1, 2, 3), (1, 0, 4), (2, 1, 4), (0, 1, 5), (1, 2, 5)],
            ),
        ]
        for vertices, faces in cases:
            with self.subTest(faces=faces):
                mesh = Mesh(vertices=vertices, faces=faces)
                result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
                self.assertEqual([], engine.non_manifold_edges(result))
                assert_manifold_connectors_preserved(self, mesh, result)

    def test_retained_face_choice_preserves_connectors_for_every_face_order(self):
        vertices = [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (1, 0, 1), (0, 0, 1), (1, -1, 0), (0, -1, 0), (0, 1, 1), (1, -1, 1)]
        incident = [(0, 1, 2, 3), (0, 1, 4, 5), (0, 1, 6, 7)]
        for order in ((0, 1, 2), (0, 2, 1), (1, 0, 2), (1, 2, 0), (2, 0, 1), (2, 1, 0)):
            with self.subTest(order=order):
                faces = [incident[index] for index in order] + [(0, 3, 8, 5), (1, 4, 9, 6)]
                mesh = Mesh(vertices=vertices, faces=faces)
                result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
                self.assertEqual([], engine.non_manifold_edges(result))
                assert_manifold_connectors_preserved(self, mesh, result)

    def test_mixed_fan_repairs_preserve_all_manifold_connectors(self):
        cases = [
            (
                [(index, 0, 0) for index in range(6)],
                [(1, 0, 4), (5, 2, 0), (1, 0, 5), (1, 0, 2, 3), (1, 3, 4)],
            ),
            (
                [(index, 0, 0) for index in range(6)],
                [(0, 5, 4, 1), (4, 2, 1, 0), (0, 1, 5, 3), (3, 1, 5, 4), (3, 1, 4, 0)],
            ),
        ]
        for vertices, faces in cases:
            with self.subTest(faces=faces):
                mesh = Mesh(vertices=vertices, faces=faces)
                result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
                self.assertEqual([], engine.non_manifold_edges(result))
                assert_manifold_connectors_preserved(self, mesh, result)

    def test_healthy_triangle_mesh_is_unchanged_by_repair(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            faces=[(0, 1, 2), (0, 2, 3)],
        )
        with mock.patch.object(engine, "_part_faces", side_effect=AssertionError("healthy mesh must not scan components")):
            result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], bad_edges)
        self.assertEqual(mesh.vertices, result.vertices)
        self.assertEqual(mesh.faces, result.faces)
        self.assertEqual(1, len(result.connected_components()))

    def test_retained_fan_repairs_preserve_all_manifold_connectors(self):
        cases = [
            (
                [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (1, 0, 1), (0, 0, 1), (1, -1, 0), (0, -1, 0), (0, 1, 1), (1, -1, 1)],
                [(0, 1, 2, 3), (0, 1, 4, 5), (0, 1, 6, 7), (0, 3, 8, 5), (1, 4, 9, 6)],
                [(0, 3), (1, 3), (1, 4), (2, 4)],
            ),
            (
                [(index, 0, 0) for index in range(10)],
                [(0, 1, 2, 3), (1, 0, 4, 5), (0, 1, 6, 7), (0, 3, 8, 7), (1, 2, 9, 5)],
                [(0, 3), (2, 3), (0, 4), (1, 4)],
            ),
        ]
        for vertices, faces, connected_pairs in cases:
            with self.subTest(faces=faces):
                mesh = Mesh(vertices=vertices, faces=faces)
                result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
                self.assertEqual([], engine.non_manifold_edges(result))
                for first, second in connected_pairs:
                    first_edges = {Mesh.edge_key(result.faces[first][index], result.faces[first][(index + 1) % 4]) for index in range(4)}
                    second_edges = {Mesh.edge_key(result.faces[second][index], result.faces[second][(index + 1) % 4]) for index in range(4)}
                    self.assertTrue(first_edges & second_edges, (first, second))

    def test_repair_preserves_original_loose_vertices_and_line_attributes(self):
        mesh = Mesh(
            vertices=[(9, 9, 9), (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 1, 1), (0, -1, 0)],
            faces=[(1, 2, 3, 4), (2, 1, 5, 6), (1, 2, 4, 6)],
            lines=[(2, 4)],
            attributes={"point_guide": [True] * 7},
            edge_attributes={"crease_edge": {(2, 4): 1.0, (1, 2): 0.5}},
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertIn((9.0, 9.0, 9.0), result.vertices)
        self.assertEqual(len(result.vertices), len(result.attributes["point_guide"]))
        self.assertEqual(1.0, result.edge_attributes["crease_edge"][result.lines[0]])
        self.assertEqual([], engine.non_manifold_edges(result))

    def test_debug_edges_follow_real_descendant_faces(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (-1, 0, 0)],
            faces=[(0, 2, 5), (0, 1, 2), (1, 0, 3), (0, 1, 4)],
        )
        original_bad = engine.non_manifold_edges(mesh)
        result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertTrue(set(bad_edges) <= set(result.edges()))
        self.assertEqual(set(bad_edges), set(result.edge_attributes["bad_mesh"]))
        for original, returned in zip(original_bad, bad_edges):
            self.assertEqual(tuple(mesh.vertices[index] for index in original), tuple(result.vertices[index] for index in returned))

    def test_inspection_without_repair_preserves_loose_data_and_debug_indices(self):
        mesh = Mesh(
            vertices=[(9, 9, 9), (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 1, 1), (0, -1, 0)],
            faces=[(1, 2, 3, 4), (2, 1, 5, 6), (1, 2, 4, 6)],
            lines=[(2, 4)],
            attributes={"point_guide": [True] * 7},
            edge_attributes={"crease_edge": {(2, 4): 1.0, (1, 2): 0.5}},
        )
        result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=False)
        self.assertEqual(mesh.vertices, result.vertices)
        self.assertEqual(mesh.lines, result.lines)
        self.assertEqual(mesh.attributes["point_guide"], result.attributes["point_guide"])
        self.assertEqual(1.0, result.edge_attributes["crease_edge"][(2, 4)])
        for first, second in bad_edges:
            self.assertEqual((mesh.vertices[first], mesh.vertices[second]), (result.vertices[first], result.vertices[second]))

    def test_adjacent_bad_edges_preserve_shared_sheet_edges(self):
        mesh = Mesh(
            vertices=[(x, y, z) for z in (0, 1, -1) for y in (0, 1) for x in (0, 1, 2)],
            faces=[(0, 1, 4, 3), (1, 2, 5, 4), (0, 1, 7, 6), (1, 2, 8, 7), (0, 1, 10, 9), (1, 2, 11, 10)],
            uv=[(0, 0)] * 24,
        )
        result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        self.assertEqual(len(mesh.faces), len(result.faces))
        self.assertEqual(len(result.uv), sum(len(face) for face in result.faces))
        self.assertEqual(engine.non_manifold_edges(mesh), bad_edges)
        for first, second in ((0, 1), (2, 3), (4, 5)):
            first_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[first],) for index in range(4)}
            second_edges = {Mesh.edge_key(face[index], face[(index + 1) % len(face)]) for face in (result.faces[second],) for index in range(4)}
            self.assertTrue(first_edges & second_edges, (first, second))

    def test_density_does_not_weld_coincident_independent_planes(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (0.5, 0, 0), (0.5, 1, 0), (0, 1, 0), (0.5, 0, 0), (1, 0, 0), (1, 1, 0), (0.5, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        )
        guide.edge_attributes["bevel_weight_edge"] = {(1, 2): 1.0, (4, 7): 1.0}
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        result = engine.retopologize(
            differential.curved_grid(), guide,
            RetopoSettings(
                base_density=20, fit_uv=False, separate_parts=False, separate_patterns=False,
                seams_merge_distance=0, snapping_distance=0,
            ),
        )
        self.assertEqual(2, len(result.connected_components()))
        self.assertEqual(2, len(set(result.attributes["retopo_plane_id"])))
        self.assertEqual(2, len(result.edge_attributes["bevel_weight_edge"]))

    def test_morph_preview_tolerates_mirror_control_vertices(self):
        result = engine.morph_preview(
            differential.mirror_source(), differential.mirror_guide(),
            RetopoSettings(morph_2d_3d=0.5, fit_uv=False),
        )
        self.assertTrue(all(math.isfinite(value) for vertex in result.vertices for value in vertex))
        self.assertEqual(2, len(result.faces))

    def test_initialize_planes_uses_marked_manual_corner_points(self):
        guide = Mesh(
            vertices=[(0.1, 0.1, 0), (0.9, 0.1, 0), (0.9, 0.9, 0), (0.1, 0.9, 0)],
            faces=[],
            attributes={"point_guide": [True] * 4},
        )
        result = engine.retopologize(
            differential.curved_grid(), guide,
            RetopoSettings(initialize_planes=True, output_generated_planes=True),
        )
        self.assertEqual(guide.vertices, result.vertices)
        self.assertEqual(1, len(result.faces))

    def test_invalid_guide_markers_do_not_change_generated_geometry(self):
        source = differential.curved_grid()
        source.attributes["point_guide"] = [True]
        source.attribute_domains["point_guide"] = "POINT"
        guide = Mesh(
            vertices=[(0.1, 0.1, 0), (0.9, 0.1, 0), (0.9, 0.9, 0), (0.1, 0.9, 0)],
            faces=[(0, 1, 2, 3)] * 4,
            attributes={"point_guide": [True] * 4},
            attribute_domains={"point_guide": "FACE"},
        )
        result = engine.retopologize(
            source, guide,
            RetopoSettings(initialize_planes=True, output_generated_planes=True),
        )
        self.assertEqual(differential.unit_guide().vertices, result.vertices)
        self.assertNotIn("point_guide", result.attributes)
        self.assertNotIn("point_guide", result.attribute_domains)

    def test_direct_initialization_and_snap_ignore_invalid_markers(self):
        source = differential.curved_grid()
        selective = [index in {1, 3, 5, 7} for index in range(len(source.vertices))]
        for domain in ("CORNER", "FACE"):
            with self.subTest(source_domain=domain):
                source.attributes["point_guide"] = selective
                source.attribute_domains["point_guide"] = domain
                flat = engine.flatten_to_patterns(source)
                result = engine.initialize_planes(flat, RetopoSettings())
                self.assertEqual(differential.unit_guide().vertices, result.vertices)
                self.assertNotIn("point_guide", result.attributes)
        source.attribute_domains["point_guide"] = "POINT"
        valid_source_result = engine.initialize_planes(engine.flatten_to_patterns(source), RetopoSettings())
        self.assertNotEqual(differential.unit_guide().vertices, valid_source_result.vertices)
        source.attributes.pop("point_guide")
        source.attribute_domains.pop("point_guide")
        flat = engine.flatten_to_patterns(source)
        for domain, values in (("CORNER", selective), ("POINT", [True])):
            with self.subTest(direct_domain=domain, direct_length=len(values)):
                invalid = flat.copy()
                invalid.attributes["point_guide"] = values
                invalid.attribute_domains["point_guide"] = domain
                result = engine.initialize_planes(invalid, RetopoSettings())
                self.assertEqual(differential.unit_guide().vertices, result.vertices)
        valid_direct = flat.copy()
        valid_direct.attributes["point_guide"] = selective
        valid_direct.attribute_domains["point_guide"] = "POINT"
        valid_direct_result = engine.initialize_planes(valid_direct, RetopoSettings())
        self.assertNotEqual(differential.unit_guide().vertices, valid_direct_result.vertices)
        face_marked = flat.copy()
        face_marked.attributes["point_guide"] = [False, True, False, True]
        face_marked.attribute_domains["point_guide"] = "FACE"
        face_marked_result = engine.initialize_planes(face_marked, RetopoSettings())
        self.assertEqual(differential.unit_guide().vertices, face_marked_result.vertices)
        first = engine.flatten_to_patterns(differential.curved_grid())
        second = first.copy()
        second.vertices = [(x + 2, y, z) for x, y, z in second.vertices]
        offset = len(first.vertices)
        combined = Mesh(
            vertices=first.vertices + second.vertices,
            faces=first.faces + [tuple(index + offset for index in face) for face in second.faces],
            attributes={name: values + second.attributes[name] for name, values in first.attributes.items()},
            attribute_domains=first.attribute_domains,
        )
        expected = engine.initialize_planes(combined, RetopoSettings())
        for domain in ("CORNER", "FACE"):
            with self.subTest(component_domain=domain):
                invalid = combined.copy()
                invalid.attributes["point_guide"] = [index == 0 for index in range(len(first.vertices))] * 2
                invalid.attribute_domains["point_guide"] = domain
                result = engine.initialize_planes(invalid, RetopoSettings())
                self.assertEqual((expected.vertices, expected.faces), (result.vertices, result.faces))
        ring = engine.flatten_to_patterns(differential.ring())
        ring.attributes.update({"corner_tag": [1] * len(ring.vertices), "short_point": [1, 2]})
        ring.attribute_domains.update({"corner_tag": "CORNER", "short_point": "POINT", "orphan": "FACE"})
        initialized_ring = engine.initialize_planes(ring, RetopoSettings())
        self.assertEqual({"corner_tag", "short_point"} & initialized_ring.attributes.keys(), set())
        self.assertEqual({"corner_tag", "short_point", "orphan"} & initialized_ring.attribute_domains.keys(), set())
        annulus = differential.ring_guide()
        annulus.faces = [triangle for face in annulus.faces for triangle in ((face[0], face[1], face[2]), (face[0], face[2], face[3]))]
        clockwise = differential.ring_guide()
        clockwise.vertices = [(x, 1 - y, z) for x, y, z in clockwise.vertices]
        clockwise.faces = [triangle for face in clockwise.faces for triangle in ((face[2], face[1], face[0]), (face[3], face[2], face[0]))]

        def orientation(first, second, third):
            return (second[0] - first[0]) * (third[1] - first[1]) - (second[1] - first[1]) * (third[0] - first[0])

        def intersects(first, second, third, fourth):
            return orientation(first, second, third) * orientation(first, second, fourth) < 0 and orientation(third, fourth, first) * orientation(third, fourth, second) < 0

        for annulus_input in (annulus, clockwise):
            initialized_annulus = engine.initialize_planes(annulus_input, RetopoSettings())
            self.assertEqual((16, 8, 16), (len(initialized_annulus.vertices), len(initialized_annulus.faces), len(initialized_annulus.boundary_edges())))
            expected_area = sum(engine.signed_area(face, annulus_input.vertices) for face in annulus_input.faces) / len(initialized_annulus.faces)
            for face in initialized_annulus.faces:
                points = [initialized_annulus.vertices[index] for index in face]
                self.assertAlmostEqual(expected_area, engine.signed_area(face, initialized_annulus.vertices))
                self.assertGreater(engine.signed_area(face, initialized_annulus.vertices), 0)
                self.assertFalse(intersects(points[0], points[1], points[2], points[3]))
                self.assertFalse(intersects(points[1], points[2], points[3], points[0]))
        irregular = Mesh(
            vertices=[(30, 0, 0), (0, -30, 0), (-30, 0, 0), (0, 30, 0), (30, 10, 0), (18, 1, 0), (18, 6, 0), (3, 16, 0), (-12, 1, 0), (3, -14, 0)],
            faces=[(0, 4, 6), (0, 6, 5), (4, 3, 7), (4, 7, 6), (3, 2, 8), (3, 8, 7), (2, 1, 9), (2, 9, 8), (1, 0, 5), (1, 5, 9)],
        )
        initialized_irregular = engine.initialize_planes(irregular, RetopoSettings())
        for face in initialized_irregular.faces:
            points = [initialized_irregular.vertices[index] for index in face]
            self.assertGreater(engine.signed_area(face, initialized_irregular.vertices), 0)
            self.assertFalse(intersects(points[0], points[1], points[2], points[3]))
            self.assertFalse(intersects(points[1], points[2], points[3], points[0]))
        unequal_faces = []
        outer_index = inner_index = 0
        while outer_index < 8 or inner_index < 4:
            if inner_index >= 4 or (outer_index < 8 and outer_index / 8 <= inner_index / 4):
                unequal_faces.append((outer_index % 8, (outer_index + 1) % 8, 8 + inner_index % 4))
                outer_index += 1
            else:
                unequal_faces.append((outer_index % 8, 8 + (inner_index + 1) % 4, 8 + inner_index % 4))
                inner_index += 1
        unequal = Mesh(
            vertices=[(2 * math.cos(math.tau * index / 8), 2 * math.sin(math.tau * index / 8), 0) for index in range(8)]
            + [(math.cos(math.tau * index / 4), math.sin(math.tau * index / 4), 0) for index in range(4)],
            faces=unequal_faces,
        )
        initialized_unequal = engine.initialize_planes(unequal, RetopoSettings())
        self.assertEqual((16, 8, 16), (len(initialized_unequal.vertices), len(initialized_unequal.faces), len(initialized_unequal.boundary_edges())))
        for face in initialized_unequal.faces:
            points = [initialized_unequal.vertices[index] for index in face]
            self.assertGreater(engine.signed_area(face, initialized_unequal.vertices), 0)
            self.assertFalse(intersects(points[0], points[1], points[2], points[3]))
            self.assertFalse(intersects(points[1], points[2], points[3], points[0]))

        def loop_case(outer, inner):
            faces = []
            outer_index = inner_index = 0
            while outer_index < len(outer) or inner_index < len(inner):
                if inner_index >= len(inner) or (outer_index < len(outer) and outer_index / len(outer) <= inner_index / len(inner)):
                    faces.append((outer_index % len(outer), (outer_index + 1) % len(outer), len(outer) + inner_index % len(inner)))
                    outer_index += 1
                else:
                    faces.append((outer_index % len(outer), len(outer) + (inner_index + 1) % len(inner), len(outer) + inner_index % len(inner)))
                    inner_index += 1
            return Mesh(vertices=outer + inner, faces=faces)

        corner_cases = (
            loop_case([(-3, -3, 0), (0, -3, 0), (3, -3, 0), (3, 3, 0), (0, 3, 0), (-3, 3, 0)], [(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)]),
            loop_case([(-3, -3, 0), (3, -3, 0), (3, 3, 0), (-3, 3, 0)], [(0, -1, 0), (1, -1, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (-1, 1, 0), (-1, 0, 0), (-1, -1, 0)]),
        )
        for corner_case in corner_cases:
            initialized_corners = engine.initialize_planes(corner_case, RetopoSettings())
            input_area = sum(engine.signed_area(face, corner_case.vertices) for face in corner_case.faces)
            output_area = sum(engine.signed_area(face, initialized_corners.vertices) for face in initialized_corners.faces)
            self.assertAlmostEqual(input_area, output_area)
            self.assertTrue(all(vertex in initialized_corners.vertices for vertex in corner_case.vertices))
        alternative_split = loop_case(
            [(1.257, 1.809, 0), (-2.368, 2.23, 0), (-3.207, 1.863, 0), (-1.976, -1.841, 0), (-1.216, -3.272, 0), (-0.382, -2.454, 0), (0.889, -2.864, 0), (2.673, -2.136, 0)],
            [(0.5 * math.cos(1.7202529039022743 + math.tau * index / 3), 0.5 * math.sin(1.7202529039022743 + math.tau * index / 3), 0) for index in range(3)],
        )
        initialized_alternative = engine.initialize_planes(alternative_split, RetopoSettings())
        self.assertEqual(8, len(initialized_alternative.faces))
        self.assertTrue(all(vertex in initialized_alternative.vertices for vertex in alternative_split.vertices))
        later_rotation = loop_case(
            [(-2.012, 1.099, 0), (-3.069, 1.647, 0), (-3.937, -0.595, 0), (-1.75, -2.123, 0), (-0.58, -2.262, 0), (0.777, -2.28, 0), (2.75, -0.747, 0)],
            [(0.596, -0.164, 0), (0.232, -0.49, 0), (0.259, -0.977, 0), (0.657, -1.26, 0), (1.126, -1.125, 0), (1.314, -0.674, 0), (1.078, -0.246, 0)],
        )
        initialized_later = engine.initialize_planes(later_rotation, RetopoSettings())
        self.assertEqual(7, len(initialized_later.faces))
        self.assertTrue(all(engine.signed_area(face, initialized_later.vertices) > 0 for face in initialized_later.faces))
        crossing_outer = [(2.07, 0.774, 0), (1.944, 1.884, 0), (1.449, 1.551, 0), (1.69, 2.588, 0), (0.799, 1.641, 0), (-0.975, 2.182, 0), (-2.456, -1.221, 0), (-1.573, -1.198, 0), (-2.086, -1.825, 0), (0.887, -1.398, 0)]
        crossing_inner = [(-1.034, -0.31, 0), (0.39, -0.604, 0), (0.683, 0.82, 0), (-0.74, 1.113, 0)]
        while len(crossing_inner) < len(crossing_outer):
            lengths = [math.dist(crossing_inner[index], crossing_inner[(index + 1) % len(crossing_inner)]) for index in range(len(crossing_inner))]
            split_index = max(range(len(lengths)), key=lengths.__getitem__)
            crossing_inner.insert(split_index + 1, engine.lerp(crossing_inner[split_index], crossing_inner[(split_index + 1) % len(crossing_inner)], 0.5))
        assembled_crossing = loop_case(crossing_outer, crossing_inner)
        with self.assertRaisesRegex(ValueError, "could not align hole boundaries"):
            engine.initialize_planes(assembled_crossing, RetopoSettings())
        def star_case(count, high, low, radius, phase, offset):
            outer = [((high if index % 2 == 0 else low) * math.cos(math.tau * index / count), (high if index % 2 == 0 else low) * math.sin(math.tau * index / count), 0) for index in range(count)]
            inner = [(radius * math.cos(phase + math.tau * index / count) + offset, radius * math.sin(phase + math.tau * index / count), 0) for index in range(count)]
            return Mesh(
                vertices=outer + inner,
                faces=[triangle for index in range(count) for triangle in (
                    (index, (index + 1) % count, count + (index + 1) % count),
                    (index, count + (index + 1) % count, count + index),
                )],
            )

        with self.assertRaisesRegex(ValueError, "simple hole inside the outer boundary"):
            engine.initialize_planes(star_case(12, 3.57, 0.91, 0.83, 3.07, 0.14), RetopoSettings())
        longer_inner = Mesh(
            vertices=[(0, 0, 0), (4, 0, 0), (4, 4, 0), (0, 4, 0), (1, 1, 0), (2, 1, 0), (3, 1, 0), (3, 3, 0), (1, 3, 0)],
            faces=[(0, 1, 5), (0, 5, 4), (1, 6, 5), (1, 2, 7), (1, 7, 6), (2, 3, 8), (2, 8, 7), (3, 0, 4), (3, 4, 8)],
        )
        initialized_longer_inner = engine.initialize_planes(longer_inner, RetopoSettings())
        self.assertEqual((10, 5, 10), (len(initialized_longer_inner.vertices), len(initialized_longer_inner.faces), len(initialized_longer_inner.boundary_edges())))
        self.assertTrue(all(engine.signed_area(face, initialized_longer_inner.vertices) > 0 for face in initialized_longer_inner.faces))
        square = [(0, 0, 0), (4, 0, 0), (4, 4, 0), (0, 4, 0)]
        for name, inner, error in (
            ("touching", [(0, 0, 0), (3, 1, 0), (3, 3, 0), (1, 3, 0)], "inside the outer boundary"),
            ("crossing", [(1, 1, 0), (3, 3, 0), (1, 3, 0), (3, 1, 0)], "simple hole boundaries"),
        ):
            with self.subTest(invalid_hole=name):
                malformed = Mesh(
                    vertices=square + inner,
                    faces=[triangle for index in range(4) for triangle in (
                        (index, (index + 1) % 4, 4 + (index + 1) % 4),
                        (index, 4 + (index + 1) % 4, 4 + index),
                    )],
                )
                with self.assertRaisesRegex(ValueError, error):
                    engine.initialize_planes(malformed, RetopoSettings())
        invalid_alignment = Mesh(
            vertices=[(3, 0, 0), (0.5, 1, 0), (0, 3, 0), (-1, 1, 0), (-3, 0, 0), (-1, -1, 0), (0, -3, 0), (1, -1, 0)]
            + [(1 + 0.1 * math.cos(math.tau * index / 8), 0.1 * math.sin(math.tau * index / 8), 0) for index in range(8)],
            faces=[triangle for index in range(8) for triangle in (
                (index, (index + 1) % 8, 8 + (index + 1) % 8),
                (index, 8 + (index + 1) % 8, 8 + index),
            )],
        )
        with self.assertRaisesRegex(ValueError, "could not align hole boundaries"):
            engine.initialize_planes(invalid_alignment, RetopoSettings())
        guide = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            faces=[(0, 1, 2, 3)],
            attributes={"point_guide": [True, True, True, True]},
            attribute_domains={"point_guide": "CORNER"},
        )
        snapped = engine.snap_guides_to_pattern(guide, differential.unit_guide(), 0.001)
        self.assertNotIn("point_guide", snapped.attributes)
        self.assertNotIn("point_guide", snapped.attribute_domains)

    def test_density_plane_ids_distinguish_unequal_components(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (0.4, 0, 0), (0.4, 1, 0), (0, 1, 0), (0.6, 0, 0), (1, 0, 0), (1, 1, 0), (0.6, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        )
        guide.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        result = engine.retopologize(
            differential.curved_grid(), guide,
            RetopoSettings(base_density=100, fit_uv=False, separate_parts=False, seams_merge_distance=0),
        )
        components = result.connected_components()
        self.assertEqual([11, 1], [len(component) for component in components])
        self.assertEqual([{0}, {1}], [{result.attributes["retopo_plane_id"][index] for index in component} for component in components])
        cursor = 0
        for face in result.faces:
            for vertex in face:
                u, v = result.uv[cursor]
                expected = (u - 0.5, v - 0.5, 0.2 - 0.2 * (abs(u - 0.5) + abs(v - 0.5)))
                for axis in range(3):
                    self.assertAlmostEqual(expected[axis], result.vertices[vertex][axis], delta=0.00001)
                cursor += 1

    def test_simplification_preserves_remapped_point_guides(self):
        source = differential.curved_grid()
        source.attributes["point_guide"] = [index == 8 for index in range(9)]
        result = engine.flatten_to_patterns(source, simplify=True, merge_distance=0.51)
        self.assertEqual(5, len(result.vertices))
        self.assertEqual(4, result.attributes["source_vertex"].index(8))
        self.assertEqual(4, result.attributes["point_guide"].index(True))

    def test_seam_merge_never_welds_two_corners_of_one_face_to_one_cluster(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (5, 0, 0), (5, -5, 0), (0, -5, 0), (0.1, 0, 0), (0, 0.1, 0), (-5, 5, 0), (5, 5, 0)],
            faces=[(0, 3, 2, 1), (4, 5, 6, 7)],
            uv=[(0, 0)] * 8,
        )
        result = engine.merge_output_seams(mesh, 0.5)
        self.assertTrue(all(len(face) == 4 for face in result.faces))

    def test_triangle_repair_preserves_face_identity_uv_and_metadata(self):
        source_faces = [(0, 1, 2), (1, 0, 3), (0, 1, 4)]
        vertices = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)]
        for order in ((0, 1, 2), (0, 2, 1), (1, 0, 2), (1, 2, 0), (2, 0, 1), (2, 1, 0)):
            with self.subTest(order=order):
                faces = [source_faces[index] for index in order]
                uv = [(face * 10 + corner, 0) for face in range(3) for corner in range(3)]
                mesh = Mesh(
                    vertices=vertices,
                    faces=faces,
                    uv=uv,
                    attributes={"pattern_id": [10, 11, 12], "part_id": [20, 21, 22], "retopo_plane_id": [30, 31, 32]},
                    edge_attributes={"crease_edge": {edge: index / 10 for index, edge in enumerate(sorted({Mesh.edge_key(face[i], face[(i + 1) % 3]) for face in faces for i in range(3)}))}},
                )
                result, bad_edges = engine.inspect_topology(mesh, fix_split_edges=True)
                self.assertEqual([], engine.non_manifold_edges(result))
                self.assertTrue(set(bad_edges) <= set(result.edges()))
                for original, returned in zip(engine.non_manifold_edges(mesh), bad_edges):
                    self.assertEqual({mesh.vertices[index] for index in original}, {result.vertices[index] for index in returned})
                for face_index, face in enumerate(mesh.faces):
                    self.assertEqual([mesh.vertices[index] for index in face], [result.vertices[index] for index in result.faces[face_index]])
                    self.assertEqual(mesh.uv[face_index * 3:face_index * 3 + 3], result.uv[face_index * 3:face_index * 3 + 3])
                    for name in ("pattern_id", "part_id", "retopo_plane_id"):
                        self.assertEqual(mesh.attributes[name], result.attributes[name])
                    for corner, vertex in enumerate(face):
                        original_edge = Mesh.edge_key(vertex, face[(corner + 1) % 3])
                        new_face = result.faces[face_index]
                        new_edge = Mesh.edge_key(new_face[corner], new_face[(corner + 1) % 3])
                        self.assertEqual(mesh.edge_attributes["crease_edge"][original_edge], result.edge_attributes["crease_edge"][new_edge])

    def test_face_attributes_follow_auto_fill_and_mirror(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (2, 0, 0), (2, 1, 0)],
            faces=[(0, 1, 2, 3), (1, 4, 5, 2)],
            attributes={
                "retopo_plane_id": [7, 8],
                "pattern_id": [10, 11],
                "part_id": [20, 21],
                "source_vertex": list(range(6)),
                "point_guide": [True, False, False, False, False, True],
            },
            attribute_domains={
                "retopo_plane_id": "FACE", "pattern_id": "FACE", "part_id": "FACE",
                "source_vertex": "POINT", "point_guide": "POINT",
            },
        )
        filled = engine.auto_fill(guide, [(0.3, 0, 0)], 5)
        self.assertEqual(3, len(filled.faces))
        for name, expected in (("retopo_plane_id", [8, 7, 7]), ("pattern_id", [11, 10, 10]), ("part_id", [21, 20, 20])):
            self.assertEqual(expected, filled.attributes[name])
        self.assertEqual(len(filled.vertices), len(filled.attributes["source_vertex"]))
        self.assertEqual(len(filled.vertices), len(filled.attributes["point_guide"]))
        across_components = engine.auto_fill(guide, [(0.3, 0, 0)], 5, across_patterns=False)
        for name, expected in (("retopo_plane_id", [8, 7, 7]), ("pattern_id", [11, 10, 10]), ("part_id", [21, 20, 20])):
            self.assertEqual(expected, across_components.attributes[name])
        self.assertEqual(len(across_components.vertices), len(across_components.attributes["source_vertex"]))
        mirrored = engine.mirror_guides(guide, axes=("x",))
        self.assertEqual(4, len(mirrored.faces))
        for name, expected in (("retopo_plane_id", [7, 8, 7, 8]), ("pattern_id", [10, 11, 10, 11]), ("part_id", [20, 21, 20, 21])):
            self.assertEqual(expected, mirrored.attributes[name])

    def test_auto_fill_subdivision_preserves_point_and_face_metadata(self):
        guide = differential.unit_guide()
        guide.attributes.update({
            "source_vertex": [10, 20, 30, 40],
            "point_guide": [True, False, False, True],
            "pattern_id": [9],
            "corner_tag": [1, 1, 1, 1],
        })
        guide.attribute_domains.update({"point_guide": "POINT", "pattern_id": "FACE", "corner_tag": "CORNER"})
        result = engine.auto_fill(guide, [(0.5, 0.5, 0)], 1)
        self.assertEqual(9, len(result.vertices))
        self.assertEqual(4, len(result.faces))
        self.assertEqual([10, 20, 30, 40, 10, 10, 20, 30, 10], result.attributes["source_vertex"])
        self.assertEqual([True, False, False, True, False, False, False, False, False], result.attributes["point_guide"])
        self.assertEqual([9] * 4, result.attributes["pattern_id"])
        self.assertNotIn("corner_tag", result.attributes)

    def test_auto_fill_separates_metadata_by_component(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (3, 0, 0), (4, 0, 0), (4, 1, 0), (3, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            attributes={
                "pattern_id": [100, 200],
                "source_vertex": list(range(8)),
                "point_guide": [False] * 8,
            },
            attribute_domains={"pattern_id": "FACE"},
        )
        result = engine.auto_fill(mesh, [(0.5, 0.5, 0)], 1, across_patterns=False)
        self.assertEqual([100, 100, 100, 100, 200], result.attributes["pattern_id"])
        self.assertEqual([0, 1, 2, 3, 0, 0, 1, 2, 0, 4, 5, 6, 7], result.attributes["source_vertex"])
        self.assertEqual([False] * 13, result.attributes["point_guide"])

    def test_undeclared_known_point_domains_are_consistent(self):
        size = 6
        vertices = [
            ((3 + math.cos(math.tau * i / size) * math.cos(math.tau * j / size), 3 + math.sin(math.tau * i / size) * math.cos(math.tau * j / size), math.sin(math.tau * j / size)))
            for i in range(size) for j in range(size)
        ]
        faces = [
            (i * size + j, ((i + 1) % size) * size + j, ((i + 1) % size) * size + (j + 1) % size, i * size + (j + 1) % size)
            for i in range(size) for j in range(size)
        ]
        guide = Mesh(
            vertices=vertices,
            faces=faces,
            attributes={"source_vertex": list(range(36)), "point_guide": [True] + [False] * 35},
        )
        first, second, third, fourth = (guide.vertices[index] for index in guide.faces[0])
        normal = engine.normalize(engine.cross(engine.subtract(second, first), engine.subtract(fourth, first)))
        center = engine.add(engine.average((first, second, third, fourth)), engine.multiply(normal, 0.34))
        composite = guide.copy()
        composite.vertices.extend([(100, 0, 0), (101, 0, 0), (101, 1, 0), (100, 1, 0)])
        composite.faces.append((36, 37, 38, 39))
        composite.attributes["source_vertex"].extend(range(36, 40))
        composite.attributes["point_guide"].extend([False] * 4)
        composite.attributes["pattern_id"] = list(range(40))
        filled = engine.auto_fill(composite, [center], 1, across_patterns=False)
        self.assertGreater(len(filled.faces), len(composite.faces))
        self.assertEqual(list(range(36)), filled.attributes["pattern_id"][:36])
        self.assertEqual(len(filled.vertices), len(filled.attributes["source_vertex"]))
        self.assertEqual(1, sum(filled.attributes["point_guide"]))
        mirrored = engine.mirror_guides(guide, axes=("x",))
        self.assertEqual(len(mirrored.vertices), len(mirrored.attributes["source_vertex"]))
        self.assertEqual(2, sum(mirrored.attributes["point_guide"]))
        subdivided = engine.subdivide_quads(guide)
        self.assertEqual(list(range(36)), subdivided.attributes["source_vertex"][:36])
        self.assertEqual(1, sum(subdivided.attributes["point_guide"]))
        merged = engine.merge_output_seams(
            Mesh(
                vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 0), (1, 0, 0), (1, -1, 0), (0, -1, 0)],
                faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
                attributes={"source_vertex": list(range(8))},
            ),
            0.01,
        )
        self.assertEqual(6, len(merged.vertices))
        self.assertEqual([0, 1, 2, 3, 6, 7], merged.attributes["source_vertex"])

    def test_repair_freezes_undeclared_point_domain_after_count_change(self):
        vertices = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
        faces = [(0, 1, 2), (1, 0, 3), (0, 1, 4), (1, 0, 5)]
        for _ in range(4):
            offset = len(vertices)
            vertices.extend([(6, 0, 0), (7, 0, 0), (6, 1, 0), (5, 0, 0), (6, -1, 0), (6, 0, 1)])
            faces.extend(tuple(offset + index for index in face) for face in (
                (0, 2, 1), (0, 1, 3), (1, 2, 3), (0, 3, 2),
                (4, 1, 2), (4, 2, 5), (4, 5, 1), (4, 3, 5),
            ))
        mesh = Mesh(vertices=vertices, faces=faces, attributes={"weight": list(range(30))})
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual(52, len(result.vertices))
        self.assertEqual(36, len(result.faces))
        self.assertEqual("POINT", result.attribute_domains["weight"])
        self.assertEqual(len(result.vertices), len(result.attributes["weight"]))

    def test_reconstruction_drops_unresolved_and_unsupported_attributes(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)],
            faces=[(0, 1, 2, 3)],
            uv=[(0, 0), (1, 0), (1, 1), (0, 1)],
            attributes={
                "source_vertex": [0, 1, 2, 3],
                "weight": [9, 8, 7, 6, 5, 4],
                "corner_tag": [1, 1, 1, 1],
                "short_point": [1, 2, 3],
            },
            attribute_domains={"corner_tag": "CORNER", "short_point": "POINT"},
        )
        weld_mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 0), (1, 0, 0), (1, -1, 0), (0, -1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
            attributes={"weight": list(range(6)), "source_vertex": list(range(8))},
        )
        repair_mesh = differential.non_manifold()
        repair_mesh.attributes.update({"source_vertex": list(range(5)), "stale": list(range(52)), "corner_tag": [0] * 5})
        repair_mesh.attribute_domains["corner_tag"] = "CORNER"
        mismatched_axis = mesh.copy()
        mismatched_axis.lines = [(0, 1)]
        two_axes = mesh.copy()
        two_axes.vertices.extend([(0.25, 0.5, 0), (0.5, 0.25, 0)])
        two_axes.attributes["source_vertex"] = list(range(6))
        two_axes.attribute_domains["weight"] = "FACE"
        two_axes.lines = [(1, 4), (2, 5)]
        results = [
            engine.mirror_guides(mesh, axes=("x",)),
            engine.mirror_guides(mismatched_axis, axes=("y",), source=differential.curved_grid()),
            engine.mirror_guides(two_axes, axes=("x", "y"), source=differential.curved_grid()),
            engine.snap_guides_to_pattern(mesh, differential.unit_guide(), 0),
            engine._separate(mesh, [0]),
            engine.merge_output_seams(weld_mesh, 0.01),
            engine.thicken(mesh, 0.1),
            engine.subdivide_quads(mesh),
            engine.auto_fill(mesh, [(0.5, 0.5, 0)], 1),
            engine.auto_fill(mesh, [(0.5, 0.5, 0)], 1, across_patterns=False),
            engine.inspect_topology(repair_mesh, fix_split_edges=True)[0],
            engine.inspect_topology(mesh, fix_split_edges=True)[0],
            engine.inspect_topology(mesh, fix_split_edges=False)[0],
            engine.merge_output_seams(mesh, 0),
            engine.retopologize(differential.curved_grid(), mesh, RetopoSettings(output_generated_planes=True)),
        ]
        for result in results:
            self.assertNotIn("weight", result.attributes)
            self.assertNotIn("corner_tag", result.attributes)
            self.assertNotIn("short_point", result.attributes)
            self.assertNotIn("stale", result.attributes)
            self.assertNotIn("weight", result.attribute_domains)
            self.assertNotIn("corner_tag", result.attribute_domains)
            self.assertNotIn("short_point", result.attribute_domains)
            self.assertNotIn("stale", result.attribute_domains)
            self.assertIn("source_vertex", result.attributes)
        line_mesh = weld_mesh.copy()
        line_mesh.vertices.append((9, 9, 0))
        line_mesh.lines = [(4, 8)]
        line_result = engine.merge_output_seams(line_mesh, 0.01)
        self.assertEqual([(0, 6)], line_result.lines)
        engine.subdivide_quads(line_result)
        attributed = differential.two_guide()
        attributed.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        for across_patterns in (True, False):
            with self.subTest(auto_fill_across_patterns=across_patterns):
                filled = engine.auto_fill(attributed, [(0.3, 0, 0)], 1, across_patterns=across_patterns)
                self.assertEqual({(0, 6): 0.5, (1, 6): 0.5}, filled.edge_attributes["crease_edge"])
                self.assertEqual(set(filled.edge_attributes["crease_edge"]) - set(filled.edges()), set())
                subdivided = engine.subdivide_quads(filled)
                self.assertEqual({0.5}, set(subdivided.edge_attributes["crease_edge"].values()))
        loose = differential.unit_guide()
        loose.lines = [(0, 2)]
        loose.edge_attributes["crease_edge"] = {(0, 2): 0.5}
        filled_loose = engine.auto_fill(loose, [(0.5, 0.5, 0)], 1, across_patterns=False)
        self.assertEqual(2, len(filled_loose.lines))
        self.assertNotIn((0, 2), filled_loose.lines)
        self.assertNotIn((0, 2), filled_loose.edge_attributes["crease_edge"])
        subdivided_loose = engine.subdivide_quads(filled_loose)
        self.assertTrue(subdivided_loose.lines)
        self.assertEqual(set(subdivided_loose.edge_attributes["crease_edge"]) - set(subdivided_loose.edges()), set())
        source = differential.curved_grid()
        source.attributes["point_guide"] = [index == 4 for index in range(9)]
        pipeline = engine.retopologize(source, loose, RetopoSettings(auto_fill_prepass=True, subdiv_reproject=1))
        self.assertTrue(pipeline.faces)
        for across_patterns in (True, False):
            with self.subTest(off_center_across_patterns=across_patterns):
                filled = engine.auto_fill(loose, [(0.52, 0.5, 0)], 1, across_patterns=across_patterns)
                face_vertices = {index for face in filled.faces for index in face}
                self.assertTrue(any(math.dist(filled.vertices[index], (0.52, 0.5, 0)) < 1e-7 for index in face_vertices))
        for across_patterns in (True, False):
            with self.subTest(iterative_off_center_across_patterns=across_patterns):
                filled = engine.auto_fill(loose, [(0.49, 0.5, 0), (0.5, 0.5, 0)], 5, across_patterns=across_patterns)
                face_vertices = {index for face in filled.faces for index in face}
                self.assertTrue(any(math.dist(filled.vertices[index], (0.49, 0.5, 0)) < 1e-7 for index in face_vertices))
        free = differential.unit_guide()
        free.vertices.append((5, 5, 0))
        free.lines = [(2, 4)]
        subdivided_free = engine.subdivide_quads(free)
        self.assertIn((5, 5, 0), subdivided_free.vertices)
        filled_free = engine.auto_fill(free, [(0.5, 0.5, 0)], 1)
        self.assertIn((5, 5, 0), filled_free.vertices)
        cross_component = differential.two_islands()
        cross_component.vertices.append((5, 5, 0))
        cross_component.lines = [(1, 4), (2, 8)]
        cross_component.edge_attributes["crease_edge"] = {(1, 4): 0.5, (2, 8): 0.7}
        filled_cross = engine.auto_fill(cross_component, [(0.3, 0, 0)], 1, across_patterns=False)
        original_lines = sorted(tuple(sorted((cross_component.vertices[first], cross_component.vertices[second]))) for first, second in cross_component.lines)
        filled_lines = sorted(tuple(sorted((filled_cross.vertices[first], filled_cross.vertices[second]))) for first, second in filled_cross.lines)
        self.assertEqual(original_lines, filled_lines)
        self.assertEqual([0.5, 0.7], sorted(filled_cross.edge_attributes["crease_edge"].values()))
        self.assertEqual(set(filled_cross.edge_attributes["crease_edge"]) - set(filled_cross.edges()), set())
        self.assertIn((5, 5, 0), filled_cross.vertices)
        twice_subdivided = engine.subdivide_quads(subdivided_loose)
        self.assertIn((0.5, 0.5, 0), twice_subdivided.vertices)
        self.assertEqual(set(twice_subdivided.edge_attributes["crease_edge"]) - set(twice_subdivided.edges()), set())
        beveled = differential.unit_guide()
        beveled.lines = [(0, 2)]
        beveled.edge_attributes["bevel_weight_edge"] = {(0, 2): 0.5}
        final_output = engine.retopologize(
            differential.curved_grid(), beveled,
            RetopoSettings(subdiv_reproject=1, separate_parts=False, fit_uv=False, seams_merge_distance=0),
        )
        self.assertEqual(set(final_output.edge_attributes.get("bevel_weight_edge", {})) - set(final_output.edges()), set())
        engine.subdivide_quads(final_output)
        six_point = differential.unit_guide()
        six_point.edge_attributes["crease_edge"] = {(0, 1): 0.5, (2, 3): 0.7}
        for across_patterns in (True, False):
            with self.subTest(six_point_across_patterns=across_patterns):
                filled = engine.auto_fill(six_point, [(index / 5, 0, 0) for index in range(6)], 1, across_patterns=across_patterns)
                self.assertEqual({(0, 4): 0.5, (1, 4): 0.5, (2, 5): 0.7, (3, 5): 0.7}, filled.edge_attributes["crease_edge"])
                self.assertEqual(set(filled.edge_attributes["crease_edge"]) - set(filled.edges()), set())
                engine.subdivide_quads(filled)
        face_line = differential.two_guide()
        face_line.lines = [(0, 1)]
        face_line.edge_attributes["crease_edge"] = {(0, 1): 0.5}
        for across_patterns in (True, False):
            with self.subTest(face_line_across_patterns=across_patterns):
                filled = engine.auto_fill(face_line, [(0.3, 0, 0)], 1, across_patterns=across_patterns)
                self.assertNotIn((0, 1), filled.lines)
                self.assertNotIn((0, 1), filled.edge_attributes["crease_edge"])
                self.assertEqual({(0, 6): 0.5, (1, 6): 0.5}, filled.edge_attributes["crease_edge"])
                self.assertEqual([(0, 6), (1, 6)], sorted(filled.lines))
                engine.subdivide_quads(filled)

    def test_reconstruction_preserves_explicit_attribute_domains(self):
        separated = engine._separate(
            Mesh(
                vertices=[(0, 0, 0), (0.5, 0, 0), (1, 0, 0), (0, 1, 0), (0.5, 1, 0), (1, 1, 0)],
                faces=[(0, 1, 4, 3), (1, 2, 5, 4)],
                uv=[(0, 0), (0.5, 0), (0.5, 1), (0, 1), (0.5, 0), (1, 0), (1, 1), (0.5, 1)],
                attributes={"pattern_id": [0, 1, 2, 3, 4, 5]},
                attribute_domains={},
            ),
            [0, 1],
        )
        self.assertEqual([0, 1, 4, 3, 1, 2, 5, 4], separated.attributes["pattern_id"])
        self.assertEqual("POINT", separated.attribute_domains["pattern_id"])
        merged = engine.merge_output_seams(
            Mesh(
                vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 0), (-1, 0, 0), (-1, -1, 0), (0, -1, 0)],
                faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
                attributes={"source_vertex": list(range(8)), "pattern_id": [10, 20]},
                attribute_domains={"pattern_id": "FACE"},
            ),
            0.01,
        )
        self.assertEqual([0, 1, 2, 3, 5, 6, 7], merged.attributes["source_vertex"])
        self.assertEqual([10, 20], merged.attributes["pattern_id"])
        positions = [(0, 0, 0), (2, 0, 0), (2, 2, 0), (0, 2, 0), (1, 1, 0), (1, 0.3, 0)]
        source_faces = [(0, 1, 5), (1, 4, 5), (4, 0, 5), (1, 2, 4), (2, 3, 4), (3, 0, 4)]
        equal_counts = Mesh(
            vertices=positions + [(-x, -y, z) for x, y, z in positions],
            faces=source_faces + [tuple(index + 6 for index in face) for face in source_faces],
            attributes={"source_vertex": list(range(12))},
        )
        merged_equal = engine.merge_output_seams(equal_counts, 0.01)
        self.assertEqual([0, 1, 2, 3, 4, 5, 7, 8, 9, 10, 11], merged_equal.attributes["source_vertex"])
        guide = differential.ring_guide()
        guide.attributes["pattern_id"] = list(range(16))
        thickened = engine.thicken(guide, 0.1)
        self.assertEqual(list(range(16)) * 2, thickened.attributes["pattern_id"])
        self.assertEqual("POINT", thickened.attribute_domains["pattern_id"])

    def test_mirror_normalizes_loose_line_attribute_keys(self):
        guide = Mesh(
            vertices=[(3, 0, 0), (1, 0, 0), (2, 0, 0), (2, 1, 0), (1, 1, 0)],
            faces=[(1, 2, 3, 4)],
            lines=[(0, 1)],
            edge_attributes={"crease_edge": {(0, 1): 0.7}},
        )
        result = engine.mirror_guides(guide, axes=("x",))
        self.assertTrue(all(edge == Mesh.edge_key(*edge) for edge in result.edge_attributes["crease_edge"]))
        self.assertEqual(2, len(result.edge_attributes["crease_edge"]))

    def test_repair_preserves_face_domains_when_vertex_count_crosses_face_count(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, -1, -1)],
            faces=[(0, 2, 1), (0, 1, 3), (1, 2, 3), (0, 3, 2), (0, 1, 4)],
            uv=[(face * 10 + corner, 0) for face in range(5) for corner in range(3)],
            attributes={
                "pattern_id": [10, 11, 12, 13, 14],
                "part_id": [20, 21, 22, 23, 24],
                "retopo_plane_id": [30, 31, 32, 33, 34],
                "source_vertex": [0, 1, 2, 3, 4],
            },
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        for name in ("pattern_id", "part_id", "retopo_plane_id"):
            self.assertEqual(mesh.attributes[name], result.attributes[name])
        self.assertEqual(len(result.vertices), len(result.attributes["source_vertex"]))
        for face_index, face in enumerate(mesh.faces):
            self.assertEqual(mesh.uv[face_index * 3:face_index * 3 + 3], result.uv[face_index * 3:face_index * 3 + 3])
            self.assertEqual([mesh.vertices[index] for index in face], [result.vertices[index] for index in result.faces[face_index]])

    def test_repair_uses_explicit_attribute_domains(self):
        point_mesh = differential.non_manifold()
        point_mesh.attributes = {"pattern_id": [10, 11, 12, 13, 14], "source_vertex": [0, 1, 2, 3, 4]}
        point_mesh.attribute_domains = {"pattern_id": "POINT", "source_vertex": "POINT"}
        point_result, _ = engine.inspect_topology(point_mesh, fix_split_edges=True)
        self.assertEqual([10, 11, 14, 10, 11, 12, 11, 10, 13], point_result.attributes["pattern_id"])
        self.assertEqual("POINT", point_result.attribute_domains["pattern_id"])
        face_mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, -1, -1)],
            faces=[(0, 2, 1), (0, 1, 3), (1, 2, 3), (0, 3, 2), (0, 1, 4)],
            attributes={
                "pattern_id": [10, 11, 12, 13, 14],
                "part_id": [20, 21, 22, 23, 24],
                "retopo_plane_id": [30, 31, 32, 33, 34],
                "source_position": [(0, 0, 0)] * 5,
            },
            attribute_domains={"pattern_id": "FACE", "part_id": "FACE", "retopo_plane_id": "FACE", "source_position": "POINT"},
        )
        face_result, _ = engine.inspect_topology(face_mesh, fix_split_edges=True)
        for name in ("pattern_id", "part_id", "retopo_plane_id"):
            self.assertEqual(face_mesh.attributes[name], face_result.attributes[name])
            self.assertEqual("FACE", face_result.attribute_domains[name])
        self.assertEqual(len(face_result.vertices), len(face_result.attributes["source_position"]))

    def test_repair_is_canonical_across_face_order(self):
        faces = [(1, 3, 5, 4), (2, 5, 4, 3), (0, 6, 2, 5), (5, 2, 4, 6), (5, 4, 0, 1), (3, 1, 0, 2)]
        vertices = [(index, 0, 0) for index in range(7)]
        first, _ = engine.inspect_topology(Mesh(vertices=vertices, faces=faces), fix_split_edges=True)
        order = (5, 0, 2, 3, 1, 4)
        second, _ = engine.inspect_topology(Mesh(vertices=vertices, faces=[faces[index] for index in order]), fix_split_edges=True)
        self.assertEqual(len(first.vertices), len(second.vertices))
        self.assertEqual(len(first.boundary_edges()), len(second.boundary_edges()))
        assert_manifold_connectors_preserved(self, Mesh(vertices=vertices, faces=faces), first)

    def test_duplicate_triangle_pairs_keep_their_manifold_edges(self):
        mesh = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0)],
            faces=[(2, 0, 3), (0, 2, 3), (2, 3, 1)],
        )
        result, _ = engine.inspect_topology(mesh, fix_split_edges=True)
        self.assertEqual([], engine.non_manifold_edges(result))
        assert_manifold_connectors_preserved(self, mesh, result)

    def test_mirror_does_not_swap_asymmetric_edge_attributes(self):
        guide = Mesh(
            vertices=[(-1, 0, 0), (1, 0, 0), (1, 1, 0), (-1, 1, 0), (-3, 0, 0), (-3, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 0, 3, 5)],
        )
        guide.edge_attributes["crease_edge"] = {edge: 0.0 for edge in guide.edges()}
        guide.edge_attributes["crease_edge"][(0, 3)] = 0.8
        mirrored = engine.mirror_guides(guide, axes=("x",))
        self.assertEqual(0.8, mirrored.edge_attributes["crease_edge"][(0, 3)])
        self.assertEqual(0.0, mirrored.edge_attributes["crease_edge"][(1, 2)])

    def test_mirror_and_separation_preserve_edge_attributes(self):
        guide = differential.unit_guide()
        guide.edge_attributes["bevel_weight_edge"] = {edge: 1.0 for edge in guide.edges()}
        guide.edge_attributes["crease_edge"] = {edge: 0.8 for edge in guide.edges()}
        guide.attributes["point_guide"] = [True, True, False, True]
        mirrored = engine.mirror_guides(guide, axes=("x",))
        self.assertEqual(len(mirrored.vertices), len(mirrored.attributes["point_guide"]))
        self.assertEqual(4, sum(mirrored.attributes["point_guide"]))
        self.assertTrue(mirrored.attributes["point_guide"][mirrored.vertices.index((-1.0, 0.0, 0.0))])
        self.assertTrue(all(value > 0 for value in mirrored.edge_attributes["bevel_weight_edge"].values()))
        self.assertTrue(all(value > 0 for value in mirrored.edge_attributes["crease_edge"].values()))
        self.assertEqual(7, len(mirrored.edge_attributes["bevel_weight_edge"]))
        self.assertEqual(7, len(mirrored.edge_attributes["crease_edge"]))
        separated = engine.retopologize(
            differential.curved_grid(), guide, RetopoSettings(fit_uv=False, separate_parts=True)
        )
        self.assertEqual(16, len(separated.edge_attributes["bevel_weight_edge"]))
        self.assertTrue(all(value > 0 for value in separated.edge_attributes["bevel_weight_edge"].values()))

    def test_separation_edge_attributes_reference_only_real_group_edges(self):
        mesh = Mesh(
            vertices=[(index, 0, 0) for index in range(13)],
            faces=[(0, 1, 2, 3), (0, 7, 8, 9), (1, 10, 11, 12)],
            uv=[(0, 0)] * 12,
            edge_attributes={"bevel_weight_edge": {(0, 1): 1.0}},
        )
        result = engine._separate(mesh, [0, 1, 1])
        self.assertTrue(set(result.edge_attributes["bevel_weight_edge"]) <= set(result.edges()))

    def test_plane_ids_follow_connected_components(self):
        guide = Mesh(
            vertices=[(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (2, 0, 0), (3, 0, 0), (3, 1, 0), (2, 1, 0)],
            faces=[(0, 1, 2, 3), (4, 5, 6, 7)],
        )
        result = engine.retopologize(differential.curved_grid(), guide, RetopoSettings(fit_uv=False, seams_merge_distance=0))
        self.assertEqual(2, len(result.connected_components()))
        self.assertEqual(2, len(set(result.attributes["retopo_plane_id"])))
        for component in result.connected_components():
            self.assertEqual(1, len({result.attributes["retopo_plane_id"][index] for index in component}))

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
