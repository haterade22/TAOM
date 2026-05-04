#!/usr/bin/env python3
"""
Open a tpac, find a named Skeleton, decompress its SkeletonUserData segment,
and dump the bones, bodies, and constraints (including IK joints).

Schema mirrors TpacTool.Lib/SkeletonUserData.cs and SkeletonDefinitionData.cs.

Usage:
  python3 tpac_skeleton_dump.py <tpac-path> <skeleton-name>
"""
import struct
import sys
import os
import io
import lz4.block

SKELETON_TYPE_GUID = bytes.fromhex('d5a335c6bbeadd45883eaa57e4196113')
SKELDEF_TYPE_GUID = bytes.fromhex('377dd01120e76b40ab67c846f96a8771')
SKELUSR_TYPE_GUID = bytes.fromhex('6dc06a9b46a5af40a55540d301ab4b2f')


class Reader:
    def __init__(self, data: bytes):
        self.s = io.BytesIO(data)
    def u32(self): return struct.unpack('<I', self.s.read(4))[0]
    def i32(self): return struct.unpack('<i', self.s.read(4))[0]
    def u64(self): return struct.unpack('<Q', self.s.read(8))[0]
    def i64(self): return struct.unpack('<q', self.s.read(8))[0]
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
    def remaining(self):
        cur = self.s.tell()
        self.s.seek(0, 2); end = self.s.tell()
        self.s.seek(cur)
        return end - cur


def find_skeleton(tpac_path: str, target_name: str):
    """Return (item_offset, defdata_seg, userdata_seg) for the named Skeleton."""
    f = open(tpac_path, 'rb')
    magic = struct.unpack('<I', f.read(4))[0]
    assert magic == 0x43415054, f'not a tpac: 0x{magic:08x}'
    ver = struct.unpack('<I', f.read(4))[0]
    f.read(16)  # package_guid
    num_items = struct.unpack('<I', f.read(4))[0]
    f.read(8)   # padding

    for i in range(num_items):
        item_start = f.tell()
        type_guid = f.read(16)
        item_guid = f.read(16)
        item_ver = 0
        if ver > 1:
            item_ver = struct.unpack('<I', f.read(4))[0]
        n = struct.unpack('<i', f.read(4))[0]
        name = '' if n == 0 else f.read(n).decode('utf-8', errors='replace')
        meta_size = struct.unpack('<q', f.read(8))[0]
        f.seek(meta_size, 1)
        f.read(8)  # checksum
        seg_count = struct.unpack('<i', f.read(4))[0]
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
                'storage_format': storage_format,
                'actual_size': actual_size,
                'storage_size': storage_size,
                'data_offset': seg_offset,
            })
        udep_count = struct.unpack('<i', f.read(4))[0]
        f.seek(udep_count * 48, 1)

        if type_guid == SKELETON_TYPE_GUID and name == target_name:
            defdata = next((s for s in segments if s['type_guid'] == SKELDEF_TYPE_GUID), None)
            userdata = next((s for s in segments if s['type_guid'] == SKELUSR_TYPE_GUID), None)
            f.close()
            return item_start, defdata, userdata
    f.close()
    return None, None, None


def read_segment_data(tpac_path: str, seg: dict) -> bytes:
    """Seek to segment offset, read storage_size bytes, decompress if LZ4."""
    with open(tpac_path, 'rb') as f:
        f.seek(seg['data_offset'])
        compressed = f.read(seg['storage_size'])
    if seg['storage_format'] == 1:
        return lz4.block.decompress(compressed, uncompressed_size=seg['actual_size'])
    return compressed


def parse_skeleton_definition(data: bytes):
    r = Reader(data)
    name = r.sized_string()
    bone_count = r.i32()
    bones = []
    for _ in range(bone_count):
        bn = r.sized_string()
        parent_idx = r.i32()
        rest_frame = r.mat4()
        bones.append({'name': bn, 'parent_idx': parent_idx, 'rest_frame': rest_frame})
    return {'name': name, 'bones': bones, 'remaining_bytes': r.remaining()}


def parse_skeleton_userdata(data: bytes):
    r = Reader(data)
    bb_padding = r.f32()
    bb_min = r.vec4()
    bb_max = r.vec4()
    usage = r.sized_string()
    unknown_str = r.sized_string()
    unknown_guid = r.guid()
    body_count = r.i32()
    bodies = []
    for _ in range(body_count):
        b = {
            'bone_name': r.sized_string(),
            'enable_blend': r.b(),
            'type': r.sized_string(),
            'body_type': r.sized_string(),
            'mass': r.f32(),
            'ragdoll_pos1': r.vec4(),
            'ragdoll_pos2': r.vec4(),
            'ragdoll_radius': r.f32(),
            'collision_pos1': r.vec4(),
            'collision_pos2': r.vec4(),
            'collision_radius': r.f32(),
            'collision_max_radius': r.f32(),
        }
        bodies.append(b)
    unknown_int = r.i32()
    constraint_count = r.i32()
    constraints = []
    for _ in range(constraint_count):
        skipped = r.u32()
        ctype = r.sized_string()
        cname = r.sized_string()
        bone1 = r.sized_string()
        bone2 = r.sized_string()
        rot = r.quat()
        pos = r.vec4()
        c = {'type': ctype, 'name': cname, 'bone1': bone1, 'bone2': bone2, 'rot': rot, 'pos': pos}
        if ctype == 'hinge':
            c['unk_float1'] = r.f32()
            c['unk_float2'] = r.f32()
        elif ctype == 'd6':
            c['axis_lock_x'] = r.sized_string()
            c['axis_lock_y'] = r.sized_string()
            c['axis_lock_z'] = r.sized_string()
            c['twist_lock'] = r.sized_string()
            c['swing1_lock'] = r.sized_string()
            c['swing2_lock'] = r.sized_string()
            c['axis_limit'] = r.f32()
            c['twist_lower'] = r.f32()
            c['twist_upper'] = r.f32()
            c['swing1_limit'] = r.f32()
            c['swing2_limit'] = r.f32()
        elif ctype == 'ik':
            c['unk_uint'] = r.u32()
            c['swing1_limit'] = r.f32()
            c['swing2_limit'] = r.f32()
            c['twist_lower'] = r.f32()
            c['twist_upper'] = r.f32()
        else:
            c['UNKNOWN_TYPE'] = True
        constraints.append(c)
    return {
        'bb_padding': bb_padding, 'bb_min': bb_min, 'bb_max': bb_max,
        'usage': usage, 'unknown_str': unknown_str, 'unknown_guid': unknown_guid,
        'bodies': bodies, 'unknown_int': unknown_int, 'constraints': constraints,
        'remaining_bytes': r.remaining(),
    }


def main():
    if len(sys.argv) != 3:
        print('Usage: python3 tpac_skeleton_dump.py <tpac-path> <skeleton-name>')
        sys.exit(1)
    tpac_path, target_name = sys.argv[1], sys.argv[2]
    item_off, defseg, usrseg = find_skeleton(tpac_path, target_name)
    if item_off is None:
        print(f'Skeleton {target_name!r} not found in {tpac_path}')
        sys.exit(2)
    print(f'\n=== Skeleton {target_name!r} ===')
    print(f'  item_offset: 0x{item_off:x}')
    print(f'  defdata seg: storage={defseg["storage_size"]}  actual={defseg["actual_size"]}  offset=0x{defseg["data_offset"]:x}')
    print(f'  usrdata seg: storage={usrseg["storage_size"]}  actual={usrseg["actual_size"]}  offset=0x{usrseg["data_offset"]:x}')

    # Definition
    def_data = read_segment_data(tpac_path, defseg)
    parsed_def = parse_skeleton_definition(def_data)
    print(f'\n  Skeleton name (internal): {parsed_def["name"]!r}')
    print(f'  Bones: {len(parsed_def["bones"])} (definition unread bytes: {parsed_def["remaining_bytes"]})')
    print('  First 10 bones:')
    for b in parsed_def['bones'][:10]:
        print(f'    [{b["name"]!r:25s}] parent_idx={b["parent_idx"]}')
    if len(parsed_def['bones']) > 10:
        print(f'    ...and {len(parsed_def["bones"]) - 10} more')

    # UserData
    usr_data = read_segment_data(tpac_path, usrseg)
    parsed_usr = parse_skeleton_userdata(usr_data)
    print(f'\n  Usage: {parsed_usr["usage"]!r}')
    print(f'  Bounding box: padding={parsed_usr["bb_padding"]}  min={parsed_usr["bb_min"]}  max={parsed_usr["bb_max"]}')
    print(f'  Bodies: {len(parsed_usr["bodies"])}')
    for b in parsed_usr['bodies'][:5]:
        print(f'    [{b["bone_name"]!r:25s}] type={b["type"]!r:12s} body_type={b["body_type"]!r:12s} mass={b["mass"]:.2f}')
    if len(parsed_usr['bodies']) > 5:
        print(f'    ...and {len(parsed_usr["bodies"]) - 5} more')

    print(f'\n  Constraints: {len(parsed_usr["constraints"])}')
    for c in parsed_usr['constraints']:
        if c['type'] == 'ik':
            print(f'    [IK] {c["name"]!r:30s}  bone1={c["bone1"]!r:18s} bone2={c["bone2"]!r:18s}  swing1={c["swing1_limit"]:.3f} swing2={c["swing2_limit"]:.3f} twist=[{c["twist_lower"]:.3f},{c["twist_upper"]:.3f}] unk={c["unk_uint"]}')
        elif c['type'] == 'd6':
            print(f'    [d6] {c["name"]!r:30s}  bone1={c["bone1"]!r:18s} bone2={c["bone2"]!r:18s}')
            print(f'           locks: x={c["axis_lock_x"]!r} y={c["axis_lock_y"]!r} z={c["axis_lock_z"]!r} twist={c["twist_lock"]!r} sw1={c["swing1_lock"]!r} sw2={c["swing2_lock"]!r}')
            print(f'           limits: axis={c["axis_limit"]:.3f} twist=[{c["twist_lower"]:.3f},{c["twist_upper"]:.3f}] swing1={c["swing1_limit"]:.3f} swing2={c["swing2_limit"]:.3f}')
        else:
            print(f'    [{c["type"]:5s}] {c["name"]!r:30s}  bone1={c["bone1"]!r:18s} bone2={c["bone2"]!r:18s}')
    ik_count = sum(1 for c in parsed_usr['constraints'] if c['type'] == 'ik')
    hinge_count = sum(1 for c in parsed_usr['constraints'] if c['type'] == 'hinge')
    d6_count = sum(1 for c in parsed_usr['constraints'] if c['type'] == 'd6')
    print(f'\n  Summary: {ik_count} IK joints, {hinge_count} hinge joints, {d6_count} d6 joints')
    print(f'  Userdata unread bytes: {parsed_usr["remaining_bytes"]}')


if __name__ == '__main__':
    main()
