# -*- coding: utf-8 -*-
"""Scout: dump skill_template + equipment for Group A lords AND genuine-Dunland
reference lords, from lords.xslt (templates) + characters/lords.xml (lines).

Read-only. Used to build the orc/Mordor -> Dunland re-theme mapping.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MD = ROOT / "Main" / "_Module" / "ModuleData"
XSLT = (MD / "lords.xslt").read_text(encoding="utf-8")
XML = (MD / "characters" / "lords.xml").read_text(encoding="utf-8")

GROUP_A = [
    "lord_1_20", "lord_1_21", "lord_1_22", "lord_1_31", "lord_1_32", "lord_1_33",
    "lord_1_41", "lord_1_411", "lord_1_42", "lord_1_422", "lord_1_43",
    "lord_1_50", "lord_1_51", "lord_1_56", "lord_1_56_1", "lord_1_56_2",
    "lord_1_58", "lord_1_64", "lord_1_66", "lord_1_67", "lord_1_70",
]
# Genuine Dunland (empire) reference lords for the target loadout vocabulary.
REFERENCE = [
    "lord_1_1", "lord_1_2", "lord_1_3", "lord_1_4", "lord_1_5", "lord_1_6",
    "lord_NE7_u", "lord_NE8_l", "lord_NE8_s", "lord_NE8_c1", "lord_NE8_c2",
    "lord_NE9_l", "lord_NE9_s", "lord_NE9_d",
]


def from_xslt(lid):
    m = re.search(r"<xsl:template match=\"NPCCharacter\[@id='" + re.escape(lid) + r"'\]\">(.*?)</xsl:template>", XSLT, re.DOTALL)
    if not m:
        return None
    b = m.group(1)
    name = re.search(r"name=\"name\">(?:\{=[^}]+\})?([^<]+)</", b)
    skill = re.search(r"name=\"skill_template\">([^<]+)</", b)
    grp = re.search(r"name=\"default_group\">([^<]+)</", b)
    fem = 'name="is_female">true' in b
    race = re.search(r"name=\"race\">([^<]+)</", b)
    cult = re.search(r"name=\"culture\">([^<]+)</", b)
    eqs = re.findall(r'<EquipmentSet id="([^"]+)"(?:\s+equipmentType="([^"]+)")?\s*/>', b)
    return dict(src="xslt", name=name.group(1).strip() if name else "?", skill=skill.group(1) if skill else "-",
                group=grp.group(1) if grp else "-", female=fem, race=race.group(1) if race else "human",
                culture=cult.group(1) if cult else "?", eq=eqs)


def from_xml(lid):
    m = re.search(r'(<NPCCharacter id="' + re.escape(lid) + r'".*?</NPCCharacter>)', XML, re.DOTALL)
    if not m:
        return None
    b = m.group(1)
    head = b.split(">", 1)[0]
    name = re.search(r'name="(?:\{=[^}]+\})?([^"]+)"', head)
    skill = re.search(r'skill_template="SkillSet\.([^"]+)"', head)
    grp = re.search(r'default_group="([^"]+)"', head)
    race = re.search(r'race="([^"]+)"', head)
    cult = re.search(r'culture="([^"]+)"', head)
    eqs = re.findall(r'<EquipmentSet id="([^"]+)"(?:\s+equipmentType="([^"]+)")?\s*/>', b)
    return dict(src="xml ", name=name.group(1).strip() if name else "?", skill=skill.group(1) if skill else "-",
                group=grp.group(1) if grp else "-", female='is_female="true"' in head,
                race=race.group(1) if race else "human", culture=cult.group(1) if cult else "?", eq=eqs)


def dump(title, ids):
    print(f"\n===== {title} =====")
    for lid in ids:
        x = from_xslt(lid)
        y = from_xml(lid)
        for src in (x, y):
            if not src:
                continue
            eqs = ", ".join(f"{e[0]}{'[civ]' if e[1] else ''}" for e in src["eq"]) or "(none)"
            fem = "F" if src["female"] else "M"
            print(f"  [{src['src']}] {lid:13s} {fem} grp={src['group']:10s} cult={src['culture']:14s} race={src['race']:6s} skill={src['skill']}")
            print(f"              name={src['name']!r}  EQ: {eqs}")


dump("GROUP A (to re-theme -> Dunland)", GROUP_A)
dump("REFERENCE: genuine Dunland lords", REFERENCE)
