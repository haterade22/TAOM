#!/usr/bin/env python3
"""Hold in a repo spec which Armory spears can be couched and braced, and re-apply it.

WHY THIS EXISTS

A crafted weapon gets one usage per WeaponDescription whose AvailablePieces list holds EVERY piece the
item uses (Crafting.GenerateCraftedItem, Crafting.cs:566-608). The couch usage is the description
TwoHandedPolearm_Couchable (item_usage_features="polearm:couch", usage set polearm_couch, flag
requires_mount) and the brace usage is TwoHandedPolearm_Bracing (polearm:bracing, flags
requires_no_mount and requires_no_shield). LOTRLOME_Armory's weapon_descriptions.xslt lists its own
pieces in each description's template and then copies Native's list (xsl:apply-templates), so a spear
whose blade the Armory lists only under OneHandedPolearm and TwoHandedPolearm can be thrust and swung
but never couched or braced.

LOTRLOME_Armory is not in this repo, and a reinstall, a sync from the mirror or an art drop puts the
old file back. The spec, tools/couch_brace_polearms.json, names the items and the descriptions; the
pieces are read from the items' own <CraftedItem> definitions, so a re-modelled spear moves the
registration with it. Only pieces a description does not already list are written, in one marker region
per description (<!-- TAOM-COUCH-BRACE:BEGIN --> to END), through _marker_region.replace_region, so the
rest of the file stays as it was and a damaged region (a lone marker, two regions) is refused.

    python tools/register_couch_brace_polearms.py                          # dry run: the plan and its effects
    python tools/register_couch_brace_polearms.py --apply --backup-tag T   # write; one .bak-T copy, first write only
    python tools/register_couch_brace_polearms.py --check                  # exit 1 on drift, 2 if it cannot run
    python tools/register_couch_brace_polearms.py --revert                 # dry run: remove the regions
    python tools/register_couch_brace_polearms.py --revert --apply --backup-tag T

--armory-path names a LOTRLOME_Armory folder (tests use a temp one); --spec names another spec file.

The dry run also names every item that gains a usage: all items built from the same pieces get it, not
only the spec's. It lists the items that share a registered piece but still do not fit.

The edit is byte-faithful (tools/README.md XML I/O convention): BOM kept, the block takes the file's
majority line ending, only the marker region changes, and the result is parsed before anything is
written (on every path, --check included). A full game RESTART is needed: weapon descriptions load at
process launch. --check cannot see a region that sits in a template the spec no longer names, a
duplicated line outside the region, or a region made redundant because the Armory lists the pieces itself.

Exit: 0 clean; 1 drift, an item the Armory no longer defines, a piece with no definition, a stylesheet
whose anchor moved or whose marker region is damaged, a file that does not decode or parse, or an
unusable spec; 2 the Armory is not installed (a check that cannot run is not a pass; tools/data_gates.py
reads 2 as NOT RUN); 3 a crash (the traceback is printed; data_gates.py reads 3 as a crash, not a finding).
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402
from _marker_region import replace_region  # noqa: E402
from register_one_handed_polearms import CRAFTED_ITEM_RE  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
DEFAULT_SPEC = REPO / "tools" / "couch_brace_polearms.json"
MARKER = "TAOM-COUCH-BRACE"
MARKER_BEGIN = f"<!-- {MARKER}:BEGIN -->"
MARKER_END = f"<!-- {MARKER}:END -->"
TAG = re.compile(r"[A-Za-z0-9_-]+")
COMMENT_RE = re.compile(r"<!--.*?-->", re.S)
PIECE_RE = re.compile(r'<AvailablePiece\s+id="([^"]+)"')
CLOSE = "</AvailablePieces>"


class SpecError(Exception):
    pass


def _text(value) -> bool:
    return isinstance(value, str) and bool(value.strip())


def load_spec(path) -> dict:
    """The spec: {"descriptions": [ids], "items": [{"id", "reason"}]}. Any other shape is a SpecError."""
    try:
        spec = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise SpecError(str(exc))
    if not isinstance(spec, dict):
        raise SpecError("the spec must be a JSON object")
    descriptions, items = spec.get("descriptions"), spec.get("items")
    if not isinstance(descriptions, list) or not descriptions or not all(_text(d) for d in descriptions):
        raise SpecError('"descriptions" must be a non-empty list of description ids')
    if not isinstance(items, list) or not items:
        raise SpecError('"items" must be a non-empty list')
    for item in items:
        if not isinstance(item, dict) or not _text(item.get("id")) or not _text(item.get("reason")):
            raise SpecError('every item needs a text "id" and a non-blank "reason"')
    return spec


def _read(path: Path) -> str:
    """Strict UTF-8: an invalid byte outside the block must be refused, not rewritten. The BOM, if any,
    stays as a leading U+FEFF character and is written back as bytes by encode("utf-8")."""
    return path.read_bytes().decode("utf-8")


def scan_items(items_dir: Path) -> dict:
    """{item id: [piece ids in the order the item lists them]} over every live <CraftedItem> under
    items_dir. A commented-out definition is not live (the Armory retires items that way)."""
    out: dict = {}
    for path in sorted(items_dir.rglob("*.xml")):
        for match in CRAFTED_ITEM_RE.finditer(COMMENT_RE.sub("", _read(path))):
            block = match.group(0)
            found = re.search(r'\bid="([^"]+)"', block)
            if found:
                out.setdefault(found.group(1), re.findall(r'<Piece\s+id="([^"]+)"', block))
    return out


def defined_pieces(data_dir: Path) -> set:
    out: set = set()
    for path in sorted(data_dir.glob("*crafting_pieces*.xml")):
        out |= set(re.findall(r'<CraftingPiece\b[^>]*\bid="([^"]+)"', COMMENT_RE.sub("", _read(path))))
    return out


def template_re(description: str) -> re.Pattern:
    return re.compile(
        r"(<xsl:template\s+match=\"WeaponDescription\[@id='%s'\]/AvailablePieces\">)(.*?)(</xsl:template>)"
        % re.escape(description), re.S)


def listed(text: str) -> set:
    """The AvailablePiece ids in text, comments ignored."""
    return set(PIECE_RE.findall(COMMENT_RE.sub("", text)))


def block_text(pieces: list, indent: str, eol: str) -> str:
    lines = [f"{indent}{MARKER_BEGIN}"] + [f'{indent}<AvailablePiece id="{p}"/>' for p in pieces]
    lines.append(f"{indent}{MARKER_END}")
    return eol.join(lines) + eol


def _region(body: str, block: str, description: str) -> str:
    """body with its one marker region replaced by block ("" removes it), or block put before the close
    tag's own line. A damaged region raises SpecError. The close line is passed with its indent so the
    block lands at a line start, in the file's own layout."""
    close = body.rfind(CLOSE)
    if close == -1:
        raise SpecError(f"the {description} template has no {CLOSE} close tag")
    line = body[body.rfind("\n", 0, close) + 1:close + len(CLOSE)]
    try:
        return replace_region(body, MARKER, block, line)
    except RuntimeError as exc:
        raise SpecError(f"{exc}; repair the {description} template by hand or from its .bak copy")


def plan_description(src: str, description: str, wanted: list, eol: str):
    """(new src, pieces registered by the block, pieces listed after, pieces listed before). Raises
    SpecError when the stylesheet has no template for the description or its region is damaged."""
    match = template_re(description).search(src)
    if not match:
        raise SpecError(f"no <xsl:template> for {description}/AvailablePieces: the spec names a description "
                        "this stylesheet does not override (a typo?), or the Armory's weapon_descriptions.xslt "
                        "was restructured and the anchor needs re-deriving")
    head, body, tail = match.group(1), match.group(2), match.group(3)
    outside = _region(body, "", description)
    before = listed(body)
    missing = [p for p in wanted if p not in listed(outside)]
    if missing:
        indent_match = re.search(r"\n([ \t]*)<AvailablePiece\b", outside)
        new_body = _region(body, block_text(missing, indent_match.group(1) if indent_match else "\t\t\t", eol),
                           description)
    else:
        new_body = outside
    new_src = src[:match.start()] + head + new_body + tail + src[match.end():]
    return new_src, missing, listed(outside) | set(missing), before


def fits(pieces: list, available: set) -> bool:
    return bool(pieces) and all(p in available for p in pieces)


def _run(argv=None) -> int:
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")
        except (AttributeError, ValueError):
            pass
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true", help="write the changes (needs --backup-tag)")
    mode.add_argument("--check", action="store_true", help="exit 1 on drift, 2 if it cannot run")
    ap.add_argument("--revert", action="store_true", help="remove the marker regions (a dry run unless --apply)")
    ap.add_argument("--backup-tag", help="suffix of the one-time backup: <file>.bak-<tag> (not .xslt or .xml)")
    ap.add_argument("--armory-path", help="the LOTRLOME_Armory folder (default: the installed module)")
    ap.add_argument("--spec", default=str(DEFAULT_SPEC), help="the spec file (default: tools/couch_brace_polearms.json)")
    args = ap.parse_args(argv)
    if args.revert and args.check:
        ap.error("--revert cannot be combined with --check")
    if args.apply and not (args.backup_tag and TAG.fullmatch(args.backup_tag)):
        ap.error("--apply needs --backup-tag made of letters, digits, - and _ (the backup must not end in .xslt)")

    armory = Path(args.armory_path) if args.armory_path else game_modules(DEFAULT_GAME_ROOT) / "LOTRLOME_Armory"
    data = armory / "ModuleData"
    xslt = data / "weapon_descriptions.xslt"
    if not xslt.is_file():
        print(f"ERROR: SKIPPED, LOTRLOME_Armory is not installed at {armory}; nothing was checked")
        return 2
    try:
        spec = load_spec(args.spec)
    except SpecError as exc:
        print(f"ERROR: unusable spec: {exc}")
        return 1

    try:
        items = scan_items(data / "LOTRLOME_items")
        defined = defined_pieces(data)
        src = _read(xslt)
    except UnicodeDecodeError as exc:
        print(f"ERROR: a file is not valid UTF-8 ({exc}); nothing was written")
        return 1
    problems, wanted = [], []
    for entry in spec["items"]:
        pieces = items.get(entry["id"])
        if pieces is None:
            problems.append(f"MISSING {entry['id']} is not defined under LOTRLOME_items (spec reason: {entry['reason']})")
            continue
        if not pieces:
            problems.append(f"ERROR {entry['id']} has no <Piece> rows, so there is nothing to register")
            continue
        print(f"{entry['id']} -> {', '.join(pieces)}  [{entry['reason']}]")
        wanted += [p for p in pieces if p not in wanted]
        problems += [f"ERROR piece {p} of {entry['id']} has no <CraftingPiece> definition" for p in pieces if p not in defined]
    if problems:
        print("\n".join(problems))
        return 1

    crlf = src.count("\r\n")
    eol = "\r\n" if crlf > src.count("\n") - crlf else "\n"
    new_src, drift = src, 0
    try:
        for description in spec["descriptions"]:
            if args.revert:
                match = template_re(description).search(new_src)
                if not match:
                    raise SpecError(f"no <xsl:template> for {description}/AvailablePieces")
                body = _region(match.group(2), "", description)
                new_src = new_src[:match.start(2)] + body + new_src[match.end(2):]
                continue
            new_src, missing, after, before = plan_description(new_src, description, wanted, eol)
            lacking = [p for p in wanted if p not in before]
            if lacking:
                drift += 1
                print(f"DRIFT {description} lacks {', '.join(lacking)}")
            if args.check:
                continue
            gain = sorted(i for i, p in items.items() if fits(p, after) and not fits(p, before))
            near = sorted(i for i, p in items.items() if not fits(p, after) and (set(p) & set(missing)))
            if lacking:
                print(f"{description}: register {len(missing)} piece(s): {', '.join(missing)}")
                print(f"    items that gain it: {', '.join(gain) or 'none'}")
                print(f"    items that share a registered piece but still do not fit: {', '.join(near) or 'none'}")
    except SpecError as exc:
        print(f"ERROR: {exc}")
        return 1
    try:
        ET.fromstring(new_src.encode("utf-8"))   # a leading BOM is accepted in bytes
    except ET.ParseError as exc:
        print(f"ERROR: the stylesheet would not parse ({exc}); nothing was written")
        return 1

    if args.check:
        print(f"check: {drift} description(s) drift from the spec" if drift else "check: all match")
        return 1 if drift else 0
    if new_src == src:
        print("Nothing to revert: no marker region is present." if args.revert
              else "Already registered and up to date: nothing to do.")
        return 1 if args.revert else 0
    if not args.apply:
        print("DRY RUN: nothing written. Re-run with --apply --backup-tag <tag>.")
        return 0
    backup = Path(str(xslt) + f".bak-{args.backup_tag}")
    if not backup.exists():
        shutil.copyfile(xslt, backup)
    xslt.write_bytes(new_src.encode("utf-8"))
    print(f"WROTE {xslt}; backup {backup.name}. A full game RESTART is required.")
    return 0


def main(argv=None) -> int:
    """_run, with a crash reported as exit 3 so a caller can tell it from drift (exit 1)."""
    try:
        return _run(argv)
    except Exception:
        traceback.print_exc()
        return 3


if __name__ == "__main__":
    sys.exit(main())
