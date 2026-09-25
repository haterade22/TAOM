#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/lod_fill_batch.py.

Run:  python -m unittest tools.tests.test_lod_fill_batch

The chains are the shapes the 2026-09-25 census of the live Armory found. Blender runs are proven
by their own reports and by the independent diff the driver checks, not here.
"""
import os
import sys
import tempfile
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)

import audit_fbx_lods as afl  # noqa: E402
import lod_fill_batch as lfb  # noqa: E402


class FillTargetTests(unittest.TestCase):
    def test_lod0_only_takes_the_standard_chain(self):
        """70/30/15/7/3%: the elf and dwarf basemeshes' and hairs A to E's own chain."""
        self.assertEqual(lfb.fill_targets({0: 6484}), {1: 4539, 2: 1945, 3: 973, 4: 454, 5: 195})

    def test_a_gap_is_interpolated_between_its_neighbours(self):
        """warg_low: L0 24,525, L1 6,130, L4 976. L2 and L3 sit geometrically between L1 and L4;
        L5 continues the standard step below L4."""
        # L2 = 6130^(2/3) * 976^(1/3) = 3322.4, L3 = sqrt(L2 * 976) = 1800.7, L5 = 976 * 3/7
        self.assertEqual(lfb.fill_targets({0: 24525, 1: 6130, 4: 976}), {2: 3322, 3: 1801, 5: 418})

    def test_existing_levels_are_never_planned(self):
        self.assertEqual(lfb.fill_targets({n: 100 - n for n in range(6)}), {})
        self.assertNotIn(6, lfb.fill_targets({0: 1000, 2: 300, 6: 10}))

    def test_tiny_meshes_floor_at_four_tris(self):
        self.assertEqual(lfb.fill_targets({0: 24, 1: 24}), {2: 10, 3: 5, 4: 4, 5: 4})


def mesh(name, tris=100):
    return afl.FbxMesh(name, tris, tris, [], ["m"])


class PlanTests(unittest.TestCase):
    CATALOGUE = {
        "sk_boots_a": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "Y"},
        "sk_boots_b": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "N"},
        "clo_cape": {"tpacs": {"dale/boots_geo.tpac"}, "referenced": "Y"},
        "sk_head": {"tpacs": {"Race Test/uruk_geo.tpac"}, "referenced": "Y"},
        "sk_hair": {"tpacs": {"Race Test/uruk_geo.tpac"}, "referenced": "N"},
    }

    def plan(self, rel, meshes, skins=(), body=()):
        return lfb.plan_for(rel, meshes, self.CATALOGUE, set(skins), set(body))

    def test_only_used_shipped_incomplete_chains(self):
        p = self.plan("dale/boots.fbx", [
            mesh("sk_boots_a", 6484), mesh("sk_boots_b", 5000), mesh("clo_cape", 900),
            mesh("sk_boots_a.pillow.001", 10), mesh("bo_sk_boots_a", 12)])
        self.assertEqual([m["base"] for m in p["meshes"]], ["sk_boots_a"])
        self.assertEqual(p["meshes"][0]["levels"], {"1": 4539, "2": 1945, "3": 973, "4": 454, "5": 195})
        self.assertFalse(p["meshes"][0]["lock"])

    def test_seams_lock_on_body_parts_of_a_race_file_not_on_hair(self):
        p = self.plan("Race Test/uruk.fbx", [mesh("SK_Head", 3590), mesh("SK_Head.eyes", 176),
                                             mesh("SK_Hair", 9000)],
                      skins={"sk_head", "sk_hair"}, body={"sk_head"})
        locks = {m["base"]: m["lock"] for m in p["meshes"]}
        self.assertEqual(locks, {"SK_Head": True, "SK_Head.eyes": True, "SK_Hair": False})

    def test_nothing_to_do_is_an_empty_plan(self):
        full = [mesh("sk_boots_a")] + [mesh("sk_boots_a.lod%d" % n) for n in range(1, 6)]
        self.assertEqual(self.plan("dale/boots.fbx", full)["meshes"], [])


class VerifyTests(unittest.TestCase):
    PLAN = {"meshes": [{"base": "sk_boots_a", "levels": {"4": 454, "5": 195}},
                       {"base": "Dwarf_Hair_A", "levels": {"4": 1279}}]}

    def test_only_planned_additions_pass(self):
        lines = ["+ sk_boots_a.lod4: 454 tris", "+ sk_boots_a.lod5: 195 tris",
                 "+ Dwarf_Hair_A_lod4: 1279 tris",
                 "~ Dwarf_Hair_A_lod0: channel labels renamed, shape geometry and order unchanged"]
        self.assertEqual(lfb.verify(lines, self.PLAN), [])

    def test_anything_else_is_a_problem(self):
        problems = lfb.verify(["+ sk_boots_a.lod3: 900 tris", "- bo_sk_boots_a",
                               "~ sk_boots_a: tris 6484 -> 6600"], self.PLAN)
        self.assertEqual(len(problems), 3)

    def test_dropped_degenerate_faces_on_an_existing_level_pass(self):
        """Blender's importer drops degenerate triangles (a vertex used twice), so a round trip
        can take 1 to 14 off an artist's low LOD (the Dale chests) with the vertex count intact."""
        lines = ["~ sk_dale_chest_chivalry_a04_lod5: tris 619 -> 605",
                 "~ sk_dale_chest_chivalry_a03_lod2: tris 5428 -> 5427"]
        self.assertEqual(lfb.verify(lines, self.PLAN), [])
        self.assertEqual(lfb.degenerate_drops(lines), 2)

    def test_a_tiny_collision_body_may_lose_two(self):
        """t_isengard_weapon_set4_nohand: `bo_wm_isengard_shield_a02_clean` 14 -> 12, over 5% of
        a 14-triangle hull but the same zero-area faces."""
        self.assertEqual(lfb.verify(["~ bo_wm_isengard_shield_a02_clean: tris 14 -> 12"], self.PLAN), [])
        self.assertEqual(len(lfb.verify(["~ bo_wm_isengard_shield_a02_clean: tris 14 -> 11"], self.PLAN)), 1)

    def test_a_real_geometry_change_on_an_existing_level_fails(self):
        for line in ("~ sk_boots_a: tris 600 -> 500",                 # 17% gone
                     "~ sk_boots_a: tris 600 -> 610",                 # grew
                     "~ sk_boots_a: tris 600 -> 599; verts 400 -> 398"):
            self.assertEqual(len(lfb.verify([line], self.PLAN)), 1, line)

    def test_bind_pose_within_round_off(self):
        self.assertTrue(lfb.bind_ok((49, 3.8e-5, 0.0013, 314.8)))     # the warg
        self.assertTrue(lfb.bind_ok((60, 4.3e-5, 5.96e-5, 3.996)))    # the elephant, 1.5e-5 of the rig
        self.assertFalse(lfb.bind_ok((49, 3.8e-5, 0.5, 314.8)))       # a bone moved 5 mm
        self.assertFalse(lfb.bind_ok((28, 0.2, 1e-6, 1.9)))           # a bone turned
        self.assertTrue(lfb.bind_ok((0, 0.0, 0.0, 0.0)))              # no rig at all


class FixTests(unittest.TestCase):
    CATALOGUE = {"sk_chest": {"tpacs": {"g/c_geo.tpac"}, "referenced": "Y"},
                 "sk_glv": {"tpacs": {"g/c_geo.tpac"}, "referenced": "Y"}}

    def test_a_misnamed_lod_takes_its_gap_and_replaces_the_generated_copy(self):
        """The Lamedon slim chest: `.lod` (10,091 tris) sits in the gap the fill batch already
        filled with a generated `.lod1`; the artist's mesh wins."""
        meshes = [mesh("sk_chest", 18154), mesh("sk_chest.lod1", 9449), mesh("sk_chest.lod", 10091)]
        fix = {"delete": ["sk_chest.lod1"], "renames": {"sk_chest.lod": "sk_chest.lod1"}}
        fixed, applied = lfb.apply_fixes(meshes, fix)
        self.assertEqual([(m.name, m.tris) for m in fixed], [("sk_chest", 18154), ("sk_chest.lod1", 10091)])
        self.assertEqual(applied, fix)

    def test_a_delete_of_a_level_that_was_never_generated_is_dropped(self):
        meshes = [mesh("sk_chest", 18154), mesh("sk_chest.lod", 10091)]
        fixed, applied = lfb.apply_fixes(meshes, {"delete": ["sk_chest.lod1"],
                                                  "renames": {"sk_chest.lod": "sk_chest.lod1"}})
        self.assertEqual(applied["delete"], [])
        self.assertEqual([m.name for m in fixed], ["sk_chest", "sk_chest.lod1"])

    def test_a_rename_of_a_missing_object_is_refused(self):
        with self.assertRaises(SystemExit):
            lfb.apply_fixes([mesh("sk_chest")], {"renames": {"sk_chest.lod": "sk_chest.lod1"}})

    def test_the_plan_fills_after_the_fix_and_carries_it(self):
        meshes = [mesh("sk_glv.lod0", 3636)] + [mesh("sk_glv.lod%d" % n, 100) for n in range(1, 6)] + [
            mesh("sk_glv.lod0.001", 7472)]
        plan = lfb.plan_for("g/c.fbx", meshes, self.CATALOGUE, set(), set(),
                            fix={"renames": {"sk_glv.lod0.001": "sk_glv.b.lod0"}})
        self.assertEqual(plan["renames"], {"sk_glv.lod0.001": "sk_glv.b.lod0"})
        self.assertEqual([m["base"] for m in plan["meshes"]], ["sk_glv.b"])
        lines = ["- sk_glv.lod0.001", "+ sk_glv.b.lod0: 7472 tris"] + [
            "+ sk_glv.b.lod%s: 1 tris" % lv for lv in plan["meshes"][0]["levels"]]
        self.assertEqual(lfb.verify(lines, plan), [])
        self.assertEqual(lfb.expected_additions(plan, {"sk_glv.lod0", "sk_glv.lod0.001"}), 6)

    def test_verify_accepts_a_rename_that_replaces_a_deleted_level(self):
        plan = {"meshes": [], "delete": ["sk_chest.lod1"], "renames": {"sk_chest.lod": "sk_chest.lod1"}}
        self.assertEqual(lfb.verify(["- sk_chest.lod", "~ sk_chest.lod1: tris 9449 -> 10091"], plan), [])
        self.assertEqual(len(lfb.verify(["- sk_chest.lod2"], plan)), 1)


class MaterialFixTests(unittest.TestCase):
    def meshes(self):
        return [afl.FbxMesh("wm_cave_troll_shield_a01.lod0", 10, 10, [], ["t_cave_troll_set2.001"]),
                afl.FbxMesh("wm_cave_troll_shield_a01.lod5", 4, 4, [], ["t_cave_troll_set2.001"]),
                afl.FbxMesh("roh_nbl_shldr_cs.b.lod0", 9, 9, [], ["Material.002", "Material.006"]),
                afl.FbxMesh("roh_nbl_shldr_cs.lod0", 9, 9, [], ["Material.023", "Material.021"]),
                afl.FbxMesh("bo_wm_cave_troll_shield_a01", 9, 9, [], ["t_cave_troll_set2.001"])]

    def test_slot_renames_apply_to_every_object_but_collision_bodies(self):
        got = lfb.expand_materials(self.meshes(), {"slots": {"t_cave_troll_set2.001": "t_cave_troll_set2"}})
        self.assertEqual(got, {"wm_cave_troll_shield_a01.lod0": ["t_cave_troll_set2"],
                               "wm_cave_troll_shield_a01.lod5": ["t_cave_troll_set2"]})

    def test_a_chain_rule_beats_the_file_rule_and_star_takes_every_slot(self):
        got = lfb.expand_materials(self.meshes(), {
            "slots": {"Material.002": "roh_nbl_gorg"},
            "chains": {"roh_nbl_shldr_cs": {"*": "roh_nbl_arm_pads"},
                       "roh_nbl_shldr_cs.b": {"Material.002": "roh_nbl_arm_pads", "Material.006": "roh_nbl_clk"}}})
        self.assertEqual(got["roh_nbl_shldr_cs.lod0"], ["roh_nbl_arm_pads", "roh_nbl_arm_pads"])
        self.assertEqual(got["roh_nbl_shldr_cs.b.lod0"], ["roh_nbl_arm_pads", "roh_nbl_clk"])

    def test_verify_accepts_the_planned_material_change_only(self):
        plan = {"meshes": [], "materials": {"x.lod0": ["t_cave_troll_set2"], "y.lod0": ["a", "a"]}}
        ok = ["~ x.lod0: materials ['t_cave_troll_set2.001'] -> ['t_cave_troll_set2']",
              "~ y.lod0: materials ['m1', 'm2'] -> ['a']"]
        self.assertEqual(lfb.verify(ok, plan), [])
        bad = ["~ x.lod0: materials ['t_cave_troll_set2.001'] -> ['t_cave_troll_set1']",
               "~ z.lod0: materials ['q'] -> ['r']",
               "~ x.lod0: tris 10 -> 9; materials ['t_cave_troll_set2.001'] -> ['t_cave_troll_set2']"]
        self.assertEqual(len(lfb.verify(bad, plan)), 2)   # the third is a drop plus the planned change


class SkinsBodyTests(unittest.TestCase):
    def test_body_attributes_only_not_hair(self):
        xml = b"""<skins><race id="x"><skin name="man" body_meta_mesh="SK_Body"
            face_meta_mesh="sk_head" hands_mesh="sk_hands" legs_mesh="sk_legs"
            body_meta_mesh_shoulders="sk_sh" underwear_bottom_mesh="">
            <hair_meshes><hair_mesh name="sk_hair" cover_type1="sk_hair_c" /></hair_meshes>
            </skin></race></skins>"""
        fd, path = tempfile.mkstemp(suffix=".xml")
        with os.fdopen(fd, "wb") as f:
            f.write(xml)
        try:
            self.assertEqual(lfb.skins_body_meshes(path),
                             {"sk_body", "sk_head", "sk_hands", "sk_legs", "sk_sh"})
        finally:
            os.remove(path)


if __name__ == "__main__":
    unittest.main()
