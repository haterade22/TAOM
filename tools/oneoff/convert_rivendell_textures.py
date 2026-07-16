"""
Convert UE metal-rough texture sets into Bannerlord spec-gloss triples
(t_rivendell_*_{d,n,s}.png) for the Rivendell kit.

UE set (per base name):   <Base>_BaseColor/_Color, <Base>_Normal, <Base>_ChannelMap
Bannerlord output:        t_rivendell_<base>_d.png  (albedo x (1-metallic);
                                                     alpha kept when meaningful — foliage masks)
                          t_rivendell_<base>_n.png  (normal; --flip-green if the
                                                     in-editor smoke test shows inverted relief)
                          t_rivendell_<base>_s.png  (R=metallic, G=gloss=255-rough, B=AO)

The _s channel layout is EMPIRICAL, derived 2026-07-15 from the shipped
Gondor/Mirkwood kit textures (metal lamps: R bimodal 0/255; stone statue: R=0;
G smooth mid-range; B tracks crevice darkening). Validate in the Modding Kit
smoke test before batch-approving the whole kit.

Packed-map packing: R=AO, G=Roughness, B=Metallic (ORM) — CONFIRMED 2026-07-15
from the kit's master-material parameter names in material_bindings.json
(`ORM`, `OcclusionRoughnessMetallic`, `AO_R_MT`). Defaults follow that.
Safety valve: sources suffixed `_ChannelMap` (the foliage masters, packing
unproven) still get the constant-high-metal refusal — a constant B=255 read as
metallic blacks out the diffuse (caught live). Explicitly-named `_ORM` files
are TRUSTED (an all-metal prop legitimately has constant metal=255).
Separate `_Opacity` textures composite into the `_d` alpha; standalone `_AO`
fills AO when no packed map exists. Constant channels are still flagged.

Usage (system Python; needs Pillow + numpy):
    python tools/oneoff/convert_rivendell_textures.py ^
        --src "E:\\LOTRAOMAssets\\_export\\rivendell\\textures" ^
        --dst "E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord\\Modules\\TAOM_Map\\AssetSources\\Scenes\\Rivendell\\textures" ^
        [--ao r --rough g --metal b] [--flip-green] [--max-size 2048] [--dry-run]
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
KIT = "rivendell"

ROLE_SUFFIXES = {
    "basecolor": "base", "color": "base", "albedo": "base", "diffuse": "base",
    "bc": "base", "d": "base", "b": "base",  # _B verified as BaseColor in this kit (sits beside _N/_ORM)
    "normal": "normal", "nrm": "normal", "n": "normal",
    "channelmap": "channel", "orm": "channel", "arm": "channel", "mra": "channel",
    "occlusionroughnessmetallic": "channel",
    "opacity": "opacity",
    "ao": "ao",
}
# Packed maps explicitly named ORM are trusted; ambiguous ChannelMap sources
# keep the constant-high-metal refusal.
TRUSTED_CHANNEL_SUFFIXES = {"orm", "arm", "mra", "occlusionroughnessmetallic"}
CHANNEL_INDEX = {"r": 0, "g": 1, "b": 2, "a": 3, "none": None}


def sanitize(name: str) -> str:
    name = re.sub(r"^(T_|SM_)", "", name, flags=re.IGNORECASE)
    return re.sub(r"[^A-Za-z0-9]+", "_", name).strip("_").lower()


def classify(path: Path):
    stem = path.stem
    m = re.match(r"^(.*)_([A-Za-z]+)$", stem)
    if not m:
        return None, None, None
    base, suffix = m.group(1), m.group(2).lower()
    role = ROLE_SUFFIXES.get(suffix)
    return (base, role, suffix) if role else (None, None, None)


def load_rgba(path: Path) -> np.ndarray:
    im = Image.open(path).convert("RGBA")
    return np.asarray(im).astype(np.float32) / 255.0


def save_png(arr: np.ndarray, path: Path, max_size: int | None):
    im = Image.fromarray(np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8))
    if max_size and max(im.size) > max_size:
        scale = max_size / max(im.size)
        im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))),
                       Image.LANCZOS)
    im.save(path)


def constant_channels(arr: np.ndarray) -> list[str]:
    names = "rgba"
    return [names[i] for i in range(arr.shape[2]) if arr[:, :, i].std() < (1.5 / 255.0)]


def convert_set(base_key, out_base, files, dst: Path, opts, rows):
    src_base = files.get("base")
    notes = []

    if src_base is None:
        rows.append({"set": base_key, "out": "", "notes": "SKIPPED: no BaseColor found"})
        return

    albedo = load_rgba(src_base[0])
    ao = np.ones(albedo.shape[:2], dtype=np.float32)
    rough = np.full(albedo.shape[:2], 0.7, dtype=np.float32)
    metal = np.zeros(albedo.shape[:2], dtype=np.float32)

    def fit(arr):
        if arr.shape[:2] != albedo.shape[:2]:
            arr = np.asarray(Image.fromarray((arr * 255.0 + 0.5).clip(0, 255).astype(np.uint8))
                             .resize((albedo.shape[1], albedo.shape[0]), Image.BILINEAR)
                             ).astype(np.float32) / 255.0
        return arr

    src_channel = files.get("channel")
    if src_channel is not None:
        path, suffix = src_channel
        cm = fit(load_rgba(path))
        trusted = suffix in TRUSTED_CHANNEL_SUFFIXES
        const = constant_channels(cm)
        if const and not trusted:
            notes.append(f"channelmap constant channels: {','.join(const)} (verify packing)")
        if opts.ao_idx is not None:
            ao = cm[:, :, opts.ao_idx]
        if opts.rough_idx is not None:
            rough = cm[:, :, opts.rough_idx]
        if opts.metal_idx is not None:
            metal = cm[:, :, opts.metal_idx]
            if not trusted and metal.mean() > 0.9 and metal.std() < 0.02:
                notes.append("metal channel constant-high in untrusted ChannelMap; IGNORED "
                             "(would black out diffuse — verify packing)")
                metal = np.zeros(albedo.shape[:2], dtype=np.float32)
    elif files.get("ao") is not None:
        ao = fit(load_rgba(files["ao"][0]))[:, :, 0]
        notes.append("no packed map: standalone _AO used; rough=0.7 metal=0")
    else:
        notes.append("no packed map: defaults ao=1 rough=0.7 metal=0")

    # _d : albedo dimmed where metallic (spec-gloss diffuse), alpha kept if used
    diffuse = albedo[:, :, :3] * (1.0 - metal)[:, :, None]
    alpha = albedo[:, :, 3]
    src_opacity = files.get("opacity")
    if src_opacity is not None:
        alpha = fit(load_rgba(src_opacity[0]))[:, :, 0]
        notes.append("separate _Opacity composited into _d alpha")
    keep_alpha = alpha.std() > (2.0 / 255.0) or alpha.min() < 0.5
    d_out = np.dstack([diffuse, alpha]) if keep_alpha else diffuse
    if keep_alpha:
        notes.append("alpha mask kept in _d")

    # _s : R=metallic, G=gloss, B=AO (empirical Bannerlord packing)
    s_out = np.dstack([metal, 1.0 - rough, ao])

    outputs = {f"{out_base}_d.png": d_out, f"{out_base}_s.png": s_out}

    src_normal = files.get("normal")
    if src_normal is not None:
        normal = load_rgba(src_normal[0])[:, :, :3]
        if opts.flip_green:
            normal[:, :, 1] = 1.0 - normal[:, :, 1]
        outputs[f"{out_base}_n.png"] = normal
    else:
        notes.append("no Normal map")

    if not opts.dry_run:
        for name, arr in outputs.items():
            save_png(arr, dst / name, opts.max_size)
    rows.append({"set": base_key, "out": " ".join(sorted(outputs)), "notes": "; ".join(notes)})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", required=True, type=Path)
    ap.add_argument("--dst", required=True, type=Path)
    ap.add_argument("--ao", default="r", choices=CHANNEL_INDEX)
    ap.add_argument("--rough", default="g", choices=CHANNEL_INDEX)
    ap.add_argument("--metal", default="b", choices=CHANNEL_INDEX)
    ap.add_argument("--flip-green", action="store_true")
    ap.add_argument("--max-size", type=int, default=None)
    ap.add_argument("--dry-run", action="store_true")
    opts = ap.parse_args()
    opts.ao_idx = CHANNEL_INDEX[opts.ao]
    opts.rough_idx = CHANNEL_INDEX[opts.rough]
    opts.metal_idx = CHANNEL_INDEX[opts.metal]

    sets: dict[str, dict[str, tuple[Path, str]]] = {}
    unmatched = []
    for path in sorted(opts.src.rglob("*")):
        if path.suffix.lower() not in (".png", ".tga"):
            continue
        base, role, suffix = classify(path)
        if base is None:
            unmatched.append(str(path))
            continue
        key = str(path.parent.relative_to(opts.src) / base)
        sets.setdefault(key, {})
        # Prefer PNG over TGA duplicates of the same map.
        if role not in sets[key] or path.suffix.lower() == ".png":
            sets[key][role] = (path, suffix)

    # Output stems come from the base FILENAME; the folder is prepended only
    # when two folders would otherwise collide on the same name. The fallback
    # itself is re-checked (deep-review: two folders sanitizing identically
    # could silently overwrite each other) — numeric suffix as last resort.
    stems: dict[str, str] = {}
    claimed: dict[str, str] = {}
    for key in sorted(sets):
        folder, base = str(Path(key).parent), Path(key).name
        stem = f"t_{KIT}_{sanitize(base)}"
        if stem in claimed and claimed[stem] != key:
            stem = f"t_{KIT}_{sanitize(folder)}_{sanitize(base)}"
            n = 2
            while stem in claimed and claimed[stem] != key:
                stem = f"t_{KIT}_{sanitize(folder)}_{sanitize(base)}_{n}"
                n += 1
        claimed[stem] = key
        stems[key] = stem

    if not opts.dry_run:
        opts.dst.mkdir(parents=True, exist_ok=True)
    rows = []
    for key in sorted(sets):
        try:
            convert_set(key, stems[key], sets[key], opts.dst, opts, rows)
        except Exception as exc:  # keep batch going; report the failure
            rows.append({"set": key, "out": "", "notes": f"ERROR: {exc}"})
        print(f"[textures] {rows[-1]['set']}: {rows[-1]['notes'] or 'ok'}")

    for u in unmatched:  # proper rows, not #-comments — keeps the CSV parseable
        rows.append({"set": "", "out": "", "notes": f"UNMATCHED: {u}"})
    if opts.dry_run:
        print(f"[textures] dry-run: report not written ({len(rows)} rows)")
    else:
        report = opts.dst / "_convert_report.csv"
        with open(report, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=["set", "out", "notes"])
            w.writeheader()
            w.writerows(rows)

    errors = [r for r in rows if r["notes"].startswith(("ERROR", "SKIPPED"))]
    print(f"[textures] {len(rows)} rows, {len(errors)} errors/skips, "
          f"{len(unmatched)} unmatched -> {opts.dst}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
