"""
Build the Rivendell material rename-map + Modding Kit authoring sheet.

Naming rule (user decision 2026-07-15, revised same day): a material carries the
SAME NAME as its texture set INCLUDING the t_ prefix — material t_rivendell_<stem>
binds t_rivendell_<stem>_{d,n,s} — so creating a material in the editor points
straight at its textures and mis-assignments are visible at a glance. This is a
deliberate departure from the m_/t_ split in docs/kitbash/README.md, scoped to
the Rivendell kit (raised once, user confirmed twice). UE material instances that
bind the same texture set MERGE into one material; instances whose master implies
different shader flags (foliage alpha/two-sided, translucent) get a suffix instead
of merging with an opaque user of the same set.

Outputs (next to the bindings JSON):
  material_rename_map.json  — {current m_rivendell_<MI-name>: final m_rivendell_<stem>}
                              consumed by blender_normalize_rivendell.py --material-map
  material_sheet.csv        — one row per FINAL material: textures, mesh count, notes;
                              every texture ref existence-checked against the converted dir

Usage:
    python tools/oneoff/build_rivendell_material_sheet.py \
        "E:\\LOTRAOMAssets\\_export\\rivendell\\material_bindings.json" \
        "E:\\...\\TAOM_Map\\AssetSources\\Scenes\\Rivendell\\textures"
"""

from __future__ import annotations

import csv
import json
import re
import sys
from collections import defaultdict
from pathlib import Path

KIT = "rivendell"

PARAM_ROLES = {
    "d": ["basecolor", "bc", "base color", "00_basecolor", "basecolor/alpha",
          "tileable_basecolour", "colour_variation_basecolour"],
    "n": ["normal", "normals", "00_normal", "base normalmap", "tileable_normal",
          "unique_normal_map", "t_normal"],
    "s": ["orm", "00_orm", "base orm", "channelmap", "ao_r_mt",
          "occlusionroughnessmetallic", "tileable_orm"],
}

# Master-material families that need distinct shader flags in Bannerlord —
# they must not merge with an opaque material over the same texture set.
FLAG_FAMILIES = [
    (re.compile(r"MM_Foliage|MM_Grass|MM_WallLeaves|MM_PineBranches", re.I), "foliage"),
    (re.compile(r"Translucent|Glass|MM_Drapes|MM_Wine", re.I), "translucent"),
]


def sanitize(name: str) -> str:
    name = re.sub(r"^(T_|SM_)", "", name, flags=re.IGNORECASE)
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()


def current_mat_name(instance_path: str) -> str:
    base = instance_path.split(".")[0].split("/")[-1]
    base = re.sub(r"^(M_|MI_)", "", base, flags=re.IGNORECASE)
    return f"m_{KIT}_{sanitize(base)}"


def tex_stem(asset_path: str) -> str:
    base = asset_path.split(".")[0].split("/")[-1]
    m = re.match(r"^(.*)_([A-Za-z]+)$", base)
    if m and m.group(2).lower() in (
            "basecolor", "color", "albedo", "diffuse", "bc", "d", "b",
            "normal", "nrm", "n", "channelmap", "orm", "arm", "mra",
            "occlusionroughnessmetallic", "opacity", "ao"):
        base = m.group(1)
    return f"t_{KIT}_{sanitize(base)}"


def role_for(param: str) -> str | None:
    p = param.lower()
    for role, names in PARAM_ROLES.items():
        if p in names:
            return role
    return None


def flag_family(master: str) -> str:
    for rx, fam in FLAG_FAMILIES:
        if rx.search(master):
            return fam
    return "opaque"


def main():
    bindings = json.loads(Path(sys.argv[1]).read_text())
    tex_dir = Path(sys.argv[2])

    # Collect per material INSTANCE first.
    instances = {}
    for mesh_path, slots in bindings.items():
        mesh = mesh_path.split(".")[0].split("/")[-1]
        for slot in slots:
            mat = slot.get("material")
            if not mat:
                continue
            cur = current_mat_name(mat.get("path", "unknown"))
            inst = instances.setdefault(cur, {"meshes": set(), "master": "", "tex": {}})
            inst["meshes"].add(mesh)
            inst["master"] = (mat["chain"][-1] if mat.get("chain") else "?").split("/")[-1].split(".")[0]
            for param, tex in mat.get("textures", {}).items():
                role = role_for(param)
                if role and role not in inst["tex"]:
                    inst["tex"][role] = tex_stem(tex)

    # Derive final names: t_ + texture-set stem (+ family suffix on flag
    # conflicts). Material name == texture set name, per user decision.
    rename = {}
    for cur, inst in instances.items():
        d_stem = inst["tex"].get("d")
        if d_stem is None:
            # No textures -> keep instance-derived name, t_-prefixed for
            # kit-wide consistency in the editor.
            rename[cur] = "t_" + cur[2:] if cur.startswith("m_") else cur
            continue
        fam = flag_family(inst["master"])
        rename[cur] = d_stem if fam == "opaque" else f"{d_stem}_{fam}"

    # Merge instances by final name.
    finals = defaultdict(lambda: {"meshes": set(), "masters": set(), "tex": {}, "merged_from": set()})
    for cur, inst in instances.items():
        f = finals[rename[cur]]
        f["meshes"] |= inst["meshes"]
        f["masters"].add(inst["master"])
        f["merged_from"].add(cur)
        for role, stem in inst["tex"].items():
            f.setdefault("tex", {}).setdefault(role, stem)

    rows = []
    missing = 0
    for name in sorted(finals):
        f = finals[name]
        row = {"material": name, "masters": " ".join(sorted(f["masters"])),
               "mesh_count": len(f["meshes"]),
               "meshes_sample": " ".join(sorted(f["meshes"])[:3])}
        notes = []
        d_stem = f["tex"].get("d")
        for role in ("d", "n", "s"):
            stem = f["tex"].get(role)
            if stem is None:
                row[f"tex_{role}"] = ""
                continue
            fname = f"{stem}_{role}.png"
            if (tex_dir / fname).is_file():
                row[f"tex_{role}"] = fname
            elif d_stem and (tex_dir / f"{d_stem}_{role}.png").is_file():
                row[f"tex_{role}"] = f"{d_stem}_{role}.png"
            else:
                row[f"tex_{role}"] = f"MISSING:{fname}"
                missing += 1
        if len(f["merged_from"]) > 1:
            notes.append(f"merged {len(f['merged_from'])} UE instances")
        if any("MM_RGB_Masking" in m for m in f["masters"]):
            notes.append("SIMPLIFIED: layered UE material -> tileable triple only")
        if not f["tex"]:
            notes.append("no texture params on master -> author manually (translucent/hardwired)")
        if name.endswith("_foliage"):
            notes.append("flags: alpha test + two-sided")
        row["notes"] = "; ".join(notes)
        rows.append(row)

    out_dir = Path(sys.argv[1]).parent
    with open(out_dir / "material_rename_map.json", "w", encoding="utf-8") as fh:
        json.dump(rename, fh, indent=1, sort_keys=True)
    with open(out_dir / "material_sheet.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=["material", "masters", "mesh_count", "tex_d", "tex_n",
                                           "tex_s", "meshes_sample", "notes"])
        w.writeheader()
        w.writerows(sorted(rows, key=lambda r: -r["mesh_count"]))

    manual = sum(1 for r in rows if "author manually" in r["notes"])
    merged = sum(1 for r in rows if "merged" in r["notes"])
    print(f"[sheet] {len(instances)} UE instances -> {len(rows)} final materials "
          f"({merged} merged rows), {missing} missing texture refs, {manual} manual-only")
    print(f"[sheet] wrote {out_dir / 'material_rename_map.json'} and {out_dir / 'material_sheet.csv'}")


if __name__ == "__main__":
    main()
