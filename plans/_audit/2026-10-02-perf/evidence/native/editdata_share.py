"""Mesh edit data (segment 5f98413d...) versus render buffers (97f81dbb...) per package (scratch, read-only).

Sums each segment type's decompressed (actual) and stored bytes over every metamesh item in the
given tpacs, so vanilla's packaging can be compared with TAOM's.
Usage: editdata_share.py <label>=<glob> [...]
"""
import glob
import os
import sys

sys.path.insert(0, r"E:\repos\TAOM\tools")
import audit_map_scene_memory as a  # noqa: E402

for arg in sys.argv[1:]:
    label, pattern = arg.split("=", 1)
    files = sorted(glob.glob(pattern))
    edit_actual = edit_stored = buf_actual = buf_stored = meshes = with_edit = 0
    for p in files:
        try:
            for it in a.iter_tpac_items(p, label):
                if it.kind != "metamesh":
                    continue
                meshes += 1
                has = False
                for s in it.segments:
                    if s.type_guid == a.SEG_MESH_A:
                        edit_actual += s.actual
                        edit_stored += s.stored
                        has = True
                    elif s.type_guid == a.SEG_MESH_B:
                        buf_actual += s.actual
                        buf_stored += s.stored
                with_edit += has
        except Exception as e:  # a tpac this reader cannot walk
            print(f"   {os.path.basename(p)}: {e}")
    mb = 2 ** 20
    print(f"{label}: {len(files)} tpacs, {meshes} metameshes, {with_edit} with edit data; "
          f"edit data {edit_actual / mb:,.0f} MB ({edit_stored / mb:,.0f} MB stored), "
          f"render buffers {buf_actual / mb:,.0f} MB ({buf_stored / mb:,.0f} MB stored)")
