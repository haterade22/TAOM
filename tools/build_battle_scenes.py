#!/usr/bin/env python3
"""Generate Main/_Module/ModuleData/sp_battle_scenes.xml from a Middle-earth region table.

The campaign map's worldmap_battle_scene_grid paints each map cell with an index (red channel); a field
battle on that cell picks with equal odds among the scenes whose map_indices list the index. REGIONS
below is the source of truth: (region, cells, scenes). A cell in several regions gets the union of their
scenes. Cells no region claims fall to the catch-all battle_terrain_r, so 0-255 stays fully covered.

Native scene attributes (terrain, forest_density, TerrainTypes) come from vanilla SandBox's
sp_battle_scenes.xml. Scenes vanilla's list does not define (TAOM's own, and battle_terrain_r, which
SandBoxCore ships but vanilla lists only inside a comment) are described in CUSTOM. How the cell to
region table was derived: docs/reference/worldmap-battle-scene-grid.md, "Region table". Adding a scene:
docs/modding/recipe-add-a-field-battle-scene.md.

    python tools/build_battle_scenes.py            # dry run: summary, shared cells, would it change
    python tools/build_battle_scenes.py --apply    # write the XML (UTF-8, no BOM, CRLF)
    python tools/build_battle_scenes.py --check    # exit 1 if the file is stale or a SceneObj is missing

The engine's loader runs int.Parse on every map_indices token and nothing catches a throw there, so a
bad table would stop every campaign from starting. The tool refuses to write one: every region needs at
least one cell in 0-255, no scene is written without cells, and the output must parse.
"""
from __future__ import annotations

import argparse
import collections
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from _gamedir import ensure_exists, game_modules

REPO = Path(__file__).resolve().parent.parent
OUT = REPO / "Main" / "_Module" / "ModuleData" / "sp_battle_scenes.xml"
MODULES: Path = game_modules(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
VANILLA_XML = MODULES / "SandBox" / "ModuleData" / "sp_battle_scenes.xml"


def rng(a: int, b: int) -> list[int]:
    return list(range(a, b + 1))


T = "battle_terrain_"
MORDOR = ["taom_mordor_battle_001_forceatmo", "taom_mordor_battle_002_forceatmo",
          "taom_mordor_battle_003_forceatmo", "taom_mordor_battle_004_forceatmo"]
FOREST = [T + "001", T + "h", T + "k", T + "004"]
MOUNTAIN = [T + "O", T + "031"]

REGIONS: list[tuple[str, list[int], list[str]]] = [
    # Gondor
    ("Gondor: Pelennor and Anorien", [85, 86, 90, 91, 92], [T + "a", T + "n", T + "t", T + "023", T + "025"]),
    ("Gondor: Anduin crossings", [85, 93, 97], [T + "f", T + "s", T + "011"]),
    ("Gondor: Belfalas, Dol Amroth, Anfalas, Andrast coast", rng(100, 108),
     [T + "p", T + "021", T + "029", T + "030"]),
    ("Gondor: Lamedon, Ringlo, Morthond", [98, 99], MOUNTAIN),
    ("Ithilien", [94, 95, 96], FOREST),
    # Rohan, Dunland, Isengard
    ("Rohan", [80, 81, 87, 88, 89],
     [T + "z", T + "003", T + "026", T + "028", T + "032", T + "biome_012", T + "biome_030",
      "taom_rohan_battle_001_forceatmo"]),
    ("Rohan: Fords of Isen", [62, 88, 89], ["taom_rohan_battle_fords_of_isen_forceatmo"]),
    ("Dunland and Enedwaith", [52, 57, 58, 59, 60, 61, 62],
     [T + "J", T + "L", T + "biome_022", T + "biome_058", T + "biome_085", T + "biome_088", T + "031"]),
    ("Isengard", [62, 63], MOUNTAIN + [T + "biome_098"]),
    # Elves and forests
    ("Lothlorien", [65, 80], [T + "001", T + "h", T + "k"]),
    ("Rivendell", [42], [T + "001", T + "h"]),
    ("Mirkwood and the Woodland Realm", [25, 45, 46, 54, 55], FOREST),
    ("Dol Guldur", [66], [T + "h", T + "k", T + "004", T + "biome_046"]),
    # Mountains
    ("Misty Mountains, Moria, Gundabad, Ettenmoors", [22, 23, 24, 42, 43, 44, 53, 64], MOUNTAIN + [T + "004", T + "k"]),
    ("Grey Mountains", rng(13, 17), MOUNTAIN + [T + "033"]),
    ("Emyn Muil", [79], MOUNTAIN),
    # North and east
    ("Dale", [46, 47, 56],
     [T + "m", T + "biome_034", T + "biome_053", T + "biome_114", T + "f", T + "biome_047", T + "biome_048"]),
    ("Erebor and the Iron Hills", [26, 27, 28, 46, 48],
     [T + "033", T + "biome_025", T + "biome_027", T + "biome_065", T + "O"]),
    ("Rhun", [20, 29, 30, 49, 50, 51, 69, 70, 71, 74, 75, 76, 77, 78, 160],
     [T + "012", T + "014", T + "017", T + "biome_005", T + "biome_006", T + "biome_040", T + "biome_044",
      T + "biome_050", T + "biome_075", T + "biome_076", T + "biome_083"]),
    ("Rhun: sand patch", [49, 50], [T + "009", T + "biome_067"]),
    ("Far east wastes", [18, 19, 21, 159], [T + "012", T + "014", T + "017"]),
    ("Forodwaith", rng(8, 11), [T + "033", T + "biome_005", T + "biome_006"]),
    ("Brown Lands", [67, 68], [T + "biome_067", T + "008", T + "033"]),
    # South
    ("Khand", [72, 73, 148, 149, 151, 152, 153, 154, 155, 156, 178],
     [T + "biome_089", T + "biome_094", T + "biome_096", T + "biome_097", T + "biome_106", T + "biome_107",
      T + "biome_148", T + "022"]),
    ("Near Harad", [109, 110, 111, 112, 113, 114, 115, 116, 127, 131, 172, 176, 177, 178],
     [T + "010", T + "b", T + "d", T + "008", T + "biome_057", T + "biome_063", T + "biome_128", T + "biome_131"]),
    ("Harad", [114, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 128, 129, 130, 132, 133, 134, 135, 144, 145],
     [T + "009", T + "022", T + "biome_067", T + "b", T + "d", T + "g", T + "010"]),
    ("Far Harad", rng(136, 143) + [146, 147, 150, 157, 158],
     [T + "biome_128", T + "biome_130", T + "biome_131", T + "biome_148", T + "017", T + "biome_144",
      T + "biome_149"]),
    # 255 is the grid's unpainted area: Umbar's settlements plus the sea and the parchment edge.
    ("Umbar", [255], [T + "d", T + "b", T + "p", T + "029"]),
    # Mordor. 176-178 hold Harad and Khand settlements and no Mordor one, so they stay out.
    ("Mordor", rng(161, 175) + [179], MORDOR),
    ("Mordor: Barad-dur", [77], MORDOR),
    ("Mordor: the Morannon", [84], ["taom_mordor_battle_black_gates_forceatmo", "taom_mordor_battle_001_forceatmo"]),
    ("Dead Marshes", [82, 83],
     ["taom_mordor_battle_dead_marshes_forceatmo", T + "005", T + "034", T + "biome_092", T + "biome_087b",
      T + "biome_093b"]),
    # Eriador, Arnor, Lindon
    ("Eriador and Arnor", [2, 6, 7, 12, 31, 32, 34, 35, 40, 41, 180],
     [T + "biome_144", T + "biome_149", T + "biome_056", T + "biome_028", T + "a", T + "t"]),
    ("Lindon and the west coast", [1, 36, 37, 38, 39], [T + "p", T + "021", T + "029", T + "biome_056"]),
    ("Ered Luin", [3, 4, 5, 33], [T + "O", T + "h", T + "031", T + "020"]),
]

CATCH_ALL = T + "r"

# Scenes vanilla's live list does not define: (terrain, forest_density or None). The engine reads
# terrain only when no scene lists a cell, which full coverage prevents; forest_density never.
CUSTOM: dict[str, tuple[str, str | None]] = {
    **{sid: ("Swamp", "Low") for sid in MORDOR},
    "taom_mordor_battle_black_gates_forceatmo": ("Swamp", "Low"),
    "taom_mordor_battle_dead_marshes_forceatmo": ("Swamp", "Low"),
    "taom_rohan_battle_001_forceatmo": ("Plain", "Low"),
    "taom_rohan_battle_fords_of_isen_forceatmo": ("Plain", "Low"),
    CATCH_ALL: ("Plain", None),
}

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<SPBattleScenes>
\t<!-- GENERATED by tools/build_battle_scenes.py: edit its REGIONS table and re-run, not this file.
\t     Field-battle scenes per Middle-earth region; the cell to region table is in
\t     docs/reference/worldmap-battle-scene-grid.md ("Region table"). A cell listed by several scenes
\t     picks one with equal odds. Every index 0-255 is covered; cells no region claims fall to
\t     battle_terrain_r. The eight taom_* scenes were re-enabled 2026-10-03 knowing their pbr_terrain
\t     shader can crash some GPUs on scene load (disabled 2026-06-19 in ee2cb04b and 62470413; #287). -->
"""


def load_vanilla(path: Path) -> dict[str, ET.Element]:
    """Live scene definitions in vanilla's list. ElementTree skips the commented-out ones."""
    return {s.get("id"): s for s in ET.parse(path).getroot().findall("Scene")}


def assign(regions) -> tuple[dict[str, set[int]], dict[str, list[str]]]:
    """Scene -> cells and scene -> region names, with the catch-all taking every unclaimed cell."""
    cells_of: dict[str, set[int]] = collections.defaultdict(set)
    regions_of: dict[str, list[str]] = collections.defaultdict(list)
    for name, cells, scenes in regions:
        if "--" in name:
            raise ValueError(f"region {name!r}: '--' cannot appear in an XML comment")
        bad = [c for c in cells if not (type(c) is int and 0 <= c <= 255)]
        if not cells or bad:
            raise ValueError(f"region {name!r}: needs at least one cell, every cell 0-255 (bad: {bad})")
        for sid in scenes:
            cells_of[sid].update(cells)
            regions_of[sid].append(name)
    unclaimed = set(range(256)) - set().union(*cells_of.values())
    if unclaimed:  # an empty catch-all would be written as map_indices="", which the engine throws on
        cells_of[CATCH_ALL].update(unclaimed)
        regions_of[CATCH_ALL].append("catch-all for unclaimed cells")
    return cells_of, regions_of


def overlaps(regions) -> dict[int, list[str]]:
    """Cells claimed by more than one region: each gets the union of their scenes."""
    owners: dict[int, list[str]] = collections.defaultdict(list)
    for name, cells, _ in regions:
        for c in cells:
            if name not in owners[c]:
                owners[c].append(name)
    return {c: names for c, names in sorted(owners.items()) if len(names) > 1}


def _emit(sid, cells, regions, vanilla, custom) -> list[str]:
    if sid in custom:
        terrain, forest = custom[sid]
        attrs = [("terrain", terrain)] + ([("forest_density", forest)] if forest else [])
        terrain_types: list[str] = []
    else:
        el = vanilla[sid]
        attrs = [(k, el.get(k)) for k in ("terrain", "forest_density") if el.get(k) is not None]
        terrain_types = [t.get("name") for t in el.iter("TerrainType")]
    idx = ", ".join(str(i) for i in sorted(cells))
    lines = [f"\t<!-- {'; '.join(dict.fromkeys(regions))} -->", "\t<Scene", f'\t\tid="{sid}"']
    lines += [f'\t\t{k}="{v}"' for k, v in attrs]
    if not terrain_types:
        return lines + [f'\t\tmap_indices="{idx}"></Scene>']
    lines += [f'\t\tmap_indices="{idx}">', "\t\t<TerrainTypes>"]
    for name in terrain_types:
        lines += ["\t\t\t<TerrainType", f'\t\t\t\tname="{name}" />']
    return lines + ["\t\t</TerrainTypes>", "\t</Scene>"]


def render(regions, vanilla: dict[str, ET.Element], custom) -> str:
    """The whole file as text, or ValueError for a table the engine could not load."""
    cells_of, regions_of = assign(regions)
    unknown = [s for s in cells_of if s not in vanilla and s not in custom]
    if unknown:
        raise ValueError(f"scene ids neither in vanilla's live list nor CUSTOM: {unknown}")
    lines = HEADER.splitlines()
    for sid, cells in cells_of.items():  # first appearance in the table (unclaimed cells last)
        lines += _emit(sid, cells, regions_of[sid], vanilla, custom)
    lines.append("</SPBattleScenes>")
    text = "\r\n".join(lines) + "\r\n"
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as e:
        raise ValueError(f"generated XML does not parse ({e})") from e
    return text


def missing_scene_folders(scene_ids, modules: Path) -> list[str]:
    """Scene ids with no Modules/*/SceneObj/<id> folder (Windows lookup is case-insensitive)."""
    present = {p.name.lower() for p in modules.glob("*/SceneObj/*") if p.is_dir()}
    return [sid for sid in scene_ids if sid.lower() not in present]


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the XML")
    mode.add_argument("--dry-run", action="store_true", help="summary only (the default)")
    mode.add_argument("--check", action="store_true", help="exit 1 if the file is stale or a SceneObj is missing")
    args = ap.parse_args(argv)

    vanilla = load_vanilla(ensure_exists(VANILLA_XML, "vanilla SandBox sp_battle_scenes.xml"))
    try:
        text = render(REGIONS, vanilla, CUSTOM)
    except ValueError as e:
        print(f"ERROR: {e}; nothing written", file=sys.stderr)
        return 1

    data = text.encode("utf-8")
    root = ET.fromstring(data)
    scenes = [s.get("id") for s in root.findall("Scene")]
    catch_all = root.find(f"Scene[@id='{CATCH_ALL}']")
    print(f"{len(scenes)} scenes, {len(REGIONS)} regions, catch-all cells: "
          f"{len(catch_all.get('map_indices').split(',')) if catch_all is not None else 0}")
    # On a shared cell each region's odds are its scene count over the union's.
    size = {name: len(set(region_scenes)) for name, _, region_scenes in REGIONS}
    for cell, names in overlaps(REGIONS).items():
        print(f"  shared cell {cell}: {' + '.join(f'{n} ({size[n]})' for n in names)}")

    missing = missing_scene_folders(scenes, MODULES)
    for sid in missing:
        print(f"MISSING SceneObj folder: {sid} (a field battle on its cells would fail to load)")

    old = OUT.read_bytes() if OUT.exists() else b""
    changed = old.replace(b"\r\n", b"\n") != data.replace(b"\r\n", b"\n")
    print(f"{OUT.name}: {'would change' if changed else 'up to date'}")
    if args.check:
        return 1 if changed or missing else 0
    if args.apply:
        OUT.write_bytes(data)
        print(f"wrote {OUT}")
    else:
        print("dry run: pass --apply to write")
    return 0


if __name__ == "__main__":
    sys.exit(main())
