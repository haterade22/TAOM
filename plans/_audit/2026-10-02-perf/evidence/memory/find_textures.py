"""Find textures by name prefix across the tpacs of some folders and print their format (scratch, read-only).

Usage: find_textures.py <tools dir> <prefix> <folder> [<folder> ...]
"""
import glob
import os
import sys

sys.path.insert(0, sys.argv[1])
import audit_map_scene_memory as am  # noqa: E402

prefix = sys.argv[2].lower()
for folder in sys.argv[3:]:
    for pack in sorted(glob.glob(os.path.join(folder, "*.tpac"))):
        try:
            for item in am.iter_tpac_items(pack, "x"):
                if item.kind != "texture" or not item.name.lower().startswith(prefix):
                    continue
                info = am.parse_texture_meta(item.meta)
                segs = {s.type_guid.hex()[:8]: s.actual for s in item.segments}
                print(f"{os.path.basename(folder.rstrip('/'))}/{os.path.basename(pack)} {item.name} "
                      f"{info.width}x{info.height} mips={info.mips} {info.fmt} segments={segs}")
        except Exception as e:  # noqa: BLE001
            print(f"{pack}: read error {e}")
