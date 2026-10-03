#!/usr/bin/env python3
"""Build the One Ring loading sprite sheet that replaces the galloping horse on Bannerlord's loading screens.

The horse is not Gauntlet. The native loading view (`rglLoading_screen_view`) draws the material
`loading_bar` above every loading screen, the first one at launch included. That material plays the
texture `loading_sprite_bannerlord` with `use_animated_texture_coords` and `MeshVectorArgument
<8, 8, 30, 64>`: 8 columns, 8 rows, 30 frames a second, 64 frames, row by row from the top left. The
vanilla texture is 2048 x 2048 (256 px cells) and blends `Modulate`, the ordinary alpha blend. All of
this was read from the installed v1.5.3 packages; see docs/features/native-loading-screen.md. The material counts
the grid in cells, not pixels, so TAOM ships the sheet at 1K (128 px cells), a quarter of the memory.

This tool turns ONE master image of the inscription (gold script in a circle on black) into that sheet:

  1. alpha from brightness: the brightest 1% of the ink is opaque, black is transparent, and a dim
     glow in between becomes partial alpha in the ink's own colour;
  2. the circle's centre and radius are fitted to the ink (an off-centre master would wobble as it
     turns) and everything beyond the inscription's outer edge is cleared;
  3. 64 frames, each turned a further 360/64 degrees clockwise about that centre (counter-clockwise
     with --counter-clockwise), rotated at the master's resolution on premultiplied channels, then
     scaled so the inscription is --diameter px across, centred in its 128 px cell;
  4. transparent texels take the ink's average colour, so mipmaps and filtering never pull in black;
  5. the cells packed row by row into a 1024 x 1024 RGBA PNG, plus an optional 30 fps preview GIF.

One full turn takes 64 / 30 = 2.13 s in game; the speed belongs to the material, not to this sheet.

Usage:
    python tools/build_loading_ring_sheet.py MASTER.png OUT.png [--preview PREVIEW.gif]
        [--preview-bg IMAGE] [--diameter 100] [--black-point 0.03] [--counter-clockwise] [--force]

The sheet then goes through the Modding Kit (import into TAOM as `loading_sprite_bannerlord`, mipmaps
on, quality downgrade off, save the module). Needs numpy and Pillow. Exit 0 on success, 2 on a refused
or failed run. Tests: tools/tests/test_build_loading_ring_sheet.py.
"""
import argparse
import math
import os
import sys

import numpy as np
from PIL import Image

COLUMNS, ROWS, FRAMES = 8, 8, 64      # the loading_bar material's MeshVectorArgument <8, 8, 30, 64>
CELL = 128                            # a 1K sheet; vanilla's 2K sheet has 256 px cells
FPS = 30
DEFAULT_DIAMETER = 100                # the inscription's outer diameter inside a cell
MIN_MARGIN = 8                        # clear texels around every cell, so mipmaps never bleed frames
DEFAULT_BLACK_POINT = 0.03            # brightness at or below this is background, not ink
OUTER_MASS = 0.998                    # the inscription's outer edge holds this share of the ink


def key_alpha(rgb, black_point=0.0, white_point=None):
    """HxWx3 uint8 on black -> HxWx4 float32 straight alpha (0..1).

    Brightness (the brightest channel) at or above the white point is solid ink, at or below the black
    point is background, and in between is glow: partial alpha in the ink's own colour. The white point
    defaults to the 99th percentile of the ink's brightness, so the glyph cores come out opaque."""
    c = np.asarray(rgb, dtype=np.float32)[:, :, :3] / 255.0
    v = c.max(axis=2)
    ink = v > black_point
    if white_point is None:
        white_point = float(np.percentile(v[ink], 99)) if ink.any() else 1.0
    a = np.where(ink, np.clip(v / white_point, 0.0, 1.0), 0.0).astype(np.float32)
    colour = np.divide(c, a[:, :, None], out=np.zeros_like(c), where=a[:, :, None] > 0)
    return np.dstack([np.clip(colour, 0.0, 1.0), a])


def fit_circle(alpha, threshold=0.25):
    """Alpha-weighted least-squares circle through the ink: (cx, cy, r) in pixel-index coordinates."""
    a = np.asarray(alpha, dtype=np.float64)
    ys, xs = np.nonzero(a > threshold * a.max())
    w = a[ys, xs]
    x, y = xs.astype(np.float64), ys.astype(np.float64)
    m = np.column_stack([x, y, np.ones_like(x)]) * w[:, None]
    rhs = -(x * x + y * y) * w
    (d, e, f), *_ = np.linalg.lstsq(m, rhs, rcond=None)
    cx, cy = -d / 2, -e / 2
    return float(cx), float(cy), float(math.sqrt(max(cx * cx + cy * cy - f, 0.0)))


def outer_radius(alpha, cx, cy, mass=OUTER_MASS):
    """Radius from (cx, cy) inside which `mass` of the ink lies; stray specks beyond it are dropped."""
    a = np.asarray(alpha, dtype=np.float64)
    ys, xs = np.nonzero(a > 0)
    dist = np.hypot(xs - cx, ys - cy)
    order = np.argsort(dist)
    cum = np.cumsum(a[ys, xs][order])
    return float(dist[order][np.searchsorted(cum, mass * cum[-1])])


def _rotate_crop(channel, cx, cy, size, degrees_clockwise):
    """Turn a float32 channel clockwise about (cx, cy) and crop a size x size square centred on it."""
    t = math.radians(degrees_clockwise)
    c, s = math.cos(t), math.sin(t)
    h = size / 2.0
    # Continuous coordinates (pixel i spans [i, i+1)): output offset o = R(t) * input offset, so the
    # inverse map Pillow wants is input = centre + R(-t) * (output - h).
    px, py = cx + 0.5, cy + 0.5
    coeffs = (c, s, px - c * h - s * h,
              -s, c, py + s * h - c * h)
    return Image.fromarray(channel, mode="F").transform(
        (size, size), Image.Transform.AFFINE, coeffs, resample=Image.Resampling.BICUBIC)


def build_frames(master_rgb, diameter=DEFAULT_DIAMETER, clockwise=True, black_point=DEFAULT_BLACK_POINT,
                 frames=FRAMES, cell=CELL):
    """The sheet's cells, frame 0 unturned, as cell x cell RGBA PIL images."""
    if diameter > cell - 2 * MIN_MARGIN:
        raise ValueError(f"diameter {diameter} leaves less than {MIN_MARGIN} px of margin in a {cell} px cell")
    rgba = key_alpha(master_rgb, black_point)
    alpha = rgba[:, :, 3]
    if not alpha.any():
        raise ValueError("the master has no ink brighter than the black point")
    cx, cy, _ = fit_circle(alpha)
    r_out = outer_radius(alpha, cx, cy)
    yy, xx = np.mgrid[0:alpha.shape[0], 0:alpha.shape[1]]
    alpha = np.where(np.hypot(xx - cx, yy - cy) <= r_out, alpha, 0.0).astype(np.float32)
    premultiplied = [np.ascontiguousarray(rgba[:, :, k] * alpha) for k in range(3)] + [alpha]
    ink = (rgba[:, :, :3] * alpha[:, :, None]).reshape(-1, 3).sum(axis=0) / alpha.sum()
    ink8 = np.round(ink * 255).astype(np.uint8)

    size = 2 * int(math.ceil(r_out * 1.03))  # even, like the cell and the diameter, so centres line up
    offset = (cell - diameter) // 2
    sign = 1 if clockwise else -1
    out = []
    for i in range(frames):
        turned = [_rotate_crop(ch, cx, cy, size, sign * 360.0 * i / frames)
                  .resize((diameter, diameter), Image.Resampling.LANCZOS) for ch in premultiplied]
        p = np.dstack([np.asarray(ch, dtype=np.float32) for ch in turned])
        a = np.clip(p[:, :, 3], 0.0, 1.0)
        colour = np.divide(p[:, :, :3], a[:, :, None], out=np.zeros_like(p[:, :, :3]), where=a[:, :, None] > 1e-4)
        a8 = np.round(a * 255).astype(np.uint8)
        rgb8 = np.round(np.clip(colour, 0.0, 1.0) * 255).astype(np.uint8)
        tile = np.empty((cell, cell, 4), dtype=np.uint8)
        tile[:, :, :3] = ink8
        tile[:, :, 3] = 0
        tile[offset:offset + diameter, offset:offset + diameter, :3] = np.where(a8[:, :, None] > 0, rgb8, ink8)
        tile[offset:offset + diameter, offset:offset + diameter, 3] = a8
        out.append(Image.fromarray(tile, mode="RGBA"))
    return out


def pack_sheet(frames, columns=COLUMNS, rows=ROWS):
    """Cells row by row from the top left, the order the loading_bar material plays them."""
    if len(frames) != columns * rows:
        raise ValueError(f"{len(frames)} frames for a {columns} x {rows} sheet")
    cell = frames[0].size[0]
    sheet = Image.new("RGBA", (columns * cell, rows * cell), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        r, c = divmod(i, columns)
        sheet.paste(f, (c * cell, r * cell))
    return sheet


def write_preview(frames, path, background=None):
    """The frames over a background, looping at the material's 30 fps (GIF rounds to 30 ms)."""
    cell = frames[0].size[0]
    if background is None:
        bg = Image.new("RGBA", (cell, cell), (38, 34, 30, 255))
    else:
        src = Image.open(background).convert("RGBA")
        side = min(src.size)
        left, top = (src.size[0] - side) // 2, (src.size[1] - side) // 2
        bg = src.crop((left, top, left + side, top + side)).resize((cell, cell), Image.Resampling.LANCZOS)
    shots = [Image.alpha_composite(bg, f).convert("RGB") for f in frames]
    shots[0].save(path, save_all=True, append_images=shots[1:], duration=round(1000 / FPS), loop=0)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("master", help="the inscription: gold script in a circle on black")
    ap.add_argument("out", help="the 1024 x 1024 sheet PNG to write")
    ap.add_argument("--preview", help="also write a looping 30 fps preview GIF here")
    ap.add_argument("--preview-bg", help="composite the preview over this image (centre square)")
    ap.add_argument("--diameter", type=int, default=DEFAULT_DIAMETER)
    ap.add_argument("--black-point", type=float, default=DEFAULT_BLACK_POINT)
    ap.add_argument("--counter-clockwise", action="store_true")
    ap.add_argument("--force", action="store_true", help="overwrite existing output files")
    args = ap.parse_args(argv)

    for path in (args.out, args.preview):
        if path and os.path.exists(path) and not args.force:
            print(f"refused: {path} exists (pass --force to overwrite)", file=sys.stderr)
            return 2
    try:
        master = np.asarray(Image.open(args.master).convert("RGB"))
        frames = build_frames(master, args.diameter, not args.counter_clockwise, args.black_point)
    except (OSError, ValueError) as exc:
        print(f"failed: {exc}", file=sys.stderr)
        return 2
    pack_sheet(frames).save(args.out)
    print(f"wrote {args.out}: {COLUMNS} x {ROWS} cells of {CELL} px, {FRAMES} frames, "
          f"{'counter-clockwise' if args.counter_clockwise else 'clockwise'}, {args.diameter} px inscription")
    if args.preview:
        write_preview(frames, args.preview, args.preview_bg)
        print(f"wrote {args.preview}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
