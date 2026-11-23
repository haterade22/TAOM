"""Build cb_army_plate.png, the army panel's plate (#1532), from the campaign stats plate art.

The stats plate (cb_stats_upper.png) has a leaf vine on its left that cannot be stretched, so the army plate is
cut from its other parts at the same pixel density (2.25 source px per UI px):

- right end: the plate's right end past its top step, raised so its chamfered top border lines up with the main
  top border; its straight right edge runs from row 64 to 144;
- left end: the right end mirrored;
- middle: the plain stone with straight top and bottom borders;
- extra height: rows from a stone band (a straight-edge band in the ends), reflected so each joint continues the rows beside it, with a short
  crossfade where the two halves meet.

Usage: python tools/make_army_overlay_plate.py --out <path to cb_army_plate.png>
"""

import argparse
from pathlib import Path

from PIL import Image

SOURCE = Path(__file__).resolve().parent.parent / "Main/_Module/GUI/FactionUI/RuntimeSprites/cb_stats_upper.png"

WIDTH, HEIGHT = 891, 527  # 396 x 234 UI px
RIGHT_FROM = 936  # clear of the top step and its curve; the lowered top border is row 37
RIGHT_RAISE = 22  # 37 - 15 lines the lowered border up with the main top border
MAIN = (206, 869)  # clear of the left vine and the top step; top and bottom borders are straight here
TOP_ROWS = 99  # rows 0..98 hold the top border
BOTTOM_FROM = 140  # rows 140..233 hold the bottom border and the corner leaves
EDGE_BAND = (72, 136)  # right end: straight edge, clear of the chamfer and the bulge at each end
STONE_BAND = (35, 185)  # middle: plain stone between the borders
SEAM = 12  # crossfade width in px


def reflect(start: int, step: int, lo: int, hi: int) -> int:
    """Row `step` rows on from `start` (negative goes up), bouncing inside [lo, hi)."""
    length = hi - lo
    k = (start - lo + step) % (2 * length)
    return lo + (k if k < length else 2 * length - 1 - k)


def column_strip(src: Image.Image, x0: int, x1: int, top_start: int, band: tuple) -> Image.Image:
    """One full-height strip: the top part from `top_start`, the band, then the bottom part.

    The bottom part starts at the band's end when that is above BOTTOM_FROM, so the run up into it starts on the
    row just above it. Every source row keeps its distance from the strip's bottom either way, so strips with
    different bands still line up."""
    bottom_from = min(BOTTOM_FROM, band[1])
    assert band[0] <= top_start + TOP_ROWS < band[1], "the run down does not start inside its band"
    assert band[0] <= bottom_from - 1 < band[1], "the run up does not start inside its band"
    bottom_rows = src.height - bottom_from
    middle = HEIGHT - TOP_ROWS - bottom_rows
    strip = Image.new("RGBA", (x1 - x0, HEIGHT))
    strip.paste(src.crop((x0, top_start, x1, top_start + TOP_ROWS)), (0, 0))
    joint = middle // 2
    for t in range(middle):
        # A run down from the top part and a run up into the bottom part, each continuous with its neighbour,
        # crossfaded over SEAM rows around the middle.
        down = reflect(top_start + TOP_ROWS, t, *band)
        up = reflect(bottom_from - 1, -(middle - 1 - t), *band)
        weight = min(1.0, max(0.0, (t - joint + SEAM / 2) / SEAM))
        row = Image.blend(src.crop((x0, down, x1, down + 1)), src.crop((x0, up, x1, up + 1)), weight)
        strip.paste(row, (0, TOP_ROWS + t))
    strip.paste(src.crop((x0, bottom_from, x1, src.height)), (0, HEIGHT - bottom_rows))
    return strip


def crossfade(left: Image.Image, right: Image.Image) -> Image.Image:
    """Join two strips side by side, blending the last SEAM columns of `left` into `right`."""
    out = Image.new("RGBA", (left.width + right.width - SEAM, HEIGHT))
    out.paste(left, (0, 0))
    out.paste(right, (left.width - SEAM, 0))
    for i in range(SEAM):
        x = left.width - SEAM + i
        a = left.crop((x, 0, x + 1, HEIGHT))
        b = right.crop((i, 0, i + 1, HEIGHT))
        out.paste(Image.blend(a, b, (i + 1) / (SEAM + 1)), (x, 0))
    return out


def build() -> Image.Image:
    src = Image.open(SOURCE).convert("RGBA")
    right = column_strip(src, RIGHT_FROM, src.width, RIGHT_RAISE, EDGE_BAND)
    left = right.transpose(Image.FLIP_LEFT_RIGHT)
    # The middle is wider than the clean stretch, so it joins that stretch's left and right halves.
    main_width = WIDTH - left.width - right.width + 2 * SEAM
    first = main_width // 2 + SEAM
    second = main_width - main_width // 2
    assert first <= MAIN[1] - MAIN[0] and second <= MAIN[1] - MAIN[0], "plate wider than the clean stone"
    main = crossfade(
        column_strip(src, MAIN[0], MAIN[0] + first, 0, STONE_BAND),
        column_strip(src, MAIN[1] - second, MAIN[1], 0, STONE_BAND))
    plate = crossfade(crossfade(left, main), right)
    assert plate.size == (WIDTH, HEIGHT), plate.size
    return plate


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    build().save(args.out)
    print(f"wrote {args.out} {WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
