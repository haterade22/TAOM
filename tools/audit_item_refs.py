#!/usr/bin/env python3
"""
Audit every `id="Item.X"` reference in TAOM ModuleData XML against the actual
v1.4.5 item registry (vanilla + LOTRLOME_Armory + TAOM-owned).

Reports references that resolve to no known item. Each unresolved ref is a
silent-null at runtime → empty equipment slot in-game.

Usage:  python tools/audit_item_refs.py [--show-locations]
"""
import argparse
import re
import sys
from collections import defaultdict
from pathlib import Path

from _gamedir import ensure_exists, game_dir

ROOT = Path(__file__).resolve().parent.parent
TAOM_MODULEDATA = ROOT / "Main/_Module/ModuleData"

GAME_MODULES = Path(game_dir("E:/Steam/steamapps/common/Mount & Blade II Bannerlord")) / "Modules"

# Modules whose Item XMLs build the runtime registry. TAOM's own XMLs are
# included so TAOM-authored items count as valid.
ITEM_REGISTRY_ROOTS = [
    GAME_MODULES / "SandBoxCore" / "ModuleData",
    GAME_MODULES / "SandBox" / "ModuleData",
    GAME_MODULES / "Native" / "ModuleData",
    GAME_MODULES / "StoryMode" / "ModuleData",
    GAME_MODULES / "CustomBattle" / "ModuleData",
    GAME_MODULES / "LOTRLOME_Armory" / "ModuleData",
    GAME_MODULES / "ADOD_Beasts" / "ModuleData",
    GAME_MODULES / "NavalDLC" / "ModuleData",
    TAOM_MODULEDATA,
]

# Vanilla item ID patterns that may not appear in XML but exist in code-loaded
# defaults — skip these even if not found in the registry.
KNOWN_CODE_DEFAULTS = {
    "Item.None",  # explicit empty slot
}

ITEM_DEF_PATTERN = re.compile(r'<(?:Item|CraftedItem)\s+[^>]*?\bid="([^"]+)"')
ITEM_REF_PATTERN = re.compile(r'\bid="Item\.([A-Za-z0-9_-]+)"')


def collect_defined_ids(roots: list[Path]) -> set[str]:
    """Walk every XML under the given roots and collect <Item id="X"> + <CraftedItem id="X">."""
    defined = set()
    for root in roots:
        if not root.exists():
            print(f"  (skip missing root: {root})", file=sys.stderr)
            continue
        for xml in root.rglob("*.xml"):
            try:
                text = xml.read_text(encoding="utf-8", errors="ignore")
            except Exception as e:
                print(f"  WARN read failed {xml}: {e}", file=sys.stderr)
                continue
            for m in ITEM_DEF_PATTERN.finditer(text):
                defined.add(m.group(1))
    return defined


def collect_references(taom_root: Path) -> dict[str, list[tuple[Path, int]]]:
    """
    Walk every XML under TAOM ModuleData and collect `id="Item.X"` refs.
    Returns dict: item_id -> [(file_path, line_number), ...]
    """
    refs = defaultdict(list)
    for xml in taom_root.rglob("*.xml"):
        try:
            text = xml.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            continue
        for ln, line in enumerate(text.splitlines(), start=1):
            for m in ITEM_REF_PATTERN.finditer(line):
                refs[m.group(1)].append((xml, ln))
    return refs


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--show-locations", action="store_true",
                    help="Show every (file:line) for each broken ref, not just counts")
    ap.add_argument("--limit", type=int, default=40,
                    help="Max broken refs to print (default 40; --limit 0 = all)")
    args = ap.parse_args()

    # Nine of the ten registry roots hang off GAME_MODULES; the tenth is TAOM's
    # own ModuleData in the repo. With the root wrong they are each skipped with
    # a notice, only TAOM-authored items get registered, and every reference to
    # a vanilla or Armory item is then reported broken — 35,443 sites against a
    # true count of 0, at exit 0. The inverse of #404 point 4: not a clean result
    # that hides a wrong root, a catastrophic one invented by it.
    ensure_exists(GAME_MODULES, what="the Bannerlord Modules folder")

    print("Building v1.4.5 item registry from modules...", file=sys.stderr)
    defined = collect_defined_ids(ITEM_REGISTRY_ROOTS)
    print(f"  {len(defined)} item IDs defined across all modules", file=sys.stderr)

    print("Collecting TAOM ModuleData Item.X references...", file=sys.stderr)
    refs = collect_references(TAOM_MODULEDATA)
    print(f"  {len(refs)} distinct items referenced from TAOM", file=sys.stderr)

    broken = {item_id: locs for item_id, locs in refs.items()
              if item_id not in defined and f"Item.{item_id}" not in KNOWN_CODE_DEFAULTS}

    print()
    print(f"=== BROKEN REFERENCES: {len(broken)} distinct items ===")
    print()

    # Sort by number of usage sites (most-used first — likely to cause widespread breakage)
    sorted_broken = sorted(broken.items(), key=lambda kv: -len(kv[1]))
    limit = args.limit if args.limit > 0 else len(sorted_broken)
    for item_id, locs in sorted_broken[:limit]:
        n = len(locs)
        print(f"  Item.{item_id:50}  {n} ref{'s' if n != 1 else ''}")
        if args.show_locations:
            # Group by file
            by_file = defaultdict(list)
            for fp, ln in locs:
                by_file[fp.relative_to(ROOT)].append(ln)
            for fp, lns in sorted(by_file.items()):
                print(f"      {fp} : {lns[:8]}{'…' if len(lns) > 8 else ''}")
    if len(sorted_broken) > limit:
        print(f"\n  ...and {len(sorted_broken) - limit} more (use --limit 0 to see all)")

    print()
    print(f"Total broken refs: {sum(len(locs) for locs in broken.values())} sites across {len(broken)} item IDs")


if __name__ == "__main__":
    main()
