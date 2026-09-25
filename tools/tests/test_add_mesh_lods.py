#!/usr/bin/env python3
"""Unit tests for the pure parts of tools/blender/add_mesh_lods.py.

Run:  python -m unittest tools.tests.test_add_mesh_lods

The script imports bpy at module level, so a stub module is installed before the import and
`main()` is behind a `__main__` guard. The Blender side (decimate, export, re-import) is proven
by the report the script writes on every run and by `tools/audit_fbx_lods.py --diff`.
"""
import importlib.util
import os
import sys
import types
import unittest

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPT = os.path.join(TOOLS, "blender", "add_mesh_lods.py")

if "bpy" not in sys.modules:
    sys.modules["bpy"] = types.ModuleType("bpy")
_spec = importlib.util.spec_from_file_location("add_mesh_lods", SCRIPT)
aml = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(aml)

BASE = ["blender", "-b", "--"]


class ParseArgsTests(unittest.TestCase):
    def test_required(self):
        for missing, argv in (
            ("fbx", ["--mesh", "M", "--levels", "4", "--ratios", "0.07"]),
            ("mesh", ["--fbx", "f.fbx", "--levels", "4", "--ratios", "0.07"]),
            ("levels", ["--fbx", "f.fbx", "--mesh", "M", "--ratios", "0.07"]),
            ("ratios", ["--fbx", "f.fbx", "--mesh", "M", "--levels", "4"]),
        ):
            with self.assertRaises(SystemExit) as cm:
                aml.parse_args(BASE + argv)
            self.assertIn("--%s is required" % missing, str(cm.exception))

    def test_repeated_mesh_with_cap_and_flags(self):
        a = aml.parse_args(BASE + ["--fbx", "f.fbx", "--mesh", "Head", "--mesh", "Head.eyes@4",
                                   "--levels", "1,2,3,4,5", "--ratios", "0.7,0.3,0.15,0.07,0.03",
                                   "--lock-boundary", "--apply"])
        self.assertEqual(a["meshes"], [("Head", None), ("Head.eyes", 4)])
        self.assertEqual(a["levels"], [1, 2, 3, 4, 5])
        self.assertEqual(a["ratios"], [0.7, 0.3, 0.15, 0.07, 0.03])
        self.assertTrue(a["lock_boundary"])
        self.assertTrue(a["apply"])

    def test_levels_and_ratios_must_pair_and_shrink(self):
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "4,5",
                                   "--ratios", "0.07"])
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "4,5",
                                   "--ratios", "0.03,0.07"])
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "0",
                                   "--ratios", "0.5"])

    def test_unknown_and_dangling(self):
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "4", "--ratios",
                                   "0.07", "--bogus"])
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh"])

    def test_absolute_tris_with_a_plane_and_replace(self):
        """KEYforce's warg fur spec: LOD2 5k, LOD3 2k, LOD4 1k, LOD5 a single plane, LOD6 gone."""
        a = aml.parse_args(BASE + ["--fbx", "f", "--mesh", "warg_low_fur", "--levels", "2,3,4,5",
                                   "--tris", "5000,2000,1000,plane", "--replace",
                                   "--delete-levels", "6"])
        self.assertEqual(a["tris"], [5000, 2000, 1000, "plane"])
        self.assertIsNone(a["ratios"])
        self.assertTrue(a["replace"])
        self.assertEqual(a["delete_levels"], [6])

    def test_island_filters_by_level(self):
        a = aml.parse_args(BASE + ["--fbx", "f", "--mesh", "orc_rider_saddle", "--levels",
                                   "0,1,2,3,4,5", "--tris", "15000,10000,5000,2000,500,100",
                                   "--replace", "--drop-small", "2,3,4,5:0.15",
                                   "--bulky-only", "4,5:0.5"])
        self.assertEqual(a["drop_small"], {2: 0.15, 3: 0.15, 4: 0.15, 5: 0.15})
        self.assertEqual(a["bulky_only"], {4: 0.5, 5: 0.5})
        self.assertEqual(a["levels"], [0, 1, 2, 3, 4, 5])

    def test_targets_are_ratios_or_tris_never_both(self):
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "4",
                                   "--ratios", "0.07", "--tris", "500"])
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "2,3",
                                   "--tris", "2000,5000"])
        with self.assertRaises(SystemExit):   # a plane is the last level, nothing after it
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "4,5",
                                   "--tris", "plane,100"])

    def test_level_zero_only_with_replace_and_tris(self):
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "0,1",
                                   "--tris", "15000,10000"])
        aml.parse_args(BASE + ["--fbx", "f", "--mesh", "M", "--levels", "0,1",
                               "--tris", "15000,10000", "--replace"])

    def test_plan_replaces_the_per_mesh_arguments(self):
        a = aml.parse_args(BASE + ["--fbx", "f.fbx", "--plan", "p.json", "--apply"])
        self.assertEqual(a["plan"], "p.json")
        with self.assertRaises(SystemExit):
            aml.parse_args(BASE + ["--fbx", "f.fbx", "--plan", "p.json", "--mesh", "M"])

    def test_fbx_from_argv_survives_bad_arguments(self):
        path = aml.fbx_from_argv(BASE + ["--fbx", "x/y.fbx", "--bogus"])
        self.assertTrue(path.endswith(os.path.join("x", "y.fbx")))


class NamingTests(unittest.TestCase):
    HAIR = ["Dwarf_Hair_A_lod0", "Dwarf_Hair_A_lod1", "Dwarf_Hair_A_lod2", "Dwarf_Hair_A_lod3"]
    BODY = ["SK_Pale_Uruk_BM_A_Head", "SK_Pale_Uruk_BM_A_Head.eyes",
            "SK_Pale_Uruk_Underwear_A1", "SK_Pale_Uruk_Underwear_A1.lod1"]

    def test_scheme_follows_the_file(self):
        self.assertEqual(aml.detect_scheme("Dwarf_Hair_A", self.HAIR), "underscore")
        self.assertEqual(aml.detect_scheme("SK_Pale_Uruk_BM_A_Head", self.BODY), "dot")

    def test_dot_lod0_scheme(self):
        """sr_dale_kingdom_boots.fbx names LOD0 `sk_dale_boots_archer_a01.lod0`."""
        names = ["sk_dale_boots_archer_a01.lod0"]
        self.assertEqual(aml.detect_scheme("sk_dale_boots_archer_a01", names), "dot0")
        self.assertEqual(aml.lod_name("sk_dale_boots_archer_a01", 0, "dot0"),
                         "sk_dale_boots_archer_a01.lod0")
        self.assertEqual(aml.lod_name("sk_dale_boots_archer_a01", 2, "dot0"),
                         "sk_dale_boots_archer_a01.lod2")
        self.assertEqual(aml.existing_levels("sk_dale_boots_archer_a01", names, "dot0"), [0])

    def test_scheme_needs_a_lod0(self):
        with self.assertRaises(SystemExit):
            aml.detect_scheme("Nope", self.HAIR)

    def test_lod_names(self):
        self.assertEqual(aml.lod_name("Dwarf_Hair_A", 0, "underscore"), "Dwarf_Hair_A_lod0")
        self.assertEqual(aml.lod_name("Dwarf_Hair_A", 4, "underscore"), "Dwarf_Hair_A_lod4")
        self.assertEqual(aml.lod_name("SK_Head.eyes", 0, "dot"), "SK_Head.eyes")
        self.assertEqual(aml.lod_name("SK_Head.eyes", 3, "dot"), "SK_Head.eyes.lod3")

    def test_existing_levels(self):
        self.assertEqual(aml.existing_levels("Dwarf_Hair_A", self.HAIR, "underscore"), [0, 1, 2, 3])
        # `.eyes` is a sub-mesh of Head, not a LOD of it
        self.assertEqual(aml.existing_levels("SK_Pale_Uruk_BM_A_Head", self.BODY, "dot"), [0])
        self.assertEqual(aml.existing_levels("SK_Pale_Uruk_Underwear_A1", self.BODY, "dot"), [0, 1])


class PlanTests(unittest.TestCase):
    def test_cap_drops_higher_levels(self):
        self.assertEqual(aml.plan_levels([1, 2, 3, 4, 5], [0.7, 0.3, 0.15, 0.07, 0.03], 4),
                         [(1, 0.7), (2, 0.3), (3, 0.15), (4, 0.07)])
        self.assertEqual(aml.plan_levels([4, 5], [0.07, 0.03], None), [(4, 0.07), (5, 0.03)])

    def test_resolve_target(self):
        self.assertEqual(aml.resolve_target(18278, ratio=0.07), 1279)
        self.assertEqual(aml.resolve_target(18018, tris=5000), 5000)
        self.assertEqual(aml.resolve_target(18018, tris="plane"), "plane")

    def test_source_is_the_lowest_kept_lod_above_the_target(self):
        """Warg fur has L0 18,018, L1 11,368, L4 6,820 (being replaced): L2 5k and L4 1k come
        from L1, the artist's own reduction, not from a level about to be thrown away."""
        tris = {0: 18018, 1: 11368, 4: 6820}
        self.assertEqual(aml.pick_source(tris, replaced={2, 3, 4, 5}, target=5000), 1)
        self.assertEqual(aml.pick_source(tris, replaced={2, 3, 4, 5}, target=1000), 1)
        # hairs: nothing replaced, LOD3 is the lowest LOD with more tris than the target
        self.assertEqual(aml.pick_source({0: 18278, 1: 12794, 2: 5483, 3: 2739}, set(), 1279), 3)
        # the saddle rebuilds every level: all of them come from the untouched LOD0
        saddle = {0: 23150, 2: 16205, 3: 11574, 4: 6944, 5: 2314}
        self.assertEqual(aml.pick_source(saddle, replaced={0, 1, 2, 3, 4, 5}, target=10000), 0)

    def test_a_level_being_deleted_is_never_a_source(self):
        """The warg fur's LOD6 (1,240 tris) is deleted in the same run; LOD4 1k must come from
        LOD1, not from the fur KEYforce asked to remove."""
        tris = {0: 18018, 1: 11368, 4: 6820, 6: 1240}
        self.assertEqual(aml.pick_source(tris, replaced={2, 3, 4, 5} | {6}, target=1000), 1)

    def test_source_too_small_is_refused(self):
        with self.assertRaises(SystemExit):
            aml.pick_source({0: 400}, set(), 500)


class LoadPlanTests(unittest.TestCase):
    def test_plan_json_becomes_per_mesh_levels(self):
        import json
        import tempfile
        fd, path = tempfile.mkstemp(suffix=".json")
        with os.fdopen(fd, "w") as f:
            json.dump({"meshes": [{"base": "SK_Head", "levels": {"5": 108, "1": 2513}, "lock": True},
                                  {"base": "Dwarf_Hair_A", "levels": {"4": 1279}, "lock": False}]}, f)
        try:
            plan = aml.load_plan(path)
        finally:
            os.remove(path)
        self.assertEqual(plan, [("SK_Head", [(1, 2513), (5, 108)], True),
                                ("Dwarf_Hair_A", [(4, 1279)], False)])

    def test_a_plan_never_asks_for_lod0_or_a_growing_level(self):
        import json
        import tempfile
        for levels in ({"0": 100}, {"1": 0}):
            fd, path = tempfile.mkstemp(suffix=".json")
            with os.fdopen(fd, "w") as f:
                json.dump({"meshes": [{"base": "M", "levels": levels}]}, f)
            try:
                with self.assertRaises(SystemExit):
                    aml.load_plan(path)
            finally:
                os.remove(path)


class PlanFixTests(unittest.TestCase):
    def test_fixes_ride_in_the_plan(self):
        import json
        import tempfile
        fd, path = tempfile.mkstemp(suffix=".json")
        with os.fdopen(fd, "w") as f:
            json.dump({"meshes": [], "delete": ["X.lod1"], "renames": {"X.lod": "X.lod1"},
                       "materials": {"X.lod5": ["t_cave_troll_set1"]}}, f)
        try:
            self.assertEqual(aml.load_plan_fixes(path), (["X.lod1"], {"X.lod": "X.lod1"},
                                                         {"X.lod5": ["t_cave_troll_set1"]}))
        finally:
            os.remove(path)

    def test_a_plan_without_fixes(self):
        import json
        import tempfile
        fd, path = tempfile.mkstemp(suffix=".json")
        with os.fdopen(fd, "w") as f:
            json.dump({"meshes": []}, f)
        try:
            self.assertEqual(aml.load_plan_fixes(path), ([], {}, {}))
        finally:
            os.remove(path)


class RoundTripToleranceTests(unittest.TestCase):
    def test_a_fourth_decimal_flip_is_the_same_size(self):
        """dunland_caerdh_helmet_lord_a.feather.lod5: 0.1393 -> 0.1392 after a round trip."""
        self.assertTrue(aml.same_vector([0.2846, 0.1393, 0.1936], [0.2846, 0.1392, 0.1936]))

    def test_a_real_change_is_not(self):
        self.assertFalse(aml.same_vector([0.2846, 0.1393, 0.1936], [0.2846, 0.1293, 0.1936]))

    def test_grouping_empties_are_exported(self):
        """wm_boromir_shield is an empty that parents the shield's LODs and collision bodies; an
        export without empties dropped it and orphaned every child."""
        self.assertIn("EMPTY", aml.EXPORT_TYPES)


class IslandFilterTests(unittest.TestCase):
    # dims of orc_rider_saddle islands (FBX units), mesh extent 153
    SEAT = (39.1, 36.9, 36.7)
    STRAP = (134.0, 28.2, 12.8)
    BUCKLE = (6.7, 6.3, 5.7)

    def test_drop_small_removes_metal_only(self):
        keep = lambda d: aml.keep_island(d, 153.0, drop_small=0.15, bulky_only=None)
        self.assertTrue(keep(self.SEAT))
        self.assertTrue(keep(self.STRAP))
        self.assertFalse(keep(self.BUCKLE))

    def test_bulky_only_keeps_the_seat(self):
        keep = lambda d: aml.keep_island(d, 153.0, drop_small=0.15, bulky_only=0.5)
        self.assertTrue(keep(self.SEAT))
        self.assertFalse(keep(self.STRAP))
        self.assertFalse(keep(self.BUCKLE))

    def test_no_filter_keeps_everything(self):
        self.assertTrue(aml.keep_island(self.BUCKLE, 153.0, None, None))

    def test_existing_level_is_refused(self):
        with self.assertRaises(SystemExit):
            aml.check_new_levels("Dwarf_Hair_A", [0, 1, 2, 3], [(3, 0.15), (4, 0.07)])
        aml.check_new_levels("Dwarf_Hair_A", [0, 1, 2, 3], [(4, 0.07), (5, 0.03)])

    def test_target_tris(self):
        self.assertEqual(aml.target_tris(18278, 0.07), 1279)
        self.assertEqual(aml.target_tris(176, 0.07), 12)
        self.assertEqual(aml.target_tris(10, 0.03), 4)   # never below a sliver of a mesh


class BoundaryTests(unittest.TestCase):
    def test_closed_quad_strip_edges(self):
        # two quads sharing edge (1,2): the outer ring is boundary, the shared edge is not
        polys = [(0, 1, 2, 3), (1, 4, 5, 2)]
        self.assertEqual(aml.boundary_vertices(polys), {0, 1, 2, 3, 4, 5})

    def test_interior_vertex_is_not_boundary(self):
        # a fan of four triangles around vertex 4 inside a square
        polys = [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)]
        self.assertEqual(aml.boundary_vertices(polys), {0, 1, 2, 3})

    def test_closed_surface_has_no_boundary(self):
        tetra = [(0, 1, 2), (0, 3, 1), (1, 3, 2), (2, 3, 0)]
        self.assertEqual(aml.boundary_vertices(tetra), set())


if __name__ == "__main__":
    unittest.main()
