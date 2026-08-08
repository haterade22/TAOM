#!/usr/bin/env python3
"""
Compare TAOM's sp_battle_scenes.xml against vanilla (SandBox + NavalDLC) and against
on-disk SceneObj folders. sp_battle_scenes.xml maps campaign-map terrain cells
(map_indices) to a battle-terrain Scene id; if TAOM lists a Scene id that no longer
exists on disk (v1.4.5 removed/renamed it), a FIELD BATTLE on a cell with that index
crashes. Also checks map_indices coverage 0-255 (an uncovered index = no battle scene).
"""
from __future__ import annotations
import re
from pathlib import Path
from _gamedir import game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = Path(game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"))
MODULES = GAME / "Modules"
FILES = {
    "TAOM": MODULES / "TAOM" / "ModuleData" / "sp_battle_scenes.xml",
    "vanilla_SandBox": MODULES / "SandBox" / "ModuleData" / "sp_battle_scenes.xml",
    "vanilla_NavalDLC": MODULES / "NavalDLC" / "ModuleData" / "sp_battle_scenes.xml",
}


def scene_ids(path: Path) -> set[str]:
    if not path.exists():
        return set()
    t = path.read_text(encoding="utf-8-sig", errors="replace")
    return set(re.findall(r'<Scene\s+id="([^"]+)"', t))


def map_indices(path: Path) -> set[int]:
    if not path.exists():
        return set()
    t = path.read_text(encoding="utf-8-sig", errors="replace")
    idx: set[int] = set()
    for m in re.findall(r'map_indices="([^"]*)"', t):
        for tok in m.split(","):
            tok = tok.strip()
            if tok.isdigit():
                idx.add(int(tok))
    return idx


def scene_folders_lower() -> set[str]:
    out = set()
    for so in MODULES.glob("*/SceneObj"):
        for c in so.iterdir():
            if c.is_dir():
                out.add(c.name.lower())
    return out


def main() -> None:
    folders = scene_folders_lower()
    ids = {k: scene_ids(p) for k, p in FILES.items()}
    for k, s in ids.items():
        print(f"{k}: {len(s)} Scene ids  ({FILES[k]})")
    print()

    taom = ids["TAOM"]
    vanilla = ids["vanilla_SandBox"] | ids["vanilla_NavalDLC"]

    print("=" * 70)
    print("CRASH SUSPECTS — TAOM battle Scene id with NO SceneObj folder on disk")
    print("=" * 70)
    missing = sorted(s for s in taom if s.lower() not in folders)
    if missing:
        for s in missing:
            invan = "in-vanilla" if s in vanilla else "NOT-in-vanilla"
            print(f"  {s}   [{invan}]")
    else:
        print("  (none — every TAOM battle scene id exists on disk)")

    print("\n" + "=" * 70)
    print("DIFF — TAOM Scene ids NOT present in vanilla's current set")
    print("=" * 70)
    only_taom = sorted(taom - vanilla)
    for s in only_taom:
        present = "OK" if s.lower() in folders else "MISSING-ON-DISK"
        print(f"  [{present}] {s}")

    print("\n" + "=" * 70)
    print("DIFF — vanilla Scene ids TAOM does NOT list (dropped coverage)")
    print("=" * 70)
    for s in sorted(vanilla - taom):
        print(f"  {s}")

    # map_indices coverage
    ti = map_indices(FILES["TAOM"])
    print("\n" + "=" * 70)
    print("map_indices coverage (TAOM)")
    print("=" * 70)
    full = set(range(256))
    uncovered = sorted(full - ti)
    print(f"  TAOM covers {len(ti)} distinct indices; range {min(ti) if ti else '-'}..{max(ti) if ti else '-'}")
    if uncovered:
        print(f"  UNCOVERED indices (0-255): {len(uncovered)} -> {uncovered[:30]}{' ...' if len(uncovered)>30 else ''}")
    else:
        print("  all 0-255 covered")


if __name__ == "__main__":
    main()
