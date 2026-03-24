#!/usr/bin/env python3
"""
Assemble FactionMap from cropped region PNGs + full map image.

Takes cropped region PNGs (arbitrary names) and the full map PNG,
locates each region on the map via template matching, then generates:
  - Renamed region PNGs (region_{id}.png) ready for deployment
  - Full-canvas PNGs (fullres/region_{id}_full.png)
  - regions.json with normalized bounding boxes
  - factions.json migrated from old data
  - polygon_widgets.xml snippet for the UI

Usage:
    python tools/assemble_faction_map.py
"""

import json
import os
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# ============================================================================
# CONFIGURATION
# ============================================================================

FULL_MAP_PATH = r"C:\Users\mikew\Downloads\Vista_dot-settlements-with-borders.png"
REGIONS_DIR = r"E:\LOTRAOMAssets\Culture Selection"
OUTPUT_DIR = r"C:\Users\mikew\source\repos\TAOM\tools\factionmap_output"

# Mapping: user filename (without .png) -> region_id
FILE_TO_REGION = {
    "Abanissa":                       "chajaphan_of_abanissa",
    "Andrast":                        "clans_of_andrast",
    "Anduin_Vale":                    "anduin_vale",
    "Angaladh":                       "kingdom_of_angaladh",
    "Angmar":                         "remnants_of_angmar",
    "Arthedain":                      "kingdom_of_arthedain",
    "Bellakar":                       "aru_thani_of_bellakar",
    "Blue_Crag":                      "goblins_of_blue_craig",
    "Browlands (not playable)":       "browlands",
    "Cardolan":                       "kingdom_of_cardolan",
    "Dale":                           "kingdom_of_dale",
    "Dol_Guldur":                     "overlordship_of_dol_guldur",
    "Dorwinion":                      "kingdom_of_dorwinion",
    "Druwaith_Iaur":                  "clans_of_druwaith_iaur",
    "Dunland":                        "clans_of_dunland",
    "Enedwaith":                      "wildmen_of_enedwaith",
    "Erebor":                         "kingdom_of_erebor",
    "Ered_Gwaer":                     "stronghold_of_ered_gwaer",
    "Ered_Luin":                      "kingdom_of_ered_duin",
    "Ered_Mithrin":                   "kingdom_of_ered_mithrin",
    "Eregion (not playable)":         "eregion",
    "Fangorn_Forest (not playable)":  "fangorn_forest",
    "Forochel":                       "clans_of_forochel",
    "Goblin_Town":                    "goblins_of_goblin_town",
    "Gondor":                         "stewardship_of_gondor",
    "Gundabad":                       "overlordship_of_gundabad",
    "Harwan":                         "taskralan_of_harwan",
    "Imladris":                       "kingdom_of_imladris",
    "Isengard":                       "dominion_of_isengard",
    "Khand":                          "khudorom_of_khand",
    "Lindon":                         "high_kingdom_of_lindon",
    "Lothlorien":                     "kingdom_of_lothlorien",
    "Map_Boundary":                   "map_boundary",
    "Mirkwood_Realm":                 "kingdom_of_lasgalen",
    "Mordor":                         "dominion_of_mordor",
    "Moria":                          "kingdom_of_moria",
    "Narager":                        "stronghold_of_narager",
    "Neldoreth":                      "kingdom_of_neldoreth",
    "Nurunkhizdin":                   "nurunkhizdin",
    "Rhudaur":                        "kingdom_of_rhudaur",
    "Rhun":                           "golden_realm_of_rhun",
    "Rohan":                          "kingdom_of_rohan",
    "Shaghana":                       "taskralan_of_shaghana",
    "South_Rhovanion":                "kingdom_of_south_rhovanion",
    "Umbar":                          "havens_of_umbar",
    "Zigalnara":                      "kingdom_of_zigalnara",
}

# Region metadata: region_id -> (display_name, old_region_id, game_faction, playable, side)
REGION_META = {
    "high_kingdom_of_lindon":       ("High Kingdom of Lindon",       "elves_of_lindon",              "rivendell",  False, "free"),
    "clans_of_forochel":            ("Clans of Forochel",            "orcs_of_forochel",             "",           False, "evil"),
    "goblins_of_blue_craig":        ("Goblins of Blue Craig",        None,                           "",           False, "evil"),
    "remnants_of_angmar":           ("Remnants of Angmar",           "realm_of_angmar",              "gundabad",   False, "evil"),
    "kingdom_of_ered_mithrin":      ("Kingdom of Ered Mithrin",      None,                           "",           False, "neutral"),
    "kingdom_of_arthedain":         ("Kingdom of Arthedain",         "kingdom_of_arthedain",         "vlandia",    False, "free"),
    "kingdom_of_rhudaur":           ("Kingdom of Rhudaur",           "hill_men_of_rhudaur",          "battania",   False, "evil"),
    "kingdom_of_ered_duin":         ("Kingdom of Ered Duin",         "dwarves_of_ered_luin",         "erebor",     False, "free"),
    "kingdom_of_cardolan":          ("Kingdom of Cardolan",          "realm_of_cardolan",            "vlandia",    False, "free"),
    "goblins_of_goblin_town":       ("Goblins of Goblin Town",       "orcs_of_the_misty_mountains",  "",           False, "evil"),
    "overlordship_of_gundabad":     ("Overlordship of Gundabad",     "orcs_of_gundabad",             "gundabad",   True,  "evil"),
    "kingdom_of_imladris":          ("Kingdom of Imladris",          "elves_of_rivendell",           "rivendell",  True,  "free"),
    "kingdom_of_moria":             ("Kingdom of Moria",             None,                           "",           False, "evil"),
    "anduin_vale":                  ("Anduin Vale",                  "vale_of_anduin",               "sturgia",    False, "free"),
    "kingdom_of_lothlorien":        ("Kingdom of Lothlorien",        "elves_of_lothlorien",          "lothlorien", True,  "free"),
    "wildmen_of_enedwaith":         ("Wildmen of Enedwaith",         "clans_of_enedwaith",           "battania",   False, "neutral"),
    "clans_of_dunland":             ("Clans of Dunland",             "hill_men_of_dunland",          "battania",   True,  "evil"),
    "dominion_of_isengard":         ("Dominion of Isengard",         "dominion_of_isengard",         "isengard",   True,  "evil"),
    "kingdom_of_lasgalen":          ("Kingdom of Lasgalen",          "elves_of_mirkwood",            "mirkwood",   True,  "free"),
    "kingdom_of_erebor":            ("Kingdom of Erebor",            "dwarves_of_erebor",            "erebor",     True,  "free"),
    "kingdom_of_dale":              ("Kingdom of Dale",              "kingdom_of_dale",              "sturgia",    True,  "free"),
    "kingdom_of_neldoreth":         ("Kingdom of Neldoreth",         "elves_of_neldoreth",           "mirkwood",   False, "free"),
    "kingdom_of_angaladh":          ("Kingdom of Angaladh",          None,                           "",           False, "free"),
    "overlordship_of_dol_guldur":   ("Overlordship of Dol Guldur",  "shadow_of_dol_guldur",         "dolguldur",  True,  "evil"),
    "kingdom_of_dorwinion":         ("Kingdom of Dorwinion",         "realm_of_dorwinion",           "vlandia",    False, "neutral"),
    "kingdom_of_south_rhovanion":   ("Kingdom of South Rhovanion",   None,                           "",           False, "neutral"),
    "kingdom_of_rohan":             ("Kingdom of Rohan",             "kingdom_of_rohan",             "vlandia",    True,  "free"),
    "clans_of_druwaith_iaur":       ("Clans of Druwaith Iaur",       None,                           "",           False, "neutral"),
    "clans_of_andrast":             ("Clans of Andrast",             None,                           "",           False, "neutral"),
    "stewardship_of_gondor":        ("Stewardship of Gondor",        "kingdom_of_gondor",            "gondor",     True,  "free"),
    "dominion_of_mordor":           ("Dominion of Mordor",           "dark_lands_of_mordor",         "mordor",     True,  "evil"),
    "golden_realm_of_rhun":         ("Golden-Realm of Rhun",         "easterlings_of_rhun",          "khuzait",    True,  "evil"),
    "kingdom_of_zigalnara":         ("Kingdom of Zigalnara",         None,                           "",           False, "evil"),
    "stronghold_of_narager":        ("Stronghold of Narager",        "orcs_of_narager",              "",           False, "evil"),
    "khudorom_of_khand":            ("Khudorom of Khand",            "variags_of_khand",             "khuzait",    True,  "neutral"),
    "stronghold_of_ered_gwaer":     ("Stronghold of Ered Gwaer",     "orcs_of_gwaer",                "",           False, "evil"),
    "taskralan_of_harwan":          ("Taskralan of Harwan",          "tribes_of_harad",              "aserai",     True,  "evil"),
    "havens_of_umbar":              ("Havens of Umbar",              "havens_of_umbar",              "umbar",      True,  "neutral"),
    "taskralan_of_shaghana":        ("Taskralan of Shaghana",        None,                           "aserai",     False, "evil"),
    "aru_thani_of_bellakar":        ("Aru-Thani of Bellakar",        "faithful_of_bellakar",         "aserai",     False, "free"),
    "chajaphan_of_abanissa":        ("Chajaphan of Abanissa",        None,                           "",           False, "neutral"),
    "nurunkhizdin":                 ("Nurunkhizdin",                 None,                           "erebor",     False, "neutral"),
    "browlands":                    ("Browlands",                    None,                           "",           False, "neutral"),
    "eregion":                      ("Eregion",                      None,                           "",           False, "neutral"),
    "fangorn_forest":               ("Fangorn Forest",               "fangorn_forest",               "",           False, "neutral"),
    "map_boundary":                 ("Map Boundary",                 None,                           "",           False, "neutral"),
}


def ncc_at(full_gray, region_gray, mask, x, y):
    """Compute masked normalized cross-correlation at a given position."""
    rh, rw = region_gray.shape[:2]
    patch = full_gray[y:y+rh, x:x+rw]
    p_masked = patch[mask]
    r_masked = region_gray[mask]
    p_mean = p_masked.mean()
    p_std = p_masked.std() + 1e-6
    r_mean = r_masked.mean()
    r_std = r_masked.std() + 1e-6
    return np.sum(((p_masked - p_mean) / p_std) * ((r_masked - r_mean) / r_std))


def downsample(arr, factor):
    """Downsample an array by averaging factor x factor blocks."""
    h, w = arr.shape[:2]
    new_h = h // factor
    new_w = w // factor
    cropped = arr[:new_h * factor, :new_w * factor]
    if arr.ndim == 3:
        return cropped.reshape(new_h, factor, new_w, factor, -1).mean(axis=(1, 3)).astype(np.float32)
    return cropped.reshape(new_h, factor, new_w, factor).mean(axis=(1, 3)).astype(np.float32)


def find_region_position_ncc(full_gray, region_arr, initial_pos=None, search_radius=32):
    """
    Find region position using normalized cross-correlation on grayscale.
    If initial_pos is provided, only searches within search_radius of it.
    Otherwise does a coarse full-map search then refines.
    Returns ((x, y), ncc_score) or None.
    """
    alpha = region_arr[:, :, 3]
    mask = alpha > 10
    if np.sum(mask) < 100:
        print(f"    Warning: Only {np.sum(mask)} opaque pixels")
        return None

    region_gray = np.mean(region_arr[:, :, :3], axis=2).astype(np.float32)
    rh, rw = region_gray.shape[:2]
    fh, fw = full_gray.shape[:2]

    if rh > fh or rw > fw:
        print(f"    Error: Region ({rw}x{rh}) larger than map ({fw}x{fh})")
        return None

    if initial_pos is not None:
        # Refine around known position
        cx, cy = initial_pos
        best_score = -float('inf')
        best_pos = (cx, cy)
        for y in range(max(0, cy - search_radius), min(fh - rh + 1, cy + search_radius + 1)):
            for x in range(max(0, cx - search_radius), min(fw - rw + 1, cx + search_radius + 1)):
                score = ncc_at(full_gray, region_gray, mask, x, y)
                if score > best_score:
                    best_score = score
                    best_pos = (x, y)
        return best_pos, best_score
    else:
        # Full coarse search at 1/8 resolution, then refine
        ds = 8
        region_small_gray = downsample(region_gray[:, :, np.newaxis], ds)[:, :, 0]
        full_small_gray = downsample(full_gray[:, :, np.newaxis], ds)[:, :, 0]

        mh, mw = mask.shape
        new_mh, new_mw = mh // ds, mw // ds
        mask_cropped = mask[:new_mh * ds, :new_mw * ds]
        mask_small = mask_cropped.reshape(new_mh, ds, new_mw, ds).any(axis=(1, 3))

        srh, srw = region_small_gray.shape[:2]
        sfh, sfw = full_small_gray.shape[:2]

        best_score = -float('inf')
        best_pos = None
        for y in range(sfh - srh + 1):
            for x in range(sfw - srw + 1):
                score = ncc_at(full_small_gray, region_small_gray, mask_small, x, y)
                if score > best_score:
                    best_score = score
                    best_pos = (x, y)

        if best_pos is None:
            return None

        # Refine at full resolution
        cx, cy = best_pos[0] * ds, best_pos[1] * ds
        search_range = ds * 2
        best_score_fine = -float('inf')
        best_pos_fine = (cx, cy)
        for y in range(max(0, cy - search_range), min(fh - rh + 1, cy + search_range + 1)):
            for x in range(max(0, cx - search_range), min(fw - rw + 1, cx + search_range + 1)):
                score = ncc_at(full_gray, region_gray, mask, x, y)
                if score > best_score_fine:
                    best_score_fine = score
                    best_pos_fine = (x, y)

        return best_pos_fine, best_score_fine


def default_color_for_side(side):
    return {
        "free": "#4870B8FF",
        "evil": "#604830FF",
        "neutral": "#706838FF",
    }.get(side, "#808080FF")


def main():
    print("=" * 70)
    print("FACTION MAP ASSEMBLER")
    print("=" * 70)

    # Load full map for NCC matching (grayscale)
    print(f"\nLoading full map: {FULL_MAP_PATH}")
    full_map = Image.open(FULL_MAP_PATH).convert("RGBA")
    full_map_arr = np.array(full_map)
    canvas_w, canvas_h = full_map.size
    print(f"  Map size: {canvas_w}x{canvas_h}")
    full_gray = np.mean(full_map_arr[:, :, :3], axis=2).astype(np.float32)

    # Load existing regions.json (42 entries from previous run) for seeding
    current_regions_path = Path(__file__).parent.parent / "Main" / "_Module" / "ModuleData" / "factionmap" / "regions.json"
    seed_positions = {}
    if current_regions_path.exists():
        with open(current_regions_path, "r", encoding="utf-8") as f:
            current_regions = json.load(f)
        for region_id, rdata in current_regions.items():
            bbox = rdata["norm_bbox"]
            seed_positions[region_id] = (int(bbox[0] * canvas_w), int(bbox[1] * canvas_h))
        print(f"  Loaded {len(seed_positions)} seed positions from current regions.json")

    # Create output directories
    output_path = Path(OUTPUT_DIR)
    deploy_dir = output_path / "deploy"
    fullres_dir = output_path / "deploy" / "fullres"
    deploy_dir.mkdir(parents=True, exist_ok=True)
    fullres_dir.mkdir(parents=True, exist_ok=True)

    # Process each region
    regions_data = {}
    results = []

    input_files = sorted(os.listdir(REGIONS_DIR))
    total = len([f for f in input_files if f.endswith('.png')])
    count = 0

    for filename in input_files:
        if not filename.endswith('.png'):
            continue

        stem = filename[:-4]  # remove .png
        if stem not in FILE_TO_REGION:
            print(f"\n  WARNING: Unknown file '{filename}', skipping")
            continue

        region_id = FILE_TO_REGION[stem]
        count += 1
        print(f"\n[{count}/{total}] {filename} -> {region_id}")

        # Load region image
        region_path = os.path.join(REGIONS_DIR, filename)
        region_img = Image.open(region_path).convert("RGBA")
        region_arr = np.array(region_img)
        rw, rh = region_img.size
        print(f"  Size: {rw}x{rh}")

        # Map boundary is a decorative overlay, not a region — skip template matching
        if region_id == "map_boundary":
            deploy_dest = deploy_dir / f"region_{region_id}.png"
            region_img.save(str(deploy_dest), "PNG")
            fullres_dest = fullres_dir / f"region_{region_id}_full.png"
            region_img.save(str(fullres_dest), "PNG")
            regions_data[region_id] = {
                "faction": region_id,
                "norm_bbox": [0.0, 0.0, round(rw / canvas_w, 4), round(rh / canvas_h, 4)],
                "capital_pos": [0.5, 0.5],
            }
            results.append((region_id, filename, (0, 0), 0.0, rw, rh))
            print(f"  Decorative overlay — saved directly (no template matching)")
            continue

        # Determine initial position from backup
        initial_pos = seed_positions.get(region_id)
        if initial_pos:
            print(f"  Seeding from backup position: ({initial_pos[0]}, {initial_pos[1]})")

        # Find position using NCC (normalized cross-correlation on grayscale)
        # NCC handles color differences between region art and source map
        if initial_pos:
            print(f"  Refining position (±32px NCC)...", end=" ", flush=True)
            result = find_region_position_ncc(full_gray, region_arr, initial_pos, search_radius=32)
        else:
            print(f"  Full map search (new region, NCC)...", end=" ", flush=True)
            result = find_region_position_ncc(full_gray, region_arr)

        if result is None:
            print("FAILED - could not locate")
            results.append((region_id, filename, None, None, rw, rh))
            continue

        (x, y), ncc_score = result
        n_pixels = max(np.sum(region_arr[:, :, 3] > 10), 1)
        norm_score = ncc_score / n_pixels
        confidence = "GOOD" if norm_score > 0.5 else ("OK" if norm_score > 0.3 else "POOR")
        print(f"found at ({x}, {y}) - NCC: {norm_score:.3f} ({confidence})")
        avg_error = norm_score  # store for results

        # Store bbox
        norm_x = round(x / canvas_w, 4)
        norm_y = round(y / canvas_h, 4)
        norm_w = round(rw / canvas_w, 4)
        norm_h = round(rh / canvas_h, 4)
        cap_x = round((x + rw / 2) / canvas_w, 4)
        cap_y = round((y + rh / 2) / canvas_h, 4)

        regions_data[region_id] = {
            "faction": region_id,
            "norm_bbox": [norm_x, norm_y, norm_w, norm_h],
            "capital_pos": [cap_x, cap_y],
        }

        results.append((region_id, filename, (x, y), avg_error, rw, rh))

        # Save cropped deploy PNG (already cropped, just rename)
        deploy_dest = deploy_dir / f"region_{region_id}.png"
        region_img.save(str(deploy_dest), "PNG")

        # Create full-canvas PNG for fullres
        fullres_img = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
        fullres_img.paste(region_img, (x, y))
        fullres_dest = fullres_dir / f"region_{region_id}_full.png"
        fullres_img.save(str(fullres_dest), "PNG")

    # Write regions.json
    regions_json_path = output_path / "regions.json"
    with open(regions_json_path, "w", encoding="utf-8") as f:
        json.dump(regions_data, f, indent=2)
    print(f"\nWrote {len(regions_data)} regions to {regions_json_path}")

    # Write factions.json (migrate from current deployed version, then old vanilla)
    current_factions_path = Path(__file__).parent.parent / "Main" / "_Module" / "ModuleData" / "factionmap" / "factions.json"
    current_factions = {}
    if current_factions_path.exists():
        with open(current_factions_path, "r", encoding="utf-8") as f:
            current_factions = json.load(f)

    new_factions = {}
    for region_id, (display_name, old_id, game_faction, playable, side) in REGION_META.items():
        if region_id == "map_boundary":
            continue  # decorative, not a faction
        # First try to migrate from current factions (already has migrated data)
        if region_id in current_factions:
            entry = dict(current_factions[region_id])
            # Update fields that may have changed
            entry["name"] = display_name
            entry["playable"] = playable
            entry["game_faction"] = game_faction
            entry["side"] = side
        elif old_id and old_id in current_factions:
            entry = dict(current_factions[old_id])
            entry["name"] = display_name
            entry["image"] = f"faction_{region_id}"
            entry["playable"] = playable
            entry["game_faction"] = game_faction
            entry["side"] = side
        else:
            entry = {
                "name": display_name,
                "color": default_color_for_side(side),
                "playable": playable,
                "game_faction": game_faction,
                "side": side,
                "description": f"The {display_name}.",
                "image": f"faction_{region_id}",
                "traits": [],
            }
        new_factions[region_id] = entry

    factions_json_path = output_path / "factions.json"
    with open(factions_json_path, "w", encoding="utf-8") as f:
        json.dump(new_factions, f, indent=2, ensure_ascii=False)
    print(f"Wrote {len(new_factions)} factions to {factions_json_path}")

    # Write PolygonWidget XML snippet
    xml_lines = ['<!-- Generated PolygonWidget entries for new Factions map -->',
                 '<!-- Replace existing PolygonWidgets in CharacterCreationCultureStage.xml -->', '']
    for region_id in regions_data:
        xml_lines.append(
            f'                <PolygonWidget WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" '
            f'RegionName="{region_id}" Command.Click="ExecuteSelectRegion" />'
        )

    xml_path = output_path / "polygon_widgets.xml"
    with open(xml_path, "w", encoding="utf-8") as f:
        f.write("\n".join(xml_lines) + "\n")
    print(f"Wrote PolygonWidget XML to {xml_path}")

    # Summary
    print("\n" + "=" * 70)
    print("SUMMARY")
    print("=" * 70)
    found = sum(1 for _, _, pos, _, _, _ in results if pos is not None)
    failed = sum(1 for _, _, pos, _, _, _ in results if pos is None)
    print(f"  Matched: {found}/{len(results)}")
    if failed:
        print(f"  Failed:  {failed}")
        for rid, fn, pos, _, _, _ in results:
            if pos is None:
                print(f"    - {fn} ({rid})")

    # Show any low-confidence matches
    low_conf = [(rid, fn, score) for rid, fn, pos, score, _, _ in results if pos and score is not None and score < 0.3]
    if low_conf:
        print(f"\n  Low confidence matches (review manually):")
        for rid, fn, score in low_conf:
            print(f"    - {fn} ({rid}): NCC={score:.3f}")

    print(f"\nOutput directory: {OUTPUT_DIR}")
    print(f"\nNext steps:")
    print(f"  1. Review region positions in regions.json")
    print(f"  2. Copy deploy/*.png to Modules/TAOM/GUI/SpriteData/FactionMap/")
    print(f"  3. Copy deploy/fullres/*.png to Modules/TAOM/GUI/SpriteData/FactionMap/fullres/")
    print(f"  4. Copy regions.json + factions.json to Main/_Module/ModuleData/factionmap/")
    print(f"  5. Update CharacterCreationCultureStage.xml with polygon_widgets.xml")
    print(f"  6. Create faction_*.png and banner_*.png for new regions")


if __name__ == "__main__":
    main()
