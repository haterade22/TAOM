#!/usr/bin/env python3
"""Parse Bannerlord per-clip _anm.tpac AnimationClip metadata: name, flags, and
frame-range floats (Source1/Source2 etc.). Pure stdlib. For elephant clip slicing."""
import struct, os, glob, sys

def read_str(buf, off):
    n = struct.unpack('<i', buf[off:off+4])[0]
    if n < 0 or n > 100000:
        return None, off + 4
    return buf[off+4:off+4+n].decode('utf-8', 'replace'), off + 4 + n

def find_flag_strings(meta):
    out = []; i = 0; L = len(meta)
    while i < L - 4:
        n = struct.unpack('<i', meta[i:i+4])[0]
        if 1 <= n <= 40 and i + 4 + n <= L:
            s = meta[i+4:i+4+n]
            if all(32 <= c < 127 for c in s):
                out.append(s.decode('ascii')); i += 4 + n; continue
        i += 1
    return out

def frame_floats(meta):
    out = []
    for off in range(0, len(meta) - 3):
        v = struct.unpack('<f', meta[off:off+4])[0]
        if 1.0 < v <= 1400 and abs(v - round(v)) < 0.02:
            out.append((off, round(v)))
    return out

def parse(path):
    with open(path, 'rb') as f:
        buf = f.read()
    if buf[0:4] != b'TPAC':
        return {'file': os.path.basename(path), 'err': 'not tpac'}
    ver = struct.unpack('<I', buf[4:8])[0]
    off = 8 + 16
    num = struct.unpack('<I', buf[off:off+4])[0]; off += 4
    off += 8
    type_guid = buf[off:off+16]; off += 16
    off += 16  # item guid
    if ver > 1:
        off += 4
    name, off = read_str(buf, off)
    meta_size = struct.unpack('<q', buf[off:off+8])[0]; off += 8
    meta = buf[off:off+meta_size]
    ff = frame_floats(meta)
    # The frame-range pair is usually the first two integral floats > 1 in the meta head
    big = [v for (o, v) in ff if v > 5]
    src1 = big[0] if len(big) >= 1 else None
    src2 = big[1] if len(big) >= 2 else None
    return {'file': os.path.basename(path), 'name': name, 'items': num,
            'src1': src1, 'src2': src2, 'span': (src2 - src1) if (src1 and src2) else None,
            'flags': find_flag_strings(meta), 'all_frame_floats': [v for (o, v) in ff]}

if __name__ == '__main__':
    d = sys.argv[1] if len(sys.argv) > 1 else r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\Assets\creature\elephant\animations'
    files = sorted(glob.glob(os.path.join(d, 'elephant_*_anm.tpac')))
    print('%-40s %6s %6s %5s  %s' % ('clip', 'src1', 'src2', 'span', 'flags'))
    print('-' * 100)
    rows = []
    for fp in files:
        r = parse(fp)
        if 'err' in r:
            print('%-40s  %s' % (r['file'], r['err'])); continue
        rows.append(r)
        print('%-40s %6s %6s %5s  %s' % (r['name'] or r['file'], r['src1'], r['src2'], r['span'], ','.join(r['flags'])))
    print('\n# raw frame-floats (for clips where the heuristic looks off):')
    for r in rows:
        print('  %-36s %s' % (r['name'], r['all_frame_floats']))
