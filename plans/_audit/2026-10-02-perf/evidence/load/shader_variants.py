"""Tabulate the shader variants an rgl log compiles in a time window (scratch, read-only).

Usage: shader_variants.py <rgl log> [<from hh:mm:ss> <to hh:mm:ss>]
Line shape: [hh:mm:ss.mmm] compile_shader: <source>, <entry>, <profile>, <n>, <a>, <b>, <c>.
Prints the count per (source, entry, profile), the distinct (a, b) pairs per source and entry, the
distinct b values overall, and which bits of b and a vary.
"""
import collections
import re
import sys

path = sys.argv[1]
lo = sys.argv[2] if len(sys.argv) > 2 else None
hi = sys.argv[3] if len(sys.argv) > 3 else None
rx = re.compile(r"^\[(\d\d:\d\d:\d\d)\.\d+\] compile_shader: \$BASE/Shaders/Sources/([^,]+), ([^,]+), ([^,]+), "
                r"(-?\d+), (-?\d+), (-?\d+), (-?\d+)\.")
rows = []
for ln in open(path, encoding="utf-8", errors="replace"):
    m = rx.match(ln)
    if not m:
        continue
    t = m.group(1)
    if lo and t < lo or hi and t > hi:
        continue
    rows.append((m.group(2), m.group(3), m.group(4), int(m.group(5)), int(m.group(6)) & 0xFFFFFFFF,
                 int(m.group(7)) & 0xFFFFFFFF, int(m.group(8))))
print(f"compiles: {len(rows)}")
by = collections.Counter((r[0], r[1], r[2]) for r in rows)
for k, v in sorted(by.items(), key=lambda kv: -kv[1]):
    print(f"  {v:5d}  {k[0]} {k[1]} {k[2]}")
bs = collections.Counter(r[5] for r in rows)
as_ = collections.Counter(r[4] for r in rows)
ns = collections.Counter(r[3] for r in rows)
cs = collections.Counter(r[6] for r in rows)
print(f"distinct a (field 5): {len(as_)}  b (field 6): {len(bs)}  n: {dict(ns)}  c: {dict(cs)}")
print("a values:", sorted(as_.items()))


def varying(values):
    allv = list(values)
    and_ = allv[0]
    or_ = 0
    for v in allv:
        and_ &= v
        or_ |= v
    return and_, or_, [i for i in range(32) if (or_ ^ and_) >> i & 1]


for name, vals in (("a", as_), ("b", bs)):
    and_, or_, bits = varying(vals)
    print(f"{name}: always-set bits 0x{and_:08x}, ever-set 0x{or_:08x}, varying bits {bits}")
# How many b values per (source, entry): if each material config yields one compile per pass, the number of
# distinct b per pass is the number of configs that pass needs.
per = collections.defaultdict(set)
for r in rows:
    per[(r[0], r[1])].add((r[4], r[5]))
for k in sorted(per):
    print(f"  distinct (a,b) for {k[0]} {k[1]}: {len(per[k])}")
# Bit frequency in b
freq = collections.Counter()
for v, c in bs.items():
    for i in range(32):
        if v >> i & 1:
            freq[i] += 1
print("b bit frequency over distinct values:", sorted(freq.items()))
