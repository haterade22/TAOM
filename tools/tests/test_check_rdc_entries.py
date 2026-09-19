#!/usr/bin/env python3
"""Unit tests for tools/check_rdc_entries.py.

Run:  python -m unittest tools.tests.test_check_rdc_entries
  or:  python tools/tests/test_check_rdc_entries.py

Pure stdlib over synthetic tpac files in a temp module; no game install needed. Pins:
  - a package with a .rdc entry is not reported
  - a mesh package (Metamesh item) without an entry is reported and fails the exit code
  - an animation master (SkeletalAnimation item, no Metamesh) without an entry is NOT a failure:
    the Kit never writes one for a master (measured 2026-09-18: warg 56/56, elephant 31/31, chariot 3/3,
    spider 24/26 masters have none, and all of them animate in game); it is counted separately
  - a package holding both a SkeletalAnimation and a Metamesh item is still treated as a mesh
"""
import io
import os
import sys
import tempfile
import unittest
import uuid
from contextlib import redirect_stdout

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import check_rdc_entries as rdc  # noqa: E402

SKELANIM = bytes.fromhex("07b0faba3f7e3f45bac6e7640043112b")
METAMESH = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")
GEOMETRY = bytes.fromhex("7936ba3ebdde7a4c8634f121f6325e33")


def _tpac(guid, *item_types):
    body = b"".join(t + uuid.uuid4().bytes_le + b"\x00" * 8 for t in item_types)
    return b"TPAC" + b"\x02\x00\x00\x00" + guid.bytes_le + len(item_types).to_bytes(4, "little") + b"\x00" * 8 + body


class CheckRdcEntriesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.module = self.tmp.name
        os.makedirs(os.path.join(self.module, "RuntimeDataCache"))
        os.makedirs(os.path.join(self.module, "Assets", "creature", "x"))

    def tearDown(self):
        self.tmp.cleanup()

    def _write(self, name, *item_types, with_entry=False):
        g = uuid.uuid4()
        with open(os.path.join(self.module, "Assets", "creature", "x", name), "wb") as fh:
            fh.write(_tpac(g, *item_types))
        if with_entry:
            open(os.path.join(self.module, "RuntimeDataCache", str(g).upper() + ".rdc"), "wb").close()

    def _run(self, *extra):
        buf = io.StringIO()
        with redirect_stdout(buf):
            code = rdc.main(["--module", self.module, *extra])
        return code, buf.getvalue()

    def test_package_with_entry_passes(self):
        self._write("mesh_geo.tpac", METAMESH, with_entry=True)
        code, out = self._run()
        self.assertEqual(code, 0)
        self.assertNotIn("NO RDC", out)

    def test_mesh_without_entry_fails(self):
        self._write("mesh_geo.tpac", METAMESH)
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("NO RDC", out)
        self.assertIn("without-rdc=1", out)

    def test_animation_master_without_entry_is_not_a_failure(self):
        self._write("walk_geo.tpac", GEOMETRY, SKELANIM)
        code, out = self._run()
        self.assertEqual(code, 0)
        self.assertNotIn("NO RDC", out)
        self.assertIn("without-rdc=0", out)
        self.assertIn("animation-masters-without-entry=1", out)

    def test_package_with_animation_and_mesh_is_a_mesh(self):
        self._write("skinned_geo.tpac", SKELANIM, METAMESH)
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("NO RDC", out)

    def test_show_masters_lists_them(self):
        self._write("walk_geo.tpac", GEOMETRY, SKELANIM)
        code, out = self._run("--show-masters")
        self.assertEqual(code, 0)
        self.assertIn("ANIM MASTER", out)

    def test_missing_under_folder_is_a_failure_not_a_clean_pass(self):
        code, out = self._run("--under", "creature/nope")
        self.assertEqual(code, 1)
        self.assertIn("no folder", out)

    def test_a_folder_with_nothing_to_check_is_a_failure(self):
        self._write("skin_mtl.tpac", METAMESH)
        code, out = self._run()
        self.assertEqual(code, 1)
        self.assertIn("nothing checked", out)


if __name__ == "__main__":
    unittest.main()
