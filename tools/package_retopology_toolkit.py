"""Build the KEYforce source-only retopology toolkit from an explicit manifest."""
import argparse
import hashlib
from io import BytesIO
import json
from pathlib import Path
import zipfile


PACKAGE_FILES = [
    "tools/__init__.py",
    "tools/retopo_engine/__init__.py",
    "tools/retopo_engine/model.py",
    "tools/retopo_engine/geometry.py",
    "tools/retopo_engine/engine.py",
    "tools/retopo_engine/staging.py",
    "tools/retopo_engine/differential.py",
    "tools/blender/retopo_adapter.py",
    "tools/blender/retopo_operators.py",
    "tools/blender/retopo_selftest.py",
    "tools/blender/retopo_installable_selftest.py",
    "tools/blender/retopo_reference_differential.py",
    "tools/blender/petik_selftest.py",
    "tools/examples/retopo_active.py",
    "tools/petik/Petik/__init__.py",
    "tools/petik/Petik/op_md_petik.py",
    "tools/petik/Petik/petik_preferences.py",
    "tools/petik/Petik/blender_manifest.toml",
    "tools/petik/Petik/LICENSE.txt",
    "tools/petik/Petik/NOTICE.txt",
    "tools/retopo_addon/__init__.py",
    "tools/retopo_addon/blender_manifest.toml",
    "tools/retopo_toolkit/README.md",
    "tools/retopo_toolkit/LICENSE.txt",
    "tools/retopo_toolkit/NOTICE.txt",
    "tools/tests/__init__.py",
    "tools/tests/test_retopo_engine.py",
    "tools/tests/test_retopo_differential.py",
    "tools/tests/test_retopo_contract_regressions.py",
    "tools/tests/test_petik_compat.py",
    "tools/tests/test_package_retopology_toolkit.py",
    "tools/tests/fixtures/retopo_reference.json",
    "tools/package_retopology_toolkit.py",
    "docs/features/retopology-toolkit.md",
    "docs/research/retopo-planes-1-6-3-clean-room.md",
    "docs/research/retopo-planes-complete-capability-matrix.md",
]

TAOM_ADDON_FILES = {
    "tools/retopo_addon/__init__.py": "taom_retopology/__init__.py",
    "tools/retopo_addon/blender_manifest.toml": "taom_retopology/blender_manifest.toml",
    "tools/blender/retopo_adapter.py": "taom_retopology/retopo_adapter.py",
    "tools/blender/retopo_operators.py": "taom_retopology/retopo_operators.py",
    "tools/retopo_engine/__init__.py": "taom_retopology/retopo_engine/__init__.py",
    "tools/retopo_engine/model.py": "taom_retopology/retopo_engine/model.py",
    "tools/retopo_engine/geometry.py": "taom_retopology/retopo_engine/geometry.py",
    "tools/retopo_engine/engine.py": "taom_retopology/retopo_engine/engine.py",
    "tools/retopo_toolkit/LICENSE.txt": "taom_retopology/LICENSE.txt",
    "tools/retopo_toolkit/NOTICE.txt": "taom_retopology/NOTICE.txt",
}


def _write(archive, name, data):
    info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
    info.compress_type = zipfile.ZIP_DEFLATED
    info.external_attr = 0o100644 << 16
    archive.writestr(info, data)


def _source_bytes(repo, relative):
    return (repo / relative).read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def _taom_installable(repo):
    buffer = BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        for source, destination in sorted(TAOM_ADDON_FILES.items()):
            _write(archive, destination, _source_bytes(repo, source))
    return buffer.getvalue()


def _petik_installable(repo):
    buffer = BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        for source in sorted(PACKAGE_FILES):
            if not source.startswith("tools/petik/Petik/"):
                continue
            destination = "Petik/" + source.removeprefix("tools/petik/Petik/")
            _write(archive, destination, _source_bytes(repo, source))
    return buffer.getvalue()


def build_package(repo, output):
    repo = Path(repo).resolve()
    output = Path(output).resolve()
    missing = [relative for relative in PACKAGE_FILES if not (repo / relative).is_file()]
    if missing:
        raise FileNotFoundError("missing retopology toolkit sources: " + ", ".join(missing))
    taom = _taom_installable(repo)
    petik = _petik_installable(repo)
    file_hashes = {
        relative: hashlib.sha256(_source_bytes(repo, relative)).hexdigest()
        for relative in sorted(PACKAGE_FILES)
    }
    manifest = {
        "format": 1,
        "files": file_hashes,
        "installable": {
            "taom_retopology.zip": hashlib.sha256(taom).hexdigest(),
            "Petik.zip": hashlib.sha256(petik).hexdigest(),
        },
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w") as archive:
        for name in ("README.md", "LICENSE.txt", "NOTICE.txt"):
            _write(archive, "retopo-toolkit/" + name, _source_bytes(repo, "tools/retopo_toolkit/" + name))
        for relative in sorted(PACKAGE_FILES):
            name = (Path("retopo-toolkit") / relative).as_posix()
            _write(archive, name, _source_bytes(repo, relative))
        _write(archive, "retopo-toolkit/installable/taom_retopology.zip", taom)
        _write(archive, "retopo-toolkit/installable/Petik.zip", petik)
        _write(
            archive,
            "retopo-toolkit/manifest.json",
            json.dumps(manifest, indent=2, sort_keys=True).encode("utf-8") + b"\n",
        )
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
