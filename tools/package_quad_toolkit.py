"""Build the shareable quad-toolkit ZIP from an explicit source file list."""

import argparse
import hashlib
from pathlib import Path
import zipfile

PACKAGE_FILES = [
    "tools/__init__.py",
    "tools/quad_engine/__init__.py",
    "tools/quad_engine/cube_map.py",
    "tools/blender/cube_map_remesh.py",
    "tools/blender/quad_remesh_job.py",
    "tools/examples/cube_map_active.py",
    "tools/examples/remesh_active.py",
    "tools/tests/__init__.py",
    "tools/tests/test_cube_map_remesh.py",
    "tools/tests/test_quad_remesh_job.py",
    "tools/quad_toolkit/README.md",
    "tools/quad_toolkit/LICENSE.txt",
    "tools/quad_toolkit/NOTICE.txt",
    "tools/package_quad_toolkit.py",
    "docs/features/cube-map-remesh.md",
    "docs/features/quad-remesher-job.md",
    "docs/research/quad-remesher-engine.md",
]


def build_package(repo, output):
    repo = Path(repo).resolve()
    output = Path(output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for relative in PACKAGE_FILES:
            source = repo / relative
            if not source.is_file():
                raise FileNotFoundError(source)
            archive.write(source, str(Path("quad-toolkit") / relative))
    return hashlib.sha256(output.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", default=".")
    parser.add_argument("--out", required=True, help="Destination ZIP")
    args = parser.parse_args()
    digest = build_package(args.repo, args.out)
    print(f"Wrote {Path(args.out).resolve()}")
    print(f"SHA-256: {digest}")


if __name__ == "__main__":
    main()
