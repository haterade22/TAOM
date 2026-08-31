#!/usr/bin/env python3
"""Rename a Bannerlord Animation Clip by patching its `*_anm.tpac` on disk.

WHY THIS EXISTS. Renaming an animation clip inside the Modding Kit corrupts it: the Kit keeps
looking for the old name, the clip inspector reports `Size in KB = 0`, it refuses to save, the model
viewer draws a scrambled pose, and the renamed file can vanish outright. The only in-Kit remedy is to
restart the tools after every single rename. (Reported by Artem, 2026-08-29, and matched exactly by
TAOM's own log: `Unable to find data anim_war_ram_butt_a::Optimized animation` began firing at
06:20:09, the moment the war-ram clip was renamed, and repeated 93 times until the Kit was closed.)

The workflow this enables, which never renames anything inside the Kit:
  1. In the Kit, create the clip and leave it on the default `new_animation_clip` name. Set the
     source range, sample rate and flags. Save. Close the Kit.
  2. Run this tool to give it its real name.
  3. Reopen the Kit. It loads a correctly-named clip that was never renamed in-session.

FORMAT (verified 2026-08-29 against elephant_attack_1_anm.tpac and a default new_animation_clip):
    0   "TPAC", uint32 version (2)
    8   16-byte package GUID
    24  uint32 item count
    28  uint32 payload size == filesize - 36   <- MUST be corrected when the name length changes
    36  16-byte type GUID (same for every AnimationClip)
    52  16-byte item GUID (unique per asset; preserved, it is the asset's identity)
    72  int32 name length, then the name bytes
    ..  int64 metadata size, then the metadata (flags are length-prefixed strings in here)

Read-only by default; pass --apply to write. Never overwrites an existing target.
"""
from __future__ import annotations

import argparse
import os
import struct
import sys

MAGIC = b"TPAC"
NAME_OFFSET = 72
SIZE_FIELD_OFFSET = 28
SIZE_FIELD_DELTA = 36  # filesize - payload-size, constant across every sample measured


def read_clip(path: str) -> tuple[bytes, str]:
    with open(path, "rb") as handle:
        data = handle.read()
    if data[:4] != MAGIC:
        raise ValueError(f"not a tpac (magic is {data[:4]!r}): {path}")
    length = struct.unpack("<i", data[NAME_OFFSET:NAME_OFFSET + 4])[0]
    if not 1 <= length <= 200:
        raise ValueError(f"implausible name length {length}; layout may have changed")
    name = data[NAME_OFFSET + 4:NAME_OFFSET + 4 + length].decode("ascii")
    declared = struct.unpack("<i", data[SIZE_FIELD_OFFSET:SIZE_FIELD_OFFSET + 4])[0]
    if declared != len(data) - SIZE_FIELD_DELTA:
        raise ValueError(
            f"size field {declared} != filesize-{SIZE_FIELD_DELTA} ({len(data) - SIZE_FIELD_DELTA}); "
            "refusing to patch a file whose layout I do not recognise")
    return data, name


def rename(data: bytes, new_name: str) -> bytes:
    old_len = struct.unpack("<i", data[NAME_OFFSET:NAME_OFFSET + 4])[0]
    new_bytes = new_name.encode("ascii")
    head = data[:NAME_OFFSET]
    tail = data[NAME_OFFSET + 4 + old_len:]
    out = head + struct.pack("<i", len(new_bytes)) + new_bytes + tail
    # correct the payload-size field for the new total length
    out = (out[:SIZE_FIELD_OFFSET]
           + struct.pack("<i", len(out) - SIZE_FIELD_DELTA)
           + out[SIZE_FIELD_OFFSET + 4:])
    return out


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("path", help="the *_anm.tpac to rename")
    ap.add_argument("new_name", help="the clip's new name, e.g. war_ram_butt")
    ap.add_argument("--apply", action="store_true", help="write the renamed file (default: dry run)")
    ap.add_argument("--keep-original", action="store_true", help="leave the source file in place")
    args = ap.parse_args()

    if not args.new_name.replace("_", "").isalnum():
        print(f"refusing: {args.new_name!r} is not a plain identifier")
        return 1

    data, old = read_clip(args.path)
    out = rename(data, args.new_name)
    dest = os.path.join(os.path.dirname(args.path), f"{args.new_name}_anm.tpac")

    print(f"  {os.path.basename(args.path)}")
    print(f"    clip name : {old!r} -> {args.new_name!r}")
    print(f"    file size : {len(data)} -> {len(out)} bytes")
    print(f"    target    : {dest}")

    # prove the result re-parses before it is written anywhere
    check_name = out[NAME_OFFSET + 4:NAME_OFFSET + 4 + struct.unpack('<i', out[NAME_OFFSET:NAME_OFFSET + 4])[0]].decode("ascii")
    declared = struct.unpack("<i", out[SIZE_FIELD_OFFSET:SIZE_FIELD_OFFSET + 4])[0]
    ok = check_name == args.new_name and declared == len(out) - SIZE_FIELD_DELTA
    print(f"    re-parse  : name={check_name!r} size_field={declared} -> {'OK' if ok else 'FAILED'}")
    if not ok:
        return 1

    if not args.apply:
        print("    (dry run; pass --apply to write)")
        return 0
    if os.path.exists(dest):
        print(f"    REFUSING: {dest} already exists")
        return 1
    with open(dest, "wb") as handle:
        handle.write(out)
    if not args.keep_original and os.path.abspath(dest) != os.path.abspath(args.path):
        os.remove(args.path)
        print("    removed the original")
    print("    written")
    return 0


if __name__ == "__main__":
    sys.exit(main())
