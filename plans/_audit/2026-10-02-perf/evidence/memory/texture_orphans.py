"""Candidate orphan textures in one module's packs (scratch, read-only).

Usage: texture_orphans.py <tools dir> <target packs folder> --mat <packs folder> [...] --text <folder> [...]
A target texture is a candidate orphan when (a) no material in any --mat folder's packs references its guid
(parsed slots plus the guid-window scan) and (b) its name appears in no file under any --text folder
(scenes, module data, source; binary files searched as bytes, case-insensitive). Prints the candidates by
stored size with their decompressed size.
"""
import glob
import os
import sys

sys.path.insert(0, sys.argv[1])
import audit_map_scene_memory as am  # noqa: E402

target = sys.argv[2]
args = sys.argv[3:]
mat_dirs, text_dirs, mode = [], [], None
for a in args:
    if a == "--mat":
        mode = mat_dirs
    elif a == "--text":
        mode = text_dirs
    else:
        mode.append(a)

targets = {}
for pack in sorted(glob.glob(os.path.join(target, "*.tpac"))):
    for item in am.iter_tpac_items(pack, "t"):
        if item.kind == "texture":
            targets[item.guid] = item
print(f"target textures: {len(targets)}")

referenced = set()
mat_count = 0
for d in mat_dirs:
    for pack in sorted(glob.glob(os.path.join(d, "*.tpac"))):
        try:
            for item in am.iter_tpac_items(pack, "m"):
                if item.kind == "texture":
                    continue
                mat_count += 1
                if item.kind == "material":
                    try:
                        referenced |= {g for _, g in am.parse_material_meta(item.meta).textures}
                    except Exception:  # noqa: BLE001
                        pass
                referenced |= am.scan_guids(item.meta, targets, "texture")
        except Exception as e:  # noqa: BLE001
            print(f"  {pack}: read error {e}")
print(f"materials scanned: {mat_count}; target textures referenced by a material: "
      f"{sum(1 for g in targets if g in referenced)}")

candidates = {g: t for g, t in targets.items() if g not in referenced}
names = {t.name.lower().encode("utf-8"): g for g, t in candidates.items()}
found = set()
files = 0
for d in text_dirs:
    for root, _, fnames in os.walk(d):
        for fn in fnames:
            if fn.lower().endswith((".tpac", ".dds", ".png", ".tga", ".wav", ".ogg", ".dll", ".pdb")):
                continue
            p = os.path.join(root, fn)
            try:
                if os.path.getsize(p) > 512 * 1024 * 1024:
                    continue
                data = open(p, "rb").read().lower()
            except OSError:
                continue
            files += 1
            for nb, g in names.items():
                if g not in found and nb in data:
                    found.add(g)
orphans = [(sum(s.stored for s in t.segments), sum(s.actual for s in t.segments), t.name, t.pack)
           for g, t in candidates.items() if g not in found]
orphans.sort(reverse=True)
print(f"text files searched: {files}; candidates named in a text file: {len(found)}")
print(f"candidate orphans: {len(orphans)}, {sum(o[0] for o in orphans) / 2**20:.0f} MiB stored, "
      f"{sum(o[1] for o in orphans) / 2**20:.0f} MiB decompressed")
for stored, actual, name, pack in orphans:
    print(f"  {stored / 2**20:7.1f} MiB stored {actual / 2**20:7.1f} MiB actual  {name}  ({pack})")
