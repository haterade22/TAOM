#!/usr/bin/env python3
"""Fail on a TAOM_Map prefab whose meta_mesh_component names a mesh no loaded .tpac holds.

The Modding Kit editor copies a prefab entity to draw its Prefabs-list preview. A prefab that
names a missing metamesh leaves a null component in that entity, and the editor dies with an
access violation in rglEntity.cpp when the list scrolls to it (2026-11-17: gondor_wall_deco_2 and
gondor_wall_deco_4). The engine logs only "Meta mesh with name X cannot be found" at load.

A mesh counts as present when any .tpac of the chosen modules holds it. A prefab that uses
Armory meshes passes with the Armory in --modules, and still crashes an editor started without it:
load the same modules in the editor.

Exit 0 = every mesh found, 1 = a mesh is missing, 2 = could not check (no folder, unparsed pack).
"""

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

from _gamedir import ensure_exists, game_dir
import validate_mesh_refs as vm

DEFAULT_MODULES = ["Native", "SandBoxCore", "SandBox", "LOTRLOME_Armory", "TAOM_Map"]
PREFABS_DIR = Path("Modules") / "TAOM_Map" / "Prefabs"

_ENTITY_RE = re.compile(r'<game_entity\s[^>]*?name="([^"]*)"')
_MESH_RE = re.compile(r'<meta_mesh_component\s[^>]*?name="([^"]+)"')


@dataclass
class PrefabMesh:
    name: str
    prefab: str
    file: str
    line: int


def extract_prefab_meshes(text: str, rel: str) -> list:
    """Every meta_mesh_component name in one prefab file, with its top-level prefab and line."""
    refs = []
    depth = 0
    top = ""
    for lineno, line in enumerate(text.splitlines(), 1):
        for tok in re.finditer(r"<game_entity\s|</game_entity>|<meta_mesh_component\s", line):
            if tok.group().startswith("</"):
                depth -= 1
            elif tok.group().startswith("<game_entity"):
                depth += 1
                if depth == 1:
                    m = _ENTITY_RE.match(line, tok.start())
                    top = m.group(1) if m else ""
            else:
                m = _MESH_RE.match(line, tok.start())
                if m:
                    refs.append(PrefabMesh(re.sub(r"\.lod\d+$", "", m.group(1), flags=re.I),
                                           top, rel, lineno))
    return refs


def find_missing(refs: list, present: set) -> list:
    """The refs whose base name no present mesh has; one row per (prefab, name)."""
    have = {re.sub(r"\.lod\d+$", "", n, flags=re.I) for n in present}
    seen = set()
    out = []
    for r in refs:
        key = (r.file, r.prefab, r.name)
        if r.name not in have and key not in seen:
            seen.add(key)
            out.append(r)
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--modules", nargs="+", default=DEFAULT_MODULES,
                    help="modules whose .tpac files count as loaded (default: %(default)s)")
    ap.add_argument("--prefabs", help="Prefabs folder to scan (default: the live TAOM_Map's)")
    args = ap.parse_args()

    game = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
    prefabs = Path(args.prefabs) if args.prefabs else game / PREFABS_DIR
    ensure_exists(prefabs, "the TAOM_Map Prefabs folder")

    refs = []
    files = sorted(prefabs.glob("*.xml"))
    for xml in files:
        refs += extract_prefab_meshes(xml.read_text(encoding="utf-8", errors="replace"), xml.name)

    present = vm.build_present_set(vm.tpac_paths_for_modules(game, args.modules))
    print(f"{len(refs)} mesh refs in {len(files)} prefab files; {len(present.metameshes)} "
          f"metameshes in {len(present.tpac_paths)} tpacs of {', '.join(args.modules)}")
    if present.unparsed:
        for path, err in present.unparsed[:5]:
            print(f"UNVERIFIED: could not parse {path}: {err}")
        return 2

    missing = find_missing(refs, present.metameshes)
    for r in missing:
        print(f"MISSING  {r.file}:{r.line}  prefab {r.prefab}  mesh {r.name}")
    if missing:
        print(f"FAIL: {len(missing)} missing mesh ref(s); the editor can crash on these prefabs.")
        return 1
    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
