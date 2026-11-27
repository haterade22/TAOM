#!/usr/bin/env python3
"""Measure how well an exported texture tiles, and write a tiled preview.

    python tools/painter/tile_seam_metric.py <image.png> [<image2.png> ...] [--preview out.png --tiles 2]

For each image it prints one JSON object. The metric: the image is rolled by half its size so the wrap
sits in the middle; the step across the wrap is divided by the 95th percentile of the steps in a
window of 16 columns (rows) on each side. A step that is not larger than the sharpest natural step
nearby gives about 1 or less. A real step at the wrap gives a ratio well above 1 (a plain ramp gives
about 127 at 128 px). The verdict flags a ratio above 1.5 as "check the wrap": on strongly structured
stone (joints and chips that meet at the border) it can flag a tile that looks fine, so look at the
tiled preview before you call it a seam. Checked in this job on 12 exports: 3 flagged, and all 3 looked
fine in a 2 x 2 preview (a joint lies on the tile border in each).

It finds a step. It does not find repetition (the same stone every metre: look at the tiled preview),
a seam that only shows in a few rows (the steps are averaged over the rows), or a mismatch of the
pattern itself (two block rows that do not line up but have no tone step). It does not replace a look
at the tile in the engine.
"""
from __future__ import annotations

import argparse
import json
import sys

import numpy as np

THRESHOLD = 1.5
NO_INTERIOR_STEP = 1e9
WINDOW = 16


def _ratio(wrap, inner):
    if wrap == 0 and inner == 0:
        return 1.0
    if inner == 0:
        return NO_INTERIOR_STEP
    return float(wrap / inner)


def _axis_ratio(a):
    """Seam ratio for the wrap between the last and the first column of a (H x W x C)."""
    width = a.shape[1]
    centre = width // 2
    rolled = np.roll(a, centre, axis=1)
    steps = np.abs(rolled[:, 1:, :] - rolled[:, :-1, :]).mean(axis=(0, 2))  # steps[i]: column i to i + 1
    seam = steps[centre - 1]
    near = np.concatenate([steps[max(0, centre - 1 - WINDOW):centre - 1], steps[centre:centre + WINDOW]])
    return _ratio(seam, float(np.percentile(near, 95)))


def seam_ratios(image):
    """Seam ratios of a float image (H x W or H x W x C, any scale)."""
    a = np.asarray(image, dtype=np.float64)
    if a.ndim == 2:
        a = a[:, :, None]
    if a.shape[0] < 4 or a.shape[1] < 4:
        raise ValueError("image is too small to measure: %r" % (a.shape,))
    return {"horizontal": _axis_ratio(a), "vertical": _axis_ratio(np.swapaxes(a, 0, 1))}


def verdict(ratios):
    bad = [axis for axis in ("horizontal", "vertical") if ratios[axis] > THRESHOLD]
    return "no step at the wrap" if not bad else "check the wrap on " + " and ".join(bad)


def tile(image, count):
    """The image repeated count x count times."""
    a = np.asarray(image)
    reps = (count, count) + (1,) * (a.ndim - 2)
    return np.tile(a, reps)


def _load(path):
    from PIL import Image
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float64) / 255.0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("images", nargs="+")
    parser.add_argument("--preview", help="write a tiled preview of the first image here")
    parser.add_argument("--tiles", type=int, default=2)
    opts = parser.parse_args(argv)
    rows = []
    for path in opts.images:
        img = _load(path)
        ratios = seam_ratios(img)
        rows.append({"file": path, "size": [img.shape[1], img.shape[0]],
                     "horizontal": round(ratios["horizontal"], 3), "vertical": round(ratios["vertical"], 3),
                     "verdict": verdict(ratios)})
    if opts.preview:
        from PIL import Image
        Image.fromarray((tile(_load(opts.images[0]), opts.tiles) * 255).astype(np.uint8)).save(opts.preview)
    print(json.dumps(rows, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
