#!/usr/bin/env python3
"""Unit tests for the replay half (tools/register_one_handed_polearms.py).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"

The gate's tests prove a bad state is NOTICED. These prove the fix can be REPLAYED, which is the
other half of CLAUDE.md's dependency-module contract: the Armory is not in this repo, so after any
refresh this script is the only thing standing between the fix and a silent reversion.

Each test maps to a way the replay could be wrong and still look like it worked:
  - derives_pieces_from_items       -> piece ids come from the item, never a second hardcoded list
  - rejects_unknown_item            -> a renamed item fails loudly instead of registering nothing
  - rejects_undefined_piece         -> an XSLT typo is refused; in game it fails silently
  - insert_is_idempotent            -> re-running does not duplicate or churn the block
  - reapply_after_refresh_restores  -> the Armory-overwrite case this script exists for
  - revert_removes_block            -> the gate's own proof step round-trips
  - migrates_legacy_dale_marker     -> an install carrying the old block converges, not doubles
  - preserves_crlf_and_bom          -> byte-level round-trip; text-mode i/o would rewrite the file
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import register_one_handed_polearms as reg  # noqa: E402

LEGACY_START = "<!-- TAOM-DALE-1H:START -->"
LEGACY_END = "<!-- TAOM-DALE-1H:END -->"

# Every id in ONE_HANDED_ITEMS must be defined here: the script aborts on a missing one, which is
# test_rejects_unknown_item's contract. `dale_halberd_a` is the non-target control -- it shares a
# handle with a target and must still not be registered.
ITEMS = """<?xml version="1.0" encoding="utf-8"?>
<Items>
  <CraftedItem id="dale_spear_a" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_blade_1" Type="Blade"/><Piece id="p_handle_1" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="dale_spear_b" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_blade_2" Type="Blade"/><Piece id="p_handle_2" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="dale_winged_spear_a" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_head_3" Type="Blade"/><Piece id="p_handle_2" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="dale_winged_spear_b" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_head_4" Type="Blade"/><Piece id="p_handle_3" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="sm_md_num_lance_a" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_blade_5" Type="Blade"/><Piece id="p_handle_5" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="dale_halberd_a" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="p_head_9" Type="Blade"/><Piece id="p_handle_1" Type="Handle"/></Pieces>
  </CraftedItem>
</Items>
"""

PIECES = """<?xml version="1.0" encoding="utf-8"?>
<CraftingPieces>
  <CraftingPiece id="p_blade_1" piece_type="Blade"/>
  <CraftingPiece id="p_blade_2" piece_type="Blade"/>
  <CraftingPiece id="p_blade_5" piece_type="Blade"/>
  <CraftingPiece id="p_head_3" piece_type="Blade"/>
  <CraftingPiece id="p_head_4" piece_type="Blade"/>
  <CraftingPiece id="p_head_9" piece_type="Blade"/>
  <CraftingPiece id="p_handle_1" piece_type="Handle"/>
  <CraftingPiece id="p_handle_2" piece_type="Handle"/>
  <CraftingPiece id="p_handle_3" piece_type="Handle"/>
  <CraftingPiece id="p_handle_5" piece_type="Handle"/>
</CraftingPieces>
"""

# CRLF plus a passthrough call, shaped like the Armory's real sheet.
XSLT = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\r\n'
    "\t<xsl:template match=\"WeaponDescription[@id='OneHandedPolearm']/AvailablePieces\">\r\n"
    "\t\t<AvailablePieces>\r\n"
    '\t\t\t<AvailablePiece id="existing_blade"/>\r\n'
    '\t\t\t<xsl:apply-templates select="@*|node()"/>\r\n'
    "\t\t</AvailablePieces>\r\n"
    "\t</xsl:template>\r\n"
    "</xsl:stylesheet>\r\n"
)

PASSTHROUGH = '\t\t\t<xsl:apply-templates select="@*|node()"/>\r\n'


class ReplayContractTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        data = Path(self._tmp.name) / "Modules" / "LOTRLOME_Armory" / "ModuleData"
        (data / "LOTRLOME_items").mkdir(parents=True)
        self.items = data / "LOTRLOME_items" / "LOTRAOM_weapons.xml"
        self.pieces = data / "LOTRLOME_crafting_pieces.xml"
        self.items.write_bytes(ITEMS.encode("utf-8"))
        self.pieces.write_bytes(PIECES.encode("utf-8"))

    # -- derivation ------------------------------------------------------------------

    def test_derives_pieces_from_items(self):
        """Piece ids come from each item's own <CraftedItem>, and only from targeted items."""
        pieces, per_item = reg.derive_pieces(self.items)
        self.assertEqual(set(per_item), set(reg.ONE_HANDED_ITEMS))
        # Blades, then heads, then handles -- matching how the surrounding culture blocks read.
        # p_handle_2 appears once despite two items using it; a duplicate would be a silent bloat.
        self.assertEqual(
            pieces,
            [
                "p_blade_1", "p_blade_2", "p_blade_5",
                "p_head_3", "p_head_4",
                "p_handle_1", "p_handle_2", "p_handle_3", "p_handle_5",
            ],
        )
        self.assertNotIn("p_head_9", pieces, "the halberd must stay two-handed")
        self.assertNotIn("dale_halberd_a", per_item)

    def test_rejects_unknown_item(self):
        """A renamed or deleted item must abort, not quietly register a shorter list."""
        broken = ITEMS.replace('id="sm_md_num_lance_a"', 'id="renamed"')
        self.items.write_bytes(broken.encode("utf-8"))
        with self.assertRaises(SystemExit):
            reg.derive_pieces(self.items)

    def test_rejects_undefined_piece(self):
        """An id with no <CraftingPiece> is a typo the engine would swallow in silence."""
        self.assertEqual(reg.check_pieces_exist(self.pieces, ["p_blade_1", "ghost"]), ["ghost"])

    # -- write path ------------------------------------------------------------------

    def test_insert_is_idempotent(self):
        pieces, _ = reg.derive_pieces(self.items)
        once, action = reg.apply_block(XSLT, pieces, "\r\n")
        self.assertEqual(action, "inserted")
        twice, action = reg.apply_block(once, pieces, "\r\n")
        self.assertEqual(action, "noop")
        self.assertEqual(once, twice)
        self.assertEqual(once.count(reg.MARKER_START), 1)

    def test_reapply_after_refresh_restores(self):
        """The case this script exists for: an Armory refresh wipes the block."""
        pieces, _ = reg.derive_pieces(self.items)
        applied, _ = reg.apply_block(XSLT, pieces, "\r\n")
        refreshed, found = reg.revert_block(applied)
        self.assertTrue(found)
        self.assertEqual(refreshed, XSLT, "revert must return the pristine sheet byte for byte")
        again, action = reg.apply_block(refreshed, pieces, "\r\n")
        self.assertEqual(action, "inserted")
        self.assertEqual(again, applied)

    def test_revert_removes_block(self):
        pieces, _ = reg.derive_pieces(self.items)
        applied, _ = reg.apply_block(XSLT, pieces, "\r\n")
        reverted, found = reg.revert_block(applied)
        self.assertTrue(found)
        self.assertNotIn(reg.MARKER_START, reverted)
        self.assertNotIn("p_blade_1", reverted)

    def test_migrates_legacy_dale_marker(self):
        """An install still carrying TAOM-DALE-1H converges on one block, never ends up with two.

        Without this the Dale pieces are listed twice: once under the old marker, which the new
        revert pattern no longer matches, and once under the new one.
        """
        legacy = XSLT.replace(
            PASSTHROUGH,
            PASSTHROUGH
            + "\t\t\t" + LEGACY_START + "\r\n"
            + '\t\t\t<AvailablePiece id="p_blade_1"/>\r\n'
            + '\t\t\t<AvailablePiece id="p_handle_1"/>\r\n'
            + "\t\t\t" + LEGACY_END + "\r\n",
        )
        pieces, _ = reg.derive_pieces(self.items)
        migrated, action = reg.apply_block(legacy, pieces, "\r\n")
        self.assertNotIn(LEGACY_START, migrated, "the legacy block must be removed")
        self.assertEqual(migrated.count('<AvailablePiece id="p_blade_1"/>'), 1)
        self.assertEqual(migrated.count(reg.MARKER_START), 1)
        self.assertIn("p_blade_2", migrated)
        self.assertNotEqual(action, "noop")
        # Converging is stable: a second pass over the migrated sheet changes nothing.
        self.assertEqual(reg.apply_block(migrated, pieces, "\r\n")[1], "noop")

    def test_preserves_crlf_and_bom(self):
        """Text-mode i/o would strip the BOM and renormalise every line, per tools/README.md."""
        pieces, _ = reg.derive_pieces(self.items)
        out, _ = reg.apply_block("﻿" + XSLT, pieces, "\r\n")
        self.assertTrue(out.startswith("﻿"))
        self.assertNotIn("\n", out.replace("\r\n", ""), "a bare LF means the eol was renormalised")
        body = out.split(reg.MARKER_START, 1)[1]
        self.assertTrue(body.startswith("\r\n"), "block lines must keep CRLF endings")


if __name__ == "__main__":
    unittest.main()
