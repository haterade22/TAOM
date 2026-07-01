#!/usr/bin/env python3
"""
native_sig_author.py — reverse-engineering helper for authoring NativeSkinFixes
byte-pattern signatures + verifying struct/vtable offsets against a specific
build of TaleWorlds.Native.dll.

Why this exists: NativeSkinFixes detours 7 internal native functions and writes
to ~20 hardcoded struct offsets + 6 vtable indices inside undocumented native
C++ classes (Face_mesh, rglCloth_simulator_component, the cloth factory, the
scene). All of these are engine-version-specific; a single wrong value corrupts
memory. This tool lets us locate the functions and PROVE each offset against the
installed binary offline (no IDA/Ghidra, no game running), so only verified
values ship.

Capabilities:
  * PE load (pefile): image base, section map, RVA<->file-offset.
  * MSVC RTTI: resolve the vtable address(es) for a class by demangled name
    (e.g. "Face_mesh", "rglCloth_simulator_component"), via type-descriptor ->
    Complete Object Locator -> vtable.
  * Disassemble a window at an RVA (capstone x86-64).
  * IDA-style pattern scan (same semantics as the C++ SignatureScanner) for
    uniqueness-testing a candidate byte pattern.
  * xref scan: find rip-relative LEA/MOV/CALL that resolve to a target RVA.

Usage:
  python tools/native_sig_author.py rtti  <name-substr>        # list matching RTTI classes + vtable RVAs
  python tools/native_sig_author.py vtable <name> [--count N]   # dump first N vtable slots (RVA + disasm head)
  python tools/native_sig_author.py disasm <rva-hex> [--n N]    # disassemble N instrs at RVA
  python tools/native_sig_author.py scan   "<ida pattern>"      # count matches of a pattern, print RVAs
  python tools/native_sig_author.py xref   <rva-hex>            # find references to RVA in .text
  python tools/native_sig_author.py pattern <rva-hex> [--len L] # auto-build a wildcarded pattern from prologue

Default DLL: installed v1.4.6 client. Override with --dll <path>.
"""
import argparse, struct, sys, re

DEFAULT_DLL = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Native.dll"

try:
    import pefile
except ImportError:
    sys.exit("pip install pefile capstone")
try:
    from capstone import Cs, CS_ARCH_X86, CS_MODE_64
except ImportError:
    sys.exit("pip install capstone")


class Image:
    def __init__(self, path):
        self.path = path
        self.pe = pefile.PE(path, fast_load=True)
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.data = self.pe.__data__  # full file bytes
        self.sections = []
        for s in self.pe.sections:
            name = s.Name.rstrip(b"\x00").decode("latin1")
            self.sections.append((name, s.VirtualAddress,
                                  s.Misc_VirtualSize, s.PointerToRawData, s.SizeOfRawData))
        self.text = next((x for x in self.sections if x[0] == ".text"), None)
        self.md = Cs(CS_ARCH_X86, CS_MODE_64)
        self.md.detail = True

    def rva_to_off(self, rva):
        for _, va, vsz, roff, rsz in self.sections:
            if va <= rva < va + max(vsz, rsz):
                return roff + (rva - va)
        return None

    def off_to_rva(self, off):
        for _, va, vsz, roff, rsz in self.sections:
            if roff <= off < roff + rsz:
                return va + (off - roff)
        return None

    def read(self, rva, n):
        off = self.rva_to_off(rva)
        if off is None:
            return None
        return self.data[off:off + n]

    def u32(self, rva):
        b = self.read(rva, 4)
        return struct.unpack("<I", b)[0] if b else None

    def u64(self, rva):
        b = self.read(rva, 8)
        return struct.unpack("<Q", b)[0] if b else None

    # ---- RTTI ----
    def find_type_descriptors(self, substr):
        """Return list of (rva_of_type_descriptor, demangled-ish name)."""
        out = []
        needle = b".?AV"  # classes; .?AU for structs
        d = self.data
        i = 0
        sub = substr.encode("latin1").lower()
        while True:
            j = d.find(needle, i)
            if j < 0:
                break
            i = j + 1
            end = d.find(b"\x00", j)
            if end < 0 or end - j > 200:
                continue
            name = d[j:end]
            if sub and sub not in name.lower():
                continue
            # The TypeDescriptor starts 0x10 before the .?AV name string
            # (vftable ptr @+0, spare @+8, name @+0x10).
            td_off = j - 0x10
            td_rva = self.off_to_rva(td_off)
            if td_rva is not None:
                out.append((td_rva, name.decode("latin1")))
        return out

    def vtables_for_type(self, td_rva):
        """Given a TypeDescriptor RVA, find Complete Object Locators that point
        to it, then the vtable that points to each COL. Returns list of vtable RVAs."""
        results = []
        # COL layout (x64): u32 sig, u32 offset, u32 cdOffset, u32 pTypeDescriptor(RVA),
        #                   u32 pClassDescriptor(RVA), u32 selfRVA
        # We scan .rdata for a u32 == td_rva at COL+0x0C, validate it's a COL,
        # then find a pointer (image-absolute) to that COL — the slot right
        # before the vtable's first method.
        rdata = next((x for x in self.sections if x[0] == ".rdata"), None)
        if not rdata:
            return results
        _, va, vsz, roff, rsz = rdata
        section_bytes = self.data[roff:roff + rsz]
        # find COLs: u32 at +0x0C equals td_rva
        for m in re.finditer(re.escape(struct.pack("<I", td_rva)), section_bytes):
            col_field_off = m.start()
            col_rva = va + col_field_off - 0x0C
            # sanity: signature field should be 0 or 1
            sig = self.u32(col_rva)
            if sig not in (0, 1):
                continue
            # find an absolute pointer (u64 == base+col_rva) in .rdata -> that's
            # the "RTTI COL" slot at vtable-0x08; vtable starts at +0x08.
            col_abs = self.base + col_rva
            needle = struct.pack("<Q", col_abs)
            k = section_bytes.find(needle)
            while k >= 0:
                ptr_rva = va + k
                vtable_rva = ptr_rva + 8
                results.append(vtable_rva)
                k = section_bytes.find(needle, k + 1)
        return sorted(set(results))

    def disasm(self, rva, n=20):
        code = self.read(rva, n * 16)
        out = []
        for ins in self.md.disasm(code, self.base + rva):
            out.append(ins)
            if len(out) >= n:
                break
        return out


def cmd_rtti(img, args):
    tds = img.find_type_descriptors(args.name)
    if not tds:
        print("no matching RTTI type descriptors")
        return
    for td_rva, name in tds:
        vts = img.vtables_for_type(td_rva)
        vt = ", ".join(f"0x{v:X}" for v in vts) if vts else "(no vtable found)"
        print(f"{name}\n    TypeDescriptor RVA=0x{td_rva:X}  vtable(s): {vt}")


def _first_class_vtable(img, name):
    tds = img.find_type_descriptors(name)
    # prefer an exact ".?AV<name>@@"
    exact = [t for t in tds if t[1] == f".?AV{name}@@"]
    tds = exact or tds
    for td_rva, nm in tds:
        vts = img.vtables_for_type(td_rva)
        if vts:
            return nm, vts[0], vts
    return (tds[0][1] if tds else None), None, []


def cmd_vtable(img, args):
    nm, vt, vts = _first_class_vtable(img, args.name)
    if not vt:
        print(f"no vtable for {args.name} (td found: {nm})")
        return
    print(f"{nm}  vtable RVA=0x{vt:X}  (all: {[hex(v) for v in vts]})")
    for i in range(args.count):
        slot_rva = vt + i * 8
        fn_abs = img.u64(slot_rva)
        if not fn_abs:
            break
        fn_rva = fn_abs - img.base
        head = img.disasm(fn_rva, 3)
        hs = "; ".join(f"{x.mnemonic} {x.op_str}" for x in head)
        print(f"  [0x{i*8:X}] -> 0x{fn_rva:X}   {hs}")


def cmd_disasm(img, args):
    rva = int(args.rva, 16)
    for ins in img.disasm(rva, args.n):
        b = " ".join(f"{x:02X}" for x in ins.bytes)
        print(f"0x{ins.address - img.base:08X}  {b:<28} {ins.mnemonic} {ins.op_str}")


def _parse_pattern(p):
    toks = p.split()
    mask = []
    for t in toks:
        if t == "?" or t == "??":
            mask.append(None)
        else:
            mask.append(int(t, 16))
    return mask


def cmd_scan(img, args):
    mask = _parse_pattern(args.pattern)
    _, va, vsz, roff, rsz = img.text
    blob = img.data[roff:roff + rsz]
    hits = []
    n = len(mask)
    # build a quick prefilter on first concrete byte
    first = next((i for i, m in enumerate(mask) if m is not None), 0)
    fb = mask[first]
    start = 0
    while True:
        idx = blob.find(bytes([fb]), start)
        if idx < 0:
            break
        s = idx - first
        if s >= 0 and s + n <= len(blob):
            if all(mask[k] is None or blob[s + k] == mask[k] for k in range(n)):
                hits.append(va + s)
        start = idx + 1
    print(f"matches: {len(hits)}")
    for h in hits[:20]:
        print(f"  RVA=0x{h:X}")


def _func_start(img, rva):
    """Scan backward from rva to the nearest function start: the byte after a
    run of int3 (0xCC) padding, or after a ret (C3) followed by alignment."""
    off = img.rva_to_off(rva)
    d = img.data
    i = off
    lo = off - 0x2000
    while i > lo:
        # int3 padding boundary: ...CC CC <start>
        if d[i-1] == 0xCC and d[i-2] == 0xCC:
            return img.off_to_rva(i)
        i -= 1
    return None


def cmd_funcstart(img, args):
    rva = int(args.rva, 16)
    fs = _func_start(img, rva)
    print(f"function start for 0x{rva:X}: 0x{fs:X}" if fs else "not found")
    if fs:
        for ins in img.disasm(fs, 8):
            b = " ".join(f"{x:02X}" for x in ins.bytes)
            print(f"  0x{ins.address - img.base:08X}  {b:<28} {ins.mnemonic} {ins.op_str}")


def cmd_fxref(img, args):
    """Fast rip-relative xref: find lea/mov/call/jmp r64,[rip+disp] in .text
    whose resolved target == RVA. Scans for the 7-byte lea form and 6/5-byte
    forms by checking disp bytes against the position — O(n) byte math, no
    full capstone sweep."""
    target = int(args.rva, 16)
    _, va, vsz, roff, rsz = img.text
    blob = img.data[roff:roff + rsz]
    found = []
    # lea r64,[rip+d]: REX.W (48/4C) 8D /r d32  -> length 7, next_ip = rva+7
    # call rel32: E8 d32 (len5); jmp rel32: E9 d32 (len5)
    # mov r64,[rip+d]: 48/4C 8B /r d32 (len7)
    n = len(blob)
    for i in range(n - 7):
        b0 = blob[i]
        # 7-byte REX forms
        if b0 in (0x48, 0x4C) and blob[i+1] in (0x8D, 0x8B):
            modrm = blob[i+2]
            if (modrm & 0xC7) == 0x05:  # mod=00 rm=101 -> rip-relative
                disp = struct.unpack_from("<i", blob, i+3)[0]
                ins_rva = va + i
                if ins_rva + 7 + disp == target:
                    reg = ((b0 & 1) << 3) | ((modrm >> 3) & 7)
                    found.append((ins_rva, f"{'lea' if blob[i+1]==0x8D else 'mov'} r{reg},[rip+0x{disp:x}]"))
        # 5-byte rel32 call/jmp
        if b0 in (0xE8, 0xE9):
            disp = struct.unpack_from("<i", blob, i+1)[0]
            ins_rva = va + i
            if ins_rva + 5 + disp == target:
                found.append((ins_rva, f"{'call' if b0==0xE8 else 'jmp'} 0x{target:x}"))
    print(f"xrefs to 0x{target:X}: {len(found)}")
    for rva, desc in found[:40]:
        print(f"  0x{rva:X}  {desc}")


def cmd_xref(img, args):
    target = int(args.rva, 16)
    _, va, vsz, roff, rsz = img.text
    blob = img.data[roff:roff + rsz]
    found = []
    # scan for rip-relative refs: instructions ending in a 4-byte disp where
    # next_ip + disp == target. We brute force E8/E9 (call/jmp rel32) and
    # 48 8D (lea) / common rip-relative loads by disassembling around candidates.
    # Simpler: disassemble the whole .text and check operands.
    for ins in img.md.disasm(blob, img.base + va):
        for op in ins.operands:
            if op.type == 3 and op.mem.base == 0x29 + 0:  # X86_REG_RIP heuristic
                pass
        # use capstone's computed rip-relative target where available
        if ins.mnemonic in ("lea", "call", "jmp", "mov"):
            m = re.search(r"\[rip \+ (0x[0-9a-f]+)\]", ins.op_str)
            disp = None
            if m:
                disp = int(m.group(1), 16)
                tgt = ins.address + ins.size + disp - img.base
            elif ins.mnemonic in ("call", "jmp") and ins.op_str.startswith("0x"):
                tgt = int(ins.op_str, 16) - img.base
            else:
                tgt = None
            if tgt == target:
                found.append((ins.address - img.base, ins.mnemonic, ins.op_str))
    print(f"xrefs to 0x{target:X}: {len(found)}")
    for rva, mn, ops in found[:30]:
        print(f"  0x{rva:X}  {mn} {ops}")


def cmd_pattern(img, args):
    """Auto-build a wildcarded IDA pattern from the first <len> bytes at rva:
    wildcard the 4-byte displacement of any rip-relative / rel32 operand."""
    rva = int(args.rva, 16)
    want = args.len
    out = []
    consumed = 0
    for ins in img.disasm(rva, 40):
        if consumed >= want:
            break
        b = list(ins.bytes)
        # mark displacement/imm bytes as wildcard when rip-relative or rel32
        wild = set()
        if "rip" in ins.op_str or (ins.mnemonic in ("call", "jmp") and ins.op_str.startswith("0x")):
            # wildcard the trailing 4 bytes (disp32/rel32)
            for k in range(len(b) - 4, len(b)):
                if k >= 0:
                    wild.add(k)
        for k, bv in enumerate(b):
            if consumed >= want:
                break
            out.append("?" if k in wild else f"{bv:02X}")
            consumed += 1
    pat = " ".join(out)
    print(pat)
    # immediately uniqueness-test it
    args.pattern = pat
    cmd_scan(img, args)


# Known v1.3.15 reference RVAs (from the upstream mod source + TAOM git a77e25f9).
# Used by `diff` to capture the exact old prologue and pattern-scan it into the
# installed (new) DLL — definitive function identification, no guessing.
TARGETS = {
    "add_skin_meshes_to_agent_entity": 0x617B50,
    "cloth_factory":                    0x359C10,
    "AddToList":                        0x0C4040,
    "GpuInit":                          0x292570,
    "HasClothData":                     0x2C3420,
    "NotifyPhysics":                    0x34A570,
    "render_list_build":                0x61FE20,
    "Face_mesh_ctor(anchor)":           0x61C8F0,
}


def _build_pattern_from(img, rva, want=28):
    """Wildcard rip-relative disps + rel32 in the first <want> bytes at rva."""
    out, consumed = [], 0
    for ins in img.disasm(rva, 40):
        if consumed >= want:
            break
        b = list(ins.bytes)
        wild = set()
        if "rip" in ins.op_str or (ins.mnemonic in ("call", "jmp") and ins.op_str.startswith("0x")):
            for k in range(len(b) - 4, len(b)):
                if k >= 0:
                    wild.add(k)
        # also wildcard absolute imm64 loads (movabs) tail 8 bytes
        if ins.mnemonic == "movabs":
            for k in range(len(b) - 8, len(b)):
                if k >= 0:
                    wild.add(k)
        for k, bv in enumerate(b):
            if consumed >= want:
                break
            out.append("?" if k in wild else f"{bv:02X}")
            consumed += 1
    return " ".join(out)


def _scan_count(img, pattern):
    mask = _parse_pattern(pattern)
    _, va, vsz, roff, rsz = img.text
    blob = img.data[roff:roff + rsz]
    first = next((i for i, m in enumerate(mask) if m is not None), 0)
    fb = mask[first]
    n = len(mask)
    hits, start = [], 0
    while True:
        idx = blob.find(bytes([fb]), start)
        if idx < 0:
            break
        s = idx - first
        if s >= 0 and s + n <= len(blob) and all(mask[k] is None or blob[s + k] == mask[k] for k in range(n)):
            hits.append(va + s)
        start = idx + 1
    return hits


def cmd_diff(img, args):
    """Capture each target's prologue from the OLD dll, build a pattern, scan
    the NEW (installed) dll. Single match = definitive new RVA."""
    old = Image(args.old)
    print(f"OLD: {args.old}\nNEW: {img.path}\n")
    print(f"{'target':<34} {'oldRVA':>10} {'newRVA':>10}  matches  pattern")
    for name, old_rva in TARGETS.items():
        # confirm the old rva is a function prologue (best-effort)
        pat = _build_pattern_from(old, old_rva, args.len)
        old_hits = _scan_count(old, pat)
        new_hits = _scan_count(img, pat)
        new_rva = f"0x{new_hits[0]:X}" if len(new_hits) == 1 else ("MULTI" if new_hits else "NONE")
        flag = "OK" if len(new_hits) == 1 else "**"
        print(f"{name:<34} 0x{old_rva:08X} {new_rva:>10}  {len(new_hits):>3}{flag:>4}  {pat}")
        if len(old_hits) != 1:
            print(f"    (note: pattern matches OLD dll {len(old_hits)}x — old rva may not be a clean prologue)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dll", default=DEFAULT_DLL)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("rtti"); p.add_argument("name")
    p = sub.add_parser("vtable"); p.add_argument("name"); p.add_argument("--count", type=int, default=16)
    p = sub.add_parser("disasm"); p.add_argument("rva"); p.add_argument("--n", type=int, default=20)
    p = sub.add_parser("scan"); p.add_argument("pattern")
    p = sub.add_parser("xref"); p.add_argument("rva")
    p = sub.add_parser("fxref"); p.add_argument("rva")
    p = sub.add_parser("funcstart"); p.add_argument("rva")
    p = sub.add_parser("diff"); p.add_argument("--old", required=True); p.add_argument("--len", type=int, default=28)
    p = sub.add_parser("pattern"); p.add_argument("rva"); p.add_argument("--len", type=int, default=28)
    args = ap.parse_args()
    img = Image(args.dll)
    {"rtti": cmd_rtti, "vtable": cmd_vtable, "disasm": cmd_disasm,
     "scan": cmd_scan, "xref": cmd_xref, "fxref": cmd_fxref,
     "funcstart": cmd_funcstart, "pattern": cmd_pattern,
     "diff": cmd_diff}[args.cmd](img, args)


if __name__ == "__main__":
    main()
