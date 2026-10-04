"""List the largest textures of one tpac by decompressed pixel size (scratch, read-only).

Usage: tpac_big_textures.py <tools dir> <pack> [top]
"""
import sys

sys.path.insert(0, sys.argv[1])
import audit_map_scene_memory as am  # noqa: E402

pack = sys.argv[2]
top = int(sys.argv[3]) if len(sys.argv) > 3 else 15
rows = []
for item in am.iter_tpac_items(pack, "x"):
    if item.kind != "texture":
        continue
    full = [s for s in item.segments if s.type_guid.hex()[:8] == "2c4eee70"]
    actual = sum(s.actual for s in full)
    stored = sum(s.stored for s in full)
    try:
        info = am.parse_texture_meta(item.meta)
        desc = f"{info.width}x{info.height} mips={info.mips} faces={info.faces} {info.fmt}"
        formula = am.texture_chain_bytes(info.width, info.height, info.mips, info.fmt, info.faces)
    except Exception as e:  # noqa: BLE001
        desc, formula = f"<{type(e).__name__}>", None
    rows.append((actual, stored, item.name, desc, formula))
rows.sort(reverse=True)
print(f"textures: {len(rows)}; total actual {sum(r[0] for r in rows) / 2**20:.1f} MiB, "
      f"stored {sum(r[1] for r in rows) / 2**20:.1f} MiB")
for actual, stored, name, desc, formula in rows[:top]:
    print(f"  {actual / 2**20:8.1f} MiB actual {stored / 2**20:7.1f} MiB stored  {name}  {desc}  formula={formula}")
