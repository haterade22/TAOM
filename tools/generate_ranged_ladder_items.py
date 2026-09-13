#!/usr/bin/env python3
"""Author the ranged ladder items: one `ladder_<line>_<bow|xbow>_<band>` per cell of the grid
in tools/ranged_ladders.json, each a clone of that line's own donor bow with only the id, the
name and `missile_speed` changed (#582).

WHY
---
An archer's reach is its launcher's missile_speed (tools/ranged_ladder.py). The grid gives
every (line, band) cell an exact speed, and no existing bow happens to sit on it, so every
cell is a generated item. Existing bows stay untouched for lords, the player and the shops.

WHAT IT WRITES (live Armory and, when present, the lotraom-assets mirror)
-------------------------------------------------------------------------
  LOTRLOME_items/<folder>/ranged_ladder.xml   one generated file per line folder
  Languages/loc_<folder>.xml                  one marker block of English name rows, so
                                              `translate_with_claude.py --module Armory
                                              --sync-ids` seeds the other eleven languages

The loc template is the one shared file touched: the block sits between markers just before
`</strings>`, and a `.bak-rangedladder` sidecar is taken once. A clone keeps the
donor's mesh, flags, damage, accuracy, usage and everything else; `is_merchandise` is forced to
`false` (130 near-duplicate bows must not flood the town shops; loot still drops them because
loot is the fallen troop's own kit). The name is `{=<id>}<donor name, its own numeral and
"- Starting" / "- Horse" suffix stripped> <band numeral I..V>`, so `/localize` picks the keys up.

RULES THIS TOOL FOLLOWS (tools/README.md "XML I/O convention")
---------------------------------------------------------------
Generated files are written whole, CRLF, no BOM (the starter kit's shape); every write is
re-parsed first; dry-run by default; idempotent (`--apply` twice writes nothing the second
time); `--verify` exits 1 on any drift in either tree; `--revert` removes exactly the generated
files. A NEW item file loads only at process launch: after `--apply`, restart Bannerlord fully
before believing anything. A folder the Armory's SubModule.xml does not register never loads,
so an unregistered `folder` in the spec is an error, not a warning.

USAGE
-----
    python tools/generate_ranged_ladder_items.py             # dry run: the plan
    python tools/generate_ranged_ladder_items.py --apply     # write to the Armory (+ mirror)
    python tools/generate_ranged_ladder_items.py --verify    # exit 1 if any file drifted
    python tools/generate_ranged_ladder_items.py --revert    # remove every generated file
"""
from __future__ import annotations

import argparse
import copy
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import OrderedDict
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_starter_kit as gsk  # noqa: E402  read_xml, checked_write, index_items, registered_item_folders, _serialize
import ranged_ladder as rl  # noqa: E402
import rebalance_troops as rb  # noqa: E402  DEFAULT_GAME_MODULES

DEFAULT_ASSET_REPO = Path(r"E:\repos\lotraom-assets") / "v1.4" / "LOTRLOME_Armory"
ITEMS_FILE_NAME = "ranged_ladder.xml"
EOL = "\r\n"
LOC_MARKER_START = "<!-- TAOM-RANGED-LADDER:START -->"
LOC_MARKER_END = "<!-- TAOM-RANGED-LADDER:END -->"
BACKUP_TAG = "rangedladder"
_LOC_BLOCK_RE = re.compile(r"[ \t]*" + re.escape(LOC_MARKER_START) + r".*?" + re.escape(LOC_MARKER_END) + r"[ \t]*\r?\n?", re.S)

_TAG_RE = re.compile(r"^\{=[^}]*\}")
_SUFFIX_RE = re.compile(r"\s*-?\s*(?:Starting|Horse|Starter)\s*$", re.I)
_NUMERAL_RE = re.compile(r"\s+(?:I|II|III|IV|V|VI|VII)\s*$")


class GeneratorError(Exception):
    """A condition the run must not paper over: an unregistered folder, a missing donor."""


# --------------------------------------------------------------------------- #
# Clones                                                                        #
# --------------------------------------------------------------------------- #
def ladder_name(donor_name: str | None, new_id: str, band: str) -> str:
    text = _TAG_RE.sub("", donor_name or new_id).strip()
    for _ in range(3):
        text = _SUFFIX_RE.sub("", text).strip()
        text = _NUMERAL_RE.sub("", text).strip()
    return "{=%s}%s %s" % (new_id, text, rl.BAND_NUMERAL[band])


def clone_launcher(donor: ET.Element, item: rl.LadderItem) -> ET.Element:
    """A verbatim copy with the identity, the name, the speed and the shop flag swapped."""
    out = copy.deepcopy(donor)
    out.set("id", item.id)
    out.set("name", ladder_name(donor.get("name"), item.id, item.band))
    out.set("is_merchandise", "false")
    changed = 0
    for weapon in out.iter("Weapon"):
        if weapon.get("weapon_class") == item.cls:
            weapon.set("missile_speed", str(item.speed))
            changed += 1
    if not changed:
        raise GeneratorError(f"{item.donor}: no <Weapon weapon_class=\"{item.cls}\"> to set missile_speed on")
    return out


def render_items_file(clones: list[tuple[rl.LadderItem, ET.Element]], folder: str, eol: str = EOL) -> str:
    donors = ", ".join(sorted({i.donor for i, _ in clones}))
    header = (
        f'<?xml version="1.0" encoding="utf-8"?>{eol}'
        f"<!--{eol}"
        f"  TAOM ranged range ladder for the {folder} folder. GENERATED by{eol}"
        f"  tools/generate_ranged_ladder_items.py from tools/ranged_ladders.json (do not hand-edit;{eol}"
        f"  re-run the generator). Each item is a clone of a line's donor bow with only id, name and{eol}"
        f"  missile_speed changed, is_merchandise=false. Donors: {donors}{eol}"
        f"-->{eol}"
        f"<Items>{eol}"
    )
    body = "".join(gsk._serialize(elem, "    ", eol, level=1) for _, elem in clones)
    return header + body + f"</Items>{eol}"


# --------------------------------------------------------------------------- #
# English loc rows (Languages/loc_<folder>.xml)                                 #
# --------------------------------------------------------------------------- #
def _loc_path(md: Path, folder: str) -> Path:
    return md / "Languages" / f"loc_{folder}.xml"


def _xml_attr(text: str) -> str:
    return text.replace("&", "&amp;").replace('"', "&quot;").replace("<", "&lt;").replace(">", "&gt;")


def render_loc_block(clones, eol: str, indent: str = "    ") -> str:
    rows = f"{indent}{LOC_MARKER_START}{eol}"
    for item, elem in clones:
        name = _TAG_RE.sub("", elem.get("name") or "")
        rows += f'{indent}<string id="{item.id}" text="{_xml_attr(name)}"/>{eol}'
    rows += f"{indent}{LOC_MARKER_END}{eol}"
    return rows


def apply_loc(text: str, clones) -> tuple[str, str]:
    """(text, inserted|updated|noop). The block sits just before `</strings>`."""
    eol = gsk.dominant_newline(text)
    wanted = render_loc_block(clones, eol)
    existing = _LOC_BLOCK_RE.search(text)
    if existing:
        if existing.group(0) == wanted:
            return text, "noop"
        return text[:existing.start()] + wanted + text[existing.end():], "updated"
    close = text.rfind("</strings>")
    if close == -1:
        raise GeneratorError("loc template has no </strings> close tag")
    line_start = text.rfind("\n", 0, close) + 1
    return text[:line_start] + wanted + text[line_start:], "inserted"


def revert_loc(text: str) -> tuple[str, bool]:
    new_text, count = _LOC_BLOCK_RE.subn("", text)
    return new_text, bool(count)


# --------------------------------------------------------------------------- #
# Plan                                                                          #
# --------------------------------------------------------------------------- #
def donor_files(game_modules: Path) -> list[Path]:
    """Every item file a donor can live in: the Armory (minus our own output) and vanilla."""
    files: list[Path] = []
    armory_md = game_modules / "LOTRLOME_Armory" / "ModuleData"
    files += sorted(p for p in armory_md.rglob("*.xml") if p.name != ITEMS_FILE_NAME)
    vanilla = game_modules / "SandBoxCore" / "ModuleData" / "items"
    if vanilla.exists():
        files += sorted(vanilla.glob("*.xml"))
    return files


def build_plan(spec: dict, game_modules: Path) -> "OrderedDict[str, list[tuple[rl.LadderItem, ET.Element]]]":
    """folder -> [(item, element)]. Raises on a folder the Armory does not register or a
    donor the index cannot see."""
    armory = game_modules / "LOTRLOME_Armory"
    submodule = armory / "SubModule.xml"
    if not submodule.exists():
        raise GeneratorError(f"no SubModule.xml at {submodule}; is --game-modules the Modules folder?")
    registered = gsk.registered_item_folders(gsk.read_xml(submodule)[0])
    failures: list[str] = []
    index = gsk.index_items(donor_files(game_modules), armory / "ModuleData" / "LOTRLOME_items", failures)
    for f in failures:
        print(f"WARNING: {f}")
    plan: "OrderedDict[str, list]" = OrderedDict()
    for item in rl.planned_items(spec):
        if item.folder not in registered:
            raise GeneratorError(
                f"line {item.line!r} writes to LOTRLOME_items/{item.folder}, which the Armory's "
                f"SubModule.xml does not register as an Items path; a file there never loads")
        rec = index.get(item.donor)
        if rec is None:
            raise GeneratorError(f"line {item.line!r}: donor {item.donor!r} is not defined in the Armory or vanilla items")
        plan.setdefault(item.folder, []).append((item, clone_launcher(rec.element, item)))
    return plan


def _items_path(md: Path, folder: str) -> Path:
    return md / "LOTRLOME_items" / folder / ITEMS_FILE_NAME


def apply_plan(plan, md: Path, write: bool) -> list[str]:
    log: list[str] = []
    for folder, clones in plan.items():
        path = _items_path(md, folder)
        text = render_items_file(clones, folder)
        if path.exists() and path.read_bytes() == text.encode("utf-8"):
            log.append(f"noop       {path} ({len(clones)} items)")
            continue
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            err = gsk.checked_write(path, text, False, "rangedladder", backup=False)
            if err:
                raise GeneratorError(err)
        log.append(f"{'wrote' if write else 'would write':10s} {path} ({len(clones)} items)")
    for folder, clones in plan.items():
        loc = _loc_path(md, folder)
        if not loc.exists():
            log.append(f"WARNING    no English loc template at {loc}; the {len(clones)} names stay unregistered there")
            continue
        text, had_bom = gsk.read_xml(loc)
        new_text, action = apply_loc(text, clones)
        if action != "noop" and write:
            err = gsk.checked_write(loc, new_text, had_bom, BACKUP_TAG, backup=True)
            if err:
                raise GeneratorError(err)
        log.append(f"{action if write or action == 'noop' else 'would ' + action:10s} {loc} ({len(clones)} name rows)")
    return log


def verify_plan(plan, md: Path) -> list[str]:
    """Drift: a missing file, a missing id, or a speed that is not the grid's."""
    drift: list[str] = []
    for folder, clones in plan.items():
        path = _items_path(md, folder)
        if not path.exists():
            drift.append(f"missing file {path}")
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as exc:
            drift.append(f"{path}: not well-formed ({exc})")
            continue
        on_disk = {}
        for node in root.iter("Item"):
            w = node.find("ItemComponent/Weapon")
            on_disk[node.get("id")] = w.get("missile_speed") if w is not None else None
        for item, _ in clones:
            got = on_disk.get(item.id)
            if got is None:
                drift.append(f"{path.name} ({folder}): item {item.id} missing")
            elif got != str(item.speed):
                drift.append(f"{path.name} ({folder}): {item.id} missile_speed {got}, grid says {item.speed}")
    for folder, clones in plan.items():
        loc = _loc_path(md, folder)
        if not loc.exists():
            continue  # reported as a warning at apply time; a template that never existed is not drift
        text = gsk.read_xml(loc)[0]
        block = _LOC_BLOCK_RE.search(text)
        body = block.group(0) if block else ""
        for item, _ in clones:
            if f'id="{item.id}"' not in body:
                drift.append(f"{loc.name} ({folder}): name row {item.id} missing")
    return drift


def revert(md: Path) -> list[str]:
    log: list[str] = []
    items_root = md / "LOTRLOME_items"
    if not items_root.exists():
        return log
    for path in sorted(items_root.glob(f"*/{ITEMS_FILE_NAME}")):
        path.unlink()
        log.append(f"removed    {path}")
    lang = md / "Languages"
    for loc in sorted(lang.glob("loc_*.xml")) if lang.exists() else []:
        text, had_bom = gsk.read_xml(loc)
        new_text, changed = revert_loc(text)
        if changed:
            err = gsk.checked_write(loc, new_text, had_bom, BACKUP_TAG, backup=True)
            if err:
                raise GeneratorError(err)
            log.append(f"stripped   {loc}")
    return log


# --------------------------------------------------------------------------- #
# Main                                                                          #
# --------------------------------------------------------------------------- #
def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES,
                    help=".../Mount & Blade II Bannerlord/Modules (the live Armory and vanilla donors)")
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO),
                    help="the lotraom-assets LOTRLOME_Armory mirror; skipped with a warning when absent")
    ap.add_argument("--spec", default=str(rl.DEFAULT_SPEC))
    ap.add_argument("--moduledata", default=str(rl.MODULEDATA_DIR),
                    help="TAOM ModuleData root, only to check the spec's files tokens against troops/")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the generated files")
    mode.add_argument("--verify", action="store_true", help="exit 1 if either tree drifted from the plan")
    mode.add_argument("--revert", action="store_true", help="remove every generated file from both trees")
    args = ap.parse_args(argv)

    game_modules = Path(args.game_modules)
    armory_md = game_modules / "LOTRLOME_Armory" / "ModuleData"
    if not armory_md.is_dir():
        print(f"ERROR: LOTRLOME_Armory not found under {game_modules}; pass --game-modules. Nothing was written.")
        return 2
    mirror_md = Path(args.asset_repo) / "ModuleData"
    trees = [("armory", armory_md)]
    if mirror_md.is_dir():
        trees.append(("mirror", mirror_md))
    else:
        print(f"WARNING: assets mirror not found at {args.asset_repo}; only the live Armory is touched")

    if args.revert:
        for label, md in trees:
            for line in revert(md):
                print(f"{label:7s} {line}")
        return 0

    try:
        spec = rl.load_spec(args.spec)
    except (OSError, ValueError) as exc:
        print(f"ERROR: cannot read spec {args.spec}: {exc}")
        return 2
    problems = rl.validate_spec(spec, cultures=rl.troop_file_cultures(args.moduledata) or None)
    if problems:
        print("ERROR: the spec contradicts itself; nothing was written:")
        for p in problems:
            print(f"  - {p}")
        return 2
    try:
        plan = build_plan(spec, game_modules)
    except GeneratorError as exc:
        print(f"ERROR: {exc}")
        return 2
    total = sum(len(v) for v in plan.values())

    if args.verify:
        drift = []
        for label, md in trees:
            drift += [f"{label}: {d}" for d in verify_plan(plan, md)]
        if drift:
            print(f"DRIFT: {len(drift)} problem(s) against {total} planned items:")
            for d in drift:
                print(f"  - {d}")
            return 1
        print(f"OK: all {total} ladder items present with their grid speeds in {len(trees)} tree(s)")
        return 0

    for label, md in trees:
        try:
            for line in apply_plan(plan, md, write=args.apply):
                print(f"{label:7s} {line}")
        except GeneratorError as exc:
            print(f"ERROR: {exc}")
            return 2
    print(f"{'Wrote' if args.apply else 'Would write'} {total} items into {len(plan)} folder(s)"
          + ("" if args.apply else " (dry run; pass --apply)"))
    if args.apply:
        print("A new item file loads only at process launch: restart Bannerlord fully before testing.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
