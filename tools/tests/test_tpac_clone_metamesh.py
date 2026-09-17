#!/usr/bin/env python3
"""Contract tests for tools/tpac_clone_metamesh.py.

Run:  python -m unittest tools.tests.test_tpac_clone_metamesh

The synthetic tpac below is built by hand, byte for byte, from the layout the tool
relies on (header, TOC entry, segment entry, LZ4 binding segment), so the test is
independent of the tool's own parser. The one test that touches the game install
(round-trip identity on the live spider bundle) is the check
docs/ai-includes/creature-mount-authoring.md asks of every tpac writer: serialise the
parsed file unchanged and prove the bytes match. It is skipped where the install is
absent.
"""
import os
import struct
import sys
import tempfile
import unittest
import uuid
from pathlib import Path

import lz4.block

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import tpac_clone_metamesh as tcm  # noqa: E402
from _gamedir import game_dir  # noqa: E402

LIVE_SPIDER = (Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
               / "Modules" / "LOTRLOME_Armory" / "Assets" / "creature" / "spider"
               / "animations" / "spider_correct_geo.tpac")

METAMESH = bytes.fromhex("978b8fa07c19ea4bb95b53846cae834e")
OTHER_TYPE = bytes.fromhex("7936ba3e00000000000000000000aaaa")
LOD_TAG = bytes.fromhex("5f98413d")
BIND_TAG = bytes.fromhex("f6304064")
LOD_CONST = bytes.fromhex("d224c14f82e46a6e0da3f4e2")
BIND_CONST = bytes.fromhex("428a864cb9359b9daa9391c2")

OLD_MAT = uuid.UUID("3dddc75e-b580-8244-906a-093c9e3f9225").bytes
NEW_MAT = uuid.UUID("11111111-2222-3333-4444-555555555555").bytes
FBX_GUID = uuid.UUID("47582011-3181-4d4d-a885-d89a0f8de2a1").bytes


def _sized(s: str) -> bytes:
    b = s.encode("ascii")
    return struct.pack("<i", len(b)) + b


def _seg_entry(offset: int, actual: int, storage: int, guid: bytes, tag: bytes, const: bytes) -> bytes:
    return (struct.pack("<QQQ", offset, actual, storage) + guid + tag + const
            + b"\x11" * 8 + b"\0" * 4 + b"\x01")


def build_metamesh(name: str, lods: int, item_guid: bytes, mat_guid: bytes, lod_blobs: list):
    """A metamesh item in the shape the live spider bundle has: `lods` geometry segments
    plus one LZ4 binding segment naming mesh and material per LOD. Returns
    (toc_bytes_without_offsets_fixed, [(guid, tag, const, blob, actual)])."""
    seg_guids = [uuid.uuid4().bytes for _ in range(lods)]
    meta = b"\x01\0\0\0" + FBX_GUID + b"\xff\xff\x7f\x7f" + b"\0" * 24
    lod_names = [name] + [f"{name}.lod{i}" for i in range(1, lods)]
    for guid, lod_name in zip(seg_guids, lod_names):
        meta += guid + _sized(lod_name) + b"\0" * 8 + mat_guid + struct.pack("<6f", *([1.0] * 6))
    binding_raw = struct.pack("<i", lods)
    for lod_name in lod_names:
        binding_raw += _sized(lod_name) + _sized("m_test_mat_a3")
    binding_blob = lz4.block.compress(binding_raw, store_size=False)
    segs = [(g, LOD_TAG, LOD_CONST, blob, len(blob)) for g, blob in zip(seg_guids, lod_blobs)]
    segs.append((item_guid, BIND_TAG, BIND_CONST, binding_blob, len(binding_raw)))
    return meta, segs


def build_tpac(pkg_guid: bytes, items: list) -> bytes:
    """items: [(type_guid, item_guid, version_field, name, meta, checksum, segs)] with segs as
    build_metamesh returns them. Lays segment data out contiguously in TOC order."""
    tocs = []
    blobs = []
    for type_guid, item_guid, ver_field, name, meta, checksum, segs in items:
        toc = type_guid + item_guid + struct.pack("<I", ver_field) + _sized(name)
        toc += struct.pack("<q", len(meta)) + meta + checksum + struct.pack("<i", len(segs))
        for guid, tag, const, blob, actual in segs:
            toc += _seg_entry(0, actual, len(blob), guid, tag, const)
            blobs.append(blob)
        toc += struct.pack("<i", 0)
        tocs.append(bytearray(toc))
    toc_size = sum(len(t) for t in tocs)
    # second pass: write the real offsets (segment entries are the last 69*n + 4 bytes of each toc)
    cur = 36 + toc_size
    blob_iter = iter(blobs)
    for (_, _, _, _, _, _, segs), toc in zip(items, tocs):
        seg_start = len(toc) - 4 - 69 * len(segs)
        for i in range(len(segs)):
            struct.pack_into("<Q", toc, seg_start + 69 * i, cur)
            cur += len(next(blob_iter))
    header = b"TPAC" + struct.pack("<I", 2) + pkg_guid + struct.pack("<I", len(items)) + struct.pack("<Q", toc_size)
    return header + b"".join(bytes(t) for t in tocs) + b"".join(blobs)


class SyntheticFixture(unittest.TestCase):
    def setUp(self):
        self.pkg = uuid.uuid4().bytes
        self.mesh_guid = uuid.uuid4().bytes
        self.lod_blobs = [os.urandom(300), os.urandom(120), os.urandom(40)]
        meta, segs = build_metamesh("sk_test_body_c", 3, self.mesh_guid, OLD_MAT, self.lod_blobs)
        other_meta = b"\x01\0\0\0" + FBX_GUID
        other_segs = [(FBX_GUID, bytes.fromhex("f83d7de9"), b"\0" * 12, b"raw-fbx-bytes", len(b"raw-fbx-bytes"))]
        self.items = [
            (METAMESH, self.mesh_guid, 1, "sk_test_body_c", meta, b"\xab" * 8, segs),
            (OTHER_TYPE, FBX_GUID, 0, "sk_test_body_c.fbx", other_meta, b"\xcd" * 8, other_segs),
        ]
        self.data = build_tpac(self.pkg, self.items)
        self.materials = {"m_test_mat_a3": OLD_MAT, "m_test_mat_a1": NEW_MAT}


class ParseAndSerialise(SyntheticFixture):
    def test_synthetic_round_trip_is_byte_identical(self):
        pkg = tcm.parse(self.data)
        self.assertEqual(pkg.package_guid, self.pkg)
        self.assertEqual([it.name for it in pkg.items], ["sk_test_body_c", "sk_test_body_c.fbx"])
        self.assertEqual(tcm.serialize(pkg.package_guid, pkg.items), self.data)

    def test_metamesh_items_are_recognised_by_type_guid(self):
        pkg = tcm.parse(self.data)
        self.assertTrue(pkg.items[0].is_metamesh)
        self.assertFalse(pkg.items[1].is_metamesh)
        self.assertEqual(len(pkg.items[0].segments), 4)
        self.assertTrue(pkg.items[0].segments[-1].is_binding)
        self.assertFalse(pkg.items[0].segments[0].is_binding)

    @unittest.skipUnless(LIVE_SPIDER.exists(), "live spider bundle not installed on this machine")
    def test_live_spider_bundle_round_trips_byte_identical(self):
        data = LIVE_SPIDER.read_bytes()
        pkg = tcm.parse(data)
        self.assertEqual(tcm.serialize(pkg.package_guid, pkg.items), data)
        self.assertEqual([it.name for it in pkg.items],
                         ["sk_spider_forest_c", "sk_spider_forest_c_2", "spider_correct.fbx", "spider_skeleton"])


class CloneContract(SyntheticFixture):
    def _clone(self, new_name="sk_test_body_a", old_mat="m_test_mat_a3", new_mat="m_test_mat_a1"):
        pkg = tcm.parse(self.data)
        return pkg, tcm.clone_metamesh(pkg, "sk_test_body_c", new_name,
                                       (old_mat, self.materials[old_mat]), (new_mat, self.materials[new_mat]))

    def test_clone_carries_the_new_name_everywhere_and_the_old_one_nowhere(self):
        _, clone = self._clone()
        self.assertEqual(clone.name, "sk_test_body_a")
        toc = bytes(clone.toc)
        self.assertNotIn(b"sk_test_body_c", toc)
        self.assertEqual(toc.count(b"sk_test_body_a"), 1 + 3)  # name field + one per LOD in the metadata
        self.assertIn(_sized("sk_test_body_a.lod2"), toc)

    def test_clone_rebinds_the_material_guid_and_name(self):
        _, clone = self._clone()
        toc = bytes(clone.toc)
        self.assertEqual(toc.count(OLD_MAT), 0)
        self.assertEqual(toc.count(NEW_MAT), 3)
        raw = tcm.segment_payload(clone, clone.segments[-1])
        self.assertEqual(raw.count(b"m_test_mat_a1"), 3)
        self.assertNotIn(b"m_test_mat_a3", raw)
        self.assertNotIn(b"sk_test_body_c", raw)
        self.assertEqual(struct.unpack_from("<i", raw, 0)[0], 3)
        self.assertEqual(clone.segments[-1].actual, len(raw))

    def test_clone_has_fresh_item_and_segment_guids_consistent_with_its_metadata(self):
        pkg, clone = self._clone()
        src = pkg.items[0]
        self.assertNotEqual(clone.item_guid, src.item_guid)
        self.assertEqual(len(clone.item_guid), 16)
        for s_seg, c_seg in zip(src.segments, clone.segments):
            self.assertNotEqual(c_seg.guid, s_seg.guid)
            self.assertNotIn(s_seg.guid, bytes(clone.toc))
        for c_seg in clone.segments[:-1]:
            self.assertEqual(bytes(clone.toc).count(c_seg.guid), 2)  # segment entry + metadata
        # the binding segment is keyed by the item guid, on the source and on the clone
        self.assertEqual(src.segments[-1].guid, src.item_guid)
        self.assertEqual(clone.segments[-1].guid, clone.item_guid)
        self.assertNotIn(src.item_guid, bytes(clone.toc))
        self.assertIn(FBX_GUID, bytes(clone.toc))  # the source-asset link is left alone

    def test_clone_copies_lod_geometry_verbatim_and_keeps_the_checksum(self):
        pkg, clone = self._clone()
        src = pkg.items[0]
        for s_seg, c_seg, blob in zip(src.segments[:-1], clone.segments[:-1], self.lod_blobs):
            self.assertEqual(tcm.segment_bytes(clone, c_seg), blob)
            self.assertEqual((c_seg.actual, c_seg.storage), (s_seg.actual, s_seg.storage))
            self.assertEqual(c_seg.tag, LOD_TAG)
        self.assertEqual(clone.checksum, src.checksum)

    def test_written_package_reparses_with_a_fresh_package_guid_and_correct_toc_size(self):
        pkg, clone_a = self._clone()
        clone_b = tcm.clone_metamesh(pkg, "sk_test_body_c", "sk_test_body_b",
                                     ("m_test_mat_a3", OLD_MAT), ("m_test_mat_a1", NEW_MAT))
        out = tcm.serialize(uuid.uuid4().bytes, [clone_a, clone_b])
        self.assertNotEqual(out[8:24], self.pkg)
        toc_size = struct.unpack_from("<Q", out, 28)[0]
        self.assertEqual(toc_size, len(clone_a.toc) + len(clone_b.toc))
        back = tcm.parse(out)
        self.assertEqual([it.name for it in back.items], ["sk_test_body_a", "sk_test_body_b"])
        self.assertNotEqual(back.items[0].item_guid, back.items[1].item_guid)
        self.assertEqual(tcm.segment_bytes(back.items[1], back.items[1].segments[0]), self.lod_blobs[0])
        raw = tcm.segment_payload(back.items[1], back.items[1].segments[-1])
        self.assertIn(_sized("sk_test_body_b.lod1"), raw)
        self.assertIn(_sized("m_test_mat_a1"), raw)
        self.assertEqual(tcm.serialize(back.package_guid, back.items), out)

    def test_refuses_a_name_of_a_different_length(self):
        pkg = tcm.parse(self.data)
        with self.assertRaises(tcm.CloneError):
            tcm.clone_metamesh(pkg, "sk_test_body_c", "sk_test_body_long",
                               ("m_test_mat_a3", OLD_MAT), ("m_test_mat_a1", NEW_MAT))

    def test_refuses_a_material_name_of_a_different_length(self):
        pkg = tcm.parse(self.data)
        with self.assertRaises(tcm.CloneError):
            tcm.clone_metamesh(pkg, "sk_test_body_c", "sk_test_body_a",
                               ("m_test_mat_a3", OLD_MAT), ("m_test_mat_a1_x", NEW_MAT))

    def test_refuses_a_material_the_mesh_does_not_bind(self):
        pkg = tcm.parse(self.data)
        stranger = uuid.uuid4().bytes
        with self.assertRaises(tcm.CloneError):
            tcm.clone_metamesh(pkg, "sk_test_body_c", "sk_test_body_a",
                               ("m_test_mat_zz", stranger), ("m_test_mat_a1", NEW_MAT))

    def test_refuses_an_unknown_or_non_metamesh_source(self):
        pkg = tcm.parse(self.data)
        with self.assertRaises(tcm.CloneError):
            tcm.clone_metamesh(pkg, "sk_nobody", "sk_nobod1", ("m_test_mat_a3", OLD_MAT), ("m_test_mat_a1", NEW_MAT))
        with self.assertRaises(tcm.CloneError):
            tcm.clone_metamesh(pkg, "sk_test_body_c.fbx", "sk_test_body_a.fbx",
                               ("m_test_mat_a3", OLD_MAT), ("m_test_mat_a1", NEW_MAT))


class MaterialLookup(unittest.TestCase):
    def test_material_item_guid_is_read_from_the_mtl_tpac_toc_not_the_filename(self):
        guid = uuid.uuid4().bytes
        mtl = build_tpac(uuid.uuid4().bytes, [
            (bytes.fromhex("9313b01d0269194f83bab37a39830717"), guid, 0, "m_real_name", b"\0" * 4, b"\0" * 8,
             [(guid, b"\0\0\0\0", b"\0" * 12, b"x", 1)]),
        ])
        with tempfile.TemporaryDirectory() as tmp:
            Path(tmp, "m_other_file_mtl.tpac").write_bytes(mtl)
            self.assertEqual(tcm.find_material_guid(Path(tmp), "m_real_name"), guid)
            with self.assertRaises(tcm.CloneError):
                tcm.find_material_guid(Path(tmp), "m_other_file")


class CommandLine(SyntheticFixture):
    def test_dry_run_writes_nothing_and_apply_refuses_to_overwrite(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp, "src_geo.tpac")
            src.write_bytes(self.data)
            mats = Path(tmp, "textures")
            mats.mkdir()
            for name, guid in self.materials.items():
                mtl = build_tpac(uuid.uuid4().bytes, [
                    (bytes.fromhex("9313b01d0269194f83bab37a39830717"), guid, 0, name, b"\0" * 4, b"\0" * 8,
                     [(guid, b"\0\0\0\0", b"\0" * 12, b"x", 1)]),
                ])
                Path(mats, f"{name}_mtl.tpac").write_bytes(mtl)
            out = Path(tmp, "variants_geo.tpac")
            argv = [str(src), "--out", str(out), "--material-dir", str(mats),
                    "--clone", "sk_test_body_c=sk_test_body_a,m_test_mat_a3=m_test_mat_a1"]
            self.assertEqual(tcm.main(argv), 0)
            self.assertFalse(out.exists())
            self.assertEqual(tcm.main(argv + ["--apply"]), 0)
            self.assertTrue(out.exists())
            back = tcm.parse(out.read_bytes())
            self.assertEqual([it.name for it in back.items], ["sk_test_body_a"])
            self.assertNotEqual(tcm.main(argv + ["--apply"]), 0)  # never overwrites


if __name__ == "__main__":
    unittest.main()
