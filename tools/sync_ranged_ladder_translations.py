#!/usr/bin/env python3
"""Carry the ranged ladder's translated item names from the retired ids to the current ones (#617).

WHY
---
generate_ranged_ladder_items.py writes the English name rows (Languages/loc_<folder>.xml); the twelve
translated languages live in Languages/<LANG>/loc_<folder>.xml and are filled by translate_with_claude.py,
whose --sync-ids only ADDS missing ids and never removes retired ones. When #617 replaced the #582
band items (`ladder_<line>_<cls>_<e|r|v|x|c>`) with per-tier items (`..._t<tier>`), every language
kept 130 dead band rows and had none of the 123 new ones, so check_external_loc_coverage.py failed
and every non-English player saw English bow names.

A ladder name is `<the donor's name, numeral stripped> <numeral>`, and the donor is chosen by the
tier's band, so the translated base of a new tier item is exactly the translated base of the retired
band item of the same line, class and band. This tool copies that base, appends the new tier
numeral, and replaces the retired rows with the current ones in place. No machine translation.

RULES (tools/README.md "XML I/O convention")
--------------------------------------------
Binary round-trip: the files keep their doubled-CR line endings, BOM state and every other byte;
only the `ladder_*` rows are replaced (removed, and the new block written where the first one sat).
The result is parsed before it is written; dry-run by default; `--apply` writes; `--verify` exits 1
when a language lacks a current row or still holds a retired one; idempotent. A row the tool cannot
derive (no retired row for that line, class and band) is an error and nothing is written.

USAGE
-----
    python tools/sync_ranged_ladder_translations.py            # dry run
    python tools/sync_ranged_ladder_translations.py --apply    # live Armory + v1.5 mirror
    python tools/sync_ranged_ladder_translations.py --verify
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
# The line ids #617 split out of the #582 gondor_special line.
RENAMED_LINES = {"ithilien": "gondor_special", "blackroot": "gondor_special"}
_ROW_RE = re.compile(r'^(?P<indent>[ \t]*)<string id="(?P<id>ladder_[^"]+)" text="(?P<text>[^"]*)" ?/>(?P<eol>[\r\n]*)$')
_NUMERAL_RE = re.compile(r"\s+(?:X|IX|VIII|VII|VI|V|IV|III|II|I)\s*$")


class SyncError(Exception):
    """A row that cannot be derived, or a file that would stop parsing."""


def retired_id(item: rl.LadderItem) -> str:
    line = RENAMED_LINES.get(item.line, item.line)
    return f"{rl.ID_PREFIX}{line}_{rl.CLASS_TOKEN[item.cls]}_{item.band.lower()}"


def translated_base(text: str) -> str:
    base = _NUMERAL_RE.sub("", text)
    if base == text:
        raise SyncError(f"no trailing numeral to strip in {text!r}")
    return base


def split_lines(text: str) -> list[str]:
    """Lines WITH their terminators, whatever the terminator is (the translated files use \\r\\r\\n)."""
    return re.findall(r"[^\r\n]*(?:\r*\n|\r+|$)", text)[:-1] if text else []


def rewrite(text: str, items: list[rl.LadderItem]) -> tuple[str, int, int]:
    """(new text, rows removed, rows written). Raises SyncError when a current row cannot be derived."""
    lines = split_lines(text)
    old: dict[str, str] = {}
    first = None
    keep = []
    indent, eol = "    ", "\n"
    for ln in lines:
        m = _ROW_RE.match(ln)
        if m:
            old[m.group("id")] = m.group("text")
            if first is None:
                first = len(keep)
                indent, eol = m.group("indent"), m.group("eol") or eol
            continue
        keep.append(ln)
    current = {i.id for i in items}
    if first is None:
        raise SyncError("the file holds no ladder rows to derive translations from")
    rows = []
    for item in items:
        if item.id in old and item.id in current and _NUMERAL_RE.search(old[item.id]):
            text_ = old[item.id]           # already current: keep whatever translation it has
        else:
            src = old.get(retired_id(item))
            if src is None:
                raise SyncError(f"{item.id}: no retired row {retired_id(item)} to take the translation from")
            text_ = f"{translated_base(src)} {rl.TIER_NUMERAL[item.tier]}"
        rows.append(f'{indent}<string id="{item.id}" text="{text_}" />{eol}')
    removed = sum(1 for k in old if k not in current)
    new_text = "".join(keep[:first] + rows + keep[first:])
    return new_text, removed, len(rows)


def targets(md: Path, spec: dict):
    """[(path, items)] for every translated loc file of every ladder folder."""
    by_folder: dict[str, list] = {}
    for item in rl.planned_items(spec):
        by_folder.setdefault(item.folder, []).append(item)
    out = []
    lang_root = md / "Languages"
    for lang in sorted(p for p in lang_root.iterdir() if p.is_dir()) if lang_root.exists() else []:
        for folder, items in by_folder.items():
            path = lang / f"loc_{folder}.xml"
            if path.exists():
                out.append((path, items))
    return out


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES)
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO))
    ap.add_argument("--spec", default=str(rl.DEFAULT_SPEC))
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--verify", action="store_true")
    args = ap.parse_args(argv)

    spec = rl.load_spec(args.spec)
    armory = Path(args.game_modules) / "LOTRLOME_Armory" / "ModuleData"
    if not armory.is_dir():
        print(f"ERROR: LOTRLOME_Armory not found under {args.game_modules}. Nothing was written.")
        return 2
    trees = [("armory", armory)]
    mirror = Path(args.asset_repo) / "ModuleData"
    if mirror.is_dir():
        trees.append(("mirror", mirror))
    else:
        print(f"WARNING: assets mirror not found at {args.asset_repo}; only the live Armory is touched")

    planned, drift = [], []
    try:
        for label, md in trees:
            files = targets(md, spec)
            if not files:
                raise SyncError(f"{label}: no translated loc files under {md / 'Languages'}")
            for path, items in files:
                raw = path.read_bytes()
                text = raw.decode("utf-8")
                new_text, removed, written = rewrite(text, items)
                if new_text != text:
                    try:
                        ET.fromstring(new_text.encode("utf-8"))
                    except ET.ParseError as exc:
                        raise SyncError(f"{path} would no longer parse: {exc}") from None
                    planned.append((path, new_text))
                    drift.append(f"{label} {path.parent.name}/{path.name}: {removed} retired, {written} current")
    except SyncError as exc:
        print(f"ERROR: {exc}. Nothing was written.")
        return 2

    if args.verify:
        if drift:
            print(f"DRIFT: {len(drift)} file(s) not in sync:")
            for d in drift:
                print(f"  - {d}")
            return 1
        print("OK: every translated loc file carries exactly the current ladder rows")
        return 0
    for d in drift:
        print(("rewrote " if args.apply else "would rewrite ") + d)
    if args.apply:
        for path, new_text in planned:
            path.write_bytes(new_text.encode("utf-8"))
        print(f"Wrote {len(planned)} file(s). Restart Bannerlord fully to load them.")
    else:
        print(f"Would rewrite {len(planned)} file(s) (dry run; pass --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
