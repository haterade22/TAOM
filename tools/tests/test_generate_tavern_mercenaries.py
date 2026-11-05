#!/usr/bin/env python
"""tools/oneoff/generate_tavern_mercenaries.py: dry run by default, writes only changed files.

Run:  python -m unittest discover -s tools/tests -p "test_generate_tavern_mercenaries.py"

The tool had no argument parsing, so `--help` or a typo ran both writers, and it rewrote
taom_spcultures.xml on every run even when nothing changed. Each test points the tool at a
temporary tree and a one-row PICKS table.
"""
import contextlib
import importlib.util
import io
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

TOOL = Path(__file__).resolve().parents[1] / "oneoff" / "generate_tavern_mercenaries.py"
spec = importlib.util.spec_from_file_location("generate_tavern_mercenaries", TOOL)
gtm = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gtm)

TROOPS = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    '<NPCCharacters>\r\n'
    '  <NPCCharacter id="foo_a" name="Foo A" occupation="Soldier" level="11">\r\n'
    '    <upgrade_targets>\r\n'
    '      <upgrade_target id="NPCCharacter.foo_b" />\r\n'
    '    </upgrade_targets>\r\n'
    '    <skills />\r\n'
    '  </NPCCharacter>\r\n'
    '</NPCCharacters>\r\n'
)
CULTURES = (
    '<?xml version="1.0" encoding="utf-8"?>\r\n'
    '<Cultures>\r\n'
    '  <Culture id="cul">\r\n'
    '    <basic_mercenary_troops>\r\n'
    '      <template name="NPCCharacter.vanilla_merc" />\r\n'
    '    </basic_mercenary_troops>\r\n'
    '  </Culture>\r\n'
    '</Cultures>\r\n'
)


def _snapshot(root: Path) -> dict:
    return {str(p.relative_to(root)): p.read_bytes() for p in sorted(root.rglob("*")) if p.is_file()}


class _Tree(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.root = Path(self._tmp.name)
        (self.root / "troops").mkdir()
        self.troops = self.root / "troops" / "troops_foo.xml"
        self.cultures = self.root / "taom_spcultures.xml"
        self.troops.write_bytes(TROOPS.encode("utf-8"))
        self.cultures.write_bytes(CULTURES.encode("utf-8"))
        for patch in (
            mock.patch.object(gtm, "TROOPS_DIR", self.root / "troops"),
            mock.patch.object(gtm, "SPCULTURES", self.cultures),
            mock.patch.object(gtm, "PICKS", {"cul": [("foo_a", "foo", "[Foo] Hired A")]}),
        ):
            patch.start()
            self.addCleanup(patch.stop)

    def run_main(self, argv):
        out = io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(io.StringIO()):
            gtm.main(argv)
        return out.getvalue()


class DryRunByDefault(_Tree):
    def test_main_without_arguments_writes_nothing(self):
        before = _snapshot(self.root)
        out = self.run_main([])
        self.assertEqual(before, _snapshot(self.root))
        self.assertIn("dry run", out)

    def test_help_exits_and_writes_nothing(self):
        before = _snapshot(self.root)
        with self.assertRaises(SystemExit) as ctx:
            self.run_main(["--help"])
        self.assertEqual(0, ctx.exception.code)
        self.assertEqual(before, _snapshot(self.root))

    def test_unknown_flag_is_rejected_and_writes_nothing(self):
        before = _snapshot(self.root)
        with self.assertRaises(SystemExit) as ctx:
            self.run_main(["--aply"])
        self.assertEqual(2, ctx.exception.code)
        self.assertEqual(before, _snapshot(self.root))


class Apply(_Tree):
    def test_apply_adds_one_copy_and_repoints_the_culture(self):
        self.run_main(["--apply"])
        troops = self.troops.read_text(encoding="utf-8", newline="")
        self.assertEqual(1, troops.count('id="foo_a_merc"'))
        self.assertIn('occupation="Mercenary"', troops)
        self.assertNotIn("\n    <upgrade_targets>", troops.split("foo_a_merc")[1])
        cultures = self.cultures.read_text(encoding="utf-8", newline="")
        self.assertIn("NPCCharacter.foo_a_merc", cultures)
        self.assertNotIn("vanilla_merc", cultures)
        # CRLF survives: no bare LF was introduced.
        self.assertEqual(troops.count("\n"), troops.count("\r\n"))
        self.assertEqual(cultures.count("\n"), cultures.count("\r\n"))

    def test_second_apply_is_a_byte_identical_no_op(self):
        self.run_main(["--apply"])
        after_first = _snapshot(self.root)
        mtimes = {p: p.stat().st_mtime_ns for p in (self.troops, self.cultures)}
        out = self.run_main(["--apply"])
        self.assertEqual(after_first, _snapshot(self.root))
        self.assertEqual(mtimes, {p: p.stat().st_mtime_ns for p in mtimes})
        self.assertIn("0 files changed", out)

    def test_unchanged_spcultures_is_not_rewritten(self):
        self.run_main(["--apply"])
        with mock.patch.object(gtm, "write", side_effect=AssertionError("rewrote an unchanged file")):
            self.run_main(["--apply"])


class FailsBeforeWriting(_Tree):
    def test_missing_source_troop_exits_before_any_write(self):
        picks = {"cul": [("foo_a", "foo", "[Foo] Hired A"), ("ghost", "foo", "[Foo] Hired Ghost")]}
        before = _snapshot(self.root)
        with mock.patch.object(gtm, "PICKS", picks):
            with self.assertRaises(SystemExit):
                self.run_main(["--apply"])
        self.assertEqual(before, _snapshot(self.root))

    def test_output_that_does_not_parse_is_never_written(self):
        before = _snapshot(self.root)
        with mock.patch.object(gtm, "BANNER", "<unclosed"):
            with self.assertRaises(SystemExit):
                self.run_main(["--apply"])
        self.assertEqual(before, _snapshot(self.root))


class RepointOffsets(unittest.TestCase):
    """A replacement that grows one culture's block must not shift the next culture's offsets."""

    def test_a_grown_first_block_still_repoints_the_second_culture(self):
        xml = (
            '<Cultures>\r\n'
            '  <Culture id="one">\r\n'
            '    <basic_mercenary_troops>\r\n'
            '      <template name="NPCCharacter.vanilla_a" />\r\n'
            '    </basic_mercenary_troops>\r\n'
            '  </Culture>\r\n'
            '  <Culture id="two">\r\n'
            '    <basic_mercenary_troops>\r\n'
            '      <template name="NPCCharacter.vanilla_b" />\r\n'
            '    </basic_mercenary_troops>\r\n'
            '  </Culture>\r\n'
            '</Cultures>\r\n'
        )
        picks = {
            "one": [("one_a", "troops_one", "A"), ("one_b", "troops_one", "B"), ("one_c", "troops_one", "C")],
            "two": [("two_a", "troops_two", "A")],
        }
        with mock.patch.object(gtm, "PICKS", picks):
            out, changed = gtm.plan_repoint(xml)
        self.assertEqual(2, changed)
        self.assertIn('NPCCharacter.two_a_merc', out)
        self.assertNotIn('vanilla_b', out)
        self.assertEqual(3, out.count('NPCCharacter.one_'))


if __name__ == "__main__":
    unittest.main()
