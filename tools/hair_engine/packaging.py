"""Verified archive extraction and non-destructive local installation."""

import hashlib
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import tempfile
import zipfile


def _member_name(info):
    name = info.filename
    path = PurePosixPath(name)
    if ("\\" in name or "\x00" in name or path.is_absolute() or ".." in path.parts
            or (path.parts and ":" in path.parts[0])):
        raise ValueError(f"unsafe archive member {name!r}")
    if stat.S_ISLNK(info.external_attr >> 16):
        raise ValueError(f"unsafe archive symlink {name!r}")
    return path


def install_archive(archive, destination, expected_sha256=None):
    archive = Path(archive).resolve()
    destination = Path(destination).resolve()
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    if expected_sha256 is not None and digest.lower() != expected_sha256.lower():
        raise ValueError(f"archive SHA-256 mismatch: expected {expected_sha256}, found {digest}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=".hair-install-", dir=destination.parent))
    backup_root = None
    moved_previous = False
    try:
        with zipfile.ZipFile(archive) as package:
            members = []
            for info in package.infolist():
                path = _member_name(info)
                if info.is_dir():
                    continue
                target = staging / path
                target.parent.mkdir(parents=True, exist_ok=True)
                with package.open(info) as source, target.open("wb") as output:
                    shutil.copyfileobj(source, output)
                members.append(path.as_posix())
        if destination.exists():
            backup_root = Path(tempfile.mkdtemp(prefix=destination.name + ".backup-",
                                                dir=destination.parent))
            backup = backup_root / "previous"
            os.replace(destination, backup)
            moved_previous = True
        os.replace(staging, destination)
        return {"files": sorted(members), "sha256": digest,
                "backup": str(backup_root / "previous") if moved_previous else None}
    except BaseException:
        if destination.exists() and not moved_previous:
            pass
        if moved_previous:
            if destination.exists():
                shutil.rmtree(destination)
            os.replace(backup_root / "previous", destination)
        raise
    finally:
        if staging.exists():
            shutil.rmtree(staging)
