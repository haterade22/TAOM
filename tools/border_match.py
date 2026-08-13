#!/usr/bin/env python3
"""
Position regions by matching their alpha-channel outlines to the black
border lines on the source map.
"""

import json
import os
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# Same two out-of-repo inputs as assemble_faction_map.py, and the same reason for
# carrying no default: the old ones named one machine's Downloads folder and asset drive.
FULL_MAP_PATH = os.environ.get("TAOM_MAP_IMAGE", "")
REGIONS_DIR = os.environ.get("TAOM_REGIONS_DIR", "")
OUTPUT_DIR = os.environ.get("TAOM_FACTION_MAP_OUTPUT", str(Path(__file__).parent / "factionmap_output"))
BORDER_THRESHOLD = 25  # pixels darker than this are "border"

# Import region mappings from assemble script
sys.path.insert(0, str(Path(__file__).parent))
from assemble_faction_map import FILE_TO_REGION, REGION_META, require_inputs


def extract_alpha_edge(alpha, thickness=2):
    """Extract the boundary pixels of an alpha mask."""
    mask = alpha > 10
    h, w = mask.shape
    edge = np.zeros_like(mask)
    for dy in range(-thickness, thickness + 1):
        for dx in range(-thickness, thickness + 1):
            if dy == 0 and dx == 0:
                continue
            shifted = np.zeros_like(mask)
            sy = max(0, dy)
            ey = min(h, h + dy)
            sx = max(0, dx)
            ex = min(w, w + dx)
            shifted[sy - dy:ey - dy, sx - dx:ex - dx] = mask[sy:ey, sx:ex]
            # Edge = opaque pixel next to a transparent pixel
            edge |= (mask & ~shifted)
    return edge


def score_position(border_map, edge_mask, x, y):
    """Count how many edge pixels align with border pixels at (x, y)."""
    eh, ew = edge_mask.shape
    bh, bw = border_map.shape
    if y < 0 or x < 0 or y + eh > bh or x + ew > bw:
        return -1
    patch = border_map[y:y+eh, x:x+ew]
    return np.sum(patch & edge_mask)


def downsample_binary(arr, factor):
    """Downsample binary array - a block is True if any pixel is True."""
    h, w = arr.shape
    nh, nw = h // factor, w // factor
    cropped = arr[:nh * factor, :nw * factor]
    return cropped.reshape(nh, factor, nw, factor).any(axis=(1, 3))


def find_position(border_map, edge_mask, initial_pos=None, search_radius=40):
    """Find best position via border-edge overlap."""
    eh, ew = edge_mask.shape
    bh, bw = border_map.shape
    n_edge = np.sum(edge_mask)

    if n_edge < 20:
        print(f"    Warning: only {n_edge} edge pixels")
        return None

    if initial_pos is not None:
        # Refine around known position
        cx, cy = initial_pos
        best_score = -1
        best_pos = (cx, cy)
        for y in range(max(0, cy - search_radius), min(bh - eh + 1, cy + search_radius + 1)):
            for x in range(max(0, cx - search_radius), min(bw - ew + 1, cx + search_radius + 1)):
                score = score_position(border_map, edge_mask, x, y)
                if score > best_score:
                    best_score = score
                    best_pos = (x, y)
        return best_pos, best_score, n_edge
    else:
        # Coarse search at 1/4 resolution, then refine
        ds = 4
        border_small = downsample_binary(border_map, ds)
        edge_small = downsample_binary(edge_mask, ds)
        seh, sew = edge_small.shape
        sbh, sbw = border_small.shape

        best_score = -1
        best_pos = None
        for y in range(sbh - seh + 1):
            for x in range(sbw - sew + 1):
                patch = border_small[y:y+seh, x:x+sew]
                score = np.sum(patch & edge_small)
                if score > best_score:
                    best_score = score
                    best_pos = (x, y)

        if best_pos is None:
            return None

        # Refine at full resolution
        cx, cy = best_pos[0] * ds, best_pos[1] * ds
        sr = ds * 2
        best_score_fine = -1
        best_pos_fine = (cx, cy)
        for y in range(max(0, cy - sr), min(bh - eh + 1, cy + sr + 1)):
            for x in range(max(0, cx - sr), min(bw - ew + 1, cx + sr + 1)):
                score = score_position(border_map, edge_mask, x, y)
                if score > best_score_fine:
                    best_score_fine = score
                    best_pos_fine = (x, y)

        return best_pos_fine, best_score_fine, n_edge


def main():
    # Pass this module's own values: require_inputs would otherwise validate
    # assemble_faction_map's copies, which are separate reads of the same variables.
    require_inputs(FULL_MAP_PATH, REGIONS_DIR)
    print("=" * 70)
    print("BORDER-LINE MATCHING")
    print("=" * 70)

    # Load source map and create border map
    print(f"\nLoading source map: {FULL_MAP_PATH}")
    full_map = np.array(Image.open(FULL_MAP_PATH).convert("RGB"))
    gray = np.mean(full_map, axis=2)
    border_map = gray < BORDER_THRESHOLD
    canvas_h, canvas_w = full_map.shape[:2]
    border_count = np.sum(border_map)
    print(f"  Map size: {canvas_w}x{canvas_h}")
    print(f"  Border pixels (gray < {BORDER_THRESHOLD}): {border_count} ({100*border_count/gray.size:.1f}%)")

    # Load current regions.json for seeding
    regions_path = Path(OUTPUT_DIR) / "regions.json"
    with open(regions_path) as f:
        regions = json.load(f)
    print(f"  Loaded {len(regions)} current region positions for seeding")

    # Process each region
    deploy_dir = Path(OUTPUT_DIR) / "deploy"
    fullres_dir = deploy_dir / "fullres"
    results = []

    input_files = sorted(os.listdir(REGIONS_DIR))
    png_files = [f for f in input_files if f.endswith(".png")]
    total = len(png_files)
    count = 0

    for filename in png_files:
        stem = filename[:-4]
        if stem not in FILE_TO_REGION:
            continue

        region_id = FILE_TO_REGION[stem]
        count += 1

        if region_id == "map_boundary":
            print(f"\n[{count}/{total}] {filename} -> {region_id} (decorative, skipping)")
            continue

        # Load region and extract alpha edge
        region_path = os.path.join(REGIONS_DIR, filename)
        region_img = Image.open(region_path).convert("RGBA")
        region_arr = np.array(region_img)
        rw, rh = region_img.size

        alpha = region_arr[:, :, 3]
        edge = extract_alpha_edge(alpha, thickness=2)
        n_edge = np.sum(edge)

        print(f"\n[{count}/{total}] {filename} -> {region_id} ({rw}x{rh}, {n_edge} edge pixels)")

        # Get seed position from current regions.json
        initial_pos = None
        if region_id in regions:
            bbox = regions[region_id]["norm_bbox"]
            initial_pos = (int(bbox[0] * canvas_w), int(bbox[1] * canvas_h))
            print(f"  Seed: ({initial_pos[0]}, {initial_pos[1]})")

        # Find position by border matching
        if initial_pos:
            print(f"  Refining (+/-40px)...", end=" ", flush=True)
            result = find_position(border_map, edge, initial_pos, search_radius=40)
        else:
            print(f"  Full search...", end=" ", flush=True)
            result = find_position(border_map, edge)

        if result is None:
            print("FAILED")
            results.append((region_id, filename, None, 0, 0))
            continue

        (x, y), score, n_edge_total = result
        overlap_pct = 100 * score / n_edge_total if n_edge_total > 0 else 0
        quality = "GOOD" if overlap_pct > 30 else ("OK" if overlap_pct > 15 else "POOR")

        moved = ""
        if initial_pos:
            dx = x - initial_pos[0]
            dy = y - initial_pos[1]
            moved = f" (moved {dx:+d},{dy:+d})"

        print(f"({x}, {y}) - {score}/{n_edge_total} edge pixels on border = {overlap_pct:.0f}% ({quality}){moved}")

        # Update regions.json
        regions[region_id] = {
            "faction": region_id,
            "norm_bbox": [round(x/canvas_w, 4), round(y/canvas_h, 4), round(rw/canvas_w, 4), round(rh/canvas_h, 4)],
            "capital_pos": [round((x + rw/2)/canvas_w, 4), round((y + rh/2)/canvas_h, 4)],
        }

        # Update deploy PNGs
        region_img.save(str(deploy_dir / f"region_{region_id}.png"), "PNG")
        fullres_img = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        fullres_img.paste(region_img, (x, y))
        fullres_img.save(str(fullres_dir / f"region_{region_id}_full.png"), "PNG")

        results.append((region_id, filename, (x, y), overlap_pct, score))

    # Save updated regions.json
    with open(regions_path, "w", encoding="utf-8") as f:
        json.dump(regions, f, indent=2)
    print(f"\nUpdated {regions_path}")

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    for rid, fn, pos, pct, score in sorted(results, key=lambda r: r[3]):
        if pos is None:
            print(f"  FAILED: {fn} ({rid})")
        else:
            quality = "GOOD" if pct > 30 else ("OK" if pct > 15 else "POOR")
            print(f"  {quality:4s} {pct:5.1f}%  ({pos[0]:4d},{pos[1]:4d})  {fn}")


if __name__ == "__main__":
    main()
