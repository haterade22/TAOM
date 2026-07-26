"""
Pack one prop's plain PBR maps into a Bannerlord spec-gloss triple
t_<stem>_{d,n,s}.png.

Single-set sibling of convert_rivendell_textures.py — same conversion math
(_d = albedo x (1-metallic); _s = R:metallic, G:gloss=255-rough, B:AO — the
empirical packing verified against the shipped Gondor/Mirkwood kits), but fed
by SEPARATE maps (no packed-map layout guessing): a bake staging dir from
blender_prep_witchking_throne.py, or a Substance Painter export.

Substance round-trip (the reason this stays its own script):
    1. Import the prepped FBX (clean chart UVs) into Substance Painter.
    2. Load the staging maps as fill layers / bake anchors, paint.
    3. Export with the plain PBR Metallic Roughness preset as PNGs — any
       filenames containing basecolor/normal/roughness/metallic/ao.
    4. Re-run this script with --src <export dir> to regenerate the triple.

Usage (system Python; needs Pillow + numpy):
    python tools/oneoff/convert_tripo_prop_textures.py \
        [--src E:\\LOTRAOMAssets\\_export\\witchking_throne] \
        [--dst ...\\TAOM_Map\\AssetSources\\Scenes\\Mordor\\textures] \
        [--stem t_mordor_mm_throne] [--max-size 2048] [--flip-green] [--dry-run]

--flip-green: the in-editor smoke test is the arbiter for normal-map relief
direction (Tripo/bake output is OpenGL +Y; flip if relief looks inverted).
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image

DEFAULT_SRC = r"E:\LOTRAOMAssets\_export\witchking_throne"
DEFAULT_DST = (r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
               r"\Modules\TAOM_Map\AssetSources\Scenes\Mordor\textures")

ROLE_PATTERNS = {
    "base": r"base_?colou?r|albedo|diffuse",
    "normal": r"normal",
    "rough": r"roughness",
    "metal": r"metal(lic|ness)?",
    "ao": r"\bao\b|ambient_?occlusion|occlusion",
}


def load(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255.0


def save_png(arr: np.ndarray, path: Path, max_size: int):
    im = Image.fromarray(np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8))
    if max_size and max(im.size) > max_size:
        scale = max_size / max(im.size)
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))),
                       Image.LANCZOS)
    im.save(path)


def find_maps(src: Path) -> dict[str, Path]:
    found: dict[str, Path] = {}
    for path in sorted(src.iterdir()):
        if path.suffix.lower() not in (".png", ".tga", ".jpg", ".jpeg"):
            continue
        stem = path.stem.lower()
        for role, pat in ROLE_PATTERNS.items():
            if re.search(pat, stem) and role not in found:
                found[role] = path
    return found


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", type=Path, default=Path(DEFAULT_SRC))
    ap.add_argument("--dst", type=Path, default=Path(DEFAULT_DST))
    ap.add_argument("--stem", default="t_mordor_mm_throne")
    ap.add_argument("--max-size", type=int, default=2048)
    ap.add_argument("--flip-green", action="store_true")
    ap.add_argument("--dry-run", action="store_true")
    opts = ap.parse_args()

    maps = find_maps(opts.src)
    print(f"[convert] maps found: {({r: p.name for r, p in maps.items()})}")
    if "base" not in maps:
        print("[convert] ERROR: no basecolor map in --src")
        return 1

    albedo = load(maps["base"])
    h, w = albedo.shape[:2]

    def gray(role, default):
        if role not in maps:
            print(f"[convert] no {role} map: using constant {default}")
            return np.full((h, w), default, dtype=np.float32)
        arr = load(maps[role])[:, :, 0]
        if arr.shape != (h, w):
            arr = np.asarray(Image.fromarray((arr * 255).astype(np.uint8))
                             .resize((w, h), Image.BILINEAR)).astype(np.float32) / 255.0
        if arr.std() < (1.5 / 255.0):
            print(f"[convert] WARNING: {role} map is near-constant ({arr.mean():.3f})")
        return arr

    rough = gray("rough", 0.7)
    metal = gray("metal", 0.0)
    ao = gray("ao", 1.0)

    d_out = albedo[:, :, :3] * (1.0 - metal)[:, :, None]
    s_out = np.dstack([metal, 1.0 - rough, ao])
    outputs = {f"{opts.stem}_d.png": d_out, f"{opts.stem}_s.png": s_out}

    if "normal" in maps:
        normal = load(maps["normal"])[:, :, :3]
        if opts.flip_green:
            normal[:, :, 1] = 1.0 - normal[:, :, 1]
        outputs[f"{opts.stem}_n.png"] = normal
    else:
        print("[convert] WARNING: no normal map")

    for name, arr in outputs.items():
        out = opts.dst / name
        if opts.dry_run:
            print(f"[convert] dry-run: would write {out} {arr.shape}")
        else:
            save_png(arr, out, opts.max_size)
            print(f"[convert] wrote {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
