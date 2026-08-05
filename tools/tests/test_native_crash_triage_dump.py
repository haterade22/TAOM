#!/usr/bin/env python3
"""Unit tests for native_crash_triage.py --dump mode (minidump parsing).

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_native_crash_triage_dump.py

Pure stdlib. Builds SYNTHETIC minidumps (MDMP header + stream directory + streams
3 ThreadList / 4 ModuleList / 5 MemoryList / 6 Exception / 16 MemoryInfoList) in the
exact struct layouts recorded from the real TW CrashDumper dump (build 117484,
prototype-validated 2026-08-05):
  - MINIDUMP_MODULE stride 108 (BaseOfImage u64 @0, SizeOfImage u32 @8, NameRva u32 @20)
  - thread entry 48 bytes <IIIIQQIIII; CONTEXT_AMD64 Rsp @0x98 / Rip @0xF8
  - per-thread stack descriptor Rva=0 -> stack bytes located via MemoryList (observed
    TW CrashDumper behavior; the fallback under test)
  - MemoryInfoList header <IIQ + 48-byte <QQIIQIIII entries
No real dump or game install needed. The chain test also builds a minimal PE
(.text/.rdata/.pdata) so the --dump -> RVA-pipeline handoff runs hermetically.

Contract under test:
  - module resolution + RVA math (sorted module list, end-exclusive bounds)
  - exception decode incl. parameters (AV read/write decode)
  - commit summary bucketing (only MEM_COMMIT counted; image/private/mapped split)
  - Rva-0 per-thread stack fallback via MemoryList; per-thread capture primary path
  - graceful handling of a dump with no MemoryInfoList (prints 'no MemoryInfoList')
  - --dump chains into the existing RVA pipeline when basenames match; hint otherwise
  - --rva CLI output unchanged by the refactor
"""
import os
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import native_crash_triage as nct  # noqa: E402

TOOL = Path(__file__).resolve().parent.parent / "native_crash_triage.py"

MOD_BASE = 0x7FFD00000000
MOD_SIZE = 0x600000
MOD_NAME = r"C:\Games\Fake\Other.dll"
NTDLL_BASE = 0x7FFE00000000
NTDLL_NAME = r"C:\Windows\System32\ntdll.dll"
FAULT_RVA = 0x58232C
TID = 5560
RSP = 0x14F000
GB = 1024 ** 3

MEM_COMMIT, MEM_RESERVE, MEM_FREE = 0x1000, 0x2000, 0x10000
MEM_IMAGE, MEM_MAPPED, MEM_PRIVATE = 0x1000000, 0x40000, 0x20000


def _mdstring(text):
    b = text.encode("utf-16-le")
    return struct.pack("<I", len(b)) + b + b"\x00\x00"


class DumpBuilder:
    """Minimal MDMP writer: 16-byte header + payload chunks + trailing stream directory.

    The directory sits at the END of the file (its rva is in the header), so payload
    chunks can be appended freely and referenced by rva as they are added.
    """

    def __init__(self):
        self.buf = bytearray(16)  # header patched in write()
        self.dirs = []

    def add(self, blob):
        rva = len(self.buf)
        self.buf += blob
        return rva

    def add_stream(self, stype, body):
        rva = self.add(body)
        self.dirs.append((stype, len(body), rva))

    def add_modules(self, mods):
        entries = b""
        for base, size, name in mods:
            name_rva = self.add(_mdstring(name))
            entries += (struct.pack("<QI", base, size) + b"\x00" * 8
                        + struct.pack("<I", name_rva) + b"\x00" * 84)
        self.add_stream(4, struct.pack("<I", len(mods)) + entries)

    def add_exception(self, tid, code, addr, params):
        padded = list(params) + [0] * (15 - len(params))
        rec = (struct.pack("<IIQQII", code, 0, 0, addr, len(params), 0)
               + struct.pack("<15Q", *padded))
        self.add_stream(6, struct.pack("<II", tid, 0) + rec)

    def _context(self, rsp, rip):
        cx = bytearray(0x100)
        struct.pack_into("<Q", cx, 0x98, rsp)
        struct.pack_into("<Q", cx, 0xF8, rip)
        return bytes(cx)

    def add_threads(self, threads):
        entries = b""
        for t in threads:
            cx_rva = self.add(self._context(t["rsp"], t.get("rip", 0)))
            entries += struct.pack(
                "<IIIIQQIIII", t["tid"], 0, 0, 0, 0,
                t.get("stack_start", 0), t.get("stack_size", 0),
                t.get("stack_rva", 0), 0x100, cx_rva)
        self.add_stream(3, struct.pack("<I", len(threads)) + entries)

    def add_memory_list(self, regions):
        entries = b""
        for start, data in regions:
            drva = self.add(data)
            entries += struct.pack("<QII", start, len(data), drva)
        self.add_stream(5, struct.pack("<I", len(regions)) + entries)

    def add_memory_info(self, infos):
        body = struct.pack("<IIQ", 16, 48, len(infos))
        for size, state, mtype in infos:
            body += struct.pack("<QQIIQIIII", 0, 0, 0, 0, size, state, 0, mtype, 0)
        self.add_stream(16, body)

    def write(self, path):
        dirrva = len(self.buf)
        for d in self.dirs:
            self.buf += struct.pack("<III", *d)
        struct.pack_into("<4sIII", self.buf, 0, b"MDMP", 42899, len(self.dirs), dirrva)
        Path(path).write_bytes(bytes(self.buf))


def _stack_data():
    """8 KB stack region; return addresses planted at rsp (offset 0x1000 into region)."""
    data = bytearray(0x2000)
    struct.pack_into("<QQQQ", data, 0x1000,
                     0x1111,                    # garbage, not in any module
                     MOD_BASE + 0x57FC75,       # hit: Other.dll+0x57FC75
                     0xDEADBEEF,                # garbage
                     NTDLL_BASE + 0x1234)       # hit: ntdll.dll+0x1234
    return bytes(data)


def build_canonical(path, with_meminfo=True):
    """The canonical synthetic dump used across unit + CLI tests.

    Thread 5560 (the exception thread) has stack Rva=0 -> MemoryList fallback.
    Thread 777 has a real per-thread stack capture -> primary path.
    """
    b = DumpBuilder()
    b.add_modules([(MOD_BASE, MOD_SIZE, MOD_NAME),
                   (NTDLL_BASE, 0x10000, NTDLL_NAME)])
    b.add_exception(TID, 0xC0000005, MOD_BASE + FAULT_RVA, (0, 0x24C))
    direct = bytearray(0x100)
    struct.pack_into("<Q", direct, 0x10, MOD_BASE + 0x9999)
    direct_rva = b.add(bytes(direct))
    b.add_threads([
        {"tid": TID, "rsp": RSP, "rip": MOD_BASE + FAULT_RVA,
         "stack_start": 0x14E000, "stack_size": 0x2000, "stack_rva": 0},
        {"tid": 777, "rsp": 0x24F010, "stack_start": 0x24F000,
         "stack_size": 0x100, "stack_rva": direct_rva},
    ])
    b.add_memory_list([(0x14E000, _stack_data())])
    if with_meminfo:
        b.add_memory_info([
            (1 * GB, MEM_COMMIT, MEM_IMAGE),
            (2 * GB, MEM_COMMIT, MEM_PRIVATE),
            (GB // 2, MEM_COMMIT, MEM_MAPPED),
            (0x8000, MEM_RESERVE, MEM_PRIVATE),   # excluded: not committed
            (0x10000, MEM_FREE, 0),               # excluded: not committed
        ])
    b.write(path)


def _build_pe():
    """Minimal PE64 the Pe class can parse: .text/.rdata/.pdata, one crash function
    (0x1040..0x1080) whose LEA references 'monster_usage.cpp', one caller (0x10A0..0x10C0)
    with an E8 call at 0x10A4 targeting the crash function."""
    d = bytearray(0x618)
    d[0:2] = b"MZ"
    struct.pack_into("<I", d, 0x3C, 0x80)
    d[0x80:0x84] = b"PE\x00\x00"
    coff = 0x84
    struct.pack_into("<HH", d, coff, 0x8664, 3)     # machine, NumberOfSections
    struct.pack_into("<H", d, coff + 16, 0)         # SizeOfOptionalHeader = 0
    sec = coff + 20

    def put_sec(i, name, vaddr, vsize, roff, rsize):
        off = sec + i * 40
        d[off:off + 8] = name.ljust(8, b"\x00")
        struct.pack_into("<IIII", d, off + 8, vsize, vaddr, rsize, roff)

    put_sec(0, b".text", 0x1000, 0x200, 0x200, 0x200)
    put_sec(1, b".rdata", 0x2000, 0x100, 0x400, 0x100)
    put_sec(2, b".pdata", 0x3000, 0x18, 0x600, 0x18)

    # crash function: LEA rax,[rip+disp] at rva 0x1048 -> string at rva 0x2000
    d[0x248:0x24B] = b"\x48\x8D\x05"
    struct.pack_into("<i", d, 0x24B, 0x2000 - (0x1048 + 7))
    d[0x250:0x252] = b"\x8B\x08"                    # crash instruction (mov ecx,[rax])
    # caller: E8 rel32 at rva 0x10A4 -> 0x1040
    d[0x2A4] = 0xE8
    struct.pack_into("<i", d, 0x2A5, 0x1040 - (0x10A4 + 5))

    d[0x400:0x412] = b"monster_usage.cpp\x00"

    struct.pack_into("<III", d, 0x600, 0x1040, 0x1080, 0)
    struct.pack_into("<III", d, 0x60C, 0x10A0, 0x10C0, 0)
    return bytes(d)


# --------------------------------------------------------------------------- #
# Minidump class (direct)                                                      #
# --------------------------------------------------------------------------- #
class MinidumpParseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        cls.dump_path = Path(cls._tmp.name) / "canon.dmp"
        build_canonical(cls.dump_path)
        cls.md = nct.Minidump(str(cls.dump_path))

    @classmethod
    def tearDownClass(cls):
        cls.md.close()
        cls._tmp.cleanup()

    def test_module_resolution_and_rva_math(self):
        self.assertEqual(self.md.module_of(MOD_BASE + 0x1234), (MOD_NAME, 0x1234))
        self.assertEqual(self.md.module_of(MOD_BASE), (MOD_NAME, 0))
        self.assertIsNone(self.md.module_of(MOD_BASE - 1))
        self.assertIsNone(self.md.module_of(MOD_BASE + MOD_SIZE))  # end-exclusive

    def test_second_module_resolves_via_sorted_bases(self):
        self.assertEqual(self.md.module_of(NTDLL_BASE + 0x50), (NTDLL_NAME, 0x50))
        self.assertIsNone(self.md.module_of(0))
        self.assertIsNone(self.md.module_of(MOD_BASE + MOD_SIZE + 0x1000))  # gap

    def test_exception_decode_includes_parameters(self):
        tid, code, addr, params = self.md.exception()
        self.assertEqual(tid, TID)
        self.assertEqual(code, 0xC0000005)
        self.assertEqual(addr, MOD_BASE + FAULT_RVA)
        self.assertEqual(tuple(params), (0, 0x24C))

    def test_commit_summary_buckets_only_committed_regions(self):
        nent, tot, img, priv, mapped = self.md.commit_summary()
        self.assertEqual(nent, 5)
        self.assertEqual(img, 1 * GB)
        self.assertEqual(priv, 2 * GB)
        self.assertEqual(mapped, GB // 2)
        self.assertEqual(tot, 3 * GB + GB // 2)  # reserve + free excluded

    def test_thread_rsp_reads_context_registers(self):
        rsp, rip, s_start, s_size, s_rva = self.md.thread_rsp(TID)
        self.assertEqual(rsp, RSP)
        self.assertEqual(rip, MOD_BASE + FAULT_RVA)
        self.assertEqual((s_start, s_size, s_rva), (0x14E000, 0x2000, 0))
        self.assertIsNone(self.md.thread_rsp(999))

    def test_stack_scan_rva0_falls_back_to_memory_list(self):
        rsp, _rip, s_start, s_size, s_rva = self.md.thread_rsp(TID)
        hits, source = self.md.stack_scan(rsp, s_start, s_size, s_rva)
        self.assertIn("MemoryList", source)
        self.assertEqual(len(hits), 2)  # garbage qwords + zeros are not module hits
        self.assertEqual(hits[0], (8, MOD_BASE + 0x57FC75, MOD_NAME, 0x57FC75))
        self.assertEqual(hits[1], (24, NTDLL_BASE + 0x1234, NTDLL_NAME, 0x1234))

    def test_stack_scan_uses_per_thread_capture_when_rva_nonzero(self):
        rsp, _rip, s_start, s_size, s_rva = self.md.thread_rsp(777)
        self.assertNotEqual(s_rva, 0)
        hits, source = self.md.stack_scan(rsp, s_start, s_size, s_rva)
        self.assertIn("per-thread", source)
        self.assertEqual(hits, [(0, MOD_BASE + 0x9999, MOD_NAME, 0x9999)])

    def test_commit_summary_none_when_stream_absent(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "nomem.dmp"
            build_canonical(p, with_meminfo=False)
            with nct.Minidump(str(p)) as md:
                self.assertIsNone(md.commit_summary())

    def test_rejects_non_minidump_file(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "garbage.dmp"
            p.write_bytes(b"GARBAGE not a dump" * 4)
            with self.assertRaises(ValueError):
                nct.Minidump(str(p))


# --------------------------------------------------------------------------- #
# CLI --dump                                                                   #
# --------------------------------------------------------------------------- #
class DumpCliTests(unittest.TestCase):
    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_dump_reports_exception_module_commit_stack_and_hint(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "canon.dmp"
            build_canonical(p)
            r = self._run(["--dump", str(p)])
            out = r.stdout
            self.assertEqual(r.returncode, 0, out + r.stderr)
            self.assertIn("exception: code 0xC0000005", out)
            self.assertIn("thread 5560", out)
            self.assertIn("parameters: [0x0, 0x24C]", out)
            self.assertIn("(read of 0x24C)", out)
            self.assertIn("faulting module: Other.dll+0x58232C", out)
            self.assertIn(
                "commit: 3.50 GB total (image 1.00 / private 2.00 / mapped 0.50)", out)
            self.assertIn("MemoryList fallback", out)
            self.assertIn("rsp+0x0008: 0x00007FFD0057FC75  Other.dll+0x57FC75", out)
            self.assertIn("rsp+0x0018: 0x00007FFE00001234  ntdll.dll+0x1234", out)
            # faulting module is NOT the --dll module -> hint, no disassembly chained
            self.assertIn("--rva 0x58232C", out)
            self.assertNotIn("crash function:", out)

    def test_dump_without_meminfo_prints_note_and_exits_zero(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "nomem.dmp"
            build_canonical(p, with_meminfo=False)
            r = self._run(["--dump", str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("no MemoryInfoList", r.stdout)
            self.assertIn("faulting module: Other.dll+0x58232C", r.stdout)

    def test_dump_chains_into_rva_pipeline_when_module_matches_dll(self):
        base = 0x7FFD30000000
        with tempfile.TemporaryDirectory() as d:
            pe_path = Path(d) / "TaleWorlds.Native.dll"
            pe_path.write_bytes(_build_pe())
            dmp = Path(d) / "bundle.dmp"
            b = DumpBuilder()
            b.add_modules([(base, 0x4000, r"D:\PlayerPC\bin\TaleWorlds.Native.dll")])
            b.add_exception(42, 0xC0000005, base + 0x1050, (0, 0))
            b.write(dmp)
            r = self._run(["--dump", str(dmp), "--dll", str(pe_path)])
            out = r.stdout
            self.assertEqual(r.returncode, 0, out + r.stderr)
            self.assertIn("faulting module: TaleWorlds.Native.dll+0x1050", out)
            self.assertIn(
                "crash function: 0x1040 .. 0x1080 (size 0x40, crash at +0x10)", out)
            self.assertIn("'monster_usage.cpp'", out)
            self.assertIn("L1 callers of 0x1040: 1 site(s)", out)

    def test_dump_missing_file_exits_nonzero(self):
        with tempfile.TemporaryDirectory() as d:
            r = self._run(["--dump", str(Path(d) / "absent.dmp")])
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("dump not found", r.stderr)


# --------------------------------------------------------------------------- #
# --rva CLI regression (the refactor must not change existing output)          #
# --------------------------------------------------------------------------- #
class RvaCliRegressionTests(unittest.TestCase):
    def test_rva_cli_output_shape_unchanged(self):
        with tempfile.TemporaryDirectory() as d:
            pe_path = Path(d) / "fake.dll"
            pe_path.write_bytes(_build_pe())
            r = subprocess.run(
                [sys.executable, str(TOOL), "--rva", "0x1050", "--dll", str(pe_path)],
                capture_output=True, text=True)
            out = r.stdout
            self.assertEqual(r.returncode, 0, out + r.stderr)
            self.assertTrue(out.startswith(f"module: {pe_path} (1,560 bytes)"), out)
            self.assertIn(
                "crash function: 0x1040 .. 0x1080 (size 0x40, crash at +0x10)", out)
            self.assertIn("strings referenced in crash function (1):", out)
            self.assertIn("  'monster_usage.cpp'", out)
            self.assertIn("L1 callers of 0x1040: 1 site(s)", out)
            self.assertNotIn("dump:", out)
            self.assertNotIn("exception:", out)


class MalformedDumpTests(unittest.TestCase):
    """Truncated/corrupt dumps must exit with a message, never a traceback —
    struct.error is NOT a ValueError subclass (deep-review 2026-08-05)."""

    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def test_truncated_directory_exits_with_message_not_traceback(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "trunc.dmp"
            # Valid signature, directory claims 5 streams at rva 32 — file ends first.
            p.write_bytes(struct.pack("<4sIII", b"MDMP", 42899, 5, 32) + b"\x00" * 24)
            r = self._run(["--dump", str(p)])
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("malformed or truncated minidump", r.stdout + r.stderr)
            self.assertNotIn("Traceback", r.stdout + r.stderr)

    def test_truncated_tail_exits_with_message_not_traceback(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "cut.dmp"
            build_canonical(p)
            data = p.read_bytes()
            p.write_bytes(data[:len(data) - 100])  # directory is trailing — this corrupts it
            r = self._run(["--dump", str(p)])
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("malformed or truncated minidump", r.stdout + r.stderr)
            self.assertNotIn("Traceback", r.stdout + r.stderr)

    def test_commit_summary_none_for_undersized_szentry(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "smallentry.dmp"
            b = DumpBuilder()
            b.add_modules([(MOD_BASE, MOD_SIZE, MOD_NAME)])
            # MemoryInfoList header claiming 16-byte entries (< the 48 the unpack needs)
            b.add_stream(16, struct.pack("<IIQ", 16, 16, 3) + b"\x00" * 48)
            b.write(p)
            md = nct.Minidump(str(p))
            try:
                self.assertIsNone(md.commit_summary())
            finally:
                md.close()


class GracefulDegradationTests(unittest.TestCase):
    """The five named degrade-don't-crash paths, each pinned (deep-review 2026-08-05)."""

    def _run(self, args):
        return subprocess.run([sys.executable, str(TOOL), *args],
                              capture_output=True, text=True)

    def _base_builder(self, tid=TID, exc_addr=MOD_BASE + FAULT_RVA, with_exception=True,
                      with_memory_list=True, threads=None):
        b = DumpBuilder()
        b.add_modules([(MOD_BASE, MOD_SIZE, MOD_NAME)])
        if with_exception:
            b.add_exception(tid, 0xC0000005, exc_addr, (0, 0x24C))
        b.add_threads(threads if threads is not None else [
            {"tid": TID, "rsp": RSP, "rip": MOD_BASE + FAULT_RVA,
             "stack_start": 0x14E000, "stack_size": 0x2000, "stack_rva": 0}])
        if with_memory_list:
            b.add_memory_list([(0x14E000, _stack_data())])
        return b

    def test_no_exception_stream_exits_with_named_message(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "noexc.dmp"
            self._base_builder(with_exception=False).write(p)
            r = self._run(["--dump", str(p)])
            self.assertNotEqual(r.returncode, 0)
            self.assertIn("no Exception stream", r.stdout + r.stderr)
            self.assertNotIn("Traceback", r.stdout + r.stderr)

    def test_thread_not_in_threadlist_prints_note_and_continues(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "nothread.dmp"
            self._base_builder(tid=9999).write(p)
            r = self._run(["--dump", str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("thread 9999 not found in ThreadList", r.stdout)

    def test_fault_address_outside_all_modules_prints_note(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "nomod.dmp"
            self._base_builder(exc_addr=0x1000).write(p)
            r = self._run(["--dump", str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("is not inside any module", r.stdout)

    def test_memory_list_absent_prints_no_memorylist_source(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "nomemlist.dmp"
            self._base_builder(with_memory_list=False).write(p)
            r = self._run(["--dump", str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("no MemoryList stream", r.stdout)

    def test_rsp_outside_all_memory_list_ranges_degrades(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "rspout.dmp"
            self._base_builder(threads=[
                {"tid": TID, "rsp": 0x999000, "rip": MOD_BASE + FAULT_RVA,
                 "stack_start": 0x998000, "stack_size": 0x2000, "stack_rva": 0}]).write(p)
            r = self._run(["--dump", str(p)])
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            self.assertIn("rsp not inside any MemoryList range", r.stdout)


if __name__ == "__main__":
    unittest.main(verbosity=2)
