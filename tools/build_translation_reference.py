#!/usr/bin/env python3
"""Build the translation team reference the AI translator follows (#1421).

    python tools/build_translation_reference.py --lang CNs --lang CNt --from <TAOM_CN.zip or folder>
    python tools/build_translation_reference.py --lang CNs --lang CNt --from <drop> --apply

Writes tools/translation_reference/<lang>.json: an [English, text] pair for every row of ours whose
current text is the team's text. The team's text comes from its ModuleData/Languages/<lang>/*.xml rows
(by id) and its runtime files (ModuleData/RuntimeTranslations*.xml for CNs, ModuleData/CNt/ for CNt,
by English). A row holds the team's text only as the importer would write it (an Armory tier appended);
a stale row (its English changed since the team's base), a kept, rejected or overridden row is ours, so
it is not in the reference. Run it after tools/import_team_translations.py --apply.

One English with two team texts keeps the one the team uses most (a tie: the first in sort order).
--module adds TAOM_Map and the Armory from the live install; the shipped files hold all three, so name
all three on a rebuild. A language that yields no pairs writes nothing, and the run exits 2. A dry run (the default) prints the counts
and writes nothing.

The drop is untrusted data: it is read as XML only (import_team_translations.Drop), never run or
extracted.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from _team_reference import REFERENCE_DIR, path_for  # noqa: E402
from import_team_translations import (  # noqa: E402
    KEYED,
    MODULES,
    TEAM_LANGS,
    Drop,
    Target,
    _drop_xml,
    base_version,
    history_at,
    make_plan,
    our_targets,
    team_rows,
)
from translate_with_claude import load_overrides  # noqa: E402

# The team's runtime files for TAOM, per language (the DualWield file belongs to another module).
RUNTIME_FILES = {
    "CNs": ("ModuleData/RuntimeTranslations.xml", "ModuleData/RuntimeTranslations.TAOM.Client.xml"),
    "CNt": ("ModuleData/CNt/RuntimeTranslations.xml", "ModuleData/CNt/RuntimeTranslations.TAOM.Client.xml"),
}


def _unkeyed(text: str) -> str:
    m = KEYED.match(text)
    return m.group(2) if m else text


def runtime_pairs(drop: Drop, lang: str, problems: list[str]) -> list[tuple[str, str]]:
    """(English, text) from the team's runtime files, with any {=key} prefix removed from both."""
    pairs = []
    for wanted in RUNTIME_FILES[lang]:
        name = drop.find(wanted)
        root = _drop_xml(drop, name, problems) if name else None
        if root is None:
            continue
        for el in root.iter("string"):
            source, text = _unkeyed(el.get("source", "")), _unkeyed(el.get("text", ""))
            if source.strip() and text.strip():
                pairs.append((source, text))
    return pairs


def reference_pairs(drop: Drop, lang: str, targets: list[Target], problems: list[str] | None = None,
                    overrides: dict[str, str] | None = None) -> list[list[str]]:
    """[English, text] for each row of ours that holds the team's text, one per English, sorted. A drop with
    no rows for the language is a problem, and gives no pairs.

    A row holds the team's text when it equals what the importer accepts for it (make_plan: the team's text
    with an Armory tier appended; never a stale row, whose English changed since the team's base, or a
    KEEP_OURS row), or when a runtime pair gives its English that text. A row with an override is ours."""
    problems = [] if problems is None else problems
    overrides = {} if overrides is None else overrides
    team = team_rows(drop, lang, problems)
    runtime: dict[str, set[str]] = defaultdict(set)
    for source, text in runtime_pairs(drop, lang, problems):
        runtime[source].add(text)
    if not team and not runtime:
        problems.append(f"{lang}: the drop holds no ModuleData/Languages/{lang} rows and no runtime files")
        return []
    accepted = make_plan(team, targets, overrides).cache
    counts: dict[str, Counter] = defaultdict(Counter)
    for target in targets:
        for sid, current in target.current.items():
            english = target.english.get(sid)
            if english is None or current == english or sid in overrides:
                continue
            if accepted.get(sid) == current or current in runtime.get(english, ()):
                counts[english][current] += 1
    return [[en, min(c.items(), key=lambda kv: (-kv[1], kv[0]))[0]] for en, c in sorted(counts.items())]


def write_reference(path: Path, lang: str, base: str | None, pairs: list[list[str]]) -> None:
    """One pair per line, so a rebuild's diff shows the pairs that changed."""
    head = json.dumps({"lang": lang, "base": base}, ensure_ascii=False)[:-1]
    body = ",\n".join("    " + json.dumps(p, ensure_ascii=False) for p in pairs)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f'{head}, "pairs": [\n{body}\n]}}\n', encoding="utf-8")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n\n", 1)[0])
    p.add_argument("--lang", action="append", required=True, choices=TEAM_LANGS)
    p.add_argument("--from", dest="source", required=True, type=Path, help="the team's module: a .zip or a folder")
    p.add_argument("--module", action="append", choices=MODULES,
                   help="our module(s) to read; default TAOM (TAOM_Map and Armory are live installs)")
    p.add_argument("--base", help="the git ref of TAOM the team translated (default: the TAOM version the "
                                  "drop's SubModule.xml depends on)")
    p.add_argument("--out-dir", type=Path, default=REFERENCE_DIR)
    p.add_argument("--apply", action="store_true", help="write the reference files (default: a dry run)")
    args = p.parse_args(argv)

    modules = list(dict.fromkeys(args.module or ["TAOM"]))
    problems: list[str] = []
    with Drop(args.source) as drop:
        base = args.base or base_version(drop)
        then, later = history_at(base, modules, args.source)
        for lang in dict.fromkeys(args.lang):
            targets = our_targets(lang, modules, then, later)
            pairs = reference_pairs(drop, lang, targets, problems, load_overrides(lang))
            path = path_for(lang, args.out_dir)
            if not pairs:  # a wrong drop must not empty a tracked reference
                problems.append(f"{lang}: 0 pairs; {path.name} not written")
                continue
            print(f"[{lang}] {len(pairs)} pairs from {', '.join(modules)}" + (f" -> {path}" if args.apply else ""))
            if args.apply:
                write_reference(path, lang, base, pairs)
    for problem in problems:
        print(f"  PROBLEM: {problem}", file=sys.stderr)
    if not args.apply:
        print("dry run: nothing written")
    return 2 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
