"""Build the source-only Hair Toolkit ZIP from an explicit source manifest."""
import argparse
import hashlib
from pathlib import Path
import zipfile

PACKAGE_FILES = [
    "tools/__init__.py",
    "tools/blender/hair_tool_pipeline.py",
    "tools/blender/add_mesh_lods.py",
    "tools/blender/transfer_upper_mesh_weights.py",
    "tools/blender/groom_kitbash.py",
    "tools/blender/kitbash_grooms.py",
    "tools/blender/specs/dwarf_grooms_kitbash.json",
    "tools/groom_contact_sheet.py",
    "tools/oneoff/append_dwarf_grooms.py",
    "tools/tests/__init__.py",
    "tools/tests/test_hair_tool_pipeline.py",
    "tools/tests/test_package_hair_toolkit.py",
    "tools/tests/test_groom_kitbash.py",
    "tools/tests/test_append_dwarf_grooms.py",
    "tools/tests/test_add_mesh_lods.py",
    "tools/hair_toolkit/README.md",
    "tools/hair_toolkit/LICENSE.txt",
    "tools/hair_toolkit/NOTICE.txt",
    "tools/package_hair_toolkit.py",
    ".claude/skills/groom-hair/SKILL.md",
    "docs/features/hair-tool-workflow.md",
    "docs/research/hair-tool-4-6-1.md",
    "docs/reference/race-face-and-hand-morphs.md",
    "docs/reference/helmet-hair-beard-cover.md",
]


def build_package(repo, output):
    repo = Path(repo).resolve()
    output = Path(output).resolve()
    sources = [(relative, repo / relative) for relative in PACKAGE_FILES]
    missing = [str(path) for _, path in sources if not path.is_file()]
    if missing:
        raise FileNotFoundError("missing Hair Toolkit sources: " + ", ".join(missing))
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for relative, source in sources:
            archive.write(source, (Path("hair-toolkit") / relative).as_posix())
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
