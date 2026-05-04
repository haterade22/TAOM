#!/usr/bin/env python3
"""
Stream-scan a Bannerlord .tpac AssetPackage and list every Skeleton asset.

For each Skeleton found, prints:
  - name
  - asset_guid
  - file offset of the AssetItem header (so we can locate it for read/edit later)
  - metadata size
  - data segment count + their type_guids and storage info

Designed for 1.3 tpac format (container version 2). Avoids the TpacTool bug where
Skeleton.ReadMetadata reads exactly 21 bytes regardless of declared metadata_size.

Usage:
  python3 tpac_skeleton_scan.py <path-to-tpac> [--all-types]
"""
import struct
import sys
import os
from pathlib import Path

# Known TYPE_GUIDs from TpacTool.Lib decompile
TYPE_GUIDS = {
    bytes.fromhex('5fce46680596c44b8db21edaa9408411'): 'Geometry',
    # Skeleton TYPE_GUID from Skeleton.cs: c635a3d5-eabb-45dd-883e-aa57e4196113
    # Convert from .NET Guid little-endian: first 3 fields LE, last 8 bytes BE
    bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113'): 'Skeleton',
    # SkeletonDefinitionData TYPE_GUID = 11d07d37-e720-406b-ab67-c846f96a8771
    bytes.fromhex('377dd01120e76b40ab67c846f96a8771'): 'SkeletonDefinitionData',
    # SkeletonUserData TYPE_GUID = 9b6ac06d-a546-40af-a555-40d301ab4b2f
    bytes.fromhex('6dc06a9b46a5af40a55540d301ab4b2f'): 'SkeletonUserData',
}

SKELETON_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')


def guid_to_str(g: bytes) -> str:
    """Convert .NET Guid little-endian bytes to canonical string."""
    a = struct.unpack('<I', g[0:4])[0]
    b = struct.unpack('<H', g[4:6])[0]
    c = struct.unpack('<H', g[6:8])[0]
    d = g[8:16]
    return f'{a:08x}-{b:04x}-{c:04x}-{d[0]:02x}{d[1]:02x}-{d[2]:02x}{d[3]:02x}{d[4]:02x}{d[5]:02x}{d[6]:02x}{d[7]:02x}'


def read_sized_string(f) -> str:
    n = struct.unpack('<i', f.read(4))[0]
    if n == 0:
        return ''
    return f.read(n).decode('utf-8', errors='replace')


def scan_tpac(path: str, only_skeletons: bool = True, max_results: int = 50):
    sz = os.path.getsize(path)
    with open(path, 'rb') as f:
        magic = struct.unpack('<I', f.read(4))[0]
        if magic != 0x43415054:
            print(f'NOT A TPAC: {path} (magic=0x{magic:08x})')
            return
        ver = struct.unpack('<I', f.read(4))[0]
        package_guid = f.read(16)
        num_items = struct.unpack('<I', f.read(4))[0]
        f.read(4)  # padding
        f.read(4)  # padding

        print(f'\n=== {os.path.basename(path)} ===')
        print(f'  size: {sz:,} bytes  ver={ver}  items={num_items}')
        print(f'  package_guid: {guid_to_str(package_guid)}')

        results = 0
        for i in range(num_items):
            item_start = f.tell()
            type_guid = f.read(16)
            item_guid = f.read(16)
            item_ver = 0
            if ver > 1:
                item_ver = struct.unpack('<I', f.read(4))[0]
            name = read_sized_string(f)
            meta_size = struct.unpack('<q', f.read(8))[0]
            meta_start = f.tell()
            f.seek(meta_size, 1)  # skip metadata
            checksum = struct.unpack('<q', f.read(8))[0]
            seg_count = struct.unpack('<i', f.read(4))[0]

            type_name = TYPE_GUIDS.get(type_guid, f'unknown:{guid_to_str(type_guid)}')
            is_skel = (type_guid == SKELETON_GUID)

            # Segment headers (per AssetPackage.cs:163-178). Each segment is 69 bytes:
            #   offset(8) + actualSize(8) + storageSize(8) + ownerGuid(16) + typeGuid(16)
            #   + unknownUlong(8) + unknownUint(4) + storageFormat(1)
            # Data lives at `offset` elsewhere in the file (not inline).
            segments = []
            for s in range(seg_count):
                seg_offset = struct.unpack('<Q', f.read(8))[0]
                actual_size = struct.unpack('<Q', f.read(8))[0]
                storage_size = struct.unpack('<Q', f.read(8))[0]
                owner_guid = f.read(16)
                seg_type_guid = f.read(16)
                unknown_ulong = struct.unpack('<Q', f.read(8))[0]
                unknown_uint = struct.unpack('<I', f.read(4))[0]
                storage_format = f.read(1)[0]
                segments.append({
                    'type_guid': seg_type_guid,
                    'type_name': TYPE_GUIDS.get(seg_type_guid, f'unknown:{guid_to_str(seg_type_guid)}'),
                    'storage_format': storage_format,
                    'actual_size': actual_size,
                    'storage_size': storage_size,
                    'data_offset': seg_offset,
                })

            # Scan UnknownDependences list — read int32 count + N * (3 GUIDs)
            try:
                udep_count = struct.unpack('<i', f.read(4))[0]
                if 0 <= udep_count <= 1000:
                    f.seek(udep_count * 48, 1)
                else:
                    print(f'  [WARN at item {i} ({name!r}): suspicious udep_count={udep_count}, parser may be drifting]')
                    return
            except Exception as e:
                print(f'  [WARN at item {i} ({name!r}): {e}, parser drifted]')
                return

            if (not only_skeletons) or is_skel:
                results += 1
                print(f'\n  [{i:4d}] type={type_name:25s}  ver={item_ver}  name={name!r}')
                print(f'         item_offset=0x{item_start:x}  meta_size={meta_size}  segs={seg_count}')
                if is_skel:
                    print(f'         item_guid: {guid_to_str(item_guid)}')
                for s in segments:
                    print(f'           segment: {s["type_name"]:30s} storage_size={s["storage_size"]:>10}  actual={s["actual_size"]:>10}  fmt={s["storage_format"]}  data_offset=0x{s["data_offset"]:x}')
                if results >= max_results:
                    print(f'\n  [stopping at {max_results} results]')
                    return


def main():
    paths = []
    only_skeletons = True
    for arg in sys.argv[1:]:
        if arg == '--all-types':
            only_skeletons = False
        else:
            paths.append(arg)
    if not paths:
        print('Usage: python3 tpac_skeleton_scan.py <tpac> [tpac...] [--all-types]')
        sys.exit(1)
    for p in paths:
        scan_tpac(p, only_skeletons=only_skeletons)


if __name__ == '__main__':
    main()
