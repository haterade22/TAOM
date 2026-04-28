"""Extract mesh names from an FBX file via headless Blender.

Wraps the existing tools/list_fbx_objects_all.py script (Erebor-tested). That script
imports the FBX in a headless Blender process and emits one mesh name per line, with
SM_ visuals and bo_ collisions interleaved. We forward stdout to a `.txt` file beside
the FBX (or the user's --cache-dir) and return the list.

Caching: re-running with the same FBX skips Blender if the .txt already exists and is
newer than the FBX. Pass --refresh-cache to bypass.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from dataclasses import dataclass


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LIST_SCRIPT = os.path.join(SCRIPT_DIR, "..", "list_fbx_objects_all.py")


@dataclass
class ExtractResult:
    fbx_path: str
    cache_path: str
    mesh_names: list[str]


def extract_mesh_names(fbx_path: str, *, cache_dir: str | None = None, refresh: bool = False) -> ExtractResult:
    """Run Blender headless against an FBX and return the mesh names it contains."""
    cache_path = _cache_path_for(fbx_path, cache_dir)
    if not refresh and _cache_is_fresh(fbx_path, cache_path):
        with open(cache_path, encoding="utf-8") as f:
            names = [line.strip() for line in f if line.strip()]
        return ExtractResult(fbx_path=fbx_path, cache_path=cache_path, mesh_names=names)

    blender = _find_blender()
    os.makedirs(os.path.dirname(cache_path) or ".", exist_ok=True)
    cmd = [
        blender, "--background", "--python", LIST_SCRIPT, "--",
        fbx_path, "--output", cache_path,
    ]
    print(f"[extract] running: {' '.join(cmd)}", file=sys.stderr)
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        sys.stderr.write(proc.stdout)
        sys.stderr.write(proc.stderr)
        raise RuntimeError(f"Blender failed extracting {fbx_path} (exit {proc.returncode})")

    with open(cache_path, encoding="utf-8") as f:
        names = [line.strip() for line in f if line.strip()]
    return ExtractResult(fbx_path=fbx_path, cache_path=cache_path, mesh_names=names)


def _cache_path_for(fbx_path: str, cache_dir: str | None) -> str:
    base = os.path.splitext(os.path.basename(fbx_path))[0] + ".meshes.txt"
    if cache_dir:
        return os.path.join(cache_dir, base)
    return os.path.join(os.path.dirname(fbx_path), base)


def _cache_is_fresh(fbx_path: str, cache_path: str) -> bool:
    if not os.path.isfile(cache_path):
        return False
    return os.path.getmtime(cache_path) >= os.path.getmtime(fbx_path)


def _find_blender() -> str:
    blender = os.environ.get("BLENDER_EXE") or shutil.which("blender")
    if not blender:
        raise EnvironmentError(
            "Blender not on PATH and BLENDER_EXE not set. "
            "Install Blender and either add it to PATH or export BLENDER_EXE=<full path>."
        )
    return blender
