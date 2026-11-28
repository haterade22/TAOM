#!/usr/bin/env python3
"""Does any terrain node of the scenes use only flora layers?

A terrain node whose `layer_is_used_mask_<season>` holds flora layers and nothing else makes the
engine compile a terrain shader variant with no layers. In `pbr_terrain_gbuffer.rs` the world normal
then folds to the constant (0,0,0), `normalize()` at `terrain_pixel_functions.rsh:818` becomes 0/0, and
the game shows "Error compiling shader file pbr_terrain_gbuffer.rs X4008" on every load of the scene.
The compile never succeeds, so nothing is cached. Found 2026-11-28 in Helm's Deep (node 0,3); see
docs/reference/terrain-layer-gate.md. That the engine drops flora layers from the compiled layer list is
read from the shader source and the data, not from the native code.

The masks are bit sets over the `<layers>` list (bit i = layer i, in file order) and are kept per
season. A layer is flora per season: `is_flora_layer` on the `summer`, `fall`, `winter` or `spring`
child of the `<layer>`. A season is judged with that season's flags. A mask of 0 (no layer at all) is
also a fault, since the variant has no layers either; no scene in the 2026-11-28 install scan has one.

TAOM_Map is live and unversioned, so a fix made in the Modding Kit reverts on reinstall. This gate
reads the install (BANNERLORD_GAME_DIR) and the files are large, so they are parsed as a stream (each element is freed once read, so the peak stays near 1 MB).

Usage:
    python tools/check_terrain_layers.py                       # TAOM and TAOM_Map under the install
    python tools/check_terrain_layers.py --modules TAOM_Map Armory
    python tools/check_terrain_layers.py <scene.xscene> [...]  # only these files

Exit code: 0 clean, 1 a node is a fault (printed: scene, node idx/idy, season, world square), 2 NOT RUN
(no install, no scene found, or a file is not a scene.xscene, does not parse or holds no mask; each prints an
`ERROR:` line, the other files are still judged, and a fault in them wins with exit 1), 3 the tool crashed. Never read 2 or 3 as a pass.
"""
from __future__ import annotations

import argparse
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import DEFAULT_GAME_ROOT, game_dir  # noqa: E402

SEASONS = ("summer", "fall", "winter", "spring")
DEFAULT_MODULES = ("TAOM", "TAOM_Map")
SCENE_FILE = "scene.xscene"

# Known faults the gate tolerates so the suite stays green while a fix is owed. Each entry is
# (scene folder, idx, idy, season, mask, reason): the fault must match all five keys, so a changed mask or
# an extra node is a NEW fault. Every entry needs a stated reason. An entry that matches nothing in a scene
# that was read is printed as STALE.
DWARVES_FALL_NODES = ((0, 3), (0, 9), (2, 0), (2, 13), (3, 13), (4, 12), (5, 0), (9, 0), (12, 11), (12, 12),
                      (13, 2), (13, 5), (13, 8), (13, 11), (13, 12))
KNOWN_FAULTS = (
    # Helm's Deep, node (0,3): mask 1 = layer 0 "default", a flora layer in summer. Fixed in the
    # Modding Kit by haterade (paint a little non-flora layer on that node); remove this entry then.
    ("taom_rohan_castle_helms_deep_forceatmo", 0, 3, "summer", 1, "flora-only node, Kit fix owed"),
) + tuple(
    # Dwarves battle 001: its layer 0 "rock_cliff" has is_flora_layer="true" in the FALL entry only, so these
    # 15 nodes with mask 1 read as flora-only in fall. Found 2026-11-28 by this gate; whether the engine
    # compiles the fall variant when the scene loads, and so shows the popup, is UNVERIFIED (see the doc,
    # "Open question"). Remove these when the Kit owner has cleared the fall flag or painted a second layer.
    ("taom_dwarves_battle_001_forceatmo", idx, idy, "fall", 1, "fall-only flora flag on layer 0, unverified")
    for idx, idy in DWARVES_FALL_NODES
)


def known_index(scene: str, idx: int, idy: int, season: str, mask: int):
    """Index of the KNOWN_FAULTS entry that covers this fault, or None."""
    for i, (k_scene, k_idx, k_idy, k_season, k_mask, _reason) in enumerate(KNOWN_FAULTS):
        if (k_scene, k_idx, k_idy, k_season, k_mask) == (scene, idx, idy, season, mask):
            return i
    return None


def parse_scene(path: Path, scene: str | None = None):
    """Return (faults, nodes, masks) for one scene file.

    A fault is (scene, idx, idy, season, mask, reason, square); `square` is (x0, y0, x1, y1) in metres, or
    None when the terrain has no node_size. `nodes` counts terrain nodes, `masks` the season masks judged.
    """
    scene = scene or path.parent.name
    stack: list[str] = []
    layers: list[dict[str, bool]] = []  # per layer index: season -> is_flora_layer
    node_size = None
    faults = []
    nodes = masks = 0
    for event, el in ET.iterparse(str(path), events=("start", "end")):
        if event == "start":
            stack.append(el.tag)
            if el.tag == "terrain":
                try:
                    node_size = float(el.get("node_size"))
                except (TypeError, ValueError):
                    node_size = None
            continue
        stack.pop()
        parent = stack[-1] if stack else ""
        if el.tag == "layer" and parent == "layers":
            layers.append({s: (el.find(s) is not None and el.find(s).get("is_flora_layer") == "true")
                           for s in SEASONS})
        elif el.tag == "node" and parent == "nodes":
            nodes += 1
            idx, idy = el.get("idx"), el.get("idy")
            for season in SEASONS:
                raw = el.get(f"layer_is_used_mask_{season}")
                if raw is None:
                    continue
                masks += 1
                mask = int(raw)
                used = [i for i in range(max(len(layers), mask.bit_length())) if mask >> i & 1]
                if not used:
                    why = "no layer is used"
                elif all(i < len(layers) and layers[i][season] for i in used):
                    why = "only flora layers are used (" + ", ".join(str(i) for i in used) + ")"
                else:
                    continue
                square = None
                if node_size:
                    x0, y0 = int(idx) * node_size, int(idy) * node_size
                    square = (x0, y0, x0 + node_size, y0 + node_size)
                faults.append((scene, int(idx), int(idy), season, mask, why, square))
        if "layer" not in stack:  # a layer's children are read when the layer ends; free everything else
            el.clear()
    return faults, nodes, masks


def scene_files(game: Path, modules) -> list[Path]:
    return sorted(p for m in modules for p in (game / "Modules" / m / "SceneObj").glob(f"*/{SCENE_FILE}"))


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("files", nargs="*", help="scene.xscene files to judge (default: the modules' scenes)")
    ap.add_argument("--modules", nargs="+", default=list(DEFAULT_MODULES))
    ap.add_argument("--game-dir", default=None, help="install root (default: BANNERLORD_GAME_DIR)")
    ap.add_argument("--no-known", action="store_true", help="also report the KNOWN_FAULTS entries as faults")
    args = ap.parse_args(argv)

    errors: list[str] = []
    if args.files:
        files = [Path(f) for f in args.files]
    else:
        game = Path(args.game_dir or game_dir(DEFAULT_GAME_ROOT))
        if not (game / "Modules").is_dir():
            print(f"ERROR: NOT RUN: Bannerlord install not found at {game}; set BANNERLORD_GAME_DIR",
                  file=sys.stderr)
            return 2
        files = scene_files(game, args.modules)
        if not files:
            print(f"ERROR: NOT RUN: no {SCENE_FILE} under {', '.join(args.modules)} in {game}", file=sys.stderr)
            return 2

    new, known = [], []
    read, matched = set(), set()
    n_files = n_nodes = n_masks = 0
    for f in files:
        if f.name.lower() != SCENE_FILE:
            errors.append(f"ERROR: NOT RUN: not a {SCENE_FILE}: {f}")
            continue
        if not f.is_file():
            errors.append(f"ERROR: NOT RUN: not a file: {f}")
            continue
        try:
            found, nodes, masks = parse_scene(f)
        except (ET.ParseError, OSError, ValueError) as exc:
            errors.append(f"ERROR: {f}: {exc}")
            continue
        if nodes and not masks:
            errors.append(f"ERROR: {f}: {nodes} terrain node(s) but no layer_is_used_mask_<season> read")
            continue
        n_files, n_nodes, n_masks = n_files + 1, n_nodes + nodes, n_masks + masks
        read.add(f.parent.name)
        for fault in found:
            i = None if args.no_known else known_index(*fault[:5])
            if i is None:
                new.append(fault)
            else:
                matched.add(i)
                known.append(fault)

    def show(tag, fault):
        scene, idx, idy, season, mask, why, sq = fault
        where = f", world square x {sq[0]:g}-{sq[2]:g} y {sq[1]:g}-{sq[3]:g}" if sq else ""
        print(f"{tag} {scene} node ({idx},{idy}) {season} mask {mask}: {why}{where}")

    for fault in known:
        show("KNOWN", fault)
    for fault in new:
        show("FAIL", fault)
    if not args.no_known:
        for i, (k_scene, k_idx, k_idy, k_season, k_mask, _reason) in enumerate(KNOWN_FAULTS):
            if k_scene in read and i not in matched:
                print(f"STALE known fault {k_scene} node ({k_idx},{k_idy}) {k_season} mask {k_mask}: "
                      "matches nothing in the scene: remove it")
    for line in errors:
        print(line, file=sys.stderr)
    print(f"{n_files} scene(s) read, {n_nodes} node(s), {n_masks} mask(s) judged, "
          f"{len(new)} fault(s), {len(known)} known and tolerated, {len(errors)} unreadable")
    if new:
        return 1
    return 2 if errors else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except Exception:
        traceback.print_exc()
        sys.exit(3)
