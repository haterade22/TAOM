#!/usr/bin/env python3
"""Offline forensics for native Bannerlord crashes (TaleWorlds.Native.dll and friends).

Given a crash RVA (or a runtime IP + module base), names the crash site without symbols:
  - exact function bounds from the PE .pdata (RUNTIME_FUNCTION) table
  - annotated hexdump around the crash instruction
  - every string the function references via rip-relative LEA (shipping builds keep
    assert/trace strings - this is how Agent_ai::set_attack_entity and monster_usage.cpp
    were named during the 2026-06-12 v1.4.6 spider campaign)
  - direct callers (E8 rel32 scan) with THEIR referenced strings, one level up

With --dump, decodes a TW CrashDumper minidump (player crash bundles - no Event Log
needed): exception code/thread/parameters, faulting module + RVA via the module list,
commit summary from MemoryInfoList, and a return-address scan of the faulting thread's
stack (located through MemoryList when the per-thread descriptor Rva is 0, which is
what TW's CrashDumper writes). When the faulting module's basename matches --dll it
chains straight into the RVA pipeline above; --callers applies there too.

Born from the v1.4.6 spider campaign (3 crash sites root-caused in one day); the full
protocol that drives this tool (Event Log offsets, debugger setup, Immediate probes) is
.claude/skills/native-crash-triage/SKILL.md.

Usage:
  python tools/native_crash_triage.py --rva 0x634396
  python tools/native_crash_triage.py --ip 0x7FFD8B65E0C9 --base 0x7FFD8B060000
  python tools/native_crash_triage.py --rva 0x5FE0C9 --dll "<path-to-native-dll>" --callers 2
  python tools/native_crash_triage.py --dump "<bundle>/dump.dmp" --callers 2
"""
import argparse
import bisect
import os
import re
import struct
import sys

DEFAULT_DLL = os.environ.get(
    "BANNERLORD_GAME_DIR",
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord",
) + r"\bin\Win64_Shipping_Client\TaleWorlds.Native.dll"


class Pe:
    def __init__(self, path):
        self.path = path
        self.d = open(path, "rb").read()
        d = self.d
        if d[:2] != b"MZ":
            raise ValueError(f"{path}: not a PE file")
        e_lfanew = struct.unpack_from("<I", d, 0x3C)[0]
        coff = e_lfanew + 4
        nsec = struct.unpack_from("<HH", d, coff)[1]
        opt_size = struct.unpack_from("<H", d, coff + 16)[0]
        sec_off = coff + 20 + opt_size
        self.secs = []
        for i in range(nsec):
            name = d[sec_off + i * 40: sec_off + i * 40 + 8].rstrip(b"\x00").decode()
            vsize, vaddr, rsize, roff = struct.unpack_from("<IIII", d, sec_off + i * 40 + 8)
            self.secs.append((name, vaddr, vsize, roff, rsize))
        self.text = next(s for s in self.secs if s[0] == ".text")
        self.pdata = next((s for s in self.secs if s[0] == ".pdata"), None)

    def rva_to_off(self, rva):
        for name, vaddr, vsize, roff, rsize in self.secs:
            if vaddr <= rva < vaddr + max(vsize, rsize):
                return roff + (rva - vaddr)
        return None

    def off_to_rva(self, off):
        for name, vaddr, vsize, roff, rsize in self.secs:
            if roff <= off < roff + rsize:
                return vaddr + (off - roff)
        return None

    def func_of(self, rva):
        """Exact function bounds from .pdata RUNTIME_FUNCTION entries."""
        if not self.pdata:
            return None, None
        po, psz = self.pdata[3], self.pdata[4]
        for i in range(0, psz - 11, 12):
            s_rva, e_rva, _ = struct.unpack_from("<III", self.d, po + i)
            if s_rva <= rva < e_rva:
                return s_rva, e_rva
        return None, None

    def strings_in(self, s_rva, e_rva, cap=30):
        """ASCII strings referenced via rip-relative LEA (48/4C 8D modrm=rip+disp32)."""
        out = []
        so, eo = self.rva_to_off(s_rva), self.rva_to_off(e_rva)
        if so is None or eo is None:
            return out
        for m in re.finditer(rb"[\x48\x4C]\x8D[\x05\x0D\x15\x1D\x25\x2D\x35\x3D]", self.d[so:eo]):
            ins = so + m.start()
            disp = struct.unpack_from("<i", self.d, ins + 3)[0]
            target = self.off_to_rva(ins) + 7 + disp
            toff = self.rva_to_off(target)
            if toff is None:
                continue
            raw = self.d[toff: toff + 160]
            end = raw.find(b"\x00")
            if end <= 3:
                continue
            s = raw[:end]
            if all(32 <= b < 127 for b in s) and s.decode() not in out:
                out.append(s.decode())
            if len(out) >= cap:
                break
        return out

    def callers_of(self, target_rva):
        """Direct call sites: every E8 rel32 in .text whose target == target_rva."""
        out = []
        t_start, t_end = self.text[3], self.text[3] + self.text[4]
        for m in re.finditer(rb"\xE8", self.d[t_start:t_end]):
            off = t_start + m.start()
            rel = struct.unpack_from("<i", self.d, off + 1)[0]
            src = self.off_to_rva(off)
            if src is not None and (src + 5 + rel) == target_rva:
                out.append(src)
        return out


def hexdump(pe, rva, before=0x50, after=0x30):
    fo = pe.rva_to_off(rva)
    lines = []
    start = fo - before
    for i in range(start, fo + after, 16):
        row_rva = pe.off_to_rva(i)
        hexs = " ".join(f"{b:02X}" for b in pe.d[i: i + 16])
        mark = "  <-- crash row" if i <= fo < i + 16 else ""
        lines.append(f"0x{row_rva:06X}: {hexs}{mark}")
    return "\n".join(lines)


# --------------------------------------------------------------------------- #
# Minidump (--dump mode)                                                       #
# --------------------------------------------------------------------------- #
EXCEPTION_CODES = {
    0xC0000005: "ACCESS_VIOLATION",
    0xC0000409: "STACK_BUFFER_OVERRUN / FailFast",
    0xC00000FD: "STACK_OVERFLOW",
    0xC0000374: "HEAP_CORRUPTION",
    0x80000003: "BREAKPOINT",
    0xE0434352: "CLR managed exception",
    0xE06D7363: "MSVC C++ exception",
}

MEM_COMMIT = 0x1000
MEM_IMAGE = 0x1000000
MEM_MAPPED = 0x40000
MEM_PRIVATE = 0x20000

MAX_STACK_ROWS = 64  # printed stack-scan hits (the full list is rarely useful past this)


def _basename(path):
    """Basename that handles Windows separators regardless of host OS (dump paths are \\)."""
    return path.replace("\\", "/").rsplit("/", 1)[-1]


class Minidump:
    """Minimal MDMP reader for the streams TW's CrashDumper writes.

    Ground truth from the real build-117484 dump (prototype validated 2026-08-05):
      - streams present: 3 ThreadList, 4 ModuleList, 5 MemoryList, 6 Exception,
        16 MemoryInfoList (+ others we don't need). NO Memory64List (type 9).
      - per-thread MINIDUMP_MEMORY_DESCRIPTOR.Memory.Rva can be 0 -> the stack bytes
        must be located by searching MemoryList ranges for RSP (stack_scan fallback).
      - MINIDUMP_MODULE stride is 108 bytes: BaseOfImage u64 @0, SizeOfImage u32 @8,
        ModuleNameRva u32 @20 -> MINIDUMP_STRING (u32 byte length + UTF-16LE).
      - thread entry is 48 bytes <IIIIQQIIII (Stack{Start u64, DataSize u32, Rva u32}
        then Context{DataSize u32, Rva u32}); CONTEXT_AMD64 Rsp @0x98, Rip @0xF8.
      - MemoryInfoList: header <IIQ (sizeOfHeader, sizeOfEntry, numberOfEntries),
        entries <QQIIQIIII (48 bytes).
      - MemoryList: u32 count, then 16-byte <QII entries.
    """

    THREAD_LIST, MODULE_LIST, MEMORY_LIST, EXCEPTION, MEMORY_INFO_LIST = 3, 4, 5, 6, 16

    def __init__(self, path):
        self.path = path
        self.f = open(path, "rb")
        hdr = self.f.read(16)
        if len(hdr) < 16 or hdr[:4] != b"MDMP":
            self.f.close()
            raise ValueError(f"{path}: not a minidump (no MDMP signature)")
        _sig, _ver, nstreams, dirrva = struct.unpack("<4sIII", hdr)
        self.f.seek(dirrva)
        self.dirs = [struct.unpack("<III", self.f.read(12)) for _ in range(nstreams)]
        self.modules = self._read_modules()  # sorted [(base, size, name)]
        self._bases = [m[0] for m in self.modules]

    def close(self):
        self.f.close()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
        return False

    def _stream(self, stype):
        for st, sz, rva in self.dirs:
            if st == stype:
                return sz, rva
        return None

    def _read_mdstring(self, rva):
        self.f.seek(rva)
        (ln,) = struct.unpack("<I", self.f.read(4))
        return self.f.read(ln).decode("utf-16-le", errors="replace")

    def _read_modules(self):
        st = self._stream(self.MODULE_LIST)
        if st is None:
            return []
        _sz, rva = st
        self.f.seek(rva)
        (n,) = struct.unpack("<I", self.f.read(4))
        raw = self.f.read(n * 108)
        mods = []
        for i in range(n):
            base, size = struct.unpack_from("<QI", raw, i * 108)
            name_rva = struct.unpack_from("<I", raw, i * 108 + 20)[0]
            mods.append((base, size, self._read_mdstring(name_rva)))
        mods.sort()
        return mods

    def module_of(self, addr):
        """(module name, rva) for an absolute address, or None if outside every module."""
        i = bisect.bisect_right(self._bases, addr) - 1
        if i >= 0:
            base, size, name = self.modules[i]
            if base <= addr < base + size:
                return name, addr - base
        return None

    def exception(self):
        """(thread id, code, faulting address, parameters) or None if no stream 6."""
        st = self._stream(self.EXCEPTION)
        if st is None:
            return None
        _sz, rva = st
        self.f.seek(rva)
        tid, _aln = struct.unpack("<II", self.f.read(8))
        rec = self.f.read(152)
        code, _flags, _chain, addr, nparams = struct.unpack_from("<IIQQI", rec, 0)
        params = struct.unpack_from("<15Q", rec, 32)[:min(nparams, 15)]
        return tid, code, addr, params

    def commit_summary(self):
        """(regions, total, image, private, mapped) committed bytes, or None if no stream 16."""
        st = self._stream(self.MEMORY_INFO_LIST)
        if st is None:
            return None
        _sz, rva = st
        self.f.seek(rva)
        szhdr, szentry, nent = struct.unpack("<IIQ", self.f.read(16))
        # Don't trust the header blindly: MINIDUMP_MEMORY_INFO is 48 bytes; a smaller
        # szentry would under-read the unpack below, and an absurd count means corruption.
        if szentry < 48 or nent > 100_000_000:
            return None
        self.f.seek(rva + szhdr)
        tot = img = priv = mapped = 0
        for _ in range(nent):
            e = self.f.read(szentry)
            if len(e) < 48:
                break
            (_b, _ab, _ap, _r1, regionsize, state, _prot,
             mtype, _r2) = struct.unpack_from("<QQIIQIIII", e, 0)
            if state == MEM_COMMIT:
                tot += regionsize
                if mtype == MEM_IMAGE:
                    img += regionsize
                elif mtype == MEM_PRIVATE:
                    priv += regionsize
                elif mtype == MEM_MAPPED:
                    mapped += regionsize
        return nent, tot, img, priv, mapped

    def thread_rsp(self, tid):
        """(rsp, rip, stack start, stack size, stack rva) for a thread, or None."""
        st = self._stream(self.THREAD_LIST)
        if st is None:
            return None
        _sz, rva = st
        self.f.seek(rva)
        (n,) = struct.unpack("<I", self.f.read(4))
        for _ in range(n):
            (t, _susp, _pc, _pr, _teb, s_start, s_size, s_rva,
             cx_size, cx_rva) = struct.unpack("<IIIIQQIIII", self.f.read(48))
            if t == tid:
                if cx_size < 0x100:
                    return None
                self.f.seek(cx_rva)
                cx = self.f.read(cx_size)
                rsp = struct.unpack_from("<Q", cx, 0x98)[0]
                rip = struct.unpack_from("<Q", cx, 0xF8)[0]
                return rsp, rip, s_start, s_size, s_rva
        return None

    def _stack_bytes(self, rsp, s_start, s_size, s_rva):
        """Captured stack bytes from RSP to the end of the capture, + source label."""
        if s_rva and s_start <= rsp < s_start + s_size:
            self.f.seek(s_rva + (rsp - s_start))
            return self.f.read(s_start + s_size - rsp), "per-thread stack capture"
        # TW CrashDumper writes Rva=0 descriptors; find the range in MemoryList (5).
        st = self._stream(self.MEMORY_LIST)
        if st is None:
            return b"", "no MemoryList stream"
        _sz, rva = st
        self.f.seek(rva)
        (n,) = struct.unpack("<I", self.f.read(4))
        entries = [struct.unpack("<QII", self.f.read(16)) for _ in range(n)]
        for start, dsize, drva in entries:
            if start <= rsp < start + dsize:
                self.f.seek(drva + (rsp - start))
                return (self.f.read(start + dsize - rsp),
                        "MemoryList fallback (thread stack Rva=0)")
        return b"", "rsp not inside any MemoryList range"

    def stack_scan(self, rsp, s_start=0, s_size=0, s_rva=0):
        """Return-address scan: every 8-aligned qword above RSP that lands in a module.

        Returns ([(rsp offset, value, module name, module rva), ...], source label).
        """
        data, source = self._stack_bytes(rsp, s_start, s_size, s_rva)
        hits = []
        for o in range(0, len(data) - 7, 8):
            (v,) = struct.unpack_from("<Q", data, o)
            m = self.module_of(v)
            if m:
                hits.append((o, v, m[0], m[1]))
        return hits, source


def run_rva(args):
    """The original pipeline: name the site inside --dll at --rva."""
    pe = Pe(args.dll)
    print(f"module: {args.dll} ({len(pe.d):,} bytes)")

    fs, fe = pe.func_of(args.rva)
    if fs is None:
        sys.exit(f"RVA 0x{args.rva:X} not inside any .pdata function - check the base/offset math")
    print(f"\ncrash function: 0x{fs:X} .. 0x{fe:X} (size 0x{fe - fs:X}, crash at +0x{args.rva - fs:X})")

    print("\nhexdump around crash:")
    print(hexdump(pe, args.rva))

    strs = pe.strings_in(fs, fe)
    print(f"\nstrings referenced in crash function ({len(strs)}):")
    for s in strs:
        print(f"  {s!r}")

    frontier = [(fs, fe)]
    for level in range(1, args.callers + 1):
        nxt = []
        for tfs, _ in frontier:
            calls = pe.callers_of(tfs)
            print(f"\nL{level} callers of 0x{tfs:X}: {len(calls)} site(s)")
            seen = set()
            for c in calls[:12]:
                cfs, cfe = pe.func_of(c)
                if cfs in seen or cfs is None:
                    continue
                seen.add(cfs)
                cstrs = pe.strings_in(cfs, cfe, cap=8)
                print(f"  call@0x{c:X}  func 0x{cfs:X}..0x{cfe:X}  strings: {cstrs}")
                nxt.append((cfs, cfe))
        frontier = nxt
        if not frontier:
            break


def run_dump(args):
    """Decode a TW CrashDumper minidump, then chain into run_rva when possible."""
    if not os.path.isfile(args.dump):
        sys.exit(f"dump not found: {args.dump}")
    try:
        md = Minidump(args.dump)
    except (ValueError, struct.error, OSError) as e:
        # struct.error is NOT a ValueError subclass - every truncated-file unpack raises it.
        sys.exit(f"malformed or truncated minidump ({args.dump}): {e}")
    try:
        print(f"dump: {args.dump} ({os.path.getsize(args.dump):,} bytes, "
              f"{len(md.modules)} modules)")

        exc = md.exception()
        if exc is None:
            sys.exit("no Exception stream (type 6) in this dump - nothing to triage")
        tid, code, addr, params = exc
        print(f"\nexception: code 0x{code:08X} "
              f"({EXCEPTION_CODES.get(code, 'unknown')})  thread {tid}")
        print(f"  faulting address: 0x{addr:X}")
        note = ""
        if code == 0xC0000005 and len(params) >= 2:
            kind = {0: "read", 1: "write", 8: "execute (DEP)"}.get(
                params[0], f"op{params[0]}")
            note = f"  ({kind} of 0x{params[1]:X})"
        print(f"  parameters: [{', '.join(f'0x{p:X}' for p in params)}]{note}")

        fault = md.module_of(addr)
        if fault:
            fname, frva = fault
            fbase = _basename(fname)
            print(f"\nfaulting module: {fbase}+0x{frva:X}")
            print(f"  (path in dump: {fname})")
        else:
            print(f"\nfaulting address 0x{addr:X} is not inside any module in the dump")

        cs = md.commit_summary()
        gb = 1024 ** 3
        if cs is None:
            print("\nno MemoryInfoList (stream 16) in this dump - commit summary unavailable")
        else:
            nent, tot, img, priv, mapped = cs
            print(f"\ncommit: {tot / gb:.2f} GB total "
                  f"(image {img / gb:.2f} / private {priv / gb:.2f} / mapped {mapped / gb:.2f}) "
                  f"across {nent} regions")

        t = md.thread_rsp(tid)
        if t is None:
            print(f"\nthread {tid} not found in ThreadList (stream 3) - no stack scan")
        else:
            rsp, _rip, s_start, s_size, s_rva = t
            hits, source = md.stack_scan(rsp, s_start, s_size, s_rva)
            print(f"\nstack scan of thread {tid} (rsp=0x{rsp:X}, {source}): "
                  f"{len(hits)} return-address candidate(s)")
            for off, val, mname, mrva in hits[:MAX_STACK_ROWS]:
                print(f"  rsp+0x{off:04X}: 0x{val:016X}  {_basename(mname)}+0x{mrva:X}")
            if len(hits) > MAX_STACK_ROWS:
                print(f"  ... (+{len(hits) - MAX_STACK_ROWS} more)")

        if fault:
            want = os.path.basename(args.dll)
            if fbase.lower() == want.lower():
                if os.path.isfile(args.dll):
                    print(f"\n--- disassembly of {fbase}+0x{frva:X} "
                          f"(local copy: {args.dll}) ---")
                    args.rva = frva
                    run_rva(args)
                else:
                    print(f"\n--dll not found on disk: {args.dll}")
                    print(f"rerun: python tools/native_crash_triage.py --rva 0x{frva:X} "
                          f"--dll <local copy of {fbase}>")
            else:
                print(f"\nfaulting module is {fbase}, not {want} - no local disassembly chained")
                print(f"rerun: python tools/native_crash_triage.py --rva 0x{frva:X} "
                      f"--dll <local copy of {fbase}>")
    except (ValueError, struct.error, OSError) as e:
        sys.exit(f"malformed or truncated minidump ({args.dump}): {e}")
    finally:
        md.close()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--rva", type=lambda x: int(x, 0), help="crash RVA (e.g. from an Event Log fault offset)")
    ap.add_argument("--ip", type=lambda x: int(x, 0), help="runtime instruction pointer (debugger)")
    ap.add_argument("--base", type=lambda x: int(x, 0), help="module base (VS Modules window) - required with --ip")
    ap.add_argument("--dump", help="TW CrashDumper minidump (.dmp) from a crash bundle - "
                                   "decodes exception/module/commit/stack, then chains into "
                                   "the RVA pipeline when the faulting module matches --dll")
    ap.add_argument("--dll", default=DEFAULT_DLL, help="module on disk (default: shipping TaleWorlds.Native.dll)")
    ap.add_argument("--callers", type=int, default=1, help="caller-chain levels to climb (default 1)")
    args = ap.parse_args()

    if args.dump is not None:
        run_dump(args)
        return

    if args.rva is None:
        if args.ip is None or args.base is None:
            ap.error("need --rva, --ip together with --base, or --dump")
        args.rva = args.ip - args.base
        print(f"RVA = 0x{args.ip:X} - 0x{args.base:X} = 0x{args.rva:X}")

    run_rva(args)


if __name__ == "__main__":
    main()
