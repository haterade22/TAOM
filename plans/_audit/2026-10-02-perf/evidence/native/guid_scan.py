"""Find tpac segment-type GUIDs in the engine DLLs (scratch, read-only).

Searches each DLL for every GUID as 16 raw bytes and as its two little-endian 8-byte halves (a
compiler may build a GUID with two 64-bit immediates), and prints the file offsets and, for PE
files, the RVA and section of each hit.
"""
import struct
import sys

GUIDS = {
    "SEG_MESH_A (editor positions)": "5f98413dd224c14f82e46a6e0da3f4e2",
    "SEG_MESH_B (runtime streams)": "97f81dbb4f587047abf2663fe449f247",
    "SEG_MESH_TABLE": "f6304064428a864cb9359b9daa9391c2",
    "SEG_TEXTURE_PIXELS": "2c4eee70e4792d4b8d54d53ecd2a559c",
    "SEG_TEXTURE_STUB": "0365d55444994041ad1755bbd9ce9e39",
    "TYPE_METAMESH": "978b8fa07c19ea4bb95b53846cae834e",
}
DLLS = {
    "shipping": r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Native.dll",
    "editor": r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_wEditor\TaleWorlds.Native.dll",
}


def sections(data):
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    nsec = struct.unpack_from("<H", data, pe + 6)[0]
    opt = struct.unpack_from("<H", data, pe + 20)[0]
    base = pe + 24 + opt
    out = []
    for i in range(nsec):
        o = base + i * 40
        name = data[o:o + 8].rstrip(b"\0").decode("ascii", "replace")
        vsize, va, rsize, rptr = struct.unpack_from("<IIII", data, o + 8)
        out.append((name, va, vsize, rptr, rsize))
    return out


def where(secs, off):
    for name, va, vsize, rptr, rsize in secs:
        if rptr <= off < rptr + rsize:
            return name, va + (off - rptr)
    return "?", None


def find_all(data, needle):
    hits, i = [], data.find(needle)
    while i != -1 and len(hits) < 20:
        hits.append(i)
        i = data.find(needle, i + 1)
    return hits


for dll_name, path in DLLS.items():
    try:
        data = open(path, "rb").read()
    except OSError as e:
        print(dll_name, "unreadable:", e)
        continue
    secs = sections(data)
    print(f"== {dll_name}: {path} ({len(data):,} bytes)")
    for label, hexs in GUIDS.items():
        g = bytes.fromhex(hexs)
        full = find_all(data, g)
        lo = find_all(data, g[:8])
        hi = find_all(data, g[8:])
        def fmt(hits):
            parts = []
            for h in hits[:6]:
                s, rva = where(secs, h)
                parts.append(f"{s}:0x{rva:X}" if rva is not None else f"off 0x{h:X}")
            return ", ".join(parts) + (" ..." if len(hits) > 6 else "")
        print(f"   {label:32} full={len(full)} [{fmt(full)}]  low8={len(lo)} [{fmt(lo)}]  high8={len(hi)} [{fmt(hi)}]")
