"""set_clip_balance_name.py: self-key a clip for the engine's melee attack table, the way the Modding Kit does.

The clip packages are built byte by byte here (not with the tool's parser), in the engine's metadata order
(TaleWorlds.Native.dll 0x58C500; the Kit writes the same order at 0xB8C890).
"""
import os
import struct
import sys
import unittest
import uuid

import xxhash

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import set_clip_balance_name as s  # noqa: E402

ANIM_TYPE = bytes.fromhex("c809655063e5a44cb166a53b92e913a7")
PKG = uuid.UUID("6afdca48-e301-4531-9aff-60b8994ad4dd").bytes_le
ITEM = bytes(range(16))


def sstr(v):
    b = v.encode("ascii")
    return struct.pack("<i", len(b)) + b


def meta(field="", blends_action="act_release_overswing_2h_balanced", src1="", src2="", gen=-1, version=5,
         flags=("client_prediction", "enable_left_hand_ik"), usages=0):
    m = struct.pack("<I", version)
    m += struct.pack("<6fi", 1.1, 2.0, 111.0, 1.02, 0.0, 0.0, 10)
    m += b"\x11" * 16                                  # animation guid
    m += struct.pack("<4f", 0.2, -1.0, -1.0, -1.0)     # step points
    m += sstr("event:/mission/combat/swing/large") + sstr("") + sstr("") + sstr(blends_action) + sstr("")
    m += struct.pack("<ii", 3, 3)                      # hand poses
    m += sstr("twohanded_up_heavy")
    m += struct.pack("<ff", 0.3, 0.2) + b"\x00" + struct.pack("<i", 0)
    m += sstr(field) + sstr(src1) + sstr(src2) + struct.pack("<b", gen)
    m += struct.pack("<IH", 2, 0)
    m += struct.pack("<i", len(flags)) + b"".join(sstr(f) for f in flags)
    m += struct.pack("<i", usages)
    return m


def package(name="anim_hill_troll_release_overswing_2h", m=None, segs=0, userdata=0, magic=b"TPAC",
            type_guid=ANIM_TYPE):
    m = meta() if m is None else m
    body = type_guid + ITEM + struct.pack("<I", 0) + sstr(name)
    region = struct.pack("<q", len(m)) + m
    body += region + struct.pack("<Q", xxhash.xxh64(region, seed=0).intdigest())
    body += struct.pack("<i", segs) + b"\xaa" * (69 * segs)
    body += struct.pack("<i", userdata) + b"\xbb" * (48 * userdata)
    return magic + struct.pack("<I", 2) + PKG + struct.pack("<I", 1) + struct.pack("<Q", len(body)) + body


def rdc(stamp, item=ITEM):
    h = bytearray(0xA5)
    h[0:4] = b"RDC0"
    h[0x14:0x24] = item
    h[0x24:0x34] = item
    h[0x68:0x70] = stamp
    return bytes(h) + b"\xcc" * 64


def stored_checksum(buf):
    c = s.parse(buf)
    return buf[c.ck_at:c.ck_at + 8]


class ParseTests(unittest.TestCase):
    def test_reads_the_fields_the_engine_reads(self):
        c = s.parse(package())
        self.assertEqual((c.name, c.field, c.blends_with_action, c.src1, c.gen_index),
                         ("anim_hill_troll_release_overswing_2h", "", "act_release_overswing_2h_balanced", "", -1))
        self.assertEqual(c.meta_version, 5)

    def test_refuses_what_is_not_a_single_clip_package(self):
        for bad in (package(magic=b"XPAC"), package(type_guid=b"\x00" * 16), package(segs=1)):
            with self.assertRaises(s.ClipError):
                s.parse(bad)

    def test_refuses_a_toc_size_that_does_not_match_the_file(self):
        with self.assertRaises(s.ClipError):
            s.parse(package() + b"\x00")


class SelfKeyTests(unittest.TestCase):
    def test_writes_the_own_name_clears_blends_with_action_and_keeps_the_package_valid(self):
        old = package()
        new, changed = s.self_key(old)
        self.assertTrue(changed)
        c = s.parse(new)
        self.assertEqual(c.field, "anim_hill_troll_release_overswing_2h")
        self.assertEqual(c.blends_with_action, "")
        self.assertEqual(struct.unpack_from("<Q", new, 28)[0], len(new) - 36)
        region = new[c.mlen_at:c.ck_at]
        self.assertEqual(new[c.ck_at:c.ck_at + 8], struct.pack("<Q", xxhash.xxh64(region, seed=0).intdigest()))
        o = s.parse(old)
        self.assertEqual((c.src1, c.src2, c.gen_index, c.flags, c.usages, c.meta_version),
                         (o.src1, o.src2, o.gen_index, o.flags, o.usages, o.meta_version))

    def test_matches_a_byte_built_expectation_exactly(self):
        want = package(m=meta(field="anim_hill_troll_release_overswing_2h", blends_action=""))
        self.assertEqual(s.self_key(package())[0], want)

    def test_keeps_user_data_entries_the_kit_adds_on_save(self):
        new, _ = s.self_key(package(userdata=1))
        self.assertEqual(new[-48:], b"\xbb" * 48)
        self.assertEqual(s.parse(new).userdata, 1)

    def test_an_already_self_keyed_clip_is_left_alone(self):
        done = package(m=meta(field="anim_hill_troll_release_overswing_2h", blends_action=""))
        self.assertEqual(s.self_key(done), (done, False))

    def test_refuses_a_field_naming_another_clip(self):
        with self.assertRaises(s.ClipError):
            s.self_key(package(m=meta(field="release_overswing_2h_balanced")))

    def test_refuses_a_generated_child(self):
        for m in (meta(src1="a", src2="b"), meta(gen=3)):
            with self.assertRaises(s.ClipError):
                s.self_key(package(m=m))

    def test_refuses_a_name_the_engine_buffer_cannot_hold(self):
        with self.assertRaises(s.ClipError):
            s.self_key(package(name="a" * 64))


class RdcTests(unittest.TestCase):
    def test_stamp_follows_the_new_checksum_and_nothing_else_moves(self):
        old = package()
        new, _ = s.self_key(old)
        entry = rdc(stored_checksum(old))
        out = s.stamp_rdc(entry, s.parse(new).item_guid, stored_checksum(new))
        self.assertEqual(out[0x68:0x70], stored_checksum(new))
        self.assertEqual(out[:0x68] + out[0x70:], entry[:0x68] + entry[0x70:])

    def test_refuses_an_entry_for_another_item(self):
        with self.assertRaises(s.ClipError):
            s.stamp_rdc(rdc(b"\x00" * 8, item=b"\x01" * 16), ITEM, b"\x00" * 8)

    def test_rdc_file_is_named_by_the_package_guid(self):
        self.assertEqual(os.path.basename(s.rdc_path("M", s.parse(package()).package_guid)),
                         "6AFDCA48-E301-4531-9AFF-60B8994AD4DD.rdc")


class KeyedClipsTests(unittest.TestCase):
    """keyed_clips: which named clips have a melee attack table row, read from their packages (the binder's rule 0
    and wire_hill_troll_race.py --check both ask this)."""

    def test_only_self_keyed_packages_count(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            def put(name, m):
                open(os.path.join(d, name + "_anm.tpac"), "wb").write(package(name=name, m=m))
            put("anim_a", meta(field="anim_a", blends_action=""))
            put("anim_b", meta())                                   # unkeyed
            put("anim_c", meta(field="release_c_balanced"))         # keyed to a twin: not self-keyed
            open(os.path.join(d, "anim_d_anm.tpac"), "wb").write(b"x")   # not a package
            self.assertEqual(s.keyed_clips(d, ["anim_a", "anim_b", "anim_c", "anim_d", "anim_missing"]), {"anim_a"})

    def test_a_package_whose_item_is_another_clip_does_not_count(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            open(os.path.join(d, "anim_a_anm.tpac"), "wb").write(
                package(name="anim_z", m=meta(field="anim_z", blends_action="")))
            self.assertEqual(s.keyed_clips(d, ["anim_a"]), set())


class LiveInstallTests(unittest.TestCase):
    """Read-only against the install: the two clips Mike self-keyed in the Kit on 2026-09-26 parse as done."""

    def setUp(self):
        self.dir = s.CLIPS_DIR
        if not os.path.isdir(self.dir):
            self.skipTest("no install")

    def test_the_kit_edited_pair_is_already_self_keyed(self):
        for name in ("anim_hill_troll_release_overswing_2h", "anim_hill_troll_blocked_overswing_2h"):
            path = os.path.join(self.dir, name + "_anm.tpac")
            if not os.path.exists(path):
                self.skipTest("clip missing")
            buf = open(path, "rb").read()
            self.assertEqual(s.self_key(buf), (buf, False), name)


if __name__ == "__main__":
    unittest.main()
