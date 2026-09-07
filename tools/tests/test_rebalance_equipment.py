#!/usr/bin/env python3
"""Unit tests for equipment-driven weapon specialization in
tools/rebalance_troops.py + the item-class registry in tools/taom_schema.py.

Run:  python -m unittest discover -s tools/tests -p "test_*.py"
  or:  python tools/tests/test_rebalance_equipment.py

Synthetic data only (no game install). Encodes the fix contract for the
skill-vs-equipment mismatch bug class: crossbowmen named "Sharpshooter" got
Bow-top skills and two-hander troops named "Knight" got Polearm-top skills,
because detection was name-keyword-only.
"""
import os
import re
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import rebalance_troops as rb  # noqa: E402
import taom_schema as ts       # noqa: E402


class ItemClassRegistryTests(unittest.TestCase):
    """build_item_class_registry must read BOTH vanilla <Item Type="..."> and
    Armory <CraftedItem crafting_template="..."> (no two-hander anywhere in the
    install uses a Type attribute)."""

    def test_parses_both_item_shapes(self):
        with tempfile.TemporaryDirectory() as tmp:
            md = Path(tmp) / "ModuleData"
            md.mkdir(parents=True)
            (md / "items.xml").write_text("""<?xml version="1.0"?>
<Items>
  <Item id="vanilla_xbow" Type="Crossbow" name="x" />
  <Item
      id="vanilla_polearm"
      Type="Polearm" />
  <CraftedItem id="taom_greatsword" crafting_template="TwoHandedSword" culture="Culture.gondor" />
  <CraftedItem id="taom_pike" crafting_template="Pike" />
  <Item id="some_helmet" Type="HeadArmor" />
  <!-- <Item id="commented_out" Type="Bow" /> -->
</Items>
""", encoding="utf-8")
            classes = ts.build_item_class_registry(md, None)
        self.assertEqual(classes.get("vanilla_xbow"), "Crossbow")
        self.assertEqual(classes.get("vanilla_polearm"), "Polearm")
        self.assertEqual(classes.get("taom_greatsword"), "TwoHanded")
        self.assertEqual(classes.get("taom_pike"), "Polearm")
        self.assertNotIn("some_helmet", classes, "non-weapon types must be omitted")
        self.assertNotIn("commented_out", classes, "commented defs must be ignored")


def _npc(equipment_ids):
    """Minimal NPCCharacter element with the given Item-slot equipment ids."""
    eq_lines = "".join(
        f'<equipment slot="Item{i}" id="Item.{iid}" />' for i, iid in enumerate(equipment_ids))
    xml = f"""<NPCCharacter id="t" name="T" level="31" default_group="Ranged">
  <Equipments><EquipmentRoster>{eq_lines}
    <equipment slot="Head" id="Item.helmet_a" />
  </EquipmentRoster></Equipments>
</NPCCharacter>"""
    return ET.fromstring(xml)


ITEM_CLASSES = {
    "xbow_a": "Crossbow", "bolt_a": "Bolts", "bow_a": "Bow", "arrows_a": "Arrows",
    "sword_1h": "OneHanded", "sword_2h": "TwoHanded", "spear_a": "Polearm",
    "javelin_a": "Throwing", "shield_a": "Shield",
}


class WeaponClassExtractionTests(unittest.TestCase):
    def test_collects_weapon_slots_only(self):
        npc = _npc(["xbow_a", "bolt_a", "sword_1h"])
        wc = rb.troop_weapon_classes(npc, ITEM_CLASSES)
        self.assertEqual(wc, {"Crossbow", "Bolts", "OneHanded"})


class CrossbowSwapTests(unittest.TestCase):
    """The Bow<->Crossbow swap must fire on EQUIPMENT, not name."""

    def test_sharpshooter_with_crossbow_swaps(self):
        # The reported bug: "Sharpshooter" (no crossbow keyword) carrying a crossbow.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "tol_sharpshooter", "Tolfalas Sharpshooter",
            weapon_classes={"Crossbow", "Bolts", "OneHanded"})
        self.assertGreater(skills["Crossbow"], skills["Bow"],
                           "crossbow-armed troop must be Crossbow-top")

    def test_crossbowman_named_troop_with_bow_does_not_swap(self):
        # Name says crossbow, equipment says bow -> equipment wins, no swap.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "x_crossbowman", "Crossbowman",
            weapon_classes={"Bow", "Arrows"})
        self.assertGreater(skills["Bow"], skills["Crossbow"])

    def test_name_fallback_without_equipment_data(self):
        # weapon_classes=None (no game install) -> name keywords still work.
        skills = rb.calculate_skills(
            "gondor", 31, "Ranged", "x_crossbowman", "Crossbowman", weapon_classes=None)
        self.assertGreater(skills["Crossbow"], skills["Bow"])

    def test_naffatun_keyword_removed_from_fallback(self):
        # 'naffatun' wrongly swapped javelin throwers; it must not fire by name.
        skills = rb.calculate_skills(
            "rhun", 31, "Ranged", "sagarun_naffatun", "Naffatun",
            weapon_classes={"OneHanded", "Throwing"})
        self.assertGreater(skills["Bow"], skills["Crossbow"])


class MeleeSanitySwapTests(unittest.TestCase):
    """A two-hander-only troop must not have Polearm as top melee skill."""

    def test_knight_with_two_hander_swaps(self):
        # The reported bug: cavalry "Hill-Knight" carrying only a 2H sword.
        base = rb.CAVALRY_BASELINES[41]
        self.assertGreater(base["Polearm"], base["TwoHanded"],
                           "precondition: cavalry baseline is polearm-biased")
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes={"TwoHanded"})
        self.assertGreater(skills["TwoHanded"], skills["Polearm"])

    def test_swap_is_total_preserving(self):
        with_swap = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes={"TwoHanded"})
        without = rb.calculate_skills(
            "gondor", 41, "Cavalry", "arn_hill_knight", "Arndir Hill-Knight",
            weapon_classes=None)
        self.assertEqual(sorted(with_swap.values()), sorted(without.values()))

    def test_mixed_carrier_untouched(self):
        # Carries both a 2H and a polearm -> baseline ordering stands.
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "t", "Mixed Guard",
            weapon_classes={"TwoHanded", "Polearm"})
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_polearm_carrier_untouched(self):
        skills = rb.calculate_skills(
            "gondor", 41, "Cavalry", "t", "Lancer",
            weapon_classes={"Polearm", "OneHanded", "Shield"})
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_idempotent(self):
        # The swap condition (Polearm > TwoHanded) is false after swapping, so
        # recomputing from the same inputs yields the same output.
        a = rb.calculate_skills("gondor", 41, "Cavalry", "t", "Knight",
                                weapon_classes={"TwoHanded"})
        b = rb.calculate_skills("gondor", 41, "Cavalry", "t", "Knight",
                                weapon_classes={"TwoHanded"})
        self.assertEqual(a, b)
        self.assertGreater(a["TwoHanded"], a["Polearm"])


if __name__ == "__main__":
    unittest.main()
class ThrowingArcherParityTests(unittest.TestCase):
    """A troop whose ONLY ranged option is a thrown weapon takes the Bow curve on
    Throwing (#554). The sibling Bow<->Crossbow swap above shipped with four tests
    and this rule shipped with none, which is how the deep review found it."""

    def test_thrown_primary_troop_takes_the_ranged_bow_curve(self):
        skills = rb.calculate_skills(
            "gondor", 26, "Infantry", "gondor_har_javelineer", "Harondor Javelineer",
            weapon_classes={"Throwing", "OneHanded", "Shield"})
        expected = rb.RANGED_BASELINES[26]["Bow"] + rb.CULTURAL_MODS["gondor"].get("Bow", 0)
        self.assertEqual(skills["Throwing"], expected)

    def test_parity_reaches_past_the_troops_own_group_table(self):
        # The point of the rule: an Infantry troop's own Bow column is 15-25, so there
        # is no ranged number in its own table to swap. Guards a regression to s["Bow"].
        base = rb.INFANTRY_BASELINES[26]
        skills = rb.calculate_skills(
            "gondor", 26, "Infantry", "gondor_har_javelineer", "Harondor Javelineer",
            weapon_classes={"Throwing", "OneHanded"})
        self.assertGreater(skills["Throwing"], base["Throwing"])
        self.assertGreater(skills["Throwing"], skills["Bow"])

    def test_borrowed_value_carries_the_bow_modifier_not_the_throwing_one(self):
        # rhun is the discriminating culture: Bow -10, Throwing -5. Taking the wrong
        # modifier yields 200 instead of 195 and no other test would notice.
        self.assertEqual(rb.CULTURAL_MODS["rhun"]["Bow"], -10)
        self.assertEqual(rb.CULTURAL_MODS["rhun"]["Throwing"], -5)
        skills = rb.calculate_skills(
            "rhun", 31, "Infantry", "sagarun_naffatun", "Sagarun Naffatun",
            weapon_classes={"Throwing", "OneHanded"})
        self.assertEqual(skills["Throwing"], rb.RANGED_BASELINES[31]["Bow"] - 10)

    def test_rule_does_not_fire_for_a_troop_outside_the_set(self):
        # 87 other troops carry a thrown weapon. Until the Armory install is repaired
        # (#555) the trigger is an explicit id set, so they must be untouched.
        self.assertNotIn("gondor_pel_anchor_guard", rb.THROWN_PRIMARY_TROOP_IDS)
        skills = rb.calculate_skills(
            "gondor", 26, "Infantry", "gondor_pel_anchor_guard", "Anchor Guard",
            weapon_classes={"Throwing", "Polearm"})
        expected = rb.INFANTRY_BASELINES[26]["Throwing"] + rb.CULTURAL_MODS["gondor"]["Throwing"]
        self.assertEqual(skills["Throwing"], expected)

    def test_rule_stays_silent_when_the_javelin_did_not_classify(self):
        # The crippled-registry case (#555): LOTRLOME_Armory ModuleData is empty on some
        # installs, so a weapon can fail to classify. The rule must no-op rather than
        # write a number derived from a registry it could not read.
        baseline = rb.INFANTRY_BASELINES[26]["Throwing"] + rb.CULTURAL_MODS["gondor"]["Throwing"]
        for classes in ({"OneHanded"}, set(), None):
            skills = rb.calculate_skills(
                "gondor", 26, "Infantry", "gondor_har_javelineer", "Harondor Javelineer",
                weapon_classes=classes)
            self.assertEqual(skills["Throwing"], baseline,
                             "rule fired with weapon_classes=%r" % (classes,))

    def test_every_thrown_primary_id_has_a_ranged_baseline_for_its_level(self):
        # RANGED_BASELINES has no level 1 row while INFANTRY_BASELINES does, and the
        # lookup fails soft. A level-1 thrown-primary troop would lose the rule with no
        # error. This also catches an id renamed out from under the set.
        levels = {}
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            for npc in ET.parse(str(path)).getroot().iter("NPCCharacter"):
                if npc.get("id") in rb.THROWN_PRIMARY_TROOP_IDS:
                    levels[npc.get("id")] = int(npc.get("level"))
        missing = sorted(set(rb.THROWN_PRIMARY_TROOP_IDS) - set(levels))
        self.assertEqual(missing, [],
                         "THROWN_PRIMARY_TROOP_IDS names a troop no file defines: %s" % missing)
        for tid, level in sorted(levels.items()):
            self.assertIn(level, rb.RANGED_BASELINES,
                          "%s is level %d, which RANGED_BASELINES has no row for, so the "
                          "archer-parity rule would silently no-op" % (tid, level))


class RespecializationExemptionMirrorTests(unittest.TestCase):
    """RESPECIALIZATION_EXEMPT_EDGES is hand-copied into three files in two languages.
    All three comments say they must agree; nothing enforced it until this test. The
    militia mirror next door earned its pinning test the same way (#554)."""

    CSHARP_TEST = (Path(__file__).resolve().parents[2] / "TAOM.Tests" / "Features"
                   / "TroopProgression" / "TroopUpgradeSkillMonotonicityTests.cs")

    ENTRY_RE = re.compile(
        r"\[\(\"(?P<src>[^\"]+)\",\s*\"(?P<dst>[^\"]+)\"\)\]\s*=\s*"
        r"new\s+HashSet<string>\([^)]*\)\s*\{(?P<skills>[^}]*)\}")

    def test_python_writer_and_validator_agree(self):
        self.assertEqual(rb.RESPECIALIZATION_EXEMPT_EDGES,
                         ts.Validator._RESPECIALIZATION_EXEMPT_EDGES,
                         "rebalance_troops.py floors a value that taom_schema.py then "
                         "reports as a regression, or the reverse")

    def test_csharp_gate_agrees_with_the_python_pair(self):
        self.assertTrue(self.CSHARP_TEST.is_file(), str(self.CSHARP_TEST))
        text = self.CSHARP_TEST.read_text(encoding="utf-8-sig")
        found = {(m.group("src"), m.group("dst")):
                 set(re.findall(r"\"([^\"]+)\"", m.group("skills")))
                 for m in self.ENTRY_RE.finditer(text)}
        self.assertEqual(found, rb.RESPECIALIZATION_EXEMPT_EDGES,
                         "TroopUpgradeSkillMonotonicityTests.cs has drifted from "
                         "rebalance_troops.py; the writer and the CI gate would judge the "
                         "same upgrade edge differently")

    def test_exempt_edges_name_troops_that_still_exist(self):
        # The _BODYLESS_BY_DESIGN staleness test next door exists for this reason: an
        # allowlist keyed on ids rots silently when a troop is renamed or deleted.
        ids = set()
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            ids.update(n.get("id") for n in ET.parse(str(path)).getroot().iter("NPCCharacter"))
        named = {t for edge in rb.RESPECIALIZATION_EXEMPT_EDGES for t in edge}
        self.assertEqual(sorted(named - ids), [], "exempt edge names a troop no file defines")

    def test_exemption_is_pinned_by_identity(self):
        # Pinned, not counted: a second entry added to all three files in lockstep would
        # still pass every test above. Widening the exemption must be deliberate.
        self.assertEqual(
            rb.RESPECIALIZATION_EXEMPT_EDGES,
            {("sagarun_crossbowman", "sagarun_naffatun"): {"Bow", "Crossbow"}},
            "The set of upgrade edges allowed to drop a parent skill changed. Each entry "
            "suppresses a real ladder regression, so add one only with a recorded reason "
            "and update this assertion deliberately.")
