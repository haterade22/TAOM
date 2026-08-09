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

# Field duties need ONLY a _title: ServiceStatusTextWriter renders "You have orders: {DUTY_NAME}"
# and nothing else about a field duty is player-facing text. They were missed on the first pass
# because that pass derived its key set from `interactiveDuties` + `incidents` only, so all 13
# rendered as their raw snake_case id ("You have orders: recon_sweep") in every language.
#
# Authored rather than Humanize()d: these read after a colon, so they want a noun phrase a soldier
# would recognise as an order, not a title-cased identifier ("Recon Sweep").
FIELD_DUTY_TITLES = {
    "recon_sweep":        "a reconnaissance sweep",
    "mounted_pursuit":    "a mounted pursuit",
    "bandit_hunt":        "a bandit hunt",
    "deserter_sweep":     "a sweep for deserters",
    "road_patrol":        "a road patrol",
    "scout_route":        "to scout the road ahead",
    "recruitment_errand": "a recruiting errand",
    "trusted_dispatch":   "to carry a dispatch",
    "relief_dispatch":    "to carry word to a hard-pressed fief",
    "supply_delivery":    "a supply run",
    "forage":             "to forage for the company",
    "hideout_strike":     "to strike a hideout",
    "service_shift":      "camp duty",
}


# Field duties resolve OFF-SCREEN now — the player accepts one, a few hours pass, and this toast is
# the ONLY thing they ever see of it. Generic "It went well." would make thirteen distinct duties
# read as one. Each line is written to imply the work happened without narrating a scene.
FIELD_DUTY_RESULTS = {
    "recon_sweep":        ("You ride the line and come back with the ground mapped.",
                           "You lose the light before you lose the ground, and come back with little."),
    "mounted_pursuit":    ("You run them down before they reach the treeline.",
                           "They scatter into broken country and the horses will not follow."),
    "bandit_hunt":        ("The camp is cold ash by the time you leave it.",
                           "You find the camp abandoned, warm — they were warned."),
    "deserter_sweep":     ("You bring them back to the column. Most of them walking.",
                           "They know the country better than you do, and use it."),
    "road_patrol":        ("The road is quiet behind you, and stays quiet.",
                           "You ride the road twice and still miss whatever was on it."),
    "scout_route":        ("The way ahead is clear, and you can say why.",
                           "You come back with guesses. The captain wanted certainties."),
    "recruitment_errand": ("Three names, and two of them can already hold a spear.",
                           "The village has given all the sons it intends to."),
    "trusted_dispatch":   ("The dispatch is in the right hands, and no others.",
                           "The dispatch arrives late, and you cannot swear it arrived unread."),
    "relief_dispatch":    ("Word reaches them in time to matter.",
                           "You reach them. The news reached them first."),
    "supply_delivery":    ("The wagons are lighter and the stores are fuller.",
                           "Half the load is spoiled by the time it is counted."),
    "forage":             ("You come back heavier than you left.",
                           "The country has been picked over, and recently."),
    "hideout_strike":     ("They never formed up. It was over in the dark.",
                           "The approach goes wrong early and you pull back with what you brought."),
    "service_shift":      ("Your shift passes without incident.",
                           "You are found asleep at your post. It is noted."),
}

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

    # Field duties: title only. Driven off the JSON rows so a new row fails loudly here rather
    # than shipping as a raw id, which is exactly how the first 13 escaped.
    data = json.loads(io.open(DUTIES_JSON, encoding="utf-8").read())
    field_ids = [r.get("id") for r in (data.get("fieldDuties") or []) if r.get("id")]
    unauthored = [i for i in field_ids if i not in FIELD_DUTY_TITLES]
    if unauthored:
        sys.exit(
            "ERROR: field duty rows with no authored title: "
            + ", ".join(unauthored)
            + "\n       Add them to FIELD_DUTY_TITLES — an unauthored row renders as its raw id in all 12 languages."
        )
    missing_results = [i for i in field_ids if i not in FIELD_DUTY_RESULTS]
    if missing_results:
        sys.exit(
            "ERROR: field duty rows with no authored success/failure text: "
            + ", ".join(missing_results)
            + "\n       Add them to FIELD_DUTY_RESULTS — the toast is the ONLY thing the player"
            + " sees of a duty now, so an unauthored row ships the generic fallback in 12 languages."
        )
    for row_id in sorted(field_ids):
        entries.append((f"taom_enlist_duty_{row_id}_title", FIELD_DUTY_TITLES[row_id]))
        ok, bad = FIELD_DUTY_RESULTS[row_id]
        entries.append((f"taom_enlist_duty_{row_id}_success", ok))
        entries.append((f"taom_enlist_duty_{row_id}_failure", bad))

    stale = sorted(set(FIELD_DUTY_TITLES) - set(field_ids))
    if stale:
        print(f"  NOTE: {len(stale)} authored field-duty title(s) have no matching row: {', '.join(stale)}")

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
    # newline="" IS LOAD-BEARING on both the read and the write below, and `utf-8` (not
    # `utf-8-sig`) is deliberate. Together they make this a byte-faithful round-trip:
    # newline="" disables newline translation in both directions so CRLF survives, and plain
    # utf-8 decodes a BOM to a literal U+FEFF and re-encodes it unchanged rather than eating it.
    # Delete either kwarg as apparent boilerplate and a 13-line insert silently becomes a
    # whole-file rewrite with every line ending changed. See tools/README.md "XML I/O convention".
    xml = io.open(STRINGS_XML, encoding="utf-8", newline="").read()
    # Anchored to `<string id="` on purpose. A bare `id="([^"]+)"` would also match an id
    # mentioned inside an XML comment and silently treat that key as already registered,
    # so the generator would skip writing it and nothing would report the omission.
    existing = set(re.findall(r'<string\s+id="([^"]+)"', xml))
    new = [(k, v) for k, v in entries if k not in existing]

    print(f"  interactive/incident rows: {sum(1 for k, _ in entries if k.endswith('_body'))}")
    print(f"  field-duty rows:           {sum(1 for k, _ in entries if k.endswith('_title')) - sum(1 for k, _ in entries if k.endswith('_body'))}")
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
