#!/usr/bin/env python3
"""Build the authoritative TAOM clan registry: every clan that exists in-game
after spclans.xslt runs, with its culture, super-faction (kingdom), source file,
and current color/color2/default_party_template.

Sources:
  - TAOM  characters/clans.xml          (TAOM-authored noble + bandit clans)
  - vanilla SandBox/ModuleData/spclans.xml (the base spclans.xslt transforms)
  - TAOM  spclans.xslt                  (which vanilla clans are renamed/removed + culture overrides)

Output: prints a per-culture summary + writes docs/reviews/_clan_registry.json (scratch).

Usage:  python tools/clan_registry.py [--json PATH]
"""
import argparse
import json
import os
import re
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CLANS_XML = os.path.join(ROOT, "Main", "_Module", "ModuleData", "characters", "clans.xml")
SPCLANS_XSLT = os.path.join(ROOT, "Main", "_Module", "ModuleData", "spclans.xslt")
VANILLA_SPCLANS = r"E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/SandBox/ModuleData/spclans.xml"

ATTRS = ("culture", "super_faction", "color", "color2", "default_party_template",
         "is_bandit", "is_minor_faction", "is_clan_type_mercenary", "is_outlaw")


def faction_rows(xml_path):
    """Yield dicts for each <Faction> in a well-formed clan XML file."""
    tree = ET.parse(xml_path)
    for fac in tree.getroot().iter("Faction"):
        if fac.get("id") is None:
            continue
        row = {"id": fac.get("id")}
        for a in ATTRS:
            row[a] = fac.get(a)
        yield row


def parse_xslt(xslt_path):
    """Return (overrides, removed): overrides[id] = {culture?, default_party_template?},
    removed = set of Faction ids deleted by an empty template."""
    with open(xslt_path, "r", encoding="utf-8-sig") as f:
        text = f.read()
    overrides, removed = {}, set()
    # Each per-clan template: <xsl:template match="Faction[@id='X']"> ... </xsl:template>
    # or self-closing empty removal: <xsl:template match="Faction[@id='X']"/>
    for m in re.finditer(r'<xsl:template\s+match="Faction\[@id=\'([^\']+)\'\]"\s*(/?)>', text):
        cid, selfclose = m.group(1), m.group(2)
        if selfclose == "/":
            removed.add(cid)
            continue
        # find the body until the closing </xsl:template>
        body = text[m.end():]
        end = body.find("</xsl:template>")
        body = body[:end] if end >= 0 else body
        ov = {}
        cm = re.search(r'<xsl:attribute\s+name="culture">Culture\.([^<]+)</xsl:attribute>', body)
        if cm:
            ov["culture"] = cm.group(1).strip()
        dm = re.search(r'<xsl:attribute\s+name="default_party_template">PartyTemplate\.([^<]+)</xsl:attribute>', body)
        if dm:
            ov["default_party_template"] = dm.group(1).strip()
        overrides[cid] = ov
    return overrides, removed


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", default=os.path.join(ROOT, "docs", "reviews", "_clan_registry.json"))
    args = ap.parse_args()

    overrides, removed = parse_xslt(SPCLANS_XSLT)

    registry = []  # consolidated

    # 1) TAOM-authored clans (clans.xml) — these win, source=xml
    taom_ids = set()
    for row in faction_rows(CLANS_XML):
        row["source"] = "xml"
        registry.append(row)
        taom_ids.add(row["id"])

    # 2) vanilla clans, possibly renamed/removed by xslt
    for row in faction_rows(VANILLA_SPCLANS):
        cid = row["id"]
        if cid in removed:
            continue  # XSLT deletes it (the 5 hideout-bandit clans)
        if cid in taom_ids:
            continue  # already TAOM-authored
        ov = overrides.get(cid)
        if ov is not None:
            row["source"] = "xslt"
            if ov.get("culture"):
                row["culture"] = ov["culture"]  # effective culture after override
            if ov.get("default_party_template"):
                row["default_party_template"] = ov["default_party_template"]
        else:
            row["source"] = "vanilla-passthrough"
        registry.append(row)

    # group by culture
    by_culture = {}
    for r in registry:
        by_culture.setdefault(r.get("culture") or "(none)", []).append(r)

    print("TAOM CLAN REGISTRY  (clans existing in-game after spclans.xslt)\n")
    print("  total clans: %d   (xml=%d, xslt=%d, vanilla-passthrough=%d)" % (
        len(registry),
        sum(1 for r in registry if r["source"] == "xml"),
        sum(1 for r in registry if r["source"] == "xslt"),
        sum(1 for r in registry if r["source"] == "vanilla-passthrough"),
    ))
    print("  vanilla clans removed by xslt: %s\n" % ", ".join(sorted(removed)))

    print("  %-18s %4s  %4s %4s  %4s  detail" % ("culture", "cnt", "col", "col2", "dpt"))
    for cul in sorted(by_culture):
        rows = by_culture[cul]
        n_col = sum(1 for r in rows if r.get("color"))
        n_dpt = sum(1 for r in rows if r.get("default_party_template"))
        print("  %-18s %4d  %4d %4d  %4d  %s" % (
            cul, len(rows), n_col, n_col, n_dpt,
            ", ".join(sorted(r["id"] for r in rows))[:90],
        ))

    os.makedirs(os.path.dirname(args.json), exist_ok=True)
    with open(args.json, "w", encoding="utf-8") as f:
        json.dump({"clans": registry, "removed": sorted(removed)}, f, indent=2)
    print("\n  wrote %s" % args.json)


if __name__ == "__main__":
    main()
