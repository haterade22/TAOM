"""Turn Substance Painter exports into the Rivendell set form the engine reads:
<set>_d.png, <set>_n.png (green DOWN, the DirectX form) and <set>_s.png (R metallic, G gloss, B ambient
occlusion). There is no _n_flipgreen file.

Evidence for the packing: the game's own shader (standart.rsh compute_specularity reads x as metallic,
y as gloss, z as occlusion) and the channel statistics of the Gondor control maps. Gloss is 255 minus
roughness. Blue is the real occlusion map, white where open. The normal maps of the controls are green
down (painter_lab/research/engine/normal_sense_*.json).

Run: python tools/oneoff/convert_painter_stone_sets.py <export folder> --out <set folder> --name <set>
         --target <hex> [--gloss-mean 105] [--gloss-std 14] [--normal-form down|up]
The export folder holds rivendell_tile_baseColor/normal/roughness/ambientOcclusion.png. Export the normal
as Normal_DirectX (--normal-form down); an OpenGL export (--normal-form up) is turned over here.
The pure array steps are tested in tools/tests/test_convert_painter_stone_sets.py.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

AO_STRENGTH = 0.35


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def apply_ao(base, ao, strength):
    """Multiply `strength` of the occlusion (0 = occluded, 255 = open) into an RGB array."""
    occ = 1.0 - strength * (1.0 - np.asarray(ao, dtype=float) / 255.0)
    return base * occ[..., None]


def match_mean(img, target, rounds=6):
    """Shift each channel so its mean is `target`, keeping contrast; clipping is re-corrected."""
    out = np.asarray(img, dtype=float).copy()
    for _ in range(rounds):
        out = np.clip(out + (np.asarray(target, dtype=float) - out.reshape(-1, 3).mean(0)), 0, 255)
    return out


def _shift_to_mean(ch, mean, rounds=8):
    """Add a constant so the clipped channel has mean `mean`."""
    ch = np.asarray(ch, dtype=float)
    for _ in range(rounds):
        ch = np.clip(ch + (mean - np.clip(ch, 0, 255).mean()), 0, 255)
    return ch


def spec_from_maps(rough, ao, gloss_mean=None, gloss_std=None):
    """The engine _s: R 0 (metallic, none for stone), G gloss = 255 minus roughness, B the occlusion map.
    `gloss_mean` shifts G to that mean (a smooth stone wants 90 to 120); `gloss_std` only narrows a
    wider spread to that value."""
    r = np.asarray(rough, dtype=float)
    out = np.zeros(r.shape + (3,))
    g = 255.0 - r
    if gloss_std is not None and g.std() > gloss_std:
        g = g.mean() + (g - g.mean()) * (gloss_std / g.std())
    if gloss_mean is not None:
        g = _shift_to_mean(g, gloss_mean)
    out[..., 1] = np.clip(g, 0, 255)
    out[..., 2] = np.clip(np.asarray(ao, dtype=float), 0, 255)
    return out


def retarget_gloss(spec, gloss_mean):
    """A copy of an _s array whose green channel is shifted to `gloss_mean` (the trim sheet follows the wall)."""
    out = np.array(spec, dtype=float, copy=True)
    out[..., 1] = _shift_to_mean(out[..., 1], gloss_mean)
    return out


def flip_green(n):
    out = np.array(n, copy=True)
    out[..., 1] = 255 - out[..., 1]
    return out


def engine_normal(n, form):
    """A normal map in the engine form (green down). `form` is the green sense of the input."""
    if form == "down":
        return np.array(n, copy=True)
    if form == "up":
        return flip_green(n)
    raise ValueError("normal form must be 'down' or 'up', not %r" % (form,))


def seam_ratio(tile):
    """Wrap-edge step over the typical interior step, worst of both axes. About 1: tiles."""
    t = np.asarray(tile, dtype=float)
    if t.ndim == 3:
        t = t.mean(2)
    worst = 0.0
    for ax in (0, 1):
        d = np.abs(np.diff(t, axis=ax))
        interior = d.mean() or 1e-6
        edge = np.abs(np.take(t, 0, axis=ax) - np.take(t, -1, axis=ax)).mean()
        worst = max(worst, edge / interior)
    return worst


def load(p, mode):
    return np.asarray(Image.open(p).convert(mode))


def save(a, p):
    Image.fromarray(np.clip(np.rint(a), 0, 255).astype(np.uint8)).save(p)


def convert_one(export_dir, out, name, target_hex, gloss_mean=None, gloss_std=None, normal_form="down",
                ao_strength=AO_STRENGTH):
    """One export folder to <name>_d/_n/_s.png in `out`. Returns a small JSON-able report."""
    os.makedirs(out, exist_ok=True)

    def f(m):
        return os.path.join(export_dir, "rivendell_tile_%s.png" % m)

    base = load(f("baseColor"), "RGB").astype(float)
    ao = load(f("ambientOcclusion"), "L")
    diff = match_mean(apply_ao(base, ao, ao_strength), hex_rgb(target_hex))
    n = engine_normal(load(f("normal"), "RGB"), normal_form)
    spec = spec_from_maps(load(f("roughness"), "L"), ao, gloss_mean, gloss_std)
    save(diff, os.path.join(out, name + "_d.png"))
    save(n, os.path.join(out, name + "_n.png"))
    save(spec, os.path.join(out, name + "_s.png"))
    stat = lambda a: [round(float(a.mean()), 2), round(float(a.std()), 2)]
    return {
        "export": export_dir, "target": "#" + target_hex.lstrip("#"),
        "diffuse_mean": [round(float(v), 2) for v in diff.reshape(-1, 3).mean(0)],
        "spec_r": stat(spec[..., 0]), "spec_g": stat(spec[..., 1]), "spec_b": stat(spec[..., 2]),
        "normal_green_form": "down", "normal_input_form": normal_form,
        "seam_ratio_d": round(seam_ratio(diff), 2), "seam_ratio_n": round(seam_ratio(n), 2),
    }


def main(argv=None):
    import argparse

    ap = argparse.ArgumentParser()
    ap.add_argument("export", help="folder holding rivendell_tile_<map>.png")
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--target", required=True, help="mean colour of _d, #rrggbb")
    ap.add_argument("--gloss-mean", type=float, default=None)
    ap.add_argument("--gloss-std", type=float, default=None)
    ap.add_argument("--normal-form", choices=("down", "up"), default="down")
    ap.add_argument("--ao-strength", type=float, default=AO_STRENGTH)
    a = ap.parse_args(argv)
    rep = convert_one(a.export, a.out, a.name, a.target, a.gloss_mean, a.gloss_std, a.normal_form, a.ao_strength)
    json.dump({a.name: rep}, open(os.path.join(a.out, a.name + "_report.json"), "w"), indent=1)
    print(json.dumps({a.name: rep}, indent=1))


if __name__ == "__main__":
    main(sys.argv[1:])
