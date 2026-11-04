#!/usr/bin/env python3
"""Report staged English edits whose translations were left behind. Report only: it never fails.

    python tools/check_stale_loc_rows.py --staged

For each TAOM English source (tools/_loc_sources.py) it compares the index with HEAD. A key whose
English text changed is listed per language when that language's row is byte-identical to its HEAD
row and the key's entry in tools/translation_cache/<lang>.json is unchanged too: nothing in the
commit touches that translation, so the game keeps showing the old wording
(docs/localization/TRANSLATOR_GUIDE.md, "Changing English Text That Is Already Translated").

A row passes when it changed in the same commit, equals the new English (a reset the translator will
redo), or still equals the OLD English (never translated). A changed or deleted cache entry passes. An
accent-only or number-only edit that the old translation already carries is also listed: read it, and
leave it when the row is right.

Exit code is 0 whatever it finds. Run it before committing an English edit; `/localize` names it.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _loc_sources import TAOM_SOURCES  # noqa: E402
from translate_with_claude import LANGUAGES as _LANGUAGES  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = "Main/_Module/ModuleData"
CACHE_DIR = "tools/translation_cache"
SOURCES = TAOM_SOURCES
LANGUAGES = {code: locale for code, (locale, _name) in _LANGUAGES.items()}

_STRING_RE = re.compile(r'<string\b(?:"[^"]*"|[^>"])*>')   # a quoted value may hold a `>`: {?DAYS>1}
_ID_RE = re.compile(r'\bid="([^"]*)"')
_TEXT_RE = re.compile(r'\btext="([^"]*)"')
_KEY_PREFIX_RE = re.compile(r"^\{=([^}]*)\}")


def _rows(xml: str, english: bool) -> dict[str, str]:
    rows: dict[str, str] = {}
    for tag in _STRING_RE.findall(xml):
        i, t = _ID_RE.search(tag), _TEXT_RE.search(tag)
        if not (i and t):
            continue
        if not english:
            rows[i.group(1)] = t.group(1)
            continue
        # The language rows and the cache use the `{=KEY}` as the id, which can differ from the English id.
        prefix = _KEY_PREFIX_RE.match(t.group(1))
        rows[prefix.group(1) if prefix else i.group(1)] = _KEY_PREFIX_RE.sub("", t.group(1))
    return rows


def english_rows(xml: str) -> dict[str, str]:
    """`{=KEY}` (else id) -> English text, without the `{=KEY}` prefix."""
    return _rows(xml, True)


def language_rows(xml: str) -> dict[str, str]:
    """id -> the row's text exactly as written."""
    return _rows(xml, False)


def find_stale(old_en: dict, new_en: dict, old_row: dict, new_row: dict,
               old_cache: dict, new_cache: dict) -> list[str]:
    """Keys whose English changed while one language's row and cache entry stayed as they were."""
    stale = []
    for key, new_text in new_en.items():
        old_text = old_en.get(key)
        if old_text is None or old_text == new_text:
            continue
        row = new_row.get(key)
        if row is None or row != old_row.get(key):
            continue            # no row, or the row changed in this commit
        if row in (new_text, old_text):
            continue            # reset to the new English, or never translated
        if new_cache.get(key) != old_cache.get(key):
            continue            # the cache was updated or the entry deleted
        stale.append(key)
    return sorted(stale)


def _git_show(spec: str) -> str | None:
    proc = subprocess.run(["git", "-C", str(REPO_ROOT), "show", spec],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    return proc.stdout.lstrip("﻿") if proc.returncode == 0 else None


def _both(path: str) -> tuple[str | None, str | None]:
    """(HEAD text, index text) of a tracked path; None for a side that does not hold it."""
    return _git_show(f"HEAD:{path}"), _git_show(f":{path}")


def _cache(text: str | None) -> dict:
    try:
        data = json.loads(text) if text else {}
    except ValueError:
        return {}
    return data if isinstance(data, dict) else {}


def staged_report() -> tuple[list[tuple[str, str, str]], int]:
    """((English source, key, language code) for every stale row of the staged edit, sources read)."""
    caches = {code: tuple(_cache(t) for t in _both(f"{CACHE_DIR}/{code.lower()}.json")) for code in LANGUAGES}
    found: list[tuple[str, str, str]] = []
    read = 0
    for source, template in SOURCES:
        old_xml, new_xml = _both(f"{DATA_DIR}/{source}")
        if old_xml is None or new_xml is None:
            continue
        read += 1
        if old_xml == new_xml:
            continue
        old_en, new_en = english_rows(old_xml), english_rows(new_xml)
        for code, locale in LANGUAGES.items():
            old_l, new_l = _both(f"{DATA_DIR}/Languages/{code}/{template.format(locale=locale)}")
            if old_l is None or new_l is None:
                continue
            old_cache, new_cache = caches[code]
            for key in find_stale(old_en, new_en, language_rows(old_l), language_rows(new_l),
                                  old_cache, new_cache):
                found.append((source, key, code))
    return found, read


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--staged", action="store_true", required=True,
                    help="compare the index with HEAD (the only mode)")
    ap.parse_args(argv)
    found, read = staged_report()
    if not read:
        print("check_stale_loc_rows: no source was read (not a git tree, or no source at HEAD and in the index)")
    for source, key, code in found:
        print(f"{source} {key}  {code}")
    keys = {(s, k) for s, k, _ in found}
    print(f"check_stale_loc_rows: {len(found)} stale row(s) in {len(keys)} key(s) "
          f"(report only; see TRANSLATOR_GUIDE.md 'Changing English Text That Is Already Translated')")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
