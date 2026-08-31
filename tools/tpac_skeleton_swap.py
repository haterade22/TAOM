#!/usr/bin/env python3
"""
Replace a Skeleton item in a target tpac with the same-named Skeleton from a source tpac,
KEEPING THE TARGET'S ITEM GUID so every animation clip whose Owner Skeleton points at the
target's skeleton keeps resolving.

WHY THIS EXISTS
An FBX re-import through the Modding Kit rebuilds a skeleton's bone definition but NOT its
SkeletonUserData: every Body comes back as body_type='none' with mass 0, and every ragdoll
constraint is dropped. That data was authored in the Kit's Skeleton Inspector and lives only in
the compiled tpac, so no amount of re-importing recovers it (verified 2026-08-30: the warg's
source FBX contains no 'Collision', 'Ragdoll', 'capsule' or body-type token at all, and the
donor's FBX is byte-identical to ours).

When an older compile of the SAME skeleton still exists, that compile is the authored data.
Copy it rather than synthesising defaults (which is what tpac_skeleton_transplant.py does when
no good copy survives).

WHAT IT SWAPS
The whole Skeleton item: metadata, checksum, both segments (SkeletonDefinitionData +
SkeletonUserData) and their blobs. Only the 16-byte item_guid is rewritten, to the target's.
Every other item in the target is carried across untouched and all segment data-offsets are
recomputed, exactly as tpac_skeleton_inject.py does.

Validate the result with:
  python tools/tpac_skeleton_dump.py <out.tpac> <skeleton-name>
Bodies should show real body_type values and Constraints should be non-zero.

Usage:
  python tools/tpac_skeleton_swap.py <target.tpac> <source.tpac> <skeleton_name> <out.tpac> [--dry-run]
"""
import os
import struct
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from tpac_skeleton_inject import parse_items, MAGIC  # noqa: E402

GUID_OFF = 16  # item_guid sits immediately after the 16-byte type_guid in an item's TOC entry


def swap(target, source, skel_name, out_path, dry_run=False):
    pkg, t_items, t_data = parse_items(target)
    _, s_items, s_data = parse_items(source)

    t_skel = next((it for it in t_items if it['is_skel'] and it['name'] == skel_name), None)
    s_skel = next((it for it in s_items if it['is_skel'] and it['name'] == skel_name), None)
    if t_skel is None:
        sys.exit('target %s has no skeleton %r' % (target, skel_name))
    if s_skel is None:
        sys.exit('source %s has no skeleton %r' % (source, skel_name))

    keep_guid = bytes(t_skel['toc'][GUID_OFF:GUID_OFF + 16])
    donor_guid = bytes(s_skel['toc'][GUID_OFF:GUID_OFF + 16])
    print('target skeleton guid : %s   (KEPT)' % uuid.UUID(bytes_le=keep_guid))
    print('source skeleton guid : %s   (discarded)' % uuid.UUID(bytes_le=donor_guid))
    print('target skeleton item : %d bytes, %d segments' % (len(t_skel['toc']), len(t_skel['segs'])))
    print('source skeleton item : %d bytes, %d segments' % (len(s_skel['toc']), len(s_skel['segs'])))
    for label, it, data in (('target', t_skel, t_data), ('source', s_skel, s_data)):
        for i, (_rel, off, sto) in enumerate(it['segs']):
            print('   %s seg%d storage=%d' % (label, i, sto))

    s_skel = dict(s_skel)
    s_skel['toc'] = bytearray(s_skel['toc'])
    s_skel['toc'][GUID_OFF:GUID_OFF + 16] = keep_guid
    s_skel['src'] = s_data

    out_items = [s_skel if it is t_skel else it for it in t_items]

    toc_size = sum(len(it['toc']) for it in out_items)
    data_cur = 36 + toc_size
    blobs = []
    for it in out_items:
        src = it['src']
        for (rel_field, data_off, storage) in it['segs']:
            blobs.append(src[data_off:data_off + storage])
            struct.pack_into('<Q', it['toc'], rel_field, data_cur)
            data_cur += storage
    # offset 28..35 is the TOC SIZE; the engine derives data_start = 36 + toc_size from it.
    # Hardcoding it to zero makes the engine read the TOC as segment data (rglBuffer.cpp:899,
    # "Potential read/write miss match for rglVec3"). Verified tail == toc_size on 250 shipped tpacs.
    header = struct.pack('<II', MAGIC, 2) + pkg + struct.pack('<I', len(out_items)) + struct.pack('<Q', toc_size)
    out = header + b''.join(bytes(it['toc']) for it in out_items) + b''.join(blobs)

    print('\nitems out: %d  %s' % (len(out_items), [it['name'] for it in out_items]))
    print('package guid kept    : %s' % uuid.UUID(bytes_le=pkg))
    print('size %d -> %d bytes (%+d)' % (os.path.getsize(target), len(out), len(out) - os.path.getsize(target)))
    if dry_run:
        print('(dry-run; nothing written)')
        return out
    open(out_path, 'wb').write(out)
    print('wrote %s' % out_path)
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if len(args) != 4:
        print(__doc__)
        sys.exit(1)
    swap(args[0], args[1], args[2], args[3], dry_run='--dry-run' in sys.argv)


if __name__ == '__main__':
    main()
