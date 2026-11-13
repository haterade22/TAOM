#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/mesh_preflight.py.

Run:  python -m unittest tools.tests.test_mesh_preflight

The script imports bpy, bmesh and mathutils only inside its Blender-side functions, so the rules
load here without stubs. The Blender side (reading a mesh, the ray test behind inverted_share) is
proven by the report a run writes and by the canary in docs/features/blender-asset-gates.md.
"""
import importlib.util
import math
import os
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "mesh_preflight.py")

_spec = importlib.util.spec_from_file_location("mesh_preflight", SCRIPT)
mp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mp)

CUBE_VERTS = [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]
CUBE_FACES = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]


def mesh(name="sm_gd_crate_001", **over):
    """A mesh record that breaks no rule; each test changes one field."""
    m = {"name": name, "data_users": 1, "identity": True, "scale": [1.0, 1.0, 1.0], "mirrored": False,
         "tris": 12, "uv_layers": ["UVMap"], "collapsed_uv_share": 0.0,
         "slots": [{"name": "m_gd_crate", "faces": 6}], "inverted_share": 0.0, "inverted_faces": 0,
         "topo": mp.topology_stats(CUBE_VERTS, CUBE_FACES), "skin": None}
    m.update(over)
    return m


def codes(findings, severity=None):
    return sorted(f["code"] for f in findings if severity is None or f["severity"] == severity)


class ClassifyNameTests(unittest.TestCase):
    def test_plain_mesh_is_a_base(self):
        c = mp.classify_name("sm_gd_wall_001")
        self.assertEqual((c["kind"], c["base"], c["metamesh"]), ("base", "sm_gd_wall_001", "sm_gd_wall_001"))
        self.assertFalse(c["dup"] or c["upper"] or c["cloth"])

    def test_both_lod_spellings(self):
        for name in ("sm_gd_wall_001.lod2", "sm_gd_wall_001_lod2"):
            c = mp.classify_name(name)
            self.assertEqual((c["kind"], c["base"]), ("lod", "sm_gd_wall_001"), name)

    def test_collision_body(self):
        c = mp.classify_name("bo_sm_gd_wall_001")
        self.assertEqual((c["kind"], c["base"]), ("bo", "sm_gd_wall_001"))

    def test_sub_mesh_shares_its_metamesh(self):
        self.assertEqual(mp.classify_name("sm_gd_house.roof")["metamesh"], "sm_gd_house")

    def test_blender_duplicate_suffix(self):
        self.assertTrue(mp.classify_name("sm_gd_wall_001.001")["dup"])
        self.assertFalse(mp.classify_name("sm_gd_wall_001")["dup"])  # _001 is a kit number, not a duplicate

    def test_lod_zero_spelled_out_is_the_base(self):
        # armory-guide.md "Naming": LOD0 is the bare name or <name>_lod0
        for name in ("Dwarf_Hair_A_lod0", "dwarf_hair_a.lod0"):
            c = mp.classify_name(name)
            self.assertEqual((c["kind"], c["base"].lower()), ("base", "dwarf_hair_a"), name)

    def test_duplicate_suffix_does_not_hide_a_lod_or_a_body(self):
        lod = mp.classify_name("sm_x.lod1.001")
        self.assertEqual((lod["kind"], lod["base"], lod["dup"]), ("lod", "sm_x", True))
        body = mp.classify_name("bo_sm_x.001")
        self.assertEqual((body["kind"], body["base"], body["dup"]), ("bo", "sm_x", True))

    def test_upper_case_and_cloth(self):
        self.assertTrue(mp.classify_name("SM_Gd_Wall")["upper"])
        self.assertTrue(mp.classify_name("clo_cape_a")["cloth"])
        self.assertFalse(mp.classify_name("sm_cloak_a")["cloth"])


class TopologyStatsTests(unittest.TestCase):
    def test_closed_cube(self):
        t = mp.topology_stats(CUBE_VERTS, CUBE_FACES)
        self.assertEqual((t["tris"], t["ngons"], t["boundary_edges"], t["non_manifold_edges"],
                          t["loose_verts"], t["zero_area_faces"]), (12, 0, 0, 0, 0, 0))
        self.assertTrue(t["closed"])

    def test_open_cube_has_a_boundary(self):
        t = mp.topology_stats(CUBE_VERTS, CUBE_FACES[1:])
        self.assertEqual(t["boundary_edges"], 4)
        self.assertFalse(t["closed"])

    def test_edge_with_three_faces_is_non_manifold(self):
        verts = [(0, 0, 0), (1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1)]
        t = mp.topology_stats(verts, [[0, 1, 2], [0, 1, 3], [0, 1, 4]])
        self.assertEqual(t["non_manifold_edges"], 1)

    def test_ngon_loose_vertex_and_zero_area_face(self):
        verts = [(0, 0, 0), (1, 0, 0), (2, 1, 0), (1, 2, 0), (0, 1, 0), (9, 9, 9), (5, 5, 5), (6, 6, 6)]
        t = mp.topology_stats(verts, [[0, 1, 2, 3, 4], [0, 6, 7]])  # a pentagon, and three points on a line
        self.assertEqual((t["ngons"], t["tris"], t["loose_verts"], t["zero_area_faces"]), (1, 4, 1, 1))

    def test_no_faces_is_not_closed(self):
        self.assertFalse(mp.topology_stats([(0, 0, 0)], [])["closed"])


class RayRuleTests(unittest.TestCase):
    def test_sphere_dirs_are_unit_and_cover_both_halves(self):
        dirs = mp.sphere_dirs(48)
        self.assertEqual(len(dirs), 48)
        for d in dirs:
            self.assertAlmostEqual(math.sqrt(sum(c * c for c in d)), 1.0, places=6)
        self.assertTrue(any(d[2] > 0.5 for d in dirs) and any(d[2] < -0.5 for d in dirs))

    def test_inverted_only_when_hidden_in_front_and_seen_from_behind(self):
        self.assertTrue(mp.is_inverted(0.0, 1.0))
        self.assertTrue(mp.is_inverted(0.0, 0.5))
        self.assertFalse(mp.is_inverted(0.25, 1.0))  # seen along its normal: a normal face
        self.assertFalse(mp.is_inverted(0.0, 0.25))  # mostly buried: a face inside the solid
        self.assertFalse(mp.is_inverted(0.0, 0.0))


class SkinCountTests(unittest.TestCase):
    BONES = {"spine", "arm_l", "arm_r", "head", "neck"}

    def test_only_bone_groups_count_as_weight(self):
        verts = [[("spine", 1.0)], [("mask", 1.0)], [], [("spine", 0.0)]]
        self.assertEqual(mp.skin_counts(verts, self.BONES), (3, 0))

    def test_a_helper_group_is_not_a_fifth_influence(self):
        four = [("spine", 0.25), ("arm_l", 0.25), ("arm_r", 0.25), ("head", 0.25)]
        self.assertEqual(mp.skin_counts([four + [("pin", 1.0)]], self.BONES), (0, 0))
        self.assertEqual(mp.skin_counts([four + [("neck", 0.1)]], self.BONES), (0, 1))


class SampleStepTests(unittest.TestCase):
    def test_small_meshes_read_every_face(self):
        self.assertEqual(mp.sample_step(mp.MAX_RAY_FACES), 1)

    def test_large_meshes_read_an_even_sample_under_the_cap(self):
        step = mp.sample_step(mp.MAX_RAY_FACES * 2 + 1)
        self.assertEqual(step, 3)
        self.assertLessEqual(len(range(0, mp.MAX_RAY_FACES * 2 + 1, step)), mp.MAX_RAY_FACES)


class CollapsedUvTests(unittest.TestCase):
    def test_share_is_weighted_by_surface_area(self):
        self.assertAlmostEqual(mp.collapsed_uv_share([(0.0, 1.0), (0.2, 3.0)]), 0.25)

    def test_no_surface_is_zero_and_zero_area_faces_are_ignored(self):
        self.assertEqual(mp.collapsed_uv_share([]), 0.0)
        self.assertEqual(mp.collapsed_uv_share([(0.0, 0.0), (0.1, 1.0)]), 0.0)


class CheckMeshTests(unittest.TestCase):
    def test_clean_mesh_has_no_findings(self):
        self.assertEqual(mp.check_mesh(mesh()), [])

    def test_kit_frame_scale_is_accepted(self):
        m = mesh(identity=False, scale=[0.01, 0.01, 0.01])
        self.assertEqual(mp.check_mesh(m), [])

    def test_material_with_duplicate_suffix_blocks(self):
        m = mesh(slots=[{"name": "m_gd_crate.001", "faces": 6}])
        self.assertEqual(codes(mp.check_mesh(m), "blocker"), ["material-duplicate-suffix"])

    def test_empty_slot_in_use_is_major_and_unused_slot_is_minor(self):
        m = mesh(slots=[{"name": "", "faces": 4}, {"name": "m_spare", "faces": 0}, {"name": "m_ok", "faces": 2}])
        found = mp.check_mesh(m)
        self.assertEqual(codes(found, "major"), ["material-empty-slot"])
        self.assertEqual(codes(found, "minor"), ["material-unused-slot"])
        self.assertEqual(codes(found, "blocker"), [])

    def test_visual_mesh_without_any_slot_is_major(self):
        self.assertEqual(codes(mp.check_mesh(mesh(slots=[]))), ["material-empty-slot"])
        self.assertEqual(codes(mp.check_mesh(mesh(slots=[])), "major"), ["material-empty-slot"])

    def test_visual_mesh_without_uvs_blocks(self):
        self.assertEqual(codes(mp.check_mesh(mesh(uv_layers=[], collapsed_uv_share=None))), ["uv-missing"])

    def test_collision_body_needs_no_uvs_but_a_physics_material(self):
        body = mesh("bo_sm_gd_crate_001", uv_layers=[], collapsed_uv_share=None,
                    slots=[{"name": "wood", "faces": 6}])
        self.assertEqual(mp.check_mesh(body), [])
        bare = dict(body, slots=[])
        self.assertEqual(codes(mp.check_mesh(bare), "major"), ["body-no-physics-material"])

    def test_collision_body_skips_the_visual_checks(self):
        body = mesh("bo_sm_gd_crate_001", slots=[{"name": "wood", "faces": 6}], inverted_share=0.5,
                    inverted_faces=3, collapsed_uv_share=0.9)
        self.assertEqual(mp.check_mesh(body), [])

    def test_collapsed_uvs_over_the_limit_are_major(self):
        self.assertEqual(codes(mp.check_mesh(mesh(collapsed_uv_share=0.02)), "major"), ["uv-collapsed"])
        self.assertEqual(mp.check_mesh(mesh(collapsed_uv_share=0.005)), [])

    def test_second_uv_layer_is_a_note(self):
        self.assertEqual(codes(mp.check_mesh(mesh(uv_layers=["UVMap", "UVMap.001"])), "note"), ["uv-extra-layers"])

    def test_inverted_faces_block_only_when_a_large_share_is_wound_inward(self):
        found = mp.check_mesh(mesh(inverted_share=0.06, inverted_faces=900))
        self.assertEqual(codes(found, "blocker"), ["faces-inverted"])

    def test_inverted_faces_between_the_two_limits_are_major(self):
        # the approved erebor_dwarf_house_01 measures 0.99%: thin trim slabs, no hole in game
        found = mp.check_mesh(mesh(inverted_share=0.0099, inverted_faces=36))
        self.assertEqual(codes(found, "major"), ["faces-inverted-suspect"])
        self.assertEqual(codes(found, "blocker"), [])

    def test_a_few_inverted_faces_are_a_note(self):
        self.assertEqual(codes(mp.check_mesh(mesh(inverted_share=0.0005, inverted_faces=2)), "note"),
                         ["faces-inverted-few"])

    def test_interior_shell_turns_inverted_faces_into_a_note(self):
        found = mp.check_mesh(mesh(inverted_share=0.9, inverted_faces=6), interior=True)
        self.assertEqual(codes(found, "note"), ["faces-inverted-interior"])
        self.assertEqual(codes(found, "blocker"), [])

    def test_mirrored_object_is_major(self):
        self.assertEqual(codes(mp.check_mesh(mesh(mirrored=True, identity=False)), "major"), ["transform-mirrored"])

    def test_shared_data_with_a_transform_is_major_but_not_at_identity(self):
        moved = mesh(data_users=3, identity=False)
        self.assertEqual(codes(mp.check_mesh(moved), "major"), ["transform-multi-user"])
        self.assertEqual(mp.check_mesh(mesh(data_users=3)), [])

    def test_non_uniform_scale_is_minor(self):
        m = mesh(identity=False, scale=[1.0, 2.0, 1.0])
        self.assertEqual(codes(mp.check_mesh(m)), ["transform-non-uniform-scale"])
        self.assertEqual(codes(mp.check_mesh(m), "minor"), ["transform-non-uniform-scale"])

    def test_duplicate_suffix_on_the_object_is_major_and_upper_case_a_note(self):
        self.assertEqual(codes(mp.check_mesh(mesh("sm_gd_crate_001.001")), "major"), ["name-duplicate-suffix"])
        self.assertEqual(codes(mp.check_mesh(mesh("SM_Gd_Crate")), "note"), ["name-upper-case"])

    def test_skin_rules(self):
        ok = mesh(skin={"unweighted": 0, "over_limit": 0, "bones": 64})
        self.assertEqual(mp.check_mesh(ok), [])
        bad = mesh(skin={"unweighted": 2, "over_limit": 5, "bones": 65})
        found = mp.check_mesh(bad)
        self.assertEqual(codes(found, "blocker"), ["skin-too-many-bones", "skin-unweighted"])
        self.assertEqual(codes(found, "major"), ["skin-over-four-influences"])

    def test_topology_counts_are_notes_never_more(self):
        topo = dict(mp.topology_stats(CUBE_VERTS, CUBE_FACES), non_manifold_edges=3, loose_verts=1,
                    zero_area_faces=2, ngons=4)
        found = mp.check_mesh(mesh(topo=topo))
        self.assertEqual(codes(found), ["topo-loose-verts", "topo-ngons", "topo-non-manifold", "topo-zero-area"])
        self.assertEqual({f["severity"] for f in found}, {"note"})


class CheckSetTests(unittest.TestCase):
    BODY = {"name": "wood", "faces": 6}

    def test_prop_without_its_body_blocks(self):
        self.assertEqual(codes(mp.check_set([mesh()], "prop"), "blocker"), ["body-missing"])

    def test_prop_with_its_body_passes(self):
        pair = [mesh(), mesh("bo_sm_gd_crate_001", slots=[self.BODY])]
        self.assertEqual(mp.check_set(pair, "prop"), [])

    def test_body_name_matches_without_case(self):
        pair = [mesh("SM_Gd_Crate"), mesh("bo_sm_gd_crate", slots=[self.BODY])]
        self.assertEqual(codes(mp.check_set(pair, "prop"), "blocker"), [])

    def test_one_body_serves_the_sub_meshes_of_its_prefix(self):
        parts = [mesh("sm_gd_house"), mesh("sm_gd_house.roof"), mesh("bo_sm_gd_house", slots=[self.BODY])]
        self.assertEqual(mp.check_set(parts, "prop"), [])

    def test_a_body_with_a_duplicate_suffix_is_a_name_defect_not_a_missing_body(self):
        pair = [mesh("sm_x"), mesh("bo_sm_x.001", slots=[self.BODY])]
        found = mp.check_set(pair, "prop")
        self.assertEqual(codes(found, "blocker"), [])
        self.assertEqual(codes(found, "major"), ["name-duplicate-suffix"])

    def test_a_lod_with_a_duplicate_suffix_stays_out_of_the_budget(self):
        parts = [mesh("sm_x", tris=100), mesh("sm_x.lod1.001", tris=50)]
        self.assertEqual(codes(mp.check_set(parts, "mesh", budget_tris=120)), ["name-duplicate-suffix"])

    def test_lod_zero_spelled_out_counts_in_the_budget_and_takes_a_body(self):
        parts = [mesh("sm_x_lod0", tris=100), mesh("sm_x_lod1", tris=50)]
        self.assertEqual(codes(mp.check_set(parts, "mesh", budget_tris=90), "major"), ["over-budget"])
        self.assertEqual(codes(mp.check_set(parts, "prop"), "blocker"), ["body-missing"])

    def test_lods_and_cloth_need_no_body_of_their_own(self):
        parts = [mesh(), mesh("sm_gd_crate_001.lod1"), mesh("clo_gd_banner"),
                 mesh("bo_sm_gd_crate_001", slots=[self.BODY])]
        self.assertEqual(mp.check_set(parts, "prop"), [])

    def test_plain_mesh_profile_asks_for_no_body(self):
        self.assertEqual(mp.check_set([mesh()], "mesh"), [])

    def test_map_icon_with_a_body_is_major(self):
        pair = [mesh(), mesh("bo_sm_gd_crate_001", slots=[self.BODY])]
        self.assertEqual(codes(mp.check_set(pair, "icon"), "major"), ["icon-has-body"])
        self.assertEqual(mp.check_set([mesh()], "icon"), [])

    def test_budget_counts_only_visual_lod0(self):
        parts = [mesh(tris=600), mesh("sm_gd_crate_001.lod1", tris=300),
                 mesh("bo_sm_gd_crate_001", slots=[self.BODY], tris=200), mesh("sm_gd_lid_001", tris=500)]
        self.assertEqual(codes(mp.check_set(parts, "mesh", budget_tris=1000), "major"), ["over-budget"])
        self.assertEqual(mp.check_set(parts, "mesh", budget_tris=1100), [])

    def test_no_meshes_is_a_blocker_not_a_pass(self):
        self.assertEqual(codes(mp.check_set([], "mesh"), "blocker"), ["no-meshes"])


class VerdictTests(unittest.TestCase):
    def f(self, severity):
        return {"severity": severity, "code": "x", "mesh": "m", "message": ""}

    def test_blocker_fails(self):
        self.assertEqual(mp.verdict([self.f("note"), self.f("blocker")]), "FAIL")

    def test_major_or_minor_passes_with_notes(self):
        self.assertEqual(mp.verdict([self.f("major")]), "PASS WITH NOTES")
        self.assertEqual(mp.verdict([self.f("minor")]), "PASS WITH NOTES")

    def test_notes_alone_pass(self):
        self.assertEqual(mp.verdict([self.f("note")]), "PASS")
        self.assertEqual(mp.verdict([]), "PASS")


class ParseArgsTests(unittest.TestCase):
    def test_fbx_and_out(self):
        a = mp.parse_args(["--fbx", "a.fbx", "--out", "o"])
        self.assertEqual((a.fbx, a.blend, a.out, a.profile, a.budget_tris, a.interior),
                         ("a.fbx", None, "o", "mesh", None, False))

    def test_blend_profile_budget_interior(self):
        a = mp.parse_args(["--blend", "a.blend", "--out", "o", "--profile", "icon", "--budget-tris", "4000",
                           "--interior"])
        self.assertEqual((a.blend, a.profile, a.budget_tris, a.interior), ("a.blend", "icon", 4000, True))

    def test_one_source_is_required_and_only_one(self):
        for argv in (["--out", "o"], ["--fbx", "a.fbx", "--blend", "b.blend", "--out", "o"]):
            with self.assertRaises(SystemExit):
                mp.parse_args(argv)

    def test_unknown_profile_is_refused(self):
        with self.assertRaises(SystemExit):
            mp.parse_args(["--fbx", "a.fbx", "--out", "o", "--profile", "weapon"])


class ReportTests(unittest.TestCase):
    def test_report_carries_verdict_counts_and_findings(self):
        r = mp.build_report([mesh(slots=[{"name": "m.001", "faces": 6}])], "mesh")
        self.assertEqual(r["verdict"], "FAIL")
        self.assertEqual(r["counts"], {"blocker": 1, "major": 0, "minor": 0, "note": 0})
        self.assertEqual(r["findings"][0]["mesh"], "sm_gd_crate_001")
        self.assertEqual(r["profile"], "mesh")

    def test_findings_are_ordered_worst_first(self):
        m = mesh("SM_Crate", slots=[{"name": "m.001", "faces": 6}], mirrored=True, identity=False)
        r = mp.build_report([m], "mesh")
        self.assertEqual([f["severity"] for f in r["findings"]], ["blocker", "major", "note"])


if __name__ == "__main__":
    unittest.main()
