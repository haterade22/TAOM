#!/usr/bin/env python3
"""
Patch a skeleton tpac to populate its SkeletonUserData with default Body records
and D6 ragdoll constraints, mirroring the structure that works in Alliance.Wargs.

Symptom this addresses:
  Spider skeleton has Usage='other', 62 placeholder Bodies (body_type='none', mass=0),
  and 0 Constraints. Engine animation playback applies bone transforms with no
  reference frame -> mesh contorts wildly during ANY animation.

After patching:
  Usage='horse', 62 Bodies (body_type='abdomen', mass scales by bone depth),
  61 D6 joint constraints (one per parent-child link) with warg-default
  locked-translation/limited-swing limits.

Usage:
  python3 tpac_skeleton_transplant.py <tpac-path> <skeleton-name> [--dry-run] [--out <new-path>]

Schema mirrors TpacTool.Lib's SkeletonUserData and SkeletonDefinitionData readers.
"""
import struct
import sys
import os
import io
import shutil
import lz4.block

SKELETON_TYPE_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')
SKELDEF_TYPE_GUID  = bytes.fromhex('377dd01120e76b40ab67c846f96a8771')
SKELUSR_TYPE_GUID  = bytes.fromhex('6dc06a9b46a5af40a55540d301ab4b2f')


class Reader:
    def __init__(self, data: bytes):
        self.s = io.BytesIO(data)
    def u32(self): return struct.unpack('<I', self.s.read(4))[0]
    def i32(self): return struct.unpack('<i', self.s.read(4))[0]
    def u64(self): return struct.unpack('<Q', self.s.read(8))[0]
    def f32(self): return struct.unpack('<f', self.s.read(4))[0]
    def b(self): return self.s.read(1)[0] != 0
    def vec4(self): return struct.unpack('<ffff', self.s.read(16))
    def quat(self): return struct.unpack('<ffff', self.s.read(16))
    def mat4(self): return struct.unpack('<16f', self.s.read(64))
    def guid(self): return self.s.read(16)
    def sized_string(self):
        n = self.i32()
        if n == 0: return ''
        return self.s.read(n).decode('utf-8', errors='replace')


class Writer:
    def __init__(self):
        self.buf = io.BytesIO()
    def u32(self, v): self.buf.write(struct.pack('<I', v))
    def i32(self, v): self.buf.write(struct.pack('<i', v))
    def f32(self, v): self.buf.write(struct.pack('<f', v))
    def b(self, v): self.buf.write(b'\x01' if v else b'\x00')
    def vec4(self, v): self.buf.write(struct.pack('<ffff', *v))
    def quat(self, v): self.buf.write(struct.pack('<ffff', *v))
    def guid(self, g): self.buf.write(g)
    def sized_string(self, s):
        if not s:
            self.buf.write(b'\x00\x00\x00\x00')
        else:
            data = s.encode('utf-8')
            self.buf.write(struct.pack('<i', len(data)))
            self.buf.write(data)
    def bytes_(self): return self.buf.getvalue()


def parse_tpac_items(path):
    """Parse the tpac TOC. Returns (header_bytes, items[], segment_data_start_offset)."""
    f = open(path, 'rb')
    raw = f.read()
    f.close()
    s = io.BytesIO(raw)
    magic = struct.unpack('<I', s.read(4))[0]
    assert magic == 0x43415054, f'not a tpac: 0x{magic:08x}'
    ver = struct.unpack('<I', s.read(4))[0]
    package_guid = s.read(16)
    num_items = struct.unpack('<I', s.read(4))[0]
    s.read(8)
    items = []
    for i in range(num_items):
        item_start = s.tell()
        type_guid = s.read(16)
        item_guid = s.read(16)
        item_ver = 0
        if ver > 1:
            item_ver = struct.unpack('<I', s.read(4))[0]
        nlen = struct.unpack('<i', s.read(4))[0]
        name = '' if nlen == 0 else s.read(nlen).decode('utf-8')
        meta_size = struct.unpack('<q', s.read(8))[0]
        meta_pos = s.tell()
        s.seek(meta_size, 1)
        checksum_pos = s.tell()
        checksum = struct.unpack('<q', s.read(8))[0]
        seg_count = struct.unpack('<i', s.read(4))[0]
        segments = []
        seg_header_start = s.tell()
        for sg in range(seg_count):
            seg_pos = s.tell()
            seg_offset = struct.unpack('<Q', s.read(8))[0]
            actual_size = struct.unpack('<Q', s.read(8))[0]
            storage_size = struct.unpack('<Q', s.read(8))[0]
            owner_guid = s.read(16)
            seg_type_guid = s.read(16)
            unknown_ulong = struct.unpack('<Q', s.read(8))[0]
            unknown_uint = struct.unpack('<I', s.read(4))[0]
            storage_format = s.read(1)[0]
            segments.append({
                'header_pos': seg_pos,
                'data_offset': seg_offset,
                'actual_size': actual_size,
                'storage_size': storage_size,
                'owner_guid': owner_guid,
                'type_guid': seg_type_guid,
                'unknown_ulong': unknown_ulong,
                'unknown_uint': unknown_uint,
                'storage_format': storage_format,
            })
        udep_pos = s.tell()
        udep_count = struct.unpack('<i', s.read(4))[0]
        s.seek(udep_count * 48, 1)
        item_end = s.tell()
        items.append({
            'index': i,
            'item_start': item_start,
            'type_guid': type_guid,
            'item_guid': item_guid,
            'item_ver': item_ver,
            'name': name,
            'meta_pos': meta_pos,
            'meta_size': meta_size,
            'checksum_pos': checksum_pos,
            'checksum': checksum,
            'segments': segments,
            'udep_pos': udep_pos,
            'udep_count': udep_count,
            'item_end': item_end,
        })
    toc_end = s.tell()
    return raw, ver, package_guid, items, toc_end


def parse_definition(data: bytes):
    r = Reader(data)
    name = r.sized_string()
    bone_count = r.i32()
    bones = []
    for _ in range(bone_count):
        bn = r.sized_string()
        parent_idx = r.i32()
        rest_frame = r.mat4()
        bones.append({'name': bn, 'parent_idx': parent_idx, 'rest_frame': rest_frame})
    return {'name': name, 'bones': bones}


def classify_bone(name: str) -> dict:
    """Map a spider bone name to its joint group and physics defaults.

    Handles BOTH naming conventions:
    - Old (spider_skeleton, 62 bones): lowercase 'root_m', 'joint40_l', 'joint5_r'
    - New (erkamspider_skeleton, 58 bones): mixed 'Root_M', 'spine1_M', 'joint40_L', 'joint5_R'

    Classification is case-insensitive and prefix-based. Defaults per group are
    heuristic — picked from anatomy of an 8-legged spider rig. Tunable; if visual
    results need refining, edit values here.

    Returns dict with: group, body_type, mass, swing1_limit, swing2_limit.
    """
    n = name.lower()

    # Body axis: stiff, modest swing
    if n in ('root_m', 'spine1_m', 'spine2_m', 'chest_m'):
        return {'group': 'body_axis',  'body_type': 'abdomen', 'mass': 8.0, 'swing1': 0.30, 'swing2': 0.50}

    # Head + mouth: look-around freedom (joint12_m only present in old 62-bone skeleton)
    if n in ('head_m', 'joint12_m'):
        return {'group': 'head',       'body_type': 'abdomen', 'mass': 3.0, 'swing1': 0.70, 'swing2': 1.20}

    # Fangs (chelicerae): bite articulation
    if n in ('joint5_l', 'joint5_r'):
        return {'group': 'fang',       'body_type': 'abdomen', 'mass': 0.5, 'swing1': 0.40, 'swing2': 0.60}

    # Abdomen + stinger: tail-like bend (joint16_m only in old skeleton)
    if n in ('joint13_m', 'joint14_m', 'joint15_m', 'joint16_m'):
        return {'group': 'abdomen',    'body_type': 'abdomen', 'mass': 4.0, 'swing1': 0.30, 'swing2': 0.50}

    # Pedipalps: held near mouth, articulate freely (joint21 only in old skeleton)
    if n.startswith('joint17_') or n.startswith('joint18_') or n.startswith('joint19_') \
       or n.startswith('joint20_') or n.startswith('joint21_'):
        return {'group': 'pedipalp',   'body_type': 'abdomen', 'mass': 0.3, 'swing1': 0.80, 'swing2': 1.40}

    # All four pairs of legs (back -> front: 22-26, 28-32, 34-38, 40-44)
    # Standard quadruped leg constraint pattern from warg's leg chain.
    leg_prefixes = (
        'joint22_', 'joint23_', 'joint24_', 'joint25_', 'joint26_',
        'joint28_', 'joint29_', 'joint30_', 'joint31_', 'joint32_',
        'joint34_', 'joint35_', 'joint36_', 'joint37_', 'joint38_',
        'joint40_', 'joint41_', 'joint42_', 'joint43_', 'joint44_',
    )
    if any(n.startswith(p) for p in leg_prefixes):
        return {'group': 'leg',        'body_type': 'abdomen', 'mass': 0.6, 'swing1': 0.40, 'swing2': 0.80}

    # Fallback: unrecognized bone, use warg-style defaults
    return {'group': 'unknown',        'body_type': 'abdomen', 'mass': 1.0, 'swing1': 0.50, 'swing2': 1.00}


def build_userdata(parsed_def: dict, original_userdata: dict | None) -> bytes:
    """Build a new SkeletonUserData buffer.

    parsed_def: output of parse_definition (gives us bone names + parents)
    original_userdata: the existing parsed UserData (we keep its bb_padding,
                       bb_min/max, unknown_str, unknown_guid, unknown_int).
    """
    bones = parsed_def['bones']
    w = Writer()
    if original_userdata:
        w.f32(original_userdata['bb_padding'])
        w.vec4(original_userdata['bb_min'])
        w.vec4(original_userdata['bb_max'])
    else:
        w.f32(0.0)
        w.vec4((0.0, 0.0, 0.0, 1.0))
        w.vec4((0.0, 0.0, 0.0, 1.0))

    # Usage: 'horse' (force regardless of original)
    w.sized_string('horse')

    if original_userdata:
        w.sized_string(original_userdata['unknown_str'])
        w.guid(original_userdata['unknown_guid'])
        unknown_int = original_userdata['unknown_int']
    else:
        w.sized_string('')
        w.guid(b'\x00' * 16)
        unknown_int = 0

    # Bodies: per-bone, classified by name pattern
    w.i32(len(bones))
    group_counts = {}
    for b in bones:
        cls = classify_bone(b['name'])
        group_counts[cls['group']] = group_counts.get(cls['group'], 0) + 1
        w.sized_string(b['name'])    # bone_name
        w.b(False)                    # enable_blend = false
        w.sized_string('')            # type = ''
        w.sized_string(cls['body_type'])
        w.f32(cls['mass'])
        w.vec4((0.0, 0.0, 0.0, 1.0))  # ragdoll_pos1
        w.vec4((0.0, 0.0, 0.0, 1.0))  # ragdoll_pos2
        w.f32(0.05)                   # ragdoll_radius
        w.vec4((0.0, 0.0, 0.0, 1.0))  # collision_pos1
        w.vec4((0.0, 0.0, 0.0, 1.0))  # collision_pos2
        w.f32(0.05)                   # collision_radius
        w.f32(0.05)                   # collision_max_radius

    # Print classification summary (visible during transplant run)
    print('  Bone classification:')
    for g, c in sorted(group_counts.items()):
        print(f'    {g:12s}: {c} bones')

    w.i32(unknown_int)

    # Constraints: one D6 per bone with a parent. Swing limits are taken from
    # the CHILD bone's classification (child is bone1 in the joint).
    pairs = [(i, b) for i, b in enumerate(bones) if b['parent_idx'] >= 0]
    w.i32(len(pairs))
    for i, b in pairs:
        parent_name = bones[b['parent_idx']]['name']
        child_name = b['name']
        cls = classify_bone(child_name)
        w.u32(0)                                            # padding/skipped uint
        w.sized_string('d6')                                 # type
        w.sized_string(f'joint_{parent_name}_{child_name}')  # name
        w.sized_string(child_name)                           # bone1 (child)
        w.sized_string(parent_name)                          # bone2 (parent)
        w.quat((1.0, 0.0, 0.0, 0.0))                         # rot identity
        w.vec4((0.0, 0.0, 0.0, 1.0))                         # position
        # D6 lock flags — same as warg pattern
        w.sized_string('locked')
        w.sized_string('locked')
        w.sized_string('locked')
        w.sized_string('locked')
        w.sized_string('limited')
        w.sized_string('limited')
        w.f32(0.010)               # axis_limit
        w.f32(0.0)                 # twist_lower
        w.f32(0.0)                 # twist_upper
        w.f32(cls['swing1'])       # swing1_limit (group-specific)
        w.f32(cls['swing2'])       # swing2_limit (group-specific)

    return w.bytes_()


def parse_userdata(data: bytes):
    r = Reader(data)
    return {
        'bb_padding': r.f32(),
        'bb_min': r.vec4(),
        'bb_max': r.vec4(),
        'usage': r.sized_string(),
        'unknown_str': r.sized_string(),
        'unknown_guid': r.guid(),
        'unknown_int_pos': None,
        # We only need the first 6 fields + unknown_int for build_userdata defaults
        # the unknown_int comes after the bodies — re-parse fully if needed
    }


def parse_userdata_full(data: bytes):
    r = Reader(data)
    bb_padding = r.f32()
    bb_min = r.vec4()
    bb_max = r.vec4()
    usage = r.sized_string()
    unknown_str = r.sized_string()
    unknown_guid = r.guid()
    body_count = r.i32()
    for _ in range(body_count):
        r.sized_string(); r.b(); r.sized_string(); r.sized_string()
        r.f32(); r.vec4(); r.vec4(); r.f32(); r.vec4(); r.vec4(); r.f32(); r.f32()
    unknown_int = r.i32()
    return {
        'bb_padding': bb_padding, 'bb_min': bb_min, 'bb_max': bb_max,
        'usage': usage, 'unknown_str': unknown_str, 'unknown_guid': unknown_guid,
        'unknown_int': unknown_int,
    }


def main():
    args = sys.argv[1:]
    if len(args) < 2:
        print('Usage: python3 tpac_skeleton_transplant.py <tpac-path> <skeleton-name> [--dry-run] [--out <new-path>]')
        sys.exit(1)
    tpac_path = args[0]
    target_name = args[1]
    dry_run = '--dry-run' in args
    out_path = tpac_path
    if '--out' in args:
        out_path = args[args.index('--out') + 1]

    raw, container_ver, pkg_guid, items, toc_end = parse_tpac_items(tpac_path)
    skel = next((it for it in items if it['type_guid'] == SKELETON_TYPE_GUID and it['name'] == target_name), None)
    if not skel:
        print(f'Skeleton {target_name!r} not found in {tpac_path}')
        sys.exit(2)

    defseg = next((s for s in skel['segments'] if s['type_guid'] == SKELDEF_TYPE_GUID), None)
    usrseg = next((s for s in skel['segments'] if s['type_guid'] == SKELUSR_TYPE_GUID), None)
    assert defseg and usrseg, 'expected both definition and userdata segments'

    # Read existing definition + userdata
    def_compressed = raw[defseg['data_offset']:defseg['data_offset'] + defseg['storage_size']]
    def_raw = lz4.block.decompress(def_compressed, uncompressed_size=defseg['actual_size']) if defseg['storage_format'] == 1 else def_compressed
    parsed_def = parse_definition(def_raw)

    usr_compressed = raw[usrseg['data_offset']:usrseg['data_offset'] + usrseg['storage_size']]
    usr_raw = lz4.block.decompress(usr_compressed, uncompressed_size=usrseg['actual_size']) if usrseg['storage_format'] == 1 else usr_compressed
    parsed_usr = parse_userdata_full(usr_raw)

    print(f'\n=== {target_name} BEFORE ===')
    print(f'  bones in def: {len(parsed_def["bones"])}')
    print(f'  Usage: {parsed_usr["usage"]!r}')
    print(f'  segment storage={usrseg["storage_size"]}  actual={usrseg["actual_size"]}')

    # Build new userdata
    new_userdata = build_userdata(parsed_def, parsed_usr)
    new_compressed = lz4.block.compress(new_userdata, mode='high_compression', store_size=False)
    print(f'\n=== {target_name} NEW UserData ===')
    print(f'  Usage: \'horse\'')
    print(f'  Bodies: {len(parsed_def["bones"])}')
    print(f'  Constraints: {len([b for b in parsed_def["bones"] if b["parent_idx"] >= 0])} D6 joints')
    print(f'  Uncompressed size: {len(new_userdata)} bytes (was {usrseg["actual_size"]})')
    print(f'  Compressed size:   {len(new_compressed)} bytes (was {usrseg["storage_size"]})')

    if dry_run:
        print('\n[dry-run] no changes written')
        return

    # Patch strategy: rewrite the entire tpac with the new UserData inserted.
    # The data section starts at toc_end. UserData segment lives somewhere in
    # the data section. We need to:
    #   1. Build a new buffer containing all segments — replacing the old userdata
    #      bytes with the new compressed bytes.
    #   2. Update each segment's data_offset (in the TOC headers) to reflect new positions.
    #   3. Update the userdata segment's storage_size and actual_size in its TOC header.

    # Collect ALL segments in the file (across all items), in order of their data_offset.
    all_segs = []
    for it in items:
        for sg in it['segments']:
            all_segs.append((sg, it['name']))
    # sort by current data_offset
    all_segs.sort(key=lambda x: x[0]['data_offset'])

    # Build new data section
    new_data = bytearray()
    seg_new_offsets = {}  # id(seg) -> new_offset
    for sg, owner_name in all_segs:
        new_off = toc_end + len(new_data)
        seg_new_offsets[id(sg)] = new_off
        if id(sg) == id(usrseg):
            # Replace with new compressed userdata
            new_data.extend(new_compressed)
        else:
            # Copy original bytes
            new_data.extend(raw[sg['data_offset']:sg['data_offset'] + sg['storage_size']])

    # Build new TOC by editing the header bytes for each segment
    out = bytearray(raw[:toc_end])

    for sg, owner_name in all_segs:
        new_off = seg_new_offsets[id(sg)]
        hp = sg['header_pos']
        # offset (8) + actualSize (8) + storageSize (8)
        struct.pack_into('<Q', out, hp + 0, new_off)
        if id(sg) == id(usrseg):
            struct.pack_into('<Q', out, hp + 8, len(new_userdata))     # new actual_size
            struct.pack_into('<Q', out, hp + 16, len(new_compressed))  # new storage_size
        # Other segments keep their actual/storage sizes (unchanged)

    out.extend(new_data)

    # Backup
    backup = out_path + '.backup'
    if out_path == tpac_path and not os.path.exists(backup):
        shutil.copy2(tpac_path, backup)
        print(f'\n  Backup saved: {backup}')
    with open(out_path, 'wb') as f:
        f.write(out)
    print(f'  Wrote: {out_path} ({len(out):,} bytes)')


if __name__ == '__main__':
    main()
