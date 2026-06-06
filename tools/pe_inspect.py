#!/usr/bin/env python3
"""
See into a NATIVE (non-.NET) Windows DLL/EXE without a decompiler: parse the PE
export + import tables (the factual API surface + dependencies) using only stdlib.

For .NET assemblies use ilspycmd (full source). For native DLLs ilspycmd produces
nothing, so this is how we document what they actually do as FACT, not a guess.

Usage:
  python tools/pe_inspect.py <file.dll> [--max-names N]   # default N=60
Prints: machine, PE32/PE32+, internal name, #exports + sample, imported DLLs.
"""
import struct, sys, os

def _u(d, off, fmt): return struct.unpack_from(fmt, d, off)

def rva_to_off(rva, sections):
    for vaddr, vsize, roff, rsize in sections:
        if vaddr <= rva < vaddr + max(vsize, rsize):
            return roff + (rva - vaddr)
    return None

def cstr(d, off):
    end = d.index(b'\x00', off)
    return d[off:end].decode('ascii', 'replace')

def inspect(path, max_names=60):
    d = open(path, 'rb').read()
    if d[:2] != b'MZ':
        return f"{os.path.basename(path)}: not a PE file"
    e_lfanew = _u(d, 0x3C, '<I')[0]
    if d[e_lfanew:e_lfanew+4] != b'PE\x00\x00':
        return f"{os.path.basename(path)}: no PE signature"
    coff = e_lfanew + 4
    machine, nsec = _u(d, coff, '<HH')
    opt_size = _u(d, coff + 16, '<H')[0]
    opt = coff + 20
    magic = _u(d, opt, '<H')[0]                 # 0x10b=PE32, 0x20b=PE32+
    dd_off = opt + (112 if magic == 0x20b else 96)
    export_rva = _u(d, dd_off, '<I')[0]
    import_rva = _u(d, dd_off + 8, '<I')[0]
    sec_off = opt + opt_size
    sections = []
    for i in range(nsec):
        vsize, vaddr, rsize, roff = _u(d, sec_off + i*40 + 8, '<IIII')
        sections.append((vaddr, vsize, roff, rsize))
    out = [f"{os.path.basename(path)}",
           f"  machine={'x64' if machine==0x8664 else hex(machine)}  {'PE32+' if magic==0x20b else 'PE32'}  sections={nsec}"]
    # exports
    if export_rva:
        eo = rva_to_off(export_rva, sections)
        name_rva = _u(d, eo + 0x0C, '<I')[0]
        nfuncs = _u(d, eo + 0x14, '<I')[0]
        nnames = _u(d, eo + 0x18, '<I')[0]
        aon = _u(d, eo + 0x20, '<I')[0]
        intern = cstr(d, rva_to_off(name_rva, sections)) if name_rva else '(none)'
        names = []
        ao = rva_to_off(aon, sections)
        for i in range(min(nnames, max_names)):
            nrva = _u(d, ao + i*4, '<I')[0]
            names.append(cstr(d, rva_to_off(nrva, sections)))
        out.append(f"  internal_name={intern}  exports: {nfuncs} funcs / {nnames} named")
        if names:
            out.append("  sample exports: " + ", ".join(names[:max_names]))
    else:
        out.append("  exports: NONE (no export directory)")
    # imports (dependencies)
    if import_rva:
        io = rva_to_off(import_rva, sections)
        imps = []
        while io is not None:
            nm_rva = _u(d, io + 0x0C, '<I')[0]
            if nm_rva == 0:
                break
            imps.append(cstr(d, rva_to_off(nm_rva, sections)))
            io += 0x14
        out.append(f"  imports ({len(imps)}): " + ", ".join(imps))
    return "\n".join(out)

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(__doc__); sys.exit(1)
    mx = 60
    if '--max-names' in sys.argv:
        mx = int(sys.argv[sys.argv.index('--max-names') + 1])
    for p in [a for a in sys.argv[1:] if not a.startswith('--') and a.isascii() and os.path.exists(a)]:
        print(inspect(p, mx)); print()
