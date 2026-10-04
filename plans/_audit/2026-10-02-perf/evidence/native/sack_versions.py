"""Tally the format-version dword (bytes 4-7) of every compressed_shader_cache.sack under given roots (read-only)."""
import os
import struct
import sys
from collections import Counter, defaultdict

for root in sys.argv[1:]:
    tally = Counter()
    examples = defaultdict(list)
    for dirpath, _, files in os.walk(root):
        for f in files:
            if f != "compressed_shader_cache.sack":
                continue
            p = os.path.join(dirpath, f)
            try:
                with open(p, "rb") as fh:
                    head = fh.read(8)
                ver = struct.unpack("<I", head[4:8])[0] if len(head) >= 8 else None
            except OSError as e:
                ver = f"unreadable: {e}"
            key = f"0x{ver:04X}" if isinstance(ver, int) else str(ver)
            tally[key] += 1
            if len(examples[key]) < 3:
                examples[key].append(os.path.relpath(p, root))
    print(f"== {root}")
    for k, n in tally.most_common():
        print(f"  {k}: {n}  e.g. {'; '.join(examples[k])}")
