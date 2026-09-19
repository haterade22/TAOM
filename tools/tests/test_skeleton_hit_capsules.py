#!/usr/bin/env python3
"""Unit tests for tools/skeleton_hit_capsules.py.

Run:  python -m unittest tools.tests.test_skeleton_hit_capsules
  or:  python tools/tests/test_skeleton_hit_capsules.py

Synthetic tpac packages built in a temp folder; no game install, Kit or Blender needed. Pins:
  - skeleton bones and hit-capsule bodies read out of an LZ4 SkeletonUserData segment
  - a patch rewrites only the named capsules (both ends, radius, max radius), keeps each vec4's padding
    float, leaves every other segment byte-identical, and stores the new compressed size plus the
    xxHash64 of the new uncompressed data (the segment hash field, measured 2026-09-18 on the elephant)
  - a size change moves every later segment's offset by the same amount
  - an unknown bone name is refused; the CLI dry run writes nothing; --apply writes a backup first
  - the fit sizes a capsule to a synthetic cylinder of skin, a little bigger than the skin
  - the Blender-to-engine axis map is found from bone positions (the elephant: engine = (-x, -y, z))
Skips, never errors, where lz4, numpy or xxhash is missing (CI's python-tests job installs nothing);
the fit tests also need scipy.
"""
import importlib.util
import io
import json
import math
import os
import struct
import sys
import tempfile
import unittest
import uuid
from contextlib import redirect_stdout
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
try:
    import lz4.block
    import numpy as np
    import xxhash
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("skeleton_hit_capsules needs lz4, numpy and xxhash (%s)" % exc)
import skeleton_hit_capsules as shc  # noqa: E402
import tpac_clone_metamesh as tcm  # noqa: E402

HAVE_SCIPY = importlib.util.find_spec("scipy") is not None


def _read(path):
    with open(path, "rb") as fh:
        return fh.read()


def _sstr(s):
    b = s.encode("utf-8")
    return struct.pack("<i", len(b)) + b


def _vec4(v, w):
    return struct.pack("<4f", v[0], v[1], v[2], w)


def _identity(tx=0.0, ty=0.0, tz=0.0):
    return [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, tx, ty, tz, 1]


def _definition(name, bones):
    out = _sstr(name) + struct.pack("<i", len(bones))
    for bname, parent, mat in bones:
        out += _sstr(bname) + struct.pack("<i", parent) + struct.pack("<16f", *mat)
    return out


def _userdata(bodies, pad_w=7.5):
    out = struct.pack("<f", 0.25) + _vec4((0, 0, 0), 0) + _vec4((0, 0, 0), 0)
    out += _sstr("horse") + _sstr("") + uuid.UUID(int=0).bytes_le + struct.pack("<i", len(bodies))
    for b in bodies:
        out += _sstr(b["bone"]) + bytes([0]) + _sstr("") + _sstr(b.get("zone", "chest")) + struct.pack("<f", 3.0)
        out += _vec4(b["rp1"], pad_w) + _vec4(b["rp2"], pad_w) + struct.pack("<f", b["rr"])
        out += _vec4(b["cp1"], pad_w) + _vec4(b["cp2"], pad_w) + struct.pack("<ff", b["cr"], b["cmax"])
    out += struct.pack("<i", 0) + struct.pack("<i", 0)  # UnknownInt, then zero constraints
    return out


def _tpac(items):
    """items: [(type_uuid, name, [(seg_type_uuid, uncompressed_bytes)])]. Every segment stored LZ4HC."""
    head_len = 36
    for _, name, segs in items:
        head_len += 16 + 16 + 4 + len(_sstr(name)) + 8 + 8 + 4 + 69 * len(segs) + 4
    header, blobs, offset = b"", [], head_len
    for typ, name, segs in items:
        header += typ.bytes_le + uuid.uuid4().bytes_le + struct.pack("<I", 1) + _sstr(name)
        header += struct.pack("<Q", 0) + struct.pack("<Q", 0) + struct.pack("<I", len(segs))
        for seg_type, data in segs:
            comp = lz4.block.compress(data, mode="high_compression", store_size=False)
            header += struct.pack("<QQQ", offset, len(data), len(comp)) + uuid.uuid4().bytes_le + seg_type.bytes_le
            header += struct.pack("<QI", xxhash.xxh64_intdigest(data), 0) + bytes([1])
            blobs.append(comp)
            offset += len(comp)
        header += struct.pack("<I", 0)
    top = struct.pack("<II", 1128353876, 2) + uuid.uuid4().bytes_le + struct.pack("<I", len(items))
    top += struct.pack("<II", head_len - 36, 0)
    return top + header + b"".join(blobs)


BODIES = [
    {"bone": " Neck_06", "rp1": (0.1, 0, 0), "rp2": (0.5, 0, 0), "rr": 0.2,
     "cp1": (0.047, 0, 0), "cp2": (0.471, 0, 0), "cr": 0.047, "cmax": 0.047},
    {"bone": " Head_08", "rp1": (0.0, 0.2, 0), "rp2": (-0.1, 0.2, 0), "rr": 0.16,
     "cp1": (0.01, 0.21, 0), "cp2": (-0.13, 0.24, 0), "cr": 0.674, "cmax": 0.674},
]
BONES = [(" Neck_06", -1, _identity()), (" Head_08", 0, _identity(0.5, 0, 0))]
MESHDATA = bytes(range(256)) * 40
MESH_SEGMENT_TYPE = uuid.UUID("5f98413d-0000-0000-0000-000000000000")


def _package(userdata_first=False):
    skel_segs = [(shc.SKELETON_DEFINITION_TYPE, _definition("test_skeleton", BONES)),
                 (shc.SKELETON_USERDATA_TYPE, _userdata(BODIES))]
    mesh = (shc.METAMESH_ITEM_TYPE, "test_mesh", [(MESH_SEGMENT_TYPE, MESHDATA)])
    skel = (shc.SKELETON_ITEM_TYPE, "test_skeleton", skel_segs)
    return _tpac([skel, mesh] if userdata_first else [mesh, skel])


class ReadTests(unittest.TestCase):
    def test_reads_bones_and_bodies_from_lz4_segment(self):
        sk = shc.read_skeleton(_package())
        self.assertEqual(sk["name"], "test_skeleton")
        self.assertEqual([b["name"] for b in sk["bones"]], [" Neck_06", " Head_08"])
        self.assertEqual(sk["bones"][1]["parent"], 0)
        self.assertAlmostEqual(sk["world"][1][3][0], 0.5, places=6)
        self.assertEqual([b["bone"] for b in sk["bodies"]], [" Neck_06", " Head_08"])
        self.assertAlmostEqual(sk["bodies"][0]["cr"], 0.047, places=6)
        self.assertAlmostEqual(sk["bodies"][1]["cp2"][1], 0.24, places=6)

    def test_every_segment_hash_is_xxh64_of_its_data(self):
        raw = _package()
        self.assertEqual(len(tcm.parse(raw).items), 2)
        self.assertEqual(shc.stale_segment_hashes(raw), [])

    def test_a_stale_hash_is_reported(self):
        raw = bytearray(_package())
        pkg = tcm.parse(bytes(raw))
        mesh = pkg.items[0]
        entry = shc.tcm.HEADER_SIZE + mesh.segments[0].entry_pos + 56
        raw[entry] ^= 0xFF
        self.assertEqual(shc.stale_segment_hashes(bytes(raw)), [("test_mesh", 0)])


class PatchTests(unittest.TestCase):
    NEW = {"Neck_06": ((0.0, 0.1, 0.0), (0.4, 0.1, 0.0), 0.6)}

    def test_patch_rewrites_only_the_named_capsule(self):
        raw = _package()
        out = shc.patch_bodies(raw, self.NEW)
        neck, head = shc.read_skeleton(out)["bodies"]
        self.assertEqual(tuple(round(x, 6) for x in neck["cp1"]), (0.0, 0.1, 0.0))
        self.assertEqual(tuple(round(x, 6) for x in neck["cp2"]), (0.4, 0.1, 0.0))
        self.assertAlmostEqual(neck["cr"], 0.6, places=6)
        self.assertAlmostEqual(neck["cmax"], 0.6, places=6)
        self.assertAlmostEqual(neck["rr"], 0.2, places=6, msg="ragdoll capsule untouched")
        self.assertEqual(head, shc.read_skeleton(raw)["bodies"][1])

    def test_patch_keeps_vec4_padding(self):
        out = shc.patch_bodies(_package(), self.NEW)
        data = shc.userdata_payload(out)
        body = shc.parse_bodies(data)[0]
        self.assertEqual(struct.unpack_from("<f", data, body["off"]["cp1"] + 12)[0], 7.5)
        self.assertEqual(struct.unpack_from("<f", data, body["off"]["cp2"] + 12)[0], 7.5)

    def test_patch_updates_size_and_hash_and_keeps_other_segments(self):
        raw = _package()
        out = shc.patch_bodies(raw, self.NEW)
        self.assertEqual(shc.stale_segment_hashes(out), [])
        before, after = tcm.parse(raw), tcm.parse(out)
        for it_b, it_a in zip(before.items, after.items):
            self.assertEqual(it_b.checksum, it_a.checksum, "item metadata and its checksum untouched")
            for sb, sa in zip(it_b.segments, it_a.segments):
                if sb.tag != shc.SKELETON_USERDATA_TAG:
                    self.assertEqual(tcm.segment_payload(it_b, sb), tcm.segment_payload(it_a, sa))
        skel = after.items[1]
        seg = skel.segments[1]
        self.assertEqual(seg.tag, shc.SKELETON_USERDATA_TAG)
        self.assertEqual(seg.offset + seg.storage, len(out), "userdata was the last segment and still is")

    def test_patch_moves_later_segments_when_size_changes(self):
        raw = _package(userdata_first=True)
        big = {"Neck_06": ((0.123456, 0.654321, 0.111111), (0.987654, 0.333333, 0.777777), 0.555555),
               "Head_08": ((0.314159, 0.271828, 0.161803), (0.141421, 0.173205, 0.223606), 0.912345)}
        out = shc.patch_bodies(raw, big)
        before, after = tcm.parse(raw), tcm.parse(out)
        mesh_b, mesh_a = before.items[1].segments[0], after.items[1].segments[0]
        ud_b, ud_a = before.items[0].segments[1], after.items[0].segments[1]
        self.assertNotEqual(ud_b.storage, ud_a.storage, "the test needs a size change to mean anything")
        self.assertEqual(mesh_a.offset - mesh_b.offset, ud_a.storage - ud_b.storage)
        self.assertEqual(tcm.segment_payload(after.items[1], mesh_a), MESHDATA)
        self.assertEqual(shc.stale_segment_hashes(out), [])

    def test_duplicate_bone_names_are_refused(self):
        dup = [dict(b) for b in BODIES]
        dup[1]["bone"] = "Neck_06 "  # strips to the same name as " Neck_06"
        raw = _tpac([(shc.SKELETON_ITEM_TYPE, "test_skeleton",
                      [(shc.SKELETON_DEFINITION_TYPE, _definition("test_skeleton", BONES)),
                       (shc.SKELETON_USERDATA_TYPE, _userdata(dup))])])
        with self.assertRaises(ValueError):
            shc.patch_bodies(raw, self.NEW)

    def test_unknown_bone_is_refused(self):
        with self.assertRaises(KeyError):
            shc.patch_bodies(_package(), {"Tail_99": ((0, 0, 0), (1, 0, 0), 0.1)})


class CliTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.tpac = os.path.join(self.tmp.name, "x_geo.tpac")
        with open(self.tpac, "wb") as fh:
            fh.write(_package())
        self.fit = os.path.join(self.tmp.name, "fit.json")
        with open(self.fit, "w") as fh:
            json.dump({"bodies": [
                {"bone": " Neck_06", "action": "refit", "new": {"cp1": [0, 0.1, 0], "cp2": [0.4, 0.1, 0], "cr": 0.6}},
                {"bone": " Head_08", "action": "kept", "new": {"cp1": [9, 9, 9], "cp2": [9, 9, 9], "cr": 9}}]}, fh)

    def tearDown(self):
        self.tmp.cleanup()

    def _run(self, *argv, running=False):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(shc, "game_or_kit_running", return_value=running):
            code = shc.main(list(argv))
        return code, buf.getvalue()

    def _backups(self):
        return [f for f in os.listdir(self.tmp.name) if ".bak" in f]

    def test_dry_run_writes_nothing(self):
        before = _read(self.tpac)
        code, out = self._run("patch", "--tpac", self.tpac, "--fit", self.fit)
        self.assertEqual(code, 0)
        self.assertIn("dry run", out)
        self.assertEqual(_read(self.tpac), before)
        self.assertEqual(self._backups(), [])

    def test_apply_writes_backup_then_only_refit_bodies(self):
        before = _read(self.tpac)
        code, out = self._run("patch", "--tpac", self.tpac, "--fit", self.fit, "--apply")
        self.assertEqual(code, 0, out)
        baks = [f for f in self._backups() if ".bak-hitcapsules-" in f]
        self.assertEqual(len(baks), 1)
        self.assertEqual(_read(os.path.join(self.tmp.name, baks[0])), before)
        neck, head = shc.read_skeleton(_read(self.tpac))["bodies"]
        self.assertAlmostEqual(neck["cr"], 0.6, places=6)
        self.assertAlmostEqual(head["cr"], 0.674, places=6, msg="a 'kept' body is never written")

    def test_apply_refuses_a_stale_skeleton_hash_and_writes_nothing(self):
        raw = bytearray(_read(self.tpac))
        skel = tcm.parse(bytes(raw)).items[1]
        seg = skel.segments[1]
        raw[tcm.HEADER_SIZE + sum(len(i.toc) for i in tcm.parse(bytes(raw)).items[:1]) + seg.entry_pos + 56] ^= 0xFF
        with open(self.tpac, "wb") as fh:
            fh.write(bytes(raw))
        code, out = self._run("patch", "--tpac", self.tpac, "--fit", self.fit, "--apply")
        self.assertEqual(code, 1)
        self.assertIn("REFUSED", out)
        self.assertEqual(_read(self.tpac), bytes(raw))
        self.assertEqual(self._backups(), [])

    def test_apply_refuses_a_fit_made_for_another_skeleton(self):
        with open(self.fit) as fh:
            fit = json.load(fh)
        fit["skeleton"] = "some_other_skeleton"
        with open(self.fit, "w") as fh:
            json.dump(fit, fh)
        before = _read(self.tpac)
        code, out = self._run("patch", "--tpac", self.tpac, "--fit", self.fit, "--apply")
        self.assertEqual(code, 1)
        self.assertIn("some_other_skeleton", out)
        self.assertEqual(_read(self.tpac), before)
        self.assertEqual(self._backups(), [])

    def test_apply_refuses_while_game_or_kit_runs(self):
        code, _ = self._run("patch", "--tpac", self.tpac, "--fit", self.fit, "--apply", running=True)
        self.assertEqual(code, 2)
        self.assertEqual(self._backups(), [])


class ProcessCheckTests(unittest.TestCase):
    def _stdout(self, text):
        return mock.patch.object(shc.subprocess, "run", return_value=mock.Mock(stdout=text))

    def test_game_in_the_process_list_is_running(self):
        with self._stdout('"Bannerlord.exe","42164","Console","1","6,584,108 K"\n'):
            self.assertTrue(shc.game_or_kit_running())

    def test_other_processes_are_not(self):
        with self._stdout('"explorer.exe","1234","Console","1","90,000 K"\n'):
            self.assertFalse(shc.game_or_kit_running())

    def test_a_process_list_that_cannot_be_read_counts_as_running(self):
        buf = io.StringIO()
        with redirect_stdout(buf), mock.patch.object(shc.subprocess, "run", side_effect=OSError("no tasklist")):
            self.assertTrue(shc.game_or_kit_running())
        self.assertIn("refusing", buf.getvalue())


def _cylinder(radius=0.5, length=2.0, rings=21, around=32):
    """Closed cylinder of skin along +x: side rings plus two capped ends, outward normals."""
    pts, nrm = [], []
    for i in range(rings):
        x = length * i / (rings - 1)
        for k in range(around):
            a = 2 * math.pi * k / around
            pts.append((x, radius * math.cos(a), radius * math.sin(a)))
            nrm.append((0, math.cos(a), math.sin(a)))
    for x, nx in ((0.0, -1.0), (length, 1.0)):
        for rr in (0.15, 0.3, 0.45):
            for k in range(around):
                a = 2 * math.pi * k / around
                pts.append((x, rr * math.cos(a), rr * math.sin(a)))
                nrm.append((nx, 0, 0))
    return np.array(pts), np.array(nrm)


@unittest.skipUnless(HAVE_SCIPY, "the fit needs scipy")
class FitTests(unittest.TestCase):
    @staticmethod
    def _skeleton(cr=0.05):
        return {"name": "s", "bones": [{"name": " b", "parent": -1}], "world": [np.eye(4)],
                "bodies": [{"bone": " b", "cp1": (0.1, 0, 0), "cp2": (0.9, 0, 0), "cr": cr, "cmax": cr}]}

    def test_capsule_fits_a_cylinder_a_little_bigger_than_the_skin(self):
        v, n = _cylinder()
        res = shc.fit_capsules(self._skeleton(), v, n, np.array(["b"] * len(v)), limit=0.20, pct=95, margin=0.03)
        body = res["bodies"][0]
        self.assertEqual(body["action"], "refit")
        self.assertGreaterEqual(body["new"]["cr"], 0.5)
        self.assertLessEqual(body["new"]["cr"], 0.56)
        for p in (body["new"]["cp1"], body["new"]["cp2"]):
            self.assertLess(abs(p[1]) + abs(p[2]), 0.02, "axis centred on the cylinder")
        self.assertGreater(res["coverage"]["new"], 0.97)
        self.assertLess(res["coverage"]["old"], 0.2)

    def test_an_old_capsule_that_already_covers_more_is_kept(self):
        v, n = _cylinder()
        skel = self._skeleton()
        skel["bodies"][0].update(cp1=(0.0, 0, 0), cp2=(2.0, 0, 0), cr=0.6, cmax=0.6)
        res = shc.fit_capsules(skel, v, n, np.array(["b"] * len(v)), limit=0.20, pct=95, margin=0.03)
        self.assertEqual(res["bodies"][0]["action"], "kept")

    def test_a_mesh_name_missing_from_the_export_is_refused(self):
        skin = {"bones": {" b": [[0, 0, 0], [1, 0, 0]], " c": [[0, 1, 0], [1, 1, 0]], " d": [[0, 0, 1], [1, 0, 1]]},
                "meshes": [{"name": "body", "verts": [[0, 0, 0, 0, 0, 1, [["b", 1.0]]]]}]}
        skel = {"name": "s", "bodies": [],
                "bones": [{"name": " b"}, {"name": " c"}, {"name": " d"}],
                "world": [np.eye(4), np.array([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 1, 0, 1.0]]),
                          np.array([[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 1, 1.0]])]}
        with tempfile.TemporaryDirectory() as d:
            path = os.path.join(d, "skin.json")
            with open(path, "w") as fh:
                json.dump(skin, fh)
            with self.assertRaises(ValueError) as ctx:
                shc.load_skin(path, {"body", "armour_typo"}, skel)
        self.assertIn("armour_typo", str(ctx.exception))

    def test_bone_with_too_few_vertices_keeps_its_capsule(self):
        v, n = _cylinder(rings=2, around=4)
        v, n = v[:5], n[:5]
        res = shc.fit_capsules(self._skeleton(), v, n, np.array(["b"] * len(v)), min_verts=15)
        self.assertEqual(res["bodies"][0]["action"], "kept")

    def test_axis_map_found_from_bone_positions(self):
        engine = {"a": np.array([0.0, -2.47, 2.175]), "b": np.array([0.3, 1.1, 2.4]), "c": np.array([-0.7, -1.0, 0.3])}
        blender = {k: np.array([-p[0], -p[1], p[2]]) for k, p in engine.items()}
        perm, signs, err = shc.find_axis_map(blender, engine)
        self.assertEqual((tuple(perm), tuple(signs)), ((0, 1, 2), (-1, -1, 1)))
        self.assertLess(err, 1e-9)

    def test_axis_map_refuses_a_mismatched_skeleton(self):
        engine = {"a": np.array([0.0, 0.0, 0.0]), "b": np.array([1.0, 0.0, 0.0]), "c": np.array([0.0, 2.0, 0.0])}
        blender = {"a": np.array([0.0, 0.0, 0.0]), "b": np.array([3.0, 0.0, 0.0]), "c": np.array([0.0, 0.0, 5.0])}
        with self.assertRaises(ValueError):
            shc.find_axis_map(blender, engine)


if __name__ == "__main__":
    unittest.main()
