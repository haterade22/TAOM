#!/usr/bin/env python3
"""Graft the quad_movement clip-usage + step points onto the two chariot gait clips that
lack it (chariot_gait_walkfast / chariot_gait_walkbackfast), copying the meta TAIL from a
tagged donor walk clip. Parse-based (handles different name lengths). Backs up to .bak-untagged.

Structure (v2 single-item AnimationClip _anm.tpac):
  TPAC | ver | pkg_guid(16) | num_items | content_size@0x1C (=filesize-36) | pad |
  item: type_guid(16) item_guid(16) item_ver(4) name(len4+str) meta_size(q8) meta[...] checksum(q8) seg_count(4=0) udep(4=0)
  meta tail after the last flag string: [ClipUsages count][len][quad_movement] [empty list count=0] [3 step floats]
"""
import struct, os, shutil
from _gamedir import game_dir

# BANNERLORD_GAME_DIR is the install path README.md requires and setup-dev-env.ps1 sets.
# The literal stays as the fallback so behaviour is unchanged where it is not set.
GAME = game_dir(r'E:\Steam\steamapps\common\Mount & Blade II Bannerlord')
DIR = GAME + r'\Modules\LOTRLOME_Armory\Assets\creature\chariot\animations'
PAIRS = [  # (target, donor) — donor is a tagged sibling of the same gait direction
    ('chariot_gait_walkfast_anm.tpac',     'chariot_walkfast_anm.tpac'),
    ('chariot_gait_walkbackfast_anm.tpac', 'chariot_walkbackfast_anm.tpac'),
]

def parse(buf):
    assert buf[:4] == b'TPAC', 'not a tpac'
    ver = struct.unpack('<I', buf[4:8])[0]
    off = 8 + 16
    num = struct.unpack('<I', buf[off:off + 4])[0]; off += 4
    off += 8  # two pads (0x1C content_size, 0x20)
    off += 16 + 16  # type_guid + item_guid
    if ver > 1: off += 4  # item_ver
    nl = struct.unpack('<i', buf[off:off + 4])[0]; off += 4
    name = buf[off:off + nl].decode(); off += nl
    meta_size_off = off
    ms = struct.unpack('<q', buf[off:off + 8])[0]; off += 8
    meta = buf[off:off + ms]; off += ms
    return dict(ver=ver, num=num, name=name, meta_size_off=meta_size_off, meta=meta, after_meta=off)

def main():
    for tname, dname in PAIRS:
        tpath, dpath = os.path.join(DIR, tname), os.path.join(DIR, dname)
        tbuf = open(tpath, 'rb').read(); dbuf = open(dpath, 'rb').read()
        tp, dp = parse(tbuf), parse(dbuf)
        # donor tail = everything in donor meta AFTER the make_walk_sound flag string
        dmws = dp['meta'].rfind(b'make_walk_sound')
        assert dmws >= 0, 'donor missing make_walk_sound'
        donor_tail = dp['meta'][dmws + len(b'make_walk_sound'):]
        assert b'quad_movement' in donor_tail, 'donor tail lacks quad_movement'
        # target: replace its (empty) ClipUsages tail with the donor tail
        tmws = tp['meta'].rfind(b'make_walk_sound')
        assert tmws >= 0, 'target missing make_walk_sound'
        if b'quad_movement' in tp['meta']:
            print('SKIP %s (already has quad_movement)' % tname); continue
        new_meta = tp['meta'][:tmws + len(b'make_walk_sound')] + donor_tail
        head = tbuf[:tp['meta_size_off']]
        after = tbuf[tp['after_meta']:]
        newbuf = head + struct.pack('<q', len(new_meta)) + new_meta + after
        newbuf = newbuf[:0x1C] + struct.pack('<I', len(newbuf) - 36) + newbuf[0x20:]
        # verify round-trip parse + flag presence
        chk = parse(newbuf)
        ok = (b'quad_movement' in chk['meta']) and (chk['name'] == tp['name'])
        bak = tpath + '.bak-untagged'
        if not os.path.exists(bak):
            shutil.copy(tpath, bak)
        open(tpath, 'wb').write(newbuf)
        print('PATCHED %-32s donor=%-26s %d->%d bytes  meta %d->%d  verify=%s'
              % (tname, dname, len(tbuf), len(newbuf), len(tp['meta']), len(new_meta), ok))

if __name__ == '__main__':
    main()
