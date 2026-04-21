"""
List ALL mesh object names from an FBX (including bo_ collision meshes).
Based on list_bannerlord_fbx_meshes.py but without the bo_ filter.

Usage (via Blender):
    blender --background --factory-startup --python tools/list_fbx_objects_all.py -- <fbx> --output <txt>

Output: one mesh name per line, LOD suffixes (.lod0/.lod5/etc.) collapsed,
duplicates removed, sorted case-insensitively.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

import bpy


def parse_args(argv: list[str]) -> tuple[Path, Path | None]:
    fbx_path: Path | None = None
    output_path: Path | None = None
    i = 0
    while i < len(argv):
        arg = argv[i]
        if arg == "--output":
            i += 1
            output_path = Path(argv[i]).expanduser().resolve()
            i += 1
            continue
        if fbx_path is None:
            fbx_path = Path(arg).expanduser().resolve()
            i += 1
            continue
        raise SystemExit(f"Unexpected argument: {arg}")
    if fbx_path is None:
        raise SystemExit("Missing FBX path.")
    return fbx_path, output_path


def collect_mesh_names() -> list[str]:
    seen: set[str] = set()
    names: list[str] = []
    for obj in sorted(bpy.context.scene.objects, key=lambda o: o.name.lower()):
        if obj.type != "MESH":
            continue
        name = re.sub(r"\.lod\d+$", "", obj.name, flags=re.IGNORECASE)
        if name in seen:
            continue
        seen.add(name)
        names.append(name)
    return names


def main() -> int:
    argv = sys.argv
    separator_index = argv.index("--")
    fbx_path, output_path = parse_args(argv[separator_index + 1 :])

    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(fbx_path), use_image_search=False)
    if "FINISHED" not in result:
        raise RuntimeError(f"Failed to import FBX: {fbx_path}")

    output = "\n".join(collect_mesh_names())
    if output_path is not None:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(output, encoding="utf-8")
        print(f"Wrote output to: {output_path}")
    else:
        print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
