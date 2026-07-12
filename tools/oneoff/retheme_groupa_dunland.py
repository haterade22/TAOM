# -*- coding: utf-8 -*-
"""Re-theme the 21 Group A lords from orc/Mordor to Dunland: skill_template +
equipment. Culture was already moved to Culture.empire in a prior pass; this
makes them *fully* Dunland.

Scoped to each Group A lord's <xsl:template> block (lords.xslt) and standalone
<NPCCharacter> block (characters/lords.xml for the 2 children) so no other lord
is touched.

Mapping:
  SkillSet.taom_orc_warrior_skills   -> SkillSet.taom_dunland_warrior_skills
  SkillSet.taom_orc_female_skills    -> SkillSet.taom_dunland_warrior_skills  (keep warrior-women combat-statted)
  SkillSet.taom_orc_chieftain_skills -> SkillSet.taom_dunland_raider_skills
  mordor_bat_template_medium_X       -> dunland_bat_template_medium_X
  mordor_civ_template_default_X      -> dunland_civ_template_default_X

Usage:
  python tools/retheme_groupa_dunland.py --dry-run
  python tools/retheme_groupa_dunland.py --apply
"""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "Main" / "_Module" / "ModuleData"
LORDS_XSLT = MD / "lords.xslt"
LORDS_XML = MD / "characters" / "lords.xml"

GROUP_A = [
    "lord_1_20", "lord_1_21", "lord_1_22", "lord_1_31", "lord_1_32", "lord_1_33",
    "lord_1_41", "lord_1_411", "lord_1_42", "lord_1_422", "lord_1_43",
    "lord_1_50", "lord_1_51", "lord_1_56", "lord_1_56_1", "lord_1_56_2",
    "lord_1_58", "lord_1_64", "lord_1_66", "lord_1_67", "lord_1_70",
]
CHILDREN_IN_XML = ["lord_1_56_1", "lord_1_56_2"]

REPL = [
    ("SkillSet.taom_orc_female_skills", "SkillSet.taom_dunland_warrior_skills"),
    ("SkillSet.taom_orc_warrior_skills", "SkillSet.taom_dunland_warrior_skills"),
    ("SkillSet.taom_orc_chieftain_skills", "SkillSet.taom_dunland_raider_skills"),
    ("mordor_bat_template_medium_", "dunland_bat_template_medium_"),
    ("mordor_civ_template_default_", "dunland_civ_template_default_"),
]


def apply_block(block):
    changes = []
    for old, new in REPL:
        n = block.count(old)
        if n:
            block = block.replace(old, new)
            changes.append((old, new, n))
    return block, changes


def patch_xslt(text):
    rows = []
    for lid in GROUP_A:
        m = re.search(r"(<xsl:template match=\"NPCCharacter\[@id='" + re.escape(lid) + r"'\]\">.*?</xsl:template>)", text, re.DOTALL)
        if not m:
            rows.append((lid, "lords.xslt", "NOT FOUND", 0))
            continue
        new_block, changes = apply_block(m.group(1))
        if changes:
            text = text[: m.start(1)] + new_block + text[m.end(1):]
        for old, new, n in changes:
            rows.append((lid, "lords.xslt", f"{old} -> {new}", n))
        # residue check
        if "taom_orc_" in new_block or "mordor_bat_template" in new_block or "mordor_civ_template" in new_block:
            rows.append((lid, "lords.xslt", "!! ORC/MORDOR RESIDUE REMAINS", 0))
    return text, rows


def patch_xml(text):
    rows = []
    for lid in CHILDREN_IN_XML:
        m = re.search(r'(<NPCCharacter id="' + re.escape(lid) + r'".*?</NPCCharacter>)', text, re.DOTALL)
        if not m:
            rows.append((lid, "lords.xml", "NOT FOUND", 0))
            continue
        new_block, changes = apply_block(m.group(1))
        if changes:
            text = text[: m.start(1)] + new_block + text[m.end(1):]
        for old, new, n in changes:
            rows.append((lid, "lords.xml", f"{old} -> {new}", n))
        if "taom_orc_" in new_block or "mordor_bat_template" in new_block or "mordor_civ_template" in new_block:
            rows.append((lid, "lords.xml", "!! ORC/MORDOR RESIDUE REMAINS", 0))
    return text, rows


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group(required=True)
    g.add_argument("--dry-run", action="store_true")
    g.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    xslt = LORDS_XSLT.read_text(encoding="utf-8")
    xml = LORDS_XML.read_text(encoding="utf-8")
    new_xslt, r1 = patch_xslt(xslt)
    new_xml, r2 = patch_xml(xml)

    for lid, f, msg, n in r1 + r2:
        print(f"  {lid:13s} [{f}] x{n}  {msg}")
    total = sum(n for *_, n in r1 + r2 if isinstance(n, int))
    residue = [(lid, f) for lid, f, msg, n in r1 + r2 if "RESIDUE" in msg]
    print(f"\nTotal replacements: {total}")
    if residue:
        print("!! RESIDUE in:", residue)

    if args.apply and not residue:
        LORDS_XSLT.write_text(new_xslt, encoding="utf-8")
        LORDS_XML.write_text(new_xml, encoding="utf-8")
        print("APPLIED.")
    elif residue:
        print("NOT APPLIED - residue detected.")
    else:
        print("DRY RUN - no files written.")
    return 1 if residue else 0


if __name__ == "__main__":
    sys.exit(main())
