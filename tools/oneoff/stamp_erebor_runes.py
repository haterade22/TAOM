"""
Stamp dwarven rune masks onto Erebor base PBR textures.

Reads a base PBR triple (`<base>_d.png`, `<base>_n.png`, `<base>_s.png`),
applies a black-on-white mask in one of three blend modes, and writes a
runic variant triple. Each channel is processed at its own resolution; the
mask is scaled per-channel.

Modes
-----
  carved   — engraved groove. Diffuse darkens, normal indents (concave),
             specular dampens. Use for weathered stone runes.
  gold     — warm yellow metal inlay. Bandos-Warforge style hero pieces.
  silver   — neutral cool metal inlay. Restrained heraldic decoration.
  bronze   — warm copper-orange metal inlay. Forge / smithing iconography.
  mithril  — pale silver-blue Tolkien "true-silver" inlay. The brilliant
             metal — highest specular, brightest highlight.

Metal modes share the same algorithm (replace base with metal RGB inside
mask + bevel highlight along edge + high specular) and differ only in their
RGB profile. See METALS dict for tunable colour values.

Mask convention
---------------
  black (=0)   = stamp lands here at full strength
  white (=255) = passthrough (base texture untouched)
  greys        = partial-strength stamp (handy for soft edges)

Usage
-----
  # Single stamp
  python stamp_erebor_runes.py \
      --base t_dw_wall_block_a1 \
      --mask runes/masks/hero/durin_seal.png \
      --mode incise \
      --out-name t_dw_wall_block_a1_runic

  # Manifest batch
  python stamp_erebor_runes.py --manifest tools/runes/manifest.json

The mask is centered and scaled to a configurable percentage of the texture
width. Future: --tile to tile the mask across the surface (for trim borders).
"""
from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

from PIL import Image, ImageFilter

import numpy as np


Mode = Literal["carved", "gold", "silver", "bronze", "mithril"]
MODES: tuple[Mode, ...] = ("carved", "gold", "silver", "bronze", "mithril")


# ---------- mode parameters (calibrated to read at gameplay distance) ----------

@dataclass(frozen=True)
class MetalProfile:
    """Tunable RGB profile for one metal-inlay mode."""
    body: tuple[int, int, int]        # diffuse fill colour inside mask
    highlight: tuple[int, int, int]   # bevel highlight along mask edge
    spec: tuple[int, int, int]        # specular RGB inside mask
    normal_depth: float = +0.10       # raised bevel only (edges; flat fill)


METALS: dict[str, MetalProfile] = {
    # Warm yellow metal — Bandos-Warforge / classic dwarven hoard look.
    "gold":    MetalProfile((212, 175, 55),  (252, 235, 140), (245, 220, 180)),
    # Neutral cool metal — restrained, slightly desaturated.
    "silver":  MetalProfile((190, 195, 200), (235, 238, 240), (220, 222, 225)),
    # Warm copper-orange metal — forge / smithy / older hero pieces.
    "bronze":  MetalProfile((140, 90, 50),   (200, 140, 90),  (195, 155, 110)),
    # Tolkien "true-silver" — pale silver-blue, brightest of all metals.
    "mithril": MetalProfile((210, 220, 230), (245, 248, 252), (240, 245, 250)),
}

# Carved-groove (non-metal) parameters.
CARVED_DIFFUSE_MULT = 0.40                          # darkens to 40% inside mask
CARVED_NORMAL_DEPTH = -0.45                         # negative = indent
CARVED_SPEC_MULT    = (0.70, 0.85, 1.00)            # roughened spec inside mask

# Default stamp placement (single-stamp; manifest entries can override).
DEFAULT_SCALE = 0.55            # mask occupies 55% of texture width
DEFAULT_CENTER = (0.5, 0.5)     # normalized texture coords

# Auto-crop threshold: pixels >= this value are considered "white margin"
# and will be cropped before scaling. AI outputs from MJ/Recraft have
# whitespace around the actual mark; cropping it gives correct on-texture sizing.
AUTO_CROP_WHITE_THRESHOLD = 240


@dataclass(frozen=True)
class Placement:
    kind: str = "centered"                    # "centered" | "band"
    scale: float = DEFAULT_SCALE              # centered: mask width as fraction of texture width
    center: tuple[float, float] = DEFAULT_CENTER  # centered: normalized (x, y)
    band_y_start: float = 0.0                 # band: top of band (0..1)
    band_y_end: float = 1.0                   # band: bottom of band (0..1)
    tile: bool = False                        # band: tile horizontally (else stretch full width)
    auto_crop: bool = True                    # crop white margin around mask before placement


# ---------- mask -> per-channel stamp helpers ----------

def _auto_crop(mask: Image.Image, threshold: int = AUTO_CROP_WHITE_THRESHOLD) -> Image.Image:
    """Crop white margins around a black-on-white mask. No-op if mask is all white."""
    arr = np.asarray(mask.convert("L"), dtype=np.uint8)
    nonwhite = arr < threshold
    if not nonwhite.any():
        return mask
    rows = np.where(nonwhite.any(axis=1))[0]
    cols = np.where(nonwhite.any(axis=0))[0]
    top, bottom = int(rows[0]), int(rows[-1]) + 1
    left, right = int(cols[0]), int(cols[-1]) + 1
    return mask.crop((left, top, right, bottom))


def _compose_centered(mask: Image.Image, target_size: tuple[int, int],
                      scale: float, center: tuple[float, float]) -> Image.Image:
    """Center a mask on the canvas at `scale` fraction of canvas width."""
    tw, th = target_size
    stamp_w = max(1, int(round(tw * scale)))
    stamp_h = max(1, int(round(stamp_w * mask.height / mask.width)))
    scaled = mask.resize((stamp_w, stamp_h), Image.LANCZOS)
    canvas = Image.new("L", (tw, th), 255)
    cx, cy = center
    x0 = int(round(cx * tw - stamp_w / 2))
    y0 = int(round(cy * th - stamp_h / 2))
    canvas.paste(scaled, (x0, y0))
    return canvas


def _compose_band(mask: Image.Image, target_size: tuple[int, int],
                  y_start: float, y_end: float, tile: bool) -> Image.Image:
    """Stamp a mask as a horizontal band spanning the texture's full width.

    The band sits at vertical position [y_start, y_end] (normalized 0..1).
    If `tile=True`, the mask is scaled to the band height and tiled horizontally
    (preserving aspect — natural for horizontally-repeating trim patterns).
    If `tile=False`, the mask is stretched to fit the full width.
    """
    tw, th = target_size
    band_h = max(1, int(round((y_end - y_start) * th)))
    y0 = int(round(y_start * th))
    canvas = Image.new("L", (tw, th), 255)

    if tile:
        scale_factor = band_h / mask.height
        tile_w = max(1, int(round(mask.width * scale_factor)))
        scaled_tile = mask.resize((tile_w, band_h), Image.LANCZOS)
        x = 0
        while x < tw:
            canvas.paste(scaled_tile, (x, y0))
            x += tile_w
    else:
        stretched = mask.resize((tw, band_h), Image.LANCZOS)
        canvas.paste(stretched, (0, y0))

    return canvas


def _scale_mask(mask: Image.Image, target_size: tuple[int, int], placement: Placement) -> Image.Image:
    """Place a black-on-white mask onto a canvas of `target_size`, returning a
    grayscale image where black = stamp, white = passthrough.
    """
    if placement.auto_crop:
        mask = _auto_crop(mask)

    if placement.kind == "centered":
        return _compose_centered(mask, target_size, placement.scale, placement.center)
    if placement.kind == "band":
        return _compose_band(mask, target_size,
                             placement.band_y_start, placement.band_y_end, placement.tile)
    raise ValueError(f"unknown placement kind: {placement.kind!r} (expected 'centered' or 'band')")


def _mask_to_strength(mask_canvas: Image.Image) -> np.ndarray:
    """Convert mask canvas to a strength array in [0..1] (0 = no stamp, 1 = full)."""
    arr = np.asarray(mask_canvas, dtype=np.float32) / 255.0
    return 1.0 - arr  # invert: black -> 1.0, white -> 0.0


def _edge_gradient(strength: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Return (gx, gy) sobel-ish gradients of the strength field, normalized."""
    sx = np.zeros_like(strength)
    sy = np.zeros_like(strength)
    sx[:, 1:-1] = 0.5 * (strength[:, 2:] - strength[:, :-2])
    sy[1:-1, :] = 0.5 * (strength[2:, :] - strength[:-2, :])
    mag = np.sqrt(sx * sx + sy * sy) + 1e-6
    sx = sx / mag.max() if mag.max() > 0 else sx
    sy = sy / mag.max() if mag.max() > 0 else sy
    return sx, sy


# ---------- diffuse / normal / specular stampers ----------

def stamp_diffuse(base: Image.Image, mask_canvas: Image.Image, mode: Mode) -> Image.Image:
    arr = np.asarray(base, dtype=np.float32)
    strength = _mask_to_strength(mask_canvas)[..., None]  # (H, W, 1)

    if mode == "carved":
        out = arr * (1.0 - strength * (1.0 - CARVED_DIFFUSE_MULT))
    elif mode in METALS:
        profile = METALS[mode]
        body = np.array(profile.body, dtype=np.float32)
        highlight = np.array(profile.highlight, dtype=np.float32)
        out = arr * (1.0 - strength) + body * strength
        gx, gy = _edge_gradient(strength.squeeze(-1))
        edge = np.clip(np.sqrt(gx * gx + gy * gy) * 6.0, 0.0, 1.0)[..., None]
        out = out * (1.0 - edge * 0.6) + highlight * edge * 0.6
    else:
        raise ValueError(f"unknown mode: {mode}")

    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def stamp_normal(base: Image.Image, mask_canvas: Image.Image, mode: Mode) -> Image.Image:
    """Modulate a tangent-space normal map by the mask gradient.

    Tangent normals: R = nx (signed), G = ny (signed), B = nz (mostly +1).
    Encoded so 128 = 0; 255 = +1; 0 = -1.
    """
    arr = np.asarray(base, dtype=np.float32)
    nx = (arr[..., 0] - 128.0) / 127.0
    ny = (arr[..., 1] - 128.0) / 127.0
    nz = (arr[..., 2] - 128.0) / 127.0

    strength = _mask_to_strength(mask_canvas)
    gx, gy = _edge_gradient(strength)

    if mode == "carved":
        depth = CARVED_NORMAL_DEPTH
    elif mode in METALS:
        depth = METALS[mode].normal_depth
    else:
        raise ValueError(f"unknown mode: {mode}")

    # Sign convention: positive depth = bumps stick out (raised); negative = sink (carved).
    nx = nx + gx * depth * 8.0
    ny = ny + gy * depth * 8.0

    length = np.sqrt(nx * nx + ny * ny + np.maximum(nz, 0.05) ** 2)
    nx /= length
    ny /= length
    nz_out = np.maximum(np.sqrt(np.clip(1.0 - nx * nx - ny * ny, 0.0, 1.0)), 0.0)

    out = np.stack([
        (nx * 127.0 + 128.0),
        (ny * 127.0 + 128.0),
        (nz_out * 127.0 + 128.0),
    ], axis=-1)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


def stamp_specular(base: Image.Image, mask_canvas: Image.Image, mode: Mode) -> Image.Image:
    arr = np.asarray(base, dtype=np.float32)
    strength = _mask_to_strength(mask_canvas)[..., None]

    if mode == "carved":
        mult = np.array(CARVED_SPEC_MULT, dtype=np.float32)
        out = arr * (1.0 - strength * (1.0 - mult))
    elif mode in METALS:
        target = np.array(METALS[mode].spec, dtype=np.float32)
        out = arr * (1.0 - strength) + target * strength
    else:
        raise ValueError(f"unknown mode: {mode}")

    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB")


# ---------- entry-point ----------

def stamp(
    base_dir: Path,
    base: str,
    mask_path: Path,
    mode: Mode,
    out_dir: Path,
    out_name: str,
    placement: Placement = Placement(),
) -> list[Path]:
    """Process all available channels of `<base>_*` and write `<out_name>_*`."""
    if mode not in MODES:
        raise ValueError(f"mode must be one of {MODES}, got {mode!r}")

    mask = Image.open(mask_path).convert("L")
    written: list[Path] = []

    channel_processors = {
        "d": stamp_diffuse,
        "n": stamp_normal,
        "s": stamp_specular,
    }

    for ch, processor in channel_processors.items():
        src = base_dir / f"{base}_{ch}.png"
        if not src.exists():
            print(f"  skip {ch}: {src.name} not found")
            continue
        base_img = Image.open(src).convert("RGB")
        canvas = _scale_mask(mask, base_img.size, placement)
        out_img = processor(base_img, canvas, mode)
        dst = out_dir / f"{out_name}_{ch}.png"
        dst.parent.mkdir(parents=True, exist_ok=True)
        out_img.save(dst, "PNG", optimize=True)
        written.append(dst)
        print(f"  {ch}: {src.name} -> {dst.name}  ({base_img.size[0]}x{base_img.size[1]}, mode={mode})")

    return written


# ---------- CLI ----------

def _placement_from_args(args: argparse.Namespace) -> Placement:
    return Placement(
        kind=args.placement,
        scale=args.scale,
        center=(args.center_x, args.center_y),
        band_y_start=args.band_y_start,
        band_y_end=args.band_y_end,
        tile=args.tile,
        auto_crop=not args.no_auto_crop,
    )


def cmd_single(args: argparse.Namespace) -> int:
    placement = _placement_from_args(args)
    written = stamp(
        base_dir=args.base_dir,
        base=args.base,
        mask_path=args.mask,
        mode=args.mode,
        out_dir=args.out_dir,
        out_name=args.out_name or f"{args.base}_runic",
        placement=placement,
    )
    print(f"Wrote {len(written)} channel(s).")
    return 0 if written else 1


def _placement_from_entry(entry: dict) -> Placement:
    """Build a Placement from a manifest entry, supporting both the legacy
    flat schema (`scale` / `center`) and the new `placement` block.
    """
    p = entry.get("placement", {}) or {}
    return Placement(
        kind=p.get("kind", "centered"),
        scale=p.get("scale", entry.get("scale", DEFAULT_SCALE)),
        center=tuple(p.get("center", entry.get("center", DEFAULT_CENTER))),
        band_y_start=p.get("band_y_start", 0.0),
        band_y_end=p.get("band_y_end", 1.0),
        tile=p.get("tile", False),
        auto_crop=p.get("auto_crop", True),
    )


def cmd_manifest(args: argparse.Namespace) -> int:
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    base_dir = Path(manifest["base_dir"])
    out_dir = Path(manifest["out_dir"])
    mask_root = Path(manifest.get("mask_root", "tools/runes/masks"))

    failed = 0
    for entry in manifest["stamps"]:
        if entry.get("skip"):
            continue
        print(f"\n[{entry['out_name']}]  base={entry['base']}  mask={entry['mask']}  mode={entry['mode']}")
        mask_path = (mask_root / entry["mask"]).resolve()
        if not mask_path.exists():
            print(f"  ! mask missing: {mask_path}")
            failed += 1
            continue
        try:
            stamp(
                base_dir=base_dir,
                base=entry["base"],
                mask_path=mask_path,
                mode=entry["mode"],
                out_dir=out_dir,
                out_name=entry["out_name"],
                placement=_placement_from_entry(entry),
            )
        except Exception as exc:
            print(f"  ! failed: {exc}")
            failed += 1
    return 0 if failed == 0 else 2


def main(argv: list[str]) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd")

    # legacy single-shot mode (no subcommand)
    p.add_argument("--base-dir", type=Path,
                   default=Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor\textures"),
                   help="Directory holding the base PBR triples")
    p.add_argument("--base", type=str, help="Base texture stem (e.g. t_dw_wall_block_a1)")
    p.add_argument("--mask", type=Path, help="Path to the black-on-white mask PNG")
    p.add_argument("--mode", choices=MODES, help="Blend mode")
    p.add_argument("--out-dir", type=Path,
                   default=Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\erebor\textures"),
                   help="Output directory")
    p.add_argument("--out-name", type=str, help="Output stem; defaults to <base>_runic")
    p.add_argument("--placement", choices=("centered", "band"), default="centered",
                   help="Placement kind. centered=stamp at (--center-x, --center-y) at --scale of width. "
                        "band=stamp as a horizontal band between --band-y-start and --band-y-end.")
    p.add_argument("--scale", type=float, default=DEFAULT_SCALE, help="Centered: mask width as fraction of texture width")
    p.add_argument("--center-x", type=float, default=DEFAULT_CENTER[0])
    p.add_argument("--center-y", type=float, default=DEFAULT_CENTER[1])
    p.add_argument("--band-y-start", type=float, default=0.0, help="Band: top of band (0..1)")
    p.add_argument("--band-y-end", type=float, default=1.0, help="Band: bottom of band (0..1)")
    p.add_argument("--tile", action="store_true", help="Band: tile horizontally instead of stretching")
    p.add_argument("--no-auto-crop", action="store_true", help="Skip auto-crop of white margin around mask")
    p.add_argument("--manifest", type=Path, help="Run a JSON manifest of stamps instead of one-shot")

    args = p.parse_args(argv)
    if args.manifest:
        return cmd_manifest(args)
    if args.base and args.mask and args.mode:
        return cmd_single(args)
    p.print_help()
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
