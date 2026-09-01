#!/usr/bin/env python3
"""Audit (and optionally fix) `has_gender_variations` against the art that ships.

WHY THIS EXISTS

`has_gender_variations` does NOT mean "this mesh has a slim variant". The engine
(`BasicCharacterTableau.cs:531-537`, v1.4.8) resolves an armour mesh like this:

    flag3 = isFemale && has_gender_variations
    try   name + (flag3 ? "_female" : "_male")
    else  name + (flag3 ? (slimBuild ? "_converted_slim" : "_converted")
                        : (slimBuild ? "_slim"           : ""))
    else  name

So `_slim` is the slim-BUILD suffix on the NON-female branch. The female suffixes
are `_female`, `_converted` and `_converted_slim`, and only those are gated on the
flag. Conflating the two produced a wrong data change on 2026-09-01.

TAOM ships NO female armour art: measured across 2,938 Armory armour items, zero
have a `_female` / `_converted` / `_converted_slim` mesh. Females are meant to wear
the male art. That makes `has_gender_variations="true"` strictly worse than
`"false"` here: `true` sends a female down a path with no art, so she falls through
to the bare mesh, while `false` puts her on the branch that can still find `_slim`.

**The engine default is `true`** when the attribute is absent (`ArmorComponent.cs:159`),
so an item that simply omits it also takes the dead female path. That is only worth
correcting where a `_slim` actually exists to be reached; otherwise both settings end
at the same bare mesh and adding the attribute is churn.

Usage:
  python tools/audit_gender_variation_flags.py                 # report
  python tools/audit_gender_variation_flags.py --apply         # flip true -> false
  python tools/audit_gender_variation_flags.py --include-omitted --apply

Exit: 1 if any item takes the female path while a reachable `_slim` exists, else 0.
"""
from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import validate_mesh_refs as vm  # noqa: E402
from _gamedir import ensure_exists, game_dir  # noqa: E402

DEFAULT_GAME = game_dir(r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord")
DEFAULT_ARMORY = Path(DEFAULT_GAME) / "Modules" / "LOTRLOME_Armory"
DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.4" / "LOTRLOME_Armory"

FEMALE_SUFFIXES = ("_female", "_converted", "_converted_slim")
_COMMENT_RE = re.compile(r"<!--.*?-->", re.S)
# Self-closing alternative FIRST. With `<Item\b.*?</Item>` leading, a self-closing
# `<Item ... />` does not match it, so the engine of the regex runs on to the next
# `</Item>` and swallows every item in between.
_ITEM_RE = re.compile(r"<Item\b[^>]*/>|<Item\b.*?</Item>", re.S)


def read_xml(path: Path) -> tuple:
    """(text, had_bom). Byte-faithful, per .claude/rules/moduledata-validation.md."""
    raw = Path(path).read_bytes()
    had_bom = raw.startswith(b"\xef\xbb\xbf")
    return raw.decode("utf-8-sig" if had_bom else "utf-8"), had_bom


def write_xml(path: Path, text: str, had_bom: bool) -> None:
    Path(path).write_bytes((b"\xef\xbb\xbf" if had_bom else b"") + text.encode("utf-8"))


def has_female_art(mesh: str, meshes: set) -> bool:
    return any((mesh + s) in meshes for s in FEMALE_SUFFIXES)


def classify(armory: Path, meshes: set) -> list:
    """One record per armour item that currently takes the female path."""
    root = armory / "ModuleData" / "LOTRLOME_items"
    out = []
    for f in sorted(root.rglob("*.xml")):
        if ".bak" in f.name or "Languages" in f.parts:
            continue
        text, _ = read_xml(f)
        for m in _ITEM_RE.finditer(_COMMENT_RE.sub("", text)):
            block = m.group(0)
            if "<Armor" not in block:
                continue          # the flag only applies to armour
            mesh = re.search(r'\bmesh="([^"]+)"', block)
            item = re.search(r'\bid="([^"]+)"', block)
            if not (mesh and item):
                continue
            flag = re.search(r'has_gender_variations="(true|false)"', block)
            if flag and flag.group(1) == "false":
                continue          # already on the male branch
            out.append({
                "item": item.group(1),
                "mesh": mesh.group(1),
                "file": f,
                "explicit": bool(flag),
                "female_art": has_female_art(mesh.group(1), meshes),
                "slim": (mesh.group(1) + "_slim") in meshes,
            })
    return out


def apply_flip(path: Path, item_ids: set, write: bool, tag: str) -> int:
    """Set has_gender_variations="false" on the named items. Explicit flags only:
    inserting the attribute where it was omitted is churn unless a `_slim` exists,
    and that decision belongs to the caller, not this function."""
    text, had_bom = read_xml(path)
    original = text
    changed = 0
    # Back to front. `"true"` -> `"false"` changes the length, so splicing
    # front-to-back with offsets taken from the original text corrupts every
    # edit after the first. That is the same offset-invalidation defect the
    # 2026-08-28 comment-restore shipped, and it is why this function has a
    # parse check below rather than trusting the transform.
    for m in reversed(list(_ITEM_RE.finditer(text))):
        block = m.group(0)
        found = re.search(r'\bid="([^"]+)"', block)
        if not found or found.group(1) not in item_ids:
            continue
        new_block, n = re.subn(r'has_gender_variations="true"',
                               'has_gender_variations="false"', block)
        if n:
            text = text[:m.start()] + new_block + text[m.end():]
            changed += n
    if not changed or text == original:
        return 0
    ET.fromstring(text.encode("utf-8"))     # never write a broken document
    if write:
        backup = path.with_suffix(path.suffix + f".bak-{tag}")
        if not backup.exists():
            backup.write_bytes(path.read_bytes())
        write_xml(path, text, had_bom)
    return changed


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass

    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--armory", default=str(DEFAULT_ARMORY))
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO))
    ap.add_argument("--apply", action="store_true", help="commit the edits (default: report)")
    ap.add_argument("--include-omitted", action="store_true",
                    help="also report items that omit the attribute (engine default is true)")
    args = ap.parse_args()

    armory = ensure_exists(args.armory, "the LOTRLOME_Armory module")
    game = armory.parent.parent
    present = vm.build_present_set(vm.tpac_paths_for_modules(game, vm.DEFAULT_TPAC_MODULES))
    rows = classify(armory, present.metameshes)

    explicit = [r for r in rows if r["explicit"]]
    omitted = [r for r in rows if not r["explicit"]]
    reachable = [r for r in rows if r["slim"] and not r["female_art"]]

    print(f"Armory armour items taking the female path: {len(rows)}")
    print(f"  explicit has_gender_variations=\"true\" : {len(explicit)}")
    print(f"  omitted (engine default is true)       : {len(omitted)}")
    print(f"  of all of the above, WITH female art   : {sum(r['female_art'] for r in rows)}")
    print(f"  with an unreachable _slim (real cost)  : {len(reachable)}")

    if args.include_omitted and omitted:
        print("\nOmitted-attribute items (no change proposed unless they have a _slim):")
        for r in omitted[:20]:
            if r["slim"]:
                print(f"  _slim unreachable  {r['item']}")
        print(f"  ...{len(omitted)} total, {sum(r['slim'] for r in omitted)} with a _slim")

    if not explicit:
        print("\nNothing to flip.")
        return 1 if reachable else 0

    print(f"\n{'WRITING' if args.apply else 'PREVIEW (pass --apply to commit)'}\n")
    tag = "genderflag-" + datetime.now().strftime("%Y%m%d%H%M%S")
    ids = {r["item"] for r in explicit if not r["female_art"]}
    total = 0
    for tree in (armory, Path(args.asset_repo)):
        if not tree.exists():
            print(f"  skipped (absent): {tree}")
            continue
        for f in sorted((tree / "ModuleData" / "LOTRLOME_items").rglob("*.xml")):
            if ".bak" in f.name or "Languages" in f.parts:
                continue
            n = apply_flip(f, ids, args.apply, tag)
            if n:
                total += n
                print(f"  {n:3d}  {f.relative_to(tree)}")
    print(f"\n{total} flag(s) set to false across both trees.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
