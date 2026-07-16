"""
Analyze material_bindings.json from the Rivendell UE export.

Answers, from evidence rather than assumption:
  1. Which master materials exist, which texture parameters they expose, and
     which meshes use them — the packing of the packed map (ChannelMap/ORM)
     is inferred from parameter NAMES and reported per master.
  2. A flat mesh -> slot -> textures CSV for Modding Kit material authoring
     (which t_rivendell_* set each m_rivendell_* material needs).

Usage:
    python tools/oneoff/analyze_rivendell_bindings.py \
        "E:\\LOTRAOMAssets\\_export\\rivendell\\material_bindings.json"

Writes <dir>/bindings_masters.txt and <dir>/bindings_mesh_table.csv.
"""

from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path


def short(asset_path: str) -> str:
    # "/Game/ElvenForestCity/Textures/Foo.Foo" -> "Textures/Foo"
    p = asset_path.split(".")[0]
    return p.replace("/Game/ElvenForestCity/", "")


def main():
    src = Path(sys.argv[1])
    data = json.loads(src.read_text())

    masters = defaultdict(lambda: {"meshes": set(), "tex_params": defaultdict(set),
                                   "scalars": defaultdict(set), "instances": set()})
    rows = []

    for mesh_path, slots in sorted(data.items()):
        for slot in slots:
            mat = slot.get("material")
            if not mat:
                rows.append({"mesh": short(mesh_path), "slot": slot.get("slot", ""),
                             "material": "(none)"})
                continue
            master = mat["chain"][-1] if mat.get("chain") else mat.get("path", "?")
            m = masters[master]
            m["meshes"].add(short(mesh_path))
            m["instances"].add(short(mat.get("path", "?")))
            for pname, tex in sorted(mat.get("textures", {}).items()):
                m["tex_params"][pname].add(short(tex))
            for pname, val in sorted(mat.get("scalars", {}).items()):
                m["scalars"][pname].add(round(float(val), 4))
            rows.append({
                "mesh": short(mesh_path),
                "slot": slot.get("slot", ""),
                "material": short(mat.get("path", "?")),
                "master": short(master),
                **{f"tex:{k}": short(v) for k, v in sorted(mat.get("textures", {}).items())},
            })

    out_masters = src.parent / "bindings_masters.txt"
    with open(out_masters, "w", encoding="utf-8") as f:
        for master in sorted(masters):
            m = masters[master]
            f.write(f"MASTER {short(master)}\n")
            f.write(f"  used by {len(m['meshes'])} meshes via {len(m['instances'])} instances\n")
            for pname in sorted(m["tex_params"]):
                texes = m["tex_params"][pname]
                sample = sorted(texes)[0] if texes else ""
                f.write(f"  texparam {pname!r}: {len(texes)} textures (e.g. {sample})\n")
            for pname in sorted(m["scalars"]):
                vals = sorted(m["scalars"][pname])
                f.write(f"  scalar {pname!r}: {vals[:6]}{'...' if len(vals) > 6 else ''}\n")
            f.write("\n")

    fieldnames = sorted({k for r in rows for k in r})
    out_csv = src.parent / "bindings_mesh_table.csv"
    with open(out_csv, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

    print(f"[bindings] {len(data)} meshes, {len(masters)} master materials")
    for master in sorted(masters):
        m = masters[master]
        params = ", ".join(sorted(m["tex_params"]))
        print(f"  {short(master)}  ({len(m['meshes'])} meshes)  texparams: {params}")
    print(f"[bindings] wrote {out_masters}\n[bindings] wrote {out_csv}")


if __name__ == "__main__":
    main()
