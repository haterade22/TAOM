#!/usr/bin/env python3
"""Unit tests for the equipment-driven role table in tools/rebalance_troops.py + the item-class
registry in tools/taom_schema.py.

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
import troop_purpose as tp     # noqa: E402


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


def _kit(*sets):
    return tp.kit_of([{f"Item{i}": c for i, c in enumerate(s)} for s in sets])


class CrossbowTests(unittest.TestCase):
    """Bow against Crossbow follows the EQUIPMENT, never the name (#340)."""

    def test_sharpshooter_with_crossbow_is_crossbow_top(self):
        # The reported bug: "Sharpshooter" (no crossbow keyword) carrying a crossbow.
        skills = rb.calculate_skills("gondor", 31, "tol_sharpshooter", _kit(("Crossbow", "Bolts", "OneHanded")))
        self.assertGreater(skills["Crossbow"], skills["Bow"], "crossbow-armed troop must be Crossbow-top")

    def test_crossbowman_named_troop_with_bow_is_bow_top(self):
        skills = rb.calculate_skills("gondor", 31, "x_crossbowman", _kit(("Bow", "Arrows")))
        self.assertGreater(skills["Bow"], skills["Crossbow"])

    def test_a_thrower_carrying_no_launcher_leads_with_neither(self):
        # 'naffatun' once swapped javelin throwers by name; with no launcher both are uncarried.
        skills = rb.calculate_skills("rhun", 31, "sagarun_naffatun", _kit(("Throwing", "OneHanded")))
        self.assertEqual(skills["Bow"], skills["Crossbow"])


class MeleeTests(unittest.TestCase):
    """A two-hander-only troop must not have Polearm as top melee skill (#341)."""

    def test_knight_with_two_hander_is_two_hander_top(self):
        # The reported bug: cavalry "Hill-Knight" carrying only a 2H sword.
        skills = rb.calculate_skills("gondor", 41, "arn_hill_knight", _kit(("TwoHanded",)), mounted=True)
        self.assertGreater(skills["TwoHanded"], skills["Polearm"])

    def test_mixed_carrier_follows_item0(self):
        skills = rb.calculate_skills("gondor", 41, "t", _kit(("Polearm", "TwoHanded")), mounted=True)
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_polearm_carrier(self):
        skills = rb.calculate_skills("gondor", 41, "t", _kit(("Polearm", "OneHanded", "Shield")), mounted=True)
        self.assertGreater(skills["Polearm"], skills["TwoHanded"])

    def test_idempotent(self):
        a = rb.calculate_skills("gondor", 41, "t", _kit(("TwoHanded",)), mounted=True)
        b = rb.calculate_skills("gondor", 41, "t", _kit(("TwoHanded",)), mounted=True)
        self.assertEqual(a, b)

    def test_no_name_is_read(self):
        # A "Spearman" holding a sword once took 15 points onto a spear he did not hold (#343).
        sword = _kit(("OneHanded", "Shield"))
        self.assertEqual(rb.calculate_skills("gondor", 31, "gondor_x_spearman", sword),
                         rb.calculate_skills("gondor", 31, "gondor_x_guard", sword))


class PurposeFormulaTests(unittest.TestCase):
    """calculate_skills follows the kit (#343): the Item0 class takes the troop's highest melee or
    launcher value."""

    def test_a_sword_troop_of_a_polearm_culture_keeps_its_sword_on_top(self):
        self.assertGreater(rb.CULTURE_PCT["rohan"]["Polearm"], rb.CULTURE_PCT["rohan"]["OneHanded"], "precondition")
        s = rb.calculate_skills("rohan", 26, "rohan_x_guard", _kit(("OneHanded", "Shield")))
        self.assertGreater(s["OneHanded"], s["Polearm"])

    def test_an_archer_with_a_two_hander_fights_with_its_second_skill(self):
        s = rb.calculate_skills("rivendell", 31, "x_archer", _kit(("Bow", "Arrows", "TwoHanded")))
        self.assertGreater(s["TwoHanded"], s["OneHanded"])
        self.assertGreater(s["Bow"], s["TwoHanded"])

    def test_alternative_side_weapons_tie_on_the_curve_too(self):
        # One set carries a two handed sword where the other carries a one handed sword: both
        # take the higher value, so --apply cannot undo what --fix-purpose writes.
        s = rb.calculate_skills("lothlorien", 31, "x_archer",
                                _kit(("Bow", "Arrows", "TwoHanded"), ("Bow", "Arrows", "OneHanded")))
        self.assertEqual(s["TwoHanded"], s["OneHanded"])

    def test_the_result_is_a_fixed_point_of_the_purpose_rule(self):
        kit = _kit(("Polearm", "Shield", "OneHanded"), ("OneHanded", "Shield"))
        s = rb.calculate_skills("dale", 36, "t", kit)
        self.assertEqual(tp.tie_raise(tp.purpose_skills(s, kit), kit), s)
        self.assertEqual(tp.kit_mismatches(s, kit), [])


class ThrowerTests(unittest.TestCase):
    """A troop whose ONLY ranged option is a thrown weapon takes the main number on Throwing
    (#554). The trigger is an explicit id set (THROWN_PRIMARY_TROOP_IDS)."""

    def test_thrown_primary_troop_takes_the_main_number(self):
        s = rb.calculate_skills("gondor", 26, "gondor_har_javelineer", _kit(("OneHanded", "Throwing", "Shield")))
        self.assertEqual(s["Throwing"], rb.round5(130 * rb.Fraction(95, 100)))

    def test_rule_does_not_fire_for_a_troop_outside_the_set(self):
        self.assertNotIn("gondor_pel_anchor_guard", rb.THROWN_PRIMARY_TROOP_IDS)
        s = rb.calculate_skills("gondor", 26, "gondor_pel_anchor_guard", _kit(("Polearm", "Throwing")))
        self.assertEqual(s["Throwing"], rb.round5(130 * rb.SIDEARM * rb.Fraction(95, 100)))

    def test_rule_stays_silent_when_the_javelin_did_not_classify(self):
        # The crippled-registry case (#555): a weapon can fail to classify. The rule must not
        # write a number derived from a registry it could not read.
        s = rb.calculate_skills("gondor", 26, "gondor_har_javelineer", _kit(("OneHanded",)))
        self.assertEqual(s["Throwing"], rb.round5(130 * rb.UNCARRIED_RANGED))

    def test_every_thrown_primary_id_names_a_troop(self):
        # Catches an id renamed out from under the set.
        ids = set()
        for path in sorted(Path(rb.TROOPS_DIR).glob("troops_*.xml")):
            ids.update(n.get("id") for n in ET.parse(str(path)).getroot().iter("NPCCharacter"))
        missing = sorted(set(rb.THROWN_PRIMARY_TROOP_IDS) - ids)
        self.assertEqual(missing, [], "THROWN_PRIMARY_TROOP_IDS names a troop no file defines: %s" % missing)


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


if __name__ == "__main__":
    unittest.main()
