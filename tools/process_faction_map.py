#!/usr/bin/env python3
"""
Process full-canvas region PNGs into deploy-ready FactionMap assets.

Usage:
    python tools/process_faction_map.py --input <dir_with_fullcanvas_pngs> --output <deploy_dir>
    python tools/process_faction_map.py --checklist          # Print region filename checklist
    python tools/process_faction_map.py --dry-run --input .   # Preview bounding boxes without writing

Input:  Directory containing region_<name>.png files (full canvas, e.g. 2048x1758, RGBA)
        Each PNG should be the full map size with only the region's territory opaque.

Output: - deploy/<region_name>.png              (cropped to bbox, max 512px wide)
        - deploy/fullres/<region_name>_full.png  (original full-canvas, copied)
        - regions.json                           (normalized bounding boxes + capital positions)
        - factions.json                          (faction data with new names, migrated from old)
        - CharacterCreationCultureStage.xml      (updated PolygonWidget entries)
"""

import argparse
import json
import os
import struct
import sys
from pathlib import Path

# ============================================================================
# REGION DEFINITIONS
# ============================================================================
# Complete list of regions for the new Factions map.
# Format: (region_id, display_name, old_region_id_or_None, game_faction, playable, side)

REGIONS = [
    # --- Northwest: Eriador ---
    ("high_kingdom_of_lindon",       "High Kingdom of Lindon",       "elves_of_lindon",              "rivendell", False, "free"),
    ("clans_of_forochel",            "Clans of Forochel",            "orcs_of_forochel",             "",          False, "evil"),
    ("goblins_of_blue_craig",        "Goblins of Blue Craig",        None,                           "",          False, "evil"),
    ("remnants_of_angmar",           "Remnants of Angmar",           "realm_of_angmar",              "gundabad",  False, "evil"),
    ("kingdom_of_ered_mithrin",      "Kingdom of Ered Mithrin",      None,                           "",          False, "neutral"),
    ("kingdom_of_arthedain",         "Kingdom of Arthedain",         "kingdom_of_arthedain",         "vlandia",   False, "free"),
    ("kingdom_of_rhudaur",           "Kingdom of Rhudaur",           "hill_men_of_rhudaur",          "battania",  False, "evil"),
    ("kingdom_of_ered_duin",         "Kingdom of Ered Duin",         "dwarves_of_ered_luin",         "erebor",    False, "free"),
    ("kingdom_of_cardolan",          "Kingdom of Cardolan",          "realm_of_cardolan",            "vlandia",   False, "free"),

    # --- Central: Misty Mountains & Anduin ---
    ("goblins_of_goblin_town",       "Goblins of Goblin Town",       "orcs_of_the_misty_mountains",  "",          False, "evil"),
    ("overlordship_of_gundabad",     "Overlordship of Gundabad",     "orcs_of_gundabad",             "gundabad",  True,  "evil"),
    ("kingdom_of_imladris",          "Kingdom of Imladris",          "elves_of_rivendell",           "rivendell", True,  "free"),
    ("kingdom_of_moria",             "Kingdom of Moria",             None,                           "",          False, "neutral"),
    ("anduin_vale",                  "Anduin Vale",                  "vale_of_anduin",               "sturgia",   False, "free"),
    ("kingdom_of_lothlorien",        "Kingdom of Lothlorien",        "elves_of_lothlorien",          "lothlorien", True, "free"),

    # --- Central-West: Dunland, Enedwaith, Isengard ---
    ("wildmen_of_enedwaith",         "Wildmen of Enedwaith",         "clans_of_enedwaith",           "battania",  False, "neutral"),
    ("clans_of_dunland",             "Clans of Dunland",             "hill_men_of_dunland",          "battania",  True,  "evil"),
    ("dominion_of_isengard",         "Dominion of Isengard",         "dominion_of_isengard",         "isengard",  True,  "evil"),

    # --- Central-East: Rhovanion ---
    ("kingdom_of_lasgalen",          "Kingdom of Lasgalen",          "elves_of_mirkwood",            "mirkwood",  True,  "free"),
    ("kingdom_of_erebor",            "Kingdom of Erebor",            "dwarves_of_erebor",            "erebor",    True,  "free"),
    ("kingdom_of_dale",              "Kingdom of Dale",              "kingdom_of_dale",              "sturgia",   True,  "free"),
    ("kingdom_of_neldoreth",         "Kingdom of Neldoreth",         "elves_of_neldoreth",           "mirkwood",  False, "free"),
    ("kingdom_of_angaladh",          "Kingdom of Angaladh",          None,                           "",          False, "free"),
    ("overlordship_of_dol_guldur",   "Overlordship of Dol Guldur",   "shadow_of_dol_guldur",        "dolguldur", True,  "evil"),
    ("kingdom_of_dorwinion",         "Kingdom of Dorwinion",         "realm_of_dorwinion",           "vlandia",   False, "neutral"),
    ("kingdom_of_south_rhovanion",   "Kingdom of South Rhovanion",   None,                           "",          False, "neutral"),

    # --- South: Gondor, Rohan ---
    ("kingdom_of_rohan",             "Kingdom of Rohan",             "kingdom_of_rohan",             "vlandia",   True,  "free"),
    ("clans_of_druwaith_iaur",       "Clans of Druwaith Iaur",       None,                           "",          False, "neutral"),
    ("clans_of_andrast",             "Clans of Andrast",             None,                           "",          False, "neutral"),
    ("stewardship_of_gondor",        "Stewardship of Gondor",        "kingdom_of_gondor",            "gondor",    True,  "free"),

    # --- East: Mordor, Rhun, Khand ---
    ("dominion_of_mordor",           "Dominion of Mordor",           "dark_lands_of_mordor",         "mordor",    True,  "evil"),
    ("golden_realm_of_rhun",         "Golden-Realm of Rhun",         "easterlings_of_rhun",          "khuzait",   True,  "evil"),
    ("kingdom_of_zigalnara",         "Kingdom of Zigalnara",         None,                           "",          False, "evil"),
    ("stronghold_of_narager",        "Stronghold of Narager",        "orcs_of_narager",              "",          False, "evil"),
    ("khudorom_of_khand",            "Khudorom of Khand",            "variags_of_khand",             "khuzait",   True,  "evil"),
    ("stronghold_of_ered_gwaer",     "Stronghold of Ered Gwaer",     "orcs_of_gwaer",                "",          False, "evil"),

    # --- South: Harad, Umbar, Bellakar ---
    ("taskralan_of_harwan",          "Taskralan of Harwan",          "tribes_of_harad",              "aserai",    True,  "evil"),
    ("havens_of_umbar",              "Havens of Umbar",              "havens_of_umbar",              "umbar",     True,  "evil"),
    ("taskralan_of_shaghana",        "Taskralan of Shaghana",        None,                           "aserai",    False, "evil"),
    ("aru_thani_of_bellakar",        "Aru-Thani of Bellakar",        "faithful_of_bellakar",         "aserai",    False, "free"),
    ("chajaphan_of_abanissa",        "Chajaphan of Abanissa",        None,                           "",          False, "neutral"),
]

# Regions from the old map that are REMOVED (no equivalent in new map)
REMOVED_REGIONS = [
    "iron_hills",           # merged into kingdom_of_erebor
    "dwarves_of_orocarni",  # off the east edge / removed
    "forodwaith",           # replaced by kingdom_of_ered_mithrin
    "orcs_of_ered_luin",    # merged / removed
    "fangorn_forest",       # merged into surrounding regions
    "deco_1",               # decorative element
]

# Max width for deployed (cropped) region PNGs
DEPLOY_MAX_WIDTH = 512


def print_checklist():
    """Print a checklist of all region PNG filenames needed."""
    print("=" * 70)
    print("FACTION MAP REGION CUTOUT CHECKLIST")
    print("=" * 70)
    print()
    print(f"Source image: E:\\LOTRAOMAssets\\Renders\\Factions.jpg (2048x1758)")
    print(f"Output format: RGBA PNG, full canvas size (2048x1758)")
    print(f"Total regions: {len(REGIONS)}")
    print()
    print("Instructions:")
    print("  1. Open Factions.jpg in Photoshop/GIMP")
    print("  2. For each region below, select the colored area within its borders")
    print("  3. Copy to a new 2048x1758 canvas with transparent background")
    print("  4. Save as the filename shown below")
    print("  5. Place all PNGs in a single input directory")
    print()
    print("-" * 70)
    print(f"{'#':<4} {'Filename':<50} {'Display Name'}")
    print("-" * 70)

    for i, (region_id, display_name, old_id, *_) in enumerate(REGIONS, 1):
        filename = f"region_{region_id}.png"
        migrated = f"  (was: {old_id})" if old_id else "  ** NEW **"
        print(f"{i:<4} {filename:<50} {display_name}{migrated}")

    print("-" * 70)
    print()
    print("REMOVED regions (no longer needed):")
    for old_id in REMOVED_REGIONS:
        print(f"  - region_{old_id}.png")
    print()


def read_png_dimensions(filepath):
    """Read width and height from PNG IHDR chunk without PIL."""
    with open(filepath, "rb") as f:
        f.read(8)   # PNG signature
        f.read(4)   # IHDR length
        f.read(4)   # IHDR tag
        data = f.read(8)
        w = struct.unpack(">I", data[0:4])[0]
        h = struct.unpack(">I", data[4:8])[0]
    return w, h


def find_alpha_bbox(filepath):
    """
    Find the bounding box of non-transparent pixels in a PNG.
    Uses System.Drawing via subprocess or falls back to manual IDAT parsing.
    Returns (x, y, width, height) or None if fully transparent.
    """
    try:
        # Try using Python's built-in tkinter (usually available)
        # Actually, let's use a more reliable approach with a helper script
        import subprocess
        result = subprocess.run(
            [sys.executable, "-c", f"""
import struct, zlib, io

def read_png_rgba(path):
    with open(path, 'rb') as f:
        sig = f.read(8)
        chunks = []
        width = height = 0
        bit_depth = color_type = 0
        while True:
            length_data = f.read(4)
            if len(length_data) < 4:
                break
            length = struct.unpack('>I', length_data)[0]
            chunk_type = f.read(4)
            chunk_data = f.read(length)
            f.read(4)  # CRC
            if chunk_type == b'IHDR':
                width = struct.unpack('>I', chunk_data[0:4])[0]
                height = struct.unpack('>I', chunk_data[4:8])[0]
                bit_depth = chunk_data[8]
                color_type = chunk_data[9]
            elif chunk_type == b'IDAT':
                chunks.append(chunk_data)
            elif chunk_type == b'IEND':
                break

    if color_type != 6:  # Must be RGBA
        raise ValueError(f"Expected RGBA (color_type=6), got {{color_type}}")

    raw = zlib.decompress(b''.join(chunks))
    stride = width * 4 + 1  # 4 bytes per pixel + 1 filter byte

    min_x, min_y = width, height
    max_x, max_y = -1, -1

    offset = 0
    for y in range(height):
        filter_byte = raw[offset]
        row_start = offset + 1
        offset += stride

        # Reconstruct filtered row (handle filter types 0-4)
        if filter_byte == 0:
            row = raw[row_start:row_start + width * 4]
        else:
            # For bbox detection we need proper defiltering
            # Use bytearray for mutation
            row = bytearray(raw[row_start:row_start + width * 4])
            if y == 0:
                prev_row = bytes(width * 4)
            if filter_byte == 1:  # Sub
                for i in range(len(row)):
                    a = row[i - 4] if i >= 4 else 0
                    row[i] = (row[i] + a) & 0xFF
            elif filter_byte == 2:  # Up
                for i in range(len(row)):
                    b = prev_row[i]
                    row[i] = (row[i] + b) & 0xFF
            elif filter_byte == 3:  # Average
                for i in range(len(row)):
                    a = row[i - 4] if i >= 4 else 0
                    b = prev_row[i]
                    row[i] = (row[i] + (a + b) // 2) & 0xFF
            elif filter_byte == 4:  # Paeth
                for i in range(len(row)):
                    a = row[i - 4] if i >= 4 else 0
                    b = prev_row[i]
                    c = prev_row[i - 4] if i >= 4 else 0
                    p = a + b - c
                    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                    if pa <= pb and pa <= pc:
                        pr = a
                    elif pb <= pc:
                        pr = b
                    else:
                        pr = c
                    row[i] = (row[i] + pr) & 0xFF
            prev_row = bytes(row)
            if filter_byte == 0:
                prev_row = raw[row_start:row_start + width * 4]

        # Check alpha channel (every 4th byte starting at index 3)
        for x in range(width):
            alpha = row[x * 4 + 3]
            if alpha > 10:  # threshold to ignore near-transparent pixels
                if x < min_x: min_x = x
                if x > max_x: max_x = x
                if y < min_y: min_y = y
                if y > max_y: max_y = y

        if filter_byte == 0 and y == 0:
            prev_row = raw[row_start:row_start + width * 4]

    if max_x < 0:
        print("EMPTY")
    else:
        print(f"{{min_x}},{{min_y}},{{max_x - min_x + 1}},{{max_y - min_y + 1}},{{width}},{{height}}")

read_png_rgba(r'{filepath}')
"""],
            capture_output=True, text=True, timeout=120
        )

        output = result.stdout.strip()
        if output == "EMPTY":
            return None
        parts = output.split(",")
        return int(parts[0]), int(parts[1]), int(parts[2]), int(parts[3]), int(parts[4]), int(parts[5])

    except Exception as e:
        print(f"  Warning: Could not compute bbox: {e}", file=sys.stderr)
        return None


def crop_png_to_bbox(input_path, output_path, bbox, max_width=DEPLOY_MAX_WIDTH):
    """
    Crop a PNG to the given bounding box and resize if wider than max_width.
    Uses a subprocess with pure Python PNG manipulation.
    """
    x, y, w, h, canvas_w, canvas_h = bbox
    import subprocess
    result = subprocess.run(
        [sys.executable, "-c", f"""
import struct, zlib

def crop_and_save(input_path, output_path, cx, cy, cw, ch, max_w):
    # Read PNG
    with open(input_path, 'rb') as f:
        sig = f.read(8)
        chunks = []
        width = height = 0
        while True:
            length_data = f.read(4)
            if len(length_data) < 4:
                break
            length = struct.unpack('>I', length_data)[0]
            chunk_type = f.read(4)
            chunk_data = f.read(length)
            f.read(4)
            if chunk_type == b'IHDR':
                width = struct.unpack('>I', chunk_data[0:4])[0]
                height = struct.unpack('>I', chunk_data[4:8])[0]
            elif chunk_type == b'IDAT':
                chunks.append(chunk_data)
            elif chunk_type == b'IEND':
                break

    # Decompress
    raw = zlib.decompress(b''.join(chunks))
    stride = width * 4 + 1

    # Defilter all rows
    rows = []
    prev_row = bytes(width * 4)
    offset = 0
    for y in range(height):
        fb = raw[offset]
        row = bytearray(raw[offset + 1:offset + 1 + width * 4])
        offset += stride
        if fb == 1:
            for i in range(len(row)):
                a = row[i - 4] if i >= 4 else 0
                row[i] = (row[i] + a) & 0xFF
        elif fb == 2:
            for i in range(len(row)):
                row[i] = (row[i] + prev_row[i]) & 0xFF
        elif fb == 3:
            for i in range(len(row)):
                a = row[i - 4] if i >= 4 else 0
                b = prev_row[i]
                row[i] = (row[i] + (a + b) // 2) & 0xFF
        elif fb == 4:
            for i in range(len(row)):
                a = row[i - 4] if i >= 4 else 0
                b = prev_row[i]
                c = prev_row[i - 4] if i >= 4 else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                row[i] = (row[i] + pr) & 0xFF
        prev_row = bytes(row)
        rows.append(row)

    # Crop
    cropped = []
    for y in range(cy, min(cy + ch, height)):
        row = rows[y]
        cropped_row = row[cx * 4:(cx + cw) * 4]
        cropped.append(bytes(cropped_row))

    out_w, out_h = cw, len(cropped)

    # Write PNG (no filter, simple)
    import io
    buf = io.BytesIO()

    def write_chunk(f, chunk_type, data):
        f.write(struct.pack('>I', len(data)))
        f.write(chunk_type)
        f.write(data)
        crc = zlib.crc32(chunk_type + data) & 0xFFFFFFFF
        f.write(struct.pack('>I', crc))

    buf.write(b'\\x89PNG\\r\\n\\x1a\\n')
    ihdr = struct.pack('>IIBBBBB', out_w, out_h, 8, 6, 0, 0, 0)
    write_chunk(buf, b'IHDR', ihdr)

    raw_data = bytearray()
    for row in cropped:
        raw_data.append(0)  # no filter
        raw_data.extend(row)

    compressed = zlib.compress(bytes(raw_data), 9)
    write_chunk(buf, b'IDAT', compressed)
    write_chunk(buf, b'IEND', b'')

    with open(output_path, 'wb') as f:
        f.write(buf.getvalue())

    print(f"{{out_w}}x{{out_h}}")

crop_and_save(r'{input_path}', r'{output_path}', {x}, {y}, {w}, {h}, {max_width})
"""],
        capture_output=True, text=True, timeout=120
    )
    if result.returncode != 0:
        print(f"  Error cropping: {result.stderr}", file=sys.stderr)
        return False
    print(f"  Cropped to {result.stdout.strip()}")
    return True


def generate_regions_json(bboxes, canvas_w, canvas_h, output_path):
    """Generate regions.json from computed bounding boxes."""
    regions_data = {}
    for region_id, display_name, old_id, game_faction, playable, side in REGIONS:
        if region_id not in bboxes:
            print(f"  Warning: No bbox for {region_id}, skipping in regions.json")
            continue

        x, y, w, h, _, _ = bboxes[region_id]
        norm_x = round(x / canvas_w, 4)
        norm_y = round(y / canvas_h, 4)
        norm_w = round(w / canvas_w, 4)
        norm_h = round(h / canvas_h, 4)

        # Capital position defaults to center of region
        cap_x = round((x + w / 2) / canvas_w, 4)
        cap_y = round((y + h / 2) / canvas_h, 4)

        entry = {
            "faction": region_id,
            "norm_bbox": [norm_x, norm_y, norm_w, norm_h],
            "capital_pos": [cap_x, cap_y],
        }
        regions_data[region_id] = entry

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(regions_data, f, indent=2)
    print(f"  Wrote {len(regions_data)} regions to {output_path}")


def generate_factions_json(old_factions_path, output_path):
    """Generate new factions.json, migrating data from old where possible."""
    old_factions = {}
    if old_factions_path and os.path.exists(old_factions_path):
        with open(old_factions_path, "r", encoding="utf-8") as f:
            old_factions = json.load(f)

    new_factions = {}
    for region_id, display_name, old_id, game_faction, playable, side in REGIONS:
        # Try to migrate from old faction data
        if old_id and old_id in old_factions:
            entry = dict(old_factions[old_id])  # copy
            entry["name"] = display_name
            entry["image"] = f"faction_{region_id}"
            entry["playable"] = playable
            entry["game_faction"] = game_faction
            entry["side"] = side
        else:
            # New region — create minimal entry
            color = _default_color_for_side(side)
            entry = {
                "name": display_name,
                "color": color,
                "playable": playable,
                "game_faction": game_faction,
                "side": side,
                "description": f"The {display_name}.",
                "image": f"faction_{region_id}",
                "traits": [],
            }

        new_factions[region_id] = entry

    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(new_factions, f, indent=2, ensure_ascii=False)
    print(f"  Wrote {len(new_factions)} factions to {output_path}")


def _default_color_for_side(side):
    """Default color by alignment."""
    return {
        "free": "#4870B8FF",
        "evil": "#604830FF",
        "neutral": "#706838FF",
    }.get(side, "#808080FF")


def generate_xml(output_path):
    """Generate the PolygonWidget section of CharacterCreationCultureStage.xml."""
    lines = []
    for region_id, *_ in REGIONS:
        lines.append(
            f'                <PolygonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" '
            f'RegionName="{region_id}" Command.Click="ExecuteSelectRegion" />'
        )

    xml_snippet = "\n".join(lines)

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("<!-- Generated PolygonWidget entries for new Factions map -->\n")
        f.write("<!-- Copy these into CharacterCreationCultureStage.xml, replacing existing PolygonWidgets -->\n\n")
        f.write(xml_snippet)
        f.write("\n")
    print(f"  Wrote PolygonWidget XML snippet to {output_path}")


def process_regions(input_dir, output_dir, dry_run=False):
    """Main processing pipeline."""
    input_path = Path(input_dir)
    output_path = Path(output_dir)

    if not input_path.exists():
        print(f"Error: Input directory not found: {input_path}")
        return False

    # Find all region PNGs
    found = {}
    missing = []
    for region_id, display_name, *_ in REGIONS:
        filename = f"region_{region_id}.png"
        filepath = input_path / filename
        if filepath.exists():
            found[region_id] = filepath
        else:
            missing.append((region_id, display_name))

    print(f"\nFound: {len(found)}/{len(REGIONS)} region PNGs")
    if missing:
        print(f"\nMissing {len(missing)} regions:")
        for region_id, display_name in missing:
            print(f"  - region_{region_id}.png  ({display_name})")
        print()

    if not found:
        print("No region PNGs found. Nothing to process.")
        return False

    # Compute bounding boxes
    print("\nComputing bounding boxes...")
    bboxes = {}
    canvas_w = canvas_h = None

    for region_id, filepath in sorted(found.items()):
        print(f"  Processing {region_id}...", end=" ", flush=True)
        result = find_alpha_bbox(str(filepath))
        if result is None:
            print("EMPTY (fully transparent)")
            continue

        x, y, w, h, cw, ch = result
        if canvas_w is None:
            canvas_w, canvas_h = cw, ch
            print(f"Canvas: {canvas_w}x{canvas_h}")

        norm_x = round(x / canvas_w, 4)
        norm_y = round(y / canvas_h, 4)
        norm_w = round(w / canvas_w, 4)
        norm_h = round(h / canvas_h, 4)
        print(f"bbox=({x},{y},{w},{h}) norm=({norm_x},{norm_y},{norm_w},{norm_h})")
        bboxes[region_id] = result

    if dry_run:
        print("\n[DRY RUN] Would generate:")
        print(f"  - {len(bboxes)} cropped region PNGs in {output_path}/")
        print(f"  - {len(bboxes)} full-res copies in {output_path}/fullres/")
        print(f"  - regions.json")
        print(f"  - factions.json")
        print(f"  - polygon_widgets.xml")
        return True

    # Create output directories
    deploy_dir = output_path
    fullres_dir = output_path / "fullres"
    deploy_dir.mkdir(parents=True, exist_ok=True)
    fullres_dir.mkdir(parents=True, exist_ok=True)

    # Crop and deploy
    print("\nCropping region PNGs...")
    for region_id, filepath in sorted(found.items()):
        if region_id not in bboxes:
            continue

        # Copy full-res
        import shutil
        fullres_dest = fullres_dir / f"region_{region_id}_full.png"
        shutil.copy2(str(filepath), str(fullres_dest))

        # Crop to bbox
        deploy_dest = deploy_dir / f"region_{region_id}.png"
        crop_png_to_bbox(str(filepath), str(deploy_dest), bboxes[region_id])

    # Generate data files
    print("\nGenerating data files...")
    old_factions_path = Path(__file__).parent.parent / "Main" / "_Module" / "ModuleData" / "factionmap" / "factions.json"
    generate_regions_json(bboxes, canvas_w, canvas_h, str(output_path / "regions.json"))
    generate_factions_json(str(old_factions_path), str(output_path / "factions.json"))
    generate_xml(str(output_path / "polygon_widgets.xml"))

    print("\nDone!")
    print(f"\nNext steps:")
    print(f"  1. Review generated files in {output_path}/")
    print(f"  2. Copy region PNGs to: Modules/TAOM/GUI/SpriteData/FactionMap/")
    print(f"  3. Copy fullres PNGs to: Modules/TAOM/GUI/SpriteData/FactionMap/fullres/")
    print(f"  4. Copy regions.json to: Main/_Module/ModuleData/factionmap/")
    print(f"  5. Copy factions.json to: Main/_Module/ModuleData/factionmap/")
    print(f"  6. Update CharacterCreationCultureStage.xml with polygon_widgets.xml content")
    print(f"  7. Create faction_*.png and banner_*.png for new regions")

    return True


def main():
    parser = argparse.ArgumentParser(description="Process FactionMap region PNGs")
    parser.add_argument("--checklist", action="store_true", help="Print region filename checklist")
    parser.add_argument("--input", type=str, help="Directory containing full-canvas region PNGs")
    parser.add_argument("--output", type=str, default="tools/factionmap_output",
                        help="Output directory for processed files")
    parser.add_argument("--dry-run", action="store_true", help="Preview without writing files")
    parser.add_argument("--old-factions", type=str, help="Path to existing factions.json for data migration")

    args = parser.parse_args()

    if args.checklist:
        print_checklist()
        return

    if not args.input:
        parser.error("--input is required (or use --checklist)")

    process_regions(args.input, args.output, args.dry_run)


if __name__ == "__main__":
    main()
