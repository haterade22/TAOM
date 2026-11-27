"""Reference maps of the Rivendell trim sheet: from the Blender bake arrays to PNG files, with checks and
lit pictures. numpy and PIL, no Blender. Tests: tools/tests/test_rivendell_trim_maps.py.

    python rivendell_trim_maps.py --raw <dir>/maps_raw --out <dir>

Reads height_mm.npy, normal.npy (green up, 0 to 1) and ao.npy, written by blender_bake_trim_reference.py
(1024 x 1024, row 0 at the top). Writes, with the prefix `trim_ref`:
  _height16.png          16 bit greyscale, the lowest point black and the highest white
  _n_greenup.png         the tangent normal as Blender bakes it (OpenGL form)
  _n_greendown.png       the same with the green channel flipped (the engine form, DirectX)
  _ao.png, _curvature.png (convex light, concave dark, flat 0.5 grey)
  _lit.png, _lit_3x1.png, _lit_zoom_S1..S5.png   the carving lit from the upper left, with the AO
  _report.json           the numbers of the checks
`--joint-map <png> [--joint-size 2048]` writes the map of the joints instead (see joint_weight_map).
`--halo-map <png> [--halo-sigma-mm 7]` writes the contact shade beside the joints instead (see halo_map).
The sign rule: for a height h with rows going down, a green-down map has corr(G, dh/drow) and
corr(R, dh/dx) of the same sign (both negative); a green-up map has them opposite.
"""
import argparse
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

LIGHT = (-0.55, 0.55, 0.63)  # x right, y up, z out of the sheet: from the upper left


def green_flip(n):
    out = np.array(n, dtype=float, copy=True)
    out[..., 1] = 1.0 - out[..., 1]
    return out


def height_u16(h_mm):
    """16 bit greyscale of a height in mm: the lowest point 0, the highest 65535. Returns (array, lo, hi)."""
    lo, hi = float(np.min(h_mm)), float(np.max(h_mm))
    a = np.round((np.asarray(h_mm) - lo) / (hi - lo) * 65535.0)
    return a.astype(np.uint16), lo, hi


def _gradients(h):
    """dh/drow (edge rows kept) and dh/dcol (wrapping in U) with central differences."""
    h = np.asarray(h, dtype=float)
    d_row = np.gradient(h, axis=0)
    d_col = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    return d_row, d_col


def sign_corr(h, rgb):
    """(corr(R, dh/dx), corr(G, dh/drow)) over the pixels where the height changes."""
    d_row, d_col = _gradients(h)
    live = (np.abs(d_row) + np.abs(d_col)) > 1e-6
    r = rgb[..., 0][live]
    g = rgb[..., 1][live]
    return float(np.corrcoef(r, d_col[live])[0, 1]), float(np.corrcoef(g, d_row[live])[0, 1])


def green_sense(corr_r, corr_g):
    return "green_down" if corr_r * corr_g > 0 else "green_up"


def wrap_ratio(a):
    """How far the last and the first column are apart, against the step to their other neighbour:
    mean |col 0 - col N-1| / mean of (|col 1 - col 0| and |col N-1 - col N-2|). Near 1 for a map that
    wraps in U, also where a groove lies on the seam (both columns then sit on its two walls)."""
    a = np.asarray(a, dtype=float)
    if a.ndim == 2:
        a = a[..., None]
    seam = np.abs(a[:, 0] - a[:, -1]).mean()
    local = 0.5 * (np.abs(a[:, 1] - a[:, 0]).mean() + np.abs(a[:, -1] - a[:, -2]).mean())
    return float(seam / max(local, 1e-12))


def curvature(n_up, blur=1.0):
    """Curvature from a green-up normal map: 0.5 flat, above 0.5 convex, below concave. The divergence of
    the normal's XY part, gently blurred, scaled by its 99.5 percentile."""
    nx = n_up[..., 0] * 2.0 - 1.0
    ny = n_up[..., 1] * 2.0 - 1.0
    dnx = (np.roll(nx, -1, axis=1) - np.roll(nx, 1, axis=1)) * 0.5
    dny_up = -np.gradient(ny, axis=0)
    c = dnx + dny_up
    if blur > 0:
        c = _blur(c, blur)
    scale = max(float(np.percentile(np.abs(c), 99.5)), 1e-9)
    return np.clip(0.5 + 0.5 * c / scale, 0.0, 1.0)


def _blur(a, sigma):
    r = int(3 * sigma) + 1
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    out = np.apply_along_axis(lambda v: np.convolve(np.pad(v, r, mode="wrap"), k, mode="valid"), 1, a)
    return np.apply_along_axis(lambda v: np.convolve(np.pad(v, r, mode="edge"), k, mode="valid"), 0, out)


def shade(n_up, ao, light=LIGHT):
    """A plain lit picture, 0 to 1: Lambert light from the upper left times the ambient occlusion. A flat
    open face comes out at 0.62."""
    lx, ly, lz = np.asarray(light) / np.linalg.norm(light)
    n = n_up * 2.0 - 1.0
    n = n / np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-9)
    d = n[..., 0] * lx + n[..., 1] * ly + n[..., 2] * lz
    return np.clip(0.62 * d / lz, 0.0, 1.0) * np.asarray(ao)


def tile_u(a, k):
    return np.tile(a, (1, k) + (1,) * (a.ndim - 2))


def _u8(a):
    return np.clip(np.round(np.asarray(a) * 255.0), 0, 255).astype(np.uint8)


def _gamma(a):
    return np.clip(a, 0.0, 1.0) ** (1.0 / 2.2)


def analytic_height_mm(size):
    """The height function of rivendell_trim_strips at the pixel centres, in mm (row 0 at the top)."""
    import rivendell_trim_strips as rts
    out = np.zeros((size, size))
    spans = {n: rts.strip_bounds_m(n) for n in rts.STRIPS}
    for row in range(size):
        v = 1.0 - (row + 0.5) / size
        name = next(n for n, (v0, v1) in rts.STRIPS.items() if v0 <= v < v1 or (v1 == 1.0 and v >= v0))
        s = v * rts.TILE_M - spans[name][0]
        out[row] = [rts.height(name, (c + 0.5) / size, s) for c in range(size)]
    return out * 1000.0


def joint_weight_map(size, widen=0.0):
    """0 to 1 where a joint is, at the pixel centres of a size x size sheet (row 0 at the top): the joint weight
    of rivendell_trim_strips. The Painter texture uses it as the mask of the joint colour and roughness."""
    import rivendell_trim_strips as rts
    out = np.zeros((size, size))
    spans = {n: rts.strip_bounds_m(n) for n in rts.STRIPS}
    us = [(c + 0.5) / size for c in range(size)]
    for row in range(size):
        v = 1.0 - (row + 0.5) / size
        name = next(n for n, (v0, v1) in rts.STRIPS.items() if v0 <= v < v1 or (v1 == 1.0 and v >= v0))
        s = v * rts.TILE_M - spans[name][0]
        out[row] = [rts.joint_weight(name, u, s, widen) for u in us]
    return out


def halo_map(joint, sigma_px):
    """The contact shade of the wall stone (block_layout.make_layout): 1 in the joint and far from it, darkest at
    the edge of the joint, back to 1 over about 2.5 sigma. 1 - (1 - J) * clip(2 * gauss(J), 0, 1). Wraps in U."""
    j = np.asarray(joint, dtype=float)
    near = _blur(j, max(0.5, float(sigma_px)))
    return 1.0 - (1.0 - j) * np.clip(2.0 * near, 0.0, 1.0)


def main(argv=None):
    from PIL import Image
    import rivendell_trim_strips as rts
    ap = argparse.ArgumentParser()
    ap.add_argument("--raw")
    ap.add_argument("--out")
    ap.add_argument("--prefix", default="trim_ref")
    ap.add_argument("--no-analytic", action="store_true")
    ap.add_argument("--joint-map", help="write the 8 bit joint map of this size to this PNG and stop")
    ap.add_argument("--joint-size", type=int, default=2048)
    ap.add_argument("--joint-widen", type=float, default=0.0, help="metres added to the joint width in the map")
    ap.add_argument("--halo-map", help="write the 8 bit halo map (contact shade) of --joint-size to this PNG and stop")
    ap.add_argument("--halo-sigma-mm", type=float, default=7.0, help="sigma of the halo in mm (the wall: 7)")
    args = ap.parse_args(argv)
    if args.joint_map:
        Image.fromarray(_u8(joint_weight_map(args.joint_size, args.joint_widen))).save(args.joint_map)
        return
    if args.halo_map:
        import rivendell_trim_strips as rts
        sigma = args.halo_sigma_mm / 1000.0 * args.joint_size / rts.TILE_M
        Image.fromarray(_u8(halo_map(joint_weight_map(args.joint_size), sigma))).save(args.halo_map)
        return
    if not (args.raw and args.out):
        ap.error("--raw and --out are required")
    h = np.load(os.path.join(args.raw, "height_mm.npy"))
    n_up = np.load(os.path.join(args.raw, "normal.npy"))
    ao = np.load(os.path.join(args.raw, "ao.npy"))
    size = h.shape[0]
    base = os.path.join(args.out, args.prefix)
    h16, lo, hi = height_u16(h)
    Image.fromarray(h16).save(base + "_height16.png")
    n_down = green_flip(n_up)
    Image.fromarray(_u8(n_up)).save(base + "_n_greenup.png")
    Image.fromarray(_u8(n_down)).save(base + "_n_greendown.png")
    Image.fromarray(_u8(ao)).save(base + "_ao.png")
    curv = curvature(n_up)
    Image.fromarray(_u8(curv)).save(base + "_curvature.png")
    lit = _u8(_gamma(shade(n_up, ao)))
    Image.fromarray(lit).save(base + "_lit.png")
    Image.fromarray(tile_u(lit, 3)).save(base + "_lit_3x1.png")
    rows = rts.strip_rows()
    for name, (t, b) in rows.items():
        crop = Image.fromarray(lit[t:b, : size * 3 // 4])
        crop.resize((crop.width * 2, crop.height * 2), Image.LANCZOS).save("%s_lit_zoom_%s.png" % (base, name))

    rep = {"size": size, "height_range_mm": [lo, hi], "height16_mm_per_step": (hi - lo) / 65535.0}
    cr_u, cg_u = sign_corr(h, n_up)
    cr_d, cg_d = sign_corr(h, n_down)
    rep["sign_check"] = {
        "green_up_file": {"corr_R_dhdx": cr_u, "corr_G_dhdrow": cg_u, "sense": green_sense(cr_u, cg_u)},
        "green_down_file": {"corr_R_dhdx": cr_d, "corr_G_dhdrow": cg_d, "sense": green_sense(cr_d, cg_d)},
    }
    rep["wrap_ratio_col0_vs_col_last"] = {
        "height": wrap_ratio(h), "n_greenup": wrap_ratio(n_up), "n_greendown": wrap_ratio(n_down),
        "ao": wrap_ratio(ao), "curvature": wrap_ratio(curv), "lit": wrap_ratio(lit.astype(float))}
    rep["wrap_raw_diff_col0_vs_col_last"] = {
        "height_mm_max": float(np.abs(h[:, 0] - h[:, -1]).max()),
        "height_mm_mean": float(np.abs(h[:, 0] - h[:, -1]).mean()),
        "normal_max": float(np.abs(n_up[:, 0] - n_up[:, -1]).max()),
        "ao_max": float(np.abs(ao[:, 0] - ao[:, -1]).max())}
    flat = {}
    for name, (t, b) in rows.items():
        m = np.r_[t + 1:t + 7, b - 7:b - 1]
        flat[name] = {"height_mm_min": float(h[m].min()), "height_mm_max": float(h[m].max()),
                      "ao_min": float(ao[m].min()), "normal_B_min": float(n_up[m][..., 2].min())}
    rep["flat_margin_rows"] = flat
    rep["per_strip_height_mm"] = {n: [float(h[t:b].min()), float(h[t:b].max())] for n, (t, b) in rows.items()}
    rep["ao"] = {"min": float(ao.min()), "mean": float(ao.mean()),
                 "p1": float(np.percentile(ao, 1)), "share_below_0.9": float((ao < 0.9).mean())}
    if not args.no_analytic:
        diff = np.abs(h - analytic_height_mm(size))
        rep["bake_vs_function_mm"] = {"mean": float(diff.mean()), "p99": float(np.percentile(diff, 99)),
                                      "max": float(diff.max()), "share_under_0.1mm": float((diff < 0.1).mean()),
                                      "share_under_0.5mm": float((diff < 0.5).mean())}
    with open(base + "_report.json", "w", encoding="utf-8") as f:
        json.dump(rep, f, indent=1)
    print(json.dumps(rep, indent=1))


if __name__ == "__main__":
    main()
