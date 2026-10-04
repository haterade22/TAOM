"""Texture format census per module: count and decompressed MiB by format, size class and mip state
(scratch, read-only).

Usage: texture_format_census.py <tools dir> <label>=<folder with .tpac> [...]
Flags: uncompressed formats, textures with one mip at 512 px or more (no mip chain), and the 15 largest.
"""
import collections
import glob
import os
import sys

sys.path.insert(0, sys.argv[1])
import audit_map_scene_memory as am  # noqa: E402

UNCOMPRESSED = ("R8G8B8A8_UNORM", "B8G8R8A8_UNORM", "R16G16B16A16F", "R32G32B32A32F", "R8_UNORM",
                "R16_UNORM", "R8G8_UNORM")
for arg in sys.argv[2:]:
    label, folder = arg.split("=", 1)
    by_fmt = collections.Counter()
    mib_fmt = collections.Counter()
    one_mip = []
    big = []
    n = 0
    for pack in sorted(glob.glob(os.path.join(folder, "*.tpac"))):
        for item in am.iter_tpac_items(pack, label):
            if item.kind != "texture":
                continue
            try:
                info = am.parse_texture_meta(item.meta)
            except Exception:  # noqa: BLE001
                continue
            n += 1
            size = sum(s.actual for s in item.segments if s.type_guid.hex()[:8] == "2c4eee70")
            by_fmt[info.fmt] += 1
            mib_fmt[info.fmt] += size / 2**20
            if info.mips <= 1 and max(info.width, info.height) >= 512:
                one_mip.append((size, item.name, f"{info.width}x{info.height} {info.fmt}"))
            big.append((size, item.name, f"{info.width}x{info.height} mips={info.mips} {info.fmt}"))
    total = sum(mib_fmt.values())
    print(f"== {label}: {n} textures, {total:.0f} MiB decompressed")
    for fmt, c in by_fmt.most_common():
        flag = "  <- uncompressed" if fmt in UNCOMPRESSED else ""
        print(f"   {fmt:18s} {c:5d}  {mib_fmt[fmt]:8.0f} MiB{flag}")
    one_mip.sort(reverse=True)
    print(f"   one mip at 512 px or more: {len(one_mip)} textures, {sum(r[0] for r in one_mip) / 2**20:.0f} MiB; "
          f"largest: " + "; ".join(f"{r[1]} {r[2]} {r[0] / 2**20:.0f} MiB" for r in one_mip[:5]))
    big.sort(reverse=True)
    print("   largest: " + "; ".join(f"{r[1]} {r[2]} {r[0] / 2**20:.0f} MiB" for r in big[:6]))
