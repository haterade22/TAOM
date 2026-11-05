#!/usr/bin/env python
"""Keep taom_enlistment_strings.xml in step with the duty text in enlistment_duties.json.

WHY THIS EXISTS
---------------
The English for every duty, interactive duty and incident lives in ONE place:
`Main/_Module/ModuleData/enlistment/enlistment_duties.json`, as `{=KEY}English` strings.

    title    every row                      taom_enlist_duty_<id>_title
    success  field duties                   taom_enlist_duty_<id>_success
    failure  field duties                   taom_enlist_duty_<id>_failure
    body     interactive duties, incidents  taom_enlist_duty_<id>_body
    label    each option (A and B)          taom_enlist_duty_<id>_opta / _optb

The key is fixed by the row id and the suffix, because the game composes the same key from the id
at runtime (`DutyText.KeyFor`). A literal `{=key}` grep cannot find a runtime-composed key, so
without this tool the keys would ship unregistered and untranslated in all 12 languages.

`taom_enlistment_strings.xml` stays the translation registry: this tool reads the JSON and adds or
updates the matching rows there. It never deletes a row, and it never touches a row it does not own.
The `_success` / `_failure` rows of interactive duties and incidents are shared sentences, hand
registered and not in the JSON; the tool only checks that every row id has both registered.

MODES AND EXIT CODES (every mode: 0 ok, 1 drift, 2 error)
---------------------------------------------------------
  --dry-run   report what would change; exit 0 (2 on an error)
  --check     the same report; exit 1 when the XML is out of step (a new row, a changed English, or a
              row id with no registered _success/_failure row), for CI and review
  --apply     write the new rows and updated English into the XML; exit 0 (2 on an error, and
              nothing is written)

An error is a malformed or missing input file, a duty section that is not a list, text that is not
exactly `{=KEY}English`, no string read at all, output that would not parse as XML, a file that cannot
be written, and (for --apply) a row id whose shared `_success`/`_failure` rows are not registered yet.
Register those two rows by hand first, then run --apply.

A changed English rewrites a registered row, which makes its 12 translations stale. The report lists
EVERY changed key; the follow-up is the checklist in docs/localization/TRANSLATOR_GUIDE.md, "The
checklist when you edit an existing English string".

The XML is read and written byte-faithfully (`newline=""`, plain `utf-8`, BOM kept): see tools/README.md
"XML I/O convention".
"""

from __future__ import annotations

import argparse
import html
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parents[1]
DUTIES_JSON = REPO_ROOT / "Main/_Module/ModuleData/enlistment/enlistment_duties.json"
STRINGS_XML = REPO_ROOT / "Main/_Module/ModuleData/taom_enlistment_strings.xml"

FIELD_SECTION = "fieldDuties"
POPUP_SECTIONS = ("interactiveDuties", "incidents")
SECTIONS = (FIELD_SECTION,) + POPUP_SECTIONS
BLOCK_COMMENT = "<!-- Duty text: from enlistment_duties.json via tools/harvest_duty_strings.py -->"
GUIDE_SECTION = 'docs/localization/TRANSLATOR_GUIDE.md, "The checklist when you edit an existing English string"'
KEY_RE = re.compile(r"^\{=([^}]+)\}(.*)$", re.DOTALL)
ID_RE = re.compile(r"^[a-z0-9_]+$")
ROW_RE = re.compile(r'<string\s+id="([^"]+)"\s+text="([^"]*)"\s*/>')
RESULT_SUFFIXES = ("success", "failure")


class HarvestError(Exception):
    """Bad input or output: reported, exit 2."""


def xml_escape(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                .replace('"', "&quot;").replace("'", "&#x27;")
                .replace("\t", "&#9;").replace("\n", "&#10;").replace("\r", "&#13;"))


def _row_texts(row: dict, popup: bool) -> Iterable[tuple[str, str, object]]:
    """(suffix, JSON field, text) for every text the row should carry, in a stable order.

    A required text that is absent is yielded as None, so the caller reports it. A field duty
    carries title, success and failure; an interactive duty or incident carries title, body and a
    label on each option (its result lines are shared rows, not in the JSON).
    """
    yield "title", "title", row.get("title")
    if popup:
        yield "body", "body", row.get("body")
        for suffix, option in (("opta", "optionA"), ("optb", "optionB")):
            spec = row.get(option)
            yield suffix, option + ".label", spec.get("label") if isinstance(spec, dict) else None
    else:
        yield "success", "success", row.get("success")
        yield "failure", "failure", row.get("failure")


def collect(data) -> tuple[dict[str, str], list[str], list[str]]:
    """(key -> English, row ids, problems) for the duty rows.

    A string must be exactly `{=taom_enlist_duty_<id>_<suffix>}English` with non-blank English, and an
    id must match [a-z0-9_]+. A list section the tool does not read is a problem, because its text
    would silently never be registered. Problems never produce a row.
    """
    strings: dict[str, str] = {}
    ids: list[str] = []
    problems: list[str] = []
    if not isinstance(data, dict):
        return strings, ids, ["the JSON root is not an object"]

    for section in SECTIONS:
        if section in data and data[section] is not None and not isinstance(data[section], list):
            problems.append(f"section '{section}' must be a list of rows")

    for section, value in data.items():
        if section not in SECTIONS and isinstance(value, list) and any(isinstance(r, dict) and "id" in r for r in value):
            problems.append(f"section '{section}' holds rows but is not one this tool reads ({', '.join(SECTIONS)})")

    for section in SECTIONS:
        popup = section in POPUP_SECTIONS
        rows = data.get(section)
        for row in rows if isinstance(rows, list) else []:
            row_id = row.get("id") if isinstance(row, dict) else None
            if not isinstance(row_id, str) or not row_id:
                problems.append(f"{section}: a row has no id")
                continue
            if not ID_RE.match(row_id):
                problems.append(f"{section}: id '{row_id}' must match [a-z0-9_]+")
                continue
            if row_id in ids:
                problems.append(f"{row_id}: duplicate row id")
                continue
            ids.append(row_id)
            for suffix, field, text in _row_texts(row, popup):
                key = f"taom_enlist_duty_{row_id}_{suffix}"
                if text is None:
                    problems.append(f"{row_id}: {field} text is missing")
                    continue
                m = KEY_RE.match(text) if isinstance(text, str) else None
                if not m or m.group(1) != key:
                    problems.append(f"{row_id}: {field} must start with '{{={key}}}'")
                elif not m.group(2).strip():
                    problems.append(f"{row_id}: {field} has no English after '{{={key}}}'")
                else:
                    strings[key] = m.group(2)
    if not strings and not problems:
        problems.append("no duty string was read from the JSON")
    return strings, ids, problems


def registered_rows(xml: str) -> dict[str, str]:
    """id -> decoded text attribute. Anchored to `<string id=` so an id named in a comment never counts."""
    return {html.unescape(m.group(1)): html.unescape(m.group(2)) for m in ROW_RE.finditer(xml)}


def plan(strings: dict[str, str], xml: str) -> tuple[list[tuple[str, str]], list[tuple[str, str, str]]]:
    """(new rows, changed rows). A changed row is (key, registered text, wanted text), compared whole."""
    registered = registered_rows(xml)
    new: list[tuple[str, str]] = []
    changed: list[tuple[str, str, str]] = []
    for key in sorted(strings):
        wanted = "{=" + key + "}" + strings[key]
        if key not in registered:
            new.append((key, strings[key]))
        elif registered[key] != wanted:
            changed.append((key, registered[key], wanted))
    return new, changed


def missing_results(ids: list[str], xml: str, strings: dict[str, str] | None = None) -> list[str]:
    """The `_success`/`_failure` keys every row id must have registered under their OWN inline key,
    unless the JSON supplies them (`strings`). A row that carries another row's key does not count."""
    registered = registered_rows(xml)
    needed = (f"taom_enlist_duty_{i}_{s}" for i in ids for s in RESULT_SUFFIXES)
    return [k for k in needed if k not in (strings or {}) and not registered.get(k, "").startswith("{=" + k + "}")]


def apply_plan(xml: str, new: list[tuple[str, str]], changed: list[tuple[str, str, str]]) -> str:
    """The XML with the new rows added and the changed rows rewritten in place. Raises ValueError
    when the result would not parse; nothing else about the file is touched."""
    nl = "\r\n" if xml.count("\r\n") * 2 > xml.count("\n") else "\n"
    for key, _old, wanted in changed:
        row = f'\t<string id="{xml_escape(key)}" text="{xml_escape(wanted)}" />'
        pattern = re.compile(r'[ \t]*<string\s+id="' + re.escape(xml_escape(key)) + r'"\s+text="[^"]*"\s*/>')
        xml, count = pattern.subn(lambda _m: row, xml, count=1)
        if count != 1:
            raise ValueError(f"could not rewrite the row for {key}")
    if new:
        block = nl.join(
            f'\t<string id="{xml_escape(k)}" text="{xml_escape("{=" + k + "}" + v)}" />' for k, v in new)
        marker = re.search(r"(\s*</strings>)", xml)
        if not marker:
            raise ValueError("no </strings> close tag in the strings XML")
        xml = xml[:marker.start()] + nl + "\t" + BLOCK_COMMENT + nl + block + xml[marker.start():]
    try:
        ET.fromstring(xml.encode("utf-8"))
    except ET.ParseError as ex:
        raise ValueError(f"the result would not parse as XML: {ex}") from ex
    return xml


def orphans(strings: dict[str, str], xml: str, ids: list[str]) -> list[str]:
    """Registered duty text rows (title/body/opta/optb/success/failure) whose row id the JSON no longer has."""
    pattern = re.compile(r"^taom_enlist_duty_(.+)_(title|body|opta|optb|success|failure)$")
    found = []
    for key in registered_rows(xml):
        m = pattern.match(key)
        if m and key not in strings and m.group(1) not in ids:
            found.append(key)
    return sorted(found)


def read_text(path: Path) -> str:
    # newline="" and plain utf-8 are load-bearing: CRLF and a BOM survive the round trip.
    with open(path, encoding="utf-8", newline="") as handle:
        return handle.read()


def load_inputs(json_path: Path, xml_path: Path) -> tuple[object, str]:
    try:
        data = json.loads(json_path.read_bytes())
    except (OSError, ValueError) as ex:
        raise HarvestError(f"cannot read {json_path}: {ex}") from ex
    try:
        xml = read_text(xml_path)
        ET.fromstring(xml.encode("utf-8"))
    except (OSError, ET.ParseError, UnicodeError) as ex:
        raise HarvestError(f"cannot read {xml_path}: {ex}") from ex
    return data, xml


def run(args: argparse.Namespace) -> int:
    data, xml = load_inputs(args.json, args.xml)
    strings, ids, problems = collect(data)
    if problems:
        print("ERROR: the duty text in enlistment_duties.json is malformed:")
        for p in problems:
            print(f"  - {p}")
        return 2

    new, changed = plan(strings, xml)
    absent = missing_results(ids, xml, strings)
    stray = orphans(strings, xml, ids)

    print(f"  rows read from the JSON:  {len(ids)}")
    print(f"  duty strings:             {len(strings)}")
    print(f"  new rows:                 {len(new)}")
    print(f"  changed English:          {len(changed)}")
    print(f"  missing result rows:      {len(absent)}")
    for key, v in new:
        print(f"    + {key} = {v[:60]}")
    for key, old, wanted in changed:
        print(f"    ~ {key}:\n        was: {old}\n        now: {wanted}")
    for key in absent:
        print(f"    ! {key} is not registered under its own key (hand-register the shared result row first, or add the string to a field row)")
    if changed:
        print(f"  NOTE: each changed English makes its 12 translations stale. Follow {GUIDE_SECTION}.")
    if stray:
        print(f"  NOTE: {len(stray)} registered duty text row(s) belong to a row the JSON no longer has (left alone): {', '.join(stray[:6])}")
    drift = len(new) + len(changed) + len(absent)
    print(f"  total drift: {drift}")

    if args.apply:
        if absent:
            print("\nERROR: nothing written: a row id has no registered result row, and this tool cannot invent a shared sentence.")
            return 2
        if not (new or changed):
            print("\n  Nothing to write.")
            return 0
        try:
            out = apply_plan(xml, new, changed)
        except ValueError as ex:
            print(f"\nERROR: nothing written: {ex}")
            return 2
        try:
            with open(args.xml, "w", encoding="utf-8", newline="") as handle:
                handle.write(out)
        except OSError as ex:
            print(f"\nERROR: could not write {args.xml}: {ex}")
            return 2
        print(f"\n  Wrote {len(new) + len(changed)} change(s) to {args.xml}")
        return 0
    if args.check:
        return 1 if drift else 0
    print("\n  (dry run: nothing written)")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true", help="report only; exit 0")
    mode.add_argument("--check", action="store_true", help="report only; exit 1 when the XML is out of step")
    mode.add_argument("--apply", action="store_true", help="write the XML")
    ap.add_argument("--json", type=Path, default=DUTIES_JSON, help=argparse.SUPPRESS)
    ap.add_argument("--xml", type=Path, default=STRINGS_XML, help=argparse.SUPPRESS)
    args = ap.parse_args(argv)
    try:
        return run(args)
    except HarvestError as ex:
        print(f"ERROR: {ex}")
        return 2


if __name__ == "__main__":
    sys.exit(main())
