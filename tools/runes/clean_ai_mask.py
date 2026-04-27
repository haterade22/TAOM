"""
Clean an AI-generated rune image into a stamper-ready mask.

Pipeline:
  1. Load the raw AI output (any size, any mode).
  2. Convert to grayscale.
  3. Threshold at 128 -> pure black/white.
  4. Median-filter to drop antialiased speckle.
  5. Downsample to 1024x1024 with LANCZOS.
  6. Save as 8-bit grayscale PNG.

Usage:
  python clean_ai_mask.py <input> <output>
  python clean_ai_mask.py --batch raw_ai/ masks/filler/

The mask convention: black (=0) = stamp lands here, white (=255) = passthrough.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image, ImageFilter


TARGET_SIZE = 1024
THRESHOLD = 128
MEDIAN_KERNEL = 3


def clean(src: Path, dst: Path) -> None:
    img = Image.open(src).convert("L")
    img = img.point(lambda p: 0 if p < THRESHOLD else 255, mode="L")
    img = img.filter(ImageFilter.MedianFilter(size=MEDIAN_KERNEL))
    img = img.resize((TARGET_SIZE, TARGET_SIZE), Image.LANCZOS)
    img = img.point(lambda p: 0 if p < THRESHOLD else 255, mode="L")
    dst.parent.mkdir(parents=True, exist_ok=True)
    img.save(dst, "PNG", optimize=True)
    print(f"  {src.name} -> {dst}")


def main(argv: list[str]) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("input", type=Path, help="Input PNG file or directory (with --batch)")
    p.add_argument("output", type=Path, help="Output PNG file or directory (with --batch)")
    p.add_argument("--batch", action="store_true", help="Treat input/output as directories")
    args = p.parse_args(argv)

    if args.batch:
        if not args.input.is_dir():
            p.error(f"--batch given but {args.input} is not a directory")
        sources = sorted(args.input.glob("*.png")) + sorted(args.input.glob("*.jpg")) + sorted(args.input.glob("*.jpeg"))
        if not sources:
            print(f"No images found in {args.input}", file=sys.stderr)
            return 1
        print(f"Cleaning {len(sources)} image(s):")
        for src in sources:
            clean(src, args.output / f"{src.stem}.png")
    else:
        if not args.input.is_file():
            p.error(f"{args.input} is not a file")
        clean(args.input, args.output)

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
