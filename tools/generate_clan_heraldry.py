#!/usr/bin/env python3
"""Generate per-clan heraldry (color/color2) + a unique per-clan party template,
and wire default_party_template, from a per-culture spec file.

Spec: Main/_Module/ModuleData/clan_heraldry/<culture>.json
  {
    "culture": "gondor",
    "clans": [
      { "id": "clan_empire_west_1", "source": "xslt",
        "template_id": "kingdom_hero_party_gondor_dol_amroth_template",   # optional; default kingdom_hero_party_<id>_template
        "theme": "Dol Amroth — Swan Knights",
        "color": "FF1E6FA0", "color2": "FFE8EEF2",
        "roster": [ {"troop": "gondor_da_squire", "min": 3, "max": 7}, ... ] },
      ...
    ]
  }

Three idempotent operations (re-running replaces, never duplicates):
  A. characters/clans.xml      (source=="xml")  -> set color/color2/default_party_template on <Faction id=..>
  B. spclans.xslt              (source=="xslt") -> override color/color2/default_party_template (extend/create template)
  C. taom_partyTemplates.xml   (all)            -> upsert <MBPartyTemplate id="<template_id>"> with the roster

Usage:
  python tools/generate_clan_heraldry.py --spec gondor                 # dry-run
  python tools/generate_clan_heraldry.py --spec gondor --apply
  python tools/generate_clan_heraldry.py --all [--apply]
"""
import argparse
import glob
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
MD = os.path.join(ROOT, "Main", "_Module", "ModuleData")
CLANS_XML = os.path.join(MD, "characters", "clans.xml")
SPCLANS_XSLT = os.path.join(MD, "spclans.xslt")
PARTY_TMPL = os.path.join(MD, "taom_partyTemplates.xml")
SPEC_DIR = os.path.join(MD, "clan_heraldry")

TAB = "\t"


def read(path):
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        return f.read()


def write(path, text):
    with open(path + ".bak", "w", encoding="utf-8-sig", newline="") as f:
        f.write(read(path))
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        f.write(text)


def template_id_for(clan):
    return clan.get("template_id") or ("kingdom_hero_party_%s_template" % clan["id"])


# ---------- A. clans.xml ----------
def set_clansxml_attrs(text, clan_id, color, color2, dpt):
    """Set color/color2/default_party_template on the <Faction id=..> start tag
    (handles both self-closing `<Faction .../>` and `<Faction ...></Faction>`)."""
    pat = re.compile(r'(<Faction\b[^>]*?\bid="%s"[^>]*?>)' % re.escape(clan_id), re.S)
    m = pat.search(text)
    if not m:
        raise SystemExit("ERROR: <Faction id=%r> not found in clans.xml" % clan_id)
    block = m.group(1)
    # detect indentation from the id= line
    im = re.search(r'(\r?\n)([ \t]*)id="', block)
    nl, indent = (im.group(1), im.group(2)) if im else ("\n", TAB + TAB)

    def upsert(b, name, value):
        ap = re.compile(r'%s="[^"]*"' % re.escape(name))
        if ap.search(b):
            return ap.sub('%s="%s"' % (name, value), b, count=1)
        # insert a new attribute line before banner_key=, else before the closing />
        ins = '%s="%s"%s%s' % (name, value, nl, indent)
        bm = re.search(r'banner_key="', b)
        if bm:
            return b[:bm.start()] + ins + b[bm.start():]
        return re.sub(r'\s*/>\s*$', '%s%s="%s" />' % (nl + indent, name, value), b)

    new = block
    new = upsert(new, "color", color)
    new = upsert(new, "color2", color2)
    if dpt:
        new = upsert(new, "default_party_template", "PartyTemplate." + dpt)
    return text[:m.start()] + new + text[m.end():]


# ---------- B. spclans.xslt ----------
def _excludes(names):
    return " and ".join("local-name() != '%s'" % n for n in names)


def upsert_xslt_override(text, clan_id, color, color2, dpt):
    """Ensure the per-clan template overrides color/color2 (+ default_party_template if dpt given)."""
    # ordered overrides: dpt is optional (colors-only clans pass dpt=None)
    over = [("color", color), ("color2", color2)]
    if dpt:
        over.append(("default_party_template", "PartyTemplate." + dpt))
    names = [n for n, _ in over]
    tpat = re.compile(
        r'(<xsl:template\s+match="Faction\[@id=\'%s\'\]"\s*>)(.*?)(</xsl:template>)' % re.escape(clan_id),
        re.S)
    m = tpat.search(text)

    def attr_lines(indent):
        return "".join('%s<xsl:attribute name="%s">%s</xsl:attribute>\n' % (indent, n, v) for n, v in over)

    if m:
        body = m.group(2)
        indent = "      "  # 6 spaces, matches existing per-clan templates
        am = re.search(r'\n([ \t]*)<xsl:attribute', body)
        if am:
            indent = am.group(1)
        # 1) widen the @* exclusion predicate to exclude the attrs we override
        def widen(sel):
            inside = sel.group(1)
            for n in names:
                if "'%s'" % n not in inside:
                    inside = inside + " and local-name() != '%s'" % n
            return '<xsl:apply-templates select="@*[%s]"/>' % inside
        body2, n_sel = re.subn(r'<xsl:apply-templates select="@\*\[([^\]]*)\]"/>', widen, body, count=1)
        if n_sel == 0:
            # template copies all @*; replace a bare select="@*"
            body2 = re.sub(r'<xsl:apply-templates select="@\*"/>',
                           '<xsl:apply-templates select="@*[%s]"/>' % _excludes(names),
                           body, count=1)
        # 2) drop any pre-existing color/color2/dpt xsl:attribute lines (idempotent replace)
        body2 = re.sub(r'[ \t]*<xsl:attribute name="(color|color2|default_party_template)">.*?</xsl:attribute>\r?\n',
                       "", body2)
        # 3) insert our attribute block before the node() apply (or before end)
        newattrs = attr_lines(indent)
        nm = re.search(r'[ \t]*<xsl:apply-templates select="node\(\)"/>', body2)
        if nm:
            body2 = body2[:nm.start()] + newattrs + body2[nm.start():]
        else:
            body2 = body2.rstrip() + "\n" + newattrs
        return text[:m.start()] + m.group(1) + body2 + m.group(3) + text[m.end():]

    # No existing template -> create one before </xsl:stylesheet>
    block = (
        '  <xsl:template match="Faction[@id=\'%(id)s\']">\n'
        '    <xsl:copy>\n'
        '      <xsl:apply-templates select="@*[%(ex)s]"/>\n'
        '%(attrs)s'
        '      <xsl:apply-templates select="node()"/>\n'
        '    </xsl:copy>\n'
        '  </xsl:template>\n'
    ) % {"id": clan_id, "ex": _excludes(names), "attrs": attr_lines("      ")}
    return text.replace("</xsl:stylesheet>", block + "</xsl:stylesheet>", 1)


# ---------- C. taom_partyTemplates.xml ----------
def render_template(template_id, roster):
    lines = ['\t<MBPartyTemplate id="%s">' % template_id, '\t\t<stacks>']
    for s in roster:
        lines.append('\t\t\t<PartyTemplateStack min_value="%d" max_value="%d" troop="NPCCharacter.%s" />'
                     % (int(s["min"]), int(s["max"]), s["troop"]))
    lines.append('\t\t</stacks>')
    lines.append('\t</MBPartyTemplate>')
    return "\n".join(lines)


def upsert_party_template(text, template_id, roster):
    rendered = render_template(template_id, roster)
    existing = re.compile(r'\t?<MBPartyTemplate id="%s">.*?</MBPartyTemplate>' % re.escape(template_id), re.S)
    if existing.search(text):
        return existing.sub(lambda _: rendered, text, count=1)
    # insert before closing </partyTemplates>
    return text.replace("</partyTemplates>", rendered + "\n\n</partyTemplates>", 1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", help="culture spec name (without .json), e.g. gondor")
    ap.add_argument("--all", action="store_true", help="process every spec in clan_heraldry/")
    ap.add_argument("--apply", action="store_true", help="write files (+ .bak); default dry-run")
    args = ap.parse_args()

    if args.all:
        specs = sorted(glob.glob(os.path.join(SPEC_DIR, "*.json")))
    elif args.spec:
        specs = [os.path.join(SPEC_DIR, args.spec + ".json")]
    else:
        ap.error("pass --spec <culture> or --all")

    clans_text = read(CLANS_XML)
    xslt_text = read(SPCLANS_XSLT)
    party_text = read(PARTY_TMPL)

    total = 0
    for spec_path in specs:
        spec = json.load(open(spec_path, "r", encoding="utf-8"))
        print("\n== %s (%d clans) ==" % (os.path.basename(spec_path), len(spec["clans"])))
        for clan in spec["clans"]:
            roster = clan.get("roster") or []
            tid = template_id_for(clan) if roster else None
            dpt = tid  # None -> colors-only clan (no per-clan template, keeps culture fallback)
            src = clan["source"]
            print("  %-26s %-6s col=%s/%s  tmpl=%s  (%s)" % (
                clan["id"], src, clan["color"], clan["color2"], tid or "(colors-only)", clan.get("theme", "")))
            if src == "xml":
                clans_text = set_clansxml_attrs(clans_text, clan["id"], clan["color"], clan["color2"], dpt)
            elif src == "xslt":
                xslt_text = upsert_xslt_override(xslt_text, clan["id"], clan["color"], clan["color2"], dpt)
            else:
                raise SystemExit("ERROR: clan %s has unknown source %r" % (clan["id"], src))
            if roster:
                party_text = upsert_party_template(party_text, tid, roster)
            total += 1

    print("\nprocessed %d clans" % total)
    if not args.apply:
        print("DRY-RUN — re-run with --apply to write the 3 files")
        return
    write(CLANS_XML, clans_text)
    write(SPCLANS_XSLT, xslt_text)
    write(PARTY_TMPL, party_text)
    print("WROTE clans.xml, spclans.xslt, taom_partyTemplates.xml (+ .bak each)")


if __name__ == "__main__":
    main()
