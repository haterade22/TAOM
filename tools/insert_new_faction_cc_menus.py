#!/usr/bin/env python3
"""Clone the gundabad character-creation narrative menu entries for the new orc cultures
(goblin, mistymountainorcs). Without these, the CC Family/Youth/Adulthood/Education stages render
BLANK when the player picks one of the new cultures: NarrativeMenuBuilder filters entries by
`culture_id == selectedCulture.StringId`, and the new cultures had zero entries (RCA 2026-06-02).

Childhood is culture-INDEPENDENT (entries have no culture_id) — shared across all cultures — so it
needs no per-culture entries. parents/youth/adulthood/education are culture-keyed (6 gundabad entries
each).

Inline text/description are plain English (the existing entries are not {=key}-localized), so no loc
registration is needed; the gundabad-specific flavor is remapped to each culture's wording.

TEXTUAL append (not json.dump) so the existing entries stay byte-for-byte identical (clean diff) and
the file's CRLF / inline-array style is preserved. Idempotent: skips a file that already contains the
new-culture entries (revert + re-run to regenerate).

Run: python tools/insert_new_faction_cc_menus.py
"""
import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CC = os.path.join(ROOT, "Main", "_Module", "ModuleData", "charactercreation")
MENUS = ["parents", "youth", "adulthood", "education"]
SRC = "gundabad"
NEW = ["goblin", "mistymountainorcs"]

# Per-culture display-text remap of the gundabad flavor (longest phrase FIRST so substrings don't
# double-substitute). goblin = Goblin-town in the High Pass; mistymountainorcs = the orc-host of the
# Misty Mountains / Moria. "Orc" left intact (orcs + goblins coexist in lore).
SUBS = {
    "goblin": [
        ("Warg riders of Gundabad", "warg-packs of the High Pass"),
        ("war-halls of Mount Gundabad", "tunnel-halls of Goblin-town"),
        ("Mount Gundabad", "the High Pass"),
        ("forges of Gundabad", "warrens of Goblin-town"),
        ("garrison of Gundabad", "warrens of Goblin-town"),
        ("Nobles of Gundabad", "Chiefs of Goblin-town"),
        ("Smiths of Gundabad", "Tinkerers of Goblin-town"),
        ("Gundabad had smiths", "Goblin-town had tinkerers"),
        ("of Gundabad", "of Goblin-town"),
        ("Gundabad", "Goblin-town"),
    ],
    "mistymountainorcs": [
        ("Warg riders of Gundabad", "warg-riders of the Misty Mountains"),
        ("war-halls of Mount Gundabad", "war-halls of Moria"),
        ("Mount Gundabad", "the Misty Mountains"),
        ("forges of Gundabad", "deep forges of Moria"),
        ("garrison of Gundabad", "garrison of Moria"),
        ("Nobles of Gundabad", "Warlords of the Misty Mountains"),
        ("Smiths of Gundabad", "Smiths of Moria"),
        ("Gundabad had smiths", "Moria had smiths"),
        ("of Gundabad", "of the Misty Mountains"),
        ("Gundabad", "the Misty Mountains"),
    ],
}
FORBIDDEN = ("Gundabad", "Pale Uruk", "pale orc", "Pale Orc")


def remap(s, culture):
    if not isinstance(s, str):
        return s
    for old, new in SUBS[culture]:
        s = s.replace(old, new)
    return s


def clone_entry(entry, culture):
    out = {}
    for k, v in entry.items():
        if k == "string_id":
            out[k] = v.replace(SRC, culture)
        elif k == "culture_id":
            out[k] = culture
        elif k in ("text", "description"):
            out[k] = remap(v, culture)
        else:
            out[k] = v
    return out


def fmt_entry(d, nl):
    """Serialize one entry to match the existing style: 4-space '{'/'}' indent, 8-space keys,
    each value via json.dumps so arrays stay inline (["a", "b"])."""
    items = list(d.items())
    lines = ["    {"]
    for i, (k, v) in enumerate(items):
        comma = "," if i < len(items) - 1 else ""
        lines.append("        " + json.dumps(k) + ": " + json.dumps(v, ensure_ascii=False) + comma)
    lines.append("    }")
    return nl.join(lines)


def main():
    for menu in MENUS:
        path = os.path.join(CC, menu + "_menu.json")
        raw = open(path, encoding="utf-8", newline="").read()
        if any(f'"culture_id": "{c}"' in raw for c in NEW):
            print(f"  {menu}_menu.json: new-culture entries already present — skipping (revert to regen)")
            continue
        nl = "\r\n" if "\r\n" in raw else "\n"
        data = json.loads(raw)
        src = [e for e in data if e.get("culture_id") == SRC]
        if not src:
            print(f"  WARNING {menu}: no '{SRC}' entries to clone")
            continue
        blocks = [fmt_entry(clone_entry(e, c), nl) for c in NEW for e in src]
        # residual guard: no source-culture leftover in the new entries
        bad = [b for b in blocks if any(w in b for w in FORBIDDEN)]
        if bad:
            raise RuntimeError(f"{menu}: source-culture leftover survived remap in {len(bad)} entrie(s)")
        # textual splice: insert before the closing ']' (comma after the existing last entry)
        idx = raw.rfind("]")
        before = raw[:idx].rstrip()          # ends at the last entry's '}'
        after = raw[idx:]                     # ']' + trailing newline
        new_text = before + "," + nl + (("," + nl).join(blocks)) + nl + after
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(new_text)
        json.loads(open(path, encoding="utf-8").read())  # validate still parses
        print(f"  {menu}_menu.json: +{len(blocks)} entries ({len(src)} per culture x {len(NEW)}); valid JSON, no leftovers")
    print("CC menus done.")


if __name__ == "__main__":
    main()
