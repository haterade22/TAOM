"""Read typed values at RVAs of a PE file (scratch helper, read-only). Usage: read_rva.py <dll> <rva> [<rva> ...]"""
import struct
import sys

import pefile

pe = pefile.PE(sys.argv[1], fast_load=True)
for arg in sys.argv[2:]:
    rva = int(arg, 16)
    raw = pe.get_data(rva, 8)
    f32 = struct.unpack("<f", raw[:4])[0]
    i32 = struct.unpack("<i", raw[:4])[0]
    f64 = struct.unpack("<d", raw)[0]
    print(f"0x{rva:X}: f32={f32!r} i32={i32} f64={f64!r} bytes={raw.hex()}")
