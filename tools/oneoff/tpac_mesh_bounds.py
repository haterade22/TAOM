#!/usr/bin/env python3
"""Print the stored bounding boxes of items in a Bannerlord tpac. Read only.

Two layouts were read from vanilla packages on 2026-11-24 (props_siege_gates_outer.tpac and
meshes_shared_9.tpac, Native, v1.5.7):

  Body (type 0e8e52e8): the item metadata ends in min vec4, max vec4, centre vec4 and a float
  radius (52 bytes); every vec4 has w = 1.0.
  Metamesh (type 978b8fa0): int32 LOD count at metadata offset 52; LOD records start at offset 85,
  each 16-byte material guid, int32 name length, name, then a 260-byte tail whose min vec4 sits
  at +112 and max vec4 at +128. Positions are in the mesh frame.

Both rest on a few items only; the parsers refuse a record whose w is not 1.0.

Usage: python tools/oneoff/tpac_mesh_bounds.py <tpac> <item name> [<item name> ...]
"""
from __future__ import annotations

import struct
import sys
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

BODY_TAIL = 52
LOD_COUNT_AT = 52
LOD_FIRST = 85
LOD_TAIL = 260
BOUNDS_MIN_AT = 112
BOUNDS_MAX_AT = 128
BODY_TYPE = "0e8e52e8"
METAMESH_TYPE = "978b8fa0"


@dataclass
class Bounds:
    min: tuple
    max: tuple
    radius: float | None = None

    @property
    def size(self) -> tuple:
        return tuple(round(b - a, 6) for a, b in zip(self.min, self.max))


@dataclass
class LodBounds:
    name: str
    bounds: Bounds


def _vec4(buf: bytes, at: int) -> tuple:
    x, y, z, w = struct.unpack_from("<4f", buf, at)
    if w != 1.0:
        raise ValueError(f"vec4 at {at} has w={w}, not 1.0: layout does not match")
    return (x, y, z)


def parse_body_bounds(meta: bytes) -> Bounds:
    base = len(meta) - BODY_TAIL
    if base < 0:
        raise ValueError("metadata shorter than a body tail")
    lo, hi = _vec4(meta, base), _vec4(meta, base + 16)
    _vec4(meta, base + 32)
    return Bounds(lo, hi, struct.unpack_from("<f", meta, base + 48)[0])


def parse_metamesh_bounds(meta: bytes) -> list[LodBounds]:
    count = struct.unpack_from("<i", meta, LOD_COUNT_AT)[0]
    pos = LOD_FIRST
    out = []
    for _ in range(count):
        n = struct.unpack_from("<i", meta, pos + 16)[0]
        name = meta[pos + 20:pos + 20 + n].decode("ascii")
        tail = pos + 20 + n
        out.append(LodBounds(name, Bounds(_vec4(meta, tail + BOUNDS_MIN_AT), _vec4(meta, tail + BOUNDS_MAX_AT))))
        pos = tail + LOD_TAIL
    return out


def main(argv: list[str]) -> int:
    import tpac_clone_metamesh as t
    pkg = t.parse(Path(argv[0]).read_bytes())
    for it in pkg.items:
        if it.name not in argv[1:]:
            continue
        s, e = t._metadata_span(it)
        meta = bytes(it.toc[s + 8:e])
        if it.type_guid.hex().startswith(BODY_TYPE):
            b = parse_body_bounds(meta)
            print(f"{it.name} body min={b.min} max={b.max} size={b.size} radius={b.radius}")
        elif it.type_guid.hex().startswith(METAMESH_TYPE):
            for lod in parse_metamesh_bounds(meta):
                print(f"{it.name} lod {lod.name} min={lod.bounds.min} max={lod.bounds.max} size={lod.bounds.size}")
        else:
            print(f"{it.name} type {it.type_guid.hex()} not handled")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
