#!/usr/bin/env python3
"""Guard TAOM_Map's prefab folder against the engine's parallel-load queue cap.

The Modding Kit editor enqueues every <game_entity> across all loaded Prefabs
folders into a native queue hard-capped at 131,072 items (chunk 4096 x 32,
EXACT: cmp eax,0x20000 in the wEditor TaleWorlds.Native.dll queue push at RVA
0x7708F0). Crossing it asserts at startup; ignoring the assert corrupts the
queue and hangs the loader. RCA: docs/investigations/
editor-rglconcurrentqueue-assert-2026-07.md (2026-07-24: folder hit 132,378).

Exit 0 = under warn threshold, 1 = warn (>120,000), 2 = over cap / error.
"""

import argparse
import re
import sys
from pathlib import Path

CAP = 131_072  # EXACT (disassembly)
WARN = 120_000  # margin for other modules' prefabs + in-flight overhead

DEFAULT_DIR = (
    r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"
    r"\Modules\TAOM_Map\Prefabs"
)

ENTITY_RE = re.compile(rb"<game_entity[\s>]")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=DEFAULT_DIR, help="Prefabs folder to count")
    args = ap.parse_args()

    root = Path(args.dir)
    if not root.is_dir():
        print(f"ERROR: not a directory: {root}")
        return 2

    total = 0
    per_file = []
    for xml in sorted(root.glob("*.xml")):
        n = len(ENTITY_RE.findall(xml.read_bytes()))
        per_file.append((n, xml.name))
        total += n

    for n, name in sorted(per_file, reverse=True)[:10]:
        print(f"  {n:>7}  {name}")
    print(f"TOTAL: {total} entities in {len(per_file)} files (cap {CAP}, warn {WARN})")

    if total >= CAP:
        print("ERROR: over the engine queue cap — the editor WILL assert at startup.")
        return 2
    if total > WARN:
        print("WARN: within margin of the cap — park unused prefabs before adding more.")
        return 1
    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
