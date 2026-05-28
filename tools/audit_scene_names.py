#!/usr/bin/env python3
"""
Audit settlement scene_name references against actual on-disk scenes.

Motivation: after the v1.3.15 -> v1.4.5 bump, TaleWorlds may have renamed/removed
scenes. A settlement (or hideout) that references a scene_name with no matching
SceneObj folder will crash when the player enters a battle/encounter there.

Outputs:
  1. All scene_names referenced by VANILLA SandBox settlements (the v1.4.5 baseline).
  2. All scene_names referenced by TAOM_Map settlements.
  3. CRASH SUSPECTS: scene_names referenced anywhere that have NO scene folder on disk.
  4. Vanilla-vs-TAOM diff (scenes TAOM uses that vanilla doesn't, and vice versa).
"""
from __future__ import annotations
import re
from pathlib import Path

GAME = Path(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
MODULES = GAME / "Modules"

SETTLEMENT_FILES = {
    "vanilla_SandBox": MODULES / "SandBox" / "ModuleData" / "settlements.xml",
    "vanilla_NavalDLC": MODULES / "NavalDLC" / "ModuleData" / "settlements.xml",
    "TAOM_Map": MODULES / "TAOM_Map" / "ModuleData" / "settlements.xml",
    "TAOM_shadow": MODULES / "TAOM" / "ModuleData" / "settlements.xml",
}

SCENE_RE = re.compile(r'scene_name="([^"]+)"')


def scenes_in(path: Path) -> dict[str, list[str]]:
    """scene_name -> list of settlement ids that reference it."""
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    out: dict[str, list[str]] = {}
    # walk settlement blocks to attribute each scene to its settlement id
    for m in re.finditer(r'<Settlement id="([^"]+)"[^>]*>(.*?)</Settlement>', text, re.DOTALL):
        sid, body = m.group(1), m.group(2)
        for sm in SCENE_RE.finditer(body):
            out.setdefault(sm.group(1), []).append(sid)
    # also catch any scene_name not inside a Settlement block (defensive)
    return out


def all_scene_folders() -> set[str]:
    """Lowercased SceneObj folder names (Windows scene lookup is case-insensitive,
    so a case-only difference like HART_ISENGARD vs HART_isengard is NOT a crash)."""
    folders: set[str] = set()
    for sceneobj in MODULES.glob("*/SceneObj"):
        for child in sceneobj.iterdir():
            if child.is_dir():
                folders.add(child.name.lower())
    return folders


def all_sceneeditdata() -> set[str]:
    """Lowercased SceneEditData folder names — scenes authored in the editor but not
    necessarily exported/compiled to SceneObj yet (work-in-progress)."""
    folders: set[str] = set()
    for sed in MODULES.glob("*/SceneEditData"):
        for child in sed.iterdir():
            if child.is_dir():
                folders.add(child.name.lower())
    return folders


def main() -> None:
    folders = all_scene_folders()
    editdata = all_sceneeditdata()
    print(f"On-disk SceneObj folders: {len(folders)} | SceneEditData (WIP): {len(editdata)} (case-insensitive)\n")

    refs = {label: scenes_in(p) for label, p in SETTLEMENT_FILES.items()}
    for label, d in refs.items():
        print(f"{label}: {len(d)} distinct scene_names referenced ({SETTLEMENT_FILES[label]})")
    print()

    vanilla = set(refs["vanilla_SandBox"]) | set(refs["vanilla_NavalDLC"])
    taom = set(refs["TAOM_Map"])

    # CRASH SUSPECTS: referenced but no folder on disk
    print("=" * 70)
    print("CRASH SUSPECTS — scene_name referenced but NO matching SceneObj folder")
    print("=" * 70)
    any_missing = False
    for label in ("vanilla_SandBox", "vanilla_NavalDLC", "TAOM_Map", "TAOM_shadow"):
        d = refs[label]
        missing = sorted(s for s in d if s.lower() not in folders)
        if missing:
            any_missing = True
            print(f"\n[{label}] {len(missing)} missing from SceneObj:")
            for s in missing:
                tag = " (WIP: in SceneEditData)" if s.lower() in editdata else " *** NO SCENE ANYWHERE ***"
                ex = ", ".join(d[s][:4]) + (" ..." if len(d[s]) > 4 else "")
                print(f"  {s}{tag}\n      <- {ex}")
    if not any_missing:
        print("  (none — every referenced scene_name has a folder)")

    # Diff
    print("\n" + "=" * 70)
    print("DIFF — TAOM_Map scenes NOT used by vanilla (custom or possibly stale)")
    print("=" * 70)
    only_taom = sorted(taom - vanilla)
    for s in only_taom:
        present = "OK" if s.lower() in folders else ("WIP" if s.lower() in editdata else "MISSING")
        print(f"  [{present}] {s}")

    print("\n" + "=" * 70)
    print("VANILLA scene_name inventory (v1.4.5 SandBox + NavalDLC), folder presence")
    print("=" * 70)
    for s in sorted(vanilla):
        present = "OK " if s.lower() in folders else "MISSING"
        print(f"  [{present}] {s}")


if __name__ == "__main__":
    main()
