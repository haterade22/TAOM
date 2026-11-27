#!/usr/bin/env python3
"""Measure size and colour of texture files, for a building palette.

    python tools/oneoff/texture_stats.py PATH_OR_FOLDER [...] --out stats.json [--kind auto|color|data]

A folder contributes its image files (png, tga, dds, jpg, jpeg, bmp, tif), not recursively.
Output is one JSON object: {"files": [...]}, one entry per file, in argument order.

A colour map reports, in sRGB 0 to 255 (no linearisation):
  size, mode, per-channel mean and std, mean_hex, luminance p5/p50/p95 (Rec.601 weights of the sRGB
  values, as PIL's "L" does), mean_saturation (HSV, 0 to 1) and the five dominant colours.
The dominant colours come from PIL median-cut quantize on at most 262144 pixels taken at an even
stride, so a share is a share of that sample. Every other number uses every counted pixel.
A data map (specular, normal, height, packed) reports per-channel mean, std, p5, p50, p95 only.
kind=auto reads the file name: a _s, _n, _h, _sdf, _normal, _specular, _spec, _rough, _orm or
_packed suffix is data, anything else is colour.
Pixels with alpha 0 are not counted in either kind; alpha_zero_share says how many were dropped.
A file that cannot be read gets an "error" entry; the run goes on and exits 0.
"""
import argparse
import json
import os
import re
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None

IMAGE_EXT = (".png", ".tga", ".dds", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff")
DATA_SUFFIX = re.compile(r"_(s|n|h|sdf|normal|specular|spec|rough|roughness|orm|packed|height|mask)$", re.I)
QUANT_SAMPLE = 262144
GREY_MODES = ("L", "LA", "I", "I;16", "F", "1")


def kind_from_name(path):
    stem = os.path.splitext(os.path.basename(path))[0]
    return "data" if DATA_SUFFIX.search(stem) else "color"


def _to_array(img):
    """Return (rgba uint8 array H x W x 4, band names)."""
    if img.mode in ("I;16", "I", "F"):
        a = np.asarray(img, dtype=np.float64)
        top = 65535.0 if img.mode == "I;16" else max(float(a.max()), 1.0)
        g = np.clip(a / top * 255.0, 0, 255).astype(np.uint8)
        rgba = np.stack([g, g, g, np.full_like(g, 255)], axis=-1)
        return rgba, ["L"]
    bands = ["L"] if img.mode in GREY_MODES else ["R", "G", "B"]
    has_alpha = img.mode in ("LA", "RGBA", "PA") or "transparency" in img.info
    rgba = np.asarray(img.convert("RGBA"), dtype=np.uint8)
    if has_alpha and img.mode not in ("P",):
        bands = bands + ["A"]
    return rgba, bands


def _percentiles(v):
    p5, p50, p95 = np.percentile(v, [5, 50, 95])
    return float(p5), float(p50), float(p95)


def _hex(rgb):
    return "#%02x%02x%02x" % tuple(int(round(min(max(c, 0), 255))) for c in rgb)


def _dominant(rgb):
    n = rgb.shape[0]
    if n > QUANT_SAMPLE:
        rgb = rgb[:: int(np.ceil(n / QUANT_SAMPLE))]
    strip = Image.fromarray(rgb.reshape(1, -1, 3).astype(np.uint8), "RGB")
    q = strip.quantize(colors=5, method=Image.Quantize.MEDIANCUT)
    counts = q.getcolors() or []
    pal = q.getpalette() or []
    total = float(sum(c for c, _ in counts))
    out = []
    for c, idx in sorted(counts, reverse=True):
        out.append({"hex": _hex(pal[idx * 3: idx * 3 + 3]), "share": round(c / total, 4)})
    return out


def image_stats(img, kind="color"):
    img.load()
    rgba, bands = _to_array(img)
    flat = rgba.reshape(-1, 4)
    total = flat.shape[0]
    keep = flat[:, 3] > 0
    px = flat[keep]
    res = {
        "size": [img.size[0], img.size[1]],
        "mode": img.mode,
        "kind": kind,
        "counted_pixels": int(px.shape[0]),
        "alpha_zero_share": round(1.0 - px.shape[0] / float(total), 4) if total else 0.0,
    }
    if px.shape[0] == 0:
        res["error"] = "no pixel with alpha above 0"
        return res
    chan_index = {"R": 0, "G": 1, "B": 2, "A": 3}
    channels = {}
    for b in bands:
        col = (px[:, 0] if b == "L" else px[:, chan_index[b]]).astype(np.float64)
        entry = {"mean": float(col.mean()), "std": float(col.std())}
        if kind == "data":
            entry["p5"], entry["p50"], entry["p95"] = _percentiles(col)
        channels[b] = entry
    res["channels"] = channels
    if kind == "data":
        return res
    rgb = px[:, :3].astype(np.float64)
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    mx, mn = rgb.max(axis=1), rgb.min(axis=1)
    sat = np.where(mx > 0, (mx - mn) / np.where(mx > 0, mx, 1.0), 0.0)
    res["mean_hex"] = _hex(rgb.mean(axis=0))
    p5, p50, p95 = _percentiles(lum)
    res["luminance"] = {"p5": p5, "p50": p50, "p95": p95}
    res["mean_saturation"] = float(sat.mean())
    res["dominant"] = _dominant(px[:, :3])
    return res


def file_stats(path, kind="auto"):
    k = kind_from_name(path) if kind == "auto" else kind
    entry = {"path": path}
    try:
        with Image.open(path) as img:
            entry.update(image_stats(img, k))
    except Exception as ex:  # noqa: BLE001 - one bad file must not end the run
        entry.update({"kind": k, "error": "%s: %s" % (type(ex).__name__, ex)})
    return entry


def expand(paths):
    out = []
    for p in paths:
        if os.path.isdir(p):
            for name in sorted(os.listdir(p)):
                full = os.path.join(p, name)
                if os.path.isfile(full) and name.lower().endswith(IMAGE_EXT):
                    out.append(full)
        else:
            out.append(p)
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("paths", nargs="+", help="image files or folders")
    ap.add_argument("--out", required=True, help="JSON output path")
    ap.add_argument("--kind", choices=("auto", "color", "data"), default="auto")
    args = ap.parse_args(argv)
    files = [file_stats(p, args.kind) for p in expand(args.paths)]
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump({"files": files}, f, indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main())
