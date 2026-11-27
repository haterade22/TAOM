"""Lit previews and contact sheets for Painter tiling-plane exports (no Painter needed).

    python tools/painter/painter_tile_preview.py --cat <catalogue folder> [--names a b] [--sub tests/x]

For each material folder it reads rivendell_tile_baseColor.png, _normal.png (OpenGL) and
_roughness.png when present, writes preview_lit_2x2.png (a 2 x 2 tiling, Lambert plus a small
roughness-scaled highlight, light from the upper left), and makes contact sheets of all of them.
"""
import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw

LIGHT = np.array([-0.5, 0.6, 0.62])
LIGHT = LIGHT / np.linalg.norm(LIGHT)
VIEW = np.array([0.0, 0.0, 1.0])
PREFIX = "rivendell_tile_"


def lit(base, normal, rough=None):
    """Shade base (uint8 RGB) with an OpenGL normal map (uint8 RGB) and optional roughness (uint8)."""
    n = normal.astype(np.float64) / 127.5 - 1
    n[..., 2] = np.sqrt(np.clip(1 - n[..., 0] ** 2 - n[..., 1] ** 2, 0, 1))
    n = n / np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    alb = (base.astype(np.float64) / 255) ** 2.2
    lam = np.clip(n @ LIGHT, 0, 1)[..., None]
    half = LIGHT + VIEW
    half = half / np.linalg.norm(half)
    shin = 1.0 - (0.5 if rough is None else rough.astype(np.float64)[..., None] / 255)
    spec = np.clip(n @ half, 0, 1)[..., None] ** 24 * 0.12 * shin
    out = alb * (0.18 + 0.95 * lam) + spec
    return np.clip(out ** (1 / 2.2) * 255, 0, 255).astype(np.uint8)


def tile2x2(a):
    return np.tile(a, (2, 2, 1))


def pages(items, size):
    return [items[i:i + size] for i in range(0, len(items), size)]


def contact_sheet(cells, cols, cell, label_h):
    """cells: [(name, uint8 RGB array)]; each is resized to cell x cell under a label line."""
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell, rows * (cell + label_h)), (20, 20, 20))
    draw = ImageDraw.Draw(sheet)
    for i, (name, arr) in enumerate(cells):
        x, y = (i % cols) * cell, (i // cols) * (cell + label_h)
        sheet.paste(Image.fromarray(arr).resize((cell, cell), Image.LANCZOS), (x, y + label_h))
        draw.text((x + 3, y + 1), name, fill=(255, 255, 255))
    return sheet


def read(folder, name, mode):
    path = os.path.join(folder, PREFIX + name + ".png")
    return np.asarray(Image.open(path).convert(mode)) if os.path.exists(path) else None


def preview(folder):
    base, normal, rough = read(folder, "baseColor", "RGB"), read(folder, "normal", "RGB"), read(folder, "roughness", "L")
    if base is None or normal is None:
        return None
    img = lit(tile2x2(base), tile2x2(normal), None if rough is None else np.tile(rough, (2, 2)))
    Image.fromarray(img).save(os.path.join(folder, "preview_lit_2x2.png"))
    return img


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--cat", required=True)
    ap.add_argument("--names", nargs="*")
    ap.add_argument("--sub", default="")
    ap.add_argument("--per-sheet", type=int, default=12)
    ap.add_argument("--cell", type=int, default=400)
    ap.add_argument("--sheet-prefix", default="contact_sheet")
    a = ap.parse_args(argv)
    names = a.names or sorted(d for d in os.listdir(a.cat) if os.path.isdir(os.path.join(a.cat, d)))
    cells = []
    for n in names:
        img = preview(os.path.join(a.cat, n, a.sub) if a.sub else os.path.join(a.cat, n))
        if img is not None:
            cells.append((n, img))
    written = []
    for i, page in enumerate(pages(cells, a.per_sheet), 1):
        out = os.path.join(a.cat, f"{a.sheet_prefix}_{i}.png")
        contact_sheet(page, 4, a.cell, 14).save(out)
        written.append(out)
    print(json.dumps({"previews": len(cells), "sheets": written}))


if __name__ == "__main__":
    main()
