#!/usr/bin/env python3
"""Guard the Prefabs folders of every installed module against the engine's parallel-load queue cap.

The Modding Kit editor enqueues every <game_entity> across all loaded Prefabs
folders into a native queue hard-capped at 131,072 items (chunk 4096 x 32,
EXACT: cmp eax,0x20000 in the wEditor TaleWorlds.Native.dll queue push at RVA
0x7708F0). Crossing it asserts at startup; ignoring the assert corrupts the
queue and hangs the loader. RCA: docs/investigations/
editor-rglconcurrentqueue-assert-2026-07.md (2026-07-24: folder hit 132,378).

The cap asserts in the Modding Kit (wEditor) only, so this is a Kit hazard, not a
player one. The queue is global, so the count sums every module: TAOM_Map alone
can read OK while the sum is at the cap. NavalDLC decides the margin, because it
is in the sum only on a machine that has it installed.

Without --dir the tool sums `*/Prefabs/*.xml` under the Modules folder, which comes
from --modules, else $BANNERLORD_GAME_MODULES, else $BANNERLORD_GAME_DIR, else the
desktop path. --dir counts one folder, file by file.

Exit 0 = under warn threshold, 1 = warn (>120,000), 2 = over cap / error.
"""

import argparse
import re
import sys
from pathlib import Path

from _gamedir import game_modules

CAP = 131_072  # EXACT (disassembly)
WARN = 120_000  # margin for other modules' prefabs + in-flight overhead

ENTITY_RE = re.compile(rb"<game_entity[\s>]")


def verdict(total: int, cap: int, warn: int) -> tuple:
    """(exit code, message) for a total against the cap and the warn line."""
    if total >= cap:
        return 2, "ERROR: over the engine queue cap, the editor WILL assert at startup."
    if total > warn:
        return 1, "WARN: within margin of the cap, park unused prefabs before adding more."
    return 0, "OK"


def _count(path: Path) -> int:
    return len(ENTITY_RE.findall(path.read_bytes()))


def _single_folder(root: Path) -> tuple:
    per_file = [(_count(xml), xml.name) for xml in sorted(root.glob("*.xml"))]
    for n, name in sorted(per_file, reverse=True)[:10]:
        print(f"  {n:>7}  {name}")
    return sum(n for n, _ in per_file), f"{len(per_file)} files"


def _all_modules(modules: Path) -> tuple:
    per_module = []
    for prefabs in sorted(p for p in modules.glob("*/Prefabs") if p.is_dir()):
        per_module.append((sum(_count(xml) for xml in prefabs.glob("*.xml")), prefabs.parent.name))
    for n, name in sorted(per_module, reverse=True):
        print(f"  {n:>7}  {name}")
    return sum(n for n, _ in per_module), f"{len(per_module)} modules"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--dir", default=None, help="count this one Prefabs folder instead of summing the modules")
    ap.add_argument("--all-modules", action="store_true", help="sum every module's Prefabs (the default without --dir)")
    ap.add_argument("--modules", default=None, help="the Modules folder (default: found from the install)")
    args = ap.parse_args(argv)

    if args.dir and not args.all_modules:
        root = Path(args.dir)
        counter = _single_folder
    else:
        root = Path(args.modules) if args.modules else game_modules(
            r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
        counter = _all_modules
    if not root.is_dir():
        print(f"ERROR: not a directory: {root}")
        return 2

    total, scope = counter(root)
    print(f"TOTAL: {total} entities in {scope} (cap {CAP}, warn {WARN})")
    code, message = verdict(total, CAP, WARN)
    print(message)
    return code


if __name__ == "__main__":
    sys.exit(main())
