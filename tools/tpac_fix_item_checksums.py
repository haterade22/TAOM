#!/usr/bin/env python3
"""Recompute the per-item xxHash64 checksum in a tpac after an in-place metadata patch.

Why: the 8 bytes after an item's metadata are xxHash64 (seed 0) over the int64 metadata length
plus the metadata (docs/reviews/lessons/animation-skeleton.md, 2026-09-17). A tool that patches
metadata bytes in place (tools/gen_troll_anim_clips.ps1 writes the human_skeleton GUID into a
SkeletalAnimation master's empty Skeleton field) leaves that checksum stale. A stale hash that
collides with nothing still loads, but the rule is to recompute it, re-parse, and assert.

Usage:
    python tools/tpac_fix_item_checksums.py <file-or-dir> [--glob '*_geo.tpac'] [--apply]
    python tools/tpac_fix_item_checksums.py <file>            # report only
Dry run by default. --apply rewrites only the 8 checksum bytes per stale item and re-verifies.
Pure stdlib apart from xxhash (tpac_clone_metamesh.py's dependency); reuses that module's parser.
"""
import argparse
import glob
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpac_clone_metamesh as tcm  # noqa: E402

HEADER_SIZE = tcm.HEADER_SIZE


def item_checksum_offsets(data: bytes):
    """Yield (item, absolute offset of its 8-byte checksum) for every item in a version-2 tpac."""
    pkg = tcm.parse(data)
    pos = HEADER_SIZE
    for item in pkg.items:
        start, end = tcm._metadata_span(item)
        yield item, pos + end
        pos += len(item.toc)


def check_file(path: str, apply: bool) -> tuple[int, int]:
    data = bytearray(open(path, "rb").read())
    stale = 0
    fixed = 0
    for item, off in item_checksum_offsets(bytes(data)):
        want = tcm.expected_checksum(item)
        have = bytes(data[off:off + 8])
        if have != want:
            stale += 1
            print("  STALE %-44s %s  have=%s want=%s" % (os.path.basename(path), item.name, have.hex(), want.hex()))
            if apply:
                data[off:off + 8] = want
                fixed += 1
    if apply and fixed:
        open(path, "wb").write(bytes(data))
        # re-verify
        for item, off in item_checksum_offsets(bytes(data)):
            if bytes(data[off:off + 8]) != tcm.expected_checksum(item):
                raise SystemExit("re-verify failed: %s %s" % (path, item.name))
    return stale, fixed


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("target", help="a tpac file or a directory")
    ap.add_argument("--glob", default="*.tpac")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args(argv)
    files = [args.target] if os.path.isfile(args.target) else sorted(glob.glob(os.path.join(args.target, args.glob)))
    tot_stale = tot_fixed = 0
    for f in files:
        s, x = check_file(f, args.apply)
        tot_stale += s
        tot_fixed += x
    print("files=%d stale-items=%d %s" % (len(files), tot_stale, ("fixed=%d" % tot_fixed) if args.apply else "(dry run)"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
