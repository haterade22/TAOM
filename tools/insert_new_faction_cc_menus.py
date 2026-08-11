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
NEW = ["goblin", "mistymountainorcs", "bluecraig", "lindon"]

# Which culture each new one clones its narrative options from. The first two were carved out of
# Gundabad; Blue Craig is carved out of Goblin-town and Lindon out of Rivendell, so they clone the
# cultures they were promoted from rather than the original orc source.
SRC_FOR = {
    "goblin": "gundabad",
    "mistymountainorcs": "gundabad",
    "bluecraig": "goblin",
    "lindon": "rivendell",
}

# Words that must not survive the remap, per clone source. A leftover here is the defect the
# new-factions RCA recorded: an id rename is case-sensitive and leaves player-facing text naming
# the source faction.
FORBIDDEN_FOR = {
    "gundabad": ("Gundabad", "Pale Uruk", "pale orc", "Pale Orc"),
    "goblin": ("Goblin-town", "High Pass"),
    "rivendell": ("Rivendell", "Imladris"),
}

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
    # Blue Craig: western goblins of the Ered Luin, cut off from their Goblin-town kin by the whole
    # width of Eriador. Their neighbours are the Dwarves of the Blue Mountains and the Elves of
    # Mithlond, so the flavour moves west even though the people do not change.
    "bluecraig": [
        ("warg-packs of the High Pass", "warg-packs of the Blue Mountains"),
        ("tunnel-halls of Goblin-town", "crag-halls of Blue Craig"),
        ("warrens of Goblin-town", "warrens of Blue Craig"),
        ("Chiefs of Goblin-town", "Chiefs of Blue Craig"),
        ("Tinkerers of Goblin-town", "Tinkerers of Blue Craig"),
        ("Goblin-town had tinkerers", "Blue Craig had tinkerers"),
        ("of Goblin-town", "of Blue Craig"),
        ("the High Pass", "the Ered Luin"),
        ("Goblin-town", "Blue Craig"),
    ],
    # Lindon: Círdan's Falathrim at the Grey Havens. Imladris is a hidden valley of loremasters;
    # Mithlond is a haven of shipwrights, so the imagery moves from the vale to the sea.
    "lindon": [
        ("hidden valley of Imladris", "grey havens of Mithlond"),
        ("Last Homely House", "Grey Havens"),
        ("valley of Imladris", "firth of Lune"),
        ("house of Elrond", "quays of Círdan"),
        ("of Imladris", "of Mithlond"),
        ("of Rivendell", "of Lindon"),
        ("Imladris", "Mithlond"),
        ("Rivendell", "Lindon"),
    ],
}


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
            out[k] = v.replace(SRC_FOR[culture], culture)
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
        nl = "\r\n" if "\r\n" in raw else "\n"
        data = json.loads(raw)

        # Skip per CULTURE, not per file. A single file-level guard meant that once Goblin-town had
        # its entries the whole file was considered done, so Blue Craig and Lindon could never be
        # added to it — the check has to ask the question separately for each culture.
        blocks = []
        for c in NEW:
            if f'"culture_id": "{c}"' in raw:
                print(f"  {menu}_menu.json: {c} already present — skipping")
                continue
            src_culture = SRC_FOR[c]
            src = [e for e in data if e.get("culture_id") == src_culture]
            if not src:
                # A source that is ITSELF new this run is a different failure from a source that
                # does not exist, and it must not be a warning. Blue Craig clones Goblin-town, which
                # this same run may be minting: the generated entries live in `blocks` until the
                # splice at the end, so a naive read of `data` finds nothing, prints a warning,
                # exits 0, and reports success — leaving Blue Craig with zero narrative options and
                # therefore blank CC stages, which is the exact bug this script exists to prevent.
                # `data.extend(cloned)` below closes that hole; this is the backstop if the ordering
                # in NEW is ever changed so a source comes after its dependent.
                if src_culture in NEW:
                    raise SystemExit(
                        f"{menu}/{c}: clone source '{src_culture}' is also new this run and has not "
                        f"been generated yet — order NEW so a source precedes its dependents")
                print(f"  WARNING {menu}: no '{src_culture}' entries to clone for {c}")
                continue
            cloned = [clone_entry(e, c) for e in src]
            made = [fmt_entry(d, nl) for d in cloned]
            bad = [b for b in made if any(w in b for w in FORBIDDEN_FOR[src_culture])]
            if bad:
                raise RuntimeError(
                    f"{menu}/{c}: source-culture leftover survived remap in {len(bad)} entrie(s)")
            blocks += made
            # Make this culture's entries visible to later cultures in the same run, so a chained
            # source (gundabad -> goblin -> bluecraig) resolves in ONE invocation instead of
            # silently requiring two.
            data.extend(cloned)

        if not blocks:
            continue
        # textual splice: insert before the closing ']' (comma after the existing last entry)
        idx = raw.rfind("]")
        before = raw[:idx].rstrip()          # ends at the last entry's '}'
        after = raw[idx:]                     # ']' + trailing newline
        new_text = before + "," + nl + (("," + nl).join(blocks)) + nl + after
        with open(path, "w", encoding="utf-8", newline="") as f:
            f.write(new_text)
        json.loads(open(path, encoding="utf-8").read())  # validate still parses
        print(f"  {menu}_menu.json: +{len(blocks)} entries; valid JSON, no leftovers")
    print("CC menus done.")


if __name__ == "__main__":
    main()
