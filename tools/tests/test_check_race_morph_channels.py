#!/usr/bin/env python3
"""Unit tests for tools/check_race_morph_channels.py (a fake FBX reader, no install needed).

Run:  python -m pytest tools/tests/test_check_race_morph_channels.py -q
Pins: every spec mesh with its exact channel count passes; a missing mesh or a wrong count fails and is named;
an absent install or FBX prints SKIPPED and exits 2 saying the gate did not run; an unreadable or truncated FBX
fails by name; the gate never writes.
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest
from types import SimpleNamespace

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import check_race_morph_channels as c  # noqa: E402

TROLL = "AssetSources/Race Test/Mordor/Trolls/hill_troll_a/hill_troll_a.fbx"
DWARF = "AssetSources/Race Test/dwarf/sk_dwarf_bm_f1.fbx"


def _mesh(name, n):
    return SimpleNamespace(name=name, channels=["shape_%02d" % i for i in range(1, n + 1)])


def _good():
    """What the live FBX carry when they are right: the LOD0 meshes with channels, their LODs and the rest without."""
    return {
        TROLL: [_mesh("hill_troll_a_body", 0), _mesh("hill_troll_a_hands", 26), _mesh("hill_troll_a_hands.lod1", 0),
                _mesh("hill_troll_a_head", 101), _mesh("hill_troll_a_head.eye", 101),
                _mesh("hill_troll_a_head.eye.lod1", 0), _mesh("hill_troll_a_head.mouth", 101)],
        DWARF: [_mesh("sk_dwarf_bm_f1_arms", 26), _mesh("sk_dwarf_bm_f1_head", 101),
                _mesh("sk_dwarf_bm_f1_head.eye", 101), _mesh("sk_dwarf_bm_f1_head.mouth", 101),
                _mesh("sk_dwarf_bm_f1_head_lod1", 0)],
    }


class _Armory(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.armory = self._tmp.name
        self.meshes = _good()
        for rel in (TROLL, DWARF):
            path = os.path.join(self.armory, *rel.split("/"))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as fh:
                fh.write(b"fbx")

    def tearDown(self):
        self._tmp.cleanup()

    def reader(self, path):
        rel = os.path.relpath(path, self.armory).replace(os.sep, "/")
        return self.meshes[rel]

    def run_main(self, armory=None):
        with contextlib.redirect_stdout(io.StringIO()) as out:
            rc = c.main(["--armory", armory or self.armory], reader=self.reader)
        return rc, out.getvalue()


class SpecTests(unittest.TestCase):
    def test_the_spec_names_each_race_heads_three_parts_and_its_hands(self):
        spec = {(rel, mesh): n for rel, mesh, n in c.SPEC}
        self.assertEqual(spec, {
            (TROLL, "hill_troll_a_head"): 101, (TROLL, "hill_troll_a_head.eye"): 101,
            (TROLL, "hill_troll_a_head.mouth"): 101, (TROLL, "hill_troll_a_hands"): 26,
            (DWARF, "sk_dwarf_bm_f1_head"): 101, (DWARF, "sk_dwarf_bm_f1_head.eye"): 101,
            (DWARF, "sk_dwarf_bm_f1_head.mouth"): 101, (DWARF, "sk_dwarf_bm_f1_arms"): 26})


class GateTests(_Armory):
    def test_all_channels_present_passes(self):
        rc, out = self.run_main()
        self.assertEqual(rc, 0, out)
        self.assertIn("OK", out)
        self.assertNotIn("SKIPPED", out)

    def test_a_missing_mesh_fails_and_is_named(self):
        self.meshes[TROLL] = [m for m in self.meshes[TROLL] if m.name != "hill_troll_a_head.eye"]
        rc, out = self.run_main()
        self.assertEqual(rc, 1, out)
        self.assertIn("hill_troll_a_head.eye", out)
        self.assertIn("MISMATCH", out)

    def test_a_wrong_count_fails_with_both_numbers(self):
        # an export_rig_for_kit.py re-export or a restored backup leaves the hands with none
        self.meshes[TROLL] = [_mesh("hill_troll_a_hands", 0) if m.name == "hill_troll_a_hands" else m
                              for m in self.meshes[TROLL]]
        self.meshes[DWARF] = [_mesh("sk_dwarf_bm_f1_head.mouth", 100) if m.name == "sk_dwarf_bm_f1_head.mouth" else m
                              for m in self.meshes[DWARF]]
        rc, out = self.run_main()
        self.assertEqual(rc, 1, out)
        mismatches = [ln for ln in out.splitlines() if ln.startswith("MISMATCH")]
        self.assertEqual(len(mismatches), 2, out)
        self.assertIn("hill_troll_a_hands has 0 morph channels, needs 26", out)
        self.assertIn("sk_dwarf_bm_f1_head.mouth has 100 morph channels, needs 101", out)

    def test_an_absent_fbx_is_skipped_not_passed(self):
        # exit 2, like the repo's other install gates: a check that ran nothing must not read as a pass
        os.remove(os.path.join(self.armory, *DWARF.split("/")))
        rc, out = self.run_main()
        self.assertEqual(rc, 2, out)
        self.assertIn("SKIPPED", out)
        self.assertIn("sk_dwarf_bm_f1.fbx", out)
        self.assertIn("did not run", out)
        self.assertFalse(any(ln.startswith("OK") for ln in out.splitlines()), out)

    def test_an_absent_fbx_does_not_hide_a_mismatch_in_the_other(self):
        os.remove(os.path.join(self.armory, *DWARF.split("/")))
        self.meshes[TROLL] = [m for m in self.meshes[TROLL] if m.name != "hill_troll_a_hands"]
        rc, out = self.run_main()
        self.assertEqual(rc, 1, out)
        self.assertIn("SKIPPED", out)
        self.assertIn("hill_troll_a_hands", out)

    def test_an_absent_install_is_skipped(self):
        rc, out = self.run_main(os.path.join(self.armory, "no_such_armory"))
        self.assertEqual(rc, 2, out)
        self.assertIn("SKIPPED", out)
        self.assertIn("did not run", out)

    def test_a_truncated_fbx_is_named_and_the_other_still_checked(self):
        # the FBX reader raises struct.error on a cut file and zlib.error on a damaged array: neither is a ValueError
        import struct
        good = self.meshes

        def reader(path):
            if path.endswith("hill_troll_a.fbx"):
                raise struct.error("unpack requires a buffer of 4 bytes")
            return good[DWARF]
        with contextlib.redirect_stdout(io.StringIO()) as out:
            rc = c.main(["--armory", self.armory], reader=reader)
        self.assertEqual(rc, 1, out.getvalue())
        self.assertIn("hill_troll_a.fbx cannot be read (error)", out.getvalue())
        self.assertNotIn("sk_dwarf_bm_f1", out.getvalue())   # the dwarf was checked and matched

    def test_an_unreadable_fbx_fails(self):
        def broken(path):
            raise ValueError("not a binary FBX (ASCII FBX is not supported)")
        with contextlib.redirect_stdout(io.StringIO()) as out:
            rc = c.main(["--armory", self.armory], reader=broken)
        self.assertEqual(rc, 1, out.getvalue())
        self.assertIn("not a binary FBX", out.getvalue())

    def test_the_gate_never_writes(self):
        before = {rel: open(os.path.join(self.armory, *rel.split("/")), "rb").read() for rel in (TROLL, DWARF)}
        self.run_main()
        after = {rel: open(os.path.join(self.armory, *rel.split("/")), "rb").read() for rel in (TROLL, DWARF)}
        self.assertEqual(before, after)

    def test_the_default_reader_is_the_fbx_audits(self):
        import audit_fbx_lods
        self.assertIs(c.read_fbx_meshes, audit_fbx_lods.read_fbx_meshes)


if __name__ == "__main__":
    unittest.main()
