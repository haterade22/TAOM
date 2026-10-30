#!/usr/bin/env python3
"""Tests for tools/generate_career_forks.py (#769 C1).

Run:  python -m unittest discover -s tools/tests -t .

Pure stdlib. Every file operation happens in a temporary directory: the shipped career XML is never
read for writing here. The fixtures are built twice, CRLF and LF, because the two shipped career files
have differed in line endings before, and once with a BOM, to pin the byte-faithful I/O the tool owes
(tools/README.md "XML I/O convention").

The C# sets the generator reads (PassiveEffectType, AttackTypeMask, PassiveEffectConsumers.Consumed,
ClassFilteredTypes, MaskGatedTypes, CareerPerkConsumerMap.IsFlat) come from a synthetic CareerSystem
folder of tiny .cs files, and the special resource data from a synthetic special_resources folder, so
the tests do not move when the engine side or the shipped numbers do. The classes that end in
"Shipped" read the real files, read-only, to pin the rules against the real C#.
"""
import contextlib
import copy
import io
import itertools
import json
import os
import random
import re
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from unittest import mock

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import generate_career_forks as gcf  # noqa: E402

TOOLS = Path(__file__).resolve().parents[1]
REPO = TOOLS.parent
REAL_SYSTEM_DIR = REPO / "Main" / "Features" / "CareerSystem"
REAL_RESOURCE_DIR = REPO / "Main" / "_Module" / "ModuleData" / "special_resources"
REAL_CAREER_DIR = REPO / "Main" / "_Module" / "ModuleData" / "career_system"

# A self-contained rule set with round numbers, so the tests do not move when the maintainer tunes
# the shipped draft numbers in tools/career_design/_rules.json.
TEST_RULES = {
    "banned": {"StealthBonus": "test ban", "Special": "sentinel", "BuffDuration": "reserved",
               "ShrugOff": "on/off"},
    "archetypes": ["infantry", "ranged", "cavalry"],
    "slot_tiers": [1, 1, 2, 2, 3, 3],
    "tier_budget": {"1": 3, "2": 4, "3": 5},
    "budget_tolerance": 0.5,
    "fork_spread_tolerance": 0.5,
    "class_bonus": 1.5,
    "root_budget": 1.5,
    "troop_classes": ["Infantry", "Ranged", "Cavalry"],
    "lower_is_better": ["TroopWages", "RecruitCost", "SpecialResourceUpkeepModifier",
                        "SpecialResourceUpgradeCostModifier"],
    "mask_forbidden": ["TroopDamage", "TroopResistance"],
    "resource_types": ["SpecialResourceGain", "SpecialResourceUpkeepModifier",
                       "SpecialResourceUpgradeCostModifier"],
    "upkeep_types": ["SpecialResourceUpkeepModifier"],
    "mutation_properties": ["Duration", "Radius", "CooldownReduction"],
    "mutation_calculators": ["flat"],
    "mutation_operations": ["Add"],
    "vu_unit": {"Damage": 0.05, "ArmorPenetration": 0.05, "Health": 10, "RenownGain": 0.1,
                "TroopDamage": 0.05, "TroopResistance": 0.05, "PartySize": 12.5,
                "SpecialResourceGain": 0.25, "SpecialResourceUpkeepModifier": 0.25,
                "SpecialResourceUpgradeCostModifier": 0.25, "Resistance": 0.05, "TroopWages": 0.1,
                "RecruitCost": 0.1, "TroopFearResistance": 0.15, "TroopMorale": 0.05,
                "DailyGold": 20, "TroopTraining": 2},
    "caps": {
        "Damage+ArmorPenetration": {"types": ["Damage", "ArmorPenetration"], "limit": 0.45},
        "TroopDamage+TroopResistance": {"types": ["TroopDamage", "TroopResistance"], "limit": 0.20},
        "PartySize": {"types": ["PartySize"], "limit": 75},
        "SpecialResourceGain": {"types": ["SpecialResourceGain"], "limit": 1.0},
        "RecruitCost": {"types": ["RecruitCost"], "limit": 0.5},
        # every priced type needs a group (check_rules); these limits are far out of the tests' way
        "Health": {"types": ["Health"], "limit": 1000},
        "RenownGain": {"types": ["RenownGain"], "limit": 100},
        "SpecialResourceUpkeepModifier": {"types": ["SpecialResourceUpkeepModifier"], "limit": 100},
        "SpecialResourceUpgradeCostModifier": {"types": ["SpecialResourceUpgradeCostModifier"], "limit": 100},
        "Resistance": {"types": ["Resistance"], "limit": 100},
        "TroopWages": {"types": ["TroopWages"], "limit": 100},
        "TroopFearResistance": {"types": ["TroopFearResistance"], "limit": 100},
        "TroopMorale": {"types": ["TroopMorale"], "limit": 100},
        "DailyGold": {"types": ["DailyGold"], "limit": 10000},
        "TroopTraining": {"types": ["TroopTraining"], "limit": 1000},
    },
}

TIER_BUDGET = {1: 3, 2: 4, 3: 5}
SLOT_TIERS = [1, 1, 2, 2, 3, 3]

DAMAGE_CAP = "Damage+ArmorPenetration"


# --------------------------------------------------------------------------- #
# Synthetic CareerSystem folder                                                #
# --------------------------------------------------------------------------- #
ENUM_CS = """namespace TAOM.Features.CareerSystem.Domain;

public enum PassiveEffectType
{
    Special,
    Health,
    Damage,
    Resistance,
    ArmorPenetration,
    Ammo,
    ShrugOff,
    StealthBonus,   // a comment, Imaginary, Names
    BuffDuration,
    PartySize,
    TroopDamage,
    TroopResistance,
    TroopFearResistance,
    TroopMorale,
    TroopWages,
    RenownGain,
    RecruitCost,
    DailyGold,
    TroopTraining,
    SpecialResourceGain,
    SpecialResourceUpkeepModifier,
    SpecialResourceUpgradeCostModifier,
    Unwired
}
"""

MASK_CS = """using System;
[Flags]
public enum AttackTypeMask
{
    None = 0,
    Melee = 1,
    Ranged = 2,
    All = Melee | Ranged,
    Cut = 4,
    Pierce = 8,
    Blunt = 16,
}
"""

DEFAULT_CONSUMED = ["Health", "Damage", "Resistance", "ArmorPenetration", "Ammo", "ShrugOff", "StealthBonus",
                    "PartySize", "TroopDamage", "TroopResistance", "TroopFearResistance", "TroopMorale",
                    "TroopWages", "RenownGain", "RecruitCost", "DailyGold", "TroopTraining",
                    "SpecialResourceGain", "SpecialResourceUpkeepModifier", "SpecialResourceUpgradeCostModifier"]
DEFAULT_CLASS_FILTERED = ["TroopDamage", "TroopResistance", "TroopFearResistance", "TroopTraining"]
DEFAULT_MASK_GATED = ["Damage", "Resistance", "TroopDamage", "TroopResistance"]
DEFAULT_FLAT = ["Health", "PartySize", "DailyGold", "TroopTraining"]


def type_set(name, members, comment=""):
    rows = "".join(f"        PassiveEffectType.{m},   // {m} consumer\n" for m in members)
    return (f"    private static readonly HashSet<PassiveEffectType> {name} = new HashSet<PassiveEffectType>\n"
            f"    {{\n{comment}{rows}    }};\n\n")


def consumers_cs(consumed=None, class_filtered=None, mask_gated=None, skip=()):
    consumed = DEFAULT_CONSUMED if consumed is None else consumed
    class_filtered = DEFAULT_CLASS_FILTERED if class_filtered is None else class_filtered
    mask_gated = DEFAULT_MASK_GATED if mask_gated is None else mask_gated
    parts = ["using System.Collections.Generic;\n\n",
             "// PassiveEffectType.Unwired is mentioned in prose and must not count.\n",
             "/* PassiveEffectType.Special either */\n",
             "public static class PassiveEffectConsumers\n{\n"]
    if "Consumed" not in skip:
        parts.append(type_set("Consumed", consumed, "        // PassiveEffectType.Unwired,  (commented out)\n"))
    if "ClassFilteredTypes" not in skip:
        parts.append(type_set("ClassFilteredTypes", class_filtered))
    if "MaskGatedTypes" not in skip:
        parts.append(type_set("MaskGatedTypes", mask_gated))
    parts.append("    public static bool IsConsumed(PassiveEffectType t) => Consumed.Contains(t);\n}\n")
    return "".join(parts)


def map_cs(flat=None, block_body=False, skip=False):
    flat = DEFAULT_FLAT if flat is None else flat
    cond = "\n        || ".join(f"type == PassiveEffectType.{m}" for m in flat)
    if skip:
        method = "    public static bool IsHalf(PassiveEffectType type) => false;\n"
    elif block_body:
        method = f"    public static bool IsFlat(PassiveEffectType type)\n    {{\n        return {cond};\n    }}\n"
    else:
        method = f"    public static bool IsFlat(PassiveEffectType type)\n        => {cond};\n"
    return ("public static class CareerPerkConsumerMap\n{\n    // see http://example.com/not-a-comment-start\n"
            + method
            + '    public static string Describe(PassiveEffectType type) => "a // inside /* a string */";\n}\n')


DEFAULT_ARCHETYPES = {"alpha_career": "Infantry", "beta_career": "Ranged", "gamma_career": "Infantry"}


def ioc_cs(archetypes=None, body=None):
    """A CareerSystemIoC.cs shaped like the real one: a static field, a getter, and the expression-bodied
    BuildCareerArchetypeMap with a dictionary initialiser, comments, and a disabled (commented-out) entry."""
    archetypes = DEFAULT_ARCHETYPES if archetypes is None else archetypes
    if body is None:
        rows = "".join(f'        ["{cid}"]   = CareerArchetype.{name},\n' for cid, name in archetypes.items())
        body = ("        // === ALPHA === CareerArchetype.Cavalry named in prose\n" + rows
                + '        // DISABLED: ["far_halftroll"] = CareerArchetype.Infantry,\n')
    return ("using System.Collections.Generic;\n\npublic static class CareerSystemIoC\n{\n"
            "    private static readonly IReadOnlyDictionary<string, CareerArchetype> CareerArchetypeMap = "
            "BuildCareerArchetypeMap();\n\n"
            "    internal static IReadOnlyDictionary<string, CareerArchetype> GetCareerArchetypeMap() => "
            "CareerArchetypeMap;\n\n"
            "    private static IReadOnlyDictionary<string, CareerArchetype> BuildCareerArchetypeMap() => "
            "new Dictionary<string, CareerArchetype>\n    {\n" + body + "    };\n}\n")


def write_system_dir(root, enum_cs=ENUM_CS, mask_cs=MASK_CS, consumers=None, flat_cs=None, drop=(), ioc=None):
    root = Path(root)
    (root / "Domain").mkdir(parents=True, exist_ok=True)
    (root / "Diagnostics").mkdir(parents=True, exist_ok=True)
    files = {"Domain/PassiveEffectType.cs": enum_cs, "Domain/AttackTypeMask.cs": mask_cs,
             "Domain/PassiveEffectConsumers.cs": consumers_cs() if consumers is None else consumers,
             "Diagnostics/CareerPerkConsumerMap.cs": map_cs() if flat_cs is None else flat_cs,
             "CareerSystemIoC.cs": ioc_cs() if ioc is None else ioc}
    for rel, text in files.items():
        if rel not in drop:
            (root / rel).write_text(text, encoding="utf-8")
    return root


# --------------------------------------------------------------------------- #
# Fixture XML                                                                  #
# --------------------------------------------------------------------------- #
def career_block(cid, cultures, tree_version=None):
    cultures = [cultures] if isinstance(cultures, str) else cultures
    version = f'\n          tree_version="{tree_version}"' if tree_version is not None else ""
    rows = "".join(f'      <Culture id="{c}" />\n' for c in cultures)
    return (
        f'  <Career id="{cid}" display_name="{{=taom_career_{cid}}}{cid} > name"\n'
        f'          description="{{=taom_career_{cid}_desc}}text"\n'
        f'          ability_template_id="{cid}_ability"\n'
        f'          min_clan_tier="0"\n'
        f'          root_choice_id="{cid}_root"{version}\n'
        f'          keystone_icon="1">\n'
        f'    <EligibleCultures>\n'
        f'{rows}'
        f'    </EligibleCultures>\n'
        f'    <ChoiceGroups>\n'
        f'      <Group id="{cid}_t1_a" />\n'
        f'      <Group id="{cid}_t1_b" />\n'
        f'      <Group id="{cid}_t2_a" />\n'
        f'    </ChoiceGroups>\n'
        f'  </Career>\n')


def choices_span(cid):
    """The root choice and three groups of one career, with comments between: the span first
    adoption must replace and nothing else."""
    return (
        f'  <Choice id="{cid}_root" group_id="" type="Passive"\n'
        f'          description="{{=taom_{cid}_root_desc}}old root"\n'
        f'          icon_sprite="career_choice_{cid}_root">\n'
        f'    <PassiveEffect type="Damage" magnitude="0.05" operation="Add" is_percentage="true" />\n'
        f'  </Choice>\n'
        f'\n'
        f'  <!-- T1-A: old -->\n'
        f'  <ChoiceGroup id="{cid}_t1_a" display_name="{{=taom_career_grp_{cid}_t1_a}}Old A" career_id="{cid}" tier="1">\n'
        f'    <Choice id="{cid}_t1_a_key" type="Keystone"\n'
        f'            description="{{=taom_{cid}_t1a_key}}old key"\n'
        f'            icon_sprite="career_choice_{cid}_t1a_key">\n'
        f'      <Mutations>\n'
        f'        <Mutation target_id="{cid}_ability" property="Radius" calculator="flat" value="3" operation="Add" />\n'
        f'      </Mutations>\n'
        f'    </Choice>\n'
        f'  </ChoiceGroup>\n'
        f'\n'
        f'  <!-- T1-B: old -->\n'
        f'  <ChoiceGroup id="{cid}_t1_b" display_name="{{=taom_career_grp_{cid}_t1_b}}Old B" career_id="{cid}" tier="1">\n'
        f'    <Choice id="{cid}_t1_b_p1" type="Passive"\n'
        f'            description="{{=taom_{cid}_t1b_p1}}old"\n'
        f'            icon_sprite="career_choice_{cid}_t1b_p1">\n'
        f'      <PassiveEffect type="Health" magnitude="5" operation="Add" />\n'
        f'    </Choice>\n'
        f'  </ChoiceGroup>\n'
        f'\n'
        f'  <ChoiceGroup id="{cid}_t2_a" display_name="{{=taom_career_grp_{cid}_t2_a}}Old C" career_id="{cid}" tier="2">\n'
        f'    <Choice id="{cid}_t2_a_p1" type="Passive"\n'
        f'            description="{{=taom_{cid}_t2a_p1}}old"\n'
        f'            icon_sprite="career_choice_{cid}_t2a_p1">\n'
        f'      <PassiveEffect type="Health" magnitude="8" operation="Add" />\n'
        f'    </Choice>\n'
        f'  </ChoiceGroup>')


def banner(cid):
    return f'  <!-- banner {cid} -->\n\n'


DEFAULT_RESOURCES = [("gold_g", [], ["gammaland"])]
DEFAULT_TROOPS = [("g_troop", "gold_g", 0.1), ("g_merchant", "gold_g", None)]


class FixtureFiles:
    """The two career files in a temp dir, plus a design dir holding the test rules and the spec, a
    synthetic CareerSystem folder and a synthetic special_resources folder."""

    def __init__(self, test, newline="\n", bom=False, alpha_cultures="alphaland", alpha_version=None):
        self.test = test
        self.nl = newline
        self.bom = bom
        self.root = Path(tempfile.mkdtemp(prefix="gcf-test-"))
        test.addCleanup(self._cleanup)
        self.career_dir = self.root / "career_system"
        self.design_dir = self.root / "design"
        self.system_dir = self.root / "CareerSystem"
        self.resource_dir = self.root / "special_resources"
        self.troops_dir = self.root / "troops"
        self.characters_dir = self.root / "characters"
        self.languages_dir = self.root / "Languages"
        for d in (self.career_dir, self.design_dir, self.resource_dir, self.troops_dir, self.characters_dir):
            d.mkdir()
        write_system_dir(self.system_dir)
        self.write_language_rows({"FR": [], "DE": []})
        self.careers_path = self.career_dir / "taom_careers.xml"
        self.choices_path = self.career_dir / "taom_career_choices.xml"
        self.out = self.root / "table.md"

        self.careers_head = '<?xml version="1.0" encoding="utf-8"?>\n<Careers max_perk_points="30">\n\n'
        self.careers_tail = '\n</Careers>\n'
        self.choices_head = '<?xml version="1.0" encoding="utf-8"?>\n<CareerChoices>\n\n'
        self.choices_tail = '\n</CareerChoices>\n'
        self.careers_text = (self.careers_head
                             + career_block("alpha_career", alpha_cultures, alpha_version) + "\n"
                             + career_block("beta_career", "alphaland") + "\n"
                             + career_block("gamma_career", "gammaland")
                             + self.careers_tail)
        self.choices_text = (self.choices_head
                             + banner("alpha_career") + choices_span("alpha_career") + "\n\n"
                             + banner("beta_career") + choices_span("beta_career") + "\n\n"
                             + banner("gamma_career") + choices_span("gamma_career")
                             + self.choices_tail)
        self.write_files()
        self.write_json(self.design_dir / "_rules.json", TEST_RULES)
        self.write_resources(DEFAULT_RESOURCES, DEFAULT_TROOPS)

    def _cleanup(self):
        import shutil
        shutil.rmtree(self.root, ignore_errors=True)

    def encode(self, text):
        prefix = b"\xef\xbb\xbf" if self.bom else b""
        return prefix + text.replace("\n", self.nl).encode("utf-8")

    def write_files(self):
        self.careers_path.write_bytes(self.encode(self.careers_text))
        self.choices_path.write_bytes(self.encode(self.choices_text))

    def write_json(self, path, obj):
        path.write_text(json.dumps(obj, indent=2), encoding="utf-8")

    def write_language_rows(self, rows_by_language):
        """Languages/<lang>/std_taom_career_data_strings_<lang>.xml, one file per language, holding the given
        string ids. A language of [] gets a file with no row."""
        for lang, keys in rows_by_language.items():
            folder = self.languages_dir / lang
            folder.mkdir(parents=True, exist_ok=True)
            rows = "".join(f'    <string id="{k}" text="old {k}" />\n' for k in keys)
            (folder / f"std_taom_career_data_strings_{lang}.xml").write_text(
                '<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" type="string">\n  <strings>\n'
                f'{rows}  </strings>\n</base>\n', encoding="utf-8")

    def write_spec(self, spec, name="alpha"):
        self.write_json(self.design_dir / f"{name}.json", spec)

    def write_resources(self, resources, troops):
        """resources: (id, kingdom ids, culture ids); troops: (troop id, resource id, daily_upkeep or None
        [, culture]). The NPCCharacter row of each troop goes to troops/troops_fixture.xml with the given
        culture, else the first culture (or kingdom) id of its resource; a culture of False writes no
        NPCCharacter row at all, so the troop is "found nowhere"."""
        rows = ["<SpecialResources>"]
        owner = {}
        for rid, kingdoms, cultures in resources:
            rows.append(f'  <Resource id="{rid}" display_name="{rid}" cap="100">')
            rows += [f'    <Kingdom id="{k}" />' for k in kingdoms]
            rows += [f'    <Culture id="{c}" />' for c in cultures]
            rows.append("  </Resource>")
            owner.setdefault(rid, (list(cultures) + list(kingdoms) or [None])[0])
        rows.append("</SpecialResources>")
        (self.resource_dir / "special_resources_config.xml").write_text("\n".join(rows), encoding="utf-8")
        trows = ["<TroopResourceCosts>"]
        npcs = ["<NPCCharacters>"]
        for troop in troops:
            tid, rid, upkeep = troop[:3]
            culture = troop[3] if len(troop) > 3 else owner.get(rid)
            extra = f' daily_upkeep="{upkeep}"' if upkeep is not None else ' merchant_cost="5"'
            trows.append(f'  <Troop id="{tid}" resource_id="{rid}"{extra} />')
            if culture is not False:
                npcs.append(f'  <NPCCharacter id="{tid}" culture="Culture.{culture}" />')
        trows.append("</TroopResourceCosts>")
        npcs.append("</NPCCharacters>")
        (self.resource_dir / "troop_resource_costs.xml").write_text("\n".join(trows), encoding="utf-8")
        (self.troops_dir / "troops_fixture.xml").write_text("\n".join(npcs), encoding="utf-8")

    def lf(self, path):
        """The file as LF text without a BOM, for assertions that ignore the line-ending style."""
        return path.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")

    def snapshot(self):
        return self.careers_path.read_bytes(), self.choices_path.read_bytes()

    def temp_files(self):
        return sorted(p.name for p in self.career_dir.iterdir() if p.name.endswith(gcf.TEMP_SUFFIX))

    def args(self, *extra, name="alpha"):
        return [name, "--career-dir", str(self.career_dir), "--design-dir", str(self.design_dir),
                "--system-dir", str(self.system_dir), "--resources-dir", str(self.resource_dir),
                "--languages-dir", str(self.languages_dir), *extra]

    def run(self, *extra, name="alpha"):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = gcf.main(self.args(*extra, name=name))
        return code, out.getvalue() + err.getvalue()


# --------------------------------------------------------------------------- #
# Fixture spec                                                                 #
# --------------------------------------------------------------------------- #
def fork(slot, extra_options=None):
    tier = SLOT_TIERS[slot - 1]
    budget = TIER_BUDGET[tier]
    options = [
        {"name": "Vitality", "description": f"+{10 * budget} max health.",
         "effects": [{"type": "Health", "magnitude": 10 * budget}]},
        {"name": "Renown", "description": f"+{budget * 10}% renown.",
         "effects": [{"type": "RenownGain", "magnitude": 0.1 * budget}]},
    ]
    options.extend(extra_options or [])
    return {"slot": slot, "name": f"Fork {slot}", "max_picks": 1, "options": options}


def damage_option(tier):
    return {"name": "Fury", "description": "More damage.",
            "effects": [{"type": "Damage", "magnitude": 0.05 * TIER_BUDGET[tier],
                         "attack_type_mask": "Melee"}]}


def keystone_option():
    return {"name": "Wide Ambush", "description": "Radius up.", "vu": 3,
            "mutations": [{"property": "Radius", "calculator": "flat", "value": 3, "operation": "Add"}]}


def career_spec(cid, archetype="infantry"):
    forks = [fork(1, [damage_option(1)]), fork(2, [keystone_option()])]
    forks += [fork(slot) for slot in range(3, 7)]
    return {"id": cid, "archetype": archetype, "tree_version": 2, "point_levels": [2, 4, 6, 8, 10, 12],
            "root": {"description": "Root grants melee damage.",
                     "effects": [{"type": "Damage", "magnitude": 0.05, "attack_type_mask": "Melee"}]},
            "forks": forks}


def good_spec():
    return {"kingdom": "Alphaland", "culture_ids": ["alphaland"], "careers": [career_spec("alpha_career")]}


def two_career_spec():
    spec = good_spec()
    spec["careers"].append(career_spec("beta_career", "ranged"))
    return spec


def gamma_spec(effects, fork_slot=3, option=0):
    """A one-career spec for gamma_career (gammaland resolves to gold_g in the default fixture)."""
    career = career_spec("gamma_career")
    career["forks"][fork_slot - 1]["options"][option]["effects"] = effects
    return {"kingdom": "Gammaland", "culture_ids": ["gammaland"], "careers": [career]}


def career_infos(fix):
    return gcf.load_career_info(fix.careers_path.read_bytes().decode("utf-8-sig"))


def errors_for(fix, spec, rules=TEST_RULES):
    return gcf.validate_spec(spec, rules, career_infos(fix), gcf.load_cs_sets(fix.system_dir),
                             gcf.load_resource_data(fix.resource_dir))


def has(errors, *needles):
    return any(all(n in e for n in needles) for e in errors)


REGION_CHOICES = re.compile(r"[ \t]*<!-- career-forks:begin (\w+) sha256=[0-9a-f]{64}[^>]*? -->\n"
                            r".*?<!-- career-forks:end \1 -->", re.S)


def edit_bytes(path, old, new, count=1):
    raw = path.read_bytes()
    assert old in raw, old
    path.write_bytes(raw.replace(old, new, count))


def rewrite_careers_marker(fix, change):
    """Rewrite the recorded attributes of alpha_career's begin marker in taom_careers.xml and sign the region
    again, as a generator with another definition would have written it (change maps the attribute text, a
    string such as " tree_version=2 point_levels=... shape=v2-...", to its replacement)."""
    text = fix.lf(fix.careers_path)
    region = gcf.find_region(text, "alpha_career", "taom_careers.xml")
    extra = change(region.extra)
    begin = (f"{region.indent}<!-- career-forks:begin alpha_career "
             f"sha256={gcf.region_hash(extra, region.body)}{extra} -->\n")
    fix.careers_path.write_bytes(fix.encode(text[:region.start] + begin + text[region.body_start:]))


# --------------------------------------------------------------------------- #
# Byte-faithful splice                                                         #
# --------------------------------------------------------------------------- #
class SpliceTests(unittest.TestCase):
    STYLES = [("\n", False), ("\r\n", False), ("\n", True), ("\r\n", True)]

    def test_first_adoption_replaces_exactly_the_careers_span(self):
        for nl, bom in self.STYLES:
            with self.subTest(newline=repr(nl), bom=bom):
                fix = FixtureFiles(self, nl, bom)
                fix.write_spec(two_career_spec())
                before_choices = fix.lf(fix.choices_path)
                code, out = fix.run("--apply")
                self.assertEqual(code, 0, out)
                after = fix.lf(fix.choices_path)

                alpha_old = choices_span("alpha_career")
                beta_old = choices_span("beta_career")
                pre, rest = before_choices.split(alpha_old)
                mid, post = rest.split(beta_old)
                regions = [m.group(0) for m in REGION_CHOICES.finditer(after)]
                self.assertEqual(len(regions), 2)
                self.assertEqual(after, pre + regions[0] + mid + regions[1] + post)
                self.assertNotIn(alpha_old, after)
                self.assertNotIn(beta_old, after)
                self.assertIn(choices_span("gamma_career"), after)
                self.assertIn("gamma_career_t1_a", after)

    def test_careers_xml_changes_only_the_group_list_and_two_attributes(self):
        for nl, bom in self.STYLES:
            with self.subTest(newline=repr(nl), bom=bom):
                fix = FixtureFiles(self, nl, bom)
                fix.write_spec(two_career_spec())
                original = fix.lf(fix.careers_path)
                code, out = fix.run("--apply")
                self.assertEqual(code, 0, out)
                after = fix.lf(fix.careers_path)
                self.assertIn('tree_version="2"', after)
                self.assertIn('point_levels="2,4,6,8,10,12"', after)

                restored = re.sub(r'\n +tree_version="\d+" point_levels="[\d,]+"', "", after)
                for cid in ("alpha_career", "beta_career"):
                    old_groups = re.search(rf'  <Career id="{cid}".*?(    <ChoiceGroups>.*?</ChoiceGroups>\n)',
                                           original, re.S).group(1)
                    restored = re.sub(
                        rf'(?s)    <!-- career-forks:begin {cid} [^\n]*-->\n.*?    <!-- career-forks:end {cid} -->\n',
                        lambda _m, g=old_groups: g, restored, count=1)
                self.assertEqual(restored, original)

    def test_line_endings_and_bom_survive(self):
        for nl, bom in self.STYLES:
            with self.subTest(newline=repr(nl), bom=bom):
                fix = FixtureFiles(self, nl, bom)
                fix.write_spec(good_spec())
                self.assertEqual(fix.run("--apply")[0], 0)
                for path in (fix.careers_path, fix.choices_path):
                    raw = path.read_bytes()
                    self.assertEqual(raw.startswith(b"\xef\xbb\xbf"), bom)
                    self.assertEqual(raw.count(b"\xef\xbb\xbf"), 1 if bom else 0)
                    if nl == "\r\n":
                        self.assertEqual(raw.count(b"\n"), raw.count(b"\r\n"))
                    else:
                        self.assertNotIn(b"\r", raw)

    def test_output_reparses_and_carries_the_generated_tree(self):
        fix = FixtureFiles(self, "\r\n", True)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        careers = ET.fromstring(fix.careers_path.read_bytes())
        alpha = [c for c in careers.iter("Career") if c.get("id") == "alpha_career"][0]
        self.assertEqual(alpha.get("tree_version"), "2")
        self.assertEqual(alpha.get("point_levels"), "2,4,6,8,10,12")
        self.assertEqual([g.get("id") for g in alpha.find("ChoiceGroups")],
                         [f"alpha_career_f{n}" for n in range(1, 7)])
        choices = ET.fromstring(fix.choices_path.read_bytes())
        root = [c for c in choices.findall("Choice") if c.get("id") == "alpha_career_root"][0]
        self.assertEqual(root.get("description"), "{=taom_cf_alpha_career_root}Root grants melee damage.")
        f1 = [g for g in choices.findall("ChoiceGroup") if g.get("id") == "alpha_career_f1"][0]
        self.assertEqual(f1.get("max_picks"), "1")
        self.assertEqual(f1.get("tier"), "1")
        self.assertEqual(f1.get("career_id"), "alpha_career")
        self.assertEqual(f1.get("display_name"), "{=taom_career_grp_alpha_career_f1}Fork 1")
        self.assertEqual([c.get("id") for c in f1.findall("Choice")],
                         ["alpha_career_f1_a", "alpha_career_f1_b", "alpha_career_f1_c"])
        c = f1.findall("Choice")[2]
        pe = c.find("PassiveEffect")
        self.assertEqual((pe.get("type"), pe.get("magnitude"), pe.get("attack_type_mask")),
                         ("Damage", "0.15", "Melee"))
        health = f1.findall("Choice")[0].find("PassiveEffect")
        self.assertEqual(health.get("magnitude"), "30")
        f2 = [g for g in choices.findall("ChoiceGroup") if g.get("id") == "alpha_career_f2"][0]
        key = f2.findall("Choice")[2]
        self.assertEqual(key.get("type"), "Keystone")
        mut = key.find("Mutations").find("Mutation")
        self.assertEqual((mut.get("target_id"), mut.get("property"), mut.get("calculator"),
                          mut.get("value"), mut.get("operation")),
                         ("alpha_career_ability", "Radius", "flat", "3", "Add"))

    def test_dead_attributes_are_no_longer_written(self):
        fix = FixtureFiles(self, "\n")
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        region = "\n".join(m.group(0) for m in REGION_CHOICES.finditer(fix.lf(fix.choices_path)))
        for dead in ('operation="Add"', "is_percentage", "icon_sprite", 'group_id=""'):
            with self.subTest(attribute=dead):
                # the Mutation rows keep their operation attribute, the loader reads that one
                stripped = re.sub(r"<Mutation [^>]*>", "", region)
                self.assertNotIn(dead, stripped)
        self.assertIn('operation="Add"', region)

    def test_second_run_is_a_noop(self):
        for nl in ("\n", "\r\n"):
            with self.subTest(newline=repr(nl)):
                fix = FixtureFiles(self, nl)
                fix.write_spec(two_career_spec())
                self.assertEqual(fix.run("--apply")[0], 0)
                first = fix.snapshot()
                code, out = fix.run("--apply")
                self.assertEqual(code, 0, out)
                self.assertEqual(fix.snapshot(), first)
                self.assertIn("unchanged", out)

    def test_spec_change_rewrites_only_that_region(self):
        fix = FixtureFiles(self, "\r\n")
        fix.write_spec(two_career_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        first = fix.lf(fix.choices_path)
        spec = two_career_spec()
        spec["careers"][0]["forks"][2]["options"][0]["description"] = "Changed text."
        fix.write_spec(spec)
        self.assertEqual(fix.run("--apply")[0], 0)
        second = fix.lf(fix.choices_path)
        self.assertNotEqual(first, second)
        self.assertIn("Changed text.", second)
        beta_before = [m.group(0) for m in REGION_CHOICES.finditer(first)][1]
        beta_after = [m.group(0) for m in REGION_CHOICES.finditer(second)][1]
        self.assertEqual(beta_before, beta_after)

    def test_dry_run_writes_nothing(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run()
        self.assertEqual(code, 0, out)
        self.assertEqual(fix.snapshot(), before)
        self.assertIn("dry run", out.lower())
        self.assertEqual(fix.temp_files(), [])

    def test_hand_edit_inside_the_choices_region_is_refused(self):
        fix = FixtureFiles(self, "\r\n")
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        edit_bytes(fix.choices_path, b"{=taom_cf_alpha_career_f1_a}", b"{=taom_cf_alpha_career_f1_a}EDIT ")
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("hand-edited", out)
        self.assertIn("alpha_career", out)
        self.assertIn("taom_career_choices.xml", out)
        self.assertEqual(fix.snapshot(), before)

    def test_hand_edit_inside_the_careers_region_is_refused(self):
        fix = FixtureFiles(self, "\n")
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        edit_bytes(fix.careers_path, b'<Group id="alpha_career_f6" />', b'<Group id="alpha_career_f7" />')
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("hand-edited", out)
        self.assertIn("taom_careers.xml", out)

    def test_changed_recorded_attribute_is_refused(self):
        fix = FixtureFiles(self, "\r\n")
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        edit_bytes(fix.careers_path, b'tree_version="2"', b'tree_version="3"')
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("tree_version", out)
        self.assertIn("hand-edited", out)
        self.assertIn("alpha_career", out)
        self.assertEqual(fix.snapshot(), before)

    def test_a_changed_point_levels_attribute_is_refused_too(self):
        fix = FixtureFiles(self, "\n")
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        edit_bytes(fix.careers_path, b'point_levels="2,4,6,8,10,12"', b'point_levels="2,4,6,8,10,13"')
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("point_levels", out)

    def test_a_noncontiguous_span_is_refused(self):
        fix = FixtureFiles(self, "\r\n")
        foreign = '  <Choice id="foreign_x" type="Passive" description="{=k}x" />\n'
        fix.choices_text = fix.choices_text.replace(
            '  <!-- T1-B: old -->\n  <ChoiceGroup id="alpha_career_t1_b"',
            foreign + '  <!-- T1-B: old -->\n  <ChoiceGroup id="alpha_career_t1_b"')
        fix.write_files()
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("not contiguous", out)
        self.assertIn("alpha_career", out)
        self.assertEqual(fix.snapshot(), before)

    def test_a_missing_old_group_is_refused_not_guessed(self):
        fix = FixtureFiles(self)
        fix.choices_text = fix.choices_text.replace('<ChoiceGroup id="alpha_career_t2_a"',
                                                    '<ChoiceGroup id="renamed_elsewhere"')
        fix.write_files()
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("alpha_career_t2_a", out)

    def test_mixed_line_endings_are_refused(self):
        fix = FixtureFiles(self, "\r\n")
        raw = fix.choices_path.read_bytes()
        fix.choices_path.write_bytes(raw.replace(b"\r\n", b"\n", 1))
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("mixed", out)

    def test_invalid_utf8_is_refused_naming_the_file(self):
        fix = FixtureFiles(self)
        fix.choices_path.write_bytes(fix.choices_path.read_bytes().replace(b"old root", b"old \xff\xfe root"))
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("taom_career_choices.xml", out)
        self.assertIn("UTF-8", out)

    def test_ensure_parses_rejects_a_malformed_document(self):
        with self.assertRaises(gcf.GeneratorError):
            gcf.ensure_parses("<a><b></a>", "x.xml")
        gcf.ensure_parses('<?xml version="1.0" encoding="utf-8"?>\n<a/>\n', "x.xml")

    def test_a_control_character_in_a_description_is_a_spec_error(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["root"]["description"] = "bad \x01 char"
        self.assertTrue(has(errors_for(fix, spec), "control character"))

    def test_descriptions_are_escaped_for_an_attribute(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["root"]["description"] = 'Tom & "Jerry" <win>'
        fix.write_spec(spec)
        self.assertEqual(fix.run("--apply")[0], 0)
        choices = ET.fromstring(fix.choices_path.read_bytes())
        root = [c for c in choices.findall("Choice") if c.get("id") == "alpha_career_root"][0]
        self.assertEqual(root.get("description"), '{=taom_cf_alpha_career_root}Tom & "Jerry" <win>')

    def test_braces_in_a_description_or_name_are_refused(self):
        fix = FixtureFiles(self)
        for where, mutate in (
                ("root description", lambda s: s["careers"][0]["root"].__setitem__("description", "Gain {GOLD} now.")),
                ("option description", lambda s: s["careers"][0]["forks"][2]["options"][0].__setitem__("description", "x }")),
                ("option name", lambda s: s["careers"][0]["forks"][2]["options"][0].__setitem__("name", "{=k}Name")),
                ("fork name", lambda s: s["careers"][0]["forks"][2].__setitem__("name", "Fork {1}"))):
            with self.subTest(where=where):
                spec = good_spec()
                mutate(spec)
                self.assertTrue(has(errors_for(fix, spec), "brace"), where)


class AtomicWriteTests(unittest.TestCase):
    def test_the_temp_suffix_is_not_an_xml_file(self):
        self.assertTrue(gcf.TEMP_SUFFIX.endswith("gcf-new"))
        self.assertFalse(("a.xml" + gcf.TEMP_SUFFIX).endswith(".xml"))

    def test_a_successful_apply_leaves_no_temp_file(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        self.assertEqual(fix.temp_files(), [])

    def test_a_failure_writing_the_second_file_leaves_both_originals_and_no_temp(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        before = fix.snapshot()
        real = Path.write_bytes
        calls = []

        def flaky(self_path, data):
            calls.append(self_path.name)
            if len(calls) == 2:
                raise OSError("disk full")
            return real(self_path, data)

        with mock.patch.object(Path, "write_bytes", flaky):
            code, out = fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("disk full", out)
        self.assertEqual(fix.snapshot(), before)
        self.assertEqual(fix.temp_files(), [])

    def test_g3_a_failure_on_the_second_rename_puts_the_first_file_back(self):
        for nl, bom in (("\n", False), ("\r\n", True)):
            with self.subTest(newline=repr(nl), bom=bom):
                fix = FixtureFiles(self, nl, bom)
                fix.write_spec(good_spec())
                before = fix.snapshot()
                real = os.replace
                targets = []

                def flaky(src, dst):
                    targets.append(Path(dst).name)
                    if len(targets) == 2:
                        raise OSError("rename blocked")
                    return real(src, dst)

                with mock.patch.object(os, "replace", flaky):
                    code, out = fix.run("--apply")
                self.assertEqual(len(targets), 2, targets)
                self.assertEqual(code, 2, out)
                self.assertIn("rename blocked", out)
                self.assertNotIn("git checkout", out)
                self.assertEqual(fix.snapshot(), before)
                self.assertEqual(fix.temp_files(), [])

    def test_g3_a_failed_restore_is_reported_by_name(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        real_replace, real_write = os.replace, Path.write_bytes
        replaced, writes = [], []

        def flaky_replace(src, dst):
            replaced.append(Path(dst).name)
            if len(replaced) == 2:
                raise OSError("rename blocked")
            return real_replace(src, dst)

        def flaky_write(self_path, data):
            writes.append(self_path.name)
            if len(writes) == 3:          # two temp files, then the restore
                raise OSError("restore blocked")
            return real_write(self_path, data)

        with mock.patch.object(os, "replace", flaky_replace), mock.patch.object(Path, "write_bytes", flaky_write):
            code, out = fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("restore blocked", out)
        self.assertIn(replaced[0], out)
        self.assertNotIn("git checkout", out)
        self.assertEqual(fix.temp_files(), [])
        self.assertIn("Restore taom_careers.xml", out)

    def test_the_replace_step_runs_only_after_both_temp_files_exist(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        seen = []
        real = os.replace

        def spy(src, dst):
            seen.append(fix.temp_files())
            return real(src, dst)

        with mock.patch.object(os, "replace", spy):
            self.assertEqual(fix.run("--apply")[0], 0)
        self.assertEqual(len(seen[0]), 2, seen)


class CheckModeTests(unittest.TestCase):
    def test_check_reports_drift_then_match(self):
        fix = FixtureFiles(self, "\r\n")
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run("--check")
        self.assertEqual(code, 1, out)
        self.assertEqual(fix.snapshot(), before)
        self.assertEqual(fix.run("--apply")[0], 0)
        code, out = fix.run("--check")
        self.assertEqual(code, 0, out)

    def test_check_flags_a_changed_spec(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        fix.run("--apply")
        spec = good_spec()
        spec["careers"][0]["root"]["description"] = "Different."
        fix.write_spec(spec)
        before = fix.snapshot()
        self.assertEqual(fix.run("--check")[0], 1)
        self.assertEqual(fix.snapshot(), before)

    def test_check_treats_a_hand_edit_as_drift(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        fix.run("--apply")
        edit_bytes(fix.choices_path, b"Fork 1", b"Fork One")
        code, out = fix.run("--check")
        self.assertEqual(code, 1, out)
        self.assertIn("hand-edited", out)

    def test_check_with_an_invalid_spec_is_an_error_not_drift(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["tree_version"] = 1
        fix.write_spec(spec)
        self.assertEqual(fix.run("--check")[0], 2)


# --------------------------------------------------------------------------- #
# Validation, one test per rule                                                #
# --------------------------------------------------------------------------- #
class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.fix = FixtureFiles(self)

    def errors(self, mutate):
        spec = copy.deepcopy(good_spec())
        mutate(spec)
        return errors_for(self.fix, spec)

    def test_the_good_spec_has_no_errors(self):
        self.assertEqual(errors_for(self.fix, good_spec()), [])

    # Rule 1: schema
    def test_rule1_missing_required_field(self):
        errs = self.errors(lambda s: s["careers"][0].pop("archetype"))
        self.assertTrue(has(errs, "archetype", "required"))

    def test_rule1_wrong_field_type(self):
        errs = self.errors(lambda s: s.__setitem__("kingdom", ["no"]))
        self.assertTrue(has(errs, "kingdom"))

    def test_rule1_unknown_key_is_a_typo_not_ignored(self):
        errs = self.errors(lambda s: s["careers"][0].__setitem__("tree_verson", 2))
        self.assertTrue(has(errs, "tree_verson", "unknown"))

    def test_rule1_the_dropped_spec_fields_are_unknown_keys(self):
        for where, mutate, key in (
                ("resource", lambda s: s.__setitem__("resource", "Mithril"), "resource"),
                ("upkeep_troops", lambda s: s.__setitem__("upkeep_troops", True), "upkeep_troops"),
                ("tier", lambda s: s["careers"][0]["forks"][0].__setitem__("tier", 1), "tier"),
                ("is_percentage", lambda s: s["careers"][0]["root"]["effects"][0].__setitem__("is_percentage", True),
                 "is_percentage")):
            with self.subTest(field=where):
                self.assertTrue(has(self.errors(mutate), key, "unknown"))

    def test_rule1_bad_archetype(self):
        errs = self.errors(lambda s: s["careers"][0].__setitem__("archetype", "mage"))
        self.assertTrue(has(errs, "archetype", "mage"))

    def test_rule1_six_forks_exactly(self):
        errs = self.errors(lambda s: s["careers"][0]["forks"].pop())
        self.assertTrue(has(errs, "6 forks"))

    def test_rule1_each_slot_once(self):
        def mutate(s):
            s["careers"][0]["forks"][5]["slot"] = 5
        self.assertTrue(has(self.errors(mutate), "slot"))

    def test_rule1_two_or_three_options(self):
        def one(s):
            s["careers"][0]["forks"][2]["options"].pop()
        def four(s):
            s["careers"][0]["forks"][2]["options"] += [copy.deepcopy(s["careers"][0]["forks"][2]["options"][0])] * 2
        self.assertTrue(has(self.errors(one), "2 or 3 options"))
        self.assertTrue(has(self.errors(four), "2 or 3 options"))

    def test_rule1_at_most_two_effects_per_option(self):
        def mutate(s):
            opt = s["careers"][0]["forks"][2]["options"][0]
            opt["effects"] = [{"type": "Health", "magnitude": 10}] * 3
        self.assertTrue(has(self.errors(mutate), "at most 2 effects"))

    def test_rule1_keystone_property_must_be_in_the_rules(self):
        def mutate(s):
            s["careers"][0]["forks"][1]["options"][2]["mutations"][0]["property"] = "Wobble"
        self.assertTrue(has(self.errors(mutate), "Wobble", "property"))

    def test_rule1_keystone_needs_an_explicit_vu(self):
        def mutate(s):
            s["careers"][0]["forks"][1]["options"][2].pop("vu")
        self.assertTrue(has(self.errors(mutate), "vu", "keystone"))

    def test_rule1_option_cannot_be_both_passive_and_keystone(self):
        def mutate(s):
            s["careers"][0]["forks"][1]["options"][2]["effects"] = [{"type": "Health", "magnitude": 30}]
        self.assertTrue(has(self.errors(mutate), "both"))

    def test_rule1_max_picks_is_bounded_by_the_option_count(self):
        def mutate(s):
            s["careers"][0]["forks"][0]["max_picks"] = 4
        self.assertTrue(has(self.errors(mutate), "max_picks"))

    def test_rule1_a_keystone_value_of_zero_or_less_is_refused(self):
        for value in (0, -3, -0.5):
            with self.subTest(value=value):
                def mutate(s, v=value):
                    s["careers"][0]["forks"][1]["options"][2]["mutations"][0]["value"] = v
                self.assertTrue(has(self.errors(mutate), "mutation value", "greater than 0"))

    def test_rule1_the_root_needs_one_or_two_effects(self):
        def none(s):
            s["careers"][0]["root"]["effects"] = []
        def three(s):
            s["careers"][0]["root"]["effects"] = [{"type": "Health", "magnitude": 5}] * 3
        def two(s):
            s["careers"][0]["root"]["effects"] = [{"type": "Health", "magnitude": 5},
                                                  {"type": "Damage", "magnitude": 0.05}]
        self.assertTrue(has(self.errors(none), "root", "at least one effect"))
        self.assertTrue(has(self.errors(three), "root", "at most 2"))
        self.assertFalse(has(self.errors(two), "root"))

    # Rule 2: career and cultures
    def test_rule2_unknown_career(self):
        errs = self.errors(lambda s: s["careers"][0].__setitem__("id", "no_such_career"))
        self.assertTrue(has(errs, "no_such_career", "not in taom_careers.xml"))

    def test_rule2_cultures_must_equal_the_careers_eligible_cultures(self):
        errs = self.errors(lambda s: s.__setitem__("culture_ids", ["alphaland", "extraland"]))
        self.assertTrue(has(errs, "alpha_career", "culture"))

    def test_rule2_culture_order_does_not_matter(self):
        fix = FixtureFiles(self, alpha_cultures=["alphaland", "betaland"])
        spec = good_spec()
        spec["culture_ids"] = ["betaland", "alphaland"]
        self.assertEqual(career_infos(fix)["alpha_career"].cultures, ["alphaland", "betaland"])
        self.assertFalse(has(errors_for(fix, spec), "culture"))
        spec["culture_ids"] = ["betaland"]
        self.assertTrue(has(errors_for(fix, spec), "culture"))

    def test_rule2_ids_and_culture_lists_are_type_checked_not_crashed_on(self):
        odd = [None, 7, ["a"], {"x": 1}, True]
        for value in odd:
            with self.subTest(career_id=value):
                spec = good_spec()
                spec["careers"][0]["id"] = value
                self.assertTrue(errors_for(self.fix, spec))
        for value in (None, [1, 2], [["a"]], "alphaland", [], [""]):
            with self.subTest(culture_ids=value):
                spec = good_spec()
                spec["culture_ids"] = value
                self.assertTrue(has(errors_for(self.fix, spec), "culture_ids"))

    def test_rule2_garbage_shapes_give_errors_never_an_exception(self):
        spec = good_spec()
        career = spec["careers"][0]
        career["forks"] = ["x", 3, None, {"slot": "a"}, {"slot": 1, "name": 5, "options": "no"}, []]
        career["root"] = ["not", "an", "object"]
        self.assertTrue(errors_for(self.fix, spec))
        for bad in (None, [], "text", 4):
            self.assertTrue(errors_for(self.fix, bad))

    def test_rule2_random_junk_in_any_field_gives_errors_never_an_exception(self):
        junk = [None, [], {}, "x", 7, -1.5, True, [1], {"a": 1}, ""]
        rng = random.Random(7)

        def paths(node, prefix=()):
            yield prefix
            if isinstance(node, dict):
                for key, value in node.items():
                    yield from paths(value, prefix + (key,))
            elif isinstance(node, list):
                for index, value in enumerate(node):
                    yield from paths(value, prefix + (index,))

        all_paths = [p for p in paths(good_spec()) if p]
        for trial in range(400):
            spec = good_spec()
            for _ in range(rng.choice([1, 2, 3])):
                path = rng.choice(all_paths)
                try:
                    holder = spec
                    for step in path[:-1]:
                        holder = holder[step]
                    holder[path[-1]] = copy.deepcopy(rng.choice(junk))
                except (KeyError, IndexError, TypeError):
                    continue
            try:
                errors_for(self.fix, spec)
            except Exception as exc:        # noqa: BLE001 - the point of the test
                self.fail(f"trial {trial}: {type(exc).__name__}: {exc}")

    def test_rule2_an_effect_type_that_is_not_a_string_is_an_error(self):
        def mutate(s):
            s["careers"][0]["root"]["effects"] = [{"type": ["Damage"], "magnitude": 0.05,
                                                   "attack_type_mask": "Melee", "troop_class": "Infantry"}]
        self.assertTrue(has(self.errors(mutate), "type must be a string"))

    # Rule 3: effect types
    def test_rule3_unknown_effect_type(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "Sparkle", "magnitude": 1}]
        self.assertTrue(has(self.errors(mutate), "Sparkle", "PassiveEffectType"))

    def test_rule3_banned_effect_type(self):
        for etype in ("StealthBonus", "ShrugOff"):
            with self.subTest(etype=etype):
                def mutate(s, t=etype):
                    s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": t, "magnitude": 0.1}]
                self.assertTrue(has(self.errors(mutate), etype, "banned"))

    def test_rule3_a_type_the_runtime_never_reads_is_refused(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "Unwired", "magnitude": 1}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "Unwired", "PassiveEffectConsumers"))
        self.assertFalse(has(errs, "Unwired", "not in PassiveEffectType.cs"))

    def test_rule3_bad_attack_type_mask_token(self):
        def mutate(s):
            s["careers"][0]["root"]["effects"][0]["attack_type_mask"] = "Melee,Sideways"
        self.assertTrue(has(self.errors(mutate), "Sideways", "attack_type_mask"))

    def test_rule3_the_none_mask_is_not_authorable(self):
        def mutate(s):
            s["careers"][0]["root"]["effects"][0]["attack_type_mask"] = "None"
        self.assertTrue(has(self.errors(mutate), "None", "attack_type_mask"))

    def test_rule3_a_mask_is_allowed_only_on_mask_gated_types(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "Health", "magnitude": 40, "attack_type_mask": "Melee"}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "attack_type_mask", "Health", "mask-gated"))

    def test_rule3_troop_damage_and_resistance_take_no_mask_but_all(self):
        for etype in ("TroopDamage", "TroopResistance"):
            for mask, refused in (("Ranged", True), ("Melee", True), ("Blunt", True), ("Melee,Blunt", True),
                                  ("All", False), ("all", False), ("Melee,Ranged", False)):
                with self.subTest(etype=etype, mask=mask):
                    def mutate(s, t=etype, m=mask):
                        s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                            {"type": t, "magnitude": 0.2, "attack_type_mask": m}]
                    errs = self.errors(mutate)
                    self.assertEqual(has(errs, "attack_type_mask", "All", "Active Effects"), refused, errs)

    def test_rule3_the_hero_pair_may_still_carry_a_narrow_mask(self):
        def mutate(s):
            s["careers"][0]["root"]["effects"] = [{"type": "Resistance", "magnitude": 0.05,
                                                   "attack_type_mask": "Ranged"}]
        self.assertFalse(has(self.errors(mutate), "attack_type_mask"))

    def test_rule3_bad_troop_class_name(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "TroopDamage", "magnitude": 0.15, "troop_class": "Navy"}]
        self.assertTrue(has(self.errors(mutate), "Navy", "troop_class"))

    def test_rule3_troop_class_only_on_class_filtered_types(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "Health", "magnitude": 40, "troop_class": "Infantry"}]
        self.assertTrue(has(self.errors(mutate), "troop_class", "Health", "class-filtered"))

    def test_rule3_troop_class_is_allowed_on_a_type_the_csharp_class_filters(self):
        def mutate(s):
            s["careers"][0]["forks"][4]["options"][0]["effects"] = [
                {"type": "TroopTraining", "magnitude": 15, "troop_class": "Infantry"}]
        self.assertFalse(has(self.errors(mutate), "troop_class"))

    def class_errors(self, slot, classes, archetype="infantry", where="fork"):
        def mutate(s):
            s["careers"][0]["archetype"] = archetype
            effects = [{"type": "TroopDamage", "magnitude": 0.08, "troop_class": classes}]
            if where == "root":
                s["careers"][0]["root"]["effects"] = effects
            else:
                s["careers"][0]["forks"][slot - 1]["options"][0]["effects"] = effects
        return [e for e in self.errors(mutate) if "troop_class" in e and "archetype" in e]

    def test_rule3_a_class_effect_in_slot_1_5_or_6_must_name_the_archetypes_class(self):
        for slot in (1, 5, 6):
            with self.subTest(slot=slot):
                errs = self.class_errors(slot, "Ranged")
                self.assertTrue(has(errs, f"slot {slot}", "Ranged", "infantry"), errs)

    def test_rule3_a_class_list_that_adds_another_class_is_refused_in_the_locked_slots(self):
        self.assertTrue(self.class_errors(1, "Infantry,Ranged"))

    def test_rule3_the_archetypes_own_class_passes_in_the_locked_slots(self):
        for archetype, cls in (("infantry", "Infantry"), ("ranged", "Ranged"), ("cavalry", "Cavalry")):
            for slot in (1, 5, 6):
                with self.subTest(archetype=archetype, slot=slot):
                    self.assertEqual(self.class_errors(slot, cls, archetype), [])

    def test_rule3_a_ranged_career_is_refused_an_infantry_class_in_slot_1(self):
        self.assertTrue(self.class_errors(1, "Infantry", "ranged"))

    def test_rule3_slots_2_to_4_and_the_root_take_any_class(self):
        for slot in (2, 3, 4):
            with self.subTest(slot=slot):
                self.assertEqual(self.class_errors(slot, "Ranged"), [])
        self.assertEqual(self.class_errors(1, "Ranged", where="root"), [])

    def test_rule3_naming_every_class_is_not_a_class_filter(self):
        self.assertEqual(self.class_errors(1, "Infantry,Ranged,Cavalry"), [])

    def test_rule3_types_added_to_the_enum_later_are_picked_up(self):
        root = Path(tempfile.mkdtemp(prefix="gcf-system-"))
        self.addCleanup(lambda: __import__("shutil").rmtree(root, ignore_errors=True))
        enum = ENUM_CS.replace("Unwired", "Unwired,\n    BrandNewThing")
        write_system_dir(root, enum_cs=enum, consumers=consumers_cs(consumed=DEFAULT_CONSUMED + ["BrandNewThing"]))
        cs = gcf.load_cs_sets(root)
        self.assertIn("BrandNewThing", cs.effect_types)
        self.assertIn("BrandNewThing", cs.consumed)
        self.assertNotIn("Imaginary", cs.effect_types)   # a name in a comment is not a member

    def test_rule3_attack_mask_names_come_from_the_csharp_file(self):
        cs = gcf.load_cs_sets(self.fix.system_dir)
        self.assertEqual(cs.attack_masks, {"Melee", "Ranged", "All", "Cut", "Pierce", "Blunt"})

    # Rule 4: resources, derived from special_resources_config.xml and troop_resource_costs.xml
    def test_rule4_resource_effects_need_a_resource(self):
        for etype in ("SpecialResourceGain", "SpecialResourceUpkeepModifier",
                      "SpecialResourceUpgradeCostModifier"):
            with self.subTest(etype=etype):
                def mutate(s, t=etype):
                    s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": t, "magnitude": 1.0 if t == "SpecialResourceGain" else -1.0}]
                errs = self.errors(mutate)    # alphaland resolves to no resource in the fixture
                self.assertTrue(has(errs, etype, "resource", "special_resources_config.xml"), errs)

    def test_rule4_a_career_with_a_resource_but_no_upkeep_troops_refuses_the_upkeep_perk(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_merchant", "mithril", None)])
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "SpecialResourceUpkeepModifier", "magnitude": -1.0}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "SpecialResourceUpkeepModifier", "upkeep"), errs)
        self.assertFalse(has(errs, "needs a resource"))

    def test_rule4_a_resource_with_upkeep_troops_passes_both_perks(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_troop", "mithril", 0.5)])
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "SpecialResourceUpkeepModifier", "magnitude": -1.0}]
            s["careers"][0]["forks"][2]["options"][1]["effects"] = [
                {"type": "SpecialResourceGain", "magnitude": 1.0}]
        errs = self.errors(mutate)
        self.assertFalse(has(errs, "resource"), errs)
        self.assertFalse(has(errs, "upkeep"), errs)

    def test_rule4_the_kingdom_row_wins_over_the_culture_row(self):
        fix = FixtureFiles(self, alpha_cultures=["alphaland", "alphakingdom"])
        fix.write_resources([("by_culture", [], ["alphaland"]), ("by_kingdom", ["alphakingdom"], [])],
                            [("t1", "by_kingdom", 1.0), ("t2", "by_culture", None)])
        data = gcf.load_resource_data(fix.resource_dir)
        info = career_infos(fix)["alpha_career"]
        self.assertEqual(data.resource_for(info.cultures), "by_kingdom")
        self.assertEqual(data.upkeep_troops(info.cultures), 1)      # t1 only; t2 has no daily_upkeep
        self.assertEqual(data.upkeep_troops(["alphaland"]), 0)

    # G2: upkeep troops are counted by the troop's NPCCharacter culture, never by the cost row's resource_id
    def test_g2_a_troop_of_another_culture_does_not_count_even_with_the_right_resource_id(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_troop", "mithril", 0.5, "elsewhere")])
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "SpecialResourceUpkeepModifier", "magnitude": -1.0}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "SpecialResourceUpkeepModifier", "upkeep"), errs)
        self.assertFalse(has(errs, "needs a special resource"), errs)

    def test_g2_a_troop_of_the_careers_culture_counts_whatever_its_resource_id_says(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])],
                                 [("m_troop", "documentation_only_id", 0.5, "alphaland")])
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "SpecialResourceUpkeepModifier", "magnitude": -1.0}]
        errs = self.errors(mutate)
        self.assertFalse(has(errs, "upkeep"), errs)
        self.assertFalse(has(errs, "resource"), errs)

    def test_g2_a_troop_without_a_daily_upkeep_does_not_count(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_merchant", "mithril", None, "alphaland")])
        data = gcf.load_resource_data(self.fix.resource_dir)
        self.assertEqual(data.upkeep_troops(["alphaland"]), 0)

    def test_g2_a_troop_defined_in_the_characters_folder_is_found(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_troop", "mithril", 0.5, False)])
        (self.fix.characters_dir / "npcs_fixture.xml").write_text(
            '<NPCCharacters><NPCCharacter id="m_troop" culture="Culture.alphaland" /></NPCCharacters>',
            encoding="utf-8")
        data = gcf.load_resource_data(self.fix.resource_dir)
        self.assertEqual(data.upkeep_troops(["alphaland"]), 1)

    def test_g2_an_upkeep_troop_found_nowhere_is_an_error(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("ghost_troop", "mithril", 0.5, False)])
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_resource_data(self.fix.resource_dir)
        self.assertIn("ghost_troop", str(ctx.exception))
        self.fix.write_spec(good_spec())
        code, out = self.fix.run()
        self.assertEqual(code, 2, out)
        self.assertIn("ghost_troop", out)

    def test_g2_a_row_without_upkeep_may_name_a_troop_found_nowhere(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("ghost_merchant", "mithril", None, False)])
        gcf.load_resource_data(self.fix.resource_dir)

    def test_g2_an_upkeep_troop_with_no_culture_is_an_error(self):
        self.fix.write_resources([("mithril", [], ["alphaland"])], [("m_troop", "mithril", 0.5, False)])
        (self.fix.characters_dir / "npcs_fixture.xml").write_text(
            '<NPCCharacters><NPCCharacter id="m_troop" /></NPCCharacters>', encoding="utf-8")
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_resource_data(self.fix.resource_dir)
        self.assertIn("m_troop", str(ctx.exception))
        self.assertIn("culture", str(ctx.exception))

    # G10: C# reads the resources last-wins, the generator used to read them first-wins
    def test_g10_a_duplicate_resource_id_is_refused(self):
        self.fix.write_resources([("mithril", [], ["alphaland"]), ("mithril", [], ["gammaland"])],
                                 [("m_troop", "mithril", 0.5, "alphaland")])
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_resource_data(self.fix.resource_dir)
        self.assertIn("mithril", str(ctx.exception))
        self.assertIn("duplicate", str(ctx.exception))

    def test_rule4_two_kingdoms_with_different_resources_are_ambiguous(self):
        data = gcf.ResourceData({"k1": "r1", "k2": "r2"}, {}, {})
        with self.assertRaises(gcf.GeneratorError):
            data.resource_for(["k1", "k2"])
        self.assertIsNone(data.resource_for(["nothing"]))

    def test_rule4_the_default_fixture_gamma_career_has_a_resource_and_upkeep(self):
        spec = gamma_spec([{"type": "SpecialResourceUpkeepModifier", "magnitude": -1.0}])
        self.assertFalse(has(errors_for(self.fix, spec), "resource"))
        self.assertFalse(has(errors_for(self.fix, spec), "upkeep"))

    def test_rule4_unreadable_or_empty_resource_data_is_an_error_not_a_pass(self):
        empty = Path(tempfile.mkdtemp(prefix="gcf-res-"))
        self.addCleanup(lambda: __import__("shutil").rmtree(empty, ignore_errors=True))
        with self.assertRaises(gcf.GeneratorError):          # no files at all
            gcf.load_resource_data(empty)
        (empty / "special_resources_config.xml").write_text("<SpecialResources/>", encoding="utf-8")
        (empty / "troop_resource_costs.xml").write_text("<TroopResourceCosts/>", encoding="utf-8")
        with self.assertRaises(gcf.GeneratorError):          # parsed, but zero rows
            gcf.load_resource_data(empty)
        (empty / "troop_resource_costs.xml").write_text("<TroopResourceCosts><Troop", encoding="utf-8")
        with self.assertRaises(gcf.GeneratorError):          # does not parse
            gcf.load_resource_data(empty)

    # Rule 5: point levels and version
    def test_rule5_point_levels_count_matches_forks(self):
        errs = self.errors(lambda s: s["careers"][0].__setitem__("point_levels", [2, 4, 6]))
        self.assertTrue(has(errs, "point_levels", "6"))

    def test_rule5_point_levels_strictly_ascending_and_positive(self):
        for levels in ([2, 4, 4, 8, 10, 12], [2, 4, 6, 8, 10, 0], [6, 5, 4, 3, 2, 1]):
            with self.subTest(levels=levels):
                errs = self.errors(lambda s, lv=levels: s["careers"][0].__setitem__("point_levels", lv))
                self.assertTrue(has(errs, "point_levels", "ascending"))

    def test_rule5_tree_version_at_least_two(self):
        errs = self.errors(lambda s: s["careers"][0].__setitem__("tree_version", 1))
        self.assertTrue(has(errs, "tree_version", ">= 2"))

    # Rule 6: balance
    def test_rule6_option_far_from_the_tier_budget(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "Health", "magnitude": 90}]
            s["careers"][0]["forks"][2]["options"][1]["effects"] = [{"type": "RenownGain", "magnitude": 0.9}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "budget", "slot 3"))

    def test_rule6_an_option_within_tolerance_passes(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "Health", "magnitude": 43}]
        self.assertFalse(has(self.errors(mutate), "budget"))

    def test_rule6_options_in_one_fork_must_be_close(self):
        def mutate(s):
            fork3 = s["careers"][0]["forks"][2]
            fork3["options"][0]["effects"] = [{"type": "Health", "magnitude": 35}]   # 3.5 VU
            fork3["options"][1]["effects"] = [{"type": "RenownGain", "magnitude": 0.45}]  # 4.5 VU
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "spread", "slot 3"))
        self.assertFalse(has(errs, "budget"))

    def test_rule6_class_only_effects_may_be_fifty_percent_larger(self):
        def narrowed(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "TroopResistance", "magnitude": 0.30, "troop_class": "Infantry"}]
        def wide(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "TroopResistance", "magnitude": 0.30}]
        # army-wide 0.30 = 6 VU, far over a tier 2 budget of 4; class-only 0.30 = 4 VU
        self.assertFalse(has(self.errors(narrowed), "budget"))
        self.assertTrue(has(self.errors(wide), "budget"))

    def test_rule6_naming_all_three_classes_is_not_narrowing(self):
        self.assertAlmostEqual(gcf.effect_vu({"type": "TroopDamage", "magnitude": 0.15,
                                              "troop_class": "Infantry,Ranged,Cavalry"}, TEST_RULES), 3.0)
        self.assertAlmostEqual(gcf.effect_vu({"type": "TroopDamage", "magnitude": 0.15,
                                              "troop_class": "Infantry"}, TEST_RULES), 2.0)

    def test_rule6_a_type_without_a_vu_unit_cannot_be_balanced(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "Ammo", "magnitude": 0.2}]
        self.assertTrue(has(self.errors(mutate), "Ammo", "vu_unit"))

    def test_rule6_keystone_vu_is_checked_against_the_budget(self):
        def mutate(s):
            s["careers"][0]["forks"][1]["options"][2]["vu"] = 5
        self.assertTrue(has(self.errors(mutate), "budget", "slot 2"))

    # Sign: cost types are a benefit when negative
    def test_sign_the_benefit_of_a_cost_type_is_the_negative_magnitude(self):
        self.assertEqual(gcf.benefit({"type": "TroopWages", "magnitude": -0.25}, TEST_RULES), 0.25)
        self.assertEqual(gcf.benefit({"type": "RecruitCost", "magnitude": 0.3}, TEST_RULES), -0.3)
        self.assertEqual(gcf.benefit({"type": "RenownGain", "magnitude": 0.25}, TEST_RULES), 0.25)
        self.assertEqual(gcf.benefit({"type": "RenownGain", "magnitude": -0.25}, TEST_RULES), -0.25)

    def test_sign_vu_prices_the_benefit(self):
        self.assertAlmostEqual(gcf.effect_vu({"type": "TroopWages", "magnitude": -0.25}, TEST_RULES), 2.5)
        self.assertAlmostEqual(gcf.effect_vu({"type": "RenownGain", "magnitude": 0.25}, TEST_RULES), 2.5)
        option = {"effects": [{"type": "Health", "magnitude": 10}, {"type": "RecruitCost", "magnitude": -0.1}]}
        self.assertAlmostEqual(gcf.option_vu(option, TEST_RULES), 2.0)

    def test_a_penalty_is_refused(self):
        for effect in ({"type": "RenownGain", "magnitude": -0.4},          # a normal type, negative
                       {"type": "TroopWages", "magnitude": 0.4},           # a cost type, raised
                       {"type": "SpecialResourceUpkeepModifier", "magnitude": 1.0},
                       {"type": "RecruitCost", "magnitude": 0.4}):
            with self.subTest(effect=effect):
                def mutate(s, e=effect):
                    s["careers"][0]["forks"][2]["options"][0]["effects"] = [e]
                self.fix.write_resources([("m", [], ["alphaland"])], [("t", "m", 1.0)])
                errs = self.errors(mutate)
                self.assertTrue(has(errs, effect["type"], "penalty"), errs)

    def test_a_penalty_on_the_root_is_refused_too(self):
        def mutate(s):
            s["careers"][0]["root"]["effects"] = [{"type": "TroopWages", "magnitude": 0.05}]
        self.assertTrue(has(self.errors(mutate), "root", "TroopWages", "penalty"))

    def test_a_wage_cut_is_accepted_and_priced(self):
        def mutate(s):
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "TroopWages", "magnitude": -0.4}]
        errs = self.errors(mutate)    # 0.4 / 0.1 = 4.0 VU, a tier 2 budget
        self.assertEqual(errs, [])

    # Rule 7: caps
    def test_rule7_summed_damage_over_the_cap(self):
        def mutate(s):
            for slot in (3, 4):
                s["careers"][0]["forks"][slot - 1]["options"].append(damage_option(2))
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "cap", DAMAGE_CAP, "alpha_career"))

    def test_rule7_armor_penetration_shares_the_damage_cap(self):
        def mutate(s):
            c = s["careers"][0]
            c["forks"][2]["options"][0]["effects"] = [{"type": "ArmorPenetration", "magnitude": 0.2}]
            c["forks"][3]["options"][0]["effects"] = [{"type": "ArmorPenetration", "magnitude": 0.2}]
        errs = self.errors(mutate)   # root 0.05 + 0.2 + 0.2 = 0.45 plus fork 1 option c 0.15 = 0.60
        self.assertTrue(has(errs, "cap", DAMAGE_CAP))

    def test_rule7_the_combined_troop_cap_sums_both_types(self):
        def mutate(s):
            c = s["careers"][0]
            c["forks"][2]["options"][0]["effects"] = [{"type": "TroopDamage", "magnitude": 0.1}, {"type": "TroopResistance", "magnitude": 0.1}]
            c["forks"][3]["options"][0]["effects"] = [{"type": "TroopDamage", "magnitude": 0.1}, {"type": "TroopResistance", "magnitude": 0.1}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "cap", "TroopDamage+TroopResistance"))

    def test_rule7_a_cost_cap_counts_the_benefit_not_the_raw_sign(self):
        def mutate(s):
            c = s["careers"][0]
            for slot in (3, 4):          # two tier 2 forks at -0.4 each: 0.8 of benefit against a cap of 0.5
                c["forks"][slot - 1]["options"][0]["effects"] = [{"type": "RecruitCost", "magnitude": -0.4}]
        errs = self.errors(mutate)
        self.assertTrue(has(errs, "cap", "RecruitCost", "0.8"), errs)

    def test_rule7_cap_search_finds_the_worst_build(self):
        career = career_spec("alpha_career")
        career["forks"][2]["options"].append(damage_option(2))   # 0.20 on fork 3
        career["forks"][4]["options"].append(damage_option(3))   # 0.25 on fork 5
        result = gcf.analyze_caps(career, TEST_RULES)[DAMAGE_CAP]
        # root 0.05 + fork 1 option c 0.15 + fork 3 option c 0.20 + fork 5 option c 0.25
        self.assertAlmostEqual(result["max"], 0.65)
        self.assertEqual(result["limit"], 0.45)
        self.assertEqual(result["build"][1], ["c"])
        self.assertEqual(result["build"][3], ["c"])
        self.assertEqual(result["build"][5], ["c"])

    def test_rule7_a_fork_with_nothing_to_gain_is_left_empty(self):
        result = gcf.analyze_caps(career_spec("alpha_career"), TEST_RULES)[DAMAGE_CAP]
        self.assertEqual(result["build"][2], [])
        self.assertEqual(gcf.describe_build(result["build"]), "f1:c")

    def test_rule7_a_wide_fork_counts_its_best_picks(self):
        career = career_spec("alpha_career")
        fork3 = career["forks"][2]
        fork3["options"][1] = damage_option(2)
        fork3["options"].append(damage_option(2))
        fork3["max_picks"] = 2
        result = gcf.analyze_caps(career, TEST_RULES)[DAMAGE_CAP]
        self.assertAlmostEqual(result["max"], 0.05 + 0.15 + 0.2 + 0.2)
        self.assertEqual(result["build"][3], ["b", "c"])

    def test_rule7_a_career_under_every_cap_reports_headroom(self):
        result = gcf.analyze_caps(career_spec("alpha_career"), TEST_RULES)
        self.assertAlmostEqual(result[DAMAGE_CAP]["max"], 0.20)
        self.assertEqual(result["PartySize"]["max"], 0)

    def test_rule7_cap_search_equals_brute_force_with_empty_forks_allowed(self):
        rng = random.Random(20261015)
        types = ["Damage", "ArmorPenetration", "RecruitCost", "Health"]
        for trial in range(60):
            forks = []
            for slot in range(1, 7):
                options = []
                for _ in range(rng.choice([2, 3])):
                    effects = [{"type": rng.choice(types),
                                "magnitude": round(rng.choice([-0.3, -0.1, 0.05, 0.1, 0.2, 0.4]), 3)}
                               for _ in range(rng.choice([0, 1, 2]))]
                    options.append({"name": "o", "description": "d", "effects": effects})
                forks.append({"slot": slot, "name": "f", "max_picks": rng.randint(1, len(options)),
                              "options": options})
            career = {"id": "c", "archetype": "infantry", "tree_version": 2, "point_levels": [1, 2, 3, 4, 5, 6],
                      "root": {"description": "r", "effects": [{"type": "Damage", "magnitude": 0.05}]},
                      "forks": forks}
            for name, cap in TEST_RULES["caps"].items():
                types_in_cap = set(cap["types"])

                def contribution(effects):
                    return sum(gcf.benefit(e, TEST_RULES) for e in effects if e["type"] in types_in_cap)

                best = contribution(career["root"]["effects"])
                for fk in forks:
                    values = [contribution(o["effects"]) for o in fk["options"]]
                    fork_best = 0.0     # picking nothing is allowed
                    for size in range(1, fk["max_picks"] + 1):
                        for combo in itertools.combinations(values, size):
                            fork_best = max(fork_best, sum(combo))
                    best += fork_best
                got = gcf.analyze_caps(career, TEST_RULES)[name]["max"]
                self.assertAlmostEqual(got, best, msg=f"trial {trial} cap {name}")

    # Rule 8: uniqueness
    def test_rule8_an_existing_id_blocks_the_write(self):
        fix = FixtureFiles(self)
        fix.choices_text = fix.choices_text.replace(
            banner("gamma_career"),
            '  <Choice id="alpha_career_f1_a" type="Passive" description="{=taom_other}x" />\n\n'
            + banner("gamma_career"))
        fix.write_files()
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("collides", out)
        self.assertIn("alpha_career_f1_a", out)
        self.assertEqual(fix.snapshot(), before)

    def test_rule8_an_existing_loc_key_blocks_the_write(self):
        fix = FixtureFiles(self)
        fix.choices_text = fix.choices_text.replace(
            banner("gamma_career"),
            '  <Choice id="unrelated" type="Passive" description="{=taom_cf_alpha_career_f1_a}x" />\n\n'
            + banner("gamma_career"))
        fix.write_files()
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("collides", out)
        self.assertIn("taom_cf_alpha_career_f1_a", out)

    def test_rule8_a_colliding_id_inside_taom_careers_xml_blocks_the_write(self):
        fix = FixtureFiles(self)
        fix.careers_text = fix.careers_text.replace('<Group id="gamma_career_t1_a" />',
                                                    '<Group id="alpha_career_f2" />')
        fix.write_files()
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("collides", out)
        self.assertIn("alpha_career_f2", out)
        self.assertIn("taom_careers.xml", out)
        self.assertEqual(fix.snapshot(), before)

    def test_rule8_a_commented_out_id_is_not_a_collision(self):
        fix = FixtureFiles(self)
        fix.choices_text = fix.choices_text.replace(
            banner("gamma_career"), '  <!-- <Choice id="alpha_career_f1_a" /> -->\n\n' + banner("gamma_career"))
        fix.write_files()
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)

    def test_rule8_two_careers_in_one_spec_cannot_share_an_id(self):
        def mutate(s):
            s["careers"].append(copy.deepcopy(s["careers"][0]))
        self.assertTrue(has(self.errors(mutate), "duplicate", "alpha_career"))

    def test_every_error_is_listed_and_nothing_is_written(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["tree_version"] = 1
        spec["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "StealthBonus", "magnitude": 0.1}]
        spec["careers"][0]["archetype"] = "mage"
        fix.write_spec(spec)
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("tree_version", out)
        self.assertIn("StealthBonus", out)
        self.assertIn("mage", out)
        self.assertEqual(fix.snapshot(), before)


# --------------------------------------------------------------------------- #
# The tree version gate                                                        #
# --------------------------------------------------------------------------- #
class TreeVersionGateTests(unittest.TestCase):
    def applied(self, spec=None, **fixture):
        fix = FixtureFiles(self, **fixture)
        fix.write_spec(spec or good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        return fix

    def with_version(self, fix, version, mutate=None):
        spec = good_spec()
        spec["careers"][0]["tree_version"] = version
        if mutate:
            mutate(spec["careers"][0])
        fix.write_spec(spec)
        return fix.run("--apply")

    def test_first_adoption_must_raise_the_live_tree_version(self):
        for live, spec_version, ok in ((None, 2, True), (2, 2, False), (3, 2, False), (2, 3, True)):
            with self.subTest(live=live, spec=spec_version):
                fix = FixtureFiles(self, alpha_version=live)
                spec = good_spec()
                spec["careers"][0]["tree_version"] = spec_version
                fix.write_spec(spec)
                before = fix.snapshot()
                code, out = fix.run("--apply")
                if ok:
                    self.assertEqual(code, 0, out)
                else:
                    self.assertEqual(code, 2, out)
                    self.assertIn("tree_version", out)
                    self.assertIn("alpha_career", out)
                    self.assertEqual(fix.snapshot(), before)

    def test_a_structural_change_without_a_bump_is_refused(self):
        changes = {
            "an effect type": lambda c: c["forks"][2]["options"][0].__setitem__(
                "effects", [{"type": "PartySize", "magnitude": 50}]),
            "an added option": lambda c: c["forks"][2]["options"].append(
                {"name": "n", "description": "d", "effects": [{"type": "Health", "magnitude": 40}]}),
            "max_picks": lambda c: c["forks"][0].__setitem__("max_picks", 2),
            "a mutation property": lambda c: c["forks"][1]["options"][2]["mutations"][0].__setitem__("property", "Duration"),
            "a point level": lambda c: c.__setitem__("point_levels", [2, 4, 6, 8, 10, 13]),
        }
        for label, change in changes.items():
            with self.subTest(change=label):
                fix = self.applied()
                before = fix.snapshot()
                code, out = self.with_version(fix, 2, change)
                self.assertEqual(code, 2, out)
                self.assertIn("tree_version", out)
                self.assertIn("alpha_career", out)
                self.assertEqual(fix.snapshot(), before)

    def test_a_structural_change_with_a_bump_is_written(self):
        fix = self.applied()
        code, out = self.with_version(fix, 3, lambda c: c["forks"][2]["options"][0].__setitem__(
            "effects", [{"type": "PartySize", "magnitude": 50}]))
        self.assertEqual(code, 0, out)
        self.assertIn('tree_version="3"', fix.lf(fix.careers_path))
        self.assertIn("tree_version=3", fix.lf(fix.careers_path))
        self.assertEqual(fix.run("--check")[0], 0)

    def test_a_text_only_or_magnitude_only_change_keeps_the_version(self):
        def text(c):
            c["forks"][2]["name"] = "Renamed"
            c["forks"][2]["options"][0]["description"] = "New words."
        def magnitude(c):
            c["forks"][2]["options"][0]["effects"][0]["magnitude"] = 42
            c["forks"][2]["options"][0]["effects"] = [{"type": "Health", "magnitude": 42}]
        for label, change in (("text", text), ("magnitude", magnitude)):
            with self.subTest(change=label):
                fix = self.applied()
                code, out = self.with_version(fix, 2, change)
                self.assertEqual(code, 0, out)
                self.assertIn('tree_version="2"', fix.lf(fix.careers_path))

    def test_g4_reordering_the_effects_of_a_two_effect_option_is_not_structural(self):
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"] = [
            {"type": "Health", "magnitude": 20}, {"type": "RenownGain", "magnitude": 0.2}]   # 2 + 2 = 4 VU
        fix = self.applied(spec)
        reordered = copy.deepcopy(spec)
        reordered["careers"][0]["forks"][2]["options"][0]["effects"].reverse()
        fix.write_spec(reordered)
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn('tree_version="2"', fix.lf(fix.careers_path))
        self.assertEqual(fix.run("--check")[0], 0)

    # G1: the shape sees an effect's mask and class, and respelling them stays non-structural
    def damage_spec(self, mask):
        spec = good_spec()
        effect = spec["careers"][0]["forks"][0]["options"][2]["effects"][0]
        if mask is None:
            del effect["attack_type_mask"]
        else:
            effect["attack_type_mask"] = mask
        return spec

    def class_spec(self, classes, magnitude):
        """Fork 3 option a is a TroopTraining pip: 12 narrowed to one class, or 8 for every class, is 4 VU (fork 3
        takes any class; forks 1, 5 and 6 must name the archetype's own)."""
        spec = good_spec()
        effect = {"type": "TroopTraining", "magnitude": magnitude}
        if classes is not None:
            effect["troop_class"] = classes
        spec["careers"][0]["forks"][2]["options"][0]["effects"] = [effect]
        return spec

    def test_g1_a_mask_change_at_the_same_tree_version_is_refused(self):
        for old, new in ((None, "Melee"), ("Melee", "Ranged"), ("Melee", "Melee,Blunt"), ("All", "Melee"),
                         ("Melee,Cut", "Melee,Pierce"), ("Melee", None)):
            with self.subTest(old=old, new=new):
                fix = self.applied(self.damage_spec(old))
                before = fix.snapshot()
                fix.write_spec(self.damage_spec(new))
                code, out = fix.run("--apply")
                self.assertEqual(code, 2, out)
                self.assertIn("tree structure changed", out)
                self.assertEqual(fix.snapshot(), before)

    def test_g1_a_class_change_at_the_same_tree_version_is_refused(self):
        for (old, old_mag), (new, new_mag) in (((None, 8), ("Infantry", 12)), (("Infantry", 12), ("Cavalry", 12)),
                                               (("Infantry", 12), ("Infantry,Ranged", 12)),
                                               (("Infantry", 12), (None, 8))):
            with self.subTest(old=old, new=new):
                fix = self.applied(self.class_spec(old, old_mag))
                before = fix.snapshot()
                fix.write_spec(self.class_spec(new, new_mag))
                code, out = fix.run("--apply")
                self.assertEqual(code, 2, out)
                self.assertIn("tree structure changed", out)
                self.assertEqual(fix.snapshot(), before)

    def test_g1_a_mask_or_class_change_with_a_bump_is_written(self):
        fix = self.applied(self.damage_spec("Melee"))
        spec = self.damage_spec("Ranged")
        spec["careers"][0]["tree_version"] = 3
        fix.write_spec(spec)
        self.assertEqual(fix.run("--apply")[0], 0)
        self.assertEqual(fix.run("--check")[0], 0)

    def test_g1_respelling_a_mask_is_not_structural(self):
        for old, new in ((None, "All"), ("All", None), (None, "Melee,Ranged"), ("All", "ranged | melee"),
                         ("Melee,Cut", "cut, MELEE"), ("Melee,Ranged,Cut", "All,Cut")):
            with self.subTest(old=old, new=new):
                fix = self.applied(self.damage_spec(old))
                fix.write_spec(self.damage_spec(new))
                code, out = fix.run("--apply")
                self.assertEqual(code, 0, out)
                self.assertIn('tree_version="2"', fix.lf(fix.careers_path))

    def test_g1_respelling_a_class_is_not_structural(self):
        for (old, old_mag), (new, new_mag) in (((None, 8), ("Infantry,Ranged,Cavalry", 8)),
                                               (("Infantry,Ranged,Cavalry", 8), ("cavalry | ranged, infantry", 8)),
                                               (("Infantry,Cavalry", 12), ("cavalry, infantry", 12))):
            with self.subTest(old=old, new=new):
                fix = self.applied(self.class_spec(old, old_mag))
                fix.write_spec(self.class_spec(new, new_mag))
                code, out = fix.run("--apply")
                self.assertEqual(code, 0, out)
                self.assertIn('tree_version="2"', fix.lf(fix.careers_path))

    def test_g1_the_normalised_forms(self):
        for text in (None, "All", "all", "Melee,Ranged", "ranged | melee", "All,Melee"):
            self.assertEqual(gcf.normalise_mask(text), "All", text)
        self.assertEqual(gcf.normalise_mask("Melee"), "Melee")
        self.assertEqual(gcf.normalise_mask("cut, MELEE"), gcf.normalise_mask("Melee,Cut"))
        self.assertEqual(gcf.normalise_mask("All,Cut"), gcf.normalise_mask("Melee,Ranged,Cut"))
        self.assertNotEqual(gcf.normalise_mask("Melee,Cut"), gcf.normalise_mask("Melee,Pierce"))
        self.assertNotEqual(gcf.normalise_mask("Melee,Cut"), "All")
        for text in (None, "All", "all", "Infantry,Ranged,Cavalry", "cavalry | infantry, ranged", "All,Infantry"):
            self.assertEqual(gcf.normalise_classes(text), "All", text)
        self.assertEqual(gcf.normalise_classes("Infantry"), "Infantry")
        self.assertEqual(gcf.normalise_classes("cavalry, INFANTRY"), gcf.normalise_classes("Infantry,Cavalry"))
        self.assertNotEqual(gcf.normalise_classes("Infantry,Cavalry"), "All")
        self.assertEqual(gcf.effect_shape({"type": "Damage", "magnitude": 0.1}), "Damage@All#All")
        self.assertEqual(gcf.effect_shape({"type": "Damage", "magnitude": 9, "attack_type_mask": "Melee,Cut"}),
                         "Damage@Cut+Melee#All")
        self.assertEqual(gcf.effect_shape({"type": "TroopTraining", "magnitude": 1, "troop_class": "Infantry"}),
                         "TroopTraining@All#Infantry")

    def test_g1_the_marker_records_a_versioned_shape(self):
        fix = self.applied()
        self.assertEqual(gcf.SHAPE_VERSION, "v2")
        self.assertRegex(fix.lf(fix.careers_path), r"shape=v2-[0-9a-f]{16} -->")
        infos = career_infos(fix)
        shape = gcf.structure_fingerprint(career_spec("alpha_career"), infos["alpha_career"], TEST_RULES)
        self.assertRegex(shape, r"^v2-[0-9a-f]{16}$")

    def test_g1_a_marker_from_another_shape_definition_is_told_apart_from_an_edit(self):
        for label, change in (("v1, no prefix", lambda extra: extra.replace("shape=v2-", "shape=")),
                              ("a later v3", lambda extra: extra.replace("shape=v2-", "shape=v3-"))):
            with self.subTest(marker=label):
                fix = self.applied()
                rewrite_careers_marker(fix, change)
                before = fix.snapshot()
                code, out = fix.run("--apply")
                self.assertEqual(code, 2, out)
                self.assertIn("shape definition", out)
                self.assertNotIn("tree structure changed", out)
                self.assertNotIn("hand-edited", out)
                self.assertEqual(fix.snapshot(), before)

    # G8: the region hash covers the marker's recorded attributes with the body
    def test_g8_a_hand_edited_shape_in_the_marker_cannot_let_a_structural_change_through(self):
        fix = self.applied()
        changed = good_spec()
        changed["careers"][0]["forks"][2]["options"][0]["effects"] = [{"type": "PartySize", "magnitude": 50}]
        forged = gcf.structure_fingerprint(changed["careers"][0], career_infos(fix)["alpha_career"], TEST_RULES)
        raw = fix.careers_path.read_bytes()
        fix.careers_path.write_bytes(re.sub(rb"shape=\S+", b"shape=" + forged.encode("ascii"), raw, count=1))
        fix.write_spec(changed)
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("hand-edited", out)
        self.assertEqual(fix.snapshot(), before)

    def test_g8_any_edit_to_a_recorded_marker_attribute_is_refused(self):
        for label, pattern, replacement in (("tree_version", rb"tree_version=2", b"tree_version=3"),
                                            ("point_levels", rb"point_levels=\d[\d,]*", b"point_levels=1,2,3,4,5,6"),
                                            ("shape", rb"shape=v2-[0-9a-f]{16}", b"shape=v2-0123456789abcdef")):
            with self.subTest(attribute=label):
                fix = self.applied()
                raw = fix.careers_path.read_bytes()
                fix.careers_path.write_bytes(re.sub(pattern, replacement, raw, count=1))
                fix.write_spec(good_spec())
                code, out = fix.run("--apply")
                self.assertEqual(code, 2, out)
                self.assertIn("hand-edited", out)

    def test_g8_the_hash_depends_on_the_attributes_and_the_body(self):
        self.assertNotEqual(gcf.region_hash(" a=1", "body\n"), gcf.region_hash(" a=2", "body\n"))
        self.assertNotEqual(gcf.region_hash(" a=1", "body\n"), gcf.region_hash(" a=1", "body2\n"))
        self.assertEqual(gcf.region_hash(" a=1", "body\n"), gcf.region_hash(" a=1", "body\n"))

    def test_lowering_the_version_is_always_refused(self):
        fix = self.applied(self.spec_with_version(3))
        before = fix.snapshot()
        code, out = self.with_version(fix, 2)
        self.assertEqual(code, 2, out)
        self.assertIn("lower", out)
        self.assertEqual(fix.snapshot(), before)

    def spec_with_version(self, version):
        spec = good_spec()
        spec["careers"][0]["tree_version"] = version
        return spec

    def test_raising_the_version_with_no_structural_change_is_allowed(self):
        fix = self.applied()
        code, out = self.with_version(fix, 3)
        self.assertEqual(code, 0, out)
        self.assertIn('tree_version="3"', fix.lf(fix.careers_path))

    def test_a_marker_without_a_shape_is_refused(self):
        fix = self.applied()
        rewrite_careers_marker(fix, lambda extra: re.sub(r" shape=\S+", "", extra))
        self.assertNotIn("shape=", fix.lf(fix.careers_path))
        code, out = fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("no shape fingerprint", out)

    def test_the_fingerprint_ignores_text_and_magnitude_and_sees_structure(self):
        infos = {"alpha_career": gcf.CareerInfo("alpha_career", "alpha_career_ability", "alpha_career_root", ["x"], [])}
        base = career_spec("alpha_career")
        shape = gcf.structure_fingerprint(base, infos["alpha_career"], TEST_RULES)
        edited = copy.deepcopy(base)
        edited["forks"][2]["name"] = "Other"
        edited["root"]["description"] = "Other"
        edited["forks"][2]["options"][0]["effects"][0]["magnitude"] = 77
        edited["forks"][1]["options"][2]["mutations"][0]["value"] = 9
        self.assertEqual(gcf.structure_fingerprint(edited, infos["alpha_career"], TEST_RULES), shape)
        moved = copy.deepcopy(base)
        moved["forks"][2]["options"][0]["effects"][0]["type"] = "PartySize"
        self.assertNotEqual(gcf.structure_fingerprint(moved, infos["alpha_career"], TEST_RULES), shape)
        masked = copy.deepcopy(base)
        masked["forks"][0]["options"][2]["effects"][0]["attack_type_mask"] = "Ranged"
        self.assertNotEqual(gcf.structure_fingerprint(masked, infos["alpha_career"], TEST_RULES), shape)
        respelled = copy.deepcopy(base)
        respelled["forks"][0]["options"][0]["effects"][0]["attack_type_mask"] = "Melee,Ranged"   # a Health pip: All
        self.assertEqual(gcf.structure_fingerprint(respelled, infos["alpha_career"], TEST_RULES), shape)
        classed = copy.deepcopy(base)
        classed["forks"][4]["options"][0]["effects"][0]["troop_class"] = "Infantry"
        self.assertNotEqual(gcf.structure_fingerprint(classed, infos["alpha_career"], TEST_RULES), shape)
        other_root = gcf.CareerInfo("alpha_career", "a", "different_root", ["x"], [])
        self.assertNotEqual(gcf.structure_fingerprint(base, other_root, TEST_RULES), shape)


# --------------------------------------------------------------------------- #
# C# sets, read at run time                                                    #
# --------------------------------------------------------------------------- #
class CsSetTests(unittest.TestCase):
    def system(self, **kw):
        root = Path(tempfile.mkdtemp(prefix="gcf-cs-"))
        self.addCleanup(lambda: __import__("shutil").rmtree(root, ignore_errors=True))
        return write_system_dir(root, **kw)

    def test_the_four_sets_come_from_the_csharp_files(self):
        cs = gcf.load_cs_sets(self.system())
        self.assertEqual(cs.consumed, set(DEFAULT_CONSUMED))
        self.assertEqual(cs.class_filtered, set(DEFAULT_CLASS_FILTERED))
        self.assertEqual(cs.mask_gated, set(DEFAULT_MASK_GATED))
        self.assertEqual(cs.flat, set(DEFAULT_FLAT))
        self.assertNotIn("Unwired", cs.consumed)     # named only in comments
        self.assertNotIn("Special", cs.consumed)

    def test_a_block_bodied_is_flat_is_read_too(self):
        cs = gcf.load_cs_sets(self.system(flat_cs=map_cs(flat=["Health", "DailyGold"], block_body=True)))
        self.assertEqual(cs.flat, {"Health", "DailyGold"})

    def test_a_changed_set_is_followed(self):
        cs = gcf.load_cs_sets(self.system(consumers=consumers_cs(class_filtered=["TroopDamage"], mask_gated=["Damage"])))
        self.assertEqual(cs.class_filtered, {"TroopDamage"})
        self.assertEqual(cs.mask_gated, {"Damage"})

    def test_a_missing_set_is_an_error_naming_it(self):
        for skip in ("Consumed", "ClassFilteredTypes", "MaskGatedTypes"):
            with self.subTest(missing=skip):
                root = self.system(consumers=consumers_cs(skip=(skip,)))
                with self.assertRaises(gcf.GeneratorError) as ctx:
                    gcf.load_cs_sets(root)
                self.assertIn(skip, str(ctx.exception))

    def flat_system(self, body, block=False, param="type"):
        method = (f"public static bool IsFlat(PassiveEffectType {param}) {{ {body} }}" if block
                  else f"public static bool IsFlat(PassiveEffectType {param}) => {body};")
        return self.system(flat_cs="public static class CareerPerkConsumerMap\n{\n    " + method + "\n}\n")

    def test_g9_only_a_chain_of_equality_tests_joined_by_or_is_read(self):
        a, b = "type == PassiveEffectType.Health", "type == PassiveEffectType.PartySize"
        accepted = {
            "one test": (self.flat_system(a), {"Health"}),
            "two tests": (self.flat_system(f"{a} || {b}"), {"Health", "PartySize"}),
            "tests across lines": (self.flat_system(f"{a}\n        || {b}"), {"Health", "PartySize"}),
            "a block body": (self.flat_system(f"return {a} || {b};", block=True), {"Health", "PartySize"}),
            "another parameter name": (self.flat_system("t == PassiveEffectType.DailyGold", param="t"), {"DailyGold"}),
            "a comment inside": (self.flat_system(f"{a} // the first\n        || {b}"), {"Health", "PartySize"}),
        }
        for label, (root, expected) in accepted.items():
            with self.subTest(body=label):
                self.assertEqual(gcf.load_cs_sets(root).flat, expected)

    def test_g9_any_other_shape_of_is_flat_is_an_error(self):
        a = "type == PassiveEffectType.Health"
        rejected = {
            "an and": self.flat_system(f"{a} && type == PassiveEffectType.PartySize"),
            "an exclusion": self.flat_system(f"{a} || type != PassiveEffectType.PartySize"),
            "an and inside an or": self.flat_system(f"{a} || type == PassiveEffectType.PartySize && type != PassiveEffectType.DailyGold"),
            "a negation": self.flat_system(f"!({a})"),
            "a ternary": self.flat_system(f"{a} ? false : type == PassiveEffectType.PartySize"),
            "another variable": self.flat_system("other == PassiveEffectType.Health"),
            "the wrong parameter": self.flat_system("type == PassiveEffectType.Health", param="t"),
            "a bare member": self.flat_system("PassiveEffectType.Health"),
            "a call": self.flat_system("Helper(PassiveEffectType.Health)"),
            "a trailing or": self.flat_system(f"{a} ||"),
            "a switch": self.flat_system(
                "switch (type) { case PassiveEffectType.Health: return true; default: return false; }", block=True),
            "an if before the return": self.flat_system(f"if ({a}) return true; return false;", block=True),
            "two statements": self.flat_system(f"var x = 1; return {a};", block=True),
        }
        for label, root in rejected.items():
            with self.subTest(body=label):
                with self.assertRaises(gcf.GeneratorError) as ctx:
                    gcf.load_cs_sets(root)
                self.assertIn("IsFlat", str(ctx.exception))

    def test_a_missing_is_flat_is_an_error(self):
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(flat_cs=map_cs(skip=True)))
        self.assertIn("IsFlat", str(ctx.exception))

    def test_an_empty_set_is_an_error_not_an_empty_pass(self):
        with self.assertRaises(gcf.GeneratorError):
            gcf.load_cs_sets(self.system(consumers=consumers_cs(class_filtered=[])))

    def test_a_set_naming_a_type_the_enum_lacks_is_an_error(self):
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(consumers=consumers_cs(consumed=DEFAULT_CONSUMED + ["Ghost"])))
        self.assertIn("Ghost", str(ctx.exception))

    def test_a_missing_file_is_an_error(self):
        for rel in ("Domain/PassiveEffectType.cs", "Domain/AttackTypeMask.cs", "Domain/PassiveEffectConsumers.cs",
                    "Diagnostics/CareerPerkConsumerMap.cs"):
            with self.subTest(missing=rel):
                with self.assertRaises(gcf.GeneratorError) as ctx:
                    gcf.load_cs_sets(self.system(drop=(rel,)))
                self.assertIn(Path(rel).name, str(ctx.exception))

    def test_an_enum_that_is_not_found_is_an_error(self):
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(enum_cs="public class NotAnEnum { }"))
        self.assertIn("enum PassiveEffectType", str(ctx.exception))

    def test_the_cli_exits_2_when_a_set_is_missing(self):
        fix = FixtureFiles(self)
        write_system_dir(fix.system_dir, consumers=consumers_cs(skip=("MaskGatedTypes",)))
        fix.write_spec(good_spec())
        code, out = fix.run()
        self.assertEqual(code, 2)
        self.assertIn("MaskGatedTypes", out)

    def test_comment_stripping_leaves_string_literals_alone(self):
        text = 'a = "x // y"; // gone\nb = "/* kept */"; /* gone */ c'
        self.assertEqual(gcf.strip_cs_comments(text), 'a = "x // y"; \nb = "/* kept */";  c')

    def test_rules_naming_a_type_the_enum_lacks_are_an_error(self):
        cs = gcf.load_cs_sets(self.system())
        broken = copy.deepcopy(TEST_RULES)
        broken["vu_unit"]["Typo"] = 1.0
        broken["lower_is_better"].append("Wages")
        broken["caps"]["x"] = {"types": ["Nope"], "limit": 1}
        problems = gcf.check_rules_against_cs(broken, cs)
        for name in ("Typo", "Wages", "Nope"):
            self.assertTrue(has(problems, name), problems)
        self.assertEqual(gcf.check_rules_against_cs(TEST_RULES, cs), [])


# --------------------------------------------------------------------------- #
# Review table, rules file, CLI                                                #
# --------------------------------------------------------------------------- #
class TableTests(unittest.TestCase):
    def test_the_table_renders_forks_effects_units_and_caps(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--table", str(fix.out))
        self.assertEqual(code, 0, out)
        text = fix.out.read_text(encoding="utf-8")
        self.assertIn("alpha_career", text)
        self.assertIn("| Fork | Tier | Option | Effects | VU | Description |", text)
        self.assertIn("+30 health", text)
        self.assertIn("+5% damage (melee)", text)
        self.assertIn("Keystone", text)
        self.assertIn("Radius +3", text)
        self.assertIn("3.0", text)
        self.assertIn("| Cap | Highest any build reaches | Limit | Headroom |", text)
        self.assertIn("Damage", text)
        self.assertIn("0.2", text)
        self.assertNotIn("—", text)
        self.assertNotIn("–", text)

    def test_the_root_line_shows_its_value_units_against_the_root_limit(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--table", str(fix.out))[0], 0)
        root = next(l for l in fix.out.read_text(encoding="utf-8").splitlines() if l.startswith("**Root:**"))
        self.assertIn("1.0 VU of at most 2 (root_budget 1.5 plus budget_tolerance 0.5)", root)

    def test_the_table_is_not_written_for_an_invalid_spec(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["tree_version"] = 1
        fix.write_spec(spec)
        self.assertEqual(fix.run("--table", str(fix.out))[0], 2)
        self.assertFalse(fix.out.exists())

    def test_plain_words_follow_the_csharp_flat_set(self):
        flat = {"Health", "PartySize", "DailyGold", "TroopTraining"}
        self.assertEqual(gcf.effect_words({"type": "TroopDamage", "magnitude": 0.06, "troop_class": "Infantry"}, flat),
                         "+6% troop damage [infantry]")
        self.assertEqual(gcf.effect_words({"type": "TroopWages", "magnitude": -0.04}, flat), "-4% troop wages")
        self.assertEqual(gcf.effect_words({"type": "PartySize", "magnitude": 25}, flat), "+25 party size")
        self.assertEqual(gcf.effect_words({"type": "DailyGold", "magnitude": 50}, flat), "+50 daily gold")
        self.assertEqual(gcf.effect_words({"type": "TroopTraining", "magnitude": 15, "troop_class": "Infantry"}, flat),
                         "+15 troop training [infantry]")


class ShippedRulesTests(unittest.TestCase):
    def setUp(self):
        self.rules = json.loads((TOOLS / "career_design" / "_rules.json").read_text(encoding="utf-8"))
        self.cs = gcf.load_cs_sets(REAL_SYSTEM_DIR)

    def test_the_shipped_rules_load_and_carry_the_agreed_numbers(self):
        gcf.check_rules(self.rules)
        self.assertEqual(self.rules["tier_budget"], {"1": 3, "2": 4, "3": 5})
        self.assertEqual(self.rules["budget_tolerance"], 0.5)
        self.assertEqual(self.rules["class_bonus"], 1.5)
        caps = {k: v["limit"] for k, v in self.rules["caps"].items()}
        expected = {"Damage+ArmorPenetration": 0.45, "Resistance": 0.40, "TroopDamage+TroopResistance": 0.20,
                    "PartySize": 75, "SpecialResourceGain": 1.0, "SpecialResourceUpkeepModifier": 1.0,
                    "TroopFearResistance": 0.75, "TroopKnockdownResistance": 0.60, "TroopCrushHold": 0.60,
                    "TroopMorale": 0.5, "RecruitCost": 0.5, "TroopWages": 0.5, "FoodConsumption": 0.5,
                    "TradePrices": 0.75, "RaidLoot": 0.8, "LootGold": 1.0, "InfluenceGain": 1.0,
                    "RenownGain": 1.0, "DailyGold": 250, "TroopTraining": 25, "WoundRecovery": 1.0,
                    "PrisonerConversion": 1.0, "TroopSurvival": 0.5,
                    "TroopUpgradeCost": 0.5, "SpecialResourceUpgradeCostModifier": 0.5,
                    "Ammo": 0.5, "Health": 50, "HeroHealing": 1.0, "MountChargeDamage": 0.45, "MountHealth": 0.5,
                    "MovementSpeed": 0.15, "PartyMovementSpeed": 0.15, "PartySpottingRange": 0.5,
                    "SwingSpeed": 0.15}
        self.assertEqual(caps, expected)
        for name in ("Damage+ArmorPenetration", "TroopDamage+TroopResistance"):
            self.assertEqual(len(self.rules["caps"][name]["types"]), 2)

    def test_the_balance_decisions_units_are_pinned(self):
        units = self.rules["vu_unit"]
        expected = {"Damage": 0.05, "TroopDamage": 0.0167, "TroopResistance": 0.0167, "PartySize": 12.5,
                    "SpecialResourceGain": 0.25, "Health": 10, "TroopMorale": 0.05, "Resistance": 0.04,
                    "RenownGain": 0.125, "InfluenceGain": 0.25, "SpecialResourceUpkeepModifier": 0.333,
                    "SpecialResourceUpgradeCostModifier": 0.25, "PrisonerConversion": 0.1,
                    "FoodConsumption": 0.083, "WoundRecovery": 0.1, "DailyGold": 20, "LootGold": 0.1,
                    "RaidLoot": 0.1, "TroopTraining": 2, "TradePrices": 0.1, "RecruitCost": 0.1}
        for etype, unit in expected.items():
            self.assertEqual(units[etype], unit, etype)

    def test_the_bans_and_the_sign_list_are_the_decided_ones(self):
        for etype in ("StealthBonus", "Special", "BuffDuration", "ShrugOff", "CompanionLimit",
                      "InventoryCapacity", "SmithingCostReduction"):
            self.assertIn(etype, self.rules["banned"])
        self.assertEqual(sorted(self.rules["lower_is_better"]),
                         sorted(["TroopWages", "TroopUpgradeCost", "RecruitCost", "FoodConsumption", "TradePrices",
                                 "SpecialResourceUpkeepModifier", "SpecialResourceUpgradeCostModifier"]))
        self.assertEqual(sorted(self.rules["mask_forbidden"]), ["TroopDamage", "TroopResistance"])
        self.assertNotIn("class_filtered", self.rules)
        self.assertNotIn("flat_types", self.rules)

    def test_every_unit_has_a_note_and_no_dash_in_the_prose(self):
        self.assertEqual(set(self.rules["vu_unit"]), set(self.rules["vu_notes"]))
        raw = (TOOLS / "career_design" / "_rules.json").read_text(encoding="utf-8")
        self.assertNotIn("—", raw)
        self.assertNotIn("–", raw)
        self.assertNotIn("not in PassiveEffectType.cs yet", raw)
        self.assertNotIn("type not in", raw)

    def test_units_match_the_runtime_kind(self):
        # A flat type (a count, CareerPerkConsumerMap.IsFlat) is priced per whole unit, so its VU unit is 1 or
        # more; every other type is a fraction, so its unit is below 1. TroopMorale was priced as a count (2) while
        # the runtime multiplies a factor, and the gate rejected 0.15 and accepted 10.
        self.assertTrue(self.cs.flat, "no flat type was read: the check below would pass vacuously")
        for etype, unit in self.rules["vu_unit"].items():
            if etype in self.cs.flat:
                self.assertGreaterEqual(unit, 1, f"{etype} is flat at runtime but its unit {unit} is a fraction")
            else:
                self.assertLess(unit, 1, f"{etype} is a factor at runtime but its unit {unit} is a count")

    def test_every_consumed_unbanned_type_has_a_unit_and_every_unit_is_a_real_type(self):
        banned = set(self.rules["banned"])
        missing = sorted(t for t in self.cs.consumed - banned if t not in self.rules["vu_unit"])
        self.assertEqual(missing, [], "consumed types the generator cannot balance")
        self.assertEqual(sorted(set(self.rules["vu_unit"]) - self.cs.consumed), [])
        self.assertEqual(sorted(set(self.rules["vu_unit"]) & banned), [], "a banned type carries a unit")

    def test_the_shipped_rules_agree_with_the_csharp_enum(self):
        self.assertEqual(gcf.check_rules_against_cs(self.rules, self.cs), [])

    def test_check_rules_rejects_a_missing_key(self):
        broken = dict(self.rules)
        del broken["caps"]
        with self.assertRaises(gcf.GeneratorError):
            gcf.check_rules(broken)

    def test_check_rules_rejects_malformed_shapes_and_numbers(self):
        cases = {
            "rules not an object": [],
            "slot_tiers empty": {"slot_tiers": []},
            "slot_tiers not ints": {"slot_tiers": ["a"]},
            "tier without budget": {"slot_tiers": [1, 1, 2, 2, 3, 9]},
            "budget not a number": {"tier_budget": {"1": "three", "2": 4, "3": 5}},
            "negative tolerance": {"budget_tolerance": -1},
            "tolerance a string": {"fork_spread_tolerance": "x"},
            "class_bonus below 1": {"class_bonus": 0.5},
            "unit zero": {"vu_unit": {"Damage": 0}},
            "unit a string": {"vu_unit": {"Damage": "0.05"}},
            "unit nan": {"vu_unit": {"Damage": float("nan")}},
            "cap not an object": {"caps": {"Damage": 5}},
            "cap without limit": {"caps": {"Damage": {"types": ["Damage"]}}},
            "cap limit zero": {"caps": {"Damage": {"types": ["Damage"], "limit": 0}}},
            "cap types empty": {"caps": {"Damage": {"types": [], "limit": 1}}},
            "cap types not strings": {"caps": {"Damage": {"types": [3], "limit": 1}}},
            "banned a list": {"banned": ["StealthBonus"]},
            "lower_is_better not strings": {"lower_is_better": [1]},
            "archetypes a string": {"archetypes": "infantry"},
        }
        for label, patch in cases.items():
            with self.subTest(case=label):
                rules = patch if isinstance(patch, list) else {**copy.deepcopy(TEST_RULES), **patch}
                with self.assertRaises(gcf.GeneratorError):
                    gcf.check_rules(rules)
        gcf.check_rules(copy.deepcopy(TEST_RULES))


class RealResourceDataTests(unittest.TestCase):
    """Read-only against the shipped special_resources files and the shipped troop and character files."""

    def setUp(self):
        self.data = gcf.load_resource_data(REAL_RESOURCE_DIR)
        self.infos = gcf.load_career_info((REAL_CAREER_DIR / "taom_careers.xml").read_bytes().decode("utf-8-sig"))

    def test_gondor_resolves_to_castar_with_upkeep_troops(self):
        self.assertEqual(self.data.resource_for(["gondor", "empire_w"]), "caster")
        self.assertGreater(self.data.upkeep_troops(["gondor", "empire_w"]), 0)

    def test_every_gondor_career_has_upkeep_troops_of_its_own_cultures(self):
        gondor = [i for i in self.infos.values() if set(i.cultures) == {"gondor", "empire_w"}]
        self.assertGreaterEqual(len(gondor), 3)
        for info in gondor:
            with self.subTest(career=info.id):
                self.assertEqual(self.data.resource_for(info.cultures), "caster")
                self.assertGreater(self.data.upkeep_troops(info.cultures), 0)

    def test_rohan_has_a_resource_and_no_upkeep_troops(self):
        self.assertEqual(self.data.resource_for(["vlandia"]), "marks")
        self.assertEqual(self.data.upkeep_troops(["vlandia"]), 0)

    def test_the_plans_no_resource_kingdoms_resolve_to_no_resource(self):
        # Lindon, Goblin-town, the Misty Mountain Orcs and Blue Craig (their culture ids are the
        # EligibleCultures of their careers in taom_careers.xml).
        for culture in ("lindon", "goblin", "mistymountainorcs", "bluecraig"):
            with self.subTest(culture=culture):
                careers = [i for i in self.infos.values() if i.cultures == [culture]]
                self.assertGreaterEqual(len(careers), 1, f"no shipped career is eligible for {culture} alone")
                for info in careers:
                    self.assertIsNone(self.data.resource_for(info.cultures), info.id)
                    self.assertEqual(self.data.upkeep_troops(info.cultures), 0, info.id)

    def test_every_shipped_career_resolves_without_ambiguity(self):
        self.assertGreater(len(self.infos), 50)
        for cid, info in self.infos.items():
            self.data.resource_for(info.cultures)      # raises on an ambiguous career


class CliTests(unittest.TestCase):
    def test_a_missing_spec_is_an_error(self):
        fix = FixtureFiles(self)
        code, out = fix.run(name="nope")
        self.assertEqual(code, 2)
        self.assertIn("nope.json", out)

    def test_a_malformed_spec_is_an_error(self):
        fix = FixtureFiles(self)
        (fix.design_dir / "alpha.json").write_text("{not json", encoding="utf-8")
        code, out = fix.run()
        self.assertEqual(code, 2)
        self.assertIn("alpha.json", out)

    def test_apply_and_check_together_are_rejected(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--apply", "--check")
        self.assertEqual(code, 2)

    def test_the_summary_names_each_career_and_file(self):
        fix = FixtureFiles(self)
        fix.write_spec(two_career_spec())
        code, out = fix.run()
        self.assertEqual(code, 0, out)
        self.assertIn("alpha_career", out)
        self.assertIn("beta_career", out)
        self.assertIn("taom_careers.xml", out)
        self.assertIn("taom_career_choices.xml", out)
        self.assertIn("adopt", out)

    def test_the_summary_prints_the_tree_counts(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run()
        self.assertEqual(code, 0, out)
        # three old groups holding three choices; six forks holding 3 + 3 + 2 + 2 + 2 + 2 choices
        self.assertIn("adopt: root + 3 groups (3 choices) -> root + 6 forks (14 choices)", out)

    def test_an_unexpected_exception_exits_2_never_1(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        with mock.patch.object(gcf, "run", side_effect=RuntimeError("boom")):
            code, out = fix.run()
        self.assertEqual(code, 2)
        self.assertIn("boom", out)

    def test_a_career_file_that_does_not_parse_is_an_error_not_a_crash(self):
        fix = FixtureFiles(self)
        fix.careers_path.write_bytes(b"<Careers><Career id='x'></Careers>")
        fix.write_spec(good_spec())
        code, out = fix.run()
        self.assertEqual(code, 2)
        self.assertIn("taom_careers.xml", out)

    def test_a_rules_file_that_is_not_an_object_is_an_error(self):
        fix = FixtureFiles(self)
        fix.write_json(fix.design_dir / "_rules.json", [1, 2])
        fix.write_spec(good_spec())
        self.assertEqual(fix.run()[0], 2)

    def test_after_an_apply_the_localization_steps_are_printed(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("generate_name_localization_strings.py --apply --category career_data", out)
        self.assertIn("translate_with_claude.py", out)
        self.assertIn("--seed-only", out)
        self.assertIn("unittest discover", out)

    # G7: the printed next steps
    def test_g7_the_steps_name_one_seeding_command_per_language_and_the_tests(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("new loc keys", out)
        for lang in gcf.TRANSLATOR_LANGS:
            self.assertIn(f"python tools/translate_with_claude.py --lang {lang} --module TAOM --sync-ids --seed-only --apply", out)
        self.assertNotIn("for L in", out)       # a bash loop; PowerShell cannot run it
        self.assertNotIn("$L", out)
        self.assertIn("unittest discover", out)
        for name in ("LanguageFileCoverageTests", "CareerChoicesIntegrationTests", "CareerChoiceIntegrityTests",
                     "CareerConfigProviderTests"):
            self.assertIn(name, out)
        self.assertIn("dotnet test TAOM.Tests", out)

    # The printed seeding line must be one the translator accepts. It was pinned only as text, and a merge that kept a
    # stricter translator left it failing (docs/reviews/rca-merge-ui-speed-career-forks-2026-10-30.md finding 1).
    def test_g7_the_printed_seeding_command_runs_through_the_translator(self):
        import shlex
        import translate_with_claude as twc
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        line = next(l.strip() for l in out.splitlines() if "translate_with_claude.py" in l and "--seed-only" in l)
        argv = shlex.split(line)[2:]
        self.assertIn("--apply", argv)
        argv = ["--dry-run" if a == "--apply" else a for a in argv]

        def boom(*_a, **_k):
            raise AssertionError("a seed-only run must return before discovery")

        with mock.patch.object(twc, "english_source_files", lambda module: []), \
                mock.patch.object(twc, "discover_entries", boom), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            try:
                rc = twc.main(argv)
            except SystemExit as exc:
                rc = exc.code
        self.assertEqual(0, rc)

    def test_g7_the_new_keys_header_appears_only_when_keys_were_added(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 41     # numbers only
        fix.write_spec(spec)
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("new loc keys", out)
        self.assertNotIn("--seed-only", out)
        self.assertNotIn("generate_name_localization_strings", out)
        self.assertIn("unittest discover", out)        # the tests step stays
        self.assertIn("LanguageFileCoverageTests", out)

        spec["careers"][0]["tree_version"] = 3
        spec["careers"][0]["forks"][2]["options"].append(
            {"name": "Third", "description": "A third choice.", "effects": [{"type": "Health", "magnitude": 40}]})
        fix.write_spec(spec)
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("new loc keys", out)
        self.assertIn("--seed-only", out)

    def test_the_printed_language_list_is_the_translators_language_list(self):
        import translate_with_claude as twc
        self.assertEqual(sorted(gcf.TRANSLATOR_LANGS), sorted(twc.LANGUAGES))

    def test_a_dry_run_does_not_print_the_steps(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertNotIn("--seed-only", fix.run()[1])

    def test_an_update_lists_every_key_whose_english_changed(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["description"] = "New wording."
        spec["careers"][0]["forks"][2]["name"] = "Renamed fork"
        fix.write_spec(spec)
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("taom_cf_alpha_career_f3_a", out)
        self.assertIn("taom_career_grp_alpha_career_f3", out)
        self.assertIn("New wording.", out)
        self.assertIn("TRANSLATOR_GUIDE.md", out)
        self.assertNotIn("taom_cf_alpha_career_f4_a", out)

    def test_an_update_that_changes_no_english_prints_no_stale_key_list(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 41
        fix.write_spec(spec)
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("TRANSLATOR_GUIDE.md", out)


class GuardTests(unittest.TestCase):
    """One refusal test per guard that had none."""

    def setUp(self):
        self.fix = FixtureFiles(self)
        self.fix.write_spec(good_spec())

    def region_run(self):
        return self.fix.run("--apply")

    def test_an_end_marker_without_a_begin_marker_is_refused(self):
        self.assertEqual(self.region_run()[0], 0)
        raw = self.fix.choices_path.read_bytes()
        self.fix.choices_path.write_bytes(re.sub(rb"[ \t]*<!-- career-forks:begin alpha_career [^\n]*\n", b"", raw, count=1))
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("end marker without a begin marker", out)

    def test_a_begin_marker_without_an_end_marker_is_refused(self):
        self.assertEqual(self.region_run()[0], 0)
        raw = self.fix.choices_path.read_bytes()
        self.fix.choices_path.write_bytes(re.sub(rb"[ \t]*<!-- career-forks:end alpha_career -->", b"", raw, count=1))
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("begin marker without an end marker", out)

    def test_two_begin_markers_are_refused(self):
        self.assertEqual(self.region_run()[0], 0)
        text = self.fix.choices_path.read_bytes().decode("utf-8")
        begin = re.search(r"[ \t]*<!-- career-forks:begin alpha_career [^\n]*\n", text).group(0)
        self.fix.choices_path.write_bytes(text.replace(begin, begin + begin, 1).encode("utf-8"))
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 2)
        self.assertIn("more than one begin marker", out)

    def test_element_span_refuses_an_unterminated_or_unclosed_tag(self):
        text = '<Root>\n  <ChoiceGroup id="g" a="1"\n'
        match = re.search(r'<ChoiceGroup\s+id="g"', text)
        with self.assertRaises(gcf.Refusal):
            gcf.element_span(text, match, "ChoiceGroup", "f.xml", "c")
        text = '<Root>\n  <ChoiceGroup id="g" a="1">\n  <Choice id="x" />\n</Root>\n'
        match = re.search(r'<ChoiceGroup\s+id="g"', text)
        with self.assertRaises(gcf.Refusal):
            gcf.element_span(text, match, "ChoiceGroup", "f.xml", "c")

    def test_element_span_accepts_a_self_closing_and_a_closed_element(self):
        text = '  <Choice id="x" type="Passive" />\n  <ChoiceGroup id="g">\n  </ChoiceGroup>\n'
        a = gcf.element_span(text, re.search(r'<Choice\s+id="x"', text), "Choice", "f", "c")
        self.assertEqual(text[a[0]:a[1]].strip(), '<Choice id="x" type="Passive" />')
        b = gcf.element_span(text, re.search(r'<ChoiceGroup\s+id="g"', text), "ChoiceGroup", "f", "c")
        self.assertTrue(text[b[0]:b[1]].strip().endswith("</ChoiceGroup>"))

    def test_find_one_refuses_zero_or_two_matches(self):
        text = '  <Choice id="x" />\n  <Choice id="x" />\n'
        with self.assertRaises(gcf.Refusal):
            gcf.find_one(text, "Choice", "x", "f", "c")
        with self.assertRaises(gcf.Refusal):
            gcf.find_one(text, "Choice", "y", "f", "c")

    def splice_inputs(self, careers_text=None):
        if careers_text is not None:
            self.fix.careers_text = careers_text
            self.fix.write_files()
        cf = gcf.CareerFile(self.fix.careers_path)
        infos = gcf.load_career_info(cf.text)
        return cf, infos["alpha_career"], good_spec()["careers"][0]

    def test_splice_careers_refuses_a_missing_duplicate_or_self_closing_career(self):
        cf, info, career = self.splice_inputs()
        cf.text = cf.text.replace('<Career id="alpha_career"', '<Career id="alpha_career_x"')
        with self.assertRaises(gcf.Refusal) as ctx:
            gcf.splice_careers(cf, career, info, TEST_RULES)
        self.assertIn("expected exactly one <Career>", str(ctx.exception))

        cf, info, career = self.splice_inputs()
        cf.text = cf.text + cf.text[cf.text.index("  <Career id=\"alpha_career\""):cf.text.index("  </Career>") + 12]
        with self.assertRaises(gcf.Refusal):
            gcf.splice_careers(cf, career, info, TEST_RULES)

        cf, info, career = self.splice_inputs()
        cf.text = re.sub(r'(<Career id="alpha_career".*?keystone_icon="1")>', r"\1/>", cf.text, count=1, flags=re.S)
        with self.assertRaises(gcf.Refusal) as ctx:
            gcf.splice_careers(cf, career, info, TEST_RULES)
        self.assertIn("open tag", str(ctx.exception))

    def test_splice_careers_refuses_a_career_without_a_closing_tag_or_a_group_list(self):
        cf, info, career = self.splice_inputs()
        cf.text = (self.fix.careers_head + career_block("alpha_career", "alphaland").replace("  </Career>\n", "")
                   + "</Careers>\n")
        with self.assertRaises(gcf.Refusal) as ctx:
            gcf.splice_careers(cf, career, info, TEST_RULES)
        self.assertIn("closing tag", str(ctx.exception))

        cf, info, career = self.splice_inputs()
        cf.text = re.sub(r"    <ChoiceGroups>.*?</ChoiceGroups>\n", "", cf.text, count=1, flags=re.S)
        with self.assertRaises(gcf.Refusal) as ctx:
            gcf.splice_careers(cf, career, info, TEST_RULES)
        self.assertIn("ChoiceGroups", str(ctx.exception))

    def test_splice_choices_refuses_a_career_with_no_old_groups(self):
        cf = gcf.CareerFile(self.fix.choices_path)
        info = gcf.CareerInfo("alpha_career", "alpha_career_ability", "alpha_career_root", ["alphaland"], [])
        with self.assertRaises(gcf.Refusal) as ctx:
            gcf.splice_choices(cf, good_spec()["careers"][0], info, [], TEST_RULES, gcf.load_cs_sets(self.fix.system_dir))
        self.assertIn("lists no groups", str(ctx.exception))

    def test_verify_output_reports_each_kind_of_problem(self):
        self.assertEqual(self.region_run()[0], 0)
        careers = self.fix.lf(self.fix.careers_path)
        choices = self.fix.lf(self.fix.choices_path)
        spec_careers = good_spec()["careers"]
        self.assertEqual(gcf.verify_output(careers, choices, spec_careers), [])
        broken = {
            "Career element not found": (careers.replace('<Career id="alpha_career"', '<Career id="zzz"'), choices),
            "ChoiceGroups list is not the six fork groups": (careers.replace('<Group id="alpha_career_f6" />', ""), choices),
            "tree_version attribute is missing or wrong": (careers.replace('tree_version="2"', 'tree_version="9"', 1), choices),
            "point_levels attribute is missing or wrong": (careers.replace('point_levels="2,4,6,8,10,12"', 'point_levels="1"', 1), choices),
            "root choice": (careers, choices.replace('<Choice id="alpha_career_root"', '<Choice id="elsewhere"')),
            "group f1 missing": (careers, choices.replace('<ChoiceGroup id="alpha_career_f1"', '<ChoiceGroup id="elsewhere_f1"')),
            "wrong number of choices": (careers, re.sub(r'[ ]*<Choice id="alpha_career_f3_b".*?</Choice>\n', "",
                                                          choices, count=1, flags=re.S)),
        }
        for needle, (c, ch) in broken.items():
            with self.subTest(problem=needle):
                problems = gcf.verify_output(c, ch, spec_careers)
                self.assertTrue(any(needle in p for p in problems), (needle, problems))


class DriftGateTests(unittest.TestCase):
    """The shipped kingdom specs against the shipped XML. Read-only: --check never writes."""

    def specs(self):
        return sorted(p for p in (TOOLS / "career_design").glob("*.json") if not p.name.startswith("_"))

    def marker_ids(self):
        ids = set()
        for name in ("taom_careers.xml", "taom_career_choices.xml"):
            text = (REAL_CAREER_DIR / name).read_bytes().decode("utf-8-sig")
            ids |= set(re.findall(r"<!-- career-forks:begin (\S+) sha256=", text))
        return ids

    def test_every_spec_matches_the_career_xml(self):
        specs = self.specs()
        if not specs:
            self.skipTest("no kingdom spec in tools/career_design yet: nothing to check against the XML")
        for spec in specs:
            with self.subTest(spec=spec.stem):
                out, err = io.StringIO(), io.StringIO()
                with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
                    code = gcf.main([spec.stem, "--check"])
                self.assertEqual(code, 0, f"{spec.name} drifted from the career XML; run "
                                          f"`python tools/generate_career_forks.py {spec.stem} --apply`. "
                                          + out.getvalue() + err.getvalue())

    def test_every_generated_career_belongs_to_a_spec_and_to_only_one(self):
        owners = {}
        for spec in self.specs():
            for career in json.loads(spec.read_text(encoding="utf-8-sig")).get("careers", []):
                owners.setdefault(career.get("id"), []).append(spec.stem)
        shared = {cid: stems for cid, stems in owners.items() if len(stems) > 1}
        self.assertEqual(shared, {}, "a career is in two specs")
        orphans = sorted(self.marker_ids() - set(owners))
        self.assertEqual(orphans, [], "careers carry generator markers but no spec lists them")

    def test_the_gate_catches_a_drifted_fixture(self):
        # The same two checks the shipped gate runs, against a fixture that really drifted, so the gate
        # is proven able to fail.
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        self.assertEqual(fix.run("--check")[0], 0)
        spec = good_spec()
        spec["careers"][0]["root"]["description"] = "Edited in the spec only."
        fix.write_spec(spec)
        self.assertEqual(fix.run("--check")[0], 1)


# --------------------------------------------------------------------------- #
# The plan's worked example, priced with the lead's decisions                  #
# --------------------------------------------------------------------------- #
def passive(name, description, *effects):
    return {"name": name, "description": description, "effects": list(effects)}


def effect(etype, magnitude, **extra):
    return {"type": etype, "magnitude": magnitude, **extra}


def osgiliath_spec():
    forks = [
        {"slot": 1, "name": "Steel", "options": [
            passive("Blade", "+15% melee damage.", effect("Damage", 0.15, attack_type_mask="Melee")),
            passive("Line", "Infantry hit 8% harder.", effect("TroopDamage", 0.08, troop_class="Infantry"))]},
        {"slot": 2, "name": "Standing", "options": [
            passive("Renown", "+25% renown and influence.", effect("RenownGain", 0.25), effect("InfluenceGain", 0.25)),
            passive("Provision", "Elites cost no daily upkeep.", effect("SpecialResourceUpkeepModifier", -1.0))]},
        {"slot": 3, "name": "Muster", "options": [
            passive("Host", "+50 party size.", effect("PartySize", 50)),
            passive("Lore", "Double Castar earned.", effect("SpecialResourceGain", 1.0))]},
        {"slot": 4, "name": "Resolve", "options": [
            passive("Courage", "Troops resist fear.", effect("TroopFearResistance", 0.60)),
            passive("Anchor", "Troops hold the line.", effect("TroopCrushHold", 0.50)),
            passive("Footing", "Troops stay upright.", effect("TroopKnockdownResistance", 0.50))]},
        {"slot": 5, "name": "Drill", "options": [
            passive("Drill", "Infantry gain 15 XP a day.", effect("TroopTraining", 15, troop_class="Infantry")),
            passive("Levy", "+20 party size, recruits 30% cheaper.", effect("PartySize", 20),
                    effect("RecruitCost", -0.30))]},
        {"slot": 6, "name": "Last Stand", "options": [
            passive("Ward", "You take 20% less damage.", effect("Resistance", 0.20)),
            passive("Shield wall", "Infantry take 12% less damage.", effect("TroopResistance", 0.12, troop_class="Infantry")),
            {"name": "Beacon", "description": "The ability lasts longer and reaches further.", "vu": 5,
             "mutations": [{"property": "Duration", "calculator": "flat", "value": 8, "operation": "Add"},
                           {"property": "Radius", "calculator": "flat", "value": 20, "operation": "Add"}]}]},
    ]
    career = {"id": "captain_of_osgiliath", "archetype": "infantry", "tree_version": 2,
              "point_levels": [1, 5, 10, 15, 20, 25],
              "root": {"description": "Steady in the line.",
                       "effects": [effect("Damage", 0.05, attack_type_mask="Melee")]},
              "forks": forks}
    return {"kingdom": "Gondor", "culture_ids": ["gondor", "empire_w"], "careers": [career]}


class WorkedExampleTests(unittest.TestCase):
    """Shipped rules, the real C# sets and the real special resource data, with a synthetic career row."""

    def setUp(self):
        self.rules = json.loads((TOOLS / "career_design" / "_rules.json").read_text(encoding="utf-8"))
        self.cs = gcf.load_cs_sets(REAL_SYSTEM_DIR)
        self.data = gcf.load_resource_data(REAL_RESOURCE_DIR)
        self.infos = {"captain_of_osgiliath": gcf.CareerInfo(
            "captain_of_osgiliath", "captain_of_osgiliath_ability", "captain_of_osgiliath_root",
            ["gondor", "empire_w"], [])}

    def test_the_plans_worked_example_validates(self):
        errors = gcf.validate_spec(osgiliath_spec(), self.rules, self.infos, self.cs, self.data)
        self.assertEqual(errors, [])

    def test_the_worked_example_prices_each_option_as_the_decisions_say(self):
        forks = {f["slot"]: f["options"] for f in osgiliath_spec()["careers"][0]["forks"]}
        expected = {(1, 0): 3.0, (1, 1): 3.2, (2, 0): 3.0, (2, 1): 3.0, (3, 0): 4.0, (3, 1): 4.0,
                    (4, 0): 4.0, (4, 1): 4.0, (4, 2): 4.0, (5, 0): 5.0, (5, 1): 4.6, (6, 0): 5.0, (6, 1): 4.8}
        for (slot, index), vu in expected.items():
            self.assertAlmostEqual(gcf.option_vu(forks[slot][index], self.rules), vu, delta=0.05,
                                   msg=f"fork {slot} option {index}")

    def test_the_worked_example_stays_under_every_cap(self):
        career = osgiliath_spec()["careers"][0]
        for name, result in gcf.analyze_caps(career, self.rules).items():
            self.assertLessEqual(result["max"], result["limit"] + gcf.EPS, name)
        caps = gcf.analyze_caps(career, self.rules)
        self.assertAlmostEqual(caps["TroopDamage+TroopResistance"]["max"], 0.20)
        self.assertAlmostEqual(caps["Damage+ArmorPenetration"]["max"], 0.20)

    def test_the_upgrade_cost_caps_count_the_benefit(self):
        # TroopUpgradeCost has no runtime floor: a total cut past -1 would give a negative upgrade price, so the
        # rules cap the summed cut at 0.5. Two 0.3 cuts (3 VU each, tier 1) are fine alone and 0.6 together.
        spec = osgiliath_spec()
        forks = spec["careers"][0]["forks"]
        forks[0]["options"][0]["effects"] = [effect("TroopUpgradeCost", -0.30)]
        forks[1]["options"][0]["effects"] = [effect("TroopUpgradeCost", -0.30)]
        errors = gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data)
        self.assertTrue(has(errors, "cap 'TroopUpgradeCost' exceeded", "0.6", "0.5"), errors)
        forks[1]["options"][0]["effects"] = [effect("RenownGain", 0.25), effect("InfluenceGain", 0.25)]
        self.assertEqual(gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data), [])

        spec = osgiliath_spec()
        spec["careers"][0]["forks"][0]["options"][0]["effects"] = [effect("SpecialResourceUpgradeCostModifier", -0.75)]
        errors = gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data)
        self.assertTrue(has(errors, "cap 'SpecialResourceUpgradeCostModifier' exceeded", "0.75", "0.5"), errors)
        # -0.5 at the cap passes: 2 VU alone is out of the tier 1 budget, so a RenownGain makes it 3
        spec["careers"][0]["forks"][0]["options"][0]["effects"] = [
            effect("SpecialResourceUpgradeCostModifier", -0.5), effect("RenownGain", 0.125)]
        self.assertEqual(gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data), [])

    def test_the_example_would_be_refused_if_a_cost_perk_had_the_wrong_sign(self):
        spec = osgiliath_spec()
        spec["careers"][0]["forks"][4]["options"][1]["effects"][1]["magnitude"] = 0.30
        errors = gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data)
        self.assertTrue(has(errors, "RecruitCost", "penalty"), errors)


# --------------------------------------------------------------------------- #
# Late review findings: root budget, cap coverage, archetype source, warnings   #
# --------------------------------------------------------------------------- #
class RootBudgetTests(unittest.TestCase):
    """H1: a root perk is priced like an option and refused above root_budget plus the tolerance."""

    def setUp(self):
        self.fix = FixtureFiles(self)

    def root_errors(self, effects):
        spec = good_spec()
        spec["careers"][0]["root"]["effects"] = effects
        return errors_for(self.fix, spec)

    def test_a_root_at_the_budget_plus_the_tolerance_passes(self):
        # 0.10 / 0.05 = 2.0 VU = root_budget 1.5 + budget_tolerance 0.5
        self.assertEqual(self.root_errors([effect("Damage", 0.10, attack_type_mask="Melee")]), [])

    def test_a_root_just_above_the_limit_is_refused(self):
        errs = self.root_errors([effect("Damage", 0.11, attack_type_mask="Melee")])
        self.assertTrue(has(errs, "alpha_career", "root", "2.20 VU", "root limit of 2", "root_budget 1.5 plus budget_tolerance 0.5"), errs)

    def test_a_big_root_is_refused_before_the_caps_are_summed(self):
        # 10 VU, and on its own 0.5 plus fork 1's 0.15 Damage option is 0.65 against the 0.45 cap
        errs = self.root_errors([effect("Damage", 0.5, attack_type_mask="Melee")])
        self.assertEqual(sum("root limit" in e for e in errs), 1, errs)
        self.assertFalse(has(errs, "cap '"), errs)

    def test_the_root_is_priced_with_the_class_bonus(self):
        # TroopDamage 0.12 / 0.05 = 2.4 VU, narrowed to one class: 1.6 VU, inside the budget
        self.assertEqual(self.root_errors([effect("TroopDamage", 0.12, troop_class="Infantry")]), [])
        errs = self.root_errors([effect("TroopDamage", 0.12)])
        self.assertTrue(has(errs, "root limit"), errs)

    def test_the_budget_follows_the_rules_file(self):
        rules = copy.deepcopy(TEST_RULES)
        rules["root_budget"] = 3.0
        spec = good_spec()
        spec["careers"][0]["root"]["effects"] = [effect("Damage", 0.15, attack_type_mask="Melee")]
        self.assertEqual(errors_for(self.fix, spec, rules), [])
        self.assertTrue(has(errors_for(self.fix, spec), "root limit"))

    def test_check_rules_needs_a_positive_root_budget(self):
        broken = copy.deepcopy(TEST_RULES)
        del broken["root_budget"]
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.check_rules(broken)
        self.assertIn("root_budget", str(ctx.exception))
        for bad in (0, -1, "1.5", None, True):
            with self.subTest(value=bad):
                rules = {**copy.deepcopy(TEST_RULES), "root_budget": bad}
                with self.assertRaises(gcf.GeneratorError) as ctx:
                    gcf.check_rules(rules)
                self.assertIn("root_budget", str(ctx.exception))


class RootBudgetShippedTests(unittest.TestCase):
    """The reviewer's h_root_huge against the shipped rules and the real C# sets."""

    def setUp(self):
        self.rules = json.loads((TOOLS / "career_design" / "_rules.json").read_text(encoding="utf-8"))
        self.cs = gcf.load_cs_sets(REAL_SYSTEM_DIR)
        self.data = gcf.load_resource_data(REAL_RESOURCE_DIR)
        self.infos = {"captain_of_osgiliath": gcf.CareerInfo(
            "captain_of_osgiliath", "captain_of_osgiliath_ability", "captain_of_osgiliath_root",
            ["gondor", "empire_w"], [])}

    def test_a_root_of_double_speed_and_three_hundred_health_is_refused(self):
        spec = osgiliath_spec()
        spec["careers"][0]["root"]["effects"] = [effect("MovementSpeed", 1.0), effect("Health", 300)]
        errors = gcf.validate_spec(spec, self.rules, self.infos, self.cs, self.data)
        self.assertTrue(has(errors, "captain_of_osgiliath", "root", "root limit"), errors)

    def test_the_shipped_root_budget_is_one_and_a_half(self):
        self.assertEqual(self.rules["root_budget"], 1.5)


class CapCoverageTests(unittest.TestCase):
    """H1: every priced type belongs to a cap group, so no passive pip can stack without limit. Keystone mutations are
    outside every group (only CooldownReduction has a runtime floor, min_cooldown_seconds)."""

    NEW_CAPS = {"Ammo": 0.5, "Health": 50, "HeroHealing": 1.0, "MountChargeDamage": 0.45, "MountHealth": 0.5,
                "MovementSpeed": 0.15, "PartyMovementSpeed": 0.15, "PartySpottingRange": 0.5, "SwingSpeed": 0.15}

    def setUp(self):
        self.rules = json.loads((TOOLS / "career_design" / "_rules.json").read_text(encoding="utf-8"))

    def test_check_rules_refuses_a_priced_type_in_no_cap_group(self):
        rules = copy.deepcopy(TEST_RULES)
        rules["vu_unit"]["Ammo"] = 0.1
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.check_rules(rules)
        self.assertIn("Ammo", str(ctx.exception))
        self.assertIn("cap group", str(ctx.exception))

    def test_check_rules_lists_every_uncapped_type(self):
        rules = copy.deepcopy(TEST_RULES)
        rules["vu_unit"].update({"Ammo": 0.1, "SwingSpeed": 0.05})
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.check_rules(rules)
        self.assertIn("Ammo", str(ctx.exception))
        self.assertIn("SwingSpeed", str(ctx.exception))

    def test_a_cap_group_holding_the_type_satisfies_the_check(self):
        rules = copy.deepcopy(TEST_RULES)
        rules["vu_unit"]["Ammo"] = 0.1
        rules["caps"]["Ammo"] = {"types": ["Ammo"], "limit": 0.5}
        gcf.check_rules(rules)

    def test_a_type_inside_a_shared_group_counts_as_capped(self):
        rules = copy.deepcopy(TEST_RULES)
        rules["vu_unit"]["Ammo"] = 0.1
        rules["caps"]["Damage+ArmorPenetration"]["types"].append("Ammo")
        gcf.check_rules(rules)

    def test_every_shipped_priced_type_is_in_a_cap_group(self):
        capped = {t for cap in self.rules["caps"].values() for t in cap["types"]}
        self.assertEqual(sorted(set(self.rules["vu_unit"]) - capped), [])

    def test_the_nine_new_groups_carry_the_agreed_limits_and_a_note(self):
        for etype, limit in self.NEW_CAPS.items():
            with self.subTest(type=etype):
                self.assertEqual(self.rules["caps"][etype], {"types": [etype], "limit": limit})
                self.assertIn(etype, self.rules["cap_notes"])

    def test_two_forks_that_stack_movement_speed_trip_the_new_cap(self):
        # one 0.15 pip is 3 VU, a tier 1 pick that passes every budget; two of them reach 0.30 against a 0.15 cap
        fix = FixtureFiles(self)
        spec = good_spec()
        career = spec["careers"][0]
        rules = copy.deepcopy(TEST_RULES)
        rules["vu_unit"]["MovementSpeed"] = 0.05
        rules["caps"]["MovementSpeed"] = {"types": ["MovementSpeed"], "limit": 0.15}
        cs = gcf.load_cs_sets(fix.system_dir)
        cs = cs._replace(effect_types=cs.effect_types | {"MovementSpeed"}, consumed=cs.consumed | {"MovementSpeed"})
        for slot in (1, 2):
            career["forks"][slot - 1]["options"][0] = {"name": "Swift", "description": "Faster.",
                                                       "effects": [effect("MovementSpeed", 0.15)]}
        errors = gcf.validate_spec(spec, rules, career_infos(fix), cs, gcf.load_resource_data(fix.resource_dir))
        self.assertTrue(has(errors, "cap 'MovementSpeed' exceeded"), errors)


class ArchetypeSourceTests(unittest.TestCase):
    """H3: a spec career's archetype must equal the one the ability executors use."""

    def system(self, **kw):
        root = Path(tempfile.mkdtemp(prefix="gcf-ioc-"))
        self.addCleanup(lambda: __import__("shutil").rmtree(root, ignore_errors=True))
        return write_system_dir(root, **kw)

    def test_the_map_is_read_from_the_ioc_file(self):
        self.assertEqual(gcf.load_cs_sets(self.system()).archetypes,
                         {"alpha_career": "infantry", "beta_career": "ranged", "gamma_career": "infantry"})

    def test_a_commented_out_entry_and_prose_do_not_count(self):
        archetypes = gcf.load_cs_sets(self.system()).archetypes
        self.assertNotIn("far_halftroll", archetypes)
        self.assertEqual(len(archetypes), 3)

    def test_a_changed_map_is_followed(self):
        cs = gcf.load_cs_sets(self.system(ioc=ioc_cs({"alpha_career": "Cavalry"})))
        self.assertEqual(cs.archetypes, {"alpha_career": "cavalry"})

    def test_a_block_bodied_builder_is_read_too(self):
        text = ioc_cs().replace("BuildCareerArchetypeMap() => new", "BuildCareerArchetypeMap() { return new")
        text = text.replace("    };\n}\n", "    }; }\n}\n")
        self.assertEqual(gcf.load_cs_sets(self.system(ioc=text)).archetypes["beta_career"], "ranged")

    def test_a_missing_file_is_an_error(self):
        root = self.system(drop=("CareerSystemIoC.cs",))
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(root)
        self.assertIn("CareerSystemIoC.cs", str(ctx.exception))

    def test_a_missing_builder_is_an_error(self):
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc="public static class CareerSystemIoC { }\n"))
        self.assertIn("BuildCareerArchetypeMap", str(ctx.exception))

    def test_an_empty_map_is_an_error(self):
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc=ioc_cs(body="        // nothing enabled\n")))
        self.assertIn("no career", str(ctx.exception))

    def test_an_entry_the_reader_cannot_parse_is_an_error(self):
        body = '        ["alpha_career"] = CareerArchetype.Infantry,\n        ["beta_career"] = Pick("ranged"),\n'
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc=ioc_cs(body=body)))
        self.assertIn("Pick", str(ctx.exception))

    def test_a_duplicate_key_is_an_error(self):
        body = ('        ["alpha_career"] = CareerArchetype.Infantry,\n'
                '        ["alpha_career"] = CareerArchetype.Ranged,\n')
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc=ioc_cs(body=body)))
        self.assertIn("alpha_career", str(ctx.exception))

    def test_a_hand_built_cs_sets_must_carry_the_archetype_map(self):
        # a missing map must fail loudly, not switch the archetype rule off
        with self.assertRaises(TypeError):
            gcf.CsSets(set(), set(), set(), set(), set(), set())

    def test_a_statement_after_the_initialiser_is_an_error(self):
        # the run time map differs from the initialiser: alpha_career ends up Cavalry
        block = ioc_cs().replace("BuildCareerArchetypeMap() => new", "BuildCareerArchetypeMap() { var map = new")
        written_after = block.replace(
            "    };\n}\n", '    };\n        map["alpha_career"] = CareerArchetype.Cavalry;\n        return map; }\n}\n')
        trailing_work = ioc_cs().replace(
            "BuildCareerArchetypeMap() => new", "BuildCareerArchetypeMap() { return new").replace(
            "    };\n}\n", '    };\n        Register(); }\n}\n')
        for label, text in (("written after", written_after), ("work after the return", trailing_work)):
            with self.subTest(shape=label):
                with self.assertRaises(gcf.GeneratorError) as ctx:
                    gcf.load_cs_sets(self.system(ioc=text))
                self.assertIn("BuildCareerArchetypeMap", str(ctx.exception))

    def test_a_call_chained_on_the_initialiser_is_an_error(self):
        # the run time map lacks alpha_career, the initialiser lists it
        text = ioc_cs().replace(
            "    };\n}\n", '    }.Where(p => p.Key != "alpha_career").ToDictionary(p => p.Key, p => p.Value);\n}\n')
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc=text))
        self.assertIn("BuildCareerArchetypeMap", str(ctx.exception))

    def test_a_dictionary_outside_the_builder_is_not_read(self):
        text = ("public static class CareerSystemIoC\n{\n"
                "    private static IReadOnlyDictionary<string, CareerArchetype> BuildCareerArchetypeMap() => LoadFromXml();\n\n"
                "    private static Dictionary<string, CareerArchetype> Legacy() => new Dictionary<string, CareerArchetype>\n"
                '    {\n        ["alpha_career"] = CareerArchetype.Infantry,\n    };\n}\n')
        with self.assertRaises(gcf.GeneratorError) as ctx:
            gcf.load_cs_sets(self.system(ioc=text))
        self.assertIn("BuildCareerArchetypeMap", str(ctx.exception))

    def test_an_archetype_outside_the_rules_is_an_error(self):
        cs = gcf.load_cs_sets(self.system(ioc=ioc_cs({"alpha_career": "Healer"})))
        fix = FixtureFiles(self)
        errs = gcf.validate_spec(good_spec(), TEST_RULES, career_infos(fix), cs, gcf.load_resource_data(fix.resource_dir))
        self.assertTrue(has(errs, "alpha_career", "healer", "not one of"), errs)

    def test_a_career_whose_archetype_disagrees_is_refused(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["archetype"] = "ranged"
        errs = errors_for(fix, spec)
        self.assertTrue(has(errs, "alpha_career", "archetype", "ranged", "infantry", "CareerSystemIoC.cs"), errs)

    def test_agreement_is_case_blind(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["archetype"] = "infantry"
        self.assertEqual(errors_for(fix, spec), [])

    def test_a_career_missing_from_the_map_is_refused(self):
        fix = FixtureFiles(self)
        write_system_dir(fix.system_dir, ioc=ioc_cs({"beta_career": "Ranged"}))
        errs = errors_for(fix, good_spec())
        self.assertTrue(has(errs, "alpha_career", "CareerSystemIoC.cs", "no archetype"), errs)

    def test_the_command_line_exits_2_when_the_map_cannot_be_found(self):
        fix = FixtureFiles(self)
        (fix.system_dir / "CareerSystemIoC.cs").unlink()
        fix.write_spec(good_spec())
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("CareerSystemIoC.cs", out)
        self.assertEqual(fix.snapshot(), before)

    def test_the_command_line_exits_2_for_a_disagreeing_archetype_and_writes_nothing(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["archetype"] = "cavalry"
        fix.write_spec(spec)
        before = fix.snapshot()
        code, out = fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("archetype", out)
        self.assertEqual(fix.snapshot(), before)


class ArchetypeShippedTests(unittest.TestCase):
    """Read-only against the real CareerSystemIoC.cs and the shipped Gondor spec."""

    def test_the_real_map_is_read_in_full(self):
        text = gcf.read_cs(REAL_SYSTEM_DIR / "CareerSystemIoC.cs")
        entries = re.findall(r'\[\s*"[^"]+"\s*\]\s*=\s*CareerArchetype\s*\.\s*\w+', text)
        archetypes = gcf.load_cs_sets(REAL_SYSTEM_DIR).archetypes
        self.assertGreater(len(entries), 40)
        self.assertEqual(len(archetypes), len(entries))

    def test_the_gondor_careers_agree_with_the_runtime_map(self):
        archetypes = gcf.load_cs_sets(REAL_SYSTEM_DIR).archetypes
        spec = json.loads((TOOLS / "career_design" / "gondor.json").read_text(encoding="utf-8"))
        self.assertEqual(len(spec["careers"]), 3)
        for career in spec["careers"]:
            self.assertEqual(archetypes[career["id"]], career["archetype"], career["id"])


class LanguageRowWarningTests(unittest.TestCase):
    """H4: seeding only fills missing ids, so an added key that already has a row revives its old translation."""

    def setUp(self):
        self.fix = FixtureFiles(self)
        self.fix.write_spec(good_spec())

    def test_an_added_key_with_a_row_is_named_with_its_languages_and_the_reset_step(self):
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_c"],
                                      "DE": ["taom_cf_alpha_career_f1_c", "taom_cf_alpha_career_f2_a"]})
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        warning = out[out.index("warning"):]
        self.assertIn("taom_cf_alpha_career_f1_c (DE, FR)", warning)
        self.assertIn("taom_cf_alpha_career_f2_a (DE)", warning)
        self.assertIn(gcf.GUIDE, warning)
        self.assertIn("translation_cache", warning)
        self.assertNotIn("taom_cf_alpha_career_f1_a", warning)

    def test_no_row_means_no_warning(self):
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("already has a row", out)
        self.assertNotIn("warning", out)

    def test_a_dry_run_warns_too(self):
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_c"]})
        code, out = self.fix.run()
        self.assertEqual(code, 0, out)
        self.assertIn("taom_cf_alpha_career_f1_c (FR)", out)

    def test_an_update_that_adds_no_key_stays_quiet_even_when_rows_exist(self):
        self.assertEqual(self.fix.run("--apply")[0], 0)
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_a"]})
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 44
        spec["careers"][0]["forks"][2]["options"][0]["description"] = "+44 max health."
        self.fix.write_spec(spec)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("warning", out)

    def test_a_removed_option_letter_coming_back_is_caught_on_an_update(self):
        self.assertEqual(self.fix.run("--apply")[0], 0)
        smaller = good_spec()
        smaller["careers"][0]["tree_version"] = 3
        smaller["careers"][0]["forks"][0]["options"].pop()
        self.fix.write_spec(smaller)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("warning", out)
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_c"]})
        bigger = good_spec()
        bigger["careers"][0]["tree_version"] = 4
        self.fix.write_spec(bigger)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("taom_cf_alpha_career_f1_c (FR)", out)

    def test_a_row_in_a_sibling_std_taom_file_is_reported_with_its_file(self):
        # the translator skips every key another std_taom file of the language carries, and the engine
        # reads the sibling row, so it revives an old translation just the same
        folder = self.fix.languages_dir / "FR"
        (folder / "std_taom_other_strings_FR.xml").write_text(
            '<base><strings><string id="taom_cf_alpha_career_f1_c" text="x" /></strings></base>', encoding="utf-8")
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("taom_cf_alpha_career_f1_c (FR in std_taom_other_strings_FR.xml)", out)

    def test_a_file_that_is_not_a_std_taom_file_is_not_read(self):
        (self.fix.languages_dir / "FR" / "language_data.xml").write_text(
            '<base><strings><string id="taom_cf_alpha_career_f1_c" text="x" /></strings></base>', encoding="utf-8")
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("warning", out)

    def test_a_commented_out_row_is_not_a_row(self):
        (self.fix.languages_dir / "FR" / "std_taom_career_data_strings_FR.xml").write_text(
            '<base><strings><!-- <string id="taom_cf_alpha_career_f1_c" text="x" /> --></strings></base>',
            encoding="utf-8")
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("warning", out)

    def test_the_next_steps_count_only_the_keys_without_a_row(self):
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_c"],
                                      "DE": ["taom_cf_alpha_career_f1_c", "taom_cf_alpha_career_f2_a"]})
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        # 21 generated keys (root, 6 groups, 14 options), two of them already have a row
        self.assertIn("Next steps (19 new loc keys exist in no language file yet)", out)

    def test_a_languages_folder_with_no_career_data_file_is_an_error(self):
        shutil.rmtree(self.fix.languages_dir)
        (self.fix.languages_dir / "FR").mkdir(parents=True)
        (self.fix.languages_dir / "FR" / "std_taom_other_strings_FR.xml").write_text("<base/>", encoding="utf-8")
        before = self.fix.snapshot()
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("std_taom_career_data_strings_", out)
        self.assertEqual(self.fix.snapshot(), before)

    def test_a_run_that_adds_no_key_never_reads_the_languages_folder(self):
        self.assertEqual(self.fix.run("--apply")[0], 0)
        shutil.rmtree(self.fix.languages_dir)
        self.assertEqual(self.fix.run("--check")[0], 0)

    def test_a_missing_languages_folder_is_an_error_when_a_key_was_added(self):
        import shutil
        shutil.rmtree(self.fix.languages_dir)
        before = self.fix.snapshot()
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 2, out)
        self.assertIn("Languages", out)
        self.assertEqual(self.fix.snapshot(), before)


class StaleTooltipWarningTests(unittest.TestCase):
    """H5: on an update, an option whose effects changed but whose description did not may state an old number."""

    def setUp(self):
        self.fix = FixtureFiles(self)
        self.fix.write_spec(good_spec())
        self.assertEqual(self.fix.run("--apply")[0], 0)

    def update(self, mutate):
        spec = good_spec()
        mutate(spec["careers"][0])
        self.fix.write_spec(spec)
        return self.fix.run("--apply")

    def test_a_magnitude_change_under_the_same_text_warns_with_both_values(self):
        code, out = self.update(lambda c: c["forks"][2]["options"][0]["effects"][0].__setitem__("magnitude", 44))
        self.assertEqual(code, 0, out)
        line = next(l for l in out.splitlines() if "alpha_career_f3_a" in l and "description" in l)
        self.assertIn("Health 40", line)
        self.assertIn("Health 44", line)
        self.assertIn("+40 max health.", line)

    def test_a_changed_description_clears_the_warning(self):
        def mutate(c):
            option = c["forks"][2]["options"][0]
            option["effects"][0]["magnitude"] = 44
            option["description"] = "+44 max health."
        code, out = self.update(mutate)
        self.assertEqual(code, 0, out)
        self.assertNotIn("description reads unchanged", out)

    def test_a_text_only_change_does_not_warn(self):
        code, out = self.update(lambda c: c["forks"][2]["options"][0].__setitem__("description", "Bigger body."))
        self.assertEqual(code, 0, out)
        self.assertNotIn("description reads unchanged", out)

    def test_a_keystone_value_change_warns(self):
        code, out = self.update(lambda c: c["forks"][1]["options"][2]["mutations"][0].__setitem__("value", 4))
        self.assertEqual(code, 0, out)
        line = next(l for l in out.splitlines() if "alpha_career_f2_c" in l and "description" in l)
        self.assertIn("Radius flat 3", line)
        self.assertIn("Radius flat 4", line)

    def test_every_stale_option_is_listed(self):
        def mutate(c):
            c["forks"][2]["options"][0]["effects"][0]["magnitude"] = 44
            c["forks"][2]["options"][1]["effects"][0]["magnitude"] = 0.43
        code, out = self.update(mutate)
        self.assertEqual(code, 0, out)
        self.assertIn("alpha_career_f3_a", out)
        self.assertIn("alpha_career_f3_b", out)

    def test_an_unchanged_spec_does_not_warn(self):
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("description reads unchanged", out)

    def test_a_first_adoption_has_nothing_to_compare_and_does_not_warn(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("description reads unchanged", out)

    def test_a_re_adoption_warns_like_an_update(self):
        # the recovery path the refusals prescribe: delete both markers, raise tree_version, run again
        for path in (self.fix.careers_path, self.fix.choices_path):
            text = re.sub(r"^[ \t]*<!-- career-forks:(?:begin|end) [^\n]*-->[ \t]*\n", "", self.fix.lf(path),
                          flags=re.M)
            self.assertNotIn("career-forks:", text)
            path.write_bytes(self.fix.encode(text))
        spec = good_spec()
        spec["careers"][0]["tree_version"] = 3
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 44
        self.fix.write_spec(spec)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("taom_career_choices.xml adopt", out)
        line = next(l for l in out.splitlines() if "alpha_career_f3_a" in l and "description" in l)
        self.assertIn("Health 40", line)
        self.assertIn("Health 44", line)

    def test_a_respelled_mask_or_class_does_not_warn(self):
        def spec(mask, classes):
            s = good_spec()
            s["careers"][0]["root"]["effects"][0]["attack_type_mask"] = mask
            s["careers"][0]["forks"][2]["options"][0]["effects"] = [
                {"type": "TroopTraining", "magnitude": 8, "troop_class": classes}]
            return s
        fix = FixtureFiles(self)
        fix.write_spec(spec("Melee,Ranged", "Infantry,Ranged,Cavalry"))
        self.assertEqual(fix.run("--apply")[0], 0)
        fix.write_spec(spec("All", "All"))
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("applied", out)           # the respelling is written, it is only not a change of meaning
        self.assertNotIn("description reads unchanged", out)

    def test_reordered_effects_do_not_warn(self):
        def spec(effects):
            s = good_spec()
            s["careers"][0]["root"]["effects"] = effects
            return s
        melee, health = effect("Damage", 0.03, attack_type_mask="Melee"), effect("Health", 5)
        fix = FixtureFiles(self)
        fix.write_spec(spec([melee, health]))
        self.assertEqual(fix.run("--apply")[0], 0)
        fix.write_spec(spec([health, melee]))
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("applied", out)
        self.assertNotIn("description reads unchanged", out)

    def test_a_changed_mutation_calculator_warns_and_a_changed_operation_does_not(self):
        def tree(calculator, operation):
            return ('<Choice id="c_f2_c" type="Keystone" description="{=taom_cf_c_f2_c}Radius up.">'
                    f'<Mutations><Mutation target_id="t" property="Radius" calculator="{calculator}" value="3" '
                    f'operation="{operation}" /></Mutations></Choice>')
        stale = gcf.stale_tooltips(tree("flat", "Add"), tree("multiply", "Add"))
        self.assertEqual([(cid, old, new) for cid, old, new, _ in stale],
                         [("c_f2_c", ["Radius flat 3"], ["Radius multiply 3"])])
        self.assertEqual(gcf.stale_tooltips(tree("flat", "Add"), tree("flat", "Replace")), [])

    def test_a_changed_magnitude_in_a_reordered_pair_still_warns(self):
        def spec(effects):
            s = good_spec()
            s["careers"][0]["root"]["effects"] = effects
            return s
        fix = FixtureFiles(self)
        fix.write_spec(spec([effect("Damage", 0.03, attack_type_mask="Melee"), effect("Health", 5)]))
        self.assertEqual(fix.run("--apply")[0], 0)
        fix.write_spec(spec([effect("Health", 5), effect("Damage", 0.04, attack_type_mask="Melee")]))
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn("alpha_career_root", out)
        self.assertIn("description reads unchanged", out)

    def test_the_warning_prints_in_check_mode_too(self):
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 44
        self.fix.write_spec(spec)
        code, out = self.fix.run("--check")
        self.assertEqual(code, 1, out)
        self.assertIn("alpha_career_f3_a", out)

    def test_the_root_is_covered(self):
        code, out = self.update(lambda c: c["root"]["effects"][0].__setitem__("magnitude", 0.06))
        self.assertEqual(code, 0, out)
        self.assertIn("alpha_career_root", out)


class StringsToolStepTests(unittest.TestCase):
    """The English source (taom_career_data_strings.xml) is regenerated by the strings tool; a run that edits or
    removes a loc key leaves it stale, and rows reset to the new English would then read as translated."""

    STRINGS_STEP = "generate_name_localization_strings.py --apply --category career_data"

    def setUp(self):
        self.fix = FixtureFiles(self)
        self.fix.write_spec(good_spec())
        self.assertEqual(self.fix.run("--apply")[0], 0)

    def test_an_english_only_edit_prints_the_strings_step_but_no_seeding(self):
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["description"] = "Bigger body."
        self.fix.write_spec(spec)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn(self.STRINGS_STEP, out)
        self.assertNotIn("--seed-only", out)
        self.assertNotIn("new loc keys", out)

    def test_a_removal_only_run_prints_the_strings_step_but_no_seeding(self):
        spec = good_spec()
        spec["careers"][0]["tree_version"] = 3
        spec["careers"][0]["forks"][0]["options"].pop()
        self.fix.write_spec(spec)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn(self.STRINGS_STEP, out)
        self.assertNotIn("--seed-only", out)

    def test_a_numbers_only_edit_prints_no_strings_step(self):
        spec = good_spec()
        spec["careers"][0]["forks"][2]["options"][0]["effects"][0]["magnitude"] = 41
        self.fix.write_spec(spec)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn(self.STRINGS_STEP, out)

    def test_a_run_whose_every_added_key_already_has_a_row_still_prints_the_strings_step(self):
        smaller = good_spec()
        smaller["careers"][0]["tree_version"] = 3
        smaller["careers"][0]["forks"][0]["options"].pop()
        self.fix.write_spec(smaller)
        self.assertEqual(self.fix.run("--apply")[0], 0)
        self.fix.write_language_rows({"FR": ["taom_cf_alpha_career_f1_c"]})
        bigger = good_spec()
        bigger["careers"][0]["tree_version"] = 4
        self.fix.write_spec(bigger)
        code, out = self.fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertIn(self.STRINGS_STEP, out)
        self.assertNotIn("--seed-only", out)
        self.assertNotIn("exist in no language file yet", out)


class MutationOperationTests(unittest.TestCase):
    """NIT: the loader defaults a mutation's operation to Add (CareerConfigProvider), so the spec may omit it."""

    def keystone_without_operation(self):
        spec = good_spec()
        spec["careers"][0]["forks"][1]["options"][2]["mutations"][0].pop("operation")
        return spec

    def test_a_mutation_without_operation_validates(self):
        fix = FixtureFiles(self)
        self.assertEqual(errors_for(fix, self.keystone_without_operation()), [])

    def test_the_output_omits_operation_when_the_spec_does(self):
        fix = FixtureFiles(self)
        fix.write_spec(self.keystone_without_operation())
        self.assertEqual(fix.run("--apply")[0], 0)
        mutation = re.search(r"<Mutation [^>]*>", fix.lf(fix.choices_path)).group(0)
        self.assertNotIn("operation", mutation)
        self.assertIn('property="Radius"', mutation)

    def test_the_output_keeps_operation_when_the_spec_states_it(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        self.assertIn('value="3" operation="Add" />', fix.lf(fix.choices_path))

    def test_a_stated_operation_is_still_checked(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["forks"][1]["options"][2]["mutations"][0]["operation"] = "Multiply"
        self.assertTrue(has(errors_for(fix, spec), "operation", "Multiply"))

    def test_the_other_mutation_fields_stay_required(self):
        fix = FixtureFiles(self)
        for key in ("property", "calculator", "value"):
            with self.subTest(key=key):
                spec = good_spec()
                spec["careers"][0]["forks"][1]["options"][2]["mutations"][0].pop(key)
                self.assertTrue(has(errors_for(fix, spec), key, "required"))

    def test_the_two_spellings_are_not_a_structural_change(self):
        fix = FixtureFiles(self)
        fix.write_spec(good_spec())
        self.assertEqual(fix.run("--apply")[0], 0)
        fix.write_spec(self.keystone_without_operation())
        code, out = fix.run("--apply")
        self.assertEqual(code, 0, out)
        self.assertNotIn("operation", re.search(r"<Mutation [^>]*>", fix.lf(fix.choices_path)).group(0))
        self.assertNotIn("description reads unchanged", out)


class TroopClassAllTests(unittest.TestCase):
    """NIT: troop_class "All" means every class, the same as omitting it, and the C# parser accepts it."""

    def spec_with(self, troop_class):
        spec = good_spec()
        effect_ = {"type": "TroopTraining", "magnitude": 8}
        if troop_class is not None:
            effect_["troop_class"] = troop_class
        spec["careers"][0]["forks"][2]["options"][0]["effects"] = [effect_]
        return spec

    def test_all_validates_in_any_case(self):
        fix = FixtureFiles(self)
        for spelling in ("All", "all", "ALL"):
            with self.subTest(spelling=spelling):
                self.assertEqual(errors_for(fix, self.spec_with(spelling)), [])

    def test_all_is_priced_like_an_omitted_class(self):
        option = self.spec_with("All")["careers"][0]["forks"][2]["options"][0]
        omitted = self.spec_with(None)["careers"][0]["forks"][2]["options"][0]
        self.assertEqual(gcf.option_vu(option, TEST_RULES), gcf.option_vu(omitted, TEST_RULES))
        self.assertEqual(gcf.option_vu(option, TEST_RULES), 4.0)

    def test_all_next_to_a_class_is_all_classes(self):
        effect_ = {"type": "TroopTraining", "magnitude": 8, "troop_class": "Infantry, All"}
        self.assertFalse(gcf.narrowed(effect_, TEST_RULES))

    def test_all_is_not_narrowed_in_the_archetype_locked_slots(self):
        fix = FixtureFiles(self)
        spec = good_spec()
        spec["careers"][0]["archetype"] = "infantry"
        spec["careers"][0]["forks"][0]["options"][0]["effects"] = [
            {"type": "TroopTraining", "magnitude": 6, "troop_class": "All"}]
        self.assertEqual([e for e in errors_for(fix, spec) if "archetype" in e], [])

    def test_all_is_written_as_all(self):
        fix = FixtureFiles(self)
        fix.write_spec(self.spec_with("all"))
        self.assertEqual(fix.run("--apply")[0], 0)
        self.assertIn('<PassiveEffect type="TroopTraining" magnitude="8" troop_class="All" />',
                      fix.lf(fix.choices_path))

    def test_all_keeps_the_shape_of_an_omitted_class(self):
        every = {"type": "TroopTraining", "magnitude": 8}
        self.assertEqual(gcf.effect_shape({**every, "troop_class": "All"}), gcf.effect_shape(every))

    def test_a_bad_name_is_still_refused(self):
        fix = FixtureFiles(self)
        self.assertTrue(has(errors_for(fix, self.spec_with("Navy")), "Navy", "troop_class"))
        self.assertTrue(has(errors_for(fix, self.spec_with("Allies")), "Allies", "troop_class"))


if __name__ == "__main__":
    unittest.main()
