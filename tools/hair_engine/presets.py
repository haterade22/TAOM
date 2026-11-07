"""Validated, versioned JSON presets and non-destructive atomic writes."""

import copy
import json
import os
from pathlib import Path
import tempfile

import numpy as np

from .model import Hair, Strand

SCHEMA_VERSION = (1, 0, 0)


def version_tuple(value):
    if (not isinstance(value, (list, tuple)) or len(value) != 3
            or any(isinstance(part, bool) or not isinstance(part, int) or part < 0 for part in value)):
        raise ValueError("schema version must contain three nonnegative integers")
    return tuple(value)


def _validate_json(value):
    try:
        json.dumps(value, allow_nan=False)
    except (TypeError, ValueError) as error:
        raise ValueError(f"preset is not strict JSON: {error}") from error


def validate(data):
    if not isinstance(data, dict):
        raise ValueError("preset must be a JSON object")
    if "schema_version" not in data:
        raise ValueError("schema_version is required")
    version_tuple(data["schema_version"])
    if data.get("kind") not in {"deformer", "hair_system", "hair_systems", "hair"}:
        raise ValueError("unknown preset kind")
    if not isinstance(data.get("name"), str) or not data["name"]:
        raise ValueError("preset name must be nonempty")
    if data["kind"] == "hair":
        hair_from_dict(data)
    elif not isinstance(data.get("settings"), dict):
        raise ValueError("preset settings must be an object")
    _validate_json(data)
    return data


def dumps(data):
    validate(data)
    return json.dumps(data, indent=2, sort_keys=True, allow_nan=False) + "\n"


def loads(text):
    try:
        data = json.loads(text)
    except (json.JSONDecodeError, TypeError) as error:
        raise ValueError(f"invalid preset JSON: {error}") from error
    return validate(data)


def migrate(data):
    backup = copy.deepcopy(data)
    if not isinstance(data, dict) or "schema_version" not in data:
        raise ValueError("schema_version is required for migration")
    version = version_tuple(data["schema_version"])
    if version > SCHEMA_VERSION:
        raise ValueError(f"preset version {version} is newer than {SCHEMA_VERSION}")
    migrated = copy.deepcopy(data)
    if version == (0, 9, 0):
        migrated["settings"] = migrated.pop("deformer")
        migrated["schema_version"] = list(SCHEMA_VERSION)
    if version_tuple(migrated["schema_version"]) != SCHEMA_VERSION:
        raise ValueError(f"no migration from preset version {version}")
    return validate(migrated), backup


def write(path, data, overwrite=False):
    path = Path(path)
    validate(data)
    if path.exists() and not overwrite:
        raise FileExistsError(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    handle = tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent,
                                         prefix=path.name + ".", delete=False)
    temporary = Path(handle.name)
    try:
        with handle:
            handle.write(dumps(data))
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def read(path):
    data = loads(Path(path).read_text(encoding="utf-8"))
    if version_tuple(data["schema_version"]) != SCHEMA_VERSION:
        data, _ = migrate(data)
    return data


def hair_to_dict(hair):
    strands = []
    for strand in hair.strands:
        strands.append({
            "positions": strand.positions.tolist(),
            "radius": strand.radius.tolist(),
            "tilt": strand.tilt.tolist(),
            "profile_id": strand.profile_id,
            "uv_region": strand.uv_region,
            "group_id": strand.group_id,
            "tags": sorted(strand.tags),
            "point_attributes": {name: np.asarray(values).tolist()
                                 for name, values in strand.point_attributes.items()},
        })
    return {
        "schema_version": list(SCHEMA_VERSION),
        "kind": "hair",
        "name": hair.attributes.get("name", "hair"),
        "attributes": hair.attributes,
        "strands": strands,
    }


def hair_from_dict(data):
    if data.get("kind") != "hair" or not isinstance(data.get("strands"), list):
        raise ValueError("hair preset requires a strands array")
    strands = []
    for item in data["strands"]:
        try:
            strand = Strand(
                np.asarray(item["positions"], dtype=np.float64),
                item.get("radius"),
                item.get("tilt"),
                item.get("profile_id", 0),
                item.get("uv_region", -1),
                item.get("group_id", 0),
                frozenset(item.get("tags", [])),
                item.get("point_attributes", {}),
            )
        except (KeyError, TypeError, ValueError) as error:
            raise ValueError(f"invalid strand in hair preset: {error}") from error
        strands.append(strand)
    return Hair(strands, dict(data.get("attributes", {})))
