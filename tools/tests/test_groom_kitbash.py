"""Tests for tools/blender/groom_kitbash.py: classify dwarf hair cards, then validate and resolve kitbash recipes."""
import copy
import math
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "blender"))
import groom_kitbash as gk  # noqa: E402


def at(az_deg, z, r=0.1):
    """A point `r` from the vertical axis through the head, at azimuth `az_deg` (0 = face front, + toward +x)."""
    a = math.radians(az_deg)
    return (r * math.sin(a), gk.AXIS_Y + r * math.cos(a), z)


class IslandsTests(unittest.TestCase):
    def test_two_islands_sharing_no_vertex_are_two_parts(self):
        self.assertEqual(gk.islands(6, [[0, 1, 2], [3, 4, 5]]), [[0, 1, 2], [3, 4, 5]])

    def test_a_shared_vertex_joins_polygons(self):
        self.assertEqual(gk.islands(5, [[0, 1, 2], [2, 3, 4]]), [[0, 1, 2, 3, 4]])

    def test_parts_are_ordered_by_lowest_vertex_not_by_polygon_order(self):
        self.assertEqual(gk.islands(8, [[5, 6, 7], [0, 1, 2], [2, 3]]), [[0, 1, 2, 3], [5, 6, 7]])

    def test_vertices_in_no_polygon_are_not_parts(self):
        self.assertEqual(gk.islands(5, [[0, 1, 2]]), [[0, 1, 2]])

    def test_a_chain_of_polygons_is_one_part(self):
        polys = [[i, i + 1, i + 2] for i in range(0, 20)]
        self.assertEqual(len(gk.islands(25, polys)), 1)

    def test_same_mesh_gives_same_ids(self):
        polys = [[4, 5, 6], [0, 1, 2], [9, 10, 11]]
        self.assertEqual(gk.islands(12, polys), gk.islands(12, polys))

    def test_empty_mesh(self):
        self.assertEqual(gk.islands(0, []), [])

    def test_vertex_labels(self):
        parts = gk.islands(6, [[0, 1], [3, 4, 5]])
        self.assertEqual(gk.vertex_labels(parts, 6), [0, 0, -1, 1, 1, 1])


class AzimuthSideTests(unittest.TestCase):
    def test_front_is_zero(self):
        self.assertAlmostEqual(gk.azimuth((0.0, 0.165, 1.3)), 0.0)

    def test_positive_toward_plus_x(self):
        self.assertAlmostEqual(gk.azimuth((0.1, gk.AXIS_Y, 1.3)), 90.0)
        self.assertAlmostEqual(gk.azimuth((-0.1, gk.AXIS_Y, 1.3)), -90.0)

    def test_back_is_180_in_magnitude(self):
        self.assertAlmostEqual(abs(gk.azimuth((0.0, -0.1, 1.3))), 180.0)

    def test_az_helper_roundtrip(self):
        self.assertAlmostEqual(gk.azimuth(at(40, 1.3)), 40.0)

    def test_side_dead_zone_is_centre(self):
        self.assertEqual(gk.side((-0.006, 0, 0)), "left")
        self.assertEqual(gk.side((0.006, 0, 0)), "right")
        self.assertEqual(gk.side((0.005, 0, 0)), "centre")
        self.assertEqual(gk.side((-0.005, 0, 0)), "centre")
        self.assertEqual(gk.side((0.0, 0, 0)), "centre")


class BeardClassifyTests(unittest.TestCase):
    def test_region_boundaries_are_exact(self):
        self.assertEqual(gk.beard_region(39.999, 0.17, 1.31), "moustache")
        self.assertEqual(gk.beard_region(40.0, 0.17, 1.31), "cheek")
        self.assertEqual(gk.beard_region(79.999, 0.17, 1.31), "cheek")
        self.assertEqual(gk.beard_region(80.0, 0.17, 1.31), "sideburn")

    def test_moustache_needs_lip_height_and_forward_y(self):
        self.assertEqual(gk.beard_region(0.0, 0.17, 1.302), "moustache")
        self.assertEqual(gk.beard_region(0.0, 0.17, 1.3019), "chin")
        # high but not forward: not a moustache, falls to cheek
        self.assertEqual(gk.beard_region(0.0, 0.14, 1.31), "cheek")

    def test_rooted_moustache(self):
        c = gk.classify_beard((0.0, 0.17, 1.31), 0.002, 1.25)
        self.assertEqual((c["region"], c["length"], c["rooted"]), ("moustache", "short", True))

    def test_chin_and_lengths(self):
        root = (0.0, 0.17, 1.2)
        self.assertEqual(gk.classify_beard(root, 0.0, 1.0)["length"], "chest")
        self.assertEqual(gk.classify_beard(root, 0.0, 1.119)["length"], "chest")
        self.assertEqual(gk.classify_beard(root, 0.0, 1.12)["length"], "mid")
        self.assertEqual(gk.classify_beard(root, 0.0, 1.22)["length"], "short")
        self.assertEqual(gk.classify_beard(root, 0.0, 1.0)["region"], "chin")

    def test_cheek_and_sideburn_by_azimuth(self):
        self.assertEqual(gk.classify_beard(at(60, 1.3), 0.0, 1.25)["region"], "cheek")
        self.assertEqual(gk.classify_beard(at(-100, 1.3), 0.0, 1.25)["region"], "sideburn")

    def test_rooted_threshold_is_strict(self):
        self.assertTrue(gk.classify_beard((0, 0.17, 1.3), 0.0099, 1.2)["rooted"])
        far = gk.classify_beard((0, 0.17, 1.3), 0.01, 1.2)
        self.assertFalse(far["rooted"])
        self.assertEqual(far["region"], "fill")

    def test_inside_the_head_is_rooted(self):
        self.assertTrue(gk.classify_beard((0, 0.17, 1.3), -0.004, 1.2)["rooted"])

    def test_side_comes_from_root_x(self):
        self.assertEqual(gk.classify_beard((-0.02, 0.17, 1.2), 0.0, 1.2)["side"], "left")


class HairClassifyTests(unittest.TestCase):
    def test_region_boundaries(self):
        self.assertEqual(gk.hair_region(0.0, 1.45), "crown")
        self.assertEqual(gk.hair_region(0.0, 1.449), "side")
        self.assertEqual(gk.hair_region(120.0, 1.3), "back")
        self.assertEqual(gk.hair_region(119.999, 1.3), "side")
        self.assertEqual(gk.hair_region(150.0, 1.46), "crown")  # crown wins over back

    def test_hang_boundaries(self):
        self.assertEqual(gk.hang_of(59.999), "front")
        self.assertEqual(gk.hang_of(60.0), "side")
        self.assertEqual(gk.hang_of(119.999), "side")
        self.assertEqual(gk.hang_of(120.0), "back")

    def test_hair_length_bands(self):
        self.assertEqual(gk.hair_length(1.0), "long")
        self.assertEqual(gk.hair_length(1.21), "mid")
        self.assertEqual(gk.hair_length(1.319), "mid")
        self.assertEqual(gk.hair_length(1.32), "short")

    def test_classify_hair_back_card_hanging_front(self):
        c = gk.classify_hair(at(170, 1.3), 0.001, 1.0, at(20, 1.0))
        self.assertEqual((c["region"], c["length"], c["hang"], c["rooted"]), ("back", "long", "front", True))

    def test_unrooted_hair_is_fill_but_keeps_hang_and_length(self):
        c = gk.classify_hair(at(0, 1.3), 0.05, 1.25, at(90, 1.25))
        self.assertEqual((c["region"], c["hang"], c["length"], c["rooted"]), ("fill", "side", "mid", False))


class RingTests(unittest.TestCase):
    RINGS = [((0.0, 0.0, 0.0), 0.5), ((2.0, 0.0, 0.0), 0.5)]

    def test_geometry_is_bbox_centre_and_half_largest_dimension(self):
        centre, half = gk.ring_geometry([(0, 0, 0), (2, 1, 0.5), (1, 0.5, 0.25)])
        self.assertEqual(centre, (1.0, 0.5, 0.25))
        self.assertEqual(half, 1.0)

    def test_card_inside_one_ring(self):
        m, conflicts = gk.ring_membership({0: [(0.1, 0, 0)], 1: [(9, 9, 9)]}, self.RINGS)
        self.assertEqual(m, {0: 0, 1: None})
        self.assertEqual(conflicts, [])

    def test_vertex_exactly_at_half_extent_is_inside(self):
        m, _ = gk.ring_membership({0: [(0.5, 0, 0)]}, self.RINGS)
        self.assertEqual(m[0], 0)

    def test_any_vertex_counts(self):
        m, _ = gk.ring_membership({0: [(9, 9, 9), (2.1, 0, 0)]}, self.RINGS)
        self.assertEqual(m[0], 1)

    def test_card_in_two_rings_is_a_conflict_and_goes_to_the_nearer(self):
        rings = [((0.0, 0.0, 0.0), 1.0), ((1.5, 0.0, 0.0), 1.0)]
        card = [(0.9, 0, 0), (0.4, 0, 0)]  # nearest to ring 0's centre 0.4, to ring 1's 0.6: both within 1.0
        m, conflicts = gk.ring_membership({7: card}, rings)
        self.assertEqual(m[7], 0)
        self.assertEqual(conflicts, [(7, [0, 1])])

    def test_no_rings(self):
        m, conflicts = gk.ring_membership({0: [(0, 0, 0)]}, [])
        self.assertEqual((m, conflicts), ({0: None}, []))


class UvTests(unittest.TestCase):
    def test_first_nonzero_layer_wins(self):
        spans = {"UVChannel_1": (0.0, 1.0, 0.0, 1.0), "map1": (0.0, 0.5, 0.0, 0.5)}
        self.assertEqual(gk.valid_uv_layer(spans), "UVChannel_1")

    def test_hair_i_collapsed_first_layer_falls_to_map1(self):
        spans = {"UVMap": (0.25, 0.25, 0.75, 0.75), "map1": (0.1, 0.2, 0.3, 0.9)}
        self.assertEqual(gk.valid_uv_layer(spans), "map1")

    def test_extent_on_one_axis_is_valid(self):
        self.assertEqual(gk.valid_uv_layer({"a": (0.0, 0.0, 0.0, 1.0)}), "a")

    def test_all_collapsed_or_none(self):
        self.assertIsNone(gk.valid_uv_layer({"a": (0.5, 0.5, 0.5, 0.5)}))
        self.assertIsNone(gk.valid_uv_layer({}))

    def test_uv_span(self):
        self.assertEqual(gk.uv_span([(0.2, 0.9), (0.4, 0.1)]), (0.2, 0.4, 0.1, 0.9))
        self.assertIsNone(gk.uv_span([]))


class AnalyzeTests(unittest.TestCase):
    def test_penetration_counts_only_the_closed_part_of_the_head(self):
        pts = [(0, 0, 1.3), (0, 0, 1.1), (0, 0, 1.21)]
        self.assertAlmostEqual(gk.penetration(pts, [-0.004, -0.5, -0.006]), 0.006)
        self.assertEqual(gk.penetration(pts, [0.01, 0.02, 0.03]), 0.0)
        self.assertEqual(gk.penetration([(0, 0, 1.1)], [-0.5]), 0.0)

    def test_root_is_the_vertex_nearest_the_surface_tip_the_farthest_from_root(self):
        pts = [(0.0, 0.17, 1.2), (0.0, 0.17, 1.1), (0.0, 0.17, 0.9)]
        dists = [0.001, 0.05, 0.1]
        a = gk.analyze_part(pts, dists, "hair")
        self.assertEqual(a["root"], pts[0])
        self.assertEqual(a["tip"], pts[2])
        self.assertAlmostEqual(a["lowest_z"], 0.9)
        self.assertEqual(a["length"], "long")

    def test_root_prefers_smallest_unsigned_distance(self):
        pts = [(0.0, 0.17, 1.2), (0.0, 0.17, 1.25)]
        a = gk.analyze_part(pts, [0.02, -0.001], "beard")
        self.assertEqual(a["root"], pts[1])
        self.assertAlmostEqual(a["root_dist"], -0.001)
        self.assertTrue(a["rooted"])
        self.assertAlmostEqual(a["depth"], 0.001)

    def test_beard_analysis_has_no_hang(self):
        a = gk.analyze_part([(0.0, 0.17, 1.31)], [0.0], "beard")
        self.assertNotIn("hang", a)
        self.assertEqual(a["region"], "moustache")

    def test_unknown_family(self):
        with self.assertRaises(ValueError):
            gk.analyze_part([(0, 0, 0)], [0.0], "moustache")

    def test_empty_part(self):
        with self.assertRaises(ValueError):
            gk.analyze_part([], [], "beard")

    def test_summary(self):
        parts = [
            {"id": 0, "kind": "card", "region": "chin", "braid": None, "depth": 0.002, "uv_layer": "a"},
            {"id": 1, "kind": "card", "region": "fill", "braid": None, "depth": 0.0, "uv_layer": None},
            {"id": 2, "kind": "card", "region": "cheek", "braid": 0, "depth": 0.0, "uv_layer": "a"},
            {"id": 3, "kind": "ring", "ring_index": 0, "region": "fill", "braid": None, "depth": 0.0,
             "uv_layer": "a"},
        ]
        s = gk.summarize_groom(parts, [(2, [0, 1])])
        self.assertEqual((s["parts"], s["cards"], s["rings"], s["fill"]), (4, 3, 1, 1))
        self.assertEqual(s["regions"], {"chin": 1})  # loose rooted cards only: cheek is braided, fill counted apart
        self.assertEqual(s["braid_members"], {0: 1})
        self.assertEqual((s["conflicts"], s["no_valid_uv"]), (1, 1))
        self.assertAlmostEqual(s["max_depth"], 0.002)


class TransformTests(unittest.TestCase):
    def test_mirror_x(self):
        self.assertEqual(gk.mirror_x([(1, 2, 3), (-4, 5, 6)]), [(-1, 2, 3), (4, 5, 6)])

    def test_mirror_twice_is_identity(self):
        pts = [(0.1, 0.2, 0.3)]
        self.assertEqual(gk.mirror_x(gk.mirror_x(pts)), pts)

    def test_swap_side_group(self):
        self.assertEqual(gk.swap_side_group("l_jaw"), "r_jaw")
        self.assertEqual(gk.swap_side_group("r_jaw"), "l_jaw")
        self.assertEqual(gk.swap_side_group("head"), "head")
        self.assertEqual(gk.swap_side_group("lower_jaw"), "lower_jaw")
        self.assertEqual(gk.swap_side_group("jaw_l"), "jaw_l")

    def test_translate(self):
        self.assertEqual(gk.translate([(1, 1, 1)], (0.5, 0, -1)), [(1.5, 1, 0)])

    def test_max_move(self):
        self.assertAlmostEqual(gk.max_move([(0, 0, 0), (1, 1, 1)], [(3, 4, 0), (1, 1, 1)]), 5.0)
        self.assertEqual(gk.max_move([], []), 0.0)
        with self.assertRaises(ValueError):
            gk.max_move([(0, 0, 0)], [])


def card(i, region, side="centre", length="short", braid=None, hang=None):
    p = {"id": i, "kind": "card", "region": region, "side": side, "length": length, "braid": braid}
    if hang:
        p["hang"] = hang
    return p


def ring(i, n):
    return {"id": i, "kind": "ring", "ring_index": n, "region": "fill", "side": "centre", "length": "short",
            "braid": None}


def make_inventory():
    return {"sources": {
        "SK_Dwarf_Beard_A_07": {
            "object": "SK_Dwarf_Beard_A_07", "sha1": "aaa", "family": "beard",
            "mesh_names": ["SK_Dwarf_Beard_A_07", "SK_Dwarf_Beard_A_12", "SK_Dwarf_Beard_A_07.lod1"],
            "parts": [card(0, "moustache", "left"), card(1, "moustache", "right"), card(2, "chin", length="mid"),
                      card(3, "cheek", "left", "chest", braid=0), ring(4, 0), card(5, "sideburn", "right"),
                      card(6, "fill", length="mid")]},
        "Dwarf_Hair_A_lod0": {
            "object": "Dwarf_Hair_A_lod0", "sha1": "hhh", "family": "hair",
            "mesh_names": ["Dwarf_Hair_A_lod0", "Dwarf_Hair_B_lod0"],
            "parts": [card(0, "crown", length="long", hang="front"), card(1, "crown", length="short", hang="back"),
                      card(2, "back", length="long", hang="back"), card(3, "side", length="mid", hang="side")]},
    }}


def fin(candidate, channels="keep"):
    return {"candidate": candidate, "channels": channels}


def make_recipe():
    return {
        "version": 1,
        "inventory_sha1": {"SK_Dwarf_Beard_A_07": "aaa", "Dwarf_Hair_A_lod0": "hhh"},
        "families": {"beard": {"target": "SK_Dwarf_Beards.fbx", "materials": {"cards": "m1", "ring": "m2"}},
                     "hair": {"target": "Dwarf_Hairs.fbx", "materials": {"cards": "m3", "ring": "m4"}}},
        "candidates": [
            {"id": "b01", "family": "beard", "note": "moustache pair",
             "parts": [{"source": "SK_Dwarf_Beard_A_07", "regions": ["moustache"], "braids": "none"}]},
            {"id": "b02", "family": "beard", "note": "braid",
             "parts": [{"source": "SK_Dwarf_Beard_A_07", "braids": "all"}]},
            {"id": "h01", "family": "hair", "note": "crown",
             "parts": [{"source": "Dwarf_Hair_A_lod0", "regions": ["crown"]}]},
        ],
        "final": {"SK_Dwarf_Beard_A_13": fin("b01"), "SK_Dwarf_Beard_A_14": fin("b02"), "Dwarf_Hair_K": fin("h01")},
    }


class RecipeTests(unittest.TestCase):
    def setUp(self):
        self.inv = make_inventory()
        self.recipe = make_recipe()

    def errors(self):
        return gk.validate_recipe(self.recipe, self.inv)

    def assertError(self, text):
        errs = self.errors()
        self.assertTrue(any(text in e for e in errs), "no error containing %r in %r" % (text, errs))

    def spec(self, cid="b01", i=0):
        return next(c for c in self.recipe["candidates"] if c["id"] == cid)["parts"][i]

    def test_valid_recipe_has_no_errors(self):
        self.assertEqual(self.errors(), [])

    def test_resolve_regions(self):
        r = gk.resolve(self.recipe, self.inv)
        self.assertEqual([(s, p) for s, p, _ in r["b01"]], [("SK_Dwarf_Beard_A_07", 0), ("SK_Dwarf_Beard_A_07", 1)])
        self.assertEqual(r["b01"][0][2], ())

    def test_regions_skip_braided_cards(self):
        self.spec()["regions"] = ["cheek"]
        self.assertError("selects nothing")

    def test_braids_all_selects_ring_and_members_together(self):
        r = gk.resolve(self.recipe, self.inv)
        self.assertEqual(sorted(p for _, p, _ in r["b02"]), [3, 4])

    def test_braids_by_index(self):
        self.spec("b02")["braids"] = [0]
        self.assertEqual(self.errors(), [])
        self.spec("b02")["braids"] = [5]
        self.assertError("selects nothing")

    def test_braids_none_selects_no_ring(self):
        self.spec("b02")["braids"] = "none"
        self.spec("b02")["regions"] = ["chin"]
        r = gk.resolve(self.recipe, self.inv)
        self.assertEqual([p for _, p, _ in r["b02"]], [2])

    def test_regions_and_braids_union(self):
        self.spec("b02")["regions"] = ["chin"]
        r = gk.resolve(self.recipe, self.inv)
        self.assertEqual(sorted(p for _, p, _ in r["b02"]), [2, 3, 4])

    def test_length_and_side_and_hang_filters(self):
        s = self.spec()
        s["regions"] = ["chin", "fill", "moustache"]
        s["length"] = ["mid"]
        self.assertEqual([p for _, p, _ in gk.resolve(self.recipe, self.inv)["b01"]], [2, 6])
        s["length"] = ["short"]
        s["side"] = "left"
        self.assertEqual([p for _, p, _ in gk.resolve(self.recipe, self.inv)["b01"]], [0])
        h = self.spec("h01")
        h["regions"] = ["crown"]
        h["hang"] = ["back"]
        self.assertEqual([p for _, p, _ in gk.resolve(self.recipe, self.inv)["h01"]], [1])

    def test_side_both_selects_every_side(self):
        self.spec()["side"] = "both"
        self.assertEqual(len(gk.resolve(self.recipe, self.inv)["b01"]), 2)

    def test_drop_and_add_parts(self):
        c = self.recipe["candidates"][0]
        c["drop_parts"] = {"SK_Dwarf_Beard_A_07": [1]}
        c["add_parts"] = {"SK_Dwarf_Beard_A_07": [5]}
        self.assertEqual([p for _, p, _ in gk.resolve(self.recipe, self.inv)["b01"]], [0, 5])

    def test_add_part_already_selected_is_twice(self):
        self.recipe["candidates"][0]["add_parts"] = {"SK_Dwarf_Beard_A_07": [0]}
        self.assertError("selected twice")

    def test_unknown_part_id_in_add_or_drop(self):
        self.recipe["candidates"][0]["add_parts"] = {"SK_Dwarf_Beard_A_07": [99]}
        self.assertError("no part 99")
        self.recipe["candidates"][0]["add_parts"] = {}
        self.recipe["candidates"][0]["drop_parts"] = {"SK_Dwarf_Beard_A_07": [99]}
        self.assertError("no part 99")

    def test_drop_everything_leaves_nothing(self):
        self.recipe["candidates"][0]["drop_parts"] = {"SK_Dwarf_Beard_A_07": [0, 1]}
        self.assertError("selects nothing")

    def test_same_part_selected_twice_by_two_specs(self):
        c = self.recipe["candidates"][0]
        c["parts"].append({"source": "SK_Dwarf_Beard_A_07", "regions": ["moustache"], "side": "left"})
        self.assertError("selected twice")

    def test_unknown_region(self):
        self.spec()["regions"] = ["goatee"]
        self.assertError("unknown region 'goatee'")

    def test_hair_region_is_not_a_beard_region(self):
        self.spec()["regions"] = ["crown"]
        self.assertError("unknown region 'crown'")

    def test_unknown_family(self):
        self.recipe["candidates"][0]["family"] = "tail"
        self.assertError("unknown family 'tail'")

    def test_source_of_the_wrong_family(self):
        self.spec()["source"] = "Dwarf_Hair_A_lod0"
        self.assertError("is a hair source")

    def test_unknown_source(self):
        self.spec()["source"] = "SK_Nope"
        self.assertError("unknown source 'SK_Nope'")

    def test_unknown_transform_op(self):
        self.spec()["transform"] = [{"op": "twist"}]
        self.assertError("unknown transform op 'twist'")

    def test_mirror_copy_needs_a_side(self):
        self.spec()["transform"] = [{"op": "mirror_copy"}]
        self.assertError("mirror_copy needs side")
        self.spec()["side"] = "both"
        self.assertError("mirror_copy needs side")

    def test_mirror_copy_with_side_resolves_with_transforms(self):
        s = self.spec()
        s["side"] = "left"
        s["transform"] = [{"op": "mirror_copy"}, {"op": "translate", "d": [0, 0.001, 0]}]
        self.recipe["final"]["SK_Dwarf_Beard_A_13"] = fin("b01", "drop")  # keep plus mirror_copy is refused
        self.assertEqual(self.errors(), [])
        r = gk.resolve(self.recipe, self.inv)
        self.assertEqual(len(r["b01"]), 1)
        self.assertEqual([t["op"] for t in r["b01"][0][2]], ["mirror_copy", "translate"])

    def test_translate_needs_three_numbers(self):
        self.spec()["transform"] = [{"op": "translate", "d": [1, 2]}]
        self.assertError("translate needs d")

    def test_final_name_already_in_the_inventory(self):
        self.recipe["final"] = {"SK_Dwarf_Beard_A_12": fin("b01")}
        self.assertError("already exists")

    def test_final_hair_name_clashing_with_an_lod0_name(self):
        self.recipe["final"] = {"Dwarf_Hair_B": fin("h01")}
        self.assertError("already exists")

    def test_final_names_must_be_consecutive(self):
        self.recipe["final"]["SK_Dwarf_Beard_A_16"] = self.recipe["final"].pop("SK_Dwarf_Beard_A_14")
        self.assertError("not consecutive")

    def test_final_beards_must_start_at_13(self):
        self.recipe["final"] = {"SK_Dwarf_Beard_A_14": fin("b01"), "SK_Dwarf_Beard_A_15": fin("b02"),
                                "Dwarf_Hair_K": fin("h01")}
        self.assertError("not consecutive")

    def test_final_hairs_start_at_k(self):
        self.recipe["final"]["Dwarf_Hair_L"] = self.recipe["final"].pop("Dwarf_Hair_K")
        self.assertError("not consecutive")

    def test_final_name_wrong_family_shape(self):
        self.recipe["final"]["Dwarf_Hair_K"] = fin("b01")
        self.assertError("does not fit")

    def test_final_names_unknown_candidate(self):
        self.recipe["final"]["Dwarf_Hair_K"] = fin("h99")
        self.assertError("unknown candidate 'h99'")

    def test_final_entry_must_be_an_object(self):
        self.recipe["final"]["Dwarf_Hair_K"] = "h01"
        self.assertError("must be an object")

    def test_channels_are_required(self):
        self.recipe["final"]["Dwarf_Hair_K"] = {"candidate": "h01"}
        self.assertError("channels must be keep or drop")

    def test_channels_must_be_keep_or_drop(self):
        self.recipe["final"]["Dwarf_Hair_K"] = fin("h01", "maybe")
        self.assertError("channels must be keep or drop")

    def test_test_names_skip_the_consecutive_rule(self):
        self.recipe["final"] = {"SK_Dwarf_Beard_Test_Ch": fin("b01"), "SK_Dwarf_Beard_Test_NoCh": fin("b01", "drop")}
        self.assertEqual(self.errors(), [])

    def test_test_names_do_not_count_toward_consecutive_names(self):
        self.recipe["final"]["SK_Dwarf_Beard_Test_Ch"] = fin("b01")
        self.assertEqual(self.errors(), [])

    def test_test_name_already_in_the_inventory(self):
        self.inv["sources"]["SK_Dwarf_Beard_A_07"]["mesh_names"].append("SK_Dwarf_Beard_Test_Ch")
        self.recipe["final"] = {"SK_Dwarf_Beard_Test_Ch": fin("b01")}
        self.assertError("already exists")

    def test_test_name_must_still_fit_its_family(self):
        self.recipe["final"] = {"SK_Dwarf_Beard_Test_Ch": fin("h01")}
        self.assertError("does not fit")

    def test_a_name_implying_no_family(self):
        self.recipe["final"] = {"Foo_Test_1": fin("b01")}
        self.assertError("does not fit")

    def test_two_finals_may_share_a_candidate(self):
        self.recipe["final"]["SK_Dwarf_Beard_A_14"] = fin("b01", "drop")
        self.assertEqual(self.errors(), [])

    def test_keep_with_mirror_copy_is_an_error(self):
        s = self.spec()
        s["side"] = "left"
        s["transform"] = [{"op": "mirror_copy"}]
        self.assertError("keeps channels but candidate 'b01' uses mirror_copy")

    def test_drop_with_mirror_copy_is_fine(self):
        s = self.spec()
        s["side"] = "left"
        s["transform"] = [{"op": "mirror_copy"}]
        self.recipe["final"]["SK_Dwarf_Beard_A_13"] = fin("b01", "drop")
        self.assertEqual(self.errors(), [])

    def test_family_finals_sorted_with_channels(self):
        self.recipe["final"]["SK_Dwarf_Beard_Test_Ch"] = fin("b01", "drop")
        self.assertEqual(gk.family_finals(self.recipe, "beard"),
                         [("SK_Dwarf_Beard_A_13", "b01", "keep"), ("SK_Dwarf_Beard_A_14", "b02", "keep"),
                          ("SK_Dwarf_Beard_Test_Ch", "b01", "drop")])
        self.assertEqual(gk.family_finals(self.recipe, "hair"), [("Dwarf_Hair_K", "h01", "keep")])

    def test_final_family_and_object_name(self):
        self.assertEqual(gk.final_family("SK_Dwarf_Beard_A_13"), "beard")
        self.assertEqual(gk.final_family("SK_Dwarf_Beard_Test_Ch"), "beard")
        self.assertEqual(gk.final_family("Dwarf_Hair_K"), "hair")
        self.assertIsNone(gk.final_family("Foo"))
        self.assertEqual(gk.object_name("SK_Dwarf_Beard_A_13", "beard"), "SK_Dwarf_Beard_A_13")
        self.assertEqual(gk.object_name("Dwarf_Hair_K", "hair"), "Dwarf_Hair_K_lod0")

    def test_changed_source_sha1(self):
        self.recipe["inventory_sha1"]["SK_Dwarf_Beard_A_07"] = "bbb"
        self.assertError("sha1 differs")

    def test_missing_source_sha1(self):
        del self.recipe["inventory_sha1"]["SK_Dwarf_Beard_A_07"]
        self.assertError("sha1 differs")

    def test_bad_version(self):
        self.recipe["version"] = 2
        self.assertError("version")

    def test_duplicate_candidate_id(self):
        self.recipe["candidates"].append(copy.deepcopy(self.recipe["candidates"][0]))
        self.assertError("duplicate candidate id 'b01'")

    def test_candidate_without_parts(self):
        self.recipe["candidates"][0]["parts"] = []
        self.assertError("selects nothing")


class BuildPlanTests(unittest.TestCase):
    def test_shape_names_are_basis_then_101_channels(self):
        self.assertEqual(len(gk.SHAPE_NAMES), 101)
        self.assertEqual((gk.SHAPE_NAMES[0], gk.SHAPE_NAMES[8], gk.SHAPE_NAMES[-1]),
                         ("shape_01", "shape_09", "shape_101"))
        self.assertEqual(gk.expected_keys()[:2], ["Basis", "shape_01"])
        self.assertEqual(len(gk.expected_keys()), 102)

    def test_plan_for_a_source_with_202_channels_copies_the_first_101(self):
        names = ["Basis"] + ["shape_%02d" % i for i in range(1, 203)]
        plan = gk.plan_shape_keys(names)
        self.assertEqual(plan, [(n, "copy") for n in gk.SHAPE_NAMES])

    def test_plan_for_a_zero_channel_source_is_all_zero_offsets(self):
        self.assertEqual(gk.plan_shape_keys([]), [(n, "zero") for n in gk.SHAPE_NAMES])

    def test_plan_for_a_partial_source_is_refused(self):
        with self.assertRaises(ValueError):
            gk.plan_shape_keys(["Basis", "shape_01", "shape_02"])

    def test_group_parts_by_source_and_transform(self):
        mirror = ({"op": "mirror_copy"},)
        sel = [("A", 1, ()), ("A", 2, ()), ("B", 5, mirror), ("A", 3, mirror), ("A", 7, ()), ("B", 6, mirror)]
        self.assertEqual(gk.group_parts(sel), [("A", [1, 2, 7], ()), ("B", [5, 6], mirror), ("A", [3], mirror)])

    def test_plan_pieces(self):
        self.assertEqual(gk.plan_pieces(()), [(False, (0, 0, 0))])
        self.assertEqual(gk.plan_pieces(({"op": "mirror_copy"},)), [(False, (0, 0, 0)), (True, (0, 0, 0))])
        self.assertEqual(gk.plan_pieces(({"op": "translate", "d": [1, 2, 3]},)), [(False, (1, 2, 3))])

    def test_plan_pieces_order_matters(self):
        t = {"op": "translate", "d": [1, 0, 0]}
        m = {"op": "mirror_copy"}
        # translate then mirror: the copy is the mirror of the moved piece
        self.assertEqual(gk.plan_pieces((t, m)), [(False, (1, 0, 0)), (True, (-1, 0, 0))])
        # mirror then translate: both pieces are moved the same way
        self.assertEqual(gk.plan_pieces((m, t)), [(False, (1, 0, 0)), (True, (1, 0, 0))])

    def test_apply_piece(self):
        pts = [(1.0, 2.0, 3.0)]
        self.assertEqual(gk.apply_piece(pts, (False, (0, 0, 0))), pts)
        self.assertEqual(gk.apply_piece(pts, (True, (0.5, 0, 0))), [(-0.5, 2.0, 3.0)])

    def test_reverse_loop_order_reverses_each_polygon_in_place(self):
        # a triangle (loops 0..2) and a quad (loops 3..6)
        self.assertEqual(gk.reverse_loop_order([0, 3], [3, 4]), [2, 1, 0, 6, 5, 4, 3])
        self.assertEqual(gk.reverse_loop_order([], []), [])


if __name__ == "__main__":
    unittest.main()
