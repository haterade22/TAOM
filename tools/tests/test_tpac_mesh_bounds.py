#!/usr/bin/env python3
"""Tests for the pure parsing parts of tools/oneoff/tpac_mesh_bounds.py.

Run:  python -m unittest tools.tests.test_tpac_mesh_bounds

The byte strings are built by hand from the layout the tool documents (body metadata ends in
min, max, centre as four floats each plus a radius; metamesh LOD records are 16-byte material
guid, int32 name length, name, then a fixed tail holding the min and max vec4 at +112 and +128).
"""
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "oneoff"))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import tpac_mesh_bounds as tmb  # noqa: E402


def vec4(x, y, z):
    return struct.pack("<4f", x, y, z, 1.0)


class BodyBoundsTests(unittest.TestCase):
    def test_reads_min_max_centre_radius_from_the_tail(self):
        meta = b"\xAA" * 69 + vec4(-1, -2, -3) + vec4(1, 2, 3) + vec4(0, 0, 0) + struct.pack("<f", 3.5)
        b = tmb.parse_body_bounds(meta)
        self.assertEqual(b.min, (-1.0, -2.0, -3.0))
        self.assertEqual(b.max, (1.0, 2.0, 3.0))
        self.assertEqual(b.size, (2.0, 4.0, 6.0))
        self.assertEqual(b.radius, 3.5)

    def test_refuses_a_tail_whose_w_is_not_one(self):
        bad = struct.pack("<4f", 0, 0, 0, 2.0)
        meta = b"\x00" * 10 + bad + vec4(1, 1, 1) + vec4(0, 0, 0) + struct.pack("<f", 1)
        with self.assertRaises(ValueError):
            tmb.parse_body_bounds(meta)


def lod_record(name, lo, hi):
    n = name.encode("ascii")
    tail = bytearray(260)
    tail[112:128] = vec4(*lo)
    tail[128:144] = vec4(*hi)
    return b"\x11" * 16 + struct.pack("<i", len(n)) + n + bytes(tail)


class MetameshBoundsTests(unittest.TestCase):
    def build(self, records):
        head = bytearray(85)
        struct.pack_into("<i", head, 52, len(records))
        return bytes(head) + b"".join(records)

    def test_reads_every_lod_record(self):
        meta = self.build([
            lod_record("door.0", (-2, -0.25, 0.5), (2, 0.25, 6.5)),
            lod_record("door.1", (-2.1, -0.3, 0.4), (2.1, 0.3, 6.6)),
        ])
        lods = tmb.parse_metamesh_bounds(meta)
        self.assertEqual([l.name for l in lods], ["door.0", "door.1"])
        self.assertEqual(lods[0].bounds.size, (4.0, 0.5, 6.0))
        self.assertAlmostEqual(lods[1].bounds.max[2], 6.6, places=5)

    def test_refuses_a_record_that_does_not_end_in_w_one(self):
        rec = bytearray(lod_record("door.0", (0, 0, 0), (1, 1, 1)))
        struct.pack_into("<f", rec, 16 + 4 + 6 + 112 + 12, 5.0)
        with self.assertRaises(ValueError):
            tmb.parse_metamesh_bounds(self.build([bytes(rec)]))


if __name__ == "__main__":
    unittest.main()
