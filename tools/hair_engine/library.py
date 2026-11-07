"""Local hair-preset library with safe names and duplicate remapping."""

from pathlib import Path
import re

from . import presets

_SUFFIX = ".hair.json"
_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]*$")


def _name(value):
    if not isinstance(value, str) or not _NAME.fullmatch(value) or ".." in value:
        raise ValueError("preset name must be a safe filename without path traversal")
    return value


def duplicate_remap(names):
    used = set()
    output = []
    for name in names:
        candidate = name
        index = 0
        while candidate in used:
            index += 1
            candidate = f"{name}.{index:03d}"
        used.add(candidate)
        output.append(candidate)
    return output


class PresetLibrary:
    def __init__(self, root):
        self.root = Path(root).resolve()
        self.root.mkdir(parents=True, exist_ok=True)

    def _path(self, name):
        return self.root / (_name(name) + _SUFFIX)

    def save(self, name, hair, overwrite=False):
        name = _name(name)
        if not overwrite:
            names = {path.name[:-len(_SUFFIX)] for path in self.root.glob("*" + _SUFFIX)}
            if name in names:
                used = set()
                candidate = name
                index = 0
                while candidate in used or candidate in names:
                    index += 1
                    candidate = f"{name}.{index:03d}"
                name = candidate
        data = presets.hair_to_dict(hair)
        data["name"] = name
        presets.write(self._path(name), data, overwrite=overwrite)
        return name

    def load(self, name):
        return presets.hair_from_dict(presets.read(self._path(name)))

    def list(self):
        return sorted(path.name[:-len(_SUFFIX)] for path in self.root.glob("*" + _SUFFIX))
