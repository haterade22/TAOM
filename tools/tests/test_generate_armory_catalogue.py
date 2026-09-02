#!/usr/bin/env python3
"""Unit tests for the Armoury mesh catalogue generator.

Pure stdlib, synthetic inputs, no game install. The two things worth pinning are
the ones that were wrong when the tool was first written:

  - the override lookup keys on the ESCAPED name, because one shipped mesh
    carries eight NUL bytes and raw != escaped for exactly that asset
  - rename/move/delete classification cannot join on the tpac path alone. A geo
    tpac holds many meshes, so a genuine deletion from a tpac that also gained a
    name read as a rename until a similarity check was added.
"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_armory_catalogue as g  # noqa: E402


def row(mesh, tpac, referenced="N", folder="f"):
    return {"mesh": mesh, "kind": "metamesh", "prefix": "-", "culture": "c",
            "sub": "-", "category": "chest", "tier": "-", "variant": "-",
            "folder": folder, "tpac": tpac, "referenced": referenced,
            "source": "parsed"}


class EscapeTests(unittest.TestCase):
    def test_plain_name_is_unchanged(self):
        self.assertEqual(g.escape("sk_gd_dol_chest_elite_a"), "sk_gd_dol_chest_elite_a")

    def test_nul_bytes_are_escaped(self):
        self.assertEqual(g.escape("sk_dwarf\x00\x00_10"), "sk_dwarf\\x00\\x00_10")

    def test_space_survives_but_tab_is_escaped(self):
        # spaces are legal in a TSV field; a tab would corrupt the row
        self.assertEqual(g.escape("legolas gloves"), "legolas gloves")
        self.assertEqual(g.escape("a\tb"), "a\\tb")

    def test_backslash_is_escaped_so_the_encoding_round_trips(self):
        self.assertEqual(g.escape("a\\b"), "a\\\\b")


class ClassifyTests(unittest.TestCase):
    def test_canonical_name_parses_on_every_axis(self):
        c = g.classify("sk_gd_dol_cav_helmet_elite_a", "gondor_assets")
        self.assertEqual(c["prefix"], "sk")
        self.assertEqual(c["culture"], "gondor")
        self.assertEqual(c["sub"], "dol")
        self.assertEqual(c["category"], "helmet")
        self.assertEqual(c["tier"], "elite")

    def test_troop_class_is_not_reported_as_a_tier(self):
        """`cav` is a troop class and `elite` is the tier. One shared set made
        whichever token came first win, so half the catalogue called `cav` a tier."""
        c = g.classify("sk_gd_dol_cav_helmet_elite_a", "gondor_assets")
        self.assertEqual(c["tier"], "elite")
        self.assertNotEqual(c["tier"], "cav")

    def test_bo_cap_infix_does_not_hide_the_culture(self):
        """`cap` is a capsule marker, not a culture. Leaving it in shifted every
        token one position and lost the culture for 8 shield bodies."""
        c = g.classify("bo_cap_cts_rohan_shield", "shield")
        self.assertEqual(c["culture"], "rohan")
        self.assertEqual(c["category"], "shield")

    def test_clo_wrapper_is_stripped_and_reparsed(self):
        c = g.classify("clo_sk_gd_vale_cape_a", "gondor_assets")
        self.assertEqual(c["prefix"], "sk")
        self.assertEqual(c["culture"], "gondor")
        self.assertEqual(c["category"], "cape")

    def test_culture_is_found_at_any_position_not_just_two(self):
        """`wm_` mixes culture, region and hero names at position 2, so a
        positional rule cannot serve it."""
        c = g.classify("bo_wm_pelargir_shield_a", "weapons")
        self.assertEqual(c["culture"], "gondor")

    def test_plural_slot_words_resolve(self):
        self.assertEqual(g.classify("am_glorfindel_gauntlets", "Rivendell")["category"], "bracer")
        self.assertEqual(g.classify("am_glorfindel_pauldrons", "Rivendell")["category"], "pauldron")

    def test_folder_supplies_culture_when_the_name_cannot(self):
        c = g.classify("some_legacy_name_a", "dunland_armors_wulf")
        self.assertEqual(c["culture"], "dunland")

    def test_unresolvable_name_reports_unknown_rather_than_guessing(self):
        c = g.classify("zzz_qqq", "Race Test")
        self.assertEqual(c["culture"], "unknown")
        self.assertEqual(c["category"], "unknown")


class ChangeClassificationTests(unittest.TestCase):
    def test_rename_is_detected_within_one_tpac(self):
        old = {"sk_gd_dol_chest_elite_a": row("sk_gd_dol_chest_elite_a", "p/x_geo.tpac")}
        new = {"sk_gd_dol_chest_elite_a2": row("sk_gd_dol_chest_elite_a2", "p/x_geo.tpac")}
        ch = g.classify_changes(old, new)
        self.assertEqual(len(ch["rename"]), 1)
        self.assertEqual(ch["rename"][0][0], "sk_gd_dol_chest_elite_a")
        self.assertEqual(ch["delete"], [])

    def test_deletion_from_a_tpac_that_also_gained_a_name_is_NOT_a_rename(self):
        """The bug the first implementation shipped. A geo tpac holds many
        meshes, so joining on path alone turned a real deletion into a rename
        and the report said nothing was lost. Realistic names, because the
        threshold is calibrated for them."""
        old = {"sk_gd_dol_chest_elite_a": row("sk_gd_dol_chest_elite_a", "p/x_geo.tpac"),
               "wm_gondor_spear_b_blade": row("wm_gondor_spear_b_blade", "p/x_geo.tpac",
                                              referenced="Y")}
        new = {"sk_gd_dol_chest_elite_a2": row("sk_gd_dol_chest_elite_a2", "p/x_geo.tpac")}
        ch = g.classify_changes(old, new)
        self.assertEqual([r[0] for r in ch["rename"]], ["sk_gd_dol_chest_elite_a"])
        self.assertEqual([d[0] for d in ch["delete"]], ["wm_gondor_spear_b_blade"])

    def test_a_dissimilar_addition_in_the_same_tpac_is_not_a_rename(self):
        """Real names, real ratio 0.600. Under the old 0.60 threshold this
        reported a referenced collision-body deletion as a rename with ZERO
        deletions, which is the one direction that matters: it says nothing
        was lost when something was."""
        T = "weapons/Isengard/wm_isengard_weapon_pack_geo.tpac"
        old = {"bo_cap_wm_isengard_shield_a01":
               row("bo_cap_wm_isengard_shield_a01", T, referenced="Y")}
        new = {"wm_isengard_arrow_a03": row("wm_isengard_arrow_a03", T)}
        ch = g.classify_changes(old, new)
        self.assertEqual(ch["rename"], [])
        self.assertEqual(ch["delete"], [("bo_cap_wm_isengard_shield_a01", "Y")])

    def test_ambiguous_candidates_are_not_claimed_by_the_first_comer(self):
        """Greedy assignment let a 0.936 match steal the candidate a 0.979
        match wanted, so the report named the wrong survivor AND the wrong
        casualty. Neither should be claimed without a clear margin."""
        T = "p/x_geo.tpac"
        old = {"sk_gd_dol_chest_elite_a": row("sk_gd_dol_chest_elite_a", T, referenced="Y"),
               "sk_gd_dol_chest_elite_b": row("sk_gd_dol_chest_elite_b", T, referenced="Y")}
        new = {"sk_gd_dol_chest_elite_b2": row("sk_gd_dol_chest_elite_b2", T)}
        ch = g.classify_changes(old, new)
        named = [r[0] for r in ch["rename"]]
        self.assertNotIn("sk_gd_dol_chest_elite_a", named,
                         "the weaker match must not claim the candidate")

    def test_rename_and_move_carry_the_referenced_flag(self):
        """A RENAME breaks every item naming the old id exactly as a DELETE
        does. Only DELETE used to carry the flag, so the 'will break' warning
        never printed for renames."""
        T = "p/x_geo.tpac"
        old = {"sk_gd_dol_chest_elite_a": row("sk_gd_dol_chest_elite_a", T, referenced="Y")}
        new = {"sk_gd_dol_chest_elite_a2": row("sk_gd_dol_chest_elite_a2", T)}
        ch = g.classify_changes(old, new)
        self.assertEqual(len(ch["rename"]), 1)
        self.assertEqual(len(ch["rename"][0]), 3, "rename tuple must carry referenced")
        self.assertEqual(ch["rename"][0][2], "Y")

    def test_a_referenced_deletion_carries_its_referenced_flag(self):
        old = {"gone": row("gone", "p/x_geo.tpac", referenced="Y")}
        ch = g.classify_changes(old, {})
        self.assertEqual(ch["delete"], [("gone", "Y")])

    def test_same_name_new_folder_is_a_move_not_a_deletion(self):
        old = {"m": row("m", "old/x_geo.tpac")}
        new = {"m": row("m", "new/x_geo.tpac")}
        ch = g.classify_changes(old, new)
        self.assertEqual(ch["moved_same_name"], ["m"])
        self.assertEqual(ch["delete"], [])

    def test_one_added_name_cannot_absorb_two_deletions(self):
        old = {"sk_gd_dol_chest_elite_a": row("sk_gd_dol_chest_elite_a", "p/x_geo.tpac"),
               "sk_gd_dol_chest_elite_b": row("sk_gd_dol_chest_elite_b", "p/x_geo.tpac")}
        new = {"sk_gd_dol_chest_elite_a2": row("sk_gd_dol_chest_elite_a2", "p/x_geo.tpac")}
        ch = g.classify_changes(old, new)
        self.assertEqual(len(ch["rename"]), 1)
        self.assertEqual(len(ch["delete"]), 1, "the second must not reuse the same match")

    def test_unrelated_new_name_is_reported_as_new(self):
        old = {}
        new = {"brand_new": row("brand_new", "p/y_geo.tpac")}
        ch = g.classify_changes(old, new)
        self.assertEqual(ch["new"], ["brand_new"])


class ColumnContractTests(unittest.TestCase):
    def test_columns_are_stable(self):
        """The committed TSV is diffed across art drops, so a column change is a
        format break that invalidates every prior snapshot."""
        self.assertEqual(g.COLUMNS, [
            "mesh", "kind", "prefix", "culture", "sub", "category", "tier",
            "variant", "folder", "tpac", "referenced", "source"])

    def test_tpac_column_is_present_because_it_is_the_rename_key(self):
        self.assertIn("tpac", g.COLUMNS)


if __name__ == "__main__":
    unittest.main(verbosity=2)
