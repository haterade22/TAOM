"""Compare a module sack's shader_mapping.bin with the variants the game compiled (scratch, read-only).

Usage: sack_mapping_compare.py <shader_mapping.bin> <rgl log> <from hh:mm:ss> <to hh:mm:ss>

Record layout assumed from the bytes (28 bytes each; the file size divides by 28):
  0  u64  key or hash
  8  u32  material flags (low half)        12 u32  system flags
  16 u8   input layout, 17-18 two bytes, 19 u8 a tag byte ('t' or 'u' seen)
  20 u32  ?                                24 u32  ?
The game's compile line gives: input layout, system flags, material flags low, material flags high.
"""
import collections
import re
import struct
import sys

mapping, log, lo, hi = sys.argv[1:5]
data = open(mapping, "rb").read()
n = len(data) // 28
print(f"mapping: {len(data)} bytes, {n} records, remainder {len(data) % 28}")
recs = [struct.unpack_from("<QIIBBBBII", data, i * 28) for i in range(n)]
tags = collections.Counter(r[6] for r in recs)
print("tag byte values:", {chr(k) if 32 <= k < 127 else k: v for k, v in tags.most_common(8)})
layouts = collections.Counter(r[3] for r in recs)
print("layouts:", layouts.most_common(8))
f20 = collections.Counter(r[7] for r in recs)
f24 = collections.Counter(r[8] for r in recs)
print("field20 top:", f20.most_common(6), " field24 top:", f24.most_common(6))
mat = collections.Counter(r[1] for r in recs)
sysf = collections.Counter(r[2] for r in recs)
print(f"distinct material flags: {len(mat)}, distinct system flags: {len(sysf)}")
print("top material flags:", [(hex(k), v) for k, v in mat.most_common(12)])

rx = re.compile(r"^\[(\d\d:\d\d:\d\d)\.\d+\] compile_shader: \$BASE/Shaders/Sources/([^,]+), ([^,]+), ([^,]+), "
                r"(-?\d+), (-?\d+), (-?\d+), (-?\d+)\.")
game = []
for ln in open(log, encoding="utf-8", errors="replace"):
    m = rx.match(ln)
    if m and lo <= m.group(1) <= hi:
        game.append((m.group(2), m.group(3), int(m.group(5)), int(m.group(6)) & 0xFFFFFFFF,
                     int(m.group(7)) & 0xFFFFFFFF, int(m.group(8))))
print(f"game compiles: {len(game)}")
keys_ls = {(r[3], r[2], r[1]) for r in recs}          # layout, system, material
keys_sm = {(r[2], r[1]) for r in recs}                # system, material
mats = set(mat)
hit_ls = sum(1 for g in game if (g[2], g[3], g[4]) in keys_ls)
hit_sm = sum(1 for g in game if (g[3], g[4]) in keys_sm)
hit_m = sum(1 for g in game if g[4] in mats)
print(f"game keys found: layout+system+material {hit_ls}, system+material {hit_sm}, material alone {hit_m}")
game_mats = sorted({g[4] for g in game})
print("game material flags and whether the mapping has them:")
for gm in game_mats:
    near = sorted(mats, key=lambda x: bin(x ^ gm).count("1"))[:3]
    print(f"  0x{gm:08x} in mapping: {gm in mats}; nearest: " +
          ", ".join(f"0x{x:08x} (xor 0x{x ^ gm:08x})" for x in near))
