#!/usr/bin/env python3
"""Generate the localization entries for interactive duties and incidents.

WHY THIS EXISTS
---------------
`InteractiveDutyPresenter` builds its localization keys at RUNTIME from data-row ids:

    KeyFor(id, suffix) => "taom_enlist_duty_" + id + "_" + suffix

so a literal `{=key}` grep — which is how every other TAOM string is discovered, and what
`/localize` relies on — cannot find a single one of them. 14 duty/incident rows x 6 suffixes =
84 keys that ship as their English fallback in all 12 languages, silently, with no error anywhere.
`Main/Adapters/IInquiryAdapter.cs` documents the hazard; this closes it.

WHERE THE ENGLISH COMES FROM
----------------------------
Three different places, which is the other reason this needs a generator rather than hand-authoring:

  title / body   `DutyCopy` in InteractiveDutyPresenter.cs (a hardcoded C# dictionary)
  opta / optb    Humanize(option.Key) — the snake_case option key from enlistment_duties.json,
                 title-cased at runtime ("walk_rounds" -> "Walk Rounds")
  success/failure  two shared sentences in ShowResultToast

Run with --apply to write the entries into taom_enlistment_strings.xml (idempotent: existing ids
are left alone, so hand-tuned copy survives a re-run).
"""

from __future__ import annotations

import argparse
import io
import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
PRESENTER = REPO_ROOT / "Main/Features/Enlistment/Duties/InteractiveDutyPresenter.cs"
DUTIES_JSON = REPO_ROOT / "Main/_Module/ModuleData/enlistment/enlistment_duties.json"
STRINGS_XML = REPO_ROOT / "Main/_Module/ModuleData/taom_enlistment_strings.xml"

# Kept byte-identical to ShowResultToast's fallbacks. If those change, change these.
SUCCESS_TEXT = "It went well."
FAILURE_TEXT = "It didn't go as planned."


def xml_escape(text: str) -> str:
    return (text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                .replace('"', "&quot;").replace("'", "&#x27;"))


def humanize(snake: str) -> str:
    """Mirror of InteractiveDutyPresenter.Humanize — keep the two in step."""
    if not snake:
        return "Proceed"
    return " ".join(p[:1].upper() + p[1:] if p else p for p in snake.split("_"))


def parse_duty_copy() -> dict[str, tuple[str, str]]:
    """Lift the DutyCopy dictionary out of the C# source."""
    src = io.open(PRESENTER, encoding="utf-8").read()
    block = re.search(r"DutyCopy\s*=\s*new\(StringComparer\.Ordinal\)\s*\{(.*?)\n    \};", src, re.S)
    if not block:
        sys.exit("ERROR: could not locate the DutyCopy dictionary — did InteractiveDutyPresenter.cs change shape?")

    copy: dict[str, tuple[str, str]] = {}
    for m in re.finditer(r'\["([a-z0-9_]+)"\]\s*=\s*\("((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\)', block.group(1)):
        copy[m.group(1)] = (m.group(2), m.group(3))
    return copy


def option_keys() -> dict[str, list[str]]:
    """Per-row option keys, from the data rows the presenter actually renders."""
    data = json.loads(io.open(DUTIES_JSON, encoding="utf-8").read())
    out: dict[str, list[str]] = {}
    for section in ("interactiveDuties", "incidents"):
        for row in data.get(section, []) or []:
            row_id = row.get("id")
            if not row_id:
                continue
            keys = []
            for opt in ("optionA", "optionB"):
                spec = row.get(opt) or {}
                keys.append(spec.get("key") or "")
            out[row_id] = keys
    return out


def build_entries() -> list[tuple[str, str]]:
    copy = parse_duty_copy()
    options = option_keys()
    entries: list[tuple[str, str]] = []

    for row_id in sorted(options):
        title, body = copy.get(row_id, ("Camp Business", "Something needs attention."))
        opt_a, opt_b = options[row_id]
        entries.append((f"taom_enlist_duty_{row_id}_title", title))
        entries.append((f"taom_enlist_duty_{row_id}_body", body))
        entries.append((f"taom_enlist_duty_{row_id}_opta", humanize(opt_a)))
        entries.append((f"taom_enlist_duty_{row_id}_optb", humanize(opt_b)))
        entries.append((f"taom_enlist_duty_{row_id}_success", SUCCESS_TEXT))
        entries.append((f"taom_enlist_duty_{row_id}_failure", FAILURE_TEXT))

    missing = sorted(set(options) - set(copy))
    if missing:
        print(f"  NOTE: {len(missing)} row(s) have no DutyCopy entry and fall back to generic copy: {', '.join(missing)}")
    orphan = sorted(set(copy) - set(options))
    if orphan:
        print(f"  NOTE: {len(orphan)} DutyCopy entr(ies) have no matching data row: {', '.join(orphan)}")

    return entries


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    entries = build_entries()
    xml = io.open(STRINGS_XML, encoding="utf-8", newline="").read()
    existing = set(re.findall(r'id="([^"]+)"', xml))
    new = [(k, v) for k, v in entries if k not in existing]

    print(f"  duty/incident rows: {len(entries) // 6}")
    print(f"  keys generated:     {len(entries)}")
    print(f"  already registered: {len(entries) - len(new)}")
    print(f"  to add:             {len(new)}")

    if args.dry_run:
        for k, v in new[:8]:
            print(f"    + {k} = {v[:60]}")
        if len(new) > 8:
            print(f"    … and {len(new) - 8} more")
        print("\n  (dry run — nothing written)")
        return

    if not new:
        print("\n  Nothing to add.")
        return

    nl = "\r\n" if "\r\n" in xml else "\n"
    block = nl.join(f'\t<string id="{k}" text="{{={k}}}{xml_escape(v)}" />' for k, v in new)
    marker = re.search(r"(\s*</strings>)", xml)
    if not marker:
        sys.exit("ERROR: no </strings> close tag in taom_enlistment_strings.xml")
    xml = xml[:marker.start()] + nl + f"\t<!-- Interactive duties + incidents: generated by tools/generate_enlistment_duty_strings.py -->" + nl + block + xml[marker.start():]
    io.open(STRINGS_XML, "w", encoding="utf-8", newline="").write(xml)
    print(f"\n  Wrote {len(new)} entries to {STRINGS_XML.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
