"""set_clip_balance_name.py: self-key a clip for the engine's melee attack table, the way the Modding Kit does.

The clip packages are built byte by byte here (not with the tool's parser), in the engine's metadata order
(TaleWorlds.Native.dll 0x58C500; the Kit writes the same order at 0xB8C890).
"""
import contextlib
import io
import os
import struct
import sys
import tempfile
import unittest
import uuid
from unittest import mock

try:
    import xxhash
except ImportError as exc:  # CI's python-tests job installs no packages: skip, never error
    raise unittest.SkipTest("the clip packages these tests build need xxhash (%s)" % exc)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import set_clip_balance_name as s  # noqa: E402

ANIM_TYPE = bytes.fromhex("c809655063e5a44cb166a53b92e913a7")
PKG = uuid.UUID("6afdca48-e301-4531-9aff-60b8994ad4dd").bytes_le
ITEM = bytes(range(16))


def sstr(v):
    b = v if isinstance(v, bytes) else v.encode("ascii")
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
            type_guid=ANIM_TYPE, pkg=PKG):
    m = meta() if m is None else m
    body = type_guid + ITEM + struct.pack("<I", 0) + sstr(name)
    region = struct.pack("<q", len(m)) + m
    body += region + struct.pack("<Q", xxhash.xxh64(region, seed=0).intdigest())
    body += struct.pack("<i", segs) + b"\xaa" * (69 * segs)
    body += struct.pack("<i", userdata) + b"\xbb" * (48 * userdata)
    return magic + struct.pack("<I", 2) + pkg + struct.pack("<I", 1) + struct.pack("<Q", len(body)) + body


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


def pkg_guid(name):
    """A package GUID of its own per clip: each package has its own RuntimeDataCache entry."""
    return uuid.uuid5(uuid.NAMESPACE_URL, name).bytes_le


def put(folder, module, name, m, entry=True, stamp=None, item=ITEM):
    """Write `<name>_anm.tpac` into folder and, unless entry is False, its RuntimeDataCache entry under module,
    stamped with the package's stored checksum (or `stamp`): the state a Kit save leaves. Returns the package."""
    buf = package(name=name, m=m, pkg=pkg_guid(name))
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, name + "_anm.tpac"), "wb") as fh:
        fh.write(buf)
    if entry:
        path = s.rdc_path(module, pkg_guid(name))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as fh:
            fh.write(rdc(stored_checksum(buf) if stamp is None else stamp, item))
    return buf


def read(path):
    with open(path, "rb") as fh:
        return fh.read()


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

    def test_refuses_a_metadata_version_before_the_generated_child_index(self):
        # TpacTool AnimationClip.ReadMetadata: the three key strings arrive in version 4 and GeneratedIndex in 5, so
        # an older layout is not the one this parser walks
        for version in (3, 4):
            with self.assertRaises(s.ClipError) as cm:
                s.parse(package(m=meta(version=version)))
            self.assertIn("metadata version %d" % version, str(cm.exception))
        self.assertEqual(s.parse(package(m=meta(version=6))).meta_version, 6)   # the Kit's saves: 107 live clips


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

    def test_cuts_a_non_utf8_blends_with_action_by_its_raw_length(self):
        # a "replace" decode turns the one byte 0xFF into U+FFFD, three bytes when re-encoded: a length recomputed
        # from the decoded text cut two bytes too many
        want = package(m=meta(field="anim_hill_troll_release_overswing_2h", blends_action=""))
        self.assertEqual(s.self_key(package(m=meta(blends_action=b"act_\xff")))[0], want)

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


class IsSelfKeyedTests(unittest.TestCase):
    """One definition of self-keyed: the shape self_key() and the Kit edit write, read by keyed_clips and --check."""

    NAME = "anim_hill_troll_release_overswing_2h"

    def test_only_the_shape_self_key_writes(self):
        n = self.NAME
        self.assertTrue(s.is_self_keyed(s.parse(package(name=n, m=meta(field=n, blends_action=""))), n))
        for m in (meta(), meta(field=n), meta(field="release_overswing_2h_balanced", blends_action=""),
                  meta(field=n, blends_action="", src1="a", src2="b"), meta(field=n, blends_action="", gen=3)):
            self.assertFalse(s.is_self_keyed(s.parse(package(name=n, m=m)), n))
        other = s.parse(package(name="anim_z", m=meta(field="anim_z", blends_action="")))
        self.assertFalse(s.is_self_keyed(other, n), "the item must be the named clip")


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
    """keyed_clips: which named clips have a melee attack table row, read from their packages and their
    RuntimeDataCache entries (the binder's rule 0 and wire_hill_troll_race.py --check both ask this)."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.module = self._tmp.name
        self.dir = os.path.join(self.module, "clips")

    def tearDown(self):
        self._tmp.cleanup()

    def keyed(self, names):
        return s.keyed_clips(self.dir, names, self.module)

    def test_only_self_keyed_packages_count(self):
        put(self.dir, self.module, "anim_a", meta(field="anim_a", blends_action=""))
        put(self.dir, self.module, "anim_b", meta())                                   # unkeyed
        put(self.dir, self.module, "anim_c", meta(field="release_c_balanced"))         # keyed to a twin
        put(self.dir, self.module, "anim_e", meta(field="anim_e"))   # own key, a Blends with action left over
        with open(os.path.join(self.dir, "anim_d_anm.tpac"), "wb") as fh:
            fh.write(b"x")                                                               # not a package
        self.assertEqual(self.keyed(["anim_a", "anim_b", "anim_c", "anim_d", "anim_e", "anim_missing"]), {"anim_a"})

    def test_a_package_whose_item_is_another_clip_does_not_count(self):
        buf = put(self.dir, self.module, "anim_z", meta(field="anim_z", blends_action=""))
        os.rename(os.path.join(self.dir, "anim_z_anm.tpac"), os.path.join(self.dir, "anim_a_anm.tpac"))
        self.assertEqual(s.parse(buf).name, "anim_z")
        self.assertEqual(self.keyed(["anim_a"]), set())

    def test_a_package_the_kit_never_saved_does_not_count(self):
        # the engine skips a package with no RuntimeDataCache entry (tools/check_rdc_entries.py), and a re-cut writes
        # fresh packages without one
        put(self.dir, self.module, "anim_a", meta(field="anim_a", blends_action=""), entry=False)
        self.assertEqual(self.keyed(["anim_a"]), set())

    def test_a_stale_stamp_does_not_count(self):
        put(self.dir, self.module, "anim_a", meta(field="anim_a", blends_action=""), stamp=b"\x00" * 8)
        self.assertEqual(self.keyed(["anim_a"]), set())

    def test_an_entry_for_another_item_does_not_count(self):
        put(self.dir, self.module, "anim_a", meta(field="anim_a", blends_action=""), item=b"\x01" * 16)
        self.assertEqual(self.keyed(["anim_a"]), set())


class IndexClipsTests(unittest.TestCase):
    def test_a_truncated_package_or_a_folder_named_like_one_is_skipped(self):
        with tempfile.TemporaryDirectory() as d:
            good = package(name="anim_a")
            with open(os.path.join(d, "anim_a_anm.tpac"), "wb") as fh:
                fh.write(good)
            # cut right after the item name, the TOC size fixed to match: struct.error, not ClipError
            cut = bytearray(good[:72 + 4 + len("anim_a")])
            struct.pack_into("<Q", cut, 28, len(cut) - 36)
            with open(os.path.join(d, "anim_cut_anm.tpac"), "wb") as fh:
                fh.write(bytes(cut))
            os.mkdir(os.path.join(d, "anim_dir_anm.tpac"))                             # open() raises OSError
            self.assertEqual(s.index_clips(d), {"anim_a": [os.path.join(d, "anim_a_anm.tpac")]})


class MainTests(unittest.TestCase):
    """plan, check, apply and main on a temp module: two unkeyed clips the Kit has saved (RDC entries current)."""

    A, B = "anim_hill_troll_release_overswing_2h", "anim_hill_troll_blocked_overswing_2h"

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.module = self._tmp.name
        self.dir = os.path.join(self.module, "Assets", "clips")
        self.bufs = {n: put(self.dir, self.module, n, meta()) for n in (self.A, self.B)}

    def tearDown(self):
        self._tmp.cleanup()

    def run_main(self, *argv, running=False):
        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(s, "game_or_kit_running", return_value=running), \
                contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            rc = s.main(["--clips-dir", self.dir, "--module", self.module] + list(argv))
        return rc, out.getvalue(), err.getvalue()

    def pkg(self, name):
        return os.path.join(self.dir, name + "_anm.tpac")

    def entry(self, name):
        return s.rdc_path(self.module, pkg_guid(name))

    def snapshot(self, backups=True):
        out = {}
        for root, _dirs, files in os.walk(self.module):
            for f in files:
                if backups or ".bak-balancename-" not in f:
                    out[os.path.join(root, f)] = read(os.path.join(root, f))
        return out

    def backups(self):
        return sorted(p for p in self.snapshot() if ".bak-balancename-" in p)

    def test_dry_run_writes_nothing(self):
        before = self.snapshot()
        rc, out, _ = self.run_main("--clip", self.A)
        self.assertEqual(rc, 0, out)
        self.assertIn("DRY RUN", out)
        self.assertEqual(self.snapshot(), before)

    def test_apply_refuses_while_the_game_or_kit_runs(self):
        before = self.snapshot()
        rc, _, err = self.run_main("--clip", self.A, "--apply", running=True)
        self.assertEqual(rc, 2)
        self.assertIn("REFUSED", err)
        self.assertEqual(self.snapshot(), before)

    def test_apply_keys_the_clip_moves_the_stamp_backs_up_both_then_reports_no_change(self):
        rc, out, err = self.run_main("--clip", self.A, "--apply")
        self.assertEqual(rc, 0, out + err)
        new = read(self.pkg(self.A))
        self.assertTrue(s.is_self_keyed(s.parse(new), self.A))
        self.assertEqual(read(self.entry(self.A))[0x68:0x70], stored_checksum(new))
        baks = self.backups()
        self.assertEqual(len(baks), 2, baks)
        self.assertEqual({read(p) for p in baks},
                         {self.bufs[self.A], rdc(stored_checksum(self.bufs[self.A]))})
        self.assertEqual(read(self.pkg(self.B)), self.bufs[self.B], "a clip not named is not touched")
        # nothing left to write: no change, and no refusal either while the game runs
        rc, out, _ = self.run_main("--clip", self.A, "--apply", running=True)
        self.assertEqual(rc, 0)
        self.assertIn("no change", out)
        rc, out, _ = self.run_main("--clip", self.A, "--check")
        self.assertEqual(rc, 0, out)
        self.assertTrue(out.startswith("OK"), out)

    def test_check_exits_1_on_a_clip_that_is_not_self_keyed(self):
        rc, out, _ = self.run_main("--clip", self.A, "--check")
        self.assertEqual(rc, 1)
        self.assertIn("STALE", out)

    def test_a_failed_read_back_restores_both_files(self):
        before = self.snapshot()
        with mock.patch.object(s, "checksum_ok", return_value=False):
            rc, _, err = self.run_main("--clip", self.A, "--apply")
        self.assertEqual(rc, 1)
        self.assertIn("does not verify", err)
        self.assertEqual(self.snapshot(backups=False), before, "the package and its RDC entry are restored")

    def test_an_existing_backup_is_refused_before_anything_is_written(self):
        with mock.patch.object(s, "dt") as fake:
            fake.datetime.now.return_value.strftime.return_value = "20260926-000000"
            with open(self.entry(self.B) + ".bak-balancename-20260926-000000", "wb") as fh:
                fh.write(b"an older backup")
            before = self.snapshot()
            rc, _, err = self.run_main("--clip", self.A, "--clip", self.B, "--apply")
        self.assertEqual(rc, 1)
        self.assertIn("backup exists", err)
        self.assertEqual(self.snapshot(), before, "the first clip is not written either")

    def test_a_missing_or_duplicated_name_is_refused(self):
        rc, _, err = self.run_main("--clip", "anim_hill_troll_missing")
        self.assertEqual(rc, 1)
        self.assertIn("0 packages carry", err)
        os.makedirs(os.path.join(self.dir, "copy"))
        with open(os.path.join(self.dir, "copy", "other_anm.tpac"), "wb") as fh:
            fh.write(self.bufs[self.A])
        rc, _, err = self.run_main("--clip", self.A)
        self.assertEqual(rc, 1)
        self.assertIn("2 packages carry", err)

    def test_a_package_with_no_rdc_entry_is_refused(self):
        os.remove(self.entry(self.A))
        rc, _, err = self.run_main("--clip", self.A)
        self.assertEqual(rc, 1)
        self.assertIn("no RuntimeDataCache entry", err)

    def test_the_dry_run_refuses_a_foreign_rdc_entry_and_apply_writes_nothing(self):
        # the second clip's entry belongs to another item: --apply used to write the first clip, then refuse
        with open(self.entry(self.B), "wb") as fh:
            fh.write(rdc(stored_checksum(self.bufs[self.B]), item=b"\x01" * 16))
        before = self.snapshot()
        rc, _, err = self.run_main("--clip", self.A, "--clip", self.B)
        self.assertEqual(rc, 1)
        self.assertIn("another item", err)
        rc, _, _ = self.run_main("--clip", self.A, "--clip", self.B, "--apply")
        self.assertEqual(rc, 1)
        self.assertEqual(self.snapshot(), before)

    def test_a_doubled_name_is_written_once(self):
        rc, out, err = self.run_main("--clip", self.A, "--clip", self.A, "--apply")
        self.assertEqual(rc, 0, out + err)
        self.assertEqual(out.count("written %s" % self.A), 1)
        self.assertEqual(len(self.backups()), 2)

    def test_a_clips_file_saved_with_a_bom_names_its_first_clip(self):
        path = os.path.join(self.module, "names.txt")
        with open(path, "wb") as fh:
            fh.write(b"\xef\xbb\xbf" + ("%s\r\n# a comment\r\n%s\r\n" % (self.A, self.B)).encode("ascii"))
        rc, out, err = self.run_main("--clips-file", path)
        self.assertEqual(rc, 0, out + err)
        self.assertIn(self.A, out)
        self.assertIn(self.B, out)


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
