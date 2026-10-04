"""Find LOCK-prefixed instructions whose memory operand has a given displacement (scratch helper, read-only).

Usage: lock_disp_scan.py <dll> <disp hex> [--any]   (--any: also report non-LOCK instructions using the disp)
Prints each hit's RVA, the instruction text, and the containing .pdata function start when known.
"""
import bisect
import struct
import sys

import capstone
import pefile

dll, disp = sys.argv[1], int(sys.argv[2], 16)
want_any = "--any" in sys.argv
pe = pefile.PE(dll, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
text = next(s for s in pe.sections if s.Name.rstrip(b"\0") == b".text")
data = text.get_data()
base_rva = text.VirtualAddress

starts = []
for entry in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []):
    starts.append((entry.struct.BeginAddress, entry.struct.EndAddress))
starts.sort()
begins = [b for b, _ in starts]


def func_of(rva):
    i = bisect.bisect_right(begins, rva) - 1
    if i >= 0 and starts[i][0] <= rva < starts[i][1]:
        return starts[i][0]
    return None


md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True
needle = struct.pack("<i", disp)
seen = set()
pos = data.find(needle)
hits = 0
while pos != -1:
    for back in range(2, 12):
        start = pos - back
        if start < 0:
            continue
        try:
            insn = next(md.disasm(data[start:start + 16], base_rva + start), None)
        except capstone.CsError:
            insn = None
        if insn is None or insn.address in seen:
            continue
        end = insn.address + insn.size
        if not (base_rva + pos + 4 <= end):
            continue
        has_lock = capstone.x86.X86_PREFIX_LOCK in insn.prefix or insn.mnemonic.startswith("lock")
        uses = any(op.type == capstone.x86.X86_OP_MEM and op.mem.disp == disp for op in insn.operands)
        if uses and (has_lock or want_any or insn.mnemonic in ("xchg", "cmpxchg", "xadd")):
            seen.add(insn.address)
            fn = func_of(insn.address)
            print(f"0x{insn.address:X}  {insn.mnemonic} {insn.op_str}  func=0x{fn:X}" if fn else
                  f"0x{insn.address:X}  {insn.mnemonic} {insn.op_str}  func=?")
            hits += 1
            break
    pos = data.find(needle, pos + 1)
print(f"{hits} hit(s)")
