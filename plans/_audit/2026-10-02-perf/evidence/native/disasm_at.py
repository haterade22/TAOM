"""Disassemble a window of TaleWorlds.Native.dll's .text around given RVAs (read-only).

Usage: disasm_at.py <dll> <rva hex> [<rva hex>...]
Starts decoding at the RVA's function start from the exception directory, so instruction boundaries
are right, and prints the instructions from 6 before to 6 after the RVA.
"""
import bisect
import sys

import capstone
import pefile

dll = sys.argv[1]
pe = pefile.PE(dll, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
text = next(s for s in pe.sections if s.Name.rstrip(b"\0") == b".text")
data = text.get_data()
base = text.VirtualAddress
spans = sorted((e.struct.BeginAddress, e.struct.EndAddress) for e in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []))
begins = [b for b, _ in spans]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)

for arg in sys.argv[2:]:
    rva = int(arg, 16)
    i = bisect.bisect_right(begins, rva) - 1
    start = spans[i][0] if i >= 0 and spans[i][0] <= rva < spans[i][1] else max(base, rva - 64)
    code = data[start - base: rva - base + 96]
    ins = list(md.disasm(code, start))
    idx = next((k for k, x in enumerate(ins) if x.address <= rva < x.address + x.size), None)
    print(f"== 0x{rva:X} (function 0x{start:X})")
    if idx is None:
        print("   no instruction boundary at this RVA")
        continue
    for x in ins[max(0, idx - 6): idx + 7]:
        mark = ">>" if x.address <= rva < x.address + x.size else "  "
        print(f" {mark} 0x{x.address:X}  {x.bytes.hex():<22} {x.mnemonic} {x.op_str}")
