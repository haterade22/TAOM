#!/usr/bin/env python3
"""tools/register_couch_brace_polearms.py: the Armory registers the spec's spear pieces under the couch and
brace weapon descriptions, replayable and checkable. Synthetic Armory only, no game install.

Run:  python -m unittest tools.tests.test_register_couch_brace_polearms

Each test maps to a way the replay could look done and be wrong: a piece list written by hand, a reinstall
that silently undoes it, a write that rewrites the whole file, a missing item that registers nothing.
"""
import contextlib
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import register_couch_brace_polearms as reg  # noqa: E402

XSLT_HEAD = ('<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\r\n'
             '\t<xsl:template match="@*|node()"><xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>'
             '</xsl:template>\r\n')


def template(description, pieces):
    rows = "".join(f'\t\t\t<AvailablePiece id="{p}"/>\r\n' for p in pieces)
    return (f"\t<!-- {description} -->\r\n"
            f"\t<xsl:template match=\"WeaponDescription[@id='{description}']/AvailablePieces\">\r\n"
            f"\t\t<AvailablePieces>\r\n{rows}\t\t</AvailablePieces>\r\n\t</xsl:template>\r\n")


def xslt(couch=("blade_ok", "handle_ok"), brace=("blade_ok", "handle_ok")):
    return (XSLT_HEAD + template("TwoHandedPolearm", ("blade_new_a", "blade_new_b", "handle_ok", "blade_ok"))
            + template("TwoHandedPolearm_Couchable", couch)
            + template("TwoHandedPolearm_Bracing", brace) + "</xsl:stylesheet>\r\n")


ITEMS = """<?xml version="1.0" encoding="utf-8"?>
<Items>
  <CraftedItem id="spear_a" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="blade_new_a" Type="Blade"/><Piece id="handle_ok" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="spear_b" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="blade_new_b" Type="Blade"/><Piece id="handle_ok" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="spear_third_party" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="blade_new_a" Type="Blade"/><Piece id="handle_ok" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="spear_same_blade_other_handle" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="blade_new_a" Type="Blade"/><Piece id="handle_not_listed" Type="Handle"/></Pieces>
  </CraftedItem>
  <CraftedItem id="spear_already" crafting_template="TwoHandedPolearm">
    <Pieces><Piece id="blade_ok" Type="Blade"/><Piece id="handle_ok" Type="Handle"/></Pieces>
  </CraftedItem>
</Items>
"""
PIECES = """<?xml version="1.0" encoding="utf-8"?>
<CraftingPieces>
  <CraftingPiece id="blade_new_a" piece_type="Blade"/><CraftingPiece id="blade_new_b" piece_type="Blade"/>
  <CraftingPiece id="blade_ok" piece_type="Blade"/><CraftingPiece id="handle_ok" piece_type="Handle"/>
  <CraftingPiece id="handle_not_listed" piece_type="Handle"/>
</CraftingPieces>
"""
SPEC = {"comment": "test", "descriptions": ["TwoHandedPolearm_Couchable", "TwoHandedPolearm_Bracing"],
        "items": [{"id": "spear_a", "reason": "r"}, {"id": "spear_b", "reason": "r"}]}


class Base(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        root = Path(self._tmp.name)
        self.armory = root / "LOTRLOME_Armory"
        data = self.armory / "ModuleData"
        (data / "LOTRLOME_items").mkdir(parents=True)
        self.xslt = data / "weapon_descriptions.xslt"
        self.xslt.write_bytes(xslt().encode("utf-8"))
        (data / "LOTRLOME_items" / "weapons.xml").write_text(ITEMS, encoding="utf-8")
        (data / "LOTRLOME_crafting_pieces.xml").write_text(PIECES, encoding="utf-8")
        self.spec = root / "spec.json"
        self.spec.write_text(json.dumps(SPEC), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def run_tool(self, *args, armory=None):
        out = io.StringIO()
        argv = ["--armory-path", str(armory or self.armory), "--spec", str(self.spec), *args]
        with contextlib.redirect_stdout(out):
            code = reg.main(argv)
        return code, out.getvalue()


class DryRunAndApplyTests(Base):
    def test_a_dry_run_names_the_missing_pieces_and_writes_nothing(self):
        before = self.xslt.read_bytes()
        code, out = self.run_tool()
        self.assertEqual(code, 0)
        self.assertEqual(self.xslt.read_bytes(), before)
        self.assertIn("TwoHandedPolearm_Couchable", out)
        self.assertIn("blade_new_a", out)
        self.assertIn("DRY RUN", out)

    def test_apply_adds_only_the_pieces_the_description_lacks_and_keeps_every_other_byte(self):
        before = self.xslt.read_bytes().decode("utf-8")
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 0, out)
        after = self.xslt.read_bytes().decode("utf-8")
        self.assertIn("\r\n", after)
        self.assertNotIn("\n", after.replace("\r\n", ""), "line endings stay CRLF throughout")
        for description in SPEC["descriptions"]:
            body = after.split(f"@id='{description}']")[1].split("</xsl:template>")[0]
            self.assertIn('<AvailablePiece id="blade_new_a"/>', body)
            self.assertIn('<AvailablePiece id="blade_new_b"/>', body)
            self.assertEqual(body.count('id="handle_ok"'), 1, "a piece already listed is not listed twice")
        untouched = after.split("TAOM-COUCH-BRACE:BEGIN")[0]
        self.assertTrue(before.startswith(untouched[:200]))
        # The first template, which the spec does not name, is byte for byte what it was.
        self.assertEqual(after.split("@id='TwoHandedPolearm_Couchable'")[0], before.split("@id='TwoHandedPolearm_Couchable'")[0])

    def test_the_backup_sits_beside_the_file_once_and_does_not_end_in_xslt(self):
        before = self.xslt.read_bytes()
        self.run_tool("--apply", "--backup-tag", "t1")
        backup = Path(str(self.xslt) + ".bak-t1")
        self.assertEqual(backup.read_bytes(), before)
        self.run_tool("--apply", "--backup-tag", "t2")      # nothing changes, so no second backup
        self.assertFalse(Path(str(self.xslt) + ".bak-t2").exists())

    def test_a_second_apply_changes_nothing_and_check_passes(self):
        self.run_tool("--apply", "--backup-tag", "t1")
        once = self.xslt.read_bytes()
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 0)
        self.assertEqual(self.xslt.read_bytes(), once)
        self.assertEqual(self.run_tool("--check")[0], 0)

    def test_apply_without_a_tag_is_refused(self):
        with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
            self.run_tool("--apply")

    def test_the_bom_is_kept(self):
        self.xslt.write_bytes(b"\xef\xbb\xbf" + self.xslt.read_bytes())
        self.run_tool("--apply", "--backup-tag", "t1")
        self.assertTrue(self.xslt.read_bytes().startswith(b"\xef\xbb\xbf"))

    def test_every_item_built_from_the_same_pieces_is_named_as_gaining_the_usage(self):
        _, out = self.run_tool()
        self.assertIn("items that gain it: spear_a, spear_b, spear_third_party", out)
        self.assertIn("still do not fit: spear_same_blade_other_handle", out)


class CheckTests(Base):
    def test_check_reports_drift_before_and_passes_after_and_again_after_a_reinstall(self):
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("DRIFT", out)
        self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(self.run_tool("--check")[0], 0)
        self.xslt.write_bytes(xslt().encode("utf-8"))        # an Armory refresh puts the old file back
        self.assertEqual(self.run_tool("--check")[0], 1)

    def test_check_without_the_install_cannot_run(self):
        code, out = self.run_tool("--check", armory=Path(self._tmp.name) / "nowhere")
        self.assertEqual(code, 2)
        self.assertIn("SKIPPED", out)

    def test_an_item_the_armory_no_longer_defines_is_exit_one(self):
        self.spec.write_text(json.dumps(dict(SPEC, items=[{"id": "gone", "reason": "r"}])), encoding="utf-8")
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("MISSING", out)

    def test_a_piece_with_no_definition_is_refused(self):
        (self.armory / "ModuleData" / "LOTRLOME_crafting_pieces.xml").write_text(
            PIECES.replace('<CraftingPiece id="blade_new_a" piece_type="Blade"/>', ""), encoding="utf-8")
        before = self.xslt.read_bytes()
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertEqual(self.xslt.read_bytes(), before)
        self.assertIn("blade_new_a", out)

    def test_a_restructured_stylesheet_is_refused_and_nothing_is_written(self):
        self.xslt.write_bytes(xslt().replace("TwoHandedPolearm_Bracing", "Renamed").encode("utf-8"))
        before = self.xslt.read_bytes()
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertEqual(self.xslt.read_bytes(), before)
        self.assertIn("TwoHandedPolearm_Bracing", out)


BEGIN, END = "<!-- TAOM-COUCH-BRACE:BEGIN -->", "<!-- TAOM-COUCH-BRACE:END -->"
COUCH_OPEN = "<xsl:template match=\"WeaponDescription[@id='TwoHandedPolearm_Couchable']/AvailablePieces\">"


class DamagedFileTests(Base):
    """A damaged marker region is refused, as _marker_region.replace_region refuses it (README, XML I/O)."""

    def damage(self, how):
        self.run_tool("--apply", "--backup-tag", "pre")
        src = self.xslt.read_bytes().decode("utf-8")
        block = src[src.index("\t\t\t" + BEGIN): src.index(END) + len(END) + 2]
        if how == "lone-begin":
            src = src.replace(block, "").replace(COUCH_OPEN, COUCH_OPEN + "\r\n\t\t\t" + BEGIN, 1)
        elif how == "lone-end":
            src = src.replace(block, "").replace(COUCH_OPEN, COUCH_OPEN + "\r\n\t\t\t" + END, 1)
        else:
            src = src.replace(block, block + block, 1)
        self.xslt.write_bytes(src.encode("utf-8"))
        for stale in self.xslt.parent.glob("*.bak-*"):
            stale.unlink()
        return self.xslt.read_bytes()

    def test_a_lone_marker_or_two_blocks_is_refused_by_apply_and_by_check(self):
        for how in ("lone-begin", "lone-end", "two-blocks"):
            with self.subTest(how=how):
                self.setUp()
                before = self.damage(how)
                for args in (("--apply", "--backup-tag", "t2"), ("--check",)):
                    code, out = self.run_tool(*args)
                    self.assertEqual(code, 1, (how, args, out))
                    self.assertIn("TAOM-COUCH-BRACE", out)
                    self.assertEqual(self.xslt.read_bytes(), before)
                    self.assertEqual(list(self.xslt.parent.glob("*.bak-*")), [])
                self.tearDown()


class CommentedItemTests(Base):
    def test_a_spec_item_that_exists_only_in_a_comment_is_missing(self):
        items = self.armory / "ModuleData" / "LOTRLOME_items" / "weapons.xml"
        text = ITEMS.replace('<CraftedItem id="spear_b"', '<!-- <CraftedItem id="spear_b"', 1).replace(
            '</CraftedItem>\n  <CraftedItem id="spear_third_party"',
            '</CraftedItem> -->\n  <CraftedItem id="spear_third_party"', 1)
        items.write_text(text, encoding="utf-8")
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("MISSING spear_b", out)

    def test_a_commented_old_definition_that_sorts_first_does_not_win(self):
        old = self.armory / "ModuleData" / "LOTRLOME_items" / "a_old.xml"
        old.write_text('<Items><!-- <CraftedItem id="spear_a" crafting_template="TwoHandedPolearm"><Pieces>'
                       '<Piece id="stale_blade" Type="Blade"/></Pieces></CraftedItem> --></Items>', encoding="utf-8")
        code, out = self.run_tool()
        self.assertEqual(code, 0, out)
        self.assertNotIn("stale_blade", out)
        self.assertIn("blade_new_a", out)


class SpecShapeTests(Base):
    def run_spec(self, spec, *args):
        self.spec.write_text(json.dumps(spec), encoding="utf-8")
        return self.run_tool(*args)

    def test_a_malformed_spec_is_exit_one_with_no_traceback(self):
        bad = [[], {"descriptions": "TwoHandedPolearm_Couchable", "items": SPEC["items"]},
               {"descriptions": [""], "items": SPEC["items"]},
               {"descriptions": SPEC["descriptions"], "items": ["spear_a"]},
               {"descriptions": SPEC["descriptions"], "items": [{"id": 5, "reason": "r"}]},
               {"descriptions": SPEC["descriptions"], "items": [{"id": "spear_a", "reason": "  "}]},
               {"descriptions": SPEC["descriptions"], "items": [{"id": "spear_a"}]}]
        for spec in bad:
            with self.subTest(spec=spec):
                code, out = self.run_spec(spec, "--check")
                self.assertEqual(code, 1)
                self.assertIn("unusable spec", out)

    def test_a_description_the_stylesheet_does_not_override_blames_the_spec_first(self):
        code, out = self.run_spec(dict(SPEC, descriptions=["TwoHandedPolearm_Couchabel"]), "--check")
        self.assertEqual(code, 1)
        self.assertIn("a typo?", out)

    def test_an_item_with_no_piece_rows_is_refused(self):
        items = self.armory / "ModuleData" / "LOTRLOME_items" / "weapons.xml"
        items.write_text(ITEMS.replace(
            '<Pieces><Piece id="blade_new_a" Type="Blade"/><Piece id="handle_ok" Type="Handle"/></Pieces>\n'
            '  </CraftedItem>\n  <CraftedItem id="spear_b"',
            '<Pieces></Pieces>\n  </CraftedItem>\n  <CraftedItem id="spear_b"', 1), encoding="utf-8")
        code, out = self.run_tool("--check")
        self.assertEqual(code, 1)
        self.assertIn("no <Piece>", out)

    def test_a_crash_exits_three_not_one(self):
        from unittest import mock
        with mock.patch.object(reg, "scan_items", side_effect=RuntimeError("boom")), \
                contextlib.redirect_stderr(io.StringIO()) as err:
            code, _ = self.run_tool("--check")
        self.assertEqual(code, 3)
        self.assertIn("boom", err.getvalue())


class ParseAndBytesTests(Base):
    def test_check_refuses_a_registered_stylesheet_that_does_not_parse(self):
        self.run_tool("--apply", "--backup-tag", "t1")
        self.xslt.write_bytes(self.xslt.read_bytes().replace(b"</xsl:stylesheet>", b""))
        self.assertEqual(self.run_tool("--check")[0], 1)

    def test_an_edit_that_would_not_parse_writes_nothing_and_makes_no_backup(self):
        broken = xslt().replace("</xsl:stylesheet>", "").encode("utf-8")
        self.xslt.write_bytes(broken)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertEqual(self.xslt.read_bytes(), broken)
        self.assertEqual(list(self.xslt.parent.glob("*.bak-*")), [])

    def test_an_invalid_byte_is_refused_not_rewritten(self):
        raw = self.xslt.read_bytes() + b"<!-- \xff -->"
        self.xslt.write_bytes(raw)
        code, out = self.run_tool("--apply", "--backup-tag", "t1")
        self.assertEqual(code, 1)
        self.assertEqual(self.xslt.read_bytes(), raw)

    def test_a_mostly_crlf_file_with_a_few_bare_lf_lines_gets_a_crlf_block(self):
        text = xslt().replace("\r\n", "\n", 2)          # two bare LF lines among many CRLF ones
        self.xslt.write_bytes(text.encode("utf-8"))
        self.run_tool("--apply", "--backup-tag", "t1")
        after = self.xslt.read_bytes().decode("utf-8")
        block = after[after.index(BEGIN): after.index(END) + len(END)]
        self.assertIn("\r\n", block)

    def test_a_four_space_indent_is_kept_in_the_block(self):
        self.xslt.write_bytes(xslt().replace("\t", "    ").encode("utf-8"))
        self.run_tool("--apply", "--backup-tag", "t1")
        after = self.xslt.read_bytes().decode("utf-8")
        self.assertIn("\r\n            " + BEGIN, after)

    def test_a_backup_tag_that_would_end_in_xslt_is_refused(self):
        with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
            self.run_tool("--apply", "--backup-tag", "a.xslt")

    def test_a_second_write_with_the_same_tag_keeps_the_pristine_backup(self):
        original = self.xslt.read_bytes()
        self.run_tool("--apply", "--backup-tag", "t1")
        self.run_tool("--revert", "--apply", "--backup-tag", "t1")
        self.assertEqual(Path(str(self.xslt) + ".bak-t1").read_bytes(), original)

    def test_a_second_dry_run_prints_no_register_line(self):
        self.run_tool("--apply", "--backup-tag", "t1")
        code, out = self.run_tool()
        self.assertEqual(code, 0)
        self.assertNotIn("register ", out)
        self.assertIn("Already registered", out)


class RevertTests(Base):
    def test_revert_restores_the_original_bytes(self):
        original = self.xslt.read_bytes()
        self.run_tool("--apply", "--backup-tag", "t1")
        applied = self.xslt.read_bytes()
        code, out = self.run_tool("--revert")
        self.assertEqual(code, 0)
        self.assertIn("DRY RUN", out)
        self.assertEqual(self.xslt.read_bytes(), applied, "a dry run revert writes nothing")
        self.assertEqual(self.run_tool("--revert", "--apply", "--backup-tag", "t2")[0], 0)
        self.assertEqual(self.xslt.read_bytes(), original)

    def test_revert_with_nothing_to_revert_is_exit_one(self):
        self.assertEqual(self.run_tool("--revert")[0], 1)

    def test_revert_cannot_be_combined_with_check(self):
        with self.assertRaises(SystemExit), contextlib.redirect_stderr(io.StringIO()):
            self.run_tool("--revert", "--check")


class SpecTests(unittest.TestCase):
    def test_the_shipped_spec_names_the_two_khamul_spears_and_both_descriptions(self):
        spec = reg.load_spec(reg.DEFAULT_SPEC)
        self.assertEqual([i["id"] for i in spec["items"]], ["sm_dg_khml_spear_a", "sm_dg_khml_spear_b"])
        self.assertEqual(spec["descriptions"], ["TwoHandedPolearm_Couchable", "TwoHandedPolearm_Bracing"])


if __name__ == "__main__":
    unittest.main()
