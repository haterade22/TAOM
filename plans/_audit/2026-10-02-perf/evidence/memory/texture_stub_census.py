"""Count textures with and without the low-resolution stub segment per module (scratch, read-only).

Usage: texture_stub_census.py <tools dir> <label>=<folder with .tpac files> [...]
Stub segment 0365d554 (about 175 KB, a low mip tail); full pixel segment 2c4eee70.
"""
import glob
import os
import sys

sys.path.insert(0, sys.argv[1])
import audit_map_scene_memory as am  # noqa: E402

STUB, FULL = "0365d554", "2c4eee70"
for arg in sys.argv[2:]:
    label, folder = arg.split("=", 1)
    n = stub_only = both = full_only = neither = 0
    full_bytes_no_stub = 0
    for pack in sorted(glob.glob(os.path.join(folder, "*.tpac"))):
        try:
            for item in am.iter_tpac_items(pack, label):
                if item.kind != "texture":
                    continue
                n += 1
                types = {s.type_guid.hex()[:8] for s in item.segments}
                has_stub, has_full = STUB in types, FULL in types
                if has_stub and has_full:
                    both += 1
                elif has_stub:
                    stub_only += 1
                elif has_full:
                    full_only += 1
                    full_bytes_no_stub += sum(s.actual for s in item.segments if s.type_guid.hex()[:8] == FULL)
                else:
                    neither += 1
        except Exception as e:  # noqa: BLE001
            print(f"   {os.path.basename(pack)}: read error {e}")
    print(f"{label}: {n} textures; stub only {stub_only}, stub and full {both}, full without a stub {full_only} "
          f"({full_bytes_no_stub / 2**30:.2f} GiB of full chains), neither {neither}")
