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
The result is parsed before it is written; dry-run by default; `--apply` writes (a `.bak-rangedsync`
sidecar once per live file; the mirror is a git repo and needs none); `--verify` exits 1 when a
language lacks a current row or still holds a retired one; idempotent. A row the tool cannot derive
(no retired row for that line, class and band), or a `ladder_*` row it cannot parse, is an error and
nothing is written.

THE TRANSLATION CACHE
---------------------
rebuild_translation_files.py (the recovery after an Armory reinstall) resolves each row as
override, then tools/translation_cache/<lang>.json, then English, keyed on the string id. A cache
still holding the retired ids rebuilds every carried name in English, and translate_with_claude.py
would pay to translate them again (docs/reference/localization-map.md: a tool that rewrites
translated text must update the cache too). So `--apply` also sets each current ladder id in each
language's cache to the text now in the live file and drops the retired ladder ids; `--verify`
checks the cache as well.

USAGE
-----
    python tools/sync_ranged_ladder_translations.py            # dry run
    python tools/sync_ranged_ladder_translations.py --apply    # live Armory + v1.5 mirror
    python tools/sync_ranged_ladder_translations.py --verify
"""
from __future__ import annotations

import argparse
import html
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _gamedir as gd  # noqa: E402  ASSET_REPO, armory_trees
import ranged_ladder as rl  # noqa: E402
import rebalance_troops as rb  # noqa: E402  DEFAULT_GAME_MODULES
import translate_with_claude as tc  # noqa: E402  the cache's one reader and writer

DEFAULT_ASSET_REPO = gd.ASSET_REPO
BACKUP_SUFFIX = ".bak-rangedsync"
# The line ids #617 split out of the #582 gondor_special line.
RENAMED_LINES = {"ithilien": "gondor_special", "blackroot": "gondor_special"}
_ROW_RE = re.compile(r'^(?P<indent>[ \t]*)<string id="(?P<id>ladder_[^"]+)" text="(?P<text>[^"]*)" ?/>(?P<eol>[\r\n]*)$')
# Any line naming a ladder id in an id attribute. One _ROW_RE does not match would be kept as an
# ordinary line: a current id written twice that --verify then passes.
_ANY_LADDER_ROW_RE = re.compile(r'\bid\s*=\s*["\']ladder_')


class SyncError(Exception):
    """A row that cannot be derived, or a file that would stop parsing."""


def retired_id(item: rl.LadderItem) -> str:
    line = RENAMED_LINES.get(item.line, item.line)
    return f"{rl.ID_PREFIX}{line}_{rl.CLASS_TOKEN[item.cls]}_{item.band.lower()}"


def translated_base(text: str) -> str:
    base = rl.TIER_NUMERAL_RE.sub("", text)
    if base == text:
        raise SyncError(f"no trailing numeral to strip in {text!r}")
    return base


def split_lines(text: str) -> list[str]:
    """Lines WITH their terminators, whatever the terminator is (the translated files use \\r\\r\\n)."""
    return re.findall(r"[^\r\n]*(?:\r*\n|\r+|$)", text)[:-1] if text else []


def rewrite(text: str, items: list[rl.LadderItem]) -> tuple[str, int, dict[str, str]]:
    """(new text, rows removed, {id: text} of the rows written, XML-escaped as in the file).
    Raises SyncError when a current row cannot be derived."""
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
        if _ANY_LADDER_ROW_RE.search(ln):
            raise SyncError(f"a ladder row this tool cannot parse: {ln.strip()!r}")
        keep.append(ln)
    current = {i.id for i in items}
    if first is None:
        raise SyncError("the file holds no ladder rows to derive translations from")
    rows: dict[str, str] = {}
    for item in items:
        if item.id in old and item.id in current and rl.TIER_NUMERAL_RE.search(old[item.id]):
            text_ = old[item.id]           # already current: keep whatever translation it has
        else:
            src = old.get(retired_id(item))
            if src is None:
                raise SyncError(f"{item.id}: no retired row {retired_id(item)} to take the translation from")
            text_ = f"{translated_base(src)} {rl.TIER_NUMERAL[item.tier]}"
        rows[item.id] = text_
    removed = sum(1 for k in old if k not in current)
    block = [f'{indent}<string id="{iid}" text="{t}" />{eol}' for iid, t in rows.items()]
    new_text = "".join(keep[:first] + block + keep[first:])
    return new_text, removed, rows


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


def read_cache(lang: str, cache_dir: Path) -> dict[str, str]:
    try:
        cache = tc.load_cache(lang, cache_dir)
    except (OSError, ValueError) as exc:
        raise SyncError(f"cannot read the {lang} translation cache in {cache_dir}: {exc}") from None
    if not isinstance(cache, dict):
        raise SyncError(f"the {lang} translation cache in {cache_dir} is not an id-to-text object")
    return cache


def cache_plan(texts: dict[str, dict[str, str]], planned_ids: set[str], cache_dir: Path) -> dict[str, dict]:
    """{language: new cache} for every language whose cache is out of step with its live rows.
    `texts` is {language: {ladder id: row text}}, the rows rewrite() derived for the live files.
    Those ids take the row's text (unescaped, as the translator stores it); every other id of a
    ladder cell's shape that is not a current id leaves the cache; nothing else in it moves."""
    out = {}
    for lang, rows in sorted(texts.items()):
        cache = read_cache(lang, cache_dir)
        new = {k: v for k, v in cache.items() if not (rl.LADDER_ID_RE.match(k) and k not in planned_ids)}
        for iid, text in rows.items():
            new[iid] = html.unescape(text)
        if new != cache:
            out[lang] = new
    return out


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--game-modules", default=rb.DEFAULT_GAME_MODULES)
    ap.add_argument("--asset-repo", default=str(DEFAULT_ASSET_REPO))
    ap.add_argument("--spec", default=str(rl.DEFAULT_SPEC))
    ap.add_argument("--cache-dir", default=str(tc.CACHE_DIR),
                    help="the translator's cache folder (default %(default)s)")
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true")
    mode.add_argument("--verify", action="store_true")
    args = ap.parse_args(argv)

    try:
        spec = rl.load_spec(args.spec)
    except (OSError, ValueError) as exc:
        print(f"ERROR: cannot read spec {args.spec}: {exc}. Nothing was written.")
        return 2
    problems = rl.validate_spec(spec)
    if problems:
        print(f"ERROR: {args.spec} contradicts itself; nothing was written:")
        for p in problems[:8]:
            print(f"  - {p}")
        return 2
    armory = Path(args.game_modules) / "LOTRLOME_Armory" / "ModuleData"
    if not armory.is_dir():
        print(f"ERROR: LOTRLOME_Armory not found under {args.game_modules}. Nothing was written.")
        return 2
    cache_dir = Path(args.cache_dir)
    if not cache_dir.is_dir():
        print(f"ERROR: translation cache folder {cache_dir} not found; pass --cache-dir. Nothing was written.")
        return 2
    trees = gd.armory_trees(armory, args.asset_repo)

    planned, drift = [], []
    live_rows: dict[str, dict[str, str]] = {}      # the cache follows the LIVE files only
    try:
        for label, md in trees:
            files = targets(md, spec)
            if not files:
                raise SyncError(f"{label}: no translated loc files under {md / 'Languages'}")
            for path, items in files:
                raw = path.read_bytes()
                text = raw.decode("utf-8")
                new_text, removed, rows = rewrite(text, items)
                if label == "armory":
                    live_rows.setdefault(path.parent.name, {}).update(rows)
                if new_text != text:
                    try:
                        ET.fromstring(new_text.encode("utf-8"))
                    except ET.ParseError as exc:
                        raise SyncError(f"{path} would no longer parse: {exc}") from None
                    planned.append((label, path, new_text))
                    drift.append(f"{label} {path.parent.name}/{path.name}: {removed} retired, {len(rows)} current")
        caches = cache_plan(live_rows, {i.id for i in rl.planned_items(spec)}, cache_dir)
    except SyncError as exc:
        print(f"ERROR: {exc}. Nothing was written.")
        return 2
    drift += [f"cache {lang.lower()}.json out of step with the live rows" for lang in caches]

    if args.verify:
        if drift:
            print(f"DRIFT: {len(drift)} file(s) not in sync:")
            for d in drift:
                print(f"  - {d}")
            return 1
        print("OK: every translated loc file and cache carries exactly the current ladder rows")
        return 0
    for d in drift:
        print(("rewrote " if args.apply else "would rewrite ") + d)
    if args.apply:
        for label, path, new_text in planned:
            backup = path.with_name(path.name + BACKUP_SUFFIX)
            if label == "armory" and not backup.exists():
                backup.write_bytes(path.read_bytes())
            path.write_bytes(new_text.encode("utf-8"))
        for lang, cache in caches.items():
            tc.save_cache(lang, cache, cache_dir)
        print(f"Wrote {len(planned)} file(s) and {len(caches)} cache(s). Restart Bannerlord fully to load them.")
    else:
        print(f"Would rewrite {len(planned)} file(s) and {len(caches)} cache(s) (dry run; pass --apply)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
