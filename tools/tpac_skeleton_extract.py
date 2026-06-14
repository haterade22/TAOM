#!/usr/bin/env python3
"""
Extract a single Skeleton asset from a Bannerlord .tpac into a NEW standalone tpac
containing ONLY that skeleton (no meshes/geometry), so it can be deployed loose to
register the skeleton resource without colliding with any mesh tpac.

WHY: a creature's skeleton resource (referenced by an action_set's skeleton="<name>")
must be present in a loaded tpac or CreateAgentSkeleton returns null -> riderless mount.
If a mesh re-export drops the skeleton from the geo tpac, this rebuilds a skeleton-only
tpac from a backup that still has it, WITHOUT reintroducing the (possibly stale/un-split)
backup meshes. Born 2026-06-13: the spider rework's new spider_correct_geo.tpac shipped
mesh-only; spider_skeleton survived only in sk_spider_forest_c_geo.tpac.backup.

Format (container ver 2, per tpac_skeleton_scan.py + AssetPackage.cs):
  header(36) = magic(4)+ver(4)+package_guid(16)+num_items(4)+pad(4)+pad(4)
  item TOC   = type_guid(16)+item_guid(16)+[item_ver(4) if ver>1]+name(i32 len+bytes)
               +meta_size(8)+metadata+checksum(8)+seg_count(4)+segments(69 each)+udeps(i32+N*48)
  segment(69)= offset(8)+actualSize(8)+storageSize(8)+ownerGuid(16)+typeGuid(16)
               +unknownUlong(8)+unknownUint(4)+storageFormat(1)   ; DATA at `offset` elsewhere

Usage:
  python tools/tpac_skeleton_extract.py <src.tpac> <skeleton_name> <out.tpac> [--dry-run]
"""
import os
import struct
import sys

MAGIC = 0x43415054
SKELETON_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')


def _rs(buf, off):
    n = struct.unpack_from('<i', buf, off)[0]
    return (buf[off + 4: off + 4 + n].decode('utf-8', 'replace'), off + 4 + n)


def extract(src_path, skel_name, out_path, dry_run=False):
    d = open(src_path, 'rb').read()
    magic, ver = struct.unpack_from('<II', d, 0)
    if magic != MAGIC:
        sys.exit(f'NOT A TPAC: {src_path} (magic=0x{magic:08x})')
    if ver < 2:
        sys.exit(f'unsupported container ver {ver} (need 2)')
    num_items = struct.unpack_from('<I', d, 24)[0]
    pos = 36  # after header (magic4+ver4+guid16+num4+pad4+pad4)

    for i in range(num_items):
        item_start = pos
        type_guid = d[pos: pos + 16]; pos += 16
        item_guid = d[pos: pos + 16]; pos += 16
        if ver > 1:
            pos += 4  # item_ver
        name, pos = _rs(d, pos)
        meta_size = struct.unpack_from('<q', d, pos)[0]; pos += 8
        pos += meta_size                                   # skip metadata
        pos += 8                                            # checksum
        seg_count = struct.unpack_from('<i', d, pos)[0]; pos += 4
        seg_field_pos = []   # absolute pos of each segment's 8-byte offset field
        segs = []            # (data_offset, storage_size)
        for s in range(seg_count):
            off_field = pos
            seg_offset, actual, storage = struct.unpack_from('<QQQ', d, pos)
            seg_field_pos.append(off_field)
            segs.append((seg_offset, storage))
            pos += 69
        udep_count = struct.unpack_from('<i', d, pos)[0]; pos += 4
        pos += udep_count * 48
        item_end = pos

        if type_guid == SKELETON_GUID and name == skel_name:
            toc = bytearray(d[item_start:item_end])   # full TOC entry, rewrite offsets below
            # new layout: header(36) + toc + blob0 + blob1 ...
            data_base = 36 + len(toc)
            blobs = []
            cur = data_base
            for k, (seg_off, storage) in enumerate(segs):
                blobs.append(d[seg_off: seg_off + storage])
                # rewrite this segment's offset field (relative within toc)
                rel = seg_field_pos[k] - item_start
                struct.pack_into('<Q', toc, rel, cur)
                cur += storage
            # build header: num_items=1, reuse the skeleton item_guid as package_guid (deterministic)
            header = struct.pack('<II', MAGIC, 2) + item_guid + struct.pack('<III', 1, 0, 0)
            out = header + bytes(toc) + b''.join(blobs)
            print(f'extracted {name!r}: {seg_count} segs, {sum(len(b) for b in blobs)} data bytes, '
                  f'out size {len(out)}')
            if dry_run:
                print('(dry-run; not written)')
                return out
            open(out_path, 'wb').write(out)
            print(f'wrote {out_path} ({len(out)} bytes)')
            return out

    sys.exit(f'skeleton {skel_name!r} not found in {src_path}')


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    dry = '--dry-run' in sys.argv
    if len(args) != 3:
        print('Usage: python tpac_skeleton_extract.py <src.tpac> <skeleton_name> <out.tpac> [--dry-run]')
        sys.exit(1)
    extract(args[0], args[1], args[2], dry_run=dry)


if __name__ == '__main__':
    main()
