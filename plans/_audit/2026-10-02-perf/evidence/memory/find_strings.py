"""Find ASCII strings in TaleWorlds.Native.dll that contain any of the given needles; print RVA and text.

Usage: find_strings.py <the game's TaleWorlds.Native.dll> <needle>...
"""
import re
import sys

import pefile

DLL = sys.argv[1]
needles = [n.encode() for n in sys.argv[2:]]
pe = pefile.PE(DLL, fast_load=True)
data = open(DLL, "rb").read()
for m in re.finditer(rb"[\x20-\x7e]{5,}", data):
    s = m.group(0)
    if any(n in s for n in needles):
        off = m.start()
        try:
            rva = pe.get_rva_from_offset(off)
        except Exception:
            rva = None
        print(f"0x{rva:X}" if rva is not None else "?", s.decode()[:160])
