"""Host side numbers that compare a Painter bake of the trim sheet with a reference bake.

Pure numpy, no Painter, no PIL needed for 16 bit files (PIL reads a 16 bit RGB PNG as 8 bit, the trap
in docs/kitbash/tiling-texture-sets.md). Tests: tools/tests/test_trim_bake_check.py.

    python tools/painter/trim_bake_check.py <painter map folder> [--ref <trim_bake folder>] [--out report.json]

Row 0 of every map is the top of the image (V = 1).
"""
import argparse
import json
import os
import struct
import zlib

import numpy as np

ORIENTATIONS = {
    "identity": lambda a: a,
    "flipud": lambda a: a[::-1],
    "fliplr": lambda a: a[:, ::-1],
    "rot180": lambda a: a[::-1, ::-1],
    "transpose": lambda a: np.swapaxes(a, 0, 1),
}


def read_png(path):
    """A non-interlaced PNG of colour type 0, 2 or 6, bit depth 8 or 16, as uint8 or uint16, shape
    (h, w) or (h, w, channels)."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG: " + path)
    pos, idat, head = 8, [], None
    while pos < len(data):
        (n,) = struct.unpack(">I", data[pos:pos + 4])
        tag, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + n]
        pos += 12 + n
        if tag == b"IHDR":
            head = struct.unpack(">IIBBBBB", body)
        elif tag == b"IDAT":
            idat.append(body)
    w, h, depth, ctype, _, _, interlace = head
    if interlace or depth not in (8, 16) or ctype not in (0, 2, 6):
        raise ValueError("unsupported PNG (depth %d, colour type %d, interlace %d)" % (depth, ctype, interlace))
    channels = {0: 1, 2: 3, 6: 4}[ctype]
    bpp = channels * depth // 8
    stride = w * bpp
    raw = np.frombuffer(zlib.decompress(b"".join(idat)), dtype=np.uint8).reshape(h, stride + 1)
    out = np.zeros((h, stride), dtype=np.uint8)
    prev = np.zeros(stride, dtype=np.int32)
    for y in range(h):
        ft, line = raw[y, 0], raw[y, 1:].astype(np.int32)
        if ft == 0:
            cur = line
        elif ft == 2:
            cur = (line + prev) & 255
        elif ft == 1:
            cur = line.copy()
            for i in range(bpp, stride):
                cur[i] = (cur[i] + cur[i - bpp]) & 255
        elif ft == 3:
            cur = line.copy()
            for i in range(stride):
                left = cur[i - bpp] if i >= bpp else 0
                cur[i] = (cur[i] + ((left + prev[i]) >> 1)) & 255
        elif ft == 4:
            cur = line.copy()
            for i in range(stride):
                a = cur[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pred = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                cur[i] = (cur[i] + pred) & 255
        else:
            raise ValueError("bad PNG filter %d" % ft)
        out[y] = cur
        prev = cur.astype(np.int32)
    if depth == 16:
        arr = out.reshape(h, w * channels, 2)
        arr = (arr[..., 0].astype(np.uint16) << 8) | arr[..., 1].astype(np.uint16)
    else:
        arr = out
    arr = arr.reshape(h, w, channels)
    return arr[..., 0] if channels == 1 else arr


def unit(a):
    """0 to 1 floats from a uint8 or uint16 array."""
    a = np.asarray(a)
    if a.dtype == np.uint16:
        return a.astype(float) / 65535.0
    if a.dtype == np.uint8:
        return a.astype(float) / 255.0
    return a.astype(float)


def box_down(a, k):
    """Mean of k by k blocks, for an (h, w) or (h, w, c) array whose sides divide by k."""
    a = np.asarray(a, dtype=float)
    h, w = a.shape[:2]
    return a.reshape(h // k, k, w // k, k, *a.shape[2:]).mean(axis=(1, 3))


def pearson(a, b):
    """Correlation of two arrays, 0.0 when either is constant."""
    a = np.asarray(a, dtype=float).ravel()
    b = np.asarray(b, dtype=float).ravel()
    a = a - a.mean()
    b = b - b.mean()
    d = float(np.sqrt((a * a).sum() * (b * b).sum()))
    return 0.0 if d < 1e-300 else float((a * b).sum() / d)


def fit_scale(ref, other):
    """(slope, offset) of the least squares line other = slope * ref + offset."""
    x = np.asarray(ref, dtype=float).ravel()
    y = np.asarray(other, dtype=float).ravel()
    xm, ym = x.mean(), y.mean()
    var = float(((x - xm) ** 2).sum())
    slope = 0.0 if var < 1e-300 else float(((x - xm) * (y - ym)).sum() / var)
    return slope, float(ym - slope * xm)


def best_orientation(ref, test):
    """The flip or turn of `test` that correlates best with `ref`: (name, correlation)."""
    best = ("identity", -2.0)
    for name, fn in ORIENTATIONS.items():
        t = fn(np.asarray(test))
        if t.shape[:2] != np.asarray(ref).shape[:2]:
            continue
        c = pearson(ref, t)
        if c > best[1]:
            best = (name, c)
    return best


def wrap_ratio(a):
    """How far the first and last column are apart against their step to the other neighbour: near 1
    for a map that wraps in U (the definition of rivendell_trim_maps.wrap_ratio)."""
    a = np.asarray(a, dtype=float)
    if a.ndim == 2:
        a = a[..., None]
    seam = np.abs(a[:, 0] - a[:, -1]).mean()
    local = 0.5 * (np.abs(a[:, 1] - a[:, 0]).mean() + np.abs(a[:, -1] - a[:, -2]).mean())
    return float(seam / max(local, 1e-12))


def to_green_up(rgb, green_down):
    out = np.array(rgb, dtype=float, copy=True)
    if green_down:
        out[..., 1] = 1.0 - out[..., 1]
    return out


def decode_normal(rgb01):
    """Unit vectors from a 0 to 1 normal map."""
    v = np.asarray(rgb01, dtype=float) * 2.0 - 1.0
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-12)


def downsample_normal(rgb01, k):
    """k by k blocks of a normal map averaged as vectors, renormalised, back to 0 to 1."""
    v = box_down(np.asarray(rgb01, dtype=float) * 2.0 - 1.0, k)
    v = v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-12)
    return (v + 1.0) / 2.0


def angle_map(a_up, b_up):
    """Per pixel angle in degrees between two green up normal maps (0 to 1)."""
    dot = np.clip((decode_normal(a_up) * decode_normal(b_up)).sum(axis=-1), -1.0, 1.0)
    return np.degrees(np.arccos(dot))


def normal_report(ref_rgb, test_rgb, ref_green_down=False, test_green_down=False):
    """Both maps are brought to green up. Correlation per channel and the angle between the vectors
    (degrees): mean, 95th and 99th percentile."""
    a = to_green_up(ref_rgb, ref_green_down)
    b = to_green_up(test_rgb, test_green_down)
    ang = angle_map(a, b)
    return {"corr_r": pearson(a[..., 0], b[..., 0]), "corr_g": pearson(a[..., 1], b[..., 1]),
            "corr_b": pearson(a[..., 2], b[..., 2]), "mean_angle_deg": float(ang.mean()),
            "p95_angle_deg": float(np.percentile(ang, 95)), "p99_angle_deg": float(np.percentile(ang, 99))}


def margin_rows(top, bottom, inset=1, width=6):
    """Rows beside the two edges of a strip [top, bottom): `width` rows from top + inset, and `width`
    rows ending at bottom - 1 - inset. The reference report used inset 1 and width 6."""
    return np.r_[top + inset:top + inset + width, bottom - inset - width:bottom - inset]


def margin_stats(a, rows, inset=1, width=6):
    """Min and max of an (h, w) map over the margin rows of each strip: rows is {name: (top, bottom)}."""
    out = {}
    for name, (t, b) in rows.items():
        m = a[margin_rows(t, b, inset, width)]
        out[name] = {"min": float(m.min()), "max": float(m.max()), "mean": float(m.mean())}
    return out


def face_offset(trim_rgb, row_index, wall_rgb):
    """Per channel offset (0 to 255 levels) that brings the median of the trim's flat face rows to the median of
    the wall's pixels. trim_rgb is (h, w, 3), row_index the rows of the flat faces, wall_rgb (h, w, 3)."""
    face = np.median(np.asarray(trim_rgb, dtype=float)[row_index].reshape(-1, 3), axis=0)
    wall = np.median(np.asarray(wall_rgb, dtype=float).reshape(-1, 3), axis=0)
    return wall - face


def read_unit(path):
    return unit(read_png(path))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("folder", help="folder with rivendell_tile_<map>_Raw.png of the Painter export")
    ap.add_argument("--ref", default="E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/painter_lab/trim_bake")
    ap.add_argument("--out", default=None)
    ap.add_argument("--flip-green", action="store_true", help="the Painter normal map is green down")
    ap.add_argument("--height-mm", type=float, default=None, help="mm for height 1.0 of the Painter map")
    opts = ap.parse_args(argv)
    here = os.path.dirname(os.path.abspath(__file__))
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(here), "oneoff"))
    import rivendell_trim_strips as rts

    def f(name):
        return os.path.join(opts.folder, "rivendell_tile_%s_Raw.png" % name)

    raw = os.path.join(opts.ref, "maps_raw")
    ref_h = np.load(os.path.join(raw, "height_mm.npy"))
    ref_n = np.load(os.path.join(raw, "normal.npy"))
    ref_ao = np.load(os.path.join(raw, "ao.npy"))
    rows = rts.strip_rows()
    rep = {}

    def down(a, size=1024):
        k = a.shape[0] // size
        return a if k == 1 else box_down(a, k)

    ao = down(read_unit(f("ambient_occlusion")))
    if ao.ndim == 3:
        ao = ao[..., 0]
    rep["ao"] = {"orientation": best_orientation(ref_ao, ao), "corr": pearson(ref_ao, ao),
                 "mean_ref": float(ref_ao.mean()), "mean_painter": float(ao.mean()),
                 "fit_slope_offset": fit_scale(ref_ao, ao), "wrap_ratio": wrap_ratio(ao),
                 "margin_min": margin_stats(ao, rows)}
    nrm = read_unit(f("normal_base"))
    nrm = downsample_normal(nrm, nrm.shape[0] // 1024) if nrm.shape[0] > 1024 else nrm
    rep["normal"] = normal_report(ref_n, nrm, False, opts.flip_green)
    rep["normal"]["wrap_ratio"] = wrap_ratio(nrm)
    rep["normal"]["wrap_ratio_ref"] = wrap_ratio(ref_n)
    up = to_green_up(nrm, opts.flip_green)
    rep["normal"]["wrap_raw_max_diff_col0_last"] = float(np.abs(up[:, 0] - up[:, -1]).max())
    rep["normal"]["wrap_raw_max_diff_col0_last_ref"] = float(np.abs(ref_n[:, 0] - ref_n[:, -1]).max())
    ang = angle_map(ref_n, up)
    rep["normal"]["mean_angle_per_strip"] = {n: float(ang[t:b].mean()) for n, (t, b) in rows.items()}
    rep["normal"]["share_angle_over_5deg"] = float((ang > 5).mean())
    rep["normal"]["margin_B_min"] = {k: v["min"] for k, v in margin_stats(to_green_up(nrm, opts.flip_green)[..., 2], rows).items()}
    hp = down(read_unit(f("height_base")))
    if hp.ndim == 3:
        hp = hp[..., 0]
    rep["height"] = {"orientation": best_orientation(ref_h, hp), "corr": pearson(ref_h, hp),
                     "fit_mm_per_unit_and_offset": fit_scale(hp, ref_h), "wrap_ratio": wrap_ratio(hp),
                     "margin": margin_stats(hp, rows)}
    if opts.height_mm:
        rep["height"]["mean_abs_err_mm"] = float(np.abs(hp * opts.height_mm - ref_h).mean())
    cp = down(read_unit(f("curvature")))
    if cp.ndim == 3:
        cp = cp[..., 0]
    ref_curv = read_unit(os.path.join(opts.ref, "trim_ref_curvature.png"))
    rep["curvature"] = {"corr": pearson(ref_curv, cp), "mean_painter": float(cp.mean()),
                        "flat_value_painter": float(np.median(cp)), "wrap_ratio": wrap_ratio(cp)}
    text = json.dumps(rep, indent=1, default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))
    if opts.out:
        with open(opts.out, "w", encoding="utf-8") as fh:
            fh.write(text)
    print(text)


if __name__ == "__main__":
    main()
