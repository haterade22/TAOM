"""
Build taom_erebor_kitbash.xml from per-FBX mesh-name dumps emitted by
`list_fbx_objects_all.py` (run under Blender). Each input .txt contains ALL
mesh names from one FBX — both SM_ (visual) and bo_ (collision).

For every SM_ mesh, the script picks a bo_ shape from the SAME FBX:
  1. exact match — `bo_<SM_name>` if it's present in the FBX
  2. longest common prefix — the bo_ that shares the most characters at
     the start (falling back up family hierarchies like _A2 -> _A1,
     _Cor_Out_A2 -> _Cor_Out_A1, _Ramp_Mrln_01 -> _Ramp_Mrln_0, etc.)

Usage:
    python tools/build_erebor_kitbash.py <mesh_list_dir> <output_xml>

Emits the same flat <game_entity> format as taom_mirkwood_kitbash.xml:
tab-indented, zero transform, <physics shape="<actual_bo>"/>, and a
meta_mesh_component of the SM_ name.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

ENTRY_WITH_PHYSICS = (
    '\t<game_entity name="{n}" old_prefab_name="">\n'
    '\t\t<transform position="0.000, 0.000, 0.000" rotation_euler="0.000, 0.000, 0.000"/>\n'
    '\t\t<physics shape="{bo}"/>\n'
    '\t\t<components>\n'
    '\t\t\t<meta_mesh_component name="{n}"/>\n'
    '\t\t</components>\n'
    '\t</game_entity>\n'
)

ENTRY_NO_PHYSICS = (
    '\t<game_entity name="{n}" old_prefab_name="">\n'
    '\t\t<transform position="0.000, 0.000, 0.000" rotation_euler="0.000, 0.000, 0.000"/>\n'
    '\t\t<components>\n'
    '\t\t\t<meta_mesh_component name="{n}"/>\n'
    '\t\t</components>\n'
    '\t</game_entity>\n'
)


def common_prefix_len(a: str, b: str) -> int:
    n = min(len(a), len(b))
    for i in range(n):
        if a[i] != b[i]:
            return i
    return n


def common_suffix_len(a: str, b: str) -> int:
    n = min(len(a), len(b))
    for i in range(1, n + 1):
        if a[-i] != b[-i]:
            return i - 1
    return n


def pick_bo(sm_name: str, bo_names: list[str]) -> str | None:
    exact = f"bo_{sm_name}"
    if exact in bo_names:
        return exact
    if not bo_names:
        return None
    target = exact
    scored = [
        (common_prefix_len(target, b), common_suffix_len(target, b), -len(b), b)
        for b in bo_names
    ]
    scored.sort(reverse=True)
    return scored[0][3]


def parse_fbx_dump(path: Path) -> tuple[list[str], list[str]]:
    # Bannerlord lowercases mesh IDs on FBX import, so the runtime mesh
    # database only knows lowercase names. Normalize here so pick_bo() and
    # the final XML reference what the engine can actually resolve.
    sm: list[str] = []
    bo: list[str] = []
    seen_sm: set[str] = set()
    seen_bo: set[str] = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        name = line.strip().lower()
        if not name:
            continue
        if name.startswith("bo_"):
            if name in seen_bo:
                continue
            seen_bo.add(name)
            bo.append(name)
        else:
            if name in seen_sm:
                continue
            seen_sm.add(name)
            sm.append(name)
    return sm, bo


def build(mesh_list_dir: Path) -> tuple[str, list[tuple[str, str, bool]]]:
    # entries is a list of (sm_name, bo_name, exact_match) preserving per-FBX, then sorted order
    entries: list[tuple[str, str, bool]] = []
    seen_sm: set[str] = set()

    for txt in sorted(mesh_list_dir.glob("*.txt")):
        sm_list, bo_list = parse_fbx_dump(txt)
        for sm_name in sm_list:
            if sm_name in seen_sm:
                continue
            seen_sm.add(sm_name)
            bo_name = pick_bo(sm_name, bo_list)
            if bo_name is None:
                bo_name = f"bo_{sm_name}"
                exact = False
            else:
                exact = bo_name == f"bo_{sm_name}"
            entries.append((sm_name, bo_name, exact))

    parts = ["<prefabs>\n"]
    for sm_name, bo_name, is_exact in entries:
        # Orphan SM_ meshes share a bo_ from a sibling (e.g., _a2 -> _a1). The
        # visual/collision geometry mismatch appears to crash the editor's
        # preview-scene physics init, so skip <physics> for remapped entries.
        if is_exact:
            parts.append(ENTRY_WITH_PHYSICS.format(n=sm_name, bo=bo_name))
        else:
            parts.append(ENTRY_NO_PHYSICS.format(n=sm_name))
    parts.append("</prefabs>\n")
    return "".join(parts), entries


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("Usage: build_erebor_kitbash.py <mesh_list_dir> <output_xml>", file=sys.stderr)
        return 2

    mesh_list_dir = Path(argv[1]).expanduser().resolve()
    output_path = Path(argv[2]).expanduser().resolve()

    if not mesh_list_dir.is_dir():
        print(f"mesh_list_dir not found: {mesh_list_dir}", file=sys.stderr)
        return 1

    xml, entries = build(mesh_list_dir)
    if not entries:
        print(f"no mesh names found under {mesh_list_dir}", file=sys.stderr)
        return 1

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(xml, encoding="utf-8")

    total = len(entries)
    exact = sum(1 for _, _, e in entries if e)
    print(f"wrote {total} entries to {output_path}  (exact-bo={exact}, remapped={total - exact})")
    for sm_name, bo_name, is_exact in entries:
        if not is_exact:
            print(f"  remapped: {sm_name} -> {bo_name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
