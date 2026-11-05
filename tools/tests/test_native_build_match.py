#!/usr/bin/env python3
"""Unit tests for native_build_match.py (carry a native address from one engine build to the next).

Run:  python -m unittest tools.tests.test_native_build_match

No install needed beyond capstone (the decoding classes skip without it). The normalizer is fed
exact instruction bytes; the PE-level tests build a tiny synthetic PE. The opt-in integration test
(TAOM_NATIVE_MATCH_IT=1) runs the oracle on the archived v1.5.3, v1.5.4 and v1.5.5 client DLLs
against the cached engine-method maps.
"""
import contextlib
import io
import json
import os
import struct
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_build_match as bm  # noqa: E402
import native_engine_methods as nem  # noqa: E402

try:
    import capstone  # noqa: F401  (CI runs tools/tests with no pip install)
    HAVE_CAPSTONE = True
except ImportError:
    HAVE_CAPSTONE = False

IMG_BASE = 0x180000000
IMG_SIZE = 0x100000


def make_md():
    from capstone import CS_ARCH_X86, CS_MODE_64, Cs
    return Cs(CS_ARCH_X86, CS_MODE_64)


def digest(code, start=0x1000):
    return bm.normalize(code, start, start + len(code), IMG_BASE, IMG_SIZE, make_md())


def rel32(op, target_delta):
    """`op` (E8, E9) or a two-byte 0F 8x opcode, then a rel32 that lands `target_delta` bytes
    from the instruction end."""
    return op + struct.pack("<i", target_delta)


RET = b"\xC3"
NOP = b"\x90"


@unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
class NormalizeTests(unittest.TestCase):
    def test_external_relative_branches_are_masked(self):
        for op in (b"\xE8", b"\xE9", b"\x0F\x85"):
            a = digest(rel32(op, 0x5000) + RET)
            b = digest(rel32(op, -0x7000) + RET)
            self.assertEqual(a, b, op.hex())

    def test_internal_branch_hashes_by_relative_offset(self):
        body = b"\x0F\x84" + struct.pack("<i", 1) + NOP + RET  # jz over the nop to the ret
        self.assertEqual(digest(body, 0x1000), digest(body, 0x7000))
        other = b"\x0F\x84" + struct.pack("<i", 2) + NOP + NOP + RET  # another internal target
        self.assertNotEqual(digest(body), digest(other))
        short_a = b"\x74\x02" + NOP + NOP + RET
        short_b = b"\x74\x01" + NOP + NOP + RET
        self.assertNotEqual(digest(short_a), digest(short_b))

    def test_internal_target_differences_change_the_hash(self):
        a = b"\x0F\x84" + struct.pack("<i", 1) + NOP + NOP + RET  # lands on the second nop
        b = b"\x0F\x84" + struct.pack("<i", 2) + NOP + NOP + RET  # lands on the ret
        self.assertEqual(len(a), len(b))
        self.assertNotEqual(digest(a), digest(b))

    def test_rip_relative_displacement_is_masked(self):
        for opcode in (b"\x48\x8D\x05", b"\x48\x8B\x05"):  # lea / mov rax, [rip+disp]
            self.assertEqual(digest(opcode + struct.pack("<i", 0x1234) + RET),
                             digest(opcode + struct.pack("<i", -0x4321) + RET))

    def test_rip_relative_with_trailing_immediate_keeps_the_immediate(self):
        mov = b"\xC7\x05"  # mov dword [rip+disp], imm32
        a = mov + struct.pack("<i", 0x100) + struct.pack("<I", 7) + RET
        b = mov + struct.pack("<i", 0x900) + struct.pack("<I", 7) + RET
        c = mov + struct.pack("<i", 0x100) + struct.pack("<I", 8) + RET
        self.assertEqual(digest(a), digest(b))
        self.assertNotEqual(digest(a), digest(c))
        cmp_ = b"\x83\x3D"  # cmp dword [rip+disp], imm8
        self.assertEqual(digest(cmp_ + struct.pack("<i", 0x100) + b"\x01" + RET),
                         digest(cmp_ + struct.pack("<i", 0x200) + b"\x01" + RET))
        self.assertNotEqual(digest(cmp_ + struct.pack("<i", 0x100) + b"\x01" + RET),
                            digest(cmp_ + struct.pack("<i", 0x100) + b"\x02" + RET))

    def test_imm64_inside_the_image_is_masked_outside_is_kept(self):
        mov = b"\x48\xB8"
        inside_a = mov + struct.pack("<Q", IMG_BASE + 0x1234) + RET
        inside_b = mov + struct.pack("<Q", IMG_BASE + 0x4321) + RET
        self.assertEqual(digest(inside_a), digest(inside_b))
        out_a = mov + struct.pack("<Q", 0x1122334455667788) + RET
        out_b = mov + struct.pack("<Q", 0x1122334455667789) + RET
        self.assertNotEqual(digest(out_a), digest(out_b))

    def test_indexed_disp32_in_image_is_masked_small_disp_kept(self):
        mov = b"\x8B\x8C\x82"  # mov ecx, [rdx + rax*4 + disp32]
        self.assertEqual(digest(mov + struct.pack("<I", 0x2000) + RET),
                         digest(mov + struct.pack("<I", 0x3000) + RET))
        self.assertNotEqual(digest(mov + struct.pack("<I", 0x10) + RET),
                            digest(mov + struct.pack("<I", 0x20) + RET))

    def test_a_field_offset_without_an_index_register_is_kept(self):
        mov = b"\x8B\x89"  # mov ecx, [rcx + disp32]
        self.assertNotEqual(digest(mov + struct.pack("<I", 0x2000) + RET),
                            digest(mov + struct.pack("<I", 0x3000) + RET))

    def test_jump_table_inside_the_body_stops_decoding(self):
        head = b"\x8B\x8C\x82" + struct.pack("<I", 0x1008) + RET  # table at start + 8
        a = head + struct.pack("<II", 0x11111111, 0x22222222)
        b = head + struct.pack("<II", 0x33333333, 0x44444444)
        self.assertEqual(digest(a), digest(b))
        self.assertNotEqual(digest(a), digest(a + b"\x00\x00\x00\x00"))

    def test_undecodable_tail_counts_by_length_only(self):
        a = RET + b"\x06\x11\x22"  # 0x06 does not decode in 64-bit mode
        b = RET + b"\x06\x33\x44"
        self.assertEqual(digest(a), digest(b))
        self.assertNotEqual(digest(a), digest(a + b"\x55"))

    def test_other_imm32_is_kept(self):
        self.assertNotEqual(digest(b"\xB9" + struct.pack("<I", 1) + RET),
                            digest(b"\xB9" + struct.pack("<I", 2) + RET))


class FakeImage:
    """A bytearray image with a `read(rva, n)` like the one pdata_units takes."""

    def __init__(self, size=0x400):
        self.mem = bytearray(size)

    def read(self, rva, n):
        return bytes(self.mem[rva:rva + n])

    def unwind(self, rva, flags, count=0, parent=None):
        """UNWIND_INFO at `rva`; a chained one carries its parent RUNTIME_FUNCTION."""
        self.mem[rva:rva + 4] = bytes([(flags << 3) | 1, 0, count, 0])
        if parent is not None:
            at = rva + 4 + 2 * ((count + 1) & ~1)
            self.mem[at:at + 12] = struct.pack("<III", *parent)


class PdataUnitTests(unittest.TestCase):
    def test_chained_entries_find_their_root_and_zero_entries_are_skipped(self):
        img = FakeImage()
        root = (0x1000, 0x1100, 0x200)
        img.unwind(0x200, 0)
        mid = (0x1100, 0x1180, 0x220)
        img.unwind(0x220, 4, count=3, parent=root)  # odd count: codes padded to four
        tail = (0x1180, 0x11C0, 0x240)
        img.unwind(0x240, 4, count=2, parent=mid)  # two hops to the root
        units = bm.pdata_units([tail, (0, 0, 0), root, mid], img.read)
        self.assertEqual(units, [bm.Unit(0x1000, 0x1100, "primary", None),
                                 bm.Unit(0x1100, 0x1180, "chained", 0x1000),
                                 bm.Unit(0x1180, 0x11C0, "chained", 0x1000)])


@unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
class LeafUnitTests(unittest.TestCase):
    def leaves(self, text, spans=()):
        return bm.leaf_units(bytes(text), 0x1000, list(spans), make_md())

    def test_two_leaves_separated_by_padding(self):
        text = (b"\xB8\x01\x00\x00\x00\xC3" + b"\xCC" * 10) + b"\x31\xC0\xC3" + b"\xCC" * 13
        self.assertEqual([(u.start, u.end, u.kind) for u in self.leaves(text)],
                         [(0x1000, 0x1006, "leaf"), (0x1010, 0x1013, "leaf")])

    def test_adjacent_leaves_split_at_an_aligned_ret(self):
        text = NOP * 15 + RET + b"\x31\xC0\xC3" + b"\xCC" * 13
        self.assertEqual([(u.start, u.end) for u in self.leaves(text)],
                         [(0x1000, 0x1010), (0x1010, 0x1013)])

    def test_early_return_before_unaligned_code_stays_one_unit(self):
        text = b"\x84\xC0\x74\x01\xC3\x31\xC0\xC3" + b"\xCC" * 24
        self.assertEqual([(u.start, u.end) for u in self.leaves(text)], [(0x1000, 0x1008)])

    def test_leading_padding_is_skipped_and_padding_has_no_unit(self):
        text = b"\xCC\xCC\x31\xC0\xC3" + b"\xCC" * 27
        units = self.leaves(text)
        self.assertEqual([(u.start, u.end) for u in units], [(0x1002, 0x1005)])
        self.assertFalse(any(u.start <= 0x1008 < u.end for u in units))

    def test_pdata_spans_are_not_leaves(self):
        text = NOP * 15 + RET + b"\x31\xC0\xC3" + b"\xCC" * 13
        self.assertEqual([(u.start, u.end) for u in self.leaves(text, [(0x1000, 0x1010)])],
                         [(0x1010, 0x1013)])


def row(start, size, h, strings=(), kind="primary", root=None):
    return bm.Row(start, start + size, kind, root, h, frozenset(strings))


def make_index(rows, key="old-0000000000000000", text=(0x1000, 0x9000)):
    rows = sorted(rows)
    return bm.Index(key, text[0], text[1], rows, [r.start for r in rows])


class MatchTests(unittest.TestCase):
    def test_unique_hash_matches_and_carries_the_offset(self):
        old = make_index([row(0x1000, 0x100, "aa")])
        new = make_index([row(0x2000, 0x100, "aa")])
        results = bm.match(old.rows, new.rows)
        self.assertEqual(results[0x1000], bm.Result("MATCHED", 0x2000, "hash", 1, 1))
        carried = bm.carry(old, results, 0x1010)
        self.assertEqual((carried.row.start, carried.offset, carried.result.new_start),
                         (0x1000, 0x10, 0x2000))

    def test_a_duplicate_on_either_side_is_ambiguous(self):
        new_dup = bm.match([row(0x1000, 16, "aa")], [row(0x2000, 16, "aa"), row(0x3000, 16, "aa")])
        self.assertEqual(new_dup[0x1000], bm.Result("AMBIGUOUS", None, "hash", 2, 1))
        old_dup = bm.match([row(0x1000, 16, "aa"), row(0x1100, 16, "aa")], [row(0x2000, 16, "aa")])
        self.assertEqual(old_dup[0x1000], bm.Result("AMBIGUOUS", None, "hash", 1, 2))

    def test_identical_stubs_are_never_matched(self):
        old = [row(0x1000 + 0x10 * i, 5, "stub") for i in range(35)]
        new = [row(0x5000 + 0x10 * i, 5, "stub") for i in range(35)]
        results = bm.match(old, new)
        self.assertTrue(all(r.status == "AMBIGUOUS" and r.n == 35 and r.k == 35
                            for r in results.values()))

    def test_strings_match_when_hashes_differ(self):
        old = [row(0x1000, 0x100, "aa", {"Foo.cpp", "bad value"})]
        new = [row(0x2000, 0x110, "bb", {"Foo.cpp", "bad value"})]
        self.assertEqual(bm.match(old, new)[0x1000], bm.Result("MATCHED", 0x2000, "strings", 1, 1))

    def test_two_string_candidates_are_ambiguous(self):
        old = [row(0x1000, 0x100, "aa", {"Foo.cpp"})]
        new = [row(0x2000, 0x100, "bb", {"Foo.cpp"}), row(0x3000, 0x100, "cc", {"Foo.cpp"})]
        self.assertEqual(bm.match(old, new)[0x1000], bm.Result("AMBIGUOUS", None, "strings", 2, 1))

    def test_strings_outside_the_size_band_are_unknown(self):
        old = [row(0x1000, 0x100, "aa", {"Foo.cpp"})]
        new = [row(0x2000, 0x200, "bb", {"Foo.cpp"})]
        self.assertEqual(bm.match(old, new)[0x1000].status, "UNKNOWN")

    def test_an_old_string_set_shared_by_two_units_is_not_used(self):
        old = [row(0x1000, 0x100, "aa", {"Foo.cpp"}), row(0x1200, 0x100, "ab", {"Foo.cpp"})]
        new = [row(0x2000, 0x100, "bb", {"Foo.cpp"})]
        self.assertEqual({r.status for r in bm.match(old, new).values()}, {"UNKNOWN"})

    def test_a_hash_matched_unit_is_never_claimed_by_strings(self):
        old = [row(0x1000, 0x100, "aa", {"Foo.cpp"}), row(0x1200, 0x100, "zz", {"Foo.cpp"})]
        new = [row(0x2000, 0x100, "aa", {"Foo.cpp"})]
        results = bm.match(old, new)
        self.assertEqual(results[0x1000].how, "hash")
        self.assertEqual(results[0x1200].status, "UNKNOWN")

    def test_a_match_whose_shift_no_neighbour_shares_is_ambiguous(self):
        old = [row(0x1000 + 0x100 * i, 0x20, f"h{i}") for i in range(4)]
        shifts = [0x1000, 0x1000, 0x1000, 0x1010]  # the last one moved differently
        new = [row(o.start + s, 0x20, o.hash) for o, s in zip(old, shifts)]
        results = bm.match(old, new)
        self.assertEqual([results[o.start].status for o in old],
                         ["MATCHED", "MATCHED", "MATCHED", "AMBIGUOUS"])
        self.assertEqual(results[old[3].start].how, "shift")

    def test_two_displaced_neighbours_cannot_vouch_for_each_other(self):
        old = [row(0x1000 + 0x100 * i, 0x20, f"h{i}") for i in range(8)]
        shifts = [0x1000, 0x1000, 0x1000, 0x2000, 0x2000, 0x1000, 0x1000, 0x1000]
        new = [row(o.start + s, 0x20, o.hash) for o, s in zip(old, shifts)]
        results = bm.match(old, new)
        self.assertEqual([results[o.start].status for o in old],
                         ["MATCHED"] * 3 + ["AMBIGUOUS"] * 2 + ["MATCHED"] * 3)

    def test_a_lone_match_has_no_neighbour_to_disagree(self):
        results = bm.match([row(0x1000, 0x20, "aa")], [row(0x5000, 0x20, "aa")])
        self.assertEqual(results[0x1000].status, "MATCHED")

    def test_the_shift_check_covers_strings_matches(self):
        old = [row(0x1000 + 0x100 * i, 0x20, f"h{i}") for i in range(3)]
        old.append(row(0x1300, 0x20, "sx", {"Foo.cpp"}))
        new = [row(o.start + 0x1000, 0x20, o.hash) for o in old[:3]]
        new.append(row(0x1310, 0x20, "sy", {"Foo.cpp"}))  # shift 0x10 against 0x1000 next door
        results = bm.match(old, new)
        self.assertEqual(results[0x1300].status, "AMBIGUOUS")
        self.assertEqual(results[0x1300].how, "shift")

    def test_string_sets_are_counted_over_every_unit_not_only_leftovers(self):
        shared = {"%.%df"}
        old = [row(0x1000, 0x20, "aa", shared), row(0x1100, 0x20, "bb", shared)]
        new = [row(0x2000, 0x20, "aa", shared), row(0x2100, 0x20, "cc", shared)]
        results = bm.match(old, new)
        self.assertEqual(results[0x1000].how, "hash")
        self.assertEqual(results[0x1100].status, "UNKNOWN")  # its set is shared with a matched unit

    def test_unknown_is_never_called_removed(self):
        results = bm.match([row(0x1000, 0x100, "aa")], [row(0x2000, 0x100, "bb")])
        self.assertEqual(results[0x1000].status, "UNKNOWN")
        old = make_index([row(0x1000, 0x100, "aa")])
        text = bm.format_row(bm.carry(old, results, 0x1000), 0x1000, results)
        self.assertIn("UNKNOWN", text)
        self.assertNotIn("removed", text.lower())

    def test_carry_in_padding_and_a_strings_match(self):
        old = make_index([row(0x1000, 0x10, "aa", {"s1x"})])
        results = bm.match(old.rows, [row(0x2000, 0x10, "bb", {"s1x"})])
        self.assertIsNone(bm.carry(old, results, 0x1020).row)
        carried = bm.carry(old, results, 0x1004)
        self.assertIn("offset not carried", bm.format_row(carried, 0x1004, results))
        self.assertIn("padding: no function",
                      bm.format_row(bm.carry(old, results, 0x1020), 0x1020, results))

    def test_chained_row_reports_its_root(self):
        old = make_index([row(0x1000, 0x100, "aa"), row(0x1100, 0x40, "cc", kind="chained", root=0x1000)])
        results = bm.match(old.rows, [row(0x2000, 0x100, "aa")])
        text = bm.format_row(bm.carry(old, results, 0x1100), 0x1100, results)
        self.assertIn("chained 0x1100+0x0", text)
        self.assertIn("of 0x1000", text)
        self.assertIn("root MATCHED 0x2000", text)


class IndexCacheTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dll = Path(self.tmp.name) / "bin" / "TaleWorlds.Native.dll"
        self.dll.parent.mkdir()
        self.dll.write_bytes(b"MZ not a real image")
        self.cache = Path(self.tmp.name) / "cache"
        self.built = make_index([row(0x1000, 0x20, "aa", {"x y z"}),
                                 row(0x1100, 0x20, "bb", kind="chained", root=0x1000)])

    def load(self, builder):
        with mock.patch.object(bm, "build_index", side_effect=builder) as patched:
            return bm.load_index(self.dll, self.cache, True), patched.call_count

    def test_round_trip_reads_the_cache_the_second_time(self):
        first, calls = self.load(lambda pe_path, key: self.built._replace(key=key))
        self.assertEqual(calls, 1)
        second, calls = self.load(lambda *a: self.fail("the cache should answer"))
        self.assertEqual(calls, 0)
        self.assertEqual(first.rows, second.rows)
        self.assertEqual(first.starts, second.starts)
        self.assertIsInstance(second.rows[0].strings, frozenset)

    def test_stale_format_and_cut_short_files_rebuild(self):
        first, _ = self.load(lambda p, key: self.built._replace(key=key))
        cache_file = self.cache / f"{first.key}.build-match.json"
        data = json.loads(cache_file.read_text(encoding="utf-8"))
        data["format"] = bm.FORMAT + 1
        cache_file.write_text(json.dumps(data), encoding="utf-8")
        _, calls = self.load(lambda p, key: self.built._replace(key=key))
        self.assertEqual(calls, 1)
        cache_file.write_text(cache_file.read_text(encoding="utf-8")[:40], encoding="utf-8")
        _, calls = self.load(lambda p, key: self.built._replace(key=key))
        self.assertEqual(calls, 1)

    def test_no_cache_never_writes(self):
        with mock.patch.object(bm, "build_index", side_effect=lambda p, key: self.built):
            bm.load_index(self.dll, self.cache, False)
        self.assertFalse(self.cache.exists())


def make_pe(text, entries=(), unwind=None, with_pdata=True):
    """A synthetic PE32+: .text at 0x1000, .pdata at 0x2000, .rdata at 0x3000, raw offset equal to
    RVA. `entries` are (start, end, unwind rva); `unwind` maps an rva to UNWIND_INFO bytes."""
    img = bytearray(0x4000)
    img[0:2] = b"MZ"
    struct.pack_into("<I", img, 0x3C, 0x80)
    img[0x80:0x84] = b"PE\0\0"
    sections = [(b".text", 0x1000), (b".rdata", 0x3000)]
    if with_pdata:
        sections.insert(1, (b".pdata", 0x2000))
    struct.pack_into("<HHIIIHH", img, 0x84, 0x8664, len(sections), 0, 0, 0, 240, 0x22)
    opt = 0x98
    struct.pack_into("<H", img, opt, 0x20B)
    struct.pack_into("<Q", img, opt + 24, IMG_BASE)
    struct.pack_into("<I", img, opt + 56, IMG_SIZE)
    sec = opt + 240
    for i, (name, rva) in enumerate(sections):
        struct.pack_into("<8sIIII", img, sec + 40 * i, name, 0x1000, rva, 0x1000, rva)
    img[0x1000:0x1000 + len(text)] = text
    for i, entry in enumerate(entries):
        struct.pack_into("<III", img, 0x2000 + 12 * i, *entry)
    for rva, blob in (unwind or {}).items():
        img[rva:rva + len(blob)] = blob
    return bytes(img)


SYNTH_TEXT = (b"\xB8\x01\x00\x00\x00\xC3" + b"\xCC" * 10 + b"\x31\xC0\xC3").ljust(0x1000, b"\xCC")
SYNTH_ENTRIES = [(0x1000, 0x1006, 0x3000)]
SYNTH_UNWIND = {0x3000: bytes([0x01, 0, 0, 0])}


def labelled(iface, method, engine, rva, ident, assembly="MountAndBlade"):
    return nem.Resolved(assembly, ident, iface, method, engine, rva)


class OracleTests(unittest.TestCase):
    def setUp(self):
        self.old_map = nem.EngineMethodMap([
            labelled("IMBAgent", "Foo", "foo", 0x1000, 5),
            labelled("IMBAgent", "Bar", "bar", 0x1100, 6),
            labelled("IMBAgent", "Baz", "baz", 0x1200, 7),
            labelled("IMBAgent", "Qux", "qux", 0x1200, 8),
        ])
        self.new_map = nem.EngineMethodMap([
            labelled("IMBAgent", "Foo", "foo", 0x2000, 9),  # renumbered from 5
            labelled("IMBAgent", "Bar", "bar", 0x2100, 12),
            labelled("IMBAgent", "Baz", "baz", 0x2200, 13),
            labelled("IMBAgent", "Qux", "qux", 0x2200, 14),
        ])
        # p1 and p2 anchor aa's shift; the check needs two matched neighbours to vouch for a unit
        self.old = make_index([row(0x0E00, 0x20, "p1"), row(0x0F00, 0x20, "p2"),
                               row(0x1000, 0x20, "aa"), row(0x1100, 0x20, "bb"),
                               row(0x1200, 0x20, "cc"), row(0x1300, 0x20, "dd"),
                               row(0x1400, 0x20, "ee")])

    def score(self, new_rows):
        new = make_index(new_rows + [row(0x1E00, 0x20, "p1"), row(0x1F00, 0x20, "p2")], key="new-1111111111111111")
        results = bm.match(self.old.rows, new.rows)
        return bm.oracle(self.old_map, self.new_map, self.old, results)

    def test_renumbered_ids_and_multi_owner_addresses(self):
        report = self.score([row(0x2000, 0x20, "aa"), row(0x2100, 0x20, "bb"),
                             row(0x2200, 0x20, "cc")])
        self.assertEqual((report.total, report.correct_hash, report.wrong), (2, 2, []))

    def test_a_wrong_match_is_listed(self):
        report = self.score([row(0x2000, 0x20, "aa"), row(0x2300, 0x20, "bb"),
                             row(0x2100, 0x20, "zz"), row(0x2400, 0x20, "cc"),
                             row(0x2500, 0x20, "dd"), row(0x2600, 0x20, "ee")])
        self.assertEqual(report.correct_hash, 1)
        self.assertEqual(report.wrong, [("IMBAgent.Bar", 0x1100, 0x2300, 0x2100, "hash")])
        lines = bm.format_oracle(report)
        self.assertTrue(lines[0].startswith("oracle: 2 single-owner methods: correct 1 (hash 1, strings 0), wrong 1"))
        self.assertIn("WRONG IMBAgent.Bar 0x1100 -> 0x2300, registered 0x2100 (hash)", lines)

    def test_ambiguous_and_unknown_are_counted(self):
        report = self.score([row(0x2000, 0x20, "aa"), row(0x2300, 0x20, "aa")])
        self.assertEqual((report.ambiguous, report.unknown, report.correct_hash), (1, 1, 0))

    def strings_case(self, registered):
        old_map = nem.EngineMethodMap([labelled("IMBAgent", "Foo", "foo", 0x1000, 5)])
        new_map = nem.EngineMethodMap([labelled("IMBAgent", "Foo", "foo", registered, 9)])
        old = make_index([row(0x1000, 0x20, "aa", {"Foo.cpp"})])
        new = make_index([row(0x2000, 0x20, "bb", {"Foo.cpp"}), row(0x3000, 0x20, "cc")],
                         key="new-1111111111111111")
        return bm.oracle(old_map, new_map, old, bm.match(old.rows, new.rows))

    def test_a_strings_match_on_the_registered_address_is_correct(self):
        report = self.strings_case(0x2000)
        self.assertEqual((report.correct_strings, report.wrong), (1, []))

    def test_a_strings_match_on_another_unit_is_wrong(self):
        report = self.strings_case(0x3000)
        self.assertEqual(report.wrong, [("IMBAgent.Foo", 0x1000, 0x2000, 0x3000, "strings")])

    def test_a_strings_match_is_wrong_when_the_registered_address_is_mid_unit(self):
        self.assertEqual(len(self.strings_case(0x2008).wrong), 1)

    def run_main(self, argv):
        indexes = {"old.dll": self.old,
                   "new.dll": make_index([row(0x2000, 0x20, "aa"), row(0x2300, 0x20, "bb"),
                                          row(0x2100, 0x20, "zz"), row(0x2400, 0x20, "cc"),
                                          row(0x2500, 0x20, "dd"), row(0x2600, 0x20, "ee"),
                                          row(0x1E00, 0x20, "p1"), row(0x1F00, 0x20, "p2")],
                                         key="new-1111111111111111")}
        maps = {"old": (self.old_map, ""), "new": (self.new_map, "")}
        with tempfile.TemporaryDirectory() as tmp:
            for name in indexes:
                (Path(tmp) / name).write_bytes(b"x")
            args = [a.replace("old.dll", str(Path(tmp) / "old.dll")).replace("new.dll", str(Path(tmp) / "new.dll"))
                    for a in argv]
            by_path = {str(Path(tmp) / n): v for n, v in indexes.items()}
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = bm.main(args, index_loader=lambda dll, d, use: by_path[str(dll)],
                               map_loader=lambda dll, d: maps["old" if "old" in Path(dll).name else "new"])
        return code, out.getvalue()

    def test_cli_oracle_exits_1_on_a_wrong_match(self):
        code, out = self.run_main(["--old", "old.dll", "--new", "new.dll", "--oracle"])
        self.assertEqual(code, 1)
        self.assertIn("WRONG IMBAgent.Bar", out)
        self.assertIn("  primary: correct", out)

    def test_cli_rva_prints_a_row(self):
        code, out = self.run_main(["--old", "old.dll", "--new", "new.dll", "--rva", "0x1104"])
        self.assertEqual(code, 0)
        self.assertIn("0x1104  primary 0x1100+0x4 (0x20)  MATCHED 0x2300+0x4 (hash)", out)

    def test_cli_oracle_without_a_map_exits_2(self):
        def no_map(dll, cache_dir):
            raise bm.ToolError("no engine-method map")
        with tempfile.TemporaryDirectory() as tmp:
            dll = Path(tmp) / "x.dll"
            dll.write_bytes(b"x")
            err = io.StringIO()
            with contextlib.redirect_stderr(err), contextlib.redirect_stdout(io.StringIO()):
                code = bm.main(["--old", str(dll), "--new", str(dll), "--oracle"],
                               index_loader=lambda d, c, u: self.old, map_loader=no_map)
        self.assertEqual(code, 2)
        self.assertIn("ERROR: no engine-method map", err.getvalue())


class MapFallbackTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dll = Path(self.tmp.name) / "bin" / "TaleWorlds.Native.dll"
        self.dll.parent.mkdir()
        self.dll.write_bytes(b"MZ native bytes")
        self.cache = Path(self.tmp.name) / "cache"
        self.cache.mkdir()
        self.sha = bm.nd.cache_key(self.dll).rpartition("-")[2]

    def write(self, name, fmt=None, problems=(), rva=0x1000, native=None):
        data = {"format": nem.FORMAT if fmt is None else fmt, "native": native or name.split(".")[0],
                "managed": {}, "problems": list(problems),
                "methods": [["MountAndBlade", 1, "IMBAgent", "Foo", "foo", rva]]}
        (self.cache / name).write_text(json.dumps(data), encoding="utf-8")

    def test_picks_the_file_for_this_binary(self):
        self.write("a-ffffffffffffffff.engine-methods.json", rva=0x9999)
        self.write(f"b-{self.sha}.engine-methods.json", rva=0x1234)
        engine_map, note = bm.engine_map_for(self.dll, self.cache)
        self.assertEqual([r.rva for r in engine_map.methods], [0x1234])
        self.assertEqual(note, "(cached map, managed hashes unchecked)")

    def test_rejects_format_problems_and_a_mismatched_native_key(self):
        self.write(f"a-{self.sha}.engine-methods.json", fmt=nem.FORMAT + 1)
        self.write(f"b-{self.sha}.engine-methods.json", problems=["x"])
        self.write(f"c-{self.sha}.engine-methods.json", native="c-ffffffffffffffff")
        with self.assertRaises(bm.ToolError):
            bm.engine_map_for(self.dll, self.cache)

    def test_no_map_at_all_is_a_tool_error(self):
        with self.assertRaises(bm.ToolError):
            bm.engine_map_for(self.dll, self.cache)


class CliInputTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir = Path(self.tmp.name)

    def run_main(self, argv, **kw):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            try:
                code = bm.main(argv, **kw)
            except SystemExit as exc:
                code = exc.code
        return code, out.getvalue(), err.getvalue()

    def pe_file(self, name, **kw):
        path = self.dir / name
        path.write_bytes(make_pe(SYNTH_TEXT, SYNTH_ENTRIES, SYNTH_UNWIND, **kw))
        return str(path)

    def args(self, old, new=None, *more):
        return ["--old", old, "--new", new or old, "--cache-dir", str(self.dir / "cache"), "--no-cache", *more]

    def test_missing_file_exits_2(self):
        code, _, err = self.run_main(self.args(str(self.dir / "nope.dll"), None, "--rva", "0x1000"))
        self.assertEqual(code, 2)
        self.assertIn("ERROR:", err)

    def test_non_pe_exits_2(self):
        bad = self.dir / "bad.dll"
        bad.write_bytes(b"hello")
        self.assertEqual(self.run_main(self.args(str(bad), None, "--rva", "0x1000"))[0], 2)

    def test_truncated_pe_exits_2(self):
        bad = self.dir / "cut.dll"
        bad.write_bytes(b"MZ" + b"\x00" * 10)
        code, _, err = self.run_main(self.args(str(bad), None, "--rva", "0x1000"))
        self.assertEqual(code, 2)
        self.assertIn("ERROR:", err)

    def test_no_pdata_exits_2(self):
        path = self.pe_file("nopdata.dll", with_pdata=False)
        code, _, err = self.run_main(self.args(path, None, "--rva", "0x1000"))
        self.assertEqual(code, 2)
        self.assertIn(".pdata", err)

    def test_missing_capstone_exits_2(self):
        path = self.pe_file("ok.dll")
        with mock.patch.dict(sys.modules, {"capstone": None}):
            code, _, err = self.run_main(self.args(path, None, "--rva", "0x1000"))
        self.assertEqual(code, 2)
        self.assertIn("capstone", err)

    def test_argparse_needs_exactly_one_mode(self):
        path = self.pe_file("ok.dll")
        self.assertEqual(self.run_main(self.args(path))[0], 2)
        self.assertEqual(self.run_main(self.args(path, None, "--rva", "0x1000", "--oracle"))[0], 2)

    @unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
    def test_rva_outside_text_exits_2(self):
        path = self.pe_file("ok.dll")
        code, _, err = self.run_main(self.args(path, None, "--rva", "0x9000"))
        self.assertEqual(code, 2)
        self.assertIn(".text", err)

    @unittest.skipUnless(HAVE_CAPSTONE, "capstone not installed")
    def test_synthetic_image_end_to_end(self):
        path = self.pe_file("ok.dll")
        code, out, _ = self.run_main(self.args(path, None, "--rva", "0x1002", "--rva", "0x1012",
                                               "--rva", "0x1008"))
        self.assertEqual(code, 0)
        self.assertIn("0x1002  primary 0x1000+0x2 (0x6)  MATCHED 0x1000+0x2 (hash)", out)
        self.assertIn("0x1012  leaf 0x1010+0x2 (0x3, bounds from padding)  MATCHED 0x1010+0x2 (hash)", out)
        self.assertIn("0x1008  padding: no function  UNKNOWN", out)
        self.assertIn("primary 1", out)
        self.assertIn("leaf 1", out)


ARCHIVE = Path(r"E:\Decompiled_Bannerlord\_native")


def archived(version):
    return ARCHIVE / f"TaleWorlds.Native.v{version}.client.dll"


@unittest.skipUnless(os.environ.get("TAOM_NATIVE_MATCH_IT") == "1" and HAVE_CAPSTONE,
                     "opt-in: TAOM_NATIVE_MATCH_IT=1 and capstone")
class IntegrationTests(unittest.TestCase):
    """The oracle on the real archived builds. The first run builds and caches each index
    (about 35 s per DLL) and reads the cached engine-method maps in the Ghidra project folder."""

    def run_oracle(self, old_version, new_version=None, new=None):
        old = archived(old_version)
        new = new or archived(new_version)
        for dll in (old, new):
            if not dll.is_file():
                self.skipTest(f"{dll} is not on this machine")
        cache = bm.nd.DEFAULT_PROJECT_DIR
        try:
            maps = [bm.engine_map_for(dll, cache)[0] for dll in (old, new)]
        except bm.ToolError as exc:
            self.skipTest(str(exc))
        indexes = [bm.load_index(dll, cache, True) for dll in (old, new)]
        results = bm.match(indexes[0].rows, indexes[1].rows)
        return bm.oracle(maps[0], maps[1], indexes[0], results)

    def test_v154_to_v155_has_no_wrong_match(self):
        report = self.run_oracle("1.5.4", "1.5.5")
        self.assertEqual(report.wrong, [])
        self.assertGreaterEqual(report.correct_hash + report.correct_strings, 1700)

    def test_v153_to_v154_has_no_wrong_match(self):
        self.assertEqual(self.run_oracle("1.5.3", "1.5.4").wrong, [])

    def test_installed_client_against_installed_weditor_is_a_negative_control(self):
        """The installed client and wEditor builds of one version (another compile, where neighbour code
        collides): the carry may say AMBIGUOUS or UNKNOWN, never a wrong address."""
        game = os.environ.get("BANNERLORD_GAME_DIR", "").strip()
        if not game:
            self.skipTest("BANNERLORD_GAME_DIR is not set")
        client = Path(game, "bin", "Win64_Shipping_Client", "TaleWorlds.Native.dll")
        weditor = Path(game, "bin", "Win64_Shipping_wEditor", "TaleWorlds.Native.dll")
        for dll in (client, weditor):
            if not dll.is_file():
                self.skipTest(f"{dll} is not on this machine")
        with tempfile.TemporaryDirectory() as cache:
            maps = [bm.engine_map_for(dll, cache)[0] for dll in (client, weditor)]
            indexes = [bm.load_index(dll, cache, False) for dll in (client, weditor)]
        results = bm.match(indexes[0].rows, indexes[1].rows)
        report = bm.oracle(maps[0], maps[1], indexes[0], results)
        self.assertEqual(report.wrong, [])
        self.assertGreater(report.correct_hash + report.correct_strings, 100)


if __name__ == "__main__":
    unittest.main()
