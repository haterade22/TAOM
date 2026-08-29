#!/usr/bin/env python3
"""Unit tests for the Rohan spear reforge (tools/apply_rohan_spear_reforge.py).

Pure stdlib, synthetic XML, no game install. Every transform is a pure function over a
document string, so the traps this repo has already paid for are pinned here:

  - exact id matching, never a prefix (wm_rohan_spear_a must not eat wm_rohan_spear_a_blade)
  - comments are never rewritten, and survive a length-changing edit
  - XSLT inserts land BEFORE <xsl:apply-templates>, or the passthrough stops being last
  - a removal takes the whole element, self-closing or not
  - the mapping itself is checked: no target is also a thing being deleted
"""
import os
import sys
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_rohan_spear_reforge as rs  # noqa: E402


class TestRemoveCraftingPieces(unittest.TestCase):
    def test_removes_the_whole_element(self):
        xml = ('<CraftingPieces>\n'
               '  <CraftingPiece id="keep" piece_type="Blade" />\n'
               '  <CraftingPiece id="drop" piece_type="Blade">\n'
               '    <BladeData body_name="bo_drop" />\n'
               '  </CraftingPiece>\n'
               '</CraftingPieces>\n')
        out, removed = rs.remove_crafting_pieces(xml, {"drop"})
        self.assertEqual(removed, ["drop"])
        self.assertNotIn("drop", out)
        self.assertIn('id="keep"', out)
        ET.fromstring(out)

    def test_exact_id_only(self):
        xml = '<CraftingPieces><CraftingPiece id="drop_extra" /></CraftingPieces>'
        out, removed = rs.remove_crafting_pieces(xml, {"drop"})
        self.assertEqual(removed, [])
        self.assertIn("drop_extra", out)

    def test_comments_survive(self):
        xml = ('<CraftingPieces>\n'
               '  <!-- Rohan spears -->\n'
               '  <CraftingPiece id="drop" />\n'
               '  <!-- end -->\n'
               '</CraftingPieces>\n')
        out, removed = rs.remove_crafting_pieces(xml, {"drop"})
        self.assertEqual(removed, ["drop"])
        self.assertIn("<!-- Rohan spears -->", out)
        self.assertIn("<!-- end -->", out)
        self.assertNotIn("\x00", out)


class TestInsertCraftingPieces(unittest.TestCase):
    def test_inserts_before_the_closing_tag(self):
        xml = '<CraftingPieces>\n  <CraftingPiece id="a" />\n</CraftingPieces>\n'
        block = '  <CraftingPiece id="new" />\n'
        out, _ = rs.insert_crafting_pieces(xml, block)
        self.assertLess(out.index('id="new"'), out.index("</CraftingPieces>"))
        ET.fromstring(out)

    def test_is_idempotent(self):
        xml = '<CraftingPieces>\n  <CraftingPiece id="new" />\n</CraftingPieces>\n'
        block = '  <CraftingPiece id="new" />\n'
        self.assertEqual(rs.insert_crafting_pieces(xml, block)[0], xml)


class TestXsltPieceRefs(unittest.TestCase):
    XSLT = (
        '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
        '<xsl:template match="WeaponDescription[@id=\'OneHandedPolearm\']/AvailablePieces">\n'
        '  <AvailablePieces>\n'
        '    <AvailablePiece id="old_blade" />\n'
        '    <xsl:apply-templates select="@*|node()"/>\n'
        '  </AvailablePieces>\n'
        '</xsl:template>\n'
        '</xsl:stylesheet>\n'
    )

    def test_removes_a_ref(self):
        out, n = rs.remove_xslt_piece_refs(self.XSLT, {"old_blade"})
        self.assertEqual(n, 1)
        self.assertNotIn("old_blade", out)
        ET.fromstring(out)

    def test_insert_lands_before_the_passthrough(self):
        out, _ = rs.insert_xslt_piece_refs(
            self.XSLT, "WeaponDescription[@id='OneHandedPolearm']/AvailablePieces",
            "AvailablePiece", "id", ["new_blade"])
        self.assertIn('<AvailablePiece id="new_blade" />', out)
        self.assertLess(out.index("new_blade"), out.index("xsl:apply-templates"),
                        "the XSLT passthrough must stay last")
        ET.fromstring(out)

    def test_insert_is_idempotent(self):
        once, _ = rs.insert_xslt_piece_refs(
            self.XSLT, "WeaponDescription[@id='OneHandedPolearm']/AvailablePieces",
            "AvailablePiece", "id", ["new_blade"])
        twice, _ = rs.insert_xslt_piece_refs(
            once, "WeaponDescription[@id='OneHandedPolearm']/AvailablePieces",
            "AvailablePiece", "id", ["new_blade"])
        self.assertEqual(once, twice)

    def test_unknown_category_is_a_no_op(self):
        out, _ = rs.insert_xslt_piece_refs(
            self.XSLT, "WeaponDescription[@id='Nope']/AvailablePieces",
            "AvailablePiece", "id", ["x"])
        self.assertEqual(out, self.XSLT)


class TestReviewFindings(unittest.TestCase):
    """Regressions from the 2026-08-28 deep review of this script."""

    def test_newline_is_picked_by_majority_not_presence(self):
        # tools/README.md names presence-testing as the WRONG test, because
        # LOTRLOME_items/mordor/*_armors.xml are genuinely mixed (2356 CRLF vs 517 bare
        # LF). One stray CRLF must not flip an LF file's inserts to CRLF.
        mostly_lf = "a\n" * 100 + "b\r\n"
        mostly_crlf = "a\r\n" * 100 + "b\n"
        self.assertEqual(rs.dominant_newline(mostly_lf), "\n")
        self.assertEqual(rs.dominant_newline(mostly_crlf), "\r\n")

    def test_insert_reports_the_actual_count_not_the_request(self):
        # A no-op second run must report 0 inserted, not 4. Reporting the request size
        # overclaims success and would mask a silent insert failure.
        xml = '<CraftingPieces>\n  <CraftingPiece id="new" />\n</CraftingPieces>\n'
        block = '  <CraftingPiece id="new" />\n'
        _, n = rs.insert_crafting_pieces(xml, block)
        self.assertEqual(n, 0)
        _, n2 = rs.insert_crafting_pieces('<CraftingPieces>\n</CraftingPieces>\n', block)
        self.assertEqual(n2, 1)

    def test_insert_reports_zero_when_the_anchor_is_missing(self):
        # rfind("</CraftingPieces>") == -1 returned the text unchanged while the caller
        # still printed "+4 pieces".
        _, n = rs.insert_crafting_pieces('<Wrong></Wrong>', '  <CraftingPiece id="x" />\n')
        self.assertEqual(n, 0)

    def test_xslt_insert_reports_actual_count(self):
        out, n = rs.insert_xslt_piece_refs(
            TestXsltPieceRefs.XSLT,
            "WeaponDescription[@id='OneHandedPolearm']/AvailablePieces",
            "AvailablePiece", "id", ["p1", "p2"])
        self.assertEqual(n, 2)
        _, again = rs.insert_xslt_piece_refs(
            out, "WeaponDescription[@id='OneHandedPolearm']/AvailablePieces",
            "AvailablePiece", "id", ["p1", "p2"])
        self.assertEqual(again, 0)

    def test_repoint_only_touches_Piece_elements(self):
        # The substitution ran over the whole CraftedItem block, so a sibling carrying
        # the same id="..." Type="..." adjacency would have been rewritten too.
        xml = ('<Items><CraftedItem id="w">\n'
               '  <Pieces><Piece id="old_blade" Type="Blade" /></Pieces>\n'
               '  <Decoy id="do_not_touch" Type="Blade" />\n'
               '</CraftedItem></Items>\n')
        out, n = rs.repoint_crafted_item(xml, "w", {"Blade": "new_blade"})
        self.assertEqual(n, 1)
        self.assertIn('<Piece id="new_blade" Type="Blade" />', out)
        self.assertIn('<Decoy id="do_not_touch" Type="Blade" />', out)


class TestCraftedItems(unittest.TestCase):
    ITEMS = (
        '<Items>\n'
        '  <CraftedItem id="wm_rohan_spear_a" crafting_template="TwoHandedPolearm">\n'
        '    <Pieces>\n'
        '      <Piece id="wm_rohan_spear_a_blade" Type="Blade" scale_factor="100" />\n'
        '      <Piece id="wm_rohan_spear_a_handle" Type="Handle" scale_factor="100" />\n'
        '    </Pieces>\n'
        '  </CraftedItem>\n'
        '  <CraftedItem id="wm_rohan_spear_c" crafting_template="TwoHandedPolearm">\n'
        '    <Pieces><Piece id="wm_rohan_spear_c_blade" Type="Blade" /></Pieces>\n'
        '  </CraftedItem>\n'
        '</Items>\n'
    )

    def test_repoint_swaps_only_the_named_item(self):
        out, n = rs.repoint_crafted_item(
            self.ITEMS, "wm_rohan_spear_a",
            {"Blade": "sm_ro_rohan_spear_blade_a", "Handle": "sm_ro_rohan_spear_handle_a"})
        self.assertEqual(n, 2)
        self.assertIn('id="sm_ro_rohan_spear_blade_a" Type="Blade"', out)
        self.assertIn('id="sm_ro_rohan_spear_handle_a" Type="Handle"', out)
        self.assertIn('id="wm_rohan_spear_c_blade"', out, "other items untouched")
        ET.fromstring(out)

    def test_remove_crafted_items(self):
        out, removed = rs.remove_crafted_items(self.ITEMS, {"wm_rohan_spear_c"})
        self.assertEqual(removed, ["wm_rohan_spear_c"])
        self.assertNotIn("wm_rohan_spear_c", out)
        self.assertIn('id="wm_rohan_spear_a"', out)
        ET.fromstring(out)

    def test_remove_is_exact(self):
        out, removed = rs.remove_crafted_items(self.ITEMS, {"wm_rohan_spear"})
        self.assertEqual(removed, [])


class TestMappingIntegrity(unittest.TestCase):
    def test_no_new_piece_is_also_being_deleted(self):
        for pid in rs.NEW_PIECE_IDS:
            self.assertNotIn(pid, rs.OLD_PIECE_IDS)

    def test_no_surviving_item_is_also_being_deleted(self):
        for item in rs.ITEM_PIECES:
            self.assertNotIn(item, rs.DELETE_ITEMS)

    def test_every_roster_remap_targets_a_surviving_item(self):
        for old, new in rs.ITEM_REMAP.items():
            self.assertIn(old, rs.DELETE_ITEMS, f"{old} is remapped but not deleted")
            self.assertIn(new, rs.ITEM_PIECES, f"{new} is a remap target but not kept")

    def test_the_new_pieces_are_thrust_only_spear_heads(self):
        # excluded_item_usage_features="swing" on a thrust-only head. Declaring a swing the
        # animation set cannot deliver is the crafting-usage-features defect class.
        for block in rs.NEW_PIECE_BLOCKS:
            if 'piece_type="Blade"' in block:
                self.assertIn('excluded_item_usage_features="swing"', block)
                self.assertIn("<Thrust", block)
                self.assertNotIn("<Swing", block)

    def test_every_new_blade_declares_its_collision_body(self):
        # A body_name the engine cannot resolve hangs mission load forever (#352).
        for block in rs.NEW_PIECE_BLOCKS:
            if 'piece_type="Blade"' in block:
                self.assertIn('body_name="bo_sm_ro_rohan_spear_blade_', block)

    def test_handles_carry_no_collision_body(self):
        for block in rs.NEW_PIECE_BLOCKS:
            if 'piece_type="Handle"' in block:
                self.assertNotIn("body_name", block)

    def test_couchable_is_registered(self):
        self.assertIn("TwoHandedPolearm_Couchable",
                      " ".join(rs.WEAPON_DESCRIPTION_CATEGORIES))

    def test_onehanded_polearm_is_registered_for_shield_troops(self):
        # 8 Rohan rosters pair these spears with a shield. Absent from OneHandedPolearm the
        # primary usage resolves requires_no_shield and the troop never draws the spear.
        self.assertIn("OneHandedPolearm", " ".join(rs.WEAPON_DESCRIPTION_CATEGORIES))


if __name__ == "__main__":
    unittest.main(verbosity=2)
