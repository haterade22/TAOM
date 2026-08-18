#!/usr/bin/env python3
"""Unit tests for tools/check_external_xslt.py (issue #462).

The gap this tool closes: CI's "Validate XML & XSLT" job and `/xslt-check` both
glob `Main/_Module/ModuleData`, so the 8 stylesheets in the live `TAOM_Map` and
`LOTRLOME_Armory` installs had no gate of any kind. CI structurally cannot cover
them, because those modules are not in the checkout.

Every test builds a SYNTHETIC module tree in a tempdir, so the suite never depends
on a game install and a broken stylesheet can actually be exercised. Each guard is
tested in BOTH directions, clean and broken, because a check that has only ever
been seen passing is not known to be able to fail.
"""
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import check_external_xslt as cx  # noqa: E402

GOOD = ('<?xml version="1.0" encoding="utf-8"?>\n'
        '<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">\n'
        '  <xsl:template match="@*|node()">\n'
        '    <xsl:copy><xsl:apply-templates select="@*|node()"/></xsl:copy>\n'
        '  </xsl:template>\n'
        '</xsl:stylesheet>\n')


def _write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


class DiscoveryTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.repo_md = self.root / "repo" / "ModuleData"
        self.modules = self.root / "Modules"
        _write(self.repo_md / "spcultures.xslt", GOOD)
        _write(self.modules / "TAOM_Map" / "ModuleData" / "settlements.xslt", GOOD)
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "action_sets.xslt", GOOD)
        # Nested, because two of the Armory's real stylesheets are in subdirectories.
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "Animations" / "action_sets.xslt", GOOD)

    def tearDown(self):
        self._tmp.cleanup()

    def test_finds_all_three_modules(self):
        found = cx.find_stylesheets(self.repo_md, self.modules)
        self.assertEqual(len(found["TAOM (repo)"]), 1)
        self.assertEqual(len(found["TAOM_Map"]), 1)
        self.assertEqual(len(found["LOTRLOME_Armory"]), 2, "must recurse into subdirectories")

    def test_absent_external_module_is_empty_not_missing(self):
        """An absent module must report an empty list, never vanish from the report:
        a silently dropped module reads exactly like a module with nothing to check."""
        found = cx.find_stylesheets(self.repo_md, self.root / "no_such_dir")
        self.assertIn("TAOM_Map", found)
        self.assertEqual(found["TAOM_Map"], [])

    def test_repo_is_scanned_even_with_no_game_install(self):
        found = cx.find_stylesheets(self.repo_md, None)
        self.assertEqual(len(found["TAOM (repo)"]), 1)


class StylesheetCheckTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)

    def tearDown(self):
        self._tmp.cleanup()

    def test_well_formed_stylesheet_is_clean(self):
        p = self.root / "ok.xslt"
        _write(p, GOOD)
        self.assertEqual(cx.check_stylesheet(p), [])

    def test_malformed_xml_is_reported(self):
        p = self.root / "bad.xslt"
        _write(p, '<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform">')
        problems = cx.check_stylesheet(p)
        self.assertTrue(problems)
        self.assertIn("not well-formed", problems[0])

    def test_wrong_root_element_is_reported(self):
        """A stylesheet the engine will silently ignore is worse than a broken one,
        because the merge just does nothing and nothing says so."""
        p = self.root / "wrong_root.xslt"
        _write(p, '<?xml version="1.0"?><Settlements><Settlement id="x" /></Settlements>')
        problems = cx.check_stylesheet(p)
        self.assertTrue(problems)
        self.assertIn("expected xsl:stylesheet", problems[0])

    def test_xsl_transform_root_is_accepted(self):
        p = self.root / "transform.xslt"
        _write(p, '<?xml version="1.0"?>'
                  '<xsl:transform version="1.0" '
                  'xmlns:xsl="http://www.w3.org/1999/XSL/Transform"/>')
        self.assertEqual(cx.check_stylesheet(p), [])

    def test_unreadable_file_is_reported_not_raised(self):
        problems = cx.check_stylesheet(self.root / "does_not_exist.xslt")
        self.assertTrue(problems)
        self.assertIn("unreadable", problems[0])


class RunReportTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.repo_md = self.root / "repo" / "ModuleData"
        self.modules = self.root / "Modules"

    def tearDown(self):
        self._tmp.cleanup()

    def test_clean_tree_reports_no_problems(self):
        _write(self.repo_md / "a.xslt", GOOD)
        _write(self.modules / "TAOM_Map" / "ModuleData" / "settlements.xslt", GOOD)
        report = cx.run(self.repo_md, self.modules)
        self.assertEqual(report["problems"], [])

    def test_broken_external_stylesheet_is_caught(self):
        """The whole point: a defect in a module CI cannot see must still be found."""
        _write(self.repo_md / "a.xslt", GOOD)
        _write(self.modules / "LOTRLOME_Armory" / "ModuleData" / "broken.xslt", "<xsl:stylesheet")
        report = cx.run(self.repo_md, self.modules)
        # Both layers fire on a malformed file (well-formedness, then the lxml
        # compile when lxml is installed), so assert on the file, not the count.
        self.assertTrue(report["problems"])
        self.assertTrue(all("broken.xslt" in p["file"] for p in report["problems"]),
                        f"only the broken file should be reported, got {report['problems']}")


if __name__ == "__main__":
    unittest.main()
