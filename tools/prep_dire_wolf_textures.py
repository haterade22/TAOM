#!/usr/bin/env python3
"""Convert the purchased ADOD_Beasts wolf textures to the 1K maps the dire wolf's Kit materials use (#1081).

    python tools/prep_dire_wolf_textures.py --src <ADOD_Beasts>/AssetSources/wolves/textures --dst <dir> [--apply]

A dry run lists what it would write. It reads the pack's files only and never overwrites a file, so a second run
with everything in place writes nothing. The set is Mike's (2026-11-04):
- `_d`: body and whiskers per skin, and four eyes: yellow, blue, red and white, with a default per skin. The pack
  has no red or white eye, and its white skin's eye is the blue map. Red is the yellow map with its green and blue
  channels scaled down. White is the blue map as brightened greyscale. Both keep the iris detail.
- The white skin's body is albino: the pack's white fur reads as pale grey (median 180), so it loses its tint and its
  levels rise, which keeps the fur detail.
- `_n` and `_s`: one each for body, whiskers and eye.
- The mouth keeps its own set, because the pack shares one mouth across its skins.

The one body `_s` is the grey skin's. The pack's black and grey share it (flat, gloss 106), and brown and white have a
glossier flat map (gloss 179), so brown and white come out more matte than in the pack.
"""
import argparse
import sys
from pathlib import Path

from PIL import Image

SKINS = {"grey": "Grey", "black": "Black", "brown": "Brown", "white": "White"}
PER_SKIN = {"body": "Body", "whiskers": "Wiskers"}
MAPS = ("d", "n", "s")
MAX_SIZE = 1024
RED_EYE_KEEP = 0.25
RED_EYE = "dire_wolf_eye_red_d.png"
WHITE_EYE_GAIN = 2.4
WHITE_EYE = "dire_wolf_eye_white_d.png"
ALBINO_BODY = "dire_wolf_white_body_d.png"
ALBINO_WHITE_POINT = 215
ALBINO_GAMMA = 0.8


def plan(src: Path, dst: Path) -> list[tuple[Path, Path]]:
    pairs = [(src / f"All_Mouth_{m}.png", dst / f"dire_wolf_mouth_{m}.png") for m in MAPS]
    pairs += [
        (src / "grey" / "Grey_Eye_d.png", dst / "dire_wolf_eye_yellow_d.png"),
        (src / "white" / "White_Eye_d.png", dst / "dire_wolf_eye_blue_d.png"),
        (src / "grey" / "Grey_Eye_d.png", dst / RED_EYE),
        (src / "white" / "White_Eye_d.png", dst / WHITE_EYE),
        (src / "grey" / "Grey_Eye_n.png", dst / "dire_wolf_eye_n.png"),
        (src / "grey" / "Grey_Eye_s.png", dst / "dire_wolf_eye_s.png"),
        (src / "grey" / "Grey_Wiskers_n.png", dst / "dire_wolf_whiskers_n.png"),
        (src / "grey" / "Grey_Wiskers_s.png", dst / "dire_wolf_whiskers_s.png"),
        (src / "grey" / "Grey_Body_n.png", dst / "dire_wolf_body_n.png"),
        (src / "grey" / "Grey_Body_s.png", dst / "dire_wolf_body_s.png"),
    ]
    for skin, folder in SKINS.items():
        for role, stem in PER_SKIN.items():
            pairs.append((src / skin / f"{folder}_{stem}_d.png", dst / f"dire_wolf_{skin}_{role}_d.png"))
    return pairs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--src", type=Path, required=True)
    parser.add_argument("--dst", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    pairs = plan(args.src, args.dst)
    missing = [str(s) for s, _ in pairs if not s.is_file()]
    if missing:
        print("missing source maps:\n  " + "\n  ".join(missing), file=sys.stderr)
        return 2
    todo = [(s, d) for s, d in pairs if not d.exists()]
    print(f"{len(pairs)} maps, {len(pairs) - len(todo)} already present, {len(todo)} to write")
    if not args.apply:
        print("dry run: pass --apply to write")
        return 0
    args.dst.mkdir(parents=True, exist_ok=True)
    for source, target in todo:
        with Image.open(source) as image:
            converted = image.convert("RGB").resize((MAX_SIZE, MAX_SIZE), Image.LANCZOS)
            if target.name == RED_EYE:
                r, g, b = converted.split()
                scale = lambda v: int(v * RED_EYE_KEEP)
                converted = Image.merge("RGB", (r, g.point(scale), b.point(scale)))
            elif target.name == WHITE_EYE:
                grey = converted.convert("L").point(lambda v: min(255, int(v * WHITE_EYE_GAIN)))
                converted = Image.merge("RGB", (grey, grey, grey))
            elif target.name == ALBINO_BODY:
                grey = converted.convert("L").point(
                    lambda v: min(255, int(255 * (v / ALBINO_WHITE_POINT) ** ALBINO_GAMMA)))
                converted = Image.merge("RGB", (grey, grey, grey))
            converted.save(target)
    print(f"wrote {len(todo)} maps to {args.dst}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
