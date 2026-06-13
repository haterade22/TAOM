#!/usr/bin/env python3
"""Offline forensics for native Bannerlord crashes (TaleWorlds.Native.dll and friends).

Given a crash RVA (or a runtime IP + module base), names the crash site without symbols:
  - exact function bounds from the PE .pdata (RUNTIME_FUNCTION) table
  - annotated hexdump around the crash instruction
  - every string the function references via rip-relative LEA (shipping builds keep
    assert/trace strings - this is how Agent_ai::set_attack_entity and monster_usage.cpp
    were named during the 2026-06-12 v1.4.6 spider campaign)
  - direct callers (E8 rel32 scan) with THEIR referenced strings, one level up

Born from the v1.4.6 spider campaign (3 crash sites root-caused in one day); the full
protocol that drives this tool (Event Log offsets, debugger setup, Immediate probes) is
.claude/skills/native-crash-triage/SKILL.md.

Usage:
  python tools/native_crash_triage.py --rva 0x634396
  python tools/native_crash_triage.py --ip 0x7FFD8B65E0C9 --base 0x7FFD8B060000
  python tools/native_crash_triage.py --rva 0x5FE0C9 --dll "<path-to-native-dll>" --callers 2
"""
import argparse
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


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--rva", type=lambda x: int(x, 0), help="crash RVA (e.g. from an Event Log fault offset)")
    ap.add_argument("--ip", type=lambda x: int(x, 0), help="runtime instruction pointer (debugger)")
    ap.add_argument("--base", type=lambda x: int(x, 0), help="module base (VS Modules window) - required with --ip")
    ap.add_argument("--dll", default=DEFAULT_DLL, help="module on disk (default: shipping TaleWorlds.Native.dll)")
    ap.add_argument("--callers", type=int, default=1, help="caller-chain levels to climb (default 1)")
    args = ap.parse_args()

    if args.rva is None:
        if args.ip is None or args.base is None:
            ap.error("need --rva, or --ip together with --base")
        args.rva = args.ip - args.base
        print(f"RVA = 0x{args.ip:X} - 0x{args.base:X} = 0x{args.rva:X}")

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


if __name__ == "__main__":
    main()
