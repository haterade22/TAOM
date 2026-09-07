#!/usr/bin/env python3
"""Author the `supply_caravan_template_<family>` party templates SupplyLines spawns from.

Why these exist
---------------
`SupplyCaravanService.PickCaravanTemplate` used to hand the player's supply caravan
`culture.CaravanPartyTemplates[0]`, the AI notable-caravan template. That coupling made a caravan
balance change land on a logistics feature that never asked for it: when the AI caravans were
resized for bandit parity (#543) the player's supply escort went from 20-29 troops to 60-70, and the
provisioning cost, which is linear in headcount, went with it (#549).

The two are unrelated jobs. An AI caravan has to survive a bandit warband on its own; a supply
caravan is escorted by whatever the player paid for. So SupplyLines now gets its own small templates
and the AI ones are free to move without touching it.

Sizing
------
A supply caravan hits none of `DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit`'s
bonus branches: it has no `LeaderHero`, and `SupplyCaravanComponent` derives from `PartyComponent`
so it is neither `IsCaravan` nor `IsVillager`. Its member cap is therefore the flat
`ExplainedNumber(20f)`. These templates are sized so the crew PLUS the default 10-man mercenary
escort still fits under that: 4 to 8 crew, so 14 to 18 with an escort.

(A player who raises `SupplyMercenaryGuardCount` to its 40 maximum still exceeds the cap. That was
true before these templates existed and is not something they can fix; the cap is vanilla's.)

One template per TROOP FAMILY, not per culture. Cultures share caravan rosters (Lothlorien fields
Rivendell's, Umbar and the two Harad splinters field Harad's), and the C# resolves the id from the
culture's own caravan binding rather than from a mapping table, so a re-binding carries across
automatically and cannot go stale.

Usage:
    python tools/generate_supply_caravan_templates.py            # dry-run (default)
    python tools/generate_supply_caravan_templates.py --apply
"""

import argparse
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO / "Main" / "_Module" / "ModuleData"
TARGET_FILE = MODULE_DATA / "taom_partyTemplates.xml"
CULTURES_XML = MODULE_DATA / "taom_spcultures.xml"
CULTURES_XSLT = MODULE_DATA / "spcultures.xslt"

# Crew only. The escort is whatever the player bought.
CREW = (
    ("armed_trader", 3, 5),
    ("caravan_guard", 1, 3),
)

BLOCK_START = "\t<!-- ==== SupplyLines crew templates (generated: tools/generate_supply_caravan_templates.py) ==== -->"
BLOCK_END = "\t<!-- ==== end SupplyLines crew templates ==== -->"

TEMPLATE_RE = re.compile(
    r'<MBPartyTemplate id="(caravan_template_[a-z_]+)">(.*?)</MBPartyTemplate>', re.S)
STACK_RE = re.compile(r'troop="NPCCharacter\.([a-z_0-9]+)"')


def bound_caravan_families(text_xml, text_xslt):
    """Every `caravan_template_<family>` a culture actually binds, custom XML and XSLT alike.

    Reading the bindings rather than globbing the templates means an unbound template is not
    given a supply sibling nobody can reach, and a culture that binds something unexpected is
    followed rather than guessed at.
    """
    families = set()
    root = ET.fromstring(text_xml.lstrip("﻿"))
    for culture in root.iter("Culture"):
        node = culture.find("caravan_party_templates/caravan_party_template")
        if node is not None and node.get("id", "").startswith("PartyTemplate.caravan_template_"):
            families.add(node.get("id").replace("PartyTemplate.caravan_template_", ""))
    for match in re.finditer(r"Culture\[@id='[a-z_]+'\](.*?)</xsl:template>", text_xslt, re.S):
        m = re.search(r'<caravan_party_template id="PartyTemplate\.caravan_template_([a-z_]+)"',
                      match.group(1))
        if m:
            families.add(m.group(1))
    return sorted(families)


def troops_for_family(text, family):
    """The real troop ids the family's own caravan template names, so none is ever invented."""
    for tid, body in TEMPLATE_RE.findall(text):
        if tid != "caravan_template_" + family:
            continue
        found = STACK_RE.findall(body)
        out = {}
        for role, _mn, _mx in CREW:
            # Longest-first so veteran_caravan_guard_x is never mistaken for caravan_guard_x.
            for troop in sorted(found, key=len, reverse=True):
                if troop.startswith(role + "_") and not troop.startswith("veteran_" + role):
                    out.setdefault(role, troop)
                    break
        return out
    return {}


def render(families, text, eol):
    lines = [BLOCK_START]
    for family in families:
        troops = troops_for_family(text, family)
        missing = [role for role, _, _ in CREW if role not in troops]
        if missing:
            raise ValueError(
                "caravan_template_%s names no %s troop, so a supply template cannot be built from "
                "it without inventing an id" % (family, "/".join(missing)))
        lines.append('\t<MBPartyTemplate id="supply_caravan_template_%s">' % family)
        lines.append("\t\t<stacks>")
        for role, mn, mx in CREW:
            lines.append(
                '\t\t\t<PartyTemplateStack min_value="%d" max_value="%d" troop="NPCCharacter.%s" />'
                % (mn, mx, troops[role]))
        lines.append("\t\t</stacks>")
        lines.append("\t</MBPartyTemplate>")
    lines.append(BLOCK_END)
    return eol.join(lines) + eol


def rewrite(text, eol):
    """Return (new_text, families). Idempotent: an existing generated block is replaced whole."""
    families = bound_caravan_families(CULTURES_XML.read_text(encoding="utf-8-sig"),
                                      CULTURES_XSLT.read_text(encoding="utf-8-sig"))
    block = render(families, text, eol)

    start = text.find(BLOCK_START)
    if start != -1:
        end = text.find(BLOCK_END, start)
        if end == -1:
            raise ValueError("found the generated block's opening marker but not its close")
        end += len(BLOCK_END) + len(eol)
        out = text[:start] + block + text[end:]
    else:
        close = text.rindex("</partyTemplates>")
        out = text[:close] + block + text[close:]

    try:
        ET.fromstring(out.lstrip("﻿"))
    except ET.ParseError as exc:
        raise ValueError("transform produced XML that no longer parses: %s" % exc)
    return out, families


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="write changes (default: dry-run)")
    args = parser.parse_args()

    text = TARGET_FILE.read_bytes().decode("utf-8")
    eol = "\r\n" if "\r\n" in text else "\n"
    new_text, families = rewrite(text, eol)

    print("supply crew templates: %d" % len(families))
    for family in families:
        troops = troops_for_family(text, family)
        print("  supply_caravan_template_%-22s %s"
              % (family, ", ".join(troops[r] for r, _, _ in CREW)))
    crew_min = sum(mn for _, mn, _ in CREW)
    crew_max = sum(mx for _, _, mx in CREW)
    print()
    print("crew %d-%d, plus the player's escort, against the flat 20-man vanilla cap"
          % (crew_min, crew_max))
    print("changed: %s" % ("no" if new_text == text else "yes"))

    if not args.apply:
        print("DRY-RUN: re-run with --apply to write.")
        return 0

    TARGET_FILE.write_bytes(new_text.encode("utf-8"))
    print("APPLIED: %s" % TARGET_FILE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
