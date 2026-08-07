#!/usr/bin/env python3
"""Point the adult female dwarf's underwear slot at the mesh that actually ships (#403).

WHY
---
`LOTRLOME_Armory/ModuleData/skins.xml`, `<race id="dwarf">`, `<skin gender="1" name="woman">`:

    underwear_bottom_mesh="sk_dwarf_underwear_female"      <- does not exist
    underwear_bottom_mesh="sk_dwarf_underwear_female_a"    <- what the armory ships (8 occurrences)

The bare name occurs in the asset packages ONLY as a prefix of the `_a` form, never as a complete
resource name. That prefix relationship is why it survived so long: any substring search reports the
mesh present. Check with a trailing token boundary, or just run `tools/validate_mesh_refs.py`.

Two reporters hit this as a hard CTD whenever a female dwarf came into view in a settlement, battle
or tournament — but never in dialogue, because the facegen path does not draw underwear. It faults
natively (`0xC0000005` at `TaleWorlds.Native.dll+0x58232C`, faulting address `0x24C` = a 16-bit index
read at a null geometry base), so there is no managed exception and **no TAOM crash bundle** — which
is why it went unattributed. It was the only unresolved mesh reference among the 89 in the file, and
the adult female dwarf is the only dwarf skin with a non-empty `underwear_bottom_mesh` (males and
every child row are `""`), so it touched female dwarves and nothing else.

RE-RUN CONDITION
----------------
**Any LOTRLOME_Armory update.** The Armory is a dependency module; an update overwrites the live
`skins.xml` and silently reverts this edit. Nothing else re-applies it.

Verify afterwards with:  python tools/validate_mesh_refs.py --no-rgl-log     (exit 0 == clean)

BACKUPS
-------
`<file>.bak-dwarf-female-underwear`, deliberately NOT a `.xml` extension: Bannerlord globs `*.xml`
in registered ModuleData folders, so an `.xml` backup injects duplicate ids
(`.claude/rules/moduledata-validation.md`).

USAGE
-----
    python tools/oneoff/fix_dwarf_female_underwear_mesh.py            # dry run (default)
    python tools/oneoff/fix_dwarf_female_underwear_mesh.py --apply
    python tools/oneoff/fix_dwarf_female_underwear_mesh.py --revert   # restore from backups

Idempotent: a second --apply reports "already applied" and writes nothing.
"""

from __future__ import annotations

import argparse
import os
import shutil
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_GAME = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"

LIVE = os.path.join(DEFAULT_GAME, "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")
SNAPSHOT = os.path.join(REPO_ROOT, "docs", "reference", "lotrlome-armory-snapshot", "skins.xml")
BACKUP_SUFFIX = ".bak-dwarf-female-underwear"

# Anchored on the closing quote so the OLD token cannot match inside the NEW one — the same
# trailing-boundary discipline that the defect itself defeated.
OLD = b'underwear_bottom_mesh="sk_dwarf_underwear_female"'
NEW = b'underwear_bottom_mesh="sk_dwarf_underwear_female_a"'


def targets(game_dir: str):
    live = os.path.join(game_dir, "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml")
    return [("live", live), ("snapshot", SNAPSHOT)]


def patch_one(label: str, path: str, apply: bool) -> int:
    """Returns 0 on success/no-op, 1 on error."""
    if not os.path.isfile(path):
        print(f"  {label:9} ERROR: not found: {path}")
        return 1

    # Full binary round-trip (idiom B in tools/README.md "XML I/O convention"). Never a text-mode
    # read+write: that strips a BOM and normalises CRLF->LF, turning a one-attribute edit into a
    # whole-file rewrite. The live file is LF and the tracked snapshot is CRLF; both must survive.
    data = open(path, "rb").read()
    n_old, n_new = data.count(OLD), data.count(NEW)

    if n_old == 0 and n_new >= 1:
        print(f"  {label:9} already applied ({n_new} occurrence(s) of the fixed name) - no change")
        return 0
    if n_old == 0 and n_new == 0:
        print(f"  {label:9} ERROR: neither the old nor the new attribute is present. "
              f"Wrong file, or the Armory changed this row — inspect before forcing.")
        return 1
    if n_old > 1:
        print(f"  {label:9} ERROR: expected exactly 1 occurrence, found {n_old}. Refusing to guess.")
        return 1

    if not apply:
        print(f"  {label:9} would patch 1 occurrence  ({os.path.basename(path)})")
        return 0

    backup = path + BACKUP_SUFFIX
    if not os.path.exists(backup):
        shutil.copy2(path, backup)
        print(f"  {label:9} backup -> {os.path.basename(backup)}")
    else:
        print(f"  {label:9} backup already exists, left as-is (it is the true pre-change state)")

    open(path, "wb").write(data.replace(OLD, NEW, 1))
    print(f"  {label:9} patched 1 occurrence")
    return 0


def revert_one(label: str, path: str, apply: bool) -> int:
    backup = path + BACKUP_SUFFIX
    if not os.path.isfile(backup):
        print(f"  {label:9} no backup at {os.path.basename(backup)} — nothing to revert")
        return 0
    if not apply:
        print(f"  {label:9} would restore from {os.path.basename(backup)}")
        return 0
    shutil.copy2(backup, path)
    print(f"  {label:9} restored from {os.path.basename(backup)}")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write changes (default is a dry run)")
    ap.add_argument("--revert", action="store_true",
                    help=f"restore both files from their {BACKUP_SUFFIX} backups")
    ap.add_argument("--game", default=DEFAULT_GAME, help="Bannerlord install dir")
    args = ap.parse_args()

    mode = "REVERT" if args.revert else "APPLY"
    print(f"{mode} {'(dry run - pass --apply to write)' if not args.apply else ''}")

    rc = 0
    for label, path in targets(args.game):
        if args.revert:
            rc |= revert_one(label, path, args.apply)
        else:
            rc |= patch_one(label, path, args.apply)

    if rc == 0 and args.apply and not args.revert:
        print("\nVerify with:  python tools/validate_mesh_refs.py --no-rgl-log")
        print("An Armory update reverts this - re-run then.")
    return rc


if __name__ == "__main__":
    sys.exit(main())
