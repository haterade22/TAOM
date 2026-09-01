#!/usr/bin/env python3
"""Unit tests for the dead-mesh item swap (tools/apply_dead_mesh_item_swaps.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

Pure stdlib, synthetic XML, no game install. The three text transforms are pure
functions over a document string, so every edge case that has bitten this repo
before is pinned here rather than discovered in-game:

  - a swap must be an EXACT id match; `easterling_head` must never be rewritten
    inside `easterling_head_v2`
  - swaps must not touch an item DEFINITION, only references to it
  - comments must not be rewritten (a commented example is not a live ref)
  - removing an item definition must take the whole element, self-closing or not
  - byte-faithful I/O: a BOM present on read is present on write, and CRLF is
    preserved (a text-mode round-trip silently rewrites the whole file)
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import apply_dead_mesh_item_swaps as sw  # noqa: E402


# --------------------------------------------------------------------------- #
# Reference swapping                                                           #
# --------------------------------------------------------------------------- #
class TestSwapItemRefs(unittest.TestCase):
    def test_swaps_a_reference(self):
        xml = '<equipment slot="Body" id="Item.angbor_body" />'
        out, n = sw.swap_item_refs(xml, {"angbor_body": "sk_gd_lam_inf_chest_lord_a"})
        self.assertEqual(n, 1)
        self.assertIn('id="Item.sk_gd_lam_inf_chest_lord_a"', out)
        self.assertNotIn("angbor_body", out)

    def test_match_is_exact_not_prefix(self):
        # easterling_head is a strict prefix of easterling_head_v2. A substring
        # rewrite would silently corrupt the longer id.
        xml = '<equipment id="Item.easterling_head_v2" />'
        out, n = sw.swap_item_refs(xml, {"easterling_head": "sk_rh_loke_helmet_inf_elite_i"})
        self.assertEqual(n, 0)
        self.assertEqual(out, xml)

    def test_definitions_are_not_rewritten(self):
        # An <Item id="x"> in an Armory file DEFINES x. Only Item.-prefixed
        # references may be swapped.
        xml = '<Item id="angbor_body" mesh="angbor_body" Type="BodyArmor" />'
        out, n = sw.swap_item_refs(xml, {"angbor_body": "replacement"})
        self.assertEqual(n, 0)
        self.assertEqual(out, xml)

    def test_comments_are_not_rewritten(self):
        xml = '<!-- <equipment id="Item.angbor_body" /> -->'
        out, n = sw.swap_item_refs(xml, {"angbor_body": "replacement"})
        self.assertEqual(n, 0)
        self.assertEqual(out, xml)

    def test_every_occurrence_is_swapped(self):
        xml = ('<equipment id="Item.easterling_boots" />\n'
               '<Equipment id="Item.easterling_boots" />\n'
               '<item id="Item.easterling_boots" />')
        out, n = sw.swap_item_refs(xml, {"easterling_boots": "sk_rh_loke_grvs_plate_light_a"})
        self.assertEqual(n, 3)
        self.assertNotIn("easterling_boots", out)

    def test_unrelated_ids_are_untouched(self):
        xml = '<equipment id="Item.keep_me" />'
        out, n = sw.swap_item_refs(xml, {"other": "x"})
        self.assertEqual((out, n), (xml, 0))


class TestCommentsSurviveLengthChange(unittest.TestCase):
    """Regression, 2026-08-28. Comments were masked by byte offset and restored
    at those same offsets. A swap changes the text length, so restoration wrote
    each comment back in the wrong place, splicing `<!-- GOLASGIL -->` into the
    middle of an item id and leaving NUL bytes behind. It corrupted 8 files
    before the well-formedness check caught it."""

    def test_comments_survive_a_length_changing_swap(self):
        import xml.etree.ElementTree as ET
        xml = ('<Roster>\n'
               '  <!-- ==================== ANGBOR ==================== -->\n'
               '  <Equipment slot="Leg" id="Item.angbor_boots" />\n'
               '  <!-- ==================== GOLASGIL ==================== -->\n'
               '  <Equipment slot="Leg" id="Item.golasgil_boots" />\n'
               '</Roster>\n')
        out, n = sw.swap_item_refs(xml, {
            "angbor_boots": "sk_gd_sere_grvs_lord_a",       # longer
            "golasgil_boots": "sk_gd_sere_grvs_lord_a",     # longer
        })
        self.assertEqual(n, 2)
        self.assertIn("<!-- ==================== ANGBOR ==================== -->", out)
        self.assertIn("<!-- ==================== GOLASGIL ==================== -->", out)
        self.assertNotIn("\x00", out)
        ET.fromstring(out)

    def test_shorter_replacement_also_keeps_comments_intact(self):
        import xml.etree.ElementTree as ET
        xml = ('<Roster>\n'
               '  <!-- a comment -->\n'
               '  <Equipment id="Item.sk_dwarf_erebor_bracers_elite_d_blue" />\n'
               '  <!-- another -->\n'
               '</Roster>\n')
        out, n = sw.swap_item_refs(
            xml, {"sk_dwarf_erebor_bracers_elite_d_blue": "sk_dwarf_erebor_bracers_elite_d"})
        self.assertEqual(n, 1)
        self.assertIn("<!-- a comment -->", out)
        self.assertIn("<!-- another -->", out)
        ET.fromstring(out)

    def test_removal_keeps_surrounding_comments(self):
        import xml.etree.ElementTree as ET
        xml = ('<Items>\n'
               '  <!-- keep me -->\n'
               '  <Item id="drop_me" mesh="dead" />\n'
               '  <!-- keep me too -->\n'
               '  <Item id="keep" mesh="live" />\n'
               '</Items>\n')
        out, removed = sw.remove_item_defs(xml, {"drop_me"})
        self.assertEqual(removed, ["drop_me"])
        self.assertIn("<!-- keep me -->", out)
        self.assertIn("<!-- keep me too -->", out)
        self.assertNotIn("\x00", out)
        ET.fromstring(out)


# --------------------------------------------------------------------------- #
# Mesh re-pointing                                                             #
# --------------------------------------------------------------------------- #
class TestRepointMesh(unittest.TestCase):
    def test_repoints_only_the_named_item(self):
        xml = ('<Item id="starter_ranged_khuzait_leg_a" mesh="easterling_boots" Type="LegArmor" />\n'
               '<Item id="other" mesh="easterling_boots" Type="LegArmor" />')
        out, n = sw.repoint_mesh(xml, {"starter_ranged_khuzait_leg_a": "sk_rh_loke_boots_a"})
        self.assertEqual(n, 1)
        self.assertIn('id="starter_ranged_khuzait_leg_a" mesh="sk_rh_loke_boots_a"', out)
        self.assertIn('<Item id="other" mesh="easterling_boots"', out)

    def test_handles_a_multiline_item_open(self):
        xml = ('<Item\n'
               '    id="starter_infantry_khuzait_leg_a"\n'
               '    mesh="easterlingwarriors01_boots"\n'
               '    Type="LegArmor" />')
        out, n = sw.repoint_mesh(xml, {"starter_infantry_khuzait_leg_a": "sk_rh_loke_boots_a"})
        self.assertEqual(n, 1)
        self.assertIn('mesh="sk_rh_loke_boots_a"', out)

    def test_is_idempotent(self):
        xml = '<Item id="a" mesh="new_mesh" />'
        out, n = sw.repoint_mesh(xml, {"a": "new_mesh"})
        self.assertEqual(n, 0)
        self.assertEqual(out, xml)


# --------------------------------------------------------------------------- #
# Item-definition removal                                                      #
# --------------------------------------------------------------------------- #
class TestRemoveItemDefs(unittest.TestCase):
    def test_removes_a_self_closing_item(self):
        xml = ('<Items>\n'
               '  <Item id="keep" mesh="m" />\n'
               '  <Item id="drop_me" mesh="dead" />\n'
               '</Items>\n')
        out, removed = sw.remove_item_defs(xml, {"drop_me"})
        self.assertEqual(removed, ["drop_me"])
        self.assertNotIn("drop_me", out)
        self.assertIn('id="keep"', out)

    def test_removes_an_item_with_children(self):
        xml = ('<Items>\n'
               '  <Item id="drop_me" mesh="dead">\n'
               '    <ItemComponent><Armor body_armor="10" /></ItemComponent>\n'
               '  </Item>\n'
               '  <Item id="keep" />\n'
               '</Items>\n')
        out, removed = sw.remove_item_defs(xml, {"drop_me"})
        self.assertEqual(removed, ["drop_me"])
        self.assertNotIn("drop_me", out)
        self.assertNotIn("body_armor", out)
        self.assertIn('id="keep"', out)

    def test_leaves_a_reference_alone(self):
        # Removal targets definitions. A ref to the id is a separate concern and
        # must have been swapped first; silently eating it would hide a bug.
        xml = '<equipment id="Item.drop_me" />'
        out, removed = sw.remove_item_defs(xml, {"drop_me"})
        self.assertEqual(removed, [])
        self.assertEqual(out, xml)

    def test_unknown_id_removes_nothing(self):
        xml = '<Item id="a" />'
        out, removed = sw.remove_item_defs(xml, {"nope"})
        self.assertEqual((out, removed), (xml, []))

    def test_exact_id_match_only(self):
        xml = '<Item id="drop_me_too" />'
        out, removed = sw.remove_item_defs(xml, {"drop_me"})
        self.assertEqual(removed, [])
        self.assertIn("drop_me_too", out)


# --------------------------------------------------------------------------- #
# Byte-faithful I/O (.claude/rules/moduledata-validation.md)                   #
# --------------------------------------------------------------------------- #
class TestByteFaithfulIO(unittest.TestCase):
    def test_bom_and_crlf_survive_a_round_trip(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "t.xml"
            original = b'\xef\xbb\xbf<Items>\r\n  <Item id="a" />\r\n</Items>\r\n'
            p.write_bytes(original)
            text, had_bom = sw.read_xml(p)
            self.assertTrue(had_bom)
            self.assertIn("\r\n", text)
            sw.write_xml(p, text, had_bom)
            self.assertEqual(p.read_bytes(), original)

    def test_no_bom_stays_no_bom(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "t.xml"
            original = b'<Items>\n  <Item id="a" />\n</Items>\n'
            p.write_bytes(original)
            text, had_bom = sw.read_xml(p)
            self.assertFalse(had_bom)
            sw.write_xml(p, text, had_bom)
            self.assertEqual(p.read_bytes(), original)


# --------------------------------------------------------------------------- #
# The mapping itself                                                           #
# --------------------------------------------------------------------------- #
class TestMappingIntegrity(unittest.TestCase):
    def test_no_target_is_also_a_dead_source(self):
        # Swapping one dead item onto another dead item would look like a fix
        # and change nothing.
        dead = set(sw.ITEM_SWAPS) | set(sw.DELETE_ITEMS)
        for old, new in sw.ITEM_SWAPS.items():
            self.assertNotIn(new, dead, f"{old} -> {new} targets a dead item")

    def test_every_swap_changes_the_id(self):
        for old, new in sw.ITEM_SWAPS.items():
            self.assertNotEqual(old, new)

    def test_the_five_erebor_swaps_drop_only_the_colour_suffix(self):
        erebor = {k: v for k, v in sw.ITEM_SWAPS.items() if k.startswith("sk_dwarf_erebor")}
        self.assertEqual(len(erebor), 5)
        for old, new in erebor.items():
            self.assertTrue(old.endswith(("_blue", "_green", "_red")))
            self.assertEqual(new, old.rsplit("_", 1)[0])

    def test_all_57_erebor_colour_items_are_marked_for_removal(self):
        # Scoped to the Erebor family rather than the whole set: the 2026-09-01
        # wave added 83 more deletions, and pinning len(DELETE_ITEMS) would make
        # this test fail on every later wave without saying anything true.
        erebor = {i for i in sw.DELETE_ITEMS if i.startswith("sk_dwarf_erebor")}
        self.assertEqual(len(erebor), 57)
        for i in erebor:
            self.assertTrue(i.endswith(("_blue", "_green", "_red")))

    def test_the_five_equipped_erebor_items_are_also_removed(self):
        # Their references get swapped first, then the definition goes.
        for old in [k for k in sw.ITEM_SWAPS if k.startswith("sk_dwarf_erebor")]:
            self.assertIn(old, sw.DELETE_ITEMS)

    def test_starter_items_are_re_meshed_not_swapped(self):
        # Their armour is tuned to 5/7/9 for the career start; the lowest
        # surviving Loke leg item is 15, so swapping would triple it.
        starters = {i for i in sw.MESH_REPOINTS if i.startswith("starter_")}
        self.assertEqual(len(starters), 3)
        for i in starters:
            self.assertNotIn(i, sw.ITEM_SWAPS)

    def test_no_id_is_both_deleted_and_re_meshed(self):
        """The two mechanisms contradict each other, and the deletion would win
        silently: remove_item_defs runs after repoint_mesh, so a contradictory
        entry looks applied and then vanishes."""
        self.assertEqual(set(sw.MESH_REPOINTS) & set(sw.DELETE_ITEMS), set())

    def test_easterling_crafting_pieces_are_re_meshed_never_deleted(self):
        """The trap of the 2026-09-01 wave.

        audit_deleted_mesh_impact.py reports all six as ORPHAN because it matches
        `Item.<id>` refs and rosters, and a crafting piece is named by neither: it
        is referenced by <UsablePiece> in crafting_templates.xslt and by <Piece>
        inside the CraftedItems easterling_sword and easterling_spear.
        easterling_spear is player career starting equipment, so acting on that
        ORPHAN verdict would have stripped a Rhun start's weapon.
        """
        pieces = {
            "easterling_sword_blade", "easterling_sword_guard",
            "easterling_sword_handle", "easterling_sword_pommel",
            "easterling_spear_blade", "easterling_spear_handle",
        }
        for p in pieces:
            self.assertIn(p, sw.MESH_REPOINTS, f"{p} must be re-meshed")
            self.assertNotIn(p, sw.DELETE_ITEMS, f"{p} must never be deleted")

    def test_troll_armour_is_left_alone_by_this_tool(self):
        """No donor exists, and deleting it would take the cave_troll from 95 to
        0 armour in every slot. It is gated in validate_mesh_refs.KNOWN_DEAD_MESHES
        instead, so it must appear in neither mechanism here."""
        for i in ("lotr_troll_armor", "lotr_troll_bracers", "lotr_troll_helmet"):
            self.assertIn(i, sw.NOT_COVERED)
            self.assertNotIn(i, sw.DELETE_ITEMS)
            self.assertNotIn(i, sw.MESH_REPOINTS)
            self.assertNotIn(i, sw.ITEM_SWAPS)


if __name__ == "__main__":
    unittest.main(verbosity=2)
