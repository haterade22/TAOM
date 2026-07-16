"""
Convert the Fab Medieval Tent Collection's Substance-style separate maps into
Bannerlord sets: t_<set>_{d,n,s,h}.png.

Kept sets (user decision 2026-07-16): the 'clear' (undyed/white) canvas only —
color variants (B/GB/RB/WBK/... Base_color-only stems) are skipped — plus the
physical part sets (frame, ropes, rivet, fasteners) every tent needs.

Map assembly:
  _d = Base_color                      _n = Normal (DirectX variant, matching
  _h = Height (kit parallax convention)     the Rivendell kit; _OpenGL skipped)
  _s = R:Metallic  G:255-Roughness  B:Mixed_AO   (empirical Bannerlord packing,
       same as the Rivendell kit conversion)

Usage:
    python tools/oneoff/convert_tent_textures.py ^
        --src "E:\\TAOM ASSETS FBX\\VaultCache\\FabLibrary\\Medieval_Tent_Collection-881edec8\\additional-files\\tent_medieval_collection_extracted" ^
        --dst "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\AssetSources\\Scenes\\Tents\\textures"
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

ROLES = {"Base_color": "d", "Normal": "n", "Roughness": "rough",
         "Metallic": "metal", "Mixed_AO": "ao", "AO": "ao",
         "Height": "h", "Displacement": "h"}
COLOR_VARIANT = re.compile(r"_[A-Z]{1,3}$")  # _B, _GB, _WBK ... (case-sensitive)


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255.0


def save(arr: np.ndarray, path: Path):
    Image.fromarray((arr * 255 + 0.5).clip(0, 255).astype(np.uint8)).save(path)


def fit(arr: np.ndarray, shape) -> np.ndarray:
    if arr.shape[:2] != shape:
        im = Image.fromarray((arr * 255.0 + 0.5).clip(0, 255).astype(np.uint8)).resize((shape[1], shape[0]), Image.BILINEAR)
        arr = np.asarray(im).astype(np.float32) / 255.0
    return arr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, type=Path)
    ap.add_argument("--dst", required=True, type=Path)
    args = ap.parse_args()

    sets: dict[str, dict[str, Path]] = {}
    for tent_dir in sorted(args.src.glob("*_Textures_extracted")):
        for png in sorted(list(tent_dir.rglob("*.png")) + list(tent_dir.rglob("*.jpg"))):
            stem = png.stem
            for role_suffix, role in ROLES.items():
                if stem.endswith("_" + role_suffix):
                    base = stem[: -len(role_suffix) - 1]
                    break
            else:
                continue
            if role == "d" and COLOR_VARIANT.search(base):
                continue  # skip dyed canvas variants
            roles = sets.setdefault(base, {})
            if role in roles:
                # shared part sets duplicate across the tent zips: keep the
                # first (sorted) copy, but warn if the duplicate differs
                if roles[role].stat().st_size != png.stat().st_size:
                    print(f"[tents] WARNING duplicate {base}_{role} differs: "
                          f"kept {roles[role]}, ignored {png}")
                continue
            roles[role] = png

    args.dst.mkdir(parents=True, exist_ok=True)
    rows = []
    for base in sorted(sets):
        files = sets[base]
        if "d" not in files:
            continue  # color-variant residue (n/s shared under the clear stem)
        stem_l = re.sub(r"_clear$", "", base, flags=re.IGNORECASE).lower()
        out_stem = ("t_" + stem_l) if stem_l.startswith("tent") else ("t_tent_" + stem_l)
        albedo = load(files["d"])
        shape = albedo.shape[:2]
        notes = []
        save(albedo, args.dst / f"{out_stem}_d.png")
        if "n" in files:
            save(load(files["n"]), args.dst / f"{out_stem}_n.png")
        else:
            notes.append("no normal")
        metal = fit(load(files["metal"]), shape)[:, :, 0] if "metal" in files else np.zeros(shape, np.float32)
        rough = fit(load(files["rough"]), shape)[:, :, 0] if "rough" in files else np.full(shape, 0.7, np.float32)
        ao = fit(load(files["ao"]), shape)[:, :, 0] if "ao" in files else np.ones(shape, np.float32)
        save(np.dstack([metal, 1.0 - rough, ao]), args.dst / f"{out_stem}_s.png")
        if "h" in files:
            save(load(files["h"]), args.dst / f"{out_stem}_h.png")
        rows.append({"set": base, "out": out_stem, "notes": "; ".join(notes)})
        print(f"[tents] {base} -> {out_stem} {('(' + ', '.join(notes) + ')') if notes else ''}")

    with open(args.dst / "_convert_report.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["set", "out", "notes"])
        w.writeheader()
        w.writerows(rows)
    print(f"[tents] {len(rows)} sets converted -> {args.dst}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
