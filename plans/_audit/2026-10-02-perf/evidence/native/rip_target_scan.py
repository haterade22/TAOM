"""List every RIP-relative operand in .text that resolves to a target RVA (scratch helper, read-only).

Usage: rip_target_scan.py <dll> <target rva hex>
Brute force: every 4-byte window is treated as a candidate disp32; candidates whose computed target matches
are confirmed by decoding an instruction that starts a few bytes earlier and ends at or after the window.
"""
import bisect
import struct
import sys

import capstone
import pefile

dll, target = sys.argv[1], int(sys.argv[2], 16)
pe = pefile.PE(dll, fast_load=True)
pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXCEPTION"]])
text = next(s for s in pe.sections if s.Name.rstrip(b"\0") == b".text")
data = text.get_data()
base = text.VirtualAddress
spans = sorted((e.struct.BeginAddress, e.struct.EndAddress) for e in getattr(pe, "DIRECTORY_ENTRY_EXCEPTION", []))
begins = [b for b, _ in spans]


def func_of(rva):
    i = bisect.bisect_right(begins, rva) - 1
    return spans[i][0] if i >= 0 and spans[i][0] <= rva < spans[i][1] else None


md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True
hits = 0
n = len(data) - 4
unpack = struct.unpack_from
for p in range(0, n):
    disp = unpack("<i", data, p)[0]
    # The instruction ends at p+4 (no immediate) or p+5 / p+8 (imm8 / imm32 after the disp).
    for imm in (0, 1, 4):
        end_rva = base + p + 4 + imm
        if end_rva + disp != target:
            continue
        for back in range(2, 10):
            start = p - back
            if start < 0:
                continue
            insn = next(md.disasm(data[start:start + 16], base + start), None)
            if insn is None or insn.address + insn.size != end_rva:
                continue
            if any(op.type == capstone.x86.X86_OP_MEM and op.mem.base == capstone.x86.X86_REG_RIP
                   and insn.address + insn.size + op.mem.disp == target for op in insn.operands):
                fn = func_of(insn.address)
                print(f"0x{insn.address:X}  {insn.mnemonic} {insn.op_str}  func={'0x%X' % fn if fn else '?'}")
                hits += 1
                break
print(f"{hits} reference(s) to 0x{target:X}")
