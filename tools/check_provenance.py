#!/usr/bin/env python3
"""Check TAOM's provenance register against what ships (docs/reference/provenance-register.md).

Three checks, and nothing else:

1. Vocabulary: every register row's Derivation and Status cell is in its closed set.
2. Binaries: every tracked non-TAOM binary under `*/_Module/bin/*` matches, through BINARY_ROWS
   below, a register row whose Derivation is `redistributed` and whose Status is not `removed`.
3. Adoption records: every `docs/reviews/adopt-*.md` is named by the register (its file name
   or its source slug appears there) or says "nothing taken".

    python tools/check_provenance.py

Exit 0 when clean, 1 with one line per miss, 2 when it cannot judge (no git, a root outside a repo, no
register). A zero does not prove intent: it cannot see a source
that is named nowhere, an unlisted euphemism, or a wrong Derivation. Read the register when you
touch a ported feature. A new binary needs a BINARY_ROWS line, which is the point: it forces the
question of which register row covers it.
"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
REGISTER = REPO_ROOT / "docs" / "reference" / "provenance-register.md"
ADOPT_GLOB = "docs/reviews/adopt-*.md"
START, END = "<!-- provenance-register-start -->", "<!-- provenance-register-end -->"

DERIVATIONS = {"clean-room", "behavioural-port", "verbatim-port", "data-port", "redistributed",
               "interop-only", "comparison-only"}
STATUSES = {"cleared", "pending-license", "uncleared", "removed"}

BINARY_EXTENSIONS = {".dll", ".exe", ".so", ".dylib", ".winmd"}
# Binaries TAOM builds itself: no third-party row applies.
TAOM_BUILT = re.compile(r"^TAOM[\w.]*\.dll$")
# File name pattern -> the register row (Source cell) that must cover it. First match wins.
BINARY_ROWS = [
    (re.compile(r"^0Harmony\.dll$"), "Lib.Harmony"),
    (re.compile(r"^MinHook[\w.]*\.dll$"), "MinHook"),
    (re.compile(r"^(BUTR\.CrashReport|Bannerlord\.ButterLib|Bannerlord\.MBOptionScreen|"
                r"Bannerlord\.ModuleLoader|Bannerlord\.UIExtenderEx|MCM\.)[\w.]*\.dll$"), "BUTR stack"),
    (re.compile(r"^(Microsoft|System)\.[\w.]*\.dll$"), ".NET Foundation"),
    (re.compile(r"^Serilog[\w.]*\.dll$"), "Serilog"),
    (re.compile(r"^DryIoc[\w.]*\.dll$"), "DryIoc (Maksim Volkau)"),
    (re.compile(r"^Newtonsoft\.Json[\w.]*\.dll$"), "Json.NET (James Newton-King)"),
]
NOTHING_TAKEN = re.compile(r"nothing\s+(?:was\s+)?taken", re.IGNORECASE)


def _cells(line: str) -> list[str]:
    parts = re.split(r"(?<!\\)\|", line.strip())
    return [c.strip() for c in parts[1:-1]] if len(parts) > 2 else []


def parse_register(text: str) -> list[dict]:
    """Rows of the register table between its markers. Source is the first cell, Derivation the
    fourth, Status the last, so a stray pipe inside a Covers cell does not shift them."""
    if START in text and END in text:
        text = text[text.index(START):text.index(END)]
    rows = []
    for number, line in enumerate(text.splitlines(), start=1):
        if not line.lstrip().startswith("|"):
            continue
        cells = _cells(line)
        if not cells or cells[0] == "Source" or set(cells[0]) <= set("-: "):
            continue
        if len(cells) < 6:
            rows.append({"source": cells[0], "cells": len(cells), "line": number})
            continue
        rows.append({"source": cells[0], "derivation": cells[3].strip("`"),
                     "status": cells[-1].strip("`"), "line": number})
    return rows


def check_vocabulary(rows: list[dict]) -> list[str]:
    misses = []
    for r in rows:
        if "cells" in r:
            misses.append(f"register row '{r['source']}' (line {r['line']}): {r['cells']} cells, expected 6, "
                          f"so its Derivation and Status cannot be checked")
            continue
        if r["derivation"] not in DERIVATIONS:
            misses.append(f"register row '{r['source']}': Derivation '{r['derivation']}' is not in the closed set")
        if r["status"] not in STATUSES:
            misses.append(f"register row '{r['source']}': Status '{r['status']}' is not in the closed set")
    return misses


def check_binaries(rows: list[dict], files: list[str]) -> list[str]:
    by_source = {r["source"]: r for r in rows}
    misses = []
    for path in files:
        name = path.replace("\\", "/").rsplit("/", 1)[-1]
        if Path(name).suffix.lower() not in BINARY_EXTENSIONS or TAOM_BUILT.match(name):
            continue
        source = next((src for rx, src in BINARY_ROWS if rx.match(name)), None)
        if source is None:
            misses.append(f"{path}: no BINARY_ROWS entry, so no register row is known to cover it")
            continue
        row = by_source.get(source)
        if row is None:
            misses.append(f"{path}: BINARY_ROWS names '{source}', which is not a register row")
        elif row["derivation"] != "redistributed" or row["status"] == "removed":
            misses.append(f"{path}: row '{source}' is {row['derivation']} / {row['status']}, "
                          f"not a live redistributed row")
    return misses


def _slug(record_name: str) -> str:
    """`adopt-agent-reach-2026-10-26.md` -> `agent reach`."""
    m = re.match(r"adopt-(.+?)(?:-\d{4}-\d{2}-\d{2})?\.md$", record_name)
    return re.sub(r"[^a-z0-9]+", " ", (m.group(1) if m else record_name).lower()).strip()


def check_adopt_records(register_text: str, records: dict[str, str]) -> list[str]:
    """A record passes when the register names its file, or names its source (the record's slug
    in the register text, hyphens read as spaces), or the record says "nothing taken"."""
    flat = " " + re.sub(r"[^a-z0-9]+", " ", register_text.lower()) + " "
    misses = []
    for name, text in sorted(records.items()):
        if name in register_text or f" {_slug(name)} " in flat or NOTHING_TAKEN.search(text):
            continue
        misses.append(f"docs/reviews/{name}: the register names neither the file nor '{_slug(name)}', "
                      f"and the record does not say 'nothing taken'; add a register row or state the outcome")
    return misses


def _tracked(*pathspecs: str) -> list[str]:
    """Tracked paths matching the pathspecs. Raises OSError when git cannot answer: an empty list
    would read as a clean tree."""
    proc = subprocess.run(["git", "-C", str(REPO_ROOT), "ls-files", "--", *pathspecs],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        raise OSError(f"git ls-files failed in {REPO_ROOT} (exit {proc.returncode}): {proc.stderr.strip()}")
    return [n for n in proc.stdout.splitlines() if n.strip()]


def run_checks() -> list[str]:
    text = REGISTER.read_text(encoding="utf-8", errors="replace")
    rows = parse_register(text)
    if not rows:
        return [f"{REGISTER.relative_to(REPO_ROOT).as_posix()}: no register rows found between the markers"]
    records = {Path(n).name: (REPO_ROOT / n).read_text(encoding="utf-8", errors="replace")
               for n in _tracked(ADOPT_GLOB)}
    return (check_vocabulary(rows) + check_binaries(rows, _tracked("*/_Module/bin/*"))
            + check_adopt_records(text, records))


def main(argv: list[str]) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    try:
        misses = run_checks()
    except OSError as err:   # no git, a root outside a repo, or no register: nothing was checked
        print(f"check_provenance: cannot judge: {err}", file=sys.stderr)
        return 2
    for m in misses:
        print(m)
    print(f"check_provenance: {len(misses)} miss(es)")
    return 1 if misses else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
