#!/usr/bin/env python3
"""Restat the Armory's own bows, crossbows and ammo from tools/ranged_ladders.json (#617).

WHY
---
The ladder items (`ladder_*`) carry every troop's launcher, but the Armory's own launchers are what
lords, wanderers, named companions and the town shops hand out, and they sat at 97 to 130 damage
with accuracy 100 (vanilla's lord bow is 95 / 98, its best soldier bow 64). Elven arrows added +5
and the Iron Hills and Isengard bolts +8, over vanilla's +4 and +5. `donor_stats` and `ammo_stats`
in the spec hold the target values; this tool writes them into the item XML. The generated ladder
items are cloned from these donors but set their own stats, so the order of the two tools does
not matter.

WHAT IT WRITES
--------------
Only the `thrust_damage` and `accuracy` attribute values inside the matching <Weapon> tag of each
listed <Item>, in the live LOTRLOME_Armory and, when present, the lotraom-assets v1.5 mirror.
Nothing else in the file moves: the text is edited in place (tools/README.md "XML I/O convention",
binary round-trip), re-parsed before it is written, and a `.bak-rangeddonor` sidecar is taken once
per file (never a `.xml` name: the item folders are globbed). An id defined in vanilla SandBoxCore,
defined twice in the Armory, or not at all is an error and nothing is written.

USAGE
-----
    python tools/restat_ranged_donors.py            # dry run: what would change
    python tools/restat_ranged_donors.py --apply    # write
    python tools/restat_ranged_donors.py --verify   # exit 1 if any value drifted
Item XML loads at process launch: restart Bannerlord fully after --apply.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ranged_ladder as rl  # noqa: E402
import rebalance_troops as rb  # noqa: E402  DEFAULT_GAME_MODULES

DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.5" / "LOTRLOME_Armory"
BACKUP_SUFFIX = ".bak-rangeddonor"
LAUNCHER_CLASSES = ("Bow", "Crossbow")
AMMO_CLASSES = ("Arrow", "Bolt")
_WEAPON_TAG_RE = re.compile(r"<Weapon\b[^>]*>", re.S)
_CLASS_RE = re.compile(r'\bweapon_class="([^"]*)"')


class RestatError(Exception):
    """A condition the run must not paper over: an unknown, duplicated or vanilla id, an item whose
    weapon lacks the attribute to set, or a file that would stop parsing."""


def targets(spec: dict) -> dict[str, tuple[tuple[str, ...], dict[str, int]]]:
    """id -> (weapon classes it must carry, {attribute: value})."""
    out: dict[str, tuple] = {}
    for iid, row in (spec.get("donor_stats") or {}).items():
        attrs = {}
        if "damage" in row:
            attrs["thrust_damage"] = int(row["damage"])
        if "accuracy" in row:
            attrs["accuracy"] = int(row["accuracy"])
        out[iid] = (LAUNCHER_CLASSES, attrs)
    for iid, dmg in (spec.get("ammo_stats") or {}).items():
        if iid in out:
            raise RestatError(f"{iid} is in both donor_stats and ammo_stats")
        out[iid] = (AMMO_CLASSES, {"thrust_damage": int(dmg)})
    return out


def _item_span(text: str, iid: str) -> list[tuple[int, int]]:
    """(start, end) of every <Item> element whose id attribute is exactly iid."""
    spans = []
    for m in re.finditer(r'<Item\b[^>]*?\bid="' + re.escape(iid) + r'"[^>]*?(/?)>', text, re.S):
        if m.group(1) == "/":
            spans.append((m.start(), m.end()))
            continue
        close = text.find("</Item>", m.end())
        if close == -1:
            raise RestatError(f"item {iid} has no closing </Item>")
        spans.append((m.start(), close + len("</Item>")))
    return spans


def _files(md: Path) -> list[Path]:
    root = md / "LOTRLOME_items"
    return sorted(p for p in root.rglob("*.xml")) if root.exists() else []


def locate(md: Path, ids) -> dict[str, list[Path]]:
    """id -> files defining it, over one tree's LOTRLOME_items."""
    where: dict[str, list[Path]] = {i: [] for i in ids}
    for path in _files(md):
        text = path.read_bytes().decode("utf-8")
        for iid in ids:
            if f'id="{iid}"' in text and _item_span(text, iid):
                where[iid].append(path)
    return where


def vanilla_ids(game_modules: Path, ids) -> set[str]:
    found = set()
    items = game_modules / "SandBoxCore" / "ModuleData" / "items"
    for path in sorted(items.glob("*.xml")) if items.exists() else []:
        text = path.read_bytes().decode("utf-8", errors="replace")
        for iid in ids:
            if f'id="{iid}"' in text and _item_span(text, iid):
                found.add(iid)
    return found


def current(text: str, iid: str, classes) -> list[dict[str, str]]:
    """The attributes of every <Weapon> of a listed class inside the item."""
    (start, end), = _item_span(text, iid)
    out = []
    for m in _WEAPON_TAG_RE.finditer(text, start, end):
        cls = _CLASS_RE.search(m.group(0))
        if cls and cls.group(1) in classes:
            out.append(dict(re.findall(r'\b([A-Za-z_]+)="([^"]*)"', m.group(0))))
    return out


def rewrite(text: str, iid: str, classes, attrs: dict[str, int]) -> tuple[str, list[str]]:
    """The text with the attributes set inside every matching <Weapon> tag of the item, and a
    change log. Only the digits of existing attributes move; a missing attribute is an error."""
    spans = _item_span(text, iid)
    if len(spans) != 1:
        raise RestatError(f"{iid} is defined {len(spans)} times in one file")
    start, end = spans[0]
    block = text[start:end]
    log: list[str] = []
    hits = 0

    def fix(m):
        nonlocal hits
        tag = m.group(0)
        cls = _CLASS_RE.search(tag)
        if not cls or cls.group(1) not in classes:
            return tag
        hits += 1
        for attr, value in attrs.items():
            am = re.search(r'\b' + attr + r'="([^"]*)"', tag)
            if am is None:
                raise RestatError(f"{iid}: its {cls.group(1)} weapon has no {attr} to set")
            if am.group(1) != str(value):
                log.append(f"{attr} {am.group(1)} -> {value}")
                tag = tag[:am.start(1)] + str(value) + tag[am.end(1):]
        return tag

    block = _WEAPON_TAG_RE.sub(fix, block)
    if not hits:
        raise RestatError(f"{iid} has no <Weapon weapon_class> among {', '.join(classes)}")
    return text[:start] + block + text[end:], log


def plan(md: Path, want: dict) -> tuple[dict[Path, str], list[str], list[str]]:
    """({file: new text}, change log, drift) for one tree. Raises on an id the tree defines twice."""
    where = locate(md, want)
    texts: dict[Path, str] = {}
    log: list[str] = []
    drift: list[str] = []
    for iid, (classes, attrs) in want.items():
        files = where[iid]
        if len(files) > 1:
            raise RestatError(f"{iid} is defined in {len(files)} files under {md}: "
                              f"{', '.join(p.name for p in files)} (the engine keeps one, silently)")
        if not files:
            continue
        path = files[0]
        text = texts.get(path) or path.read_bytes().decode("utf-8")
        new, changes = rewrite(text, iid, classes, attrs)
        if changes:
            texts[path] = new
            log.append(f"{iid} ({path.name}): " + ", ".join(changes))
            drift.append(f"{iid}: " + ", ".join(changes))
    return texts, log, drift


def write(texts: dict[Path, str]) -> None:
    for path, text in texts.items():
        try:
            ET.fromstring(text.encode("utf-8"))
        except ET.ParseError as exc:
            raise RestatError(f"{path} would no longer parse, nothing written to it: {exc}") from None
    for path, text in texts.items():
        backup = path.with_name(path.name + BACKUP_SUFFIX)
        if not backup.exists():
            backup.write_bytes(path.read_bytes())
        path.write_bytes(text.encode("utf-8"))


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES,
                    help=".../Mount & Blade II Bannerlord/Modules (the live Armory and vanilla items)")
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO),
                    help="the lotraom-assets LOTRLOME_Armory mirror; skipped with a warning when absent")
    ap.add_argument("--spec", default=str(rl.DEFAULT_SPEC))
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the values")
    mode.add_argument("--verify", action="store_true", help="exit 1 if either tree drifted from the spec")
    args = ap.parse_args(argv)

    game_modules = Path(args.game_modules)
    armory_md = game_modules / "LOTRLOME_Armory" / "ModuleData"
    if not armory_md.is_dir():
        print(f"ERROR: LOTRLOME_Armory not found under {game_modules}; pass --game-modules. Nothing was written.")
        return 2
    try:
        spec = rl.load_spec(args.spec)
        want = targets(spec)
    except (OSError, ValueError, RestatError) as exc:
        print(f"ERROR: cannot read the donor and ammo tables from {args.spec}: {exc}")
        return 2
    if not want:
        print(f"ERROR: {args.spec} lists no donor_stats or ammo_stats; nothing to restat.")
        return 2
    vanilla = vanilla_ids(game_modules, want)
    if vanilla:
        print(f"ERROR: {', '.join(sorted(vanilla))} {'is' if len(vanilla) == 1 else 'are'} vanilla SandBoxCore "
              "items; restat those through an XSLT, never in place. Nothing was written.")
        return 2
    trees = [("armory", armory_md)]
    mirror_md = Path(args.asset_repo) / "ModuleData"
    if mirror_md.is_dir():
        trees.append(("mirror", mirror_md))
    else:
        print(f"WARNING: assets mirror not found at {args.asset_repo}; only the live Armory is touched")

    plans = []
    try:
        for label, md in trees:
            where = locate(md, want)
            unknown = sorted(i for i, f in where.items() if not f)
            if unknown and label == "armory":
                print(f"ERROR: the live Armory defines none of: {', '.join(unknown)}. Nothing was written.")
                return 2
            for i in unknown:
                print(f"WARNING: {label} does not define {i}")
            plans.append((label, md, *plan(md, want)))
    except RestatError as exc:
        print(f"ERROR: {exc}. Nothing was written.")
        return 2

    if args.verify:
        drift = [f"{label}: {d}" for label, _md, _t, _log, dr in plans for d in dr]
        if drift:
            print(f"DRIFT: {len(drift)} item(s) differ from {args.spec}:")
            for d in drift:
                print(f"  - {d}")
            return 1
        print(f"OK: all {len(want)} listed items carry their values in {len(trees)} tree(s)")
        return 0

    for label, _md, texts, log, _dr in plans:
        for line in log:
            print(f"{label:7s} {'set' if args.apply else 'would set'} {line}")
        if args.apply and texts:
            try:
                write(texts)
            except RestatError as exc:
                print(f"ERROR: {exc}")
                return 2
    changed = sum(len(log) for _l, _m, _t, log, _d in plans)
    if not changed:
        print(f"Nothing to change: all {len(want)} listed items already carry their values.")
    elif args.apply:
        print(f"Wrote {changed} item(s). Item XML loads at process launch: restart Bannerlord fully before testing.")
    else:
        print(f"Would change {changed} item(s) (dry run; pass --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
