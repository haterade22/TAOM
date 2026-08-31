#!/usr/bin/env python3
"""
Inject a Skeleton resource from a source tpac INTO a target mesh/geo tpac, producing a new tpac
that bundles the skeleton WITH the meshes — the PROVEN structure every working creature uses
(elephant_skeleton + meshes in adod_elephant_geo.tpac; the 06-12 working spider_correct_geo.tpac).

WHY (not the standalone extract): a STANDALONE skeleton-only tpac is an unproven structure and
crashed the engine's recursive resource loader (spider 2026-06-14; the extract's standalone tpac
also reused the item_guid as the package guid). Re-bundling the skeleton into the mesh tpac keeps
a distinct package guid and matches the proven layout. Use this after a mesh re-export drops the
skeleton: inject the skeleton from the `.backup` into the new mesh tpac.

Rebuilds the whole tpac: header (KEEPS the target's package guid) + all target items + the
skeleton item, with every segment data-offset recomputed. Validate the output with
tpac_skeleton_scan.py.

Usage:
  python tools/tpac_skeleton_inject.py <target_mesh.tpac> <source.tpac> <skeleton_name> <out.tpac> [--dry-run]
"""
import os
import struct
import sys

MAGIC = 0x43415054
SKELETON_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')


def parse_items(path):
    """Return (pkg_guid, [ {toc: bytearray, segs: [(rel_off_field, data_off, storage)], is_skel, name} ]).
    toc = the item's full TOC entry bytes; rel_off_field = byte pos of each segment's 8-byte offset
    field WITHIN toc; data_off/storage locate the blob in THIS file."""
    d = open(path, 'rb').read()
    if struct.unpack_from('<I', d, 0)[0] != MAGIC:
        sys.exit(f'NOT A TPAC: {path}')
    ver = struct.unpack_from('<I', d, 4)[0]
    pkg = d[8:24]
    num = struct.unpack_from('<I', d, 24)[0]
    pos = 36
    items = []
    for _ in range(num):
        start = pos
        tg = d[pos:pos + 16]; pos += 16
        pos += 16  # item_guid
        if ver > 1:
            pos += 4
        n = struct.unpack_from('<i', d, pos)[0]; pos += 4
        name = d[pos:pos + n].decode('utf-8', 'replace'); pos += n
        meta = struct.unpack_from('<q', d, pos)[0]; pos += 8 + meta
        pos += 8  # checksum
        segc = struct.unpack_from('<i', d, pos)[0]; pos += 4
        segs = []
        for _ in range(segc):
            off, _act, sto = struct.unpack_from('<QQQ', d, pos)
            segs.append((pos - start, off, sto))   # rel offset-field pos, data offset, storage size
            pos += 69
        ud = struct.unpack_from('<i', d, pos)[0]; pos += 4 + ud * 48
        items.append({'toc': bytearray(d[start:pos]), 'segs': segs,
                      'is_skel': tg == SKELETON_GUID, 'name': name, 'src': d})
    return pkg, items, d


def inject(target, source, skel_name, out_path, dry_run=False):
    pkg, t_items, t_data = parse_items(target)
    _, s_items, s_data = parse_items(source)
    skel = next((it for it in s_items if it['is_skel'] and it['name'] == skel_name), None)
    if not skel:
        sys.exit(f'skeleton {skel_name!r} not in {source}')
    if any(it['is_skel'] and it['name'] == skel_name for it in t_items):
        sys.exit(f'{target} already contains skeleton {skel_name!r}; nothing to do')
    skel['src'] = s_data
    out_items = t_items + [skel]

    # header (KEEP target package guid — distinct from any item guid) + TOC + data
    toc_size = sum(len(it['toc']) for it in out_items)
    data_cur = 36 + toc_size
    blobs = []
    for it in out_items:
        src = it['src']
        for (rel_field, data_off, storage) in it['segs']:
            blobs.append(src[data_off:data_off + storage])
            struct.pack_into('<Q', it['toc'], rel_field, data_cur)   # rewrite segment offset
            data_cur += storage
    # offset 28..35 is the TOC SIZE; the engine derives data_start = 36 + toc_size from it.
    # Hardcoding it to zero makes the engine read the TOC as segment data (rglBuffer.cpp:899,
    # "Potential read/write miss match for rglVec3"). Verified tail == toc_size on 250 shipped tpacs.
    header = struct.pack('<II', MAGIC, 2) + pkg + struct.pack('<I', len(out_items)) + struct.pack('<Q', toc_size)
    out = header + b''.join(bytes(it['toc']) for it in out_items) + b''.join(blobs)

    names = [it['name'] for it in out_items]
    print(f'inject {skel_name!r} into {os.path.basename(target)}: {len(out_items)} items '
          f'{names}, out {len(out)} bytes')
    if dry_run:
        print('(dry-run; not written)')
        return out
    open(out_path, 'wb').write(out)
    print(f'wrote {out_path} ({len(out)} bytes)')
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if len(args) != 4:
        print('Usage: python tpac_skeleton_inject.py <target_mesh.tpac> <source.tpac> <skel_name> <out.tpac> [--dry-run]')
        sys.exit(1)
    inject(args[0], args[1], args[2], args[3], dry_run='--dry-run' in sys.argv)


if __name__ == '__main__':
    main()
