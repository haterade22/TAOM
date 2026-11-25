#!/usr/bin/env python3
"""Contract tests for tools/tpac_slim_package.py.

Run:  python -m unittest tools.tests.test_tpac_slim_package

The items of the synthetic packages are built byte by byte here, from the layout
tpac_clone_metamesh.py documents; the package around them is laid out by its `serialize`, the
writer the tool uses, so a fault of that writer is its own tests' to find. One test runs the round-trip gate
on a real package named by the environment variable TAOM_SLIM_REAL_TPAC and is skipped when
that is unset or the file is absent.
"""
import io
import os
import struct
import sys
import tempfile
import unittest
import uuid
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

import lz4.block
import xxhash

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import tpac_clone_metamesh as tcm  # noqa: E402
import tpac_slim_package as tsp  # noqa: E402

SKELETON = tsp.PROTECTED_TYPE_GUIDS["Skeleton"]
CLIP = tsp.PROTECTED_TYPE_GUIDS["AnimationClip"]
G = {n: uuid.UUID(int=i + 1).bytes_le for i, n in enumerate(
    ["mesh_a", "mesh_b", "mat_a", "mat_b", "mat_shared", "tex_a", "tex_b", "tex_shared", "skel", "clip"])}
ROOT_OLD = "$BASE/Modules/SomeMod/AssetSources/deep/folder"
REAL = os.environ.get("TAOM_SLIM_REAL_TPAC", "")


def _sized(s: bytes) -> bytes:
    return struct.pack("<i", len(s)) + s


def build_item(type_guid, name, item_guid, meta, payloads=(), deps=()):
    """One item entry. Segment offsets are left 0; the writer under test fills them in. A payload is raw bytes,
    stored as they are (storage format 0), or a pair (LZ4 blob, unpacked bytes) for a packed segment (format 1)."""
    stored = [p if isinstance(p, tuple) else (p, None) for p in payloads]
    body = _sized(meta)[4:]
    meta_block = struct.pack("<q", len(body)) + body
    entry = type_guid + item_guid + struct.pack("<I", 7) + _sized(name.encode()) + meta_block
    entry += struct.pack("<Q", xxhash.xxh64(meta_block, seed=0).intdigest())
    entry += struct.pack("<i", len(payloads))
    for k, (blob, unpacked) in enumerate(stored):
        raw = blob if unpacked is None else unpacked
        entry += (struct.pack("<QQQ", 0, len(raw), len(blob)) + uuid.uuid4().bytes + bytes([k + 1, 2, 3, 4]) + b"\x00" * 12
                  + struct.pack("<Q", xxhash.xxh64(raw, seed=0).intdigest()) + b"\0" * 4
                  + (b"\x00" if unpacked is None else b"\x01"))
    entry += struct.pack("<i", len(deps)) + b"".join(d + b"\0" * 32 for d in deps)
    item, end = tcm._read_item(entry, 0, 2)
    assert end == len(entry)
    item.blobs = [bytes(blob) for blob, _ in stored]
    return item


def path_meta(prefix: bytes, path: str, suffix: bytes = b"\x01\x02\x03") -> bytes:
    return prefix + _sized(path.encode()) + suffix


def make_package(guid=None):
    """mesh_a uses mat_a and mat_shared; mesh_b uses mat_b and mat_shared. Each material has its own
    texture, mat_shared has tex_shared. A skeleton and a clip ride along (the clip has a dependency)."""
    items = [
        build_item(SKELETON, "skel", G["skel"], b"skeleton-meta", [b"S" * 40]),
        build_item(tcm.METAMESH_TYPE_GUID, "mesh_a", G["mesh_a"], b"hdr" + G["mat_a"] + b"mid" + G["mat_shared"], [b"A" * 100, b"a" * 20]),
        build_item(tcm.METAMESH_TYPE_GUID, "mesh_b", G["mesh_b"], b"hdr" + G["mat_b"] + b"mid" + G["mat_shared"], [b"B" * 90]),
        build_item(tsp.MATERIAL_TYPE_GUID, "mat_a", G["mat_a"], b"m" + G["tex_a"]),
        build_item(tsp.MATERIAL_TYPE_GUID, "mat_b", G["mat_b"], b"m" + G["tex_b"]),
        build_item(tsp.MATERIAL_TYPE_GUID, "mat_shared", G["mat_shared"], b"m" + G["tex_shared"]),
        build_item(tsp.TEXTURE_TYPE_GUID, "tex_a", G["tex_a"], path_meta(b"\x05\x00", f"{ROOT_OLD}/tex_a.png"), [b"TA" * 30]),
        build_item(tsp.TEXTURE_TYPE_GUID, "tex_b", G["tex_b"], path_meta(b"\x05\x00", f"{ROOT_OLD}/tex_b.png"), [b"TB" * 30]),
        build_item(tsp.TEXTURE_TYPE_GUID, "tex_shared", G["tex_shared"], path_meta(b"\x05\x00", f"{ROOT_OLD}/tex_shared.png", b"has_alpha"), [b"TS" * 30]),
        build_item(CLIP, "clip", G["clip"], b"clip-meta", [b"C" * 10], deps=[G["skel"]]),
    ]
    guid = guid or uuid.UUID(int=999).bytes
    return tcm.serialize(guid, items)


def names(pkg):
    return [i.name for i in pkg.items]


def _zero_offsets(toc, item, shift=0):
    t = bytearray(toc)
    for s in item.segments:
        struct.pack_into("<Q", t, s.entry_pos - shift, 0)
    return bytes(t)


def _regions(item):
    """The item cut around its path string: everything before the metadata length, the metadata before
    the string's length prefix, the metadata after the string, and the tail after the checksum
    (segment offsets zeroed, since removing or resizing items moves them)."""
    toc = bytes(item.toc)
    start, end = tcm._metadata_span(item)
    at = toc.find(b"$BASE/", start + 8)
    n = struct.unpack_from("<i", toc, at - 4)[0]
    return (toc[:start], toc[start + 8:at - 4], toc[at + n:end], _zero_offsets(toc[end + 8:], item, end + 8))


class RoundTripTests(unittest.TestCase):
    def test_synthetic_package_round_trips_through_the_gate(self):
        data = make_package()
        pkg = tsp.roundtrip_gate(data)
        self.assertEqual(tcm.serialize(pkg.package_guid, pkg.items, pkg.version), data)

    def test_gate_names_first_differing_offset_when_the_writer_is_lossy(self):
        data = make_package()
        real = tcm.serialize
        with mock.patch.object(tcm, "serialize", lambda g, it, v=2: real(g, it, v)[:-1] + b"\xff"):
            with self.assertRaisesRegex(tsp.SlimError, r"first differing offset %d" % (len(data) - 1)):
                tsp.roundtrip_gate(data)

    def test_unchanged_round_trip_is_byte_identical(self):
        data = make_package()
        out, _, plan = tsp.slim(data, [], None, False)
        self.assertEqual(out, data)
        self.assertFalse(plan.removed)

    def test_round_trip_gate_on_a_real_package(self):
        if not REAL or not Path(REAL).is_file():
            self.skipTest("TAOM_SLIM_REAL_TPAC not set or file absent")
        pkg = tsp.roundtrip_gate(Path(REAL).read_bytes())
        self.assertGreater(len(pkg.items), 0)


class DropMeshTests(unittest.TestCase):
    def test_dropping_a_mesh_removes_unshared_material_and_texture_and_keeps_the_shared_ones(self):
        data = make_package()
        out, _, plan = tsp.slim(data, ["mesh_a"], None, False)
        self.assertEqual(names(tcm.parse(out)), ["skel", "mesh_b", "mat_b", "mat_shared", "tex_b", "tex_shared", "clip"])
        self.assertEqual(sorted(i.name for i in plan.removed), ["mat_a", "mesh_a", "tex_a"])
        self.assertEqual(tsp.verify_output(tcm.parse(data), out, plan), [])

    def test_dropping_every_mesh_removes_all_materials_and_textures_but_never_skeleton_or_clip(self):
        out, *_ = tsp.slim(make_package(), ["mesh_a", "mesh_b"], None, False)
        self.assertEqual(names(tcm.parse(out)), ["skel", "clip"])

    def test_unknown_or_non_mesh_drop_name_is_refused(self):
        with self.assertRaises(tsp.SlimError):
            tsp.slim(make_package(), ["nope"], None, False)
        with self.assertRaises(tsp.SlimError):
            tsp.slim(make_package(), ["mat_a"], None, False)

    def test_kept_item_naming_a_removed_one_is_refused(self):
        items = tcm.parse(make_package()).items
        items.insert(3, build_item(tcm.METAMESH_TYPE_GUID, "mesh_c", uuid.UUID(int=50).bytes_le, b"x" + G["mat_a"] + G["mesh_a"]))
        data = tcm.serialize(uuid.UUID(int=1).bytes, items)
        with self.assertRaisesRegex(tsp.SlimError, "still names removed"):
            tsp.slim(data, ["mesh_a"], None, False)

    def test_verify_catches_a_stale_toc_size_and_a_missing_reference(self):
        data = make_package()
        out, src, plan = tsp.slim(data, ["mesh_a"], None, False)
        broken = bytearray(out)
        struct.pack_into("<Q", broken, 28, 5)
        self.assertTrue(any("TOC size" in p for p in tsp.verify_output(src, bytes(broken), plan)))
        keep = [i for i in plan.kept if i.name != "mat_b"]
        out2 = tcm.serialize(src.package_guid, keep)
        self.assertTrue(any("mat_b" in p for p in tsp.verify_output(src, out2, tsp.Plan(keep, []))))


class TexturePathTests(unittest.TestCase):
    def test_path_rewrite_with_longer_and_shorter_string(self):
        for root in ("$BASE/Modules/Other/AssetSources/a/b/c/much/longer/folder/chain/here", "$BASE/x"):
            with self.subTest(root=root):
                data = make_package()
                out, src, plan = tsp.slim(data, [], root, False)
                for it in tcm.parse(out).items:
                    if it.type_guid == tsp.TEXTURE_TYPE_GUID:
                        self.assertEqual(tsp.texture_path(it), f"{root}/{it.name}.png")
                        self.assertEqual(it.checksum, tcm.expected_checksum(it))
                        start, end = tcm._metadata_span(it)
                        self.assertEqual(struct.unpack_from("<q", it.toc, start)[0], end - start - 8)
                self.assertEqual(tsp.verify_output(src, out, plan), [])
                self.assertEqual(struct.unpack_from("<Q", out, 28)[0], sum(len(i.toc) for i in tcm.parse(out).items))
                tampered = bytearray(out)          # the checksum of the first rewritten Texture, changed
                first = next(i for i in tcm.parse(out).items if i.type_guid == tsp.TEXTURE_TYPE_GUID)
                at = out.index(bytes(first.checksum), tcm.HEADER_SIZE)
                tampered[at] ^= 0xFF
                self.assertTrue(any("differs from the planned" in p
                                    for p in tsp.verify_output(src, bytes(tampered), plan)))

    def test_only_the_path_changes_in_a_kept_texture(self):
        data = make_package()
        root = "$BASE/Modules/X/y/deeper"
        out, *_ = tsp.slim(data, [], root, False)
        before = {i.name: i for i in tcm.parse(data).items}
        after = {i.name: i for i in tcm.parse(out).items}
        for name in ("tex_a", "tex_b", "tex_shared"):
            a, b = before[name], after[name]
            self.assertEqual(tsp.texture_path(b), f"{root}/{name}.png")
            self.assertEqual(_regions(a), _regions(b))
            self.assertEqual(b.blobs, a.blobs)
            sa, ea = tcm._metadata_span(a)
            sb, eb = tcm._metadata_span(b)
            self.assertEqual((eb - sb) - (ea - sa), len(root) - len(ROOT_OLD))
        for name in ("skel", "mesh_a", "mat_a", "clip"):
            a, b = before[name], after[name]
            self.assertEqual(_zero_offsets(bytes(a.toc), a), _zero_offsets(bytes(b.toc), b))
            self.assertEqual(a.checksum, b.checksum)

    def test_a_texture_without_a_source_path_is_refused(self):
        items = tcm.parse(make_package()).items
        items[6] = build_item(tsp.TEXTURE_TYPE_GUID, "tex_a", G["tex_a"], b"no path here", [b"TA" * 30])
        with self.assertRaisesRegex(tsp.SlimError, "'\\$BASE/' strings"):
            tsp.slim(tcm.serialize(uuid.UUID(int=1).bytes, items), [], "$BASE/x", False)

    def test_new_guid_changes_only_the_package_guid(self):
        data = make_package()
        out, *_ = tsp.slim(data, [], None, True)
        self.assertNotEqual(out[8:24], data[8:24])
        self.assertEqual(out[:8], data[:8])
        self.assertEqual(out[24:], data[24:])


class CommandLineTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.tmp = Path(self._tmp.name)
        self.src = self.tmp / "in.tpac"
        self.src.write_bytes(make_package())

    def run_main(self, args):
        out = io.StringIO()
        with redirect_stdout(out):
            code = tsp.main(args)
        return code, out.getvalue()

    def test_forbid_trips_in_ascii_utf16_and_any_case(self):
        out = self.tmp / "out.tpac"
        base = [str(self.src), "--out", str(out), "--apply"]
        self.assertEqual(self.run_main(base + ["--forbid", "absentxyz"])[0], 0)
        self.assertTrue(out.exists())
        out.unlink()
        self.assertEqual(self.run_main(base + ["--forbid", "SOMEMOD"])[0], 1)          # the path in the textures
        self.assertFalse(out.exists())
        self.assertEqual(self.run_main(base + ["--texture-source-root", "$BASE/Modules/Armory/x", "--forbid", "SomeMod"])[0], 0)
        self.assertEqual(tsp.contains_text("xx".encode() + "Marker".encode("utf-16-le"), "marker"), ["utf-16le"])
        self.assertEqual(tsp.contains_text(b"..mArKer..", "MARKER"), ["ascii"])
        self.assertEqual(tsp.contains_text(b"nothing", "marker"), [])

    def test_forbid_reads_the_payload_of_a_compressed_segment(self):
        """LZ4 writes a repeated run as a back reference, so a text can be in the payload and not in the file's
        bytes. Every segment of the live rig package is compressed."""
        payload = b"MARKER" + b"q" * 40 + b"MARKERTEXT" + b"z" * 40
        packed = lz4.block.compress(payload, store_size=False)
        self.assertNotIn(b"markertext", packed.lower())
        item = build_item(CLIP, "clip", G["clip"], b"clip-meta", [(packed, payload)])
        self.src.write_bytes(tcm.serialize(uuid.UUID(int=999).bytes, [item]))
        out = self.tmp / "out.tpac"
        self.assertEqual(tsp.contains_text(self.src.read_bytes(), "markertext"), [])
        code, text = self.run_main([str(self.src), "--out", str(out), "--apply", "--forbid", "MarkerText"])
        self.assertEqual(code, 1, text)
        self.assertIn("clip segment 0", text)
        self.assertFalse(out.exists())
        self.assertEqual(self.run_main([str(self.src), "--out", str(out), "--apply", "--forbid", "absentxyz"])[0], 0)

    def test_forbid_reads_a_packed_segment_whose_two_sizes_are_equal(self):
        """The Kit packs a segment as LZ4 even when that saves nothing: 139 such segments were in the Armory's
        creature folder on 2026-11-24. Equal sizes do not mean "stored raw"."""
        packed = bytes.fromhex("504d41524b7805008054455854656e6473")
        payload = b"MARKxMARKTEXTends"
        self.assertEqual(lz4.block.decompress(packed, uncompressed_size=len(payload)), payload)
        self.assertEqual(len(packed), len(payload))
        item = build_item(CLIP, "clip", G["clip"], b"clip-meta", [(packed, payload)])
        self.src.write_bytes(tcm.serialize(uuid.UUID(int=999).bytes, [item]))
        out = self.tmp / "out.tpac"
        self.assertEqual(tsp.contains_text(self.src.read_bytes(), "marktext"), [])
        code, text = self.run_main([str(self.src), "--out", str(out), "--apply", "--forbid", "MARKTEXT"])
        self.assertEqual(code, 1, text)
        self.assertIn("clip segment 0", text)
        self.assertFalse(out.exists())

    def test_a_segment_marked_packed_that_does_not_unpack_is_refused(self):
        item = build_item(CLIP, "clip", G["clip"], b"clip-meta", [(b"\xff" * 9, b"y" * 40)])
        self.src.write_bytes(tcm.serialize(uuid.UUID(int=999).bytes, [item]))
        code, text = self.run_main([str(self.src), "--out", str(self.tmp / "out.tpac"), "--apply", "--forbid", "abc"])
        self.assertEqual(code, 1, text)
        self.assertIn("does not unpack", text)
        # the storage byte alone marks it: both sizes are 17, and the bytes are not LZ4
        item = build_item(CLIP, "clip", G["clip"], b"clip-meta", [(b"\xff" * 17, b"y" * 17)])
        self.src.write_bytes(tcm.serialize(uuid.UUID(int=999).bytes, [item]))
        code, text = self.run_main([str(self.src), "--out", str(self.tmp / "out.tpac"), "--apply", "--forbid", "abc"])
        self.assertEqual(code, 1, text)
        self.assertIn("does not unpack", text)

    def test_forbid_takes_ascii_text_only(self):
        """The search lowers bytes, which is exact for ASCII and wrong for any other letter."""
        code, text = self.run_main([str(self.src), "--out", str(self.tmp / "out.tpac"), "--apply", "--forbid", "caf\u00e9"])
        self.assertEqual(code, 1, text)
        self.assertIn("ASCII", text)
        self.assertFalse((self.tmp / "out.tpac").exists())
        code, text = self.run_main([str(self.src), "--out", str(self.tmp / "out.tpac"), "--apply", "--forbid", ""])
        self.assertEqual(code, 1, text)             # an empty text would be "absent" from every file
        self.assertFalse((self.tmp / "out.tpac").exists())

    def test_a_failed_write_leaves_no_file(self):
        out = self.tmp / "out.tpac"
        real = Path.read_bytes
        with mock.patch.object(Path, "read_bytes", lambda p: b"other" if p == out else real(p)):
            code, text = self.run_main([str(self.src), "--out", str(out), "--apply"])
        self.assertEqual(code, 1, text)
        self.assertIn("refusing", text)
        self.assertFalse(out.exists())

    def test_a_file_that_appears_before_the_write_is_not_touched(self):
        out = self.tmp / "out.tpac"
        out.write_bytes(b"someone else's file")
        real = Path.exists
        with mock.patch.object(Path, "exists", lambda p: False if p == out else real(p)):
            code, text = self.run_main([str(self.src), "--out", str(out), "--apply"])
        self.assertEqual(code, 1, text)
        self.assertEqual(out.read_bytes(), b"someone else's file")

    def test_a_damaged_source_is_refused_with_no_traceback(self):
        whole = make_package()
        out = self.tmp / "out.tpac"
        # A count of -1 on an item with no segment reads as "none" in a loop, so the package round-trips.
        negative, at = bytearray(whole), tcm.HEADER_SIZE
        for item in tcm.parse(whole).items:
            at += len(item.toc)
            if item.name == "mat_a":
                self.assertEqual(struct.unpack_from("<ii", negative, at - 8), (0, 0))
                struct.pack_into("<i", negative, at - 8, -1)
        for label, data in (("one byte short", whole[:-1]), ("negative segment count", bytes(negative))):
            self.src.write_bytes(data)
            code, text = self.run_main([str(self.src), "--out", str(out), "--apply"])
            self.assertEqual(code, 1, label)
            self.assertIn("refusing", text, label)
            self.assertFalse(out.exists(), label)

    def test_the_package_id_is_printed_as_the_cache_entry_names_it(self):
        guid = uuid.UUID("01020304-0506-0708-090a-0b0c0d0e0f10")       # its two byte orders print differently
        self.src.write_bytes(make_package(guid.bytes_le))
        code, text = self.run_main([str(self.src), "--out", str(self.tmp / "o.tpac"), "--apply"])
        self.assertEqual(code, 0)
        self.assertIn(str(guid), text)

    def test_source_is_never_overwritten_and_dry_run_writes_nothing(self):
        before = self.src.read_bytes()
        self.assertEqual(self.run_main([str(self.src), "--out", str(self.src), "--apply"])[0], 1)
        self.assertEqual(self.run_main([str(self.src), "--out", str(self.tmp / "o.tpac")])[0], 0)
        self.assertFalse((self.tmp / "o.tpac").exists())
        self.assertEqual(self.src.read_bytes(), before)

    def test_apply_writes_a_file_that_verifies(self):
        out = self.tmp / "o.tpac"
        self.assertEqual(self.run_main([str(self.src), "--out", str(out), "--apply", "--drop-mesh", "mesh_a", "--new-guid",
                                        "--texture-source-root", "$BASE/Modules/Armory/t"])[0], 0)
        self.assertEqual(names(tcm.parse(out.read_bytes())),
                         ["skel", "mesh_b", "mat_b", "mat_shared", "tex_b", "tex_shared", "clip"])
        self.assertEqual(self.run_main([str(self.src), "--out", str(out), "--apply"])[0], 1)


if __name__ == "__main__":
    unittest.main()
